// Wioski na mapie - paczka W2 "Map Villages" (SAM WIDOK; docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md rozdz. 5, 6.7, 7.1, 8, 10, 11).
// Kazdy wiersz pliku ModuleData\arm_map_villages.tsv to nazwana wioska = obrazek gromady osad okregu wsi gry (wsi-matki).
// Wyglad: kopia SAMYCH SIATEK domow wsi-matki (nowe puste encje + kopie MetaMesh, uklad i ramki lokalne jak u matki),
// bez fizyki, bez skryptow, bez tagow, bez czastek - nic nie lapie klikniec i nic nie widzi jej jako osady.
// Polozenie z pliku, wysokosc z terenu, obrot z pliku (front_deg, dopasowany do dlugiej osi domow matki), poziom 1/2/3 z pliku.
// Dymek po najechaniu, ogien na pierwszej wiosce lancucha, gdy wies gry jest rabowana, dym w czasie jej czujnosci.
// NIC nie idzie do zapisu gry; spalona / mniejsza / odbudowa to ksiega wiosek (W3).
// Wybor kopii (projekt rozdz. 5.1, krytyk K6): NIE GameEntity.CopyFrom (kopiuje natywny skrypt "Town Entity Manager"
// 563 wsi, tag "village", flagi predisplay i kule "_bo" - zachowanie skryptu na kopii nieznane) i NIE Instantiate prefabu
// (plik nie ma nazwy prefabu domow, a 264 wsie Essos / Calradii w ogole nie maja prefabu domow - siatki stoja wprost w scenie).
// Zamiast tego to, co gra robi z namiotem partii (MobilePartyVisual.AddTentEntityForParty: CreateEmpty + AddMultiMesh(kopia) +
// SetFrame + AddChild): tylko publiczne API, a widocznosc wedlug maski poziomu jak SettlementVisual.SetSettlementLevelVisibility.
// POPRAWKA PO AUTOTESCIE 07.10 17:41 ("brak siatek" we wszystkich 41 wzorach): natywny skrypt mapy "Town Scene Manager" przy wczytaniu
// sceny zbiera siatki osad do wspolnych ikon (rgl_log "Town scene manager: Total mesh: 23858, Total Unique Mesh: 304"), wiec encje
// wsi-matek nie maja juz komponentow MetaMesh. Wzor powstaje teraz kilkoma drogami po kolei (BuildTemplate): drzewo matki z siatka
// po nazwie encji, kopia prefabu bez sceny, prefab wsi kultury; trzy pierwsze matki wypisane drzewem do wioski.log.
// Teksty w grze po angielsku (VillageTexts.cs), komentarze bez polskich znakow.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Armoury.Villages;
using SandBox.View;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    // =====================================================================================================
    // DANE (bez typow gry - do proby poza gra: wczytanie pliku, CRC, wybor wioski do ognia, obrys, przerzedzenie)
    // =====================================================================================================

    /// <summary>Jeden wiersz pliku arm_map_villages.tsv (sama tresc, bez stanu widoku).</summary>
    internal sealed class MapVillageRecord
    {
        public int Id;
        public string VillageId = "";     // wies gry = okreg (StringId osady)
        public string Name = "";          // nazwa wioski po angielsku
        public float X, Y;                // polozenie (jedn. mapy, jak posX / posY)
        public float RoadDeg;             // kierunek drogi / rzeki / brzegu 0..180
        public int Settlements;           // osady gromady (po ok. 250 ludzi)
        public int Order;                 // domyslna kolejnosc palenia w okregu (0 = najblizej wsi gry po drodze)
        public string Kind = "";          // bridge / ford / crossroad / road / river / coast / field ...
        public string Uid = "";           // stale id (id wsi + skrot polozenia)
        public string Class = "";         // H wezel / R przy linii / F pole
        public int Level = 2;             // 1/2/3 (ile domow stoi)
        public float FrontDeg;            // obrot rot_z w stopniach: lokalna +Y = front, lokalna X = ulica wzdluz drogi
        public float HalfLen = 1.3f;      // pol dlugosci obrysu (wzdluz ulicy)
        public float HalfWid = 0.7f;      // pol szerokosci obrysu
        public int Line;                  // numer wiersza w pliku (do logu)
    }

    /// <summary>Wynik wczytania pliku: wiersze, naglowek, odrzucone wiersze z powodami.</summary>
    internal sealed class MapVillageFile
    {
        public readonly List<MapVillageRecord> Records = new List<MapVillageRecord>();
        public string Title = "";         // pierwsza linia naglowka (wariant pliku, np. "6000 people per village")
        public bool HasSceneCrc;          // naglowek "# scene_xml_crc: <liczba>" (IMapScene.GetSceneXmlCrc)
        public uint SceneCrc;
        public uint FileCrc32;            // crc32 calego pliku (ktory plik jest w grze)
        public int DataLines;             // wiersze danych (bez komentarzy i pustych)
        public int BadRows;
        public int LevelClamped;          // poziom spoza 1..3 przyciety (wiersz zostaje)
        public string MissingColumns = ""; // brak wymaganej kolumny w naglowku = caly plik odrzucony
        public readonly SortedDictionary<string, int> BadReasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public string FirstBad = "";

        internal void Bad(string why, int line)
        {
            BadRows++;
            int n;
            BadReasons.TryGetValue(why, out n);
            BadReasons[why] = n + 1;
            if (FirstBad.Length == 0) FirstBad = "wiersz " + line + ": " + why;
        }

        internal string ReasonsText()
        {
            if (BadReasons.Count == 0) return "-";
            var sb = new StringBuilder();
            foreach (var kv in BadReasons) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
            return sb.ToString();
        }
    }

    /// <summary>Czyste funkcje danych wiosek (bez gry): wczytanie, CRC, lancuch ognia, obrys, przerzedzenie, os domow.</summary>
    internal static class MapVillageData
    {
        internal const string FileName = "arm_map_villages.tsv";
        internal const int CheckNoCrc = 0, CheckMatch = 1, CheckMismatch = 2;

        private static readonly string[] Required = { "id", "village_id", "name", "x", "y", "settlements", "order", "level", "front_deg" };
        // kolumny pliku v2 / wedlug ludnosci (generator: naglowek "#id\tvillage_id\t...") - gdy naglowka brak, takie pozycje
        private static readonly string[] DefaultColumns = { "id", "village_id", "name", "x", "y", "road_deg", "settlements", "order", "kind", "uid",
            "class", "side", "level", "road_dist", "toward_village", "neighbors", "front_deg", "half_len", "half_wid", "face" };
        private static readonly Regex CrcRx = new Regex(@"scene_xml_crc\s*[:=]\s*(0x[0-9A-Fa-f]+|[0-9]+)", RegexOptions.CultureInvariant);

        /// <summary>Wczytuje tresc pliku. Wiersz z bledem jest odrzucany (z powodem), reszta zostaje.</summary>
        internal static MapVillageFile Parse(string text)
        {
            var f = new MapVillageFile();
            if (string.IsNullOrEmpty(text)) return f;
            if (text[0] == (char)0xFEFF) text = text.Substring(1);   // BOM
            var col = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < DefaultColumns.Length; i++) col[DefaultColumns[i]] = i;
            var uids = new HashSet<string>(StringComparer.Ordinal);
            string[] lines = text.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li].TrimEnd('\r');
                int lineNo = li + 1;
                if (line.Trim().Length == 0) continue;
                if (line[0] == '#')
                {
                    string body = line.Substring(1);
                    if (body.StartsWith("id\t", StringComparison.Ordinal))
                    {
                        col.Clear();
                        string[] names = body.Split('\t');
                        for (int i = 0; i < names.Length; i++) { string nm = names[i].Trim(); if (nm.Length > 0 && !col.ContainsKey(nm)) col[nm] = i; }
                        var miss = new List<string>();
                        foreach (var r in Required) if (!col.ContainsKey(r)) miss.Add(r);
                        if (miss.Count > 0) { f.MissingColumns = string.Join(",", miss.ToArray()); f.Records.Clear(); return f; }
                        continue;
                    }
                    var m = CrcRx.Match(body);
                    if (m.Success)
                    {
                        uint crc;
                        string v = m.Groups[1].Value;
                        bool ok = v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? uint.TryParse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out crc)
                            : uint.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out crc);
                        if (ok) { f.HasSceneCrc = true; f.SceneCrc = crc; }
                        continue;
                    }
                    if (f.Title.Length == 0) f.Title = body.Trim();
                    continue;
                }
                f.DataLines++;
                string[] c = line.Split('\t');
                var rec = new MapVillageRecord { Line = lineNo };
                string why = Fill(rec, c, col);
                if (why == null && !uids.Add(rec.Uid)) why = "powtorzony uid";
                if (why != null) { f.Bad(why, lineNo); continue; }
                if (rec.Level < 1 || rec.Level > 3) { rec.Level = rec.Level < 1 ? 1 : 3; f.LevelClamped++; }
                f.Records.Add(rec);
            }
            return f;
        }

        private static string Get(string[] c, Dictionary<string, int> col, string name)
        {
            int i;
            if (!col.TryGetValue(name, out i) || i < 0 || i >= c.Length) return null;
            return c[i].Trim();
        }

        private static bool Num(string s, out float v)
        {
            v = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
            return !(float.IsNaN(v) || float.IsInfinity(v));
        }

        private static bool Int(string s, out int v)
        {
            v = 0;
            return !string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        /// <summary>Wypelnia rekord z kolumn; zwraca powod odrzucenia albo null.</summary>
        private static string Fill(MapVillageRecord r, string[] c, Dictionary<string, int> col)
        {
            foreach (var req in Required) if (Get(c, col, req) == null) return "za malo kolumn";
            if (!Int(Get(c, col, "id"), out r.Id)) return "zla liczba";
            r.VillageId = Get(c, col, "village_id");
            if (r.VillageId.Length == 0) return "brak wsi gry";
            r.Name = Get(c, col, "name");
            if (r.Name.Length == 0) return "brak nazwy";
            if (!Num(Get(c, col, "x"), out r.X) || !Num(Get(c, col, "y"), out r.Y)) return "zla liczba";
            if (r.X < 0f || r.Y < 0f || r.X > 100000f || r.Y > 100000f) return "poza mapa";
            if (!Int(Get(c, col, "settlements"), out r.Settlements)) return "zla liczba";
            if (r.Settlements < 1) return "osady < 1";
            if (!Int(Get(c, col, "order"), out r.Order)) return "zla liczba";
            if (!Int(Get(c, col, "level"), out r.Level)) return "zla liczba";
            if (!Num(Get(c, col, "front_deg"), out r.FrontDeg)) return "zla liczba";
            float v;
            if (Num(Get(c, col, "road_deg"), out v)) r.RoadDeg = v;
            if (Num(Get(c, col, "half_len"), out v) && v > 0.05f && v < 20f) r.HalfLen = v;
            if (Num(Get(c, col, "half_wid"), out v) && v > 0.05f && v < 20f) r.HalfWid = v;
            r.Kind = Get(c, col, "kind") ?? "";
            r.Class = Get(c, col, "class") ?? "";
            string uid = Get(c, col, "uid");
            r.Uid = string.IsNullOrEmpty(uid) || uid == "-" ? r.VillageId + "#" + r.Id.ToString(CultureInfo.InvariantCulture) : uid;
            return null;
        }

        /// <summary>CRC sceny: plik bez naglowka CRC - bez sprawdzenia; zgodny; niezgodny (wioski wylaczone).</summary>
        internal static int CheckScene(MapVillageFile f, uint sceneCrc)
        {
            if (f == null || !f.HasSceneCrc) return CheckNoCrc;
            return f.SceneCrc == sceneCrc ? CheckMatch : CheckMismatch;
        }

        private static uint[] _crcTable;

        /// <summary>CRC32 (IEEE, jak zlib) - tylko do rozpoznania pliku w logu.</summary>
        internal static uint Crc32(byte[] data)
        {
            if (_crcTable == null)
            {
                var t = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    t[i] = c;
                }
                _crcTable = t;
            }
            uint crc = 0xFFFFFFFFu;
            if (data != null) foreach (byte b in data) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        /// <summary>Wioski okregow, kazda lista wedlug kolejnosci palenia (order, potem id).</summary>
        internal static Dictionary<string, List<MapVillageRecord>> ByDistrict(List<MapVillageRecord> all)
        {
            var d = new Dictionary<string, List<MapVillageRecord>>(StringComparer.Ordinal);
            foreach (var r in all)
            {
                List<MapVillageRecord> l;
                if (!d.TryGetValue(r.VillageId, out l)) d[r.VillageId] = l = new List<MapVillageRecord>();
                l.Add(r);
            }
            foreach (var l in d.Values) l.Sort(CompareOrder);
            return d;
        }

        internal static int CompareOrder(MapVillageRecord a, MapVillageRecord b)
        {
            int c = a.Order.CompareTo(b.Order);
            return c != 0 ? c : a.Id.CompareTo(b.Id);
        }

        /// <summary>Pierwsza wioska lancucha palenia, ktora da sie pokazac (projekt 6.7 "tryb gry": ogien na pierwszej wiosce
        /// lancucha przy IsUnderRaid). Lista musi byc posortowana wedlug kolejnosci. -1 = brak (plonie sama wies gry, jak dzis).</summary>
        internal static int PickFirst(IList<MapVillageRecord> byOrder, Func<MapVillageRecord, bool> usable)
        {
            if (byOrder == null) return -1;
            for (int i = 0; i < byOrder.Count; i++) if (usable == null || usable(byOrder[i])) return i;
            return -1;
        }

        /// <summary>Punkt w obroconym prostokacie obrysu (+ margines) - test najechania (krytyk K4).</summary>
        internal static bool InFootprint(MapVillageRecord r, float px, float py, float margin)
        {
            double a = r.FrontDeg * Math.PI / 180.0;
            double ca = Math.Cos(a), sa = Math.Sin(a);
            double dx = px - r.X, dy = py - r.Y;
            double along = dx * ca + dy * sa;          // lokalna X = ulica wzdluz drogi
            double across = -dx * sa + dy * ca;        // lokalna Y = front
            return Math.Abs(along) <= r.HalfLen + margin && Math.Abs(across) <= r.HalfWid + margin;
        }

        /// <summary>Skala obrazka wedlug poziomu (zawsze mniejszy od wsi-matki, projekt 5.3).</summary>
        internal static float LevelScale(int level)
        {
            return level >= 3 ? 0.80f : level == 2 ? 0.70f : 0.60f;
        }

        internal static uint Fnv(string s)
        {
            uint h = 2166136261u;
            if (s != null) foreach (char ch in s) { h ^= ch; h *= 16777619u; }
            return h;
        }

        /// <summary>Przerzedzenie wsi o jednym wygladzie na 1/2/3 (307 wsi ROT z domami andal / fm, projekt 5.3): poziom 3 - wszystkie
        /// domy, 2 - ok. 2/3, 1 - ok. 1/3, zawsze co najmniej 2. Wybor staly z ziarna uid (ta sama wioska wyglada tak samo co wczytanie).</summary>
        internal static bool[] ThinLeaves(string uid, int leafCount, int level)
        {
            var keep = new bool[Math.Max(0, leafCount)];
            if (leafCount <= 0) return keep;
            int want = level >= 3 ? leafCount : (int)Math.Ceiling(leafCount * (level == 2 ? 2.0 / 3.0 : 1.0 / 3.0));
            want = Math.Max(Math.Min(2, leafCount), Math.Min(leafCount, want));
            var idx = new int[leafCount];
            var key = new uint[leafCount];
            for (int i = 0; i < leafCount; i++) { idx[i] = i; key[i] = Fnv(uid + ":" + i.ToString(CultureInfo.InvariantCulture)); }
            Array.Sort(key, idx);
            for (int i = 0; i < want; i++) keep[idx[i]] = true;
            return keep;
        }

        /// <summary>Dluga os rozkladu domow matki (PCA srodkow) w jej ukladzie lokalnym, kat w (-pi/2, pi/2].
        /// false = za malo domow albo rozklad prawie okragly (wtedy obrot wprost z pliku).</summary>
        internal static bool LongAxis(IList<float> xs, IList<float> ys, out float angle)
        {
            angle = 0f;
            int n = xs == null || ys == null ? 0 : Math.Min(xs.Count, ys.Count);
            if (n < 3) return false;
            double mx = 0, my = 0;
            for (int i = 0; i < n; i++) { mx += xs[i]; my += ys[i]; }
            mx /= n; my /= n;
            double cxx = 0, cyy = 0, cxy = 0;
            for (int i = 0; i < n; i++) { double ax = xs[i] - mx, ay = ys[i] - my; cxx += ax * ax; cyy += ay * ay; cxy += ax * ay; }
            double tr = cxx + cyy, det = cxx * cyy - cxy * cxy;
            double disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
            double l1 = tr / 2 + disc, l2 = tr / 2 - disc;
            if (l1 <= 1e-6 || l1 < 1.5 * Math.Max(l2, 0)) return false;
            angle = (float)(0.5 * Math.Atan2(2 * cxy, cxx - cyy));
            return true;
        }

        /// <summary>Obrot obrazka (radiany, rot_z): z pliku; gdy matka ma wyrazna dluga os - tak, zeby ta os szla wzdluz ulicy
        /// (lokalna X pliku), po tej stronie, ktora jest najblizej frontu z pliku (projekt 2.9).</summary>
        internal static float Yaw(float frontDeg, bool hasAxis, float axis)
        {
            float a = (float)(frontDeg * Math.PI / 180.0);
            return hasAxis ? a - axis : a;
        }

        // ---------- siatki wsi-matek bez komponentow (poprawka po autotescie 07.10 17:41) ----------
        // Skrypt mapy "Town Scene Manager" przy wczytaniu sceny zbiera siatki osad do wspolnych ikon (rgl_log: "Town scene manager:
        // Total mesh: 23858, Total Unique Mesh: 304"; natywny rglTown_icon_component = GameEntity.ComponentType.TownIcon, bez klasy
        // zarzadzanej) - encje zostaja z nazwami, ramkami i maskami, ale MultiMeshComponentCount = 0. Siatke encji bierzemy wtedy po
        // NAZWIE (MetaMesh.GetCopy, jak namiot partii). Tablica wygenerowana ze sceny ROT 8.1.8 (ROT-Map\SceneObj\Main_map\scene.xscene
        // + prefaby Native / ROT-Map; dzien-6\wioski-2200\proba-1741\gen_tablica.py): w 571 wsiach 94 nazwy encji z siatka, kazda z
        // dokladnie jedna siatka, zadna niejednoznaczna. Tu tylko domy i ikony wsi (pola / kopalnie / stada i zgliszcza nie ida do wiosek);
        // nazwa spoza tablicy: tylko lisc z nazwa domu ROT (andal_wm_* / fm_wm_*: nazwa encji = nazwa siatki).
        private static readonly Dictionary<string, string> NameToMesh = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "andal_wm_house1", "andal_wm_house1" }, { "andal_wm_house2", "andal_wm_house2" }, { "andal_wm_house3", "andal_wm_house3" },
            { "andal_wm_house4", "andal_wm_house4" }, { "andal_wm_house5", "andal_wm_house5" }, { "andal_wm_house6", "andal_wm_house6" },
            { "andal_wm_well", "andal_wm_well" }, { "andal_wm_well2", "andal_wm_well2" },
            { "dothraki_village_l1", "dothraki_village" }, { "dothraki_village_l2", "dothraki_village" }, { "dothraki_village_l3", "dothraki_village" },
            { "fm_wm_hall_snow", "fm_wm_hall_snow" }, { "fm_wm_house_1", "fm_wm_house_1" }, { "fm_wm_house_1_snow", "fm_wm_house_1_snow" },
            { "fm_wm_house_2", "fm_wm_house_2" }, { "fm_wm_shed", "fm_wm_shed" }, { "fm_wm_shed2", "fm_wm_shed2" },
            { "fm_wm_shed2_snow", "fm_wm_shed2_snow" }, { "fm_wm_shed_snow", "fm_wm_shed_snow" },
            { "map_icons_aserai_village_l1", "village_aserai_1" }, { "map_icons_aserai_village_l2", "village_aserai_2" },
            { "map_icons_aserai_village_l3", "village_aserai_3" },
            { "map_icons_battania_village_l1", "village_battania_1" }, { "map_icons_battania_village_l2", "village_battania_2" },
            { "map_icons_battania_village_l3", "village_battania_3" },
            { "map_icons_empire_village_l1", "village_empire_1" }, { "map_icons_empire_village_l2", "village_empire_2" },
            { "map_icons_empire_village_l3", "village_empire_3" },
            { "map_icons_khuzait_village_l1", "village_khuzait_1" }, { "map_icons_khuzait_village_l2", "village_khuzait_2" },
            { "map_icons_khuzait_village_l3", "village_khuzait_3" },
            { "map_icons_sturgia_village_l1", "village_sturgia_1" }, { "map_icons_sturgia_village_l2", "village_sturgia_2" },
            { "map_icons_sturgia_village_l3", "village_sturgia_3" },
            { "map_icons_vlandia_village_l1", "village_vlandia_1" }, { "map_icons_vlandia_village_l2", "village_vlandia_2" },
            { "map_icons_vlandia_village_l3", "village_vlandia_3" }
        };

        // Zapas "kultura": prefab domow najczestszy w wsiach tej kultury w scenie ROT 8.1.8 (ten sam skrypt; prefab polaczony albo
        // old_prefab_name). Kultury Essos / Calradii nie maja domow andal / fm - dla nich stary prefab samej matki (map_icon_full_*).
        private static readonly Dictionary<string, string> CultureHouses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "battania", "fm_village1" }, { "sturgia", "fm_village1" }, { "nightswatch", "fm_village3" }, { "skagosi", "fm_village3" },
            { "crownlands", "andal_village3" }, { "reach", "andal_village3" }, { "vlandia", "andal_village3" },
            { "dragonstone", "andal_village5" }, { "vale", "andal_village5" }, { "river", "andal_village10" }, { "stormlands", "andal_village7" }
        };

        internal const string LastResortPrefab = "andal_village3";   // ostatni zapas, gdy nic innego nie ma (ROT-Map\Prefabs\ROT_north.xml)

        /// <summary>Encje, ktorych nie kopiujemy: kula kolizji i pomocniki (bo_*), czastki ognia oblezenia / zlupienia, zgliszcza (*_looted,
        /// w scenie maska "looted" bez "civilian"), pola / kopalnie / stada produkcji wsi (map_icons_production*) - wioska to same domy.</summary>
        internal static bool IsHelperName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name == "_bo" || name.StartsWith("bo_", StringComparison.Ordinal) || name.StartsWith("siege_", StringComparison.Ordinal)
                   || name.IndexOf("particle", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.EndsWith("_looted", StringComparison.Ordinal) || name.StartsWith("map_icons_production", StringComparison.Ordinal);
        }

        /// <summary>Nazwa siatki dla encji bez komponentow MetaMesh: z tablicy, a spoza niej tylko lisc z nazwa domu ROT (andal_wm_* / fm_wm_*,
        /// tam nazwa encji = nazwa siatki - na wypadek nowych domow w aktualizacji ROT); null = brak. Inne liscie sceny (proba: drop_point,
        /// empty_object) to pomocniki edytora - nigdy nie ida do MetaMesh.GetCopy.</summary>
        internal static string MeshForName(string entityName, bool leaf)
        {
            if (string.IsNullOrEmpty(entityName) || IsHelperName(entityName)) return null;
            string m;
            if (NameToMesh.TryGetValue(entityName, out m)) return m;
            bool rotHouse = entityName.StartsWith("andal_wm_", StringComparison.Ordinal) || entityName.StartsWith("fm_wm_", StringComparison.Ordinal);
            return leaf && rotHouse ? entityName : null;
        }

        /// <summary>Prefab domow kultury wsi (StringId kultury) albo null.</summary>
        internal static string CultureHousePrefab(string cultureId)
        {
            string p;
            return !string.IsNullOrEmpty(cultureId) && CultureHouses.TryGetValue(cultureId, out p) ? p : null;
        }

        /// <summary>Czy encja z maska m stoi przy masce wsi cur. Gra (SettlementVisual.SetSettlementLevelVisibility): (m &amp; cur) == cur.
        /// Encja bez poziomow (m = 0; SceneLeveler.cs:218 "bez poziomu" = UpgradeLevelMask.None) stoi zawsze - w scenie bez &lt;levels&gt; sa
        /// WSZYSTKIE domy andal / fm i ikony Dothrakow (dawny scisly warunek wycinal je w calosci). Zapas (lenient, tylko gdy scisle nic
        /// nie wyszlo): poziom zgodny albo bez poziomow, ale nigdy zgliszcza / oblezenie bez "civilian".</summary>
        internal static bool LevelVisible(uint m, uint cur, bool lenient, uint civil, uint looted, uint siege, uint levels)
        {
            if (m == 0) return true;
            if ((m & cur) == cur) return true;
            if (!lenient) return false;
            if ((m & civil) == 0 && (m & (looted | siege)) != 0) return false;
            uint lvCur = cur & levels;
            return (m & levels) == 0 || (m & lvCur) != 0;
        }
    }

    // =====================================================================================================
    // WIDOK (komponent mapy SandBox.View; zyje z MapScreen, MapScreen.OnFinalize konczy go razem z innymi menedzerami)
    // =====================================================================================================

    /// <summary>Wioski na mapie (W2): obrazki, widocznosc, ogien / dym, dymek. Rejestracja: SubModuleMain.OnApplicationTick.</summary>
    public sealed class MapVillagesView : EntityVisualManagerBase
    {
        // po partiach (10), osadach (40), sladach (50), pogodzie (60) i dzwieku (70): dymek tylko, gdy nikt inny nie trafil
        public override int Priority => 100;

        private const float Cell = 50f;               // komorki siatki wyszukiwania (jedn. mapy)
        private const float TickEvery = 0.25f;        // praca widoku 4 razy na sekunde, nie co klatke (CLAUDE.md 7)
        private const float ShowRadiusExtra = 120f;   // pokazane w promieniu (wysokosc kamery + 120)
        private const float HoverMargin = 0.3f;       // margines obrysu przy najechaniu
        private const int CreatePerTick = 48;         // potem tworzenie po 48 na cwierc sekundy
        private const int FirstFillCap = 600;         // pierwsze wypelnienie po wczytaniu - bez limitu w praktyce (projekt 5.7), z bezpiecznikiem
        private const int StrikesPerVillage = 3;      // potkniecia jednej wioski, po ktorych ta JEDNA wioska jest pomijana (nigdy cala warstwa)
        private const float SummaryEvery = 120f;      // linia podsumowania w logu najwyzej co 2 minuty, tylko gdy cos sie zmienilo
        private const int FxNone = 0, FxSmoke = 1, FxFire = 2;
        private const string FireFx = "psys_fire_smoke_env_point";        // ogien + dym rabowanej wsi gry (SettlementVisual.cs:416)
        private const string SmokeFx = "map_icon_village_plunder_fx";     // dym nad zlupiona wsia (SettlementVisual.cs:427)

        private sealed class PlanNode
        {
            public int Parent;                // -1 = korzen wioski
            public MatrixFrame Local;         // ramka lokalna jak u matki (GetFrame)
            public MetaMesh[] Meshes;         // siatki tej encji matki (kopiowane przy tworzeniu)
            public bool NoSeason;             // flaga not_affected_by_season encji matki
            public int LeafIndex = -1;        // numer liscia z siatka (do przerzedzenia), -1 = nie lisc
            public float RootX, RootY;        // polozenie w ukladzie korzenia matki (dluga os)
            public int Src;                   // skad siatka: SrcComp komponent encji, SrcName po nazwie encji, SrcPrefab z kopii prefabu
        }

        private const int SrcComp = 0, SrcName = 1, SrcPrefab = 2;
        private const int WayTree = 1, WayPrefab = 2, WayCulture = 3;
        private static readonly MetaMesh[] NoMeshes = new MetaMesh[0];

        private sealed class Template
        {
            public bool Ok;
            public string Why = "";
            public Vec3 Scale = new Vec3(1f, 1f, 1f);
            public bool NoSeason;
            public readonly PlanNode[][] Plans = new PlanNode[4][];   // [poziom 1..3]
            public bool Flat;                 // ten sam uklad na 1/2/3 (domy ROT bez poziomow) - poziom = przerzedzenie
            public int LeafCount;
            public bool HasAxis;
            public float Axis;
            public int Way;                   // droga, ktora dala wzor (WayTree / WayPrefab / WayCulture), 0 = zadna
            public string WaySource = "";     // prefab drogi "kultura" (do logu)
            public bool Lenient;              // wzor z zapasu maski
            public int FromComp, FromName, FromPrefab;   // siatki poziomu 3 wedlug zrodla
        }

        /// <summary>Warunki przejscia drzewa: maska wsi, zapas maski, droga "prefaby" (encja z nazwa prefabu -> kopia prefabu bez sceny).</summary>
        private sealed class WalkArgs
        {
            public uint Mask;
            public bool Lenient;
            public bool Prefabs;
        }

        private sealed class District
        {
            public string Id;
            public Settlement S;
            public readonly List<Slot> ByOrder = new List<Slot>();
            public int Fx;                    // stan wsi gry: 0 nic, 1 dym (Looted), 2 ogien (IsUnderRaid)
            public Slot FxSlot;               // wioska, na ktorej stoi ogien / dym
            public Template T;
            public bool TemplateTried;
        }

        private sealed class Slot
        {
            public MapVillageRecord R;
            public District D;
            public GameEntity Root;
            public Visual V;
            public bool Shown, Failed, NeedRes;
            public int Strikes;
            public int FxWanted, FxShown;
            public float Z;
            public string Why = "";
        }

        // ---------- stan komponentu (jeden na mape; nic statycznego poza rejestracja) ----------
        private MapScreen _screen;
        private bool _active, _finalized, _wasOff, _firstFillDone;
        private float _acc, _sinceSummary;
        private uint _maskCivil, _maskL1, _maskL2, _maskL3, _maskLooted, _maskSiege;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<District> _districts = new List<District>();
        private readonly Dictionary<long, List<Slot>> _grid = new Dictionary<long, List<Slot>>();
        private readonly List<Slot> _shown = new List<Slot>();
        private readonly SortedDictionary<string, int> _skip = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private int _created, _createdTotal, _shownMax, _fires, _smokes, _templatesOk, _templatesBad, _thinned, _axisTurned, _lenient;
        // drogi wzoru (poprawka po autotescie 17:41): wzory wedlug drogi, siatki wzorow (poziom 3) wedlug zrodla, prefaby bez sceny, nazwy
        private int _tplComp, _tplName, _tplPrefab, _tplCulture, _meshComp, _meshName, _meshPrefab, _prefabMade, _prefabNoMesh, _prefabMissing, _nameMiss;
        private readonly Dictionary<string, MetaMesh> _meshByName = new Dictionary<string, MetaMesh>(StringComparer.Ordinal);   // nazwa siatki -> wzor (null = brak)
        private readonly Dictionary<string, GameEntity> _prefabs = new Dictionary<string, GameEntity>(StringComparer.Ordinal);   // prefab -> kopia bez sceny (null = brak)
        private int _diagMothers, _diagPrefabs, _stDiag;   // diagnostyka do wioski.log: drzewa 3 pierwszych wsi-matek (+ pierwszej bez wzoru) i 3 prefabow
        private bool _diagFailDone;
        private long _createTicks;
        private int _stCreate, _stFx, _stHover, _stVis, _stTemplate, _stTick, _stRemove;
        private string _lastSummary = "";

        // ---------- rejestracja (z SubModuleMain.OnApplicationTick; tani test co klatke) ----------
        private static WeakReference _regVm, _regScreen;
        private static int _stRegister;

        internal static void EnsureRegistered()
        {
            try
            {
                if (!Settings.Current.MapVillagesEnabled) return;       // wylaczone: nie rejestrujemy (zarejestrowany komponent sam sprzata)
                if (Campaign.Current == null) return;
                MapScreen screen = MapScreen.Instance;
                if (screen == null) return;
                SandBoxViewVisualManager vm = SandBoxViewSubModule.SandBoxViewVisualManager;
                if (vm == null) return;
                if (_regVm != null && ReferenceEquals(_regVm.Target, vm) && _regScreen != null && ReferenceEquals(_regScreen.Target, screen)) return;
                if (vm.GetEntityComponent<SettlementVisualManager>() == null) return;   // mapa jeszcze nie zbudowala swoich menedzerow
                var mine = vm.GetEntityComponent<MapVillagesView>();
                if (mine != null && !ReferenceEquals(mine._screen, screen))
                {
                    // komponent zostal po innej mapie tego samego menedzera (nie powinno sie zdarzac): jego OnFinalize nie rusza
                    // encji cudzej sceny, tylko zrzuca odwolania
                    vm.RemoveEntityComponent(mine);
                    mine = null;
                }
                if (mine == null) vm.AddEntityComponent<MapVillagesView>();
                _regVm = new WeakReference(vm);
                _regScreen = new WeakReference(screen);
            }
            catch (Exception e)
            {
                _stRegister++;
                if (_stRegister <= 3 || _stRegister % 500 == 0) Log.Error("MapVillagesView.EnsureRegistered (potkniecie " + _stRegister + ")", e);
            }
        }

        // ---------- cykl zycia ----------
        protected override void OnInitialize()
        {
            base.OnInitialize();
            _screen = MapScreen.Instance;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { Load(sw); }
            catch (Exception e) { _active = false; Log.Error("MapVillagesView.Load - wioski na mapie wylaczone do nastepnego wczytania", e); }
        }

        private static string DataPath()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // Modules\Armoury\bin\Win64_Shipping_Client
                string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", ".."));
                string p = System.IO.Path.Combine(root, "ModuleData", MapVillageData.FileName);
                if (System.IO.File.Exists(p)) return p;
            }
            catch { }
            try { return System.IO.Path.Combine(BasePath.Name, "Modules", "Armoury", "ModuleData", MapVillageData.FileName); }
            catch { return null; }
        }

        private void Load(System.Diagnostics.Stopwatch sw)
        {
            string path = DataPath();
            if (path == null || !System.IO.File.Exists(path))
            {
                Log.Info("Wioski: brak pliku " + (path ?? MapVillageData.FileName) + " - wioski na mapie nie beda pokazane (gra bez zmian).");
                return;
            }
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            var file = MapVillageData.Parse(Encoding.UTF8.GetString(bytes));
            file.FileCrc32 = MapVillageData.Crc32(bytes);
            string head = "Wioski: plik '" + file.Title + "' (crc32 " + file.FileCrc32.ToString("X8") + ", " + bytes.Length + " B, wierszy danych "
                          + file.DataLines + ")";
            if (file.MissingColumns.Length > 0)
            {
                Log.Info(head + " - BRAK kolumn w naglowku: " + file.MissingColumns + "; wioski na mapie WYLACZONE.");
                return;
            }
            uint sceneCrc = Campaign.Current.MapSceneWrapper.GetSceneXmlCrc();
            int check = MapVillageData.CheckScene(file, sceneCrc);
            if (check == MapVillageData.CheckMismatch)
            {
                Log.Info(head + " - CRC sceny w pliku " + file.SceneCrc + ", scena gry " + sceneCrc
                         + ": plik z innej wersji mapy ROT - wioski na mapie WYLACZONE (wsie gry bez zmian; potrzebny nowy przebieg generatora).");
                return;
            }
            var ms = Campaign.Current.MapSceneWrapper;
            _maskCivil = ms.GetSceneLevel("civilian");
            _maskL1 = ms.GetSceneLevel("level_1");
            _maskL2 = ms.GetSceneLevel("level_2");
            _maskL3 = ms.GetSceneLevel("level_3");
            _maskLooted = ms.GetSceneLevel("looted");   // tylko do zapasu maski (zgliszcza / oblezenie nigdy nie ida do wiosek)
            _maskSiege = ms.GetSceneLevel("siege");
            Vec2 bmin, bmax;
            float bh;
            ms.GetMapBorders(out bmin, out bmax, out bh);

            var byDistrict = MapVillageData.ByDistrict(file.Records);
            int noMother = 0, noMotherDistricts = 0, notVillage = 0, outside = 0;
            foreach (var kv in byDistrict)
            {
                var d = new District { Id = kv.Key };
                try { d.S = Settlement.Find(kv.Key); } catch { d.S = null; }
                string why = d.S == null ? "brak wsi gry" : !d.S.IsVillage ? "nie wies" : null;
                if (d.S == null) noMotherDistricts++;
                foreach (var r in kv.Value)
                {
                    var s = new Slot { R = r, D = d };
                    d.ByOrder.Add(s);
                    _slots.Add(s);
                    if (why != null)
                    {
                        Fail(s, why);
                        if (d.S == null) noMother++; else notVillage++;
                        continue;
                    }
                    if (r.X < bmin.x || r.Y < bmin.y || r.X > bmax.x || r.Y > bmax.y) { Fail(s, "poza granica mapy"); outside++; continue; }
                    long k = Key(r.X, r.Y);
                    List<Slot> l;
                    if (!_grid.TryGetValue(k, out l)) _grid[k] = l = new List<Slot>();
                    l.Add(s);
                }
                _districts.Add(d);
            }
            _active = true;
            Log.Info(head + "; wiosek " + file.Records.Count + " w " + byDistrict.Count + " okregach; odrzucone wiersze " + file.BadRows
                     + " (" + file.ReasonsText() + (file.FirstBad.Length > 0 ? "; pierwszy: " + file.FirstBad : "") + ")"
                     + (file.LevelClamped > 0 ? "; poziom przyciety do 1-3: " + file.LevelClamped : "")
                     + "; CRC sceny: " + (check == MapVillageData.CheckMatch ? "zgodne (" + sceneCrc + ")"
                                          : "brak w pliku - sprawdzenie pominiete (scena gry " + sceneCrc + "; do naglowka: '# scene_xml_crc: " + sceneCrc + "')")
                     + "; pominiete od razu: brak wsi gry " + noMother + " (okregow " + noMotherDistricts + "), nie wies " + notVillage
                     + ", poza granica mapy " + outside + "; maski sceny civilian " + _maskCivil + ", level_1/2/3 " + _maskL1 + "/" + _maskL2 + "/" + _maskL3
                     + ", looted/siege " + _maskLooted + "/" + _maskSiege
                     + "; czas " + sw.ElapsedMilliseconds + " ms. Obrazki powstaja przy kamerze (z <= "
                     + Settings.Current.MapVillagesHideAboveCameraHeight.ToString("0", CultureInfo.InvariantCulture) + "), nic nie idzie do zapisu gry.");
        }

        protected override void OnFinalize()
        {
            if (_finalized) { base.OnFinalize(); return; }
            _finalized = true;
            int removed = 0;
            try
            {
                // MapScreen.OnFinalize konczy komponenty PRZED MapScene.ClearAll (MapScreen.cs:732-741) - scena jeszcze zyje,
                // zdejmujemy swoje encje (jak SettlementVisualManager.OnFinalize czysci swoje). Inna sciezka (stary komponent po
                // innej mapie) - tylko zrzucamy odwolania, cudzej sceny nie ruszamy.
                bool sceneAlive = _screen != null && ReferenceEquals(MapScreen.Instance, _screen);
                string sum = _active ? SummaryText() : "";
                if (sceneAlive) removed = RemoveAll(); else DropAll();
                if (_active)
                    Log.Info("Wioski: koniec mapy (wyjscie z kampanii albo wczytanie) - zdjete obrazki " + removed + (sceneAlive ? "" : " (scena juz nie ta - same odwolania)")
                             + "; " + sum);
            }
            catch (Exception e) { Log.Error("MapVillagesView.OnFinalize", e); }
            _slots.Clear(); _districts.Clear(); _grid.Clear(); _shown.Clear();
            _active = false;
            _screen = null;
            base.OnFinalize();
        }

        public override void ClearVisualMemory()
        {
            // MapScreen.ClearGPUMemory: zasoby pokazanych obrazkow trzeba zaladowac od nowa przy nastepnym ticku widoku
            foreach (var s in _shown) s.NeedRes = true;
        }

        // ---------- praca widoku: 4 razy na sekunde, tylko przy gotowej scenie (VisualTick wola gra tylko gdy MapScreen.IsReady) ----------
        public override void OnVisualTick(MapScreen screen, float realDt, float dt)
        {
            if (!_active || _finalized) return;
            _acc += realDt;
            _sinceSummary += realDt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            try
            {
                if (!Settings.Current.MapVillagesEnabled)
                {
                    if (!_wasOff)
                    {
                        int n = RemoveAll();
                        Log.Info("Wioski: wylaczone w MCM - zdjete obrazki " + n + ".");
                        _wasOff = true;
                    }
                    return;
                }
                if (_wasOff) { _wasOff = false; _firstFillDone = false; Log.Info("Wioski: wlaczone w MCM - obrazki wracaja przy kamerze."); }
                PollRaids();
                UpdateVisibility(screen);
                if (_sinceSummary >= SummaryEvery)
                {
                    _sinceSummary = 0f;
                    string sum = SummaryText();
                    if (sum != _lastSummary) { _lastSummary = sum; Log.Info("Wioski: " + sum); }
                }
            }
            catch (Exception e)
            {
                _stTick++;
                if (_stTick <= 3 || _stTick % 200 == 0) Log.Error("MapVillagesView.OnVisualTick (potkniecie " + _stTick + ")", e);
            }
        }

        private string SummaryText()
        {
            var sb = new StringBuilder();
            sb.Append("postawione ").Append(_created).Append(" z ").Append(_slots.Count)
              .Append(" (od wczytania ").Append(_createdTotal).Append(", pokazane teraz ").Append(_shown.Count).Append(", najwiecej naraz ").Append(_shownMax).Append(')');
            int skipped = 0;
            foreach (var kv in _skip) skipped += kv.Value;
            sb.Append("; pominiete ").Append(skipped);
            if (_skip.Count > 0)
            {
                sb.Append(" (");
                bool first = true;
                foreach (var kv in _skip) { if (!first) sb.Append(", "); first = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); }
                sb.Append(')');
            }
            sb.Append("; wzory wsi-matek ").Append(_templatesOk).Append(" dobrych (drzewo: komponenty ").Append(_tplComp).Append(", nazwy ").Append(_tplName)
              .Append("; prefaby ").Append(_tplPrefab).Append("; kultura ").Append(_tplCulture).Append("), ").Append(_templatesBad).Append(" bez wzoru")
              .Append(_lenient > 0 ? " (dobrych z zapasu maski: " + _lenient + ")" : "")
              .Append("; siatki wzorow (poziom 3) z komponentow ").Append(_meshComp).Append(", z nazw ").Append(_meshName).Append(", z prefabow ").Append(_meshPrefab)
              .Append("; prefaby bez sceny ").Append(_prefabMade).Append(" (bez siatek ").Append(_prefabNoMesh).Append(", brak ").Append(_prefabMissing).Append(')')
              .Append("; nazwy bez siatki ").Append(_nameMiss)
              .Append("; przerzedzone (domy ROT) ").Append(_thinned).Append(", obrot do osi domow ").Append(_axisTurned);
            double ms = _createdTotal > 0 ? _createTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _createdTotal : 0;
            sb.Append("; sredni czas obrazka ").Append(ms.ToString("0.000", CultureInfo.InvariantCulture)).Append(" ms");
            int fireNow = 0, smokeNow = 0;
            foreach (var d in _districts) { if (d.Fx == FxFire && d.FxSlot != null) fireNow++; else if (d.Fx == FxSmoke && d.FxSlot != null) smokeNow++; }
            sb.Append("; ognie od wczytania ").Append(_fires).Append(" (teraz ").Append(fireNow).Append("), dym ").Append(_smokes)
              .Append(" (teraz ").Append(smokeNow).Append(')');
            sb.Append("; potkniecia: tworzenie ").Append(_stCreate).Append(", wzor ").Append(_stTemplate).Append(", ogien ").Append(_stFx)
              .Append(", widocznosc ").Append(_stVis).Append(", dymek ").Append(_stHover).Append(", zdejmowanie ").Append(_stRemove)
              .Append(", tick ").Append(_stTick).Append(", diagnostyka ").Append(_stDiag).Append('.');
            return sb.ToString();
        }

        private void Fail(Slot s, string why)
        {
            if (s.Failed) return;
            s.Failed = true;
            s.Why = why;
            int n;
            _skip.TryGetValue(why, out n);
            _skip[why] = n + 1;
        }

        private static long Key(float x, float y)
        {
            return ((long)Math.Floor(x / Cell) << 32) ^ (uint)(int)Math.Floor(y / Cell);
        }

        // ---------- ogien i dym: stan wsi gry -> pierwsza wioska lancucha (projekt 6.7, "tryb gry" bez ksiegi) ----------
        private void PollRaids()
        {
            for (int i = 0; i < _districts.Count; i++)
            {
                var d = _districts[i];
                if (d.S == null || d.ByOrder.Count == 0) continue;
                try
                {
                    int want = d.S.IsUnderRaid ? FxFire : d.S.IsRaided ? FxSmoke : FxNone;
                    Slot target = null;
                    if (want != FxNone)
                        target = d.FxSlot != null && !d.FxSlot.Failed ? d.FxSlot : FirstUsable(d);
                    if (want == d.Fx && ReferenceEquals(target, d.FxSlot)) continue;
                    if (d.FxSlot != null && !ReferenceEquals(d.FxSlot, target)) { d.FxSlot.FxWanted = FxNone; ApplyFx(d.FxSlot); }
                    if (target != null) { target.FxWanted = want; ApplyFx(target); }
                    if (want == FxFire && d.Fx != FxFire && target != null)
                    {
                        _fires++;
                        Log.Info("Wioski: ogien nad " + target.R.Name + " (okreg " + d.S.Name + ", kolejnosc " + target.R.Order + ") - rabuje " + RaiderName(d.S) + ".");
                    }
                    else if (d.Fx == FxFire && want != FxFire && d.FxSlot != null)
                    {
                        Log.Info("Wioski: zgaszony ogien nad " + d.FxSlot.R.Name + " (okreg " + d.S.Name + ")"
                                 + (want == FxSmoke && target != null ? " - zostaje dym na czas czujnosci wsi gry." : "."));
                    }
                    if (want == FxSmoke && d.Fx != FxSmoke && target != null) _smokes++;
                    d.Fx = want;
                    d.FxSlot = target;
                }
                catch (Exception e)
                {
                    _stFx++;
                    if (_stFx <= 3 || _stFx % 200 == 0) Log.Error("MapVillagesView.PollRaids " + d.Id + " (potkniecie " + _stFx + ")", e);
                }
            }
        }

        private static Slot FirstUsable(District d)
        {
            for (int i = 0; i < d.ByOrder.Count; i++) if (!d.ByOrder[i].Failed) return d.ByOrder[i];
            return null;
        }

        private static string RaiderName(Settlement s)
        {
            try
            {
                var me = s.Party != null ? s.Party.MapEvent : null;
                var lp = me != null && me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                if (lp != null && lp.Name != null) return lp.Name.ToString();
            }
            catch { }
            return "?";
        }

        /// <summary>Czastki na korzeniu obrazka - jedno przelaczenie przy zmianie stanu, jak gra dla rabowanej wsi (SettlementVisual.cs:396-439).</summary>
        private void ApplyFx(Slot s)
        {
            if (s.Root == null || s.FxShown == s.FxWanted) return;
            try
            {
                GameEntity e = s.Root;
                e.RemoveAllParticleSystems();
                if (s.FxWanted == FxFire || s.FxWanted == FxSmoke)
                {
                    e.EntityFlags = e.EntityFlags & ~EntityFlags.DoNotTick;   // czastki musza tykac
                    e.AddParticleSystemComponent(s.FxWanted == FxFire ? FireFx : SmokeFx);
                }
                else e.EntityFlags = e.EntityFlags | EntityFlags.DoNotTick;
                e.CheckResources(true, false);
                s.FxShown = s.FxWanted;
            }
            catch (Exception ex)
            {
                _stFx++;
                s.FxShown = s.FxWanted;   // bez ponawiania co tick; nastepna zmiana stanu sprobuje znowu
                if (_stFx <= 3 || _stFx % 200 == 0) Log.Error("MapVillagesView.ApplyFx " + s.R.Uid + " (potkniecie " + _stFx + ")", ex);
            }
        }

        // ---------- widocznosc: chowanie po oddaleniu / poza promieniem / pod mgla, leniwe tworzenie w komorkach przy kamerze ----------
        private void UpdateVisibility(MapScreen screen)
        {
            if (screen == null || screen.MapCameraView == null || screen.MapCameraView.Camera == null) return;
            Vec3 cam = screen.MapCameraView.Camera.Position;
            float hideZ = Settings.Current.MapVillagesHideAboveCameraHeight;
            if (hideZ <= 0f) hideZ = 160f;
            bool far = cam.z > hideZ;
            float r = cam.z + ShowRadiusExtra;
            float r2 = r * r;
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                var s = _shown[i];
                try
                {
                    float dx = s.R.X - cam.x, dy = s.R.Y - cam.y;
                    bool keep = !far && dx * dx + dy * dy < r2 && MotherVisible(s);
                    if (!keep)
                    {
                        if (s.Root != null) s.Root.SetVisibilityExcludeParents(false);
                        s.Shown = false;
                        _shown.RemoveAt(i);
                    }
                    else if (s.NeedRes && s.Root != null) { s.Root.CheckResources(true, false); s.NeedRes = false; }
                }
                catch (Exception e)
                {
                    _stVis++;
                    s.Shown = false;
                    _shown.RemoveAt(i);
                    s.Strikes++;
                    if (s.Strikes >= StrikesPerVillage) Fail(s, "potkniecia");   // ta jedna wioska nie wraca (inaczej pokaz / schowaj co cwierc sekundy)
                    if (_stVis <= 3 || _stVis % 200 == 0) Log.Error("MapVillagesView.Hide " + s.R.Uid + " (potkniecie wioski " + s.Strikes + "/" + StrikesPerVillage + ", razem " + _stVis + ")", e);
                }
            }
            if (far) return;
            int budget = _firstFillDone ? CreatePerTick : FirstFillCap;
            int made = 0;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int c0x = (int)Math.Floor((cam.x - r) / Cell), c1x = (int)Math.Floor((cam.x + r) / Cell);
            int c0y = (int)Math.Floor((cam.y - r) / Cell), c1y = (int)Math.Floor((cam.y + r) / Cell);
            for (int cx = c0x; cx <= c1x; cx++)
                for (int cy = c0y; cy <= c1y; cy++)
                {
                    List<Slot> l;
                    if (!_grid.TryGetValue(((long)cx << 32) ^ (uint)cy, out l)) continue;
                    for (int j = 0; j < l.Count; j++)
                    {
                        var s = l[j];
                        if (s.Shown || s.Failed) continue;
                        float dx = s.R.X - cam.x, dy = s.R.Y - cam.y;
                        if (dx * dx + dy * dy >= r2) continue;
                        try
                        {
                            if (!MotherVisible(s)) continue;
                            if (s.Root == null)
                            {
                                if (made >= budget) continue;
                                if (!TryCreate(s)) continue;
                                made++;
                            }
                            s.Root.SetVisibilityExcludeParents(true);   // tylko przy zmianie stanu
                            s.Root.CheckResources(true, false);         // scena mapy laduje tylko widoczne (MapScene.cs:222)
                            s.NeedRes = false;
                            s.Shown = true;
                            _shown.Add(s);
                        }
                        catch (Exception e)
                        {
                            _stVis++;
                            s.Strikes++;
                            // nie w _shown = nikt by jej juz nie schowal: na wszelki wypadek schowana od razu
                            if (!s.Shown && s.Root != null) { try { s.Root.SetVisibilityExcludeParents(false); } catch { } }
                            if (s.Strikes >= StrikesPerVillage) Fail(s, "potkniecia");
                            if (_stVis <= 3 || _stVis % 200 == 0) Log.Error("MapVillagesView.Show " + s.R.Uid + " (potkniecie wioski " + s.Strikes + "/" + StrikesPerVillage + ", razem " + _stVis + ")", e);
                        }
                    }
                }
            if (_shown.Count > _shownMax) _shownMax = _shown.Count;
            if (!_firstFillDone)
            {
                _firstFillDone = true;
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Info("Wioski: pierwsze wypelnienie - postawione " + made + " w " + ms.ToString("0", CultureInfo.InvariantCulture) + " ms (kamera z "
                         + cam.z.ToString("0", CultureInfo.InvariantCulture) + ", promien " + r.ToString("0", CultureInfo.InvariantCulture)
                         + (made >= budget ? ", bezpiecznik " + budget + " - reszta po " + CreatePerTick + " na cwierc sekundy" : "")
                         + "); " + SummaryText());
            }
        }

        private static bool MotherVisible(Slot s)
        {
            // ScoutingFog (HideSettlements) chowa wies gry - wioski jej okregu chowaja sie razem z nia (projekt 5.7)
            return s.D.S != null && s.D.S.IsVisible;
        }

        // ---------- tworzenie obrazka ----------
        private bool TryCreate(Slot s)
        {
            Template t = TemplateOf(s.D);
            if (t == null || !t.Ok) { Fail(s, t != null && t.Why.Length > 0 ? t.Why : "brak wzoru"); return false; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                Create(s, t);
                _createTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                _created++;
                _createdTotal++;
                return true;
            }
            catch (Exception e)
            {
                s.Strikes++;
                _stCreate++;
                if (s.Strikes >= StrikesPerVillage) Fail(s, "potkniecia");
                if (_stCreate <= 5 || _stCreate % 100 == 0)
                    Log.Error("MapVillagesView.Create " + s.R.Uid + " '" + s.R.Name + "' (okreg " + s.D.Id + ", potkniecie wioski " + s.Strikes + "/" + StrikesPerVillage
                              + ", razem " + _stCreate + ")", e);
                return false;
            }
        }

        private void Create(Slot s, Template t)
        {
            int lv = Math.Max(1, Math.Min(3, s.R.Level));
            PlanNode[] plan = t.Plans[lv];
            bool[] keep = t.Flat && lv < 3 && t.LeafCount >= 3 ? MapVillageData.ThinLeaves(s.R.Uid, t.LeafCount, lv) : null;
            Scene scene = MapScene;
            if (scene == null) throw new InvalidOperationException("brak sceny mapy");
            GameEntity root = GameEntity.CreateEmpty(scene, false, false, false);   // bez fizyki, bez skryptow
            if (root == (GameEntity)null) throw new InvalidOperationException("CreateEmpty zwrocil null");
            try
            {
                root.Name = "arm_mapvillage_" + SafeName(s.R.Uid);   // gra szuka osad po nazwie encji (SettlementVisual.cs:465) - nazwa nie moze byc id osady
                float yaw = MapVillageData.Yaw(s.R.FrontDeg, t.HasAxis, t.Axis);
                PlaceRoot(s, root, t, lv, yaw);
                var made = new GameEntity[plan.Length];
                int meshes = 0;
                for (int i = 0; i < plan.Length; i++)
                {
                    PlanNode n = plan[i];
                    if (keep != null && n.LeafIndex >= 0 && n.LeafIndex < keep.Length && !keep[n.LeafIndex]) continue;
                    GameEntity parent = n.Parent < 0 ? root : made[n.Parent];
                    if (parent == (GameEntity)null) continue;
                    GameEntity e = GameEntity.CreateEmpty(scene, false, false, false);
                    if (e == (GameEntity)null) continue;
                    for (int m = 0; m < n.Meshes.Length; m++)
                    {
                        MetaMesh copy = n.Meshes[m].CreateCopy();
                        if (copy == null || !copy.IsValid) copy = MetaMesh.GetCopy(n.Meshes[m].GetName(), false, true);
                        if (copy != null && copy.IsValid) { e.AddMultiMesh(copy, true); meshes++; }
                    }
                    MatrixFrame lf = n.Local;
                    e.SetFrame(ref lf, true);                 // jak namiot partii: ramka, potem AddChild bez przeliczania = ramka lokalna
                    parent.AddChild(e, false);
                    if (n.NoSeason) e.EntityFlags = e.EntityFlags | EntityFlags.NotAffectedBySeason;
                    e.EntityFlags = e.EntityFlags | EntityFlags.DoNotTick;
                    e.SetReadyToRender(true);
                    made[i] = e;
                }
                if (meshes == 0) throw new InvalidOperationException("kopia bez siatek");
                if (t.NoSeason) root.EntityFlags = root.EntityFlags | EntityFlags.NotAffectedBySeason;   // snieg jak na matce (projekt 5.1)
                root.EntityFlags = root.EntityFlags | EntityFlags.DoNotTick;
                root.SetReadyToRender(true);                 // jak SettlementVisual.OnStartup (:590-591)
                root.SetEntityEnvMapVisibility(false);
                root.SetVisibilityExcludeParents(false);     // pokaze UpdateVisibility
                if (keep != null) _thinned++;
                if (t.HasAxis) _axisTurned++;
                s.Root = root;
                s.FxShown = FxNone;
                ApplyFx(s);                                  // gdy wies gry wlasnie plonie, ogien od razu
            }
            catch
            {
                try { root.Remove(111); } catch { }
                s.Root = null;
                throw;
            }
        }

        private static string SafeName(string uid)
        {
            var sb = new StringBuilder(uid.Length);
            foreach (char ch in uid) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            return sb.ToString();
        }

        /// <summary>Korzen na terenie: wysokosc z MapSceneWrapper (sam teren), os "gora" wedlug normalnej, obrot, skala matki x poziom.</summary>
        private static void PlaceRoot(Slot s, GameEntity root, Template t, int lv, float yaw)
        {
            float z;
            Vec3 n;
            Campaign.Current.MapSceneWrapper.GetTerrainHeightAndNormal(new Vec2(s.R.X, s.R.Y), out z, out n);
            Vec3 u = n;
            if (u.Length < 0.5f || u.z < 0.5f) u = new Vec3(0f, 0f, 1f);
            u.Normalize();
            Vec3 s0 = new Vec3(MathF.Cos(yaw), MathF.Sin(yaw), 0f);
            Vec3 f = Vec3.CrossProduct(u, s0);
            f.Normalize();
            Vec3 side = Vec3.CrossProduct(f, u);
            side.Normalize();
            Mat3 rot = new Mat3(in side, in f, in u);
            Vec3 scale = t.Scale * MapVillageData.LevelScale(lv);
            rot.ApplyScaleLocal(in scale);
            Vec3 o = new Vec3(s.R.X, s.R.Y, z - 0.02f, 1f);
            MatrixFrame fr = new MatrixFrame(in rot, in o);
            root.SetFrame(ref fr, true);
            s.Z = z;
        }

        // ---------- wzor wsi-matki: uklad siatek widocznych przy danym poziomie (algorytm gry, bez kopiowania encji) ----------
        // AUTOTEST 07.10 17:41: 41 wzorow, 0 dobrych - "brak siatek" (MultiMeshComponentCount = 0 na calym drzewie kazdej matki, tez
        // przy zapasie maski). Natywny "Town Scene Manager" przy wczytaniu mapy zbiera siatki osad do wspolnych ikon (rgl_log 17:41:06
        // "Town scene manager: Total mesh: 23858, Total Unique Mesh: 304"; rglTown_icon_component = ComponentType.TownIcon bez klasy
        // zarzadzanej, GetComponentAtIndex dalby null). Encje, nazwy, ramki i maski zostaja. Drogi po kolei, kazda liczona w logu:
        //  1 drzewo matki: siatka encji = jej komponenty MetaMesh, a gdy ich brak - siatka po nazwie encji (MapVillageData.MeshForName,
        //    MetaMesh.GetCopy jak namiot partii); ramki i maski z encji w grze;
        //  2 prefaby: encje matki z nazwa prefabu (GetPrefabName, potem GetOldPrefabName) -> kopia prefabu BEZ SCENY
        //    (GameEntity.Instantiate(null, ...) jak DestructableComponent.cs:194 / MissionDeploymentBoundaryMarker.cs:184) i jej siatki;
        //  3 kultura: prefab domow kultury wsi (Westeros), potem stary prefab samej matki (map_icon_full_*), na koncu andal_village3.
        private Template TemplateOf(District d)
        {
            if (d.TemplateTried) return d.T;
            d.TemplateTried = true;
            try { d.T = BuildTemplate(d); }
            catch (Exception e)
            {
                _stTemplate++;
                d.T = new Template { Why = "blad wzoru" };
                if (_stTemplate <= 5 || _stTemplate % 100 == 0) Log.Error("MapVillagesView.Template " + d.Id + " (potkniecie " + _stTemplate + ")", e);
            }
            if (d.T.Ok)
            {
                _templatesOk++;
                if (d.T.Lenient) _lenient++;
                if (d.T.Way == WayTree) { if (d.T.FromName > 0) _tplName++; else _tplComp++; }
                else if (d.T.Way == WayPrefab) _tplPrefab++;
                else if (d.T.Way == WayCulture) _tplCulture++;
                _meshComp += d.T.FromComp;
                _meshName += d.T.FromName;
                _meshPrefab += d.T.FromPrefab;
            }
            else _templatesBad++;
            // diagnostyka (nastepny autotest rozstrzyga, jesli poprawka nie trafi): 3 pierwsze matki po wczytaniu + pierwsza bez wzoru
            if (_diagMothers < 3 || (!d.T.Ok && !_diagFailDone))
            {
                if (_diagMothers >= 3) _diagFailDone = true;
                _diagMothers++;
                DiagMother(d);
            }
            return d.T;
        }

        private static GameEntity MotherOf(Settlement s)
        {
            var svm = SettlementVisualManager.Current;
            var vis = svm != null && s != null ? svm.GetVisualOfEntity(s.Party) as SettlementVisual : null;
            return vis != null ? vis.StrategicEntity : null;
        }

        private Template BuildTemplate(District d)
        {
            var t = new Template();
            Settlement s = d.S;
            if (s == null) { t.Why = "brak wsi gry"; return t; }
            if (!s.IsVillage) { t.Why = "nie wies"; return t; }
            GameEntity mother = MotherOf(s);
            if (mother == (GameEntity)null) { t.Why = "brak obrazka wsi gry"; return t; }
            MatrixFrame mg = mother.GetGlobalFrame();
            Vec3 sc = mg.rotation.GetScaleVector();
            t.Scale = new Vec3(Clamp(sc.x, 0.2f, 5f), Clamp(sc.y, 0.2f, 5f), Clamp(sc.z, 0.2f, 5f));
            t.NoSeason = (mother.EntityFlags & EntityFlags.NotAffectedBySeason) != 0;
            // droga 1: drzewo matki (komponenty, a gdy ich brak - po nazwie encji); droga 2: prefaby encji matki bez sceny
            if (FillPlans(t, mother, false, false)) t.Way = WayTree;
            else if (FillPlans(t, mother, true, false)) t.Way = WayPrefab;
            else
            {
                // droga 3: prefab wsi kultury (Westeros: domy andal / fm), potem stary prefab samej matki, na koncu jeden staly
                string cid = s.Culture != null ? s.Culture.StringId : "";
                string[] cands = { MapVillageData.CultureHousePrefab(cid), PrefabNameOf(mother), MapVillageData.LastResortPrefab };
                foreach (string p in cands)
                {
                    GameEntity tmp = PrefabTemplate(p);
                    if (tmp == (GameEntity)null) continue;
                    if (FillPlans(t, tmp, false, true)) { t.Way = WayCulture; t.WaySource = p; break; }
                }
            }
            if (t.Way == 0) { t.Why = "brak siatek"; return t; }
            // poziom bez siatek bierze najblizszy z siatkami (3 -> 2 -> 1, 1 -> 2 -> 3)
            for (int lv = 1; lv <= 3; lv++)
            {
                if (t.Plans[lv].Length > 0) continue;
                int[] order = lv == 1 ? new[] { 2, 3 } : lv == 2 ? new[] { 3, 1 } : new[] { 2, 1 };
                foreach (int o in order) if (t.Plans[o].Length > 0) { t.Plans[lv] = t.Plans[o]; break; }
            }
            if (t.Plans[3].Length == 0) { t.Why = "brak siatek"; t.Way = 0; return t; }
            string s1 = Signature(t.Plans[1]), s2 = Signature(t.Plans[2]), s3 = Signature(t.Plans[3]);
            t.Flat = s1 == s2 && s2 == s3;
            t.LeafCount = 0;
            var xs = new List<float>();
            var ys = new List<float>();
            foreach (var n in t.Plans[3])
            {
                if (n.LeafIndex >= 0) { t.LeafCount++; xs.Add(n.RootX); ys.Add(n.RootY); }
                if (n.Meshes.Length == 0) continue;
                if (n.Src == SrcPrefab) t.FromPrefab += n.Meshes.Length;
                else if (n.Src == SrcName) t.FromName += n.Meshes.Length;
                else t.FromComp += n.Meshes.Length;
            }
            float axis;
            t.HasAxis = MapVillageData.LongAxis(xs, ys, out axis);
            t.Axis = axis;
            t.Ok = true;
            return t;
        }

        /// <summary>Plany 1/2/3 z drzewa src: najpierw scisly warunek maski (gra + encje bez poziomow), gdy nic - zapas maski.
        /// includeRoot = siatki samego src tez (kopia prefabu wsi kultury: korzen prefabu bywa jedyna siatka).</summary>
        private bool FillPlans(Template t, GameEntity src, bool prefabs, bool includeRoot)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool lenient = pass == 1;
                bool any = false;
                for (int lv = 1; lv <= 3; lv++)
                {
                    var a = new WalkArgs { Mask = _maskCivil | LevelMask(lv), Lenient = lenient, Prefabs = prefabs };
                    t.Plans[lv] = BuildPlan(src, a, includeRoot);
                    if (t.Plans[lv].Length > 0) any = true;
                }
                if (any) { t.Lenient = lenient; return true; }
            }
            return false;
        }

        private uint LevelMask(int lv)
        {
            return lv >= 3 ? _maskL3 : lv == 2 ? _maskL2 : _maskL1;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static string Signature(PlanNode[] plan)
        {
            var sb = new StringBuilder();
            sb.Append(plan.Length).Append(':');
            foreach (var n in plan)
            {
                sb.Append(n.Parent).Append('/');
                foreach (var m in n.Meshes) sb.Append(m.GetName()).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        /// <summary>Encje src widoczne przy masce (MapVillageData.LevelVisible), w kolejnosci rodzic przed dzieckiem; galezie bez siatek
        /// wycinane; powtorzone liscie (ta sama siatka w tym samym miejscu) raz.</summary>
        private PlanNode[] BuildPlan(GameEntity src, WalkArgs a, bool includeRoot)
        {
            var raw = new List<PlanNode>();
            int top = -1;
            if (includeRoot)
            {
                int rs;
                MetaMesh[] rm = MeshesOf(src, out rs);
                raw.Add(new PlanNode { Parent = -1, Local = MatrixFrame.Identity, Meshes = rm, NoSeason = (src.EntityFlags & EntityFlags.NotAffectedBySeason) != 0, Src = SrcPrefab });
                top = 0;
            }
            Walk(src, top, a, raw, 0, includeRoot);
            var keep = new bool[raw.Count];
            for (int i = raw.Count - 1; i >= 0; i--)
            {
                if (raw[i].Meshes.Length > 0) keep[i] = true;
                if (keep[i] && raw[i].Parent >= 0) keep[raw[i].Parent] = true;
            }
            var remap = new int[raw.Count];
            var outList = new List<PlanNode>();
            for (int i = 0; i < raw.Count; i++)
            {
                remap[i] = -1;
                if (!keep[i]) continue;
                var n = raw[i];
                n.Parent = n.Parent >= 0 ? remap[n.Parent] : -1;
                remap[i] = outList.Count;
                outList.Add(n);
            }
            // liscie z siatka (do przerzedzenia i do osi domow) + polozenie w ukladzie korzenia matki
            var hasChild = new bool[outList.Count];
            foreach (var n in outList) if (n.Parent >= 0) hasChild[n.Parent] = true;
            int leaf = 0;
            for (int i = 0; i < outList.Count; i++)
            {
                var n = outList[i];
                Vec3 p = n.Local.origin;
                int up = n.Parent;
                while (up >= 0) { p = outList[up].Local.TransformToParent(in p); up = outList[up].Parent; }
                n.RootX = p.x;
                n.RootY = p.y;
                n.LeafIndex = !hasChild[i] && n.Meshes.Length > 0 ? leaf++ : -1;
            }
            return outList.ToArray();
        }

        private void Walk(GameEntity src, int parent, WalkArgs a, List<PlanNode> outList, int depth, bool inPrefab)
        {
            if (depth > 8) return;
            uint levels = _maskL1 | _maskL2 | _maskL3;
            var seen = new HashSet<string>(StringComparer.Ordinal);   // Dothrakowie: 4 x ta sama siatka w tym samym miejscu
            foreach (GameEntity c in src.GetChildren())
            {
                if (c == (GameEntity)null) continue;
                string name = c.Name ?? "";
                if (MapVillageData.IsHelperName(name)) continue;
                uint m = (uint)c.GetUpgradeLevelMask();
                if (!MapVillageData.LevelVisible(m, a.Mask, a.Lenient, _maskCivil, _maskLooted, _maskSiege, levels)) continue;
                MatrixFrame lf = c.GetFrame();
                bool noSeason = (c.EntityFlags & EntityFlags.NotAffectedBySeason) != 0;
                if (a.Prefabs && !inPrefab)
                {
                    // droga 2: encja z nazwa prefabu - zamiast jej dzieci w grze kopia prefabu bez sceny w ramce tej encji
                    GameEntity tmp = PrefabTemplate(PrefabNameOf(c));
                    if (tmp != (GameEntity)null)
                    {
                        int ts;
                        MetaMesh[] tm = MeshesOf(tmp, out ts);
                        int pi = outList.Count;
                        outList.Add(new PlanNode { Parent = parent, Local = lf, Meshes = tm, NoSeason = noSeason, Src = SrcPrefab });
                        Walk(tmp, pi, a, outList, depth + 1, true);
                        continue;
                    }
                }
                int s;
                MetaMesh[] meshes = MeshesOf(c, out s);
                if (meshes.Length > 0 && c.ChildCount == 0)
                {
                    string sig = meshes[0].GetName() + "|" + Math.Round(lf.origin.x, 2).ToString(CultureInfo.InvariantCulture) + "|"
                                 + Math.Round(lf.origin.y, 2).ToString(CultureInfo.InvariantCulture) + "|" + Math.Round(lf.origin.z, 2).ToString(CultureInfo.InvariantCulture);
                    if (!seen.Add(sig)) continue;
                }
                int idx = outList.Count;
                outList.Add(new PlanNode { Parent = parent, Local = lf, Meshes = meshes, NoSeason = noSeason, Src = inPrefab ? SrcPrefab : s });
                Walk(c, idx, a, outList, depth + 1, inPrefab);
            }
        }

        /// <summary>Siatki encji: komponenty MetaMesh, a gdy ich brak (Town Scene Manager) - jedna siatka po nazwie encji.</summary>
        private MetaMesh[] MeshesOf(GameEntity e, out int src)
        {
            src = SrcComp;
            List<MetaMesh> list = null;
            int mc = e.MultiMeshComponentCount;
            for (int i = 0; i < mc; i++)
            {
                MetaMesh mm = e.GetMetaMesh(i);
                if (mm == null || !mm.IsValid) continue;
                if (list == null) list = new List<MetaMesh>();
                list.Add(mm);
            }
            if (list != null) return list.ToArray();
            MetaMesh byName = MeshByName(e.Name, e.ChildCount == 0);
            if (byName == null) return NoMeshes;
            src = SrcName;
            return new[] { byName };
        }

        /// <summary>Wzor siatki po nazwie encji (MetaMesh.GetCopy bez bledow i z null, jak MobilePartyVisual namiot) - raz na nazwe siatki.</summary>
        private MetaMesh MeshByName(string entityName, bool leaf)
        {
            string mesh = MapVillageData.MeshForName(entityName, leaf);
            if (mesh == null) return null;
            MetaMesh mm;
            if (_meshByName.TryGetValue(mesh, out mm)) return mm;
            _meshByName[mesh] = null;   // blad w srodku = ta nazwa juz nie probowana
            mm = MetaMesh.GetCopy(mesh, false, true);
            if (mm != null && !mm.IsValid) mm = null;
            _meshByName[mesh] = mm;
            if (mm == null) _nameMiss++;
            return mm;
        }

        private static string PrefabNameOf(GameEntity e)
        {
            string p = e.GetPrefabName();
            if (string.IsNullOrEmpty(p)) p = e.GetOldPrefabName();
            return string.IsNullOrEmpty(p) ? null : p;
        }

        /// <summary>Kopia prefabu BEZ SCENY, bez skryptow i fizyki (tylko do odczytu siatek; nigdy nie trafia do sceny mapy) - raz na prefab.
        /// PrefabExists najpierw (bez tego gra stawia czerwony TEMP - NightRest.cs:583-584).</summary>
        private GameEntity PrefabTemplate(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            GameEntity g;
            if (_prefabs.TryGetValue(name, out g)) return g;
            g = null;
            _prefabs[name] = null;   // blad w srodku = ten prefab juz nie probowany
            if (GameEntity.PrefabExists(name)) g = GameEntity.Instantiate((Scene)null, name, false, false, "");
            if (g == (GameEntity)null) { _prefabMissing++; return null; }
            List<GameEntity> all = new List<GameEntity>();
            g.GetChildrenRecursive(ref all);
            all.Add(g);
            int meshes = 0;
            foreach (var e in all) meshes += e.MultiMeshComponentCount;
            if (meshes == 0) _prefabNoMesh++;   // zostaje: siatki po nazwach encji prefabu
            _prefabMade++;
            _prefabs[name] = g;
            if (_diagPrefabs < 3)
            {
                _diagPrefabs++;
                try { Log.Info("Wioski: drzewo prefabu " + name + " (kopia bez sceny, komponentow MetaMesh w drzewie " + meshes + "):" + TreeText(g, 40)); }
                catch (Exception ex) { _stDiag++; if (_stDiag <= 3) Log.Error("MapVillagesView.DiagPrefab " + name, ex); }
            }
            return g;
        }

        // ---------- diagnostyka do wioski.log (tylko odczyt; nic nie zmienia w encjach) ----------
        private static FieldInfo _fLevelMask;

        private void DiagMother(District d)
        {
            try
            {
                GameEntity mother = MotherOf(d.S);
                var t = d.T;
                var sb = new StringBuilder();
                sb.Append("Wioski: drzewo wsi-matki ").Append(d.Id).Append(" '").Append(d.S != null ? d.S.Name.ToString() : "?").Append("' kultura ")
                  .Append(d.S != null && d.S.Culture != null ? d.S.Culture.StringId : "?").Append(": wynik ");
                if (t.Ok)
                {
                    sb.Append(t.Way == WayTree ? "drzewo" : t.Way == WayPrefab ? "prefaby" : "kultura " + t.WaySource)
                      .Append(t.Lenient ? " (zapas maski)" : "").Append(", siatki poziomu 3: komponenty ").Append(t.FromComp).Append(", nazwy ").Append(t.FromName)
                      .Append(", prefaby ").Append(t.FromPrefab).Append(t.Flat ? ", jeden wyglad na 1/2/3 (przerzedzenie)" : "");
                }
                else sb.Append("BEZ WZORU (").Append(t.Why).Append(')');
                for (int lv = 1; lv <= 3; lv++)
                {
                    var p = t.Plans[lv];
                    int mm = 0;
                    if (p != null) foreach (var n in p) mm += n.Meshes.Length;
                    sb.Append(lv == 1 ? "; plan l1/l2/l3 wezlow/siatek " : " | ").Append(p != null ? p.Length : 0).Append('/').Append(mm);
                }
                sb.Append("; maski sceny civilian ").Append(_maskCivil).Append(" level_1/2/3 ").Append(_maskL1).Append('/').Append(_maskL2).Append('/').Append(_maskL3)
                  .Append(" looted ").Append(_maskLooted).Append(" siege ").Append(_maskSiege);
                if (mother == (GameEntity)null) { sb.Append("; brak encji matki"); Log.Info(sb.ToString()); return; }
                try
                {
                    var svm = SettlementVisualManager.Current;
                    var vis = svm != null ? svm.GetVisualOfEntity(d.S.Party) as SettlementVisual : null;
                    if (_fLevelMask == null) _fLevelMask = typeof(SettlementVisual).GetField("_currentLevelMask", BindingFlags.Instance | BindingFlags.NonPublic);
                    object v = vis != null && _fLevelMask != null ? _fLevelMask.GetValue(vis) : null;
                    sb.Append("; maska wsi w grze teraz ").Append(v != null ? v.ToString() : "?");
                }
                catch { sb.Append("; maska wsi w grze teraz ?"); }
                sb.Append("; skrypt 'Town Entity Manager' ").Append(mother.HasScriptComponent("Town Entity Manager") ? "tak" : "nie");
                sb.Append(TreeText(mother, 80));
                Log.Info(sb.ToString());
            }
            catch (Exception e)
            {
                _stDiag++;
                if (_stDiag <= 3) Log.Error("MapVillagesView.DiagMother " + d.Id + " (potkniecie " + _stDiag + ")", e);
            }
        }

        /// <summary>Drzewo encji, linia na encje: nazwa, prefab / stary prefab, maska (kumul., widocznosc poziomow z rodzicami), widoczna,
        /// komponenty wedlug typu (MM swiatlo zlozony plotno czastki ikona-osady inny naklejka), siatki komponentow, pierwsza siatka,
        /// rozmiar BB, ramka lokalna, dzieci, siatka po nazwie.</summary>
        private static string TreeText(GameEntity root, int maxLines)
        {
            var sb = new StringBuilder();
            int lines = 0;
            TreeLine(sb, root, 0, ref lines, maxLines);
            if (lines >= maxLines) sb.Append("\n    ... (ucieto po ").Append(maxLines).Append(" encjach)");
            return sb.ToString();
        }

        private static void TreeLine(StringBuilder sb, GameEntity e, int depth, ref int lines, int maxLines)
        {
            if (lines >= maxLines || depth > 8) return;
            lines++;
            string name = e.Name ?? "";
            sb.Append("\n    ").Append(' ', depth * 2).Append(name.Length > 0 ? name : "(bez nazwy)");
            string pf = e.GetPrefabName(), op = e.GetOldPrefabName();
            if (!string.IsNullOrEmpty(pf)) sb.Append(" | prefab ").Append(pf);
            if (!string.IsNullOrEmpty(op)) sb.Append(" | stary prefab ").Append(op);
            sb.Append(" | maska ").Append((uint)e.GetUpgradeLevelMask()).Append(" kumul ").Append((uint)e.GetUpgradeLevelMaskCumulative())
              .Append(" z rodzicami ").Append(e.GetVisibilityLevelMaskIncludingParents())
              .Append(" | widoczna ").Append(e.IsVisibleIncludeParents() ? 1 : 0)
              .Append(" | flagi ").Append(((uint)e.EntityFlags).ToString("X"));
            sb.Append(" | komp");
            for (int ct = 0; ct <= 7; ct++) sb.Append(' ').Append(e.GetComponentCount((GameEntity.ComponentType)ct));
            int subs = 0;
            int mc = e.MultiMeshComponentCount;
            for (int i = 0; i < mc; i++) { MetaMesh mm = e.GetMetaMesh(i); if (mm != null && mm.IsValid) subs += mm.MeshCount; }
            sb.Append(" | siatek ").Append(subs);
            Mesh fm = e.GetFirstMesh();
            if (fm != null && fm.IsValid) sb.Append(" pierwsza ").Append(fm.Name);
            Vec3 bmin = e.GlobalBoxMin, bmax = e.GlobalBoxMax;
            sb.Append(" | BB ").Append(F2(bmax.x - bmin.x)).Append('x').Append(F2(bmax.y - bmin.y)).Append('x').Append(F2(bmax.z - bmin.z));
            MatrixFrame lf = e.GetFrame();
            Vec3 scl = lf.rotation.GetScaleVector();
            sb.Append(" | ramka ").Append(F2(lf.origin.x)).Append(',').Append(F2(lf.origin.y)).Append(',').Append(F2(lf.origin.z))
              .Append(" skala ").Append(F2(scl.x)).Append(',').Append(F2(scl.y)).Append(',').Append(F2(scl.z));
            int cc = e.ChildCount;
            sb.Append(" | dzieci ").Append(cc);
            string byName = MapVillageData.MeshForName(name, cc == 0);
            if (byName != null) sb.Append(" | po nazwie ").Append(byName);
            foreach (GameEntity c in e.GetChildren())
            {
                if (c == (GameEntity)null) continue;
                TreeLine(sb, c, depth + 1, ref lines, maxLines);
            }
        }

        private static string F2(float v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // ---------- sprzatanie ----------
        private int RemoveAll()
        {
            int n = 0;
            foreach (var s in _slots)
            {
                if (s.Root != null)
                {
                    try { s.Root.Remove(111); n++; }
                    catch (Exception e) { _stRemove++; if (_stRemove <= 3) Log.Error("MapVillagesView.Remove " + s.R.Uid, e); }
                }
                s.Root = null;
                s.V = null;
                s.Shown = false;
                s.NeedRes = false;
                s.FxShown = FxNone;
            }
            _shown.Clear();
            _created = 0;
            foreach (var d in _districts) { d.T = null; d.TemplateTried = false; }   // wzory od nowa (siatki matek moga byc przeladowane)
            _templatesOk = _templatesBad = _lenient = _thinned = _axisTurned = 0;   // liczniki "teraz" razem z wzorami (bez podwojnego liczenia po wlaczeniu w MCM)
            ClearTemplateCaches();
            return n;
        }

        private void DropAll()
        {
            foreach (var s in _slots) { s.Root = null; s.V = null; s.Shown = false; }
            foreach (var d in _districts) { d.T = null; d.TemplateTried = false; }
            _shown.Clear();
            _created = 0;
            ClearTemplateCaches();
        }

        /// <summary>Wzory siatek po nazwie i kopie prefabow bez sceny razem z ich licznikami (nic z tego nie jest w scenie - samo puszczenie
        /// odwolan, jak gra z kopia prefabu w DestructableComponent).</summary>
        private void ClearTemplateCaches()
        {
            _meshByName.Clear();
            _prefabs.Clear();
            _tplComp = _tplName = _tplPrefab = _tplCulture = _meshComp = _meshName = _meshPrefab = 0;
            _prefabMade = _prefabNoMesh = _prefabMissing = _nameMiss = 0;
        }

        // ---------- dymek po najechaniu: bez fizyki, punkt terenu pod kursorem w obroconym obrysie (krytyk K4) ----------
        // MapScreen.HandleMouse (:1599-1605): komponenty po kolei wedlug Priority, petla staje na pierwszym, ktory zwroci true;
        // ustawiamy tylko hoveredVisual (selectedVisual zostaje null - klik idzie w teren, :1623-1628) i zawsze zwracamy false.
        public override bool OnVisualIntersected(Ray mouseRay, UIntPtr[] intersectedEntityIDs, Intersection[] intersectionInfos, int entityCount,
            Vec3 worldMouseNear, Vec3 worldMouseFar, Vec3 terrainIntersectionPoint, ref MapEntityVisual hoveredVisual, ref MapEntityVisual selectedVisual)
        {
            if (hoveredVisual != null || !_active || _finalized || _shown.Count == 0) return false;
            try
            {
                if (!Settings.Current.MapVillagesEnabled || !Settings.Current.MapVillageNamesOnHover) return false;
                float px = terrainIntersectionPoint.x, py = terrainIntersectionPoint.y;
                int bx = (int)Math.Floor(px / Cell), by = (int)Math.Floor(py / Cell);
                Slot best = null;
                float bestD = float.MaxValue;
                for (int ix = -1; ix <= 1; ix++)
                    for (int iy = -1; iy <= 1; iy++)
                    {
                        List<Slot> l;
                        if (!_grid.TryGetValue(((long)(bx + ix) << 32) ^ (uint)(by + iy), out l)) continue;
                        for (int j = 0; j < l.Count; j++)
                        {
                            var s = l[j];
                            if (!s.Shown || s.Root == null) continue;
                            if (!MapVillageData.InFootprint(s.R, px, py, HoverMargin)) continue;
                            float dx = s.R.X - px, dy = s.R.Y - py, dd = dx * dx + dy * dy;
                            if (dd < bestD) { bestD = dd; best = s; }
                        }
                    }
                if (best != null)
                {
                    if (best.V == null) best.V = new Visual(best);
                    hoveredVisual = best.V;
                }
            }
            catch (Exception e)
            {
                _stHover++;
                if (_stHover <= 3 || _stHover % 500 == 0) Log.Error("MapVillagesView.Hover (potkniecie " + _stHover + ")", e);
            }
            return false;
        }

        /// <summary>"Wizerunek" wioski dla MapScreen: tylko dymek. Klik idzie w teren, kursor bez raczki (IsMobileEntity).</summary>
        private sealed class Visual : MapEntityVisual
        {
            private readonly Slot _s;
            internal Visual(Slot s) { _s = s; }

            public override CampaignVec2 InteractionPositionForPlayer => new CampaignVec2(new Vec2(_s.R.X, _s.R.Y), true);
            public override MapEntityVisual AttachedTo => null;
            public override bool IsMobileEntity => true;            // MapScreen.cs:1608 - bez kursora "raczki", wioski nie da sie kliknac
            public override bool OnMapClick(bool followModifierUsed) { return false; }
            public override void OnOpenEncyclopedia() { }
            public override bool IsVisibleOrFadingOut() { return _s.Shown; }
            public override Vec3 GetVisualPosition() { return new Vec3(_s.R.X, _s.R.Y, _s.Z); }

            public override void OnHover()
            {
                try
                {
                    var list = new List<TooltipProperty>();
                    list.Add(new TooltipProperty("", _s.R.Name, 0, false, TooltipProperty.TooltipPropertyFlags.Title));
                    TextObject district = _s.D.S != null ? _s.D.S.Name : new TextObject(_s.D.Id);
                    list.Add(new TooltipProperty("", VillageTexts.Make(VillageTexts.VilTipDistrict, "DISTRICT", district).ToString(), 0));
                    int people = _s.R.Settlements * 250;   // osada = ok. 250 ludzi (PROJEKT-RABUNEK pkt 1)
                    list.Add(new TooltipProperty("", VillageTexts.Make(VillageTexts.VilTipPeople, "PEOPLE", people.ToString("N0", CultureInfo.InvariantCulture),
                        "SETTLEMENTS", _s.R.Settlements).ToString(), 0));
                    if (_s.FxWanted == FxFire && _s.D.S != null)
                    {
                        string raider = RaiderName(_s.D.S);
                        list.Add(new TooltipProperty("", raider != "?"
                            ? VillageTexts.Make(VillageTexts.VilTipBurning, "RAIDER", raider).ToString()
                            : VillageTexts.Make(VillageTexts.VilBurning, "VILLAGE", _s.R.Name).ToString(), 0));
                    }
                    InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new object[] { list });
                }
                catch (Exception e)
                {
                    _stTip++;
                    if (_stTip <= 3 || _stTip % 200 == 0) Log.Error("MapVillagesView.OnHover " + _s.R.Uid + " (potkniecie " + _stTip + ")", e);
                }
            }

            private static int _stTip;   // licznik bledow dymka (najechanie co chwila = bez zalewu logu)
        }
    }
}
