using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Armoury.Villages;
using InformationManager = TaleWorlds.Library.InformationManager;
using InquiryData = TaleWorlds.Library.InquiryData;

namespace Armoury
{
    /// <summary>
    /// E7/R1 (235, PLAN 7.11 a, R15; projekt etapu 7 v2 rozdz. 3 "E7/R1"; RB = docs/PROJEKT-RABUNEK-OSADA-PO-OSADZIE-2026-10-07.md po poprawkach K1-K12) -
    /// RABUNEK OSADA PO OSADZIE DLA AI. Wies na mapie to okreg (n osad po ok. DistrictSettlementPeople ludzi - ksiega wiosek E4/11). Dotad gra konczyla rabunek
    /// po ok. 2.4 doby bez wzgledu na sile napastnika i dawala zloto za hearth oraz towar "z produkcji" i "zwykly lup" z niczego. Odtad (AI; rabunki z graczem - punkt GRACZ nizej, E7/R2):
    ///  - PRZEJECIE LUPIENIA: prefiks Priority.First na RaidEventComponent.Update (G :160) zwraca false w fazie lupienia (DefenderSide.TroopCount == 0, G :162).
    ///    Decyzja "rabunek prowadzi R1" zapada RAZ, w pierwszym wywolaniu prefiksu (przed Devastation.RaidPrefix i oknem W6): klucz RaidBySettlementEnabled,
    ///    wies objeta spustoszeniem (Devastation.Covers), wodzem jest partia ruchoma (od E7/R2 takze gracz i zdarzenie gracza). Prefiksy void po naszym false biegna dalej (Harmony
    ///    AffectsOriginal) - stad straznik Owns na gorze Devastation.RaidPrefix i SmallGold.RaidPre.
    ///  - KROK = m osad: m = min(Z, max(1, N / D)), Z = floor(Room / S_v) osad w zasiegu (Room E4/5 - pulap i dno 11 hearth), S_v = ludzie okregu / n,
    ///    D = RaidDetachmentMen. Postep w ludziach przeszukanych: += R x N x f x u x dt (R = DevastationPerRaiderDay, N = napastnicy na biezaco, f = mnoznik
    ///    modelu rabunku (perki BK, BKROT, NavalDLC - iloraz ResultNumber / BaseNumber), u = Room / zasieg na poczatku kroku, nie mniej niz UFloor - straznik
    ///    przeciw rabunkowi "na wieki" w okregu prawie wypalonym, licznik w linii). Punkty osady = punkty startu x (1 - 0.99 x postep) - w kroku nigdy 0 (K1).
    ///  - LUDZIE: co 5% kroku Devastation.StrikePeople - ten sam rdzen co Strike (PeopleBook.Move, zabici / las / uchodzcy, Famine.Burn, VillageBook.OnStrike
    ///    z kluczem sesji rs.Comp) i TEN SAM stan rabunku E4/5 (Devastation.StateOf): jency E4/9 przy koncu rabunku, nekromancja Innych (RaidDead), ksiega wiosek.
    ///  - JEDZENIE (K4): przy kazdym uderzeniu napastnicy dostaja ze spichlerza okregu (Famine.Take) zboze rowne swemu zuzyciu za ten czas (20 racji za sztuke).
    ///  - AI: jeden krok na sesje, potem koniec tak jak gra (G :307-313): punkty 0, AttackerVictory (setter internal - delegat), finish, SetLevelMaskIsDirty ->
    ///    OnBeforeFinalize: Looted -> RaidCompleted (jeden na sesje). RaidDamage = postep kroku w kazdym ticku (kara relacji gry, K3; 1 po pelnym kroku).
    ///    CZUJNOSC: po pelnym kroku wies zostaje Looted bez leczenia ceil(1 + 100 x d) dob (d = trafieni sesji / ludzie okregu), najwyzej RaidAlertMaxDays -
    ///    prefiks VillageHealCampaignBehavior.DailyTickSettlement tylko odklada leczenie (milicji nie rusza: +20 przy odbudowie zamyka E4/8).
    ///  - LUP tylko z zasobow wsi, raz na sesje (koniec kroku albo przerwanie - za zrobiona czesc): udzial s = trafieni sesji / ludzie w domu na starcie; zloto
    ///    s x kiesa wsi TA SAMA funkcja platnika co E5/A7 (SmallGold.PayRaidLoot -> LootPayer); towar s z magazynu wsi (Settlement.ItemRoster): konie i zywy
    ///    inwentarz ida z napastnikiem, reszta wedlug wolnego udzwigu taboru (najcenniejsze na kilogram najpierw), czego nie uniesie - "spalone w rabunku"
    ///    (ksiega towarow 146 FRaid, ArmsBook KRaid). Zadnego zlota za hearth, towaru "z produkcji" ani "zwyklego lupu" (G :176-188, :272-301 - z niczego).
    ///    OnItemsLooted jak oryginal (sluchacze tylko w grze - ROT, BKROT, BEE, DTE, NavalDLC nie sluchaja).
    ///  - NIEZALEZNY BILANS (v2, R1-test): wokol kazdej operacji pomiar kiesy wodza i wsi, magazynu wsi, rosterow napastnikow i spichlerza - linia podaje roznice.
    ///  - GRACZ (E7/R2, 236, PLAN 7.11 b; projekt etapu 7 v2 rozdz. 3 "E7/R2", RB rozdz. 4 i K1/K2/K10): rabunek ze zdarzeniem gracza prowadzi ta sama regula.
    ///    Wodzem jest AI (gracz w armii AI albo dolaczyl do rabunku) - regula AI: jeden krok i koniec gry (punkty 0, AttackerVictory, finish); zdarzenia gracza
    ///    MapEvent.Update nie konczy sam (G MapEvent.cs:1040-1044) - konczy je konsekwencja menu oczekiwania gry "raiding_village" przy pasku 100% (G GameMenu.cs:234-238
    ///    -> PlayerEncounter.Finish), dokladnie jak pelny rabunek w grze; do tego czasu prefiks trzyma finish (ss.Done). Wodzem jest GRACZ (sam albo jako wodz armii):
    ///    po kazdym pelnym kroku lup kroku (udzial s = trafieni kroku / ludzie w domu na poczatku kroku) i - przy kluczu RaidPlayerStepPopup - stan "czeka na decyzje":
    ///    punkty stoja na 1% punktow startu (pasek 99% - gra NIE wola konsekwencji menu, K1), a postfiks ticku menu (wait_menu_raiding_village_on_tick) otwiera
    ///    okienko InformationManager.ShowInquiry z pauza (wzorzec NightRest: flaga, okienko z ticku; pauza = MapState dostaje OnIdleTick, ani menu, ani kampania
    ///    nie tykaja): ilu trafiono, zabito, pojmano (plan E4/9), ilu ucieklo, lup, spichlerz, ile osad wioski stoi. "Raid the next settlement (about N days)"
    ///    (takze Escape - odwracalne, "End Raiding" jest zawsze w menu) - nowy krok od pelnych punktow (ten sam Plan co pierwszy krok); "End the raid" (takze Enter)
    ///    - koniec gry jak u AI. Okienko ustawia TYLKO wybor w sesji; wykonuje go prefiks kroku w najblizszym ticku kampanii (oryginal Update dalej nie biegnie).
    ///    Okreg bez osad w zasiegu - okienko z samym "End the raid". Opcja gry "End Raiding" (ta sama metoda co konsekwencja menu - G :99-100; isLeave, wiec gra
    ///    robi EndWait PRZED konsekwencja - K2): prefiks void na wait_menu_end_raiding_on_consequence przy >= 1 spalonej osadzie zamyka sesje jak "End the raid"
    ///    (czesc biezacego kroku rozliczona, punkty 0, AttackerVictory) i ZAWSZE przepuszcza oryginal (PlayerEncounter.Finish -> wies Looted, czujnosc) - bez wlasnego
    ///    StartWait i bez zadnego SwitchToMenu (CLAUDE.md 7); przy 0 osadach - gra (odwolany, wies Normal, czesc kroku w ksiedze). Po wczytaniu gra sama wznawia
    ///    oczekiwanie (GameMenuVM.Refresh -> RunWaitMenuCondition -> StartWait), sesja wraca z zapisu ze stanem decyzji i liczbami kroku, okienko otwiera sie znowu.
    ///    Klucz zdjety albo latki okienka niewpiete - gracz jak AI (jeden krok i koniec). Wyjatek okienka liczony; po MaxStumbles nieudanych prob - koniec jak AI.
    /// Zapis: sekcje r: (sesje) i c: (czujnosc) klucza arm_district przez VillageBook.Export/Import (brak = sesja od nowa z punktow gry). Wyjatek per sesja liczony;
    /// po MaxStumbles w jednej sesji - sesja oddana grze (bez globalnego wylacznika, CLAUDE.md 7). Reset w konstruktorze ArmouryBehavior.
    /// </summary>
    internal static class DistrictRaid
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double PointsKeep = 0.99;          // punkty osady w kroku: start x (1 - 0.99 x postep) - zero tylko na koniec sesji (K1)
        private const int Chunks = 20;                   // uderzenie StrikePeople co 5% kroku
        private const double RationsPerGrain = 20.0;     // skala zolnierza: 1 sztuka jedzenia = 20 czlowieko-dni (gra: zuzycie partii = ludzie / 20 na dobe; PROJEKT-GLOD 1.6)
        private const double UFloor = 0.25;              // najnizsze u kroku (okreg prawie wypalony: krok najwyzej 4x dluzszy niz w swiezym okregu zamiast setek dob)
        private const int MaxStumbles = 3;               // potkniec jednej sesji, po ktorych oddajemy ja grze
        private const int MaxDaysList = 40;              // dni na osade w linii doby (lista do mediany okna w sprawdz_logi)
        private const int ChoiceNext = 1, ChoiceEnd = 2; // E7/R2: wybor gracza z okienka (0 = jeszcze nie wybral)
        private const int FieldsR1 = 19, FieldsR2 = 31;  // pola wpisu sesji w zapisie: E7/R1 i z krokiem gracza (E7/R2)

        internal static bool On { get { var s = Settings.Current; return s != null && s.RaidBySettlementEnabled && _ready; } }
        /// <summary>E7/R2: okienko gracza po kazdej osadzie czynne (klucz i obie latki menu); nieczynne = gracz jak AI (jeden krok i koniec).</summary>
        internal static bool PopupOn { get { var s = Settings.Current; return s != null && s.RaidPlayerStepPopup && _popupReady; } }

        internal sealed class Sess
        {
            public RaidEventComponent Comp; public Village V; public Devastation.RaidState Rs;
            public bool Started, Done, Restored, BurnedOut, UClamped, Lord, LootGiven;
            public float Hp0 = 1f;
            public double Prog, Work, M, U, L0, Home0, Sv;   // postep i praca kroku w ludziach przeszukanych
            public double Struck;                            // ludzie kroku juz oddani do StrikePeople
            public double Hit, Dead, Fled, Ref;              // wynik uderzen tej sesji
            public double PdPend, EatDays, LootDays;         // osobodni od uderzenia, doby od posilku, doby lupienia razem
            public double F = 1.0;                           // ostatni znany mnoznik tempa z modelu rabunku
            public int Stumbles;
            // E7/R2 (236): krok po kroku u gracza. Liczby S* dotycza BIEZACEGO kroku (u AI jedyny krok = sesja): udzial lupu kroku i tresc okienka
            public bool Player, Ask, Shown;                  // wodzem jest gracz; pelny krok czeka na decyzje gracza; okienko tego kroku juz raz otwarte
            public int Choice, Steps, AskFails;              // wybor z okienka; pelne kroki sesji z osadami; nieudane proby okienka w tym kroku
            public double Settl;                             // osady spalone w pelnych krokach sesji
            public double SHit, SDead, SFled, SRef, SCap;    // trafieni, zabici, las, uchodzcy, plan pojmanych (E4/9) kroku
            public double SFoodBurn, SFoodEat;               // racje spichlerza spalone przy uderzeniach / zjedzone przez napastnikow w kroku (tylko sesja gracza)
            public long SGold, STake, SBurn;                 // lup kroku: zloto z kiesy wsi, sztuki zabrane, sztuki spalone w rabunku
        }

        internal sealed class EndCtx { public Sess Ss; public long Cap0; public double RaidDead; }

        /// <summary>Plan kroku przy stanie okregu teraz (Start i szacunek "about N days" okienka E7/R2 - jedna arytmetyka).</summary>
        private struct StepPlan { public double M, Work, U, L, Home, Sv; public bool BurnedOut, UClamped; }

        private static readonly Dictionary<RaidEventComponent, Sess> _sess = new Dictionary<RaidEventComponent, Sess>();
        private static readonly HashSet<RaidEventComponent> _notMine = new HashSet<RaidEventComponent>();
        private static readonly Dictionary<string, string> _pendingR = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _alert = new Dictionary<string, int>(StringComparer.Ordinal);   // id wsi -> doba konca czujnosci
        private static int _pendingDays;
        private static bool _ready;
        private static Action<Settlement, float> _setHp;
        private static Action<RaidEventComponent, float> _setDmg;
        private static Action<MapEvent, BattleState> _setBs;

        // liczniki doby
        private static int _dEnded, _dSteps, _dInterrupted, _dReleased, _dBurnedOut, _dUClamped, _dStarted, _dRestored, _dStrikes, _dAlertNew, _dHealHeld, _dAbandoned, _dStumbles;
        private static double _dSettl, _dHit, _dDead, _dFled, _dRef, _dRaidDead, _dCheckDead;
        private static long _dCap, _dGold, _dTake, _dTakeValue, _dBurn, _dBurnValue, _dEat;
        private static double _dBal1, _dBal2, _dBal3;
        private static long _dTicks;
        private static readonly List<double> _dDaysPer = new List<double>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static bool _offLogged;

        // E7/R2 (236): okienko gracza. _ask = sesja gracza czekajaca na decyzje (zdarzenie gracza jest jedno naraz) - tick menu w zwyklej klatce konczy na null
        private static Sess _ask;
        private static bool _askOpen, _popupReady;
        private static readonly List<string> _wired2 = new List<string>(), _missing2 = new List<string>();
        private static int _dPStarted, _dPEnded, _dPWon, _dPCalled, _dPBroken, _dPSteps, _dAsk, _dShown, _dShownAgain, _dNoNext, _dNext, _dEndPopup, _dEndMenu, _dEndMenu0, _dAutoEnd, _dAskFail;
        private static double _dPSettl;

        internal static void Reset()
        {
            _sess.Clear(); _notMine.Clear(); _pendingR.Clear(); _alert.Clear(); _pendingDays = 0; _offLogged = false;
            _ask = null; _askOpen = false;
            lock (_errSites) _errSites.Clear();
            NewDay();
        }

        private static void NewDay()
        {
            _dEnded = _dSteps = _dInterrupted = _dReleased = _dBurnedOut = _dUClamped = _dStarted = _dRestored = _dStrikes = _dAlertNew = _dHealHeld = _dAbandoned = _dStumbles = 0;
            _dSettl = _dHit = _dDead = _dFled = _dRef = _dRaidDead = _dCheckDead = 0.0;
            _dCap = _dGold = _dTake = _dTakeValue = _dBurn = _dBurnValue = _dEat = 0;
            _dBal1 = _dBal2 = _dBal3 = 0.0;
            _dTicks = 0;
            _dDaysPer.Clear();
            _dPStarted = _dPEnded = _dPWon = _dPCalled = _dPBroken = _dPSteps = _dAsk = _dShown = _dShownAgain = _dNoNext = _dNext = _dEndPopup = _dEndMenu = _dEndMenu0 = _dAutoEnd = _dAskFail = 0;
            _dPSettl = 0.0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error("DistrictRaid." + where, e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static double Clamp01(double x) { return double.IsNaN(x) ? 0.0 : (x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x)); }
        private static string F(double x, string f) { return x.ToString(f, Inv); }

        private static double TickDays()
        {
            // czas tego tykniecia mapy - ten sam, z ktorego gra liczy postep rabunku (DefaultRaidModel.CalculateHitDamage)
            try { double d = CampaignTime.DeltaTime.ToDays; return d > 0.0 && d < 1.0 ? d : 0.0; }
            catch (Exception e) { Stumble("TickDays", e); return 0.0; }
        }

        /// <summary>Czy rabunek prowadzi R1 (Devastation.RaidPrefix/EndPrefix, SmallGold.RaidPre - straznik przeciw podwojnemu liczeniu).</summary>
        internal static bool Owns(RaidEventComponent r)
        {
            return r != null && _sess.Count > 0 && _sess.ContainsKey(r);
        }

        // ------------------------------------------------------------ decyzja (raz na rabunek)
        private static Sess Decide(RaidEventComponent comp)
        {
            Sess ss;
            if (_sess.TryGetValue(comp, out ss)) return ss;
            if (_notMine.Contains(comp)) return null;
            bool mine = false, player = false;
            Village v = null;
            try
            {
                var c = Campaign.Current;
                var me = comp.MapEvent;
                var st = comp.MapEventSettlement;
                v = st != null ? st.Village : null;
                float per;
                var s = Settings.Current;
                // R = 0 dalby krok bez konca (postep stoi) - wtedy rabunek gry pod E4/5
                // E7/R2: zdarzenie gracza tez (wodz AI - regula AI, wodz gracz - krok po kroku z okienkiem)
                if (On && s.DevastationPerRaiderDay > 0f && c != null && c.GameStarted && me != null && v != null && Devastation.Covers(v, out per))
                {
                    var att = comp.AttackerSide;
                    var lp = att != null ? att.LeaderParty : null;
                    var mp = lp != null ? lp.MobileParty : null;
                    mine = mp != null;
                    player = mp != null && mp == MobileParty.MainParty;
                }
            }
            catch (Exception e) { mine = false; Stumble("Decide", e); }
            if (!mine) { _notMine.Add(comp); return null; }
            ss = new Sess { Comp = comp, V = v, Player = player };
            ss.Rs = Devastation.StateOf(comp, v, true);           // TEN SAM stan rabunku E4/5 (v2): jency E4/9, Inni, klucz sesji ksiegi wiosek
            _sess[comp] = ss;
            _dStarted++;
            try { Restore(ss); } catch (Exception e) { Stumble("Restore", e); }
            if (ss.Player && ss.Ask && !ss.Done) _ask = ss;       // E7/R2: wczytanie w stanie decyzji - tick menu otworzy okienko znowu
            return ss;
        }

        /// <summary>Sesja oddana grze (potkniecia): czesc kroku rozliczona, lup za zrobiona czesc; dalej rabunek gry pod E4/5 (ten sam stan rabunku).</summary>
        private static void Release(Sess ss, RaidEventComponent comp)
        {
            try
            {
                if (ss.Started && !ss.Done)
                {
                    double rest = Math.Min(ss.Prog, ss.Work) - ss.Struck;
                    if (rest > 1e-9) StrikeChunk(ss, rest, comp);
                    Loot(ss, comp);
                }
            }
            catch (Exception e) { Stumble("Release", e); }
            _sess.Remove(comp);
            _notMine.Add(comp);
            _dReleased++;
        }

        // ------------------------------------------------------------ prefiks RaidEventComponent.Update(ref bool finish) - Priority.First
        public static bool UpdatePrefix(RaidEventComponent __instance, ref bool finish)
        {
            if (__instance == null) return true;
            Sess ss;
            try { ss = Decide(__instance); }
            catch (Exception e) { Stumble("UpdatePrefix(decyzja)", e); return true; }
            if (ss == null) return true;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var me = __instance.MapEvent;
                if (me == null) return true;
                var def = __instance.DefenderSide; var att = __instance.AttackerSide;
                if (def == null || att == null || def.TroopCount > 0) return true;       // walka (milicja, odsiecz) - gra i MapEvent
                var v = ss.V; var st = v.Settlement;
                me.WasEverInLootingPhase = true;                                          // jak G :164 (odsiecz -> bitwa polowa, rabunek z morza)
                if (ss.Done) { finish = true; return false; }                             // zdarzenie gracza: konczy je konsekwencja menu gry przy pasku 100% (E7/R2)
                int men = att.TroopCount;
                if (ss.Ask)
                {
                    // E7/R2: pelny krok gracza czeka na decyzje - punkty stoja na 1% (pasek 99%, K1), okienko otwiera tick menu; tu TYLKO wykonanie wyboru
                    if (ss.Choice == 0 && !PopupOn) { ss.Choice = ChoiceEnd; _dAutoEnd++; }           // klucz zdjety w trakcie decyzji - koniec jak AI
                    if (ss.Choice == ChoiceNext) NextStep(ss, __instance, men, st);
                    else if (ss.Choice == ChoiceEnd) { Win(ss, __instance, me, st); finish = true; }
                    return false;
                }
                if (!ss.Started) { Start(ss, __instance, men); if (ss.Player) _dPStarted++; }
                double dt = TickDays();
                if (men > 0 && dt > 0.0)
                {
                    ss.F = Factor(att, st, ss.F);
                    double r = Math.Max(0f, Settings.Current.DevastationPerRaiderDay);
                    ss.Prog += r * men * ss.F * ss.U * dt;
                    ss.PdPend += men * dt; ss.EatDays += dt; ss.LootDays += dt;
                    double chunk = ss.Work / Chunks;
                    int guard = 0;
                    while (chunk > 0.0 && ss.Struck < ss.Work - 1e-9 && ss.Prog >= ss.Struck + chunk - 1e-9 && guard++ < Chunks + 1)
                        StrikeChunk(ss, Math.Min(chunk, ss.Work - ss.Struck), __instance);
                }
                double q = ss.Work > 0.0 ? Math.Min(1.0, ss.Prog / ss.Work) : 1.0;
                if (q >= 1.0 - 1e-9)
                {
                    // koniec kroku AI - dokladnie zakonczenie gry (G :307-313); oryginal nie biegnie (inaczej zloto i towar z niczego - G :179-188, :272-301)
                    if (ss.Work - ss.Struck > 1e-9) StrikeChunk(ss, ss.Work - ss.Struck, __instance);
                    Loot(ss, __instance);
                    if (ss.Player && PlayerStepDone(ss, __instance, st)) return false;    // E7/R2: gracz decyduje w okienku - pasek 99%, oryginal dalej nie biegnie
                    Win(ss, __instance, me, st);
                    finish = true;
                    return false;
                }
                _setHp(st, ss.Hp0 * (float)(1.0 - PointsKeep * q));
                _setDmg(__instance, ss.Steps > 0 ? 1f : (float)q);                        // E7/R2: w kolejnym kroku gracza kara relacji zostaje 1 (osada juz spalona)
                return false;
            }
            catch (Exception e)
            {
                ss.Stumbles++;
                Stumble("UpdatePrefix", e);
                if (ss.Stumbles >= MaxStumbles) { Release(ss, __instance); return true; }
                return false;
            }
            finally { _dTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>
        /// Plan kroku przy stanie okregu TERAZ (bez zmiany stanu): osady okregu, Z w zasiegu, m, praca w ludziach, u, ludzie w domu (udzial lupu). Arytmetyka
        /// E7/R1 bez zmian - wydzielona, bo E7/R2 liczy nia takze nastepny krok gracza (przycisk "about N days" i sam krok - jedna regula).
        /// </summary>
        private static StepPlan Plan(Village v, int men)
        {
            var s = Settings.Current;
            var p = new StepPlan();
            double home, away, open, reach;
            if (!Devastation.DistrictPeople(v, out home, out away, out open, out reach)) { home = away = open = reach = 0.0; }
            double l = home + away;
            double n;
            if (!VillageBook.DistrictN(v, out n)) n = Math.Max(1.0, Math.Round(l / Math.Max(50, Math.Min(5000, s.DistrictSettlementPeople))));
            double sv = n > 0.0 ? l / n : 0.0;
            int d = Math.Max(20, Math.Min(2000, s.RaidDetachmentMen));
            double z = sv > 0.0 ? Math.Floor(open / sv) : 0.0;
            if (z >= 1.0) p.M = Math.Min(z, Math.Max(1.0, Math.Floor(men / (double)d)));
            else { p.M = 0.0; p.BurnedOut = true; }                            // okreg wypalony (Z = 0): nic do spalenia - koniec od razu (Looted, czujnosc 1 doba)
            p.Work = p.BurnedOut ? 0.0 : Math.Max(0.0, Math.Min(p.M * sv, open));
            p.U = reach > 0.0 ? open / reach : 0.0;
            if (p.U < UFloor) { p.U = UFloor; p.UClamped = true; }
            p.L = l; p.Home = home; p.Sv = sv;
            return p;
        }

        /// <summary>Recenzja wyspy-wolni R2-7 (WW3 ReaverLaw.LootEstimate): okreg wsi wypalony TERAZ (Plan: Z = 0 osad w zasiegu - krok nic nie spali, lup 0).
        /// false - regula E7/R1 nieczynna albo wies poza spustoszeniem (rabunek gry). Tylko odczyt, bez kalibracji ludnosci.</summary>
        internal static bool BurnedOutNow(Village v)
        {
            if (!On || v == null || !Devastation.CoversQuiet(v)) return false;
            try { return Plan(v, 0).BurnedOut; }
            catch (Exception e) { Stumble("BurnedOutNow", e); return false; }
        }

        /// <summary>p6-okup (R3-03a, RaidTribute): szacunek jednego kroku rabunku AI TERAZ dla `men` napastnikow - ta sama arytmetyka co Start (Plan): udzial lupu
        /// s = praca kroku / ludzie w domu, doby kroku = praca / (DevastationPerRaiderDay x men x u), mnoznik modelu rabunku 1 (perki znane dopiero w zdarzeniu).
        /// Okreg wypalony - s = 0, doby 0. false - regula E7/R1 nieczynna, wies poza spustoszeniem albo R = 0 (rabunek gry - lup z innej reguly). Tylko odczyt.</summary>
        internal static bool StepEstimate(Village v, int men, out double share, out double days)
        {
            share = 0.0; days = 0.0;
            var s = Settings.Current;
            if (!On || v == null || men <= 0 || s == null || !(s.DevastationPerRaiderDay > 0f) || !Devastation.CoversQuiet(v)) return false;
            try
            {
                var p = Plan(v, men);
                if (p.BurnedOut || !(p.Work > 0.0)) return true;
                share = p.Home > 0.0 ? Clamp01(p.Work / p.Home) : 0.0;
                days = p.Work / (s.DevastationPerRaiderDay * (double)men * Math.Max(UFloor, p.U));
                if (double.IsNaN(days) || double.IsInfinity(days)) days = 0.0;
                return true;
            }
            catch (Exception e) { Stumble("StepEstimate", e); return false; }
        }

        /// <summary>Poczatek kroku: plan (Plan) do sesji i punkty startu.</summary>
        private static void Start(Sess ss, RaidEventComponent comp, int men)
        {
            var v = ss.V;
            ss.Started = true;
            ss.Hp0 = Math.Max(0.01f, Math.Min(1f, v.Settlement.SettlementHitPoints));
            var p = Plan(v, men);
            ss.M = p.M; ss.Work = p.Work; ss.U = p.U;
            ss.BurnedOut = p.BurnedOut; ss.UClamped = p.UClamped;
            if (p.BurnedOut) _dBurnedOut++;
            if (p.UClamped) _dUClamped++;
            ss.L0 = p.L; ss.Home0 = p.Home; ss.Sv = p.Sv;
            try { var lp = comp.AttackerSide.LeaderParty; var h = lp != null ? lp.LeaderHero : null; ss.Lord = h != null && h.IsLord; }
            catch { ss.Lord = false; }
        }

        /// <summary>Koniec sesji tak jak gra konczy pelny rabunek (G RaidEventComponent.cs:307-313): punkty 0, RaidDamage 1, AttackerVictory, maska poziomu wsi.
        /// `finish` ustawia wolajacy prefiks Update; przy "End Raiding" gracza (E7/R2) zdarzenie konczy PlayerEncounter.Finish gry.</summary>
        private static void Win(Sess ss, RaidEventComponent comp, MapEvent me, Settlement st)
        {
            ss.Done = true;
            ss.Ask = false;
            if (ReferenceEquals(_ask, ss)) _ask = null;
            _setHp(st, 0f);
            _setDmg(comp, 1f);
            if (me.BattleState != BattleState.AttackerVictory) _setBs(me, BattleState.AttackerVictory);
            st.Party.SetLevelMaskIsDirty();
        }

        /// <summary>
        /// E7/R2: pelny krok gracza (lup kroku juz wydany). Liczniki krokow gracza ida tutaj (kroki AI - w EndFin). true = krok czeka na decyzje gracza: punkty
        /// na 1% punktow startu (pasek 99% - GameMenu.RunOnTick nie wola konsekwencji menu, K1), RaidDamage 1 (kara relacji gry za spalona osade, K3).
        /// false = koniec jak AI: okienko nieczynne (klucz, latki) albo okreg wypalony przed krokiem (nic nie spalono - nie ma o czym meldowac).
        /// </summary>
        private static bool PlayerStepDone(Sess ss, RaidEventComponent comp, Settlement st)
        {
            _dSteps++; _dSettl += ss.M; _dPSteps++; _dPSettl += ss.M;
            if (ss.M >= 1.0) { ss.Steps++; ss.Settl += ss.M; }
            if (ss.BurnedOut || !PopupOn) return false;
            ss.Ask = true; ss.Choice = 0; ss.AskFails = 0; ss.Shown = false;
            _ask = ss; _dAsk++;
            _setHp(st, ss.Hp0 * (float)(1.0 - PointsKeep));
            _setDmg(comp, 1f);
            return true;
        }

        /// <summary>E7/R2: "Raid the next settlement" - nowy krok tej samej sesji: liczniki kroku od zera, punkty osady pelne (pasek od 0), plan od stanu okregu teraz
        /// (Start). Sumy sesji (trafieni, zabici, doby lupienia, stan rabunku E4/5) zostaja - czujnosc, jency E4/9 i Inni licza cala sesje.</summary>
        private static void NextStep(Sess ss, RaidEventComponent comp, int men, Settlement st)
        {
            ss.Ask = false; ss.Choice = 0; ss.Shown = false;
            if (ReferenceEquals(_ask, ss)) _ask = null;
            ss.Prog = 0.0; ss.Struck = 0.0; ss.PdPend = 0.0; ss.LootGiven = false;
            ss.SHit = ss.SDead = ss.SFled = ss.SRef = ss.SCap = ss.SFoodBurn = ss.SFoodEat = 0.0;
            ss.SGold = ss.STake = ss.SBurn = 0;
            _setHp(st, 1f);
            Start(ss, comp, men);
        }

        /// <summary>Mnoznik tempa z czynnego modelu rabunku (perki gry, BK, BKROT, NavalDLC - wszystko AddFactor): ResultNumber / BaseNumber; przy bazie 0 - poprzedni.</summary>
        private static double Factor(MapEventSide att, Settlement st, double prev)
        {
            try
            {
                var en = Campaign.Current.Models.RaidModel.CalculateHitDamage(att, st.SettlementHitPoints);
                double b = en.BaseNumber;
                if (b > 1e-12) { double f = en.ResultNumber / b; if (f > 0.0 && !double.IsInfinity(f)) return f; }
            }
            catch (Exception e) { Stumble("Factor", e); }
            return prev;
        }

        /// <summary>Uderzenie: `people` ludzi przeszukanej czesci kroku przez Devastation.StrikePeople (wspolny rdzen, ten sam stan rabunku), potem posilek ze spichlerza.</summary>
        private static void StrikeChunk(Sess ss, double people, RaidEventComponent comp)
        {
            if (!(people > 0.0)) return;
            double pd = ss.PdPend > 0.0 ? ss.PdPend : 1e-9;
            double d, f, r;
            // E7/R2 (tylko sesja gracza - liczby okienka): plan pojmanych E4/9 i racje spichlerza spalone przy tym uderzeniu (Famine.Burn w rdzeniu Hit) - odczyt przed i po
            double c0 = 0.0, b0 = 0.0, b1;
            bool hasB = false;
            if (ss.Player) { c0 = ss.Rs != null ? ss.Rs.CapWould : 0.0; hasB = Famine.FoodOf(ss.V, out b0); }
            double h = Devastation.StrikePeople(ss.V, people, pd, ss.Rs, null, out d, out f, out r);
            ss.Struck += people; ss.PdPend = 0.0;
            ss.Hit += h; ss.Dead += d; ss.Fled += f; ss.Ref += r;
            ss.SHit += h; ss.SDead += d; ss.SFled += f; ss.SRef += r;
            if (ss.Player)
            {
                if (ss.Rs != null) ss.SCap += Math.Max(0.0, ss.Rs.CapWould - c0);
                if (hasB && Famine.FoodOf(ss.V, out b1)) ss.SFoodBurn += Math.Max(0.0, b0 - b1);
            }
            _dHit += h; _dDead += d; _dFled += f; _dRef += r; _dStrikes++;
            Eat(ss, comp);
        }

        /// <summary>K4: kazda partia napastnika dostaje ze spichlerza okregu zboze rowne swemu zuzyciu od ostatniego posilku (Famine.Take, 20 racji za sztuke).</summary>
        private static void Eat(Sess ss, RaidEventComponent comp)
        {
            double days = ss.EatDays;
            ss.EatDays = 0.0;
            if (!(days > 0.0)) return;
            var grain = DefaultItems.Grain;
            var att = comp.AttackerSide;
            if (grain == null || att == null) return;
            foreach (var mep in att.Parties)
            {
                try
                {
                    var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (mp == null || mp.ItemRoster == null) continue;
                    double need = Math.Max(0f, -mp.FoodChange) * days;
                    int pieces = MBRandom.RoundRandomized((float)need);
                    if (pieces <= 0) continue;
                    double f0, f1;
                    bool hasF = Famine.FoodOf(ss.V, out f0);
                    int g0 = mp.ItemRoster.GetItemNumber(grain);
                    int got = Famine.Take(ss.V, pieces, RationsPerGrain);
                    if (got <= 0) continue;
                    var gf = GoodsLedger.Begin(GoodsLedger.FRaid, mp);
                    try { mp.ItemRoster.AddToCounts(grain, got); }
                    finally { GoodsLedger.End(gf); }
                    int g1 = mp.ItemRoster.GetItemNumber(grain);
                    double drop = hasF && Famine.FoodOf(ss.V, out f1) ? f0 - f1 : got * RationsPerGrain;
                    ss.SFoodEat += drop;                                     // E7/R2: zjedzone w tym kroku (okienko gracza)
                    _dEat += got;
                    _dBal3 += (g1 - g0) * RationsPerGrain - drop;            // niezalezny bilans: zjedzone (zboze w taborze) - ubytek spichlerza
                }
                catch (Exception e) { Stumble("Eat", e); }
            }
        }

        private static long Pieces(ItemRoster r)
        {
            long n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Amount > 0) n += el.Amount; }
            return n;
        }

        /// <summary>
        /// Lup kroku (raz na krok; u AI krok = sesja): udzial s = trafieni kroku / ludzie w domu na poczatku kroku. Zloto s x kiesa wsi przez SmallGold.PayRaidLoot
        /// (platnik E5/A7); towar s z magazynu wsi - konie i zywy inwentarz z napastnikiem, reszta wedlug wolnego udzwigu (najcenniejsze na kilogram najpierw),
        /// nadwyzka "spalone w rabunku". E7/R2: kolejny krok gracza bierze udzial z tego, co w kiesie i magazynie ZOSTALO (nic dwa razy).
        /// </summary>
        private static void Loot(Sess ss, RaidEventComponent comp)
        {
            if (ss.LootGiven) return;
            ss.LootGiven = true;
            double s0 = ss.Home0 > 0.0 ? Clamp01(ss.SHit / ss.Home0) : 0.0;
            var v = ss.V; var st = v.Settlement;
            var att = comp.AttackerSide;
            var lp = att != null ? att.LeaderParty : null;
            // wyspy-wolni WW1 (zelazna cena): wodz z krolestwa najezdzcow (Zelazne Wyspy, Wolni Ludzie; AI i gracz-wasal) bierze udzial max(s, Q x u), u = postep
            // kroku (1 po pelnym kroku, przerwany - zrobiona czesc); ten sam udzial z kiesy wsi i z magazynu wsi. Inni (Dothrakowie) i klucz off - s jak dotad.
            // Recenzja R2-3: zelazna cena i nabrzeze RAZ NA SESJE (pierwszy krok - ss.Steps 0; u AI krok = sesja) - kolejne kroki gracza biora s (trafieni kroku /
            // ludzie w domu) z tego, co zostalo, jak w 63aaaedf; inaczej gracz w jednej sesji bralby 50/75/87.5/94% kiesy za kilka spalonych osad.
            int rg = ReaverLaw.RaidGroup(lp);
            bool first = ss.Steps == 0;
            double u = ss.Work > 0.0 ? Clamp01(Math.Min(ss.Prog, ss.Work) / ss.Work) : 0.0;
            double s = first ? ReaverLaw.IronShare(rg, s0, u) : s0;
            // R3-06c (p5-stada): stado okregu najpierw - ubytek stada za udzial s, zwierzeta do taboru wodza (liczba do okienka gracza); znacznik rabunku takze przy s = 0 (VillageHerds; wlasny try w srodku)
            try { ss.STake += VillageHerds.OnRaid(v, s, lp != null ? lp.MobileParty : null); } catch (Exception e) { Stumble("Loot(stado)", e); }
            if (!(s > 0.0)) return;
            // Recenzja R1-1: rabunek z morza jak w grze i NavalDLC - napastnik na morzu (MapEvent.IsNavalMapEvent dla rabunku jest zawsze false: pozycja wsi na ladzie)
            bool sea = rg >= 0 && first && ReaverLaw.RaiderAtSea(att);
            if (rg >= 0 && first) { try { var bnd = v.Bound; if (bnd != null && bnd.HasPort) ReaverLaw.NotePort(rg); } catch { } }
            if (sea) ReaverLaw.NoteSea(rg);
            // zloto - jeden platnik (kiesa wsi), wodz napastnika; bez wodza nic (jak gra G :177)
            try
            {
                var leader = lp != null ? lp.LeaderHero : null;
                if (leader != null)
                {
                    long g0 = leader.Gold, v0 = v.Gold;
                    long due = (long)Math.Round(s0 * Math.Max(0, v.Gold));
                    long paid = SmallGold.PayRaidLoot(v, leader, due);
                    // WW1: przyrost zelaznej ceny ponad s - OSOBNY przelew tym samym platnikiem (kiesa wsi -> wodz), do kosza WW2 (ReaverLaw.NoteIronGold)
                    long extra = 0;
                    if (s > s0)
                    {
                        long dueI = (long)Math.Round(s * Math.Max(0L, v0)) - paid;
                        if (dueI > 0) extra = SmallGold.PayRaidLoot(v, leader, dueI);
                        if (extra > 0) ReaverLaw.NoteIronGold(leader, extra);
                    }
                    long g1 = leader.Gold, v1 = v.Gold;
                    ss.SGold += paid + extra;
                    _dGold += paid + extra;
                    _dBal1 += (g1 - g0) - (v0 - v1);                          // niezalezny bilans: przyrost kiesy wodza - ubytek kiesy wsi
                    ReaverLaw.NoteVillageGold(rg, leader, v, paid, extra, (g1 - g0) - (v0 - v1));
                    // WW1 nabrzeze: rabunek z morza wsi portu - Qw x u kiesy W portu ponad zapas dni (kiesa ludu -> wodz); raz na sesje (sea tylko w pierwszym kroku)
                    if (sea) ss.SGold += ReaverLaw.Waterfront(rg, leader, v, u);
                }
            }
            catch (Exception e) { Stumble("Loot(zloto)", e); }
            // towar z magazynu wsi
            try
            {
                var roster = st.ItemRoster;
                if (roster == null || roster.Count == 0) return;
                var plan = new List<KeyValuePair<EquipmentElement, int>>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                    int want = MBRandom.RoundRandomized(el.Amount * (float)s);
                    if (want > el.Amount) want = el.Amount;
                    if (want > 0) plan.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, want));
                }
                if (plan.Count == 0) return;
                plan.Sort((a, b) =>
                {
                    bool aa = Walks(a.Key.Item), bb = Walks(b.Key.Item);
                    if (aa != bb) return aa ? -1 : 1;
                    return PerKg(b.Key.Item).CompareTo(PerKg(a.Key.Item));
                });
                // partie napastnika: wodz najpierw, wolny udzwig
                var parties = new List<MobileParty>();
                var free = new List<double>();
                var got = new List<ItemRoster>();
                if (lp != null && lp.MobileParty != null) parties.Add(lp.MobileParty);
                if (att != null) foreach (var mep in att.Parties) { var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null; if (mp != null && !parties.Contains(mp)) parties.Add(mp); }
                if (parties.Count == 0) return;
                long p0 = 0;
                foreach (var mp in parties)
                {
                    double cap = 0.0;
                    try { cap = Math.Max(0.0, mp.InventoryCapacity - mp.TotalWeightCarried); } catch { cap = 0.0; }
                    free.Add(cap); got.Add(new ItemRoster());
                    p0 += Pieces(mp.ItemRoster);
                }
                long v0 = Pieces(roster);
                long take = 0, burn = 0;
                // audyt 13.10 (drobne, [sztuki]): lista dla OnItemsLooted (got[]) budowana PO ramce FRaid. Dotad tymczasowy roster dostawal sztuki
                // wewnatrz ramki, a podsluch ksiegi (GoodsLedger.Tap) liczy kazdy ItemRoster.AddToCounts bez pytania, czyj to roster - kazda zabrana
                // sztuka byla w ksiedze falszywa produkcja "zboze ze spichlerza okregu dla napastnikow" i falszywym "bez wyjasnienia", a "spalone"
                // wychodzilo za male. W ramce tylko prawdziwe ruchy (magazyn wsi -> tabor); tu ruchy do listy (partia, przedmiot, ile).
                var mvP = new List<int>(); var mvEl = new List<EquipmentElement>(); var mvN = new List<int>();
                var gf = GoodsLedger.Begin(GoodsLedger.FRaid, st);
                try
                {
                    foreach (var kv in plan)
                    {
                        var el = kv.Key; var it = el.Item; int n = kv.Value;
                        roster.AddToCounts(el, -n);
                        int left = n;
                        if (Walks(it))
                        {
                            parties[0].ItemRoster.AddToCounts(el, left); mvP.Add(0); mvEl.Add(el); mvN.Add(left);
                            take += left; _dTakeValue += (long)it.Value * left; left = 0;
                        }
                        else
                        {
                            double w = Math.Max(0f, it.Weight);
                            for (int p = 0; p < parties.Count && left > 0; p++)
                            {
                                int fit = w > 1e-6 ? (int)Math.Min(left, Math.Floor(free[p] / w)) : left;
                                if (fit <= 0) continue;
                                parties[p].ItemRoster.AddToCounts(el, fit); mvP.Add(p); mvEl.Add(el); mvN.Add(fit);
                                free[p] -= fit * w; left -= fit;
                                take += fit; _dTakeValue += (long)it.Value * fit;
                            }
                        }
                        if (left > 0)
                        {
                            burn += left; _dBurnValue += (long)it.Value * left;
                            ArmsBook.Sink(ArmsBook.KRaid, it, left);                   // uzbrojenie - ujscie z przyczyna (E6/0); towar handlowy - ramka FRaid ksiegi 146
                        }
                    }
                }
                finally { GoodsLedger.End(gf); }
                long v1 = Pieces(roster), p1 = 0;
                foreach (var mp in parties) p1 += Pieces(mp.ItemRoster);
                if (rg >= 0) { long tv = 0; for (int k = 0; k < mvP.Count; k++) tv += (long)mvEl[k].Item.Value * mvN[k]; ReaverLaw.NoteGoods(rg, take, tv); }   // WW1: towar najezdzcow
                _dTake += take; _dBurn += burn;
                ss.STake += take; ss.SBurn += burn;
                _dBal2 += (p1 - p0) + burn - (v0 - v1);                          // niezalezny bilans: towar napastnika + spalone - ubytek magazynu
                for (int k = 0; k < mvP.Count; k++) got[mvP[k]].AddToCounts(mvEl[k], mvN[k]);   // lista zdarzenia - juz poza ramka FRaid
                for (int p = 0; p < parties.Count; p++)
                {
                    if (got[p].Count == 0) continue;
                    try { CampaignEventDispatcher.Instance.OnItemsLooted(parties[p], got[p]); }   // jak oryginal (G :302-305): znaczniki nowych przedmiotow gry
                    catch (Exception e) { Stumble("Loot(OnItemsLooted)", e); }
                }
                // lista zyla tylko na czas zdarzenia (sluchacze gry jej nie trzymaja: znaczniki gracza, XP rabunku): zdjecie wpisow znosi ja do zera
                // takze w ramce ZEWNETRZNEJ ksiegi (koniec rabunku wywolany z ticku dobowego partii / osady) - bez ramki podsluch konczy sie od razu
                for (int k = 0; k < mvP.Count; k++) got[mvP[k]].AddToCounts(mvEl[k], -mvN[k]);
            }
            catch (Exception e) { Stumble("Loot(towar)", e); }
        }

        private static bool Walks(ItemObject it) { return it != null && (it.HasHorseComponent || it.IsAnimal); }
        private static double PerKg(ItemObject it) { return it == null ? 0.0 : it.Value / Math.Max(0.1, (double)it.Weight); }

        // ------------------------------------------------------------ koniec rabunku: RaidEventComponent.OnBeforeFinalize - prefiks i finalizer Priority.First
        public static void EndPre(RaidEventComponent __instance, out EndCtx __state)
        {
            __state = null;
            Sess ss;
            if (__instance == null || !_sess.TryGetValue(__instance, out ss)) return;
            try
            {
                if (ss.Started && !ss.Done)
                {
                    // przerwany krok (AI, odsiecz, pokoj): czesc kroku w ksiedze, lup za zrobiona czesc - gdy napastnik nie przegral; bez czujnosci (punkty > 0 -> Normal)
                    double rest = Math.Min(ss.Prog, ss.Work) - ss.Struck;
                    if (rest > 1e-9) StrikeChunk(ss, rest, __instance);
                    var att = __instance.AttackerSide;
                    bool lost = __instance.BattleState == BattleState.DefenderVictory || att == null || att.TroopCount <= 0;
                    if (!lost) Loot(ss, __instance);
                    // kara relacji gry za zrobiona czesc (K3); E7/R2: po >= 1 pelnym kroku gracza zostaje 1 (osada juz spalona)
                    _setDmg(__instance, ss.Steps > 0 ? 1f : (ss.Work > 0.0 ? (float)Math.Min(1.0, ss.Prog / ss.Work) : 0f));
                }
                __state = new EndCtx { Ss = ss, Cap0 = Devastation.CapTakenAll, RaidDead = Devastation.RaidDead(__instance) };
            }
            catch (Exception e) { Stumble("EndPre", e); }
        }

        public static Exception EndFin(RaidEventComponent __instance, EndCtx __state, Exception __exception)
        {
            try
            {
                if (__state != null && __state.Ss != null && __state.Ss.Started)
                {
                    var ss = __state.Ss;
                    _dEnded++;
                    if (ss.Player) _dPEnded++;
                    _dRaidDead += __state.RaidDead; _dCheckDead += ss.Dead;
                    _dCap += Devastation.CapTakenAll - __state.Cap0;              // jency E4/9 z tej sesji (BK ExecuteCapture na RaidCompleted -> ThrallLaw)
                    try { ReaverLaw.NoteCaptives(ReaverLaw.RaidGroup(__instance.AttackerSide != null ? __instance.AttackerSide.LeaderParty : null), Devastation.CapTakenAll - __state.Cap0); } catch { }   // WW1: thralle najezdzcow (tylko licznik)
                    if (ss.Done)
                    {
                        if (ss.Player) _dPWon++;                                  // E7/R2: kroki gracza policzone przy kazdym pelnym kroku (PlayerStepDone); mediana lordow - tylko AI
                        else
                        {
                            _dSteps++; _dSettl += ss.M;
                            if (ss.Lord && ss.M >= 1.0 && _dDaysPer.Count < 4096) _dDaysPer.Add(ss.LootDays / ss.M);
                        }
                        var v = ss.V;
                        if (v != null && v.VillageState == Village.VillageStates.Looted && v.Settlement != null && v.Settlement.StringId != null)
                        {
                            var s = Settings.Current;
                            double d = ss.L0 > 0.0 ? ss.Hit / ss.L0 : 0.0;
                            int days = (int)Math.Ceiling(1.0 + 100.0 * d);
                            days = Math.Max(1, Math.Min(Math.Max(1, Math.Min(60, s != null ? s.RaidAlertMaxDays : 17)), days));
                            _alert[v.Settlement.StringId] = Today() + days;
                            _dAlertNew++;
                        }
                    }
                    else
                    {
                        _dInterrupted++;
                        if (ss.Player) { if (ss.Steps > 0) _dPBroken++; else _dPCalled++; }
                    }
                }
            }
            catch (Exception e) { Stumble("EndFin", e); }
            finally
            {
                if (__instance != null) { _sess.Remove(__instance); _notMine.Remove(__instance); }
                if (_ask != null && ReferenceEquals(_ask.Comp, __instance)) _ask = null;
            }
            return __exception;
        }

        // ------------------------------------------------------------ E7/R2 (236): okienko gracza po kazdej osadzie
        /// <summary>
        /// Postfiks VillageHostileActionCampaignBehavior.wait_menu_raiding_village_on_tick (G :504 - tick menu oczekiwania "raiding_village"; GameMenu.RunOnTick wola go
        /// tylko przy czynnym oczekiwaniu, co klatke). Zwykla klatka: jedno porownanie z null. Gdy sesja gracza czeka na decyzje - otwiera okienko (wzorzec NightRest:
        /// flaga, okienko z ticku; ZADNEGO SwitchToMenu - CLAUDE.md 7). Okienko z pauza zatrzymuje MapState (OnIdleTick), wiec tick nie biegnie, dopoki stoi;
        /// _askOpen przy ticku = okienko czeka w kolejce za innym albo ktos je zamknal bez wyboru (HideInquiry) - wtedy otwieramy znowu, najwyzej MaxStumbles razy
        /// na krok (potem ta sesja konczy jak AI - licznik "okienko zawiodlo", nic sie nie gasi).
        /// </summary>
        public static void MenuTickPostfix()
        {
            var ss = _ask;
            if (ss == null) return;
            try
            {
                if (!ss.Ask || ss.Done || !Owns(ss.Comp)) { _ask = null; return; }       // sesja skonczona albo oddana grze
                if (ss.Choice != 0 || !PopupOn) return;                                   // wybor czeka na tick kampanii; klucz zdjety - prefiks kroku konczy jak AI
                if (_askOpen)
                {
                    if (InformationManager.IsAnyInquiryActive()) return;
                    _askOpen = false;                                                     // zamkniete bez wyboru - nieudana proba (bez petli otwierania co klatke)
                    if (++ss.AskFails >= MaxStumbles) { ss.Choice = ChoiceEnd; _dAskFail++; return; }
                }
                var me = MapEvent.PlayerMapEvent;
                if (me == null || !ReferenceEquals(me.Component, ss.Comp) || me.DiplomaticallyFinished) return;   // pokoj: Update nie biegnie - gracz konczy opcja gry
                ShowStep(ss);
            }
            catch (Exception e)
            {
                _askOpen = false;
                Stumble("R2.MenuTick", e);
                if (++ss.AskFails >= MaxStumbles && ss.Choice == 0) { ss.Choice = ChoiceEnd; _dAskFail++; }   // okienko zawodzi - ta sesja konczy jak AI (bez wylacznika)
            }
        }

        private static string DaysText(double days)
        {
            double d = Math.Max(0.1, days);
            return d < 10.0 ? F(d, "0.#") : F(d, "0");
        }

        /// <summary>Okienko pelnego kroku gracza: liczby kroku z sesji (po wczytaniu - z zapisu), wioska z ogniem i jej osady z ksiegi wiosek, plan nastepnego kroku.
        /// Tylko odczyt; przyciski ustawiaja wybor w sesji (wykonuje go prefiks kroku w ticku kampanii).</summary>
        private static void ShowStep(Sess ss)
        {
            var s = Settings.Current;
            var v = ss.V; var comp = ss.Comp;
            var att = comp.AttackerSide;
            int men = att != null ? att.TroopCount : 0;
            var p = Plan(v, men);
            double rate = Math.Max(0f, s.DevastationPerRaiderDay) * men * ss.F * p.U;          // ludzi przeszukanych na dobe - ta sama regula co krok (R x N x f x u)
            bool next = !p.BurnedOut && p.M >= 1.0 && p.Work > 0.0 && rate > 0.0;
            string vname, dname; int burned, n; bool sb, wb;
            if (!VillageBook.LootNews(v, comp, out vname, out dname, out burned, out n, out sb, out wb) || string.IsNullOrEmpty(vname))
            {
                // ksiega wiosek nieczynna: wies gry i osady okregu z ludzi
                vname = v.Settlement != null && v.Settlement.Name != null ? v.Settlement.Name.ToString() : "?";
                n = Math.Max(1, (int)Math.Round(ss.Sv > 0.0 ? ss.L0 / ss.Sv : 1.0));
                burned = Math.Max(0, Math.Min(n, n - (int)Math.Round(ss.Sv > 0.0 ? p.Home / ss.Sv : n)));
            }
            int people = Math.Max(0, (int)Math.Round(ss.SHit));
            int k = Math.Max(0, Math.Min(people, (int)Math.Round(ss.SDead)));
            int o = Math.Max(0, Math.Min(people - k, (int)Math.Round(ss.SFled)));
            int c = Devastation.CaptivesLive ? Math.Max(0, Math.Min(people - k - o, (int)Math.Round(ss.SCap))) : 0;   // plan E4/9: do lochu przy koncu wygranego rabunku
            int r = people - k - o - c;
            string items = ss.STake <= 0 && ss.SBurn <= 0 ? VillageTexts.Make(VillageTexts.RaidItemsNone).ToString()
                         : ss.SBurn > 0 ? VillageTexts.Make(VillageTexts.RaidItemsBurned, "TAKEN", (int)ss.STake, "BURNED", (int)ss.SBurn).ToString()
                         : VillageTexts.Make(VillageTexts.RaidItems, "TAKEN", (int)ss.STake).ToString();
            double home = ss.Home0;
            string lost = home > 0.0 ? F((ss.SFoodBurn + ss.SFoodEat) / home, "0.0") : "0";      // dni jedzenia gospodarzy (racje / ludzie w domu - jak Famine.DaysOf)
            string ate = home > 0.0 ? F(ss.SFoodEat / home, "0.0") : "0";
            int m = Math.Max(1, (int)Math.Round(ss.M));
            string title = VillageTexts.Make(VillageTexts.RaidStepTitle, "VILLAGE", vname).ToString();
            string body = VillageTexts.Make(m >= 2 ? VillageTexts.RaidStepBodyMany : VillageTexts.RaidStepBody, "VILLAGE", vname, "M", m, "PEOPLE", people, "K", k, "C", c,
                                            "R", r, "O", o, "ITEMS", items, "GOLD", (int)ss.SGold, "DAYS_FOOD", lost, "FOOD", ate,
                                            "STANDING", Math.Max(0, n - burned), "N", n).ToString();
            if (!next) body += " " + VillageTexts.Make(VillageTexts.RaidStepNoMore).ToString();
            string end = VillageTexts.Make(VillageTexts.RaidEnd).ToString();
            string nxt = null;
            if (next)
            {
                int pm = Math.Max(1, (int)Math.Round(p.M));
                nxt = VillageTexts.Make(pm >= 2 ? VillageTexts.RaidNextMany : VillageTexts.RaidNext, "M", pm, "DAYS", DaysText(p.Work / rate)).ToString();
                // n20-napisy: przycisk okna ma 164 px - dawny napis ("Raid the next settlement (about N days)") idzie do tekstu okna, na przycisku "Raid on"
                if (ShortLabels.On) { body += "\n\n" + nxt + "."; nxt = VillageTexts.Make(VillageTexts.RaidNextShort).ToString(); }
            }
            if (ss.Shown) _dShownAgain++; else { _dShown++; if (!next) _dNoNext++; }
            ss.Shown = true;
            _askOpen = true;
            // Enter = "End the raid" (przycisk twierdzacy), Escape = "Raid the next settlement" (RB 4 pkt 4: odwracalne - "End Raiding" jest zawsze w menu);
            // bez nastepnej osady sam przycisk "End the raid" (Escape nic nie robi)
            Action onEnd = delegate { _askOpen = false; if (ss.Ask && ss.Choice == 0) { ss.Choice = ChoiceEnd; _dEndPopup++; } };
            Action onNext = null;
            if (next) onNext = delegate { _askOpen = false; if (ss.Ask && ss.Choice == 0) { ss.Choice = ChoiceNext; _dNext++; } };
            InformationManager.ShowInquiry(new InquiryData(title, body, true, next, end, nxt, onEnd, onNext), true);
        }

        /// <summary>
        /// Prefiks VOID VillageHostileActionCampaignBehavior.wait_menu_end_raiding_on_consequence (G :381-385) - jedna metoda gry dla opcji "End Raiding" i dla konca
        /// paska menu (G :99-100). Oryginal (PlayerEncounter.Finish) biegnie ZAWSZE - tu tylko: gracz konczy opcja po >= 1 spalonej osadzie w tej sesji (takze w srodku
        /// nastepnego kroku albo w stanie decyzji) = "End the raid": czesc biezacego kroku do ksiegi, lup za nia, koniec gry (punkty 0, AttackerVictory), wiec Finish
        /// finalizuje zdarzenie jako pelny rabunek (wies Looted, RaidCompleted napastnika, czujnosc). Inaczej zdarzenie konczyloby sie "odwolane" (wies Normal) mimo
        /// spalonych osad (K2). Nie dotyczy: sesja juz zakonczona (pasek 100%), 0 osad (gra: odwolany), wodz AI, walka z obroncami, pokoj.
        /// </summary>
        public static void EndRaidingPrefix()
        {
            try
            {
                var me = MapEvent.PlayerMapEvent;
                var comp = me != null ? me.Component as RaidEventComponent : null;
                if (comp == null) return;
                var ss = Decide(comp);                       // takze sesja z zapisu, zanim pobiegl pierwszy tick mapy
                if (ss == null || !ss.Player || !ss.Started || ss.Done) return;
                var def = comp.DefenderSide; var att = comp.AttackerSide;
                if (def == null || att == null || def.TroopCount > 0 || att.TroopCount <= 0 || me.DiplomaticallyFinished || me.HasWinner) return;
                if (ss.Steps < 1) { _dEndMenu0++; return; }
                double rest = Math.Min(ss.Prog, ss.Work) - ss.Struck;
                if (!ss.Ask && rest > 1e-9) StrikeChunk(ss, rest, comp);
                Loot(ss, comp);
                Win(ss, comp, me, ss.V.Settlement);
                _dEndMenu++;
            }
            catch (Exception e) { Stumble("R2.EndRaiding", e); }
        }

        // ------------------------------------------------------------ czujnosc: VillageHealCampaignBehavior.DailyTickSettlement - prefiks (tylko odklada leczenie)
        public static bool HealPrefix(Settlement settlement)
        {
            try
            {
                // recenzja E7/R1: wylacznik zdjety (MCM) - wies leczy sie jak w grze takze z czujnoscia z zapisu albo sprzed zdjecia (off = stan sprzed R1)
                if (_alert.Count == 0 || !On || settlement == null || !settlement.IsVillage || settlement.StringId == null) return true;
                int end;
                if (!_alert.TryGetValue(settlement.StringId, out end)) return true;
                var v = settlement.Village;
                if (Today() < end && v != null && v.VillageState == Village.VillageStates.Looted) { _dHealHeld++; return false; }
                return true;
            }
            catch (Exception e) { Stumble("HealPrefix", e); return true; }
        }

        // ------------------------------------------------------------ zapis (VillageBook.Export / ApplyPending)
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf('/') >= 0; }

        /// <summary>"|r:id=hp0/prog/work/m/u/l0/home0/sv/struck/pd/hit/dead/fled/ref/capw/eat/loot/f/flagi[/kroki/osady/shit/sdead/sfled/sref/scap/sspal/szjedz/szloto/stowar/sspalone];...
        /// |c:id=doba;..." - sesje w toku i czujnosc. E7/R2: 12 pol kroku gracza po flagach (zapis R1 z 19 polami wczytuje sie dalej), flaga 16 = czeka na decyzje,
        /// 32 = zakonczona, a zdarzenie gracza jeszcze czeka na konsekwencje menu gry (u AI sesja zakonczona znika w tym samym ticku - do zapisu nie trafia).</summary>
        internal static void Export(StringBuilder sb)
        {
            int nr = 0, nc = 0;
            sb.Append("|r:");
            bool first = true;
            foreach (var ss in _sess.Values)
            {
                if (ss == null || !ss.Started || ss.V == null || ss.V.Settlement == null || BadId(ss.V.Settlement.StringId)) continue;
                var rs = ss.Rs;
                if (!first) sb.Append(';');
                first = false;
                int flags = (ss.BurnedOut ? 1 : 0) | (ss.UClamped ? 2 : 0) | (ss.Lord ? 4 : 0) | (ss.LootGiven ? 8 : 0) | (ss.Ask ? 16 : 0) | (ss.Done ? 32 : 0);
                sb.Append(ss.V.Settlement.StringId).Append('=')
                  .Append(F(ss.Hp0, "R")).Append('/').Append(F(ss.Prog, "R")).Append('/').Append(F(ss.Work, "R")).Append('/').Append(F(ss.M, "R")).Append('/')
                  .Append(F(ss.U, "R")).Append('/').Append(F(ss.L0, "R")).Append('/').Append(F(ss.Home0, "R")).Append('/').Append(F(ss.Sv, "R")).Append('/')
                  .Append(F(ss.Struck, "R")).Append('/').Append(F(ss.PdPend, "R")).Append('/').Append(F(ss.Hit, "R")).Append('/').Append(F(ss.Dead, "R")).Append('/')
                  .Append(F(ss.Fled, "R")).Append('/').Append(F(ss.Ref, "R")).Append('/').Append(F(rs != null ? rs.CapWould : 0.0, "R")).Append('/')
                  .Append(F(ss.EatDays, "R")).Append('/').Append(F(ss.LootDays, "R")).Append('/').Append(F(ss.F, "R")).Append('/').Append(flags.ToString(Inv))
                  .Append('/').Append(ss.Steps.ToString(Inv)).Append('/').Append(F(ss.Settl, "R")).Append('/').Append(F(ss.SHit, "R")).Append('/').Append(F(ss.SDead, "R"))
                  .Append('/').Append(F(ss.SFled, "R")).Append('/').Append(F(ss.SRef, "R")).Append('/').Append(F(ss.SCap, "R")).Append('/').Append(F(ss.SFoodBurn, "R"))
                  .Append('/').Append(F(ss.SFoodEat, "R")).Append('/').Append(ss.SGold.ToString(Inv)).Append('/').Append(ss.STake.ToString(Inv)).Append('/').Append(ss.SBurn.ToString(Inv));
                nr++;
            }
            foreach (var kv in _pendingR)                                       // wczytane, a jeszcze nieodebrane (zapis zaraz po wczytaniu) - nie gubimy
            {
                if (BadId(kv.Key)) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value);
                nr++;
            }
            sb.Append("|c:"); first = true;
            int today = Today();
            foreach (var kv in _alert)
            {
                if (BadId(kv.Key) || kv.Value <= today) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                nc++;
            }
            try { var s = Settings.Current; if (s != null && s.LogEnabled) Log.Info("Rabunek osada po osadzie (E7/R1): zapis - sesji " + nr + ", czujnosc " + nc + " wsi."); }
            catch { }
        }

        /// <summary>Wczytanie (VillageBook.ApplyPending - po Reset konstruktora): sesje czekaja na swoj rabunek (pierwsze wywolanie prefiksu przy tej wsi), czujnosc od razu.</summary>
        internal static void Import(string sr, string sc)
        {
            _pendingR.Clear(); _alert.Clear(); _pendingDays = 0;
            int bad = 0;
            if (!string.IsNullOrEmpty(sr))
                foreach (var item in sr.Split(';'))
                {
                    int eq = item.IndexOf('=');
                    if (eq <= 0) { if (item.Length > 0) bad++; continue; }
                    string id = item.Substring(0, eq), body = item.Substring(eq + 1);
                    int nf = body.Split('/').Length;
                    if (nf != FieldsR1 && nf != FieldsR2) { bad++; continue; }        // 19 pol = zapis E7/R1, 31 = z krokiem gracza (E7/R2)
                    _pendingR[id] = body;
                }
            if (!string.IsNullOrEmpty(sc))
                foreach (var item in sc.Split(';'))
                {
                    int eq = item.IndexOf('='), end;
                    if (eq <= 0 || !int.TryParse(item.Substring(eq + 1), NumberStyles.Integer, Inv, out end)) { if (item.Length > 0) bad++; continue; }
                    _alert[item.Substring(0, eq)] = end;
                }
            try
            {
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                    Log.Info("Rabunek osada po osadzie (E7/R1): wczytanie - sesji " + _pendingR.Count + ", czujnosc " + _alert.Count + " wsi" + (bad > 0 ? ", zepsute wpisy " + bad : "")
                             + (sr == null && sc == null ? " (zapis bez sekcji r:/c: - stary zapis albo bez rabunkow)" : "") + ".");
            }
            catch { }
        }

        private static void Restore(Sess ss)
        {
            var st = ss.V != null ? ss.V.Settlement : null;
            string body;
            if (st == null || st.StringId == null || !_pendingR.TryGetValue(st.StringId, out body)) return;
            _pendingR.Remove(st.StringId);
            var a = body.Split('/');
            var x = new double[18];
            for (int i = 0; i < 18; i++)
                if (!double.TryParse(a[i], NumberStyles.Float, Inv, out x[i]) || double.IsNaN(x[i]) || double.IsInfinity(x[i])) return;   // zepsuty wpis - sesja od nowa z punktow gry
            int flags;
            if (!int.TryParse(a[18], NumberStyles.Integer, Inv, out flags)) return;
            // E7/R2: 12 pol kroku gracza po flagach; zapis E7/R1 (19 pol) - jedyny krok = sesja
            bool r2 = a.Length >= FieldsR2;
            var y = new double[FieldsR2 - FieldsR1];
            if (r2)
                for (int i = 0; i < y.Length; i++)
                    if (!double.TryParse(a[FieldsR1 + i], NumberStyles.Float, Inv, out y[i]) || double.IsNaN(y[i]) || double.IsInfinity(y[i])) return;   // zepsuty wpis - sesja od nowa
            ss.Hp0 = (float)Math.Max(0.01, Math.Min(1.0, x[0])); ss.Prog = Math.Max(0.0, x[1]); ss.Work = Math.Max(0.0, x[2]); ss.M = Math.Max(0.0, x[3]);
            ss.U = Math.Max(UFloor, Math.Min(1.0, x[4])); ss.L0 = x[5]; ss.Home0 = x[6]; ss.Sv = x[7]; ss.Struck = Math.Max(0.0, x[8]); ss.PdPend = Math.Max(0.0, x[9]);
            ss.Hit = x[10]; ss.Dead = x[11]; ss.Fled = x[12]; ss.Ref = x[13];
            ss.EatDays = Math.Max(0.0, x[15]); ss.LootDays = Math.Max(0.0, x[16]); ss.F = x[17] > 0.0 ? x[17] : 1.0;
            ss.BurnedOut = (flags & 1) != 0; ss.UClamped = (flags & 2) != 0; ss.Lord = (flags & 4) != 0; ss.LootGiven = (flags & 8) != 0;
            if (r2)
            {
                ss.Steps = (int)Math.Max(0.0, Math.Min(10000.0, y[0])); ss.Settl = Math.Max(0.0, y[1]);
                ss.SHit = Math.Max(0.0, y[2]); ss.SDead = Math.Max(0.0, y[3]); ss.SFled = Math.Max(0.0, y[4]); ss.SRef = Math.Max(0.0, y[5]); ss.SCap = Math.Max(0.0, y[6]);
                ss.SFoodBurn = Math.Max(0.0, y[7]); ss.SFoodEat = Math.Max(0.0, y[8]);
                ss.SGold = (long)Math.Max(0.0, y[9]); ss.STake = (long)Math.Max(0.0, y[10]); ss.SBurn = (long)Math.Max(0.0, y[11]);
                ss.Done = (flags & 32) != 0;                              // zdarzenie gracza zapisane miedzy koncem sesji a konsekwencja menu - prefiks dalej trzyma finish
                ss.Ask = ss.Player && !ss.Done && (flags & 16) != 0;      // stan decyzji tylko u gracza-wodza (Decide ustawia Player przed Restore)
            }
            else { ss.SHit = ss.Hit; ss.SDead = ss.Dead; ss.SFled = ss.Fled; ss.SRef = ss.Ref; ss.SCap = Math.Max(0.0, x[14]); }
            ss.Started = true; ss.Restored = true;
            if (ss.Rs != null) { ss.Rs.Hit = ss.Hit; ss.Rs.Dead = ss.Dead; ss.Rs.CapWould = Math.Max(0.0, x[14]); }   // stan rabunku bez zapisu - odtworzony z sesji (jency E4/9, Inni)
            _dRestored++;
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior - po VillageNews.Daily)
        internal static void Daily()
        {
            try
            {
                int today = Today();
                // czujnosc: wpisy po terminie albo wsie juz nie Looted
                if (_alert.Count > 0)
                {
                    var drop = new List<string>();
                    foreach (var kv in _alert) if (kv.Value <= today) drop.Add(kv.Key);
                    foreach (var k in drop) _alert.Remove(k);
                }
                // rabunki zakonczone bez OnBeforeFinalize (nie powinno sie zdarzyc) - bez wiszacych wpisow
                if (_notMine.Count > 0)
                {
                    var gone = new List<RaidEventComponent>();
                    foreach (var c in _notMine) { var me = c != null ? c.MapEvent : null; if (me == null || me.IsFinalized) gone.Add(c); }
                    foreach (var c in gone) _notMine.Remove(c);
                }
                if (_sess.Count > 0)
                {
                    var gone = new List<RaidEventComponent>();
                    foreach (var kv in _sess) { var me = kv.Key != null ? kv.Key.MapEvent : null; if (me == null || me.IsFinalized) gone.Add(kv.Key); }
                    foreach (var c in gone) { _sess.Remove(c); _dAbandoned++; }
                }
                int unclaimed = 0;
                if (_pendingR.Count > 0 && ++_pendingDays >= 2) { unclaimed = _pendingR.Count; _pendingR.Clear(); }   // sesje z zapisu bez rabunku po wczytaniu
                Line(today, unclaimed);
                LineR2(today);
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { NewDay(); }
        }

        private static double Median(List<double> xs)
        {
            if (xs.Count == 0) return double.NaN;
            var a = new List<double>(xs); a.Sort();
            int n = a.Count;
            return (n % 2) == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        private static void Line(int today, int unclaimed)
        {
            var s = Settings.Current;
            if (s == null || !s.LogEnabled) return;
            int day = today - 1;
            if (!On)
            {
                if (!_offLogged)
                {
                    _offLogged = true;
                    Log.Info("Rabunek osada po osadzie (E7/R1): dzien " + day + " | WYLACZONE (" + (!s.RaidBySettlementEnabled ? "Raid By Settlement Enabled w MCM" : "latki niewpiete: " + string.Join(", ", _missing.ToArray()))
                             + ") - rabunek AI jak w E4/5 (spustoszenie od osobodni, lup z kiesy wsi E5/A7).");
                }
                if (_sess.Count == 0 && _alert.Count == 0 && _dEnded == 0) return;
            }
            double check = _dHit - _dDead - _dFled - _dRef;
            double med = Median(_dDaysPer);
            int act = 0, actR = 0;
            foreach (var ss in _sess.Values) if (ss != null && ss.Started && !ss.Done) { act++; if (ss.Restored) actR++; }
            var sb = new StringBuilder(1600);
            sb.Append("Rabunek osada po osadzie (E7/R1): dzien ").Append(day)
              .Append(" | sesji ").Append(_dEnded).Append(" (AI ").Append(_dEnded - _dPEnded).Append(", gracz ").Append(_dPEnded).Append(')')
              .Append(", krokow ").Append(_dSteps).Append(" (osad ").Append(F(_dSettl, "0")).Append(')')
              .Append(", dni na osade mediana ").Append(double.IsNaN(med) ? "-" : F(med, "0.00")).Append(" (lordowie ").Append(_dDaysPer.Count);
            if (_dDaysPer.Count > 0)
            {
                sb.Append(": ");
                for (int i = 0; i < _dDaysPer.Count && i < MaxDaysList; i++) sb.Append(i > 0 ? "/" : "").Append(F(_dDaysPer[i], "0.00"));
            }
            sb.Append(')')
              .Append(", przerwanych ").Append(_dInterrupted).Append(" (").Append(_dEnded > 0 ? F(100.0 * _dInterrupted / _dEnded, "0") : "0").Append("%)");
            sb.Append(" | trafieni ").Append(F(_dHit, "0.##")).Append(" = zabici ").Append(F(_dDead, "0.##")).Append(" + las ").Append(F(_dFled, "0.##"))
              .Append(" + jency 0 + uchodzcy ").Append(F(_dRef, "0.##")).Append(" (kontrola ").Append(F(check, "0.000")).Append("; uderzen ").Append(_dStrikes).Append(')');
            sb.Append(" | lup: zloto z kiesy wsi ").Append(_dGold).Append(" zl, towar z magazynu ").Append(_dTake).Append(" szt. (").Append(_dTakeValue).Append(" zl), spalone w rabunku ")
              .Append(_dBurn).Append(" szt. (").Append(_dBurnValue).Append(" zl), jedzenie ze spichlerza ").Append(_dEat).Append(" szt. (").Append(F(_dEat * RationsPerGrain, "0")).Append(" racji)");
            sb.Append(" | bilans (v2): zloto wodza - kiesa wsi ").Append(F(_dBal1, "0")).Append(", towar + spalone - magazyn ").Append(F(_dBal2, "0"))
              .Append(", zjedzone - spichlerz ").Append(F(_dBal3, "0.###"));
            sb.Append(" | jency z rabunkow R1 ").Append(_dCap).Append(", zabici w stanie rabunku (RaidDead) ").Append(F(_dRaidDead, "0.#")).Append(" (zabici R1 ").Append(F(_dCheckDead, "0.#")).Append(')');
            sb.Append(" | czujnosc: wsi ").Append(_alert.Count).Append(" (nowe ").Append(_dAlertNew).Append(", leczenie wstrzymane ").Append(_dHealHeld).Append(')');
            sb.Append(" | w toku: sesji ").Append(act).Append(" (z zapisu ").Append(actR).Append("), nowych ").Append(_dStarted).Append(", podjetych z zapisu ").Append(_dRestored);
            if (unclaimed > 0) sb.Append(", z zapisu bez rabunku ").Append(unclaimed);
            sb.Append(" | okreg wypalony ").Append(_dBurnedOut).Append(", u przyciete do ").Append(F(UFloor, "0.00")).Append(' ').Append(_dUClamped)
              .Append(", oddane grze (potkniecia) ").Append(_dReleased);
            if (_dAbandoned > 0) sb.Append(", porzucone bez konca rabunku ").Append(_dAbandoned);
            sb.Append(" | czas ").Append(F(_dTicks * 1000.0 / Stopwatch.Frequency, "0.0")).Append(" ms");
            sb.Append(" | potkniecia ").Append(_dStumbles).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>E7/R2: linia doby okienka gracza - tylko w dobach, w ktorych gracz rabowal (albo jego sesja jest w toku).</summary>
        private static void LineR2(int today)
        {
            var s = Settings.Current;
            if (s == null || !s.LogEnabled) return;
            int live = 0, wait = 0;
            foreach (var ss in _sess.Values) if (ss != null && ss.Player && ss.Started && !ss.Done) { live++; if (ss.Ask) wait++; }
            int any = _dPStarted + _dPEnded + _dPSteps + _dAsk + _dShown + _dShownAgain + _dNext + _dEndPopup + _dEndMenu + _dEndMenu0 + _dAutoEnd + _dAskFail;
            if (any == 0 && live == 0) return;
            var sb = new StringBuilder(700);
            sb.Append("Okienko rabunku gracza (E7/R2): dzien ").Append(today - 1)
              .Append(" | okienko ").Append(PopupOn ? "TAK" : (!s.RaidPlayerStepPopup ? "NIE (Raid Player Step Popup w MCM - gracz jak AI)" : "NIE (latki niewpiete - gracz jak AI)"))
              .Append(" | sesji gracza: nowych ").Append(_dPStarted).Append(", zakonczonych ").Append(_dPEnded).Append(" (spalone ").Append(_dPWon)
              .Append(", odwolane przy 0 osadach ").Append(_dPCalled).Append(", przerwane po >= 1 osadzie ").Append(_dPBroken).Append(')')
              .Append(" | krokow pelnych ").Append(_dPSteps).Append(" (osad ").Append(F(_dPSettl, "0")).Append(')')
              .Append(" | okienek ").Append(_dShown).Append(" z ").Append(_dAsk).Append(" decyzji (ponownie ").Append(_dShownAgain).Append(", bez nastepnej osady ").Append(_dNoNext).Append(')')
              .Append(" | wybory: nastepna ").Append(_dNext).Append(", koniec ").Append(_dEndPopup)
              .Append("; End Raiding po >= 1 osadzie ").Append(_dEndMenu).Append(", przy 0 osadach ").Append(_dEndMenu0)
              .Append("; koniec bez okienka (klucz zdjety w decyzji) ").Append(_dAutoEnd).Append(", okienko zawiodlo ").Append(_dAskFail)
              .Append(" | w toku ").Append(live).Append(" (w decyzji ").Append(wait).Append(").");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ wpiecia (SubModuleMain - po Devastation)
        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear(); _ready = false;
            if (h == null) { _missing.Add("Harmony"); StartLine(); return; }
            try
            {
                var hp = AccessTools.PropertySetter(typeof(Settlement), "SettlementHitPoints");
                var dmg = AccessTools.PropertySetter(typeof(RaidEventComponent), "RaidDamage");
                var bs = AccessTools.PropertySetter(typeof(MapEvent), "BattleState");
                if (hp != null) _setHp = (Action<Settlement, float>)Delegate.CreateDelegate(typeof(Action<Settlement, float>), hp); else _missing.Add("Settlement.SettlementHitPoints (setter)");
                if (dmg != null) _setDmg = (Action<RaidEventComponent, float>)Delegate.CreateDelegate(typeof(Action<RaidEventComponent, float>), dmg); else _missing.Add("RaidEventComponent.RaidDamage (setter)");
                if (bs != null) _setBs = (Action<MapEvent, BattleState>)Delegate.CreateDelegate(typeof(Action<MapEvent, BattleState>), bs); else _missing.Add("MapEvent.BattleState (setter)");
            }
            catch (Exception e) { _missing.Add("settery (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(settery)", e); }
            if (_setHp == null || _setDmg == null || _setBs == null) { StartLine(); return; }
            bool upd = false, end = false;
            try
            {
                var m = AccessTools.Method(typeof(RaidEventComponent), "Update", new[] { typeof(bool).MakeByRefType() });
                if (m != null && m.DeclaringType == typeof(RaidEventComponent))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(DistrictRaid), nameof(UpdatePrefix)) { priority = Priority.First });
                    upd = true; _wired.Add("krok rabunku (Update, First)");
                }
                else _missing.Add("RaidEventComponent.Update");
            }
            catch (Exception e) { _missing.Add("RaidEventComponent.Update (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(Update)", e); }
            if (upd)
            {
                try
                {
                    var m = AccessTools.Method(typeof(RaidEventComponent), "OnBeforeFinalize");
                    if (m != null && m.DeclaringType == typeof(RaidEventComponent) && m.GetParameters().Length == 0)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(DistrictRaid), nameof(EndPre)) { priority = Priority.First },
                                   finalizer: new HarmonyMethod(typeof(DistrictRaid), nameof(EndFin)) { priority = Priority.First });
                        end = true; _wired.Add("koniec rabunku (OnBeforeFinalize)");
                    }
                    else _missing.Add("RaidEventComponent.OnBeforeFinalize");
                }
                catch (Exception e) { _missing.Add("RaidEventComponent.OnBeforeFinalize (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(OnBeforeFinalize)", e); }
            }
            try
            {
                var m = AccessTools.Method(typeof(VillageHealCampaignBehavior), "DailyTickSettlement", new[] { typeof(Settlement) });
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(DistrictRaid), nameof(HealPrefix)));
                    _wired.Add("czujnosc (VillageHealCampaignBehavior.DailyTickSettlement)");
                }
                else _missing.Add("VillageHealCampaignBehavior.DailyTickSettlement (czujnosc nieczynna - wies leczy sie jak w grze)");
            }
            catch (Exception e) { _missing.Add("DailyTickSettlement (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyAll(czujnosc)", e); }
            // prefiks Update bez konca rabunku liczylby sesje bez rozliczenia - R1 czynne tylko z obiema latkami
            _ready = upd && end;
            StartLine();
            ApplyPopup(h);
        }

        /// <summary>
        /// E7/R2: dwie latki menu rabunku gracza (metody prywatne statyczne VillageHostileActionCampaignBehavior - klasa bez konstruktora statycznego, wiec wpiecie
        /// przy starcie gry jest bezpieczne; PeopleUnit lata te sama klase od E4). Okienko czynne tylko z OBIEMA: sam tick bez prefiksu "End Raiding" zostawialby
        /// rabunek po spalonej osadzie jako "odwolany" (K2). Brak - gracz jak AI (jeden krok i koniec), R1 bez zmian.
        /// </summary>
        private static void ApplyPopup(Harmony h)
        {
            _wired2.Clear(); _missing2.Clear(); _popupReady = false;
            bool tick = false, cons = false;
            if (!_ready) _missing2.Add("rabunek osada po osadzie (E7/R1) nieczynny");
            else
            {
                try
                {
                    var m = AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "wait_menu_raiding_village_on_tick", new[] { typeof(MenuCallbackArgs), typeof(CampaignTime) });
                    if (m != null && m.IsStatic)
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(DistrictRaid), nameof(MenuTickPostfix)));
                        tick = true; _wired2.Add("okienko (tick menu raiding_village)");
                    }
                    else _missing2.Add("VillageHostileActionCampaignBehavior.wait_menu_raiding_village_on_tick");
                }
                catch (Exception e) { _missing2.Add("wait_menu_raiding_village_on_tick (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyPopup(tick)", e); }
                try
                {
                    var m = AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "wait_menu_end_raiding_on_consequence", new[] { typeof(MenuCallbackArgs) });
                    if (m != null && m.IsStatic)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(DistrictRaid), nameof(EndRaidingPrefix)));
                        cons = true; _wired2.Add("End Raiding (wait_menu_end_raiding_on_consequence)");
                    }
                    else _missing2.Add("VillageHostileActionCampaignBehavior.wait_menu_end_raiding_on_consequence");
                }
                catch (Exception e) { _missing2.Add("wait_menu_end_raiding_on_consequence (" + e.GetType().Name + ")"); Log.Error("DistrictRaid.ApplyPopup(End Raiding)", e); }
            }
            _popupReady = _ready && tick && cons;
            var s = Settings.Current;
            Log.Info("Okienko rabunku gracza (E7/R2): latki przy starcie gry - " + (_popupReady ? "GOTOWE" : "NIECZYNNE (gracz rabuje jak AI: jeden krok i koniec)")
                     + "; wpiete: " + (_wired2.Count > 0 ? string.Join(", ", _wired2.ToArray()) : "nic")
                     + "; BRAK: " + (_missing2.Count > 0 ? string.Join(", ", _missing2.ToArray()) : "-")
                     + (s != null ? "; ustawienie startowe przed MCM: klucz " + (s.RaidPlayerStepPopup ? "TAK" : "NIE") : "") + ".");
        }

        private static void StartLine()
        {
            var s = Settings.Current;
            Log.Info("Rabunek osada po osadzie (E7/R1): latki przy starcie gry - " + (_ready ? "GOTOWE" : "NIECZYNNE") + "; wpiete: " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "nic")
                     + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + (s != null ? "; ustawienia startowe przed MCM (stan - linia doby): klucz " + (s.RaidBySettlementEnabled ? "TAK" : "NIE") + ", oddzial " + s.RaidDetachmentMen
                                    + " ludzi, czujnosc najwyzej " + s.RaidAlertMaxDays + " dob, R " + F(Math.Max(0f, s.DevastationPerRaiderDay), "0.##") + " czlowieka na zbrojnego na dobe" : "") + ".");
        }
    }
}
