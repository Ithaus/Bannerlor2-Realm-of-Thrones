using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// F4 (noc 17/18.10, paczka f4-warsztaty; audyt ekonomii tej nocy, synteza P4 "Przezbrojenie warsztatow liczy zysk ta sama cena i tym samym wsadem co prawdziwy cykl";
    /// klucz WorkshopRetoolFairScore) - OCENA TYPU WARSZTATU TAK, JAK NAPRAWDE ZAPLACI MU MIASTO.
    /// Dowod [L] (fin86, Armoury-2026-10-17_23-22-37.log): "Warsztaty za cena (E3-D3)" 49 zmian, spodziewany wynik srednio: kuznia srebra x8 16 603 d/d, aksamit x4 81 538,
    /// welna x20 3 045, kuznia x12 1 366; rzeczywiscie ("Warsztaty towarowe" d109230): kuznie srebra 19, pracowalo 0, cykli 0, -4.8 d na warsztat; aksamit 13, pracowal 1;
    /// welna 63.3, kuznia 52.1 d na warsztat. Warsztaty 109075 -> 109235: len 17 -> 8, browary 12 -> 3, garbarnie 12 -> 2, garncarze 6 -> 1 (tak samo bez KS, fin84).
    /// Przyczyna [K]: NoTownMines.MarketScore liczyl cene sztuki o srodkowej wartosci kategorii (Price - pusta polka wyrobu = cena z sufitu), a wsad tylko jako "jest"
    /// (WorkshopInputs.HasInput), choc prawdziwy cykl dostaje najwyzej cene sprawiedliwa (WorkshopTrade.FairCycle: (wsad + place) x marza mistrza) i rusza tylko, gdy
    /// wsad lezy na polce miasta.
    /// Regula (klucz wlaczony, razem z E3-D3 WorkshopRetoolByPrice; dla podmiany kopalni N10 - z NoTownMines):
    ///  1. cykl linii = WorkshopTrade.LineCycleAt (prog gry, cena miasta, sufit ceny sprawiedliwej - ta sama regula co cykl);
    ///  2. cykli na dobe = min(szybkosc linii gry, wsad regionu na dobe / warsztaty regionu, ktore biora ten wsad (z nowym) / sztuk wsadu na cykl); linie od najlepszej,
    ///     wsad dzielony miedzy linie typu; warsztat, ktory sie przezbraja, z kapitalem ponizej ceny wsadu linii - ta linia nie rusza (CanNotableWorkshopProduceThisCycle);
    ///  3. wsad regionu naprawde dostepny = srednia produkcja 28 dob x f + polka / 30 dob, f = min(1, polka / 7 dob produkcji) - produkcja, ktora zostaje na rynku
    ///     (przy pustych polkach biora ja inni: lud, wojsko, karawany); produkcja ze zdarzenia gry OnItemProduced (wies -> jej miasto targowe, warsztaty i rzemioslo
    ///     miasta -> miasto; wies KS i bez KS - te same zdarzenia BK/gry) oraz z drog Armoury, ktore dopisuja surowiec osadzie BEZ zdarzenia (NoteProduced, recenzja:
    ///     pola mieszczan KS StartCalibration.DeliverTownFields, las wsi VillageWoodlot, zlom i sztaby -> zelazo ArmsScrap / Drill / GoodsPaths.Ingots); miasta w promieniu 150 jedn. mapy
    ///     (mediana 4 innych miast ROT);
    ///  4. typ, ktory w regionie juz stoi i ma warsztaty z >= 60 dobami rachunku: wynik = min(zmierzony wynik roku tych warsztatow + utrzymanie, szacunek z p. 1-3);
    ///     typ obecny warsztatu przezbrajanego - jego wlasny zmierzony wynik (WorkshopTrade.Rec: srednia roku, >= 60 dob) + utrzymanie;
    ///  5. kara za dubel: x0.8 za kazdy warsztat tego typu w REGIONIE rynku (dotad w miescie); bramka wsadu w okregu (HasInput) bez zmian.
    /// Pierwsze 7 pelnych dob pomiaru (stary zapis bez klucza arm_wsinput) - regula tylko mierzy: przezbrojenia i typ z cen przy bankructwie odlozone, N10 po staremu.
    /// Kto placi, kto dostaje: nic nowego - zmienia sie tylko WYBOR typu; sprzet nadal wlasciciel -> kasa miasta (WorkshopTrade.ConvertPrefix/Postfix), cykle jak dotad.
    /// Zapis: "arm_wsinput" (srednie produkcji wedlug miasta targowego i kategorii, straznik 60 000 znakow); stary zapis bez klucza = pomiar od zera, starsza DLL klucza
    /// nie czyta. Wyjatek per obiekt - licznik potkniec, regula dalej czynna.
    /// </summary>
    internal static class WorkshopFairScore
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float RegionRange = 150f;        // promien regionu rynku (jedn. mapy, linia prosta): ROT-Map settlements.xml - mediana 4 innych miast w promieniu
        internal const float DupKeep = 0.8f;            // kara za warsztat tego typu w regionie (jak dywersyfikacja BK w DecideBestWorkshopType)
        internal const float NoScore = -1e9f;
        private const int Span = 28, WarmDays = 7, ShelfDays = 30, KeepDays = 7, MeasuredDays = 60, ActualAfterDays = 30, SaveMaxChars = 60000;
        private const string SaveTag = "f4v1";

        // ------------------------------------------------------------ pomiar wsadu (miasto targowe StringId -> kategoria -> sztuki na dobe)
        private static readonly Dictionary<string, Dictionary<ItemCategory, float>> _avg = new Dictionary<string, Dictionary<ItemCategory, float>>();
        private static readonly Dictionary<string, Dictionary<ItemCategory, int>> _cur = new Dictionary<string, Dictionary<ItemCategory, int>>();
        private static int _days;                       // pelne doby w sredniej (z zapisu albo od wczytania)
        private static bool _firstDay = true;           // pierwsza doba sesji niepelna (od wczytania do pierwszego ticku dnia) - nie liczymy
        private static string _pending;                 // dane z zapisu, rozwiazywane przy pierwszym uzyciu (kategorie z MBObjectManager)
        private static int _loadedDays = -1, _loadedTowns, _saveCut;
        private static HashSet<ItemCategory> _inputs;   // kategorie wsadu typow widocznych (tylko te mierzymy)
        private static readonly Dictionary<WorkshopType, HashSet<ItemCategory>> _uses = new Dictionary<WorkshopType, HashSet<ItemCategory>>();
        private static readonly Dictionary<Town, List<Town>> _region = new Dictionary<Town, List<Town>>();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly object Gate = new object();   // licznik doby _cur: zdarzenie gry i doba (na wypadek wywolan spoza watku glownego)

        // ------------------------------------------------------------ pamiec doby
        private sealed class TD
        {
            public Town T;
            public readonly Dictionary<ItemCategory, float> Prod = new Dictionary<ItemCategory, float>();     // produkcja na dobe (srednia 28 dob)
            public readonly Dictionary<ItemCategory, float> Shelf = new Dictionary<ItemCategory, float>();    // sztuk na polce dzis
            public readonly Dictionary<ItemCategory, int> Users = new Dictionary<ItemCategory, int>();        // warsztaty widoczne, ktore biora ten wsad
            public readonly Dictionary<WorkshopType, int> Types = new Dictionary<WorkshopType, int>();
            public readonly Dictionary<WorkshopType, double[]> Meas = new Dictionary<WorkshopType, double[]>(); // suma zmierzonych wynikow, liczba warsztatow
            public List<TD> Region;
        }
        private sealed class LineEval { public bool Runs; public float Net, In; }
        private static Dictionary<Town, TD> _data;
        private static int _dataDay = int.MinValue;
        private static readonly Dictionary<Town, Dictionary<WorkshopType, LineEval[]>> _lines = new Dictionary<Town, Dictionary<WorkshopType, LineEval[]>>();

        /// <summary>Ocena jednego typu w jednym miescie (do decyzji i do linii logu).</summary>
        internal sealed class Quote
        {
            public float Score = NoScore, Estimate = NoScore, Measured = float.NaN, Cycles, LimitPer = -1f;
            public int MeasN;
            public ItemCategory Limit;
        }

        // ------------------------------------------------------------ liczniki doby i pamiec przezbrojen sesji
        private static int _dMeas, _dEst, _dNone, _dWarm, _dStumbles, _sessStumbles;
        private static long _dEvents;
        // recenzja (wazne): surowiec dopisany osadzie przez Armoury BEZ zdarzenia gry OnItemProduced (sztuki kategorii wsadu dzis, wedlug drogi)
        internal const int SrcTownFields = 0, SrcWoodlot = 1, SrcScrap = 2;
        private static readonly long[] _dDirect = new long[3];
        // recenzja (drobne): czas SAMEJ reguly F4 (pomiar doby, oceny, wybory - takze przy bankructwie i N10 miedzy liniami dnia), nie calego kroku E3-D3
        private static long _dTicks;
        [ThreadStatic] private static int _depth;
        private static long Tin() { return _depth++ == 0 ? Stopwatch.GetTimestamp() : 0L; }
        private static void Tout(long t0) { if (--_depth <= 0) { _depth = 0; if (t0 != 0L) Interlocked.Add(ref _dTicks, Stopwatch.GetTimestamp() - t0); } }
        private sealed class Track { public Workshop W; public WorkshopType To; public int Day; public float Exp; }
        private static readonly List<Track> _tracks = new List<Track>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopRetoolFairScore; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
        /// <summary>Regula decyduje (nie tylko mierzy): klucz, kampania trwa, >= 7 pelnych dob pomiaru wsadu.</summary>
        internal static bool Ready { get { if (!On || !Live) return false; ResolvePending(); return _days >= WarmDays; } }

        internal static void Reset()
        {
            _avg.Clear(); lock (Gate) _cur.Clear(); _days = 0; _firstDay = true; _pending = null; _loadedDays = -1; _loadedTowns = 0; _saveCut = 0;
            _inputs = null; _uses.Clear(); _region.Clear(); _err.Clear();
            _data = null; _dataDay = int.MinValue; _lines.Clear(); _tracks.Clear(); _sessStumbles = 0;
            EndDay();
        }

        /// <summary>Zerowanie licznikow doby (po linii "Warsztaty za cena (E3-D3)").</summary>
        internal static void EndDay() { _dMeas = _dEst = _dNone = _dWarm = _dStumbles = 0; _dEvents = 0; lock (Gate) { _dDirect[0] = _dDirect[1] = _dDirect[2] = 0; } Interlocked.Exchange(ref _dTicks, 0L); }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _sessStumbles++;
            try { if (_err.Add(where)) Log.Error("WorkshopFairScore." + where, e); } catch { }
        }

        /// <summary>Po przezbrojeniu - sklad warsztatow sie zmienil, pamiec doby od nowa przy nastepnym pytaniu.</summary>
        internal static void Invalidate() { _data = null; _dataDay = int.MinValue; }

        /// <summary>Przezbrojenie odlozone, bo pomiar wsadu jeszcze sie zbiera (pierwszy tydzien).</summary>
        internal static void NoteWarm() { _dWarm++; }

        // ------------------------------------------------------------ kategorie wsadu
        internal static HashSet<ItemCategory> Uses(WorkshopType t)
        {
            HashSet<ItemCategory> u;
            if (t == null) return new HashSet<ItemCategory>();
            if (_uses.TryGetValue(t, out u)) return u;
            u = new HashSet<ItemCategory>();
            if (t.Productions != null)
                foreach (var p in t.Productions)
                    if (p.Inputs != null) foreach (var i in p.Inputs) if (i.Item1 != null) u.Add(i.Item1);
            _uses[t] = u;
            return u;
        }

        private static HashSet<ItemCategory> Inputs()
        {
            if (_inputs != null) return _inputs;
            var set = new HashSet<ItemCategory>();
            try
            {
                if (Campaign.Current == null) return set;
                foreach (var t in WorkshopType.All) { if (t == null || t.IsHidden) continue; foreach (var c in Uses(t)) set.Add(c); }
            }
            catch (Exception e) { Stumble("Inputs", e); return set; }
            if (set.Count > 0) _inputs = set;   // typy jeszcze nie wczytane - bez pamietania
            return set;
        }

        // ------------------------------------------------------------ pomiar: zdarzenie gry i doba
        /// <summary>CampaignEvents.OnItemProducedEvent (WorkshopTradeBehavior): sztuki dopisane osadzie - wies liczy sie do swojego miasta targowego (TradeBound, inaczej
        /// Bound), miasto do siebie; tylko kategorie wsadu warsztatow widocznych. Gra (VillageGoodProductionCampaignBehavior) i BK (TickGoodProductionPatch) wolaja to
        /// zdarzenie przy produkcji kazdej wsi - wies KS i wies poza KS tak samo. Sam licznik.</summary>
        internal static void OnItemProduced(ItemObject item, Settlement st, int n) { Count(item, st, n, -1); }

        /// <summary>
        /// Recenzja (wazne): surowiec, ktory Armoury dopisuje osadzie BEZ zdarzenia gry OnItemProduced - ten sam licznik co zdarzenie (wies -> miasto targowe, miasto ->
        /// miasto, tylko kategorie wsadu). Nie wolamy zdarzenia gry: sluchaja go TownFolk, Stage6Measure, VillageByPeople, OreLedger i liczylyby drugi raz. Drogi:
        /// SrcTownFields - pola mieszczan KS na polke miasta (StartCalibration.DeliverTownFields: zboze, welna, len, bawelna, skory ... - dotad ok. 19% surowca doby
        /// na drodze KS poza pomiarem, fin86 d109235 9 420 szt.), SrcWoodlot - drewno lasu wsi do magazynu wsi (VillageWoodlot.TickPostfix, bez czesci prosto do dymarki),
        /// SrcScrap - zelazo ze zlomu i sztab na polce miasta (ArmsScrap: nadmiar broni miasta na zlom, Drill.SellScrap: zlom z cwiczen odkupiony przez kowali,
        /// GoodsPaths.Ingots: sztaby przekute na zelazo) - ta sama sztuka zelaza, co z kopalni, do kuzni. Sam licznik, nie rzuca.
        /// </summary>
        internal static void NoteProduced(Settlement st, ItemObject item, int n, int src) { Count(item, st, n, src < 0 || src > 2 ? 0 : src); }

        private static void Count(ItemObject item, Settlement st, int n, int src)
        {
            if (n <= 0 || item == null || st == null || !On) return;
            try
            {
                var c = item.ItemCategory;
                if (c == null || !Inputs().Contains(c)) return;
                Settlement mk = null;
                if (st.IsVillage) { var v = st.Village; if (v != null) mk = v.TradeBound ?? v.Bound; }
                else if (st.IsTown) mk = st;
                if (mk == null || !mk.IsTown || string.IsNullOrEmpty(mk.StringId)) return;
                lock (Gate)
                {
                    Dictionary<ItemCategory, int> d;
                    if (!_cur.TryGetValue(mk.StringId, out d)) { d = new Dictionary<ItemCategory, int>(); _cur[mk.StringId] = d; }
                    int x; d.TryGetValue(c, out x); d[c] = x + n;
                    if (src < 0) _dEvents += n; else _dDirect[src] += n;
                }
            }
            catch (Exception e) { Stumble(src < 0 ? "OnItemProduced" : "NoteProduced", e); }
        }

        /// <summary>Raz na dobe (z WorkshopRetool.Daily, przed decyzjami): dzisiejsza produkcja do sredniej 28 dob. Pierwsza doba sesji (niepelna) odrzucona.</summary>
        internal static void DayFold()
        {
            long t0 = Tin();
            try
            {
                Dictionary<string, Dictionary<ItemCategory, int>> cur;
                lock (Gate) { cur = new Dictionary<string, Dictionary<ItemCategory, int>>(_cur); _cur.Clear(); }
                if (!On) { _firstDay = true; return; }
                ResolvePending();
                if (_firstDay) { _firstDay = false; return; }
                _days++;
                float k = Math.Min(_days, Span);
                var keys = new List<string>(_avg.Keys);
                foreach (var key in cur.Keys) if (!_avg.ContainsKey(key)) keys.Add(key);
                foreach (var key in keys)
                {
                    Dictionary<ItemCategory, float> a; Dictionary<ItemCategory, int> c;
                    if (!_avg.TryGetValue(key, out a)) { a = new Dictionary<ItemCategory, float>(); _avg[key] = a; }
                    cur.TryGetValue(key, out c);
                    var cats = new List<ItemCategory>(a.Keys);
                    if (c != null) foreach (var cc in c.Keys) if (!a.ContainsKey(cc)) cats.Add(cc);
                    foreach (var cat in cats)
                    {
                        float old; a.TryGetValue(cat, out old);
                        int now = 0; if (c != null) c.TryGetValue(cat, out now);
                        float v = old + (now - old) / k;
                        if (now == 0 && v < 0.001f) a.Remove(cat); else a[cat] = v;
                    }
                }
                Invalidate();
            }
            catch (Exception e) { Stumble("DayFold", e); }
            finally { Tout(t0); }
        }

        // ------------------------------------------------------------ zapis
        /// <summary>SyncData (zapis): "f4v1|doby|miasto=kategoria:sztuki,...|..." - null przy wylaczonym kluczu (klucz zapisu nie powstaje, zapis jak dotad).</summary>
        internal static string Export()
        {
            if (!On) return null;
            try
            {
                ResolvePending();
                var sb = new StringBuilder(8192);
                sb.Append(SaveTag).Append('|').Append(_days.ToString(Inv));
                var part = new StringBuilder(256);
                foreach (var kv in _avg)
                {
                    if (kv.Value == null || kv.Value.Count == 0 || !SafeId(kv.Key)) continue;
                    part.Length = 0;
                    foreach (var c in kv.Value)
                    {
                        if (c.Key == null || !SafeId(c.Key.StringId) || !(c.Value >= 0.01f) || float.IsInfinity(c.Value)) continue;
                        part.Append(part.Length > 0 ? "," : "").Append(c.Key.StringId).Append(':').Append(c.Value.ToString("0.##", Inv));
                    }
                    if (part.Length == 0) continue;
                    if (sb.Length + part.Length + kv.Key.Length + 2 > SaveMaxChars) { _saveCut++; break; }   // straznik rozmiaru: reszta miast od zera po wczytaniu
                    sb.Append('|').Append(kv.Key).Append('=').Append(part);
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        private static bool SafeId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.IndexOf('|') < 0 && id.IndexOf('=') < 0 && id.IndexOf(',') < 0 && id.IndexOf(':') < 0;
        }

        /// <summary>SyncData (wczytanie): napis odlozony do pierwszego uzycia; brak klucza (stary zapis) = pomiar od zera.</summary>
        internal static void Import(string data) { _pending = string.IsNullOrEmpty(data) ? null : data; }

        private static void ResolvePending()
        {
            if (_pending == null) return;
            var om = MBObjectManager.Instance;
            if (om == null) return;   // jeszcze przed obiektami - nastepne pytanie sprobuje znowu
            string data = _pending; _pending = null;
            try
            {
                var parts = data.Split('|');
                int d;
                if (parts.Length < 2 || parts[0] != SaveTag || !int.TryParse(parts[1], NumberStyles.Integer, Inv, out d) || d < 0) { _loadedDays = 0; return; }
                int towns = 0;
                for (int i = 2; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq <= 0) continue;
                    var a = new Dictionary<ItemCategory, float>();
                    foreach (var e in parts[i].Substring(eq + 1).Split(','))
                    {
                        int col = e.IndexOf(':');
                        if (col <= 0) continue;
                        ItemCategory cat = null;
                        try { cat = om.GetObject<ItemCategory>(e.Substring(0, col)); } catch { cat = null; }
                        float v;
                        if (cat == null || !float.TryParse(e.Substring(col + 1), NumberStyles.Float, Inv, out v) || !(v >= 0f) || float.IsInfinity(v)) continue;
                        a[cat] = v;
                    }
                    if (a.Count > 0) { _avg[parts[i].Substring(0, eq)] = a; towns++; }
                }
                _days = Math.Max(_days, d); _loadedDays = d; _loadedTowns = towns;
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ------------------------------------------------------------ pamiec doby: region, wsad, warsztaty, wyniki zmierzone
        private static List<Town> RegionOf(Town t)
        {
            List<Town> l;
            if (_region.TryGetValue(t, out l)) return l;
            l = new List<Town> { t };
            var pos = t.Settlement.GetPosition2D;
            foreach (var o in Town.AllTowns)
            {
                if (o == null || o == t || o.Settlement == null || !o.Settlement.IsTown) continue;
                if (pos.Distance(o.Settlement.GetPosition2D) <= RegionRange) l.Add(o);
            }
            _region[t] = l;
            return l;
        }

        /// <summary>Zmierzony wynik warsztatu: srednia roku (WorkshopTrade.Rec - prawdziwe wyniki dob od zmiany typu, najwyzej rok) + utrzymanie, gdy rachunek ma >= 60 dob.</summary>
        internal static bool MeasuredOf(Workshop w, out float m)
        {
            m = 0f;
            float avg, slow; int days;
            if (w == null || !WorkshopTrade.RecOf(w, out avg, out slow, out days) || days < MeasuredDays) return false;
            m = slow + WorkshopTrade.UpkeepOf(w);
            return true;
        }

        private static Dictionary<Town, TD> Data()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_data != null && _dataDay == today) return _data;
            ResolvePending();
            var d = new Dictionary<Town, TD>();
            var ins = Inputs();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null || !t.Settlement.IsTown) continue;
                var td = new TD { T = t };
                try
                {
                    Dictionary<ItemCategory, float> a;
                    if (t.Settlement.StringId != null && _avg.TryGetValue(t.Settlement.StringId, out a)) foreach (var kv in a) td.Prod[kv.Key] = kv.Value;
                    var r = t.Owner != null ? t.Owner.ItemRoster : null;
                    if (r != null)
                        for (int i = 0; i < r.Count; i++)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (it == null || it.ItemCategory == null || el.Amount <= 0 || !ins.Contains(it.ItemCategory)) continue;
                            float x; td.Shelf.TryGetValue(it.ItemCategory, out x); td.Shelf[it.ItemCategory] = x + el.Amount;
                        }
                    if (t.Workshops != null)
                        foreach (var w in t.Workshops)
                        {
                            if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                            var wt = w.WorkshopType;
                            int n; td.Types.TryGetValue(wt, out n); td.Types[wt] = n + 1;
                            foreach (var c in Uses(wt)) { int u; td.Users.TryGetValue(c, out u); td.Users[c] = u + 1; }
                            float m;
                            if (MeasuredOf(w, out m))
                            {
                                double[] s;
                                if (!td.Meas.TryGetValue(wt, out s)) { s = new double[2]; td.Meas[wt] = s; }
                                s[0] += m; s[1] += 1;
                            }
                        }
                }
                catch (Exception e) { Stumble("Data(miasto)", e); }
                d[t] = td;
            }
            foreach (var td in d.Values)
            {
                td.Region = new List<TD>();
                try { foreach (var t2 in RegionOf(td.T)) { TD o; if (d.TryGetValue(t2, out o)) td.Region.Add(o); } }
                catch (Exception e) { Stumble("Data(region)", e); if (td.Region.Count == 0) td.Region.Add(td); }
            }
            _data = d; _dataDay = today; _lines.Clear();
            return d;
        }

        private static LineEval[] LinesOf(Town town, WorkshopType t)
        {
            Dictionary<WorkshopType, LineEval[]> m;
            if (!_lines.TryGetValue(town, out m)) { m = new Dictionary<WorkshopType, LineEval[]>(); _lines[town] = m; }
            LineEval[] r;
            if (m.TryGetValue(t, out r)) return r;
            var ps = t.Productions;
            r = new LineEval[ps != null ? ps.Count : 0];
            for (int i = 0; i < r.Length; i++)
            {
                float net, inC;
                bool ok = false;
                try { ok = WorkshopTrade.LineCycleAt(town, t, ps[i], out net, out inC); }
                catch (Exception e) { Stumble("LineCycleAt", e); net = 0f; inC = 0f; }
                r[i] = new LineEval { Runs = ok, Net = net, In = inC };
            }
            m[t] = r;
            return r;
        }

        // ------------------------------------------------------------ ocena
        /// <summary>
        /// Budzet wsadu na warsztat na dobe: wsad NAPRAWDE dostepny w regionie / (warsztaty regionu z tym wsadem + nowy); warsztat przezbrajany przestaje brac swoj
        /// dawny wsad. Dostepny = produkcja x f + polka / 30 dob, f = min(1, polka / (7 dob produkcji)): produkcja liczy sie w calosci, gdy polki regionu trzymaja
        /// >= 7 dob tej produkcji (towar zostaje na rynku), a przy pustych polkach (inni odbiorcy - lud, wojsko, karawany, warsztaty - biora wszystko) tylko polka.
        /// </summary>
        private static float Budget(TD td, ItemCategory c, HashSet<ItemCategory> selfUses)
        {
            double prod = 0, shelf = 0; int n = 0;
            foreach (var r in td.Region)
            {
                float x;
                if (r.Prod.TryGetValue(c, out x)) prod += x;
                if (r.Shelf.TryGetValue(c, out x)) shelf += x;
                int u; if (r.Users.TryGetValue(c, out u)) n += u;
            }
            if (selfUses != null && selfUses.Contains(c)) n--;
            double f = prod > 0 ? Math.Min(1.0, shelf / (KeepDays * prod)) : 0.0;
            return (float)((prod * f + shelf / ShelfDays) / Math.Max(1, n + 1));
        }

        /// <summary>Szacunek wyniku doby typu przed utrzymaniem: suma linii (od najlepszego cyklu) cykle x wynik cyklu; NoScore - zadna linia nie przechodzi progu gry.</summary>
        private static float Estimate(TD td, WorkshopType t, Workshop self, Quote q)
        {
            var lines = LinesOf(td.T, t);
            var ps = t.Productions;
            var order = new List<int>();
            bool runs = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Runs) continue;
                runs = true;
                if (lines[i].Net > 0f) order.Add(i);
            }
            if (!runs) return NoScore;
            order.Sort((a, b) => lines[b].Net.CompareTo(lines[a].Net));
            HashSet<ItemCategory> selfUses = self != null && self.WorkshopType != null ? Uses(self.WorkshopType) : null;
            var budget = new Dictionary<ItemCategory, float>();
            float total = 0f, cyc = 0f;
            foreach (int i in order)
            {
                var p = ps[i];
                float c = Math.Max(0f, p.ConversionSpeed);
                ItemCategory lim = null; float limB = -1f;
                foreach (var inp in p.Inputs)
                {
                    if (inp.Item1 == null || inp.Item2 <= 0) continue;
                    float b;
                    if (!budget.TryGetValue(inp.Item1, out b)) { b = Budget(td, inp.Item1, selfUses); budget[inp.Item1] = b; }
                    float lc = b / inp.Item2;
                    if (lc < c) { c = lc; lim = inp.Item1; limB = b; }
                }
                if (self != null && self.Capital < lines[i].In) c = 0f;   // gra: cykl tylko przy kapitale >= cena wsadu (CanNotableWorkshopProduceThisCycle)
                if (q.Limit == null && lim != null) { q.Limit = lim; q.LimitPer = limB; }
                if (!(c > 0f)) continue;
                foreach (var inp in p.Inputs)
                    if (inp.Item1 != null && inp.Item2 > 0) { float b; budget.TryGetValue(inp.Item1, out b); budget[inp.Item1] = Math.Max(0f, b - c * inp.Item2); }
                total += c * lines[i].Net; cyc += c;
            }
            q.Cycles = cyc;
            return total;
        }

        /// <summary>Wynik typu `t` w miescie `town` przed utrzymaniem (d/d): szacunek (cena sprawiedliwa x cykle z wsadu regionu), a gdy typ stoi w regionie z >= 60 dobami
        /// rachunku - najwyzej ich zmierzony wynik. `self` - warsztat, ktory sie przezbraja (nie liczy sie do pomiaru typu ani do odbiorcow swojego dawnego wsadu).</summary>
        internal static Quote Score(Town town, WorkshopType t, Workshop self)
        {
            var q = new Quote();
            if (town == null || t == null) return q;
            long t0 = Tin();
            try
            {
                TD td;
                if (!Data().TryGetValue(town, out td)) return q;
                double ms = 0; int mn = 0;
                foreach (var r in td.Region) { double[] s; if (r.Meas.TryGetValue(t, out s)) { ms += s[0]; mn += (int)s[1]; } }
                float own;
                if (self != null && self.WorkshopType == t && MeasuredOf(self, out own)) { ms -= own; mn--; }
                q.Estimate = Estimate(td, t, self, q);
                if (mn > 0) { q.MeasN = mn; q.Measured = (float)(ms / mn); q.Score = Math.Min(q.Measured, q.Estimate); _dMeas++; }
                else { q.Score = q.Estimate; if (q.Estimate > NoScore) _dEst++; else _dNone++; }
            }
            catch (Exception e) { Stumble("Score", e); q.Score = NoScore; }
            finally { Tout(t0); }
            return q;
        }

        /// <summary>Wynik obecnego typu warsztatu przezbrajanego: jego wlasny zmierzony wynik (srednia roku, >= 60 dob) + utrzymanie; bez rachunku - NoScore.</summary>
        internal static float OwnScore(Workshop w)
        {
            float m;
            long t0 = Tin();
            try { return MeasuredOf(w, out m) ? m : NoScore; }
            finally { Tout(t0); }
        }

        /// <summary>
        /// E3-D3 z F4: typ o najlepszym wyniku (Score) w miescie sposrod typow z wsadem w okregu (bramka HasInput jak dotad), bez ukrytych, kopalni i `exclude`;
        /// wynik dodatni x 0.8 za kazdy warsztat tego typu w REGIONIE rynku (bez `self`). null - zaden typ z wynikiem > 0. raw - wynik przed kara (spodziewany wynik doby).
        /// </summary>
        internal static WorkshopType Best(Town town, WorkshopType exclude, IDictionary<ItemCategory, float> dict, Workshop self, out float score, out Quote quote)
        {
            long t0 = Tin();
            try { return BestCore(town, exclude, dict, self, out score, out quote); }
            finally { Tout(t0); }
        }

        private static WorkshopType BestCore(Town town, WorkshopType exclude, IDictionary<ItemCategory, float> dict, Workshop self, out float score, out Quote quote)
        {
            score = NoScore; quote = null;
            WorkshopType pick = null;
            if (town == null) return null;
            TD td;
            if (!Data().TryGetValue(town, out td)) return null;
            foreach (var t in WorkshopType.All)
            {
                if (t == null || t.IsHidden || t == exclude || t.StringId == "mines") continue;
                try
                {
                    if (!WorkshopInputs.NeedsInput(t) || !WorkshopInputs.HasInput(t, dict)) continue;
                    var q = Score(town, t, self);
                    float sc = q.Score;
                    if (!(sc > 0f)) continue;
                    int dup = 0;
                    foreach (var r in td.Region) { int n; if (r.Types.TryGetValue(t, out n)) dup += n; }
                    if (self != null && self.WorkshopType == t) dup--;
                    if (dup > 0) sc *= (float)Math.Pow(DupKeep, dup);
                    if (pick == null || sc > score) { pick = t; score = sc; quote = q; }
                }
                catch (Exception e) { Stumble("Best(typ)", e); }
            }
            return pick;
        }

        // ------------------------------------------------------------ log
        private static string F1(float v) { return v <= NoScore * 0.1f || float.IsNaN(v) ? "-" : v.ToString("0.0", Inv); }

        /// <summary>Opis oceny do probki zmiany w linii E3-D3 (bez srednikow i nawiasow kwadratowych - checker dzieli probki po "; ").</summary>
        internal static string QuoteText(Quote q, float raw)
        {
            if (q == null) return "ocena F4 -";
            var sb = new StringBuilder(160);
            sb.Append("ocena F4: ").Append(q.MeasN > 0 ? "zmierzony " + F1(q.Measured) + " (" + q.MeasN + " warszt.) / " : "").Append("szacunek ").Append(F1(q.Estimate))
              .Append(" (").Append(q.Cycles.ToString("0.00", Inv)).Append(" cyk/d");
            if (q.Limit != null) sb.Append(", wsad ").Append(q.Limit.StringId).Append(' ').Append(q.LimitPer.ToString("0.00", Inv)).Append("/d na warsztat");
            sb.Append("), przed kara ").Append(F1(raw));
            return sb.ToString();
        }

        /// <summary>Po udanym przezbrojeniu (E3-D3 krok b): spodziewany wynik do porownania z rzeczywistym po 30 dobach (pamiec sesji).</summary>
        internal static void NoteRetool(Workshop w, WorkshopType to, float expected, int day)
        {
            try { if (w != null && to != null) _tracks.Add(new Track { W = w, To = to, Day = day, Exp = expected }); } catch { }
            Invalidate();
        }

        /// <summary>Linia startu E3-D3: stan klucza i pomiaru z zapisu.</summary>
        internal static string StartText()
        {
            if (!On) return "ocena F4 (Workshop Retool Fair Score) nie - ocena z mediany cen kategorii jak dotad";
            ResolvePending();
            return "ocena F4 (Workshop Retool Fair Score) tak: cena sprawiedliwa cyklu, cykle z wsadu regionu (promien " + RegionRange.ToString("0", Inv) + "), zmierzony wynik >= "
                   + MeasuredDays + " dob, kara x" + DupKeep.ToString("0.0", Inv) + " w regionie; pomiar wsadu z zapisu "
                   + (_loadedDays >= 0 ? _loadedDays + " dob, miast " + _loadedTowns : "brak (pierwsze " + WarmDays + " dob tylko pomiar)");
        }

        /// <summary>Czesc linii dnia E3-D3 (klucz wlaczony): regula, pomiar, oceny doby, spodziewany vs rzeczywisty, typy swiata, czas. `stepMs` - caly krok E3-D3 dzis
        /// (z akcja gry przezbrojenia) - tylko do informacji; "czas F4" = sama regula (DayFold, oceny i wybory od ostatniej linii, ta czesc linii).</summary>
        internal static string DayText(int today, double stepMs)
        {
            var sb = new StringBuilder(900);
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                int towns = 0; double units = 0;
                foreach (var kv in _avg) { if (kv.Value.Count == 0) continue; towns++; foreach (var v in kv.Value.Values) units += v; }
                sb.Append(" | ocena F4 (Workshop Retool Fair Score): cena sprawiedliwa cyklu, cykle z wsadu regionu rynku (promien ").Append(RegionRange.ToString("0", Inv))
                  .Append(", produkcja wsi i miast srednio ").Append(Span).Append(" dob x min(1, polka / ").Append(KeepDays).Append(" dob produkcji) + polka / ").Append(ShelfDays)
                  .Append(" dob, dzielone miedzy warsztaty regionu z tym wsadem), zmierzony wynik >= ")
                  .Append(MeasuredDays).Append(" dob jako sufit, kara x").Append(DupKeep.ToString("0.0", Inv)).Append(" za warsztat tego typu w regionie")
                  .Append(" | pomiar wsadu: dob ").Append(_days).Append(" (gotowy ").Append(_days >= WarmDays ? "tak" : "nie").Append(", od ").Append(WarmDays).Append(" dob), miast ").Append(towns)
                  .Append(", sztuk na dobe ").Append(units.ToString("0", Inv)).Append(", dzis zdarzen ").Append(_dEvents);
                long dTf, dWl, dSc; lock (Gate) { dTf = _dDirect[SrcTownFields]; dWl = _dDirect[SrcWoodlot]; dSc = _dDirect[SrcScrap]; }
                sb.Append(", bez zdarzenia gry (Armoury) pola mieszczan KS ").Append(dTf).Append(", las wsi ").Append(dWl).Append(", zlom i sztaby -> zelazo ").Append(dSc)
                  .Append(_loadedDays >= 0 ? ", z zapisu " + _loadedDays + " dob" : ", z zapisu brak").Append(_saveCut > 0 ? ", zapis uciety " + _saveCut + " razy" : "")
                  .Append(" | oceny dzis: zmierzone ").Append(_dMeas).Append(", szacunek ").Append(_dEst).Append(", bez linii ").Append(_dNone).Append(", odlozone (pomiar w toku) ").Append(_dWarm);
                int n10, n10f; NoTownMines.TakeF4(out n10, out n10f);
                sb.Append(" | kopalnie BK w miastach (N10): wyborow z ocena F4 ").Append(n10).Append(", bez typu z wynikiem F4 -> ocena z cen ").Append(n10f);
                // spodziewany vs rzeczywisty: przezbrojone w tej sesji, po >= 30 dobach, typ bez zmian
                int k = 0, work = 0, gone = 0; double se = 0, sa = 0;
                foreach (var tr in _tracks)
                {
                    try
                    {
                        if (tr.W == null || tr.W.WorkshopType != tr.To) { gone++; continue; }
                        if (today - tr.Day < ActualAfterDays) continue;
                        float avg, slow; int days;
                        if (!WorkshopTrade.RecOf(tr.W, out avg, out slow, out days)) continue;
                        float act = avg + WorkshopTrade.UpkeepOf(tr.W);
                        k++; se += tr.Exp; sa += act; if (act > 0f) work++;
                    }
                    catch (Exception e) { Stumble("DayText(sledzenie)", e); }
                }
                sb.Append(" | spodziewany vs rzeczywisty: przezbrojone w sesji ").Append(_tracks.Count).Append(", po >= ").Append(ActualAfterDays).Append(" dobach ").Append(k)
                  .Append(": spodziewany srednio ").Append(k > 0 ? (se / k).ToString("0.0", Inv) : "-").Append(" d/d, rzeczywisty srednio ").Append(k > 0 ? (sa / k).ToString("0.0", Inv) : "-")
                  .Append(" d/d (srednia miesiaca + utrzymanie), pracuje ").Append(work).Append(" z ").Append(k).Append(", typ juz inny ").Append(gone);
                // typy warsztatow swiata (browary, garbarnie, garncarze ... - czy nie znikaja)
                var cnt = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Workshops == null) continue;
                    foreach (var w in t.Workshops)
                    {
                        if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden) continue;
                        string id = w.WorkshopType.StringId ?? "?";
                        int n; cnt.TryGetValue(id, out n); cnt[id] = n + 1;
                    }
                }
                var parts = new List<string>();
                foreach (var kv in cnt) parts.Add(kv.Key + " " + kv.Value);
                double ms = (Interlocked.Read(ref _dTicks) + (Stopwatch.GetTimestamp() - t0)) * 1000.0 / Stopwatch.Frequency;
                sb.Append(" | typy (swiat): ").Append(parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "-")
                  .Append(" | czas F4 ").Append(ms.ToString("0.00", Inv)).Append(" ms (pomiar, oceny i wybory od ostatniej linii, ta linia; caly krok E3-D3 z akcja gry ")
                  .Append(stepMs.ToString("0.00", Inv)).Append(" ms) | obieg bez zmian: zmienia sie tylko wybor typu (sprzet notable -> kasa miasta jak dotad)")
                  .Append(" | potkniecia F4 ").Append(_dStumbles).Append(" (sesja ").Append(_sessStumbles).Append(')');
            }
            catch (Exception e) { Stumble("DayText", e); }
            return sb.ToString();
        }
    }
}
