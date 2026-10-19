// f11-drogi (Jeff 18.10, ok. 09:40 PDT: "a drogi do wiosek odchodzace od glownego traktu, jesli wioska jest blisko traktu to nie rob do niej drogi, bo to bedzie
// wygladalo jak jakas krotka kreska, tylko dalsze wioski, chyba ze potrafisz to tak namalowac na mapie, aby to dobrze wygladalo?") - BOCZNE DROGI NA MAPIE.
// WIDOK: drogi nie zmieniaja tras partii, ekonomii ani zapisu gry. n20-drogi (noc 19/20.10): plan drog bocznych idzie tez do premii predkosci MapRoads
// (MapRoads.SidePublish / SideClear; klucz MapSideRoadSpeedBonus) - partia na drodze bocznej +7.5% (wpis "Side road").
//
// Co i dlaczego tak (proba 14.10 - decale sciezek osad na 2 oknach, Jeff: "drogi wygladaja fatalnie" - zdjecia D:\Backup-Bannerlord\zdjecia-drogi-2026-10-14:
// cienkie, jasne, prawie biale kreski z twardym brzegiem (material sciezki osady: alpha test, bez mieszania), lamane zygzaki z rastra, droga do kazdej wioski):
//  - material: mainmap_decal_undervillage - ziemia pod wioskami mapy (Native map_icon_parts.xml prefab map_decal_undervillage; ROT-Map stawia go 35 razy pod
//    wsiami z kolorem factor 4287727990 = FF918976), w materials.tpac: gbuffer_alpha_blend + render_on_terrain (miekki brzeg, maluje tylko teren - nie drzewa
//    ani domy); ten sam kolor co ziemia pod wioskami, wiec droga wchodzi w plac wioski bez szwu. Zapas: mainmap_decal_mud_a, decal_battania_path_a;
//  - decale BEZ encji (wzor gry na mapie: NavalDLC NavalMobilePartyVisual - piana za statkiem: Decal.CreateDecal -> SetMaterial -> Scene.AddDecalInstance
//    (decal, "editor_set", true) -> Frame -> SetAlpha; zdjecie Scene.RemoveDecalInstance(decal, "editor_set")) - zero encji, zero ticku silnika;
//  - drogi z planu (MapSideRoads.Plan.cs, czysta geometria): tylko dalsze wioski, siec od traktow, trasa po ladzie, krzywe zamiast zygzakow, stemple
//    zachodzace na siebie (pas bez przerw); plan liczy watek z puli (wejscie zbierane na watku glownym: wioski ukladu sesji z VillageBook - v6 / v7, wsie
//    gry, miasta i zamki), stawianie na watku glownym z budzetem czasu (8 ms na cwierc sekundy; w biegu zdjec autotestu od razu), RAZ na mape;
//  - stempel nad woda / brodem / mostem (sciana siatki nawigacyjnej nie ladowa - MapVillageData.IsLandGroup) pominiety: droga urywa sie na brzegu jak przy brodzie;
//  - co klatke tylko licznik czasu klatki (CLAUDE.md 7); praca 4 razy na sekunde: widocznosc (chowanie razem z wioskami, gdy kamera wyzej niz
//    min(MapVillagesHideAboveCameraHeight, 110) - zmiana tylko przy przejsciu progu), zmiana ukladu wiosek (crc - drogi od nowa), linia pomiaru co minute;
//  - potkniecia liczone (zly stempel pominiety), nigdy globalny wylacznik.
// Wylacznik: klucz MCM "Map Side Roads" (MapSideRoads, DOMYSLNIE NIE - Jeff decyduje po zdjeciach). Przy NIE typ widoku sie nie laduje (SubModuleMain), jedna
// linia przy starcie sesji. Autotest: plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-drogi-boczne.flag (tylko w trybie autotestu CrashScribe,
// LogGate 1 / 2 - nigdy w grze Jeffa) wlacza drogi na ten bieg; jego tresc (linie klucz=wartosc, opcjonalnie) zmienia parametry planu i wyglad do zdjec bez
// przebudowy DLL: NearExtra, NearHalfFactor, MaxHop, MaxNet, MinLen, Width, StampHalfLen, Spacing, Meander, ObstR, VillageAvoidR, MaxRoads, MaxStamps, Seed,
// material=..., factor=FF918976, alpha=0..1, hide=wysokosc kamery, argument=a,b,c,d (atlas materialu); f19: AllVillages (1/0 - nadpisuje klucz), GameLinkMax,
// HubLinkMax, OnPaintPad, MergeK, MergeEdge, MinLenAll, HubR, SpacingOnlyMax.
// f19-drogi (Jeff 18.10 wieczorem: "kazda wioska powinna miec droge do siebie"): klucz MapSideRoadsAllVillages (DOMYSLNIE TAK) - plan MapSideRoads.All.cs: kazda
// wioska ukladu dostaje droge (siec od traktow, miast, zamkow i wsi gry, kolejnosc jak u Prima, laczka pod katem przy trakcie); NIE = plan f11. Wejscie to samo
// (+ obrys obrazka i nazwa wioski z ukladu); przelaczenie klucza na mapie = decale zdjete i plan od nowa (raz na zmiane).
// f22-drogi-perf (Jeff 18.10: "przy max x8 predkosci strasznie gra zwalnia i sie przycina tam gdzie jest duzo jednostek"): klucz MapSideRoadsChunks (DOMYSLNIE TAK) -
// MapSideRoads.Chunks.cs: w scenie tylko komorki 32 x 32 jedn. w kole widoku kamery (zamiast ok. 74 tys. decali caly czas - zwykle 1.5-3.5 tys.); podzial na komorki
// liczy watek z puli razem z planem; NIE = droga ponizej co do znaku (wszystkie decale, chowane z wioskami). Przelaczenie na mapie - decale od nowa, plan ten sam.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Text;
using System.Threading;
using SandBox.View;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>Dane i wejscie bocznych drog (bez typow SandBox.View - bezpieczne do wolania co klatke z SubModuleMain i z ArmouryBehavior).</summary>
    internal static partial class MapSideRoads
    {
        internal const string GridFile = "arm_map_side_roads_grid.tsv";
        internal const string MainFile = "arm_map_roads_geo.tsv";
        internal const string FlagName = "armoury-drogi-boczne.flag";
        internal const string P = "Drogi boczne (f11): ";
        internal const string DefaultMaterial = "mainmap_decal_undervillage";
        internal const uint DefaultFactor = 0xFF7E7466u;         // ciemniejsza, szarobura ziemia (wariant A 18.10); ROT-Map pod wsiami ma FF918976
        internal static readonly string[] FallbackMaterials = { "mainmap_decal_mud_a", "decal_battania_path_a" };
        internal const float DefaultHideZ = 110f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Wyglad stempli (domyslny albo z pliku-przelacznika autotestu).</summary>
        internal sealed class Look
        {
            public string Material = DefaultMaterial;
            public uint Factor = DefaultFactor;
            public float Alpha = 0.55f;          // polprzezroczyste - gesto nalozone stemple daja miekki ciagly pas (wariant A 18.10)
            public float HideZ = DefaultHideZ;
            public bool HasArg;
            public float A0 = 1f, A1 = 1f, A2, A3;

            internal string Text()
            {
                return "material '" + Material + "', kolor " + Factor.ToString("X8", Inv) + ", alfa " + Alpha.ToString("0.00", Inv) + ", chowane razem z wioskami / powyzej z "
                       + HideZ.ToString("0", Inv) + (HasArg ? ", atlas " + A0.ToString("0.###", Inv) + "," + A1.ToString("0.###", Inv) + "," + A2.ToString("0.###", Inv) + "," + A3.ToString("0.###", Inv) : "");
            }
        }

        // ---------- przelaczniki ----------
        private static int _flag = -1;      // -1 nie sprawdzony w tej sesji, 0 nie, 1 tak
        private static string _flagText = "";

        internal static bool Key { get { var s = Settings.Current; return s != null && s.MapSideRoads; } }

        /// <summary>f19-drogi: klucz MapSideRoadsAllVillages (kazda wioska ukladu ma droge; NIE = plan f11). Bez ustawien - TAK (domyslna klucza).</summary>
        internal static bool KeyAll { get { var s = Settings.Current; return s == null || s.MapSideRoadsAllVillages; } }

        /// <summary>Drogi boczne czynne: klucz MCM albo plik-przelacznik autotestu (tylko w kampanii).</summary>
        internal static bool On { get { return Key || AutotestFlag; } }

        /// <summary>Plik Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-drogi-boczne.flag - tylko w trybie autotestu CrashScribe (LogGate 1 pelne / 2 profil),
        /// nigdy w grze Jeffa; sprawdzany raz na sesje (SessionStart zeruje); poza kampania zawsze false (bez pytania dysku co klatke w menu).</summary>
        internal static bool AutotestFlag
        {
            get
            {
                if (_flag >= 0) return _flag == 1;
                if (Campaign.Current == null) return false;
                bool f = false;
                string text = "";
                try
                {
                    int m = LogGate.Mode;
                    if (m == 1 || m == 2)
                    {
                        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string path = Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), FlagName);
                        if (File.Exists(path))
                        {
                            f = true;
                            try { text = File.ReadAllText(path, Encoding.UTF8); } catch { text = ""; }
                            if (text.Length > 4000) text = text.Substring(0, 4000);
                        }
                    }
                }
                catch { f = false; }
                _flagText = text;
                _flag = f ? 1 : 0;
                return f;
            }
        }

        /// <summary>Start sesji (OnSessionLaunched, PO McmSettings.Apply): plik-przelacznik od nowa i jedna linia stanu.</summary>
        internal static void SessionStart()
        {
            _flag = -1;
            _flagText = "";
            bool flag = AutotestFlag, key = Key;
            if (!key && !flag)
            {
                Log.Info(P + "wylaczone (klucz MapSideRoads = NIE, bez pliku-przelacznika autotestu) - widok bocznych drog sie nie laduje, nic nie stawiam.");
                return;
            }
            var p = new SideRoadPlan.Params { AllVillages = KeyAll ? 1 : 0 };   // f19: regula z klucza (plik-przelacznik autotestu moze nadpisac)
            var look = new Look();
            string note = ApplyFlag(p, look);
            p.Clamp();
            Log.Info(P + "wlaczone (" + (key ? "klucz MapSideRoads = TAK" : "klucz NIE") + (flag ? ", plik-przelacznik autotestu " + FlagName + " jest" + (note.Length > 0 ? " (" + note + ")" : " (bez zmian parametrow)") : "")
                     + ") - plan: " + p.Text() + "; wyglad: " + look.Text() + "; komorki f22 (klucz MapSideRoadsChunks): "
                     + (KeyChunks ? "TAK - w scenie tylko komorki " + ChunkCell.ToString("0", Inv) + " jedn. wokol widoku kamery, najdalej " + ChunkCap.ToString("0", Inv) + " jedn. od kamery (nie blizej niz rog ekranu przy zwyklym pochyleniu)"
                                  : "NIE - wszystkie decale w scenie, chowane z wioskami")
                     + ". Drogi postawi widok mapy (raz) - bez zmian tras i zapisu gry; premia predkosci drog bocznych (klucz MapSideRoadSpeedBonus " + (Settings.Current != null && Settings.Current.MapSideRoadSpeedBonus ? "TAK" : "NIE") + ") po planie (linia Drogi (DR): drogi boczne w premii).");
        }

        /// <summary>Parametry planu i wyglad z tresci pliku-przelacznika (linie klucz=wartosc, '#' - komentarz); zwraca opis zmian ("" - bez zmian).</summary>
        internal static string ApplyFlag(SideRoadPlan.Params p, Look look)
        {
            if (_flag != 1 || string.IsNullOrEmpty(_flagText)) return "";
            var sb = new StringBuilder();
            foreach (string raw in _flagText.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                bool ok = false;
                try
                {
                    switch (k.ToLowerInvariant())
                    {
                        case "material": if (v.Length > 0 && v.Length < 80) { look.Material = v; ok = true; } break;
                        case "factor":
                            uint fc;
                            if (uint.TryParse(v.Replace("0x", "").Replace("#", ""), NumberStyles.HexNumber, Inv, out fc)) { look.Factor = fc; ok = true; }
                            break;
                        case "alpha": float al; if (float.TryParse(v, NumberStyles.Float, Inv, out al)) { look.Alpha = SideRoadPlan.Cl(al, 0.05f, 1f); ok = true; } break;
                        case "hide": float hz; if (float.TryParse(v, NumberStyles.Float, Inv, out hz)) { look.HideZ = SideRoadPlan.Cl(hz, 10f, 400f); ok = true; } break;
                        case "argument":
                            string[] a = v.Split(',');
                            float a0, a1, a2, a3;
                            if (a.Length == 4 && float.TryParse(a[0].Trim(), NumberStyles.Float, Inv, out a0) && float.TryParse(a[1].Trim(), NumberStyles.Float, Inv, out a1)
                                && float.TryParse(a[2].Trim(), NumberStyles.Float, Inv, out a2) && float.TryParse(a[3].Trim(), NumberStyles.Float, Inv, out a3))
                            { look.HasArg = true; look.A0 = a0; look.A1 = a1; look.A2 = a2; look.A3 = a3; ok = true; }
                            break;
                        default:
                            FieldInfo fi = null;
                            foreach (var f in typeof(SideRoadPlan.Params).GetFields(BindingFlags.Public | BindingFlags.Instance))
                                if (string.Equals(f.Name, k, StringComparison.OrdinalIgnoreCase)) { fi = f; break; }
                            if (fi == null) break;
                            if (fi.FieldType == typeof(float)) { float fv; if (float.TryParse(v, NumberStyles.Float, Inv, out fv)) { fi.SetValue(p, fv); ok = true; } }
                            else if (fi.FieldType == typeof(int)) { int iv; if (int.TryParse(v, NumberStyles.Integer, Inv, out iv)) { fi.SetValue(p, iv); ok = true; } }
                            break;
                    }
                }
                catch { ok = false; }
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(k).Append('=').Append(v).Append(ok ? "" : " (NIEZNANE / ZLE - pominiete)");
            }
            return sb.ToString();
        }

        // ---------- plan: wejscie na watku glownym, liczenie na watku z puli ----------
        internal sealed class Job
        {
            public volatile bool Done;
            public SideRoadPlan.Result Res;
            public string Error = "";
            public string Note = "";            // siatka, trakty, straznik mapy (watek z puli)
            public string Input = "";           // wioski, wsie gry, miasta (watek glowny)
            public double MainMs, WorkMs;
            public bool HasLayout;
            public uint LayoutCrc;
            public bool KeyAll;                 // f19: klucz MapSideRoadsAllVillages przy starcie planu (zmiana na mapie - plan od nowa)
            public SideRoadPlan.Params Params;
            public Look Look;
            public ChunkGrid Grid;              // f22: podzial stempli na komorki (watek z puli, po planie); null - plan nieudany albo podzial nieudany
            public float ChunkCap;              // f22: klucz MapSideRoadsChunkRadiusCap przy starcie planu (do linii oczekiwanych decali)
        }

        /// <summary>Zbiera wejscie (watek glowny: wioski ukladu sesji, wsie gry, miasta i zamki) i puszcza plan na watek z puli. Wioski ukladu tylko przy
        /// wlaczonych wioskach na mapie (MapVillagesEnabled - bez obrazkow droga prowadzilaby donikad).</summary>
        internal static Job Start()
        {
            var job = new Job { Params = new SideRoadPlan.Params(), Look = new Look(), KeyAll = KeyAll, ChunkCap = ChunkCap };
            job.Params.AllVillages = job.KeyAll ? 1 : 0;   // f19: regula z klucza; plik-przelacznik autotestu (ApplyFlag, AllVillages=0/1) moze nadpisac
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string fl = ApplyFlag(job.Params, job.Look);
            job.Params.Clamp();
            var nodes = new List<SideRoadPlan.Node>(3000);
            var obst = new List<SideRoadPlan.Obstacle>(300);
            int layoutN = 0, gameN = 0, townN = 0, castleN = 0, bad = 0;
            string layoutNote;
            var s = Settings.Current;
            if (s != null && s.MapVillagesEnabled)
            {
                uint crc;
                job.HasLayout = VillageBook.LayoutCrcNow(out crc);
                job.LayoutCrc = crc;
                var recs = VillageBook.LayoutRecords();
                if (recs != null)
                {
                    foreach (var r in recs)
                    {
                        if (r == null || float.IsNaN(r.X) || float.IsNaN(r.Y)) { bad++; continue; }
                        // f19: obrys obrazka (obrazek na farbie traktu = bez drogi) i nazwa (wioski bez trasy po ladzie w logu z nazwy)
                        nodes.Add(new SideRoadPlan.Node { X = r.X, Y = r.Y, Id = r.Uid ?? "", Game = false, Hl = r.HalfLen, Hw = r.HalfWid, FrontDeg = r.FrontDeg, Name = r.Name ?? "" });
                        layoutN++;
                    }
                    layoutNote = "wioski ukladu " + layoutN + " (" + VillageBook.LayoutFile + ", crc " + crc.ToString("X8", Inv) + ")";
                }
                else layoutNote = "uklad wiosek NIEWCZYTANY (ksiega wiosek) - tylko wsie gry";
            }
            else layoutNote = "wioski na mapie wylaczone (MapVillagesEnabled) - tylko wsie gry";
            var seen = new HashSet<Village>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    if (st.IsVillage && st.Village != null)
                    {
                        if (!seen.Add(st.Village)) continue;                 // ROT podaje dwie wsie dwa razy (jak VillageBook.Resolve)
                        Vec2 v = st.GetPosition2D;
                        nodes.Add(new SideRoadPlan.Node { X = v.x, Y = v.y, Id = st.StringId ?? "", Game = true, Name = RawName(st.Name) });   // f19: nazwa do logu
                        gameN++;
                    }
                    else if (st.IsTown || st.IsCastle)
                    {
                        Vec2 v = st.GetPosition2D;
                        obst.Add(new SideRoadPlan.Obstacle { X = v.x, Y = v.y });
                        if (st.IsTown) townN++; else castleN++;
                    }
                }
                catch { bad++; }
            }
            job.Input = layoutNote + ", wsie gry " + gameN + ", miasta " + townN + " i zamki " + castleN + " (przeszkody)" + (bad > 0 ? ", zle wiersze " + bad : "")
                        + (fl.Length > 0 ? "; plik-przelacznik: " + fl : "");
            job.MainMs = sw.Elapsed.TotalMilliseconds;
            string gridPath = MapVillageData.DataPathFor(GridFile), mainPath = MapVillageData.DataPathFor(MainFile), terrain = TerrainPath();
            try
            {
                ThreadPool.QueueUserWorkItem(_ => Work(job, nodes, obst, gridPath, mainPath, terrain));
            }
            catch (Exception e)
            {
                job.Error = "kolejka watkow: " + e.GetType().Name + " - plan na watku glownym";
                Work(job, nodes, obst, gridPath, mainPath, terrain);
            }
            return job;
        }

        /// <summary>f19: nazwa osady do logu bez przetwarzania tekstu gry (surowe TextObject.Value bez "{=id}") - tanio na watku glownym (ok. 570 wsi gry).</summary>
        private static string RawName(TaleWorlds.Localization.TextObject t)
        {
            string v = t != null ? t.Value : null;
            if (string.IsNullOrEmpty(v)) return "";
            if (v.StartsWith("{=", StringComparison.Ordinal)) { int e = v.IndexOf('}'); if (e > 0) v = v.Substring(e + 1); }
            return v;
        }

        private static void Work(Job job, List<SideRoadPlan.Node> nodes, List<SideRoadPlan.Obstacle> obst, string gridPath, string mainPath, string terrain)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (gridPath == null || !File.Exists(gridPath)) { job.Note = "BRAK pliku ModuleData\\" + GridFile + " - drog nie bedzie"; return; }
                if (mainPath == null || !File.Exists(mainPath)) { job.Note = "BRAK pliku ModuleData\\" + MainFile + " - drog nie bedzie"; return; }
                var g = SideRoadPlan.ParseGrid(File.ReadAllText(gridPath, Encoding.UTF8));
                var m = SideRoadPlan.ParseMainRoads(File.ReadAllText(mainPath, Encoding.UTF8));
                string guard;
                long tsize = terrain != null && File.Exists(terrain) ? new FileInfo(terrain).Length : -1;
                if (tsize > 0 && g.TerrainSize > 0 && tsize != g.TerrainSize)
                {
                    job.Note = "siatka " + GridFile + " z INNEJ MAPY: terrain.bin ROT-Map ma " + tsize + " B, siatka z " + g.TerrainSize + " B - drog nie bedzie (dane do odswiezenia: tools/drogi/drogi_boczne_siatka.py)";
                    return;
                }
                if (tsize > 0 && m.TerrainSize > 0 && tsize != m.TerrainSize)
                {
                    job.Note = "trakty " + MainFile + " z INNEJ MAPY (terrain.bin " + tsize + " B, plik " + m.TerrainSize + " B) - drog nie bedzie";
                    return;
                }
                guard = tsize > 0 ? "terrain.bin zgodny (" + tsize + " B)" : "terrain.bin nie znaleziony - bez straznika mapy";
                job.Note = "siatka v" + g.Version + " " + g.Nx + " x " + g.Ny + " po " + g.Cell.ToString("0.#", Inv) + " jedn. (lad " + g.Land + ", zle wiersze " + g.BadRows + "), trakty: lamanych "
                           + m.Lines + ", odcinkow " + m.Segments + " (zle wiersze " + m.BadRows + "); " + guard;
                job.Res = SideRoadPlan.Build(g, m, nodes, obst, job.Params);
                // f22: podzial na komorki i oczekiwane decale w scenie (czysta geometria, tu - nie na watku glownym); nieudany = komorek nie ma, widok idzie stara droga
                if (job.Res != null)
                {
                    try { job.Grid = GroupChunks(job.Res, nodes, job.ChunkCap); }
                    catch (Exception e) { job.Grid = null; job.Error = (job.Error.Length > 0 ? job.Error + "; " : "") + "komorki f22: " + e.GetType().Name + ": " + e.Message; }
                }
            }
            catch (Exception e) { job.Error = (job.Error.Length > 0 ? job.Error + "; " : "") + e.GetType().Name + ": " + e.Message; job.Res = null; }
            finally { job.WorkMs = sw.Elapsed.TotalMilliseconds; job.Done = true; }
        }

        private static string TerrainPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // Modules\Armoury\bin\Win64_Shipping_Client
                string p = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "ROT-Map", "SceneObj", "Main_map", "terrain.bin"));
                if (File.Exists(p)) return p;
            }
            catch { }
            try { return Path.Combine(BasePath.Name, "Modules", "ROT-Map", "SceneObj", "Main_map", "terrain.bin"); }
            catch { return null; }
        }
    }

    /// <summary>Widok bocznych drog (komponent mapy SandBox.View; zyje z MapScreen). Rejestracja: SubModuleMain.OnApplicationTick (tylko przy MapSideRoads.On).</summary>
    public sealed partial class MapSideRoadsView : EntityVisualManagerBase
    {
        public override int Priority => 106;   // po wioskach (100), zajazdach (101) i probie drog (105); nic nie trafia myszka

        private const float TickEvery = 0.25f;
        private const float SummaryEvery = 60f;
        private const float LayoutWaitS = 20f;          // uklad wiosek jeszcze niewczytany - czekamy najwyzej tyle, potem same wsie gry
        private const int Buckets = 256;
        private const string DecalSet = "editor_set";
        private static readonly long BuildBudgetTicks = System.Diagnostics.Stopwatch.Frequency * 8 / 1000;   // ok. 8 ms stawiania na cwierc sekundy
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private MapScreen _screen;
        private bool _finalized, _wasOff, _built, _visible = true, _visKnown, _loggedPlan, _failedPlan;
        private float _acc, _sinceSummary, _waited;
        private MapSideRoads.Job _job;
        private MapSideRoads.Job _speedJob;             // n20-drogi: plan, ktory widok podal do premii predkosci (MapRoads.SidePublish)
        private int _speedEpoch;                        // n20-drogi: MapRoads.Epoch przy podaniu (nowa sesja - podac od nowa)
        private Material _mat;
        private string _matName = "", _matNote = "";
        private readonly List<Decal> _decals = new List<Decal>(16000);
        private int _next;
        // pomiar
        private long _buildTicks, _maxTickTicks;
        private int _buildTickCount, _skipWater, _skipBad, _stCreate, _stTick, _stVis, _stRemove, _visChanges, _clearMem, _replans, _noMat;
        private readonly int[][] _hist = { new int[Buckets], new int[Buckets] };   // [0] drogi schowane, [1] drogi widoczne
        private readonly double[] _sumMs = new double[2];
        private string _lastSummary = "";

        // ---------- rejestracja (tani test co klatke, jak MapRoadsProbeView) ----------
        private static WeakReference _regVm, _regScreen;
        private static int _stRegister;

        internal static void EnsureRegistered()
        {
            try
            {
                if (!MapSideRoads.On) return;
                if (Campaign.Current == null) return;
                MapScreen screen = MapScreen.Instance;
                if (screen == null) return;
                SandBoxViewVisualManager vm = SandBoxViewSubModule.SandBoxViewVisualManager;
                if (vm == null) return;
                if (_regVm != null && ReferenceEquals(_regVm.Target, vm) && _regScreen != null && ReferenceEquals(_regScreen.Target, screen)) return;
                if (vm.GetEntityComponent<SettlementVisualManager>() == null) return;   // mapa jeszcze nie zbudowala swoich menedzerow
                var mine = vm.GetEntityComponent<MapSideRoadsView>();
                if (mine != null && !ReferenceEquals(mine._screen, screen))
                {
                    vm.RemoveEntityComponent(mine);   // komponent po innej mapie: jego OnFinalize nie rusza cudzej sceny
                    mine = null;
                }
                if (mine == null) vm.AddEntityComponent<MapSideRoadsView>();
                _regVm = new WeakReference(vm);
                _regScreen = new WeakReference(screen);
            }
            catch (Exception e)
            {
                _stRegister++;
                if (_stRegister <= 3 || _stRegister % 500 == 0) Log.Error("MapSideRoadsView.EnsureRegistered (potkniecie " + _stRegister + ")", e);
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _screen = MapScreen.Instance;
        }

        protected override void OnFinalize()
        {
            if (_finalized) { base.OnFinalize(); return; }
            _finalized = true;
            try
            {
                bool sceneAlive = _screen != null && ReferenceEquals(MapScreen.Instance, _screen);
                if (_decals.Count > 0 || _active.Count > 0 || _job != null)
                {
                    string sum = SummaryText();
                    int removed = sceneAlive ? RemoveAll() : DropAll();
                    Log.Info(MapSideRoads.P + "koniec mapy - zdjete decale " + removed + (sceneAlive ? "" : " (scena juz nie ta - same odwolania)") + "; " + sum);
                }
                SpeedClear("koniec mapy");   // n20-drogi: tylko gdy w premii jest plan tego widoku
            }
            catch (Exception e) { Log.Error("MapSideRoadsView.OnFinalize", e); }
            _job = null;
            _screen = null;
            base.OnFinalize();
        }

        public override void ClearVisualMemory()
        {
            _clearMem++;   // material wspolny z ziemia pod wioskami mapy - tylko liczymy
        }

        // ---------- co klatke: TYLKO licznik czasu klatki; praca 4 razy na sekunde ----------
        public override void OnVisualTick(MapScreen screen, float realDt, float dt)
        {
            if (_finalized) return;
            if (_built)
            {
                int b = (int)(realDt * 1000f);
                if (b < 0) b = 0; else if (b >= Buckets) b = Buckets - 1;
                int k = _visible ? 1 : 0;
                _hist[k][b]++;
                _sumMs[k] += realDt * 1000.0;
                if (_mode == 1) CountChunkFrame(b, realDt);   // f22: klatki wedlug liczby komorek w scenie
            }
            _acc += realDt;
            _sinceSummary += realDt;
            if (_acc < TickEvery) return;
            float step = _acc;
            _acc = 0f;
            try
            {
                MapRoads.FlushSideNote();   // n20-drogi: linia logu z watku budowy siatki premii drog bocznych
                if (!MapSideRoads.On)
                {
                    if (!_wasOff) { int n = RemoveAll(); _wasOff = true; Log.Info(MapSideRoads.P + "wylaczone w MCM - zdjete decale " + n + "."); SpeedClear("drogi boczne wylaczone w MCM"); }
                    return;
                }
                if (_wasOff)
                {
                    _wasOff = false; _next = 0; _built = false; _visKnown = false;
                    bool sameRule = _job == null || _job.KeyAll == MapSideRoads.KeyAll;   // f19: klucz przelaczony przy wylaczonych drogach - plan od nowa (KeyChanged ponizej)
                    Log.Info(MapSideRoads.P + "wlaczone w MCM - drogi wracaja (" + (sameRule ? "ten sam plan" : "regula 'kazda wioska' zmieniona - plan od nowa") + ").");
                }
                if (_job == null)
                {
                    if (_failedPlan) return;
                    // uklad wiosek tej sesji: czekamy na ksiege (VillageBook.Resolve w OnSessionLaunched) - najwyzej LayoutWaitS
                    uint crc;
                    var s = Settings.Current;
                    if (s != null && s.MapVillagesEnabled && !VillageBook.LayoutCrcNow(out crc) && _waited < LayoutWaitS) { _waited += step; return; }
                    _job = MapSideRoads.Start();
                    _loggedPlan = false;
                    return;
                }
                if (!_job.Done) return;
                if (!_loggedPlan) { _loggedPlan = true; LogPlan(); if (_job.Res == null) _failedPlan = true; }
                // f19 po recenzji: klucz MapSideRoadsAllVillages sprawdzany PRZED stawianiem - przelaczony przy wylaczonych drogach, w trakcie planu albo stawiania
                // nie stawia juz starego planu (wczesniej ok. 74 tys. decali szlo na mape i zaraz z niej schodzilo); takze po planie NIEUDANYM (inna regula = inny plan)
                if (KeyChanged()) return;
                if (_job.Res == null) return;
                // n20-drogi: drogi boczne do premii predkosci - raz na plan i raz na sesje (MapRoads.Reset przy starcie sesji zdejmuje plan); siatke buduje watek z puli
                if (!ReferenceEquals(_speedJob, _job) || _speedEpoch != MapRoads.Epoch)
                {
                    _speedJob = _job;
                    _speedEpoch = MapRoads.Epoch;
                    MapRoads.SidePublish(_job.Res, _job.Params != null ? _job.Params.Width : 0.5f);
                }
                // f22: tryb komorek (klucz MapSideRoadsChunks, podzial z watku z puli); przelaczony klucz - wszystko ze sceny i stawianie od nowa w nowym trybie
                bool chunks = MapSideRoads.KeyChunks && _job.Grid != null;
                if (_mode >= 0 && _mode != (chunks ? 1 : 0)) ModeChanged(chunks);
                if (chunks) { ChunkTick(screen); if (_built) CheckLayout(); }
                else
                {
                    _mode = 0;
                    // widocznosc takze PRZED kazda porcja stawiania: kamera wysoko = nowe decale od razu schowane (bez kilku sekund drog przy oddalonej kamerze);
                    // przelacza tylko juz postawione i tylko przy przejsciu progu
                    if (!_built) { UpdateVisibility(screen); Build(); }
                    if (_built) { UpdateVisibility(screen); CheckLayout(); }
                }
                if (_sinceSummary >= SummaryEvery)
                {
                    _sinceSummary = 0f;
                    string sum = SummaryText();
                    if (sum != _lastSummary) { _lastSummary = sum; Log.Info(MapSideRoads.P + sum); }
                }
            }
            catch (Exception e)
            {
                _stTick++;
                if (_stTick <= 3 || _stTick % 200 == 0) Log.Error("MapSideRoadsView.OnVisualTick (potkniecie " + _stTick + ")", e);
            }
        }

        private void LogPlan()
        {
            var j = _job;
            if (j.Res == null)
            {
                Log.Info(MapSideRoads.P + "plan NIEUDANY - " + (j.Note.Length > 0 ? j.Note : "-") + (j.Error.Length > 0 ? " | blad: " + j.Error : "") + " | wejscie: " + j.Input
                         + " - drog nie bedzie do nastepnego wczytania (gra bez zmian).");
                return;
            }
            Log.Info(MapSideRoads.P + "plan - " + j.Res.Text() + " | " + j.Note + " | wejscie: " + j.Input + " | parametry: " + j.Params.Text()
                     + " | czas: wejscie (watek glowny) " + j.MainMs.ToString("0.0", Inv) + " ms, plan (watek z puli) " + j.WorkMs.ToString("0", Inv) + " ms"
                     + (j.Error.Length > 0 ? " | uwaga: " + j.Error : "") + ".");
            Log.Info(MapSideRoads.P + "miejsca z najgestsza siecia (cele zdjec, kwadrat 24 jedn.): " + SideRoadPlan.DenseSpots(j.Res, 6) + ".");
        }

        // ---------- stawianie: raz, z budzetem czasu (bieg zdjec autotestu - od razu wszystko, zeby zdjecia zaraz po wejsciu na mape mialy drogi) ----------
        private bool PickMaterial()
        {
            if (_mat != null) return true;
            var look = _job.Look;
            var names = new List<string> { look.Material };
            foreach (var f in MapSideRoads.FallbackMaterials) if (!names.Contains(f)) names.Add(f);
            foreach (var n in names)
            {
                try
                {
                    Material m = Material.GetFromResource(n);
                    if ((object)m == null || !m.IsValid) continue;
                    string got = m.Name;
                    if (got == null || !string.Equals(got, n, StringComparison.OrdinalIgnoreCase)) continue;   // zastepczy material - nie
                    _mat = m; _matName = n;
                    _matNote = n == look.Material ? "" : " (ZAPAS - brak materialu '" + look.Material + "')";
                    return true;
                }
                catch { }
            }
            return false;
        }

        private void Build()
        {
            Scene scene = MapScene;
            var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
            if (scene == null || ms == null) return;
            bool rend = true;
            try { rend = scene.HasDecalRenderer(); } catch { }
            if (!rend) return;   // renderer decali jeszcze nie gotowy - nastepna cwierc sekundy (jak ShipWaterEffects)
            if (!PickMaterial())
            {
                _built = true; _noMat++;
                Log.Info(MapSideRoads.P + "BRAK materialu (" + _job.Look.Material + " ani zapasowych) - nic nie stawiam.");
                return;
            }
            var res = _job.Res;
            var look = _job.Look;
            bool now = MapSideRoads.AutotestFlag;   // bieg zdjec: bez budzetu (jedno stawianie zaraz po planie)
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int made = 0;
            bool hidden = _visKnown && !_visible;   // _visKnown ustawia UpdateVisibility tuz przed Build (brak kamery = jak dotad: widoczne)
            while (_next < res.Stamps.Count)
            {
                if (!now && made > 0 && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > BuildBudgetTicks) break;
                var st = res.Stamps[_next++];
                try
                {
                    var cv = new CampaignVec2(new Vec2(st.X, st.Y), true);
                    PathFaceRecord f = ms.GetFaceIndex(in cv);
                    if (!f.IsValid() || !MapVillageData.IsLandGroup(f.FaceGroupIndex)) { _skipWater++; continue; }
                    float z; Vec3 n;
                    ms.GetTerrainHeightAndNormal(new Vec2(st.X, st.Y), out z, out n);
                    if (float.IsNaN(z) || float.IsInfinity(z)) { _skipBad++; continue; }
                    MatrixFrame fr = Frame(st, z, n);
                    Decal d = Decal.CreateDecal(null);                       // wzor: NavalMobilePartyVisual (piana na mapie)
                    if ((object)d == null || !d.IsValid) { _skipBad++; continue; }
                    d.SetMaterial(_mat);                                     // material PRZED dodaniem do sceny
                    if (_decals.Count == 0)
                    {
                        Material got = d.GetMaterial();                      // raz: decal ma NASZ material (nie zastepczy)
                        if ((object)got == null || !got.IsValid || (got.Pointer != _mat.Pointer && !string.Equals(got.Name, _mat.Name, StringComparison.OrdinalIgnoreCase)))
                        { _noMat++; _built = true; Log.Info(MapSideRoads.P + "decal nie przyjal materialu '" + _matName + "' - nic nie stawiam."); return; }
                    }
                    if (look.HasArg) d.SetVectorArgument(look.A0, look.A1, look.A2, look.A3);
                    else if (_matName.StartsWith("decal_", StringComparison.Ordinal) && _matName.Contains("_path_"))
                    {
                        float a0, a1, a2, a3;
                        MapRoadsProbe.AtlasArgument(_matName, out a0, out a1, out a2, out a3);   // atlas sciezek jak w scenie ROT (tylko material zapasowy)
                        d.SetVectorArgument(a0, a1, a2, a3);
                    }
                    d.SetFactor1(look.Factor);
                    scene.AddDecalInstance(d, DecalSet, true);
                    _decals.Add(d);                                          // od razu w liscie: wyjatek ponizej nie zostawi decala bez zdjecia
                    d.Frame = fr;
                    float a = st.Alpha * look.Alpha;
                    if (a < 0.999f) d.SetAlpha(a);
                    if (hidden) d.SetIsVisible(false);
                    made++;
                }
                catch (Exception ex)
                {
                    _stCreate++;
                    if (_stCreate <= 3 || _stCreate % 200 == 0) Log.Error("MapSideRoadsView.Build stempel " + (_next - 1) + " (potkniecie " + _stCreate + ")", ex);
                }
            }
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            _buildTicks += dt;
            _buildTickCount++;
            if (dt > _maxTickTicks) _maxTickTicks = dt;
            if (_next >= res.Stamps.Count)
            {
                _built = true;
                Log.Info(MapSideRoads.P + "postawione decale " + _decals.Count + " z " + res.Stamps.Count + " stempli (drog " + res.Roads.Count + "; pominiete nad woda / brodem / mostem "
                         + _skipWater + ", zle " + _skipBad + ", potkniecia " + _stCreate + ") w " + Ms(_buildTicks) + " ms ("
                         + (_decals.Count > 0 ? (_buildTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency / _decals.Count).ToString("0.0", Inv) : "-") + " us na decal), tickow "
                         + _buildTickCount + ", najdluzszy tick " + Ms(_maxTickTicks) + " ms" + (now ? " (bieg zdjec - bez budzetu)" : "") + "; " + _matName + _matNote + ", " + look.Text() + ".");
            }
        }

        /// <summary>Ramka jak CalculateTrackFrame gry (i proba drog): os u = normalna terenu, f = kierunek drogi, s = f x u; skala = pol wymiaru pudla
        /// (s w poprzek, f wzdluz, u w gore - na stoku pudlo musi objac teren pod calym stemplem).</summary>
        private static MatrixFrame Frame(SideRoadPlan.Stamp st, float z, Vec3 n)
        {
            Vec3 u = n;
            if (!(u.z >= 0.2f)) u = new Vec3(0f, 0f, 1f);
            u.Normalize();
            Vec3 f = new Vec3((float)Math.Cos(st.Yaw), (float)Math.Sin(st.Yaw), 0f);
            Vec3 s = Vec3.CrossProduct(f, u);
            s.Normalize();
            f = Vec3.CrossProduct(u, s);
            f.Normalize();
            float h = Math.Max(st.Across, st.Along) + 0.6f;
            MatrixFrame fr = MatrixFrame.Identity;
            fr.rotation.s = s * st.Across;
            fr.rotation.f = f * st.Along;
            fr.rotation.u = u * h;
            fr.origin = new Vec3(st.X, st.Y, z);
            return fr;
        }

        // ---------- widocznosc: razem z wioskami (wysokosc kamery), zmiana TYLKO przy przejsciu progu ----------
        private void UpdateVisibility(MapScreen screen)
        {
            if (screen == null || screen.MapCameraView == null || screen.MapCameraView.Camera == null) return;
            float camZ = screen.MapCameraView.Camera.Position.z;
            float hideZ = Settings.Current.MapVillagesHideAboveCameraHeight;
            if (hideZ <= 0f) hideZ = 160f;
            hideZ = Math.Min(hideZ, _job.Look.HideZ);
            bool want = _visible ? camZ <= hideZ : camZ <= hideZ - 10f;   // histereza 10 jedn. - bez migania na progu
            if (_visKnown && want == _visible) return;
            if (!_visKnown && want && _visible) { _visKnown = true; return; }   // decale stawiane widoczne - pierwszy odczyt bez przelaczania
            int toggled = 0;
            foreach (var d in _decals)
            {
                try { d.SetIsVisible(want); toggled++; }
                catch (Exception ex) { _stVis++; if (_stVis <= 3) Log.Error("MapSideRoadsView.Visibility", ex); }
            }
            if (_visKnown) _visChanges++;
            _visKnown = true;
            _visible = want;
        }

        /// <summary>f19: klucz MapSideRoadsAllVillages inny niz przy starcie planu - decale zdjete (jesli sa) i plan od nowa, raz na zmiane (porownanie z kluczem
        /// przy starcie planu, nie z plikiem-przelacznikiem autotestu - ten nadpisuje tylko regule planu). Wolane po skonczonym planie, PRZED stawianiem
        /// i sprawdzianem ukladu - takze po planie nieudanym (nowa regula moze sie udac; jeden plan naraz - nowy dopiero po skonczeniu biezacego).</summary>
        private bool KeyChanged()
        {
            var j = _job;
            if (j == null) return false;
            bool keyAll = MapSideRoads.KeyAll;
            if (keyAll == j.KeyAll) return false;
            int n0 = RemoveAll();
            _replans++;
            Log.Info(MapSideRoads.P + "regula 'kazda wioska' (klucz MapSideRoadsAllVillages) zmieniona na " + (keyAll ? "TAK" : "NIE") + " - zdjete decale " + n0
                     + (j.Res == null ? " (plan byl NIEUDANY)" : "") + ", plan od nowa.");
            SpeedClear("regula 'kazda wioska' zmieniona - plan od nowa");   // n20-drogi
            _job = null; _built = false; _next = 0; _visKnown = false; _waited = 0f; _failedPlan = false;
            ResetChunks();
            return true;
        }

        /// <summary>Uklad wiosek inny niz w planie (crc ksiegi) - decale zdjete, plan od nowa (raz na zmiane).</summary>
        private void CheckLayout()
        {
            var j = _job;
            if (j == null) return;
            if (!j.HasLayout) return;
            uint crc;
            if (!VillageBook.LayoutCrcNow(out crc) || crc == j.LayoutCrc) return;
            int n = RemoveAll();
            _replans++;
            Log.Info(MapSideRoads.P + "uklad wiosek zmieniony (crc " + j.LayoutCrc.ToString("X8", Inv) + " -> " + crc.ToString("X8", Inv) + ") - zdjete decale " + n + ", plan od nowa.");
            SpeedClear("uklad wiosek zmieniony - plan od nowa");   // n20-drogi
            _job = null; _built = false; _next = 0; _visKnown = false; _waited = 0f;
            ResetChunks();
        }

        /// <summary>n20-drogi: drogi boczne tego widoku zdjete z premii predkosci (MapRoads.SideClear zdejmuje tylko plan, ktory jest w premii).</summary>
        private void SpeedClear(string why)
        {
            var sj = _speedJob;
            _speedJob = null;
            if (sj != null && sj.Res != null) MapRoads.SideClear(sj.Res, why);
        }

        // ---------- zdejmowanie ----------
        private int RemoveAll()
        {
            int n = 0;
            Scene scene = MapScene;
            bool rend = false;
            try { rend = scene != null && scene.HasDecalRenderer(); } catch { }
            foreach (var d in _decals)
            {
                try
                {
                    d.SetIsVisible(false);
                    if (rend) scene.RemoveDecalInstance(d, DecalSet);
                    n++;
                }
                catch (Exception ex) { _stRemove++; if (_stRemove <= 3) Log.Error("MapSideRoadsView.Remove", ex); }
            }
            _decals.Clear();
            if (_active.Count > 0) n += RemoveCells(scene, rend);   // f22: komorki w scenie (tryb komorek)
            _next = 0; _built = false; _mode = -1;
            return n;
        }

        private int DropAll()
        {
            int n = _decals.Count;
            _decals.Clear();
            n += DropCells();   // f22
            _mode = -1;
            return n;
        }

        // ---------- pomiar ----------
        private string SummaryText()
        {
            var sb = new StringBuilder();
            var r = _job != null ? _job.Res : null;
            sb.Append("decali ").Append(_mode == 1 ? _liveDecals : _decals.Count).Append(_mode == 1 ? " w scenie (tryb komorek f22)" : "").Append(r != null ? " (drog " + r.Roads.Count + ", dlugosc " + r.Length.ToString("0", Inv) + " jedn.)" : "")
              .Append("; stawianie ").Append(Ms(_buildTicks)).Append(" ms w ").Append(_buildTickCount).Append(" tickach (najdluzszy ").Append(Ms(_maxTickTicks)).Append(" ms)")
              .Append("; widoczne ").Append(_visible ? "tak" : "nie").Append(", zmiany widocznosci ").Append(_visChanges)
              .Append("; plan od nowa (zmiana ukladu) ").Append(_replans)
              .Append("; czyszczenie pamieci GPU ").Append(_clearMem)
              .Append("; klatki z drogami widocznymi: ").Append(HistText(1)).Append("; schowanymi: ").Append(HistText(0));
            if (_mode == 1 || _grid != null) ChunkSummary(sb);   // f22: komorki i klatki wedlug liczby komorek w scenie
            sb.Append("; potkniecia: stawianie ").Append(_stCreate).Append(", widocznosc ").Append(_stVis).Append(", zdejmowanie ").Append(_stRemove)
              .Append(", tick ").Append(_stTick).Append(", bez materialu ").Append(_noMat).Append('.');
            return sb.ToString();
        }

        private string HistText(int k)
        {
            int[] h = _hist[k];
            long n = 0;
            for (int i = 0; i < Buckets; i++) n += h[i];
            if (n == 0) return "0";
            long acc = 0; int p50 = -1, p95 = -1; long over50 = 0;
            for (int i = 0; i < Buckets; i++)
            {
                acc += h[i];
                if (p50 < 0 && acc * 2 >= n) p50 = i;
                if (p95 < 0 && acc * 100 >= n * 95) p95 = i;
                if (i >= 50) over50 += h[i];
            }
            return n + " (srednio " + (_sumMs[k] / n).ToString("0.0", Inv) + " ms, mediana " + p50 + ", p95 " + p95 + ", ponad 50 ms " + over50 + ")";
        }

        private static string Ms(long ticks) { return (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", Inv); }
    }
}
