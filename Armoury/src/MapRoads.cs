using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// SW-DROGI (noc 17/18.10, Jeff 16.10 lista pkt 4 i 17.10: "a zajazdy, szybsze poruszanie po drogach?") - +15% PREDKOSCI NA DROGACH ROT.
    /// Drogi ROT to farba terenu (warstwa "road" w terrain.bin), nie encje - silnik ich nie podaje (mapa bez materialow fizycznych), wiec lamane
    /// policzyla sonda geografii (galaz w-toku/noc-geo 19e6f041: tools/geo/geo_drogi_rot.py + MapGeoProbe G3 w grze, STAN-PRAC 59), a mod czyta gotowy
    /// plik ModuleData\arm_map_roads_geo.tsv (generator tools/drogi/drogi_plik_modu.py; 311 lamanych, 10876 punktow, 10565 odcinkow, 10016 jedn.).
    ///
    /// PLIK -> SIATKA: raz na sesje kampanii (OnSessionLaunched, watek glowny) plik wczytany do siatki komorek 2 x 2 jedn. (CSR: poczatek komorki + lista
    /// odcinkow); odcinek wpisany do kazdej komorki, ktora siega jego kapsula (pol-szerokosc + najwiekszy margines 2 jedn.). Siatka niezmienna po budowie,
    /// publikowana jednym zapisem referencji - czytana bez blokad z watkow roboczych gry (predkosc partii liczy sie rownolegle, TWParallel w
    /// CampaignTickCacheDataStore). Pol-szerokosc drogi z farby: width / 2 lamanej (pas farby z wagi > 48), przycieta do 0.15..1.5 jedn.
    /// NA DRODZE = odleglosc do najblizszego odcinka <= pol-szerokosc + MapRoadMargin (domyslnie 0.4 jedn. - farba ma probke 0.39 jedn., szkielet
    /// Douglas-Peucker 0.2). Symulacja na trasach AI sondy (geo-trasy-punkty, 80 526 punktow): przy 0.4 na drodze 4.9% punktow tras (AI nie szuka drog).
    /// STRAZNIK MAPY: naglowek pliku ma rozmiar terrain.bin ROT-Map; inny rozmiar w grze = inna mapa = premia nieczynna z powodem w logu (bez pliku
    /// terrain.bin - bez straznika).
    ///
    /// PREMIA: MarchPace.SpeedPostfix (jedna latka na kazdy CalculateFinalSpeed, Priority.Last, najbardziej zewnetrzny poziom lancucha - SpeedDepth) po
    /// suficie kolumny wola SpeedRoad: partia ladowa na drodze dostaje +MapRoadSpeedPercent% WYNIKU jako wpis "Road" (Add / (1 + suma wspolczynnikow),
    /// jak karczmy) i sufit LimitMax (kolumna marszowa) w gore o ten sam procent - jak TownInns.SpeedBonus (STAN-PRAC 58: bez tego premia znikala pod
    /// sufitem). Wszystkie partie ladowe (gracz, lordowie, karawany, wozy wsi, bandy, patrole); partie na morzu i w przejsciu lad-morze bez zmian.
    /// Kara snu (NightRest) i premia karczmy (TownInns) biegna PO nas (procent od wyniku) - mnoza sie z premia drogi, sufit karczmy liczy sie od
    /// sufitu juz podniesionego o droge. Przy predkosci na podlodze gry (LimitMin) premia moze zginac - rzadki brzeg.
    ///
    /// PAMIEC PREDKOSCI GRY (sprawdzone w dekompilacji 1.4.8, MobileParty.CalculateSpeedForPartyUnified): gra przelicza predkosc koncowa tylko przy
    /// zmianie sciany siatki ruchu, nocy, wiatru albo bazy (IsLastSpeedCacheInvalid). Bez naszej latki premia liczylaby sie z punktu wejscia na sciane
    /// (sciany sa duze, droga ma ~0.5 jedn.) - na drodze wzdluz sciany zostawalby stary wynik. Dlatego: postfiks na MobileParty.IsLastSpeedCacheInvalid
    /// (DeclaredMethod na klasie deklarujacej) - gdy gra mowi "pamiec dobra", a stan drogi w biezacym punkcie rozni sie od stanu uzytego przy ostatnim
    /// liczeniu predkosci tej partii -> "pamiec niewazna" i gra liczy od nowa (raz na wejscie / zejscie z drogi).
    /// PAMIEC PARTII: tablica bez blokad (1 << 17 slotow long, indeks = Party.Index modulo): Party.Index (32 bity) | komorka siatki + 1 (24 bity) |
    /// stan (2 bity: 0 nieznany, 1 poza droga, 2 na drodze). Zapis w SpeedRoad (watek liczacy te partie), odczyt w postfiksie pamieci; jeden long =
    /// zapis atomowy na x64 (Volatile). Inna partia w tym samym slocie = stan nieznany = decyduje gra (jak bez latki). Ta sama komorka bez odcinkow =
    /// stan "poza droga" bez liczenia odleglosci (O(1)). Tablica czyszczona przy kazdym starcie sesji (indeksy partii sa z kampanii).
    ///
    /// POMIAR (tylko log): spis partio-godzin w dobach pomiaru (LogGate.Today - w grze co QuietLogEveryDays, w autotescie codziennie), audyt w takcie
    /// linii (do 8 partii AI na drodze + gracz: predkosc bez premii drogi i z premia z modelu wprost - bez zapisu do pamieci partii; premia rzeczywista,
    /// wpis "Road", sufit, pamiec gry), linia "Drogi (DR): dzien N". Bledy liczone (potkniecia), nigdy wylacznik calej funkcji. Nic nie idzie do zapisu gry.
    /// Klucze: MapRoadSpeedBonus (TAK), MapRoadSpeedPercent (15), MapRoadMargin (0.4). Wylaczony klucz = sciezka sprzed zmiany (SpeedRoad i postfiks
    /// pamieci wychodza od razu; plik nie jest wczytywany).
    /// </summary>
    internal static class MapRoads
    {
        internal const string FileName = "arm_map_roads_geo.tsv";
        private const string P = "Drogi (DR): ";
        private const float CellSize = 2f;
        internal const float MaxMargin = 2f;           // MCM i kod przycinaja MapRoadMargin do 0..2 (siatka budowana na ten zasieg)
        private const float HalfMin = 0.15f, HalfMax = 1.5f;
        private const int MaxPercent = 50;
        private const int SlotBits = 17;
        private const int SlotMask = (1 << SlotBits) - 1;
        private const int AuditMax = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject _txtRoad = new TextObject("{=!}Road");

        // ------------------------------------------------------------------ siatka (niezmienna po budowie)
        private sealed class Grid
        {
            public float X0, Y0, Inv;
            public int Nx, Ny;
            public int[] Start;       // Nx * Ny + 1
            public int[] Seg;         // odcinki komorek
            public float[] Ax, Ay, Dx, Dy, InvL2, Half;
            public int Lines, Points, Segments, CellsUsed, Entries, MaxPerCell;
            public double Length;
            public int Version;
            public string Md5 = "-";
            public long TerrainSize;

            internal int CellOf(float x, float y)
            {
                float fx = (x - X0) * Inv, fy = (y - Y0) * Inv;
                if (!(fx >= 0f) || !(fy >= 0f)) return -1;   // takze NaN
                int cx = (int)fx, cy = (int)fy;
                if (cx >= Nx || cy >= Ny) return -1;
                return cy * Nx + cx;
            }

            internal bool Empty(int c) { return c < 0 || Start[c] == Start[c + 1]; }

            /// <summary>Na drodze w komorce c: odleglosc punktu od ktoregos odcinka <= pol-szerokosc + margines.</summary>
            internal bool OnRoad(int c, float x, float y, float margin)
            {
                if (c < 0) return false;
                int k1 = Start[c + 1];
                for (int k = Start[c]; k < k1; k++)
                {
                    int s = Seg[k];
                    float px = x - Ax[s], py = y - Ay[s];
                    float t = (px * Dx[s] + py * Dy[s]) * InvL2[s];
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float ex = px - t * Dx[s], ey = py - t * Dy[s];
                    float r = Half[s] + margin;
                    if (ex * ex + ey * ey <= r * r) return true;
                }
                return false;
            }

            /// <summary>Odleglosc od krawedzi farby (odleglosc - pol-szerokosc) najblizszego odcinka komorki; float.MaxValue = brak odcinka w zasiegu.</summary>
            internal float EdgeDist(int c, float x, float y)
            {
                float best = float.MaxValue;
                if (c < 0) return best;
                int k1 = Start[c + 1];
                for (int k = Start[c]; k < k1; k++)
                {
                    int s = Seg[k];
                    float px = x - Ax[s], py = y - Ay[s];
                    float t = (px * Dx[s] + py * Dy[s]) * InvL2[s];
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float ex = px - t * Dx[s], ey = py - t * Dy[s];
                    float d = (float)Math.Sqrt(ex * ex + ey * ey) - Half[s];
                    if (d < best) best = d;
                }
                return best;
            }
        }

        private static volatile Grid _grid;
        private static string _note = "nie wczytany";
        private static bool _cacheHooked;
        private static string _hookNote = "nie wpinana";
        private static readonly long[] _slots = new long[1 << SlotBits];

        // audyt: [ThreadStatic] - tylko watek glowny audytu; watki robocze gry widza false
        [ThreadStatic] private static bool _suppress;   // liczenie "bez premii drogi"
        [ThreadStatic] private static bool _noRecord;   // liczenie bez zapisu stanu do pamieci partii

        // liczniki z watkow roboczych (przyblizone - bez blokad, jak HorseState): przeliczenia wymuszone, wywolania postfiksu pamieci, probka czasu
        private static long _flips, _cacheCalls, _cacheTicks, _cacheSampled, _speedCalls, _speedOn;
        private static int _parStumbles, _parStumblesAll;
        private static int _stumbles, _stumblesAll;
        private static Exception _firstPar;

        // spis partio-godzin (watek glowny): 0 rody, 1 karawany, 2 wozy wsi, 3 bandy, 4 inne, 5 gracz
        private static readonly int[] _hN = new int[6], _hOn = new int[6];
        private static int _hours;
        private static long _censusTicks;
        private static readonly Random _rng = new Random(1718);
        private static readonly List<MobileParty> _cand = new List<MobileParty>();
        private static AccessTools.FieldRef<MobileParty, float> _lastSpeedRef;
        private static bool _lastSpeedTried;

        internal static bool Ready { get { return _grid != null; } }

        private static float MarginOf(Settings s)
        {
            float m = s.MapRoadMargin;
            return m < 0f ? 0f : m > MaxMargin ? MaxMargin : m;
        }

        private static float PctOf(Settings s)
        {
            int p = s.MapRoadSpeedPercent;
            return (p < 0 ? 0 : p > MaxPercent ? MaxPercent : p) / 100f;
        }

        private static void ParStumble(Exception e)
        {
            Interlocked.Increment(ref _parStumbles);
            Interlocked.Increment(ref _parStumblesAll);
            if (_firstPar == null) _firstPar = e;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_stumblesAll <= 3) Log.Error("MapRoads." + where, e);
        }

        // ------------------------------------------------------------------ sciezka predkosci (watki robocze gry)
        /// <summary>MarchPace.SpeedPostfix (po suficie kolumny, ten sam poziom lancucha): partia ladowa na drodze +x% wyniku jako "Road" i sufit LimitMax
        /// w gore o ten sam procent (jak TownInns.SpeedBonus). Zapisuje stan drogi uzyty w tym liczeniu do pamieci partii (postfiks pamieci gry).</summary>
        internal static void SpeedRoad(MobileParty mp, ref ExplainedNumber r)
        {
            if (_suppress) return;
            var g = _grid;
            if (g == null) return;
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus) return;
            try
            {
                if (mp == null || !SpeedDepth.OutermostFinal) return;
                if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;   // morze i przejscie lad-morze - prawa morza
                var p = mp.Position;
                float x = p.X, y = p.Y;
                int c = g.CellOf(x, y);
                bool on = !g.Empty(c) && g.OnRoad(c, x, y, MarginOf(s));
                if (!_noRecord && !TerrainEase.ProbeActive) Record(mp, c, on);
                _speedCalls++;
                if (!on) return;
                _speedOn++;
                float pct = PctOf(s);
                if (pct <= 0f) return;
                float res = r.ResultNumber;
                if (res <= 0f) return;
                float f = 1f + r.SumOfFactors;
                float add = res * pct;
                r.Add(f > 0.01f ? add / f : add, _txtRoad);
                float lim = r.LimitMaxValue;
                if (lim > 0f && lim < float.MaxValue * 0.5f) r.LimitMax(lim * (1f + pct) + 0.001f);   // zapas 0.001 - bez linii "Maximum -0.00"
            }
            catch (Exception e) { ParStumble(e); }
        }

        private static void Record(MobileParty mp, int cell, bool on)
        {
            int idx = mp.Party.Index;
            long v = ((long)idx << 32) | ((long)((cell + 1) & 0xFFFFFF) << 2) | (on ? 2L : 1L);
            Volatile.Write(ref _slots[idx & SlotMask], v);
        }

        /// <summary>Postfiks MobileParty.IsLastSpeedCacheInvalid (prywatna, DeclaredMethod): gra uznala pamiec predkosci za dobra - sprawdzamy, czy stan
        /// drogi w biezacym punkcie jest ten sam co przy ostatnim liczeniu; inny = pamiec niewazna (gra liczy predkosc od nowa, SpeedRoad zapisze nowy stan).</summary>
        public static void CachePostfix(MobileParty __instance, ref bool __result)
        {
            if (__result) return;
            var g = _grid;
            if (g == null) return;
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus) return;
            long t0 = (++_cacheCalls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                var mp = __instance;
                if (mp == null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;
                int idx = mp.Party.Index;
                long v = Volatile.Read(ref _slots[idx & SlotMask]);
                if ((int)(v >> 32) != idx) return;          // inna partia w slocie albo jeszcze nie liczona - decyduje gra
                int st = (int)(v & 3);
                if (st == 0) return;
                var p = mp.Position;
                float x = p.X, y = p.Y;
                int c = g.CellOf(x, y);
                int prevCell = (int)((v >> 2) & 0xFFFFFF) - 1;
                bool on;
                if (c == prevCell && g.Empty(c)) on = false;   // ta sama komorka bez drog - O(1)
                else on = !g.Empty(c) && g.OnRoad(c, x, y, MarginOf(s));
                if (on != (st == 2)) { __result = true; _flips++; }
            }
            catch (Exception e) { ParStumble(e); }
            finally { if (t0 != 0) { _cacheTicks += Stopwatch.GetTimestamp() - t0; _cacheSampled++; } }
        }

        // ------------------------------------------------------------------ latka (menu glowne, SubModuleMain - po MarchPace.ApplyAll)
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MapRoadSpeedBonus)
                {
                    _hookNote = "NIE wpieta (MapRoadSpeedBonus wylaczony przy starcie gry)";
                    Log.Info(P + "premia drog wylaczona przy starcie gry (MapRoadSpeedBonus) - latka pamieci predkosci MobileParty.IsLastSpeedCacheInvalid niewpieta.");
                    return;
                }
                var m = AccessTools.DeclaredMethod(typeof(MobileParty), "IsLastSpeedCacheInvalid");
                if (m == null)
                {
                    _hookNote = "NIE wpieta (brak metody MobileParty.IsLastSpeedCacheInvalid)";
                    Log.Info(P + "BRAK metody MobileParty.IsLastSpeedCacheInvalid - premia drog liczy sie tylko przy zmianie sciany siatki (pamiec predkosci gry).");
                    return;
                }
                var post = new HarmonyMethod(typeof(MapRoads).GetMethod("CachePostfix", BindingFlags.Public | BindingFlags.Static));
                h.Patch(m, postfix: post);
                var pi = Harmony.GetPatchInfo(m);
                bool ok = false;
                if (pi != null) foreach (var x in pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(MapRoads)) { ok = true; break; }
                _cacheHooked = ok;
                _hookNote = ok ? "wpieta" : "NIE wpieta (brak postfiksu po Patch)";
                Log.Info(P + "latka pamieci predkosci MobileParty.IsLastSpeedCacheInvalid " + (ok ? "wpieta" : "NIE wpieta (brak postfiksu po Patch)")
                         + " (premia drogi przeliczana przy wejsciu na droge i zejsciu z niej, nie tylko przy zmianie sciany siatki).");
            }
            catch (Exception e) { _hookNote = "NIE wpieta (" + e.GetType().Name + ")"; Log.Error("MapRoads.ApplyAll", e); }
        }

        // ------------------------------------------------------------------ plik i siatka (watek glowny)
        internal static void Reset()
        {
            _grid = null;
            _note = "nie wczytany";
            Array.Clear(_slots, 0, _slots.Length);
            ZeroDay();
            _parStumbles = _parStumblesAll = 0; _firstPar = null;
            _stumbles = _stumblesAll = 0;
        }

        private static void ZeroDay()
        {
            for (int i = 0; i < _hN.Length; i++) { _hN[i] = 0; _hOn[i] = 0; }
            _hours = 0; _censusTicks = 0;
            _flips = 0; _cacheCalls = 0; _cacheTicks = 0; _cacheSampled = 0; _speedCalls = 0; _speedOn = 0;
            _parStumbles = 0; _stumbles = 0;
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched, po McmSettings.Apply): pamiec partii od zera, plik wczytany do siatki (przy wlaczonym kluczu).</summary>
        internal static void SessionStart()
        {
            Reset();
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus)
            {
                Log.Info(P + "start sesji - premia drog WYLACZONA (MapRoadSpeedBonus) - plik " + FileName + " niewczytany, predkosc bez zmian.");
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            Load();
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            var g = _grid;
            Log.Info(P + "start sesji - klucz MapRoadSpeedBonus tak, premia " + s.MapRoadSpeedPercent.ToString(Inv) + "% (przyciete " + (PctOf(s) * 100f).ToString("0", Inv)
                     + "%), margines " + MarginOf(s).ToString("0.00", Inv) + " jedn. | " + _note
                     + (g != null ? " | siatka komorek " + CellSize.ToString("0", Inv) + " jedn.: " + g.Nx + " x " + g.Ny + ", z drogami " + g.CellsUsed + ", wpisow " + g.Entries
                                    + ", maks. odcinkow na komorke " + g.MaxPerCell : "")
                     + " | latka pamieci predkosci " + _hookNote + " | wczytanie " + ms.ToString("0.0", Inv) + " ms"
                     + " | premia " + (g != null && _cacheHooked ? "CZYNNA" : g != null ? "CZYNNA (bez latki pamieci - tylko przy zmianie sciany siatki)" : "NIECZYNNA") + ".");
        }

        private static void Load()
        {
            try
            {
                string path = MapVillageData.DataPathFor(FileName);
                if (path == null || !File.Exists(path)) { _note = "BRAK pliku ModuleData\\" + FileName + " - premia drog nieczynna"; return; }
                var xs = new List<float>(11000);
                var ys = new List<float>(11000);
                var ws = new List<float>(11000);
                var ids = new List<int>(11000);
                int bad = 0, version = 0;
                long tsize = 0;
                string md5 = "-";
                var cols = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string raw in File.ReadAllText(path, Encoding.UTF8).Replace("\r", "").Split('\n'))
                {
                    if (raw.Length == 0) continue;
                    if (raw[0] == '#')
                    {
                        string h = raw.TrimStart('#').Trim();
                        if (h.StartsWith("version:", StringComparison.Ordinal)) int.TryParse(h.Substring(8).Trim(), NumberStyles.Integer, Inv, out version);
                        else if (h.StartsWith("terrain_size:", StringComparison.Ordinal)) long.TryParse(h.Substring(13).Trim(), NumberStyles.Integer, Inv, out tsize);
                        else if (h.StartsWith("terrain_md5:", StringComparison.Ordinal)) md5 = h.Substring(12).Trim();
                        else if (h.StartsWith("road\t", StringComparison.Ordinal))
                        {
                            string[] hc = h.Split('\t');
                            for (int i = 0; i < hc.Length; i++) cols[hc[i].Trim()] = i;
                        }
                        continue;
                    }
                    try
                    {
                        string[] c = raw.Split('\t');
                        int id = int.Parse(c[Col(cols, "road", 0)], Inv);
                        float x = float.Parse(c[Col(cols, "x", 2)], Inv), y = float.Parse(c[Col(cols, "y", 3)], Inv);
                        float w = float.Parse(c[Col(cols, "width", 4)], Inv);
                        if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) { bad++; continue; }
                        ids.Add(id); xs.Add(x); ys.Add(y); ws.Add(float.IsNaN(w) ? 0f : w);
                    }
                    catch { bad++; }
                }
                if (version < 1) { _note = "plik " + FileName + " bez wersji w naglowku - premia drog nieczynna"; return; }
                // straznik mapy: rozmiar terrain.bin ROT-Map z naglowka
                string tnote;
                string tb = TerrainPath();
                if (tsize > 0 && tb != null && File.Exists(tb))
                {
                    long now = new FileInfo(tb).Length;
                    if (now != tsize)
                    {
                        _note = "plik " + FileName + " v" + version + " z INNEJ MAPY: terrain.bin ROT-Map ma " + now + " B, dane z " + tsize + " B (terrain_md5 " + md5
                                + ") - premia drog nieczynna (dane do odswiezenia: tools/geo/geo_drogi_rot.py + tools/drogi/drogi_plik_modu.py)";
                        return;
                    }
                    tnote = "terrain.bin ROT-Map zgodny (" + now + " B)";
                }
                else tnote = tsize > 0 ? "terrain.bin ROT-Map nie znaleziony - bez straznika mapy" : "naglowek bez terrain_size - bez straznika mapy";
                var g = Build(ids, xs, ys, ws);
                g.Version = version; g.Md5 = md5; g.TerrainSize = tsize;
                _grid = g;
                _note = "plik " + FileName + " v" + version + ": lamanych " + g.Lines + ", punktow " + g.Points + ", odcinkow " + g.Segments + ", dlugosc "
                        + g.Length.ToString("0.0", Inv) + " jedn., zle wiersze " + bad + "; " + tnote;
            }
            catch (Exception e) { _grid = null; _note = "blad wczytania " + FileName + " (" + e.GetType().Name + ") - premia drog nieczynna"; Stumble("Load", e); }
        }

        private static int Col(Dictionary<string, int> cols, string name, int def)
        {
            int i;
            return cols.TryGetValue(name, out i) ? i : def;
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

        private static Grid Build(List<int> ids, List<float> xs, List<float> ys, List<float> ws)
        {
            var g = new Grid();
            int n = xs.Count;
            g.Points = n;
            // odcinki: kolejne punkty tej samej lamanej (plik posortowany po road, seq)
            var sa = new List<int>(n);
            int lines = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == 0 || ids[i] != ids[i - 1]) { lines++; continue; }
                sa.Add(i);
            }
            int m = sa.Count;
            g.Lines = lines; g.Segments = m;
            g.Ax = new float[m]; g.Ay = new float[m]; g.Dx = new float[m]; g.Dy = new float[m]; g.InvL2 = new float[m]; g.Half = new float[m];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            double len = 0;
            for (int k = 0; k < m; k++)
            {
                int i = sa[k];
                float ax = xs[i - 1], ay = ys[i - 1], dx = xs[i] - ax, dy = ys[i] - ay;
                float l2 = dx * dx + dy * dy;
                float half = ws[i] * 0.5f;
                if (half < HalfMin) half = HalfMin; else if (half > HalfMax) half = HalfMax;
                g.Ax[k] = ax; g.Ay[k] = ay; g.Dx[k] = dx; g.Dy[k] = dy; g.InvL2[k] = l2 > 1e-8f ? 1f / l2 : 0f; g.Half[k] = half;
                len += Math.Sqrt(l2);
                minX = Math.Min(minX, Math.Min(ax, ax + dx)); maxX = Math.Max(maxX, Math.Max(ax, ax + dx));
                minY = Math.Min(minY, Math.Min(ay, ay + dy)); maxY = Math.Max(maxY, Math.Max(ay, ay + dy));
            }
            g.Length = len;
            float pad = HalfMax + MaxMargin + CellSize;
            if (m == 0) { minX = minY = 0f; maxX = maxY = 1f; }
            g.X0 = minX - pad; g.Y0 = minY - pad; g.Inv = 1f / CellSize;
            g.Nx = Math.Max(1, (int)Math.Ceiling((maxX + pad - g.X0) / CellSize));
            g.Ny = Math.Max(1, (int)Math.Ceiling((maxY + pad - g.Y0) / CellSize));
            int cells = g.Nx * g.Ny;
            var count = new int[cells + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = pass == 1 ? new int[cells] : null;
                if (pass == 1)
                {
                    g.Start = new int[cells + 1];
                    for (int c = 0; c < cells; c++) g.Start[c + 1] = g.Start[c] + count[c];
                    g.Seg = new int[g.Start[cells]];
                }
                for (int k = 0; k < m; k++)
                {
                    float reach = g.Half[k] + MaxMargin;
                    float x0 = Math.Min(g.Ax[k], g.Ax[k] + g.Dx[k]) - reach, x1 = Math.Max(g.Ax[k], g.Ax[k] + g.Dx[k]) + reach;
                    float y0 = Math.Min(g.Ay[k], g.Ay[k] + g.Dy[k]) - reach, y1 = Math.Max(g.Ay[k], g.Ay[k] + g.Dy[k]) + reach;
                    int cx0 = Math.Max(0, (int)((x0 - g.X0) * g.Inv)), cx1 = Math.Min(g.Nx - 1, (int)((x1 - g.X0) * g.Inv));
                    int cy0 = Math.Max(0, (int)((y0 - g.Y0) * g.Inv)), cy1 = Math.Min(g.Ny - 1, (int)((y1 - g.Y0) * g.Inv));
                    for (int cy = cy0; cy <= cy1; cy++)
                        for (int cx = cx0; cx <= cx1; cx++)
                        {
                            int c = cy * g.Nx + cx;
                            if (pass == 0) count[c]++;
                            else g.Seg[g.Start[c] + fill[c]++] = k;
                        }
                }
            }
            int used = 0, maxc = 0;
            for (int c = 0; c < cells; c++) { int q = count[c]; if (q > 0) used++; if (q > maxc) maxc = q; }
            g.CellsUsed = used; g.Entries = g.Seg.Length; g.MaxPerCell = maxc;
            return g;
        }

        // ------------------------------------------------------------------ pomiar (watek glowny)
        /// <summary>Spis godzinny w dobach pomiaru (LogGate.Today): partie ladowe w ruchu (bez doczepionych do armii) - ile z nich na drodze, wedlug rodzaju.</summary>
        internal static void Hourly()
        {
            var g = _grid;
            var s = Settings.Current;
            if (g == null || s == null || !s.MapRoadSpeedBonus || Campaign.Current == null || !LogGate.Today) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                float margin = MarginOf(s);
                var all = MobileParty.All;
                var main = MobileParty.MainParty;
                for (int i = 0; i < all.Count; i++)
                {
                    var mp = all[i];
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                        int k = mp == main ? 5 : mp.IsLordParty ? 0 : mp.IsCaravan ? 1 : mp.IsVillager ? 2 : mp.IsBandit ? 3 : 4;
                        var p = mp.Position;
                        int c = g.CellOf(p.X, p.Y);
                        bool on = !g.Empty(c) && g.OnRoad(c, p.X, p.Y, margin);
                        _hN[k]++;
                        if (on) _hOn[k]++;
                    }
                    catch (Exception e) { Stumble("Hourly", e); }
                }
                _hours++;
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { _censusTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static string Pct(int a, int b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) : "-"; }

        /// <summary>Takt doby (po TerrainEase.Stage7Daily): w dobie linii (LogGate.Lines) audyt i linia "Drogi (DR): dzien N" za dobe spisu; potem zerowanie.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                if (s.MapRoadSpeedBonus && _grid == null && _note == "nie wczytany") Load();   // klucz wlaczony w MCM w trakcie sesji - plik wczytany tu (watek glowny)
                if (!s.MapRoadSpeedBonus) { ZeroDay(); return; }
                if (!LogGate.Lines) return;   // ciche logi: spis zbiera sie do doby linii
                Line(s, (int)CampaignTime.Now.ToDays - 1);
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay();
        }

        private static void Line(Settings s, int day)
        {
            var g = _grid;
            var ci = Inv;
            var sb = new StringBuilder(900);
            float pct = PctOf(s);
            sb.Append(P).Append("dzien ").Append(day)
              .Append(" | klucz MapRoadSpeedBonus tak, premia ").Append((pct * 100f).ToString("0", ci)).Append("%, margines ").Append(MarginOf(s).ToString("0.00", ci))
              .Append(" | plik ").Append(g != null ? "v" + g.Version + ": lamanych " + g.Lines + ", punktow " + g.Points + ", odcinkow " + g.Segments : "NIECZYNNY (" + _note + ")");
            int tot = 0, totOn = 0;
            for (int k = 0; k < 5; k++) { tot += _hN[k]; totOn += _hOn[k]; }
            sb.Append(" | partio-godziny w ruchu na ladzie (AI, godzin spisu ").Append(_hours).Append("): ").Append(tot).Append(", na drodze ").Append(totOn)
              .Append(" (").Append(Pct(totOn, tot)).Append("%)")
              .Append(" | rody ").Append(_hOn[0]).Append('/').Append(_hN[0]).Append(" (").Append(Pct(_hOn[0], _hN[0])).Append("%)")
              .Append(", karawany ").Append(_hOn[1]).Append('/').Append(_hN[1]).Append(" (").Append(Pct(_hOn[1], _hN[1])).Append("%)")
              .Append(", wozy wsi ").Append(_hOn[2]).Append('/').Append(_hN[2]).Append(" (").Append(Pct(_hOn[2], _hN[2])).Append("%)")
              .Append(", bandy ").Append(_hOn[3]).Append('/').Append(_hN[3]).Append(" (").Append(Pct(_hOn[3], _hN[3])).Append("%)")
              .Append(", inne ").Append(_hOn[4]).Append('/').Append(_hN[4]).Append(" (").Append(Pct(_hOn[4], _hN[4])).Append("%)")
              .Append(" | gracz godzin w ruchu ").Append(_hN[5]).Append(", na drodze ").Append(_hOn[5]);
            long sc = _speedCalls, so = _speedOn;
            sb.Append(" | liczenia predkosci (ok.) ").Append(sc).Append(", z premia drogi ").Append(so).Append(" (").Append(sc > 0 ? (100.0 * so / sc).ToString("0.0", ci) : "-").Append("%)")
              .Append(", przeliczenia wymuszone zmiana drogi (ok.) ").Append(_flips).Append(", latka pamieci ").Append(_cacheHooked ? "wpieta" : "NIE");
            Audit(s, g, sb, pct);
            double censusMs = _censusTicks * 1000.0 / Stopwatch.Frequency;
            double cacheUs = _cacheSampled > 0 ? _cacheTicks * 1e6 / Stopwatch.Frequency / _cacheSampled : 0;
            sb.Append(" | koszt: spis ").Append(censusMs.ToString("0.000", ci)).Append(" ms/dobe, postfiks pamieci ").Append(cacheUs.ToString("0.000", ci))
              .Append(" us/wywolanie (probka ").Append(_cacheSampled).Append(", wywolan ok. ").Append(_cacheCalls).Append(')')
              .Append(" | potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append("), w predkosci dzis ").Append(_parStumbles)
              .Append(" (od wczytania ").Append(_parStumblesAll).Append(")")
              .Append(_firstPar != null ? " pierwsze: " + _firstPar.GetType().Name + " " + (_firstPar.Message ?? "") : "").Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Audyt w takcie linii: do 8 losowych partii AI w ruchu na drodze + gracz na ladzie - model predkosci wprost (bez zapisu do pamieci partii):
        /// bez premii drogi (r0) i z premia (r1, z opisami): premia rzeczywista r1/r0 - 1, wpis "Road", wynik na suficie LimitMax, premia zjedzona przez sufit
        /// (r1 < r0 x (1 + premia) x 0.995), pamiec gry (_lastCalculatedSpeed) zgodna z r1 +-2%.</summary>
        private static void Audit(Settings s, Grid g, StringBuilder sb, float pct)
        {
            var ci = Inv;
            if (g == null || Campaign.Current == null) { sb.Append(" | audyt: brak siatki | gracz: -"); return; }
            var model = Campaign.Current.Models.PartySpeedCalculatingModel;
            if (model == null) { sb.Append(" | audyt: brak modelu predkosci | gracz: -"); return; }
            if (!_lastSpeedTried)
            {
                _lastSpeedTried = true;
                try { _lastSpeedRef = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); } catch { _lastSpeedRef = null; }
            }
            float margin = MarginOf(s);
            _cand.Clear();
            var all = MobileParty.All;
            var main = MobileParty.MainParty;
            for (int i = 0; i < all.Count; i++)
            {
                var mp = all[i];
                try
                {
                    if (mp == null || mp == main || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                    var p = mp.Position;
                    int c = g.CellOf(p.X, p.Y);
                    if (!g.Empty(c) && g.OnRoad(c, p.X, p.Y, margin)) _cand.Add(mp);
                }
                catch (Exception e) { Stumble("Audit.Scan", e); }
            }
            int n = 0, noLine = 0, eaten = 0, atCap = 0, cacheOk = 0, cacheN = 0;
            double sum = 0, mn = double.MaxValue, mx = double.MinValue;
            int k = Math.Min(AuditMax, _cand.Count);
            for (int i = 0; i < k; i++)
            {
                int j = i + _rng.Next(_cand.Count - i);
                var tmp = _cand[i]; _cand[i] = _cand[j]; _cand[j] = tmp;
                float r0, r1, lim, cached;
                bool line;
                if (!Probe(model, _cand[i], out r0, out r1, out lim, out line, out cached)) continue;
                n++;
                double b = r0 > 0.0001f ? r1 / r0 - 1.0 : 0.0;
                sum += b; if (b < mn) mn = b; if (b > mx) mx = b;
                if (!line) noLine++;
                if (r1 < r0 * (1f + pct) * 0.995f) eaten++;
                if (lim < float.MaxValue * 0.5f && r1 >= lim - 0.0015f) atCap++;
                if (cached > 0f) { cacheN++; if (Math.Abs(cached - r1) <= 0.02f * r1) cacheOk++; }
            }
            int candN = _cand.Count;
            _cand.Clear();
            sb.Append(" | audyt AI na drodze: kandydatow ").Append(candN).Append("; probek ").Append(n)
              .Append(", premia rzeczywista sr. ").Append(n > 0 ? (100.0 * sum / n).ToString("0.00", ci) : "-").Append("% (min ").Append(n > 0 ? (100.0 * mn).ToString("0.00", ci) : "-")
              .Append(", maks ").Append(n > 0 ? (100.0 * mx).ToString("0.00", ci) : "-").Append(')')
              .Append(", bez wpisu Road ").Append(noLine).Append(", zjedzona przez sufit ").Append(eaten).Append(", na suficie LimitMax ").Append(atCap)
              .Append(", pamiec gry zgodna ").Append(cacheOk).Append('/').Append(cacheN);
            // gracz (na ladzie): zawsze, z flaga drogi
            try
            {
                if (main != null && main.IsActive && !main.IsCurrentlyAtSea && !main.IsTransitionInProgress)
                {
                    var p = main.Position;
                    int c = g.CellOf(p.X, p.Y);
                    bool on = !g.Empty(c) && g.OnRoad(c, p.X, p.Y, margin);
                    float ed = g.EdgeDist(c, p.X, p.Y);
                    float r0, r1, lim, cached;
                    bool line;
                    if (Probe(model, main, out r0, out r1, out lim, out line, out cached))
                        sb.Append(" | gracz: na drodze ").Append(on ? "tak" : "nie").Append(" (od krawedzi farby ").Append(ed < 1e30f ? ed.ToString("0.00", ci) : ">2").Append(" jedn.)")
                          .Append(", bez premii ").Append(r0.ToString("0.000", ci)).Append(", z premia ").Append(r1.ToString("0.000", ci))
                          .Append(", LimitMax ").Append(lim < float.MaxValue * 0.5f ? lim.ToString("0.000", ci) : "brak")
                          .Append(", wpis Road ").Append(line ? "tak" : "nie").Append(", pamiec gry ").Append(cached.ToString("0.000", ci));
                    else sb.Append(" | gracz: audyt nieudany");
                }
                else sb.Append(" | gracz: na morzu / w przejsciu / nieczynny");
            }
            catch (Exception e) { Stumble("Audit.Player", e); sb.Append(" | gracz: blad audytu"); }
        }

        private static bool Probe(PartySpeedModel model, MobileParty mp, out float r0, out float r1, out float lim, out bool line, out float cached)
        {
            r0 = r1 = lim = cached = 0f; line = false;
            try
            {
                _noRecord = true;
                _suppress = true;
                try { r0 = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, false)).ResultNumber; }
                finally { _suppress = false; }
                var en = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, true));
                r1 = en.ResultNumber;
                lim = en.LimitMaxValue;
                string road = _txtRoad.ToString();
                foreach (var l in en.GetLines()) if (l.Item1 == road) { line = true; break; }
                if (_lastSpeedRef != null) cached = _lastSpeedRef(mp);
                return true;
            }
            catch (Exception e) { Stumble("Audit.Probe", e); return false; }
            finally { _noRecord = false; _suppress = false; }
        }
    }
}
