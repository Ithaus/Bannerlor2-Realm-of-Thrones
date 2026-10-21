using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// DANINY SREDNIOWIECZNE (DS) - KROK F1: WSIE (docs/PROJEKT-DANINY-SREDNIOWIECZNE-2026-10-17.md rozdz. 3 i 10.1; decyzja Jeffa 17.10: "daniny, oplaty i logika
    /// musi byc taka jak byla w sredniowieczu ... system dynamiczny"). Klucz glowny DuesMedieval; wies DS = wies przy kiesie ludu czynnej (TownFolk.Live).
    /// ODSTEPSTWO od projektu (zadanie 17.10): DS dziala BEZ kalibracji startu KS (galaz bez KS) - On(st) nie czyta StartCalibration.AppliedFor; koszyk wsi to dzisiejsza
    /// stara lista dnia (buy1, TownFolk.Villages). "Alfa z praw" (rozdz. 4) i kredyt kupcow KS-H (F2, rozdz. 5) - galaz KS; tu tylko API BudgetShare / G / RentPerDay.
    ///
    /// Zasada (Z1, Z2): zadna danina nie jest "x% dziennie tego, co ponad prog". Kolejnosc wydatkow wsi:
    ///  0. przy wozie (VillageTakings.EnterPostfix, po 112 i F4b - zloto ledwo weszlo do kiesy): udzial pana i majatki (W1, bez zmian), dziesiecina 5% oddanego utargu
    ///     (W2: polowa jalmuzna zostaje w kiesie, polowa septy - place ludu i material miasta targowego; TownFolk.TitheVillageAtCart), udzial korony g/10 czesci wsi po
    ///     dziesiecinie (W4: g = 1 w wojnie, 1.5 z polityka War Tax, 0 w pokoju; wies spladrowana, miasto targowe/zamek oblezone albo glod - 0) -> skarbiec, wplyw dnia
    ///     (LeviesToday przy najblizszym KingdomTreasury.Levies), DailyLife.NoteVillageLevy; to samo g/10 od czesci wsi z markietanow (Sutlers.One - WarShareSutlers;
    ///     bez NoteVillageLevy - ZC0 nie liczy tego wplywu do kiesy, recenzja calosci 17/18.10);
    ///  1. czynsz staly (W3, PopulationLaw - przed TownFolk.Daily) placony pierwszy: rata dnia + zaleglosc, najwyzej kiesa; brak - ksiega zaleglosci (DsRentArrears);
    ///     spladrowanie (wies), oblezenie miasta targowego, glod - rata dnia darowana; zaleglosc ponad DsRentForgiveDays rat - darowana ("decasus", licznik);
    ///  2. koszyk dnia (stara lista, buy1 - bez zmian); 3. zaleglosc koszyka wozem (E3-W1, bez zmian);
    ///  4. skrzynia na czarna godzine Chest = DsVillageChestDays x (koszyk dnia + czynsz dnia); ponizej niej nic wiecej nie idzie;
    ///  5. zbytki tylko z nadwyzki: rampa x (kiesa - skrzynia) / DsSurplusSpendDays dziennie - (1 - DsSurplusBkShare) na liste dostatku P5 w miescie targowym, reszta
    ///     tego budzetu na liste wsi (najpierw sol i narzedzia przy VillWorkOnly), a czego nie ma na polkach - ROBOTY GMINY (DsCommunalWorks, recenzja 17.10: fabryka
    ///     kosciola, mlyn, most, drogi - 3/4 place ludu miasta targowego, 1/4 material do jego kasy; bez tego pieniadz wsi bez towaru zostawal w kiesie na zawsze);
    ///     DsSurplusBkShare na rynek wsi BK (BkSeal34, najblizszy tick BK; niewydane w ticku - nastepnego dnia do czesci P5). Pozyczki ludu dla panow (TownFolk.Loans) -
    ///     w rampie prog (1 - rampa) x stary + rampa x skrzynia.
    /// KLUCZ DuesMedieval DOMYSLNIE WYLACZONY (recenzja 17.10, krytyczne): bez koszyka KS wies nie ma na co wydac uwolnionych pieniedzy - wlaczenie tylko po autotescie.
    /// Pobory "resztowe" kiesy wsi DS (danina korony od kiesy w Levies, podatek od nadwyzki PayHoardVillage, stawka wojenna pana W7, rynek BK 5% kiesy, 15%/d nadwyzki
    /// na stara liste i stara drabina P5) slabna liniowo przez rampe DsRampDays (28 dob) od pierwszej doby z kluczem (DayOn w zapisie) - x (1 - Ramp); nowe (W4,
    /// krok 5) rosna x Ramp. Wylaczenie klucza - stare zasady od razu, DayOn od nowa (ponowne wlaczenie = nowa rampa); zaleglosci zostaja w zapisie bez skutku.
    /// Zamknieta ekonomia: kazdy przelew to ChangeGold platnika i dokladnie tyle u odbiorcy (kontrola w linii "Daniny wsi (DS)": platnicy - odbiorcy = 0);
    /// zaleglosc to roszczenie, nie pieniadz. Zapis arm_daniny (SaveText): "1|r~DayOn|v~wies~zaleglosc~doba|c~krolestwo~W4 w skarbcu przed wplywem dnia";
    /// brak klucza (stary zapis) = zera, rampa od pierwszej doby z nowa DLL. Kazde zdarzenie we wlasnym try, potkniecia liczone (bez globalnych wylacznikow).
    /// </summary>
    internal static partial class DsVillage
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double AlmsShare = 0.5;           // polowa dziesieciny wraca jako jalmuzna (TownFolk.TitheAlms)
        private const double BkOldShare = 0.05;         // stary rynek wsi BK: 5% kiesy (BkSeal34)

        // ------------------------------------------------------------ stan kampanii (zapis arm_daniny)
        private sealed class Arr { public double Owed; public int Since = -1; public double LastDue; }
        private static int _dayOn = -1;                                                       // pierwsza doba DS (rampa); -1 = jeszcze nie
        private static readonly Dictionary<string, Arr> _arr = new Dictionary<string, Arr>();  // id wsi -> zaleglosc czynszu
        private static readonly Dictionary<string, long> _pend = new Dictionary<string, long>(); // id krolestwa -> W4 juz w skarbcu, jeszcze nie w wplywie dnia (LeviesToday)

        // ------------------------------------------------------------ sesja
        private static readonly Dictionary<string, int> _bk = new Dictionary<string, int>();     // id wsi -> polowa kroku 5 na rynek BK (do najblizszego ticku BK wsi)
        private static readonly Dictionary<string, int> _bkTick = new Dictionary<string, int>(); // id wsi -> doba ostatniego ticku rynku BK wsi (wies bez rynku BK - krok 5 caly na P5)
        private static readonly Dictionary<string, int> _bkLeft = new Dictionary<string, int>(); // id wsi -> nowy budzet BK niewydany w ostatnim ticku (brak towaru z nadwyzka podazy) - jutro P5 / roboty
        // m105 gosp4 G2 (Ks Village Grain Spike From Surplus): id wsi -> skok ceny ziarna i soli zaplacony z nadwyzki ponad skrzynie (zakup koszyka za 1-2 doby), jeszcze nie
        // odjety od czesci P5 kroku 5 (SurplusDay odejmuje najwyzej P5 doby, reszta czeka na nastepne doby). Sesja (bez zapisu - najwyzej 2 doby kroku 5 jednej wsi).
        private static readonly Dictionary<string, double> _spikeOwed = new Dictionary<string, double>();
        private static bool _startLogged;
        private static int _stumbles, _stumblesSess;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        // ------------------------------------------------------------ liczniki od ostatniej linii (Daily)
        private static int _cN, _cNoPart, _cPeaceN, _cRemitRaid, _cRemitSiege, _cRemitFam, _cTiNoList, _cTiNoPriest, _cTiNoMarket;
        private static long _cHanded, _cPart, _cLord, _cTithe, _cAlms, _cWage, _cMat, _cTiStay, _cCrown, _cWorks, _cSide;
        private static int _sN, _sRemit; private static long _sBase, _sCrown;
        private static int _iN; private static long _iBase, _iCrown;                            // sw-zajazdy (recenzja): czesc z tego - noclegi w zajazdach przy szlakach (WarShareSutlers inn)
        private static int _rN, _rRemitN, _rRemitRaid, _rRemitSiege, _rRemitFam, _rForgivenN;
        private static double _rDue, _rPaid, _rArrPaid, _rNewArr, _rForgiven, _rRemit, _rLost;
        private static int _kVill; private static double _kGold, _kChest, _kOver, _kBudget, _kP5Plan, _kBkPlan, _kP5, _kOldExtra, _kOldLadder;
        private static long _kBkSpent, _kBkOldPlan, _kBkNewPlan, _kBkOldSpent, _kBkNoMkt, _kBkLeft, _kBkCarry; private static int _kBkTicks, _kBkNoMktN, _kBkCarryN;
        private static long _kGoods, _kWorks, _kWorksWage, _kWorksMat; private static double _kWorksNo; private static int _kWorksN, _kWorksNoN;
        private static long _oLevy, _oHoard, _oW7, _oW7Cut; private static int _oLevyN, _oHoardN, _oW7N, _oW7CutN;
        private static long _payOut, _payIn, _flushed;
        private static long _k5Treas; private static int _k5N;                                   // K5 przy samym F1: brak zwrotu pokryty ze skarbca ponad zapas wojny (DsSeason.TreasuryForRefund)
        private static double _kKept; private static int _kKeptN;                               // r4-bkwies (recenzja): czesc P5 zostawiona w kiesie (potrzeba pokryta - krok 5 do potrzeby, wies z koszykiem ZC4)
        private static double _kSpike; private static int _kSpikeN;                             // m105 gosp4 G2: P5 mniej o skok ceny chleba zaplacony z nadwyzki (zl, wsi)
        private static double _rBread; private static int _rBreadN;                             // m105 gosp4 G5: rata dnia czynszu do ksiegi zaleglosci, bo kiesa nie miala nic ponad koszyk dnia (zl, wsi)

        private static void ClearCounters()
        {
            _cN = _cNoPart = _cPeaceN = _cRemitRaid = _cRemitSiege = _cRemitFam = _cTiNoList = _cTiNoPriest = _cTiNoMarket = 0;
            _cHanded = _cPart = _cLord = _cTithe = _cAlms = _cWage = _cMat = _cTiStay = _cCrown = _cWorks = _cSide = 0;
            _sN = _sRemit = 0; _sBase = _sCrown = 0;
            _iN = 0; _iBase = _iCrown = 0;
            _rN = _rRemitN = _rRemitRaid = _rRemitSiege = _rRemitFam = _rForgivenN = 0;
            _rDue = _rPaid = _rArrPaid = _rNewArr = _rForgiven = _rRemit = _rLost = 0;
            _kVill = 0; _kGold = _kChest = _kOver = _kBudget = _kP5Plan = _kBkPlan = _kP5 = _kOldExtra = _kOldLadder = 0;
            _kBkSpent = _kBkOldPlan = _kBkNewPlan = _kBkOldSpent = _kBkNoMkt = _kBkLeft = _kBkCarry = 0; _kBkTicks = _kBkNoMktN = _kBkCarryN = 0;
            _kGoods = _kWorks = _kWorksWage = _kWorksMat = 0; _kWorksNo = 0; _kWorksN = _kWorksNoN = 0;
            _oLevy = _oHoard = _oW7 = _oW7Cut = 0; _oLevyN = _oHoardN = _oW7N = _oW7CutN = 0;
            _payOut = _payIn = _flushed = 0;
            _k5Treas = 0; _k5N = 0;
            _kKept = 0; _kKeptN = 0;
            _kSpike = 0; _kSpikeN = 0; _rBread = 0; _rBreadN = 0;   // m105 gosp4
            _stumbles = 0;
        }

        /// <summary>Konstruktor ArmouryBehavior: nowa sesja (zapis wczytuje Import po konstruktorze).</summary>
        internal static void Reset()
        {
            _dayOn = -1; _arr.Clear(); _pend.Clear(); _bk.Clear(); _bkTick.Clear(); _bkLeft.Clear(); _startLogged = false; _atFlag = -1;
            _spikeOwed.Clear();                                                  // m105 gosp4 G2
            _stumblesSess = 0; _errOnce.Clear();
            ClearCounters();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSess++;
            try { if (_errOnce.Add(where)) Log.Error("DsVillage." + where, e); } catch { }
        }

        // ============================================================ przelaczniki
        internal static bool Key { get { var s = Settings.Current; return s != null && (s.DuesMedieval || AutotestFlag); } }

        internal const string AutotestFlagName = "armoury-ds-wlacz.flag";
        private static int _atFlag = -1;   // -1 nie sprawdzony w tej sesji, 0 nie, 1 tak

        /// <summary>Plik-przelacznik autotestu (jak KS armoury-ks-wlacz.flag): Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-ds-wlacz.flag
        /// wlacza DS tylko w trybie autotestu CrashScribe (LogGate 1 pelne / 2 profil) - nigdy w grze Jeffa; sprawdzany raz na sesje (Reset zeruje).</summary>
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

        /// <summary>F1 czynne (klucz DuesMedieval i kiesa ludu) - bez wzgledu na osade. Korona (K5 krok 1 przy samym F1: KingdomTreasury.WageRefund, DsSeason.TreasuryForRefund).</summary>
        internal static bool Live { get { return Key && TownFolk.Live; } }

        /// <summary>Wies DS: klucz DuesMedieval, wies, kiesa ludu czynna (oplata stala zamiast renty 20% - VillageTakings.VillageRentYieldsToFees). Bez KS (odstepstwo).</summary>
        internal static bool On(Settlement st) { return Key && st != null && st.IsVillage && st.Village != null && TownFolk.Live; }

        /// <summary>Daniny przy wozie (dziesiecina, udzial korony): wies DS, klucz DsVillageCartDues i okno powrotu wozu 112 wpiete i wlaczone.</summary>
        internal static bool CartDuesOn(Settlement st) { var s = Settings.Current; return On(st) && s.DsVillageCartDues && VillageTakings.CartHookLive; }

        /// <summary>Udzial korony od czesci wsi z markietanow: wies DS i klucz DsVillageCartDues (bez okna wozu).</summary>
        private static bool SutlerDuesOn(Settlement st) { var s = Settings.Current; return On(st) && s.DsVillageCartDues; }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static double Unit(double v) { return double.IsNaN(v) ? 0 : (v < 0 ? 0 : (v > 1 ? 1 : v)); }
        private static double WarShare(Settings s) { return s != null ? Math.Max(0.0, Math.Min(0.5, (double)s.DsWarShareVillage)) : 0.1; }
        private static int ForgiveDays(Settings s) { return s != null ? Math.Max(0, Math.Min(3640, s.DsRentForgiveDays)) : 364; }
        private static int ChestDays(Settings s) { return s != null ? Math.Max(0, Math.Min(364, s.DsVillageChestDays)) : 21; }
        private static int SpendDays(Settings s) { return s != null ? Math.Max(0, Math.Min(364, s.DsSurplusSpendDays)) : 15; }
        private static int ArrPayDays(Settings s) { return s != null ? Math.Max(0, Math.Min(364, s.DsRentArrearsPayDays)) : 28; }

        /// <summary>Rampa 0..1 od pierwszej doby DS (DsRampDays; 0 = od razu 1). Klucz albo kiesa ludu wylaczone - 0 (stare zasady). Pierwsze wywolanie z kluczem ustala DayOn.</summary>
        internal static double Ramp
        {
            get
            {
                var s = Settings.Current;
                if (s == null || !Key || !TownFolk.Live) return 0;
                int today = Today;
                if (_dayOn < 0) _dayOn = today;
                int days = Math.Max(0, Math.Min(364, s.DsRampDays));
                if (days <= 0) return 1;
                return Unit((today - _dayOn) / (double)days);
            }
        }

        /// <summary>Czesc starych poborow resztowych kiesy wsi: wies DS - (1 - Ramp), po rampie 0; inne wsie - 1 (jak dotad).</summary>
        internal static double OldShare(Settlement st) { return On(st) ? 1.0 - Ramp : 1.0; }

        // ============================================================ API (KS krok 8 "alfa z praw" pozniej; rozdz. 4.1)
        /// <summary>Nadanie wojenne g krolestwa: przy ustalonych firmach F3 - nadanie na kwartal z DsSeason (0, 0.5 .. 2); inaczej (F1) wojna - 1 (z polityka War Tax 1.5),
        /// pokoj albo bez krolestwa - 0.</summary>
        internal static double G(Kingdom k)
        {
            try
            {
                if (k == null || k.IsEliminated) return 0;
                double g1 = 0;
                if (KingdomTreasury.AtWar(k))
                {
                    bool wt = false;
                    try { wt = k.ActivePolicies.Contains(DefaultPolicies.WarTax); } catch { wt = false; }
                    g1 = wt ? 1.5 : 1.0;
                }
                if (!DsSeason.Live) return g1;
                // DS F3 (K2): g z nadania wojennego (stale przez kwartal); recenzja 17.10 - w przejsciu F3 liniowo od g F1 do g nadania (rampa DsRampDays od ustalenia firm)
                double gs = DsSeason.GrantOf(k), rn = DsTowns.NewShare;
                return rn >= 1 ? gs : (1 - rn) * g1 + rn * gs;
            }
            catch { return 0; }
        }

        /// <summary>Udzial wsi w oddanym utargu z prawa pana (czynny model podatku wsi - ten sam co BK i 112, dekret fiefu): 1 - podatek / utarg. Bez kiesy wsi.</summary>
        internal static double VillageShareOfTakings(Village v)
        {
            if (v == null) return 0;
            try
            {
                const int probe = 10000;
                var m = Campaign.Current != null ? Campaign.Current.Models.SettlementTaxModel : null;
                int t = m != null ? m.CalculateVillageTaxFromIncome(v, probe) : 0;
                return Unit(1.0 - t / (double)probe);
            }
            catch (Exception e) { Stumble("VillageShareOfTakings", e); return 0; }
        }

        /// <summary>
        /// API dla KS (rozdz. 4.1, k_v): czesc oddanego utargu, ktora z PRAW zostaje wsi na wydatki - (u_v - T) x (1 - g_k x DsWarShareVillage) + T x 1/2
        /// (T - dziesiecina TitheVillageShare przy TempleTithe, polowa wraca jako jalmuzna). Bez kiesy, bez rampy, bez remisji. 0..1.
        /// </summary>
        // R3-09f: lordExtra - dodatkowy udzial pana w oddanym utargu spoza modelu podatku (regale gornicze, MineRegale.KsShareOfTakings) - odjety od u_v.
        internal static double BudgetShare(Village v, Kingdom k, double lordExtra = 0)
        {
            if (v == null) return 0;
            var s = Settings.Current;
            double u = VillageShareOfTakings(v);
            if (lordExtra > 0) u = Unit(u - lordExtra);
            double t = s != null && s.TempleTithe ? Math.Max(0.0, Math.Min(0.2, (double)s.TitheVillageShare)) : 0.0;
            if (t > u) t = u;
            double w = s != null && s.DsVillageCartDues ? WarShare(s) : 0.0;
            return Unit((u - t) * (1.0 - G(k) * w) + t * AlmsShare);
        }

        /// <summary>API: czynsz staly wsi na dobe (VillageDuesPerHeadYear / 364 x wolne glowy x dekret pana); 0 - wies nieskalibrowana albo kiesa nieczynna.</summary>
        internal static double RentPerDay(Village v)
        {
            if (v == null || v.Settlement == null) return 0;
            double nv, rent;
            return TownFolk.DsVillageDay(v.Settlement, out nv, out rent) ? rent * Decree(v.Settlement) : 0;
        }

        /// <summary>API: skrzynia na czarna godzine wsi (DsVillageChestDays x (koszyk dnia + czynsz dnia)); 0 - wies nieskalibrowana.</summary>
        internal static double Chest(Village v)
        {
            if (v == null || v.Settlement == null) return 0;
            double nv, rent;
            return TownFolk.DsVillageDay(v.Settlement, out nv, out rent) ? ChestOf(v.Settlement, nv, rent) : 0;
        }

        /// <summary>Skrzynia z liczb dnia wsi (koszyk nv, czynsz przed dekretem).</summary>
        internal static double ChestOf(Settlement st, double nv, double rentBeforeDecree)
        {
            int days = ChestDays(Settings.Current);
            if (days <= 0) return 0;
            nv = VillNeeds.ChestBasket(st, nv);        // r4-bkwies (B2/M9, klucz Ks Chest Paid Basket): przy KS koszyk dnia = koszyk zaplacony z Planu (x lambda); inaczej bez zmian
            double r = Math.Max(0, rentBeforeDecree) * Decree(st);
            double c = days * (Math.Max(0, nv) + r);
            return double.IsNaN(c) || double.IsInfinity(c) ? 0 : c;
        }

        /// <summary>TownFolk.Loans (zrodlo wies DS): prog pozyczek panom w rampie - (1 - Ramp) x stary prog (zapas dni zakupow) + Ramp x skrzynia; przy rampie 0 stare zasady.</summary>
        internal static double LoanFloor(Settlement st, double oldNorm, double nv, double rentBeforeDecree)
        {
            double r = Ramp;
            if (r <= 0) return oldNorm;
            double c = ChestOf(st, nv, rentBeforeDecree);
            return (1.0 - r) * oldNorm + r * c;
        }

        private static double Decree(Settlement st)
        {
            // ks-koszt (klucz Ks Cost Fast): w petli wsi TownFolk.Villages dekret pana (polityka podatkowa BK - wywolanie przez refleksje i Traverse, kilka us) raz na wies;
            // polityka nie zmienia sie w srodku petli - te same liczby; tylko watek, ktory wlaczyl pamiec
            var memo = _decMemo;
            if (memo != null && st != null && Environment.CurrentManagedThreadId == _decMemoThread)
            {
                double m;
                if (memo.TryGetValue(st, out m)) return m;
                m = DecreeRaw(st); memo[st] = m;
                return m;
            }
            return DecreeRaw(st);
        }

        private static double DecreeRaw(Settlement st)
        {
            try { float d = PopulationLaw.TaxDecree(st); return float.IsNaN(d) || d < 0 ? 1.0 : d; } catch { return 1.0; }
        }

        private static Dictionary<Settlement, double> _decMemo; private static int _decMemoThread = -1;

        /// <summary>ks-koszt: pamiec dekretu pana na czas petli wsi (TownFolk.Villages); false - bez pamieci (zawsze w finally).</summary>
        internal static void DecreeMemo(bool on)
        {
            _decMemoThread = on ? Environment.CurrentManagedThreadId : -1;
            _decMemo = on ? new Dictionary<Settlement, double>() : null;
        }

        /// <summary>Zwolnienie z udzialu korony (W4) i z raty czynszu: 1 - wies nie Normal (rabunek, spladrowanie), 2 - zamek/miasto wsi albo miasto targowe oblezone,
        /// 3 - glod (brak jedzenia gospodarzy ponad prog Famine); 0 - bez zwolnienia.</summary>
        private static int Remit(Village v, bool raid, bool siegeAndFamine)
        {
            if (v == null) return 0;
            try
            {
                if (raid && v.VillageState != Village.VillageStates.Normal) return 1;
                if (!siegeAndFamine) return 0;
                if ((v.Bound != null && v.Bound.IsUnderSiege) || (v.TradeBound != null && v.TradeBound.IsUnderSiege)) return 2;
                if (Famine.HungerOf(v) > Famine.ThresholdNow) return 3;
            }
            catch (Exception e) { Stumble("Remit", e); }
            return 0;
        }

        private static Kingdom KingdomOf(Settlement st) { var c = st != null ? st.OwnerClan : null; return c != null ? c.Kingdom : null; }

        private static void Pend(Kingdom k, long x)
        {
            if (k == null || x <= 0 || k.StringId == null) return;
            long p; _pend.TryGetValue(k.StringId, out p); _pend[k.StringId] = p + x;
        }

        /// <summary>Wies -> skarbiec korony (udzial W4): ChangeGold wsi, potem skarbiec DOKLADNIE tyle, ile zeszlo z kiesy. Zwraca zdjete z kiesy; `received` - przyrost
        /// skarbca (zmierzony; kontrola linii). `zc` = zglos do Dostatku ZC0 jako danine wsi (DailyLife.NoteVillageLevy). Recenzja calosci 17/18.10: udzial korony od
        /// czesci wsi markietanow i od noclegu w zajezdzie (WarShareSutlers) - BEZ zgloszenia: ZC0 nie widzi tych wplywow do kiesy wsi (gotowka wsi ZC0 = tylko okno
        /// powrotu taboru, NoteVillageReturn), wiec odejmowal sam udzial od niewidzianego dochodu (zubozenie zamiast wplywu). Udzial przy wozie - jak dotad (utarg brutto w oknie).</summary>
        private static long ToCrown(Village v, Kingdom k, long want, out long received, bool zc = true)
        {
            received = 0;
            if (v == null || k == null || want <= 0) return 0;
            long ask = Math.Min(want, Math.Max(0, (long)v.Gold));
            if (ask <= 0) return 0;
            int had = v.Gold;
            v.ChangeGold(-(int)Math.Min(int.MaxValue, ask));
            long got = Math.Max(0, (long)had - v.Gold);
            if (got <= 0) return 0;
            try { int w0 = k.KingdomBudgetWallet; k.KingdomBudgetWallet += (int)got; received = (long)k.KingdomBudgetWallet - w0; }
            catch (Exception e) { v.ChangeGold((int)got); Stumble("ToCrown(skarbiec)", e); received = 0; return 0; }   // wyjatek przy wplacie - zloto wraca do wsi (nic w nicosc)
            Pend(k, got);
            if (zc) { try { DailyLife.NoteVillageLevy(v.Settlement, (int)got); } catch { } }   // ZC0: zloto korony z kiesy wsi do Dostatku (jak danina, tylko licznik)
            return got;
        }

        // ============================================================ 0. przy wozie
        /// <summary>
        /// VillageTakings.EnterPostfix (po 112 i F4b, w oknie powrotu wozu do wsi macierzystej): `part` - przyrost kiesy wsi w oknie (czesc wsi utargu), `handed` -
        /// gotowka oddana przez woz. Dziesiecina 5% handed (najwyzej part), potem udzial korony g/10 x Ramp od (part - dziesiecina). Przesuniecie okna ksiegi pieniadza
        /// (MoneyLedger.CartDuesInWindow - "kiesa wsi z utargu" brutto) i pozycja "daniny wsi przy wozie (DS)" w "Przeplywy osad (przyczyny)". Nie rzuca.
        /// </summary>
        internal static void DuesAtCart(MobileParty cart, Village home, long part, int handed, long works = 0, long side = 0)
        {
            try
            {
                if (home == null || handed <= 0) return;
                var st = home.Settlement;
                if (!CartDuesOn(st)) return;
                _cN++; _cHanded += handed;
                if (part <= 0) { _cNoPart++; return; }
                // recenzja calosci (noc 17/18.10): wyplata prac okregu (DistrictWorks - licznik pana + kiesa wsi, przed ta danina) osobno, nie jako udzial pana. Dziesiecina zostaje
                // od calego utargu `handed` (jak dla udzialu pana: placi wies, najwyzej `part`), udzial korony W4 - od czesci wsi netto - te same reguly dla pana i inwestora
                long wk = Math.Max(0, Math.Min(works, (long)handed));
                // przeglad calosci 20.10: udzial wioski pobocznej gracza (SideVillageFief.AtCart) osobno, nie jako udzial pana; r4-wioska: przy wozie zawsze 0 (dochod co dobe z licznika pana - SideVillageFief.Pay)
                long sd = Math.Max(0, Math.Min(side, Math.Max(0, (long)handed - part - wk)));
                _cPart += part; _cWorks += wk; _cSide += sd; _cLord += Math.Max(0, handed - part - wk - sd);
                // W2 dziesiecina: polowa jalmuzna zostaje w kiesie, budowa septow - z kiesy do ludu i kasy miasta targowego
                long alms, wage, mat, tOut, tIn; int state;
                long tithe = TownFolk.TitheVillageAtCart(home, handed, part, out alms, out wage, out mat, out tOut, out tIn, out state);
                if (state == 4) _cTiNoList++;
                if (tithe > 0)
                {
                    _cTithe += tithe;
                    if (state == 1) { _cTiNoPriest++; _cTiStay += tithe; }
                    else
                    {
                        _cAlms += alms; _cWage += wage; _cMat += mat;
                        long stay = tithe - alms - wage - mat;
                        if (stay > 0) _cTiStay += stay;
                        if (state == 2) _cTiNoMarket++;
                    }
                }
                long outV = tOut;
                _payOut += tOut; _payIn += tIn;
                // W4 udzial korony przy wozie
                var k = KingdomOf(st);
                double g = G(k);
                long crown = 0;
                bool dsOn = DsTowns.On;
                int why = g > 0 || dsOn ? Remit(home, true, true) : 0;
                if (why == 0 && k != null && dsOn) DsSeason.NoteVillageBase(k, Math.Max(0, part - tithe) * WarShare(Settings.Current) * Ramp);   // DS F3: plon W4 na jednostke g (prognoza nadania)
                if (g <= 0) _cPeaceN++;
                else
                {
                    if (why == 1) _cRemitRaid++; else if (why == 2) _cRemitSiege++; else if (why == 3) _cRemitFam++;
                    if (why == 0)
                    {
                        double rate = g * WarShare(Settings.Current) * Ramp;
                        long baseC = Math.Max(0, part - tithe);
                        // ds-akumulator (recenzja): budowa septu zlecona przy wozie (DsWorksRealMaterial - zloto czeka w kiesie do wykonania) liczy sie jak juz wydana
                        long queued = state == 3 && TownFolk.DaWorksOn ? Math.Max(0, tithe - alms) : 0;
                        long want = Math.Min((long)Math.Floor(baseC * rate), Math.Max(0, part - tOut - queued));   // najwyzej to, co z czesci wsi zostalo w kiesie
                        long wIn;
                        crown = ToCrown(home, k, want, out wIn);
                        _cCrown += crown; _payOut += crown; _payIn += wIn;
                        outV += crown;
                    }
                }
                if (outV > 0)
                {
                    int o = (int)Math.Min(int.MaxValue, outV);
                    MoneyLedger.CartDuesInWindow(cart, o);                                     // okno powrotu: kiesa wsi z utargu brutto (daniny nie sa "zniklo")
                    MoneyLedger.Note169(MoneyLedger.N169Dues, st, -o);                         // kiesa wsi poza tickiem - nazwane w "Przeplywy osad (przyczyny)"
                }
            }
            catch (Exception e) { Stumble("DuesAtCart", e); }
        }

        /// <summary>Sutlers.One (blok doby, po wplacie czesci wsi markietanow do kiesy): udzial korony g/10 x Ramp od tej czesci. Zwraca przelane. Nie rzuca.
        /// sw-zajazdy: `inn` = nocleg w zajezdzie przy szlaku (MenPurse.InnPayVillage, TownInns.RoadHeroPays - poza blokiem doby, z menu) - ta sama regula i te same
        /// liczniki markietanow (kontrola NDS-14e bez zmian), do tego osobny licznik "w tym zajazdy" w linii doby (recenzja: pieniadz zajazdow nazwany).</summary>
        internal static int WarShareSutlers(Village v, int purse, bool inn = false)
        {
            try
            {
                if (v == null || purse <= 0) return 0;
                var st = v.Settlement;
                if (!SutlerDuesOn(st)) return 0;
                var k = KingdomOf(st);
                double g = G(k);
                bool dsOn = DsTowns.On;
                bool remit = (g > 0 || dsOn) && Remit(v, true, true) != 0;
                if (!remit && k != null && dsOn) DsSeason.NoteVillageBase(k, purse * WarShare(Settings.Current) * Ramp);   // DS F3: plon W4 na jednostke g (prognoza nadania)
                if (g <= 0) return 0;
                _sBase += purse;
                if (inn) _iBase += purse;
                if (remit) { _sRemit++; return 0; }
                long want = (long)Math.Floor(purse * g * WarShare(Settings.Current) * Ramp);
                long wIn;
                long got = ToCrown(v, k, Math.Min(want, purse), out wIn, false);   // recenzja calosci: bez NoteVillageLevy (ZC0 nie widzi tego wplywu do kiesy)
                if (got > 0) { _sN++; _sCrown += got; _payOut += got; _payIn += wIn; }
                if (got > 0 && inn) { _iN++; _iCrown += got; }
                return (int)got;
            }
            catch (Exception e) { Stumble("WarShareSutlers", e); return 0; }
        }

        /// <summary>KingdomTreasury.Levies (po wyczyszczeniu LeviesToday, przed CrownIncome.Begin): W4 wplacone do skarbcow od ostatniego Levies - do wplywu dnia krolestwa
        /// (zloto juz w skarbcu; tu tylko licznik wplywow 165).</summary>
        internal static void FlushCrown(Dictionary<Kingdom, long> levies)
        {
            try
            {
                if (_pend.Count == 0 || levies == null) return;
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.StringId == null) continue;
                    long x;
                    if (!_pend.TryGetValue(k.StringId, out x) || x <= 0) continue;
                    long lk; levies.TryGetValue(k, out lk); levies[k] = lk + x;
                    _flushed += x;
                    DsSeason.NoteW4(k, x);                                          // DS F3 (K3): W4 tej doby - garnek W5 i splata zaliczek mieszczan
                }
                _pend.Clear();                                                   // krolestwo zniklo - zloto zostaje w jego skarbcu, licznik wplywu przepada (tylko licznik)
            }
            catch (Exception e) { Stumble("FlushCrown", e); }
        }

        // ============================================================ 1. czynsz staly i zaleglosci (PopulationLaw.Daily)
        /// <summary>
        /// PopulationLaw.Daily, wies DS (oplata stala): `due` - rata dnia po dekrecie, `gold` - kiesa wsi. Oblezenie miasta targowego albo glod (DsRentRemitRaided) - rata
        /// darowana, zaleglosc czeka. Inaczej (recenzja 17.10): 1. rata dnia pierwsza, najwyzej kiesa (jak przed DS); 2. zaleglosc - rata zaleglosci (1/DsRentArrearsPayDays,
        /// najmniej rata dnia) tylko z kiesy PONAD koszyk dnia (zaleglosc nigdy nie zabiera chleba - projekt 3.3); 3. niezaplacona rata dnia do ksiegi x Ramp (DsRentArrears;
        /// przy rampie 0 i przy wylaczonej ksiedze przepada jak przed DS - licznik "przepadlo"). Zaleglosc ponad DsRentForgiveDays rat - darowana ("decasus").
        /// Zwraca kwote do zaplaty teraz (wolajacy przelewa ja panu). Nie rzuca.
        /// </summary>
        internal static int RentPay(Settlement st, float due, int gold)
        {
            try
            {
                var s = Settings.Current;
                if (st == null || st.StringId == null || s == null) return 0;
                double d = float.IsNaN(due) || float.IsInfinity(due) || due < 0 ? 0 : due;
                _rN++; _rDue += d;
                int why = s.DsRentRemitRaided ? Remit(st.Village, false, true) : 0;
                if (why != 0)
                {
                    _rRemit += d; _rRemitN++;
                    if (why == 2) _rRemitSiege++; else _rRemitFam++;
                    Arr r0; if (_arr.TryGetValue(st.StringId, out r0)) r0.LastDue = d;
                    return 0;
                }
                Arr a = null; double owed = 0;
                if (s.DsRentArrears && _arr.TryGetValue(st.StringId, out a)) owed = Math.Max(0, a.Owed);
                long g = Math.Max(0, (long)gold);
                long payDay = (long)Math.Max(0, Math.Min(Math.Floor(d), (double)g));          // 1. rata dnia - pierwsza, najwyzej kiesa
                long payArr = 0;
                if (owed >= 1)
                {
                    // 2. rata zaleglosci tylko z kiesy ponad koszyk dnia (TownFolk.Villages kupuje go po czynszu)
                    double nv, r1;
                    double basket = TownFolk.DsVillageDay(st, out nv, out r1) ? Math.Max(0, VillNeeds.ChestBasket(st, nv)) : 0;   // r4-bkwies (M9): przy KS koszyk z Planu
                    int pd = ArrPayDays(s);
                    double inst = pd > 0 ? Math.Max(Math.Max(1.0, d), owed / pd) : owed;
                    double room = g - payDay - basket;
                    payArr = (long)Math.Floor(Math.Max(0, Math.Min(Math.Min(owed, inst), room)));
                }
                long pay = payDay + payArr;
                _rPaid += pay; _rArrPaid += payArr;
                double shortDay = Math.Max(0, d - payDay);
                if (!s.DsRentArrears) { _rLost += shortDay; return (int)Math.Min(int.MaxValue, pay); }
                // 3. ksiega zaleglosci w rampie (krytyczne recenzji: przy rampie 0 stare zasady - niezaplacona rata przepada)
                double toBook = shortDay * Ramp;
                _rNewArr += toBook; _rLost += shortDay - toBook;
                double left = owed - payArr + toBook;
                // ulamek zlotowki (rata po dekrecie, wyplata w calych) przechodzi na nastepna dobe w tej samej ksiedze - zaleglosc "prawdziwa" od 1 zl
                if (left > 1e-6)
                {
                    int today = Today;
                    if (a == null) { a = new Arr { Since = today }; _arr[st.StringId] = a; }
                    else if (owed < 1 || a.Since < 0) a.Since = today;
                    a.Owed = left; a.LastDue = d;
                    int fd = ForgiveDays(s);
                    if (fd > 0 && a.Owed >= 1)
                    {
                        // FIFO: najstarsza zaleglosc splacana pierwsza - czesc starsza niz fd dni to wszystko ponad fd rat dnia
                        double cap = d > 0 ? fd * d : (today - a.Since > fd ? 0 : a.Owed);
                        if (a.Owed > cap) { _rForgiven += a.Owed - cap; _rForgivenN++; a.Owed = cap; }
                    }
                    if (a.Owed <= 1e-6) _arr.Remove(st.StringId);
                }
                else if (a != null) _arr.Remove(st.StringId);
                return (int)Math.Min(int.MaxValue, pay);
            }
            catch (Exception e) { Stumble("RentPay", e); return 0; }
        }

        /// <summary>PopulationLaw.Daily: wies DS spladrowana albo w rabunku - rata dnia darowana (bez zaleglosci; wies i tak nie placila). Tylko licznik: nalezna i wstrzymana
        /// (recenzja 17.10: bez "nalezny" bramka NDS-6c zanizala mianownik).</summary>
        internal static void RentRemit(Settlement st)
        {
            try
            {
                if (!On(st)) return;
                double nv, rent;
                double d = TownFolk.DsVillageDay(st, out nv, out rent) ? rent * Decree(st) : 0;
                _rN++; _rDue += d;
                _rRemit += d; _rRemitN++; _rRemitRaid++;
            }
            catch (Exception e) { Stumble("RentRemit", e); }
        }

        // ============================================================ 4-5. skrzynia i zbytki z nadwyzki (TownFolk.Villages)
        /// <summary>TownFolk.Villages (wies DS, po koszyku dnia): budzet zbytkow tej doby = Ramp x max(0, kiesa - skrzynia) / DsSurplusSpendDays; czesc DsSurplusBkShare
        /// odlozona na najblizszy tick rynku BK wsi (BkSeal34; bez latki albo wylaczony, albo wies bez ticku rynku BK w ostatnich 2 dobach - bez budynku targu BK, nie Normal -
        /// calosc na P5; model 17.10: rynek BK kupuje dziennie w ok. 65 z 571 wsi, reszta zostawialaby polowe kroku 5 w kiesie na zawsze). Recenzja 17.10: nowy budzet BK niewydany
        /// w ostatnim ticku (brak towaru z nadwyzka podazy) dochodzi dzis do czesci P5 (najwyzej kiesa ponad skrzynie) - krok 5 wydaje caly budzet, nie polowe.
        /// Zwraca czesc P5 do wydania teraz (TownFolk.DsSurplus: lista dostatku, lista wsi, reszta - roboty gminy DsCommunalWorks).</summary>
        internal static double SurplusDay(Settlement st, int gold, double chest)
        {
            try
            {
                var s = Settings.Current;
                if (st == null || st.StringId == null || s == null) return 0;
                double over = Math.Max(0, gold - chest);
                _kVill++; _kGold += Math.Max(0, gold); _kChest += chest; _kOver += over;
                AkNote(st, gold, chest);   // ds-akumulator: skrzynia wsi tej doby (danina od skarbca wsi) i mediany kies / skrzyn (NDS-3c) - tylko liczniki
                int sd = SpendDays(s);
                double ramp = Ramp;
                int carry;
                if (_bkLeft.TryGetValue(st.StringId, out carry)) _bkLeft.Remove(st.StringId); else carry = 0;
                if (sd <= 0 || ramp <= 0 || over < 1) { _bk.Remove(st.StringId); return 0; }
                double budget = ramp * over / sd;
                double bkShare = Unit(s.DsSurplusBkShare);
                if (!BkSeal34.VillageHooked || !BkSeal34.VillageOn) bkShare = 0;
                else if (bkShare > 0)
                {
                    int tick;
                    if (!_bkTick.TryGetValue(st.StringId, out tick) || Today - tick > 2) { _kBkNoMktN++; _kBkNoMkt += (long)Math.Floor(budget * bkShare); bkShare = 0; }
                }
                double bk = Math.Floor(budget * bkShare), p5 = budget - bk;
                double cy = Math.Floor(Math.Min(Math.Max(0, carry), Math.Max(0, over - budget)));
                if (cy >= 1) { p5 += cy; _kBkCarry += (long)cy; _kBkCarryN++; }
                p5 -= SpikeTake(st.StringId, p5);                                // m105 gosp4 G2: skok ceny chleba z nadwyzki byl pierwszy - P5 mniej o to samo (bez podwojnego wydatku)
                if (bk >= 1) _bk[st.StringId] = (int)Math.Min(int.MaxValue, bk); else _bk.Remove(st.StringId);
                _kBudget += budget; _kBkPlan += bk; _kP5Plan += p5;
                return p5;
            }
            catch (Exception e) { Stumble("SurplusDay", e); return 0; }
        }

        /// <summary>m105 gosp4 G2 (TownFolk.Zc4BuyCore, klucz Ks Village Grain Spike From Surplus): wies DS zaplacila `zl` skoku ceny ziarna i soli (ponad cene Planu) z kiesy
        /// ponad skrzynie - w tempie kroku 5 (lam x rampa x (kiesa - skrzynia) / DsSurplusSpendDays). Krok 5 tej i nastepnych dob wydaje o tyle mniej (SurplusDay - SpikeTake).
        /// Nie rzuca.</summary>
        internal static void SpikeNote(Settlement st, double zl)
        {
            try
            {
                if (st == null || st.StringId == null || !(zl > 0) || double.IsInfinity(zl)) return;
                double o; _spikeOwed.TryGetValue(st.StringId, out o);
                _spikeOwed[st.StringId] = o + zl;
            }
            catch (Exception e) { Stumble("SpikeNote", e); }
        }

        /// <summary>m105 gosp4 G2 (SurplusDay): czesc P5 doby zajeta przez skok ceny chleba juz zaplacony z nadwyzki (najwyzej P5; reszta czeka na nastepne doby). Wolajacy w try.</summary>
        private static double SpikeTake(string id, double p5)
        {
            double o;
            if (id == null || _spikeOwed.Count == 0 || !_spikeOwed.TryGetValue(id, out o)) return 0;
            double t = Math.Min(Math.Max(0, p5), Math.Max(0, o));
            o -= t;
            if (o < 0.5) _spikeOwed.Remove(id); else _spikeOwed[id] = o;
            if (t > 0) { _kSpike += t; _kSpikeN++; }
            return t;
        }

        /// <summary>m105 gosp4 G2 (TownFolk.Zc4BuyCore): tempo kroku 5 - DsSurplusSpendDays (0 - wies nie wydaje nadwyzki, skoku ceny z nadwyzki nie ma).</summary>
        internal static int SpendDaysNow { get { return SpendDays(Settings.Current); } }

        /// <summary>m105 gosp4 G4 (TownFolk.Zc4BuyCore): DsVillageChestDays (0 - bez skrzyni).</summary>
        internal static int ChestDaysNow { get { return ChestDays(Settings.Current); } }

        /// <summary>TownFolk.Villages: zaplacone z czesci P5 kroku 5 - towar (lista dostatku i lista wsi, `goods`) i roboty gminy (`works` = place `wage` + material `mat`);
        /// `noWorks` - czesc niewydana (bez towaru i bez robot: klucz wylaczony, miasto targowe bez kiesy ludu).</summary>
        internal static void NoteP5(long goods, long works, long wage, long mat, double noWorks)
        {
            if (goods > 0) { _kP5 += goods; _kGoods += goods; }
            if (works > 0) { _kP5 += works; _kWorks += works; _kWorksWage += wage; _kWorksMat += mat; _kWorksN++; }
            if (noWorks >= 1) { _kWorksNo += noWorks; _kWorksNoN++; }
        }

        /// <summary>r4-bkwies (recenzja): TownFolk.DsSurplus - czesc P5, ktora ZOSTAJE W KIESIE wsi, bo potrzeba pokryta (krok 5 do potrzeby - Ds Surplus Need Limit, wies
        /// z koszykiem ZC4 - Ks Surplus Skips Basket, plotno zatrzymane progiem K2); P5 = towar + roboty gminy + niewydane + zostalo w kiesie.</summary>
        internal static void NoteP5Kept(double kept) { if (kept >= 1 && !double.IsInfinity(kept)) { _kKept += kept; _kKeptN++; } }
        // ============================================================ ds-akumulator (noc 17/18.10): miary kies i skrzyn, roboty gminy po budowach
        private static readonly Dictionary<string, double> _akChest = new Dictionary<string, double>();   // id wsi -> skrzynia tej doby (SurplusDay)
        private static readonly List<double> _akGold = new List<double>(), _akCh = new List<double>(), _akRat = new List<double>();
        private static int _akDay = -1, _akPoor;

        /// <summary>m105 gosp4 (linia "Akumulator kies", M5G-7): wsie DS z dzisiejszego kroku 4 z kiesa ponizej min(1000 zl, skrzynia) i wszystkie wsie kroku 4; false - doby nie bylo.</summary>
        internal static bool AkPoor(out int poor, out int n)
        {
            poor = 0; n = 0;
            if (_akDay != Today || _akGold.Count == 0) return false;
            poor = _akPoor; n = _akGold.Count;
            return true;
        }

        private static void AkNote(Settlement st, int gold, double chest)
        {
            try
            {
                int today = Today;
                if (today != _akDay) { _akDay = today; _akChest.Clear(); _akGold.Clear(); _akCh.Clear(); _akRat.Clear(); _akPoor = 0; }
                if (st == null || st.StringId == null) return;
                _akChest[st.StringId] = chest;
                _akGold.Add(Math.Max(0, gold)); _akCh.Add(Math.Max(0, chest));
                if (chest >= 1) _akRat.Add(Math.Max(0, gold) / chest);
                if (gold < Math.Min(1000.0, chest)) _akPoor++;                   // m105 gosp4 (M5G-7): wies biedna - kiesa ponizej 1000 zl i ponizej skrzyni (tylko licznik)
            }
            catch (Exception e) { Stumble("AkNote", e); }
        }

        /// <summary>TownFolk (ds-akumulator): skrzynia wsi policzona dzis w kroku 4 (SurplusDay); -1 - nie liczona dzis.</summary>
        internal static double ChestToday(Settlement st)
        {
            double c;
            return st != null && st.StringId != null && _akDay == Today && _akChest.TryGetValue(st.StringId, out c) ? c : -1;
        }

        private static double Med(List<double> v)
        {
            if (v == null || v.Count == 0) return double.NaN;
            var a = v.ToArray(); Array.Sort(a);
            int n = a.Length;
            return n % 2 == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        /// <summary>TownFolk (linia "Akumulator kies"): mediana kiesy wsi DS, mediana skrzyni i mediana kiesa / skrzynia z dzisiejszego kroku 4; false - doby nie bylo.</summary>
        internal static bool AkMedians(out int n, out double medGold, out double medChest, out double medRatio, out double sumGold, out double sumChest)
        {
            n = 0; medGold = medChest = medRatio = double.NaN; sumGold = sumChest = 0;
            try
            {
                if (_akDay != Today || _akGold.Count == 0) return false;
                n = _akGold.Count; medGold = Med(_akGold); medChest = Med(_akCh); medRatio = Med(_akRat);
                foreach (var x in _akGold) sumGold += x;
                foreach (var x in _akCh) sumChest += x;
                return true;
            }
            catch (Exception e) { Stumble("AkMedians", e); return false; }
        }

        /// <summary>TownFolk.DaWorksRun (DsWorksRealMaterial): roboty gminy wykonane po budowach tej doby - towar z polki (`mat`) i rzemieslnicy miasta (`wage`); w kroku 5 byly
        /// liczone jako niewydane (zlecenie) - tu przechodza do "roboty gminy" linii DS.</summary>
        internal static void NoteWorksLate(long mat, long wage)
        {
            long works = Math.Max(0, mat) + Math.Max(0, wage);
            if (works <= 0) return;
            _kP5 += works; _kWorks += works; _kWorksWage += Math.Max(0, wage); _kWorksMat += Math.Max(0, mat); _kWorksN++;
            _kWorksNo = Math.Max(0, _kWorksNo - works);
        }

        /// <summary>DsSeason.TreasuryForRefund przy samym F1 (bez firm F3): brak zwrotu pokryty ze skarbca ponad zapas wojny - licznik linii.</summary>
        internal static void NoteK5(long x) { if (x > 0) { _k5Treas += x; _k5N++; } }

        /// <summary>TownFolk.Villages: stare wydatki z nadwyzki wsi DS w rampie (15%/d na stara liste, stara drabina P5) - liczniki "chciane".</summary>
        internal static void NoteOldExtras(double extra, double ladder) { if (extra > 0) _kOldExtra += extra; if (ladder > 0) _kOldLadder += ladder; }

        /// <summary>
        /// BkSeal34.RunVillage (tick BK wsi DS): zwraca STARY pulap BK na sztuke = (1 - Ramp) x 5% kiesy (jak w BK - nie maleje w petli; w rampie slabnie liniowo, przy
        /// rampie 0 rynek kupuje co do sztuki jak przed DS), `fresh` - NOWY budzet = odlozona czesc kroku 5 (maleje z kazdym zakupem). Zapisuje dobe ticku (SurplusDay).
        /// </summary>
        internal static int BkBudget(Settlement st, int gold, out int fresh)
        {
            fresh = 0;
            try
            {
                double old = (1.0 - Ramp) * Math.Max(0, gold) * BkOldShare;
                int nw = 0;
                if (st != null && st.StringId != null)
                {
                    if (_bk.TryGetValue(st.StringId, out nw)) _bk.Remove(st.StringId);
                    _bkTick[st.StringId] = Today;
                }
                fresh = Math.Max(0, nw);
                _kBkTicks++; _kBkOldPlan += (long)old; _kBkNewPlan += fresh;
                return (int)Math.Min(int.MaxValue, Math.Floor(old));
            }
            catch (Exception e) { Stumble("BkBudget", e); fresh = 0; return 0; }
        }

        /// <summary>BkSeal34.RunVillage: zakupy wsi DS na rynku BK - razem i w tym w starym pulapie na sztuke (oczekiwane 0 po rampie); `freshLeft` - nowy budzet niewydany
        /// (jutro do czesci P5 kroku 5 - SurplusDay).</summary>
        internal static void NoteBkSpent(Settlement st, long spent, long oldCap, int freshLeft)
        {
            if (spent > 0) _kBkSpent += spent;
            if (oldCap > 0) _kBkOldSpent += oldCap;
            try
            {
                if (st == null || st.StringId == null) return;
                if (freshLeft >= 1) { _bkLeft[st.StringId] = freshLeft; _kBkLeft += freshLeft; }
                else _bkLeft.Remove(st.StringId);
            }
            catch (Exception e) { Stumble("NoteBkSpent", e); }
        }

        // ============================================================ stare pobory wsi DS w rampie (oczekiwane 0 po rampie)
        /// <summary>KingdomTreasury.Levies (galaz wsi): danina korony od kiesy ponad 15 dni zakupow - wies DS x (1 - Ramp). Zwraca kwote po rampie.</summary>
        internal static int OldLevy(Settlement st, int x)
        {
            if (x <= 0 || !On(st)) return x;
            int y = (int)Math.Floor(x * (1.0 - Ramp));
            if (y > 0) { _oLevy += y; _oLevyN++; }
            return Math.Max(0, y);
        }

        /// <summary>TownFolk.PayHoardVillage: podatek od nadwyzki wsi DS x (1 - Ramp). Zwraca kwote po rampie.</summary>
        internal static long OldHoard(Settlement st, long x)
        {
            if (x <= 0 || !On(st)) return x;
            long y = (long)Math.Floor(x * (1.0 - Ramp));
            return Math.Max(0, y);
        }
        internal static void NoteOldHoard(long took) { if (took > 0) { _oHoard += took; _oHoardN++; } }

        /// <summary>KingdomTreasury.LvSources: nadwyzka wsi DS jako zrodlo W7 x (1 - Ramp) (po rampie 0 - wies DS nie jest zrodlem).</summary>
        internal static double OldW7Room(Settlement st, double room)
        {
            if (room <= 0 || !On(st)) return room;
            return Math.Floor(room * (1.0 - Ramp));
        }
        internal static void NoteOldW7(Settlement st, long took) { if (took > 0 && On(st)) { _oW7 += took; _oW7N++; } }

        /// <summary>KingdomTreasury.LvKingdom: potrzeba W7 rodu pomniejszona o czesc wsi DS (bez przerzucania na lud miast) - licznik linii.</summary>
        internal static void NoteW7Cut(long x) { if (x > 0) { _oW7Cut += x; _oW7CutN++; } }

        // ============================================================ linia doby
        private static string N0(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v).ToString("0", Inv); }

        /// <summary>ArmouryBehavior (koniec bloku doby, po ksiedze pieniadza): linia "Daniny wsi (DS)" za okno od poprzedniej linii; liczniki od zera. Klucz wylaczony -
        /// DayOn od nowa (ponowne wlaczenie = nowa rampa), linia tylko przy zaleglosciach w zapisie (raz).</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                int day = (int)CampaignTime.Now.ToDays - 1;
                bool on = s != null && Key && TownFolk.Live;
                if (!on)
                {
                    if (_dayOn >= 0 || _startLogged)
                        Log.Info("Daniny wsi (DS): dzien " + day + " | WYLACZONE (" + (s != null && s.DuesMedieval ? "kiesa ludu nieczynna" : "klucz Dues Medieval") + ") - stare pobory wsi od tej doby; zaleglosci czynszu "
                                 + _arr.Count + " wsi zostaja w zapisie bez skutku; ponowne wlaczenie = nowa rampa.");
                    _dayOn = -1; _startLogged = false; _bk.Clear(); _bkTick.Clear(); _bkLeft.Clear();
                    ClearCounters();
                    return;
                }
                double ramp = Ramp;
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info("Daniny wsi (DS): start - pierwsza doba DS " + _dayOn + ", rampa " + Math.Max(0, Math.Min(364, s.DsRampDays)) + " dob (stare pobory wsi x (1 - rampa), nowe x rampa)"
                             + "; zaleglosci czynszu z zapisu " + _arr.Count + " wsi; bez KS (koszyk wsi = stara lista dnia).");
                }
                // nadanie g krolestw (F1: wojna 1, War Tax 1.5, pokoj 0)
                int g0 = 0, g1 = 0, g15 = 0;
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    double g = G(k);
                    if (g <= 0) g0++; else if (g < 1.25) g1++; else g15++;
                }
                // ksiega zaleglosci
                double arrSum = 0; int arrN = 0, arr91 = 0;
                foreach (var a in _arr.Values)
                {
                    if (a.Owed < 1) continue;                                   // ulamek zlotowki na nastepna dobe - nie zaleglosc
                    arrSum += a.Owed; arrN++;
                    if (a.LastDue > 0 && a.Owed > 91.0 * a.LastDue) arr91++;
                }
                long tithePriest = _cAlms + _cWage + _cMat;
                long ctl = _payOut - _payIn;
                var sb = new StringBuilder(1600);
                sb.Append("Daniny wsi (DS): dzien ").Append(day)
                  .Append(" | wozy: powrotow ").Append(_cN).Append(", oddane ").Append(_cHanded).Append(", pan i majatki (bez zmian) ").Append(_cLord).Append(", czesc wsi ").Append(_cPart)
                  .Append(" (bez przyrostu kiesy ").Append(_cNoPart).Append(")")
                  .Append(_cWorks > 0 ? ", prace okregu (inwestorom) " + _cWorks : "")
                  .Append(_cSide > 0 ? ", wioska gracza (z udzialu pana) " + _cSide : "")
                  .Append(", dziesiecina ").Append(_cTithe).Append(" (przez kaplanow ").Append(tithePriest).Append(": jalmuzna ").Append(_cAlms).Append(", septy ").Append(_cWage + _cMat)
                  .Append(" = place ludu ").Append(_cWage).Append(" + material ").Append(_cMat).Append("; zostalo w kiesach ").Append(_cTiStay)
                  .Append(" - bez kaplana ").Append(_cTiNoPriest).Append(" wozow, bez miasta targowego ").Append(_cTiNoMarket).Append("; pominieta bez listy kaplanow doby ").Append(_cTiNoList).Append(" wozow)")
                  .Append(", korona przy wozie ").Append(_cCrown).Append(" (g krolestw: 0 - ").Append(g0).Append(", 1 - ").Append(g1).Append(", 1.5 - ").Append(g15)
                  .Append("; wozow w pokoju ").Append(_cPeaceN).Append(", zwolnione: rabunek ").Append(_cRemitRaid).Append(", oblezenie ").Append(_cRemitSiege).Append(", glod ").Append(_cRemitFam).Append(")")
                  .Append(" | markietani: czesc wsi w wojnie ").Append(_sBase).Append(", korona ").Append(_sCrown).Append(" (").Append(_sN).Append(" wplat, zwolnione ").Append(_sRemit).Append(")")
                  .Append(_iBase > 0 ? ", w tym zajazdy przy szlakach (ZJ): czesc wsi " + _iBase + ", korona " + _iCrown + " (" + _iN + " wplat)" : "")   // sw-zajazdy: tylko gdy byly noclegi - bez nich linia jak dotad
                  .Append(" | czynsz: nalezny ").Append(N0(_rDue)).Append(" (").Append(_rN).Append(" wsi), zaplacony ").Append(N0(_rPaid)).Append(" (w tym zaleglosci ").Append(N0(_rArrPaid)).Append(")")
                  .Append(", nowa zaleglosc ").Append(N0(_rNewArr)).Append(", zaleglosci ").Append(N0(arrSum)).Append(" u ").Append(arrN).Append(" wsi (ponad 91 rat ").Append(arr91).Append(")")
                  .Append(", darowane po roku ").Append(N0(_rForgiven)).Append(" (").Append(_rForgivenN).Append(" wsi), wstrzymane ").Append(N0(_rRemit)).Append(" (").Append(_rRemitN)
                  .Append(": rabunek ").Append(_rRemitRaid).Append(", oblezenie ").Append(_rRemitSiege).Append(", glod ").Append(_rRemitFam).Append(")")
                  .Append(", przepadlo (" + (s.DsRentArrears ? "rampa ksiegi" : "bez ksiegi") + ") ").Append(N0(_rLost))
                  .Append(" | skrzynia: wsi ").Append(_kVill).Append(", kiesy ").Append(N0(_kGold)).Append(", cel skrzyni ").Append(N0(_kChest)).Append(", ponad skrzynia ").Append(N0(_kOver))
                  .Append(", zbytki z nadwyzki ").Append(N0(_kBudget)).Append(" (P5 ").Append(N0(_kP5Plan)).Append(" - kupione ").Append(N0(_kP5))
                  .Append(" / rynek BK ").Append(N0(_kBkPlan)).Append(" odlozone; wsi bez rynku BK (krok 5 caly na P5) ").Append(_kBkNoMktN).Append(" - ").Append(_kBkNoMkt)
                  .Append("; ticki BK wsi DS ").Append(_kBkTicks).Append(": pulap stary na sztuke ").Append(_kBkOldPlan).Append(", budzet nowy ").Append(_kBkNewPlan)
                  .Append(", kupione ").Append(_kBkSpent).Append(" (w starym pulapie ").Append(_kBkOldSpent).Append(")), rampa ").Append(ramp.ToString("0.000", Inv)).Append(" (od doby ").Append(_dayOn).Append(")")
                  .Append(" | krok 5 wydany: towar ").Append(_kGoods).Append(", roboty gminy ").Append(_kWorks).Append(" (").Append(_kWorksN).Append(" wsi: place ludu miast targowych ").Append(_kWorksWage)
                  .Append(" + material do kas ").Append(_kWorksMat).Append("), niewydane ").Append(N0(_kWorksNo)).Append(" (").Append(_kWorksNoN).Append(" wsi)")
                  .Append(_kKeptN > 0 ? ", zostalo w kiesie (potrzeba pokryta) " + N0(_kKept) + " (" + _kKeptN + " wsi)" : "")
                  .Append("; budzet BK niewydany w ticku ").Append(_kBkLeft).Append(", z poprzedniego ticku do P5 ").Append(_kBkCarry).Append(" (").Append(_kBkCarryN).Append(" wsi)")
                  .Append(" | stare pobory wsi DS (oczekiwane 0 po rampie): danina ").Append(_oLevy).Append(" (").Append(_oLevyN).Append(" wsi), podatek od nadwyzki ").Append(_oHoard).Append(" (").Append(_oHoardN)
                  .Append(" wsi), W7 ").Append(_oW7).Append(" (").Append(_oW7N).Append(" zrodel), 15%/d nadwyzki na stara liste (chciane) ").Append(N0(_kOldExtra)).Append(", stara drabina P5 (chciane) ").Append(N0(_kOldLadder))
                  .Append(" | W7: potrzeba rodow pomniejszona o czesc wsi DS ").Append(_oW7Cut).Append(" (").Append(_oW7CutN).Append(" rodow)")
                  .Append(" | korona: W4 do wplywu dnia (LeviesToday) ").Append(_flushed).Append(", czeka w skarbcach do nastepnego Levies ").Append(PendSum())
                  .Append("; brak zwrotu zoldu ze skarbca ponad zapas wojny (K5, bez firm F3) ").Append(_k5Treas).Append(" (").Append(_k5N).Append(" krolestw)");
                // m105 gosp4 (G2 krok 5 mniej o skok ceny chleba, G5 czynsz po chlebie) - tylko przy ktoryms kluczu (oba NIE - linia jak w WGRANIU 102)
                bool g2 = StartCalibration.SwGrainSpike, g5 = s.DsRentAfterBread;
                if (g2 || g5)
                {
                    double spOwed = 0; foreach (var x in _spikeOwed.Values) spOwed += x;
                    sb.Append(" | m105 gosp4 (Grain Spike From Surplus ").Append(g2 ? "TAK" : "NIE").Append(", Rent After Bread ").Append(g5 && s.DsRentArrears ? "TAK" : "NIE")
                      .Append("): krok 5 mniej o skok ceny chleba ").Append(N0(_kSpike)).Append(" zl (").Append(_kSpikeN).Append(" wsi), czeka na nastepne doby ").Append(N0(spOwed))
                      .Append(" zl (").Append(_spikeOwed.Count).Append(" wsi); czynsz po chlebie - rata dnia do zaleglosci ").Append(N0(_rBread)).Append(" zl (").Append(_rBreadN).Append(" wsi)");
                }
                sb.Append(" | kontrola: platnicy - odbiorcy = ").Append(ctl);
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (w sesji ").Append(_stumblesSess).Append(")");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearCounters(); }
        }

        private static long PendSum() { long t = 0; foreach (var v in _pend.Values) t += v; return t; }

        // ============================================================ zapis arm_daniny
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('~') >= 0; }

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(64 + _arr.Count * 40);
                sb.Append("1|r~").Append(_dayOn.ToString(Inv));
                foreach (var kv in _arr)
                {
                    var a = kv.Value;
                    if (a == null || BadId(kv.Key) || !(a.Owed >= 0.01) || double.IsInfinity(a.Owed)) continue;
                    sb.Append("|v~").Append(kv.Key).Append('~').Append(Math.Round(a.Owed, 3).ToString("R", Inv)).Append('~').Append(a.Since.ToString(Inv));
                }
                foreach (var kv in _pend)
                {
                    if (BadId(kv.Key) || kv.Value <= 0) continue;
                    sb.Append("|c~").Append(kv.Key).Append('~').Append(kv.Value.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset): brak klucza (stary zapis) albo zly format - zera (rampa od pierwszej doby z kluczem).</summary>
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
                        int n; long l; double d;
                        if (a[0] == "r" && int.TryParse(a[1], NumberStyles.Integer, Inv, out n)) _dayOn = n < 0 ? -1 : n;
                        else if (a[0] == "v" && a.Length >= 3 && !BadId(a[1]) && double.TryParse(a[2], NumberStyles.Float, Inv, out d) && d >= 0.01 && !double.IsInfinity(d))
                        {
                            int since = -1;
                            if (a.Length >= 4 && int.TryParse(a[3], NumberStyles.Integer, Inv, out n)) since = n;
                            _arr[a[1]] = new Arr { Owed = d, Since = since };
                        }
                        else if (a[0] == "c" && a.Length >= 3 && !BadId(a[1]) && long.TryParse(a[2], NumberStyles.Integer, Inv, out l) && l > 0) _pend[a[1]] = l;
                    }
                    catch (Exception e) { Stumble("Import(wpis)", e); }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
