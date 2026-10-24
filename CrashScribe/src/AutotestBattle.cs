using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using MapState = TaleWorlds.CampaignSystem.GameState.MapState;
using PartyState = TaleWorlds.CampaignSystem.GameState.PartyState;
using InventoryState = TaleWorlds.CampaignSystem.GameState.InventoryState;
using MenuContext = TaleWorlds.CampaignSystem.GameState.MenuContext;

namespace CrashScribe
{
    /// <summary>
    /// [AT5] BITWA W AUTOTESCIE (16.10: wywrotka 0xC0000005 przy pierwszym ciosie na arenie - autotest
    /// jezdzil tylko dobami na mapie, kod misji nie byl sprawdzany). Klucze przelacznika: "battles" (ile
    /// bitew, 0 = brak - wtedy ten etap nie istnieje), "battle_s" (sekundy rzeczywiste na bitwe, 150) i "arena"
    /// (walka cwiczebna na arenie przed bitwami; domyslnie tak, gdy battles > 0).
    ///
    /// KIEDY: po wejsciu na mape i osadzeniu gracza w miescie (EnsureWaiting = mapa spokojna), PRZED
    /// pierwsza doba biegu. Potem gracz wraca do miasta i bieg dob idzie jak dotad (KONIEC, zapis, wyjscie).
    ///
    /// ARENA (recenzja 16.10: dzisiejsza wywrotka byla na arenie, inna scena niz bitwa w polu): gracz czeka w miescie,
    /// wiec najpierw "Stop waiting" -> "Go to the arena" [town_arena] -> "Practice fight" [mno_enter_practice_fight]
    /// (ArenaMasterCampaignBehavior: LocationEncounter.CreateAndOpenMissionController(arena) + StartPlayerPractice) ->
    /// walka cwiczebna do omdlenia bohatera albo do limitu -> rozmowa z mistrzem areny konczona -> wyjscie z misji jak
    /// Tab (Mission.EndMission). Linie "AUTOTEST ARENA ...".
    ///
    /// BITWY W POLU - ustalona kolejnosc zamiast losu (recenzja 16.10: dwie przegrane przez ucieczke nie sprawdzily
    /// wygranej, lupu ani odwrotu): bitwa 1 (i 4, 7...) MOCNA druzyna (jednostka kultury po awansach, 2 x wrogowie) ->
    /// wygrana, "Capture the enemy", ekrany lupu; bitwa 2 ODWROT w trakcie walki (bohater zostaje na miejscu,
    /// niesmiertelny w tej bitwie, odwrot jak Tab -> "Odwrot?" -> OK); bitwa 3 SLABA druzyna (bez dokladki) ->
    /// przegrana / niewola -> ucieczka. Dolozeni zolnierze wracaja po kazdej bitwie (sklad jak przed bitwa).
    ///
    /// JAK (to samo, co gracz): "Stop waiting" + "Leave" z miasta -> nowa druzyna bandytow obok gracza
    /// (BanditPartyComponent.CreateBanditParty, klan kryjowki najblizszej graczowi) -> spotkanie
    /// EncounterManager.StartPartyEncounter (jak dojechanie do wrogiej druzyny) -> rozmowa ze spotkania
    /// konczona (EndConversation) -> menu "encounter" -> opcja "attack" (MenuHelper.EncounterAttackConsequence
    /// -> CampaignMission.OpenBattleMission: misja z zachowaniami wszystkich modow) -> w misji:
    /// DeploymentHandler.FinishDeployment (przycisk "Rozpocznij"), rozkaz Szarza dla wszystkich szykow,
    /// bohater pod SI (Agent.Controller = AI), przewijanie (Mission.SetFastForwardingFromUI) -> koniec sam
    /// (BattleEndLogic.TryExit, jak "Gotowe" na tablicy wynikow) albo odwrot jak gracz
    /// (BattleEndLogic.TryExit + BasicMissionHandler: OnBattleEnded + Mission.RetreatMission) -> ekrany lupu "Gotowe",
    /// menu spotkania "Leave", niewola -> ucieczka -> mapa spokojna -> nastepna bitwa albo bieg dob.
    ///
    /// BOHATER: w bitwach testu nie moze zginac (CampaignEvents.CanHeroDieEvent -> false tylko w etapie bitew, tylko
    /// smierc w bitwie; SandboxAgentDecideKilledOrUnconsciousModel zamienia smierc na omdlenie) - smierc bohatera dawalaby
    /// losowy czerwony wynik albo bieg z innym bohaterem.
    ///
    /// BLEDY (recenzja 16.10: podzial musi isc po pierwszej ramce, nie po nazwie zestawu): wyjatek, ktorego pierwsza
    /// ramka spoza BCL jest w CrashScribe - "AUTOTEST BITWA BLAD: ..." (blad autotestu), bieg dalej; w sladzie mod
    /// spoza TaleWorlds (lista modulow z folderu Modules bez Native/SandBox/StoryMode/...) - "AUTOTEST BITWA BLAD (...):
    /// wyjatek z moda" i rzut dalej, jak w zwyklej grze; same ramki gry w kroku rownym klikowi gracza -
    /// "AUTOTEST BITWA BLAD GRY (...)" (u gracza ten sam klik wywrocilby gre), bieg dalej. Skrypt liczy kazda linie
    /// "AUTOTEST BITWA BLAD" jako blad testu bitew.
    /// </summary>
    internal static partial class AutotestBattle
    {
        internal static int Battles;               // "battles" (0 = brak bitew)
        internal static int BattleSec = 150;       // "battle_s": sekundy rzeczywiste na jedna bitwe
        internal static bool ArenaOn = true;       // "arena": walka cwiczebna na arenie przed bitwami (tylko gdy battles > 0)
        private enum Kind { Strong, Retreat, Weak }
        private static readonly Kind[] Plan = { Kind.Strong, Kind.Retreat, Kind.Weak };
        private static readonly int[] EnemySizes = { 30, 40, 40 };            // wrogowie wedlug planu bitwy
        private const double ProgressEveryS = 20.0;
        private const double LeaveLimitS = 90.0, SpawnLimitS = 60.0, MeetLimitS = 45.0, MeetHardS = 90.0, AttackLimitS = 20.0, AttackHardS = 30.0;
        private const double LoadLimitS = 360.0, LoadStuckS = 300.0, AfterLimitS = 300.0, EndingLimitS = 180.0;
        private const double RetreatSec = 25.0;    // bitwa z planem odwrotu: odwrot najpozniej po tylu s
        private const int RetreatHits = 25;        // ... albo po tylu ciosach wrecz (walka trwa)
        private const double ArenaGoLimitS = 90.0, ArenaLoadLimitS = 300.0, ArenaSec = 120.0;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // m113-turniej: fazy turnieju na koncu (zakresy ArenaGo..ArenaEnd i Leave..Load bez zmian)
        private enum Ph { Idle, ArenaGo, ArenaLoad, ArenaFight, ArenaEnd, Leave, Spawn, Meet, Attack, Load, Fight, Ending, After, Cleanup, Over, TourGo, TourLoad, TourRun, TourEnd, TourAfter }
        private static Ph _ph = Ph.Idle;
        private static double _phAt, _actAt, _beginAt, _fightAt, _lastProgress;
        private static int _n;                     // biezaca bitwa 1..Battles
        private static int _played, _won, _lost, _timeouts, _notStarted, _errors, _gameErrors, _modErrors;
        private static bool _over;
        private static string _stopWhy;            // bitwy przerywane (limit czasu biegu, nie ruszyla, blad)

        // arena
        private static int _arenaDone, _arenaNot, _arenaBeaten = -1;
        private static bool _arenaKnown, _arenaPractice, _arenaPracticeOver, _arenaSurvived, _arenaGaveUp;
        private static double _arenaQuietSince = -1;

        // biezaca bitwa
        private static Kind _kind;
        private static MobileParty _enemy;
        private static CharacterObject _addedTroop;
        private static int _enemyMen, _ourMen, _added;
        private static bool _expectMission, _startLogged, _koniecLogged, _timeoutHit, _exitTried, _endForced, _noDeployNoted, _refreshed, _escaped, _encFinished, _immortal;
        private static Mission _mission;
        private static AutotestBattleLog _log;
        private static double _deploySeenAt, _deployCalledAt, _lastCharge, _lastFf, _prisonSince, _encSince, _idleSince, _stSince;
        private static int _deployTries, _chargeCount, _aiCount, _ffCount, _stTries;
        private static object _stObj;
        private static readonly HashSet<string> MenusNoted = new HashSet<string>();
        private static readonly List<MobileParty> Leftover = new List<MobileParty>();   // druzyny wroga, ktorych nie dalo sie usunac (MapEvent)
        private static bool _dieNoted;

        // bledy: pierwszy w danym miejscu w calosci, potem licznik
        private static readonly Dictionary<string, int> Errs = new Dictionary<string, int>();
        private static Exception _passThrough;

        // ------------------------------------------------------------------ przelacznik

        internal static void Parse(string json)
        {
            Battles = Autotest.Int(json, "battles", 0, 0, 50);
            BattleSec = Autotest.Int(json, "battle_s", 150, 20, 3600);
            ArenaOn = Autotest.Int(json, "arena", 1, 0, 1) == 1 && !string.Equals(Autotest.Raw(json, "arena"), "false", StringComparison.OrdinalIgnoreCase);
            ParseTour(json);   // m113-turniej: klucze "tournament" / "tournament_s"
        }

        // m113-turniej: etap istnieje takze przy samym turnieju (battles 0)
        internal static bool Pending { get { return (Battles > 0 || TourOn) && !_over; } }

        private static Kind KindOf(int n) { return Plan[(Math.Max(1, n) - 1) % Plan.Length]; }

        private static string KindName(Kind k)
        {
            return k == Kind.Strong ? "mocna" : k == Kind.Retreat ? "odwrot" : "slaba";
        }

        internal static string Describe()
        {
            if (Battles <= 0 && !TourOn) return "";
            string tour = TourDescribe();   // m113-turniej: "" bez klucza "tournament"
            if (Battles <= 0) return tour;
            var parts = new List<string>();
            for (int i = 1; i <= Math.Min(Battles, 6); i++) parts.Add(i + "=" + KindName(KindOf(i)) + "/" + EnemySizes[(i - 1) % EnemySizes.Length]);
            return (tour.Length > 0 ? tour + " + " : "") + (ArenaOn ? "arena (walka cwiczebna, do " + (int)ArenaSec + " s) + " : "") + Battles + " x bitwa w polu z bandytami (do " + BattleSec + " s rzeczywistych kazda; plan/wrogowie "
                   + string.Join(", ", parts.ToArray()) + (Battles > 6 ? ", ..." : "") + ") po osadzeniu w miescie, przed pierwsza doba biegu";
        }

        internal static string Summary()
        {
            if (Battles <= 0 && !TourOn) return "";
            if (Battles <= 0) return TourSummary();   // m113-turniej: sam turniej
            return TourSummary() + " | bitwy " + _played + "/" + Battles + " (wygrane " + _won + ", przegrane " + _lost + ", odwrot/limit " + _timeouts
                   + (_notStarted > 0 ? ", nie ruszyly " + _notStarted : "") + (_errors > 0 ? ", bledy bitwy " + _errors : "") + (_gameErrors > 0 ? " (w tym gry " + _gameErrors + ")" : "")
                   + (_modErrors > 0 ? ", wyjatki modow " + _modErrors : "") + (Leftover.Count > 0 ? ", druzyny wroga zostaly " + Leftover.Count : "") + ")"
                   + (ArenaOn ? " | arena " + (_arenaDone > 0 ? "KONIEC" : _arenaNot > 0 ? "NIE RUSZYLA" : "nie doszla") : "");
        }

        private static string Tag() { return "AUTOTEST BITWA " + _n + "/" + Battles + " [" + KindName(_kind) + "]"; }

        private static void Note(string s, bool mirror) { Autotest.Note(s, mirror); }

        // ------------------------------------------------------------------ bohater nie ginie w bitwach testu

        /// <summary>
        /// CampaignEvents.CanHeroDieEvent (AutotestBehavior, tylko w autotescie): bohater gracza w etapie bitew nie ginie w bitwie -
        /// SandboxAgentDecideKilledOrUnconsciousModel daje wtedy omdlenie, bitwa toczy sie normalnie.
        /// </summary>
        internal static void CanHeroDie(Hero hero, KillCharacterAction.KillCharacterActionDetail detail, ref bool result)
        {
            try
            {
                if (!result || !Autotest.Active || Autotest.Now != Autotest.Stage.Battle || hero == null || hero != Hero.MainHero) return;
                if (detail != KillCharacterAction.KillCharacterActionDetail.DiedInBattle) return;
                result = false;
                if (!_dieNoted)
                {
                    _dieNoted = true;
                    Note("AUTOTEST BITWA: bohater gracza w bitwach testu nie moze zginac (CampaignEvents.CanHeroDieEvent -> false, tylko DiedInBattle w etapie bitew; zamiast smierci omdlenie)", true);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------ bledy

        /// <summary>Wyjatek, ktory Guard puscil dalej (cudzy mod) - Autotest.Tick go nie lapie.</summary>
        internal static bool IsPassThrough(Exception e)
        {
            return e != null && ReferenceEquals(e, _passThrough);
        }

        // moduly gry (TaleWorlds) - nie "mody" przy podziale bledow; Bannerlord.Harmony to tylko narzedzie latek
        private static readonly HashSet<string> GameModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Native", "SandBox", "SandBoxCore", "StoryMode", "CustomBattle", "BirthAndDeath", "NavalDLC", "Multiplayer", "Bannerlord.Harmony"
        };

        private static bool IsBcl(string asm)
        {
            return asm == "mscorlib" || asm == "netstandard" || asm == "0Harmony"
                   || asm.StartsWith("System", StringComparison.OrdinalIgnoreCase) || asm.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                   || asm.StartsWith("MonoMod", StringComparison.OrdinalIgnoreCase) || asm.StartsWith("Mono.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Mody w sladzie stosu: tylko foldery z Blame.LoadedModules() bez modulow gry i bez BCL. first = wlasciciel pierwszej ramki
        /// spoza BCL: "CrashScribe", "gra" (TaleWorlds, SandBox... albo zestaw spoza folderu Modules) albo nazwa moda.
        /// </summary>
        internal static List<string> ModsInTrace(Exception ex, out string first)
        {
            first = null;
            var res = new List<string>();
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var x in Blame.LoadedModules()) loaded.Add(x); } catch { }
            StackFrame[] frames = null;
            try { frames = new StackTrace(ex, false).GetFrames(); } catch { }
            if (frames == null) return res;
            var self = typeof(AutotestBattle).Assembly;
            foreach (var f in frames)
            {
                MethodBase m = null;
                try { m = f.GetMethod(); } catch { }
                var dt = m != null ? m.DeclaringType : null;
                if (dt == null) continue;   // metody dynamiczne (latki Harmony) - ramka prefiksu moda i tak ma swoj typ
                string asm = null;
                try { asm = dt.Assembly.GetName().Name; } catch { }
                if (string.IsNullOrEmpty(asm) || IsBcl(asm)) continue;
                bool own = dt.Assembly == self;
                string mod = own ? "CrashScribe" : asm.StartsWith("TaleWorlds.", StringComparison.OrdinalIgnoreCase) ? "Native" : Blame.ModuleOfAssemblyName(asm);
                bool game = !own && (string.IsNullOrEmpty(mod) || GameModules.Contains(mod) || !loaded.Contains(mod));
                if (first == null) first = own ? "CrashScribe" : game ? "gra" : mod;
                if (own || game) continue;
                if (!res.Contains(mod)) res.Add(mod);
                if (res.Count >= 5) break;
            }
            return res;
        }

        /// <summary>
        /// Krok autotestu w ochronie: blad autotestu (pierwsza ramka w CrashScribe) - linia "AUTOTEST BITWA BLAD", false;
        /// mod w sladzie - linia i rzut dalej (bez polykania); same ramki gry - linia "AUTOTEST BITWA BLAD GRY", false.
        /// </summary>
        private static bool Guard(string where, Action a)
        {
            try { a(); return true; }
            catch (Exception e)
            {
                if (IsPassThrough(e)) throw;   // juz opisany w wewnetrznym Guard - dalej bez drugiej linii
                var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                List<string> mods = null;
                string first = null;
                try { mods = ModsInTrace(inner, out first); } catch { }
                if (first != "CrashScribe" && mods != null && mods.Count > 0)
                {
                    try
                    {
                        _modErrors++;
                        int n = Count("mod:" + where);
                        if (n <= 3 || n % 25 == 0)
                            Note("AUTOTEST BITWA BLAD (" + where + "): wyjatek z moda " + string.Join(", ", mods.ToArray()) + " - " + inner.GetType().Name + ": "
                                 + Autotest.OneLine(inner.Message, 200) + (n > 1 ? " (x" + n + ")" : "") + " (bitwa " + _n + "/" + Battles + ", " + Autotest.Where() + ") - NIE polykam, idzie dalej jak w zwyklej grze", true);
                        if (n == 40 && _stopWhy == null) _stopWhy = "powtarzajacy sie wyjatek moda w " + where;
                    }
                    catch { }
                    _passThrough = e;
                    throw;
                }
                if (first == null || first == "CrashScribe") Own(where, inner);
                else GameErr(where, inner);
                return false;
            }
        }

        private static int Count(string key)
        {
            int n;
            Errs.TryGetValue(key, out n);
            Errs[key] = ++n;
            return n;
        }

        private static void Own(string where, Exception e)
        {
            try
            {
                _errors++;
                int n = Count(where);
                if (n == 1)
                {
                    Note("AUTOTEST BITWA BLAD: " + where + ": " + e.GetType().Name + ": " + Autotest.OneLine(e.Message, 200) + " (blad autotestu - pierwsza ramka w CrashScribe; bitwa " + _n + "/" + Battles + ", " + Autotest.Where() + ")", true);
                    try { Scribe.Report("AUTOTEST BITWA", e, "AutotestBattle." + where, null); } catch { }
                }
                else if (n % 25 == 0) Note("AUTOTEST BITWA BLAD: " + where + " x" + n + " (ostatni: " + e.GetType().Name + ")", true);
                // ten sam blad w kolko - ta bitwa sie nie uda; koniec bitew, zwykly bieg dalej
                if (n == 40 && _stopWhy == null) _stopWhy = "powtarzajacy sie blad w " + where;
            }
            catch { }
        }

        /// <summary>Wyjatek gry (same ramki TaleWorlds / SandBox, bez moda) w kroku rownym klikowi gracza - u gracza ten sam klik wywrocilby gre.</summary>
        private static void GameErr(string where, Exception e)
        {
            try
            {
                _errors++;
                _gameErrors++;
                int n = Count(where);
                if (n == 1)
                {
                    Note("AUTOTEST BITWA BLAD GRY (" + where + "): wyjatek gry bez moda w sladzie, w kroku rownym klikowi gracza - " + e.GetType().Name + ": " + Autotest.OneLine(e.Message, 200)
                         + " (bitwa " + _n + "/" + Battles + ", " + Autotest.Where() + ") - u gracza ten sam klik wywrocilby gre", true);
                    try { Scribe.Report("AUTOTEST BITWA GRA", e, "AutotestBattle." + where, null); } catch { }
                }
                else if (n % 25 == 0) Note("AUTOTEST BITWA BLAD GRY (" + where + ") x" + n + " (ostatni: " + e.GetType().Name + ")", true);
                if (n == 40 && _stopWhy == null) _stopWhy = "powtarzajacy sie blad gry w " + where;
            }
            catch { }
        }

        private static void Err(string text)
        {
            _errors++;
            Note("AUTOTEST BITWA BLAD: " + text + " (bitwa " + _n + "/" + Battles + ")", true);
        }

        // ------------------------------------------------------------------ wejscie z Autotest

        internal static void Begin(double t)
        {
            _n = 1;
            _kind = KindOf(1);
            _beginAt = t;
            ResetBattle();
            Note("AUTOTEST BITWA plan: " + Describe() + " | " + Autotest.Where(), true);
            // m113-turniej: najpierw turniej w miescie (potem arena i bitwy jak dotad; przy battles 0 tylko turniej)
            Go(TourOn ? Ph.TourGo : ArenaOn ? Ph.ArenaGo : Ph.Leave, t);
        }

        /// <summary>Miekki limit biegu w trakcie bitew: biezaca bitwa konczy sie odwrotem, nastepnych nie ma.</summary>
        internal static void StopAll(string why)
        {
            if (_stopWhy == null)
            {
                _stopWhy = why;
                Note("AUTOTEST BITWA: przerwanie bitew - " + why, true);
            }
        }

        private static bool InMissionPhase() { return _ph == Ph.ArenaLoad || _ph == Ph.ArenaFight || _ph == Ph.ArenaEnd || _ph == Ph.Load || _ph == Ph.Fight || _ph == Ph.Ending || _ph == Ph.After || TourMission(); }

        /// <summary>
        /// Koniec biegu (Autotest.Finish) w trakcie bitew: etap bitew zamkniety, misja dostaje tylko zadanie zamkniecia
        /// (Mission.EndMission = stan EndingNextFrame). Zwraca true, gdy misja jest otwarta - Finish czeka wtedy na mape
        /// (do 60 s), a dopiero potem robi zapis i wyjscie.
        /// </summary>
        internal static bool Abort(string why)
        {
            if (_over) return false;
            _over = true;
            if (_stopWhy == null) _stopWhy = why;
            bool open = false;
            try
            {
                var m = Mission.Current;
                if (m != null)
                {
                    open = true;
                    // m113-turniej: turniej konczy sie jak "Leave" gracza (pominiecie reszty meczy + Mission.EndMission)
                    if (!m.MissionEnded && TourMission()) TourQuit(m, "koniec biegu: " + why);
                    else if (!m.MissionEnded && (ReferenceEquals(m, _mission) || InMissionPhase())) m.EndMission();
                }
            }
            catch (Exception e) { Own("Abort", e); }
            try
            {
                if (_enemy != null) { if (!Leftover.Contains(_enemy)) Leftover.Add(_enemy); _enemy = null; }
                Sweep("koniec biegu w trakcie bitew");
            }
            catch (Exception e) { Own("Abort.Sweep", e); }
            Note("AUTOTEST BITWY przerwane: " + why + Summary() + (open ? " | misja otwarta: zadanie zamkniecia (Mission.EndMission), zapis i wyjscie po powrocie na mape" : ""), true);
            return open;
        }

        /// <summary>Ponowna proba usuniecia druzyn wroga, ktore przy sprzataniu byly w MapEvent (koniec bitew, start biegu dob, koniec biegu).</summary>
        internal static void Sweep(string when)
        {
            if (Leftover.Count == 0 || Campaign.Current == null) return;
            foreach (var e in Leftover.ToArray())
            {
                string id = "?";
                try
                {
                    id = e != null ? e.StringId : "null";
                    if (e == null || !e.IsActive) { Leftover.Remove(e); Note("AUTOTEST BITWA: druzyna wroga " + id + " juz nieaktywna (" + when + ")", false); continue; }
                    if (e.MapEvent != null) continue;
                    int left = e.MemberRoster.TotalManCount;
                    DestroyPartyAction.Apply(null, e);
                    Leftover.Remove(e);
                    Note("AUTOTEST BITWA: druzyna wroga " + id + " usunieta przy ponownej probie (" + when + ", zostalo w niej " + left + " ludzi)", true);
                }
                catch (Exception ex) { Leftover.Remove(e); Own("DestroyParty (ponownie)", ex); }
            }
            if (Leftover.Count > 0)
                Note("AUTOTEST BITWA: druzyny wroga ZOSTAJA (" + when + "): " + string.Join(", ", Leftover.Select(x => x != null ? x.StringId : "?").ToArray()) + " - nadal w MapEvent", true);
        }

        /// <summary>SubModuleMain.OnMissionBehaviorInitialize: liczniki do misji, ktora otworzyl nasz "Attack" / "Practice fight".</summary>
        internal static void OnMissionInit(Mission mission)
        {
            if (!Autotest.Active || Autotest.Now != Autotest.Stage.Battle || !_expectMission || mission == null) return;
            try
            {
                _expectMission = false;
                _mission = mission;
                _log = new AutotestBattleLog();
                mission.AddMissionBehavior(_log);
                Note(Pfx() + ": liczniki dolaczone do misji (OnMissionBehaviorInitialize, zachowan misji " + mission.MissionBehaviors.Count + ")", false);
            }
            catch (Exception e) { Own("OnMissionInit", e); }
        }

        private static bool IsArena() { return _ph >= Ph.ArenaGo && _ph <= Ph.ArenaEnd; }

        /// <summary>m113-turniej: przedrostek linii wedlug fazy (turniej / arena / bitwa) - poza turniejem jak dotad.</summary>
        private static string Pfx() { return IsTour() ? "AUTOTEST TURNIEJ" : IsArena() ? "AUTOTEST ARENA" : Tag(); }

        // ------------------------------------------------------------------ krok (Autotest.Tick, 4 razy na sekunde)

        internal static void Step(double t)
        {
            var c = Campaign.Current;
            if (c == null || Autotest.ActiveState() is InitialState)
            {
                Autotest.Finish("gra wrocila do menu glownego w trakcie bitew", false, t);
                return;
            }
            Ph ph = _ph;
            Guard("faza " + ph, () =>
            {
                switch (ph)
                {
                    case Ph.ArenaGo: StepArenaGo(c, t); break;
                    case Ph.ArenaLoad: StepArenaLoad(t); break;
                    case Ph.ArenaFight: StepArenaFight(c, t); break;
                    case Ph.ArenaEnd: StepArenaEnd(c, t); break;
                    case Ph.Leave: StepLeave(c, t); break;
                    case Ph.Spawn: StepSpawn(c, t); break;
                    case Ph.Meet: StepMeet(c, t); break;
                    case Ph.Attack: StepAttack(c, t); break;
                    case Ph.Load: StepLoad(t); break;
                    case Ph.Fight: StepFight(t); break;
                    case Ph.Ending: StepEnding(t); break;
                    case Ph.After: StepAfter(c, t); break;
                    case Ph.Cleanup: StepCleanup(t); break;
                    case Ph.TourGo: StepTourGo(c, t); break;       // m113-turniej
                    case Ph.TourLoad: StepTourLoad(t); break;
                    case Ph.TourRun: StepTourRun(c, t); break;
                    case Ph.TourEnd: StepTourEnd(c, t); break;
                    case Ph.TourAfter: StepTourAfter(c, t); break;
                    case Ph.Idle:
                    case Ph.Over:
                        if (!_over) Over(t);
                        else Autotest.BattlesOver(t, "bitwy za nami");
                        break;
                }
            });
            // powtarzajacy sie blad przed misja: arena pominieta / bitwy koncza sie (sprzatanie po bitwie), bieg dob idzie dalej
            if (_stopWhy != null && Errs.Values.Any(v => v >= 40))
            {
                if (_ph == Ph.TourGo || _ph == Ph.TourLoad) TourNot(t, "powtarzajacy sie blad (" + _stopWhy + ")");   // m113-turniej
                else if (_ph == Ph.ArenaGo || _ph == Ph.ArenaLoad) ArenaNot(t, "powtarzajacy sie blad (" + _stopWhy + ")");
                else if (_ph >= Ph.Leave && _ph < Ph.Load) Go(Ph.After, t);
            }
        }

        private static void Go(Ph p, double t)
        {
            _ph = p;
            _phAt = t;
            _actAt = 0;
        }

        private static void StopTime(Campaign c)
        {
            if (!c.TimeControlModeLock && c.TimeControlMode != CampaignTimeControlMode.Stop) c.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        private static bool Talking(Campaign c)
        {
            return c.ConversationManager != null && c.ConversationManager.IsConversationInProgress;
        }

        private static void ResetBattle()
        {
            _enemy = null;
            _addedTroop = null;
            _enemyMen = _ourMen = _added = 0;
            _expectMission = _startLogged = _koniecLogged = _timeoutHit = _exitTried = _endForced = _noDeployNoted = _refreshed = _escaped = _encFinished = _immortal = false;
            _mission = null;
            _log = null;
            _deploySeenAt = _deployCalledAt = _lastCharge = _lastFf = _prisonSince = _encSince = _idleSince = _stSince = -1;
            _deployTries = _chargeCount = _aiCount = _ffCount = _stTries = 0;
            _stObj = null;
            _lastProgress = 0;
            _fightAt = 0;
            MenusNoted.Clear();
        }

        /// <summary>Bitwa nie ruszyla: linia, reszta bitew pominieta, sprzatanie (menu, spotkanie, druzyna wroga) i bieg dob.</summary>
        private static void NotStarted(double t, string reason)
        {
            _notStarted++;
            _expectMission = false;
            Note("AUTOTEST BITWA NIE RUSZYLA: " + reason + " (bitwa " + _n + "/" + Battles + ", plan " + KindName(_kind) + ")" + (_n < Battles ? " - pozostale bitwy pominiete" : "") + " - dalej zwykly bieg dob", true);
            if (_stopWhy == null) _stopWhy = "bitwa " + _n + " nie ruszyla";
            Go(Ph.After, t);
        }

        private static bool Hero_Heal(Hero hero, string why)
        {
            if (hero == null || !hero.IsWounded) return false;
            int before = hero.HitPoints;
            hero.HitPoints = hero.MaxHitPoints;
            Note(Pfx() + ": bohater ranny (" + before + "/" + hero.MaxHitPoints + " HP) - leczenie na czas testu (" + why + ")", true);
            return true;
        }

        // ------------------------------------------------------------------ 0. arena (walka cwiczebna) - gracz czeka w miescie

        private static void ArenaNot(double t, string reason)
        {
            _arenaNot++;
            _expectMission = false;
            Note("AUTOTEST ARENA NIE RUSZYLA: " + reason + " - dalej bitwy w polu", true);
            try
            {
                var m = Mission.Current;
                if (m != null && ReferenceEquals(m, _mission) && !m.MissionEnded) { Note("AUTOTEST ARENA: zamykam misje areny (Mission.EndMission)", true); m.EndMission(); }
            }
            catch (Exception e) { Own("ArenaNot.EndMission", e); }
            ResetBattle();
            Go(Ph.Leave, t);
        }

        private static MissionBehavior ArenaCtrl(Mission m)
        {
            if (m == null) return null;
            foreach (var b in m.MissionBehaviors)
            {
                if (b == null) continue;
                for (var ty = b.GetType(); ty != null; ty = ty.BaseType)
                    if (ty.Name == "ArenaPracticeFightMissionController") return b;
            }
            return null;
        }

        private static bool Flag(object o, string name)
        {
            var v = AutotestUi.Get(o, name);
            return v is bool && (bool)v;
        }

        /// <summary>
        /// Opcja "Practice fight" wymaga, zeby gracz "znal" arene (ArenaMasterCampaignBehavior._knowTournaments - w grze po
        /// pierwszej rozmowie z mistrzem areny). Zapis testowy moze go nie miec - tylko w tescie ustawiamy go jak po tej rozmowie.
        /// </summary>
        private static string EnsureArenaKnown()
        {
            var ty = AutotestUi.FindType("SandBox", "SandBox.CampaignBehaviors.ArenaMasterCampaignBehavior");
            if (ty == null) return "brak typu SandBox.CampaignBehaviors.ArenaMasterCampaignBehavior";
            var gm = typeof(Campaign).GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x => x.Name == "GetCampaignBehavior" && x.IsGenericMethodDefinition && x.GetParameters().Length == 0);
            object beh = gm != null ? gm.MakeGenericMethod(ty).Invoke(Campaign.Current, null) : null;
            if (beh == null) return "brak zachowania ArenaMasterCampaignBehavior w kampanii";
            if (Flag(beh, "_knowTournaments")) return "gracz zna arene (ArenaMasterCampaignBehavior._knowTournaments = tak)";
            if (AutotestUi.Set(beh, "_knowTournaments", true))
                return "gracz NIE znal areny - ustawiam ArenaMasterCampaignBehavior._knowTournaments = true (tylko test, zapis testowy; w grze po pierwszej rozmowie z mistrzem areny)";
            return "nie da sie ustawic ArenaMasterCampaignBehavior._knowTournaments";
        }

        private static void StepArenaGo(Campaign c, double t)
        {
            if (t - _phAt > ArenaGoLimitS) { ArenaNot(t, "nie doszedl do walki cwiczebnej w " + (int)ArenaGoLimitS + " s (" + Autotest.Where() + ")"); return; }
            if (_stopWhy != null) { ArenaNot(t, "bitwy przerwane: " + _stopWhy); return; }
            StopTime(c);
            if (AutotestUi.AnyWindowOpen()) return;
            if (Talking(c))
            {
                if (t - _actAt > 2.5) { _actAt = t; Note("AUTOTEST ARENA: rozmowa w drodze na arene - koncze (EndConversation)", true); c.ConversationManager.EndConversation(); }
                return;
            }
            if (!(Autotest.ActiveState() is MapState)) return;
            var mp = MobileParty.MainParty;
            var hero = Hero.MainHero;
            if (mp == null || hero == null) return;
            var town = mp.CurrentSettlement;
            if (town == null || !town.IsTown) { ArenaNot(t, "gracz nie jest w miescie (" + Autotest.Where() + ")"); return; }
            var ctx = c.CurrentMenuContext;
            var menu = ctx != null ? ctx.GameMenu : null;
            if (t - _actAt < 1.5 || menu == null) return;
            _actAt = t;
            string id = menu.StringId;
            Hero_Heal(hero, "ranny nie moze walczyc na arenie");
            if (id == "town_wait_menus")
            {
                if (Autotest.ClickOption(ctx, "wait_leave")) Note("AUTOTEST ARENA: klikam \"Stop waiting\" [wait_leave] w " + town.Name, true);
                return;
            }
            if (id == "town")
            {
                if (Autotest.ClickOption(ctx, "town_arena")) Note("AUTOTEST ARENA: klikam \"Go to the arena\" [town_arena]", true);
                else ArenaNot(t, "w menu town opcja town_arena niedostepna (opcje " + Autotest.ListOptions(ctx) + ")");
                return;
            }
            if (id == "town_arena")
            {
                if (!_arenaKnown)
                {
                    _arenaKnown = true;
                    string k;
                    try { k = EnsureArenaKnown(); } catch (Exception e) { var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; k = "sprawdzenie _knowTournaments rzucilo " + inner.GetType().Name + ": " + Autotest.OneLine(inner.Message, 120); }
                    Note("AUTOTEST ARENA: " + town.Name + " [" + town.StringId + "] menu town_arena, opcje " + Autotest.ListOptions(ctx) + " | " + k, true);
                    return;   // opcje menu licza sie od nowa przy nastepnym kliknieciu
                }
                bool tour = false;
                try { tour = town.Town != null && town.Town.HasTournament; } catch { }
                _expectMission = true;
                Go(Ph.ArenaLoad, t);
                Note("AUTOTEST ARENA: klikam \"Practice fight\" [mno_enter_practice_fight] (to samo, co gracz: ArenaMasterCampaignBehavior -> LocationEncounter.CreateAndOpenMissionController(arena) + StartPlayerPractice)", true);
                if (!Autotest.ClickOption(ctx, "mno_enter_practice_fight"))
                {
                    _expectMission = false;
                    ArenaNot(t, "opcja mno_enter_practice_fight niedostepna (opcje " + Autotest.ListOptions(ctx) + (tour ? ", w miescie trwa turniej" : "") + ")");
                }
                return;
            }
            // inne podmenu miasta: "Back" do menu town
            if (!Autotest.ClickLeave(ctx) && MenusNoted.Add(id)) Note("AUTOTEST ARENA: menu " + id + " bez wyjscia - czekam (opcje " + Autotest.ListOptions(ctx) + ")", false);
        }

        private static void StepArenaLoad(double t)
        {
            if (t - _phAt > ArenaLoadLimitS) { ArenaNot(t, "misja areny nie ruszyla w " + (int)ArenaLoadLimitS + " s (" + Autotest.Where() + ")"); return; }
            var m = Mission.Current;
            if (m == null)
            {
                if (Autotest.ActiveState() is MapState && t - _phAt > 25) ArenaNot(t, "po \"Practice fight\" misja nie ruszyla w 25 s (" + Autotest.Where() + ")");
                return;
            }
            if (_mission == null) _mission = m;
            if (!ReferenceEquals(m, _mission)) { ArenaNot(t, "na wierzchu obca misja (" + Autotest.Where() + ")"); return; }
            if (m.CurrentState != Mission.State.Continuing) return;
            if (_log == null || !m.MissionBehaviors.Contains(_log))
            {
                _log = new AutotestBattleLog();
                m.AddMissionBehavior(_log);
                Note("AUTOTEST ARENA: liczniki dolaczone pozno - ciosy liczone od teraz", true);
            }
            _expectMission = false;
            _log.Scene = SceneOf(m);
            var ctrl = ArenaCtrl(m);
            _arenaPractice = ctrl != null && Flag(ctrl, "IsPlayerPracticing");
            Note("AUTOTEST ARENA START: misja ruszyla po " + (t - _phAt).ToString("0", Inv) + " s ladowania (scena " + _log.Scene + ", " + m.GetType().Name + ", zachowan " + m.MissionBehaviors.Count
                 + ") | kontroler walki cwiczebnej " + (ctrl != null ? ctrl.GetType().Name : "BRAK") + ", walka gracza " + (_arenaPractice ? "trwa" : "NIE trwa") + " | limit " + (int)ArenaSec + " s", true);
            _fightAt = t;
            _lastProgress = t;
            _arenaQuietSince = -1;
            Go(Ph.ArenaFight, t);
        }

        private static void StepArenaFight(Campaign c, double t)
        {
            var m = Mission.Current;
            if (m == null || !ReferenceEquals(m, _mission)) { ArenaKoniec(t, "misja areny zamknieta poza autotestem"); ResetBattle(); Go(Ph.Leave, t); return; }
            Track(m);
            if (m.MissionEnded) { Go(Ph.ArenaEnd, t); return; }
            // twardy limit przed rozmowa i oknami: rozmowa, ktorej EndConversation nie konczy, nie trzyma misji bez konca
            if (t - _phAt > ArenaSec + 60)
            {
                Err("arena: walka albo rozmowa trzyma misje " + (int)(ArenaSec + 60) + " s (" + Autotest.Where() + ") - Mission.EndMission()");
                m.EndMission();
                Go(Ph.ArenaEnd, t);
                return;
            }
            var ctrl = ArenaCtrl(m);
            bool practicing = ctrl != null && Flag(ctrl, "IsPlayerPracticing");
            if (practicing)
            {
                _arenaPractice = true;
                try { var v = AutotestUi.Get(ctrl, "OpponentCountBeatenByPlayer"); if (v is int) _arenaBeaten = (int)v; } catch { }
            }
            else if (_arenaPractice && !_arenaPracticeOver)
            {
                _arenaPracticeOver = true;
                _arenaSurvived = ctrl != null && Flag(ctrl, "IsPlayerSurvived");
                Note("AUTOTEST ARENA: walka cwiczebna skonczona po " + (t - _fightAt).ToString("0", Inv) + " s (bohater " + (_arenaSurvived ? "wygral" : "pokonany") + ", pokonal " + Math.Max(0, _arenaBeaten) + ") - gra przenosi go do mistrza areny", true);
            }
            if (Talking(c))
            {
                _arenaQuietSince = -1;
                if (t - _actAt > 2.5) { _actAt = t; Note("AUTOTEST ARENA: rozmowa w arenie (mistrz areny po walce) - koncze (EndConversation)", true); c.ConversationManager.EndConversation(); }
                return;
            }
            bool noPractice = !_arenaPractice && t - _fightAt > 20;
            bool timeUp = t - _fightAt > ArenaSec || _stopWhy != null;   // limit areny albo przerwanie (miekki limit biegu)
            if (_arenaPracticeOver || noPractice || timeUp)
            {
                if (_arenaQuietSince < 0) { _arenaQuietSince = t; return; }
                if (t - _arenaQuietSince < 2.0) return;   // po walce gra zaczyna rozmowe z mistrzem areny (przeniesienie po 5 s)
                if (practicing)
                {
                    _arenaGaveUp = true;
                    Note("AUTOTEST ARENA: " + (_stopWhy != null ? "przerwanie (" + _stopWhy + ")" : "limit " + (int)ArenaSec + " s minal") + " w trakcie walki - poddanie jak gracz (Tab -> \"Give up\" OK: Mission.OnEndMissionResult)", true);
                    m.OnEndMissionResult();
                }
                else
                {
                    Note("AUTOTEST ARENA: wyjscie z areny jak gracz (Tab -> Mission.EndMission)" + (noPractice && !_arenaPracticeOver ? " - walka gracza NIE ruszyla w 20 s (IsPlayerPracticing = nie)" : ""), true);
                    m.EndMission();
                }
                Go(Ph.ArenaEnd, t);
                return;
            }
            if (t - _lastProgress >= ProgressEveryS)
            {
                _lastProgress = t;
                var L = _log ?? new AutotestBattleLog();
                Note("AUTOTEST ARENA postep: czas misji " + m.CurrentTime.ToString("0", Inv) + " s (rzeczywisty " + (t - _fightAt).ToString("0", Inv) + " s), ciosy wrecz " + L.Melee + ", strzaly " + L.Missile
                     + ", nieprzytomni " + L.Unconscious + ", zabici " + L.Killed + " | walka gracza " + (practicing ? "trwa" : "nie") + ", pokonani przez bohatera " + Math.Max(0, _arenaBeaten), false);
            }
        }

        private static void StepArenaEnd(Campaign c, double t)
        {
            var m = Mission.Current;
            if (m != null && ReferenceEquals(m, _mission))
            {
                Track(m);
                if (Talking(c) && t - _actAt > 2.5) { _actAt = t; Note("AUTOTEST ARENA: rozmowa przy wyjsciu - koncze", false); c.ConversationManager.EndConversation(); }
                if (!_endForced && t - _phAt > 15) { _endForced = true; Note("AUTOTEST ARENA: misja nie zamknela sie w 15 s - Mission.EndMission()", true); m.EndMission(); }
                if (t - _phAt > EndingLimitS)
                {
                    Err("misja areny nie zamknela sie w " + (int)EndingLimitS + " s (" + Autotest.Where() + ")");
                    ArenaKoniec(t, "misja areny nie zamknela sie");
                    ResetBattle();
                    Go(Ph.Leave, t);
                }
                return;
            }
            ArenaKoniec(t, null);
            ResetBattle();
            Go(Ph.Leave, t);
        }

        private static void ArenaKoniec(double t, string note)
        {
            if (_arenaDone > 0) return;
            _arenaDone++;
            var L = _log;
            if (L == null) { L = new AutotestBattleLog(); note = (note != null ? note + "; " : "") + "liczniki NIE byly podlaczone"; }
            string wynik = !_arenaPractice ? "bez walki gracza" : _arenaGaveUp ? "poddanie (limit)" : _arenaPracticeOver ? (_arenaSurvived ? "wygrana" : "bohater pokonany") : "przerwana";
            Note("AUTOTEST ARENA KONIEC: ciosy wrecz " + L.Melee + ", strzaly " + L.Missile + ", nieprzytomni " + L.Unconscious + ", zabici " + L.Killed + ", czas " + L.LastTime.ToString("0", Inv) + " s, wynik " + wynik
                 + " | pokonani przez bohatera " + Math.Max(0, _arenaBeaten) + ", kolizje wrecz " + L.MeleeColl + " / pociski " + L.MissileColl + ", scena " + L.Scene
                 + ", czas rzeczywisty " + (_fightAt > 0 ? (t - _fightAt).ToString("0", Inv) : "?") + " s, bledy bitwy " + _errors + (note != null ? " | " + note : ""), true);
        }

        // ------------------------------------------------------------------ 1. wyjscie z miasta

        private static void StepLeave(Campaign c, double t)
        {
            if (t - _phAt > LeaveLimitS) { NotStarted(t, "nie da sie wyjsc z miasta w " + (int)LeaveLimitS + " s (" + Autotest.Where() + ")"); return; }
            if (_stopWhy != null) { NotStarted(t, "bitwy przerwane przed startem: " + _stopWhy); return; }
            StopTime(c);
            if (AutotestUi.AnyWindowOpen()) return;
            if (Talking(c))
            {
                if (t - _actAt > 2.5) { _actAt = t; Note(Tag() + ": rozmowa w trakcie wyjscia z miasta - koncze (EndConversation)", true); c.ConversationManager.EndConversation(); }
                return;
            }
            if (!(Autotest.ActiveState() is MapState)) return;   // wydarzenie ROT, misja areny konczy sie itp.
            var mp = MobileParty.MainParty;
            if (mp == null) return;
            var ctx = c.CurrentMenuContext;
            var menu = ctx != null ? ctx.GameMenu : null;
            if (mp.CurrentSettlement == null && menu == null && PlayerEncounter.Current == null && mp.MapEvent == null)
            {
                Note(Tag() + ": druzyna poza miastem, czas STOP (" + Autotest.Where() + ")", false);
                Go(Ph.Spawn, t);
                return;
            }
            if (t - _actAt < 1.5 || menu == null) return;
            _actAt = t;
            string id = menu.StringId;
            if (id == "town_wait_menus" && Autotest.ClickOption(ctx, "wait_leave")) Note(Tag() + ": klikam \"Stop waiting\" [wait_leave]", true);
            else if (id == "town" && Autotest.ClickOption(ctx, "town_leave")) Note(Tag() + ": klikam \"Leave\" [town_leave] - druzyna wychodzi pod brame", true);
            else if (!Autotest.ClickLeave(ctx)) Note(Tag() + ": menu " + id + " bez wyjscia - czekam (opcje " + Autotest.ListOptions(ctx) + ")", false);
        }

        // ------------------------------------------------------------------ 2. wrog obok gracza i spotkanie

        private static string Blocked(MobileParty mp, Hero hero)
        {
            if (mp == null || !mp.IsActive) return "brak aktywnej druzyny gracza";
            if (hero == null || hero.IsDead) return "brak zywego bohatera gracza";
            if (hero.IsPrisoner) return "bohater gracza w niewoli";
            if (hero.PartyBelongedTo != mp) return "bohater gracza poza swoja druzyna";
            if (mp.Army != null) return "gracz w armii (" + mp.Army.Name + ")";
            if (mp.MapEvent != null) return "druzyna gracza juz w bitwie";
            if (PlayerEncounter.Current != null) return "gracz w trakcie spotkania";
            if (mp.CurrentSettlement != null) return "gracz w osadzie " + mp.CurrentSettlement.Name;
            if (mp.IsCurrentlyAtSea) return "druzyna gracza na morzu";
            return null;
        }

        private static void StepSpawn(Campaign c, double t)
        {
            if (t - _phAt > SpawnLimitS) { NotStarted(t, "przez " + (int)SpawnLimitS + " s nie da sie postawic wroga (" + Autotest.Where() + ")"); return; }
            if (_stopWhy != null) { NotStarted(t, "bitwy przerwane: " + _stopWhy); return; }
            StopTime(c);
            if (t - _phAt < 1.5 || AutotestUi.AnyWindowOpen() || Talking(c)) return;
            if (!(Autotest.ActiveState() is MapState)) return;
            var mp = MobileParty.MainParty;
            var hero = Hero.MainHero;
            string why = Blocked(mp, hero);
            if (why != null) { NotStarted(t, why + " (" + Autotest.Where() + ")"); return; }

            // bohater zdrowy - ranny ma zablokowana opcje Attack (MenuHelper.EncounterAttackCondition)
            Hero_Heal(hero, "ranny nie moze zaatakowac");

            // wrog juz powstal, a krok wywrocil sie dalej (linia AUTOTEST BITWA BLAD) - bez drugiej druzyny; sprzatanie po bitwie
            if (_enemy != null) { NotStarted(t, "tworzenie wroga przerwane bledem (zob. linie AUTOTEST BITWA BLAD)"); return; }

            int want = EnemySizes[(_n - 1) % EnemySizes.Length];
            Hideout ho;
            Clan clan = PickBandits(mp, out ho);
            if (clan == null) { NotStarted(t, "brak klanu bandytow (Clan.BanditFactions)"); return; }

            // druzyna gracza wedlug planu bitwy (dokladka tylko w tescie, po bitwie wraca)
            int healthy = mp.MemberRoster.TotalHealthyCount;
            _added = 0;
            _addedTroop = null;
            string plan;
            if (_kind == Kind.Weak)
            {
                if (healthy > 15) want = Math.Min(150, healthy * 3);
                plan = "slaba druzyna: bez dokladki (" + healthy + " zdrowych) przeciw " + want + " - przegrana / niewola";
            }
            else
            {
                string how;
                var tr = _kind == Kind.Strong ? StrongTroop(hero, mp, out how) : OurTroop(hero, mp, out how);
                int need = _kind == Kind.Strong ? want * 2 : want + 10;
                plan = (_kind == Kind.Strong ? "mocna druzyna: " + need + " zdrowych przeciw " + want + " - wygrana i lup" : "odwrot: " + need + " zdrowych przeciw " + want + " - odwrot w trakcie walki");
                if (healthy < need)
                {
                    if (tr != null)
                    {
                        _added = Math.Min(need - healthy, 100);
                        _addedTroop = tr;
                        mp.MemberRoster.AddToCounts(tr, _added);
                        Note(Tag() + ": druzyna gracza miala " + healthy + " zdrowych - dokladam " + _added + " x " + tr.Name + " [" + tr.StringId + ", poziom " + SafeTier(tr) + ", " + how + "] (tylko test, po bitwie wracaja)", true);
                    }
                    else Note(Tag() + ": druzyna gracza ma " + healthy + " zdrowych, brak jednostki do dolozenia (" + how + ")", true);
                }
            }
            _ourMen = mp.MemberRoster.TotalHealthyCount;

            CampaignVec2 pos = NavigationHelper.FindPointAroundPosition(mp.Position, MobileParty.NavigationType.Default, 1.5f, 0.4f);
            string pid = "autotest_bitwa_" + _n + "_" + DateTime.Now.ToString("HHmmss", Inv);
            Settlement near = null;
            MobileParty bp = ho != null
                ? BanditPartyComponent.CreateBanditParty(pid, clan, ho, false, null, pos)
                : BanditPartyComponent.CreateLooterParty(pid, clan, near = NearestTown(mp), false, null, pos);
            if (bp == null) { NotStarted(t, "BanditPartyComponent.Create" + (ho != null ? "Bandit" : "Looter") + "Party zwrocil null"); return; }
            _enemy = bp;
            string fill = Fill(bp, clan, want);
            if (bp.MemberRoster.TotalManCount <= 0) { NotStarted(t, "druzyna wroga pusta (" + fill + ")"); return; }
            bp.InitializePartyTrade(200);
            bp.Party.SetVisualAsDirty();
            _enemyMen = bp.MemberRoster.TotalHealthyCount;

            bool canDie = false;
            try { canDie = hero.CanDie(KillCharacterAction.KillCharacterActionDetail.DiedInBattle); } catch { }
            Note(Tag() + ": plan " + plan + " | wrog \"" + bp.Name + "\" [" + pid + "] klan " + clan.StringId + " (kultura " + (clan.Culture != null ? clan.Culture.StringId : "?") + "), " + _enemyMen + " ludzi: " + fill
                 + (ho != null ? " | kryjowka " + ho.Settlement.StringId : " | looterzy przy " + (near != null ? near.StringId : "-"))
                 + " | druzyna gracza " + _ourMen + " zdrowych" + (_added > 0 ? " (w tym " + _added + " dolozonych)" : "")
                 + " | bohater moze zginac w bitwie: " + (canDie ? "TAK (CanHeroDie nie zadzialal!)" : "nie (autotest: CanHeroDieEvent)"), true);
            Note(Tag() + ": spotkanie EncounterManager.StartPartyEncounter(gracz -> wrog) - to samo, co dojechanie do wrogiej druzyny na mapie", true);
            Go(Ph.Meet, t);
            EncounterManager.StartPartyEncounter(PartyBase.MainParty, bp.Party);
        }

        private static int SafeTier(CharacterObject c)
        {
            try { return c.Tier; } catch { return -1; }
        }

        /// <summary>Klan kryjowki najblizszej graczowi (jak ExtortionByDesertersIssueBehavior), inaczej looterzy, inaczej pierwszy klan bandytow z szablonem.</summary>
        private static Clan PickBandits(MobileParty mp, out Hideout ho)
        {
            ho = null;
            Clan clan = null;
            try { ho = SettlementHelper.FindNearestHideoutToMobileParty(mp, MobileParty.NavigationType.Default); } catch (Exception e) { Own("FindNearestHideout", e); }
            var all = Clan.BanditFactions.Where(x => x != null && !x.IsEliminated).ToList();
            if (ho != null && ho.Settlement != null)
            {
                var cul = ho.Settlement.Culture;
                clan = all.FirstOrDefault(x => x.Culture == cul && UsableTemplate(x.DefaultPartyTemplate)) ?? all.FirstOrDefault(x => x.Culture == cul);
            }
            if (clan == null)
            {
                ho = null;
                clan = all.FirstOrDefault(x => x.StringId == "looters") ?? all.FirstOrDefault(x => UsableTemplate(x.DefaultPartyTemplate)) ?? all.FirstOrDefault();
            }
            return clan;
        }

        private static bool UsableTemplate(PartyTemplateObject pt)
        {
            return pt != null && pt.Stacks != null && pt.Stacks.Any(s => s.Character != null && !s.Character.IsHero && s.MaxValue > 0);
        }

        /// <summary>Wrogowie wedlug szablonu klanu (proporcje srednich stosow), bez losowych petli FillPartyManuallyAfterCreation.</summary>
        private static string Fill(MobileParty bp, Clan clan, int want)
        {
            var pt = clan.DefaultPartyTemplate;
            var parts = new List<string>();
            if (UsableTemplate(pt))
            {
                var stacks = pt.Stacks.Where(s => s.Character != null && !s.Character.IsHero && s.MaxValue > 0).ToList();
                float wsum = stacks.Sum(s => (s.MinValue + s.MaxValue) / 2f + 0.01f);
                int left = want;
                for (int i = 0; i < stacks.Count && left > 0; i++)
                {
                    float w = (stacks[i].MinValue + stacks[i].MaxValue) / 2f + 0.01f;
                    int k = i == stacks.Count - 1 ? left : Math.Min(left, (int)Math.Round(want * w / wsum));
                    if (k <= 0) continue;
                    bp.MemberRoster.AddToCounts(stacks[i].Character, k);
                    parts.Add(k + " x " + stacks[i].Character.StringId);
                    left -= k;
                }
                return "szablon " + pt.StringId + ": " + string.Join(", ", parts.ToArray());
            }
            var cul = clan.Culture;
            var troops = cul != null ? new[] { cul.BanditBandit, cul.BanditRaider, cul.BanditChief } : new CharacterObject[0];
            var shares = new[] { 0.6, 0.3, 0.1 };
            int rest = want;
            for (int i = 0; i < troops.Length && rest > 0; i++)
            {
                if (troops[i] == null) continue;
                int k = i == troops.Length - 1 ? rest : Math.Min(rest, (int)Math.Round(want * shares[i]));
                if (troops.Skip(i + 1).All(x => x == null)) k = rest;
                if (k <= 0) continue;
                bp.MemberRoster.AddToCounts(troops[i], k);
                parts.Add(k + " x " + troops[i].StringId);
                rest -= k;
            }
            return "jednostki bandytow kultury " + (cul != null ? cul.StringId : "?") + ": " + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "brak");
        }

        private static CharacterObject OurTroop(Hero hero, MobileParty mp, out string how)
        {
            var cul = hero.Culture;
            if (cul != null && cul.BasicTroop != null) { how = "basic_troop kultury " + cul.StringId; return cul.BasicTroop; }
            if (cul != null && cul.EliteBasicTroop != null) { how = "elite_basic_troop kultury " + cul.StringId; return cul.EliteBasicTroop; }
            CharacterObject best = null;
            int most = 0;
            foreach (var e in mp.MemberRoster.GetTroopRoster())
                if (e.Character != null && !e.Character.IsHero && e.Number > most) { most = e.Number; best = e.Character; }
            how = best != null ? "najliczniejsza jednostka druzyny" : "kultura bez basic_troop";
            return best;
        }

        /// <summary>Mocna jednostka: elite_basic_troop albo basic_troop kultury po 3 awansach (UpgradeTargets) - wyzszy poziom wygrywa.</summary>
        private static CharacterObject StrongTroop(Hero hero, MobileParty mp, out string how)
        {
            var cul = hero.Culture;
            CharacterObject best = null;
            string bestHow = null;
            foreach (var start in new[] { cul != null ? cul.EliteBasicTroop : null, cul != null ? cul.BasicTroop : null })
            {
                if (start == null) continue;
                var tr = start;
                int steps = 0;
                while (steps < 3 && tr.UpgradeTargets != null)
                {
                    var next = tr.UpgradeTargets.FirstOrDefault(x => x != null && !x.IsHero);
                    if (next == null) break;
                    tr = next;
                    steps++;
                }
                if (best == null || SafeTier(tr) > SafeTier(best)) { best = tr; bestHow = (start == cul.EliteBasicTroop ? "elite_basic_troop" : "basic_troop") + " " + start.StringId + " po " + steps + " awansach"; }
            }
            if (best != null) { how = bestHow; return best; }
            return OurTroop(hero, mp, out how);
        }

        /// <summary>Po bitwie: dolozeni na czas bitwy wracaja (zdrowi i ranni), sklad druzyny jak przed bitwa - kolejne bitwy wedlug planu.</summary>
        private static void RemoveAdded()
        {
            var tr = _addedTroop;
            int added = _added;
            _addedTroop = null;
            _added = 0;
            if (tr == null || added <= 0) return;
            var mp = MobileParty.MainParty;
            if (mp == null || mp.MemberRoster == null) return;
            var r = mp.MemberRoster;
            int idx = r.FindIndexOfTroop(tr);
            if (idx < 0) { Note(Tag() + ": dolozonych " + tr.StringId + " nie ma juz w druzynie (polegli albo w niewoli)", false); return; }
            int num = r.GetElementNumber(idx), wnd = r.GetElementWoundedNumber(idx);
            int take = Math.Min(num, added), takeW = Math.Min(wnd, take);
            if (take <= 0) return;
            r.AddToCounts(tr, -take, false, -takeW);
            Note(Tag() + ": dolozeni na czas bitwy wracaja: " + take + " x " + tr.StringId + " (w tym rannych " + takeW + "), w druzynie " + mp.MemberRoster.TotalHealthyCount + " zdrowych", false);
        }

        private static Settlement NearestTown(MobileParty mp)
        {
            Settlement best = null;
            float bd = float.MaxValue;
            foreach (var s in Settlement.All)
            {
                if (s == null || !s.IsTown) continue;
                float d = s.Position.Distance(mp.Position);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Rozmowa ze spotkania (DoMeeting otwiera ja od razu) -> EndConversation -> menu odswieza sie -> "encounter".</summary>
        private static void StepMeet(Campaign c, double t)
        {
            var ctx = c.CurrentMenuContext;
            string id = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : null;
            if (t - _phAt > MeetHardS) { NotStarted(t, "spotkanie nie doszlo do menu encounter w " + (int)MeetHardS + " s (" + Autotest.Where() + (ctx != null ? ", opcje " + Autotest.ListOptions(ctx) : "") + ")"); return; }
            if (AutotestUi.AnyWindowOpen()) return;
            if (Talking(c))
            {
                if (t - _phAt > 2.0 && t - _actAt > 2.5)
                {
                    _actAt = t;
                    var who = Campaign.Current.ConversationManager.ConversationParty;
                    Note(Tag() + ": rozmowa przy spotkaniu" + (who != null ? " (" + who.Name + ")" : "") + " - koncze (EndConversation); gra odswieza menu spotkania", true);
                    c.ConversationManager.EndConversation();
                }
                return;
            }
            if (id == "encounter")
            {
                Note(Tag() + ": menu encounter, opcje " + Autotest.ListOptions(ctx), true);
                Go(Ph.Attack, t);
                return;
            }
            if (Mission.Current != null) return;   // misja przy spotkaniu - limit wyzej
            if (PlayerEncounter.Current == null && id == null && t - _phAt > 5)
            {
                NotStarted(t, "EncounterManager.StartPartyEncounter nie zalozyl spotkania (" + Autotest.Where() + ")");
                return;
            }
            if (id == "encounter_meeting" && !_refreshed && t - _phAt > 5)
            {
                _refreshed = true;
                Note(Tag() + ": menu encounter_meeting bez rozmowy - odswiezam menu (MenuContext.Refresh, jak po koncu rozmowy)", true);
                ctx.Refresh();
                return;
            }
            if (t - _phAt > MeetLimitS) NotStarted(t, "spotkanie nie doszlo do menu encounter w " + (int)MeetLimitS + " s (" + Autotest.Where() + (ctx != null ? ", opcje " + Autotest.ListOptions(ctx) : "") + ")");
        }

        private static void StepAttack(Campaign c, double t)
        {
            if (t - _phAt > AttackHardS) { NotStarted(t, "w " + (int)AttackHardS + " s nie dalo sie kliknac Attack (" + Autotest.Where() + ")"); return; }
            if (AutotestUi.AnyWindowOpen() || Talking(c) || t - _phAt < 1.0) return;
            var ctx = c.CurrentMenuContext;
            string id = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : null;
            if (id != "encounter")
            {
                if (t - _phAt > AttackLimitS) NotStarted(t, "menu encounter zniknelo przed atakiem (" + Autotest.Where() + ")");
                return;
            }
            Hero_Heal(Hero.MainHero, "ranny nie moze zaatakowac");
            _expectMission = true;
            Note(Tag() + ": klikam \"Attack!\" [attack] w menu encounter (to samo, co gracz: MenuHelper.EncounterAttackConsequence -> CampaignMission.OpenBattleMission)", true);
            Go(Ph.Load, t);
            if (!Autotest.ClickOption(ctx, "attack"))
            {
                _expectMission = false;
                NotStarted(t, "opcja attack w menu encounter niedostepna (opcje " + Autotest.ListOptions(ctx) + ")");
            }
        }

        // ------------------------------------------------------------------ 3. misja

        private static void StepLoad(double t)
        {
            if (t - _phAt > LoadLimitS) { NotStarted(t, "misja nie ruszyla w " + (int)LoadLimitS + " s (" + Autotest.Where() + ")"); StopAll("misja bitwy nie rusza"); return; }
            var m = Mission.Current;
            if (m == null)
            {
                if (Autotest.ActiveState() is MapState && t - _phAt > 25) NotStarted(t, "po Attack misja nie ruszyla w 25 s (" + Autotest.Where() + ")");
                return;
            }
            if (_mission == null) _mission = m;
            if (!ReferenceEquals(m, _mission)) { NotStarted(t, "na wierzchu obca misja zamiast bitwy (" + Autotest.Where() + ")"); StopAll("obca misja zamiast bitwy"); return; }
            if (m.CurrentState != Mission.State.Continuing)
            {
                if (t - _phAt > LoadStuckS) { NotStarted(t, "misja laduje sie dluzej niz " + (int)LoadStuckS + " s (stan " + m.CurrentState + ", " + Autotest.Where() + ")"); StopAll("misja bitwy laduje sie za dlugo"); }
                return;
            }
            if (_log == null || !m.MissionBehaviors.Contains(_log))
            {
                _log = new AutotestBattleLog();
                m.AddMissionBehavior(_log);
                Note(Tag() + ": liczniki dolaczone pozno (misja bez OnMissionBehaviorInitialize od CrashScribe) - ciosy liczone od teraz", true);
            }
            _expectMission = false;
            _log.Scene = SceneOf(m);
            Note(Tag() + ": misja ruszyla po " + (t - _phAt).ToString("0", Inv) + " s ladowania (scena " + _log.Scene + ", " + m.GetType().Name + ")" + (_stopWhy != null ? " - bitwy przerwane (" + _stopWhy + "): od razu odwrot" : ""), true);
            _fightAt = t;
            _lastProgress = t;
            Go(Ph.Fight, t);
        }

        private static string SceneOf(Mission m)
        {
            try { return m.SceneName ?? "?"; } catch { return "?"; }
        }

        private static void StepFight(double t)
        {
            var m = Mission.Current;
            if (m == null || !ReferenceEquals(m, _mission)) { EndOfMission(t, "misja zakonczona poza autotestem"); return; }
            Track(m);
            if (m.MissionEnded)
            {
                Note(Tag() + ": bitwa rozstrzygnieta sama (" + ResultText(m.MissionResult) + ") po " + (t - _fightAt).ToString("0", Inv) + " s, czas misji " + m.CurrentTime.ToString("0", Inv) + " s", true);
                Go(Ph.Ending, t);
                return;
            }
            if (_stopWhy != null || t - _fightAt > BattleSec)
            {
                Retreat(m, t, _stopWhy != null ? "przerwanie (" + _stopWhy + ")" : "limit " + BattleSec + " s minal");
                return;
            }
            if (_kind == Kind.Retreat && _startLogged && m.IsDeploymentFinished)
            {
                int hits = _log != null ? _log.Melee : 0;
                if (hits >= RetreatHits || t - _fightAt > RetreatSec)
                {
                    Retreat(m, t, "plan bitwy: odwrot w trakcie walki (ciosy wrecz " + hits + ", " + (t - _fightAt).ToString("0", Inv) + " s)");
                    return;
                }
            }
            Drive(m, t);
            Progress(m, t);
        }

        /// <summary>Rozstawienie, szarza, bohater pod SI, przewijanie - z tiku aplikacji, jak przyciski interfejsu (poza petla Mission.Tick).</summary>
        private static void Drive(Mission m, double t)
        {
            if (m.CurrentState != Mission.State.Continuing) return;
            if (!m.IsDeploymentFinished)
            {
                var dmc = m.GetMissionBehavior<DeploymentMissionController>();
                var dh = m.GetMissionBehavior<DeploymentHandler>();
                if (dmc == null && dh == null)
                {
                    if (t - _fightAt < 3) return;
                    if (!_noDeployNoted) { _noDeployNoted = true; Note(Tag() + ": misja bez fazy rozstawienia (brak DeploymentMissionController) - dalej", false); }
                }
                else
                {
                    if (!_startLogged && t - _fightAt > 45) LogStart(m, t, "rozstawienie NIE skonczone po 45 s");
                    if (dmc != null && !dmc.TeamSetupOver) { _deploySeenAt = -1; return; }
                    if (_deploySeenAt < 0) { _deploySeenAt = t; return; }
                    if (t - _deploySeenAt < 2.0) return;
                    if (_deployTries == 0 || (t - _deployCalledAt > 10 && _deployTries < 3))
                    {
                        _deployTries++;
                        _deployCalledAt = t;
                        Note(Tag() + ": rozstawienie gotowe - koncze je jak przycisk \"Rozpocznij bitwe\": " + (dh != null ? "DeploymentHandler(" + dh.GetType().Name + ").FinishDeployment()" : "DeploymentMissionController.FinishDeployment()") + (_deployTries > 1 ? " (proba " + _deployTries + ")" : ""), true);
                        Sub("rozstawienie", () => { if (dh != null) dh.FinishDeployment(); else dmc.FinishDeployment(); });
                    }
                    return;
                }
            }
            if (!_startLogged) LogStart(m, t, m.IsDeploymentFinished ? "rozstawienie zakonczone" : "bez rozstawienia");

            // kazdy krok osobno: wywrotka jednego (np. rozkazu) nie blokuje pozostalych
            // szarza wszystkich szykow gracza (co 15 s - posilki, nowe szyki)
            if (_lastCharge < 0 || t - _lastCharge > 15)
            {
                _lastCharge = t;
                Sub("szarza", () =>
                {
                    var team = m.PlayerTeam;
                    var oc = team != null ? team.PlayerOrderController : null;
                    if (oc == null) return;
                    oc.SelectAllFormations();
                    int sel = oc.SelectedFormations != null ? oc.SelectedFormations.Count : -1;
                    oc.SetOrder(OrderType.Charge);
                    _chargeCount++;
                    if (_chargeCount == 1) Note(Tag() + ": rozkaz SZARZA dla wszystkich szykow gracza (PlayerOrderController.SelectAllFormations + SetOrder(Charge)), szykow " + sel + ", gracz generalem: " + (team.IsPlayerGeneral ? "tak" : "nie"), true);
                });
            }

            if (_kind == Kind.Retreat)
            {
                // odwrot jak gracz: bohater zostaje z tylu (gracz bez ruchu), niesmiertelny tylko w tej bitwie - Tab nie trafia na "wrog blisko"
                if (!_immortal)
                {
                    Sub("bohater niesmiertelny", () =>
                    {
                        var ma = m.MainAgent;
                        if (ma == null || !ma.IsActive()) return;
                        ma.SetMortalityState(Agent.MortalityState.Immortal);
                        _immortal = true;
                        Note(Tag() + ": bohater \"" + ma.Name + "\" zostaje na miejscu (sterowanie gracza, bez ruchu) i jest niesmiertelny w tej bitwie (Agent.SetMortalityState(Immortal): ciosy i OnAgentHit dalej, obrazenia 0) - odwrot jak gracz przez Tab", true);
                    });
                }
            }
            else
            {
                // bohater pod SI (FinishDeployment oddaje go graczowi - stad sprawdzanie co tik)
                Sub("bohater SI", () =>
                {
                    var ma = m.MainAgent;
                    if (ma == null || !ma.IsActive() || ma.Controller != AgentControllerType.Player) return;
                    ma.Controller = AgentControllerType.AI;
                    _aiCount++;
                    if (_aiCount <= 3) Note(Tag() + ": bohater \"" + ma.Name + "\" pod kontrola SI (Agent.Controller = AI, jak Mission.JoinEnemyTeam)" + (_aiCount > 1 ? " - ponownie (" + _aiCount + ")" : ""), true);
                });
            }

            // przewijanie misji
            if (!m.IsFastForward && (_lastFf < 0 || t - _lastFf > 3))
            {
                _lastFf = t;
                Sub("przewijanie", () =>
                {
                    m.SetFastForwardingFromUI(true);
                    _ffCount++;
                    if (_ffCount <= 2) Note(Tag() + ": przewijanie misji wlaczone (Mission.SetFastForwardingFromUI(true))" + (_ffCount > 1 ? " - ponownie" : ""), false);
                });
            }
        }

        /// <summary>Krok w misji w ochronie Guard; po 20 bledach w tym samym kroku - krok pominiety (bitwa idzie dalej bez niego).</summary>
        private static void Sub(string where, Action a)
        {
            int n, k;
            if ((Errs.TryGetValue(where, out n) && n >= 20) || (Errs.TryGetValue("mod:" + where, out k) && k >= 20)) return;
            Guard(where, a);
        }

        private static void CountAgents(Mission m, out int ours, out int theirs, out int mounts)
        {
            ours = theirs = mounts = 0;
            var pt = m.PlayerTeam;
            foreach (Agent a in m.Agents)
            {
                if (a == null || !a.IsActive()) continue;
                if (a.IsHuman)
                {
                    if (a.Team == null) continue;
                    if (a.Team.IsPlayerAlly) ours++;
                    else if (pt != null && a.Team.IsEnemyOf(pt)) theirs++;
                }
                else if (a.IsMount) mounts++;
            }
        }

        private static void LogStart(Mission m, double t, string deploy)
        {
            _startLogged = true;
            int ours, theirs, mounts;
            CountAgents(m, out ours, out theirs, out mounts);
            var team = m.PlayerTeam;
            Note("AUTOTEST BITWA START " + _n + "/" + Battles + ": agenci gracz " + ours + ", wrog " + theirs + " (ludzi " + (ours + theirs) + ", koni " + mounts + ")"
                 + " | druzyny na mapie: gracz " + _ourMen + ", wrog " + _enemyMen + " | scena " + (_log != null ? _log.Scene : SceneOf(m))
                 + " | " + deploy + ", gracz generalem: " + (team != null && team.IsPlayerGeneral ? "tak" : "nie") + " | plan " + KindName(_kind)
                 + " | limit " + (_kind == Kind.Retreat ? (int)RetreatSec + " s albo " + RetreatHits + " ciosow wrecz (odwrot)" : BattleSec + " s"), true);
        }

        private static string HeroState(Mission m)
        {
            var ma = m.MainAgent;
            if (ma == null || !ma.IsActive()) return "poza walka";
            return ma.Controller == AgentControllerType.AI ? "SI" : ma.Controller.ToString();
        }

        private static void Track(Mission m)
        {
            if (_log == null) return;
            _log.LastTime = m.CurrentTime;
            if (m.MissionResult != null) _log.Result = m.MissionResult;
        }

        private static void Progress(Mission m, double t)
        {
            if (t - _lastProgress < ProgressEveryS) return;
            _lastProgress = t;
            int ours, theirs, mounts;
            CountAgents(m, out ours, out theirs, out mounts);
            var L = _log ?? new AutotestBattleLog();
            Note(Tag() + " postep: czas misji " + m.CurrentTime.ToString("0", Inv) + " s (rzeczywisty " + (t - _fightAt).ToString("0", Inv) + " s), zywi gracz " + ours + " / wrog " + theirs
                 + ", ciosy wrecz " + L.Melee + ", strzaly " + L.Missile + ", polegli/ranni " + (L.Killed + L.Unconscious) + ", paniki " + L.Scared.Count
                 + " | rozstawienie " + (m.IsDeploymentFinished ? "koniec" : "TRWA") + ", przewijanie " + (m.IsFastForward ? "tak" : "nie") + ", bohater " + HeroState(m), false);
        }

        private static string ResultText(MissionResult r)
        {
            if (r == null) return "bez wyniku";
            return r.PlayerVictory ? "wygrana" + (r.EnemyRetreated ? " (wrog uciekl)" : "") : r.PlayerDefeated ? "przegrana" : "wynik " + r.BattleState;
        }

        /// <summary>
        /// Odwrot jak gracz: Tab (BattleEndLogic.TryExit) -> okno "Odwrot?" -> OK (BasicMissionHandler.OnEventAcceptSelectionWidget =
        /// OnBattleEnded wszystkich logik + Mission.RetreatMission). Gdy Tab odmawia (wrog blisko bohatera) - odwrot mimo to, z linia.
        /// </summary>
        private static void Retreat(Mission m, double t, string why)
        {
            _timeoutHit = true;
            Go(Ph.Ending, t);
            var bel = m.GetMissionBehavior<BattleEndLogic>();
            var tab = bel != null ? bel.TryExit() : BattleEndLogic.ExitResult.NeedsPlayerConfirmation;
            if (tab == BattleEndLogic.ExitResult.True) { Note(Tag() + ": " + why + " - Tab (BattleEndLogic.TryExit) zamknal misje (bitwa juz rozstrzygnieta)", true); return; }
            string how = tab == BattleEndLogic.ExitResult.NeedsPlayerConfirmation ? "Tab -> okno \"Odwrot?\" -> OK"
                : "Tab = " + tab + " (gracz nie moglby teraz: wrog blisko bohatera albo bitwa rozstrzygnieta) - odwrot mimo to (limit autotestu)";
            var bmh = m.GetMissionBehavior<BasicMissionHandler>();
            var mi = typeof(BasicMissionHandler).GetMethod("OnEventAcceptSelectionWidget", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (bmh != null && mi != null)
            {
                Note(Tag() + ": " + why + " - odwrot jak gracz: " + how + " (BasicMissionHandler.OnEventAcceptSelectionWidget: OnBattleEnded + Mission.RetreatMission)", true);
                mi.Invoke(bmh, null);
            }
            else
            {
                Note(Tag() + ": " + why + " - odwrot: " + how + ", OnBattleEnded wszystkich logik + Mission.RetreatMission (brak BasicMissionHandler)", true);
                foreach (var l in m.MissionLogics.ToArray()) l.OnBattleEnded();
                m.RetreatMission();
            }
        }

        private static void StepEnding(double t)
        {
            var m = Mission.Current;
            if (m != null && ReferenceEquals(m, _mission))
            {
                Track(m);
                if (t - _phAt > EndingLimitS)
                {
                    Err("misja nie zamknela sie w " + (int)EndingLimitS + " s po koncu bitwy (" + Autotest.Where() + ")");
                    StopAll("misja sie nie zamyka");
                    EndOfMission(t, "misja nie zamknela sie");
                    return;
                }
                if (!_timeoutHit)
                {
                    // koniec sam: wyjscie jak gracz ("Gotowe" na tablicy wynikow / Tab -> BattleEndLogic.TryExit)
                    if (!_exitTried && t - _phAt > 3)
                    {
                        _exitTried = true;
                        var bel = m.GetMissionBehavior<BattleEndLogic>();
                        if (bel != null)
                        {
                            var r = bel.TryExit();
                            Note(Tag() + ": wyjscie z bitwy jak gracz: BattleEndLogic.TryExit() = " + r, false);
                        }
                        else { Note(Tag() + ": brak BattleEndLogic - Mission.EndMission()", false); m.EndMission(); }
                    }
                    else if (_exitTried && !_endForced && t - _phAt > 15) { _endForced = true; Note(Tag() + ": TryExit nie zamknal misji w 12 s - Mission.EndMission()", true); m.EndMission(); }
                }
                else if (!_endForced && t - _phAt > 20) { _endForced = true; Note(Tag() + ": odwrot nie zamknal misji w 20 s - Mission.EndMission()", true); m.EndMission(); }
                return;
            }
            EndOfMission(t, null);
        }

        private static void EndOfMission(double t, string note)
        {
            if (!_koniecLogged)
            {
                _koniecLogged = true;
                _played++;
                var L = _log;
                var r = L != null ? L.Result : null;
                string wynik = r != null && r.PlayerVictory ? "won" : r != null && r.PlayerDefeated ? "lost" : _timeoutHit && _kind == Kind.Retreat && _stopWhy == null ? "retreat" : "timeout";
                if (wynik == "won") _won++; else if (wynik == "lost") _lost++; else _timeouts++;
                if (!_timeoutHit && (wynik == "timeout" || wynik == "retreat")) note = (note != null ? note + "; " : "") + "misja zakonczona bez wyniku i nie przez limit autotestu";
                if (L == null) { L = new AutotestBattleLog(); note = (note != null ? note + "; " : "") + "liczniki NIE byly podlaczone"; }
                Note("AUTOTEST BITWA KONIEC " + _n + "/" + Battles + ": ciosy wrecz " + L.Melee + ", strzaly " + L.Missile + ", polegli/ranni " + (L.Killed + L.Unconscious)
                     + ", paniki " + L.Scared.Count + ", czas " + L.LastTime.ToString("0", Inv) + " s, wynik " + wynik + " (plan " + KindName(_kind) + ")"
                     + " | zabici " + L.Killed + ", nieprzytomni " + L.Unconscious + ", uciekli z pola " + L.Routed + ", panika " + L.Panicked + ", ucieczka " + L.Fleeing
                     + ", konie padle " + L.Mounts + ", kolizje wrecz " + L.MeleeColl + " / pociski " + L.MissileColl
                     + ", czas rzeczywisty " + (_fightAt > 0 ? (t - _fightAt).ToString("0", Inv) : "?") + " s, bledy bitwy " + _errors
                     + (note != null ? " | " + note : ""), true);
            }
            _mission = null;
            Go(Ph.After, t);
        }

        // ------------------------------------------------------------------ 4. po bitwie

        private static void StepAfter(Campaign c, double t)
        {
            if (t - _phAt > AfterLimitS)
            {
                Err("po bitwie mapa nie wrocila do spokoju w " + (int)AfterLimitS + " s (" + Autotest.Where() + ")");
                StopAll("po bitwie bez spokojnej mapy");
                Go(Ph.Cleanup, t);
                return;
            }
            if (AutotestUi.AnyWindowOpen()) { _idleSince = -1; return; }
            var st = Autotest.ActiveState();
            if (!ReferenceEquals(st, _stObj)) { _stObj = st; _stSince = t; _stTries = 0; }
            if (st is MissionState || Mission.Current != null)
            {
                _idleSince = -1;
                if (Talking(c) && t - _actAt > 3) { _actAt = t; Note(Tag() + ": po bitwie rozmowa w misji - koncze", false); c.ConversationManager.EndConversation(); }
                return;
            }
            if (Talking(c))
            {
                _idleSince = -1;
                if (t - _actAt > 2.5)
                {
                    _actAt = t;
                    var who = c.ConversationManager.ConversationParty;
                    Note(Tag() + ": po bitwie rozmowa" + (who != null ? " (" + who.Name + ")" : "") + " - koncze (EndConversation)", true);
                    c.ConversationManager.EndConversation();
                }
                return;
            }
            if (st is PartyState) { _idleSince = -1; LootParty((PartyState)st, t); return; }
            if (st is InventoryState) { _idleSince = -1; LootItems(t); return; }
            if (!(st is MapState))
            {
                _idleSince = -1;
                if (t - _stSince > 30 && _stTries < 1)
                {
                    _stTries++;
                    var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                    if (gsm != null && gsm.LastOrDefault<MapState>() != null)
                    {
                        Note(Tag() + ": po bitwie od 30 s stan " + (st != null ? st.GetType().Name : "-") + " - zdejmuje (PopState, jak Esc)", true);
                        gsm.PopState(0);
                    }
                }
                return;
            }
            StopTime(c);
            var mp = MobileParty.MainParty;
            var hero = Hero.MainHero;
            var ctx = c.CurrentMenuContext;
            var menu = ctx != null ? ctx.GameMenu : null;
            string id = menu != null ? menu.StringId : null;

            if (hero != null && hero.IsDead)
            {
                Err("bohater polegl w bitwie mimo CanHeroDieEvent (" + Autotest.Where() + ") - dalsze bitwy przerwane");
                StopAll("bohater polegl");
                Go(Ph.Cleanup, t);
                return;
            }
            if (hero != null && hero.IsPrisoner)
            {
                _idleSince = -1;
                if (_prisonSince < 0) { _prisonSince = t; Note(Tag() + ": po bitwie bohater w niewoli (" + Autotest.Where() + ")", true); }
                if ((id == "taken_prisoner" || id == "defeated_and_taken_prisoner") && t - _stSince > 3.0 && t - _actAt > 2)
                {
                    _actAt = t;
                    if (Autotest.ClickOption(ctx, "taken_prisoner_continue")) Note(Tag() + ": menu " + id + " - klikam \"Continue\" [taken_prisoner_continue]", true);
                    return;
                }
                if (!_escaped && t - _prisonSince > 6)
                {
                    _escaped = true;
                    Note(Tag() + ": bohater w niewoli po przegranej - ucieczka EndCaptivityAction.ApplyByEscape (skrot testu, zeby bieg dob szedl dalej)", true);
                    EndCaptivityAction.ApplyByEscape(hero);
                }
                return;
            }
            bool inTown = mp != null && mp.CurrentSettlement != null && id != null && id.StartsWith("town", StringComparison.Ordinal) && mp.MapEvent == null;
            if (id != null && !inTown)
            {
                _idleSince = -1;
                if (t - _stSince < 3.0 || t - _actAt < 2) return;   // mapa po misji: menu spotkania najpierw przelicza wynik (PlayerEncounter.Update)
                _actAt = t;
                if (id == "encounter")
                {
                    if (Autotest.ClickOption(ctx, "capture_the_enemy")) Note(Tag() + ": menu encounter - klikam \"Capture the enemy\" [capture_the_enemy]", true);
                    else if (Autotest.ClickOption(ctx, "leave")) Note(Tag() + ": menu encounter - klikam \"Leave\" [leave]", true);
                    else if (Autotest.ClickOption(ctx, "leave_soldiers_behind")) Note(Tag() + ": menu encounter - klikam \"Try to get away\" [leave_soldiers_behind]", true);
                    else if (!Autotest.ClickLeave(ctx) && MenusNoted.Add(id)) Note(Tag() + ": menu encounter bez wyjscia - opcje " + Autotest.ListOptions(ctx), true);
                    return;
                }
                if ((id == "taken_prisoner" || id == "defeated_and_taken_prisoner") && Autotest.ClickOption(ctx, "taken_prisoner_continue"))
                {
                    Note(Tag() + ": menu " + id + " - klikam \"Continue\" [taken_prisoner_continue]", true);
                    return;
                }
                if (!Autotest.ClickLeave(ctx) && MenusNoted.Add(id)) Note(Tag() + ": po bitwie menu " + id + " bez wyjscia - opcje " + Autotest.ListOptions(ctx), true);
                return;
            }
            if (!inTown && PlayerEncounter.Current != null)
            {
                _idleSince = -1;
                if (_encSince < 0) _encSince = t;
                if (!_encFinished && t - _encSince > 15)
                {
                    _encFinished = true;
                    Note(Tag() + ": po bitwie spotkanie bez menu od 15 s - PlayerEncounter.Finish()", true);
                    PlayerEncounter.Finish(true);
                }
                return;
            }
            if (mp == null || mp.MapEvent != null || (!mp.IsActive && !inTown)) { _idleSince = -1; return; }
            if (_idleSince < 0) { _idleSince = t; return; }
            if (t - _idleSince < 2.0) return;
            Note(Tag() + ": po bitwie mapa spokojna po " + (t - _phAt).ToString("0", Inv) + " s (" + Autotest.Where() + ") | druzyna gracza " + mp.MemberRoster.TotalHealthyCount
                 + " zdrowych, " + mp.MemberRoster.TotalWounded + " rannych, jencow " + mp.PrisonRoster.TotalManCount, false);
            Go(Ph.Cleanup, t);
        }

        /// <summary>Ekran druzyny po bitwie (lup: jency, uwolnieni) - "Gotowe" jak gracz; gdy nie schodzi - wymuszone, potem anuluj, potem zdjecie stanu.</summary>
        private static void LootParty(PartyState ps, double t)
        {
            if (t - _stSince < 2.0 || t - _actAt < 3.0) return;
            _actAt = t;
            _stTries++;
            string mode = "?";
            try { mode = ps.PartyScreenMode.ToString(); } catch { }
            switch (_stTries)
            {
                case 1: Note(Tag() + ": ekran druzyny po bitwie (" + mode + ") -> Gotowe (PartyScreenHelper.CloseScreen, jak przycisk Done)", true); PartyScreenHelper.CloseScreen(false, false); break;
                case 2: Note(Tag() + ": ekran druzyny nie zszedl - CloseScreen(isForced: true)", true); PartyScreenHelper.CloseScreen(true, false); break;
                case 3: Note(Tag() + ": ekran druzyny nie zszedl - CloseScreen(fromCancel: true)", true); PartyScreenHelper.CloseScreen(false, true); break;
                case 4: Note(Tag() + ": ekran druzyny nie zszedl - PopState", true); Game.Current.GameStateManager.PopState(0); break;
            }
        }

        private static void LootItems(double t)
        {
            if (t - _stSince < 2.0 || t - _actAt < 3.0) return;
            _actAt = t;
            _stTries++;
            switch (_stTries)
            {
                case 1: Note(Tag() + ": ekran lupu (przedmioty) -> Gotowe (InventoryScreenHelper.CloseScreen, jak przycisk Done)", true); InventoryScreenHelper.CloseScreen(false); break;
                case 2: Note(Tag() + ": ekran lupu nie zszedl - CloseScreen(fromCancel: true)", true); InventoryScreenHelper.CloseScreen(true); break;
                case 3: Note(Tag() + ": ekran lupu nie zszedl - PopState", true); Game.Current.GameStateManager.PopState(0); break;
            }
        }

        // ------------------------------------------------------------------ 5. sprzatanie i nastepna bitwa

        private static void StepCleanup(double t)
        {
            var e = _enemy;
            _enemy = null;
            if (e != null)
            {
                try
                {
                    if (e.IsActive && e.MapEvent == null)
                    {
                        int left = e.MemberRoster.TotalManCount;
                        DestroyPartyAction.Apply(null, e);
                        Note(Tag() + ": druzyna wroga usunieta z mapy (zostalo w niej " + left + " ludzi)", false);
                    }
                    else if (!e.IsActive) Note(Tag() + ": druzyna wroga juz nieaktywna (rozbita albo usunieta przez gre)", false);
                    else
                    {
                        if (!Leftover.Contains(e)) Leftover.Add(e);
                        Note(Tag() + ": druzyna wroga " + e.StringId + " ZOSTAJE na razie (nadal w MapEvent) - ponowna proba na koniec bitew i przy starcie biegu dob", true);
                    }
                }
                catch (Exception ex) { Own("DestroyParty", ex); }
            }
            try { RemoveAdded(); } catch (Exception ex) { Own("RemoveAdded", ex); }
            if (_stopWhy == null && _n < Battles)
            {
                _n++;
                _kind = KindOf(_n);
                ResetBattle();
                Note("AUTOTEST BITWA: nastepna bitwa " + _n + "/" + Battles + " (plan " + KindName(_kind) + ")", false);
                Go(Ph.Spawn, t);
                return;
            }
            Over(t);
        }

        private static void Over(double t)
        {
            _over = true;
            _ph = Ph.Over;
            try { Sweep("koniec bitew"); } catch (Exception e) { Own("Sweep", e); }
            Note("AUTOTEST BITWY koniec: rozegrane " + _played + "/" + Battles + " (wygrane " + _won + ", przegrane " + _lost + ", odwrot/limit " + _timeouts + ", nie ruszyly " + _notStarted
                 + "), bledy bitwy " + _errors + (_gameErrors > 0 ? " (gry " + _gameErrors + ")" : "") + (_modErrors > 0 ? ", wyjatki modow " + _modErrors : "")
                 + (ArenaOn && Battles > 0 ? " | arena: " + (_arenaDone > 0 ? "KONIEC" : _arenaNot > 0 ? "NIE RUSZYLA" : "nie doszla") : "") + TourSummary()
                 + (_stopWhy != null ? " | przerwane: " + _stopWhy : "") + " | " + (t - _beginAt).ToString("0", Inv) + " s | dalej zwykly bieg dob", true);
            Autotest.BattlesOver(t, "bitwy za nami");
        }
    }

    /// <summary>[AT5] Liczniki bitwy autotestu (tylko liczy, nic nie zmienia w misji).</summary>
    internal sealed class AutotestBattleLog : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal int Melee, Missile, MeleeColl, MissileColl, Killed, Unconscious, Routed, Mounts, Panicked, Fleeing;
        internal readonly HashSet<int> Scared = new HashSet<int>();
        internal float LastTime;
        internal MissionResult Result;
        internal string Scene = "?";

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                if (affectedAgent == null) return;
                if (blow.IsMissile) Missile++; else Melee++;
            }
            catch { }
        }

        public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            if (!isCanceled) MeleeColl++;
        }

        public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            if (!isCanceled) MissileColl++;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                if (affectedAgent == null) return;
                if (affectedAgent.IsHuman)
                {
                    if (agentState == AgentState.Killed) Killed++;
                    else if (agentState == AgentState.Unconscious) Unconscious++;
                    else if (agentState == AgentState.Routed) { Routed++; Scared.Add(affectedAgent.Index); }
                }
                else if (affectedAgent.IsMount && agentState == AgentState.Killed) Mounts++;
            }
            catch { }
        }

        public override void OnAgentPanicked(Agent affectedAgent)
        {
            Panicked++;
            try { if (affectedAgent != null) Scared.Add(affectedAgent.Index); } catch { }
        }

        public override void OnAgentFleeing(Agent affectedAgent)
        {
            Fleeing++;
            try { if (affectedAgent != null) Scared.Add(affectedAgent.Index); } catch { }
        }

        public override void OnEndMissionInternal()
        {
            try
            {
                var m = Mission;
                if (m != null)
                {
                    LastTime = m.CurrentTime;
                    if (m.MissionResult != null) Result = m.MissionResult;
                }
            }
            catch { }
            base.OnEndMissionInternal();
        }
    }
}
