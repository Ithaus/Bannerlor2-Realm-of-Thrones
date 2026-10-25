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
    /// 142 (25.10, fala "AI walczy o zycie", karta "139a Panika bez powrotu"; klucz RoutedMenNeverRally, dziala przy BrokenMenNeverRally): w bitwie
    /// polowej KAZDA panika gry (morale gry ponizej 0.01, RBM CoordinatedRetreat = morale 0 calej strony) u czlowieka AI spoza zbioru trafia do zbioru
    /// zlamanych - RoutKeeper.OnAgentPanicked tylko wpisuje do kolejki, przejecie w nastepnym OnMissionTick przez Break(a, MissionTeams.GamePanic)
    /// sciezka "juz w panice" (bez drugiego Panic, okrzyku i animacji, morale 0, dalej jak nasz zlamany: przypiecie, ponowienia, kierunek). Najwyzej
    /// TakePerFrame przejec na klatke i tylko do MaxEntries zywych wpisow; czekajacy maja morale 0. Bez zbiorki przy sztandarze (decyzja Jeffa: pozniej).
    /// Liczniki i linia "PANIKA BEZ POWROTU (142)" - NoReturn.cs.
    /// 143 (25.10, karta "141 Ranny ponizej 20% HP: odwrot z bronia", decyzja Jeffa D2 "Odwrot z bronia"; klucz WoundedWithdrawWithArms): w bitwie polowej
    /// BrokenMen nie lamie juz rannego (Break -> Panic), tylko wola RoutKeeper.Withdraw - tryb "Wycofanie" (RoutHold.Withdraw.cs, liczniki WoundedWithdraw.cs):
    /// natywny Agent.Retreat za tyl wlasnego szyku albo do sztandaru BEZ CommonAIComponent.Panic / Retreat (bez OnAgentPanicked: bez zrzutu broni Artem, okrzyku
    /// Debacle i zarazania; komponent nie ma IsRetreating, wiec RBM StopRetreating - AgentAi.cs:651-657 - go nie dotyczy), postoj za linia (ScriptedPosition).
    /// ZMIANA REGULY NB2 z 16.10: panika rannego dopiero z bolu ponizej AiFleeBelowPercent (FieldCraft), przy zalamaniu kompanii, panice gry albo rozbitym szyku -
    /// wtedy Break konczy odwrot (wyjatek od "odwrotu z rozkazu szyku") i dalej jak kazdy zlamany.
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
        // m111-walka (24.10): pojedynki - DuelAi, DuelCraft, DuelGuard (turniej, arena, pojedynek ROT)
        internal const int SiteDuel = 8;
        // noc 25.10 P0 (krytyk K1 - indeksy nadaje integrator): marsz (MarchToContact, P1), doktryna (Doctrine, P1), choragiew (BannerCompanies / BannerKit, P3),
        // dzwiek (BattleSound, P2 - osobno od "glosy" BattleCries, zeby potkniecia sie nie zlewaly). UWAGA: RoutHold.Break(a, site) bierze miejsce MissionTeams
        // (Broken / Flee), NIE te stale - zlamanie choragwi idzie przez Break(a, MissionTeams.Flee) i liczy sie jako "ucieczka".
        internal const int SiteMarch = 9, SiteDoctrine = 10, SiteBanner = 11, SiteSound = 12;
        // 139 (25.10): pomiar losu zlamanych (BrokenFate) - wlasne miejsce, zeby potkniecia pomiaru nie zlewaly sie z panika
        internal const int SiteFate = 13;
        // 140 (25.10): tarcza pod ostrzalem (ShieldsUp) - wlasne miejsce (kolejka pociskow, pass ludzi i szykow, postfiksy SetAi / UpdateFormationOrders)
        internal const int SiteShields = 14;
        // 142 (25.10): panika bez powrotu (NoReturn) - kolejka paniki gry w RoutKeeper i przejecie do zbioru zlamanych; nazwa jednym slowem (regex NB2)
        internal const int SiteNoReturn = 15;
        // 143 (25.10): odwrot rannego z bronia (WoundedWithdraw, RoutHold.Withdraw.cs) - wlasne miejsce; nazwa jednym slowem (regex NB2)
        internal const int SiteWithdraw = 16;
        // 144 (25.10): AI oszczedza sily (AiBreath) i zadyszke widac / slychac (WindedShow) - wlasne miejsce; nazwa jednym slowem (regex NB2)
        internal const int SiteBreath = 17;
        // 145: ranny walczy ostroznie (WoundedWary przez VeteranAi.Postfix) - wlasne miejsce; nazwa jednym slowem (regex NB2)
        internal const int SiteWary = 18;
        // 146: szyk pod ostrzalem (CloseRanks - pass szykow i latka na decyzji szyku RBM); nazwa jednym slowem (regex NB2)
        internal const int SiteRanks = 19;
        // 147: bieg bez odsapki w panice (PanicRun - pomiar w FieldCraft); nazwa jednym slowem (regex NB2)
        internal const int SiteRun = 20;
        // 148: poscig konnicy AI (HorsePursuit - pass szykow, cel jezdzcow, haki trafien i strat); nazwa jednym slowem (regex NB2)
        internal const int SitePursuit = 21;
        // 149: bohater walczy o zycie (HeroCaution - na tyl szyku, wycofanie rannego / samotnego bohatera); nazwa jednym slowem (regex NB2)
        internal const int SiteHeroes = 22;
        private const int Sites = 23;
        private static readonly string[] SiteName = { "panika", "kierunek", "postura", "taktyka", "weteran", "krew", "glosy", "kaluze", "pojedynek",
                                                      "marsz", "doktryna", "choragiew", "dzwiek", "los", "tarcze", "powrot", "odwrot", "oddech", "ostroznosc",
                                                      "szyk", "bieg", "poscig", "bohaterowie" };
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
        // 142: panika gry -> RoutHold. Przejecie z kolejki najwyzej tyle na klatke (RBM CoordinatedRetreat = cala strona w jednej klatce: kazde
        // przejecie to pierwsza ocena kierunku z zapytaniem natywnym o wrogow w 50 m) i tylko do limitu zywych wpisow (koszt ticku liniowy);
        // reszta czeka w kolejce z morale przypietym na 0 (RBM jej nie zawroci), az zwolni sie miejsce.
        internal const int TakePerFrame = 30;
        internal const int MaxEntries = 800;

        // NB2 czesc 2: wspolna blokada licznikow linii misji - takze dla WarCouncil, VeteranAi, BattleGore, BattleCries (EnsureLocked)
        internal static readonly object Gate = new object();
        private static WeakReference _mission;
        private static bool _any, _flushed;
        private static string _kind = "?";
        internal static RoutKeeper Live;          // zachowanie biezacej misji (null albo inna misja = nowa droga nieczynna)
        // 145: zbior zlamanych dla postfiksow z dowolnego watku (SurvivalMode.PanicAnyThread <- WoundedWary <- VeteranAi.Postfix): czlowiek wedlug
        // Agent.Index, pisane TYLKO na glownym watku (RoutKeeper.Break, Drop, przejecie przez gracza, Clear); czytane bez blokady - odczyt referencji
        // z tablicy jest atomowy, nowa (wieksza) tablica publikowana po skopiowaniu. Ta sama tresc co RoutKeeper.Knows (bez przejetych przez gracza).
        private static Agent[] _known;
        private const int KnownMaxIndex = 65536;

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

        /// <summary>145: czlowiek jest w zbiorze zlamanych tej bitwy (jak RoutKeeper.Knows) - odczyt z dowolnego watku, bez blokady.</summary>
        internal static bool KnownAnyThread(Agent a)
        {
            if (a == null) return false;
            var k = System.Threading.Volatile.Read(ref _known);
            if (k == null) return false;
            int i = a.Index;
            return i >= 0 && i < k.Length && ReferenceEquals(k[i], a);
        }

        /// <summary>145: wpis / zdjecie w tablicy zbioru zlamanych - TYLKO glowny watek (RoutKeeper). Wieksza tablica: kopia, wpis, potem publikacja.</summary>
        internal static void MarkKnown(Agent a, bool on)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= KnownMaxIndex) return;
            var k = _known;
            if (k == null || i >= k.Length)
            {
                if (!on) return;
                int n = k == null ? 1024 : k.Length;
                while (n <= i) n *= 2;
                var nk = new Agent[n];
                if (k != null) Array.Copy(k, nk, k.Length);
                nk[i] = a;
                System.Threading.Volatile.Write(ref _known, nk);
                return;
            }
            if (on) k[i] = a;
            else if (ReferenceEquals(k[i], a)) k[i] = null;
        }

        /// <summary>145: koniec misji (RoutKeeper.Clear) - bez referencji do ludzi skonczonej misji.</summary>
        internal static void ClearKnown()
        {
            System.Threading.Volatile.Write(ref _known, null);
        }

        /// <summary>143: tryb "Wycofanie" czynny w tej misji - RoutHold czynny (On), klucz WoundedWithdrawWithArms i bitwa polowa (ten sam zakres co 142:
        /// IsFieldBattle bez kryjowki i morza, rodzaj "bitwa"). Oblezenie, wypad, kryjowka, morze - stara regula NB2 (Break, panika).</summary>
        internal static bool WithdrawHere(Mission m)
        {
            var c = Settings.Current;
            if (c == null || !c.WoundedWithdrawWithArms || !On(m)) return false;
            try { return Live.FieldForWithdraw(m); }
            catch { return false; }
        }

        /// <summary>143: ranny ponizej WoundedFleePercent (BrokenMen) - odwrot z bronia za wlasny szyk albo do sztandaru, bez Panic (RoutKeeper.Withdraw).
        /// Raz na czlowieka na bitwe; odmowa (cudzy odwrot, koniec bitwy) - BrokenMen sprobuje przy nastepnym takcie.</summary>
        internal static void Withdraw(Agent a)
        {
            var k = Live;
            if (k == null || a == null) return;
            try { k.Withdraw(a); }
            catch (Exception e) { Stumble(SiteWithdraw, "RoutHold.Withdraw", e); }
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

        /// <summary>m111-walka: potkniecia miejsca w biezacej misji (do linii "Pojedynek (DU)") - wolajacy trzyma Gate.</summary>
        internal static int StumblesLocked(int site)
        {
            return site >= 0 && site < Sites ? Stum[site] : 0;
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
            // noc 25.10 P0: segmenty nocy w tej samej linii (jeden wlasciciel linii - K1); kazda klasa zeruje tylko swoje liczniki
            MarchToContact.ResetCounters();
            Doctrine.ResetCounters();
            BannerCompanies.ResetCounters();
            BrokenFate.ResetCounters();     // 139: los zlamanych - liczniki i lista ludzi poprzedniej misji
            NoReturn.ResetCounters();       // 142: panika bez powrotu - liczniki misji
            WoundedWithdraw.ResetCounters();   // 143: odwrot rannego z bronia - liczniki misji
            WoundedWary.ResetCounters();       // 145: ranny walczy ostroznie - liczniki misji
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
                        + BloodPools.Activity                                                                 // czesc 3 - kaluze krwi
                        + MarchToContact.Activity + Doctrine.Activity + BannerCompanies.Activity;              // noc 25.10 (P1, P3)
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
            // noc 25.10 P0: segmenty marsz / doktryna (P1) i choragwie (P3) - wolane pod Gate (Append nie moze brac innej blokady)
            MarchToContact.Append(sb, c);
            Doctrine.Append(sb, c);
            BannerCompanies.Append(sb, c);
            sb.Append(" | potkniecia: ");
            for (int i = 0; i < Sites; i++) sb.Append(i > 0 ? ", " : "").Append(SiteName[i]).Append(' ').Append(Stum[i]);
            sb.Append(" (sesja ").Append(Sum(StumSession)).Append(", linia misji ").Append(_lines).Append(").");
            Log.Info(sb.ToString());
            // 139: osobna linia "LOS ZLAMANYCH (139)" zaraz po NB2 (tylko bitwa / kryjowka z kluczem FateOfTheBrokenLog)
            try { BrokenFate.Write(c); } catch { }
            // 142: osobna linia "PANIKA BEZ POWROTU (142)" po linii 139 (tylko bitwa polowa)
            try { NoReturn.Write(c); } catch { }
            // 143: osobna linia "ODWROT RANNYCH (143)" po linii 142 (tylko bitwa polowa)
            try { WoundedWithdraw.Write(c); } catch { }
            // 145: osobna linia "ranni ostrozni (145)" po linii 143 (bitwa z wywolaniami postfiksu VeteranAi albo klucz TAK w bitwie / kryjowce)
            try { WoundedWary.Write(c, _kind); } catch { }
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
              .Append(", oddech bohaterow ").Append(BattleWind.Active ? "TAK" : "NIE").Append(" (stamina tylko z Atletyki, 130: ").Append(c.StaminaFromAthleticsOnly ? "TAK" : "NIE").Append(')')
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
              .Append(BloodPools.MaterialPrefix).Append("0..").Append(BloodPools.Materials - 1).Append(", zgon 1-3 kaluze po upadku ciala, nokaut obuchem plama, kon 1-2, plama od ")
              .Append(BloodPools.SplatMinDamage).Append(" obrazen ciecia / klucia / strzaly ").Append(BloodPools.SplatsPerSecond).Append(" na s; ziemia, woda i pudlo sceny sprawdzane; najwyzej ")
              .Append(BloodPools.MaxPerMission).Append(" na misje, ").Append(BloodPools.PerSecond).Append(" na s, ").Append(BloodPools.PoolsPerCell)
              .Append(" kaluze na komorke ").Append(BloodPools.CellSize.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(" m)")
              // 139: pomiar losu zlamanych (sam log) i przelacznik trybu PANIKA / ROZSADEK
              .Append(" | Fate Of The Broken Log ").Append(c.FateOfTheBrokenLog ? "TAK" : "NIE").Append(" (linia LOS ZLAMANYCH (139) po bitwie; probka co ")
              .Append(BrokenFate.SampleEvery.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" s, spis przelacznika co ")
              .Append(BrokenFate.CensusEvery.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" s; panika = IsPanicked albo RoutHold albo odwrot komponentu, odwrot z rozkazu szyku = rozsadek)")
              // 142: kazda panika gry w bitwie polowej do RoutHold (bez powrotu)
              .Append(" | Routed Men Never Rally ").Append(c.RoutedMenNeverRally ? "TAK" : "NIE")
              .Append(c.RoutedMenNeverRally && !c.BrokenMenNeverRally ? " (nieczynne: Broken Men Never Rally NIE)" : "")
              .Append(" (panika gry w bitwie polowej - morale gry, RBM CoordinatedRetreat - do RoutHold w nastepnym ticku sciezka 'juz w panice': bez drugiego Panic, okrzyku i animacji, morale 0; najwyzej ")
              .Append(TakePerFrame).Append(" na klatke, limit zywych wpisow ").Append(MaxEntries).Append(", czekajacy z morale 0; zbiorki przy sztandarze brak)")
              // 143: ranny ponizej WoundedFleePercent - odwrot z bronia za wlasny szyk, bez Panic (zmiana reguly NB2 z 16.10)
              .Append(" | Wounded Withdraw With Arms ").Append(c.WoundedWithdrawWithArms ? "TAK" : "NIE")
              .Append(c.WoundedWithdrawWithArms && !(c.BrokenMenNeverRally && c.WoundedFleeEnabled) ? " (nieczynne: Broken Men Never Rally albo Wounded Flee NIE)" : "")
              .Append(" (bitwa polowa: ranny ponizej ").Append(c.WoundedFleePercent.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
              .Append("% HP bez Panic - natywny odwrot za tyl szyku (srodek - kierunek x (glebokosc/2 + 5 m)) albo do sztandaru, ponowienie co ")
              .Append(RefreshEvery.ToString("0")).Append(" s, postoj za linia (ScriptedPosition) od ").Append(WoundedWithdraw.ArriveRadius.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" m od celu, morale co najmniej 0.02; panika dopiero z bolu ponizej ").Append(c.AiFleeBelowPercent)
              .Append("% HP (FieldCraft), przy zalamaniu kompanii albo gdy szyk rozbity (bez szyku, sam, polowa w panice); oblezenia, kryjowki, morze - stara regula)")
              // 140: tarcza pod ostrzalem (linia 'Bitwa (140 tarcze)' po bitwie)
              .Append(" | AI Shields Up Under Fire ").Append(c.AiShieldsUpUnderFire ? "TAK" : "NIE").Append(" (trzymanie ")
              .Append(c.ShieldsUpHoldSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(" s, szyk od ").Append(c.ShieldsUpFormationShots)
              .Append(" pociskow w 5 s, ludzie gracza ").Append(c.ShieldsUpYourMenToo ? "TAK" : "NIE").Append(", piki przy konnicy w ")
              .Append(ShieldsUp.CavalryNear.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m ").Append(c.PikesKeptAgainstHorse ? "TAK" : "NIE")
              .Append("; bez zmiany broni w ").Append(ShieldsUp.NoSwapNear.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m od wroga, tarcza w gore poza ")
              .Append(ShieldsUp.RaiseNear.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m; latki ")
              .Append(ShieldsUp.PatchedSetAi && ShieldsUp.PatchedUfo ? "wpiete" : "przy pierwszej bitwie z kluczem").Append(')');
            // 146: szyk pod ostrzalem (linia 'Szyk pod ostrzalem (146)' po linii 140)
            sb.Append(" | AI Formations Under Fire ").Append(c.AiFormationsUnderFire ? "TAK" : "NIE")
              .Append(c.AiFormationsUnderFire && !c.AiShieldsUpUnderFire ? " (nieczynne: AI Shields Up Under Fire NIE - brak sygnalu S4)" : "")
              .Append(" (szyk AI piechoty pod ostrzalem przed zwarciem, bitwa polowa: ShieldWall od ")
              .Append((CloseRanks.WallFrom * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append("% ludzi z tarcza w rece, mniej - Loose (nie przy konnicy wroga w ")
              .Append(ShieldsUp.CavalryNear.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m); po zwarciu i bez ostrzalu decyduje RBM; zmiana najwyzej co ")
              .Append(CloseRanks.MinGap.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" s; szyki pod rozkazami gracza nietkniete; latka ")
              .Append(CloseRanks.Patched ? "wpieta" : "przy pierwszej bitwie z kluczem").Append(')');
            AiBreath.Session(sb, c);   // 144: AI oszczedza sily, marsz krokiem, zadyszke widac / slychac
            // 145: ranny walczy ostroznie (VeteranAi.Postfix -> WoundedWary; linia 'ranni ostrozni (145)' po bitwie)
            sb.Append(" | Wounded Fight Warily ").Append(c.WoundedFightWarily ? "TAK" : "NIE").Append(" (ranny ponizej ").Append(c.WoundedBelowPercent)
              .Append("% HP - prog kary FieldCraft: atak po decyzji x f, porzucenie zamachu i obrona tarcza x (2 - f), blok blizej 1, f = ")
              .Append(WoundedWary.Floor.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" + ")
              .Append((1f - WoundedWary.Floor).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" x HP / prog; bez bohaterow (krok 149) i bez PANIKI (139); odswiezanie RBM co 3 s; latka VeteranAi ").Append(VeteranAi.Patched ? "TAK" : "NIE").Append(')');
            // 147: bieg bez odsapki w panice (FieldCraft; linia 'Bieg zlamanych (147)' po misji)
            sb.Append(" | Panic Runs Until Spent ").Append(c.PanicRunsUntilSpent ? "TAK" : "NIE")
              .Append(c.PanicRunsUntilSpent && !(c.FieldCraftEnabled && c.SprintFatigueEnabled) ? " (nieczynne: Field Craft albo Sprint Fatigue NIE)" : "")
              .Append(" (pieszy w PANICE - przelacznik 139 - pije oddech jak w biegu 127 niezaleznie od predkosci, bez mnoznika strachu; w panice bez odnowy przy No Breath While Running ")
              .Append(c.NoBreathWhileRunning ? "TAK" : "NIE").Append(" - oddech 137 m = 0, regeneracja RBM pominieta; po wyczerpaniu 50% do granicy pola albo smierci; jezdziec bez zmian)");
            // 148: poscig konnicy AI (HorsePursuit; linia 'poscig (148)' po bitwie)
            sb.Append(" | AI Cavalry Pursues ").Append(c.AiCavalryPursues ? "TAK" : "NIE")
              .Append(" (bitwa polowa: konnica i konni lucznicy AI strony, ktora zlamala wroga - uciekajacy (PANIKA 139) w ")
              .Append(HorsePursuit.SeekRadius.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m, bez nietknietej formacji wroga w ")
              .Append(HorsePursuit.IntactRadius.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m; limit ")
              .Append(HorsePursuit.MaxSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" s / ")
              .Append(HorsePursuit.MaxFromMain.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m od glownej sily, noca polowa; najwyzej ")
              .Append(HorsePursuit.PerVictim).Append(" jezdzcow na uciekajacego; cel na czlowieka (SetTargetAgent + pozycja skryptowa co ")
              .Append(HorsePursuit.TrackEvery.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" s); formacje gracza, bohaterowie, chorazowie, smoki i umarli nie gonia; konie ponizej Horse AI Rest Below ").Append(c.HorseAiRestBelow).Append(" (136) nie gonia)");
            // 149: bohater walczy o zycie (HeroCaution; linia 'bohaterowie (149)' po bitwie)
            sb.Append(" | Heroes Keep Behind Their Men ").Append(c.HeroesKeepBehindTheirMen ? "TAK" : "NIE")
              .Append(" (bitwa polowa, bohater AI obu stron, nie gracz: w szyku w ostatnich 2 szeregach (zamiana miejsc co ")
              .Append(HeroCaution.RankEvery.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" s); ranny ponizej ")
              .Append((HeroCaution.WoundedBelow * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append("% HP albo samotny (0 swoich w ")
              .Append(HeroCaution.AllyRadius.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" m, co najmniej ").Append(HeroCaution.FoeMin)
              .Append(" wrogow w ").Append(HeroCaution.FoeRadius.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" m) - za swoj szyk pozycja skryptowa, bez paniki i okrzyku; samotny wraca do walki po ")
              .Append(HeroCaution.AloneHold.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" s, ranny stoi do konca bitwy; Duelists In Battles ")
              .Append(c.DuelistsInBattles ? "TAK" : "NIE").Append(" bez zmian; formacja wodza RBM nieprzywrocona)");
            sb.Append(" | bledy do logu: pierwsze ").Append(ErrorsPerSite).Append(" na miejsce w sesji, reszta w liczniku linii misji.");
            Log.Info(sb.ToString());
        }
    }

    /// <summary>
    /// NB2: zachowanie misji pilnujace zlamanych (patrz RoutHold). Rejestrowane w SubModuleMain.OnMissionBehaviorInitialize, gdy jest czym sie
    /// zajac (BrokenMenNeverRally przy WoundedFlee albo FieldCraft + AiFleeWhenNearDeath) albo gdy liczona jest postura wedlug tieru (linia NB2).
    /// Czesc 2 (16.10): takze przy kluczach CommanderTactics, VeteransByTier, BloodThroughArmour, BattleCriesAndMoans - OnEndMission pisze linie NB2
    /// z ich licznikami (dodawane przed BattleSense, wiec jego AfterStart widzi juz RoutHold.Begin).
    /// </summary>
    internal sealed partial class RoutKeeper : MissionBehavior
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
            public bool ByPlayer;             // 139: przejety przez gracza - przelacznik trybu (Knows) go nie liczy
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

        // 142: kolejka paniki gry. Mission.OnAgentPanicked iteruje zachowania misji (Mission.cs:6704-6710) - w callbacku tylko wpis do kolejki,
        // przejecie (Break, sciezka "juz w panice") w nastepnym OnMissionTick. Lock - na wypadek callbacku spoza glownego watku (konie od ciosu
        // odpadaja wczesniej: !IsHuman).
        private struct Pend { public Agent A; public float At; }
        private readonly object _qLock = new object();
        private readonly Queue<Pend> _queue = new Queue<Pend>();
        private readonly HashSet<Agent> _queued = new HashSet<Agent>();
        private readonly List<Pend> _take = new List<Pend>();
        private int _noReturn = -1;               // -1 nie wiadomo, 0 poza bitwa polowa, 1 bitwa polowa (bez kryjowki i morza)
        private bool _endSeen;                    // koniec bitwy (MissionEnded) juz zapisany do NoReturn

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
            BrokenFate.Begin(Mission);                // 139: pomiar losu zlamanych (sam wylapuje swoje wyjatki)
            // 142: bitwa polowa (liczona raz) i liczniki linii "PANIKA BEZ POWROTU (142)"
            try { if (Mission != null) NoReturn.Begin(Mission, NoReturnHere(Mission)); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteNoReturn, "NoReturn.Begin", e); }
            // 143: odwrot rannego z bronia - stan kluczy i bitwa polowa (ten sam zakres co 142)
            try { if (Mission != null) WoundedWithdraw.Begin(Mission, NoReturnHere(Mission)); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteWithdraw, "WoundedWithdraw.Begin", e); }
            base.AfterStart();
        }

        public override void OnDeploymentFinished()
        {
            try { if (Mission != null) NoReturn.Deployed(Mission.CurrentTime); } catch { }   // 142: czas bitwy od rozstawienia
            base.OnDeploymentFinished();
        }

        protected override void OnEndMission()
        {
            // 139: Close (uciekajacy wciaz na polu, ostatni wiwat) przed linia; Release (bez referencji do ludzi) po niej
            // 142: czekajacy w kolejce do licznika "po koncu bitwy", czas konca misji - przed linia
            try { if (Mission != null) NoReturn.Close(Mission.CurrentTime, DropQueue()); } catch { }
            try { CloseWithdraw(); } catch { }        // 143: kto na koniec w drodze / na postoju, bron - przed linia
            try { if (Mission != null) FieldHere(Mission); RoutHold.Field = _field; BrokenFate.Close(Mission); RoutHold.Flush(Mission); } catch { }
            BrokenFate.Release();
            Clear();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            Clear();
            DropQueue();                              // 142: bez referencji do ludzi skonczonej misji
            BrokenFate.Release();
            if (ReferenceEquals(RoutHold.Live, this)) RoutHold.Live = null;
            base.OnRemoveBehavior();
        }

        /// <summary>139: przelacznik trybu (SurvivalMode.InPanic) - czlowiek jest w zbiorze zlamanych tej bitwy (takze zabity i takze, gdy jego panike zgasil
        /// ktos inny), poza przejetym przez gracza. Tylko odczyt, glowny watek; po koncu misji (Clear) zawsze false.</summary>
        internal bool Knows(Agent a)
        {
            Entry e;
            return a != null && _by.TryGetValue(a, out e) && !e.ByPlayer;
        }

        private void Clear()
        {
            try { _by.Clear(); _list.Clear(); _near.Clear(); } catch { }
            // 145: tablica zbioru dla postfiksow (bez referencji do ludzi skonczonej misji) - nie ruszamy tablicy nowszego RoutKeeper (nastepna misja)
            if (RoutHold.Live == null || ReferenceEquals(RoutHold.Live, this)) RoutHold.ClearKnown();
            ClearWithdraw();                          // 143: wpisy odwrotu rannych (bez referencji do ludzi skonczonej misji)
        }

        /// <summary>142: kolejka paniki gry pusta (koniec misji, koniec bitwy); zwraca, ilu czekalo.</summary>
        private int DropQueue()
        {
            try
            {
                lock (_qLock)
                {
                    int n = _queue.Count;
                    _queue.Clear();
                    _queued.Clear();
                    _take.Clear();
                    return n;
                }
            }
            catch { return 0; }
        }

        /// <summary>142: bitwa polowa na mapie (albo wlasna) - IsFieldBattle bez kryjowki (FieldHere), bez bitwy morskiej, rodzaj misji "bitwa".
        /// Oblezenie, wypad, kryjowka, morze, turniej, arena - panika gry jak dotad. Liczone raz na misje.</summary>
        private bool NoReturnHere(Mission m)
        {
            if (_noReturn < 0)
            {
                bool f = false;
                try
                {
                    bool naval = false;
                    try { naval = m.IsNavalBattle || m.IsNavalRaidBattle; } catch { naval = false; }
                    f = !naval && FieldHere(m) && MissionTeams.Kind(m) == "bitwa";
                }
                catch { f = false; }
                _noReturn = f ? 1 : 0;
            }
            return _noReturn == 1;
        }

        /// <summary>142: panika gry (OnAgentPanicked) u czlowieka AI spoza zbioru zlamanych w bitwie polowej - pomiar zawsze, kolejka przy kluczach
        /// RoutedMenNeverRally i BrokenMenNeverRally. Nic poza wpisem (callback w petli zachowan misji).</summary>
        private void Enqueue(Agent a)
        {
            if (a == null || !a.IsHuman) return;                         // kon bez jezdzca panikuje od ciosu (CommonAIComponent.OnHit) - nie nasz
            var m = Mission;
            if (m == null || m.MissionEnded || !a.IsAIControlled || a.IsMainAgent) return;
            if (_by.ContainsKey(a)) return;                               // nasz zlamany (Break wpisuje przed Panic) albo juz przejety
            if (!NoReturnHere(m)) return;
            float now = m.CurrentTime;
            NoReturn.Candidate(a, now);
            var c = Settings.Current;
            if (c == null || !c.RoutedMenNeverRally || !c.BrokenMenNeverRally) return;
            lock (_qLock)
            {
                if (_queued.Add(a)) _queue.Enqueue(new Pend { A = a, At = now });
                NoReturn.QueueLen(_queue.Count);
            }
        }

        /// <summary>142: przejecie z kolejki (glowny watek, OnMissionTick): najwyzej TakePerFrame ludzi i tylko do limitu zywych wpisow; tylko kto
        /// WCIAZ jest w panice gry (IsPanicked) - Break idzie wtedy sciezka "juz w panice" (bez Panic, okrzyku i animacji; morale 0). Czekajacy:
        /// morale przypiete na 0, dopoki panika trwa (RBM StopRetreating wymaga morale &gt; 0).</summary>
        private void TakeQueued(Mission m, float now)
        {
            _take.Clear();
            int room = RoutHold.MaxEntries - _list.Count;
            lock (_qLock)
            {
                if (_queue.Count == 0) return;
                if (room <= 0) NoReturn.LimitFrame();
                int n = Math.Min(RoutHold.TakePerFrame, Math.Max(0, room));
                while (n-- > 0 && _queue.Count > 0)
                {
                    var p = _queue.Dequeue();
                    if (p.A != null) _queued.Remove(p.A);
                    _take.Add(p);
                }
            }
            for (int i = 0; i < _take.Count; i++)
            {
                var p = _take[i];
                var a = p.A;
                try
                {
                    if (a == null || !a.IsActive()) { NoReturn.Gone++; continue; }           // zginal, padl albo uszedl w kolejce - los liczy 139
                    if (_by.ContainsKey(a)) { NoReturn.OtherWay++; continue; }             // BrokenMen / FieldCraft / choragiew wziely go pierwsze
                    var cai = a.CommonAIComponent;
                    if (cai == null || !cai.IsPanicked) { NoReturn.Quenched++; continue; } // panike zgasil ktos przed przejeciem - bez drugiego Panic
                    if (Break(a, MissionTeams.GamePanic))
                    {
                        bool mine = false;
                        try { var t = a.Team; mine = t != null && (t.IsPlayerTeam || t.IsPlayerAlly); } catch { }
                        NoReturn.Took(now - p.At, a.IsHero, mine);
                    }
                    else NoReturn.Refused++;
                }
                catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteNoReturn, "RoutKeeper.TakeQueued", ex); }
            }
            _take.Clear();
            // czekajacy (limit wpisow albo ponad TakePerFrame): panika gry trwa - morale 0, zeby RBM nie zawrocil ich do walki przed przejeciem
            lock (_qLock)
            {
                if (_queue.Count == 0) return;
                foreach (var p in _queue)
                {
                    var a = p.A;
                    try
                    {
                        if (a == null || !a.IsActive()) continue;
                        var cai = a.CommonAIComponent;
                        if (cai != null && cai.IsPanicked && a.GetMorale() > 0f) { a.SetMorale(0f); NoReturn.WaitPins++; }
                    }
                    catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteNoReturn, "RoutKeeper.QueuePin", ex); }
                }
            }
        }

        private void Drop(Entry e, bool keepKey)
        {
            e.Dead = true;
            if (!keepKey && e.A != null) _by.Remove(e.A);
            if ((!keepKey || e.ByPlayer) && e.A != null) RoutHold.MarkKnown(e.A, false);   // 145: Knows = false (usuniety albo przejety przez gracza)
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try { Entry e; if (affectedAgent != null && _by.TryGetValue(affectedAgent, out e)) Drop(e, false); } catch { }
            BrokenFate.Deleted(affectedAgent);        // 139: usuniety bez losu - wypada z probek
            WithdrawDeleted(affectedAgent);           // 143: usuniety bez losu - wypada z ticku odwrotu
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            // zabity, powalony albo zniknal na granicy - zostaje klucz (raz na bitwe), wypada z ticku
            try { Entry e; if (affectedAgent != null && _by.TryGetValue(affectedAgent, out e)) Drop(e, true); } catch { }
            // 139: los zlamanego (stan, cios konczacy, bron w rece - przed zrzutem broni z trupa, Mission.OnAgentRemoved:2997-3031); tylko liczniki
            BrokenFate.Removed(Mission, affectedAgent, affectorAgent, agentState, in blow);
            WithdrawRemoved(affectedAgent, agentState);   // 143: los rannego w odwrocie (w drodze / na postoju), bron przy losie
        }

        public override void OnAgentControllerSetToPlayer(Agent agent)
        {
            try
            {
                Entry e;
                if (agent != null && _by.TryGetValue(agent, out e) && !e.Dead) { RoutHold.Player++; e.ByPlayer = true; Drop(e, true); }
            }
            catch { }
            WithdrawToPlayer(agent);                  // 143: gracz przejmuje rannego w odwrocie - postoj zdjety, wypada z ticku
        }

        public override void OnAgentPanicked(Agent affectedAgent)
        {
            try { CountFled(affectedAgent); } catch { }   // recenzja 143: zbiegli z szyku (szyk jeszcze ustawiony) - BrokenFormation odwrotu rannego
            try
            {
                Entry e;
                if (affectedAgent != null && _by.TryGetValue(affectedAgent, out e))
                {
                    if (e.PanicSeen)
                    {
                        RoutHold.RePanic++;               // druga panika gry dla zlamanego (oczekiwane 0)
                        if (e.Site == MissionTeams.GamePanic) NoReturn.RePanicGame++;   // 142: wsrod przejetych z paniki gry
                    }
                    e.PanicSeen = true;
                }
            }
            catch { }
            // 139: panika gry (czlowiek spoza zbioru zlamanych) albo ponowna panika po powrocie do walki - tylko liczniki
            BrokenFate.Panicked(Mission, affectedAgent);
            // 142: panika gry spoza zbioru zlamanych w bitwie polowej - tylko wpis do kolejki (przejecie w nastepnym ticku, nie z wnetrza callbacku)
            try { Enqueue(affectedAgent); }
            catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteNoReturn, "RoutKeeper.Enqueue", ex); }
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
            // 143: ranny w odwrocie z bronia (nasz natywny Agent.Retreat - IsRunningAway bez odwrotu komponentu) to nie "rozkaz szyku": konczymy jego
            // odwrot (postoj zdjety) i lamiemy go jak kazdego - z bolu (FieldCraft), przy zalamaniu kompanii, panice gry albo rozbitym szyku (tick odwrotu)
            bool wd = Withdrawing(a);
            // odwrot z rozkazu szyku (silnik: IsRunningAway, bez paniki i bez odwrotu komponentu) - nie nasz; sprobujemy, gdy sie skonczy
            if (!wd && a.IsRunningAway && !cai.IsRetreating && !cai.IsPanicked) { RoutHold.Ordered.Add(a.Index); return false; }
            // 142: panika gry bierzemy tylko trwajaca - nigdy nie wolamy za nia Panic (drugi okrzyk i animacja)
            if (site == MissionTeams.GamePanic && !cai.IsPanicked) return false;
            if (wd) EndWithdrawForBreak(a, site);
            // 149: bohater w wycofaniu (HeroCaution) - nasza pozycja skryptowa zdjeta PRZED Panic (jak postoj rannego 143 wyzej), powod do linii 149
            try { HeroCaution.BeforeBreak(a, site); }
            catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteHeroes, "HeroCaution.BeforeBreak", ex); }

            float now = m.CurrentTime;
            var e = new Entry { A = a, Site = site, Since = now, NextEval = now };
            _by[a] = e;
            RoutHold.MarkKnown(a, true);                                  // 145: ten sam zbior dla postfiksow z dowolnego watku (SurvivalMode.PanicAnyThread)
            _list.Add(e);
            bool game = site == MissionTeams.GamePanic;                   // 142: panika gry z kolejki - licznik w NoReturn, NB2 bez zmian
            if (site == MissionTeams.Broken) RoutHold.Broken++; else if (!game) RoutHold.Fled++;
            if (!game) MissionTeams.Pushed(site, a);                      // linia NM: odwrot wyslany - raz na czlowieka (panika gry: odwrot dala gra)
            BrokenFate.Broken(m, a, cai.IsPanicked);                      // 139: los zlamanych - wpis PRZED Panic (OnAgentPanicked widzi juz "nasz")
            if (cai.IsPanicked)
            {
                // juz w panice (rozbicie szyku przez morale) - bez drugiego Panic, czyli bez drugiego okrzyku i bez drugiej animacji
                if (!game) RoutHold.Already++;
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
            // 139: probka losu zlamanych (co 1 s, takze bez naszych zlamanych i po koncu bitwy - wiwat zwyciezcow); sama lapie wyjatki
            BrokenFate.Tick(Mission, dt);
            // 142: koniec bitwy (czas do linii) - raz; czekajacy w kolejce przepadaja (gra sama gasi panike zwyciezcom)
            if (!_endSeen)
            {
                try
                {
                    var me = Mission;
                    if (me != null && me.MissionEnded) { _endSeen = true; NoReturn.BattleEnded(me.CurrentTime, DropQueue()); }
                }
                catch { }
            }
            // 143: odwrot rannych z bronia - wlasna lista (po zbiorze zlamanych: przejecie paniki gry z kolejki konczy odwrot w Break)
            if (_list.Count == 0 && _queue.Count == 0)
            {
                if (_wList.Count > 0) TickWithdraw();
                return;
            }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();       // 142: koszt ticku przy pracy (linia 142)
            try { TickList(); }
            finally { NoReturn.Cost(System.Diagnostics.Stopwatch.GetTimestamp() - t0, _list.Count); }
            if (_wList.Count > 0) TickWithdraw();
        }

        private void TickList()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.BrokenMenNeverRally) return;
                var m = Mission;
                if (m == null || m.MissionEnded) return;                  // gra sama zatrzymuje odwrot zwyciezcom (CommonAIComponent.cs:109-116)
                float now = m.CurrentTime;
                // 142: kolejka paniki gry - przejecie poza callbackiem; nowi ida od razu przez TickOne ponizej (pierwszy cel po handlerze paniki)
                if (_queue.Count > 0)
                {
                    try { TakeQueued(m, now); }
                    catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteNoReturn, "RoutKeeper.TakeQueued", ex); }
                }
                if (_list.Count == 0) return;
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
            if (!a.IsAIControlled) { RoutHold.Player++; e.ByPlayer = true; Drop(e, true); return; }
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
                    if (e.Site == MissionTeams.GamePanic) NoReturn.PinGame.Add(a.Index);   // 142: premia morale u przejetego z paniki gry
                }
            }
            else
            {
                if (!e.Extinct)
                {
                    e.Extinct = true;
                    RoutHold.Extinct++;
                    if (e.Site == MissionTeams.GamePanic) NoReturn.ExtinctGame++;          // 142: tyle razy RBM / premia morale zgasily mimo przypiecia
                }
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
                    if (e.Site == MissionTeams.GamePanic) NoReturn.ResendGame++;           // 142: ponowiony odwrot (bez okrzyku) przejetego z paniki gry
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
