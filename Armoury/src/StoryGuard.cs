using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Armoury
{
    /// <summary>396 (Jeff 11.11: "tylko aby postacie w fabule ROT nie ginely przed wydarzneiami, odpiero moga ginac czy cos po tym jak juz nie sa potrzebne
    /// w wydarzniach o ile jescze zyja") - STRAZ FABULY, rdzen: weto smierci postaci, ktorych wydarzenia ROT jeszcze potrzebuja.
    ///
    /// Zrodlo prawdy o obsadzie: JEDNA tabela CanonCompanions.Guarded (32 id -> termy ev:X / done:X). Stan bohatera: 0 = poza fabula (klucz NIE, null, martwy,
    /// gracz, spoza tabeli), 1 = zwolniony, 2 = chroniony. Zwolniony, gdy: ma znak smierci albo jest Bialym Wedrowcem (na zywo); lista wydarzen ROT niewidoczna
    /// (wtedy Ready = false - weto gry nie dziala, bo nie da sie ocenic); ktorykolwiek term spelniony; wszystkie termy niemozliwe (tabela Need - kto musi zyc,
    /// zeby wydarzenie zaszlo); zawor Story Guard Grace Days po planowanym dniu (os Fabuli CrashScribe przez Chronicle.Terms.PlanDay).
    ///
    /// Weto: sluchacz CampaignEvents.CanHeroDieEvent - NIGDY nie ustawia true, tylko false dla chronionego. Lost (usuniecie) nie jest wetowane. Mord / egzekucja
    /// przechodzi, gdy bohater jest ofiara TRWAJACEGO wydarzenia (tabela Victims, IsActive czytane na zywo z obiektu - ROT zabija w tej samej dobie, w ktorej
    /// wlacza wydarzenie). Drogi wymuszone (isForced) i Lost omijaja sluchacz - sluchacz BeforeHeroKilledEvent pisze wtedy linie UTRATA.
    ///
    /// Bez stanu w zapisie (wszystko z listy ROT, dnia opowiesci i tabel). Pamiec do 32 bohaterow przeliczana raz na dobe (Daily) i leniwie po wczytaniu;
    /// czyszczona przy zmianie Campaign.Current, w SessionStart i OnGameEnd. Wyjatek = potkniecie liczone, bez weta; nic sie nie gasi.</summary>
    internal static class StoryGuard
    {
        private const string Tag = "StoryGuard: ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Kto musi ZYC (i nie byc Bialym Wedrowcem), zeby wydarzenie-term moglo jeszcze zajsc (MainCheck / ConditionsMet ROT). Tylko smierc - zadnych
        /// warunkow odwracalnych (wlasnosc osad, przynaleznosc rodu).</summary>
        private static readonly Dictionary<string, string[]> Need = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "RenlyDeathEvent", new[] { "lord_1_11", "lord_1_9" } }, { "JoffreyEvent", new[] { "lord_7_1" } },
            { "RickardKarstarkExecutionEvent", new[] { "lord_2_18" } }, { "JeorMutinyEvent", new[] { "lord_5_3" } },
            { "RooseDeathEvent", new[] { "lord_1_17", "lord_1_29" } }, { "JonEvent", new[] { "lord_5_17" } }, { "BastardsEvent", new[] { "lord_5_17" } },
            { "WallSiegeEvent", new[] { "lord_2_3" } }, { "BlackwaterSiegeEvent", new[] { "lord_1_11" } }, { "HarrenhalSiegeEvent", new[] { "lord_1_17" } },
            { "EastwatchEvent", new[] { "lord_1_11" } }, { "AegonInvasionEvent", new[] { "ROTlord_369" } }, { "DanyInvasionEvent", new[] { "lord_1_14" } },
        };

        /// <summary>Zawor: gdy termu nie ma na osi Fabuli, plan bierzemy z tego wydarzenia.</summary>
        private static readonly Dictionary<string, string> PlanOf = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "BastardsEvent", "BastardsSiegeEvent" },
        };

        /// <summary>Ofiary wydarzen: id bohatera -> wydarzenia ROT (dokladne nazwy typow), ktore maja go zabic. Roose w RedWeddingEvent osobno (klucz).</summary>
        private static readonly Dictionary<string, string[]> VictimOf = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "lord_7_1", new[] { "BlackwaterEvent", "JoffreyEvent" } }, { "lord_5_20", new[] { "BastardsEvent" } }, { "lord_2_23", new[] { "BastardsEvent" } },
            { "ROTlord_568", new[] { "JonEvent" } }, { "lord_1_17", new[] { "RamsayEvent" } }, { "lord_5_3", new[] { "JeorMutinyEvent" } },
            { "lord_2_18", new[] { "RickardKarstarkExecutionEvent" } }, { "lord_5_1", new[] { "RedWeddingEvent" } }, { "ROTlord_513", new[] { "RedWeddingEvent" } },
            { "lord_1_9", new[] { "RenlyDeathEvent" } }, { "lord_1_25", new[] { "RenlyDeathEvent" } },
        };
        private const string RooseId = "lord_1_17";
        private const string RedWedding = "RedWeddingEvent";

        // ---------------------------------------------------------------- pamiec (poza zapisem)
        private static Campaign _camp;
        private static bool _built, _ready, _startSaid, _blindSaid;
        private static int _tryHour = int.MinValue, _eventsSeen = -1;
        private static readonly Dictionary<Hero, int> _memo = new Dictionary<Hero, int>();          // bohater z tabeli (zywy) -> 1 zwolniony / 2 chroniony
        private static readonly Dictionary<Hero, string> _why = new Dictionary<Hero, string>();     // powod stanu (1: czemu zwolniony, 2: na co czeka)
        private static readonly Dictionary<string, object> _evObj = new Dictionary<string, object>(StringComparer.Ordinal);   // typ wydarzenia ROT -> obiekt (IsActive na zywo)
        private static readonly Dictionary<string, Hero> _byId = new Dictionary<string, Hero>(StringComparer.Ordinal);       // robocze: id z tabel -> bohater (zywy albo martwy)
        private static readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);                          // wszystkie id z tabel Guarded + Need
        private static readonly HashSet<Hero> _passed = new HashSet<Hero>();                        // przepuszczeni jako ofiary trwajacego wydarzenia (nie UTRATA)
        private static readonly HashSet<Hero> _blind = new HashSet<Hero>();                         // 396-p: stan 1 z BLEDU oceny / nieznanego typu wydarzenia - weto gry otwarte, ale NASZ kod nie zabija (Clear)
        private static readonly Dictionary<Hero, int> _vetoSaid = new Dictionary<Hero, int>();      // maska przyczyn z linia WETO w tej dobie
        private static readonly HashSet<Hero> _passSaid = new HashSet<Hero>();                      // linia PRZEPUSZCZONE w tej dobie
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static int _dBattle, _dKill, _dLabor, _dPass, _dLost, _dFreed, _dStumbles, _stumbles;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("StoryGuard." + where, e); } catch { }
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static bool KeyOn { get { var s = Settings.Current; return s != null && s.StoryGuard; } }

        /// <summary>Bialy Wedrowiec ROT (Hero.IsWhiteWalker: CharacterObject.Race == 1) - ROT sam decyduje o jego smierci, straz go nie dotyka.</summary>
        private static bool Walker(Hero h) { var c = h.CharacterObject; return c != null && c.Race == 1; }

        private static bool Marked(Hero h) { return h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None; }

        private static string EventOf(string term)
        {
            int i = term != null ? term.IndexOf(':') : -1;
            return i >= 0 ? term.Substring(i + 1) : (term ?? "");
        }

        // ---------------------------------------------------------------- cykl zycia
        private static void Wipe()
        {
            _built = _ready = _startSaid = _blindSaid = false;
            _tryHour = int.MinValue; _eventsSeen = -1;
            _memo.Clear(); _why.Clear(); _evObj.Clear(); _byId.Clear(); _passed.Clear(); _blind.Clear(); _vetoSaid.Clear(); _passSaid.Clear(); _errSites.Clear();
            _dBattle = _dKill = _dLabor = _dPass = _dLost = _dFreed = _dStumbles = _stumbles = 0;
        }

        /// <summary>OnGameEnd: pamiec od zera (obiekty kampanii nie zostaja w statykach).</summary>
        internal static void Reset() { _camp = null; Wipe(); }

        /// <summary>OnSessionLaunched (nowa kampania i wczytanie): pamiec od zera. Liste budujemy leniwie przy pierwszym pytaniu albo w dobie - ROT dopisuje swoje
        /// wydarzenia we wlasnym OnSessionLaunched, ktorego kolejnosci wobec naszego nie znamy.</summary>
        internal static void SessionStart()
        {
            _camp = Campaign.Current; Wipe();
            if (!KeyOn) Log.Info(Tag + "klucz NIE - bez ochrony postaci fabuly.");
        }

        /// <summary>true - pamiec zbudowana dla tej kampanii (takze przy niewidocznej liscie: wtedy wszyscy z tabeli maja stan 1, a ponowna proba idzie co godzine gry
        /// i w dobie). false - nie ma kampanii albo budowa padla.</summary>
        private static bool Ensure()
        {
            var c = Campaign.Current;
            if (c == null) return false;
            if (_camp != c) { _camp = c; Wipe(); }
            if (_built && _ready) return true;
            int hr = (int)CampaignTime.Now.ToHours;
            if (hr == _tryHour) return _built;   // 396-p: jedna proba na godzine gry niezaleznie od wyniku (Rebuild, ktory padl przed _built, nie jest ponawiany przy kazdym pytaniu CanDie)
            _tryHour = hr;
            Rebuild();
            return _built;
        }

        /// <summary>Przeliczenie stanu calej tabeli (doba strazy, pierwsze pytanie po wczytaniu, ponowna proba przy niewidocznej liscie).</summary>
        private static void Rebuild()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (_ids.Count == 0)
                {
                    foreach (var id in CanonCompanions.GuardedIds) _ids.Add(id);
                    foreach (var kv in Need) foreach (var id in kv.Value) _ids.Add(id);
                }
                _byId.Clear();
                foreach (var h in Hero.AllAliveHeroes) if (h != null && h.StringId != null && _ids.Contains(h.StringId)) _byId[h.StringId] = h;
                foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.StringId != null && _ids.Contains(h.StringId) && !_byId.ContainsKey(h.StringId)) _byId[h.StringId] = h;

                // 396-p: swiezy odczyt stanu wydarzen - w pamieci dobowej CanonCompanions moze lezec -1 zapisane (BaseTrue / Kronika), zanim ROT dopisal wydarzenia;
                // bez tego pierwsza udana budowa dalaby "nieznany typ wydarzenia" calej tabeli do nastepnej doby. Rebuild jest rzadki (doba, leniwy start, co godzine przy slepej liscie)
                CanonCompanions.ClearEventCache();
                int n = CanonCompanions.EventObjects(_evObj);
                _eventsSeen = n;
                bool ready = n > 0;
                double day = CanonCompanions.StoryDay();
                int total = 0, gone = 0, kept = 0, freed = 0, blind = 0;
                StringBuilder sbKept = null, sbFreed = null;
                bool first = !_startSaid && ready;
                if (first) { sbKept = new StringBuilder(); sbFreed = new StringBuilder(); }
                var old = _built && _ready ? new Dictionary<Hero, int>(_memo) : null;
                _memo.Clear(); _why.Clear(); _blind.Clear();
                foreach (var id in CanonCompanions.GuardedIds)
                {
                    total++;
                    Hero h;
                    if (!_byId.TryGetValue(id, out h) || h == null || !h.IsAlive) { gone++; continue; }
                    if (h == Hero.MainHero) continue;
                    string why = "lista wydarzen ROT niewidoczna"; int st = 1;
                    try { if (ready) st = Compute(h, CanonCompanions.GuardedTerms(id), s, day, out why); }
                    catch (Exception e) { Stumble("Compute", e); st = -1; why = "blad oceny"; }
                    // 396-p: -1 = nie dalo sie ocenic (wyjatek albo nieznany typ wydarzenia ROT): weto gry zostaje otwarte (stan 1, nie chronimy "na zawsze"),
                    // ale nasze wlasne zabojstwa / okaleczenia / wyroki odmawiaja (PLAN-396 rozdz. 0 pkt 4 - nie zabijamy postaci fabuly na slepo)
                    if (st < 0) { st = 1; _blind.Add(h); blind++; why = "NIEOCENIONY: " + why; }
                    _memo[h] = st; _why[h] = why;
                    if (st == 2) { kept++; if (first) sbKept.Append(kept > 1 ? ", " : "").Append(Nm(h)).Append(' ').Append(id).Append(" -> ").Append(why); }
                    else
                    {
                        freed++;
                        if (first) sbFreed.Append(freed > 1 ? ", " : "").Append(Nm(h)).Append(' ').Append(id).Append(" (").Append(why).Append(')');
                        int was;
                        if (old != null && old.TryGetValue(h, out was) && was == 2)
                        {
                            _dFreed++;
                            Log.Info(Tag + "ZWOLNIONY " + Nm(h) + " (" + id + ") - " + why + " | dzien opowiesci " + day.ToString("0", Inv) + ".");
                        }
                    }
                }
                _built = true; _ready = ready;
                if (!ready)
                {
                    if (!_blindSaid)
                    {
                        _blindSaid = true;
                        Log.Info(Tag + "lista wydarzen ROT niewidoczna (" + (n < 0 ? "brak ROT albo odbicie padlo" : "lista pusta - przed startem sesji ROT albo wydarzenia ROT wylaczone")
                                 + ") - weto smierci nie dziala, ponowna proba co godzine gry i w dobie | z tabeli zywych " + _memo.Count + "/" + total + ".");
                    }
                    return;
                }
                if (first)
                {
                    _startSaid = true;
                    Log.Info(Tag + "klucz " + YN(s.StoryGuard) + " (bitwa/starosc " + YN(s.StoryGuardBattleAndAge) + ", egzekucje " + YN(s.StoryGuardExecutions) + ", zawor "
                             + s.StoryGuardGraceDays + ", Roose po Godach " + YN(s.StoryGuardRooseSurvivesRedWedding) + ") | wydarzen ROT widac " + n + " | os Fabuli "
                             + YN(Chronicle.Terms.PlanAvailable) + " | dzien opowiesci " + day.ToString("0", Inv) + " | chronionych " + kept + "/" + total + ": "
                             + (kept > 0 ? sbKept.ToString() : "-") + " | zwolnionych " + freed + ": " + (freed > 0 ? sbFreed.ToString() : "-")
                             + " | nieocenionych (bez weta gry, nasz kod ich nie zabija) " + blind + " | poza swiatem (martwi / brak id) " + gone + ".");
                }
            }
            catch (Exception e) { Stumble("Rebuild", e); }
        }

        /// <summary>Stan bohatera z tabeli przy widocznej liscie wydarzen: 1 zwolniony (why = powod), 2 chroniony (why = na co czeka, z planem) albo
        /// -1 nieoceniony (396-p: nieznany typ wydarzenia ROT - Rebuild zapisuje stan 1 i dopisuje bohatera do _blind).</summary>
        private static int Compute(Hero h, string[] terms, Settings s, double day, out string why)
        {
            why = "";
            if (Marked(h)) { why = "znak smierci " + h.DeathMark; return 1; }
            if (Walker(h)) { why = "Bialy Wedrowiec"; return 1; }
            if (terms == null || terms.Length == 0) { why = "brak termow"; return 1; }
            // (c) term spelniony albo nieznany typ wydarzenia
            foreach (var t in terms)
            {
                string ev = EventOf(t);
                int es = CanonCompanions.EventState(ev);
                if (es < 0) { why = "nieznany typ wydarzenia ROT " + ev + " (term " + t + ")"; return -1; }   // przy widocznej liscie: nie chronimy "na zawsze" (weto gry otwarte), nasz kod odmawia
                if (t.StartsWith("ev:", StringComparison.Ordinal) ? es >= 1 : es == 2) { why = t + " spelnione"; return 1; }
            }
            // (d) niemozliwe i (e) zawor
            int open = 0, planned = 0, overdue = 0;
            double worst = 0;
            string dead = null, wait = null;
            foreach (var t in terms)
            {
                string ev = EventOf(t);
                string who = Blocker(ev, h);
                if (who != null) { dead = (dead != null ? dead + "; " : "") + ev + " niemozliwe: " + who; continue; }
                open++;
                double plan;
                bool has = PlanFor(ev, out plan);
                wait = (wait != null ? wait + " | " : "") + t + (has ? " (plan " + plan.ToString("0", Inv) + ")" : " (bez planu)");
                if (!has) continue;
                planned++;
                if (s.StoryGuardGraceDays > 0 && day > plan + s.StoryGuardGraceDays)
                {
                    overdue++;
                    double late = day - plan - s.StoryGuardGraceDays;
                    if (late > worst) worst = late;
                }
            }
            if (open == 0) { why = dead ?? "termy niemozliwe"; return 1; }
            if (planned > 0 && overdue == planned)
            {
                why = wait + " przeterminowane o " + worst.ToString("0", Inv) + " dni ponad zawor " + s.StoryGuardGraceDays + (dead != null ? "; " + dead : "");
                return 1;
            }
            why = wait;
            return 2;
        }

        /// <summary>Kto z tabeli Need (nie sam h) nie zyje, jest Bialym Wedrowcem albo nie istnieje - opis; null = wydarzenie dalej mozliwe.</summary>
        private static string Blocker(string ev, Hero self)
        {
            string[] need;
            if (!Need.TryGetValue(ev, out need)) return null;
            foreach (var id in need)
            {
                Hero x;
                if (!_byId.TryGetValue(id, out x) || x == null) return id + " nie istnieje";
                if (x == self) continue;
                if (!x.IsAlive) return Nm(x) + " nie zyje";
                if (Walker(x)) return Nm(x) + " jest Bialym Wedrowcem";
            }
            return null;
        }

        private static bool PlanFor(string ev, out double plan)
        {
            if (Chronicle.Terms.PlanDay(ev, out plan)) return true;
            string alt;
            return PlanOf.TryGetValue(ev, out alt) && Chronicle.Terms.PlanDay(alt, out plan);
        }

        /// <summary>Bohater jest ofiara wydarzenia, ktore WLASNIE trwa (IsActive czytane na zywo). name = typ wydarzenia.</summary>
        private static bool VictimNow(Hero h, out string name)
        {
            name = null;
            string id = h.StringId;
            string[] evs;
            if (id == null) return false;
            if (VictimOf.TryGetValue(id, out evs))
                for (int i = 0; i < evs.Length; i++)
                {
                    object o;
                    if (_evObj.TryGetValue(evs[i], out o) && CanonCompanions.EventActiveLive(o)) { name = evs[i]; return true; }
                }
            if (id == RooseId)
            {
                var s = Settings.Current;
                object o;
                if (s != null && !s.StoryGuardRooseSurvivesRedWedding && _evObj.TryGetValue(RedWedding, out o) && CanonCompanions.EventActiveLive(o)) { name = RedWedding; return true; }
            }
            return false;
        }

        // ---------------------------------------------------------------- sluchacze
        /// <summary>CampaignEvents.CanHeroDieEvent. Nigdy nie ustawia true. Blad = bez weta (potkniecie liczone).</summary>
        internal static void CanHeroDie(Hero hero, KillCharacterAction.KillCharacterActionDetail detail, ref bool result)
        {
            try
            {
                if (!result || hero == null) return;
                var s = Settings.Current;
                if (s == null || !s.StoryGuard) return;
                if (!Ensure() || !_ready) return;
                int st;
                if (!_memo.TryGetValue(hero, out st) || st != 2) return;
                if (Marked(hero) || Walker(hero)) return;   // na zywo: znak smierci juz postawiony (smierc po bitwie musi dojsc do konca) albo Bialy Wedrowiec
                switch (detail)
                {
                    case KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge:
                        if (s.StoryGuardBattleAndAge) result = false;   // gra pyta o to co dobe kazdego bohatera - bez linii i bez licznika
                        return;
                    case KillCharacterAction.KillCharacterActionDetail.DiedInBattle:
                    case KillCharacterAction.KillCharacterActionDetail.WoundedInBattle:
                        if (!s.StoryGuardBattleAndAge) return;
                        result = false; _dBattle++; SayVeto(hero, detail);
                        return;
                    case KillCharacterAction.KillCharacterActionDetail.DiedInLabor:
                        if (!s.StoryGuardBattleAndAge) return;
                        result = false; _dLabor++; SayVeto(hero, detail);
                        return;
                    case KillCharacterAction.KillCharacterActionDetail.Murdered:
                    case KillCharacterAction.KillCharacterActionDetail.Executed:
                    case KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent:
                        if (!s.StoryGuardExecutions) return;
                        string ev;
                        if (VictimNow(hero, out ev))
                        {
                            _dPass++; _passed.Add(hero);
                            if (_passSaid.Add(hero)) Log.Info(Tag + "PRZEPUSZCZONE " + Nm(hero) + " (" + hero.StringId + ") przyczyna " + detail + " - ofiara trwajacego " + ev + ".");
                            return;
                        }
                        result = false; _dKill++; SayVeto(hero, detail);
                        return;
                    default:
                        return;   // Lost (usuniecie) i None - bez weta
                }
            }
            catch (Exception e) { Stumble("CanHeroDie", e); }
        }

        /// <summary>Linia WETO raz na bohatera i przyczyne na dobe (sondy CanDie naszych i cudzych modulow, BK co godzine - bez zalewu logu).</summary>
        private static void SayVeto(Hero h, KillCharacterAction.KillCharacterActionDetail detail)
        {
            int bit = 1 << (int)detail, m;
            _vetoSaid.TryGetValue(h, out m);
            if ((m & bit) != 0) return;
            _vetoSaid[h] = m | bit;
            string why;
            _why.TryGetValue(h, out why);
            Log.Info(Tag + "WETO smierci " + Nm(h) + " (" + h.StringId + ") przyczyna " + detail + " - jeszcze przed " + (why ?? "?") + ".");
        }

        /// <summary>CampaignEvents.BeforeHeroKilledEvent (KillCharacterAction.ApplyInternal, juz po pytaniu CanDie): chroniony ginie mimo strazy - droga wymuszona
        /// (isForced) albo usuniecie Lost (DestroyClanAction). Tylko linia UTRATA; niczego nie zatrzymujemy.</summary>
        internal static void BeforeHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (victim == null || !KeyOn || !_built || !_ready || _camp != Campaign.Current) return;
                int st;
                if (!_memo.TryGetValue(victim, out st) || st != 2) return;
                string ev;
                if (_passed.Contains(victim) || Walker(victim) || VictimNow(victim, out ev)) return;
                string why;
                _why.TryGetValue(victim, out why);
                // 396-p: dwa przypadki, ktore NIE sa utrata mimo strazy (falszywe linie UTRATA w zwyklym biegu fabuly):
                // (a) znak smierci postawiony wczesniej (klucz Battle And Age NIE albo znak sprzed ochrony) - sluchacz sam przepuszcza naznaczonych;
                // (b) usuniecie Lost w trakcie wydarzenia ROT - ROT niszczy rod (DestroyClanAction.ApplyByClanLeaderDeath zdejmuje wszystkich poza glowa)
                if (Marked(victim))
                {
                    _dPass++; _memo[victim] = 1;
                    Log.Info(Tag + "ZGON chronionego ze znakiem smierci " + Nm(victim) + " (" + victim.StringId + ") przyczyna " + detail + ", znak " + victim.DeathMark
                             + " (weto bitwy wylaczone kluczem albo znak sprzed ochrony) - " + (why ?? "?") + " moze nie zajsc.");
                    return;
                }
                string run;
                if (detail == KillCharacterAction.KillCharacterActionDetail.Lost && AnyEventActive(out run))
                {
                    _dPass++; _memo[victim] = 1;
                    Log.Info(Tag + "USUNIETY przez wydarzenie ROT " + Nm(victim) + " (" + victim.StringId + ") - Lost w trakcie " + run + " (niszczenie rodu przez wydarzenie); czekal na " + (why ?? "?") + ".");
                    return;
                }
                _dLost++;
                _memo[victim] = 1;
                Log.Info(Tag + "UTRATA chronionego " + Nm(victim) + " (" + victim.StringId + ") przyczyna " + detail + (killer != null ? ", zabojca " + Nm(killer) : "")
                         + " mimo strazy (droga wymuszona albo usuniecie) - " + (why ?? "?") + " moze nie zajsc.");
            }
            catch (Exception e) { Stumble("BeforeHeroKilled", e); }
        }

        /// <summary>396-p: ktorekolwiek wydarzenie ROT wlasnie trwa (IsActive na zywo; do ~40 odczytow, wolane tylko przy smierci chronionego). name = typ.</summary>
        private static bool AnyEventActive(out string name)
        {
            name = null;
            foreach (var kv in _evObj)
                if (CanonCompanions.EventActiveLive(kv.Value)) { name = kv.Key; return true; }
            return false;
        }

        /// <summary>Doba strazy (pierwsza w dobie Armoury): swiezy odczyt wydarzen ROT, przeliczenie tabeli, linia doby tylko gdy cos zaszlo.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                if (_camp != Campaign.Current) { _camp = Campaign.Current; Wipe(); }
                if (!KeyOn) return;
                CanonCompanions.ClearEventCache();
                _tryHour = (int)CampaignTime.Now.ToHours;
                Rebuild();
                int vet = _dBattle + _dKill + _dLabor;
                if (vet + _dPass + _dLost + _dFreed + _dStumbles > 0)
                    Log.Info(Tag + "doba " + ((int)CampaignTime.Now.ToDays) + ": wet " + vet + " (bitwa " + _dBattle + ", egzekucja/mord " + _dKill + ", polog " + _dLabor
                             + "), przepuszczone " + _dPass + ", zwolnieni " + _dFreed + ", utraty " + _dLost + "; potkniecia " + _dStumbles + " (sesja " + _stumbles
                             + ") | chronionych " + CountKept() + ", lista ROT " + (_ready ? "widoczna (" + _eventsSeen + ")" : "NIEWIDOCZNA") + ".");
                _dBattle = _dKill = _dLabor = _dPass = _dLost = _dFreed = _dStumbles = 0;
                _vetoSaid.Clear(); _passSaid.Clear();
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static int CountKept()
        {
            int k = 0;
            foreach (var kv in _memo) if (kv.Value == 2) k++;
            return k;
        }

        // ---------------------------------------------------------------- API (rany, loch, pogrzeby - od 397)
        /// <summary>Lista wydarzen ROT widoczna - stan da sie ocenic. false: nikt nie ma stanu 2 (bohater z tabeli ma 1 z powodem "lista niewidoczna").</summary>
        internal static bool Ready { get { try { return KeyOn && Ensure() && _ready; } catch { return false; } } }

        /// <summary>0 = poza fabula (klucz NIE, null, martwy, gracz, spoza tabeli), 1 = zwolniony, 2 = chroniony. why: powod (1) albo na co czeka (2).</summary>
        internal static int State(Hero h, out string why)
        {
            why = null;
            try
            {
                if (h == null || !h.IsAlive || h == Hero.MainHero || !KeyOn || !Ensure()) return 0;
                int st;
                if (!_memo.TryGetValue(h, out st)) return 0;
                if (st == 2 && Marked(h)) { why = "znak smierci " + h.DeathMark; return 1; }
                if (st == 2 && Walker(h)) { why = "Bialy Wedrowiec"; return 1; }
                _why.TryGetValue(h, out why);
                return st;
            }
            catch (Exception e) { Stumble("State", e); return 0; }
        }

        /// <summary>Fabula jeszcze potrzebuje tego bohatera (stan 2).</summary>
        internal static bool Needed(Hero h) { string w; return State(h, out w) == 2; }

        /// <summary>Opis stanu do logu / komunikatu (null poza tabela).</summary>
        internal static string Why(Hero h) { string w; State(h, out w); return w; }

        /// <summary>Najblizszy planowany dzien opowiesci niezakonczonego termu chronionego bohatera (os Fabuli); -1 gdy brak planu albo bohater nie jest chroniony.</summary>
        internal static double NextNeedDay(Hero h)
        {
            try
            {
                string w;
                if (State(h, out w) != 2) return -1;
                var terms = CanonCompanions.GuardedTerms(h.StringId);
                if (terms == null) return -1;
                double best = -1;
                foreach (var t in terms)
                {
                    string ev = EventOf(t);
                    double plan;
                    if (Blocker(ev, h) != null || !PlanFor(ev, out plan)) continue;
                    if (best < 0 || plan < best) best = plan;
                }
                return best;
            }
            catch (Exception e) { Stumble("NextNeedDay", e); return -1; }
        }

        // ---------------------------------------------------------------- API 403 (HeroFate: rany; od 404 loch, od 409 wyrok)
        /// <summary>403: NASZ kod moze zabic tego bohatera ta przyczyna? NIE dla chronionego (stan 2); NIE dla bohatera z tabeli obsady, gdy listy wydarzen ROT
        /// nie widac (nie zabijamy postaci fabuly "na slepo" - PLAN-396 rozdz. 0 pkt 4); NIE, gdy gra / inny sluchacz CanHeroDie odmawia (Prologue: Robert, Ned).
        /// Klucz Story Guard NIE = pytamy tylko gre. Pytac PRZED jakimkolwiek ksiegowaniem. why: powod odmowy do logu (PL).</summary>
        internal static bool MayDie(Hero h, KillCharacterAction.KillCharacterActionDetail detail, string source, out string why)
        {
            why = null;
            try
            {
                if (h == null || !h.IsAlive) { why = "brak bohatera"; return false; }
                if (!Clear(h, out why)) return false;
                if (!h.CanDie(detail)) { why = "zablokowane (CanHeroDie: gra albo cudzy sluchacz, przyczyna " + detail + ")"; return false; }
                return true;
            }
            catch (Exception e) { Stumble("MayDie", e); why = "blad strazy fabuly"; return false; }
        }

        /// <summary>403: NASZ kod moze trwale okaleczyc tego bohatera? Te same bramki co MayDie z przyczyna WoundedInBattle (chroniony nigdy).</summary>
        internal static bool MayBeMaimed(Hero h, string source)
        {
            try
            {
                string why;
                return h != null && h.IsAlive && Clear(h, out why) && h.CanDie(KillCharacterAction.KillCharacterActionDetail.WoundedInBattle);
            }
            catch (Exception e) { Stumble("MayBeMaimed", e); return false; }
        }

        /// <summary>409: NASZ kod moze trwale wyjac tego bohatera z fabuly (wyrok gracza; pozniej Mur)? Sama bramka strazy, bez pytania gry o CanDie:
        /// NIE dla chronionego (stan 2) i dla postaci z tabeli obsady przy niewidocznej liscie wydarzen ROT. why: powod odmowy do logu (PL).</summary>
        internal static bool MayLeaveStory(Hero h, out string why)
        {
            why = null;
            try
            {
                if (h == null || !h.IsAlive) { why = "brak bohatera"; return false; }
                return Clear(h, out why);
            }
            catch (Exception e) { Stumble("MayLeaveStory", e); why = "blad strazy fabuly"; return false; }
        }

        /// <summary>410: rod, w ktorym zyje ktokolwiek chroniony (stan 2) - spor o dziedzictwo takiego rodu nie rusza (FuneralRites.Succession). Nie rzuca.</summary>
        internal static bool ClanLocked(Clan c)
        {
            try
            {
                if (c == null || !KeyOn || !Ensure()) return false;
                foreach (var kv in _memo)
                {
                    var h = kv.Key;
                    if (kv.Value != 2 || h == null || h.Clan != c || !h.IsAlive) continue;
                    if (Marked(h) || Walker(h)) continue;   // jak State: znak smierci / Bialy Wedrowiec = zwolniony na zywo
                    return true;
                }
                return false;
            }
            catch (Exception e) { Stumble("ClanLocked", e); return false; }
        }

        /// <summary>Bramka strazy dla naszych systemow: false = chroniony, nieoceniony (396-p) albo postac z tabeli przy niewidocznej liscie wydarzen ROT.</summary>
        private static bool Clear(Hero h, out string why)
        {
            why = null;
            if (!KeyOn || h == Hero.MainHero) return true;
            string w;
            if (State(h, out w) == 2) { why = "straz fabuly (" + (w ?? "?") + ")"; return false; }
            if (_blind.Contains(h)) { why = "straz fabuly (" + (w ?? "stan nieoceniony") + ")"; return false; }   // 396-p: blad oceny / nieznany typ wydarzenia - nie zabijamy na slepo
            if (h.StringId != null && CanonCompanions.GuardedTerms(h.StringId) != null && !(Ensure() && _ready))
            { why = "straz fabuly (postac z tabeli obsady, lista wydarzen ROT niewidoczna)"; return false; }
            return true;
        }
    }
}
