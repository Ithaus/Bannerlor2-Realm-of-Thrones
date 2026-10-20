using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// p6-spichlerz (R3-06f, POMYSLY-35 poz. 16; PROJEKT-GLOD-2026-10-07 rozdz. 2.13 krok B, rozdz. 4 pkt 4-5, tabela BetterEconomy w 1.5) - MIASTO JE ZE SWOICH WSI.
    /// Dotad zapas jedzenia miasta (Town.FoodStocks) dostawal od kazdej wsi okregu staly skladnik gry (DefaultSettlementFoodModel: (poziom + 1) x 6 w stanie
    /// Normal; VillageBook.TownFoodPostfix: x czynnik rak) - zadne zboze nie schodzilo przy tym ze spichlerza wsi (Famine, f23). Teraz (klucz TownGranaryFromVillages):
    ///  1. SKLADNIK WSI MIASTA ZDJETY: postfiks na bazowym modelu (dziala w lancuchu ROT -> BK -> baza) odejmuje dokladnie to, co dzis daja wsie miasta
    ///     (VillageBook.FortVillageFood - gra albo E4/11, ta sama funkcja co targ przy zamku). Tylko MIASTA (97); zamki bez zmian (woz wsi zamkowej i danina
    ///     zalogi - CastleMarket, E5/B4). Oblezenie - gra i tak nie liczy wsi (nic do zdjecia).
    ///  2. PRAWDZIWA NADWYZKA: co dobe (po Famine.Daily - migawka spichlerzy tej doby, w pozycji MFolk ksiegi pieniadza) kazda wies miasta w stanie Normal,
    ///     bez najazdu, bez glodu gospodarzy (u = 0), sprzedaje miastu to, co lezy w jej spichlerzu ponad KeepShare (98%) sufitu (sufit = 2 lata potrzeby, f23) -
    ///     to, czego i tak by nie zmiescila (nadwyzka ponad sufit przepadala w Famine jako "towar na wozy"). Gdy okreg zjada wlasny zapas (spichlerz ponizej 98%) - nic.
    ///     Miasto w niedoborze (zapas ponizej ShortDays dni wlasnego spozycia: mieszczanie + zaloga, albo ponizej ShortCapShare sufitu zapasu - nad alarmem BK
    ///     ClusterFoodTracker: bilans &lt; 0 i zapas &lt; 25% sufitu przez 14 dob = okreg "w zastoju", BK tnie plon wsi i sle wozy jedzenia z innych miast) placi
    ///     drozej (cena kupna targu, nie sprzedazy), wiec wies sprzedaje takze z zapasu ponad KeepShareShort (rok wlasnej potrzeby) - do podwojnego progu niedoboru.
    ///     Glod przychodzi do miasta dopiero wtedy, gdy wsie maja juz tylko swoj rok, a targ jest pusty (BK przy zapasie &lt;= 10% sufitu dokupuje jedzenie z polki
    ///     na brak - EconomyPatches.ItemConsumptionPatch).
    ///     Jednostki: racje spichlerza (1 racja = jedzenie 1 czlowieka na dobe) / Q miasta (mieszczan na jednostke Food = ludzie ksiegi / (dobrobyt / 40) -
    ///     ta sama skala co Famine QOf), wiec racje wsi karmia tylu mieszczan, ilu ludzi wykarmilyby na wsi.
    ///  3. ZAMKNIETA EKONOMIA: racje schodza ze spichlerza wsi (Famine.TributeTake) i od razu wchodza do Town.FoodStocks (najwyzej do miejsca: sufit - zapas -
    ///     dzisiejszy dodatni bilans bez zakupu, zeby nic nie przepadlo na sufice gry); miasto placi wsi cena zboza na swoim targu (Town.GetItemPrice(grain,
    ///     sprzedajacy); w niedoborze cena kupna) za jednostke Food - gra liczy jedna sztuke jedzenia z targu jako +1 Food) z nadwyzki kasy ponad zapas kupcow (TownPurse.SpendableTown;
    ///     brak kasy - wies nie sprzedaje, nadwyzka zostaje w spichlerzu). Zloto: kasa miasta -> kiesa wsi (ChangeGold para) w naszym ticku dobowym (MoneyLedger
    ///     BlockOpen .. Mark(MFolk): migawki ksiegi lapia przelew, bez nowej pozycji Note169). Decyzja: sprzedaz za cene targu, nie danina pana - danina od "nadwyzki
    ///     ponad zapas" to pobor resztowy (Jeff 17.10), a sprzedaz tego, czego wies nie zmiesci, to zwykly targ.
    ///  4. DYMEK I BILANS: dzisiejszy zakup widac w bilansie Food miasta ("Grain from village barns") do konca doby zakupu, poza wnetrzem Town.DailyTick
    ///     (prefiks/finalizer - zakup juz jest w zapasie, gra nie doda go drugi raz), i tylko do wysokosci dzisiejszego braku (recenzja 20.10: zakup w niedoborze
    ///     to jednorazowo kilka tysiecy Food - jako stopa dobowa udawalby plus, ktorego jutro nie bedzie: BK ClusterFoodTracker wzialby miasto za zrodlo wozow
    ///     jedzenia, BK dokup z polki i Famine/KS rano nastepnej doby liczylyby brak z wczorajszym zakupem). Nadwyzke zakupu pokazuje sam zapas.
    ///  5. BEZPIECZNIK BetterEconomy: BE zabiera 0.5% chlopow (0.4% rzemieslnikow) dziennie, gdy Town.FoodChange &lt; -3 - przy kazdym zapasie; ten sam prog
    ///     przelacza miasto AI na polityke FoodRelief, wybiera projekt Granaries i obniza poparcie cechow (pospolstwo, wlasciciele ziemi). Mieszczanie
    ///     jedza tez z targu (gra dopisuje kazda sprzedana sztuke jedzenia do bilansu - SoldItems), wiec ujemny bilans przy pelnym spichlerzu i targu z jedzeniem
    ///     to nie glod. Dlatego tylko w czasie dobowych tickow BE (prefiks/finalizer PopulationCampaignBehavior.OnSettlementDailyTick i
    ///     TownEconomyCampaignBehavior.OnSettlementDailyTick) bilans miasta, ktore ma jedzenie (zapas Food co najmniej na 1 dobe spozycia albo na polce targu
    ///     tyle sztuk jedzenia), nie spada ponizej progu BE. Glod BE zostaje, gdy naprawde nic nie ma: pusty spichlerz miasta i pusty targ (oblezenie, spalone
    ///     wsie, odciete drogi; BE i tak sprawdza tez FoodStocks &lt; 20). Gra, BK, Famine i reszta widza prawdziwy bilans.
    ///  6. WERBUNEK ZALOGI (regresja zdjetego skladnika wsi): gra dobiera zalodze ludzi tylko przy Town.FoodChange &gt; 0 (GarrisonRecruitmentCampaignBehavior
    ///     .CanSettlementAutoRecruit). Dotad skladnik wsi trzymal bilans prawie kazdego miasta na plusie; teraz miasto, ktore zyje z zapasu i targu, mialoby
    ///     ujemny bilans przy pelnym spichlerzu i przestaloby werbowac. Dlatego tylko w czasie tego testu bilans miasta, ktore nie jest w niedoborze (zapas
    ///     co najmniej prog niedoboru z pkt 2) i ktore ze starym skladnikiem wsi mialoby plus, nie spada ponizej +0.01. Miasto w niedoborze nie werbuje - jak
    ///     w grze przy braku jedzenia; miasto z deficytem takze ze starym skladnikiem wsi - regula gry bez zmian (recenzja 20.10). Okno testu: prefiks/finalizer
    ///     na OnDailySettlementTick (wolajacy; recenzja 20.10: BetterEconomy lata go w OnSubModuleLoad, a Harmony kompiluje zastepnik od razu - JIT mogl wkleic
    ///     do niego oryginal malej CanSettlementAutoRecruit przed nasza latka; nasza latka na wolajacym buduje zastepnik od nowa, a MonoMod.Core wylacza
    ///     wklejanie zalatanej metody) i na CanSettlementAutoRecruit (dymek zalogi GetGarrisonChangeExplainedNumber).
    /// Klucz wylaczony = wszystko jak w grze, bit w bit (postfiksy wracaja na wejsciu). Famine wylaczone - skladnik wsi zostaje (nie ma czego sprzedac), bezpiecznik
    /// BE dziala. Stan bez zapisu: zakup dnia widac w bilansie do konca doby zakupu (po wczytaniu dymek bez zakupu do pierwszego Daily). Moze isc z watkow AI (postfiks): tylko
    /// odczyty, ksiega dnia (slownik + doba) podmieniana w calosci (bez blokad), liczniki Interlocked. Potkniecia liczone per miejsce (bez globalnych wylacznikow, CLAUDE.md 7).
    /// </summary>
    internal static class TownGranary
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double KeepShare = 0.98;        // wies trzyma do 98% sufitu spichlerza (2 lata potrzeby) - ponad to sprzedaje miastu (WallTribute CapFill)
        private const double KeepShareShort = 0.5;    // miasto w niedoborze: wies sprzedaje takze z zapasu ponad rok wlasnej potrzeby (polowa sufitu 2 lat)
        private const double ShortDays = 30.0;        // miasto w niedoborze: zapas Food ponizej tylu dni spozycia (mieszczanie + zaloga)
        private const double ShortCapShare = 0.30;    // ... albo ponizej 30% sufitu zapasu (nad alarmem zastoju BK - 25% sufitu przy ujemnym bilansie)
        private const double MinUnits = 0.05;         // mniej jednostek Food sie nie kupuje
        private const double QDefault = 906.0;        // GL 2.3 (Famine.QWorldDefault): mieszczan na jednostke Food, gdy miasta nie da sie policzyc
        private const float BeDefault = -3f;          // BetterEconomySettings.StarvationFoodChangeThreshold (domyslnie)

        private static readonly TextObject _txtKeep = new TextObject("{=!}Villages keep their grain");
        private static readonly TextObject _txtBought = new TextObject("{=!}Grain from village barns");
        private static readonly TextObject _txtStore = new TextObject("{=!}Food in store");

        /// <summary>Zakup dnia (jednostki Food na miasto) i doba zakupu - podmieniany w calosci w Daily, czytany w postfiksie (takze z watkow AI).</summary>
        private sealed class DayBook
        {
            internal readonly Dictionary<Town, float> Map; internal readonly int Day;
            internal DayBook(Dictionary<Town, float> map, int day) { Map = map; Day = day; }
        }
        private static readonly DayBook Empty = new DayBook(new Dictionary<Town, float>(), int.MinValue);
        private static volatile DayBook _today = Empty;
        [ThreadStatic] private static bool _inTownTick;                   // wnetrze Town.DailyTick - zakup juz w zapasie
        [ThreadStatic] private static bool _noTerm;                       // nasz pomiar bilansu bez zakupu dnia
        [ThreadStatic] private static Town _beTown;                       // wnetrze BE PopulationCampaignBehavior.OnSettlementDailyTick - ta osada
        [ThreadStatic] private static Town _beEcoTown;                    // wnetrze BE TownEconomyCampaignBehavior.OnSettlementDailyTick - ta osada
        [ThreadStatic] private static Town _garTown;                      // wnetrze OnDailySettlementTick / CanSettlementAutoRecruit (werbunek zalogi gry) - ta osada
        [ThreadStatic] private static bool _garTick;                      // wnetrze dziennego ticku zalogi gry (OnDailySettlementTick)

        private static bool _foodPatched, _tickPatched, _bePatched, _beEcoPatched, _beTried, _garPatched, _garTickPatched;
        private static string _beWhy = "nie sprawdzone", _beEcoWhy = "nie sprawdzone", _garWhy = "nie sprawdzone";
        private static FieldInfo _beThrField;
        private static float _beThr = BeDefault;
        private static Harmony _h;

        // liczniki (bez zapisu)
        private static int _beGuard, _beHungry, _beSeen;                 // od ostatniego Daily: bilans podniesiony do progu BE / glod BE bez jedzenia / testy BE miast
        private static int _beEcoSeen, _beEcoGuard, _beEcoHungry;        // od ostatniego Daily: odczyty bilansu w gospodarce miasta BE / podniesione do progu BE / bez jedzenia
        private static int _garTickN, _garReads;                         // od ostatniego Daily: dzienne ticki zalogi miast / odczyty bilansu w nich
        private static int _garSeen, _garGuard, _garShort, _garOld;      // od ostatniego Daily: testy werbunku zalogi miast z bilansem <= 0 / podniesione do +0.01 / w niedoborze (bez werbunku) / deficyt takze ze starym skladnikiem wsi (regula gry)
        private static readonly HashSet<string> _beGuardTowns = new HashSet<string>();
        private static readonly object Gate = new object();
        private static double _sessUnits, _sessRations; private static long _sessGold;
        private static int _stumbles, _stumblesAll;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _offLogged;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.TownGranaryFromVillages; } }
        /// <summary>Sprzedaz zboza i zdjecie skladnika wsi: klucz, spichlerze wsi czynne (Famine) i latka modelu wpieta.</summary>
        private static bool Live { get { return _foodPatched && KeyOn && Famine.On; } }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania / wczytanie): stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset()
        {
            _today = Empty;
            lock (Gate) { _beGuardTowns.Clear(); }
            _beGuard = _beHungry = _beSeen = 0;
            _beEcoSeen = _beEcoGuard = _beEcoHungry = 0;
            _garTickN = _garReads = 0;
            _garSeen = _garGuard = _garShort = _garOld = 0;
            _sessUnits = _sessRations = 0.0; _sessGold = 0;
            _stumbles = _stumblesAll = 0;
            lock (_errSites) _errSites.Clear();
            _offLogged = false;
        }

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);
            System.Threading.Interlocked.Increment(ref _stumblesAll);
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) { try { Log.Error("TownGranary." + where, e); } catch { } }
        }

        private static string F(double v, string f) { return double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString(f, Inv); }

        // ------------------------------------------------------------ wpiecie
        /// <summary>SubModuleMain (start gry): postfiks bazowego modelu jedzenia warowni, prefiks/finalizer Town.DailyTick, CanSettlementAutoRecruit i OnDailySettlementTick (werbunek zalogi) - typy gry. Raz na proces.</summary>
        internal static void ApplyAll(Harmony h)
        {
            _h = h;
            string missing = "-";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange", new[] { typeof(Town), typeof(bool), typeof(bool) });
                if (m != null)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(TownGranary), nameof(FoodPostfix)) { priority = Priority.Last });
                    _foodPatched = true;
                }
                else missing = "DefaultSettlementFoodModel.CalculateTownFoodStocksChange";
            }
            catch (Exception e) { missing = "CalculateTownFoodStocksChange (" + e.GetType().Name + ")"; Log.Error("TownGranary.ApplyAll", e); }
            try
            {
                var t = AccessTools.DeclaredMethod(typeof(Town), "DailyTick");
                if (t != null)
                {
                    h.Patch(t, prefix: new HarmonyMethod(typeof(TownGranary), nameof(TickPre)) { priority = Priority.First },
                               finalizer: new HarmonyMethod(typeof(TownGranary), nameof(TickFin)));
                    _tickPatched = true;
                }
                else missing = (missing == "-" ? "" : missing + ", ") + "Town.DailyTick";
            }
            catch (Exception e) { missing = (missing == "-" ? "" : missing + ", ") + "Town.DailyTick (" + e.GetType().Name + ")"; Log.Error("TownGranary.ApplyAll(tick)", e); }
            var grc = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.GarrisonRecruitmentCampaignBehavior);
            try
            {
                // werbunek zalogi gry (pkt 6): mala metoda prywatna - dymek zalogi (GetGarrisonChangeExplainedNumber, kompilowany po tej latce) i dzienny tick
                var g = AccessTools.DeclaredMethod(grc, "CanSettlementAutoRecruit", new[] { typeof(Settlement) });
                if (g == null) missing = (missing == "-" ? "" : missing + ", ") + "CanSettlementAutoRecruit";
                else
                {
                    h.Patch(g, prefix: new HarmonyMethod(typeof(TownGranary), nameof(GarPre)) { priority = Priority.First },
                               finalizer: new HarmonyMethod(typeof(TownGranary), nameof(GarFin)));
                    _garPatched = true;
                }
            }
            catch (Exception e) { missing = (missing == "-" ? "" : missing + ", ") + "CanSettlementAutoRecruit (" + e.GetType().Name + ")"; Log.Error("TownGranary.ApplyAll(zaloga)", e); }
            try
            {
                // ... i wolajacy - dzienny tick zalogi (recenzja 20.10): BetterEconomy lata OnDailySettlementTick juz w OnSubModuleLoad, a Harmony kompiluje zastepnik
                // od razu - JIT mogl wkleic do niego oryginal CanSettlementAutoRecruit. Ta latka (po latce CanSettlementAutoRecruit) buduje zastepnik od nowa
                // (MonoMod.Core wylacza wklejanie zalatanej metody), a okno testu stoi na wolajacym - dziala, nawet gdyby cos wkleilo mala metode
                var gt = AccessTools.DeclaredMethod(grc, "OnDailySettlementTick", new[] { typeof(Settlement) });
                if (gt == null) missing = (missing == "-" ? "" : missing + ", ") + "OnDailySettlementTick";
                else
                {
                    h.Patch(gt, prefix: new HarmonyMethod(typeof(TownGranary), nameof(GarTickPre)) { priority = Priority.First },
                                finalizer: new HarmonyMethod(typeof(TownGranary), nameof(GarTickFin)));
                    _garTickPatched = true;
                }
            }
            catch (Exception e) { missing = (missing == "-" ? "" : missing + ", ") + "OnDailySettlementTick (" + e.GetType().Name + ")"; Log.Error("TownGranary.ApplyAll(tick zalogi)", e); }
            _garWhy = _garTickPatched && _garPatched ? "wpiety (OnDailySettlementTick i CanSettlementAutoRecruit)"
                    : _garTickPatched ? "wpiety (OnDailySettlementTick; BRAK CanSettlementAutoRecruit - dymek zalogi bez oslony)"
                    : _garPatched ? "CZESCIOWO (tylko CanSettlementAutoRecruit - BRAK OnDailySettlementTick)" : "BRAK (GarrisonRecruitmentCampaignBehavior)";
            Log.Info("Spichlerz miejski (p6): latki przy starcie gry - bilans jedzenia miasta " + (_foodPatched ? "wpiety (DefaultSettlementFoodModel, Last)" : "BRAK")
                     + ", Town.DailyTick " + (_tickPatched ? "wpiety" : "BRAK") + ", werbunek zalogi " + _garWhy + "; BRAK: " + missing + ".");
        }

        /// <summary>ArmouryBehavior.OnSessionLaunched: bezpiecznik BetterEconomy (typ BE moze ladowac sie po Armoury - dopiero w kampanii), raz na proces; linia startu.</summary>
        internal static void EnsureHooks()
        {
            if (!_beTried && _h != null)
            {
                _beTried = true;
                try
                {
                    var t = AccessTools.TypeByName("BetterEconomy.Behaviors.PopulationCampaignBehavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "OnSettlementDailyTick", new[] { typeof(Settlement) }) : null;
                    if (t == null) _beWhy = "bez BetterEconomy";
                    else if (m == null) _beWhy = "BRAK (PopulationCampaignBehavior.OnSettlementDailyTick)";
                    else
                    {
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(TownGranary), nameof(BePre)) { priority = Priority.First },
                                    finalizer: new HarmonyMethod(typeof(TownGranary), nameof(BeFin)));
                        _bePatched = true;
                        _beWhy = "wpiety (PopulationCampaignBehavior.OnSettlementDailyTick)";
                    }
                    var ts = AccessTools.TypeByName("BetterEconomy.Config.BetterEconomySettings");
                    _beThrField = ts != null ? AccessTools.Field(ts, "StarvationFoodChangeThreshold") : null;
                }
                catch (Exception e) { _beWhy = "wyjatek " + e.GetType().Name; Stumble("EnsureHooks(BE)", e); }
                try
                {
                    // recenzja 20.10: ten sam prog BE w gospodarce miasta - polityka AI FoodRelief, projekt Granaries, dryf cechow (TickGuilds) - wszystko w tym ticku
                    var t = AccessTools.TypeByName("BetterEconomy.Behaviors.TownEconomyCampaignBehavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "OnSettlementDailyTick", new[] { typeof(Settlement) }) : null;
                    if (t == null) _beEcoWhy = "bez BetterEconomy";
                    else if (m == null) _beEcoWhy = "BRAK (TownEconomyCampaignBehavior.OnSettlementDailyTick)";
                    else
                    {
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(TownGranary), nameof(BeEcoPre)) { priority = Priority.First },
                                    finalizer: new HarmonyMethod(typeof(TownGranary), nameof(BeEcoFin)));
                        _beEcoPatched = true;
                        _beEcoWhy = "wpiety (TownEconomyCampaignBehavior.OnSettlementDailyTick)";
                    }
                }
                catch (Exception e) { _beEcoWhy = "wyjatek " + e.GetType().Name; Stumble("EnsureHooks(BE gospodarka)", e); }
            }
            ReadBeThr();
            var s = Settings.Current;
            Log.Info("Spichlerz miejski (p6): start sesji | klucz Town Granary From Villages " + (s != null && s.TownGranaryFromVillages ? "TAK" : "NIE")
                     + " | spichlerze wsi (Famine) " + (Famine.On ? "czynne" : "NIECZYNNE - skladnik wsi miast bez zmian") + " | bilans miasta " + (_foodPatched ? "wpiety" : "BRAK")
                     + ", Town.DailyTick " + (_tickPatched ? "wpiety" : "BRAK") + " | bezpiecznik BE " + _beWhy + ", prog BE " + F(_beThr, "0.##")
                     + " | gospodarka miasta BE (polityka, projekty, cechy) " + _beEcoWhy
                     + " | werbunek zalogi " + _garWhy
                     + " | wies trzyma " + F(KeepShare * 100.0, "0") + "% sufitu spichlerza (miasto w niedoborze ponizej " + F(ShortDays, "0") + " dni spozycia albo "
                     + F(ShortCapShare * 100.0, "0") + "% sufitu zapasu: " + F(KeepShareShort * 100.0, "0")
                     + "%), zaplata cena zboza targu miasta (sprzedazy; w niedoborze kupna) z nadwyzki kasy ponad zapas kupcow.");
        }

        private static void ReadBeThr()
        {
            try
            {
                if (_beThrField == null) return;
                object v = _beThrField.GetValue(null);
                if (v is float f && !float.IsNaN(f) && !float.IsInfinity(f)) _beThr = f;
            }
            catch (Exception e) { Stumble("ReadBeThr", e); }
        }

        public static void TickPre() { _inTownTick = true; }
        public static void TickFin() { _inTownTick = false; }
        public static void BePre(Settlement __0) { try { _beTown = __0 != null && __0.IsTown ? __0.Town : null; } catch { _beTown = null; } }
        public static void BeFin() { _beTown = null; }
        public static void BeEcoPre(Settlement __0, out Town __state) { __state = _beEcoTown; try { _beEcoTown = __0 != null && __0.IsTown ? __0.Town : null; } catch { _beEcoTown = null; } }
        public static void BeEcoFin(Town __state) { _beEcoTown = __state; }
        // okno werbunku: dzienny tick (wolajacy) i sama CanSettlementAutoRecruit (dymek) - poprzednia osada wraca w finalizerze (zagniezdzenie tick -> metoda)
        public static void GarPre(Settlement __0, out Town __state) { __state = _garTown; try { _garTown = __0 != null && __0.IsTown ? __0.Town : null; } catch { _garTown = null; } }
        public static void GarFin(Town __state) { _garTown = __state; }
        public static void GarTickPre(Settlement __0, out Town __state)
        {
            __state = _garTown;
            try { _garTown = __0 != null && __0.IsTown ? __0.Town : null; } catch { _garTown = null; }
            _garTick = true;
            if (_garTown != null) System.Threading.Interlocked.Increment(ref _garTickN);
        }
        public static void GarTickFin(Town __state) { _garTown = __state; _garTick = false; }

        // ------------------------------------------------------------ bilans jedzenia miasta (postfiks, moze isc z watkow AI)
        /// <summary>
        /// Postfiks (Last) na DefaultSettlementFoodModel.CalculateTownFoodStocksChange - tylko miasta i tylko przy kluczu: (1) skladnik wsi zdjety (Live, bez oblezenia),
        /// (2) zakup dnia (poza Town.DailyTick i naszym pomiarem; do wysokosci dzisiejszego braku, tylko w dobie zakupu), (3) w dobowych tickach BE (ludnosc
        /// i gospodarka miasta) - bilans miasta z jedzeniem nie nizszy niz prog BE, (4) w tescie werbunku zalogi gry - bilans miasta bez niedoboru, ktore ze starym
        /// skladnikiem wsi mialoby plus, nie nizszy niz +0.01 (tylko przy Famine). Nie rzuca.
        /// </summary>
        public static void FoodPostfix(Town town, ref ExplainedNumber __result)
        {
            if (!_foodPatched || town == null) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.TownGranaryFromVillages) return;
                var st = town.Settlement;
                if (st == null || !st.IsTown) return;
                double vt = 0.0;   // zdjety dzis skladnik wsi (0 pod oblezeniem i bez Famine)
                if (Famine.On && !town.IsUnderSiege)
                {
                    double v = VillageTerm(st);
                    if (v > 0.001 && !double.IsInfinity(v)) { __result.Add(-(float)v, _txtKeep); vt = v; }
                }
                // zakup dnia (takze pod oblezeniem w ciagu doby: zakup z rana juz w zapasie)
                if (Famine.On && !_inTownTick && !_noTerm) AddBought(town, ref __result);
                bool bePop = ReferenceEquals(_beTown, town), beEco = !bePop && ReferenceEquals(_beEcoTown, town);
                if (bePop || beEco)
                {
                    if (bePop) System.Threading.Interlocked.Increment(ref _beSeen); else System.Threading.Interlocked.Increment(ref _beEcoSeen);
                    float r = __result.ResultNumber, thr = _beThr;
                    if (r < thr)
                    {
                        if (HasFood(town, st))
                        {
                            __result.Add(thr + 0.01f - r, _txtStore);
                            if (bePop)
                            {
                                System.Threading.Interlocked.Increment(ref _beGuard);
                                lock (Gate) _beGuardTowns.Add(st.StringId ?? "?");
                            }
                            else System.Threading.Interlocked.Increment(ref _beEcoGuard);
                        }
                        else if (bePop) System.Threading.Interlocked.Increment(ref _beHungry);
                        else System.Threading.Interlocked.Increment(ref _beEcoHungry);
                    }
                }
                if (ReferenceEquals(_garTown, town) && Famine.On)   // tylko gdy skladnik wsi zdjety (Famine wylaczone - gra bez zmian)
                {
                    if (_garTick) System.Threading.Interlocked.Increment(ref _garReads);
                    float r = __result.ResultNumber;
                    if (!(r > 0f))
                    {
                        System.Threading.Interlocked.Increment(ref _garSeen);
                        if (IsShort(town)) System.Threading.Interlocked.Increment(ref _garShort);
                        else if (!(r + vt > 0.0)) System.Threading.Interlocked.Increment(ref _garOld);   // deficyt takze ze starym skladnikiem wsi - regula gry bez zmian
                        else
                        {
                            __result.Add(0.01f - r, _txtStore);
                            System.Threading.Interlocked.Increment(ref _garGuard);
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("FoodPostfix", e); }
        }

        /// <summary>
        /// Zakup dnia w bilansie: tylko do konca doby zakupu i tylko do wysokosci dzisiejszego braku (bilans bez zakupu ponizej 0) - nadwyzka zakupu jest juz
        /// w zapasie, a jako stopa dobowa udawalaby plus, ktorego jutro nie bedzie (recenzja 20.10). Bez alokacji. Wyjatek leci do wolajacego.
        /// </summary>
        private static void AddBought(Town town, ref ExplainedNumber n)
        {
            var d = _today;
            float u;
            if (d.Map.Count == 0 || !d.Map.TryGetValue(town, out u) || !(u > 0f)) return;
            if ((int)CampaignTime.Now.ToDays > d.Day) return;   // zakup z poprzedniej doby (rano przed naszym Daily: Famine, KS) - juz w zapasie
            float add = Math.Min(u, -n.ResultNumber);
            if (add > 0f) n.Add(add, _txtBought);
        }

        /// <summary>Dzisiejszy skladnik wsi miasta w bilansie (gra albo E4/11) - ta sama funkcja co targ przy zamku. Wyjatek leci do wolajacego.</summary>
        private static double VillageTerm(Settlement st)
        {
            var bv = st.BoundVillages;
            if (bv == null || bv.Count == 0) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < bv.Count; i++) sum += VillageBook.FortVillageFood(bv[i]);
            return double.IsNaN(sum) ? 0.0 : sum;
        }

        /// <summary>Spozycie doby miasta w jednostkach Food (mieszczanie dobrobyt / 40 + zaloga / 20 - jak baza gry).</summary>
        private static double Cons(Town t)
        {
            double c = Math.Max(0f, t.Prosperity) / 40.0;
            try { var g = t.GarrisonParty; if (g != null && g.Party != null) c += g.Party.NumberOfAllMembers / 20.0; } catch { }
            return c;
        }

        /// <summary>Prog niedoboru miasta (jednostki Food): ShortDays dni spozycia albo ShortCapShare sufitu zapasu - wiekszy. Wyjatek leci do wolajacego.</summary>
        private static double ShortAt(Town t, double cons) { return Math.Max(ShortDays * cons, ShortCapShare * t.FoodStocksUpperLimit()); }

        /// <summary>Miasto w niedoborze (zapas ponizej progu niedoboru). Bez alokacji. Wyjatek leci do wolajacego.</summary>
        private static bool IsShort(Town t)
        {
            double cons = Cons(t);
            return cons > 0.0 && Math.Max(0f, t.FoodStocks) < ShortAt(t, cons);
        }

        /// <summary>Miasto ma jedzenie: zapas Food co najmniej na 1 dobe spozycia albo na polce targu tyle sztuk jedzenia. Bez alokacji. Wyjatek leci do wolajacego.</summary>
        private static bool HasFood(Town t, Settlement st)
        {
            double need = Math.Max(1.0, Cons(t));
            if (t.FoodStocks >= need) return true;
            var r = st.ItemRoster;
            if (r == null) return false;
            double n = 0.0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || !it.IsFood || el.Amount <= 0) continue;
                n += el.Amount;
                if (n >= need) return true;
            }
            return false;
        }

        /// <summary>Mieszczan na jednostke Food miasta: ludzie ksiegi / (dobrobyt / 40) - jak Famine.QOf; brak danych - QDefault.</summary>
        private static double QOf(Town t)
        {
            double units = Math.Max(0f, t.Prosperity) / 40.0;
            double people = PopulationLaw.PeopleOf(t.Settlement);
            double q = units > 0.0 && people > 0.0 ? people / units : QDefault;
            return double.IsNaN(q) || double.IsInfinity(q) || q < 1.0 ? QDefault : q;
        }

        // ------------------------------------------------------------ doba (ArmouryBehavior.OnDailyTick, po Famine.Daily, w pozycji MFolk ksiegi pieniadza)
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            if (!s.TownGranaryFromVillages)
            {
                _today = Empty;
                if (!_offLogged && s.LogEnabled) { _offLogged = true; Log.Info("Spichlerz miejski (p6): WYLACZONY (Town Granary From Villages w MCM) - zywnosc miast ze wsi jak w grze."); }
                return;
            }
            _offLogged = false;
            long t0 = Stopwatch.GetTimestamp();
            ReadBeThr();
            int today = (int)CampaignTime.Now.ToDays;
            bool live = Live;
            var next = new Dictionary<Town, float>();
            int towns = 0, besieged = 0, walkers = 0, fedTowns = 0, fedVills = 0, shortTowns = 0, shortVills = 0, fullTown = 0, tinyVills = 0, noGold = 0, surplusVills = 0;
            double units = 0.0, rations = 0.0, barnDrop = 0.0, priceSum = 0.0, stockIn = 0.0; long gold = 0, townOut = 0, vilIn = 0; int priceN = 0;
            foreach (var town in Town.AllTowns)
            {
                try
                {
                    var st = town != null ? town.Settlement : null;
                    if (st == null || !st.IsTown) continue;
                    towns++;
                    if (OthersRise.IsWhiteWalkerClan(st.MapFaction as Clan)) { walkers++; continue; }   // ROT zwraca 0 przed baza - postfiks nie biegnie
                    if (st.IsUnderSiege) { besieged++; continue; }
                    if (!live) continue;
                    var bv = st.BoundVillages;
                    if (bv == null || bv.Count == 0) continue;
                    double cons = Cons(town);
                    double stocks = Math.Max(0f, town.FoodStocks);
                    double cap = town.FoodStocksUpperLimit();
                    float bal;
                    _noTerm = true;
                    try { bal = town.FoodChange; } finally { _noTerm = false; }
                    double room = cap - stocks - Math.Max(0.0, (double)bal);
                    double shortAt = ShortAt(town, cons);
                    bool shortT = cons > 0.0 && stocks < shortAt;
                    if (shortT)
                    {
                        // niedobor: miasto kupuje z zapasu wsi tylko do podwojnego progu niedoboru (plus dzisiejszy brak) - bez zrzutu pol spichlerzy okregu w jedna dobe
                        shortTowns++;
                        room = Math.Min(room, Math.Max(0.0, 2.0 * shortAt - stocks - (double)bal));
                    }
                    double q = QOf(town);
                    int spend = -1, price = 0;   // kasa i cena - dopiero przy pierwszej wsi z nadwyzka (BuyFloor liczy zapytania do linii 169c)
                    double got = 0.0; long paid = 0; bool any = false;
                    float fs0 = town.FoodStocks; int tg0 = town.Gold;   // kontrola: zapas i kasa miasta przed zakupem (roznica po petli = to, co weszlo / wyszlo)
                    for (int i = 0; i < bv.Count; i++)
                    {
                        var v = bv[i];
                        if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal || v.Settlement.IsUnderRaid) continue;
                        double home, harv, food, vcap; float u; int d;
                        if (!Famine.DayOf(v, out home, out harv, out food, out vcap, out u, out d) || d != today || u > 0f || !(vcap > 0.0)) continue;
                        double sur = food - (shortT ? KeepShareShort : KeepShare) * vcap;
                        if (!(sur > 0.0)) continue;
                        surplusVills++;
                        double want = Math.Min(sur / q, room - got);
                        if (want < MinUnits)
                        {
                            if (room - got < MinUnits) fullTown++; else tinyVills++;   // miasto pelne / nadwyzka wsi ponizej 0.05 Food (recenzja 20.10: osobno)
                            continue;
                        }
                        if (spend < 0) { spend = Math.Max(0, TownPurse.SpendableTown(town)); price = Math.Max(1, town.GetItemPrice(DefaultItems.Grain, null, !shortT)); }   // niedobor - cena kupna targu (drozej)
                        long afford = spend - paid;
                        double canU = afford / (double)price;
                        if (canU < want) { want = canU; noGold++; }
                        if (want < MinUnits) continue;
                        double took = Famine.TributeTake(v, want * q);   // racje ze spichlerza wsi (nie wiecej niz lezy)
                        if (!(took > 0.0)) continue;
                        double fAfter;   // kontrola: prawdziwy ubytek stodoly wsi (stan przed - stan po), nie z tych samych zmiennych co zakup
                        if (Famine.FoodOf(v, out fAfter)) barnDrop += food - fAfter;
                        double gu = took / q;
                        town.FoodStocks += (float)gu;                     // od razu - zboze zdjete ze spichlerza wsi jest juz w miescie, nawet gdyby dalej cos rzucilo
                        long pay = (long)Math.Round(gu * price);
                        if (pay > afford) pay = afford;
                        if (pay < 0) pay = 0;
                        int vg0 = v.Gold;
                        if (pay > 0) { town.ChangeGold(-(int)pay); v.ChangeGold((int)pay); }
                        vilIn += v.Gold - vg0;
                        // liczniki po kazdej sprzedazy (wyjatek dalej w tym miescie nie gubi tego, co juz przeszlo)
                        if (!any) { fedTowns++; priceSum += price; priceN++; }
                        got += gu; paid += pay; units += gu; gold += pay; rations += took; fedVills++; any = true;
                        next[town] = (float)got;
                        if (shortT && food - took < KeepShare * vcap) shortVills++;   // sprzedala z zapasu ponizej 98% sufitu (miasto w niedoborze)
                    }
                    if (any) { stockIn += town.FoodStocks - fs0; townOut += tg0 - town.Gold; }
                }
                catch (Exception e) { Stumble("Daily(miasto)", e); }
            }
            _today = new DayBook(next, today);
            _sessUnits += units; _sessRations += rations; _sessGold += gold;
            if (!s.LogEnabled) { NewDay(); return; }
            // pomiar po zakupie: pelny bilans (z naszymi skladnikami), Food 0, skladnik wsi zdjety
            int neg = 0, negPeace = 0, hungry = 0, hungryPeace = 0, termTowns = 0, below80 = 0; double termSum = 0.0, low = double.MaxValue; string lowName = "-";
            foreach (var town in Town.AllTowns)
            {
                try
                {
                    var st = town != null ? town.Settlement : null;
                    if (st == null || !st.IsTown || OthersRise.IsWhiteWalkerClan(st.MapFaction as Clan)) continue;
                    float b = town.FoodChange;
                    if (b < _beThr) { neg++; if (!st.IsUnderSiege) negPeace++; }
                    if (b < low) { low = b; lowName = st.Name != null ? st.Name.ToString() : st.StringId; }
                    if (town.FoodStocks <= 0f) { hungry++; if (!st.IsUnderSiege) hungryPeace++; }
                    if (!st.IsUnderSiege && town.FoodStocks < 0.8f * town.FoodStocksUpperLimit()) below80++;   // BK BKLoyaltyModel: +0.5 lojalnosci "Well fed populace" od 80% sufitu
                    if (live && !st.IsUnderSiege) { double v = VillageTerm(st); if (v > 0.001) { termTowns++; termSum += v; } }
                }
                catch (Exception e) { Stumble("Daily(pomiar)", e); }
            }
            int guardTowns; lock (Gate) guardTowns = _beGuardTowns.Count;
            var sb = new StringBuilder(1200);
            sb.Append("Spichlerz miejski (p6): dzien ").Append(today).Append(live ? " | CZYNNE" : " | SPICHLERZE WSI NIECZYNNE (Famine) - skladnik wsi miast jak w grze, bezpiecznik BE czynny")
              .Append(" | miast ").Append(towns).Append(" (oblezone ").Append(besieged).Append(", Innych ").Append(walkers).Append(')')
              .Append(" | zboze ze wsi do miast dzis ").Append(F(units, "0.##")).Append(" szt./d Food (").Append(F(rations, "0")).Append(" racji) z ").Append(fedVills)
              .Append(" wsi do ").Append(fedTowns).Append(" miast; wsi z nadwyzka ").Append(surplusVills).Append(", bez miejsca w miescie ").Append(fullTown)
              .Append(", nadwyzka ponizej ").Append(F(MinUnits, "0.##")).Append(" Food ").Append(tinyVills).Append(", bez zaplaty (kasa miasta) ").Append(noGold)
              .Append(" | zloto miasto->wies ").Append(gold).Append(" zl (cena zboza srednio ").Append(priceN > 0 ? F(priceSum / priceN, "0.#") : "-").Append(" zl/szt.)")
              .Append(" | miasta w niedoborze (zapas < ").Append(F(ShortDays, "0")).Append(" dni albo ").Append(F(ShortCapShare * 100.0, "0")).Append("% sufitu): ").Append(shortTowns).Append(", wsie sprzedaly z zapasu ponizej 98% sufitu ").Append(shortVills)
              .Append(" | skladnik wsi gry zdjety -").Append(F(termSum, "0.#")).Append("/d w ").Append(termTowns).Append(" miastach")
              .Append(" | miasta z bilansem < ").Append(F(_beThr, "0.##")).Append(": ").Append(neg).Append(" (bez oblezenia ").Append(negPeace).Append("; najnizszy ").Append(lowName).Append(' ')
              .Append(low == double.MaxValue ? "-" : F(low, "0.#")).Append(')')
              .Append(" | miasta glodujace (Food 0): ").Append(hungry).Append(" (bez oblezenia ").Append(hungryPeace).Append(')')
              .Append(" | miasta ponizej 80% sufitu zapasu bez oblezenia (BK Well fed +0.5 lojalnosci): ").Append(below80)
              .Append(_bePatched ? " | BE od wczoraj: testy miast " : " | BE (" + _beWhy + "): testy miast ").Append(_beSeen).Append(", oslonieto ").Append(_beGuard).Append(" (").Append(guardTowns).Append(" miast), glod BE bez jedzenia ").Append(_beHungry)
              .Append(_beEcoPatched ? " | BE gospodarka miasta od wczoraj: odczyty bilansu " : " | BE gospodarka miasta (" + _beEcoWhy + "): odczyty bilansu ").Append(_beEcoSeen).Append(", oslonieto ").Append(_beEcoGuard).Append(", bez jedzenia ").Append(_beEcoHungry)
              .Append(_garTickPatched ? " | werbunek zalogi od wczoraj: dzienne ticki miast " : " | werbunek zalogi (" + _garWhy + "): dzienne ticki miast ").Append(_garTickN).Append(" (odczyty bilansu ").Append(_garReads).Append(')')
              .Append(", testy miast z bilansem <= 0 ").Append(_garSeen).Append(", oslonieto ").Append(_garGuard).Append(", deficyt takze ze skladnikiem wsi (regula gry) ").Append(_garOld)
              .Append(", w niedoborze bez werbunku ").Append(_garShort)
              .Append(" | kontrola: racje zdjete ").Append(F(rations, "0.##")).Append(" = ubytek stodol wsi ").Append(F(barnDrop, "0.##"))
              .Append("; zapas miast +").Append(F(stockIn, "0.##")).Append(" = zakup ").Append(F(units, "0.##"))
              .Append("; zloto: kasy miast -").Append(townOut).Append(", kiesy wsi +").Append(vilIn).Append(", zaplata ").Append(gold)
              .Append("; od wczytania ").Append(F(_sessUnits, "0.#")).Append(" szt. Food, ").Append(F(_sessRations, "0")).Append(" racji, ").Append(_sessGold).Append(" zl")
              .Append(" | potkniecia ").Append(_stumbles).Append(" (razem ").Append(_stumblesAll).Append(')')
              .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
            Log.Info(sb.ToString());
            NewDay();
        }

        private static void NewDay()
        {
            _beGuard = _beHungry = _beSeen = 0; _beEcoSeen = _beEcoGuard = _beEcoHungry = 0;
            _garTickN = _garReads = 0; _garSeen = _garGuard = _garShort = _garOld = 0; _stumbles = 0;
            lock (Gate) _beGuardTowns.Clear();
        }
    }
}
