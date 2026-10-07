using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// DOCHOD Z LENN OD LUDNOSCI (Jeff 04.10: "to nie jest jedna wioska, to symbol - trzeba liczyc
    /// po ludnosci, ktora podalem dla kazdego krolestwa/regionu"). Ludnosc krain z
    /// docs/POPULACJA-WESTEROS-ESSOS.md (moje estymacje, fan-estymacje dla Essos z zapleczem).
    ///
    /// Dzis gra daje 2-26 pensow na mieszkanca rocznie (Reach najmniej, Zelazne Wyspy najwiecej),
    /// bo liczy od liczby osad. Historycznie panowie i korona brali ok. 40 pensow na glowe rocznie
    /// (Anglia ok. 1300: ~200-240 d PKB na glowe - ESTYMACJA).
    ///
    /// Raz na kampanie (zapis w save) dla kazdej krainy (kultura osady) liczymy: ilu ludzi na punkt
    /// hearth wsi i ilu na punkt dobrobytu miasta, tak zeby suma dala ludnosc krainy (czesc miejska
    /// wedle udzialu miast). Potem wies ma hearth x k ludzi - spalona wies ma mniej ludzi i daje mniej,
    /// rosnaca daje wiecej. Dochod: podatek miasta (CalculateTownTax) i dochod wsi BK
    /// (CalculateVillageTaxFromIncome) = ludzie x `PopulationRentPerHead` / dni roku. Zamki bez zmian
    /// (ich dochod to wsie). Cla od handlu bez zmian.
    /// </summary>
    internal static class PopulationLaw
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.PopulationRentEnabled; } }

        private sealed class Region { public float Pop; public float Urban; public Region(float p, float u) { Pop = p; Urban = u; } }

        // kultura osady ROT -> ludnosc krainy i udzial miast (ESTYMACJE, docs/POPULACJA-WESTEROS-ESSOS.md)
        private static readonly Dictionary<string, Region> Table = new Dictionary<string, Region>
        {
            { "battania",   new Region(3000000f, 0.08f) },  // Polnoc
            { "river",      new Region(3500000f, 0.08f) },  // Dorzecze
            { "reach",      new Region(8000000f, 0.10f) },  // Reach (Oldtown)
            { "aserai",     new Region(1500000f, 0.08f) },  // Dorne
            { "vlandia",    new Region(4000000f, 0.08f) },  // Westerlands (Lannisport)
            { "vale",       new Region(3000000f, 0.06f) },  // Dolina
            { "stormlands", new Region(2500000f, 0.05f) },  // Krainy Burzy
            { "sturgia",    new Region(500000f,  0.05f) },  // Zelazne Wyspy
            { "crownlands", new Region(600000f,  0.75f) },  // Krolewska Przystan i Duskendale
            { "dragonstone",new Region(850000f,  0.05f) },  // reszta Ziem Korony
            { "freefolk",   new Region(150000f,  0f) },
            { "nightswatch",new Region(20000f,   0f) },
            { "skagosi",    new Region(50000f,   0f) },
            { "volantine",  new Region(5000000f, 0.25f) },
            { "empire",     new Region(2500000f, 0.32f) },  // Braavos
            { "norvos",     new Region(1500000f, 0.40f) },
            { "pentoshi",   new Region(1500000f, 0.33f) },
            { "qohorik",    new Region(1500000f, 0.33f) },
            { "tyroshi",    new Region(1000000f, 0.40f) },
            { "myrish",     new Region(1000000f, 0.40f) },
            { "lyseni",     new Region(1000000f, 0.40f) },
            { "nord",       new Region(800000f,  0.31f) },  // Lorath
            { "ghiscari",   new Region(3000000f, 0.40f) },  // Zatoka Niewolnicza
            { "qartheen",   new Region(4000000f, 0.37f) },
            { "khuzait",    new Region(750000f,  0f) },     // Dothrakowie
            { "sarnor",     new Region(100000f,  0.20f) },
            { "ibbenese",   new Region(350000f,  0.20f) },
            { "summer",     new Region(750000f,  0.10f) },
            { "yiti",       new Region(100000f,  0.20f) },
            { "valyrian",   new Region(50000f,   0f) },
        };

        // kultura -> [ludzi na punkt hearth wsi, ludzi na punkt dobrobytu miasta]
        private static readonly Dictionary<string, float[]> _k = new Dictionary<string, float[]>();

        internal static void Reset()
        {
            _k.Clear(); RentToday.Clear(); _errCalibrate = false;
            // wpis 87 (audyt pkt 4): BK tworzy nowe PopulationManager/PolicyManager przy kazdej grze - stare referencje dawaly
            // dekret podatkowy zawsze Standard i pomijaly autonomie po wczytaniu drugiego save'a bez restartu
            _bkResolved = false; _popMgr = null; _popData = null; _polResolved = false; _policyMgr = null; _getPolicy = null;
            // demografia krok 3 i 9a: stan ludzi miast, pamiec doby przyrostu i liczniki (latka zostaje wpieta - _growthPatched nie jest stanem kampanii)
            _townPeople.Clear(); _townSeeded = 0; _errTowns = false;
            lock (_gLock) { _gDay = int.MinValue; _gW.Clear(); _gWar.Clear(); }
            _gStumbles = 0; _errGrowth = false; _errGrowthLog = false; _modelLogged = false;
            _hearthLast = 0.0; _hearthLastDay = int.MinValue; _gLast = default(Growth);
        }

        private static void Calibrate()
        {
            if (_k.Count > 0) return;
            float scale = Math.Max(0f, Settings.Current.PopulationScale);
            var hearth = new Dictionary<string, float>();
            var prosp = new Dictionary<string, float>();
            foreach (var s in Settlement.All)
            {
                if (s == null || s.Culture == null) continue;
                string c = s.Culture.StringId;
                if (!Table.ContainsKey(c)) continue;
                if (s.IsVillage && s.Village != null) { float v; hearth.TryGetValue(c, out v); hearth[c] = v + Math.Max(1f, s.Village.Hearth); }
                else if (s.IsTown && s.Town != null) { float v; prosp.TryGetValue(c, out v); prosp[c] = v + Math.Max(1f, s.Town.Prosperity); }
            }
            var parts = new List<string>();
            foreach (var kv in Table)
            {
                float h, p;
                hearth.TryGetValue(kv.Key, out h); prosp.TryGetValue(kv.Key, out p);
                float pop = kv.Value.Pop * scale;
                float urban = p > 0f ? kv.Value.Urban : 0f;
                if (h <= 0f) urban = p > 0f ? 1f : 0f;                      // kraina bez wsi - wszystko w miastach
                float kv0 = h > 0f ? pop * (1f - urban) / h : 0f;
                float kv1 = p > 0f ? pop * urban / p : 0f;
                _k[kv.Key] = new[] { kv0, kv1 };
                parts.Add(kv.Key + " " + (pop / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M (wies " + (int)kv0 + "/hearth, miasto " + (int)kv1 + "/dobrobyt)");
            }
            Log.Info("PopulationLaw: kalibracja ludnosci - " + string.Join("; ", parts.ToArray()) + ".");
            // demografia krok 9a: ludzie miast zapisani jako stan od razu, z dobrobytu tej samej chwili co kalibracja -
            // suma miast krainy to dokladnie jej udzial miejski z tabeli
            SeedTowns("przy kalibracji");
        }

        /// <summary>Czy ludnosc jest juz skalibrowana (ksiega "Ludzie:" czyta PeopleOf tylko wtedy - sama kalibracji nie wywoluje).</summary>
        internal static bool Calibrated { get { return _k.Count > 0; } }

        // ------------------------------------------------------------ jednostka ludzi (demografia krok 2, PeopleUnit)
        // klucz w `_k`, ktory nie jest id kultury: srednio ludzi wsi na punkt hearth na calym swiecie
        private const string WorldKey = "*";
        private static bool _errCalibrate;

        /// <summary>
        /// Kalibracja na zadanie. Jednostka ludzi wola ja PRZED pierwszym zdjeciem hearth (siew puli wyrzutkow, pierwszy tabor):
        /// dotad kalibracja szla dopiero przy pierwszych rentach, po siewie, i rozkladala ludnosc krain na hearth juz pomniejszony.
        /// </summary>
        internal static void EnsureCalibrated()
        {
            try { Calibrate(); }
            catch (Exception e) { if (!_errCalibrate) { _errCalibrate = true; Log.Error("PopulationLaw.EnsureCalibrated", e); } }
        }

        /// <summary>
        /// Ile punktow hearth wsi to jeden jej czlowiek: 1/k kultury osady (k = ludzi na punkt hearth - ta sama liczba, przez
        /// ktora PeopleOf mnozy hearth). 0 = brak przelicznika (kultura spoza tabeli, kraina bez wsi w dniu kalibracji,
        /// PopulationScale 0) - wolajacy zostaje wtedy przy stawce dotychczasowej; dzielenia przez zero nie ma.
        /// </summary>
        internal static float HearthPerMan(Village v)
        {
            if (v == null || v.Settlement == null || v.Settlement.Culture == null) return 0f;
            EnsureCalibrated();
            float[] k;
            if (!_k.TryGetValue(v.Settlement.Culture.StringId, out k) || k == null || k.Length == 0) return 0f;
            float kv = k[0];
            if (!(kv > 0f) || float.IsInfinity(kv)) return 0f;       // takze NaN
            float per = 1f / kv;
            return per > 0f ? per : 0f;
        }

        /// <summary>
        /// Srednio ludzi wsi na punkt hearth na swiecie (ok. 191). Liczone raz - z ludnosci i hearth wsi w chwili pierwszego
        /// pytania (w nowej kampanii: przy siewie puli wyrzutkow, zaraz po kalibracji) - i trzymane w `_k` pod kluczem "*",
        /// wiec idzie do zapisu razem z reszta kalibracji; zapis sprzed tej wersji dostaje je przy pierwszym pytaniu.
        /// 0 = brak danych (zadna wies nie ma przelicznika).
        /// </summary>
        internal static float WorldPeoplePerHearth()
        {
            EnsureCalibrated();
            float[] w;
            if (_k.TryGetValue(WorldKey, out w) && w != null && w.Length > 0 && w[0] > 0f) return w[0];
            double people = 0.0, hearth = 0.0;
            try
            {
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsVillage || s.Village == null || s.Culture == null) continue;
                    float[] k;
                    if (!_k.TryGetValue(s.Culture.StringId, out k) || k == null || k.Length == 0 || !(k[0] > 0f)) continue;
                    double h = Math.Max(0f, s.Village.Hearth);
                    people += h * k[0]; hearth += h;
                }
            }
            catch (Exception e)
            {
                if (!_errCalibrate) { _errCalibrate = true; Log.Error("PopulationLaw.WorldPeoplePerHearth", e); }
                return 0f;      // wolajacy liczy wtedy od hearth, jak dotad
            }
            float kw = hearth > 0.0 ? (float)(people / hearth) : 0f;
            if (!(kw > 0f) || float.IsInfinity(kw)) return 0f;
            _k[WorldKey] = new[] { kw, 0f };
            Log.Info("PopulationLaw: srednia swiata " + kw.ToString("0.0", CultureInfo.InvariantCulture) + " ludzi wsi na punkt hearth (podstawa liczenia wyrzutkow od ludnosci).");
            return kw;
        }

        internal static float PeopleOf(Settlement s)
        {
            if (s == null || s.Culture == null) return 0f;
            Calibrate();
            float[] k;
            if (!_k.TryGetValue(s.Culture.StringId, out k)) return 0f;
            if (s.IsVillage && s.Village != null) return Math.Max(0f, s.Village.Hearth) * k[0];
            if (s.IsTown && s.Town != null) return TownPeople(s, k[1]);
            return 0f;
        }

        // ------------------------------------------------------------ ludnosc miast jako stan (demografia krok 9a)
        // Miasto nie ma jeszcze wlasnych narodzin, zgonow ani migracji (krok 9c). Dotad jego ludzie byli biezacym dobrobytem x k,
        // wiec "Ludnosc:" i renty nalezne chodzily za kazdym ruchem dobrobytu (model BK, glod, oblezenie, budowy), a ludzie
        // przybywali i znikali bez przyczyny. Odtad liczba ludzi miasta jest STANEM: zaklada sie raz - w nowej kampanii przy
        // kalibracji (czyli dokladnie udzial miejski krainy z tabeli), w starym zapisie pierwszego dnia po wczytaniu, z dobrobytu
        // x k tej chwili (zadnego skoku) - i idzie do zapisu (klucz arm_people). Zmieni go dopiero krok 9b / 9c.
        private static readonly Dictionary<string, float> _townPeople = new Dictionary<string, float>();
        private static int _townSeeded;            // miasta zalozone od ostatniej linii logu
        private static bool _errTowns;

        internal static bool TownsFrozen { get { var s = Settings.Current; return s != null && s.TownPeopleFrozen; } }

        /// <summary>Ludzie miasta: stan (zakladany przy pierwszym pytaniu), a przy wylaczonym `TownPeopleFrozen` - dobrobyt x k jak dotad.</summary>
        private static float TownPeople(Settlement s, float kTown)
        {
            float live = Math.Max(0f, s.Town.Prosperity) * kTown;
            if (!TownsFrozen) return live;                                   // co do bitu stary rachunek
            float p;
            if (_townPeople.TryGetValue(s.StringId, out p)) return p;
            if (float.IsNaN(live) || float.IsInfinity(live)) return 0f;      // zepsuty dobrobyt nie zostaje stanem - zalozymy, gdy wroci liczba
            _townPeople[s.StringId] = live;
            _townSeeded++;
            return live;
        }

        /// <summary>
        /// Zaklada stan miastom, ktore go nie maja (wszystkim naraz, zeby dzien zalozenia byl jeden), i raz o tym pisze.
        /// Bez kalibracji nic nie robi - sam jej nie wywoluje.
        /// </summary>
        private static void SeedTowns(string when)
        {
            try
            {
                if (!TownsFrozen || _k.Count == 0) return;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsTown || s.Town == null || s.Culture == null) continue;
                    float[] k;
                    if (!_k.TryGetValue(s.Culture.StringId, out k) || k == null || k.Length < 2) continue;
                    TownPeople(s, k[1]);
                }
                if (_townSeeded <= 0) return;
                double sum = 0.0;
                foreach (var p in _townPeople.Values) sum += p;
                Log.Info("PopulationLaw: ludnosc miast zapisana jako stan (" + when + ") - zalozono " + _townSeeded + " miast, razem " + _townPeople.Count
                         + " miast i " + (sum / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln ludzi (dobrobyt x k z tej chwili); odtad liczba ludzi miasta nie idzie za dobrobytem.");
                _townSeeded = 0;
            }
            catch (Exception e) { if (!_errTowns) { _errTowns = true; Log.Error("PopulationLaw.SeedTowns", e); } }
        }

        /// <summary>Raz na dobe, przed rentami: stan miast zalozony (stary zapis, pierwsza doba) albo porzucony (wylacznik w MCM).</summary>
        private static void TownsDaily()
        {
            try
            {
                if (TownsFrozen) { SeedTowns("pierwsza doba po wczytaniu"); return; }
                _townSeeded = 0;
                if (_townPeople.Count == 0) return;
                Log.Info("PopulationLaw: ludnosc miast znow liczona z dobrobytu (Town People Frozen wylaczone w MCM) - stan " + _townPeople.Count
                         + " miast porzucony; po ponownym wlaczeniu zalozy sie od nowa, z dobrobytu tamtego dnia.");
                _townPeople.Clear();
            }
            catch (Exception e) { if (!_errTowns) { _errTowns = true; Log.Error("PopulationLaw.TownsDaily", e); } }
        }

        /// <summary>Stan ksiegi ludzi do zapisu (klucz arm_people): sekcja "t:" = ludzie miasta. Kolejne kroki dopisza wlasne sekcje.</summary>
        internal static string ExportPeople()
        {
            if (_townPeople.Count == 0) return "";
            var sb = new StringBuilder("v1");
            foreach (var kv in _townPeople)
                sb.Append("|t:").Append(kv.Key).Append('=').Append(kv.Value.ToString("R", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static void ImportPeople(string data)
        {
            try
            {
                _townPeople.Clear(); _townSeeded = 0;
                if (string.IsNullOrEmpty(data)) return;     // zapis sprzed tej wersji - stan zalozy sie pierwszego dnia
                foreach (var part in data.Split('|').Skip(1))
                {
                    if (!part.StartsWith("t:", StringComparison.Ordinal)) continue;      // sekcje kolejnych krokow - nie nasze
                    int eq = part.LastIndexOf('=');
                    if (eq <= 2) continue;
                    float p;
                    if (float.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out p)
                        && !float.IsNaN(p) && !float.IsInfinity(p) && p >= 0f)
                        _townPeople[part.Substring(2, eq - 2)] = p;
                }
            }
            catch (Exception e) { Log.Error("PopulationLaw.ImportPeople", e); }
        }

        // ------------------------------------------------------------ przyrost naturalny wsi (demografia krok 3)
        // Gra dopisuje wsi stale +4 / +1.2 / +0.2 hearth dziennie (ponizej 300 / ponizej 600 / wyzej) bez wzgledu na to, ilu ludzi
        // ta wies oznacza: hearth swiata rosl o ok. 0.3% DZIENNIE (x1.67 w rok), a 6 wsi krain o malym hearth (Wyspy Letnie,
        // Yi Ti) dawalo ksiedze 32 tys. "nowych ludzi" na dobe. Odtad hearth wsi zmienia sie o stope roczna g:
        //   zmiana dobowa = hearth x g / 100 / dni roku,   g [% rocznie] = G0 + GW x W - GH x H - GU x U, obciete do [-1.3; +0.8]
        //   W - dobrobyt warowni wsi wobec mediany warowni tego samego rodzaju (miasto albo zamek) w tej samej krainie, -0.5..+0.5.
        //       [KRYT 4: nie wobec stanu startowego.] Mediana krainy, nie srednia kroczaca z 364 dni: gdy dobrobyt rosnie stale
        //       o 0.3% dziennie, iloraz do sredniej z roku tez dochodzi do pulapu (1 + 0.003 x 364 = 2.1); mediana nie zalezy od
        //       wspolnego dryfu, nie potrzebuje stanu w zapisie i nie skacze ani w dniu wlaczenia, ani po wczytaniu.
        //   H - glod WSI z jej wlasnego stanu [KRYT 5]: czesc plonu, ktora zabiera jej zima (WinterBite.VillageCut). Domyslnie
        //       WYLACZONY (GrowthHungerEnabled) do pomiaru - linia dobowa pokazuje, co by zrobil.
        //   U - niebezpieczenstwo: 0.4 x (1 - bezpieczenstwo warowni / 100) + 0.3 x [kraina w wojnie z krolestwem]
        //       + 0.3 x udzial spalonych i lupionych wsi regionu.
        // Premie hearth gry, BK i BKROT (perki, budynki, polityki, laski bogow, podatki, prawa) nie dopisuja hearth, tylko mnoza
        // DODATNIE g [KRYT 3]: (1 + dodatki / 1.2) x (1 + czynniki), obciete do [0.5; 2]; pulap +0.8 dopiero PO mnozeniu.
        // Wies spalona, lupiona albo pod przymusem (kazdy stan poza Normal): 0. Powrot uchodzcow +0.5 hearth ponizej 40
        // (ScorchedEarth.RefugeeReturn) zostaje do kroku 4.
        // k kultury sie skraca, wiec rachunek idzie wprost na hearth; ludzi (hearth x k) pokazuja tylko dymek i log.
        private const float BonusUnit = 1.2f;          // hearth dziennie premii gry, ktore podwajaja przyrost (baza srodkowego pasma gry)
        private const float BonusMin = 0.5f, BonusMax = 2f;
        private const float WSpan = 0.5f;              // dobrobyt liczy sie do +-50% wobec mediany krainy
        private const float USecurity = 0.4f, UWar = 0.3f, UBurnt = 0.3f;

        private static bool _growthPatched;            // latka wpieta (raz na proces - to nie jest stan kampanii)
        internal static bool GrowthOn { get { var s = Settings.Current; return s != null && s.NaturalGrowthEnabled && _growthPatched; } }

        /// <summary>Stopa przyrostu jednej wsi i jej skladowe (punkty procentowe rocznie, o ile nie napisano inaczej).</summary>
        internal struct Growth
        {
            public bool Set;                // policzone przez postfiks (przyrost czynny)
            public bool Blocked;            // wies nie jest w stanie Normal (spalona, lupiona, pod przymusem): 0
            public float W, H, U;           // dobrobyt -0.5..+0.5; glod 0..1 (liczony takze, gdy wylaczony); niebezpieczenstwo 0..1
            public bool War; public float Security, Burnt;
            public float Base, Prosper, Hunger, Danger;     // G0; +GW x W; -GH x H (0 przy wylaczonym glodzie); -GU x U
            public float Raw;               // suma przed premiami gry
            public float Bonus;             // mnoznik premii gry (tylko przy dodatniej sumie); 1 = brak
            public float G;                 // stopa po premiach i obcieciu do korytarza
            public float Lo, Hi;            // korytarz z ustawien
            public float Refugees;          // hearth dziennie: powrot uchodzcow do wsi przy dnie
            public double Delta;            // hearth dziennie razem
        }

        private static readonly object _gLock = new object();
        private static int _gDay = int.MinValue;
        private static readonly Dictionary<Settlement, float> _gW = new Dictionary<Settlement, float>();
        private static readonly Dictionary<IFaction, bool> _gWar = new Dictionary<IFaction, bool>();
        private static int _gStumbles;
        private static bool _errGrowth, _errGrowthLog, _modelLogged;
        [ThreadStatic] private static Growth _gLast;       // ostatni rachunek postfiksu - linia dobowa czyta go zaraz po pytaniu modelu
        private static double _hearthLast; private static int _hearthLastDay = int.MinValue;

        private static int YearDays() { return Math.Max(28, CampaignTime.DaysInYear); }

        /// <summary>Zmiana dobowa hearth przy stopie `rate` % rocznie (plus powrot uchodzcow) - jedno miejsce, zeby pulap i wynik zaokraglaly sie tak samo.</summary>
        private static float Step(float hearth, float rate, float refugees)
        {
            return (float)((double)hearth * rate / 100.0 / YearDays() + refugees);
        }

        // pod _gLock: raz na dobe mediany dobrobytu (kraina x rodzaj warowni) i czysta pamiec wojen
        private static void DayCache()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _gDay) return;
            _gDay = day; _gW.Clear(); _gWar.Clear();
            var groups = new Dictionary<string, List<Settlement>>();
            foreach (var n in OutlawLaw.RegionNodes())
            {
                if (n == null || n.Town == null || n.Culture == null) continue;
                string key = n.Culture.StringId + (n.IsTown ? "|miasto" : "|zamek");
                List<Settlement> l;
                if (!groups.TryGetValue(key, out l)) groups[key] = l = new List<Settlement>();
                l.Add(n);
            }
            var ps = new List<float>();
            foreach (var l in groups.Values)
            {
                ps.Clear();
                foreach (var n in l) { float p = n.Town.Prosperity; ps.Add(p > 0f ? p : 0f); }       // takze NaN -> 0
                ps.Sort();
                float med = ps.Count % 2 == 1 ? ps[ps.Count / 2] : (ps[ps.Count / 2 - 1] + ps[ps.Count / 2]) * 0.5f;
                foreach (var n in l)
                {
                    float p = n.Town.Prosperity;
                    float w = med > 0f && p > 0f ? p / med - 1f : (med > 0f ? -1f : 0f);
                    if (float.IsNaN(w) || float.IsInfinity(w)) w = 0f;
                    _gW[n] = MBMath.ClampFloat(w, -WSpan, WSpan);
                }
            }
        }

        /// <summary>Dobrobyt warowni wobec mediany warowni tego samego rodzaju w jej krainie: -0.5..+0.5 (0 = mediana albo jedyna taka w krainie).</summary>
        internal static float ProsperityIndex(Settlement node)
        {
            if (node == null) return 0f;
            lock (_gLock)
            {
                DayCache();
                float w;
                return _gW.TryGetValue(node, out w) ? w : 0f;
            }
        }

        /// <summary>Czy frakcja jest dzis w wojnie z jakims krolestwem (ta sama regula co nedza regionu w OutlawLaw; bandyci i rody bez krolestwa sie nie licza).</summary>
        private static bool AtWarToday(IFaction f)
        {
            if (f == null) return false;
            lock (_gLock)
            {
                DayCache();
                bool w;
                if (_gWar.TryGetValue(f, out w)) return w;
                w = false;
                try { foreach (var k in Kingdom.All) if (k != null && k != f && !k.IsEliminated && f.IsAtWarWith(k)) { w = true; break; } }
                catch (Exception e)
                {
                    // frakcja, o ktorej gra nie umie odpowiedziec: liczymy jak pokoj (do konca doby), potkniecie do linii dobowej
                    _gStumbles++;
                    if (!_errGrowth) { _errGrowth = true; Log.Error("PopulationLaw.AtWarToday", e); }
                }
                _gWar[f] = w;
                return w;
            }
        }

        /// <summary>
        /// Rachunek jednej wsi. `gameBase` i `gameFactors` to wynik gry z chwili wejscia postfiksu (po latkach BK i BKROT):
        /// suma dodatkow razem z baza pasma i suma czynnikow.
        /// </summary>
        private static Growth GrowthOf(Village v, Settings s, float gameBase, float gameFactors)
        {
            var g = new Growth { Set = true, Bonus = 1f, Security = 100f };
            g.Hi = Math.Max(0f, s.GrowthMaxPercent); g.Lo = -Math.Max(0f, s.GrowthMaxDeclinePercent);
            float hearth = v.Hearth;
            if (!(hearth > 0f) || float.IsInfinity(hearth)) return g;       // takze NaN - niczego nie dopisujemy
            // rosnie tylko wies w stanie Normal - jak w grze, ktora baze pasma daje tylko jej; spalona (Looted), lupiona
            // (BeingRaided) i wies pod przymusem gracza (ForcedForVolunteers / ForcedForSupplies) maja 0
            if (v.VillageState != Village.VillageStates.Normal) { g.Blocked = true; return g; }

            Settlement node = v.Bound;      // region wsi: jej miasto albo zamek
            g.W = ProsperityIndex(node);
            if (node != null)
            {
                if (node.Town != null) { float sec = node.Town.Security; g.Security = sec >= 0f ? Math.Min(100f, sec) : 0f; }
                var vs = node.BoundVillages; int nv = 0, nb = 0;
                if (vs != null)
                    foreach (var o in vs)
                    {
                        if (o == null) continue;
                        nv++;
                        if (o.VillageState == Village.VillageStates.Looted || o.VillageState == Village.VillageStates.BeingRaided) nb++;
                    }
                g.Burnt = nv > 0 ? (float)nb / nv : 0f;
            }
            g.War = AtWarToday(node != null ? node.MapFaction : null);       // frakcja wsi to frakcja jej warowni (Village.MapFaction)
            g.U = MBMath.ClampFloat(USecurity * (1f - g.Security / 100f) + (g.War ? UWar : 0f) + UBurnt * g.Burnt, 0f, 1f);
            g.H = MBMath.ClampFloat(WinterBite.VillageCut(v), 0f, 1f);

            g.Base = s.GrowthBasePercent;
            g.Prosper = Math.Max(0f, s.GrowthProsperityWeight) * g.W;
            g.Danger = -Math.Max(0f, s.GrowthDangerWeight) * g.U;
            g.Hunger = s.GrowthHungerEnabled ? -Math.Max(0f, s.GrowthHungerWeight) * g.H : 0f;
            g.Raw = g.Base + g.Prosper + g.Hunger + g.Danger;
            float rate = g.Raw;
            if (rate > 0f)
            {
                // baza pasma jak w DefaultSettlementProsperityModel.CalculateHearthChangeInternal; reszta wyniku gry to premie
                float band = hearth < 300f ? 4f : (hearth < 600f ? 1.2f : 0.2f);
                float m = (1f + (gameBase - band) / BonusUnit) * (1f + gameFactors);
                if (float.IsNaN(m) || float.IsInfinity(m)) m = 1f;
                g.Bonus = MBMath.ClampFloat(m, BonusMin, BonusMax);
                rate *= g.Bonus;
            }
            if (float.IsNaN(rate)) rate = 0f;
            g.G = MBMath.ClampFloat(rate, g.Lo, g.Hi);
            g.Refugees = ScorchedEarth.RefugeeReturn(v);
            g.Delta = Step(hearth, g.G, g.Refugees);
            return g;
        }

        // linie dymka: [0] ludzie rocznie, [1] hearth rocznie (kultura wsi bez przelicznika ludzi)
        private static TextObject[] Lines(string what)
        {
            return new[] { new TextObject("{=!}" + what + " - people a year"), new TextObject("{=!}" + what + " - hearths a year") };
        }
        private static readonly TextObject[] _txtBirths = Lines("Births over deaths"), _txtProsper = Lines("Prosperity of the land"),
            _txtHunger = Lines("Hunger"), _txtDanger = Lines("War, raids and lawlessness"), _txtBonus = Lines("Lord's care (perks, buildings, laws)"),
            _txtLimit = Lines("Limit of natural growth"), _txtRefugees = Lines("Refugees returning");

        /// <summary>
        /// Linia opisu, ktora NIE wchodzi do wyniku: dopisana i od razu zdjeta bez opisu (ExplainedNumber nie umie inaczej
        /// pokazac liczby w innej jednostce niz wynik). Dymek gry pomija linie ponizej 0.01, a dobowa zmiana hearth to
        /// ok. 0.005 - dlatego linie mowia o ludziach na rok.
        /// </summary>
        private static void Line(ref ExplainedNumber r, double perYear, TextObject[] text, bool people)
        {
            float shown = people ? (float)Math.Round(perYear) : (float)Math.Round(perYear, 2);
            if (shown == 0f || float.IsNaN(shown) || float.IsInfinity(shown)) return;
            r.Add(shown, text[people ? 0 : 1], null);
            r.Add(-shown, null, null);
        }

        private static void Describe(ref ExplainedNumber r, Village v, Growth g)
        {
            if (g.Blocked) return;
            float k = PeoplePerHearth(v);
            bool people = k > 0f;
            double unit = (double)Math.Max(0f, v.Hearth) * (people ? k : 1f) / 100.0;       // ludzi (albo hearth) na 1 pkt % rocznie
            Line(ref r, unit * g.Base, _txtBirths, people);
            Line(ref r, unit * g.Prosper, _txtProsper, people);
            Line(ref r, unit * g.Hunger, _txtHunger, people);
            Line(ref r, unit * g.Danger, _txtDanger, people);
            double before = g.Raw;
            if (g.Raw > 0f) { before = (double)g.Raw * g.Bonus; Line(ref r, unit * (before - g.Raw), _txtBonus, people); }
            Line(ref r, unit * (g.G - before), _txtLimit, people);
            Line(ref r, (double)g.Refugees * YearDays() * (people ? k : 1f), _txtRefugees, people);
        }

        /// <summary>Ludzi na punkt hearth w krainie wsi (0 = brak przelicznika albo kalibracji); sam kalibracji nie wywoluje.</summary>
        private static float PeoplePerHearth(Village v)
        {
            if (v == null || v.Settlement == null || v.Settlement.Culture == null) return 0f;
            float[] k;
            if (!_k.TryGetValue(v.Settlement.Culture.StringId, out k) || k == null || k.Length == 0) return 0f;
            return k[0] > 0f && !float.IsInfinity(k[0]) ? k[0] : 0f;
        }

        /// <summary>
        /// Postfiks na DefaultSettlementProsperityModel.CalculateHearthChange(Village, bool), Priority.Last, wpinany po
        /// ScorchedEarth: biegnie po latkach BK (priorytet 400) i BKROT (200), widzi ich wynik i podmienia go na przyrost
        /// naturalny. Wynik dostaje limity ExplainedNumber rowne korytarzowi wsi, wiec model owijajacy baze (NavalDLC dopisuje
        /// perk gubernatora PO latkach) nie wyprowadzi go poza [-1.3; +0.8]. Wyjatek: ta jedna odpowiedz = 0, licznik potkniec,
        /// pierwszy wyjatek do logu - niczego nie gasimy.
        /// </summary>
        public static void GrowthPostfix(Village __0, bool __1, ref ExplainedNumber __result)
        {
            if (!GrowthOn || __0 == null) return;
            try
            {
                var s = Settings.Current;
                Growth g = GrowthOf(__0, s, __result.BaseNumber, __result.SumOfFactors);
                float delta = (float)g.Delta;
                if (float.IsNaN(delta) || float.IsInfinity(delta)) { delta = 0f; g.Delta = 0.0; }
                ExplainedNumber r;
                if (!__1) r = new ExplainedNumber(delta, false, null);
                else
                {
                    r = new ExplainedNumber(0f, true, null);
                    Describe(ref r, __0, g);
                    r.Add(delta, null, null);
                }
                // korytarz tej wsi jako limity wyniku (wies poza stanem Normal: dokladnie 0)
                float hearth = __0.Hearth > 0f && !float.IsInfinity(__0.Hearth) ? __0.Hearth : 0f;
                r.LimitMin(g.Blocked ? delta : Math.Min(delta, Step(hearth, g.Lo, 0f)));
                r.LimitMax(g.Blocked ? delta : Math.Max(delta, Step(hearth, g.Hi, g.Refugees)));
                __result = r;
                _gLast = g;
            }
            catch (Exception e)
            {
                _gStumbles++;
                if (!_errGrowth) { _errGrowth = true; Log.Error("PopulationLaw.GrowthPostfix", e); }
                __result = new ExplainedNumber(0f, __1, null);
            }
        }

        internal static void ApplyGrowth(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(DefaultSettlementProsperityModel), "CalculateHearthChange", new[] { typeof(Village), typeof(bool) });
                if (m == null || m.ReturnType != typeof(ExplainedNumber))
                {
                    Log.Info("PopulationLaw: przyrost naturalny - BRAK metody DefaultSettlementProsperityModel.CalculateHearthChange(Village, bool); hearth wsi liczy gra jak dotad.");
                    return;
                }
                h.Patch(m, postfix: new HarmonyMethod(typeof(PopulationLaw), nameof(GrowthPostfix)) { priority = Priority.Last });
                _growthPatched = true; _growthTarget = m;
                // stan z ustawien startowych; MCM wchodzi przy starcie kampanii - prawdziwy stan stoi co dobe w linii "Ludzie: przyrost naturalny".
                // Kolejnosc tez jest tu niepelna: BannerKings wpina swoje latki dopiero przy starcie kampanii - pelna stoi w linii
                // "PopulationLaw: czynny model dobrobytu" pierwszej doby.
                var s = Settings.Current;
                Log.Info("PopulationLaw: przyrost naturalny wsi zamiast stalej gry (+4 / +1.2 / +0.2 hearth dziennie) " + (GrowthOn ? "CZYNNY" : "wylaczony w ustawieniach")
                         + (s != null ? " - g = " + F(s.GrowthBasePercent) + " + " + F(s.GrowthProsperityWeight) + " x dobrobyt - " + F(s.GrowthDangerWeight) + " x niebezpieczenstwo"
                                        + (s.GrowthHungerEnabled ? " - " + F(s.GrowthHungerWeight) + " x glod" : " (glod wylaczony)")
                                        + " [% rocznie], korytarz -" + F(Math.Max(0f, s.GrowthMaxDeclinePercent)) + "..+" + F(Math.Max(0f, s.GrowthMaxPercent)) : "")
                         + "; latka na DefaultSettlementProsperityModel.CalculateHearthChange wpieta, na razie: " + PatchOrder() + ".");
            }
            catch (Exception e) { Log.Error("PopulationLaw.ApplyGrowth", e); }
        }

        private static System.Reflection.MethodBase _growthTarget;

        /// <summary>Latki na celu przyrostu w kolejnosci biegu (priorytet malejaco, potem kolejnosc wpiecia) - do logu.</summary>
        private static string PatchOrder()
        {
            try
            {
                var info = _growthTarget != null ? Harmony.GetPatchInfo(_growthTarget) : null;
                if (info == null) return "brak latek";
                Func<IEnumerable<Patch>, string> list = ps =>
                {
                    var names = ps.OrderByDescending(p => p.priority).ThenBy(p => p.index)
                                  .Select(p => p.owner + ":" + (p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.Name : "?") + "." + p.PatchMethod.Name + "(" + p.priority + ")").ToArray();
                    return names.Length > 0 ? string.Join(" -> ", names) : "brak";
                };
                return "postfiksy w kolejnosci biegu: " + list(info.Postfixes) + "; prefiksy: " + list(info.Prefixes)
                       + "; transpilery: " + info.Transpilers.Count + "; finalizery: " + info.Finalizers.Count;
            }
            catch (Exception e) { return "kolejnosci latek nie odczytano (" + e.GetType().Name + ")"; }
        }

        private static string F(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static string F3(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static string Sg(double v, string fmt) { return (v >= 0.0 ? "+" : "") + v.ToString(fmt, CultureInfo.InvariantCulture); }

        private sealed class RealmGrowth { public double People, Rate, Gain; public int Villages; }

        /// <summary>
        /// Raz na dobe, z ksiegi "Ludzie:" (PRZED wyzerowaniem licznikow doby PeopleUnit): pyta czynny model gry o zmiane hearth
        /// kazdej wsi - to samo, co dopisze jej dobowy tick - i pisze linie "Ludzie: przyrost naturalny". Niczego nie zmienia.
        /// </summary>
        internal static void GrowthDaily(int day)
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                if (s == null) return;
                bool on = GrowthOn;
                int days = YearDays();

                if (!_modelLogged)
                {
                    _modelLogged = true;
                    try
                    {
                        var model = Campaign.Current.Models.SettlementProsperityModel;
                        var mt = model != null ? model.GetType() : null;
                        var mm = mt != null ? mt.GetMethod("CalculateHearthChange", new[] { typeof(Village), typeof(bool) }) : null;
                        var decl = mm != null ? mm.DeclaringType : null;
                        Log.Info("PopulationLaw: czynny model dobrobytu gry: " + (mt != null ? mt.FullName : "?") + "; zmiane hearth liczy " + (decl != null ? decl.FullName : "?")
                                 + (decl == typeof(DefaultSettlementProsperityModel) ? " - latany model bazowy, po naszej latce nikt nie dopisuje"
                                                                                     : " - ten model dopisuje PO naszej latce; wynik trzymaja w korytarzu limity ExplainedNumber")
                                 + "; " + PatchOrder() + ".");
                    }
                    catch (Exception e) { Log.Error("PopulationLaw.GrowthDaily(model)", e); }
                }

                // wsie
                int n = 0, up = 0, down = 0, flat = 0, blocked = 0, war = 0, burnt = 0, capHi = 0, capLo = 0, refugees = 0, bonusN = 0, hungerN = 0, tiny = 0, noK = 0, stumbles = 0;
                double hearthAll = 0, weightAll = 0, weightLive = 0, peopleAll = 0, modelHearth = 0, takenHearth = 0, gainPeople = 0;
                double backHearth = 0, backPeople = 0;      // powrot uchodzcow (+0.5 hearth przy dnie) - siedzi w wyniku modelu, ale przyrostem naturalnym nie jest
                double wBase = 0, wProsper = 0, wDanger = 0, wHunger = 0, wDryHunger = 0, wBonus = 0, wSec = 0, wRate = 0, sumH = 0;
                float bonusMin = float.MaxValue, bonusMax = float.MinValue;
                var rates = new List<float>();
                var levels = new int[3];
                var realms = new Dictionary<string, RealmGrowth>();
                float gh = Math.Max(0f, s.GrowthHungerWeight);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage || st.Village == null) continue;
                    var v = st.Village;
                    try
                    {
                        float hearth = v.Hearth;
                        if (!(hearth > 0f)) continue;
                        float k = PeoplePerHearth(v);
                        if (!(k > 0f)) noK++;
                        double weight = k > 0f ? (double)hearth * k : hearth;        // ludzie wsi; bez przelicznika - hearth
                        _gLast = default(Growth);
                        float d = v.HearthChange;                                   // czynny model gry
                        Growth g = _gLast;
                        float taken = (float)(hearth + d) - hearth;                 // tyle przyjmie float32 wsi
                        n++; hearthAll += hearth; weightAll += weight; if (k > 0f) peopleAll += weight;
                        levels[hearth >= 600f ? 2 : (hearth >= 200f ? 1 : 0)]++;       // progi produkcji gry (Village.GetHearthLevel)
                        modelHearth += d; takenHearth += taken;
                        double gain = (double)taken * (k > 0f ? k : 0f);
                        gainPeople += gain;
                        if (d > 0f) up++; else if (d < 0f) down++; else flat++;
                        if (d != 0f && taken == 0f) tiny++;
                        // powrot uchodzcow liczony osobno: jedna wies Reach ponizej 40 hearth to +222 ludzi dziennie (0.5 x k), ponad
                        // jedna trzecia przyrostu naturalnego calego swiata w pokoju - w sumie "+N ludzi" udawalby przyrost i psul stope krainy
                        float back = g.Set ? g.Refugees : 0f;
                        double backGain = (double)back * (k > 0f ? k : 0f);
                        backHearth += back; backPeople += backGain;
                        // stopa roczna wsi: przy czynnym przyroscie stopa naturalna g, przy wylaczonym - z wyniku gry
                        float rate = g.Set ? g.G : (float)((double)d * days * 100.0 / hearth);
                        RealmGrowth rg; string c = st.Culture != null ? st.Culture.StringId : "?";
                        if (!realms.TryGetValue(c, out rg)) realms[c] = rg = new RealmGrowth();
                        rg.Villages++; rg.People += weight; rg.Rate += weight * rate; rg.Gain += gain - backGain;
                        if (!g.Set) continue;                                       // przyrost wylaczony: tylko suma wyniku gry
                        rates.Add(g.G); wRate += weight * g.G;
                        if (g.Blocked) { blocked++; continue; }
                        weightLive += weight;
                        if (g.War) war++;
                        if (g.Burnt > 0f) burnt++;
                        wBase += weight * g.Base; wProsper += weight * g.Prosper; wDanger += weight * g.Danger; wHunger += weight * g.Hunger;
                        wSec += weight * g.Security;
                        if (g.H > 0f) { hungerN++; sumH += g.H; wDryHunger += weight * gh * g.H; }
                        if (g.Raw > 0f)
                        {
                            wBonus += weight * g.Bonus;
                            if (g.Bonus != 1f) bonusN++;
                            if (g.Bonus < bonusMin) bonusMin = g.Bonus;
                            if (g.Bonus > bonusMax) bonusMax = g.Bonus;
                            if (g.Raw * g.Bonus > g.Hi) capHi++;
                        }
                        else { wBonus += weight; if (g.Raw < g.Lo) capLo++; }
                        if (g.Refugees > 0f) refugees++;
                    }
                    catch (Exception e)
                    {
                        stumbles++;
                        if (!_errGrowthLog) { _errGrowthLog = true; Log.Error("PopulationLaw.GrowthDaily(" + st.StringId + ")", e); }
                    }
                }
                _gLast = default(Growth);

                // rozliczenie zmiany hearth wsi od wczoraj (stan w pamieci - pierwsza doba po wczytaniu bez rozliczenia)
                string ledger;
                if (_hearthLastDay == day - 1 && _hearthLast > 0.0)
                {
                    double change = hearthAll - _hearthLast, moved = PeopleUnit.DayNetHearth();
                    ledger = F(hearthAll) + ", od wczoraj " + Sg(change, "0.###") + " = " + (on ? "przyrost naturalny " : "wynik gry ") + Sg(takenHearth - backHearth, "0.###")
                             + (backHearth > 0.0 ? ", powrot uchodzcow " + Sg(backHearth, "0.###") : "")
                             + ", ruch ludzi (tabory, wyrzutki, pobor, zadania, incydenty) " + Sg(moved, "0.###")
                             + ", RESZTA " + Sg(change - takenHearth - moved, "0.###") + " (inwestycje BetterEconomy +10/25/50 za zloto, rabunki, zerowanie armii, dno 10 hearth)";
                }
                else ledger = F(hearthAll) + ", rozliczenie zmiany od jutra (pierwsza doba po wczytaniu)";
                _hearthLast = hearthAll; _hearthLastDay = day;

                // miasta: stan wobec tego, co dalby dzisiejszy dobrobyt
                double townState = 0, townLive = 0; int towns = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown || st.Town == null || st.Culture == null) continue;
                    float[] k;
                    if (!_k.TryGetValue(st.Culture.StringId, out k) || k == null || k.Length < 2) continue;
                    float live = Math.Max(0f, st.Town.Prosperity) * k[1], p;
                    if (float.IsNaN(live) || float.IsInfinity(live)) live = 0f;
                    towns++; townLive += live;
                    townState += _townPeople.TryGetValue(st.StringId, out p) ? p : live;
                }
                string townNote = !Calibrated ? "ludnosc nieskalibrowana"
                    : TownsFrozen ? (townState / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln w " + towns + " miastach - stan (zamrozony); z dzisiejszego dobrobytu byloby "
                                    + (townLive / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln (" + Sg(townLive - townState, "0") + " ludzi dryfu od zalozenia stanu)"
                                  : (townLive / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln w " + towns + " miastach - dobrobyt x k (stan wylaczony w MCM)";

                var sb = new StringBuilder();
                double yearRate = peopleAll > 0 ? gainPeople * days * 100.0 / peopleAll : (hearthAll > 0 ? takenHearth * days * 100.0 / hearthAll : 0.0);
                if (!on)
                {
                    sb.Append("Ludzie: przyrost naturalny WYLACZONY (").Append(_growthPatched ? "Natural Growth Enabled w MCM" : "latka nie wpieta").Append("; dzien ").Append(day)
                      .Append(") - hearth wsi liczy gra: ").Append(Sg(gainPeople, "0")).Append(" ludzi dzis (").Append(Sg(takenHearth, "0.###")).Append(" hearth na ").Append(n)
                      .Append(" wsi = ").Append(Sg(yearRate, "0.##")).Append("% rocznie)");
                }
                else
                {
                    double gAvg = weightAll > 0 ? wRate / weightAll : 0.0;
                    double natPeople = gainPeople - backPeople, natHearth = takenHearth - backHearth;       // sam przyrost naturalny
                    sb.Append("Ludzie: przyrost naturalny ").Append(Sg(natPeople, "0")).Append(" ludzi (g srednio ").Append(Sg(gAvg, "0.###")).Append("% rocznie, wazone ludnoscia wsi; dzien ")
                      .Append(day).Append("; ").Append(Sg(natHearth, "0.####")).Append(" hearth na ").Append(n).Append(" wsi, w rok ok. ").Append(Sg(natPeople * days, "0")).Append(" ludzi)");
                    if (backHearth > 0.0)
                        sb.Append("; osobno powrot uchodzcow ").Append(Sg(backPeople, "0")).Append(" ludzi (").Append(Sg(backHearth, "0.#")).Append(" hearth w ").Append(refugees)
                          .Append(" wsiach przy dnie - regula ScorchedEarth do kroku 4, hearth z niczego, nie przyrost)");
                    rates.Sort();
                    sb.Append(" | wsie: rosnie ").Append(up).Append(", kurczy sie ").Append(down).Append(", bez zmiany ").Append(flat).Append(" (spalone, lupione albo pod przymusem ").Append(blocked).Append(')');
                    if (rates.Count > 0)
                        sb.Append("; g od ").Append(Sg(rates[0], "0.##")).Append(" przez ").Append(Sg(rates[rates.Count / 2], "0.##")).Append(" (mediana) do ").Append(Sg(rates[rates.Count - 1], "0.##"));
                    if (weightLive > 0)
                    {
                        sb.Append(" | skladowe g wsi niespalonych (pkt % rocznie, wazone ludnoscia): podstawa ").Append(Sg(wBase / weightLive, "0.###"))
                          .Append(", dobrobyt wobec mediany krainy ").Append(Sg(wProsper / weightLive, "0.###"))
                          .Append(", niebezpieczenstwo ").Append(Sg(wDanger / weightLive, "0.###")).Append(" (wsi krain w wojnie ").Append(war).Append(", wsi w regionach ze spalonymi ").Append(burnt)
                          .Append(", bezpieczenstwo warowni srednio ").Append(F(wSec / weightLive)).Append(')');
                        if (s.GrowthHungerEnabled) sb.Append(", glod ").Append(Sg(wHunger / weightLive, "0.###")).Append(" (zima tnie plon ").Append(hungerN).Append(" wsi)");
                        else sb.Append(", glod WYLACZONY (na sucho: zima tnie plon ").Append(hungerN).Append(" wsi").Append(hungerN > 0 ? ", srednio o " + F(100.0 * sumH / hungerN) + "%" : "")
                               .Append("; z waga ").Append(F(gh)).Append(" byloby ").Append(Sg(-wDryHunger / weightLive, "0.###")).Append(')');
                        sb.Append(", premie gry srednio x").Append(F3(wBonus / weightLive)).Append(" (wsi z premia ").Append(bonusN);
                        if (bonusN > 0) sb.Append(", od x").Append(F(bonusMin)).Append(" do x").Append(F(bonusMax));
                        sb.Append("), pulap +").Append(F(Math.Max(0f, s.GrowthMaxPercent))).Append(" obcial ").Append(capHi).Append(" wsi, dno -").Append(F(Math.Max(0f, s.GrowthMaxDeclinePercent)))
                          .Append(" obcielo ").Append(capLo).Append(" wsi, powrot uchodzcow +0.5 hearth: ").Append(refugees).Append(" wsi");
                    }
                }
                if (realms.Count > 0)
                {
                    var parts = realms.OrderByDescending(kv => kv.Value.People)
                                      .Select(kv => kv.Key + " " + Sg(kv.Value.People > 0 ? kv.Value.Rate / kv.Value.People : 0.0, "0.##") + "% (" + Sg(kv.Value.Gain, "0") + ")");
                    sb.Append(" | krainy g rocznie (ludzi dzis): ").Append(string.Join(", ", parts.ToArray()));
                }
                sb.Append(" | hearth wsi swiata ").Append(ledger);
                sb.Append(" | wsie wedle progow produkcji (ponizej 200 / 200-599 / od 600 hearth): ").Append(levels[0]).Append(" / ").Append(levels[1]).Append(" / ").Append(levels[2]);
                sb.Append(" | float32: model ").Append(Sg(modelHearth, "0.####")).Append(" hearth, wsie przyjmuja ").Append(Sg(takenHearth, "0.####")).Append(backHearth > 0.0 ? " (oba z powrotem uchodzcow; " : " (")
                  .Append("wsi z krokiem ponizej polowy kroku floata: ").Append(tiny).Append(')');
                sb.Append(" | miasta: ").Append(townNote);
                if (noK > 0) sb.Append(" | wsi bez przelicznika ludzi: ").Append(noK);
                int st0 = _gStumbles; _gStumbles = 0;
                if (st0 + stumbles > 0) sb.Append(" | potkniecia: rachunek wsi ").Append(st0).Append(", linia dobowa ").Append(stumbles);
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("PopulationLaw.GrowthDaily", e); }
        }

        private static float DailyRent(float people)
        {
            int days = Math.Max(28, CampaignTime.DaysInYear);
            return people * Math.Max(0f, Settings.Current.PopulationRentPerHead) / days;
        }

        // ------------------------------------------------------------ renty jako przeplyw
        // Audyt 04.10: podmiana podatku w modelach (a) nie wpinala sie (BK ma dwie wersje
        // CalculateVillageTaxFromIncome - AmbiguousMatch wywracal cale ApplyAll), (b) bylaby zlotem
        // z niczego (~7 mln/dzien). Teraz renta to PRZEPLYW: raz dziennie pan bierze z kasy wsi/miasta
        // to, co mu sie nalezy wedle ludnosci, najwyzej `PopulationRentMaxShare` tego, co osada ma.
        // Podatki gry (BK/vanilla) zostaja, jak byly. Spalona albo lupiona wies nie placi.
        internal static void ApplyAll(Harmony h)
        {
            Log.Info("PopulationLaw: renty od ludnosci jako przeplyw z kasy osad do panow " + (On ? "CZYNNE" : "wylaczone w MCM") + " (bez latek modeli podatku).");
        }

        /// <summary>Renty zaplacone dzis kazdemu rodowi (do powinnosci wobec korony).</summary>
        internal static readonly Dictionary<Clan, int> RentToday = new Dictionary<Clan, int>();

        // ------------------------------------------------------------ podatek ludnosci miasta BK -> renta (wpis 49)
        [ThreadStatic] private static int _taxDepth;
        public static void TownTaxPrefix() { _taxDepth++; }
        public static Exception TownTaxFinalizer(Exception __exception) { if (_taxDepth > 0) _taxDepth--; return __exception; }
        public static void TownTaxPostfix(Town __0, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            if (_taxDepth > 1) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !On || !s.RentReplacesTownTax) return;
                // wpis 51 (ZRODLA-DOCHODU.md C1): zerujemy TYLKO podatki klas ludnosci i cla od konsumpcji (z niczego);
                // zostaje to, co ktos naprawde placi: podatek od cudzych warsztatow (BK pobiera go od wlascicieli), dochod
                // kopalni i koszt materialow budow - inaczej te pieniadze znikaly w nicosc, a pan nie placil za budowy
                float kept = KeptTownLines(__0);
                __result = new TaleWorlds.CampaignSystem.ExplainedNumber(kept, __1, _txtRent);
            }
            catch { }
        }
        private static readonly TaleWorlds.Localization.TextObject _txtRent = new TaleWorlds.Localization.TextObject("{=!}Town rents are paid from the town purse (see daily rents)");

        private static object _bkCfg; private static System.Reflection.MethodInfo _wsTax, _mining, _materials, _popData; private static Type _bldT; private static bool _bkResolved;

        /// <summary>Linie podatku miasta BK placone przez kogos (warsztaty cudzych wlascicieli, kopalnie, materialy budow) x autonomia.</summary>
        private static float KeptTownLines(Town town)
        {
            try
            {
                if (town == null) return 0f;
                if (!_bkResolved)
                {
                    _bkResolved = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    _bkCfg = cfgT != null ? AccessTools.Property(cfgT, "Instance").GetValue(null, null) : null;
                    var cfm = _bkCfg != null ? AccessTools.Property(cfgT, "ClanFinanceModel").GetValue(_bkCfg, null) : null;
                    if (cfm != null) { _wsTax = AccessTools.Method(cfm.GetType(), "GetWorkshopTaxes", new[] { typeof(TaleWorlds.CampaignSystem.Settlements.Workshops.Workshop) }); _wsModel = cfm; }
                    _bldT = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                    if (_bldT != null) { _mining = AccessTools.Method(_bldT, "GetMiningRevenue", new[] { typeof(Town) }); _materials = AccessTools.Method(_bldT, "GetMaterialExpenses", new[] { typeof(Town) }); }
                    var pm = _bkCfg != null ? AccessTools.Property(cfgT, "PopulationManager").GetValue(_bkCfg, null) : null;
                    if (pm != null) { _popMgr = pm; _popData = AccessTools.Method(pm.GetType(), "GetPopData", new[] { typeof(Settlement) }); }
                }
                float sum = 0f;
                var leader = town.OwnerClan != null ? town.OwnerClan.Leader : null;
                if (_wsTax != null)
                    foreach (var w in town.Workshops)
                        if (w != null && w.Owner != null && w.Owner != leader)
                            sum += Convert.ToSingle(_wsTax.Invoke(_wsModel, new object[] { w }));
                if (_bldT != null && Campaign.Current != null)
                {
                    if (_getBeh == null) _getBeh = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(_bldT);
                    var get = _getBeh;
                    var beh = get.Invoke(Campaign.Current, null);
                    if (beh != null)
                    {
                        if (_mining != null) sum += Convert.ToSingle(_mining.Invoke(beh, new object[] { town }));
                        if (_materials != null) sum -= Convert.ToSingle(_materials.Invoke(beh, new object[] { town }));
                    }
                }
                if (_popData != null)
                {
                    var pd = _popData.Invoke(_popMgr, new object[] { town.Settlement });
                    if (pd != null) { float aut = Traverse.Create(pd).Property("Autonomy").GetValue<float>(); if (aut > 0f) sum *= Math.Max(0f, 1f - 0.6f * aut); }
                }
                return sum;
            }
            catch { return 0f; }
        }
        private static object _wsModel, _popMgr; private static System.Reflection.MethodInfo _getBeh;

        private static System.Reflection.MethodInfo _getPolicy; private static object _policyMgr; private static bool _polResolved;
        /// <summary>Mnoznik renty z dekretu podatkowego BK osady (1 gdy brak BK albo dekretu).</summary>
        internal static float TaxDecree(Settlement st)
        {
            try
            {
                if (!_polResolved)
                {
                    _polResolved = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    var cfg = cfgT != null ? AccessTools.Property(cfgT, "Instance").GetValue(null, null) : null;
                    _policyMgr = cfg != null ? AccessTools.Property(cfgT, "PolicyManager").GetValue(cfg, null) : null;
                    if (_policyMgr != null) _getPolicy = AccessTools.Method(_policyMgr.GetType(), "GetPolicy", new[] { typeof(Settlement), typeof(string) });
                }
                if (_getPolicy == null || st == null) return 1f;
                var pol = _getPolicy.Invoke(_policyMgr, new object[] { st, "tax" });
                if (pol == null) return 1f;
                var type = Traverse.Create(pol).Property("Policy").GetValue();
                string name = type != null ? type.ToString() : "Standard";
                var s = Settings.Current;
                if (name == "Low") return Math.Max(0f, s.RentTaxLow);
                if (name == "High") return Math.Max(0f, s.RentTaxHigh);
                if (name == "Exemption") return Math.Max(0f, s.RentTaxExemption);
                return 1f;
            }
            catch { return 1f; }
        }

        internal static void ApplyTownTax(HarmonyLib.Harmony h)
        {
            int n = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.SettlementTaxModel).IsAssignableFrom(t)) continue;
                        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            if (m.Name != "CalculateTownTax" || m.IsAbstract) continue;
                            var ps = m.GetParameters();
                            if (ps.Length != 2 || ps[0].ParameterType != typeof(Town) || ps[1].ParameterType != typeof(bool)) continue;
                            h.Patch(m, prefix: new HarmonyLib.HarmonyMethod(typeof(PopulationLaw), nameof(TownTaxPrefix)) { priority = HarmonyLib.Priority.First },
                                       postfix: new HarmonyLib.HarmonyMethod(typeof(PopulationLaw), nameof(TownTaxPostfix)) { priority = HarmonyLib.Priority.Last },
                                       finalizer: new HarmonyLib.HarmonyMethod(typeof(PopulationLaw), nameof(TownTaxFinalizer)));
                            n++;
                        }
                    }
                    catch { }
                }
            }
            Log.Info("PopulationLaw: podatek ludnosci miasta (Walled Demesnes) zastapiony renta z kasy miasta w " + n + " modelach podatku.");
        }

        internal static void Daily()
        {
            RentToday.Clear();   // wpis 86 (audyt pkt 7): przy wylaczonych rentach nie zostaja stare kwoty w dochodzie
            TownsDaily();        // demografia krok 9a: stan ludzi miast PRZED rentami (takze przy wylaczonych rentach - czyta go ksiega "Ludzie:")
            if (!On) return;
            try
            {
                var s = Settings.Current;
                float share = Math.Max(0f, Math.Min(1f, s.PopulationRentMaxShare));
                var pop = new Dictionary<string, float>();
                var due = new Dictionary<string, float>();
                var paid = new Dictionary<string, float>();
                RentToday.Clear();
                long totalPaid = 0, totalDue = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Culture == null || !(st.IsVillage || st.IsTown)) continue;
                    float p = PeopleOf(st);
                    if (p <= 0f) continue;
                    string c = st.Culture.StringId;
                    float v; pop.TryGetValue(c, out v); pop[c] = v + p;
                    float rent = DailyRent(p);
                    due.TryGetValue(c, out v); due[c] = v + rent;
                    totalDue += (long)rent;
                    try
                    {
                        if (st.IsVillage && (st.Village.VillageState == Village.VillageStates.Looted || st.Village.VillageState == Village.VillageStates.BeingRaided)) continue;
                        var lord = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                        if (lord == null || !lord.IsAlive) continue;
                        int gold = st.SettlementComponent != null ? st.SettlementComponent.Gold : 0;
                        // Wpis 49 (Jeff 04.10: "tak" - jedno zrodlo dochodu z ziemi): podatek ludnosci miasta BK ("Walled Demesnes",
                        // z niczego) wylaczony (TownTaxPostfix); pan bierze z MIASTA czesc kasy ponad prog bogactwa kupcow
                        // (BK BKProsperityModel: kasa < 20 000 = do -2 dobrobytu dziennie) - miasto nie bankrutuje i nie traci dobrobytu.
                        // Wies placi czesc swojej kiesy (PopulationRentMaxShare, 0.2 - wczesniej 0.5 oproznialo wsie w kilka dni).
                        float takeShare = share;
                        if (st.IsTown && st.Town != null)
                        {
                            gold = Math.Max(0, gold - (int)Math.Max(0f, s.TownRentFloorGold));
                            takeShare = Math.Max(0f, Math.Min(1f, s.TownRentShare));
                        }
                        // wpis 54: dekret podatkowy lenna BK (Low/Standard/High/Exemption) steruje renta
                        float decree = TaxDecree(st);
                        int pay = (int)Math.Min(rent * decree, gold * takeShare * decree);
                        if (pay <= 0) continue;
                        GiveGoldAction.ApplyForSettlementToCharacter(st, lord, pay, true);
                        { int r0; RentToday.TryGetValue(st.OwnerClan, out r0); RentToday[st.OwnerClan] = r0 + pay; }
                        paid.TryGetValue(c, out v); paid[c] = v + pay;
                        totalPaid += pay;
                    }
                    catch { }
                }
                var parts = pop.OrderByDescending(kv => kv.Value).Select(kv =>
                {
                    float pd; paid.TryGetValue(kv.Key, out pd);
                    return kv.Key + " " + (kv.Value / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M " + (int)pd + "/" + (int)due[kv.Key];
                });
                Log.Info("Ludnosc: dzien " + (int)CampaignTime.Now.ToDays + " | " + (pop.Values.Sum() / 1e6f).ToString("0.0", CultureInfo.InvariantCulture)
                         + " mln | renty zaplacone " + totalPaid + " z naleznych " + totalDue + " zl (z kasy osad) | kraina ludnosc zaplacone/nalezne: " + string.Join(", ", parts.ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("PopulationLaw.Daily", e); }
        }

        internal static string Export()
        {
            if (_k.Count == 0) return "";
            var sb = new StringBuilder("v1");
            foreach (var kv in _k)
                sb.Append('|').Append(kv.Key).Append('=').Append(kv.Value[0].ToString("R", CultureInfo.InvariantCulture))
                  .Append(':').Append(kv.Value[1].ToString("R", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            try
            {
                _k.Clear();
                if (string.IsNullOrEmpty(data)) return;     // stary save - kalibracja z obecnego stanu swiata
                foreach (var part in data.Split('|').Skip(1))
                {
                    int eq = part.IndexOf('='); int col = part.LastIndexOf(':');
                    if (eq <= 0 || col <= eq) continue;
                    float a, b;
                    if (float.TryParse(part.Substring(eq + 1, col - eq - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out a)
                        && float.TryParse(part.Substring(col + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                        _k[part.Substring(0, eq)] = new[] { a, b };
                }
            }
            catch (Exception e) { Log.Error("PopulationLaw.Import", e); }
        }
    }
}
