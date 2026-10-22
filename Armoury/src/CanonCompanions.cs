using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m101-postacie (Jeff 21.10, doslownie): "bohaterowie Jaime Lannister i Tyrion nie sa bohaterami, zrob ich lordami Lannisterow, tak samo innych, Hodor nie moze
    /// byc bohaterem, bo idzie z Branem i potem umiera, wywal bohaterow, ktorzy sa de facto po stronie i w jakiejs fabule"; po liscie: "A - tak, zgadzam sie przeniesc,
    /// ale pamietaj, ze potem Tyrion zmieni barwy ...; B - ok usun; C - ok do wynajecia; D - ok"; Jeyne Westerling zamiast Talisy - "tak"; "zrob wydarzenia wedlug
    /// ksiazki". Rozpoznanie i mapa rodow: docs/noc-1920/m101-postacie-CHANGELOG.md.
    /// Dane: ModuleData\arm_canon_moves.tsv (#start: wzor ROTwanderer*, grupa A/B/C/D, rod docelowy, druzyna yes/no/later).
    /// TYLKO NOWA KAMPANIA: flaga w zapisie (arm_canon_companions) przy OnNewGameCreated z kluczem CanonCompanionsToClans w tej chwili; stary zapis - nic.
    /// A (krok 2): po utworzeniu wedrowcow przez ROT (ROTSpawnUniqueWanderers.OnNewGameCreated) - zawod Lord (Hero.SetNewOccupation jak gra przy towarzyszu-lordzie),
    ///   rod docelowy (Hero.Clan), z karczmy do siedziby rodu (Leave/EnterSettlementAction jak ROT). Nasz nasluch OnNewGameCreated biegnie przed petla partii lordow
    ///   (OnNewGameCreatedPartialFollowUp) - lord z druzyna "yes" moze dostac partie jak kazdy lord rodu. Ponowna proba przy FollowUpEnd, starcie sesji i w pierwszej
    ///   dobie (gdyby nasz nasluch szedl przed ROT); po pierwszej dobie start zamkniety (brak bohatera / rodu - linia logu).
    ///   Dworzanin ("no" / "later"): nasluch CampaignEvents.CanHeroLeadPartyEvent (false) i postfiks HeroSpawnCampaignBehavior.GetBestAvailableCommander (wynik =
    ///   dworzanin -> null, jak postfiks BK tej metody) - nie dostaje druzyny; gra przenosi bezczynnych lordow po osadach krolestwa
    ///   (HeroSpawnCampaignBehavior.OnHeroDailyTick - glownie miasta rodu), wiec dworzanin jest w miastach swojego krolestwa, nie stale w siedzibie. Dziecko postaci (ten sam Template, z rodzicem) nie jest dworzaninem.
    /// B (krok 3): wzor nie powstaje wcale - prefiks ROT ROTSpawnUniqueWanderers.SpawnHero(CharacterObject) (DeclaredMethod): w fazie tworzenia nowej gry
    ///   (Campaign.CampaignGameLoadingType == NewCampaign i !GameStarted, klucz w tej chwili - ROT tworzy wedrowcow w OnNewGameCreated, mozliwe ze przed naszym
    ///   nasluchem) i potem przy fladze kampanii - wzor B nigdy, wzor A po zamknieciu startu (bohater A, ktory zginal albo zostal usuniety, nie wraca jako wedrowiec).
    ///   Zapas: bohater B, ktory jednak powstal (latka ROT nie wpieta), usuniety przy starcie po cichu (KillCharacterAction.ApplyByRemove - Lost, bez komunikatu).
    ///   CrashScribe UC pyta IsCanonBlocked(wzor) (refleksja) - wzor zablokowany liczy jako "usuniete z kanonu", nie "brak" (UN-5) i nie "ROT go odtworzy".
    /// ZMIANY STRON (krok 4, klucz CanonCompanionsFollowStory): #move w arm_canon_moves.tsv - postac (wzor albo id bohatera), wyzwalacz (ev:/done: wydarzenie ROT -
    ///   odczyt ROTEventBehavior._events jak CrashScribe Fabula.EventState; dead:bohater; war:krolestwo:krolestwo; own:osada:krolestwo; rotday:N - dzien opowiesci
    ///   StoryDay(); prolday:N - dzien prologu, tylko z prologiem; +N dni po pierwszym spelnieniu), akcja (clan / court / lead / prisoner / free / wander / remove / die /
    ///   marry / wed). Raz na doba po starcie; kazda zmiana raz (stan w zapisie); wiersze tej samej postaci po kolei - wczesniejszy bez wyzwalacza odpada, gdy
    ///   pozniejszy zaszedl ("wyprzedzony"), wczesniejszy w toku (bitwa, niewola AI) wstrzymuje pozniejsze (do 60 dob, potem pominiety). Pominiete z linia: postac
    ///   martwa, u gracza (rod, towarzysz, druzyna) albo w niewoli gracza; glowa rodu nie zmienia rodu. Zmiana rodu lorda z druzyna - wzor gry
    ///   MarriageAction.HandleClanChangeAfterMarriageForHero (armia, RemoveTroop, MakeHeroFugitiveAction, DisbandPartyAction.StartDisband - ludzie nie przepadaja).
    ///   Prolog: jedno miejsce do podpiecia - PrologDays() (galaz m101-prolog).
    /// RATUNEK (poprawka po recenzji): ROT niszczy rody DestroyClanAction (RenlyDeath - rod Renly'ego, JeorMutiny - Nocna Straz Jeora, Bastards, StannisDeath), a gra
    ///   usuwa wtedy (Lost) wszystkich czlonkow poza glowa - Brienne, Benjen, Samwell, Yoren znikaliby bez zmian m16/m17/m21. OnClanDestroyedEvent (przed usunieciem
    ///   czlonkow) przenosi postacie kanonu z niszczonego rodu do rodu z #rescue (rod Renly'ego -> Tarth, Nocna Straz Jeora -> Snow); bez wiersza - znikaja z rodem
    ///   (linia logu). Zrodlo kazdego usuniecia (Lost) postaci kanonu - zapis (lost) i CanonRemovedBy dla UC (Armoury albo cudzy kod).
    /// Wyjatek per obiekt - licznik potkniec (CLAUDE.md 7), nic nie gasnie.
    /// m105-kronika K0 (krok A4): wykonanie akcji wydzielone do ApplyAction(Hero, akcja, arg, zrodlo) - wola je Execute (#move) i Kronika (Chronicle.Actions: hide,
    ///   unhide). Nowe akcje hide/unhide (KRONIKA-FORMAT 8, 11, plan P3): hide = LeaveParty + DisableHeroAction (bohater w liscie martwych i wylaczonych gry),
    ///   zakaz dla glowy rodu, wladcy, postaci gracza i listy chronionych przed ich wydarzeniem ROT (FORMAT 15); gubernator, jeniec i bitwa - czeka; rejestr ukrytych
    ///   "hid" w zapisie (klucz postaci, zrodlo, koniec ukrycia - term Kroniki liczony w dobie Kroniki); drugie hide na ukrytym = nowy koniec; unhide tylko z rejestru
    ///   (postac nieukryta przez Armoury - POMINIETA "nie ukryty"), teleport do osady z akcji / siedziby rodu (TeleportHeroAction - stan Active). Wiersze m11/m20/m21
    ///   'remove' -> 'hide' w tym samym commicie. Term kr:NNN.w[=x] w wyzwalaczach #move - stan okien Kroniki (Chronicle.Resolved); klucz ChronicleOfWesteros NIE
    ///   = falsz "nigdy" (FORMAT 3.3, stary wyzwalacz po '|' dziala dalej). Postac ukryta: kolejne wiersze #move czekaja na powrot.
    /// </summary>
    internal static class CanonCompanions
    {
        private const string Tag = "Postacie kanonu (m101)";
        internal const string FileName = "arm_canon_moves.tsv";
        internal const string WandererPrefix = "ROTwanderer";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal sealed class StartRow
        {
            public string Template = "", Group = "", Name = "", Clan = "", Party = "", Note = "";
            public bool Court { get { return Party != "yes"; } }
        }

        internal sealed class MoveRow
        {
            public string Id = "", Who = "", Name = "", Trigger = "", Action = "", Arg = "", Mode = "", Message = "", Note = "";
            public string[] Terms = NoTerms;
        }

        /// <summary>#rescue: rod niszczony przez DestroyClanAction (wydarzenie ROT: RenlyDeath, JeorMutiny...) -> rod, do ktorego przechodza postacie kanonu z niego
        /// (gra usuwa wszystkich czlonkow niszczonego rodu poza glowa - KillCharacterAction.ApplyByRemove, Lost).</summary>
        internal sealed class RescueRow
        {
            public string Clan = "", To = "", Note = "";
        }

        private static readonly string[] NoTerms = new string[0];
        private static readonly HashSet<string> KnownActions = new HashSet<string>(StringComparer.Ordinal)
            { "clan", "court", "lead", "prisoner", "free", "wander", "remove", "die", "marry", "wed", "hide", "unhide" };   // A4: hide/unhide (arg opcjonalny)
        private static readonly HashSet<string> ArgActions = new HashSet<string>(StringComparer.Ordinal)
            { "clan", "court", "prisoner", "free", "wander", "marry", "wed" };
        private const int WaitDays = 60;   // zmiana w toku (bitwa, niewola AI) czeka najwyzej tyle dob, potem pominieta

        // --- dane (raz na proces) ---
        private static bool _tried;
        private static string _note = "nie wczytane";
        private static readonly List<StartRow> _start = new List<StartRow>();
        private static readonly Dictionary<string, StartRow> _byTpl = new Dictionary<string, StartRow>(StringComparer.Ordinal);
        private static int _nA, _nB, _nC, _nD;
        private static readonly List<MoveRow> _moves = new List<MoveRow>();
        private static readonly Dictionary<string, RescueRow> _rescue = new Dictionary<string, RescueRow>(StringComparer.Ordinal);
        private static readonly HashSet<string> _whoIds = new HashSet<string>(StringComparer.Ordinal);   // id bohaterow (nie wzorow) z #move, np. ROTlord_177
        private static int _badRows;

        // --- stan w zapisie (arm_canon_companions) ---
        private static bool _nk;          // kampania zalozona z ta paczka (OnNewGameCreated)
        private static bool _on;          // klucz CanonCompanionsToClans w chwili zalozenia kampanii
        private static bool _startDone;   // start A/B zamkniety
        private static readonly HashSet<string> _conv = new HashSet<string>(StringComparer.Ordinal);       // wzory A przeniesione do rodu
        private static readonly HashSet<string> _startFail = new HashSet<string>(StringComparer.Ordinal);  // wzory A bez bohatera / rodu przy starcie
        private static readonly HashSet<string> _court = new HashSet<string>(StringComparer.Ordinal);      // klucz postaci (wzor albo id bohatera) - bez druzyny
        private static readonly Dictionary<string, string> _done = new Dictionary<string, string>(StringComparer.Ordinal);   // id zmiany -> "dzien|ok" / "dzien|skip"
        private static readonly Dictionary<string, int> _first = new Dictionary<string, int>(StringComparer.Ordinal);      // wyzwalacz (bez +N) -> pierwszy dzien prawdy
        private static readonly Dictionary<string, int> _pend = new Dictionary<string, int>(StringComparer.Ordinal);       // id zmiany w toku -> dzien od ktorego czeka
        private static readonly HashSet<string> _gone = new HashSet<string>(StringComparer.Ordinal);       // rody zniszczone w tej kampanii (OnClanDestroyed; ROT wyjmuje
                                                                                                             // rod Renly'ego i Stannisa z Clan.All - kontrola odwolan)
        private static readonly Dictionary<string, string> _lostBy = new Dictionary<string, string>(StringComparer.Ordinal);  // klucz postaci -> kto usunal (Lost)

        /// <summary>m105-kronika A4: wpis rejestru ukrytych (akcja hide z #move albo z Kroniki) - klucz postaci (wzor oryginalu albo id bohatera), zrodlo
        /// ("#m11", "Kronika 008.1"), koniec ukrycia (term Kroniki; pusty = do unhide albo na zawsze), dzien kampanii ukrycia.</summary>
        internal sealed class HidRow
        {
            public string Key = "", Src = "", End = "";
            public int Day;
        }

        private static readonly Dictionary<string, HidRow> _hid = new Dictionary<string, HidRow>(StringComparer.Ordinal);   // zapis "hid" (niezaleznie od _on)

        // --- sesja ---
        private static int _st, _stDay, _commanderNulled, _commanderNulledDay;
        private static int _spawnBlocked, _spawnBlockedDay, _removedAfter;
        private static readonly HashSet<string> _spawnSaid = new HashSet<string>(StringComparer.Ordinal);
        private static int _dDone, _dSkip, _tDone, _tSkip;
        private static readonly HashSet<string> _waitSaid = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _waitWhy = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _evDay = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _firedBy = new Dictionary<string, string>(StringComparer.Ordinal);   // A4: id zmiany -> pierwszy prawdziwy term (linia)
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static bool _installed, _pCommander, _pSpawn;
        private static string _patchLine = "";
        private static bool _startLineSaid;
        private static string _removingKey, _removingWhy;   // na czas wlasnego KillCharacterAction.ApplyByRemove (zrodlo usuniecia dla UC i linii Lost)
        private static int _rescDay, _rescTotal, _lostOtherDay;

        internal static bool Active { get { return _on; } }

        // --- m104-e5: jency kanonu trzymani do uwolnienia z tabeli (straz niewoli prologu - Prologue.World; bez zapisu - liczone z _done) ---
        // postac, ktorej ostatni wykonany (ok) wiersz to "prisoner", a pozniejszy wiersz "free" tej postaci jeszcze nie zapadl -> id wiersza niewoli (np. p01 Tyrion,
        // p10 Jaime). Mapa liczona w dobie (po Moves) i przy starcie sesji; czytana w latkach Armoury (EndCaptivityAction, okup) - odczyt O(1), watek gry.
        private static readonly Dictionary<Hero, string> _held = new Dictionary<Hero, string>();
        private static readonly Dictionary<Hero, int> _heldDay = new Dictionary<Hero, int>();   // dzien kampanii wykonania wiersza niewoli
        private static readonly Dictionary<Hero, string> _heldArg = new Dictionary<Hero, string>(); // recenzja m104: cel wiersza niewoli (osada albo rod, np. town_EN1)
        private static Hero _freeing;   // na czas wlasnej akcji "free" (EndCaptivityAction.ApplyByReleasedByChoice) - straz przepuszcza

        /// <summary>m104-e5: id wiersza niewoli, w ktorej postac jest trzymana do wiersza "free" (null - nie trzymana).</summary>
        internal static string HeldRow(Hero h)
        {
            string r;
            return h != null && _held.Count > 0 && _held.TryGetValue(h, out r) ? r : null;
        }

        /// <summary>m104-e5: trwa nasze uwolnienie tej postaci (akcja "free") - straz niewoli prologu nie blokuje.</summary>
        internal static bool Freeing(Hero h) { return h != null && ReferenceEquals(h, _freeing); }

        /// <summary>m104-e5: dzien kampanii, w ktorym wiersz niewoli trzymanej postaci zostal wykonany (-1 - nie trzymana).</summary>
        internal static int HeldDay(Hero h)
        {
            int d;
            return h != null && _heldDay.Count > 0 && _heldDay.TryGetValue(h, out d) ? d : -1;
        }

        /// <summary>Recenzja m104: cel wiersza niewoli trzymanej postaci (argument akcji "prisoner": osada albo rod; null - nie trzymana). Straz prologu trzyma
        /// tylko w lochu tej osady (p01 town_EN1, p10 town_V2) - po ucieczce przez upadek osady i ponownym pojmaniu cudzy loch nie trzyma.</summary>
        internal static string HeldArg(Hero h)
        {
            string a;
            return h != null && _heldArg.Count > 0 && _heldArg.TryGetValue(h, out a) ? a : null;
        }

        internal static int HeldCount { get { return _held.Count; } }

        /// <summary>m104-e5: postac kanonu z wzoru ROTwanderer* (oryginal bez rodzicow) - dla scen prologu (orszak, turniej).</summary>
        internal static Hero TemplateHero(string tpl) { try { return FindOriginal(tpl); } catch { return null; } }

        private static void RebuildHeld()
        {
            _held.Clear(); _heldDay.Clear(); _heldArg.Clear();
            if (!_on || !_startDone || _moves.Count == 0) return;
            try
            {
                var byWho = new Dictionary<string, List<MoveRow>>(StringComparer.Ordinal);
                foreach (var r in _moves)
                {
                    List<MoveRow> l;
                    if (!byWho.TryGetValue(r.Who, out l)) { l = new List<MoveRow>(); byWho[r.Who] = l; }
                    l.Add(r);
                }
                foreach (var kv in byWho)
                {
                    var l = kv.Value;
                    int last = -1;
                    for (int j = 0; j < l.Count; j++)
                    {
                        string v;
                        if (_done.TryGetValue(l[j].Id, out v) && v.EndsWith("|ok", StringComparison.Ordinal)) last = j;
                    }
                    if (last < 0 || l[last].Action != "prisoner") continue;
                    bool freeLater = false;
                    for (int j = last + 1; j < l.Count; j++) if (l[j].Action == "free" && !_done.ContainsKey(l[j].Id)) { freeLater = true; break; }
                    if (!freeLater) continue;
                    var h = WhoHero(l[last]);
                    if (h == null || !h.IsAlive || !h.IsPrisoner) continue;
                    _held[h] = l[last].Id;
                    _heldArg[h] = l[last].Arg;
                    string dv; int dd;
                    _heldDay[h] = _done.TryGetValue(l[last].Id, out dv) && int.TryParse(dv.Split('|')[0], NumberStyles.Integer, Inv, out dd) ? dd : Day();
                }
            }
            catch (Exception e) { Stumble("RebuildHeld", e); }
        }

        /// <summary>Konstruktor zachowania (kazda kampania: nowa i wczytana) - stan od zera; dane z pliku zostaja.</summary>
        internal static void Reset()
        {
            _nk = _on = _startDone = false;
            _conv.Clear(); _startFail.Clear(); _court.Clear();
            _st = _stDay = _commanderNulled = _commanderNulledDay = 0;
            _spawnBlocked = _spawnBlockedDay = _removedAfter = 0;
            _spawnSaid.Clear();
            _done.Clear(); _first.Clear(); _pend.Clear(); _gone.Clear(); _lostBy.Clear();
            _hid.Clear(); _firedBy.Clear();   // m105-kronika A4: ukryci (zapis) i term, ktory zaszedl (linia logu)
            _removingKey = _removingWhy = null;
            _held.Clear(); _heldDay.Clear(); _heldArg.Clear(); _freeing = null;   // m104-e5: jency kanonu trzymani do wiersza "free"
            _rescDay = _rescTotal = _lostOtherDay = 0;
            _dDone = _dSkip = _tDone = _tSkip = 0;
            _waitSaid.Clear(); _waitWhy.Clear(); _evDay.Clear();
            _evBeh = null; _evCamp = null;
            lock (_errSites) _errSites.Clear();
            _startLineSaid = false;
        }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first || _st % 200 == 0) Log.Error("CanonCompanions." + where + " (potkniecie " + _st + ")", e);
        }

        // ------------------------------------------------------------ plik
        private static void EnsureLoaded()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                string path = MapVillageTooltip.ModulePath("ModuleData", FileName);
                if (path == null || !System.IO.File.Exists(path)) { _note = "brak pliku " + FileName; return; }
                Parse(System.IO.File.ReadAllText(path, Encoding.UTF8));
                _note = "";
            }
            catch (Exception e)
            {
                _start.Clear(); _byTpl.Clear(); _moves.Clear(); _rescue.Clear(); _whoIds.Clear();
                _note = "blad wczytania (" + e.GetType().Name + ")";
                Log.Error("CanonCompanions.Load", e);
            }
        }

        private static void Parse(string text)
        {
            _start.Clear(); _byTpl.Clear(); _moves.Clear(); _rescue.Clear(); _whoIds.Clear();
            _nA = _nB = _nC = _nD = 0; _badRows = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            string section = null;
            var col = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("# ", StringComparison.Ordinal) || line == "#") continue;
                var cells = line.Split('\t');
                if (cells[0].StartsWith("#", StringComparison.Ordinal))
                {
                    section = cells[0].Substring(1);
                    col.Clear();
                    for (int i = 1; i < cells.Length; i++) col[cells[i].Trim()] = i - 1;
                    continue;
                }
                if (section == "start")
                {
                    var r = new StartRow
                    {
                        Template = Cell(cells, col, "template"), Group = Cell(cells, col, "group"), Name = Cell(cells, col, "name"),
                        Clan = Cell(cells, col, "clan"), Party = Cell(cells, col, "party"), Note = Cell(cells, col, "note")
                    };
                    if (r.Template.Length == 0 || _byTpl.ContainsKey(r.Template)) continue;
                    _start.Add(r);
                    _byTpl[r.Template] = r;
                    if (r.Group == "A") _nA++; else if (r.Group == "B") _nB++; else if (r.Group == "C") _nC++; else if (r.Group == "D") _nD++;
                }
                else if (section == "move")
                {
                    var m = new MoveRow
                    {
                        Id = Cell(cells, col, "id"), Who = Cell(cells, col, "who"), Name = Cell(cells, col, "name"), Trigger = Cell(cells, col, "trigger"),
                        Mode = Cell(cells, col, "mode"), Message = Cell(cells, col, "message"), Note = Cell(cells, col, "note")
                    };
                    string act = Cell(cells, col, "action");
                    int c = act.IndexOf(':');
                    m.Action = c < 0 ? act : act.Substring(0, c);
                    m.Arg = c < 0 ? "" : act.Substring(c + 1);
                    var terms = new List<string>();
                    foreach (var t in m.Trigger.Split('|')) { var tt = t.Trim(); if (tt.Length > 0) terms.Add(tt); }
                    m.Terms = terms.ToArray();
                    bool ok = m.Id.Length > 0 && ids.Add(m.Id) && m.Who.Length > 0 && KnownActions.Contains(m.Action) && m.Terms.Length > 0
                              && (!ArgActions.Contains(m.Action) || m.Arg.Length > 0) && (m.Mode.Length == 0 || m.Mode == "prolog" || m.Mode == "classic");
                    if (!ok) { _badRows++; continue; }
                    _moves.Add(m);
                    if (!m.Who.StartsWith(WandererPrefix, StringComparison.Ordinal)) _whoIds.Add(m.Who);
                }
                else if (section == "rescue")
                {
                    var q = new RescueRow { Clan = Cell(cells, col, "clan"), To = Cell(cells, col, "to"), Note = Cell(cells, col, "note") };
                    if (q.Clan.Length == 0 || q.To.Length == 0 || q.Clan == q.To || _rescue.ContainsKey(q.Clan)) { _badRows++; continue; }
                    _rescue[q.Clan] = q;
                }
            }
        }

        private static string Cell(string[] cells, Dictionary<string, int> col, string name)
        {
            int i;
            if (!col.TryGetValue(name, out i) || i < 0 || i >= cells.Length) return "";
            return (cells[i] ?? "").Trim();
        }

        // ------------------------------------------------------------ latki (raz na proces, SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot)
        internal static void ApplyAll(Harmony h)
        {
            if (_installed) return;
            _installed = true;
            var parts = new List<string>();
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(HeroSpawnCampaignBehavior), "GetBestAvailableCommander", new[] { typeof(Clan) });
                if (m != null)
                {
                    try { h.Patch(m, postfix: new HarmonyMethod(typeof(CanonCompanions), nameof(CommanderPostfix))); _pCommander = true; }
                    catch (Exception e) { Stumble("ApplyAll(GetBestAvailableCommander)", e); }
                }
                parts.Add("dowodca partii (dworzanie bez druzyny) " + (_pCommander ? "PODPIETY" : "BRAK (HeroSpawnCampaignBehavior.GetBestAvailableCommander)"));
                var tRot = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTSpawnUniqueWanderers");
                var mSpawn = tRot != null ? AccessTools.DeclaredMethod(tRot, "SpawnHero", new[] { typeof(CharacterObject) }) : null;
                if (mSpawn != null)
                {
                    try { h.Patch(mSpawn, prefix: new HarmonyMethod(typeof(CanonCompanions), nameof(SpawnPrefix))); _pSpawn = true; }
                    catch (Exception e) { Stumble("ApplyAll(ROT SpawnHero)", e); }
                }
                parts.Add("tworzenie wedrowcow ROT (B nie powstaja) " + (_pSpawn ? "PODPIETE" : "BRAK (" + (tRot == null ? "ROT nieobecny" : "ROTSpawnUniqueWanderers.SpawnHero") + ")"));
            }
            catch (Exception e) { Stumble("ApplyAll", e); parts.Add("wyjatek " + e.GetType().Name); }
            _patchLine = string.Join(", ", parts.ToArray());
            Log.Info(Tag + ": wpiecie | " + _patchLine + ".");
        }

        // ------------------------------------------------------------ rozpoznanie postaci
        internal static bool IsRotTemplate(CharacterObject t)
        {
            return t != null && t.StringId != null && t.StringId.StartsWith(WandererPrefix, StringComparison.Ordinal);
        }

        /// <summary>Postac z wzoru ROT bez rodzicow (dziecko ma ten sam Template - jak UC IsOriginal).</summary>
        internal static bool IsOriginal(Hero h)
        {
            return h != null && IsRotTemplate(h.Template) && h.Father == null && h.Mother == null;
        }

        /// <summary>Zywy albo zmarly oryginal z wzoru (najpierw zywy).</summary>
        private static Hero FindOriginal(string tpl)
        {
            foreach (var h in Hero.AllAliveHeroes) if (h != null && h.Father == null && h.Mother == null && h.Template != null && h.Template.StringId == tpl) return h;
            foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.Father == null && h.Mother == null && h.Template != null && h.Template.StringId == tpl) return h;
            return null;
        }

        /// <summary>Ktokolwiek (zywy albo zmarly, takze dziecko) z tym wzorem - ten sam warunek co ROT SpawnHero (AllHeroes.AnyQ(Template == wzor)).</summary>
        private static bool AnyWithTemplate(string tpl)
        {
            foreach (var h in Hero.AllAliveHeroes) if (h != null && h.Template != null && h.Template.StringId == tpl) return true;
            foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.Template != null && h.Template.StringId == tpl) return true;
            return false;
        }

        private static string Key(Hero h)
        {
            return IsOriginal(h) ? h.Template.StringId : (h != null ? h.StringId : "");
        }

        private static Clan FindClan(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in Clan.All) if (c != null && c.StringId == id) return c;
            return null;
        }

        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }

        private static int Day() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

        private static string Where(Hero h)
        {
            try
            {
                if (h.PartyBelongedToAsPrisoner != null) return "jeniec " + h.PartyBelongedToAsPrisoner.Name;
                if (h.PartyBelongedTo != null) return "druzyna " + h.PartyBelongedTo.Name;
                if (h.CurrentSettlement != null) return Nm(h.CurrentSettlement);
                return "bez miejsca (" + h.HeroState + ")";
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ dworzanie (bez druzyny)
        internal static bool IsCourtier(Hero h)
        {
            if (!_on || _court.Count == 0 || h == null) return false;
            // w rodzie gracza (np. po slubie) albo jako jego towarzysz - o druzynie decyduje gracz, znacznik dworzanina nie dziala (recenzja)
            var pc = Clan.PlayerClan;
            if (pc != null && (h.Clan == pc || h.CompanionOf == pc)) return false;
            return _court.Contains(Key(h));
        }

        private static void SetCourt(Hero h, bool court)
        {
            string k = Key(h);
            if (k.Length == 0) return;
            if (court) _court.Add(k); else _court.Remove(k);
        }

        /// <summary>CampaignEvents.CanHeroLeadPartyEvent: dworzanin kanonu nie prowadzi druzyny (1. przejscie GetBestAvailableCommander i inne pytania gry).</summary>
        internal static void CanLead(Hero hero, ref bool result)
        {
            try { if (result && IsCourtier(hero)) result = false; }
            catch (Exception e) { Stumble("CanLead", e); }
        }

        /// <summary>Postfiks HeroSpawnCampaignBehavior.GetBestAvailableCommander: 2. przejscie gry (rody AI) nie pyta CanLeadParty - dworzanin -> null (jak BK).</summary>
        public static void CommanderPostfix(ref Hero __result)
        {
            try
            {
                if (__result != null && IsCourtier(__result)) { __result = null; _commanderNulled++; _commanderNulledDay++; }
            }
            catch (Exception e) { Stumble("CommanderPostfix", e); }
        }

        // ------------------------------------------------------------ B: wzor nie powstaje
        /// <summary>Faza blokady: flaga kampanii albo trwa tworzenie nowej gry z kluczem (ROT moze tworzyc wedrowcow przed naszym OnNewGameCreated).</summary>
        private static bool BlockPhase()
        {
            if (_on) return true;
            var c = Campaign.Current;
            if (c == null || c.GameStarted || c.CampaignGameLoadingType != Campaign.GameLoadingType.NewCampaign) return false;
            var s = Settings.Current;
            return s != null && s.CanonCompanionsToClans;
        }

        private static bool Blocked(StartRow r)
        {
            return r != null && (r.Group == "B" || (r.Group == "A" && _startDone));
        }

        /// <summary>Dla CrashScribe UC (refleksja, Armoury.CanonCompanions.IsCanonBlocked): wzor, ktorego ROT nie odtworzy w tej kampanii (B zawsze, A po starcie).</summary>
        internal static bool IsCanonBlocked(string templateId)
        {
            try
            {
                if (!_on || templateId == null) return false;
                EnsureLoaded();
                StartRow r;
                return _byTpl.TryGetValue(templateId, out r) && Blocked(r);
            }
            catch { return false; }
        }

        /// <summary>Prefiks ROT ROTSpawnUniqueWanderers.SpawnHero: wzor B (zawsze) i A (po starcie) nie powstaje.</summary>
        public static bool SpawnPrefix(CharacterObject __0)
        {
            try
            {
                if (__0 == null || __0.StringId == null || !BlockPhase()) return true;
                EnsureLoaded();
                StartRow r;
                if (!_byTpl.TryGetValue(__0.StringId, out r) || !Blocked(r)) return true;
                // ROT SpawnHero tworzy tylko, gdy nikt (zywy ani zmarly) nie ma tego wzoru - wtedy nic nie blokujemy (bez falszywej linii i licznika; recenzja)
                if (AnyWithTemplate(__0.StringId)) return true;
                _spawnBlocked++; _spawnBlockedDay++;
                if (_spawnSaid.Add(__0.StringId))
                    Log.Info(Tag + ": ROT chcial stworzyc " + r.Name + " [" + __0.StringId + "] (grupa " + r.Group + (r.Group == "B" ? " - usuniety z kanonu" : " - postac kanonu juz byla w swiecie")
                             + ") - nie powstaje (linia raz na sesje na wzor; ROT pyta co dobe).");
                return false;
            }
            catch (Exception e) { Stumble("SpawnPrefix", e); return true; }
        }

        // ------------------------------------------------------------ zniszczenie rodu (ratunek) i zrodlo usuniec
        /// <summary>Postac kanonu: oryginal wzoru A/B albo bohater z kolumny who tabeli zmian (np. Jeyne Westerling).</summary>
        private static bool IsCanonHero(Hero h)
        {
            if (h == null) return false;
            if (IsOriginal(h))
            {
                StartRow r;
                return _byTpl.TryGetValue(h.Template.StringId, out r) && (r.Group == "A" || r.Group == "B");
            }
            return h.StringId != null && _whoIds.Contains(h.StringId);
        }

        private static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '); }

        /// <summary>Wlasne usuniecie (Lost, bez komunikatu) ze znacznikiem zrodla - HeroKilled i UC (CanonRemovedBy) wiedza, ze to Armoury.</summary>
        private static void RemoveQuiet(Hero h, string why)
        {
            _removingKey = Key(h);
            _removingWhy = "Armoury m101: " + why;
            try { KillCharacterAction.ApplyByRemove(h); }   // Lost, bez komunikatu (showNotification false)
            finally { _removingKey = null; _removingWhy = null; }
        }

        /// <summary>Dla CrashScribe UC (refleksja, Armoury.CanonCompanions.CanonRemovedBy): kto usunal postac kanonu (klucz = wzor oryginalu albo id bohatera);
        /// null = nie wiadomo (UC sam opisuje zrodlo wedlug stanu rodu).</summary>
        internal static string CanonRemovedBy(string key)
        {
            try
            {
                if (!_on || key == null) return null;
                if (_removingKey != null && _removingKey == key) return _removingWhy;
                string s;
                return _lostBy.TryGetValue(key, out s) ? s : null;
            }
            catch { return null; }
        }

        /// <summary>CampaignEvents.OnClanDestroyedEvent - idzie na poczatku DestroyClanAction.ApplyInternal, PRZED usunieciem czlonkow rodu (KillCharacterAction.ApplyByRemove
        /// kazdego zywego poza glowa). ROT niszczy tak rod Renly'ego (RenlyDeathEvent - przed zabiciem Renly'ego), Jeora Mormonta (JeorMutinyEvent), Boltonow albo Snow
        /// (BastardsEvent) i Stannisa (StannisDeathEvent). Postac kanonu z niszczonego rodu przechodzi do rodu z #rescue (jak przy zmianie strony); bez wiersza - znika z rodem.</summary>
        internal static void OnClanDestroyed(Clan c)
        {
            try
            {
                if (!_on || c == null) return;
                EnsureLoaded();
                if (c.StringId != null) _gone.Add(c.StringId);
                RescueRow q;
                _rescue.TryGetValue(c.StringId ?? "", out q);
                Clan to = q != null ? FindClan(q.To) : null;
                var list = new List<Hero>();
                foreach (var h in c.Heroes) if (h != null && h.IsAlive && IsCanonHero(h)) list.Add(h);
                foreach (var h in list)
                {
                    try { RescueOne(c, h, q, to); }
                    catch (Exception e) { Stumble("OnClanDestroyed(" + Key(h) + ")", e); }
                }
            }
            catch (Exception e) { Stumble("OnClanDestroyed", e); }
        }

        private static void RescueOne(Clan c, Hero h, RescueRow q, Clan to)
        {
            string head = Tag + ": rod " + Nm(c) + " (" + c.StringId + ") niszczony (DestroyClanAction - wydarzenie ROT albo gra) - " + Nm(h) + " [" + Key(h) + "] " + Where(h);
            if (c.Leader == h) { Log.Info(head + " - glowa rodu, ratunek pominiety (o glowie decyduje gra)."); return; }
            if (q == null) { Log.Info(head + " - bez ratunku w tabeli #rescue: postac znika z rodem (Lost)."); return; }
            if (to == null || to.IsEliminated || to == c || to == Clan.PlayerClan)
            {
                Log.Info(head + " - ratunek NIEMOZLIWY: rod " + q.To + (to == null ? " nie istnieje" : to == Clan.PlayerClan ? " to rod gracza" : " wyeliminowany") + " - postac znika z rodem (Lost).");
                return;
            }
            if (Busy(h)) { Log.Info(head + " - ratunek NIEMOZLIWY: w bitwie albo oblezeniu - postac znika z rodem (Lost)."); return; }
            string before = Side(h);
            string why;
            bool hidden = IsHidden(h);
            if (Captive(h) || hidden)
            {
                h.Clan = to;   // jeniec zostaje w niewoli, ukryty (A4) zostaje ukryty - tylko rod (bez teleportu, ktory przywrocilby ukrytego)
                h.IsMinorFactionHero = to.IsMinorFaction;
                why = hidden ? "ukryty - zostaje ukryty" : "jeniec - zostaje w niewoli";
            }
            else
            {
                var res = MoveToClan(h, to, IsCourtier(h), out why);
                if (res != Res.Done) { Log.Info(head + " - ratunek NIEMOZLIWY: " + why + " - postac znika z rodem (Lost)."); return; }
            }
            _rescDay++; _rescTotal++;
            Log.Info(Tag + ": ratunek - " + Nm(h) + " [" + Key(h) + "] " + before + " -> " + Side(h) + " | rod " + Nm(c) + " (" + c.StringId
                     + ") niszczony (DestroyClanAction - wydarzenie ROT albo gra), #rescue " + q.Clan + " -> " + q.To + (why.Length > 0 ? " (" + why + ")" : "")
                     + " | dzien " + Day() + ".");
        }

        /// <summary>CampaignEvents.HeroKilledEvent: usuniecie (Lost) postaci kanonu - zrodlo do zapisu (UC, linia doby); cudze usuniecie ma linie logu.</summary>
        internal static void HeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (!_on || victim == null || detail != KillCharacterAction.KillCharacterActionDetail.Lost) return;
                EnsureLoaded();
                if (!IsCanonHero(victim)) return;
                string k = Key(victim);
                string src;
                if (_removingKey != null && _removingKey == k) src = _removingWhy;
                else
                {
                    var c = victim.Clan;
                    src = c != null && c.IsEliminated ? "zniszczenie rodu " + Nm(c) + " (" + c.StringId + ", DestroyClanAction - wydarzenie ROT albo gra)" : "inny kod (gra albo inny mod)";
                    _lostOtherDay++;
                    Log.Info(Tag + ": postac kanonu usunieta (Lost) NIE przez Armoury - " + Nm(victim) + " [" + k + "] przez " + src + ", rod " + Nm(c) + " | dzien " + Day() + ".");
                }
                _lostBy[k] = Clean(src);
            }
            catch (Exception e) { Stumble("HeroKilled", e); }
        }

        // ------------------------------------------------------------ start kampanii
        /// <summary>OnNewGameCreatedEvent: flaga nowej kampanii i klucz w tej chwili (McmSettings.Apply byl w OnGameStart); start A.</summary>
        internal static void OnNewGame()
        {
            try
            {
                _nk = true;
                var s = Settings.Current;
                _on = s != null && s.CanonCompanionsToClans;
                if (!_on) return;
                EnsureLoaded();
                StartPass("OnNewGameCreated", false);
            }
            catch (Exception e) { Stumble("OnNewGame", e); }
        }

        /// <summary>OnNewGameCreatedPartialFollowUpEndEvent: ponowna proba (gdyby ROT tworzyl wedrowcow po nas).</summary>
        internal static void AfterNewGame()
        {
            try { if (_on && !_startDone) StartPass("FollowUpEnd", false); }
            catch (Exception e) { Stumble("AfterNewGame", e); }
        }

        internal static void SessionStart()
        {
            try
            {
                EnsureLoaded();   // maly plik - linia startu z licznikami takze w starym zapisie
                if (_on && !_startDone) StartPass("start sesji", false);
                RebuildHeld();   // m104-e5: po wczytaniu - jency kanonu trzymani (straz niewoli prologu) od pierwszej godziny
                StartLine();
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static void StartLine()
        {
            if (_startLineSaid) return;
            _startLineSaid = true;
            var s = Settings.Current;
            int clansWant = 0, clansFound = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (var r in _start)
            {
                if (r.Group != "A" || r.Clan.Length == 0 || !seen.Add(r.Clan)) continue;
                clansWant++;
                if (ClanKnown(r.Clan)) clansFound++; else missing.Add(r.Clan);
            }
            foreach (var q in _rescue.Values)   // #rescue: niszczony rod i rod ratunku
                foreach (var id in new[] { q.Clan, q.To })
                {
                    if (!seen.Add(id)) continue;
                    clansWant++;
                    if (ClanKnown(id)) clansFound++; else missing.Add(id);
                }
            // rody, osady, bohaterowie i krolestwa z tabeli zmian stron (cel akcji i wyzwalacze)
            int refsWant = 0, refsFound = 0;
            var seenRef = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in _moves)
            {
                if ((m.Action == "clan" || m.Action == "court" || m.Action == "free") && seen.Add(m.Arg))
                {
                    clansWant++;
                    if (ClanKnown(m.Arg)) clansFound++; else missing.Add(m.Arg);
                }
                if (m.Action == "prisoner" && seenRef.Add("p:" + m.Arg))
                {
                    refsWant++;
                    if (Settlement.Find(m.Arg) != null || ClanKnown(m.Arg)) refsFound++; else missing.Add(m.Arg);
                }
                if ((m.Action == "marry" || m.Action == "wed") && seenRef.Add("h:" + m.Arg))
                {
                    refsWant++;
                    if (FindHeroId(m.Arg) != null) refsFound++; else missing.Add(m.Arg);
                }
                if (!m.Who.StartsWith(WandererPrefix, StringComparison.Ordinal) && seenRef.Add("h:" + m.Who))
                {
                    refsWant++;
                    if (FindHeroId(m.Who) != null) refsFound++; else missing.Add(m.Who);
                }
                foreach (var t in m.Terms)
                {
                    string bas = t;
                    int i = t.LastIndexOf('+');
                    if (i > 0) bas = t.Substring(0, i);
                    var q = bas.Split(':');
                    if (q.Length < 2 || !seenRef.Add("t:" + bas)) continue;
                    bool ok;
                    if (q[0] == "dead") ok = FindHeroId(q[1]) != null;
                    else if (q[0] == "war") ok = q.Length > 2 && FindKingdom(q[1]) != null && FindKingdom(q[2]) != null;
                    else if (q[0] == "own") ok = q.Length > 2 && Settlement.Find(q[1]) != null && FindKingdom(q[2]) != null;
                    else if (q[0] == "ev" || q[0] == "done") ok = AccessTools.TypeByName("ROT.Events." + q[1]) != null;
                    else continue;
                    refsWant++;
                    if (ok) refsFound++; else missing.Add(bas);
                }
            }
            Log.Info(Tag + ": start | klucz CanonCompanionsToClans " + YN(s != null && s.CanonCompanionsToClans) + " (w chwili zalozenia kampanii: "
                     + (_nk ? YN(_on) : "stary zapis - nic") + "), zmiany stron CanonCompanionsFollowStory " + YN(s != null && s.CanonCompanionsFollowStory)
                     + " | plik " + (_note.Length == 0 ? "wczytany" : _note) + " | A " + _nA + ", B " + _nB + ", C " + _nC + ", D " + _nD
                     + " | zmian stron w tabeli " + _moves.Count + " (zlych wierszy " + _badRows + "), wykonanych " + CountDone("ok") + ", pominietych " + CountDone("skip")
                     + ", w toku " + _pend.Count + " | prolog " + YN(IsProlog()) + ", dzien opowiesci " + StoryDay().ToString("0", Inv)
                     + " | mapa rodow znaleziona " + clansFound + "/" + clansWant + ", odwolania zmian (osady, postacie, krolestwa, wydarzenia ROT) " + refsFound + "/" + refsWant
                     + (missing.Count > 0 ? " BRAK: " + string.Join(", ", missing.ToArray()) : "") + " | przeniesieni " + _conv.Count + ", bez rodu/bohatera " + _startFail.Count
                     + ", dworzan " + _court.Count + ", start " + (_startDone ? "zamkniety" : "otwarty") + " | B zablokowane przy tworzeniu " + _spawnBlocked
                     + ", usuniete po utworzeniu " + _removedAfter + " | ratunek przy zniszczeniu rodu (#rescue) " + _rescue.Count + " wierszy, rody zniszczone w kampanii "
                     + _gone.Count + (_gone.Count > 0 ? " [" + string.Join(", ", new List<string>(_gone).ToArray()) + "]" : "") + ", postacie kanonu usuniete (Lost) " + _lostBy.Count
                     + " | latki: " + _patchLine + ".");
        }

        /// <summary>Rod z tabeli: jest w Clan.All albo zniszczony w tej kampanii (ROT wyjmuje rod Renly'ego i Stannisa z listy rodow) albo nadal w MBObjectManager.</summary>
        private static bool ClanKnown(string id)
        {
            if (FindClan(id) != null || _gone.Contains(id)) return true;
            try { return TaleWorlds.ObjectSystem.MBObjectManager.Instance != null && TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<Clan>(id) != null; }
            catch { return false; }
        }

        private static string YN(bool b) { return b ? "tak" : "nie"; }

        /// <summary>Jedno przejscie startu: A do rodow. final = pierwsza doba (brak bohatera = linia, start zamkniety).</summary>
        private static void StartPass(string where, bool final)
        {
            int wait = 0;
            foreach (var r in _start)
            {
                if (r.Group != "A" || _conv.Contains(r.Template) || _startFail.Contains(r.Template)) continue;
                try
                {
                    var h = FindOriginal(r.Template);
                    if (h == null || !h.IsAlive)
                    {
                        if (final)
                        {
                            _startFail.Add(r.Template);
                            Log.Info(Tag + ": przeniesienie POMINIETE - " + r.Name + " [" + r.Template + "] - " + (h == null ? "BRAK bohatera z wzoru" : "martwy") + " (" + where + ").");
                        }
                        else wait++;
                        continue;
                    }
                    var clan = FindClan(r.Clan);
                    if (clan == null || clan.IsEliminated)
                    {
                        _startFail.Add(r.Template);
                        Log.Info(Tag + ": przeniesienie POMINIETE - " + Nm(h) + " [" + r.Template + "] - BRAK rodu " + r.Clan + (clan != null ? " (wyeliminowany)" : "") + " - zostaje wedrowcem.");
                        continue;
                    }
                    if (h.CompanionOf != null || h.Clan != null)
                    {
                        _startFail.Add(r.Template);
                        Log.Info(Tag + ": przeniesienie POMINIETE - " + Nm(h) + " [" + r.Template + "] - juz w rodzie " + Nm(h.Clan) + " (" + where + ").");
                        continue;
                    }
                    string from = Where(h);
                    ToLord(h, clan, r.Court);
                    _conv.Add(r.Template);
                    Log.Info(Tag + ": przeniesienie - " + Nm(h) + " [" + r.Template + "] wedrowiec w " + from + " -> rod " + Nm(clan) + " (" + clan.StringId + ", "
                             + (clan.Kingdom != null ? clan.Kingdom.StringId : "bez krolestwa") + "), lord, druzyna: " + (r.Party == "yes" ? "tak" : r.Party == "later" ? "pozniej" : "nie (dworzanin)")
                             + ", teraz " + Where(h) + " | wyzwalacz start (" + where + ").");
                }
                catch (Exception e) { Stumble("StartPass(" + r.Template + ")", e); }
            }
            foreach (var r in _start)
            {
                if (r.Group != "B") continue;
                try
                {
                    var h = FindOriginal(r.Template);
                    if (h == null || !h.IsAlive || h.CompanionOf != null || h.Clan != null) continue;
                    string from = Where(h);
                    RemoveQuiet(h, "grupa B usunieta po utworzeniu (start)");   // Lost, bez komunikatu
                    _removedAfter++;
                    Log.Info(Tag + ": B usuniety po utworzeniu - " + Nm(h) + " [" + r.Template + "] w " + from + " (latka ROT " + (_pSpawn ? "wpieta - powstal przed nasza latka" : "NIE wpieta")
                             + "; Lost, bez komunikatu) | wyzwalacz start (" + where + ").");
                }
                catch (Exception e) { Stumble("StartPass.B(" + r.Template + ")", e); }
            }
            if (wait == 0 || final) _startDone = true;
        }

        /// <summary>Wedrowiec / postac bez rodu -> lord rodu: zawod (zdarzenie gry HeroOccupationChanged - gra zdejmuje go z listy zywych wedrowcow), rod, siedziba.</summary>
        private static void ToLord(Hero h, Clan clan, bool court)
        {
            if (h.Occupation != Occupation.Lord) h.SetNewOccupation(Occupation.Lord);
            h.Clan = clan;
            h.IsMinorFactionHero = clan.IsMinorFaction;
            SetCourt(h, court);
            if (h.PartyBelongedTo == null && h.PartyBelongedToAsPrisoner == null)
            {
                var seat = SeatOf(clan);
                if (seat != null && seat != h.CurrentSettlement)
                {
                    if (h.CurrentSettlement != null) LeaveSettlementAction.ApplyForCharacterOnly(h);
                    EnterSettlementAction.ApplyForCharacterOnly(h, seat);
                }
                if (!h.IsActive) h.ChangeState(Hero.CharacterStates.Active);
            }
        }

        /// <summary>Siedziba rodu: HomeSettlement (twierdza, nieoblezona) -> lenno rodu -> osada glowy rodu -> stolica krolestwa -> HomeSettlement jaki jest.</summary>
        private static Settlement SeatOf(Clan clan)
        {
            Settlement home = null;
            try { home = clan.HomeSettlement; } catch { }
            if (home != null && home.IsFortification && home.SiegeEvent == null) return home;
            try
            {
                foreach (var t in clan.Fiefs)
                    if (t != null && t.Settlement != null && t.Settlement.SiegeEvent == null) return t.Settlement;
            }
            catch { }
            try
            {
                var ls = clan.Leader != null ? clan.Leader.CurrentSettlement : null;
                if (ls != null && ls.IsFortification && ls.SiegeEvent == null) return ls;
            }
            catch { }
            try
            {
                var k = clan.Kingdom;
                var ks = k != null ? k.InitialHomeSettlement : null;
                if (ks != null && ks.SiegeEvent == null) return ks;
            }
            catch { }
            return home;
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            try
            {
                if (!_on) return;
                EnsureLoaded();
                if (!_startDone) StartPass("pierwsza doba", true);
                var s = Settings.Current;
                if (_startDone && s != null && s.CanonCompanionsFollowStory) Moves();
                RebuildHeld();   // m104-e5: jency kanonu trzymani (po dzisiejszych zmianach stron)
                DayLine();
            }
            catch (Exception e) { Stumble("Daily", e); }
            _stDay = 0; _commanderNulledDay = 0; _spawnBlockedDay = 0; _dDone = _dSkip = 0; _rescDay = 0; _lostOtherDay = 0;
        }

        private static void DayLine()
        {
            int aClan = 0, aLord = 0, aCourt = 0, aParty = 0, aPris = 0, aDead = 0, aWand = 0, aGone = 0;
            var wand = new StringBuilder();
            foreach (var r in _start)
            {
                if (r.Group != "A") continue;
                var h = FindOriginal(r.Template);
                if (h == null) { aGone++; continue; }
                if (!h.IsAlive) { aDead++; continue; }
                if (h.Clan != null && h.CompanionOf == null) aClan++;
                if (h.Occupation == Occupation.Lord) aLord++;
                if (IsCourtier(h)) aCourt++;
                if (h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h) aParty++;
                if (h.PartyBelongedToAsPrisoner != null) aPris++;
                if (h.Occupation == Occupation.Wanderer)
                {
                    aWand++;
                    if (wand.Length > 0) wand.Append("; ");
                    wand.Append(Nm(h)).Append(" (").Append(Where(h)).Append(h.CompanionOf != null ? ", towarzysz" : "").Append(')');
                }
            }
            int bAlive = 0, bDead = 0, bNone = 0;
            var bLive = new StringBuilder();
            foreach (var r in _start)
            {
                if (r.Group != "B") continue;
                var h = FindOriginal(r.Template);
                if (h == null) bNone++;
                else if (!h.IsAlive) bDead++;
                else
                {
                    bAlive++;
                    if (bLive.Length > 0) bLive.Append("; ");
                    bLive.Append(Nm(h)).Append(" (").Append(Where(h)).Append(h.CompanionOf != null ? ", towarzysz" : "").Append(')');
                }
            }
            Log.Info(Tag + ": dzien " + Day() + " | A " + _nA + ": w rodach " + aClan + " (lordow " + aLord + ", dworzan " + aCourt + ", z wlasna druzyna " + aParty
                     + ", jencow " + aPris + "), martwi " + aDead + ", bez bohatera " + aGone + ", wedrowcy " + aWand + (wand.Length > 0 ? " [" + wand + "]" : "")
                     + " | B " + _nB + ": zywych " + bAlive + (bLive.Length > 0 ? " [" + bLive + "]" : "") + ", nie powstali " + bNone + ", usunieci " + bDead
                     + ", prob ROT zablokowanych dzis " + _spawnBlockedDay + " (od wczytania " + _spawnBlocked + ")"
                     + " | zmiany stron (" + (Settings.Current != null && Settings.Current.CanonCompanionsFollowStory ? "wlaczone" : "WYLACZONE") + "): dzis wykonane " + _dDone
                     + ", pominiete " + _dSkip + "; razem wykonane " + CountDone("ok") + ", pominiete " + CountDone("skip") + ", czeka na wyzwalacz " + WaitingTrigger()
                     + ", w toku " + _pend.Count + (_pend.Count > 0 ? " [" + PendText() + "]" : "") + " | dzien opowiesci " + StoryDay().ToString("0", Inv) + (IsProlog() ? " (prolog)" : "")
                     + " | dworzanin wybrany na dowodce i odrzucony dzis " + _commanderNulledDay + " (razem " + _commanderNulled + ")"
                     + " | ratunek przy zniszczeniu rodu dzis " + _rescDay + " (od wczytania " + _rescTotal + "), usuniete przez cudzy kod dzis " + _lostOtherDay
                     + (_stDay > 0 ? " | POTKNIECIA " + _stDay + " (razem " + _st + ")" : "") + ".");
        }

        // ------------------------------------------------------------ dzien opowiesci (miejsce prologu)
        /// <summary>m101-prolog: JEDNO MIEJSCE DO PODPIECIA prologu - liczba dni od startu kampanii do startu opowiesci ROT (egzekucja Neda, NedEvent); klasyczny
        /// start = 0. Galaz m101-prolog zwraca tu dlugosc prologu kampanii zaczetej z prologiem (musi byc znana w pierwszej dobie - wybor jest w tworzeniu postaci,
        /// po OnNewGameCreated). Od tego zaleza: StoryDay() (wyzwalacz rotday:N), IsProlog() (wiersze mode prolog i wyzwalacz prolday:N).</summary>
        internal static double PrologDays() { try { return Prologue.Active ? Prologue.PrologueDays : 0.0; } catch { return 0.0; } }   // n30: podpiete pod m101-prolog (Prologue.Active, 188 dni)

        internal static bool IsProlog() { return PrologDays() > 0.0; }

        private static double CampaignDays()
        {
            try { return (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch { return 0.0; }
        }

        /// <summary>Dzien opowiesci ROT: 0 = start opowiesci ROT (klasycznie start kampanii), w prologu ujemny.</summary>
        internal static double StoryDay() { return CampaignDays() - PrologDays(); }

        // ------------------------------------------------------------ wydarzenia ROT (odczyt jak CrashScribe Fabula.EventState)
        private static object _evBeh;
        private static Campaign _evCamp;
        private static FieldInfo _fEvents;
        private static PropertyInfo _pTotal, _pActive;
        private static bool _evTypeTried;

        /// <summary>-1 = nie widac listy wydarzen ROT albo tego wydarzenia, 0 = czeka, 1 = trwa (IsActive), 2 = zakonczone (TotalEvents &gt; 0).</summary>
        private static int EventState(string typeName)
        {
            int cached;
            if (_evDay.TryGetValue(typeName, out cached)) return cached;
            int r = -1;
            try
            {
                if (_evBeh == null || _evCamp != Campaign.Current)
                {
                    _evCamp = Campaign.Current;
                    _evBeh = null;
                    var tBeh = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTEventBehavior");
                    if (tBeh != null && Campaign.Current != null)
                    {
                        var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                        if (mi != null) _evBeh = mi.MakeGenericMethod(tBeh).Invoke(Campaign.Current, null);
                        if (_fEvents == null) _fEvents = AccessTools.Field(tBeh, "_events");
                    }
                    if (!_evTypeTried)
                    {
                        _evTypeTried = true;
                        var tBase = AccessTools.TypeByName("ROT.Events.EventBase");
                        if (tBase != null) { _pTotal = AccessTools.Property(tBase, "TotalEvents"); _pActive = AccessTools.Property(tBase, "IsActive"); }
                    }
                }
                var list = _evBeh != null && _fEvents != null ? _fEvents.GetValue(_evBeh) as System.Collections.IEnumerable : null;
                if (list != null && _pTotal != null && _pActive != null)
                {
                    foreach (var ev in list)
                    {
                        if (ev == null || ev.GetType().Name != typeName) continue;
                        int total = Convert.ToInt32(_pTotal.GetValue(ev, null), Inv);
                        bool active = Convert.ToBoolean(_pActive.GetValue(ev, null), Inv);
                        r = active ? 1 : (total > 0 ? 2 : 0);
                        break;
                    }
                }
            }
            catch (Exception e) { Stumble("EventState", e); r = -1; }
            _evDay[typeName] = r;
            return r;
        }

        // ------------------------------------------------------------ wyszukiwanie
        private static Hero FindHeroId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var h in Hero.AllAliveHeroes) if (h != null && h.StringId == id) return h;
            foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.StringId == id) return h;
            return null;
        }

        private static Hero WhoHero(MoveRow r)
        {
            return r.Who.StartsWith(WandererPrefix, StringComparison.Ordinal) ? FindOriginal(r.Who) : FindHeroId(r.Who);
        }

        private static Kingdom FindKingdom(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        // ------------------------------------------------------------ wyzwalacze
        /// <summary>m105-kronika (K0): internal dla Chronicle.Terms (ev, done, war, own, rotday, prolday) - logika bez zmian. A4: kr:NNN.w[=x] (wyzwalacze #move
        /// z para 'kr', FORMAT 8) - Chronicle.Resolved; dotad kr: byl nieznanym rodzajem ("nigdy").</summary>
        internal static bool BaseTrue(string t, out bool never)
        {
            never = false;
            int c = t.IndexOf(':');
            string kind = c < 0 ? t : t.Substring(0, c), a = c < 0 ? "" : t.Substring(c + 1);
            int n;
            switch (kind)
            {
                case "ev": return EventState(a) >= 1;
                case "done": return EventState(a) == 2;
                case "dead": { var h = FindHeroId(a); return h != null && h.IsDead; }
                case "rotday": return int.TryParse(a, NumberStyles.Integer, Inv, out n) && StoryDay() >= n;
                case "prolday":
                    if (!IsProlog()) { never = true; return false; }
                    return int.TryParse(a, NumberStyles.Integer, Inv, out n) && CampaignDays() >= n;
                case "war":
                    {
                        var p = a.Split(':');
                        if (p.Length < 2) return false;
                        var k1 = FindKingdom(p[0]); var k2 = FindKingdom(p[1]);
                        return k1 != null && k2 != null && !k1.IsEliminated && !k2.IsEliminated && FactionManager.IsAtWarAgainstFaction(k1, k2);
                    }
                case "own":
                    {
                        var p = a.Split(':');
                        if (p.Length < 2) return false;
                        var st = Settlement.Find(p[0]);
                        return st != null && st.MapFaction != null && st.MapFaction.StringId == p[1];
                    }
                case "kr": return Chronicle.Resolved(a, out never);   // A4: okno Kroniki rozstrzygniete (=x - wybor); Kronika wylaczona / okno pominiete - nigdy
            }
            never = true;   // nieznany rodzaj - nie zajdzie nigdy
            return false;
        }

        // --- m105-kronika (K0): odczyt dla Chronicle.Terms (tylko odczyt stanu; logika zmian stron bez zmian) ---

        /// <summary>Term Kroniki mv:mNN: "done" (wiersz #move wykonany), "skip" (pominiety), "wait" (czeka na wyzwalacz albo w toku; tez przy wylaczonym
        /// kluczu CanonCompanionsFollowStory - moze wrocic), null (brak wiersza w arm_canon_moves.tsv albo kampania bez postaci kanonu - nie zajdzie nigdy).</summary>
        internal static string MoveState(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string v;
            if (_done.TryGetValue(id, out v)) return v.EndsWith("|ok", StringComparison.Ordinal) ? "done" : "skip";
            if (!_on) return null;
            EnsureLoaded();
            foreach (var r in _moves) if (r.Id == id) return "wait";
            return null;
        }

        /// <summary>Dzien kampanii rozstrzygniecia wiersza #move (wykonany albo pominiety); -1 - nierozstrzygniety.</summary>
        internal static int MoveDay(string id)
        {
            string v; int d;
            if (string.IsNullOrEmpty(id) || !_done.TryGetValue(id, out v)) return -1;
            int bar = v.IndexOf('|');
            return int.TryParse(bar > 0 ? v.Substring(0, bar) : v, NumberStyles.Integer, Inv, out d) ? d : -1;
        }

        /// <summary>Pierwszy dzien prawdy wyzwalacza (baza bez +N) z pamieci zmian stron; -1 - brak (Kronika bierze wczesniejszy z dwoch zapisow).</summary>
        internal static int FirstDay(string bas)
        {
            int d;
            return bas != null && _first.TryGetValue(bas, out d) ? d : -1;
        }

        /// <summary>Swiezy odczyt stanu wydarzen ROT przy nastepnym BaseTrue (pamiec _evDay czyszczona dotad tylko w dobowym przebiegu Moves).</summary>
        internal static void ClearEventCache() { _evDay.Clear(); }

        /// <summary>Wyzwalacz "baza[+N]": baza spelniona kiedykolwiek (pierwszy dzien w zapisie) i minelo N dob.</summary>
        private static bool TermTrue(string term, int day, out bool never)
        {
            never = false;
            string bas = term;
            int plus = 0, i = term.LastIndexOf('+');
            if (i > 0 && int.TryParse(term.Substring(i + 1), NumberStyles.Integer, Inv, out plus)) bas = term.Substring(0, i);
            else plus = 0;
            bool kr = bas.StartsWith("kr:", StringComparison.Ordinal);
            if (kr && !Chronicle.KeyOn) { never = true; return false; }   // A4: Kronika wylaczona - kr: falsz "nigdy" takze z pamiecia +N (FORMAT 3.3)
            int f;
            if (!_first.TryGetValue(bas, out f))
            {
                if (!BaseTrue(bas, out never)) return false;
                f = day;
                if (kr) { int rd = Chronicle.ResolvedDay(bas.Substring(3)); if (rd >= 0 && rd < day) f = rd; }   // A4: dokladny dzien rozstrzygniecia okna (+N)
                _first[bas] = f;
            }
            return day - f >= plus;
        }

        private static bool Fires(MoveRow r, int day, out bool never)
        {
            bool any = false, allNever = true;
            foreach (var t in r.Terms)
            {
                bool nv;
                if (TermTrue(t, day, out nv)) { if (!any) _firedBy[r.Id] = t; any = true; }   // A4: pierwszy prawdziwy term do linii logu (para kr, KW-13)
                if (!nv) allNever = false;
            }
            never = !any && allNever;
            return any;
        }

        // ------------------------------------------------------------ zmiany stron (doba)
        internal enum Res { Done, Skip, Wait }   // A4: internal - wynik ApplyAction dla Kroniki

        private static int CountDone(string res)
        {
            int n = 0;
            foreach (var v in _done.Values) if (v.EndsWith("|" + res, StringComparison.Ordinal)) n++;
            return n;
        }

        private static int WaitingTrigger()
        {
            int n = 0;
            foreach (var r in _moves) if (!_done.ContainsKey(r.Id) && !_pend.ContainsKey(r.Id)) n++;
            return n;
        }

        private static string PendText()
        {
            var sb = new StringBuilder();
            foreach (var r in _moves)
            {
                int d;
                if (!_pend.TryGetValue(r.Id, out d)) continue;
                string why;
                _waitWhy.TryGetValue(r.Id, out why);
                if (sb.Length > 0) sb.Append("; ");
                sb.Append('#').Append(r.Id).Append(' ').Append(r.Name).Append(": ").Append(why ?? "?").Append(" od dnia ").Append(d.ToString(Inv));
            }
            return sb.ToString();
        }

        private static void Moves()
        {
            int day = Day();
            _evDay.Clear();
            _firedBy.Clear();
            var fires = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var r in _moves)
            {
                if (_done.ContainsKey(r.Id)) continue;
                try
                {
                    if (r.Mode == "prolog" && !IsProlog()) { Resolve(r, day, Res.Skip, "tylko kampania z prologiem (klasyczny start)", null, null, false); continue; }
                    if (r.Mode == "classic" && IsProlog()) { Resolve(r, day, Res.Skip, "tylko klasyczny start (kampania z prologiem)", null, null, false); continue; }
                    bool never;
                    bool f = Fires(r, day, out never);
                    if (never) { Resolve(r, day, Res.Skip, "wyzwalacz nie moze zajsc w tej kampanii", null, null, false); continue; }
                    fires[r.Id] = f || _pend.ContainsKey(r.Id);
                }
                catch (Exception e) { Stumble("Moves.Fires(" + r.Id + ")", e); }
            }
            // wiersze tej samej postaci po kolei
            var order = new List<string>();
            var byWho = new Dictionary<string, List<MoveRow>>(StringComparer.Ordinal);
            foreach (var r in _moves)
            {
                if (_done.ContainsKey(r.Id) || !fires.ContainsKey(r.Id)) continue;
                List<MoveRow> l;
                if (!byWho.TryGetValue(r.Who, out l)) { l = new List<MoveRow>(); byWho[r.Who] = l; order.Add(r.Who); }
                l.Add(r);
            }
            foreach (var who in order)
            {
                var l = byWho[who];
                int last = -1;
                for (int j = 0; j < l.Count; j++) if (fires[l[j].Id]) last = j;
                if (last < 0) continue;
                for (int j = 0; j <= last; j++)
                {
                    var r = l[j];
                    try
                    {
                        if (!fires[r.Id])
                        {
                            Resolve(r, day, Res.Skip, "wyprzedzona przez #" + l[last].Id + " (jej wyzwalacz nie zaszedl, a pozniejszy tak)", null, null, false);
                            continue;
                        }
                        if (Execute(r, day) == Res.Wait) break;   // w toku - pozniejsze tej postaci czekaja
                    }
                    catch (Exception e) { Stumble("Moves.Execute(" + r.Id + ")", e); break; }
                }
            }
        }

        private static string Side(Hero h)
        {
            if (h == null) return "-";
            try
            {
                var c = h.Clan;
                string occ = h.Occupation == Occupation.Wanderer ? "wedrowiec" : h.Occupation == Occupation.Lord ? "lord" : h.Occupation.ToString();
                return (c != null ? "rod " + Nm(c) : "bez rodu") + ", " + occ + (IsCourtier(h) ? " (dworzanin)" : "") + ", " + Where(h);
            }
            catch { return "?"; }
        }

        /// <summary>Wynik zmiany: zapis (raz), linia logu, wiesc dla gracza.</summary>
        private static void Resolve(MoveRow r, int day, Res res, string why, Hero h, string before, bool tell)
        {
            if (res == Res.Wait)
            {
                if (!_pend.ContainsKey(r.Id)) _pend[r.Id] = day;
                _waitWhy[r.Id] = why;
                int since = _pend[r.Id];
                if (day - since >= WaitDays) { Resolve(r, day, Res.Skip, "czekala " + (day - since) + " dob (" + why + ")", h, before, false); return; }
                if (_waitSaid.Add(r.Id))
                    Log.Info(Tag + ": zmiana strony #" + r.Id + " CZEKA - " + r.Name + " [" + r.Who + "] - " + why + " (najwyzej " + WaitDays + " dob, linia raz na sesje) | wyzwalacz " + r.Trigger + ".");
                return;
            }
            _pend.Remove(r.Id); _waitWhy.Remove(r.Id);
            _done[r.Id] = day.ToString(Inv) + "|" + (res == Res.Done ? "ok" : "skip");
            string fired;
            _firedBy.TryGetValue(r.Id, out fired);   // A4: ktory term alternatywy zaszedl (np. kr:008.1+1 albo stary wyzwalacz) - para kr w KW-13
            string when = " | wyzwalacz " + r.Trigger + (fired != null && r.Terms.Length > 1 ? " (zaszlo " + fired + ")" : "")
                          + " (dzien kampanii " + day + ", opowiesci " + StoryDay().ToString("0", Inv) + ")";
            if (res == Res.Skip)
            {
                _dSkip++; _tSkip++;
                Log.Info(Tag + ": zmiana strony #" + r.Id + " POMINIETA - " + r.Name + " [" + r.Who + "] (" + r.Action + (r.Arg.Length > 0 ? ":" + r.Arg : "") + ") - " + why + when + ".");
                return;
            }
            _dDone++; _tDone++;
            string news = "brak";
            if (r.Message.Length > 0)
            {
                if (tell) { Log.Player(r.Message); news = "pokazana: \"" + r.Message + "\""; }
                else news = "nie pokazana (gracz nie zna postaci i jego krolestwa to nie dotyczy)";
            }
            Log.Info(Tag + ": zmiana strony #" + r.Id + " - " + Nm(h) + " [" + r.Who + "] akcja " + r.Action + (r.Arg.Length > 0 ? ":" + r.Arg : "") + " | " + (before ?? "-") + " -> " + Side(h)
                     + (why != null && why.Length > 0 ? " (" + why + ")" : "") + when + " | wiesc: " + news + ".");
        }

        private static bool PlayerCaptor(Hero h)
        {
            var c = h.PartyBelongedToAsPrisoner;
            if (c == null) return false;
            if (c == PartyBase.MainParty) return true;
            if (c.IsSettlement && c.Settlement != null && c.Settlement.OwnerClan == Clan.PlayerClan) return true;
            if (c.IsMobile && c.MobileParty != null && c.MobileParty.ActualClan == Clan.PlayerClan) return true;
            return false;
        }

        private static string PlayerSide(Hero h)
        {
            if (h == Hero.MainHero) return "to gracz";
            if (h.Clan == Clan.PlayerClan) return "w rodzie gracza";
            if (h.CompanionOf == Clan.PlayerClan) return "towarzysz gracza";
            if (h.PartyBelongedTo != null && h.PartyBelongedTo == MobileParty.MainParty) return "w druzynie gracza";
            if (PlayerCaptor(h)) return "w niewoli gracza";
            return null;
        }

        private static bool Busy(Hero h)
        {
            var p = h.PartyBelongedTo;
            return p != null && (p.MapEvent != null || p.SiegeEvent != null);
        }

        private static bool Captive(Hero h) { return h.PartyBelongedToAsPrisoner != null || h.IsPrisoner; }

        /// <summary>Wyjscie z druzyny jak gra przy zmianie rodu po slubie (MarriageAction.HandleClanChangeAfterMarriageForHero): armia, RemoveTroop, zbieg,
        /// druzyna lorda bez dowodcy rozwiazana (StartDisband - ludzie odchodza do osady, nic nie przepada).</summary>
        private static void LeaveParty(Hero h)
        {
            var p = h.PartyBelongedTo;
            if (p == null) return;
            if (p.Army != null)
            {
                if (p.Army.LeaderParty == p) DisbandArmyAction.ApplyByUnknownReason(p.Army);
                else p.Army = null;
            }
            bool leader = p.LeaderHero == h;
            p.MemberRoster.RemoveTroop(h.CharacterObject);
            MakeHeroFugitiveAction.Apply(h);
            if (leader && p.IsLordParty) DisbandPartyAction.StartDisband(p);
        }

        private static Res MoveToClan(Hero h, Clan clan, bool court, out string why)
        {
            why = "";
            if (h.Clan == clan && h.CompanionOf == null) { SetCourt(h, court); why = "juz w tym rodzie"; return Res.Done; }
            if (h.Clan != null && h.Clan.Leader == h) { why = "glowa rodu " + Nm(h.Clan) + " - nie zmienia rodu"; return Res.Skip; }
            if (Busy(h)) { why = "w bitwie albo oblezeniu"; return Res.Wait; }
            LeaveParty(h);
            if (h.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(h);
            if (h.Occupation != Occupation.Lord) h.SetNewOccupation(Occupation.Lord);
            h.Clan = clan;
            h.IsMinorFactionHero = clan.IsMinorFaction;
            SetCourt(h, court);
            var seat = SeatOf(clan);
            if (seat != null) TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, seat);
            else if (!h.IsActive) h.ChangeState(Hero.CharacterStates.Active);
            return Res.Done;
        }

        private static Settlement PickTown(string culture, Hero h)
        {
            Settlement player = null;
            try { player = Hero.MainHero != null ? Hero.MainHero.CurrentSettlement : null; } catch { }
            var same = new List<Settlement>();
            var any = new List<Settlement>();
            foreach (var t in Town.AllTowns)
            {
                var s = t != null ? t.Settlement : null;
                if (s == null || s == player || s.SiegeEvent != null || t.Culture == null) continue;
                if (t.Culture.StringId == "whitewalker") continue;
                any.Add(s);
                if (t.Culture.StringId == culture) same.Add(s);
            }
            var pool = same.Count > 0 ? same : any;
            return pool.Count > 0 ? pool[MBRandom.RandomInt(pool.Count)] : null;
        }

        private static PartyBase Captor(string arg)
        {
            var st = Settlement.Find(arg);
            if (st != null) return st.OwnerClan == Clan.PlayerClan ? null : st.Party;
            var clan = FindClan(arg);
            if (clan == null || clan.IsEliminated || clan == Clan.PlayerClan) return null;
            var seat = SeatOf(clan);
            if (seat != null && seat.IsFortification && seat.Party != null && seat.OwnerClan != Clan.PlayerClan) return seat.Party;
            var l = clan.Leader;
            var lp = l != null && l.IsAlive ? l.PartyBelongedTo : null;
            if (lp != null && lp.LeaderHero == l && lp.IsActive && lp.MapEvent == null && !lp.IsCurrentlyAtSea) return lp.Party;
            return null;
        }

        /// <summary>Koniec wszystkich zalotow obojga (jak Romance.EndAllCourtships gry - internal): RomanticState.Level = Ended. Zwraca liczbe zmienionych.</summary>
        private static int EndCourtships(Hero a, Hero b)
        {
            int n = 0;
            var list = Romance.RomanticStateList;
            if (list == null) return 0;
            foreach (var rs in list)
            {
                if (rs == null || rs.Level == Romance.RomanceLevelEnum.Ended) continue;
                if (rs.Person1 == a || rs.Person2 == a || rs.Person1 == b || rs.Person2 == b) { rs.Level = Romance.RomanceLevelEnum.Ended; n++; }
            }
            return n;
        }

        private static Res Execute(MoveRow r, int day)
        {
            var h = WhoHero(r);
            string before = h != null && h.IsAlive ? Side(h) : null;
            IFaction fromK = null;
            try { if (h != null) fromK = h.MapFaction; } catch { }
            string why;
            Res res = ApplyAction(h, r.Action, r.Arg, "#" + r.Id, out why);   // A4: ta sama droga co akcje Kroniki
            if (res == Res.Wait) { Resolve(r, day, Res.Wait, why, h, before, false); return Res.Wait; }
            if (res == Res.Skip) { Resolve(r, day, Res.Skip, why, h, before, false); return Res.Skip; }
            bool tell = false;
            try
            {
                var pk = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                IFaction toK = h.IsAlive ? h.MapFaction : null;
                tell = h.HasMet || (pk != null && (pk == fromK || pk == toK));
            }
            catch { }
            Resolve(r, day, Res.Done, why, h, before, tell);
            return Res.Done;
        }

        /// <summary>m105-kronika A4: jedna akcja zmiany postaci (wydzielona z Execute - logika akcji #move bez zmian). source - zrodlo do linii i zapisu ("#m11",
        /// "Kronika 008.1"). Wspolne kontrole: brak postaci / martwa - POMINIETA; postac gracza (rod, towarzysz, druzyna, jeniec gracza) - POMINIETA (nie dla
        /// unhide); postac ukryta przez Armoury - kazda akcja poza hide/unhide CZEKA na powrot. Wynik Done/Skip/Wait + why (linia wolajacego).</summary>
        internal static Res ApplyAction(Hero h, string action, string arg, string source, out string why)
        {
            why = "";
            arg = arg ?? "";
            source = source ?? "";
            if (h == null) { why = "brak postaci (nie powstala albo wymazana)"; return Res.Skip; }
            if (!h.IsAlive) { why = "postac martwa"; return Res.Skip; }
            if (action != "unhide")
            {
                string ps = PlayerSide(h);
                if (ps != null) { why = ps; return Res.Skip; }
                HidRow hr;
                if (action != "hide" && _hid.TryGetValue(Key(h), out hr) && h.IsDisabled)
                {
                    why = "ukryty (" + hr.Src + ", od dnia " + hr.Day.ToString(Inv) + ") - czeka na powrot";
                    return Res.Wait;
                }
            }
            if (action == "hide") return Hide(h, arg, source, out why);
            if (action == "unhide") return Unhide(h, arg, source, out why);
            Res res = Res.Done;
            switch (action)
            {
                case "lead":
                    SetCourt(h, false);
                    why = "moze prowadzic druzyne";
                    break;
                case "clan":
                case "court":
                    {
                        if (Captive(h)) { res = Res.Wait; why = "jeniec (AI) - czeka na wolnosc"; break; }
                        var clan = FindClan(arg);
                        if (clan == null || clan.IsEliminated) { res = Res.Skip; why = "brak rodu " + arg + (clan != null ? " (wyeliminowany)" : ""); break; }
                        res = MoveToClan(h, clan, action == "court", out why);
                        break;
                    }
                case "prisoner":
                    {
                        if (Captive(h)) { res = Res.Skip; why = "juz jeniec " + Where(h); break; }
                        if (Busy(h)) { res = Res.Wait; why = "w bitwie albo oblezeniu"; break; }
                        var cap = Captor(arg);
                        if (cap == null) { res = Res.Wait; why = "brak miejsca niewoli (" + arg + ": siedziba / druzyna glowy rodu)"; break; }
                        LeaveParty(h);
                        if (h.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(h);
                        if (h.CurrentSettlement != null) LeaveSettlementAction.ApplyForCharacterOnly(h);
                        TakePrisonerAction.Apply(cap, h);
                        why = "jeniec " + cap.Name;
                        break;
                    }
                case "free":
                    {
                        var clan = FindClan(arg);
                        if (Captive(h))
                        {
                            _freeing = h;   // m104-e5: nasze uwolnienie - straz niewoli prologu (Prologue.World) przepuszcza
                            try { EndCaptivityAction.ApplyByReleasedByChoice(h); }
                            finally { _freeing = null; }
                            why = "uwolniony";
                        }
                        else why = "juz wolny";
                        if (clan != null && !clan.IsEliminated)
                        {
                            string w2;
                            var r2 = MoveToClan(h, clan, IsCourtier(h), out w2);
                            if (r2 != Res.Done) { res = r2; why += "; " + w2; break; }
                            if (w2.Length > 0) why += "; " + w2;
                            else why += "; do rodu " + Nm(clan);
                        }
                        break;
                    }
                case "wander":
                    {
                        if (Captive(h)) { res = Res.Wait; why = "jeniec (AI) - czeka na wolnosc"; break; }
                        if (h.Clan != null && h.Clan.Leader == h) { res = Res.Skip; why = "glowa rodu " + Nm(h.Clan) + " - nie odchodzi"; break; }
                        if (Busy(h)) { res = Res.Wait; why = "w bitwie albo oblezeniu"; break; }
                        LeaveParty(h);
                        if (h.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(h);
                        if (h.Clan != null) h.Clan = null;
                        SetCourt(h, false);
                        h.IsMinorFactionHero = false;
                        if (h.Occupation != Occupation.Wanderer) h.SetNewOccupation(Occupation.Wanderer);
                        var town = PickTown(arg, h);
                        if (town != null) TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, town);
                        else if (!h.IsActive) h.ChangeState(Hero.CharacterStates.Active);
                        why = "do wynajecia w " + Nm(town) + " (kultura " + arg + ")";
                        break;
                    }
                case "remove":
                    RemoveQuiet(h, "zmiana strony " + source + " (remove)");   // Lost, bez komunikatu; wzor A po starcie zablokowany - ROT go nie odtworzy
                    why = "usuniety ze swiata (Lost, bez komunikatu)";
                    break;
                case "die":
                    KillCharacterAction.ApplyByMurder(h, null, false);   // smierc bez komunikatu; lord zostaje na liscie zmarlych (gra wymazuje tylko wedrowcow)
                    why = h.IsDead ? "nie zyje (bez komunikatu)" : "znak smierci - umrze po bitwie";
                    break;
                case "marry":
                case "wed":
                    {
                        var o = FindHeroId(arg);
                        if (o == null || !o.IsAlive) { res = Res.Skip; why = "brak albo martwy " + arg; break; }
                        if (h.Spouse != null) { res = Res.Skip; why = "juz w malzenstwie z " + Nm(h.Spouse); break; }
                        if (o.Spouse != null) { res = Res.Skip; why = Nm(o) + " juz w malzenstwie z " + Nm(o.Spouse); break; }
                        string ops = PlayerSide(o);
                        if (ops != null) { res = Res.Skip; why = Nm(o) + ": " + ops; break; }
                        if (Captive(h) || Captive(o)) { res = Res.Wait; why = "jedno z nich w niewoli"; break; }
                        if (action == "wed" && o.Clan != null && h.Clan != o.Clan)
                        {
                            string w2;
                            var r2 = MoveToClan(h, o.Clan, true, out w2);
                            if (r2 != Res.Done) { res = r2; why = w2; break; }
                        }
                        h.Spouse = o;   // setter ustawia obie strony (Hero.Spouse); bez zmiany rodu drugiej strony
                        // jak gra w MarriageAction.ApplyInternal: koniec zalotow obojga (Romance.EndAllCourtships jest internal - ta sama petla) i stan Marriage
                        // (inaczej zaloty gracza do jednego z nich trwalyby dalej, a slub w grze po cichu by sie nie udal - recenzja)
                        int ended = EndCourtships(h, o);
                        ChangeRomanticStateAction.Apply(h, o, Romance.RomanceLevelEnum.Marriage);
                        why = "slub z " + Nm(o) + " (rod " + Nm(o.Clan) + ")" + (ended > 0 ? ", zakonczone zaloty " + ended : "");
                        break;
                    }
                default:
                    res = Res.Skip;
                    why = "nieznana akcja " + action;
                    break;
            }
            return res;
        }

        // ------------------------------------------------------------ ukrycie i powrot (m105-kronika A4, plan P3, KRONIKA-FORMAT 8, 11, 15)

        /// <summary>Lista chronionych (KRONIKA-FORMAT 15, jak kronika_check.py): bohater -> wydarzenia ROT, z ktorych KTOREKOLWIEK musi zajsc, zanim wolno go ukryc
        /// (ev:X - wydarzenie ruszylo, done:X - skonczone). Postacie potrzebne pozniejszym wydarzeniom ROT nie moga zniknac wczesniej (prog KW-15).</summary>
        private static readonly Dictionary<string, string[]> Guarded = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "lord_1_29", new[] { "done:BastardsEvent" } }, { "lord_2_3", new[] { "done:WallSiegeEvent" } }, { "lord_5_17", new[] { "done:BastardsEvent" } },
            { "lord_1_14", new[] { "ev:DanyInvasionEvent" } }, { "lord_1_11", new[] { "done:StannisDeathEvent", "done:EastwatchEvent" } },
            { "lord_1_17", new[] { "done:RooseDeathEvent" } }, { "lord_5_1", new[] { "done:RedWeddingEvent" } }, { "lord_5_7", new[] { "done:RedWeddingEvent" } },
            { "lord_5_13_1", new[] { "done:RedWeddingEvent" } }, { "lord_7_1", new[] { "done:JoffreyEvent" } }, { "ROTlord_369", new[] { "ev:AegonInvasionEvent" } },
            { "lord_1_9", new[] { "done:RenlyDeathEvent" } }, { "lord_1_25", new[] { "done:RenlyDeathEvent" } },
            { "lord_WE9_l", new[] { "done:BlackwaterSiegeEvent" } }, { "lord_4_14", new[] { "done:BlackwaterSiegeEvent" } },
            { "lord_4_25", new[] { "done:HarrenhalSiegeEvent" } }, { "lord_4_25_1", new[] { "done:HarrenhalSiegeEvent" } },
            { "lord_1_3", new[] { "done:RedWeddingEvent" } }, { "lord_1_50", new[] { "done:RedWeddingEvent" } }, { "lord_1_5", new[] { "done:RedWeddingEvent" } },
            { "ROTlord_513", new[] { "done:RedWeddingEvent" } }, { "lord_5_16", new[] { "done:RedWeddingEvent" } },
            { "lord_2_18", new[] { "done:RickardKarstarkExecutionEvent" } }, { "lord_5_3", new[] { "done:JeorMutinyEvent" } }, { "lord_4_2", new[] { "done:JoffreyEvent" } },
            { "ROTlord_568", new[] { "done:JonEvent" } }, { "lord_5_21", new[] { "done:JonEvent" } }, { "lord_2_19", new[] { "done:JonEvent" } },
            { "lord_2_23", new[] { "done:BastardsEvent" } }, { "lord_5_20", new[] { "done:BastardsEvent" } },
            { "ROTlord_486", new[] { "ev:DanyInvasionEvent" } }, { "ROTlord_373", new[] { "ev:AegonInvasionEvent" } },
        };

        /// <summary>0 - poza lista chronionych, 1 - na liscie i wydarzenie juz zaszlo (need = term spelniony), 2 - na liscie, wydarzenie jeszcze nie (need = czego brak).</summary>
        internal static int GuardState(Hero h, out string need)
        {
            need = null;
            string[] ev;
            if (h == null || h.StringId == null || !Guarded.TryGetValue(h.StringId, out ev)) return 0;
            foreach (var t in ev)
            {
                bool nv;
                if (BaseTrue(t, out nv)) { need = t; return 1; }
            }
            need = string.Join("|", ev);
            return 2;
        }

        internal static int HiddenCount { get { return _hid.Count; } }

        /// <summary>Kopia rejestru ukrytych (doba Kroniki - koniec ukrycia, linie startu i doby).</summary>
        internal static List<HidRow> HiddenRows() { return new List<HidRow>(_hid.Values); }

        /// <summary>Ukryty przez Armoury (wpis w rejestrze i stan Disabled).</summary>
        internal static bool IsHidden(Hero h)
        {
            HidRow r;
            return h != null && _hid.Count > 0 && _hid.TryGetValue(Key(h), out r) && h.IsDisabled;
        }

        /// <summary>Bohater wpisu rejestru: wzor ROTwanderer* - oryginal (zywy, wylaczony albo martwy), inaczej id bohatera (takze z listy wylaczonych).</summary>
        internal static Hero HiddenHero(HidRow r)
        {
            if (r == null) return null;
            try { return r.Key.StartsWith(WandererPrefix, StringComparison.Ordinal) ? FindOriginal(r.Key) : FindHeroId(r.Key); }
            catch { return null; }
        }

        /// <summary>Wpis rejestru bez dzialania w grze (postac zmarla w ukryciu albo przywrocona przez gre / inny mod) - usuniecie z powodem (doba Kroniki).</summary>
        internal static void DropHidden(HidRow r) { if (r != null) _hid.Remove(r.Key); }

        private static Res Hide(Hero h, string end, string source, out string why)
        {
            string k = Key(h);
            HidRow hr;
            if (_hid.TryGetValue(k, out hr))
            {
                if (h.IsDisabled)
                {
                    // FORMAT 11: hide na postaci juz ukrytej - nowy koniec ZASTEPUJE stary (np. Arya 008.1 rotday:2000, potem 044.2 kr:098.2)
                    string old = hr.End;
                    hr.End = Clean(end); hr.Src = Clean(source);
                    why = "UKRYCIE: nowy koniec " + (end.Length > 0 ? end : "do unhide (bez terminu)") + " (byl " + (old.Length > 0 ? old : "bez terminu") + ", ukryty od dnia "
                          + hr.Day.ToString(Inv) + ")";
                    return Res.Done;
                }
                _hid.Remove(k);   // wpis nieaktualny (gra przywrocila postac) - ukrycie od nowa
            }
            if (h.IsDisabled) { why = "wylaczony przez gre albo inny mod (nie przez Armoury) - bez zmian"; return Res.Skip; }
            if (h.IsClanLeader) { why = "glowa rodu " + Nm(h.Clan) + " - ukrycie zakazane (FORMAT 11)"; return Res.Skip; }
            if (h.IsKingdomLeader) { why = "wladca " + (h.MapFaction != null ? h.MapFaction.StringId : "?") + " - ukrycie zakazane"; return Res.Skip; }
            string need;
            int g = GuardState(h, out need);
            if (g == 2) { why = "lista chronionych - jeszcze przed " + need + " (FORMAT 15)"; return Res.Skip; }
            if (Captive(h)) { why = "jeniec " + Where(h) + " - czeka na wolnosc (jenca nie ukrywamy)"; return Res.Wait; }
            if (h.GovernorOf != null) { why = "gubernator " + Nm(h.GovernorOf.Settlement) + " - czeka"; return Res.Wait; }
            if (Busy(h)) { why = "w bitwie albo oblezeniu"; return Res.Wait; }
            string from = Where(h);
            LeaveParty(h);                 // plan P3: przed DisableHeroAction zawsze wyjscie z druzyny (armia, zbieg, druzyna lorda - StartDisband, ludzie nie przepadaja)
            DisableHeroAction.Apply(h);    // gra: z osady, stan Disabled (lista martwych i wylaczonych - CampaignObjectManager.HeroStateChanged)
            if (!h.IsDisabled) { why = "DisableHeroAction bez skutku (stan " + h.HeroState + ")"; return Res.Skip; }
            _hid[k] = new HidRow { Key = k, Src = Clean(source), End = Clean(end), Day = Day() };
            why = "ukryty (byl: " + from + ")" + (end.Length > 0 ? ", koniec ukrycia " + end : ", bez terminu (do unhide albo na zawsze)")
                  + (g == 1 ? " | lista chronionych: " + need + " spelnione" : "");
            return Res.Done;
        }

        private static Res Unhide(Hero h, string arg, string source, out string why)
        {
            string k = Key(h);
            HidRow hr;
            if (!_hid.TryGetValue(k, out hr))
            {
                // FORMAT 11: unhide postaci, ktorej Armoury nie ukryl - POMINIETA, bez teleportu (postac zostaje tam, gdzie jest)
                why = "nie ukryty" + (h.IsDisabled ? " (wylaczony przez gre albo inny mod - bez zmian)" : "");
                return Res.Skip;
            }
            if (!h.IsDisabled)
            {
                _hid.Remove(k);
                why = "nie ukryty - przywrocony wczesniej przez gre albo inny mod (stan " + h.HeroState + "), wpis ukrycia usuniety";
                return Res.Skip;
            }
            Settlement to = null;
            string how = "";
            if (arg.Length > 0)
            {
                var st = Settlement.Find(arg);
                bool enemy = false;
                try { enemy = st != null && st.MapFaction != null && h.MapFaction != null && FactionManager.IsAtWarAgainstFaction(st.MapFaction, h.MapFaction); } catch { }
                if (st != null && st.SiegeEvent == null && !st.IsHideout && !enemy) { to = st; how = "osada z akcji"; }
                else how = "osada " + arg + (st == null ? " nie istnieje" : st.SiegeEvent != null ? " oblezona" : enemy ? " wroga" : " to kryjowka") + " - ";
            }
            if (to == null && h.Clan != null && !h.Clan.IsEliminated)
            {
                var seat = SeatOf(h.Clan);
                if (seat != null && seat.SiegeEvent == null && !seat.IsHideout) { to = seat; how += "siedziba rodu " + Nm(h.Clan); }
            }
            if (to == null)
            {
                to = PickTown(h.Culture != null ? h.Culture.StringId : "", h);
                if (to != null) how += "miasto kultury " + (h.Culture != null ? h.Culture.StringId : "-");
            }
            if (to == null) { why = "brak osady powrotu"; return Res.Wait; }
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, to);   // gra: Disabled -> Active (lista zywych), potem wejscie do osady
            if (!h.IsActive) h.ChangeState(Hero.CharacterStates.Active);
            _hid.Remove(k);
            why = "powrot do " + Nm(to) + " (" + to.StringId + ", " + how + ") po ukryciu od dnia " + hr.Day.ToString(Inv) + " (" + hr.Src + ")"
                  + (source.Length > 0 ? " | zrodlo " + source : "");
            return Res.Done;
        }


        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var sb = new StringBuilder();
            sb.Append("v\t1\n");
            if (_nk) sb.Append("nk\t1\n");
            if (_on) sb.Append("on\t1\n");
            if (_startDone) sb.Append("sd\t1\n");
            foreach (var t in _conv) sb.Append("conv\t").Append(t).Append('\n');
            foreach (var t in _startFail) sb.Append("fail\t").Append(t).Append('\n');
            foreach (var t in _court) sb.Append("court\t").Append(t).Append('\n');
            foreach (var kv in _done) sb.Append("done\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            foreach (var kv in _first) sb.Append("first\t").Append(kv.Key).Append('\t').Append(kv.Value.ToString(Inv)).Append('\n');
            foreach (var kv in _pend) sb.Append("pend\t").Append(kv.Key).Append('\t').Append(kv.Value.ToString(Inv)).Append('\n');
            foreach (var t in _gone) sb.Append("gone\t").Append(t).Append('\n');
            foreach (var kv in _lostBy) sb.Append("lost\t").Append(kv.Key).Append('\t').Append(Clean(kv.Value)).Append('\n');
            foreach (var h in _hid.Values)   // A4: "hid klucz dzien zrodlo koniec" (koniec na koncu - moze miec ':')
                sb.Append("hid\t").Append(h.Key).Append('\t').Append(h.Day.ToString(Inv)).Append('\t').Append(Clean(h.Src)).Append('\t').Append(Clean(h.End)).Append('\n');
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            Reset();
            if (string.IsNullOrEmpty(s)) return;   // zapis sprzed paczki albo kampania bez klucza - nic
            foreach (var raw in s.Split('\n'))
            {
                var p = raw.Split('\t');
                if (p.Length < 2) continue;
                switch (p[0])
                {
                    case "nk": _nk = p[1] == "1"; break;
                    case "on": _on = p[1] == "1"; break;
                    case "sd": _startDone = p[1] == "1"; break;
                    case "conv": _conv.Add(p[1]); break;
                    case "fail": _startFail.Add(p[1]); break;
                    case "court": _court.Add(p[1]); break;
                    case "done": if (p.Length >= 3) _done[p[1]] = p[2]; break;
                    case "first": { int d; if (p.Length >= 3 && int.TryParse(p[2], NumberStyles.Integer, Inv, out d)) _first[p[1]] = d; break; }
                    case "pend": { int d; if (p.Length >= 3 && int.TryParse(p[2], NumberStyles.Integer, Inv, out d)) _pend[p[1]] = d; break; }
                    case "gone": _gone.Add(p[1]); break;
                    case "lost": if (p.Length >= 3) _lostBy[p[1]] = p[2]; break;
                    case "hid":
                        {
                            int d;
                            if (p.Length >= 3 && p[1].Length > 0 && int.TryParse(p[2], NumberStyles.AllowLeadingSign, Inv, out d))
                                _hid[p[1]] = new HidRow { Key = p[1], Day = d, Src = p.Length >= 4 ? p[3].TrimEnd('\r') : "", End = p.Length >= 5 ? p[4].TrimEnd('\r') : "" };
                            break;
                        }
                }
            }
        }
    }

    /// <summary>m101-postacie: stan postaci kanonu w zapisie (arm_canon_companions), start nowej kampanii, doba, dworzanie bez druzyny.</summary>
    internal sealed class CanonCompanionsBehavior : CampaignBehaviorBase
    {
        public CanonCompanionsBehavior() { CanonCompanions.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => CanonCompanions.OnNewGame());
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, _ => CanonCompanions.AfterNewGame());
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => CanonCompanions.SessionStart());
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, CanonCompanions.Daily);
            CampaignEvents.CanHeroLeadPartyEvent.AddNonSerializedListener(this, CanonCompanions.CanLead);
            CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, CanonCompanions.OnClanDestroyed);   // ratunek postaci kanonu z rodu niszczonego przez ROT
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, CanonCompanions.HeroKilled);             // zrodlo usuniecia (Lost) postaci kanonu
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? CanonCompanions.Export() : null;
                SaveText.Sync(dataStore, "arm_canon_companions", ref v);
                if (dataStore.IsLoading) CanonCompanions.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_canon_companions", e); }
        }
    }
}
