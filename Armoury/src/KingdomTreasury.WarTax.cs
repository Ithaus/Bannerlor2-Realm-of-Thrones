using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E5/B2 (paczka 184, PLAN 5.6) - PODATEK WOJENNY KORONY OD ZAROBKU LUDU (projekt etapu 5 v2 rozdz. 3 "E5/B2", regula R15 (b); decyzja Jeffa 09.10 C:
    /// "mniej wojska AI - NIE, podatek wojenny korony ze skutkami dla gospodarki"). Wylacznik WarTaxFromFolk (wylaczony = stan sprzed paczki: s = 0, pobor 0).
    /// Dziala tylko przy czynnej kiesie ludu (v2 K3, rozdz. 5 pkt 9) - hak B1 WtFolkLive: kiesa nieczynna = jak wylaczony klucz (s = 0, pobor 0; pierscienie
    /// potrzeby biegna dalej), wiec wlaczenie kiesy zaczyna od s = 0 i narastania o krok na tydzien. Scalenie B: hak = TownFolk.Live.
    ///
    /// DOCHODZI do daniny (v2, E5K1-01): danina wojenna z nadwyzki kas kupcow i kies wsi (Levies) i War Tax 5% podatku miast (RulingIncomePostfix) zostaja jak sa;
    /// polityka War Tax dalej x2 daniny i podnosi pulap s z WarTaxMax (3%) do WarTaxMaxWithPolicy (6%).
    ///  - POBOR: od zarobku ludu - scalenie B: raz na dobe od zarobku okna klasy w TownFolk.DailyLive krok 1, obok podatkow pana (plan B1 pkt 6 "czlon w tej
    ///    samej funkcji"; Earn zostaje sama arytmetyka) - FolkWarTax(osada, klasa, zarobek, stan kiesy): klasa W 0.5 s, R 1.0 s, P 1.5 s, podzamcze 1.0 s.
    ///    Tylko w wojnie krolestwa wlasciciela osady (stan wojny raz na dobe), tylko z 165 (potrzeba liczona z wplywow dnia 165 i kredytu 186), rod bez krolestwa - nic.
    ///    Osada oblezona nie placi (R22, bramka BesiegedPay.Holds - rodzaj KFolkWarTax). Bez czynnej kiesy ludu (B1) pobor 0 i s = 0 (K3, hak WtFolkLive).
    ///    Platnik -> odbiorca: kiesa klasy ludu osady (wolajacy zdejmuje zwrocone CALE zloto) -> Kingdom.KingdomBudgetWallet (to samo zloto; ulamek ponizej 1 zl
    ///    czeka w osadzie na nastepny zarobek - to nie pieniadz, tylko niezaplacona reszta). Wplyw dnia korony (CrownIncome.Begin - "nasze").
    ///  - STAWKA s krolestwa co 7 dob (doba gry podzielna przez 7) wedlug potrzeby z ostatnich 7 dob: kredyt 186 (CrownIncome.DayOf.LoanIn > 0) albo nalezny
    ///    zwrot wiekszy niz wplywy dnia bez 1/360 zapasu (RefundDue > Measured), albo skarbiec < zapas wojny -> +WarTaxStep (0.5 pkt);
    ///    zapas wojny = max(CrownReserveGold 500 000, WarTaxReserveDays (60) x sredni nalezny zwrot z 28 dob - f6-zwrot: pelny, przed mnoznikiem potrzeby rodu); skarbiec > 2 x zapas wojny, bez kredytu i bez
    ///    siegania do zapasu przez 7 dob i dlug 186 = 0 -> -WarTaxStep; inaczej bez zmian. Pokoj (albo 165 wylaczone) - s spada co tydzien o
    ///    max(krok, wiekszy pulap / 4), wiec do zera najwyzej w 4 tygodnie (krotki pokoj nie zeruje stawki; w pokoju pobor 0).
    ///    AI krola nie decyduje osobno - regula potrzeby jest decyzja korony (gracz-krol tak samo). Pulap pilnowany CODZIENNIE (recenzja B2): gdy spadnie miedzy
    ///    krokami (krolestwo zdejmie polityke War Tax, nizszy pulap w MCM), s od razu przyciete do pulapu - linia pokazuje stawke faktycznie pobierana.
    ///  - Zapis: szoste pole klucza 165 ("arm_crown165", CrownIncome.Export/Import; starsza wersja moda je pomija).
    ///  - Linie: "Korona: podatek wojenny ludu (184)" (po rentach 180, przed CrownIncome.End), pozycja w "Korona: wplywy dnia (165)" i w "Obieg" (korona).
    ///  - W5b REGULA 1 (decyzja Jeffa 13.10 "zrob poprawke daniny") - BRAK WOJSKA U PANOW: potrzeba korony wyzej liczy sie tylko z rachunkow korony, wiec
    ///    krolestwo, ktorego panowie nie maja budzetu na druzyny, placi maly zwrot zoldu, "nie ma potrzeby", s = 0 i nie ma daniny P1 ani doplaty W5 (bledne
    ///    kolo; Zelazne Wyspy w dobie 364 biegu rocznego: skarbiec 1 046 588 przy zapasie wojny 500 000, s = 0, druzyny 1.6 tys. z 8.1 tys. limitu).
    ///    Wylacznik WarAidFollowsLordsShortfall (czynny tylko z danina P1 i doplata W5; wylaczony = stawka daniny idzie za samym s, jak przed paczka).
    ///    DRUGA stawka krolestwa SN (WarTaxK.SN): w kroku tygodnia rosnie o JEDEN WarTaxStep od stawki, ktora danina szla w mierzonym tygodniu (wieksza z s i
    ///    SN sprzed dzisiejszego kroku s - recenzja: nie dwa kroki naraz, gdy s samo roslo, i nie zero, gdy s spadalo), do tego samego pulapu,
    ///    gdy srednia z 7 dob NIEPOKRYTEJ potrzeby W5 (rody z warunkiem rent ponizej celu z powodu pulapu; po garnku daniny, po skarbcu ponad zapas wojny i po
    ///    tarczowym - NeedAidMeasure) > max(250 zl, WarAidLordsShortfallShare 5% sredniego naleznego zwrotu z 28 dob). Spada o krok, gdy brak przewidziany
    ///    przy nizszej stawce (potrzeba minus garnek daniny pomniejszony o ubytek po kroku) miesci sie w tym samym progu; w pokoju spada jak s.
    ///    OD SN LICZY SIE TYLKO DANINA Z OSZCZEDNOSCI (WtAidS: wieksza z s i SN - KingdomTreasury.WarAid.cs); podatek od zarobku (FolkWarTax) idzie dalej za
    ///    samym s - zarobek biednych miast nie jest ruszany (chleb swiata w biegu rocznym 97.4-98.1% przy progu 98%), a garnek W5 i tak jest ograniczony danina.
    ///    Pamiec 7 dob to PAMIEC SESJI (po wczytaniu pusta; pierwsza doba sesji nie ma pomiaru, bo budzet 166 nie jest policzony). Recenzja: krok w gore i
    ///    w dol tylko przy pomiarze z co najmniej LnMinDays (4) dob - niepokryta potrzeba chodzi z doby na dobe 2-5 razy w rytmie ok. 4 dob, srednia z 1-3
    ///    dob dawalaby falszywy krok; krok z krotszym pomiarem stawki nie rusza (stoi stan z zapisu), w pokoju spadek jak s. Sama stawka SN jest w zapisie -
    ///    29. liczba listy zwrotow szostego pola (starsza wersja moda czyta 28 i reszte pomija).
    /// Wyjatek per krolestwo / per pobor - licznik potkniec, bez globalnych wylacznikow (CLAUDE.md 7). MoneyLedger bez nowych pozycji (przelew miedzy
    /// posiadaczami spoza kas osad: kiesa ludu B1 -> skarbiec; licznik w "Obieg").
    /// </summary>
    internal static partial class KingdomTreasury
    {
        /// <summary>Klasy ludu dla poboru 184: wyrobnicy, rzemieslnicy, patrycjat (miasto), ludzie podzamcza (zamek).</summary>
        internal const int WtW = 0, WtR = 1, WtP = 2, WtCastle = 3;
        private const int WtClasses = 4;
        private static readonly double[] WtWeight = { 0.5, 1.0, 1.5, 1.0 };
        private const int WtRing7 = 7, WtRing28 = 28;
        private static readonly CultureInfo WtInv = CultureInfo.InvariantCulture;

        private sealed class WarTaxK
        {
            public double S;                                                          // stawka s (ulamek: 0.005 = 0.5%)
            public readonly bool[] Loan = new bool[WtRing7], Over = new bool[WtRing7];   // 7 dob: kredyt 186 / zwrot ponad wplywy dnia bez 1/360
            public int H7, F7;
            public readonly long[] Due = new long[WtRing28];                         // 28 dob naleznego zwrotu zoldu (f6-zwrot: pelnego - KDay.RefundFull, przed mnoznikiem potrzeby)
            public int H28, F28;
            // f23-korona (War Reserve By Need): nalezny zwrot PO mierze potrzeby rodu (KDay.RefundDue) w tych samych miejscach pierscienia co Due; FN - ile dob z DueN jest
            // w pierscieniu. Recenzja: w zapisie za lista Due (29. liczba SN zawsze, potem 28 x DueN i FN) - po wczytaniu udzial jak przed zapisem; zapis sprzed recenzji -
            // FN 0, pierwsza doba udzial 1 (zapas jak z pelnego). Due z zapisu zostaje pelny - stara DLL czyta go jak dotad
            public readonly long[] DueN = new long[WtRing28];
            public int FN;
            public string Name = "";
            // doba (linia)
            public long DTaken, DHeld; public double DMove; public bool DStep;
            // W5b regula 1 (brak wojska u panow): stawka potrzeby z tego powodu (ulamek) - liczy sie od niej TYLKO danina z oszczednosci (P1: wieksza z S i SN);
            // podatek 184 od zarobku idzie dalej za samym S. Zapis: jedna liczba dopisana za 28 zwrotami w szostym polu klucza 165 (WarTaxExport).
            public double SN;
            // pamiec 7 dob (PAMIEC SESJI, NaN = doba bez pomiaru): niepokryta potrzeba W5 po daninie, skarbcu i tarczowym; potrzeba W5; garnek daniny; danina dnia
            public readonly double[] LnU = new double[WtRing7], LnNeed = new double[WtRing7], LnPot = new double[WtRing7], LnAid = new double[WtRing7];
            public int HN;
            public WarTaxK() { for (int i = 0; i < WtRing7; i++) LnU[i] = LnNeed[i] = LnPot[i] = LnAid[i] = double.NaN; }
        }

        private static readonly Dictionary<string, WarTaxK> _wt = new Dictionary<string, WarTaxK>();       // id krolestwa -> stan
        private static readonly Dictionary<Kingdom, long> _wtWindow = new Dictionary<Kingdom, long>();      // pobrane od ostatniego CrownIncome.End (wplyw dnia 165)
        private static readonly Dictionary<Settlement, double> _wtCarry = new Dictionary<Settlement, double>();   // ulamek ponizej 1 zl na osade (niezaplacona reszta)
        private static readonly Dictionary<Kingdom, bool> _wtWar = new Dictionary<Kingdom, bool>();         // stan wojny krolestwa tej doby
        private static int _wtWarDay = int.MinValue;
        private static readonly long[] _wtCls = new long[WtClasses];
        private static long _wtHeld; private static int _wtCalls, _wtHeldN, _wtStumbles, _wtImportN = -1, _wtImportBad;
        private static readonly HashSet<string> _wtErr = new HashSet<string>();
        /// <summary>Paczka 169 ("Obieg", korona): podatek wojenny ludu pobrany od poprzedniej linii 184 (kiesy ludu -> skarbce).</summary>
        internal static long LastFolkWarTax;

        /// <summary>CrownIncome.Reset (stan zapisany w kluczu 165 - czyszczony razem z nim; konstruktor ArmouryBehavior).</summary>
        internal static void WarTaxReset()
        {
            _wt.Clear(); _wtWindow.Clear(); _wtCarry.Clear(); _wtWar.Clear(); _wtWarDay = int.MinValue;
            Array.Clear(_wtCls, 0, WtClasses); _wtHeld = 0; _wtCalls = _wtHeldN = 0; _wtStumbles = 0; _wtErr.Clear();
            _wtImportN = -1; _wtImportBad = 0; LastFolkWarTax = 0;
            WarAidReset();   // P1: danina z oszczednosci ludu (KingdomTreasury.WarAid.cs) - stan sesji czyszczony razem z 184
        }

        private static void WtStumble(string where, Exception e)
        {
            _wtStumbles++;
            try { if (_wtErr.Add(where)) Log.Error("KingdomTreasury.WarTax." + where, e); } catch { }
        }

        /// <summary>Hak B1 (v2 K3): kiesa ludu czynna - scalenie B: TownFolk.Live, jeden przelacznik kiesy z PLAN-KODU-E5B1 pkt 1 (TownFolkLive i
        /// TownFolkMode >= 1 i A1 cofa zakupy, TownPurse.NoMint), NIE sam warunek z ustawien (K3). Ta sama funkcja w LordsEvenUp.FolkLive i CourtFull.FolkShare.</summary>
        private static bool WtFolkLive { get { return TownFolk.Live; } }

        private static bool WtOn { get { var s = Settings.Current; return s != null && (s.WarTaxFromFolk || DsTowns.FarmsLive) && WtFolkLive && CrownIncome.On; } }   // DS F3 (M8): podatek od zarobku czesc nadania - bez klucza 184

        /// <summary>W5b regula 1: brak wojska u panow liczy sie do stawki daniny - klucz WarAidFollowsLordsShortfall, czynny tylko z danina P1 (FolkWarAid) i doplata
        /// wedlug potrzeby W5 (bez niej nie ma miary potrzeby). Wylaczony = stawka daniny idzie za samym s korony, jak przed paczka.</summary>
        private static bool LnOn { get { var s = Settings.Current; return s != null && s.WarAidFollowsLordsShortfall && s.FolkWarAid && NeedAidOn && !DsTowns.FarmsLive; } }   // DS F3: bez P1 - bez stawki daniny z braku wojska

        /// <summary>Niepokryta potrzeba ponizej tej kwoty na dobe nie jest powodem (zold ok. 40 ludzi - jeden maly poczet, szum pomiaru).</summary>
        private const double LnFloor = 250;

        /// <summary>Recenzja W5b: krok stawki daniny (w gore i w dol) tylko przy pomiarze z co najmniej tylu dob z ostatnich 7. Niepokryta potrzeba i danina
        /// chodza z doby na dobe 2-5 razy (bieg 120 dob, doby 93-120: Iron Islands 0 - 6 051 zl, Free Folk 0 - 7 302 zl, rytm ok. 4 dob - dwie doby wysokie,
        /// dwie niskie), wiec srednia z 1-3 dob po wczytaniu gry trafia w szczyt albo w dolek i dawalaby falszywy krok. 4 doby = pelny rytm. Sesja krotsza
        /// niz 4 doby pomiaru przed doba kroku: stawka stoi (stan z zapisu), nic sie nie psuje; w pokoju spadek jak s - bez pomiaru.</summary>
        private const int LnMinDays = 4;

        /// <summary>W5b regula 1: stawka potrzeby, od ktorej liczy sie DANINA z oszczednosci (P1) - wieksza z s korony (stara regula 184) i stawki z braku wojska
        /// u panow. Podatek 184 od zarobku tej liczby NIE uzywa (FolkWarTax / FolkWarTaxRate - samo S).</summary>
        private static double WtAidS(WarTaxK w) { return w == null ? 0 : (w.SN > w.S && LnOn ? w.SN : w.S); }

        private static double WtClamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>Pulap s krolestwa: WarTaxMax, z polityka War Tax - wiekszy z (WarTaxMax, WarTaxMaxWithPolicy). Obie przyciete do 0..0.2.</summary>
        private static double WtCap(Kingdom k, Settings s)
        {
            if (DsTowns.FarmsLive) return DsSeason.TallageCap(k, s);   // DS F3 (M8): pulap DsTallageMax (z polityka War Tax DsTallageMaxPolicy)
            return WtCapOld(k, s);
        }

        /// <summary>Pulap s sprzed DS F3 (WarTaxMax / z polityka WarTaxMaxWithPolicy) - takze danina P1 w przejsciu F3 (recenzja 17.10: P1 x (1 - rampa) ze starym pulapem).</summary>
        private static double WtCapOld(Kingdom k, Settings s)
        {
            double cap = WtClamp(s.WarTaxMax, 0, 0.2);
            bool pol = false;
            try { pol = k != null && k.ActivePolicies.Contains(DefaultPolicies.WarTax); } catch { pol = false; }
            return pol ? Math.Max(cap, WtClamp(s.WarTaxMaxWithPolicy, 0, 0.2)) : cap;
        }

        private static WarTaxK WtOf(Kingdom k, bool create)
        {
            if (k == null || k.StringId == null) return null;
            WarTaxK w;
            if (_wt.TryGetValue(k.StringId, out w)) return w;
            if (!create) return null;
            w = new WarTaxK();
            _wt[k.StringId] = w;
            return w;
        }

        /// <summary>Stan wojny krolestwa tej doby (raz na dobe - pobor przy kazdym zarobku nie liczy wojen od nowa).</summary>
        private static bool WtAtWar(Kingdom k)
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day != _wtWarDay) { _wtWar.Clear(); _wtWarDay = day; }
            bool w;
            if (!_wtWar.TryGetValue(k, out w)) { w = AtWar(k); _wtWar[k] = w; }
            return w;
        }

        /// <summary>
        /// Stawka 184 klasy ludu tej osady dzis (s krolestwa x waga klasy, najwyzej pulap) - 0: wylaczone, kiesa ludu nieczynna, bez 165, rod bez krolestwa, pokoj, osada oblezona
        /// przy wlaczonym BesiegedPayNothing. Tylko odczyt (bez licznikow i bez zlota) - np. do prognozy kiesy.
        /// </summary>
        internal static double FolkWarTaxRate(Settlement st, int cls)
        {
            try
            {
                var s = Settings.Current;
                if (st == null || cls < 0 || cls >= WtClasses || !WtOn) return 0;
                var k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
                if (k == null || k.IsEliminated) return 0;
                var w = WtOf(k, false);
                if (w == null || w.S <= 0 || !WtAtWar(k)) return 0;
                if (st.IsUnderSiege && BesiegedPay.On) return 0;
                return Math.Min(w.S, WtCap(k, s)) * WtWeight[cls];
            }
            catch { return 0; }
        }

        /// <summary>
        /// E5/B2 POBOR od zarobku ludu (scalenie B: wola TownFolk.PayCrown raz na dobe od zarobku okna klasy - miasto: klasy WtW/WtR/WtP, zamek: WtCastle). Zwraca
        /// CALE zloto, ktore klasa oddaje koronie - wolajacy zdejmuje dokladnie tyle z kiesy tej klasy; skarbiec krolestwa wlasciciela osady dostaje to samo
        /// (przelew, nic z niczego). avail - stan kiesy klasy: wiecej nie pobiera; czesc ponad kiese przepada (jak podatek pana - TownFolk.PayLord), bez dlugu.
        /// Ulamek ponizej 1 zl zostaje jako niezaplacona reszta osady (doliczana do nastepnego poboru). 0: wylaczone, kiesa ludu nieczynna, bez 165, rod bez krolestwa, pokoj,
        /// s = 0, osada oblezona (R22 - bramka BesiegedPay.Holds liczy, ile by zaplacila). Nie rzuca (wlasny try, licznik potkniec).
        /// </summary>
        internal static int FolkWarTax(Settlement st, int cls, double earned, long avail = long.MaxValue)
        {
            try
            {
                if (st == null || cls < 0 || cls >= WtClasses || !(earned > 0) || double.IsInfinity(earned) || !WtOn) return 0;
                var k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
                if (k == null || k.IsEliminated) return 0;
                var w = WtOf(k, false);
                if (w == null || w.S <= 0 || !WtAtWar(k)) return 0;
                _wtCalls++;
                double rate = Math.Min(w.S, WtCap(k, Settings.Current)) * WtWeight[cls];
                double owed = earned * rate;
                double carry; _wtCarry.TryGetValue(st, out carry);
                double due = owed + carry;
                long pay = (long)Math.Floor(due);
                if (pay > int.MaxValue) pay = int.MaxValue;
                // R22: oblezona osada nie placi (zostaje w kiesie ludu - prawdziwy pieniadz, poza regulatorem gry); niezaplacona reszta czeka jak byla
                if (BesiegedPay.Holds(st, BesiegedPay.KFolkWarTax, pay, true))
                {
                    long h = (long)Math.Round(owed);
                    _wtHeld += h; _wtHeldN++; w.DHeld += h;
                    return 0;
                }
                if (pay > avail) { pay = Math.Max(0, avail); due = pay; }   // scalenie B: najwyzej stan kiesy klasy - reszta przepada (bez dlugu w osadzie)
                if (pay <= 0) { _wtCarry[st] = due; return 0; }
                int ip = (int)pay;
                _wtCarry[st] = due - ip;
                k.KingdomBudgetWallet += ip;                       // odbiorca: skarbiec krolestwa (platnik - kiesa klasy u wolajacego, to samo zloto)
                long x; _wtWindow.TryGetValue(k, out x); _wtWindow[k] = x + ip;   // wplyw dnia 165 (CrownIncome.Begin)
                _wtCls[cls] += ip; w.DTaken += ip;
                return ip;
            }
            catch (Exception e) { WtStumble("FolkWarTax", e); return 0; }
        }

        /// <summary>CrownIncome.Begin: podatek wojenny ludu pobrany od ostatniego CrownIncome.End do tego krolestwa ("nasze" wplywy dnia).</summary>
        internal static long FolkWarTaxWindow(Kingdom k)
        {
            long x;
            return k != null && _wtWindow.TryGetValue(k, out x) ? x : 0;
        }

        /// <summary>CrownIncome.End (zawsze): okno wplywow dnia zamkniete - pobor po Begin, a przed End jest juz w migawce skarbcow (zapas, nie wplyw dnia).</summary>
        internal static void FolkWarTaxWindowClear() { _wtWindow.Clear(); _waWindow.Clear(); }   // P1: okno daniny z oszczednosci zamykane razem z oknem 184

        // ------------------------------------------------------------ doba: potrzeba korony, krok tygodnia, linia (po rentach 180, przed CrownIncome.End - DayOf czynne)
        internal static void WarTaxDaily()
        {
            LastFolkWarTax = 0; LastFolkWarAid = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) { WtClearDay(); return; }
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                bool ds = DsTowns.FarmsLive;                       // DS F3 (M8): s z nadania wojennego na kwartal (DsSeason.TallageS) zamiast kroku tygodnia
                bool key = s.WarTaxFromFolk || ds, folk = WtFolkLive, on = key && folk, inc = CrownIncome.On, step = today % 7 == 0 && !ds;
                _wtWar.Clear(); _wtWarDay = today;                 // stan wojny tej doby - od nowa (pobor do jutrzejszego bloku czyta te liczbe)
                double stepV = WtClamp(s.WarTaxStep, 0, 0.05);
                double capHi = Math.Max(WtClamp(s.WarTaxMax, 0, 0.2), WtClamp(s.WarTaxMaxWithPolicy, 0, 0.2));
                double fall = Math.Max(stepV, capHi / 4.0);
                long reserveBase = Math.Max(0, s.CrownReserveGold);
                double reserveDays = WtClamp(s.WarTaxReserveDays, 0, 728);
                int warK = 0, posK = 0, capK = 0, peaceK = 0, needLoan = 0, needOver = 0, needLow = 0, up = 0, down = 0, hold = 0, drop = 0;
                double sumS = 0, maxS = 0; long reserveWar = 0;
                // W5b regula 1: brak wojska u panow (tylko stawka daniny z oszczednosci)
                bool ln = LnOn;
                double lnShare = WtClamp(s.WarAidLordsShortfallShare, 0, 1);
                int lnShortK = 0, lnPosK = 0, lnBlindK = 0, lnUp = 0, lnDown = 0, lnHold = 0;
                var parts = new List<KeyValuePair<double, string>>();
                var live = new HashSet<string>();
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        live.Add(k.StringId);
                        var w = WtOf(k, true);
                        w.Name = k.Name != null ? k.Name.ToString() : k.StringId;
                        w.DMove = 0; w.DStep = false;
                        // pierscienie doby (zawsze - po wlaczeniu klucza potrzeba z 7 i 28 dob jest juz znana)
                        // f6-zwrot (recenzja): pierscien zapasu wojny bierze PELNY zwrot (KDay.RefundFull, przed mnoznikiem potrzeby rodu) - zapas wojny W5b / K5 / 184 / DS / L5b-1 / korona2
                        // zostaje jak przed f6; flaga 'zwrot ponad wplywy dnia' (over) dalej z naleznego po f6 (RefundDue - to, co korona naprawde winna)
                        var kd = CrownIncome.DayOf(k);
                        bool loan = kd != null && kd.LoanIn > 0, over = kd != null && kd.RefundDue > kd.Measured;
                        w.Loan[w.H7] = loan; w.Over[w.H7] = over; w.H7 = (w.H7 + 1) % WtRing7; if (w.F7 < WtRing7) w.F7++;
                        w.Due[w.H28] = kd != null ? Math.Max(0, kd.RefundFull) : 0;
                        w.DueN[w.H28] = kd != null ? Math.Max(0, kd.RefundDue) : 0; if (w.FN < WtRing28) w.FN++;   // f23: nalezny po f6 w tym samym miejscu (pamiec sesji)
                        w.H28 = (w.H28 + 1) % WtRing28; if (w.F28 < WtRing28) w.F28++;
                        bool loan7 = false, over7 = false; long dsum = 0;
                        for (int i = 0; i < WtRing7; i++) { if (w.Loan[i]) loan7 = true; if (w.Over[i]) over7 = true; }
                        for (int i = 0; i < WtRing28; i++) dsum += w.Due[i];
                        double davg = w.F28 > 0 ? (double)dsum / w.F28 : 0;
                        long reserve = Math.Max(reserveBase, (long)(reserveDays * davg * (WarReserveByNeedOn ? WtNeedShare(w) : 1.0)));   // f23: ta sama liczba co WarReserveOf (davg dalej z pelnego - prog W5b regula 1 bez zmian)
                        long wallet = k.KingdomBudgetWallet;
                        bool low = wallet < reserve;
                        bool war = AtWar(k);
                        _wtWar[k] = war;
                        double cap = WtCap(k, s);
                        double debt = CrownBorrow.DebtOf(k);
                        bool arrears = CrownBorrow.InArrears(k);
                        double eff0 = Math.Min(WtAidS(w), cap);        // W5b: stawka, od ktorej dzis liczyla sie danina (przed krokiem)
                        if (!on) w.S = 0;                              // wylaczone albo kiesa ludu nieczynna (K3) = stan sprzed paczki (wlaczenie zaczyna od zera)
                        else if (ds)
                        {
                            // DS F3: s = min(pulap, g x DsTallageRate); recenzja 17.10: w przejsciu (rampa DsRampDays od ustalenia firm) s idzie liniowo od stawki sprzed firm do s z nadania
                            double sd = Math.Min(cap, DsSeason.TallageS(k)), left = DsTowns.RampDaysLeft;
                            double s1 = left > 0 ? w.S + (sd - w.S) / (left + 1) : sd;
                            s1 = Math.Round(WtClamp(s1, 0, cap), 6);
                            if (Math.Abs(s1 - w.S) > 1e-9) { w.DStep = true; w.DMove = s1 - w.S; }
                            w.S = s1;
                        }
                        else if (w.S > cap + 1e-6) w.S = Math.Round(cap, 6);   // recenzja B2: pulap spadl miedzy krokami (zdjeta polityka War Tax, MCM) - codziennie, przed krokiem
                        if (on && step)
                        {
                            double s0 = w.S, s1 = s0;
                            if (!war || !inc) s1 = s0 - fall;
                            else if (loan7 || over7 || low) s1 = s0 + stepV;
                            else if (wallet > 2 * reserve && debt < 1) s1 = s0 - stepV;
                            s1 = Math.Round(WtClamp(s1, 0, cap), 6);
                            w.S = s1; w.DStep = true; w.DMove = s1 - s0;
                            if (war && inc) { if (s1 > s0) up++; else if (s1 < s0) down++; else hold++; }
                            else if (s0 > 0) drop++;
                        }
                        // W5b regula 1 - BRAK WOJSKA U PANOW: pamiec doby (zawsze - po wlaczeniu klucza srednia jest juz znana) i krok stawki daniny z oszczednosci.
                        // Krok W5 tej doby juz biegl (CrownRents.Daily, przed tym blokiem). Doba bez pomiaru (pokoj, W5 nieczynne, budzet 166 jeszcze nie policzony
                        // w tej sesji) = NaN i nie wchodzi do sredniej. Po wczytaniu pierscien jest pusty: krok z pomiarem krotszym niz LnMinDays (4) doby stawki nie rusza.
                        double lu, lnd, lpt, lai;
                        bool lk = NeedAidMeasure(k, today, out lu, out lnd, out lpt, out lai);
                        w.LnU[w.HN] = lk ? lu : double.NaN; w.LnNeed[w.HN] = lk ? lnd : double.NaN; w.LnPot[w.HN] = lk ? lpt : double.NaN; w.LnAid[w.HN] = lk ? lai : double.NaN;
                        w.HN = (w.HN + 1) % WtRing7;
                        int n7 = 0; double u7 = 0, nd7 = 0, pt7 = 0, a7 = 0;
                        for (int i = 0; i < WtRing7; i++) if (!double.IsNaN(w.LnU[i])) { n7++; u7 += w.LnU[i]; nd7 += w.LnNeed[i]; pt7 += w.LnPot[i]; a7 += w.LnAid[i]; }
                        if (n7 > 0) { u7 /= n7; nd7 /= n7; pt7 /= n7; a7 /= n7; }
                        double lnThr = Math.Max(LnFloor, lnShare * davg);
                        bool lnMeas = n7 >= LnMinDays;                     // recenzja W5b: pomiar z co najmniej 4 dob (srednia z 1-3 dob po wczytaniu = szczyt albo dolek rytmu daniny)
                        bool lnShort = ln && war && inc && lnMeas && u7 > lnThr, stepN = false;
                        if (!on || !ln) w.SN = 0;                          // wylaczone (albo bez daniny P1 / bez W5) = stawka daniny idzie za samym s - stan sprzed paczki
                        else if (w.SN > cap + 1e-6) w.SN = Math.Round(cap, 6);
                        if (on && ln && step)
                        {
                            double n0 = w.SN, n1 = n0;
                            if (!war || !inc) n1 = n0 - fall;                            // pokoj albo bez 165 - spada jak s (do zera najwyzej w 4 tygodnie)
                            // brak ponad prog - JEDEN zwykly krok w gore od stawki, ktora danina szla w mierzonym tygodniu (eff0 - sprzed dzisiejszego kroku s), do
                            // zwyklego pulapu. Recenzja W5b: liczone od eff0, nie od s po dzisiejszym kroku - inaczej w dobie, w ktorej s samo roslo, danina skakala
                            // o dwa kroki naraz (krok s + krok z braku), a w dobie, w ktorej s spadalo ("luz"), mimo braku nie rosla wcale (s po spadku + krok = eff0)
                            else if (lnShort) n1 = eff0 + stepV;
                            // brak zniknal: krok w dol dopiero, gdy garnek daniny pokrylby potrzebe takze przy nizszej stawce - brak przewidziany po kroku (potrzeba
                            // minus garnek pomniejszony o ubytek daniny) nie wiekszy od tego samego progu, ktory podnosi stawke; bez potrzeby - zawsze. Inaczej
                            // stawka krazylaby co tydzien w gore i w dol, a rody z doplata na zmiane werbowalyby i zwalnialy. Potrzeba pokrywana skarbcem albo
                            // tarczowym stawki nie obniza (liczy sie sam garnek daniny). Stawka nie wieksza od s korony niczego nie zmienia - schodzi do zera.
                            else if (n0 > 0 && lnMeas && (n0 <= w.S + 1e-9 || nd7 - Math.Max(0, pt7 - a7 * stepV / n0) <= lnThr)) n1 = n0 - stepV;
                            w.SN = Math.Round(WtClamp(n1, 0, cap), 6); stepN = true;
                        }
                        double eff1 = Math.Min(WtAidS(w), cap), moveN = eff1 - eff0;
                        if (war && ln)
                        {
                            if (!lnMeas) lnBlindK++;
                            if (lnShort) lnShortK++;
                            if (w.SN > w.S + 1e-9) lnPosK++;
                            if (stepN && inc) { if (moveN > 1e-9) lnUp++; else if (moveN < -1e-9) lnDown++; else lnHold++; }
                        }
                        if (war)
                        {
                            warK++; reserveWar += reserve;
                            if (loan7) needLoan++;
                            if (over7) needOver++;
                            if (low) needLow++;
                            sumS += w.S; if (w.S > maxS) maxS = w.S;
                            if (w.S > 0) posK++;
                            if (w.S > 0 && w.S >= cap - 1e-9) capK++;
                        }
                        else if (w.S > 0) peaceK++;
                        if (war || w.S > 0 || w.DMove != 0 || w.DTaken > 0 || w.SN > 0 || moveN != 0)
                        {
                            var sb1 = new StringBuilder(128);
                            sb1.Append(w.Name).Append(": s ").Append((100 * w.S).ToString("0.00", WtInv)).Append("% / pulap ").Append((100 * cap).ToString("0.0", WtInv)).Append('%')
                               .Append(war ? ", wojna" : ", pokoj");
                            if (war) sb1.Append(loan7 || over7 || low ? ", potrzeba " + (loan7 ? "K" : "") + (over7 ? "Z" : "") + (low ? "S" : "") : (wallet > 2 * reserve && debt < 1 && !loan7 && !over7 ? ", luz" : ""));
                            if (w.DStep) sb1.Append(", krok ").Append(w.DMove >= 0 ? "+" : "").Append((100 * w.DMove).ToString("0.00", WtInv)).Append(" pkt");
                            sb1.Append(", skarbiec ").Append(wallet).Append(" / zapas wojny ").Append(reserve);
                            if (debt >= 1) sb1.Append(", dlug 186 ").Append((long)debt);
                            if (arrears) sb1.Append(", ZALEGLOSC");
                            if (w.DTaken > 0 || w.DHeld > 0) sb1.Append(", pobrano ").Append(w.DTaken).Append(w.DHeld > 0 ? " (wstrzymane w oblezeniu " + w.DHeld + ")" : "");
                            // W5b regula 1: powod kroku daniny (na koncu wiersza - czytniki starych pol ida od poczatku)
                            if (ln && (war ? (n7 > 0 || w.SN > 0) : w.SN > 0))
                            {
                                sb1.Append(", brak wojska u panow ").Append(Math.Round(u7).ToString("0", WtInv)).Append(" zl/d (prog ").Append(Math.Round(lnThr).ToString("0", WtInv))
                                   .Append(", pomiar ").Append(n7).Append(" dob), stawka daniny ").Append((100 * eff1).ToString("0.00", WtInv)).Append('%');
                                if (stepN) sb1.Append(", krok daniny ").Append(moveN >= 0 ? "+" : "").Append((100 * moveN).ToString("0.00", WtInv)).Append(" pkt");
                            }
                            parts.Add(new KeyValuePair<double, string>(w.S + (war ? 1 : 0), sb1.ToString()));
                        }
                    }
                    catch (Exception e) { WtStumble("WarTaxDaily(krolestwo)", e); }
                }
                // krolestwa, ktorych juz nie ma - poza stanem i zapisem
                var gone = new List<string>();
                foreach (var kv in _wt) if (!live.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var id in gone) _wt.Remove(id);
                long taken = _wtCls[WtW] + _wtCls[WtR] + _wtCls[WtP] + _wtCls[WtCastle];
                LastFolkWarTax = taken;
                if (s.LogEnabled)
                {
                    parts.Sort((x, y) => y.Key.CompareTo(x.Key));
                    var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                    var sb = new StringBuilder(2048);
                    sb.Append("Korona: podatek wojenny ludu (184): dzien ").Append(today)
                      .Append(" | wylacznik War Tax From Folk ").Append(key ? "tak" : "nie (s = 0, pobor 0 - jak przed paczka)")
                      .Append(", 165 Crown Current Income ").Append(inc ? "tak" : "nie (pobor 0, s spada jak w pokoju)")
                      .Append(", kiesa ludu (B1) ").Append(folk ? "czynna" : "nieczynna (s = 0, pobor 0 - K3: 184 tylko z czynna kiesa)")
                      .Append(" | pulap ").Append((100 * WtClamp(s.WarTaxMax, 0, 0.2)).ToString("0.0", WtInv)).Append("% (z polityka War Tax ").Append((100 * capHi).ToString("0.0", WtInv))
                      .Append("%), krok ").Append((100 * stepV).ToString("0.00", WtInv)).Append(" pkt co 7 dob, w pokoju -").Append((100 * fall).ToString("0.00", WtInv))
                      .Append(" pkt; zapas wojny max(").Append(reserveBase).Append(", ").Append(reserveDays.ToString("0", WtInv)).Append(WarReserveByNeedOn ? " dob naleznego zwrotu z 28 dob - wedlug potrzeby rodu, f23" : " dob naleznego zwrotu z 28 dob").Append("); wagi W 0.5 / R 1.0 / P 1.5 / podzamcze 1.0")
                      .Append(" | s krolestw w wojnie (srednia, max): ").Append(warK > 0 ? (100 * sumS / warK).ToString("0.00", WtInv) : "0.00").Append("%, ").Append((100 * maxS).ToString("0.00", WtInv))
                      .Append("% - krolestw w wojnie ").Append(warK).Append(", z s > 0 ").Append(posK).Append(", na pulapie ").Append(capK).Append("; s > 0 w pokoju (spada, bez poboru) ").Append(peaceK)
                      .Append(" | pobrano ").Append(taken).Append(" zl (ludzie miast ").Append(_wtCls[WtW] + _wtCls[WtR] + _wtCls[WtP]).Append(": W ").Append(_wtCls[WtW]).Append(", R ").Append(_wtCls[WtR])
                      .Append(", P ").Append(_wtCls[WtP]).Append("; podzamcza ").Append(_wtCls[WtCastle]).Append("), wolan poboru ").Append(_wtCalls)
                      .Append(" (0 przy s > 0 w wojnie = pobor niewpiety - TownFolk.PayCrown, scalenie B), wstrzymane w oblezeniu ").Append(_wtHeld).Append(" (").Append(_wtHeldN).Append(')')
                      .Append(" | potrzeba (7 dob, krolestwa w wojnie): kredyt 186 ").Append(needLoan).Append(" krolestw, zwrot ponad wplywy ").Append(needOver)
                      .Append(", skarbiec < zapas wojny ").Append(needLow).Append(" (zapas wojny razem ").Append(reserveWar).Append(')')
                      .Append(" | krok tygodnia: ").Append(step ? "dzis" : "nie (za " + (7 - today % 7) + " dob)");
                    if (step) sb.Append(" - w gore ").Append(up).Append(", w dol ").Append(down).Append(", bez zmian ").Append(hold).Append(", pokoj albo bez 165 - spadek ").Append(drop);
                    // W5b regula 1
                    sb.Append(" | brak wojska u panow (W5b): wylacznik War Aid Follows Lords Shortfall ")
                      .Append(s.WarAidFollowsLordsShortfall ? (ln ? "tak" : "tak, ale nieczynny (bez daniny P1 albo bez doplaty W5 - stawka daniny = s)") : "nie (stawka daniny = s - jak przed paczka)")
                      .Append(", powod: srednia z 7 dob niepokrytej potrzeby W5 (po daninie, skarbcu i tarczowym) > max(").Append(LnFloor.ToString("0", WtInv)).Append(", ")
                      .Append((100 * lnShare).ToString("0.0", WtInv)).Append("% naleznego zwrotu z 28 dob) - podnosi TYLKO stawke daniny z oszczednosci (podatek 184 od zarobku po staremu)")
                      .Append("; krok daniny tylko przy pomiarze z co najmniej ").Append(LnMinDays).Append(" dob z 7 (po wczytaniu gry stawka stoi do tego czasu)")
                      .Append("; krolestw w wojnie z brakiem ponad prog ").Append(lnShortK).Append(", ze stawka daniny ponad s ").Append(lnPosK).Append(", z pomiarem krotszym niz ").Append(LnMinDays).Append(" doby ").Append(lnBlindK);
                    if (step) sb.Append("; krok daniny: w gore ").Append(lnUp).Append(", w dol ").Append(lnDown).Append(", bez zmian ").Append(lnHold);
                    sb.Append(" | na krolestwo: ").Append(txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-")
                      .Append(" | potkniecia ").Append(_wtStumbles).Append('.');
                    if (_wtImportN >= 0) { sb.Append(" Wczytano z zapisu (165, szoste pole): stan ").Append(_wtImportN).Append(" krolestw (bledne ").Append(_wtImportBad).Append(")."); _wtImportN = -1; }
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { WtStumble("WarTaxDaily", e); }
            // P1: linia doby daniny z oszczednosci i komunikaty gracza - po rentach 180, przed CrownIncome.End (DayOf czynne), przed zerowaniem licznikow doby
            try { WarAidDay(); } catch (Exception e) { WaStumble("WarAidDay", e); }
            // M4: plan daniny pokoju na jutro (chleb korony tej doby - po BreadRest) i linia "Danina pokoju (M4)" - przed zerowaniem licznikow doby
            try { PeaceTributeDay(); } catch (Exception e) { WaStumble("PeaceTributeDay", e); }
            finally { WtClearDay(); }
        }

        private static void WtClearDay()
        {
            Array.Clear(_wtCls, 0, WtClasses); _wtHeld = 0; _wtCalls = _wtHeldN = 0; _wtStumbles = 0;
            foreach (var kv in _wt) { kv.Value.DTaken = 0; kv.Value.DHeld = 0; }
            WaClearDay();   // P1: liczniki doby daniny z oszczednosci
        }

        // ------------------------------------------------------------ zapis: szoste pole klucza 165 (CrownIncome.Export/Import)
        // "id=s:pozyczki7:ponad7:glowa7:wypelnienie7:glowa28:wypelnienie28:zwrot0,zwrot1,...,zwrot27;..." (pozyczki7/ponad7 - maski bitowe pierscienia 7 dob)
        private static readonly char[] WtBad = { '|', ';', '=', ':', ',', '>' };

        internal static string WarTaxExport()
        {
            try
            {
                var sb = new StringBuilder(_wt.Count * 200);
                bool first = true;
                foreach (var kv in _wt)
                {
                    var w = kv.Value;
                    if (kv.Key == null || kv.Key.IndexOfAny(WtBad) >= 0) continue;
                    int lm = 0, om = 0;
                    for (int i = 0; i < WtRing7; i++) { if (w.Loan[i]) lm |= 1 << i; if (w.Over[i]) om |= 1 << i; }
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append('=').Append(w.S.ToString("0.######", WtInv)).Append(':').Append(lm.ToString(WtInv)).Append(':').Append(om.ToString(WtInv))
                      .Append(':').Append(w.H7.ToString(WtInv)).Append(':').Append(w.F7.ToString(WtInv)).Append(':').Append(w.H28.ToString(WtInv)).Append(':').Append(w.F28.ToString(WtInv)).Append(':');
                    for (int i = 0; i < WtRing28; i++) { if (i > 0) sb.Append(','); sb.Append(w.Due[i].ToString(WtInv)); }
                    // W5b regula 1: stawka z braku wojska u panow (milionowe) - 29. liczba listy, tylko gdy > 0. Starsza wersja moda czyta pierwsze 28 liczb i reszte
                    // pomija (WarTaxImport: petla do WtRing28), wiec format pola sie nie zmienia; zapis bez niej = 0
                    // f23-korona (recenzja): pierscien naleznego zwrotu po mierze potrzeby rodu (DueN) i FN - po 28 liczbach Due ZAWSZE 29. (SN, takze 0), potem 28 liczb DueN
                    // i FN (razem 58 liczb). Starsza DLL czyta 28 liczb i 29. jako SN (0 = jak zapis bez stawki), reszte pomija; zapis bez nich - FN 0 (udzial z dob sesji)
                    if (w.FN > 0)
                    {
                        sb.Append(',').Append(((long)Math.Round(Math.Max(0, w.SN) * 1e6)).ToString(WtInv));
                        for (int i = 0; i < WtRing28; i++) sb.Append(',').Append(w.DueN[i].ToString(WtInv));
                        sb.Append(',').Append(Math.Min(WtRing28, w.FN).ToString(WtInv));
                    }
                    else if (w.SN > 0) sb.Append(',').Append(((long)Math.Round(w.SN * 1e6)).ToString(WtInv));
                }
                return sb.ToString();
            }
            catch (Exception e) { WtStumble("WarTaxExport", e); return ""; }
        }

        /// <summary>null albo pusty - zapis bez szostego pola (sprzed E5/B2): s = 0 i puste pierscienie u wszystkich krolestw.</summary>
        internal static void WarTaxImport(string data)
        {
            _wt.Clear(); _wtImportN = 0; _wtImportBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                foreach (var p in data.Split(';'))
                {
                    try
                    {
                        var kv = p.Split('=');
                        if (kv.Length != 2 || kv[0].Length == 0) { _wtImportBad++; continue; }
                        var x = kv[1].Split(':');
                        double sv; int lm, om, h7, f7, h28, f28;
                        if (x.Length != 8 || !double.TryParse(x[0], NumberStyles.Float, WtInv, out sv) || !int.TryParse(x[1], NumberStyles.Integer, WtInv, out lm)
                            || !int.TryParse(x[2], NumberStyles.Integer, WtInv, out om) || !int.TryParse(x[3], NumberStyles.Integer, WtInv, out h7) || !int.TryParse(x[4], NumberStyles.Integer, WtInv, out f7)
                            || !int.TryParse(x[5], NumberStyles.Integer, WtInv, out h28) || !int.TryParse(x[6], NumberStyles.Integer, WtInv, out f28))
                        { _wtImportBad++; continue; }
                        var w = new WarTaxK
                        {
                            S = WtClamp(double.IsNaN(sv) ? 0 : sv, 0, 0.2), H7 = Math.Max(0, Math.Min(WtRing7 - 1, h7)), F7 = Math.Max(0, Math.Min(WtRing7, f7)),
                            H28 = Math.Max(0, Math.Min(WtRing28 - 1, h28)), F28 = Math.Max(0, Math.Min(WtRing28, f28))
                        };
                        for (int i = 0; i < WtRing7; i++) { w.Loan[i] = (lm & (1 << i)) != 0; w.Over[i] = (om & (1 << i)) != 0; }
                        var dd = x[7].Split(',');
                        for (int i = 0; i < WtRing28; i++) { long v; if (i < dd.Length && long.TryParse(dd[i], NumberStyles.Integer, WtInv, out v)) w.Due[i] = Math.Max(0, v); }
                        long sn;   // W5b regula 1: 29. liczba listy (zapis sprzed paczki - brak, stawka 0)
                        if (dd.Length > WtRing28 && long.TryParse(dd[WtRing28], NumberStyles.Integer, WtInv, out sn)) w.SN = WtClamp(sn / 1e6, 0, 0.2);
                        // f23-korona (recenzja): 30.-57. liczba - pierscien DueN, 58. - FN (zapis sprzed recenzji: brak - FN 0, udzial z dob sesji); jedna zla liczba - FN 0
                        if (dd.Length >= 2 * WtRing28 + 2)
                        {
                            bool okN = true; long vn; int fn;
                            for (int i = 0; i < WtRing28 && okN; i++) { if (long.TryParse(dd[WtRing28 + 1 + i], NumberStyles.Integer, WtInv, out vn)) w.DueN[i] = Math.Max(0, vn); else okN = false; }
                            if (okN && int.TryParse(dd[2 * WtRing28 + 1], NumberStyles.Integer, WtInv, out fn)) w.FN = Math.Max(0, Math.Min(Math.Min(WtRing28, w.F28), fn));
                            else { w.FN = 0; Array.Clear(w.DueN, 0, WtRing28); }
                        }
                        _wt[kv[0]] = w; _wtImportN++;
                    }
                    catch { _wtImportBad++; }
                }
            }
            catch (Exception e) { WtStumble("WarTaxImport", e); }
        }
    }
}
