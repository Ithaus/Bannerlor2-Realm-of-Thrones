using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
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
        }

        private bool _known, _field, _started, _ended;
        private float _startAt = -1f, _next;
        private int _stumbles, _errs;
        private MissionResult _result;
        private bool _resultSeen;
        private readonly List<St> _teams = new List<St>();
        private readonly MBList<Agent> _bufE = new MBList<Agent>();

        private static string _rbmTick;   // wlasciciele postfiksow HumanAIComponent.OnTick (raz na proces)

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
                if (m.MissionEnded || now < _next) return;
                _next = now + Every;
                Scan(m, now);
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
                if (s.Trig) continue;
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
            Log.Info(P + "t=" + F0(now) + "s | T" + t.TeamIndex + " " + t.Side + (s.EnemyOfPlayer ? " (wrog gracza)" : " (sojusznik gracza)") + " | bylby odwrot (nic nie rozkazano)"
                     + " | wodz " + ProfName[Math.Max(0, Math.Min(2, (int)s.Prof))] + (s.ProfKnown ? "" : " (profil nieznany)") + " prog " + Thr[Math.Max(0, Math.Min(2, (int)s.Prof))].ToString("0.0", Inv)
                     + " | zdolni " + s.TrigOwn + " z szczytu " + s.TrigPeak + " (" + Pct(s.TrigOwn, s.TrigPeak) + "), wroga " + s.TrigFoe + " | od kontaktu " + F0(now - s.ContactAt) + " s"
                     + " | wodz: " + s.Cmd + (cmd != null ? ", " + (s.CmdMounted ? "konno" : "pieszo") + ", do granicy pola " + (s.CmdEdge >= 0f ? F0(s.CmdEdge) + " m (ok. " + F0(eta) + " s)" : "?") : "")
                     + " | tylna straz: " + s.Rear + " | wyprowadzani " + s.W.Count + " (konni " + s.WMounted + ", srednio do granicy " + (s.WEdgeN > 0 ? F0(s.WEdgeSum / s.WEdgeN) + " m" : "?") + ")"
                     + " | jazda wroga w 100 m " + s.FoeCav + ".");
        }

        // ------------------------------------------------------------ los po chwili "widzi przegrana"

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            if (_ended || affectedAgent == null || _teams.Count == 0) return;
            try
            {
                if (!affectedAgent.IsHuman) return;
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
        }

        // ------------------------------------------------------------ koniec misji

        protected override void OnEndMission()
        {
            try
            {
                var m = Mission;
                if (_started && !_ended && _field)
                {
                    float now = m != null ? m.CurrentTime : 0f;
                    var r = _result;
                    var sb = new StringBuilder(1024);
                    sb.Append("Odwrot wodza (458) koniec: t=").Append(F0(now)).Append("s | sam pomiar (regula i klucz Commander Withdraws Under Rearguard nie wdrozone - opis 458)")
                      .Append(" | wynik misji ").Append(r == null ? (_resultSeen ? "null" : "brak (wyjscie bez wyniku)") : r.BattleState + (r.PlayerVictory ? ", zwyciestwo gracza" : "")
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
                        sb.Append(" | bylby odwrot t=").Append(F0(s.TrigAt)).Append("s (do konca ").Append(F0(now - s.TrigAt)).Append(" s), zdolni ").Append(s.TrigOwn).Append(" / wroga ").Append(s.TrigFoe)
                          .Append(" | wodz ").Append(s.Cmd).Append(" -> ").Append(s.CmdFate)
                          .Append(s.CmdEdge >= 0f ? ", granica pola " + F0(s.CmdEdge) + " m" : "")
                          .Append(" | wyprowadzani ").Append(wAll).Append(": zabici ").Append(s.WFate[FKilled]).Append(", nieprzytomni ").Append(s.WFate[FUncon])
                          .Append(", zeszli z morale >= 0.01 (zostaja w partii) ").Append(s.WFate[FLeft]).Append(", uciekli w panice (wypisani) ").Append(s.WFate[FFled]).Append(", na polu ").Append(wLive)
                          .Append(" | tylna straz ").Append(s.Rear).Append(s.RearF == null ? "" : ": zabici " + s.RFate[FKilled] + ", nieprzytomni " + s.RFate[FUncon] + ", zeszli " + s.RFate[FLeft]
                              + ", uciekli " + s.RFate[FFled] + ", na polu " + rLive)
                          .Append(" | jazda wroga w 100 m ").Append(s.FoeCav);
                        sb.Append(" | mapa: ").Append(MapText(m, me, s, r));
                    }
                    sb.Append(" || razem: widzialo przegrana ").Append(trig).Append(" | HumanAIComponent.OnTick postfiksy: ").Append(RbmTick())
                      .Append(" | potkniecia ").Append(_stumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            _teams.Clear();
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
                    sb.Append("; regula 458 (bylaby, przy udanym zejsciu wodza): RetreatingSide - strona uchodzi z calym skladem");
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
