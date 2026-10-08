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
// POPRAWKA WYGLADU 08.10 (zrzut Jeffa: wioska = mala szara brylka): Town Scene Manager laczy domy wsi w JEDNA grupe - zostaje jedna
// encja-zastepca (np. szopa fm_wm_shed2_snow) przeniesiona pod matke z ikona calej grupy, a encja prefabu domow (fm_village4,
// andal_village*) zostaje pusta (0 dzieci, 0 siatek). Droga 1 rozwija teraz taka zwinieta encje kopia jej prefabu bez sceny (wszystkie
// domy z ramkami), zastepce po nazwie zdejmuje jako duplikat, a skale obrazka liczy z BB widocznych domow matki (MapVillageData.FitToMother).
// Prefab encji = jej NAZWA (PrefabExists) - GetPrefabName / GetOldPrefabName nie sa wolane nigdzie (naruszenie pamieci w silniku na
// bo_village / *_village_looted, CrashScribe 08.10 02:03); drzewa encji w wioski.log tylko w autotescie, lista siatek 3 wzorow zawsze
// (w autotescie tez pierwszy wzor kazdego rodzaju). Recenzja 08.10: flaga NotAffectedBySeason matki na KAZDEJ encji obrazka (zima ROT).
// Poziom: 0.70 / 0.75 / 0.80 wielkosci matki.
// WYGLAD 2 (08.10, zdjecia autotestu 03:37 + plik v4 z kolumna "model"): (1) KAZDY budynek na wysokosci gruntu w swoim punkcie (korzen
// bez pochylania wedlug jednej normalnej; domy na brzegu Poppymead / Sweet Bridge byly zapadniete po dach), wioska na stoku / brzegu
// przesuwana w granicach obrysu z pliku albo sciskana, budynki z krawedzi stoku zdejmowane - wszystko liczone w logu; (2) kepa "village"
// zwarta jak wies gry: domy wzoru w ukladzie prefabu + detale (sterta drewna, oborka, studnia / stog, woz) do 9 budynkow, poziom 1 / 2 / 3
// = co najmniej 5 / 7 / wszystkie, przerzedzanie od BRZEGU kepy; (3) obrazek wedlug kolumny "model" (village / mill / windmill / farm /
// granary / fishing; brak kolumny = village): wzor na (okreg, model), siatki mapowe gry / ROT i modele scenowe (mlyn wodny z kolem,
// wiatrak, stodoly) jako SAME KOPIE SIATEK ze skala z BB - bez fizyki, skryptow, czastek i dzwiekow. Kazda uzyta siatka w wioski.log.
// Recenzja wygladu 2 (08.10): poziom 1 / 2 kepy "village" ma najwyzej 2 / 3 detale (inaczej detale z luk przy srodku wypieraly domy);
// mlyn / rybacy z woda ZA kepa (front z pliku na droge) - obrazek odwrocony o pi, zeby kolo / pomost staly od wody.
// WYGLAD 3 (08.10, zdjecia autotestu 04:42): (1) wysokosc KAZDEGO budynku = dol BB (ze skala i obrotem) na najnizszym gruncie pod obrysem
// minus 0.03 - domy Reach (Poppymead, Berrybush) byly zapadniete po dach takze na plaskim, bo "wysokosc z prefabu" liczyla sie od korzenia
// wsi-matki (Berrybush: prefab 0.23 pod korzeniem); (2) mlyn / rybacy: brzeg wody z wachlarza 16 kierunkow (sciany siatki nawigacyjnej
// bez ladu + teren pod poziomem wody), front obrazka do wody, obrazek przesuniety tak, ze kolo mlyna stoi NAD woda i jej dotyka (przy
// wodzie na polnoc mlyn obrocony o 50 st. wokol kola - kolo od strony kamery), pomost od brzegu, lodzie na wodzie; brak wody w 5 jedn. -
// mlyn = wiatrak, rybacy = wioska; (3) wiatrak = sturgia_windmill_a ze skrzydlami (smiglo) i schodami jako doczepione kopie siatek,
// skrzydla od strony kamery (battania_windmill to wieza bez skrzydel - zapas); (4) srodek obrazka i kazdy budynek na ladzie (sciana siatki
// ladowej i teren nad morzem 5.5) - inaczej przeniesiony / zdjety. Bez nowych prefabow scenowych, fizyki, skryptow, czastek i dzwiekow.
// Recenzja wygladu 3 (08.10): srodek obrazka przeniesiony (na lad, ku wodzie, ze stoku) nie blizej innej wioski niz 3.5 jedn. (plik: >= 4.5) -
// na lad do 6 jedn. i ku wodzie do 3.5 jedn. stawialo wioski przy sasiadach (Gallowsmill 2.1 od Blackholt, Talosa 2.6 od Stonecove).
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
        public int Model;                 // v4: rodzaj obrazka (MapVillageData.ModelVillage / Mill / Windmill / Farm / Granary / Fishing)
        public bool ModelUnknown;         // v4: w kolumnie "model" nieznana wartosc (obrazek village, liczone w logu)
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
        public bool HasModelColumn;       // v4: naglowek ma kolumne "model" (bez niej kazda wioska = village)
        public readonly int[] ModelCounts = new int[MapVillageData.ModelCount];   // wiersze wedlug rodzaju obrazka
        public int ModelUnknown;          // nieznana wartosc w kolumnie "model" (obrazek village)
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

        /// <summary>Rozklad rodzajow obrazka do logu, np. "village 1053, farm 446, ...".</summary>
        internal string ModelsText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ModelCounts.Length; i++)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(MapVillageData.ModelName(i)).Append(' ').Append(ModelCounts[i]);
            }
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
            "class", "side", "level", "road_dist", "toward_village", "neighbors", "front_deg", "half_len", "half_wid", "face", "model" };
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
                        f.HasModelColumn = col.ContainsKey("model");
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
                f.ModelCounts[rec.Model]++;
                if (rec.ModelUnknown) f.ModelUnknown++;
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
            r.Model = ParseModel(Get(c, col, "model"), out r.ModelUnknown);   // v4; brak kolumny = village
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
            return InFootprintAt(r, r.X, r.Y, px, py, margin);
        }

        /// <summary>Jak InFootprint, ale srodek obrazka (cx, cy) - po przesunieciu wioski ze stoku na plaskie miejsce (v4).</summary>
        internal static bool InFootprintAt(MapVillageRecord r, float cx, float cy, float px, float py, float margin)
        {
            return InFootprintDeg(r, r.FrontDeg, cx, cy, px, py, margin);
        }

        /// <summary>Jak InFootprintAt, ale obrot obrazka deg (wyglad 3: mlyn / rybacy obroceni frontem do wody).</summary>
        internal static bool InFootprintDeg(MapVillageRecord r, float deg, float cx, float cy, float px, float py, float margin)
        {
            double a = deg * Math.PI / 180.0;
            double ca = Math.Cos(a), sa = Math.Sin(a);
            double dx = px - cx, dy = py - cy;
            double along = dx * ca + dy * sa;          // lokalna X = ulica wzdluz drogi
            double across = -dx * sa + dy * ca;        // lokalna Y = front
            return Math.Abs(along) <= r.HalfLen + margin && Math.Abs(across) <= r.HalfWid + margin;
        }

        /// <summary>Skala obrazka wedlug poziomu (zawsze mniejszy od wsi-matki, projekt 5.3). Poprawka wygladu 08.10: 0.70 / 0.75 / 0.80
        /// (bylo 0.60 / 0.70 / 0.80) - 2136 z 2449 wiosek pliku 4000 ma poziom 1, a przy 0.6 i 1/3 domow zostawaly "grudki"; poziom mowi
        /// teraz glownie, ILE domow stoi (ThinCluster), a obrazek ma zawsze ok. 0.7-0.8 wielkosci widocznych domow matki (x FitToMother).</summary>
        internal static float LevelScale(int level)
        {
            return level >= 3 ? 0.80f : level == 2 ? 0.75f : 0.70f;
        }

        internal static uint Fnv(string s)
        {
            uint h = 2166136261u;
            if (s != null) foreach (char ch in s) { h ^= ch; h *= 16777619u; }
            return h;
        }

        // ---------- v4 (08.10, autotest 03:37): rodzaj obrazka (kolumna "model"), zwarta kepa wsi, kazdy budynek na swoim gruncie ----------
        // Zdjecia autotestu 08.10 03:37: (1) domy na stoku / brzegu (Poppymead przy Sweet Bridge) zapadniete po dach - wysokosc terenu brana
        // w srodku wioski i cala kepa pochylona wedlug jednej normalnej; (2) wioska poziomu 1 = 3 domy luzno, a wies gry (Tumbledown) to zwarta
        // kepa ok. 9 budynkow; (3) Jeff: farmy, mlyny, spichlerze, przy rzekach mlyny - plik v4 ma kolumne "model". Ponizej czyste funkcje
        // (bez typow gry, do proby poza gra): liczby budynkow, przerzedzenie od brzegu kepy, ukladanie elementow, przesuniecie ze stoku, przepisy.
        internal const int ModelVillage = 0, ModelMill = 1, ModelWindmill = 2, ModelFarm = 3, ModelGranary = 4, ModelFishing = 5, ModelCount = 6;
        internal static readonly string[] ModelNames = { "village", "mill", "windmill", "farm", "granary", "fishing" };

        /// <summary>Rodzaj obrazka z kolumny "model": brak / pusty / "-" = village; nieznana wartosc = village (unknown = true, liczona w logu).</summary>
        internal static int ParseModel(string s, out bool unknown)
        {
            unknown = false;
            if (string.IsNullOrEmpty(s) || s.Trim() == "-" || s.Trim().Length == 0) return ModelVillage;
            string v = s.Trim().ToLowerInvariant();
            for (int i = 0; i < ModelNames.Length; i++) if (ModelNames[i] == v) return i;
            unknown = true;
            return ModelVillage;
        }

        internal static string ModelName(int m)
        {
            return m >= 0 && m < ModelCount ? ModelNames[m] : "?";
        }

        internal const int VillageKitMin = 9;   // wies gry Tumbledown (fm_village2): 9 budynkow - kepa "village" ma co najmniej tyle (domy wzoru + detale)

        /// <summary>Ile budynkow kepy "village" stoi przy poziomie (n = budynki wzoru): 3 - wszystkie, 2 - co najmniej 7 (ok. 2/3), 1 - co najmniej 5
        /// (ok. 1/3); nigdy wiecej niz n. Zdjecia 08.10 03:37: poziom 1 = 3 domy to nie wies (bylo minimum 3).</summary>
        internal static int VillageWant(int n, int level)
        {
            if (n <= 0) return 0;
            if (level >= 3) return n;
            int want = level == 2 ? Math.Max(7, (int)Math.Ceiling(n * 2.0 / 3.0)) : Math.Max(5, (int)Math.Ceiling(n / 3.0));
            return Math.Min(n, want);
        }

        /// <summary>Domy przy innych obrazkach (poziom 1/2/3): zagroda 1/2/2, mlyn 1/2/3, wiatrak 1/2/2, spichlerz 2/3/4, rybacy 2/3/4.</summary>
        internal static int HousesFor(int model, int level)
        {
            int lv = level < 1 ? 1 : level > 3 ? 3 : level;
            switch (model)
            {
                case ModelFarm: return lv >= 2 ? 2 : 1;
                case ModelMill: return lv;
                case ModelWindmill: return lv >= 2 ? 2 : 1;
                case ModelGranary: return lv + 1;
                case ModelFishing: return lv + 1;
            }
            return 0;
        }

        /// <summary>Kolejnosc budynkow od SRODKA kepy: kotwica = srodek ciezkosci (lekko przesuniety ziarnem, najwyzej 0.3 sredniego promienia),
        /// potem odleglosc od kotwicy (remis - nizszy numer). Przerzedzenie bierze poczatek listy, wiec ubywa budynkow z BRZEGU kepy.</summary>
        internal static int[] RankCentral(string seed, IList<float> xs, IList<float> ys)
        {
            int n = xs == null || ys == null ? 0 : Math.Min(xs.Count, ys.Count);
            var idx = new int[n];
            if (n == 0) return idx;
            double mx = 0, my = 0;
            for (int i = 0; i < n; i++) { mx += xs[i]; my += ys[i]; }
            mx /= n; my /= n;
            double rms = 0;
            for (int i = 0; i < n; i++) { double dx = xs[i] - mx, dy = ys[i] - my; rms += dx * dx + dy * dy; }
            rms = Math.Sqrt(rms / n);
            uint h = Fnv(seed ?? "");
            double ang = (h % 3600u) / 3600.0 * 2.0 * Math.PI;
            double mag = 0.3 * rms * ((h / 3600u) % 1000u) / 1000.0;
            double ax = mx + Math.Cos(ang) * mag, ay = my + Math.Sin(ang) * mag;
            var d2 = new double[n];
            for (int i = 0; i < n; i++)
            {
                idx[i] = i;
                double dx = xs[i] - ax, dy = ys[i] - ay;
                d2[i] = dx * dx + dy * dy + i * 1e-9;
            }
            Array.Sort(d2, idx);
            return idx;
        }

        /// <summary>Przerzedzenie kepy od brzegu (RankCentral): stoi `want` budynkow najblizszych srodka.</summary>
        internal static bool[] ThinCentral(string seed, IList<float> xs, IList<float> ys, int want)
        {
            int[] order = RankCentral(seed, xs, ys);
            var keep = new bool[order.Length];
            for (int i = 0; i < order.Length && i < want; i++) keep[order[i]] = true;
            return keep;
        }

        /// <summary>Najwiecej detali (studnia, szopa, sterta drewna, stog, woz, worki) w kepie "village" na poziomie: 1 - 2, 2 - 3, 3 - wszystkie.
        /// Recenzja 08.10: detale wypelniaja luki przy SRODKU kepy, wiec samo przerzedzanie od brzegu zostawialo na poziomie 1 glownie detale
        /// (lustro fm_village4: 1 dom + hala + 2 szopy + studnia), a wies gry (Tumbledown) to w wiekszosci domy.</summary>
        internal static int VillageDetailMax(int level)
        {
            return level >= 3 ? int.MaxValue : level == 2 ? 3 : 2;
        }

        /// <summary>Budynek wzoru wsi-matki, ktory jest detalem, a nie domem: studnia (andal_wm_well*), szopa / sterta drewna Polnocy (fm_wm_shed*).</summary>
        internal static bool IsDetailMesh(string mesh)
        {
            return !string.IsNullOrEmpty(mesh) && (mesh.IndexOf("well", StringComparison.Ordinal) >= 0 || mesh.IndexOf("_shed", StringComparison.Ordinal) >= 0);
        }

        /// <summary>Przerzedzenie kepy "village" od brzegu (RankCentral) z limitem detali: od srodka stoi `want` budynkow, w tym najwyzej
        /// maxDetail detali; gdy domow za malo - dopelnienie pominietymi detalami (dalej od srodka). detail == null = jak ThinCentral.</summary>
        internal static bool[] ThinVillage(string seed, IList<float> xs, IList<float> ys, IList<bool> detail, int want, int maxDetail)
        {
            int[] order = RankCentral(seed, xs, ys);
            var keep = new bool[order.Length];
            int kept = 0, det = 0;
            foreach (int i in order)
            {
                if (kept >= want) break;
                bool d = detail != null && i < detail.Count && detail[i];
                if (d && det >= maxDetail) continue;
                keep[i] = true;
                kept++;
                if (d) det++;
            }
            foreach (int i in order)
            {
                if (kept >= want) break;
                if (keep[i]) continue;
                keep[i] = true;
                kept++;
            }
            return keep;
        }

        // ---------- ukladanie elementow obrazka (jednostki wzoru = uklad wsi-matki; front +Y do drogi / wody, X wzdluz ulicy) ----------
        internal const int SlotPreset = 0, SlotFill = 1, SlotFront = 2, SlotBack = 3, SlotRight = 4, SlotLeft = 5,
                           SlotFrontRight = 6, SlotFrontLeft = 7, SlotBackRight = 8, SlotBackLeft = 9, SlotCenter = 10;
        internal const float LayoutGap = 0.92f;   // kola obrysow moga zachodzic o 8% (obrys prostokata jest mniejszy niz kolo)

        /// <summary>Element ukladu: kolo obrysu (R = pol wiekszego boku BB) i miejsce. SlotPreset = polozenie z wzoru (domy wsi-matki);
        /// Core = czesc kepy domow (od jej obrysu licza sie miejsca Front / Back / Right / Left ...).</summary>
        internal sealed class LayoutItem
        {
            public int Slot;
            public float R;
            public float X, Y;
            public bool Core;
        }

        internal static bool FreeAt(IList<float> xs, IList<float> ys, IList<float> rs, float px, float py, float r)
        {
            for (int i = 0; i < xs.Count; i++)
            {
                float dx = xs[i] - px, dy = ys[i] - py, need = (rs[i] + r) * LayoutGap;
                if (dx * dx + dy * dy < need * need) return false;
            }
            return true;
        }

        /// <summary>Najblizsze wolne miejsce przy (cx, cy): pierscienie co ringStep, na kazdym perRing katow (przesuniete ziarnem). false = brak.</summary>
        internal static bool FreeSpot(IList<float> xs, IList<float> ys, IList<float> rs, float cx, float cy, float r, float ringStep, int rings, int perRing,
            uint seed, out float ox, out float oy)
        {
            ox = cx; oy = cy;
            if (FreeAt(xs, ys, rs, cx, cy, r)) return true;
            double off = (seed % 1000u) / 1000.0;
            for (int k = 1; k <= rings; k++)
            {
                double rad = k * ringStep;
                for (int j = 0; j < perRing; j++)
                {
                    double a = 2.0 * Math.PI * (j + off) / perRing;
                    float px = cx + (float)(Math.Cos(a) * rad), py = cy + (float)(Math.Sin(a) * rad);
                    if (FreeAt(xs, ys, rs, px, py, r)) { ox = px; oy = py; return true; }
                }
            }
            return false;
        }

        /// <summary>Uklada elementy po kolei: najpierw SlotPreset (domy z wzoru, bez przesuwania), potem reszta - punkt startu wedlug miejsca
        /// wzgledem obrysu kepy (Core) i przesuwanie w kierunku miejsca az bez kolizji; SlotFill / SlotCenter = najblizsze wolne miejsce przy
        /// srodku kepy (wypelnianie luk - kepa zwarta jak wies gry).</summary>
        internal static void Layout(IList<LayoutItem> items, uint seed)
        {
            var xs = new List<float>();
            var ys = new List<float>();
            var rs = new List<float>();
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
            bool any = false;
            Action<LayoutItem> grow = it =>
            {
                if (!it.Core) return;
                if (!any) { minX = it.X - it.R; maxX = it.X + it.R; minY = it.Y - it.R; maxY = it.Y + it.R; any = true; return; }
                minX = Math.Min(minX, it.X - it.R); maxX = Math.Max(maxX, it.X + it.R);
                minY = Math.Min(minY, it.Y - it.R); maxY = Math.Max(maxY, it.Y + it.R);
            };
            foreach (var it in items)
            {
                if (it.Slot != SlotPreset) continue;
                xs.Add(it.X); ys.Add(it.Y); rs.Add(it.R);
                grow(it);
            }
            uint k = 0;
            foreach (var it in items)
            {
                if (it.Slot == SlotPreset) continue;
                k++;
                float r = Math.Max(0.02f, it.R);
                float cx = any ? (minX + maxX) * 0.5f : 0f, cy = any ? (minY + maxY) * 0.5f : 0f;
                float hx = any ? (maxX - minX) * 0.5f : 0f, hy = any ? (maxY - minY) * 0.5f : 0f;
                float px, py, dx, dy;
                switch (it.Slot)
                {
                    case SlotFront: px = cx; py = cy + hy + r * 0.9f; dx = 0f; dy = 1f; break;
                    case SlotBack: px = cx; py = cy - hy - r * 0.9f; dx = 0f; dy = -1f; break;
                    case SlotRight: px = cx + hx + r * 0.9f; py = cy; dx = 1f; dy = 0f; break;
                    case SlotLeft: px = cx - hx - r * 0.9f; py = cy; dx = -1f; dy = 0f; break;
                    case SlotFrontRight: px = cx + hx * 0.6f + r * 0.5f; py = cy + hy + r * 0.7f; dx = 0.8f; dy = 0.6f; break;
                    case SlotFrontLeft: px = cx - hx * 0.6f - r * 0.5f; py = cy + hy + r * 0.7f; dx = -0.8f; dy = 0.6f; break;
                    case SlotBackRight: px = cx + hx * 0.6f + r * 0.5f; py = cy - hy - r * 0.7f; dx = 0.8f; dy = -0.6f; break;
                    case SlotBackLeft: px = cx - hx * 0.6f - r * 0.5f; py = cy - hy - r * 0.7f; dx = -0.8f; dy = -0.6f; break;
                    default:
                    {
                        // SlotFill / SlotCenter: najblizsze wolne miejsce przy srodku ciezkosci tego, co juz stoi
                        float gx = 0f, gy = 0f;
                        if (xs.Count > 0) { for (int i = 0; i < xs.Count; i++) { gx += xs[i]; gy += ys[i]; } gx /= xs.Count; gy /= xs.Count; }
                        float fx, fy;
                        if (!FreeSpot(xs, ys, rs, gx, gy, r, Math.Max(0.02f, r * 0.3f), 60, 12, Fnv(seed.ToString(CultureInfo.InvariantCulture) + ":" + k), out fx, out fy))
                        { fx = gx + (hx + r) * 1.5f; fy = gy; }
                        it.X = fx; it.Y = fy;
                        xs.Add(it.X); ys.Add(it.Y); rs.Add(r);
                        grow(it);
                        continue;
                    }
                }
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                dx /= len; dy /= len;
                float step = Math.Max(0.02f, r * 0.15f);
                for (int s = 0; s < 120 && !FreeAt(xs, ys, rs, px, py, r); s++) { px += dx * step; py += dy * step; }
                it.X = px; it.Y = py;
                xs.Add(it.X); ys.Add(it.Y); rs.Add(r);
                grow(it);
            }
        }

        // ---------- teren (poprawka 1): przesuniecie ze stoku / brzegu w granicach obrysu z pliku ----------
        /// <summary>Najwiekszy dopuszczalny rozrzut wysokosci gruntu pod budynkami wioski (jedn. mapy): 0.9 wysokosci domu na mapie, 0.25..0.8.</summary>
        internal static float SpreadMax(float houseHeightWorld)
        {
            float v = 0.9f * houseHeightWorld;
            return v < 0.25f ? 0.25f : v > 0.8f ? 0.8f : v;
        }

        internal static float Spread(IList<float> h)
        {
            if (h == null || h.Count == 0) return 0f;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (float v in h) { if (v < lo) lo = v; if (v > hi) hi = v; }
            return hi - lo;
        }

        /// <summary>Przesuniecia srodka wioski (jedn. mapy, uklad obrazka: X wzdluz ulicy, Y do frontu) w granicach obrysu z pliku (half_len /
        /// half_wid), od najmniejszego. Obrazek przy wodzie (mlyn, rybacy) nie odchodzi od wody (+Y) ani nie wchodzi w nia - tylko wzdluz brzegu
        /// i lekko w tyl.</summary>
        internal static List<float[]> ShiftCandidates(float halfLen, float halfWid, bool waterSide)
        {
            var l = new List<float[]>();
            float[] fx = { 0f, 0.35f, -0.35f, 0.7f, -0.7f, 1f, -1f };
            float[] fy = waterSide ? new[] { 0f, -0.5f } : new[] { 0f, -0.5f, 0.5f, -1f, 1f };
            foreach (float a in fx)
                foreach (float b in fy)
                {
                    if (a == 0f && b == 0f) continue;
                    l.Add(new[] { a * halfLen, b * halfWid });
                }
            l.Sort((p, q) => (p[0] * p[0] + p[1] * p[1]).CompareTo(q[0] * q[0] + q[1] * q[1]));
            return l;
        }

        /// <summary>Budynki do zdjecia, gdy po przesunieciu i scisnieciu grunt pod kepa dalej jest zbyt nierowny: kolejno ten, ktorego grunt
        /// najdalej od mediany, az rozrzut &lt;= max albo zostanie minKeep stojacych; budynki niezbedne (mlyn, stodola...) nigdy.</summary>
        internal static bool[] DropOutliers(IList<float> h, IList<bool> essential, int minKeep, float max)
        {
            int n = h == null ? 0 : h.Count;
            var keep = new bool[n];
            for (int i = 0; i < n; i++) keep[i] = true;
            int alive = n;
            while (alive > minKeep)
            {
                var cur = new List<float>();
                for (int i = 0; i < n; i++) if (keep[i]) cur.Add(h[i]);
                if (Spread(cur) <= max) break;
                cur.Sort();
                float med = cur[cur.Count / 2];
                int worst = -1;
                float wd = -1f;
                for (int i = 0; i < n; i++)
                {
                    if (!keep[i] || (essential != null && i < essential.Count && essential[i])) continue;
                    float d = Math.Abs(h[i] - med);
                    if (d > wd) { wd = d; worst = i; }
                }
                if (worst < 0) break;
                keep[worst] = false;
                alive--;
            }
            return keep;
        }

        // ---------- WYGLAD 3 (zdjecia autotestu 08.10 04:42): lad pod budynkami, brzeg wody dla mlyna / rybakow ----------
        // Zdjecia: mlyn kilka domow od rzeki bez widocznego kola (Sweet Bridge), osada rybacka na skalach w wodzie (Widow's Horn). Proba poza
        // gra na danych mapy ROT (proba-0810\proba_v5_dane.py): srodek Widow's Horn lezy na scianie siatki typu Plain, ale teren ma tam 5.21,
        // pod poziomem morza 5.50 (9 wiosek pliku v4 ma tak srodek, 38 czesc obrysu); rzeki to pasy scian Mountain (472 z 520 punktow osi
        // rzek), woda rzeki stoi ok. 0.22 nad dnem koryta; brzeg wody od srodka wioski-mlyna: mediana 2.85 jedn. (dawny profil szukal 2.5 jedn.
        // tylko przed frontem i przesuwal mlyn najwyzej o 1.6). Lad = sciana siatki ladowej I teren nad poziomem morza.
        internal const float SeaLevelRot = 5.5f;        // ROT 8.1.8 Main_map: water_properties water_level 5.500 (scene.xscene); tylko przy zgodnym CRC sceny
        internal const float NoSea = -1e6f;             // poziom morza nieznany (plik bez CRC sceny) - lad tylko wedlug siatki
        internal const float GroundSink = 0.03f;        // dol BB budynku tyle pod najnizszym gruntem pod obrysem (jedn. mapy)
        internal const float RiverAboveBed = 0.22f;     // woda rzeki nad dnem koryta (mediana 520 punktow sciezek 56 rzek ROT; 10% 0.07, 90% 0.39)
        internal const float ShoreMaxR = 5.0f;          // brzeg wody szukany do 5 jedn. od srodka (pierwsza sciana bez ladu; proba-0810\ProbaV5 na mapie ROT)
        internal const float ShoreStep = 0.25f;
        internal const float ShoreNorthPenalty = 0.6f;  // kamera mapy patrzy na polnoc: woda na N (kolo / pomost za budynkiem) liczy sie jak 0.6 jedn. dalej
        internal const float ShoreMaxShift = 3.5f;      // obrazek przesuwany ku wodzie najwyzej o tyle (jedn. mapy)
        internal const float ShoreMaxBack = 1.0f;       // ... i od wody najwyzej o tyle (woda blizej niz przod kepy)
        internal const int ShoreDirs = 16;
        internal const float LandSearchR = 6.0f;        // srodek obrazka poza ladem: najblizsze miejsce z obrysem na ladzie do 6 jedn. (Muqazmion: lad 4.6 jedn.)
        internal const float NeighborMinDist = 3.5f;    // recenzja: srodek przeniesiony (na lad / ku wodzie / ze stoku) nie blizej innej wioski (plik: >= 4.5)

        /// <summary>Typ sciany siatki nawigacyjnej (PathFaceRecord.FaceGroupIndex = TerrainType), na ktorym moze stac budynek: Plain 1, Desert 2,
        /// Snow 3, Forest 4, Steppe 5, RuralArea 14, Swamp 15, Dune 16, Beach 20. Nie: Fording 6, Mountain 7 (w ROT pasy rzek, urwiska i plot
        /// zeglugi), Lake 8, Water 10, River 11, Canyon 13, Bridge 17, CoastalSea 18, OpenSea 19, Cliff 21, NonNavigableRiver 22, 23-25; -1 = brak sciany.</summary>
        internal static bool IsLandGroup(int g)
        {
            return g == 1 || g == 2 || g == 3 || g == 4 || g == 5 || g == 14 || g == 15 || g == 16 || g == 20;
        }

        /// <summary>Lad pod punktem: sciana ladowa i teren nad poziomem morza (seaLevel = NoSea: tylko sciana).</summary>
        internal static bool LandAt(int group, float h, float seaLevel)
        {
            return IsLandGroup(group) && !(seaLevel > NoSea * 0.5f && h <= seaLevel + 0.02f);
        }

        /// <summary>Zaglebienie dolu BB pod najnizszy grunt: GroundSink, ale najwyzej 10% wysokosci budynku (worek, stog nie znika w ziemi).</summary>
        internal static float Sink(float heightWorld)
        {
            return Math.Max(0f, Math.Min(GroundSink, 0.1f * heightWorld));
        }

        /// <summary>Brzeg wody na promieniu: kierunek (Dx, Dy), odleglosc od startu do wody (Edge) i do ostatniego ladu (LastLand), poziom wody
        /// (WaterZ), grunt na ostatnim ladzie (Top), morze / rzeka-jezioro, ocena (Edge + kara za wode na polnoc), numer promienia.</summary>
        internal sealed class ShoreHit
        {
            public float Dx, Dy, Edge, LastLand, WaterZ, Top, Score;
            public bool Sea;
            public int Dir = -1;
        }

        /// <summary>Promien od (x, y) w kierunku (dx, dy) co step do maxR: pierwszy punkt bez ladu (LandAt), granica doprecyzowana polowieniem.
        /// Teren pod poziomem morza = brzeg morza tutaj (woda na poziomie morza). Sciana bez ladu nad poziomem morza (pas rzeki Mountain, jezioro):
        /// dno = najnizszy teren w nastepnych 3.5 jedn. (do ladu po drugiej stronie; pasy Mountain bywaja szersze niz koryto - Andarel: koryto
        /// 2.2 jedn. za poczatkiem pasa); spadek od brzegu ponizej 0.15 = urwisko, nie woda (null);
        /// poziom wody = dno + 0.22 (najwyzej 0.12 pod brzegiem, co najmniej 0.05 nad dnem), brzeg wody = pierwszy punkt pasa z terenem nie
        /// wyzej niz woda. null = start nie na ladzie albo brak wody na promieniu.</summary>
        internal static ShoreHit ShoreRay(Func<float, float, int> group, Func<float, float, float> height, float sea, float x, float y, float dx, float dy,
            float maxR, float step)
        {
            if (group == null || height == null || !(step > 0.01f)) return null;
            bool hasSea = sea > NoSea * 0.5f;
            float top = 0f, lastLand = 0f;
            int steps = (int)Math.Floor(maxR / step + 1e-4f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i * step;
                float px = x + dx * t, py = y + dy * t, h = height(px, py);
                if (LandAt(group(px, py), h, sea)) { lastLand = t; top = h; continue; }
                if (i == 0) return null;   // start nie na ladzie (srodek obrazka przenosi wczesniej FindLandSpot)
                float a = lastLand, b = t;
                for (int it = 0; it < 4; it++)
                {
                    float m = (a + b) * 0.5f, mx = x + dx * m, my = y + dy * m;
                    if (LandAt(group(mx, my), height(mx, my), sea)) a = m; else b = m;
                }
                float hb = height(x + dx * b, y + dy * b);
                if (hasSea && hb <= sea + 0.02f)
                    return new ShoreHit { Dx = dx, Dy = dy, Edge = b, LastLand = a, WaterZ = sea, Top = top, Sea = true };
                // pas bez ladu nad poziomem morza (rzeka, jezioro): dno i brzeg wody
                float bed = hb;
                int bedAt = 0;
                bool across = false;
                var qs = new List<float>();
                var hs = new List<float>();
                for (int j = 0; j <= 35; j++)
                {
                    float q = b + j * 0.1f, qx = x + dx * q, qy = y + dy * q, hq = height(qx, qy);
                    if (hasSea && hq <= sea + 0.02f)   // ujscie rzeki / zatoka: morze w pasie
                        return new ShoreHit { Dx = dx, Dy = dy, Edge = q, LastLand = a, WaterZ = sea, Top = top, Sea = true };
                    if (j >= 3 && LandAt(group(qx, qy), hq, sea)) { across = true; break; }   // druga strona pasa
                    qs.Add(q);
                    hs.Add(hq);
                    if (hq < bed) { bed = hq; bedAt = qs.Count - 1; }
                }
                if (top - bed < 0.15f) return null;   // urwisko / gora - nie woda
                if (!across && bedAt >= qs.Count - 2) return null;   // teren tylko opada (stok za krawedzia), bez koryta - nie woda
                float wl = Math.Max(bed + 0.05f, Math.Min(bed + RiverAboveBed, top - 0.12f));
                for (int j = 0; j < qs.Count; j++)
                    if (hs[j] <= wl) return new ShoreHit { Dx = dx, Dy = dy, Edge = qs[j], LastLand = a, WaterZ = wl, Top = top, Sea = false };
                return null;
            }
            return null;
        }

        /// <summary>Najblizszy brzeg wody wokol (cx, cy): `dirs` promieni (pierwszy na front z pliku - generator v4 kieruje tam wode - potem na
        /// przemian w lewo / w prawo). Ocena = odleglosc do wody + northPenalty x skladowa polnocna kierunku (kamera mapy patrzy na polnoc: kolo
        /// mlyna / pomost od strony kamery wygrywa przy podobnej odleglosci); promien konczy sie, gdy nie moze juz pobic najlepszego. null = brak.</summary>
        internal static ShoreHit FindShore(Func<float, float, int> group, Func<float, float, float> height, float sea, float cx, float cy, float frontDeg,
            int dirs, float maxR, float step, float northPenalty)
        {
            ShoreHit best = null;
            if (dirs < 1) dirs = 1;
            double a0 = (frontDeg + 90.0) * Math.PI / 180.0, da = 2.0 * Math.PI / dirs;   // front (+Y lokalne) = swiat (-sin f, cos f)
            for (int k = 0; k < dirs; k++)
            {
                int s = (k + 1) / 2;
                double a = a0 + (k % 2 == 1 ? s : -s) * da;
                float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                float pen = northPenalty * Math.Max(0f, dy);
                float lim = best == null ? maxR : Math.Min(maxR, best.Score - pen);
                if (lim <= 0f) continue;
                ShoreHit h = ShoreRay(group, height, sea, cx, cy, dx, dy, lim, step);
                if (h == null) continue;
                h.Score = h.Edge + pen;
                h.Dir = k;
                if (best == null || h.Score < best.Score) best = h;
            }
            return best;
        }

        /// <summary>Woda wzdluz linii od (x, y) w kierunku (dx, dy): odleglosc do pierwszego punktu z terenem nie wyzej niz poziom wody wl (co step
        /// do maxR); -1 = brak. Zapas dla elementu przy wodzie, gdy punkt 1.2 jedn. za nim nie stoi na ladzie (szeroki pas Mountain przy rzece:
        /// srodek obrazka nie wchodzi w pas, wiec kolo stawalo nad sucha skarpa - Andarel).</summary>
        internal static float WaterAlong(Func<float, float, float> height, float wl, float x, float y, float dx, float dy, float maxR, float step)
        {
            if (height == null || float.IsNaN(wl) || !(step > 0.01f)) return -1f;
            int steps = (int)Math.Floor(maxR / step + 1e-4f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i * step;
                if (height(x + dx * t, y + dy * t) <= wl) return t;
            }
            return -1f;
        }

        /// <summary>Obrot korzenia obrazka (rad), przy ktorym front (+Y lokalne = swiat (-sin, cos)) patrzy w kierunku (dx, dy).</summary>
        internal static float ShoreYaw(float dx, float dy)
        {
            return (float)Math.Atan2(-dx, dy);
        }

        /// <summary>Obrys z pliku (srodek + 8 punktow prostokata half_len x half_wid x k, obrot frontDeg) caly na ladzie.</summary>
        internal static bool FootprintLand(Func<float, float, bool> land, float cx, float cy, float frontDeg, float halfLen, float halfWid, float k)
        {
            if (land == null || !land(cx, cy)) return false;
            double a = frontDeg * Math.PI / 180.0;
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    float u = i * halfLen * k, v = j * halfWid * k;
                    if (!land(cx + u * ca - v * sa, cy + u * sa + v * ca)) return false;
                }
            return true;
        }

        /// <summary>Najblizsze miejsce, gdzie obrys z pliku (x 0.8) jest caly na ladzie: pierscienie co 0.35 jedn. do maxR, 24 katy (od frontu,
        /// potem na przemian). false = brak (obrazek zostaje, kazdy budynek sprawdza PushToLand).</summary>
        internal static bool FindLandSpot(Func<float, float, bool> land, float cx, float cy, float frontDeg, float halfLen, float halfWid, float maxR,
            out float nx, out float ny)
        {
            return FindLandSpotWhere(land, null, cx, cy, frontDeg, halfLen, halfWid, maxR, out nx, out ny);
        }

        /// <summary>Jak FindLandSpot, ale miejsce musi tez spelniac ok (null = bez warunku). Recenzja wygladu 3: ok = odstep od innych wiosek
        /// (NeighborMinDist) - przeniesienie na lad do 6 jedn. stawialo wioske przy sasiedzie (Talosa 2.6 od Stonecove, Widow's Horn 3.2 od
        /// Misthaven; plik trzyma odstep co najmniej 4.5). Najpierw ok (bez wywolan silnika), potem obrys na ladzie.</summary>
        internal static bool FindLandSpotWhere(Func<float, float, bool> land, Func<float, float, bool> ok, float cx, float cy, float frontDeg, float halfLen,
            float halfWid, float maxR, out float nx, out float ny)
        {
            nx = cx; ny = cy;
            double a0 = (frontDeg + 90.0) * Math.PI / 180.0;
            int rings = (int)Math.Floor(maxR / 0.35f + 1e-4f);
            for (int ring = 1; ring <= rings; ring++)
            {
                float r = ring * 0.35f;
                for (int k = 0; k < 24; k++)
                {
                    int s = (k + 1) / 2;
                    double a = a0 + (k % 2 == 1 ? s : -s) * Math.PI / 12.0;
                    float px = cx + (float)Math.Cos(a) * r, py = cy + (float)Math.Sin(a) * r;
                    if (ok != null && !ok(px, py)) continue;
                    if (FootprintLand(land, px, py, frontDeg, halfLen, halfWid, 0.8f)) { nx = px; ny = py; return true; }
                }
            }
            return false;
        }

        /// <summary>Budynek (srodek BB cx, cy; polboki rx, ry) caly na ladzie: srodek i 4 rogi (x 0.85).</summary>
        internal static bool BoxLand(Func<float, float, bool> land, float cx, float cy, float rx, float ry)
        {
            if (land == null || !land(cx, cy)) return false;
            float ux = rx * 0.85f, uy = ry * 0.85f;
            return land(cx - ux, cy - uy) && land(cx + ux, cy - uy) && land(cx - ux, cy + uy) && land(cx + ux, cy + uy);
        }

        /// <summary>Nowe miejsce budynku na ladzie (uklad wzoru): najpierw w strone (tx, ty) - srodek kepy / od wody - krokami pol promienia az za
        /// cel, potem pierscienie 8 kierunkow (promien x 1, 2, 3); kazde miejsce: BoxLand i bez kolizji z kolami obrysow innych (FreeAt).
        /// false = brak miejsca (budynek zdjety albo zostaje, gdy niezbedny).</summary>
        internal static bool PushToLand(Func<float, float, bool> land, float cx, float cy, float rx, float ry, float tx, float ty,
            IList<float> ox, IList<float> oy, IList<float> orr, out float nx, out float ny)
        {
            nx = cx; ny = cy;
            float r = Math.Max(0.02f, Math.Max(rx, ry));
            float vx = tx - cx, vy = ty - cy, len = (float)Math.Sqrt(vx * vx + vy * vy);
            if (len > 1e-3f)
            {
                vx /= len; vy /= len;
                float st = 0.5f * r;
                int n = Math.Min(24, (int)Math.Ceiling((len + 2f * r) / st));
                for (int i = 1; i <= n; i++)
                {
                    float px = cx + vx * st * i, py = cy + vy * st * i;
                    if (BoxLand(land, px, py, rx, ry) && FreeAt(ox, oy, orr, px, py, r)) { nx = px; ny = py; return true; }
                }
            }
            for (int ring = 1; ring <= 3; ring++)
                for (int k = 0; k < 8; k++)
                {
                    double a = k * Math.PI / 4.0;
                    float px = cx + (float)Math.Cos(a) * r * ring, py = cy + (float)Math.Sin(a) * r * ring;
                    if (BoxLand(land, px, py, rx, ry) && FreeAt(ox, oy, orr, px, py, r)) { nx = px; ny = py; return true; }
                }
            return false;
        }

        /// <summary>Wysokosc origin mlyna wodnego (os kola = podloga, jak w prefabie battania_watermill: kolo w z = 0): kolo dotyka wody (os 0.8
        /// promienia nad woda), ale podloga nie nizej niz grunt od strony ladu - 0.45 wysokosci mlyna nad podloga (nie zakopany w brzegu) i nie
        /// wyzej niz ten grunt. Bez wody (waterZ NaN): podloga na gruncie od strony ladu. Mlyn ma pod podloga fundament 0.42 swojej wysokosci
        /// (BB z od -4.62 do 6.44) - wysokosc z dolu BB postawilaby kolo nad brzegiem, nie w wodzie.</summary>
        internal static float MillFloor(float landBack, float waterZ, float wheelR, float aboveFloor)
        {
            if (float.IsNaN(waterZ)) return landBack;
            float f = Math.Min(landBack, waterZ + 0.8f * wheelR);
            return Math.Max(f, landBack - 0.45f * Math.Max(0f, aboveFloor));
        }

        // ---------- przepisy obrazkow (poprawka 3): siatki MAPOWE gry / ROT, modele scenowe tylko jako kopia siatki ze skala z BB ----------
        internal const int RoleHouse = 0, RoleDetail = 1, RoleSpecial = 2, RoleWater = 3, RoleAttached = 4;
        internal const int SizeFitH = 0, SizeFitHeight = 1, SizeNatural = 2, SizeUnit = 3;
        internal const int GroundMin = 0, GroundLand = 1, GroundEnd = 2, GroundBeach = 3, GroundTilt = 4, GroundAttached = 5;
        internal const int TurnNone = 0, TurnLongX = 1, TurnLongY = 2, TurnPi = 3, TurnSeed = 4, TurnCamera = 5;
        internal const string SrcMap = "mapa gry (Native Prefabs\\map_icon_parts.xml)";
        internal const string SrcRot = "mapa ROT (ROT-Map, scena Main_map)";
        internal const string SrcScene = "model scenowy gry (Native Prefabs\\archhitecture_*.xml) - sama siatka, skala z BB";
        internal const string SrcHouse = "dom wsi okregu (kopia prefabu domow ROT)";
        internal const float IconHouseH = 0.55f;   // wielkosc domu (jedn. wzoru) dla wsi z ikona Calradii / Essos (dom andal / fm: ok. 0.5)
        internal const string Watermill = "battania_watermill", WatermillWheel = "battania_watermill_mill";
        // Wyglad 3 (zdjecie windmill-applewick 04:42: sama kamienna wieza): battania_windmill to w grze JEDNA siatka bez dzieci (archhitecture_
        // battania.xml, paczka archhitecture_battania.tpac: tylko battania_windmill.0-7 - wieza bez skrzydel, z ruin zamku). Skrzydla ma tylko
        // wiatrak sturgia_windmill_a: osobny prefab smigla sturgia_windmill_fan_a_open, w 15 scenach gry stawiany wzgledem wiatraka w (0.01,
        // -2.43, 10.64) (mediana; m.in. sturgia_village_a / e, sturgia_town_c, ROT_twins, arena_sturgia_a, mp_skirmish_map_009 / 014,
        // ROT_kings_landing_field_battle; proba-0810\proba_v5_dane.py), obrot jak wiatrak + obrot wokol osi smigla; schody
        // sturgia_windmill_a_stair w prefabie wiatraka.
        internal const string Windmill = "sturgia_windmill_a", WindmillTower = "battania_windmill", WindmillFan = "sturgia_windmill_fan_a_open",
                              WindmillStair = "sturgia_windmill_a_stair";

        /// <summary>Siatka doczepiona do budynku (kolo mlyna, skrzydla i schody wiatraka) jako dziecko jego encji: ramka w jednostkach siatki
        /// rodzica, jak dziecko w prefabie gry (obrot wokol pionu, potem wokol lokalnej osi Y = os kola / smigla). Tylko gdy budynek ma siatke
        /// ForMesh. Sama kopia siatki - bez skryptu WindMill, fizyki, czastek i dzwieku (AmbientSoundEmitter smigla).</summary>
        internal sealed class AttachSpec
        {
            public string ForMesh = "", Mesh = "", Why = "";
            public float X, Y, Z, RotUp, RotFwd;
        }

        internal static readonly AttachSpec[] WatermillParts =
        {
            new AttachSpec { ForMesh = Watermill, Mesh = WatermillWheel, X = 0.358f, Y = -5.333f, Z = 0f, RotFwd = -1.591f,
                             Why = "prefab battania_watermill: (0.358, -5.333, 0), rotation_euler (0, -1.591, 0)" }
        };

        internal static readonly AttachSpec[] WindmillParts =
        {
            new AttachSpec { ForMesh = Windmill, Mesh = WindmillFan, X = 0.01f, Y = -2.43f, Z = 10.64f, RotFwd = 0.6f,
                             Why = "mediana 15 scen gry: smiglo wzgledem wiatraka (0.01, -2.43, 10.64), obrot wiatraka + 0.6 rad wokol osi smigla" },
            new AttachSpec { ForMesh = Windmill, Mesh = WindmillStair, X = 0.065f, Y = 7.610f, Z = 0f, RotUp = -0.021f,
                             Why = "prefab sturgia_windmill_a: (0.065, 7.610, 0), rotation_euler (0, 0, -0.021)" }
        };

        /// <summary>Jeden element przepisu. Size = wielkosc wzgledem domu wsi okregu (H): SizeFitH - wiekszy bok BB, SizeFitHeight - wysokosc BB;
        /// SizeNatural - skala Natural z mapy gry (mediana sceny ROT), poprawiana do Size x H tylko gdy wychodzi poza 0.4..2.5 tego;
        /// SizeUnit - skala domow wzoru x Natural (siatki rodziny domow fm / andal).</summary>
        internal sealed class PieceSpec
        {
            public int Role;
            public string[] Meshes = new string[0];   // warianty (wybor z ziarna okregu, pierwszy istniejacy; Ordered - pierwszy istniejacy po kolei)
            public bool Ordered;
            public float Size = 1f;
            public int SizeMode;
            public float Natural = 1f;
            public int Slot;
            public int MinLevel = 1;
            public int Ground;
            public int Turn;
            public string Source = "";
            public bool Essential;                    // bez niego obrazek nie ma sensu (mlyn, pomost, stodola) - nigdy zdejmowany ze stoku
            public bool WaterSide;                    // stoi przy wodzie (+Y) - przesuwany do brzegu
            public bool BarnLike;                     // stodola: brak siatki -> najwieksza szopa / dom stylu x 1.3
            public bool Pivot;                        // wysokosc wedlug punktu zaczepienia siatki (mlyn: podloga = os kola, MillFloor)
            public AttachSpec[] Attach;               // siatki doczepione (kolo mlyna, skrzydla wiatraka)
        }

        private static PieceSpec P(int role, string[] meshes, float size, int mode, float natural, int slot, int minLevel, int ground, int turn, string src)
        {
            return new PieceSpec { Role = role, Meshes = meshes ?? new string[0], Size = size, SizeMode = mode, Natural = natural, Slot = slot,
                                   MinLevel = minLevel, Ground = ground, Turn = turn, Source = src };
        }

        /// <summary>Styl krainy: domy i budynki gospodarcze wedlug rodziny domow wsi-matki (andal = Westeros poludniowy, fm = Polnoc) albo
        /// kultury ikony wsi (Calradia / Essos).</summary>
        internal sealed class StyleKit
        {
            public string Name = "";
            public string[] Houses = new string[0];    // domy kultury (wsie z ikona): mapowe mi_*
            public string Well;                        // studnia
            public string[] Barn = new string[0];      // spichlerz / stodola dziesiecinna
            public string[] FarmBarn = new string[0];  // stodola zagrody
            public string[] Sheds = new string[0];     // fm: [0] oborka / szopa, [1] sterta drewna pod daszkiem (skala domow wzoru)
            public string Hall;                        // fm: najwiekszy budynek gospodarczy (zapas stodoly)
            public string[] Pier = new string[0];
            public string[] Boats = new string[0];
            public string[] Fill = new string[0];      // detale do kepy wsi (do VillageKitMin budynkow)
            public bool FillUnit;                      // detale Fill w skali domow wzoru (fm), inaczej mapowe naturalne
        }

        internal static readonly string[] StyleNames = { "andal", "fm", "empire", "aserai", "khuzait", "sturgia", "vlandia", "battania" };
        private static readonly string[] Pens = { "mi_cattle_farm_a", "mi_cattle_farm_b", "mi_cattle_farm_c", "mi_cattle_farm_d" };
        private static readonly string[] Fishers = { "mi_fisherman_1", "mi_fisherman_2", "mi_fisherman_3" };

        /// <summary>Skala naturalna siatek mapowych (mediana iloczynu skal od korzenia osady w scenie ROT 8.1.8 Main_map; dzien-6\wioski-2200\proba-0810).</summary>
        internal static float NaturalScale(string mesh)
        {
            switch (mesh)
            {
                case "mi_straw_pile": return 1.44f;
                case "mi_sack_a": return 1.0f;
                case "mi_sack_b": return 1.11f;
                case "mi_sack_c": return 1.40f;
                case "mi_barrels_a": return 1.07f;
                case "mi_cart_a": return 0.83f;
                case "mi_cart_b_full": return 0.785f;
                case "mi_pier_a": case "rot_dock1": case "mi_docks_b": case "mi_asera_docks_c": return 1.0f;
                case "rot_boat1": case "rot_boat3": return 0.30f;
                case "mi_fisherman_1": case "mi_fisherman_2": case "mi_fisherman_3": return 0.76f;
                case "mi_emp_well": return 0.32f;
                case "mi_bat_well": return 0.76f;
            }
            if (mesh != null && mesh.StartsWith("mi_cattle_farm_", StringComparison.Ordinal)) return 1.0f;
            return 1.0f;
        }

        internal static StyleKit StyleFor(string style, bool snow)
        {
            var s = new StyleKit { Name = style ?? "empire" };
            string[] westPier = { "rot_dock1", "mi_pier_a" };
            string[] boats = { "rot_boat1", "rot_boat3" };
            switch (s.Name)
            {
                case "andal":
                    s.Houses = new[] { "mi_vla_house_b", "mi_vla_house_a", "mi_vla_house_c" };
                    s.Well = "andal_wm_well";
                    s.Barn = new[] { "european_village_barn_a" };
                    s.FarmBarn = new[] { "european_village_barn_b", "european_village_barn_a" };
                    s.Pier = westPier; s.Boats = boats;
                    s.Fill = new[] { "mi_straw_pile", "mi_cart_a", "mi_sack_a" };
                    break;
                case "fm":
                    s.Name = snow ? "fm_snow" : "fm";
                    s.Houses = new[] { "mi_stu_house_a", "mi_stu_house_b" };
                    s.Well = "andal_wm_well2";
                    s.Barn = new[] { "sturgia_village_barn_a" };
                    s.FarmBarn = new[] { "sturgia_village_barn_b", "sturgia_village_barn_a" };
                    s.Sheds = snow ? new[] { "fm_wm_shed_snow", "fm_wm_shed2_snow" } : new[] { "fm_wm_shed", "fm_wm_shed2" };
                    s.Hall = "fm_wm_hall_snow";
                    s.Pier = westPier; s.Boats = boats;
                    s.Fill = snow ? new[] { "fm_wm_shed2_snow", "fm_wm_shed_snow", "andal_wm_well2" } : new[] { "fm_wm_shed2", "fm_wm_shed", "andal_wm_well2" };
                    s.FillUnit = true;
                    break;
                case "aserai":
                    s.Houses = new[] { "mi_aserai_city_house_a", "mi_aserai_city_house_b", "mi_aserai_city_house_c" };
                    s.Well = "mi_emp_well";
                    s.Barn = new[] { "aserai_village_barn_a" };
                    s.FarmBarn = new[] { "aserai_village_barn_a" };
                    s.Pier = new[] { "mi_asera_docks_c", "mi_pier_a" }; s.Boats = boats;
                    break;
                case "khuzait":
                    s.Houses = new[] { "mi_khuz_tent_1", "mi_khuz_tent_2" };
                    s.Barn = new[] { "khuzait_barn_a" };
                    s.FarmBarn = new[] { "khuzait_barn_a" };
                    s.Pier = new[] { "mi_pier_a" }; s.Boats = boats;
                    break;
                case "sturgia":
                    s.Houses = new[] { "mi_stu_house_a", "mi_stu_house_b", "mi_stu_house_c", "mi_stu_house_d" };
                    s.Well = "mi_bat_well";
                    s.Barn = new[] { "sturgia_village_barn_a" };
                    s.FarmBarn = new[] { "sturgia_village_barn_b", "sturgia_village_barn_a" };
                    s.Pier = westPier; s.Boats = boats;
                    break;
                case "vlandia":
                    s.Houses = new[] { "mi_vla_house_a", "mi_vla_house_b", "mi_vla_house_c", "mi_vla_house_d", "mi_vla_house_e", "mi_vla_house_f" };
                    s.Well = "mi_emp_well";
                    s.Barn = new[] { "european_village_barn_a" };
                    s.FarmBarn = new[] { "european_village_barn_b", "european_village_barn_a" };
                    s.Pier = westPier; s.Boats = boats;
                    break;
                case "battania":
                    s.Houses = new[] { "mi_bat_house_a", "mi_bat_house_b", "mi_bat_house_c" };
                    s.Well = "mi_bat_well";
                    s.Barn = new[] { "european_village_barn_a" };
                    s.FarmBarn = new[] { "sturgia_village_barn_b", "european_village_barn_b" };
                    s.Pier = westPier; s.Boats = boats;
                    break;
                default:   // empire i Essos bez wlasnej rodziny
                    s.Name = "empire";
                    s.Houses = new[] { "mi_emp_house_a", "mi_emp_house_b", "mi_emp_house_c", "mi_emp_house_d" };
                    s.Well = "mi_emp_well";
                    s.Barn = new[] { "empire_village_barn_a" };
                    s.FarmBarn = new[] { "empire_village_barn_a2", "empire_village_barn_a" };
                    s.Pier = new[] { "mi_pier_a", "mi_docks_b" }; s.Boats = boats;
                    break;
            }
            return s;
        }

        /// <summary>Przepis obrazka bez domow (domy dobiera widok: z wzoru wsi-matki albo domy kultury; ile - HousesFor). village = brak
        /// elementow (kepa domow + detale Fill). Bez fizyki, skryptow, czastek i dzwiekow: kazdy element to tylko KOPIA SIATKI (MetaMesh).</summary>
        internal static List<PieceSpec> Recipe(int model, StyleKit st)
        {
            var l = new List<PieceSpec>();
            bool fm = st.Sheds.Length > 0;
            switch (model)
            {
                case ModelMill:
                {
                    var mill = P(RoleSpecial, new[] { Watermill }, 1.5f, SizeFitH, 1f, SlotFront, 1, GroundLand, TurnPi, SrcScene);
                    mill.Essential = true; mill.WaterSide = true; mill.Pivot = true; mill.Attach = WatermillParts;
                    l.Add(mill);
                    l.Add(P(RoleDetail, new[] { "mi_sack_a" }, 0.3f, SizeNatural, NaturalScale("mi_sack_a"), SlotFrontRight, 1, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_sack_b" }, 0.3f, SizeNatural, NaturalScale("mi_sack_b"), SlotFrontLeft, 2, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_cart_b_full" }, 0.6f, SizeNatural, NaturalScale("mi_cart_b_full"), SlotRight, 3, GroundMin, TurnSeed, SrcMap));
                    break;
                }
                case ModelWindmill:
                {
                    // wyglad 3: wiatrak ze skrzydlami (sturgia_windmill_a + smiglo), skrzydla od strony kamery; brak - dawna wieza bez skrzydel
                    var wm = P(RoleSpecial, new[] { Windmill, WindmillTower }, 2.2f, SizeFitHeight, 1f, SlotBackRight, 1, GroundMin, TurnCamera, SrcScene);
                    wm.Essential = true; wm.Ordered = true; wm.Attach = WindmillParts;
                    l.Add(wm);
                    l.Add(P(RoleDetail, new[] { "mi_straw_pile" }, 0.55f, SizeNatural, NaturalScale("mi_straw_pile"), SlotBackLeft, 1, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_sack_a" }, 0.3f, SizeNatural, NaturalScale("mi_sack_a"), SlotRight, 2, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_cart_a" }, 0.6f, SizeNatural, NaturalScale("mi_cart_a"), SlotFrontRight, 3, GroundMin, TurnSeed, SrcMap));
                    break;
                }
                case ModelFarm:
                {
                    var barn = P(RoleSpecial, st.FarmBarn, 1.5f, SizeFitH, 1f, SlotRight, 1, GroundMin, TurnLongX, SrcScene);
                    barn.Essential = true; barn.BarnLike = true;
                    l.Add(barn);
                    if (fm)
                    {
                        l.Add(P(RoleDetail, new[] { st.Sheds[0] }, 1f, SizeUnit, 1f, SlotLeft, 1, GroundMin, TurnSeed, SrcRot));
                        l.Add(P(RoleDetail, new[] { st.Sheds[1] }, 1f, SizeUnit, 1f, SlotBackLeft, 2, GroundMin, TurnSeed, SrcRot));
                    }
                    l.Add(P(RoleDetail, Pens, 1.8f, SizeNatural, 1.0f, SlotBack, 1, GroundMin, TurnLongX, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_straw_pile" }, 0.55f, SizeNatural, NaturalScale("mi_straw_pile"), SlotBackRight, 1, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_sack_a" }, 0.3f, SizeNatural, NaturalScale("mi_sack_a"), SlotFrontRight, 2, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_straw_pile" }, 0.55f, SizeNatural, NaturalScale("mi_straw_pile"), SlotBackRight, 3, GroundMin, TurnSeed, SrcMap));
                    break;
                }
                case ModelGranary:
                {
                    var barn = P(RoleSpecial, st.Barn, 2.0f, SizeFitH, 1f, SlotRight, 1, GroundMin, TurnLongX, SrcScene);
                    barn.Essential = true; barn.BarnLike = true;
                    l.Add(barn);
                    l.Add(P(RoleDetail, new[] { "mi_sack_c" }, 0.35f, SizeNatural, NaturalScale("mi_sack_c"), SlotFrontRight, 1, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_barrels_a" }, 0.3f, SizeNatural, NaturalScale("mi_barrels_a"), SlotFrontRight, 2, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_cart_b_full" }, 0.6f, SizeNatural, NaturalScale("mi_cart_b_full"), SlotFront, 2, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, new[] { "mi_straw_pile" }, 0.55f, SizeNatural, NaturalScale("mi_straw_pile"), SlotBackRight, 3, GroundMin, TurnSeed, SrcMap));
                    if (fm) l.Add(P(RoleDetail, new[] { st.Sheds[1] }, 1f, SizeUnit, 1f, SlotLeft, 3, GroundMin, TurnSeed, SrcRot));
                    break;
                }
                case ModelFishing:
                {
                    var pier = P(RoleWater, st.Pier, 2.0f, SizeNatural, 1.0f, SlotFront, 1, GroundEnd, TurnLongY, SrcRot);
                    pier.Essential = true; pier.WaterSide = true;
                    l.Add(pier);
                    // wyglad 3: lodzie burta do brzegu (dluga os wzdluz brzegu) - na rzece szerokiej na ok. 1 jedn. lodz w poprzek siegala drugiego brzegu
                    var b1 = P(RoleWater, new[] { st.Boats.Length > 0 ? st.Boats[0] : "rot_boat1" }, 0.9f, SizeNatural, NaturalScale("rot_boat1"), SlotFrontRight, 1, GroundBeach, TurnLongX, SrcRot);
                    b1.WaterSide = true;
                    l.Add(b1);
                    var b2 = P(RoleWater, new[] { st.Boats.Length > 1 ? st.Boats[1] : "rot_boat3" }, 0.9f, SizeNatural, NaturalScale("rot_boat3"), SlotFrontLeft, 2, GroundBeach, TurnLongX, SrcRot);
                    b2.WaterSide = true;
                    l.Add(b2);
                    l.Add(P(RoleDetail, new[] { "mi_barrels_a" }, 0.3f, SizeNatural, NaturalScale("mi_barrels_a"), SlotFrontLeft, 2, GroundMin, TurnSeed, SrcMap));
                    l.Add(P(RoleDetail, Fishers, 1.4f, SizeNatural, NaturalScale("mi_fisherman_1"), SlotRight, 3, GroundMin, TurnSeed, SrcMap));
                    break;
                }
            }
            return l;
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
        // old_prefab_name). Kultury Essos / Calradii nie maja domow andal / fm - dla nich stary prefab wsi-matek kultury (CultureMothers).
        private static readonly Dictionary<string, string> CultureHouses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "battania", "fm_village1" }, { "sturgia", "fm_village1" }, { "nightswatch", "fm_village3" }, { "skagosi", "fm_village3" },
            { "crownlands", "andal_village3" }, { "reach", "andal_village3" }, { "vlandia", "andal_village3" },
            { "dragonstone", "andal_village5" }, { "vale", "andal_village5" }, { "river", "andal_village10" }, { "stormlands", "andal_village7" }
        };

        // Zapas "kultura", krok 2 (poprawka wygladu 08.10): zamiast GetOldPrefabName(matka) - najczestszy stary prefab wsi-matek tej kultury
        // w scenie ROT 8.1.8 (dzien-6\wioski-2200\proba-0810\gen_tablica_0810.py; kod gry nie wola juz GetPrefabName / GetOldPrefabName).
        private static readonly Dictionary<string, string> CultureMothers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "aserai", "map_icon_full_aserai_village" }, { "battania", "map_icon_full_battania_village" }, { "crownlands", "map_village_emp_all_40" },
            { "dragonstone", "map_village_emp_all_40" }, { "empire", "map_village_emp_all_40" }, { "freefolk", "map_icon_full_sturgia_village" },
            { "ghiscari", "map_icon_full_khuzait_village" }, { "ibbenese", "map_icon_full_battania_village" }, { "khuzait", "map_icon_full_khuzait_village" },
            { "lyseni", "map_icon_full_aserai_village" }, { "myrish", "map_icon_full_khuzait_village" }, { "nightswatch", "map_icon_full_battania_village" },
            { "nord", "map_village_emp_all_40" }, { "norvos", "map_village_emp_all_40" }, { "pentoshi", "map_village_emp_all_40" },
            { "qartheen", "map_village_emp_all_40" }, { "qohorik", "map_village_emp_all_40" }, { "reach", "map_icon_full_vlandia_village" },
            { "river", "map_village_emp_all_40" }, { "sarnor", "map_icon_full_khuzait_village" }, { "skagosi", "map_icon_full_battania_village" },
            { "stormlands", "map_village_emp_all_40" }, { "sturgia", "map_icon_full_sturgia_village" }, { "summer", "map_icon_full_khuzait_village" },
            { "tyroshi", "map_icon_full_aserai_village" }, { "vale", "map_village_emp_all_40" }, { "valyrian", "map_icon_full_khuzait_village" },
            { "vlandia", "map_icon_full_vlandia_village" }, { "volantine", "map_village_emp_all_40" }, { "yiti", "map_icon_full_khuzait_village" }
        };

        /// <summary>Stary prefab wsi-matek kultury (StringId kultury) albo null.</summary>
        internal static string CultureMotherPrefab(string cultureId)
        {
            string p;
            return !string.IsNullOrEmpty(cultureId) && CultureMothers.TryGetValue(cultureId, out p) ? p : null;
        }

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

        // ---------- poprawka wygladu 08.10: zwiniete prefaby domow, zastepcy grup, skala z BB ----------
        // Wioski.log 08.10 02:03 (Wolves Den / Silver Rock / White Ranch, Polnoc): pod matka zostaja fm_village4 / fm_village3 (prefab, 0 dzieci,
        // BB 0) i JEDNA szopa fm_wm_shed2_snow przeniesiona pod matke (ramka = ramka prefabu x ramka szopy w prefabie, komponent TownIcon,
        // BB 2.86x2.95 = cala grupa 6 domow, choc sama szopa w skali 0.6 ma ok. 0.5). Tak samo andal (wioski.log 07.10 19:02: andal_village3/4
        // puste, zostaje andal_wm_house1 z BB 3.49x3.53).

        internal const float SameHouseTol = 0.15f;   // ten sam dom: ta sama siatka, srodki w XY blizej niz 0.15 (rozjazd sceny i prefabu tylko w Z)
        internal const float FitMin = 0.5f, FitMax = 1.5f;   // wzor wiernie z domow matki daje ok. 1; dalej od 1 = cos nie tak, bez skrajnosci

        /// <summary>Czy encje matki rozwinac kopia jej prefabu bez sceny: ma nazwe prefabu, jest zwinieta (0 dzieci i 0 komponentow MetaMesh)
        /// i nie jest sama domem / ikona po nazwie (zastepca grupy Town Scene Managera zostaje lisciem po nazwie). Pomocniki nigdy.</summary>
        internal static bool ExpandCollapsed(string entityName, int childCount, int meshComponents, string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName) || IsHelperName(entityName) || IsHelperName(prefabName)) return false;
            if (childCount > 0 || meshComponents > 0) return false;
            return MeshForName(entityName, true) == null;
        }

        /// <summary>Ten sam dom (zastepca grupy vs dom z rozwinietego prefabu): ta sama siatka albo ta sama nazwa encji i srodki w XY (uklad
        /// korzenia matki) blizej niz SameHouseTol.</summary>
        internal static bool SameHouse(string meshA, string nameA, float ax, float ay, string meshB, string nameB, float bx, float by)
        {
            bool same = (!string.IsNullOrEmpty(meshA) && meshA == meshB) || (!string.IsNullOrEmpty(nameA) && nameA == nameB);
            if (!same) return false;
            float dx = ax - bx, dy = ay - by;
            return dx * dx + dy * dy <= SameHouseTol * SameHouseTol;
        }

        /// <summary>Dopasowanie wielkosci wzoru do wsi-matki: (pole BB widocznych domow matki / pole BB wzoru w skali matki) ^ 1/2, przyciete do
        /// FitMin..FitMax; 1 gdy ktores BB puste. Obrazek = LevelScale(poziom) x to x skala matki, czyli 0.70 / 0.75 / 0.80 wielkosci matki.</summary>
        internal static float FitToMother(float motherW, float motherD, float tplW, float tplD)
        {
            if (!(motherW > 0.05f) || !(motherD > 0.05f) || !(tplW > 0.05f) || !(tplD > 0.05f)) return 1f;
            double f = Math.Sqrt((double)motherW * motherD / ((double)tplW * tplD));
            if (double.IsNaN(f) || double.IsInfinity(f)) return 1f;
            return (float)Math.Max(FitMin, Math.Min(FitMax, f));
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
        private static readonly long CreateBudgetTicks = System.Diagnostics.Stopwatch.Frequency * 8 / 1000;   // wyglad 3: ok. 8 ms tworzenia na tick (po pierwszym wypelnieniu)
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
            public string Name = "";          // nazwa encji matki / prefabu (duplikaty zastepcow, diagnostyka)
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
            // poprawka wygladu 08.10 (poziom 3): rozwiniete zwiniete prefaby, zdjete duplikaty zastepcow, siatki pominiete bez materialu
            public int Expanded, DupDropped, MatSkipped;
            public float Fit = 1f;            // dopasowanie wielkosci do matki (MapVillageData.FitToMother)
            public float TplW, TplD, TplH;    // BB wzoru poziomu 3 w skali i obrocie matki
            public float MotherW, MotherD;    // BB widocznych domow matki (bez pomocnikow, zgliszcz, oblezenia)
            public float MotherAllW, MotherAllD;   // BB calej encji matki (z ukrytymi zgliszczami)
            public bool MotherFromAll;        // widoczne domy bez BB - wzieta cala matka
        }

        /// <summary>Warunki przejscia drzewa: maska wsi, zapas maski, droga "prefaby" (kazda encja z nazwa prefabu -> kopia prefabu bez sceny),
        /// droga 1 "rozwin zwiniete" (tylko encja zwinieta przez Town Scene Manager -> kopia prefabu); liczniki jednego przejscia.</summary>
        private sealed class WalkArgs
        {
            public uint Mask;
            public bool Lenient;
            public bool Prefabs;
            public bool Expand;
            public int Expanded, DupDropped, MatSkipped;
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
            public Kit[] Kits;                // v4: wzor obrazka na (okreg, model) - wspolny dla wszystkich wiosek okregu z tym modelem
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
            public float Cx, Cy;              // v4: srodek obrazka (po ewentualnym przesunieciu ze stoku; dymek, ogien)
            public float Deg;                 // wyglad 3: obrot obrazka w stopniach (front do wody u mlyna / rybakow; dymek wedlug niego)
            public bool Placed;               // Cx / Cy / Deg ustawione
            public string Why = "";
        }

        // ---------- v4: wzor obrazka (okreg, model) - elementy z siatkami, bez fizyki / skryptow / czastek ----------
        /// <summary>Element obrazka: kopie siatek w ramce wzoru (obrot + skala; X / Y srodka ustawia uklad, Z liczy teren przy tworzeniu).</summary>
        private sealed class Piece
        {
            public MetaMesh[] Meshes = NoMeshes;
            public MatrixFrame Local = MatrixFrame.Identity;   // obrot + skala, origin X / Y w jedn. wzoru
            public float Bottom, Top;         // najnizszy / najwyzszy punkt BB pod obrotem i skala (jedn. wzoru, wzgledem origin)
            public float Cx0, Cy0;            // srodek BB w XY wzgledem origin (jedn. wzoru)
            public float Rx, Ry;              // pol boku BB w X / Y wzoru
            public int Role;
            public int Levels = 14;           // bity 1..3: na jakich poziomach stoi
            public int ThinIndex = -1;        // kepa "village": numer do przerzedzenia od brzegu
            public int Ground;
            public bool NoSeason, Essential, WaterSide;
            public int Parent = -1;           // element doczepiony (kolo mlyna, skrzydla wiatraka) - numer rodzica, ramka wzgledem niego
            public bool Pivot;                // wysokosc wedlug punktu zaczepienia: ikona wsi (origin na gruncie + ZOff, jak wies gry), mlyn (podloga = os kola)
            public float ZOff;                // ikona: wysokosc origin nad korzeniem matki (jedn. wzoru); dom wzoru (PrefabZ): to samo - tylko do logu korekty
            public bool PrefabZ;              // dom z wzoru wsi-matki: dawniej wysokosc z ramki prefabu (ZOff), od wygladu 3 dol BB na gruncie
            public bool FaceCamera;           // wiatrak: skrzydla (lokalne -Y) od strony kamery mapy (swiat -Y), obrot elementu ustawiany przy tworzeniu
            public string Name = "";          // nazwa siatki (log)
        }

        private sealed class Kit
        {
            public int Model, Asked;          // zbudowany model / proszony (zapas: windmill -> farm, mill / fishing -> village)
            public string Style = "";
            public readonly List<Piece> Pieces = new List<Piece>();
            public float[] ThinX, ThinY;      // kepa "village": srodki budynkow do przerzedzenia (wedlug ThinIndex)
            public bool[] ThinDetail;         // kepa "village": budynek to detal (studnia, szopa, stog...) - limit na poziomie (VillageDetailMax)
            public int ThinN;
            public float H = MapVillageData.IconHouseH;   // wielkosc domu wsi okregu (jedn. wzoru)
            public bool HasWater;             // ma elementy przy wodzie (mlyn, pomost, lodzie)
            // wyglad 3: kotwica przy wodzie - punkt elementu Anchor (mlyn: srodek kola; pomost: ladowy koniec), ktory staje na brzegu wody
            public int Anchor = -1;           // numer elementu (k.Pieces) albo -1
            public float AnchorDX, AnchorDY;  // punkt kotwicy wzgledem origin elementu (jedn. wzoru)
            public float AnchorOut;           // o ile za brzegiem w strone wody (jedn. wzoru; mlyn: 0.6 polgrubosci kola)
            public float WheelR;              // promien kola mlyna (jedn. wzoru), 0 = brak kola
            public int AttachMiss;            // doczepione siatki, ktorych brak w grze (kolo / skrzydla)
            public bool HasFan;               // wiatrak ze skrzydlami (sturgia_windmill_fan_a_open doczepione)
            public List<HouseUnit> Units;     // domy wzoru wsi-matki (rodzina andal / fm) - siatki rodziny w skali i wysokosci jak we wzorze
            public string Why = "";
        }

        /// <summary>Dom wsi okregu z wzoru (lisc z siatka poziomu 3 w ramce zlozonej wzgledem korzenia matki, obrocony do osi domow).</summary>
        private sealed class HouseUnit
        {
            public MetaMesh[] Meshes;
            public MatrixFrame F;
            public bool NoSeason;
            public string Name = "";
            public float Ext;                 // wiekszy bok BB w XY (jedn. wzoru)
            public float Scale;               // skala (|s|) ramki
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
        private int _tplComp, _tplName, _tplExpand, _tplPrefab, _tplCulture, _meshComp, _meshName, _meshPrefab, _prefabMade, _prefabNoMesh, _prefabMissing, _nameMiss;
        private readonly Dictionary<string, MetaMesh> _meshByName = new Dictionary<string, MetaMesh>(StringComparer.Ordinal);   // nazwa siatki -> wzor (null = brak)
        private readonly Dictionary<string, GameEntity> _prefabs = new Dictionary<string, GameEntity>(StringComparer.Ordinal);   // prefab -> kopia bez sceny (null = brak)
        private int _diagMothers, _diagPrefabs, _stDiag;   // diagnostyka do wioski.log: drzewa 3 pierwszych wsi-matek (+ pierwszej bez wzoru) i 3 prefabow
        private bool _diagFailDone;
        private bool _diagOn;   // drzewa encji matek / kopii prefabow w wioski.log tylko w autotescie (AutotestActive przy Load)
        // poprawka wygladu 08.10: rozwiniete zwiniete prefaby (droga 1), duplikaty zastepcow, siatki bez materialu, dopasowanie skali, 3 wzory w logu
        private int _expandedTotal, _dupDropped, _matSkipped, _matBad, _matDefault, _fitN, _diagTemplates;
        private float _fitMin = float.MaxValue, _fitMax, _fitSum;
        private const int DiagKindsMax = 40;   // autotest: najwyzej tyle rodzajow wzoru z lista siatek w wioski.log
        private readonly HashSet<string> _diagKinds = new HashSet<string>(StringComparer.Ordinal);   // rodzaje wzoru juz wypisane (autotest)
        private readonly Dictionary<string, int> _prefabMeshes = new Dictionary<string, int>(StringComparer.Ordinal);   // prefab -> komponentow MetaMesh w kopii
        private readonly Dictionary<string, bool> _prefabExists = new Dictionary<string, bool>(StringComparer.Ordinal); // nazwa encji -> jest prefab o tej nazwie
        private readonly Dictionary<string, bool> _matOk = new Dictionary<string, bool>(StringComparer.Ordinal);       // nazwa siatki -> ma material (podsiatka z waznym materialem)
        private long _createTicks;
        private int _stCreate, _stFx, _stHover, _stVis, _stTemplate, _stTick, _stRemove;
        private string _lastSummary = "";
        // v4 (zdjecia 08.10 03:37): obrazki wedlug modelu, zapasy, teren pod kazdym budynkiem, stok / brzeg, brzeg wody
        private readonly int[] _modelMade = new int[MapVillageData.ModelCount];      // wioski postawione wedlug zbudowanego modelu (od wczytania)
        private readonly int[] _modelAsked = new int[MapVillageData.ModelCount];     // ... wedlug modelu z pliku
        private readonly SortedDictionary<string, int> _kitFallback = new SortedDictionary<string, int>(StringComparer.Ordinal);   // "mill->village" -> okregi
        private int _kitsBuilt, _kitMeshMiss, _snapPieces, _tiltPieces, _slopeVillages, _slopeShift, _slopeSqueeze, _slopeDropped, _slopeLeft,
                    _steepDropped, _diagSlope;
        // wyglad 3 (zdjecia 08.10 04:42): wysokosc z dolu BB, lad pod budynkami, brzeg wody (mlyn / rybacy), skrzydla wiatraka
        private float _seaLevel = MapVillageData.NoSea;   // poziom morza (ROT 5.5 przy zgodnym CRC sceny; inaczej lad tylko z siatki)
        private int _faceTest = -1;                       // sprawdzian scian siatki na bramach wsi gry: -1 nie robiony, 1 dobry, 0 zly (lad z wysokosci)
        private int _bbPieces, _corrN, _landMoved, _landMiss, _landPushed, _landDropped, _landStuck, _diagLand,
                    _shoreRiver, _shoreSea, _noWaterMill, _noWaterFish, _wheelWater, _wheelDry, _wheelCam, _wheelTouch, _millNoWheel,
                    _boatsWater, _boatsBeach, _pierShore, _diagWater, _fanOn, _fanOff, _attachMiss,
                    _landNear, _shiftNear;   // recenzja: przeniesione na lad blizej sasiada (brak ladu z odstepem); przesuniecie ku wodzie / ze stoku skrocone przez sasiada
        private float _corrMin = float.MaxValue, _corrMax = float.MinValue, _corrSum, _shiftSum, _shiftMax;
        private readonly HashSet<string> _hLogged = new HashSet<string>(StringComparer.Ordinal);   // domy wzoru z korekta wysokosci juz w wioski.log
        private readonly HashSet<string> _meshLogged = new HashSet<string>(StringComparer.Ordinal);   // siatki obrazkow juz opisane w wioski.log
        private readonly HashSet<string> _kitLogged = new HashSet<string>(StringComparer.Ordinal);    // (model, styl) juz opisane
        private readonly HashSet<string> _missLogged = new HashSet<string>(StringComparer.Ordinal);   // brakujace siatki juz zapisane

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
            _diagOn = AutotestActive();
            // wyglad 3: poziom morza ze sceny ROT 8.1.8 (water_properties water_level 5.500) - tylko gdy plik jest dla tej sceny (CRC zgodne);
            // bez CRC lad tylko wedlug scian siatki (zadnego nowego odczytu silnika: GetWaterLevel na scenie mapy gra nie wola)
            _seaLevel = check == MapVillageData.CheckMatch ? MapVillageData.SeaLevelRot : MapVillageData.NoSea;
            Log.Info(head + "; wiosek " + file.Records.Count + " w " + byDistrict.Count + " okregach; odrzucone wiersze " + file.BadRows
                     + " (" + file.ReasonsText() + (file.FirstBad.Length > 0 ? "; pierwszy: " + file.FirstBad : "") + ")"
                     + (file.LevelClamped > 0 ? "; poziom przyciety do 1-3: " + file.LevelClamped : "")
                     + "; obrazki (kolumna model" + (file.HasModelColumn ? "" : " - BRAK, kazda wioska village") + "): " + file.ModelsText()
                     + (file.ModelUnknown > 0 ? " (nieznana wartosc -> village: " + file.ModelUnknown + ")" : "")
                     + "; CRC sceny: " + (check == MapVillageData.CheckMatch ? "zgodne (" + sceneCrc + ")"
                                          : "brak w pliku - sprawdzenie pominiete (scena gry " + sceneCrc + "; do naglowka: '# scene_xml_crc: " + sceneCrc + "')")
                     + "; pominiete od razu: brak wsi gry " + noMother + " (okregow " + noMotherDistricts + "), nie wies " + notVillage
                     + ", poza granica mapy " + outside + "; maski sceny civilian " + _maskCivil + ", level_1/2/3 " + _maskL1 + "/" + _maskL2 + "/" + _maskL3
                     + ", looted/siege " + _maskLooted + "/" + _maskSiege
                     + "; lad pod budynkami: sciana siatki ladowej" + (_seaLevel > MapVillageData.NoSea * 0.5f
                         ? " i teren nad poziomem morza " + F2(_seaLevel) + " (scena ROT)" : " (poziom morza nieznany - plik bez CRC sceny)")
                     + "; drzewa encji w wioski.log: " + (_diagOn ? "tak (autotest)" : "nie (tylko w autotescie)")
                     + "; czas " + sw.ElapsedMilliseconds + " ms. Obrazki powstaja przy kamerze (z <= "
                     + Settings.Current.MapVillagesHideAboveCameraHeight.ToString("0", CultureInfo.InvariantCulture) + "), nic nie idzie do zapisu gry.");
        }

        private static int _autotest = -1;   // -1 nie sprawdzone, 0 nie, 1 tak

        /// <summary>Czy dziala autotest (pole CrashScribe.Autotest.Active - jest tylko w DLL autotestu CrashScribe-AT1; w grze Jeffa typu brak).
        /// Sprawdzane raz na sesje gry (autotest uzbraja sie przy starcie, przed kampania); blad = nie.</summary>
        private static bool AutotestActive()
        {
            if (_autotest >= 0) return _autotest == 1;
            _autotest = 0;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    FieldInfo f = t != null ? t.GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(null)) _autotest = 1;
                    break;
                }
            }
            catch { _autotest = 0; }
            return _autotest == 1;
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
            sb.Append("; wzory wsi-matek ").Append(_templatesOk).Append(" dobrych (drzewo: komponenty ").Append(_tplComp).Append(", nazwy ").Append(_tplName).Append(", rozwiniete prefaby domow ").Append(_tplExpand)
              .Append("; prefaby ").Append(_tplPrefab).Append("; kultura ").Append(_tplCulture).Append("), ").Append(_templatesBad).Append(" bez wzoru")
              .Append(_lenient > 0 ? " (dobrych z zapasu maski: " + _lenient + ")" : "")
              .Append("; siatki wzorow (poziom 3) z komponentow ").Append(_meshComp).Append(", z nazw ").Append(_meshName).Append(", z prefabow ").Append(_meshPrefab)
              .Append("; prefaby bez sceny ").Append(_prefabMade).Append(" (bez siatek ").Append(_prefabNoMesh).Append(", brak ").Append(_prefabMissing).Append(')')
              .Append("; nazwy bez siatki ").Append(_nameMiss)
              .Append("; rozwiniete zwiniete prefaby domow ").Append(_expandedTotal).Append(", zdjete duplikaty zastepcow ").Append(_dupDropped)
              .Append(", siatki pominiete bez materialu ").Append(_matSkipped).Append(" (nazw ").Append(_matBad).Append("), nazw z samym domyslnym materialem ").Append(_matDefault)
              .Append("; dopasowanie skali do matki ").Append(_fitN > 0
                  ? F2(_fitMin) + ".." + F2(_fitMax) + " (srednio " + F2(_fitSum / _fitN) + ", wzorow " + _fitN + ")" : "-")
              .Append("; przerzedzone od brzegu kepy ").Append(_thinned).Append(", obrot do osi domow ").Append(_axisTurned);
            // v4: obrazki wedlug modelu (z pliku -> postawione), zapasy, teren
            sb.Append("; obrazki (od wczytania, model z pliku -> postawiony): ");
            for (int i = 0; i < MapVillageData.ModelCount; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(MapVillageData.ModelName(i)).Append(' ').Append(_modelAsked[i]).Append("->").Append(_modelMade[i]);
            }
            sb.Append("; wzory obrazkow ").Append(_kitsBuilt).Append(" (zapas: ");
            if (_kitFallback.Count == 0) sb.Append('-');
            else { bool f1 = true; foreach (var kv in _kitFallback) { if (!f1) sb.Append(", "); f1 = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); } }
            sb.Append("; brak siatek ").Append(_kitMeshMiss).Append(')');
            sb.Append("; teren: budynki na wlasnym gruncie ").Append(_snapPieces).Append(", ikony pochylone do stoku ").Append(_tiltPieces)
              .Append("; stok / brzeg (rozrzut gruntu > prog) wiosek ").Append(_slopeVillages).Append(": przesuniete w obrysie ").Append(_slopeShift)
              .Append(", scisniete ").Append(_slopeSqueeze).Append(", zdjete budynki ").Append(_slopeDropped).Append(", dalej nierowne ").Append(_slopeLeft)
              .Append("; zdjete budynki na stromym gruncie ").Append(_steepDropped);
            // wyglad 3 (zdjecia 08.10 04:42)
            sb.Append("; wysokosc: dol BB na najnizszym gruncie pod obrysem (- do ").Append(F2(MapVillageData.GroundSink)).Append(") budynkow ").Append(_bbPieces)
              .Append(", korekta domow wzoru wobec dawnej wysokosci z prefabu ").Append(_corrN > 0
                  ? F2(_corrMin) + ".." + F2(_corrMax) + " (srednio " + F2(_corrSum / _corrN) + ", domow " + _corrN + ")" : "-")
              .Append("; lad (siatka ").Append(_faceTest == 1 ? "dobra" : _faceTest == 0 ? "ZLA - tylko wysokosc nad morzem" : "-").Append("): srodki obrazkow przeniesione na lad ").Append(_landMoved).Append(" (brak ladu w ").Append(F2(MapVillageData.LandSearchR))
              .Append(" jedn. ").Append(_landMiss).Append("), budynki przesuniete na lad ").Append(_landPushed).Append(", zdjete z wody ").Append(_landDropped)
              .Append(", niezbedne dalej w wodzie ").Append(_landStuck)
              .Append("; odstep od innych wiosek ").Append(F2(MapVillageData.NeighborMinDist)).Append(": na lad mimo sasiada ").Append(_landNear)
              .Append(", przesuniecia skrocone przez sasiada ").Append(_shiftNear)
              .Append("; woda (mlyn / rybacy): brzeg rzeki / jeziora ").Append(_shoreRiver).Append(", morza ").Append(_shoreSea)
              .Append(", brak wody w ").Append(F2(MapVillageData.ShoreMaxR)).Append(" jedn.: mlyn->wiatrak ").Append(_noWaterMill).Append(", rybacy->wioska ").Append(_noWaterFish)
              .Append("; przesuniecie obrazka ku wodzie srednio ").Append(F2(_shoreRiver + _shoreSea > 0 ? _shiftSum / (_shoreRiver + _shoreSea) : 0f)).Append(" (najwiecej ").Append(F2(_shiftMax)).Append(')')
              .Append("; kolo mlyna nad woda ").Append(_wheelWater).Append(", nad ladem ").Append(_wheelDry).Append(" (dotyka wody ").Append(_wheelTouch)
              .Append(", od strony kamery ").Append(_wheelCam).Append("), mlyn bez kola ").Append(_millNoWheel)
              .Append("; pomost od brzegu ").Append(_pierShore).Append(", lodzie na wodzie ").Append(_boatsWater).Append(", na brzegu ").Append(_boatsBeach)
              .Append("; wiatraki ze skrzydlami ").Append(_fanOn).Append(", bez skrzydel ").Append(_fanOff).Append("; brak siatek doczepionych ").Append(_attachMiss);
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
                                // wyglad 3: obrazek przy wodzie kosztuje wiecej (lad pod budynkami, brzeg wody) - po pierwszym wypelnieniu
                                // najwyzej ok. 8 ms tworzenia na cwierc sekundy, reszta w nastepnym ticku (bez przyciec przy szybkim przesuwaniu kamery)
                                if (_firstFillDone && made > 0 && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > CreateBudgetTicks) continue;
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

        /// <summary>Obrazek wioski. v4 (zdjecia 08.10 03:37): wzor obrazka (okreg, model) -> elementy widoczne przy poziomie (kepa "village"
        /// przerzedzana od brzegu) -> stok / brzeg (przesuniecie w obrysie z pliku, scisniecie, zdjecie budynkow z krawedzi). WYGLAD 3 (zdjecia
        /// 08.10 04:42): (4) srodek obrazka na ladzie - obrys z pliku pod woda albo na scianie bez ladu -> najblizsze miejsce na ladzie; (2) mlyn /
        /// rybacy: najblizszy brzeg wody wokol (16 kierunkow, 5 jedn.), front obrazka do wody, obrazek przesuniety tak, ze kolo mlyna stoi nad
        /// woda, a ladowy koniec pomostu na brzegu, domy za nimi od strony ladu (brak wody: mlyn = wiatrak, rybacy = wioska); (3) skrzydla
        /// wiatraka od strony kamery; (4) kazdy budynek caly na ladzie (przesuniety albo zdjety); (1) wysokosc KAZDEGO budynku: dol BB na
        /// najnizszym gruncie pod obrysem minus male zaglebienie - nie wysokosc z ramki prefabu wsi-matki, ktora liczyla sie od KORZENIA matki
        /// (Berrybush: prefab andal_village3 0.23 pod korzeniem), nie od gruntu; ikona wsi Calradii / Essos - pochylona do stoku, origin na
        /// gruncie jak wies gry; mlyn - podloga (os kola) nad woda (MapVillageData.MillFloor). Tylko kopie siatek: bez fizyki, skryptow, czastek.</summary>
        private void Create(Slot s, Template t)
        {
            int lv = Math.Max(1, Math.Min(3, s.R.Level));
            Scene scene = MapScene;
            if (scene == null) throw new InvalidOperationException("brak sceny mapy");
            // 0. uklad: korzen BEZ pochylenia (obrot z pliku), skala matki x poziom x dopasowanie do matki
            Vec3 S = t.Scale * (MapVillageData.LevelScale(lv) * t.Fit);
            float yaw = (float)(s.R.FrontDeg * Math.PI / 180.0);
            var g = new Geo
            {
                Ca = (float)Math.Cos(yaw), Sa = (float)Math.Sin(yaw), Sx = S.x, Sy = S.y, Sz = Math.Max(0.05f, S.z),
                Cx = s.R.X, Cy = s.R.Y, Ms = Campaign.Current.MapSceneWrapper, Sea = _seaLevel
            };
            g.Faces = FacesOk(g);
            var notes = new StringBuilder();
            // 1. (poprawka 4) srodek obrazka na ladzie
            ToLand(s, g, notes);
            // 2. (poprawka 2) mlyn / rybacy: brzeg wody wokol; brak wody w 5 jedn. -> mlyn = wiatrak, rybacy = wioska
            int model = s.R.Model < 0 || s.R.Model >= MapVillageData.ModelCount ? MapVillageData.ModelVillage : s.R.Model;
            MapVillageData.ShoreHit shore = null;
            if (model == MapVillageData.ModelMill || model == MapVillageData.ModelFishing)
            {
                shore = MapVillageData.FindShore(g.Group, g.H, g.Sea, g.Cx, g.Cy, s.R.FrontDeg, MapVillageData.ShoreDirs, MapVillageData.ShoreMaxR,
                                                 MapVillageData.ShoreStep, MapVillageData.ShoreNorthPenalty);
                if (shore == null)
                {
                    if (model == MapVillageData.ModelMill) { _noWaterMill++; model = MapVillageData.ModelWindmill; }
                    else { _noWaterFish++; model = MapVillageData.ModelVillage; }
                    notes.Append("; brak wody w ").Append(F2(MapVillageData.ShoreMaxR)).Append(" jedn. -> ").Append(MapVillageData.ModelName(model));
                }
            }
            Kit k = KitFor(s.D, t, model);
            if (k == null || k.Pieces.Count == 0)
                throw new InvalidOperationException("brak wzoru obrazka" + (k != null && k.Why.Length > 0 ? " (" + k.Why + ")" : ""));
            // 3. elementy widoczne przy poziomie
            bool[] keepThin = null;
            if (k.ThinN > 0 && k.ThinX != null && k.ThinY != null)
            {
                int want = MapVillageData.VillageWant(k.ThinN, lv);
                // od brzegu kepy, ale detali (studnia, szopy, stog) najwyzej 2 / 3 / wszystkie - poziom 1 to glownie domy (recenzja 08.10)
                if (want < k.ThinN) keepThin = MapVillageData.ThinVillage(s.R.Uid, k.ThinX, k.ThinY, k.ThinDetail, want, MapVillageData.VillageDetailMax(lv));
            }
            var vis = new List<int>();
            for (int i = 0; i < k.Pieces.Count; i++)
            {
                Piece p = k.Pieces[i];
                if (p.Parent >= 0 || (p.Levels & (1 << lv)) == 0) continue;
                if (keepThin != null && p.ThinIndex >= 0 && p.ThinIndex < keepThin.Length && !keepThin[p.ThinIndex]) continue;
                vis.Add(i);
            }
            if (vis.Count == 0) throw new InvalidOperationException("brak elementow obrazka na poziomie " + lv);
            int n = vis.Count;
            var px = new float[n];
            var py = new float[n];
            var alive = new bool[n];
            // osobne ramki elementow tej wioski: obrot ze skala, srodek / polboki / dol / gora BB (wiatrak obracany do kamery, pomost i lodzie
            // na rzece mniejsze); jedn. wzoru
            var rot = new Mat3[n];
            var cx0 = new float[n];
            var cy0 = new float[n];
            var rxj = new float[n];
            var ryj = new float[n];
            var bot = new float[n];
            var top = new float[n];
            var adx = new float[n];   // mlyn: srodek kola wzgledem origin (jedn. wzoru; obracany z mlynem - 6b)
            var ady = new float[n];
            int ja = -1;
            for (int j = 0; j < n; j++)
            {
                Piece p = k.Pieces[vis[j]];
                px[j] = p.Local.origin.x; py[j] = p.Local.origin.y; alive[j] = true;
                rot[j] = p.Local.rotation; cx0[j] = p.Cx0; cy0[j] = p.Cy0; rxj[j] = p.Rx; ryj[j] = p.Ry; bot[j] = p.Bottom; top[j] = p.Top;
                if (vis[j] == k.Anchor) { ja = j; adx[j] = k.AnchorDX; ady[j] = k.AnchorDY; }
            }
            int minKeep = MinKeep(k, vis);
            // 4. woda: front obrazka do wody, obrazek przesuniety tak, zeby kotwica (srodek kola / ladowy koniec pomostu) stanela na brzegu
            bool water = k.HasWater && shore != null;
            float shift = 0f;
            if (water)
            {
                yaw = MapVillageData.ShoreYaw(shore.Dx, shore.Dy);
                g.Ca = (float)Math.Cos(yaw);
                g.Sa = (float)Math.Sin(yaw);
                if (!shore.Sea) ShrinkForRiver(k, vis, px, py, rot, cx0, cy0, rxj, ryj, bot, top);
                if (ja >= 0)
                {
                    float ay = AnchorY(k.Pieces[vis[ja]], ja, py, cy0, ryj, ady);
                    float want = shore.Edge + AnchorOutW(k, k.Pieces[vis[ja]], ja, shore.Sea, ryj, g) - ay * g.Sy;
                    shift = Clamp(want, -MapVillageData.ShoreMaxBack, MapVillageData.ShoreMaxShift);
                    // srodek obrazka zostaje na ladzie (kotwica jest przed srodkiem; gdy nie - mniejsze przesuniecie); recenzja: i nie blizej
                    // innej wioski niz NeighborMinDist (przesuniecie do 3.5 jedn. stawialo mlyn przy sasiedzie) - wtedy tez mniejsze przesuniecie,
                    // kolo / pomost dosuwa do wody PlaceAtWater (do 1 jedn.)
                    bool nearHit = false;
                    Func<float, bool> okAt = sh =>
                    {
                        float x = g.Cx + shore.Dx * sh, y = g.Cy + shore.Dy * sh;
                        if (!g.Land(x, y)) return false;
                        if (Math.Abs(sh) > 0.01f && !FarFromOthers(s, x, y)) { nearHit = true; return false; }
                        return true;
                    };
                    for (int it = 0; it < 6 && Math.Abs(shift) > 0.01f && !okAt(shift); it++) shift *= 0.6f;
                    if (!okAt(shift)) shift = 0f;
                    if (nearHit) _shiftNear++;
                    g.Cx += shore.Dx * shift;
                    g.Cy += shore.Dy * shift;
                }
                if (shore.Sea) _shoreSea++; else _shoreRiver++;
                _shiftSum += Math.Abs(shift);
                if (Math.Abs(shift) > _shiftMax) _shiftMax = Math.Abs(shift);
            }
            // 5. stok / brzeg (wyglad 2) - przesuniecia srodka tylko na lad
            Settle(s, k, g, vis, px, py, alive, minKeep);
            // 6. elementy przy wodzie na brzegu SWOJEJ linii: kolo mlyna nad woda, pomost od brzegu, lodzie na wodzie
            var wz = new float[n];
            for (int j = 0; j < n; j++) wz[j] = float.NaN;
            if (water) PlaceAtWater(k, g, vis, px, py, alive, cx0, cy0, ryj, wz, shore, ja, adx, ady);
            // 6b. mlyn z woda na polnoc (kamera mapy patrzy na polnoc - kolo za mlynem): mlyn obrocony o 50 st. wokol kola (kolo zostaje nad
            // woda), budynek w strone ladu na wschod albo zachod - kolo widac obok mlyna, nie za nim
            float millTurn = 0f;
            if (water && ja >= 0 && alive[ja] && k.Pieces[vis[ja]].Ground == MapVillageData.GroundLand && k.WheelR > 0f && shore.Dy > 0.35f)
                millTurn = TurnMill(s, g, ja, px, py, rot, cx0, cy0, rxj, ryj, adx, ady, wz[ja]);
            // 7. wiatrak: skrzydla (lokalne -Y wiatraka) od strony kamery mapy (swiat -Y), +-25 st. z ziarna wioski; srodek BB zostaje
            for (int j = 0; j < n; j++)
            {
                Piece p = k.Pieces[vis[j]];
                if (!p.FaceCamera || !alive[j]) continue;
                float jit = ((MapVillageData.Fnv(s.R.Uid + ":wiatrak") % 51u) - 25f) * (float)Math.PI / 180f;
                float extra = jit - yaw;
                Mat3 rz = Mat3.Identity;
                rz.RotateAboutUp(extra);
                float bx = px[j] + cx0[j], by = py[j] + cy0[j];
                rot[j] = rz.TransformToParent(in rot[j]);
                float ce = (float)Math.Cos(extra), se = (float)Math.Sin(extra);
                float ncx = ce * cx0[j] - se * cy0[j], ncy = se * cx0[j] + ce * cy0[j];
                cx0[j] = ncx; cy0[j] = ncy;
                float rr0 = Math.Max(rxj[j], ryj[j]);
                rxj[j] = rr0; ryj[j] = rr0;
                px[j] = bx - ncx; py[j] = by - ncy;
            }
            // 8. (poprawka 4) kazdy budynek caly na ladzie: przesuniety (do srodka kepy / od wody) albo zdjety, gdy niekonieczny
            OnLand(k, g, vis, px, py, alive, minKeep, cx0, cy0, rxj, ryj, water, notes);
            // 9. (poprawka 1) wysokosc kazdego budynku z gruntu w JEGO punkcie: dol BB na najnizszym gruncie pod obrysem - zaglebienie
            float zc = g.H(g.Cx, g.Cy);
            var frames = new MatrixFrame[n];
            var tilt = new bool[n];
            var orgW = new float[n];
            int kept = 0;
            for (int j = 0; j < n; j++) if (alive[j]) kept++;
            int boatsW = 0, boatsB = 0;
            for (int j = 0; j < n; j++)
            {
                if (!alive[j]) continue;
                Piece p = k.Pieces[vis[j]];
                float ckx = px[j] + cx0[j], cky = py[j] + cy0[j], rx = rxj[j], ry = ryj[j];   // srodek i polboki BB (jedn. wzoru)
                float hgtW = Math.Max(0.01f, (top[j] - bot[j]) * g.Sz);
                MatrixFrame f = p.Local;
                f.rotation = rot[j];
                float originW;   // wysokosc origin elementu w swiecie
                switch (p.Ground)
                {
                    case MapVillageData.GroundLand:
                    {
                        // mlyn wodny: podloga (os kola) - kolo dotyka wody, mlyn nie zakopany w brzegu (grunt od strony ladu, -Y)
                        float hb0 = g.HK(ckx, cky - ry * 0.8f), hb1 = g.HK(ckx - rx * 0.8f, cky - ry * 0.8f), hb2 = g.HK(ckx + rx * 0.8f, cky - ry * 0.8f);
                        originW = MapVillageData.MillFloor(Math.Min(hb0, Math.Min(hb1, hb2)), wz[j], k.WheelR * g.Sz, top[j] * g.Sz);
                        break;
                    }
                    case MapVillageData.GroundEnd:
                    {
                        // pomost: poklad 0.175 skali nad woda (jak rot_dock1 w scenie ROT: origin 0.135 pod poziomem morza przy skali 1);
                        // bez wody - poklad na gruncie przy ladowym koncu (jak dotad)
                        float ps = Math.Abs(rot[j].GetScaleVector().x);
                        originW = !float.IsNaN(wz[j]) ? wz[j] + 0.175f * g.Sz * ps - top[j] * g.Sz : g.HK(ckx, cky - ry * 0.9f) + 0.01f - top[j] * g.Sz;
                        break;
                    }
                    case MapVillageData.GroundBeach:
                    {
                        // lodz: na wodzie (teren pod nia nie wyzej niz woda) - origin na wodzie, jak lodzie ROT w scenie (5.50); inaczej na brzegu
                        float h0 = g.HK(ckx, cky);
                        if (!float.IsNaN(wz[j]) && h0 <= wz[j] + 0.02f) { originW = wz[j]; boatsW++; }
                        else
                        {
                            float lo = Math.Min(h0, Math.Min(Math.Min(g.HK(ckx - rx, cky - ry), g.HK(ckx + rx, cky - ry)), Math.Min(g.HK(ckx - rx, cky + ry), g.HK(ckx + rx, cky + ry))));
                            originW = lo - MapVillageData.Sink(hgtW) - bot[j] * g.Sz;
                            boatsB++;
                        }
                        break;
                    }
                    case MapVillageData.GroundTilt:
                    {
                        // ikona calej wsi gry (jedna siatka): jak wies gry - pochylona do normalnej terenu w swoim srodku, origin na gruncie
                        float wx = g.WX(ckx, cky), wy = g.WY(ckx, cky), hz;
                        Vec3 nw;
                        g.Ms.GetTerrainHeightAndNormal(new Vec2(wx, wy), out hz, out nw);
                        g.Calls++;
                        f.rotation = Tilted(rot[j], nw, g);
                        originW = hz - 0.02f + p.ZOff * g.Sz;   // jak dawny korzen (z - 0.02) + wysokosc ikony we wzorze
                        tilt[j] = true;
                        break;
                    }
                    default:
                    {
                        // dom / detal / stodola / wiatrak: dol BB na najnizszym z 5 punktow pod obrysem - zaglebienie (nic nie wisi i nic nie
                        // zapada sie po dach); na zbyt stromym gruncie budynek niekonieczny zdjety
                        float h0 = g.HK(ckx, cky), h1 = g.HK(ckx - rx, cky - ry), h2 = g.HK(ckx + rx, cky - ry),
                              h3 = g.HK(ckx - rx, cky + ry), h4 = g.HK(ckx + rx, cky + ry);
                        float lo = Math.Min(h0, Math.Min(Math.Min(h1, h2), Math.Min(h3, h4)));
                        float hi = Math.Max(h0, Math.Max(Math.Max(h1, h2), Math.Max(h3, h4)));
                        if (hi - lo > 0.5f * hgtW && !p.Essential && kept > minKeep)
                        {
                            alive[j] = false;
                            kept--;
                            _steepDropped++;
                            continue;
                        }
                        originW = lo - MapVillageData.Sink(hgtW) - bot[j] * g.Sz;
                        _bbPieces++;
                        if (p.PrefabZ) NoteHeight(p, s, originW - (lo + p.ZOff * g.Sz), bot[j], g.Sz);
                        break;
                    }
                }
                orgW[j] = originW;
                f.origin = new Vec3(px[j], py[j], (originW - zc) / g.Sz, 1f);
                frames[j] = f;
            }
            if (kept <= 0) throw new InvalidOperationException("brak elementow obrazka po terenie");
            if (water) WaterNote(s, k, g, vis, px, py, alive, cx0, cy0, ryj, wz, orgW, shore, ja, shift, boatsW, boatsB, notes, adx, ady, millTurn);
            else if (notes.Length > 0 && _diagLand < 15)
            {
                _diagLand++;
                Log.Info("Wioski: drzewo ladu " + s.R.Uid + " '" + s.R.Name + "' (" + MapVillageData.ModelName(s.R.Model) + " -> " + MapVillageData.ModelName(k.Model) + ")" + notes + ".");
            }
            if (k.Model == MapVillageData.ModelWindmill) { if (k.HasFan) _fanOn++; else _fanOff++; }
            if (k.Model == MapVillageData.ModelMill && !(k.WheelR > 0f)) _millNoWheel++;
            GameEntity root = GameEntity.CreateEmpty(scene, false, false, false);   // bez fizyki, bez skryptow
            if (root == (GameEntity)null) throw new InvalidOperationException("CreateEmpty zwrocil null");
            try
            {
                root.Name = "arm_mapvillage_" + SafeName(s.R.Uid);   // gra szuka osad po nazwie encji (SettlementVisual.cs:465) - nazwa nie moze byc id osady
                Mat3 rr = Mat3.Identity;
                rr.RotateAboutUp(yaw);
                rr.ApplyScaleLocal(in S);
                MatrixFrame rf = new MatrixFrame(in rr, new Vec3(g.Cx, g.Cy, zc, 1f));
                root.SetFrame(ref rf, true);
                var made = new GameEntity[k.Pieces.Count];
                int meshes = 0, snapped = 0, tilted = 0;
                for (int j = 0; j < n; j++)
                {
                    if (!alive[j]) continue;
                    Piece p = k.Pieces[vis[j]];
                    GameEntity e = MakeEntity(scene, root, p, frames[j], t.NoSeason, ref meshes);
                    if (e == (GameEntity)null) continue;
                    made[vis[j]] = e;
                    if (tilt[j]) tilted++; else snapped++;
                }
                // doczepione (kolo mlyna, skrzydla i schody wiatraka): pod rodzicem, ramka wzgledem niego (w jednostkach siatki rodzica)
                for (int i = 0; i < k.Pieces.Count; i++)
                {
                    Piece p = k.Pieces[i];
                    if (p.Parent < 0 || p.Parent >= made.Length || made[p.Parent] == (GameEntity)null) continue;
                    MakeEntity(scene, made[p.Parent], p, p.Local, t.NoSeason, ref meshes);
                }
                if (meshes == 0) throw new InvalidOperationException("kopia bez siatek");
                if (t.NoSeason) root.EntityFlags = root.EntityFlags | EntityFlags.NotAffectedBySeason;   // snieg jak na matce (projekt 5.1)
                root.EntityFlags = root.EntityFlags | EntityFlags.DoNotTick;
                root.SetReadyToRender(true);                 // jak SettlementVisual.OnStartup (:590-591)
                root.SetEntityEnvMapVisibility(false);
                root.SetVisibilityExcludeParents(false);     // pokaze UpdateVisibility
                if (keepThin != null) _thinned++;
                if (t.HasAxis) _axisTurned++;
                _snapPieces += snapped;
                _tiltPieces += tilted;
                _boatsWater += boatsW;
                _boatsBeach += boatsB;
                _modelAsked[Math.Max(0, Math.Min(MapVillageData.ModelCount - 1, s.R.Model))]++;
                _modelMade[k.Model]++;
                s.Root = root;
                s.Z = zc;
                s.Cx = g.Cx;
                s.Cy = g.Cy;
                s.Deg = yaw * 180f / (float)Math.PI;
                s.Placed = true;
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

        /// <summary>Bezpiecznik ladu (raz na mape): bramy do 40 wsi gry (Settlement.GatePosition - zawsze na ladzie) musza w wiekszosci lezec na
        /// scianie ladowej siatki (GetFaceIndex z IsOnLand = true). Nie - siatka nie odpowiada tak, jak zakladamy: lad tylko wedlug wysokosci nad
        /// morzem (bez tego kazdy budynek bylby "w wodzie" i zdjety). Wynik w glownym logu.</summary>
        private bool FacesOk(Geo g)
        {
            if (_faceTest >= 0) return _faceTest == 1;
            int tried = 0, land = 0;
            try
            {
                foreach (var d in _districts)
                {
                    if (d.S == null || !d.S.IsVillage) continue;
                    Vec2 gp = d.S.GatePosition.ToVec2();
                    tried++;
                    if (MapVillageData.IsLandGroup(g.Group(gp.x, gp.y))) land++;
                    if (tried >= 40) break;
                }
                _faceTest = tried == 0 || land * 2 >= tried ? 1 : 0;
            }
            catch (Exception e)
            {
                _faceTest = 0;
                Log.Error("MapVillagesView.FacesOk - lad tylko wedlug wysokosci", e);
            }
            Log.Info("Wioski: sprawdzian siatki nawigacyjnej - bramy wsi gry " + tried + ", na scianie ladowej " + land
                     + (_faceTest == 1 ? " - lad pod budynkami wedlug scian siatki" : " - SIATKA NIE PASUJE: lad tylko wedlug wysokosci nad morzem")
                     + (_seaLevel > MapVillageData.NoSea * 0.5f ? " (morze " + F2(_seaLevel) + ")" : " (poziom morza nieznany)") + ".");
            return _faceTest == 1;
        }

        /// <summary>Poprawka 4 (zdjecie fishing-widows-horn: osada na skalach w wodzie): obrys z pliku (x 0.8, 9 punktow) nie caly na ladzie -
        /// srodek obrazka na najblizsze miejsce z obrysem na ladzie (MapVillageData.FindLandSpot, do 6 jedn.); brak - zostaje (budynki sprawdzi
        /// OnLand). Proba: 9 wiosek pliku v4 ma srodek pod poziomem morza na scianie siatki Plain (Widow's Horn: teren 5.21).</summary>
        private void ToLand(Slot s, Geo g, StringBuilder notes)
        {
            Func<float, float, bool> land = g.Land;
            if (MapVillageData.FootprintLand(land, g.Cx, g.Cy, s.R.FrontDeg, s.R.HalfLen, s.R.HalfWid, 0.8f)) return;
            float h0 = g.H(g.Cx, g.Cy);
            int g0 = g.Group(g.Cx, g.Cy);
            notes.Append("; obrys z pliku nie na ladzie (srodek: teren ").Append(F2(h0)).Append(", sciana typu ").Append(g0)
                 .Append(MapVillageData.IsLandGroup(g0) ? "" : " - bez ladu")
                 .Append(g.Sea > MapVillageData.NoSea * 0.5f ? ", morze " + F2(g.Sea) : "").Append(')');
            float nx, ny;
            // recenzja: najpierw miejsce z odstepem od innych wiosek (NeighborMinDist); brak - najblizszy lad mimo sasiada (lad wazniejszy niz odstep)
            bool near = false;
            if (!MapVillageData.FindLandSpotWhere(land, (x, y) => FarFromOthers(s, x, y), g.Cx, g.Cy, s.R.FrontDeg, s.R.HalfLen, s.R.HalfWid,
                                                  MapVillageData.LandSearchR, out nx, out ny))
            {
                if (!MapVillageData.FindLandSpot(land, g.Cx, g.Cy, s.R.FrontDeg, s.R.HalfLen, s.R.HalfWid, MapVillageData.LandSearchR, out nx, out ny))
                {
                    _landMiss++;
                    notes.Append(" - brak ladu w ").Append(F2(MapVillageData.LandSearchR)).Append(" jedn., srodek zostaje");
                    return;
                }
                near = true;
                _landNear++;
            }
            float d = (float)Math.Sqrt((nx - g.Cx) * (nx - g.Cx) + (ny - g.Cy) * (ny - g.Cy));
            notes.Append(" -> srodek przeniesiony o ").Append(F2(d)).Append(" jedn. na lad (").Append(F2(nx)).Append(", ").Append(F2(ny)).Append(')')
                 .Append(near ? " BLIZEJ niz " + F2(MapVillageData.NeighborMinDist) + " jedn. od innej wioski (dalej lad z odstepem brak)" : "");
            g.Cx = nx;
            g.Cy = ny;
            _landMoved++;
        }

        /// <summary>Recenzja wygladu 3: (x, y) co najmniej MapVillageData.NeighborMinDist od kazdej innej wioski - od jej miejsca z pliku i, gdy
        /// juz postawiona, od jej srodka po przesunieciu (Slot.Cx / Cy). Przeniesienie na lad (do 6 jedn.) i ku wodzie (do 3.5 jedn.) stawialo
        /// wioski przy sasiadach (proba na mapie ROT: 17 par blizej niz 4, Gallowsmill 2.1 od Blackholt; plik trzyma co najmniej 4.5).
        /// Sam odczyt pol slotow z siatki wyszukiwania (komorki 50 jedn.) - zadnych wywolan silnika.</summary>
        private bool FarFromOthers(Slot s, float x, float y)
        {
            float m2 = MapVillageData.NeighborMinDist * MapVillageData.NeighborMinDist;
            int bx = (int)Math.Floor(x / Cell), by = (int)Math.Floor(y / Cell);
            for (int ix = -1; ix <= 1; ix++)
                for (int iy = -1; iy <= 1; iy++)
                {
                    List<Slot> l;
                    if (!_grid.TryGetValue(((long)(bx + ix) << 32) ^ (uint)(by + iy), out l)) continue;
                    for (int j = 0; j < l.Count; j++)
                    {
                        var o = l[j];
                        if (o == s || o.R == null) continue;
                        float dx = o.R.X - x, dy = o.R.Y - y;
                        if (dx * dx + dy * dy < m2) return false;
                        if (!o.Placed) continue;
                        dx = o.Cx - x; dy = o.Cy - y;
                        if (dx * dx + dy * dy < m2) return false;
                    }
                }
            return true;
        }

        /// <summary>Pomost i lodzie na rzece mniejsze (pomost x 0.6, lodz x 0.8; rzeka na mapie ma ok. 1 jedn. szerokosci, pomost rot_dock1
        /// ok. 1.4 jedn. - siegalby drugiego brzegu); srodek BB zostaje.</summary>
        private static void ShrinkForRiver(Kit k, List<int> vis, float[] px, float[] py, Mat3[] rot, float[] cx0, float[] cy0, float[] rxj, float[] ryj,
            float[] bot, float[] top)
        {
            for (int j = 0; j < vis.Count; j++)
            {
                Piece p = k.Pieces[vis[j]];
                if (p.Ground != MapVillageData.GroundEnd && p.Ground != MapVillageData.GroundBeach) continue;
                float f = p.Ground == MapVillageData.GroundEnd ? 0.6f : 0.8f;
                float bx = px[j] + cx0[j], by = py[j] + cy0[j];
                rot[j].ApplyScaleLocal(f);
                cx0[j] *= f; cy0[j] *= f; rxj[j] *= f; ryj[j] *= f; bot[j] *= f; top[j] *= f;
                px[j] = bx - cx0[j];
                py[j] = by - cy0[j];
            }
        }

        /// <summary>Punkt kotwicy elementu przy wodzie (Y, jedn. wzoru): mlyn - srodek kola (albo przod mlyna bez kola); pomost / lodz - koniec od ladu.</summary>
        private static float AnchorY(Piece p, int j, float[] py, float[] cy0, float[] ryj, float[] ady)
        {
            if (p.Ground == MapVillageData.GroundLand) return py[j] + ady[j];
            return py[j] + cy0[j] - ryj[j];
        }

        private static float AnchorX(Piece p, int j, float[] px, float[] cx0, float[] adx)
        {
            if (p.Ground == MapVillageData.GroundLand) return px[j] + adx[j];
            return px[j] + cx0[j];
        }

        /// <summary>Mlyn z woda na polnoc: obrot o +-50 st. wokol srodka kola (jedn. wzoru; kolo zostaje nad woda), gdy srodek budynku i jego tyl
        /// stoja nad woda (teren wyzej niz woda + 0.05); kierunek z ziarna wioski, drugi gdy pierwszy w wodzie. Zwraca kat (0 = bez obrotu).</summary>
        private static float TurnMill(Slot s, Geo g, int j, float[] px, float[] py, Mat3[] rot, float[] cx0, float[] cy0, float[] rxj, float[] ryj,
            float[] adx, float[] ady, float wz)
        {
            float wx = px[j] + adx[j], wy = py[j] + ady[j];   // srodek kola - zostaje
            float lim = float.IsNaN(wz) ? float.MinValue : wz + 0.05f;
            bool first = (MapVillageData.Fnv(s.R.Uid + ":mlyn") & 1u) == 0;
            foreach (float deg in first ? new[] { 50f, -50f } : new[] { -50f, 50f })
            {
                float a = deg * (float)Math.PI / 180f, c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                float nadx = c * adx[j] - sn * ady[j], nady = sn * adx[j] + c * ady[j];
                float ncx = c * cx0[j] - sn * cy0[j], ncy = sn * cx0[j] + c * cy0[j];
                float ox = wx - nadx, oy = wy - nady, bx = ox + ncx, by = oy + ncy;
                float tx = bx + (bx - wx) * 0.6f, ty = by + (by - wy) * 0.6f;   // tyl budynku (od kola za srodek)
                if (g.HK(bx, by) < lim || g.HK(tx, ty) < lim) continue;
                Mat3 rz = Mat3.Identity;
                rz.RotateAboutUp(a);
                rot[j] = rz.TransformToParent(in rot[j]);
                adx[j] = nadx; ady[j] = nady; cx0[j] = ncx; cy0[j] = ncy;
                float rr = Math.Max(rxj[j], ryj[j]);
                rxj[j] = rr; ryj[j] = rr;
                px[j] = ox; py[j] = oy;
                return deg;
            }
            return 0f;
        }

        /// <summary>O ile kotwica staje za brzegiem wody (jedn. mapy, + w wode): mlyn - 0.6 polgrubosci kola (kolo nad woda; bez kola przod 0.05
        /// przed brzegiem); pomost - ladowy koniec 0.05 na ladzie (morze) albo pol pomostu na ladzie (rzeka); lodz - cala na wodzie (+0.05).</summary>
        private static float AnchorOutW(Kit k, Piece p, int j, bool sea, float[] ryj, Geo g)
        {
            float sy = Math.Max(0.05f, g.Sy);
            if (p.Ground == MapVillageData.GroundLand) return k.WheelR > 0f ? k.AnchorOut * sy : -0.05f;
            if (p.Ground == MapVillageData.GroundEnd) return sea ? -0.05f : -ryj[j] * sy;
            return 0.05f;
        }

        /// <summary>Elementy przy wodzie na brzegu SWOJEJ linii (+Y = do wody): promien od punktu 1.2 jedn. za kotwica elementu
        /// (MapVillageData.ShoreRay co 0.1 jedn.), kotwica staje AnchorOutW za brzegiem; przesuniecie najwyzej 1 jedn.; wz[j] = poziom wody
        /// przy elemencie (brak brzegu na jego linii - poziom z brzegu obrazka).</summary>
        private void PlaceAtWater(Kit k, Geo g, List<int> vis, float[] px, float[] py, bool[] alive, float[] cx0, float[] cy0, float[] ryj, float[] wz,
            MapVillageData.ShoreHit shore, int ja, float[] adx, float[] ady)
        {
            float fx = -g.Sa, fy = g.Ca;   // front (+Y lokalne) w swiecie
            float sy = Math.Max(0.05f, g.Sy);
            const float back = 1.2f;
            var order = new List<int>();
            if (ja >= 0) order.Add(ja);
            for (int j = 0; j < vis.Count; j++) if (j != ja) order.Add(j);
            foreach (int j in order)
            {
                if (!alive[j]) continue;
                Piece p = k.Pieces[vis[j]];
                if (!p.WaterSide) continue;
                wz[j] = shore.WaterZ;
                float ax = AnchorX(p, j, px, cx0, adx), ay = AnchorY(p, j, py, cy0, ryj, ady);
                float sx = g.WX(ax, ay - back / sy), syw = g.WY(ax, ay - back / sy);
                MapVillageData.ShoreHit hit = MapVillageData.ShoreRay(g.Group, g.H, g.Sea, sx, syw, fx, fy, back + 2.0f, 0.2f);
                float edge;
                if (hit != null) { edge = hit.Edge; wz[j] = hit.WaterZ; }
                else
                {
                    // start nie na ladzie (szeroki pas przy rzece) albo brzeg dalej: pierwszy punkt z terenem pod woda z brzegu obrazka, od kotwicy
                    float wa = MapVillageData.WaterAlong(g.H, shore.WaterZ, g.WX(ax, ay), g.WY(ax, ay), fx, fy, 2.0f, 0.1f);
                    if (wa < 0f) continue;   // woda dalej niz 2 jedn. przed kotwica: zostaje po przesunieciu obrazka
                    edge = back + wa;
                }
                float d = (edge + AnchorOutW(k, p, j, shore.Sea, ryj, g) - back) / sy;
                float lim = 1.0f / sy;
                if (d > lim) d = lim;
                if (d < -lim) d = -lim;
                py[j] += d;
                if (p.Ground == MapVillageData.GroundEnd) _pierShore++;
            }
        }

        /// <summary>Poprawka 4: kazdy budynek (bez elementow przy wodzie i ikon wsi) caly na ladzie (MapVillageData.BoxLand: srodek i 4 rogi BB);
        /// gdy nie - nowe miejsce (MapVillageData.PushToLand: do srodka kepy, przy wodzie od wody), bez kolizji z innymi; brak miejsca: budynek
        /// niekonieczny zdjety (zostaje najmniej minKeep), niezbedny zostaje (licznik).</summary>
        private void OnLand(Kit k, Geo g, List<int> vis, float[] px, float[] py, bool[] alive, int minKeep, float[] cx0, float[] cy0, float[] rxj, float[] ryj,
            bool water, StringBuilder notes)
        {
            int n = vis.Count, kept = 0, pushed = 0, dropped = 0, stuck = 0;
            for (int j = 0; j < n; j++) if (alive[j]) kept++;
            Func<float, float, bool> landK = (kx, ky) => g.Land(g.WX(kx, ky), g.WY(kx, ky));
            for (int j = 0; j < n; j++)
            {
                if (!alive[j]) continue;
                Piece p = k.Pieces[vis[j]];
                if (p.WaterSide || p.Ground == MapVillageData.GroundTilt) continue;   // mlyn / pomost / lodzie - wlasna regula; ikona wsi - ToLand
                float cx = px[j] + cx0[j], cy = py[j] + cy0[j];
                if (MapVillageData.BoxLand(landK, cx, cy, rxj[j], ryj[j])) continue;
                var ox = new List<float>();
                var oy = new List<float>();
                var orr = new List<float>();
                for (int q = 0; q < n; q++)
                {
                    if (q == j || !alive[q]) continue;
                    ox.Add(px[q] + cx0[q]); oy.Add(py[q] + cy0[q]); orr.Add(Math.Max(rxj[q], ryj[q]));
                }
                float tx = 0f, ty = water ? -1.5f / Math.Max(0.05f, g.Sy) : 0f;
                float nx, ny;
                if (MapVillageData.PushToLand(landK, cx, cy, rxj[j], ryj[j], tx, ty, ox, oy, orr, out nx, out ny))
                {
                    px[j] += nx - cx;
                    py[j] += ny - cy;
                    pushed++;
                    continue;
                }
                if (!p.Essential && kept > minKeep) { alive[j] = false; kept--; dropped++; continue; }
                stuck++;
            }
            _landPushed += pushed;
            _landDropped += dropped;
            _landStuck += stuck;
            if (pushed + dropped + stuck > 0)
                notes.Append("; budynki nie na ladzie: przesuniete ").Append(pushed).Append(", zdjete ").Append(dropped).Append(", niezbedne zostaly ").Append(stuck);
        }

        /// <summary>Poprawka 1 - log: korekta wysokosci domu wzoru wobec dawnej (origin = najnizszy grunt + wysokosc z ramki prefabu wsi-matki,
        /// liczona od KORZENIA matki); min / max / srednia w podsumowaniu, kazda siatka domu raz w wioski.log (najwyzej 40).</summary>
        private void NoteHeight(Piece p, Slot s, float corr, float bottom, float sz)
        {
            _corrN++;
            _corrSum += corr;
            if (corr < _corrMin) _corrMin = corr;
            if (corr > _corrMax) _corrMax = corr;
            if (_hLogged.Count >= 40 || !_hLogged.Add(p.Name)) return;
            float oldBottom = (p.ZOff + bottom) * sz;   // dawniej: dol BB wzgledem najnizszego gruntu (jedn. mapy)
            Log.Info("Wioski: drzewo wysokosci - siatka " + p.Name + " (pierwsza: okreg " + s.D.Id + ", wioska '" + s.R.Name + "'): dol BB w ukladzie encji "
                     + F2(bottom) + " jedn. wzoru (ze skala siatki), wysokosc z ramki prefabu wsi-matki " + F2(p.ZOff) + " -> dawniej dol "
                     + (oldBottom < 0f ? F2(-oldBottom) + " jedn. mapy POD gruntem" : F2(oldBottom) + " jedn. mapy nad gruntem")
                     + "; teraz dol BB " + F2(MapVillageData.Sink(Math.Max(0.01f, (p.Top - p.Bottom) * sz))) + " pod najnizszym gruntem pod obrysem (korekta "
                     + (corr >= 0f ? "+" : "") + F2(corr) + ").");
        }

        /// <summary>Mlyn / rybacy - do wioski.log (15 pierwszych): brzeg wody, obrot i przesuniecie obrazka, kolo mlyna (siatka, gdzie stoi, nad
        /// woda / ladem, czy dotyka wody, od strony kamery), pomost i lodzie; liczniki kola w podsumowaniu.</summary>
        private void WaterNote(Slot s, Kit k, Geo g, List<int> vis, float[] px, float[] py, bool[] alive, float[] cx0, float[] cy0, float[] ryj, float[] wz,
            float[] orgW, MapVillageData.ShoreHit shore, int ja, float shift, int boatsW, int boatsB, StringBuilder notes, float[] adx, float[] ady, float millTurn)
        {
            double az = Math.Atan2(shore.Dx, shore.Dy) * 180.0 / Math.PI;
            if (az < 0) az += 360.0;
            // kamera mapy patrzy na polnoc: woda na S / E / W - kolo / pomost przed budynkiem; mlyn obrocony wokol kola (6b) - kolo obok budynku
            bool camSide = shore.Dy <= 0.35f || millTurn != 0f;
            var sb = new StringBuilder();
            sb.Append("Wioski: drzewo wody ").Append(s.R.Uid).Append(" '").Append(s.R.Name).Append("' (").Append(MapVillageData.ModelName(k.Model))
              .Append(", styl ").Append(k.Style).Append(", miejsce ").Append(s.R.Kind).Append("): brzeg ").Append(shore.Sea ? "morza" : "rzeki / jeziora")
              .Append(" w kierunku ").Append(az.ToString("0", CultureInfo.InvariantCulture)).Append(" st. (0 = N), woda ").Append(F2(shore.Edge))
              .Append(" jedn. od srodka (ostatni lad ").Append(F2(shore.LastLand)).Append("), poziom wody ").Append(F2(shore.WaterZ))
              .Append("; obrazek frontem do wody, przesuniety ku wodzie o ").Append(F2(shift)).Append(notes);
            if (k.Model == MapVillageData.ModelMill)
            {
                if (ja < 0 || !alive[ja]) sb.Append("; mlyn NIE STOI na tym poziomie");
                else if (!(k.WheelR > 0f)) sb.Append("; kolo: siatki ").Append(MapVillageData.WatermillWheel).Append(" BRAK w grze (mlyn bez kola)");
                else
                {
                    Piece p = k.Pieces[vis[ja]];
                    float kx = AnchorX(p, ja, px, cx0, adx), ky = AnchorY(p, ja, py, cy0, ryj, ady);
                    float wx = g.WX(kx, ky), wy = g.WY(kx, ky), hw = g.H(wx, wy), wr = k.WheelR * g.Sz, zw = orgW[ja];
                    bool overWater = !g.Land(wx, wy) && hw <= wz[ja] + 0.05f;
                    bool touch = zw - wr <= wz[ja] + 0.02f;
                    if (overWater) _wheelWater++; else _wheelDry++;
                    if (touch) _wheelTouch++;
                    if (camSide) _wheelCam++;
                    sb.Append("; kolo: siatka ").Append(MapVillageData.WatermillWheel).Append(" jest, srodek kola (").Append(F2(wx)).Append(", ").Append(F2(wy))
                      .Append(", ").Append(F2(zw)).Append("), promien ").Append(F2(wr)).Append(", teren pod kolem ").Append(F2(hw)).Append(", woda ").Append(F2(wz[ja]))
                      .Append(overWater ? " - NAD WODA" : " - nad ladem / brzegiem").Append(touch ? ", dotyka wody" : ", nad woda o " + F2(zw - wr - wz[ja]))
                      .Append(millTurn != 0f ? "; woda na polnoc - mlyn obrocony o " + millTurn.ToString("0", CultureInfo.InvariantCulture) + " st. wokol kola, kolo obok mlyna od strony kamery"
                              : camSide ? "; od strony kamery" : "; woda na polnoc - kolo za mlynem (kamera patrzy na polnoc; obrot wokol kola: budynek nie mial gdzie stanac)");
                }
            }
            else
            {
                int pier = 0, pierOk = 0;
                for (int j = 0; j < vis.Count; j++)
                {
                    if (!alive[j] || k.Pieces[vis[j]].Ground != MapVillageData.GroundEnd) continue;
                    pier++;
                    float lx = px[j] + cx0[j], ly = py[j] + cy0[j] - ryj[j] * 0.8f;
                    if (g.Land(g.WX(lx, ly), g.WY(lx, ly)) || g.HK(lx, ly) >= wz[j] - 0.01f) pierOk++;   // na ladzie albo na brzegu nad woda
                }
                sb.Append("; pomost ").Append(pier > 0 ? (pierOk > 0 ? "od brzegu (ladowy koniec na brzegu nad woda)" : "ladowy koniec POD WODA") : "brak na tym poziomie")
                  .Append(", lodzie na wodzie ").Append(boatsW).Append(", na brzegu ").Append(boatsB).Append(camSide ? "; od strony kamery" : "; woda na polnoc");
            }
            if (_diagWater < 15) { _diagWater++; Log.Info(sb.Append('.').ToString()); }
        }

        /// <summary>Encja elementu: pusta encja + kopie siatek (jak namiot partii: CreateEmpty + AddMultiMesh + SetFrame + AddChild), flagi jak dotad.</summary>
        private static GameEntity MakeEntity(Scene scene, GameEntity parent, Piece p, MatrixFrame f, bool noSeason, ref int meshes)
        {
            GameEntity e = GameEntity.CreateEmpty(scene, false, false, false);
            if (e == (GameEntity)null) return null;
            for (int m = 0; m < p.Meshes.Length; m++)
            {
                MetaMesh copy = p.Meshes[m].CreateCopy();
                if (copy == null || !copy.IsValid) copy = MetaMesh.GetCopy(p.Meshes[m].GetName(), false, true);
                if (copy != null && copy.IsValid) { e.AddMultiMesh(copy, true); meshes++; }
            }
            e.SetFrame(ref f, true);                 // jak namiot partii: ramka, potem AddChild bez przeliczania = ramka lokalna
            parent.AddChild(e, false);
            // recenzja 08.10: flaga "bez sniegu pory roku" na KAZDEJ encji obrazka, gdy ma ja matka (zima ROT: jednolita szara brylka)
            if (p.NoSeason || noSeason) e.EntityFlags = e.EntityFlags | EntityFlags.NotAffectedBySeason;
            e.EntityFlags = e.EntityFlags | EntityFlags.DoNotTick;
            e.SetReadyToRender(true);
            return e;
        }

        private static string SafeName(string uid)
        {
            var sb = new StringBuilder(uid.Length);
            foreach (char ch in uid) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            return sb.ToString();
        }

        /// <summary>Uklad obrazka na mapie: jedn. wzoru (X wzdluz ulicy, Y do frontu) -> swiat; grunt z MapSceneWrapper.GetTerrainHeightAndNormal
        /// (sam teren, jak dotad). Wyglad 3: typ sciany siatki nawigacyjnej pod punktem - MapSceneWrapper.GetFaceIndex (to samo, co gra wola
        /// dla kazdej partii przez CampaignVec2.Face; tu z IsOnLand = true, jak partia ladowa) i PathFaceRecord.FaceGroupIndex = TerrainType
        /// (MapScene.GetFaceTerrainType robi tylko to rzutowanie); lad = sciana ladowa i teren nad poziomem morza (Sea).</summary>
        private sealed class Geo
        {
            public float Ca, Sa, Sx, Sy, Sz, Cx, Cy;
            public float Sea = MapVillageData.NoSea;
            public bool Faces = true;         // sciany siatki przeszly sprawdzian (FacesOk); nie - lad tylko wedlug wysokosci nad morzem
            public TaleWorlds.CampaignSystem.Map.IMapScene Ms;
            public int Calls;
            public float WX(float kx, float ky) { return Cx + Ca * kx * Sx - Sa * ky * Sy; }
            public float WY(float kx, float ky) { return Cy + Sa * kx * Sx + Ca * ky * Sy; }
            public float H(float wx, float wy)
            {
                float z;
                Vec3 nn;
                Ms.GetTerrainHeightAndNormal(new Vec2(wx, wy), out z, out nn);
                Calls++;
                return z;
            }
            public float HK(float kx, float ky) { return H(WX(kx, ky), WY(kx, ky)); }
            /// <summary>Typ sciany siatki nawigacyjnej (TerrainType) pod punktem swiata; -1 = poza siatka.</summary>
            public int Group(float wx, float wy)
            {
                if (!Faces) return 1;   // siatka nie przeszla sprawdzianu: kazdy punkt "Plain", lad wedlug wysokosci nad morzem
                var cv = new CampaignVec2(new Vec2(wx, wy), true);
                PathFaceRecord f = Ms.GetFaceIndex(in cv);
                Calls++;
                return f.IsValid() ? f.FaceGroupIndex : -1;
            }
            /// <summary>Lad pod punktem swiata (MapVillageData.LandAt); wysokosc liczona tylko na scianie ladowej, gdy znany poziom morza.</summary>
            public bool Land(float wx, float wy)
            {
                if (!MapVillageData.IsLandGroup(Group(wx, wy))) return false;
                return !(Sea > MapVillageData.NoSea * 0.5f) || H(wx, wy) > Sea + 0.02f;
            }
        }

        /// <summary>Obrot elementu pochylony do normalnej terenu (normalna swiata -> uklad korzenia: obrot o -yaw), z zachowaniem skali elementu.</summary>
        private static Mat3 Tilted(Mat3 rot, Vec3 nWorld, Geo g)
        {
            Vec3 u = new Vec3(g.Ca * nWorld.x + g.Sa * nWorld.y, -g.Sa * nWorld.x + g.Ca * nWorld.y, nWorld.z);
            if (u.Length < 0.5f || u.z < 0.5f) return rot;
            u.Normalize();
            Vec3 sc = rot.GetScaleVector();
            Vec3 s0 = rot.s;
            s0 = new Vec3(s0.x, s0.y, 0f);
            if (s0.Length < 1e-4f) return rot;
            s0.Normalize();
            Vec3 f = Vec3.CrossProduct(u, s0);
            f.Normalize();
            Vec3 side = Vec3.CrossProduct(f, u);
            side.Normalize();
            Mat3 r = new Mat3(in side, in f, in u);
            r.ApplyScaleLocal(in sc);
            return r;
        }

        /// <summary>Ile budynkow zostaje najmniej przy zdejmowaniu ze stoku: kepa "village" - tyle co poziom 1 (VillageWant), inne obrazki -
        /// elementy niezbedne (mlyn, stodola, pomost...) + 1 dom.</summary>
        private static int MinKeep(Kit k, List<int> vis)
        {
            if (k.ThinN > 0) return Math.Min(vis.Count, MapVillageData.VillageWant(k.ThinN, 1));
            int ess = 0;
            foreach (int i in vis) if (k.Pieces[i].Essential) ess++;
            return Math.Min(vis.Count, ess + 1);
        }

        /// <summary>Poprawka 1 (zdjecie crop-sb-dach: domy na brzegu zapadniete po dach): rozrzut gruntu pod budynkami (srodki elementow, bez
        /// elementow przy wodzie i pochylonych ikon). Gdy wiekszy niz prog (MapVillageData.SpreadMax ok. 0.9 wysokosci domu): (a) przesuniecie
        /// srodka w granicach obrysu z pliku (half_len / half_wid) na najrowniejsze miejsce, (b) scisniecie kepy do 0.75, (c) tylko urwisko /
        /// brzeg (dalej &gt; 2 x prog): zdjecie budynkow z gruntem najdalej od mediany (MapVillageData.DropOutliers, nigdy niezbednych, zostaje
        /// MinKeep). Wszystko liczone w logu.</summary>
        private void Settle(Slot s, Kit k, Geo g, List<int> vis, float[] px, float[] py, bool[] alive, int minKeep)
        {
            int n = vis.Count;
            var idx = new List<int>();
            for (int j = 0; j < n; j++)
            {
                Piece p = k.Pieces[vis[j]];
                if (p.WaterSide || p.Ground == MapVillageData.GroundTilt || p.Ground == MapVillageData.GroundEnd || p.Ground == MapVillageData.GroundBeach) continue;
                idx.Add(j);
            }
            if (idx.Count < 2) return;
            float max = MapVillageData.SpreadMax(k.H * g.Sz);
            var h = new List<float>();
            float sp0 = SpreadAt(k, g, vis, px, py, alive, idx, g.Cx, g.Cy, h);
            if (sp0 <= max) return;
            _slopeVillages++;
            float bestSp = sp0, bx = g.Cx, by = g.Cy, bdx = 0f, bdy = 0f, cx0 = g.Cx, cy0 = g.Cy;
            bool water = k.HasWater, nearHit = false;
            foreach (float[] c in MapVillageData.ShiftCandidates(s.R.HalfLen, s.R.HalfWid, water))
            {
                // przesuniecie w ukladzie obrazka (jedn. mapy) -> swiat; wyglad 3: od srodka po ToLand / przesunieciu ku wodzie, tylko na lad
                // i (recenzja) nie blizej innej wioski niz NeighborMinDist
                float wx = cx0 + g.Ca * c[0] - g.Sa * c[1], wy = cy0 + g.Sa * c[0] + g.Ca * c[1];
                if (!g.Land(wx, wy)) continue;
                if (!FarFromOthers(s, wx, wy)) { nearHit = true; continue; }
                float sp = SpreadAt(k, g, vis, px, py, alive, idx, wx, wy, null);
                if (sp < bestSp - 0.02f) { bestSp = sp; bx = wx; by = wy; bdx = c[0]; bdy = c[1]; }
                if (bestSp <= max) break;
            }
            if (nearHit) _shiftNear++;
            bool shifted = bx != g.Cx || by != g.Cy;
            if (shifted) { g.Cx = bx; g.Cy = by; _slopeShift++; }
            bool squeezed = false;
            int dropped = 0;
            if (bestSp > max)
            {
                // scisniecie kepy (bez elementow przy wodzie) do 0.75 wokol jej srodka
                float mx = 0f, my = 0f;
                foreach (int j in idx) { mx += px[j]; my += py[j]; }
                mx /= idx.Count; my /= idx.Count;
                var ox = (float[])px.Clone();
                var oy = (float[])py.Clone();
                foreach (int j in idx) { px[j] = mx + (px[j] - mx) * 0.75f; py[j] = my + (py[j] - my) * 0.75f; }
                float sp = SpreadAt(k, g, vis, px, py, alive, idx, g.Cx, g.Cy, null);
                if (sp < bestSp - 0.02f) { bestSp = sp; squeezed = true; _slopeSqueeze++; }
                else { Array.Copy(ox, px, ox.Length); Array.Copy(oy, py, oy.Length); }
            }
            if (bestSp > 2f * max)
            {
                // urwisko / brzeg (rozrzut > 2 x prog): zdjecie budynkow z gruntem najdalej od mediany (dom w korycie rzeki, na skarpie), nigdy
                // niezbednych, az rozrzut <= 2 x prog. Rowny stok (rozrzut <= 2 x prog) nic nie zdejmuje - kazdy dom i tak stoi na swoim gruncie.
                h.Clear();
                SpreadAt(k, g, vis, px, py, alive, idx, g.Cx, g.Cy, h);
                var ess = new List<bool>();
                foreach (int j in idx) ess.Add(k.Pieces[vis[j]].Essential);
                int aliveNow = 0;
                for (int j = 0; j < n; j++) if (alive[j]) aliveNow++;
                int keepAmong = Math.Max(1, idx.Count - Math.Max(0, aliveNow - minKeep));
                bool[] keep = MapVillageData.DropOutliers(h, ess, keepAmong, 2f * max);
                for (int q = 0; q < idx.Count; q++) if (!keep[q] && alive[idx[q]]) { alive[idx[q]] = false; dropped++; }
                _slopeDropped += dropped;
                bestSp = SpreadAt(k, g, vis, px, py, alive, idx, g.Cx, g.Cy, null);
            }
            if (bestSp > max) _slopeLeft++;
            if (_diagSlope < 15)
            {
                _diagSlope++;
                Log.Info("Wioski: drzewo terenu " + s.R.Uid + " '" + s.R.Name + "' (" + MapVillageData.ModelName(k.Model) + ", poziom " + s.R.Level
                         + "): rozrzut gruntu pod budynkami " + F2(sp0) + " > prog " + F2(max) + " -> " + F2(bestSp)
                         + (shifted ? "; przesuniete w obrysie o " + F2(bdx) + " wzdluz ulicy, " + F2(bdy) + " do frontu" : "; bez przesuniecia")
                         + (squeezed ? "; kepa scisnieta do 0.75" : "") + (dropped > 0 ? "; zdjete budynki " + dropped : "")
                         + (bestSp > max ? "; DALEJ NIEROWNO (kazdy budynek i tak na swoim gruncie)" : "") + ".");
            }
        }

        /// <summary>Rozrzut gruntu w srodkach elementow idx przy srodku obrazka (cx, cy); h (gdy nie null) dostaje wysokosci wedlug idx.</summary>
        private static float SpreadAt(Kit k, Geo g, List<int> vis, float[] px, float[] py, bool[] alive, List<int> idx, float cx, float cy, List<float> h)
        {
            float ox = g.Cx, oy = g.Cy;
            g.Cx = cx; g.Cy = cy;
            float lo = float.MaxValue, hi = float.MinValue;
            try
            {
                foreach (int j in idx)
                {
                    Piece p = k.Pieces[vis[j]];
                    float z = g.HK(px[j] + p.Cx0, py[j] + p.Cy0);
                    if (h != null) h.Add(z);
                    if (!alive[j]) continue;
                    if (z < lo) lo = z;
                    if (z > hi) hi = z;
                }
            }
            finally { g.Cx = ox; g.Cy = oy; }
            return hi >= lo ? hi - lo : 0f;
        }

        // ---------- v4: wzor obrazka na (okreg, model) - wspolny dla wiosek okregu, budowany raz ----------
        private Kit KitFor(District d, Template t, int model)
        {
            if (model < 0 || model >= MapVillageData.ModelCount) model = MapVillageData.ModelVillage;
            if (d.Kits == null) d.Kits = new Kit[MapVillageData.ModelCount];
            Kit k = d.Kits[model];
            if (k != null) return k;
            k = BuildKit(d, t, model);
            d.Kits[model] = k;
            return k;
        }

        /// <summary>Wzor obrazka: styl krainy z domow wzoru wsi-matki (andal / fm) albo z ikony wsi (kultura Calradii / Essos), potem przepis
        /// modelu (MapVillageData.Recipe). Brak niezbednej siatki = zapas: windmill -> farm -> village, mill / fishing / farm / granary -> village
        /// (stodola granary / farm ma najpierw swoj zapas: najwieksza szopa / dom stylu x 1.3).</summary>
        private Kit BuildKit(District d, Template t, int asked)
        {
            float unitScale;
            string family;
            List<HouseUnit> units = HouseUnits(t, out unitScale, out family);
            bool snow = false;
            if (units != null) foreach (var u in units) if (u.Name.EndsWith("_snow", StringComparison.Ordinal)) snow = true;
            string style = family ?? IconStyle(t, d);
            MapVillageData.StyleKit st = MapVillageData.StyleFor(style, snow);
            float H = MapVillageData.IconHouseH;
            if (units != null)
            {
                var ex = new List<float>();
                foreach (var u in units) if (u.Ext > 0.01f) ex.Add(u.Ext);
                if (ex.Count > 0) { ex.Sort(); H = ex[ex.Count / 2]; }
            }
            int m = asked;
            Kit k = null;
            for (int guard = 0; guard < 4; guard++)
            {
                k = new Kit { Asked = asked, Model = m, Style = st.Name, H = H, Units = units };
                string why = null;
                bool ok;
                try
                {
                    ok = m == MapVillageData.ModelVillage ? FillVillage(k, d, t, units, st, unitScale)
                                                          : FillModel(k, d, t, units, st, unitScale, m, out why);
                }
                catch (Exception e)
                {
                    ok = false;
                    why = "blad " + e.GetType().Name;
                    _stTemplate++;
                    if (_stTemplate <= 5 || _stTemplate % 100 == 0) Log.Error("MapVillagesView.BuildKit " + d.Id + " " + MapVillageData.ModelName(m) + " (potkniecie " + _stTemplate + ")", e);
                }
                if (ok && k.Pieces.Count > 0)
                {
                    _kitsBuilt++;
                    LogKit(k, d);
                    return k;
                }
                k.Why = why ?? "brak elementow";
                if (m == MapVillageData.ModelVillage) return k;   // bez elementow - Create rzuci "brak wzoru obrazka"
                int next = m == MapVillageData.ModelWindmill ? MapVillageData.ModelFarm : MapVillageData.ModelVillage;
                string key = MapVillageData.ModelName(m) + "->" + MapVillageData.ModelName(next);
                int c;
                _kitFallback.TryGetValue(key, out c);
                _kitFallback[key] = c + 1;
                if (c < 3)
                    Log.Info("Wioski: drzewo obrazkow - okreg " + d.Id + " (styl " + st.Name + "): obrazek " + MapVillageData.ModelName(m) + " -> "
                             + MapVillageData.ModelName(next) + " (" + k.Why + ")" + (c == 2 ? "; dalsze takie zapasy tylko w liczniku podsumowania." : "."));
                m = next;
            }
            return k;
        }

        /// <summary>Domy wsi-matki z wzoru (rodzina andal / fm; ten sam wyglad na 1/2/3): kazda encja z siatka poziomu 3 w ramce zlozonej
        /// wzgledem korzenia matki, obrocona tak, zeby dluga os domow szla wzdluz ulicy (lokalna X; dawniej obrot calego korzenia). null =
        /// wies z ikona (Calradia / Essos / Dothrakowie) albo mniej niz 3 domy ROT.</summary>
        private static List<HouseUnit> HouseUnits(Template t, out float unitScale, out string family)
        {
            unitScale = 1f;
            family = null;
            if (t == null || !t.Flat) return null;
            var plan = t.Plans[3];
            if (plan == null || plan.Length == 0) return null;
            var frames = PlanFrames(plan);
            Mat3 tr = Mat3.Identity;
            if (t.HasAxis) tr.RotateAboutUp(-t.Axis);
            var turn = new MatrixFrame(in tr, new Vec3(0f, 0f, 0f, 1f));
            int andal = 0, fm = 0;
            var list = new List<HouseUnit>();
            var scales = new List<float>();
            for (int i = 0; i < plan.Length; i++)
            {
                var nd = plan[i];
                if (nd.Meshes.Length == 0) continue;
                string mn = nd.Meshes[0].GetName() ?? "";
                if (mn.StartsWith("andal_wm_", StringComparison.Ordinal)) andal++;
                else if (mn.StartsWith("fm_wm_", StringComparison.Ordinal)) fm++;
                MatrixFrame f = turn.TransformToParent(in frames[i]);
                Vec3 b0, b1;
                float ext = RotBox(nd.Meshes, f.rotation, out b0, out b1) ? Math.Max(b1.x - b0.x, b1.y - b0.y) : 0f;
                float sc = Math.Abs(f.rotation.GetScaleVector().x);
                list.Add(new HouseUnit { Meshes = nd.Meshes, F = f, NoSeason = nd.NoSeason, Name = mn, Ext = ext, Scale = sc });
                if (sc > 1e-3f && !mn.Contains("well")) scales.Add(sc);
            }
            if (list.Count < 3 || andal + fm < 3) return null;
            family = fm > andal ? "fm" : "andal";
            if (scales.Count > 0) { scales.Sort(); unitScale = scales[scales.Count / 2]; }
            return list;
        }

        /// <summary>Styl wsi z ikona: kultura z nazwy siatki ikony (village_&lt;c&gt;_N, dothraki_village -> khuzait), inaczej ze starego
        /// prefabu wsi-matek kultury (map_icon_full_&lt;c&gt;_village), inaczej empire.</summary>
        private static string IconStyle(Template t, District d)
        {
            var p = t != null ? t.Plans[3] : null;
            if (p != null)
                foreach (var n in p)
                    foreach (var m in n.Meshes)
                    {
                        string nm = m.GetName() ?? "";
                        if (nm.StartsWith("dothraki_village", StringComparison.Ordinal)) return "khuzait";
                        if (nm.StartsWith("village_", StringComparison.Ordinal))
                        {
                            string c = nm.Substring(8);
                            int us = c.IndexOf('_');
                            if (us > 0) c = c.Substring(0, us);
                            if (Array.IndexOf(MapVillageData.StyleNames, c) >= 2) return c;
                        }
                    }
            string cid = d != null && d.S != null && d.S.Culture != null ? d.S.Culture.StringId : "";
            string mp = MapVillageData.CultureMotherPrefab(cid) ?? "";
            if (mp.StartsWith("map_icon_full_", StringComparison.Ordinal))
            {
                string c = mp.Substring(14);
                int us = c.IndexOf('_');
                if (us > 0) c = c.Substring(0, us);
                if (Array.IndexOf(MapVillageData.StyleNames, c) >= 2) return c;
            }
            return "empire";
        }

        /// <summary>BB siatek pod obrotem (ze skala) rot: min / max wzgledem origin (jedn. wzoru); false = brak BB.</summary>
        private static bool RotBox(MetaMesh[] meshes, Mat3 rot, out Vec3 mn, out Vec3 mx)
        {
            mn = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            mx = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;
            if (meshes == null) return false;
            foreach (MetaMesh mm in meshes)
            {
                Vec3 b0, b1;
                if (!MeshBox(mm, out b0, out b1)) continue;
                for (int c = 0; c < 8; c++)
                {
                    Vec3 v = new Vec3((c & 1) != 0 ? b1.x : b0.x, (c & 2) != 0 ? b1.y : b0.y, (c & 4) != 0 ? b1.z : b0.z);
                    Vec3 w = rot.TransformToParent(in v);
                    mn = new Vec3(Math.Min(mn.x, w.x), Math.Min(mn.y, w.y), Math.Min(mn.z, w.z));
                    mx = new Vec3(Math.Max(mx.x, w.x), Math.Max(mx.y, w.y), Math.Max(mx.z, w.z));
                    any = true;
                }
            }
            return any;
        }

        /// <summary>Element z siatek pod obrotem rot (ze skala): dol / gora / srodek / polboki BB (jedn. wzoru). null = brak BB.</summary>
        private static Piece MakePiece(MetaMesh[] meshes, Mat3 rot, string name)
        {
            Vec3 mn, mx;
            if (!RotBox(meshes, rot, out mn, out mx)) return null;
            var p = new Piece { Meshes = meshes, Name = name ?? "" };
            p.Local = new MatrixFrame(in rot, new Vec3(0f, 0f, 0f, 1f));
            p.Bottom = mn.z;
            p.Top = mx.z;
            p.Cx0 = (mn.x + mx.x) * 0.5f;
            p.Cy0 = (mn.y + mx.y) * 0.5f;
            p.Rx = Math.Max(0.01f, (mx.x - mn.x) * 0.5f);
            p.Ry = Math.Max(0.01f, (mx.y - mn.y) * 0.5f);
            return p;
        }

        private static int LevelsFrom(int minLevel)
        {
            int b = 0;
            for (int lv = Math.Max(1, minLevel); lv <= 3; lv++) b |= 1 << lv;
            return b;
        }

        /// <summary>Dom numer r (od srodka) stoi na poziomach, gdzie HousesFor(model, poziom) &gt; r.</summary>
        private static int LevelsForHouse(int model, int r)
        {
            int b = 0;
            for (int lv = 1; lv <= 3; lv++) if (MapVillageData.HousesFor(model, lv) > r) b |= 1 << lv;
            return b;
        }

        /// <summary>Kepa "village": wies z domami ROT - wszystkie domy wzoru w ukladzie wsi-matki (odstepy jak w prefabie) + detale stylu
        /// (sterta drewna, oborka, studnia / stog, woz, worki) w luki przy srodku do VillageKitMin budynkow; przerzedzanie od brzegu w Create.
        /// Wies z ikona (cala wies gry w jednej siatce na poziom): ikony poziomow z wzoru, pochylone do stoku jak wies gry.</summary>
        private bool FillVillage(Kit k, District d, Template t, List<HouseUnit> units, MapVillageData.StyleKit st, float unitScale)
        {
            if (units == null)
            {
                for (int lv = 1; lv <= 3; lv++)
                {
                    var plan = t.Plans[lv];
                    if (plan == null) continue;
                    var frames = PlanFrames(plan);
                    for (int i = 0; i < plan.Length; i++)
                    {
                        if (plan[i].Meshes.Length == 0) continue;
                        int same = -1;
                        for (int q = 0; q < k.Pieces.Count; q++)
                        {
                            Piece o = k.Pieces[q];
                            if (o.Meshes.Length == plan[i].Meshes.Length && o.Meshes[0].GetName() == plan[i].Meshes[0].GetName()
                                && Math.Abs(o.Local.origin.x - frames[i].origin.x) < 0.01f && Math.Abs(o.Local.origin.y - frames[i].origin.y) < 0.01f) { same = q; break; }
                        }
                        if (same >= 0) { k.Pieces[same].Levels |= 1 << lv; continue; }
                        Piece p = MakePiece(plan[i].Meshes, frames[i].rotation, plan[i].Meshes[0].GetName());
                        if (p == null) continue;
                        p.Local.origin = new Vec3(frames[i].origin.x, frames[i].origin.y, 0f, 1f);
                        p.Levels = 1 << lv;
                        p.Role = MapVillageData.RoleHouse;
                        p.Ground = MapVillageData.GroundTilt;
                        p.NoSeason = plan[i].NoSeason;
                        p.Essential = true;
                        p.Pivot = true;
                        p.ZOff = frames[i].origin.z;
                        k.Pieces.Add(p);
                    }
                }
                return k.Pieces.Count > 0;
            }
            var pieces = new List<Piece>();
            var items = new List<MapVillageData.LayoutItem>();
            foreach (var u in units)
            {
                Piece p = MakePiece(u.Meshes, u.F.rotation, u.Name);
                if (p == null) continue;
                p.Role = MapVillageData.RoleHouse;
                p.NoSeason = u.NoSeason;
                p.PrefabZ = true;            // wyglad 3: wysokosc z dolu BB (ZOff tylko do logu korekty)
                p.ZOff = u.F.origin.z;
                pieces.Add(p);
                LogUnit(u, k);
                items.Add(new MapVillageData.LayoutItem { Slot = MapVillageData.SlotPreset, R = Math.Max(p.Rx, p.Ry), X = u.F.origin.x + p.Cx0, Y = u.F.origin.y + p.Cy0, Core = true });
            }
            if (pieces.Count == 0) return false;
            Recenter(items);
            uint seed = MapVillageData.Fnv(d.Id);
            int fi = 0;
            while (pieces.Count < MapVillageData.VillageKitMin && st.Fill.Length > 0 && fi < MapVillageData.VillageKitMin * 2)
            {
                string name = st.Fill[fi % st.Fill.Length];
                fi++;
                // studnia bez wzoru we wsi-matce: skala domow x 0.5 (= 0.3 jak andal_wm_well2 w fm_village1/2); gdy wzor ja ma, SpecPiece bierze
                // jej wlasna skale z prefabu - bez drugiego x 0.5 (recenzja 08.10)
                bool unitHas = false;
                foreach (var u in units) if (u.Name == name) { unitHas = true; break; }
                var sp = st.FillUnit
                    ? new MapVillageData.PieceSpec { Role = MapVillageData.RoleDetail, Meshes = new[] { name }, Size = 1f, SizeMode = MapVillageData.SizeUnit,
                                                     Natural = name.Contains("well") && !unitHas ? 0.5f : 1f, Turn = MapVillageData.TurnSeed, Source = MapVillageData.SrcRot }
                    : new MapVillageData.PieceSpec { Role = MapVillageData.RoleDetail, Meshes = new[] { name }, Size = name == "mi_cart_a" ? 0.6f : name == "mi_sack_a" ? 0.3f : 0.55f,
                                                     SizeMode = MapVillageData.SizeNatural, Natural = MapVillageData.NaturalScale(name), Turn = MapVillageData.TurnSeed, Source = MapVillageData.SrcMap };
                sp.Slot = MapVillageData.SlotFill;
                Piece p = SpecPiece(sp, k, t, unitScale, seed + (uint)fi);
                if (p == null) continue;
                pieces.Add(p);
                items.Add(new MapVillageData.LayoutItem { Slot = MapVillageData.SlotFill, R = Math.Max(p.Rx, p.Ry), Core = true });
            }
            MapVillageData.Layout(items, seed);
            k.ThinN = pieces.Count;
            k.ThinX = new float[pieces.Count];
            k.ThinY = new float[pieces.Count];
            k.ThinDetail = new bool[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece p = pieces[i];
                p.Local.origin = new Vec3(items[i].X - p.Cx0, items[i].Y - p.Cy0, 0f, 1f);
                p.ThinIndex = i;
                k.ThinX[i] = items[i].X;
                k.ThinY[i] = items[i].Y;
                k.ThinDetail[i] = p.Role == MapVillageData.RoleDetail || MapVillageData.IsDetailMesh(p.Name);
                k.Pieces.Add(p);
            }
            return true;
        }

        /// <summary>Obrazki mill / windmill / farm / granary / fishing: domy (z wzoru - najblizsze srodka kepy wsi-matki, albo domy kultury)
        /// + elementy przepisu ulozone wokol kepy (MapVillageData.Layout). false = brak niezbednej siatki (zapas w BuildKit).</summary>
        private bool FillModel(Kit k, District d, Template t, List<HouseUnit> units, MapVillageData.StyleKit st, float unitScale, int model, out string why)
        {
            why = null;
            uint seed = MapVillageData.Fnv(d.Id + ":" + model.ToString(CultureInfo.InvariantCulture));
            var pieces = new List<Piece>();
            var specs = new List<MapVillageData.PieceSpec>();   // przepis elementu (null = dom) - siatki doczepione
            var items = new List<MapVillageData.LayoutItem>();
            int nh = MapVillageData.HousesFor(model, 3);
            if (units != null)
            {
                var all = new List<Piece>();
                var xs = new List<float>();
                var ys = new List<float>();
                foreach (var u in units)
                {
                    if (u.Name.Contains("well")) continue;   // studnia to nie dom
                    Piece p = MakePiece(u.Meshes, u.F.rotation, u.Name);
                    if (p == null) continue;
                    p.Role = MapVillageData.RoleHouse;
                    p.NoSeason = u.NoSeason;
                    p.PrefabZ = true;        // wyglad 3: wysokosc z dolu BB (ZOff tylko do logu korekty)
                    p.ZOff = u.F.origin.z;
                    p.Local.origin = new Vec3(u.F.origin.x, u.F.origin.y, 0f, 1f);
                    all.Add(p);
                    LogUnit(u, k);
                    xs.Add(u.F.origin.x + p.Cx0);
                    ys.Add(u.F.origin.y + p.Cy0);
                }
                int[] order = MapVillageData.RankCentral(d.Id + ":" + model.ToString(CultureInfo.InvariantCulture), xs, ys);
                for (int r = 0; r < nh && r < order.Length; r++)
                {
                    Piece p = all[order[r]];
                    p.Levels = LevelsForHouse(model, r);
                    pieces.Add(p);
                    specs.Add(null);
                    items.Add(new MapVillageData.LayoutItem { Slot = MapVillageData.SlotPreset, R = Math.Max(p.Rx, p.Ry), X = xs[order[r]], Y = ys[order[r]], Core = true });
                }
                Recenter(items);
            }
            else
            {
                for (int i = 0; i < nh && st.Houses.Length > 0; i++)
                {
                    var hs = new string[st.Houses.Length];
                    for (int q = 0; q < hs.Length; q++) hs[q] = st.Houses[(i + q) % st.Houses.Length];
                    var sp = new MapVillageData.PieceSpec { Role = MapVillageData.RoleHouse, Meshes = hs, Size = 1f, SizeMode = MapVillageData.SizeFitH,
                                                            Slot = i == 0 ? MapVillageData.SlotCenter : MapVillageData.SlotFill, Turn = MapVillageData.TurnSeed,
                                                            Source = MapVillageData.SrcMap };
                    Piece p = SpecPiece(sp, k, t, unitScale, seed + (uint)i);
                    if (p == null) continue;
                    p.Levels = LevelsForHouse(model, i);
                    pieces.Add(p);
                    specs.Add(null);
                    items.Add(new MapVillageData.LayoutItem { Slot = sp.Slot, R = Math.Max(p.Rx, p.Ry), Core = true });
                }
            }
            if (pieces.Count == 0) { why = "brak domow stylu " + st.Name; return false; }
            int mill = -1, pier = -1;
            foreach (var sp in MapVillageData.Recipe(model, st))
            {
                Piece p = SpecPiece(sp, k, t, unitScale, seed);
                if (p == null && sp.BarnLike) p = BarnSubstitute(k, t, st, units, unitScale, sp, seed);
                if (p == null)
                {
                    if (sp.Essential && (model == MapVillageData.ModelMill || model == MapVillageData.ModelWindmill))
                    { why = "brak siatki " + (sp.Meshes.Length > 0 ? sp.Meshes[0] : "?"); return false; }
                    continue;
                }
                p.Levels = LevelsFrom(sp.MinLevel);
                if (model == MapVillageData.ModelMill && sp.Role == MapVillageData.RoleSpecial) mill = pieces.Count;
                if (model == MapVillageData.ModelFishing && sp.Ground == MapVillageData.GroundEnd && pier < 0) pier = pieces.Count;
                pieces.Add(p);
                specs.Add(sp);
                items.Add(new MapVillageData.LayoutItem { Slot = sp.Slot, R = Math.Max(p.Rx, p.Ry), Core = false });
            }
            if (model == MapVillageData.ModelFishing)
            {
                bool anyWater = false;
                foreach (var p in pieces) if (p.Role == MapVillageData.RoleWater) anyWater = true;
                if (!anyWater) { why = "brak pomostu i lodzi"; return false; }
            }
            MapVillageData.Layout(items, seed);
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece p = pieces[i];
                p.Local.origin = new Vec3(items[i].X - p.Cx0, items[i].Y - p.Cy0, 0f, 1f);
                if (p.WaterSide) k.HasWater = true;
                k.Pieces.Add(p);
            }
            // siatki doczepione (kolo mlyna; skrzydla i schody wiatraka - wyglad 3): osobne kopie siatek jako dzieci encji budynku, ramka w
            // jednostkach jego siatki jak dziecko w prefabie / scenie gry (bez skryptu WindMill, fizyki, czastek, dzwieku smigla)
            for (int i = 0; i < pieces.Count; i++)
            {
                var spc = specs[i];
                if (spc == null || spc.Attach == null) continue;
                Piece par = pieces[i];
                float psc = Math.Abs(par.Local.rotation.GetScaleVector().x);
                foreach (var at in spc.Attach)
                {
                    if (at.ForMesh != par.Name) continue;   // zapas (np. wieza battania_windmill) - bez czesci innego modelu
                    MetaMesh am = KitMesh(at.Mesh);
                    if (am == null) { k.AttachMiss++; _attachMiss++; continue; }
                    Mat3 ar = Mat3.Identity;
                    if (at.RotUp != 0f) ar.RotateAboutUp(at.RotUp);
                    if (at.RotFwd != 0f) ar.RotateAboutForward(at.RotFwd);
                    k.Pieces.Add(new Piece
                    {
                        Meshes = new[] { am }, Local = new MatrixFrame(in ar, new Vec3(at.X, at.Y, at.Z, 1f)), Parent = i, Role = MapVillageData.RoleAttached,
                        Ground = MapVillageData.GroundAttached, Levels = 14, Name = at.Mesh, NoSeason = par.NoSeason || t.NoSeason
                    });
                    if (at.Mesh == MapVillageData.WindmillFan) k.HasFan = true;
                    Vec3 b0, b1;
                    if (!MeshBox(am, out b0, out b1)) continue;
                    LogMesh(at.Mesh, "doczepione do " + par.Name, k, MapVillageData.SrcScene, b0, b1, psc,
                            "jak " + par.Name + "; ramka (" + F2(at.X) + ", " + F2(at.Y) + ", " + F2(at.Z) + ") w jedn. jego siatki - " + at.Why,
                            Math.Max(b1.x - b0.x, Math.Max(b1.y - b0.y, b1.z - b0.z)) * psc);
                    if (at.Mesh == MapVillageData.WatermillWheel && i == mill)
                    {
                        // kotwica mlyna = srodek kola (ramka kola obrocona i przeskalowana jak mlyn, wzgledem jego origin); kolo: promien z BB (os
                        // kola = lokalna Y kola: obrot wokol niej nie zmienia grubosci), grubosc wzdluz osi
                        var wo = new Vec3(at.X, at.Y, at.Z);
                        Vec3 off = par.Local.rotation.TransformToParent(in wo);
                        k.AnchorDX = off.x;
                        k.AnchorDY = off.y;
                        k.WheelR = Math.Max(b1.x - b0.x, b1.z - b0.z) * 0.5f * psc;
                        k.AnchorOut = 0.6f * (b1.y - b0.y) * 0.5f * psc;
                    }
                }
            }
            if (mill >= 0)
            {
                k.Anchor = mill;
                if (!(k.WheelR > 0f)) { k.AnchorDX = pieces[mill].Cx0; k.AnchorDY = pieces[mill].Cy0 + pieces[mill].Ry; k.AnchorOut = 0f; }   // bez kola: przod mlyna
            }
            else if (pier >= 0) k.Anchor = pier;
            return true;
        }

        private static void Recenter(List<MapVillageData.LayoutItem> items)
        {
            float mx = 0f, my = 0f;
            int c = 0;
            foreach (var it in items) if (it.Slot == MapVillageData.SlotPreset) { mx += it.X; my += it.Y; c++; }
            if (c == 0) return;
            mx /= c; my /= c;
            foreach (var it in items) if (it.Slot == MapVillageData.SlotPreset) { it.X -= mx; it.Y -= my; }
        }

        /// <summary>Element przepisu: wariant siatki z ziarna (pierwszy istniejacy), skala wedlug trybu (z BB / naturalna z mapy gry / skala domow
        /// wzoru), obrot (dluga os wzdluz ulicy / do wody, pi dla mlyna, z ziarna). Kazda uzyta siatka raz w wioski.log (nazwa, skala, zrodlo).</summary>
        private Piece SpecPiece(MapVillageData.PieceSpec sp, Kit k, Template t, float unitScale, uint seed)
        {
            int nm = sp.Meshes.Length;
            if (nm == 0) return null;
            string name = null;
            MetaMesh mm = null;
            for (int i = 0; i < nm && mm == null; i++)
            {
                string cand = sp.Ordered ? sp.Meshes[i] : sp.Meshes[(int)((seed + (uint)i) % (uint)nm)];   // Ordered: pierwszy istniejacy po kolei
                mm = KitMesh(cand);
                if (mm != null) name = cand;
            }
            if (mm == null) return null;
            Vec3 b0, b1;
            if (!MeshBox(mm, out b0, out b1)) return null;
            float w = b1.x - b0.x, dd = b1.y - b0.y, hh = b1.z - b0.z;
            float ext = Math.Max(w, dd);
            float target = sp.Size * k.H;
            // siatka rodziny domow (fm_wm_* / andal_wm_*), ktora JEST we wzorze wsi-matki: ta sama siatka (kolory z prefabu), skala i wysokosc
            // nad gruntem jak u autora prefabu (x Natural)
            HouseUnit clone = null;
            if (sp.SizeMode == MapVillageData.SizeUnit && k.Units != null)
                foreach (var u in k.Units) if (u.Name == name && u.Meshes.Length == 1) { clone = u; break; }
            MetaMesh[] meshes = clone != null ? clone.Meshes : new[] { mm };
            Mat3 r0;
            float sc;
            string how;
            bool pivot = sp.Pivot;
            float zoff = 0f;
            bool prefabZ = false;
            if (clone != null)
            {
                r0 = clone.F.rotation;
                if (Math.Abs(sp.Natural - 1f) > 1e-3f) r0.ApplyScaleLocal(sp.Natural);
                sc = Math.Abs(r0.GetScaleVector().x);
                how = "jak ta siatka we wzorze wsi-matki" + (Math.Abs(sp.Natural - 1f) > 0.01f ? " x " + F2(sp.Natural) : "");
                prefabZ = true;   // wyglad 3: wysokosc z dolu BB (ZOff tylko do logu korekty)
                zoff = clone.F.origin.z * sp.Natural;
            }
            else
            {
                switch (sp.SizeMode)
                {
                    case MapVillageData.SizeFitHeight:
                        sc = hh > 1e-3f ? target / hh : 1f;
                        how = "z BB: wysokosc " + F2(sp.Size) + " x dom";
                        if (ext * sc > 1.3f * target) { sc = 1.3f * target / ext; how += " (przycieta szerokoscia)"; }
                        break;
                    case MapVillageData.SizeNatural:
                        sc = sp.Natural;
                        how = "naturalna z mapy gry " + F2(sp.Natural);
                        if (ext * sc < 0.4f * target || ext * sc > 2.5f * target)
                        {
                            sc = ext > 1e-3f ? target / ext : 1f;
                            how = "z BB (naturalna " + F2(sp.Natural) + " dalaby " + F2(ext * sp.Natural / Math.Max(0.01f, k.H)) + " x dom)";
                        }
                        break;
                    case MapVillageData.SizeUnit:
                        sc = unitScale * sp.Natural;
                        how = "skala domow wzoru " + F2(unitScale) + (Math.Abs(sp.Natural - 1f) > 0.01f ? " x " + F2(sp.Natural) : "");
                        break;
                    default:
                        sc = ext > 1e-3f ? target / ext : 1f;
                        how = "z BB: wiekszy bok " + F2(sp.Size) + " x dom";
                        break;
                }
                if (!(sc > 1e-4f) || sc > 50f || float.IsNaN(sc)) return null;
                r0 = Mat3.Identity;
                r0.ApplyScaleLocal(sc);
            }
            Vec3 e0, e1;
            if (!RotBox(meshes, r0, out e0, out e1)) return null;
            float ex = e1.x - e0.x, ey = e1.y - e0.y;
            float yaw = 0f;
            switch (sp.Turn)
            {
                case MapVillageData.TurnPi: yaw = (float)Math.PI; break;
                case MapVillageData.TurnLongX: yaw = ey > ex ? (float)(Math.PI / 2) : 0f; break;
                case MapVillageData.TurnLongY: yaw = ex > ey ? (float)(Math.PI / 2) : 0f; break;
                case MapVillageData.TurnSeed: yaw = (MapVillageData.Fnv(name + ":" + seed.ToString(CultureInfo.InvariantCulture)) % 360u) * (float)Math.PI / 180f; break;
                case MapVillageData.TurnCamera: yaw = 0f; break;   // wiatrak: obrot do kamery ustawia Create (zalezy od obrotu obrazka)
            }
            Mat3 ry = Mat3.Identity;
            ry.RotateAboutUp(yaw);
            Mat3 r = ry.TransformToParent(in r0);
            Piece p = MakePiece(meshes, r, name);
            if (p == null) return null;
            p.Role = sp.Role;
            p.Ground = sp.Ground;
            p.Essential = sp.Essential;
            p.WaterSide = sp.WaterSide;
            p.Pivot = pivot;
            p.ZOff = zoff;
            p.PrefabZ = prefabZ;
            p.FaceCamera = sp.Turn == MapVillageData.TurnCamera;
            p.NoSeason = (t != null && t.NoSeason) || (clone != null && clone.NoSeason);
            string role = sp.Role == MapVillageData.RoleHouse ? "dom" : sp.Role == MapVillageData.RoleSpecial ? "budynek glowny" : sp.Role == MapVillageData.RoleWater ? "przy wodzie" : "detal";
            LogMesh(name, role, k, clone != null ? MapVillageData.SrcHouse : sp.Source, b0, b1, sc, how, Math.Max(ex, ey));
            return p;
        }

        /// <summary>Zapas stodoly (brak modelu scenowego): fm - najwiekszy budynek gospodarczy Polnocy (fm_wm_hall_snow) w skali domow x 1.3;
        /// inaczej najwiekszy dom wzoru / stylu x 1.3 ("najwieksza szopa / stodola w stylu krainy").</summary>
        private Piece BarnSubstitute(Kit k, Template t, MapVillageData.StyleKit st, List<HouseUnit> units, float unitScale, MapVillageData.PieceSpec barn, uint seed)
        {
            Piece p = null;
            if (!string.IsNullOrEmpty(st.Hall))
            {
                var sp = new MapVillageData.PieceSpec { Role = MapVillageData.RoleSpecial, Meshes = new[] { st.Hall }, Size = barn.Size, SizeMode = MapVillageData.SizeUnit,
                                                        Natural = 1.3f, Turn = MapVillageData.TurnLongX, Ground = barn.Ground, Source = MapVillageData.SrcRot, Essential = true };
                p = SpecPiece(sp, k, t, unitScale, seed);
            }
            if (p == null && units != null)
            {
                HouseUnit big = null;
                foreach (var u in units) if (!u.Name.Contains("well") && (big == null || u.Ext > big.Ext)) big = u;
                if (big != null)
                {
                    Mat3 r = big.F.rotation;
                    r.ApplyScaleLocal(1.3f);
                    p = MakePiece(big.Meshes, r, big.Name);
                    if (p != null) { p.Role = MapVillageData.RoleSpecial; p.Essential = true; p.NoSeason = big.NoSeason; p.PrefabZ = true; p.ZOff = big.F.origin.z * 1.3f; }
                }
            }
            if (p == null && st.Houses.Length > 0)
            {
                var sp = new MapVillageData.PieceSpec { Role = MapVillageData.RoleSpecial, Meshes = st.Houses, Size = 1.6f, SizeMode = MapVillageData.SizeFitH,
                                                        Turn = MapVillageData.TurnLongX, Ground = barn.Ground, Source = MapVillageData.SrcMap, Essential = true };
                p = SpecPiece(sp, k, t, unitScale, seed);
            }
            string key = "stodola->" + (p != null ? "szopa/dom" : "nic");
            int c;
            _kitFallback.TryGetValue(key, out c);
            _kitFallback[key] = c + 1;
            return p;
        }

        /// <summary>Siatka elementu obrazka po NAZWIE SIATKI (MetaMesh.GetCopy bez bledow i z null - jak namiot partii i ikony wsi), raz na nazwe;
        /// siatka bez materialu = brak. Brak = element pominiety albo zapas (log raz na nazwe).</summary>
        private MetaMesh KitMesh(string mesh)
        {
            if (string.IsNullOrEmpty(mesh)) return null;
            MetaMesh mm;
            if (_meshByName.TryGetValue(mesh, out mm)) return mm;
            _meshByName[mesh] = null;   // blad w srodku = ta nazwa juz nie probowana
            mm = MetaMesh.GetCopy(mesh, false, true);
            if (mm != null && !mm.IsValid) mm = null;
            if (mm != null && !MaterialOk(mm)) mm = null;
            _meshByName[mesh] = mm;
            if (mm == null)
            {
                _kitMeshMiss++;
                if (_missLogged.Add(mesh)) Log.Info("Wioski: drzewo obrazkow - brak siatki '" + mesh + "' w grze (albo bez materialu) - element pominiety albo zapas.");
            }
            return mm;
        }

        /// <summary>Raz na nazwe siatki do wioski.log: rola, model, styl, zrodlo, BB, skala i wielkosc wzgledem domu wsi okregu.</summary>
        private void LogMesh(string name, string role, Kit k, string src, Vec3 b0, Vec3 b1, float sc, string how, float extScaled)
        {
            if (string.IsNullOrEmpty(name) || !_meshLogged.Add(name)) return;
            Log.Info("Wioski: drzewo obrazkow - siatka " + name + ": " + role + " w '" + MapVillageData.ModelName(k.Model) + "' (styl " + k.Style + "); zrodlo "
                     + src + "; BB siatki " + F2(b1.x - b0.x) + "x" + F2(b1.y - b0.y) + "x" + F2(b1.z - b0.z) + " (z od " + F2(b0.z) + " do " + F2(b1.z) + ")"
                     + "; skala " + sc.ToString("0.000", CultureInfo.InvariantCulture)
                     + " (" + how + "); na obrazku " + F2(extScaled) + " jedn. wzoru = " + F2(extScaled / Math.Max(0.01f, k.H)) + " x dom wsi okregu (H " + F2(k.H)
                     + "); tylko kopia siatki - bez fizyki, skryptow, czastek, dzwiekow.");
        }

        /// <summary>Dom wsi okregu z wzoru wsi-matki (siatka rodziny andal / fm) - raz na nazwe do wioski.log, jak siatki przepisow.</summary>
        private void LogUnit(HouseUnit u, Kit k)
        {
            if (u == null || u.Meshes.Length == 0 || _meshLogged.Contains(u.Name)) return;
            Vec3 b0, b1;
            if (!MeshBox(u.Meshes[0], out b0, out b1)) return;
            LogMesh(u.Name, "dom wsi okregu", k, MapVillageData.SrcHouse, b0, b1, u.Scale, "jak w prefabie domow wsi-matki", u.Ext);
        }

        /// <summary>Raz na (model, styl) do wioski.log: sklad wzoru obrazka, ile budynkow na poziomach 1/2/3, obrys.</summary>
        private void LogKit(Kit k, District d)
        {
            string key = k.Model + ":" + k.Style;
            if (!_kitLogged.Add(key)) return;
            int houses = 0, det = 0, spec = 0, water = 0, att = 0;
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            var names = new List<string>();
            var lvCount = new int[4];
            foreach (var p in k.Pieces)
            {
                if (p.Role == MapVillageData.RoleHouse) houses++; else if (p.Role == MapVillageData.RoleSpecial) spec++;
                else if (p.Role == MapVillageData.RoleWater) water++; else if (p.Role == MapVillageData.RoleAttached) att++; else det++;
                if (!names.Contains(p.Name)) names.Add(p.Name);
                if (p.Parent >= 0) continue;
                for (int lv = 1; lv <= 3; lv++) if ((p.Levels & (1 << lv)) != 0) lvCount[lv]++;
                float cx = p.Local.origin.x + p.Cx0, cy = p.Local.origin.y + p.Cy0;
                x0 = Math.Min(x0, cx - p.Rx); x1 = Math.Max(x1, cx + p.Rx); y0 = Math.Min(y0, cy - p.Ry); y1 = Math.Max(y1, cy + p.Ry);
            }
            if (k.ThinN > 0) for (int lv = 1; lv <= 3; lv++) lvCount[lv] = MapVillageData.VillageWant(k.ThinN, lv);
            Log.Info("Wioski: drzewo obrazkow - wzor '" + MapVillageData.ModelName(k.Model) + "'" + (k.Asked != k.Model ? " (z pliku '" + MapVillageData.ModelName(k.Asked) + "')" : "")
                     + " styl " + k.Style + " (pierwszy: okreg " + d.Id + " '" + (d.S != null ? d.S.Name.ToString() : "?") + "'): elementow " + k.Pieces.Count
                     + " (domy " + houses + ", detale " + det + ", glowny " + spec + ", przy wodzie " + water + ", doczepione " + att + "); budynkow na poziomie 1/2/3 "
                     + lvCount[1] + "/" + lvCount[2] + "/" + lvCount[3] + (k.ThinN > 0 ? " (przerzedzanie od brzegu kepy)" : "") + "; dom H " + F2(k.H)
                     + "; obrys " + F2(x1 - x0) + "x" + F2(y1 - y0) + " jedn. wzoru (" + F2((x1 - x0) / Math.Max(0.01f, k.H)) + "x" + F2((y1 - y0) / Math.Max(0.01f, k.H))
                     + " domu); siatki: " + string.Join(", ", names.ToArray()) + ".");
        }

        // ---------- wzor wsi-matki: uklad siatek widocznych przy danym poziomie (algorytm gry, bez kopiowania encji) ----------
        // AUTOTEST 07.10 17:41: 41 wzorow, 0 dobrych - "brak siatek" (MultiMeshComponentCount = 0 na calym drzewie kazdej matki, tez
        // przy zapasie maski). Natywny "Town Scene Manager" przy wczytaniu mapy zbiera siatki osad do wspolnych ikon (rgl_log 17:41:06
        // "Town scene manager: Total mesh: 23858, Total Unique Mesh: 304"; rglTown_icon_component = ComponentType.TownIcon bez klasy
        // zarzadzanej, GetComponentAtIndex dalby null). Encje, nazwy, ramki i maski zostaja. Drogi po kolei, kazda liczona w logu:
        //  1 drzewo matki: siatka encji = jej komponenty MetaMesh, a gdy ich brak - siatka po nazwie encji (MapVillageData.MeshForName,
        //    MetaMesh.GetCopy jak namiot partii); ramki i maski z encji w grze;
        //  2 prefaby: encje matki, ktorych NAZWA jest nazwa prefabu (PrefabExists; od 08.10 bez GetPrefabName / GetOldPrefabName) -> kopia BEZ SCENY
        //    (GameEntity.Instantiate(null, ...) jak DestructableComponent.cs:194 / MissionDeploymentBoundaryMarker.cs:184) i jej siatki;
        //  3 kultura: prefab domow kultury wsi (Westeros), potem stary prefab samej matki (map_icon_full_*), na koncu andal_village3.
        // POPRAWKA WYGLADU 08.10: droga 1 dal na Polnocy JEDNA szope (zastepce grupy domow), bo encja prefabu domow byla zwinieta. Teraz
        // droga 1 = drzewo matki + kazda zwinieta encja z nazwa prefabu rozwinieta kopia prefabu bez sceny (MapVillageData.ExpandCollapsed,
        // wszystkie domy z ramkami lokalnymi) + liscie po nazwie; zastepca, ktory jest tym samym domem co dom z rozwinietego prefabu, zdjety
        // (MapVillageData.SameHouse). Siatka bez materialu pominieta (z samym domyslnym zostaje, liczona). Skala z BB: widoczne domy matki / BB wzoru.
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
                if (d.T.Way == WayTree) { if (d.T.Expanded > 0) _tplExpand++; else if (d.T.FromName > 0) _tplName++; else _tplComp++; }
                else if (d.T.Way == WayPrefab) _tplPrefab++;
                else if (d.T.Way == WayCulture) _tplCulture++;
                _meshComp += d.T.FromComp;
                _meshName += d.T.FromName;
                _meshPrefab += d.T.FromPrefab;
                _expandedTotal += d.T.Expanded;
                _dupDropped += d.T.DupDropped;
                _matSkipped += d.T.MatSkipped;
                _fitN++;
                _fitSum += d.T.Fit;
                if (d.T.Fit < _fitMin) _fitMin = d.T.Fit;
                if (d.T.Fit > _fitMax) _fitMax = d.T.Fit;
            }
            else _templatesBad++;
            // diagnostyka (nastepny autotest rozstrzyga, jesli poprawka nie trafi): 3 pierwsze matki po wczytaniu + pierwsza bez wzoru
            // 08.10: drzewo encji matki TYLKO w autotescie (CrashScribe.Autotest.Active) - naruszenia pamieci w silniku nie lapie zaden catch
            if (_diagOn && (_diagMothers < 3 || (!d.T.Ok && !_diagFailDone)))
            {
                if (_diagMothers >= 3) _diagFailDone = true;
                _diagMothers++;
                DiagMother(d);
            }
            // 3 pierwsze dobre wzory: lista siatek (nazwa, material, ramka, BB) i BB calego wzoru (poprawka wygladu 08.10); w autotescie
            // dodatkowo pierwszy wzor kazdego RODZAJU (droga + zestaw nazw siatek, najwyzej DiagKindsMax) - recenzja 08.10: start autotestu
            // jest daleko od Polnocy, wiec 3 pierwsze wzory nie pokazalyby materialow domow fm_wm_* ze zrzutu Jeffa (Tumbledown, fm_village2)
            if (d.T.Ok)
            {
                bool newKind = false;
                if (_diagOn && _diagKinds.Count < DiagKindsMax)
                {
                    try { newKind = _diagKinds.Add(TemplateKind(d.T)); }
                    catch (Exception e) { _stDiag++; if (_stDiag <= 3) Log.Error("MapVillagesView.TemplateKind " + d.Id + " (potkniecie " + _stDiag + ")", e); }
                }
                if (_diagTemplates < 3 || newKind)
                {
                    _diagTemplates++;
                    DiagTemplate(d);
                }
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
            // droga 1: drzewo matki (komponenty, a gdy ich brak - po nazwie encji) + zwiniete encje prefabow domow rozwiniete kopia prefabu
            // bez sceny (poprawka wygladu 08.10); droga 2: kazda encja matki z prefabem -> kopia bez sceny
            if (FillPlans(t, mother, false, true, false)) t.Way = WayTree;
            else if (FillPlans(t, mother, true, false, false)) t.Way = WayPrefab;
            else
            {
                // droga 3: prefab wsi kultury (Westeros: domy andal / fm), potem stary prefab samej matki, na koncu jeden staly
                string cid = s.Culture != null ? s.Culture.StringId : "";
                string[] cands = { MapVillageData.CultureHousePrefab(cid), MapVillageData.CultureMotherPrefab(cid), MapVillageData.LastResortPrefab };
                foreach (string p in cands)
                {
                    GameEntity tmp = PrefabTemplate(p);
                    if (tmp == (GameEntity)null) continue;
                    if (FillPlans(t, tmp, false, false, true)) { t.Way = WayCulture; t.WaySource = p; break; }
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
            FitTemplate(t, mother, mg);
            t.Ok = true;
            return t;
        }

        /// <summary>Plany 1/2/3 z drzewa src: najpierw scisly warunek maski (gra + encje bez poziomow), gdy nic - zapas maski.
        /// expand = droga 1 rozwija zwiniete encje prefabow domow; includeRoot = siatki samego src tez (kopia prefabu wsi kultury: korzen
        /// prefabu bywa jedyna siatka). Liczniki (rozwiniete, duplikaty, bez materialu) z przejscia poziomu 3.</summary>
        private bool FillPlans(Template t, GameEntity src, bool prefabs, bool expand, bool includeRoot)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool lenient = pass == 1;
                bool any = false;
                for (int lv = 1; lv <= 3; lv++)
                {
                    var a = new WalkArgs { Mask = _maskCivil | LevelMask(lv), Lenient = lenient, Prefabs = prefabs, Expand = expand };
                    t.Plans[lv] = BuildPlan(src, a, includeRoot);
                    if (t.Plans[lv].Length > 0) any = true;
                    if (lv == 3) { t.Expanded = a.Expanded; t.DupDropped = a.DupDropped; t.MatSkipped = a.MatSkipped; }
                }
                if (any) { t.Lenient = lenient; return true; }
            }
            return false;
        }

        // ---------- skala z BB (poprawka wygladu 08.10): obrazek ma 0.70 / 0.75 / 0.80 wielkosci widocznych domow matki ----------
        /// <summary>BB wzoru poziomu 3 (siatki w ramkach wzoru, obrocone i przeskalowane jak matka) vs BB widocznych domow matki z gry
        /// (zastepca grupy Town Scene Managera ma BB calej grupy); t.Fit = MapVillageData.FitToMother.</summary>
        private void FitTemplate(Template t, GameEntity mother, MatrixFrame mg)
        {
            t.Fit = 1f;
            try
            {
                Vec3 mn, mx;
                var turn = new MatrixFrame(in mg.rotation, new Vec3(0f, 0f, 0f, 1f));
                if (PlanBox(t.Plans[3], turn, out mn, out mx)) { t.TplW = mx.x - mn.x; t.TplD = mx.y - mn.y; t.TplH = mx.z - mn.z; }
                Vec3 all0 = mother.GlobalBoxMin, all1 = mother.GlobalBoxMax;
                t.MotherAllW = Math.Max(0f, all1.x - all0.x);
                t.MotherAllD = Math.Max(0f, all1.y - all0.y);
                bool any = false;
                Vec3 h0 = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue), h1 = new Vec3(float.MinValue, float.MinValue, float.MinValue);
                HousesBox(mother, 0, ref any, ref h0, ref h1);
                if (any)
                {
                    t.MotherW = h1.x - h0.x;
                    t.MotherD = h1.y - h0.y;
                    t.Fit = MapVillageData.FitToMother(t.MotherW, t.MotherD, t.TplW, t.TplD);
                }
                else
                {
                    // brak widocznych domow z BB (np. zastepca grupy to pole / stado - pomocnik): cala matka ma w BB pola i zgliszcza,
                    // wiec jej nie ufamy - dopasowanie 1 (skala jak dotad: matka x poziom)
                    t.MotherW = t.MotherAllW;
                    t.MotherD = t.MotherAllD;
                    t.MotherFromAll = true;
                    t.Fit = 1f;
                }
            }
            catch (Exception e)
            {
                t.Fit = 1f;
                _stDiag++;
                if (_stDiag <= 3) Log.Error("MapVillagesView.FitTemplate (potkniecie " + _stDiag + ")", e);
            }
        }

        /// <summary>Ramki wezlow planu wzgledem korzenia wzoru (rodzic przed dzieckiem).</summary>
        private static MatrixFrame[] PlanFrames(PlanNode[] plan)
        {
            var f = new MatrixFrame[plan.Length];
            for (int i = 0; i < plan.Length; i++)
            {
                MatrixFrame l = plan[i].Local;
                f[i] = plan[i].Parent >= 0 && plan[i].Parent < i ? f[plan[i].Parent].TransformToParent(in l) : l;
            }
            return f;
        }

        /// <summary>BB siatek planu (MetaMesh.GetBoundingBox w ramkach wezlow) po przeksztalceniu turn; false = brak siatek z BB.</summary>
        private static bool PlanBox(PlanNode[] plan, MatrixFrame turn, out Vec3 mn, out Vec3 mx)
        {
            mn = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            mx = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            if (plan == null || plan.Length == 0) return false;
            var frames = PlanFrames(plan);
            bool any = false;
            for (int i = 0; i < plan.Length; i++)
            {
                if (plan[i].Meshes.Length == 0) continue;
                MatrixFrame g = turn.TransformToParent(in frames[i]);
                foreach (MetaMesh mm in plan[i].Meshes)
                {
                    Vec3 b0, b1;
                    if (!MeshBox(mm, out b0, out b1)) continue;
                    for (int k = 0; k < 8; k++)
                    {
                        Vec3 c = new Vec3((k & 1) != 0 ? b1.x : b0.x, (k & 2) != 0 ? b1.y : b0.y, (k & 4) != 0 ? b1.z : b0.z);
                        Vec3 w = g.TransformToParent(in c);
                        mn = new Vec3(Math.Min(mn.x, w.x), Math.Min(mn.y, w.y), Math.Min(mn.z, w.z));
                        mx = new Vec3(Math.Max(mx.x, w.x), Math.Max(mx.y, w.y), Math.Max(mx.z, w.z));
                        any = true;
                    }
                }
            }
            return any;
        }

        private static bool MeshBox(MetaMesh mm, out Vec3 b0, out Vec3 b1)
        {
            b0 = b1 = new Vec3(0f, 0f, 0f);
            if (mm == null || !mm.IsValid) return false;
            BoundingBox bb = mm.GetBoundingBox();
            b0 = bb.min;
            b1 = bb.max;
            return b1.x > b0.x && b1.y > b0.y && !float.IsNaN(b0.x) && !float.IsInfinity(b1.x) && b1.x - b0.x < 1000f;
        }

        /// <summary>BB (swiat) encji domow matki widocznych przy pelnej wsi (civilian + level_3, jak wzor poziomu 3): bez pomocnikow, zgliszcz,
        /// oblezenia i ich poddrzew; encje z pustym BB (zwiniete prefaby) pomijane.</summary>
        private void HousesBox(GameEntity e, int depth, ref bool any, ref Vec3 mn, ref Vec3 mx)
        {
            if (depth > 8) return;
            uint levels = _maskL1 | _maskL2 | _maskL3;
            foreach (GameEntity c in e.GetChildren())
            {
                if (c == (GameEntity)null) continue;
                if (MapVillageData.IsHelperName(c.Name ?? "")) continue;
                uint m = (uint)c.GetUpgradeLevelMask();
                if (!MapVillageData.LevelVisible(m, _maskCivil | _maskL3, false, _maskCivil, _maskLooted, _maskSiege, levels)) continue;
                Vec3 b0 = c.GlobalBoxMin, b1 = c.GlobalBoxMax;
                if (b1.x - b0.x > 0.01f && b1.y - b0.y > 0.01f && b1.x - b0.x < 1000f && b1.y - b0.y < 1000f)
                {
                    mn = new Vec3(Math.Min(mn.x, b0.x), Math.Min(mn.y, b0.y), Math.Min(mn.z, b0.z));
                    mx = new Vec3(Math.Max(mx.x, b1.x), Math.Max(mx.y, b1.y), Math.Max(mx.z, b1.z));
                    any = true;
                }
                HousesBox(c, depth + 1, ref any, ref mn, ref mx);
            }
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
                MetaMesh[] rm = MeshesOf(src, a, out rs);
                raw.Add(new PlanNode { Parent = -1, Local = MatrixFrame.Identity, Meshes = rm, NoSeason = (src.EntityFlags & EntityFlags.NotAffectedBySeason) != 0, Src = SrcPrefab, Name = src.Name ?? "" });
                top = 0;
            }
            Walk(src, top, a, raw, 0, includeRoot);
            DropDuplicateStandIns(raw, a);
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

        /// <summary>Zastepca grupy domow (lisc po nazwie), ktory jest tym samym domem co dom z rozwinietego prefabu (ta sama siatka / nazwa,
        /// srodek w XY korzenia blizej niz MapVillageData.SameHouseTol) - zdjety (bez siatek; galaz wytnie BuildPlan). Inne liscie po nazwie
        /// (ikony Calradii / Essos, domy spoza prefabow) zostaja.</summary>
        private static void DropDuplicateStandIns(List<PlanNode> raw, WalkArgs a)
        {
            bool anyPrefab = false, anyName = false;
            foreach (var n in raw) { if (n.Meshes.Length == 0) continue; if (n.Src == SrcPrefab) anyPrefab = true; else if (n.Src == SrcName) anyName = true; }
            if (!anyPrefab || !anyName) return;
            var px = new float[raw.Count];
            var py = new float[raw.Count];
            var mesh = new string[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                Vec3 p = raw[i].Local.origin;
                int up = raw[i].Parent;
                int guard = 0;
                while (up >= 0 && up < raw.Count && guard++ < 16) { p = raw[up].Local.TransformToParent(in p); up = raw[up].Parent; }
                px[i] = p.x;
                py[i] = p.y;
                mesh[i] = raw[i].Meshes.Length > 0 ? raw[i].Meshes[0].GetName() ?? "" : "";
            }
            for (int i = 0; i < raw.Count; i++)
            {
                var n = raw[i];
                if (n.Src != SrcName || n.Meshes.Length == 0) continue;
                for (int j = 0; j < raw.Count; j++)
                {
                    var o = raw[j];
                    if (j == i || o.Src != SrcPrefab || o.Meshes.Length == 0) continue;
                    if (!MapVillageData.SameHouse(mesh[i], n.Name, px[i], py[i], mesh[j], o.Name, px[j], py[j])) continue;
                    n.Meshes = NoMeshes;
                    a.DupDropped++;
                    break;
                }
            }
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
                if ((a.Prefabs || a.Expand) && !inPrefab)
                {
                    // droga 2 (a.Prefabs): encja z nazwa prefabu; droga 1 (a.Expand): tylko encja zwinieta przez Town Scene Manager (0 dzieci,
                    // 0 komponentow MetaMesh), ktora nie jest sama domem po nazwie. Zamiast jej dzieci w grze - kopia prefabu bez sceny
                    // w ramce tej encji, z niej WSZYSTKIE siatki rekurencyjnie (ramki lokalne z prefabu).
                    string pn = PrefabNameFor(name);
                    bool take = a.Prefabs || MapVillageData.ExpandCollapsed(name, c.ChildCount, c.MultiMeshComponentCount, pn);
                    GameEntity tmp = take ? PrefabTemplate(pn) : null;
                    int pm;
                    if (tmp != (GameEntity)null && (a.Prefabs || (_prefabMeshes.TryGetValue(pn, out pm) && pm > 0)))
                    {
                        int ts;
                        MetaMesh[] tm = MeshesOf(tmp, a, out ts);
                        int pi = outList.Count;
                        outList.Add(new PlanNode { Parent = parent, Local = lf, Meshes = tm, NoSeason = noSeason, Src = SrcPrefab, Name = name });
                        Walk(tmp, pi, a, outList, depth + 1, true);
                        if (a.Expand) a.Expanded++;
                        continue;
                    }
                }
                int s;
                MetaMesh[] meshes = MeshesOf(c, a, out s);
                if (meshes.Length > 0 && c.ChildCount == 0)
                {
                    string sig = meshes[0].GetName() + "|" + Math.Round(lf.origin.x, 2).ToString(CultureInfo.InvariantCulture) + "|"
                                 + Math.Round(lf.origin.y, 2).ToString(CultureInfo.InvariantCulture) + "|" + Math.Round(lf.origin.z, 2).ToString(CultureInfo.InvariantCulture);
                    if (!seen.Add(sig)) continue;
                }
                int idx = outList.Count;
                outList.Add(new PlanNode { Parent = parent, Local = lf, Meshes = meshes, NoSeason = noSeason, Src = inPrefab ? SrcPrefab : s, Name = name });
                Walk(c, idx, a, outList, depth + 1, inPrefab);
            }
        }

        /// <summary>Siatki encji: komponenty MetaMesh, a gdy ich brak (Town Scene Manager) - jedna siatka po nazwie encji. Siatka bez
        /// materialu pominieta - liczona w a.MatSkipped (z samym domyslnym materialem silnika zostaje, liczona w MaterialOk).</summary>
        private MetaMesh[] MeshesOf(GameEntity e, WalkArgs a, out int src)
        {
            src = SrcComp;
            List<MetaMesh> list = null;
            int mc = e.MultiMeshComponentCount;
            for (int i = 0; i < mc; i++)
            {
                MetaMesh mm = e.GetMetaMesh(i);
                if (mm == null || !mm.IsValid) continue;
                if (!MaterialOk(mm)) { a.MatSkipped++; continue; }
                if (list == null) list = new List<MetaMesh>();
                list.Add(mm);
            }
            if (list != null) return list.ToArray();
            if (mc > 0) return NoMeshes;   // komponenty byly, ale bez materialu - nie zgadujemy siatki po nazwie
            MetaMesh byName = MeshByName(e.Name, e.ChildCount == 0);
            if (byName == null) return NoMeshes;
            if (!MaterialOk(byName)) { a.MatSkipped++; return NoMeshes; }
            src = SrcName;
            return new[] { byName };
        }

        private static string _defaultMaterial;

        private static string DefaultMaterialName()
        {
            if (_defaultMaterial != null) return _defaultMaterial;
            try { Material dm = Material.GetDefaultMaterial(); _defaultMaterial = dm != null && dm.IsValid ? dm.Name ?? "" : ""; }
            catch { _defaultMaterial = ""; }
            return _defaultMaterial;
        }

        /// <summary>Czy siatka ma material: co najmniej jedna podsiatka z waznym materialem o niepustej nazwie. Raz na nazwe siatki. Siatka
        /// z samym domyslnym materialem silnika (Material.GetDefaultMaterial) ZOSTAJE (nie ryzykujemy zgaszenia wszystkich wiosek przez
        /// zly test), ale jest liczona (_matDefault) i widac ja w liscie siatek wzoru w wioski.log. Siatki domow ROT (fm_wm_*) maja w paczce
        /// ROT-Content\pack2.tpac wlasny material first_men_modular_mat1 (tekstury first_men_modular_mat1_d / _s).</summary>
        private bool MaterialOk(MetaMesh mm)
        {
            string key = mm.GetName() ?? "";
            bool ok;
            if (_matOk.TryGetValue(key, out ok)) return ok;
            ok = true;   // blad odczytu = nie odrzucamy
            try
            {
                string def = DefaultMaterialName();
                int n = mm.MeshCount;
                bool any = false, onlyDefault = true;
                for (int k = 0; k < n; k++)
                {
                    Mesh sm = mm.GetMeshAtIndex(k);
                    if (sm == null || !sm.IsValid) continue;
                    Material mat = sm.GetMaterial();
                    if (mat == null || !mat.IsValid) continue;
                    string mn = mat.Name ?? "";
                    if (mn.Length == 0) continue;
                    any = true;
                    if (mn != def) onlyDefault = false;
                }
                ok = any;
                if (any && onlyDefault && def.Length > 0) _matDefault++;
            }
            catch (Exception e)
            {
                ok = true;
                _stDiag++;
                if (_stDiag <= 3) Log.Error("MapVillagesView.MaterialOk " + key + " (potkniecie " + _stDiag + ")", e);
            }
            _matOk[key] = ok;
            if (!ok) _matBad++;
            return ok;
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

        /// <summary>Prefab o NAZWIE ENCJI (albo null): GameEntity.GetPrefabName / GetOldPrefabName NIE sa wolane nigdzie (08.10: odczyt na
        /// bo_village i *_village_looted konczyl sie naruszeniem pamieci w silniku - w wioski.log 08.10 linie tych encji urwane po nazwie).
        /// W scenie ROT 8.1.8 kazda encja prefabu domow wsi ma nazwe = nazwa prefabu (265 linkow fm_village* / andal_village* + 42 ze
        /// starym linkiem andal_village*: 307 / 307, proba-0810). Pomocniki nigdy; PrefabExists raz na nazwe.</summary>
        private string PrefabNameFor(string entityName)
        {
            if (string.IsNullOrEmpty(entityName) || MapVillageData.IsHelperName(entityName)) return null;
            bool ex;
            if (!_prefabExists.TryGetValue(entityName, out ex))
            {
                ex = false;
                try { ex = GameEntity.PrefabExists(entityName); } catch { ex = false; }
                _prefabExists[entityName] = ex;
            }
            return ex ? entityName : null;
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
            _prefabMeshes[name] = meshes;       // droga 1 rozwija zwinieta encje tylko prefabem z siatkami
            // 08.10: BEZ drzewa kopii w logu - TreeText na kopii bez sceny skonczyl sie u Jeffa naruszeniem pamieci (CrashScribe 02:07:38;
            // gettery widocznosci / BB / pierwszej siatki na encji bez sceny). Na kopii tylko to, co przeszlo: GetChildren, Name, GetFrame,
            // MultiMeshComponentCount, GetMetaMesh, ChildCount, GetUpgradeLevelMask, EntityFlags, GetChildrenRecursive.
            if (_diagPrefabs < 3 && _diagOn)
            {
                _diagPrefabs++;
                Log.Info("Wioski: drzewo prefabu " + name + " (kopia bez sceny): encji " + all.Count + ", komponentow MetaMesh " + meshes + ".");
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

        /// <summary>Wzor wsi-matki do wioski.log (poprawka wygladu 08.10): naglowek (droga, siatki poziomu 3 wedlug zrodla, rozwiniete prefaby,
        /// duplikaty zastepcow, bez materialu, BB wzoru, BB matki, dopasowanie, skala obrazka na poziomach) i linia na kazda siatke poziomu 3
        /// (encja, siatka, podsiatki, material / shader / tekstura pierwszej podsiatki, czynnik koloru, zrodlo, ramka w ukladzie wzoru, BB).
        /// Kazda linia osobnym wpisem (zrzut 08.10 mial w srodku drzew linie z sama nazwa encji). Tylko odczyt.</summary>
        private void DiagTemplate(District d)
        {
            var t = d.T;
            string id = d.Id;
            try
            {
                var p = t.Plans[3] ?? new PlanNode[0];
                int nodes = 0, meshes = 0;
                foreach (var n in p) if (n.Meshes.Length > 0) { nodes++; meshes += n.Meshes.Length; }
                float l3 = MapVillageData.LevelScale(3) * t.Fit;
                var sb = new StringBuilder();
                sb.Append("Wioski: drzewo wzoru ").Append(id).Append(" '").Append(d.S != null ? d.S.Name.ToString() : "?").Append("' kultura ")
                  .Append(d.S != null && d.S.Culture != null ? d.S.Culture.StringId : "?").Append(": droga ")
                  .Append(t.Way == WayTree ? "drzewo" : t.Way == WayPrefab ? "prefaby" : "kultura " + t.WaySource).Append(t.Lenient ? " (zapas maski)" : "")
                  .Append("; poziom 3: encji z siatka ").Append(nodes).Append(", siatek ").Append(meshes)
                  .Append(" (z prefabow ").Append(t.FromPrefab).Append(", po nazwie ").Append(t.FromName).Append(", z komponentow ").Append(t.FromComp)
                  .Append("); rozwiniete zwiniete prefaby ").Append(t.Expanded).Append(", zdjete duplikaty zastepcow ").Append(t.DupDropped)
                  .Append(", pominiete bez materialu ").Append(t.MatSkipped)
                  .Append("; liscie ").Append(t.LeafCount).Append(t.Flat ? " (jeden wyglad 1/2/3, przerzedzenie)" : "")
                  .Append("; BB wzoru w skali matki ").Append(F2(t.TplW)).Append('x').Append(F2(t.TplD)).Append('x').Append(F2(t.TplH))
                  .Append("; BB matki: widoczne domy ").Append(F2(t.MotherW)).Append('x').Append(F2(t.MotherD)).Append(t.MotherFromAll ? " (brak - cala matka, dopasowanie 1)" : "")
                  .Append(", cala matka ").Append(F2(t.MotherAllW)).Append('x').Append(F2(t.MotherAllD))
                  .Append("; dopasowanie ").Append(F2(t.Fit)).Append("; skala obrazka (x skala matki) poziom 1/2/3 ")
                  .Append(F2(MapVillageData.LevelScale(1) * t.Fit)).Append('/').Append(F2(MapVillageData.LevelScale(2) * t.Fit)).Append('/').Append(F2(l3))
                  .Append(" = obrazek poziomu 3 ok. ").Append(F2(t.TplW * l3)).Append('x').Append(F2(t.TplD * l3))
                  .Append("; material domyslny silnika '").Append(DefaultMaterialName()).Append('\'');
                Log.Info(sb.ToString());
                var frames = PlanFrames(p);
                int k = 0;
                for (int i = 0; i < p.Length; i++)
                {
                    var n = p[i];
                    if (n.Meshes.Length == 0) continue;
                    k++;
                    for (int m = 0; m < n.Meshes.Length; m++)
                    {
                        string line;
                        try { line = MeshLine(n, n.Meshes[m], frames[i]); }
                        catch (Exception ex) { line = (n.Name ?? "?") + " | blad odczytu " + ex.GetType().Name + ": " + ex.Message; }
                        Log.Info("Wioski: drzewo wzoru " + id + " siatka " + k + "/" + nodes + (n.Meshes.Length > 1 ? "." + (m + 1) : "") + ": " + line);
                    }
                }
            }
            catch (Exception e)
            {
                _stDiag++;
                if (_stDiag <= 3) Log.Error("MapVillagesView.DiagTemplate " + id + " (potkniecie " + _stDiag + ")", e);
            }
        }

        /// <summary>Rodzaj wzoru do diagnostyki autotestu: droga + posortowane nazwy siatek poziomu 3 (np. fm_village1/2 = jeden rodzaj,
        /// fm_village3/4 = drugi, ikony wsi kazdej kultury osobno). Tylko nazwy siatek (MetaMesh.GetName - jak Signature).</summary>
        private static string TemplateKind(Template t)
        {
            var names = new List<string>();
            var p = t.Plans[3] ?? new PlanNode[0];
            foreach (var n in p)
                foreach (var m in n.Meshes)
                {
                    string nm = m.GetName() ?? "";
                    if (!names.Contains(nm)) names.Add(nm);
                }
            names.Sort(StringComparer.Ordinal);
            return t.Way + ":" + string.Join(",", names.ToArray());
        }

        private static string MeshLine(PlanNode n, MetaMesh mm, MatrixFrame f)
        {
            var sb = new StringBuilder();
            sb.Append(n.Name.Length > 0 ? n.Name : "(bez nazwy)").Append(" | siatka ").Append(mm.GetName());
            int subs = mm.MeshCount;
            sb.Append(" (podsiatek ").Append(subs).Append(')');
            Mesh sm = subs > 0 ? mm.GetMeshAtIndex(0) : null;
            Material mat = sm != null && sm.IsValid ? sm.GetMaterial() : null;
            if (mat != null && mat.IsValid)
            {
                sb.Append(" | material ").Append(mat.Name);
                // jak gra (MapScreen.CheckValidityOfItems :1490-1494): GetMeshAtIndex -> GetMaterial().Name i GetTexture(slot) != null; bez GetShader
                Texture tx = mat.GetTexture(Material.MBTextureType.DiffuseMap);
                sb.Append(" tekstura ").Append(tx != null && tx.IsValid ? "jest" : "BRAK");
            }
            else sb.Append(" | material BRAK");
            if (subs > 1)
            {
                var names = new List<string>();
                for (int k = 1; k < subs && k < 4; k++)
                {
                    Mesh s2 = mm.GetMeshAtIndex(k);
                    Material m2 = s2 != null && s2.IsValid ? s2.GetMaterial() : null;
                    names.Add(m2 != null && m2.IsValid ? m2.Name : "BRAK");
                }
                sb.Append(" (dalsze ").Append(string.Join(",", names.ToArray())).Append(')');
            }
            sb.Append(" | czynnik ").Append(mm.GetFactor1().ToString("X8"));
            sb.Append(" | zrodlo ").Append(n.Src == SrcPrefab ? "prefab" : n.Src == SrcName ? "nazwa" : "komponent");
            Vec3 scl = f.rotation.GetScaleVector();
            Vec3 fwd = f.rotation.f;
            double yaw = Math.Atan2(-fwd.x, fwd.y) * 180.0 / Math.PI;
            sb.Append(" | ramka we wzorze ").Append(F2(f.origin.x)).Append(',').Append(F2(f.origin.y)).Append(',').Append(F2(f.origin.z))
              .Append(" obrot ").Append(((float)yaw).ToString("0", CultureInfo.InvariantCulture)).Append(" skala ").Append(F2(scl.x));
            Vec3 b0, b1;
            if (MeshBox(mm, out b0, out b1))
                sb.Append(" | BB siatki ").Append(F2(b1.x - b0.x)).Append('x').Append(F2(b1.y - b0.y)).Append('x').Append(F2(b1.z - b0.z))
                  .Append(" (w skali ").Append(F2((b1.x - b0.x) * scl.x)).Append('x').Append(F2((b1.y - b0.y) * scl.y)).Append(')');
            else sb.Append(" | BB siatki brak");
            return sb.ToString();
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
            // pomocniki (bo_*, *_looted, siege_*, czastki, produkcja) bez zadnych odczytow z silnika - wzor ich nie uzywa, a na bo_village /
            // *_village_looted odczyt nazwy prefabu konczyl sie naruszeniem pamieci (08.10)
            if (MapVillageData.IsHelperName(name)) { sb.Append(" | pomocnik (bez odczytu, bez dzieci)"); return; }
            try { TreeDetails(sb, e, name); }
            catch (Exception ex) { sb.Append(" | blad odczytu ").Append(ex.GetType().Name).Append(": ").Append(ex.Message); }
            foreach (GameEntity c in e.GetChildren())
            {
                if (c == (GameEntity)null) continue;
                TreeLine(sb, c, depth + 1, ref lines, maxLines);
            }
        }

        private static void TreeDetails(StringBuilder sb, GameEntity e, string name)
        {
            // bez GetPrefabName / GetOldPrefabName (naruszenie pamieci na bo_village / *_village_looted, 08.10) - tylko: jest prefab o nazwie encji
            bool pfx = false;
            try { pfx = !string.IsNullOrEmpty(name) && GameEntity.PrefabExists(name); } catch { }
            if (pfx) sb.Append(" | prefab o tej nazwie");
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
                s.Placed = false;
            }
            _shown.Clear();
            _created = 0;
            foreach (var d in _districts) { d.T = null; d.TemplateTried = false; d.Kits = null; }   // wzory (i wzory obrazkow v4) od nowa (siatki matek moga byc przeladowane)
            _templatesOk = _templatesBad = _lenient = _thinned = _axisTurned = 0;   // liczniki "teraz" razem z wzorami (bez podwojnego liczenia po wlaczeniu w MCM)
            ClearTemplateCaches();
            return n;
        }

        private void DropAll()
        {
            foreach (var s in _slots) { s.Root = null; s.V = null; s.Shown = false; s.Placed = false; }
            foreach (var d in _districts) { d.T = null; d.TemplateTried = false; d.Kits = null; }
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
            _prefabMeshes.Clear();
            _prefabExists.Clear();
            _matOk.Clear();
            _tplComp = _tplName = _tplExpand = _tplPrefab = _tplCulture = _meshComp = _meshName = _meshPrefab = 0;
            _prefabMade = _prefabNoMesh = _prefabMissing = _nameMiss = 0;
            _expandedTotal = _dupDropped = _matSkipped = _matBad = _matDefault = _fitN = 0;
            _fitSum = _fitMax = 0f;
            _fitMin = float.MaxValue;
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
                            float cx = s.Placed ? s.Cx : s.R.X, cy = s.Placed ? s.Cy : s.R.Y;   // v4: srodek po przesunieciu ze stoku
                            if (!MapVillageData.InFootprintDeg(s.R, s.Placed ? s.Deg : s.R.FrontDeg, cx, cy, px, py, HoverMargin)) continue;
                            float dx = cx - px, dy = cy - py, dd = dx * dx + dy * dy;
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

            public override CampaignVec2 InteractionPositionForPlayer => new CampaignVec2(_s.Placed ? new Vec2(_s.Cx, _s.Cy) : new Vec2(_s.R.X, _s.R.Y), true);
            public override MapEntityVisual AttachedTo => null;
            public override bool IsMobileEntity => true;            // MapScreen.cs:1608 - bez kursora "raczki", wioski nie da sie kliknac
            public override bool OnMapClick(bool followModifierUsed) { return false; }
            public override void OnOpenEncyclopedia() { }
            public override bool IsVisibleOrFadingOut() { return _s.Shown; }
            public override Vec3 GetVisualPosition() { return _s.Placed ? new Vec3(_s.Cx, _s.Cy, _s.Z) : new Vec3(_s.R.X, _s.R.Y, _s.Z); }

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
