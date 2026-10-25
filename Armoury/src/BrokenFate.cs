using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 139 (25.10, fala "AI walczy o zycie", krok 1 = "136 Pomiar losu zlamanych" z syntezy ai-przetrwanie; Jeff 13:30: "w panice jest zasada uciekaj
    /// i brak rozsadku ... Wtedy ginie najwiecej osob"). SAM POMIAR, zero zmian zachowania: co sie dzieje z ludzmi, ktorzy sie zlamali - zanim cokolwiek
    /// kalibrujemy (panika bez powrotu, bieg bez odsapki, poscig konnicy AI).
    /// Kogo liczymy (bitwa na mapie albo kryjowka, klucz FateOfTheBrokenLog; kazdy czlowiek raz, w jednej grupie wedlug tego, kto go zlamal):
    ///  - "nasi" - zlamani przez RoutHold.Break, ktorzy nie byli w panice (nasze Panic: BrokenMen ponizej 20% HP, FieldCraft ponizej 10%, zalamanie choragwi);
    ///  - "panika gry" - pierwsza panika z Mission.OnAgentPanicked u czlowieka spoza zbioru zlamanych (morale gry ponizej 0.01, RBM CoordinatedRetreat,
    ///    stare drogi BrokenMen/FieldCraft przy BrokenMenNeverRally NIE); gdy potem wezmie go RoutHold (sciezka "juz w panice") - "przejety", grupa ta sama.
    /// Co liczymy: los (OnAgentRemoved: zabity / nieprzytomny / uszedl z pola; reszta "na polu" na koniec), kto dopadl uciekajacego (cios konczacy:
    /// pocisk, konny, pieszy, inne - wykrwawienie albo bez sprawcy), droga i czas ucieczki (probki pozycji co 1 s), zadyszka w ucieczce (FieldCraft st.Winded),
    /// bron i tarcza w rece przy losie albo na koniec misji (ArtemsLivelyAnimations zrzuca bron przy OnAgentPanicked), powroty z paniki do walki (przejscie
    /// PANIKA -> ROZSADEK przelacznika SurvivalMode u paniki gry przed koncem bitwy - RBM StopRetreating, AgentAi.cs:651-657; takze wykryte dopiero przy
    /// ponownej panice, bo OnAgentPanicked przychodzi tylko przy !IsPanicked), wiwatujacych zwyciezcow (AgentVictoryLogic._cheeringAgents, probka co 1 s)
    /// i spis przelacznika co 2 s (ludzie: panika gry / RoutHold / sam odwrot komponentu / odwrot z rozkazu szyku).
    /// Wejscia tylko z RoutKeeper (wlasciciel linii NB2, zarejestrowany w kazdej bitwie z kluczem): Break, OnAgentPanicked, OnAgentRemoved, OnAgentDeleted,
    /// OnMissionTick (probka co 1 s - tylko lista zlamanych, bez alokacji; spis co 2 s po Mission.Agents), OnEndMission (Close przed linia, Release po niej).
    /// Wszystko pod RoutHold.Gate; wyjatek liczony w miejscu "los" linii NB2 (pierwsze 3 w sesji do logu), czlowiek z 5 potknieciami wypada z probek -
    /// funkcji nie gasimy (CLAUDE.md 7). Liczniki zeruje RoutHold.Ensure (zmiana misji). Linia "LOS ZLAMANYCH (139): ..." zaraz po linii "Bitwa (NB2): misja".
    /// </summary>
    internal static class BrokenFate
    {
        private sealed class Track
        {
            public Agent A;
            public bool Ours;          // zlamany przez nasze RoutHold.Break (Panic z naszej reki)
            public bool Taken;         // panika gry przejeta potem przez RoutHold
            public bool Done;          // los zapisany albo czlowiek usuniety / wypadl po potknieciach
            public bool Panic = true;  // PANIKA w ostatniej probce (SurvivalMode)
            public bool Back;          // powrot do walki zauwazony w probce (do ponownej paniki)
            public bool Returned;      // kiedykolwiek wrocil z paniki
            public bool Winded;        // zadyszka w ucieczce - kiedykolwiek
            public int Fails;
            public float Since;        // poczatek biezacej ucieczki (czas misji)
            public Vec2 Last;          // pozycja z ostatniej probki
            public float Path;         // droga biezacej ucieczki (suma odcinkow probek)
        }

        internal const float SampleEvery = 1f;    // probka zlamanych
        internal const float CensusEvery = 2f;    // spis przelacznika po wszystkich ludziach
        private const int MaxFails = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly Dictionary<Agent, Track> ByAgent = new Dictionary<Agent, Track>();
        private static readonly List<Track> Tracks = new List<Track>();
        private static readonly Predicate<Track> IsDone = t => t.Done;
        private static WeakReference _mission;
        private static bool _battle;               // linia tej misji (bitwa / kryjowka z kluczem) - zostaje po Release do zapisu linii
        private static bool _live;                 // wejscia przyjmowane (do Release na koniec misji)
        private static bool _fcSeen;               // FieldCraft tej misji widziany w probce (zadyszka mierzona)
        private static string _kind = "?";
        private static float _acc, _census;

        // wiwat zwyciezcow: AgentVictoryLogic (gra) - lista _cheeringAgents (prywatna), pola CheeringAgent.Agent / IsCheeringOnRetreat (sprawdzone w 1.4.8)
        private static AgentVictoryLogic _avl;
        private static bool _avlInit;
        private static FieldInfo _cheerList, _cheerAgent, _cheerRetreat;

        // ---- liczniki misji
        private static int _ours, _game, _taken, _rePanic, _dropped;
        private static readonly int[,] Fate = new int[2, 3];   // [nasi, panika gry] x [zabici, nieprzytomni, uszli]
        private static int _byHorse, _byFoot, _byMissile, _byOther, _fellAfterReturn;
        private static int _caughtN, _escN;
        private static float _caughtPath, _caughtTime, _escPath, _escTime;
        private static int _winded;
        private static int _armDen, _armed, _shield, _endFleeing;
        private static int _returns, _returnPeople, _returnsUnarmed, _returnsLate;
        private static readonly HashSet<int> Cheer = new HashSet<int>();
        private static readonly HashSet<int> CheerRetreat = new HashSet<int>();
        private static int _cheerMax;
        private static readonly HashSet<int> SwPanic = new HashSet<int>();
        private static readonly HashSet<int> SwRout = new HashSet<int>();
        private static readonly HashSet<int> SwRetreat = new HashSet<int>();
        private static readonly HashSet<int> SwOrdered = new HashSet<int>();

        /// <summary>Zmiana misji (RoutHold.Ensure, wolajacy trzyma Gate): wszystko od zera, wejscia nieczynne do Begin.</summary>
        internal static void ResetCounters()
        {
            ByAgent.Clear();
            Tracks.Clear();
            _mission = null;
            _battle = _live = _fcSeen = false;
            _kind = "?";
            _acc = _census = 0f;
            _avl = null;
            _ours = _game = _taken = _rePanic = _dropped = 0;
            Array.Clear(Fate, 0, Fate.Length);
            _byHorse = _byFoot = _byMissile = _byOther = _fellAfterReturn = 0;
            _caughtN = _escN = 0;
            _caughtPath = _caughtTime = _escPath = _escTime = 0f;
            _winded = 0;
            _armDen = _armed = _shield = _endFleeing = 0;
            _returns = _returnPeople = _returnsUnarmed = _returnsLate = 0;
            Cheer.Clear();
            CheerRetreat.Clear();
            _cheerMax = 0;
            SwPanic.Clear();
            SwRout.Clear();
            SwRetreat.Clear();
            SwOrdered.Clear();
        }

        /// <summary>142: linia 139 tej misji jest (bitwa / kryjowka z kluczem) i jej powroty RBM (PANIKA -> ROZSADEK u paniki gry nieprzejetej) - wolajacy trzyma Gate.</summary>
        internal static bool LineLocked { get { return _battle; } }
        internal static int ReturnsLocked { get { return _returns; } }
        internal static int ReturnPeopleLocked { get { return _returnPeople; } }

        private static bool Same(Mission m)
        {
            var w = _mission;
            return w != null && m != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>Poczatek misji (RoutKeeper.AfterStart, po RoutHold.Begin): bitwa na mapie albo kryjowka z kluczem - pomiar czynny.</summary>
        internal static void Begin(Mission m)
        {
            if (m == null) return;
            try
            {
                var c = Settings.Current;
                string kind = MissionTeams.Kind(m);
                AgentVictoryLogic avl = null;
                try { avl = m.GetMissionBehavior<AgentVictoryLogic>(); } catch { avl = null; }
                lock (RoutHold.Gate)
                {
                    RoutHold.EnsureLocked(m);
                    _mission = new WeakReference(m);
                    _kind = kind;
                    _battle = c != null && c.FateOfTheBrokenLog && (kind == "bitwa" || kind == "kryjowka");
                    _live = _battle;
                    _avl = _battle ? avl : null;
                    if (_avl != null) InitCheer();
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Begin", e); }
        }

        private static void InitCheer()
        {
            if (_avlInit) return;
            _avlInit = true;
            try
            {
                var t = typeof(AgentVictoryLogic);
                _cheerList = t.GetField("_cheeringAgents", BindingFlags.NonPublic | BindingFlags.Instance);
                var n = t.GetNestedType("CheeringAgent", BindingFlags.NonPublic);
                if (n != null)
                {
                    _cheerAgent = n.GetField("Agent", BindingFlags.Public | BindingFlags.Instance);
                    _cheerRetreat = n.GetField("IsCheeringOnRetreat", BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch { }
        }

        private static Track New(Mission m, Agent a)
        {
            var t = new Track { A = a, Since = m.CurrentTime, Last = Vec2.Invalid };
            try { t.Last = a.Position.AsVec2; } catch { }
            ByAgent[a] = t;
            Tracks.Add(t);
            return t;
        }

        // ------------------------------------------------------------ wejscia z RoutKeeper

        /// <summary>RoutKeeper.Break: czlowiek wlasnie wszedl do zbioru zlamanych (wpis w RoutKeeper juz jest, Panic jeszcze nie). already = byl w panice gry.</summary>
        internal static void Broken(Mission m, Agent a, bool already)
        {
            if (!_live || a == null) return;
            try
            {
                lock (RoutHold.Gate)
                {
                    if (!_live || !Same(m)) return;
                    Track t;
                    if (ByAgent.TryGetValue(a, out t))
                    {
                        if (!t.Ours && !t.Taken) { t.Taken = true; _taken++; }   // panika gry przejeta przez RoutHold (albo po powrocie zlamany od nowa)
                        return;
                    }
                    t = New(m, a);
                    if (already) { t.Taken = true; _game++; _taken++; }        // panika gry sprzed naszego pomiaru - grupa gry, przejety
                    else { t.Ours = true; _ours++; }
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Broken", e); }
        }

        /// <summary>RoutKeeper.OnAgentPanicked (kazda panika w misji): panika gry - nowy czlowiek albo ponowna panika po powrocie.</summary>
        internal static void Panicked(Mission m, Agent a)
        {
            if (!_live || a == null) return;
            try
            {
                if (!a.IsHuman) return;                       // kon bez jezdzca panikuje od ciosu (CommonAIComponent.OnHit) - nie liczymy
                lock (RoutHold.Gate)
                {
                    if (!_live || !Same(m)) return;
                    Track t;
                    if (ByAgent.TryGetValue(a, out t))
                    {
                        // nasza panika (Break wpisuje czlowieka przed Panic) albo przejety - ponowne paniki liczy RoutHold (RePanic)
                        if (t.Ours || t.Taken || t.Done) return;
                        // druga panika gry: Panic wola OnAgentPanicked tylko przy !IsPanicked, a IsPanicked zeruje wylacznie StopRetreating - wrocil do walki
                        _rePanic++;
                        if (!t.Back)
                        {
                            _returns++;
                            _returnsLate++;                       // powrot niezauwazony w probce (krotszy niz 1 s) - bez stanu broni
                            if (!t.Returned) { t.Returned = true; _returnPeople++; }
                        }
                        t.Back = false;
                        t.Panic = true;
                        t.Since = m.CurrentTime;
                        t.Path = 0f;
                        try { t.Last = a.Position.AsVec2; } catch { t.Last = Vec2.Invalid; }
                        return;
                    }
                    if (a.IsMainAgent || !a.IsAIControlled) return;
                    var k = RoutHold.Live;
                    bool ours = k != null && k.Knows(a);          // zabezpieczenie: zlamany bez naszego wpisu (nie powinno sie zdarzyc)
                    t = New(m, a);
                    if (ours) { t.Ours = true; _ours++; }
                    else _game++;
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Panicked", e); }
        }

        /// <summary>RoutKeeper.OnAgentRemoved: los zlamanego (stan i cios konczacy przed zrzutem broni z trupa - Mission.OnAgentRemoved:2997-3031).</summary>
        internal static void Removed(Mission m, Agent a, Agent by, AgentState state, in KillingBlow blow)
        {
            if (!_live || a == null) return;
            try
            {
                lock (RoutHold.Gate)
                {
                    if (!_live) return;
                    Track t;
                    if (!ByAgent.TryGetValue(a, out t) || t.Done || !Same(m)) return;
                    t.Done = true;
                    int f = state == AgentState.Killed ? 0 : (state == AgentState.Unconscious ? 1 : (state == AgentState.Routed ? 2 : -1));
                    if (f < 0) return;                            // inny stan - zostaje w "na polu" (liczone odejmowaniem)
                    Fate[t.Ours ? 0 : 1, f]++;
                    if (!SurvivalMode.InPanic(a))
                    {
                        if (f < 2) _fellAfterReturn++;            // wrocil do walki i w niej padl
                        return;
                    }
                    float path = 0f, time = 0f;
                    if (t.Panic)
                    {
                        path = t.Path;
                        try
                        {
                            Vec2 pos = a.Position.AsVec2;
                            if (t.Last.IsValid && pos.IsValid) path += (pos - t.Last).Length;
                        }
                        catch { }
                        time = Math.Max(0f, m.CurrentTime - t.Since);
                    }
                    bool w, s;
                    Arms(a, out w, out s);
                    _armDen++;
                    if (w) _armed++;
                    if (s) _shield++;
                    if (f == 2) { _escN++; _escPath += path; _escTime += time; return; }
                    _caughtN++;
                    _caughtPath += path;
                    _caughtTime += time;
                    if (blow.IsValid && blow.IsMissile) _byMissile++;
                    else if (by != null && !ReferenceEquals(by, a))
                    {
                        if (by.IsMount || by.MountAgent != null) _byHorse++;
                        else _byFoot++;
                    }
                    else _byOther++;                              // wykrwawienie (FieldCraft: cios od siebie), upadek, bez sprawcy
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Removed", e); }
        }

        /// <summary>RoutKeeper.OnAgentDeleted: czlowiek usuniety bez losu - wypada z probek (zostaje w "na polu").</summary>
        internal static void Deleted(Agent a)
        {
            if (!_live || a == null) return;
            try
            {
                lock (RoutHold.Gate)
                {
                    Track t;
                    if (ByAgent.TryGetValue(a, out t)) t.Done = true;
                }
            }
            catch { }
        }

        /// <summary>RoutKeeper.OnMissionTick (co klatke, przed reszta): co 1 s probka zlamanych, co 2 s spis przelacznika (do konca bitwy), wiwat.</summary>
        internal static void Tick(Mission m, float dt)
        {
            if (!_live) return;
            _acc += dt;
            if (_acc < SampleEvery) return;
            float step = _acc;
            _acc = 0f;
            try
            {
                lock (RoutHold.Gate)
                {
                    if (!_live || !Same(m)) return;
                    bool ended = m.MissionEnded;
                    float now = m.CurrentTime;
                    bool fc = FieldCraft.LiveIn(m);
                    if (fc) _fcSeen = true;
                    bool any = false;
                    for (int i = 0; i < Tracks.Count; i++)
                    {
                        var t = Tracks[i];
                        if (t.Done) { any = true; continue; }
                        try { Sample(t, now, ended, fc); }
                        catch (Exception e)
                        {
                            RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Sample", e);
                            if (++t.Fails >= MaxFails) { t.Done = true; _dropped++; }   // jeden zly czlowiek wypada, reszta liczona dalej
                        }
                        if (t.Done) any = true;
                    }
                    if (any) Tracks.RemoveAll(IsDone);
                    _census += step;
                    if (_census >= CensusEvery)
                    {
                        _census = 0f;
                        if (!ended) Census(m);                    // po koncu bitwy gra sama gasi panike zwyciezcom - spis nic nie mowi
                    }
                    Cheers();
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Tick", e); }
        }

        private static void Sample(Track t, float now, bool ended, bool fc)
        {
            var a = t.A;
            if (a == null || !a.IsActive()) return;               // los przyjdzie z OnAgentRemoved
            bool p = SurvivalMode.InPanic(a);
            Vec2 pos = a.Position.AsVec2;
            if (p)
            {
                if (!t.Panic) { t.Since = now; t.Path = 0f; }     // nowa ucieczka po powrocie (np. odwrot RBM po szarzy)
                else if (t.Last.IsValid && pos.IsValid) t.Path += (pos - t.Last).Length;
                if (fc && !t.Winded && FieldCraft.WindedNow(a)) { t.Winded = true; _winded++; }
            }
            else if (t.Panic && !t.Ours && !t.Taken && !ended && a.IsAIControlled)
            {
                // PANIKA -> ROZSADEK u paniki gry przed koncem bitwy: powrot do walki (RBM StopRetreating po 10 s bez ciosu wrecz przy morale > 0)
                _returns++;
                if (!t.Returned) { t.Returned = true; _returnPeople++; }
                bool w, s;
                Arms(a, out w, out s);
                if (!w) _returnsUnarmed++;
                t.Back = true;
            }
            t.Panic = p;
            t.Last = pos;
        }

        /// <summary>Spis przelacznika (co 2 s, bez alokacji poza nowymi numerami w zbiorach): kto jest w PANICE i z jakiego powodu; odwrot z rozkazu szyku to ROZSADEK.</summary>
        private static void Census(Mission m)
        {
            try
            {
                var k = RoutHold.Live;
                var ags = m.Agents;
                if (ags == null) return;
                for (int i = 0; i < ags.Count; i++)
                {
                    var a = ags[i];
                    if (a == null || !a.IsHuman || !a.IsActive()) continue;
                    var cai = a.CommonAIComponent;
                    // kolejnosc zrodel: nasz zbior zlamanych (Break wola Panic, wiec tez IsPanicked), panika gry, sam odwrot komponentu (RBM szarza), rozkaz szyku
                    if (k != null && k.Knows(a)) SwRout.Add(a.Index);
                    else if (cai != null && cai.IsPanicked) SwPanic.Add(a.Index);
                    else if (cai != null && cai.IsRetreating) SwRetreat.Add(a.Index);
                    else if (a.IsRunningAway) SwOrdered.Add(a.Index);
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Census", e); }
        }

        private static void Cheers()
        {
            var avl = _avl;
            if (avl == null || _cheerList == null) return;
            try
            {
                var list = _cheerList.GetValue(avl) as IList;
                if (list == null || list.Count == 0) return;
                if (list.Count > _cheerMax) _cheerMax = list.Count;
                if (_cheerAgent == null) return;
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i];
                    if (o == null) continue;
                    var ag = _cheerAgent.GetValue(o) as Agent;
                    if (ag == null) continue;
                    Cheer.Add(ag.Index);
                    if (_cheerRetreat != null && (bool)_cheerRetreat.GetValue(o)) CheerRetreat.Add(ag.Index);
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Cheers", e); }
        }

        /// <summary>RoutKeeper.OnEndMission przed linia: uciekajacy, ktorzy jeszcze sa na polu (bron i tarcza), ostatnia probka wiwatu.</summary>
        internal static void Close(Mission m)
        {
            if (!_live) return;
            try
            {
                lock (RoutHold.Gate)
                {
                    if (!_live || !Same(m)) return;
                    for (int i = 0; i < Tracks.Count; i++)
                    {
                        var t = Tracks[i];
                        if (t.Done) continue;
                        try
                        {
                            var a = t.A;
                            if (a == null || !a.IsActive() || !SurvivalMode.InPanic(a)) continue;
                            bool w, s;
                            Arms(a, out w, out s);
                            _endFleeing++;
                            _armDen++;
                            if (w) _armed++;
                            if (s) _shield++;
                        }
                        catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Close", e); }
                    }
                    Cheers();
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteFate, "BrokenFate.Close", e); }
        }

        /// <summary>RoutKeeper.OnEndMission po linii i OnRemoveBehavior: bez referencji do ludzi skonczonej misji; liczniki zostaja do zapisu linii.</summary>
        internal static void Release()
        {
            try
            {
                lock (RoutHold.Gate)
                {
                    _live = false;
                    ByAgent.Clear();
                    Tracks.Clear();
                    _avl = null;
                }
            }
            catch { }
        }

        /// <summary>Bron (nie amunicja, nie sztandar) i tarcza w slotach 0-3 - te same sloty, ktore zrzuca ArtemsLivelyAnimations.DropAllWeapons.</summary>
        private static void Arms(Agent a, out bool weapon, out bool shield)
        {
            weapon = false;
            shield = false;
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var w = a.Equipment[(EquipmentIndex)i];
                    if (w.IsEmpty) continue;
                    var it = w.Item;
                    if (it == null) continue;
                    switch (it.ItemType)
                    {
                        case ItemObject.ItemTypeEnum.Shield: shield = true; break;
                        case ItemObject.ItemTypeEnum.Arrows:
                        case ItemObject.ItemTypeEnum.Bolts:
                        case ItemObject.ItemTypeEnum.SlingStones:
                        case ItemObject.ItemTypeEnum.Bullets:
                        case ItemObject.ItemTypeEnum.Banner: break;
                        default: weapon = true; break;
                    }
                }
            }
            catch { }
        }

        // ------------------------------------------------------------ linia

        private static string Avg(float sum, int n, string fmt)
        {
            return n > 0 ? (sum / n).ToString(fmt, Inv) : "-";
        }

        private static void Group(StringBuilder sb, int g, int total)
        {
            int k = Fate[g, 0], u = Fate[g, 1], r = Fate[g, 2];
            int field = Math.Max(0, total - k - u - r);
            sb.Append(" (zabici ").Append(k).Append(" / nieprzytomni ").Append(u).Append(" / uszli ").Append(r).Append(" / na polu ").Append(field);
        }

        /// <summary>Linia "LOS ZLAMANYCH (139)" zaraz po "Bitwa (NB2): misja" (RoutHold.Write, wolajacy trzyma Gate). Tylko bitwa / kryjowka z kluczem.</summary>
        internal static void Write(Settings c)
        {
            if (!_battle) return;
            var sb = new StringBuilder(1100);
            sb.Append("LOS ZLAMANYCH (139): misja ").Append(_kind)
              .Append(" | nasi ").Append(_ours);
            Group(sb, 0, _ours);
            sb.Append(") | panika gry ").Append(_game);
            Group(sb, 1, _game);
            sb.Append("; przejeci przez RoutHold ").Append(_taken).Append(", ponowne paniki ").Append(_rePanic).Append(')')
              .Append(" | dopadnieci w ucieczce ").Append(_caughtN).Append(": konny ").Append(_byHorse).Append(" / pieszy ").Append(_byFoot)
              .Append(" / pocisk ").Append(_byMissile).Append(" / inne ").Append(_byOther)
              .Append("; padli po powrocie do walki ").Append(_fellAfterReturn)
              .Append(" | sr. bieg: dopadnieci ").Append(Avg(_caughtPath, _caughtN, "0")).Append(" m / ").Append(Avg(_caughtTime, _caughtN, "0.0"))
              .Append(" s, uszli ").Append(Avg(_escPath, _escN, "0")).Append(" m / ").Append(Avg(_escTime, _escN, "0.0")).Append(" s (droga z probek co ")
              .Append(SampleEvery.ToString("0", Inv)).Append(" s)")
              .Append(" | z zadyszka ").Append(_winded).Append(" z ").Append(_ours + _game).Append(" (FieldCraft ").Append(_fcSeen ? "TAK" : "NIE").Append(')')
              .Append(" | w ucieczce z bronia ").Append(_armed).Append(", z tarcza ").Append(_shield).Append(" z ").Append(_armDen)
              .Append(" (przy losie albo na koniec; na koniec wciaz w ucieczce ").Append(_endFleeing).Append(')')
              .Append(" | powroty RBM ").Append(_returns).Append(" (ludzi ").Append(_returnPeople).Append(", bez broni ").Append(_returnsUnarmed)
              .Append(", wykryte dopiero przy ponownej panice ").Append(_returnsLate).Append(')')
              .Append(" | wiwat ").Append(Cheer.Count).Append(" (przy ucieczce wroga ").Append(CheerRetreat.Count).Append(", naraz najwiecej ").Append(_cheerMax)
              .Append(_cheerList != null && _cheerAgent != null ? "" : ", lista wiwatu BRAK").Append(')')
              .Append(" | przelacznik (spis co ").Append(CensusEvery.ToString("0", Inv)).Append(" s do konca bitwy, ludzi; czlowiek moze trafic do kilku): RoutHold ")
              .Append(SwRout.Count).Append(", panika gry ").Append(SwPanic.Count).Append(", sam odwrot komponentu (RBM szarza) ").Append(SwRetreat.Count)
              .Append(" - to PANIKA; odwrot z rozkazu szyku ").Append(SwOrdered.Count).Append(" - to ROZSADEK")
              .Append(" | wypadli z probek ").Append(_dropped).Append(", potkniecia los ").Append(RoutHold.StumblesLocked(RoutHold.SiteFate)).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
