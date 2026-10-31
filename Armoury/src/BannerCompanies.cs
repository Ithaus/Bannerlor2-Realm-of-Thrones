using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P3 "choragwie" (projekt choragwie.json B-C, krytyk K5/K6/K9/K10; klucze BannerCompaniesEnabled, BannerCompanySize, FieldStandards,
    /// BannerShockStandardLost, BannerShockCommanderLost, BannerBreakChance, BannerVictoryCheer, BannerCompanyMessages). Jeff 25.10: "niech niosa sztandary ...
    /// choragwie po np. 50 ludzi i kazdy ma dowodce i sztandar i jak padnie sztandar lub dowodca to spada morale i choragiew moze sie zalamac, im wyzszy tier
    /// sredni tym mniejsza szansa".
    ///
    /// Pojecia (K5): "Banner" = formacja z dowodca (kapitan Order of Battle - przeglad armii P4); "company" = podgrupa ok. Banner Company Size ludzi wewnatrz
    /// formacji (podzial sam, w bitwie). Kapitan formacji dowodzi kompania, w ktorej stoi. Komunikaty "{BANNER} - company 2".
    ///
    /// Dowod [K]: gra ma chorazych (BannerBearerLogic + SandboxBattleBannerBearersModel): 1 na formacje, tylko gdy kapitan-bohater ma sztandar
    /// (GeneralsAndCaptainsAssignmentLogic.AssignBestCaptainsForTeam -> SetFormationBanner). Smierc kapitana gra liczy jak kazda smierc (najwyzej 10 sasiadow
    /// w 4 m, AgentMoraleInteractionLogic) - na formacje jako calosc nie dziala nic. Mission.OnDeploymentFinished wola zachowania OD KONCA listy (mbfull
    /// Mission.cs:6740-6754) - nasze (dodane w OnMissionBehaviorInitialize) biegnie PRZED BannerBearerLogic.OnDeploymentFinished, czyli gdy gra jeszcze pamieta
    /// sprzet sprzed podmiany (_initialSpawnEquipments - bez niego UpdateAgent(agent, false) rzucilby KeyNotFound); DeploymentMissionController.FinishDeployment
    /// odpauzowuje AI dopiero PO Mission.OnDeploymentFinished (:41-63), wiec SetFormationBanner (UpdateAgent pauzuje chorazego) jest tu bezpieczne.
    ///
    /// Zasady (bitwa polowa kampanii: IsFieldBattle, nie kryjowka; misja z BannerBearerLogic):
    ///  B (OnDeploymentFinished, raz; klucze FieldStandards / BannerCompaniesEnabled, tylko przy czynnym A - BannerKit.MoreAllowed, i z DefaultBattleMissionAgentSpawnLogic,
    ///    bo UpdateBannerBearersForDeployment czyta AgentSpawnLogic.GetSpawnHorses - NRE bez niej): formacja 0-7 od 2 ludzi bez sztandaru, ktorej wiekszosc
    ///    nie jest z partii bandytow ani z umarlych, dostaje sztandar polowy campaign_banner_small (herb z Origin.Banner chorazego, -10% kary morale od strat,
    ///    dopoki stoi; zaden roster nie dostaje sztuki - DTE pomija IsBannerItem). Formacja ze sztandarem, ktora ma mniej chorazych niz chce model (postfiks
    ///    BannerKit.DesiredPostfix: k = liczba kompanii), dostaje ponowne SetFormationBanner (przeliczenie - OOB gracza przelicza tylko przy progu 2 ludzi).
    ///  C (OnDeploymentFinished po B; klucz BannerCompaniesEnabled): kazda formacja 0-7 dzielona na k kompanii (CompaniesFor: ponizej 0.6 x Size - jedna,
    ///    inaczej round(n / Size), najwyzej 6) wedlug rzutu pozycji na wektor w prawo formacji - rowne liczebnie od lewej. Czlonkowie: ludzie (bez umarlych),
    ///    bohaterowie i gracz tez (moga dowodzic), ale nie lamia sie (jak BrokenMen). Chorazy kompanii: chorazy gry stojacy w jej przedziale (zajety - najblizsza
    ///    kompania bez chorazego). Dowodca: kapitan formacji w swojej kompanii, inni bohaterowie - kompania bez bohatera najblizej; reszta - najwyzszy tier
    ///    (nie chorazy; remis: zdrowie, potem Index) - "sierzant".
    ///  Zdarzenia (takt 0.5 s; smierc dowodcy i chorazego od razu w OnAgentRemoved):
    ///    1. sztandar padl (chorazy zabity / omdlaly / zbiegl albo upuscil sztandar - Banner null): wstrzas S = BannerShockStandardLost, rzut F = 1;
    ///       sztandar podniesiony (szukajacy gry w 9 m co 3 s, posilki ze sztandarem, od 158 takze biegacz - BannerCompanies.Pickup.cs) - chorazy przypisany
    ///       do kompanii bez sztandaru najblizej miejsca upadku, +S/2 raz na upadek; chorazy odszedl z formacja (RBM split, rozkaz) - bez wstrzasu, liczony osobno;
    ///    2. dowodca padl (zabity albo omdlaly): S = BannerShockCommanderLost x1.5 bohater x2 wodz strony; F = 2 wodz / 1.5 bohater / 1;
    ///    3. pierwszy raz zostala polowa skladu (w szeregu wobec stanu po rozstawieniu i posilkach): sam rzut, F = 1.
    ///  Wstrzas: kazdy czlonek AI (nie gracz, nie uciekajacy) ChangeMorale(-BattleMoraleModel.CalculateMoraleChangeToCharacter(agent, S)) - ten sam model co gra.
    ///  Rzut: raz na zdarzenie, kompania niezlamana, srednie morale ponizej 50: p = clamp(BannerBreakChance x F x M / R, 0, 0.9), M = clamp(2 - morale/25, 0.4, 1.6),
    ///    R = 0.5 x sredni tier szeregowych + 1 (wzor odpornosci gry CharacterObject.GetMoraleResistance).
    ///  Zalamanie: szeregowi AI w 4 falach co 0.5 s - RoutHold.Break(a, MissionTeams.Flee) (jedna regula zlamanego, liczy sie jako "ucieczka"), bez RoutKeeper
    ///    (BrokenMenNeverRally wylaczony) SetMorale(0) - droga paniki gry. Okrzyk zwyciezcow: najblizsza wroga formacja w 60 m - BattleCries.Cheer (P2);
    ///    zdarzenia dla dzwieku BannerCompanies.StandardFell / CommanderFell / CompanyBroke (BannerCompanies.Events.cs).
    ///  Czlonkostwo zamrozone; co 3 s przeglad Mission.Agents: posilki i przeniesieni do innej formacji - do kompanii swojej formacji z najmniejsza liczba
    ///    w szeregu (formacja bez kompanii - podzial od razu).
    ///  Komunikaty (BannerCompanyMessages, najwyzej 1 na 2 s): strona gracza - upadek sztandaru, dowodcy, zalamanie; wrog - tylko zalamanie.
    /// Bez Harmony, bez zmian wizerunkow, nic co klatke na agenta (OnMissionTick tylko licznik czasu; 158: co klatke tylko oproznienie kolejki zdarzen
    /// sztandarow, biegacze co 0.25 s). Wyjatki liczone (RoutHold.Stumble, miejsce
    /// "choragiew") - bez globalnego wylacznika. Linie: "Choragwie (CH) start" po rozstawieniu, "Choragwie (CH) koniec" po misji, segment 'choragwie' w NB2.
    ///
    /// KONTRAKT P0: ResetCounters / Activity / Append - segment ' | choragwie ...' linii "Bitwa (NB2)" (bez znaku '|' w srodku), wolane pod RoutHold.Gate.
    /// </summary>
    internal static partial class BannerCompanies
    {
        internal const int MaxPerFormation = 6;          // tyle miejsc na sztandar ma DefaultFormationArrangementModel (linia, kolo, klin, kwadrat)
        internal const float SmallShare = 0.6f;          // ponizej 0.6 x Size ludzi - jedna kompania
        internal const float TickEvery = 0.5f, ReviewEvery = 3f, MsgGap = 2f, CheerRange = 60f, WaveGap = 0.5f;
        internal const int Waves = 4;
        internal const float RollBelow = 50f, PMax = 0.9f;

        /// <summary>Liczba kompanii dla n szeregowych (wspolna dla podzialu i dla liczby chorazych - BannerKit.DesiredPostfix).</summary>
        internal static int CompaniesFor(int n, int size)
        {
            if (size < 1) size = 50;
            if (n < SmallShare * size) return 1;
            int k = (int)Math.Floor(n / (double)size + 0.5);
            if (k < 1) k = 1;
            if (k > MaxPerFormation) k = MaxPerFormation;
            return k;
        }

        // recenzja: licznik szeregowych dla BannerKit.DesiredPostfix (glowny watek, bez alokacji na wolanie)
        private static int _soldiers;
        private static readonly Action<Agent> _countSoldier = a =>
        {
            if (a == null || !a.IsActive() || !a.IsHuman || a.IsHero || Undead.Character(a.Character)) return;
            _soldiers++;
        };

        /// <summary>Szeregowi formacji liczeni jak w podziale (BannerCompaniesMission.Split): czynni ludzie bez bohaterow i bez umarlych.</summary>
        internal static int SoldiersOf(Formation f)
        {
            _soldiers = 0;
            if (f == null) return 0;
            f.ApplyActionOnEachUnit(_countSoldier);
            return _soldiers;
        }

        // ---- liczniki misji (glowny watek; czytane w Append pod RoutHold.Gate, zerowane w RoutHold.Ensure i w BannerCompaniesMission.AfterStart)
        internal static int Formations, Companies, Members, MinSize, MaxSize, BearersSet, NoBearer, ExtraBearers, CmdHero, CmdSergeant, CmdPlayer;
        internal static int StdHero, StdField, StdSkipped, StdNoItem, StdRecount, StdCallFails, CapTotal, CapBanner;
        internal static int StdFell, StdRaised, StdMoved, CmdFell, CmdFellHero, CmdFellGeneral, CmdFellPlayer, CmdMoved, Half, Rolls, RollsSkipped,
                            Breaks, BrokenMen, BrokenRout, BrokenMorale, BrokenRefused, Cheers, CheersNoEnemy, Msgs, MsgsLimited, Joined, Moved, Lazy, Shocked, Stumbles;
        internal static readonly int[] TierRolls = new int[8], TierBreaks = new int[8];
        // 305c (klucz BannerLossBreakIgnoresMorale): rzut po stratach bez bramki morale - trzecia czesc strat, rzuty strat (w tym ponad bramka), suma morale i p, zalamania ze strat
        internal static int Third, LossRolls, LossOverGate, LossBreaks, LossBrokenMen;
        internal static float LossMoraleSum, LossPSum;
        // 307b (klucz BannerLossBreakByShare): rzuty wedlug udzialu strat - co 1/6 skladu od 1/3, M = clamp(2 x utracona czesc, 0.4, 1.6) zamiast z morale
        internal static int ShareRolls, ShareBreaks;
        internal static float ShareLostSum, SharePSum;
        internal static readonly int[] ShareStep = new int[7];

        internal static void ResetCounters()
        {
            Formations = Companies = Members = MinSize = MaxSize = BearersSet = NoBearer = ExtraBearers = CmdHero = CmdSergeant = CmdPlayer = 0;
            StdHero = StdField = StdSkipped = StdNoItem = StdRecount = StdCallFails = CapTotal = CapBanner = 0;
            StdFell = StdRaised = StdMoved = CmdFell = CmdFellHero = CmdFellGeneral = CmdFellPlayer = CmdMoved = Half = Rolls = RollsSkipped = 0;
            Breaks = BrokenMen = BrokenRout = BrokenMorale = BrokenRefused = Cheers = CheersNoEnemy = Msgs = MsgsLimited = Joined = Moved = Lazy = Shocked = Stumbles = 0;
            for (int i = 0; i < 8; i++) { TierRolls[i] = 0; TierBreaks[i] = 0; }
            Third = LossRolls = LossOverGate = LossBreaks = LossBrokenMen = 0;
            LossMoraleSum = LossPSum = 0f;
            ShareRolls = ShareBreaks = 0;
            ShareLostSum = SharePSum = 0f;
            Array.Clear(ShareStep, 0, ShareStep.Length);
        }

        internal static int Activity { get { return Companies + StdField + StdFell + CmdFell + Breaks + Stumbles; } }

        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | choragwie ").Append(c != null && c.BannerCompaniesEnabled ? "TAK" : "NIE").Append(": kompanii ").Append(Companies)
              .Append(" w formacjach ").Append(Formations).Append(", sztandary polowe ").Append(StdField).Append(", sztandar padl ").Append(StdFell)
              .Append(" (podniesiony ").Append(StdRaised).Append("), dowodca padl ").Append(CmdFell).Append(", rzutow ").Append(Rolls)
              .Append(", zalaman ").Append(Breaks).Append(" (ludzi ").Append(BrokenMen).Append("), okrzykow zwyciestwa ").Append(Cheers);
        }

        internal static void Stumble(string where, Exception e)
        {
            Stumbles++;
            RoutHold.Stumble(RoutHold.SiteBanner, where, e);
        }
    }

    /// <summary>noc 25.10 P3: zachowanie misji choragwi (patrz BannerCompanies). Rejestrowane w SubModuleMain.OnMissionBehaviorInitialize (kampania, klucze P3).
    /// 158: czesc "biegacz po wlasny sztandar" w BannerCompanies.Pickup.cs (partial).</summary>
    internal sealed partial class BannerCompaniesMission : MissionLogic
    {
        private sealed class Company
        {
            public Formation F;
            public Team T;
            public int Index, Count;                       // numer w formacji (0..) i liczba kompanii formacji przy podziale
            public readonly List<Agent> Men = new List<Agent>();
            public Agent Bearer;
            public bool HadStandard, StandardUp, LastFall, BonusDue;
            public Vec3 LastPos, FallPos;
            public Agent Commander;
            public bool CmdHero, CmdGeneral, CmdPlayer, CmdDown;
            public string CmdName;
            public int Peak;
            public bool HalfDone, Broken;
            public bool ThirdDone, LossBreak;              // 305c: rzut po trzeciej czesci strat juz byl; zalamanie przyszlo z rzutu strat
            public int LossStep;                           // 307b: ostatnia szosta czesc strat, przy ktorej byl rzut (0 = jeszcze nie)
            public List<Agent> Queue;
            public int Wave, Chunk;
            public float NextWave;
            public int TierIx = 1;
        }

        private BannerBearerLogic _bbl;
        private bool _spawnOk, _field, _sub, _deployed, _inCall, _unpauseDone, _lineDone;
        private float _acc, _accReview, _deployedAt, _lastMsg = -100f;
        private readonly Dictionary<Formation, List<Company>> _forms = new Dictionary<Formation, List<Company>>();
        private readonly List<Company> _all = new List<Company>();
        private readonly Dictionary<Agent, Company> _mem = new Dictionary<Agent, Company>();
        private readonly Dictionary<Agent, Company> _bearerOf = new Dictionary<Agent, Company>();
        private readonly HashSet<Agent> _skip = new HashSet<Agent>();             // umarli (Undead) - poza kompaniami
        private readonly List<Agent> _buf = new List<Agent>();
        private readonly List<Agent> _unpause = new List<Agent>();
        private readonly List<Formation> _lazy = new List<Formation>();
        private readonly Dictionary<string, int[]> _capCult = new Dictionary<string, int[]>();
        private Action<Agent> _collect;
        private Action<Agent, bool> _onBearer;
        private string _deployNote = "-";
        // 154 (Jeff 25.10: "baner stoi pusty"): linia na kazde zdarzenie sztandaru (padl / odszedl z chorazym / podniesiony) - najwyzej StdLinesMax na misje
        // 158: ta sama linia dla biegacza (wyslany / porzucil / podniosl) i ponownego upuszczenia - do 4 linii wiecej na upadek, wiec limit 60 -> 120
        private const int StdLinesMax = 120;
        private int _stdLines, _stdLinesCut;

        // ------------------------------------------------------------ start / koniec

        public override void AfterStart()
        {
            base.AfterStart();
            var m = Mission;
            try
            {
                var c = Settings.Current;
                _collect = a => _buf.Add(a);
                _bbl = m != null ? m.GetMissionBehavior<BannerBearerLogic>() : null;
                _spawnOk = m != null && m.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>() != null;
                bool battle = false;
                try { battle = Campaign.Current != null && TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent != null; } catch { battle = false; }
                _field = m != null && _bbl != null && battle && m.IsFieldBattle && !HideoutAlarm.IsHideout(m);
                BannerCompanies.ResetCounters();
                BannerKit.BeginMission(m, _field && _spawnOk);
                // A: zdarzenie gry przy kazdej podmianie chorazego (kazda misja z logika sztandarow - takze oblezenie; DTE rozlicza tak samo)
                if (_bbl != null && c != null && c.StandardBearerKitSafe)
                {
                    _onBearer = OnBearer;
                    _bbl.OnBannerBearerAgentUpdated += _onBearer;
                    _sub = true;
                }
            }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.AfterStart", e); }
            // 158: zdarzenia upuszczenia / podniesienia przedmiotu (tylko kolejka) - bitwa polowa, takze przy kluczu BannerPickupRange 0 (sam pomiar)
            try { RunStart(m); }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.RunStart", e); }
        }

        private void Unsub()
        {
            try { if (_sub && _bbl != null && _onBearer != null) _bbl.OnBannerBearerAgentUpdated -= _onBearer; } catch { }
            _sub = false;
            RunUnsub();
        }

        protected override void OnEndMission()
        {
            try { if (!_lineDone) { _lineDone = true; if (_field) WriteEnd(); } }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.OnEndMission", e); }
            // 158: biegacze zdjeci, linia "Sztandar (158)" (raz na misje)
            try { RunEnd(); }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.RunEnd", e); }
            Unsub();
            try { BannerKit.EndMission(Mission); } catch { }
            Clear();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            Unsub();
            try { BannerKit.EndMission(Mission); } catch { }
            try { BannerKit.Finalized(Mission); } catch { }
            Clear();
            base.OnRemoveBehavior();
        }

        private void Clear()
        {
            _forms.Clear(); _all.Clear(); _mem.Clear(); _bearerOf.Clear(); _skip.Clear(); _buf.Clear(); _unpause.Clear(); _lazy.Clear(); _capCult.Clear();
            RunClear();   // 158
        }

        private void OnBearer(Agent a, bool bearer)
        {
            try
            {
                BannerKit.OnBearerUpdated(a, bearer);
                if (_inCall && a != null) _unpause.Add(a);
            }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.OnBearer", e); }
        }

        // ------------------------------------------------------------ rozstawienie

        public override void OnDeploymentFinished()
        {
            base.OnDeploymentFinished();
            if (!_field || _deployed) return;
            var m = Mission;
            var c = Settings.Current;
            if (m == null || c == null) return;
            _deployed = true;
            _deployedAt = m.CurrentTime;
            try { Standards(m, c); }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.Standards", e); }
            if (c.BannerCompaniesEnabled)
            {
                foreach (var t in m.Teams)
                {
                    if (!MissionTeams.Valid(t)) continue;
                    foreach (var f in t.FormationsIncludingEmpty)
                    {
                        if (f == null || (int)f.FormationIndex >= 8 || f.CountOfUnits <= 0) continue;
                        try { Split(f, m); }
                        catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.Split", e); }
                    }
                }
            }
            try { WriteStart(m, c); }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.WriteStart", e); }
        }

        /// <summary>B: sztandary polowe i przeliczenie liczby chorazych (tylko w rozstawieniu - patrz opis klasy).</summary>
        private void Standards(Mission m, Settings c)
        {
            bool allowed = BannerKit.MoreAllowed(c);
            bool recount = allowed && c.BannerCompaniesEnabled && BannerKit.CountHooked;
            ItemObject plain = null;
            if (c.FieldStandards && allowed)
            {
                try { plain = MBObjectManager.Instance.GetObject<ItemObject>("campaign_banner_small"); } catch { plain = null; }
                if (plain == null || !BannerBearerLogic.IsBannerItem(plain)) { plain = null; BannerCompanies.StdNoItem++; }
            }
            if (!_spawnOk || _bbl.AgentSpawnLogic == null)
            {
                _deployNote = "bez DefaultBattleMissionAgentSpawnLogic - sztandary bez zmian";
                recount = false; plain = null;
            }
            else if (!allowed) _deployNote = "A nieczynne (StandardBearerKitSafe albo latka DTE) - sztandary bez zmian";
            else _deployNote = "TAK";
            var model = MissionGameModels.Current != null ? MissionGameModels.Current.BattleBannerBearersModel : null;
            foreach (var t in m.Teams)
            {
                if (!MissionTeams.Valid(t)) continue;
                foreach (var f in t.FormationsIncludingEmpty)
                {
                    if (f == null || (int)f.FormationIndex >= 8 || f.CountOfUnits < 2) continue;
                    try
                    {
                        var cur = _bbl.GetFormationBanner(f);
                        var cap = f.Captain;
                        if (cap != null && cap.IsHero && cap.Character != null)
                        {
                            string cu = cap.Character.Culture != null ? cap.Character.Culture.StringId ?? "?" : "?";
                            int[] v;
                            if (!_capCult.TryGetValue(cu, out v)) { v = new int[2]; _capCult[cu] = v; }
                            v[0]++; BannerCompanies.CapTotal++;
                            if (cur != null) { v[1]++; BannerCompanies.CapBanner++; }
                        }
                        if (cur != null)
                        {
                            BannerCompanies.StdHero++;
                            if (recount && model != null)
                            {
                                int want = model.GetDesiredNumberOfBannerBearersForFormation(f);
                                int have = _bbl.GetFormationBannerBearers(f).Count;
                                // tylko w gore: zmniejszenie wolaloby UpdateAgent(agent, false) takze dla chorazego z posilkow (bez wpisu _initialSpawnEquipments - KeyNotFound w grze)
                                if (want > have) { Call(f, cur); BannerCompanies.StdRecount++; }
                            }
                        }
                        else if (plain != null)
                        {
                            if (Unworthy(f)) { BannerCompanies.StdSkipped++; continue; }
                            Call(f, plain);
                            BannerCompanies.StdField++;
                        }
                    }
                    catch (Exception e) { BannerCompanies.StdCallFails++; BannerCompanies.Stumble("BannerCompaniesMission.SetFormationBanner", e); }
                }
            }
        }

        private void Call(Formation f, ItemObject banner)
        {
            _inCall = true;
            try { _bbl.SetFormationBanner(f, banner); }
            finally { _inCall = false; }
        }

        /// <summary>Formacja bez sztandaru polowego: wiekszosc ludzi z partii bandytow albo umarli (Nocny Krol).</summary>
        private bool Unworthy(Formation f)
        {
            _buf.Clear();
            f.ApplyActionOnEachUnit(_collect);
            int n = 0, bad = 0;
            for (int i = 0; i < _buf.Count; i++)
            {
                var a = _buf[i];
                if (a == null || !a.IsHuman) continue;
                n++;
                bool b = false;
                try
                {
                    var pb = a.Origin != null ? a.Origin.BattleCombatant as PartyBase : null;
                    var mp = pb != null ? pb.MobileParty : null;
                    b = (mp != null && mp.IsBandit) || Undead.Character(a.Character);
                }
                catch { b = false; }
                if (b) bad++;
            }
            _buf.Clear();
            return n == 0 || bad * 2 > n;
        }

        /// <summary>C: podzial formacji na kompanie (rozstawienie albo formacja bez kompanii przy przegladzie).</summary>
        private bool Split(Formation f, Mission m)
        {
            var c = Settings.Current;
            if (c == null || f == null) return false;
            _buf.Clear();
            f.ApplyActionOnEachUnit(_collect);
            var men = new List<Agent>(_buf.Count);
            int soldiers = 0;
            for (int i = 0; i < _buf.Count; i++)
            {
                var a = _buf[i];
                if (a == null || !a.IsActive() || !a.IsHuman || _skip.Contains(a)) continue;
                if (Undead.Character(a.Character)) { _skip.Add(a); continue; }
                men.Add(a);
                if (!a.IsHero) soldiers++;
            }
            _buf.Clear();
            if (soldiers == 0) return false;                   // sami bohaterowie - bez kompanii
            int k = BannerCompanies.CompaniesFor(soldiers, c.BannerCompanySize);
            if (k > men.Count) k = men.Count;
            // rzut pozycji na wektor w prawo formacji - kompanie rowne liczebnie od lewej
            Vec2 dir = f.Direction;
            float dl = dir.Length;
            if (!(dl > 1e-3f)) dir = new Vec2(0f, 1f); else dir = dir * (1f / dl);
            var right = new Vec2(dir.y, -dir.x);
            Vec2 cen = Vec2.Zero;
            for (int i = 0; i < men.Count; i++) cen += men[i].Position.AsVec2;
            cen = cen * (1f / men.Count);
            var key = new float[men.Count];
            var order = new Agent[men.Count];
            for (int i = 0; i < men.Count; i++) { key[i] = (men[i].Position.AsVec2 - cen).DotProduct(right); order[i] = men[i]; }
            Array.Sort(key, order);
            var list = new List<Company>(k);
            for (int i = 0; i < k; i++)
            {
                var co = new Company { F = f, T = f.Team, Index = i, Count = k };
                list.Add(co);
                _all.Add(co);
            }
            _forms[f] = list;
            for (int i = 0; i < order.Length; i++)
            {
                int ci = (int)((long)i * k / order.Length);
                if (ci >= k) ci = k - 1;
                Put(order[i], list[ci]);
            }
            // chorazowie gry: kompania, w ktorej stoi, a gdy zajeta - najblizsza bez chorazego
            List<Agent> bearers = null;
            try { bearers = _bbl != null ? _bbl.GetFormationBannerBearers(f) : null; } catch { bearers = null; }
            if (bearers != null)
            {
                for (int i = 0; i < bearers.Count; i++)
                {
                    var b = bearers[i];
                    if (b == null || !b.IsActive() || b.Banner == null || !ReferenceEquals(b.Formation, f) || _bearerOf.ContainsKey(b)) continue;
                    Company home;
                    if (!_mem.TryGetValue(b, out home) || !ReferenceEquals(home.F, f)) continue;
                    var to = home.Bearer == null ? home : NearestFree(list, home.Index, x => x.Bearer == null);
                    if (to == null) { BannerCompanies.ExtraBearers++; continue; }
                    if (!ReferenceEquals(to, home)) Put(b, to);
                    SetBearer(to, b);
                }
            }
            // dowodcy: kapitan formacji w swojej kompanii, inni bohaterowie - kompania bez bohatera najblizej; reszta - sierzant
            var cap = f.Captain;
            Company cc;
            if (cap != null && cap.IsActive() && _mem.TryGetValue(cap, out cc) && ReferenceEquals(cc.F, f)) SetCommander(cc, cap);
            for (int i = 0; i < order.Length; i++)
            {
                var a = order[i];
                if (!a.IsHero || ReferenceEquals(a, cap)) continue;
                Company home;
                if (!_mem.TryGetValue(a, out home) || !ReferenceEquals(home.F, f)) continue;
                if (home.Commander == null) { SetCommander(home, a); continue; }
                var to = NearestFree(list, home.Index, x => x.Commander == null);
                if (to == null) continue;
                Put(a, to);
                SetCommander(to, a);
            }
            for (int i = 0; i < list.Count; i++)
            {
                var co = list[i];
                if (co.Commander == null) SetCommander(co, Sergeant(co));
                co.Peak = co.Men.Count;
                co.TierIx = TierIx(AvgTier(co));
                // liczniki podzialu
                BannerCompanies.Companies++;
                BannerCompanies.Members += co.Men.Count;
                if (BannerCompanies.MinSize == 0 || co.Men.Count < BannerCompanies.MinSize) BannerCompanies.MinSize = co.Men.Count;
                if (co.Men.Count > BannerCompanies.MaxSize) BannerCompanies.MaxSize = co.Men.Count;
                if (co.HadStandard) BannerCompanies.BearersSet++; else BannerCompanies.NoBearer++;
                if (co.Commander == null) { }
                else if (co.CmdPlayer) BannerCompanies.CmdPlayer++;
                else if (co.CmdHero) BannerCompanies.CmdHero++;
                else BannerCompanies.CmdSergeant++;
            }
            BannerCompanies.Formations++;
            return true;
        }

        private static Company NearestFree(List<Company> list, int from, Func<Company, bool> free)
        {
            Company best = null;
            int bd = int.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                var x = list[i];
                if (!free(x)) continue;
                int d = Math.Abs(x.Index - from);
                if (d < bd) { bd = d; best = x; }
            }
            return best;
        }

        /// <summary>Czlowiek do kompanii (z wypisaniem z poprzedniej).</summary>
        private void Put(Agent a, Company to)
        {
            Company old;
            if (_mem.TryGetValue(a, out old))
            {
                if (ReferenceEquals(old, to)) return;
                old.Men.Remove(a);
                if (old.Peak > 0) old.Peak--;
            }
            to.Men.Add(a);
            _mem[a] = to;
        }

        private void SetBearer(Company co, Agent b)
        {
            co.Bearer = b;
            co.HadStandard = co.StandardUp = true;
            co.LastPos = b.Position;
            _bearerOf[b] = co;
        }

        private static void SetCommander(Company co, Agent a)
        {
            co.Commander = a;
            if (a == null) return;
            co.CmdHero = a.IsHero;
            co.CmdPlayer = a.IsMainAgent;
            co.CmdGeneral = co.T != null && ReferenceEquals(co.T.GeneralAgent, a);
            string n = null;
            try { n = a.Name; } catch { n = null; }
            co.CmdName = string.IsNullOrEmpty(n) ? null : n;
        }

        /// <summary>Sierzant: najwyzszy tier szeregowego AI, ktory nie jest chorazym (remis: wiecej zdrowia, potem nizszy Index).</summary>
        private Agent Sergeant(Company co)
        {
            Agent best = null;
            int bt = -1;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive() || a.IsHero || !a.IsAIControlled || ReferenceEquals(a, co.Bearer) || _bearerOf.ContainsKey(a)) continue;
                int t = Tier(a);
                if (best == null || t > bt || (t == bt && (a.HealthLimit > best.HealthLimit || (a.HealthLimit == best.HealthLimit && a.Index < best.Index))))
                { best = a; bt = t; }
            }
            return best;
        }

        private static int Tier(Agent a)
        {
            var co = a != null ? a.Character as CharacterObject : null;
            return co != null ? co.Tier : 0;
        }

        private static float AvgTier(Company co)
        {
            int n = 0, s = 0;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive() || a.IsHero) continue;
                n++; s += Tier(a);
            }
            return n > 0 ? (float)s / n : 1f;
        }

        private static int TierIx(float t)
        {
            int i = (int)Math.Round(t);
            if (i < 1) i = 1;
            if (i > 7) i = 7;
            return i;
        }

        // ------------------------------------------------------------ tick

        public override void OnMissionTick(float dt)
        {
            // 158: kolejka zdarzen sztandarow co klatke (tani odczyt), biegacze co 0.25 s / wybor co 0.5 s - takze bez kompanii (klucz BannerCompaniesEnabled NIE)
            if (_runSub)
            {
                try { RunTick(dt); }
                catch (Exception e) { _rStumbles++; BannerCompanies.Stumble("BannerCompaniesMission.RunTick", e); }
            }
            if (!_deployed || (_all.Count == 0 && _unpause.Count == 0)) return;
            var m = Mission;
            if (m == null) return;
            // zapas: chorazowie z naszego SetFormationBanner odpauzowani, gdy rozstawienie sie skonczylo (FinishDeployment robi to sam - drugi raz nie szkodzi)
            if (!_unpauseDone && (m.Mode != MissionMode.Deployment || m.CurrentTime - _deployedAt > 3f))
            {
                _unpauseDone = true;
                for (int i = 0; i < _unpause.Count; i++)
                {
                    var a = _unpause[i];
                    try { if (a != null && a.IsActive() && a.IsAIControlled) a.SetIsAIPaused(false); } catch { }
                }
                _unpause.Clear();
            }
            if (_all.Count == 0 || m.MissionEnded) return;
            _acc += dt;
            _accReview += dt;
            if (_acc < BannerCompanies.TickEvery) return;
            _acc = 0f;
            float now = m.CurrentTime;
            var c = Settings.Current;
            if (c == null || !c.BannerCompaniesEnabled) return;
            for (int i = 0; i < _all.Count; i++)
            {
                var co = _all[i];
                try { TickOne(co, now, c, m); }
                catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.Tick", e); }
            }
            bool review = _accReview >= BannerCompanies.ReviewEvery;
            try { Raise(c, review); }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.Raise", e); }
            if (review)
            {
                _accReview = 0f;
                try { Review(m); }
                catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.Review", e); }
            }
        }

        private void TickOne(Company co, float now, Settings c, Mission m)
        {
            // fale zalamania
            if (co.Queue != null && now >= co.NextWave)
            {
                int from = co.Wave * co.Chunk, to = Math.Min(co.Queue.Count, from + co.Chunk);
                bool rout = RoutHold.On(m);
                for (int i = from; i < to; i++)
                {
                    var a = co.Queue[i];
                    if (a == null || !a.IsActive() || !a.IsAIControlled || a.IsRunningAway) continue;
                    BannerCompanies.BrokenMen++;
                    if (co.LossBreak) BannerCompanies.LossBrokenMen++;
                    if (rout)
                    {
                        if (RoutHold.Break(a, MissionTeams.Flee)) BannerCompanies.BrokenRout++;
                        else BannerCompanies.BrokenRefused++;
                    }
                    else { a.SetMorale(0f); BannerCompanies.BrokenMorale++; }
                }
                co.Wave++;
                co.NextWave = now + BannerCompanies.WaveGap;
                if (co.Wave >= BannerCompanies.Waves || to >= co.Queue.Count) co.Queue = null;
            }
            // sztandar
            if (co.StandardUp)
            {
                var b = co.Bearer;
                if (b == null) co.StandardUp = false;
                else if (!b.IsActive()) StandardLost(co, co.LastPos, false, now, c);
                else if (b.Banner == null) StandardLost(co, b.Position, false, now, c);
                else if (!ReferenceEquals(b.Formation, co.F)) StandardLost(co, b.Position, true, now, c);
                else co.LastPos = b.Position;
            }
            // polowa skladu; 305c (BannerLossBreakIgnoresMorale): takze trzecia czesc strat (InRanks x 3 <= Peak x 2, pol szansy) - oba rzuty strat bez bramki
            // morale (ludzie widza, ile padlo, bez wzgledu na morale gry), wzor p bez zmian (mm i odpornosc tieru - weterani trzymaja sie dluzej)
            // 307b (klucz BannerLossBreakByShare; krytyk 307: 142a 0 zalaman na 23 rzuty, p 4-8% przy morale gry 69 mimo straty polowy): rzut przy kazdej
            // szostej czesci strat od 1/3 (1/3 pol szansy, 1/2, 2/3, 5/6 pelna), M z udzialu strat zamiast z morale gry - ludzie widza, ile padlo; odpornosc tieru zostaje
            if (c.BannerLossBreakByShare)
            {
                if (!co.Broken && co.Peak >= 4)
                {
                    int ranks = InRanks(co);
                    float lost = 1f - (float)ranks / co.Peak;
                    int k = (int)(lost * 6f + 0.0001f);
                    if (k > 5) k = 5;
                    if (k >= 2 && k > co.LossStep)
                    {
                        co.LossStep = k;
                        if (!co.ThirdDone) { co.ThirdDone = true; if (k == 2) BannerCompanies.Third++; }
                        if (k >= 3 && !co.HalfDone) { co.HalfDone = true; BannerCompanies.Half++; }
                        BannerCompanies.ShareStep[k]++;
                        Roll(co, k == 2 ? 0.5f : 1f, c, now, true, lost);
                    }
                }
            }
            else if (!co.HalfDone && !co.Broken && co.Peak >= 4)
            {
                int ranks = InRanks(co);
                bool loss = c.BannerLossBreakIgnoresMorale;
                if (ranks * 2 <= co.Peak)
                {
                    co.HalfDone = true;
                    co.ThirdDone = true;
                    BannerCompanies.Half++;
                    Roll(co, 1f, c, now, loss);
                }
                else if (loss && !co.ThirdDone && ranks * 3 <= co.Peak * 2)
                {
                    co.ThirdDone = true;
                    BannerCompanies.Third++;
                    Roll(co, 0.5f, c, now, true);
                }
            }
        }

        private static int InRanks(Company co)
        {
            int n = 0;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive()) continue;
                var cai = a.CommonAIComponent;
                if (cai != null && cai.IsPanicked) continue;
                n++;
            }
            return n;
        }

        /// <summary>Sztandar podniesiony: chorazy gry w formacji bez kompanii-wlasciciela - do kompanii bez sztandaru najblizej miejsca upadku (co 0.5 s);
        /// never = takze kompanie, ktore sztandaru nie mialy (chorazy z posilkow albo przeniesiony z innej formacji - co 3 s, bez premii morale).</summary>
        private void Raise(Settings c, bool never)
        {
            foreach (var kv in _forms)
            {
                var list = kv.Value;
                bool need = false;
                for (int i = 0; i < list.Count; i++)
                    if (!list[i].StandardUp && !list[i].Broken && (never || list[i].HadStandard)) { need = true; break; }
                if (!need) continue;
                var f = kv.Key;
                List<Agent> bearers = null;
                try { bearers = _bbl.GetFormationBannerBearers(f); } catch { bearers = null; }
                if (bearers == null) continue;
                for (int i = 0; i < bearers.Count; i++)
                {
                    var b = bearers[i];
                    if (b == null || !b.IsActive() || b.Banner == null || !ReferenceEquals(b.Formation, f) || _bearerOf.ContainsKey(b)) continue;
                    Company best = null;
                    float bd = float.MaxValue;
                    for (int j = 0; j < list.Count; j++)
                    {
                        var x = list[j];
                        if (!x.HadStandard || x.StandardUp || x.Broken) continue;
                        float d = x.FallPos.DistanceSquared(b.Position);
                        if (d < bd) { bd = d; best = x; }
                    }
                    if (best == null && never)
                        for (int j = 0; j < list.Count && best == null; j++)
                            if (!list[j].HadStandard && !list[j].Broken) best = list[j];
                    if (best == null) break;
                    bool again = best.HadStandard, afterFall = best.LastFall;   // 154: linia tylko dla kompanii, ktora sztandar juz miala
                    Put(b, best);
                    SetBearer(best, b);
                    if (best.BonusDue && best.LastFall)
                    {
                        best.BonusDue = false;
                        BannerCompanies.StdRaised++;
                        Shock(best, +0.5f * c.BannerShockStandardLost);
                    }
                    if (again) StdLine(best, afterFall ? "PODNIESIONY po upadku" : "znowu w gorze (poprzedni chorazy odszedl do innego szyku)", b, b.Position);
                }
            }
        }

        /// <summary>Co 3 s: posilki i przeniesieni do innej formacji - do kompanii swojej formacji z najmniejsza liczba w szeregu.</summary>
        private void Review(Mission m)
        {
            var agents = m.Agents;
            _lazy.Clear();
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (a == null || !a.IsHuman || !a.IsActive() || _skip.Contains(a)) continue;
                var f = a.Formation;
                if (f == null || (int)f.FormationIndex >= 8) continue;   // formacja wodza / ochrony - zostaje, gdzie byl
                Company old;
                bool known = _mem.TryGetValue(a, out old);
                if (known && ReferenceEquals(old.F, f)) continue;
                if (!known && Undead.Character(a.Character)) { _skip.Add(a); continue; }
                List<Company> list;
                if (!_forms.TryGetValue(f, out list) || list.Count == 0) { if (!_lazy.Contains(f)) _lazy.Add(f); continue; }
                Company to = null;
                int tn = int.MaxValue;
                for (int pass = 0; pass < 2 && to == null; pass++)
                    for (int j = 0; j < list.Count; j++)
                    {
                        var x = list[j];
                        if (pass == 0 && x.Broken) continue;
                        int n = InRanks(x);
                        if (n < tn) { tn = n; to = x; }
                    }
                if (to == null) continue;
                if (known)
                {
                    BannerCompanies.Moved++;
                    if (ReferenceEquals(old.Commander, a) && !old.CmdDown) { old.Commander = null; BannerCompanies.CmdMoved++; }
                }
                else BannerCompanies.Joined++;
                Put(a, to);
                to.Peak++;
            }
            for (int i = 0; i < _lazy.Count; i++)
            {
                var f = _lazy[i];
                if (f.CountOfUnits < 2 || !MissionTeams.Valid(f.Team) || _forms.ContainsKey(f)) continue;
                // przeniesieni z innych kompanii - wypisani z nich w Put (dowodca, ktory odszedl, przestaje dowodzic)
                foreach (var co in _all)
                    if (co.Commander != null && !co.CmdDown && ReferenceEquals(co.Commander.Formation, f) && !ReferenceEquals(co.F, f))
                    { co.Commander = null; BannerCompanies.CmdMoved++; }
                if (Split(f, m)) BannerCompanies.Lazy++;
            }
            _lazy.Clear();
        }

        // ------------------------------------------------------------ zdarzenia

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (affectedAgent == null || _mem.Count == 0) return;
            try
            {
                Company co;
                if (!affectedAgent.IsHuman || !_mem.TryGetValue(affectedAgent, out co)) return;
                var m = Mission;
                var c = Settings.Current;
                if (m == null || c == null || !c.BannerCompaniesEnabled || m.MissionEnded) return;
                float now = m.CurrentTime;
                if (co.StandardUp && ReferenceEquals(co.Bearer, affectedAgent)) StandardLost(co, affectedAgent.Position, false, now, c);
                if (ReferenceEquals(co.Commander, affectedAgent) && !co.CmdDown && (agentState == AgentState.Killed || agentState == AgentState.Unconscious))
                    CommanderLost(co, affectedAgent.Position, now, c);
            }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.OnAgentRemoved", e); }
        }

        private void StandardLost(Company co, Vec3 pos, bool moved, float now, Settings c)
        {
            co.StandardUp = false;
            var bearer = co.Bearer;
            if (co.Bearer != null) _bearerOf.Remove(co.Bearer);
            co.Bearer = null;
            co.FallPos = pos;
            StdLine(co, moved ? "odszedl z chorazym do innego szyku (bez okrzyku)" : co.Broken ? "PADL (kompania juz zlamana - bez okrzyku)" : "PADL", bearer, pos);
            if (moved) { co.LastFall = false; BannerCompanies.StdMoved++; return; }
            co.LastFall = true;
            co.BonusDue = true;
            if (co.Broken) return;                             // kompania juz zlamana (chorazy w panice upuszcza sztandar - BannerBearerLogic.OnAgentPanicked) - bez licznika
            BannerCompanies.StdFell++;
            Shock(co, -c.BannerShockStandardLost);
            if (PlayerSide(co)) Msg("The standard of " + NameOf(co) + " has fallen!", now, c, true);
            BannerCompanies.StandardFell(co.F, co.Index, pos);
            Roll(co, 1f, c, now);
        }

        private void CommanderLost(Company co, Vec3 pos, float now, Settings c)
        {
            co.CmdDown = true;
            BannerCompanies.CmdFell++;
            if (co.CmdPlayer) BannerCompanies.CmdFellPlayer++;
            if (co.CmdHero) BannerCompanies.CmdFellHero++;
            if (co.CmdGeneral) BannerCompanies.CmdFellGeneral++;
            if (co.Broken) return;
            float s = c.BannerShockCommanderLost * (co.CmdHero ? 1.5f : 1f) * (co.CmdGeneral ? 2f : 1f);
            Shock(co, -s);
            if (PlayerSide(co))
            {
                string name = NameOf(co);
                Msg(co.CmdHero && co.CmdName != null ? co.CmdName + " has fallen - " + name + " wavers!"
                                                     : "The sergeant of " + name + " has fallen - the company wavers!", now, c, true);
            }
            BannerCompanies.CommanderFell(co.F, co.Index, pos);
            Roll(co, co.CmdGeneral ? 2f : (co.CmdHero ? 1.5f : 1f), c, now);
        }

        /// <summary>154: linia zdarzenia sztandaru (kto, strona, co) - dowod do testu okrzyku "Save the banner!" i podnoszenia; najwyzej StdLinesMax na misje
        /// (reszta liczona w linii konca). Tylko log - stan kompanii bez zmian.
        /// 158: ta sama linia dla biegacza (role "biegacz" / "podniosl" / "upuscil"); szyk bez kompanii (co == null) - nazwa z klasy szyku f, kompania "-".</summary>
        private void StdLine(Company co, string what, Agent bearer, Vec3 pos, string role = "chorazy", Formation f = null)
        {
            if (_stdLines >= StdLinesMax) { _stdLinesCut++; return; }
            _stdLines++;
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                bool real = co != null;
                if (!real)
                {
                    if (f == null) return;
                    co = new Company { F = f, T = f.Team, Index = 0, Count = 1 };   // tylko do nazwy i strony - nie trafia do _all
                }
                string side = co.T != null && co.T.IsPlayerTeam ? "druzyna gracza" : PlayerSide(co) ? "sojusznik gracza" : "wrog";
                string who = "-";
                try { if (bearer != null) who = bearer.Name ?? "?"; } catch { who = "?"; }
                float t = Mission != null ? Mission.CurrentTime - _deployedAt : 0f;
                Log.Info("Choragwie (154): sztandar " + what + " | " + NameOf(co) + " (szyk " + ((int)co.F.FormationIndex + 1).ToString(inv) + ", kompania " + (real ? (co.Index + 1).ToString(inv) : "-")
                         + ") | strona " + side + " | " + role + " " + who + " | miejsce " + pos.x.ToString("0", inv) + "," + pos.y.ToString("0", inv)
                         + " | " + (_deployed ? t.ToString("0", inv) + " s po rozstawieniu" : "przed rozstawieniem") + ".");
            }
            catch (Exception e) { BannerCompanies.Stumble("BannerCompaniesMission.StdLine", e); }
        }

        /// <summary>Wstrzas morale kompanii przez model gry (CalculateMoraleChangeToCharacter - odpornosc wedlug tieru, bohater x1.5).</summary>
        private static void Shock(Company co, float s)
        {
            if (s == 0f) return;
            var mg = MissionGameModels.Current;
            var model = mg != null ? mg.BattleMoraleModel : null;
            float mag = Math.Abs(s), sign = s < 0f ? -1f : 1f;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive() || !a.IsAIControlled || a.IsMainAgent || a.IsRunningAway || a.CommonAIComponent == null) continue;
                float d = model != null ? model.CalculateMoraleChangeToCharacter(a, mag) : mag;
                a.ChangeMorale(sign * d);
                BannerCompanies.Shocked++;
            }
        }

        /// <summary>Rzut na zalamanie (raz na zdarzenie): p = szansa x F x M / R. 305c: loss = rzut po stratach (trzecia czesc / polowa skladu) przy kluczu
        /// BannerLossBreakIgnoresMorale - bez bramki morale ponizej 50 (M przy wysokim morale zostaje na dole 0.4).</summary>
        private void Roll(Company co, float factor, Settings c, float now, bool loss = false, float share = -1f)
        {
            if (co.Broken) return;
            float sum = 0f, tsum = 0f;
            int n = 0;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive() || !a.IsAIControlled || a.IsHero || a.IsRunningAway) continue;
                float mo = a.GetMorale();
                if (mo < 0f) continue;
                sum += mo; tsum += Tier(a); n++;
            }
            if (n == 0) return;
            float avg = sum / n, tier = tsum / n;
            int ix = TierIx(tier);
            co.TierIx = ix;
            if (avg >= BannerCompanies.RollBelow)
            {
                if (!loss) { BannerCompanies.RollsSkipped++; return; }
                BannerCompanies.LossOverGate++;
            }
            float r = 0.5f * tier + 1f;
            if (r < 1f) r = 1f;
            float mm = share >= 0f ? 2f * share : 2f - avg / 25f;   // 307b: przy rzucie wedlug udzialu strat M z udzialu, nie z morale gry
            if (mm < 0.4f) mm = 0.4f;
            if (mm > 1.6f) mm = 1.6f;
            float p = c.BannerBreakChance * factor * mm / r;
            if (p < 0f) p = 0f;
            if (p > BannerCompanies.PMax) p = BannerCompanies.PMax;
            BannerCompanies.Rolls++;
            BannerCompanies.TierRolls[ix]++;
            if (loss)
            {
                BannerCompanies.LossRolls++;
                BannerCompanies.LossMoraleSum += avg;
                BannerCompanies.LossPSum += p;
            }
            if (share >= 0f)
            {
                BannerCompanies.ShareRolls++;
                BannerCompanies.ShareLostSum += share;
                BannerCompanies.SharePSum += p;
            }
            if (MBRandom.RandomFloat < p)
            {
                if (share >= 0f) BannerCompanies.ShareBreaks++;
                if (loss) { co.LossBreak = true; BannerCompanies.LossBreaks++; }
                Break(co, ix, c, now);
            }
        }

        private void Break(Company co, int ix, Settings c, float now)
        {
            co.Broken = true;
            BannerCompanies.Breaks++;
            BannerCompanies.TierBreaks[ix]++;
            var q = new List<Agent>(co.Men.Count);
            Vec3 cen = Vec3.Zero;
            int nc = 0;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive()) continue;
                cen += a.Position; nc++;
                if (!a.IsAIControlled || a.IsHero || a.IsMainAgent || a.IsRunningAway) continue;   // bohaterowie i gracz nie lamia sie (jak BrokenMen)
                q.Add(a);
            }
            if (nc > 0) cen = cen * (1f / nc);
            if (q.Count > 0)
            {
                co.Queue = q;
                co.Wave = 0;
                co.Chunk = (q.Count + BannerCompanies.Waves - 1) / BannerCompanies.Waves;
                co.NextWave = now;
            }
            if (PlayerSide(co)) Msg(NameOf(co) + " breaks and runs!", now, c, true);
            else Msg("An enemy company breaks and runs!", now, c, false);
            if (c.BannerVictoryCheer)
            {
                var foe = NearestEnemy(co, cen);
                if (foe != null)
                {
                    try { BattleCries.Cheer(foe.Team, cen); BannerCompanies.Cheers++; }
                    catch (Exception e) { BannerCompanies.Stumble("BattleCries.Cheer", e); }
                }
                else BannerCompanies.CheersNoEnemy++;
            }
            BannerCompanies.CompanyBroke(co.F, co.Index, cen);
        }

        private Formation NearestEnemy(Company co, Vec3 cen)
        {
            var m = Mission;
            if (m == null || co.T == null) return null;
            Formation best = null;
            float bd = BannerCompanies.CheerRange * BannerCompanies.CheerRange;
            foreach (var t in m.Teams)
            {
                if (!MissionTeams.Enemies(t, co.T)) continue;
                foreach (var f in t.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits <= 0) continue;
                    float d = f.CachedAveragePosition.DistanceSquared(cen.AsVec2);
                    if (d < bd) { bd = d; best = f; }
                }
            }
            return best;
        }

        // ------------------------------------------------------------ komunikaty

        private bool PlayerSide(Company co)
        {
            var pt = Mission != null ? Mission.PlayerTeam : null;
            return pt != null && co.T != null && MissionTeams.Valid(pt) && co.T.Side == pt.Side;
        }

        private void Msg(string text, float now, Settings c, bool own)
        {
            if (!c.BannerCompanyMessages) return;
            if (now - _lastMsg < BannerCompanies.MsgGap) { BannerCompanies.MsgsLimited++; return; }
            _lastMsg = now;
            BannerCompanies.Msgs++;
            try { InformationManager.DisplayMessage(new InformationMessage(text, own ? Color.FromUint(0xFFE07A5Fu) : Color.FromUint(0xFF9ACD65u))); } catch { }
        }

        private static readonly string[] ClassNames = { "Infantry", "Archers", "Cavalry", "Horse Archers", "Skirmishers", "Heavy Infantry", "Light Cavalry", "Heavy Cavalry" };

        /// <summary>{BANNER} - company N: nazwa choragwi z przegladu armii (ArmyReview.NameOf - tylko druzyna gracza) albo klasa formacji i numer;
        /// sojusznicy z przedrostkiem "Allied ".</summary>
        private static string NameOf(Company co)
        {
            int ix = (int)co.F.FormationIndex;
            string banner = null;
            try { if (co.T != null && co.T.IsPlayerTeam) banner = ArmyReview.NameOf(ix); } catch { banner = null; }
            if (string.IsNullOrEmpty(banner))
            {
                int cls = (int)co.F.RepresentativeClass;
                if (cls < 0 || cls >= ClassNames.Length) cls = ix >= 0 && ix < ClassNames.Length ? ix : 0;
                banner = ClassNames[cls] + " (" + (ix + 1) + ")";
            }
            // recenzja: sojusznik (ta sama strona, nie druzyna gracza) - inaczej "Infantry (1) - company 2" brzmi jak wlasna formacja gracza
            if (co.T != null && !co.T.IsPlayerTeam) banner = "Allied " + banner;
            return co.Count > 1 ? banner + " - company " + (co.Index + 1) : banner;
        }

        // ------------------------------------------------------------ linie logu

        private void WriteStart(Mission m, Settings c)
        {
            float tp = 0f, te = 0f;
            int np = 0, ne = 0;
            var pt = m.PlayerTeam;
            foreach (var co in _all)
            {
                bool own = pt != null && co.T != null && MissionTeams.Valid(pt) && co.T.Side == pt.Side;
                for (int i = 0; i < co.Men.Count; i++)
                {
                    var a = co.Men[i];
                    if (a == null || a.IsHero) continue;
                    if (own) { tp += Tier(a); np++; } else { te += Tier(a); ne++; }
                }
            }
            int teams = 0;
            foreach (var t in m.Teams) if (MissionTeams.Valid(t)) teams++;
            var sb = new StringBuilder(900);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.Append("Choragwie (CH) start: misja ").Append(MissionTeams.Kind(m))
              .Append(" | klucze: kompanie ").Append(c.BannerCompaniesEnabled ? "TAK" : "NIE").Append(" (po ").Append(c.BannerCompanySize)
              .Append("), sztandary polowe ").Append(c.FieldStandards ? "TAK" : "NIE").Append(", zwrot sprzetu chorazego ").Append(c.StandardBearerKitSafe ? "TAK" : "NIE")
              .Append(" (A czynne ").Append(BannerKit.KitReady ? "TAK" : "NIE").Append(", wielu chorazych ").Append(BannerKit.MoreAllowed(c) && BannerKit.CountHooked ? "TAK" : "NIE")
              .Append(") | sztandary: ").Append(_deployNote).Append(", bohatera ").Append(BannerCompanies.StdHero).Append(" formacji, polowe ").Append(BannerCompanies.StdField)
              .Append(" (pominiete - bandyci i umarli ").Append(BannerCompanies.StdSkipped).Append(", brak campaign_banner_small ").Append(BannerCompanies.StdNoItem)
              .Append("), przeliczone ").Append(BannerCompanies.StdRecount).Append(", podniesiona liczba chorazych w formacjach ").Append(BannerKit.RaisedFormations)
              .Append(", bledy SetFormationBanner ").Append(BannerCompanies.StdCallFails).Append(", model sztandarow ").Append(ModelName())
              .Append(" | druzyn ").Append(teams).Append(", formacji ").Append(BannerCompanies.Formations).Append(", kompanii ").Append(BannerCompanies.Companies)
              .Append(" (srednio ").Append(BannerCompanies.Companies > 0 ? ((float)BannerCompanies.Members / BannerCompanies.Companies).ToString("0.0", inv) : "-")
              .Append(" ludzi, ").Append(BannerCompanies.MinSize).Append('-').Append(BannerCompanies.MaxSize).Append(')')
              .Append(" | chorazy w kompanii ").Append(BannerCompanies.BearersSet).Append(", bez chorazego ").Append(BannerCompanies.NoBearer)
              .Append(", nadmiarowi chorazowie ").Append(BannerCompanies.ExtraBearers)
              .Append(" | dowodcy: bohaterowie ").Append(BannerCompanies.CmdHero).Append(", gracz ").Append(BannerCompanies.CmdPlayer).Append(", sierzanci ").Append(BannerCompanies.CmdSergeant)
              .Append(" | sredni tier: strona gracza ").Append(np > 0 ? (tp / np).ToString("0.00", inv) : "-").Append(" (ludzi ").Append(np).Append("), wrog ")
              .Append(ne > 0 ? (te / ne).ToString("0.00", inv) : "-").Append(" (ludzi ").Append(ne).Append(')')
              .Append(" | kapitanowie-bohaterowie ze sztandarem ").Append(BannerCompanies.CapBanner).Append(" z ").Append(BannerCompanies.CapTotal).Append(" (wedlug kultury: ");
            int k = 0;
            foreach (var kv in _capCult)
            {
                if (k++ > 0) sb.Append(", ");
                sb.Append(kv.Key).Append(' ').Append(kv.Value[1]).Append('/').Append(kv.Value[0]);
            }
            if (k == 0) sb.Append('-');
            sb.Append(") | potkniecia ").Append(BannerCompanies.Stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static string ModelName()
        {
            try
            {
                var mg = MissionGameModels.Current;
                var bm = mg != null ? mg.BattleBannerBearersModel : null;
                return bm != null ? bm.GetType().Name : "-";
            }
            catch { return "?"; }
        }

        private void WriteEnd()
        {
            var sb = new StringBuilder(900);
            sb.Append("Choragwie (CH) koniec: ");
            if (!_deployed) sb.Append("rozstawienie NIE przyszlo (OnDeploymentFinished) - kompanie i sztandary polowe nie ustawione | ");
            sb.Append("kompanii ").Append(BannerCompanies.Companies).Append(" | sztandar padl ").Append(BannerCompanies.StdFell).Append(" (podniesiony ").Append(BannerCompanies.StdRaised)
              .Append(", odszedl z chorazym do innej formacji ").Append(BannerCompanies.StdMoved).Append(')')
              .Append(", dowodca padl ").Append(BannerCompanies.CmdFell).Append(" (bohater ").Append(BannerCompanies.CmdFellHero).Append(", wodz ").Append(BannerCompanies.CmdFellGeneral)
              .Append(", gracz ").Append(BannerCompanies.CmdFellPlayer).Append("; odszedl do innej formacji ").Append(BannerCompanies.CmdMoved).Append(')')
              .Append(", polowa skladu ").Append(BannerCompanies.Half).Append(", wstrzasnietych ludzi ").Append(BannerCompanies.Shocked)
              .Append(" | rzutow ").Append(BannerCompanies.Rolls).Append(" (pominiete przy morale od 50: ").Append(BannerCompanies.RollsSkipped).Append("), zalaman ").Append(BannerCompanies.Breaks).Append(" (");
            for (int t = 1; t <= 7; t++)
            {
                if (t > 1) sb.Append(", ");
                sb.Append('T').Append(t).Append(' ').Append(BannerCompanies.TierBreaks[t]).Append('/').Append(BannerCompanies.TierRolls[t]);
            }
            sb.Append("), zlamanych ludzi ").Append(BannerCompanies.BrokenMen).Append(" (RoutHold ").Append(BannerCompanies.BrokenRout).Append(", morale 0 ")
              .Append(BannerCompanies.BrokenMorale).Append(", odmowy ").Append(BannerCompanies.BrokenRefused).Append(')')
              .Append(" | okrzykow zwyciestwa ").Append(BannerCompanies.Cheers).Append(" (bez wroga w ").Append((int)BannerCompanies.CheerRange).Append(" m ")
              .Append(BannerCompanies.CheersNoEnemy).Append("), komunikatow ").Append(BannerCompanies.Msgs).Append(" (pominiete limitem ").Append(BannerCompanies.MsgsLimited).Append(')')
              .Append(" | posilkow dolaczonych ").Append(BannerCompanies.Joined).Append(", przeniesionych ").Append(BannerCompanies.Moved)
              .Append(", formacji podzielonych pozniej ").Append(BannerCompanies.Lazy)
              .Append(" | linie sztandaru (154) ").Append(_stdLines).Append(" (ponad limit ").Append(_stdLinesCut).Append(')');
            // 305c: rzuty po stratach bez bramki morale (klucz BannerLossBreakIgnoresMorale)
            var c305 = Settings.Current;
            var inv305 = System.Globalization.CultureInfo.InvariantCulture;
            int lr = BannerCompanies.LossRolls;
            sb.Append(" | straty bez bramki morale (305c) ").Append(c305 != null && c305.BannerLossBreakIgnoresMorale ? "TAK" : "NIE")
              .Append(": trzecia czesc strat ").Append(BannerCompanies.Third).Append(", rzutow strat ").Append(lr).Append(" (morale od 50: ").Append(BannerCompanies.LossOverGate)
              .Append(", sr. morale ").Append(lr > 0 ? (BannerCompanies.LossMoraleSum / lr).ToString("0.0", inv305) : "-")
              .Append(", sr. szansa ").Append(lr > 0 ? (BannerCompanies.LossPSum / lr).ToString("0.000", inv305) : "-")
              .Append("), zalaman ze strat ").Append(BannerCompanies.LossBreaks).Append(" (ludzi ").Append(BannerCompanies.LossBrokenMen).Append(')');
            // 307b: rzuty wedlug udzialu strat (klucz BannerLossBreakByShare)
            int sr = BannerCompanies.ShareRolls;
            sb.Append(" | straty wedlug udzialu (307b) ").Append(c305 != null && c305.BannerLossBreakByShare ? "TAK" : "NIE")
              .Append(": rzutow ").Append(sr).Append(" (przy 2/6 ").Append(BannerCompanies.ShareStep[2]).Append(", 3/6 ").Append(BannerCompanies.ShareStep[3])
              .Append(", 4/6 ").Append(BannerCompanies.ShareStep[4]).Append(", 5/6 ").Append(BannerCompanies.ShareStep[5])
              .Append("; sr. utracona czesc ").Append(sr > 0 ? (BannerCompanies.ShareLostSum / sr).ToString("0.00", inv305) : "-")
              .Append(", sr. szansa ").Append(sr > 0 ? (BannerCompanies.SharePSum / sr).ToString("0.000", inv305) : "-")
              .Append("), zalaman wedlug udzialu ").Append(BannerCompanies.ShareBreaks);
            sb.Append(" | potkniecia ").Append(BannerCompanies.Stumbles).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
