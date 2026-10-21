using System;
using System.Collections.Generic;
using System.Globalization;
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
    ///   dworzanin -> null, jak postfiks BK tej metody) - nie dostaje druzyny, zostaje w osadzie rodu. Dziecko postaci (ten sam Template, z rodzicem) nie jest dworzaninem.
    /// Wyjatek per obiekt - licznik potkniec (CLAUDE.md 7), nic nie gasnie.
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

        // --- dane (raz na proces) ---
        private static bool _tried;
        private static string _note = "nie wczytane";
        private static readonly List<StartRow> _start = new List<StartRow>();
        private static readonly Dictionary<string, StartRow> _byTpl = new Dictionary<string, StartRow>(StringComparer.Ordinal);
        private static int _nA, _nB, _nC, _nD;

        // --- stan w zapisie (arm_canon_companions) ---
        private static bool _nk;          // kampania zalozona z ta paczka (OnNewGameCreated)
        private static bool _on;          // klucz CanonCompanionsToClans w chwili zalozenia kampanii
        private static bool _startDone;   // start A/B zamkniety
        private static readonly HashSet<string> _conv = new HashSet<string>(StringComparer.Ordinal);       // wzory A przeniesione do rodu
        private static readonly HashSet<string> _startFail = new HashSet<string>(StringComparer.Ordinal);  // wzory A bez bohatera / rodu przy starcie
        private static readonly HashSet<string> _court = new HashSet<string>(StringComparer.Ordinal);      // klucz postaci (wzor albo id bohatera) - bez druzyny

        // --- sesja ---
        private static int _st, _stDay, _commanderNulled, _commanderNulledDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static bool _installed, _pCommander;
        private static string _patchLine = "";
        private static bool _startLineSaid;

        internal static bool Active { get { return _on; } }

        /// <summary>Konstruktor zachowania (kazda kampania: nowa i wczytana) - stan od zera; dane z pliku zostaja.</summary>
        internal static void Reset()
        {
            _nk = _on = _startDone = false;
            _conv.Clear(); _startFail.Clear(); _court.Clear();
            _st = _stDay = _commanderNulled = _commanderNulledDay = 0;
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
                _start.Clear(); _byTpl.Clear();
                _note = "blad wczytania (" + e.GetType().Name + ")";
                Log.Error("CanonCompanions.Load", e);
            }
        }

        private static void Parse(string text)
        {
            _start.Clear(); _byTpl.Clear();
            _nA = _nB = _nC = _nD = 0;
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
                if (_on) EnsureLoaded();
                if (_on && !_startDone) StartPass("start sesji", false);
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
            foreach (var r in _start)
            {
                if (r.Group != "A" || r.Clan.Length == 0 || !seen.Add(r.Clan)) continue;
                clansWant++;
                if (FindClan(r.Clan) != null) clansFound++;
            }
            Log.Info(Tag + ": start | klucz CanonCompanionsToClans " + YN(s != null && s.CanonCompanionsToClans) + " (w chwili zalozenia kampanii: "
                     + (_nk ? YN(_on) : "stary zapis - nic") + "), zmiany stron CanonCompanionsFollowStory " + YN(s != null && s.CanonCompanionsFollowStory)
                     + " | plik " + (_note.Length == 0 ? "wczytany" : _note) + " | A " + _nA + ", B " + _nB + ", C " + _nC + ", D " + _nD
                     + " | mapa rodow znaleziona " + clansFound + "/" + clansWant + " | przeniesieni " + _conv.Count + ", bez rodu/bohatera " + _startFail.Count
                     + ", dworzan " + _court.Count + ", start " + (_startDone ? "zamkniety" : "otwarty") + " | latki: " + _patchLine + ".");
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
                DayLine();
            }
            catch (Exception e) { Stumble("Daily", e); }
            _stDay = 0; _commanderNulledDay = 0;
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
                    if (wand.Length > 0) wand.Append(", ");
                    wand.Append(Nm(h)).Append(" (").Append(Where(h)).Append(h.CompanionOf != null ? ", towarzysz" : "").Append(')');
                }
            }
            Log.Info(Tag + ": dzien " + Day() + " | A " + _nA + ": w rodach " + aClan + " (lordow " + aLord + ", dworzan " + aCourt + ", z wlasna druzyna " + aParty
                     + ", jencow " + aPris + "), martwi " + aDead + ", bez bohatera " + aGone + ", wedrowcy " + aWand + (wand.Length > 0 ? " [" + wand + "]" : "")
                     + " | dworzanin wybrany na dowodce i odrzucony dzis " + _commanderNulledDay + " (razem " + _commanderNulled + ")"
                     + (_stDay > 0 ? " | POTKNIECIA " + _stDay + " (razem " + _st + ")" : "") + ".");
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
