using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 474-P8/P10: kwestie karczmy z pliku ModuleData\arm_tavern_lines.tsv (wlasne zdania w stylu Westeros, bez cytatow; SAME TEKSTY - Jeff 13.11 "glosy pozniej,
    /// na razie zrob tylko teksty", zadnych nagran). Wiersz: rodzaj TAB region TAB temat TAB tekst; '#' - komentarz. Rodzaje: greet (barman wita), wench (kelnerka),
    /// music (grajkowie), sit (bywalec zaprasza do stolu - 474-P9), table (scenka przy stole: kwestie rozdzielone " || " - 474-P9). Region: any albo klucz RegionOf
    /// (north, iron, west, reach, river, vale, storm, crown, dorne, essos). Temat: general (zawsze), war (pan okregu w wojnie), winter (zima), peace (bez wojny).
    /// Wczytanie RAZ na proces przy pierwszym uzyciu; brak pliku albo brak wiersza - tekst wbudowany wolajacego. Nic do zapisu gry.
    /// </summary>
    internal static class TavernLines
    {
        internal const string FileName = "arm_tavern_lines.tsv";
        private sealed class Row { public string Kind, Region, Topic, Text; }
        private static List<Row> _rows;
        private static string _load = "nie wczytano";
        private static readonly Random _rng = new Random();
        private static int _bad;

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
                    rows.Add(new Row { Kind = f[0].Trim(), Region = f[1].Trim(), Topic = f[2].Trim(), Text = f[3].Trim() });
                }
                var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var r in rows) { int n; kinds.TryGetValue(r.Kind, out n); kinds[r.Kind] = n + 1; }
                var sb = new StringBuilder("plik " + FileName + ": " + rows.Count + " wierszy");
                foreach (var kv in kinds) sb.Append(", ").Append(kv.Key).Append(' ').Append(kv.Value);
                if (_bad > 0) sb.Append(", zlych ").Append(_bad);
                _load = sb.ToString();
            }
            catch (Exception e) { _load = "blad czytania " + FileName + " (" + e.GetType().Name + ") - kwestie wbudowane"; RoadsideInnInterior.Stumble("TavernLines.Load", e); }
            _rows = rows;
        }

        internal static string Summary() { Ensure(); return _load; }

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
                default: return false;
            }
        }

        private static bool AtWar()
        {
            try
            {
                var l = InnInteriorLogic.Current;
                IFaction f = l != null && l.Inn != null && l.Inn.Village != null ? l.Inn.Village.MapFaction : (Hero.MainHero != null ? Hero.MainHero.MapFaction : null);
                if (f == null) return false;
                foreach (var k in Kingdom.All) if (k != null && k != f && !k.IsEliminated && f.IsAtWarWith(k)) return true;
                return false;
            }
            catch { return false; }
        }

        /// <summary>Losowa kwestia rodzaju dla regionu (wiersze regionu i "any"); brak - def.</summary>
        internal static string Pick(string kind, string region, string def)
        {
            var l = All(kind, region);
            return l.Count > 0 ? l[_rng.Next(l.Count)] : def;
        }

        internal static List<string> All(string kind, string region)
        {
            Ensure();
            var res = new List<string>();
            foreach (var r in _rows)
            {
                if (r.Kind != kind) continue;
                if (r.Region != "any" && r.Region != region) continue;
                if (!TopicOk(r.Topic)) continue;
                res.Add(r.Text);
            }
            return res;
        }
    }
}
