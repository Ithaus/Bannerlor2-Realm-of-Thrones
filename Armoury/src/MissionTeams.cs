using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NM (noc 15/16.10, bledy misji z sesji Jeffa 15.10): DRUZYNY W MISJI. Jedno pytanie "czy wrog" dla wszystkich naszych petli misji.
    ///
    /// Dowod [K]: Team.Invalid = new Team(MBTeam.InvalidTeam, None, mission: null) (Team.cs:169-173), MBTeam.InvalidTeam = new MBTeam(null, -1)
    /// (MBTeam.cs:9); Team.IsEnemyOf -> MBTeam.IsEnemyOf -> MBAPI.IMBTeam.IsEnemy(_mission.Pointer, ...) (Team.cs:629-632, MBTeam.cs:39-42) -
    /// pytanie o czlowieka "bez druzyny" konczy sie NullReferenceException. Pojedynek z hersztem kryjowki przenosi widzow (zbojcow i ludzi gracza
    /// pod AI) do Team.Invalid (HideoutMissionController.cs:931/938); Retreat agenta z Team.Invalid wywraca Mission.GetClosestFleePositionForAgent
    /// (Team.Mission == null, Team.cs:228). Log 10-56-30: 9 x ERROR in HideoutAlarm.AlarmAround i 14 x ERROR in BrokenMen.OnMissionTick 12:10:00-12:10:19.
    /// Turnieje [L]: FleeTheField 8 (11:10-11:16), 167 (07-16-10), GuardMaster.Tick 35 (08-34-15) - zrodlo agenta bez druzyny w turnieju nieustalone [P].
    /// Team.Invalid to tez ZWYKLY stan ludzi gry [K]: kazdy neutralny cywil (MissionAgentHandler.SpawnWanderingAgentWithInitialFrame,
    /// MissionAgentHandler.cs:849-853 - misje miasta, zamku, wsi, wnetrz, areny, bojki w zaulku: SandBoxMissions.cs), ludzie po strefie skradania
    /// (StealthZone.cs:159), sprawca w FamilyFeudIssueBehavior.cs:846. Turniej gry nie ma MissionAgentHandler (TournamentMissionStarter.cs:46-61),
    /// Tournaments XPanded druzyn nie rusza (dec/txp: SetTeam / Team.Invalid 0) - stad "pierwszy" w linii turnieju.
    ///
    /// Zasady:
    ///  - Valid / Enemies: druzyna wazna (MBTeam.Index >= 0, bez wywolania natywnego) i obie wazne, zanim padnie natywne IsEnemy;
    ///  - Skip: czlowiek bez waznej druzyny jest pomijany w petli (nie budzimy, nie panikujemy, nie wysylamy do ucieczki) - liczony;
    ///  - Stumble: wyjatek per miejsce liczony (misja i sesja), do logu pelny slad tylko 3 pierwszych na miejsce w sesji (CLAUDE.md 7: licz, nie gas);
    ///  - IsBattle: bitwa na mapie (Campaign.Current == null albo MapEvent.PlayerMapEvent != null) - poza nia ranni nie uciekaja
    ///    (klucz WoundedFleeBattlesOnly; gra w turnieju sama zabrania rozbicia: TournamentFightMissionController.CanAgentRout = false);
    ///  - Flush: jedna linia "Druzyny w misji (NM)" po misji (kryjowka, turniej, arena, bitwa albo misja "inna" z wstrzymanymi, odwrotami
    ///    albo potknieciami - sami pominieci jej nie daja, bo cywile miasta zawsze sa w Team.Invalid; spacer po miescie bez linii).
    /// Stan kluczowany referencja misji (nowa misja = liczniki od zera; niewypisana poprzednia wypisuje sie przy zmianie). Bez zapisu kampanii.
    /// Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings" (CrashScribe Net.cs:155 - te omija siec finalizatorow).
    /// Na liste "optymalizacja na koniec" (recenzja NM): Valid / Enemies / Skip wolane na kazdego agenta (GuardMaster co 0.05 s przy bloku,
    /// BrokenMen co 0.5 s) ida przez siec finalizatorow CrashScribe - do rozwazenia wyjatek t.Name == "MissionTeams" w Net.cs:155
    /// (kazda metoda ma wlasny try/catch albo jest jednolinijkowa bez wyjatkow).
    /// </summary>
    internal static class MissionTeams
    {
        // miejsca (indeksy licznikow)
        internal const int Alarm = 0, Broken = 1, Flee = 2, Parry = 3, Kill = 4;
        private const int Sites = 5;
        private static readonly string[] SiteName = { "alarm", "rozbici", "ucieczka", "blok", "zabicia" };
        private const int ErrorsPerSite = 3;      // pelny slad do logu: pierwsze 3 na miejsce w sesji, reszta tylko w liczniku

        private static WeakReference _mission;    // misja, ktorej dotycza liczniki (slaba referencja - skonczona misja nie zostaje w pamieci)
        private static bool _any;                 // byla jakas misja od startu procesu
        private static bool _flushed;             // linia tej misji juz wypisana
        private static string _kind = "inna";     // kryjowka / turniej / arena / bitwa / inna
        private static int _battle = -1;          // -1 nie sprawdzone, 0 misja poza bitwa na mapie, 1 bitwa (stale przez cala misje)
        private static readonly HashSet<int>[] _skipped = NewSets();   // rozni ludzie bez druzyny pominieci w miejscu (Agent.Index)
        private static readonly HashSet<int> _skippedAll = new HashSet<int>();
        private static string _first;             // pierwszy pominiety: StringId i zdrowie
        private static readonly HashSet<int>[] _held = NewSets();      // rozni ranni wstrzymani poza bitwa (Broken, Flee)
        private static readonly HashSet<int>[] _pushed = NewSets();    // rozni ludzie, ktorym wyslano odwrot w tej misji (Broken: Panic/Retreat, Flee: Retreat)
        private static readonly int[] _pushCalls = new int[Sites];     // wywolania odwrotu (FieldCraft ponawia co 0.2 s, gdy odwrot wygasl)
        private static readonly int[] _stum = new int[Sites];          // potkniecia tej misji
        private static readonly int[] _stumSession = new int[Sites];   // potkniecia sesji (limit sladow w logu)
        private static int _lines;                // linii misji w sesji

        private static HashSet<int>[] NewSets()
        {
            var a = new HashSet<int>[Sites];
            for (int i = 0; i < Sites; i++) a[i] = new HashSet<int>();
            return a;
        }

        // ------------------------------------------------------------ pytanie "czy wrog"

        /// <summary>Druzyna wazna: nie null i MBTeam.Index >= 0 (Team.IsValid, Team.cs:181 - pole, bez wywolania natywnego).</summary>
        internal static bool Valid(Team t)
        {
            return t != null && t.IsValid;
        }

        /// <summary>Obie druzyny wazne i wrogie. Zamiast a.IsEnemyOf(b) w kazdej petli misji (Team.Invalid wywraca natywne IsEnemy).</summary>
        internal static bool Enemies(Team a, Team b)
        {
            return Valid(a) && Valid(b) && a.IsEnemyOf(b);
        }

        /// <summary>Czlowiek bez waznej druzyny (widz pojedynku z hersztem, Team.Invalid): true = pominac w petli; liczony dla miejsca.</summary>
        internal static bool Skip(Agent a, int site)
        {
            if (a == null) return true;
            if (Valid(a.Team)) return false;
            try
            {
                Ensure(a.Mission ?? Mission.Current);
                int id = a.Index;
                if (site >= 0 && site < Sites) _skipped[site].Add(id);
                if (_skippedAll.Add(id) && _first == null)
                {
                    string sid = "?";
                    try { if (a.Character != null) sid = a.Character.StringId; } catch { }
                    int hp = -1;
                    try { if (a.HealthLimit > 0f) hp = (int)Math.Round(100f * a.Health / a.HealthLimit); } catch { }
                    _first = sid + " " + (hp >= 0 ? hp + "%" : "?") + (a.Team == null ? " (druzyna null)" : "");
                }
            }
            catch { }
            return true;
        }

        // ------------------------------------------------------------ potkniecia i liczniki

        /// <summary>Wyjatek w miejscu: licznik misji i sesji; pelny slad tylko dla pierwszych 3 na miejsce w sesji. Funkcja zostaje wlaczona.</summary>
        internal static void Stumble(int site, string where, Exception e)
        {
            try
            {
                Ensure(Mission.Current);
                if (site < 0 || site >= Sites) site = Alarm;
                _stum[site]++;
                _stumSession[site]++;
                if (_stumSession[site] <= ErrorsPerSite) Log.Error(where, e);
                if (_stumSession[site] == ErrorsPerSite)
                    Log.Info("Druzyny w misji (NM): " + where + " - " + ErrorsPerSite + ". potkniecie miejsca '" + SiteName[site]
                             + "' w tej sesji; kolejne tylko w liczniku linii misji.");
            }
            catch { }
        }

        /// <summary>Ranny wstrzymany poza bitwa na mapie (klucz WoundedFleeBattlesOnly): rozni ludzie na miejsce.</summary>
        internal static void Held(int site, Agent a)
        {
            try
            {
                if (a == null || site < 0 || site >= Sites) return;
                Ensure(a.Mission ?? Mission.Current);
                _held[site].Add(a.Index);
            }
            catch { }
        }

        /// <summary>Odwrot wyslany (BrokenMen: Panic albo Retreat; FieldCraft: Retreat) - do progu "poza bitwa przy kluczu 0".
        /// Rozni ludzie (jak Held) i osobno wywolania (ponowienia FieldCraft co 0.2 s - lekcja zapetlonego krzyku 27.08).</summary>
        internal static void Pushed(int site, Agent a)
        {
            try
            {
                if (a == null || site < 0 || site >= Sites) return;
                Ensure(a.Mission ?? Mission.Current);
                _pushed[site].Add(a.Index);
                _pushCalls[site]++;
            }
            catch { }
        }

        // ------------------------------------------------------------ rodzaj misji

        /// <summary>Bitwa na mapie: Campaign.Current == null (bitwa wlasna) albo MapEvent.PlayerMapEvent != null (ta sama regula co FieldCraft.InBattle
        /// i XpFair, bez CombatType - gra go nie ustawia, recenzja E8/X). Liczona raz na misje. Nie wiadomo (wyjatek) = bitwa (jak dotad).</summary>
        internal static bool IsBattle(Mission m)
        {
            try
            {
                Ensure(m);
                return _battle != 0;
            }
            catch { return true; }
        }

        /// <summary>Poczatek misji (AfterStart naszych zachowan): liczniki od zera, rodzaj misji i bitwa liczone raz.</summary>
        internal static void Begin(Mission m)
        {
            try { Ensure(m); } catch { }
        }

        private static void Ensure(Mission m)
        {
            if (m == null || (_mission != null && ReferenceEquals(m, _mission.Target))) return;
            if (_any && !_flushed)
            {
                try { Write(); } catch { }            // poprzednia misja bez OnEndMission - linia teraz
            }
            _mission = new WeakReference(m);
            _any = true;
            _flushed = false;
            for (int i = 0; i < Sites; i++) { _skipped[i].Clear(); _held[i].Clear(); _pushed[i].Clear(); _pushCalls[i] = 0; _stum[i] = 0; }
            _skippedAll.Clear();
            _first = null;
            bool battle = true;
            try { battle = Campaign.Current == null || TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent != null; }
            catch { battle = true; }
            _battle = battle ? 1 : 0;
            _kind = KindOf(m, battle);
        }

        // rodzaj tylko do linii; o ucieczce decyduje sama bitwa na mapie (_battle) - nazwa cudzego zachowania jej nie zmieni
        private static string KindOf(Mission m, bool battle)
        {
            try
            {
                if (HideoutAlarm.IsHideout(m)) return "kryjowka";
                if (battle) return "bitwa";
                bool arena = false;
                foreach (var b in m.MissionBehaviors)
                {
                    var n = b != null ? b.GetType().Name : null;
                    if (n == null) continue;
                    if (n.Contains("Tournament")) return "turniej";
                    if (n.Contains("Arena")) arena = true;
                }
                if (arena) return "arena";
            }
            catch { }
            return battle ? "bitwa" : "inna";
        }

        // ------------------------------------------------------------ linia misji

        /// <summary>Koniec misji (OnEndMission naszych zachowan): linia raz na misje.</summary>
        internal static void Flush(Mission m)
        {
            try
            {
                if (m == null) return;
                Ensure(m);
                if (_flushed) return;
                _flushed = true;
                Write();
            }
            catch { }
        }

        private static int Sum(int[] a) { int s = 0; for (int i = 0; i < a.Length; i++) s += a[i]; return s; }

        private static void Write()
        {
            int skips = _skippedAll.Count, held = 0, pushed = 0, stum = Sum(_stum);
            for (int i = 0; i < Sites; i++) { held += _held[i].Count; pushed += _pushed[i].Count; }
            // recenzja NM: cywile gry maja Team.Invalid (MissionAgentHandler.cs:849-853) - sami pominieci nie daja linii misji "inna"
            // (spacer po miescie, wsi, zamku, karczmie); linia tylko, gdy cos sie stalo: wstrzymani, odwroty albo potkniecia
            if (held + pushed + stum == 0 && _kind == "inna") return;
            _lines++;
            var c = Settings.Current;
            bool key = c != null && c.WoundedFleeBattlesOnly;
            var sb = new StringBuilder(400);
            sb.Append("Druzyny w misji (NM): misja ").Append(_kind).Append(" (bitwa na mapie ").Append(_battle != 0 ? "TAK" : "NIE")
              .Append(") | agenci bez druzyny (Team.Invalid) pominieci: ");
            for (int i = 0; i < Sites; i++) sb.Append(i > 0 ? ", " : "").Append(SiteName[i]).Append(' ').Append(_skipped[i].Count);
            sb.Append(" (roznych ludzi ").Append(skips);
            if (_first != null) sb.Append(", pierwszy ").Append(_first);
            sb.Append(") | ucieczka tylko w bitwie (WoundedFleeBattlesOnly ").Append(key ? "TAK" : "NIE").Append("): wstrzymani rozbici ")
              .Append(_held[Broken].Count).Append(", ucieczka ").Append(_held[Flee].Count)
              .Append(" | odwroty wyslane (roznych ludzi): rozbici ").Append(_pushed[Broken].Count).Append(", ucieczka ").Append(_pushed[Flee].Count)
              .Append(" (wywolan: rozbici ").Append(_pushCalls[Broken]).Append(", ucieczka ").Append(_pushCalls[Flee]).Append(')')
              .Append(" | potkniecia: ");
            for (int i = 0; i < Sites; i++) sb.Append(i > 0 ? ", " : "").Append(SiteName[i]).Append(' ').Append(_stum[i]);
            sb.Append(" (sesja ").Append(Sum(_stumSession)).Append(", linia misji ").Append(_lines).Append(").");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ start sesji

        /// <summary>Linia startu sesji (ArmouryBehavior.OnSessionLaunched, PO McmSettings.Apply): klucz i mechaniki pod straznikiem; liczniki sesji od zera.</summary>
        internal static void SessionStart()
        {
            for (int i = 0; i < Sites; i++) _stumSession[i] = 0;
            _lines = 0;
            var c = Settings.Current;
            if (c == null) return;
            Log.Info("Druzyny w misji (NM): start sesji | klucz WoundedFleeBattlesOnly " + (c.WoundedFleeBattlesOnly ? "TAK" : "NIE")
                     + " (ranni uciekaja tylko w bitwie na mapie: BrokenMen WoundedFlee " + (c.WoundedFleeEnabled ? "TAK" : "NIE")
                     + ", FieldCraft AiFleeWhenNearDeath " + (c.FieldCraftEnabled && c.AiFleeWhenNearDeath ? "TAK" : "NIE")
                     + ") | straznik druzyn (Team.Invalid) w: HideoutAlarm " + (c.HideoutAlarmEnabled ? "TAK" : "NIE")
                     + ", BrokenMen, FieldCraft, GuardMaster " + (c.AutoParryEnabled ? "TAK" : "NIE")
                     + ", BattlefieldLaw.CountKill | bledy do logu: pierwsze " + ErrorsPerSite + " na miejsce w sesji, reszta w liczniku linii misji.");
        }
    }
}
