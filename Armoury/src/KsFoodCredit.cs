using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KS-R4 r4-kupcy H5 (noc 17/18.10, PLAN-KS-RUNDA4 rozdz. 3; projekt DS F2 "kupcy na wlasnym kapitale"): KREDYT KUPIECKI KS-H - rachunki miast celu wobec miast
    /// zrodla za jedzenie dowiezione na kredyt (klucz Ks Food Carts Consign). Woz wyjezdza bez zaplaty (fracht placi zrodlo), przy dostawie cel placi wartosc lotu
    /// u siebie (KsFoodCore.DestIntegral) z kasy ponad KsConsignFloorShare x zapas kupcow, reszta to RACHUNEK (zrodlo, cel, zostalo, doba). Codziennie (SupplyDemand.DailyTrade,
    /// przed wozami kupcow) kazdy cel splaca swoje rachunki od najstarszego z kasy ponad te sama podloge. Rachunek starszy niz KsConsignOverdueDays - cel bez nowych dostaw
    /// (KsFoodCore DstOverdue). Wojna miedzy zrodlem a celem - rachunek przepada ("kupcy stracili": znika roszczenie, zloto nie powstaje i nie ginie; licznik).
    /// Zmiana wlasciciela w jednym krolestwie (i pokoj) - rachunek zostaje. Najwyzej 8 wpisow na pare (zrodlo, cel): nowy przy pelnej liscie dopisywany do najnowszego.
    /// ZLOTO: kazda moneta cel -> zrodlo (ChangeGold z pomiarem obu stron); kontrola doby: otwarte wczoraj + nowe - splacone - przepadle = otwarte dzis (reszta 0).
    /// ZAPIS: klucz arm_daniny_kupcy "kk1|zrodlo~cel~zostalo~doba|..." (ponad 64 KB - wpisy pary scalane w jeden, najstarsza doba; nic nie przepada); brak klucza
    /// (stary zapis) = brak rachunkow; starsza DLL klucza nie czyta. Recenzja r4-kupcy: 64 KB to limit MIEKKI - po scaleniu zostaje jeden wpis (ok. 40 znakow) na pare
    /// (zrodlo, cel) z rachunkiem; par nie da sie scalic bez utraty odbiorcy splaty, wiec ponad ok. 1600 par zapis zostaje wiekszy (SaveText dzieli go na paczki) i linia
    /// zapisu mowi "NADAL PONAD 64 KB" (NKU-10 NIE). Gorna granica: pary miast tego samego rynku, w praktyce do 20 zrodel na miasto (12 najblizszych + 8 dalszych).
    /// KS-R5 ceny-r5 (recenzja): roszczenia z zadatkow kupcow celu (OpenClaim) na OSOBNYCH kontach pary (Claim, wpis zapisu z 5. polem "z"): splacane w PayDaily
    /// jak rachunki, ale przy wojnie czekaja do pokoju (recenzja calosci 17/18.10 - nie przepadaja), nie blokuja dowozu do dluznika (OverdueDst) i nie placi ich pan
    /// glodnego miasta (OverdueOf / PayByLord).
    /// </summary>
    internal static class KsFoodCredit
    {
        private sealed class Bill { internal long Left; internal int Day; }
        private sealed class Acct { internal Settlement Src, Dst; internal bool Claim; internal readonly List<Bill> B = new List<Bill>(8); }   // recenzja ceny-r5: Claim - roszczenie z zadatku (osobne konto pary)

        private const int MaxEntries = 8, SaveCapChars = 64 * 1024;
        private const string Ver = "kk1";
        private static readonly Dictionary<string, Acct> _acc = new Dictionary<string, Acct>(StringComparer.Ordinal);
        private static string _pending;
        // liczniki doby (linia KS-H) i od wczytania
        internal static long DNew, DPaidNow, DBilled, DRepaid, DForfeit; internal static int DNewN, DForfeitN, DDeliv;
        internal static long GDstPaid, GSrcGot;            // zloto doby: cel zaplacil / zrodla dostaly (przy dostawie + splaty)
        internal static long ForfeitTotal; internal static int ForfeitTotalN;
        // scalenie r4-glodbunt + r4-kupcy (noc 17/18.10): doba - zalegle rachunki splacone przez panow glodnych miast (KsLordFood tryb RACHUNKI; w DRepaid tez),
        // zloto od panow przyjete przez kasy zrodel (w GSrcGot tez - kontrola zlota linii KS-H: cel - panowie + zrodla), splat (miasto-doby)
        internal static long DLordRepaid, GLordPaid; internal static int DLordN;
        // f4 P5 (przeglad ekonomii 18.10, uwaga 6): z tego splacone przez panow PRZED zwloka (rachunki mlodsze niz Ks Consign Overdue Days - wczesna splata, klucz
        // Ks Lord Food Bills Early Share; w DLordRepaid tez) i liczba takich splat
        internal static long DLordEarly; internal static int DLordEarlyN;
        // KS-R5 ceny-r5 (zadatek kupcow celu): roszczenia celu wobec zrodla (nadplacony albo niezwrocony zadatek, ktorego kasa zrodla nie oddala od razu) - rachunek
        // odwrotny (wierzyciel = miasto celu, dluznik = miasto zrodla), splacany jak kazdy rachunek; w kontroli rachunkow obok "nowe" (nie w "u celu")
        internal static long DClaimNew; internal static int DClaimN;
        private static long _openPrev = -1;
        private static int _stum; private static readonly HashSet<string> _err = new HashSet<string>();

        // 322e (Jeff 03.11, audyt ekonomii p. 5 "rachunki zywnosci: dluznik splaca rata z biezacego zarobku, nawet gdy jest ponizej rezerwy"): RATA Z ZAROBKU.
        // Dotad dluznik splacal tylko z kasy ponad KsConsignFloorShare x zapas kupcow - Qarth i Braavos (kasa 1-20 tys., zapas ok. 340 tys.) nie splacaly nigdy
        // (1.06 mln otwartych, najstarszy 364 dni). Teraz po splacie z nadwyzki dluznik doklada do raty: Ks Consign Rate Share x zarobek doby kupcow miasta
        // (TownPurse.NetOf - doplyw netto okna blok-blok, czyli zarobek PRZED poborem pana i korony) + to, co miasto splacilo w poprzedniej dobie (splata
        // starego dlugu to nie koszt dnia - inaczej rata zjadalaby sama siebie). Rata placona z zywej kasy (najwyzej cala kasa), od najstarszego, takze ponizej
        // podlogi. Zloto: kasa dluznika -> kasa wierzyciela (Transfer z pomiarem obu stron), nic z niczego. Brak pomiaru (TownPurse wylaczony, pierwsza doba) albo
        // zarobek <= 0 - bez raty (jak dotad). Udzial 0 - bit w bit jak przed 322e (poza linia).
        // ODBLOKOWANIE DOWOZU: dluznik, ktory dzis zaplacil pelna rate (rata > 0) i ktorego rachunki za jedzenie po splacie <= rata x Ks Consign Rate Release Days,
        // dostaje znow dowoz (OverdueDst go pomija tej doby). Uzasadnienie: blokada ma chronic wierzycieli przed dluznikiem, ktory nie placi; dluznik placacy
        // w terminie, ktorego dlug jego rata splaci w tym horyzoncie, jest wiarygodny - glodzenie go nie pomaga nikomu (nowy rachunek tez zmiesci sie w horyzoncie,
        // bo warunek liczy CALY dlug za jedzenie, wiec dlug nie urosnie ponad rata x horyzont). Release Days 0 - blokada jak dotad.
        private static int _payDay = int.MinValue, _relDay = int.MinValue;
        private static Dictionary<Settlement, long> _paidToday = new Dictionary<Settlement, long>(), _paidPrev = new Dictionary<Settlement, long>();
        // recenzja 322e: splaty rachunkow dluznika przez pana / skarbiec korony (KsLordFood RACHUNKI, BeforeEarly, HoardBills - ta sama kwota co TownPurse.NoteLordFood,
        // po zwrotach). Kasa dluznika sie nie rusza, a CloseWindow zdejmuje ja z jego doplywu netto - bez dodania z powrotem rata malalaby o 15% splaty pana
        // (miasto, ktoremu pan pomaga, placiloby mniej albo wcale i nie dostawalo odblokowania). Doba jak _paidToday (pan placi po PayDaily -> okno jutra).
        // Doplata DOPLATA (Settle) zostaje w kasie miasta - tej nie liczymy.
        private static Dictionary<Settlement, long> _lordPaidToday = new Dictionary<Settlement, long>(), _lordPaidPrev = new Dictionary<Settlement, long>();
        private static readonly HashSet<Settlement> _released = new HashSet<Settlement>();
        // linia doby 322e: dluznicy, z rata, w terminie, odblokowani, bez pomiaru, splacone z raty
        internal static long DRateRepaid; internal static int DRateN, DRateDebtors, DRateOnTime, DRateReleased, DRateNoMeasure;

        internal static double RateShare { get { var s = Settings.Current; return s == null ? 0.15 : Math.Max(0.0, Math.Min(0.5, s.KsConsignRateShare)); } }
        internal static int RateReleaseDays { get { var s = Settings.Current; return s == null ? 91 : Math.Max(0, Math.Min(364, s.KsConsignRateReleaseDays)); } }

        internal static void Reset()
        {
            _acc.Clear(); _pending = null; _openPrev = -1; ForfeitTotal = 0; ForfeitTotalN = 0; _stum = 0; _err.Clear();
            _payDay = int.MinValue; _relDay = int.MinValue; _paidToday.Clear(); _paidPrev.Clear(); _released.Clear();
            _lordPaidToday.Clear(); _lordPaidPrev.Clear();
            ClearDay();
        }

        internal static void ClearDay()
        {
            DNew = DPaidNow = DBilled = DRepaid = DForfeit = 0; DNewN = DForfeitN = DDeliv = 0; GDstPaid = GSrcGot = 0;
            DLordRepaid = GLordPaid = 0; DLordN = 0;
            DLordEarly = 0; DLordEarlyN = 0;
            DClaimNew = 0; DClaimN = 0;
            DRateRepaid = 0; DRateN = DRateDebtors = DRateOnTime = DRateReleased = DRateNoMeasure = 0;
        }

        internal static int TakeStumbles() { int n = _stum; _stum = 0; return n; }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            try { if (_err.Add(where)) Log.Error("KsFoodCredit." + where, e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string Key(Settlement s, Settlement d) { return s.StringId + "|" + d.StringId; }
        /// <summary>Recenzja ceny-r5: konto roszczen z zadatkow (wierzyciel = cel, ktory zaplacil zadatek, dluznik = zrodlo) - osobne od rachunkow za jedzenie tej pary.</summary>
        private static string ClaimKey(Settlement creditor, Settlement debtor) { return "z|" + creditor.StringId + "|" + debtor.StringId; }

        internal static bool Any { get { Resolve(); return _acc.Count > 0; } }

        /// <summary>Cena podlogi kasy celu (zaplata przy dostawie i splaty): KsConsignFloorShare x zapas kupcow (0..1).</summary>
        internal static double FloorShare { get { var s = Settings.Current; return s == null ? 0.5 : Math.Max(0.0, Math.Min(1.0, s.KsConsignFloorShare)); } }
        internal static int OverdueDays { get { var s = Settings.Current; return s == null ? 30 : Math.Max(1, Math.Min(364, s.KsConsignOverdueDays)); } }

        /// <summary>Kasa celu ponad podloge (zl) - z tego placi przy dostawie i splaca rachunki.</summary>
        private static long Free(Town t)
        {
            if (t == null) return 0;
            long res = 0; try { res = Seal31.TownReserve(t); } catch { res = 0; }
            return FreeAbove(t.Gold, res);
        }

        /// <summary>KS-R5 ceny-r5: kasa ponad podloge kredytu (KsConsignFloorShare x zapas kupcow) - ta sama granica dla zaplaty przy dostawie, splat i zadatku przy wysylce.</summary>
        internal static long FreeAbove(long gold, long reserve) { return Math.Max(0L, gold - (long)Math.Ceiling(FloorShare * Math.Max(0L, reserve))); }

        /// <summary>KS-R5 ceny-r5: kasa miasta ponad podloge kredytu teraz (zadatek przy wysylce).</summary>
        internal static long FreeOf(Town t) { return Free(t); }

        /// <summary>KS-R5 ceny-r5 (zadatek): roszczenie wierzyciela `creditor` (miasto celu, ktore zaplacilo zadatek) wobec dluznika `debtor` (miasto zrodla bez kasy na zwrot
        /// nadplaty albo zadatku zawroconego wozu) - splata jak rachunek (od najstarszego z kasy ponad podloge w PayDaily; wojna - czeka do pokoju, recenzja calosci); zloto sie nie rusza.
        /// Recenzja ceny-r5: osobne konto (Claim) - zwrot zadatku to nie rachunek za jedzenie: NIE blokuje dowozu do dluznika (OverdueDst) i NIE placi go pan glodnego
        /// miasta (OverdueOf / PayByLord - pan placi za jedzenie dowiezione do swojego miasta, a dluznik roszczenia jedzenia za nie nie dostal).</summary>
        internal static void OpenClaim(Settlement creditor, Settlement debtor, long x, int today)
        {
            Resolve();
            if (x <= 0 || creditor == null || debtor == null || creditor == debtor) return;
            Acct a; string k = ClaimKey(creditor, debtor);
            if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = creditor, Dst = debtor, Claim = true }; _acc[k] = a; }
            var last = a.B.Count > 0 ? a.B[a.B.Count - 1] : null;
            if (last != null && (last.Day == today || a.B.Count >= MaxEntries)) last.Left += x;
            else a.B.Add(new Bill { Left = x, Day = today });
            DClaimNew += x; DClaimN++;
        }

        /// <summary>Przelew cel -> zrodlo z pomiarem obu stron; zwraca to, co zeszlo z kasy celu (zrodlo dostaje dokladnie tyle - kasa zrodla nie ma sufitu).</summary>
        private static long Transfer(Town dst, Town src, long x)
        {
            if (x <= 0 || dst == null || src == null) return 0;
            int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, dst.Gold)));
            if (ask <= 0) return 0;
            int d0 = dst.Gold; dst.ChangeGold(-ask); long got = d0 - dst.Gold;
            if (got <= 0) return 0;
            int s0 = src.Gold; src.ChangeGold((int)got); long rec = src.Gold - s0;
            GDstPaid += got; GSrcGot += rec;
            return got;
        }

        private static bool AtWar(Settlement a, Settlement b)
        {
            try { return a.MapFaction != null && b.MapFaction != null && FactionManager.IsAtWarAgainstFaction(a.MapFaction, b.MapFaction); } catch { return false; }
        }

        /// <summary>Dostawa wozu na kredyt (KsFoodCarts.Arrivals): cel placi z kasy ponad podloge najwyzej wartosc lotu u siebie, reszta = nowy rachunek. Zwraca zaplacone teraz.</summary>
        internal static long Deliver(Settlement src, Settlement dst, long bill, int today)
        {
            Resolve();
            if (bill <= 0 || src == null || dst == null || src.Town == null || dst.Town == null) return 0;
            DBilled += bill; DDeliv++;
            long now = Transfer(dst.Town, src.Town, Math.Min(bill, Free(dst.Town)));
            DPaidNow += now;
            long rest = bill - now;
            if (rest > 0) Open(src, dst, rest, today);
            return now;
        }

        private static void Open(Settlement src, Settlement dst, long x, int today)
        {
            Acct a; string k = Key(src, dst);
            if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = src, Dst = dst }; _acc[k] = a; }
            var last = a.B.Count > 0 ? a.B[a.B.Count - 1] : null;
            if (last != null && (last.Day == today || a.B.Count >= MaxEntries)) last.Left += x;      // ta sama doba albo pelna lista - do najnowszego
            else a.B.Add(new Bill { Left = x, Day = today });
            DNew += x; DNewN++;
        }

        /// <summary>322e: nowa doba splat - dzisiejsze splaty (wlasne i panow) przechodza na "wczoraj"; przerwa (pierwsza doba po wczytaniu) - wczorajszych nie znamy.</summary>
        private static void RotatePay(int today)
        {
            if (_payDay == today) return;
            var t0 = _paidPrev; _paidPrev = _paidToday; _paidToday = t0; _paidToday.Clear();
            var t1 = _lordPaidPrev; _lordPaidPrev = _lordPaidToday; _lordPaidToday = t1; _lordPaidToday.Clear();
            if (_payDay != today - 1) { _paidPrev.Clear(); _lordPaidPrev.Clear(); }
            _payDay = today;
        }

        /// <summary>Recenzja 322e: KsLordFood (RACHUNKI, BeforeEarly, HoardBills) - pan / skarbiec splacil rachunki dluznika `dst` kwota `x` (ta sama, co TownPurse.NoteLordFood,
        /// po zwrotach). Jutro dodana do zarobku doby dluznika (podstawa raty). Nie dla doplaty DOPLATA (zostaje w kasie miasta). Nie rzuca.</summary>
        internal static void NoteLordBills(Settlement dst, long x)
        {
            if (dst == null || x <= 0) return;
            try { RotatePay(Today()); long v; _lordPaidToday.TryGetValue(dst, out v); _lordPaidToday[dst] = v + x; }
            catch (Exception e) { Stumble("NoteLordBills", e); }
        }

        /// <summary>Splata doby (SupplyDemand.DailyTrade, przed wozami): wojna - rachunek przepada; potem kazdy cel od najstarszego z kasy ponad podloge. Dziala zawsze,
        /// gdy sa rachunki (takze przy wylaczonym kluczu - dlugi trzeba splacac).</summary>
        internal static void PayDaily()
        {
            Resolve();
            if (_acc.Count == 0) return;
            int today = Today();
            try
            {
                // rachunki wedlug celu (stala kolejnosc: StringId celu, potem doba, potem StringId zrodla)
                var byDst = new SortedDictionary<string, List<KeyValuePair<Acct, Bill>>>(StringComparer.Ordinal);
                var gone = new List<string>();
                foreach (var kv in _acc)
                {
                    var a = kv.Value;
                    try
                    {
                        // recenzja calosci (noc 17/18.10): roszczenie z zadatku (Claim) przy wojnie CZEKA do pokoju (bez splaty i bez przepadku) - zrodlo wozu zawroconego wojna
                        // odebralo caly towar, wiec przepadek roszczenia zostawialby mu tez zadatek (cel placilby za nic); rachunki za dowiezione jedzenie - jak dotad
                        if (a.Claim && a.Src != null && a.Dst != null && a.Src.Town != null && a.Dst.Town != null && AtWar(a.Src, a.Dst)) continue;
                        if (a.Src == null || a.Dst == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst))
                        {
                            // kupcy stracili - roszczenie znika (bez zlota)
                            foreach (var b in a.B) { DForfeit += b.Left; DForfeitN++; ForfeitTotal += b.Left; ForfeitTotalN++; }
                            a.B.Clear(); gone.Add(kv.Key); continue;
                        }
                        List<KeyValuePair<Acct, Bill>> l;
                        if (!byDst.TryGetValue(a.Dst.StringId, out l)) { l = new List<KeyValuePair<Acct, Bill>>(); byDst[a.Dst.StringId] = l; }
                        foreach (var b in a.B) l.Add(new KeyValuePair<Acct, Bill>(a, b));
                    }
                    catch (Exception e) { Stumble("PayDaily.war", e); }
                }
                // 322e: doba splat - wczorajsze splaty dluznikow (dodawane do zarobku doby: okno TownPurse zamkniete dzis rano zawiera wczorajsza PayDaily)
                RotatePay(today);
                _released.Clear(); _relDay = today;
                double share = RateShare; int relDays = RateReleaseDays;
                var rows = new List<string>();
                foreach (var kv in byDst)
                {
                    try
                    {
                        var l = kv.Value; if (l.Count == 0) continue;
                        l.Sort((x, y) => x.Value.Day != y.Value.Day ? x.Value.Day.CompareTo(y.Value.Day) : string.CompareOrdinal(x.Key.Src.StringId, y.Key.Src.StringId));
                        var dst = l[0].Key.Dst; var dt = dst.Town;
                        long gold0 = dt.Gold, owed0 = 0, food0 = 0;
                        foreach (var p in l) { if (p.Value.Left <= 0) continue; owed0 += p.Value.Left; if (!p.Key.Claim) food0 += p.Value.Left; }
                        long free = Free(dt), paidS = 0, paidR = 0;
                        foreach (var p in l)
                        {
                            if (free <= 0) break;
                            var b = p.Value; if (b.Left <= 0) continue;
                            long got = Transfer(dt, p.Key.Src.Town, Math.Min(free, b.Left));
                            if (got <= 0) break;
                            b.Left -= got; free -= got; DRepaid += got; paidS += got;
                        }
                        // 322e: rata z zarobku doby - dopelnienie splaty z nadwyzki do raty, z zywej kasy (takze ponizej podlogi), od najstarszego
                        long inc = 0, net = 0, prev = 0, lprev = 0, rate = 0; bool meas = false;
                        if (share > 0 && owed0 > 0)
                        {
                            DRateDebtors++;
                            _paidPrev.TryGetValue(dst, out prev);
                            _lordPaidPrev.TryGetValue(dst, out lprev);     // recenzja 322e: splaty panow / korony wczoraj (CloseWindow zdjal je z netto, kasa sie nie ruszyla)
                            meas = TownPurse.NetOf(dt, out net);
                            if (!meas) DRateNoMeasure++;
                            else
                            {
                                inc = net + prev + lprev;
                                rate = inc > 0 ? (long)Math.Ceiling(share * inc) : 0;
                                long due = Math.Min(owed0 - paidS, Math.Max(0L, rate - paidS));
                                foreach (var p in l)
                                {
                                    if (due <= 0) break;
                                    var b = p.Value; if (b.Left <= 0) continue;
                                    long got = Transfer(dt, p.Key.Src.Town, Math.Min(due, b.Left));
                                    if (got <= 0) break;
                                    b.Left -= got; due -= got; DRepaid += got; paidR += got; DRateRepaid += got;
                                }
                                if (paidR > 0) DRateN++;
                            }
                        }
                        long paid = paidS + paidR;
                        if (paid > 0) { long v; _paidToday.TryGetValue(dst, out v); _paidToday[dst] = v + paid; }
                        if (share > 0 && owed0 > 0)
                        {
                            long food1 = 0; int oldest = int.MaxValue;
                            foreach (var p in l) if (!p.Key.Claim && p.Value.Left > 0) { food1 += p.Value.Left; oldest = Math.Min(oldest, p.Value.Day); }
                            bool onTime = meas && rate > 0 && paid >= Math.Min(rate, owed0);
                            if (onTime) DRateOnTime++;
                            bool rel = onTime && relDays > 0 && food1 > 0 && food1 <= rate * (long)relDays;
                            if (rel) { _released.Add(dst); DRateReleased++; }
                            if (rows.Count < 24)
                                rows.Add((dst.Name != null ? dst.Name.ToString() : dst.StringId) + ": dlug " + owed0 + " (za jedzenie " + food0 + ", najstarszy " + (oldest == int.MaxValue ? "-" : (today - oldest).ToString(CultureInfo.InvariantCulture)) + " dni)"
                                         + ", zarobek doby " + (meas ? inc.ToString(CultureInfo.InvariantCulture) + " (netto " + net + " + splaty wczoraj " + prev + " + splaty panow wczoraj " + lprev + ")" : "bez pomiaru")
                                         + ", rata " + rate + ", splacone z nadwyzki " + paidS + " + z raty " + paidR + ", kasa " + gold0 + " -> " + dt.Gold
                                         + ", zostalo za jedzenie " + food1 + (rate > 0 ? " (" + ((food1 + rate - 1) / rate) + " dob raty)" : "")
                                         + ", w terminie " + (onTime ? "TAK" : "NIE") + ", dowoz " + (food1 <= 0 ? "-" : rel ? "ODBLOKOWANY" : "wedlug zwloki"));
                        }
                    }
                    catch (Exception e) { Stumble("PayDaily.pay", e); }
                }
                foreach (var kv in _acc) { kv.Value.B.RemoveAll(b => b.Left <= 0); if (kv.Value.B.Count == 0) gone.Add(kv.Key); }
                foreach (var k in gone) _acc.Remove(k);
                if (share > 0 && DRateDebtors > 0)
                {
                    try
                    {
                        Log.Info("Rachunki zywnosci - rata z zarobku (322e): dzien " + today + " | udzial " + share.ToString("0.00", CultureInfo.InvariantCulture) + ", horyzont odblokowania "
                                 + relDays + " dob | dluznikow " + DRateDebtors + ", z rata " + DRateN + ", w terminie " + DRateOnTime + ", dowoz odblokowany " + DRateReleased
                                 + ", bez pomiaru zarobku " + DRateNoMeasure + " | splacone z raty " + DRateRepaid + " zl (kasy dluznikow -> kasy wierzycieli), potkniecia kredytu " + _stum + ".");
                        foreach (var r in rows) Log.Info("Rachunki zywnosci - dluznik (322e): dzien " + today + " | " + r + ".");
                    }
                    catch { }
                }
            }
            catch (Exception e) { Stumble("PayDaily", e); }
        }

        // ------------------------------------------------------------ scalenie r4-glodbunt + r4-kupcy (noc 17/18.10): pan glodnego miasta splaca zalegle rachunki
        // KsLordFood (tryb RACHUNKI, klucz Ks Lord Funds Food, kredyt kupcow Ks Food Carts Consign TAK): Before - po splatach miast (PayDaily) i dostawach (Arrivals),
        // przed decyzja wozow - glodne miasto, ktorego rachunki sa starsze niz zwloka (te blokuja nowe dostawy - DstOverdue), dostaje splate od glowy rodu wlasciciela:
        // tylko zloto ponad rezerwe wojny i najwyzej tempo skrzyni doby (te same granice co chleb M3 - KsLordFood). Splata od najstarszego, zloto prosto do kas zrodel
        // (wierzycieli), z pomiarem; czego kasy zrodel nie przyjma, wraca do pana. Rachunki splacone przez pana licza sie do "splacone" linii KS-H (kontrola rachunkow)
        // i do zlota linii KS-H (panowie -, zrodla +). Nic z niczego: zloto glowa -> kasy zrodel, roszczenie maleje o tyle samo.

        /// <summary>KsLordFood.FoodBillsOpen: zalegle rachunki zywnosci miasta `dst` jako celu (zl) - starsze niz zwloka (KsConsignOverdueDays); 0 - brak.</summary>
        internal static long OverdueOf(Settlement dst) { return OlderThanOf(dst, OverdueDays); }

        /// <summary>f4 P5: rachunki zywnosci miasta `dst` jako celu starsze niz `age` dni (zl; bez roszczen z zadatkow i rachunkow par w wojnie) - OverdueOf przy age =
        /// Ks Consign Overdue Days (bit w bit jak dotad).</summary>
        internal static long OlderThanOf(Settlement dst, int age)
        {
            Resolve();
            if (dst == null || _acc.Count == 0) return 0;
            int today = Today(); long s = 0;
            foreach (var a in _acc.Values)
            {
                if (a.Claim || a.Dst != dst || a.Src == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst)) continue;   // recenzja ceny-r5: bez roszczen z zadatkow
                foreach (var b in a.B) if (b.Left > 0 && today - b.Day > age) s += b.Left;
            }
            return s;
        }

        /// <summary>f4 P5 (wczesna splata panow): jeden przebieg po rachunkach - miasta celu z rachunkiem zywnosci starszym niz `age` dni (te same filtry co OlderThanOf):
        /// kwota takich rachunkow i doba najstarszego. Rachunki w wojnie i roszczenia z zadatkow pominiete (pan ich nie placi).</summary>
        internal static void OlderThanByDst(int age, Dictionary<Settlement, long> sum, Dictionary<Settlement, int> oldest)
        {
            sum.Clear(); oldest.Clear();
            Resolve();
            if (_acc.Count == 0) return;
            int today = Today();
            foreach (var a in _acc.Values)
            {
                if (a.Claim || a.Dst == null || a.Src == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst)) continue;
                foreach (var b in a.B)
                {
                    if (b.Left <= 0 || today - b.Day <= age) continue;
                    long v; sum.TryGetValue(a.Dst, out v); sum[a.Dst] = v + b.Left;
                    int d; if (!oldest.TryGetValue(a.Dst, out d) || b.Day < d) oldest[a.Dst] = b.Day;
                }
            }
        }

        /// <summary>f4 P5 (linia KS-H): wiek najstarszego rachunku zywnosci (dni; -1 - brak) i kwota rachunkow starszych niz `age` dni (bez roszczen z zadatkow).</summary>
        internal static void AgeStats(int today, int age, out int oldestAge, out long older)
        {
            Resolve();
            oldestAge = -1; older = 0;
            foreach (var a in _acc.Values)
            {
                if (a.Claim) continue;
                foreach (var b in a.B)
                {
                    if (b.Left <= 0) continue;
                    int g = today - b.Day;
                    if (g > oldestAge) oldestAge = g;
                    if (g > age) older += b.Left;
                }
            }
        }

        /// <summary>KsLordFood.FoodBillsPay: splata zaleglych rachunkow `dst` (najstarsze najpierw; ta sama doba - wedlug StringId zrodla, jak PayDaily) kwota `x` JUZ
        /// zdjeta panu - przelew do kas zrodel z pomiarem obu stron; zwraca to, co kasy zrodel przyjely (0..x; reszta wraca do pana). Wyjatek w srodku - zwraca to, co juz
        /// przeszlo (bez zwrotu panu pieniedzy, ktore dostal wierzyciel).</summary>
        internal static long PayByLord(Settlement dst, long x) { return PayByLordAge(dst, x, OverdueDays); }

        /// <summary>f4 P5 (wczesna splata, klucz Ks Lord Food Bills Early Share): jak PayByLord, ale rachunki starsze niz `age` dni (PayByLord = age Ks Consign Overdue
        /// Days, bit w bit jak dotad). Splata rachunku mlodszego niz zwloka liczy sie tez do DLordEarly (linia KS-H: "w tym przed zwloka").</summary>
        internal static long PayByLordAge(Settlement dst, long x, int age)
        {
            Resolve();
            if (dst == null || x <= 0 || _acc.Count == 0) return 0;
            long paid = 0;
            try
            {
                int today = Today(), od = OverdueDays;
                var l = new List<KeyValuePair<Acct, Bill>>();
                foreach (var a in _acc.Values)
                {
                    if (a.Claim || a.Dst != dst || a.Src == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst)) continue;   // recenzja ceny-r5: bez roszczen z zadatkow
                    foreach (var b in a.B) if (b.Left > 0 && today - b.Day > age) l.Add(new KeyValuePair<Acct, Bill>(a, b));
                }
                if (l.Count == 0) return 0;
                l.Sort((p, q) => p.Value.Day != q.Value.Day ? p.Value.Day.CompareTo(q.Value.Day) : string.CompareOrdinal(p.Key.Src.StringId, q.Key.Src.StringId));
                long left = x; bool early = false;
                foreach (var p in l)
                {
                    if (left <= 0) break;
                    var b = p.Value; var st = p.Key.Src.Town;
                    int ask = (int)Math.Min(int.MaxValue, Math.Min(left, b.Left));
                    if (ask <= 0) continue;
                    int s0 = st.Gold; st.ChangeGold(ask); long rec = Math.Max(0L, (long)st.Gold - s0);   // kasa zrodla bez miejsca (int) - reszta wraca do pana
                    if (rec <= 0) continue;
                    b.Left -= rec; left -= rec; paid += rec;
                    DRepaid += rec; DLordRepaid += rec; GLordPaid += rec; GSrcGot += rec;
                    if (today - b.Day <= od) { DLordEarly += rec; early = true; }   // f4 P5: rachunek jeszcze przed zwloka (tylko przy age < zwloka)
                }
                if (paid > 0) DLordN++;
                if (early) DLordEarlyN++;
                var gone = new List<string>();
                foreach (var kv in _acc) { if (kv.Value.Dst != dst) continue; kv.Value.B.RemoveAll(b => b.Left <= 0); if (kv.Value.B.Count == 0) gone.Add(kv.Key); }
                foreach (var k in gone) _acc.Remove(k);
            }
            catch (Exception e) { Stumble("PayByLord", e); }
            return paid;
        }

        /// <summary>Rachunki otwarte wedlug zrodla (zl) - narazenie zrodla (KsFoodCarts.Daily, miejsce kredytu).</summary>
        internal static Dictionary<Settlement, long> OwedBySource()
        {
            Resolve();
            var d = new Dictionary<Settlement, long>();
            foreach (var a in _acc.Values)
            {
                if (a.Src == null) continue;
                long s = 0; foreach (var b in a.B) s += Math.Max(0, b.Left);
                long v; d.TryGetValue(a.Src, out v); d[a.Src] = v + s;
            }
            return d;
        }

        /// <summary>Cele z rachunkiem starszym niz zwloka (dzis - doba > KsConsignOverdueDays).</summary>
        internal static HashSet<Settlement> OverdueDst(int today)
        {
            Resolve();
            var h = new HashSet<Settlement>(); int od = OverdueDays;
            bool relNow = _relDay == today && _released.Count > 0;      // 322e: dluznik w terminie z dlugiem w horyzoncie raty - dowoz tej doby odblokowany
            foreach (var a in _acc.Values)
            {
                if (a.Dst == null || a.Claim) continue;                 // recenzja ceny-r5: roszczenie z zadatku nie blokuje dowozu do dluznika
                if (relNow && _released.Contains(a.Dst)) continue;
                foreach (var b in a.B) if (b.Left > 0 && today - b.Day > od) { h.Add(a.Dst); break; }
            }
            return h;
        }

        /// <summary>Stan do linii: otwarte (zl), rachunkow, miast celu, starsze niz zwloka (zl i miast).</summary>
        internal static void Census(int today, out long open, out int bills, out int towns, out long overdue, out int overdueTowns, out long ownedMax, out string topDst)
        {
            Resolve();
            open = 0; bills = 0; overdue = 0; ownedMax = 0; topDst = "-";
            var dsts = new Dictionary<Settlement, long>(); var od = new HashSet<Settlement>(); int odd = OverdueDays;
            foreach (var a in _acc.Values)
                foreach (var b in a.B)
                {
                    if (b.Left <= 0) continue;
                    open += b.Left; bills++;
                    long v; dsts.TryGetValue(a.Dst, out v); dsts[a.Dst] = v + b.Left;
                    if (today - b.Day > odd && !a.Claim) { overdue += b.Left; od.Add(a.Dst); }   // recenzja ceny-r5: zalegle = blokujace dowoz (bez roszczen z zadatkow)
                }
            towns = dsts.Count; overdueTowns = od.Count;
            var top = new List<KeyValuePair<Settlement, long>>(dsts);
            top.Sort((x, y) => y.Value.CompareTo(x.Value));
            if (top.Count > 0)
            {
                ownedMax = top[0].Value;
                var sb = new StringBuilder();
                for (int i = 0; i < top.Count && i < 5; i++) sb.Append(i == 0 ? "" : ", ").Append(top[i].Key == null ? "?" : (top[i].Key.Name != null ? top[i].Key.Name.ToString() : top[i].Key.StringId)).Append(' ').Append(top[i].Value);
                topDst = sb.ToString();
            }
        }

        /// <summary>Koniec doby (KsFoodCarts.Flush, co dobe - takze bez linii): kontrola rachunkow otwarte wczoraj + nowe - splacone - przepadle - otwarte dzis
        /// (pierwsza doba po wczytaniu - "-"); otwarte dzis do nastepnej doby.</summary>
        internal static string EndDay(out long openNow)
        {
            Resolve();
            openNow = 0;
            foreach (var a in _acc.Values) foreach (var b in a.B) if (b.Left > 0) openNow += b.Left;
            // KS-R5 ceny-r5: roszczenia z zadatkow (DClaimNew) - nowe rachunki odwrotne; bez zadatkow 0 (bit w bit)
            string s = _openPrev < 0 ? "-" : (_openPrev + DNew + DClaimNew - DRepaid - DForfeit - openNow).ToString(CultureInfo.InvariantCulture);
            _openPrev = openNow;
            return s;
        }

        // ------------------------------------------------------------ zapis (arm_daniny_kupcy)
        internal static string Export()
        {
            Resolve();
            if (_acc.Count == 0) return "";
            string s = Build(false);
            if (s.Length > SaveCapChars)
            {
                // rozmiar ponad 64 KB - wpisy pary scalane w jeden (najstarsza doba, suma); nic nie przepada
                s = Build(true);
                // recenzja r4-kupcy: limit miekki - po scaleniu jeden wpis na pare; wiecej par = wiekszy zapis (roszczenia nie przepadaja), linia mowi to wprost
                bool over = s.Length > SaveCapChars;
                try { Log.Info("KS: zapis arm_daniny_kupcy - wpisy par scalone (rozmiar ponad 64 KB), teraz " + (s.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB" + (over ? " - NADAL PONAD 64 KB (limit miekki: jeden wpis na pare, " + _acc.Count + " par; roszczenia nie przepadaja)" : "") + "."); } catch { }
            }
            try { Log.Info("KS: zapis arm_daniny_kupcy doba " + Today() + " | " + (s.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB (par " + _acc.Count + ")."); } catch { }
            return s;
        }

        private static string Build(bool merge)
        {
            var sb = new StringBuilder(_acc.Count * 40 + 8);
            sb.Append(Ver);
            var keys = new List<string>(_acc.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                var a = _acc[k]; if (a.Src == null || a.Dst == null) continue;
                if (merge)
                {
                    long sum = 0; int day = int.MaxValue;
                    foreach (var b in a.B) { if (b.Left <= 0) continue; sum += b.Left; day = Math.Min(day, b.Day); }
                    if (sum > 0) { sb.Append('|').Append(a.Src.StringId).Append('~').Append(a.Dst.StringId).Append('~').Append(sum).Append('~').Append(day); if (a.Claim) sb.Append("~z"); }
                    continue;
                }
                // recenzja ceny-r5: roszczenie z zadatku - 5. pole "z" (starsza DLL czyta 4 pola i bierze je za zwykly rachunek pary - tak jak przed recenzja)
                foreach (var b in a.B)
                    if (b.Left > 0) { sb.Append('|').Append(a.Src.StringId).Append('~').Append(a.Dst.StringId).Append('~').Append(b.Left).Append('~').Append(b.Day); if (a.Claim) sb.Append("~z"); }
            }
            return sb.ToString();
        }

        internal static void Import(string s) { _acc.Clear(); _openPrev = -1; _pending = string.IsNullOrEmpty(s) ? null : s; }

        /// <summary>Rachunki z zapisu (wczytanie albo pierwsze uzycie): osady wedlug StringId; rekord bez osady - linia (roszczenie odrzucone tylko przy uszkodzonym zapisie).</summary>
        internal static void Resolve()
        {
            var s = _pending; if (string.IsNullOrEmpty(s)) return;
            _pending = null;
            int ok = 0, bad = 0; long sum = 0;
            try
            {
                var om = MBObjectManager.Instance;
                var p = s.Split('|');
                if (p.Length == 0 || p[0] != Ver) { Log.Info("KS: arm_daniny_kupcy - nieznany format (" + (p.Length > 0 ? p[0] : "") + "), rachunki pominiete."); return; }
                for (int i = 1; i < p.Length; i++)
                {
                    try
                    {
                        var q = p[i].Split('~'); long left; int day;
                        if (q.Length < 4 || !long.TryParse(q[2], out left) || !int.TryParse(q[3], out day) || left <= 0) { bad++; continue; }
                        Settlement src = null, dst = null;
                        try { src = om.GetObject<Settlement>(q[0]); dst = om.GetObject<Settlement>(q[1]); } catch { }
                        if (src == null || dst == null) { bad++; continue; }
                        bool claim = q.Length >= 5 && q[4] == "z";     // recenzja ceny-r5: roszczenie z zadatku (brak pola - rachunek za jedzenie, stary zapis)
                        Acct a; string k = claim ? ClaimKey(src, dst) : Key(src, dst);
                        if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = src, Dst = dst, Claim = claim }; _acc[k] = a; }
                        if (a.B.Count >= MaxEntries) a.B[a.B.Count - 1].Left += left; else a.B.Add(new Bill { Left = left, Day = day });
                        ok++; sum += left;
                    }
                    catch (Exception e) { Stumble("Resolve.rec", e); bad++; }
                }
                foreach (var a in _acc.Values) a.B.Sort((x, y) => x.Day.CompareTo(y.Day));
            }
            catch (Exception e) { Stumble("Resolve", e); }
            try { Log.Info("Kupcy zywnosci (KS-H): rachunki kredytu z zapisu " + ok + " (" + sum + " zl; odrzucone bez osady " + bad + ")."); } catch { }
        }
    }
}
