using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A1) - WARUNKI KRONIKI (KRONIKA-FORMAT.md rozdz. 10; plan E "Chronicle.Terms.cs").
        /// Skladnia: termy laczone ' &amp; ' (i), w skladniku alternatywa ' | ' (lub, wiaze mocniej: a | b &amp; c = (a lub b) i c), '!' przed termem = negacja,
        /// '+N' na koncu = N dni KAMPANII od pierwszego dnia prawdy termu (pamiec w zapisie - jak CanonCompanions._first; nie z '!' i nie przy p:).
        /// Wynik trojwartosciowy: prawda / falsz / NIGDY (term juz nie zajdzie w tej kampanii - okno przepada z powodem, koniec ukrycia "nigdy" konczy ukrycie):
        /// i = nigdy, gdy ktorykolwiek skladnik nigdy; lub = nigdy, gdy kazda alternatywa nigdy; !t = prawda, gdy t nigdy; !t = nigdy, gdy t prawdziwy i trwaly
        /// (ev, done, dead, rotday, prolday, kr, flag, mv, plan - raz prawdziwe zostaja prawdziwe).
        /// Zrodla: ev/done/war/own(krolestwo)/rotday/prolday - CanonCompanions.BaseTrue (bez zmiany logiki; dead/alive wlasne - postacie z szablonow ROTwanderer*);
        /// mv - CanonCompanions.MoveState; cw - Prologue.CrownGuardHolds; season - WesterosClimate.TextSeason; plan - CrashScribe Fabula.Timeline (odbicie) x
        /// Prologue.StoryScale (brak osi = prawda, decyduje od); kr/flag - Chronicle.State.
        /// Nieznany term albo zle argumenty: FALSZ (nie nigdy - silnik nowszej wersji moze go znac), licznik + linia "NIEZNANY TERM" raz na term.
        /// Liczenie bez skrotu (kazdy term skladnika - zeby pamiec pierwszego spelnienia +N zapisala sie tego samego dnia, jak CanonCompanions.Fires).
        /// Pamieci wyszukiwan (bohater/rod/krolestwo/osada, najblizsza osada gracza) - do zmiany godziny gry albo kampanii; stan wydarzen ROT CanonCompanions
        /// odswiezany tak samo (ClearEventCache). Wyjatek przy termie - potkniecie (licznik), term = falsz, nic nie gasnie.
        /// </summary>
        internal static class Terms
        {
            /// <summary>Rodzaje termow znane silnikowi (porownuje je narzedzie scratchpadu z danymi; kolejnosc jak KRONIKA-FORMAT 10).</summary>
            internal static readonly string[] Kinds = { "ev", "done", "dead", "alive", "clanof", "head", "leads", "prisoner", "war", "peace", "kexists", "own",
                                                        "rotday", "prolday", "kr", "flag", "mv", "cw", "season", "plan", "p" };
            internal static readonly string[] PlayerKinds = { "region", "in", "near", "vassal", "army", "rel" };
            /// <summary>Wydarzenia ROT (29 klas ROT.Events, KRONIKA-FORMAT 10) - dobowy zapis pierwszego dnia ev:/done: (Daily).</summary>
            internal static readonly string[] RotEvents = { "AegonInvasionEvent", "BastardsEvent", "BastardsSiegeEvent", "BastardsSiegeNotificationEvent", "BlackwaterEvent",
                "BlackwaterSiegeEvent", "BlackwaterSiegeNotificationEvent", "DanyInvasionEvent", "EastwatchEvent", "FistEvent", "HarrenhalEvent", "HarrenhalSiegeEvent",
                "HarrenhalSiegeNotificationEvent", "JeorMutinyEvent", "JoffreyEvent", "JonEvent", "KingsLandingEvent", "NedEvent", "NightKingDeathEvent", "RamsayEvent",
                "RedWeddingEvent", "RenlyDeathEvent", "RickardKarstarkExecutionEvent", "RiverlandsDeclareWarEvent", "RooseDeathEvent", "StannisDeathEvent", "WallEvent",
                "WallSiegeEvent", "WallSiegeNotificationEvent" };
            private static readonly HashSet<string> Lasting = new HashSet<string>(StringComparer.Ordinal) { "ev", "done", "dead", "rotday", "prolday", "kr", "flag", "mv", "plan" };
            private static readonly string[] Seasons = { "spring", "summer", "autumn", "winter" };   // kolejnosc CampaignTime.Seasons
            /// <summary>Tokeny z argumentem (zjadaja nastepna czesc przy dzieleniu po ':' - jak kronika_check.split_args).</summary>
            private static readonly HashSet<string> ArgTokens = new HashSet<string>(StringComparer.Ordinal) { "@wladca", "@glowa", "@pan", "@imie", "@nazwa", "@skarbiec", "@kasa" };
            /// <summary>Skala dokumentu Kroniki: dni od/do liczone przy FabulaTimeScale 4.33 (rok 364 dni).</summary>
            internal const float DocScale = 4.33f;

            /// <summary>Tokeny instancji okna powracajacego (rozdz. 13). Wartosci = id w grze (osada, bohater, rod, krolestwo) albo liczba/pora jako tekst.</summary>
            internal sealed class Tokens
            {
                public string Cel = "", Kto = "", Rodkto = "", Region = "", Liczba = "", Pora = "";

                internal string Get(string token)
                {
                    switch (token)
                    {
                        case "@cel": return Cel;
                        case "@kto": return Kto;
                        case "@rodkto": return Rodkto;
                        case "@region": return Region;
                        case "@liczba": return Liczba;
                        case "@pora": return Pora;
                    }
                    return null;
                }
            }

            private sealed class Term
            {
                public string Raw = "", Base = "", Kind = "", Bad;
                public bool Neg, HasTok;
                public int Plus;
                public string[] A = Data.NoStrings;
            }

            // --- pamieci ---
            private static readonly Dictionary<string, Term[][]> _parsed = new Dictionary<string, Term[][]>(StringComparer.Ordinal);
            private static readonly Dictionary<string, int> _first = new Dictionary<string, int>(StringComparer.Ordinal);   // ZAPIS: baza termu -> pierwszy dzien kampanii prawdy
            private static readonly HashSet<string> _unknownSaid = new HashSet<string>(StringComparer.Ordinal);
            internal static int Unknown, UnknownDay;   // nieznane termy przy liczeniu (sesja / doba)

            private static Campaign _cCamp;
            private static long _cHour = long.MinValue;
            private static readonly Dictionary<string, Hero> _heroes = new Dictionary<string, Hero>(StringComparer.Ordinal);
            private static readonly Dictionary<string, Clan> _clans = new Dictionary<string, Clan>(StringComparer.Ordinal);
            private static readonly Dictionary<string, Kingdom> _kingdoms = new Dictionary<string, Kingdom>(StringComparer.Ordinal);
            private static readonly Dictionary<string, Hero> _heroIdx = new Dictionary<string, Hero>(StringComparer.Ordinal);
            private static bool _heroIdxBuilt, _clansBuilt, _kingdomsBuilt;
            private static Settlement _near;
            private static bool _nearDone;
            private static Vec2 _nearPos;

            private static Dictionary<string, float> _timeline;
            private static bool _tlTried;

            // ---------------------------------------------------------------- stan kampanii (zapis)
            /// <summary>Nowa kampania / przed wczytaniem: pamiec pierwszego spelnienia od zera (dane i pamiec skladni zostaja).</summary>
            internal static void Reset()
            {
                _first.Clear();
                _unknownSaid.Clear();
                Unknown = UnknownDay = 0;
                Invalidate();
            }

            /// <summary>Linie zapisu "tf baza dzien" (pamiec pierwszego spelnienia +N).</summary>
            internal static string Export()
            {
                var sb = new StringBuilder(32 * _first.Count);
                foreach (var kv in _first) sb.Append("tf\t").Append(kv.Key).Append('\t').Append(kv.Value.ToString(Inv)).Append('\n');
                return sb.ToString();
            }

            /// <summary>Wczytanie: Reset, potem linie "tf" (inne prefiksy pomijane).</summary>
            internal static void Import(string s)
            {
                Reset();
                if (string.IsNullOrEmpty(s)) return;
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    int d;
                    if (p.Length >= 3 && p[0] == "tf" && p[1].Length > 0 && int.TryParse(p[2], NumberStyles.AllowLeadingSign, Inv, out d)) _first[p[1]] = d;
                }
            }

            internal static int FirstCount { get { return _first.Count; } }

            /// <summary>Doba (DailyTick Kroniki, krok A2 - na poczatku przebiegu): swieze pamieci i zapis pierwszego dnia ev:X / done:X wszystkich 29 wydarzen ROT
            /// (dokladny dzien dla +N, nawet gdy okno liczy swoj warunek dopiero od 'od').</summary>
            internal static void Daily()
            {
                UnknownDay = 0;
                Invalidate();
                int day = Day();
                foreach (var ev in RotEvents)
                {
                    try
                    {
                        bool nv;
                        string k = "ev:" + ev;
                        if (!_first.ContainsKey(k) && CanonCompanions.BaseTrue(k, out nv)) _first[k] = day;
                        k = "done:" + ev;
                        if (!_first.ContainsKey(k) && CanonCompanions.BaseTrue(k, out nv)) _first[k] = day;
                    }
                    catch (Exception e) { Stumble("Terms.Daily(" + ev + ")", e); }
                }
            }

            /// <summary>Poczatek przebiegu (doba albo pokazanie okna): swieze pamieci wyszukiwan i stanu wydarzen ROT. Bez wolania - odswiezane co godzine gry.</summary>
            internal static void Invalidate()
            {
                _heroes.Clear(); _clans.Clear(); _kingdoms.Clear(); _heroIdx.Clear();
                _heroIdxBuilt = _clansBuilt = _kingdomsBuilt = false;
                _nearDone = false; _near = null;
                _cCamp = Campaign.Current;
                _cHour = HourNow();
                try { CanonCompanions.ClearEventCache(); } catch { }
            }

            private static long HourNow() { try { return (long)Math.Floor(CampaignTime.Now.ToHours); } catch { return 0; } }

            private static void CheckCache()
            {
                if (Campaign.Current != _cCamp || HourNow() != _cHour) Invalidate();
            }

            // ---------------------------------------------------------------- czas
            /// <summary>Dzien kampanii (calkowity, jak CanonCompanions.Day) - jednostka '+N' i pamieci pierwszego spelnienia.</summary>
            internal static int Day() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

            /// <summary>Dzien opowiesci (CanonCompanions.StoryDay: od egzekucji Neda; w prologu ujemny).</summary>
            internal static double StoryDay() { return CanonCompanions.StoryDay(); }

            /// <summary>Mnoznik dni dokumentu Kroniki: k = FabulaTimeScale / 4.33 (Prologue.StoryScale - CrashScribe odbiciem, zapas rok/84).</summary>
            internal static float DocK() { return Prologue.StoryScale() / DocScale; }

            /// <summary>Dzien od/do okna (skala dokumentu) -> dzien opowiesci kampanii.</summary>
            internal static double DocToStory(int docDay) { return docDay * (double)DocK(); }

            /// <summary>Planowany dzien opowiesci wydarzenia ROT: CrashScribe Fabula.Timeline[X] x skala osi (jak Fabula.Scaled). false - brak osi albo wydarzenia bez daty.</summary>
            internal static bool PlanDay(string rotEvent, out double storyDay)
            {
                storyDay = 0;
                try
                {
                    if (!_tlTried)
                    {
                        _tlTried = true;
                        var t = AccessTools.TypeByName("CrashScribe.Fabula");
                        var f = t != null ? AccessTools.Field(t, "Timeline") : null;
                        _timeline = f != null ? f.GetValue(null) as Dictionary<string, float> : null;
                    }
                    float d;
                    if (_timeline == null || rotEvent == null || !_timeline.TryGetValue(rotEvent, out d)) return false;
                    storyDay = d * (double)Prologue.StoryScale();
                    return true;
                }
                catch (Exception e) { Stumble("Terms.PlanDay", e); return false; }
            }

            /// <summary>Os Fabuli dostepna (do linii startu: "plan Fabuli TAK/NIE").</summary>
            internal static bool PlanAvailable { get { double d; PlanDay("NedEvent", out d); return _timeline != null; } }

            // ---------------------------------------------------------------- liczenie
            /// <summary>Warunek bez tokenow, bez powodu. Pusty = prawda.</summary>
            internal static bool Eval(string expr)
            {
                bool nv; string why;
                return Eval(expr, null, null, out nv, out why);
            }

            /// <summary>Warunek (kolumny po/przed/warunki/gracz/dostepna, koniec ukrycia). Pusty = prawda.
            /// tok - tokeny instancji okna powracajacego (null - brak); where - miejsce do linii nieznanego termu (np. "008.1 warunki").
            /// never - falsz na zawsze (POMINIETE z powodem); why - pierwszy niespelniony skladnik (przy never: skladnik "nigdy" z dopiskiem), null gdy prawda.</summary>
            internal static bool Eval(string expr, Tokens tok, string where, out bool never, out string why)
            {
                never = false; why = null;
                if (string.IsNullOrEmpty(expr)) return true;
                Term[][] e;
                try { e = Parse(expr); }
                catch (Exception ex) { Stumble("Terms.Parse", ex); why = expr + " (blad skladni)"; return false; }
                CheckCache();
                int day = Day();
                bool all = true;
                foreach (var g in e)
                {
                    bool any = false, allNever = true;
                    foreach (var t in g)
                    {
                        bool nv;
                        if (One(t, tok, where, day, out nv)) any = true;
                        if (!nv) allNever = false;
                    }
                    if (any) continue;
                    all = false;
                    if (allNever && !never) { never = true; why = GroupText(g) + " (nigdy)"; }
                    else if (why == null) why = GroupText(g);
                }
                if (all) { never = false; why = null; }
                return all;
            }

            /// <summary>Skladnia bez liczenia (Data przy wczytaniu): false + powod przy nieznanym termie albo zlych argumentach.</summary>
            internal static bool Syntax(string expr, out string why)
            {
                why = null;
                if (string.IsNullOrEmpty(expr)) return true;
                foreach (var g in Parse(expr))
                    foreach (var t in g)
                        if (t.Bad != null) { why = "'" + t.Raw + "': " + t.Bad; return false; }
                return true;
            }

            private static string GroupText(Term[] g)
            {
                if (g.Length == 1) return g[0].Raw;
                var sb = new StringBuilder();
                for (int i = 0; i < g.Length; i++) { if (i > 0) sb.Append(" | "); sb.Append(g[i].Raw); }
                return sb.ToString();
            }

            private static Term[][] Parse(string expr)
            {
                Term[][] e;
                if (_parsed.TryGetValue(expr, out e)) return e;
                var groups = new List<Term[]>();
                foreach (var conj in expr.Split('&'))
                {
                    var alts = new List<Term>();
                    foreach (var a in conj.Split('|'))
                    {
                        string s = a.Trim();
                        if (s.Length == 0) { alts.Add(new Term { Raw = "(pusty)", Bad = "pusty term (podwojne & albo |)" }); continue; }
                        alts.Add(ParseTerm(s));
                    }
                    groups.Add(alts.ToArray());
                }
                e = groups.ToArray();
                if (_parsed.Count < 4000) _parsed[expr] = e;
                return e;
            }

            private static Term ParseTerm(string raw)
            {
                var t = new Term { Raw = raw };
                string s = raw;
                if (s.StartsWith("!", StringComparison.Ordinal)) { t.Neg = true; s = s.Substring(1).Trim(); }
                if (!s.StartsWith("p:", StringComparison.Ordinal))
                {
                    int i = s.LastIndexOf('+'), n;
                    if (i > 0 && int.TryParse(s.Substring(i + 1), NumberStyles.None, Inv, out n)) { t.Plus = n; s = s.Substring(0, i); }
                }
                t.Base = s;
                var p = s.Split(':');
                t.Kind = p[0];
                if (p.Length > 1) { t.A = new string[p.Length - 1]; Array.Copy(p, 1, t.A, 0, p.Length - 1); }
                foreach (var a in t.A) if (a.StartsWith("@", StringComparison.Ordinal)) t.HasTok = true;
                t.Bad = Check(t);
                if (t.Bad == null && t.Neg && t.Plus > 0) t.Bad = "negacja z +N niedozwolona";
                return t;
            }

            /// <summary>Liczba i postac argumentow wedlug KRONIKA-FORMAT 10; null = dobrze.</summary>
            private static string Check(Term t)
            {
                var a = t.A;
                int n;
                foreach (var x in a) if (x.Length == 0) return "pusty argument";
                switch (t.Kind)
                {
                    case "ev": case "done": case "dead": case "alive": case "kexists": case "flag":
                        return a.Length == 1 ? null : t.Kind + " ma miec 1 argument";
                    case "clanof": case "head": case "leads": case "war": case "peace": case "own":
                        return a.Length == 2 ? null : t.Kind + " ma miec 2 argumenty";
                    case "prisoner":
                        return a.Length == 1 || a.Length == 2 ? null : "prisoner:<bohater>[:<rod|krolestwo|osada>]";
                    case "rotday": case "prolday":
                        return a.Length == 1 && int.TryParse(a[0], NumberStyles.AllowLeadingSign, Inv, out n) ? null : t.Kind + ":N";
                    case "kr":
                        {
                            if (a.Length != 1) return "kr:NNN.w[=x]";
                            string id = a[0], opt = null;
                            int eq = id.IndexOf('=');
                            if (eq >= 0) { opt = id.Substring(eq + 1); id = id.Substring(0, eq); }
                            if (id.Length != 5 || id[3] != '.') return "kr:NNN.w[=x] - id okna NNN.w";
                            if (opt != null && (opt.Length != 1 || opt[0] < 'a' || opt[0] > 'd')) return "kr:NNN.w=x - opcja a-d";
                            return null;
                        }
                    case "mv":
                        {
                            if (a.Length != 1) return "mv:mNN[=done|skip]";
                            int eq = a[0].IndexOf('=');
                            if (eq == 0) return "mv:mNN[=done|skip]";
                            if (eq > 0) { string r = a[0].Substring(eq + 1); if (r != "done" && r != "skip") return "mv:mNN=done albo =skip"; }
                            return null;
                        }
                    case "cw":
                        return a.Length == 1 && (a[0] == "reach" || a[0] == "dorne") ? null : "cw:reach albo cw:dorne";
                    case "season":
                        return a.Length == 1 && Array.IndexOf(Seasons, a[0]) >= 0 ? null : "season:winter|spring|summer|autumn";
                    case "plan":
                        {
                            if (a.Length != 1) return "plan:<WydarzenieROT>-N";
                            int d = a[0].LastIndexOf('-');
                            return d > 0 && int.TryParse(a[0].Substring(d + 1), NumberStyles.None, Inv, out n) ? null : "plan:<WydarzenieROT>-N";
                        }
                    case "p":
                        {
                            if (a.Length == 0) return "p:<region|in|near|vassal|army|rel>";
                            switch (a[0])
                            {
                                case "region": case "in": case "vassal": return a.Length == 2 ? null : "p:" + a[0] + ":<id>";
                                case "army": return a.Length == 1 ? null : "p:army bez argumentow";
                                case "near": return a.Length == 3 && int.TryParse(a[2], NumberStyles.None, Inv, out n) ? null : "p:near:<osada>:<odleglosc>";
                                case "rel": return a.Length == 3 && int.TryParse(a[2], NumberStyles.AllowLeadingSign, Inv, out n) ? null : "p:rel:<bohater>:<min>";
                            }
                            return "nieznany term gracza p:" + a[0];
                        }
                }
                return "nieznany term " + t.Kind;
            }

            private static void SayUnknown(Term t, string where)
            {
                Unknown++; UnknownDay++;
                if (_unknownSaid.Count < 200 && _unknownSaid.Add(t.Raw))
                    Log.Info(Tag + "NIEZNANY TERM '" + t.Raw + "' (" + t.Bad + ")" + (where != null ? " w " + where : "") + " - liczony jako falsz (linia raz na term).");
            }

            private static bool One(Term t, Tokens tok, string where, int day, out bool never)
            {
                never = false;
                if (t.Bad != null) { SayUnknown(t, where); return false; }
                try
                {
                    string[] a = t.A;
                    string bas = t.Base;
                    if (t.HasTok)
                    {
                        a = new string[t.A.Length];
                        for (int i = 0; i < a.Length; i++)
                        {
                            string x = t.A[i];
                            if (x.StartsWith("@", StringComparison.Ordinal))
                            {
                                x = x == "@gracz" ? (Hero.MainHero != null ? Hero.MainHero.StringId : null) : (tok != null ? tok.Get(x) : null);
                                if (string.IsNullOrEmpty(x)) { never = true; return false; }   // instancja bez tego tokenu - nie zajdzie
                            }
                            a[i] = x;
                        }
                        bas = t.Kind + ":" + string.Join(":", a);
                    }
                    bool nv;
                    if (t.Plus > 0)
                    {
                        if (t.Kind == "kr" && !KeyOn) { never = true; return false; }   // Kronika wylaczona - kr: nigdy, takze z pamiecia +N (FORMAT 3.3)
                        int f = KnownFirst(t.Kind, a), mine, canon;
                        if (_first.TryGetValue(bas, out mine) && (f == NoDay || mine < f)) f = mine;
                        if (t.Kind == "ev" || t.Kind == "done" || t.Kind == "dead" || t.Kind == "war" || t.Kind == "own" || t.Kind == "rotday")
                        {
                            canon = CanonCompanions.FirstDay(bas);
                            if (canon >= 0 && (f == NoDay || canon < f)) f = canon;   // CanonCompanions: -1 = brak (dni kampanii sa dodatnie)
                        }
                        if (f == NoDay)
                        {
                            if (!Base(t.Kind, a, bas, out nv)) { never = nv; return false; }
                            f = day;
                            _first[bas] = day;
                        }
                        return day - f >= t.Plus;
                    }
                    bool v = Base(t.Kind, a, bas, out nv);
                    if (t.Neg)
                    {
                        if (nv) return true;                                          // term nigdy prawdziwy -> negacja zawsze prawdziwa
                        if (v && Lasting.Contains(t.Kind)) { never = true; return false; }
                        return !v;
                    }
                    never = nv;
                    return v;
                }
                catch (Exception e) { Stumble("Terms(" + t.Raw + ")", e); return false; }
            }

            private const int NoDay = int.MinValue;

            /// <summary>Dokladny pierwszy dzien z wlasnego stanu: kr - dzien rozstrzygniecia okna (State), mv - dzien wiersza #move (CanonCompanions); NoDay - brak.</summary>
            private static int KnownFirst(string kind, string[] a)
            {
                if (kind == "kr")
                {
                    string id = a[0]; int eq = id.IndexOf('=');
                    if (eq >= 0) return NoDay;   // wybor - dzien z pamieci
                    return KeyOn && State.IsResolved(id) ? State.ResolvedDay(id) : NoDay;
                }
                if (kind == "mv")
                {
                    string id = a[0]; int eq = id.IndexOf('=');
                    string want = eq >= 0 ? id.Substring(eq + 1) : null;
                    if (eq >= 0) id = id.Substring(0, eq);
                    string st = CanonCompanions.MoveState(id);
                    if (st == null || st == "wait" || (want != null && want != st)) return NoDay;
                    int d = CanonCompanions.MoveDay(id);
                    return d >= 0 ? d : NoDay;
                }
                return NoDay;
            }

            /// <summary>Term bez '!' i '+N', argumenty po podstawieniu tokenow.</summary>
            private static bool Base(string kind, string[] a, string bas, out bool never)
            {
                never = false;
                switch (kind)
                {
                    case "ev": case "done": case "rotday": case "prolday": case "war":
                        return CanonCompanions.BaseTrue(bas, out never);
                    case "own":
                        {
                            bool nv;
                            if (CanonCompanions.BaseTrue("own:" + a[0] + ":" + a[1], out nv)) return true;   // krolestwo (MapFaction)
                            var st = FindSettlement(a[0], null);
                            return st != null && st.OwnerClan != null && st.OwnerClan.StringId == a[1];      // rod
                        }
                    case "dead": { var h = FindHero(a[0], null); return h != null && h.IsDead; }
                    case "alive":
                        {
                            var h = FindHero(a[0], null);
                            if (h == null) return false;
                            if (h.IsDead) { never = true; return false; }
                            return true;   // takze ukryty przez Kronike (Disabled - nie martwy)
                        }
                    case "clanof":
                        {
                            var h = FindHero(a[0], null);
                            if (h == null) return false;
                            if (h.IsDead) { never = true; return false; }
                            return h.Clan != null && h.Clan.StringId == a[1];
                        }
                    case "head":
                        {
                            var h = FindHero(a[0], null);
                            if (h == null) return false;
                            if (h.IsDead) { never = true; return false; }
                            var c = FindClan(a[1], null);
                            return c != null && c.Leader == h;
                        }
                    case "leads":
                        {
                            var h = FindHero(a[0], null);
                            if (h == null) return false;
                            if (h.IsDead) { never = true; return false; }
                            var k = FindKingdom(a[1], null);
                            return k != null && !k.IsEliminated && k.Leader == h;
                        }
                    case "prisoner":
                        {
                            var h = FindHero(a[0], null);
                            if (h == null) return false;
                            if (h.IsDead) { never = true; return false; }
                            if (!h.IsPrisoner) return false;
                            return a.Length < 2 || CaptorIs(h, a[1]);
                        }
                    case "peace":
                        {
                            var k1 = FindKingdom(a[0], null); var k2 = FindKingdom(a[1], null);
                            return k1 != null && k2 != null && k1 != k2 && !k1.IsEliminated && !k2.IsEliminated && !FactionManager.IsAtWarAgainstFaction(k1, k2);
                        }
                    case "kexists": { var k = FindKingdom(a[0], null); return k != null && !k.IsEliminated; }
                    case "kr": return KrTrue(a[0], out never);
                    case "flag": return State.Flag(a[0]);
                    case "mv": return MvTrue(a[0], out never);
                    case "cw": return Prologue.CrownGuardHolds(a[0], out never);
                    case "season":
                        {
                            var t = CampaignTime.Now;
                            int s = WesterosClimate.TextSeason(ref t);
                            return s >= 0 && s < Seasons.Length && Seasons[s] == a[0];
                        }
                    case "plan":
                        {
                            int d = a[0].LastIndexOf('-');
                            int n = int.Parse(a[0].Substring(d + 1), NumberStyles.None, Inv);
                            double pd;
                            if (!PlanDay(a[0].Substring(0, d), out pd)) return true;   // brak osi Fabuli - decyduje od (P2)
                            return StoryDay() >= pd - n;
                        }
                    case "p": return PlayerTrue(a);
                }
                never = true;   // nie powinno sie zdarzyc (Check odrzuca nieznane)
                return false;
            }

            /// <summary>kr:NNN.w[=x]: okno rozstrzygniete (okno/wiesc/log/nic); pominiete, nagrobek albo klucz ChronicleOfWesteros NIE = nigdy (FORMAT 3.3).
            /// =x: wybrana opcja x; inna opcja = nigdy; rozstrzygniete bez wyboru - nigdy, gdy wiersz, ktory wygral, nie ma listy, inaczej czeka.
            /// Okno nieznane (paczka jeszcze nie wgrana) - falsz, nie nigdy.</summary>
            private static bool KrTrue(string arg, out bool never)
            {
                never = false;
                string id = arg, opt = null;
                int eq = arg.IndexOf('=');
                if (eq >= 0) { id = arg.Substring(0, eq); opt = arg.Substring(eq + 1); }
                if (!KeyOn || State.IsSkipped(id) || Data.IsTomb(id)) { never = true; return false; }
                if (!State.IsResolved(id)) return false;
                if (opt == null) return true;
                char c = State.Choice(id);
                if (c != '\0')
                {
                    if (c == opt[0]) return true;
                    never = true; return false;
                }
                var row = Data.Row(State.ResolvedRow(id));
                if (row != null && row.Options.Count == 0) never = true;
                return false;
            }

            /// <summary>mv:mNN[=done|skip]: wiersz #move rozstrzygniety (wykonany/pominiety). Brak wiersza albo kampania bez postaci kanonu = nigdy.</summary>
            private static bool MvTrue(string arg, out bool never)
            {
                never = false;
                string id = arg, want = null;
                int eq = arg.IndexOf('=');
                if (eq >= 0) { id = arg.Substring(0, eq); want = arg.Substring(eq + 1); }
                string st = CanonCompanions.MoveState(id);
                if (st == null) { never = true; return false; }
                if (st == "wait") return false;
                if (want == null || want == st) return true;
                never = true;
                return false;
            }

            private static bool CaptorIs(Hero h, string x)
            {
                var pb = h.PartyBelongedToAsPrisoner;
                if (pb == null) return false;
                var st = FindSettlement(x, null);
                if (st != null)
                    return (pb.IsSettlement && pb.Settlement == st) || (pb.MobileParty != null && pb.MobileParty.CurrentSettlement == st);
                var k = FindKingdom(x, null);
                if (k != null) return ReferenceEquals(pb.MapFaction, k);
                var c = FindClan(x, null);
                if (c != null)
                {
                    Clan pc = pb.IsSettlement && pb.Settlement != null ? pb.Settlement.OwnerClan : (pb.MobileParty != null ? pb.MobileParty.ActualClan : null);
                    if (pc == null && pb.Owner != null) pc = pb.Owner.Clan;
                    return pc == c;
                }
                return false;
            }

            private static bool PlayerTrue(string[] a)
            {
                var mp = MobileParty.MainParty;
                switch (a[0])
                {
                    case "region":
                        {
                            var s = NearestSettlement();
                            return s != null && s.MapFaction != null && s.MapFaction.StringId == a[1];
                        }
                    case "in":
                        {
                            Settlement s = mp != null ? mp.CurrentSettlement : null;
                            if (s == null && Hero.MainHero != null) s = Hero.MainHero.CurrentSettlement;
                            return s != null && s.StringId == a[1];
                        }
                    case "near":
                        {
                            var st = FindSettlement(a[1], null);
                            if (st == null || mp == null) return false;
                            int d = int.Parse(a[2], NumberStyles.None, Inv);
                            return mp.GetPosition2D.Distance(st.GetPosition2D) <= d;
                        }
                    case "vassal":
                        {
                            var pc = Clan.PlayerClan;
                            return pc != null && pc.Kingdom != null && pc.Kingdom.StringId == a[1];
                        }
                    case "army": return mp != null && mp.Army != null;
                    case "rel":
                        {
                            var me = Hero.MainHero;
                            var h = FindHero(a[1], null);
                            if (me == null || h == null || h == me) return false;
                            return me.GetRelation(h) >= int.Parse(a[2], NumberStyles.AllowLeadingSign, Inv);
                        }
                }
                return false;
            }

            /// <summary>Najblizsza osada gracza (miasto, zamek, wies) - p:region. Pamiec do zmiany godziny albo ruchu gracza o ponad 1 jednostke.</summary>
            internal static Settlement NearestSettlement()
            {
                var mp = MobileParty.MainParty;
                if (mp == null) return null;
                var cur = mp.CurrentSettlement;
                if (cur != null && (cur.IsTown || cur.IsCastle || cur.IsVillage)) return cur;
                Vec2 pos = mp.GetPosition2D;
                if (_nearDone && pos.DistanceSquared(_nearPos) < 1f) return _near;
                Settlement best = null;
                float bd = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage)) continue;
                    float d = s.GetPosition2D.DistanceSquared(pos);
                    if (d < bd) { bd = d; best = s; }
                }
                _near = best; _nearPos = pos; _nearDone = true;
                return best;
            }

            // ---------------------------------------------------------------- wyszukiwanie (wspolne z akcjami - krok A3)
            /// <summary>Dzieli argumenty akcji po ':' - token z argumentem (@wladca:k, @glowa:rod, @pan:osada, @imie:x, @nazwa:x, @skarbiec:k, @kasa:osada)
            /// liczy sie jako JEDEN argument, takze z tokenem w srodku (@glowa:@rodkto) - jak kronika_check.split_args.</summary>
            internal static List<string> SplitArgs(string s)
            {
                var outp = new List<string>();
                if (string.IsNullOrEmpty(s)) return outp;
                var parts = s.Split(':');
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i];
                    if (ArgTokens.Contains(p) && i + 1 < parts.Length) { outp.Add(p + ":" + parts[i + 1]); i++; continue; }
                    outp.Add(p);
                }
                return outp;
            }

            /// <summary>Bohater: id z gry (zywy, martwy albo wylaczony), ROTwanderer&lt;N&gt; (postac z szablonu - CanonCompanions.TemplateHero), @gracz,
            /// @kto (tokeny), @wladca:&lt;krolestwo&gt;, @glowa:&lt;rod&gt;, @pan:&lt;osada&gt; (glowa rodu-wlasciciela). null - brak.</summary>
            internal static Hero FindHero(string id, Tokens tok)
            {
                if (string.IsNullOrEmpty(id)) return null;
                if (id[0] == '@')
                {
                    if (id == "@gracz") return Hero.MainHero;
                    if (id == "@kto") return tok != null ? FindHero(tok.Kto, null) : null;
                    int c = id.IndexOf(':');
                    if (c < 0) return null;
                    string t = id.Substring(0, c), arg = id.Substring(c + 1);
                    if (t == "@wladca") { var k = FindKingdom(arg, tok); return k != null && !k.IsEliminated ? k.Leader : null; }
                    if (t == "@glowa") { var cl = FindClan(arg, tok); return cl != null ? cl.Leader : null; }
                    if (t == "@pan") { var st = FindSettlement(arg, tok); return st != null && st.OwnerClan != null ? st.OwnerClan.Leader : null; }
                    return null;
                }
                CheckCache();
                Hero h;
                if (_heroes.TryGetValue(id, out h)) return h;
                h = id.StartsWith(CanonCompanions.WandererPrefix, StringComparison.Ordinal) ? CanonCompanions.TemplateHero(id) : null;
                if (h == null)
                {
                    // indeks wszystkich bohaterow (zywi, martwi, wylaczeni) raz na epoke pamieci - zamiast liniowego Hero.Find dla kazdego id
                    if (!_heroIdxBuilt)
                    {
                        _heroIdxBuilt = true;
                        try
                        {
                            foreach (var x in Hero.AllAliveHeroes) if (x != null && x.StringId != null) _heroIdx[x.StringId] = x;
                            foreach (var x in Hero.DeadOrDisabledHeroes) if (x != null && x.StringId != null && !_heroIdx.ContainsKey(x.StringId)) _heroIdx[x.StringId] = x;
                        }
                        catch (Exception e) { Stumble("Terms.HeroIndex", e); }
                    }
                    _heroIdx.TryGetValue(id, out h);
                }
                _heroes[id] = h;
                return h;
            }

            /// <summary>Rod: id, @rodkto (token), @gracz (rod gracza). null - brak (tez rod wyjety z Clan.All przez ROT).</summary>
            internal static Clan FindClan(string id, Tokens tok)
            {
                if (string.IsNullOrEmpty(id)) return null;
                if (id == "@rodkto") return tok != null ? FindClan(tok.Rodkto, null) : null;
                if (id == "@gracz") return Clan.PlayerClan;
                if (id[0] == '@') return null;
                CheckCache();
                if (!_clansBuilt)
                {
                    _clansBuilt = true;
                    try { foreach (var x in Clan.All) if (x != null && x.StringId != null) _clans[x.StringId] = x; }
                    catch (Exception e) { Stumble("Terms.ClanIndex", e); }
                }
                Clan c;
                return _clans.TryGetValue(id, out c) ? c : null;
            }

            /// <summary>Krolestwo: id albo @region (token). Wyeliminowane tez zwracane (sprawdza wolajacy).</summary>
            internal static Kingdom FindKingdom(string id, Tokens tok)
            {
                if (string.IsNullOrEmpty(id)) return null;
                if (id == "@region") return tok != null ? FindKingdom(tok.Region, null) : null;
                if (id[0] == '@') return null;
                CheckCache();
                if (!_kingdomsBuilt)
                {
                    _kingdomsBuilt = true;
                    try { foreach (var x in Kingdom.All) if (x != null && x.StringId != null) _kingdoms[x.StringId] = x; }
                    catch (Exception e) { Stumble("Terms.KingdomIndex", e); }
                }
                Kingdom k;
                return _kingdoms.TryGetValue(id, out k) ? k : null;
            }

            /// <summary>Osada: id albo @cel (token).</summary>
            internal static Settlement FindSettlement(string id, Tokens tok)
            {
                if (string.IsNullOrEmpty(id)) return null;
                if (id == "@cel") return tok != null ? FindSettlement(tok.Cel, null) : null;
                if (id[0] == '@') return null;
                try { return Settlement.Find(id); } catch { return null; }
            }
        }
    }
}
