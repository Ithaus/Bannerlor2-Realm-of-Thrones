using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// DANINY SREDNIOWIECZNE (DS) - KROK F3: MIASTA I ZAMKI (docs/PROJEKT-DANINY-SREDNIOWIECZNE-2026-10-17.md rozdz. 6.1, 10.3, 11; decyzja Jeffa 17.10: "daniny, oplaty
    /// i logika musi byc taka jak byla w sredniowieczu ... system dynamiczny"). Klucz glowny DsTowns (DOMYSLNIE WYLACZONY - projekt 10.4: do testu). Korona (nadania,
    /// pomoc od sprzedazy, dziesiata od ruchomosci, kredyt) - DsSeason.cs; zaliczki mieszczan - TownFolk.CrownLoans.Ds.cs. "Stare sumy, nowe formy":
    ///  - OKNO POMIARU (DsFarmMeasureDays, 28 dob): stare zasady dzialaja co do monety; liczymy na miasto i zamek, co dzis dostaja pan (zawor po dekrecie + czesc pana
    ///    podatku od skarbca ludu tego miasta + W7 z ludu tego miasta) i korona (czesc korony zaworu). Doba DsFarmMeasureDays + 1: FIRMY USTALONE - M2 firma miejska
    ///    panu F_L = 364 x srednia doby, M3 firma krolewska F_C (to samo z czesci korony), M4 firmy zamku (pan / korona z zaworu zamku). Rata 1/52 co 7 dob z kasy
    ///    (dzien raty wedlug FNV id osady), najwyzej do DsFarmFloorShare zapasu kupcow; brak -> ksiega zaleglosci (splacana pierwsza w nastepnych ratach). Oblezenie
    ///    i zmiana krolestwa wlasciciela w ostatnich 28 dobach (zdobycie / lup) - rata darowana. Martwy pan - rata czeka w ksiedze (jak zawor: "czeka").
    ///    Zaleglosc przez DsFarmDistraintTerms dni kwartalu z rzedu - ZAJECIE: pan i korona biora zaleglosc z kiesy gminy miasta (najwyzej 2/3 jej stanu w dzien raty).
    ///    Zaleglosc ponad DsFarmForgiveWeeks rat - darowana (licznik). ODNOWA co DsFarmRenewDays: firma w strone (doplyw netto kupcow 28 dob x udzial z dnia ustalenia),
    ///    najwyzej +-DsFarmRenewMax (miasto bez udzialu - udzial swiata).
    ///    f4 P5 (przeglad ekonomii 18.10): przy Ds Farm Review Days > 0 (91) zamiast odnowy - PRZEGLAD co kwartal w obie strony (w gore tylko bez zaleglosci), przy
    ///    zaleglosciach przez Ds Farm Arrears Review Terms dni kwartalu z rzedu - w dol WYMUSZONY o pelne Ds Farm Renew Max, najwyzej jedna zmiana na kwartal (Review).
    ///  - Po ustaleniu NIE biegna: zawor miast (TownPurse.Valve) i zamkow (CastlePurse), danina z oszczednosci P1 (KingdomTreasury.WaRate), podatek od skarbca ludu miast
    ///    i zamkow (TownFolk.PayHoard), danina wojenna miast 1%/d (Levies), W7 z ludu (LvSources). Zostaja: czynsz gruntowy M1, akcyza M5, myto, clo (x2 w wojnie - Levies).
    ///  - M6 WYPLATA KUPCOM: co dobe 1/DsMerchantDrawDays kasy ponad 1.5 x zapas kupcow - w miescie P (DsMerchantDrawPShare) i R jako zarobek (placa podatek pana i 184),
    ///    w zamku do kiesy podzamcza (zamiast "5% zaworu dla ludu" i 15% zaworu zamku).
    ///  - M7 POMOC OD SPRZEDAZY: w Levies min(DsAidesMax, g x DsAidesRate) sprzedazy kasy z doby ludu (lud, markietani i zycie partii, dwor, sprzedaz okna, wsie);
    ///    brak -> ksiega (splacana w dzien raty PRZED firma). M9 dziesiata od ruchomosci kasy: kasa ponad 1.5 x zapas x g x DsMovablesRate, 13 rat co 7 dob.
    /// RECENZJA 17.10 (poprawki):
    ///  - okno pomiaru startuje dopiero po rampie F1 (projekt 11), mierzy osobno czesc pana z kasy (zawor) i z OSZCZEDNOSCI LUDU (podatek od skarbca ludu, W7 z ludu) -
    ///    ta druga (FLf) placi lud z kies ponad zapas dni ("scot and lot", DsFarmFolkFromSavings), nie kasa kupcow; sprzedaz kasy i danine miasta 1%/d - pomoc od sprzedazy
    ///    przy g = 1 ponad dawna danine zdjeta z firmy korony (kasa przy g = 1 placi tyle, co w oknie);
    ///  - podstawa M7 = WLASNA sprzedaz kasy (bez czesci uslug ludu z zycia i dworu);
    ///  - PRZEJSCIE z rampa DsRampDays od ustalenia firm (wymog Jeffa): stare pobory x (1 - rampa), firmy, wyplata kupcom, pomoc, raty dziesiatej x rampa (NewShare/OldShare);
    ///  - zaleglosc pomocy splacana w dzien raty idzie do wplywu dnia przez LeviesToday (nie jako firma), darowana ponad DsFarmForgiveWeeks tygodni pomocy;
    ///  - podloga kasy = caly zapas kupcow przy regulatorze w trybie 1; odnowa firm juz od 7 dob pamieci doplywu (pamiec sesyjna).
    /// Zamknieta ekonomia: kazdy przelew mierzony u platnika i odbiorcy (kontrola linii "Firmy miast i zamkow (DS)": platnicy - odbiorcy = 0); zaleglosc to roszczenie,
    /// nie pieniadz. Zapis arm_daniny_miasta (SaveText): brak klucza (stary zapis) = start okna pomiaru przy wlaczonym kluczu. Wylaczenie klucza - stare zasady od nastepnej
    /// doby, zaleglosci zostaja w zapisie bez skutku, zaliczki mieszczan przechodza do zwyklego dlugu korona2. Kazde zdarzenie we wlasnym try, potkniecia liczone.
    /// </summary>
    internal static class DsTowns
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const double OverReserve = 1.5;                 // M6 i M9 (kasa): ponad 1.5 x zapas kupcow
        internal const int Quarter = 91, Instalments = 13;       // dni kwartalu (91/182/273/364), raty nadania
        private const int Weeks = 52, NetDays = 28, CaptureDays = 28, YearDays = 364, NetMinDays = 7;

        private sealed class Farm
        {
            public Settlement St;                                  // sesja
            public double ML, MC;                                  // okno pomiaru: pan (zawor po dekrecie), korona (zawor)
            public double MLf;                                     // okno pomiaru: pan z oszczednosci ludu (czesc pana podatku od skarbca ludu + W7 z ludu) - recenzja 17.10
            public double SW, LW;                                  // okno pomiaru: sprzedaz kasy (podstawa M7) i danina miasta 1%/d (recenzja 17.10: pomoc ponad nia zdjeta z firmy korony)
            public double FL, FC;                                  // firmy roczne z kasy (rata = F / 52): pan, korona
            public double FLf;                                     // firma pana z oszczednosci ludu (DsFarmFolkFromSavings - "scot and lot"; wylaczony - z kasy)
            public double AidCut;                                  // przy ustaleniu: firma korony zmniejszona o pomoc od sprzedazy (g = 1) ponad dawna danine miasta (zl/rok)
            public int Fixed = -1;                                 // doba ustalenia albo ostatniej odnowy
            public double Share = double.NaN;                      // firma na dobe / doplyw netto na dobe przy ustaleniu
            public double AL, AC, ALf;                             // zaleglosci firm: pan, korona, pan z oszczednosci ludu
            public int ATerms;                                     // dni kwartalu z rzedu z zaleglosciami
            public double AidArr; public string AidK;              // pomoc od sprzedazy niezaplacona (M7) i krolestwo
            public double AidCarry;                                // ulamek pomocy ponizej 1 zl (sesja)
            public double AidLast;                                 // pomoc nalezna ostatniej doby (miara darowania zaleglosci; sesja)
            public double MovLeft, MovPer, MovCur; public string MovK; public int MovSerial = -1;   // dziesiata od ruchomosci kasy (M9); MovCur - wymiar biezacej sesji (limit L1)
            // sesja
            public readonly double[] Net = new double[NetDays]; public int NH, NF;
            public long G0 = long.MinValue, PaidWin;               // zamek: kasa przy poprzedniej probce, pobory od niej
            public string OwnerK; public int CapDay = -1;          // krolestwo wlasciciela widziane ostatnio, doba zmiany
            public double Sales; public int SalesDay = -1;         // podstawa M7 z doby ludu
            public long SG0 = long.MinValue; public int SGDay = -1;  // f5-lud (Ks Farm Follows Surplus): kasa miasta na poczatku pomiaru przyrostu i doba (sesja)
        }

        // ------------------------------------------------------------ stan (zapis arm_daniny_miasta)
        private static int _dayOn = -1, _fixDay = -1, _mDays;
        private static double _worldShare = double.NaN;
        private static readonly Dictionary<string, Farm> _farm = new Dictionary<string, Farm>();

        // ------------------------------------------------------------ sesja
        /// <summary>DsSeason.Daily: dzis ustalono firmy - sesja przelaczenia (nadania wszystkich krolestw bez kroku 0.5).</summary>
        internal static bool SwitchDue;
        private static int _stumbles, _stumblesSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly List<string> _events = new List<string>();
        private static readonly Dictionary<string, long> _aidPend = new Dictionary<string, long>();   // id krolestwa -> zaleglosc pomocy zaplacona w dzien raty (w skarbcu) - do LeviesToday w AidesDay
        private static bool _waitLogged;                                                              // okno pomiaru czeka na koniec rampy F1 (linia raz)

        // ------------------------------------------------------------ liczniki od ostatniej linii
        private static int _nDue, _nDueT, _remitSiege, _remitCap, _noLord, _distN, _forgN, _renewN, _renewUp, _renewDown, _aidRemit, _aidForgN;
        private static double _dueL, _dueC, _dueLf, _remit, _noLordAmt, _arrNew, _arrPaid, _forgiven, _aidArrNew, _aidLost, _aidForg, _movAssess, _movRemit, _movRamp, _movShort, _movLost;
        private static long _paidL, _paidC, _paidLf, _distL, _distC, _drawT, _drawC, _aidPaid, _aidArrPaid, _movPaid;
        private static long _payOut, _payIn;
        // f4 P5 (przeglad ekonomii 18.10): przeglad firm co kwartal (Ds Farm Review Days) - w dol wymuszone przy zaleglosciach (tylko te, ktore naprawde obnizyly firme),
        // w gore wstrzymane przy zaleglosciach, bez zmiany, zmiana firm w gore / w dol (zl/rok, z czescia z oszczednosci ludu); recenzja f4: wymuszone przed wlasna doba
        // przegladu (tylko przy Ds Farm Review Days > kwartal) i najkrotszy odstep miedzy przegladami tej samej osady (kontrola limitu: najwyzej jedna zmiana na kwartal)
        private static int _revArrN, _revHeld, _revSame, _revEarly, _revMinGap = int.MaxValue;
        private static double _revUpAmt, _revDownAmt;

        private static void ClearCounters()
        {
            _nDue = _nDueT = _remitSiege = _remitCap = _noLord = _distN = _forgN = _renewN = _renewUp = _renewDown = _aidRemit = _aidForgN = 0;
            _dueL = _dueC = _dueLf = _remit = _noLordAmt = _arrNew = _arrPaid = _forgiven = _aidArrNew = _aidLost = _aidForg = _movAssess = _movRemit = _movRamp = _movShort = _movLost = 0;
            _paidL = _paidC = _paidLf = _distL = _distC = _drawT = _drawC = _aidPaid = _aidArrPaid = _movPaid = 0;
            _payOut = _payIn = 0;
            _revArrN = _revHeld = _revSame = _revEarly = 0; _revUpAmt = _revDownAmt = 0; _revMinGap = int.MaxValue;
            _stumbles = 0;
        }

        /// <summary>Konstruktor ArmouryBehavior: nowa sesja (zapis wczytuje Import po konstruktorze).</summary>
        internal static void Reset()
        {
            _dayOn = -1; _fixDay = -1; _mDays = 0; _worldShare = double.NaN; _farm.Clear();
            SwitchDue = false; _stumblesSess = 0; _err.Clear(); _events.Clear(); _aidPend.Clear(); _waitLogged = false; _atFlag = -1;
            ClearCounters();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSess++;
            try { if (_err.Add(where)) Log.Error("DsTowns." + where, e); } catch { }
        }

        // ============================================================ przelaczniki
        internal static bool Key { get { var s = Settings.Current; return s != null && (s.DsTowns || AutotestFlag); } }

        internal const string AutotestFlagName = "armoury-dst-wlacz.flag";
        private static int _atFlag = -1;   // -1 nie sprawdzony w tej sesji, 0 nie, 1 tak (Reset zeruje)

        /// <summary>Plik-przelacznik autotestu F3 (jak DsVillage armoury-ds-wlacz.flag): Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-dst-wlacz.flag
        /// wlacza DsTowns tylko w trybie autotestu CrashScribe (LogGate 1 / 2) - nigdy w grze Jeffa; sprawdzany raz na sesje.</summary>
        internal static bool AutotestFlag
        {
            get
            {
                if (_atFlag >= 0) return _atFlag == 1;
                bool f = false;
                try
                {
                    int m = LogGate.Mode;
                    if (m == 1 || m == 2)
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        f = System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutotestFlagName));
                    }
                }
                catch { f = false; }
                if (Campaign.Current != null) _atFlag = f ? 1 : 0;
                return f;
            }
        }

        /// <summary>F3 czynne: klucz, kiesa ludu (TownFolk.Live), korona z biezacych wplywow (165) i zawor kas miast (A1 - kasy to prawdziwy pieniadz).</summary>
        internal static bool On { get { return Key && TownFolk.Live && CrownIncome.On && TownPurse.ValveLive; } }

        /// <summary>Okno pomiaru: stare zasady, liczymy zawor.</summary>
        internal static bool InWindow { get { return _dayOn >= 0 && _fixDay < 0 && On; } }

        /// <summary>Firmy ustalone: nowe zasady miast, zamkow i korony.</summary>
        internal static bool FarmsLive { get { return _fixDay >= 0 && On; } }
        /// <summary>Doba ustalenia firm (poczatek rampy F3); -1 - jeszcze nie (DsSeason: sesja nadan 'po rampie').</summary>
        internal static int FixDay { get { return _fixDay; } }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static double Unit(double v) { return double.IsNaN(v) ? 0 : (v < 0 ? 0 : (v > 1 ? 1 : v)); }
        private static int MeasureDays(Settings s) { return Math.Max(7, Math.Min(91, s.DsFarmMeasureDays)); }
        private static int DrawDays(Settings s) { return Math.Max(0, Math.Min(120, s.DsMerchantDrawDays)); }
        private static int ForgiveWeeks(Settings s) { return Math.Max(0, Math.Min(156, s.DsFarmForgiveWeeks)); }

        /// <summary>Rampa przejscia F3 (DsRampDays od doby ustalenia firm): 0..1; poza F3 - 0. Recenzja 17.10 (wymog Jeffa "przejscie z rampa 28 dob"): stare pobory miast,
        /// zamkow i ludu x (1 - rampa) - zawor, P1, podatek od skarbca ludu, danina miast, W7 z ludu; nowe x rampa - firmy, wyplata kupcom, pomoc od sprzedazy, raty dziesiatej,
        /// clo wojenne, pomoc lennikow, s 184 (liniowo), g udzialu korony przy wozie, zbytki ludu. W dobie ustalenia rampa 0 - stare zasady co do monety.</summary>
        internal static double FolkRamp
        {
            get
            {
                var s = Settings.Current;
                if (s == null || !FarmsLive) return 0;
                int days = Math.Max(0, Math.Min(364, s.DsRampDays));
                if (days <= 0) return 1;
                return Unit((Today - _fixDay) / (double)days);
            }
        }

        /// <summary>Czesc NOWYCH zasad F3 w przejsciu (= FolkRamp; 0 poza firmami).</summary>
        internal static double NewShare { get { return FolkRamp; } }

        /// <summary>Czesc STARYCH poborow miast, zamkow i ludu w przejsciu F3 (1 poza firmami, 0 po rampie).</summary>
        internal static double OldShare { get { return FarmsLive ? 1.0 - FolkRamp : 1.0; } }

        /// <summary>Doby do konca przejscia F3 (0 po rampie albo poza firmami) - s 184 idzie liniowo do s z nadania.</summary>
        internal static double RampDaysLeft
        {
            get
            {
                var s = Settings.Current;
                if (s == null || !FarmsLive) return 0;
                int days = Math.Max(0, Math.Min(364, s.DsRampDays));
                return Math.Max(0, days - (Today - _fixDay));
            }
        }

        private static uint Fnv(string id)
        {
            uint h = 2166136261u;
            if (id != null) for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
            return h;
        }

        /// <summary>Dzien raty osady (co 7 dob, przesuniecie z FNV id - raty rozlozone na tydzien).</summary>
        internal static bool PayDay(Settlement st)
        {
            if (st == null || st.StringId == null) return false;
            return (Today + (int)(Fnv(st.StringId) % 7u)) % 7 == 0;
        }

        private static string OwnerKey(Settlement st)
        {
            var c = st != null ? st.OwnerClan : null;
            if (c == null) return "-";
            var k = c.Kingdom;
            return k != null && !k.IsEliminated && k.StringId != null ? k.StringId : "c:" + (c.StringId ?? "?");
        }

        private static Farm FarmOf(Settlement st, bool create)
        {
            if (st == null || st.StringId == null) return null;
            Farm f;
            if (_farm.TryGetValue(st.StringId, out f)) { if (f.St == null) f.St = st; return f; }
            if (!create || BadId(st.StringId)) return null;
            f = new Farm { St = st };
            _farm[st.StringId] = f;
            return f;
        }

        /// <summary>Zwolnienie z raty: 1 - oblezenie, 2 - zmiana krolestwa wlasciciela w ostatnich 28 dobach (zdobycie, lup); 0 - bez zwolnienia.</summary>
        internal static int RemitOf(Settlement st)
        {
            try
            {
                if (st == null) return 0;
                if (st.IsUnderSiege) return 1;
                Farm f = FarmOf(st, false);
                if (f != null && f.CapDay >= 0 && Today - f.CapDay < CaptureDays) return 2;
            }
            catch (Exception e) { Stumble("RemitOf", e); }
            return 0;
        }

        private static void Push(Farm f, double net)
        {
            if (double.IsNaN(net) || double.IsInfinity(net)) return;
            f.Net[f.NH] = net; f.NH = (f.NH + 1) % NetDays; if (f.NF < NetDays) f.NF++;
        }

        private static double NetAvg(Farm f, int min)
        {
            if (f == null || f.NF < min) return double.NaN;
            double t = 0; for (int i = 0; i < f.NF; i++) t += f.Net[i];
            return t / f.NF;
        }

        // ============================================================ poczatek doby (ArmouryBehavior: po TownPurse.BeforeRents, przed PopulationLaw.Daily)
        /// <summary>Stan F3: wylaczone - stare zasady (i porzadki); start okna pomiaru; ustalenie firm po DsFarmMeasureDays; dni kwartalu (zaleglosci), odnowy, zmiany wlascicieli.</summary>
        internal static void DayStart()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                int today = Today;
                if (!On)
                {
                    if (_dayOn >= 0 || _fixDay >= 0)
                    {
                        string why = !Key ? "klucz Ds Towns" : !TownFolk.Live ? "kiesa ludu nieczynna" : !CrownIncome.On ? "165 Crown Current Income nieczynne" : "zawor kas miast nieczynny";
                        _events.Add("WYLACZONE (" + why + ") - stare zasady od tej doby (zawor miast i zamkow, P1, podatek od skarbca ludu, danina miast, W7 z ludu); firmy i zaleglosci "
                                    + "zostaja w zapisie bez skutku (ponowne wlaczenie = nowe okno pomiaru), dziesiata od ruchomosci niezebrana przepada, zaliczki mieszczan -> zwykly dlug korona2");
                        try { TownFolk.AntAllToNormal(); } catch (Exception e) { Stumble("DayStart(zaliczki)", e); }
                        try { DsSeason.Off(); } catch (Exception e) { Stumble("DayStart(nadania)", e); }
                        foreach (var f in _farm.Values) { f.ML = f.MC = f.MLf = f.SW = f.LW = 0; f.MovLeft = 0; f.MovPer = 0; f.MovCur = 0; f.MovK = null; f.MovSerial = -1; f.AidArr = 0; f.AidK = null; f.AidCarry = 0; }
                        _aidPend.Clear();
                    }
                    _dayOn = -1; _fixDay = -1; _mDays = 0; SwitchDue = false; _waitLogged = false;
                    return;
                }
                Owners(today);
                if (_dayOn < 0)
                {
                    // recenzja 17.10 (projekt 11: "okno F3 ma byc PO co najmniej 28 dobach F1"): przy F1 w rampie okno czeka - firmy z przejsciowych przeplywow bylyby zle
                    if (DsVillage.Live && DsVillage.Ramp < 1)
                    {
                        if (!_waitLogged) { _waitLogged = true; _events.Add("okno pomiaru firm CZEKA na koniec rampy danin wsi (F1, rampa " + DsVillage.Ramp.ToString("0.000", Inv) + ") - firmy maja zawierac pieniadz wsi, ktory wrocil przez miasta (projekt 11); stare zasady"); }
                        return;
                    }
                    _waitLogged = false;
                    _dayOn = today; _mDays = 0; _fixDay = -1;
                    foreach (var f in _farm.Values) { f.ML = f.MC = f.MLf = f.SW = f.LW = 0; }
                    _events.Add("start okna pomiaru firm - " + MeasureDays(s) + " dob starych zasad (zawor miast i zamkow, czesc pana podatku od skarbca ludu, W7 z ludu), potem firmy stale i nadania wojenne");
                }
                if (_fixDay < 0)
                {
                    if (_mDays >= MeasureDays(s)) { Fix(today, s); _fixDay = today; SwitchDue = true; }
                    else _mDays++;
                }
                else
                {
                    if (today % Quarter == 0) Terms();
                    Renew(today, s);
                    SurplusReview(today, s);                      // f5-lud (Ks Farm Follows Surplus): firma miasta z KS w gore o przyrost kasy ponad 1.5 x zapas kupcow
                }
            }
            catch (Exception e) { Stumble("DayStart", e); }
        }

        private static void Owners(int today)
        {
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsTown || st.IsCastle) || st.Town == null) continue;
                    var f = FarmOf(st, true);
                    if (f == null) continue;
                    string o = OwnerKey(st);
                    if (f.OwnerK != null && f.OwnerK != o) f.CapDay = today;
                    f.OwnerK = o;
                }
                catch (Exception e) { Stumble("Owners", e); }
            }
        }

        private static void Fix(int today, Settings s)
        {
            int days = Math.Max(1, _mDays);
            double sF = 0, sN = 0, tL = 0, tC = 0, cL = 0, cC = 0, tLf = 0, cLf = 0, cut = 0;
            int nT = 0, nC = 0, zT = 0, zC = 0, nCut = 0;
            double rate1 = Math.Min(Math.Max(0, Math.Min(0.3, (double)s.DsAidesMax)), Math.Max(0, Math.Min(0.1, (double)s.DsAidesRate)));   // pomoc od sprzedazy przy g = 1
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsTown || st.IsCastle) || st.Town == null) continue;
                    var f = FarmOf(st, true);
                    if (f == null) continue;
                    f.FL = YearDays * Math.Max(0, f.ML) / days; f.FC = YearDays * Math.Max(0, f.MC) / days; f.FLf = YearDays * Math.Max(0, f.MLf) / days;
                    // recenzja 17.10: kasa placi firme (ok. 96% doplywu netto) i pomoc od sprzedazy - przy g = 1 razem tyle, ile w oknie zawor i danina miasta 1%/d:
                    // pomoc (g = 1) ponad dawna danine tej osady zdjeta z firmy korony (najwyzej do 0); w ciezkiej wojnie (g > 1) kasa placi wiecej - nadanie
                    double aidPlus = Math.Max(0, rate1 * Math.Max(0, f.SW) / days - Math.Max(0, f.LW) / days) * YearDays;
                    f.AidCut = Math.Min(f.FC, aidPlus); f.FC -= f.AidCut;
                    if (f.AidCut > 0) { cut += f.AidCut; nCut++; }
                    f.ML = f.MC = f.MLf = f.SW = f.LW = 0; f.Fixed = today; f.ATerms = 0;
                    double net = NetAvg(f, NetMinDays), dayF = (f.FL + f.FC) / YearDays;
                    if (net > 0 && dayF > 0) { f.Share = dayF / net; sF += dayF; sN += net; } else f.Share = double.NaN;
                    if (st.IsTown) { nT++; tL += f.FL; tC += f.FC; tLf += f.FLf; if (dayF <= 0) zT++; }
                    else { nC++; cL += f.FL; cC += f.FC; cLf += f.FLf; if (dayF <= 0) zC++; }
                }
                catch (Exception e) { Stumble("Fix(osada)", e); }
            }
            _worldShare = sN > 0 ? sF / sN : double.NaN;
            _events.Add("FIRMY USTALONE po " + days + " dobach pomiaru: " + nT + " miast (z kasy panom " + N0(tL) + " zl/rok, koronie " + N0(tC) + " zl/rok; z oszczednosci ludu panom " + N0(tLf)
                        + "; bez firmy " + zT + "), " + nC + " zamkow (z kasy panom " + N0(cL) + ", koronie " + N0(cC) + "; z oszczednosci podzamcza panom " + N0(cLf) + "; bez firmy " + zC
                        + "), rata 1/" + Weeks + " co 7 dob; firma korony zmniejszona o pomoc od sprzedazy przy g = 1 ponad dawna danine miast " + N0(cut) + " zl/rok (" + nCut + " osad)"
                        + "; udzial firmy w doplywie netto swiata " + (double.IsNaN(_worldShare) ? "-" : _worldShare.ToString("0.000", Inv))
                        + "; przejscie " + Math.Max(0, Math.Min(364, s.DsRampDays)) + " dob: zawor, P1, podatek od skarbca ludu, danina miast i W7 z ludu x (1 - rampa), nowe x rampa; nadania wojenne - sesja dzis");
        }

        private static void Terms()
        {
            foreach (var f in _farm.Values)
            {
                double inst = (f.FL + f.FC + f.FLf) / Weeks;
                if (f.AL + f.AC + f.ALf >= Math.Max(1.0, inst)) f.ATerms++; else f.ATerms = 0;
            }
        }

        /// <summary>Doba przegladu firm (Ds Farm Review Days) - 0: stara odnowa co Ds Farm Renew Days (kod bez zmian).</summary>
        internal static int ReviewDays(Settings s) { return s == null ? 0 : Math.Max(0, Math.Min(364, s.DsFarmReviewDays)); }

        private static void Renew(int today, Settings s)
        {
            int qd = ReviewDays(s);
            if (qd > 0) { Review(today, s, qd); return; }      // f4 P5: przeglad kwartalny (nizej); 0 - stara odnowa jak w f5fd0ae3
            int rd = Math.Max(0, Math.Min(728, s.DsFarmRenewDays));
            if (rd <= 0) return;
            double m = Math.Max(0, Math.Min(0.5, (double)s.DsFarmRenewMax));
            foreach (var f in _farm.Values)
            {
                try
                {
                    // pamiec doplywu netto jest sesyjna (bez zapisu): recenzja 17.10 - odnowa juz od NetMinDays (7) dob pomiaru, nie dopiero od pelnych 28 (krotkie sesje
                    // Jeffa nigdy by jej nie doczekaly); ograniczenie +-DsFarmRenewMax zostaje
                    if (f.Fixed < 0 || today - f.Fixed < rd || f.NF < NetMinDays) continue;
                    double F = f.FL + f.FC;
                    f.Fixed = today; _renewN++;
                    if (F <= 0) continue;
                    double share = !double.IsNaN(f.Share) ? f.Share : _worldShare;
                    if (double.IsNaN(share) || share <= 0) continue;
                    double target = Math.Max(0, NetAvg(f, NetMinDays)) * share * YearDays;
                    double nf = Math.Max(F * (1 - m), Math.Min(F * (1 + m), target));
                    double r = nf / F;
                    if (r < 1 - 1e-9 && SurplusHolds(f, s)) { r = 1.0; KsFolkIncome.NoteNoCut(); }   // f5-lud: bez obnizki przy nadwyzce kasy (Ks Farm Follows Surplus)
                    f.FL *= r; f.FC *= r; f.FLf *= r;
                    if (r > 1 + 1e-9) _renewUp++; else if (r < 1 - 1e-9) _renewDown++;
                }
                catch (Exception e) { Stumble("Renew", e); }
            }
        }

        /// <summary>
        /// f4 P5 (koncowy przeglad ekonomii 18.10, uwaga 2: firmy stale na ok. 100% zysku kupcow - biedne miasta w zaleglosciach 1,69 mln po 100 dobach, bogate odkladaja):
        /// PRZEGLAD FIRM co Ds Farm Review Days (kwartal 91) zamiast odnowy co 364. Ta sama regula co odnowa (Renew): firma w strone udzialu z dnia ustalenia (Share, bez
        /// udzialu - udzial swiata) w doplywie netto kupcow ostatnich 28 dob (pamiec sesji, najmniej 7 dob), najwyzej +-Ds Farm Renew Max - poziom firmy bez zmian, firma
        /// idzie za zyskiem (bogate wiecej, biedne mniej). Dwie zmiany wobec odnowy: (1) osada z zaleglosciami (>= jednej raty) nie dostaje podwyzki (podwyzka przy
        /// zaleglosci tylko powiekszalaby zaleglosc - oscylacja); (2) osada z zaleglosciami przez Ds Farm Arrears Review Terms dni kwartalu z rzedu (ATerms, liczone w Terms
        /// w dzien kwartalu) i z dniem kwartalu po ostatnim przegladzie dostaje przeglad WYMUSZONY w dol o pelne Ds Farm Renew Max, bez wzgledu na doplyw netto: zaleglosc
        /// trwa przez dwa dni kwartalu tylko wtedy, gdy kasa stoi na podlodze (rata placi najpierw zaleglosc i rate z calego zlota ponad podloge - Pay; wyjatki: oblezenie
        /// i zmiana krolestwa do 28 dob, osada bez zywego pana - w logach 0), wiec firma jest ponad sily miasta, nawet gdy doplyw netto (cel) tego nie widzi (wyplywy kasy). Recenzja f4: (a) wymuszone obniza ZAWSZE (dotad cel >= firma dawal "bez zmiany", a linia liczyla
        /// to jako "w dol od razu"); licznik tylko przy r &lt; 1; (b) najwyzej jedna zmiana na kwartal: wymuszone przed wlasna doba przegladu tylko po >= kwartale od
        /// ostatniego przegladu (przy Ds Farm Review Days 91 zawsze we wlasna dobe; dotad przeglad we wlasna dobe i drugi w najblizszy dzien kwartalu dawaly do -27,75%
        /// w 1..90 dob). Firma pana, korony i czesc z oszczednosci ludu w tym samym stosunku (jak Renew). Zloto sie nie rusza - zmienia sie tylko nalezna rata (zamknieta
        /// ekonomia: platnik i odbiorca bez zmian, kontrola linii "platnicy - odbiorcy" jak dotad). ATerms bez zerowania (zajecie DsFarmDistraintTerms liczy dalej jak dotad).
        /// Kazda osada we wlasnym try, potkniecia liczone. Recenzja calosci 18.10: doplyw netto (TownPurse.NetOf) liczy jedzenie miasta zaplacone przez pana (KsLordFood -
        /// rachunki KS-H, wczesna splata, doplata) jak wydatek kasy - pomoc pana nie podnosi firmy (inaczej stale przelewanie pan -> korona przez miasto).
        /// </summary>
        private static void Review(int today, Settings s, int qd)
        {
            double m = Math.Max(0, Math.Min(0.5, (double)s.DsFarmRenewMax));
            int at = Math.Max(0, Math.Min(8, s.DsFarmArrearsReviewTerms));
            int lastTerms = today - (today % Quarter);              // ostatni dzien kwartalu (Terms) <= dzis
            foreach (var f in _farm.Values)
            {
                try
                {
                    if (f.Fixed < 0 || f.NF < NetMinDays) continue;
                    double inst = (f.FL + f.FC + f.FLf) / Weeks;
                    bool arrears = f.AL + f.AC + f.ALf >= Math.Max(1.0, inst);
                    int gap = today - f.Fixed;                          // dob od ostatniego przegladu (albo ustalenia firmy)
                    bool due = gap >= qd;
                    // zaleglosci przez at dni kwartalu z rzedu, nadal >= raty, i dzien kwartalu po ostatnim przegladzie (ATerms zmienione od tamtej doby)
                    bool forced = at > 0 && arrears && f.ATerms >= at && f.Fixed < lastTerms;
                    // przed wlasna doba (tylko przy Ds Farm Review Days > kwartal): najwyzej jedna zmiana na kwartal
                    bool early = !due && forced && gap >= Quarter;
                    if (!due && !early) continue;
                    double F = f.FL + f.FC, all0 = F + f.FLf;
                    f.Fixed = today; _renewN++;
                    if (gap < _revMinGap) _revMinGap = gap;
                    if (F <= 0) continue;
                    double r;
                    if (forced) r = 1 - m;                              // wymuszone w dol o pelny limit - bez wzgledu na cel
                    else
                    {
                        double share = !double.IsNaN(f.Share) ? f.Share : _worldShare;
                        if (double.IsNaN(share) || share <= 0) { _revSame++; continue; }
                        double target = Math.Max(0, NetAvg(f, NetMinDays)) * share * YearDays;
                        double hi = arrears ? F : F * (1 + m);             // przy zaleglosci bez podwyzki
                        if (arrears && target > F + 1e-9) _revHeld++;
                        double nf = Math.Max(F * (1 - m), Math.Min(hi, target));
                        r = nf / F;
                        if (r < 1 - 1e-9 && SurplusHolds(f, s)) { r = 1.0; KsFolkIncome.NoteNoCut(); }   // f5-lud: bez obnizki przy nadwyzce kasy (Ks Farm Follows Surplus); wymuszony - jak dotad
                    }
                    f.FL *= r; f.FC *= r; f.FLf *= r;
                    double d = all0 * (r - 1);
                    if (r > 1 + 1e-9) { _renewUp++; _revUpAmt += d; }
                    else if (r < 1 - 1e-9) { _renewDown++; _revDownAmt -= d; if (forced) { _revArrN++; if (early) _revEarly++; } }
                    else _revSame++;
                }
                catch (Exception e) { Stumble("Review", e); }
            }
        }

        // ============================================================ f5-lud: firma miasta z KS wedlug nadwyzki kasy (Ks Farm Follows Surplus)
        /// <summary>f5-lud (Ks Farm Follows Surplus): kasa miasta z KS ponad 1.5 x zapas kupcow (prog wyplaty kupcom M6) - kupcy maja nadwyzke; klucz wylaczony, zamek, miasto
        /// bez KS - false (przeglad jak dotad).</summary>
        private static bool SurplusHolds(Farm f, Settings s)
        {
            try
            {
                if (s == null || !s.KsFarmFollowsSurplus || f == null || f.St == null || !f.St.IsTown || f.St.Town == null) return false;
                var t = f.St.Town;
                return StartCalibration.KsTownApplied(t) && t.Gold > OverReserve * TownPurse.Reserve(t);
            }
            catch (Exception e) { Stumble("SurplusHolds", e); return false; }
        }

        /// <summary>
        /// f5-lud (noc 17/18.10, Ks Farm Follows Surplus; model tools/ekonomia-symulatory-2026-10-17/f5/simlud.py S9): dochod ludu miast z KS idzie za koszykiem klas
        /// (KsFolkIncome) - czego lud nie przyjal, zostaje w kasie miasta (kupcy). Przeglad kwartalny (Review) liczy firme z doplywu netto kupcow, w ktorym wyplata kupcom M6
        /// jest poborem - nie widzi wiec M6 zatrzymanej w kasie i podnosi firme najwyzej o 15% na kwartal (model: kasy +20 mln w pol roku - nowy akumulator). Tu: miasto
        /// z KS, ktorego kasa stoi ponad 1.5 x zapas kupcow, ma co Ks Farm Surplus Review Days przeglad W GORE o przyrost kasy na dobe od poprzedniego przegladu (x 364 =
        /// zl/rok), najwyzej Ks Farm Surplus Review Max firmy; firma pana i korony w tym samym stosunku (czesc z oszczednosci ludu bez zmiany). Przyrost liczony od ostatniej
        /// podwyzki (bez liczenia drugi raz tych samych dob); kasa nie ponad progiem, oblezenie, zmiana krolestwa, zaleglosci - pomiar od nowa. Pamiec sesyjna (po wczytaniu
        /// pierwszy przeglad po Ks Farm Surplus Review Days). Zloto sie nie rusza - zmienia sie nalezna rata (kontrola linii "platnicy - odbiorcy" jak dotad). Kazda osada
        /// we wlasnym try, potkniecia liczone.
        /// </summary>
        private static void SurplusReview(int today, Settings s)
        {
            if (s == null || !s.KsFarmFollowsSurplus) return;
            int days = Math.Max(7, Math.Min(91, s.KsFarmSurplusReviewDays));
            double mx = Math.Max(0.0, Math.Min(1.0, (double)s.KsFarmSurplusReviewMax));
            foreach (var f in _farm.Values)
            {
                try
                {
                    var st = f.St;
                    if (st == null || !st.IsTown || st.Town == null || f.Fixed < 0) continue;
                    var t = st.Town;
                    if (!StartCalibration.KsTownApplied(t)) { f.SG0 = long.MinValue; f.SGDay = -1; continue; }
                    long g = t.Gold;
                    double inst = (f.FL + f.FC + f.FLf) / Weeks;
                    bool arrears = f.AL + f.AC + f.ALf >= Math.Max(1.0, inst);
                    if (g <= OverReserve * TownPurse.Reserve(t) || RemitOf(st) != 0 || arrears) { f.SG0 = long.MinValue; f.SGDay = -1; continue; }
                    if (f.SG0 == long.MinValue || f.SGDay < 0) { f.SG0 = g; f.SGDay = today; continue; }
                    int span = today - f.SGDay;
                    if (span < days) continue;
                    double grow = (g - f.SG0) / (double)span;
                    f.SG0 = g; f.SGDay = today;                                  // przyrost od tej doby (bez liczenia drugi raz)
                    double F = f.FL + f.FC;
                    if (!(grow > 0) || !(F > 0)) continue;
                    double add = Math.Min(grow * YearDays, mx * F);
                    if (!(add > 0)) continue;
                    double r = (F + add) / F;
                    f.FL *= r; f.FC *= r;
                    KsFolkIncome.NoteRaise(add);
                }
                catch (Exception e) { Stumble("SurplusReview", e); }
            }
        }

        // ============================================================ okno pomiaru (stare zasady)
        /// <summary>PopulationLaw.ValveTowns (okno pomiaru, po zaworze miasta): czesc pana (po dekrecie) i korony z zaworu; probka doplywu netto.</summary>
        internal static void TownWindow(Settlement st, int lord, int crown)
        {
            try
            {
                if (!InWindow || st == null || st.Town == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                SampleTown(st, st.Town, f, TownPurse.Reserve(st.Town));
                if (lord > 0) f.ML += lord;
                if (crown > 0) f.MC += crown;
            }
            catch (Exception e) { Stumble("TownWindow", e); }
        }

        /// <summary>CastlePurse.Daily (okno pomiaru, po zaworze zamku): czesc pana i korony; pobory do probki doplywu.</summary>
        internal static void CastleWindow(Settlement st, int lord, int crown, int suburb)
        {
            try
            {
                if (!InWindow || st == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                if (lord > 0) f.ML += lord;
                if (crown > 0) f.MC += crown;
                f.PaidWin += Math.Max(0, lord) + Math.Max(0, crown) + Math.Max(0, suburb);
            }
            catch (Exception e) { Stumble("CastleWindow", e); }
        }

        /// <summary>TownFolk.PayHoard (okno pomiaru): czesc pana podatku od skarbca ludu tej osady - do czesci firmy z oszczednosci ludu (MLf, recenzja 17.10).</summary>
        internal static void MeasureHoard(Settlement st, long lord)
        {
            try { if (lord > 0 && InWindow) { var f = FarmOf(st, true); if (f != null) f.MLf += lord; } } catch (Exception e) { Stumble("MeasureHoard", e); }
        }

        /// <summary>KingdomTreasury.LvCollect (okno pomiaru): W7 z ludu tej osady (do pana) - do czesci firmy z oszczednosci ludu (MLf).</summary>
        internal static void MeasureW7(Settlement st, long took)
        {
            try { if (took > 0 && InWindow && st != null && (st.IsTown || st.IsCastle)) { var f = FarmOf(st, true); if (f != null) f.MLf += took; } } catch (Exception e) { Stumble("MeasureW7", e); }
        }

        /// <summary>KingdomTreasury.Levies (okno pomiaru): danina wojenna miasta 1%/d z kasy - miara do zdjecia pomocy od sprzedazy ponad nia z firmy korony (Fix).</summary>
        internal static void MeasureLevy(Settlement st, long x)
        {
            try { if (x > 0 && InWindow) { var f = FarmOf(st, true); if (f != null) f.LW += x; } } catch (Exception e) { Stumble("MeasureLevy", e); }
        }

        /// <summary>CastlePurse.Daily (przejscie F3): pobory zaworu zamku x (1 - rampa) - do probki doplywu netto (jak CastleWindow w oknie).</summary>
        internal static void CastleValvePaid(Settlement st, long x)
        {
            try { if (x > 0 && FarmsLive) { var f = FarmOf(st, true); if (f != null) f.PaidWin += x; } } catch (Exception e) { Stumble("CastleValvePaid", e); }
        }

        /// <summary>SoldierPay (Z8, przez CastlePurse.FarmDayEq): rownowaznik dnia firmy pana z kasy zamku (F_L / 364 x rampa przejscia); 0 bez firmy, oblezenie, zdobycie.</summary>
        internal static double LordFarmPerDay(Settlement st)
        {
            try
            {
                if (!FarmsLive || st == null) return 0;
                var f = FarmOf(st, false);
                if (f == null || !(f.FL > 0) || RemitOf(st) != 0) return 0;
                return f.FL / YearDays * NewShare;
            }
            catch (Exception e) { Stumble("LordFarmPerDay", e); return 0; }
        }

        /// <summary>ds-akumulator (DsHoardDue, TownFolk.PayHoard): czesc firmy pana z oszczednosci ludu na dobe (F_Lf / 364 x rampa przejscia) - ta warstwa oszczednosci jest
        /// juz oplacona firma (DsFolkRate w dzien raty), wiec danina od skarbca ludu liczy ja jako zaplacona (nic dwa razy). 0 - bez firm, klucz DsFarmFolkFromSavings
        /// wylaczony (firme placi kasa), osada bez firmy, oblezenie albo zdobycie (rata darowana). Nie rzuca.</summary>
        internal static double FolkFarmPerDay(Settlement st)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !FarmsLive || st == null || !s.DsFarmFolkFromSavings) return 0;
                var f = FarmOf(st, false);
                if (f == null || !(f.FLf > 0) || RemitOf(st) != 0) return 0;
                return f.FLf / YearDays * NewShare;
            }
            catch (Exception e) { Stumble("FolkFarmPerDay", e); return 0; }
        }

        private static void SampleTown(Settlement st, Town t, Farm f, int reserve)
        {
            long net;
            if (TownPurse.NetOf(t, out net)) Push(f, net);
            DsSeason.NoteMovBase(st, Math.Max(0, t.Gold - OverReserve * reserve));
        }

        /// <summary>CastlePurse.Daily (F3 czynne - okno i firmy), przed zaworem / firma zamku: probka doplywu netto (kasa teraz - kasa przy poprzedniej probce + nasze pobory od niej).</summary>
        internal static void CastleSample(Settlement st, Town t, int reserve)
        {
            try
            {
                if (!On || st == null || t == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                long g = t.Gold;
                if (f.G0 != long.MinValue) Push(f, g - f.G0 + f.PaidWin);
                f.G0 = g; f.PaidWin = 0;
                DsSeason.NoteMovBase(st, Math.Max(0, g - OverReserve * reserve));
            }
            catch (Exception e) { Stumble("CastleSample", e); }
        }

        // ============================================================ przelewy (mierzone u platnika i odbiorcy)
        private static long ToLord(Settlement st, Town t, Hero lord, long x)
        {
            if (x <= 0 || lord == null || t == null) return 0;
            int had = t.Gold;
            int ix = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, had)));
            if (ix <= 0) return 0;
            int h0 = lord.Gold;
            GiveGoldAction.ApplyForSettlementToCharacter(st, lord, ix, true);   // kasa -> pan (ta sama akcja co zawor i renty; kwota nigdy ponad kase)
            long o = Math.Max(0, (long)had - t.Gold), i = (long)lord.Gold - h0;
            _payOut += o; _payIn += i;
            return o;
        }

        private static long ToCrown(Town t, Kingdom k, long x)
        {
            if (x <= 0 || t == null || k == null) return 0;
            int had = t.Gold;
            int ix = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, had)));
            if (ix <= 0) return 0;
            t.ChangeGold(-ix);
            long o = Math.Max(0, (long)had - t.Gold);
            if (o <= 0) return 0;
            int w0 = k.KingdomBudgetWallet;
            try { k.KingdomBudgetWallet += (int)o; }
            catch (Exception e) { t.ChangeGold((int)o); Stumble("ToCrown(skarbiec)", e); return 0; }   // nic w nicosc: zloto wraca do kasy
            _payOut += o; _payIn += (long)k.KingdomBudgetWallet - w0;
            return o;
        }

        // ============================================================ firmy: miasto (PopulationLaw.ValveTowns) i zamek (CastlePurse.Daily)
        /// <summary>
        /// PopulationLaw.ValveTowns przy ustalonych firmach (w przejsciu obok zaworu x (1 - rampa)): probka doplywu, w dzien raty zaleglosc pomocy M7, firma M2/M3 (pan /
        /// korona) x rampa, czesc pana z oszczednosci ludu, dziesiata od ruchomosci kasy M9, zajecie i darowanie zaleglosci; co dobe wyplata kupcom M6 x rampa. lordPay - kasa ->
        /// pan (wolajacy: RentToday, DayTownRent), crownFarm - kasa -> skarbiec w TownPurse.CrownToday (wplyw dnia, "firmy korony"), crownMov - kasa -> skarbiec dziesiata
        /// (wplyw dnia przez DsSeason), crownAid - kasa -> skarbiec zaleglosc pomocy od sprzedazy (recenzja 17.10: wplyw dnia przez LeviesToday w AidesDay, nie jako firma -
        /// prognoza nadania liczyla ja dwa razy). Nie rzuca.
        /// </summary>
        internal static bool TownDay(Settlement st, out int lordPay, out int crownFarm, out int crownMov, out int crownAid, out long spare)
        {
            lordPay = crownFarm = crownMov = crownAid = 0; spare = 0;
            try
            {
                var t = st != null ? st.Town : null;
                var s = Settings.Current;
                if (t == null || s == null) return false;
                var f = FarmOf(st, true);
                if (f == null) return false;
                int reserve = TownPurse.Reserve(t);
                SampleTown(st, t, f, reserve);
                spare = (long)t.Gold - reserve;
                Kingdom k;
                Pay(st, t, f, reserve, s, true, out lordPay, out crownFarm, out crownMov, out crownAid, out k);
                if (lordPay > 0 || crownFarm > 0) TownPurse.DsNoteFarm(st, lordPay, crownFarm, k);
                if (crownMov > 0) TownPurse.NoteTake(st, crownMov);
                if (crownAid > 0) { TownPurse.NoteTake(st, crownAid); try { TownFolk.NoteTake(st, crownAid, TownFolk.TLevy); } catch { } }
                Draw(st, t, f, reserve, s, true);
                return true;
            }
            catch (Exception e) { Stumble("TownDay", e); return lordPay + crownFarm + crownMov + crownAid > 0; }
        }

        /// <summary>CastlePurse.Daily przy ustalonych firmach (w przejsciu obok zaworu x (1 - rampa)): to samo co miasto (bez zajecia - zamek nie ma gminy); suburb - M6 do kiesy
        /// podzamcza. Wolajacy: RentToday / LordDuesToday (pan), CastlePurse.CrownToday (crownFarm), ksiega MCastleCrown (crownMov + crownAid) i MFolkValve (suburb).</summary>
        internal static void CastleDay(Settlement st, int reserve, out int lordPay, out int crownFarm, out int crownMov, out int crownAid, out int suburb, out Kingdom k)
        {
            lordPay = crownFarm = crownMov = crownAid = suburb = 0; k = null;
            try
            {
                var t = st != null ? st.Town : null;
                var s = Settings.Current;
                if (t == null || s == null) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                Pay(st, t, f, reserve, s, false, out lordPay, out crownFarm, out crownMov, out crownAid, out k);
                f.PaidWin += lordPay + crownFarm + crownMov + crownAid;
                suburb = (int)Math.Min(int.MaxValue, Draw(st, t, f, reserve, s, false));
            }
            catch (Exception e) { Stumble("CastleDay", e); }
        }

        /// <summary>Podloga kasy dla firm, pomocy i dziesiatej: DsFarmFloorShare zapasu kupcow; przy regulatorze kas w trybie 1 (doplata do zapasu z niczego) - caly zapas
        /// (recenzja 17.10: inaczej gra dosypywalaby zloto do kasy oproznionej przez korone).</summary>
        private static double FloorOf(Settings s, int reserve)
        {
            double share = s.TownPurseRegulator == 1 ? 1.0 : Unit(s.DsFarmFloorShare);
            return share * Math.Max(0, reserve);
        }

        private static void Pay(Settlement st, Town t, Farm f, int reserve, Settings s, bool town, out int lordPay, out int crownFarm, out int crownMov, out int crownAid, out Kingdom kOut)
        {
            lordPay = crownFarm = crownMov = crownAid = 0; kOut = null;
            var clan = st.OwnerClan;
            var lord = clan != null ? clan.Leader : null;
            var k = clan != null ? clan.Kingdom : null;
            if (k != null && k.IsEliminated) k = null;
            kOut = k;
            if (!PayDay(st)) return;
            double rn = NewShare;                                   // przejscie: raty firm x rampa (reszta - stary zawor u wolajacego)
            double iL = f.FL / Weeks * rn, iC = f.FC / Weeks * rn, iLf = f.FLf / Weeks * rn;
            if (k == null) { iL += iC; iC = 0; if (f.AC > 0) { f.AL += f.AC; f.AC = 0; } }   // rod bez krolestwa - calosc panu (jak zawor)
            bool folkPays = s.DsFarmFolkFromSavings;
            if (!folkPays) { iL += iLf; iLf = 0; if (f.ALf > 0) { f.AL += f.ALf; f.ALf = 0; } }   // klucz wylaczony - cala firma pana z kasy
            int serial;
            double g = DsSeason.GrantNow(k, out serial);
            bool assess = k != null && g > 0 && serial > 0 && f.MovSerial != serial;
            if (iL + iC + iLf <= 0 && f.AL + f.AC + f.ALf < 1 && f.AidArr < 1 && f.MovLeft < 1 && !assess) return;   // osada bez firmy i bez naleznosci
            _nDue++; if (town) _nDueT++;
            _dueL += iL; _dueC += iC; _dueLf += iLf;
            int why = RemitOf(st);
            if (why != 0)
            {
                _remit += iL + iC + iLf; if (why == 1) _remitSiege++; else _remitCap++;
                if (f.MovLeft >= 1) { double m = Math.Min(f.MovPer, f.MovLeft); f.MovLeft -= m; _movRemit += m; }
                return;
            }
            double before = f.AL + f.AC + f.ALf;
            if (lord == null || !lord.IsAlive)
            {
                f.AL += iL; f.AC += iC; f.ALf += iLf; _noLord++; _noLordAmt += iL + iC + iLf;   // bez zywego pana - rata czeka (jak zawor)
                _arrNew += iL + iC + iLf;
                return;
            }
            double floor = FloorOf(s, reserve);
            long avail = Math.Max(0L, (long)Math.Floor(t.Gold - floor));
            // 1. zaleglosc pomocy od sprzedazy (M7) - przed firma; recenzja 17.10: do skarbca jako wplyw dnia przez LeviesToday (AidesDay), nie jako "firmy korony"
            if (f.AidArr >= 1)
            {
                if (k == null || f.AidK != k.StringId) { _aidLost += f.AidArr; f.AidArr = 0; f.AidK = null; }
                else if (avail > 0)
                {
                    long got = ToCrown(t, k, (long)Math.Floor(Math.Min(f.AidArr, avail)));
                    if (got > 0)
                    {
                        f.AidArr -= got; avail -= got; crownAid += (int)got; _aidArrPaid += got; DsSeason.NoteAides(k, got);
                        long p0; _aidPend.TryGetValue(k.StringId, out p0); _aidPend[k.StringId] = p0 + got;
                    }
                    if (f.AidArr < 1) { f.AidArr = 0; f.AidK = null; }
                }
            }
            // 2. firma z kasy: zaleglosc i rata (pan i korona w stosunku naleznosci)
            double wantL = f.AL + iL, wantC = f.AC + iC, tot = wantL + wantC;
            if (tot >= 1)
            {
                long pay = (long)Math.Floor(Math.Min(tot, avail));
                long pL = pay > 0 ? (long)Math.Floor(pay * wantL / tot) : 0, pC = pay - pL;
                long gL = pL > 0 ? ToLord(st, t, lord, pL) : 0;
                long gC = pC > 0 && k != null ? ToCrown(t, k, pC) : 0;
                f.AL = Math.Max(0, wantL - gL); f.AC = Math.Max(0, wantC - gC);
                avail -= gL + gC;
                lordPay += (int)gL; crownFarm += (int)gC;
                _paidL += gL; _paidC += gC;
            }
            else { f.AL = wantL; f.AC = wantC; }
            // 2b. czesc pana z oszczednosci ludu (recenzja 17.10, "scot and lot"): to, co pan bral dotad z oszczednosci ludu tej osady (podatek od skarbca ludu, W7), placi lud
            //     z kies ponad zapas dni - nie kasa kupcow; brak - zaleglosc (splacana pierwsza, zajecie z kiesy gminy jak firma)
            double wantLf = f.ALf + iLf;
            if (wantLf >= 1)
            {
                long gF = FolkToLord(st, lord, (long)Math.Floor(wantLf));
                f.ALf = Math.Max(0, wantLf - gF);
                _paidLf += gF;
            }
            else f.ALf = wantLf;
            // 3. dziesiata od ruchomosci kasy (M9): wymiar w pierwszy dzien raty po nadaniu wojennym, potem 1/13 co 7 dob; w przejsciu rata x rampa (reszta darowana)
            if (assess)
            {
                double baseM = Math.Max(0, t.Gold - OverReserve * reserve);
                if (f.MovK != null && f.MovK != k.StringId && f.MovLeft > 0) { _movLost += f.MovLeft; f.MovLeft = 0; }
                double a = g * Math.Max(0, Math.Min(0.3, (double)s.DsMovablesRate)) * baseM;
                f.MovLeft += a; f.MovPer = f.MovLeft / Instalments; f.MovCur = a; f.MovK = k.StringId; f.MovSerial = serial; _movAssess += a;
            }
            if (f.MovLeft >= 1)
            {
                if (k == null || f.MovK != k.StringId) { _movLost += f.MovLeft; f.MovLeft = 0; f.MovPer = 0; f.MovCur = 0; f.MovK = null; }
                else
                {
                    double inst = Math.Min(f.MovPer, f.MovLeft);
                    double instR = inst * rn;
                    if (inst - instR > 0) { f.MovLeft -= inst - instR; f.MovCur = Math.Max(0, f.MovCur - (inst - instR)); _movRamp += inst - instR; }   // przejscie: czesc raty darowana
                    long got = ToCrown(t, k, (long)Math.Floor(Math.Min(instR, avail)));
                    if (got > 0) { f.MovLeft -= got; f.MovCur = Math.Max(0, f.MovCur - got); avail -= got; crownMov += (int)got; _movPaid += got; DsSeason.NoteMovables(k, got); }
                    if (got + 0.5 < instR) _movShort += instR - got;
                    if (f.MovLeft < 1) { f.MovLeft = 0; f.MovPer = 0; f.MovCur = 0; }
                }
            }
            // 4. zajecie (miasto): zaleglosc przez DsFarmDistraintTerms dni kwartalu - z kiesy gminy (najwyzej 2/3 jej stanu)
            if (town && f.ATerms >= Math.Max(1, Math.Min(8, s.DsFarmDistraintTerms)) && f.AL + f.AC + f.ALf >= 1) Distrain(st, f, lord, k);
            double after = f.AL + f.AC + f.ALf;
            if (after > before) _arrNew += after - before; else _arrPaid += before - after;
            // 5. darowanie zaleglosci ponad DsFarmForgiveWeeks rat (licznik osobno - nie "splacone")
            int fw = ForgiveWeeks(s);
            double instAll = (f.FL + f.FC + f.FLf) / Weeks;
            if (fw > 0 && instAll > 0)
            {
                double cap = fw * instAll, arr = f.AL + f.AC + f.ALf;
                if (arr > cap) { double r = cap / arr; _forgiven += arr - cap; _forgN++; f.AL *= r; f.AC *= r; f.ALf *= r; }
            }
        }

        /// <summary>Czesc firmy pana z oszczednosci ludu: kiesy ludu osady ponad zapas dni (TownFolk.DsFolkRate) -> glowa rodu (RentToday - D "ziemia", jak podatki panu).
        /// Wyjatek przy przelewie oddaje zloto ludowi. Zwraca zaplacone.</summary>
        private static long FolkToLord(Settlement st, Hero lord, long want)
        {
            if (want <= 0 || lord == null) return 0;
            long took = TownFolk.DsFolkRate(st, want);
            if (took <= 0) return 0;
            int h0 = lord.Gold;
            try { lord.ChangeHeroGold((int)Math.Min(int.MaxValue, took)); }
            catch (Exception e) { TownFolk.DsFolkRateUndo(st, took); Stumble("FolkToLord", e); return 0; }
            long inL = (long)lord.Gold - h0;
            var clan = st.OwnerClan;
            if (clan != null) { int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = (int)Math.Min(int.MaxValue, (long)r0 + took); }
            CirculationWindows.NoteHeroGold(lord, (int)Math.Min(int.MaxValue, took));
            _payOut += took; _payIn += inL;
            return took;
        }

        /// <summary>Zajecie: zaleglosc firmy z kiesy gminy miasta (najwyzej 2/3 jej stanu) - kiesa gminy -> pan (RentToday, D "ziemia") i skarbiec (wplyw dnia przez
        /// TownPurse.CrownToday). Zaleglosc pana z oszczednosci ludu (ALf) jak zaleglosc pana. Wyjatek przy przelewie oddaje zloto gminie.</summary>
        private static void Distrain(Settlement st, Farm f, Hero lord, Kingdom k)
        {
            try
            {
                double arr = f.AL + f.AC + f.ALf;
                if (arr < 1) return;
                if (k == null) { f.AL += f.AC; f.AC = 0; }
                double aL = f.AL + f.ALf;
                long took = TownFolk.DistrainCommune(st, (long)Math.Ceiling(aL + f.AC));
                if (took <= 0) return;
                long dL = k == null ? took : (long)Math.Floor(took * aL / (aL + f.AC)), dC = took - dL;
                long inL = 0, inC = 0;
                if (dL > 0)
                {
                    int h0 = lord.Gold;
                    try { lord.ChangeHeroGold((int)dL); }
                    catch (Exception e) { TownFolk.DistrainUndo(st, took); Stumble("Distrain(pan)", e); return; }
                    inL = (long)lord.Gold - h0;
                    var clan = st.OwnerClan;
                    if (clan != null) { int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = (int)Math.Min(int.MaxValue, (long)r0 + dL); }
                    CirculationWindows.NoteHeroGold(lord, (int)dL);
                    double fromLf = Math.Min(f.ALf, dL);                    // najpierw zaleglosc z oszczednosci ludu (gmina placi za swoich mieszczan), potem z kasy
                    f.ALf = Math.Max(0, f.ALf - fromLf); f.AL = Math.Max(0, f.AL - (dL - fromLf)); _distL += dL;
                }
                if (dC > 0)
                {
                    int w0 = k.KingdomBudgetWallet;
                    try { k.KingdomBudgetWallet += (int)dC; inC = (long)k.KingdomBudgetWallet - w0; }
                    catch (Exception e) { TownFolk.DistrainUndo(st, dC); Stumble("Distrain(korona)", e); dC = 0; }
                    if (dC > 0) { TownPurse.DsCrownFolk(k, dC); f.AC = Math.Max(0, f.AC - dC); _distC += dC; }
                }
                _payOut += dL + dC; _payIn += inL + inC; _distN++;
            }
            catch (Exception e) { Stumble("Distrain", e); }
        }

        /// <summary>M6: 1/DsMerchantDrawDays kasy ponad 1.5 x zapas kupcow - miasto do P i R (zarobek), zamek do kiesy podzamcza; w przejsciu x rampa (stary zawor daje ludowi
        /// swoja czesc x (1 - rampa)). Zwraca wyplacone.</summary>
        private static long Draw(Settlement st, Town t, Farm f, int reserve, Settings s, bool town)
        {
            int days = DrawDays(s);
            if (days <= 0) return 0;
            double over = t.Gold - OverReserve * reserve;
            if (over < days) return 0;
            long x = (long)Math.Floor(over / days * NewShare);
            if (x <= 0) return 0;
            if (town)
            {
                long got = TownFolk.EarnDraw(st, x, Unit(s.DsMerchantDrawPShare));
                if (got > 0)
                {
                    _drawT += got; _payOut += got; _payIn += got;
                    MoneyLedger.SplitTownMark(MoneyLedger.MRent, MoneyLedger.MFolkValve, got);   // ksiega: kasa -> kiesy ludu (jak czesc ludu zaworu, M19)
                    TownPurse.NoteTake(st, (int)Math.Min(int.MaxValue, got));                     // wyplata z zysku kupcow - doplyw netto bez zmiany definicji
                }
                return got;
            }
            int gc = TownFolk.EarnCastleValve(st, (int)Math.Min(int.MaxValue, x));
            if (gc > 0) { _drawC += gc; _payOut += gc; _payIn += gc; f.PaidWin += gc; }
            return Math.Max(0, gc);
        }

        // ============================================================ M7 pomoc od sprzedazy (Levies)
        /// <summary>TownLive / CastleLiveDay (krok 2 doby ludu): sprzedaz WLASNA kasy w oknie (recenzja 17.10: zycie i zolnierze bez uslug ludu, dwor bez czesci ludu, sprzedaz,
        /// zaplata ludu, wsie - te same liczby co podstawa robocizny BaseLife / BaseCourt) - podstawa M7 i prognozy nadania; w oknie pomiaru takze srednia do ustalenia firm.</summary>
        internal static void NoteSales(Settlement st, double sales)
        {
            try
            {
                if (!On || st == null || !(sales > 0) || double.IsInfinity(sales)) return;
                var f = FarmOf(st, true);
                if (f == null) return;
                f.Sales = sales; f.SalesDay = Today;
                if (InWindow)
                {
                    // tylko doby wojny wlasciciela - pomoc od sprzedazy przy g = 1, jaka bylaby w oknie (w pokoju kasa nie placila daniny i nie placi pomocy - bez ciecia firmy)
                    var oc = st.OwnerClan; var ok = oc != null ? oc.Kingdom : null;
                    if (ok != null && !ok.IsEliminated && KingdomTreasury.AtWar(ok)) f.SW += sales;
                }
                DsSeason.NoteSales(st, sales);
            }
            catch (Exception e) { Stumble("NoteSales", e); }
        }

        /// <summary>KingdomTreasury.Levies (F3 czynne, po petli osad): pomoc od sprzedazy min(DsAidesMax, g x DsAidesRate) x rampa przejscia z kasy (ponad podloge FloorOf) do
        /// skarbca - wplyw dnia (LeviesToday) i garnek W5; brak - do ksiegi (dzien raty, przed firma), zaleglosc ponad DsFarmForgiveWeeks tygodni pomocy - darowana (recenzja
        /// 17.10: bez limitu rosla bez konca). Oblezone - darowane. Na poczatku: zaleglosc pomocy zaplacona w dniu raty (Pay) do wplywu dnia krolestwa.</summary>
        internal static void AidesDay(Dictionary<Kingdom, long> levies)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !FarmsLive || levies == null) return;
                if (_aidPend.Count > 0)
                {
                    foreach (var k0 in Kingdom.All)
                    {
                        if (k0 == null || k0.StringId == null) continue;
                        long x;
                        if (!_aidPend.TryGetValue(k0.StringId, out x) || x <= 0) continue;
                        long lk; levies.TryGetValue(k0, out lk); levies[k0] = lk + x;   // zloto juz w skarbcu (Pay) - tu tylko wplyw dnia 165
                    }
                    _aidPend.Clear();
                }
                int today = Today;
                double rate1 = Math.Max(0, Math.Min(0.1, (double)s.DsAidesRate)), max = Math.Max(0, Math.Min(0.3, (double)s.DsAidesMax)), rn = NewShare;
                int fw = ForgiveWeeks(s);
                foreach (var f in _farm.Values)
                {
                    try
                    {
                        if (f.SalesDay != today || !(f.Sales > 0)) continue;
                        double sales = f.Sales; f.Sales = 0;
                        var st = f.St;
                        var t = st != null ? st.Town : null;
                        var clan = st != null ? st.OwnerClan : null;
                        var k = clan != null ? clan.Kingdom : null;
                        if (t == null || k == null || k.IsEliminated) continue;
                        double g = DsSeason.GrantOf(k);
                        if (g <= 0) continue;
                        if (st.IsUnderSiege) { _aidRemit++; continue; }
                        double day = sales * Math.Min(max, g * rate1) * rn;
                        f.AidLast = day;
                        double due = day + f.AidCarry;
                        long want = (long)Math.Floor(due);
                        f.AidCarry = due - want;
                        if (want <= 0) continue;
                        int reserve = st.IsTown ? TownPurse.Reserve(t) : CastlePurse.Reserve(t);
                        long avail = Math.Max(0L, (long)Math.Floor(t.Gold - FloorOf(s, reserve)));
                        long got = ToCrown(t, k, Math.Min(want, avail));
                        if (got > 0)
                        {
                            long lk; levies.TryGetValue(k, out lk); levies[k] = lk + got;   // wplyw dnia 165
                            DsSeason.NoteAides(k, got);
                            _aidPaid += got;
                            if (st.IsTown) { TownPurse.NoteTake(st, (int)got); try { TownFolk.NoteTake(st, (int)got, TownFolk.TLevy); } catch { } }
                            else f.PaidWin += got;
                        }
                        if (got < want)
                        {
                            if (f.AidK != null && f.AidK != k.StringId) { _aidLost += f.AidArr; f.AidArr = 0; }
                            f.AidArr += want - got; f.AidK = k.StringId; _aidArrNew += want - got;
                        }
                        if (fw > 0 && f.AidArr >= 1 && day > 0)
                        {
                            double cap = fw * 7.0 * day;                    // najwyzej DsFarmForgiveWeeks tygodni pomocy tej osady - reszta darowana (jak zaleglosc firmy)
                            if (f.AidArr > cap) { _aidForg += f.AidArr - cap; _aidForgN++; f.AidArr = cap; }
                        }
                    }
                    catch (Exception e) { Stumble("AidesDay(osada)", e); }
                }
            }
            catch (Exception e) { Stumble("AidesDay", e); }
        }

        /// <summary>L1 (limit zaliczki): niezebrana dziesiata od ruchomosci kas osad krolestwa - tylko z wymiaru biezacej sesji nadania `serial` (recenzja 17.10: reszta
        /// z poprzednich kwartalow to roszczenie, ktorego moze nie byc - nie podstawa zaliczki).</summary>
        internal static double MovLeftOf(string kid, int serial)
        {
            double t = 0;
            if (kid == null) return 0;
            foreach (var f in _farm.Values) if (f.MovK == kid && f.MovSerial == serial && f.MovLeft > 0) t += Math.Min(f.MovLeft, Math.Max(0, f.MovCur));
            return t;
        }

        // ============================================================ linia doby
        private static string N0(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v).ToString("0", Inv); }

        /// <summary>ArmouryBehavior (koniec bloku doby): linia "Firmy miast i zamkow (DS)" (okno od poprzedniej linii) i "Pozyczki zaliczkowe (DS)"; liczniki od zera.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                int day = Today - 1;
                foreach (var ev in _events) Log.Info("Firmy miast i zamkow (DS): dzien " + day + " | " + ev + ".");
                _events.Clear();
                try { TownFolk.AntLine(day); } catch (Exception e) { Stumble("Daily(zaliczki)", e); }
                if (!On) return;
                var sb = new StringBuilder(1800);
                sb.Append("Firmy miast i zamkow (DS): dzien ").Append(day);
                if (_fixDay < 0)
                {
                    double mL = 0, mC = 0, mLf = 0, mS = 0, mLw = 0; foreach (var f in _farm.Values) { mL += f.ML; mC += f.MC; mLf += f.MLf; mS += f.SW; mLw += f.LW; }
                    sb.Append(" | POMIAR dzien ").Append(_mDays).Append('/').Append(MeasureDays(s)).Append(" (od doby ").Append(_dayOn).Append(") - stare zasady; zmierzone: panom z kas ")
                      .Append(N0(mL)).Append(" (zawor po dekrecie) + z oszczednosci ludu ").Append(N0(mLf)).Append(" (czesc pana podatku od skarbca ludu, W7 z ludu), koronie ").Append(N0(mC))
                      .Append(" (zawor) - srednio ").Append(N0(_mDays > 0 ? (mL + mC + mLf) / _mDays : 0)).Append(" zl/d; sprzedaz kas ").Append(N0(mS)).Append(", danina miast ").Append(N0(mLw));
                }
                else
                {
                    double fT = 0, fC = 0, fLf = 0, aL = 0, aC = 0, aLf = 0, aid = 0, mov = 0, cut = 0; int arrN = 0, distReady = 0, nT = 0, nC = 0;
                    foreach (var f in _farm.Values)
                    {
                        bool isT = f.St != null && f.St.IsTown;
                        if (isT) { fT += f.FL + f.FC; nT++; } else { fC += f.FL + f.FC; nC++; }
                        fLf += f.FLf; cut += f.AidCut;
                        aL += f.AL; aC += f.AC; aLf += f.ALf; aid += f.AidArr; mov += f.MovLeft;
                        if (f.AL + f.AC + f.ALf >= 1) arrN++;
                        if (f.ATerms >= Math.Max(1, s.DsFarmDistraintTerms) && f.AL + f.AC + f.ALf >= 1) distReady++;
                    }
                    sb.Append(" | firmy od doby ").Append(_fixDay).Append(": miasta ").Append(nT).Append(" - ").Append(N0(fT)).Append(" zl/rok, zamki ").Append(nC).Append(" - ").Append(N0(fC))
                      .Append(" zl/rok z kas (rata 1/").Append(Weeks).Append(" co 7 dob); panom z oszczednosci ludu ").Append(N0(fLf)).Append(" zl/rok (")
                      .Append(s.DsFarmFolkFromSavings ? "placi lud ponad zapas dni" : "klucz wylaczony - placi kasa").Append("); firma korony zmniejszona o pomoc od sprzedazy ").Append(N0(cut)).Append(" zl/rok")
                      .Append(" | przejscie: rampa ").Append(NewShare.ToString("0.000", Inv)).Append(" (zawor, P1, podatek od skarbca ludu, danina miast, W7 z ludu x ").Append(OldShare.ToString("0.000", Inv)).Append(")")
                      .Append(" | raty dzis: osad ").Append(_nDue).Append(" (miast ").Append(_nDueT).Append("), nalezne panom ").Append(N0(_dueL)).Append(", koronie ").Append(N0(_dueC))
                      .Append(", panom z oszczednosci ludu ").Append(N0(_dueLf))
                      .Append("; zaplacone z kas panom ").Append(_paidL).Append(", koronie ").Append(_paidC).Append(", z oszczednosci ludu panom ").Append(_paidLf)
                      .Append("; darowane ").Append(N0(_remit)).Append(" (oblezenie ").Append(_remitSiege).Append(", zdobycie / lup ").Append(_remitCap).Append(")")
                      .Append(", bez zywego pana (czeka) ").Append(N0(_noLordAmt)).Append(" (").Append(_noLord).Append(")")
                      .Append(" | zaleglosci: nowe ").Append(N0(_arrNew)).Append(", splacone ").Append(N0(_arrPaid)).Append(", razem panom ").Append(N0(aL)).Append(" + koronie ").Append(N0(aC))
                      .Append(" + panom z oszczednosci ludu ").Append(N0(aLf))
                      .Append(" u ").Append(arrN).Append(" osad (do zajecia ").Append(distReady).Append("), zajete z kies gmin ").Append(_distL + _distC).Append(" (panom ").Append(_distL).Append(", koronie ").Append(_distC)
                      .Append("; ").Append(_distN).Append("), darowane ponad ").Append(ForgiveWeeks(s)).Append(" rat ").Append(N0(_forgiven)).Append(" (").Append(_forgN).Append(")")
                      .Append(" | odnowy ").Append(_renewN).Append(" (w gore ").Append(_renewUp).Append(", w dol ").Append(_renewDown).Append(")")
                      .Append(" | wyplata kupcom M6: miasta ").Append(_drawT).Append(" (P / R), zamki ").Append(_drawC).Append(" (podzamcza)")
                      .Append(" | pomoc od sprzedazy M7: do skarbcow ").Append(_aidPaid).Append(", nowa zaleglosc ").Append(N0(_aidArrNew)).Append(", zaleglosc splacona ").Append(_aidArrPaid)
                      .Append(", przepadla (zmiana krolestwa) ").Append(N0(_aidLost)).Append(", oblezone bez poboru ").Append(_aidRemit).Append(", zaleglosci razem ").Append(N0(aid))
                      .Append(", darowane ponad ").Append(ForgiveWeeks(s)).Append(" tygodni pomocy ").Append(N0(_aidForg)).Append(" (").Append(_aidForgN).Append(")")
                      .Append(" | dziesiata od ruchomosci kas M9: wymiar ").Append(N0(_movAssess)).Append(", zaplacone ").Append(_movPaid).Append(", darowane ").Append(N0(_movRemit))
                      .Append(" (w przejsciu ").Append(N0(_movRamp)).Append(")")
                      .Append(", niezaplacone (na nastepne raty) ").Append(N0(_movShort)).Append(", przepadle (zmiana krolestwa) ").Append(N0(_movLost)).Append(", niezebrane ").Append(N0(mov))
                      .Append(" | rampa zbytkow ludu ").Append(FolkRamp.ToString("0.000", Inv));
                    // f4 P5: przeglad kwartalny firm (tylko przy Ds Farm Review Days > 0 - inaczej linia jak dotad)
                    int qd = ReviewDays(s);
                    if (qd > 0)
                    {
                        int at = Math.Max(0, Math.Min(8, s.DsFarmArrearsReviewTerms));
                        int aq = 0; foreach (var f in _farm.Values) if (f.ATerms >= 1 && f.AL + f.AC + f.ALf >= 1) aq++;
                        double mr = Math.Max(0, Math.Min(0.5, (double)s.DsFarmRenewMax));
                        sb.Append(" | przeglad firm co ").Append(qd).Append(" dob (f4): w dol wymuszone (-").Append((mr * 100).ToString("0.##", Inv)).Append("%) przy zaleglosciach przez ")
                          .Append(at).Append(" dni kwartalu z rzedu ").Append(_revArrN).Append(" (z tego przed wlasna doba ").Append(_revEarly).Append(")")
                          .Append(", w gore wstrzymane przy zaleglosciach ").Append(_revHeld).Append(", bez zmiany ").Append(_revSame)
                          .Append("; firmy w gore +").Append(N0(_revUpAmt)).Append(" zl/rok, w dol -").Append(N0(_revDownAmt)).Append(" zl/rok")
                          .Append("; osad z zaleglosciami w ostatnim dniu kwartalu ").Append(aq)
                          .Append("; najkrotszy odstep przegladow tej samej osady ").Append(_revMinGap == int.MaxValue ? "-" : _revMinGap.ToString(Inv))
                          .Append(" dob (limit: najwyzej jedna zmiana na ").Append(Math.Min(qd, Quarter)).Append(" dob)");
                    }
                }
                sb.Append(" | kontrola: platnicy - odbiorcy = ").Append(_payOut - _payIn);
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (w sesji ").Append(_stumblesSess).Append(")");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearCounters(); }
        }

        // ============================================================ zapis arm_daniny_miasta
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('~') >= 0; }
        private static string R(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v, 3).ToString("R", Inv); }

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(256 + _farm.Count * 80);
                sb.Append("1|w~").Append(_dayOn.ToString(Inv)).Append('~').Append(_fixDay.ToString(Inv)).Append('~').Append(_mDays.ToString(Inv)).Append('~')
                  .Append(double.IsNaN(_worldShare) ? "-" : _worldShare.ToString("R", Inv));
                foreach (var kv in _farm)
                {
                    var f = kv.Value;
                    if (BadId(kv.Key)) continue;
                    bool any = f.FL > 0 || f.FC > 0 || f.FLf > 0 || f.ML > 0 || f.MC > 0 || f.MLf > 0 || f.SW > 0 || f.AL > 0 || f.AC > 0 || f.ALf > 0 || f.AidArr > 0 || f.MovLeft > 0 || f.Fixed >= 0;
                    if (!any) continue;
                    sb.Append("|f~").Append(kv.Key).Append('~').Append(R(f.FL)).Append('~').Append(R(f.FC)).Append('~').Append(f.Fixed.ToString(Inv)).Append('~')
                      .Append(double.IsNaN(f.Share) ? "-" : f.Share.ToString("R", Inv)).Append('~').Append(R(f.AL)).Append('~').Append(R(f.AC)).Append('~').Append(f.ATerms.ToString(Inv))
                      .Append('~').Append(R(f.AidArr)).Append('~').Append(BadId(f.AidK) ? "-" : f.AidK).Append('~').Append(R(f.MovLeft)).Append('~').Append(R(f.MovPer))
                      .Append('~').Append(BadId(f.MovK) ? "-" : f.MovK).Append('~').Append(f.MovSerial.ToString(Inv)).Append('~').Append(R(f.ML)).Append('~').Append(R(f.MC))
                      .Append('~').Append(R(f.FLf)).Append('~').Append(R(f.ALf)).Append('~').Append(R(f.MLf)).Append('~').Append(R(f.SW)).Append('~').Append(R(f.LW))
                      .Append('~').Append(R(f.AidCut)).Append('~').Append(R(f.MovCur));   // recenzja 17.10: pola 17-23 (starszy zapis bez nich = 0)
                }
                DsSeason.Export(sb);
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static double D(string x) { double v; return x != null && x != "-" && double.TryParse(x, NumberStyles.Float, Inv, out v) && !double.IsInfinity(v) ? v : double.NaN; }
        private static double D0(string x) { double v = D(x); return double.IsNaN(v) || v < 0 ? 0 : v; }
        private static int I(string x, int def) { int v; return x != null && int.TryParse(x, NumberStyles.Integer, Inv, out v) ? v : def; }

        /// <summary>Wczytanie (po Reset): brak klucza (stary zapis) albo zly format - zera (przy wlaczonym kluczu okno pomiaru od pierwszej doby).</summary>
        internal static void Import(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var parts = data.Split('|');
                if (parts.Length == 0 || parts[0] != "1") return;
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        var a = parts[i].Split('~');
                        if (a.Length < 2) continue;
                        if (a[0] == "w" && a.Length >= 5)
                        {
                            _dayOn = I(a[1], -1); _fixDay = I(a[2], -1); _mDays = Math.Max(0, I(a[3], 0)); _worldShare = D(a[4]);
                            if (_dayOn < 0) { _dayOn = -1; _fixDay = -1; _mDays = 0; }
                        }
                        else if (a[0] == "f" && a.Length >= 17 && !BadId(a[1]))
                        {
                            var f = new Farm
                            {
                                FL = D0(a[2]), FC = D0(a[3]), Fixed = I(a[4], -1), Share = D(a[5]), AL = D0(a[6]), AC = D0(a[7]), ATerms = Math.Max(0, I(a[8], 0)),
                                AidArr = D0(a[9]), AidK = a[10] == "-" ? null : a[10], MovLeft = D0(a[11]), MovPer = D0(a[12]), MovK = a[13] == "-" ? null : a[13],
                                MovSerial = I(a[14], -1), ML = D0(a[15]), MC = D0(a[16])
                            };
                            if (a.Length >= 24)
                            {
                                f.FLf = D0(a[17]); f.ALf = D0(a[18]); f.MLf = D0(a[19]); f.SW = D0(a[20]); f.LW = D0(a[21]); f.AidCut = D0(a[22]); f.MovCur = D0(a[23]);
                            }
                            else f.MovCur = f.MovLeft;   // zapis sprzed recenzji: cala niezebrana dziesiata jak wymiar biezacy
                            if (f.AidArr <= 0) f.AidK = null;
                            if (f.MovLeft <= 0) { f.MovLeft = 0; f.MovPer = 0; }
                            _farm[a[1]] = f;
                        }
                        else DsSeason.ImportPart(a);
                    }
                    catch (Exception e) { Stumble("Import(wpis)", e); }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
