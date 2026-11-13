using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 474-P8/P10: kwestie karczmy z pliku ModuleData\arm_tavern_lines.tsv (wlasne zdania w stylu Westeros, bez cytatow; SAME TEKSTY - Jeff 13.11 "glosy pozniej,
    /// na razie zrob tylko teksty", zadnych nagran). Wiersz: rodzaj TAB region TAB temat TAB tekst; '#' - komentarz. Rodzaje: greet (barman wita), wench (kelnerka),
    /// music (grajkowie), sit (bywalec zaprasza do stolu - 474-P9), table (scenka przy stole: kwestie rozdzielone " || " - 474-P9). Region: any albo klucz RegionOf
    /// (north, iron, west, reach, river, vale, storm, crown, dorne, essos). Temat: general (zawsze), war (pan okregu w wojnie), winter (zima), peace (bez wojny).
    /// Wczytanie RAZ na proces przy pierwszym uzyciu; brak pliku albo brak wiersza - tekst wbudowany wolajacego. Nic do zapisu gry.
    /// 474-P10 (klucz TavernTalkLines, MCM "Tavern Talk Lines"): DRUGI plik ModuleData\arm_tavern_talk.tsv - ok. 3800 zdan: rodzaj TAB region TAB temat TAB plec TAB tekst
    /// (plec: m / f / any - kto mowi; w scenkach "table" kwestia moze miec przedrostek "F:" / "M:"). Nowe rodzaje: open / close (ramy barmana wokol plotek - kufel),
    /// pour (barman nalewa), murmur (gwar - urywki rozmow z sali). Tematy biezace z migawki swiata WorldTalk (liczona raz na dobe): siege, hunger, death, wedding,
    /// others; wstawki {ENEMY} (wrog frakcji miejsca), {SIEGE}, {BESIEGER}, {HUNGRY}, {LORD}, {HOUSE}, {GROOM}, {BRIDE} - brak wartosci = kwestia odpada.
    /// Wybor: ok. 40% z tematow biezacych, gdy jakis pasuje (projekt: 60% ogolne, 40% biezace). Klucz NIE = drugi plik nie jest czytany, wybor i tematy co do bitu jak
    /// przed (stary plik ma tylko general / war / peace / winter i wstawke {INN}).
    /// </summary>
    internal static class TavernLines
    {
        internal const string FileName = "arm_tavern_lines.tsv";
        internal const string TalkFile = "arm_tavern_talk.tsv";
        internal const string P = WorldTalk.P;
        internal static bool On { get { var s = Settings.Current; return s != null && s.TavernTalkLines; } }
        private sealed class Row { public string Kind, Region, Topic, Sex, Text; }
        private static List<Row> _rows;
        private static List<Row> _talk;
        private static string _load = "nie wczytano", _talkLoad = "nie wczytano";
        private static readonly Random _rng = new Random();
        private static int _bad, _talkBad;
        // 474-P10: miejsce rozmowy (zajazd / miasto) - ustawia wolajacy; brak - pozycja druzyny gracza
        private static bool _ctxSet;
        private static Vec2 _ctxAt;
        private static IFaction _ctxFaction;
        // 474-P10: liczniki doby (linia zajazdow RoadsideInns -> DayText)
        private static int _dPicks, _dTopical, _dFrames, _dMurmurs, _dDropped;

        /// <summary>Kultura (id ROT) -> region tekstow (te same grupy co muzyka karczmy 474-P4, TavernMusic.Region).</summary>
        internal static string RegionOf(string culture)
        {
            switch (culture)
            {
                case "battania": case "nightswatch": case "skagosi": case "freefolk": case "wildlings": return "north";
                case "sturgia": return "iron";
                case "vlandia": return "west";
                case "reach": return "reach";
                case "river": return "river";
                case "vale": return "vale";
                case "stormlands": return "storm";
                case "crownlands": case "dragonstone": return "crown";
                case "aserai": return "dorne";
                case null: case "": return "any";
                default: return "essos";
            }
        }

        private static void Ensure()
        {
            if (_rows != null) return;
            var rows = new List<Row>();
            try
            {
                string p = MapVillageData.DataPathFor(FileName);
                if (p == null || !File.Exists(p)) { _load = "brak pliku " + FileName + " - kwestie wbudowane"; _rows = rows; return; }
                foreach (string raw in File.ReadAllLines(p, Encoding.UTF8))
                {
                    string l = raw.TrimEnd('\r');
                    if (l.Length == 0 || l[0] == '#') continue;
                    var f = l.Split('\t');
                    if (f.Length < 4 || f[3].Trim().Length == 0) { _bad++; continue; }
                    rows.Add(new Row { Kind = f[0].Trim(), Region = f[1].Trim(), Topic = f[2].Trim(), Sex = "any", Text = f[3].Trim() });
                }
                _load = Describe(FileName, rows, _bad);
            }
            catch (Exception e) { _load = "blad czytania " + FileName + " (" + e.GetType().Name + ") - kwestie wbudowane"; RoadsideInnInterior.Stumble("TavernLines.Load", e); }
            _rows = rows;
        }

        private static string Describe(string file, List<Row> rows, int bad)
        {
            var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in rows) { int n; kinds.TryGetValue(r.Kind, out n); kinds[r.Kind] = n + 1; }
            var sb = new StringBuilder("plik " + file + ": " + rows.Count + " wierszy");
            foreach (var kv in kinds) sb.Append(", ").Append(kv.Key).Append(' ').Append(kv.Value);
            if (bad > 0) sb.Append(", zlych ").Append(bad);
            return sb.ToString();
        }

        /// <summary>474-P10: drugi plik (tylko przy kluczu TavernTalkLines TAK; czytany raz na proces).</summary>
        private static List<Row> Talk()
        {
            if (_talk != null) return _talk;
            var rows = new List<Row>();
            try
            {
                string p = MapVillageData.DataPathFor(TalkFile);
                if (p == null || !File.Exists(p)) { _talkLoad = "brak pliku " + TalkFile + " - tylko kwestie z " + FileName; _talk = rows; return rows; }
                foreach (string raw in File.ReadAllLines(p, Encoding.UTF8))
                {
                    string l = raw.TrimEnd('\r');
                    if (l.Length == 0 || l[0] == '#') continue;
                    var f = l.Split('\t');
                    if (f.Length < 5 || f[4].Trim().Length == 0) { _talkBad++; continue; }
                    string sex = f[3].Trim();
                    rows.Add(new Row { Kind = f[0].Trim(), Region = f[1].Trim(), Topic = f[2].Trim(), Sex = sex == "m" || sex == "f" ? sex : "any", Text = f[4].Trim() });
                }
                int words = 0;
                foreach (var r in rows) words += r.Kind == "table" ? r.Text.Split(new[] { "||" }, StringSplitOptions.None).Length : 1;
                _talkLoad = Describe(TalkFile, rows, _talkBad) + " (kwestii razem " + words + ")";
            }
            catch (Exception e) { _talkLoad = "blad czytania " + TalkFile + " (" + e.GetType().Name + ")"; RoadsideInnInterior.Stumble("TavernLines.LoadTalk", e); }
            _talk = rows;
            return rows;
        }

        internal static string Summary()
        {
            Ensure();
            if (!On) return _load + " | rozmowy karczmy (474-P10): klucz Tavern Talk Lines NIE";
            Talk();
            return _load + " | rozmowy karczmy (474-P10): " + _talkLoad;
        }

        /// <summary>474-P10: miejsce rozmowy - pozycja na mapie i frakcja (wojna, oblezenie najblizej, glod w okolicy). Wolajacy: wnetrze zajazdu (start misji), kufel.</summary>
        internal static void SetPlace(Vec2 at, IFaction f) { _ctxSet = true; _ctxAt = at; _ctxFaction = f; }

        private static Vec2 At()
        {
            if (_ctxSet) return _ctxAt;
            try { return MobileParty.MainParty != null ? MobileParty.MainParty.GetPosition2D : Vec2.Zero; } catch { return Vec2.Zero; }
        }

        /// <summary>Temat pasuje do chwili: general zawsze; war / peace wedlug wojny frakcji okregu (zajazd: wies okregu, inaczej frakcja gracza); winter - zima.</summary>
        private static bool TopicOk(string topic)
        {
            switch (topic)
            {
                case "": case "general": return true;
                case "winter":
                    try { var t = CampaignTime.Now; return WesterosClimate.SkySeason(ref t) == CampaignTime.Seasons.Winter; } catch { return false; }
                case "war": return AtWar();
                case "peace": return !AtWar();
                case "siege": case "hunger": case "death": case "wedding": case "others":
                    return On && WorldTalk.Topic(topic, At());   // 474-P10 (stary plik tych tematow nie ma)
                default: return false;
            }
        }

        private static IFaction PlaceFaction()
        {
            var l = InnInteriorLogic.Current;
            if (l != null && l.Inn != null && l.Inn.Village != null) return l.Inn.Village.MapFaction;
            if (On && _ctxSet && _ctxFaction != null) return _ctxFaction;   // 474-P10: kufel w miescie / zajezdzie
            return Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
        }

        private static bool AtWar()
        {
            try { return Enemy(PlaceFaction()) != null; }
            catch { return false; }
        }

        private static Kingdom Enemy(IFaction f)
        {
            if (f == null) return null;
            foreach (var k in Kingdom.All) if (k != null && k != f && !k.IsEliminated && f.IsAtWarWith(k)) return k;
            return null;
        }

        /// <summary>474-P10: wstawki migawki ({ENEMY}, {SIEGE}...); null - brak wartosci (kwestia odpada). {INN} zostaje dla wolajacego.</summary>
        private static string Fill(string text)
        {
            if (text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder(text.Length + 32);
            int i = 0;
            while (i < text.Length)
            {
                int a = text.IndexOf('{', i);
                if (a < 0) { sb.Append(text, i, text.Length - i); break; }
                int b = text.IndexOf('}', a);
                if (b < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, a - i);
                string key = text.Substring(a + 1, b - a - 1);
                string v;
                if (key == "INN") v = "{INN}";
                else if (key == "ENEMY") { Kingdom k = null; try { k = Enemy(PlaceFaction()); } catch { } v = k != null && k.Name != null ? k.Name.ToString() : null; }
                else v = WorldTalk.Value(key, At());
                if (string.IsNullOrEmpty(v)) return null;
                sb.Append(v);
                i = b + 1;
            }
            return sb.ToString();
        }

        /// <summary>Losowa kwestia rodzaju dla regionu (wiersze regionu i "any"); brak - def.</summary>
        internal static string Pick(string kind, string region, string def)
        {
            string sex;
            return PickSex(kind, region, def, null, out sex);
        }

        /// <summary>474-P10: jak Pick, z plcia mowiacego (want: null - dowolna; "m"/"f" - tylko ta albo "any"); sex - plec wybranej kwestii.</summary>
        internal static string PickSex(string kind, string region, string def, string want, out string sex)
        {
            sex = "any";
            if (!On)
            {
                var l = All(kind, region);
                return l.Count > 0 ? l[_rng.Next(l.Count)] : def;
            }
            List<Row> gen, cur;
            Collect(kind, region, want, out gen, out cur);
            List<Row> from = cur.Count > 0 && (gen.Count == 0 || _rng.NextDouble() < 0.4) ? cur : gen;
            _dPicks++;
            while (from.Count > 0)
            {
                int i = _rng.Next(from.Count);
                var r = from[i];
                string t = Fill(r.Text);
                if (t != null) { if (from == cur) _dTopical++; sex = r.Sex; return t; }
                _dDropped++;
                from.RemoveAt(i);
                if (from.Count == 0 && from == cur) from = gen;
            }
            return def;
        }

        private static void Collect(string kind, string region, string want, out List<Row> gen, out List<Row> cur)
        {
            Ensure();
            gen = new List<Row>(); cur = new List<Row>();
            for (int pass = 0; pass < 2; pass++)
            {
                var src = pass == 0 ? _rows : Talk();
                foreach (var r in src)
                {
                    if (r.Kind != kind) continue;
                    if (r.Region != "any" && r.Region != region) continue;
                    if (want != null && r.Sex != "any" && r.Sex != want) continue;
                    if (!TopicOk(r.Topic)) continue;
                    if (r.Topic == "" || r.Topic == "general") gen.Add(r); else cur.Add(r);
                }
            }
        }

        internal static List<string> All(string kind, string region)
        {
            Ensure();
            var res = new List<string>();
            if (!On)
            {
                foreach (var r in _rows)
                {
                    if (r.Kind != kind) continue;
                    if (r.Region != "any" && r.Region != region) continue;
                    if (!TopicOk(r.Topic)) continue;
                    res.Add(r.Text);
                }
                return res;
            }
            // 474-P10: oba pliki, wstawki wypelnione (bez wartosci - odpada)
            List<Row> gen, cur;
            Collect(kind, region, null, out gen, out cur);
            foreach (var r in gen) { var t = Fill(r.Text); if (t != null) res.Add(t); else _dDropped++; }
            foreach (var r in cur) { var t = Fill(r.Text); if (t != null) res.Add(t); else _dDropped++; }
            return res;
        }

        /// <summary>474-P10: scenki stolu podzielone na ogolne i biezace (wybor 60/40 robi InnTable).</summary>
        internal static void Scenes(string region, out List<string> gen, out List<string> cur)
        {
            gen = new List<string>(); cur = new List<string>();
            List<Row> g, c;
            Collect("table", region, null, out g, out c);
            foreach (var r in g) { var t = Fill(r.Text); if (t != null) gen.Add(t); else _dDropped++; }
            foreach (var r in c) { var t = Fill(r.Text); if (t != null) cur.Add(t); else _dDropped++; }
        }

        internal static void NoteTopical() { _dTopical++; }

        /// <summary>474-P10: rama barmana wokol plotek (kufel): open - przed plotkami, close - na koniec. Klucz NIE - null, null.</summary>
        internal static void Frame(string region, Vec2 at, IFaction f, out string open, out string close)
        {
            open = close = null;
            if (!On) return;
            try
            {
                SetPlace(at, f);
                open = Pick("open", region, null);
                close = Pick("close", region, null);
                if (open != null || close != null) _dFrames++;
            }
            catch (Exception e) { RoadsideInnInterior.Stumble("TavernLines.Frame", e); open = close = null; }
        }

        /// <summary>474-P10: urywek gwaru z sali (wnetrze zajazdu): tekst z opisem mowiacego wedlug plci; null - nic.</summary>
        internal static string Murmur(string region)
        {
            if (!On) return null;
            string sex;
            string t = PickSex("murmur", region, null, null, out sex);
            if (t == null) return null;
            _dMurmurs++;
            string[] who = sex == "f" ? WhoF : sex == "m" ? WhoM : WhoAny;
            return who[_rng.Next(who.Length)] + ": \"" + t + "\"";
        }

        private static readonly string[] WhoF = { "A woman at the next table", "A woman by the hearth", "An old woman in the corner", "A girl near the door", "A woman at the counter", "A woman with a baby on her hip" };
        private static readonly string[] WhoM = { "A man at the next table", "A drover by the fire", "An old man in the corner", "A pedlar near the door", "A man at the counter", "A carter with his boots off" };
        private static readonly string[] WhoAny = { "Someone by the hearth", "A voice from the back", "Someone at the long table", "A voice near the door" };

        internal static string DayText()
        {
            string t = "rozmowy (474-P10): wyborow " + _dPicks + " (biezacych " + _dTopical + "), ramy barmana " + _dFrames + ", gwar " + _dMurmurs + ", kwestie bez wstawki " + _dDropped;
            _dPicks = _dTopical = _dFrames = _dMurmurs = _dDropped = 0;
            return t;
        }
    }
}
