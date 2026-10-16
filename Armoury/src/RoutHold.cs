using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NB2 (16.10, Jeff: "krzyk paniki zapetlony; ranni ida w moja strone, rzucaja bron i bija piescia; panika ma uciekac od niego, tam gdzie nie
    /// ma wroga, a gdy wrog wszedzie - gdzie go najmniej"). JEDNA REGULA ZLAMANEGO dla BrokenMen (ranny ponizej WoundedFleePercent) i FieldCraft
    /// (ponizej AiFleeBelowPercent) - klucz BrokenMenNeverRally; wylaczony = stare drogi obu plikow bez zmian.
    ///
    /// Dowod [K] (rozpoznanie rozp_panika 16.10): RBM w postfiksie HumanAIComponent.OnTick (RBMAI AgentAi.cs:651-657) w KAZDEJ klatce wola
    /// CommonAIComponent.StopRetreating kazdemu, kto ma morale > 0 i od 10 s nie oberwal wrecz (strzala nie odswieza LastMeleeHitTime).
    /// StopRetreating zeruje IsRetreating i IsPanicked i wola natywne Agent.StopRetreating (CommonAIComponent.cs:162-172). Panic() nie zmienia
    /// morale, wiec nasz ranny byl zatrzymywany, a FieldCraft co 0,2 s pchal go od nowa przez Panic() -> MissionAgentPanicHandler ->
    /// Mission.OnAgentFleeing -> AgentMoraleInteractionLogic.OnAgentFleeing: 70% okrzyk Debacle (1,47-3,0 s) przy KAZDEJ nowej panice, a
    /// ArtemsLivelyAnimations przy kazdym OnAgentPanicked zrzuca cala bron - po kolejnym zatrzymaniu czlowiek wracal do walki na piesci.
    /// Log Jeffa 16.10 03:50: ucieczka 257 wywolan na 7 ludzi.
    ///
    /// Zasady:
    ///  - Break: raz na czlowieka na bitwe; jedyne wolanie Panic() (okrzyk gry i animacja Artem najwyzej raz), kto juz jest w panice (vanilla),
    ///    Panic nie dostaje; morale 0.
    ///  - RoutKeeper co klatke tylko po zbiorze zlamanych: dopoki panika trwa (IsPanicked), morale przypiete na 0 - warunek RBM "morale > 0" nigdy
    ///    nie zachodzi. Gdy panike zgasi ktos inny (IsPanicked false), morale trzymane na 0,02: pod 0,01 gra sama wolalaby Panic (drugi okrzyk).
    ///  - Odwrot ponawiany samym Agent.Retreat (natywne, bez Panic i bez OnAgentFleeing), nie czesciej niz co RefreshEvery (4 s).
    ///  - Cel ucieczki (klucz FleeAwayFromEnemies, tylko bitwa polowa bez kryjowki): 16 kierunkow, wrogowie w promieniu 50 m, waga 1/max(d,4),
    ///    konny x2, gracz x2. Poprawka po recenzji (16.10): jadro cosinusowe - wrog dodaje zagrozenie w = waga x max(0, cos(kat kierunku - kat
    ///    wroga)) KAZDEMU kierunkowi w swojej polowie (dawniej tylko swoj sektor i dwaj sasiedzi, +-34 stopnie, a remisy wygrywal sektor o
    ///    najnizszym numerze - czlowiek mogl biec 45-67 stopni w strone Jeffa); wektor odpychania R = suma wag x kierunek OD wroga. Wynik kierunku
    ///    = zagrozenie - 0,02 x suma wag x cos(kierunek, R), wiec remis rozstrzyga kierunek najdalej od wrogow. Gracz blizej niz 15 m: kierunki
    ///    mniej niz 60 stopni od "od gracza" przegrywaja z kazdym kierunkiem od gracza. Cel gry zostaje, gdy zagrozenie jego kierunku
    ///    <= 1,25 x minimum + 0,05 x suma wag ORAZ lezy do 60 stopni od R (gdy R wyrazne, |R| >= 0,2 x suma wag) ORAZ (gracz blizej niz 15 m)
    ///    do 60 stopni od "od gracza"; inaczej kierunek najlepszego wyniku (gdy wrog wszedzie - tam, gdzie go najmniej): punkt 2 m przed granica
    ///    sceny w tym kierunku, droga sprawdzona (Scene.GetPathDistanceBetweenPositions, jak Mission.cs:2641), najwyzej 3 kandydaci na czlowieka
    ///    i 20 zapytan na klatke; bez drogi albo ponad limit - cel gry. Oblezenie, wypad, morska, kryjowka - cel gry.
    ///  - Koniec misji (MissionEnded) - nic nie robimy (gra sama zatrzymuje odwrot zwyciezcom, CommonAIComponent.cs:109-116); gracz przejmuje
    ///    czlowieka - wypada ze zbioru.
    ///  - Wyjatek na czlowieka liczony (Stumble, pierwsze 3 na miejsce w sesji do logu) - funkcji nie gasimy (CLAUDE.md 7). Bez Harmony, wszystko
    ///    na glownym watku (OnMissionTick, przed tickiem agentow - Mission.cs:3743 / 3774). Iterujemy tylko wlasna liste, nie Mission.Agents.
    /// Postura szeregowych wedlug tieru (BattleWind.PostureInitPostfix) pisze swoje liczniki tutaj - jedna linia "Bitwa (NB2)" po misji.
    /// Czesc 2 (16.10): ta sama linia niesie liczniki taktyki wedlug dowodcy (WarCouncil), weteranow wedlug tieru (VeteranAi), krwi przez pancerz
    /// (BattleGore) i glosow bitwy (BattleCries) - ich liczniki zeruje Ensure (zmiana misji), pisza je pod Gate przez EnsureLocked.
    /// Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings" (CrashScribe Net.cs:155).
    /// </summary>
    internal static class RoutHold
    {
        internal const int SitePanic = 0, SiteDir = 1, SitePosture = 2;
        // NB2 czesc 2: taktyka wedlug dowodcy (WarCouncil), weterani wedlug tieru (VeteranAi), krew przez pancerz (BattleGore), glosy (BattleCries)
        internal const int SiteTactics = 3, SiteVeteran = 4, SiteGore = 5, SiteVoice = 6;
        // NB2 czesc 3 (16.10): kaluze krwi na ziemi (BloodPools)
        internal const int SitePools = 7;
        private const int Sites = 8;
        private static readonly string[] SiteName = { "panika", "kierunek", "postura", "taktyka", "weteran", "krew", "glosy", "kaluze" };
        private const int ErrorsPerSite = 3;      // pelny slad do logu: pierwsze 3 na miejsce w sesji

        internal const float RefreshEvery = 4f;   // najkrotszy odstep ponowienia odwrotu - dluzej niz najdluzszy okrzyk Debacle / Fear (3,0 s)
        internal const float Radius = 50f;        // promien liczenia wrogow
        internal const int PathPerFrame = 20;     // zapytan o droge na klatke (cala misja)
        internal const int PathPerMan = 3;        // kandydatow z droga na czlowieka w jednej ocenie
        internal const float NoHandlerAfter = 1f; // po tylu sekundach bez odwrotu z handlera paniki wysylamy odwrot sami
        internal const float DebacleMax = 3f;     // najdluzszy okrzyk Debacle po Panic (1,47-3,0 s): po zgaszeniu paniki przez innych ponowienie juz od Since + 3 s
        internal const float LostCheckEvery = 0.5f; // miedzy ocenami: sprawdzenie "odwrot zgasl" najwyzej co 0,5 s na czlowieka (natywne IsRetreating / IsFadingOut)
        internal const float NearPlayer = 15f;    // gracz blizej - ucieczka tylko do 60 stopni od kierunku "od gracza" (Jeff 16.10)
        internal const float AwayCos = 0.5f;      // 60 stopni

        // NB2 czesc 2: wspolna blokada licznikow linii misji - takze dla WarCouncil, VeteranAi, BattleGore, BattleCries (EnsureLocked)
        internal static readonly object Gate = new object();
        private static WeakReference _mission;
        private static bool _any, _flushed;
        private static string _kind = "?";
        internal static RoutKeeper Live;          // zachowanie biezacej misji (null albo inna misja = nowa droga nieczynna)

        // ---- liczniki misji: panika
        internal static int Broken, Fled, Panics, Already, RePanic, Pins, Extinct, Resends, Turns, NoHandler, Player;
        internal static readonly HashSet<int> AfterEnd = new HashSet<int>();     // odmowy po koncu bitwy (rozni ludzie)
        internal static readonly HashSet<int> NoTeam = new HashSet<int>();       // bez waznej druzyny (Team.Invalid)
        internal static readonly HashSet<int> Ordered = new HashSet<int>();      // odwrot z rozkazu szyku (IsRunningAway bez paniki) - nie nasz
        internal static readonly HashSet<int> PinPeople = new HashSet<int>();
        internal static readonly HashSet<int> ResendPeople = new HashSet<int>();
        // ---- kierunek
        internal static int DirGame, DirSector, DirNoRoad, DirLimit, DirOff, PathQueries;
        internal static readonly HashSet<int> GameRejected = new HashSet<int>();  // poprawka 16.10: cel gry odrzucony (w strone wrogow) - rozni ludzie
        internal static readonly HashSet<int> NearMain = new HashSet<int>();      // z tego: gracz blizej niz 15 m, a cel gry nie od niego
        internal static int Field = -1;           // -1 nie wiadomo, 0 nie pole bitwy (cel gry), 1 bitwa polowa (sektory)
        // ---- postura
        private static readonly HashSet<int>[] Tiers = NewTiers();
        private static readonly HashSet<int> Heroes = new HashSet<int>();
        private static int _noWeapon;
        private static string _sample1, _sample6;
        // ---- potkniecia
        private static readonly int[] Stum = new int[Sites];
        private static readonly int[] StumSession = new int[Sites];
        private static int _lines;

        private static HashSet<int>[] NewTiers()
        {
            var a = new HashSet<int>[8];
            for (int i = 0; i < 8; i++) a[i] = new HashSet<int>();
            return a;
        }

        // ------------------------------------------------------------ wejscie z BrokenMen i FieldCraft

        /// <summary>Nowa droga zlamanych czynna w tej misji: klucz BrokenMenNeverRally i RoutKeeper tej misji zarejestrowany.</summary>
        internal static bool On(Mission m)
        {
            var c = Settings.Current;
            var k = Live;
            return c != null && c.BrokenMenNeverRally && k != null && m != null && ReferenceEquals(k.Mission, m);
        }

        /// <summary>Zlamanie czlowieka (raz na bitwe). true = jest w zbiorze zlamanych (teraz albo wczesniej); false = odmowa (koniec bitwy,
        /// bez druzyny, odwrot z rozkazu szyku, gracz) - wolajacy moze sprobowac pozniej.</summary>
        internal static bool Break(Agent a, int site)
        {
            var k = Live;
            if (k == null || a == null) return false;
            try { return k.Break(a, site); }
            catch (Exception e) { Stumble(SitePanic, "RoutHold.Break", e); return false; }
        }

        // ------------------------------------------------------------ postura (BattleWind.PostureInitPostfix)

        internal static void PostureSoldier(Agent a, int tier, float before, float after)
        {
            try
            {
                lock (Gate)
                {
                    Ensure(a.Mission ?? Mission.Current);
                    if (tier < 1) tier = 1;
                    if (tier > 7) tier = 7;
                    Tiers[tier].Add(a.Index);
                    if (tier == 1 && _sample1 == null) _sample1 = (int)Math.Round(before) + "->" + (int)Math.Round(after);
                    if (tier == 6 && _sample6 == null) _sample6 = (int)Math.Round(before) + "->" + (int)Math.Round(after);
                }
            }
            catch { }
        }

        internal static void PostureHero(Agent a)
        {
            try { lock (Gate) { Ensure(a.Mission ?? Mission.Current); Heroes.Add(a.Index); } }
            catch { }
        }

        internal static void PostureNoWeapon(Agent a)
        {
            try { lock (Gate) { Ensure(a.Mission ?? Mission.Current); _noWeapon++; } }
            catch { }
        }

        // ------------------------------------------------------------ potkniecia

        /// <summary>Wyjatek w miejscu: licznik misji i sesji; pelny slad tylko dla pierwszych 3 na miejsce w sesji. Funkcja zostaje wlaczona.</summary>
        internal static void Stumble(int site, string where, Exception e)
        {
            try
            {
                lock (Gate)
                {
                    Ensure(Mission.Current);
                    if (site < 0 || site >= Sites) site = SitePanic;
                    Stum[site]++;
                    StumSession[site]++;
                    if (StumSession[site] <= ErrorsPerSite) Log.Error(where, e);
                    if (StumSession[site] == ErrorsPerSite)
                        Log.Info("Bitwa (NB2): " + where + " - " + ErrorsPerSite + ". potkniecie miejsca '" + SiteName[site]
                                 + "' w tej sesji; kolejne tylko w liczniku linii misji.");
                }
            }
            catch { }
        }

        // ------------------------------------------------------------ misja

        /// <summary>Poczatek misji (RoutKeeper.AfterStart): liczniki od zera, rodzaj misji (ten sam co w linii NM).</summary>
        internal static void Begin(Mission m)
        {
            try
            {
                string kind = MissionTeams.Kind(m);
                lock (Gate) { Ensure(m); _kind = kind; }
            }
            catch { }
        }

        /// <summary>NB2 czesc 2: zgodnosc licznikow z misja dla innych klas linii - wolajacy trzyma Gate.</summary>
        internal static void EnsureLocked(Mission m)
        {
            Ensure(m);
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
            _kind = "?";
            Broken = Fled = Panics = Already = RePanic = Pins = Extinct = Resends = Turns = NoHandler = Player = 0;
            AfterEnd.Clear(); NoTeam.Clear(); Ordered.Clear(); PinPeople.Clear(); ResendPeople.Clear();
            DirGame = DirSector = DirNoRoad = DirLimit = DirOff = PathQueries = 0;
            GameRejected.Clear(); NearMain.Clear();
            Field = -1;
            for (int i = 0; i < Tiers.Length; i++) Tiers[i].Clear();
            Heroes.Clear();
            _noWeapon = 0;
            _sample1 = _sample6 = null;
            for (int i = 0; i < Sites; i++) Stum[i] = 0;
            WarCouncil.ResetCounters();
            VeteranAi.ResetCounters();
            BattleGore.ResetCounters();
            BattleCries.ResetCounters();
            BloodPools.ResetCounters();
        }

        /// <summary>Licznik misji dla RoutKeeper (glowny watek): misja zgodna z licznikami.</summary>
        internal static void Touch(Mission m)
        {
            try { lock (Gate) { Ensure(m); } } catch { }
        }

        /// <summary>Koniec misji (RoutKeeper.OnEndMission): linia raz na misje.</summary>
        internal static void Flush(Mission m)
        {
            try
            {
                if (m == null) return;
                lock (Gate)
                {
                    Ensure(m);
                    if (_flushed) return;
                    _flushed = true;
                    Write();
                }
            }
            catch { }
        }

        private static int Sum(int[] a) { int s = 0; for (int i = 0; i < a.Length; i++) s += a[i]; return s; }

        private static void Write()
        {
            int soldiers = 0;
            for (int i = 1; i < Tiers.Length; i++) soldiers += Tiers[i].Count;
            int broken = Broken + Fled, stum = Sum(Stum);
            int extra = WarCouncil.Activity + VeteranAi.Activity + BattleGore.Activity + BattleCries.Activity   // czesc 2 - tylko w bitwach
                        + BloodPools.Activity;                                                                // czesc 3 - kaluze krwi
            // spacer po miescie / wsi / zamku ("inna") bez zlamanych i potkniec - bez linii (straz miasta tez dostaje postury RBM)
            if (_kind == "inna" && broken + stum + extra == 0) return;
            if (_kind == "?" && broken + stum + soldiers + Heroes.Count + extra == 0) return;
            _lines++;
            var c = Settings.Current;
            var sb = new StringBuilder(1400);
            sb.Append("Bitwa (NB2): misja ").Append(_kind).Append(" (pole bitwy ").Append(Field == 1 ? "TAK" : (Field == 0 ? "NIE" : "?"))
              .Append(", Broken Men Never Rally ").Append(c != null && c.BrokenMenNeverRally ? "TAK" : "NIE").Append(')')
              .Append(" | zlamani ").Append(broken).Append(" (rozbici ").Append(Broken).Append(", ucieczka ").Append(Fled)
              .Append("; Panic ").Append(Panics).Append(", juz w panice ").Append(Already).Append(", ponowne paniki gry ").Append(RePanic)
              .Append("; odmowy: po koncu bitwy ").Append(AfterEnd.Count).Append(", bez druzyny ").Append(NoTeam.Count)
              .Append(", odwrot z rozkazu ").Append(Ordered.Count).Append(')')
              .Append(" | morale przypiete ").Append(Pins).Append(" razy (ludzi ").Append(PinPeople.Count).Append("), zgaszone przez innych ").Append(Extinct)
              .Append(" | odwrot: ponowienia bez okrzyku ").Append(Resends).Append(" (ludzi ").Append(ResendPeople.Count).Append("), zmiany kierunku ")
              .Append(Turns).Append(", bez handlera paniki ").Append(NoHandler).Append(", przejeci przez gracza ").Append(Player)
              .Append(" | kierunek: gry ").Append(DirGame).Append(", sektor ").Append(DirSector).Append(", bez drogi ").Append(DirNoRoad)
              .Append(", limit zapytan ").Append(DirLimit).Append(", poza polem ").Append(DirOff).Append(" (zapytan o droge ").Append(PathQueries).Append(')')
              .Append(", cel gry odrzucony ").Append(GameRejected.Count).Append(" (blisko gracza ").Append(NearMain.Count).Append(')')
              .Append(" | postura wg tieru ").Append(c != null && c.SoldierPostureByTier ? "TAK" : "NIE").Append(": szeregowych ").Append(soldiers).Append(" (");
            for (int i = 1; i < Tiers.Length; i++) sb.Append(i > 1 ? ", " : "").Append('T').Append(i).Append(' ').Append(Tiers[i].Count);
            sb.Append("), bohaterow ").Append(Heroes.Count).Append(", bez broni pominiete ").Append(_noWeapon)
              .Append("; probka T1 ").Append(_sample1 ?? "-").Append(", T6 ").Append(_sample6 ?? "-");
            // czesc 2 (16.10): taktyka wedlug dowodcy, weterani wedlug tieru, krew przez pancerz, okrzyki i jeki
            WarCouncil.Append(sb, c);
            VeteranAi.Append(sb, c);
            BattleGore.Append(sb, c);
            BattleCries.Append(sb, c);
            BloodPools.Append(sb, c);   // czesc 3 (16.10): kaluze krwi na ziemi - po 'obce glosy'
            sb.Append(" | potkniecia: ");
            for (int i = 0; i < Sites; i++) sb.Append(i > 0 ? ", " : "").Append(SiteName[i]).Append(' ').Append(Stum[i]);
            sb.Append(" (sesja ").Append(Sum(StumSession)).Append(", linia misji ").Append(_lines).Append(").");
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ start sesji

        /// <summary>Linia startu sesji (ArmouryBehavior.OnSessionLaunched, PO McmSettings.Apply): klucze i mnozniki tierow; liczniki sesji od zera.</summary>
        internal static void SessionStart()
        {
            lock (Gate)
            {
                for (int i = 0; i < Sites; i++) StumSession[i] = 0;
                _lines = 0;
            }
            var c = Settings.Current;
            if (c == null) return;
            var sb = new StringBuilder(1200);
            sb.Append("Bitwa (NB2): start sesji | panika: Broken Men Never Rally ").Append(c.BrokenMenNeverRally ? "TAK" : "NIE")
              .Append(" (rozbici BrokenMen ").Append(c.WoundedFleeEnabled ? "TAK" : "NIE")
              .Append(", ucieczka FieldCraft ").Append(c.FieldCraftEnabled && c.AiFleeWhenNearDeath ? "TAK" : "NIE")
              .Append("; raz Panic na czlowieka, morale przypiete na 0, ponowienie odwrotu bez okrzyku co ").Append(RefreshEvery.ToString("0"))
              .Append(" s), Flee Away From Enemies ").Append(c.FleeAwayFromEnemies ? "TAK" : "NIE")
              .Append(" (16 kierunkow, jadro cosinusowe, ").Append(Radius.ToString("0")).Append(" m, konny i gracz x2, gracz blizej niz ")
              .Append(NearPlayer.ToString("0")).Append(" m - tylko od niego, droga sprawdzana, najwyzej ")
              .Append(PathPerMan).Append(" kandydatow na czlowieka i ").Append(PathPerFrame).Append(" zapytan na klatke)")
              .Append(" | postura szeregowych wg tieru ").Append(c.SoldierPostureByTier ? "TAK" : "NIE").Append(": ");
            for (int t = 1; t <= 7; t++) sb.Append(t > 1 ? ", " : "").Append('T').Append(t).Append(" x").Append(BattleWind.SoldierMul(t).ToString("0.00"));
            sb.Append(", regeneracja x pierwiastek | latka postury RBM wpieta ").Append(BattleWind.PosturePatched ? "TAK" : "NIE")
              .Append(", oddech bohaterow ").Append(BattleWind.Active ? "TAK" : "NIE")
              // czesc 2 (16.10)
              .Append(" | Commander Tactics ").Append(c.CommanderTactics ? "TAK" : "NIE").Append(" (tlum ponizej ").Append(WarCouncil.HordeBelow)
              .Append(" Tactics, konnica i obrona od ").Append(WarCouncil.CavalryFrom).Append(", okrazenie RBM od ").Append(WarCouncil.EnvelopFrom)
              .Append(" przy stronie od ").Append(WarCouncil.EnvelopMinMen).Append(" ludzi, druzyna gracza bez zmian)")
              .Append(" | Veterans By Tier ").Append(c.VeteransByTier ? "TAK" : "NIE").Append(" (latka weteranow wpieta ").Append(VeteranAi.Patched ? "TAK" : "NIE")
              .Append("; prog tieru 0.05 + 0.13 x (T-1), limity RBM wyzej od T5, agresja x0.80..x1.28)")
              .Append(" | Blood Through Armour ").Append(c.BloodThroughArmour ? "TAK" : "NIE").Append(" (od ").Append(BattleGore.MinDamage)
              .Append(" obrazen przez kolczuge / plyty albo cios smiertelny, 1 na czlowieka na ").Append(BattleGore.PerManEvery.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" s, ").Append(BattleGore.PerSecond).Append(" na s)")
              .Append(" | Battle Cries And Moans ").Append(c.BattleCriesAndMoans ? "TAK" : "NIE").Append(" (szarza szyku AI raz na ")
              .Append((int)BattleCries.FormationCooldown).Append(" s, czlowiek raz na ").Append((int)BattleCries.ManCooldown).Append(" s, jeki ponizej ")
              .Append((int)Math.Round(BattleCries.MoanBelow * 100)).Append("% co ").Append((int)BattleCries.MoanMin).Append('-')
              .Append((int)(BattleCries.MoanMin + BattleCries.MoanSpan)).Append(" s do ").Append((int)BattleCries.MoanRange).Append(" m, najwyzej ")
              .Append(BattleCries.MoansPerMan).Append(" na rannego, najwyzej ")
              .Append(BattleCries.MaxPerSecond).Append(" glosow na s)")
              // czesc 3 (16.10): kaluze krwi na ziemi
              .Append(" | Ground Blood Pools ").Append(c.GroundBloodPools ? "TAK" : "NIE").Append(" (decale ").Append(BloodPools.DecalSet).Append(" z materialem ")
              .Append(BloodPools.MaterialPrefix).Append("0..").Append(BloodPools.Materials - 1).Append(", zgon / nokaut 1-3 kaluze, kon 1-2, plama od ")
              .Append(BloodPools.SplatMinDamage).Append(" obrazen ciecia / klucia; najwyzej ").Append(BloodPools.MaxPerMission).Append(" na misje, ")
              .Append(BloodPools.PerSecond).Append(" na s)")
              .Append(" | bledy do logu: pierwsze ").Append(ErrorsPerSite).Append(" na miejsce w sesji, reszta w liczniku linii misji.");
            Log.Info(sb.ToString());
        }
    }

    /// <summary>
    /// NB2: zachowanie misji pilnujace zlamanych (patrz RoutHold). Rejestrowane w SubModuleMain.OnMissionBehaviorInitialize, gdy jest czym sie
    /// zajac (BrokenMenNeverRally przy WoundedFlee albo FieldCraft + AiFleeWhenNearDeath) albo gdy liczona jest postura wedlug tieru (linia NB2).
    /// Czesc 2 (16.10): takze przy kluczach CommanderTactics, VeteransByTier, BloodThroughArmour, BattleCriesAndMoans - OnEndMission pisze linie NB2
    /// z ich licznikami (dodawane przed BattleSense, wiec jego AfterStart widzi juz RoutHold.Begin).
    /// </summary>
    internal sealed class RoutKeeper : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private sealed class Entry
        {
            public Agent A;
            public int Site;
            public bool Pending = true;       // czekamy na odwrot z handlera paniki (nastepna klatka po Panic)
            public bool Decided;              // pierwszy cel juz wybrany (bez limitu zapytan)
            public bool PanicSeen;            // OnAgentPanicked juz byl (drugi = ponowna panika gry)
            public bool Extinct;              // panike zgasil ktos inny
            public bool Resent;
            public bool EarlyDone;            // poprawka 16.10: wczesne ponowienie po zgaszeniu paniki przez innych juz bylo
            public bool Dead;
            public int Fails;                 // potkniecia na tym czlowieku
            public float Since;
            public float NextEval;
            public float LastSend = -100f;
            public float NextLostCheck;       // poprawka 16.10: miedzy ocenami sprawdzenie "odwrot zgasl" najwyzej co LostCheckEvery
            public WorldPosition Game = WorldPosition.Invalid;     // cel gry (handler albo GetClosestFleePositionForAgent)
            public WorldPosition Target = WorldPosition.Invalid;   // cel, ktory czlowiek ma teraz
        }

        private const int MaxFails = 5;       // tyle potkniec na jednym czlowieku - wypada ze zbioru (reszta dziala dalej)
        private const int GameKind = 0, SectorKind = 1, NoRoadKind = 2, LimitKind = 3, OffKind = 4;

        private readonly Dictionary<Agent, Entry> _by = new Dictionary<Agent, Entry>();
        private readonly List<Entry> _list = new List<Entry>();
        private readonly MBList<Agent> _near = new MBList<Agent>();
        private readonly float[] _threat = new float[16];
        private readonly float[] _score = new float[16];
        private readonly int[] _order = new int[16];
        private static readonly Vec2[] Dirs = MakeDirs();          // srodki 16 sektorow (te same katy co Sector)

        private static Vec2[] MakeDirs()
        {
            var d = new Vec2[16];
            for (int s = 0; s < 16; s++)
            {
                double ang = -Math.PI + (s + 0.5) * (2.0 * Math.PI / 16.0);
                d[s] = new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang));
            }
            return d;
        }
        private int _pathLeft;
        private int _field = -1;

        public RoutKeeper()
        {
            // OnBehaviorInitialize nie biegnie dla zachowan dodanych w OnMissionBehaviorInitialize (Mission.cs:3809-3816) - zapis tutaj i w EarlyStart
            RoutHold.Live = this;
        }

        public override void EarlyStart()
        {
            RoutHold.Live = this;
            base.EarlyStart();
        }

        public override void AfterStart()
        {
            try { RoutHold.Begin(Mission); } catch { }
            base.AfterStart();
        }

        protected override void OnEndMission()
        {
            try { if (Mission != null) FieldHere(Mission); RoutHold.Field = _field; RoutHold.Flush(Mission); } catch { }
            Clear();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            Clear();
            if (ReferenceEquals(RoutHold.Live, this)) RoutHold.Live = null;
            base.OnRemoveBehavior();
        }

        private void Clear()
        {
            try { _by.Clear(); _list.Clear(); _near.Clear(); } catch { }
        }

        private void Drop(Entry e, bool keepKey)
        {
            e.Dead = true;
            if (!keepKey && e.A != null) _by.Remove(e.A);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try { Entry e; if (affectedAgent != null && _by.TryGetValue(affectedAgent, out e)) Drop(e, false); } catch { }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            // zabity, powalony albo zniknal na granicy - zostaje klucz (raz na bitwe), wypada z ticku
            try { Entry e; if (affectedAgent != null && _by.TryGetValue(affectedAgent, out e)) Drop(e, true); } catch { }
        }

        public override void OnAgentControllerSetToPlayer(Agent agent)
        {
            try
            {
                Entry e;
                if (agent != null && _by.TryGetValue(agent, out e) && !e.Dead) { RoutHold.Player++; Drop(e, true); }
            }
            catch { }
        }

        public override void OnAgentPanicked(Agent affectedAgent)
        {
            try
            {
                Entry e;
                if (affectedAgent == null || !_by.TryGetValue(affectedAgent, out e)) return;
                if (e.PanicSeen) RoutHold.RePanic++;      // druga panika gry dla zlamanego (oczekiwane 0)
                e.PanicSeen = true;
            }
            catch { }
        }

        // ------------------------------------------------------------ zlamanie

        internal bool Break(Agent a, int site)
        {
            var m = Mission;
            if (m == null || a == null) return false;
            Entry old;
            if (_by.TryGetValue(a, out old)) return true;                 // raz na czlowieka na bitwe
            if (!a.IsActive() || !a.IsHuman) return false;
            RoutHold.Touch(m);
            if (m.MissionEnded) { RoutHold.AfterEnd.Add(a.Index); return false; }   // po bitwie gra sama prowadzi odwroty
            if (!a.IsAIControlled || a.IsMainAgent) return false;
            if (Undead.Character(a.Character)) return false;              // trup nie zna strachu
            // bez waznej druzyny (Team.Invalid): Panic i odwrot wywracaja kod gry (MissionTeams.cs) - pomijamy i liczymy
            if (!MissionTeams.Valid(a.Team)) { MissionTeams.Skip(a, site); RoutHold.NoTeam.Add(a.Index); return false; }
            var cai = a.CommonAIComponent;
            if (cai == null) return false;
            // odwrot z rozkazu szyku (silnik: IsRunningAway, bez paniki i bez odwrotu komponentu) - nie nasz; sprobujemy, gdy sie skonczy
            if (a.IsRunningAway && !cai.IsRetreating && !cai.IsPanicked) { RoutHold.Ordered.Add(a.Index); return false; }

            float now = m.CurrentTime;
            var e = new Entry { A = a, Site = site, Since = now, NextEval = now };
            _by[a] = e;
            _list.Add(e);
            if (site == MissionTeams.Broken) RoutHold.Broken++; else RoutHold.Fled++;
            MissionTeams.Pushed(site, a);                                 // linia NM: odwrot wyslany - raz na czlowieka
            if (cai.IsPanicked)
            {
                // juz w panice (rozbicie szyku przez morale) - bez drugiego Panic, czyli bez drugiego okrzyku i bez drugiej animacji
                RoutHold.Already++;
                e.PanicSeen = true;
            }
            else
            {
                RoutHold.Panics++;
                cai.Panic();                                              // JEDYNE Panic dla naszych rannych: okrzyk gry (70%) i animacja Artem raz
            }
            a.SetMorale(0f);
            return true;
        }

        // ------------------------------------------------------------ tick

        public override void OnMissionTick(float dt)
        {
            if (_list.Count == 0) return;
            try
            {
                var c = Settings.Current;
                if (c == null || !c.BrokenMenNeverRally) return;
                var m = Mission;
                if (m == null || m.MissionEnded) return;                  // gra sama zatrzymuje odwrot zwyciezcom (CommonAIComponent.cs:109-116)
                float now = m.CurrentTime;
                _pathLeft = RoutHold.PathPerFrame;
                bool dead = false;
                for (int i = 0; i < _list.Count; i++)
                {
                    var e = _list[i];
                    if (e.Dead) { dead = true; continue; }
                    try { TickOne(e, m, now, c); }
                    catch (Exception ex)
                    {
                        RoutHold.Stumble(RoutHold.SitePanic, "RoutHold.Tick", ex);
                        if (++e.Fails >= MaxFails) Drop(e, true);         // jeden zly czlowiek wypada, reszta dziala
                    }
                    if (e.Dead) dead = true;
                }
                if (dead) _list.RemoveAll(x => x.Dead);
            }
            catch (Exception ex) { RoutHold.Stumble(RoutHold.SitePanic, "RoutHold.OnMissionTick", ex); }
        }

        private void TickOne(Entry e, Mission m, float now, Settings c)
        {
            var a = e.A;
            if (a == null || !a.IsActive()) { Drop(e, true); return; }
            if (!a.IsAIControlled) { RoutHold.Player++; Drop(e, true); return; }
            var cai = a.CommonAIComponent;
            if (cai == null) { Drop(e, true); return; }
            // przeniesiony do Team.Invalid (widz pojedynku z hersztem kryjowki) - rece precz: GetClosestFleePositionForAgent i odwrot wywracaja
            // kod gry (MissionTeams.cs); zostaje w zbiorze, wraca, gdy znow ma druzyne
            if (!MissionTeams.Valid(a.Team)) return;

            // morale: przy trwajacej panice 0 (RBM nie zatrzyma: warunek "morale > 0"); po zgaszeniu przez kogos innego >= 0,02 (pod 0,01 gra
            // sama wolalaby Panic - drugi okrzyk); recovery gry dziala tylko bez paniki (CommonAIComponent.OnTickParallel)
            if (cai.IsPanicked)
            {
                if (a.GetMorale() > 0f)
                {
                    a.SetMorale(0f);
                    RoutHold.Pins++;
                    RoutHold.PinPeople.Add(a.Index);
                }
            }
            else
            {
                if (!e.Extinct) { e.Extinct = true; RoutHold.Extinct++; }
                if (a.GetMorale() < 0.02f) a.SetMorale(0.02f);
            }

            if (e.Pending)
            {
                if (cai.IsRetreating)
                {
                    // handler paniki wlasnie wyslal czlowieka do celu gry (MissionAgentPanicHandler -> CommonAIComponent.Retreat)
                    e.Pending = false;
                    WorldPosition g = WorldPosition.Invalid;
                    try
                    {
                        var r = a.GetRetreatPos();                        // natywne: cel, ktory gra wlasnie dala (GetClosestFleePositionForAgent)
                        if (r.IsValid) g = r;
                        else if (r.AsVec2.IsValid && m.Scene != null)
                            g = new WorldPosition(m.Scene, UIntPtr.Zero, new Vec3(r.AsVec2.x, r.AsVec2.y, a.Position.z), false);
                    }
                    catch { }
                    e.Game = g;
                    e.Target = g;
                    e.LastSend = now;
                    Decide(e, m, now, c, false);
                }
                else if (now - e.Since > RoutHold.NoHandlerAfter)
                {
                    // brak odwrotu z handlera (misja bez MissionAgentPanicHandler albo panika zgaszona przed odwrotem) - cel gry liczymy sami
                    e.Pending = false;
                    RoutHold.NoHandler++;
                    WorldPosition g = WorldPosition.Invalid;
                    try { g = m.GetClosestFleePositionForAgent(a); } catch { }
                    e.Game = g;
                    Decide(e, m, now, c, true);
                }
                return;
            }
            bool early = false;
            if (now < e.NextEval)
            {
                // miedzy ocenami kierunku: gdy natywny odwrot zgasl, ponawiamy od razu po RefreshEvery od ostatniego wyslania (bez Panic).
                // Poprawka 16.10 (recenzja): panike zgasil ktos inny (RBM po premii morale z callbacku) zaraz po pierwszym celu - jedno ponowienie
                // juz od Since + DebacleMax (okrzyk Debacle skonczony), zeby czlowiek bez broni nie wracal do 4 s na piesci.
                early = e.Extinct && !e.EarlyDone && now - e.Since >= RoutHold.DebacleMax;
                if (!early && now - e.LastSend < RoutHold.RefreshEvery) return;
                // sprawdzenie natywne najwyzej co 0,5 s na czlowieka (dawniej co klatke dla kazdego po 4 s od wyslania)
                if (now < e.NextLostCheck) return;
                bool lost = false;
                try { lost = !a.IsRetreating() && !a.IsFadingOut(); } catch { }
                if (!lost) { e.NextLostCheck = now + RoutHold.LostCheckEvery; return; }
                if (early) e.EarlyDone = true;                            // raz na czlowieka - potem zwykly odstep RefreshEvery
            }
            Decide(e, m, now, c, false, early);
        }

        /// <summary>Ocena celu: pierwsza (po handlerze) albo co RefreshEvery. Wysyla Agent.Retreat (natywne, bez Panic) tylko gdy trzeba:
        /// pierwszy cel inny niz gry, odwrot zgasl albo kierunek zmienil sie o ponad 45 stopni.</summary>
        private void Decide(Entry e, Mission m, float now, Settings c, bool mustSend, bool early = false)
        {
            var a = e.A;
            e.NextEval = now + RoutHold.RefreshEvery;
            int kind = GameKind;
            WorldPosition pick = WorldPosition.Invalid;
            try { pick = Choose(e, m, c, out kind); }
            catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteDir, "RoutHold.Kierunek", ex); pick = WorldPosition.Invalid; kind = GameKind; }
            if (kind == LimitKind)
            {
                RoutHold.DirLimit++;
                if (!e.Decided) e.NextEval = now + 0.5f;                  // pierwszy cel: proba za chwile, na razie cel gry
            }
            // przy odswiezeniu limit zapytan albo brak drogi w najlepszych sektorach nie cofa czlowieka do niebezpiecznego celu gry -
            // zostaje cel, ktory ma (mial droge, gdy go wybieralismy)
            if (e.Decided && (kind == LimitKind || kind == NoRoadKind) && e.Target.IsValid) pick = e.Target;
            if (!pick.IsValid) pick = e.Game;

            bool first = !e.Decided && kind != LimitKind;
            if (first)
            {
                e.Decided = true;
                if (kind == GameKind) RoutHold.DirGame++;
                else if (kind == SectorKind) RoutHold.DirSector++;
                else if (kind == NoRoadKind) RoutHold.DirNoRoad++;
                else if (kind == OffKind) RoutHold.DirOff++;
            }
            if (!pick.IsValid)
            {
                // nie ma czego wyslac - gra prowadzi sama; nastepna proba po RefreshEvery, nie w kolejnej klatce (recenzja 16.10: bez tego
                // czlowiek z lost=true wchodzil tu co klatke z pelnym Choose i zjadal budzet zapytan o droge)
                e.LastSend = now;
                return;
            }

            bool lost = false;
            try { lost = !a.IsRetreating() && !a.IsFadingOut(); } catch { }   // natywny odwrot zgasl (Agent.IsRetreating, nie komponent)
            bool send;
            if (mustSend) send = true;
            else if (first) send = kind == SectorKind || lost;            // cel gry juz wyslany przez handler - bez drugiego wywolania
            else if (lost) send = true;
            else send = Turned(a, e.Target, pick);
            if (!send) return;
            // ponowienie nie czesciej niz co RefreshEvery (pierwszy cel - zaraz po handlerze, w tej samej klatce co odwrot gry; wczesne ponowienie
            // po zgaszeniu paniki przez innych - raz, od Since + DebacleMax)
            if (!first && !mustSend && !(early && lost) && now - e.LastSend < RoutHold.RefreshEvery) return;
            a.Retreat(pick);                                              // natywne SetRetreatMode - bez Panic, bez OnAgentFleeing, bez okrzyku gry
            if (!first && !mustSend)
            {
                if (lost)
                {
                    RoutHold.Resends++;
                    RoutHold.ResendPeople.Add(a.Index);
                    e.Resent = true;
                }
                else RoutHold.Turns++;
            }
            e.Target = pick;
            e.LastSend = now;
        }

        /// <summary>Kierunek do celu zmienil sie o ponad 45 stopni (cos &lt; 0,707).</summary>
        private static bool Turned(Agent a, WorldPosition oldT, WorldPosition newT)
        {
            if (!oldT.IsValid) return true;
            Vec2 my = a.Position.AsVec2;
            Vec2 u = oldT.AsVec2 - my, v = newT.AsVec2 - my;
            float lu = u.Length, lv = v.Length;
            if (lu < 1f || lv < 1f) return false;
            return u.DotProduct(v) / (lu * lv) < 0.7071f;
        }

        private static int Sector(Vec2 v)
        {
            double ang = Math.Atan2(v.y, v.x) + Math.PI;                  // 0 .. 2 pi
            int s = (int)(ang / (2.0 * Math.PI / 16.0));
            if (s < 0) s = 0;
            if (s > 15) s = 15;
            return s;
        }

        private bool FieldHere(Mission m)
        {
            if (_field < 0)
            {
                bool f = false;
                try { f = m.IsFieldBattle && !HideoutAlarm.IsHideout(m); } catch { f = false; }
                _field = f ? 1 : 0;
                RoutHold.Field = _field;
            }
            return _field == 1;
        }

        /// <summary>Cel ucieczki: cel gry, gdy bezpieczny i od wrogow (od gracza, gdy blisko); inaczej kierunek najlepszego wyniku (jadro
        /// cosinusowe + odpychanie) z droga do granicy sceny.</summary>
        private WorldPosition Choose(Entry e, Mission m, Settings c, out int kind)
        {
            var a = e.A;
            WorldPosition game = e.Game;
            kind = GameKind;
            if (!c.FleeAwayFromEnemies) return game;
            if (!FieldHere(m) || !MissionTeams.Valid(a.Team) || m.Scene == null) { kind = OffKind; return game; }

            Vec2 my = a.Position.AsVec2;
            if (!my.IsValid) return game;                                 // utwardzenie: pozycja bez liczb - cel gry
            for (int i = 0; i < 16; i++) _threat[i] = 0f;
            float total = 0f, rx = 0f, ry = 0f;
            bool nearMain = false;
            Vec2 fromMain = Vec2.Zero;
            try
            {
                m.GetNearbyEnemyAgents(my, RoutHold.Radius, a.Team, _near);   // zapytanie natywne do naszej listy - bez iteracji Mission.Agents
                for (int i = 0; i < _near.Count; i++)
                {
                    var f = _near[i];
                    if (f == null || !f.IsActive() || !f.IsHuman || f.IsRunningAway) continue;
                    Vec2 v = f.Position.AsVec2 - my;
                    float d = v.Length;
                    if (!(d >= 0f) || float.IsInfinity(d)) continue;      // NaN / nieskonczonosc - pomijamy
                    float w = 1f / Math.Max(d, 4f);
                    if (f.MountAgent != null) w *= 2f;                    // jezdziec dogoni
                    if (f.IsMainAgent) w *= 2f;                           // gracz (Jeff) - od niego przede wszystkim
                    total += w;
                    if (d < 0.05f) { for (int s = 0; s < 16; s++) _threat[s] += 0.5f * w; continue; }   // tuz na nas - zagrozenie wszedzie po rowno
                    Vec2 u = v * (1f / d);                                // kierunek NA wroga
                    if (f.IsMainAgent && d < RoutHold.NearPlayer) { nearMain = true; fromMain = -u; }
                    rx -= w * u.x;                                        // odpychanie: kierunek OD wroga
                    ry -= w * u.y;
                    // jadro cosinusowe: kazdy kierunek w polowie wroga dostaje zagrozenie (dawniej tylko sektor i dwaj sasiedzi, +-34 stopnie)
                    for (int s = 0; s < 16; s++)
                    {
                        float cs = Dirs[s].x * u.x + Dirs[s].y * u.y;
                        if (cs > 0f) _threat[s] += w * cs;
                    }
                }
            }
            finally { _near.Clear(); }                                    // bez referencji do agentow miedzy ocenami
            if (total <= 0f) return game;                                 // nikogo w promieniu - cel gry

            float rl = (float)Math.Sqrt(rx * rx + ry * ry);
            Vec2 rh = rl > 1e-5f ? new Vec2(rx / rl, ry / rl) : Vec2.Zero;
            bool skew = rl >= 0.2f * total;                               // wyrazny kierunek "od wrogow" (nie otoczony po rowno)
            float min = float.MaxValue;
            for (int i = 0; i < 16; i++) if (_threat[i] < min) min = _threat[i];
            if (game.IsValid)
            {
                Vec2 gv = game.AsVec2 - my;
                float gl = gv.Length;
                if (gl > 1f && gv.IsValid)
                {
                    Vec2 gu = gv * (1f / gl);
                    bool safe = _threat[Sector(gv)] <= 1.25f * min + 0.05f * total;
                    bool away = !skew || gu.DotProduct(rh) >= RoutHold.AwayCos;
                    bool fromP = !nearMain || gu.DotProduct(fromMain) >= RoutHold.AwayCos;
                    if (safe && away && fromP) return game;
                    RoutHold.GameRejected.Add(a.Index);
                    if (!fromP) RoutHold.NearMain.Add(a.Index);
                }
            }

            // wynik kierunku: zagrozenie, remis -> najdalej od wrogow (R); gracz blizej niz 15 m - kierunki nie od niego przegrywaja z kazdym od niego
            for (int s = 0; s < 16; s++)
            {
                float sc = _threat[s] - 0.02f * total * Dirs[s].DotProduct(rh);
                if (nearMain && Dirs[s].DotProduct(fromMain) < RoutHold.AwayCos) sc += 2f * total;
                _score[s] = sc;
            }
            // kierunki od najlepszego wyniku (16 elementow - wybor prosty)
            for (int i = 0; i < 16; i++) _order[i] = i;
            for (int i = 0; i < 15; i++)
            {
                int b = i;
                for (int j = i + 1; j < 16; j++) if (_score[_order[j]] < _score[_order[b]]) b = j;
                if (b != i) { int t = _order[i]; _order[i] = _order[b]; _order[b] = t; }
            }
            int tried = 0;
            float z = a.Position.z;
            for (int k = 0; k < 16 && tried < RoutHold.PathPerMan; k++)
            {
                int s = _order[k];
                Vec2 dir = Dirs[s];
                Vec2 edge = m.GetClosestBoundaryPosition(my + dir * 400f);
                if (!edge.IsValid) continue;                              // utwardzenie: NaN z natywnego nie idzie dalej
                Vec2 to = edge - my;
                float len = to.Length;
                if (len < 5f) continue;                                   // granica tuz obok - zaraz zniknie i tak
                Vec2 tn = to * (1f / len);
                if (tn.DotProduct(dir) < 0.6f) continue;                  // granica w tym kierunku nie lezy - nastepny sektor
                Vec2 p = edge - tn * 2f;                                  // 2 m przed granica (zanika przy celu albo przy granicy)
                if (!p.IsValid) continue;
                if (!m.IsPositionInsideBoundaries(p)) p = edge;
                if (_pathLeft <= 0) { kind = LimitKind; return game; }
                _pathLeft--;
                tried++;
                RoutHold.PathQueries++;
                WorldPosition from = a.GetWorldPosition();
                WorldPosition dest = new WorldPosition(m.Scene, UIntPtr.Zero, new Vec3(p.x, p.y, z), false);
                float dist;
                if (m.Scene.GetPathDistanceBetweenPositions(ref from, ref dest, 0f, out dist)) { kind = SectorKind; return dest; }
            }
            kind = NoRoadKind;
            return game;
        }
    }
}
