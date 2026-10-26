using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace Armoury
{
    /// <summary>
    /// 158 (26.10, pakiet A ze sledztwa 25.10 22:45 "investigate:sztandar"). Jeff 25.10 ok. 22:45: "potem mam odglos ktos krzyczy lapcie baner ale baner stoi
    /// pusty bo zabilem baner mena to ktos powinien podbiec i zlapac baner i isc dalej". BIEGACZ PO WLASNY SZTANDAR (klucze BannerPickupRange, BannerPickupYourFormations).
    ///
    /// Dowod [K] (dekompilacja TaleWorlds.MountAndBlade 1.4.8, scratchpad/banner - zgodna bajt w bajt z zainstalowanym DLL dla HumanAIComponent):
    ///  - gra podnosi sztandar tylko przy okazji: BannerBearerLogic co 3 s wyznacza "szukajacego" (ten sam szyk, 9 m) i tylko zdejmuje mu blokade podnoszenia
    ///    (DisablePickUpForAgentIfNeeded) - ruchu nie daje; podnosi zwykla petla HumanAIComponent.ItemPickupTick (co 2.5-3.5 s, brak celu blizej niz 20 m, nie w waznej
    ///    akcji walki, przedmiot w pudle ok. +-5 m) - przy zabitym chorazym w zwarciu prawie nikt tego nie spelnia (log 22:35: "sztandar padl 2 (podniesiony 0)");
    ///  - droga gry do podniesienia (HumanAIComponent.RequestMoveToItem + OnTick): MoveToUsableGameObject(item, null) - UseObjectMoving, claim
    ///    UsableMissionObject.OnAIMoveToUse (MovingAgent), SetScriptedPositionAndDirection(ramka uzytkownika, NoAttack); co klatke w zasiegu
    ///    GetBannerInteractionDistance x 3 i CanReachAndUseObject -> Agent.UseGameObject; animacja SpawnedItemEntity.OnTickParallel2 -> StopUsingGameObjectMT(sukces)
    ///    -> Agent.OnItemPickup (sztandar do ExtraWeaponSlot i do reki) -> Mission.TriggerOnItemPickUpEvent -> BannerBearerLogic.OnItemPickup (chorazy szyku);
    ///    porzucenie jak gra (RequestMoveToItem): StopUsingGameObject(false) - MoveToClear (OnMoveToStopped zdejmuje claim) i DisableScriptedMovement, bez AttachUnit
    ///    (detachment null);
    ///  - Mission.OnItemDrop / OnItemPickUp moga przyjsc z watku roboczego (OnTickParallel2 -> StopUsingGameObjectMT -> OnItemPickup) - tylko kolejka pod blokada,
    ///    praca w OnMissionTick (glowny watek);
    ///  - przejecie wrogiego sztandaru zablokowane w grze (BattleBannerBearersModel.IsInteractableFormationBanner: szyk sztandaru albo kapitan) - biegacz tylko
    ///    z szyku sztandaru (pakiet B osobno).
    /// Zasady (bitwa polowa z BannerCompaniesMission, po rozstawieniu):
    ///  - upadek = Mission.OnItemDrop sztandaru szyku (BannerBearerLogic.IsBannerItem + GetFormationFromBanner); kompania - kompania upuszczajacego (_mem),
    ///    inaczej najblizsza kompania tego szyku bez sztandaru (FallPos do 10 m), inaczej brak (szyk bez kompanii - wolno);
    ///  - co 0.5 s, gdy sztandar lezy od 1 s (osiada po upadku): biegacz = najblizszy czlowiek AI szyku sztandaru w promieniu R (BannerPickupRange, najwyzej 40 m;
    ///    szyk gracza - HorsePursuit.PlayerForm, jedna definicja z 148/149 - tylko przy BannerPickupYourFormations i najwyzej 12 m): nie gracz, nie bohater (149),
    ///    Banner == null, model gry CanAgentPickUpAnyBanner (CanBeAssignedForScriptedMovement - odsiewa pozycje skryptowe 143/148/149, odlaczonych, uciekajacych;
    ///    bez IsPanicked i waznej akcji walki), nie PANIKA (SurvivalMode 139), nie w odwrocie rannego 143, nie w poscigu 148, nie w pozie zadyszki 144, nie umarly,
    ///    nie w wodzie, CanUseObject(sztandar), nie probowal juz tego upadku;
    ///  - AI walczy o zycie: wrogow (czynnych, nieuciekajacych ludzi) w 6 m od sztandaru najwyzej swoich + 1; kompania zlamana i szyk z rozkazem Retreat / FallBack
    ///    nikogo nie wysylaja; sztandar z claimem gry (HasAIMovingTo) albo w rekach podnoszacego (HasUser) - zostawiamy grze;
    ///  - rozkaz: HumanAIComponent.MoveToUsableGameObject(sztandar, null, NoAttack); okrzyk "Save the banner!" (BattleSound ERunner) tylko przy PIERWSZYM biegaczu
    ///    upadku - przy samym upadku zostaje rog zbiorki i okrzyk wroga;
    ///  - co 0.25 s: w zasiegu jak gra (GetBannerInteractionDistance x 3, CanReachAndUseObject) -> UseGameObject; rezygnacja (StopUsingGameObject(false)) po 8 s,
    ///    po 2 s bez zblizenia o 0.5 m, przy panice, odwrocie rannego 143, zmianie szyku, utracie ruchu (ktos zdjal GoToPosition / claim), rozkazie odwrotu, zlamaniu
    ///    kompanii, wrogach przy sztandarze, koncu misji; poscig 148 przejal jezdzca - tylko MoveToClear (pozycja skryptowa jest juz poscigu); potem nastepny
    ///    kandydat, najwyzej RunMaxTries biegaczy na upadek;
    ///  - po podniesieniu dalej droga gry: BannerBearerLogic.OnItemPickup, nasz Raise (kompania, +S/2 morale), RBM prowadzi chorazego (AgentAi OnTick);
    ///  - pomiar DropOnWeaponChange (sztandar ma flage - SandBoxCore horses_and_others.xml, HeldInOffHand, slot ExtraWeaponSlot): nowy chorazy (podniosl w misji),
    ///    ktory upuscil sztandar zywy i nie w panice = "upuszczony ponownie przy zmianie broni" (linia z bronia w rekach). Gra czysci sloty 1-3 tylko chorazym
    ///    z rozstawienia (CreateBannerEquipmentForAgent) - tego NIE powtarzamy (decyzja Jeffa po pomiarze).
    /// Recenzja 158: biegacz w odwrot rannego 143 zdjety juz w RoutKeeper.Withdraw (BeforeWithdraw); po pierwszym takim ponownym upuszczeniu szyk sztandaru
    /// do konca misji bez biegaczy (limit na szyk, nie na czlowieka), a ex-chorazy dostaje z powrotem automatyczny wybor celu (RBM go nie przywraca);
    /// "podniosl inny", gdy ktos zaczal podnosic w trakcie biegu; srednia predkosc osobno dla podniesionych i porzuconych po 8 s.
    /// Bez Harmony. Linie: "Choragwie (154)" (wspolny limit, role biegacz / podniosl / upuscil) i "Sztandar (158)" raz na misje. Wyjatek na upadek liczony,
    /// po RunMaxFails upadek wypada - bez globalnego wylacznika.
    /// </summary>
    internal static partial class BannerCompanies
    {
        internal const float RunPlayerRange = 12f, RunMaxRange = 40f, RunCrowdRadius = 6f, RunSettle = 1f, RunPickEvery = 0.5f, RunStepEvery = 0.25f,
                             RunGiveUpAfter = 8f, RunStallAfter = 2f, RunProgress = 0.5f, RunCompanyNear = 10f;
        internal const int RunMaxTries = 5, RunMaxFails = 5, RunQueueMax = 256;

        /// <summary>158: wyslany pierwszy biegacz po upadly sztandar kompanii - okrzyk "Save the banner!" (BattleSound). Wzor BannerCompanies.Events.cs (plik integratora
        /// bez zmian).</summary>
        internal static void RunnerSent(Formation f, int company, Vec3 pos)
        {
            if (f == null) return;
            try { BattleSound.OnBannerRunner(f, company, pos); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnBannerRunner", e); }
        }

        /// <summary>Recenzja 158: biezaca misja biegaczy (glowny watek; ustawiana w RunStart, zdejmowana w RunUnsub) - tylko dla BeforeWithdraw.</summary>
        internal static BannerCompaniesMission RunLive;

        /// <summary>Recenzja 158: RoutKeeper.Withdraw (143, glowny watek), PRZED pierwszym ruchem 143 - biegacz po sztandar zdjety TERAZ (claim, UseObjectMoving
        /// i nasza pozycja NoAttack: StopUsingGameObject(false)); wzor HeroCaution / HorsePursuit.BeforeWithdraw. Dotad zdejmowal go dopiero Step (co 0.25 s)
        /// tym samym StopUsingGameObject, a Agent.StopUsingGameObjectAux (dekompilacja 1.4.8) wola DisableScriptedMovement bezwarunkowo - kasowal postoj 143
        /// postawiony w Arrive (143 przywracal go dopiero po RefreshEvery 4 s), a przy natywnym odwrocie nasza pozycja NoAttack trzymala rannego do 0.25 s.</summary>
        internal static void BeforeWithdraw(Agent a)
        {
            var k = RunLive;
            if (k == null || a == null || !ReferenceEquals(k.Mission, a.Mission)) return;
            k.RunnerWithdraw(a);
        }
    }

    internal sealed partial class BannerCompaniesMission
    {
        /// <summary>Lezacy sztandar szyku (od upadku do podniesienia / znikniecia / konca misji).</summary>
        private sealed class Fallen
        {
            public SpawnedItemEntity Sie;
            public Formation F;
            public Company Co;
            public Agent Dropper, Runner;
            public Vec3 Pos;
            public float At, RunAt, LastBetter, Best, StartDist, Dist;
            public int Sent, Uses, Fails, LastWhy = -1;
            public bool Cried, GameSeen, Closed, UserCut;   // UserCut (recenzja 158): ostatni bieg przerwal ktos inny, ktory zaczal podnosic (gracz / gra)
            public readonly HashSet<Agent> Tried = new HashSet<Agent>();
        }

        private struct ItemEv { public Agent A; public SpawnedItemEntity Sie; public bool Pick; }

        // powody rezygnacji biegacza
        private const int GTime = 0, GStall = 1, GPanic = 2, GWound = 3, GPursuit = 4, GForm = 5, GRetreat = 6, GBroken = 7, GCrowd = 8,
                          GLost = 9, GUseFail = 10, GGone = 11, GOther = 12, GDown = 13, GEnd = 14, GFail = 15, NG = 16;
        private static readonly string[] GNames = { "czas 8 s", "brak postepu 2 s", "panika (139)", "rana (143)", "poscig (148)", "inny szyk",
            "rozkaz odwrotu", "kompania zlamana", "wrogowie przy sztandarze", "ruch zdjety cudza reka", "podnoszenie przerwane", "sztandar zniknal",
            "podniosl inny", "padl / omdlal", "koniec misji", "potkniecia" };
        // dlaczego upadek skonczyl sie bez biegacza (ostatni powod)
        private const int SNotYet = 0, SOff = 1, SPlayer = 2, SBroken = 3, SRetreat = 4, SGame = 5, SCrowd = 6, SNoOne = 7, SLimit = 8, SEmpty = 9, SFail = 10,
                          SRedrop = 11, NS = 12;
        private static readonly string[] SNames = { "zanim szukano (ponizej 1 s / przed rozstawieniem)", "klucz zasieg 0", "szyk gracza (klucz NIE)", "kompania zlamana",
            "rozkaz odwrotu", "gra juz biegnie / ktos podnosi", "wrogowie przy sztandarze", "nikogo w zasiegu", "limit prob", "szyk pusty", "potkniecia",
            "po ponownym upuszczeniu w szyku" };

        private readonly List<Fallen> _fallen = new List<Fallen>();
        private readonly Dictionary<Agent, Fallen> _runners = new Dictionary<Agent, Fallen>();
        private readonly Dictionary<Agent, float> _picked = new Dictionary<Agent, float>();   // podniosl sztandar szyku w tej misji (czas) - pomiar ponownego upuszczenia
        private readonly HashSet<Agent> _pickedByRunner = new HashSet<Agent>();
        private readonly HashSet<Agent> _noRun = new HashSet<Agent>();         // upuscil podniesiony sztandar zywy (zmiana broni) - nie biega juz w tej misji (bez petli)
        // recenzja 158: szyk, w ktorym nowy chorazy upuscil sztandar zywy - bez biegaczy do konca misji (_noRun nie ograniczal lancucha roznych ludzi)
        private readonly HashSet<Formation> _noRunForm = new HashSet<Formation>();
        private readonly object _runQLock = new object();
        private readonly List<ItemEv> _runQ = new List<ItemEv>();      // pod _runQLock (dowolny watek)
        private readonly List<ItemEv> _runQ2 = new List<ItemEv>();     // glowny watek
        private readonly MBList<Agent> _near = new MBList<Agent>(), _near2 = new MBList<Agent>();
        private static readonly Predicate<Fallen> FallenClosed = x => x.Closed;
        private Action<Agent, SpawnedItemEntity> _onDrop, _onPick;
        private bool _runSub, _runLineDone;
        private float _runStepAcc, _runPickAcc;
        // liczniki misji (linia "Sztandar (158)"); _rEvFail - watki zdarzen (Interlocked), _rQueueCut - pod _runQLock
        private int _rDrops, _rNoForm, _rSent, _rFallsRun, _rCries, _rRaised, _rGame, _rGamePlayer, _rGameRun, _rGameSeen, _rVanished, _rLeft;
        private int _rRedrop, _rRedropRunner, _rRedropPanic, _rPickOther, _rStumbles, _rSpeedN, _rQueueCut, _rEvFail;
        private int _rSpeedTN, _rTargetBack;
        private float _rRaisedFall, _rRaisedRun, _rSpeedSum, _rSpeedTSum;   // predkosc: _rSpeed* - podniesieni, _rSpeedT* - porzuceni po 8 s (recenzja 158)
        private readonly int[] _rGiveUp = new int[NG], _rNoRunner = new int[NS];

        // ------------------------------------------------------------ zdarzenia (dowolny watek - tylko kolejka)

        private void RunStart(Mission m)
        {
            if (!_field || m == null || _runSub) return;
            _onDrop = OnDropEv;
            _onPick = OnPickEv;
            m.OnItemDrop += _onDrop;
            m.OnItemPickUp += _onPick;
            _runSub = true;
            BannerCompanies.RunLive = this;   // recenzja 158: BeforeWithdraw (143)
        }

        private void RunUnsub()
        {
            if (ReferenceEquals(BannerCompanies.RunLive, this)) BannerCompanies.RunLive = null;
            if (!_runSub) return;
            _runSub = false;
            try
            {
                var m = Mission;
                if (m != null) { m.OnItemDrop -= _onDrop; m.OnItemPickUp -= _onPick; }
            }
            catch { }
        }

        private void OnDropEv(Agent a, SpawnedItemEntity sie) { Enq(a, sie, false); }

        private void OnPickEv(Agent a, SpawnedItemEntity sie) { Enq(a, sie, true); }

        private void Enq(Agent a, SpawnedItemEntity sie, bool pick)
        {
            try
            {
                if (sie == null || !BannerBearerLogic.IsBannerItem(sie.WeaponCopy.Item)) return;
                lock (_runQLock)
                {
                    if (_runQ.Count >= BannerCompanies.RunQueueMax) { _rQueueCut++; return; }
                    _runQ.Add(new ItemEv { A = a, Sie = sie, Pick = pick });
                }
            }
            catch { Interlocked.Increment(ref _rEvFail); }
        }

        // ------------------------------------------------------------ takt (glowny watek)

        private void RunTick(float dt)
        {
            var m = Mission;
            if (m == null) return;
            Drain(m);
            if (_fallen.Count == 0) return;
            float now = m.CurrentTime;
            if (m.MissionEnded) { ReleaseAll(GEnd, now); return; }
            _runStepAcc += dt;
            _runPickAcc += dt;
            bool step = _runStepAcc >= BannerCompanies.RunStepEvery;
            bool pick = _runPickAcc >= BannerCompanies.RunPickEvery;
            if (step) _runStepAcc = 0f;
            if (pick) _runPickAcc = 0f;
            if (!step && !pick) return;
            var c = Settings.Current;
            for (int i = 0; i < _fallen.Count; i++)
            {
                var fl = _fallen[i];
                if (fl.Closed) continue;
                try
                {
                    var sie = fl.Sie;
                    if (sie == null || sie.IsRemoved || !sie.GameEntity.IsValid)
                    {
                        // gra kasuje lezacy przedmiot po 180 s (SpawnedItemEntity.Initialize: HasLifeTime) albo przy czyszczeniu sceny
                        _rVanished++;
                        if (fl.Runner != null) GiveUp(fl, GGone, now, true);
                        Close(fl);
                        continue;
                    }
                    if (fl.Runner != null) { if (step) Step(fl, now, m); }
                    else if (pick) Pick(fl, now, c, m);
                }
                catch (Exception e)
                {
                    _rStumbles++;
                    BannerCompanies.Stumble("BannerCompaniesMission.Biegacz", e);
                    if (++fl.Fails >= BannerCompanies.RunMaxFails)
                    {
                        try { if (fl.Runner != null) GiveUp(fl, GFail, now, true); } catch { fl.Runner = null; }
                        fl.LastWhy = SFail;
                        Close(fl);
                    }
                }
            }
            _fallen.RemoveAll(FallenClosed);
        }

        private void Drain(Mission m)
        {
            lock (_runQLock)
            {
                if (_runQ.Count == 0) return;
                _runQ2.AddRange(_runQ);
                _runQ.Clear();
            }
            float now = m.CurrentTime;
            for (int i = 0; i < _runQ2.Count; i++)
            {
                var ev = _runQ2[i];
                try
                {
                    if (ev.Pick) Picked(ev.A, ev.Sie, now);
                    else Dropped(ev.A, ev.Sie, now);
                }
                catch (Exception e) { _rStumbles++; BannerCompanies.Stumble("BannerCompaniesMission.SztandarZdarzenie", e); }
            }
            _runQ2.Clear();
            _fallen.RemoveAll(FallenClosed);
        }

        /// <summary>Sztandar upuszczony: pomiar ponownego upuszczenia (nowy chorazy) i wpis lezacego sztandaru szyku.</summary>
        private void Dropped(Agent a, SpawnedItemEntity sie, float now)
        {
            if (sie == null) return;
            Formation f = null;
            try { f = _bbl != null ? _bbl.GetFormationFromBanner(sie) : null; } catch { f = null; }
            float pickedAt;
            if (a != null && _picked.TryGetValue(a, out pickedAt))
            {
                _picked.Remove(a);
                bool runner = _pickedByRunner.Remove(a);
                if (a.IsActive() && a.Health > 0f)
                {
                    if (a.IsRunningAway || SurvivalMode.InPanic(a)) _rRedropPanic++;
                    else
                    {
                        // DropOnWeaponChange: zywy, nie w panice - sztandar zszedl z reki przy zmianie broni (tarcza / bron dwureczna) albo inna droga silnika
                        _rRedrop++;
                        if (runner) _rRedropRunner++;
                        _noRun.Add(a);   // ten sam czlowiek stoi najblizej - bez tego podnosilby i upuszczal w kolko (kazdy upadek to wstrzas i rzut 154)
                        // recenzja 158: limit na szyk, nie na czlowieka - nastepny biegacz z tym samym ekwipunkiem podnioslby i upuscil znowu, a kazde
                        // upuszczenie to w P3 nowy upadek (wstrzas -S, rzut, komunikat) z nowym limitem RunMaxTries; od teraz do tego szyku nikt nie biega
                        if (f != null) _noRunForm.Add(f);
                        // recenzja 158: RBM (AgentAi OnTick, chorazy bez wroga w 5 m) wylacza automatyczny wybor celu i celuje w pierwszego z szyku, a wlacza
                        // go z powrotem tylko u chorazego z wrogiem blisko; po upuszczeniu RBM go juz nie rusza (IsBannerBearer = sztandar w rece), gra tez nie
                        // (SetAutomaticTargetSelection wola tylko RBM i nasz 148). Wzor HorsePursuit przy zwolnieniu jezdzca; jezdziec w poscigu 148 - cel jego.
                        if (a.IsAIControlled && !HorsePursuit.RidingAnyThread(a) && !HorsePursuit.HuntAnyThread(a))   // 204: cel czlowieka 204 - jego
                        {
                            try { a.SetAutomaticTargetSelection(true); a.InvalidateTargetAgent(); _rTargetBack++; }
                            catch (Exception e) { _rStumbles++; BannerCompanies.Stumble("BannerCompaniesMission.CelPoUpuszczeniu", e); }
                        }
                        Company rc;
                        if (!_mem.TryGetValue(a, out rc) || (f != null && !ReferenceEquals(rc.F, f))) rc = null;
                        StdLine(rc, "UPUSZCZONY PONOWNIE (158: zywy, nie w panice - zmiana broni?) po " + (now - pickedAt).ToString("0.0", CultureInfo.InvariantCulture)
                                    + " s od podniesienia" + (runner ? " przez biegacza" : "") + ", w rekach teraz " + Hands(a), a, a.Position, "upuscil", f ?? a.Formation);
                    }
                }
            }
            if (f == null) { _rNoForm++; return; }
            for (int i = 0; i < _fallen.Count; i++)
                if (ReferenceEquals(_fallen[i].Sie, sie) && !_fallen[i].Closed) return;   // juz lezy (drugie zdarzenie tego samego przedmiotu)
            var fl = new Fallen { Sie = sie, F = f, Dropper = a, At = now };
            try { fl.Pos = sie.GameEntity.GlobalPosition; } catch { fl.Pos = a != null ? a.Position : Vec3.Zero; }
            fl.Co = CompanyOf(fl);
            _fallen.Add(fl);
            _rDrops++;
        }

        /// <summary>Sztandar podniesiony: nasz biegacz (sukces) albo ktos inny (gra / gracz) - wtedy biegacz, jesli byl, odchodzi.</summary>
        private void Picked(Agent a, SpawnedItemEntity sie, float now)
        {
            if (a != null && a.IsAIControlled) _picked[a] = now;   // pomiar ponownego upuszczenia - tylko AI (gracz zmienia bron sam)
            Fallen fl = null;
            for (int i = 0; i < _fallen.Count; i++)
                if (ReferenceEquals(_fallen[i].Sie, sie) && !_fallen[i].Closed) { fl = _fallen[i]; break; }
            if (fl == null) { _rPickOther++; return; }
            var inv = CultureInfo.InvariantCulture;
            if (a != null && ReferenceEquals(fl.Runner, a))
            {
                _rRaised++;
                _rRaisedFall += now - fl.At;
                _rRaisedRun += now - fl.RunAt;
                _pickedByRunner.Add(a);
                NoteSpeed(fl, a, now, false);
                _runners.Remove(a);
                fl.Runner = null;
                Line(fl, "podniesiony przez biegacza (158) po " + (now - fl.At).ToString("0.0", inv) + " s od upadku (" + (now - fl.RunAt).ToString("0.0", inv) + " s biegu, proba "
                         + fl.Sent + ")", a, "podniosl");
                Close(fl);
                return;
            }
            _rGame++;
            bool player = a != null && a.IsMainAgent;
            if (player) _rGamePlayer++;
            // UserCut (recenzja 158): Step zobaczyl cudzego podnoszacego wczesniej niz to zdarzenie (OnUse zdejmuje claim od razu, animacja trwa dluzej)
            if (fl.Runner != null || fl.UserCut) _rGameRun++;
            if (fl.Runner != null) GiveUp(fl, GOther, now, true);
            Line(fl, "podniesiony przez " + (player ? "gracza" : "gre") + " (158: nie nasz biegacz) po " + (now - fl.At).ToString("0.0", inv) + " s od upadku", a, "podniosl");
            Close(fl);
        }

        // ------------------------------------------------------------ wybor biegacza (co 0.5 s)

        private void Pick(Fallen fl, float now, Settings c, Mission m)
        {
            float range = c != null ? c.BannerPickupRange : 0f;
            if (range > BannerCompanies.RunMaxRange) range = BannerCompanies.RunMaxRange;
            if (!(range > 0f)) { fl.LastWhy = SOff; return; }
            if (!_deployed || m.Mode == MissionMode.Deployment || now - fl.At < BannerCompanies.RunSettle) { fl.LastWhy = SNotYet; return; }
            var f = fl.F;
            if (f == null || f.CountOfUnits <= 0 || !MissionTeams.Valid(f.Team)) { fl.LastWhy = SEmpty; return; }
            if (fl.Co == null) fl.Co = CompanyOf(fl);
            if (fl.Co != null && fl.Co.Broken) { fl.LastWhy = SBroken; return; }
            if (Retreating(f)) { fl.LastWhy = SRetreat; return; }
            var sie = fl.Sie;
            if (sie.HasUser || sie.HasAIMovingTo)
            {
                fl.LastWhy = SGame;
                if (!fl.GameSeen) { fl.GameSeen = true; _rGameSeen++; }
                return;
            }
            if (HorsePursuit.PlayerForm(f))
            {
                if (c == null || !c.BannerPickupYourFormations) { fl.LastWhy = SPlayer; return; }
                if (range > BannerCompanies.RunPlayerRange) range = BannerCompanies.RunPlayerRange;
            }
            if (_noRunForm.Contains(f)) { fl.LastWhy = SRedrop; return; }   // recenzja 158: nowy chorazy tego szyku upuscil juz sztandar zywy
            if (fl.Sent >= BannerCompanies.RunMaxTries) { fl.LastWhy = SLimit; return; }
            try { fl.Pos = sie.GameEntity.GlobalPosition; } catch { }
            if (Crowded(fl.Pos, f.Team, m)) { fl.LastWhy = SCrowd; return; }
            var mg = MissionGameModels.Current;
            var model = mg != null ? mg.BattleBannerBearersModel : null;
            Agent best = null;
            float bd = float.MaxValue;
            var p2 = fl.Pos.AsVec2;
            m.GetNearbyAllyAgents(p2, range, f.Team, _near);
            for (int i = 0; i < _near.Count; i++)
            {
                var a = _near[i];
                if (!Fit(a, fl, model)) continue;
                float d = a.Position.AsVec2.Distance(p2);
                if (d < bd) { bd = d; best = a; }
            }
            _near.Clear();
            if (best == null) { fl.LastWhy = SNoOne; return; }
            Send(fl, best, bd, now);
        }

        /// <summary>Kandydat na biegacza (patrz opis klasy BannerCompanies.Pickup).</summary>
        private bool Fit(Agent a, Fallen fl, BattleBannerBearersModel model)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || !a.IsAIControlled || a.IsMainAgent || a.IsHero) return false;
            if (!ReferenceEquals(a.Formation, fl.F) || a.Banner != null) return false;
            if (_runners.ContainsKey(a) || fl.Tried.Contains(a) || _noRun.Contains(a)) return false;
            if (a.HumanAIComponent == null || !a.CanBeAssignedForScriptedMovement()) return false;
            if (model != null && !model.CanAgentPickUpAnyBanner(a)) return false;   // gra: bez sztandaru, bez IsPanicked, nie w waznej akcji walki
            if (SurvivalMode.InPanic(a) || Withdrawing(a) || HorsePursuit.RidingAnyThread(a) || HorsePursuit.HuntAnyThread(a) || WindedShow.Posing(a)) return false;   // 204: pod sterowaniem 204
            if (Undead.Character(a.Character) || a.IsInWater()) return false;
            return a.CanUseObject(fl.Sie);   // IsInteractableFormationBanner: szyk sztandaru albo kapitan
        }

        private void Send(Fallen fl, Agent a, float d, float now)
        {
            a.HumanAIComponent.MoveToUsableGameObject(fl.Sie, null, Agent.AIScriptedFrameFlags.NoAttack);
            fl.Runner = a;
            fl.RunAt = now;
            fl.LastBetter = now;
            fl.Best = d;
            fl.StartDist = d;
            fl.Dist = d;
            fl.Uses = 0;
            fl.UserCut = false;
            fl.Sent++;
            fl.Tried.Add(a);
            _runners[a] = fl;
            _rSent++;
            if (fl.Sent == 1) _rFallsRun++;
            var inv = CultureInfo.InvariantCulture;
            Line(fl, "BIEGACZ (158) wyslany z " + d.ToString("0.0", inv) + " m, " + (now - fl.At).ToString("0.0", inv) + " s po upadku (proba " + fl.Sent + ")", a, "biegacz");
            if (!fl.Cried)
            {
                fl.Cried = true;
                _rCries++;
                BannerCompanies.RunnerSent(fl.F, fl.Co != null ? fl.Co.Index : 0, fl.Pos);
            }
        }

        // ------------------------------------------------------------ biegacz w drodze (co 0.25 s)

        private void Step(Fallen fl, float now, Mission m)
        {
            var a = fl.Runner;
            var sie = fl.Sie;
            if (!a.IsActive()) { GiveUp(fl, GDown, now, false); return; }
            if (a.IsUsingGameObject && ReferenceEquals(a.CurrentlyUsedGameObject, sie)) return;   // animacja podnoszenia - koniec da OnItemPickUp albo przerwanie
            // juz trzyma sztandar (przy wyslaniu Banner == null): podniesienie skonczylo sie w tej klatce na watku roboczym, a zdarzenie czeka w kolejce
            if (a.Banner != null) return;
            // odwrot rannego 143 przed panika: jego natywny Agent.Retreat tez ustawia IsRunningAway - powod "rana", nie "panika". Recenzja 158: zwykle zdjety juz
            // w RoutKeeper.Withdraw (BeforeWithdraw, przed pierwszym ruchem 143); tu tylko zapas po wyjatku - bez stop: StopUsingGameObject wola
            // DisableScriptedMovement i zdjalby postoj 143 (Arrive), wiec tylko MoveToClear (claim i UseObjectMoving)
            if (Withdrawing(a)) { GiveUp(fl, GWound, now, false); return; }
            if (SurvivalMode.InPanic(a) || a.IsRunningAway) { GiveUp(fl, GPanic, now, true); return; }
            if (HorsePursuit.RidingAnyThread(a) || HorsePursuit.HuntAnyThread(a)) { GiveUp(fl, GPursuit, now, false); return; }   // 204: jak poscig 148
            if (!ReferenceEquals(a.Formation, fl.F)) { GiveUp(fl, GForm, now, true); return; }
            // claim i stan AI (flagi GoToPosition NIE sprawdzamy - czy silnik zdejmuje ja po dojsciu, nie wiadomo z kodu zarzadzanego; ruch zdjety cudza reka
            // bez zmiany szyku konczy brak postepu 2 s)
            // recenzja 158: ktos inny zaczal podnosic (gracz albo gra) - UsableMissionObject.OnUse zdjal nam claim (StopUsingGameObject ruchomego) od razu,
            // a animacja trwa dluzej niz takt; to "podniosl inny", nie "ruch zdjety cudza reka" (ruchu i pozycji juz nie ma - bez stop)
            if (sie.HasUser && !ReferenceEquals(sie.UserAgent, a)) { fl.UserCut = true; GiveUp(fl, GOther, now, false); return; }
            bool mine = ReferenceEquals(sie.MovingAgent, a) && Moving(a, sie);
            if (!mine) { GiveUp(fl, fl.Uses > 0 ? GUseFail : GLost, now, true); return; }
            if (Retreating(fl.F)) { GiveUp(fl, GRetreat, now, true); return; }
            if (fl.Co != null && fl.Co.Broken) { GiveUp(fl, GBroken, now, true); return; }
            try { fl.Pos = sie.GameEntity.GlobalPosition; } catch { }
            if (Crowded(fl.Pos, fl.F.Team, m)) { GiveUp(fl, GCrowd, now, true); return; }
            float d = a.Position.AsVec2.Distance(fl.Pos.AsVec2);
            fl.Dist = d;
            if (d < fl.Best - BannerCompanies.RunProgress) { fl.Best = d; fl.LastBetter = now; }
            // podejscie jak w grze (HumanAIComponent.OnTick dla _itemToPickUp): zasieg GetBannerInteractionDistance x 3, CanReachAndUseObject -> UseGameObject
            var mg = MissionGameModels.Current;
            var model = mg != null ? mg.BattleBannerBearersModel : null;
            float num = (model != null ? model.GetBannerInteractionDistance(a) : 1.5f) * 3f;
            var uf = sie.GetUserFrameForAgent(a);
            float dsq = uf.Origin.DistanceSquaredWithLimit(a.Position, num * num + 1E-05f);
            if (a.CanReachAndUseObject(sie, dsq))
            {
                a.UseGameObject(sie);
                fl.Uses++;
                return;
            }
            if (now - fl.RunAt > BannerCompanies.RunGiveUpAfter) { GiveUp(fl, GTime, now, true); return; }
            if (now - fl.LastBetter > BannerCompanies.RunStallAfter) GiveUp(fl, GStall, now, true);
        }

        /// <summary>Czlowiek idzie do tego przedmiotu wedlug wlasnego stanu AI (UseObjectMoving i obiekt zainteresowania).</summary>
        private static bool Moving(Agent a, SpawnedItemEntity sie)
        {
            var h = a.HumanAIComponent;
            return h != null && a.AIMoveToGameObjectIsEnabled() && ReferenceEquals(h.GetCurrentlyMovingGameObject(), sie);
        }

        /// <summary>Biegacz odchodzi. stop - jak gra (RequestMoveToItem): StopUsingGameObject(false), gdy wciaz idzie albo podnosi; bez stop (poscig 148 przejal) -
        /// tylko MoveToClear (claim i flaga ruchu zdjete, pozycja skryptowa zostaje u poscigu).</summary>
        private void GiveUp(Fallen fl, int why, float now, bool stop)
        {
            var a = fl.Runner;
            fl.Runner = null;
            if (a != null) _runners.Remove(a);
            _rGiveUp[why]++;
            if (why == GTime) NoteSpeed(fl, a, now, true);   // recenzja 158: tempo biegu mowia tylko podniesienia i limit 8 s (inne powody - tlok, przeszkoda)
            if (a != null && a.IsActive())
            {
                try
                {
                    var sie = fl.Sie;
                    bool use = a.IsUsingGameObject && ReferenceEquals(a.CurrentlyUsedGameObject, sie);
                    bool mov = Moving(a, sie);
                    if (stop) { if (use || mov) a.StopUsingGameObject(false); }
                    else if (mov) a.HumanAIComponent.MoveToClear();
                }
                catch (Exception e) { _rStumbles++; BannerCompanies.Stumble("BannerCompaniesMission.BiegaczStop", e); }
            }
            var inv = CultureInfo.InvariantCulture;
            Line(fl, "biegacz (158) porzucil: " + GNames[why] + " po " + (now - fl.RunAt).ToString("0.0", inv) + " s, " + fl.Dist.ToString("0.0", inv) + " m od sztandaru (z "
                     + fl.StartDist.ToString("0.0", inv) + " m)", a, "biegacz");
        }

        private void ReleaseAll(int why, float now)
        {
            for (int i = 0; i < _fallen.Count; i++)
            {
                var fl = _fallen[i];
                if (fl.Closed || fl.Runner == null) continue;
                try { GiveUp(fl, why, now, true); }
                catch (Exception e) { fl.Runner = null; _rStumbles++; BannerCompanies.Stumble("BannerCompaniesMission.BiegaczKoniec", e); }
            }
        }

        /// <summary>Recenzja 158 (BannerCompanies.BeforeWithdraw, RoutKeeper.Withdraw, glowny watek): biegacz wchodzi w odwrot rannego 143 - rezygnacja "rana (143)"
        /// ze StopUsingGameObject(false) TERAZ, zanim 143 cokolwiek wysle (natywny Retreat albo postoj). Juz trzyma sztandar - jak Step: zdarzenie w kolejce.</summary>
        internal void RunnerWithdraw(Agent a)
        {
            Fallen fl;
            if (!_runners.TryGetValue(a, out fl) || fl == null || fl.Closed || !ReferenceEquals(fl.Runner, a)) return;
            if (a.Banner != null) return;
            var m = Mission;
            GiveUp(fl, GWound, m != null ? m.CurrentTime : 0f, true);
        }

        private void Close(Fallen fl)
        {
            if (fl.Closed) return;
            fl.Closed = true;
            if (fl.Sent == 0) _rNoRunner[fl.LastWhy < 0 ? SNotYet : fl.LastWhy]++;
            fl.Tried.Clear();
        }

        // ------------------------------------------------------------ pomocnicze

        /// <summary>Kompania upadku: kompania upuszczajacego w szyku sztandaru, inaczej najblizsza kompania szyku bez sztandaru (FallPos do 10 m), inaczej null.</summary>
        private Company CompanyOf(Fallen fl)
        {
            Company co;
            var a = fl.Dropper;
            if (a != null && _mem.TryGetValue(a, out co) && ReferenceEquals(co.F, fl.F)) return co;
            List<Company> list;
            if (fl.F == null || !_forms.TryGetValue(fl.F, out list)) return null;
            Company best = null;
            float bd = BannerCompanies.RunCompanyNear * BannerCompanies.RunCompanyNear;
            for (int i = 0; i < list.Count; i++)
            {
                var x = list[i];
                if (!x.HadStandard || x.StandardUp) continue;
                float d = x.FallPos.DistanceSquared(fl.Pos);
                if (d < bd) { bd = d; best = x; }
            }
            return best;
        }

        /// <summary>AI walczy o zycie: wrogow (czynni, nieuciekajacy ludzie) w 6 m od sztandaru wiecej niz swoich + 1.</summary>
        private bool Crowded(Vec3 pos, Team t, Mission m)
        {
            if (t == null) return false;
            var p2 = pos.AsVec2;
            int foes = 0, own = 0;
            m.GetNearbyEnemyAgents(p2, BannerCompanies.RunCrowdRadius, t, _near2);
            for (int i = 0; i < _near2.Count; i++) { var x = _near2[i]; if (x != null && x.IsActive() && x.IsHuman && !x.IsRunningAway) foes++; }
            m.GetNearbyAllyAgents(p2, BannerCompanies.RunCrowdRadius, t, _near2);
            for (int i = 0; i < _near2.Count; i++) { var x = _near2[i]; if (x != null && x.IsActive() && x.IsHuman && !x.IsRunningAway) own++; }
            _near2.Clear();
            return foes > own + 1;
        }

        private static bool Retreating(Formation f)
        {
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            return oe == MovementOrder.MovementOrderEnum.Retreat || oe == MovementOrder.MovementOrderEnum.FallBack;
        }

        private static bool Withdrawing(Agent a)
        {
            var k = RoutHold.Live;
            return k != null && k.Withdrawing(a);
        }

        private static string Hands(Agent a)
        {
            try
            {
                var w = a.WieldedWeapon;
                var o = a.WieldedOffhandWeapon;
                string s1 = w.IsEmpty || w.Item == null ? "-" : w.Item.StringId;
                string s2 = o.IsEmpty || o.Item == null ? "-" : o.Item.StringId;
                return s1 + " / " + s2;
            }
            catch { return "?"; }
        }

        /// <summary>Srednia predkosc biegu (pokonany dystans / czas): timeout NIE - podniesieni, TAK - porzuceni po limicie 8 s (osobno, recenzja 158).</summary>
        private void NoteSpeed(Fallen fl, Agent a, float now, bool timeout)
        {
            float t = now - fl.RunAt;
            if (a == null || t < 0.5f) return;
            float d = fl.Dist;
            try { if (a.IsActive()) d = a.Position.AsVec2.Distance(fl.Pos.AsVec2); } catch { }
            float cov = fl.StartDist - d;
            if (cov < 0f) cov = 0f;
            if (timeout) { _rSpeedTSum += cov / t; _rSpeedTN++; }
            else { _rSpeedSum += cov / t; _rSpeedN++; }
        }

        private void Line(Fallen fl, string what, Agent who, string role)
        {
            StdLine(fl.Co, what, who, fl.Pos, role, fl.F);
        }

        // ------------------------------------------------------------ koniec misji

        private void RunEnd()
        {
            if (_runLineDone || !_runSub) return;
            _runLineDone = true;
            var m = Mission;
            float now = m != null ? m.CurrentTime : 0f;
            try { if (m != null) Drain(m); } catch { }
            ReleaseAll(GEnd, now);
            for (int i = 0; i < _fallen.Count; i++)
            {
                if (_fallen[i].Closed) continue;
                _rLeft++;
                Close(_fallen[i]);
            }
            WriteRun();
        }

        private void RunClear()
        {
            _fallen.Clear(); _runners.Clear(); _picked.Clear(); _pickedByRunner.Clear(); _noRun.Clear(); _noRunForm.Clear(); _near.Clear(); _near2.Clear(); _runQ2.Clear();
            lock (_runQLock) _runQ.Clear();
        }

        private static string Reasons(int[] v, string[] names)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(names[i]).Append(' ').Append(v[i]);
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private void WriteRun()
        {
            var c = Settings.Current;
            var inv = CultureInfo.InvariantCulture;
            float range = c != null ? Math.Min(Math.Max(c.BannerPickupRange, 0f), BannerCompanies.RunMaxRange) : 0f;
            int gave = 0, none = 0;
            for (int i = 0; i < NG; i++) gave += _rGiveUp[i];
            for (int i = 0; i < NS; i++) none += _rNoRunner[i];
            var sb = new StringBuilder(1400);
            sb.Append("Sztandar (158): misja ").Append(MissionTeams.Kind(Mission))
              .Append(" | klucze: zasieg ").Append(range.ToString("0", inv)).Append(" m").Append(range > 0f ? "" : " (biegacze wylaczeni - sam pomiar)")
              .Append(", szyki gracza ").Append(c != null && c.BannerPickupYourFormations ? "TAK" : "NIE").Append(" (do ").Append(BannerCompanies.RunPlayerRange.ToString("0", inv)).Append(" m)")
              .Append(" | upadkow ").Append(_rDrops).Append(" (lezy na koniec ").Append(_rLeft).Append(", zniknelo ").Append(_rVanished)
              .Append(", sztandar bez szyku ").Append(_rNoForm).Append(')')
              .Append(" | biegaczy wyslanych ").Append(_rSent).Append(" (do upadkow ").Append(_rFallsRun).Append(", okrzyk przy wyslaniu ").Append(_rCries).Append(')')
              .Append(" | podniesionych ").Append(_rRaised).Append(" (sr. czas ").Append(_rRaised > 0 ? (_rRaisedFall / _rRaised).ToString("0.0", inv) : "-")
              .Append(" s od upadku, ").Append(_rRaised > 0 ? (_rRaisedRun / _rRaised).ToString("0.0", inv) : "-").Append(" s biegu; sr. predkosc biegu ")
              .Append(_rSpeedN > 0 ? (_rSpeedSum / _rSpeedN).ToString("0.00", inv) : "-").Append(" m/s z ").Append(_rSpeedN)
              .Append(", porzuconych po 8 s ").Append(_rSpeedTN > 0 ? (_rSpeedTSum / _rSpeedTN).ToString("0.00", inv) : "-").Append(" m/s z ").Append(_rSpeedTN).Append(')')
              .Append(" | porzuconych ").Append(gave).Append(" (").Append(Reasons(_rGiveUp, GNames)).Append(')')
              .Append(" | przejetych przez gre ").Append(_rGame).Append(" (gracz ").Append(_rGamePlayer).Append(", w trakcie naszego biegu ").Append(_rGameRun)
              .Append("; gra biegla przed nami ").Append(_rGameSeen).Append(')')
              .Append(" | bez biegacza ").Append(none).Append(" (").Append(Reasons(_rNoRunner, SNames)).Append(')')
              .Append(" | upuszczonych ponownie przy zmianie broni ").Append(_rRedrop).Append(" (zywy, nie w panice; z tego po biegu ").Append(_rRedropRunner)
              .Append("; nowy chorazy w panice ").Append(_rRedropPanic).Append("; szykow odtad bez biegaczy ").Append(_noRunForm.Count)
              .Append("; cel automatyczny przywrocony (RBM) ").Append(_rTargetBack).Append(')')
              .Append(" | podniesienia spoza listy ").Append(_rPickOther)
              .Append(" | kolejka: pominiete ").Append(_rQueueCut).Append(", bledy zdarzen ").Append(Volatile.Read(ref _rEvFail))
              .Append(" | potkniecia ").Append(_rStumbles).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
