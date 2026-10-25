using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 149 (25.10, fala "AI walczy o zycie", karta syntezy "142 Bohater walczy o zycie", decyzja Jeffa D4: lordowie AI = "Ostrozni pozycyjnie").
    /// Jeff 13:30: "przeciwnik zachowuje sie jak prawdziwy zywy czlowiek, nie chce dac sie zabic i zrobi wszystko w obszarze jego mozliwosci i skilli aby przetrwac,
    /// walczy o zycie! ... dopiero ta zasada peka gdy jest panika".
    /// Przyczyna (synteza, sprawdzone w kodzie): RBM wylacza formacje wodza (RBMAI.CanTeamHaveGeneralsFormationPatch: __result = false), wiec lordowie stoja w zwyklych
    /// szykach (zwykle konnicy) i szarzuja z nimi; nasze DuelistsInBattles (m113) robi ich smielszymi; od ran sie nie wycofuja (WoundedFleeHeroes = false, BrokenMen) -
    /// uciekaja dopiero ponizej AiFleeBelowPercent (FieldCraft, bohaterowie nie sa wylaczeni). Nikt nie pilnuje, czy bohater zostal sam.
    ///
    /// Regula (klucz HeroesKeepBehindTheirMen; tylko bitwa polowa - IsFieldBattle, rodzaj "bitwa", bez morza; wszystko na glownym watku z BattleSense, blok Night):
    ///  - bohater AI (Agent.IsHero, IsAIControlled, nie gracz, nie umarly, nie na smoku) obu stron, takze towarzysze gracza; w trybie ROZSADEK (przelacznik 139) i poza
    ///    odwrotem rannego 143 (Withdrawing - przy WoundedFleeHeroes TAK ranny bohater ponizej 20% idzie droga 143, nie nasza);
    ///  - NA TYL SZYKU (co 3 s): bohater w szyku o ukladzie LineFormation (takze kolo, czworobok, klin, klucz; bez TransposedLineFormation i kolumny) stoi w ostatnich
    ///    dwoch szeregach (w szyku dwuszeregowym - w drugim). Inaczej zamiana miejsc z najglebiej stojacym szeregowym (najblizszy rzad) przez publiczne
    ///    IFormationArrangement.SwitchUnitLocations - ta sama droga, ktora gra stawia gracza na tyl szyku (Formation.TryRelocatePlayerUnit ->
    ///    LineFormation.SwitchUnitLocationsWithBackMostUnit -> SwitchUnitLocations; RBM OverrideLineFormation tylko sprawdza, czy obaj sa czynni i w szyku).
    ///    SwitchUnitLocationsWithBackMostUnit NIE wprost: jego "ostatni" (prywatne GetLastUnit) bywa graczem albo innym bohaterem. Szyk z graczem pominiety (gra
    ///    sama stawia tam gracza na tyl - nie przepychamy sie); recenzja: takze szyk pod rozkazami gracza (HorsePursuit.PlayerForm - jedna definicja). Gra przesuwa ludzi z tarcza do przodu (LineFormation.SwitchFrontUnitTypesToFrontRows w
    ///    OnTickOccasionally) i zapelnia luki z tylu (RemoveUnit fillInTheGap) - kolejna zamiana liczona jako "ponowna", najwyzej SwapCap na bohatera na bitwe;
    ///  - SAMOTNY: 0 swoich (nie w PANICE) w 5 m i co najmniej 2 wrogow (nie uciekajacych) w 10 m przez 2 passy z rzedu (ok. 1 s) - wycofanie za swoj szyk,
    ///    postoj 6 s, potem wraca do walki (pozycja skryptowa zdjeta); nastepne samotne wycofanie najwczesniej po 10 s;
    ///  - RANNY ponizej 45% HP (Health / HealthLimit - ta sama miara co FieldCraft i BrokenMen): wycofanie za swoj szyk do konca bitwy; co 4 s cel na nowo (szyk sie
    ///    rusza - postoj idzie za nim); broni sie, gdy ktos do niego dojdzie (bez NoAttack);
    ///  - wycofanie = Agent.SetScriptedPosition(cel, bez NoAttack i DoNotRun) - bez Panic, bez okrzyku, bez natywnego odwrotu (IsRunningAway zostaje false: gra nie
    ///    wyprowadza go z pola - CanAgentRout, BannerCompanies i RBM widza go jak walczacego). Cel: tyl wlasnego szyku (CurrentPosition - Direction x (Depth/2 + 5 m,
    ///    konno 10 m)), potem tyl glownej sily strony (najwiekszy szyk pieszy od 8 ludzi), potem 3 m za najblizszym chorazym strony do 80 m; cel w granicach pola,
    ///    10 m od granicy, bez wroga (nie uciekajacego) w 10 m, z droga (Scene.GetPathDistanceBetweenPositions, najwyzej PathPerStep zapytan na krok). Bez
    ///    bezpiecznego miejsca - walczy dalej, proba przy nastepnej ocenie;
    ///  - koniec trybu: PANIKA (RoutKeeper.Break wola BeforeBreak PRZED Panic - pozycja zdjeta; z bolu ponizej 10%, panika gry 142), odwrot rannego 143
    ///    (RoutKeeper.Withdraw wola BeforeWithdraw PRZED pierwszym ruchem 143 - pozycja zdjeta; potem w passie juz tylko wzor HorsePursuit.Uncontrol: czlowieka
    ///    w 143 dotykamy tylko, gdy ucieka), gracz przejal, koniec bitwy (MissionEnded - pozycja zdjeta).
    ///  - kolejnosc w klatce: zachowania misji tykaja od konca listy (Mission.cs:3741-3744) - BattleSense (my) PRZED RoutKeeper (zlamani, 143) i BrokenMen.
    ///  - zrecznosc pojedynkowa (DuelistsInBattles, DuelAi, zrywy DuelCraft) bez zmian - nie dotykamy pol AI; formacji wodza RBM NIE przywracamy.
    /// Klucz NIE: ta sama ocena na sucho (kto bylby przestawiony, wycofania rannych i samotnych) - bez zamian i bez pozycji skryptowej; przelaczenie na NIE w trakcie
    /// bitwy konczy trwajace wycofania (recenzja: pozycja zdjeta, powod "klucz NIE w trakcie"). Linia "bohaterowie (149)" raz na
    /// bitwe (BattleSense.Clean, po "poscig (148)"). Wyjatek na bohaterze = licznik (RoutHold.SiteHeroes); po FailsOut bohater wypada (pozycja zdjeta) - bez globalnego
    /// wylacznika. Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings".
    /// </summary>
    internal static class HeroCaution
    {
        // ------------------------------------------------------------ stale (karta syntezy 142 / D4)
        internal const float WoundedBelow = 0.45f;   // ranny bohater ponizej 45% HP
        internal const float AllyRadius = 5f;        // samotny: 0 swoich w 5 m ...
        internal const float FoeRadius = 10f;        // ... i co najmniej FoeMin wrogow w 10 m
        internal const int FoeMin = 2;
        internal const int AloneHits = 2;            // tyle passow z rzedu (ok. 1 s) - bez reakcji na przejazd obok wroga
        internal const float PassEvery = 0.5f;       // ocena bohaterow
        internal const float StepEvery = 0.25f;      // dotarcie / postoj
        internal const float ScanEvery = 2f;         // spis bohaterow (posilki)
        internal const float RankEvery = 3f;         // na tyl szyku
        internal const float RefreshWounded = 4f;    // cel rannego na nowo (= RoutHold.RefreshEvery, jak 143)
        internal const float RefreshAlone = 1.5f;    // cel samotnego na nowo (szyk konny jedzie szybko)
        internal const float RearFoot = 5f;          // za szykiem: Depth/2 + 5 m (jak 143)
        internal const float RearMounted = 10f;      // konno: Depth/2 + 10 m
        internal const float ArriveFoot = 5f;        // dotarl: blizej celu niz 5 m (konno 10 m)
        internal const float ArriveMounted = 10f;
        internal const float MoveMin = 5f;           // nowy cel dopiero, gdy przesunal sie o ponad 5 m
        internal const float SafeRadius = 10f;       // cel bez wroga w 10 m
        internal const float EdgeMargin = 10f;       // cel co najmniej 10 m od granicy pola
        internal const float BannerRange = 80f;      // chorazy strony najwyzej tak daleko
        internal const float BannerBack = 3f;        // 3 m za chorazym
        internal const int MainMin = 8;              // glowna sila: szyk pieszy od 8 ludzi
        internal const float AloneHold = 6f;         // samotny: postoj za szykiem, potem wraca do walki
        internal const float AloneCool = 10f;        // nastepne samotne wycofanie najwczesniej po tylu sekundach
        internal const float AloneGiveUp = 4f;       // samotny bez bezpiecznego miejsca tyle sekund - porzucone (walczy dalej)
        internal const float TravelMax = 25f;        // w drodze dluzej: ranny staje tam, gdzie jest; samotny wraca do walki
        internal const int PathPerStep = 6;          // zapytan o droge na krok 0.25 s (bohaterow jest malo)
        internal const int SwapCap = 20;             // najwyzej tyle zamian miejsc na bohatera na bitwe (bez szarpania szykiem)
        private const int FailsOut = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const int ModeNone = 0, ModeWounded = 1, ModeAlone = 2;
        private const int StIdle = 0, StTravel = 1, StHold = 2;
        private const int KRear = 0, KMain = 1, KBanner = 2, KInPlace = 3, KKinds = 4, KindLimit = -2;
        private const int CheckOk = 0, CheckBad = 1, CheckLimit = 2;
        // koniec trybu (przejeci); recenzja 149: EOrder - odwrot na rozkaz szyku (ROZSADEK przelacznika od recenzji 139), EKey - klucz NIE w trakcie bitwy
        private const int EPain = 0, EGame = 1, EPanicOther = 2, E143 = 3, EPlayer = 4, EOrder = 5, EKey = 6, EKinds = 7;

        private sealed class Man
        {
            public Agent A;
            public int Side = -1;
            public bool Mine, Mounted, General, Captain, Excl;
            public int Mode, St, Kind = -1, Hits, Fails, Swaps;
            public bool EverWounded, EverAlone, EverWithdrawn, EpArrived, FateDone, Out, DryW;
            public float Since, NextEval, HoldUntil, CoolUntil = -1000f, TravelStart = -100f;
            public Vec2 Target = Vec2.Invalid;
            public WorldPosition Wp = WorldPosition.Invalid;
        }

        // ------------------------------------------------------------ stan misji (glowny watek)
        private static WeakReference _m;
        private static bool _live, _key, _field, _naval, _written, _ended, _duelists;
        private static string _kind = "?";
        private static float _scanAcc, _passAcc, _stepAcc, _rankAcc;
        private static int _pathLeft;
        private static BannerBearerLogic _bbl;
        private static bool _bblInit;
        private static readonly Dictionary<Agent, Man> Men = new Dictionary<Agent, Man>();
        private static readonly List<Man> List = new List<Man>();
        private static readonly MBList<Agent> _near = new MBList<Agent>();
        private static readonly HashSet<Formation> RankForms = new HashSet<Formation>();
        private static readonly List<Agent> _u = new List<Agent>();
        private static readonly List<int> _fi = new List<int>(), _ri = new List<int>();

        // ------------------------------------------------------------ liczniki misji
        private static int _heroes, _heroesMine, _heroesFoe, _heroesMounted, _generals, _captains, _exDragon, _exDead;
        // na tyl szyku
        private static int _swaps, _reswaps, _swapRefused, _swapCapHit, _noPartner;
        private static readonly HashSet<int> SwappedHeroes = new HashSet<int>();
        private static readonly HashSet<Formation> OneRank = new HashSet<Formation>(), PlayerForms = new HashSet<Formation>(), OtherForms = new HashSet<Formation>();
        private static readonly HashSet<int> DrySwapHeroes = new HashSet<int>();
        private static int _drySwaps;
        // wycofania
        private static int _wStarts, _aStarts, _aRepeat, _conv, _mineStarts, _foeStarts, _sends, _moves, _restored, _arrived, _inPlace;
        private static readonly HashSet<int> WoundedPeople = new HashSet<int>(), AlonePeople = new HashSet<int>();
        private static readonly int[] KindSend = new int[KKinds];
        private static int _noSpot, _foeAtSpot, _noPath, _pathLimit, _pathQueries;
        private static float _travelSum, _travelMax;
        private static int _aloneBack, _aloneGaveUp, _aloneTimeout;
        private static float _aloneFoesSum, _aloneHpSum, _woundedHpSum;
        private static readonly int[] EndWhy = new int[EKinds];
        private static int _atEndTravel, _atEndHold;
        // na sucho (klucz NIE)
        private static int _dryWounded, _dryAlone;
        // los
        private static int _killed, _ko, _routed, _otherFate;
        private static int _wdKilledTravel, _wdKilledHold, _wdKoTravel, _wdKoHold;
        private static int _everWoundedKilled, _everWoundedKo, _everWounded;
        private static int _dropped;

        private static void Stumble(string where, Exception e)
        {
            RoutHold.Stumble(RoutHold.SiteHeroes, where, e);
        }

        // ------------------------------------------------------------ wejscia z BattleSense (glowny watek)

        /// <summary>BattleSense.AfterStart w bitwie: stan od zera, bitwa polowa.</summary>
        internal static void Begin(Mission m)
        {
            Clear();
            _live = false;
            if (m == null) return;
            _m = new WeakReference(m);
            _written = false;
            _ended = false;
            _kind = MissionTeams.Kind(m);
            _naval = false;
            try { _naval = m.IsNavalBattle || m.IsNavalRaidBattle; } catch { }
            _field = false;
            try { _field = m.IsFieldBattle && _kind == "bitwa" && !_naval; } catch { }
            var c = Settings.Current;
            _key = c != null && c.HeroesKeepBehindTheirMen;
            _duelists = c != null && c.DuelistsInBattles;
            _live = _field;
        }

        /// <summary>BattleSense.OnMissionTick (blok Night, po HorsePursuit): spis co 2 s, ocena co 0.5 s, dotarcie / postoj co 0.25 s, szeregi co 3 s.</summary>
        internal static void Tick(Mission m, float dt)
        {
            if (!_live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            bool deployed = false;
            try { deployed = m.IsDeploymentFinished; } catch { }
            if (!deployed) return;
            if (m.MissionEnded)
            {
                if (!_ended) { _ended = true; EndOfBattle(); }
                return;
            }
            var c = Settings.Current;
            _key = c != null && c.HeroesKeepBehindTheirMen;
            float now = m.CurrentTime;
            _scanAcc += dt;
            _passAcc += dt;
            _stepAcc += dt;
            _rankAcc += dt;
            if (_scanAcc >= ScanEvery || (List.Count == 0 && _scanAcc >= PassEvery))
            {
                _scanAcc = 0f;
                try { Scan(m); } catch (Exception e) { Stumble("HeroCaution.Scan", e); }
            }
            if (_passAcc >= PassEvery)
            {
                _passAcc = 0f;
                _stepAcc = 0f;
                Pass(m, now, true);
            }
            else if (_stepAcc >= StepEvery)
            {
                _stepAcc = 0f;
                Pass(m, now, false);
            }
            if (_rankAcc >= RankEvery)
            {
                _rankAcc = 0f;
                Ranks(m);
            }
        }

        /// <summary>BattleSense.OnAgentRemoved (blok Night): los bohatera (zabity / nieprzytomny / uszedl), w wycofaniu czy nie.</summary>
        internal static void OnRemoved(Mission m, Agent a, AgentState state)
        {
            if (!_live || a == null || !a.IsHuman || !a.IsHero || a.IsMainAgent) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            try
            {
                Man man;
                if (!Men.TryGetValue(a, out man)) man = Register(a);
                if (man == null || man.FateDone) return;
                man.FateDone = true;
                man.Out = true;
                if (man.Excl) return;
                bool killed = state == AgentState.Killed, ko = state == AgentState.Unconscious;
                if (killed) _killed++;
                else if (ko) _ko++;
                else if (state == AgentState.Routed) _routed++;
                else _otherFate++;
                if (man.Mode != ModeNone && !_ended)
                {
                    bool hold = man.St == StHold;
                    if (killed) { if (hold) _wdKilledHold++; else _wdKilledTravel++; }
                    else if (ko) { if (hold) _wdKoHold++; else _wdKoTravel++; }
                }
                if (man.EverWounded)
                {
                    if (killed) _everWoundedKilled++;
                    else if (ko) _everWoundedKo++;
                }
            }
            catch (Exception e) { Stumble("HeroCaution.OnRemoved", e); }
        }

        /// <summary>RoutKeeper.Break (glowny watek), PRZED Panic: bohater w wycofaniu zostaje zlamany (bol ponizej AiFleeBelowPercent - FieldCraft, panika gry 142,
        /// inne) - nasza pozycja skryptowa zdjeta, zanim handler paniki da odwrot; powod do licznika.</summary>
        internal static void BeforeBreak(Agent a, int site)
        {
            if (!_live || a == null) return;
            Man man;
            if (!Men.TryGetValue(a, out man) || man.Mode == ModeNone) return;
            Release(man);
            int why = site == MissionTeams.GamePanic ? EGame : (site == MissionTeams.Flee ? EPain : EPanicOther);
            EndMode(man, why);
        }

        /// <summary>RoutKeeper.Withdraw (143, glowny watek), przy WoundedFleeHeroes TAK: ranny bohater ponizej WoundedFleePercent przechodzi do odwrotu rannego -
        /// nasza pozycja skryptowa zdjeta PRZED pierwszym ruchem 143 (natywny Retreat albo jego postoj), dalej tylko droga 143.</summary>
        internal static void BeforeWithdraw(Agent a)
        {
            if (!_live || a == null) return;
            Man man;
            if (!Men.TryGetValue(a, out man) || man.Mode == ModeNone) return;
            bool had = man.Wp.IsValid;
            EndMode(man, E143);
            if (!had || !a.IsActive()) return;
            try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
        }

        /// <summary>BattleSense.Clean: pozycje zdjete, linia raz na bitwe, stan od zera.</summary>
        internal static void End(Mission m)
        {
            var w = _m;
            if (w == null) return;
            if (m != null && !ReferenceEquals(w.Target, m)) return;
            if (!_ended)
            {
                _ended = true;
                try { EndOfBattle(); } catch (Exception e) { Stumble("HeroCaution.End", e); }
            }
            if (!_written)
            {
                _written = true;
                try { Write(m); } catch (Exception e) { Stumble("HeroCaution.Write", e); }
            }
            _live = false;
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            Men.Clear(); List.Clear(); _near.Clear(); RankForms.Clear(); _u.Clear(); _fi.Clear(); _ri.Clear();
            _bbl = null; _bblInit = false;
            _scanAcc = _passAcc = _stepAcc = _rankAcc = 0f;
            _pathLeft = 0;
            _heroes = _heroesMine = _heroesFoe = _heroesMounted = _generals = _captains = _exDragon = _exDead = 0;
            _swaps = _reswaps = _swapRefused = _swapCapHit = _noPartner = 0;
            SwappedHeroes.Clear(); OneRank.Clear(); PlayerForms.Clear(); OtherForms.Clear(); DrySwapHeroes.Clear();
            _drySwaps = 0;
            _wStarts = _aStarts = _aRepeat = _conv = _mineStarts = _foeStarts = _sends = _moves = _restored = _arrived = _inPlace = 0;
            WoundedPeople.Clear(); AlonePeople.Clear();
            Array.Clear(KindSend, 0, KindSend.Length);
            _noSpot = _foeAtSpot = _noPath = _pathLimit = _pathQueries = 0;
            _travelSum = _travelMax = 0f;
            _aloneBack = _aloneGaveUp = _aloneTimeout = 0;
            _aloneFoesSum = _aloneHpSum = _woundedHpSum = 0f;
            Array.Clear(EndWhy, 0, EndWhy.Length);
            _atEndTravel = _atEndHold = 0;
            _dryWounded = _dryAlone = 0;
            _killed = _ko = _routed = _otherFate = 0;
            _wdKilledTravel = _wdKilledHold = _wdKoTravel = _wdKoHold = 0;
            _everWoundedKilled = _everWoundedKo = _everWounded = 0;
            _dropped = 0;
        }

        // ------------------------------------------------------------ pomocnicze

        private static int SideOf(Team t)
        {
            if (!MissionTeams.Valid(t)) return -1;
            if (t.Side == BattleSideEnum.Defender) return 0;
            if (t.Side == BattleSideEnum.Attacker) return 1;
            return -1;
        }

        private static bool Withdrawing(Agent a)
        {
            var k = RoutHold.Live;
            return k != null && k.Withdrawing(a);
        }

        private static string F0(float v) { return v.ToString("0", Inv); }
        private static string F1(float v) { return v.ToString("0.0", Inv); }

        /// <summary>Nowy bohater w spisie (albo przy losie) - liczniki stron; smok i umarly wykluczeni na stale.</summary>
        private static Man Register(Agent a)
        {
            int s = SideOf(a.Team);
            if (s < 0) return null;                                       // bez waznej druzyny (widz pojedynku) - sprobujemy przy nastepnym spisie
            var man = new Man { A = a, Side = s };
            Men[a] = man;
            List.Add(man);
            try { var t = a.Team; man.Mine = t != null && (t.IsPlayerTeam || t.IsPlayerAlly); } catch { }
            var mt = a.MountAgent;
            if (mt != null && MarchToContact.IsDragon(mt)) { man.Excl = true; _exDragon++; return man; }
            if (Undead.Character(a.Character)) { man.Excl = true; _exDead++; return man; }
            _heroes++;
            if (man.Mine) _heroesMine++; else _heroesFoe++;
            if (mt != null) { man.Mounted = true; _heroesMounted++; }
            try { man.General = ReferenceEquals(a.Team.GeneralAgent, a); } catch { }
            try { var f = a.Formation; man.Captain = f != null && ReferenceEquals(f.Captain, a); } catch { }
            if (man.General) _generals++;
            if (man.Captain) _captains++;
            return man;
        }

        /// <summary>Spis bohaterow AI na polu (co 2 s - takze posilki). Gracz nie.</summary>
        private static void Scan(Mission m)
        {
            var ags = m.Agents;
            int n = ags != null ? ags.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Agent a;
                try { a = ags[i]; } catch { continue; }
                if (a == null || !a.IsHuman || !a.IsHero || a.IsMainAgent || !a.IsActive()) continue;
                if (Men.ContainsKey(a)) continue;
                Register(a);
            }
        }

        // ------------------------------------------------------------ ocena i ruch

        private static void Pass(Mission m, float now, bool full)
        {
            _pathLeft = PathPerStep;
            for (int i = 0; i < List.Count; i++)
            {
                var man = List[i];
                if (man.Out || man.Excl) continue;
                if (!full && man.Mode == ModeNone) continue;
                try
                {
                    if (full) PassOne(man, m, now);
                    else if (man.Mode != ModeNone && _key) StepOne(man, m, now);   // recenzja 149: klucz NIE w trakcie - bez kroku (PassOne zdejmie pozycje)
                }
                catch (Exception e)
                {
                    Stumble("HeroCaution.Pass", e);
                    if (++man.Fails >= FailsOut)
                    {
                        try { Release(man); } catch { }
                        man.Mode = ModeNone;
                        man.Out = true;                                   // jeden zly bohater wypada, reszta dziala
                        _dropped++;
                    }
                }
            }
        }

        private static void PassOne(Man man, Mission m, float now)
        {
            var a = man.A;
            if (a == null || !a.IsActive()) { man.Out = true; return; }   // los przyjdzie z OnRemoved
            if (!a.IsAIControlled || a.IsMainAgent)
            {
                if (man.Mode != ModeNone) { Release(man); EndMode(man, EPlayer); }
                man.Hits = 0;
                return;
            }
            if (!MissionTeams.Valid(a.Team)) return;
            if (SurvivalMode.InPanic(a))
            {
                // PANIKA (przelacznik 139): bez rozsadku - pozycja zdjeta (zwykle juz w BeforeBreak), gra prowadzi ucieczke
                if (man.Mode != ModeNone) { Release(man); EndMode(man, EPanicOther); }
                man.Hits = 0;
                return;
            }
            if (Withdrawing(a))
            {
                // odwrot rannego 143 (WoundedFleeHeroes TAK) - jego droga; pozycji nie zdejmujemy (wzor HorsePursuit.Uncontrol)
                if (man.Mode != ModeNone) { Release(man); EndMode(man, E143); }
                man.Hits = 0;
                return;
            }
            if (a.IsRunningAway || SurvivalMode.OrderedRetreat(a, a.CommonAIComponent))
            {
                // recenzja 139/149: odwrot na rozkaz szyku jest teraz ROZSADKIEM przelacznika (dotad PANIKA - ta sama galaz wyzej); bohater idzie za rozkazem
                // wodza (natywny odwrot gry), naszej pozycji nie dokladamy - zachowanie jak przed recenzja, osobny powod w linii
                if (man.Mode != ModeNone) { Release(man); EndMode(man, EOrder); }
                man.Hits = 0;
                return;
            }
            man.Mounted = a.MountAgent != null;
            float max = a.HealthLimit;
            float hp = max > 0f ? a.Health / max : 1f;
            bool wounded = hp < WoundedBelow;
            if (wounded && !man.EverWounded) { man.EverWounded = true; _everWounded++; }

            if (!_key)
            {
                // recenzja 149: klucz przelaczony na NIE w trakcie bitwy - trwajace wycofanie konczy sie od razu (pozycja zdjeta), dalej tylko pomiar (wzor 148 EKey)
                if (man.Mode != ModeNone) { Release(man); EndMode(man, EKey); }
                // na sucho: kto bylby wycofany (ranny raz na bitwe; samotny z ta sama przerwa co przy TAK)
                if (wounded) { if (!man.DryW) { man.DryW = true; _dryWounded++; } return; }
                if (now < man.CoolUntil) return;
                int df;
                if (Alone(a, m, out df))
                {
                    if (++man.Hits >= AloneHits) { man.Hits = 0; _dryAlone++; man.CoolUntil = now + AloneHold + AloneCool; }
                }
                else man.Hits = 0;
                return;
            }

            if (wounded)
            {
                if (man.Mode != ModeWounded) Start(man, ModeWounded, now, hp, 0);
            }
            else if (man.Mode == ModeNone && now >= man.CoolUntil)
            {
                int foes;
                if (Alone(a, m, out foes))
                {
                    if (++man.Hits >= AloneHits) { man.Hits = 0; Start(man, ModeAlone, now, hp, foes); }
                }
                else man.Hits = 0;
            }
            if (man.Mode != ModeNone) StepOne(man, m, now);
        }

        /// <summary>Samotny: 0 swoich (nie w PANICE) w AllyRadius i co najmniej FoeMin wrogow (nie uciekajacych, nie w PANICE) w FoeRadius. Jedno zapytanie
        /// natywne o sasiadow w 10 m.</summary>
        private static bool Alone(Agent a, Mission m, out int foes)
        {
            foes = 0;
            int side = SideOf(a.Team);
            if (side < 0) return false;
            Vec2 c = a.Position.AsVec2;
            if (!c.IsValid) return false;
            int allies = 0;
            try
            {
                m.GetNearbyAgents(c, FoeRadius, _near);
                float ar2 = AllyRadius * AllyRadius;
                for (int i = 0; i < _near.Count; i++)
                {
                    var x = _near[i];
                    if (x == null || ReferenceEquals(x, a) || !x.IsHuman || !x.IsActive()) continue;
                    int s = SideOf(x.Team);
                    if (s < 0) continue;
                    if (s == side)
                    {
                        // recenzja 139: swoj w odwrocie na rozkaz szyku (od recenzji ROZSADEK) nie stoi przy nim - nie liczony, jak przed zmiana przelacznika
                        if (x.Position.AsVec2.DistanceSquared(c) <= ar2 && !SurvivalMode.InPanic(x) && !SurvivalMode.OrderedRetreat(x, x.CommonAIComponent)) { allies++; break; }
                    }
                    else if (!x.IsRunningAway && !SurvivalMode.InPanic(x)) foes++;
                }
            }
            finally { _near.Clear(); }                                    // bez referencji do agentow miedzy ocenami
            return allies == 0 && foes >= FoeMin;
        }

        private static void Start(Man man, int mode, float now, float hp, int foes)
        {
            bool conv = man.Mode == ModeAlone && mode == ModeWounded;
            man.Mode = mode;
            man.Since = now;
            man.NextEval = now;
            if (!conv)
            {
                man.St = StTravel;
                man.EpArrived = false;
                man.TravelStart = -100f;
                man.Kind = -1;
                man.Target = Vec2.Invalid;
                man.Wp = WorldPosition.Invalid;
            }
            // z samotnosci w rannego: ten sam cel, droga i postoj (bez zdejmowania pozycji); postoj rannego trwa do konca bitwy
            man.EverWithdrawn = true;
            if (man.Mine) _mineStarts++; else _foeStarts++;
            if (mode == ModeWounded)
            {
                _wStarts++;
                if (conv) _conv++;
                WoundedPeople.Add(man.A.Index);
                _woundedHpSum += hp;
            }
            else
            {
                _aStarts++;
                if (man.EverAlone) _aRepeat++;
                man.EverAlone = true;
                AlonePeople.Add(man.A.Index);
                _aloneFoesSum += foes;
                _aloneHpSum += hp;
            }
        }

        /// <summary>Krok wycofania (co 0.25 s i w passie): dotarcie, limit drogi, koniec postoju samotnego, ocena celu co 1.5 s / 4 s.</summary>
        private static void StepOne(Man man, Mission m, float now)
        {
            var a = man.A;
            if (a == null || !a.IsActive()) return;
            if (man.St == StTravel)
            {
                if (man.Wp.IsValid && man.Target.IsValid)
                {
                    float r = man.Mounted ? ArriveMounted : ArriveFoot;
                    Vec2 pos = a.Position.AsVec2;
                    if (pos.IsValid && pos.DistanceSquared(man.Target) <= r * r) { Arrive(man, now); return; }
                    if (man.TravelStart > -50f && now - man.TravelStart > TravelMax)
                    {
                        if (man.Mode == ModeAlone) { _aloneTimeout++; Release(man); EndAlone(man, now); return; }
                        ArriveInPlace(man, now);
                        return;
                    }
                }
                else if (man.Mode == ModeAlone && now - man.Since > AloneGiveUp)
                {
                    _aloneGaveUp++;                                       // nigdzie bezpiecznie - walczy dalej, nastepna proba po przerwie
                    EndAlone(man, now);
                    return;
                }
            }
            else if (man.St == StHold && man.Mode == ModeAlone && now >= man.HoldUntil)
            {
                _aloneBack++;                                             // postal za szykiem - wraca do walki ze swoimi
                Release(man);
                EndAlone(man, now);
                return;
            }
            if (now < man.NextEval) return;
            Evaluate(man, m, now);
        }

        private static void Evaluate(Man man, Mission m, float now)
        {
            var a = man.A;
            man.NextEval = now + (man.Mode == ModeAlone ? RefreshAlone : RefreshWounded);
            int kind;
            WorldPosition wp;
            Vec2 t = Pick(man, m, out kind, out wp);
            if (kind == KindLimit) { _pathLimit++; man.NextEval = now + StepEvery; return; }   // budzet zapytan - proba w nastepnym kroku
            bool lost = false;
            if (man.Wp.IsValid)
            {
                try { lost = (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { lost = false; }
            }
            if (!t.IsValid)
            {
                _noSpot++;
                // stary cel zostaje (przywrocony, gdy ktos zdjal pozycje - RBM StanceLogic przy zmianie szyku); jeszcze nie wyslany - walczy dalej
                if (lost && man.Wp.IsValid) { var o = man.Wp; a.SetScriptedPosition(ref o, false, Agent.AIScriptedFrameFlags.None); _restored++; }
                return;
            }
            bool first = !man.Wp.IsValid;
            bool moved = !man.Target.IsValid || man.Target.DistanceSquared(t) > MoveMin * MoveMin;
            if (!first && !moved && !lost) return;
            man.Target = t;
            man.Wp = wp;
            man.Kind = kind;
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);   // bez NoAttack (broni sie), bez DoNotRun
            if (first)
            {
                _sends++;
                if (kind >= 0 && kind < KKinds) KindSend[kind]++;
                man.TravelStart = now;
            }
            else if (lost) _restored++;
            else _moves++;
        }

        private static void Arrive(Man man, float now)
        {
            man.St = StHold;
            if (!man.EpArrived)
            {
                man.EpArrived = true;
                _arrived++;
                float d = man.TravelStart > -50f ? now - man.TravelStart : 0f;
                _travelSum += d;
                if (d > _travelMax) _travelMax = d;
            }
            if (man.Mode == ModeAlone) man.HoldUntil = now + AloneHold;
        }

        /// <summary>Ranny po TravelMax w drodze: postoj tam, gdzie stoi (pozycja skryptowa na miejscu).</summary>
        private static void ArriveInPlace(Man man, float now)
        {
            var a = man.A;
            var wp = a.GetWorldPosition();
            if (!wp.IsValid) return;
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            man.Wp = wp;
            man.Target = a.Position.AsVec2;
            man.Kind = KInPlace;
            _inPlace++;
            Arrive(man, now);
        }

        private static void EndAlone(Man man, float now)
        {
            man.Mode = ModeNone;
            man.St = StIdle;
            man.CoolUntil = now + AloneCool;
            man.Wp = WorldPosition.Invalid;
            man.Target = Vec2.Invalid;
            man.Hits = 0;
        }

        private static void EndMode(Man man, int why)
        {
            if (why >= 0 && why < EKinds) EndWhy[why]++;
            man.Mode = ModeNone;
            man.St = StIdle;
            man.Wp = WorldPosition.Invalid;
            man.Target = Vec2.Invalid;
            man.Hits = 0;
        }

        /// <summary>Nasza pozycja skryptowa zdjeta (DisableScriptedMovement, gdy GoToPosition). Czlowieka w odwrocie rannego 143 nie dotykamy, chyba ze juz ucieka
        /// (ten sam warunek co HorsePursuit.Uncontrol - jedna zasada zwalniania).</summary>
        private static void Release(Man man)
        {
            bool had = man.Wp.IsValid;
            man.Wp = WorldPosition.Invalid;
            man.Target = Vec2.Invalid;
            man.St = StIdle;
            if (!had) return;
            var a = man.A;
            if (a == null || !a.IsActive()) return;
            bool touch = a.IsRunningAway || !Withdrawing(a);
            if (!touch) return;
            try { if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement(); } catch { }
        }

        /// <summary>Koniec bitwy (pierwsza klatka z MissionEnded albo koniec misji): stan "na koniec" do linii, pozycje zdjete (zwyciezcy wiwatuja, przegrani
        /// odchodza droga gry).</summary>
        private static void EndOfBattle()
        {
            for (int i = 0; i < List.Count; i++)
            {
                var man = List[i];
                if (man.Mode == ModeNone) continue;
                try
                {
                    if (man.A != null && man.A.IsActive()) { if (man.St == StHold) _atEndHold++; else _atEndTravel++; }
                    Release(man);
                }
                catch (Exception e) { Stumble("HeroCaution.EndOfBattle", e); }
                man.Mode = ModeNone;
                man.St = StIdle;
            }
        }

        // ------------------------------------------------------------ cel wycofania

        /// <summary>Cel: tyl wlasnego szyku, potem tyl glownej sily strony, potem 3 m za najblizszym chorazym strony (do 80 m). Vec2.Invalid = brak bezpiecznego
        /// miejsca; kind = KindLimit - budzet zapytan o droge wyczerpany w tym kroku.</summary>
        private static Vec2 Pick(Man man, Mission m, out int kind, out WorldPosition wp)
        {
            kind = -1;
            wp = WorldPosition.Invalid;
            var a = man.A;
            float back = man.Mounted ? RearMounted : RearFoot;
            var f = a.Formation;
            Vec2 p;
            if (f != null && f.CountOfUnits >= 3 && Rear(f, back, out p))
            {
                int r = Check(a, m, p, out wp);
                if (r == CheckLimit) { kind = KindLimit; return Vec2.Invalid; }
                if (r == CheckOk) { kind = KRear; return p; }
            }
            var mf = MainOf(a.Team, f);
            if (mf != null && Rear(mf, back, out p))
            {
                int r = Check(a, m, p, out wp);
                if (r == CheckLimit) { kind = KindLimit; return Vec2.Invalid; }
                if (r == CheckOk) { kind = KMain; return p; }
            }
            Agent b = NearestBanner(a, m);
            if (b != null)
            {
                Vec2 bp = b.Position.AsVec2;
                var bf = b.Formation;
                Vec2 bd = bf != null ? bf.Direction : Vec2.Invalid;
                p = bd.IsValid && bd.LengthSquared > 0.25f ? bp - bd.Normalized() * BannerBack : bp;
                int r = Check(a, m, p, out wp);
                if (r == CheckLimit) { kind = KindLimit; return Vec2.Invalid; }
                if (r == CheckOk) { kind = KBanner; return p; }
            }
            wp = WorldPosition.Invalid;
            return Vec2.Invalid;
        }

        /// <summary>Tyl szyku: srodek - kierunek x (glebokosc/2 + back) - ten sam wzor co odwrot rannego 143.</summary>
        private static bool Rear(Formation f, float back, out Vec2 p)
        {
            p = Vec2.Invalid;
            Vec2 cen = f.CurrentPosition;
            Vec2 dir = f.Direction;
            if (!cen.IsValid || !dir.IsValid || dir.LengthSquared <= 0.25f) return false;
            p = cen - dir.Normalized() * (Math.Max(0f, f.Depth) * 0.5f + back);
            return true;
        }

        /// <summary>Glowna sila strony: najwiekszy szyk pieszy (piechota albo strzelcy) od MainMin ludzi, inny niz wlasny szyk bohatera.</summary>
        private static Formation MainOf(Team t, Formation own)
        {
            if (!MissionTeams.Valid(t)) return null;
            Formation best = null;
            int bn = MainMin - 1;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || ReferenceEquals(f, own) || f.CountOfUnits <= bn) continue;
                var q = f.QuerySystem;
                if (q == null || q.IsCavalryFormation || q.IsRangedCavalryFormation || !(q.IsInfantryFormation || q.IsRangedFormation)) continue;
                best = f;
                bn = f.CountOfUnits;
            }
            return best;
        }

        /// <summary>Cel w granicach pola i EdgeMargin od granicy, bez wroga (nie uciekajacego) w SafeRadius, z droga od bohatera (zapytanie natywne z budzetu
        /// kroku) - wzor RoutKeeper.Check (143), wlasne liczniki.</summary>
        private static int Check(Agent a, Mission m, Vec2 p, out WorldPosition wp)
        {
            wp = WorldPosition.Invalid;
            if (!p.IsValid || m.Scene == null) return CheckBad;
            if (!m.IsPositionInsideBoundaries(p)) return CheckBad;
            Vec2 edge = m.GetClosestBoundaryPosition(p);
            if (edge.IsValid && edge.DistanceSquared(p) < EdgeMargin * EdgeMargin) return CheckBad;
            bool foe = false;
            try
            {
                m.GetNearbyEnemyAgents(p, SafeRadius, a.Team, _near);
                for (int i = 0; i < _near.Count; i++)
                {
                    var x = _near[i];
                    if (x != null && x.IsActive() && x.IsHuman && !x.IsRunningAway) { foe = true; break; }
                }
            }
            finally { _near.Clear(); }
            if (foe) { _foeAtSpot++; return CheckBad; }
            if (_pathLeft <= 0) return CheckLimit;
            _pathLeft--;
            _pathQueries++;
            WorldPosition from = a.GetWorldPosition();
            WorldPosition dest = new WorldPosition(m.Scene, UIntPtr.Zero, new Vec3(p.x, p.y, a.Position.z), false);
            float dist;
            if (!m.Scene.GetPathDistanceBetweenPositions(ref from, ref dest, 0f, out dist)) { _noPath++; return CheckBad; }
            wp = dest;
            return CheckOk;
        }

        /// <summary>Najblizszy chorazy strony (BannerBearerLogic.GetFormationBannerBearers) w BannerRange; nie on sam, nie uciekajacy.</summary>
        private static Agent NearestBanner(Agent a, Mission m)
        {
            if (!_bblInit)
            {
                _bblInit = true;
                try { _bbl = m.GetMissionBehavior<BannerBearerLogic>(); } catch { _bbl = null; }
            }
            if (_bbl == null) return null;
            var t = a.Team;
            if (!MissionTeams.Valid(t)) return null;
            Vec2 my = a.Position.AsVec2;
            Agent best = null;
            float bd = BannerRange * BannerRange;
            foreach (var f in t.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                List<Agent> bs = null;
                try { bs = _bbl.GetFormationBannerBearers(f); } catch { bs = null; }
                if (bs == null) continue;
                for (int i = 0; i < bs.Count; i++)
                {
                    var b = bs[i];
                    if (b == null || ReferenceEquals(b, a) || !b.IsActive() || b.Banner == null || b.IsRunningAway) continue;
                    float d = b.Position.AsVec2.DistanceSquared(my);
                    if (d < bd) { bd = d; best = b; }
                }
            }
            return best;
        }

        // ------------------------------------------------------------ na tyl szyku (co 3 s)

        /// <summary>Bohater moze byc przestawiony: AI, czynny, w ROZSADKU, bez naszego wycofania i odwrotu 143, nie chorazy (RBM sam steruje chorazym).</summary>
        private static bool Cautious(Agent h, Formation f)
        {
            if (h == null || !h.IsHero || !h.IsAIControlled || h.IsMainAgent || !h.IsActive() || !ReferenceEquals(h.Formation, f)) return false;
            Man man;
            if (!Men.TryGetValue(h, out man) || man.Excl || man.Out || man.Mode != ModeNone) return false;
            if (SurvivalMode.InPanic(h) || Withdrawing(h) || h.IsRunningAway) return false;   // recenzja 139: takze odwrot na rozkaz szyku (ROZSADEK)
            return !HorsePursuit.Banner(h);
        }

        /// <summary>Partner zamiany: szeregowy AI tego szyku, czynny, nie gracz, nie chorazy.</summary>
        private static bool Partner(Agent p, Formation f)
        {
            return p != null && p.IsActive() && p.IsHuman && !p.IsHero && !p.IsMainAgent && p.IsAIControlled && ReferenceEquals(p.Formation, f)
                   && !HorsePursuit.Banner(p);
        }

        private static void Ranks(Mission m)
        {
            RankForms.Clear();
            for (int i = 0; i < List.Count; i++)
            {
                var man = List[i];
                if (man.Out || man.Excl || man.Mode != ModeNone) continue;
                var a = man.A;
                try
                {
                    if (a == null || !a.IsActive() || !a.IsAIControlled || a.IsMainAgent) continue;
                    var f = a.Formation;
                    if (f != null) RankForms.Add(f);
                }
                catch { }
            }
            foreach (var f in RankForms)
            {
                try { RankForm(f); }
                catch (Exception e) { Stumble("HeroCaution.Szereg", e); }
            }
            RankForms.Clear();
            _u.Clear(); _fi.Clear(); _ri.Clear();
        }

        /// <summary>Bohaterowie szyku w ostatnich dwoch szeregach (w dwuszeregowym - w drugim): zamiana z najglebiej stojacym szeregowym, najblizszy rzad.
        /// Migawka indeksow z GetAllUnits PRZED zamianami (SwitchUnitLocations odbudowuje liste gry), po zamianie poprawiana dla obu ludzi.</summary>
        private static void RankForm(Formation f)
        {
            if (f.CountOfUnits < 2) return;
            // recenzja 149: takze szyk pod rozkazami gracza (!IsAIControlled, gracz wodzem / sierzantem) - jedna definicja formacji gracza z 148 (HorsePursuit.PlayerForm);
            // D4 dotyczy lordow AI, Z9 syntezy: "jego formacji nie przejmujemy". Wycofanie rannego / samotnego towarzysza zostaje (osobista zasada jak 143).
            if (f.HasPlayerControlledTroop || HorsePursuit.PlayerForm(f)) { PlayerForms.Add(f); return; }
            var arr = f.Arrangement;
            if (!(arr is LineFormation) || arr is TransposedLineFormation) { OtherForms.Add(f); return; }
            var all = arr.GetAllUnits();
            int n = all != null ? all.Count : 0;
            _u.Clear(); _fi.Clear(); _ri.Clear();
            int maxRank = -1;
            for (int i = 0; i < n; i++)
            {
                var u = all[i];
                var ag = u as Agent;
                if (ag == null) continue;
                int fi = u.FormationFileIndex, ri = u.FormationRankIndex;
                if (fi < 0 || ri < 0) continue;                           // nieustawiony (gra trzyma go poza siatka)
                _u.Add(ag); _fi.Add(fi); _ri.Add(ri);
                if (ri > maxRank) maxRank = ri;
            }
            if (maxRank <= 0) { OneRank.Add(f); return; }
            int keep = Math.Max(1, maxRank - 1);                          // od tego szeregu bohater stoi juz z tylu
            for (int i = 0; i < _u.Count; i++)
            {
                var h = _u[i];
                if (_ri[i] >= keep || !Cautious(h, f)) continue;
                Man man;
                Men.TryGetValue(h, out man);
                if (man != null && man.Swaps >= SwapCap) { _swapCapHit++; continue; }
                int best = -1, bestRank = _ri[i], bestFile = int.MaxValue;
                for (int j = 0; j < _u.Count; j++)
                {
                    if (j == i || _ri[j] <= _ri[i] || !Partner(_u[j], f)) continue;
                    int df = Math.Abs(_fi[j] - _fi[i]);
                    if (_ri[j] > bestRank || (_ri[j] == bestRank && df < bestFile)) { best = j; bestRank = _ri[j]; bestFile = df; }
                }
                if (best < 0) { _noPartner++; continue; }
                if (!_key)
                {
                    _drySwaps++;
                    DrySwapHeroes.Add(h.Index);
                    continue;
                }
                var p = _u[best];
                int hr = _ri[i];
                arr.SwitchUnitLocations(h, p);
                int nr = ((IFormationUnit)h).FormationRankIndex;
                if (nr == hr) { _swapRefused++; continue; }               // RBM (OverrideLineFormation) albo gra nie zamienila
                int tf = _fi[i], tr = _ri[i];
                _fi[i] = _fi[best]; _ri[i] = _ri[best];
                _fi[best] = tf; _ri[best] = tr;
                _swaps++;
                if (!SwappedHeroes.Add(h.Index)) _reswaps++;
                if (man != null) man.Swaps++;
            }
        }

        // ------------------------------------------------------------ linia

        private static void Write(Mission m)
        {
            var c = Settings.Current;
            if (!_field)
            {
                if (!_key || _naval) return;
                Log.Info("bohaterowie (149): misja " + _kind + ", klucz TAK - nieczynne: tylko bitwa polowa.");
                return;
            }
            int onField = 0, onFieldWd = 0;
            for (int i = 0; i < List.Count; i++)
            {
                var man = List[i];
                if (man.Excl || man.FateDone) continue;
                try { if (man.A != null && man.A.IsActive()) { onField++; if (man.EverWithdrawn) onFieldWd++; } } catch { }
            }
            int stum;
            lock (RoutHold.Gate) { stum = RoutHold.StumblesLocked(RoutHold.SiteHeroes); }
            int fleeBelow = c != null ? c.AiFleeBelowPercent : 10;
            var sb = new StringBuilder(2600);
            sb.Append("bohaterowie (149): misja ").Append(_kind).Append(", klucz ").Append(_key ? "TAK" : "NIE (tylko pomiar: bez przestawien i wycofan)")
              .Append(" (lord / bohater AI obu stron, nie gracz, bitwa polowa: ranny ponizej ").Append(F0(WoundedBelow * 100f))
              .Append("% HP albo samotny - 0 swoich w ").Append(F0(AllyRadius)).Append(" m i co najmniej ").Append(FoeMin).Append(" wrogow w ").Append(F0(FoeRadius))
              .Append(" m przez ").Append(AloneHits).Append(" passy po ").Append(PassEvery.ToString("0.0", Inv))
              .Append(" s - wycofuje sie za swoj szyk pozycja skryptowa, bez paniki, okrzyku i natywnego odwrotu: tyl wlasnego szyku (glebokosc/2 + ")
              .Append(F0(RearFoot)).Append(" m, konno ").Append(F0(RearMounted)).Append(" m), potem glowna sila, potem sztandar do ").Append(F0(BannerRange))
              .Append(" m; samotny stoi ").Append(F0(AloneHold)).Append(" s i wraca do walki (nastepny raz po ").Append(F0(AloneCool))
              .Append(" s), ranny stoi za szykiem do konca bitwy i broni sie; w szyku na tyl - ostatnie 2 szeregi, zamiana miejsc co ").Append(F0(RankEvery))
              .Append(" s, szyki gracza (gracz w szyku / jego rozkazy) pominiete; Duelists In Battles ").Append(_duelists ? "TAK" : "NIE")
              .Append(" - zrecznosc pojedynkowa bez zmian; formacja wodza RBM nieprzywrocona; panika z bolu ponizej ").Append(fleeBelow).Append("% HP jak dotad)");
            sb.Append(" | bohaterow AI ").Append(_heroes).Append(" (strona gracza ").Append(_heroesMine).Append(" / wroga ").Append(_heroesFoe)
              .Append("; konni ").Append(_heroesMounted).Append("; wodzowie ").Append(_generals).Append(", kapitanowie ").Append(_captains)
              .Append("; wykluczeni: smoki ").Append(_exDragon).Append(", umarli ").Append(_exDead).Append(')');
            if (_key)
            {
                sb.Append(" | na tyl szyku: bohaterow ").Append(SwappedHeroes.Count).Append(" (zamian ").Append(_swaps).Append(", ponownych ").Append(_reswaps)
                  .Append(" - gra przesunela go do przodu; odmowy RBM / gry ").Append(_swapRefused).Append(", limit ").Append(SwapCap).Append(" na bohatera ").Append(_swapCapHit)
                  .Append("; szyki jednoszeregowe ").Append(OneRank.Count).Append(", bez partnera ").Append(_noPartner).Append("; szyki gracza (gracz w szyku / jego rozkazy) pominiete ").Append(PlayerForms.Count)
                  .Append(", kolumna i inne ").Append(OtherForms.Count).Append(')');
                float aloneN = Math.Max(1, _aStarts);
                sb.Append(" | wycofania ").Append(_wStarts + _aStarts).Append(": ranni ").Append(_wStarts).Append(" (ludzi ").Append(WoundedPeople.Count)
                  .Append(", z samotnosci w rannego ").Append(_conv).Append(", sr. HP przy wejsciu ").Append(_wStarts > 0 ? F0(_woundedHpSum / _wStarts * 100f) : "-")
                  .Append("%) / samotni ").Append(_aStarts).Append(" (ludzi ").Append(AlonePeople.Count).Append(", ponowne ").Append(_aRepeat)
                  .Append(", sr. wrogow w ").Append(F0(FoeRadius)).Append(" m ").Append(_aStarts > 0 ? F1(_aloneFoesSum / aloneN) : "-")
                  .Append(", sr. HP ").Append(_aStarts > 0 ? F0(_aloneHpSum / aloneN * 100f) : "-").Append("%); strona gracza ").Append(_mineStarts)
                  .Append(" / wroga ").Append(_foeStarts);
                sb.Append(" | cel: tyl wlasnego szyku ").Append(KindSend[KRear]).Append(" / glowna sila ").Append(KindSend[KMain]).Append(" / sztandar ").Append(KindSend[KBanner])
                  .Append(", w miejscu po ").Append(F0(TravelMax)).Append(" s ").Append(_inPlace).Append("; ocen bez bezpiecznego miejsca ").Append(_noSpot)
                  .Append(" (wrog przy celu ").Append(_foeAtSpot).Append(", bez drogi ").Append(_noPath).Append(", limit zapytan ").Append(_pathLimit)
                  .Append("; zapytan o droge ").Append(_pathQueries).Append(')');
                sb.Append(" | dotarli za szyk ").Append(_arrived).Append(" (sr. droga ").Append(_arrived > 0 ? F1(_travelSum / _arrived) : "-").Append(" s, najdluzej ")
                  .Append(F1(_travelMax)).Append(" s); cel przesuniety ").Append(_moves).Append(", pozycja przywrocona ").Append(_restored)
                  .Append(" | samotni: wrocili do walki po postoju ").Append(_aloneBack).Append(", bez miejsca porzucone ").Append(_aloneGaveUp)
                  .Append(", w drodze ponad ").Append(F0(TravelMax)).Append(" s ").Append(_aloneTimeout);
                sb.Append(" | przejeci: odwrot rannego 143 (Wounded Flee Heroes ").Append(c != null && c.WoundedFleeHeroes ? "TAK" : "NIE").Append(") ").Append(EndWhy[E143])
                  .Append(", PANIKA: z bolu ").Append(EndWhy[EPain]).Append(" / gry ").Append(EndWhy[EGame]).Append(" / inna ").Append(EndWhy[EPanicOther])
                  .Append(", odwrot na rozkaz szyku ").Append(EndWhy[EOrder]).Append(", gracz ").Append(EndWhy[EPlayer]).Append(", klucz NIE w trakcie ").Append(EndWhy[EKey]).Append(" | na koniec bitwy: w drodze ").Append(_atEndTravel).Append(", na postoju ").Append(_atEndHold);
            }
            else
            {
                sb.Append(" | na sucho: na tyl szyku bohaterow ").Append(DrySwapHeroes.Count).Append(" (zamian ").Append(_drySwaps).Append("); wycofania: ranni ")
                  .Append(_dryWounded).Append(", samotni ").Append(_dryAlone).Append(" (z przerwa ").Append(F0(AloneHold + AloneCool)).Append(" s)");
            }
            sb.Append(" | los bohaterow AI: zabici ").Append(_killed).Append(" / nieprzytomni ").Append(_ko).Append(" / uszli ").Append(_routed)
              .Append(" / inne ").Append(_otherFate).Append(" / na polu ").Append(onField);
            if (_key)
                sb.Append("; w wycofaniu (w drodze / na postoju): zabici ").Append(_wdKilledTravel).Append('/').Append(_wdKilledHold)
                  .Append(", nieprzytomni ").Append(_wdKoTravel).Append('/').Append(_wdKoHold).Append("; na polu z wycofanych ").Append(onFieldWd);
            sb.Append("; kiedykolwiek ranni ponizej ").Append(F0(WoundedBelow * 100f)).Append("%: ").Append(_everWounded).Append(" (zabici ").Append(_everWoundedKilled)
              .Append(", nieprzytomni ").Append(_everWoundedKo).Append(')');
            sb.Append(" | potkniecia bohaterowie ").Append(stum).Append(" (wypadli ").Append(_dropped).Append(").");
            Log.Info(sb.ToString());
        }
    }
}
