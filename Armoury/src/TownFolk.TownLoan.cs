using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// f8-pozyczka (decyzja Jeffa 18.10 "2. tak" po wyjasnieniu i radzie "10% rocznie"; projekt docs/kiesa-ludu/PROJEKT-POZYCZKI-2026-10-13.md R2 i wiersz K3) -
    /// POZYCZKA MIEJSKA. Klucz TownLoan (domyslnie TAK), TownLoanRate 0.10, TownLoanRentDays 120, TownLoanFolkReserveTimes 1.0, TownLoanCofferReserveTimes 1.5,
    /// TownLoanTakesFolkLoans (NIE - recenzja f8, uwaga 1: przejecie dlugow obieg2 tylko na slowo Jeffa).
    ///  - KTO I KIEDY (AI): ten sam kredyt wojenny 168 z drugim wierzycielem PRZED Bankiem (ClanBudget.Daily, miejsce dawnego obieg2): rod AI krolestwa w wojnie, G &lt; R,
    ///    D &gt; 0, bez zaleglosci / zajecia / karencji po zajeciu / czarnej ksiegi Banku, D pokrywa dwor i powinnosci (DebtLadder.TownCreditBlock), bez zakazu po niesplacie
    ///    obieg2. Pozycza od WLASNYCH miast i zamkow (bez oblezonych): miasto - kiesy W / R / P ponad TownLoanFolkReserveTimes x zapas dni klasy (10 / 21 / 30 dni potrzeby)
    ///    i kasa kupcow ponad TownLoanCofferReserveTimes x zapas kupcow (TownPurse.Reserve; kasy KS to akumulator - NAKUM-12/16); zamek - podzamcze ponad zapas x 10 dni.
    ///    Z kazdego zrodla najwyzej polowa nadwyzki na dobe (jak korona2), klasy pozyczaja proporcjonalnie do swojej nadwyzki, najpierw osada z najwiekszym miejscem.
    ///  - LIMIT: dlug wobec jednej osady najwyzej TownLoanRentDays (120) x srednia renta 28 dob, ktora ta osada placi panu (ClanIncomeBook.SettlementRentAvg). Udzial
    ///    dnia razem z Bankiem najwyzej WarCreditMaxShareD x D - KW Banku dostaje reszte udzialu i reszte braku (kredyt wojenny Banku maleje).
    ///  - CENA I SPLATA (DebtLadder.DayClan, roszczenie KTown): TownLoanRate (10%) rocznie, rok kalendarza gry, odsetki doliczane co dobe; w wojnie z D tylko odsetki,
    ///    z jednorazowych (lup, okupy) ten sam udzial co kredyt wojenny (50%, po pokoju 100%) - najpierw Bank, potem rady; po pokoju raty z D; wszystkie raty rodu
    ///    najwyzej AllInstalmentsMaxShare (15%) D. Gracz: raty z D w wojnie i w pokoju (bez automatycznej splaty z jednorazowych - jak w Banku).
    ///  - NIESPLATA: drabina 168 bez zmian (zaleglosc, trzecia z rzedu - zajecie dochodu wsi i kiesy ponad 38 000, wyprzedaz); dluznik - dwor -50%, budowy 0. Rod wymarly -
    ///    dlug na nowych panow jego wsi (Inherit). Bez umorzenia.
    ///  - ZLOTO WRACA do wierzycieli osady wedlug udzialow (kiesy W / R / P, kasa kupcow, podzamcze); kiesa nieczynna - kasa osady; osady nie ma - kasa najblizszego miasta.
    ///  - JEDNA REGULA: przy kluczu nowe pozyczki obieg2 (Folk Loans) nie powstaja (FolkLoanRoom = 0); stare dlugi obieg2 biegna wedlug zasad obieg2 do splaty.
    ///    TownLoanTakesFolkLoans (domyslnie NIE) przenosi je do ksiegi rad (miasto - kiesa W, zamek - podzamcze, wies - kiesa wsi) w dobie ludu (LoansDay), przed odsetkami obieg2.
    ///  - DZIENNY PULAP ZRODLA (recenzja f8, uwaga 2): "najwyzej polowa nadwyzki zrodla na dobe" liczona od nadwyzki SPRZED pozyczek tej doby - _tlTaken pamieta, ile
    ///    kazde zrodlo osady juz dzis pozyczylo (AI i gracz), wiec kolejne pozyczki gracza z menu w tej samej dobie nie zjadaja kies do zapasu.
    ///  - ODSETKI SA ZAROBKIEM LUDU (recenzja f8, uwaga 3; projekt R2 "pan i korona biora od nich swoje 1/2/3% i 184"): czesc odsetek splaty wplacona do kiesy klasy
    ///    (W / R / P, podzamcze) idzie takze do zarobku okna tej klasy (EarnWin - podstawa podatku pana, cechu, 184 i dziesieciny nastepnej doby); kapital i czesc
    ///    kasy kupcow - nie (kasa kupcow nie ma zarobku okna; zawor bierze swoje z jej nadwyzki).
    ///  - Gracz: w menu wlasnego miasta / zamku "Borrow from the town council" / "Borrow from the castle folk" (25 / 50 / 100% miejsca dnia) i "Repay ..." (takze po utracie
    ///    osady). Bez SwitchToMenu (okna ShowMultiSelectionInquiry).
    ///  - Nic z niczego: pozyczka i splata to przelewy kiesa ludu / kasa kupcow / podzamcze &lt;-&gt; glowa rodu; dlug to zapis. Ksiega kies _ctlOut / _ctlIn (kontrola r = 0),
    ///    LDayOut / LDayIn; kasy miast w menu (poza blokiem doby) - MoneyLedger.Note169 "inne". Linia "Pozyczki miejskie (f8): dzien N" z tozsamosciami (zawarte = zdjete
    ///    ze zrodel; splacone = do wierzycieli + zapasowe; kontrola dlugu). Zapis: klucz arm_townloan (DebtLadder.ExportTown / ImportTown).
    /// Wyjatek per rod / osada - licznik potkniec (CLAUDE.md 7), przelew oddany zrodlu.
    /// </summary>
    internal static partial class TownFolk
    {
        internal const int TlW = 0, TlR = 1, TlP = 2, TlKasa = 3, TlCastle = 4, TlKinds = 5;
        private const double TlSrcShare = 0.5;   // jak korona2 (KlSrcShare): z kazdego zrodla najwyzej polowa nadwyzki na dobe

        private sealed class TlSrc { public Folk F; public Town T; public int Kind; public double Ex; }
        private sealed class TlPlace { public Settlement St; public double Room, Avail, Limit, Debt; public readonly List<TlSrc> Src = new List<TlSrc>(); }

        private static readonly Dictionary<Clan, List<TlPlace>> _tlPlan = new Dictionary<Clan, List<TlPlace>>();   // plan doby: od TownLoanRoom do TownLoanLend (ten sam rod w petli 166)
        // ---- okno linii (od linii do linii)
        private static long _tlLent, _tlPlayerLent, _tlInt, _tlPaid, _tlPaidInt, _tlSpare, _tlPlayerPaid, _tlMoved, _tlOrphan, _tlTicks;
        private static readonly long[] _tlLentK = new long[TlKinds];
        private static readonly long[] _tlRepK = new long[TlKinds + 2];   // W, R, P, kasa kupcow, podzamcze, kiesa wsi (z obieg2), kasa osady (kiesa nieczynna)
        private static int _tlCand, _tlRoomN, _tlBlkBan, _tlBlkLadder, _tlBlkSeize, _tlBlkD, _tlBlkLimit, _tlBlkNoSrc, _tlOverAfter, _tlMovedN, _tlMovedParts, _tlPlayerN;
        private static int _tlStumbles, _tlStumbleSess;
        private static double _tlRoom, _tlDebtPrev = double.NaN, _tlMovedF;
        private static bool _tlStartDone, _tlOutside;
        private static readonly HashSet<string> _tlLentClans = new HashSet<string>();
        // recenzja f8 (uwaga 2): ile kazde zrodlo osady pozyczylo DZIS (TlTake minus TlUndo; indeks Tl*) - pulap 50% nadwyzki zrodla liczony od nadwyzki sprzed pozyczek doby;
        // czyszczone przy zmianie dnia (TlTakenOf) i w nowej sesji; poza zapisem (po wczytaniu w tej samej dobie - od zera, jak plan doby)
        private static readonly Dictionary<Settlement, long[]> _tlTaken = new Dictionary<Settlement, long[]>();
        private static int _tlTakenDay = int.MinValue;
        private static long _tlIntEarn;   // recenzja f8 (uwaga 3): odsetki wplacone do kies klas zapisane jako zarobek okna (EarnWin) - okno linii
        /// <summary>"Obieg" (MoneyLedger.Daily po linii f8): pozyczki miejskie okna (kiesy ludu, kasy kupcow, podzamcza -> glowy rodow) i splaty (glowy -> wierzyciele i zapasowe kasy).</summary>
        internal static long LastTownLent, LastTownRepaid;

        internal static bool TownLoanOn { get { var s = Settings.Current; return s != null && s.TownLoan && DebtLadder.On && Live; } }
        private static bool TownLoanTakesFolk { get { var s = Settings.Current; return s != null && s.TownLoanTakesFolkLoans && TownLoanOn; } }
        private static double TlRate(Settings s) { return HdClamp(s.TownLoanRate, 0, 0.5); }
        private static double TlRentDays(Settings s) { return HdClamp(s.TownLoanRentDays, 0, 364); }
        private static double TlFolkTimes(Settings s) { return HdClamp(s.TownLoanFolkReserveTimes, 1, 5); }
        private static double TlCofferTimes(Settings s) { return HdClamp(s.TownLoanCofferReserveTimes, 1, 5); }

        private static void TlStumble(string where, Exception e) { _tlStumbles++; _tlStumbleSess++; Stumble("TownLoan." + where, e); }

        /// <summary>Nowa sesja (ResetLoans): plan, okno linii, pamiec kontroli.</summary>
        private static void ResetTownLoan()
        {
            _tlPlan.Clear(); TlZero(); _tlDebtPrev = double.NaN; _tlStartDone = false; _tlStumbleSess = 0; _tlOutside = false;
            _tlTaken.Clear(); _tlTakenDay = int.MinValue;   // recenzja f8 (uwaga 2)
        }

        private static void TlZero()
        {
            _tlLent = _tlPlayerLent = _tlInt = _tlPaid = _tlPaidInt = _tlSpare = _tlPlayerPaid = _tlMoved = _tlOrphan = _tlTicks = _tlIntEarn = 0;
            Array.Clear(_tlLentK, 0, _tlLentK.Length); Array.Clear(_tlRepK, 0, _tlRepK.Length);
            _tlCand = _tlRoomN = _tlBlkBan = _tlBlkLadder = _tlBlkSeize = _tlBlkD = _tlBlkLimit = _tlBlkNoSrc = _tlOverAfter = _tlMovedN = _tlMovedParts = _tlPlayerN = 0;
            _tlStumbles = 0; _tlRoom = 0; _tlMovedF = 0;
            _tlLentClans.Clear();
        }

        // ------------------------------------------------------------ liczniki z drabiny (DebtLadder) - okno linii
        internal static void NoteTownInterest(long x) { if (x > 0) _tlInt += x; }
        internal static void NoteTownSpare(long x) { if (x > 0) _tlSpare += x; }
        internal static void NoteTownOrphan(long x) { if (x > 0) _tlOrphan += x; }

        // ------------------------------------------------------------ zrodla jednej osady
        /// <summary>Recenzja f8 (uwaga 2): ile zrodla osady st pozyczyly dzis (indeks Tl*); create - zaloz wpis. Zmiana dnia gry czysci pamiec. null - nic dzis.</summary>
        private static long[] TlTakenOf(Settlement st, bool create)
        {
            if (st == null) return null;
            int today = (int)CampaignTime.Now.ToDays;
            if (today != _tlTakenDay) { _tlTaken.Clear(); _tlTakenDay = today; }
            long[] a;
            if (!_tlTaken.TryGetValue(st, out a) && create) { a = new long[TlKinds]; _tlTaken[st] = a; }
            return a;
        }

        /// <summary>Recenzja f8 (uwaga 2): dzisiejsza oferta zrodla - polowa nadwyzki SPRZED pozyczek tej doby (stan + juz pozyczone dzis - prog) minus juz pozyczone dzis.
        /// Pierwsza pozyczka doby: taken 0 - wzor jak dotad co do monety.</summary>
        private static double TlOffer(double purse, double floor, long taken)
        {
            double t = Math.Max(0, taken);
            return TlSrcShare * (purse + t - floor) - t;
        }

        /// <summary>Miejsce i zrodla pozyczki rodu c w osadzie st (miasto albo zamek z czynna kiesa ludu, bez oblezenia); null - osada nie pozycza.</summary>
        private static TlPlace TlPlaceFor(Clan c, Settlement st, Settings s)
        {
            if (c == null || st == null || s == null || st.StringId == null || st.IsUnderSiege || !(st.IsTown || st.IsCastle)) return null;
            Folk f;
            if (!_f.TryGetValue(st, out f) || f == null) return null;
            if (st.IsTown ? (f.Kind != 0 || !Live) : (f.Kind != 1 || !CastleLive)) return null;
            var p = new TlPlace { St = st };
            p.Limit = TlRentDays(s) * ClanIncomeBook.SettlementRentAvg(st);
            p.Debt = DebtLadder.TownDebt(c, st.StringId);
            p.Room = Math.Max(0, p.Limit - p.Debt);
            double times = TlFolkTimes(s);
            var tk = TlTakenOf(st, false);   // recenzja f8 (uwaga 2): co zrodla tej osady juz dzis pozyczyly (null - nic)
            if (f.Kind == 0)
            {
                double rdW = Math.Max(1, Math.Min(60, s.TownFolkReserveDays));
                for (int k = 0; k < 3; k++)
                {
                    double need = f.Need[k];
                    if (!(need > 0)) continue;
                    double rd = k == 0 ? rdW : k == 1 ? RdR : RdP;
                    double ex = TlOffer(PurseOf(f, k), times * rd * need, tk != null ? tk[k] : 0);
                    if (ex >= 1) { p.Avail += ex; p.Src.Add(new TlSrc { F = f, Kind = k, Ex = ex }); }
                }
                var t = st.Town;
                if (t != null)
                {
                    double ex = TlOffer(t.Gold, TlCofferTimes(s) * TownPurse.Reserve(t), tk != null ? tk[TlKasa] : 0);
                    if (ex >= 1) { p.Avail += ex; p.Src.Add(new TlSrc { T = t, Kind = TlKasa, Ex = ex }); }
                }
            }
            else
            {
                double need = f.Need[0];
                if (need > 0)
                {
                    double ex = TlOffer(f.KW, times * RdC * need, tk != null ? tk[TlCastle] : 0);
                    if (ex >= 1) { p.Avail += ex; p.Src.Add(new TlSrc { F = f, Kind = TlCastle, Ex = ex }); }
                }
            }
            return p;
        }

        /// <summary>Zdjecie ze zrodla (kiesa klasy / podzamcza - ksiega kies; kasa kupcow - kasa miasta). outside - poza blokiem doby (menu gracza): kasa w "Przeplywy osad".</summary>
        private static long TlTake(TlSrc x, Settlement st, long amount, bool outside)
        {
            try
            {
                if (amount <= 0) return 0;
                if (x.Kind == TlKasa)
                {
                    if (x.T == null) return 0;
                    int had = x.T.Gold;
                    int w = (int)Math.Min(amount, Math.Max(0, had));
                    if (w <= 0) return 0;
                    x.T.ChangeGold(-w);
                    long got = Math.Max(0, had - x.T.Gold);
                    if (outside && got > 0) MoneyLedger.Note169(MoneyLedger.N169Other, st, -(int)got, -1);
                    if (got > 0) { var tk = TlTakenOf(st, true); if (tk != null) tk[TlKasa] += got; }   // recenzja f8 (uwaga 2): pulap doby zrodla
                    return got;
                }
                if (x.F == null) return 0;
                int c = x.Kind == TlCastle ? 0 : x.Kind;
                long t = Math.Min(amount, Math.Max(0, PurseOf(x.F, c)));
                if (t <= 0) return 0;
                AddPurse(x.F, c, -t); _ctlOut += t; x.F.LDayOut += t;
                { var tk = TlTakenOf(st, true); if (tk != null) tk[x.Kind] += t; }   // recenzja f8 (uwaga 2): pulap doby zrodla
                return t;
            }
            catch (Exception e) { TlStumble("Take", e); return 0; }
        }

        /// <summary>Zwrot zdjetego po wyjatku (ksiega kies z powrotem).</summary>
        private static void TlUndo(TlSrc x, Settlement st, long amount, bool outside)
        {
            try
            {
                if (amount <= 0) return;
                var tk = TlTakenOf(st, false);   // recenzja f8 (uwaga 2): zwrot zdejmuje tez z pulapu doby zrodla
                if (x.Kind == TlKasa)
                {
                    if (x.T != null) { x.T.ChangeGold((int)amount); if (outside) MoneyLedger.Note169(MoneyLedger.N169Other, st, (int)amount, -1); if (tk != null) tk[TlKasa] = Math.Max(0, tk[TlKasa] - amount); }
                    return;
                }
                if (x.F == null) return;
                int c = x.Kind == TlCastle ? 0 : x.Kind;
                AddPurse(x.F, c, amount); _ctlOut -= amount; x.F.LDayOut -= amount;
                if (tk != null) tk[x.Kind] = Math.Max(0, tk[x.Kind] - amount);
            }
            catch (Exception e) { TlStumble("Undo", e); }
        }

        /// <summary>Pozyczka `want` od osady p do kiesy `lord` (rod c): zrodla proporcjonalnie do nadwyzki, roszczenie KTown w ksiedze rodu. Zwraca pozyczone.</summary>
        private static long TlLendPlace(Clan c, Hero lord, TlPlace p, long want, bool player)
        {
            if (want <= 0 || p == null || !(p.Avail >= 1) || lord == null || p.Src.Count == 0) return 0;
            int n = p.Src.Count, big = -1; double bigEx = 0;
            var wi = new long[n]; long given = 0;
            for (int i = 0; i < n; i++)
            {
                var x = p.Src[i];
                wi[i] = Math.Max(0, Math.Min((long)Math.Floor(x.Ex), (long)Math.Floor(want * x.Ex / p.Avail)));
                given += wi[i];
                if (x.Ex > bigEx) { bigEx = x.Ex; big = i; }
            }
            if (big >= 0 && given < want) wi[big] = Math.Min((long)Math.Floor(p.Src[big].Ex), wi[big] + (want - given));   // reszta z zaokraglen - najwieksze zrodlo
            var ti = new long[n]; var took = new long[TlKinds]; long sum = 0;
            for (int i = 0; i < n; i++) { long t = TlTake(p.Src[i], p.St, wi[i], _tlOutside); ti[i] = t; took[p.Src[i].Kind] += t; sum += t; }
            if (sum <= 0) return 0;
            try { lord.ChangeHeroGold((int)Math.Min(int.MaxValue, sum)); }
            catch (Exception e) { for (int i = 0; i < n; i++) TlUndo(p.Src[i], p.St, ti[i], _tlOutside); TlStumble("Lend(glowa)", e); return 0; }
            CirculationWindows.NoteHeroGold(lord, sum);
            DebtLadder.AddTownClaim(c, p.St.StringId, sum, took);
            for (int k = 0; k < TlKinds; k++) _tlLentK[k] += took[k];
            if (player) { _tlPlayerLent += sum; _tlPlayerN++; }
            else { _tlLent += sum; if (c.StringId != null) _tlLentClans.Add(c.StringId); }
            if (DebtLadder.TownDebt(c, p.St.StringId) > p.Limit + 1) _tlOverAfter++;   // kontrola: pozyczka nie wyprowadza dlugu ponad limit (oczekiwane 0)
            p.Room = Math.Max(0, p.Room - sum); p.Avail = Math.Max(0, p.Avail - sum); p.Debt += sum;
            return sum;
        }

        // ------------------------------------------------------------ AI: budzet rodow 166 (ClanBudget.Daily, przed kredytem wojennym Banku)
        /// <summary>
        /// Miejsce pozyczki miejskiej rodu AI w pulapie zoldu dzis (0 - rod nie pozycza): warunki kredytu wojennego bez Banku, wlasne miasta i zamki, limit renty osady,
        /// nadwyzka zrodel, udzial dnia WarCreditMaxShareD x D (KW Banku dostaje reszte). D, G, R, war, household - z budzetu 166 tej doby. Nie rzuca.
        /// </summary>
        internal static double TownLoanRoom(Clan c, double D, double G, double R, bool war, double household)
        {
            if (!TownLoanOn || c == null || c == Clan.PlayerClan || c.Leader == null || c.Kingdom == null) return 0;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!war || !(D > 0) || G >= R) return 0;
                _tlCand++;
                int until;
                if (c.StringId != null && _lnBan.TryGetValue(c.StringId, out until) && (int)CampaignTime.Now.ToDays < until) { _tlBlkBan++; return 0; }
                int blk = DebtLadder.TownCreditBlock(c, D, household);
                if (blk == 1) { _tlBlkLadder++; return 0; }
                if (blk == 2) { _tlBlkSeize++; return 0; }
                if (blk == 3) { _tlBlkD++; return 0; }
                var s = Settings.Current;
                var places = new List<TlPlace>();
                double limRoom = 0, avail = 0, room = 0;
                var fiefs = c.Fiefs;
                if (fiefs != null)
                    for (int i = 0; i < fiefs.Count; i++)
                    {
                        var p = TlPlaceFor(c, fiefs[i] != null ? fiefs[i].Settlement : null, s);
                        if (p == null) continue;
                        limRoom += p.Room; avail += p.Avail;
                        double r = Math.Min(p.Room, p.Avail);
                        if (r >= 1) { room += r; places.Add(p); }
                    }
                if (limRoom < 1) { _tlBlkLimit++; return 0; }
                if (avail < 1 || room < 1) { _tlBlkNoSrc++; return 0; }
                room = Math.Floor(Math.Min(room, Math.Max(0f, Math.Min(1f, s.WarCreditMaxShareD)) * D));
                if (room < 1) { _tlBlkD++; return 0; }
                _tlPlan[c] = places; _tlRoomN++; _tlRoom += room;
                return room;
            }
            catch (Exception e) { TlStumble("Room", e); return 0; }
            finally { _tlTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Wyplata pozyczki miejskiej dzis: najwyzej `amount` z planu TownLoanRoom tego rodu (najpierw osada z najwiekszym miejscem) do kiesy glowy. Zwraca wyplacone.</summary>
        internal static int TownLoanLend(Clan c, double amount)
        {
            if (c == null || c.Leader == null || !(amount >= 1)) return 0;
            List<TlPlace> places;
            if (!_tlPlan.TryGetValue(c, out places)) return 0;
            _tlPlan.Remove(c);
            long t0 = Stopwatch.GetTimestamp(), got = 0;
            try
            {
                places.Sort((a, b) => Math.Min(b.Room, b.Avail).CompareTo(Math.Min(a.Room, a.Avail)));
                long left = (long)Math.Floor(Math.Min(amount, int.MaxValue / 2));
                foreach (var p in places)
                {
                    if (left <= 0) break;
                    long want = Math.Min(left, (long)Math.Floor(Math.Min(p.Room, p.Avail)));
                    if (want <= 0) continue;
                    long g = 0;
                    try { g = TlLendPlace(c, c.Leader, p, want, false); }
                    catch (Exception e) { TlStumble("Lend(osada)", e); g = 0; }
                    got += g; left -= g;
                }
            }
            catch (Exception e) { TlStumble("Lend", e); }
            finally { _tlTicks += Stopwatch.GetTimestamp() - t0; }
            return (int)Math.Min(int.MaxValue, got);
        }

        // ------------------------------------------------------------ splata (DebtLadder.Deliver, rodzaj KTown)
        /// <summary>
        /// Rata / zajete zloto (juz zdjete z dluznika) do wierzycieli osady stId wedlug udzialow sh: miasto - kiesy W / R / P i kasa kupcow (udzial podzamcza - do W),
        /// zamek - podzamcze (calosc), wies (dlug przejety z obieg2) - kiesa wsi, kiesa nieczynna - kasa osady. ip - w tym odsetki (licznik). Zwraca wplacone (reszte
        /// wolajacy daje zapasowemu odbiorcy). Nie rzuca.
        /// </summary>
        internal static long TownLoanRepay(string stId, double[] sh, long x, long ip)
        {
            if (x <= 0) return 0;
            _tlPaid += x; _tlPaidInt += Math.Max(0, ip);
            long placed = 0;
            try
            {
                var st = string.IsNullOrEmpty(stId) ? null : Settlement.Find(stId);
                if (st == null) return 0;
                if (st.IsVillage)
                {
                    var v = st.Village;
                    if (v == null) return 0;
                    v.ChangeGold((int)Math.Min(int.MaxValue, x));
                    _tlRepK[TlKinds] += x;
                    return x;
                }
                Folk f;
                bool live = st.IsTown ? Live : (st.IsCastle && CastleLive);
                if (live && _f.TryGetValue(st, out f) && f != null && (st.IsTown ? f.Kind == 0 : f.Kind == 1))
                {
                    var w = new double[TlKinds]; double tot = 0;
                    for (int k = 0; k < TlKinds; k++) { double v = sh != null && k < sh.Length && sh[k] > 0 && !double.IsNaN(sh[k]) ? sh[k] : 0; w[k] = v; }
                    if (st.IsTown) { w[TlW] += w[TlCastle]; w[TlCastle] = 0; }
                    else { double all = 0; for (int k = 0; k < TlKinds; k++) all += w[k]; for (int k = 0; k < TlKinds; k++) w[k] = 0; w[TlCastle] = all; }
                    for (int k = 0; k < TlKinds; k++) tot += w[k];
                    if (!(tot > 0)) { if (st.IsTown) w[TlW] = 1; else w[TlCastle] = 1; tot = 1; }
                    var a = new long[TlKinds]; long given = 0; int big = 0;
                    for (int k = 0; k < TlKinds; k++) { a[k] = (long)Math.Floor(x * w[k] / tot); given += a[k]; if (w[k] > w[big]) big = k; }
                    a[big] += x - given;
                    double ipShare = ip > 0 ? Math.Min(1.0, (double)ip / x) : 0;   // recenzja f8 (uwaga 3): czesc odsetek w tej wplacie (kazdy wierzyciel w tej samej proporcji)
                    for (int k = 0; k < TlKinds; k++)
                    {
                        long ak = a[k];
                        if (ak <= 0) continue;
                        if (k == TlKasa)
                        {
                            if (st.Town == null) continue;
                            st.Town.ChangeGold((int)Math.Min(int.MaxValue, ak));
                            if (_tlOutside) MoneyLedger.Note169(MoneyLedger.N169Other, st, (int)Math.Min(int.MaxValue, ak), -1);
                        }
                        else
                        {
                            int ck = k == TlCastle ? 0 : k;
                            AddPurse(f, ck, ak); _ctlIn += ak; f.LDayIn += ak;
                            // recenzja f8 (uwaga 3, projekt R2): odsetki sa zarobkiem ludu - do zarobku okna klasy (podstawa podatku pana 1/2/3%, cechu, 184 i dziesieciny
                            // nastepnej doby; zamek - [0], 3%); kapital nie. Zloto juz w kiesie wyzej - tu tylko podstawa podatkow (bez nowego przelewu)
                            long ie = ipShare > 0 ? (long)Math.Floor(ak * ipShare) : 0;
                            if (ie > 0 && ck >= 0 && ck < 3) { f.EarnWin[ck] += ie; _tlIntEarn += ie; }
                        }
                        _tlRepK[k] += ak; placed += ak;
                    }
                    return placed;
                }
                if (st.Town != null)
                {
                    int ix = (int)Math.Min(int.MaxValue, x);
                    st.Town.ChangeGold(ix);
                    SoldierPay.HoldCourt(st, ix);
                    if (_tlOutside) MoneyLedger.Note169(MoneyLedger.N169Other, st, ix, -1);
                    _tlRepK[TlKinds + 1] += x;
                    return x;
                }
                return 0;
            }
            catch (Exception e) { TlStumble("Repay", e); return placed; }
        }

        // ------------------------------------------------------------ przejecie dlugow obieg2 (LoansDay, przed odsetkami obieg2)
        /// <summary>
        /// TownLoanTakesFolkLoans: dlugi obieg2 zywych rodow AI (nie gracza) do ksiegi rad - kazda czesc osobnym roszczeniem KTown wobec tej samej osady i tej samej kiesy
        /// (miasto - W, zamek - podzamcze, wies - kiesa wsi). Czesc przeniesiona znika z obieg2 (Left 0). Zwraca przeniesione wedlug ksiegi obieg2 (do jej kontroli).
        /// </summary>
        private static double TlTakeOverFolk()
        {
            if (!TownLoanTakesFolk || _ln.Count == 0) return 0;
            double moved = 0;
            try
            {
                var byId = new Dictionary<string, Clan>();
                foreach (var c in Clan.All) if (c != null && c.StringId != null) byId[c.StringId] = c;
                var drop = new List<string>();
                foreach (var kv in _ln)
                {
                    Clan c;
                    if (!byId.TryGetValue(kv.Key, out c) || c == null || c.IsEliminated || c == Clan.PlayerClan || c.Leader == null || !c.Leader.IsAlive) continue;   // wymarly - zasady obieg2 tej doby
                    int parts = 0;
                    foreach (var p in kv.Value.Parts)
                    {
                        try
                        {
                            if (!(p.Left >= 0.5)) continue;
                            int kind = p.Kind == 1 ? TlCastle : TlW;
                            long a = DebtLadder.TakeFolkDebt(kv.Key, p.St, kind, p.Left);
                            if (a <= 0) continue;
                            moved += p.Left; _tlMoved += a; parts++;
                            p.Left = 0;
                        }
                        catch (Exception e) { TlStumble("TakeOverFolk(czesc)", e); }
                    }
                    kv.Value.Parts.RemoveAll(p => !(p.Left > 0));
                    if (parts > 0) { _tlMovedN++; _tlMovedParts += parts; }
                    if (kv.Value.Parts.Count == 0) drop.Add(kv.Key);
                }
                foreach (var k in drop) _ln.Remove(k);
                _tlMovedF += moved;
                if (moved > 0 && Settings.Current != null && Settings.Current.LogEnabled)
                    Log.Info("Pozyczki miejskie (f8): przejecie dlugow obieg2 (Town Loan Takes Folk Loans) - " + N0(moved) + " zl od " + _tlMovedN + " rodow w " + _tlMovedParts
                             + " czesciach przeniesione do ksiegi rad (" + _tlMoved + " zl po zaokragleniu do zlotowki; miasto - kiesa W, zamek - podzamcze, wies - kiesa wsi; "
                             + (100 * TlRate(Settings.Current)).ToString("0.#", Inv) + "% rocznie, raty i zwloka drabiny 168 zamiast umorzenia).");
            }
            catch (Exception e) { TlStumble("TakeOverFolk", e); }
            return moved;
        }

        // ------------------------------------------------------------ linia doby (po "Dlugi (168)")
        /// <summary>Linia "Pozyczki miejskie (f8): dzien N" (okno od poprzedniej linii) i start sesji. Klucz wylaczony i pusta ksiega rad - nic (log jak dotad). Nie rzuca.</summary>
        internal static void TownLoanLine()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            LastTownLent = _tlLent + _tlPlayerLent; LastTownRepaid = _tlPaid;   // "Obieg" tej doby (okno linii), takze przy linii pominietej
            try
            {
                _tlPlan.Clear();
                long debt; int clans, claims, arrear, seized;
                var book = new List<KeyValuePair<string, KeyValuePair<string, long>>>();
                DebtLadder.TownBook(out debt, out clans, out claims, out arrear, out seized, book);
                bool on = TownLoanOn;
                if (!on && claims == 0 && _tlLent + _tlPlayerLent + _tlPaid + _tlMoved + _tlInt == 0) { _tlDebtPrev = double.NaN; return; }
                if (!s.LogEnabled) { _tlDebtPrev = debt; return; }
                int today = (int)CampaignTime.Now.ToDays;
                if (!_tlStartDone)
                {
                    _tlStartDone = true;
                    Log.Info("Pozyczki miejskie (f8): start sesji | klucze: Town Loan " + (s.TownLoan ? "tak" : "nie") + " (Debt Ladder " + (DebtLadder.On ? "tak" : "nie")
                             + ", kiesa ludu " + (Live ? "czynna" : "NIECZYNNA") + ", podzamcza " + (CastleLive ? "tak" : "nie") + "), Town Loan Takes Folk Loans "
                             + (s.TownLoanTakesFolkLoans ? "tak" : "nie") + ", Folk Loans " + (s.FolkLoans ? "tak" : "nie") + " (nowe pozyczki obieg2 przy Town Loan: brak)"
                             + " | regula: rod AI w wojnie z G < R pozycza od rady wlasnego miasta / ludzi wlasnego zamku przed Bankiem; kiesy W / R / P i podzamcza ponad "
                             + TlFolkTimes(s).ToString("0.0#", Inv) + " x zapas dni, kasa kupcow ponad " + TlCofferTimes(s).ToString("0.0#", Inv) + " x zapas kupcow, najwyzej "
                             + (100 * TlSrcShare).ToString("0", Inv) + "% nadwyzki zrodla na dobe; limit " + TlRentDays(s).ToString("0", Inv) + " dni renty osady (srednia 28 dob); "
                             + (100 * TlRate(s)).ToString("0.#", Inv) + "% rocznie; udzial dnia z Bankiem " + (100 * Math.Max(0f, Math.Min(1f, s.WarCreditMaxShareD))).ToString("0", Inv)
                             + "% D; splata: wojna - odsetki z D i " + (100 * Math.Max(0f, Math.Min(1f, s.WarCreditLootRepayShare))).ToString("0", Inv)
                             + "% jednorazowych po Banku, pokoj - raty z D; wszystkie raty <= " + (100 * Math.Max(0f, s.AllInstalmentsMaxShare)).ToString("0", Inv)
                             + "% D; zwloka - drabina 168 | stan z zapisu: roszczen " + claims + " u " + clans + " rodow, dlug " + debt + "; arm_townloan wczytano "
                             + (DebtLadder.LastTownImportN < 0 ? "brak klucza" : DebtLadder.LastTownImportN + " (bledne " + DebtLadder.LastTownImportBad + ")")
                             + " | klucz Town Loan wylaczony = 7ca98719 (roszczenia z zapisu splacane dalej).");
                }
                // dlug / limit (tylko roszczenia wobec osad, ktore rod dzis trzyma)
                var places = new HashSet<string>();
                var stc = new Dictionary<string, Settlement>();
                double worst = 0; int over = 0, owned = 0;
                foreach (var e in book)
                {
                    places.Add(e.Value.Key);
                    Settlement st;
                    if (!stc.TryGetValue(e.Value.Key, out st)) { st = Settlement.Find(e.Value.Key); stc[e.Value.Key] = st; }
                    if (st == null || st.IsVillage || st.OwnerClan == null || st.OwnerClan.StringId != e.Key) continue;   // wies (dlug przejety z obieg2) i osada utracona - bez limitu
                    owned++;
                    double lim = TlRentDays(s) * ClanIncomeBook.SettlementRentAvg(st);
                    if (lim >= 1) worst = Math.Max(worst, e.Value.Value / lim);
                    if (e.Value.Value > lim + 1) over++;
                }
                long lentAll = _tlLent + _tlPlayerLent, srcAll = 0;
                for (int k = 0; k < TlKinds; k++) srcAll += _tlLentK[k];
                long repAll = 0; for (int k = 0; k < _tlRepK.Length; k++) repAll += _tlRepK[k];
                long loot = DebtLadder.TownLootToday, inst = DebtLadder.TownInstToday;
                long otherPaid = _tlPaid - loot - inst - _tlPlayerPaid;
                string ctl = double.IsNaN(_tlDebtPrev) ? "-" : Math.Round(debt - (_tlDebtPrev + lentAll + _tlMoved + _tlInt - _tlPaid - _tlOrphan)).ToString("0", Inv);
                long kw = ClanBudget.LastKwLent; int kwN = ClanBudget.LastKwLentN;
                double ms = (_tlTicks + (Stopwatch.GetTimestamp() - t0)) * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(1800);
                sb.Append("Pozyczki miejskie (f8): dzien ").Append(today)
                  .Append(" | Town Loan ").Append(on ? "tak" : "nie").Append(", ").Append((100 * TlRate(s)).ToString("0.#", Inv)).Append("% rocznie, limit ").Append(TlRentDays(s).ToString("0", Inv))
                  .Append(" dni renty osady")
                  .Append(" | zawarte (AI, 166) ").Append(_tlLent).Append(" zl u ").Append(_tlLentClans.Count).Append(" rodow, gracz ").Append(_tlPlayerLent).Append(" zl (").Append(_tlPlayerN).Append(')')
                  .Append("; zdjete ze zrodel ").Append(srcAll).Append(" (kiesy W ").Append(_tlLentK[TlW]).Append(" / R ").Append(_tlLentK[TlR]).Append(" / P ").Append(_tlLentK[TlP])
                  .Append(", kasy kupcow ").Append(_tlLentK[TlKasa]).Append(", podzamcza ").Append(_tlLentK[TlCastle]).Append(')')
                  .Append(" | kandydaci (wojna, kiesa rodziny ponizej rezerwy) ").Append(_tlCand).Append(", z miejscem ").Append(_tlRoomN).Append(" (").Append(N0(_tlRoom)).Append(" zl)")
                  .Append("; bez pozyczki: zakaz po niesplacie obieg2 ").Append(_tlBlkBan).Append(", zaleglosc / zajecie / czarna ksiega Banku ").Append(_tlBlkLadder)
                  .Append(", po zajeciu ").Append(_tlBlkSeize).Append(", D nie pokrywa kosztow stalych ").Append(_tlBlkD).Append(", limit renty ").Append(_tlBlkLimit)
                  .Append(", osady bez nadwyzki ").Append(_tlBlkNoSrc)
                  .Append(" | odsetki narosle ").Append(_tlInt).Append(" (rok ").Append(Math.Max(28, IronBank.DaysPerYearNow())).Append(" dni)")
                  .Append(" | splacone ").Append(_tlPaid).Append(" (w tym odsetki ").Append(_tlPaidInt).Append("; z jednorazowych ").Append(loot).Append(", raty z D ").Append(inst)
                  .Append(", reczne gracza ").Append(_tlPlayerPaid).Append(", zajecie i wyprzedaz ").Append(Math.Max(0, otherPaid)).Append(')')
                  .Append(" -> do wierzycieli ").Append(repAll).Append(" (kiesy W ").Append(_tlRepK[TlW]).Append(" / R ").Append(_tlRepK[TlR]).Append(" / P ").Append(_tlRepK[TlP])
                  .Append(", kasy kupcow ").Append(_tlRepK[TlKasa]).Append(", podzamcza ").Append(_tlRepK[TlCastle]).Append(", kiesy wsi (z obieg2) ").Append(_tlRepK[TlKinds])
                  .Append(", kasy osad (kiesa nieczynna) ").Append(_tlRepK[TlKinds + 1]).Append("), zapasowe kasy miast ").Append(_tlSpare)
                  .Append("; odsetki zapisane jako zarobek ludu (podstawa podatkow pana, cechu, 184 i dziesieciny) ").Append(_tlIntEarn)   // recenzja f8 (uwaga 3): <= w tym odsetki
                  .Append(" | dlug wobec rad ").Append(debt).Append(" (rodow ").Append(clans).Append(", roszczen ").Append(claims).Append(", osad ").Append(places.Count)
                  .Append("; rody w zaleglosci ").Append(arrear).Append(", w zajeciu ").Append(seized).Append(')')
                  .Append(", dlug / limit najwyzej ").Append(worst.ToString("0.000", Inv)).Append(" (roszczen wobec osad rodu ").Append(owned).Append(", ponad limitem ").Append(over)
                  .Append("), po pozyczce ponad limitem ").Append(_tlOverAfter).Append(" (oczekiwane 0)")
                  .Append(" | przeniesione z obieg2 ").Append(_tlMoved).Append(" zl (rodow ").Append(_tlMovedN).Append(", czesci ").Append(_tlMovedParts).Append(')')
                  .Append(" | bez platnika (rod wymarly bez wsi) ").Append(_tlOrphan)
                  .Append(" | kredyt wojenny Banku dzis ").Append(kw).Append(" zl u ").Append(kwN).Append(" rodow (rady + Bank ").Append(kw + _tlLent).Append(')')
                  .Append(" | kontrola: dlug - (wczoraj + zawarte + przeniesione + odsetki - splacone - bez platnika) ").Append(ctl).Append(" (oczekiwane 0)")
                  .Append(" | czas ").Append(ms.ToString("0.000", Inv)).Append(" ms")
                  .Append(" | potkniecia ").Append(_tlStumbles).Append(" (sesja ").Append(_tlStumbleSess).Append(").");
                Log.Info(sb.ToString());
                _tlDebtPrev = debt;
            }
            catch (Exception e) { TlStumble("Line", e); }
            finally { TlZero(); }
        }

        // ------------------------------------------------------------ gracz: menu wlasnego miasta i zamku
        internal static void TownLoanMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("town", "arm_townloan_borrow_town", "{=!}Borrow from the town council", TlBorrowCond, TlBorrowCons, false, 7);
                starter.AddGameMenuOption("town", "arm_townloan_repay_town", "{=!}Repay the town council", TlRepayCond, TlRepayCons, false, 8);
                starter.AddGameMenuOption("castle", "arm_townloan_borrow_castle", "{=!}Borrow from the castle folk", TlBorrowCond, TlBorrowCons, false, 7);
                starter.AddGameMenuOption("castle", "arm_townloan_repay_castle", "{=!}Repay the castle folk", TlRepayCond, TlRepayCons, false, 8);
                Log.Info("Pozyczki miejskie (f8): pozycje \"Borrow from the town council\" / \"Borrow from the castle folk\" i \"Repay ...\" w menu miasta i zamku wpiete (klucz Town Loan).");
            }
            catch (Exception e) { Log.Error("TownFolk.TownLoanMenus", e); }
        }

        private static string TlWho(Settlement st) { return (st.IsTown ? "The council of " : "The folk of ") + (st.Name != null ? st.Name.ToString() : "this place"); }
        private static string TlWhom(Settlement st) { return (st.IsTown ? "the council of " : "the folk of ") + (st.Name != null ? st.Name.ToString() : "this place"); }

        private static void TlSay(string text, bool good) { try { InformationManager.DisplayMessage(new InformationMessage(text, good ? Colors.Green : Colors.Red)); } catch { } }

        /// <summary>
        /// f8-pozyczka (projekt R2 rozdz. 7, "nie zrobione" pierwszego przebiegu): zdanie do okna "People of this land" - ile lud tej osady pozyczyl swojemu panu
        /// (gracz - "you") i ile winni mu jeszcze inni panowie (dlug zostaje po utracie osady). Tylko odczyt ksiegi drabiny 168 (jedno przejscie po rodach). null - brak
        /// dlugu wobec tej osady (okno jak dotad). Linia "PeopleMenu (f8)" do logu. Nie rzuca.
        /// </summary>
        internal static string TownLoanMenuLine(Settlement st)
        {
            try
            {
                var s = Settings.Current;
                if (st == null || s == null || string.IsNullOrEmpty(st.StringId) || !DebtLadder.On) return null;
                long own, others; int otherN;
                DebtLadder.TownOwedTo(st.StringId, st.OwnerClan, out own, out others, out otherN);
                if (own <= 0 && others <= 0) return null;
                string rate = (100 * TlRate(s)).ToString("0.#", Inv) + " parts in 100 a year";
                bool you = st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan;
                string t = own > 0
                    ? "Lent to " + (you ? "you" : "their lord") + ": " + own.ToString("N0", Inv) + " denars at " + rate + "."
                      + (others > 0 ? " Other lords still owe them " + others.ToString("N0", Inv) + " denars." : "")
                    : "Lent to other lords: " + others.ToString("N0", Inv) + " denars at " + rate + ".";
                if (s.LogEnabled)
                    Log.Info("PeopleMenu (f8): " + (st.Name != null ? st.Name.ToString() : st.StringId) + " (" + st.StringId + ") | dlug pana osady wobec ludu " + own
                             + " zl, innych rodow " + others + " zl (" + otherN + " rodow).");
                return t;
            }
            catch (Exception e) { TlStumble("MenuLine", e); return null; }
        }

        private static bool TlBorrowCond(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var st = Settlement.CurrentSettlement;
                return st != null && (st.IsTown || st.IsCastle) && Clan.PlayerClan != null && st.OwnerClan == Clan.PlayerClan && TownLoanOn && DebtLadder.PlayerOnLadder;
            }
            catch { return false; }
        }

        private static bool TlRepayCond(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var st = Settlement.CurrentSettlement;
                return st != null && (st.IsTown || st.IsCastle) && DebtLadder.On && DebtLadder.TownDebt(Clan.PlayerClan, st.StringId) > 0;
            }
            catch { return false; }
        }

        private static void TlBorrowCons(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement; var c = Clan.PlayerClan; var s = Settings.Current;
                if (st == null || c == null || s == null) return;
                string who = TlWho(st), place = st.IsTown ? "town" : "castle";
                string why;
                if (!DebtLadder.CanBorrowTown(c, out why)) { TlSay(why.Trim(), false); return; }
                if (st.IsUnderSiege) { TlSay(who + " cannot lend while the enemy is at the gates.", false); return; }
                var p = TlPlaceFor(c, st, s);
                if (p == null) { TlSay(who + " keeps no common purse that could lend.", false); return; }
                if (p.Room < 1) { TlSay(who + " will not lend you more: you owe them " + N0(p.Debt) + " denars, as much as " + N0(TlRentDays(s)) + " days of the rent the " + place + " pays you.", false); return; }
                long room = (long)Math.Floor(Math.Min(p.Room, p.Avail));
                if (room < 1) { TlSay(who + " has nothing to spare today above what the people keep for hard days.", false); return; }
                var opts = new List<InquiryElement>();
                foreach (var share in new[] { 0.25, 0.5, 1.0 })
                {
                    long amt = (long)(room * share);
                    if (amt > 0) opts.Add(new InquiryElement(amt, amt.ToString("N0", Inv) + " denars", null));
                }
                string text = who + " will lend you up to " + room.ToString("N0", Inv) + " denars today at " + (100 * TlRate(s)).ToString("0.#", Inv) + " parts in 100 a year, against the rents the "
                              + place + " pays you - at most " + N0(TlRentDays(s)) + " days of them (you owe them " + N0(p.Debt) + " now). The coin comes from the savings of "
                              + (st.IsTown ? "the townsfolk and the merchants' coffer" : "the castle folk") + ", never from their bread money. You pay it back out of your steady income: "
                              + "all your creditors together take at most " + (100 * Math.Max(0f, s.AllInstalmentsMaxShare)).ToString("0", Inv) + "% of it a day. Miss three instalments "
                              + "and your creditors take the income of your villages until all is paid.";
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(who, text, opts, true, 1, 1, "Borrow", "Leave",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        TlPlayerBorrow(st, Convert.ToInt64(sel[0].Identifier));
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { TlStumble("BorrowMenu", e); }
        }

        private static void TlPlayerBorrow(Settlement st, long want)
        {
            _tlOutside = true;
            try
            {
                var c = Clan.PlayerClan; var s = Settings.Current; var h = Hero.MainHero;
                if (st == null || c == null || s == null || h == null || want <= 0 || st.OwnerClan != c || !TownLoanOn) return;
                string why;
                if (!DebtLadder.CanBorrowTown(c, out why)) { TlSay(why.Trim(), false); return; }
                var p = TlPlaceFor(c, st, s);
                if (p == null) return;
                long amt = Math.Min(want, (long)Math.Floor(Math.Min(p.Room, p.Avail)));
                long got = amt > 0 ? TlLendPlace(c, h, p, amt, true) : 0;
                if (got > 0)
                {
                    TlSay(TlWho(st) + " lends you " + got.ToString("N0", Inv) + " denars.", true);
                    Log.Info("Pozyczki miejskie (f8): gracz pozycza od " + (st.Name != null ? st.Name.ToString() : st.StringId) + " " + got + " zl (dlug wobec tej osady "
                             + DebtLadder.TownDebt(c, st.StringId) + ", limit " + N0(p.Limit) + ").");
                }
                else TlSay(TlWho(st) + " has nothing to lend now.", false);
            }
            catch (Exception e) { TlStumble("PlayerBorrow", e); }
            finally { _tlOutside = false; }
        }

        private static void TlRepayCons(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement; var c = Clan.PlayerClan; var h = Hero.MainHero;
                if (st == null || c == null || h == null) return;
                string who = TlWho(st);
                long owe = DebtLadder.TownDebt(c, st.StringId);
                if (owe <= 0) return;
                long can = Math.Min(owe, Math.Max(0, h.Gold));
                if (can <= 0) { TlSay("You have no gold to repay.", false); return; }
                var opts = new List<InquiryElement>();
                foreach (var share in new[] { 0.25, 0.5, 1.0 })
                {
                    long amt = (long)(can * share);
                    if (amt > 0) opts.Add(new InquiryElement(amt, amt.ToString("N0", Inv) + " denars" + (amt >= owe ? " (all)" : ""), null));
                }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(who, "You owe " + TlWhom(st) + " " + owe.ToString("N0", Inv) + " denars. How much will you repay?",
                    opts, true, 1, 1, "Repay", "Leave",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        TlPlayerRepay(st, Convert.ToInt64(sel[0].Identifier));
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { TlStumble("RepayMenu", e); }
        }

        private static void TlPlayerRepay(Settlement st, long want)
        {
            _tlOutside = true;
            try
            {
                var c = Clan.PlayerClan;
                if (st == null || c == null || want <= 0) return;
                long paid = DebtLadder.PlayerRepayTown(st.StringId, want);
                if (paid <= 0) { TlSay("You have no gold to repay.", false); return; }
                _tlPlayerPaid += paid;
                long left = DebtLadder.TownDebt(c, st.StringId);
                if (left <= 0) TlSay("Your debt to " + TlWhom(st) + " is paid.", true);
                else TlSay("You repay " + paid.ToString("N0", Inv) + " denars to " + TlWhom(st) + ". You still owe " + left.ToString("N0", Inv) + ".", true);
                Log.Info("Pozyczki miejskie (f8): gracz splaca " + (st.Name != null ? st.Name.ToString() : st.StringId) + " " + paid + " zl, zostalo " + left + " zl.");
            }
            catch (Exception e) { TlStumble("PlayerRepay", e); }
            finally { _tlOutside = false; }
        }
    }
}
