using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

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
        /// A4: hide, unhide przez CanonCompanions.ApplyAction (rejestr ukrytych tam, w zapisie arm_canon_companions); koniec ukrycia (term) - HiddenDaily w dobie Kroniki.
        /// B1 (akcje swiata i ekonomii): gold (zamknieta ekonomia - platnik placi najwyzej to, co ma; linia "zloto"), security, loyalty (Town wprost, jak inne moduly -
        /// modele gry licza tylko dryf), ware (towar z rynku do rynku), garrison (z innej zalogi albo z ochotnikow regionu przez ksiege ludzi jak autowerbunek A5),
        /// item (od obecnego posiadacza, nigdy dragon_*), wound (rejestr ran w zapisie "kj" - doba trzyma rane), skill; pop - POMINIETA (ksiega ludzi bez przyczyny kronika).
        /// Wyjatek w akcji - POMINIETA z powodem i potkniecie; reszta skutkow idzie dalej (bez globalnego wylacznika).
        /// B2 (klasa partial - pliki obok): band (Chronicle.Bands.cs - rejestr band Kroniki, zapis "kb"), task (Chronicle.Tasks.cs - wlasny rejestr bez QuestBase,
        /// zapis "kz"), tourney i muster (zapis "km") oraz akcje kanonu clan/court/lead/prisoner/free/wander/die/marry/wed przez CanonCompanions.ApplyAction, succeed
        /// i tylko-pelne own/ruler/leader/alliance/truce (Chronicle.Canon.cs), uchwyty kod: test/zaraza/dzwony/wielka_rada (Chronicle.Codes.cs, RunCode).
        /// Doba swiata Kroniki (WorldDaily: bandy, zadania, zbiorki) biegnie PRZED kluczem ChronicleOfWesteros - klucz NIE wylacza okna, ale nic nie cofa.
        /// </summary>
        internal static partial class Actions
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
                { "hide", Hide }, { "unhide", Unhide },   // A4: przez CanonCompanions.ApplyAction (rejestr ukrytych w zapisie arm_canon_companions)
                { "gold", Gold }, { "security", Security }, { "loyalty", Loyalty }, { "pop", Pop }, { "ware", Ware }, { "garrison", Garrison },   // B1: swiat i ekonomia
                { "item", Item }, { "wound", Wound }, { "skill", Skill },
                { "band", Band }, { "task", Task }, { "tourney", Tourney }, { "muster", Muster },   // B2: Chronicle.Bands.cs, Chronicle.Tasks.cs, Chronicle.Canon.cs
                { "clan", CanonClan }, { "court", CanonCourt }, { "lead", CanonLead }, { "prisoner", CanonPrisoner }, { "free", CanonFree },
                { "wander", CanonWander }, { "die", CanonDie }, { "marry", CanonMarry }, { "wed", CanonWed }, { "succeed", Succeed },
                { "own", Own }, { "ruler", Ruler }, { "leader", Leader }, { "alliance", Alliance }, { "truce", Truce },
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

            /// <summary>B1: rana z akcji wound - bohater ranny do dnia Until (dzien kampanii); zapis "kj bohater do zrodlo".</summary>
            private sealed class WoundRow
            {
                public string Hero = "", Src = "";
                public int Until;
            }

            private static readonly List<WoundRow> _wounds = new List<WoundRow>();
            internal static int WoundCount { get { return _wounds.Count; } }

            internal static int WaitCount { get { return _wait.Count; } }
            /// <summary>Bandy Kroniki na mapie (B2: rejestr Chronicle.Bands - wpisy z zywa partia przy ostatnim przegladzie). Do linii doby (prog KW-14).</summary>
            internal static int Bands { get { return BandCount; } }
            /// <summary>Postacie ukryte przez Armoury (A4: rejestr CanonCompanions - akcja hide z Kroniki albo z wiersza #move). Do linii startu i doby.</summary>
            internal static int Hidden { get { try { return CanonCompanions.HiddenCount; } catch { return 0; } } }

            /// <summary>A4: ukryci do linii startu/doby - "n (Kronika a, #move b)".</summary>
            internal static string HiddenText()
            {
                int kr = 0, mv = 0;
                try
                {
                    foreach (var r in CanonCompanions.HiddenRows())
                        if (r.Src.StartsWith("Kronika", StringComparison.Ordinal)) kr++; else mv++;
                }
                catch (Exception e) { Stumble("Actions.HiddenText", e); }
                return (kr + mv) + " (Kronika " + kr + ", #move " + mv + ")";
            }

            internal static void Reset() { _wait.Clear(); _wounds.Clear(); BandsReset(); TasksReset(); MusterReset(); DayReset(); _sOk = _sSkip = 0; }

            internal static void DayReset() { _dOk = _dSkip = _dWait = _dUnknown = 0; }

            internal static string DayText()
            {
                return "akcje ok " + _dOk + ", pominiete " + _dSkip + ", czeka " + _wait.Count + " (nowe dzis " + _dWait + ")" + (_dUnknown > 0 ? ", NIEZNANE " + _dUnknown : "")
                       + (_wounds.Count > 0 ? ", rany " + _wounds.Count : "") + ", zadania " + TaskCount + ", zbiorki " + MusterCount;
            }

            /// <summary>B2: doba swiata Kroniki - bandy (koniec terminu, rozbite, zniknely), zadania (termin, dowiezienie), zbiorki (koniec) - PRZED kluczem
            /// ChronicleOfWesteros (klucz NIE nic nie cofa: banda zyje swoj termin, zadanie mozna dokonczyc). Kazda czesc we wlasnym try.</summary>
            internal static void WorldDaily(int day)
            {
                try { BandsDaily(day); } catch (Exception e) { Stumble("Actions.BandsDaily", e); }
                try { TasksDaily(day); } catch (Exception e) { Stumble("Actions.TasksDaily", e); }
                try { MusterDaily(day); } catch (Exception e) { Stumble("Actions.MusterDaily", e); }
            }

            /// <summary>B2: do linii startu sesji - bandy, zadania, zbiorki.</summary>
            internal static string WorldText() { return "bandy " + BandCount + "/" + MaxBands + ", zadania " + TaskCount + ", zbiorki " + MusterCount; }

            /// <summary>Numer wydarzenia NNN z kontekstu (okno NNN.w, instancja NNN.w#klucz, banda bNNN, zadanie tNNN); -1 - brak.</summary>
            internal static int EventNo(string id)
            {
                if (string.IsNullOrEmpty(id)) return -1;
                for (int i = 0; i + 2 < id.Length; i++)
                {
                    if (char.IsDigit(id[i]) && char.IsDigit(id[i + 1]) && char.IsDigit(id[i + 2]))
                    {
                        int v;
                        return int.TryParse(id.Substring(i, 3), NumberStyles.None, Inv, out v) ? v : -1;
                    }
                }
                return -1;
            }

            /// <summary>Wersja wiersza kontekstu (lekka|pelna|obie); "" - bez wiersza (po_rozbiciu bandy, nagroda zadania).</summary>
            internal static string RowVersion(Ctx c)
            {
                var r = c != null ? Data.Row(c.RowKey) : null;
                return r != null ? r.Version : "";
            }

            /// <summary>m107-kronika: kontekst z wiersza wersja=smierc (smierc z kanonu w wersji lekkiej) - dopisek "| smierc kanonu" w liniach akcji (progi KW-17).</summary>
            internal static bool IsDeathRow(Ctx c) { return RowVersion(c) == "smierc"; }

            private const string DeathMark = " | smierc kanonu";

            /// <summary>m107: smierc z wiersza smierc (succeed/die) nie weszla - linia "KANON: smierc z NNN.wX nie weszla" (KW-17 a).</summary>
            private static void DeathNotIn(Ctx c, string act, string why)
            {
                string kind = act;
                int col = act.IndexOf(':');
                if (col >= 0) kind = act.Substring(0, col);
                if (kind != "succeed" && kind != "die") return;
                Log.Info(Tag + "KANON: smierc z " + c.RowKey + (c.Opt != '\0' ? "=" + c.Opt : "") + " nie weszla - " + act + " (" + (why ?? "?") + ").");
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
                        Log.Info(Tag + "akcja " + c.Label + " #" + (i + 1) + " " + effects[i] + " -> CZEKA (" + why + (IsDeathRow(c) ? DeathMark : "") + ", od dnia " + day + ").");
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
                bool death = IsDeathRow(c);   // m107: akcja z wiersza smierc - dopisek w linii (KW-17)
                if (r == Res.Done)
                {
                    _dOk++; _sOk++;
                    Log.Info(Tag + "akcja " + c.Label + " #" + idx + " " + act + " -> OK" + (detail != null ? " (" + detail + (death ? DeathMark : "") + ")" : death ? " (smierc kanonu)" : "") + ".");
                }
                else if (r == Res.Skip)
                {
                    _dSkip++; _sSkip++;
                    Log.Info(Tag + "akcja " + c.Label + " #" + idx + " " + act + " -> POMINIETA (" + (why ?? "?") + (death ? DeathMark : "") + ").");
                    if (death) DeathNotIn(c, act, why);
                }
                return r;
            }

            /// <summary>Doba: akcje czekajace - ponowienie; dluzej niz WaitActionDays - POMINIETA. Linia tylko przy koncu czekania.</summary>
            internal static void Daily(int day)
            {
                WoundsDaily(day);
                for (int i = 0; i < _wait.Count; i++)
                {
                    var w = _wait[i];
                    if (day - w.Since > WaitActionDays)
                    {
                        _dSkip++; _sSkip++;
                        bool death = IsDeathRow(w.C);
                        string late = "czekala ponad " + WaitActionDays + " dni od dnia " + w.Since + ": " + w.Why;
                        Log.Info(Tag + "akcja " + w.C.Label + " #" + w.Idx + " " + w.Act + " -> POMINIETA (" + late + (death ? DeathMark : "") + ").");
                        if (death) DeathNotIn(w.C, w.Act, late);
                        _wait.RemoveAt(i); i--;
                        continue;
                    }
                    string why;
                    var r = RunOne(w.C, w.Idx, w.Act, out why);
                    if (r == Res.Wait) { w.Why = why ?? w.Why; continue; }
                    _wait.RemoveAt(i); i--;
                }
            }

            /// <summary>Linie zapisu "kw id wiersz opcja nr dzien tokeny akcja" (akcja na koncu - moze miec ':'; bez TAB) i B1 "kj bohater do zrodlo" (rany).</summary>
            internal static string Export()
            {
                var sb = new StringBuilder(80 * _wait.Count + 40 * _wounds.Count);
                foreach (var w in _wait)
                    sb.Append("kw\t").Append(w.C.Id).Append('\t').Append(w.C.RowKey).Append('\t').Append(w.C.Opt == '\0' ? "-" : w.C.Opt.ToString()).Append('\t')
                      .Append(w.Idx.ToString(Inv)).Append('\t').Append(w.Since.ToString(Inv)).Append('\t').Append(TokText(w.C.Tok)).Append('\t').Append(w.Act.Replace('\t', ' ')).Append('\n');
                foreach (var r in _wounds)
                    sb.Append("kj\t").Append(r.Hero).Append('\t').Append(r.Until.ToString(Inv)).Append('\t').Append(r.Src.Replace('\t', ' ')).Append('\n');
                BandsExport(sb);     // B2: "kb" bandy Kroniki
                TasksExport(sb);     // B2: "kz" zadania
                MusterExport(sb);    // B2: "km" zbiorki
                return sb.ToString();
            }

            internal static void Import(string s)
            {
                _wait.Clear();
                _wounds.Clear();
                BandsReset(); TasksReset(); MusterReset();
                if (string.IsNullOrEmpty(s)) return;
                try { BandsImport(s); } catch (Exception e) { Stumble("Actions.BandsImport", e); }
                try { TasksImport(s); } catch (Exception e) { Stumble("Actions.TasksImport", e); }
                try { MusterImport(s); } catch (Exception e) { Stumble("Actions.MusterImport", e); }
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    int idx, d;
                    if (p[0] == "kj")
                    {
                        if (p.Length >= 3 && p[1].Length > 0 && int.TryParse(p[2], NumberStyles.AllowLeadingSign, Inv, out d))
                            _wounds.Add(new WoundRow { Hero = p[1], Until = d, Src = p.Length > 3 ? p[3] : "" });
                        continue;
                    }
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
                if (KingInTheNorth.IsRiverAlias(id))
                {
                    // 272: po unii grupa "riverlands" = glowy rodow z unii, ktore sa dalej w Polnocy
                    foreach (var x in KingInTheNorth.RiverLords()) if (x.Leader != null && x.Leader.IsAlive && !l.Contains(x.Leader)) l.Add(x.Leader);
                    if (l.Count == 0) why = "grupa " + id + " (rody Dorzecza w Polnocy po unii 272) bez glow rodow";
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
                else if (KingInTheNorth.IsRiverAlias(a[1]) && KingInTheNorth.SeatId.Length > 0) place = KingInTheNorth.SeatId;   // 272: po unii - siedziba Dorzecza z chwili unii
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

            /// <summary>kod:&lt;nazwa&gt; - nazwany uchwyt C# (FORMAT 12): Chronicle.Codes.cs RunCode (test, zaraza, dzwony, wielka_rada); nieznany - POMINIETA "brak uchwytu"
            /// (okno idzie dalej). Uchwyt sam pisze jedna linie "kod nazwa | okno ... | ... | OK/POMINIETA (powod)".</summary>
            private static Res Code(Ctx c, List<string> a, out string detail)
            {
                string name = a.Count > 0 ? a[0] : "";
                return RunCode(name, c, out detail);
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

            // ---------------------------------------------------------------- ukrycie i powrot (A4, plan P3, FORMAT 11: przez CanonCompanions.ApplyAction)

            private static Res FromCanon(CanonCompanions.Res r) { return r == CanonCompanions.Res.Done ? Res.Done : r == CanonCompanions.Res.Wait ? Res.Wait : Res.Skip; }

            /// <summary>hide:&lt;bohater&gt;[:&lt;term konca&gt;] - ukrycie (LeaveParty + DisableHeroAction w CanonCompanions); term konca moze miec ':' (SplitArgs sklejany
            /// z powrotem). Glowa rodu, wladca, postac gracza, lista chronionych przed wydarzeniem - POMINIETA; jeniec, gubernator, bitwa - CZEKA; ukryty - nowy koniec.</summary>
            private static Res Hide(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count < 1 || a[0].Length == 0) { detail = "hide:<bohater>[:<term konca>] - zle argumenty"; return Res.Skip; }
                string end = a.Count > 1 ? string.Join(":", a.GetRange(1, a.Count - 1).ToArray()) : "";
                string sw;
                if (end.Length > 0 && !Terms.Syntax(end, out sw)) { detail = "koniec ukrycia " + end + " - " + sw; return Res.Skip; }
                var h = Terms.FindHero(a[0], c.Tok);
                if (h == null) { detail = "bohater " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                string why;
                var r = FromCanon(CanonCompanions.ApplyAction(h, "hide", end, "Kronika " + c.Label, out why));
                detail = why;
                if (r == Res.Done)
                    Log.Info(Tag + "ukryty " + HeroName(h) + " (" + c.Label + ") do " + (end.Length > 0 ? end : "unhide (bez terminu)") + " | " + why + ".");
                return r;
            }

            /// <summary>unhide:&lt;bohater&gt;[:&lt;osada&gt;] - powrot ukrytego przez Armoury (teleport do osady z akcji albo siedziby rodu); nieukryty - POMINIETA "nie ukryty".</summary>
            private static Res Unhide(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count < 1 || a.Count > 2 || a[0].Length == 0) { detail = "unhide:<bohater>[:<osada>] - zle argumenty"; return Res.Skip; }
                var h = Terms.FindHero(a[0], c.Tok);
                if (h == null) { detail = "bohater " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                string place = "";
                if (a.Count > 1)
                {
                    var st = Terms.FindSettlement(a[1], c.Tok);
                    place = st != null ? st.StringId : a[1];   // osada nieznana - CanonCompanions bierze siedzibe rodu (z powodem w linii)
                }
                string why;
                var r = FromCanon(CanonCompanions.ApplyAction(h, "unhide", place, "Kronika " + c.Label, out why));
                detail = why;
                if (r == Res.Done)
                    Log.Info(Tag + "powrot " + HeroName(h) + " -> " + (h.CurrentSettlement != null ? h.CurrentSettlement.StringId : "-") + " (" + c.Label + ") | " + why + ".");
                return r;
            }

            /// <summary>Doba Kroniki (przed kluczem ChronicleOfWesteros): rejestr ukrytych - postac zmarla / przywrocona przez gre - wpis usuniety z linia
            /// "UKRYCIE przerwane"; ukryty zostal glowa rodu albo wladca (gra) - powrot "KONIEC UKRYCIA (glowa rodu)"; koniec ukrycia: term prawdziwy albo "nigdy"
            /// (FORMAT 11 - np. kr: okna pominietego) - powrot do siedziby rodu z linia "KONIEC UKRYCIA". Recenzja K0 (R-1): takze przy kluczu ChronicleOfWesteros NIE -
            /// wtedy kr: liczy sie jako "nigdy" (FORMAT 3.3), wiec ukryci z koncem kr: wracaja (bez tego klucz NIE trzymal ich ukrytych na zawsze, a rotday: nie dzialal).</summary>
            internal static void HiddenDaily(int day)
            {
                if (CanonCompanions.HiddenCount == 0) return;
                foreach (var r in CanonCompanions.HiddenRows())
                {
                    try
                    {
                        var h = CanonCompanions.HiddenHero(r);
                        string drop = h == null ? "brak postaci w grze" : h.IsDead ? "nie zyje (smierc w ukryciu - gra albo inny mod)"
                                      : !h.IsDisabled ? "przywrocony przez gre albo inny mod (stan " + h.HeroState + ")" : null;
                        if (drop != null)
                        {
                            CanonCompanions.DropHidden(r);
                            Log.Info(Tag + "UKRYCIE przerwane " + (h != null ? HeroName(h) : r.Key) + " (" + r.Src + ", od dnia " + r.Day + ") - " + drop + " - wpis usuniety.");
                            continue;
                        }
                        string reason = null;
                        if (h.IsClanLeader || h.IsKingdomLeader) reason = (h.IsKingdomLeader ? "wladca" : "glowa rodu") + " (gra wybrala ukrytego)";
                        else if (r.End.Length > 0)
                        {
                            bool nv; string w;
                            if (Terms.Eval(r.End, null, "koniec ukrycia " + r.Key, out nv, out w)) reason = "term prawdziwy: " + r.End;
                            else if (nv) reason = "term nigdy: " + (w ?? r.End);
                        }
                        if (reason == null) continue;
                        string why;
                        var res = CanonCompanions.ApplyAction(h, "unhide", "", "koniec ukrycia", out why);
                        if (res == CanonCompanions.Res.Done)
                        {
                            _sOk++;
                            Log.Info(Tag + "KONIEC UKRYCIA " + HeroName(h) + " (" + r.Src + ", od dnia " + r.Day + ") - " + reason + " | powrot " + HeroName(h) + " -> "
                                     + (h.CurrentSettlement != null ? h.CurrentSettlement.StringId : "-") + " | " + why + ".");
                        }
                        else if (res == CanonCompanions.Res.Skip)
                            Log.Info(Tag + "KONIEC UKRYCIA " + HeroName(h) + " (" + r.Src + ") - " + reason + " - powrot POMINIETY (" + why + ").");
                        // Wait (brak osady powrotu) - nastepna doba
                    }
                    catch (Exception e) { Stumble("Actions.HiddenDaily(" + r.Key + ")", e); }
                }
            }

            // ---------------------------------------------------------------- swiat i ekonomia (B1, FORMAT 11)

            /// <summary>Kiesa platnika/odbiorcy zlota: bohater (kiesa), rod (kiesa glowy), osada (kasa SettlementComponent), @skarbiec:k (KingdomBudgetWallet).</summary>
            private sealed class Purse
            {
                public Hero H;
                public Settlement S;
                public Kingdom K;
                public string Label = "";

                internal long Have
                {
                    get
                    {
                        if (H != null) return H.Gold;
                        if (S != null) return S.SettlementComponent != null ? S.SettlementComponent.Gold : 0;
                        return K != null ? K.KingdomBudgetWallet : 0;
                    }
                }

                /// <summary>Ile jeszcze zmiesci kiesa (int gry) - odbiorca nigdy nie dostaje mniej, niz zeszlo z platnika.</summary>
                internal long Room { get { return int.MaxValue - Math.Max(0L, Have); } }

                internal bool Same(Purse o)
                {
                    return o != null && ((H != null && H == o.H) || (S != null && S == o.S) || (K != null && K == o.K));
                }
            }

            /// <summary>Kiesa osady: miasto, zamek albo wies (kasa SettlementComponent). Kryjowka i inne - null z powodem.</summary>
            private static Purse SettlementPurse(Settlement st, string id, out string why)
            {
                why = null;
                if (st == null) { why = "osada " + id + " nie istnieje w grze"; return null; }
                if (st.SettlementComponent == null || !(st.IsTown || st.IsCastle || st.IsVillage)) { why = "osada " + st.StringId + " bez kasy (tylko miasto, zamek, wies)"; return null; }
                return new Purse { S = st, Label = "kasa " + st.StringId };
            }

            /// <summary>Platnik/odbiorca zlota (FORMAT 11): @skarbiec:k, @kasa:osada, bohater (id i tokeny @gracz/@kto/@wladca/@glowa/@pan), rod (glowa rodu; rod gracza - gracz),
            /// osada (kasa). Bohater martwy albo poza swiatem (ukryty/wylaczony - jego kiesa nie jest w obiegu) - null z powodem.</summary>
            private static Purse PurseOf(string id, Ctx c, out string why)
            {
                why = null;
                if (string.IsNullOrEmpty(id)) { why = "pusty identyfikator"; return null; }
                if (id.StartsWith("@skarbiec:", StringComparison.Ordinal))
                {
                    string kid = id.Substring(10);
                    var k = Terms.FindKingdom(kid, c.Tok);
                    if (k == null) { why = "krolestwo " + kid + " nie istnieje w grze"; return null; }
                    if (k.IsEliminated) { why = "krolestwo " + k.StringId + " wyeliminowane"; return null; }
                    return new Purse { K = k, Label = "skarbiec " + k.StringId };
                }
                if (id.StartsWith("@kasa:", StringComparison.Ordinal)) return SettlementPurse(Terms.FindSettlement(id.Substring(6), c.Tok), id.Substring(6), out why);
                if (id == "@cel") return SettlementPurse(Terms.FindSettlement(id, c.Tok), id, out why);
                Hero h = null;
                Clan cl = null;
                if (id == "@rodkto") cl = Terms.FindClan(id, c.Tok);
                else
                {
                    h = Terms.FindHero(id, c.Tok);
                    if (h == null && id[0] != '@')
                    {
                        cl = Terms.FindClan(id, c.Tok);
                        if (cl == null)
                        {
                            var st = Terms.FindSettlement(id, c.Tok);
                            if (st != null) return SettlementPurse(st, id, out why);
                        }
                    }
                }
                if (h == null && cl == null) { why = id + (id[0] == '@' ? " - brak postaci w grze" : " - ani bohater, ani rod, ani osada w grze"); return null; }
                string label;
                if (cl != null)
                {
                    if (cl.IsEliminated) { why = "rod " + cl.StringId + " wyeliminowany"; return null; }
                    h = cl == Clan.PlayerClan ? Hero.MainHero : cl.Leader;
                    if (h == null) { why = "rod " + cl.StringId + " bez glowy"; return null; }
                    label = "rod " + cl.StringId + " (glowa " + HeroName(h) + ")";
                }
                else label = HeroName(h);
                if (h.IsDead) { why = HeroName(h) + " nie zyje"; return null; }
                if (h.HeroState == Hero.CharacterStates.Disabled) { why = HeroName(h) + " poza swiatem (ukryty albo wylaczony - kiesa poza obiegiem)"; return null; }
                return new Purse { H = h, Label = label };
            }

            /// <summary>Przelew x (x &lt;= kiesa platnika i &lt;= miejsce odbiorcy). Bez skarbca - zdarzenie gry GiveGoldAction (ksiegi i powiadomienie gracza jak w grze;
            /// osada placi przez swoja partie - gra obcina do kasy). Ze skarbcem - arytmetyka: najpierw platnik, potem odbiorca dostaje dokladnie to, co zeszlo;
            /// skarbiec - przelew poza wplywami dnia (CrownIncome.ShiftSnapshot, jak CapitalTreasury), kiesa bohatera bez zdarzenia - liczniki ksiegi obiegu i rodow.</summary>
            private static void MoveGold(Purse from, Purse to, int x)
            {
                if (x <= 0) return;
                if (from.K == null && to.K == null)
                {
                    if (from.H != null && to.H != null) GiveGoldAction.ApplyBetweenCharacters(from.H, to.H, x, false);
                    else if (from.H != null) GiveGoldAction.ApplyForCharacterToSettlement(from.H, to.S, x, false);
                    else if (to.H != null) GiveGoldAction.ApplyForPartyToCharacter(from.S.Party, to.H, x, false);
                    else GiveGoldAction.ApplyForPartyToParty(from.S.Party, to.S.Party, x, true);
                    return;
                }
                long f0 = from.Have;
                if (from.K != null) from.K.KingdomBudgetWallet -= x;
                else if (from.H != null) from.H.ChangeHeroGold(-x);
                else from.S.SettlementComponent.ChangeGold(-x);
                int moved = (int)Math.Max(0L, f0 - from.Have);
                if (moved <= 0) return;
                if (to.K != null) to.K.KingdomBudgetWallet += moved;
                else if (to.H != null) to.H.ChangeHeroGold(moved);
                else to.S.SettlementComponent.ChangeGold(moved);
                try
                {
                    if (from.K != null) CrownIncome.ShiftSnapshot(from.K, -moved);
                    if (to.K != null) CrownIncome.ShiftSnapshot(to.K, moved);
                    if (from.H != null) CirculationWindows.NoteHeroGold(from.H, -moved);
                    if (to.H != null)
                    {
                        CirculationWindows.NoteHeroGold(to.H, moved);
                        if (to.H != Hero.MainHero) ClanIncomeBook.NoteInflow(to.H, moved, ClanIncomeBook.KRansom);   // jednorazowy wplyw od prawdziwego platnika (jak CapitalTreasury)
                    }
                }
                catch (Exception e) { Stumble("Actions.MoveGold(ksiegi)", e); }
                try
                {
                    var k = from.K ?? to.K;
                    string kn = k.Name != null ? k.Name.ToString() : k.StringId;
                    if (to.H == Hero.MainHero) Log.Player(moved.ToString("N0", Inv) + " denars from the royal treasury of " + kn + " are now in your purse.");
                    else if (from.H == Hero.MainHero) Log.Player("You paid " + moved.ToString("N0", Inv) + " denars into the royal treasury of " + kn + ".");
                }
                catch (Exception e) { Stumble("Actions.MoveGold(wiadomosc)", e); }
            }

            /// <summary>gold:&lt;platnik&gt;:&lt;odbiorca&gt;:&lt;kwota 1-1000000 | N% 1-100&gt; - zamknieta ekonomia: platnik placi najwyzej to, co ma (N% - jego zlota); niedoplata z linia.
            /// Linia "zloto NNN.w: platnik -> odbiorca kwota (zaplacone k...)" przy kazdej probie. Nic zaplacone - POMINIETA.</summary>
            private static Res Gold(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int v;
                string amt = a.Count == 3 ? a[2] : "";
                bool pct = amt.EndsWith("%", StringComparison.Ordinal);
                if (a.Count != 3 || !(pct ? Num(amt.Substring(0, amt.Length - 1), 1, 100, out v) : Num(amt, 1, 1000000, out v)))
                { detail = "gold:<platnik>:<odbiorca>:<kwota 1-1000000|N% 1-100> - zle argumenty"; return Res.Skip; }
                string why;
                var from = PurseOf(a[0], c, out why);
                if (from == null) { detail = "platnik: " + why; return Res.Skip; }
                var to = PurseOf(a[1], c, out why);
                if (to == null) { detail = "odbiorca: " + why; return Res.Skip; }
                if (from.Same(to)) { detail = "platnik = odbiorca (" + from.Label + ")"; return Res.Skip; }
                long have = Math.Max(0L, from.Have);
                long want = pct ? have * v / 100 : v;
                long pay = Math.Min(Math.Min(want, have), to.Room);
                long f0 = from.Have, t0 = to.Have;
                if (pay > 0) MoveGold(from, to, (int)pay);
                long f1 = from.Have, t1 = to.Have, taken = f0 - f1, got = t1 - t0;
                var sb = new StringBuilder(200);
                sb.Append("zloto ").Append(c.Label).Append(": ").Append(from.Label).Append(" -> ").Append(to.Label).Append(' ').Append(want.ToString(Inv))
                  .Append(" (zaplacone ").Append(got.ToString(Inv));
                if (want > got) sb.Append(", niedoplata ").Append((want - got).ToString(Inv));
                if (pct) sb.Append(", ").Append(v.ToString(Inv)).Append("% z ").Append(have.ToString(Inv));
                if (taken != got) sb.Append(", zdjete ").Append(taken.ToString(Inv)).Append(" - dopisane ").Append(got.ToString(Inv));
                sb.Append(").");
                Log.Info(Tag + sb.ToString());
                if (got <= 0)
                {
                    detail = "nic zaplacone - " + (have <= 0 ? "platnik bez zlota" : want <= 0 ? "kwota 0 (" + v + "% z " + have + ")" : "odbiorca bez miejsca w kiesie")
                             + " (platnik " + f0 + ", odbiorca " + t0 + ")";
                    return Res.Skip;
                }
                detail = "platnik " + f0 + " -> " + f1 + ", odbiorca " + t0 + " -> " + t1 + (want > got ? ", niedoplata " + (want - got) : "");
                return Res.Done;
            }

            /// <summary>security:&lt;osada&gt;:&lt;d&gt; - bezpieczenstwo miasta/zamku (Town.Security, gra trzyma 0-100).</summary>
            private static Res Security(Ctx c, List<string> a, out string detail) { return TownStat(c, a, true, out detail); }

            /// <summary>loyalty:&lt;osada&gt;:&lt;d&gt; - lojalnosc miasta/zamku (Town.Loyalty, gra trzyma 0-100).</summary>
            private static Res Loyalty(Ctx c, List<string> a, out string detail) { return TownStat(c, a, false, out detail); }

            /// <summary>Zmiana wprost jak inne moduly (CitySack, TownDebt, KingdomTreasury.LordLevy) - DailyLife i ksiega ludzi tych wartosci nie nadpisuja;
            /// modele gry (BKLoyaltyModel, model bezpieczenstwa NavalDLC + KsHunger) licza tylko dobowy dryf od nowej wartosci.</summary>
            private static Res TownStat(Ctx c, List<string> a, bool sec, out string detail)
            {
                detail = null;
                string nm = sec ? "security" : "loyalty";
                int d;
                if (a.Count != 2 || !Num(a[1], -100, 100, out d)) { detail = nm + ":<osada>:<d -100..100> - zle argumenty"; return Res.Skip; }
                var st = Terms.FindSettlement(a[0], c.Tok);
                if (st == null) { detail = "osada " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                var t = st.Town;
                if (t == null) { detail = nm + " tylko dla miasta albo zamku (" + st.StringId + ")"; return Res.Skip; }
                float before = sec ? t.Security : t.Loyalty;
                if (sec) t.Security = before + d; else t.Loyalty = before + d;
                float after = sec ? t.Security : t.Loyalty;
                detail = "osada " + st.StringId + ": " + before.ToString("0.0", Inv) + " -> " + after.ToString("0.0", Inv) + (Math.Abs(after - before - d) > 0.01f ? " (granica 0-100)" : "");
                return Res.Done;
            }

            /// <summary>pop:&lt;osada&gt;:&lt;d&gt; - FORMAT 11: ludzie przez ksiege ludzi z przyczyna "kronika" (minus do puli regionu, plus z puli). Ksiega (PeopleBook) nie ma
            /// takiej przyczyny ani drogi osada &lt;-&gt; pula wyrzutkow dla Kroniki - cudza przyczyna (rabunek, podloga, migracja) zepsulaby jej rownania i progi.
            /// Dlatego POMINIETA z powodem "akcja jeszcze niewspierana" (KW-5b INFO), bez zmiany ludzi; linia mowi, ilu ludzi ma osada.</summary>
            private static Res Pop(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int d;
                if (a.Count != 2 || !Num(a[1], -100000, 100000, out d)) { detail = "pop:<osada>:<d> - zle argumenty"; return Res.Skip; }
                var st = Terms.FindSettlement(a[0], c.Tok);
                if (st == null) { detail = "osada " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                float people = 0f;
                try { people = PopulationLaw.PeopleOf(st); } catch (Exception e) { Stumble("Actions.Pop(ludzie)", e); }
                detail = "akcja jeszcze niewspierana: ksiega ludzi bez przyczyny kronika (PeopleBook - brak drogi osada <-> pula regionu dla Kroniki), ludzie bez zmian; osada "
                         + st.StringId + " ok. " + people.ToString("0", Inv) + " ludzi, zmiana " + d;
                return Res.Skip;
            }

            private static ItemObject ItemById(string id)
            {
                if (string.IsNullOrEmpty(id) || MBObjectManager.Instance == null) return null;
                try { return MBObjectManager.Instance.GetObject<ItemObject>(id); } catch { return null; }
            }

            private static int CountIn(ItemRoster r, ItemObject item)
            {
                int n = 0;
                if (r == null || item == null) return 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == item && el.Amount > 0) n += el.Amount;
                }
                return n;
            }

            /// <summary>ware:&lt;z&gt;:&lt;do&gt;:&lt;towar&gt;:&lt;ilosc&gt; - towar z rynku osady do rynku osady (Settlement.ItemRoster), najwyzej tyle, ile zrodlo ma (nic z niczego);
            /// sztuki z modyfikatorami jak leza. Bez zlota (przeniesienie fabularne).</summary>
            private static Res Ware(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int n;
                if (a.Count != 4 || !Num(a[3], 1, 100000, out n)) { detail = "ware:<z osady>:<do osady>:<towar>:<ilosc 1-100000> - zle argumenty"; return Res.Skip; }
                var from = Terms.FindSettlement(a[0], c.Tok);
                if (from == null) { detail = "osada " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                var to = Terms.FindSettlement(a[1], c.Tok);
                if (to == null) { detail = "osada " + a[1] + " nie istnieje w grze"; return Res.Skip; }
                if (from == to) { detail = "ta sama osada " + from.StringId; return Res.Skip; }
                if (!(from.IsTown || from.IsCastle || from.IsVillage) || !(to.IsTown || to.IsCastle || to.IsVillage) || from.ItemRoster == null || to.ItemRoster == null)
                { detail = "rynek tylko w miescie, zamku albo wsi (" + from.StringId + " -> " + to.StringId + ")"; return Res.Skip; }
                var item = ItemById(a[2]);
                if (item == null) { detail = "nieznany towar " + a[2]; return Res.Skip; }
                int f0 = CountIn(from.ItemRoster, item), t0 = CountIn(to.ItemRoster, item);
                int take = Math.Min(n, f0);
                if (take <= 0) { detail = "brak towaru " + item.StringId + " na rynku " + from.StringId + " (0 z " + n + ")"; return Res.Skip; }
                var parts = new List<KeyValuePair<EquipmentElement, int>>();
                int left = take;
                for (int i = 0; i < from.ItemRoster.Count && left > 0; i++)
                {
                    var el = from.ItemRoster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item != item || el.Amount <= 0) continue;
                    int t = Math.Min(el.Amount, left);
                    parts.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, t));
                    left -= t;
                }
                foreach (var p in parts)
                {
                    from.ItemRoster.AddToCounts(p.Key, -p.Value);
                    to.ItemRoster.AddToCounts(p.Key, p.Value);
                }
                int f1 = CountIn(from.ItemRoster, item), t1 = CountIn(to.ItemRoster, item);
                detail = "towar " + item.StringId + ": " + from.StringId + " " + f0 + " -> " + f1 + ", " + to.StringId + " " + t0 + " -> " + t1
                         + " (przeniesione " + (f0 - f1) + (take < n ? ", brak " + (n - take) : "") + ")";
                return Res.Done;
            }

            private static bool InBattle(MobileParty mp) { return mp != null && (mp.MapEvent != null || (mp.Party != null && mp.Party.MapEvent != null)); }

            /// <summary>garrison:&lt;osada&gt;:&lt;ludzi 1-2000&gt;:&lt;skad&gt; - wojsko do zalogi miasta/zamku: skad = osada (zdrowi zolnierze z jej zalogi, proporcjonalnie do skladu;
            /// ksiega ludzi - PeopleBook.Transfer) albo pula (ochotnicy notabli osady i jej wsi - sloty VolunteerTypes; ksiega ludzi jak autowerbunek zalogi A5: rekrut zdjety
            /// z DOM regionu). Najwyzej tyle, ile jest (nic z niczego); niedobor w linii. Zaloga w bitwie - CZEKA.</summary>
            private static Res Garrison(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int n;
                if (a.Count != 3 || !Num(a[1], 1, 2000, out n)) { detail = "garrison:<osada>:<ludzi 1-2000>:<skad: osada|pula> - zle argumenty"; return Res.Skip; }
                var st = Terms.FindSettlement(a[0], c.Tok);
                if (st == null) { detail = "osada " + a[0] + " nie istnieje w grze"; return Res.Skip; }
                if (st.Town == null) { detail = "zaloga tylko w miescie albo zamku (" + st.StringId + ")"; return Res.Skip; }
                var gar = st.Town.GarrisonParty;
                if (gar == null || gar.MemberRoster == null) { detail = "osada " + st.StringId + " bez zalogi (GarrisonParty)"; return Res.Skip; }
                if (InBattle(gar)) { detail = "zaloga " + st.StringId + " w bitwie"; return Res.Wait; }
                int g0 = PeopleBook.Men(gar.MemberRoster);
                if (a[2] == "pula")
                {
                    double floor = 0.0;
                    int notables;
                    int got = VolunteersTo(st, gar, n, out notables, ref floor);
                    int g1 = PeopleBook.Men(gar.MemberRoster);
                    if (got <= 0) { detail = "brak ochotnikow u notabli " + st.StringId + " i jej wsi (notabli " + notables + ", 0 z " + n + ")"; return Res.Skip; }
                    detail = "zaloga " + st.StringId + ": " + g0 + " -> " + g1 + " (ochotnicy regionu " + got + " z " + n + (got < n ? ", niedobor " + (n - got) : "")
                             + ", notabli " + notables + (floor > 1e-6 ? ", ksiega ludzi: pobor na podlodze " + floor.ToString("0.0", Inv) : "") + ")";
                    return Res.Done;
                }
                var src = Terms.FindSettlement(a[2], c.Tok);
                if (src == null) { detail = "osada zrodla " + a[2] + " nie istnieje w grze"; return Res.Skip; }
                if (src == st) { detail = "zrodlo = cel (" + st.StringId + ")"; return Res.Skip; }
                var sg = src.Town != null ? src.Town.GarrisonParty : null;
                if (sg == null || sg.MemberRoster == null) { detail = "osada zrodla " + src.StringId + " bez zalogi"; return Res.Skip; }
                if (InBattle(sg)) { detail = "zaloga zrodla " + src.StringId + " w bitwie"; return Res.Wait; }
                var r = sg.MemberRoster;
                var kinds = new List<KeyValuePair<CharacterObject, int>>();
                int healthy = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || PeopleBook.IsWight(el.Character)) continue;
                    int h = el.Number - el.WoundedNumber;
                    if (h <= 0) continue;
                    kinds.Add(new KeyValuePair<CharacterObject, int>(el.Character, h));
                    healthy += h;
                }
                int take = Math.Min(n, healthy);
                if (take <= 0) { detail = "zaloga " + src.StringId + " bez zdrowych zolnierzy (0 z " + n + ")"; return Res.Skip; }
                var plan = new int[kinds.Count];
                int planned = 0;
                for (int i = 0; i < kinds.Count; i++) { plan[i] = (int)((long)take * kinds[i].Value / healthy); planned += plan[i]; }
                for (int pass = 0; planned < take && pass < 2; pass++)          // reszta z dzielenia - od najliczniejszych
                    foreach (int i in ByCount(kinds))
                    {
                        if (planned >= take) break;
                        if (plan[i] < kinds[i].Value) { plan[i]++; planned++; }
                    }
                int s0 = PeopleBook.Men(r);
                for (int i = 0; i < kinds.Count; i++)
                {
                    if (plan[i] <= 0) continue;
                    r.AddToCounts(kinds[i].Key, -plan[i]);
                    gar.MemberRoster.AddToCounts(kinds[i].Key, plan[i]);
                }
                double moved = 0.0;
                try { moved = PeopleBook.Transfer(r, s0, gar.MemberRoster, g0, PeopleBook.FTransfer); }
                catch (Exception e) { Stumble("Actions.Garrison(ksiega)", e); }
                int s1 = PeopleBook.Men(r), g2 = PeopleBook.Men(gar.MemberRoster);
                detail = "zaloga " + src.StringId + " " + s0 + " -> " + s1 + ", zaloga " + st.StringId + " " + g0 + " -> " + g2 + " (przeszlo " + (s0 - s1) + " z " + n
                         + (take < n ? ", niedobor " + (n - take) : "") + ", ksiega ludzi " + moved.ToString("0", Inv) + ")";
                return Res.Done;
            }

            /// <summary>Indeksy listy od najliczniejszego rodzaju (reszta podzialu garrison).</summary>
            private static IEnumerable<int> ByCount(List<KeyValuePair<CharacterObject, int>> kinds)
            {
                var idx = new List<int>(kinds.Count);
                for (int i = 0; i < kinds.Count; i++) idx.Add(i);
                idx.Sort((x, y) => kinds[y].Value.CompareTo(kinds[x].Value));
                return idx;
            }

            /// <summary>Ochotnicy notabli osady i jej wsi (sloty VolunteerTypes - ten sam zasob co autowerbunek zalogi gry) do zalogi, najwyzej want. Kazdy rekrut przez
            /// ksiege ludzi jak PeopleBook.A5Post: zaloga Strazy - WatchLaw.GiveLevy; pobor wylaczony - koszyk bez pochodzenia; inaczej TakeLevy z DOM regionu (MLevyA5) +
            /// GiveNew, reszta ponizej podlogi - GiveNone (floor). Bez kroku BK DeduceManpower (prywatny w PeopleBook) - ryzyko w dzienniku.</summary>
            private static int VolunteersTo(Settlement st, MobileParty gar, int want, out int notables, ref double floor)
            {
                notables = 0;
                var places = new List<Settlement> { st };
                if (st.BoundVillages != null) foreach (var v in st.BoundVillages) if (v != null && v.Settlement != null) places.Add(v.Settlement);
                bool watch = false;
                try { watch = CrownGifts.IsWatchParty(gar); } catch { watch = false; }
                int got = 0;
                foreach (var place in places)
                {
                    if (got >= want) break;
                    if (place.Notables == null) continue;
                    foreach (var h in place.Notables)
                    {
                        if (got >= want) break;
                        if (h == null || !h.IsAlive || h.VolunteerTypes == null) continue;
                        notables++;
                        var vt = h.VolunteerTypes;
                        for (int i = 0; i < vt.Length && got < want; i++)
                        {
                            var troop = vt[i];
                            if (troop == null || troop.IsHero || PeopleBook.IsWight(troop)) continue;
                            vt[i] = null;
                            gar.MemberRoster.AddToCounts(troop, 1);
                            got++;
                            try
                            {
                                if (!PeopleBook.OriginLive) continue;
                                if (watch) { WatchLaw.GiveLevy(gar.MemberRoster, 1, WatchLaw.KA5, PeopleBook.FNoneWatch); continue; }
                                if (!PeopleBook.LevyLive) { PeopleBook.GiveNone(gar.MemberRoster, 1, PeopleBook.FNoneOff); continue; }
                                Settlement region;
                                double g = PeopleBook.TakeLevy(place, 1, PeopleBook.MLevyA5, out region);
                                if (g > 1e-6) PeopleBook.GiveNew(gar.MemberRoster, region, g, PeopleBook.FLevyA5);
                                if (1.0 - g > 1e-6) { PeopleBook.GiveNone(gar.MemberRoster, 1.0 - g, PeopleBook.FNoneFloor); floor += 1.0 - g; }
                            }
                            catch (Exception e) { Stumble("Actions.Garrison(ksiega ludzi)", e); }
                        }
                    }
                }
                return got;
            }

            /// <summary>Miejsce przedmiotu: slot wyposazenia bohatera albo roster (druzyna, rynek, schowek).</summary>
            private sealed class Spot
            {
                public Equipment Eq;
                public int Slot = -1;
                public ItemRoster R;
                public EquipmentElement El;
                public string Label = "";
            }

            private static Spot InEquipment(Hero h, ItemObject item)
            {
                if (h == null) return null;
                var sets = new[] { h.BattleEquipment, h.CivilianEquipment };
                for (int s = 0; s < sets.Length; s++)
                {
                    var eq = sets[s];
                    if (eq == null) continue;
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        var el = eq[i];
                        if (!el.IsEmpty && el.Item == item) return new Spot { Eq = eq, Slot = i, El = el, Label = HeroName(h) + (s == 0 ? " (wyposazenie, slot " : " (stroj, slot ") + i + ")" };
                    }
                }
                return null;
            }

            private static Spot InRoster(ItemRoster r, ItemObject item, string label)
            {
                if (r == null) return null;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == item && el.Amount > 0) return new Spot { R = r, El = el.EquipmentElement, Label = label };
                }
                return null;
            }

            /// <summary>Bohater z przedmiotem: wyposazenie i stroj, potem druzyna, ktora prowadzi.</summary>
            private static Spot HeldBy(Hero h, ItemObject item)
            {
                var s = InEquipment(h, item);
                if (s != null) return s;
                var mp = h != null ? h.PartyBelongedTo : null;
                if (mp != null && mp.LeaderHero == h) return InRoster(mp.ItemRoster, item, "druzyna " + HeroName(h));
                return null;
            }

            /// <summary>item:&lt;bohater&gt;:&lt;przedmiot&gt;:&lt;skad&gt; - przedmiot PRZENOSZONY od obecnego posiadacza (skad: bohater - jego wyposazenie/druzyna; rod - zywi
            /// bohaterowie rodu i ich druzyny; osada - rynek i schowek). Nikt nie ma - POMINIETA (nigdy z niczego); dragon_* zakazane (P4, smoki tylko Prologue.Dragons).
            /// Cel: gracz - ekwipunek druzyny; inna postac - pasujacy slot wyposazenia (pusty, inaczej zamiana: zdjety przedmiot wraca na miejsce wzietego).</summary>
            private static Res Item(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 3) { detail = "item:<bohater>:<przedmiot>:<skad: bohater|rod|osada> - zle argumenty"; return Res.Skip; }
                if (a[1].StartsWith("dragon_", StringComparison.Ordinal)) { detail = "smoki (dragon_*) tylko Prologue.Dragons (P4)"; return Res.Skip; }
                var item = ItemById(a[1]);
                if (item == null) { detail = "nieznany przedmiot " + a[1]; return Res.Skip; }
                var to = LiveHero(a[0], c, out detail);
                if (to == null) return Res.Skip;
                if (HeldBy(to, item) != null) { detail = "przedmiot " + item.StringId + " juz u " + HeroName(to) + " - bez zmian"; return Res.Done; }
                Spot spot = null;
                string srcName;
                var sh = Terms.FindHero(a[2], c.Tok);
                if (sh != null)
                {
                    srcName = HeroName(sh);
                    if (sh.IsDead) { detail = "posiadacz " + srcName + " nie zyje"; return Res.Skip; }
                    spot = HeldBy(sh, item);
                }
                else
                {
                    var cl = Terms.FindClan(a[2], c.Tok);
                    if (cl != null)
                    {
                        srcName = "rod " + cl.StringId;
                        foreach (var h in cl.Heroes)
                        {
                            if (h == null || !h.IsAlive || h == to) continue;
                            spot = HeldBy(h, item);
                            if (spot != null) break;
                        }
                    }
                    else
                    {
                        var st = Terms.FindSettlement(a[2], c.Tok);
                        if (st == null) { detail = "skad " + a[2] + " - ani bohater, ani rod, ani osada w grze"; return Res.Skip; }
                        srcName = "osada " + st.StringId;
                        spot = InRoster(st.ItemRoster, item, "rynek " + st.StringId);
                        if (spot == null) { ItemRoster stash = null; try { stash = st.Stash; } catch { stash = null; } spot = InRoster(stash, item, "schowek " + st.StringId); }
                    }
                }
                if (spot == null) { detail = "nikt (" + srcName + ") nie ma przedmiotu " + item.StringId + " - nic z niczego"; return Res.Skip; }
                // zdjecie z miejsca posiadacza
                if (spot.Eq != null) spot.Eq[spot.Slot] = default(EquipmentElement);
                else spot.R.AddToCounts(spot.El, -1);
                string where;
                var main = MobileParty.MainParty;
                if (to == Hero.MainHero && main != null && main.ItemRoster != null)
                {
                    main.ItemRoster.AddToCounts(spot.El, 1);
                    where = "ekwipunek druzyny gracza";
                }
                else
                {
                    var eq = to.BattleEquipment;
                    int slot = -1;
                    if (eq != null)
                    {
                        for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                            if (Equipment.IsItemFitsToSlot((EquipmentIndex)i, item)) { if (eq[i].IsEmpty) { slot = i; break; } if (slot < 0) slot = i; }
                    }
                    if (slot >= 0)
                    {
                        var old = eq[slot];
                        eq[slot] = spot.El;
                        where = "wyposazenie " + HeroName(to) + " slot " + slot;
                        if (!old.IsEmpty)
                        {
                            if (spot.Eq != null) spot.Eq[spot.Slot] = old; else spot.R.AddToCounts(old, 1);   // zamiana: zdjety wraca na miejsce wzietego (nic w nicosc)
                            where += " (zamiana: " + (old.Item != null ? old.Item.StringId : "?") + " -> " + spot.Label + ")";
                        }
                    }
                    else if (to.PartyBelongedTo != null && to.PartyBelongedTo.LeaderHero == to && to.PartyBelongedTo.ItemRoster != null)
                    {
                        to.PartyBelongedTo.ItemRoster.AddToCounts(spot.El, 1);
                        where = "druzyna " + HeroName(to);
                    }
                    else
                    {
                        if (spot.Eq != null) spot.Eq[spot.Slot] = spot.El; else spot.R.AddToCounts(spot.El, 1);   // cofniecie - przedmiot zostaje u posiadacza
                        detail = "przedmiot " + item.StringId + " nie pasuje do wyposazenia, a " + HeroName(to) + " nie prowadzi druzyny - zostaje: " + spot.Label;
                        return Res.Skip;
                    }
                }
                detail = "przedmiot " + item.StringId + ": " + spot.Label + " -> " + where;
                return Res.Done;
            }

            /// <summary>wound:&lt;bohater&gt;:&lt;dni 1-120&gt; - rana: zycie do 1 (ponizej progu rany gry) i wpis w rejestrze ran (zapis "kj") - doba Kroniki trzyma rane do konca
            /// (gra leczy co godzine; doba wraca do 1). Druga rana tej postaci - dluzszy termin wygrywa. Bohater w bitwie - CZEKA.</summary>
            private static Res Wound(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int days;
                if (a.Count != 2 || !Num(a[1], 1, 120, out days)) { detail = "wound:<bohater>:<dni 1-120> - zle argumenty"; return Res.Skip; }
                var h = LiveHero(a[0], c, out detail);
                if (h == null) return Res.Skip;
                if (InBattle(h.PartyBelongedTo)) { detail = HeroName(h) + " w bitwie"; return Res.Wait; }
                int day = Terms.Day(), until = day + days, before = h.HitPoints, lim = h.WoundedHealthLimit;
                if (h.HitPoints > 1) h.HitPoints = 1;
                WoundRow row = null;
                foreach (var w in _wounds) if (w.Hero == h.StringId) { row = w; break; }
                string reg;
                if (row == null) { _wounds.Add(new WoundRow { Hero = h.StringId, Until = until, Src = c.Label }); reg = "do dnia " + until; }
                else if (until > row.Until) { reg = "dluzsza rana: do dnia " + row.Until + " -> " + until; row.Until = until; row.Src = c.Label; }
                else reg = "juz ranny do dnia " + row.Until + " (dluzej niz " + until + ")";
                detail = HeroName(h) + ": zycie " + before + " -> " + h.HitPoints + " (prog rany " + lim + "), " + reg;
                return Res.Done;
            }

            /// <summary>Doba (przed akcjami czekajacymi): rany z rejestru - koniec terminu albo smierc - wpis usuniety z linia; trwajaca - zycie z powrotem do 1
            /// (klucz World Changes NIE - rana nie jest trzymana, termin dalej plynie). Kazda rana we wlasnym try.</summary>
            private static void WoundsDaily(int day)
            {
                for (int i = 0; i < _wounds.Count; i++)
                {
                    var w = _wounds[i];
                    try
                    {
                        var h = Terms.FindHero(w.Hero, null);
                        if (h == null || h.IsDead)
                        {
                            _wounds.RemoveAt(i); i--;
                            Log.Info(Tag + "rana " + (h != null ? HeroName(h) : w.Hero) + " (" + w.Src + ") - koniec: " + (h == null ? "brak postaci w grze" : "nie zyje") + ".");
                            continue;
                        }
                        if (day >= w.Until)
                        {
                            _wounds.RemoveAt(i); i--;
                            Log.Info(Tag + "rana " + HeroName(h) + " (" + w.Src + ") - koniec terminu (dzien " + day + ", zycie " + h.HitPoints + ", gra leczy dalej).");
                            continue;
                        }
                        if (KeyWorld && h.HitPoints > 1 && !InBattle(h.PartyBelongedTo)) h.HitPoints = 1;
                    }
                    catch (Exception e) { Stumble("Actions.Wounds(" + w.Hero + ")", e); }
                }
            }

            /// <summary>skill:&lt;bohater&gt;:&lt;umiejetnosc&gt;:&lt;d&gt; - poziom umiejetnosci +-d (0-330) przez HeroDeveloper.SetInitialSkillLevel (poziom i XP razem - bez
            /// skoku wstecz przy nastepnym XP; zmiana nie jest nauka, wiec nie idzie przez AddSkillXp i ksiege umiejetnosci).</summary>
            private static Res Skill(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                int d;
                if (a.Count != 3 || !Num(a[2], -300, 300, out d)) { detail = "skill:<bohater>:<umiejetnosc>:<d -300..300> - zle argumenty"; return Res.Skip; }
                var h = LiveHero(a[0], c, out detail);
                if (h == null) return Res.Skip;
                SkillObject sk = null;
                try { sk = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<SkillObject>(a[1]) : null; } catch { sk = null; }
                if (sk == null) { detail = "nieznana umiejetnosc " + a[1]; return Res.Skip; }
                if (h.HeroDeveloper == null) { detail = HeroName(h) + " bez HeroDeveloper"; return Res.Skip; }
                int before = h.GetSkillValue(sk), after = Math.Max(0, Math.Min(330, before + d));
                if (after != before) h.HeroDeveloper.SetInitialSkillLevel(sk, after);
                int now = h.GetSkillValue(sk);
                detail = HeroName(h) + " " + sk.StringId + ": " + before + " -> " + now + (after - before != d ? " (granica 0-330)" : "");
                return Res.Done;
            }
        }
    }
}
