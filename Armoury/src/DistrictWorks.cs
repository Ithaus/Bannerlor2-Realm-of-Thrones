using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// inwest (noc 17/18.10) - PRACE OKREGU (PO; projekt docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md czesc B, burning, money_routes, ui_text; Jeff zatwierdzil, AI nie inwestuje).
    /// Gracz (i - etap 4, klucz District Works Ai WYLACZONY - lordowie AI) placi ludziom jednej czesci okregu (wies gry albo jedna wioska poboczna) za sprzet jej zajecia:
    /// lodzie i sieci, stado i zagrody, plugi i mlyn, nowe rzedy i prase, szyby i stemple, pily i sidla. Ta czesc robi wiecej SWOJEGO towaru, a polowa tego, co dodatek
    /// da na targu, jest inwestora - przy kazdym powrocie wozow okregu.
    ///  - CENA (F4): pelny sprzet osady = Kit Years x rok tego, co ta osada robi ze swojego zajecia (cena miasta targowego, zwykla pora roku); K zlota = K / cena jednostek.
    ///  - DODATEK (F7): b = B x (2x - x^2), x = jednostki pracujace / osady stojace czesci (gora 1); B = Bonus Side (wioska) albo Bonus Main (wies gry). Jednostki rosna
    ///    liniowo przez czas gotowosci rodzaju (stado 7, lasy 14, lodzie / pola / szyby 28, sady 182 doby). Dodatek mnozy produkcje zajecia czesci: pod KS w rachunku
    ///    okregu KS (StartCalibration.Inwest.cs: kg zajecia bloku x b przed zima), bez KS w kroku zajec (VillageCrafts.Step: udzial czesci x (1 + b)) - jedna regula.
    ///  - WYPLATA (F9, przy powrocie wozu do wsi macierzystej - VillageTakings.EnterPostfix po 112 i F4b, przed danina DS): pula P = Share x (suma E / V) x utarg,
    ///    E = b x wartosc dnia zajecia czesci, V = wartosc dnia produkcji okregu (z dodatkiem); z licznika pana i z kiesy wsi w proporcji tego, co weszlo w tym oknie;
    ///    P najwyzej polowa utargu i najwyzej to, co weszlo. Majatki BK bez zmian. Wojna rodu z krolestwem okregu - bez wyplaty (zloto zostaje w zwyklym podziale).
    ///  - PIENIADZE ZAKUPU: (1 - m) x K - place do kiesy wsi okregu; m x K - prawdziwe sztuki z polki miasta targowego (ShelfBuy, sufit Build Material Price Cap x wartosc)
    ///    do kasy miasta; brak na polce - sztuki z magazynu wsi do kiesy wsi; reszta - place (wiesniacy robia sami). Sztuki opuszczaja swiat (ksiega towarow "budowy").
    ///  - OGIEN (burning): osady oproznione rabunkiem (puste z rabunku ksiegi wiosek, takze zerowanie armii) zabieraja swoja czesc: przesuniete = U x dE / S przed;
    ///    L x przesuniete stracone (do odbudowy po powrocie), reszta bezczynna; stado - zwierzeta do napastnika (bez niego - do magazynu wsi). Glod niczego nie niszczy
    ///    (bezczynne). Powrot: bezczynne pracuja od razu, stracone mozna odbudowac za R x dzisiejsza cena w Rebuild Days od powrotu (samo - Auto Rebuild, gdy zostaje
    ///    Keep Wage Days zoldu druzyny); po terminie prawo przepada.
    ///  - Rod wymarl - jednostki przechodza na wies (pracuja, dodatek idzie do zwyklego podzialu). Bez sprzedazy prac (v1). +1 relacji z notablami okregu, gdy prace gracza
    ///    przekraczaja 1% rocznej produkcji okregu (najwyzej raz na 28 dob).
    /// Zapis arm_works (SaveText, kawalki; zly wpis - pominiety i liczony, nigdy wywrotka; brak klucza = brak prac). Klucz District Works wylaczony - prace w zapisie zamrozone:
    /// bez dodatku, bez wyplat, menu widoczne nieaktywne z powodem (BEE jak dotad). Wyjatki per okreg liczone (potkniecia) - nic sie nie gasi.
    /// Recenzja (noc 17/18.10): jedna regula dodatku dla produkcji i wyplat - b liczone PRZED rachunkiem KS i planem zajec doby (BeforeProduction), zmiana b oznacza okreg
    /// do przeliczenia (KS, plan zajec, wycena doby), wyplata bierze E z opublikowanego b; bez drogi produkcji (rampa KS, produkcja wsi niewpieta - PathOf) prace zamrozone
    /// (bez zakupu i wyplat); bez zajec wiosek - plan z samych prac (VillageCrafts.WorksOnlyPlans); stado - zwierzeta z jednostek, za ktorymi stoja (HerdDriven); zapis w2
    /// z cena sprzetu czesci (strata przy ogniu zaraz po wczytaniu).
    /// </summary>
    internal static partial class DistrictWorks
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "Prace okregu (PO): ";
        private const string SaveVer = "w2";                 // recenzja: w2 = w1 + cena sprzetu czesci (Kit, wartosc straty przy ogniu po wczytaniu); w1 czytany
        private const string MainPart = "#main", VillageClan = "#village";
        private const double DefaultH = 0.868;      // swiat: utarg oddany / wartosc produkcji okregow bez stadnin (projekt F10)
        private static readonly object Gate = new object();

        internal static bool On { get { var s = Settings.Current; return s != null && s.DistrictWorks; } }

        // ============================================================ rodzaje prac (stale w kodzie - projekt "KIND TABLE")
        internal const int KHerd = 0, KBoats = 1, KFields = 2, KTrees = 3, KPits = 4, KWoods = 5, Kinds = 6;
        internal static readonly string[] KindName = { "a herd and pens", "boats and nets", "ploughs, a mill and barns", "new rows and a press", "shafts, timbering and kilns", "saws, sledges and traps" };
        internal static readonly string[] KindShort = { "herd", "boats", "ploughs", "rows", "pits", "woods" };
        private static readonly string[] KindLog = { "STADO", "LODZIE", "POLA", "SADY", "SZYBY", "LASY" };
        internal static readonly float[] KM = { 0.60f, 0.35f, 0.30f, 0.15f, 0.35f, 0.25f };   // czesc na material (reszta place)
        internal static readonly int[] KReady = { 7, 28, 28, 182, 28, 14 };                   // doby gotowosci
        internal static readonly float[] KL = { 1.00f, 1.00f, 0.50f, 0.25f, 0.50f, 1.00f };   // stracone na oproznionej osadzie
        internal static readonly float[] KR = { 1.00f, 0.50f, 0.50f, 0.50f, 0.50f, 0.50f };   // odbudowa jako czesc dzisiejszej ceny
        private static readonly string[][] KMat =
        {
            new string[0],                                   // stado - zwierzeta zajecia (AnimalOf)
            new[] { "hardwood", "flax", "tools" },
            new[] { "tools", "hardwood", "limestone" },
            new[] { "tools", "hardwood" },
            new[] { "hardwood", "tools" },
            new[] { "tools" },
        };

        /// <summary>Rodzaj prac zajecia (KsTab id); -1 - bez prac w v1 (stadniny - wozy nie sprzedaja koni).</summary>
        internal static int KindOfCraft(string id)
        {
            switch (id)
            {
                case "cattle_farm": case "cattle_farm:maslo": case "sheep_farm": case "swine_farm": return KHerd;
                case "fisherman": case "whaler": case "walrus_hunter": return KBoats;
                case "wheat_farm": case "flax_plant": case "silk_plant": case "garden": case "spice_garden": return KFields;
                case "vineyard": case "olive_trees": case "date_farm": case "citrus": return KTrees;
                case "iron_mine": case "silver_mine": case "salt_mine": case "clay_mine": case "gold_mine": case "quarry": case "lime_pit": case "sea_salt": return KPits;
                case "lumberjack": case "trapper": case "hunter": return KWoods;
                default: return -1;
            }
        }

        private static string AnimalOf(string craft)
        {
            switch (craft) { case "cattle_farm": case "cattle_farm:maslo": return "cow"; case "sheep_farm": return "sheep"; case "swine_farm": return "hog"; default: return null; }
        }

        // ============================================================ stan (zapis arm_works)
        private sealed class Lot { public double Units; public int Day, Ready; }
        private sealed class Reb { public double Units; public int Deadline; public bool Reminded; }
        private sealed class Row
        {
            public string Clan, Dist, Part; public int Craft = -1, Kind = -1; public double U, I, Z;
            public readonly List<Lot> Ramp = new List<Lot>(); public readonly List<Reb> Rebs = new List<Reb>();
            public string Animal; public double Animals;          // stado: przedmiot i zwierzeta w pracach (kupione, minus spedzone)
            public long In, InWage, InMat, Out, LostG; public int Day0 = -1;
            public double SeenBr = -1, SeenBf = -1;
            public double Paid7, Exp7;                            // wyplacone i oczekiwane od ostatniej linii 7 dob (CSV, komunikat)
            public bool War;                                      // wojna z krolestwem okregu przy ostatnim sprawdzeniu (komunikat raz)
            public double Kit;                                    // ostatnia znana cena pelnego sprzetu osady tej czesci (zakup, doba) - wartosc straty przy ogniu (czesc bez stojacych osad ma cene 0)
            // pamiec sesji
            public double Eff(int today)
            {
                double u = U;
                foreach (var l in Ramp) u += l.Units * Math.Min(1.0, Math.Max(0.0, (today - l.Day) / (double)Math.Max(1, l.Ready)));
                return u;
            }
            public double Building() { double u = 0; foreach (var l in Ramp) u += l.Units; return u; }
            public double Rebuildable() { double u = 0; foreach (var r in Rebs) u += r.Units; return u; }
            public double Held() { return U + Building() + I + Z + Rebuildable(); }
        }
        private static readonly List<Row> _rows = new List<Row>();
        private static readonly Dictionary<string, int> _relDay = new Dictionary<string, int>(StringComparer.Ordinal);   // okreg -> doba ostatniej relacji
        private static readonly Dictionary<string, double> _handed = new Dictionary<string, double>(StringComparer.Ordinal); // okreg -> suma utargu oddanego przy wozach (okno ok. 28 dob, zanikajace)
        private static readonly Dictionary<string, int> _handedDays = new Dictionary<string, int>(StringComparer.Ordinal); // okreg -> doby w sumie (najwyzej 28)
        private static readonly Dictionary<string, double> _handToday = new Dictionary<string, double>(StringComparer.Ordinal); // okreg -> utarg oddany od ostatniej doby

        /// <summary>Utarg oddany na dobe (srednia okna ok. 28 dob) okregu z pracami; 0 - brak pomiaru.</summary>
        private static double HandedPerDay(string id)
        {
            double s; int d;
            return _handed.TryGetValue(id, out s) && _handedDays.TryGetValue(id, out d) && d >= 7 ? s / d : 0.0;
        }
        private static string _pending; private static string _loadNote = "nowa kampania albo zapis bez arm_works";
        private static int _loadBad;

        // pamiec sesji: dodatek czesci (czytany z modelu produkcji - jedna referencja), wartosci okregow (doba)
        private sealed class Bonus { public int Craft; public double B; }
        private static volatile Dictionary<Village, Dictionary<string, Bonus>> _bonus = new Dictionary<Village, Dictionary<string, Bonus>>();
        private sealed class DistVal { public int Day; public double V, E; public readonly Dictionary<string, double> EPart = new Dictionary<string, double>(StringComparer.Ordinal); public readonly Dictionary<string, double> VPart = new Dictionary<string, double>(StringComparer.Ordinal); }
        private static readonly Dictionary<string, DistVal> _val = new Dictionary<string, DistVal>(StringComparer.Ordinal);
        private static readonly Dictionary<Village, double[]> _ksExtra = new Dictionary<Village, double[]>();   // pod KS: sztuki dodatku na dobe (ostatni rachunek okregu)

        // liczniki do linii (od ostatniej linii)
        private static int _st, _stAll, _dNew, _dNewAi, _dPays, _dWar, _dBurnN, _dIdleN, _dBackN, _dRebN, _dExpN, _dRemN, _dLocked, _dHerdN, _dRel, _dViolate, _dNoCart;
        private static int _dNoPath, _dDirty;                     // recenzja: powroty wozow bez drogi produkcji (bez wyplaty), okregi oznaczone do przeliczenia po zmianie b
        private static int _preDay = int.MinValue; private static double _preMs;   // recenzja: krok przed produkcja doby (BeforeProduction) - raz na dobe, koszt do linii
        private static long _dWage, _dMatTown, _dMatVil, _dPaidTax, _dPaidVil, _dPool, _dHanded, _dExpected, _dRebG, _dIn, _dLostG, _dHerdPcs, _dMatPcs;
        private static double _dBurnU, _dIdleU, _dBackU, _dRebU, _dExpU, _dHerdU;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<string> _dNotes = new List<string>();
        private static bool _startLogged;
        private static int _lastWeek = -1;

        internal static void Reset()
        {
            lock (Gate)
            {
                _rows.Clear(); _relDay.Clear(); _handed.Clear(); _handedDays.Clear(); _handToday.Clear(); _pending = null; _loadNote = "nowa kampania albo zapis bez arm_works"; _loadBad = 0;
                _bonus = new Dictionary<Village, Dictionary<string, Bonus>>(); _val.Clear(); _ksExtra.Clear();
                _st = _stAll = 0; ZeroDay(); _err.Clear(); _dNotes.Clear(); _startLogged = false; _lastWeek = -1;
                _preDay = int.MinValue; _preMs = 0;
            }
            MenuReset();
            DistrictWorksAi.Reset();
        }

        private static void ZeroDay()
        {
            _dNew = _dNewAi = _dPays = _dWar = _dBurnN = _dIdleN = _dBackN = _dRebN = _dExpN = _dRemN = _dLocked = _dHerdN = _dRel = _dViolate = _dNoCart = 0;
            _dNoPath = _dDirty = 0;
            _dWage = _dMatTown = _dMatVil = _dPaidTax = _dPaidVil = _dPool = _dHanded = _dExpected = _dRebG = _dIn = _dLostG = _dHerdPcs = _dMatPcs = 0;
            _dBurnU = _dIdleU = _dBackU = _dRebU = _dExpU = _dHerdU = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            bool first;
            lock (Gate) { _st++; _stAll++; first = _err.Add(where); }
            if (first) Log.Error("DistrictWorks." + where, e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string PartKey(string uid) { return uid ?? MainPart; }

        // ============================================================ odczyty dla produkcji (dowolny watek - jedna referencja)
        /// <summary>Okreg ma prace (czesc z dodatkiem). Sam odczyt.</summary>
        internal static bool HasWorks(Village v)
        {
            if (v == null || !On) return false;
            var b = _bonus; return b.Count > 0 && b.ContainsKey(v);
        }

        /// <summary>Dodatek czesci (wioska po uid, wies gry - null): zajecie prac i b. false - brak prac, klucz wylaczony.</summary>
        internal static bool BonusOf(Village v, string uid, out int craft, out double b)
        {
            craft = -1; b = 0;
            if (v == null || !On) return false;
            var m = _bonus; Dictionary<string, Bonus> d; Bonus x;
            if (m.Count == 0 || !m.TryGetValue(v, out d) || !d.TryGetValue(PartKey(uid), out x) || x == null) return false;
            craft = x.Craft; b = x.B;
            return b > 0;
        }

        /// <summary>Czesc z pracami (jakiegokolwiek posiadacza) nie zmienia zajecia - takze przy kluczu wylaczonym (prace zamrozone zostaja).</summary>
        internal static bool Locks(Village v, string uid)
        {
            if (v == null || v.Settlement == null) return false;
            if (_rows.Count == 0) return false;                          // szybka droga (regula A7 KS pyta o kazdy blok co dobe)
            string id = v.Settlement.StringId, part = PartKey(uid);
            lock (Gate) { foreach (var r in _rows) if (r.Dist == id && r.Part == part && r.Held() > 1e-6) return true; }
            return false;
        }

        /// <summary>Rod ma prace w tym okregu (komunikat zmiany zajecia - ui_text 7).</summary>
        internal static bool HasWorksOf(Village v, Clan c)
        {
            if (v == null || c == null || v.Settlement == null) return false;
            string id = v.Settlement.StringId, cl = c.StringId;
            lock (Gate) { foreach (var r in _rows) if (r.Dist == id && r.Clan == cl && r.Held() > 1e-6) return true; }
            return false;
        }

        /// <summary>Pod KS: dodatek w sztukach na dobe z ostatniego rachunku okregu (StartCalibration.WorksKs) - do linii.</summary>
        internal static void NoteKsExtra(Village v, ItemObject[] items, double[] pcs)
        {
            if (v == null || pcs == null) return;
            lock (Gate) _ksExtra[v] = (double[])pcs.Clone();
        }

        /// <summary>recenzja (wyplata bez towaru): okreg ma droge produkcji dodatku prac - pod KS okreg zywy KS z pelna stawka (WorksKs w rachunku okregu; w rampie KS
        /// czesc gry stawki dodatku nie ma), bez KS plan produkcji wsi (zajecia albo same prace - VillageCrafts.CarriesWorks). Bez drogi prace sa zamrozone: bez zakupu
        /// i bez wyplat (wyplata bez towaru = przelew bez pokrycia). Nie rzuca.</summary>
        internal static bool PathOf(Village v)
        {
            if (v == null) return false;
            try
            {
                if (StartCalibration.KsDistrictLive(v)) return StartCalibration.FullFor(v);
                return VillageCrafts.CarriesWorks(v);
            }
            catch { return false; }
        }

        /// <summary>Wsie gry okregow z dodatkiem prac (opublikowane b) - dla planow samych prac bez zajec (VillageCrafts). Kopia.</summary>
        internal static List<Village> BonusVillages()
        {
            var m = _bonus;
            return m.Count == 0 ? new List<Village>() : new List<Village>(m.Keys);
        }

        // ============================================================ wycena czesci (F4, F7, F11, F13)
        /// <summary>Wycena czesci okregu: zajecie prac, osady, stojace, cena pelnego sprzetu osady (c), jednostki wszystkich posiadaczy (pracujace z rampa) i trzymane,
        /// b dzis, wartosc dnia zajecia (v) i czesc zwykla pora roku.</summary>
        internal sealed class Quote
        {
            public string Uid, Name, Kind; public bool Main, Ks, Allowed; public string Why;
            public int Craft = -1, WKind = -1; public double N, S, Empty, Br, Bf, Home; public int State;
            public double KitPer, V, VNorm, Winter, UEff, UHeld, UMine, B, Bmax, Room, Sensible, ShareDist, VDist, H = DefaultH;
            public VillageCrafts.PartCraft Pc;
        }

        /// <summary>Wyceny wszystkich czesci okregu (do menu, opisu, dymka, AI). false - okreg nieznany.</summary>
        internal static bool QuotesOf(Village v, List<Quote> into, Clan mine)
        {
            if (into != null) into.Clear();
            if (v == null || into == null) return false;
            try
            {
                var parts = new List<VillageCrafts.PartCraft>();
                bool ks;
                if (!VillageCrafts.PartsOf(v, parts, out ks)) return false;
                double winter = 0;
                try { winter = WinterBite.VillageCut(v); } catch { winter = 0; }
                if (!(winter >= 0) || winter > 0.95) winter = Math.Max(0, Math.Min(0.95, winter));
                var s = Settings.Current;
                float share = s != null ? Math.Max(0f, Math.Min(1f, s.DistrictWorksShare)) : 0.5f;
                float bSide = s != null ? Math.Max(0f, Math.Min(2f, s.DistrictWorksBonusSide)) : 0.30f, bMain = s != null ? Math.Max(0f, Math.Min(2f, s.DistrictWorksBonusMain)) : 0.20f;
                float years = s != null ? Math.Max(0.1f, Math.Min(10f, s.DistrictWorksKitYears)) : 1f;
                double vd = 0; foreach (var p in parts) vd += p.ValDay;
                double H = HOf(v, vd);
                int today = Today();
                bool watch = false; try { watch = PeopleScale.WatchException(v.Settlement); } catch { watch = false; }
                bool path = PathOf(v);                                     // recenzja: bez drogi produkcji dodatku - bez nowych prac
                string id = v.Settlement.StringId;
                foreach (var pc in parts)
                {
                    var q = new Quote { Uid = pc.Uid, Name = pc.Name, Kind = pc.Kind, Main = pc.Main, Ks = ks, Pc = pc, H = H, VDist = vd };
                    var b = pc.Book;
                    q.N = b != null ? b.Nw : 0; q.Br = b != null ? b.Br : 0; q.Bf = b != null ? b.Bf : 0; q.Empty = q.Br + q.Bf; q.S = Math.Max(0, q.N - q.Empty); q.Home = b != null ? b.Home : 0;
                    q.State = b != null ? b.State : VillageBook.SStanding;
                    string part = PartKey(pc.Uid);
                    // zajecie prac: prace juz stoja - ich zajecie; inaczej zajecie czesci
                    int held = -1;
                    lock (Gate)
                        foreach (var r in _rows)
                        {
                            if (r.Dist != id || r.Part != part) continue;
                            if (held < 0) held = r.Craft;
                            q.UEff += r.Eff(today); q.UHeld += r.Held();
                            if (mine != null && r.Clan == mine.StringId) q.UMine += r.Eff(today);
                        }
                    q.Craft = held >= 0 ? held : pc.Craft;
                    q.WKind = q.Craft >= 0 ? KindOfCraft(KsTab.Crafts[q.Craft]) : -1;
                    q.V = q.Craft >= 0 && q.Craft < pc.CraftVal.Length ? pc.CraftVal[q.Craft] : 0;
                    if (q.Craft >= 0 && KsTab.Crafts[q.Craft] == "cattle_farm") q.V += pc.CraftVal[KsTab.CCattleB];
                    q.Winter = winter;
                    q.VNorm = q.V / Math.Max(0.05, 1.0 - winter);
                    q.ShareDist = vd > 0 ? pc.ValDay / vd : 0;
                    q.Bmax = pc.Main ? bMain : bSide;
                    q.KitPer = q.S > 0.5 ? years * 364.0 * q.VNorm / q.S : 0;
                    double x = q.S > 0 ? Math.Min(1.0, q.UEff / q.S) : 0;
                    q.B = q.Bmax * (2 * x - x * x);
                    q.Room = Math.Max(0, (q.N - q.UHeld) * q.KitPer);
                    // F13: "rozsadny" sufit - ostatnia moneta jeszcze >= 12% rocznie (pozyczki ludu)
                    double xs = q.Bmax > 0 && share > 0 && H > 0 ? 1.0 - 0.12 / (2.0 * share * H * q.Bmax) : 0;
                    q.Sensible = Math.Max(0, (Math.Min(1.0, Math.Max(0, xs)) * q.S - q.UEff) * q.KitPer);
                    q.Allowed = true;
                    if (!On) { q.Allowed = false; q.Why = "District works are switched off (mod settings)."; }
                    else if (q.WKind < 0) { q.Allowed = false; q.Why = q.Craft >= KsTab.CHorse0 && q.Craft <= KsTab.CHorse5 ? "Horse herds: no works yet - village carts do not sell horses." : "No works for this craft yet."; }
                    else if (watch) { q.Allowed = false; q.Why = "The Night's Watch's gift villages make the same whatever is spent on them."; }
                    else if (!path) { q.Allowed = false; q.Why = "Works here would make nothing now: the district's new economy is still settling in (or village production is not wired - see the Armoury log)."; }
                    else if (q.State == VillageBook.SBurned || q.State == VillageBook.SBurning || q.State == VillageBook.SAbandoned) { q.Allowed = false; q.Why = "No new works until it stands again."; }
                    else if (!(q.KitPer > 0)) { q.Allowed = false; q.Why = "Nobody works here now."; }
                    else if (held < 0 && (pc.Target >= 0 || (pc.Ks && pc.F > 0))) { q.Allowed = false; q.Why = "The village is changing its craft - wait until it is done."; }
                    into.Add(q);
                }
                return true;
            }
            catch (Exception e) { Stumble("QuotesOf", e); into.Clear(); return false; }
        }

        /// <summary>H okregu (F10): utarg oddany przy wozach / wartosc produkcji dnia - zmierzone (okreg z pracami), inaczej srednia swiata 0.868.</summary>
        private static double HOf(Village v, double vd)
        {
            try
            {
                string id = v.Settlement.StringId;
                lock (Gate) { double h = HandedPerDay(id); if (vd > 0 && h > 0) return Math.Max(0.2, Math.Min(1.5, h / vd)); }
            }
            catch { }
            return DefaultH;
        }

        /// <summary>Dochod dnia posiadacza z czesci przy jednostkach mine wobec wszystkich all (F8, F10): 364 x Share x H x E_h / 364.</summary>
        internal static double IncomeDay(Quote q, double mine, double all)
        {
            var s = Settings.Current;
            double share = s != null ? Math.Max(0f, Math.Min(1f, s.DistrictWorksShare)) : 0.5;
            if (!(q.S > 0) || !(all > 0) || !(mine > 0)) return 0;
            double x = Math.Min(1.0, all / q.S), b = q.Bmax * (2 * x - x * x);
            return share * q.H * b * q.V * mine / all;
        }

        // ============================================================ zakup i odbudowa (pieniadze: money_routes 1 i 5)
        internal sealed class BuyResult
        {
            public bool Ok; public string Why, Text; public long Paid, Wage, MatTown, MatVil; public double Units; public int Pieces, Animals; public string Market;
        }

        /// <summary>Kupno prac (albo odbudowa: rebuildUnits > 0, cena R x dzisiejsza) dla rodu clan, placi payer (gracz albo glowa rodu AI). Zloto sprawdzane od nowa.
        /// Place -> kiesa wsi gry; material - sztuki z polki miasta targowego -> kasa miasta (sufit ceny), brak - magazyn wsi -> kiesa wsi, reszta - place.</summary>
        internal static BuyResult Buy(Hero payer, Clan clan, Village v, string uid, int amount, double rebuildUnits, bool ai)
        {
            var res = new BuyResult();
            try
            {
                var s = Settings.Current;
                if (payer == null || clan == null || v == null || v.Settlement == null) { res.Why = "Nobody to pay."; return res; }
                if (!On) { res.Why = "District works are switched off (mod settings)."; return res; }
                bool rebuild = rebuildUnits > 0;
                string why;
                if (!CanTrade(clan, v, rebuild, out why)) { res.Why = why; return res; }
                var qs = new List<Quote>();
                if (!QuotesOf(v, qs, clan)) { res.Why = "The district's villages are not known."; return res; }
                Quote q = null; foreach (var x in qs) if (x.Uid == uid) { q = x; break; }
                if (q == null) { res.Why = "This village is not part of the district."; return res; }
                if (!rebuild && !q.Allowed) { res.Why = q.Why; return res; }
                if (rebuild && !PathOf(v)) { res.Why = "Works here would make nothing now - rebuild later."; return res; }   // recenzja: odbudowa tez tylko z droga produkcji
                if (q.WKind < 0 || !(q.KitPer > 0)) { res.Why = q.Why ?? "No works for this craft yet."; return res; }
                double units;
                if (rebuild) { units = rebuildUnits; amount = (int)Math.Ceiling(KR[q.WKind] * q.KitPer * units); }
                else
                {
                    int min = s != null ? Math.Max(0, s.DistrictWorksMinGold) : 200;
                    if (amount < min) { res.Why = "At least " + min.ToString("N0", Inv) + " gold."; return res; }
                    double room = q.Room;
                    if (amount > room + 0.5) amount = (int)Math.Floor(room);
                    if (amount < 1) { res.Why = "There is no room for more works here."; return res; }
                    units = amount / q.KitPer;
                }
                if (payer.Gold < amount) { res.Why = "You have " + Math.Max(0, payer.Gold).ToString("N0", Inv) + "."; return res; }
                var market = MarketOf(v);
                long g0 = payer.Gold;
                int mat = (int)Math.Floor(KM[q.WKind] * amount);
                long matTown = 0, matVil = 0; int pieces = 0, animals = 0;
                string animal = q.WKind == KHerd ? AnimalOf(KsTab.Crafts[q.Craft]) : null;
                var ids = q.WKind == KHerd ? (animal != null ? new[] { animal } : new string[0]) : KMat[q.WKind];
                // material z polki miasta targowego
                if (mat > 0 && market != null && market.ItemRoster != null && ids.Length > 0)
                {
                    var f = GoodsLedger.Begin(GoodsLedger.FBuild, market);
                    if (f != null) f.Info = "prace okregu (inwest)";
                    try
                    {
                        for (int pass = 0; pass < 2 && matTown < mat; pass++)
                            for (int i = 0; i < ids.Length && matTown < mat; i++)
                            {
                                var it = MBObjectManager.Instance.GetObject<ItemObject>(ids[i]);
                                if (it == null) continue;
                                long left = mat - matTown;
                                int budget = (int)(pass == 0 ? left / (ids.Length - i) : left);
                                if (budget <= 0) continue;
                                var el = new EquipmentElement(it);
                                int cap = Cap(it);
                                int cost, first, last;
                                int want = Math.Max(1, budget / Math.Max(1, it.Value));
                                int n = ShelfBuy.Take(market.ItemRoster, el, want, budget, () => Math.Max(1, market.Town.GetItemPrice(el, null, false)), out cost, out first, out last, 0, true, cap);
                                if (n <= 0) continue;
                                GiveGoldAction.ApplyForCharacterToSettlement(payer, market, cost, true);
                                matTown += cost; pieces += n;
                                if (it.StringId == animal) animals += n;
                                if (it.StringId == "hardwood" || it.StringId == "iron") OreLedger.NoteBuild(it, n);
                            }
                    }
                    finally { GoodsLedger.End(f); }
                }
                // brak na polce - sztuki z magazynu wsi (placone do kiesy wsi)
                if (matTown < mat && ids.Length > 0 && v.Settlement.ItemRoster != null)
                {
                    var f = GoodsLedger.Begin(GoodsLedger.FBuild, v.Settlement);
                    if (f != null) f.Info = "prace okregu (inwest) - magazyn wsi";
                    try
                    {
                        foreach (var id in ids)
                        {
                            if (matTown + matVil >= mat) break;
                            var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                            if (it == null) continue;
                            int have = v.Settlement.ItemRoster.GetItemNumber(it);
                            int price = Math.Max(1, it.Value);
                            int n = (int)Math.Min(have, (mat - matTown - matVil) / price);
                            if (n <= 0) continue;
                            v.Settlement.ItemRoster.AddToCounts(it, -n);
                            matVil += (long)n * price; pieces += n;
                            if (id == animal) animals += n;
                            if (id == "hardwood" || id == "iron") OreLedger.NoteBuild(it, n);
                        }
                    }
                    finally { GoodsLedger.End(f); }
                }
                long wage = amount - matTown;                                   // place + material z magazynu wsi - do kiesy wsi
                if (wage > 0) GiveGoldAction.ApplyForCharacterToSettlement(payer, v.Settlement, (int)wage, true);
                long paid = g0 - payer.Gold;
                if (paid != amount) _dViolate++;                                  // kontrola: zaplacone = place + material (co do monety)
                // jednostki: lot z rampa gotowosci
                string part = PartKey(uid);
                lock (Gate)
                {
                    Row r = null;
                    foreach (var x in _rows) if (x.Clan == clan.StringId && x.Dist == v.Settlement.StringId && x.Part == part) { r = x; break; }
                    if (r == null) { r = new Row { Clan = clan.StringId, Dist = v.Settlement.StringId, Part = part, Craft = q.Craft, Kind = q.WKind, Day0 = Today() }; _rows.Add(r); }
                    if (r.SeenBr < 0) { r.SeenBr = q.Br; r.SeenBf = q.Bf; }
                    r.Kit = q.KitPer;
                    r.Ramp.Add(new Lot { Units = units, Day = Today(), Ready = KReady[q.WKind] });
                    r.In += amount; r.InWage += wage - matVil; r.InMat += matTown + matVil;
                    if (animals > 0) { r.Animal = animal; r.Animals += animals; }
                    if (rebuild) { _dRebN++; _dRebU += units; _dRebG += amount; } else if (ai) _dNewAi++; else _dNew++;
                    _dIn += amount; _dWage += wage - matVil; _dMatTown += matTown; _dMatVil += matVil; _dMatPcs += pieces;
                    if (_dNotes.Count < 8) _dNotes.Add((ai ? "AI " : "") + (rebuild ? "odbudowa " : "") + Name(v) + "/" + (q.Name ?? part) + " " + KindLog[q.WKind] + " " + amount + " zl (" + units.ToString("0.00", Inv)
                                                      + " osad; place " + (wage - matVil) + ", material polka " + matTown + " / magazyn wsi " + matVil + ", szt. " + pieces + ")");
                }
                RebuildBonus();
                StartCalibration.KsDirty(v);                                     // pod KS: dodatek do stawek okregu nastepnej doby
                VillageCrafts.MarkDirty(v);                                      // bez KS: plan produkcji okregu z dodatkiem nastepnej doby
                res.Ok = true; res.Paid = paid; res.Wage = wage - matVil; res.MatTown = matTown; res.MatVil = matVil; res.Units = units; res.Pieces = pieces; res.Animals = animals;
                res.Market = market != null && market.Name != null ? market.Name.ToString() : "the market";
                string dname = Name(v);
                string what = q.WKind == KHerd && animals > 0 ? animals + " " + AnimalWord(animal, animals) : pieces + " loads of timber, flax and tools";
                if (q.WKind == KFields) what = pieces + " loads of tools, timber and stone";
                if (q.WKind == KTrees || q.WKind == KPits || q.WKind == KWoods) what = pieces + " loads of timber and tools";
                res.Text = "You paid " + amount.ToString("N0", Inv) + (rebuild ? " to rebuild " : " for ") + KindName[q.WKind] + " in " + (q.Name ?? dname) + ": "
                           + (wage).ToString("N0", Inv) + " wages to the villagers of " + dname + (matTown > 0 ? ", " + matTown.ToString("N0", Inv) + " for " + what + " at " + res.Market : "")
                           + ". Ready on day " + (Today() + KReady[q.WKind]) + ".";
                return res;
            }
            catch (Exception e) { Stumble("Buy", e); res.Ok = false; res.Why = "Something went wrong (see the Armoury log)."; return res; }
        }

        private static string AnimalWord(string id, int n)
        {
            switch (id) { case "cow": return n == 1 ? "cow" : "cows"; case "sheep": return "sheep"; case "hog": return n == 1 ? "pig" : "pigs"; default: return "animals"; }
        }

        private static int Cap(ItemObject it)
        {
            var s = Settings.Current;
            float k = s != null && s.BuildMaterialPriceCap > 0f ? s.BuildMaterialPriceCap : 2.0f;
            return Math.Max(1, (int)Math.Ceiling(k * Math.Max(1, it.Value)));
        }

        /// <summary>Miasto targowe okregu (wies przy zamku - najblizsze miasto, jak budowy).</summary>
        internal static Settlement MarketOf(Village v)
        {
            try
            {
                var tb = v.TradeBound;
                if (tb != null && tb.IsTown) return tb;
                var b = v.Bound;
                if (b != null && b.IsTown) return b;
                return BuildFunding.NearestTown(v.Settlement);
            }
            catch { return null; }
        }

        /// <summary>Kiedy mozna kupic (B7): wies nie w wojnie z frakcja rodu, nie najezdzana i nie spladrowana (odbudowa - takze spladrowana).</summary>
        internal static bool CanTrade(Clan clan, Village v, bool rebuild, out string why)
        {
            why = null;
            try
            {
                IFaction mine = clan != null ? clan.MapFaction : null, theirs = v != null && v.Settlement != null ? v.Settlement.MapFaction : null;
                if (mine != null && theirs != null && mine != theirs && FactionManager.IsAtWarAgainstFaction(mine, theirs))
                { why = "At war with " + (theirs.Name != null ? theirs.Name.ToString() : "them") + ": they will not take your money."; return false; }
                if (v.VillageState == Village.VillageStates.BeingRaided) { why = "The village is being raided."; return false; }
                if (!rebuild && v.VillageState == Village.VillageStates.Looted) { why = "The village was plundered - wait until people return."; return false; }
                return true;
            }
            catch (Exception e) { Stumble("CanTrade", e); why = "Not now."; return false; }
        }

        private static string Name(Village v) { try { return v != null && v.Name != null ? v.Name.ToString() : "?"; } catch { return "?"; } }

        /// <summary>Rod po StringId (Clan.All - takze rody wymarle); null - brak.</summary>
        internal static Clan ClanOf(string id)
        {
            if (string.IsNullOrEmpty(id) || id == VillageClan) return null;
            try { foreach (var c in Clan.All) if (c != null && c.StringId == id) return c; } catch { }
            return null;
        }

        // ============================================================ dodatek (b) wedlug czesci - publikacja dla produkcji
        /// <summary>Przelicza b czesci z jednostek pracujacych (z rampa) i osad stojacych; publikuje jedna referencja. Wolane przy zakupie, uderzeniu, przed produkcja doby
        /// (BeforeProduction) i co dobe. recenzja (dodatek produkcji spozniony do 7 dob wobec wyplat): okreg, ktorego b sie zmienilo (rampa lotu, bezczynne, powroty, ogien),
        /// jest oznaczany do przeliczenia (KS - ld.Res, bez KS - plan zajec) i traci wycene doby (wyplata liczy E z tego samego b co produkcja).</summary>
        private static void RebuildBonus()
        {
            try
            {
                if (!On) { if (_bonus.Count > 0) { var old0 = _bonus; _bonus = new Dictionary<Village, Dictionary<string, Bonus>>(); foreach (var v0 in old0.Keys) MarkChanged(v0); } return; }
                var s = Settings.Current;
                float bSide = s != null ? Math.Max(0f, Math.Min(2f, s.DistrictWorksBonusSide)) : 0.30f, bMain = s != null ? Math.Max(0f, Math.Min(2f, s.DistrictWorksBonusMain)) : 0.20f;
                int today = Today();
                var units = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
                var craft = new Dictionary<string, int>(StringComparer.Ordinal);
                lock (Gate)
                    foreach (var r in _rows)
                    {
                        double e = r.Eff(today);
                        if (!(e > 1e-9)) continue;
                        Dictionary<string, double> d;
                        if (!units.TryGetValue(r.Dist, out d)) { d = new Dictionary<string, double>(StringComparer.Ordinal); units[r.Dist] = d; }
                        double u; d.TryGetValue(r.Part, out u); d[r.Part] = u + e;
                        craft[r.Dist + "|" + r.Part] = r.Craft;
                    }
                var map = new Dictionary<Village, Dictionary<string, Bonus>>();
                var book = new List<VillageBook.PartView>();
                foreach (var kv in units)
                {
                    var st = Settlement.Find(kv.Key);
                    var v = st != null ? st.Village : null;
                    if (v == null || !VillageBook.PartsOf(v, book)) continue;
                    var d = new Dictionary<string, Bonus>(StringComparer.Ordinal);
                    foreach (var p in book)
                    {
                        string part = PartKey(p.Uid);
                        double u; if (!kv.Value.TryGetValue(part, out u)) continue;
                        double S = Math.Max(0, p.Nw - p.Br - p.Bf);
                        double x = S > 0 ? Math.Min(1.0, u / S) : 0;
                        double B = (p.Main ? bMain : bSide) * (2 * x - x * x);
                        int c; craft.TryGetValue(kv.Key + "|" + part, out c);
                        if (B > 0) d[part] = new Bonus { Craft = c, B = B };
                    }
                    if (d.Count > 0) map[v] = d;
                }
                var old = _bonus;
                _bonus = map;
                // okregi ze zmienionym b (takze nowe i znikniete) - przeliczenie produkcji tej doby / nastepnej i nowa wycena wyplat
                foreach (var kv in map) if (Changed(old, kv.Key, kv.Value)) MarkChanged(kv.Key);
                foreach (var kv in old) if (!map.ContainsKey(kv.Key)) MarkChanged(kv.Key);
            }
            catch (Exception e) { Stumble("RebuildBonus", e); }
        }

        private static bool Changed(Dictionary<Village, Dictionary<string, Bonus>> old, Village v, Dictionary<string, Bonus> now)
        {
            Dictionary<string, Bonus> was;
            if (!old.TryGetValue(v, out was) || was == null || was.Count != now.Count) return true;
            foreach (var kv in now)
            {
                Bonus b; if (!was.TryGetValue(kv.Key, out b) || b == null || b.Craft != kv.Value.Craft || Math.Abs(b.B - kv.Value.B) > 1e-6) return true;
            }
            return false;
        }

        /// <summary>Okreg z nowym b: KS przelicza go przy najblizszym rachunku doby, plan zajec bez KS tez, wycena doby (E dla wyplat) od nowa. Nie rzuca.</summary>
        private static void MarkChanged(Village v)
        {
            try
            {
                if (v == null || v.Settlement == null) return;
                StartCalibration.KsDirty(v);
                VillageCrafts.MarkDirty(v);
                lock (Gate) { _val.Remove(v.Settlement.StringId); _dDirty++; }
            }
            catch { }
        }

        // ============================================================ ogien, glod, powroty (burning)
        /// <summary>Devastation.Strike po ksiedze wiosek: okreg z pracami - roznica pustych osad od razu (stado do napastnika uderzenia). Nie rzuca.</summary>
        internal static void OnStrike(Village v, RaidEventComponent comp, MobileParty src)
        {
            if (v == null || v.Settlement == null || !On) return;
            bool any = false;
            string id = v.Settlement.StringId;
            lock (Gate) { foreach (var r in _rows) if (r.Dist == id) { any = true; break; } }
            if (!any) return;
            MobileParty raider = src;
            try { if (comp != null && comp.AttackerSide != null && comp.AttackerSide.LeaderParty != null) raider = comp.AttackerSide.LeaderParty.MobileParty ?? src; } catch { raider = src; }
            Diff(v, raider, true);
        }

        /// <summary>Roznica stanu czesci (puste z rabunku i glodu) od ostatniego odczytu: przesuniecie jednostek (F14), stado do napastnika, powroty, prawo odbudowy.</summary>
        private static void Diff(Village v, MobileParty raider, bool strike)
        {
            try
            {
                var book = new List<VillageBook.PartView>();
                if (!VillageBook.PartsOf(v, book)) return;
                string id = v.Settlement.StringId;
                int today = Today();
                var s = Settings.Current;
                int days = s != null ? Math.Max(1, s.DistrictWorksRebuildDays) : 91;
                var herd = new List<KeyValuePair<Row, int>>();
                lock (Gate)
                {
                    foreach (var r in _rows)
                    {
                        if (r.Dist != id) continue;
                        VillageBook.PartView p = null;
                        foreach (var x in book) if (PartKey(x.Uid) == r.Part) { p = x; break; }
                        if (p == null) continue;
                        if (r.SeenBr < 0) { r.SeenBr = p.Br; r.SeenBf = p.Bf; continue; }
                        double sBefore = Math.Max(0, p.Nw - r.SeenBr - r.SeenBf);
                        double dR = p.Br - r.SeenBr, dF = p.Bf - r.SeenBf;
                        if (dR > 1e-6 && sBefore > 1e-6)
                        {
                            double frac = Math.Min(1.0, dR / sBefore);
                            double holding = r.U + r.Building() + r.I;                    // recenzja: jednostki, za ktorymi stoja zwierzeta (przed przesunieciem; Z i odbudowy - bez zwierzat)
                            double moved = Move(r, frac);
                            double lost = KL[r.Kind] * moved;
                            r.Z += lost; r.I += moved - lost;
                            int drive = r.Kind == KHerd ? HerdDriven(r.Animals, holding, lost) : 0;
                            _dBurnN++; _dBurnU += lost; _dIdleU += moved - lost;
                            long g = (long)Math.Round(lost * r.Kit);                     // ostatnia znana cena (bez wyceny pod blokada; czesc spalona ma dzis 0 osad stojacych)
                            r.LostG += g; _dLostG += g;
                            if (drive > 0) { herd.Add(new KeyValuePair<Row, int>(r, drive)); _dHerdU += lost; }
                            if (r.Clan == PlayerClanId() && lost > 1e-3)
                                Msg("Raiders emptied " + Math.Round(dR) + " homesteads of " + (p.Name ?? "?") + " (" + Name(v) + "): " + (r.Kind == KHerd ? "your animals there were driven off" : "your " + KindShort[r.Kind] + " there burned")
                                    + " - " + g.ToString("N0", Inv) + " of works lost.", true);
                        }
                        if (dF > 1e-6 && Math.Max(0, p.Nw - r.SeenBr - r.SeenBf - Math.Max(0, dR)) > 1e-6)
                        {
                            double sb = Math.Max(1e-6, p.Nw - r.SeenBr - Math.Max(0, dR) - r.SeenBf);
                            double moved = Move(r, Math.Min(1.0, dF / sb));
                            r.I += moved; _dIdleN++; _dIdleU += moved;
                        }
                        double eBefore = r.SeenBr + r.SeenBf, eNow = p.Br + p.Bf;
                        if (eNow < eBefore - 1e-6 && eBefore > 1e-6)
                        {
                            double fr = Math.Min(1.0, (eBefore - eNow) / eBefore);
                            double back = r.I * fr, reb = r.Z * fr;
                            r.I -= back; r.U += back; r.Z -= reb;
                            if (reb > 1e-6) r.Rebs.Add(new Reb { Units = reb, Deadline = today + days });
                            _dBackN++; _dBackU += back;
                        }
                        r.SeenBr = p.Br; r.SeenBf = p.Bf;
                    }
                }
                foreach (var kv in herd) DriveOff(kv.Key, kv.Value, v, raider);
                RebuildBonus();
                if (strike) { StartCalibration.KsDirty(v); VillageCrafts.MarkDirty(v); }
            }
            catch (Exception e) { Stumble("Diff", e); }
        }

        /// <summary>Przesuwa czesc frac jednostek pracujacych i w budowie (kazdy lot ta sama czesc); zwraca przesuniete.</summary>
        private static double Move(Row r, double frac)
        {
            double m = r.U * frac; r.U -= m;
            foreach (var l in r.Ramp) { double x = l.Units * frac; l.Units -= x; m += x; }
            r.Ramp.RemoveAll(l => l.Units <= 1e-9);
            return m;
        }

        /// <summary>recenzja (stado - za malo zwierzat do napastnika): zwierzeta spedzone z utraconych jednostek = zwierzeta x stracone / jednostki, za ktorymi stoja zwierzeta
        /// PRZED przesunieciem (pracujace + w budowie + bezczynne z glodu; stracone wczesniej i prawa odbudowy zwierzat juz nie maja). Najwyzej tyle, ile jest. Ten sam rachunek
        /// w Diff i w probie T2.</summary>
        internal static int HerdDriven(double animals, double holdingBefore, double lostUnits)
        {
            if (!(animals > 0) || !(holdingBefore > 1e-9) || !(lostUnits > 0)) return 0;
            double n = Math.Floor(animals * Math.Min(1.0, lostUnits / holdingBefore) + 0.5);
            return (int)Math.Max(0, Math.Min(Math.Floor(animals), n));
        }

        /// <summary>Stado: zwierzeta z oproznionych osad do napastnika (bez niego - do magazynu wsi), ramka ksiegi towarow "rabunek osada po osadzie". n - z HerdDriven.</summary>
        private static void DriveOff(Row r, int n, Village v, MobileParty raider)
        {
            try
            {
                if (n <= 0 || r.Animal == null) return;
                var it = MBObjectManager.Instance.GetObject<ItemObject>(r.Animal);
                if (it == null) return;
                n = (int)Math.Min(n, Math.Floor(r.Animals));
                if (n <= 0) return;
                ItemRoster to = raider != null && raider.IsActive && raider.ItemRoster != null ? raider.ItemRoster : v.Settlement.ItemRoster;
                var f = GoodsLedger.Begin(GoodsLedger.FRaid, v.Settlement, raider);
                if (f != null) f.Info = "prace okregu: stado do napastnika (inwest)";
                try { to.AddToCounts(it, n); }
                finally { GoodsLedger.End(f); }
                lock (Gate) { r.Animals -= n; _dHerdN++; _dHerdPcs += n; }
            }
            catch (Exception e) { Stumble("DriveOff", e); }
        }

        private static double KitOfRow(Row r, Village v, out double kit)
        {
            kit = 0;
            try
            {
                var qs = new List<Quote>();
                if (!QuotesOf(v, qs, null)) return 0;
                foreach (var q in qs) if (PartKey(q.Uid) == r.Part) { kit = q.KitPer; return q.KitPer; }
            }
            catch { }
            return 0;
        }

        private static string PlayerClanId() { try { return Clan.PlayerClan != null ? Clan.PlayerClan.StringId : null; } catch { return null; } }

        private static void Msg(string t, bool bad = false) { try { Log.Player(t, bad); } catch { } }

        // ============================================================ wyplata przy powrocie wozu (money_routes 2)
        /// <summary>VillageTakings.EnterPostfix (woz wrocil do wsi macierzystej; po 112 i F4b, przed danina DS): pula prac okregu z licznika pana i z kiesy wsi w proporcji
        /// tego, co weszlo w tym oknie. handed - utarg oddany, tax / purse - przyrost licznika i kiesy w oknie. paid - wyplacone posiadaczom prac (licznik + kiesa; recenzja
        /// calosci: linia DS liczy je osobno, nie jako udzial pana).</summary>
        internal static void OnCartReturn(MobileParty cart, Village home, int handed, long tax, long purse, out long paid)
        {
            paid = 0;
            if (home == null || home.Settlement == null || handed <= 0 || !On) return;
            string id = home.Settlement.StringId;
            try
            {
                lock (Gate)
                {
                    bool any = false; foreach (var r in _rows) if (r.Dist == id) { any = true; break; }
                    if (!any) return;                                 // okreg bez prac - nic (bez pamieci utargu: wycena bierze srednia swiata H)
                    double h; _handToday.TryGetValue(id, out h); _handToday[id] = h + handed;   // utarg doby (H okregu = utarg na dobe / wartosc produkcji dnia)
                }
                if (!PathOf(home)) { lock (Gate) _dNoPath++; return; }       // recenzja: bez drogi produkcji dodatku (rampa KS, produkcja wsi niewpieta) - bez wyplaty, zloto w zwyklym podziale
                var s = Settings.Current;
                double share = s != null ? Math.Max(0f, Math.Min(1f, s.DistrictWorksShare)) : 0.5;
                DistVal dv = ValOf(home);
                if (dv == null || !(dv.V > 0) || !(dv.E > 0)) { _dNoCart++; return; }
                long inWin = Math.Max(0L, tax) + Math.Max(0L, purse);
                double pool = share * (dv.E / dv.V) * handed;
                pool = Math.Min(pool, Math.Min(0.5 * handed, inWin));
                if (!(pool >= 1)) return;
                int today = Today();
                // udzial posiadaczy: E wiersza = E czesci x jednostki wiersza / jednostki czesci; E rodu = suma jego wierszy
                var er = new Dictionary<Row, double>();
                var eh = new Dictionary<string, double>(StringComparer.Ordinal);
                double eAll = 0;
                lock (Gate)
                {
                    var unitsPart = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var r in _rows) if (r.Dist == id) { double u; unitsPart.TryGetValue(r.Part, out u); unitsPart[r.Part] = u + r.Eff(today); }
                    foreach (var r in _rows)
                    {
                        if (r.Dist != id) continue;
                        double ep; if (!dv.EPart.TryGetValue(r.Part, out ep) || !(ep > 0)) continue;
                        double up; unitsPart.TryGetValue(r.Part, out up);
                        double e = up > 0 ? ep * r.Eff(today) / up : 0;
                        if (!(e > 0)) continue;
                        er[r] = e;
                        double cur; eh.TryGetValue(r.Clan, out cur); eh[r.Clan] = cur + e; eAll += e;
                    }
                }
                if (!(eAll > 0)) return;
                long paidTax = 0, paidVil = 0;
                foreach (var kv in eh)
                {
                    double part = pool * kv.Value / eAll;
                    if (!(part >= 1)) continue;
                    var clan = kv.Key == VillageClan ? null : ClanOf(kv.Key);
                    var leader = clan != null && !clan.IsEliminated ? clan.Leader : null;
                    if (leader == null || !leader.IsAlive) continue;                        // wies albo rod bez glowy - dodatek w zwyklym podziale
                    IFaction mf = clan.MapFaction, df = home.Settlement.MapFaction;
                    if (mf != null && df != null && mf != df && FactionManager.IsAtWarAgainstFaction(mf, df)) { lock (Gate) _dWar++; continue; }   // B9
                    long want = (long)Math.Floor(part);
                    long fromTax = inWin > 0 ? (long)Math.Floor(want * (double)Math.Max(0L, tax) / inWin) : 0;
                    long fromVil = want - fromTax;
                    fromTax = Math.Min(fromTax, Math.Max(0, home.TradeTaxAccumulated));
                    fromVil = Math.Min(fromVil, Math.Max(0, home.Gold));
                    if (fromTax > 0) { home.TradeTaxAccumulated -= (int)fromTax; leader.ChangeHeroGold((int)fromTax); }   // licznik pana -> glowa rodu (jak F4b: licznik -> kiesa)
                    if (fromVil > 0) GiveGoldAction.ApplyForSettlementToCharacter(home.Settlement, leader, (int)fromVil, true);   // kiesa wsi -> glowa rodu (zdarzenie gry)
                    MoneyLedger.CartWorksInWindow(cart, (int)fromVil, (int)fromTax);   // okno powrotu wozu w ksiedze pieniadza: utarg wsi brutto (wyplata nie jest "zniklo")
                    if (fromVil > 0) MoneyLedger.Note169(MoneyLedger.N169Other, home.Settlement, -(int)fromVil);   // kiesa wsi poza tickiem - nazwane ("inne")
                    paidTax += fromTax; paidVil += fromVil;
                    long got = fromTax + fromVil;
                    paid += got;
                    lock (Gate)
                        foreach (var x in er)
                            if (x.Key.Clan == kv.Key) { double rp = got * x.Value / kv.Value; x.Key.Out += (long)Math.Round(rp); x.Key.Paid7 += rp; }
                }
                lock (Gate)
                {
                    _dPays++; _dPaidTax += paidTax; _dPaidVil += paidVil; _dPool += (long)Math.Floor(pool); _dHanded += handed;
                    if (paidTax + paidVil > pool + 1 || pool > 0.5 * handed + 1 || paidTax + paidVil > inWin) _dViolate++;
                }
            }
            catch (Exception e) { Stumble("OnCartReturn", e); }
        }

        /// <summary>Wartosc dnia okregu (V z dodatkiem) i E czesci z pracami (b x wartosc zajecia czesci) - pamiec doby (liczone raz na dobe na okreg z pracami; zmiana b kasuje
        /// pamiec - MarkChanged). recenzja: b z OPUBLIKOWANEGO dodatku (_bonus - to samo b, ktore czyta produkcja: WorksKs i plan zajec), nie z jednostek chwili.</summary>
        private static DistVal ValOf(Village v)
        {
            string id = v.Settlement.StringId;
            int today = Today();
            lock (Gate) { DistVal dv; if (_val.TryGetValue(id, out dv) && dv.Day == today) return dv; }
            var qs = new List<Quote>();
            if (!QuotesOf(v, qs, null)) return null;
            var d = new DistVal { Day = today };
            foreach (var q in qs)
            {
                string part = PartKey(q.Uid);
                d.VPart[part] = q.Pc != null ? q.Pc.ValDay : 0;
                d.V += q.Pc != null ? q.Pc.ValDay : 0;
                int pc; double pb;
                double e = BonusOf(v, q.Uid, out pc, out pb) && pc == q.Craft ? pb * q.V : 0.0;
                if (e > 0) { d.EPart[part] = e; d.E += e; }
            }
            d.V += d.E;
            lock (Gate)
            {
                _val[id] = d;
                foreach (var r in _rows)                                      // cena sprzetu czesci (wartosc straty przy ogniu)
                {
                    if (r.Dist != id) continue;
                    foreach (var q in qs) if (PartKey(q.Uid) == r.Part && q.KitPer > 0) { r.Kit = q.KitPer; break; }
                }
            }
            return d;
        }

        // ============================================================ doba
        /// <summary>recenzja (dodatek produkcji do 7 dob za wyplatami): ArmouryBehavior.OnDailyTick PRZED StartCalibration.Daily i VillageCrafts.Daily (PO VillageBook.Daily) -
        /// zapis z wczytania, rampa lotow, rod wymarly, zmiana ukladu, roznice czesci (glod, powroty) i dodatek b DZISIEJSZEJ doby. Okregi ze zmienionym b oznaczone
        /// (MarkChanged): rachunek KS i plan zajec tej doby licza z tym samym b, z ktorego wyplaty przy wozach biora E (ValOf). Raz na dobe; Daily wola go sam, gdy
        /// ArmouryBehavior go nie wolal. Nie rzuca.</summary>
        internal static void BeforeProduction()
        {
            int today = Today();
            if (_preDay == today) return;
            _preDay = today;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                lock (Gate) { if (_pending != null) { string p = _pending; _pending = null; ImportNow(p); } }
                if (!On) { RebuildBonus(); return; }                         // klucz wylaczony: dodatek zdjety (okregi oznaczone do przeliczenia), prace zamrozone
                // rampa: loty gotowe -> pracujace
                lock (Gate)
                    foreach (var r in _rows)
                    {
                        for (int i = r.Ramp.Count - 1; i >= 0; i--) { var l = r.Ramp[i]; if (today - l.Day >= l.Ready) { r.U += l.Units; r.Ramp.RemoveAt(i); } }
                        if (r.Clan != VillageClan)
                        {
                            var c = ClanOf(r.Clan);
                            if (c == null || c.IsEliminated) { r.Clan = VillageClan; _dNotes.Add("rod wymarl - prace " + r.Dist + "/" + r.Part + " przechodza na wies"); }
                        }
                    }
                Remap(today);                                                // prace na wioskach spoza ukladu tej sesji - przy rownej wartosci
                var dists = new HashSet<string>(StringComparer.Ordinal);
                lock (Gate) foreach (var r in _rows) dists.Add(r.Dist);
                foreach (var id in dists)
                {
                    var st = Settlement.Find(id); var v = st != null ? st.Village : null;
                    if (v == null) continue;
                    Diff(v, null, false);                                    // ksiega wiosek tej doby (VillageBook.Daily biegnie wczesniej)
                }
                RebuildBonus();                                              // b dzisiejszej doby (rampa lotow) - zmienione okregi do przeliczenia DZIS
            }
            catch (Exception e) { Stumble("BeforeProduction", e); }
            _preMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>ArmouryBehavior.OnDailyTick (po KS i zajeciach): okno utargu, odbudowa samoczynna, przypomnienia, wygasniecie prawa, wojna, relacja, wycena doby, AI,
        /// linia "Prace okregu (PO)" (gdy cos sie stalo), co 7 dob CSV i komunikat tygodnia. Rampa, roznice czesci i b doby - BeforeProduction (wolany tu, gdy go nie bylo).</summary>
        internal static void Daily()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int today = Today();
            double pre = 0;
            try
            {
                lock (Gate) { if (_pending != null) { string p = _pending; _pending = null; ImportNow(p); } }
                if (!_startLogged) { _startLogged = true; StartLine(); }
                bool called = _preDay == today;
                BeforeProduction();                                          // raz na dobe (gdy ArmouryBehavior wolal go przed KS - nic)
                pre = called ? _preMs : 0;
                if (!On) return;
                var dists = new HashSet<string>(StringComparer.Ordinal);
                lock (Gate)
                {
                    foreach (var r in _rows) dists.Add(r.Dist);
                    // utarg doby do okna ok. 28 dob (suma zanikajaca 27/28 po 28 dobach)
                    foreach (var id in dists)
                    {
                        double t; _handToday.TryGetValue(id, out t);
                        double s0; _handed.TryGetValue(id, out s0); int d0; _handedDays.TryGetValue(id, out d0);
                        if (d0 >= 28) { s0 *= 27.0 / 28.0; d0 = 27; }
                        _handed[id] = s0 + t; _handedDays[id] = d0 + 1;
                    }
                    _handToday.Clear();
                }
                foreach (var id in dists)
                {
                    var st = Settlement.Find(id); var v = st != null ? st.Village : null;
                    if (v == null) continue;
                    Rebuilds(v, today);
                    Relation(v, today);
                    WarCheck(v);
                }
                RebuildBonus();
                foreach (var id in dists)
                {
                    var st = Settlement.Find(id); var v = st != null ? st.Village : null;
                    if (v == null) continue;
                    var dv = ValOf(v);
                    if (dv != null) lock (Gate) _dExpected += (long)Math.Round(OwnShareExpected(id, dv));
                }
                DistrictWorksAi.Daily(today);
                if (today % 7 == 0 && today != _lastWeek) { _lastWeek = today; Week(today); }
                if (TestWanted(today)) Test(today);                         // proba autotestu (plik armoury-po-test.flag, tylko DLL autotestu)
            }
            catch (Exception e) { Stumble("Daily", e); }
            if (!On) return;
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency + pre;   // z krokiem przed produkcja doby
            DayLine(today - 1, ms);
        }

        /// <summary>Oczekiwana wyplata dnia wszystkich posiadaczy okregu (F10: Share x H x E) - do kontroli "wyplacone vs oczekiwane".</summary>
        private static double OwnShareExpected(string id, DistVal dv)
        {
            var s = Settings.Current;
            double share = s != null ? Math.Max(0f, Math.Min(1f, s.DistrictWorksShare)) : 0.5;
            double h; lock (Gate) h = HandedPerDay(id);
            double H = dv.V > 0 && h > 0 ? Math.Max(0.2, Math.Min(1.5, h / dv.V)) : DefaultH;
            double e = share * H * dv.E;
            lock (Gate) foreach (var r in _rows) if (r.Dist == id) { double ep; if (dv.EPart.TryGetValue(r.Part, out ep)) r.Exp7 += e > 0 && dv.E > 0 ? e * ep / dv.E / Math.Max(1, CountPart(id, r.Part)) : 0; }
            return e;
        }

        private static int CountPart(string id, string part) { int n = 0; foreach (var r in _rows) if (r.Dist == id && r.Part == part) n++; return n; }

        /// <summary>Odbudowa: samoczynna (klucz Auto Rebuild i Keep Wage Days zoldu druzyny w kiesie po zaplacie), przypomnienie 14 dob przed terminem, prawo przepada po terminie.</summary>
        private static void Rebuilds(Village v, int today)
        {
            var s = Settings.Current;
            bool autoKey = s == null || s.DistrictWorksAutoRebuild;
            int keep = s != null ? Math.Max(0, s.DistrictWorksKeepWageDays) : 30;
            var todo = new List<KeyValuePair<Row, Reb>>();
            string id = v.Settlement.StringId;
            lock (Gate)
                foreach (var r in _rows)
                {
                    if (r.Dist != id) continue;
                    for (int i = r.Rebs.Count - 1; i >= 0; i--)
                    {
                        var rb = r.Rebs[i];
                        if (today > rb.Deadline)
                        {
                            r.Rebs.RemoveAt(i); _dExpN++; _dExpU += rb.Units;
                            if (r.Clan == PlayerClanId()) Msg("You did not rebuild in " + PartName(v, r.Part) + ": those homesteads have no works now.", true);
                            continue;
                        }
                        if (r.Clan == VillageClan) continue;
                        todo.Add(new KeyValuePair<Row, Reb>(r, rb));
                    }
                }
            foreach (var kv in todo)
            {
                var r = kv.Key; var rb = kv.Value;
                var clan = ClanOf(r.Clan);
                var leader = clan != null ? clan.Leader : null;
                if (leader == null) continue;
                bool player = clan == Clan.PlayerClan;
                double kit; KitOfRow(r, v, out kit);
                if (!(kit > 0)) kit = r.Kit;
                int cost = (int)Math.Ceiling(KR[r.Kind] * kit * rb.Units);
                int wages = 0;
                try { var pp = leader.PartyBelongedTo; wages = pp != null ? Math.Max(0, pp.TotalWage) : 0; } catch { wages = 0; }
                bool can = cost > 0 && leader.Gold - cost >= (long)keep * wages;
                bool auto = player ? AutoRebuild() : autoKey;
                if (auto && can && (player || DistrictWorksAi.RebuildsToo))
                {
                    var res = Buy(leader, clan, v, r.Part == MainPart ? null : r.Part, cost, rb.Units, !player);
                    if (res.Ok) { lock (Gate) r.Rebs.Remove(rb); if (player) Msg("People are back in " + PartName(v, r.Part) + ": you paid " + res.Paid.ToString("N0", Inv) + " to rebuild your " + KindShort[r.Kind] + " (" + res.Wage.ToString("N0", Inv) + " to the villagers, " + res.MatTown.ToString("N0", Inv) + " at " + res.Market + ")."); continue; }
                }
                if (player && !rb.Reminded && rb.Deadline - today <= 14)
                {
                    rb.Reminded = true; _dRemN++;
                    Msg("Your right to rebuild in " + PartName(v, r.Part) + " ends in " + Math.Max(0, rb.Deadline - today) + " days (" + cost.ToString("N0", Inv) + ").");
                }
            }
        }

        private static string PartName(Village v, string part)
        {
            if (part == MainPart) return Name(v);
            var book = new List<VillageBook.PartView>();
            if (VillageBook.PartsOf(v, book)) foreach (var p in book) if (p.Uid == part) return p.Name + " (" + Name(v) + ")";
            return part;
        }

        /// <summary>B12: +1 relacji gracza z notablami okregu, gdy jego prace przekraczaja 1% rocznej produkcji okregu - najwyzej raz na 28 dob. Bez zlota.</summary>
        private static void Relation(Village v, int today)
        {
            try
            {
                var pc = Clan.PlayerClan;
                if (pc == null) return;
                string id = v.Settlement.StringId;
                long in_ = 0;
                lock (Gate)
                {
                    int last; if (_relDay.TryGetValue(id, out last) && today - last < 28) return;
                    foreach (var r in _rows) if (r.Dist == id && r.Clan == pc.StringId) in_ += r.In;
                }
                if (in_ <= 0) return;
                var dv = ValOf(v);
                if (dv == null || !(in_ > 0.01 * dv.V * 364)) return;
                lock (Gate) _relDay[id] = today;
                foreach (var n in v.Settlement.Notables) { if (n != null && n.IsAlive) { ChangeRelationAction.ApplyPlayerRelation(n, 1, false, false); _dRel++; } }
            }
            catch (Exception e) { Stumble("Relation", e); }
        }

        /// <summary>B9: komunikat raz przy wejsciu w wojne z krolestwem okregu (prace zostaja, bez wyplat).</summary>
        private static void WarCheck(Village v)
        {
            try
            {
                var pc = Clan.PlayerClan;
                string id = v.Settlement.StringId;
                IFaction df = v.Settlement.MapFaction;
                lock (Gate)
                    foreach (var r in _rows)
                    {
                        if (r.Dist != id || pc == null || r.Clan != pc.StringId) continue;
                        bool war = pc.MapFaction != null && df != null && pc.MapFaction != df && FactionManager.IsAtWarAgainstFaction(pc.MapFaction, df);
                        if (war && !r.War) Msg("War with " + (df.Name != null ? df.Name.ToString() : "them") + ": your works in " + Name(v) + " pay nothing until peace.", true);
                        r.War = war;
                    }
            }
            catch (Exception e) { Stumble("WarCheck", e); }
        }

        /// <summary>Co 7 dob: komunikat gracza (ui_text 7), CSV prace-okregu.csv (projekt "LOG LINES"), liczniki 7 dob od zera.</summary>
        private static void Week(int today)
        {
            try
            {
                var sb = new StringBuilder(4096);
                double mine = 0; var per = new Dictionary<string, double>(StringComparer.Ordinal);
                string pid = PlayerClanId();
                lock (Gate)
                {
                    foreach (var r in _rows)
                    {
                        var st = Settlement.Find(r.Dist); var v = st != null ? st.Village : null;
                        var book = new List<VillageBook.PartView>();
                        VillageBook.PartView p = null;
                        if (v != null && VillageBook.PartsOf(v, book)) foreach (var x in book) if (PartKey(x.Uid) == r.Part) { p = x; break; }
                        double S = p != null ? Math.Max(0, p.Nw - p.Br - p.Bf) : 0, eff = r.Eff(today);
                        double xx = S > 0 ? Math.Min(1, eff / S) : 0;
                        var s0 = Settings.Current;
                        double bm = p != null && p.Main ? (s0 != null ? s0.DistrictWorksBonusMain : 0.2f) : (s0 != null ? s0.DistrictWorksBonusSide : 0.3f);
                        double bb = bm * (2 * xx - xx * xx), sh = (s0 != null ? s0.DistrictWorksShare : 0.5f) * bb / (1 + bb);
                        sb.Append(today).Append(';').Append(r.Clan).Append(';').Append(r.Dist).Append(';').Append(r.Part == MainPart ? "#main" : r.Part).Append(';').Append(r.Craft >= 0 ? KsTab.Crafts[r.Craft] : "-")
                          .Append(';').Append(KindLog[Math.Max(0, r.Kind)]).Append(';').Append(p != null ? p.Nw.ToString("0.0", Inv) : "-").Append(';').Append(S.ToString("0.0", Inv))
                          .Append(';').Append(eff.ToString("0.00", Inv)).Append(';').Append(r.I.ToString("0.00", Inv)).Append(';').Append(r.Z.ToString("0.00", Inv)).Append(';')
                          .Append(r.Rebs.Count > 0 ? r.Rebs[0].Deadline.ToString(Inv) : "-").Append(';').Append(xx.ToString("0.000", Inv)).Append(';')
                          .Append(bb.ToString("0.000", Inv)).Append(';').Append(sh.ToString("0.000", Inv)).Append(';').Append(Math.Round(r.Paid7)).Append(';').Append(Math.Round(r.Exp7)).Append(';').Append(p != null ? p.State.ToString(Inv) : "-").Append('\n');
                        if (r.Clan == pid && r.Paid7 > 0) { mine += r.Paid7; double c; string dn = v != null ? Name(v) : r.Dist; per.TryGetValue(dn, out c); per[dn] = c + r.Paid7; }
                        r.Paid7 = 0; r.Exp7 = 0;
                    }
                }
                if (sb.Length > 0) Log.Csv("prace-okregu.csv", "dzien;wlasciciel;okreg;wioska;zajecie;rodzaj;osad;stoi;U;I;Z;termin;x;bonus;udzial_towaru;wyplata_7d;oczekiwane_7d;stan", sb.ToString());
                if (mine >= 1)
                {
                    var parts = new List<string>();
                    foreach (var kv in per) parts.Add(kv.Key + " " + Math.Round(kv.Value).ToString("N0", Inv));
                    Msg("Your district works paid " + Math.Round(mine).ToString("N0", Inv) + " this week (" + string.Join(", ", parts.ToArray()) + ").");
                }
            }
            catch (Exception e) { Stumble("Week", e); }
        }

        /// <summary>ArmouryBehavior.OnSessionLaunched (po VillageCrafts.SessionStart): zapis z wczytania i dodatek czesci od razu - pierwszy rachunek KS doby
        /// (StartCalibration.Daily biegnie przed DistrictWorks.Daily) widzi juz prace.</summary>
        internal static void SessionStart()
        {
            try
            {
                lock (Gate) { if (_pending != null) { string p = _pending; _pending = null; ImportNow(p); } }
                RebuildBonus();
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ============================================================ linie logu
        private static void StartLine()
        {
            try
            {
                var s = Settings.Current;
                int rows, dists = 0; double cap = 0, units = 0;
                lock (Gate)
                {
                    rows = _rows.Count;
                    var d = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var r in _rows) { d.Add(r.Dist); cap += r.In; units += r.Held(); }
                    dists = d.Count;
                }
                var kinds = new StringBuilder();
                for (int k = 0; k < Kinds; k++) kinds.Append(k == 0 ? "" : ", ").Append(KindLog[k]).Append(" m ").Append(KM[k].ToString("0.00", Inv)).Append('/').Append(KReady[k]).Append(" dob/L ").Append(KL[k].ToString("0.00", Inv)).Append("/R ").Append(KR[k].ToString("0.00", Inv));
                Log.Info(P + "start | klucz District Works " + (On ? "TAK" : "WYLACZONY (prace zamrozone: bez dodatku, wyplat i menu)") + ", AI " + (s != null && s.DistrictWorksAi ? "TAK" : "WYLACZONE")
                         + " | menu " + MenuText() + " | BEE: " + BeeText() + " | krok 112 (wyplata przy wozie) " + (VillageTakings.CartHookLive ? "wpiety" : "NIECZYNNY")
                         + " | z zapisu: " + _loadNote + "; prac " + rows + " w " + dists + " okregach, kapital " + Math.Round(cap).ToString("0", Inv) + " zl, jednostek " + units.ToString("0.00", Inv)
                         + " | dodatek: wioska " + (s != null ? s.DistrictWorksBonusSide : 0.3f).ToString("0.00", Inv) + ", wies gry " + (s != null ? s.DistrictWorksBonusMain : 0.2f).ToString("0.00", Inv)
                         + ", udzial inwestora " + (s != null ? s.DistrictWorksShare : 0.5f).ToString("0.00", Inv) + ", sprzet = " + (s != null ? s.DistrictWorksKitYears : 1f).ToString("0.0", Inv) + " roku produkcji osady"
                         + " | tabela rodzajow: " + kinds + ".");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void DayLine(int day, double ms)
        {
            try
            {
                bool any;
                lock (Gate) any = _dNew + _dNewAi + _dPays + _dWar + _dBurnN + _dIdleN + _dBackN + _dRebN + _dExpN + _dRemN + _dHerdN + _dRel + _dViolate + _dNoPath + _st > 0 || _dNotes.Count > 0 || _rows.Count > 0 && LogGate.Lines && day % 7 == 0;
                if (!any) { lock (Gate) { ZeroDay(); _dNotes.Clear(); _st = 0; } return; }
                var sb = new StringBuilder(1500);
                lock (Gate)
                {
                    double units = 0, cap = 0; int rows = _rows.Count;
                    foreach (var r in _rows) { units += r.Held(); cap += r.In; }
                    long paidIn = _dWage + _dMatTown + _dMatVil, paidOut = _dPaidTax + _dPaidVil;
                    sb.Append(P).Append("dzien ").Append(day).Append(" | prac ").Append(rows).Append(", jednostek ").Append(units.ToString("0.00", Inv)).Append(", kapital ").Append(Math.Round(cap).ToString("0", Inv)).Append(" zl")
                      .Append(" | nowe: gracz ").Append(_dNew).Append(", AI ").Append(_dNewAi).Append(", wplacone ").Append(_dIn).Append(" = place ").Append(_dWage).Append(" + material polka ").Append(_dMatTown)
                      .Append(" + material magazyn wsi ").Append(_dMatVil).Append(" (szt. ").Append(_dMatPcs).Append("; roznica ").Append(_dIn - paidIn).Append(")")
                      .Append(" | wyplaty: powrotow ").Append(_dPays).Append(", utarg ").Append(_dHanded).Append(", pula ").Append(_dPool).Append(", wyplacone ").Append(paidOut).Append(" = z licznika pana ").Append(_dPaidTax)
                      .Append(" + z kiesy wsi ").Append(_dPaidVil).Append(" (oczekiwane ").Append(_dExpected).Append(", wstrzymane wojna ").Append(_dWar).Append(", powroty bez wyceny ").Append(_dNoCart)
                      .Append(", bez drogi produkcji ").Append(_dNoPath).Append(")")
                      .Append(" | ogien: uderzen ").Append(_dBurnN).Append(", stracone ").Append(_dBurnU.ToString("0.00", Inv)).Append(" jedn. (").Append(_dLostG).Append(" zl), bezczynne ").Append(_dIdleU.ToString("0.00", Inv))
                      .Append(", stada do napastnikow ").Append(_dHerdN).Append(" (szt. ").Append(_dHerdPcs).Append("), wrocilo do pracy ").Append(_dBackU.ToString("0.00", Inv))
                      .Append(" | odbudowa: ").Append(_dRebN).Append(" (").Append(_dRebU.ToString("0.00", Inv)).Append(" jedn., ").Append(_dRebG).Append(" zl), przypomnienia ").Append(_dRemN).Append(", przepadlo ").Append(_dExpN)
                      .Append(" (").Append(_dExpU.ToString("0.00", Inv)).Append(" jedn.) | relacje +").Append(_dRel)
                      .Append(" | ").Append(StartCalibration.WorksKsText())
                      .Append(" | b zmienione: okregow ").Append(_dDirty).Append(" (przeliczone w dobie zmiany: KS i plan zajec), ").Append(VillageCrafts.WorksPathText())
                      .Append(" | kontrole: wplacone - place - material ").Append(_dIn - paidIn).Append(", wyplacone - licznik - kiesa 0, naruszenia (wyplata > pula, pula > pol utargu, zakup != cena) ").Append(_dViolate)
                      .Append(_dNotes.Count > 0 ? " | " + string.Join("; ", _dNotes.ToArray()) : "")
                      .Append(" | ").Append(DistrictWorksAi.DayText())
                      .Append(" | koszt ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia dzis ").Append(_st).Append(" (od wczytania ").Append(_stAll).Append(").");
                    ZeroDay(); _dNotes.Clear(); _st = 0;
                }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("DayLine", e); }
        }

        // ============================================================ zapis arm_works
        // w2|wiersz;wiersz;...|d:okreg=utarg:doba_relacji,...
        // wiersz: rod~okreg~czesc~zajecie~rodzaj~U~I~Z~loty(u:doba:gotowosc,...)~odbudowy(u:termin:przypomniane,...)~zwierze~zwierzat~wplacone~place~material~wyplacone~stracone~doba0~Br~Bf~cena_sprzetu
        // (w1 - bez ostatniego pola, czytany: cena sprzetu 0 do pierwszej wyceny)
        internal static string Export()
        {
            try
            {
                lock (Gate)
                {
                    if (_pending != null) return _pending;
                    if (_rows.Count == 0 && _handed.Count == 0 && !_autoOff) return "";
                    var sb = new StringBuilder(_rows.Count * 120 + 64);
                    sb.Append(SaveVer).Append('|');
                    bool f = true;
                    foreach (var r in _rows)
                    {
                        if (!f) sb.Append(';'); f = false;
                        sb.Append(C(r.Clan)).Append('~').Append(C(r.Dist)).Append('~').Append(C(r.Part)).Append('~').Append(r.Craft >= 0 ? KsTab.Crafts[r.Craft] : "-").Append('~').Append(r.Kind)
                          .Append('~').Append(D(r.U)).Append('~').Append(D(r.I)).Append('~').Append(D(r.Z)).Append('~');
                        for (int i = 0; i < r.Ramp.Count; i++) { if (i > 0) sb.Append(','); sb.Append(D(r.Ramp[i].Units)).Append(':').Append(r.Ramp[i].Day).Append(':').Append(r.Ramp[i].Ready); }
                        sb.Append('~');
                        for (int i = 0; i < r.Rebs.Count; i++) { if (i > 0) sb.Append(','); sb.Append(D(r.Rebs[i].Units)).Append(':').Append(r.Rebs[i].Deadline).Append(':').Append(r.Rebs[i].Reminded ? 1 : 0); }
                        sb.Append('~').Append(C(r.Animal ?? "-")).Append('~').Append(D(r.Animals)).Append('~').Append(r.In).Append('~').Append(r.InWage).Append('~').Append(r.InMat).Append('~').Append(r.Out)
                          .Append('~').Append(r.LostG).Append('~').Append(r.Day0).Append('~').Append(D(r.SeenBr)).Append('~').Append(D(r.SeenBf)).Append('~').Append(D(r.Kit));
                    }
                    sb.Append("|d:"); f = true;
                    foreach (var kv in _handed)
                    {
                        if (!f) sb.Append(','); f = false;
                        int rd; _relDay.TryGetValue(kv.Key, out rd);
                        int hd; _handedDays.TryGetValue(kv.Key, out hd);
                        sb.Append(C(kv.Key)).Append('=').Append(D(kv.Value)).Append(':').Append(_relDay.ContainsKey(kv.Key) ? rd : -1).Append(':').Append(hd);
                    }
                    sb.Append("|a:").Append(_autoOff ? 0 : 1);                 // przelacznik odbudowy samoczynnej gracza z okna (1 - wedlug klucza)
                    string res = sb.ToString();
                    // recenzja calosci (noc 17/18.10): bez twardego sufitu - dawne "" kasowalo wiersze prac (zloto gracza In/InWage/Out); SaveText.Sync dzieli dlugi napis
                    if (res.Length > 512 * 1024) Log.Info(P + "zapis arm_works duzy (" + res.Length + " znakow) - zapisany w kawalkach (SaveText).");
                    return res;
                }
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static string C(string s) { return string.IsNullOrEmpty(s) ? "-" : s.Replace('|', '/').Replace(';', '.').Replace('~', '-').Replace(',', '.').Replace(':', '.').Replace('=', '-'); }
        private static string D(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "0" : x.ToString("R", Inv); }
        private static double PD(string s) { double x; return double.TryParse(s, NumberStyles.Float, Inv, out x) && !double.IsNaN(x) && !double.IsInfinity(x) ? x : 0; }
        private static int PI(string s, int def) { int x; return int.TryParse(s, NumberStyles.Integer, Inv, out x) ? x : def; }
        private static long PL(string s) { long x; return long.TryParse(s, NumberStyles.Integer, Inv, out x) ? x : 0; }

        internal static void Import(string data) { lock (Gate) { _pending = string.IsNullOrEmpty(data) ? null : data; if (_pending == null) _loadNote = "zapis bez arm_works (brak prac)"; } }

        private static void ImportNow(string t)
        {
            _rows.Clear(); _handed.Clear(); _handedDays.Clear(); _relDay.Clear(); _loadBad = 0;
            try
            {
                var p = t.Split('|');
                bool w1 = p.Length > 0 && p[0] == "w1";                          // recenzja: zapis sprzed ceny sprzetu (20 pol) - czytany
                if (p.Length < 2 || (p[0] != SaveVer && !w1)) { _loadNote = "nieznany format arm_works (" + (p.Length > 0 ? p[0] : "?") + ") - bez prac"; _loadBad++; return; }
                int fields = w1 ? 20 : 21;
                if (p[1].Length > 0)
                    foreach (var e in p[1].Split(';'))
                    {
                        try
                        {
                            var q = e.Split('~');
                            if (q.Length != fields) { _loadBad++; continue; }
                            int craft; if (!KsTab.CraftIndex.TryGetValue(q[3], out craft)) craft = -1;
                            int kind = PI(q[4], -1);
                            if (craft < 0 || kind < 0 || kind >= Kinds) { _loadBad++; continue; }
                            var r = new Row { Clan = q[0], Dist = q[1], Part = q[2], Craft = craft, Kind = kind, U = Math.Max(0, PD(q[5])), I = Math.Max(0, PD(q[6])), Z = Math.Max(0, PD(q[7])) };
                            if (q[8].Length > 0) foreach (var l in q[8].Split(',')) { var a = l.Split(':'); if (a.Length == 3) r.Ramp.Add(new Lot { Units = Math.Max(0, PD(a[0])), Day = PI(a[1], 0), Ready = Math.Max(1, PI(a[2], 28)) }); }
                            if (q[9].Length > 0) foreach (var l in q[9].Split(',')) { var a = l.Split(':'); if (a.Length == 3) r.Rebs.Add(new Reb { Units = Math.Max(0, PD(a[0])), Deadline = PI(a[1], 0), Reminded = a[2] == "1" }); }
                            r.Animal = q[10] == "-" ? null : q[10]; r.Animals = Math.Max(0, PD(q[11]));
                            r.In = PL(q[12]); r.InWage = PL(q[13]); r.InMat = PL(q[14]); r.Out = PL(q[15]); r.LostG = PL(q[16]); r.Day0 = PI(q[17], -1);
                            r.SeenBr = PD(q[18]); r.SeenBf = PD(q[19]);
                            if (!w1) r.Kit = Math.Max(0, PD(q[20]));                // cena sprzetu czesci - strata przy ogniu zaraz po wczytaniu (recenzja)
                            _rows.Add(r);
                        }
                        catch { _loadBad++; }
                    }
                if (p.Length > 2 && p[2].StartsWith("d:", StringComparison.Ordinal) && p[2].Length > 2)
                    foreach (var e in p[2].Substring(2).Split(','))
                    {
                        int q = e.IndexOf('='); if (q <= 0) continue;
                        var a = e.Substring(q + 1).Split(':');
                        _handed[e.Substring(0, q)] = Math.Max(0, PD(a[0]));
                        if (a.Length > 1) { int rd = PI(a[1], -1); if (rd >= 0) _relDay[e.Substring(0, q)] = rd; }
                        _handedDays[e.Substring(0, q)] = a.Length > 2 ? Math.Max(0, Math.Min(28, PI(a[2], 0))) : 0;
                    }
                if (p.Length > 3 && p[3] == "a:0") _autoOff = true;
                _loadNote = "arm_works: prac " + _rows.Count + (_loadBad > 0 ? " (zlych " + _loadBad + " - pominiete)" : "") + ", okregow z utargiem " + _handed.Count;
            }
            catch (Exception e) { _rows.Clear(); _loadNote = "blad odczytu arm_works - bez prac"; Stumble("Import", e); }
        }

        // ============================================================ opis dla gracza (menu wsi, okno okregu, dymek)
        /// <summary>Naglowek opisu okregu (ui_text 4): produkcja okregu wedlug zajec i glowny towar; prace gracza. null - nic do pokazania.</summary>
        internal static string HeadText(Settlement st)
        {
            try
            {
                if (st == null || st.Village == null) return null;
                var v = st.Village;
                var qs = new List<Quote>();
                if (!QuotesOf(v, qs, Clan.PlayerClan)) return null;
                double vd = qs.Count > 0 ? qs[0].VDist : 0;
                if (!(vd > 0)) return null;
                var sb = new StringBuilder(400);
                sb.Append(Name(v)).Append(" district makes about ").Append(Math.Round(vd).ToString("N0", Inv)).Append(" a day: ").Append(DistrictIcon.MixText(v, qs)).Append('.');
                double mine = 0, inc = 0; int parts = 0;
                string pid = PlayerClanId();
                lock (Gate) foreach (var r in _rows) if (r.Dist == st.StringId && r.Clan == pid) { mine += r.In; parts++; }
                if (parts > 0)
                {
                    foreach (var q in qs) if (q.UMine > 0) inc += IncomeDay(q, q.UMine, q.UEff);
                    sb.Append("\nYour works here: ").Append(Math.Round(mine).ToString("N0", Inv)).Append(" in ").Append(parts).Append(parts == 1 ? " village" : " villages").Append(", about ")
                      .Append(inc.ToString("0.0", Inv)).Append(" a day (").Append(Math.Round(inc * 364).ToString("N0", Inv)).Append(" a year) = ")
                      .Append((100.0 * (Settings.Current != null ? Settings.Current.DistrictWorksShare : 0.5f) * EOfMine(qs) / Math.Max(1e-6, vd)).ToString("0.0", Inv)).Append("% of the district's production.");
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("HeadText", e); return null; }
        }

        private static double EOfMine(List<Quote> qs)
        {
            double e = 0;
            foreach (var q in qs) if (q.UMine > 0 && q.UEff > 0) e += q.B * q.V * q.UMine / q.UEff;
            return e;
        }

        /// <summary>Wiersz czesci w opisie okregu (ui_text 4) i w dymku wioski (ui_text 5): zajecie, udzial w produkcji okregu, prace (wszystkie i gracza), po ogniu - co stracone.</summary>
        internal static List<string> PartLines(Settlement st, string uid, bool tip)
        {
            var outl = new List<string>(4);
            try
            {
                if (st == null || st.Village == null) return outl;
                var v = st.Village;
                var qs = new List<Quote>();
                if (!QuotesOf(v, qs, Clan.PlayerClan)) return outl;
                Quote q = null; foreach (var x in qs) if (x.Uid == uid) { q = x; break; }
                if (q == null || q.Pc == null) return outl;
                var m = VillageCrafts.Meta(q.Pc.Craft);
                if (m != null)
                {
                    string makes = "Makes: " + m.Name + (m.GoodsText != m.Name ? " (" + m.GoodsText + ")" : "");
                    if (q.Pc.Target >= 0) { var mt = VillageCrafts.Meta(q.Pc.Target); if (mt != null) makes += ", turning to " + mt.Name + " (" + Math.Round(100 * q.Pc.F) + "% done)"; }
                    else if (q.Pc.Ks && q.Pc.F > 0) makes += ", changing its craft";
                    outl.Add(makes);
                }
                if (q.VDist > 0) outl.Add(Math.Round(100 * q.ShareDist).ToString("0", Inv) + "% of the " + Name(v) + " district's production - about " + Math.Round(q.Pc.ValDay).ToString("N0", Inv) + " a day");
                string pid = PlayerClanId();
                double mine = 0, lost = 0, idle = 0, rebU = 0, mineAll = 0; int deadline = -1; bool war = false;
                lock (Gate)
                    foreach (var r in _rows)
                    {
                        if (r.Dist != st.StringId || r.Part != PartKey(uid)) continue;
                        if (r.Clan == pid) { mine += r.In; lost += r.LostG; idle += r.I; rebU += r.Rebuildable(); mineAll += r.Held(); war = r.War; foreach (var rb in r.Rebs) if (deadline < 0 || rb.Deadline < deadline) deadline = rb.Deadline; }
                    }
                if (q.WKind >= 0 && mine > 0)
                {
                    double inc = IncomeDay(q, q.UMine, q.UEff);
                    double myShare = q.UEff > 0 ? (Settings.Current != null ? Settings.Current.DistrictWorksShare : 0.5f) * q.B / (1 + q.B) * q.UMine / q.UEff : 0;
                    outl.Add("Your " + KindShort[q.WKind] + ": " + Math.Round(mine).ToString("N0", Inv) + " - +" + Math.Round(100 * q.B).ToString("0", Inv) + "% " + (m != null ? m.Name : "") + " - "
                             + (100 * myShare).ToString("0.0", Inv) + "% of its " + (m != null ? m.Name : "goods") + " is yours - about " + inc.ToString("0.0", Inv) + " a day (" + Math.Round(inc * 364).ToString("N0", Inv) + " a year)");
                    if (war) outl.Add("At war with " + (st.MapFaction != null && st.MapFaction.Name != null ? st.MapFaction.Name.ToString() : "them") + ": your works here pay nothing until peace.");
                    if (lost > 0 && (q.State == VillageBook.SBurned || q.State == VillageBook.SBurning || q.Empty > 0.5))
                        outl.Add("BURNED: " + Math.Round(q.Empty) + " of " + Math.Round(q.N) + " homesteads empty. " + Math.Round(lost).ToString("N0", Inv) + " of works lost. What is left pays about " + inc.ToString("0.0", Inv) + " a day.");
                    if (rebU > 1e-3 && deadline >= 0)
                        outl.Add("Rebuild " + Math.Round(KR[q.WKind] * q.KitPer * rebU).ToString("N0", Inv) + " by day " + deadline + " (from " + Name(v) + ").");
                    if (tip) outl.Add("Kit in place: " + Math.Round(100 * Math.Min(1, q.S > 0 ? q.UEff / q.S : 0)).ToString("0", Inv) + "% of a full kit (yours " + Math.Round(100 * Math.Min(1, q.S > 0 ? q.UMine / q.S : 0)).ToString("0", Inv) + "%).");
                }
                else if (q.UEff > 0 && q.WKind >= 0)
                    outl.Add("Works here now: " + Math.Round(100 * Math.Min(1, q.S > 0 ? q.UEff / q.S : 0)).ToString("0", Inv) + "% of a full kit (" + KindShort[q.WKind] + ").");
            }
            catch (Exception e) { Stumble("PartLines", e); }
            return outl;
        }
    }
}
