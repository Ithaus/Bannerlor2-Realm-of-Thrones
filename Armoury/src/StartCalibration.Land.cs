using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// KS - ZIEMIA OKREGOW Z MAPY (plik ModuleData/arm_ks_land.tsv, generator tools/ks/ks_ziemia.py; projekt KS rozdz. 2.2): wies gry = 20 blokow (blok 0 = punkt wsi gry,
    /// 1..19 = mieszanka ziemi calego okregu z rastra), wioska poboczna = jej punkt i zajecie startowe, pola mieszczan = 20 blokow rdzenia twierdzy, rynek W/E,
    /// powierzchnia uzytkowa okregu (ha). Plik czytany raz na proces (w grze sie nie zmienia); skrot danych i skrot ukladu wiosek (ten sam crc co VillageBook) w naglowku.
    /// Brak pliku - KS nieczynny (linia startu).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const string LandFile = "arm_ks_land.tsv";

        internal sealed class LandRow
        {
            public char Kind; public string District, Uid, Start; public int Market; public KsTab.Land O; public bool Block0; public double Area; public int Index;
        }

        internal sealed class LandDist
        {
            public string Id; public int Market; public double Area; public bool Town;
            public readonly LandRow[] Main = new LandRow[20];
            public readonly List<LandRow> Side = new List<LandRow>();
            public readonly Dictionary<string, int> SideIx = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static Dictionary<string, LandDist> _landVil, _landTown;
        private static uint _landCrc, _landLayoutCrc, _landDataCrcHead;
        private static string _landNote = "";
        private static bool _landTried;
        private static int _landRows;

        /// <summary>Plik ziemi wczytany (raz na proces).</summary>
        private static bool LandReady { get { EnsureLand(); return _landVil != null; } }

        private static void EnsureLand()
        {
            if (_landTried) return;
            _landTried = true;
            try
            {
                string path = MapVillageData.DataPathFor(LandFile);
                if (path == null || !File.Exists(path)) { _landNote = "BRAK pliku " + LandFile + " (ModuleData) - KS nieczynny"; return; }
                var vil = new Dictionary<string, LandDist>(StringComparer.Ordinal);
                var town = new Dictionary<string, LandDist>(StringComparer.Ordinal);
                var body = new StringBuilder(1 << 21);
                int rows = 0, bad = 0;
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.TrimEnd('\r');
                    if (line.Length == 0) continue;
                    if (line[0] == '#')
                    {
                        uint v;
                        if (line.StartsWith("# layout_crc:", StringComparison.Ordinal) && uint.TryParse(line.Substring(13).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) _landLayoutCrc = v;
                        else if (line.StartsWith("# data_crc:", StringComparison.Ordinal) && uint.TryParse(line.Substring(11).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) _landDataCrcHead = v;
                        continue;
                    }
                    body.Append(line).Append('\n');
                    var c = line.Split('\t');
                    if (c.Length < 14 || c[0].Length != 1) { bad++; continue; }
                    double ds, dr, dm, area;
                    if (!double.TryParse(c[8], NumberStyles.Float, CultureInfo.InvariantCulture, out ds) || !double.TryParse(c[9], NumberStyles.Float, CultureInfo.InvariantCulture, out dr)
                        || !double.TryParse(c[10], NumberStyles.Float, CultureInfo.InvariantCulture, out dm) || !double.TryParse(c[13], NumberStyles.Float, CultureInfo.InvariantCulture, out area)) { bad++; continue; }
                    var r = new LandRow
                    {
                        Kind = c[0][0], District = c[1], Uid = c[2], Market = c[3] == "W" ? 0 : 1, Block0 = c[11] == "1", Start = c[12] == "-" ? null : c[12], Area = area,
                        O = new KsTab.Land { Zone = c[4], Terr = c[5], Kind = c[6], Model = c[7], DSea = ds, DRiver = dr, DMount = dm, Main = c[0] != "S" }
                    };
                    var map = r.Kind == 'T' ? town : vil;
                    LandDist d;
                    if (!map.TryGetValue(r.District, out d)) { d = new LandDist { Id = r.District, Market = r.Market, Area = area, Town = r.Kind == 'T' }; map[r.District] = d; }
                    if (r.Kind == 'S') { r.Index = d.Side.Count; d.SideIx[r.Uid] = d.Side.Count; d.Side.Add(r); }
                    else
                    {
                        int hash = r.Uid.LastIndexOf('#');
                        string tail = hash >= 0 ? r.Uid.Substring(hash + 1) : "";
                        int k;
                        string num = tail.StartsWith("main", StringComparison.Ordinal) ? tail.Substring(4) : tail.StartsWith("pola", StringComparison.Ordinal) ? tail.Substring(4) : "";
                        if (!int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out k) || k < 0 || k >= 20) { bad++; continue; }
                        r.Index = k;
                        d.Main[k] = r;
                    }
                    rows++;
                }
                // bloki glowne komplet (20) - inaczej okreg pominiety
                int incomplete = 0;
                foreach (var m in new[] { vil, town })
                {
                    var drop = new List<string>();
                    foreach (var kv in m) { foreach (var r in kv.Value.Main) if (r == null) { drop.Add(kv.Key); incomplete++; break; } }
                    foreach (var k in drop) m.Remove(k);
                }
                _landCrc = MapVillageData.Crc32(Encoding.UTF8.GetBytes(body.ToString()));
                _landRows = rows;
                _landVil = vil; _landTown = town;
                _landNote = "plik " + LandFile + ": wsi gry " + vil.Count + ", wiosek pobocznych " + CountSide(vil) + ", pol mieszczan " + town.Count + " miast, wierszy " + rows
                            + (bad > 0 ? " (zlych " + bad + ")" : "") + (incomplete > 0 ? ", okregow bez 20 blokow " + incomplete : "")
                            + "; crc danych " + _landCrc.ToString("X8") + (_landDataCrcHead != 0 && _landDataCrcHead != _landCrc ? " (NAGLOWEK MOWI " + _landDataCrcHead.ToString("X8") + " - plik zmieniony recznie)" : " (zgodny z naglowkiem)")
                            + ", uklad wiosek z pliku " + _landLayoutCrc.ToString("X8");
            }
            catch (Exception e) { _landVil = null; _landTown = null; _landNote = "blad wczytania " + LandFile + " (" + e.GetType().Name + ")"; Stumble("EnsureLand", e); }
        }

        private static int CountSide(Dictionary<string, LandDist> m) { int n = 0; foreach (var d in m.Values) n += d.Side.Count; return n; }

        private static uint _layoutSeen; private static bool _layoutOk; private static int _layoutMiss = -1;

        /// <summary>Uklad wiosek tej sesji zgodny z plikiem ziemi: crc ksiegi wiosek = crc z naglowka pliku, a gdy crc inne - co najmniej 99% wiosek pobocznych pliku ziemi
        /// jest w ukladzie gry (po uid; brakujace - poza KS). Bez ksiegi wiosek - false (wioski poboczne nieznane).</summary>
        private static bool LayoutMatches(out uint now)
        {
            now = 0;
            try
            {
                if (!VillageBook.LayoutCrcNow(out now)) return false;
                if (now == _landLayoutCrc) { _layoutMiss = 0; return true; }
                if (now == _layoutSeen && _layoutMiss >= 0) return _layoutOk;
                _layoutSeen = now;
                uint c2; List<string> uids;
                if (!VillageBook.Layout(out c2, out uids) || uids == null || _landVil == null) { _layoutOk = false; _layoutMiss = -1; return false; }
                var set = new HashSet<string>(uids, StringComparer.Ordinal);
                int all = 0, miss = 0;
                foreach (var d in _landVil.Values) foreach (var r in d.Side) { all++; if (!set.Contains(r.Uid)) miss++; }
                _layoutMiss = miss;
                _layoutOk = all > 0 && miss <= all / 100;
                return _layoutOk;
            }
            catch { return false; }
        }
    }
}
