using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A2) - SKUTKI OKIEN I OPCJI (KRONIKA-FORMAT 11, 12; plan E "Chronicle.Actions.cs"). Kolumna skutki = akcje rozdzielone ' ; ', argumenty ':'
        /// (token z argumentem, np. @wladca:battania, liczy sie jako jeden argument - Terms.SplitArgs). Kazda akcja z FORMAT 11 ma wpis w rejestrze (KnownActions + klasa):
        /// T (tekst: wiesc, plotka, flag, kod), R (relacje/slawa: rel, relp, relk, renown, influence), S (swiat - reszta). Klucz ChronicleWorldChanges NIE pomija klase S
        /// z linia. Wynik: OK / POMINIETA (powod) / CZEKA (powod) - kazdy z linia "akcja NNN.w[=x] #i <akcja> -> ...". CZEKA: ponawiana co dobe (Daily) najdluzej
        /// WaitActionDays dni, potem POMINIETA; czekajace w zapisie ("kw"). Zakres A2: wiesc, plotka (Rumours.AddStoryNews, bez lektora na zywo), flag, kod:test,
        /// rel, relp, relk, renown, influence; reszta "POMINIETA (akcja jeszcze niewspierana)" - kroki A3+ dopisuja procedury w Impl (Bands/Hidden - liczniki dla linii).
        /// Wyjatek w akcji - POMINIETA z powodem i potkniecie; reszta skutkow idzie dalej (bez globalnego wylacznika).
        /// </summary>
        internal static class Actions
        {
            internal enum Res { Done, Skip, Wait }

            /// <summary>Kontekst akcji: okno/instancja, wiersz, opcja ('\0' - skutki okna), tokeny instancji, zmienne wiersza (teksty wiesci).</summary>
            internal sealed class Ctx
            {
                public string Id = "", RowKey = "";
                public char Opt;
                public Terms.Tokens Tok;
                public Dictionary<string, string> Vars;
                internal string Label { get { return Id + (Opt != '\0' ? "=" + Opt : ""); } }
            }

            /// <summary>Wszystkie akcje FORMAT 11 (kolejnosc tabeli) - narzedzie scratchpadu porownuje je z danymi.</summary>
            internal static readonly string[] KnownActions = { "wiesc", "plotka", "flag", "kod", "rel", "relp", "relk", "renown", "influence", "gold", "security", "loyalty",
                "pop", "ware", "garrison", "band", "task", "tourney", "muster", "hide", "unhide", "wound", "skill", "item", "clan", "court", "lead", "prisoner", "free",
                "wander", "die", "marry", "wed", "succeed", "own", "ruler", "leader", "alliance", "truce" };
            private static readonly HashSet<string> TextKinds = new HashSet<string>(StringComparer.Ordinal) { "wiesc", "plotka", "flag", "kod" };
            private static readonly HashSet<string> RelKinds = new HashSet<string>(StringComparer.Ordinal) { "rel", "relp", "relk", "renown", "influence" };
            private static readonly HashSet<string> Known = new HashSet<string>(KnownActions, StringComparer.Ordinal);

            private delegate Res Handler(Ctx c, List<string> a, out string detail);
            private static readonly Dictionary<string, Handler> Impl = new Dictionary<string, Handler>(StringComparer.Ordinal)
            {
                { "wiesc", News }, { "plotka", Rumour }, { "flag", Flag }, { "kod", Code },
                { "rel", Rel }, { "relp", RelP }, { "relk", RelK }, { "renown", Renown }, { "influence", Influence },
            };

            /// <summary>Klasa akcji: 'T' tekst, 'R' relacje i slawa, 'S' swiat (FORMAT 11, klucz ChronicleWorldChanges).</summary>
            internal static char ClassOf(string kind) { return TextKinds.Contains(kind) ? 'T' : RelKinds.Contains(kind) ? 'R' : 'S'; }

            private sealed class Waiting
            {
                public Ctx C;
                public int Idx, Since;
                public string Act = "", Why = "";
            }

            private static readonly List<Waiting> _wait = new List<Waiting>();
            private static int _dOk, _dSkip, _dWait, _dUnknown, _sOk, _sSkip;

            internal static int WaitCount { get { return _wait.Count; } }
            /// <summary>Bandy Kroniki na mapie (krok A3 - akcja band). Do linii doby.</summary>
            internal static int Bands { get { return 0; } }
            /// <summary>Postacie ukryte przez Kronike (krok A3 - akcje hide/unhide). Do linii startu i doby.</summary>
            internal static int Hidden { get { return 0; } }

            internal static void Reset() { _wait.Clear(); DayReset(); _sOk = _sSkip = 0; }

            internal static void DayReset() { _dOk = _dSkip = _dWait = _dUnknown = 0; }

            internal static string DayText()
            {
                return "akcje ok " + _dOk + ", pominiete " + _dSkip + ", czeka " + _wait.Count + " (nowe dzis " + _dWait + ")" + (_dUnknown > 0 ? ", NIEZNANE " + _dUnknown : "");
            }

            /// <summary>Skutki (okna albo opcji) po kolei; kazda akcja z linia wyniku. CZEKA - do listy czekajacych (zapis).</summary>
            internal static void Run(Ctx c, string[] effects)
            {
                if (effects == null || effects.Length == 0) return;
                int day = Terms.Day();
                for (int i = 0; i < effects.Length; i++)
                {
                    string why;
                    var r = RunOne(c, i + 1, effects[i], out why);
                    if (r == Res.Wait)
                    {
                        _dWait++;
                        _wait.Add(new Waiting { C = c, Idx = i + 1, Since = day, Act = effects[i], Why = why ?? "" });
                        Log.Info(Tag + "akcja " + c.Label + " #" + (i + 1) + " " + effects[i] + " -> CZEKA (" + why + ", od dnia " + day + ").");
                    }
                }
            }

            /// <summary>Jedna akcja: rejestr, klucz swiata, procedura. Linia OK/POMINIETA tu (CZEKA - wolajacy, zeby ponowienie nie pisalo co dobe).</summary>
            private static Res RunOne(Ctx c, int idx, string act, out string why)
            {
                why = null;
                string kind = act, rest = "";
                int col = act.IndexOf(':');
                if (col >= 0) { kind = act.Substring(0, col); rest = act.Substring(col + 1); }
                Res r;
                string detail = null;
                if (!Known.Contains(kind)) { r = Res.Skip; why = "nieznana akcja"; _dUnknown++; }
                else if (ClassOf(kind) == 'S' && !KeyWorld) { r = Res.Skip; why = "klucz Chronicle World Changes NIE (akcja swiata)"; }
                else
                {
                    Handler h;
                    if (!Impl.TryGetValue(kind, out h)) { r = Res.Skip; why = "akcja jeszcze niewspierana"; }
                    else
                    {
                        try { r = h(c, Terms.SplitArgs(rest), out detail); }
                        catch (Exception e) { Stumble("Actions(" + c.Label + " " + act + ")", e); r = Res.Skip; detail = "wyjatek " + e.GetType().Name; }
                        if (r != Res.Done) { why = detail; detail = null; }
                    }
                }
                if (r == Res.Done) { _dOk++; _sOk++; Log.Info(Tag + "akcja " + c.Label + " #" + idx + " " + act + " -> OK" + (detail != null ? " (" + detail + ")" : "") + "."); }
                else if (r == Res.Skip) { _dSkip++; _sSkip++; Log.Info(Tag + "akcja " + c.Label + " #" + idx + " " + act + " -> POMINIETA (" + (why ?? "?") + ")."); }
                return r;
            }

            /// <summary>Doba: akcje czekajace - ponowienie; dluzej niz WaitActionDays - POMINIETA. Linia tylko przy koncu czekania.</summary>
            internal static void Daily(int day)
            {
                for (int i = 0; i < _wait.Count; i++)
                {
                    var w = _wait[i];
                    if (day - w.Since > WaitActionDays)
                    {
                        _dSkip++; _sSkip++;
                        Log.Info(Tag + "akcja " + w.C.Label + " #" + w.Idx + " " + w.Act + " -> POMINIETA (czekala ponad " + WaitActionDays + " dni od dnia " + w.Since + ": " + w.Why + ").");
                        _wait.RemoveAt(i); i--;
                        continue;
                    }
                    string why;
                    var r = RunOne(w.C, w.Idx, w.Act, out why);
                    if (r == Res.Wait) { w.Why = why ?? w.Why; continue; }
                    _wait.RemoveAt(i); i--;
                }
            }

            /// <summary>Linie zapisu "kw id wiersz opcja nr dzien tokeny akcja" (akcja na koncu - moze miec ':'; bez TAB).</summary>
            internal static string Export()
            {
                var sb = new StringBuilder(80 * _wait.Count);
                foreach (var w in _wait)
                    sb.Append("kw\t").Append(w.C.Id).Append('\t').Append(w.C.RowKey).Append('\t').Append(w.C.Opt == '\0' ? "-" : w.C.Opt.ToString()).Append('\t')
                      .Append(w.Idx.ToString(Inv)).Append('\t').Append(w.Since.ToString(Inv)).Append('\t').Append(TokText(w.C.Tok)).Append('\t').Append(w.Act.Replace('\t', ' ')).Append('\n');
                return sb.ToString();
            }

            internal static void Import(string s)
            {
                _wait.Clear();
                if (string.IsNullOrEmpty(s)) return;
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    int idx, d;
                    if (p[0] != "kw" || p.Length < 8 || p[1].Length == 0 || p[7].Length == 0) continue;
                    if (!int.TryParse(p[4], NumberStyles.Integer, Inv, out idx) || !int.TryParse(p[5], NumberStyles.AllowLeadingSign, Inv, out d)) continue;
                    var c = new Ctx { Id = p[1], RowKey = p[2], Opt = p[3].Length == 1 && p[3] != "-" ? p[3][0] : '\0', Tok = TokParse(p[6]) };
                    var row = Data.Row(c.RowKey);
                    if (row != null) { string why; c.Vars = Vars(row, c.Tok, out why); if (why != null) c.Vars = null; }
                    _wait.Add(new Waiting { C = c, Idx = idx, Since = d, Act = p[7], Why = "z zapisu" });
                }
            }

            // ---------------------------------------------------------------- pomocnicze

            private static bool Num(string s, int min, int max, out int v)
            {
                return int.TryParse(s, NumberStyles.AllowLeadingSign, Inv, out v) && v >= min && v <= max;
            }

            private static string HeroName(Hero h) { return h != null ? h.Name + " (" + h.StringId + ")" : "-"; }

            /// <summary>Bohater zywy do akcji (null + powod: nie istnieje / nie zyje).</summary>
            private static Hero LiveHero(string id, Ctx c, out string why)
            {
                why = null;
                var h = Terms.FindHero(id, c.Tok);
                if (h == null) { why = "bohater " + id + " nie istnieje w grze"; return null; }
                if (h.IsDead) { why = "bohater " + id + " nie zyje"; return null; }
                return h;
            }

            /// <summary>Glowy rodow grupy: rod - jego glowa; krolestwo - glowy rodow krolestwa (bez najemnikow i wyeliminowanych). Pusta lista - grupa bez glow.</summary>
            private static List<Hero> Heads(string id, Ctx c, out string why)
            {
                why = null;
                var l = new List<Hero>();
                var cl = Terms.FindClan(id, c.Tok);
                if (cl != null)
                {
                    if (cl.Leader != null && cl.Leader.IsAlive) l.Add(cl.Leader);
                    else why = "rod " + id + " bez zywej glowy";
                    return l;
                }
                var k = Terms.FindKingdom(id, c.Tok);
                if (k == null) { why = "grupa " + id + " - ani rod, ani krolestwo w grze"; return l; }
                if (k.IsEliminated) { why = "krolestwo " + id + " nie istnieje (wyeliminowane)"; return l; }
                foreach (var x in k.Clans)
                    if (x != null && !x.IsEliminated && !x.IsUnderMercenaryService && x.Leader != null && x.Leader.IsAlive && !l.Contains(x.Leader)) l.Add(x.Leader);
                if (l.Count == 0) why = "krolestwo " + id + " bez glow rodow";
                return l;
            }

            // ---------------------------------------------------------------- procedury (A2)

            /// <summary>wiesc:&lt;id&gt; - linia wiesci dla gracza (tekst z #tekst, zmienne wiersza).</summary>
            private static Res News(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 1) { detail = "wiesc:<id> - zla liczba argumentow"; return Res.Skip; }
                string raw = Data.TextRaw(a[0]);
                if (raw == null) { detail = "brak tekstu " + a[0] + " w #tekst"; return Res.Skip; }
                ShowNews(TextOf(raw, c.Vars));
                detail = "tekst " + a[0];
                return Res.Done;
            }

            /// <summary>plotka:&lt;id&gt;:&lt;osada|krolestwo&gt;:&lt;dni&gt; - wpis w ksiedze wiesci Rumours (karczmy), bez lektora na zywo.</summary>
            private static Res Rumour(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int days;
                if (a.Count != 3 || !Num(a[2], 1, 120, out days)) { detail = "plotka:<id>:<osada|krolestwo>:<dni 1-120> - zle argumenty"; return Res.Skip; }
                if (Data.TextRaw(a[0]) == null) { detail = "brak tekstu " + a[0] + " w #tekst"; return Res.Skip; }
                string place = null;
                Settlement st = Terms.FindSettlement(a[1], c.Tok);
                if (st != null) place = st.StringId;
                else { var k = Terms.FindKingdom(a[1], c.Tok); if (k != null) place = k.StringId; }
                if (place == null) { detail = "miejsce " + a[1] + " - ani osada, ani krolestwo w grze"; return Res.Skip; }
                string why;
                if (!Rumours.AddStoryNews(a[0], place, days, true, out why)) { detail = why; return Res.Skip; }
                detail = "ksiega wiesci: " + a[0] + " w " + place + " na " + days + " dni";
                return Res.Done;
            }

            /// <summary>flag:&lt;nazwa&gt; - flaga do warunkow flag:.</summary>
            private static Res Flag(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 1 || a[0].Length == 0) { detail = "flag:<nazwa> - zle argumenty"; return Res.Skip; }
                bool had = State.Flag(a[0]);
                State.SetFlag(a[0]);
                detail = had ? "juz ustawiona" : "ustawiona";
                return Res.Done;
            }

            /// <summary>kod:&lt;nazwa&gt; - nazwany uchwyt C# (FORMAT 12). K0: tylko test; inne - POMINIETA "brak uchwytu" (okno idzie dalej).</summary>
            private static Res Code(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                string name = a.Count > 0 ? a[0] : "";
                if (name == "test")
                {
                    Log.Info(Tag + "kod test | okno " + c.Label + " | proba silnika, bez zmian w swiecie | OK.");
                    detail = "uchwyt test";
                    return Res.Done;
                }
                Log.Info(Tag + "kod " + name + " | okno " + c.Label + " | POMINIETA (brak uchwytu w tej wersji silnika).");
                detail = "brak uchwytu " + name;
                return Res.Skip;
            }

            /// <summary>rel:&lt;b1&gt;:&lt;b2&gt;:&lt;d&gt; - relacja dwoch postaci, bez powiadomienia (ChangeRelationAction - mnoznik wzrostu modelu dyplomacji jak w grze).</summary>
            private static Res Rel(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int d;
                if (a.Count != 3 || !Num(a[2], -100, 100, out d)) { detail = "rel:<b1>:<b2>:<d -100..100> - zle argumenty"; return Res.Skip; }
                var h1 = LiveHero(a[0], c, out detail); if (h1 == null) return Res.Skip;
                var h2 = LiveHero(a[1], c, out detail); if (h2 == null) return Res.Skip;
                if (h1 == h2) { detail = "ta sama postac"; return Res.Skip; }
                int before = h1.GetRelation(h2);
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h1, h2, d, false);
                detail = HeroName(h1) + " <-> " + HeroName(h2) + ": " + before + " -> " + h1.GetRelation(h2);
                return Res.Done;
            }

            /// <summary>relp:&lt;bohater&gt;:&lt;d&gt; - relacja gracza z postacia (z powiadomieniem gry).</summary>
            private static Res RelP(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int d;
                if (a.Count != 2 || !Num(a[1], -100, 100, out d)) { detail = "relp:<bohater>:<d -100..100> - zle argumenty"; return Res.Skip; }
                var me = Hero.MainHero;
                if (me == null) { detail = "brak gracza"; return Res.Skip; }
                var h = LiveHero(a[0], c, out detail); if (h == null) return Res.Skip;
                if (h == me) { detail = "postac gracza"; return Res.Skip; }
                int before = me.GetRelation(h);
                ChangeRelationAction.ApplyPlayerRelation(h, d, false, true);
                detail = "gracz <-> " + HeroName(h) + ": " + before + " -> " + me.GetRelation(h);
                return Res.Done;
            }

            /// <summary>relk:&lt;grupa&gt;:&lt;d&gt; (gracz z glowami rodow grupy) / relk:&lt;g1&gt;:&lt;g2&gt;:&lt;d&gt; (glowy grupy 1 z glowami grupy 2, bez gracza) - bez powiadomien.</summary>
            private static Res RelK(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int d;
                if ((a.Count != 2 && a.Count != 3) || !Num(a[a.Count - 1], -100, 100, out d)) { detail = "relk:<grupa>[:<grupa2>]:<d -100..100> - zle argumenty"; return Res.Skip; }
                var me = Hero.MainHero;
                var g1 = Heads(a[0], c, out detail);
                if (g1.Count == 0) return Res.Skip;
                int pairs = 0;
                if (a.Count == 2)
                {
                    if (me == null) { detail = "brak gracza"; return Res.Skip; }
                    long sb = 0, sa = 0;
                    foreach (var h in g1)
                    {
                        if (h == me) continue;
                        sb += me.GetRelation(h);
                        ChangeRelationAction.ApplyPlayerRelation(h, d, false, false);
                        sa += me.GetRelation(h);
                        pairs++;
                    }
                    if (pairs == 0) { detail = "grupa " + a[0] + " - tylko gracz"; return Res.Skip; }
                    detail = "gracz <-> glowy " + a[0] + ": " + pairs + " (srednio " + (sb / (double)pairs).ToString("0.0", Inv) + " -> " + (sa / (double)pairs).ToString("0.0", Inv) + ")";
                    return Res.Done;
                }
                var g2 = Heads(a[1], c, out detail);
                if (g2.Count == 0) return Res.Skip;
                foreach (var x in g1)
                    foreach (var y in g2)
                    {
                        if (x == y || x == me || y == me) continue;
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(x, y, d, false);
                        pairs++;
                    }
                if (pairs == 0) { detail = "grupy " + a[0] + " i " + a[1] + " bez par (te same postacie albo gracz)"; return Res.Skip; }
                detail = "glowy " + a[0] + " (" + g1.Count + ") <-> glowy " + a[1] + " (" + g2.Count + "): par " + pairs;
                return Res.Done;
            }

            /// <summary>renown:&lt;d&gt; (rod gracza) / renown:&lt;rod&gt;:&lt;d&gt;; plus - jak gra (GainRenownAction / Clan.AddRenown z progiem tieru), minus - slawa w dol do 0 (tier zostaje, gra go nie obniza).</summary>
            private static Res Renown(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int d;
                if ((a.Count != 1 && a.Count != 2) || !Num(a[a.Count - 1], -1000, 1000, out d) || d == 0) { detail = "renown:[<rod>:]<d -1000..1000> - zle argumenty"; return Res.Skip; }
                Clan cl = a.Count == 1 ? Clan.PlayerClan : Terms.FindClan(a[0], c.Tok);
                if (cl == null) { detail = "rod " + (a.Count == 1 ? "gracza" : a[0]) + " nie istnieje"; return Res.Skip; }
                if (cl.IsEliminated) { detail = "rod " + cl.StringId + " wyeliminowany"; return Res.Skip; }
                float before = cl.Renown;
                if (d > 0)
                {
                    if (cl == Clan.PlayerClan && Hero.MainHero != null) GainRenownAction.Apply(Hero.MainHero, d);
                    else cl.AddRenown(d, false);
                }
                else cl.Renown = Math.Max(0f, cl.Renown + d);
                detail = "rod " + cl.StringId + ": " + before.ToString("0", Inv) + " -> " + cl.Renown.ToString("0", Inv);
                return Res.Done;
            }

            /// <summary>influence:&lt;d&gt; (rod gracza) / influence:&lt;rod&gt;:&lt;d&gt; - wplywy (ChangeClanInfluenceAction).</summary>
            private static Res Influence(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int d;
                if ((a.Count != 1 && a.Count != 2) || !Num(a[a.Count - 1], -1000, 1000, out d) || d == 0) { detail = "influence:[<rod>:]<d> - zle argumenty"; return Res.Skip; }
                Clan cl = a.Count == 1 ? Clan.PlayerClan : Terms.FindClan(a[0], c.Tok);
                if (cl == null) { detail = "rod " + (a.Count == 1 ? "gracza" : a[0]) + " nie istnieje"; return Res.Skip; }
                if (cl.IsEliminated) { detail = "rod " + cl.StringId + " wyeliminowany"; return Res.Skip; }
                float before = cl.Influence;
                ChangeClanInfluenceAction.Apply(cl, d);
                detail = "rod " + cl.StringId + ": " + before.ToString("0.0", Inv) + " -> " + cl.Influence.ToString("0.0", Inv);
                return Res.Done;
            }
        }
    }
}
