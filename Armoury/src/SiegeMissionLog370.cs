using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 370 (Jeff 09.11: "tak zrob logi oblezen aby bylo wiadomo co sie dzieje"; oblezenie Goldentooth - obie strony staly 17 minut, taran / wieza / drabiny
    /// nie ruszyly, a log nie mowil, KTO stoi i dlaczego). TYLKO POMIAR: zadnej zmiany stanu misji, zadnej latki, zadnego rozkazu.
    /// Misja szturmu (Mission.IsSiegeBattle; nie wypad, nie bitwa polowa), klucz SiegeMissionLog. Odczyt: po rozstawieniu (start), co 30 s czasu misji i przy
    /// koncu misji. Na kazdy odczyt linie "Oblezenie misja (370)": po jednej na strone (A = atakujacy, O = obroncy) - taktyka kazdej druzyny
    /// (TeamAIComponent._currentTactic, stan TacticBreachWalls._tacticState / TacticDefendCastle.CurrentTacticState), szyki (ludzi, odlaczonych,
    /// zachowanie AI.ActiveBehavior, rozkaz MovementOrderEnum, czy pozycja rozkazu wazna - Formation.OrderPositionIsValid, odleglosc do pozycji rozkazu,
    /// do najblizszego szyku wroga - CachedClosestEnemyFormationDistanceSquared, ciosy szyku w ostatnich 30 s) - i jedna na machiny (taran, wieze, drabiny:
    /// ludzie na StandingPoint / stoisk, dojechal, odleglosc do celu, szyki uzytkownikow, wylaczone dla AI; machiny miotajace, bramy, wylomy, strzaly obu
    /// stron w 30 s, potkniecia). Przy pierwszym odczycie dodatkowo lista szykow z niewazna pozycja rozkazu (silnik pisze to w rgl jako
    /// "Formation order position is not valid. Team: T, Formation: F" - tu ta sama numeracja T/F).
    /// Odczyt pozycji: tylko wlasciwosci (OrderPosition, OrderPositionIsValid) - nie MovementOrder.GetPosition (ta liczy i zapisuje pamiec podreczna rozkazu).
    /// Koszt: co 30 s jeden przeglad MissionObjects i szykow; co klatke tylko porownanie czasu; per cios / pocisk - licznik. Wyjatki per obiekt / szyk
    /// liczone (potkniecia), pierwszy slad do logu, bez globalnego wylacznika.
    /// 437 (pakiet 0 audytu AI bitwy, krok 7.0 - sonda na pas): przy kluczu BattleAiMeasurements kazdy odczyt dopisuje linie "Oblezenie misja (370) ... pasy" -
    /// na kazdy pas (TeamAISiegeComponent.SiegeLanes): stan LaneState i stan obrony, otwarty / wylom / brama, pojemnosc CalculateLaneCapacity, machiny pasa,
    /// liczniki zapytan oblezenia (atakujacy blisko, w regionie pasa, obroncy przy punkcie - pamiec gry 5 s), cel fali kazdej druzyny (GetLastAssignedFormation:
    /// szyk, ludzi, zachowanie, IsSplittableByAI) i straty szyku w oknie 30 s wedlug zrodla (pociski / reszta); do tego atakujacy w srodku (InsideAttackerCount).
    /// 437-p: liczniki zapytan czytane z pamieci QueryData (GetCachedValue przez refleksje) - wlasciwosci SiegeQuerySystem wolaja .Value, ktore po 5 s
    /// przelicza (LocateAttackers) i zapisuje cache czytany przez AI szturmu; sonda nie przesuwa juz chwili tego przeliczenia.
    /// Straty z OnAgentRemoved pod lockiem (zdarzenie bywa z ticku asynchronicznego). Klucz NIE - linii pasow brak, reszta 370 jak dotad.
    /// 450 (pakiet 7.0 audytu AI bitwy - sonda na pas, rozszerzenie; TYLKO ODCZYT, ten sam klucz BattleAiMeasurements): probka pasow co 1 s w OnMissionTick
    /// (glowny watek) z pamieci QueryData (bez przeliczania): przyczolek jak w grze (SiegeLane.IsUnderAttack && !IsDefended: >= 15 atakujacych przy punkcie
    /// obrony i < 15 obroncow - stan Conceited), pierwsze wejscie na mur (>= 5 atakujacych przy punkcie), czas przyczolka, maksima licznikow; udzial pasa
    /// glownego w pierwszych 180 s po rozstawieniu (przydzial: ludzie szykow z GetLastAssignedFormation druzyn atakujacych, szyk liczony raz - przy dwoch pasach ten o stronie f.AI.Side (450-p); obecnosc:
    /// RegionMemberCount); fale posilkow (OnAgentBuild po starcie, przydzial do pasa po 2 s wedlug szyku czlowieka, fala zamknieta po 5 s bez nowych) -
    /// linia "fala posilkow (450)"; straty wedlug zrodla pociski / wrecz / inne (upadek, bez sprawcy) na szyk i na pas (mapa szyk -> pas z ostatniej probki,
    /// pod lockiem). Linia "pasy koniec (450)" przy koncu misji. Liczniki gry licza tylko glowna druzyne atakujaca i pamietaja 5 s.
    /// 456 (pakiet 7 audytu AI bitwy, decyzja Jeffa 12 "zaloga twierdzy sie zalamuje -> odwrot do sali lorda"; TYLKO POMIAR, klucz SiegeMissionLog):
    /// gra MA ten etap gotowy - SandBoxMissions.OpenSiegeMissionWithDeployment / NoDeployment (gracz atakuje, nie wypad, nie odsiecz) wola
    /// BattleEndLogic.EnableEnemyDefenderPullBack(DefaultSiegeLordsHallFightModel.DefenderTroopNumberForSuccessfulPullBack = 20); OnAgentRemoved
    /// (mb_full 79451-79458) liczy ludzi obroncow (strona wroga gracza) zdjetych jako Routed; przy wyczerpaniu strony obroncow i >= 20 zbieglych
    /// MissionEnded daje CreateDefenderPushedBack (BattleState.DefenderPullBack) -> CampaignSiegeStateHandler przesuwa oblezenie do InTheLordsHall,
    /// a menu oblezenia otwiera walke w sali lorda (scena "lordshall" osady). Sceny ROT (ROT-Map/ModuleData/settlements.xml, 227 osad z sala lorda):
    /// keepy vanilli i 12 wlasnych scen ROT (red_keep, WinterFell_Keep, ROT_*_keep, ROT_sept_interior) - wszystkie maja FightAreaMarker (fight_area_1-3)
    /// i punkty defender_archer / defender_infantry, ktorych wymaga LordsHallFightMissionController. Zalamanie zalogi = panika z morale (RBM nie gasi
    /// odwrotu przy morale 0, rbmai 2825); TacticDefendCastle.MustRetreatToCastle zawsze false (mb_full 38754) - taktyka obrony sama do keepu nie schodzi.
    /// Brak w logach jakiejkolwiek misji szturmu gracza, wiec bez dowodu, ze zaloga AI w ogole peka - najpierw liczby, regula (o ile potrzebna) potem.
    /// Linia "sala lorda (456)" przy koncu misji: strona gracza, czy etap gry wlaczony (pole BattleEndLogic przez refleksje, tylko odczyt), prog gry,
    /// zbiegli obroncy (w panice morale &lt; 0.01 / reszta), zabici, nieprzytomni, czas pierwszego zbiega i osiagniecia progu, zostalo do progu wg gry,
    /// zepchnieci wg gry, obroncy w polu i w odwodzie spawnera, wynik misji. Licznik w OnAgentRemoved pod lockiem (zdarzenie bywa z ticku asynchronicznego).
    /// </summary>
    internal sealed class SiegeMissionLog370 : MissionLogic
    {
        private const float Every = 30f, StartDelay = 1f, NoDeployStart = 10f;
        private const int ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] FcName = { "Pie", "Luk", "Kon", "KLuk", "Har", "CPie", "LKon", "CKon", "Wodz", "Straz" };

        private static FieldInfo _fTactic, _fBreachState, _fLanes;
        private static bool _reflTried;

        private bool _known, _siege, _started, _ended, _firstDone;
        private float _next, _startAt = -1f;
        private int _readings, _stumbles, _errs;
        private int _shotsA, _shotsD, _shotsAllA, _shotsAllD;
        private readonly Dictionary<Formation, int> _hits = new Dictionary<Formation, int>();
        // 372: sklad szyku przy odczycie - [0] z bronia dystansowa i amunicja (Agent.IsRangedCached), [1] konno (HasMount)
        private readonly Dictionary<Formation, int[]> _mix = new Dictionary<Formation, int[]>();
        private readonly StringBuilder _sb = new StringBuilder(1024);
        // 437 (7.0): straty szyku w oknie odczytu - [0] od pocisku, [1] reszta (zabici + nieprzytomni); zapis pod _lostLock
        private readonly Dictionary<Formation, int[]> _lost = new Dictionary<Formation, int[]>();
        private readonly object _lostLock = new object();
        private bool _lanes437;

        // 450 (7.0): probka pasow co 1 s (glowny watek), indeks pasa = pozycja w TeamAISiegeComponent.SiegeLanes (najwyzej 3); 3 = bez pasa
        private const float LaneEvery = 1f, OpenWindow = 180f, WaveGap = 5f, WaveSettle = 2f;
        private const int WallMin = 5, HoldMin = 15, LaneMax = 3;
        private float _laneNext, _t0, _lastSample = -1f, _lastBuild = -1f;
        private int _laneN, _shareSamples, _dupAsg;
        private readonly string[] _laneName = new string[LaneMax];
        private readonly float[] _wallAt = { -1f, -1f, -1f }, _holdAt = { -1f, -1f, -1f }, _holdSec = new float[LaneMax];
        private readonly int[] _closeMax = new int[LaneMax], _defMax = new int[LaneMax];
        private readonly double[] _shareAsg = new double[LaneMax], _sharePres = new double[LaneMax];
        // straty wedlug strony (0 A, 1 O), pasa (0..2, 3 bez pasa) i zrodla (0 pociski, 1 wrecz, 2 inne); pod _lostLock razem z mapa szyk -> pas
        private readonly int[,,] _laneLost = new int[2, LaneMax + 1, 3];
        private readonly Dictionary<Formation, int> _fLane = new Dictionary<Formation, int>();
        private readonly Dictionary<Formation, int> _fLaneTmp = new Dictionary<Formation, int>();
        // posilki: zbudowani po starcie (OnAgentBuild), przydzial po WaveSettle; fala na strone: [pas 0..2, 3 bez pasa, 4 padl / bez szyku]
        private readonly List<Agent> _built = new List<Agent>();
        private readonly List<float> _builtAt = new List<float>();
        private readonly object _buildLock = new object();
        private readonly int[,] _wave = new int[2, LaneMax + 2], _reinf = new int[2, LaneMax + 2];
        private readonly int[] _waveN = new int[2], _waves = new int[2];
        private readonly float[] _waveT0 = new float[2];
        private readonly Dictionary<Formation, int>[] _waveF = { new Dictionary<Formation, int>(), new Dictionary<Formation, int>() };

        // 456: zbiegli obroncy wobec etapu sali lorda gry - [0] zbiegli, [1] w tym w panice (morale < 0.01), [2] zabici, [3] nieprzytomni; pod _keepLock
        private static FieldInfo _fPbOn, _fPbNeed, _fPbDone;
        private static bool _pbReflTried;
        private readonly int[] _keep = new int[4];
        private readonly object _keepLock = new object();
        private float _keepFirst = -1f, _keepThrAt = -1f;
        private int _keepNeed0 = -1;
        private bool _keepOn;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private bool Siege(Mission m)
        {
            if (!_known)
            {
                try { _siege = m != null && m.IsSiegeBattle; } catch { _siege = false; }
                _known = true;
            }
            return _siege;
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            if (!_started || _ended || !_siege) return;
            try
            {
                var f = affectorAgent != null && affectedAgent != null && affectedAgent.IsHuman ? affectorAgent.Formation : null;
                if (f == null) return;
                int n;
                _hits.TryGetValue(f, out n);
                _hits[f] = n + 1;
            }
            catch { _stumbles++; }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (!_started || _ended || !_siege) return;
            try { KeepCount(affectedAgent, agentState); } catch { _stumbles++; }   // 456: tylko licznik, niezaleznie od klucza pomiarow AI
            if (!_lanes437) return;
            try
            {
                if (affectedAgent == null || !affectedAgent.IsHuman || (agentState != AgentState.Killed && agentState != AgentState.Unconscious)) return;
                var f = affectedAgent.Formation;
                // 450: zrodlo - pociski / wrecz / inne (upadek z muru, brak sprawcy, sam sobie)
                int src = blow.IsMissile ? 0
                        : (affectorAgent == null || affectorAgent == affectedAgent || blow.OverrideKillInfo == Agent.KillInfo.Gravity) ? 2 : 1;
                var t = affectedAgent.Team;
                int s = t == null ? -1 : (t.Side == BattleSideEnum.Attacker ? 0 : (t.Side == BattleSideEnum.Defender ? 1 : -1));
                lock (_lostLock)
                {
                    if (f != null)
                    {
                        int[] l;
                        if (!_lost.TryGetValue(f, out l)) { l = new int[3]; _lost[f] = l; }
                        l[src]++;
                    }
                    if (s >= 0 && _laneN > 0)
                    {
                        int li;
                        if (f == null || !_fLane.TryGetValue(f, out li)) li = LaneMax;
                        _laneLost[s, li, src]++;
                    }
                }
            }
            catch { _stumbles++; }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (!_started || _ended || !_siege || !_lanes437) return;
            try
            {
                if (agent == null || !agent.IsHuman) return;
                var m = Mission;
                float now = m != null ? m.CurrentTime : 0f;
                lock (_buildLock) { _built.Add(agent); _builtAt.Add(now); _lastBuild = now; }
            }
            catch { _stumbles++; }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            if (!_started || _ended || !_siege) return;
            try
            {
                var t = shooterAgent != null ? shooterAgent.Team : null;
                if (t == null) return;
                if (t.Side == BattleSideEnum.Attacker) { _shotsA++; _shotsAllA++; }
                else if (t.Side == BattleSideEnum.Defender) { _shotsD++; _shotsAllD++; }
            }
            catch { _stumbles++; }
        }

        public override void OnMissionTick(float dt)
        {
            if (_ended) return;
            var m = Mission;
            if (m == null) return;
            float now;
            try
            {
                now = m.CurrentTime;
                if (!_started)
                {
                    if (!Siege(m)) { _ended = true; return; }
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }   // klatka po rozstawieniu: szyki dostaja pierwsze rozkazy
                    if (now < _startAt) return;
                    _started = true;
                    try { var s437 = Settings.Current; _lanes437 = s437 != null && s437.BattleAiMeasurements; } catch { _lanes437 = false; }
                    _t0 = now;
                    try { KeepStart(m); } catch (Exception e) { Stumble("SalaStart", e); }
                    if (_lanes437) { try { LaneSample(m, now, false); } catch (Exception e) { Stumble("Probka", e); } _laneNext = now + LaneEvery; }
                    Reading(m, "start");
                    _next = now + Every;
                    return;
                }
                if (m.MissionEnded) return;   // linia konca w OnEndMission
                if (_lanes437 && now >= _laneNext)
                {
                    _laneNext = now + LaneEvery;
                    try { LaneSample(m, now, false); } catch (Exception e) { Stumble("Probka", e); }
                }
                if (now < _next) return;
                _next = now + Every;
                Reading(m, null);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && m != null) Reading(m, "koniec");
                if (_started && !_ended && m != null && _lanes437)
                {
                    try { LaneSample(m, m.CurrentTime, true); } catch (Exception e) { Stumble("Probka", e); }
                    try { LaneEnd(m); } catch (Exception e) { Stumble("PasyKoniec", e); }
                }
                if (_started && !_ended && m != null) { try { KeepEnd(m); } catch (Exception e) { Stumble("SalaKoniec", e); } }
                if (_started)
                    Log.Info("Oblezenie misja (370): koniec | odczytow " + _readings + " | strzaly calej misji: A " + _shotsAllA + ", O " + _shotsAllD
                             + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _hits.Clear();
            _mix.Clear();
            lock (_lostLock) { _lost.Clear(); _fLane.Clear(); }
            lock (_buildLock) { _built.Clear(); _builtAt.Clear(); }
            _waveF[0].Clear(); _waveF[1].Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeMissionLog370." + where, e); } catch { }
        }

        // ------------------------------------------------------------ odczyt

        private void Reading(Mission m, string tag)
        {
            _readings++;
            string head = "Oblezenie misja (370): t=" + m.CurrentTime.ToString("0", Inv) + "s" + (tag != null ? " (" + tag + ")" : "");
            List<string> bad = !_firstDone ? new List<string>() : null;
            Mix(m);
            Side(m, BattleSideEnum.Attacker, head, bad);
            Side(m, BattleSideEnum.Defender, head, bad);
            Machines(m, head);
            if (_lanes437) { try { Lanes(m, head); } catch (Exception e) { Stumble("Pasy", e); } }
            if (bad != null)
            {
                _firstDone = true;
                Log.Info("Oblezenie misja (370): niewazne pozycje rozkazow przy pierwszym odczycie: " + (bad.Count == 0 ? "brak" : string.Join("; ", bad)) + ".");
            }
            _hits.Clear();
            _shotsA = 0; _shotsD = 0;
            lock (_lostLock) _lost.Clear();
        }

        /// <summary>437 (7.0): linia pasow - stan, pojemnosc, machiny, liczniki zapytan oblezenia, cel fali kazdej druzyny i jego straty w oknie.</summary>
        private void Lanes(Mission m, string head)
        {
            var sb = _sb;
            sb.Length = 0;
            sb.Append(head).Append(" | pasy:");
            List<SiegeLane> lanes = null;
            SiegeQuerySystem q = null;
            try { lanes = TeamAISiegeComponent.SiegeLanes; q = TeamAISiegeComponent.QuerySystem; } catch (Exception e) { Stumble("Pasy.Lista", e); }
            if (lanes == null || lanes.Count == 0) { sb.Append(" brak (SiegeLanes puste)."); Log.Info(sb.ToString()); return; }
            Dictionary<Formation, int[]> lost;
            lock (_lostLock) lost = new Dictionary<Formation, int[]>(_lost);
            var teams = m.Teams;
            for (int i = 0; i < lanes.Count; i++)
            {
                try
                {
                    var l = lanes[i];
                    if (l == null) continue;
                    var side = l.LaneSide;
                    sb.Append(i > 0 ? " ||" : "").Append(' ').Append(side.ToString()).Append(": ").Append(l.LaneState.ToString()).Append(", obrona ").Append(l.GetDefenseState().ToString())
                      .Append(", otwarty ").Append(l.IsOpen ? "TAK" : "NIE").Append(", wylom ").Append(l.IsBreach ? "TAK" : "NIE").Append(", brama ").Append(l.HasGate ? "TAK" : "NIE");
                    try { sb.Append(", pojemnosc ").Append(l.CalculateLaneCapacity().ToString("0", Inv)); } catch { sb.Append(", pojemnosc ?"); }
                    sb.Append(", machiny ");
                    var pw = l.PrimarySiegeWeapons;
                    if (pw == null || pw.Count == 0) sb.Append('-');
                    else for (int k = 0; k < pw.Count; k++) sb.Append(k > 0 ? "," : "").Append(pw[k] != null ? pw[k].GetType().Name : "?");
                    if (q != null)
                    {
                        try
                        {
                            // 437-p: pamiec podreczna QueryData (GetCachedValue) - wlasciwosci (.Value) przeliczaja i odswiezaja cache, ktory czyta AI szturmu
                            string ls = side == FormationAI.BehaviorSide.Left ? "_left" : (side == FormationAI.BehaviorSide.Middle ? "_middle" : "_right");
                            int ca = QVal(q, ls + "CloseAttackerCount"), dc = QVal(q, ls + "DefenderCount");
                            sb.Append(", atak blisko ").Append(QCached(q, ls + "CloseAttackerCount")).Append(", w regionie ").Append(QCached(q, ls + "RegionMemberCount"))
                              .Append(", obroncy ").Append(QCached(q, ls + "DefenderCount"))
                              .Append(", przyczolek ").Append(ca < 0 || dc < 0 ? "?" : (ca >= HoldMin && dc < HoldMin ? "TAK" : "NIE"));   // 450: jak SiegeLane.IsUnderAttack && !IsDefended
                        }
                        catch { sb.Append(", liczniki ?"); }
                    }
                    sb.Append(", fale:");
                    int wn = 0;
                    for (int ti = 0; teams != null && ti < teams.Count; ti++)
                    {
                        Formation f = null;
                        try { f = l.GetLastAssignedFormation(ti); } catch { f = null; }
                        if (f == null) continue;
                        int[] ls;
                        lost.TryGetValue(f, out ls);
                        var b = f.AI != null ? f.AI.ActiveBehavior : null;
                        sb.Append(wn++ > 0 ? "," : "").Append(" T").Append(f.Team != null ? f.Team.TeamIndex : -1).Append('F').Append((int)f.FormationIndex).Append(' ').Append(f.CountOfUnits)
                          .Append(' ').Append(BehName(b)).Append(f.IsSplittableByAI ? " dzielony" : " niedzielony")
                          .Append(" straty30s ").Append(ls != null ? ls[0] : 0).Append('/').Append(ls != null ? ls[1] : 0).Append('/').Append(ls != null ? ls[2] : 0);
                    }
                    if (wn == 0) sb.Append(" -");
                }
                catch (Exception e) { Stumble("Pas", e); sb.Append(" ?"); }
            }
            try { if (q != null) sb.Append(" | atakujacy w srodku ").Append(QCached(q, "_insideAttackerCount")); } catch { sb.Append(" | atakujacy w srodku ?"); }
            sb.Append(" | straty30s = pociski/wrecz/inne (zabici + nieprzytomni; inne = upadek, bez sprawcy); przyczolek = >= 15 atak blisko i < 15 obroncow; liczniki z pamieci gry bez przeliczania.");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ 450 (7.0): probka pasow co 1 s, fale posilkow, linia konca

        /// <summary>
        /// 450: probka pasow (glowny watek, tylko odczyt pamieci gry): wejscie na mur, przyczolek, maksima, udzial pasa w 180 s, mapa szyk -> pas dla strat,
        /// przydzial zbudowanych posilkow do pasow. flush = koniec misji (przydziel wszystkich czekajacych i zamknij fale).
        /// </summary>
        private void LaneSample(Mission m, float now, bool flush)
        {
            List<SiegeLane> lanes = null;
            SiegeQuerySystem q = null;
            try { lanes = TeamAISiegeComponent.SiegeLanes; q = TeamAISiegeComponent.QuerySystem; } catch { lanes = null; }
            if (lanes != null && lanes.Count > 0 && _laneN == 0)
            {
                _laneN = Math.Min(LaneMax, lanes.Count);
                for (int i = 0; i < _laneN; i++) { try { _laneName[i] = lanes[i] != null ? lanes[i].LaneSide.ToString() : "?"; } catch { _laneName[i] = "?"; } }
            }
            var map = _fLaneTmp;
            map.Clear();
            if (_laneN > 0 && lanes != null)
            {
                float dt = _lastSample >= 0f ? Math.Max(0f, now - _lastSample) : 0f;
                _lastSample = now;
                bool open = now - _t0 <= OpenWindow && !flush;
                var teams = m.Teams;
                int[] asg = new int[LaneMax], pres = new int[LaneMax];
                for (int i = 0; i < _laneN && i < lanes.Count; i++)
                {
                    try
                    {
                        var l = lanes[i];
                        if (l == null) continue;
                        string lp = LanePrefix(l.LaneSide);
                        int ca = QVal(q, lp + "CloseAttackerCount"), dc = QVal(q, lp + "DefenderCount"), rg = QVal(q, lp + "RegionMemberCount");
                        if (ca > _closeMax[i]) _closeMax[i] = ca;
                        if (dc > _defMax[i]) _defMax[i] = dc;
                        if (ca >= WallMin && _wallAt[i] < 0f) _wallAt[i] = now;
                        if (ca >= HoldMin && dc >= 0 && dc < HoldMin)
                        {
                            if (_holdAt[i] < 0f) _holdAt[i] = now;
                            _holdSec[i] += dt;
                        }
                        if (rg > 0) pres[i] = rg;
                        for (int ti = 0; teams != null && ti < teams.Count; ti++)
                        {
                            Formation f = null;
                            try { f = l.GetLastAssignedFormation(ti); } catch { f = null; }
                            if (f == null) continue;
                            bool att = f.Team != null && f.Team.Side == BattleSideEnum.Attacker;
                            int units = att ? Math.Max(0, f.CountOfUnits) : 0;
                            if (map.TryGetValue(f, out int prev))
                            {
                                // 450-p: gra wpisuje szyk na nowy pas (SetLastAssignedFormation), a starego wpisu nie czysci; przy kazdym przydziale
                                // ustawia tez f.AI.Side = LaneSide - przy szyku na dwoch pasach wygrywa pas o stronie szyku, inaczej pierwszy na liscie
                                if (open && att) _dupAsg++;
                                bool here = false, there = false;
                                try { var fs = f.AI != null ? f.AI.Side : FormationAI.BehaviorSide.BehaviorSideNotSet; here = l.LaneSide == fs; there = lanes[prev] != null && lanes[prev].LaneSide == fs; }
                                catch { here = false; }
                                if (here && !there) { map[f] = i; asg[prev] -= units; asg[i] += units; }
                                continue;
                            }
                            map[f] = i;
                            asg[i] += units;
                        }
                    }
                    catch (Exception e) { Stumble("Probka.Pas", e); }
                }
                if (open)
                {
                    _shareSamples++;
                    for (int i = 0; i < _laneN; i++) { _shareAsg[i] += asg[i]; _sharePres[i] += pres[i]; }
                }
                lock (_lostLock)
                {
                    _fLane.Clear();
                    foreach (var kv in map) _fLane[kv.Key] = kv.Value;
                }
            }
            Waves(now, flush, map);
        }

        /// <summary>450: przydzial posilkow do pasow wedlug szyku czlowieka WaveSettle s po zbudowaniu; fala zamknieta po WaveGap s bez nowych.</summary>
        private void Waves(float now, bool flush, Dictionary<Formation, int> map)
        {
            List<Agent> ready = null;
            List<float> readyAt = null;
            bool pending, quiet;
            lock (_buildLock)
            {
                for (int i = 0; i < _built.Count; )
                {
                    if (flush || _builtAt[i] <= now - WaveSettle)
                    {
                        if (ready == null) { ready = new List<Agent>(); readyAt = new List<float>(); }
                        ready.Add(_built[i]); readyAt.Add(_builtAt[i]);
                        _built.RemoveAt(i); _builtAt.RemoveAt(i);
                    }
                    else i++;
                }
                pending = _built.Count > 0;
                quiet = _lastBuild < 0f || now - _lastBuild >= WaveGap;
            }
            for (int k = 0; ready != null && k < ready.Count; k++)
            {
                try
                {
                    var a = ready[k];
                    var t = a != null ? a.Team : null;
                    int s = t == null ? -1 : (t.Side == BattleSideEnum.Attacker ? 0 : (t.Side == BattleSideEnum.Defender ? 1 : -1));
                    if (s < 0) continue;
                    if (_waveN[s] == 0) _waveT0[s] = readyAt[k];
                    _waveN[s]++;
                    Formation f = a.IsActive() ? a.Formation : null;
                    int b;
                    if (f == null) b = LaneMax + 1;
                    else
                    {
                        int li;
                        b = map.TryGetValue(f, out li) ? li : LaneMax;
                        int n;
                        _waveF[s].TryGetValue(f, out n);
                        _waveF[s][f] = n + 1;
                    }
                    _wave[s, b]++;
                }
                catch (Exception e) { Stumble("Fala", e); }
            }
            if (!flush && (pending || !quiet)) return;
            for (int s = 0; s < 2; s++)
            {
                if (_waveN[s] == 0) continue;
                try { WaveLine(s, map); } catch (Exception e) { Stumble("Fala.Linia", e); }
                for (int b = 0; b < LaneMax + 2; b++) { _reinf[s, b] += _wave[s, b]; _wave[s, b] = 0; }
                _waves[s]++;
                _waveN[s] = 0;
                _waveF[s].Clear();
            }
        }

        private void WaveLine(int s, Dictionary<Formation, int> map)
        {
            var sb = new StringBuilder(256);
            sb.Append("Oblezenie misja (370) fala posilkow (450): t=").Append(_waveT0[s].ToString("0", Inv)).Append("s | strona ").Append(s == 0 ? "A" : "O")
              .Append(" | ludzi ").Append(_waveN[s]).Append(" | cel:");
            for (int i = 0; i < _laneN; i++) sb.Append(i > 0 ? "," : "").Append(' ').Append(_laneName[i]).Append(' ').Append(_wave[s, i]);
            if (_laneN == 0) sb.Append(" pasow brak");
            sb.Append(", bez pasa ").Append(_wave[s, LaneMax]).Append(", padl / bez szyku ").Append(_wave[s, LaneMax + 1]).Append(" | szyki:");
            int n = 0;
            foreach (var kv in _waveF[s])
            {
                var f = kv.Key;
                int li;
                sb.Append(n++ > 0 ? "," : "").Append(" T").Append(f.Team != null ? f.Team.TeamIndex : -1).Append('F').Append((int)f.FormationIndex).Append(' ').Append(kv.Value)
                  .Append(" (").Append(map.TryGetValue(f, out li) && li < _laneN ? _laneName[li] : "bez pasa").Append(')');
            }
            if (n == 0) sb.Append(" -");
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ 456: zaloga a etap sali lorda gry (tylko odczyt)

        private static void KeepRefl()
        {
            if (_pbReflTried) return;
            _pbReflTried = true;
            _fPbOn = AccessTools.Field(typeof(BattleEndLogic), "_enemyDefenderPullbackEnabled");
            _fPbNeed = AccessTools.Field(typeof(BattleEndLogic), "_troopNumberNeededForEnemyDefenderPullBack");
            _fPbDone = AccessTools.Field(typeof(BattleEndLogic), "_isEnemyDefenderPulledBack");
        }

        /// <summary>456: przy starcie - czy gra liczy zbieglych obroncow do sali lorda i ile ich potrzeba (pole BattleEndLogic, tylko odczyt).</summary>
        private void KeepStart(Mission m)
        {
            KeepRefl();
            var bel = m.GetMissionBehavior<BattleEndLogic>();
            if (bel == null) return;
            if (_fPbOn != null) _keepOn = (bool)_fPbOn.GetValue(bel);
            if (_fPbNeed != null) _keepNeed0 = (int)_fPbNeed.GetValue(bel);
        }

        /// <summary>456: zdjety czlowiek strony obroncow - zbiegly (Routed; panika = morale &lt; 0.01), zabity, nieprzytomny; czas pierwszego zbiega i progu gry.</summary>
        private void KeepCount(Agent a, AgentState st)
        {
            if (a == null || !a.IsHuman) return;
            var t = a.Team;
            if (t == null || t.Side != BattleSideEnum.Defender) return;
            int k = st == AgentState.Routed ? 0 : (st == AgentState.Killed ? 2 : (st == AgentState.Unconscious ? 3 : -1));
            if (k < 0) return;
            bool panic = false;
            if (k == 0) { try { panic = a.GetMorale() < 0.01f; } catch { } }
            var m = Mission;
            float now = m != null ? m.CurrentTime : 0f;
            lock (_keepLock)
            {
                _keep[k]++;
                if (k != 0) return;
                if (panic) _keep[1]++;
                if (_keepFirst < 0f) _keepFirst = now;
                if (_keepThrAt < 0f && _keepNeed0 > 0 && _keep[0] >= _keepNeed0) _keepThrAt = now;
            }
        }

        /// <summary>456: linia konca - zbiegli obroncy wobec progu gry, stan etapu sali lorda wg BattleEndLogic, obroncy w polu i w odwodzie, wynik misji.</summary>
        private void KeepEnd(Mission m)
        {
            KeepRefl();
            int[] c;
            float first, thrAt;
            lock (_keepLock) { c = (int[])_keep.Clone(); first = _keepFirst; thrAt = _keepThrAt; }
            var sb = new StringBuilder(512);
            sb.Append("Oblezenie misja (370) sala lorda (456): t=").Append(m.CurrentTime.ToString("0", Inv)).Append('s');
            string ps = "?";
            try { ps = m.PlayerTeam != null ? (m.PlayerTeam.Side == BattleSideEnum.Attacker ? "atakuje" : (m.PlayerTeam.Side == BattleSideEnum.Defender ? "broni" : "-")) : "brak"; } catch { }
            sb.Append(" | gracz ").Append(ps);
            sb.Append(" | etap sali lorda gry ").Append(_keepOn ? "wlaczony" : "nie (tylko gracz atakuje, nie wypad / odsiecz)");
            if (_keepNeed0 >= 0) sb.Append(", prog ").Append(_keepNeed0).Append(" zbieglych");
            sb.Append(" | obroncy zbiegli ").Append(c[0]).Append(" (w panice ").Append(c[1]).Append(", inni ").Append(c[0] - c[1]).Append("), zabici ").Append(c[2])
              .Append(", nieprzytomni ").Append(c[3]);
            sb.Append(" | pierwszy zbieg ").Append(first >= 0f ? "t=" + first.ToString("0", Inv) + "s" : "nigdy")
              .Append(", prog ").Append(thrAt >= 0f ? "osiagniety t=" + thrAt.ToString("0", Inv) + "s" : "nieosiagniety");
            var bel = m.GetMissionBehavior<BattleEndLogic>();
            if (bel != null)
            {
                try
                {
                    if (_fPbNeed != null) sb.Append(" | zostalo do progu wg gry ").Append((int)_fPbNeed.GetValue(bel));
                    if (_fPbDone != null) sb.Append(", zepchnieci wg gry ").Append((bool)_fPbDone.GetValue(bel) ? "TAK" : "NIE");
                }
                catch { sb.Append(" | stan gry ?"); }
            }
            int field = 0, rem = -1;
            try
            {
                foreach (var t in m.Teams)
                    if (t != null && t.Side == BattleSideEnum.Defender)
                        foreach (var a in t.ActiveAgents) if (a != null && a.IsHuman) field++;
            }
            catch { field = -1; }
            try { var sp = m.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>(); if (sp != null) rem = sp.NumberOfRemainingDefenderTroops; } catch { rem = -1; }
            sb.Append(" | obroncy na koniec: w polu ").Append(field).Append(", w odwodzie spawnera ").Append(rem >= 0 ? rem.ToString(Inv) : "?");
            var r = m.MissionResult;
            sb.Append(" | wynik ").Append(r != null ? r.BattleState.ToString() + (r.EnemyRetreated ? ", odwrot wroga" : "") : "brak (wyjscie bez wyniku)");
            sb.Append(" | potkniecia ").Append(_stumbles).Append(". DefenderPullBack = gra przenosi oblezenie do sali lorda.");
            Log.Info(sb.ToString());
        }

        /// <summary>450: linia konca szturmu - na pas wejscie na mur, przyczolek, maksima, posilki, straty wg zrodla; odstep wejsc 1. i 2. pasa; udzial pasa glownego 0-180 s.</summary>
        private void LaneEnd(Mission m)
        {
            var sb = new StringBuilder(1024);
            sb.Append("Oblezenie misja (370) pasy koniec (450): t=").Append(m.CurrentTime.ToString("0", Inv)).Append('s');
            if (_laneN == 0) { sb.Append(" | pasow brak (SiegeLanes puste przez cala misje) | potkniecia ").Append(_stumbles).Append('.'); Log.Info(sb.ToString()); return; }
            int[,,] lost;
            lock (_lostLock) lost = (int[,,])_laneLost.Clone();
            for (int i = 0; i < _laneN; i++)
            {
                sb.Append(i > 0 ? " ||" : " |").Append(' ').Append(_laneName[i]).Append(": na murze ").Append(_wallAt[i] >= 0f ? "t=" + _wallAt[i].ToString("0", Inv) + "s" : "nigdy")
                  .Append(", przyczolek ").Append(_holdAt[i] >= 0f ? "od t=" + _holdAt[i].ToString("0", Inv) + "s, lacznie " + _holdSec[i].ToString("0", Inv) + "s" : "nigdy")
                  .Append(", max atak blisko ").Append(_closeMax[i]).Append(", max obroncow ").Append(_defMax[i])
                  .Append(", posilki A ").Append(_reinf[0, i]).Append(" / O ").Append(_reinf[1, i])
                  .Append(", straty A ").Append(lost[0, i, 0]).Append('/').Append(lost[0, i, 1]).Append('/').Append(lost[0, i, 2])
                  .Append(", O ").Append(lost[1, i, 0]).Append('/').Append(lost[1, i, 1]).Append('/').Append(lost[1, i, 2]);
            }
            sb.Append(" | bez pasa: posilki A ").Append(_reinf[0, LaneMax]).Append(" / O ").Append(_reinf[1, LaneMax])
              .Append(", padl / bez szyku A ").Append(_reinf[0, LaneMax + 1]).Append(" / O ").Append(_reinf[1, LaneMax + 1])
              .Append(", straty A ").Append(lost[0, LaneMax, 0]).Append('/').Append(lost[0, LaneMax, 1]).Append('/').Append(lost[0, LaneMax, 2])
              .Append(", O ").Append(lost[1, LaneMax, 0]).Append('/').Append(lost[1, LaneMax, 1]).Append('/').Append(lost[1, LaneMax, 2]);
            // odstep wejsc na mur: pierwszy i drugi pas wedlug czasu
            int first = -1, second = -1;
            for (int i = 0; i < _laneN; i++)
            {
                if (_wallAt[i] < 0f) continue;
                if (first < 0 || _wallAt[i] < _wallAt[first]) { second = first; first = i; }
                else if (second < 0 || _wallAt[i] < _wallAt[second]) second = i;
            }
            sb.Append(" | wejscie na mur: ");
            if (first < 0) sb.Append("zaden pas");
            else
            {
                sb.Append("pierwszy ").Append(_laneName[first]).Append(" t=").Append(_wallAt[first].ToString("0", Inv)).Append("s, ");
                if (second < 0) sb.Append("drugi pas nie wszedl");
                else sb.Append("drugi ").Append(_laneName[second]).Append(" po ").Append((_wallAt[second] - _wallAt[first]).ToString("0", Inv)).Append(" s");
            }
            sb.Append(" | udzial pasa glownego 0-").Append(OpenWindow.ToString("0", Inv)).Append(" s (probek ").Append(_shareSamples).Append("): przydzial ").Append(Share(_shareAsg))
              .Append(", obecnosc w regionie ").Append(Share(_sharePres)).Append(", szyk na dwoch pasach (probki x szyki) ").Append(_dupAsg)
              .Append(" | fale posilkow A ").Append(_waves[0]).Append(", O ").Append(_waves[1])
              .Append(" | straty = pociski/wrecz/inne; na murze = >= ").Append(WallMin).Append(" atak blisko, przyczolek = >= ").Append(HoldMin).Append(" atak blisko i < ").Append(HoldMin)
              .Append(" obroncow; probka co 1 s z pamieci gry (5 s), liczniki tylko glownej druzyny atakujacej | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private string Share(double[] v)
        {
            double sum = 0; int best = -1;
            for (int i = 0; i < _laneN; i++) { sum += v[i]; if (best < 0 || v[i] > v[best]) best = i; }
            if (best < 0 || sum <= 0) return "-";
            var s = new StringBuilder();
            s.Append(_laneName[best]).Append(' ').Append((100.0 * v[best] / sum).ToString("0", Inv)).Append("% (");
            for (int i = 0; i < _laneN; i++) s.Append(i > 0 ? " " : "").Append((100.0 * v[i] / sum).ToString("0", Inv));
            return s.Append(')').ToString();
        }

        // 437-p: pola QueryData<int> SiegeQuerySystem (refleksja raz na nazwe); odczyt GetCachedValue - bez Evaluate i bez zapisu czasu waznosci
        private static readonly Dictionary<string, FieldInfo> QFields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);

        private static string QCached(SiegeQuerySystem q, string field)
        {
            int v = QVal(q, field);
            return v >= 0 ? v.ToString(Inv) : "?";
        }

        /// <summary>450: wartosc z pamieci QueryData (GetCachedValue) albo -1, gdy pola brak (liczniki gry sa nieujemne).</summary>
        internal static int QVal(SiegeQuerySystem q, string field)
        {
            FieldInfo fi;
            if (!QFields.TryGetValue(field, out fi))
            {
                try { fi = AccessTools.Field(typeof(SiegeQuerySystem), field); } catch { fi = null; }
                QFields[field] = fi;
            }
            if (fi == null || q == null) return -1;
            var qd = fi.GetValue(q) as QueryData<int>;
            return qd != null ? qd.GetCachedValue() : -1;
        }

        internal static string LanePrefix(FormationAI.BehaviorSide side)
        {
            return side == FormationAI.BehaviorSide.Left ? "_left" : (side == FormationAI.BehaviorSide.Middle ? "_middle" : "_right");
        }

        private void Side(Mission m, BattleSideEnum side, string head, List<string> bad)
        {
            var sb = _sb;
            sb.Length = 0;
            sb.Append(head).Append(" | strona ").Append(side == BattleSideEnum.Attacker ? "A" : "O");
            int teamsN = 0;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                Team t;
                try
                {
                    t = teams[i];
                    if (!MissionTeams.Valid(t) || t.Side != side) continue;
                }
                catch (Exception e) { Stumble("Druzyna", e); continue; }
                teamsN++;
                try
                {
                    sb.Append(" | T").Append(t.TeamIndex).Append(t.IsPlayerTeam ? " gracz" : (t.IsPlayerAlly ? " sojusz" : ""))
                      .Append(" taktyka: ").Append(TacticText(t)).Append(" | szyki:");
                }
                catch (Exception e) { Stumble("Taktyka", e); sb.Append(" taktyka: ? | szyki:"); }
                int fN = 0;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    try
                    {
                        var f = fl[j];
                        if (f == null) continue;
                        if (f.CountOfUnits <= 0)
                        {
                            // Pusty szyk: w linii strony pomijany, ale na liscie niewaznych pozycji TAK - komunikat silnika
                            // "Formation order position is not valid" idzie z CreateNewOrderWorldPosition wolanego przez
                            // CacheAverageAndMedianPositionAndVelocity tylko dla szyku z CountOfUnits == 0 (Formation.cs:1453).
                            if (bad != null && !f.OrderPositionIsValid)
                                bad.Add("T" + t.TeamIndex + " F" + (int)f.FormationIndex + " (" + (side == BattleSideEnum.Attacker ? "A" : "O") + ", pusty, rozkaz " + f.GetReadonlyMovementOrderReference().OrderEnum + ")");
                            continue;
                        }
                        fN++;
                        int idx = (int)f.FormationIndex;
                        sb.Append(' ').Append('F').Append(idx).Append(' ').Append(idx >= 0 && idx < FcName.Length ? FcName[idx] : f.FormationIndex.ToString())
                          .Append(f.IsAIControlled ? "" : "(gracz)")
                          .Append(" ludzi ").Append(f.CountOfUnits);
                        int[] mx;
                        if (_mix.TryGetValue(f, out mx)) sb.Append(" (dyst ").Append(mx[0]).Append(", konno ").Append(mx[1]).Append(')');   // 372: sklad (indeks F to miejsce szyku, nie klasa ludzi)
                        int det = f.CountOfDetachedUnits;
                        if (det > 0) sb.Append(" (odl. ").Append(det).Append(')');
                        var b = f.AI != null ? f.AI.ActiveBehavior : null;
                        sb.Append(", zach ").Append(BehName(b));
                        var ord = f.GetReadonlyMovementOrderReference().OrderEnum;
                        bool valid = f.OrderPositionIsValid;
                        sb.Append(", rozkaz ").Append(ord.ToString()).Append(" poz ").Append(valid ? "TAK" : "NIE");
                        var avg = f.CachedAveragePosition;
                        if (valid) sb.Append(" do celu ").Append(avg.Distance(f.OrderPosition).ToString("0", Inv)).Append('m');
                        float d2 = f.CachedClosestEnemyFormationDistanceSquared;
                        sb.Append(", wrog ").Append(d2 >= 0f && d2 < float.MaxValue * 0.5f ? ((float)Math.Sqrt(d2)).ToString("0", Inv) + "m" : "-");
                        int h;
                        _hits.TryGetValue(f, out h);
                        sb.Append(", ciosy30s ").Append(h).Append(';');
                        if (bad != null && !valid)
                            bad.Add("T" + t.TeamIndex + " F" + idx + " (" + (side == BattleSideEnum.Attacker ? "A" : "O") + ", ludzi " + f.CountOfUnits + ", rozkaz " + ord + ", zach " + BehName(b) + ")");
                    }
                    catch (Exception e) { Stumble("Szyk", e); sb.Append(" ?;"); }
                }
                if (fN == 0) sb.Append(" brak");
                if (side == BattleSideEnum.Defender) sb.Append(Posts(t));
            }
            if (teamsN == 0) sb.Append(" | brak druzyn");
            Log.Info(sb.ToString());
        }

        /// <summary>372: sklad szykow (ludzie z bronia dystansowa i amunicja, konni) - jeden przeglad agentow na odczyt.</summary>
        private void Mix(Mission m)
        {
            _mix.Clear();
            try
            {
                foreach (var a in m.Agents)
                {
                    try
                    {
                        if (a == null || !a.IsActive() || !a.IsHuman) continue;
                        var f = a.Formation;
                        if (f == null) continue;
                        int[] mx;
                        if (!_mix.TryGetValue(f, out mx)) { mx = new int[2]; _mix[f] = mx; }
                        if (a.IsRangedCached) mx[0]++;
                        if (a.HasMount) mx[1]++;
                    }
                    catch (Exception e) { Stumble("Sklad", e); }
                }
            }
            catch (Exception e) { Stumble("Sklad", e); }
        }

        /// <summary>372: stanowiska strzelcow obroncy (TeamAISiegeDefender.ArcherPositions) i szyki, ktore strzelaja z murow (ShootFromCastleWalls).</summary>
        private static string Posts(Team t)
        {
            try
            {
                var d = t.TeamAI as TeamAISiegeDefender;
                if (d == null || d.ArcherPositions == null) return " | stanowiska strzelcow -";
                var used = new StringBuilder();
                foreach (var f in t.FormationsIncludingSpecialAndEmpty)
                {
                    if (f == null || f.CountOfUnits <= 0 || f.AI == null) continue;
                    var b = f.AI.ActiveBehavior as BehaviorShootFromCastleWalls;
                    if (b == null || b.ArcherPosition == null) continue;
                    used.Append(used.Length > 0 ? "," : "").Append('F').Append((int)f.FormationIndex);
                }
                return " | stanowiska strzelcow " + d.ArcherPositions.Count + ", strzelaja z murow " + (used.Length > 0 ? used.ToString() : "-");
            }
            catch { return " | stanowiska strzelcow ?"; }
        }

        private void Machines(Mission m, string head)
        {
            var sb = _sb;
            sb.Length = 0;
            sb.Append(head).Append(" | machiny:");
            var rams = new StringBuilder(); var towers = new StringBuilder(); var ladders = new StringBuilder();
            int nRam = 0, nTower = 0, nLad = 0, nLadUp = 0, nRanged = 0, nRangedMen = 0, nGate = 0, nGateDown = 0, nGateOpen = 0, nBreach = 0;
            var all = m.MissionObjects;
            int n = all != null ? all.Count : 0;
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var o = all[i];
                    if (o == null) continue;
                    if (o is BatteringRam r)
                    {
                        nRam++;
                        rams.Append(rams.Length > 0 ? "; " : "").Append(MachText(r, r.TargetCastlePosition, r.HasArrivedAtTarget, r.IsDestroyed))
                            .Append(", stan ").Append(r.State.ToString());
                    }
                    else if (o is SiegeTower t)
                    {
                        nTower++;
                        towers.Append(towers.Length > 0 ? "; " : "").Append(MachText(t, t.TargetCastlePosition, t.HasArrivedAtTarget, t.IsDestroyed))
                              .Append(", brama ").Append(t.State.ToString());
                    }
                    else if (o is SiegeLadder l)
                    {
                        nLad++;
                        var st = l.State;
                        if (st != SiegeLadder.LadderState.OnLand && st != SiegeLadder.LadderState.FallToLand) nLadUp++;
                        ladders.Append(ladders.Length > 0 ? "; " : "").Append(st.ToString()).Append(' ')
                               .Append(l.UserCountIncludingInStruckAction).Append('/').Append(Points(l))
                               .Append(Users(l)).Append(l.IsDisabled ? " wyl" : "").Append(l.IsDisabledForAI ? " AIwyl" : "");
                    }
                    else if (o is RangedSiegeWeapon w)
                    {
                        if (w.IsDisabled || w.IsDestroyed) continue;
                        nRanged++;
                        nRangedMen += w.UserCountIncludingInStruckAction;
                    }
                    else if (o is CastleGate g)
                    {
                        if (!g.IsDestructible) continue;
                        nGate++;
                        if (g.IsDestroyed) nGateDown++;
                        else if (g.IsGateOpen) nGateOpen++;
                    }
                    else if (o is WallSegment ws)
                    {
                        if (ws.IsBreachedWall) nBreach++;
                    }
                }
                catch (Exception e) { Stumble("Machina", e); }
            }
            sb.Append(" taran ").Append(nRam).Append(nRam > 0 ? " [" + rams + "]" : "")
              .Append(" | wieza ").Append(nTower).Append(nTower > 0 ? " [" + towers + "]" : "")
              .Append(" | drabiny ").Append(nLad).Append(" (w gorze ").Append(nLadUp).Append(')').Append(nLad > 0 ? " [" + ladders + "]" : "")
              .Append(" | miotajace ").Append(nRanged).Append(" (ludzi ").Append(nRangedMen).Append(')')
              .Append(" | bramy ").Append(nGate).Append(" (rozbite ").Append(nGateDown).Append(", otwarte ").Append(nGateOpen).Append(')')
              .Append(" | wylomy ").Append(nBreach)
              .Append(" | strzaly 30s: O ").Append(_shotsD).Append(", A ").Append(_shotsA)
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Taran / wieza: ludzie na stoiskach / stoisk, dojechal, odleglosc do celu (brama / odcinek muru), szyki uzytkownikow, wylaczenia.</summary>
        private static string MachText(UsableMachine u, MissionObject target, bool arrived, bool destroyed)
        {
            var s = new StringBuilder();
            s.Append("ludzi ").Append(u.UserCountIncludingInStruckAction).Append('/').Append(Points(u))
             .Append(", dojechal ").Append(arrived ? "TAK" : "NIE");
            if (destroyed) s.Append(", ZNISZCZONA");
            try
            {
                var k = target as ICastleKeyPosition;
                var me = u.GameEntity.GlobalPosition;
                if (k != null) s.Append(", do celu ").Append(me.AsVec2.Distance(k.GetPosition().AsVec2).ToString("0", Inv)).Append('m');
                else if (target != null) s.Append(", do celu ").Append(me.AsVec2.Distance(target.GameEntity.GlobalPosition.AsVec2).ToString("0", Inv)).Append('m');
                else s.Append(", cel -");
            }
            catch { s.Append(", do celu ?"); }
            s.Append(Users(u));
            if (u.IsDisabled) s.Append(", wyl");
            if (u.IsDisabledForAI) s.Append(", AIwyl");
            if (u.IsDeactivated) s.Append(", nieczynna");
            return s.ToString();
        }

        private static int Points(UsableMachine u)
        {
            var sp = u.StandingPoints;
            return sp != null ? sp.Count : 0;
        }

        /// <summary>Szyki uzytkownikow machiny (UserFormations): " szyki T1F2,T1F5" albo " szyki -".</summary>
        private static string Users(UsableMachine u)
        {
            var uf = u.UserFormations;
            if (uf == null || uf.Count == 0) return ", szyki -";
            var s = new StringBuilder(", szyki ");
            for (int i = 0; i < uf.Count; i++)
            {
                var f = uf[i];
                if (f == null) continue;
                if (s.Length > 8) s.Append(',');
                s.Append('T').Append(f.Team != null ? f.Team.TeamIndex : -1).Append('F').Append((int)f.FormationIndex);
            }
            return s.Length > 8 ? s.ToString() : ", szyki -";
        }

        private static string BehName(BehaviorComponent b)
        {
            if (b == null) return "-";
            // Tag RBM: tylko dla wlasnych klas RBMBehavior*. Vanillowe UseSiegeMachines, AssaultWalls, WaitForLadders,
            // ShootFromCastleWalls (i TacticDefendCastle) RBMAI zmienia latkami - wypisane bez tagu, ale logika jest RBM.
            string n = b.GetType().Name;
            if (n.StartsWith("RBMBehavior", StringComparison.Ordinal)) return "RBM:" + n.Substring(11);
            if (n.StartsWith("Behavior", StringComparison.Ordinal) && n.Length > 8) return n.Substring(8);
            return n;
        }

        // ------------------------------------------------------------ taktyka (refleksja raz na proces, tylko odczyt)

        private static void Refl()
        {
            if (_reflTried) return;
            _reflTried = true;
            try { _fTactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic"); } catch { _fTactic = null; }
            try { _fBreachState = AccessTools.Field(typeof(TacticBreachWalls), "_tacticState"); } catch { _fBreachState = null; }
            try { _fLanes = AccessTools.Field(typeof(TacticBreachWalls), "_lanesInUse"); } catch { _fLanes = null; }
        }

        private static string TacticText(Team t)
        {
            Refl();
            var ai = t.TeamAI;
            if (ai == null) return "brak AI druzyny";
            if (_fTactic == null) return "? (brak pola _currentTactic)";
            var tac = _fTactic.GetValue(ai) as TacticComponent;
            if (tac == null) return "-";
            string n = tac.GetType().Name;
            if (n.StartsWith("Tactic", StringComparison.Ordinal) && n.Length > 6) n = n.Substring(6);
            if (tac is TacticBreachWalls)
            {
                string st = "?", lanes = "?";
                try { if (_fBreachState != null) st = Convert.ToString(_fBreachState.GetValue(tac), Inv); } catch { }
                try { if (_fLanes != null) lanes = Convert.ToString(_fLanes.GetValue(tac), Inv); } catch { }
                return n + " (" + st + ", pasy " + lanes + ")";
            }
            if (tac is TacticDefendCastle dc)
            {
                string st = "?";
                try { st = dc.CurrentTacticState.ToString(); } catch { }
                return n + " (" + st + ")";
            }
            return n;
        }
    }
}
