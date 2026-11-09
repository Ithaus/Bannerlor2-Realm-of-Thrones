using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 371 (Jeff 09.11, Goldentooth: ok. 600 obroncow gracza wobec 13,4 tys. Polnocy - "nie jest tak ze oblezenie sie skonczy az zabije 13k ludzi w jednej
    /// bitwie, tylko szturm sie zalamywal i kontynuowano za dzien dwa 5"). ZALAMANIE SZTURMU W MISJI - faza 1: gracz BRONI, wodz atakujacych AI.
    /// Dowod (dekompilacja 1.4.8, scratchpad/d371): misja szturmu konczy sie dzis tylko wyczerpaniem strony (BattleEndLogic.IsSideDepleted) - TacticBreachWalls
    /// odstepuje, gdy stosunek sil spadnie ponizej startowego (przy 22:1 nigdy), a RBM co klatke gasi odwrot komponentu (CommonAIComponent.StopRetreating) -
    /// wiec AI nie odstepuje nigdy; wygrana gracza bez flagi odwrotu = PlayerEncounter.ContinueBattle "Battle continues" = kolejna misja z nastepnymi ~2,8 tys.,
    /// az do wybicia calej armii. Gra MA droge odstapienia bez rozbicia oblezenia: wynik misji z EnemyRetreated przy szturmie (wrogiem jest strona atakujaca)
    /// -> ContinueBattle -> MapEvent.FinishBattleAndKeepSiegeEvent + menu_siege_strategies (bitwa bez zwyciezcy, oboz zostaje) - ta sama metoda co OB2.
    /// Zasady:
    ///  - start (po rozstawieniu): oddzial w misji K_m = min(K, GetTotalNumberOfTroopsForSide(Attacker)) - nie wiecej, niz misja w ogole wystawi; reszta armii
    ///    zostaje w obozie, bo zalamanie konczy bitwe (FinishBattleAndKeepSiegeEvent). Wystawiania NIE obcinamy przed zalamaniem: StopSpawner przy ludziach
    ///    w rezerwie trzyma IsSideDepleted na false (RemainingSpawnNumber &gt; 0) - misja nie skonczylaby sie wyczerpaniem.
    ///  - prog: zabici + nieprzytomni atakujacy (ludzie; uciekinierzy nie) tej misji + z poprzednich misji tej bitwy &gt;= prog x K_m; prog = OB2 (SZ3) x wodz (fW)
    ///    x SiegeAssaultBreakShockFactor po upadku wodza atakujacych (Team.GeneralAgent zabity / nieprzytomny) i drugi raz po utracie WSZYSTKICH taranow
    ///    i wiez, ktore staly na starcie (drabin nie liczymy - gra stawia je wciaz od nowa); sprawdzane co 1 s.
    ///  - 371b (Jeff 09.11 "jesli stracil 1000 ale my 2000 to sie nie wycofa"; Goldentooth: 598 / 598 przy bramach 2/2 i dalej ok. 2330 strat atakujacych
    ///    wobec 660 obroncow, 3.5:1 - stara regula "wszystkie bramy rozbite = bez zalamania" wylaczala sprawdzanie do konca misji). Po progu decyduje
    ///    WYMIANA STRAT X = A / D (A - straty atakujacych tej bitwy, D - zabici + nieprzytomni obroncy tej bitwy): zalamanie, gdy X &gt; R,
    ///    R = min(SiegeAssaultBreakExchangeMax, 1 + SiegeAssaultBreakExchangeSlope x ln(przewaga N)) [x SiegeAssaultBreakGateExchangeFactor przy rozbitych
    ///    bramach]; N = ludzie atakujacych / obroncow w misji (spawner gry). Rozbite bramy podnosza prog (x SiegeAssaultBreakGateFactor), nie wylaczaja.
    ///    Twardy pulap: A &gt;= SiegeAssaultBreakHardCap x prog bazowy -&gt; R = 1 (kto placi wiecej, niz zadaje, odstepuje). Kto wygrywa wymiane (X &lt;= 1),
    ///    nie odstepuje nigdy. Zdobywaja: obroncow w polu + w odwodzie spawnera mniej niz SiegeAssaultBreakTakenShare wszystkich obroncow bitwy - bez
    ///    zalamania (sprawdzane co 1 s, bez trwalej flagi). Wymiana wylaczona - sam prog (z mnoznikiem bram) + "zdobywaja".
    ///  - zalamanie (raz na misje): StopSpawner(Attacker); kazdy czlowiek AI atakujacych - StopUsingGameObject (bez ponownego przypiecia) i natywne
    ///    Agent.Retreat(Mission.GetClosestFleePositionForAgent) - BEZ CommonAIComponent i bez rozkazu Retreat szyku, wiec RBM tego nie gasi (wzor RoutHold 143);
    ///    co 4 s ponowione u tych, ktorym odwrot zgasl; wystawieni po zalamaniu (takze Dynamic Reinforcements - wystawia z pominieciem spawnera gry) dostaja
    ///    odwrot w nastepnym ticku (OnAgentBuild). Uciekinier ze zwyklym morale schodzi z pola jako odwrot na rozkaz (BattleAgentLogic: SetRouted(morale &gt;= 0.01))
    ///    - zostaje w druzynie. Komunikat "The assault breaks! The enemy falls back from the walls."
    ///  - koniec: BattleEndLogic sam zglasza odwrot wroga (wszyscy aktywni IsRunningAway dluzej niz odstep posilkow + 3 s); bezpiecznik - po
    ///    SiegeAssaultBreakEndSeconds (45 s) nasze MissionEnded daje ten sam wynik (DefenderVictory, EnemyRetreated) - ta sama droga kampanii.
    ///  - przerwa przed kolejnym szturmem: SiegeWalls.OnMapEventEnded (a.MissionBreak) -&gt; Repel bez FinishBattle (1-5 / 7-14 dob, zapis arm_siege_walls).
    ///  - 378 (Jeff 09.11 "jak nie zdobede w iles minut to tez wycofanie np 30 minut"; Goldentooth: 1874 s misji bez rozstrzygniecia): LIMIT CZASU.
    ///    Czas walki = Mission.CurrentTime od Start (koniec rozstawienia, jak prog) + MisTime z poprzednich misji tej bitwy. Po SiegeAssaultTimeLimitMinutes
    ///    (domyslnie 30) - Break jak przy zalamaniu (ten sam odwrot, bezpiecznik, ranni 373 przez NoteBreak, przerwa 1-5 / 7-14 dob przez MissionBreak
    ///    i MisEngLost), niezaleznie od progu i wymiany strat; wyjatek tylko "zdobywaja" (obroncow &lt; TakenShare) - wtedy szturm trwa. Gracz atakuje: SiegeClock378.
    /// Gracz atakuje (faza 2) - bez zmian (osobny wpis, zasada "jedna zmiana naraz"). Wyjatek per czlowiek / obiekt = potkniecie, bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeBreak371 : MissionLogic
    {
        private const float EvalEvery = 1f, ResendEvery = 4f, NoDeployStart = 10f;
        private const int PerTick = 150, ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private bool _known, _active, _started, _broken, _fuse, _ended, _genDown, _engDown, _genLogged, _engLogged, _takenNoted, _holdNoted, _cap;
        private float _nextEval, _breakAt = -1f, _nextResend;
        private int _cas, _prior, _km, _total, _def0, _sent, _resent, _late, _stumbles, _errs, _engines;
        private int _dcas, _dPrior, _dTot, _dLeft, _state;               // 371b: straty obroncow tej misji / poprzednich misji, obroncy w misji (spawner), zostalo; stan 0 ponizej progu, 1 trwa (wymiana), 2 zdobywaja
        private double _thr, _limit, _x, _r = -1, _n = 1, _share = 1, _nextLogA;
        private string _why = "-";
        private float _startAt, _tPrior, _endAt = -1f;                                    // 378: czas misji na starcie walki, sekundy walki z poprzednich misji tej bitwy
        private bool _byTime, _timeNoted;                                   // 378: zalamanie z limitu czasu; limit minal przy "zdobywaja" (raz na misje)
        private MapEvent _me;
        private SiegeWalls.Assault _a;
        private Agent _general;
        private readonly List<UsableMachine> _machines = new List<UsableMachine>();
        private readonly List<Agent> _queue = new List<Agent>();
        private readonly HashSet<Agent> _sentTo = new HashSet<Agent>();
        private int _qi;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        /// <summary>372: regula zalamania dziala w tej misji (szturm, gracz w obronie, rekord szturmu) - odwrot taktyki atakujacych tylko przez 371.</summary>
        internal bool HoldsAssault { get { return _known && _active && !_ended; } }

        /// <summary>372: szturm juz sie zalamal (atakujacy schodza z murow) - bez zwalniania drabin.</summary>
        internal bool IsBroken { get { return _broken; } }

        private static string F(double v, string f) { return v.ToString(f, Inv); }

        private static bool Attacker(Agent a)
        {
            var t = a != null ? a.Team : null;
            return t != null && t.Side == BattleSideEnum.Attacker;
        }

        /// <summary>Raz na misje: szturm, gracz w obronie, rekord szturmu AI (SiegeWalls.MissionAssault).</summary>
        private bool Known(Mission m)
        {
            if (_known) return _active;
            _known = true;
            try
            {
                if (m == null || !m.IsSiegeBattle || !SiegeWalls.BreakMissionOn) return _active = false;
                var pt = m.PlayerTeam;
                if (pt == null || pt.Side != BattleSideEnum.Defender) return _active = false;   // faza 1: tylko gracz-obronca
                _me = MapEvent.PlayerMapEvent;
                SiegeWalls.NoteMissionSeen();
                _a = SiegeWalls.MissionAssault(_me, out _why);
                if (_a == null)
                {
                    SiegeWalls.NoteMissionNone();
                    Log.Info("Szturm (371): misja poza regula zalamania - " + _why + ".");
                    return _active = false;
                }
                if (SiegeWalls.EffThr(_a) <= 0.0)
                {
                    // prog OB2 0 (SiegeAssaultRepelLoss wylaczony) - _limit 0 dalby zalamanie w pierwszej sekundzie kazdej misji
                    SiegeWalls.NoteMissionNone();
                    Log.Info("Szturm (371): misja poza regula zalamania - prog OB2 0 (OB2 wylaczony).");
                    return _active = false;
                }
                _prior = _a.MisCas;
                _dPrior = _a.MisDefCas;
                _tPrior = Math.Max(0f, _a.MisTime);                             // 378
                _active = true;
            }
            catch (Exception e) { Stumble("Known", e); _active = false; }
            return _active;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (!_active || _ended) return;
            try
            {
                if (affectedAgent == null || !affectedAgent.IsHuman) return;
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
                if (!Attacker(affectedAgent))
                {
                    var t = affectedAgent.Team;
                    if (t != null && t.Side == BattleSideEnum.Defender) _dcas++;   // 371b: strata obroncow (wymiana)
                    return;
                }
                _cas++;
                if (_general != null && ReferenceEquals(affectedAgent, _general)) _genDown = true;
            }
            catch (Exception e) { Stumble("Removed", e); }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (!_broken || _ended) return;
            try
            {
                if (agent != null && agent.IsHuman && agent.IsAIControlled && Attacker(agent)) { _queue.Add(agent); _late++; }
            }
            catch (Exception e) { Stumble("Build", e); }
        }

        public override void OnMissionTick(float dt)
        {
            if (_ended) return;
            var m = Mission;
            if (m == null) return;
            try
            {
                if (!Known(m)) { _ended = true; return; }
                float now = m.CurrentTime;
                if (!_started)
                {
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    Start(m, now);
                    return;
                }
                if (!_broken)
                {
                    if (m.MissionEnded) { if (_endAt < 0f) _endAt = now; return; }   // misja skonczona bez zalamania - nic do oceny (378: chwila konca walki)
                    if (now < _nextEval) return;
                    _nextEval = now + EvalEvery;
                    Evaluate(m, now);
                    return;
                }
                Drive(m, now);                                                    // po zalamaniu takze po koncu misji (do OnEndMission): kto jeszcze walczy na murach, schodzi - inaczej gracz nie wyjdzie TAB-em (BattleEndLogic.TryExit: wrog obok)
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private void Start(Mission m, float now)
        {
            _started = true;
            _startAt = now;                                                   // 378: limit czasu liczony od konca rozstawienia
            try
            {
                var sp = m.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();   // spawner szturmu SandBox (fale z BannerlordConfig); inny - K bez obciecia
                _total = sp != null ? sp.GetTotalNumberOfTroopsForSide(BattleSideEnum.Attacker) : 0;
                _dTot = sp != null ? sp.GetTotalNumberOfTroopsForSide(BattleSideEnum.Defender) : 0;
            }
            catch (Exception e) { Stumble("Start.spawn", e); }
            int k = Math.Max(1, _a.K);
            _km = _total > 0 ? Math.Min(k, _total) : k;
            _thr = SiegeWalls.EffThr(_a);
            try { var t = m.AttackerTeam; _general = t != null ? t.GeneralAgent : null; } catch { _general = null; }
            var all = m.MissionObjects;
            int n = all != null ? all.Count : 0;
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var o = all[i];
                    if ((o is BatteringRam || o is SiegeTower) && !((UsableMachine)o).IsDestroyed) _machines.Add((UsableMachine)o);
                }
                catch (Exception e) { Stumble("Start.machine", e); }
            }
            _engines = _machines.Count;
            _def0 = DefendersActive(m);
            if (_dTot <= 0) _dTot = Math.Max(_def0, _a.Def0);                    // 371b: zapas, gdy spawner inny
            _n = Math.Max(1.0, (_total > 0 ? _total : _a.Men0) / (double)Math.Max(1, _dTot));
            _a.Missions++;
            Log.Info("Szturm (371): start misji | osada " + (_a.St != null ? _a.St.StringId : "-") + ", wodz " + _a.Leader + " (" + _a.FWTxt + ") | armia na starcie szturmu " + _a.Men0
                     + ", obroncow " + _a.Def0 + " | oddzial szturmowy K " + _a.K + ", w misji K_m " + _km + " (misja wystawi " + _total + ") | prog " + F(_thr * 100.0, "0.0")
                     + "% (OB2 " + F(_a.Thr * 100.0, "0.0") + "% x wodz " + F(_a.FW, "0.00") + ") = " + F(_thr * _km, "0") + " strat | straty z poprzednich misji tej bitwy " + _prior
                     + " | tarany i wieze w misji " + _engines + ", wodz atakujacych w misji " + (_general != null ? "tak" : "nie") + ", obroncow w misji " + _def0
                     + " | misja tej bitwy nr " + _a.Missions + (_why != null ? " (" + _why + ")" : "")
                     + " | 371b: obroncow w misji (spawner) " + _dTot + ", straty obrony z poprzednich misji " + _dPrior + ", przewaga " + F(_n, "0.0") + "x -> granica wymiany "
                     + (SiegeWalls.ExchangeOn ? F(ExchangeLimit(false), "0.00") + ":1 (bramy rozbite " + F(ExchangeLimit(true), "0.00") + ":1)" : "wylaczona")
                     + " | 378: limit czasu " + (SiegeWalls.TimeLimitSeconds > 0f ? F(SiegeWalls.TimeLimitSeconds / 60.0, "0") + " min, z poprzednich misji " + F(_tPrior, "0") + " s" : "wylaczony") + ".");
        }

        /// <summary>Bramy niszczalne w misji i ile z nich rozbitych.</summary>
        private void CountGates(Mission m, out int gates, out int down)
        {
            gates = 0; down = 0;
            var all = m.MissionObjects;
            int n = all != null ? all.Count : 0;
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var g = all[i] as CastleGate;
                    if (g == null || !g.IsDestructible) continue;
                    gates++;
                    if (g.IsDestroyed) down++;
                }
                catch (Exception e) { Stumble("Gate", e); }
            }
        }

        /// <summary>378: limit czasu minal - odwrot jak przy zalamaniu, chyba ze "zdobywaja" (wtedy szturm trwa, co sekunde od nowa).</summary>
        private void TimeUp(Mission m, float now, float fought, int A, double lim0)
        {
            int gates, down;
            CountGates(m, out gates, out down);
            bool gatesDown = gates > 0 && down >= gates;
            _limit = lim0 * (gatesDown ? SiegeWalls.GateFactor : 1.0);
            int dNow = DefendersActive(m);
            _dLeft = DefendersLeft(m, dNow);
            int D = _dPrior + _dcas;
            _share = _dLeft / Math.Max(1.0, _dTot + _dPrior);
            _x = A / (double)Math.Max(1, D);
            if (_share < SiegeWalls.TakenShare)
            {
                if (!_timeNoted)
                {
                    _timeNoted = true;
                    SiegeWalls.NoteMissionTimeUp(true);
                    Log.Info("Szturm (378): limit czasu minal (" + F(fought, "0") + " s walki, limit " + F(SiegeWalls.TimeLimitSeconds, "0") + " s), szturm trwa - ZDOBYWAJA (obroncow zostalo "
                             + _dLeft + ", " + F(_share * 100.0, "0.0") + "% < " + F(SiegeWalls.TakenShare * 100.0, "0") + "%) | straty A " + A + " / O " + D + ", wymiana " + F(_x, "0.00") + ":1.");
                }
                return;
            }
            _byTime = true;
            SiegeWalls.NoteMissionTimeUp(false);
            Log.Info("Szturm (378): limit czasu - atakujacy odstepuja | osada " + (_a.St != null ? _a.St.StringId : "-") + " | czas walki " + F(fought, "0") + " s (ta misja "
                     + F(now - _startAt, "0") + " s, wczesniej " + F(_tPrior, "0") + " s) / limit " + F(SiegeWalls.TimeLimitSeconds, "0") + " s | straty A " + A + " (prog " + F(_limit, "0")
                     + ") / O " + D + ", wymiana " + F(_x, "0.00") + ":1 | bramy rozbite " + down + "/" + gates + " | obroncow zostalo " + _dLeft + " (" + F(_share * 100.0, "0.0") + "%), w polu " + dNow + ".");
            Break(m, now, gates, down, dNow);
        }

        private static int DefendersActive(Mission m)
        {
            int d = 0;
            foreach (var t in m.Teams)
            {
                if (t == null || t.Side != BattleSideEnum.Defender) continue;
                foreach (var a in t.ActiveAgents) if (a != null && a.IsHuman) d++;
            }
            return d;
        }

        /// <summary>371b: najwyzsza znosna wymiana strat przy przewadze _n: (1 + Slope x ln N) x GateExchangeFactor przy rozbitych bramach; sufit ExchangeMax
        /// obejmuje TAKZE bramy (recenzja 371b: mnoznik po suficie dawal 3.6:1 przy N &gt;= 85, wbrew opisowi MCM "however great its numbers"); nie mniej niz 1:1.</summary>
        private double ExchangeLimit(bool gatesDown)
        {
            double r = (1.0 + SiegeWalls.ExchangeSlope * Math.Log(Math.Max(1.0, _n))) * (gatesDown ? SiegeWalls.GateExchangeFactor : 1.0);
            return Math.Max(1.0, Math.Min(SiegeWalls.ExchangeMax, r));
        }

        /// <summary>371b: obroncy w polu + jeszcze do wystawienia przez spawner gry.</summary>
        private int DefendersLeft(Mission m, int dNow)
        {
            int rem = 0;
            try { var sp = m.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>(); if (sp != null) rem = sp.NumberOfRemainingDefenderTroops; }
            catch (Exception e) { Stumble("Eval.spawn", e); }
            return dNow + Math.Max(0, rem);
        }

        private void Evaluate(Mission m, float now)
        {
            if (!_engDown && _engines > 0)
            {
                bool any = false;
                foreach (var x in _machines) { try { if (x != null && !x.IsDestroyed) { any = true; break; } } catch { } }
                if (!any) _engDown = true;
            }
            double shock = SiegeWalls.ShockFactor;
            double baseLim = _thr * _km;
            // 371b: chwila wstrzasu do logu (dotad 1196 -> 598 bez sladu)
            if (_genDown && !_genLogged) { _genLogged = true; Log.Info("Szturm (371b): wodz atakujacych padl (czas misji " + F(now, "0") + " s, straty " + (_prior + _cas) + ") - prog x" + F(shock, "0.00") + "."); }
            if (_engDown && !_engLogged) { _engLogged = true; Log.Info("Szturm (371b): tarany i wieze stracone (" + _engines + " z " + _engines + ", czas misji " + F(now, "0") + " s, straty " + (_prior + _cas) + ") - prog x" + F(shock, "0.00") + "."); }
            int A = _prior + _cas;
            double lim0 = baseLim * (_genDown ? shock : 1.0) * (_engDown ? shock : 1.0);
            // 378: limit czasu przed progiem i wymiana - po uplywie odwrot niezaleznie od strat (wyjatek: zdobywaja)
            float tl = SiegeWalls.TimeLimitSeconds, fought = _tPrior + (now - _startAt);
            if (tl > 0f && fought >= tl) { TimeUp(m, now, fought, A, lim0); return; }
            // tanie wyjscie: ponizej progu z wstrzasami (bramy tylko podnosza) - bez liczenia bram i obroncow
            if (A < lim0) { _limit = lim0; _state = 0; return; }
            int gates, down;
            CountGates(m, out gates, out down);
            bool gatesDown = gates > 0 && down >= gates;
            _limit = lim0 * (gatesDown ? SiegeWalls.GateFactor : 1.0);
            if (A < _limit) { _state = 0; return; }
            int dNow = DefendersActive(m);
            _dLeft = DefendersLeft(m, dNow);
            int D = _dPrior + _dcas;
            double allDef = Math.Max(1, _dTot + _dPrior);
            _share = _dLeft / allDef;
            _x = A / (double)Math.Max(1, D);
            _cap = A >= SiegeWalls.HardCap * baseLim;
            _r = _cap ? 1.0 : ExchangeLimit(gatesDown);
            // 1) zdobywaja - obroncow prawie nie ma: nigdy nie lamac (co sekunde od nowa, bez trwalej flagi)
            if (_share < SiegeWalls.TakenShare)
            {
                if (!_takenNoted) { _takenNoted = true; SiegeWalls.NoteMissionTaken(); }
                Hold(2, A, D, gates, down, now);
                return;
            }
            // 2) wymiana wylaczona - sam prog (z mnoznikiem bram)
            if (!SiegeWalls.ExchangeOn) { Break(m, now, gates, down, dNow); return; }
            // 3) wymiana w granicy - szturm trwa (R >= 1: kto traci mniej, niz zadaje, nie odstepuje nigdy, takze po pulapie)
            if (_x <= _r)
            {
                if (!_holdNoted) { _holdNoted = true; SiegeWalls.NoteMissionHoldEx(); }
                Hold(1, A, D, gates, down, now);
                return;
            }
            Break(m, now, gates, down, dNow);
        }

        /// <summary>371b: linia "szturm trwa" przy zmianie stanu i co 10% progu bazowego przyrostu strat.</summary>
        private void Hold(int state, int A, int D, int gates, int down, float now)
        {
            bool log = state != _state || A >= _nextLogA;
            _state = state;
            if (!log) return;
            _nextLogA = A + Math.Max(1.0, 0.1 * _thr * _km);
            Log.Info("Szturm (371b): prog strat osiagniety (" + A + " / " + F(_limit, "0") + "), szturm trwa - " + (state == 2 ? "ZDOBYWAJA (obroncow zostalo " + F(_share * 100.0, "0.0")
                     + "% < " + F(SiegeWalls.TakenShare * 100.0, "0") + "%)" : "wymiana " + F(_x, "0.00") + ":1 <= granica " + F(_r, "0.00") + ":1"
                     + (_cap ? " (pulap " + F(SiegeWalls.HardCap, "0.0") + " x prog - granica 1:1)" : ""))
                     + " | straty A " + A + " / O " + D + " | przewaga " + F(_n, "0.0") + "x | bramy rozbite " + down + "/" + gates + " | obroncow zostalo " + _dLeft
                     + " (" + F(_share * 100.0, "0.0") + "%) | czas misji " + F(now, "0") + " s.");
        }

        private void Break(Mission m, float now, int gates, int down, int dNow)
        {
            _broken = true;
            _breakAt = now;
            _a.MissionBreak = true;
            SiegeWounded373.NoteBreak(_me);   // 373: ranni atakujacy pod murami do lochu przy koncu bitwy
            _a.MisEngLost = _engDown;
            _a.Loss = (_prior + _cas) / (double)Math.Max(1, _km);
            try { var sp = m.GetMissionBehavior<IMissionAgentSpawnLogic>(); if (sp != null) sp.StopSpawner(BattleSideEnum.Attacker); }
            catch (Exception e) { Stumble("StopSpawner", e); }
            _queue.Clear(); _qi = 0;
            int att = 0;
            foreach (var a in m.Agents)
            {
                if (a == null || !a.IsActive() || !a.IsHuman || !a.IsAIControlled || !Attacker(a)) continue;
                _queue.Add(a); att++;
            }
            try { InformationManager.DisplayMessage(new InformationMessage(_byTime ? "The assault has dragged on too long - the enemy falls back from the walls." : "The assault breaks! The enemy falls back from the walls.", Color.FromUint(0xFF9ACD65u))); } catch { }
            Log.Info("Szturm (371): ZALAMANIE" + (_byTime ? " (378: limit czasu)" : "") + " | osada " + (_a.St != null ? _a.St.StringId : "-") + ", wodz " + _a.Leader + " | czas misji " + F(now, "0") + " s | straty oddzialu "
                     + (_prior + _cas) + " (ta misja " + _cas + ", wczesniej " + _prior + ") / prog " + F(_limit, "0") + " (" + F(_thr * 100.0, "0.0") + "% x K_m " + _km
                     + (_genDown ? ", wodz padl x" + F(SiegeWalls.ShockFactor, "0.00") : "") + (_engDown ? ", tarany i wieze stracone x" + F(SiegeWalls.ShockFactor, "0.00") : "")
                     + (gates > 0 && down >= gates ? ", bramy rozbite x" + F(SiegeWalls.GateFactor, "0.00") : "") + ") | atakujacych w polu " + att + ", obroncow " + dNow + " z " + _def0
                     + " | bramy rozbite " + down + "/" + gates + " | 371b: wymiana " + (SiegeWalls.ExchangeOn ? F(_x, "0.00") + (_byTime ? ":1 (378: limit czasu - wymiana nie sprawdzana; straty A " : ":1 > granica " + F(_r, "0.00") + ":1 (straty A ") + (_prior + _cas)
                     + " / O " + (_dPrior + _dcas) + ", przewaga " + F(_n, "0.0") + "x" + (_byTime ? "" : ", pulap " + (_cap ? "tak" : "nie")) + ")" : "wylaczona") + ", obroncow zostalo " + _dLeft + " (" + F(_share * 100.0, "0.0") + "%).");
        }

        private void Drive(Mission m, float now)
        {
            // kolejka odwrotu: najwyzej PerTick ludzi na klatke (pierwsze wyslanie przy tysiacach ludzi)
            int budget = PerTick;
            while (_qi < _queue.Count && budget-- > 0) Send(m, _queue[_qi++]);
            if (_qi >= _queue.Count) { _queue.Clear(); _qi = 0; }
            if (now >= _nextResend)
            {
                _nextResend = now + ResendEvery;
                if (_queue.Count == 0)
                    foreach (var a in m.Agents)
                    {
                        if (a == null || !a.IsActive() || !a.IsHuman || !a.IsAIControlled || !Attacker(a)) continue;
                        if (a.IsRunningAway && !a.IsUsingGameObject) continue;            // uciekajacy, ale znow przypiety do drabiny / wiezy przez AI szyku - zdjac jeszcze raz
                        _queue.Add(a);
                    }
            }
            if (!_fuse && !m.MissionEnded && now - _breakAt >= SiegeWalls.BreakEndSeconds) _fuse = true;   // bezpiecznik tylko, gdy misja jeszcze trwa (MissionEnded przy nastepnym sprawdzeniu konca)
        }

        private void Send(Mission m, Agent a)
        {
            try
            {
                if (a == null || !a.IsActive() || !a.IsHuman || !a.IsAIControlled || !Attacker(a)) return;
                if (!MissionTeams.Valid(a.Team)) return;
                if (a.IsUsingGameObject) a.StopUsingGameObject(false, Agent.StopUsingGameObjectFlags.None);
                var wp = m.GetClosestFleePositionForAgent(a);
                if (!wp.IsValid) return;
                a.Retreat(wp);
                if (_sentTo.Add(a)) _sent++; else _resent++;
            }
            catch (Exception e) { Stumble("Send", e); }
        }

        public override bool MissionEnded(ref MissionResult missionResult)
        {
            if (!_fuse || _ended) return false;
            try
            {
                var m = Mission;
                if (m == null || m.MissionEnded) return false;
                // ten sam wynik, co BattleEndLogic przy odwrocie wroga (CreateSuccessful(enemyRetreated: true) gracza-obroncy)
                missionResult = new MissionResult(BattleState.DefenderVictory, true, false, true);
                Log.Info("Szturm (371): bezpiecznik - " + F(SiegeWalls.BreakEndSeconds, "0") + " s po zalamaniu misja trwala; koniec z odwrotem wroga (EnemyRetreated).");
                return true;
            }
            catch (Exception e) { Stumble("MissionEnded", e); return false; }
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_active && _started && _a != null)
                {
                    float endAt = _endAt >= 0f ? _endAt : (Mission != null ? Mission.CurrentTime : _startAt);
                    _a.MisTime = _tPrior + Math.Max(0f, (_broken ? _breakAt : endAt) - _startAt);   // 378: czas walki do nastepnej misji tej bitwy (do zalamania, bez schodzenia z murow)
                    _a.MisCas = _prior + _cas;
                    _a.MisDefCas = _dPrior + _dcas;                             // 371b: straty obrony do nastepnej misji tej bitwy
                    if (_broken) SiegeWalls.NoteMissionBreak(_fuse, _late);
                    var r = Mission != null ? Mission.MissionResult : null;
                    Log.Info("Szturm (371): koniec misji | osada " + (_a.St != null ? _a.St.StringId : "-") + " | zalamanie " + (_broken ? "tak (po " + F((Mission != null ? Mission.CurrentTime : 0f) - _breakAt, "0") + " s)" : "nie")
                             + " | straty oddzialu " + (_prior + _cas) + " / prog " + F(_limit, "0") + " | 371b: straty obrony " + (_dPrior + _dcas) + ", wymiana "
                             + F((_prior + _cas) / (double)Math.Max(1, _dPrior + _dcas), "0.00") + ":1 | odwrot wyslany " + _sent + ", ponowiony " + _resent + ", wystawieni po zalamaniu " + _late
                             + " | bezpiecznik " + (_fuse ? "tak" : "nie") + " | " + SiegeHold372.MissionSummary(Mission) + " | wynik misji " + (r == null ? "-" : r.BattleState + (r.EnemyRetreated ? " (wrog odstapil)" : ""))
                             + " | 378: czas walki " + F(_a.MisTime, "0") + " s" + (_byTime ? " (odwrot z limitu)" : "") + " | potkniecia " + _stumbles + ". Przerwa - linia 'Szturm (371): przerwa' przy koncu bitwy na mapie.");
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _queue.Clear(); _sentTo.Clear(); _machines.Clear();
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            SiegeWalls.NoteMissionStumble(where, e);
        }
    }
}
