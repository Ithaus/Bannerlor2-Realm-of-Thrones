using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 453 (pakiet 7 krok 7.4 audytu AI bitwy; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"). FALE POSILKOW IDA NA PAS Z PRZYCZOLKIEM.
    /// Dowod (dekompilacja 1.4.8, rally_dec/mb_full.cs): posilki dostaja szyk w MissionReinforcementsHelper.GetReinforcementAssignments (85166-85216):
    /// wsrod szykow tej samej klasy wygrywa GetFormationReinforcementScore = 0.6 * (1 - udzial szyku w druzynie) + 0.4 * bliskosc do punktu posilkow
    /// (85246-85259) - czyli szyk NAJBARDZIEJ WYKRWAWIONY. W szturmie szyki piechoty to szyki pasow (TacticBreachWalls), wiec fala idzie na pas, ktory
    /// najwiecej stracil (lejek), a nie tam, gdzie nasi stoja na murze. Gra sama nie przesuwa ich dalej: BalanceAssaultLanes (37223-37244) obejmuje tylko
    /// szyki w UseSiegeMachines / WaitForLadders, a Formation.TransferUnitsAux odmawia, gdy szyk zrodlowy ma kogos w zamku (70755-70760, IsSplittableByAI
    /// = IsConvenientForTransfer, 69077-69122) i sama wybiera ludzi (najblizszych celu, nie swiezych). Dlatego reczne Agent.Formation (wzor 372b).
    /// Regula (glowny watek, OnMissionTick co 1 s; tylko misja szturmu murow, glowna druzyna atakujaca AI z TacticBreachWalls w stanie
    /// AssaultUnderRangedCover / TotalAttack, nie po zalamaniu 371; liczniki gry przy murze licza tylko glowna druzyne atakujaca - sojusznicy bez zmian):
    ///  - PRZYCZOLEK pasa = >= 15 atakujacych przy punkcie i < 15 obroncow (pamiec zapytan oblezenia gry, jak SiegeLane.IsUnderAttack &amp;&amp; !IsDefended
    ///    i sonda 450), nieprzerwanie >= 5 s; szyk pasa (GetLastAssignedFormation z AI.Side == strona pasa) pod AI;
    ///  - kogo: czlowiek zbudowany po starcie (OnAgentBuild - posilek), w wieku 1-10 s, w szyku INNEGO pasa; zywy, pod AI, nie bohater (381), nie konny,
    ///    nie chorazy, nie odlaczony do machiny ani nie uciekajacy;
    ///  - pas zrodlowy bez przyczolka, z < 5 atakujacych przy murze, nikogo z szyku w zamku (IsSplittableByAI) - pasa, na ktorym nasi juz sa, nie ruszamy;
    ///    nie szyk glowny 451 pod ochrona; szyk zrodlowy po oddaniu ma >= 15 ludzi bez odlaczonych (nigdy pusty pas, ludzie do taranu / wiezy zostaja);
    ///  - cel: pas z przyczolkiem; przy kilku - szyk z najmniejsza liczba ludzi; kazdy czlowiek oceniany do skutku albo do 10 s, potem zostaje tam, gdzie
    ///    dala go gra (zapadka - raz przeniesiony nie wraca).
    /// Przelozenie jak 372b: OnMassUnitTransferStart, Agent.Formation = cel, TriggerOnFormationsChanged + OnMassUnitTransferEnd + QuerySystem.Expire;
    /// nowi ida za rozkazem szyku celu (BehaviorAssaultWalls pasa). Klucz SiegeWavesReinforceSuccess (TAK). NIE = zadnego przelozenia - co do bitu jak
    /// przed; ta sama decyzja liczona i wypisana jako "bylby". Linie: "Oblezenie misja (370) fale na przyczolek (453)" przy przelozeniu (najwyzej 20 linii)
    /// i "... (453) koniec" przy koncu misji (ocenieni / przeniesieni wg pasa / pozostawieni wg powodu / przyczolki / ile probek szyk celu byl w
    /// rownowazeniu gry - UseSiegeMachines / WaitForLadders). Linia "fala posilkow (450)" pokazuje cel fali juz po przelozeniu (453 rusza w 1-2 s,
    /// sonda czyta szyk po 2 s). Wyjatek per czlowiek / druzyna = potkniecie (pierwsze 3 do logu), bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeWaves453 : MissionLogic
    {
        private const string P = "Oblezenie misja (370) fale na przyczolek (453): ";
        private const float Every = 1f, StartDelay = 3f, NoDeployStart = 10f, MinAge = 1f, MaxAge = 10f, HoldSec = 5f;
        private const int HoldMin = 15, WallMin = 5, SrcKeep = 15, ErrMax = 3, StAssault = 1, StTotal = 2, LaneN = 3, LineMax = 20;
        // powody pozostawienia (indeksy licznikow)
        private const int RNoHold = 0, RSrcBusy = 1, RMain = 2, RKeep = 3, RNoLane = 4, RSame = 5, RState = 6, RGone = 7, RN = 8;
        private static readonly string[] RName = { "bez przyczolka", "pas zrodla przy murze / nasi w zamku / szyk nie pod AI", "szyk glowny 451", "szyk zrodla przy minimum " + SrcKeep,
                                                   "nie w szyku pasa", "juz na pasie z przyczolkiem", "stan taktyki / 371 / brak taktyki", "padl / bohater / konny / chorazy / przy machinie" };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static FieldInfo _fTactic, _fState;
        private static bool _reflTried;

        private sealed class Fresh
        {
            internal Agent A;
            internal float At;
            internal int Why = RNoHold;
        }

        private bool _known, _siege, _ended, _started;
        private float _startAt = -1f, _next, _t0;
        private int _stumbles, _errs, _lines;
        private bool _keyOn;
        private readonly object _lock = new object();
        private readonly List<Fresh> _fresh = new List<Fresh>();   // posilki czekajace na ocene (OnAgentBuild -> pod lockiem)
        private int _seen, _moved, _busySamples;
        private readonly int[] _toLane = new int[LaneN], _left = new int[RN];
        private readonly float[] _holdFrom = { -1f, -1f, -1f }, _holdFirst = { -1f, -1f, -1f };
        private readonly HashSet<Formation> _targets = new HashSet<Formation>();
        private string _why;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal static bool On { get { var s = Settings.Current; return s != null && s.SiegeWavesReinforceSuccess; } }

        /// <summary>453-p: szyk zrodla bez nikogo w zamku. Przy TAK - IsSplittableByAI gry (przelicza zapytanie, gdy stare - to samo, co zrobi gra
        /// przed wlasnym przelozeniem). Przy NIE - ten sam warunek z pamieci (IsAIOwned + InsideCastleUnitCountIncludingUnpositionedReadOnly, mb 42537,
        /// 69077-69122), bez przeliczania i bez przesuwania chwili waznosci QueryData - pomiar 'bylby' nie zmienia stanu gry (co do bitu jak przed).</summary>
        private bool Splittable(Formation src)
        {
            if (_keyOn) return src.IsSplittableByAI;
            if (!src.IsAIOwned) return false;
            var q = src.QuerySystem;
            return q == null || q.InsideCastleUnitCountIncludingUnpositionedReadOnly == 0;
        }

        private static void Refl()
        {
            if (_reflTried) return;
            _reflTried = true;
            try { _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic"); } catch { _fTactic = null; }
            try { _fState = AccessTools.Field(typeof(TacticBreachWalls), "_tacticState"); } catch { _fState = null; }
        }

        private bool Siege(Mission m)
        {
            if (!_known)
            {
                try { _siege = m != null && m.IsSiegeBattle; } catch { _siege = false; }
                _known = true;
            }
            return _siege;
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (!_started || _ended || !_siege) return;
            try
            {
                var m = Mission;
                if (agent == null || !agent.IsHuman || m == null || !ReferenceEquals(agent.Team, m.AttackerTeam)) return;
                lock (_lock) _fresh.Add(new Fresh { A = agent, At = m.CurrentTime });
            }
            catch { _stumbles++; }
        }

        public override void OnMissionTick(float dt)
        {
            if (_ended) return;
            var m = Mission;
            if (m == null) return;
            try
            {
                float now = m.CurrentTime;
                if (!_started)
                {
                    if (!Siege(m)) { _ended = true; return; }
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }
                    if (now < _startAt) return;
                    _started = true;
                    _t0 = now;
                    _next = now;
                    Refl();
                    _keyOn = On;
                }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                Scan(m, now);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static int State(TacticBreachWalls tac)
        {
            try { return _fState != null ? Convert.ToInt32(_fState.GetValue(tac), Inv) : -1; } catch { return -1; }
        }

        private void Scan(Mission m, float now)
        {
            // posilki w wieku >= MinAge; starsze niz MaxAge - zostaja tam, gdzie dala je gra (ostatni powod do licznika)
            List<Fresh> ready = null;
            lock (_lock)
            {
                for (int i = 0; i < _fresh.Count; )
                {
                    var x = _fresh[i];
                    if (now - x.At > MaxAge) { _left[x.Why]++; _seen++; _fresh.RemoveAt(i); continue; }
                    if (now - x.At >= MinAge) { if (ready == null) ready = new List<Fresh>(); ready.Add(x); }
                    i++;
                }
            }
            var t = m.AttackerTeam;
            if (t == null || !MissionTeams.Valid(t)) { Mark(ready, RState); return; }
            var tac = _fTactic != null && t.TeamAI != null ? _fTactic.GetValue(t.TeamAI) as TacticBreachWalls : null;
            if (tac == null) { _why = "brak TacticBreachWalls"; Mark(ready, RState); return; }

            // pasy druzyny: szyk, przyczolek, przy murze
            List<SiegeLane> lanes = null;
            SiegeQuerySystem q = null;
            try { lanes = TeamAISiegeComponent.SiegeLanes; q = TeamAISiegeComponent.QuerySystem; } catch { lanes = null; }
            var f = new Formation[LaneN];
            var close = new int[LaneN];
            var hold = new bool[LaneN];
            for (int k = 0; k < LaneN; k++) close[k] = -1;
            for (int i = 0; lanes != null && i < lanes.Count; i++)
            {
                try
                {
                    var l = lanes[i];
                    if (l == null) continue;
                    int k = (int)l.LaneSide;
                    if (k < 0 || k >= LaneN) continue;
                    string lp = SiegeMissionLog370.LanePrefix(l.LaneSide);
                    int ca = SiegeMissionLog370.QVal(q, lp + "CloseAttackerCount"), dc = SiegeMissionLog370.QVal(q, lp + "DefenderCount");
                    close[k] = ca;
                    if (ca >= HoldMin && dc >= 0 && dc < HoldMin)
                    {
                        if (_holdFrom[k] < 0f) _holdFrom[k] = now;
                        if (_holdFirst[k] < 0f) _holdFirst[k] = now;
                    }
                    else _holdFrom[k] = -1f;
                    hold[k] = _holdFrom[k] >= 0f && now - _holdFrom[k] >= HoldSec;
                    Formation x = null;
                    try { x = l.GetLastAssignedFormation(t.TeamIndex); } catch { x = null; }
                    if (x == null || x.CountOfUnits <= 0 || !ReferenceEquals(x.Team, t) || x.AI == null || x.AI.Side != l.LaneSide) continue;
                    f[k] = x;
                }
                catch (Exception e) { Stumble("Pas", e); }
            }
            // pomiar: szyk celu w rownowazeniu gry (BalanceAssaultLanes bierze tylko UseSiegeMachines / WaitForLadders)
            foreach (var tf in _targets)
            {
                try
                {
                    if (tf == null || tf.CountOfUnits <= 0 || tf.AI == null) continue;
                    var ab = tf.AI.ActiveBehavior;
                    if (ab is BehaviorUseSiegeMachines || ab is BehaviorWaitForLadders) _busySamples++;
                }
                catch (Exception e) { Stumble("Cel", e); }
            }
            if (ready == null) return;

            int s = State(tac);
            bool broken = false;
            try { var sb = m.GetMissionBehavior<SiegeBreak371>(); broken = sb != null && sb.IsBroken; } catch { broken = false; }
            if (broken || (s != StAssault && s != StTotal)) { _why = broken ? "szturm zalamany (371)" : "stan taktyki " + s; Mark(ready, RState); return; }

            // cele: pasy z przyczolkiem i szykiem pod AI
            var tgt = new List<int>();
            for (int k = 0; k < LaneN; k++) if (hold[k] && f[k] != null && f[k].IsAIControlled) tgt.Add(k);
            if (tgt.Count == 0) { _why = "zaden pas bez przyczolka >= " + HoldSec.ToString("0", Inv) + " s"; Mark(ready, RNoHold); return; }
            _why = null;

            // liczniki lokalne (przy NIE ludzie zostaja - liczymy tak, jakby odeszli)
            var cnt = new Dictionary<Formation, int>();
            var plan = new List<KeyValuePair<Fresh, int>>();
            var fromN = new Dictionary<Formation, int>();
            for (int i = 0; i < ready.Count; i++)
            {
                var x = ready[i];
                try
                {
                    var a = x.A;
                    if (a == null || !a.IsActive() || !a.IsAIControlled || a.IsHero || a.IsMainAgent || a.IsPlayerControlled || a.HasMount || a.Banner != null
                        || a.IsDetachedFromFormation || a.CurrentlyUsedGameObject != null || a.IsRunningAway)
                    { Done(x, RGone); continue; }
                    var src = a.Formation;
                    int sk = -1;
                    for (int k = 0; k < LaneN; k++) if (src != null && ReferenceEquals(f[k], src)) { sk = k; break; }
                    if (sk < 0) { x.Why = RNoLane; continue; }
                    if (hold[sk]) { Done(x, RSame); continue; }
                    if (close[sk] < 0 || close[sk] >= WallMin || !src.IsAIControlled || !Splittable(src)) { x.Why = RSrcBusy; continue; }
                    if (SiegeMainEffort451.IsGuardedMain(src)) { x.Why = RMain; continue; }
                    int sc;
                    if (!cnt.TryGetValue(src, out sc)) sc = src.CountOfUnitsWithoutDetachedOnes;
                    if (sc - 1 < SrcKeep) { x.Why = RKeep; continue; }
                    // cel: pas z przyczolkiem o najmniejszym szyku
                    int best = -1, bc = int.MaxValue;
                    for (int j = 0; j < tgt.Count; j++)
                    {
                        var tf = f[tgt[j]];
                        int c;
                        if (!cnt.TryGetValue(tf, out c)) c = tf.CountOfUnits;
                        if (c < bc) { bc = c; best = tgt[j]; }
                    }
                    if (best < 0) { x.Why = RNoHold; continue; }
                    cnt[src] = sc - 1;
                    cnt[f[best]] = bc + 1;
                    plan.Add(new KeyValuePair<Fresh, int>(x, best));
                    int fn; fromN.TryGetValue(src, out fn); fromN[src] = fn + 1;
                }
                catch (Exception e) { Stumble("Czlowiek", e); }
            }
            if (plan.Count == 0) return;

            int moved = 0;
            var got = new int[LaneN];
            if (_keyOn)
            {
                var touched = new List<Formation>();
                foreach (var kv in fromN) touched.Add(kv.Key);
                foreach (var kv in plan) if (!touched.Contains(f[kv.Value])) touched.Add(f[kv.Value]);
                var started = new List<Formation>();
                foreach (var tf in touched) { try { tf.OnMassUnitTransferStart(); started.Add(tf); } catch (Exception e) { Stumble("Start", e); } }
                try
                {
                    foreach (var kv in plan)
                    {
                        try
                        {
                            kv.Key.A.Formation = f[kv.Value];
                            got[kv.Value]++;
                            moved++;
                            _targets.Add(f[kv.Value]);
                            Done(kv.Key, -1);
                        }
                        catch (Exception e) { Stumble("Przelozenie", e); }
                    }
                }
                finally
                {
                    foreach (var tf in touched)
                    {
                        try { t.TriggerOnFormationsChanged(tf); if (started.Contains(tf)) tf.OnMassUnitTransferEnd(); tf.QuerySystem.Expire(); }
                        catch (Exception e) { Stumble("Odswiezenie", e); }
                    }
                }
                try { t.QuerySystem.ExpireAfterUnitAddRemove(); } catch (Exception e) { Stumble("Druzyna", e); }
            }
            else
            {
                foreach (var kv in plan) { got[kv.Value]++; moved++; _targets.Add(f[kv.Value]); Done(kv.Key, -1); }
            }
            _moved += moved;
            for (int k = 0; k < LaneN; k++) _toLane[k] += got[k];
            if (moved > 0 && _lines < LineMax)
            {
                _lines++;
                var sb = new StringBuilder(256);
                sb.Append("t=").Append(now.ToString("0", Inv)).Append("s | T").Append(t.TeamIndex).Append(" | klucz ").Append(_keyOn ? "TAK" : "NIE").Append(" | ")
                  .Append(_keyOn ? "przeniesiono " : "bylby przeniosl ").Append(moved).Append(" posilkow z:");
                int n = 0;
                foreach (var kv in fromN)
                {
                    int fk = -1;
                    for (int k = 0; k < LaneN; k++) if (ReferenceEquals(f[k], kv.Key)) fk = k;
                    sb.Append(n++ > 0 ? "," : "").Append(" pas ").Append(fk >= 0 ? ((FormationAI.BehaviorSide)fk).ToString() : "?").Append(" F").Append((int)kv.Key.FormationIndex)
                      .Append(' ').Append(kv.Value).Append(" (przy murze ").Append(fk >= 0 ? close[fk] : -1).Append(')');
                }
                sb.Append(" -> ");
                n = 0;
                for (int k = 0; k < LaneN; k++)
                {
                    if (got[k] == 0) continue;
                    sb.Append(n++ > 0 ? ", " : "").Append("pas ").Append((FormationAI.BehaviorSide)k).Append(" F").Append((int)f[k].FormationIndex).Append(" +").Append(got[k])
                      .Append(" (przy murze ").Append(close[k]).Append(", przyczolek od t=").Append(_holdFrom[k].ToString("0", Inv)).Append("s, szyk ").Append(f[k].CountOfUnits).Append(')');
                }
                if (_lines == LineMax) sb.Append(" | dalsze przelozenia tylko w linii konca");
                Log.Info(P + sb.Append('.'));
            }
        }

        /// <summary>Ocena zakonczona: powod do licznika (albo -1 = przeniesiony / bylby), czlowiek wypada z kolejki.</summary>
        private void Done(Fresh x, int why)
        {
            lock (_lock)
            {
                if (!_fresh.Remove(x)) return;
                _seen++;
                if (why >= 0) _left[why]++;
            }
        }

        private void Mark(List<Fresh> ready, int why)
        {
            if (ready == null) return;
            for (int i = 0; i < ready.Count; i++) ready[i].Why = why;
        }

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && _siege)
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    int wait;
                    lock (_lock) { wait = _fresh.Count; for (int i = 0; i < _fresh.Count; i++) _left[_fresh[i].Why]++; _seen += wait; _fresh.Clear(); }
                    var sb = new StringBuilder(512);
                    sb.Append("Oblezenie misja (370) fale na przyczolek (453) koniec: t=").Append(now.ToString("0", Inv)).Append("s | klucz ").Append(_keyOn ? "TAK" : "NIE")
                      .Append(" | posilkow ocenionych ").Append(_seen).Append(" (w tym w kolejce przy koncu ").Append(wait).Append(')')
                      .Append(" | ").Append(_keyOn ? "przeniesionych " : "bylby przeniosl ").Append(_moved).Append(':');
                    for (int k = 0; k < LaneN; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append((FormationAI.BehaviorSide)k).Append(' ').Append(_toLane[k]);
                    sb.Append(" | zostali, gdzie dala gra:");
                    for (int r = 0; r < RN; r++) sb.Append(r > 0 ? ";" : "").Append(' ').Append(RName[r]).Append(' ').Append(_left[r]);
                    sb.Append(" | przyczolek (pierwszy raz):");
                    for (int k = 0; k < LaneN; k++) sb.Append(k > 0 ? "," : "").Append(' ').Append((FormationAI.BehaviorSide)k).Append(' ').Append(_holdFirst[k] >= 0f ? "t=" + _holdFirst[k].ToString("0", Inv) + "s" : "nigdy");
                    sb.Append(" | szyk celu w rownowazeniu gry (UseSiegeMachines / WaitForLadders) probek ").Append(_busySamples)
                      .Append(" | ostatnio: ").Append(_why ?? "-")
                      .Append(" | przyczolek = >= ").Append(HoldMin).Append(" atak przy punkcie i < ").Append(HoldMin).Append(" obroncow przez ").Append(HoldSec.ToString("0", Inv))
                      .Append(" s; zrodlo: < ").Append(WallMin).Append(" przy murze, nikt w zamku, szyk zostaje >= ").Append(SrcKeep).Append("; posilek w wieku ").Append(MinAge.ToString("0", Inv))
                      .Append('-').Append(MaxAge.ToString("0", Inv)).Append(" s | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            lock (_lock) _fresh.Clear();
            _targets.Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeWaves453." + where, e); } catch { }
        }
    }
}
