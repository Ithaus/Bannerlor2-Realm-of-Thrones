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
    ///    Magazyn wsi: + 5 dob dopisanych towarow (VillageFruit.CapPostfix). Krok W2 (HamletKinds) wylaczony we wsiach z planem zajec (ReplacesW2) - rybacy to zajecie
    ///    rybackie (pelne rece); okreg bez planu (bez pliku ziemi, n = 1) i wies pod KS - W2 wedlug swojego klucza (recenzja).
    ///    NEUTRALNY START (A6): w pierwszej dobie klucza n(T) = rece swiata typu T przed / po (rece = ludzie w domu okregu x udzial czesci) - swiat ma tyle samo
    ///    kazdego typu, co dzien wczesniej; "przed" z W2 dnia wczesniej (recenzja calosci: ryby W2 jako rece rybakow, zboze W2 zdjete z typu zbozowego); w zapisie
    ///    (arm_crafts). Potem zmienia go tylko zysk.
    ///    WOLNA ZMIANA (dopisek Jeffa 16.10): co 7 dob od wlaczenia wynik zajecia = przydatnosc ziemi x sqrt(indeks cen towarow zajecia w miescie targowym, wagi stawka x wartosc);
    ///    lepsze zajecie >= x Switch przez Hold Days bez przerwy, najwyzej jedna wioska okregu w przebudowie, najwyzej Region Share rak wiosek regionu (warownia) na sezon
    ///    (91 dob), koszt przebudowy 28 dob kosztu K nowego zajecia (tabela KS) za rece wioski - rata wedlug czasu (najwyzej 1/4 kiesy na dobe; zaleglosci czekaja), placona
    ///    TA SAMA regula co roboty gminy DS (recenzja calosci 17/18.10, TownFolk.CraftsRebuildPay): prawdziwy material z polki miasta targowego (kasa targu dostaje zaplate
    ///    za zdjete sztuki) i rzemieslnicy miasta najwyzej Ds Works Wage Per Material x material (kiesy ludu); brak materialu albo zlota - przebudowa stoi (postep nigdy
    ///    ponad czesc zaplacona, najwyzej 1/T na dobe). Okres przejsciowy Change Days (sady, winnice, gaje, daktyle - Tree Days): stary towar gasnie, nowy rosnie.
    ///    Wioska z pracami okregu nie zmienia zajecia.
    /// Obieg: zloto tylko w przebudowie (kiesa wsi -> kasa miasta targowego za material + lud miasta za prace; Ds Works Real Material wylaczone - jak dotad kiesa wsi -> kasa
    /// miasta; kontrola w linii). Towar z rak - zadnej sztuki z niczego (rece zdjete = rece dodane).
    /// Klucz Side Village Crafts wylaczony: lista, model, magazyn i W2 jak dotad; stan w zapisie zostaje (zamrozony). Prace okregu bez KS dostaja wtedy plan z samych
    /// prac (premia prac w towarach typu, WorksOnlyPlans) - dodatek ma towar, a wyplata pokrycie (recenzja).
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
        // f5-zajecia (noc 17/18.10, fin88: NZW-1 13.3% w daktylach, papirus +98% w ksiedze towarow): n na TOWAR (klucz Side Village Craft Neutral Per Good) - towar -> n;
        // null = n zajecia (jak dotad). Jedna referencja podmieniana (odczyt z watkow modelu bez zamka). Zapis: arm_crafts_g (osobny klucz - starsza DLL go nie zna).
        private static volatile Dictionary<ItemObject, double> _ng;
        private static string _ngPending;           // arm_crafts_g z wczytania - do rozwiazania przy pierwszej dobie sesji (przedmioty gry)
        private static string _ngNote = "";         // opis n na towar (linia startu neutralnego)
        private static int _measNowDay = -1;        // f5: doba pomiaru pelnego (Side Village Craft Measure Now) - doba wlaczenia, zaraz po planach
        private const double NgMin = 0.2, NgMax = 5.0;   // widelki n na towar (straznik; towar maly - np. papirus, dopisek w obcej wsi bez cech BK typu - potrzebuje wiecej niz 0.5-2.0)
        private static bool PerGoodKey { get { var s = Settings.Current; return s != null && s.SideVillageCraftNeutralPerGood; } }
        private static bool MeasureNowKey { get { var s = Settings.Current; return s != null && s.SideVillageCraftMeasureNow; } }
        private static bool CropKey { get { var s = Settings.Current; return s != null && s.SideVillageCraftCropFilter; } }
        /// <summary>f5: n na towar czynne - klucz i liczby (z doby wlaczenia albo z zapisu arm_crafts_g). Bez nich n zajecia (jak dotad).</summary>
        private static bool PerGoodOn { get { return _ng != null && PerGoodKey; } }
        /// <summary>f5 (klucz Side Village Craft Crop Filter): towar zajecia zabroniony we wsi przez filtr klimatu upraw (papirus poza goracym poludniem). Nie rzuca.</summary>
        private static bool CropBlocked(Village v, ItemObject it) { return CropKey && MineralOnce.CropBlocks(v, it); }
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
            public bool WorksOnly;                              // recenzja: plan z samych prac okregu (zajecia nieczynne - klucz albo zrodla) - tylko premia prac w towarach typu, n = 1
            public Dictionary<ItemObject, float> Add;          // towar -> M x ten mnoznik = dopisek (suma po zajeciach innych wiosek)
            public HashSet<ItemObject> TypeGoods;
            public Dictionary<ItemObject, float> TypeRate;     // stawka typu na towar (suma wag)
            public Dictionary<int, double> AddK;               // f4b recenzja: zajecie -> k dopisku (udzial x g/g x n x (1 + premia); towar = k x stawka x poprawka) - tylko pomiar (watek glowny)
            // f5-zajecia (n na towar): towar typu -> n tego towaru (bez zboza kuchennego); null - n typu NType (jak dotad). TypeBonusRaw - premia prac bez n (x n towaru)
            public Dictionary<ItemObject, float> NTypeG; public float TypeBonusRaw;
        }
        private static volatile Dictionary<Village, Plan> _plans = new Dictionary<Village, Plan>();

        // pamiec doby modelu (recenzja: bez pamieci M - nigdy nie byla zapisywana; M to zawsze szacunek MOf)
        private sealed class Mem { public int Day = int.MinValue; public readonly Dictionary<ItemObject, float[]> X = new Dictionary<ItemObject, float[]>(); }   // X: towar -> [zdjete, dodane, premia] (wartosc bazowa, ostatnie wywolanie doby)
        private static readonly Dictionary<Village, Mem> _mem = new Dictionary<Village, Mem>(new RefEq());
        private sealed class RefEq : IEqualityComparer<Village>
        {
            public bool Equals(Village a, Village b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Village o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        // liczniki do linii (od ostatniej linii)
        private static int _st, _stAll, _dLists, _dAdded, _dSteps, _dStarted, _dDone, _dStall, _dBusy, _dBudget, _dLocked, _dPress, _dCancel, _dRedone;
        private static int _worksOnly, _w2Skip;                // recenzja: plany z samych prac (zajecia nieczynne), wywolania W2 pominiete we wsiach z planem zajec
        private static long _dPaid;
        private static readonly RebuildTally _tally = new RebuildTally();   // recenzja calosci: rata przebudowy - material, rzemieslnicy, droga dawna (od ostatniej linii)
        private static string _w2Note = "";                                 // recenzja calosci: W2 dnia wczesniej w "przed" neutralnego startu (linia startu)
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
                _worksOnly = 0; _w2Skip = 0; _measDay = -1; _meas.Clear(); _deferLogged = false;
                _measRound = 0; _owner = null;                                // f4b-inwest
                _ng = null; _ngPending = null; _ngNote = ""; _measNowDay = -1; // f5-zajecia
                _tally.Clear(); _w2Note = ""; _measW2 = null;
                F4ZwClear();                                   // recenzja f4-pomiary: nacisk ZW od zera po wczytaniu / nowej kampanii (bez licznikow poprzedniej sesji)
            }
        }

        // ------------------------------------------------------------ recenzja calosci (noc 17/18.10): rata przebudowy zajecia - jedna regula z robotami gminy DS
        /// <summary>Liczniki raty przebudowy od ostatniej linii: z kies wsi, do kas miast (material, zmierzone), do ludu (rzemieslnicy), material zaplacony, sztuki, doby bez
        /// materialu albo kiesy, droga dawna bez towaru.</summary>
        internal sealed class RebuildTally
        {
            public long Vil, Town, Folk, Mat, Wage, Old; public int Pcs, NoMat;
            public void Clear() { Vil = Town = Folk = Mat = Wage = Old = 0; Pcs = NoMat = 0; }
        }

        /// <summary>Rata przebudowy zajecia (ZW bez KS i regula A7 KS): TownFolk.CraftsRebuildPay - prawdziwy material z polki miasta targowego `mt` (kasa targu dostaje zaplate
        /// za zdjete sztuki) i rzemieslnicy miasta (kiesy ludu); 0 - brak materialu albo kiesy ponad skrzynie (rata czeka). Droga nieczynna (Ds Works Real Material wylaczone
        /// albo kiesa ludu nieczynna) - jak dotad: kiesa wsi -> kasa miasta (bez towaru, osobny licznik). Zwraca zloto, ktore zeszlo z kiesy wsi. Watek glowny, nie rzuca.</summary>
        internal static long PayRebuild(Village v, Town mt, int ask, RebuildTally t)
        {
            if (v == null || mt == null || mt.Settlement == null || ask <= 0 || t == null) return 0;
            try
            {
                long mat, matIn, wage; int pcs;
                long r = TownFolk.CraftsRebuildPay(v, mt.Settlement, ask, out mat, out matIn, out wage, out pcs);
                if (r >= 0)
                {
                    t.Vil += r; t.Town += matIn; t.Folk += wage; t.Mat += mat; t.Wage += wage; t.Pcs += pcs;
                    if (r == 0) t.NoMat++;
                    return r;
                }
                int had = v.Gold;
                v.ChangeGold(-ask);
                long got = (long)had - v.Gold;
                if (got <= 0) return 0;
                int th = mt.Gold;
                mt.ChangeGold((int)got);
                t.Vil += got; t.Town += (long)mt.Gold - th; t.Old += got;
                return got;
            }
            catch (Exception e) { Stumble("PayRebuild", e); return 0; }
        }

        // ------------------------------------------------------------ recenzja: odczyty dla prac okregu i W2
        /// <summary>Droga produkcji dodatku prac BEZ KS: plan wsi (zajecia albo same prace) liczy MaterialLaw.ProdPostfix - wies poza KS, ksiega wiosek czynna,
        /// MaterialLaw w modelach produkcji. Klucz zajec NIE jest potrzebny (plan z samych prac). Nie rzuca.</summary>
        internal static bool CarriesWorks(Village v)
        {
            try { return v != null && !UnderKs(v) && VillageBook.Active && MaterialLaw.ProdModels > 0; }
            catch { return false; }
        }

        /// <summary>W2 (HamletKinds) milknie TYLKO we wsi, ktorej okreg ma czynny plan zajec (wioska rybacka to tam zajecie rybackie). Okreg bez planu (bez pliku ziemi,
        /// n = 1 i same zajecia typu), plan z samych prac, wies pod KS (takze w rampie KS) - W2 wedlug swojego klucza. Sam odczyt (dowolny watek).</summary>
        internal static bool ReplacesW2(Village v)
        {
            if (v == null) return false;
            var plans = _plans; Plan pl;
            if (plans.Count == 0 || !plans.TryGetValue(v, out pl) || pl == null || pl.WorksOnly) return false;
            if (!On || UnderKs(v)) return false;
            lock (Gate) _w2Skip++;
            return true;
        }

        /// <summary>Wsie z planem zajec (bez KS) - do linii W2. Sam odczyt.</summary>
        internal static int CraftPlanCount() { int n = 0; foreach (var p in _plans.Values) if (p != null && !p.WorksOnly) n++; return n; }

        /// <summary>Do linii W2: wywolania W2 pominiete we wsiach z planem zajec od ostatniego odczytu.</summary>
        internal static int TakeW2Skip() { lock (Gate) { int n = _w2Skip; _w2Skip = 0; return n; } }

        /// <summary>Do linii PO: droga bez KS (plany z samych prac / zajecia).</summary>
        internal static string WorksPathText()
        {
            return "droga bez KS: " + (On ? "plany zajec (premia prac w planie)" : "plany z samych prac " + _worksOnly + " (zajecia " + (KeyOn ? "NIECZYNNE - zrodla" : "wylaczone") + ")")
                   + (VillageBook.Active && MaterialLaw.ProdModels > 0 ? "" : " - BRAK (ksiega wiosek albo MaterialLaw) - prace bez KS zamrozone");
        }

        /// <summary>recenzja (prace bez zajec): plany z samych prac okregu - wsie gry okregow z dodatkiem prac poza KS, gdy droga zajec nie liczy (klucz wylaczony albo
        /// zrodla nieczynne). Premia prac tylko w towarach typu (czesci z pracami zajecia typu; prace innego zajecia - bez towaru i bez wyplaty: E = 0), n = 1, bez
        /// dopiskow. Co dobe (malo okregow). Bez klucza District Works, bez ksiegi albo bez MaterialLaw - pusto.</summary>
        private static Dictionary<Village, Plan> WorksOnlyPlans()
        {
            var res = new Dictionary<Village, Plan>();
            _worksOnly = 0;
            if (!DistrictWorks.On || !VillageBook.Active || MaterialLaw.ProdModels <= 0) return res;
            var parts = new List<VillageBook.PartView>();
            foreach (var v in DistrictWorks.BonusVillages())
            {
                try
                {
                    if (v == null || v.VillageType == null || UnderKs(v)) continue;
                    int D = CraftOfType(v.VillageType);
                    if (D < 0 || !VillageBook.PartsOf(v, parts) || parts.Count < 1) continue;
                    double tot = 0; foreach (var p in parts) tot += p.Home;
                    if (!(tot > 1e-6)) continue;
                    double tb = 0;
                    foreach (var p in parts)
                    {
                        int wc; double wb;
                        if (DistrictWorks.BonusOf(v, p.Main ? null : p.Uid, out wc, out wb) && wc == D) tb += p.Home / tot * wb;
                    }
                    if (!(tb > 1e-9)) continue;
                    var pl = new Plan { Other = 0f, NType = 1f, TypeBonus = (float)tb, Type = v.VillageType, Add = new Dictionary<ItemObject, float>(), KitchenKeep = v.VillageType.StringId != "wheat_farm",
                                        TypeGoods = new HashSet<ItemObject>(), TypeRate = new Dictionary<ItemObject, float>(), WorksOnly = true };
                    foreach (var pr in v.VillageType.Productions)
                    {
                        if (pr.Item1 == null || !(pr.Item2 > 0f)) continue;
                        pl.TypeGoods.Add(pr.Item1);
                        float r; pl.TypeRate.TryGetValue(pr.Item1, out r); pl.TypeRate[pr.Item1] = r + pr.Item2;
                    }
                    res[v] = pl;
                }
                catch (Exception e) { Stumble("WorksOnlyPlans(okreg)", e); }
            }
            _worksOnly = res.Count;
            return res;
        }

        private static void ZeroDay() { _dLists = _dAdded = _dSteps = _dStarted = _dDone = _dStall = _dBusy = _dBudget = _dLocked = _dPress = _dCancel = _dRedone = 0; _dPaid = 0; }

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
            Dictionary<int, double> kBy = null;                       // f4b recenzja: dopisek wedlug zajecia (pomiar neutralnosci - rozbicie wyniku na n zajec)
            var bySide = new Dictionary<string, Side>(StringComparer.Ordinal);
            foreach (var s in di.Sides) bySide[s.Uid] = s;
            bool pg = PerGoodOn;                                      // f5-zajecia: n na towar (bez klucza i bez liczb - n zajecia, jak dotad)
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
                    double k0 = part * (VillageByPeople.GOf(m.Type) / Math.Max(1e-3f, gD)) * (1.0 + bb);   // f5: k bez n (n na towar mnozy kazdy towar osobno)
                    double k = part * (VillageByPeople.GOf(m.Type) / Math.Max(1e-3f, gD)) * N(c) * (1.0 + bb);
                    if (kBy == null) kBy = new Dictionary<int, double>();
                    double kc; kBy.TryGetValue(c, out kc); kBy[c] = kc + k;
                    for (int i = 0; i < m.Goods.Length; i++)
                    {
                        float cur; add.TryGetValue(m.Goods[i], out cur);
                        // poprawka stawki typu (sol wsi solnych, futra trapperow - GoodsRates): lancuch patrzy na typ wsi gry, wioska z tym zajeciem dostaje ja w bazie
                        if (pg) add[m.Goods[i]] = cur + (float)(k0 * NGc(c, m.Goods[i]) * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, m.Goods[i].StringId));
                        else add[m.Goods[i]] = cur + (float)(k * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, m.Goods[i].StringId));
                    }
                });
            }
            double nD = N(D);
            bool anyN = Math.Abs(nD - 1.0) >= 1e-4;
            Dictionary<ItemObject, float> ntg = null;
            if (pg)
            {
                // f5: n kazdego towaru typu (zboze kuchenne typu niezbozowego bez n - jak dotad); plan potrzebny, gdy choc jeden towar ma n inne niz 1
                ntg = new Dictionary<ItemObject, float>(); anyN = false;
                bool kk = di.Type.StringId != "wheat_farm";
                foreach (var pr in di.Type.Productions)
                {
                    if (pr.Item1 == null || !(pr.Item2 > 0f) || (kk && pr.Item1.StringId == "grain") || ntg.ContainsKey(pr.Item1)) continue;
                    double x = NGc(D, pr.Item1);
                    ntg[pr.Item1] = (float)x;
                    if (Math.Abs(x - 1.0) >= 1e-4) anyN = true;
                }
            }
            if (other <= 1e-6 && !anyN && typeBonus <= 1e-9) return null;
            var pl = new Plan { Other = (float)Math.Min(1.0, other), NType = (float)nD, TypeBonus = (float)(typeBonus * nD), Type = di.Type, Add = add, KitchenKeep = di.Type.StringId != "wheat_farm",
                                TypeGoods = new HashSet<ItemObject>(), TypeRate = new Dictionary<ItemObject, float>(), AddK = kBy, NTypeG = ntg, TypeBonusRaw = (float)typeBonus };
            foreach (var pr in di.Type.Productions)
            {
                if (pr.Item1 == null || !(pr.Item2 > 0f)) continue;
                pl.TypeGoods.Add(pr.Item1);
                float r; pl.TypeRate.TryGetValue(pr.Item1, out r); pl.TypeRate[pr.Item1] = r + pr.Item2;
            }
            return pl;
        }

        private static double N(int c) { var n = _n; return n != null && c >= 0 && c < n.Length && n[c] > 0 && !double.IsInfinity(n[c]) ? n[c] : 1.0; }

        /// <summary>f5-zajecia: n towaru it w zajeciu c - n na towar (klucz Side Village Craft Neutral Per Good i liczby z wlaczenia/zapisu), bez niego (albo towaru bez
        /// liczby) n zajecia N(c), jak dotad. Ten sam towar ma to samo n w kazdym zajeciu (ryby rybakow, wielorybnikow i lowcow morsow; maslo i ser bydla, owiec i swin).</summary>
        private static double NGc(int c, ItemObject it)
        {
            var ng = _ng; double x;
            if (ng != null && it != null && PerGoodKey && ng.TryGetValue(it, out x) && x > 0 && !double.IsInfinity(x) && !double.IsNaN(x)) return x;
            return N(c);
        }

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
                    if (CropBlocked(v, kv.Key)) continue;                     // f5: filtr klimatu upraw (papirus tylko na goracym poludniu) - jak farmy zboza tej wsi
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
        private static readonly TextObject _txtWinter = new TextObject("{=!}Winter");
        private static bool WinterAdds { get { var s = Settings.Current; return s != null && s.SideVillageCraftAddsWinter; } }

        /// <summary>MaterialLaw.ProdPostfix, PIERWSZY krok po KS (obok VillageFruit.Base - przed mnoznikami lancucha: kopalnie i las, powtorzenia BK, ludzie P1, rece E4/5
        /// i E4/7, regulator, hodowla): towar typu okregu - czynnik (1 - u_inne) x n(typ) + premia prac (zboze kuchenne typu niezbozowego bez zmian); towar zajec innych
        /// wiosek - baza = suma(udzial x stawka typu zajecia x (poziom + 1) x 0.5 x g(zajecie)/g(typ) x n(zajecie) x (1 + premia)), a dalej te same mnozniki co towar
        /// wioski tego typu (ten sam wzor co owoce wsi BK). Towar w typie i w zajeciu - baza / czynnik (dopisek bez czynnika typu). Wies nie Normal albo bez targu - nic.</summary>
        internal static void Base(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (v == null || item == null) return;
            try
            {
                Plan pl;
                var plans = _plans;
                if (plans.Count == 0 || !plans.TryGetValue(v, out pl) || pl == null) return;          // szybka droga: bez planow (klucz wylaczony i bez prac) - nic
                if (pl.WorksOnly ? !DistrictWorks.On : !On) return;                                   // recenzja: plan z samych prac - wedlug klucza District Works
                if (UnderKs(v)) return;
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
                    float nT = pl.NType, bon = pl.TypeBonus;
                    if (pl.NTypeG != null) { float xg; if (pl.NTypeG.TryGetValue(item, out xg)) nT = xg; bon = pl.TypeBonusRaw * nT; }   // f5: n tego towaru (bez - jak dotad)
                    float F = kitchen ? 1f : (1f - pl.Other) * nT + bon;
                    if (F < 0f) F = 0f;
                    float sum = 1f + r.SumOfFactors;
                    if (before > 0f && Math.Abs(F - 1f) > 1e-5f && sum > 0f) r.AddFactor((F - 1f) * sum, _txtType);
                    float typePart = Math.Max(0f, r.ResultNumber);                // f4b recenzja: czesc towaru typu po czynniku F (pomiar - rozbicie na n zajec)
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
                        Put(m, item, kitchen ? 0f : before * (1f - F + bon) * item.Value, added * item.Value, kitchen ? 0f : before * bon * item.Value);
                        _dSteps++;
                        // pomiar neutralnosci (A6): wynik przed i po kroku zajec; f4b recenzja: czesc bez n (zboze kuchenne) i czesc towaru typu (n typu okregu) - reszta to dopisek
                        if (today == _measDay && !pl.WorksOnly) MeasPut(v, item, before, r.ResultNumber, kitchen ? typePart : 0f, kitchen ? 0f : typePart);
                    }
                    return;
                }
                if (!hasAdd || !(A > 0f)) return;
                if (CropBlocked(v, item)) return;                              // f5: papirus zajecia zboza poza goracym poludniem - bez dopisku (lista BK go tez nie ma)
                float r1 = r.ResultNumber;
                r.Add(A, _txtAdd);                                            // towar spoza typu: model gry dal baze 0 - te same czynniki, ktore juz sa, i dalsze kroki lancucha
                // f4b-inwest (klucz Side Village Craft Adds Winter): zima (WinterBite.VillageProdPostfix) pomija wynik 0 - towar spoza listy typu nie dostawal ciecia zimy, ktore
                // ten sam towar ma we wsi swojego typu. Recenzja f4b: tniemy TYLKO dopisek (A x ciecie - ta sama postac co WinterBite: baza x (1 + suma - ciecie)), zmiana bazy
                // o -A x ciecie / (1 + suma); czynnik na calym wyniku cial tez baze owocow i jaj VillageFruit.Base (pomarancze gaju, jablka i marchew farmy - zima ich nie tnie)
                // i wielblada Qarthu, ktore ten sam towar ma juz w r1 - jedno zjawisko, dwie reguly. Wynik dopisku = A x (1 + suma - ciecie) - ten sam wzor co NeutralGoods.
                if (!pl.WorksOnly && WinterAdds)
                {
                    float cut = WinterBite.VillageCut(v), fs = 1f + r.SumOfFactors;
                    if (cut > 0f && fs > 1e-3f && r.ResultNumber > 0f) r.Add(-A * Math.Min(cut, fs) / fs, _txtWinter);   // dopisek najwyzej do 0 (NeutralGoods: fac >= 0)
                }
                float got = Math.Max(0f, r.ResultNumber - r1);
                lock (Gate) { var m = MemFor(v, today); Put(m, item, 0f, got * item.Value, 0f); _dAdded++; if (today == _measDay && !pl.WorksOnly) MeasPut(v, item, r1, r.ResultNumber, Math.Max(0f, r1), 0f); }
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
            if (m.Day != day) { m.Day = day; m.X.Clear(); }
            return m;
        }

        // ------------------------------------------------------------ recenzja: pomiar neutralnosci startu (A6, projekt TESTS 2) na TOWARACH, nie na rekach
        // W dobie wlaczenia (w tej sesji) kazde wywolanie modelu wsi z planem zajec zapisuje: wynik przed krokiem zajec, po nim i na koncu lancucha (MaterialLaw - After).
        // Wynik bez zajec na koncu lancucha = przed x (koniec / po) - te same mnozniki (P1, rece, regulator, zima, kopalnie, powtorzenia BK, poprawki stawek). Ostatnie
        // wywolanie doby na wies i towar (jak W2). Doba nastepna: zmiana produkcji wsi z planem wedlug towarow typu - linia "pomiar neutralnosci" (NZW-1).
        private static int _measDay = -1;
        private static readonly Dictionary<Village, Dictionary<ItemObject, float[]>> _meas = new Dictionary<Village, Dictionary<ItemObject, float[]>>(new RefEq());
        private static Dictionary<Village, double> _measW2;   // recenzja calosci: u W2 dnia wczesniej wsi z SwitchOn (ten sam W2, ktory wszedl do "przed" neutralnego startu)
        private static bool _deferLogged;

        /// <summary>Pod Gate: wynik przed i po kroku zajec (koniec lancucha - jeszcze nie). f4b recenzja: unsc - czesc wyniku po kroku, ktorej n nie skaluje (zboze kuchenne
        /// typu niezbozowego, owoce VillageFruit i wielblad Qarthu w r1 towaru spoza typu); typ - czesc towaru typu po czynniku F (skaluje ja n typu okregu); reszta (mid - unsc
        /// - typ) to dopisek zajec innych wiosek (skaluje go n zajec wedlug skladu planu, Plan.AddK).</summary>
        private static void MeasPut(Village v, ItemObject it, float before, float mid, float unsc, float typ)
        {
            Dictionary<ItemObject, float[]> d;
            if (!_meas.TryGetValue(v, out d)) { d = new Dictionary<ItemObject, float[]>(); _meas[v] = d; }
            float[] a; if (!d.TryGetValue(it, out a)) { a = new float[5]; d[it] = a; }
            a[0] = before; a[1] = mid; a[2] = -1f; a[3] = unsc; a[4] = typ;
        }

        /// <summary>MaterialLaw.ProdPostfix - koniec lancucha (przed ksiega rudy): wynik koncowy wywolania - tylko w dobie pomiaru neutralnosci. Nie rzuca.</summary>
        internal static void After(Village v, ItemObject item, float final)
        {
            if (_measDay < 0 || v == null || item == null) return;
            try
            {
                if (Today() != _measDay) return;
                lock (Gate) { Dictionary<ItemObject, float[]> d; float[] a; if (_meas.TryGetValue(v, out d) && d.TryGetValue(item, out a) && a[2] < 0f) a[2] = Math.Max(0f, final); }
            }
            catch { }
        }

        /// <summary>Linia "pomiar neutralnosci (A6, TESTS 2)" - pierwsza doba po wlaczeniu (w tej sesji). Okregi z pracami okregu pominiete (premia prac nie jest startem).
        /// f4b-inwest (klucz Side Village Craft Neutral Fix): po pomiarze doby wlaczenia zajecie z odchylka ponad 0.5% na SWOICH towarach (wlasciciel towaru - najwiekszy
        /// wytworca "przed") dostaje raz n x (przed / po) - najwyzej o 10%, w widelkach 0.5-2.0; plany od razu od nowa (ta doba produkuje juz z poprawionym n), pomiar
        /// powtorzony (linia "pomiar neutralnosci po poprawce (A6)" doby nastepnej).</summary>
        private static void Measure(int day, int today)
        {
            bool again = false;
            try
            {
                var meta = MetaAll();
                var bef = new Dictionary<ItemObject, double>(); var aft = new Dictionary<ItemObject, double>();
                int vil = 0, calls = 0, noFinal = 0, noMult = 0, works = 0, partial = 0;
                // f4b-inwest (Side Village Craft Neutral Goods): wies z dopiskami spoza listy typu, ktorej zaden dopisek nie mial wywolania modelu w dobie pomiaru (tick
                // produkcji przed OnDailyTick doby wlaczenia - potem tylko magazyn wsi pyta o towary typu), daje pomiar tylko towarow typu - bez dopiskow "po" bylo za malo.
                // Taka wies poza pomiarem w calosci (ten sam zbior wsi przed i po).
                var sM = Settings.Current;
                bool whole = sM != null && sM.SideVillageCraftNeutralGoods;
                var plansM = _plans;
                var w2 = _measW2; double w2g = 0;                              // recenzja calosci: W2 dnia wczesniej - zboze x (1 - u), ryby + u x zboze (tyle samo sztuk; W2 to ostatni krok)
                ItemObject grainIt = null, fishIt = null;
                try { grainIt = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("grain"); fishIt = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("fish"); } catch { }
                // f4b recenzja: wynik "po" (koniec lancucha) rozbity na czesc bez n (zboze kuchenne, owoce VillageFruit, wielblad) i czesci skalowane n kazdego zajecia
                // (towar typu - n typu okregu, dopisek - n zajec wedlug skladu planu) - poprawka z pomiaru liczy n tylko z czesci, ktora n naprawde skaluje
                var unsc = new Dictionary<ItemObject, double>();
                var scl = new Dictionary<ItemObject, double[]>();
                lock (Gate)
                {
                    foreach (var kv in _meas)
                    {
                        if (DistrictWorks.HasWorks(kv.Key)) { works++; continue; }
                        if (whole && !WholeMeasured(kv.Key, kv.Value, plansM)) { partial++; continue; }
                        vil++;
                        double uw = 0; if (w2 != null && grainIt != null && fishIt != null) w2.TryGetValue(kv.Key, out uw);
                        Plan plv = null; if (plansM != null) plansM.TryGetValue(kv.Key, out plv);
                        int Dv = plv != null ? CraftOfType(plv.Type) : -1;
                        foreach (var x in kv.Value)
                        {
                            var a = x.Value;
                            if (a[2] < 0f) { noFinal++; continue; }
                            calls++;
                            double b0 = Math.Max(0f, a[0]), mid = Math.Max(0f, a[1]), fin = a[2], bf;
                            if (mid > 1e-6) bf = b0 * fin / mid;
                            else if (b0 <= 1e-6) bf = 0;
                            else { bf = b0; noMult++; }                        // czynnik zajec zjadl towar (u_inne = 1) - mnozniki lancucha nieznane: wynik przed krokiem
                            if (uw > 0 && ReferenceEquals(x.Key, grainIt)) { double tw = uw * bf; bf -= tw; w2g += tw; }
                            double c;
                            bef.TryGetValue(x.Key, out c); bef[x.Key] = c + bf;
                            aft.TryGetValue(x.Key, out c); aft[x.Key] = c + fin;
                            SplitAfter(x.Key, a, mid, fin, plv, Dv, meta, unsc, scl);
                        }
                    }
                    _meas.Clear();
                }
                if (w2g > 0 && fishIt != null) { double c; bef.TryGetValue(fishIt, out c); bef[fishIt] = c + w2g; }
                // swiat: produkcja wsi tej doby z ksiegi towarow (wszystkie wsie, takze pod KS i bez planu) - tylko INFO
                var led = new Dictionary<string, double[]>(StringComparer.Ordinal);
                int ld = -1; try { ld = GoodsLedger.KsTake(led); } catch { ld = -1; }
                bool ledOk = ld == day;
                var sb = new StringBuilder(1600);
                bool round2 = _measRound > 0;
                sb.Append(P).Append(round2 ? "pomiar neutralnosci po poprawce (A6) doba " : "pomiar neutralnosci (A6, TESTS 2) doba ").Append(day).Append(" | wsi zmierzonych ").Append(vil).Append(", wywolan ").Append(calls)
                  .Append(" (bez konca lancucha ").Append(noFinal).Append(", bez mnoznika ").Append(noMult).Append("), okregi z pracami pominiete ").Append(works)
                  .Append(" | zmiana produkcji typu we wsiach z planem (wartosc, przed z W2 dnia wczesniej -> po; W2 zboze -> ryby ").Append(w2g.ToString("0", Inv)).Append(" szt.):");
                double worst = 0, worstW = 0; string worstT = "-", worstWT = "-"; bool f = true;
                for (int c = 0; c < KsTab.NC; c++)
                {
                    var m = meta != null && c < meta.Length ? meta[c] : null;
                    if (m == null || m.Type == null || m.Horse || m.Goods.Length == 0 || _typeUsed == null || c >= _typeUsed.Length || !_typeUsed[c]) continue;
                    if (c == KsTab.CCattleB) continue;                         // maslo - ten sam typ gry co bydlo
                    double vb = 0, va = 0, vw = 0;
                    foreach (var it in m.Goods)
                    {
                        double b, a; bef.TryGetValue(it, out b); aft.TryGetValue(it, out a);
                        double val = Math.Max(1, it.Value);
                        vb += b * val; va += a * val;
                        double[] w; if (ledOk && it.StringId != null && led.TryGetValue(it.StringId, out w) && w != null && w.Length > GoodsLedger.KsVil) vw += Math.Max(b, w[GoodsLedger.KsVil]) * val;
                    }
                    if (!(vb > 0) && !(va > 0)) continue;
                    double dev = vb > 0 ? (va - vb) / vb : 1.0;
                    sb.Append(f ? " " : ", ").Append(m.Id).Append(' ').Append((100 * dev).ToString("+0.00;-0.00;0.00", Inv)).Append('%');
                    f = false;
                    if (Math.Abs(dev) > Math.Abs(worst)) { worst = dev; worstT = m.Id; }
                    if (vw > 0) { double dw = (va - vb) / vw; if (Math.Abs(dw) > Math.Abs(worstW)) { worstW = dw; worstWT = m.Id; } }
                }
                if (f) sb.Append(" -");
                // towary: najwieksza zmiana pojedynczego towaru (INFO)
                double wi = 0; string wit = "-";
                foreach (var kv in bef) { double a; aft.TryGetValue(kv.Key, out a); if (kv.Value > 1) { double d = (a - kv.Value) / kv.Value; if (Math.Abs(d) > Math.Abs(wi)) { wi = d; wit = kv.Key.StringId; } } }
                sb.Append(" | najwieksza odchylka produkcji typu ").Append((100 * Math.Abs(worst)).ToString("0.000", Inv)).Append("% (prog 1%) w ").Append(worstT)
                  .Append(" | wobec swiata (ksiega towarow doby ").Append(ledOk ? day.ToString(Inv) : "BRAK").Append("): najwieksza ").Append((100 * Math.Abs(worstW)).ToString("0.000", Inv)).Append("% w ").Append(worstWT)
                  .Append(" | towar: najwieksza zmiana ").Append((100 * wi).ToString("+0.0;-0.0;0.0", Inv)).Append("% (").Append(wit).Append(") | W2 (zboze -> ryby) dnia wczesniej w 'przed' (recenzja calosci) - zajecia go zastepuja");
                if (whole) sb.Append(" | wsi poza pomiarem - dopiski bez wywolania w dobie pomiaru (tick przed wlaczeniem, f4b): ").Append(partial);
                // f4b-inwest: jednorazowa poprawka n z pomiaru doby wlaczenia (klucz Side Village Craft Neutral Fix)
                var s0 = Settings.Current;
                if (!round2)
                {
                    if (s0 != null && s0.SideVillageCraftNeutralFix && vil > 0 && calls > 0 && _n != null)
                    {
                        int changed;
                        string fx;
                        if (PerGoodOn)
                        {
                            // f5-zajecia: n na towar czynne (Side Village Craft Measure Now wylaczony) - plany licza n towaru, wiec poprawka tez na towarach (n zajecia
                            // nic by w produkcji nie zmienilo); czesc bez n z rozbicia SplitAfter
                            var ms5 = new MeasSum();
                            foreach (var kv in bef) ms5.Bef[kv.Key] = kv.Value;
                            foreach (var kv in aft) ms5.Aft[kv.Key] = kv.Value;
                            foreach (var kv in unsc) ms5.UnG[kv.Key] = kv.Value;
                            string f5t = FixPerGood(ms5, meta, out changed);
                            fx = "zajec poprawionych " + changed + " (f5, n na towar - liczba to towary: " + f5t + ")";
                        }
                        else fx = FixFromMeasure(bef, aft, unsc, scl, meta, out changed);
                        sb.Append(" | poprawka n z pomiaru (f4b, Side Village Craft Neutral Fix): ").Append(fx);
                        if (changed > 0) again = true;
                    }
                    else sb.Append(" | poprawka n z pomiaru (f4b): ").Append(s0 != null && s0.SideVillageCraftNeutralFix ? "bez pomiaru (wsi 0)" : "WYLACZONA (Side Village Craft Neutral Fix)");
                }
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Measure", e); again = false; }
            finally
            {
                if (again)
                {
                    // plany od nowa jeszcze w tej dobie (Daily buduje je zaraz po pomiarze) i pomiar tej doby po poprawce
                    foreach (var di in _dist.Values) di.Dirty = true;
                    _measDay = today; _measRound = 1; lock (Gate) _meas.Clear();
                }
                else { _measDay = -1; _measRound = 0; lock (Gate) { _meas.Clear(); _measW2 = null; } }
            }
        }

        /// <summary>f4b-inwest: poprawka n z pomiaru doby wlaczenia - dla zajecia c wartosc przed / po na jego wlasnych towarach (wlasciciel = najwiekszy wytworca "przed"
        /// z rachunku na towarach; bez niego - zajecie, ktorego glowny towar to ten towar, albo pierwsze zajecie z tym towarem). |po/przed - 1| > 0.5%: n x r, najwyzej
        /// o 10%, w widelkach 0.5-2.0. Recenzja f4b: r NIE jest prostym ilorazem przed / po - wynik "po" towarow zajecia c to czesc bez n (zboze kuchenne typu niezbozowego,
        /// owoce VillageFruit, wielblad Qarthu) + suma po zajeciach d (n(d) x czesc skalowana n(d); towar na liscie kilku typow - np. ryby rybakow, wielorybnikow i lowcow
        /// morsow - skaluja n kilku zajec). Uklad liniowy przed_c = bez_n_c + suma_d r_d x X[d, c] rozwiazany iteracja Gaussa-Seidla z widelkami (zajecie bez wlasnej czesci
        /// skalowanej - bez poprawki); zajecie w 0.5% wchodzi do ukladu, gdy poprawki innych wypchnelyby je poza prog. Prosty iloraz zostawialby blad K x (1 - B / (nS + K)) / B
        /// przy czesci bez n K (zboze +5% przy 40% kuchennego - nadal ok. +2%). Zwraca opis do linii (bez pionowej kreski).</summary>
        private static string FixFromMeasure(Dictionary<ItemObject, double> bef, Dictionary<ItemObject, double> aft, Dictionary<ItemObject, double> unsc, Dictionary<ItemObject, double[]> scl,
                                             CraftMeta[] meta, out int changed)
        {
            changed = 0;
            int NC = KsTab.NC;
            var fb = new double[NC]; var fa = new double[NC]; var fu = new double[NC];
            var X = new double[NC, NC];                                   // X[d, c]: wartosc "po" towarow zajecia c skalowana n(d)
            var owner = _owner;
            var items = new HashSet<ItemObject>(bef.Keys); foreach (var k in aft.Keys) items.Add(k);
            foreach (var it in items)
            {
                int c = -1;
                if (owner == null || !owner.TryGetValue(it, out c)) c = OwnerByMeta(it, meta);
                if (c < 0 || c >= NC) continue;
                double b, a, u; bef.TryGetValue(it, out b); aft.TryGetValue(it, out a);
                double val = Math.Max(1, it.Value);
                fb[c] += b * val; fa[c] += a * val;
                if (unsc != null && unsc.TryGetValue(it, out u) && u > 0) fu[c] += u * val;
                double[] sv; if (scl != null && scl.TryGetValue(it, out sv) && sv != null) for (int d = 0; d < NC && d < sv.Length; d++) if (sv[d] > 0) X[d, c] += sv[d] * val;
            }
            var n = (double[])_n.Clone();
            var r = new double[NC]; var raw = new double[NC]; var lo = new double[NC]; var hi = new double[NC]; var act = new bool[NC];
            for (int c = 0; c < NC; c++)
            {
                r[c] = 1.0; raw[c] = 1.0;
                double nc = c < n.Length && n[c] > 0 ? n[c] : 1.0;
                lo[c] = Math.Min(1.0, Math.Max(0.9, NMin / nc)); hi[c] = Math.Max(1.0, Math.Min(1.1, NMax / nc));
                if (c < n.Length && fb[c] > 0 && fa[c] > 0 && X[c, c] > 0 && Math.Abs(fa[c] / fb[c] - 1.0) > 0.005) act[c] = true;
            }
            int iters = 0;
            for (int round = 0; round < 4; round++)
            {
                for (int k = 0; k < 200; k++)
                {
                    double md = 0;
                    for (int c = 0; c < NC; c++)
                    {
                        if (!act[c]) continue;
                        double rest = fu[c];
                        for (int d = 0; d < NC; d++) if (d != c) rest += r[d] * X[d, c];
                        double rr = (fb[c] - rest) / X[c, c];
                        raw[c] = rr;
                        rr = Math.Max(lo[c], Math.Min(hi[c], rr));
                        md = Math.Max(md, Math.Abs(rr - r[c]));
                        r[c] = rr;
                    }
                    iters++;
                    if (md < 1e-10) break;
                }
                bool more = false;
                for (int c = 0; c < NC; c++)
                {
                    if (act[c] || c >= n.Length || !(fb[c] > 0) || !(X[c, c] > 0)) continue;
                    double p = fu[c]; for (int d = 0; d < NC; d++) p += r[d] * X[d, c];
                    if (Math.Abs(p / fb[c] - 1.0) > 0.005) { act[c] = true; more = true; }
                }
                if (!more) break;
            }
            // opis: poprawione zajecia, czesc bez n, luka rozbicia (bez_n + suma X wobec "po") i przewidywana odchylka po poprawce
            var notes = new List<string>();
            double gap = 0, worstP = 0, fuT = 0, faT = 0; string worstPT = "-";
            for (int c = 0; c < NC; c++)
            {
                if (fa[c] > 0) { double s = fu[c]; for (int d = 0; d < NC; d++) s += X[d, c]; gap = Math.Max(gap, Math.Abs(s - fa[c]) / fa[c]); fuT += fu[c]; faT += fa[c]; }
                if (fb[c] > 0 && (fa[c] > 0 || X[c, c] > 0))
                {
                    double p = fu[c]; for (int d = 0; d < NC; d++) p += r[d] * X[d, c];
                    double dv = p / fb[c] - 1.0;
                    if (Math.Abs(dv) > Math.Abs(worstP)) { worstP = dv; worstPT = KsTab.Crafts[c]; }
                }
                if (!act[c] || Math.Abs(r[c] - 1.0) < 1e-6) continue;
                double nn = Math.Max(NMin, Math.Min(NMax, n[c] * r[c]));
                if (Math.Abs(nn - n[c]) < 1e-6) continue;
                double oth = 0; for (int d = 0; d < NC; d++) if (d != c) oth += X[d, c];
                bool clip = Math.Abs(raw[c] - r[c]) > 1e-9;
                notes.Add(KsTab.Crafts[c] + " x" + (nn / n[c]).ToString("0.0000", Inv) + " (n " + n[c].ToString("0.000", Inv) + "->" + nn.ToString("0.000", Inv)
                          + "; prosty iloraz x" + (fa[c] > 0 ? fb[c] / fa[c] : 1.0).ToString("0.0000", Inv)
                          + ", bez n " + (fa[c] > 0 ? 100 * fu[c] / fa[c] : 0).ToString("0.0", Inv) + "%, n innych zajec " + (fa[c] > 0 ? 100 * oth / fa[c] : 0).ToString("0.0", Inv) + "%"
                          + (clip ? ", przyciete z x" + raw[c].ToString("0.000", Inv) : "") + ")");
                n[c] = nn; changed++;
            }
            if (changed > 0) _n = n;
            return "zajec poprawionych " + changed + (notes.Count > 0 ? ": " + string.Join(", ", notes.ToArray()) : " (wszystkie w 0.5%)") + (changed > 0 ? " - plany od nowa w tej dobie, pomiar po poprawce w linii doby nastepnej" : "")
                   + (owner == null ? " (wlasciciel towaru z typow - bez rachunku na towarach)" : "")
                   + "; uklad liniowy (f4b recenzja): czesc bez n " + (faT > 0 ? 100 * fuT / faT : 0).ToString("0.0", Inv) + "% wyniku po, luka rozbicia " + (100 * gap).ToString("0.000", Inv)
                   + "%, przewidywana odchylka po poprawce " + (100 * Math.Abs(worstP)).ToString("0.000", Inv) + "% w " + worstPT + ", iteracji " + iters;
        }

        /// <summary>f4b recenzja: wynik koncowy jednego wywolania (pod Gate) rozbity na czesc bez n i czesci skalowane n zajec. a = [przed, po kroku, koniec, bez n, typ];
        /// mnozniki lancucha za krokiem zajec (koniec / po kroku) wspolne dla wszystkich czesci. Dopisek dzielony wedlug skladu planu (k zajecia x stawka x poprawka stawki).</summary>
        private static void SplitAfter(ItemObject it, float[] a, double mid, double fin, Plan plv, int Dv, CraftMeta[] meta, Dictionary<ItemObject, double> unsc, Dictionary<ItemObject, double[]> scl)
        {
            if (it == null || a == null || !(fin > 0)) return;
            double c0;
            if (!(mid > 1e-6)) { unsc.TryGetValue(it, out c0); unsc[it] = c0 + fin; return; }
            double k = fin / mid;
            double U = a.Length > 3 ? Math.Max(0.0, Math.Min(mid, a[3])) : 0.0;
            double T = a.Length > 4 ? Math.Max(0.0, Math.Min(mid - U, a[4])) : 0.0;
            double ad = Math.Max(0.0, mid - U - T);
            double[] sv = null;
            if (T > 0)
            {
                if (Dv >= 0 && Dv < KsTab.NC) { if (!scl.TryGetValue(it, out sv)) { sv = new double[KsTab.NC]; scl[it] = sv; } sv[Dv] += T * k; }
                else U += T;
            }
            if (ad > 1e-9)
            {
                double W = 0;
                if (plv != null && plv.AddK != null && meta != null) foreach (var kc in plv.AddK) W += kc.Value * RateIn(meta, kc.Key, it);
                if (W > 0)
                {
                    if (sv == null && !scl.TryGetValue(it, out sv)) { sv = new double[KsTab.NC]; scl[it] = sv; }
                    foreach (var kc in plv.AddK) { double w = kc.Value * RateIn(meta, kc.Key, it); if (w > 0 && kc.Key >= 0 && kc.Key < KsTab.NC) sv[kc.Key] += ad * k * w / W; }
                }
                else U += ad;
            }
            if (U > 0) { unsc.TryGetValue(it, out c0); unsc[it] = c0 + U * k; }
        }

        /// <summary>f4b recenzja: stawka towaru w zajeciu c (suma stawek typu x poprawka stawki GoodsRates - ten sam wzor co dopisek BuildPlan); 0 - zajecie go nie robi.</summary>
        private static double RateIn(CraftMeta[] meta, int c, ItemObject it)
        {
            if (meta == null || c < 0 || c >= meta.Length || meta[c] == null || meta[c].Type == null || meta[c].Goods == null) return 0.0;
            var m = meta[c]; double s = 0;
            for (int i = 0; i < m.Goods.Length; i++) if (ReferenceEquals(m.Goods[i], it)) s += m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, it.StringId);
            return s;
        }

        /// <summary>f4b-inwest: wies zmierzona w calosci - jej plan nie ma dopiskow spoza listy typu albo choc jeden z nich mial wywolanie modelu (z koncem lancucha)
        /// w dobie pomiaru. Pod Gate.</summary>
        private static bool WholeMeasured(Village v, Dictionary<ItemObject, float[]> got, Dictionary<Village, Plan> plans)
        {
            Plan pl;
            if (plans == null || !plans.TryGetValue(v, out pl) || pl == null || pl.Add == null || pl.TypeGoods == null) return true;
            bool need = false;
            foreach (var ad in pl.Add)
            {
                if (!(ad.Value > 0f) || pl.TypeGoods.Contains(ad.Key)) continue;
                need = true;
                float[] a; if (got.TryGetValue(ad.Key, out a) && a[2] >= 0f) return true;
            }
            return !need;
        }

        // ------------------------------------------------------------ f5-zajecia: POMIAR PELNY W DOBIE WLACZENIA (klucz Side Village Craft Measure Now)
        // Dowod (fin88 Armoury-2026-10-18_03-13-38.log, ksiega towarow wobec fin86 bez zajec, doby 109078-109105): pomiar doby nastepnej czytal tylko wywolania ticku gry -
        // wies z pelnym magazynem (TickProductions: zapas >= 1.5 x pojemnosci) nie produkuje i pyta model tylko o towary typu (GetWarehouseCapacity), wiec jej dopiski
        // nie mialy wywolan; WholeMeasured wykluczal wies tylko, gdy ZADEN dopisek spoza typu nie mial wywolania - wies z jednym wywolanym i drugim niewywolanym liczyla
        // sie cala bez czesci dopiskow "po". Pomiar dal daktyle -13.3%, poprawka podniosla n daktyli o 10%, a ksiega swiata pokazala daktyle +9.2% wobec fin86 (swiat
        // byl juz w ok. 1%, poprawka go rozregulowala; tak samo ryby, sol, glina, bydlo). Teraz: w dobie wlaczenia, zaraz po opublikowaniu planow (OnDailyTick - przed
        // jakimkolwiek tickiem produkcji tej doby) model jest pytany WPROST o kazdy towar typu i kazdy dopisek kazdej wsi z planem (pelny lancuch z naszym postfiksem -
        // te same kroki co tick); przed = wynik przed krokiem zajec x (koniec / po) - jak dotad; poprawka (n na towar - dokladna, kazdy towar osobno; bez niej - uklad
        // liniowy f4b na zajeciach) jeszcze w tej dobie, plany od nowa, pomiar powtorzony. Linia "pomiar neutralnosci (A6, TESTS 2) doba S" pokazuje stan, z ktorym
        // swiat produkuje dobe S (po poprawce), z dopiskiem "przed poprawka". Raz na kampanie; koszt w linii.
        private sealed class MeasSum
        {
            public readonly Dictionary<ItemObject, double> Bef = new Dictionary<ItemObject, double>(), Aft = new Dictionary<ItemObject, double>();
            public readonly Dictionary<ItemObject, double> Unsc = new Dictionary<ItemObject, double>(), UnG = new Dictionary<ItemObject, double>();
            public readonly Dictionary<ItemObject, double[]> Scl = new Dictionary<ItemObject, double[]>();
            public int Vil, Calls, NoFinal, NoMult, Works, Asked, AskVil; public double W2g;
        }

        /// <summary>f5: model produkcji pytany wprost o kazdy towar typu (bez papirusu zdjetego z listy BK filtrem klimatu) i kazdy dopisek (bez zabronionych filtrem
        /// dopiskow) kazdej wsi z planem zajec poza KS, bez prac okregu, Normal z targiem. Pelny lancuch modelu (BK, NavalDLC, zima, nasz postfiks - te same kroki co tick;
        /// poza tickiem bez sufitu dymarki i bez przesuniecia koni stadnin). Base i After zapisuja wynik do pomiaru (doba _measDay). Zwraca liczbe wywolan.</summary>
        private static int AskModel(MeasSum s)
        {
            TaleWorlds.CampaignSystem.ComponentInterfaces.VillageProductionCalculatorModel model = null;
            try { model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.VillageProductionCalculatorModel : null; } catch { model = null; }
            if (model == null) return 0;
            int calls = 0;
            var items = new List<ItemObject>();
            foreach (var kv in _plans)
            {
                var v = kv.Key; var pl = kv.Value;
                try
                {
                    if (v == null || pl == null || pl.WorksOnly || UnderKs(v) || DistrictWorks.HasWorks(v)) continue;
                    if (v.VillageState != Village.VillageStates.Normal || v.TradeBound == null) continue;
                    items.Clear();
                    if (pl.TypeGoods != null) foreach (var it in pl.TypeGoods) if (it != null && !MineralOnce.CropBlocks(v, it)) items.Add(it);
                    if (pl.Add != null) foreach (var ad in pl.Add) if (ad.Key != null && ad.Value > 0f && (pl.TypeGoods == null || !pl.TypeGoods.Contains(ad.Key)) && !CropBlocked(v, ad.Key)) items.Add(ad.Key);
                    s.AskVil++;
                    foreach (var it in items)
                    {
                        try { model.CalculateDailyProductionAmount(v, it); calls++; }
                        catch (Exception e) { Stumble("MeasureNow(model)", e); }
                    }
                }
                catch (Exception e) { Stumble("MeasureNow(wies)", e); }
            }
            s.Asked += calls;
            return calls;
        }

        /// <summary>f5: zbior wywolan doby pomiaru (pod Gate) - ten sam rachunek co Measure (przed = przed x koniec / po, W2 dnia wczesniej w "przed", rozbicie SplitAfter)
        /// i czesc bez n na towar (UnG: zboze kuchenne, owoce, wielblad - reszta wyniku skaluje n tego towaru). Czysci pamiec pomiaru.</summary>
        private static void Collect(MeasSum s, CraftMeta[] meta)
        {
            var plansM = _plans;
            var w2 = _measW2;
            ItemObject grainIt = null, fishIt = null;
            try { grainIt = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("grain"); fishIt = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("fish"); } catch { }
            lock (Gate)
            {
                foreach (var kv in _meas)
                {
                    if (DistrictWorks.HasWorks(kv.Key)) { s.Works++; continue; }
                    s.Vil++;
                    double uw = 0; if (w2 != null && grainIt != null && fishIt != null) w2.TryGetValue(kv.Key, out uw);
                    Plan plv = null; if (plansM != null) plansM.TryGetValue(kv.Key, out plv);
                    int Dv = plv != null ? CraftOfType(plv.Type) : -1;
                    foreach (var x in kv.Value)
                    {
                        var a = x.Value;
                        if (a[2] < 0f) { s.NoFinal++; continue; }
                        s.Calls++;
                        double b0 = Math.Max(0f, a[0]), mid = Math.Max(0f, a[1]), fin = a[2], bf;
                        if (mid > 1e-6) bf = b0 * fin / mid;
                        else if (b0 <= 1e-6) bf = 0;
                        else { bf = b0; s.NoMult++; }
                        if (uw > 0 && ReferenceEquals(x.Key, grainIt)) { double tw = uw * bf; bf -= tw; s.W2g += tw; }
                        double c;
                        s.Bef.TryGetValue(x.Key, out c); s.Bef[x.Key] = c + bf;
                        s.Aft.TryGetValue(x.Key, out c); s.Aft[x.Key] = c + fin;
                        SplitAfter(x.Key, a, mid, fin, plv, Dv, meta, s.Unsc, s.Scl);
                        double U = mid > 1e-6 ? Math.Max(0.0, Math.Min(mid, (double)a[3])) * fin / mid : Math.Max(0.0, fin);
                        s.UnG.TryGetValue(x.Key, out c); s.UnG[x.Key] = c + U;
                    }
                }
                _meas.Clear();
            }
            if (s.W2g > 0 && fishIt != null) { double c; s.Bef.TryGetValue(fishIt, out c); s.Bef[fishIt] = c + s.W2g; }
        }

        /// <summary>f5: odchylki wedlug zajecia (wartosc towarow zajecia przed -> po, jak Measure) - opis do linii, najwieksza i najwieksza wobec swiata (ksiega doby ledDay).</summary>
        private static void TypeDevs(MeasSum s, CraftMeta[] meta, Dictionary<string, double[]> led, bool ledOk, StringBuilder sb, out double worst, out string worstT, out double worstW, out string worstWT)
        {
            worst = 0; worstW = 0; worstT = "-"; worstWT = "-"; bool f = true;
            for (int c = 0; c < KsTab.NC; c++)
            {
                var m = meta != null && c < meta.Length ? meta[c] : null;
                if (m == null || m.Type == null || m.Horse || m.Goods.Length == 0 || _typeUsed == null || c >= _typeUsed.Length || !_typeUsed[c]) continue;
                if (c == KsTab.CCattleB) continue;
                double vb = 0, va = 0, vw = 0;
                foreach (var it in m.Goods)
                {
                    double b, a; s.Bef.TryGetValue(it, out b); s.Aft.TryGetValue(it, out a);
                    double val = Math.Max(1, it.Value);
                    vb += b * val; va += a * val;
                    double[] w; if (ledOk && led != null && it.StringId != null && led.TryGetValue(it.StringId, out w) && w != null && w.Length > GoodsLedger.KsVil) vw += Math.Max(b, w[GoodsLedger.KsVil]) * val;
                }
                if (!(vb > 0) && !(va > 0)) continue;
                double dev = vb > 0 ? (va - vb) / vb : 1.0;
                if (sb != null) { sb.Append(f ? " " : ", ").Append(m.Id).Append(' ').Append((100 * dev).ToString("+0.00;-0.00;0.00", Inv)).Append('%'); f = false; }
                if (Math.Abs(dev) > Math.Abs(worst)) { worst = dev; worstT = m.Id; }
                if (vw > 0) { double dw = (va - vb) / vw; if (Math.Abs(dw) > Math.Abs(worstW)) { worstW = dw; worstWT = m.Id; } }
            }
            if (sb != null && f) sb.Append(" -");
        }

        /// <summary>f5: odchylka kazdego towaru (sztuki przed -> po, towary z produkcja przed >= 1 szt./d): najwieksza, liczba ponad 1% i lista (najwyzej 6).</summary>
        private static string GoodDevs(MeasSum s, out double worst, out string worstG, out int over)
        {
            worst = 0; worstG = "-"; over = 0; int n = 0;
            var bad = new List<KeyValuePair<string, double>>();
            foreach (var kv in s.Bef)
            {
                if (kv.Key == null || !(kv.Value >= 1.0)) continue;
                double a; s.Aft.TryGetValue(kv.Key, out a);
                double d = (a - kv.Value) / kv.Value;
                n++;
                if (Math.Abs(d) > Math.Abs(worst)) { worst = d; worstG = kv.Key.StringId; }
                if (Math.Abs(d) > 0.01) { over++; bad.Add(new KeyValuePair<string, double>(kv.Key.StringId, d)); }
            }
            bad.Sort((x, y) => Math.Abs(y.Value).CompareTo(Math.Abs(x.Value)));
            var sb = new StringBuilder();
            for (int i = 0; i < bad.Count && i < 6; i++) { if (sb.Length > 0) sb.Append(", "); sb.Append(bad[i].Key).Append(' ').Append((100 * bad[i].Value).ToString("+0.00;-0.00;0.00", Inv)).Append('%'); }
            return "towarow " + n + " (produkcja przed >= 1 szt./d), ponad 1%: " + over + (sb.Length > 0 ? " (" + sb + ")" : "");
        }

        /// <summary>f5: poprawka n NA TOWAR z pomiaru pelnego - dokladna: wynik "po" towaru g = czesc bez n (UnG) + czesc skalowana n_g (reszta), wiec r_g = (przed_g - bez_n_g)
        /// / skalowana_g; poprawiany towar z odchylka ponad 0.1%, r w 0.5-2.0, n w widelkach n na towar. n zajecia (_n - wycena czesci, linia doby, starsza DLL) dostaje
        /// srednia harmoniczna poprawek jego towarow wazona wartoscia "przed". Zwraca opis (bez pionowej kreski).</summary>
        private static string FixPerGood(MeasSum s, CraftMeta[] meta, out int changed)
        {
            changed = 0;
            var ng0 = _ng;
            if (ng0 == null) return "brak n na towar - bez poprawki";
            var ng = new Dictionary<ItemObject, double>(ng0);
            var r = new Dictionary<ItemObject, double>();
            var notes = new List<KeyValuePair<double, string>>();
            int noN = 0, noScaled = 0, clip = 0; double worstP = 0; string worstPG = "-";
            var keys = new HashSet<ItemObject>(s.Bef.Keys); foreach (var k in s.Aft.Keys) keys.Add(k);
            foreach (var it in keys)
            {
                if (it == null) continue;
                double b, a, u; s.Bef.TryGetValue(it, out b); s.Aft.TryGetValue(it, out a); s.UnG.TryGetValue(it, out u);
                if (!(b > 1e-9)) continue;
                double S = a - u;
                double dev = (a - b) / b;
                if (Math.Abs(dev) <= 0.001) continue;                         // poprawka dokladna (liniowa) - prog 0.1% (f4b: 0.5% przy ukladzie przyblizonym)
                if (!(S > 1e-9)) { noScaled++; continue; }
                double x; if (!ng.TryGetValue(it, out x) || !(x > 0)) { noN++; continue; }
                double rr = (b - u) / S, rc = Math.Max(0.5, Math.Min(2.0, rr));
                double nn = Math.Max(NgMin, Math.Min(NgMax, x * rc));
                if (Math.Abs(nn - x) < 1e-9) continue;
                bool cut = Math.Abs(rc - rr) > 1e-9 || Math.Abs(nn - x * rc) > 1e-9;
                if (cut) clip++;
                ng[it] = nn; r[it] = nn / x; changed++;
                double pred = (u + S * nn / x - b) / b;
                if (Math.Abs(pred) > Math.Abs(worstP)) { worstP = pred; worstPG = it.StringId; }
                notes.Add(new KeyValuePair<double, string>(Math.Abs(dev) * b * Math.Max(1, it.Value),
                    it.StringId + " " + (100 * dev).ToString("+0.00;-0.00;0.00", Inv) + "% x" + (nn / x).ToString("0.0000", Inv) + " (n " + x.ToString("0.000", Inv) + "->" + nn.ToString("0.000", Inv)
                    + ", bez n " + (a > 0 ? 100 * u / a : 0).ToString("0.0", Inv) + "%" + (cut ? ", przyciete z x" + rr.ToString("0.000", Inv) : "") + ")"));
            }
            if (changed > 0)
            {
                _ng = ng;
                // n zajecia: srednia harmoniczna poprawek towarow zajecia (wartosc przed) - wycena czesci, linia doby i starsza DLL (arm_crafts)
                var n = _n != null ? (double[])_n.Clone() : null;
                if (n != null && meta != null)
                    for (int c = 0; c < KsTab.NC && c < n.Length; c++)
                    {
                        var m = meta[c];
                        if (m == null || m.Goods == null || m.Goods.Length == 0) continue;
                        double wb = 0, wr = 0;
                        foreach (var g in m.Goods) { double b; if (g == null || !s.Bef.TryGetValue(g, out b) || !(b > 0)) continue; double rg; if (!r.TryGetValue(g, out rg)) rg = 1.0; double val = b * Math.Max(1, g.Value); wb += val; wr += val / rg; }
                        if (wb > 0 && wr > 0) n[c] = Math.Max(NMin, Math.Min(NMax, n[c] * wb / wr));
                    }
                if (n != null) _n = n;
            }
            notes.Sort((x, y) => y.Key.CompareTo(x.Key));
            var sb = new StringBuilder();
            for (int i = 0; i < notes.Count && i < 12; i++) { if (sb.Length > 0) sb.Append(", "); sb.Append(notes[i].Value); }
            return "towarow poprawionych " + changed + (changed > 0 ? " (najwieksze: " + sb + ")" : " (wszystkie w 0.1%)") + ", przycietych " + clip + ", bez czesci z n " + noScaled + ", bez n na towar " + noN
                   + "; tozsamosc: po = bez n + skalowana n towaru (dokladnie liniowe), przewidywana odchylka po poprawce " + (100 * Math.Abs(worstP)).ToString("0.000", Inv) + "% w " + worstPG;
        }

        /// <summary>f5: plany wszystkich okregow poza KS od nowa (po poprawce n) i publikacja - ta sama budowa co Daily. Zwraca liczbe planow.</summary>
        private static int RebuildPlansNow()
        {
            var plans = new Dictionary<Village, Plan>();
            var parts = new List<VillageBook.PartView>();
            foreach (var di in _dist.Values)
            {
                try
                {
                    if (di.V == null || UnderKs(di.V)) continue;
                    di.Plan = BuildPlan(di, parts); di.HasPlan = true; di.Dirty = false; _dRedone++;
                    if (di.Plan != null) plans[di.V] = di.Plan;
                }
                catch (Exception e) { Stumble("MeasureNow(plan)", e); }
            }
            _plans = plans;
            return plans.Count;
        }

        /// <summary>f5-zajecia (klucz Side Village Craft Measure Now): Daily, doba wlaczenia, zaraz po opublikowaniu planow - pomiar pelny, poprawka n i pomiar po poprawce
        /// w tej samej dobie; linia "pomiar neutralnosci (A6, TESTS 2) doba S" (stan, z ktorym swiat produkuje dobe S). Zawsze konczy pomiar (bez pomiaru doby nastepnej).</summary>
        private static void MeasureNow(int today)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (!On) { Log.Info(P + "pomiar pelny (f5) pominiety - zajecia nieczynne (ksiega wiosek / lista BK / MaterialLaw)."); return; }
                var meta = MetaAll();
                var s0 = new MeasSum();
                lock (Gate) _meas.Clear();
                int c0 = AskModel(s0);
                Collect(s0, meta);
                var led = new Dictionary<string, double[]>(StringComparer.Ordinal);
                int ld = -1; try { ld = GoodsLedger.KsTake(led); } catch { ld = -1; }
                bool ledOk = ld == today - 1;
                double w0, ww0; string wt0, wwt0;
                TypeDevs(s0, meta, led, ledOk, null, out w0, out wt0, out ww0, out wwt0);
                double gw0; string gwt0; int go0; GoodDevs(s0, out gw0, out gwt0, out go0);
                // poprawka (klucz Side Village Craft Neutral Fix): n na towar - dokladna; bez n na towar - uklad liniowy f4b na zajeciach (ten sam FixFromMeasure)
                var sx = Settings.Current;
                string fixText; int changed = 0; bool pg = PerGoodOn;
                if (sx != null && sx.SideVillageCraftNeutralFix && s0.Vil > 0 && s0.Calls > 0 && _n != null)
                {
                    if (pg) fixText = "poprawka n z pomiaru (f5, pomiar pelny, n na towar): " + FixPerGood(s0, meta, out changed);
                    else fixText = "poprawka n z pomiaru (f5, pomiar pelny - uklad liniowy f4b na zajeciach): " + FixFromMeasure(s0.Bef, s0.Aft, s0.Unsc, s0.Scl, meta, out changed);
                }
                else fixText = "poprawka n z pomiaru (f5): " + (sx != null && sx.SideVillageCraftNeutralFix ? "bez pomiaru (wsi 0)" : "WYLACZONA (Side Village Craft Neutral Fix)");
                var fin = s0; int c1 = 0, plans1 = 0;
                if (changed > 0)
                {
                    plans1 = RebuildPlansNow();
                    var s1 = new MeasSum();
                    lock (Gate) _meas.Clear();
                    c1 = AskModel(s1);
                    Collect(s1, meta);
                    fin = s1;
                }
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                var sb = new StringBuilder(4000);
                sb.Append(P).Append("pomiar neutralnosci (A6, TESTS 2) doba ").Append(today).Append(" | wsi zmierzonych ").Append(fin.Vil).Append(", wywolan ").Append(fin.Calls)
                  .Append(" (bez konca lancucha ").Append(fin.NoFinal).Append(", bez mnoznika ").Append(fin.NoMult).Append("), okregi z pracami pominiete ").Append(fin.Works)
                  .Append(" | zmiana produkcji typu we wsiach z planem (wartosc, przed z W2 dnia wczesniej -> po; W2 zboze -> ryby ").Append(fin.W2g.ToString("0", Inv)).Append(" szt.):");
                double w1, ww1; string wt1, wwt1;
                TypeDevs(fin, meta, led, ledOk, sb, out w1, out wt1, out ww1, out wwt1);
                double gw1; string gwt1; int go1; string gtext = GoodDevs(fin, out gw1, out gwt1, out go1);
                sb.Append(" | najwieksza odchylka produkcji typu ").Append((100 * Math.Abs(w1)).ToString("0.000", Inv)).Append("% (prog 1%) w ").Append(wt1)
                  .Append(" | wobec swiata (ksiega towarow doby ").Append(ledOk ? (today - 1).ToString(Inv) : "BRAK").Append("): najwieksza ").Append((100 * Math.Abs(ww1)).ToString("0.000", Inv)).Append("% w ").Append(wwt1)
                  .Append(" | towar: najwieksza zmiana ").Append((100 * gw1).ToString("+0.0;-0.0;0.0", Inv)).Append("% (").Append(gwt1).Append(")")
                  .Append(" | towary (f5): najwieksza odchylka ").Append((100 * Math.Abs(gw1)).ToString("0.000", Inv)).Append("% w ").Append(gwt1).Append(", ").Append(gtext)
                  .Append(" | W2 (zboze -> ryby) dnia wczesniej w 'przed' (recenzja calosci) - zajecia go zastepuja")
                  .Append(" | pomiar pelny w dobie wlaczenia (f5, Side Village Craft Measure Now): wywolan modelu ").Append(c0).Append(" + ").Append(c1).Append(" (wsi pytanych ").Append(s0.AskVil)
                  .Append("), wsi poza pomiarem 0, n ").Append(pg ? "na towar" : "zajecia").Append(", plany od nowa ").Append(plans1)
                  .Append(", przed poprawka: najwieksza odchylka produkcji typu ").Append((100 * Math.Abs(w0)).ToString("0.000", Inv)).Append("% w ").Append(wt0)
                  .Append(", towar ").Append((100 * Math.Abs(gw0)).ToString("0.000", Inv)).Append("% w ").Append(gwt0).Append(" (ponad 1%: ").Append(go0).Append(")")
                  .Append(", koszt ").Append(ms.ToString("0.0", Inv)).Append(" ms")
                  .Append(" | ").Append(fixText).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("MeasureNow", e); }
            finally
            {
                _measNowDay = -1; _measDay = -1; _measRound = 0;
                lock (Gate) { _meas.Clear(); _measW2 = null; }
            }
        }

        private static int OwnerByMeta(ItemObject it, CraftMeta[] meta)
        {
            if (it == null || meta == null) return -1;
            int first = -1;
            for (int c = 0; c < meta.Length; c++)
            {
                var m = meta[c];
                if (m == null || m.Type == null || m.Horse || _typeUsed == null || c >= _typeUsed.Length || !_typeUsed[c]) continue;
                bool has = false; foreach (var g in m.Goods) if (ReferenceEquals(g, it)) { has = true; break; }
                if (!has) continue;
                if (ReferenceEquals(m.Icon, it)) return c;
                if (first < 0) first = c;
            }
            return first;
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
                foreach (var kv in pl.Add) if (!pl.TypeGoods.Contains(kv.Key) && !CropBlocked(v, kv.Key)) x += M * kv.Value;   // f5: bez papirusu zabronionego filtrem klimatu
                return (int)Math.Ceiling(5.0 * x - 1e-4);
            }
            catch (Exception e) { Stumble("CapAdd", e); return 0; }
        }

        /// <summary>M wsi (wynik towaru typu na jednostke stawki typu) - SZACUNEK: (poziom + 1) x 0.5 gry x czynnik ludzi P1 x zima x rece E4/5 i E4/7 (bez mnoznikow
        /// konkretnego towaru: kopalnie, powtorzenia BK, regulator). recenzja: dawna "pamiec M" nie byla nigdy zapisywana (martwa droga) - zdjeta; linia mowi "M szacunek".</summary>
        internal static float MOf(Village v)
        {
            if (v == null) return 0f;
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
            // f4b-inwest (District Icon Part Crafts, tylko pod KS): wlasne zajecie czesci i wartosc dnia jego towarow sprzedanych (sztuki przelicznikiem wozow KS x cena miasta targowego);
            // IconVal < 0 - nie liczone (klucz wylaczony albo droga bez KS - tam CraftVal to juz wlasne zajecie czesci)
            public int IconCraft = -1; public double IconVal = -1;
            // f5-zajecia (District Icon Craft Value, tylko pod KS): wartosc wedlug reguly f4b (sprzedaz KS) zachowana do porownania w linii ikon; -1 - nie liczona
            public double IconValKs = -1;
        }

        internal static bool CraftIconKey { get { var s = Settings.Current; return s != null && s.DistrictIconCraftValue; } }

        /// <summary>f5-zajecia (District Icon Craft Value): wartosc dnia czesci okregu wedlug F17 projektu (A5 x cena) - udzial ludzi w domu x M wsi x g(zajecie)/g(typ)
        /// x n towaru x stawka typu zajecia x poprawka stawki x cena miasta targowego; ta sama regula co droga bez KS (PartsOf ponizej), ale zajecie czesci to zajecie jej
        /// ziemi (wies gry - typ okregu), nie rece bloku KS. Towar zabroniony filtrem klimatu upraw (papirus poza goracym poludniem) - bez wartosci. n tylko po wlaczeniu
        /// zajec (bez - 1). Sam odczyt, nie rzuca.</summary>
        private static double F17Value(Village v, int c, int D, double share, float M, float gD, Town town, CraftMeta[] meta)
        {
            try
            {
                var m = meta != null && c >= 0 && c < meta.Length ? meta[c] : null;
                if (m == null || m.Type == null || !(share > 0)) return 0.0;
                bool nOn = _n != null;
                double k0 = share * M * (c == D ? 1.0 : VillageByPeople.GOf(m.Type) / Math.Max(1e-3f, gD));
                double val = 0;
                for (int i = 0; i < m.Goods.Length; i++)
                {
                    var it = m.Goods[i];
                    if (it == null || (c == D ? MineralOnce.CropBlocks(v, it) : CropBlocked(v, it))) continue;
                    val += k0 * (nOn ? NGc(c, it) : 1.0) * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, it.StringId) * Price(town, it);
                }
                return val > 0 && !double.IsNaN(val) && !double.IsInfinity(val) ? val : 0.0;
            }
            catch (Exception e) { Stumble("F17Value", e); return 0.0; }
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
                    bool f17 = CraftIconKey;                                                  // f5-zajecia: ikona pod KS wedlug F17 projektu
                    int D17 = f17 ? CraftOfType(v.VillageType) : -1;
                    float M17 = f17 ? MOf(v) : 0f, gD17 = f17 ? VillageByPeople.GOf(v.VillageType) : 1f;
                    if (f17 && !_built && KeyOn) lock (Gate) { if (_typeUsed == null) TypesUsed(); Build(); }   // zajecia wiosek z pliku ziemi (jak droga bez KS ponizej)
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
                            // f4b-inwest (District Icon Part Crafts): wlasne zajecie czesci - to, co z niego sprzedaje, przy cenie miasta targowego za sztuke (sztuki przelicznikiem wozow KS)
                            if (kp.IconKg != null)
                            {
                                pc.IconCraft = kp.IconCraft; double iv = 0;
                                for (int i = 0; i < kp.IconKg.Length && i < items.Length; i++)
                                {
                                    if (!(kp.IconKg[i] > 0) || items[i] == null) continue;
                                    iv += fnow * kp.IconKg[i] / (sigma * StartCalibration.ItemKgOf(i)) * Price(town, items[i]);
                                }
                                pc.IconVal = iv;
                            }
                        }
                        else if (p.Main)
                        {
                            // f4b: wies gry bez blokow KS w rachunku - wlasne zajecie (typ okregu) bez sprzedazy
                            var s1 = Settings.Current;
                            if (s1 != null && s1.DistrictIconPartCrafts) { pc.IconCraft = CraftOfType(v.VillageType); pc.IconVal = 0; }
                        }
                        if (f17)
                        {
                            // f5-zajecia (District Icon Craft Value, fin88: NZW-3 177 pod KS, z tego 139 przy sprzedazy typu 0): ikona pod KS wedlug F17 projektu - zajecie ziemi
                            // czesci (wies gry - typ okregu; wioska - zajecie drogi bez KS, bez niego zajecie startowe bloku z pliku ziemi, bez niego typ okregu) x ludzie w domu
                            // x stawki zajecia x cena miasta targowego. Rece bloku KS (chleb dla siebie, drewno na opal) ikony nie zmieniaja. Wartosc f4b zostaje do porownania.
                            pc.IconValKs = pc.IconVal;
                            int c17 = p.Main ? D17 : (kp != null && kp.IconCraft >= 0 ? kp.IconCraft : SideCraft(p.Uid));
                            if (c17 < 0) c17 = D17;
                            pc.IconCraft = c17;
                            pc.IconVal = F17Value(v, c17, D17, tot > 0 ? p.Home / tot : 0.0, M17, gD17, town, meta);
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
                bool pgv = on && PerGoodOn;                                   // f5-zajecia: wycena czesci z n na towar (ten sam towar co w planie)
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
                            double k0 = pgv ? pc.Share * x * M * (c == D ? 1.0 : VillageByPeople.GOf(m.Type) / Math.Max(1e-3f, gD)) : 0.0;
                            double val = 0;
                            for (int i = 0; i < m.Goods.Length; i++)
                            {
                                double pcs = pgv ? k0 * NGc(c, m.Goods[i]) * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, m.Goods[i].StringId)
                                                 : k * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, m.Goods[i].StringId);
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
                // recenzja (prace bez zajec): plan z samych prac okregu - premia prac w towarach typu wsi okregow z pracami poza KS; bez prac - pusto (jak dotad)
                var wp = WorksOnlyPlans();
                if (wp.Count > 0 || _plans.Count > 0) _plans = wp;
                if (_measDay >= 0) { _measDay = -1; lock (Gate) _meas.Clear(); }
                if (!_offLogged) { _offLogged = true; Log.Info(P + "WYLACZONE w MCM (Side Village Crafts) - lista, model i magazyn wsi jak dotad, W2 (210) wedlug swojego klucza; stan w zapisie zamrozony; prace okregu bez KS - plan z samych prac (" + _worksOnly + " wsi)."); }
                return;
            }
            _offLogged = false;
            if (!VillageBook.Active) { if (_plans.Count > 0) _plans = new Dictionary<Village, Plan>(); return; }
            lock (Gate)
            {
                if (_typeUsed == null) TypesUsed();
                if (_pending != null) { string p = _pending; _pending = null; ImportNow(p); }
                if (_ngPending != null) { string g = _ngPending; _ngPending = null; ImportGoodsNow(g); }   // f5-zajecia: n na towar z zapisu (po arm_crafts)
                Build();
            }
            if (_n == null)
            {
                // recenzja (W2 / plik ziemi): neutralny start dopiero z wierszami ziemi wiosek - bez pliku arm_ks_land.tsv wszystkie wioski = typ okregu i n = 1 zapisane
                // na zawsze zrobilyby swiat nieneutralnym, gdy plik przyjdzie z nastepnym wgraniem. Bez n zajecia = typ okregu (plan tylko z prac), W2 wedlug swojego klucza.
                if (_builtSide - _builtNoRow > 0) SwitchOn(today);
                else if (!_deferLogged)
                {
                    _deferLogged = true;
                    Log.Info(P + "neutralny start odlozony | wiosek pobocznych " + _builtSide + ", z wierszem ziemi KS 0 (brak arm_ks_land.tsv albo okregow w pliku) - zajecia = typ okregu, W2 (210) wedlug swojego klucza; start w pierwszej dobie z plikiem ziemi.");
                }
            }
            if (_measDay >= 0 && today > _measDay) Measure(_measDay, today);    // recenzja: pomiar neutralnosci na towarach (doba po wlaczeniu); f4b: poprawka n i pomiar po poprawce
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
            // recenzja: zajecia nieczynne (lista BK albo MaterialLaw) - plan zajec nie liczy w modelu; prace okregu bez KS dostaja plan z samych prac
            _plans = On ? plans : WorksOnlyPlans();
            if (On) _worksOnly = 0;
            // f5-zajecia (Side Village Craft Measure Now): doba wlaczenia - pomiar pelny, poprawka i pomiar po poprawce teraz (przed tickami produkcji tej doby)
            if (_measNowDay >= 0 && _measNowDay == today) MeasureNow(today);
            else if (_measNowDay >= 0 && _measNowDay != today) { _measNowDay = -1; }
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
            // recenzja calosci (noc 17/18.10): W2 (HamletKinds) dnia wczesniej zamienial u x zboze wsi na tyle samo sztuk ryb (takze zboze kuchenne typow niezbozowych); od tej
            // doby milknie we wsiach z planem zajec (ReplacesW2), wiec "przed" ma ten dzien z W2 - inaczej swiat wraca do stanu bez W2 (ryb mniej, zboza wiecej). Sztuki W2
            // jako rece tego okregu (ten sam wzor co dopisek zajec w BuildPlan: udzial x g(zajecie)/g(typ) x stawka): ryby - rece rybakow u x tot x stawka zboza typu x g(typ)
            // / (stawka ryb rybakow x g(rybacy)), dopisane do "przed" rybakow; zboze - rece zboza u x tot x stawka zboza typu x g(typ) / (stawka zboza wsi zbozowej x g(zboze)),
            // zdjete z "przed" zboza (okreg zbozowy: dokladnie u x tot; zboze kuchenne typu niezbozowego zostaje w jego wsiach - KitchenKeep - wiec mniej rak zboza gdzie
            // indziej trzyma swiat zboza). Pomiar neutralnosci doby nastepnej liczy "przed" z tym samym W2 (_measW2).
            var fm = Meta(KsTab.CFisher); var wm = Meta(KsTab.CWheat);
            double fishR = 0, wheatR = 0;
            if (fm != null && fm.Type != null)
                for (int i = 0; i < fm.Goods.Length; i++) if (fm.Goods[i] != null && fm.Goods[i].StringId == "fish") { fishR = fm.Rates[i]; break; }
            if (wm != null && wm.Type != null)
                for (int i = 0; i < wm.Goods.Length; i++) if (wm.Goods[i] != null && wm.Goods[i].StringId == "grain") { wheatR = wm.Rates[i]; break; }
            float gFish = fm != null && fm.Type != null ? VillageByPeople.GOf(fm.Type) : 0f;
            float gWheat = wm != null && wm.Type != null ? VillageByPeople.GOf(wm.Type) : 0f;
            bool fishOk = fishR > 0 && gFish > 1e-3f && _typeUsed != null && KsTab.CFisher < _typeUsed.Length && _typeUsed[KsTab.CFisher];
            bool wheatOk = wheatR > 0 && gWheat > 1e-3f && _typeUsed != null && KsTab.CWheat < _typeUsed.Length && _typeUsed[KsTab.CWheat];
            int w2n = 0, w2k = 0; double w2Fish = 0, w2Grain = 0;
            var measW2 = new Dictionary<Village, double>(new RefEq());
            foreach (var di in _dist.Values)
            {
                try
                {
                    if (di.V == null || di.TypeCraft < 0 || UnderKs(di.V) || !VillageBook.PartsOf(di.V, parts)) continue;
                    double tot = 0; foreach (var p in parts) tot += p.Home;
                    if (!(tot > 0)) continue;
                    nd++;
                    before[di.TypeCraft] += tot;
                    double u = HamletKinds.ShareOf(di.V);
                    if (u > 0 && fishOk && wheatOk)
                    {
                        double gr = 0;
                        foreach (var pr in di.Type.Productions) if (pr.Item1 != null && pr.Item1.StringId == "grain" && pr.Item2 > 0f) gr += pr.Item2;
                        if (gr > 0)
                        {
                            double pcs = u * tot * gr * VillageByPeople.GOf(di.Type);          // sztuki W2 (na jednostke poziomu i mnoznikow lancucha) x udzial rak
                            w2Fish += pcs / (fishR * gFish);
                            w2Grain += di.TypeCraft == KsTab.CWheat ? u * tot : pcs / (wheatR * gWheat);
                            w2n++; if (di.TypeCraft != KsTab.CWheat) w2k++;
                            measW2[di.V] = u;
                        }
                    }
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
            if (w2n > 0) { before[KsTab.CFisher] += w2Fish; before[KsTab.CWheat] = Math.Max(0.0, before[KsTab.CWheat] - w2Grain); }
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
            // f4b-inwest (fin87: NZW-1 bawelna +27.6%, daktyle -12%, oliwki +7.4%): n z rak zaklada ten sam plon na reke wszedzie, a lancuch modelu wsi daje towarowi typu
            // budowle, perki i cechy kultury warowni (gra: tylko towarom z listy typu), rece za cena E3-D1 (tylko "inne plony" wsi) i rozne P1 / rece / spustoszenie w roznych
            // okregach - dopisek zajec w obcym okregu dostaje inne mnozniki niz towar we wsi swojego typu. n na towarach: ta sama suma wartosci kazdego zajecia przed i po.
            double[] nHands = n; string goodsNote = null; bool goods = false;
            var s0 = Settings.Current;
            Dictionary<ItemObject, double> ngood = null;                  // f5-zajecia: n na towar (klucz Side Village Craft Neutral Per Good)
            if (s0 != null && s0.SideVillageCraftNeutralGoods)
            {
                double[] ng = null;
                try { ng = NeutralGoods(out goodsNote, out ngood); }
                catch (Exception e) { Stumble("NeutralGoods", e); ng = null; ngood = null; goodsNote = "BLAD rachunku - n z rak"; }
                if (ng != null) { n = ng; goods = true; }
            }
            else goodsNote = "WYLACZONE (Side Village Craft Neutral Goods) - n z rak";
            _n = n; _onDay = today;
            if (goods && ngood != null && PerGoodKey) _ng = ngood;
            _measDay = today; _measRound = 0; lock (Gate) { _meas.Clear(); _measW2 = measW2; }   // recenzja: pomiar na towarach w tej dobie (linia "pomiar neutralnosci" doby nastepnej)
            if (MeasureNowKey)
            {
                // f5-zajecia (klucz Side Village Craft Measure Now): pomiar pelny w TEJ dobie, zaraz po planach (Daily) - wszystkie wsie z planem, wszystkie towary; plany
                // wszystkich okregow od nowa dzis (takze okregi z planem z wczesniejszych dob sesji - neutralny start odlozony)
                _measNowDay = today;
                foreach (var di in _dist.Values) di.Dirty = true;
            }
            _w2Note = " | W2 dnia wczesniej w 'przed' (recenzja calosci): okregow " + w2n + ", rece rybakow +" + Math.Round(w2Fish).ToString("0", Inv) + ", rece zboza -" + Math.Round(w2Grain).ToString("0", Inv)
                      + " (w tym okregi typu niezbozowego - zboze kuchenne " + w2k + ")" + (fishOk && wheatOk ? "" : " - BRAK typu rybakow albo zboza w swiecie, W2 poza 'przed'");
            var sb = new StringBuilder(1600);
            sb.Append(P).Append("start neutralny (A6) doba ").Append(today).Append(" | okregow ").Append(nd).Append(goods ? " | n(typ) na towarach (w nawiasie z rak: przed->po):" : " | n(typ) = rece swiata przed / po:");
            bool first = true;
            for (int c = 0; c < KsTab.NC; c++)
            {
                if (!(before[c] > 0) && !(after[c] > 0)) continue;
                sb.Append(first ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(n[c].ToString("0.000", Inv)).Append(" (");
                if (goods) sb.Append(nHands[c].ToString("0.000", Inv)).Append(": ");
                sb.Append(Math.Round(before[c])).Append("->").Append(Math.Round(after[c])).Append(')');
                first = false;
            }
            // rece swiata typu po x n / przed - z definicji n rowne (poza przycietymi); recenzja: to NIE jest kontrola produkcji - tylko INFO. Kontrola na towarach (TESTS 2):
            // linia "pomiar neutralnosci (A6, TESTS 2)" doby nastepnej (wynik modelu kazdej wsi z planem przed i po kroku zajec, na koncu lancucha)
            double worst = 0;
            for (int c = 0; c < KsTab.NC; c++) if (before[c] > 0 && after[c] > 0) worst = Math.Max(worst, Math.Abs(after[c] * n[c] / before[c] - 1.0));
            sb.Append(_w2Note);
            sb.Append(" | rece swiata typu po x n / przed (").Append(goods ? "n na towarach - rece nie musza sie zgadzac" : "z definicji n, tylko przyciete").Append("): najwieksza odchylka ").Append((100 * worst).ToString("0.000", Inv)).Append("% (INFO; produkcja - linia pomiaru doby nastepnej)")
              .Append(" | n przyciete do ").Append(NMin.ToString("0.0", Inv)).Append('-').Append(NMax.ToString("0.0", Inv)).Append(": ").Append(clamped.Count > 0 ? string.Join(", ", clamped.ToArray()) : "zadne")
              .Append(" | n na towarach (f4b, Side Village Craft Neutral Goods): ").Append(goodsNote ?? "-");
            // f5-zajecia: n na towar, pomiar pelny, filtr klimatu dopiskow (odcinki tylko przy ktoryms kluczu f5 - wszystkie wylaczone: linia jak dotad)
            if (PerGoodKey || MeasureNowKey || CropKey)
                sb.Append(" | n na towar (f5, Side Village Craft Neutral Per Good): ").Append(_ng != null && PerGoodKey ? _ngNote : (PerGoodKey ? "BRAK liczb (rachunek na towarach wylaczony albo blad) - n zajecia" : "WYLACZONE - n zajecia"))
                  .Append(" | pomiar pelny (f5, Side Village Craft Measure Now): ").Append(MeasureNowKey ? "w tej dobie, zaraz po planach" : "WYLACZONY - pomiar doby nastepnej (ticki gry)")
                  .Append(" | filtr klimatu dopiskow (f5, Side Village Craft Crop Filter): ").Append(CropKey ? "TAK" : "NIE");
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ f4b-inwest: neutralny start NA TOWARACH (klucz Side Village Craft Neutral Goods)
        private static Dictionary<ItemObject, int> _owner;   // towar -> zajecie z najwieksza produkcja "przed" (poprawka z pomiaru liczy zajecie po jego wlasnych towarach)
        private static int _measRound;                       // 0 - pomiar doby wlaczenia (NZW-1), 1 - pomiar po poprawce (Side Village Craft Neutral Fix)

        /// <summary>Wynik modelu wsi gry dla (wies, towar) BEZ naszego postfiksu (MaterialLaw.ProbeBegin - wolajacy): [wynik, suma czynnikow] - to, co krok zajec
        /// (VillageCrafts.Base) widzi na wejsciu. Pamiec w obrebie okregu.</summary>
        private static float[] Probe(TaleWorlds.CampaignSystem.ComponentInterfaces.VillageProductionCalculatorModel model, Village v, ItemObject it, Dictionary<ItemObject, float[]> cache, ref int probes)
        {
            float[] a;
            if (cache.TryGetValue(it, out a)) return a;
            a = new float[2];
            try
            {
                var r = model.CalculateDailyProductionAmount(v, it);
                a[0] = r.ResultNumber; a[1] = r.SumOfFactors;
                if (float.IsNaN(a[0]) || float.IsInfinity(a[0])) a[0] = 0f;
                if (float.IsNaN(a[1]) || float.IsInfinity(a[1])) a[1] = 0f;
            }
            catch (Exception e) { Stumble("Probe", e); a[0] = 0f; a[1] = 0f; }
            probes++;
            cache[it] = a;
            return a;
        }

        /// <summary>f5-zajecia: dopisanie sztuk towaru do slownika (NeutralGoods - n na towar).</summary>
        private static void AccG(Dictionary<ItemObject, double> d, ItemObject it, double q)
        {
            if (d == null || it == null || !(q > 0) || double.IsNaN(q) || double.IsInfinity(q)) return;
            double c; d.TryGetValue(it, out c); d[it] = c + q;
        }

        /// <summary>f5-zajecia: kazdy towar typu D (bez zboza kuchennego - CraftMeta.Goods) ma n na towar rowne 1 (okreg bez innych zajec nie dostanie planu - W2 zostaje).</summary>
        private static bool AllOne(Dictionary<ItemObject, double> ng, CraftMeta[] meta, int D)
        {
            var m = meta != null && D >= 0 && D < meta.Length ? meta[D] : null;
            if (m == null || m.Goods == null) return true;
            foreach (var g in m.Goods) { double x; if (g != null && ng.TryGetValue(g, out x) && Math.Abs(x - 1.0) >= 1e-4) return false; }
            return true;
        }

        private static bool TypeHas(VillageType t, ItemObject it)
        {
            if (t == null || it == null) return false;
            foreach (var pr in t.Productions) if (ReferenceEquals(pr.Item1, it) && pr.Item2 > 0f) return true;
            return false;
        }

        /// <summary>f4b-inwest (fin87 NZW-1): n(T) neutralnego startu NA TOWARACH. Dla kazdego okregu poza KS (z ludzmi w domu): towary typu okregu (lista typu bez zboza
        /// kuchennego) = wynik modelu gry bez naszego postfiksu (te same budowle, perki, cechy kultury i zima, ktore krok zajec widzi) x mnoznik lancucha za krokiem zajec
        /// (MaterialLaw.ChainFactor: P1, rece, spustoszenie, regulator, stawki typu, ZC1); po wlaczeniu x (1 - udzial innych zajec) x n(typ). Dopiski zajec w okregu: dokladnie
        /// wzor BuildPlan (udzial x g(zajecie)/g(typ) x stawka zajecia x poprawka stawki x poziom, n = 1, bez prac) x (1 + suma czynnikow gry tego towaru w tej wsi - towar
        /// spoza listy typu dostaje od gry tylko czynniki bez bazy, np. polityki NavalDLC; towar z listy typu - jak towar typu) x zima (klucz Side Village Craft Adds Winter)
        /// x ten sam mnoznik lancucha wsi gospodarza. n(T) = wartosc przed / wartosc po przy n = 1 (wartosc = szt. x Value, jak pomiar neutralnosci). W2 dnia wczesniej:
        /// zboze swiata x (1 - u) i ryby + u x zboze (ten sam W2 co pomiar), w okregach, ktore dostana plan (okreg bez innych zajec i z n typu 1 - W2 zostaje, poza rachunkiem).
        /// Wsie nie Normal / bez targu - krok zajec ich nie rusza (wynik gry 0, bez dopiskow). null - brak modelu (n z rak). Watek glowny, raz na kampanie.</summary>
        private static double[] NeutralGoods(out string note, out Dictionary<ItemObject, double> ngOut)
        {
            note = ""; ngOut = null;
            // f5-zajecia: n na towar (klucz Side Village Craft Neutral Per Good) - te same sztuki towaru przed i po, osobno dla kazdego towaru (sztuki, nie wartosc);
            // przy nim sufit Skaly / zlota Zachodu (RockGold) w rachunku, przy filtrze dopiskow (Side Village Craft Crop Filter) - filtr klimatu upraw (papirus). Klucze
            // wylaczone = rachunek jak w f4b.
            bool pgk = PerGoodKey, cropK = CropKey, rockK = pgk;
            var tgtG = pgk ? new Dictionary<ItemObject, double>() : null;      // towar -> sztuki "przed" (wsie typu, bez zboza kuchennego)
            var aftTG = pgk ? new Dictionary<ItemObject, double>() : null;     // towar -> sztuki towaru typu "po" przy n = 1 (x (1 - inne))
            var aftAG = pgk ? new Dictionary<ItemObject, double>() : null;     // towar -> sztuki dopiskow "po" przy n = 1
            int blockedCrop = 0, blockedRock = 0;
            var meta = MetaAll();
            TaleWorlds.CampaignSystem.ComponentInterfaces.VillageProductionCalculatorModel model = null;
            try { model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.VillageProductionCalculatorModel : null; } catch { model = null; }
            if (model == null || meta == null) { note = "BRAK modelu produkcji wsi - n z rak"; return null; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var s = Settings.Current;
            bool winterAdd = s != null && s.SideVillageCraftAddsWinter;
            ItemObject grainIt = null, fishIt = null;
            try { grainIt = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("grain"); fishIt = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("fish"); } catch { }
            bool w2ok = grainIt != null && fishIt != null && _typeUsed != null && KsTab.CFisher < _typeUsed.Length && KsTab.CWheat < _typeUsed.Length && _typeUsed[KsTab.CFisher] && _typeUsed[KsTab.CWheat];
            var tgt = new double[KsTab.NC]; var aft = new double[KsTab.NC];
            var own = new Dictionary<ItemObject, double[]>();          // towar -> wartosc "przed" wedlug zajecia (wlasciciel towaru do poprawki z pomiaru)
            var parts = new List<VillageBook.PartView>();
            var addBy = new Dictionary<int, double>();
            var cache = new Dictionary<ItemObject, float[]>();
            var bySide = new Dictionary<string, Side>(StringComparer.Ordinal);
            var w2d = new List<double[]>();                              // [typ okregu, udzial innych, zboze W2 (wartosc), ryby W2 (wartosc)]
            int nd = 0, probes = 0, w2n = 0, hosts = 0, adds = 0; double addV = 0, natV = 0;
            MaterialLaw.ProbeBegin();
            try
            {
                foreach (var di in _dist.Values)
                {
                    try
                    {
                        if (di.V == null || di.TypeCraft < 0 || UnderKs(di.V) || !VillageBook.PartsOf(di.V, parts)) continue;
                        double tot = 0; foreach (var p in parts) tot += p.Home;
                        if (!(tot > 0)) continue;
                        var v = di.V; int D = di.TypeCraft; var mD = meta[D];
                        if (mD == null || mD.Type == null) continue;
                        nd++;
                        float gD = VillageByPeople.GOf(di.Type);
                        // wejscie planu jak BuildPlan (N = 1, bez prac okregu): udzial innych zajec i dopiski wedlug zajecia
                        double other = 0; addBy.Clear(); bySide.Clear();
                        foreach (var sd in di.Sides) bySide[sd.Uid] = sd;
                        foreach (var p in parts)
                        {
                            if (p.Main || p.Uid == null) continue;
                            double sh = p.Home / tot;
                            if (!(sh > 0)) continue;
                            Side sd; if (!bySide.TryGetValue(p.Uid, out sd)) continue;
                            CraftShares(sd, (c, x) =>
                            {
                                if (c == D) return;
                                double part = sh * x;
                                other += part;
                                var m = c >= 0 && c < meta.Length ? meta[c] : null;
                                if (m == null || m.Type == null) return;
                                double cur; addBy.TryGetValue(c, out cur);
                                addBy[c] = cur + part * (VillageByPeople.GOf(m.Type) / Math.Max(1e-3f, gD));
                            });
                        }
                        other = Math.Min(1.0, other);
                        bool live = v.VillageState == Village.VillageStates.Normal && v.TradeBound != null;
                        cache.Clear();
                        // towary typu okregu: wynik gry x lancuch (przed) i x (1 - inne) (po, przy n = 1)
                        for (int i = 0; i < mD.Goods.Length; i++)
                        {
                            var it = mD.Goods[i];
                            if (it == null) continue;
                            var pr = Probe(model, v, it, cache, ref probes);
                            float side; float cf = MaterialLaw.ChainFactor(v, it, out side);
                            double q = (live ? Math.Max(0f, pr[0]) : 0.0) * cf * side, val = Math.Max(1, it.Value);
                            // f5: towar, ktorego wies nie rodzi mimo listy typu - papirus poza goracym poludniem (filtr klimatu zdejmuje go z listy BK), srebro i zloto Skaly
                            // i zloto Zachodu (sufit RockGold na koncu lancucha) - w rachunku 0 (przed i po)
                            if (q > 0 && cropK && MineralOnce.CropBlocks(v, it)) { q = 0; blockedCrop++; }
                            if (q > 0 && rockK && RockGold.Blocks(v, it.StringId)) { q = 0; blockedRock++; }
                            tgt[D] += q * val; aft[D] += q * (1.0 - other) * val; natV += q * val;
                            double[] ow; if (!own.TryGetValue(it, out ow)) { ow = new double[KsTab.NC]; own[it] = ow; }
                            ow[D] += q * val;
                            if (pgk) { AccG(tgtG, it, q); AccG(aftTG, it, q * (1.0 - other)); }
                        }
                        // W2 dnia wczesniej (ostatni krok lancucha przed ZC1): u x zboze tej wsi -> tyle samo sztuk ryb
                        double u = live && w2ok ? HamletKinds.ShareOf(v) : 0.0;
                        if (u > 0)
                        {
                            var pg = Probe(model, v, grainIt, cache, ref probes);
                            float sg; float cg = MaterialLaw.ChainFactor(v, grainIt, out sg);
                            double G = Math.Max(0f, pg[0]) * cg;
                            if (G > 0) { w2d.Add(new double[] { D, other, u * G * Math.Max(1, grainIt.Value), u * G * Math.Max(1, fishIt.Value), u * G }); w2n++; }   // f5: [4] - sztuki W2
                        }
                        // dopiski zajec innych wiosek w tej wsi (wies Normal z targiem - jak w kroku zajec)
                        if (live && other > 1e-6)
                        {
                            hosts++;
                            float lvl = (v.GetHearthLevel() + 1) * 0.5f;
                            float cut = winterAdd ? WinterBite.VillageCut(v) : 0f;
                            foreach (var kv in addBy)
                            {
                                var m = meta[kv.Key];
                                for (int i = 0; i < m.Goods.Length; i++)
                                {
                                    var it = m.Goods[i];
                                    if (it == null) continue;
                                    double A = kv.Value * m.Rates[i] * GoodsRates.TypeRateFactor(m.Type.StringId, it.StringId) * lvl;
                                    if (!(A > 0)) continue;
                                    if (cropK && MineralOnce.CropBlocks(v, it)) { blockedCrop++; continue; }   // f5: dopisek zabroniony filtrem klimatu (ListStep go nie dopisze)
                                    var pr = Probe(model, v, it, cache, ref probes);
                                    double fac;
                                    if (TypeHas(di.Type, it))
                                    {
                                        // towar z listy typu: dopisek A/F do bazy przy czynniku F - wynik + A x (1 + suma gry); F ~ 0 albo wynik 0 - dopisek bazy dokladnie A
                                        bool kitchen = di.Type.StringId != "wheat_farm" && it.StringId == "grain";
                                        fac = pr[0] > 0f && (kitchen || (1.0 - other) * NMin > 1e-3) ? 1.0 + pr[1] : 1.0;
                                    }
                                    else fac = 1.0 + pr[1] - cut;                // towar spoza listy typu: baza A x (1 + czynniki gry bez bazy - zima)
                                    if (!(fac > 0)) fac = 0;
                                    float side; float cf = MaterialLaw.ChainFactor(v, it, out side);
                                    double q = A * fac * cf * side, val = Math.Max(1, it.Value);
                                    if (q > 0 && rockK && RockGold.Blocks(v, it.StringId)) { q = 0; blockedRock++; }   // f5: sufit RockGold (srebro i zloto Skaly, zloto Zachodu)
                                    aft[kv.Key] += q * val; addV += q * val; adds++;
                                    if (pgk) AccG(aftAG, it, q);
                                }
                            }
                        }
                    }
                    catch (Exception e) { Stumble("NeutralGoods(okreg)", e); }
                }
            }
            finally { MaterialLaw.ProbeEnd(); }
            // n = przed / po; W2 tylko w okregach, ktore dostana plan (okreg bez innych zajec i z n typu 1 - BuildPlan da null, W2 zostaje po obu stronach)
            var n = new double[KsTab.NC];
            int w2used = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                var t = (double[])tgt.Clone();
                w2used = 0;
                if (w2ok)
                    foreach (var w in w2d)
                    {
                        int D = (int)w[0];
                        if (pass > 0 && w[1] <= 1e-6 && Math.Abs(n[D] - 1.0) < 1e-4) continue;
                        t[KsTab.CWheat] -= w[2]; t[KsTab.CFisher] += w[3]; w2used++;
                    }
                for (int c = 0; c < KsTab.NC; c++) n[c] = t[c] > 0 && aft[c] > 0 ? t[c] / aft[c] : 1.0;
            }
            var clamped = new List<string>();
            for (int c = 0; c < KsTab.NC; c++)
                if (n[c] < NMin || n[c] > NMax) { clamped.Add(KsTab.Crafts[c] + " " + n[c].ToString("0.00", Inv)); n[c] = Math.Max(NMin, Math.Min(NMax, n[c])); }
            // f5-zajecia: n na towar - te same dwa przejscia W2 co wyzej (W2 tylko w okregach, ktore dostana plan: okreg bez innych zajec, ktorego kazdy towar typu ma n 1,
            // zostaje przy W2 po obu stronach); W2: zboze - u x G sztuk, ryby + u x G sztuk (tyle samo sztuk - W2 to ostatni krok)
            if (pgk)
            {
                var ng = new Dictionary<ItemObject, double>();
                var keys = new HashSet<ItemObject>(tgtG.Keys); foreach (var k in aftTG.Keys) keys.Add(k); foreach (var k in aftAG.Keys) keys.Add(k);
                double vB = 0, vA = 0; int w2g = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    var t = new Dictionary<ItemObject, double>(tgtG);
                    w2g = 0;
                    if (w2ok)
                        foreach (var w in w2d)
                        {
                            int D = (int)w[0];
                            if (pass > 0 && w[1] <= 1e-6 && AllOne(ng, meta, D)) continue;
                            double gq; t.TryGetValue(grainIt, out gq); t[grainIt] = Math.Max(0.0, gq - w[4]);
                            double fq; t.TryGetValue(fishIt, out fq); t[fishIt] = fq + w[4];
                            keys.Add(grainIt); keys.Add(fishIt); w2g++;
                        }
                    ng.Clear(); vB = 0; vA = 0;
                    foreach (var it in keys)
                    {
                        double b, a1, a2; t.TryGetValue(it, out b); aftTG.TryGetValue(it, out a1); aftAG.TryGetValue(it, out a2);
                        double a = a1 + a2;
                        ng[it] = b > 0 && a > 0 ? b / a : 1.0;
                        double val = Math.Max(1, it.Value); vB += b * val; vA += a * ng[it] * val;
                    }
                }
                var cl = new List<string>();
                var items = new List<ItemObject>(ng.Keys);
                foreach (var it in items)
                {
                    double x = ng[it];
                    if (x < NgMin || x > NgMax) { cl.Add(it.StringId + " " + x.ToString("0.00", Inv)); ng[it] = Math.Max(NgMin, Math.Min(NgMax, x)); }
                }
                items.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
                var lst = new StringBuilder();
                foreach (var it in items) { if (lst.Length > 0) lst.Append(", "); lst.Append(it.StringId).Append(' ').Append(ng[it].ToString("0.000", Inv)); }
                ngOut = ng;
                // tozsamosc: dla kazdego towaru sztuki przed = sztuki po przy n na towar (z definicji, poza przycietymi) - suma wartosci w linii
                _ngNote = "towarow " + ng.Count + ": " + lst + " | wartosc dnia: przed " + vB.ToString("0", Inv) + ", po przy n na towar " + vA.ToString("0", Inv) + " (tozsamosc, poza przycietymi)"
                          + " | W2 w 'przed': okregow " + w2g + " | przyciete do " + NgMin.ToString("0.0", Inv) + "-" + NgMax.ToString("0.0", Inv) + ": " + (cl.Count > 0 ? string.Join(", ", cl.ToArray()) : "zadne")
                          + " | poza rachunkiem (0 przed i po): filtr klimatu upraw " + blockedCrop + ", sufit RockGold " + blockedRock;
            }
            var ownMap = new Dictionary<ItemObject, int>();
            foreach (var kv in own) { int b = -1; double bv = 0; for (int c = 0; c < KsTab.NC; c++) if (kv.Value[c] > bv) { bv = kv.Value[c]; b = c; } if (b >= 0) ownMap[kv.Key] = b; }
            _owner = ownMap;
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            // tozsamosc: dla kazdego zajecia wartosc przed = wartosc po x n (z definicji, poza przycietymi) - suma dopiskow i towarow typu w linii
            note = "okregow " + nd + ", wsi z dopiskami " + hosts + " (dopiskow " + adds + "), wywolan modelu bez postfiksu " + probes + ", koszt " + ms.ToString("0.0", Inv) + " ms"
                 + " | wartosc dnia: towary typu przed " + natV.ToString("0", Inv) + ", dopiski przy n = 1 " + addV.ToString("0", Inv)
                 + " | W2 dnia wczesniej: okregow " + w2used + " z " + w2n + (w2ok ? "" : " (BRAK typu rybakow albo zboza)")
                 + " | zima dopiskow " + (winterAdd ? "TAK" : "NIE") + " | przyciete: " + (clamped.Count > 0 ? string.Join(", ", clamped.ToArray()) : "zadne")
                 + " | poza rachunkiem: sufity dymarki i RockGold (zelazo, zloto), owoce E3-D1 dopisane poza lista typu";
            return n;
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
                if (F4On) { try { double h0; home.TryGetValue(s.Uid, out h0); F4ZwNote(s.Craft, bc, cur, best, sw, h0 * 0.5); } catch (Exception e) { Stumble("F4ZwNote", e); } }   // f4-pomiary: nacisk i przejscia (tylko liczniki; blad nie przerywa reguly)
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

        // ------------------------------------------------------------ f4-pomiary (noc 17/18.10, przeglad ekonomii): nacisk reguly ZW i przejscia - tylko liczniki do linii
        // Regula ZW porownuje przydatnosc ziemi x sqrt(indeks cen towarow zajecia) - bez ilosci, wartosci, kosztu i renty (inna niz A7 pod KS). Przeglad: NIE przepinac jej
        // na obecne Income z A7 (przenioslo by nacisk 92% na droge bez KS) - najpierw pomiar: ile wiosek przy progu i dokad chca (z rekami: polowa ludzi w domu).
        private static bool F4On { get { var s = Settings.Current; return s != null && s.MeasureCraftRule; } }
        private static int _f4N, _f4Press; private static double _f4Hands, _f4PressHands;
        private static readonly List<double> _f4Ratio = new List<double>();
        private static readonly Dictionary<int, double[]> _f4Tr = new Dictionary<int, double[]>();

        private static void F4ZwNote(int from, int to, double cur, double best, float sw, double hands)
        {
            _f4N++; _f4Hands += hands;
            if (to < 0) return;
            if (_f4Ratio.Count < 100000) _f4Ratio.Add(best / Math.Max(0.02, cur));
            if (best < sw * Math.Max(0.02, cur)) return;
            _f4Press++; _f4PressHands += hands;
            double[] t; int k = from * 64 + to;
            if (!_f4Tr.TryGetValue(k, out t)) { t = new double[2]; _f4Tr[k] = t; }
            t[0] += 1; t[1] += hands;
        }

        private static void F4ZwClear() { _f4N = _f4Press = 0; _f4Hands = _f4PressHands = 0; _f4Ratio.Clear(); _f4Tr.Clear(); }

        private static string F4ZwText()
        {
            if (!F4On) return "";
            var sb = new StringBuilder(600);
            var s = Settings.Current;
            float sw = Math.Max(1.0f, s != null ? s.SideVillageCraftSwitch : 1.25f);
            sb.Append(" | nacisk ZW (f4-pomiary, kroki od ostatniej linii): ocenionych ").Append(_f4N);
            if (_f4Ratio.Count > 0)
            {
                _f4Ratio.Sort();
                sb.Append(", iloraz najlepsze/max(0.02, biezace) med ").Append(_f4Ratio[(_f4Ratio.Count - 1) / 2].ToString("0.00", Inv)).Append(" q90 ").Append(_f4Ratio[(int)(0.9 * (_f4Ratio.Count - 1))].ToString("0.00", Inv));
            }
            sb.Append(", nad progiem x").Append(sw.ToString("0.00", Inv)).Append(' ').Append(_f4Press).Append(" wiosek (").Append(_f4N > 0 ? (100.0 * _f4Press / _f4N).ToString("0.0", Inv) : "-").Append("%), rak ")
              .Append(_f4PressHands.ToString("0", Inv)).Append(" (").Append(_f4Hands > 0 ? (100.0 * _f4PressHands / _f4Hands).ToString("0.0", Inv) : "-").Append("%)")
              .Append(", przejscia (top 10, wiosek/rak):");
            if (_f4Tr.Count == 0) sb.Append(" -");
            else
            {
                var l = new List<KeyValuePair<int, double[]>>(_f4Tr);
                l.Sort((x, y) => y.Value[0] != x.Value[0] ? y.Value[0].CompareTo(x.Value[0]) : y.Value[1].CompareTo(x.Value[1]));
                for (int i = 0; i < l.Count && i < 10; i++)
                {
                    int a = l[i].Key / 64, b = l[i].Key % 64;
                    sb.Append(i == 0 ? " " : "; ").Append(a >= 0 && a < KsTab.NC ? KsTab.Crafts[a] : "?").Append("->").Append(b >= 0 && b < KsTab.NC ? KsTab.Crafts[b] : "?")
                      .Append(' ').Append(l[i].Value[0].ToString("0", Inv)).Append('/').Append(l[i].Value[1].ToString("0", Inv));
                }
            }
            return sb.ToString();
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

        /// <summary>Doba przebudowy: zaplata raty (koszt do dzis wedlug czasu minus zaplacone, najwyzej 1/4 kiesy) - material z polki miasta targowego i rzemieslnicy (PayRebuild),
        /// postep najwyzej 1/T na dobe i nie ponad czesc zaplacona; koniec - nowe zajecie. Rok po terminie - porzucona (rece zostaja, gdzie doszly: zajecie przewazajace).</summary>
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
                // recenzja calosci (noc 17/18.10): rata = koszt do dzis wedlug czasu minus zaplacone - zaleglosci czekaja (material kupuje sie na sztuki, wiec drobna rata dnia
                // czeka, az starczy na sztuke), najwyzej 1/4 kiesy; placona prawdziwym materialem i rzemieslnikami (PayRebuild). Postep najwyzej 1/T na dobe i nigdy ponad
                // czesc zaplacona - doba bez zaplaty = przebudowa stoi (jak dotad), pierwsza doba z pelna zaplata = rata Cost/T (jak dotad)
                double fTime = Math.Min(1.0, Math.Max(0, today - s.TStart) / (double)T);
                double owe = Math.Min(s.Cost - s.Paid, fTime * s.Cost - s.Paid);
                int ask = owe >= 0.5 ? (int)Math.Min(Math.Ceiling(owe), Math.Max(0, di.V.Gold) / 4) : 0;
                long got = ask > 0 ? PayRebuild(di.V, mt, ask, _tally) : 0;
                s.Paid += got; _dPaid += got;
                double fPay = s.Paid >= s.Cost - 0.5f ? 1.0 : Math.Max(0.0, s.Paid / s.Cost);
                progress = Math.Max(0.0, Math.Min(1.0, (fPay - s.F) * T));
                if (progress < 1.0 - 1e-6) _dStall++;
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
            float addV = 0, remV = 0, bonV = 0; int mems = 0;
            lock (Gate)
            {
                foreach (var m in _mem.Values)
                {
                    if (m.Day != day && m.Day != day + 1) continue;
                    mems++;
                    foreach (var a in m.X.Values) { remV += a[0]; addV += a[1]; bonV += a[2]; }
                }
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
              .Append(F4ZwText())                                   // f4-pomiary (klucz Measure Craft Rule): nacisk ZW i przejscia "z -> na" (pusty napis przy kluczu wylaczonym)
              .Append(" | n(typ) z doby ").Append(_onDay).Append(':');
            f = true;
            var n = _n;
            if (n != null) for (int c = 0; c < KsTab.NC; c++) { if (Math.Abs(n[c] - 1.0) < 1e-4) continue; sb.Append(f ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(n[c].ToString("0.000", Inv)); f = false; }
            if (f) sb.Append(" 1");
            sb.Append(" | krok zajec (doba): wsi z planem ").Append(plans).Append(" (przeliczone dzis ").Append(_dRedone).Append(", co 7 dob na zmiane)").Append(", wsi z pamiecia ").Append(mems).Append(", listy BK z dopiskiem ").Append(_dLists).Append(", wywolan towaru typu ").Append(_dSteps)
              .Append(", dopiskow ").Append(_dAdded).Append(" (M szacunek: poziom x P1 x zima x rece) | wartosc bazowa: zdjete z typu ").Append(remV.ToString("0", Inv)).Append(", dodane zajec ").Append(addV.ToString("0", Inv))
              .Append(", premia prac w typie ").Append(bonV.ToString("0", Inv))
              .Append(" | kontrola pieniedzy: kiesy wsi -").Append(_tally.Vil).Append(" / kasy i lud miast +").Append(_tally.Town + _tally.Folk).Append(" (roznica ").Append(_tally.Vil - _tally.Town - _tally.Folk).Append(")")
              .Append(" - przebudowa (Ds Works Real Material): material z polki +").Append(_tally.Mat).Append(" (szt. ").Append(_tally.Pcs).Append("), rzemieslnicy miasta +").Append(_tally.Wage)
              .Append(", doby bez materialu albo kiesy ponad skrzynie ").Append(_tally.NoMat).Append(", droga dawna bez towaru (klucz wylaczony / kiesa ludu nieczynna) +").Append(_tally.Old)
              .Append(" | W2 (210) ").Append(On ? "wylaczony we wsiach z planem zajec (" + plans + "), gdzie indziej wedlug swojego klucza" : "wedlug swojego klucza")
              .Append(" | ikona: ").Append(DistrictIcon.DayText())
              .Append(" | koszt ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia dzis ").Append(_st).Append(" (od wczytania ").Append(_stAll).Append(").");
            Log.Info(sb.ToString());
            ZeroDay(); _notes.Clear(); _st = 0; _tally.Clear(); F4ZwClear();
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
                    if (_ngPending != null) { string g = _ngPending; _ngPending = null; ImportGoodsNow(g); }   // f5-zajecia
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
                    // recenzja calosci (noc 17/18.10): bez twardego sufitu - dawne "" przy > 512 KB kasowalo stan (przebudowy, n(typ)) zamiast go trzymac; SaveText.Sync dzieli
                    // dlugi napis na kawalki, wiec duzy zapis tylko nazwany w logu
                    if (res.Length > 512 * 1024) Log.Info(P + "zapis arm_crafts duzy (" + res.Length + " znakow) - zapisany w kawalkach (SaveText).");
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

        // ============================================================ f5-zajecia: zapis arm_crafts_g (n na towar)
        // zg1|towar=n|towar=n...  (StringId przedmiotu; n > 0). Osobny klucz: starsza DLL go nie zna (zostaje przy n zajecia z arm_crafts); brak klucza = n zajecia (jak dotad).
        private const string SaveVerG = "zg1";
        private const int SaveGMax = 64 * 1024;                     // straznik rozmiaru (ok. 50 towarow x 25 znakow = ok. 1.3 KB)

        /// <summary>Napis do zapisu albo null (bez n na towar - klucz nie jest zapisywany). Nie rzuca.</summary>
        internal static string ExportGoods()
        {
            try
            {
                lock (Gate)
                {
                    if (_ngPending != null) return _ngPending;               // zapis nierozwiazany (sesja bez doby) - oddany bez zmian
                    var ng = _ng;
                    if (ng == null || ng.Count == 0) return null;
                    var sb = new StringBuilder(2048);
                    sb.Append(SaveVerG);
                    foreach (var kv in ng)
                    {
                        if (kv.Key == null || kv.Key.StringId == null || !(kv.Value > 0) || double.IsInfinity(kv.Value)) continue;
                        sb.Append('|').Append(kv.Key.StringId.Replace('|', '/').Replace('=', '-')).Append('=').Append(kv.Value.ToString("R", Inv));
                    }
                    string res = sb.ToString();
                    if (res.Length > SaveGMax) { Log.Info(P + "zapis arm_crafts_g za duzy (" + res.Length + " znakow) - pominiety (n zajecia po wczytaniu)."); return null; }
                    return res;
                }
            }
            catch (Exception e) { Stumble("ExportGoods", e); return null; }
        }

        internal static void ImportGoods(string data) { lock (Gate) { _ngPending = string.IsNullOrEmpty(data) ? null : data; } }

        private static void ImportGoodsNow(string t)
        {
            try
            {
                var p = t.Split('|');
                if (p.Length < 2 || p[0] != SaveVerG) { _loadNote += " | arm_crafts_g: nieznany format - n zajecia"; _st++; return; }
                var ng = new Dictionary<ItemObject, double>();
                int bad = 0;
                var om = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                for (int i = 1; i < p.Length; i++)
                {
                    int q = p[i].IndexOf('='); double v;
                    if (q <= 0 || !double.TryParse(p[i].Substring(q + 1), NumberStyles.Float, Inv, out v) || !(v > 0) || v > 20) { bad++; continue; }
                    ItemObject it = null; try { it = om != null ? om.GetObject<ItemObject>(p[i].Substring(0, q)) : null; } catch { it = null; }
                    if (it == null) { bad++; continue; }
                    ng[it] = v;
                }
                if (ng.Count > 0) _ng = ng;
                _loadNote += " | arm_crafts_g: n na towar " + ng.Count + (bad > 0 ? " (zlych " + bad + ")" : "");
            }
            catch (Exception e) { _ng = null; _loadNote += " | arm_crafts_g: blad odczytu - n zajecia"; Stumble("ImportGoods", e); }
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
