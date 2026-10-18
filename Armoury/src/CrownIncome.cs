using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace Armoury
{
    /// <summary>
    /// PACZKA 165 - KORONA Z BIEZACYCH WPLYWOW (projekt etapu 2, krok C1; PLAN 2.3, 2.6). Wylacznik glowny CrownCurrentIncome, czynny tylko
    /// razem z ClanBudgetEnabled (R1: "165 nigdy bez 166" - zwrot z samych wplywow bez hamulca wojska to ok. -170 tys. zl/dobe dla rodow).
    ///
    /// Regula (rozdz. 165 pkt 1-6):
    ///  - WPLYWY DNIA krolestwa = to, co od wczorajszego zamkniecia korony wplynelo do skarbca: powinnosci, danina wojenna, clo, 1/3 zaworu zamkow
    ///    (114), podatek gry/BK od bogatych rodow, splaty dlugu wobec korony, trybut i wszystko inne (zmiana skarbca od wczorajszej migawki -
    ///    gra, BK, Diplomacy) + raty reparacji przyjete (dochodza w trakcie) + 1/CrownReserveReleaseDays (450 od strojenia po tescie 120 dob, dotad 360) zapasu ponad CrownReserveGold
    ///    (500 000). Zapas = skarbiec minus dzisiejsze wplywy; 1/360 zawiera sie we wplywach (nigdzie drugi raz).
    ///  - KOLEJNOSC WYDATKOW: rata kredytu korony w Banku (186, CrownBorrow) -> dary (182) -> raty reparacji -> kontrakty najemnikow (185) -> wezwania sojusznikow do wojny (168, CrownCallToWar) -> (nagroda za wielkiego jenca - 178, krok D) -> zwrot
    ///    zoldu (186: brak zwrotu korona w wojnie pozycza w Banku - CrownBorrow.Lend) -> renty 180 (krok C2, CrownRents: reszta do rodow wedlug lenn, udzialy
    ///    rodow bez warunku zostaja w skarbcu). Czego nie ma - nie jest placone (niedoplata przepada, bez dlugu korony wobec rodow).
    ///  - REPARACJE Diplomacy (KingdomWalletCost.ApplyCost z portfelami "Reparations"): zamiast zabrac placacemu skarbiec ponad 2 mln i dlug
    ///    trybutu rodow, a odbiorcy dac z gory 1/3 krolowi i 1/6 najemnikom - dlug korona A -> korona B, rata najwyzej CrownReparationShare (50%)
    ///    wplywow dnia A, B dostaje dokladnie rate do skarbca. Rody nie placa, DebtToKingdom z reparacji nie powstaje.
    ///  - SPLATA DebtToKingdom (stary zapis; gra AddPaymentForDebts): z kiesy rodu do skarbca jego krolestwa (dotad w nicosc) - poza splata zaliczki gry
    ///    (recenzja C1, OBIEG-1: czesc dlugu, ktora gra uznala odbiorcy portfela z niczego - jej splata dalej w nicosc).
    ///  - Zwrot (KingdomTreasury.WageRefund): tylko partie w polu (bez zalog), minus wydatki ich ludzi we wlasnych miastach rodu (K3).
    ///
    /// Platnik -> odbiorca: skarbiec A -> skarbiec B (raty), glowa rodu -> skarbiec (splata dlugu). Zadnego zlota z niczego; kazdy przelew
    /// ma licznik w linii "Korona: wplywy dnia (165)" i w "Obieg" (korona). Zapis: migawka skarbcow i dlugi reparacji (SaveText, "arm_crown165").
    /// </summary>
    internal static class CrownIncome
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.CrownCurrentIncome && s.ClanBudgetEnabled && s.CrownWageRefundEnabled; }
        }

        // ------------------------------------------------------------ stan doby na krolestwo
        internal sealed class KDay
        {
            public long Wallet0, Own, Other, Measured, Stock, Release, Spend, Left;
            public long GiftOut, GiftIn, RepOut, RepIn, Contract, RefundDue, RefundGiven;
            public long RefundFull;       // f6-zwrot (recenzja): pelny zwrot zoldu krolestwa w wojnie (zold po cieciach W5/W7/K3 x 70%, PRZED mnoznikiem potrzeby rodu) - tylko do zapasu wojny
                                          // (pierscien 184, KingdomTreasury.WarReserveOf, sonda korony): zapas zostaje taki jak przed f6; klucz wylaczony - rowny RefundDue
            public long Rent, RentHeld;   // 180: renty do glow rodow i udzialy wstrzymane (rody bez warunku, zaokraglenia - zostaja w skarbcu)
            public long CallToWar;        // 168 dodatek: czesc dnia ceny wezwania sojusznika do wojny (skarbiec -> portfel wezwanego)
            public long LoanIn, LoanOut;  // 186: kredyt Banku na zwrot zoldu (kapital -> skarbiec) i rata (skarbiec -> kapital, pierwszy wydatek dnia)
            public long WarTax;           // E5/B2 (184): podatek wojenny ludu od wczorajszego zamkniecia korony (kiesy ludu -> skarbiec) - w Own
            public long WarAid;           // P1 (obieg kiesy ludu): danina z oszczednosci ludu od wczorajszego zamkniecia korony (kiesy ludu -> skarbiec) - w Own
            public long NeedAid;          // W5: doplata do zoldu wedlug potrzeby wyplacona tej doby (skarbiec -> glowy rodow; po zwrocie zoldu, przed rentami 180)
            public long CrownAid;         // W5b regula 2: doplata W5 wyplacona tej doby ze skarbca PONAD ZAPAS WOJNY (skarbiec -> glowy rodow; po szczeblu daniny, przed tarczowym); POZA wplywami dnia (Spend / Left) - z zapasu
            public long Bread;            // M3 (pieniadze roku 2): chleb dla glodnych osad krolestwa z reszty wplywow dnia przed rentami (TownFolk.BreadCrown; skarbiec -> kiesy ludu)
            public long KingOut;          // L1 (pieniadze roku 2): czesc RepOut - raty okupow krolow (178, dlug Kind 'k'); reszta RepOut to reparacje
            public long NeedAidRent;      // M1 (pieniadze roku 2): doplata W5 z reszty wplywow dnia przed rentami (KingdomTreasury.RentAid.cs) - obok NeedAid (szczebel daniny), nie w nim
            public long ScutIn, ScutOut;  // W6: tarczowe tej doby - pobrane (glowy platnikow -> skarbiec) i wyplacone przez W5 ponad danine (skarbiec -> glowy rodow); POZA wplywami dnia (Spend / Left)
            public long ValveKept; public double ValveR;   // E3-G5: czesc korony z zaworu miast zatrzymana w zapasie (z puli rent, po M3) i jej udzial r tej doby
            public long Court;            // E3-P1 (b): dwor krolewski - towar z polki stolicy z reszty wplywow dnia (po M3 i G5, przed rentami; skarbiec -> kasa stolicy)
            public long DsMov;            // DS F3 (M9): dziesiata od ruchomosci od wczorajszego zamkniecia korony (kiesy ludu i kasy -> skarbiec) - w Own
            public long DsTreas, DsAnt;   // DS F3 (K5): brak zwrotu zoldu pokryty ze skarbca ponad zapas wojny (poza wplywami dnia) i zaliczka mieszczan pod nadanie (L1)
            public long Windfall;         // ds-akumulator (CrownWindfallToReserve): czesc "inne" ponad 3 x mediana 28 dob krolestwa - do zapasu (Stock, 1/450), nie wplyw dnia
            public bool Snap;
        }

        private static readonly Dictionary<Kingdom, KDay> _day = new Dictionary<Kingdom, KDay>();
        private static readonly Dictionary<string, long> _wEnd = new Dictionary<string, long>();   // id krolestwa -> skarbiec przy zamknieciu korony
        private static int _wEndDay = -1;                                                         // doba gry tej migawki
        private static bool _open;                                                                // Begin bylo dzis, End jeszcze nie
        // M2b (CallToWarAfterOwnWages): zwrot zoldu nalezny wczoraj na krolestwo (migawka przy End) - rezerwa wezwan 168 nastepnej doby (pamiec sesji)
        private static readonly Dictionary<string, long> _refDueY = new Dictionary<string, long>(); private static int _refDueYDay = -1;

        // dlugi reparacji (korona -> korona)
        // 178: Kind 'k' - okup krola placony przez skarbiec (Receiver: id krolestwa albo "h:" + id bohatera porywacza bez krolestwa); 'r' - reparacje
        private sealed class Debt { public string Payer, Receiver; public long Left, Total; public int Day; public char Kind = 'r'; }
        internal static long LastKingRansomPaid; private static long _dKingRansomPaid;
        private static readonly List<Debt> _debts = new List<Debt>();

        // wydatki ludzi partii w polu we wlasnych miastach rodu od ostatniego zwrotu (K3)
        private static readonly Dictionary<Clan, long> _ownSpend = new Dictionary<Clan, long>();

        // recenzja C1 (OBIEG-1): ZALICZKA GRY w dlugu wobec korony. Gra uznaje portfel najemnikow / trybutu / wezwania do wojny w calosci, choc rod nie
        // mial na swoj udzial (brak dopisuje do DebtToKingdom) - odbiorca dostal te kwote z niczego. Czesc tego przyrostu SoldierPay rownowazy obcietym
        // zoldem (zloto zeszlo z kiesy, a nie trafilo do ludzi); reszta to zaliczka z niczego. Przed C1 splata dlugu ginela i domykala ten cykl; od C1 idzie
        // do skarbca - bez tej ewidencji ta sama kwota istnialaby dwa razy (u odbiorcy zaliczki i w skarbcu). Splata najpierw gasi zaliczke (w nicosc, jak przed
        // C1), do skarbca idzie tylko reszta (dlug z obcietego zoldu i dlugi ze starego zapisu - bez wpisu tutaj - tabela 165). Zrodlo (uznawanie portfela
        // w calosci) zamyka 168 (krok D). Zapis w arm_crown165.
        private static readonly Dictionary<string, long> _advance = new Dictionary<string, long>();   // id rodu -> niesplacona zaliczka gry (najwyzej dlug)
        internal static long LastAdvanceRepaid, LastAdvanceNew;
        private static long _dAdvRepaid, _dAdvNew;

        // ds-akumulator (CrownWindfallToReserve, noc 17/18.10; diagnoza fin80 "dochod": Dorne doba 109180 dostalo 1 218 874 zl "inne" i tego samego dnia oddalo 1 201 594
        // w rentach 180 - jednorazowy wplyw siedzial potem 28 dob w "D stalym" rodow): pierscien 28 dob "inne" krolestwa (pamiec sesji, bez zapisu - po wczytaniu od 7 probek)
        private const int WfRing = 28, WfMin = 7; private const double WfTimes = 3.0; private const long WfFloor = 1000;
        private static readonly Dictionary<string, double[]> _wfRing = new Dictionary<string, double[]>();
        private static readonly Dictionary<string, int> _wfN = new Dictionary<string, int>();
        /// <summary>Linia "Akumulator kies": jednorazowe "inne" odlozone do zapasu w ostatniej dobie korony (zl, krolestw).</summary>
        internal static long LastWindfall; internal static int LastWindfallN;
        private static long _dWf; private static int _dWfN;

        /// <summary>Czesc "inne" krolestwa ponad WfTimes x max(mediana jego "inne" z ostatnich 28 dob, "inne" doby poprzedniej) (ujemne jako 0) - do zapasu: skok jednej
        /// doby (okup, dar, lup), nie nowy staly poziom (trybut - od drugiej doby nie jest jednorazowy). Probka dopisana po decyzji (mediana z dob
        /// poprzednich). Klucz wylaczony, brak migawki albo < 7 probek - 0 (pierscien biegnie dalej, wlaczenie w trakcie bez skoku). Nie rzuca.</summary>
        private static long WindfallOf(Kingdom k, KDay d, Settings s)
        {
            try
            {
                if (k == null || k.StringId == null || d == null || !d.Snap) return 0;
                double[] ring; int n;
                if (!_wfRing.TryGetValue(k.StringId, out ring)) { ring = new double[WfRing]; _wfRing[k.StringId] = ring; }
                _wfN.TryGetValue(k.StringId, out n);
                long wf = 0;
                int have = Math.Min(n, WfRing);
                if (s != null && s.CrownWindfallToReserve && have >= WfMin && d.Other > 0)
                {
                    var tmp = new double[have];
                    for (int i = 0; i < have; i++) tmp[i] = Math.Max(0, ring[i]);
                    Array.Sort(tmp);
                    double med = have % 2 == 1 ? tmp[have / 2] : 0.5 * (tmp[have / 2 - 1] + tmp[have / 2]);
                    double prev = Math.Max(0, ring[(n - 1 + WfRing) % WfRing]);   // skok jednej doby, nie nowy poziom (trybut, wojna): od drugiej doby poprzednia podnosi prog
                    double cap = WfTimes * Math.Max(med, prev);
                    if (d.Other > cap && d.Other - cap >= WfFloor) wf = (long)Math.Floor(d.Other - cap);
                }
                ring[n % WfRing] = d.Other; _wfN[k.StringId] = n + 1;
                return wf;
            }
            catch (Exception e) { Stumble("WindfallOf", e); return 0; }
        }

        // liczniki doby (linia i "Obieg")
        internal static long LastMeasured, LastOwn, LastOther, LastRelease, LastSpend, LastGiftOut, LastRepOut, LastRepIn, LastContract, LastRefund, LastLeft;
        internal static long LastRent, LastRentHeld;   // 180
        internal static long LastCallToWar;            // 168 dodatek: wezwania do wojny
        internal static long LastLoanIn, LastLoanOut;  // 186: kredyt korony z Banku i raty
        internal static long LastDebtRepaid, LastOwnCut, LastNewDebt; internal static int LastNewDebtN;
        private static long _dDebtRepaid, _dNewDebt, _dDropped; private static int _dNewDebtN, _dDroppedN;
        private static long _dNewKing; private static int _dNewKingN;   // L1: nowe dlugi koron z okupow krolow (178) od wczorajszej linii
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _repWired, _debtWired;
        private static int _importN = -1, _importBad;

        internal static void Reset()
        {
            _day.Clear(); _wEnd.Clear(); _wEndDay = -1; _open = false; _debts.Clear(); _ownSpend.Clear(); _advance.Clear();
            _wfRing.Clear(); _wfN.Clear(); LastWindfall = 0; LastWindfallN = 0; _dWf = 0; _dWfN = 0;   // ds-akumulator: pierscien "inne" - pamiec sesji
            _refDueY.Clear(); _refDueYDay = -1;   // M2b
            _dNewKing = 0; _dNewKingN = 0;        // L1 (recenzja): nowe dlugi okupow krolow - licznik linii 165 czyszczony miedzy kampaniami
            ZeroLast(); _dDebtRepaid = _dNewDebt = _dDropped = 0; _dNewDebtN = _dDroppedN = 0; _dAdvRepaid = _dAdvNew = 0;
            _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
            KingdomTreasury.WarTaxReset();   // E5/B2 (184): stawki podatku wojennego ludu - zapisane w szostym polu tego klucza, czyszczone razem z nim
        }

        internal static void ZeroLast()
        {
            LastMeasured = LastOwn = LastOther = LastRelease = LastSpend = LastGiftOut = LastRepOut = LastRepIn = LastContract = LastRefund = LastLeft = 0;
            LastRent = LastRentHeld = 0; LastCallToWar = 0; LastLoanIn = LastLoanOut = 0;
            LastDebtRepaid = LastOwnCut = LastNewDebt = 0; LastNewDebtN = 0; LastAdvanceRepaid = LastAdvanceNew = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownIncome." + where, e); } catch { }
        }

        /// <summary>Stan doby krolestwa (null, gdy 165 dzis nie liczylo - wylaczone albo krolestwo bez wpisu).</summary>
        internal static KDay DayOf(Kingdom k)
        {
            KDay d;
            return k != null && _open && _day.TryGetValue(k, out d) ? d : null;
        }

        /// <summary>M2b (CallToWarAfterOwnWages): zwrot zoldu nalezny krolestwu wczoraj (KDay.RefundDue przy wczorajszym End); 0 - brak migawki (pierwsza doba sesji, 165 wylaczone).</summary>
        internal static long RefundDueYesterday(Kingdom k)
        {
            long v;
            return k != null && k.StringId != null && Campaign.Current != null && _refDueYDay == (int)CampaignTime.Now.ToDays - 1 && _refDueY.TryGetValue(k.StringId, out v) ? Math.Max(0, v) : 0;
        }

        /// <summary>Ile z wplywow dnia krolestwo moze jeszcze wydac (0 poza 165).</summary>
        internal static long LeftFor(Kingdom k) { var d = DayOf(k); return d != null ? Math.Max(0, Math.Min(d.Left, (long)k.KingdomBudgetWallet)) : 0; }

        /// <summary>
        /// E3-G5 (klucz CrownKeepsValveShare, DOMYSLNIE WYLACZONY - dzwignia awaryjna; uwagi recenzji 1 i 2: w Begin cielo by caly dzien korony, takze zwrot zoldu,
        /// i dublowalo regulator czesci korony straznika 5 x brak do 22%): CrownRents.Daily po M3, tuz przed podzialem rent - gdy wczorajszy udzial koron
        /// w zlocie swiata (MoneyLedger.LastShares) < FolkGuardCrownTarget, czesc r = min(0.5, 5 x brak) udzialu korony z zaworu miast tej doby (TownPurse.CrownToday)
        /// zostaje w skarbcu jako zapas (Spent - nie idzie w renty 180; jutro w Stock, wychodzi po 1/CrownReserveReleaseDays). Tylko z puli rent (`pool`), nigdy
        /// z rat, darow, kontraktow, zwrotu zoldu, W5 i M1 (te biegna wczesniej). Doba z kredytem Banku 186 (LoanIn > 0) - 0. Zwraca zatrzymane. Nie rzuca.
        /// </summary>
        internal static long KeepValve(Kingdom k, long pool)
        {
            try
            {
                var s = Settings.Current;
                var d = DayOf(k);
                if (s == null || !s.CrownKeepsValveShare || d == null || pool <= 0) return 0;
                if (d.LoanIn > 0) { _dValveLoanN++; return 0; }
                long world, folk, lords, crowns;
                if (!MoneyLedger.LastShares(out world, out folk, out lords, out crowns) || world <= 0) return 0;
                double tgt = Math.Max(0, Math.Min(0.5, s.FolkGuardCrownTarget)), share = (double)crowns / world;
                double r = Math.Min(0.5, 5.0 * (tgt - share));
                if (!(r > 0)) return 0;
                long tow; TownPurse.CrownToday.TryGetValue(k, out tow);
                long keep = Math.Min((long)(r * Math.Max(0, tow)), pool);
                if (keep <= 0) return 0;
                Spent(k, keep);
                d.ValveKept += keep; d.ValveR = r; _dValveShare = share;
                return keep;
            }
            catch (Exception e) { Stumble("KeepValve", e); return 0; }
        }
        private static int _dValveLoanN; private static double _dValveShare = double.NaN;

        /// <summary>E3-P1 (b): dwor krolewski zaplacil dzis kasie stolicy (licznik linii 165; zloto zeszlo juz ze skarbca i jest w Spent).</summary>
        internal static void NoteCourt(Kingdom k, long amount)
        {
            KDay d;
            if (k != null && amount > 0 && _day.TryGetValue(k, out d)) d.Court += amount;
        }

        /// <summary>Wydatek z wplywow dnia (dar, kontrakt, zwrot) - skarbiec placi wolajacy; tu tylko licznik reszty.</summary>
        internal static void Spent(Kingdom k, long amount)
        {
            var d = DayOf(k);
            if (d == null || amount <= 0) return;
            d.Left = Math.Max(0, d.Left - amount);
        }

        /// <summary>Wplyw do wydania tego samego dnia (np. dar przechodzacy przez skarbiec odbiorcy) - dopisany do reszty krolestwa.</summary>
        internal static void Received(Kingdom k, long amount)
        {
            var d = DayOf(k);
            if (d == null || amount <= 0) return;
            d.Left += amount;
        }

        // ------------------------------------------------------------ K3: wydatki ludzi partii w polu we wlasnych miastach rodu
        /// <summary>MenPurse.OnLeft: ludzie partii rodu wydali "na zycie" w miescie tego samego rodu - zwrot korony od tego zoldu przepada (K3).</summary>
        internal static void NoteOwnSpend(Clan c, int amount)
        {
            try
            {
                if (c == null || amount <= 0 || !On) return;
                long v; _ownSpend.TryGetValue(c, out v); _ownSpend[c] = v + amount;
            }
            catch (Exception e) { Stumble("NoteOwnSpend", e); }
        }

        /// <summary>Wydatki we wlasnych miastach od ostatniego zwrotu, na rod - oproznia licznik (wola WageRefund).</summary>
        internal static Dictionary<Clan, long> TakeOwnSpend()
        {
            var d = new Dictionary<Clan, long>(_ownSpend);
            _ownSpend.Clear();
            return d;
        }

        // ------------------------------------------------------------ poczatek: wplywy dnia i 1/360 zapasu (po powinnosciach, daninie i cle)
        internal static void Begin()
        {
            _day.Clear(); _open = false;
            _dWf = 0; _dWfN = 0; LastWindfall = 0; LastWindfallN = 0;   // ds-akumulator: jednorazowe "inne" tej doby korony (linia 165 i "Akumulator kies")
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!On) { _ownSpend.Clear(); return; }
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                bool snapOk = _wEndDay >= 0 && today - _wEndDay == 1;   // migawka z wczorajszego zamkniecia korony
                long reserve = Math.Max(0, s.CrownReserveGold);
                double days = Math.Max(1.0, s.CrownReserveReleaseDays);
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        var d = new KDay();
                        long w = k.KingdomBudgetWallet;
                        d.Wallet0 = w;
                        long dues; KingdomTreasury.DuesToday.TryGetValue(k, out dues);
                        long lev; KingdomTreasury.LeviesToday.TryGetValue(k, out lev);
                        long cas; CastlePurse.CrownToday.TryGetValue(k, out cas);
                        long tow; TownPurse.CrownToday.TryGetValue(k, out tow);   // E5/A1: udzial korony z zaworu kas miast (ta sama doba, PopulationLaw.Daily)
                        long thirds = Ransom178.ThirdsOf(k);   // 178: 1/9 z licznikow doby (prawo trzecich) - wplacone przed Begin
                        long wt = KingdomTreasury.FolkWarTaxWindow(k);   // E5/B2 (184): podatek wojenny ludu pobrany przy zarobku od wczorajszego End
                        d.WarTax = wt;
                        long wa = KingdomTreasury.FolkWarAidWindow(k);   // P1: danina z oszczednosci ludu pobrana w dobie ludu (TownFolk.PayAid) od wczorajszego End
                        d.WarAid = wa;
                        long dm = DsSeason.MovWindow(k);                 // DS F3 (M9): dziesiata od ruchomosci (kiesy ludu w dobie ludu, kasy w dzien raty) od wczorajszego End
                        d.DsMov = dm;
                        d.Own = dues + lev + cas + tow + thirds + wt + wa + dm;
                        long w0;
                        if (snapOk && _wEnd.TryGetValue(k.StringId, out w0)) { d.Snap = true; d.Other = w - w0 - d.Own; }
                        else d.Other = 0;                               // brak wczorajszej migawki (wczytanie starego zapisu, nowe krolestwo) - tylko nasze liczniki
                        d.Windfall = WindfallOf(k, d, s);               // ds-akumulator: jednorazowe "inne" do zapasu (klucz wylaczony - 0, wzor jak dotad)
                        if (d.Windfall > 0) { _dWf += d.Windfall; _dWfN++; }
                        d.Measured = Math.Max(0, d.Own + d.Other - d.Windfall);     // netto: wydatki skarbca spoza nas (Diplomacy, gra) zmniejszaja wplywy dnia
                        d.Stock = Math.Max(0, w - d.Measured);
                        d.Release = (long)(Math.Max(0, d.Stock - reserve - TownFolk.CrownTownDebt(k)) / days);   // korona2: zloto pozyczone od miast to zapas trzymany, nie do rozdania (klucz wylaczony - 0)
                        d.Spend = Math.Max(0, Math.Min(Math.Max(0, w), d.Measured + d.Release));
                        d.Left = d.Spend;
                        _day[k] = d;
                    }
                    catch (Exception e) { Stumble("Begin(krolestwo)", e); }
                }
                _open = true;
                LastWindfall = _dWf; LastWindfallN = _dWfN;
            }
            catch (Exception e) { Stumble("Begin", e); }
        }

        // ------------------------------------------------------------ raty reparacji (po darach 182, przed kontraktami 185)
        internal static void Reparations()
        {
            if (!_open || !On) return;
            var s = Settings.Current;
            try
            {
                float share = Math.Max(0f, Math.Min(1f, s.CrownReparationShare));
                var paidBy = new Dictionary<Kingdom, long>();
                for (int i = 0; i < _debts.Count; i++)
                {
                    var debt = _debts[i];
                    try
                    {
                        var a = FindKingdom(debt.Payer);
                        Kingdom b = null; Hero bh = null;
                        if (debt.Receiver != null && debt.Receiver.StartsWith("h:", StringComparison.Ordinal))
                        {
                            // 178: okup krola dla porywacza bez krolestwa - zapasowy odbiorca: glowa jego rodu
                            try { bh = Hero.Find(debt.Receiver.Substring(2)); } catch { bh = null; }
                            if (bh != null && !bh.IsAlive) { var lead = bh.Clan != null ? bh.Clan.Leader : null; bh = lead != null && lead.IsAlive ? lead : null; }
                        }
                        else b = FindKingdom(debt.Receiver);
                        if (a == null || a.IsEliminated || (b == null && bh == null) || (b != null && b.IsEliminated))
                        {
                            _dDropped += debt.Left; _dDroppedN++; debt.Left = 0;   // krolestwa nie ma - dlug przepada (nikt nie dostaje z niczego)
                            continue;
                        }
                        var da = DayOf(a);
                        if (da == null || debt.Left <= 0) continue;
                        long p0; paidBy.TryGetValue(a, out p0);
                        long cap = (long)(share * da.Spend) - p0;        // najwyzej 50% wplywow dnia placacego - na wszystkie jego raty razem
                        long pay = Math.Min(Math.Min(debt.Left, cap), Math.Min(da.Left, (long)Math.Max(0, a.KingdomBudgetWallet)));
                        if (pay <= 0) continue;
                        int ip = (int)Math.Min(int.MaxValue, pay);
                        a.KingdomBudgetWallet -= ip;                     // najpierw placacy, potem odbiorca: odbiorca dostaje dokladnie rate
                        debt.Left -= ip; paidBy[a] = p0 + ip;
                        da.Left -= ip; da.RepOut += ip;
                        if (debt.Kind == 'k') { _dKingRansomPaid += ip; da.KingOut += ip; }   // L1: raty okupow krolow osobno w linii 165
                        if (b != null)
                        {
                            b.KingdomBudgetWallet += ip;
                            var db = DayOf(b);
                            if (db != null) { db.RepIn += ip; db.Left += ip; }   // rata przyjeta - wplyw dnia odbiorcy (do zwrotu tego samego dnia)
                        }
                        else
                        {
                            bh.ChangeHeroGold(ip);
                            CirculationWindows.NoteHeroGold(bh, ip);
                            if (bh != Hero.MainHero) ClanIncomeBook.NoteInflow(bh, ip, ClanIncomeBook.KRansom);
                        }
                    }
                    catch (Exception e) { Stumble("Reparations(dlug)", e); }
                }
                _debts.RemoveAll(x => x.Left <= 0);
            }
            catch (Exception e) { Stumble("Reparations", e); }
        }

        /// <summary>178: dlug korony (okup krola placony przez skarbiec) - raty z wplywow dnia placacego jak reparacje (najwyzej CrownReparationShare wplywow dnia
        /// na wszystkie raty razem). receiver: id krolestwa albo "h:" + id bohatera.</summary>
        internal static void AddCrownDebt(Kingdom payer, string receiver, long amount)
        {
            try
            {
                if (payer == null || payer.StringId == null || string.IsNullOrEmpty(receiver) || amount <= 0) return;
                _debts.Add(new Debt { Payer = payer.StringId, Receiver = receiver, Left = amount, Total = amount, Day = (int)CampaignTime.Now.ToDays, Kind = 'k' });
                _dNewKing += amount; _dNewKingN++;   // L1: nowe dlugi z okupow krolow (linia 165)
            }
            catch (Exception e) { Stumble("AddCrownDebt", e); }
        }

        /// <summary>178: dlug korony z okupow krola (reszta do splaty) - linia "Okupy (178)".</summary>
        internal static long KingRansomDebtLeft() { long t = 0; foreach (var d in _debts) if (d.Kind == 'k') t += d.Left; return t; }

        private static Kingdom FindKingdom(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        /// <summary>GL-2 (zloto startowe): przelew `delta` do skarbca poza wplywami - wczorajsza migawka przesunieta o niego, wiec "inne wplywy" dnia go nie licza (nie wydaje sie go jak dochodu).</summary>
        internal static void ShiftSnapshot(Kingdom k, long delta)
        {
            try { long w; if (k != null && k.StringId != null && delta != 0 && _wEnd.TryGetValue(k.StringId, out w)) _wEnd[k.StringId] = w + delta; }
            catch (Exception e) { Log.Error("CrownIncome.ShiftSnapshot", e); }
        }

        // ------------------------------------------------------------ koniec: migawka skarbcow i linia "Korona: wplywy dnia (165)"
        internal static void End()
        {
            try
            {
                if (Campaign.Current == null) return;
                int today = (int)CampaignTime.Now.ToDays;
                // migawka zawsze (takze przy wylaczonym 165) - wlaczenie w trakcie kampanii ma od nastepnej doby pelne wplywy
                _wEnd.Clear();
                foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k.StringId != null) _wEnd[k.StringId] = k.KingdomBudgetWallet;
                _wEndDay = today;
                KingdomTreasury.FolkWarTaxWindowClear();   // E5/B2 (184): okno wplywow dnia zamkniete razem z migawka (pobor po Begin jest juz w migawce - zapas)
                DsSeason.WindowClear();                    // DS F3 (M9): okno dziesiatej zamkniete razem z oknem 184
                LastAdvanceRepaid = _dAdvRepaid; LastAdvanceNew = _dAdvNew; _dAdvRepaid = 0; _dAdvNew = 0;   // recenzja C1 (OBIEG-1): zaliczka gry (licznik doby)
                LastKingRansomPaid = _dKingRansomPaid; _dKingRansomPaid = 0;   // 178: raty okupu krola ze skarbca (licznik doby)
                _refDueY.Clear(); _refDueYDay = _open ? today : -1;   // M2b: migawka zwrotu naleznego (wezwania 168 jutro czytaja ja przed zwrotem)
                foreach (var kv in _day) if (kv.Key != null && kv.Key.StringId != null && kv.Value.RefundDue > 0) _refDueY[kv.Key.StringId] = kv.Value.RefundDue;
                if (!_open) { LastDebtRepaid = _dDebtRepaid; _dDebtRepaid = 0; LastNewDebt = _dNewDebt; LastNewDebtN = _dNewDebtN; _dNewDebt = 0; _dNewDebtN = 0; return; }
                long meas = 0, own = 0, oth = 0, rel = 0, spend = 0, gift = 0, giftIn = 0, repO = 0, repI = 0, con = 0, refD = 0, refG = 0, left = 0, rent = 0, rentH = 0, ctw = 0, loanIn = 0, loanOut = 0; int noSnap = 0;
                long wtax = 0;   // E5/B2 (184)
                long waid = 0;   // P1: danina z oszczednosci ludu
                long dmov = 0, dtre = 0, dant = 0;   // DS F3: dziesiata od ruchomosci, brak zwrotu ze skarbca ponad zapas wojny i z zaliczek mieszczan
                long kingO = 0;    // L1: raty okupow krolow (178) w racie dlugow koron
                long naRent = 0;   // M1: czesc doplaty W5 z reszty wplywow dnia
                long bread = 0;    // M3: chleb dla glodnych osad z reszty wplywow dnia
                long naid = 0;   // W5: doplata do zoldu wedlug potrzeby
                long scIn = 0, scOut = 0;   // W6: tarczowe (pobrane i wyplacone tej samej doby)
                long cAid = 0;   // W5b regula 2: doplata W5 ze skarbcow ponad zapas wojny (poza wplywami dnia)
                long vKept = 0; int vKeptN = 0; double vR = 0;   // E3-G5
                long court = 0; int courtN = 0;   // E3-P1 (b)
                var parts = new List<KeyValuePair<long, string>>();
                foreach (var kv in _day)
                {
                    var k = kv.Key; var d = kv.Value;
                    meas += d.Measured; own += d.Own; oth += d.Other; rel += d.Release; spend += d.Spend; gift += d.GiftOut; giftIn += d.GiftIn; repO += d.RepOut; repI += d.RepIn;
                    con += d.Contract; refD += d.RefundDue; refG += d.RefundGiven; left += Math.Max(0, d.Left); rent += d.Rent; rentH += d.RentHeld; ctw += d.CallToWar;
                    loanIn += d.LoanIn; loanOut += d.LoanOut; wtax += d.WarTax; waid += d.WarAid; dmov += d.DsMov; dtre += d.DsTreas; dant += d.DsAnt; naid += d.NeedAid; naRent += d.NeedAidRent; bread += d.Bread; kingO += d.KingOut; scIn += d.ScutIn; scOut += d.ScutOut; cAid += d.CrownAid;
                    if (d.ValveKept > 0) { vKept += d.ValveKept; vKeptN++; vR = Math.Max(vR, d.ValveR); }
                    if (d.Court > 0) { court += d.Court; courtN++; }
                    if (!d.Snap) noSnap++;
                    if (d.Spend <= 0 && d.RepIn <= 0 && d.GiftIn <= 0 && d.LoanIn <= 0 && d.ScutIn <= 0 && d.CrownAid <= 0) continue;
                    parts.Add(new KeyValuePair<long, string>(d.Spend, (k.Name != null ? k.Name.ToString() : k.StringId) + " " + d.Measured + "+" + d.Release + (d.WarTax > 0 ? " (184 " + d.WarTax + ")" : "") + (d.WarAid > 0 ? " (danina P1 " + d.WarAid + ")" : "")
                              + (d.LoanOut > 0 ? " rata186 -" + d.LoanOut : "") + (d.LoanIn > 0 ? " kredyt186 +" + d.LoanIn : "")
                              + (d.GiftOut > 0 ? " dar -" + d.GiftOut : "") + (d.GiftIn > 0 ? " dar +" + d.GiftIn : "") + (d.RepOut > 0 ? " rata -" + d.RepOut : "") + (d.RepIn > 0 ? " rata +" + d.RepIn : "")
                              + (d.Contract > 0 ? " kontr. " + d.Contract : "") + (d.CallToWar > 0 ? " wezw. " + d.CallToWar : "") + (d.RefundDue > 0 ? " zwrot " + d.RefundGiven + "/" + d.RefundDue : "")
                              + (d.Rent > 0 || d.RentHeld > 0 ? " renty " + d.Rent + " wstrz. " + d.RentHeld : "")
                              + (d.NeedAid > 0 ? " doplata W5 " + d.NeedAid : "") + (d.NeedAidRent > 0 ? " z rent M1 " + d.NeedAidRent : "")
                              + (d.Bread > 0 ? " chleb M3 " + d.Bread : "")
                              + (d.ScutIn > 0 || d.ScutOut > 0 ? " tarczowe W6 +" + d.ScutIn + " -" + d.ScutOut : "")
                              + (d.CrownAid > 0 ? " ze skarbca W5b -" + d.CrownAid : "")
                              + (d.Windfall > 0 ? " jednoraz. do zapasu " + d.Windfall : "")));
                }
                parts.Sort((x, y) => y.Key.CompareTo(x.Key));
                var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                long debtSum = 0; foreach (var dbt in _debts) debtSum += dbt.Left;
                LastMeasured = meas; LastOwn = own; LastOther = oth; LastRelease = rel; LastSpend = spend; LastGiftOut = gift; LastRepOut = repO; LastRepIn = repI; LastContract = con; LastRefund = refG; LastLeft = left;
                LastRent = rent; LastRentHeld = rentH; LastCallToWar = ctw; LastLoanIn = loanIn; LastLoanOut = loanOut;
                LastDebtRepaid = _dDebtRepaid; _dDebtRepaid = 0; LastNewDebt = _dNewDebt; LastNewDebtN = _dNewDebtN; _dNewDebt = 0; _dNewDebtN = 0;
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                {
                    var sb = new StringBuilder(2048);
                    sb.Append("Korona: wplywy dnia (165): dzien ").Append(today)
                      .Append(" | wplywy dnia ").Append(meas).Append(" (nasze: powinnosci, danina wojenna, podatek wojenny ludu (184), clo, czesc korony z zaworu zamkow i miast (E5/B3: miast 1/2 przy wyrownaniu), 1/9 prawa trzecich (178) ").Append(own)
                      .Append(", w tym podatek wojenny ludu (184) ").Append(wtax)
                      .Append(", danina z oszczednosci ludu (P1) ").Append(waid)
                      .Append(DsTowns.On || dmov > 0 ? ", dziesiata od ruchomosci (DS M9) " + dmov : "")
                      .Append("; inne do skarbcow od wczoraj - gra, BK, Diplomacy, splata dlugu wobec korony ").Append(oth).Append(")")
                      .Append(" + 1/").Append(Math.Max(1f, s.CrownReserveReleaseDays).ToString("0", Inv)).Append(" zapasu ponad ").Append(Math.Max(0, s.CrownReserveGold)).Append(' ').Append(rel)
                      .Append(" = do wydania ").Append(spend)
                      .Append(" | wydane z wplywow: rata kredytu Banku (186) ").Append(loanOut).Append(CrownBorrow.On ? "" : " (186 wylaczone)")
                      .Append(", dary (182) ").Append(gift).Append(" (przeszly do rodow odbiorcow ").Append(giftIn).Append(")")
                      .Append(", raty reparacji ").Append(repO).Append(" (przyjete ").Append(repI).Append("; L1 rozbicie: okupy krolow (178) ").Append(kingO).Append(", reparacje ").Append(repO - kingO).Append(")")
                      .Append(", kontrakty najemnikow (185) ").Append(con)
                      .Append(", wezwania sojusznikow do wojny (168) ").Append(ctw).Append(" (porozumien zerwanych z braku wplywow ").Append(CrownCallToWar.LastEndedN)
                      .Append(", niedoplata ").Append(CrownCallToWar.LastUnpaid).Append(CrownCallToWar.On ? ")" : "; wylaczone - placa rody jak w grze)")
                      .Append(", zwrot zoldu ").Append(refG).Append(" (nalezny ").Append(refD).Append(refD > 0 ? ", wyplacone " + (100.0 * refG / refD).ToString("0.0", Inv) + "%" : "")
                      .Append("; w tym z kredytu Banku (186) ").Append(loanIn).Append(DsTowns.FarmsLive || DsVillage.Live || dtre > 0 ? ", ze skarbca ponad zapas wojny (DS K5) " + dtre : "")
                      .Append(DsTowns.FarmsLive || dant > 0 ? ", z zaliczek mieszczan (DS L1) " + dant : "").Append(")")
                      .Append(", renty (180) ").Append(rent).Append(" (wstrzymane w skarbcach - rody bez warunku i zaokraglenia ").Append(rentH).Append(CrownRents.NeedNote165()).Append(CrownRents.On ? ")" : "; renty wylaczone)")   // f7-renty: dopisek tylko przy czynnym kluczu
                      .Append(", doplata do zoldu wedlug potrzeby (W5, przed rentami) ").Append(naid).Append(", doplata z reszty wplywow dnia przed rentami (M1) ").Append(naRent)
                      .Append(", chleb dla glodnych osad (M3, z reszty wplywow przed rentami) ").Append(bread)
                      .Append(", E3-G5 z zaworu miast do zapasu (Crown Keeps Valve Share ").Append(s.CrownKeepsValveShare ? "tak" : "nie - dzwignia awaryjna").Append(", z puli rent po M3) ").Append(vKept)
                      .Append(" (krolestw ").Append(vKeptN).Append(", r najwyzej ").Append(vR.ToString("0.000", Inv)).Append(", udzial koron wczoraj ").Append(double.IsNaN(_dValveShare) ? "-" : (100 * _dValveShare).ToString("0.0", Inv) + "%")
                      .Append(", pominiete przy kredycie 186 ").Append(_dValveLoanN).Append(')')
                      .Append(", E3-P1 dwor krolewski - towar z polki stolicy z puli rent (Crown Court Goods ").Append(s.CrownCourtGoods ? "tak" : "nie").Append(") ").Append(court).Append(" (krolestw ").Append(courtN).Append(')')
                      .Append(", tarczowe (W6, poza wplywami dnia): pobrane od rodow do skarbcow ").Append(scIn).Append(", wyplacone tej samej doby rodom z doplata W5 ").Append(scOut)
                      .Append(", doplata ze skarbcow ponad zapas wojny (W5b, poza wplywami dnia - z zapasu) ").Append(cAid)
                      .Append(", skarbce zdobytych stolic (L5b-1, poza wplywami dnia - migawka przesunieta): ze skarbcow przegranych ").Append(CapitalTreasury.LastTaken)
                      .Append(" do skarbcow zdobywcow ").Append(CapitalTreasury.LastToCrowns).Append(" i kies glow rodow bez krolestwa ").Append(CapitalTreasury.LastToHeads)
                      .Append(" | zostalo z wplywow dnia w skarbcach ").Append(left)
                      .Append(" | dlugi reparacji: ").Append(_debts.Count).Append(" na ").Append(debtSum).Append(" zl, nowe dzis ").Append(LastNewDebtN).Append(" na ").Append(LastNewDebt)
                      .Append(" (L1: w tym okupy krolow (178) - reszta ").Append(KingRansomDebtLeft()).Append(" zl, nowe dzis ").Append(_dNewKingN).Append(" na ").Append(_dNewKing).Append(")")
                      .Append(", przepadly (krolestwa nie ma) ").Append(_dDroppedN).Append(" na ").Append(_dDropped)
                      .Append(" | splata dlugu wobec korony do skarbcow (stary zapis) ").Append(LastDebtRepaid).Append(" (niezaplacone mimo wpisu w saldzie - nie do skarbca ").Append(_dDebtUnpaid).Append(')')
                      .Append(", splata zaliczki gry (portfel uznany z niczego) - w nicosc ").Append(LastAdvanceRepaid).Append(" (nowe zaliczki gry od wczoraj ").Append(LastAdvanceNew)
                      .Append(", niesplacone razem ").Append(AdvanceTotal()).Append(" u ").Append(_advance.Count).Append(" rodow)")
                      .Append(" | krolestwa bez wczorajszej migawki (tylko nasze liczniki) ").Append(noSnap)
                      .Append(s.CrownWindfallToReserve || _dWf > 0 ? " | jednorazowe 'inne' do zapasu (Crown Windfall To Reserve " + (s.CrownWindfallToReserve ? "tak" : "nie") + ", ponad 3 x mediana 28 dob krolestwa, rozchodzi sie 1/" + Math.Max(1f, s.CrownReserveReleaseDays).ToString("0", Inv) + "): " + _dWf + " (" + _dWfN + " krolestw)" : "")
                      .Append(" | reparacje Diplomacy przechwycone: ").Append(_repWired ? "TAK" : "BRAK").Append(", splata dlugu wobec korony: ").Append(_debtWired ? "TAK" : "BRAK")
                      .Append(" | na krolestwo (wplywy+zapas, wydatki): ").Append(txt.Count > 0 ? string.Join(", ", txt.ToArray()) : "-")
                      .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
                    if (_importN >= 0) { sb.Append(" Wczytano z zapisu: dlugow reparacji ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
                    Log.Info(sb.ToString());
                }
                _dDropped = 0; _dDroppedN = 0; _dDebtUnpaid = 0; _dNewKing = 0; _dNewKingN = 0;
                _dValveLoanN = 0; _dValveShare = double.NaN;   // E3-G5
            }
            catch (Exception e) { Stumble("End", e); }
            finally { _open = false; }
        }

        // ------------------------------------------------------------ Diplomacy: odszkodowania jako dlug korona -> korona
        private static System.Reflection.PropertyInfo _pPayer, _pRecv, _pPayerW, _pRecvW, _pValue;

        /// <summary>Prefiks KingdomWalletCost.ApplyCost (Diplomacy, wirtualna - wolana przez HybridCost przy pokoju): portfele "Reparations" -> dlug.</summary>
        public static bool ReparationPrefix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (__instance == null || s == null || !On || !s.CrownReparationsRealm) return true;
                var t = __instance.GetType();
                if (_pPayer == null || _pPayer.DeclaringType != t)
                {
                    _pPayer = AccessTools.Property(t, "PayingKingdom"); _pRecv = AccessTools.Property(t, "ReceivingKingdom");
                    _pPayerW = AccessTools.Property(t, "PayerWallet"); _pRecvW = AccessTools.Property(t, "ReceiverWallet"); _pValue = AccessTools.Property(t, "Value");
                }
                if (_pPayer == null || _pRecv == null || _pPayerW == null || _pRecvW == null || _pValue == null) return true;
                var a = _pPayer.GetValue(__instance, null) as Kingdom;
                var b = _pRecv.GetValue(__instance, null) as Kingdom;
                if (a == null || b == null || a == b || a.StringId == null || b.StringId == null) return true;
                // GiveGoldToKingdomAction.WalletType: None 0, Mercenary 1, Tribute 2, Budget 3, Reparations 4
                if (Convert.ToInt32(_pPayerW.GetValue(__instance, null), Inv) != 4 || Convert.ToInt32(_pRecvW.GetValue(__instance, null), Inv) != 4) return true;
                long amount = (long)Convert.ToSingle(_pValue.GetValue(__instance, null), Inv);
                if (amount <= 0) return true;
                _debts.Add(new Debt { Payer = a.StringId, Receiver = b.StringId, Left = amount, Total = amount, Day = (int)CampaignTime.Now.ToDays });
                _dNewDebt += amount; _dNewDebtN++;
                Log.Info("Korona: reparacje (165) - " + a.Name + " jest winne " + b.Name + " " + amount + " zl; splata ratami z wplywow dnia placacego (najwyzej "
                         + (Math.Max(0f, Math.Min(1f, s.CrownReparationShare)) * 100f).ToString("0", Inv) + "% dziennie), rody nie placa.");
                if (Clan.PlayerClan != null && (Clan.PlayerClan.Kingdom == a || Clan.PlayerClan.Kingdom == b))
                    Log.Player(a.Name + " owes " + b.Name + " " + amount + " denars in war reparations - the crown pays them from its daily income, in instalments.");
                return false;   // Diplomacy nie rusza skarbcow, trybutu ani kies (zloto z niczego dla krola i najemnikow odbiorcy - tez nie)
            }
            catch (Exception e) { Stumble("ReparationPrefix", e); return true; }
        }

        // ------------------------------------------------------------ splata DebtToKingdom do skarbca (gra: AddPaymentForDebts - dotad w nicosc)
        // przeglad C1 (uwaga 3): splata w AddPaymentForDebts to wpis w saldzie rodu, a saldo dopisuje gra na koncu rozliczenia (DailyTickClan) - kiesa
        // glowy nie schodzi ponizej 0, a BK dopisuje wydatki PO tym kroku. Skarbiec dostaje wiec dopiero na koncu rozliczenia rodu (SoldierPay.ClanTickPostfix)
        // i tylko czesc, ktora glowa naprawde zaplacila: splata minus brak salda (saldo, ktore nie zmiescilo sie w kiesie). Ujemna "splata" gry (gra dopisuje
        // rodowi brak jako dlug) - nic.
        private static Clan _debtClan; private static int _debtPaid;
        private static long _dDebtUnpaid;

        public static void DebtPrefix(Clan __0, bool __2, out int __state)
        {
            __state = -1;
            try { if (__2 && __0 != null && On) { __state = __0.DebtToKingdom; if (!ReferenceEquals(_debtClan, __0)) { _debtClan = null; _debtPaid = 0; } } } catch { __state = -1; }
        }

        public static void DebtPostfix(Clan __0, int __state)
        {
            try
            {
                if (__state < 0 || __0 == null || __0.Kingdom == null) return;
                int paid = __state - __0.DebtToKingdom;
                if (paid <= 0) return;
                _debtClan = __0; _debtPaid += paid;                // do zaplaty skarbcowi na koncu rozliczenia rodu (ClanTickEnd)
            }
            catch (Exception e) { Stumble("DebtPostfix", e); }
        }

        /// <summary>SoldierPay.ClanTickPostfix (koniec rozliczenia rodu): splata dlugu wobec korony do skarbca - najwyzej to, co glowa naprawde zaplacila,
        /// minus zaliczka gry (OBIEG-1 - ta czesc w nicosc). haveNet - znane saldo i kiesa glowy przed jego dopisaniem; bez nich - nic (nie wiemy, czy zaplacono).
        /// Zwraca, o ile gra zmniejszyla dzis dlug rodu w AddPaymentForDebts (0 - bez splaty) - SoldierPay liczy z tego brutto nowego dlugu.</summary>
        internal static int ClanTickEnd(Clan c, bool haveNet, int goldMid, int net)
        {
            int paid = 0;
            try
            {
                if (_debtClan != null && ReferenceEquals(_debtClan, c)) paid = _debtPaid;
                _debtClan = null; _debtPaid = 0;
                if (paid > 0 && c.Kingdom != null && On)
                {
                    long gap = haveNet ? Math.Max(0L, -((long)goldMid + net)) : paid;   // brak salda - najpierw obciaza splate dlugu
                    long real = Math.Max(0L, paid - gap);
                    _dDebtUnpaid += paid - real;
                    if (real > 0)
                    {
                        // recenzja C1 (OBIEG-1): splata najpierw gasi zaliczke gry - ta czesc wraca w nicosc (odbiorca portfela dostal ja juz z niczego);
                        // do skarbca tylko reszta (glowa zaplacila w saldzie - przelew rod -> skarbiec, nie w nicosc)
                        long adv = TakeAdvance(c, real);
                        long toWallet = real - adv;
                        if (toWallet > 0) { c.Kingdom.KingdomBudgetWallet += (int)toWallet; _dDebtRepaid += toWallet; }
                        _dAdvRepaid += adv;
                    }
                }
                else if (paid > 0) TakeAdvance(c, paid);   // bez skarbca (165 wylaczone w trakcie, rod bez krolestwa) - splata w nicosc gasi zaliczke tak samo
                TrimAdvance(c);                             // zaliczka nigdy wieksza niz dlug (dlug darowany albo splacony poza nasza latka)
            }
            catch (Exception e) { Stumble("ClanTickEnd", e); }
            return paid;
        }

        /// <summary>3.5 (CouncilWages35, przed ClanTickEnd w tym samym rozliczeniu): czesc dzisiejszej splaty dlugu rodu, ktora ClanTickEnd uzna za
        /// niezaplacona przy braku salda `gap` (ta sama regula: brak najpierw obciaza splate). Tylko odczyt.</summary>
        internal static long DebtCutNow(Clan c, long gap)
        {
            if (c == null || _debtClan == null || !ReferenceEquals(_debtClan, c) || _debtPaid <= 0 || c.Kingdom == null || !On) return 0;
            return Math.Min((long)_debtPaid, Math.Max(0L, gap));
        }

        /// <summary>Recenzja C1 (OBIEG-1), SoldierPay.Settle: czesc dzisiejszego przyrostu dlugu rodu, ktorej nie zrownowazyl obciety zold - zaliczka gry z niczego.</summary>
        internal static void NoteAdvance(Clan c, long amount)
        {
            try
            {
                if (c == null || c.StringId == null || amount <= 0 || !On) return;
                long v; _advance.TryGetValue(c.StringId, out v);
                v = Math.Min(v + amount, Math.Max(0L, (long)c.DebtToKingdom));
                if (v > 0) _advance[c.StringId] = v; else _advance.Remove(c.StringId);
                _dAdvNew += amount;
            }
            catch (Exception e) { Stumble("NoteAdvance", e); }
        }

        /// <summary>Splata `amount` gasi najpierw zaliczke gry rodu - zwraca zgaszona czesc.</summary>
        private static long TakeAdvance(Clan c, long amount)
        {
            long v;
            if (c == null || c.StringId == null || amount <= 0 || !_advance.TryGetValue(c.StringId, out v)) return 0;
            long adv = Math.Min(amount, v);
            v -= adv;
            if (v > 0) _advance[c.StringId] = v; else _advance.Remove(c.StringId);
            return adv;
        }

        private static void TrimAdvance(Clan c)
        {
            long v;
            if (c == null || c.StringId == null || !_advance.TryGetValue(c.StringId, out v)) return;
            long debt = Math.Max(0L, (long)c.DebtToKingdom);
            if (debt <= 0) _advance.Remove(c.StringId);
            else if (v > debt) _advance[c.StringId] = debt;
        }

        private static long AdvanceTotal() { long t = 0; foreach (var kv in _advance) t += kv.Value; return t; }

        /// <summary>168 (DebtLadder.TakeOverCrownDebt): dlug wobec korony przechodzi do ksiegi drabiny - zaliczka gry rodu wychodzi stad cala
        /// (jej splata dalej w nicosc, juz z ksiegi 168). Zwraca zaliczke (najwyzej `debt`).</summary>
        internal static long TakeAdvanceAll(Clan c, long debt)
        {
            long v;
            if (c == null || c.StringId == null || !_advance.TryGetValue(c.StringId, out v)) return 0;
            _advance.Remove(c.StringId);
            return Math.Max(0L, Math.Min(v, debt));
        }

        private static Harmony _harmony;

        /// <summary>Przy starcie gry: tylko Diplomacy (klasa bez pol statycznych gry). Latka na model finansow gry - w kampanii (EnsureHooks).</summary>
        internal static void ApplyAll(Harmony h)
        {
            _harmony = h;
            string rep = "BRAK (brak Diplomacy)";
            try
            {
                var t = AccessTools.TypeByName("Diplomacy.Costs.KingdomWalletCost");
                var m = t != null ? AccessTools.Method(t, "ApplyCost", Type.EmptyTypes) : null;
                if (m != null)
                {
                    // po oknie ksiegi obiegu (Priority.First) - okno widzi, ze skarbce sie nie zmienily
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CrownIncome), nameof(ReparationPrefix)) { priority = Priority.Normal });
                    _repWired = true; rep = "wpiete";
                }
            }
            catch (Exception e) { Log.Error("CrownIncome.ApplyAll(reparacje)", e); }
            Log.Info("CrownIncome (165): korona z biezacych wplywow - reparacje Diplomacy jako dlug korona-korona " + rep
                     + "; splata dlugu wobec korony do skarbca dojdzie w kampanii; wylacznik Crown Current Income (czynny tylko z Clan Budget Enabled).");
        }

        /// <summary>
        /// W kampanii (OnSessionLaunched), raz na proces: postfiks na DefaultClanFinanceModel.AddPaymentForDebts. Pulapka z SoldierPay: kompilacja
        /// metody czytajacej pole statyczne DefaultClanFinanceModel uruchamia jego konstruktor statyczny (czyta Game.Current) - przy starcie gry zabiloby to klase.
        /// </summary>
        internal static void EnsureHooks()
        {
            if (_debtTried || _harmony == null || Campaign.Current == null) return;
            _debtTried = true;
            try
            {
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddPaymentForDebts");
                if (m != null)
                {
                    _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CrownIncome), nameof(DebtPrefix)), postfix: new HarmonyMethod(typeof(CrownIncome), nameof(DebtPostfix)));
                    _debtWired = true;
                }
            }
            catch (Exception e) { Log.Error("CrownIncome.EnsureHooks(dlug wobec korony)", e); }
            Log.Info("CrownIncome (165): splata dlugu wobec korony (AddPaymentForDebts) do skarbca " + (_debtWired ? "wpieta" : "BRAK") + ".");
        }
        private static bool _debtTried;

        // ------------------------------------------------------------ zapis (SaveText, "arm_crown165")
        /// <summary>"v1|doba migawki|id=skarbiec;...|placacy>odbiorca>reszta>calosc>doba;...|idRodu=zaliczka gry;...|podatek wojenny ludu 184" (piate pole - recenzja C1, OBIEG-1;
        /// szoste - E5/B2, format KingdomTreasury.WarTaxExport; zapis bez nich wczytuje sie jak dotad, starsza wersja moda pomija pola ponad swoje)</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(64 + _wEnd.Count * 24 + _debts.Count * 48 + _advance.Count * 32);
                sb.Append("v1|").Append(_wEndDay.ToString(Inv)).Append('|');
                bool first = true;
                foreach (var kv in _wEnd)
                {
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                }
                sb.Append('|');
                first = true;
                foreach (var d in _debts)
                {
                    if (d.Left <= 0 || d.Payer == null || d.Receiver == null || d.Payer.IndexOfAny(Bad) >= 0 || d.Receiver.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(d.Payer).Append('>').Append(d.Receiver).Append('>').Append(d.Left.ToString(Inv)).Append('>').Append(d.Total.ToString(Inv)).Append('>').Append(d.Day.ToString(Inv));
                    if (d.Kind != 'r') sb.Append('>').Append(d.Kind);   // 178: szoste pole tylko dla okupu krola (stary zapis - 5 pol)
                }
                sb.Append('|');
                first = true;
                if (_advance.Count > 0)
                {
                    // tylko zywe rody z dlugiem (rod wymarly albo bez dlugu - zaliczki juz nie ma czym splacac)
                    var live = new Dictionary<string, int>();
                    foreach (var c in Clan.All) if (c != null && !c.IsEliminated && c.StringId != null && c.DebtToKingdom > 0) live[c.StringId] = c.DebtToKingdom;
                    foreach (var kv in _advance)
                    {
                        int debt;
                        if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0 || kv.Value <= 0 || !live.TryGetValue(kv.Key, out debt)) continue;
                        if (!first) sb.Append(';'); first = false;
                        sb.Append(kv.Key).Append('=').Append(Math.Min(kv.Value, (long)debt).ToString(Inv));
                    }
                }
                sb.Append('|').Append(KingdomTreasury.WarTaxExport());   // E5/B2 (184): szoste pole - stawki i pierscienie potrzeby podatku wojennego ludu (starsza wersja moda je pomija)
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }
        private static readonly char[] Bad = { '|', ';', '=', '>' };

        internal static void Import(string data)
        {
            _wEnd.Clear(); _wEndDay = -1; _debts.Clear(); _advance.Clear(); _importN = 0; _importBad = 0;
            KingdomTreasury.WarTaxImport(null);   // E5/B2 (184): zapis bez szostego pola - s = 0 u wszystkich krolestw
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 4 || f[0] != "v1") { _importBad++; return; }
                int.TryParse(f[1], NumberStyles.Integer, Inv, out _wEndDay);
                if (f[2].Length > 0)
                    foreach (var p in f[2].Split(';'))
                    {
                        var kv = p.Split('='); long v;
                        if (kv.Length == 2 && kv[0].Length > 0 && long.TryParse(kv[1], NumberStyles.Integer, Inv, out v)) _wEnd[kv[0]] = v;
                    }
                if (f[3].Length > 0)
                    foreach (var p in f[3].Split(';'))
                    {
                        var x = p.Split('>'); long left, total; int day;
                        if ((x.Length == 5 || x.Length == 6) && x[0].Length > 0 && x[1].Length > 0 && long.TryParse(x[2], NumberStyles.Integer, Inv, out left) && long.TryParse(x[3], NumberStyles.Integer, Inv, out total)
                            && int.TryParse(x[4], NumberStyles.Integer, Inv, out day) && left > 0)
                        { _debts.Add(new Debt { Payer = x[0], Receiver = x[1], Left = left, Total = total, Day = day, Kind = x.Length == 6 && x[5].Length > 0 ? x[5][0] : 'r' }); _importN++; }
                        else _importBad++;
                    }
                if (f.Length >= 5 && f[4].Length > 0)   // recenzja C1 (OBIEG-1): zaliczki gry; zapis bez tego pola - brak zaliczek (dlugi zostaja przy skarbcu, tabela 165)
                    foreach (var p in f[4].Split(';'))
                    {
                        var kv = p.Split('='); long v;
                        if (kv.Length == 2 && kv[0].Length > 0 && long.TryParse(kv[1], NumberStyles.Integer, Inv, out v) && v > 0) _advance[kv[0]] = v;
                        else _importBad++;
                    }
                if (f.Length >= 6) KingdomTreasury.WarTaxImport(f[5]);   // E5/B2 (184): stawki podatku wojennego ludu (zapis sprzed paczki - brak pola, s = 0)
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
