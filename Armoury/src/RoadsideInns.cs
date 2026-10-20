using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ZJ - ZAJAZDY PRZY SZLAKACH (sw-zajazdy, Jeff 16.10: "tawerny, do ktorych mozna wjechac na mapie, mozna spac, posluchac gossip"; "osobne budynki przy
    /// szlakach, ale nie za gesto, im mniej ludzi tym mniej zajazdow, zeby nie wygladalo jak armia zajazdow"; STAN-PRAC 52 pkt 1-2, 58, 59).
    ///  Z1 Miejsca: plik ModuleData\arm_roadside_inns.tsv (tools/zajazdy/zajazdy_gen.py z sondy geografii 16.10 i ukladu wiosek v5) - jedna regula dla swiata: punkty tras AI
    ///     miedzy twierdzami, odstep = 45 x sqrt(40000 / ludzie w promieniu 30) w granicach 30..110, bez zajazdu ponizej 12000 ludzi i za Murem; rozdroza najpierw.
    ///     Ten sam plik = te same zajazdy w kazdej kampanii (deterministycznie). Wczytanie RAZ na sesje (SessionStart / pierwsze uzycie): wiersz bez wsi gry - pominiety,
    ///     budynek blizej niz 2.2 jedn. od wioski na mapie z ukladu tej sesji (v5, v6 przysiolki) - pominiety. Nic z tego nie idzie do zapisu gry.
    ///  Z2 Wejscie: klik na budynek zajazdu (RoadsideInnsView) = jazda do miejsca postoju (MapState.ProcessTravel, jak klik w teren) i menu po dojechaniu; partia, ktora
    ///     stanie przy zajezdzie (u celu ruchu, do 1.6 jedn.), dostaje pytanie "Go in / Ride on" raz na postoj. Menu "arm_roadinn" (zwykle menu, nie oczekiwania) przez
    ///     GameMenu.ActivateGameMenu z ticku kampanii albo z okna pytania, tylko bez otwartego menu, poza osada, bitwa, morzem i niewola. Wyjscie - ExitToLast.
    ///  Z3 Nocleg: TownInns.Road.cs - ten sam rachunek co karczma miasta (szeregowi z sakiewki ludzi, towarzysze, gracz za siebie i konie bez jezdzca), tylko jakosci
    ///     zajazdu (1-3 z pliku, zwykle 1-2); pieniadze do kiesy wsi okregu, w ktorym stoi zajazd (podatek pana z czynnego modelu na licznik podatku wsi - regula
    ///     czesci wsi markietanow); sen w menu NightRest jak w obozie (namiot przy zajezdzie), premia po pobudce jak po karczmie (stan arm_inns, pole 16 = uid zajazdu).
    ///  Z4 Plotki: Rumours.cs (klucz InnGossip) - takze w karczmie miasta.
    ///  Z5 Linie "Zajazdy (ZJ):" - start sesji (plik, liczby wedlug ludzi, najblizszy sasiad), kazde wejscie i nocleg, doba; tools/sprawdz_logi.py --grupa nzajazdy.
    /// Wyjatek w naszym kodzie - licznik potkniec (pierwszy do Log.Error), nic nie gasimy globalnie.
    /// </summary>
    internal static class RoadsideInns
    {
        private const string P = "Zajazdy (ZJ): ";
        internal const string FileName = "arm_roadside_inns.tsv";
        // f10-uklad: zajazdy ukladu v7 (z dala od wiosek v7) w FileName; dawny plik (sw-zajazdy, z ukladu v5/v6) - przy ukladzie wiosek v6 i starszym (klucz VillageLayoutV7 NIE),
        // zeby zajazdy z ukladu v7 nie stanely w wioskach v6. Brak pliku _v6 - FileName jak dotad
        internal const string FileNameV6 = "arm_roadside_inns_v6.tsv";
        private static string _fileUsed = FileName;

        /// <summary>f10-uklad: plik zajazdow tej sesji wedlug ukladu wiosek (v7 - FileName; v6 i starszy - FileNameV6, gdy jest w module).</summary>
        private static string UsedFile()
        {
            try
            {
                if (MapVillageData.LayoutVersion >= 7) return FileName;
                string p = MapVillageData.DataPathFor(FileNameV6);
                return p != null && System.IO.File.Exists(p) ? FileNameV6 : FileName;
            }
            catch { return FileName; }
        }
        internal const string Menu = "arm_roadinn";
        internal const float EnterR = 1.6f;          // partia najwyzej tyle od miejsca postoju = przy zajezdzie
        private const float LeaveR = 3.5f;           // pytanie wraca dopiero po odjechaniu dalej niz tyle
        private const float ArriveEps = 0.25f;       // partia u celu ruchu (stanela)
        private const float NearMapVillage = 2.2f;   // budynek zajazdu blizej wioski na mapie - pominiety
        private const float FarVillage = 12.26f;     // f10-uklad (Jeff 18.10 "zajazdy nie sa obok wiosek"): generator stawia budynek >= 12.26 jedn. (odleglosc docelowa ukladu v7 = 2 x v6;
                                                     // recenzja: zajazd nie blizej wioski niz dwie wioski od siebie) od wioski i wsi gry - linia startu liczy, ile blizej
        private const float Cell = 25f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.RoadsideInns; } }

        /// <summary>Jeden zajazd (wiersz pliku + wies okregu z gry).</summary>
        internal sealed class Inn
        {
            public int Id, People, Tiers = 1, Line;
            public string Uid = "", Name = "", VillageId = "", Kind = "", Culture = "";
            public float X, Y, Bx, By, FrontDeg, Spacing;
            public Settlement Village;
        }

        private static readonly List<Inn> _inns = new List<Inn>();
        private static readonly Dictionary<string, Inn> _byUid = new Dictionary<string, Inn>(StringComparer.Ordinal);
        private static readonly Dictionary<long, List<Inn>> _grid = new Dictionary<long, List<Inn>>();
        private static bool _loaded;
        private static string _loadText = "nie wczytano";
        private static int _rows, _bad, _noVillage, _nearVillage, _sceneBad;
        private static readonly int[] _tierN = new int[4];
        // stan wejscia (tylko pamiec)
        private static string _pendingUid;   // klik na zajazd - jazda, menu po dojechaniu
        private static string _askedUid;     // pytanie zadane przy tym zajezdzie (do odjechania dalej niz LeaveR)
        private static Inn _current;         // zajazd otwartego menu
        private static long _lastCheck;
        // liczniki doby i sesji
        private static int _dEnter, _dClick, _dAsk, _dAskYes, _dAskNo, _dClickSet, _dStumbles, _stumbles;
        private static long _dTickTicks; private static int _dTickN;
        private static bool _errLogged;

        internal static List<Inn> All { get { return _inns; } }
        internal static Inn Current { get { return _current; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("RoadsideInns." + where, e); }
        }

        /// <summary>Nowa kampania albo wczytanie (konstruktor ArmouryBehavior): plik od nowa przy pierwszym uzyciu, stan wejscia i liczniki od zera.</summary>
        internal static void Reset()
        {
            _inns.Clear(); _byUid.Clear(); _grid.Clear();
            _loaded = false; _loadText = "nie wczytano";
            _rows = _bad = _noVillage = _nearVillage = _sceneBad = 0;
            Array.Clear(_tierN, 0, _tierN.Length);
            _pendingUid = _askedUid = null; _current = null; _lastCheck = 0;
            _stumbles = 0; _errLogged = false;
            DayReset();
            RoadsideInnStay.Reset();   // m95-zajazd: stan pobytu (ukrycie partii) od zera
            InnViewStats.DayZero();   // recenzja: liczniki doby widoku poprzedniej kampanii nie wchodza do pierwszej linii doby nowej (nowa gra / wczytanie bez konca doby)
        }

        private static void DayReset()
        {
            _dEnter = _dClick = _dAsk = _dAskYes = _dAskNo = _dClickSet = _dStumbles = 0;
            _dTickTicks = 0; _dTickN = 0;
        }

        private static long Key(float x, float y) { return ((long)(int)Math.Floor(x / Cell) << 32) ^ (uint)(int)Math.Floor(y / Cell); }

        internal static Inn Find(string uid) { Inn i; return uid != null && _byUid.TryGetValue(uid, out i) ? i : null; }

        /// <summary>Recenzja calosci (scalenie6): liczba zajazdow wczytanych w tej sesji (0 - plik nie czytany, brak albo pusty). TownInns.CheckTimes.</summary>
        internal static int InnCount { get { return _inns.Count; } }

        /// <summary>Nazwa zajazdu po uid (stan noclegu z zapisu - TownInns.Rest.Town); null = nie ma takiego zajazdu w tej sesji.</summary>
        internal static string NameOf(string uid)
        {
            try { EnsureLoaded(); var i = Find(uid); return i != null ? i.Name : null; }
            catch { return null; }
        }

        internal static float DistTo(Inn i, Vec2 p) { return i == null ? float.MaxValue : p.Distance(new Vec2(i.X, i.Y)); }

        /// <summary>Odleglosc partii gracza od miejsca postoju zajazdu (uid); brak zajazdu - nieskonczonosc.</summary>
        internal static float PlayerDist(string uid)
        {
            var mp = MobileParty.MainParty;
            if (mp == null) return float.MaxValue;
            EnsureLoaded();   // stan noclegu z zapisu (sen w zajezdzie) moze pytac przed pierwszym tickiem
            return DistTo(Find(uid), mp.GetPosition2D);
        }

        /// <summary>Partia gracza przy zajezdzie (poza osada, do EnterR + zapas na krok podczas snu).</summary>
        internal static bool AtInn(Inn i)
        {
            var mp = MobileParty.MainParty;
            return i != null && mp != null && mp.CurrentSettlement == null && DistTo(i, mp.GetPosition2D) <= EnterR + 0.6f;
        }

        /// <summary>Najblizszy zajazd w promieniu r od punktu (siatka 25 jedn.).</summary>
        internal static Inn Nearest(Vec2 p, float r)
        {
            Inn best = null;
            float bd = r;
            int cx = (int)Math.Floor(p.x / Cell), cy = (int)Math.Floor(p.y / Cell);
            for (int ix = -1; ix <= 1; ix++)
                for (int iy = -1; iy <= 1; iy++)
                {
                    List<Inn> l;
                    if (!_grid.TryGetValue(((long)(cx + ix) << 32) ^ (uint)(cy + iy), out l)) continue;
                    foreach (var i in l)
                    {
                        float d = DistTo(i, p);
                        if (d <= bd) { bd = d; best = i; }
                    }
                }
            return best;
        }

        // ------------------------------------------------------------ wczytanie (raz na sesje)
        internal static void EnsureLoaded()
        {
            if (_loaded) return;
            if (Campaign.Current == null) return;
            _loaded = true;
            var sw = Stopwatch.StartNew();
            try { Load(); }
            catch (Exception e) { _inns.Clear(); _byUid.Clear(); _grid.Clear(); _loadText = "BLAD wczytania (" + e.GetType().Name + ") - zajazdow brak do nastepnego wczytania"; Stumble("Load", e); }
            _loadText += "; czas " + sw.ElapsedMilliseconds + " ms";
        }

        private static string DataPath() { _fileUsed = UsedFile(); return MapVillageData.DataPathFor(_fileUsed); }

        private static void Load()
        {
            string path = DataPath();
            if (path == null || !System.IO.File.Exists(path)) { _loadText = "brak pliku " + _fileUsed + " - zajazdow brak (gra bez zmian)"; return; }
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            uint crc = MapVillageData.Crc32(bytes);
            string text = Encoding.UTF8.GetString(bytes);
            string[] head = null;
            uint fileScene = 0; bool hasScene = false;
            var rows = new List<Inn>();
            int line = 0;
            foreach (string raw in text.Split('\n'))
            {
                line++;
                string s = raw.TrimEnd('\r');
                if (s.Length == 0) continue;
                if (s.StartsWith("#id\t", StringComparison.Ordinal)) { head = s.Substring(1).Split('\t'); continue; }
                if (s.StartsWith("#", StringComparison.Ordinal))
                {
                    int k = s.IndexOf("scene_xml_crc:", StringComparison.Ordinal);
                    if (k >= 0 && uint.TryParse(s.Substring(k + 14).Trim(), NumberStyles.Integer, Inv, out fileScene)) hasScene = true;
                    continue;
                }
                if (head == null) { _bad++; continue; }
                _rows++;
                var a = s.Split('\t');
                var inn = ParseRow(head, a);
                if (inn == null) { _bad++; continue; }
                inn.Line = line;
                rows.Add(inn);
            }
            // CRC sceny: plik z innej wersji mapy - zajazdy wylaczone (polozenia nie pasuja)
            string sceneNote = hasScene ? "brak sceny do sprawdzenia" : "plik bez CRC sceny";
            try
            {
                var ms = Campaign.Current.MapSceneWrapper;
                if (hasScene && ms != null)
                {
                    uint sc = ms.GetSceneXmlCrc();
                    if (sc != fileScene) { _sceneBad = rows.Count; _loadText = "plik " + _fileUsed + " (crc32 " + crc.ToString("X8") + ") z innej mapy (CRC sceny pliku " + fileScene + ", gry " + sc + ") - zajazdy WYLACZONE"; return; }
                    sceneNote = "CRC sceny zgodne (" + sc + ")";
                }
            }
            catch { sceneNote = "CRC sceny nie odczytane"; }
            // wioski na mapie z ukladu tej sesji (v5 albo nowszy) - budynek zajazdu nie stoi w wiosce
            var vg = new Dictionary<long, List<Vec2>>();
            string layout = "-";
            try
            {
                string vp = MapVillageData.DataPath();
                if (vp != null && System.IO.File.Exists(vp))
                {
                    var vf = MapVillageData.Parse(System.IO.File.ReadAllText(vp, Encoding.UTF8));
                    foreach (var r in vf.Records)
                    {
                        long k = Key(r.X, r.Y);
                        List<Vec2> l;
                        if (!vg.TryGetValue(k, out l)) vg[k] = l = new List<Vec2>();
                        l.Add(new Vec2(r.X, r.Y));
                    }
                    layout = MapVillageData.LayoutFileName + " (" + vf.Records.Count + " wiosek)";
                    _sideAll = new List<Vec2>(vf.Records.Count);
                    foreach (var r in vf.Records) _sideAll.Add(new Vec2(r.X, r.Y));
                }
            }
            catch (Exception e) { layout = "blad odczytu (" + e.GetType().Name + ")"; }
            foreach (var inn in rows)
            {
                Settlement v = null;
                try { v = Settlement.Find(inn.VillageId); } catch { v = null; }
                if (v == null || !v.IsVillage || v.Village == null) { _noVillage++; continue; }
                if (NearAny(vg, inn.Bx, inn.By, NearMapVillage)) { _nearVillage++; continue; }
                if (_byUid.ContainsKey(inn.Uid)) { _bad++; continue; }
                inn.Village = v;
                _inns.Add(inn);
                _byUid[inn.Uid] = inn;
                long key = Key(inn.X, inn.Y);
                List<Inn> gl;
                if (!_grid.TryGetValue(key, out gl)) _grid[key] = gl = new List<Inn>();
                gl.Add(inn);
                _tierN[Math.Max(1, Math.Min(3, inn.Tiers))]++;
            }
            string farText = FarText();
            _loadText = "plik " + _fileUsed + " (crc32 " + crc.ToString("X8") + ", wierszy " + _rows + "), " + sceneNote + "; zajazdow " + _inns.Count
                        + " (odrzucone: zly wiersz " + _bad + ", brak wsi gry " + _noVillage + ", przy wiosce na mapie " + _nearVillage + " - uklad " + layout + ")"
                        + "; jakosci 1/2/3: " + _tierN[1] + "/" + _tierN[2] + "/" + _tierN[3] + "; " + DensityText() + farText;
            _sideAll = null;
        }

        private static List<Vec2> _sideAll;      // wioski ukladu tej sesji - tylko na czas Load (linia odleglosci od wiosek)

        /// <summary>f10-uklad: odleglosc budynku kazdego zajazdu od najblizszej wioski pobocznej (uklad tej sesji) i wsi gry - min, mediana i ile zajazdow blizej niz
        /// FarVillage (generator: 0; klucz VillageLayoutV7 NIE - plik zajazdow z ukladu v7 przy wioskach v6, wiec moga byc). Raz na sesje, n x wiosek. Nie rzuca.</summary>
        private static string FarText()
        {
            try
            {
                if (_inns.Count == 0) return "";
                var mains = new List<Vec2>();
                var seen = new HashSet<Village>();
                foreach (var st in Settlement.All)
                    if (st != null && st.IsVillage && st.Village != null && seen.Add(st.Village)) mains.Add(st.GetPosition2D);
                var side = _sideAll ?? new List<Vec2>();
                var ds = new List<float>(_inns.Count);
                float minS = float.MaxValue, minM = float.MaxValue;
                int near = 0;
                foreach (var inn in _inns)
                {
                    var b = new Vec2(inn.Bx, inn.By);
                    float s = float.MaxValue, m = float.MaxValue;
                    foreach (var v in side) { float d = v.Distance(b); if (d < s) s = d; }
                    foreach (var v in mains) { float d = v.Distance(b); if (d < m) m = d; }
                    minS = Math.Min(minS, s); minM = Math.Min(minM, m);
                    float both = Math.Min(s, m);
                    ds.Add(both);
                    if (both < FarVillage - 0.01f) near++;
                }
                ds.Sort();
                return "; od wiosek (f10-uklad): budynek - najblizsza wioska poboczna min " + (side.Count > 0 ? minS.ToString("0.0", Inv) : "-") + ", wies gry min "
                       + (mains.Count > 0 ? minM.ToString("0.0", Inv) : "-") + ", mediana " + ds[ds.Count / 2].ToString("0.0", Inv) + ", blizej niz " + FarVillage.ToString("0.00", Inv)
                       + " jedn. " + near + " zajazdow";
            }
            catch (Exception e) { Stumble("FarText", e); return "; od wiosek (f10-uklad): blad"; }
        }

        private static bool NearAny(Dictionary<long, List<Vec2>> g, float x, float y, float r)
        {
            int cx = (int)Math.Floor(x / Cell), cy = (int)Math.Floor(y / Cell);
            var p = new Vec2(x, y);
            for (int ix = -1; ix <= 1; ix++)
                for (int iy = -1; iy <= 1; iy++)
                {
                    List<Vec2> l;
                    if (!g.TryGetValue(((long)(cx + ix) << 32) ^ (uint)(cy + iy), out l)) continue;
                    foreach (var v in l) if (v.Distance(p) < r) return true;
                }
            return false;
        }

        private static Inn ParseRow(string[] head, string[] a)
        {
            string Get(string col) { int i = Array.IndexOf(head, col); return i >= 0 && i < a.Length ? a[i] : null; }
            float F(string col) { float v; return float.TryParse(Get(col), NumberStyles.Float, Inv, out v) ? v : float.NaN; }
            int I(string col, int d) { int v; return int.TryParse(Get(col), NumberStyles.Integer, Inv, out v) ? v : d; }
            var inn = new Inn
            {
                Id = I("id", 0), Uid = Get("uid") ?? "", Name = Get("name") ?? "", VillageId = Get("village_id") ?? "", Kind = Get("kind") ?? "road",
                Culture = Get("culture") ?? "", X = F("x"), Y = F("y"), Bx = F("bx"), By = F("by"), FrontDeg = F("front_deg"), Spacing = F("spacing"),
                People = I("people", 0), Tiers = Math.Max(1, Math.Min(3, I("tiers", 1)))
            };
            if (inn.Uid.Length == 0 || inn.Uid.Length > 64 || inn.Name.Length == 0 || inn.VillageId.Length == 0) return null;
            if (float.IsNaN(inn.X) || float.IsNaN(inn.Y)) return null;
            if (float.IsNaN(inn.Bx) || float.IsNaN(inn.By)) { inn.Bx = inn.X; inn.By = inn.Y; }
            if (float.IsNaN(inn.FrontDeg)) inn.FrontDeg = 0f;
            return inn;
        }

        /// <summary>Gestosc wedlug ludzi: pasma ludzi wokol (z pliku) - liczba zajazdow i mediana odleglosci do najblizszego sasiada; najblizszy sasiad w calym swiecie.
        /// Kontrola "armii zajazdow" (prog nzajazdy: najblizszy sasiad >= 25, mediana pasma najrzadszego >= mediany najgestszego).</summary>
        private static string DensityText()
        {
            int n = _inns.Count;
            if (n == 0) return "gestosc: -";
            var nn = new float[n];
            for (int i = 0; i < n; i++)
            {
                float b = float.MaxValue;
                for (int j = 0; j < n; j++) if (i != j) b = Math.Min(b, new Vec2(_inns[i].X, _inns[i].Y).Distance(new Vec2(_inns[j].X, _inns[j].Y)));
                nn[i] = b;
            }
            float min = float.MaxValue;
            foreach (var d in nn) min = Math.Min(min, d);
            int[] bandLo = { 0, 30000, 60000, 90000 };
            string[] bandName = { "<30k", "30-60k", "60-90k", ">=90k" };
            var sb = new StringBuilder("wedlug ludzi wokol (pasmo: zajazdow, mediana najblizszego sasiada):");
            for (int b = 0; b < bandLo.Length; b++)
            {
                int lo = bandLo[b], hi = b + 1 < bandLo.Length ? bandLo[b + 1] : int.MaxValue;
                var l = new List<float>();
                for (int i = 0; i < n; i++) if (_inns[i].People >= lo && _inns[i].People < hi) l.Add(nn[i]);
                l.Sort();
                sb.Append(' ').Append(bandName[b]).Append(": ").Append(l.Count).Append(", ").Append(l.Count > 0 ? l[l.Count / 2].ToString("0.0", Inv) : "-").Append(';');
            }
            var peo = new List<int>();
            foreach (var i in _inns) peo.Add(i.People);
            peo.Sort();
            int west = 0, essos = 0;
            foreach (var i in _inns) if (i.X > 750f) essos++; else west++;   // Essos na wschodzie mapy ROT (Westeros do x ok. 690, Essos od ok. 815)
            sb.Append(" ludzie wokol min/mediana/maks ").Append(peo[0]).Append('/').Append(peo[peo.Count / 2]).Append('/').Append(peo[peo.Count - 1])
              .Append("; najblizszy sasiad min ").Append(min.ToString("0.0", Inv)).Append(" (prog 25)")
              .Append("; lad: Westeros ").Append(west).Append(", Essos ").Append(essos);
            return sb.ToString();
        }

        // ------------------------------------------------------------ wejscie
        /// <summary>RoadsideInnsView: klik na zajazd - partia jedzie do miejsca postoju (jak klik w teren), menu po dojechaniu (Tick). true = klik obsluzony.</summary>
        internal static bool OnClick(Inn inn)
        {
            try
            {
                if (!On || inn == null) return false;
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MapEvent != null || mp.CurrentSettlement != null) return false;
                if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
                var ms = Game.Current != null ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
                if (ms == null) return false;
                var target = new CampaignVec2(new Vec2(inn.X, inn.Y), true);
                ms.ProcessTravel(target);
                _pendingUid = inn.Uid;
                _askedUid = null;
                _dClickSet++;
                return true;
            }
            catch (Exception e) { Stumble("OnClick", e); return false; }
        }

        /// <summary>CampaignEvents.TickEvent: 4 razy na sekunde (zegar rzeczywisty) - partia gracza przy zajezdzie: po kliku menu, po zwyklym postoju pytanie.
        /// Koszt: siatka 3 x 3 komorek wokol jednej partii (bez petli po swiecie).</summary>
        internal static void Tick()
        {
            if (!On) return;
            long tq = Stopwatch.GetTimestamp();
            if (tq - _lastCheck < Stopwatch.Frequency / 4) return;
            _lastCheck = tq;
            if (!_loaded) { try { EnsureLoaded(); } catch (Exception e) { Stumble("Tick.Load", e); } }   // wczytanie pliku poza pomiarem kosztu sprawdzenia
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_inns.Count == 0) return;
                var c = Campaign.Current;
                if (c == null || c.CurrentMenuContext != null) return;   // menu otwarte (takze nasze) - nic
                _current = null;                                          // menu zamkniete - zaden zajazd nie jest otwarty (po snie, po "Ride on")
                if (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null) return;   // spotkanie w toku - nic
                if (PopupBlocked()) return;                                                          // recenzja: okno, rozmowa albo autotest - nic (jak SiegeRansom.CanShow)
                var mp = MobileParty.MainParty;
                if (mp == null || Hero.MainHero == null || !Hero.MainHero.IsAlive) return;
                if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.IsCurrentlyAtSea || TaleWorlds.CampaignSystem.PlayerCaptivity.IsCaptive
                    || mp.SiegeEvent != null || mp.BesiegerCamp != null)
                { _pendingUid = null; return; }
                var pos = mp.GetPosition2D;
                if (_askedUid != null && DistTo(Find(_askedUid), pos) > LeaveR) _askedUid = null;
                Inn near = Nearest(pos, EnterR);
                if (_pendingUid != null)
                {
                    var p = Find(_pendingUid);
                    if (p == null) _pendingUid = null;
                    else if (near == p) { _pendingUid = null; _askedUid = p.Uid; _dClick++; Open(p, "klik na zajazd"); return; }
                    else if (mp.TargetPosition.ToVec2().Distance(new Vec2(p.X, p.Y)) > EnterR + 0.5f) _pendingUid = null;   // gracz pojechal gdzie indziej
                }
                if (near == null || near.Uid == _askedUid) return;
                if (pos.Distance(mp.TargetPosition.ToVec2()) > ArriveEps) return;   // jeszcze jedzie (mija zajazd)
                if (mp.Army != null && mp.Army.LeaderParty != mp) return;
                _askedUid = near.Uid;
                Ask(near);
            }
            catch (Exception e) { Stumble("Tick", e); }
            finally { _dTickTicks += Stopwatch.GetTimestamp() - t0; _dTickN++; }
        }

        /// <summary>Recenzja (wzor SiegeRansom.CanShow): nie otwieramy pytania ani menu, gdy stoi inne okno (np. NightfallAsk - dwa okna z pauza jedno na drugim),
        /// trwa rozmowa na mapie (bez PlayerEncounter - ActivateGameMenu w srodku rozmowy), aktywny stan gry to nie mapa, albo biegnie autotest CrashScribe (nikt nie
        /// kliknie okna z pauza, partia autotestu stajaca przy zajezdzie stalaby do konca). Tylko odczyt.</summary>
        private static bool PopupBlocked()
        {
            if (AutotestActive()) return true;
            if (InformationManager.IsAnyInquiryActive()) return true;
            if (ConversationOn()) return true;
            var ms = Game.Current != null && Game.Current.GameStateManager != null ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
            return ms == null;
        }

        private static bool ConversationOn()
        {
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            return cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive);
        }

        // autotest (CrashScribe.Autotest.Active - pole tylko w DLL autotestu; ta sama proba co SiegeRansom.AutotestActive, raz na sesje gry)
        private static int _autotest = -1;

        private static bool AutotestActive()
        {
            if (_autotest >= 0) return _autotest == 1;
            _autotest = 0;
            try
            {
                foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    var f = t != null ? t.GetField("Active", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public) : null;
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(null)) _autotest = 1;
                    break;
                }
            }
            catch { _autotest = 0; }
            return _autotest == 1;
        }

        private static void Ask(Inn inn)
        {
            _dAsk++;
            string body = "You draw rein by " + inn.Name + ", " + KindText(inn) + " on the lands of " + inn.Village.Name + ". " + BedsText(inn) + " Go in?";
            InformationManager.ShowInquiry(new InquiryData(inn.Name, body, true, true, "Go in", "Ride on",
                delegate
                {
                    try
                    {
                        _dAskYes++;
                        if (ConversationOn()) return;   // recenzja: w czasie okna zaczela sie rozmowa - bez menu (pytanie wroci po odjechaniu i ponownym postoju)
                        Open(inn, "pytanie przy postoju");
                    }
                    catch (Exception e) { Stumble("Ask.Yes", e); }
                },
                delegate { _dAskNo++; }), true);
        }

        private static void Open(Inn inn, string how)
        {
            var c = Campaign.Current;
            if (c == null || c.CurrentMenuContext != null || inn == null) return;
            if (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null) return;
            if (ConversationOn()) return;   // recenzja: nie w srodku rozmowy na mapie
            var mp = MobileParty.MainParty;
            if (mp == null || mp.CurrentSettlement != null || mp.MapEvent != null) return;
            _current = inn;
            _dEnter++;
            try { mp.SetMoveModeHold(); } catch { }
            GameMenu.ActivateGameMenu(Menu);
            Log.Info(P + "wejscie do " + inn.Name + " (" + inn.Uid + ", okreg " + inn.VillageId + ", jakosci " + inn.Tiers + ") - " + how + ", partia " + DistTo(inn, mp.GetPosition2D).ToString("0.00", Inv) + " jedn. od postoju.");
        }

        internal static string KindText(Inn inn)
        {
            return inn.Kind == "crossroad" ? "an inn at a crossroads" : inn.Kind == "road" ? "an inn by the road" : "an inn on the track";
        }

        internal static string BedsText(Inn inn)
        {
            if (inn.Tiers >= 3) return "The keeper has a common room, decent beds and even a few good rooms.";
            if (inn.Tiers == 2) return "The keeper has a common room and a few decent beds.";
            return "The keeper has a common room - straw and a roof.";
        }

        // ------------------------------------------------------------ menu
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenu(Menu, "{=!}{ARM_ROADINN_TEXT}", MenuInit, GameMenu.MenuOverlayType.None);
                for (int i = 0; i < 3; i++)
                {
                    int q = i;
                    starter.AddGameMenuOption(Menu, "arm_roadinn_q" + q, "{=!}{ARM_ROADINN_Q" + q + "}",
                        delegate (MenuCallbackArgs a) { return TownInns.RoadQCondition(a, CurrentOrNear(), q); },
                        delegate (MenuCallbackArgs a) { TownInns.RoadCommit(CurrentOrNear(), q); }, false, q);
                }
                starter.AddGameMenuOption(Menu, "arm_roadinn_gossip", "{=!}Listen to the gossip", Rumours.RoadGossipCondition, Rumours.RoadGossipConsequence, false, 5);
                starter.AddGameMenuOption(Menu, "arm_roadinn_leave", "{=!}Ride on",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { _current = null; RoadsideInnStay.Leave("Ride on"); GameMenu.ExitToLast(); }, true, 9);   // m95-zajazd: partia z powrotem na mapie przed zamknieciem menu
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        /// <summary>Zajazd menu: z wejscia, a po wczytaniu zapisu w srodku menu - najblizszy partii gracza.</summary>
        internal static Inn CurrentOrNear()
        {
            if (_current != null) return _current;
            try
            {
                EnsureLoaded();
                var mp = MobileParty.MainParty;
                if (mp != null && mp.CurrentSettlement == null) _current = Nearest(mp.GetPosition2D, EnterR + 0.6f);
            }
            catch { }
            return _current;
        }

        private static void MenuInit(MenuCallbackArgs args)
        {
            try
            {
                var inn = CurrentOrNear();
                string mesh = null;
                try { if (inn != null && inn.Village != null && inn.Village.SettlementComponent != null) mesh = inn.Village.SettlementComponent.WaitMeshName; } catch { }
                try { args.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh); } catch { }
                RoadsideInnStay.OnMenuInit(args, inn);   // m95-zajazd: partia schowana na czas pobytu (wlasny try); dzwiek - RoadsideInnStay.InnMenuSound
                var sb = new StringBuilder();
                if (inn == null) sb.Append("You stand by the road. There is no inn here.");
                else
                {
                    sb.Append(inn.Name).Append(" - ").Append(KindText(inn)).Append(" on the lands of ").Append(inn.Village.Name).Append('.');
                    try
                    {
                        var owner = inn.Village.OwnerClan;
                        if (owner != null) sb.Append(" The keeper pays his dues to ").Append(owner.Name).Append('.');
                    }
                    catch { }
                    sb.Append(" About ").Append(Rumours.Round(inn.People).ToString("N0", Inv)).Append(" souls live in the villages around.");
                    sb.Append("\n\n").Append(BedsText(inn));
                    sb.Append(" The prices are those of the town inns (a man / a horse / your own room): common room 1 / 1 / " + TownInns.RoomOf(0));
                    if (inn.Tiers >= 2) sb.Append(", decent inn 2 / 2 / 8");
                    if (inn.Tiers >= 3) sb.Append(", good house 4 / 3 / 20");
                    sb.Append(". Every soldier pays for himself and his horse from his own purse; you pay for yourself, your horse and every horse that carries nobody. The coin stays with the village whose lands the inn stands on.");
                    if (!TownInns.On) sb.Append("\n\nLodgings need Town Inns and Night Rest (game options).");
                    if (Rumours.On) sb.Append("\n\nDrovers, pilgrims and pedlars sit by the hearth, trading the news of the road.");
                }
                MBTextManager.SetTextVariable("ARM_ROADINN_TEXT", sb.ToString());
            }
            catch (Exception e) { Stumble("MenuInit", e); }
        }

        // ------------------------------------------------------------ sesja i doba
        private static string Keys()
        {
            var s = Settings.Current;
            return "klucze: Roadside Inns " + (s != null && s.RoadsideInns ? "tak" : "WYLACZONE") + ", Inn Gossip " + (s != null && s.InnGossip ? "tak" : "WYLACZONE")
                   + ", Town Inns " + (s != null && s.TownInns ? "tak" : "WYLACZONE") + ", Night Rest " + (s != null && s.NightRestEnabled ? "tak" : "WYLACZONE");
        }

        internal static void SessionStart()
        {
            try
            {
                if (On) EnsureLoaded();
                Log.Info(P + "start sesji | " + Keys() + " | " + (On ? _loadText : "zajazdy wylaczone - plik nie czytany") + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
            RoadsideInnStay.SessionStart();   // m95-zajazd: latki pobytu i linia "Zajazdy (ZJ): pobyt - start sesji" (wlasny try)
        }

        /// <summary>Linia doby (z OnDailyTick) - przy wlaczonym kluczu RoadsideInns.</summary>
        internal static void Daily(int day)
        {
            RoadsideInnStay.Daily(day);   // m95-zajazd: linia "Zajazdy (ZJ): pobyt - dzien N" (wlasny try, takze przy wylaczonych zajazdach - zeby bylo widac zdjecie stanu)
            try
            {
                if (!On) { DayReset(); TownInns.RoadDayReset(); return; }
                double tickMs = _dTickN > 0 ? _dTickTicks * 1000.0 / Stopwatch.Frequency / _dTickN : 0.0;
                Log.Info(P + "dzien " + day + " | " + Keys() + " | zajazdow " + _inns.Count + (_loaded ? "" : " (plik jeszcze nie czytany)")
                         + " | wejscia " + _dEnter + " (po kliku " + _dClick + ", z pytania " + _dAskYes + "), pytania " + _dAsk + ", odmowy " + _dAskNo + ", kliki na zajazd " + _dClickSet
                         + " | " + TownInns.RoadDayText()
                         + " | widok: " + InnViewStats.DayText()
                         + " | tick dojazdu: " + _dTickN + " sprawdzen, srednio " + tickMs.ToString("0.0000", Inv) + " ms"
                         + " | potkniecia dzis " + _dStumbles + " (od wczytania " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayReset(); }
        }
    }

    /// <summary>ZJ: liczniki widoku zajazdow (RoadsideInnsView) - osobno, bez typow SandBox.View, zeby linia doby nie ladowala typu widoku (blad ladowania typu po
    /// aktualizacji gry zostaje w rejestracji widoku - SubModuleMain).</summary>
    internal static class InnViewStats
    {
        internal static int Created, CreatedDay, ShownMax, HideDay, ShowDay, Failed, MeshMiss, PiecesWater, StCreate, StVis, StHover, StClick, Clicks, ClicksDay;
        internal static long CreateTicks;

        /// <summary>Od wczytania mapy (RoadsideInnsView.OnInitialize) - liczniki "od wczytania" od zera.</summary>
        internal static void MapReset()
        {
            Created = ShownMax = Failed = MeshMiss = PiecesWater = StCreate = StVis = StHover = StClick = Clicks = 0;
            CreateTicks = 0;
        }

        /// <summary>Nowa kampania albo wczytanie (RoadsideInns.Reset - konstruktor ArmouryBehavior): liczniki doby od zera.</summary>
        internal static void DayZero() { CreatedDay = ShowDay = HideDay = ClicksDay = 0; }

        /// <summary>Czesc linii doby zajazdow: obrazki postawione (od wczytania mapy i dzis), pokazane, przebudowy (zawsze 0 - obrazek powstaje raz), potkniecia.</summary>
        internal static string DayText()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double ms = Created > 0 ? CreateTicks * 1000.0 / Stopwatch.Frequency / Created : 0.0;
            string t = "obrazki postawione " + Created + " (dzis " + CreatedDay + "), pokazane najwiecej naraz " + ShownMax + ", pokaz/schowaj dzis " + ShowDay + "/" + HideDay
                       + ", przebudowy 0 (obrazek raz na mape), pominiete " + Failed + ", brak siatek " + MeshMiss + ", elementy zdjete z wody " + PiecesWater
                       + ", sredni czas obrazka " + ms.ToString("0.000", inv) + " ms, kliki " + ClicksDay + " | potkniecia widoku: tworzenie " + StCreate + ", widocznosc " + StVis
                       + ", dymek " + StHover + ", klik " + StClick;
            CreatedDay = ShowDay = HideDay = ClicksDay = 0;
            return t;
        }
    }
}
