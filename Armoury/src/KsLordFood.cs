using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// r4-glodbunt (noc 17/18.10; Jeff: "w interesie miasta lorda jest aby dowiesc jedzenie i jak bylaby taka sytuacja to od razu kupcy by przywozili jedzenie, bo duzo tam
    /// zarobia, chyba ze jest globalny glod, to powinna ustawiac ekonomia podazy i popytu") - PAN MIASTA PLACI ZA DOWOZ. Klucz KsLordFundsFood (wylaczony = kasa miasta placi
    /// sama, jak dotad). Tylko przy zastosowanej kalibracji i czynnych kupcach zywnosci KS-H (od doby A); jedna regula dla kazdego miasta swiata.
    ///  - KTO: miasto glodne wedlug KsHunger (czesc kary f > 0 - ta sama definicja glodu co niepokoj) z luka rynku dzisiejszego posilku (GapValue - brakujace kcal w ziarnie po
    ///    cenie polki miasta), ktorego kasa nie zaplaci kupcom tygodnia dowozu: budzet KS-H kasy (KsFoodCore - 15% ponad zapas kupcow, przy pustej polce zboza i soli do polowy
    ///    zapasu i 5% zapasu na dobe ponad to) < 7 x GapValue (zamowienie KS-H siega (T* + doby + 2) dni).
    ///  - PLATNIK: glowa rodu wlasciciela, tylko ponad rezerwe wojny R budzetu 166 i tylko z tej czesci tempa skrzyni (G - R) / WarChestDays, ktorej nie bierze zold (ta sama
    ///    regula co chleb M3 - TownFolk.BreadLordShare; minus chleb tego rodu dzis, splata turniejowa TP dzis i nasza doplata dzis), najwyzej kiesa glowy; NIGDY gracz; rod bez
    ///    budzetu, dluznik i rod w zajeciu (168), Straz bez zoldu, kompania najemna - nie (ClanBudget.ChestOf, jak M3). W wojnie ClanBudget zdejmuje wczorajsza doplate ze
    ///    skrzyni sprzetu (jak chleb), wiec pan w sumie nie wydaje szybciej niz skrzynia.
    ///  - TRYB "DOPLATA KASY NA CZAS ZAKUPOW" (kod KS-H bez kredytu kupcow - platnosc przy wysylce w KsFoodCarts.Daily): Before (SupplyDemand.DailyTrade, tuz przed
    ///    KsFoodCarts.Daily) - pan wplaca do kasy miasta najmniejsza kwote X, przy ktorej budzet KS-H tej kasy rosnie o e = min(tempo pana, 7 x GapValue - budzet kasy);
    ///    After (tuz po KsFoodCarts.Daily) - wydatek KS-H tej kasy w tym wywolaniu (kasa przed + X - kasa po; przychody miasta jako zrodla tylko go zmniejszaja) dzieli sie:
    ///    kasa placi najpierw do swojego budzetu, pan reszte (najwyzej e i X); niewydana reszta X wraca do pana od razu. Pieniadze pana nigdy nie zostaja w kasie poza tym
    ///    nawiasem (bunt nie wezmie ich z kasy - RebelPurseFromTown; inne wyplaty z nadwyzki kasy ich nie widza). Zloto: glowa -> kasa (na chwile) -> zrodla, furmani, zalogi
    ///    (ksiegi KS-H) -> zwrot reszty kasa -> glowa; pozycja MKsH "Przeplywy osad" obejmuje wiec takze doplate panow (linia tej klasy ja rozpisuje).
    ///    Recenzja (noc 17/18.10): (a) doplata tylko w DOBIE PRZEGLADU miasta jako celu KS-H (KsFoodCarts.ReviewDay - ta sama parzystosc Fnv co KsFoodCore; poza nia cel
    ///    nie zamawia, wiec wplata wracalaby w calosci - pusty obieg duzych kwot przez kase); limit nadal tempo JEDNEJ doby (2 x tempo w dobie przegladu przekroczyloby
    ///    skrzynie: ClanBudget zdejmuje doplate ze skrzyni sprzetu jednej doby z obcieciem do 0). (b) udzial pana ograniczony tez KSIEGA KUPCOW - zaplacone przez kase
    ///    celu za wozy kupione dzis (towar + fracht, KsFoodCarts.PaidAsDstOn) minus budzet wlasny kasy; kontrola niezalezna: wydatek kasy netto (delta kasy w nawiasie)
    ///    nie wiekszy niz ksiega kupcow (inaczej w nawiasie ruszyl kase ktos poza KS-H - linia "kasa netto ponad ksiege", oczekiwane 0).
    ///  - TRYB "RACHUNKI KUPCOW" (po scaleniu z r4-kupcy - kredyt kupcow: zaplata przy dostawie, reszta rachunkiem): gdy ustawione FoodBillsOpen i FoodBillsPay, Before splaca
    ///    otwarte rachunki zywnosci glodnego miasta z kiesy pana (najwyzej tempo i rachunki; FoodBillsPay oddaje wierzycielom i zwraca kwote zaplacona, reszta wraca do pana),
    ///    bez doplaty kasy. Scalenie: patrz docs/noc-1718/r4-glodbunt-CHANGELOG.md.
    ///    PODLACZONE przy scaleniu nocy 17/18.10: FoodBillsOpen = KsFoodCredit.OverdueOf (rachunki ZALEGLE - starsze niz Ks Consign Overdue Days, te blokuja nowe dostawy),
    ///    FoodBillsPay = KsFoodCredit.PayByLord (najstarsze najpierw, zloto prosto do kas zrodel z pomiarem; nieprzyjete wraca do pana). Tryb RACHUNKI tylko przy kluczu
    ///    Ks Food Carts Consign TAK (kredyt kupcow); przy NIE KS-H placi przy wysylce jak dotad - tryb DOPLATA KASY bez bezpiecznika (wozy kupione bez kredytu).
    ///    Kontrola linii: zloto panow (pomiar kiesy glowy) + kasy miast + wierzyciele (pomiar kas zrodel) = 0; ta sama splata w linii KS-H (splacone, panowie -).
    ///  - f4 P5 (koncowy przeglad ekonomii 18.10): WCZESNA SPLATA (klucz Ks Lord Food Bills Early Share 0.67; 0 = jak wyzej bit w bit) - w trybie RACHUNKI pan splaca
    ///    rachunki zywnosci KAZDEGO swojego miasta (nie tylko glodnego) starsze niz 2/3 zwloki, z tych samych granic (BeforeEarly).
    /// Zapis gry: nic (stan w nawiasie jednej doby; doplata wczoraj wedlug rodu - pamiec sesji). Linia doby "Pan miasta placi za dowoz (KS): dzien N".
    /// Wyjatki per miasto - licznik potkniec (CLAUDE.md 7); zaleglosc nawiasu (After nie zdazyl) zwracana na poczatku nastepnego Before.
    /// </summary>
    internal static class KsLordFood
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double WeekDays = 7.0;

        /// <summary>r4-kupcy (scalenie): otwarte rachunki zywnosci miasta u kupcow KS-H (zl; 0 - brak). Razem z FoodBillsPay wlacza tryb RACHUNKI zamiast doplaty kasy.</summary>
        internal static Func<Settlement, long> FoodBillsOpen = KsFoodCredit.OverdueOf;          // scalenie nocy 17/18.10: zalegle rachunki miasta (r4-kupcy)
        /// <summary>r4-kupcy (scalenie): splata rachunkow zywnosci miasta (najstarsze najpierw) z pieniedzy JUZ zdjetych platnikowi - przelew do wierzycieli; zwraca zaplacone (0..x).</summary>
        internal static Func<Settlement, long, long> FoodBillsPay = KsFoodCredit.PayByLord;     // scalenie nocy 17/18.10: splata od najstarszego do kas zrodel

        private sealed class Dep { public Town T; public Hero H; public Clan C; public long X, G0, Cap; public double Own0; public int Day; }
        private static readonly List<Dep> _open = new List<Dep>();
        private static readonly List<KeyValuePair<Town, KsHunger.Row>> _hungry = new List<KeyValuePair<Town, KsHunger.Row>>();
        private static readonly Dictionary<Clan, long> _paid = new Dictionary<Clan, long>();
        // nawias doby wedlug rodu: wplaty jeszcze nie rozliczone (zloto rodu w kasach na czas zakupow - liczone do G rodu) i zarezerwowane tempo (najwyzej tyle zaplaci)
        private static readonly Dictionary<Clan, long> _out = new Dictionary<Clan, long>(), _resv = new Dictionary<Clan, long>();
        private static int _paidDay = int.MinValue;

        // liczniki doby (linia)
        private static int _dDay = int.MinValue, _nH, _nOk, _nGap0, _nNeed, _nPay, _nPl, _nNb, _nRoom, _nNoBills, _nBelow, _nOver, _nCut, _nStale;
        private static long _dep, _ret, _lordPaid, _need, _spend, _dLords, _dTowns, _billsOpen, _billsPaid, _cut;
        private static long _billsRecv;                                     // scalenie: zloto przyjete przez kasy zrodel (pomiar w KsFoodCredit.PayByLord) - kontrola linii
        // recenzja: doba poza przegladem celu KS-H (bez wplaty), ksiega kupcow (zaplacone przez kasy z wplata), kasa netto ponad ksiege (miasta, zl), udzial pana uciety ksiega
        private static int _nOff, _nMis, _nBookCut;
        private static long _book, _mis;
        private static readonly Dictionary<Settlement, long> _bookOf = new Dictionary<Settlement, long>();
        private static readonly HashSet<Clan> _clans = new HashSet<Clan>();
        private static bool _ksh, _bills, _creditOff;
        // r4-kupcy (bezpiecznik scalenia): kod KS-H z kredytem kupcow (zaplata przy dostawie) bez podlaczonych rachunkow - doplata kasy NIE moze biec (wplata na czas
        // zakupow podnioslaby limit kredytu, a dostawe zaplacilaby kasa bez pieniedzy pana -> rachunki i blokada); -1 nie sprawdzone, 0 brak sladu, 1 slad (nazwy skladowych)
        private static int _credit = -1;
        private static string _creditWhy = "";
        private static long _ticks;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        // f4 P5 (wczesna splata, klucz Ks Lord Food Bills Early Share): rachunki starsze niz prog wedlug miasta celu (kwota, doba najstarszego), kolejnosc miast doby;
        // liczniki doby: prog (dni; -1 - tryb wylaczony), miasta syte z rachunkami ponad prog, z tego splacone przez pana, rachunki ponad prog (zl), splacone przez panow (zl)
        private static readonly Dictionary<Settlement, long> _eSum = new Dictionary<Settlement, long>();
        private static readonly Dictionary<Settlement, int> _eOld = new Dictionary<Settlement, int>();
        private static readonly List<Town> _eList = new List<Town>();
        private static readonly HashSet<Town> _eSeen = new HashSet<Town>();
        private static int _eAge = -1, _eSated, _eSatedPaid;
        private static long _eOpen, _ePaid;

        private static bool On(Settings s) { return s != null && s.KsLordFundsFood; }

        /// <summary>f4 P5: udzial zwloki, po ktorym pan splaca rachunki zywnosci swojego miasta (0..1; 0 - tryb wylaczony, kod jak w f5fd0ae3).</summary>
        internal static double EarlyShare(Settings s) { return s == null ? 0 : Math.Max(0.0, Math.Min(1.0, (double)s.KsLordFoodBillsEarlyShare)); }

        /// <summary>f4 P5: prog wieku rachunku (dni) dla wczesnej splaty - floor(udzial x Ks Consign Overdue Days); -1 przy wylaczonym.</summary>
        internal static int EarlyAge(Settings s)
        {
            double e = EarlyShare(s);
            if (!(e > 0)) return -1;
            return Math.Max(0, Math.Min(KsFoodCredit.OverdueDays, (int)Math.Floor(e * KsFoodCredit.OverdueDays + 1e-9)));
        }
        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("KsLordFood." + where, e); } catch { }
        }

        /// <summary>Nowa kampania i wczytanie (KsHunger.EnsureHooks): pamiec od nowa. Otwarty nawias z poprzedniej sesji nie istnieje (Before i After w jednym wywolaniu doby).</summary>
        internal static void Reset()
        {
            _open.Clear(); _hungry.Clear(); _paid.Clear(); _paidDay = int.MinValue; _out.Clear(); _resv.Clear(); _bookOf.Clear();
            ClearDay(); _dDay = int.MinValue; _stumbles = 0; _err.Clear();
        }

        /// <summary>Bezpiecznik scalenia z r4-kupcy: czy kod kupcow KS-H (KsFoodCarts, KsFoodCore) ma skladowe kredytu / rachunkow (nazwy z "bill", "rachun", "credit",
        /// "kredyt", "debt", "owed", "overdue", "zwlok", "zaleg"). Raz na proces (refleksja na wlasnych typach). Kod bez kredytu (ta galaz) - false.</summary>
        private static bool CreditCode()
        {
            if (_credit >= 0) return _credit == 1;
            _credit = 0;
            try
            {
                var tokens = new[] { "bill", "rachun", "credit", "kredyt", "debt", "owed", "overdue", "zwlok", "zaleg" };
                const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                                                         | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
                foreach (var t in new[] { typeof(KsFoodCarts), typeof(KsFoodCore) })
                    foreach (var m in t.GetMembers(all))
                    {
                        string n = m.Name.ToLowerInvariant();
                        foreach (var k in tokens)
                            if (n.Contains(k)) { _credit = 1; _creditWhy = t.Name + "." + m.Name; return true; }
                    }
            }
            catch (Exception e) { Stumble("CreditCode", e); }
            return false;
        }

        private static void ClearDay()
        {
            _nH = _nOk = _nGap0 = _nNeed = _nPay = _nPl = _nNb = _nRoom = _nNoBills = _nBelow = _nOver = _nCut = _nStale = 0;
            _dep = _ret = _lordPaid = _need = _spend = _dLords = _dTowns = _billsOpen = _billsPaid = _cut = 0; _billsRecv = 0;
            _nOff = _nMis = _nBookCut = 0; _book = _mis = 0;
            _clans.Clear(); _ksh = false; _bills = false; _creditOff = false; _ticks = 0;
            _eAge = -1; _eSated = _eSatedPaid = 0; _eOpen = _ePaid = 0;
        }

        /// <summary>Doplata rodu `c` z biezacej doby doplat (0 - inna doba albo nic).</summary>
        internal static long PaidToday(Clan c)
        {
            long v;
            return c != null && Campaign.Current != null && _paidDay == Today() && _paid.TryGetValue(c, out v) ? Math.Max(0, v) : 0;
        }

        /// <summary>ClanBudget.Daily (przed kupcami doby): doplata rodu `c` z ostatniej doby doplat, gdy byla wczoraj albo dzis (jak chleb M3 - skrzynia sprzetu w wojnie
        /// o tyle mniejsza). Klucz wylaczony - 0 (gra jak dotad).</summary>
        internal static long RecentPaid(Clan c)
        {
            long v;
            if (c == null || Campaign.Current == null || !On(Settings.Current)) return 0;
            int d = Today();
            return (_paidDay == d || _paidDay == d - 1) && _paid.TryGetValue(c, out v) ? Math.Max(0, v) : 0;
        }

        /// <summary>Budzet KS-H kasy celu wedlug KsFoodCore.Decide (gorna granica: zboze i sol przy pustej polce - do polowy zapasu kupcow, najwyzej 5% zapasu na dobe ponad
        /// zwykle 15% nadwyzki). Te same stale co rdzen kupcow (KsFoodCore).</summary>
        internal static double Budget(double gold, double res)
        {
            res = Math.Max(0.0, res);
            double b = KsFoodCore.CofferShare * Math.Max(0.0, gold - res);
            double bs = Math.Min(KsFoodCore.CofferShare * Math.Max(0.0, gold - KsFoodCore.ReserveFloorShort * res), b + KsFoodCore.ReserveDayShort * res);
            return Math.Max(b, bs);
        }

        /// <summary>Najmniejsza wplata X (0..xmax), przy ktorej Budget(kasa + X) - own0 >= e; nie starczy - xmax.</summary>
        private static long SolveX(long gold, long res, double own0, double e, long xmax)
        {
            if (xmax <= 0 || !(e > 0)) return 0;
            if (Budget(gold + xmax, res) - own0 < e) return xmax;
            long lo = 0, hi = xmax;
            while (hi - lo > 1)
            {
                long mid = lo + (hi - lo) / 2;
                if (Budget(gold + mid, res) - own0 >= e) hi = mid; else lo = mid;
            }
            return hi;
        }

        private static void NotePaid(Clan c, long x)
        {
            int d = Today();
            if (_paidDay != d) { _paid.Clear(); _paidDay = d; }
            if (c == null || x <= 0) return;
            long v; _paid.TryGetValue(c, out v); _paid[c] = v + x;
            _clans.Add(c);
        }

        /// <summary>Przelew glowa -> kasa (x > 0) albo kasa -> glowa (x < 0) z faktycznymi zmianami obu stron (kontrola zamknieta). Zwraca, ile weszlo do kasy (ze znakiem).</summary>
        private static long Move(Hero h, Town t, long x)
        {
            if (x == 0 || h == null || t == null) return 0;
            if (x > 0)
            {
                int ix = (int)Math.Min(int.MaxValue, x);
                int h0 = h.Gold; h.ChangeHeroGold(-ix); long took = h0 - h.Gold;
                CirculationWindows.NoteHeroGold(h, -took);
                int g0 = t.Gold; t.ChangeGold((int)took); long got = t.Gold - g0;
                if (got != took) { long back = took - got; int h1 = h.Gold; h.ChangeHeroGold((int)back); took -= h.Gold - h1; }   // kasa bez miejsca (int) - reszta wraca
                _dLords -= took; _dTowns += got;
                return got;
            }
            else
            {
                int ix = (int)Math.Min(int.MaxValue, Math.Min(-x, (long)t.Gold));
                int g0 = t.Gold; t.ChangeGold(-ix); long gave = g0 - t.Gold;
                int h0 = h.Gold; h.ChangeHeroGold((int)gave); long got = h.Gold - h0;
                CirculationWindows.NoteHeroGold(h, got);
                if (got != gave) { long back = gave - got; int g1 = t.Gold; t.ChangeGold((int)back); gave -= t.Gold - g1; }   // kiesa glowy na suficie int - reszta zostaje w kasie
                _dTowns -= gave; _dLords += got;
                return -gave;
            }
        }

        // ------------------------------------------------------------ nawias wokol KsFoodCarts.Daily (SupplyDemand.DailyTrade)
        /// <summary>Tuz przed KsFoodCarts.Daily: migawka glodu doby (KsHunger.Daily - zawsze), zaleglosc nawiasu, potem doplata panow do kas glodnych miast
        /// (tryb DOPLATA) albo splata rachunkow zywnosci (tryb RACHUNKI).</summary>
        internal static void Before()
        {
            try { KsHunger.Daily(); } catch (Exception e) { Stumble("KsHunger.Daily", e); }
            long t0 = Stopwatch.GetTimestamp();
            int day = Today();
            if (_dDay != day) { ClearDay(); _dDay = day; }
            if (_open.Count > 0) { _nStale += _open.Count; Settle(); }
            var s = Settings.Current;
            try
            {
                if (!On(s) || Campaign.Current == null) return;
                _ksh = KsFoodCarts.SwitchOn && StartCalibration.Applied && StartCalibration.RampDayA >= 0 && day >= StartCalibration.RampDayA;
                // scalenie: RACHUNKI przy kredycie kupcow (Ks Food Carts Consign TAK); przy NIE wozy jada za gotowke przy wysylce - tryb DOPLATA jak w paczce
                bool consign = KsFoodCarts.ConsignOn;
                _bills = consign && FoodBillsOpen != null && FoodBillsPay != null;
                _creditOff = !_bills && consign && CreditCode();             // kupcy na kredycie bez podlaczonych rachunkow - bez doplaty (linia: NIEPODLACZONY)
                if (!_ksh || _creditOff) return;
                if (_bills && EarlyShare(s) > 0) { BeforeEarly(s); return; }   // f4 P5: wczesna splata (nizej); 0 - tryb RACHUNKI jak w f5fd0ae3
                KsHunger.Hungry(_hungry);
                float wcd = Math.Max(1f, s.WarChestDays);
                foreach (var kv in _hungry)
                {
                    try
                    {
                        var t = kv.Key; var r = kv.Value; var st = t.Settlement;
                        if (st == null || st.IsUnderSiege) continue;
                        _nH++;
                        // recenzja: tryb DOPLATA tylko w dobie przegladu celu KS-H (poza nia KsFoodCore.Decide pomija cel - wplata wrocilaby cala); RACHUNKI - kazda doba
                        if (!_bills && !KsFoodCarts.ReviewDay(st, day)) { _nOff++; continue; }
                        long res = 0; try { res = Math.Max(0, Seal31.TownReserve(t)); } catch { res = 0; }
                        long g = t.Gold;
                        double own0 = Budget(g, res);
                        double want;
                        long open = 0;
                        if (_bills)
                        {
                            try { open = Math.Max(0, FoodBillsOpen(st)); } catch (Exception e) { Stumble("FoodBillsOpen", e); open = 0; }
                            _billsOpen += open;
                            if (open <= 0) { _nNoBills++; continue; }
                            want = open;
                        }
                        else
                        {
                            if (!(r.GapValue > 0)) { _nGap0++; continue; }
                            double vWeek = WeekDays * r.GapValue;
                            if (own0 >= vWeek) { _nOk++; continue; }
                            want = vWeek - own0;
                        }
                        _nNeed++;
                        _need += (long)Math.Ceiling(want);
                        var c = st.OwnerClan;
                        var h = c != null ? c.Leader : null;
                        if (c == null || h == null || !h.IsAlive) { _nNb++; continue; }
                        if (c == Clan.PlayerClan || ReferenceEquals(h, Hero.MainHero)) { _nPl++; continue; }   // gracz nigdy (jak chleb M3)
                        double G, R; bool war;
                        if (!ClanBudget.ChestOf(c, out G, out R, out war)) { _nNb++; continue; }
                        long outC, resC; _out.TryGetValue(c, out outC); _resv.TryGetValue(c, out resC);
                        long fam = ClanIncomeBook.FamilyGoldOf(c) + outC;          // wplaty tej doby do innych miast rodu wroca albo zaplaca najwyzej rezerwacje
                        double rate = TownFolk.BreadLordShare(s, war) * Math.Max(0, fam - R) / wcd - TownFolk.BreadLordToday(c) - TourneyPay.RepaidToday(c) - PaidToday(c) - resC;
                        long cap = (long)Math.Floor(Math.Min(Math.Min(rate, fam - R - resC), (double)h.Gold + (_bills ? 0 : outC)));
                        if (cap <= 0) { _nRoom++; continue; }
                        long extra = Math.Min(cap, (long)Math.Ceiling(want));
                        if (_bills)
                        {
                            // tryb RACHUNKI: glowa -> wierzyciele (FoodBillsPay), reszta z powrotem do glowy
                            int h0 = h.Gold; h.ChangeHeroGold(-(int)Math.Min(int.MaxValue, extra)); long took = h0 - h.Gold;
                            CirculationWindows.NoteHeroGold(h, -took);
                            long paidB = 0;
                            try { paidB = FoodBillsPay(st, took); } catch (Exception ex) { Stumble("FoodBillsPay", ex); paidB = 0; }
                            paidB = Math.Max(0, Math.Min(took, paidB));
                            long recv = paidB;                                      // scalenie: przyjete przez kasy zrodel (pomiar PayByLord) - drugi czlon kontroli
                            long back = took - paidB;
                            if (back > 0) { int h1 = h.Gold; h.ChangeHeroGold((int)back); long got = h.Gold - h1; CirculationWindows.NoteHeroGold(h, got); paidB = took - got; }
                            _dLords -= paidB; _billsPaid += paidB; _lordPaid += paidB; _billsRecv += recv;
                            if (paidB > 0) { _nPay++; NotePaid(c, paidB); if (ClanIncomeBook.FamilyGoldOf(c) < R) _nBelow++; if (paidB > cap) _nOver++; }
                            if (paidB > 0) TownPurse.NoteLordFood(st, paidB);         // recenzja calosci 18.10: w doplywie netto miasta jak wydatek kasy (przeglad firm DS)
                            continue;
                        }
                        // tryb DOPLATA: najmniejsza wplata, przy ktorej budzet KS-H kasy rosnie o e (reszta wraca po zakupach)
                        long xmax = Math.Min((long)h.Gold, (long)int.MaxValue - 1 - Math.Max(0, (long)t.Gold));
                        long x = SolveX(g, res, own0, extra, xmax);
                        if (x <= 0) { _nRoom++; continue; }
                        long got2 = Move(h, t, x);
                        if (got2 <= 0) continue;
                        _dep += got2;
                        _open.Add(new Dep { T = t, H = h, C = c, X = got2, G0 = g, Own0 = own0, Cap = extra, Day = day });
                        _out[c] = outC + got2; _resv[c] = resC + extra;
                    }
                    catch (Exception e) { Stumble("Before(miasto)", e); }
                }
            }
            catch (Exception e) { Stumble("Before", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>
        /// f4 P5 (koncowy przeglad ekonomii 18.10, uwaga 6: rachunki KS-H Qarth 143 tys. i Braavos 84 tys. rosna, a pan placil tylko za miasto glodne z rachunkiem juz
        /// zaleglym - gdy rachunek przekroczy zwloke, dowoz staje i przychodzi nagly glod): WCZESNA SPLATA w trybie RACHUNKI. Ten sam mechanizm i te same granice co tryb
        /// RACHUNKI w Before (glowa rodu wlasciciela, zloto ponad rezerwe wojny R i tempo skrzyni doby jak chleb M3, gracz nigdy, rod bez budzetu / dluznik / Straz nie),
        /// tylko wczesniejszy prog i kazde miasto rodu: rachunki zywnosci miasta starsze niz floor(Ks Lord Food Bills Early Share x Ks Consign Overdue Days) dni (0.67 x 30
        /// = 20), najstarsze najpierw (KsFoodCredit.PayByLordAge). Kolejnosc miast doby: najpierw glodne (kolejnosc KsHunger, jak dotad), potem syte - od najstarszego
        /// rachunku, potem StringId (tempo rodu wspolne - PaidToday). Bez reguly dla krain. Zloto: glowa -> kasy zrodel (wierzyciele, pomiar), czego nie przyjma - wraca do
        /// glowy; roszczenie maleje o tyle samo (kontrola linii: zloto panow + kasy miast + wierzyciele = 0, jak dotad). Miasto oblezone - nie (jak dotad).
        /// </summary>
        private static void BeforeEarly(Settings s)
        {
            int age = EarlyAge(s);
            _eAge = age;
            if (age < 0) return;
            KsFoodCredit.OlderThanByDst(age, _eSum, _eOld);
            KsHunger.Hungry(_hungry);
            float wcd = Math.Max(1f, s.WarChestDays);
            _eList.Clear(); _eSeen.Clear();
            foreach (var kv in _hungry) if (kv.Key != null && _eSeen.Add(kv.Key)) _eList.Add(kv.Key);
            int nHungry = _eList.Count;
            var rest = new List<KeyValuePair<Settlement, int>>();
            foreach (var kv in _eOld) { var t0 = kv.Key != null ? kv.Key.Town : null; if (t0 != null && !_eSeen.Contains(t0)) rest.Add(kv); }
            rest.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : string.CompareOrdinal(a.Key.StringId, b.Key.StringId));
            foreach (var kv in rest) { _eList.Add(kv.Key.Town); _eSeen.Add(kv.Key.Town); }
            foreach (var kv in _eSum) _eOpen += Math.Max(0, kv.Value);
            for (int i = 0; i < _eList.Count; i++)
            {
                try
                {
                    var t = _eList[i]; var st = t != null ? t.Settlement : null;
                    bool hungry = i < nHungry;
                    if (st == null || st.IsUnderSiege) continue;
                    if (hungry) _nH++; else _eSated++;
                    long open; _eSum.TryGetValue(st, out open); open = Math.Max(0, open);
                    _billsOpen += open;
                    if (open <= 0) { _nNoBills++; continue; }                   // tylko miasto glodne bez rachunkow ponad prog
                    if (hungry) _nNeed++;
                    _need += open;
                    var c = st.OwnerClan;
                    var h = c != null ? c.Leader : null;
                    if (c == null || h == null || !h.IsAlive) { _nNb++; continue; }
                    if (c == Clan.PlayerClan || ReferenceEquals(h, Hero.MainHero)) { _nPl++; continue; }   // gracz nigdy (jak chleb M3)
                    double G, R; bool war;
                    if (!ClanBudget.ChestOf(c, out G, out R, out war)) { _nNb++; continue; }
                    // ta sama granica co tryb RACHUNKI w Before (nawias doplaty pusty w tym trybie - outC i resC 0)
                    long outC, resC; _out.TryGetValue(c, out outC); _resv.TryGetValue(c, out resC);
                    long fam = ClanIncomeBook.FamilyGoldOf(c) + outC;
                    double rate = TownFolk.BreadLordShare(s, war) * Math.Max(0, fam - R) / wcd - TownFolk.BreadLordToday(c) - TourneyPay.RepaidToday(c) - PaidToday(c) - resC;
                    long cap = (long)Math.Floor(Math.Min(Math.Min(rate, fam - R - resC), (double)h.Gold));
                    if (cap <= 0) { _nRoom++; continue; }
                    long extra = Math.Min(cap, open);
                    // glowa -> wierzyciele (PayByLordAge), reszta z powrotem do glowy
                    int h0 = h.Gold; h.ChangeHeroGold(-(int)Math.Min(int.MaxValue, extra)); long took = h0 - h.Gold;
                    CirculationWindows.NoteHeroGold(h, -took);
                    long paidB = 0;
                    try { paidB = KsFoodCredit.PayByLordAge(st, took, age); } catch (Exception ex) { Stumble("PayByLordAge", ex); paidB = 0; }
                    paidB = Math.Max(0, Math.Min(took, paidB));
                    long recv = paidB;
                    long back = took - paidB;
                    if (back > 0) { int h1 = h.Gold; h.ChangeHeroGold((int)back); long got = h.Gold - h1; CirculationWindows.NoteHeroGold(h, got); paidB = took - got; }
                    _dLords -= paidB; _billsPaid += paidB; _lordPaid += paidB; _billsRecv += recv; _ePaid += paidB;
                    if (paidB > 0) TownPurse.NoteLordFood(st, paidB);                 // recenzja calosci 18.10: w doplywie netto miasta jak wydatek kasy (przeglad firm DS)
                    if (paidB > 0)
                    {
                        _nPay++; NotePaid(c, paidB);
                        if (!hungry) _eSatedPaid++;
                        if (ClanIncomeBook.FamilyGoldOf(c) < R) _nBelow++;
                        if (paidB > cap) _nOver++;
                    }
                }
                catch (Exception e) { Stumble("BeforeEarly(miasto)", e); }
            }
        }

        /// <summary>Tuz po KsFoodCarts.Daily: rozliczenie nawiasu (kasa najpierw do swojego budzetu, pan reszte, niewydane wraca do pana) i linia doby.</summary>
        internal static void After()
        {
            long t0 = Stopwatch.GetTimestamp();
            try { Settle(); } catch (Exception e) { Stumble("After", e); }
            _ticks += Stopwatch.GetTimestamp() - t0;
            try { Line(); } catch (Exception e) { Stumble("Line", e); }
        }

        private static void Settle()
        {
            int bookDay = int.MinValue;
            foreach (var d in _open)
            {
                try
                {
                    // recenzja: ksiega kupcow KS-H doby wplaty (zaplacone przez kase celu za wozy kupione tej doby) - niezalezny pomiar obok delty kasy
                    if (d.Day != bookDay) { bookDay = d.Day; try { KsFoodCarts.PaidAsDstOn(d.Day, _bookOf); } catch (Exception e) { _bookOf.Clear(); Stumble("Settle(ksiega)", e); } }
                    long book = 0; if (d.T.Settlement != null) _bookOf.TryGetValue(d.T.Settlement, out book);
                    long g1 = d.T.Gold;
                    long spent = Math.Max(0, d.G0 + d.X - g1);                     // wydatek KS-H tej kasy w nawiasie (przychody miasta jako zrodla go zmniejszaja)
                    if (spent > book) { _nMis++; _mis += spent - book; }             // kasa netto ponad ksiege kupcow: w nawiasie ruszyl kase ktos poza KS-H (oczekiwane 0)
                    long pDelta = (long)Math.Ceiling(spent - d.Own0), pBook = (long)Math.Ceiling(book - d.Own0);
                    long pMax = Math.Min(d.X, d.Cap);
                    if (pBook < pDelta && pBook < pMax) _nBookCut++;                 // udzial pana uciety ksiega (tylko przy takim ruchu kasy spoza KS-H)
                    long p = Math.Max(0, Math.Min(pMax, Math.Min(pDelta, pBook)));
                    _book += book;
                    long r = d.X - p;
                    long back = r > 0 ? -Move(d.H, d.T, -r) : 0;
                    if (back < r) { _nCut++; _cut += r - back; }                     // kasa miasta za mala na pelny zwrot - pan zaplacil o tyle wiecej (licznik)
                    long paid = d.X - back;
                    _ret += back; _lordPaid += paid; _spend += spent;
                    if (paid > 0 && d.T != null) TownPurse.NoteLordFood(d.T.Settlement, paid);   // recenzja calosci 18.10: doplata pana, ktora zostala w kasie - jak wyzej
                    if (paid > 0)
                    {
                        _nPay++; NotePaid(d.C, paid);
                        if (paid > d.Cap) _nOver++;
                    }
                }
                catch (Exception e) { Stumble("Settle(miasto)", e); }
            }
            // kontrola po rozliczeniu wszystkich miast (dopiero teraz zloto rodu jest z powrotem u glowy): rod placacy dzis ponizej rezerwy wojny (oczekiwane 0)
            foreach (var c in _resv.Keys)
            {
                try { double G, R; bool war; if (c != null && ClanBudget.ChestOf(c, out G, out R, out war) && PaidToday(c) > 0 && ClanIncomeBook.FamilyGoldOf(c) < R) _nBelow++; }
                catch (Exception e) { Stumble("Settle(rezerwa)", e); }
            }
            _open.Clear(); _out.Clear(); _resv.Clear();
        }

        private static void Line()
        {
            var s = Settings.Current;
            if (s == null || !s.LogEnabled || Campaign.Current == null) return;
            if (!On(s) && _dep == 0 && _lordPaid == 0) return;                     // klucz wylaczony i nic sie nie dzialo - bez linii
            var sb = new StringBuilder(1200);
            sb.Append("Pan miasta placi za dowoz (KS): dzien ").Append(Today())
              .Append(" | klucz Ks Lord Funds Food ").Append(On(s) ? "tak" : "nie")
              .Append(" | tryb ").Append(_bills ? "RACHUNKI KUPCOW (r4-kupcy)" : _creditOff ? "KREDYT KUPCOW NIEPODLACZONY - doplata wylaczona (slad " + _creditWhy + ")" : "DOPLATA KASY NA CZAS ZAKUPOW KS-H")
              .Append(" | KS-H ").Append(_ksh ? "czynne" : "nieczynne")
              .Append(" | miasta glodne ").Append(_nH).Append(": kasa sama starcza ").Append(_nOk).Append(", bez luki rynku dzis ").Append(_nGap0)
              .Append(", bez rachunkow ").Append(_nNoBills).Append(", do doplaty ").Append(_nNeed)
              .Append(", poza doba przegladu KS-H (cel co 2. doba) ").Append(_nOff)
              .Append(" | platnik: pan placi ").Append(_nPay).Append(" (rodow ").Append(_clans.Count).Append("), gracz ").Append(_nPl)
              .Append(" (nigdy), rod bez budzetu / dluznik / Straz ").Append(_nNb).Append(", bez miejsca ponad rezerwe wojny i tempo skrzyni ").Append(_nRoom)
              .Append(" | wplacone na czas zakupow ").Append(_dep).Append(" zl, zwrocone ").Append(_ret).Append(" zl, zaplacone przez panow ").Append(_lordPaid)
              .Append(" zl (potrzeba ").Append(_need).Append(" zl)")
              .Append(" | wydatek KS-H tych kas ").Append(_spend).Append(" zl: z kasy miasta ").Append(Math.Max(0, _spend - (_lordPaid - _billsPaid)))
              .Append(", od panow ").Append(_lordPaid - _billsPaid)
              .Append(" | ksiega kupcow KS-H: zaplacone kupcom przez te kasy ").Append(_book).Append(" zl (towar + fracht wozow kupionych dzis), od panow ").Append(_lordPaid - _billsPaid)
              .Append(" zl, udzial pana uciety ksiega ").Append(_nBookCut).Append(", kasa netto ponad ksiege ").Append(_nMis).Append(" miast (").Append(_mis).Append(" zl)")
              .Append(" | rachunki: otwarte ").Append(_billsOpen).Append(" zl, splacone przez panow ").Append(_billsPaid).Append(" zl, kasy zrodel (wierzyciele) przyjely ").Append(_billsRecv)
              .Append(_eAge >= 0
                  ? " zl (rachunki starsze niz " + _eAge.ToString(Inv) + " dni - wczesna splata f4, przed zwloka " + KsFoodCredit.OverdueDays.ToString(Inv) + " dni, ktora blokuje dowoz; takze miasta syte; najstarsze najpierw)"
                  : " zl (tylko rachunki zalegle - starsze niz " + KsFoodCredit.OverdueDays.ToString(Inv) + " dni, blokuja dowoz; najstarsze najpierw)")
              .Append(_eAge >= 0
                  ? " | wczesna splata (f4, Ks Lord Food Bills Early Share " + EarlyShare(s).ToString("0.00", Inv) + "): prog " + _eAge.ToString(Inv) + " dni, miasta syte z rachunkami ponad prog "
                    + _eSated.ToString(Inv) + " (pan splacil " + _eSatedPaid.ToString(Inv) + "), rachunki ponad prog " + _eOpen.ToString(Inv) + " zl, splacone przez panow " + _ePaid.ToString(Inv) + " zl"
                  : "")
              .Append(" | kontrola: zloto panow ").Append(_dLords).Append(" + kasy miast ").Append(_dTowns).Append(" + wierzyciele ").Append(_billsRecv)
              .Append(" = ").Append(_dLords + _dTowns + _billsRecv).Append(" (oczekiwane 0), zaplacone - (wplacone - zwrocone + rachunki) = ").Append(_lordPaid - (_dep - _ret + _billsPaid))
              .Append(" | pan ponizej rezerwy wojny po zaplacie ").Append(_nBelow).Append(", ponad tempo skrzyni ").Append(_nOver)
              .Append(", zwrot uciety (kasa za mala) ").Append(_nCut).Append(" (").Append(_cut).Append(" zl), zaleglosc nawiasu ").Append(_nStale)
              .Append(" | czas ").Append((_ticks * 1000.0 / Stopwatch.Frequency).ToString("0.000", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            _stumbles = 0;
        }
    }
}
