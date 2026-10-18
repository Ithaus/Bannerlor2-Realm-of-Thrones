using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// inwest (noc 17/18.10) - ZAJECIA WIOSEK POBOCZNYCH (ZW; projekt docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md czesc A z dopiskiem Jeffa 16.10 o wolnej zmianie zajecia).
    /// Kazda wioska poboczna okregu ma wlasne zajecie (klimat, teren, woda - w sniegu bez oliwek, winorosli i zboza), okreg robi sume wszystkich wiosek, ikona wsi gry
    /// pokazuje zajecie, ktore daje okregowi najwiecej pieniedzy (DistrictIcon.cs). Dwie drogi, jedna regula:
    ///  - POD KS (okreg w stanie zastosowanym KS): zajecie wioski = blok KS tej wioski (StartCalibration.Inwest.cs - rece Planu z jednej tabeli swiata, kandydaci
    ///    z ziemi bloku, snieg bez upraw); zmiana zajecia tylko regula A7 KS (hamulec Jeffa w StartCalibration.Rule.cs). Ta klasa tylko czyta.
    ///  - BEZ KS (droga zapasowa - gra Jeffa, zanim KS sie wlaczy, i krainy wrocone bramka): zajecie startowe z pliku ziemi KS (arm_ks_land.tsv + nakladka przysiolkow v6;
    ///    generator tools/ks/ks_ziemia.py z symulacji upraw - regula A3 projektu: obrazek rybacki - rybacy, mlyn/spichlerz - zboze, typ okregu, najlepszy kandydat, zapas
    ///    strefy), kandydaci z tej samej tabeli swiata co KS (KsTab.Land2 >= 0.3). Wies gry zawsze typ okregu (A4). Zajecie musi miec typ wsi w grze (VillageType)
    ///    i typ musi gdzies byc typem okregu (inaczej neutralny start nie ma czego przeliczyc) - inaczej typ okregu.
    ///    PRODUKCJA (uogolniony krok W2; MaterialLaw.ProdPostfix, PIERWSZY krok po KS, obok VillageFruit.Base - przed mnoznikami lancucha): towary typu okregu x
    ///    ((1 - u_inne) x n(typ) + premia prac), gdzie u_inne = udzial ludzi W DOMU wiosek z innym zajeciem (ten sam podzial co ksiega wiosek - plon idzie za ludzmi
    ///    w domu); zboze kuchenne typu niezbozowego zostaje. Towary innych zajec - baza: suma(udzial wioski x stawka typu zajecia x (poziom + 1) x 0.5 x g(zajecie)/g(typ)
    ///    x n(zajecie) x (1 + premia prac)) - dalej te same mnozniki lancucha co we wsi tego typu (kopalnie i las, ludzie P1 - z g(typ) daje g(zajecie), rece E4/5 i E4/7,
    ///    regulator TEGO towaru, zima). Lista BK (MineralOnce, ostatni krok): towary innych zajec z waga 0.
    ///    Magazyn wsi: + 5 dob dopisanych towarow (VillageFruit.CapPostfix). Krok W2 (HamletKinds) wylaczony przy tym kluczu - rybacy to zajecie rybackie (pelne rece).
    ///    NEUTRALNY START (A6): w pierwszej dobie klucza n(T) = rece swiata typu T przed / po (rece = ludzie w domu okregu x udzial czesci) - swiat ma tyle samo
    ///    kazdego typu, co dzien wczesniej; w zapisie (arm_crafts). Potem zmienia go tylko zysk.
    ///    WOLNA ZMIANA (dopisek Jeffa 16.10): co 7 dob od wlaczenia wynik zajecia = przydatnosc ziemi x sqrt(indeks cen towarow zajecia w miescie targowym, wagi stawka x wartosc);
    ///    lepsze zajecie >= x Switch przez Hold Days bez przerwy, najwyzej jedna wioska okregu w przebudowie, najwyzej Region Share rak wiosek regionu (warownia) na sezon
    ///    (91 dob), koszt przebudowy 28 dob kosztu K nowego zajecia (tabela KS) za rece wioski - co dobe z kiesy wsi do kasy miasta targowego (rzemieslnicy robia sprzet;
    ///    najwyzej 1/4 kiesy na dobe, doba bez zaplaty = przebudowa stoi), okres przejsciowy Change Days (sady, winnice, gaje, daktyle - Tree Days): stary towar gasnie,
    ///    nowy rosnie. Wioska z pracami okregu nie zmienia zajecia.
    /// Obieg: zloto tylko w przebudowie (kiesa wsi -> kasa miasta targowego, kontrola w linii). Towar z rak - zadnej sztuki z niczego (rece zdjete = rece dodane).
    /// Klucz Side Village Crafts wylaczony: lista, model, magazyn i W2 jak dotad; stan w zapisie zostaje (zamrozony).
    /// Wyjatki per wies liczone (potkniecia) - bez globalnych wylacznikow.
    /// </summary>
    internal static class VillageCrafts
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "Zajecia wiosek (ZW): ";
        private const string SaveVer = "zw1";
        private const int Season = 91, StepDays = 7, CostDays = 28;
        private const double NMin = 0.5, NMax = 2.0;                     // widelki n(typ) neutralnego startu (straznik)
        private static readonly object Gate = new object();

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.SideVillageCrafts; } }

        /// <summary>Droga bez KS czynna: klucz, ksiega wiosek, lista BK i MaterialLaw w modelach produkcji (bez nich towar zdjety nie mialby gdzie wrocic).</summary>
        internal static bool On { get { return KeyOn && VillageBook.Active && MineralOnce.Wired && MaterialLaw.ProdModels > 0; } }

        // ============================================================ zajecia: typy gry, nazwy, towary (raz na kampanie)
        internal sealed class CraftMeta
        {
            public int C; public string Id; public VillageType Type; public string Name; public ItemObject[] Goods; public float[] Rates; public ItemObject Icon; public int Kind = -1;
            public bool Tree; public bool Horse; public string GoodsText;
        }
        private static CraftMeta[] _meta;
        private static Campaign _metaFor;

        /// <summary>Opis zajecia (KsTab indeks): typ wsi gry, towary bez zboza kuchennego (typ niezbozowy), ikona (glowny towar typu), rodzaj prac okregu.</summary>
        internal static CraftMeta Meta(int c)
        {
            var m = MetaAll();
            return m != null && c >= 0 && c < m.Length ? m[c] : null;
        }

        private static CraftMeta[] MetaAll()
        {
            var cc = Campaign.Current;
            if (_meta != null && ReferenceEquals(_metaFor, cc)) return _meta;
            var arr = new CraftMeta[KsTab.NC];
            Dictionary<string, VillageType> types = new Dictionary<string, VillageType>(StringComparer.Ordinal);
            try { foreach (var t in VillageType.All) if (t != null && t.StringId != null && !types.ContainsKey(t.StringId)) types[t.StringId] = t; } catch { }
            for (int c = 0; c < KsTab.NC; c++)
            {
                string id = KsTab.Crafts[c];
                var m = new CraftMeta { C = c, Id = id, Name = NameOf(id), Tree = c == KsTab.CVine || c == KsTab.COlive || c == KsTab.CDate || c == KsTab.CCitrus, Horse = c >= KsTab.CHorse0 && c <= KsTab.CHorse5 };
                string tid = id == "cattle_farm:maslo" ? "cattle_farm" : id;
                VillageType vt; if (types.TryGetValue(tid, out vt)) m.Type = vt;
                m.Kind = DistrictWorks.KindOfCraft(id);
                var goods = new List<ItemObject>(); var rates = new List<float>();
                if (m.Type != null)
                {
                    try
                    {
                        foreach (var p in m.Type.Productions)
                        {
                            if (p.Item1 == null || !(p.Item2 > 0f)) continue;
                            if (tid != "wheat_farm" && p.Item1.StringId == "grain") continue;   // zboze kuchenne typu niezbozowego (projekt F3: G(T) bez niego)
                            int at = goods.IndexOf(p.Item1);
                            if (at >= 0) rates[at] += p.Item2; else { goods.Add(p.Item1); rates.Add(p.Item2); }
                        }
                        m.Icon = m.Type.PrimaryProduction;
                    }
                    catch { }
                }
                m.Goods = goods.ToArray(); m.Rates = rates.ToArray();
                var names = new List<string>();
                foreach (var g in m.Goods) { try { string n = g.Name != null ? g.Name.ToString() : g.StringId; if (!names.Contains(n)) names.Add(n); } catch { } }
                m.GoodsText = names.Count > 0 ? string.Join(", ", names.ToArray()).ToLowerInvariant() : m.Name;
                arr[c] = m;
            }
            _meta = arr; _metaFor = cc;
            return arr;
        }

        /// <summary>Nazwa zajecia dla gracza (po angielsku, jak w opisie projektu: "cattle 85%, grain 8%, fish 7%").</summary>
        internal static string NameOf(string id)
        {
            switch (id)
            {
                case "wheat_farm": return "grain";
                case "fisherman": return "fish";
                case "whaler": return "whales";
                case "walrus_hunter": return "walrus";
                case "vineyard": return "vines";
                case "olive_trees": return "olives";
                case "citrus": return "oranges";
                case "garden": return "gardens";
                case "spice_garden": return "spices";
                case "quarry": return "marble";
                case "lime_pit": return "limestone";
                case "date_farm": return "dates";
                case "flax_plant": return "flax";
                case "silk_plant": return "silk and cotton";
                case "sheep_farm": return "sheep";
                case "cattle_farm": case "cattle_farm:maslo": return "cattle";
                case "swine_farm": return "swine";
                case "lumberjack": return "timber";
                case "trapper": return "furs";
                case "clay_mine": return "clay";
                case "salt_mine": case "sea_salt": return "salt";
                case "iron_mine": return "iron";
                case "silver_mine": return "silver";
                case "gold_mine": return "gold";
                case "hunter": return "game";
                default: return id != null && id.EndsWith("horse_ranch", StringComparison.Ordinal) ? "horses" : (id ?? "?");
            }
        }

        /// <summary>Indeks zajecia (KsTab) typu wsi gry; -1 - typ spoza tabeli.</summary>
        internal static int CraftOfType(VillageType t)
        {
            if (t == null || t.StringId == null) return -1;
            int c; return KsTab.CraftIndex.TryGetValue(t.StringId, out c) ? c : -1;
        }

        // ============================================================ stan kampanii (zapis arm_crafts)
        private sealed class Side
        {
            public string Uid, Dist; public int Craft = -1, Start = -1, Target = -1, TStart = -1, TEnd = -1, Held; public float F, Cost, Paid;
            // pamiec sesji
            public StartCalibration.LandRow Row; public List<int> Cand;
        }
        private static readonly Dictionary<string, Side> _side = new Dictionary<string, Side>(StringComparer.Ordinal);
        private static double[] _n;                 // n(T) neutralnego startu (KsTab.NC); null - klucz jeszcze nie wlaczony w tej kampanii
        private static int _onDay = -1;             // doba wlaczenia (neutralny start)
        private static readonly Dictionary<string, double> _regionMoved = new Dictionary<string, double>(StringComparer.Ordinal);
        private static int _regionSeason = -1;
        private static string _pending;             // zapis do rozwiazania przy pierwszej dobie sesji
        private static string _loadNote = "nowa kampania albo zapis bez arm_crafts";

        // pamiec sesji
        private sealed class DistInfo
        {
            public Village V; public string Id, Region; public VillageType Type; public int TypeCraft; public readonly List<Side> Sides = new List<Side>();
            public Plan Plan; public bool HasPlan, Dirty = true; public int Slot = -1;   // plan produkcji z ostatniego przeliczenia (co 7 dob na zmiane; od razu przy zmianie stanu)
        }
        private static readonly HashSet<string> _dirty = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Okreg do przeliczenia planu nastepnej doby (prace okregu: zakup, ogien) - bez czekania na kolejke 7 dob. Nie rzuca.</summary>
        internal static void MarkDirty(Village v)
        {
            try { if (v != null && v.Settlement != null && v.Settlement.StringId != null) lock (Gate) _dirty.Add(v.Settlement.StringId); } catch { }
        }
        private static readonly Dictionary<string, DistInfo> _dist = new Dictionary<string, DistInfo>(StringComparer.Ordinal);
        private static bool _built; private static int _builtSide, _builtNoRow, _builtStartFix;

        // plan produkcji wsi (budowany na watku glownym co dobe, czytany z modelu produkcji): jedna referencja podmieniana
        private sealed class Plan
        {
            public float Other, NType, TypeBonus; public bool KitchenKeep; public VillageType Type;
            public Dictionary<ItemObject, float> Add;          // towar -> M x ten mnoznik = dopisek (suma po zajeciach innych wiosek)
            public HashSet<ItemObject> TypeGoods;
            public Dictionary<ItemObject, float> TypeRate;     // stawka typu na towar (suma wag)
        }
        private static volatile Dictionary<Village, Plan> _plans = new Dictionary<Village, Plan>();

        // pamiec doby modelu: M wsi (wynik towaru typu / stawka typu) - ostatnie wywolanie towaru typu tej doby
        private sealed class Mem { public int Day = int.MinValue; public float M; public bool NoMem; public readonly Dictionary<ItemObject, float[]> X = new Dictionary<ItemObject, float[]>(); }   // X: towar -> [zdjete, dodane, premia] (wartosc bazowa, ostatnie wywolanie doby)
        private static readonly Dictionary<Village, Mem> _mem = new Dictionary<Village, Mem>(new RefEq());
        private sealed class RefEq : IEqualityComparer<Village>
        {
            public bool Equals(Village a, Village b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Village o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        // liczniki do linii (od ostatniej linii)
        private static int _st, _stAll, _dLists, _dAdded, _dNoMem, _dSteps, _dStarted, _dDone, _dStall, _dBusy, _dBudget, _dLocked, _dPress, _dCancel, _dRedone;
        private static long _dPaid, _mVil, _mTown;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<string> _notes = new List<string>();
        private static bool _offLogged;

        internal static void Reset()
        {
            lock (Gate)
            {
                _side.Clear(); _n = null; _onDay = -1; _regionMoved.Clear(); _regionSeason = -1; _pending = null; _loadNote = "nowa kampania albo zapis bez arm_crafts";
                _dist.Clear(); _built = false; _builtSide = _builtNoRow = _builtStartFix = 0;
                _plans = new Dictionary<Village, Plan>(); _mem.Clear(); _dirty.Clear();
                _st = _stAll = 0; ZeroDay(); _err.Clear(); _notes.Clear(); _offLogged = false;
                _meta = null; _metaFor = null;
            }
        }

        private static void ZeroDay() { _dLists = _dAdded = _dNoMem = _dSteps = _dStarted = _dDone = _dStall = _dBusy = _dBudget = _dLocked = _dPress = _dCancel = _dRedone = 0; _dPaid = 0; }

        private static void Stumble(string where, Exception e)
        {
            bool first;
            lock (Gate) { _st++; _stAll++; first = _err.Add(where); }
            if (first) Log.Error("VillageCrafts." + where, e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        // ============================================================ budowa okregow (sesja)
        /// <summary>Okregi wsi gry z ksiegi wiosek i wiersze ziemi wiosek pobocznych (plik ziemi KS po nakladce v6). Raz na sesje (pierwsza doba albo start sesji).</summary>
        private static void Build()
        {
            if (_built) return;
            _built = true;
            _dist.Clear(); _builtSide = _builtNoRow = _builtStartFix = 0;
            var meta = MetaAll();
            var parts = new List<VillageBook.PartView>();
            var rows = new List<StartCalibration.LandRow>();
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null || v.VillageType == null) continue;
                    if (!VillageBook.PartsOf(v, parts) || parts.Count < 1) continue;   // takze okregi bez wiosek - n(typ) neutralnego startu dotyczy wszystkich wsi typu
                    var di = new DistInfo { V = v, Id = v.Settlement.StringId, Type = v.VillageType, TypeCraft = CraftOfType(v.VillageType) };
                    di.Region = v.Bound != null ? v.Bound.StringId : di.Id;
                    StartCalibration.SideRows(di.Id, rows);
                    var byUid = new Dictionary<string, StartCalibration.LandRow>(StringComparer.Ordinal);
                    foreach (var r in rows) if (r != null && r.Uid != null) byUid[r.Uid] = r;
                    foreach (var p in parts)
                    {
                        if (p.Main || p.Uid == null) continue;
                        StartCalibration.LandRow row; byUid.TryGetValue(p.Uid, out row);
                        Side s;
                        if (!_side.TryGetValue(p.Uid, out s)) { s = new Side { Uid = p.Uid, Dist = di.Id }; _side[p.Uid] = s; }
                        s.Dist = di.Id; s.Row = row;
                        string dtype = v.VillageType.StringId;
                        int start = -1;
                        if (row != null && row.Start != null) KsTab.CraftIndex.TryGetValue(row.Start, out start);
                        if (!Valid(start, meta)) { if (row != null) _builtStartFix++; start = di.TypeCraft; }
                        s.Start = start;
                        if (s.Craft < 0 || !Valid(s.Craft, meta)) s.Craft = start;
                        var cand = row != null ? StartCalibration.CandidatesOf(row, dtype) : new List<int>();
                        cand.RemoveAll(c => !Valid(c, meta));
                        if (!cand.Contains(s.Craft) && Valid(s.Craft, meta)) cand.Add(s.Craft);
                        s.Cand = cand;
                        if (row == null) _builtNoRow++;
                        di.Sides.Add(s);
                        _builtSide++;
                    }
                    _dist[di.Id] = di;
                }
                catch (Exception e) { Stumble("Build(okreg)", e); }
            }
        }

        /// <summary>Zajecie dozwolone drogi bez KS: typ wsi gry istnieje, nie konie (projekt B1: tabory wsi nie sprzedaja koni) i typ jest gdzies typem okregu.</summary>
        private static bool Valid(int c, CraftMeta[] meta)
        {
            if (c < 0 || c >= KsTab.NC || meta == null || meta[c] == null || meta[c].Type == null || meta[c].Goods.Length == 0 || meta[c].Horse) return false;
            return _typeUsed != null && c < _typeUsed.Length && _typeUsed[c];
        }
        private static bool[] _typeUsed;

        private static void TypesUsed()
        {
            var u = new bool[KsTab.NC];
            foreach (var v in Village.All) { int c = v != null ? CraftOfType(v.VillageType) : -1; if (c >= 0) u[c] = true; }
            _typeUsed = u;
        }

        // ============================================================ udzialy czesci (ksiega wiosek - ludzie w domu)
        /// <summary>Udzial zajecia czesci: (zajecie, udzial) - w przebudowie stare (1 - F) i nowe F.</summary>
        private static void CraftShares(Side s, Action<int, double> f)
        {
            if (s.Target >= 0 && s.Target != s.Craft && s.F > 0f) { f(s.Craft, 1.0 - Math.Min(1f, s.F)); f(s.Target, Math.Min(1f, s.F)); }
            else f(s.Craft, 1.0);
        }

        private static bool UnderKs(Village v)
        {
            try { return StartCalibration.AppliedFor(v); } catch { return false; }
        }

        /// <summary>Plan produkcji wsi z udzialow ludzi w domu czesci i zajec (watek glowny, co dobe). null - okreg bez wiosek z innym zajeciem i bez neutralnego n.</summary>
        private static Plan BuildPlan(DistInfo di, List<VillageBook.PartView> parts)
        {
            var meta = MetaAll();
            if (di.TypeCraft < 0 || !VillageBook.PartsOf(di.V, parts) || parts.Count < 1) return null;
            double tot = 0; foreach (var p in parts) tot += p.Home;
            if (!(tot > 1e-6)) return null;
            int D = di.TypeCraft;
            float gD = VillageByPeople.GOf(di.Type);
            double other = 0, typeBonus = 0;
            var add = new Dictionary<ItemObject, float>();
            var bySide = new Dictionary<string, Side>(StringComparer.Ordinal);
            foreach (var s in di.Sides) bySide[s.Uid] = s;
            foreach (var p in parts)
            {
                double sh = p.Home / tot;
                if (!(sh > 0)) continue;
                double b = 0; int wc; double wb;
                if (DistrictWorks.BonusOf(di.V, p.Main ? null : p.Uid, out wc, out wb)) b = wb; else wc = -1;
                if (p.Main) { if (wc == D) typeBonus += sh * b; continue; }
                Side s; if (!bySide.TryGetValue(p.Uid, out s)) continue;
                double shr = sh;
                CraftShares(s, (c, x) =>
                {
                    double part = shr * x;
                    double bb = c == wc ? b : 0.0;
                    if (c == D) { typeBonus += part * bb; return; }
                    other += part;
                    var m = meta[c];
                    if (m == null || m.Type == null) return;
                    double k = part * (VillageByPeople.GOf(m.Type) / Math.Max(1e-3f, gD)) * N(c) * (1.0 + bb);
                    for (int i = 0; i < m.Goods.Length; i++)
                    {
                        float cur; add.TryGetValue(m.Goods[i], out cur);
                        // poprawka stawki typu (sol wsi solnych, futra trapperow - GoodsRates): lancuch patrzy na typ wsi gry, wioska z tym zajeciem dostaje ja w bazie
                        add[m.Goods[i]] = cur + (float)(k * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, m.Goods[i].StringId));
                    }
                });
            }
            double nD = N(D);
            if (other <= 1e-6 && Math.Abs(nD - 1.0) < 1e-4 && typeBonus <= 1e-9) return null;
            var pl = new Plan { Other = (float)Math.Min(1.0, other), NType = (float)nD, TypeBonus = (float)(typeBonus * nD), Type = di.Type, Add = add, KitchenKeep = di.Type.StringId != "wheat_farm",
                                TypeGoods = new HashSet<ItemObject>(), TypeRate = new Dictionary<ItemObject, float>() };
            foreach (var pr in di.Type.Productions)
            {
                if (pr.Item1 == null || !(pr.Item2 > 0f)) continue;
                pl.TypeGoods.Add(pr.Item1);
                float r; pl.TypeRate.TryGetValue(pr.Item1, out r); pl.TypeRate[pr.Item1] = r + pr.Item2;
            }
            return pl;
        }

        private static double N(int c) { var n = _n; return n != null && c >= 0 && c < n.Length && n[c] > 0 && !double.IsInfinity(n[c]) ? n[c] : 1.0; }

        // ============================================================ haki produkcji (moga isc z innych watkow - sam odczyt planu, pamiec pod Gate)
        /// <summary>MineralOnce.Postfix (ostatni krok listy, zamiast W2): towary zajec innych wiosek dopisane z waga 0 na koncu listy (tick: towary typu przed nimi).
        /// Wies bez planu albo pod KS - ta sama lista.</summary>
        internal static List<(ItemObject, float)> ListStep(Village v, List<(ItemObject, float)> list)
        {
            if (v == null || list == null || !On) return list;
            try
            {
                Plan pl;
                var plans = _plans;
                if (plans.Count == 0 || !plans.TryGetValue(v, out pl) || pl.Add == null || pl.Add.Count == 0 || UnderKs(v)) return list;
                List<(ItemObject, float)> res = null;
                foreach (var kv in pl.Add)
                {
                    if (!(kv.Value > 0f)) continue;
                    bool has = false;
                    foreach (var e in list) if (ReferenceEquals(e.Item1, kv.Key)) { has = true; break; }
                    if (has) continue;
                    if (res == null) res = new List<(ItemObject, float)>(list);
                    res.Add((kv.Key, 0f));
                }
                if (res != null) { lock (Gate) _dLists++; return res; }
                return list;
            }
            catch (Exception e) { Stumble("ListStep", e); return list; }
        }

        private static readonly TextObject _txtType = new TextObject("{=!}Armoury: side villages of the district work at their own crafts");
        private static readonly TextObject _txtAdd = new TextObject("{=!}Armoury: goods of the side villages' crafts");

        /// <summary>MaterialLaw.ProdPostfix, PIERWSZY krok po KS (obok VillageFruit.Base - przed mnoznikami lancucha: kopalnie i las, powtorzenia BK, ludzie P1, rece E4/5
        /// i E4/7, regulator, hodowla): towar typu okregu - czynnik (1 - u_inne) x n(typ) + premia prac (zboze kuchenne typu niezbozowego bez zmian); towar zajec innych
        /// wiosek - baza = suma(udzial x stawka typu zajecia x (poziom + 1) x 0.5 x g(zajecie)/g(typ) x n(zajecie) x (1 + premia)), a dalej te same mnozniki co towar
        /// wioski tego typu (ten sam wzor co owoce wsi BK). Towar w typie i w zajeciu - baza / czynnik (dopisek bez czynnika typu). Wies nie Normal albo bez targu - nic.</summary>
        internal static void Base(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (v == null || item == null || !KeyOn) return;
            try
            {
                Plan pl;
                var plans = _plans;
                if (plans.Count == 0 || !plans.TryGetValue(v, out pl) || !On || UnderKs(v)) return;
                if (v.VillageState != Village.VillageStates.Normal || v.TradeBound == null) return;
                int today = Today();
                float addK = 0f; bool hasAdd = pl.Add != null && pl.Add.TryGetValue(item, out addK) && addK > 0f; if (!hasAdd) addK = 0f;
                float lvl = (v.GetHearthLevel() + 1) * 0.5f;
                float A = addK * lvl;                                         // dopisek zajec w bazie gry (przed czynnikami)
                float rate;
                if (pl.TypeGoods.Contains(item) && pl.TypeRate.TryGetValue(item, out rate) && rate > 0f)
                {
                    float before = r.ResultNumber;
                    bool kitchen = pl.KitchenKeep && item.StringId == "grain";
                    float F = kitchen ? 1f : (1f - pl.Other) * pl.NType + pl.TypeBonus;
                    if (F < 0f) F = 0f;
                    float sum = 1f + r.SumOfFactors;
                    if (before > 0f && Math.Abs(F - 1f) > 1e-5f && sum > 0f) r.AddFactor((F - 1f) * sum, _txtType);
                    float added = 0f;
                    if (hasAdd && A > 0f)
                    {
                        float r0 = r.ResultNumber;
                        if (F > 1e-3f && before > 0f) r.Add(A / F, _txtAdd);        // baza x (1 + czynniki) x F: dopisek A bez czynnika typu F
                        else AddBase(ref r, A);
                        added = Math.Max(0f, r.ResultNumber - r0);
                    }
                    lock (Gate)
                    {
                        var m = MemFor(v, today);
                        Put(m, item, kitchen ? 0f : before * (1f - F + pl.TypeBonus) * item.Value, added * item.Value, kitchen ? 0f : before * pl.TypeBonus * item.Value);
                        _dSteps++;
                    }
                    return;
                }
                if (!hasAdd || !(A > 0f)) return;
                float r1 = r.ResultNumber;
                r.Add(A, _txtAdd);                                            // towar spoza typu: model gry dal baze 0 - te same czynniki, ktore juz sa, i dalsze kroki lancucha
                float got = Math.Max(0f, r.ResultNumber - r1);
                lock (Gate) { var m = MemFor(v, today); Put(m, item, 0f, got * item.Value, 0f); _dAdded++; }
            }
            catch (Exception e) { Stumble("Base", e); }
        }

        /// <summary>Dopisek bazy tak, by wynik wzrosl dokladnie o amount (jak ryby W2): baza + amount / (1 + suma czynnikow); czynniki zjadaja baze - zerowane.</summary>
        private static float AddBase(ref ExplainedNumber r, float amount)
        {
            if (!(amount > 0f) || float.IsInfinity(amount)) return 0f;
            float old = Math.Max(0f, r.ResultNumber), fsum = 1f + r.SumOfFactors;
            if (fsum > 0.001f) r.Add(amount / fsum, _txtAdd);
            else { r.AddFactor(-r.SumOfFactors, _txtAdd); r.Add(old + amount - r.BaseNumber, _txtAdd); }
            return Math.Max(0f, r.ResultNumber - old);
        }

        private static Mem MemFor(Village v, int day)
        {
            Mem m;
            if (!_mem.TryGetValue(v, out m)) { m = new Mem(); _mem[v] = m; }
            if (m.Day != day) { m.Day = day; m.M = 0f; m.NoMem = false; m.X.Clear(); }
            return m;
        }

        private static void Put(Mem m, ItemObject it, float rem, float add, float bon)
        {
            float[] a;
            if (!m.X.TryGetValue(it, out a)) { a = new float[3]; m.X[it] = a; }
            a[0] = rem; a[1] = add; a[2] = bon;
        }

        /// <summary>VillageFruit.CapPostfix (po KS): + 5 dob dopisanych towarow zajec (stawka x M ostatniej doby albo szacunek z czynnika ludzi P1).</summary>
        internal static int CapAdd(Village v)
        {
            if (v == null || !On) return 0;
            try
            {
                Plan pl; var plans = _plans;
                if (plans.Count == 0 || !plans.TryGetValue(v, out pl) || pl.Add == null || pl.Add.Count == 0 || UnderKs(v)) return 0;
                float M = MOf(v);
                double x = 0;
                foreach (var kv in pl.Add) if (!pl.TypeGoods.Contains(kv.Key)) x += M * kv.Value;
                return (int)Math.Ceiling(5.0 * x - 1e-4);
            }
            catch (Exception e) { Stumble("CapAdd", e); return 0; }
        }

        /// <summary>M wsi (wynik towaru typu / stawka typu): pamiec ostatniej doby, bez niej szacunek u_v x g(typ) z reguly P1 albo (poziom + 1) x 0.5 gry.</summary>
        internal static float MOf(Village v)
        {
            if (v == null) return 0f;
            lock (Gate) { Mem m; if (_mem.TryGetValue(v, out m) && m.M > 0f && Today() - m.Day <= 2) return m.M; }
            try
            {
                float f, u, g;
                float lvl = (v.GetHearthLevel() + 1) * 0.5f;
                float m = VillageByPeople.TryFactor(v, out f, out u, out g) ? lvl * f : lvl;
                return m * Now(v);
            }
            catch { return 1f; }
        }

        /// <summary>Czynnik dnia okregu jak w rachunku KS (ComputeDistrict) i w lancuchu wsi: zima x rece spustoszenia E4/5 x rece wojny E4/7. Nie rzuca.</summary>
        internal static float Now(Village v)
        {
            try
            {
                float f = (1f - WinterBite.VillageCut(v)) * Devastation.YieldFactor(v) * FreeHands.YieldFactor(v);
                return f >= 0f && !float.IsNaN(f) && !float.IsInfinity(f) ? f : 1f;
            }
            catch { return 1f; }
        }

        // ============================================================ widok czesci (KS i bez KS) - opis, ikona, prace okregu
        /// <summary>Czesc okregu z zajeciem: Craft (KsTab), przebudowa (Target, F), udzial rak/ludzi (Share), wartosc dnia zajecia czesci przy cenach miasta targowego
        /// (ValDay, sztuki x cena, biezaca pora), wartosc wedlug zajecia (CraftVal) i sztuki dnia wedlug towaru (Pcs).</summary>
        internal sealed class PartCraft
        {
            public string Uid, Name, Kind; public bool Main, Ks; public int Craft = -1, Target = -1; public float F; public double Share, ValDay; public VillageBook.PartView Book;
            public double[] CraftVal = new double[KsTab.NC]; public Dictionary<ItemObject, double> Pcs = new Dictionary<ItemObject, double>();
        }

        /// <summary>Czesci okregu wsi gry z zajeciami i wartoscia dnia (pod KS - bloki KS; bez KS - zajecia tej klasy i plon z M). false - ksiega nieczynna / wies nieznana.
        /// Nie rzuca. Koszt: odczyt ksiegi + wiersze czesci (wolane po kliknieciu, dymku i raz na 28 dob dla ikony).</summary>
        internal static bool PartsOf(Village v, List<PartCraft> into, out bool ks)
        {
            ks = false;
            if (into != null) into.Clear();
            if (v == null || into == null) return false;
            try
            {
                var book = new List<VillageBook.PartView>();
                if (!VillageBook.PartsOf(v, book)) return false;
                Town town = v.TradeBound != null ? v.TradeBound.Town : (v.Bound != null ? v.Bound.Town : null);
                var meta = MetaAll();
                double tot = 0; foreach (var p in book) tot += p.Home;
                var ksParts = new List<StartCalibration.KsPartView>();
                if (StartCalibration.KsPartsOf(v, ksParts))
                {
                    ks = true;
                    double sigma = Math.Max(1, DailyLife.KsSigma), hands = 0, fnow = Now(v);   // bloki KS bez zimy i czynnikow rak - wartosc dnia z nimi (jak stawki KS)
                    foreach (var kp in ksParts) hands += kp.Hands;
                    foreach (var p in book)
                    {
                        StartCalibration.KsPartView kp = null;
                        foreach (var x in ksParts) if (p.Main ? x.Main : (!x.Main && x.Uid == p.Uid)) { kp = x; break; }
                        var pc = new PartCraft { Uid = p.Uid, Name = p.Name, Kind = p.Kind, Main = p.Main, Ks = true, Book = p };
                        if (kp != null)
                        {
                            pc.Craft = kp.Craft; pc.Share = hands > 0 ? kp.Hands / hands : 0; pc.F = kp.Busy ? 0.5f : 0f;
                            var items = KsItems();
                            for (int i = 0; i < kp.Kg.Length && i < items.Length; i++)
                            {
                                if (!(kp.Kg[i] > 0) || items[i] == null) continue;
                                double pcs = fnow * kp.Kg[i] / (sigma * Math.Max(0.1, items[i].Weight > 0.01f ? items[i].Weight : 10.0));
                                pc.Pcs[items[i]] = pcs;
                                pc.ValDay += pcs * Price(town, items[i]);
                            }
                            double cv = 0; for (int c = 0; c < KsTab.NC; c++) cv += kp.CraftVal[c];
                            if (cv > 0) for (int c = 0; c < KsTab.NC; c++) pc.CraftVal[c] = pc.ValDay * kp.CraftVal[c] / cv;
                        }
                        into.Add(pc);
                    }
                    return true;
                }
                // bez KS: zajecia tej klasy (wies gry - typ okregu), plon = udzial ludzi w domu x stawka typu zajecia x M x g/g x n
                int D = CraftOfType(v.VillageType);
                float M = MOf(v);
                float gD = VillageByPeople.GOf(v.VillageType);
                bool on = On && !UnderKs(v);
                lock (Gate)
                {
                    if (!_built && KeyOn) { TypesUsed(); Build(); }
                    foreach (var p in book)
                    {
                        var pc = new PartCraft { Uid = p.Uid, Name = p.Name, Kind = p.Kind, Main = p.Main, Book = p, Share = tot > 0 ? p.Home / tot : 0 };
                        Side s = null;
                        if (!p.Main && p.Uid != null) _side.TryGetValue(p.Uid, out s);
                        pc.Craft = p.Main || s == null || !on ? D : s.Craft;
                        if (s != null && on && s.Target >= 0) { pc.Target = s.Target; pc.F = s.F; }
                        Action<int, double> addCraft = (c, x) =>
                        {
                            var m = c >= 0 && c < meta.Length ? meta[c] : null;
                            if (m == null || m.Type == null) return;
                            double k = pc.Share * x * M * (c == D ? 1.0 : VillageByPeople.GOf(m.Type) / Math.Max(1e-3f, gD)) * (on ? N(c) : 1.0);
                            double val = 0;
                            for (int i = 0; i < m.Goods.Length; i++)
                            {
                                double pcs = k * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, m.Goods[i].StringId);
                                double cur; pc.Pcs.TryGetValue(m.Goods[i], out cur); pc.Pcs[m.Goods[i]] = cur + pcs;
                                val += pcs * Price(town, m.Goods[i]);
                            }
                            pc.CraftVal[c] += val; pc.ValDay += val;
                        };
                        if (s != null && on) CraftShares(s, addCraft); else addCraft(pc.Craft, 1.0);
                        into.Add(pc);
                    }
                }
                return true;
            }
            catch (Exception e) { Stumble("PartsOf", e); into.Clear(); return false; }
        }

        private static ItemObject[] _ksItems; private static Campaign _ksItemsFor;
        private static ItemObject[] KsItems()
        {
            var c = Campaign.Current;
            if (_ksItems != null && ReferenceEquals(_ksItemsFor, c)) return _ksItems;
            var a = new ItemObject[KsDeliver.ItemCount];
            for (int i = 0; i < a.Length; i++) { try { a[i] = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(KsDeliver.ItemIds[i]); } catch { a[i] = null; } }
            _ksItems = a; _ksItemsFor = c;
            return a;
        }

        /// <summary>Cena sztuki w miescie targowym: wartosc x indeks kategorii miasta (bez skutkow ubocznych); bez miasta - wartosc.</summary>
        internal static double Price(Town t, ItemObject it)
        {
            if (it == null) return 0.0;
            double ix = 1.0;
            try { if (t != null && it.ItemCategory != null) ix = t.GetItemCategoryPriceIndex(it.ItemCategory); } catch { ix = 1.0; }
            if (!(ix > 0) || double.IsNaN(ix) || double.IsInfinity(ix)) ix = 1.0;
            return Math.Max(0, it.Value) * ix;
        }

        /// <summary>Indeks cen towarow zajecia w miescie (wagi stawka x wartosc), projekt F16.</summary>
        private static double CraftIndex(Town t, CraftMeta m)
        {
            double a = 0, b = 0;
            for (int i = 0; i < m.Goods.Length; i++)
            {
                double w = m.Rates[i] * Math.Max(1, m.Goods[i].Value);
                a += w * Price(t, m.Goods[i]) / Math.Max(1, m.Goods[i].Value); b += w;
            }
            return b > 0 ? a / b : 1.0;
        }

        // ============================================================ doba
        /// <summary>ArmouryBehavior.OnDailyTick: zapis z wczytania, budowa okregow, neutralny start (pierwsza doba klucza), plany produkcji wsi bez KS (co dobe), przebudowy
        /// (co dobe), regula zmiany (co 7 dob od wlaczenia), linia "Zajecia wiosek (ZW)".</summary>
        internal static void Daily()
        {
            int today = Today();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!KeyOn)
            {
                if (_plans.Count > 0) _plans = new Dictionary<Village, Plan>();
                if (!_offLogged) { _offLogged = true; Log.Info(P + "WYLACZONE w MCM (Side Village Crafts) - lista, model i magazyn wsi jak dotad, W2 (210) wedlug swojego klucza; stan w zapisie zamrozony."); }
                return;
            }
            _offLogged = false;
            if (!VillageBook.Active) { if (_plans.Count > 0) _plans = new Dictionary<Village, Plan>(); return; }
            lock (Gate)
            {
                if (_typeUsed == null) TypesUsed();
                if (_pending != null) { string p = _pending; _pending = null; ImportNow(p); }
                Build();
            }
            if (_n == null) SwitchOn(today);
            int season = today / Season;
            if (season != _regionSeason) { _regionSeason = season; _regionMoved.Clear(); }
            bool step = _onDay >= 0 && today > _onDay && (today - _onDay) % StepDays == 0;
            var plans = new Dictionary<Village, Plan>();
            var parts = new List<VillageBook.PartView>();
            int fall = 0, ks = 0, other = 0, inTrans = 0, snow = 0;
            var countBy = new int[KsTab.NC];
            foreach (var di in _dist.Values)
            {
                try
                {
                    if (di.V == null) continue;
                    if (UnderKs(di.V)) { ks++; continue; }
                    fall++;
                    // przebudowy w toku (co dobe) i regula zmiany (co 7 dob)
                    Town mt = di.V.TradeBound != null ? di.V.TradeBound.Town : (di.V.Bound != null ? di.V.Bound.Town : null);
                    bool busy = false, moved = false;
                    foreach (var s in di.Sides) if (s.Target >= 0) { Progress(di, s, today, mt); moved = true; if (s.Target >= 0) busy = true; }
                    if (step && mt != null) { int before = _dStarted; Rule(di, mt, today, busy); if (_dStarted != before) moved = true; }
                    // koszt doby: plan okregu przeliczany co 7 dob na zmiane (udzialy ludzi w domu zmieniaja sie powoli), od razu przy przebudowie, pracach i pierwszej dobie sesji
                    if (di.Slot < 0) di.Slot = (int)((uint)(di.Id ?? "").GetHashCode() % 7u);
                    bool dirty; lock (Gate) dirty = _dirty.Remove(di.Id);
                    if (!di.HasPlan || di.Dirty || dirty || moved || (today + di.Slot) % 7 == 0)
                    {
                        di.Plan = BuildPlan(di, parts); di.HasPlan = true; di.Dirty = false; _dRedone++;
                    }
                    if (di.Plan != null) plans[di.V] = di.Plan;
                    foreach (var s in di.Sides)
                    {
                        if (s.Craft >= 0 && s.Craft < KsTab.NC) countBy[s.Craft]++;
                        if (s.Craft != di.TypeCraft) other++;
                        if (s.Target >= 0) inTrans++;
                        if (s.Row != null && s.Row.O != null && s.Row.O.Terr == "snieg" && (Crop(s.Craft) && s.Craft != di.TypeCraft || Crop(s.Target))) snow++;
                    }
                }
                catch (Exception e) { Stumble("Daily(okreg)", e); }
            }
            _plans = plans;
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (LogGate.Lines) Line(today - 1, fall, ks, other, inTrans, countBy, plans.Count, ms, snow);
        }

        /// <summary>Neutralny start (A6): n(T) = rece swiata typu T przed (wszystkie wioski = typ okregu) / po (zajecia startowe); rece = ludzie w domu okregu x udzial
        /// czesci. Okregi pod KS poza rachunkiem. Raz na kampanie (zapis).</summary>
        private static void SwitchOn(int today)
        {
            var before = new double[KsTab.NC]; var after = new double[KsTab.NC];
            var parts = new List<VillageBook.PartView>();
            int nd = 0;
            foreach (var di in _dist.Values)
            {
                try
                {
                    if (di.V == null || di.TypeCraft < 0 || UnderKs(di.V) || !VillageBook.PartsOf(di.V, parts)) continue;
                    double tot = 0; foreach (var p in parts) tot += p.Home;
                    if (!(tot > 0)) continue;
                    nd++;
                    before[di.TypeCraft] += tot;
                    foreach (var p in parts)
                    {
                        if (p.Main) { after[di.TypeCraft] += p.Home; continue; }
                        Side s; if (p.Uid == null || !_side.TryGetValue(p.Uid, out s)) { after[di.TypeCraft] += p.Home; continue; }
                        double h = p.Home;
                        CraftShares(s, (c, x) => after[c] += h * x);
                    }
                }
                catch (Exception e) { Stumble("SwitchOn(okreg)", e); }
            }
            // okregi bez ludzi w domu poza rachunkiem; typ bez rak po (wszystkie wioski typu odeszly) - n 1
            // straznik (Jeff: "nie chce, aby zabilo to ekonomie"): n w 0.5-2.0 - projekt oczekuje 0.76-1.70; typ poza widelkami - nazwany w linii (swiat tego typu nie jest
            // wtedy dokladnie neutralny)
            var n = new double[KsTab.NC]; var clamped = new List<string>();
            for (int c = 0; c < KsTab.NC; c++)
            {
                double x = before[c] > 0 && after[c] > 0 ? before[c] / after[c] : 1.0;
                if (x < NMin || x > NMax) { clamped.Add(KsTab.Crafts[c] + " " + x.ToString("0.00", Inv)); x = Math.Max(NMin, Math.Min(NMax, x)); }
                n[c] = x;
            }
            _n = n; _onDay = today;
            var sb = new StringBuilder(1200);
            sb.Append(P).Append("start neutralny (A6) doba ").Append(today).Append(" | okregow ").Append(nd).Append(" | n(typ) = rece swiata przed / po:");
            bool first = true;
            for (int c = 0; c < KsTab.NC; c++)
            {
                if (!(before[c] > 0) && !(after[c] > 0)) continue;
                sb.Append(first ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(n[c].ToString("0.000", Inv)).Append(" (").Append(Math.Round(before[c])).Append("->").Append(Math.Round(after[c])).Append(')');
                first = false;
            }
            // kontrola: rece swiata typu po x n = przed (co do zaokraglen) - swiat ma tyle samo kazdego typu w dobie wlaczenia
            double worst = 0;
            for (int c = 0; c < KsTab.NC; c++) if (before[c] > 0 && after[c] > 0) worst = Math.Max(worst, Math.Abs(after[c] * n[c] / before[c] - 1.0));
            sb.Append(" | kontrola swiat typu po x n / przed: najwieksza odchylka ").Append((100 * worst).ToString("0.000", Inv)).Append("% (prog 1%)")
              .Append(" | n przyciete do ").Append(NMin.ToString("0.0", Inv)).Append('-').Append(NMax.ToString("0.0", Inv)).Append(": ").Append(clamped.Count > 0 ? string.Join(", ", clamped.ToArray()) : "zadne").Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ wolna zmiana zajecia (dopisek Jeffa 16.10)
        private static void Rule(DistInfo di, Town mt, int today, bool busy)
        {
            var s0 = Settings.Current;
            float sw = Math.Max(1.0f, s0 != null ? s0.SideVillageCraftSwitch : 1.25f);
            int hold = Math.Max(7, s0 != null ? s0.SideVillageCraftHoldDays : 60);
            var meta = MetaAll();
            var parts = new List<VillageBook.PartView>();
            if (!VillageBook.PartsOf(di.V, parts)) return;
            var home = new Dictionary<string, double>(StringComparer.Ordinal);
            var vname = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in parts) if (!p.Main && p.Uid != null) { home[p.Uid] = p.Home; vname[p.Uid] = p.Name; }
            string dtype = di.Type != null ? di.Type.StringId : "wheat_farm";
            foreach (var s in di.Sides)
            {
                if (s.Target >= 0 || s.Cand == null || s.Cand.Count < 2 || s.Row == null) continue;
                if (DistrictWorks.Locks(di.V, s.Uid)) { s.Held = 0; _dLocked++; continue; }
                var cm = meta[s.Craft];
                if (cm == null) continue;
                double cur = StartCalibration.Suit(s.Row, s.Craft, dtype) * Math.Sqrt(Math.Max(0.01, CraftIndex(mt, cm)));
                double best = double.NegativeInfinity; int bc = -1;
                foreach (int c in s.Cand)
                {
                    if (c == s.Craft || meta[c] == null) continue;
                    double v = StartCalibration.Suit(s.Row, c, dtype) * Math.Sqrt(Math.Max(0.01, CraftIndex(mt, meta[c])));
                    if (v > best) { best = v; bc = c; }
                }
                if (bc < 0 || best < sw * Math.Max(0.02, cur)) { s.Held = 0; continue; }
                s.Held += StepDays; _dPress++;
                if (s.Held < hold) continue;
                if (busy) { _dBusy++; continue; }
                double h; home.TryGetValue(s.Uid, out h);
                double hands = h * 0.5;                                       // rece wioski: polowa ludzi w domu pracuje w polu (jak udzial pracy chlopow KS)
                double rh = RegionHands(di.Region, today), moved;
                _regionMoved.TryGetValue(di.Region ?? "-", out moved);
                float share = s0 != null ? Math.Max(0f, Math.Min(1f, s0.SideVillageCraftRegionShare)) : 0.05f;
                if (moved + hands > share * rh) { _dBudget++; continue; }
                int T = meta[bc].Tree ? Math.Max(7, s0 != null ? s0.SideVillageCraftTreeDays : 364) : Math.Max(7, s0 != null ? s0.SideVillageCraftChangeDays : 60);
                double sigma = Math.Max(1, DailyLife.KsSigma);
                s.Target = bc; s.TStart = today; s.TEnd = today + T; s.F = 0f; s.Paid = 0f;
                s.Cost = (float)(CostDays * KsTab.KCost[bc] * hands / sigma);
                _regionMoved[di.Region ?? "-"] = moved + hands;
                busy = true; _dStarted++;
                string nm; if (!vname.TryGetValue(s.Uid, out nm)) nm = s.Uid;
                if (_notes.Count < 10) _notes.Add(nm + " (" + Name(di) + ") " + KsTab.Crafts[s.Craft] + "->" + KsTab.Crafts[bc] + " (" + Math.Round(hands) + " rak, " + T + " dob, " + Math.Round(s.Cost) + " zl)");
                Notify(di, s, bc, mt);
            }
        }

        private static Dictionary<string, double> _rh; private static int _rhDay = -1;

        /// <summary>Rece wiosek pobocznych regionu (warownia i jej wsie): polowa ludzi w domu - raz na dobe dla wszystkich regionow (pamiec doby).</summary>
        private static double RegionHands(string region, int today)
        {
            if (_rh == null || _rhDay != today)
            {
                var m = new Dictionary<string, double>(StringComparer.Ordinal);
                var parts = new List<VillageBook.PartView>();
                foreach (var di in _dist.Values)
                {
                    if (di.V == null || !VillageBook.PartsOf(di.V, parts)) continue;
                    double h = 0; foreach (var p in parts) if (!p.Main) h += p.Home * 0.5;
                    double cur; m.TryGetValue(di.Region ?? "-", out cur); m[di.Region ?? "-"] = cur + h;
                }
                _rh = m; _rhDay = today;
            }
            double r; return _rh.TryGetValue(region ?? "-", out r) ? r : 0.0;
        }

        private static bool Crop(int c) { return c == KsTab.CWheat || c == KsTab.CFlax || c == KsTab.CSilk || c == KsTab.CVine || c == KsTab.COlive || c == KsTab.CDate || c == KsTab.CCitrus; }

        /// <summary>Uprawa na sniegu (teren "snieg" ziemi wioski) - kontrola projektu (0 upraw na sniegu). false - bez wiersza ziemi.</summary>
        internal static bool SnowCrop(string uid, int craft)
        {
            if (uid == null || !Crop(craft)) return false;
            lock (Gate) { Side s; return _side.TryGetValue(uid, out s) && s.Row != null && s.Row.O != null && s.Row.O.Terr == "snieg"; }
        }

        /// <summary>Doba przebudowy: zaplata czesci kosztu (kiesa wsi gry -> kasa miasta targowego, najwyzej 1/4 kiesy), postep proporcjonalny do zaplaty; koniec - nowe
        /// zajecie. Rok po terminie bez zaplaty - porzucona (rece zostaja, gdzie doszly: zajecie przewazajace).</summary>
        private static void Progress(DistInfo di, Side s, int today, Town mt)
        {
            int T = Math.Max(1, s.TEnd - s.TStart);
            if (today > s.TEnd + 364)
            {
                if (s.F >= 0.5f) s.Craft = s.Target;
                s.Target = -1; s.F = 0f; s.Held = 0; s.TStart = s.TEnd = -1; s.Cost = s.Paid = 0f; _dCancel++;
                return;
            }
            double progress = 1.0;
            if (s.Cost > 0 && di.V != null && mt != null)
            {
                double due = s.Cost / T;
                int ask = (int)Math.Min(Math.Ceiling(Math.Max(0.0, Math.Min(due, s.Cost - s.Paid))), Math.Max(0, di.V.Gold) / 4);
                int got = 0;
                if (ask > 0)
                {
                    int had = di.V.Gold;
                    di.V.ChangeGold(-ask);
                    got = had - di.V.Gold;
                    if (got > 0) { int th = mt.Gold; mt.ChangeGold(got); _mVil += got; _mTown += mt.Gold - th; } else got = 0;
                }
                s.Paid += got; _dPaid += got;
                progress = due > 0 ? Math.Min(1.0, got / due) : 1.0;
                if (s.Paid >= s.Cost - 0.5f) progress = 1.0;
                if (progress < 1.0) _dStall++;
            }
            s.F = (float)Math.Min(1.0, s.F + progress / T);
            if (s.F >= 0.9999f)
            {
                s.Craft = s.Target; s.Target = -1; s.F = 0f; s.Held = 0; s.TStart = s.TEnd = -1; s.Cost = s.Paid = 0f; _dDone++;
            }
        }

        private static string Name(DistInfo di) { try { return di.V != null && di.V.Name != null ? di.V.Name.ToString() : di.Id; } catch { return di.Id ?? "?"; } }

        /// <summary>Komunikat gracza (projekt ui_text 7) tylko tam, gdzie gracz ma prace okregu albo trzyma okreg.</summary>
        private static void Notify(DistInfo di, Side s, int to, Town mt)
        {
            try
            {
                var pc = Clan.PlayerClan;
                if (pc == null || di.V == null) return;
                bool mine = di.V.Settlement != null && (di.V.Settlement.OwnerClan == pc || (di.V.Bound != null && di.V.Bound.OwnerClan == pc));
                if (!mine && !DistrictWorks.HasWorksOf(di.V, pc)) return;
                var parts = new List<VillageBook.PartView>();
                string vname = s.Uid;
                if (VillageBook.PartsOf(di.V, parts)) foreach (var p in parts) if (p.Uid == s.Uid) { vname = p.Name; break; }
                var m = Meta(to);
                Log.Player(vname + " (" + Name(di) + ") is turning to " + (m != null ? m.Name : KsTab.Crafts[to]) + " - it sells better at " + (mt != null && mt.Name != null ? mt.Name.ToString() : "the market") + ".");
            }
            catch (Exception e) { Stumble("Notify", e); }
        }

        private static void Line(int day, int fall, int ks, int other, int inTrans, int[] countBy, int plans, double ms, int snow)
        {
            var sb = new StringBuilder(1600);
            var s = Settings.Current;
            float addV = 0, remV = 0, bonV = 0; int mems = 0, nomem;
            lock (Gate)
            {
                foreach (var m in _mem.Values)
                {
                    if (m.Day != day && m.Day != day + 1) continue;
                    mems++;
                    foreach (var a in m.X.Values) { remV += a[0]; addV += a[1]; bonV += a[2]; }
                }
                nomem = _dNoMem;
            }
            sb.Append(P).Append("dzien ").Append(day).Append(" | ").Append(On ? "CZYNNE" : "NIECZYNNE (ksiega wiosek / lista BK / MaterialLaw)").Append(" | okregow z wioskami ").Append(_dist.Count)
              .Append(" (bez KS ").Append(fall).Append(", pod KS ").Append(ks).Append(" - zajecia z blokow KS i reguly A7), wiosek ").Append(_builtSide)
              .Append(" (bez wiersza ziemi ").Append(_builtNoRow).Append(", start poprawiony na typ okregu ").Append(_builtStartFix).Append(")")
              .Append(" | zajecia wiosek bez KS:");
            bool f = true;
            for (int c = 0; c < KsTab.NC; c++) { if (countBy[c] == 0) continue; sb.Append(f ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(countBy[c]); f = false; }
            if (f) sb.Append(" -");
            sb.Append(" | inne niz typ okregu ").Append(other).Append(", w przebudowie ").Append(inTrans).Append(", uprawy na sniegu (inne niz typ okregu) ").Append(snow)
              .Append(" | zmiany: nacisk ").Append(_dPress).Append(", zaczete ").Append(_dStarted).Append(", skonczone ").Append(_dDone).Append(", wstrzymane (inna wioska okregu) ").Append(_dBusy)
              .Append(", (udzial regionu) ").Append(_dBudget).Append(", zamrozone pracami ").Append(_dLocked).Append(", doby bez pelnej zaplaty ").Append(_dStall).Append(", porzucone ").Append(_dCancel)
              .Append(", zaplacone ").Append(_dPaid).Append(" zl").Append(_notes.Count > 0 ? " (" + string.Join("; ", _notes.ToArray()) + ")" : "")
              .Append(" | n(typ) z doby ").Append(_onDay).Append(':');
            f = true;
            var n = _n;
            if (n != null) for (int c = 0; c < KsTab.NC; c++) { if (Math.Abs(n[c] - 1.0) < 1e-4) continue; sb.Append(f ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(n[c].ToString("0.000", Inv)); f = false; }
            if (f) sb.Append(" 1");
            sb.Append(" | krok zajec (doba): wsi z planem ").Append(plans).Append(" (przeliczone dzis ").Append(_dRedone).Append(", co 7 dob na zmiane)").Append(", wsi z pamiecia ").Append(mems).Append(", listy BK z dopiskiem ").Append(_dLists).Append(", wywolan towaru typu ").Append(_dSteps)
              .Append(", dopiskow ").Append(_dAdded).Append(" (bez pamieci M ").Append(nomem).Append(") | wartosc bazowa: zdjete z typu ").Append(remV.ToString("0", Inv)).Append(", dodane zajec ").Append(addV.ToString("0", Inv))
              .Append(", premia prac w typie ").Append(bonV.ToString("0", Inv))
              .Append(" | kontrola pieniedzy: kiesy wsi -").Append(_mVil).Append(" / kasy miast +").Append(_mTown).Append(" (roznica ").Append(_mVil - _mTown).Append(")")
              .Append(" | W2 (210) ").Append(On ? "wylaczony (zajecia go zastepuja)" : "wedlug swojego klucza")
              .Append(" | ikona: ").Append(DistrictIcon.DayText())
              .Append(" | koszt ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia dzis ").Append(_st).Append(" (od wczytania ").Append(_stAll).Append(").");
            Log.Info(sb.ToString());
            ZeroDay(); _notes.Clear(); _st = 0; _mVil = _mTown = 0;
        }

        /// <summary>Linia startu sesji (po KS i ksiedze wiosek): plik ziemi, wioski, zapis.</summary>
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                if (!KeyOn) { Log.Info(P + "start | WYLACZONE (Side Village Crafts) - zajecia tylko pod KS (bloki KS), droga bez KS nieczynna, W2 wedlug swojego klucza."); return; }
                lock (Gate)
                {
                    if (_typeUsed == null) TypesUsed();
                    if (_pending != null) { string p = _pending; _pending = null; ImportNow(p); }
                    if (VillageBook.Active) Build();
                }
                uint crc; VillageBook.LayoutCrcNow(out crc);
                Log.Info(P + "start | wiosek pobocznych " + _builtSide + " w " + _dist.Count + " okregach (bez wiersza ziemi KS " + _builtNoRow + ", zajecie startowe poprawione na typ okregu " + _builtStartFix
                         + ") | uklad crc " + crc.ToString("X8") + " | zapis: " + _loadNote + " | n(typ) z zapisu " + (_n != null ? "TAK (doba " + _onDay + ")" : "NIE - neutralny start w pierwszej dobie")
                         + " | regula: x" + (s != null ? s.SideVillageCraftSwitch : 1.25f).ToString("0.00", Inv) + " przez " + (s != null ? s.SideVillageCraftHoldDays : 60) + " dob, przejscie "
                         + (s != null ? s.SideVillageCraftChangeDays : 60) + "/" + (s != null ? s.SideVillageCraftTreeDays : 364) + " dob, region " + ((s != null ? s.SideVillageCraftRegionShare : 0.05f) * 100).ToString("0.0", Inv) + "% na sezon"
                         + " | ksiega wiosek " + (VillageBook.Active ? "czynna" : "NIECZYNNA") + ", lista BK " + (MineralOnce.Wired ? "tak" : "NIE") + ", modele produkcji " + MaterialLaw.ProdModels + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ============================================================ zapis arm_crafts
        // zw1|doba wlaczenia|sezon|n:typ=v,...|r:region=rece,...|s:uid~zajecie~start~cel~TStart~TEnd~F~held~koszt~zaplacone;...
        internal static string Export()
        {
            try
            {
                lock (Gate)
                {
                    if (_pending != null) return _pending;                              // zapis nierozwiazany (sesja bez doby) - oddany bez zmian
                    if (_n == null && _side.Count == 0) return "";
                    var sb = new StringBuilder(32 * 1024);
                    sb.Append(SaveVer).Append('|').Append(_onDay).Append('|').Append(_regionSeason).Append("|n:");
                    var n = _n; bool f = true;
                    if (n != null) for (int c = 0; c < n.Length; c++) { if (!f) sb.Append(','); f = false; sb.Append(KsTab.Crafts[c]).Append('=').Append(n[c].ToString("R", Inv)); }
                    sb.Append("|r:"); f = true;
                    foreach (var kv in _regionMoved) { if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key)).Append('=').Append(Math.Round(kv.Value).ToString("0", Inv)); }
                    sb.Append("|s:"); f = true;
                    foreach (var s in _side.Values)
                    {
                        // tylko wioski ze stanem innym niz start (zajecie zmienione, przebudowa, nacisk) - reszta wraca z pliku ziemi
                        if (s.Craft == s.Start && s.Target < 0 && s.Held == 0) continue;
                        if (!f) sb.Append(';'); f = false;
                        sb.Append(Clean(s.Uid)).Append('~').Append(Id(s.Craft)).Append('~').Append(Id(s.Start)).Append('~').Append(Id(s.Target)).Append('~').Append(s.TStart).Append('~').Append(s.TEnd)
                          .Append('~').Append(s.F.ToString("R", Inv)).Append('~').Append(s.Held).Append('~').Append(Math.Round(s.Cost).ToString("0", Inv)).Append('~').Append(Math.Round(s.Paid).ToString("0", Inv));
                    }
                    string res = sb.ToString();
                    if (res.Length > 512 * 1024) { Log.Info(P + "zapis arm_crafts za duzy (" + res.Length + " znakow) - pominiety, stan od zapisu poprzedniego."); return ""; }
                    return res;
                }
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static string Id(int c) { return c >= 0 && c < KsTab.NC ? KsTab.Crafts[c] : "-"; }
        private static int Ix(string id) { int c; return id != null && KsTab.CraftIndex.TryGetValue(id, out c) ? c : -1; }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "-" : s.Replace('|', '/').Replace(';', ',').Replace('~', '-').Replace(',', '.').Replace('=', '-'); }

        internal static void Import(string data) { lock (Gate) { _pending = string.IsNullOrEmpty(data) ? null : data; if (_pending == null) _loadNote = "zapis bez arm_crafts (nowy klucz - start z pliku ziemi)"; } }

        private static void ImportNow(string t)
        {
            try
            {
                var p = t.Split('|');
                if (p.Length < 6 || p[0] != SaveVer) { _loadNote = "nieznany format arm_crafts (" + (p.Length > 0 ? p[0] : "?") + ") - od zera"; _st++; return; }
                int.TryParse(p[1], NumberStyles.Integer, Inv, out _onDay);
                int.TryParse(p[2], NumberStyles.Integer, Inv, out _regionSeason);
                if (p[3].StartsWith("n:", StringComparison.Ordinal) && p[3].Length > 2)
                {
                    var n = new double[KsTab.NC]; for (int c = 0; c < n.Length; c++) n[c] = 1.0;
                    int got = 0;
                    foreach (var e in p[3].Substring(2).Split(','))
                    {
                        int q = e.IndexOf('='); double v;
                        if (q <= 0 || !double.TryParse(e.Substring(q + 1), NumberStyles.Float, Inv, out v)) continue;
                        int c = Ix(e.Substring(0, q)); if (c < 0 || !(v > 0) || v > 20) continue;
                        n[c] = v; got++;
                    }
                    if (got > 0) _n = n;
                }
                _regionMoved.Clear();
                if (p[4].StartsWith("r:", StringComparison.Ordinal) && p[4].Length > 2)
                    foreach (var e in p[4].Substring(2).Split(','))
                    {
                        int q = e.LastIndexOf('='); double v;
                        if (q > 0 && double.TryParse(e.Substring(q + 1), NumberStyles.Float, Inv, out v)) _regionMoved[e.Substring(0, q)] = v;
                    }
                int ns = 0, bad = 0;
                if (p[5].StartsWith("s:", StringComparison.Ordinal) && p[5].Length > 2)
                    foreach (var e in p[5].Substring(2).Split(';'))
                    {
                        var q = e.Split('~');
                        if (q.Length != 10) { bad++; continue; }
                        var s = new Side { Uid = q[0], Craft = Ix(q[1]), Start = Ix(q[2]), Target = Ix(q[3]) };
                        int a; float fl;
                        if (int.TryParse(q[4], NumberStyles.Integer, Inv, out a)) s.TStart = a;
                        if (int.TryParse(q[5], NumberStyles.Integer, Inv, out a)) s.TEnd = a;
                        if (float.TryParse(q[6], NumberStyles.Float, Inv, out fl)) s.F = Math.Max(0f, Math.Min(1f, fl));
                        if (int.TryParse(q[7], NumberStyles.Integer, Inv, out a)) s.Held = Math.Max(0, a);
                        if (float.TryParse(q[8], NumberStyles.Float, Inv, out fl)) s.Cost = Math.Max(0f, fl);
                        if (float.TryParse(q[9], NumberStyles.Float, Inv, out fl)) s.Paid = Math.Max(0f, fl);
                        if (s.Craft < 0) { bad++; continue; }
                        if (s.Target < 0) { s.TStart = s.TEnd = -1; s.F = 0f; }
                        _side[s.Uid] = s; ns++;
                    }
                _loadNote = "arm_crafts: doba wlaczenia " + _onDay + ", n(typ) " + (_n != null ? "TAK" : "NIE") + ", wiosek ze stanem " + ns + (bad > 0 ? " (zlych " + bad + ")" : "") + ", regionow " + _regionMoved.Count;
            }
            catch (Exception e) { _loadNote = "blad odczytu arm_crafts - od zera"; _side.Clear(); _n = null; _onDay = -1; Stumble("Import", e); }
        }

        // ============================================================ odczyty dla opisu i prac okregu
        /// <summary>Zajecie wioski po uid (droga bez KS; pod KS - z PartsOf). -1 - nieznana.</summary>
        internal static int SideCraft(string uid)
        {
            if (uid == null) return -1;
            lock (Gate) { Side s; return _side.TryGetValue(uid, out s) ? s.Craft : -1; }
        }
    }
}
