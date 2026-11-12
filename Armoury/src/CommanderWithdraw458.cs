using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 458 (pakiet 8 krok 8.2 audytu AI bitwy, docs/audyty-2026-11-11/AUDYT-AI-BITWA.md; decyzja Jeffa nr 9: "wodz uchodzi z tym, co zeszlo w szyku - tylko jesli
    /// mu sie udalo"). SAM POMIAR "bylby", BEZ REGULY I BEZ KLUCZA (wzor 456): nikomu nic nie rozkazujemy, nic nie zmieniamy w misji ani na mapie.
    ///
    /// DLACZEGO NIE REGULA (sprawdzone w dekompilacji 1.4.8 i RBM, nie zalozenie) - trzy przeszkody, kazda na granicy konca misji / MapEvent:
    ///  1. RBM gasi kazdy odwrot na rozkaz: postfiks RBMAI OnTickPatch na HumanAIComponent.OnTick (decout/full/rbmai.cs 2783-2828) w KAZDYM ticku wola
    ///     CommonAIComponent.StopRetreating u kazdego czlowieka z morale > 0, ktory od 10 s nie dostal ciosu wrecz. Rozkaz Retreat szyku (MovementOrder.RetreatAux
    ///     -> agent.Retreat -> CommonAIComponent.Retreat, mb 34517) jest wiec zdejmowany w nastepnej klatce. Z pola schodzi tylko ten, kto ma morale ~0 (panika) -
    ///     a takiego gra przy usunieciu wypisuje ze skladu (SandBox BattleAgentLogic.OnAgentRemoved: morale < 0.01 -> SetRouted(isOrderRetreat: false) ->
    ///     MapEventParty.OnTroopRouted -> MemberRoster -1, decout/v/all_sb/.../BattleAgentLogic.cs 118-122, cs 113451). Jedyna droga "zejscia w szyku" z
    ///     zostaniem w skladzie (morale >= 0.01 -> SetRouted(true), czlowiek zostaje w partii) to natywny Agent.Retreat(pozycja) jak odwrot rannego 143 - czyli
    ///     nowy mechanizm prowadzenia kilkuset ludzi do granicy pola, z podloga morale i znacznikiem dla SurvivalMode / BannerCompanies / RallyRouted359.
    ///  2. Koniec misji ma dwie drogi o roznym skutku na mapie: (a) wszyscy wrogowie uciekaja dluzej niz interwal posilkow + 3 s (BattleEndLogic, mb 79540-79620)
    ///     -> MissionResult z EnemyRetreated -> PlayerEncounter nie kontynuuje bitwy (cs 99459) -> pobici sa pobici: CaptureDefeatedPartyMembers bierze do niewoli
    ///     WSZYSTKICH zdrowych i rannych ze skladu (cs 112704), takze tych, ktorzy zeszli w szyku; (b) strona wyczerpana (nikt nie zostal na polu) ->
    ///     EnemyRetreated = false, a w skladzie sa zdrowi -> bitwa trwa dalej (ContinueBattle, cs 98850) - gracz moze uderzyc jeszcze raz. Ktora droga zajdzie,
    ///     zalezy od tego, czy tylna straz zginie przed, czy po zejsciu reszty.
    ///  3. Mapa: gra ma gotowy stan "strona sie wycofala" - MapEvent.RetreatingSide (prywatny setter; cs 110907): CaptureDefeatedPartyMembers wychodzi od razu,
    ///     AiMilitaryBehavior.OnMapEventEnded odsuwa partie poza promien spotkania (cs 220461), H3 i 312 / DoomedPartyMen juz go obsluguja (LosersFlee:1123,
    ///     BattleLosses312:190, DoomedPartyMen:112). Ale w bitwie gracza gra go nigdy nie ustawia (tylko symulacja, CheckSideRunAway cs 112075), RetreatingSide
    ///     ratuje CALY sklad (takze rannych z pola i tylna straz, ktora przezyla) - nie "tylko tych, co zeszli" - i EndedByRetreat wylacza lup DTE dla zwyciezcow
    ///     (DTE EveryoneCampaignBehavior.OnMapEventEnded, dte_ecb.cs 449). Do tego wynik misji trzeba by przestawic na EnemyRetreated przy drodze (b).
    /// Kazdy z tych punktow zmienia zakonczenie bitwy gracza bez mozliwosci sprawdzenia w autotescie (autotest bitwy armii nie rozgrywa odwrotu). Zgodnie z
    /// poleceniem kroku ("jesli ryzyko konca misji / MapEvent jest zbyt duze - tylko czesc misji + pomiar 'bylby'") - tu tylko pomiar, ktory odpowiada na
    /// pytania potrzebne do reguly: KIEDY wodz AI "widzi przegrana", ILU ludzi mialby wyprowadzic, KTO bylby tylna straza, ILE czasu mial wodz do granicy pola,
    /// CO naprawde stalo sie z tymi ludzmi (zabici / nieprzytomni / zeszli z morale >= 0.01 - zostaja w partii / uciekli w panice - wypisani) i CO zrobi gra
    /// na mapie (niewola calego skladu albo dalsza bitwa).
    ///
    /// POMIAR (glowny watek, OnMissionTick co 1 s; bitwa polowa kampanii, nie morska, nie oblezenie, nie kryjowka; druzyny AI z TeamAI, bez druzyny gracza;
    /// pod kluczem pomiarow 437 BattleAiMeasurements):
    ///  - zdolni = czynni ludzie druzyny, nie IsRunningAway i nie PANIKA (SurvivalMode.InPanic); szczyt = najwiekszy stan zdolnych; kontakt = szyk 0-7 z >= 5
    ///    wrogami (czynni, nie uciekajacy) w 30 m od mediany.
    ///  - "WIDZI PRZEGRANA" (bylby odwrot), po kontakcie, przez 2 kolejne skany: zdolni <= prog x szczyt i zdolni wroga >= 1.5 x zdolni; prog wedlug profilu
    ///    wodza 440 (Doctrine.P457Prof: OSTROZNY 0.6, ZWYKLY 0.5, PORYWCZY 0.4); szczyt >= 10. Pierwsze chwile progow 0.6 / 0.5 / 0.4 zapisywane zawsze
    ///    (dane do wyboru progu).
    ///  - w chwili "widzi przegrana": wodz (Team.GeneralAgent, a gdy nieczynny - bohater Team.Leader; brak = "brak wodza na polu" - reguly by nie bylo),
    ///    konny / pieszy, odleglosc do granicy pola (Mission.GetClosestBoundaryPosition) i czas dojscia (pieszo 3 m/s, konno 6 m/s); tylna straz = szyk 0-7
    ///    z najwieksza liczba wrogow w 30 m (>= 5), nie szyk wodza; wyprowadzani = zdolni spoza tylnej strazy (z wodzem), konni, srednia odleglosc do granicy;
    ///    jazda wroga (zdolni konni) w 100 m od srodka druzyny.
    ///  - los wyprowadzanych i tylnej strazy po tej chwili (OnAgentRemoved): zabici, nieprzytomni, zeszli z pola z morale >= 0.01 (gra zostawia ich w partii),
    ///    uciekli z morale < 0.01 (gra wypisuje ich ze skladu), na polu na koniec; los wodza.
    ///  - koniec: wynik misji (OnMissionResultReady: zwyciestwo gracza, EnemyRetreated), sklad stron MapEvent (zdrowi / ranni), co zrobi gra (niewola calego
    ///    skladu / bitwa trwa dalej) i liczba druzyn AI strony (RetreatingSide dziala na cala strone); latki na HumanAIComponent.OnTick (wlasciciele postfiksow -
    ///    dowod przeszkody 1 w logu).
    /// Linie: "Odwrot wodza (458): ..." w chwili "widzi przegrana" (raz na druzyne) i "Odwrot wodza (458) koniec: ..." na koniec misji.
    /// Wyjatek per druzyne / czlowieka = potkniecie (pierwsze 3 do logu), bez globalnego wylacznika. Zadnych pol MovementOrder.
    ///
    /// 458-r REGULA (decyzja Jeffa 12.11: "1. Rob B 2. Rob B - ... wodz ktory sie wycofa odszedl jak najszybciej a wygrany pewnie grabi rannych 3. odchodza z wodzem,
    /// bitwa przegrana wycofujemy sie i tylna straz walczy do konca"; klucz Commander Withdraws Under Rearguard, TAK; NIE = co do bitu jak przed - sam pomiar):
    /// wyzwalacz = ta sama chwila "widzi przegrana" (progi profilu 440). Tylko druzyna AI wroga gracza (nigdy strona gracza), jedyna druzyna AI swojej strony,
    /// MapEvent gracza = bitwa polowa na ladzie, wodz na polu. Przeszkody 1-3 z opisu wyzej rozwiazane tak:
    ///  1. ODWROT W SZEREGACH bez latki na RBM: natywny Agent.Retreat(pozycja) (SetRetreatMode) komponentu CommonAIComponent nie dotyka, a RBM OnTickPatch wola
    ///     CommonAIComponent.StopRetreating, ktore dziala tylko przy IsRetreating komponentu (wzor 143 RoutHold.Withdraw, 371, 381 - sprawdzone w grze). Kazdy
    ///     wyprowadzany (zdolni spoza tylnej strazy + wodz) idzie do JEDNEGO punktu ucieczki swojego szyku (Mission.GetClosestFleePositionForFormation - punkty
    ///     ucieczki strony z omijaniem scigajacych, jak gra; bez szyku - GetClosestFleePositionForAgent); gra sama zdejmuje go z pola przy punkcie (StartFadingOut).
    ///     Co 0.5 s: morale >= 15 (skala gry 0-100; ponizej 0.01 gra sama wola Panic i wypisuje ze skladu), odwrot ponowiony, gdy zgasl (np. StopRetreating RBM po
    ///     szarzy konnej). PANIKA (SurvivalMode.InPanic: komponent / RoutHold) - nie ruszamy (cudza droga), liczone. Natywny odwrot = ROZSADEK w SurvivalMode;
    ///     457 / 459 / 448 / 307e / 335 pomijaja IsRunningAway.
    ///  2. TYLNA STRAZ = szyk z najwieksza liczba wrogow w 30 m (nie szyk wodza): bez rozkazu odwrotu, bez zmian morale - walczy pod swoim AI do konca.
    ///  3. POSILKI: IMissionAgentSpawnLogic.StopSpawner(strona) (wzor 371) - niewprowadzeni zostaja w skladzie. Strona nie jest wtedy "wyczerpana"
    ///     (IsSideDepleted wymaga RemainingSpawnNumber == 0), wiec koniec misji idzie droga "wrog uciekl" (BattleEndLogic: wszyscy IsRunningAway przez interwal + 3 s).
    ///     Gdy jednak skonczy sie "wyczerpaniem" (EnemyRetreated = false - gra dalej toczylaby bitwe ze zbieglymi, ContinueBattle), w OnMissionResultReady (po
    ///     CampaignMissionComponent) PlayerEncounter.CampaignBattleResult = GetResult(stan, enemyRetreated: true) - bitwa rozstrzygnieta, strona przegrala.
    ///  4. MAPA (CommanderWithdraw458.Map.cs): od startu misji liczymy kazdego wystawionego czlowieka strony wroga (partia, postac) i jego los; przy udanym zejsciu
    ///     wodza (zszedl z pola z morale >= 0.01 - stojacy na koniec NIE) i wygranej gracza (EnemyRetreated albo wymuszone) - Pending do pojmania tej bitwy:
    ///     w partii zostaja zbiegli z wodzem (naprawde zeszli z pola) i posilki niewprowadzone; wszyscy ranni (z pola i sprzed bitwy) i reszta do zwyciezcy
    ///     jak w grze; partia odchodzi od zwyciezcy jak RetreatingSide w grze.
    /// 458-p (recenzje A i B): bez tylnej strazy / wodz nie-bohater - "bylby"; ranni w odwrocie 143 prowadzi 143; zwolnienie modulow przed odwrotem jak 143.
    /// Linie: "Odwrot wodza (458): ... ODWROT NA ROZKAZ" (decyzja), "Odwrot wodza (458) koniec: ..." (z rozliczeniem misji i kontrola sum), "Odwrot wodza (458) mapa:".
    /// </summary>
    internal sealed class CommanderWithdraw458 : MissionLogic
    {
        private const string P = "Odwrot wodza (458): ";
        private const float Every = 1f, StartDelay = 3f, NoDeployStart = 10f, ContactR = 30f, CavR = 100f, FoeRatio = 1.5f, FootSpeed = 3f, HorseSpeed = 6f;
        private const int ContactMin = 5, PeakMin = 10, Streak = 2, ErrMax = 3;
        private const int FKilled = 0, FUncon = 1, FLeft = 2, FFled = 3, Fates = 4;
        private static readonly float[] Thr = { 0.6f, 0.5f, 0.4f };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] ProfName = { "OSTROZNY", "ZWYKLY", "PORYWCZY" };

        private sealed class St
        {
            public Team T;
            public sbyte Prof = 1;
            public bool ProfKnown, EnemyOfPlayer;
            public int Peak, Own, Foe, Streak;
            public float ContactAt = -1f;
            public readonly float[] First = { -1f, -1f, -1f };
            // chwila "widzi przegrana"
            public bool Trig;
            public float TrigAt = -1f;
            public int TrigOwn, TrigFoe, TrigPeak;
            public string Cmd = "-", CmdFate = "-", Rear = "-";
            public Agent CmdA;
            public bool CmdMounted;
            public float CmdEdge = -1f;
            public Formation RearF;
            public int RearFoes, WMounted, FoeCav;
            public float WEdgeSum;
            public int WEdgeN;
            public readonly HashSet<Agent> W = new HashSet<Agent>();
            public readonly HashSet<Agent> R = new HashSet<Agent>();
            public readonly int[] WFate = new int[Fates];
            public readonly int[] RFate = new int[Fates];
            // 458-r
            public bool Rule, CmdKept, Forced, SpawnStopped;
            public string RuleWhy = "-", ResultNote = "-";
            public MapEvent Me;
            public PartyBase CmdParty;
            public Hero CmdHero;
            public readonly List<Agent> Wd = new List<Agent>();
            public readonly Dictionary<Formation, WorldPosition> Exit = new Dictionary<Formation, WorldPosition>();
            public readonly Dictionary<Formation, float> ExitAt = new Dictionary<Formation, float>();   // 458-p: chwila wyliczenia punktu (ponowienie po ExitStale liczy na nowo)
            public int Sent, Resent, Pins, PanicOut, NoExit, ByRh;
        }

        private bool _known, _field, _started, _ended;
        private float _startAt = -1f, _next;
        private int _stumbles, _errs;
        private MissionResult _result;
        private bool _resultSeen;
        private bool _resultLate;   // 458-q: wynik z Mission.MissionResult przy OnEndMission (OnMissionResultReady nie doszlo)
        private readonly List<St> _teams = new List<St>();
        private readonly MBList<Agent> _bufE = new MBList<Agent>();

        private static string _rbmTick;   // wlasciciele postfiksow HumanAIComponent.OnTick (raz na proces)

        // ------------------------------------------------------------ 458-r regula
        private const float MaintainEvery = 0.5f, MoraleFloor = 15f, Routed = 0.01f, ExitStale = 10f;
        private readonly bool _measure, _ruleKey;
        private readonly object _gate = new object();
        private sealed class Org { public PartyBase P; public CharacterObject C; }
        private readonly Dictionary<Agent, Org> _trk = new Dictionary<Agent, Org>();
        private readonly Dictionary<PartyBase, Dictionary<CharacterObject, int[]>> _cnt = new Dictionary<PartyBase, Dictionary<CharacterObject, int[]>>();
        private readonly HashSet<Hero> _spawnedHeroes = new HashSet<Hero>(), _keptHeroes = new HashSet<Hero>();
        private readonly HashSet<Agent> _esc = new HashSet<Agent>(), _panicOut = new HashSet<Agent>();
        private int _heroRm, _heroWs, _heroK;
        private int _buildMiss;   // 458-p: wystawiony czlowiek wroga bez licznika (brak partii / postaci / potkniecie) - mapa wtedy jak w grze
        private float _nextMaintain;
        // 458-u-p (recenzja U1): wlasny kon jezdzca-szeregowego ze spawnu i los tego konia (stan + chwila zejscia) - zwrot konia zbieglego z wodzem wedlug
        // losu WLASNEGO wierzchowca, nie MountAgent w chwili usuniecia (kolejnosc odpiecia konia ustala silnik; cudzy kon dosiadziety = nie jego sztuka)
        private readonly Dictionary<Agent, Agent> _ownMount = new Dictionary<Agent, Agent>();
        private readonly Dictionary<Agent, KeyValuePair<AgentState, float>> _mountFate = new Dictionary<Agent, KeyValuePair<AgentState, float>>();
        private const float MountWith = 1f;   // kon zdjety z pola osobno najwyzej tyle sekund od jezdzca = zszedl razem z nim (jedno FadeOut)

        public CommanderWithdraw458()
        {
            var c = Settings.Current;
            _measure = c != null && c.BattleAiMeasurements;
            _ruleKey = c != null && c.CommanderWithdrawsUnderRearguard;
            try { CommanderWithdraw458Map.Clear(); } catch { }   // 458-p: takze przy kluczu NIE - stare oczekiwanie nie przezyje nowej misji
        }

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private bool Field(Mission m)
        {
            if (!_known)
            {
                try { _field = m != null && m.IsFieldBattle && !m.IsNavalBattle && !m.IsSiegeBattle && !HideoutAlarm.IsHideout(m); } catch { _field = false; }
                _known = true;
            }
            return _field;
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
                    if (!Field(m)) { _ended = true; return; }
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }
                    if (now < _startAt) return;
                    _started = true;
                    _next = now;
                }
                if (_ruleKey && now >= _nextMaintain) { _nextMaintain = now + MaintainEvery; Maintain(m); }
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                lock (_gate) Scan(m, now);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private St Of(Team t)
        {
            for (int i = 0; i < _teams.Count; i++) if (ReferenceEquals(_teams[i].T, t)) return _teams[i];
            var s = new St { T = t };
            try { s.EnemyOfPlayer = MissionTeams.Enemies(t, Mission.PlayerTeam); } catch { s.EnemyOfPlayer = false; }
            _teams.Add(s);
            return s;
        }

        /// <summary>Czlowiek zdolny do walki: czynny, nie ucieka, nie PANIKA.</summary>
        private static bool Able(Agent a)
        {
            return a != null && a.IsActive() && a.IsHuman && !a.IsRunningAway && !SurvivalMode.InPanic(a);
        }

        private static int AbleOf(Team t)
        {
            int n = 0;
            var l = t.ActiveAgents;
            for (int i = 0; l != null && i < l.Count; i++) if (Able(l[i])) n++;
            return n;
        }

        private int FoesNear(Mission m, Team t, Vec2 p, float r)
        {
            m.GetNearbyEnemyAgents(p, r, t, _bufE);
            int n = 0;
            for (int i = 0; i < _bufE.Count; i++)
            {
                var a = _bufE[i];
                if (a != null && a.IsActive() && a.IsHuman && !a.IsRunningAway) n++;
            }
            return n;
        }

        private void Scan(Mission m, float now)
        {
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t) || t.IsPlayerTeam || !t.HasTeamAi) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                var s = Of(t);
                // 458-p: po "widzi przegrana" dalej liczymy zdolnych, szczyt i progi (dane do wyboru progu) - pomijamy tylko ponowne Trigger (w Watch)
                try { Watch(m, s, now); }
                catch (Exception e) { Stumble("Druzyna", e); }
            }
        }

        private void Watch(Mission m, St s, float now)
        {
            var t = s.T;
            s.Own = AbleOf(t);
            if (s.Own > s.Peak) s.Peak = s.Own;
            int foe = 0;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var o = teams[i];
                if (MissionTeams.Enemies(t, o)) foe += AbleOf(o);
            }
            s.Foe = foe;
            if (s.ContactAt < 0f)
            {
                var fl = t.FormationsIncludingEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    if (FoesNear(m, t, f.CachedMedianPosition.AsVec2, ContactR) >= ContactMin) { s.ContactAt = now; break; }
                }
                if (s.ContactAt < 0f) return;
            }
            if (s.Peak < PeakMin) return;
            bool outnumbered = s.Foe >= FoeRatio * Math.Max(s.Own, 1);
            for (int k = 0; k < Thr.Length; k++)
                if (s.First[k] < 0f && outnumbered && s.Own <= Thr[k] * s.Peak) s.First[k] = now;
            if (s.Trig) return;   // 458-p: chwila "widzi przegrana" raz na druzyne
            if (!s.ProfKnown)
            {
                bool known;
                s.Prof = Doctrine.P457Prof(t, out known);
                s.ProfKnown = known;
            }
            float thr = Thr[Math.Max(0, Math.Min(2, (int)s.Prof))];
            if (outnumbered && s.Own <= thr * s.Peak) s.Streak++;
            else s.Streak = 0;
            if (s.Streak < Streak) return;
            Trigger(m, s, now);
        }

        // ------------------------------------------------------------ chwila "widzi przegrana"

        private static Agent Commander(Team t)
        {
            Agent g = null;
            try { g = t.GeneralAgent; } catch { g = null; }
            if (g != null && g.IsActive() && g.IsHuman) return g;
            Agent l = null;
            try { l = t.Leader; } catch { l = null; }
            return l != null && l.IsActive() && l.IsHero && !l.IsMainAgent ? l : null;
        }

        private static float EdgeDist(Mission m, Vec2 p)
        {
            try
            {
                if (!m.IsPositionInsideBoundaries(p)) return 0f;
                Vec2 b = m.GetClosestBoundaryPosition(p);
                return b.IsValid ? b.Distance(p) : -1f;
            }
            catch { return -1f; }
        }

        private void Trigger(Mission m, St s, float now)
        {
            var t = s.T;
            s.Trig = true;
            s.TrigAt = now;
            s.TrigOwn = s.Own;
            s.TrigFoe = s.Foe;
            s.TrigPeak = s.Peak;
            var cmd = Commander(t);
            s.CmdA = cmd;
            if (cmd != null)
            {
                try { s.Cmd = (cmd.Name ?? "?") + (cmd.IsHero ? "" : " (nie bohater)") + (ReferenceEquals(cmd, t.GeneralAgent) ? " [GeneralAgent]" : " [Leader]"); } catch { s.Cmd = "?"; }
                s.CmdMounted = cmd.HasMount;
                s.CmdEdge = EdgeDist(m, cmd.Position.AsVec2);
                s.CmdFate = "na polu";
            }
            else s.Cmd = "brak wodza na polu";

            // tylna straz: szyk 0-7 z najwieksza liczba wrogow w 30 m (>= 5), nie szyk wodza
            Formation cmdF = null;
            try { cmdF = cmd != null ? cmd.Formation : null; } catch { cmdF = null; }
            var fl = t.FormationsIncludingEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                try
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0 || ReferenceEquals(f, cmdF)) continue;
                    if ((int)f.FormationIndex >= 8) continue;   // 458-p: szyki 8-9 (odwod 457) nie sa tylna straza - zgodnie z opisem 0-7
                    int n = FoesNear(m, t, f.CachedMedianPosition.AsVec2, ContactR);
                    if (n >= ContactMin && n > s.RearFoes) { s.RearFoes = n; s.RearF = f; }
                }
                catch (Exception e) { Stumble("Straz", e); }
            }
            Vec2 c = Vec2.Zero;
            int cn = 0;
            var l = t.ActiveAgents;
            for (int i = 0; l != null && i < l.Count; i++)
            {
                var a = l[i];
                try
                {
                    if (!Able(a)) continue;
                    if (s.RearF != null && ReferenceEquals(a.Formation, s.RearF)) { s.R.Add(a); continue; }
                    s.W.Add(a);
                    if (a.HasMount) s.WMounted++;
                    Vec2 p = a.Position.AsVec2;
                    c += p;
                    cn++;
                    float d = EdgeDist(m, p);
                    if (d >= 0f) { s.WEdgeSum += d; s.WEdgeN++; }
                }
                catch (Exception e) { Stumble("Czlowiek", e); }
            }
            if (cmd != null && !s.W.Contains(cmd) && !s.R.Contains(cmd)) s.W.Add(cmd);
            if (s.RearF != null)
            {
                string kind = "?";
                try { var q = s.RearF.QuerySystem; kind = q == null ? "?" : q.IsCavalryFormationReadOnly ? "jazda" : q.IsRangedFormationReadOnly ? "strzelcy" : q.IsInfantryFormationReadOnly ? "piechota" : "inny"; } catch { kind = "?"; }
                s.Rear = "F" + (int)s.RearF.FormationIndex + " " + kind + " " + s.R.Count + " zdolnych (wrogow w 30 m " + s.RearFoes + ")";
            }
            // jazda wroga w 100 m od srodka wyprowadzanych
            if (cn > 0)
            {
                c = c * (1f / cn);
                try
                {
                    m.GetNearbyEnemyAgents(c, CavR, t, _bufE);
                    for (int i = 0; i < _bufE.Count; i++) { var a = _bufE[i]; if (Able(a) && a.HasMount) s.FoeCav++; }
                }
                catch (Exception e) { Stumble("Jazda", e); }
            }
            float eta = s.CmdEdge >= 0f ? s.CmdEdge / (s.CmdMounted ? HorseSpeed : FootSpeed) : -1f;
            // 458-r: regula albo powod, dla ktorego tylko "bylby"
            string why = _ruleKey ? RuleWhy(m, s, cmd) : "klucz Commander Withdraws Under Rearguard NIE";
            if (why == null) StartRule(m, s, cmd);
            else s.RuleWhy = why;
            if (!_measure && !s.Rule) return;
            Log.Info(P + "t=" + F0(now) + "s | T" + t.TeamIndex + " " + t.Side + (s.EnemyOfPlayer ? " (wrog gracza)" : " (sojusznik gracza)")
                     + (s.Rule ? " | ODWROT NA ROZKAZ (regula 458): wyslano " + s.Sent + " do punktow ucieczki szykow (" + s.Exit.Count + " szykow), bez punktu " + s.NoExit
                                 + ", posilki strony " + (s.SpawnStopped ? "zatrzymane (StopSpawner)" : "NIE zatrzymane") + ", partia wodza " + PName(s.CmdParty)
                               : " | bylby odwrot (nic nie rozkazano; " + s.RuleWhy + ")")
                     + " | wodz " + ProfName[Math.Max(0, Math.Min(2, (int)s.Prof))] + (s.ProfKnown ? "" : " (profil nieznany)") + " prog " + Thr[Math.Max(0, Math.Min(2, (int)s.Prof))].ToString("0.0", Inv)
                     + " | zdolni " + s.TrigOwn + " z szczytu " + s.TrigPeak + " (" + Pct(s.TrigOwn, s.TrigPeak) + "), wroga " + s.TrigFoe + " | od kontaktu " + F0(now - s.ContactAt) + " s"
                     + " | wodz: " + s.Cmd + (cmd != null ? ", " + (s.CmdMounted ? "konno" : "pieszo") + ", do granicy pola " + (s.CmdEdge >= 0f ? F0(s.CmdEdge) + " m (ok. " + F0(eta) + " s)" : "?") : "")
                     + " | tylna straz: " + s.Rear + " | wyprowadzani " + s.W.Count + " (konni " + s.WMounted + ", srednio do granicy " + (s.WEdgeN > 0 ? F0(s.WEdgeSum / s.WEdgeN) + " m" : "?") + ")"
                     + " | jazda wroga w 100 m " + s.FoeCav + ".");
        }

        // ------------------------------------------------------------ 458-r: decyzja i prowadzenie odwrotu

        /// <summary>null = regula dziala dla tej druzyny; inaczej powod "bylby".</summary>
        private string RuleWhy(Mission m, St s, Agent cmd)
        {
            if (!s.EnemyOfPlayer) return "strona gracza - regula tylko dla wroga gracza";
            if (cmd == null) return "brak wodza na polu";
            if (!cmd.IsHero) return "wodz nie jest bohaterem (Team.GeneralAgent szeregowy)";   // 458-p (recenzja B4)
            if (s.RearF == null) return "brak tylnej strazy (zaden szyk 0-7 poza szykiem wodza nie ma >= " + ContactMin + " wrogow w 30 m)";   // 458-p (recenzja A1): bez strazy nie ma kogo zostawic
            int aiTeams = 0;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var o = teams[i];
                if (!MissionTeams.Valid(o) || o.Side != s.T.Side) continue;
                if (o.IsPlayerTeam || o.IsPlayerAlly) return "gracz po tej stronie";
                if (o.HasTeamAi) aiTeams++;
            }
            if (aiTeams != 1) return "druzyn AI strony " + aiTeams + " (regula tylko przy jednej)";
            MapEvent me = null;
            try { me = PlayerEncounter.Battle; } catch { me = null; }
            if (me == null) return "brak MapEvent gracza";
            if (!me.IsFieldBattle || me.IsNavalMapEvent) return "MapEvent nie jest bitwa polowa na ladzie";
            if (me.GetMapEventSide(s.T.Side) == null) return "brak strony MapEvent";
            return null;
        }

        private void StartRule(Mission m, St s, Agent cmd)
        {
            var t = s.T;
            s.Rule = true;
            try { s.Me = PlayerEncounter.Battle; } catch { s.Me = null; }
            try { CommanderWithdraw458Map.GearStart(this, s.Me); } catch (Exception e) { Stumble("Sprzet start", e); }   // 458-u: zapis sprzetu zbieglych z wodzem
            try { s.CmdParty = cmd.Origin != null ? cmd.Origin.BattleCombatant as PartyBase : null; } catch { s.CmdParty = null; }
            try { var co = cmd.Character as CharacterObject; s.CmdHero = co != null ? co.HeroObject : null; } catch { s.CmdHero = null; }
            try
            {
                var sp = m.GetMissionBehavior<IMissionAgentSpawnLogic>();
                if (sp != null) { sp.StopSpawner(t.Side); s.SpawnStopped = true; }
            }
            catch (Exception e) { Stumble("StopSpawner", e); }
            foreach (var a in s.W)
            {
                try
                {
                    if (a == null || !a.IsActive()) continue;
                    _esc.Add(a);
                    if (Rh(a)) { s.ByRh++; continue; }   // 458-p (recenzja A2): ranny w odwrocie 143 - prowadzi go 143, my tylko liczymy jego zejscie
                    s.Wd.Add(a);
                    Send(m, s, a, false);
                }
                catch (Exception e) { Stumble("Wyslij", e); }
            }
        }

        /// <summary>Natywny odwrot do punktu ucieczki szyku (jeden punkt na szyk - idzie w szeregach), podloga morale.</summary>
        private void Send(Mission m, St s, Agent a, bool again)
        {
            if (!again)
            {
                // 458-p (recenzja A3): zwolnienie przez moduly trzymajace agenta - ta sama kolejnosc co odwrot rannego 143 (RoutHold.Withdraw); kazdy osobno
                try { HeroCaution.BeforeWithdraw(a); } catch (Exception e) { Stumble("HeroCaution.BeforeWithdraw", e); }
                try { Doctrine.H447BeforeWithdraw(a); } catch (Exception e) { Stumble("Doctrine.H447BeforeWithdraw", e); }
                try { HorsePursuit.BeforeWithdraw(a); } catch (Exception e) { Stumble("HorsePursuit.BeforeWithdraw", e); }
                try { BannerCompanies.BeforeWithdraw(a); } catch (Exception e) { Stumble("BannerCompanies.BeforeWithdraw", e); }
            }
            if (a.IsUsingGameObject) a.StopUsingGameObject(false, Agent.StopUsingGameObjectFlags.None);
            WorldPosition wp = WorldPosition.Invalid;
            var f = a.Formation;
            if (f != null && f.CountOfUnits > 0)
            {
                // 458-p (recenzja A5): punkt szyku liczony na nowo, gdy starszy niz ExitStale s (scigajacy sie przesuneli)
                float now = m.CurrentTime, at;
                if (!s.Exit.TryGetValue(f, out wp) || !s.ExitAt.TryGetValue(f, out at) || now - at > ExitStale)
                {
                    wp = m.GetClosestFleePositionForFormation(f);
                    s.Exit[f] = wp;
                    s.ExitAt[f] = now;
                }
            }
            if (!wp.IsValid) wp = m.GetClosestFleePositionForAgent(a);
            if (!wp.IsValid) { s.NoExit++; return; }
            if (a.GetMorale() < MoraleFloor) a.SetMorale(MoraleFloor);
            a.Retreat(wp);
            if (again) s.Resent++; else s.Sent++;
        }

        /// <summary>Co 0.5 s (glowny watek): wyprowadzani dalej ida (ponowienie zgaszonego natywnego odwrotu), morale nie spada do paniki gry; PANIKA - nie ruszamy.</summary>
        private void Maintain(Mission m)
        {
            lock (_gate)
            {
                for (int i = 0; i < _teams.Count; i++)
                {
                    var s = _teams[i];
                    if (!s.Rule) continue;
                    for (int j = s.Wd.Count - 1; j >= 0; j--)
                    {
                        var a = s.Wd[j];
                        try
                        {
                            if (a == null || !a.IsActive()) { if (a != null && _panicOut.Remove(a)) s.PanicOut++; s.Wd.RemoveAt(j); continue; }
                            // 458-p (recenzja A6): panika liczona dopiero przy zejsciu / koncu misji - ofiara szarzy RBM wraca pod nasze prowadzenie
                            if (SurvivalMode.InPanic(a)) { _panicOut.Add(a); continue; }
                            _panicOut.Remove(a);
                            if (Rh(a)) continue;   // 458-p (recenzja A2): 143 przejal rannego - nie przeciagamy liny
                            if (a.GetMorale() < MoraleFloor) { a.SetMorale(MoraleFloor); s.Pins++; }
                            if (!a.IsRetreating()) Send(m, s, a, true);
                        }
                        catch (Exception e) { Stumble("Prowadzenie", e); }
                    }
                }
            }
        }

        /// <summary>458-p: ranny w odwrocie 143 (RoutHold, w drodze albo na postoju) - wzor HorsePursuit.Withdrawing.</summary>
        private static bool Rh(Agent a)
        {
            var k = RoutHold.Live;
            return k != null && k.Withdrawing(a);
        }

        // ------------------------------------------------------------ 458-r: liczniki wystawionych (partia, postac) od startu misji

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (!_ruleKey || _ended || agent == null) return;
            try
            {
                if (!agent.IsHuman) return;
                var m = Mission;
                var t = agent.Team;
                if (m == null || !MissionTeams.Valid(t) || t.IsPlayerTeam || !MissionTeams.Enemies(t, m.PlayerTeam)) return;
                var pb = agent.Origin != null ? agent.Origin.BattleCombatant as PartyBase : null;
                var c = agent.Character as CharacterObject;
                if (pb == null || c == null) { lock (_gate) _buildMiss++; return; }
                lock (_gate)
                {
                    _trk[agent] = new Org { P = pb, C = c };
                    if (c.IsHero) { if (c.HeroObject != null) _spawnedHeroes.Add(c.HeroObject); }
                    else
                    {
                        Cnt(pb, c)[CommanderWithdraw458Map.CS]++;
                        var mount = agent.MountAgent;   // kon posadzony przed OnAgentBuild jezdzca (Mission.SpawnAgent, jak HorseFateMission)
                        if (mount != null) _ownMount[agent] = mount;
                    }
                }
            }
            catch (Exception e) { lock (_gate) _buildMiss++; Stumble("Wystawiony", e); }
        }

        /// <summary>458-u: wyprowadzany z wodzem (ten sam zbior co licznik "zbiegli z wodzem" w Track) - dla zapisu sprzetu w DTE OnAgentRemoved.</summary>
        internal bool GearEscapee(Agent a)
        {
            lock (_gate) return !_ended && a != null && _esc.Contains(a);
        }

        /// <summary>458-u-p: wlasny kon jezdzca ze spawnu (null - wystawiony pieszo / bez zapisu).</summary>
        internal Agent GearOwnMount(Agent rider)
        {
            Agent m;
            lock (_gate) return rider != null && _ownMount.TryGetValue(rider, out m) ? m : null;
        }

        /// <summary>458-u-p: wlasny kon zszedl z pola z jezdzcem - nie zabity i (jezdziec siedzial na nim przy usunieciu albo kon zdjety z pola zywy
        /// najwyzej MountWith s od jezdzca). Wolane przy OnEndMission (przed czyszczeniem), los konia z wlasnego zapisu (Agent.State po usunieciu - wskaznik silnika).
        /// Wynik: 1 zszedl na nim, 2 kon zdjety osobno w oknie, 0 nie (padl / zostal na polu / zszedl osobno pozniej).</summary>
        internal int GearMountLeft(Agent own, float tRider, bool mountedOwn)
        {
            if (own == null) return 0;
            KeyValuePair<AgentState, float> f;
            bool has;
            lock (_gate) has = _mountFate.TryGetValue(own, out f);
            if (has && f.Key == AgentState.Killed) return 0;
            if (mountedOwn) return 1;
            if (has && (f.Key == AgentState.Routed || f.Key == AgentState.Deleted) && Math.Abs(f.Value - tRider) <= MountWith) return 2;
            return 0;
        }

        private int[] Cnt(PartyBase pb, CharacterObject c)
        {
            Dictionary<CharacterObject, int[]> d;
            if (!_cnt.TryGetValue(pb, out d)) { d = new Dictionary<CharacterObject, int[]>(); _cnt[pb] = d; }
            int[] k;
            if (!d.TryGetValue(c, out k)) { k = new int[CommanderWithdraw458Map.CN]; d[c] = k; }
            return k;
        }

        /// <summary>Los wystawionego tak, jak go zapisze gra (SandBox BattleAgentLogic.OnAgentRemoved: zabity / nieprzytomny = ranny / reszta: morale &lt; 0.01
        /// wypisany ze skladu, inaczej zostaje zdrowy); zszedl zywy z morale &gt;= 0.01 sposrod wyprowadzanych = zbiegl z wodzem. Pod _gate.</summary>
        private void Track(Agent a, AgentState st, bool atEnd)
        {
            Org o;
            if (!_trk.TryGetValue(a, out o)) return;
            _trk.Remove(a);
            bool hero = o.C.IsHero;
            var h = hero ? o.C.HeroObject : null;
            if (st == AgentState.Killed) { if (hero) _heroK++; else Cnt(o.P, o.C)[CommanderWithdraw458Map.CK]++; return; }
            if (st == AgentState.Unconscious) { if (hero) _heroWs++; else Cnt(o.P, o.C)[CommanderWithdraw458Map.CWS]++; return; }
            float mor = 0f;
            try { mor = a.GetMorale(); } catch { mor = 0f; }
            if (mor < Routed) { if (hero) _heroRm++; else Cnt(o.P, o.C)[CommanderWithdraw458Map.CRm]++; return; }
            // 458-p (recenzja A1): zbiegly z wodzem = wyprowadzany, ktory NAPRAWDE zszedl z pola (usuniety w misji); stojacy / idacy przez pole na koniec - nie
            bool esc = !atEnd && _esc.Contains(a);
            if (hero) { if (esc && h != null) _keptHeroes.Add(h); return; }
            var k = Cnt(o.P, o.C);
            k[CommanderWithdraw458Map.CHS]++;
            if (esc) k[CommanderWithdraw458Map.CE]++;
            if (atEnd) k[CommanderWithdraw458Map.CEnd]++;
        }

        // ------------------------------------------------------------ los po chwili "widzi przegrana"

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            if (_ended || affectedAgent == null) return;
            if (_ruleKey)
            {
                try
                {
                    lock (_gate)
                    {
                        Track(affectedAgent, agentState, false);
                        // 458-u-p: los wlasnego konia jezdzca-szeregowego (pierwsze zejscie z pola)
                        if (affectedAgent.IsMount && _ownMount.Count > 0 && !_mountFate.ContainsKey(affectedAgent) && _ownMount.ContainsValue(affectedAgent))
                            _mountFate[affectedAgent] = new KeyValuePair<AgentState, float>(agentState, Mission != null ? Mission.CurrentTime : 0f);
                    }
                }
                catch (Exception e) { Stumble("Licznik", e); }
            }
            if (_teams.Count == 0) return;
            try
            {
                if (!affectedAgent.IsHuman) return;
                lock (_gate)
                for (int i = 0; i < _teams.Count; i++)
                {
                    var s = _teams[i];
                    if (!s.Trig) continue;
                    bool w = s.W.Contains(affectedAgent), r = !w && s.R.Contains(affectedAgent);
                    if (!w && !r) continue;
                    int k;
                    if (agentState == AgentState.Killed) k = FKilled;
                    else if (agentState == AgentState.Unconscious) k = FUncon;
                    else
                    {
                        float mor = 0f;
                        try { mor = affectedAgent.GetMorale(); } catch { mor = 0f; }
                        k = mor < 0.01f ? FFled : FLeft;   // SandBox BattleAgentLogic: ten sam prog (SetRouted(!flag))
                    }
                    if (w) { s.WFate[k]++; s.W.Remove(affectedAgent); } else { s.RFate[k]++; s.R.Remove(affectedAgent); }
                    if (ReferenceEquals(affectedAgent, s.CmdA))
                    {
                        s.CmdFate = (k == FKilled ? "zabity" : k == FUncon ? "nieprzytomny" : k == FLeft ? "zszedl z pola (morale >= 0.01)" : "uciekl w panice")
                                    + " t=" + F0(Mission != null ? Mission.CurrentTime : 0f) + "s";
                        s.CmdKept = k == FLeft;   // 458-r: uszedl z pola i zostaje w skladzie
                        s.CmdA = null;
                    }
                    break;
                }
            }
            catch (Exception e) { Stumble("Los", e); }
        }

        public override void OnMissionResultReady(MissionResult missionResult)
        {
            _result = missionResult;
            _resultSeen = true;
            if (_ruleKey && missionResult != null) { try { ForceRetreated(missionResult); } catch (Exception e) { Stumble("Wynik", e); } }
        }

        /// <summary>458-r: misja skonczona "wyczerpaniem" strony wodza (EnemyRetreated = false) przy zbieglych w skladzie - gra toczylaby bitwe dalej
        /// (PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMissionInternal, cs 99456). Wynik dla mapy jak przy ucieczce wroga: CampaignBattleResult
        /// nadpisany PO CampaignMissionComponent (ta sama metoda gry GetResult, enemyRetreated: true). Tylko gdy wodz nie padl (zabity / nieprzytomny).</summary>
        private void ForceRetreated(MissionResult r)
        {
            St s = null;
            lock (_gate) foreach (var x in _teams) if (x.Rule) { s = x; break; }
            if (s == null) return;
            if (!r.PlayerVictory) { s.ResultNote = "gracz nie wygral - mapa jak w grze"; return; }
            if (r.EnemyRetreated) { s.ResultNote = "EnemyRetreated z gry (wszyscy wroga uciekali)"; return; }
            // 458-p (recenzja B8): wymuszenie tylko, gdy wodz JUZ zszedl z pola (FLeft) - inaczej gra toczy bitwe dalej jak bez reguly
            if (!s.CmdKept) { s.ResultNote = "wyczerpanie strony, wodz nie zszedl z pola (" + s.CmdFate + ") - mapa jak w grze (bitwa dalej)"; return; }
            if (PlayerEncounter.Current == null || PlayerEncounter.CampaignBattleResult == null) { s.ResultNote = "brak CampaignBattleResult (kolejnosc zachowan) - mapa jak w grze"; return; }
            PlayerEncounter.CampaignBattleResult = CampaignBattleResult.GetResult(r.BattleState, true);
            s.Forced = true;
            s.ResultNote = "wyczerpanie strony (gra: bitwa dalej) -> wynik mapy EnemyRetreated (nadpisany)";
        }

        /// <summary>458-r koniec misji: los ludzi jeszcze na polu (jak BattleEndLogic.OnEndMission / brak usuniecia: morale &gt;= 0.01 - zostaja w skladzie),
        /// linia rozliczenia misji z kontrola sum i - przy udanym zejsciu wodza i wygranej gracza - oczekiwanie na pojmanie (CommanderWithdraw458Map).</summary>
        private bool RuleEnd(Mission m)
        {
            St s = null;
            lock (_gate)
            {
                foreach (var x in _teams) if (x.Rule) { s = x; break; }
                if (s == null) return false;
                var left = new List<Agent>(_trk.Keys);
                foreach (var a in left)
                {
                    try { if (a != null && a.IsActive()) Track(a, AgentState.Active, true); }
                    catch (Exception e) { Stumble("NaPolu", e); }
                }
                // 458-p (recenzja A1): wodz stojacy / idacy przez pole na koniec NIE uszedl - CmdKept tylko z OnAgentRemoved (FLeft)
                foreach (var a in s.Wd) if (a != null && _panicOut.Remove(a)) s.PanicOut++;   // recenzja A6: w panice do konca
                int sp = 0, k = 0, ws = 0, rm = 0, hs = 0, esc = 0, end = 0, parties = 0;
                foreach (var pv in _cnt)
                {
                    parties++;
                    foreach (var cv in pv.Value)
                    {
                        var c = cv.Value;
                        sp += c[CommanderWithdraw458Map.CS]; k += c[CommanderWithdraw458Map.CK]; ws += c[CommanderWithdraw458Map.CWS]; rm += c[CommanderWithdraw458Map.CRm];
                        hs += c[CommanderWithdraw458Map.CHS]; esc += c[CommanderWithdraw458Map.CE]; end += c[CommanderWithdraw458Map.CEnd];
                    }
                }
                int lost = 0;
                foreach (var kv in _trk) if (kv.Value != null && !kv.Value.C.IsHero) lost++;   // wystawieni bez losu (nie powinno byc)
                var r = _result;
                MapEvent me = null;
                try { me = PlayerEncounter.Battle; } catch { me = null; }
                string map;
                if (r == null || !r.PlayerVictory) map = "gracz nie wygral - mapa jak w grze (zbiegli z morale >= 0.01 zostaja w skladzie)";
                else if (!s.CmdKept) map = "wodz nie uszedl (" + s.CmdFate + ") - pojmanie jak w grze (decyzja 9: tylko jesli mu sie udalo)";
                else if (!r.EnemyRetreated && !s.Forced) map = "wynik bez EnemyRetreated i bez wymuszenia (" + s.ResultNote + ") - gra rozstrzyga dalej, bez oczekiwania";   // recenzja A4
                else if (s.Me == null || !ReferenceEquals(s.Me, me)) map = "MapEvent misji nie ten sam co przy rozkazie - pojmanie jak w grze";
                else if (s.CmdParty == null) map = "brak partii wodza - pojmanie jak w grze";
                else if (s.CmdHero == null || !ReferenceEquals(s.CmdParty.LeaderHero, s.CmdHero)) map = "wodz nie jest panem swojej partii - pojmanie jak w grze";   // recenzja B4
                else if (_buildMiss > 0) map = "wystawionych bez licznika " + _buildMiss + " - liczby misji niepewne, pojmanie jak w grze";   // recenzja B3
                else
                {
                    var pend = new CommanderWithdraw458Map.Pending
                    {
                        Me = s.Me, Side = s.T.Side, CmdParty = s.CmdParty, CmdHero = s.CmdHero, Cmd = s.Cmd,
                        Cnt = new Dictionary<PartyBase, Dictionary<CharacterObject, int[]>>(_cnt),
                        Spawned = new HashSet<Hero>(_spawnedHeroes), Kept = new HashSet<Hero>(_keptHeroes)
                    };
                    if (s.CmdHero != null) pend.Kept.Add(s.CmdHero);
                    CommanderWithdraw458Map.Set(pend);
                    map = "wodz uszedl - rozliczenie przy pojmaniu (linia 'Odwrot wodza (458) mapa')";
                }
                Log.Info(P + "rozliczenie misji: T" + s.T.TeamIndex + " " + s.T.Side + " | wodz " + s.Cmd + " -> " + s.CmdFate + (s.CmdKept ? " (uszedl)" : "")
                         + " | szeregowi strony: wystawieni " + sp + " = zabici " + k + " + ranni z pola " + ws + " + wypisani (morale < 0.01) " + rm + " + wrocili do skladu zdrowi " + hs
                         + " (w tym stali na koniec " + end + ") + bez losu " + lost + " -> " + (sp == k + ws + rm + hs + lost ? "OK" : "NIEZGODNE")
                         + " | zbiegli z wodzem " + esc + " | bohaterowie: wystawieni " + _spawnedHeroes.Count + ", uszli z wodzem " + _keptHeroes.Count + ", zabici " + _heroK + ", ranni " + _heroWs + ", wypisani " + _heroRm
                         + " | partii strony " + parties + " | wystawieni bez licznika " + _buildMiss + " | wynik: " + s.ResultNote + " | mapa: " + map + " | latka rozliczenia " + CommanderWithdraw458Map.Hook + " | potkniecia " + _stumbles + ".");
            }
            return true;
        }

        private static string PName(PartyBase pb)
        {
            try { return pb != null && pb.Name != null ? pb.Name.ToString() : "?"; } catch { return "?"; }
        }

        // ------------------------------------------------------------ koniec misji

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                // 458-q: autotest at-458 - OnMissionResultReady nie doszlo do tej logiki w ZADNEJ bitwie (14 x "wynik misji brak"), choc bitwy konczyly sie
                // same (CheckMissionEnded ustawia Mission.MissionResult PRZED petla MissionResultReady). Wynik brany wtedy z Mission.MissionResult.
                if (!_resultSeen && m != null)
                {
                    MissionResult mr = null;
                    try { mr = m.MissionResult; } catch { mr = null; }
                    if (mr != null)
                    {
                        _result = mr; _resultSeen = true; _resultLate = true;
                        if (_ruleKey) { try { ForceRetreated(mr); } catch (Exception e) { Stumble("WynikPozno", e); } }
                    }
                }
                bool anyRule = false;
                if (_ruleKey && _started && !_ended && _field)
                {
                    try { anyRule = RuleEnd(m); } catch (Exception e) { Stumble("Rozliczenie", e); }
                }
                if (_started && !_ended && _field && (_measure || anyRule))
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    var r = _result;
                    var sb = new StringBuilder(1024);
                    sb.Append("Odwrot wodza (458) koniec: t=").Append(F0(now)).Append(_ruleKey ? "s | klucz Commander Withdraws Under Rearguard TAK" : "s | sam pomiar (klucz Commander Withdraws Under Rearguard NIE)")
                      .Append(" | wynik misji ").Append(_resultLate ? "(z Mission.MissionResult przy koncu - OnMissionResultReady nie doszlo) " : "").Append(r == null ? (_resultSeen ? "null" : "brak (wyjscie bez wyniku)") : r.BattleState + (r.PlayerVictory ? ", zwyciestwo gracza" : "")
                          + (r.PlayerDefeated ? ", gracz pokonany" : "") + (r.EnemyRetreated ? ", EnemyRetreated" : ""))
                      .Append(" | druzyn AI ").Append(_teams.Count);
                    MapEvent me = null;
                    try { me = PlayerEncounter.Battle; } catch { me = null; }
                    int trig = 0;
                    foreach (var s in _teams)
                    {
                        var t = s.T;
                        sb.Append(" || T").Append(t != null ? t.TeamIndex : -1).Append(' ').Append(t != null ? t.Side.ToString() : "?").Append(s.EnemyOfPlayer ? " wrog" : " sojusznik")
                          .Append(" wodz ").Append(ProfName[Math.Max(0, Math.Min(2, (int)s.Prof))]).Append(s.ProfKnown ? "" : " (nieznany)")
                          .Append(": szczyt ").Append(s.Peak).Append(", zdolni na koniec ").Append(SafeAble(t)).Append(", kontakt ").Append(s.ContactAt >= 0f ? "t=" + F0(s.ContactAt) + "s" : "nie")
                          .Append(", progi 0.6 / 0.5 / 0.4 ").Append(T(s.First[0])).Append(" / ").Append(T(s.First[1])).Append(" / ").Append(T(s.First[2]));
                        if (!s.Trig) { sb.Append(" | nie widzial przegranej"); continue; }
                        trig++;
                        int wLive = 0, rLive = 0;
                        foreach (var a in s.W) if (a != null && a.IsActive()) wLive++;
                        foreach (var a in s.R) if (a != null && a.IsActive()) rLive++;
                        if (s.CmdA != null && s.CmdA.IsActive()) s.CmdFate = "na polu na koniec" + (s.CmdA.IsRunningAway ? " (ucieka)" : "");
                        int wAll = s.WFate[0] + s.WFate[1] + s.WFate[2] + s.WFate[3] + wLive;
                        sb.Append(s.Rule ? " | ODWROT NA ROZKAZ t=" : " | bylby odwrot t=").Append(F0(s.TrigAt)).Append("s (do konca ").Append(F0(now - s.TrigAt)).Append(" s), zdolni ").Append(s.TrigOwn).Append(" / wroga ").Append(s.TrigFoe)
                          .Append(" | wodz ").Append(s.Cmd).Append(" -> ").Append(s.CmdFate)
                          .Append(s.CmdEdge >= 0f ? ", granica pola " + F0(s.CmdEdge) + " m" : "")
                          .Append(" | wyprowadzani ").Append(wAll).Append(": zabici ").Append(s.WFate[FKilled]).Append(", nieprzytomni ").Append(s.WFate[FUncon])
                          .Append(", zeszli z morale >= 0.01 (zostaja w partii) ").Append(s.WFate[FLeft]).Append(", uciekli w panice (wypisani) ").Append(s.WFate[FFled]).Append(", na polu ").Append(wLive)
                          .Append(" | tylna straz ").Append(s.Rear).Append(s.RearF == null ? "" : ": zabici " + s.RFate[FKilled] + ", nieprzytomni " + s.RFate[FUncon] + ", zeszli " + s.RFate[FLeft]
                              + ", uciekli " + s.RFate[FFled] + ", na polu " + rLive)
                          .Append(" | jazda wroga w 100 m ").Append(s.FoeCav);
                        if (s.Rule)
                            sb.Append(" | regula: wyslano ").Append(s.Sent).Append(", ponowien ").Append(s.Resent).Append(", podloga morale ").Append(s.Pins)
                              .Append(", w panice (nie ruszani) ").Append(s.PanicOut).Append(", prowadzil 143 (ranni) ").Append(s.ByRh).Append(", bez punktu ucieczki ").Append(s.NoExit).Append(", wynik: ").Append(s.ResultNote);
                        else sb.Append(" | regula nie dziala: ").Append(s.RuleWhy);
                        sb.Append(" | mapa: ").Append(MapText(m, me, s, r));
                    }
                    sb.Append(" || razem: widzialo przegrana ").Append(trig).Append(" | HumanAIComponent.OnTick postfiksy: ").Append(RbmTick())
                      .Append(" | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            lock (_gate)
            {
                _teams.Clear();
                _trk.Clear();
                _cnt.Clear();
                _esc.Clear();
                _panicOut.Clear();
            }
            try { CommanderWithdraw458Map.GearStop(this); } catch { }   // 458-u (458-u-p: tu rozstrzyga konie wedlug _mountFate - czyszczone ponizej)
            lock (_gate) { _ownMount.Clear(); _mountFate.Clear(); }
            _bufE.Clear();
            _result = null;
            base.OnEndMission();
        }

        /// <summary>Co gra zrobi na mapie z pobita strona druzyny s (tylko odczyt MapEvent gracza) i ile druzyn AI ma ta strona.</summary>
        private string MapText(Mission m, MapEvent me, St s, MissionResult r)
        {
            try
            {
                if (!s.EnemyOfPlayer) return "sojusznik gracza - regula dotyczylaby tylko strony wroga";
                int aiTeams = 0;
                var teams = m != null ? m.Teams : null;
                for (int i = 0; teams != null && i < teams.Count; i++)
                {
                    var o = teams[i];
                    if (MissionTeams.Valid(o) && !o.IsPlayerTeam && o.HasTeamAi && o.Side == s.T.Side) aiTeams++;
                }
                if (me == null) return "brak MapEvent gracza, druzyn AI strony " + aiTeams;
                int healthy = 0, wounded = 0, parties = 0;
                var side = me.GetMapEventSide(s.T.Side);
                if (side != null)
                {
                    foreach (var p in side.Parties)
                    {
                        if (p == null || p.Party == null || p.Party.MemberRoster == null) continue;
                        parties++;
                        healthy += p.Party.MemberRoster.TotalHealthyCount;
                        wounded += p.Party.MemberRoster.TotalWounded;
                    }
                }
                var sb = new StringBuilder(160);
                sb.Append(me.IsFieldBattle ? "bitwa polowa" : "nie bitwa polowa").Append(me.IsNavalMapEvent ? " (morska)" : "").Append(", partii strony ").Append(parties)
                  .Append(", druzyn AI strony ").Append(aiTeams).Append(", w skladzie zdrowych ").Append(healthy).Append(" i rannych ").Append(wounded)
                  .Append(", RetreatingSide ").Append(me.RetreatingSide).Append(" -> gra: ");
                if (r == null) sb.Append("brak wyniku misji");
                else if (r.PlayerVictory)
                {
                    if (r.EnemyRetreated) sb.Append("pobici, caly sklad (zdrowi i ranni) do niewoli");
                    else if (healthy >= 1) sb.Append("strona wyczerpana na polu, zdrowi w skladzie - bitwa trwa dalej (ContinueBattle)");
                    else sb.Append("pobici, ranni do niewoli");
                    if (!s.Rule) sb.Append("; regula 458 nie dziala (").Append(s.RuleWhy).Append(")");
                    else sb.Append("; regula 458: w skladzie zostaja tylko zbiegli z wodzem i posilki niewprowadzone (gdy wodz uszedl); wszyscy ranni do zwyciezcy");
                }
                else if (r.PlayerDefeated) sb.Append("gracz pokonany - regula nie dotyczy");
                else sb.Append("bez zwyciezcy");
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Mapa", e); return "?"; }
        }

        private static int SafeAble(Team t)
        {
            try { return MissionTeams.Valid(t) ? AbleOf(t) : 0; } catch { return -1; }
        }

        /// <summary>Wlasciciele postfiksow na HumanAIComponent.OnTick (RBM StopRetreating - przeszkoda 1); raz na proces, tylko odczyt Harmony.</summary>
        private static string RbmTick()
        {
            if (_rbmTick != null) return _rbmTick;
            try
            {
                MethodBase mi = AccessTools.DeclaredMethod(typeof(HumanAIComponent), "OnTick");
                var info = mi != null ? Harmony.GetPatchInfo(mi) : null;
                if (mi == null) _rbmTick = "brak metody";
                else if (info == null || info.Postfixes == null || info.Postfixes.Count == 0) _rbmTick = "brak";
                else
                {
                    var own = new List<string>();
                    foreach (var p in info.Postfixes) if (p != null && !own.Contains(p.owner)) own.Add(p.owner);
                    _rbmTick = string.Join(", ", own.ToArray());
                }
            }
            catch (Exception e) { _rbmTick = "? (" + e.GetType().Name + ")"; }
            return _rbmTick;
        }

        private static string F0(float v) { return v.ToString("0", Inv); }

        private static string T(float at) { return at >= 0f ? "t=" + F0(at) + "s" : "-"; }

        private static string Pct(int a, int b) { return b > 0 ? ((int)Math.Round(100f * a / b)).ToString(Inv) + "%" : "-"; }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("CommanderWithdraw458." + where, e); } catch { }
        }
    }
}
