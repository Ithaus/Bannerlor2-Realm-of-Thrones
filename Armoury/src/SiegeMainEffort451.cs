using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 451 (pakiet 7 krok 7.2 audytu AI bitwy; Jeff 11.11 decyzja 8: "60% sil na jeden odcinek" = TAK, po sondzie na pas - sonda 450 jest).
    /// WYSILEK GLOWNY SZTURMU AI: w fazie podejscia szyki pasow druzyny atakujacej AI (TacticBreachWalls) dostaja 60 / 20 / 20 ludzi (2 pasy: 60 / 40)
    /// zamiast po rowno; pas glowny losowany sposrod pasow z wylomem, wieza albo brama (niezniszczony taran / brama otwarta; sama drabina - nie); pamiec ostatniego pasa glownego (zapis tylko po przeniesieniu) w tym
    /// oblezeniu (osada + chwila poczatku oblezenia): kolejny szturm tego samego oblezenia losuje sposrod INNYCH kandydatow, jesli sa.
    /// Dowod (dekompilacja 1.4.8, rally_dec/mb_full.cs 37223-37244, 37757, 37766): TacticBreachWalls.TickOccasionally co takt wola
    /// BalanceAssaultLanes(szyki AI w UseSiegeMachines / WaitForLadders, IsAITickedAfterSplit) - przenosi ludzi do szyku, ktoremu do sredniej brakuje
    /// WIECEJ niz 20% sumy tej listy. 60/20/20 na liscie trzech szykow: brak 13.3% - gra zostawia. ALE lista bywa krotsza (szyk wylomu w AssaultWalls,
    /// szyk na murze) albo boczny pas traci ludzi pod strzalami - wtedy gra oddalaby ludzi z pasa glownego (glowny + 1 boczny: brak 20% > 16%).
    /// Dlatego w fazie podejscia prefiks BalanceAssaultLanes zdejmuje szyk glowny z listy tej druzyny (boczne rownowaza sie miedzy soba jak w grze).
    /// SplitFormationClassIntoGivenNumber (ManageFormationCounts) przy tej samej liczbie szykow nic nie robi (38117-38125) - przeniesienie nie zmienia
    /// liczby szykow ani pasow, wiec nie wywoluje ponownego przydzialu (AssignMeleeFormationsToLanes trzyma GetLastAssignedFormation).
    /// Regula (glowny watek, OnMissionTick, co 1 s; tylko misja szturmu murow):
    ///  - po rozstawieniu + 3 s, do 60 s: druzyna z taktyka TacticBreachWalls w stanie AssaultUnderRangedCover / TotalAttack; pasy w uzyciu = pasy
    ///    z GetLastAssignedFormation(druzyna), ktorego AI.Side == strona pasa (gra nie czysci starego wpisu - 450-p), rozne szyki, >= 2 pasy;
    ///    kazdy szyk pasa: AI, IsSplittableByAI (u atakujacych = nikogo w zamku), IsAITickedAfterSplit; zaden pas nie ma >= 5 atakujacych przy murze
    ///    (pamiec gry, bez przeliczania); szyk gracza-wodza - nie (IsAIControlled / IsSplittableByAI).
    ///  - JEDNORAZOWO: suma S = ludzie bez odlaczonych (CountOfUnitsWithoutDetachedOnes - ludzie przy taranie / wiezy zostaja); boczny cel
    ///    floor(0.4 S / (n - 1)), glowny reszta; kazdy boczny powyzej celu oddaje nadwyzke glownemu Formation.TransferUnits (gra wybiera ludzi
    ///    najblizszych celowi); boczny cel < 10 ludzi - nie (nigdy pusty pas). Glowny juz >= celu - nic.
    ///  - ochrona (zapadka - raz zakonczona nie wraca): do wejscia na mur pasa glownego (>= 5 przy punkcie), zmiany pasa szyku glownego, pustego
    ///    szyku, konca stanu szturmu albo 300 s.
    /// Klucz SiegeMainEffort (TAK). NIE = zadnego przeniesienia i zadnej ochrony (prefiks wraca od razu) - co do bitu jak przed; ta sama decyzja liczona
    /// i wypisana jako "bylby". Linie: "Oblezenie misja (370) wysilek glowny (451)" przy decyzji i "... (451) koniec" przy koncu misji. Udzial pasa
    /// glownego w pierwszych 180 s mierzy sonda 450 ("pasy koniec (450)": udzial pasa glownego 0-180 s). Wyjatek per druzyna / szyk = potkniecie
    /// (pierwsze 3 do logu), bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeMainEffort451 : MissionLogic
    {
        private const string P = "Oblezenie misja (370) wysilek glowny (451): ";
        private const float Every = 1f, StartDelay = 3f, Window = 60f, NoDeployStart = 10f, GuardMax = 300f, MainShare = 0.6f;
        private const int WallMin = 5, SideMin = 10, ErrMax = 3, StAssault = 1, StTotal = 2, MemMax = 64;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static FieldInfo _fTactic, _fState;
        private static MethodInfo _mBalance;
        private static bool _reflTried;
        private static int _sErrs;
        private static readonly Random _rndOff = new Random();   // 451-p: "bylby" przy kluczu NIE (glowny watek)

        // pamiec pasa glownego na oblezenie: klucz osada + SiegeStartTime (przezywa wczytanie zapisu); najwyzej MemMax wpisow
        private static readonly Dictionary<string, FormationAI.BehaviorSide> _mem = new Dictionary<string, FormationAI.BehaviorSide>(StringComparer.Ordinal);
        private static readonly List<string> _memOrder = new List<string>();

        /// <summary>Ochrona szyku glownego dla prefiksu BalanceAssaultLanes (czytana takze poza glownym watkiem - tylko odczyt referencji).</summary>
        private sealed class Guard
        {
            internal TacticBreachWalls Tac;
            internal Formation Main;
            internal volatile bool On;
            internal int Skips;
        }
        private static volatile Guard[] _guards = new Guard[0];

        private sealed class TeamState
        {
            internal Team T;
            internal bool Done;
            internal string Why;
            internal FormationAI.BehaviorSide Lane = FormationAI.BehaviorSide.BehaviorSideNotSet;
            internal int Moved, WouldMove, MainAfter = -1, MainAtEnd = -1;
            internal float GuardFrom = -1f, GuardTo = -1f;
            internal string EndWhy;
            internal Guard G;
        }

        private bool _known, _siege, _ended, _started;
        private float _startAt = -1f, _next, _t0;
        private int _stumbles, _errs;
        private bool _keyOn;
        private string _memKey;
        private bool _laneChosen;
        private FormationAI.BehaviorSide _mainLane = FormationAI.BehaviorSide.BehaviorSideNotSet;
        private string _choice;
        private readonly List<TeamState> _teams = new List<TeamState>();

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal static bool On { get { var s = Settings.Current; return s != null && s.SiegeMainEffort; } }

        // ------------------------------------------------------------ latka (przy starcie gry, przed JIT wolajacych - jak 372)

        internal static void ApplyAll(Harmony h)
        {
            Refl();
            bool ok = false;
            try
            {
                if (_mBalance != null)
                {
                    h.Patch(_mBalance, prefix: new HarmonyMethod(typeof(SiegeMainEffort451), nameof(BalancePrefix)));
                    ok = true;
                }
            }
            catch (Exception e) { Log.Error("SiegeMainEffort451.ApplyAll", e); }
            Log.Info("Szturm (451): latka TacticBreachWalls.BalanceAssaultLanes " + (ok ? "wpieta" : "NIE wpieta (" + (_mBalance == null ? "brak metody" : "blad") + ")")
                     + ", pola taktyka/stan " + (_fTactic != null ? "+" : "-") + (_fState != null ? "+" : "-") + " | klucz Siege Main Effort " + (On ? "TAK" : "NIE") + ".");
        }

        private static void Refl()
        {
            if (_reflTried) return;
            _reflTried = true;
            try { _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic"); } catch { _fTactic = null; }
            try { _fState = AccessTools.Field(typeof(TacticBreachWalls), "_tacticState"); } catch { _fState = null; }
            // zadeklarowana w samym TacticBreachWalls (DeclaredMethod - pulapka 15.10 nie dotyczy); Patch tym samym MethodInfo
            try { _mBalance = AccessTools.DeclaredMethod(typeof(TacticBreachWalls), "BalanceAssaultLanes", new[] { typeof(List<Formation>) }); } catch { _mBalance = null; }
        }

        /// <summary>Faza podejscia: szyk glowny tej druzyny poza rownowazeniem pasow gry (boczne rownowaza sie miedzy soba).</summary>
        private static void BalancePrefix(TacticBreachWalls __instance, List<Formation> __0)
        {
            try
            {
                var gs = _guards;
                if (gs.Length == 0 || __0 == null) return;
                for (int i = 0; i < gs.Length; i++)
                {
                    var g = gs[i];
                    if (g == null || !g.On || !ReferenceEquals(g.Tac, __instance)) continue;
                    if (__0.Remove(g.Main)) Interlocked.Increment(ref g.Skips);
                }
            }
            catch (Exception e)
            {
                if (_sErrs < ErrMax) { _sErrs++; try { Log.Error("SiegeMainEffort451.BalancePrefix", e); } catch { } }
            }
        }

        /// <summary>452: czy szyk jest szykiem glownym 451 pod ochrona (pasa-lejka 452 wtedy nie porzuca) - tylko odczyt.</summary>
        internal static bool IsGuardedMain(Formation f)
        {
            var gs = _guards;
            for (int i = 0; f != null && i < gs.Length; i++) { var g = gs[i]; if (g != null && g.On && ReferenceEquals(g.Main, f)) return true; }
            return false;
        }

        // ------------------------------------------------------------ misja

        private bool Siege(Mission m)
        {
            if (!_known)
            {
                try { _siege = m != null && m.IsSiegeBattle; } catch { _siege = false; }
                _known = true;
            }
            return _siege;
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
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }   // pierwsze takty taktyki: przydzial szykow do pasow
                    if (now < _startAt) return;
                    _started = true;
                    _t0 = now;
                    _next = now;
                    Refl();
                    _keyOn = On;
                    _guards = new Guard[0];   // ochrona tylko tej misji
                }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                Scan(m, now);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private void Scan(Mission m, float now)
        {
            List<SiegeLane> lanes = null;
            SiegeQuerySystem q = null;
            try { lanes = TeamAISiegeComponent.SiegeLanes; q = TeamAISiegeComponent.QuerySystem; } catch { lanes = null; }
            var teams = m.Teams;
            for (int ti = 0; teams != null && ti < teams.Count; ti++)
            {
                Team t = null;
                try
                {
                    t = teams[ti];
                    if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Attacker) continue;
                    var tac = _fTactic != null && t.TeamAI != null ? _fTactic.GetValue(t.TeamAI) as TacticBreachWalls : null;
                    var st = Find(t);
                    if (tac == null) { if (st != null && st.G != null && st.G.On) End(st, now, "taktyka druzyny zmieniona"); continue; }
                    if (st == null) { st = new TeamState { T = t }; _teams.Add(st); }
                    if (!st.Done) Decide(m, now, st, tac, lanes, q);
                    else if (st.G != null && st.G.On) Watch(m, now, st, tac, lanes, q);
                }
                catch (Exception e) { Stumble("Druzyna", e); }
            }
        }

        private TeamState Find(Team t)
        {
            for (int i = 0; i < _teams.Count; i++) if (ReferenceEquals(_teams[i].T, t)) return _teams[i];
            return null;
        }

        private static int State(TacticBreachWalls tac)
        {
            try { return _fState != null ? Convert.ToInt32(_fState.GetValue(tac), Inv) : -1; } catch { return -1; }
        }

        /// <summary>Pasy w uzyciu druzyny: pas -> szyk (szyk przypisany i AI.Side == strona pasa, rozne szyki).</summary>
        private static List<KeyValuePair<SiegeLane, Formation>> Used(Team t, List<SiegeLane> lanes)
        {
            var r = new List<KeyValuePair<SiegeLane, Formation>>();
            for (int i = 0; lanes != null && i < lanes.Count; i++)
            {
                var l = lanes[i];
                if (l == null) continue;
                Formation f = null;
                try { f = l.GetLastAssignedFormation(t.TeamIndex); } catch { f = null; }
                if (f == null || f.CountOfUnits <= 0 || !ReferenceEquals(f.Team, t) || f.AI == null || f.AI.Side != l.LaneSide) continue;
                bool dup = false;
                for (int k = 0; k < r.Count; k++) if (ReferenceEquals(r[k].Value, f)) { dup = true; break; }
                if (!dup) r.Add(new KeyValuePair<SiegeLane, Formation>(l, f));
            }
            return r;
        }

        /// <summary>Rodzaj pasa jako kandydata na glowny: wylom / wieza / brama; null = sama drabina (nie kandydat).</summary>
        private static string Kind(SiegeLane l)
        {
            if (l.IsBreach) return "wylom";
            var pw = l.PrimarySiegeWeapons;
            for (int k = 0; pw != null && k < pw.Count; k++)
            {
                var tw = pw[k] as SiegeTower;
                if (tw != null && !tw.IsDestroyed && !tw.IsDisabled) return "wieza";
            }
            // 451-p: brama tylko z niezniszczonym taranem albo otwarta brama zewnetrzna / pasem otwartym (IsOpen) - CalculateIsLaneUnusable
            // zwraca false takze dla samych drabin (SiegeLadder to IPrimarySiegeWeapon), wiec pas bramy z drabinami bez tarana nie jest kandydatem
            if (l.HasGate)
            {
                if (l.IsOpen) return "brama";
                var dp = l.DefensePoints;
                for (int k = 0; dp != null && k < dp.Count; k++)
                {
                    var g = dp[k] as CastleGate;
                    if (g != null && g.IsGateOpen && g.GameEntity.HasTag("outer_gate")) return "brama";
                }
                for (int k = 0; pw != null && k < pw.Count; k++)
                {
                    var r = pw[k] as BatteringRam;
                    if (r != null && !r.IsDestroyed) return "brama";
                }
            }
            return null;
        }

        private void Decide(Mission m, float now, TeamState st, TacticBreachWalls tac, List<SiegeLane> lanes, SiegeQuerySystem q)
        {
            if (now - _t0 > Window) { st.Done = true; st.Why = "nie bylo warunkow w " + Window.ToString("0", Inv) + " s po rozstawieniu (ostatnio: " + (st.Why ?? "-") + ")"; Line(st, null); return; }
            int s = State(tac);
            if (s != StAssault && s != StTotal) { st.Why = "stan taktyki " + s; return; }
            var used = Used(st.T, lanes);
            if (used.Count < 2) { st.Why = "pasow w uzyciu " + used.Count; return; }
            for (int i = 0; i < used.Count; i++)
            {
                var f = used[i].Value;
                if (!f.IsAIControlled || !f.IsSplittableByAI) { st.Done = true; st.Why = "szyk F" + (int)f.FormationIndex + " nie pod AI albo ktos w zamku (IsSplittableByAI)"; Line(st, null); return; }
                if (!f.IsAITickedAfterSplit) { st.Why = "szyk F" + (int)f.FormationIndex + " przed pierwszym taktem AI"; return; }
                int ca = SiegeMissionLog370.QVal(q, SiegeMissionLog370.LanePrefix(used[i].Key.LaneSide) + "CloseAttackerCount");
                if (ca >= WallMin) { st.Done = true; st.Why = "pas " + used[i].Key.LaneSide + " juz na murze (" + ca + " przy punkcie)"; Line(st, null); return; }
            }
            // pas glowny: raz na misje (wspolny dla druzyn), z pamiecia ostatniego w tym oblezeniu
            if (!_laneChosen) Choose(used);
            int mi = -1;
            for (int i = 0; i < used.Count; i++) if (used[i].Key.LaneSide == _mainLane) { mi = i; break; }
            if (mi < 0) { st.Done = true; st.Why = _mainLane == FormationAI.BehaviorSide.BehaviorSideNotSet ? "brak kandydata (wylom / wieza / brama) wsrod pasow w uzyciu" : "pas glowny " + _mainLane + " nie w uzyciu druzyny"; Line(st, null); return; }
            st.Done = true;
            st.Lane = _mainLane;
            var main = used[mi].Value;
            int n = used.Count, sum = 0;
            var before = new int[n];
            for (int i = 0; i < n; i++) { before[i] = Math.Max(0, used[i].Value.CountOfUnitsWithoutDetachedOnes); sum += before[i]; }
            int sideT = (int)Math.Floor(sum * (1f - MainShare) / (n - 1));
            int mainT = sum - sideT * (n - 1);
            var sb = new StringBuilder(512);
            sb.Append("t=").Append(now.ToString("0", Inv)).Append("s | T").Append(st.T.TeamIndex).Append(" | klucz ").Append(_keyOn ? "TAK" : "NIE").Append(" | ").Append(_choice)
              .Append(" | przed:");
            for (int i = 0; i < n; i++) sb.Append(i > 0 ? "," : "").Append(' ').Append(used[i].Key.LaneSide).Append(" F").Append((int)used[i].Value.FormationIndex).Append(' ').Append(before[i]);
            sb.Append(" (lacznie ").Append(sum).Append(", glowny ").Append(Pct(before[mi], sum)).Append(") | cel glowny ").Append(mainT).Append(", boczny ").Append(sideT);
            if (sideT < SideMin) { st.Why = "boczny cel " + sideT + " < " + SideMin + " ludzi"; sb.Append(" | nic: ").Append(st.Why); Log.Info(P + sb.Append('.')); return; }
            if (before[mi] >= mainT) { st.Why = "glowny juz >= celu"; sb.Append(" | nic: ").Append(st.Why); Log.Info(P + sb.Append('.')); return; }
            int moved = 0, would = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == mi) continue;
                int k = before[i] - sideT;
                if (k <= 0) continue;
                would += k;
                if (!_keyOn) continue;
                try
                {
                    var f = used[i].Value;
                    int c0 = f.CountOfUnits;
                    f.TransferUnits(main, k);
                    moved += Math.Max(0, c0 - f.CountOfUnits);
                }
                catch (Exception e) { Stumble("Przeniesienie", e); }
            }
            st.WouldMove = would;
            st.Moved = moved;
            sb.Append(_keyOn ? " | przeniesiono " + moved + " (plan " + would + ")" : " | bylby: przeniesione " + would);
            if (_keyOn)
            {
                st.MainAfter = main.CountOfUnitsWithoutDetachedOnes;
                sb.Append(" | po:");
                for (int i = 0; i < n; i++) { int c = used[i].Value.CountOfUnitsWithoutDetachedOnes; sb.Append(i > 0 ? "," : "").Append(' ').Append(used[i].Key.LaneSide).Append(' ').Append(c).Append(" (").Append(Pct(c, sum)).Append(')'); }
                if (moved > 0)
                {
                    try { Remember(); } catch (Exception e) { Stumble("Pamiec.Zapis", e); }
                    st.G = new Guard { Tac = tac, Main = main, On = true };
                    st.GuardFrom = now;
                    var gs = _guards;
                    var ng = new Guard[gs.Length + 1];
                    Array.Copy(gs, ng, gs.Length);
                    ng[gs.Length] = st.G;
                    _guards = ng;
                    sb.Append(" | ochrona szyku glownego przed rownowazeniem gry do wejscia na mur (najdluzej ").Append(GuardMax.ToString("0", Inv)).Append(" s)");
                }
            }
            Log.Info(P + sb.Append('.'));
        }

        /// <summary>Losowanie pasa glownego sposrod kandydatow w uzyciu; pamiec: poprzedni pas tego oblezenia odpada, jesli jest inny kandydat.</summary>
        private void Choose(List<KeyValuePair<SiegeLane, Formation>> used)
        {
            _laneChosen = true;
            var cand = new List<int>();
            var sb = new StringBuilder(128);
            sb.Append("kandydaci:");
            for (int i = 0; i < used.Count; i++)
            {
                string k = null;
                try { k = Kind(used[i].Key); } catch (Exception e) { Stumble("Kandydat", e); }
                if (k == null) continue;
                cand.Add(i);
                sb.Append(cand.Count > 1 ? "," : "").Append(' ').Append(used[i].Key.LaneSide).Append(' ').Append(k);
            }
            if (cand.Count == 0) { _choice = sb.Append(" brak").ToString(); return; }
            FormationAI.BehaviorSide prev = FormationAI.BehaviorSide.BehaviorSideNotSet;
            bool hasPrev = false;
            try
            {
                _memKey = MemKey();
                if (_memKey != null) hasPrev = _mem.TryGetValue(_memKey, out prev);
            }
            catch (Exception e) { Stumble("Pamiec", e); }
            var pool = new List<int>(cand);
            if (hasPrev && pool.Count > 1)
            {
                for (int i = pool.Count - 1; i >= 0; i--) if (used[pool[i]].Key.LaneSide == prev && pool.Count > 1) pool.RemoveAt(i);
            }
            // 451-p: przy NIE losowanie z osobnego generatora - wspolny MBRandom gry nietkniety (NIE = co do bitu jak przed)
            int pick = pool[pool.Count == 1 ? 0 : (_keyOn ? MBRandom.RandomInt(pool.Count) : _rndOff.Next(pool.Count))];
            _mainLane = used[pick].Key.LaneSide;
            sb.Insert(0, "pas glowny " + _mainLane + " (" + Kind(used[pick].Key) + "; ");
            sb.Append("; poprzedni w tym oblezeniu ").Append(hasPrev ? prev.ToString() : "-").Append(')');
            _choice = sb.ToString();
        }

        /// <summary>451-p: pamiec pasa glownego zapisana dopiero po rzeczywistym przeniesieniu ludzi (klucz TAK, moved > 0).</summary>
        private void Remember()
        {
            if (_memKey == null || _mainLane == FormationAI.BehaviorSide.BehaviorSideNotSet) return;
            if (!_mem.ContainsKey(_memKey)) { _memOrder.Add(_memKey); if (_memOrder.Count > MemMax) { _mem.Remove(_memOrder[0]); _memOrder.RemoveAt(0); } }
            _mem[_memKey] = _mainLane;
        }

        private static string MemKey()
        {
            var me = MapEvent.PlayerMapEvent;
            var stl = me != null ? me.MapEventSettlement : null;
            var se = stl != null ? stl.SiegeEvent : null;
            if (stl == null || se == null) return null;
            return stl.StringId + "@" + se.SiegeStartTime.ToHours.ToString("R", Inv);
        }

        /// <summary>Faza podejscia: koniec ochrony (zapadka) - na murze, zmiana pasa szyku, pusty szyk, koniec stanu szturmu, limit.</summary>
        private void Watch(Mission m, float now, TeamState st, TacticBreachWalls tac, List<SiegeLane> lanes, SiegeQuerySystem q)
        {
            string why = null;
            var f = st.G.Main;
            int s = State(tac);
            if (f == null || f.CountOfUnits <= 0) why = "szyk glowny pusty";
            else if (s != StAssault && s != StTotal) why = "stan taktyki " + s;
            else if (f.AI == null || f.AI.Side != st.Lane) why = "gra przestawila szyk glowny na inny pas";
            else if (now - st.GuardFrom >= GuardMax) why = "limit " + GuardMax.ToString("0", Inv) + " s";
            else
            {
                int ca = SiegeMissionLog370.QVal(q, SiegeMissionLog370.LanePrefix(st.Lane) + "CloseAttackerCount");
                if (ca >= WallMin) why = "pas glowny na murze (" + ca + " przy punkcie)";
            }
            if (why == null) return;
            End(st, now, why);
        }

        private void End(TeamState st, float now, string why)
        {
            if (st.G == null || !st.G.On) return;
            st.G.On = false;
            st.GuardTo = now;
            st.EndWhy = why;
            try { st.MainAtEnd = st.G.Main != null ? st.G.Main.CountOfUnitsWithoutDetachedOnes : -1; } catch { st.MainAtEnd = -1; }
        }

        private static string Pct(int a, int sum)
        {
            return sum > 0 ? (100.0 * a / sum).ToString("0", Inv) + "%" : "-";
        }

        private void Line(TeamState st, string extra)
        {
            Log.Info(P + "T" + st.T.TeamIndex + " | klucz " + (_keyOn ? "TAK" : "NIE") + " | nic: " + st.Why + (extra ?? "") + ".");
        }

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && _siege)
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    var sb = new StringBuilder(512);
                    sb.Append("Oblezenie misja (370) wysilek glowny (451) koniec: t=").Append(now.ToString("0", Inv)).Append("s | klucz ").Append(_keyOn ? "TAK" : "NIE")
                      .Append(" | ").Append(_choice ?? "pas glowny nie wybrany");
                    if (_teams.Count == 0) sb.Append(" | brak druzyny z TacticBreachWalls");
                    for (int i = 0; i < _teams.Count; i++)
                    {
                        var st = _teams[i];
                        try
                        {
                            if (st.G != null && st.G.On) End(st, now, "koniec misji");
                            sb.Append(" | T").Append(st.T != null ? st.T.TeamIndex : -1).Append(": ");
                            if (st.Lane == FormationAI.BehaviorSide.BehaviorSideNotSet) { sb.Append("nic (").Append(st.Why ?? "nie rozstrzygnieto").Append(')'); continue; }
                            sb.Append("pas ").Append(st.Lane).Append(", przeniesiono ").Append(st.Moved).Append(", bylby ").Append(st.WouldMove);
                            if (st.G != null)
                                sb.Append(", ochrona t=").Append(st.GuardFrom.ToString("0", Inv)).Append("-").Append(st.GuardTo.ToString("0", Inv)).Append("s (").Append(st.EndWhy ?? "-")
                                  .Append("), szyk glowny po przeniesieniu ").Append(st.MainAfter).Append(" -> przy koncu ochrony ").Append(st.MainAtEnd)
                                  .Append(", rownowazenie gry bez szyku glownego ").Append(st.G.Skips).Append(" razy");
                            else if (st.Why != null) sb.Append(" (").Append(st.Why).Append(')');
                        }
                        catch (Exception e) { Stumble("Koniec.Druzyna", e); sb.Append(" ?"); }
                    }
                    sb.Append(" | udzial pasa glownego 0-180 s: linia 'pasy koniec (450)' (przy kluczu Battle AI Measurements) | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            // ochrona tej misji zdjeta (prefiks wraca od razu)
            try { var gs = _guards; for (int i = 0; i < gs.Length; i++) if (gs[i] != null) gs[i].On = false; } catch { }
            _guards = new Guard[0];
            _teams.Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeMainEffort451." + where, e); } catch { }
        }
    }
}
