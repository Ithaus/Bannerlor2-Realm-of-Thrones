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
    /// (CalculateVillageTaxFromIncome) = ludzie x `PopulationRentPerHead` / dni roku. Dochod zamkow bez zmian
    /// (ich dochod to wsie). Cla od handlu bez zmian.
    /// Uwaga (projekt etapu 2 rozdz. 6): "bez zmian" dotyczy DOCHODU zamku, nie jego kasy - kase zamku sciagal do celu regulator gry
    /// (DefaultSettlementEconomyModel.GetTownGoldChange, opis w SoldierPay przy tarczy zoldu); od paczki 110 (CastlePurse) regulator zamku
    /// dziala tylko w dol (dosypka do zapasu), a nadwyzke ponad zapas kupcow zdejmuje zawor zamku - do pana (dopisane do RentToday).
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
            { "freefolk",   new Region(300000f,  0f) },     // E4/3 (Jeff 05.10, SP:361): Wolni Ludzie 300 tys. (walcza wszyscy dorosli - E4/9); dotad 150 tys.
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
            _k.Clear(); RentToday.Clear(); ZeroDay169(); _kTab.Clear(); _kTabDay = -1; _errCalibrate = false;
            // wpis 87 (audyt pkt 4): BK tworzy nowe PopulationManager/PolicyManager przy kazdej grze - stare referencje dawaly
            // dekret podatkowy zawsze Standard i pomijaly autonomie po wczytaniu drugiego save'a bez restartu
            _bkResolved = false; _popMgr = null; _popData = null; _polResolved = false; _policyMgr = null; _getPolicy = null;
            // E4/4 (109n): pamiec doby przyrostu i liczniki (stan miast trzyma PeopleBook - czyszczony tam; latki zostaja wpiete - _growthPatched nie jest stanem kampanii)
            _townSeeded = 0; _errTowns = false; _townStumbles = 0;
            lock (_gLock) { _gDay = int.MinValue; _gW.Clear(); _gWar.Clear(); }
            _gStumbles = 0; _floorStumbles = 0; _errGrowth = false; _errGrowthLog = false; _errFloor = false; _modelLogged = false;
        }

        private static void Calibrate()
        {
            if (_k.Count > 0) return;
            float scale = Math.Max(0f, Settings.Current.PopulationScale);
            // E4/3 (projekt 4.10, "Pogodzone"): pulap gestosci wsi - zaden punkt mapy nie znaczy wiecej ludzi niz najgesciej zaludniona kraina (Qarth 629
            // ludzi na hearth); kraina ponad pulapem (Wyspy Letnie: 3 wsie po 50 hearth = 4 500/hearth) traci nadwyzke tabeli "poza mape", udzial miast zostaje
            float cap = Math.Max(0f, Settings.Current.PopulationMaxPerHearth);
            var cut = new List<string>(); double offMap = 0;
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
            float thrOpening = 0f;
            foreach (var kv in Table)
            {
                float h, p;
                hearth.TryGetValue(kv.Key, out h); prosp.TryGetValue(kv.Key, out p);
                float pop = kv.Value.Pop * scale;
                // E4/9 (5.4, U15b): nowa kampania - niewolni Zelaznych Wysp to CZESC ludzi tabeli krainy (nie dodatek): k wsi i miast wysp liczone z reszty, niewolni
                // na konto NIEWOLNI ksiegi ludzi ponizej (SeedThralls - to samo wywolanie, ten sam warunek: nigdy odjete z DOM bez konta albo odwrotnie)
                float thr = 0f;
                try { thr = ThrallLaw.OpeningFor(kv.Key, pop, h > 0f); } catch (Exception e) { thr = 0f; Log.Error("PopulationLaw.Calibrate(E4/9)", e); }
                if (thr > 0f) { pop -= thr; thrOpening += thr; }
                float urban = p > 0f ? kv.Value.Urban : 0f;
                if (h <= 0f) urban = p > 0f ? 1f : 0f;                      // kraina bez wsi - wszystko w miastach
                float kv0 = h > 0f ? pop * (1f - urban) / h : 0f;
                float kv1 = p > 0f ? pop * urban / p : 0f;
                if (cap > 0f && kv0 > cap)
                {
                    float f = cap / kv0;
                    cut.Add(kv.Key + " " + (int)kv0 + " -> " + (int)cap + " ludzi na hearth (" + (pop / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M -> "
                            + (pop * f / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M)");
                    offMap += (double)pop * (1f - f);
                    kv0 = cap; kv1 *= f; pop *= f;
                }
                _k[kv.Key] = new[] { kv0, kv1 };
                parts.Add(kv.Key + " " + (pop / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M (wies " + (int)kv0 + "/hearth, miasto " + (int)kv1 + "/dobrobyt)"
                          + (thr > 0f ? " + niewolni " + thr.ToString("0", CultureInfo.InvariantCulture) + " (czesc tabeli " + (kv.Value.Pop * scale / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M, E4/9)" : ""));
            }
            Log.Info("PopulationLaw: kalibracja ludnosci - " + string.Join("; ", parts.ToArray())
                     + (cut.Count > 0 ? " | obcieto (pulap " + (int)cap + " ludzi na punkt hearth wsi, E4/3): " + string.Join(", ", cut.ToArray()) + "; poza mapa razem "
                                        + (offMap / 1e6).ToString("0.00", CultureInfo.InvariantCulture) + " mln"
                                      : " | pulap gestosci wsi (E4/3): " + (cap > 0f ? (int)cap + " ludzi na hearth - bez obciec" : "wylaczony")) + ".");
            // E4/3 (U3): k na osade z dnia kalibracji - do ksiegi ludzi (PeopleBook "v:"); zapis z "v:" zostaje
            try { PeopleBook.SetUnitsFromCalibration(); } catch (Exception e) { Log.Error("PopulationLaw.Calibrate(k na osade)", e); }
            // E4/4 (9a): ludzie miast zapisani jako stan od razu, z dobrobytu tej samej chwili co kalibracja - suma miast krainy to jej udzial miejski z tabeli
            SeedTowns("przy kalibracji");
            // E4/9 (5.4): bilans otwarcia niewolnych - konto NIEWOLNI regionow wysp wedlug ich ludzi (po k i stanie miast); wlasny try (SeedThralls nie rzuca)
            if (thrOpening > 0f) { try { PeopleBook.SeedThralls(thrOpening); } catch (Exception e) { Log.Error("PopulationLaw.Calibrate(E4/9 SeedThralls)", e); } }
        }

        // ------------------------------------------------------------ 174.3: ludnosc z tabeli krain (suma swiata stala)
        // PeopleOf liczy ludzi z hearth wsi i dobrobytu przez wspolczynnik z pierwszej kalibracji (_k, w zapisie) - rosnie z hearth i dobrobytem:
        // w A171 swiat 52.5 -> 59.0 mln w 39 dob, Lotus Bay 0.75 -> 2.2 mln (audyt 09.10: "nowego modelu nie wolno na niej oprzec").
        // TablePeopleOf: ten sam wzor, ale wspolczynnik liczony co dobe z dzisiejszych sum hearth i dobrobytu kultury - suma swiata = tabela krain
        // x PopulationScale zawsze; zmienia sie tylko podzial miedzy osady. _k i PeopleOf bez zmian (renty, ksiega ludzi).
        private static readonly Dictionary<string, float[]> _kTab = new Dictionary<string, float[]>();
        private static int _kTabDay = -1;
        internal static readonly HashSet<string> WesterosCultures = new HashSet<string> { "battania", "river", "reach", "aserai", "vlandia", "vale", "stormlands", "sturgia", "crownlands", "dragonstone", "freefolk", "nightswatch", "skagosi" };

        internal static float TablePeopleOf(Settlement s)
        {
            if (s == null || s.Culture == null) return 0f;
            // E4/4 ("Pogodzone", rozdz. 2 projektu): JEDNA definicja ludzi osady - przy stanie miast (TownPeopleFrozen) ludzie ksiegi (PeopleOf: wies hearth x k_v,
            // miasto stan); suma swiata nie jest juz stala, tylko idzie za przyrostem naturalnym i ruchem ludzi. WorkshopLaw.TownHands bierze max(dawny wzor rak
            // z dobrobytu - LegacyTownHands, z ludzi rynku), wiec rece nie spadna ponizej wzoru sprzed 174.3 - ALE wobec wczoraj (ten wzor tabeli, suma swiata stala)
            // zmienia sie suma i podzial ludnosci rynku (poprawka po recenzji E4/4): w nowej kampanii w dniu kalibracji oba wzory daja to samo; w STARYM zapisie
            // hearth napuchniety stala gry zostaje (projekt: bez przeliczania wstecz), wiec ludnosc rynku pierwszej sesji skacze do ludzi ksiegi (test A zbioru 30:
            // 69.1 mln wobec 52.5 mln tabeli - rynki srednio ok. +30%, rece swiata wedlug szacunku recenzji ok. 5950 -> ok. 7800 roboczodni; za nimi czytelnicy
            // TownHands: TownCrafts, TownFletchers, SmithHours, CaravanBulk.UseNow, a cel zapasu CaravanBulk i ColdStart tylko przy wylaczonych *LegacyHands),
            // a miasta krain z hearth ponizej kalibracji (rabunki) maja mniej ludzi rynku niz wczoraj
            // (rece w strone dawnego wzoru). Wylaczone - dawny wzor tabeli (wspolczynnik z dzisiejszych sum kultury), jak dotad. Przed kalibracja tez dawny
            // wzor: ta droga (WorkshopLaw przy starcie sesji) nie moze przesunac chwili kalibracji (E4/3: kalibracja przed pula wyrzutkow, z hearth startu).
            if (TownsFrozen && Calibrated) return PeopleOf(s);
            int day = (int)CampaignTime.Now.ToDays;
            if (day != _kTabDay || _kTab.Count == 0)
            {
                _kTab.Clear();
                float scale = Math.Max(0f, Settings.Current.PopulationScale);
                var hearth = new Dictionary<string, float>(); var prosp = new Dictionary<string, float>();
                foreach (var x in Settlement.All)
                {
                    if (x == null || x.Culture == null) continue;
                    string c = x.Culture.StringId;
                    if (!Table.ContainsKey(c)) continue;
                    if (x.IsVillage && x.Village != null) { float v; hearth.TryGetValue(c, out v); hearth[c] = v + Math.Max(1f, x.Village.Hearth); }
                    else if (x.IsTown && x.Town != null) { float v; prosp.TryGetValue(c, out v); prosp[c] = v + Math.Max(1f, x.Town.Prosperity); }
                }
                foreach (var kv in Table)
                {
                    float h, p;
                    hearth.TryGetValue(kv.Key, out h); prosp.TryGetValue(kv.Key, out p);
                    float pop = kv.Value.Pop * scale;
                    float urban = p > 0f ? kv.Value.Urban : 0f;
                    if (h <= 0f) urban = p > 0f ? 1f : 0f;
                    _kTab[kv.Key] = new[] { h > 0f ? pop * (1f - urban) / h : 0f, p > 0f ? pop * urban / p : 0f };
                }
                _kTabDay = day;
            }
            float[] k;
            if (!_kTab.TryGetValue(s.Culture.StringId, out k)) return 0f;
            if (s.IsVillage && s.Village != null) return Math.Max(1f, s.Village.Hearth) * k[0];
            if (s.IsTown && s.Town != null) return Math.Max(1f, s.Town.Prosperity) * k[1];
            return 0f;
        }

        /// <summary>174.3: ludnosc z tabeli krain razem (kontrola linii "Rece (174)").</summary>
        internal static float TableTotal()
        {
            float scale = Math.Max(0f, Settings.Current.PopulationScale), sum = 0f;
            foreach (var kv in Table) sum += kv.Value.Pop * scale;
            return sum;
        }

        /// <summary>Czy ludnosc jest juz skalibrowana (ksiega "Ludzie:" czyta PeopleOf tylko wtedy - sama kalibracji nie wywoluje).</summary>
        internal static bool Calibrated { get { return _k.Count > 0; } }

        /// <summary>E4/0 (pomiar, tylko odczyt): ilu ludzi tabeli na punkt hearth wsi (town = false) albo dobrobytu miasta (town = true) tej kultury;
        /// 0 przed kalibracja i dla kultury spoza tabeli - kalibracji sami nie wolamy (jak ksiega "Ludzie:").</summary>
        internal static float KOf(string culture, bool town)
        {
            float[] k;
            if (culture == null || _k.Count == 0 || !_k.TryGetValue(culture, out k)) return 0f;
            return town ? k[1] : k[0];
        }

        internal static float PeopleOf(Settlement s)
        {
            if (s == null) return 0f;
            Calibrate();
            float k = KFor(s);
            if (!(k > 0f)) return 0f;
            if (s.IsVillage && s.Village != null) return Math.Max(0f, s.Village.Hearth) * k;
            if (s.IsTown && s.Town != null) return TownPeople(s, k);
            return 0f;
        }

        // ------------------------------------------------------------ E4/4 (9a): ludnosc miast jako stan (DOM miasta w PeopleBook "t:")
        // Miasto nie ma jeszcze wlasnych narodzin, zgonow ani migracji (R2: E4/8). Dotad jego ludzie byli biezacym dobrobytem x k, wiec "Ludnosc:" i renty
        // nalezne chodzily za kazdym ruchem dobrobytu (model RBL/BK, glod, oblezenie, budowy), a ludzie przybywali i znikali bez przyczyny. Odtad liczba ludzi
        // miasta jest STANEM: zaklada sie raz - w nowej kampanii przy kalibracji (dokladnie udzial miejski krainy z tabeli), w starym zapisie pierwszej doby po
        // wczytaniu, z dobrobytu x k_v tej chwili (zadnego skoku) - i idzie do zapisu (PeopleBook, "arm_people" sekcja "t:"). Zmieni go dopiero E4/8 (migracja).
        private static int _townSeeded;            // miasta zalozone od ostatniej linii logu
        private static int _townStumbles;          // potkniecia na pojedynczym miescie (zalozenie stanu, linia dobowa) - do "potkniecia" linii "Ludzie: przyrost naturalny"
        private static bool _errTowns;

        internal static bool TownsFrozen { get { var s = Settings.Current; return s != null && s.TownPeopleFrozen; } }

        /// <summary>Ludzie miasta: stan (zakladany przy pierwszym pytaniu), a przy wylaczonym TownPeopleFrozen - dobrobyt x k jak dotad.</summary>
        private static float TownPeople(Settlement s, float kTown)
        {
            float live = Math.Max(0f, s.Town.Prosperity) * kTown;
            if (!TownsFrozen) return live;                                   // co do bitu stary rachunek
            float p;
            if (PeopleBook.TryTown(s, out p)) return p;
            if (float.IsNaN(live) || float.IsInfinity(live)) return 0f;      // zepsuty dobrobyt nie zostaje stanem - zalozymy, gdy wroci liczba
            if (PeopleBook.SetTownOnce(s, live)) _townSeeded++;
            return live;
        }

        /// <summary>Zaklada stan miastom, ktore go nie maja (wszystkim naraz - jeden dzien zalozenia), i raz o tym pisze. Bez kalibracji nic nie robi (sam jej nie wola).</summary>
        private static void SeedTowns(string when)
        {
            try
            {
                if (!TownsFrozen || _k.Count == 0) return;
                int bad = 0;
                foreach (var s in Settlement.All)
                {
                    // wyjatek na jednym miescie (poprawka po recenzji E4/4): licznik, reszta miast dostaje stan w tej samej chwili; to miasto zalozy sie leniwie
                    // przy pierwszym PeopleOf, gdy przestanie rzucac
                    try
                    {
                        if (s == null || !s.IsTown || s.Town == null) continue;
                        float k = KFor(s);
                        if (!(k > 0f)) continue;
                        TownPeople(s, k);
                    }
                    catch (Exception e)
                    {
                        bad++; _townStumbles++;
                        if (!_errTowns) { _errTowns = true; Log.Error("PopulationLaw.SeedTowns(" + (s != null ? s.StringId : "?") + ")", e); }
                    }
                }
                if (_townSeeded <= 0) return;
                Log.Info("PopulationLaw: ludnosc miast zapisana jako stan (E4/4, " + when + ") - zalozono " + _townSeeded + " miast, razem " + PeopleBook.TownCount
                         + " miast i " + (PeopleBook.TownSum / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln ludzi (dobrobyt x k osady z tej chwili); odtad liczba ludzi miasta nie idzie za dobrobytem"
                         + (bad > 0 ? "; " + bad + " miast z wyjatkiem - bez stanu (zaloza sie przy pierwszym pytaniu)" : "") + ".");
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
                int n = PeopleBook.DropTowns();
                if (n > 0)
                    Log.Info("PopulationLaw: ludnosc miast znow liczona z dobrobytu (Town People Frozen wylaczone w MCM) - stan " + n
                             + " miast porzucony; po ponownym wlaczeniu zalozy sie od nowa, z dobrobytu tamtego dnia.");
            }
            catch (Exception e) { if (!_errTowns) { _errTowns = true; Log.Error("PopulationLaw.TownsDaily", e); } }
        }

        // ------------------------------------------------------------ E4/3 (108n): jednostka ludzi i k na osade
        // klucz w `_k`, ktory nie jest id kultury: srednio ludzi wsi na punkt hearth na calym swiecie (podstawa wyrzutkow liczonych od ludnosci)
        private const string WorldKey = "*";
        private static bool _errCalibrate;

        /// <summary>
        /// E4/3 (U3): ludzi na punkt hearth wsi / dobrobytu miasta TEJ osady. Przy czynnej jednostce (PeopleUnitEnabled) - k_v z dnia kalibracji
        /// z ksiegi ludzi (zmiana kultury osady - asymilacja BK, przejecie przez Innych - nie zmienia ani liczby ludzi, ani wagi czlowieka; kultura spoza
        /// tabeli, np. whitewalker, nie zeruje juz ludzi osady); przy wylaczonej albo bez k_v - k biezacej kultury osady, jak dotad. 0 = brak przelicznika
        /// (zamek, kultura spoza tabeli, przed kalibracja). Kalibracji sami nie wolamy.
        /// </summary>
        internal static float KFor(Settlement s)
        {
            if (s == null || !(s.IsVillage || s.IsTown)) return 0f;
            float kv;
            if (PeopleUnit.On && PeopleBook.TryUnit(s, out kv)) return kv;
            float[] k;
            if (s.Culture == null || !_k.TryGetValue(s.Culture.StringId, out k) || k == null || k.Length < 2) return 0f;
            return s.IsVillage ? k[0] : k[1];
        }

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
        /// Ile punktow hearth wsi to jeden jej czlowiek: 1/k_v osady (ta sama liczba, przez ktora PeopleOf mnozy hearth). 0 = brak przelicznika
        /// (kultura spoza tabeli w dniu kalibracji, kraina bez wsi, PopulationScale 0) - wolajacy zostaje wtedy przy stawce dotychczasowej; bez dzielenia przez zero.
        /// </summary>
        internal static float HearthPerMan(Village v)
        {
            if (v == null || v.Settlement == null) return 0f;
            EnsureCalibrated();
            float kv = KFor(v.Settlement);
            if (!(kv > 0f) || float.IsInfinity(kv)) return 0f;       // takze NaN
            float per = 1f / kv;
            return per > 0f ? per : 0f;
        }

        /// <summary>
        /// Srednio ludzi wsi na punkt hearth na swiecie (ok. 191). Liczone raz - z ludnosci i hearth wsi w chwili pierwszego pytania (w nowej kampanii:
        /// przy siewie puli wyrzutkow, zaraz po kalibracji) - i trzymane w `_k` pod kluczem "*", wiec idzie do zapisu razem z reszta kalibracji;
        /// zapis sprzed tej wersji dostaje je przy pierwszym pytaniu. 0 = brak danych (zadna wies nie ma przelicznika).
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
                    if (s == null || !s.IsVillage || s.Village == null) continue;
                    float k = KFor(s);
                    if (!(k > 0f)) continue;
                    double h = Math.Max(0f, s.Village.Hearth);
                    people += h * k; hearth += h;
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
            Log.Info("PopulationLaw: srednia swiata " + kw.ToString("0.0", CultureInfo.InvariantCulture) + " ludzi wsi na punkt hearth (podstawa liczenia wyrzutkow od ludnosci, E4/3).");
            return kw;
        }

        // ------------------------------------------------------------ E4/4 (109n): przyrost naturalny wsi (R1, PLAN 4.2 + 4.3)
        // Gra dopisuje wsi stale +4 / +1.2 / +0.2 hearth dziennie (ponizej 300 / ponizej 600 / wyzej) bez wzgledu na to, ilu ludzi ta wies oznacza: w biegu 120 dob
        // (zbior 30, test A) tick wsi +834 hearth/d = ok. +165 tys. ludzi ksiegi dziennie (Ludnosc 52.2 -> 69.1 mln w 120 dob). Odtad hearth wsi zmienia sie o stope roczna g:
        //   zmiana dobowa = hearth x g / 100 / dni roku,   g [% rocznie] = G0 + GW x W - GH x H - GU x U, obciete do [-1.3; +0.8]
        //   W - dobrobyt warowni wsi wobec mediany warowni tego samego rodzaju (miasto albo zamek) w tej samej krainie (kultura z dnia kalibracji - U3), -0.5..+0.5;
        //       mediana krainy, nie srednia kroczaca: nie zalezy od wspolnego dryfu, nie potrzebuje stanu w zapisie, nie skacze w dniu wlaczenia ani po wczytaniu;
        //   H - glod okregu (E4/6, R1): brak jedzenia gospodarzy wsi w ostatniej dobie ze spichlerza okregu (Famine.HungerOf, 0..1; 0 bez glodu, 0.1 w przednowku);
        //       dawniej ciecie plonu zima (WinterBite.VillageCut) - zima glodem nie jest, dopoki spichlerz karmi; w glodzie (brak ponad prog) dodatni przyrost = 0 (GL 2.10);
        //   U - niebezpieczenstwo: 0.4 x (1 - bezpieczenstwo warowni / 100) + 0.3 x [region w wojnie - ta sama regula co powrot rozbitych E4/3: bez wiecznych
        //       wojen z Wolnymi Ludzmi i Innymi, OutlawLaw.WarWithoutEternal] + 0.3 x udzial spalonych i lupionych wsi regionu.
        // Premie hearth gry, BK i BKROT (perki, budynki, polityki, laski bogow, podatki, prawa) nie dopisuja hearth, tylko mnoza DODATNIE g:
        // (1 + dodatki / 1.2) x (1 + czynniki), obciete do [0.5; 2]; pulap +0.8 dopiero PO mnozeniu. Rosnie tylko wies w stanie Normal (jak w grze, ktora baze pasma
        // daje tylko jej); spalona, lupiona i pod przymusem: 0 (vanillowe -1 hearth w spalonej wsi znika). Powrot uchodzcow +0.5 hearth ponizej RefugeeFloorHearth
        // (ScorchedEarth.RefugeeReturn) - od E4/5 tylko wsie bez spustoszenia (objete: uchodzcy z konta w ksiedze, Devastation.Daily); w logu osobno. k osady sie skraca, wiec rachunek idzie wprost na hearth; ludzi (hearth x k_v) pokazuja dymek i log.
        // Ludzie z narodzin to jedyne (obok bilansu otwarcia) nazwane zrodlo ludzi ksiegi (R0): "przyrost naturalny" w linii "Ludzie: przyrost naturalny" i w "Hearth (E4/0)".
        private const float BonusUnit = 1.2f;          // hearth dziennie premii gry, ktore podwajaja przyrost (baza srodkowego pasma gry)
        private const float BonusMin = 0.5f, BonusMax = 2f;
        private const float WSpan = 0.5f;              // dobrobyt liczy sie do +-50% wobec mediany krainy
        private const float USecurity = 0.4f, UWar = 0.3f, UBurnt = 0.3f;
        private const float FloorProbe = 10.5f;        // R17: tylko wies ponizej tego hearth przed tickiem moze dostac podloge 10 (zmiana doby przy czynnym przyroscie < 0.001 + 0.5 powrotu)

        private static bool _growthPatched, _floorPatched;  // latki wpiete (raz na proces - to nie jest stan kampanii)
        internal static bool GrowthOn { get { var s = Settings.Current; return s != null && s.NaturalGrowthEnabled && _growthPatched; } }

        /// <summary>Stopa przyrostu jednej wsi i jej skladowe (punkty procentowe rocznie, o ile nie napisano inaczej).</summary>
        internal struct Growth
        {
            public bool Set;                // policzone przez postfiks (przyrost czynny)
            public bool Blocked;            // wies nie jest w stanie Normal (spalona, lupiona, pod przymusem): 0
            public float W, H, U;           // dobrobyt -0.5..+0.5; glod 0..1 (liczony takze, gdy wylaczony); niebezpieczenstwo 0..1
            public bool Starving;           // E4/6: glod ponad prog - dodatni przyrost wyzerowany (GL 2.10)
            public bool War; public float Security, Burnt;
            public float Base, Prosper, Hunger, Danger;     // G0; +GW x W; -GH x H (0 przy wylaczonym glodzie); -GU x U
            public float Raw;               // suma przed premiami gry
            public float Bonus;             // mnoznik premii gry (tylko przy dodatniej sumie); 1 = brak
            public float G;                 // stopa po premiach i obcieciu do korytarza
            public float Lo, Hi;            // korytarz z ustawien
            public float Refugees;          // hearth dziennie: powrot uchodzcow do wsi przy dnie (ScorchedEarth.RefugeeReturn, do E4/5)
            public double Delta;            // hearth dziennie razem
        }

        private static readonly object _gLock = new object();
        private static int _gDay = int.MinValue;
        private static readonly Dictionary<Settlement, float> _gW = new Dictionary<Settlement, float>();
        private static readonly Dictionary<IFaction, bool> _gWar = new Dictionary<IFaction, bool>();
        private static int _gStumbles, _floorStumbles;
        private static bool _errGrowth, _errGrowthLog, _errFloor, _modelLogged;
        [ThreadStatic] private static Growth _gLast;       // ostatni rachunek postfiksu - linia dobowa i okno W1 (Measure40) czytaja go zaraz po pytaniu modelu
        [ThreadStatic] private static Village _gLastVillage;

        private static int YearDays() { return Math.Max(28, CampaignTime.DaysInYear); }

        /// <summary>Zmiana dobowa hearth przy stopie `rate` % rocznie (plus powrot uchodzcow) - jedno miejsce, zeby pulap i wynik zaokraglaly sie tak samo.</summary>
        private static float Step(float hearth, float rate, float refugees)
        {
            return (float)((double)hearth * rate / 100.0 / YearDays() + refugees);
        }

        // pod _gLock: raz na dobe mediany dobrobytu (kraina z dnia kalibracji x rodzaj warowni) i czysta pamiec wojen
        private static void DayCache()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _gDay) return;
            _gDay = day; _gW.Clear(); _gWar.Clear();
            var groups = new Dictionary<string, List<Settlement>>();
            foreach (var n in OutlawLaw.RegionNodes())
            {
                if (n == null || n.Town == null) continue;
                string key = PeopleBook.CultureAtCalibration(n) + (n.IsTown ? "|miasto" : "|zamek");
                List<Settlement> l;
                if (!groups.TryGetValue(key, out l)) groups[key] = l = new List<Settlement>();
                if (!l.Contains(n)) l.Add(n);
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

        /// <summary>Czy region jest dzis w wojnie (jedna regula z powrotem rozbitych E4/3 - OutlawLaw.WarWithoutEternal: bez wiecznych wojen z Wolnymi Ludzmi i Innymi).</summary>
        private static bool AtWarToday(IFaction f)
        {
            if (f == null) return false;
            lock (_gLock)
            {
                DayCache();
                bool w;
                if (_gWar.TryGetValue(f, out w)) return w;
                w = false;
                try { w = OutlawLaw.WarWithoutEternal(f); }
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
        /// Niebezpieczenstwo wsi U, 0..1: 0.4 x (1 - bezpieczenstwo warowni / 100) + 0.3 x [region w wojnie - OutlawLaw.WarWithoutEternal] + 0.3 x udzial spalonych
        /// i lupionych wsi regionu. Jedna miara dla przyrostu naturalnego (czlon U, E4/4) i udzialu trafionych spustoszeniem, ktorzy ida w las (E4/5, HR 2.4).
        /// Wydzielone z GrowthOf bez zmiany rachunku (E4/5). Wies bez warowni: bezpieczenstwo 100, bez spalonych, pokoj. Liczy w kazdym stanie wsi.
        /// </summary>
        internal static float Danger(Village v, out float security, out bool war, out float burnt)
        {
            return DangerOf(v != null ? v.Bound : null, out security, out war, out burnt);      // region wsi: jej miasto albo zamek
        }

        /// <summary>E4/8 (R2): niebezpieczenstwo U warowni `node` (miasto liczy je jak swoje wsie) - wydzielone z Danger(Village) BEZ zmiany rachunku (wzor E4/5).</summary>
        internal static float DangerOf(Settlement node, out float security, out bool war, out float burnt)
        {
            security = 100f; burnt = 0f;
            if (node != null)
            {
                if (node.Town != null) { float sec = node.Town.Security; security = sec >= 0f ? Math.Min(100f, sec) : 0f; }
                var vs = node.BoundVillages; int nv = 0, nb = 0;
                if (vs != null)
                    foreach (var o in vs)
                    {
                        if (o == null) continue;
                        nv++;
                        if (o.VillageState == Village.VillageStates.Looted || o.VillageState == Village.VillageStates.BeingRaided) nb++;
                    }
                burnt = nv > 0 ? (float)nb / nv : 0f;
            }
            war = AtWarToday(node != null ? node.MapFaction : null);       // frakcja wsi to frakcja jej warowni (Village.MapFaction)
            return MBMath.ClampFloat(USecurity * (1f - security / 100f) + (war ? UWar : 0f) + UBurnt * burnt, 0f, 1f);
        }

        /// <summary>E4/8 (R2): skladowe stopy miasta (punkty procentowe rocznie).</summary>
        internal struct TownRateParts
        {
            public float W, U, H;           // dobrobyt warowni wobec mediany krainy; niebezpieczenstwo; glod (srednia wsi miasta)
            public bool War, Starving;
            public float Raw;               // stopa wsi regionu przed korytarzem (bez premii gry - mnoznik z modelu hearth wsi; miasto go nie ma)
            public float Village;           // po korytarzu wsi
            public float Penalty;           // TownGrowthPenaltyPercent
            public float G;                 // stopa miasta = Village - Penalty
        }

        /// <summary>
        /// E4/8 (R2, DOK:524): przyrost naturalny miasta = stopa wsi jego regionu (R1: te same W i U, H - srednia glodu wsi miasta, w glodzie bez dodatniego przyrostu,
        /// ten sam korytarz, BEZ premii gry) minus TownGrowthPenaltyPercent (1 pkt - miasto grzebalo wiecej, niz chrzcilo). Nie rzuca (wyjatek - 0, potkniecie TownGrowth).
        /// </summary>
        internal static float TownRate(Settlement town, out TownRateParts p)
        {
            p = default(TownRateParts);
            var s = Settings.Current;
            if (s == null || town == null) return 0f;
            p.W = ProsperityIndex(town);
            float sec, burnt; bool war;
            p.U = DangerOf(town, out sec, out war, out burnt); p.War = war;
            float h = 0f; int n = 0;
            if (town.BoundVillages != null) foreach (var v in town.BoundVillages) { if (v == null) continue; h += Famine.HungerOf(v); n++; }
            p.H = n > 0 ? MBMath.ClampFloat(h / n, 0f, 1f) : 0f;
            float raw = s.GrowthBasePercent + Math.Max(0f, s.GrowthProsperityWeight) * p.W - Math.Max(0f, s.GrowthDangerWeight) * p.U
                        - (s.GrowthHungerEnabled ? Math.Max(0f, s.GrowthHungerWeight) * p.H : 0f);
            if (raw > 0f && s.GrowthHungerEnabled && p.H > Famine.ThresholdNow) { raw = 0f; p.Starving = true; }      // GL 2.10, jak wies
            if (float.IsNaN(raw)) raw = 0f;
            p.Raw = raw;
            p.Village = MBMath.ClampFloat(raw, -Math.Max(0f, s.GrowthMaxDeclinePercent), Math.Max(0f, s.GrowthMaxPercent));
            p.Penalty = Math.Max(0f, s.TownGrowthPenaltyPercent);
            p.G = p.Village - p.Penalty;
            return p.G;
        }

        /// <summary>Rachunek jednej wsi. `gameBase` i `gameFactors` to wynik gry z chwili wejscia postfiksu (po latkach BK i BKROT): suma dodatkow z baza pasma i suma czynnikow.</summary>
        private static Growth GrowthOf(Village v, Settings s, float gameBase, float gameFactors)
        {
            var g = new Growth { Set = true, Bonus = 1f, Security = 100f };
            g.Hi = Math.Max(0f, s.GrowthMaxPercent); g.Lo = -Math.Max(0f, s.GrowthMaxDeclinePercent);
            float hearth = v.Hearth;
            if (!(hearth > 0f) || float.IsInfinity(hearth)) return g;       // takze NaN - niczego nie dopisujemy
            // rosnie tylko wies w stanie Normal - jak w grze, ktora baze pasma daje tylko jej; spalona (Looted), lupiona (BeingRaided)
            // i wies pod przymusem gracza (ForcedForVolunteers / ForcedForSupplies) maja 0
            if (v.VillageState != Village.VillageStates.Normal) { g.Blocked = true; return g; }

            Settlement node = v.Bound;      // region wsi: jej miasto albo zamek
            g.W = ProsperityIndex(node);
            g.U = Danger(v, out g.Security, out g.War, out g.Burnt);       // E4/5: ta sama miara dla udzialu "las" przy spustoszeniu (Devastation)
            g.H = MBMath.ClampFloat(Famine.HungerOf(v), 0f, 1f);          // E4/6: brak jedzenia ze spichlerza okregu (0 przy wylaczonym E4/6), nie ciecie zimy

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
                // E4/6 (GL 2.10): w glodzie (brak ponad prog Famine Threshold Percent) nikt sie nie rodzi ponad zgony - dodatni przyrost 0; zgony z glodu liczy Famine
                if (s.GrowthHungerEnabled && g.H > Famine.ThresholdNow) { g.Starving = true; rate = 0f; }
            }
            if (float.IsNaN(rate)) rate = 0f;
            g.G = MBMath.ClampFloat(rate, g.Lo, g.Hi);
            g.Refugees = ScorchedEarth.RefugeeReturn(v);
            g.Delta = Step(hearth, g.G, g.Refugees);
            return g;
        }

        // linie dymka: [0] ludzie rocznie, [1] hearth rocznie (osada bez przelicznika ludzi)
        private static TextObject[] Lines(string what)
        {
            return new[] { new TextObject("{=!}" + what + " - people a year"), new TextObject("{=!}" + what + " - hearths a year") };
        }
        private static readonly TextObject[] _txtBirths = Lines("Births over deaths"), _txtProsper = Lines("Prosperity of the land"),
            _txtHunger = Lines("Hunger"), _txtDanger = Lines("War, raids and lawlessness"), _txtBonus = Lines("Lord's care (perks, buildings, laws)"),
            _txtLimit = Lines("Limit of natural growth"), _txtRefugees = Lines("Refugees returning"), _txtFamine = Lines("Famine - no births");

        /// <summary>Linia opisu, ktora NIE wchodzi do wyniku: dopisana i od razu zdjeta bez opisu (ExplainedNumber nie umie inaczej pokazac liczby w innej
        /// jednostce niz wynik). Dymek gry pomija linie ponizej 0.01, a dobowa zmiana hearth to ok. 0.005 - dlatego linie mowia o ludziach na rok.</summary>
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
            float k = v.Settlement != null ? KFor(v.Settlement) : 0f;
            bool people = k > 0f && !float.IsInfinity(k);
            double unit = (double)Math.Max(0f, v.Hearth) * (people ? k : 1f) / 100.0;       // ludzi (albo hearth) na 1 pkt % rocznie
            Line(ref r, unit * g.Base, _txtBirths, people);
            Line(ref r, unit * g.Prosper, _txtProsper, people);
            Line(ref r, unit * g.Hunger, _txtHunger, people);
            Line(ref r, unit * g.Danger, _txtDanger, people);
            double before = g.Raw;
            if (g.Raw > 0f) { before = (double)g.Raw * g.Bonus; Line(ref r, unit * (before - g.Raw), _txtBonus, people); }
            Line(ref r, unit * (g.G - before), g.Starving ? _txtFamine : _txtLimit, people);
            Line(ref r, (double)g.Refugees * YearDays() * (people ? k : 1f), _txtRefugees, people);
        }

        /// <summary>
        /// Postfiks na DefaultSettlementProsperityModel.CalculateHearthChange(Village, bool), Priority.Last, wpinany po ScorchedEarth: biegnie po latkach BK (400)
        /// i BKROT (200), widzi ich wynik i podmienia go na przyrost naturalny. Wynik dostaje limity ExplainedNumber rowne korytarzowi wsi, wiec model owijajacy baze
        /// (NavalDLC dopisuje perk gubernatora PO latkach) nie wyprowadzi go poza korytarz. Wyjatek: ta jedna odpowiedz = 0, licznik potkniec, pierwszy wyjatek do
        /// logu - niczego nie gasimy.
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
                _gLast = g; _gLastVillage = __0;
            }
            catch (Exception e)
            {
                _gStumbles++;
                if (!_errGrowth) { _errGrowth = true; Log.Error("PopulationLaw.GrowthPostfix", e); }
                __result = new ExplainedNumber(0f, __1, null);
                _gLast = default(Growth); _gLastVillage = null;
            }
        }

        /// <summary>Measure40 (okno W1, tylko pomiar): rachunek przyrostu z ostatniego pytania modelu o TE wies w tym watku - przyrost naturalny i powrot uchodzcow
        /// (hearth na dobe). false = przyrost nie liczyl tej odpowiedzi (wylaczony, wyjatek, inna wies). Czysci pamiec.</summary>
        internal static bool TakeLast(Village v, out float natural, out float refugees)
        {
            natural = 0f; refugees = 0f;
            bool ok = v != null && ReferenceEquals(_gLastVillage, v) && _gLast.Set;
            if (ok) { refugees = _gLast.Refugees; natural = (float)(_gLast.Delta - _gLast.Refugees); }
            _gLast = default(Growth); _gLastVillage = null;
            return ok;
        }

        // ------------------------------------------------------------ E4/4 (R17, U15d): podloga 10 hearth bez ludzi z niczego
        /// <summary>Stan wsi sprzed ticku gry - tylko dla wsi przy samym dnie (ponizej FloorProbe).</summary>
        internal sealed class FloorState { public Village V; public float H0, R; }

        /// <summary>Prefiks void Priority.First na Village.DailyTick (przed prefiksem BK, ktory zastepuje tick): tylko wies ponizej FloorProbe hearth - zapamietuje
        /// hearth i wynik modelu (czysty rachunek; okno W1 Measure40 na chwile wylaczone, zeby nie liczylo blizny drugi raz). Niczego nie zmienia.</summary>
        public static void FloorPrefix(Village __instance, out FloorState __state)
        {
            __state = null;
            try
            {
                if (!GrowthOn || __instance == null || Campaign.Current == null) return;
                float h0 = __instance.Hearth;
                if (!(h0 < FloorProbe)) return;
                bool was = Measure40.InVillageTick;
                float r;
                Measure40.InVillageTick = false;
                try { r = Campaign.Current.Models.SettlementProsperityModel.CalculateHearthChange(__instance, false).ResultNumber; }
                finally { Measure40.InVillageTick = was; _gLast = default(Growth); _gLastVillage = null; }
                if (float.IsNaN(r) || float.IsInfinity(r)) return;
                __state = new FloorState { V = __instance, H0 = h0, R = r };
            }
            catch (Exception e)
            {
                __state = null; _floorStumbles++;
                if (!_errFloor) { _errFloor = true; Log.Error("PopulationLaw.FloorPrefix", e); }
            }
        }

        /// <summary>Finalizer ticku wsi: podloga gry/BK dopisala `po - (przed + wynik modelu)` hearth - tylu ludzi schodzi z puli wyrzutkow regionu (PeopleBook.FloorFromPool).</summary>
        public static Exception FloorFinalizer(Exception __exception, FloorState __state)
        {
            if (__state == null) return __exception;
            try
            {
                float add = __state.V.Hearth - (__state.H0 + __state.R);
                // E4/8 (recenzja R1-5/R2-6): hearth przesuniety przez ksiege w postfiksie milicji tej wsi (powrot milicji do DOM +m/k, pobor milicji -m/k) to nie podloga gry
                try { add -= (float)ArmsFromPeople.PeekTickHearth(__state.V); } catch { }
                if (add > 0.0001f && !float.IsInfinity(add)) PeopleBook.FloorFromPool(__state.V, add);
            }
            catch (Exception e)
            {
                _floorStumbles++;
                if (!_errFloor) { _errFloor = true; Log.Error("PopulationLaw.FloorFinalizer", e); }
            }
            return __exception;
        }

        internal static void ApplyGrowth(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(DefaultSettlementProsperityModel), "CalculateHearthChange", new[] { typeof(Village), typeof(bool) });
                if (m == null || m.ReturnType != typeof(ExplainedNumber))
                {
                    Log.Info("PopulationLaw: przyrost naturalny (E4/4) - BRAK metody DefaultSettlementProsperityModel.CalculateHearthChange(Village, bool); hearth wsi liczy gra jak dotad.");
                    return;
                }
                h.Patch(m, postfix: new HarmonyMethod(typeof(PopulationLaw), nameof(GrowthPostfix)) { priority = Priority.Last });
                _growthPatched = true; _growthTarget = m;
                string floor;
                try
                {
                    var t = AccessTools.Method(typeof(Village), "DailyTick");
                    if (t == null) floor = "BRAK Village.DailyTick - podloga 10 bez zwrotu z puli";
                    else
                    {
                        h.Patch(t, prefix: new HarmonyMethod(typeof(PopulationLaw), nameof(FloorPrefix)) { priority = Priority.First },
                                   finalizer: new HarmonyMethod(typeof(PopulationLaw), nameof(FloorFinalizer)));
                        _floorPatched = true;
                        floor = "podloga 10 hearth (R17) z puli wyrzutkow regionu - okno na Village.DailyTick wpiete";
                    }
                }
                catch (Exception e) { floor = "podloga 10: blad wpiecia (" + e.GetType().Name + ")"; Log.Error("PopulationLaw.ApplyGrowth(podloga)", e); }
                // stan z ustawien startowych; MCM wchodzi przy starcie kampanii - prawdziwy stan stoi co dobe w linii "Ludzie: przyrost naturalny".
                // Kolejnosc tez jest tu niepelna: BannerKings wpina swoje latki dopiero przy starcie kampanii - pelna stoi w linii "czynny model dobrobytu" pierwszej doby.
                var s = Settings.Current;
                Log.Info("PopulationLaw: przyrost naturalny wsi (E4/4) zamiast stalej gry (+4 / +1.2 / +0.2 hearth dziennie) " + (GrowthOn ? "CZYNNY" : "wylaczony w ustawieniach startowych")
                         + (s != null ? " - g = " + F(s.GrowthBasePercent) + " + " + F(s.GrowthProsperityWeight) + " x dobrobyt - " + F(s.GrowthDangerWeight) + " x niebezpieczenstwo"
                                        + (s.GrowthHungerEnabled ? " - " + F(s.GrowthHungerWeight) + " x glod ze spichlerza okregu (E4/6; w glodzie ponad prog bez dodatniego przyrostu)" : " (glod ze spichlerza wylaczony - Growth Hunger Enabled)")
                                        + " [% rocznie], korytarz -" + F(Math.Max(0f, s.GrowthMaxDeclinePercent)) + "..+" + F(Math.Max(0f, s.GrowthMaxPercent))
                                        + "; ludzie miast jako stan: " + (s.TownPeopleFrozen ? "TAK" : "NIE") : "")
                         + "; " + floor + "; latka na DefaultSettlementProsperityModel.CalculateHearthChange wpieta, na razie: " + PatchOrder() + ".");
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

        // poprawka po recenzji E4/4: wsie regionow w pokoju i w wojnie osobno - prog Jeffa 11.10 (pokoj +0.2..+0.8%/rok) mierzy sie na wsiach w pokoju
        private sealed class WarSide { public double Weight, Rate, People, Gain; public int Villages, Blocked; }

        private static void AppendSide(StringBuilder sb, string name, WarSide w, int days)
        {
            sb.Append(name).Append(": ");
            if (w.Villages == 0) { sb.Append("brak wsi"); return; }
            sb.Append("g ").Append(Sg(w.Weight > 0 ? w.Rate / w.Weight : 0.0, "0.###")).Append("% rocznie, przyrost ").Append(Sg(w.Gain, "0")).Append(" ludzi (")
              .Append(Sg(w.People > 0 ? w.Gain * days * 100.0 / w.People : 0.0, "0.###")).Append("% rocznie) z ").Append((w.People / 1e6).ToString("0.000", CultureInfo.InvariantCulture))
              .Append(" mln ludzi w ").Append(w.Villages).Append(" wsiach (w tym ").Append(w.Blocked).Append(" spalonych, lupionych albo pod przymusem)");
        }

        /// <summary>
        /// Raz na dobe, z ksiegi "Ludzie:" (PeopleLedger, zaraz po linii "Ludzie:"): pyta czynny model gry o zmiane hearth kazdej wsi (kazdej raz) - to samo, co dopisze
        /// jej dobowy tick - i pisze linie "Ludzie: przyrost naturalny". Bilans hearth wsi (zmiana od wczoraj z RESZTA) jest w JEDNYM miejscu - linii "Hearth (E4/0)"
        /// (U8: bez drugiego rachunku). Niczego nie zmienia.
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

                int n = 0, up = 0, down = 0, flat = 0, blocked = 0, war = 0, burnt = 0, capHi = 0, capLo = 0, refugees = 0, bonusN = 0, hungerN = 0, tiny = 0, noK = 0, stumbles = 0, dup = 0, famineN = 0;
                double hearthAll = 0, weightAll = 0, weightLive = 0, peopleAll = 0, modelHearth = 0, takenHearth = 0, gainPeople = 0;
                double backHearth = 0, backPeople = 0;      // powrot uchodzcow (+0.5 hearth przy dnie) - siedzi w wyniku modelu, ale przyrostem naturalnym nie jest
                double wBase = 0, wProsper = 0, wDanger = 0, wHunger = 0, wDryHunger = 0, wBonus = 0, wSec = 0, wRate = 0, sumH = 0;
                float bonusMin = float.MaxValue, bonusMax = float.MinValue;
                var rates = new List<float>();
                var levels = new int[3];
                var realms = new Dictionary<string, RealmGrowth>();
                var peaceS = new WarSide(); var warS = new WarSide();
                var seen = new HashSet<Village>();
                float gh = Math.Max(0f, s.GrowthHungerWeight);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage || st.Village == null) continue;
                    var v = st.Village;
                    if (!seen.Add(v)) { dup++; continue; }      // ta sama wies dwa razy w Settlement.All (ROT) - liczona raz
                    try
                    {
                        float hearth = v.Hearth;
                        if (!(hearth > 0f)) continue;
                        float k = KFor(st);
                        if (!(k > 0f) || float.IsInfinity(k)) { k = 0f; noK++; }
                        double weight = k > 0f ? (double)hearth * k : hearth;        // ludzie wsi; bez przelicznika - hearth
                        _gLast = default(Growth); _gLastVillage = null;
                        float d = v.HearthChange;                                   // czynny model gry
                        Growth g = ReferenceEquals(_gLastVillage, v) ? _gLast : default(Growth);
                        float taken = (float)(hearth + d) - hearth;                 // tyle przyjmie float32 wsi
                        n++; hearthAll += hearth; weightAll += weight; if (k > 0f) peopleAll += weight;
                        levels[hearth >= 600f ? 2 : (hearth >= 200f ? 1 : 0)]++;       // progi produkcji gry (Village.GetHearthLevel)
                        modelHearth += d; takenHearth += taken;
                        double gain = (double)taken * k;
                        gainPeople += gain;
                        if (d > 0f) up++; else if (d < 0f) down++; else flat++;
                        if (d != 0f && taken == 0f) tiny++;
                        // powrot uchodzcow osobno: jedna wies Reach ponizej 40 hearth to ok. +220 ludzi dziennie (0.5 x k) - w sumie "+N ludzi" udawalby przyrost
                        float back = g.Set ? g.Refugees : 0f;
                        double backGain = (double)back * k;
                        backHearth += back; backPeople += backGain;
                        float rate = g.Set ? g.G : (float)((double)d * days * 100.0 / hearth);      // przy wylaczonym przyroscie - stopa z wyniku gry
                        RealmGrowth rg; string c = PeopleBook.CultureAtCalibration(st);
                        if (!realms.TryGetValue(c, out rg)) realms[c] = rg = new RealmGrowth();
                        rg.Villages++; rg.People += weight; rg.Rate += weight * rate; rg.Gain += gain - backGain;
                        if (g.Set && k > 0f) PeopleBook.NoteBorn(v, gain - backGain);      // E4/7 (D2): przyrost naturalny netto wedlug regionu od siewu - K_m wolnych rak (nie rzuca)
                        if (!g.Set) continue;                                       // przyrost wylaczony: tylko suma wyniku gry
                        rates.Add(g.G); wRate += weight * g.G;
                        // pokoj / wojna (ta sama regula wojny co g); wies spalona, lupiona albo pod przymusem (g 0) - wedle wojny swojego regionu
                        // (GrowthOf jej wojny nie liczy; AtWarToday - pamiec doby)
                        bool atWar = g.Blocked ? AtWarToday(v.Bound != null ? v.Bound.MapFaction : null) : g.War;
                        var side = atWar ? warS : peaceS;
                        side.Villages++; side.Weight += weight; side.Rate += weight * g.G;
                        if (k > 0f) { side.People += weight; side.Gain += gain - backGain; }
                        if (g.Blocked) side.Blocked++;
                        if (g.Blocked) { blocked++; continue; }
                        weightLive += weight;
                        if (g.War) war++;
                        if (g.Burnt > 0f) burnt++;
                        wBase += weight * g.Base; wProsper += weight * g.Prosper; wDanger += weight * g.Danger; wHunger += weight * g.Hunger;
                        wSec += weight * g.Security;
                        if (g.H > 0f) { hungerN++; sumH += g.H; wDryHunger += weight * gh * g.H; }
                        if (g.Starving) famineN++;
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
                _gLast = default(Growth); _gLastVillage = null;

                // miasta: stan wobec tego, co dalby dzisiejszy dobrobyt
                double townState = 0, townLive = 0; int towns = 0;
                var seenT = new HashSet<Settlement>();
                foreach (var st in Settlement.All)
                {
                    try        // poprawka po recenzji E4/4: wyjatek na jednym miescie - licznik, linia dobowa zostaje
                    {
                        if (st == null || !st.IsTown || st.Town == null || !seenT.Add(st)) continue;
                        float k = KFor(st);
                        if (!(k > 0f)) continue;
                        float live = Math.Max(0f, st.Town.Prosperity) * k, p;
                        if (float.IsNaN(live) || float.IsInfinity(live)) live = 0f;
                        float state = PeopleBook.TryTown(st, out p) ? p : live;
                        towns++; townLive += live; townState += state;
                    }
                    catch (Exception e)
                    {
                        _townStumbles++;
                        if (!_errTowns) { _errTowns = true; Log.Error("PopulationLaw.GrowthDaily(miasto " + (st != null ? st.StringId : "?") + ")", e); }
                    }
                }
                string townNote = !Calibrated ? "ludnosc nieskalibrowana"
                    : TownsFrozen ? (townState / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln w " + towns + " miastach - stan (E4/4, "
                                    + (TownGrowth.Active ? "narodziny, zgony i migracja: linia Miasta (E4/8 R2)" : "bez narodzin i migracji - Town Growth Enabled / Natural Growth Enabled wylaczone") + "); z dzisiejszego dobrobytu byloby "
                                    + (townLive / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln (" + Sg(townLive - townState, "0") + " ludzi dryfu od zalozenia stanu)"
                                  : (townLive / 1e6).ToString("0.000", CultureInfo.InvariantCulture) + " mln w " + towns + " miastach - dobrobyt x k (stan wylaczony w MCM)";

                var sb = new StringBuilder(2400);
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
                    sb.Append("; osobno powrot uchodzcow ").Append(Sg(backPeople, "0")).Append(" ludzi (").Append(Sg(backHearth, "0.#")).Append(" hearth w ").Append(refugees)
                      .Append(" wsiach ponizej ").Append(Math.Max(1, s.RefugeeFloorHearth)).Append(" hearth - regula ScorchedEarth, ludzie z niczego, nie przyrost")
                      .Append(Devastation.On ? "; tylko wsie bez spustoszenia E4/5 - uchodzcy z konta w linii \"Ludzie (spustoszenie)\")" : "; spustoszenie E4/5 wylaczone)");
                    // poprawka po recenzji E4/4: przyrost naturalny wzgledem ludzi WSI (miasta stoja), pokoj i wojna osobno - prog Jeffa 11.10 na wsiach w pokoju
                    double natSides = peaceS.Gain + warS.Gain, pplSides = peaceS.People + warS.People;
                    sb.Append(" | pokoj i wojna (regula wojny E4/3, bez wiecznych wojen): razem ").Append(Sg(pplSides > 0 ? natSides * days * 100.0 / pplSides : 0.0, "0.###"))
                      .Append("% rocznie z ").Append((pplSides / 1e6).ToString("0.000", CultureInfo.InvariantCulture)).Append(" mln ludzi wsi; ");
                    AppendSide(sb, "pokoj", peaceS, days);
                    sb.Append("; ");
                    AppendSide(sb, "wojna", warS, days);
                    rates.Sort();
                    sb.Append(" | wsie: rosnie ").Append(up).Append(", kurczy sie ").Append(down).Append(", bez zmiany ").Append(flat).Append(" (spalone, lupione albo pod przymusem ").Append(blocked).Append(')');
                    if (rates.Count > 0)
                        sb.Append("; g od ").Append(Sg(rates[0], "0.##")).Append(" przez ").Append(Sg(rates[rates.Count / 2], "0.##")).Append(" (mediana) do ").Append(Sg(rates[rates.Count - 1], "0.##"));
                    if (weightLive > 0)
                    {
                        sb.Append(" | skladowe g wsi niespalonych (pkt % rocznie, wazone ludnoscia): podstawa ").Append(Sg(wBase / weightLive, "0.###"))
                          .Append(", dobrobyt wobec mediany krainy ").Append(Sg(wProsper / weightLive, "0.###"))
                          .Append(", niebezpieczenstwo ").Append(Sg(wDanger / weightLive, "0.###")).Append(" (wsi regionow w wojnie bez wiecznych wojen ").Append(war)
                          .Append(", wsi w regionach ze spalonymi ").Append(burnt).Append(", bezpieczenstwo warowni srednio ").Append(F(wSec / weightLive)).Append(')');
                        // E4/6: H = brak jedzenia ze spichlerza okregu (Famine), nie ciecie zimy
                        if (s.GrowthHungerEnabled) sb.Append(", glod ze spichlerza (E4/6) ").Append(Sg(wHunger / weightLive, "0.###")).Append(" (wsi z brakiem jedzenia ").Append(hungerN)
                               .Append(hungerN > 0 ? ", srednio " + F(100.0 * sumH / hungerN) + "%" : "").Append(", w glodzie bez dodatniego przyrostu ").Append(famineN).Append(')');
                        else sb.Append(", glod ze spichlerza wylaczony - Growth Hunger Enabled (na sucho: wsi z brakiem jedzenia ").Append(hungerN).Append(hungerN > 0 ? ", srednio " + F(100.0 * sumH / hungerN) + "%" : "")
                               .Append("; z waga ").Append(F(gh)).Append(" byloby ").Append(Sg(-wDryHunger / weightLive, "0.###")).Append(')');
                        sb.Append(", premie gry srednio x").Append(F3(wBonus / weightLive)).Append(" (wsi z premia ").Append(bonusN);
                        if (bonusN > 0) sb.Append(", od x").Append(F(bonusMin)).Append(" do x").Append(F(bonusMax));
                        sb.Append("), pulap +").Append(F(Math.Max(0f, s.GrowthMaxPercent))).Append(" obcial ").Append(capHi).Append(" wsi, dno -").Append(F(Math.Max(0f, s.GrowthMaxDeclinePercent)))
                          .Append(" obcielo ").Append(capLo).Append(" wsi");
                    }
                }
                if (realms.Count > 0)
                {
                    var parts = realms.OrderByDescending(kv => kv.Value.People)
                                      .Select(kv => kv.Key + " " + Sg(kv.Value.People > 0 ? kv.Value.Rate / kv.Value.People : 0.0, "0.##") + "% (" + Sg(kv.Value.Gain, "0") + ")");
                    sb.Append(" | krainy (kultura z dnia kalibracji) g rocznie (ludzi dzis): ").Append(string.Join(", ", parts.ToArray()));
                }
                // R17: podloga 10 od ostatniej linii
                int fN; double fH, fPool, fNothing, fNothingH, fRef;
                PeopleBook.TakeFloor(out fN, out fH, out fPool, out fNothing, out fNothingH, out fRef);
                sb.Append(" | podloga 10 hearth (R17): ").Append(fN).Append(" razy, +").Append(fH.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append(" hearth - z uchodzcow tej wsi (E4/5) ").Append(fRef.ToString("0.#", CultureInfo.InvariantCulture)).Append(" ludzi, z puli wyrzutkow regionu ")
                  .Append(fPool.ToString("0.#", CultureInfo.InvariantCulture)).Append(" ludzi, podloga z niczego ").Append(fNothing.ToString("0.#", CultureInfo.InvariantCulture))
                  .Append(" ludzi (").Append(fNothingH.ToString("0.###", CultureInfo.InvariantCulture)).Append(" hearth)").Append(_floorPatched ? "" : " [okno podlogi nie wpiete]");
                sb.Append(" | hearth wsi swiata ").Append(F(hearthAll)).Append(" (bilans doby z RESZTA - linia \"Hearth (E4/0)\")");
                sb.Append(" | wsie wedle progow produkcji (ponizej 200 / 200-599 / od 600 hearth): ").Append(levels[0]).Append(" / ").Append(levels[1]).Append(" / ").Append(levels[2]);
                sb.Append(" | float32: model ").Append(Sg(modelHearth, "0.####")).Append(" hearth, wsie przyjmuja ").Append(Sg(takenHearth, "0.####"))
                  .Append(" (wsi z krokiem ponizej polowy kroku floata: ").Append(tiny).Append(')');
                sb.Append(" | miasta: ").Append(townNote);
                if (noK > 0) sb.Append(" | wsi bez przelicznika ludzi: ").Append(noK);
                if (dup > 0) sb.Append(" | wsie powtorzone w Settlement.All (liczone raz): ").Append(dup);
                int st0 = _gStumbles, sf = _floorStumbles, stT = _townStumbles; _gStumbles = 0; _floorStumbles = 0; _townStumbles = 0;
                if (st0 + stumbles + sf + stT > 0)
                    sb.Append(" | potkniecia: rachunek wsi ").Append(st0).Append(", linia dobowa ").Append(stumbles).Append(", podloga ").Append(sf).Append(", miasta ").Append(stT);
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

        /// <summary>Paczka 169 (tylko log): renty dzis - wsie i zawor miast osobno, renta wsi kazdego rodu (zajecie na sucho, rozdz. 8).
        /// Zerowane tam, gdzie RentToday, i przez MoneyLedger.ClearLast169() na poczatku bloku.</summary>
        internal static long DayVillageRent, DayTownRent;
        internal static readonly Dictionary<Clan, int> RentVillageToday = new Dictionary<Clan, int>();

        internal static void ZeroDay169() { DayVillageRent = 0; DayTownRent = 0; RentVillageToday.Clear(); _rentBy.Clear(); }

        // 169c (tylko log): renta tej doby na osade i kasa, z ktorej ja wzieto (miasto: ponad prog) - czesc "wlasne" D stalego
        private static readonly Dictionary<Settlement, long[]> _rentBy = new Dictionary<Settlement, long[]>();
        internal static void RentOf(Settlement st, out long pay, out long avail)
        {
            long[] v;
            if (st != null && _rentBy.TryGetValue(st, out v)) { pay = v[0]; avail = v[1]; } else { pay = 0; avail = 0; }
        }

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
                // 3.1 poprawka po recenzji (E3/8a, BkWorkshopTaxToLord): wpis 51 zakladal, ze BK pobiera podatek od cudzych warsztatow od wlascicieli - od lordow
                // i gracza nie pobiera (czynny model warsztatow gry to nie BKWorkshopModel) - te linie byly z niczego; zdjete bez przelewu, takze z podstawy rady
                float ownerPaid;   // D11-3: z tych linii czesc placona panu przelewem przez wlasciciela (OwnerTaxDaily) - jak linie notabli E3/8a liczy sie do podstawy rady
                float unpaid = Seal31B.UnpaidOwnerTaxLines(__0, out ownerPaid);
                __result = new TaleWorlds.CampaignSystem.ExplainedNumber(kept - unpaid - Seal31B.NotableTaxLines(__0), __1, _txtRent);   // 3.1/3.3 (31b) E3/8a: warsztaty notabli placa panu przelewem (BkWorkshopTaxToLord) - bez linii z niczego
                CouncilWages35.TownBase(__0, kept - unpaid + ownerPaid);   // 3.5a: pensje rady BK od tego podatku i renty, nie od zastapionego podatku klas ludnosci (wlasny try); bez linii, ktorych nikt nie placi (D11-3: placone przez wlasciciela - w podstawie)
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
            ZeroDay169();        // paczka 169 (tylko log)
            if (Calibrated) TownsDaily();   // E4/4 (9a): stan ludzi miast PRZED rentami (takze przy wylaczonych rentach - czyta go ksiega ludzi); bez kalibracji nic do zalozenia
            // E4/8 (R2): narodziny i zgony miast, migracja ze wsi regionu - zaraz po zalozeniu stanu, PRZED rentami (renty czytaja stan miasta)
            if (Calibrated) { try { TownGrowth.Daily(); } catch (Exception e) { Log.Error("TownGrowth.Daily", e); } }
            if (!On) return;
            try
            {
                var s = Settings.Current;
                float share = Math.Max(0f, Math.Min(1f, s.PopulationRentMaxShare));
                var pop = new Dictionary<string, float>();
                var due = new Dictionary<string, float>();
                var paid = new Dictionary<string, float>();
                RentToday.Clear();
                ZeroDay169();
                long totalPaid = 0, totalDue = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Culture == null || !(st.IsVillage || st.IsTown)) continue;
                    string c = st.Culture.StringId;
                    if (!Table.ContainsKey(c)) continue;    // E4/3: osada kultury spoza tabeli (whitewalker) ma juz ludzi (k na osade), ale renty - jak dotad (etap 4 nie rusza zlota)
                    float p = PeopleOf(st);
                    if (p <= 0f) continue;
                    float v; pop.TryGetValue(c, out v); pop[c] = v + p;
                    float rent = DailyRent(p);
                    due.TryGetValue(c, out v); due[c] = v + rent;
                    totalDue += (long)rent;
                    try
                    {
                        // hak kiesy ludu (112, KL etap 5): renta wsi ustepuje stalym oplatom 173 - do etapu 5 false (renta jak dotad)
                        if (st.IsVillage && VillageTakings.VillageRentYieldsToFees) continue;
                        // E5/A1 (R4): przy czynnym zaworze kas miast miasto placi przez ValveTowns nizej (7% nadwyzki ponad zapas kupcow, pan 2/3 : korona 1/3,
                        // bez pulapu naleznej) - "nalezne" wyzej policzone jak dotad
                        if (st.IsTown && TownPurse.ValveLive) continue;
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
                        if (st.IsTown && BesiegedPay.Holds(st, BesiegedPay.KTownRent, pay, true)) continue;   // E5/A4 (R22): oblezone miasto nie placi renty - zostaje w kasie, gdy kasa miasta jest prawdziwym pieniadzem (A1; inaczej placi i liczy; wies: rabunek wyzej)
                        GiveGoldAction.ApplyForSettlementToCharacter(st, lord, pay, true);
                        _rentBy[st] = new long[] { pay, gold };   // 169c (tylko licznik)
                        { int r0; RentToday.TryGetValue(st.OwnerClan, out r0); RentToday[st.OwnerClan] = r0 + pay; }
                        if (st.IsVillage) { DayVillageRent += pay; int v0; RentVillageToday.TryGetValue(st.OwnerClan, out v0); RentVillageToday[st.OwnerClan] = v0 + pay; }   // paczka 169 (tylko licznik)
                        else DayTownRent += pay;
                        paid.TryGetValue(c, out v); paid[c] = v + pay;
                        totalPaid += pay;
                    }
                    catch { }
                }
                if (TownPurse.ValveLive) ValveTowns(pop, paid, ref totalPaid);   // E5/A1 (R4): zawor WSZYSTKICH miast (takze kultur spoza tabeli - "10 miast bez placenia")
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

        /// <summary>
        /// E5/A1 (R4, plan kodu rozdz. 5): zawor kas miast - kazde miasto (Settlement.All, takze kultury spoza tabeli i bez ludzi) przez TownPurse.Valve;
        /// zapisy rent (RentToday, _rentBy, DayTownRent, "renty zaplacone") robi PopulationLaw jak dotad - jeden wlasciciel tych pol (czesc pana; czesc
        /// korony idzie do skarbca i TownPurse.CrownToday). Renta "nalezna od ludzi" (dawny pulap) - tylko liczba do linii "Kasy miast (169c)".
        /// Kazde miasto we wlasnym try w Valve (licznik potkniec).
        /// </summary>
        private static void ValveTowns(Dictionary<string, float> pop, Dictionary<string, float> paid, ref long totalPaid)
        {
            long crownSum = 0;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                long due = 0;
                string c = st.Culture != null ? st.Culture.StringId : null;
                try { if (c != null && Table.ContainsKey(c)) { float p = PeopleOf(st); if (p > 0f) due = (long)DailyRent(p); } } catch { }
                float decree = TaxDecree(st);
                int lp, lb, cr; long spare;
                if (!TownPurse.Valve(st, decree, due, out lp, out lb, out cr, out spare)) continue;
                crownSum += cr;
                if (lp > 0 || spare > 0) _rentBy[st] = new long[] { lp, Math.Max(0L, spare) };   // 169c i rada 3.5: czesc pana i nadwyzka, z ktorej ja wzieto
                if (lp <= 0) continue;
                var clan = st.OwnerClan;
                if (clan != null) { int r0; RentToday.TryGetValue(clan, out r0); RentToday[clan] = r0 + lp; }
                DayTownRent += lp;
                totalPaid += lp;
                if (c != null && pop.ContainsKey(c)) { float v; paid.TryGetValue(c, out v); paid[c] = v + lp; }
            }
            TownPurse.ValveDay(crownSum);
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
