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
        private sealed class Bill { internal long Left; internal int Day; internal int Pcs; }   // 324 runda 3: Pcs - sztuki zboza w komisie (tylko konto Kom)
        private sealed class Acct { internal Settlement Src, Dst; internal bool Claim, Vil, Kom; internal readonly List<Bill> B = new List<Bill>(8); }   // 324 runda 3: Kom - komis zboza wsi na polce miasta   // recenzja ceny-r5: Claim - roszczenie z zadatku (osobne konto pary); 324: Vil - wies wierzycielem (zboze zima 322c)

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
        // 324 runda 2 (klucz Winter Grain Village Credit): RACHUNEK WSI - zboze ze spichlerza wsi na polke miasta (TownGranary 322c), ktorego kasa miasta nie zaplacila
        // od razu: wierzyciel = wies (Settlement wsi, osobne konto Vil, wpis zapisu z 5. polem "w"), dluznik = miasto. Splacany w PayDaily razem z rachunkami miasta
        // (od najstarszego, z nadwyzki i z raty 322e - zloto kasa miasta -> kiesa wsi). Jak Claim: NIE blokuje dowozu KS-H (OverdueDst), nie wchodzi do zaleglych
        // (OverdueOf / PayByLord - pan nie placi), ani do odblokowania 322e (food1). Doba: nowe (DVilNew, w kontroli rachunkow EndDay), splacone (DVilRepaid).
        // Starsza DLL czyta wpis "w" jak rachunek miasta z wierzycielem bez miasta - przepada w PayDaily (bez zlota; licznik przepadkow).
        internal static long DVilNew, DVilRepaid; internal static int DVilNewN;
        private static long _vilRepaidLast; private static int _vilRepaidDay = int.MinValue;   // splacone wsiom w ostatniej PayDaily (linia 322c nastepnej doby)
        // 324 runda 3 (klucz Winter Grain Consign): KOMIS WSI - zboze ze spichlerza wsi na polce miasta, za ktore miasto nie zaplacilo przy dostawie nadwyzka kasy:
        // konto Kom (wierzyciel = wies, dluznik = miasto, wpis zapisu z 5. polem "k" i 6. - sztuki). Wartosc = cena dostawy sztuka po sztuce (cennik 322c), NIE dlug:
        // miasto oddaje wsi cene dostawy kazdej sprzedanej sztuki z utargu (zywa kasa; czego kasa nie ma - rachunek wsi Vil, splacany jak rachunki), sztuki niesprzedane
        // po Ks Consign Rate Release Days wracaja do stodoly wsi (z polki), a czego na polce juz nie ma - rachunek wsi (rozliczenie). Jak Claim i Vil: NIE blokuje dowozu
        // KS-H (OverdueDst), pan nie placi (OverdueOf / PayByLord), nie wchodzi do FoodDebtOf ani narazenia zrodla; PayDaily go nie splaca (splaca go sprzedaz), wojna -
        // przepada jak rachunek wsi. Doba: nowe (DKomNew), splacone z utargu (DKomPaid), na rachunek wsi (DKomBilled), zwrocone do stodol (DKomBack) - w kontroli EndDay.
        // Starsza DLL czyta wpis "k" jak rachunek miasta z wierzycielem bez miasta - przepada w PayDaily (bez zlota).
        internal static long DKomNew, DKomPaid, DKomBilled, DKomBack, DKomSoldVal, DKomSoldBilled; internal static int DKomNewPcs, DKomSoldPcs, DKomBackPcs, DKomBilledPcs;
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

        // 428 A8 (Town Pays Villages First; Jeff 11.11: pierwszenstwo do kasy miasta ma wies, nie pan): w dzien raty firmy DS (DsTowns.Pay, PRZED przelewem panu i koronie)
        // miasto zostawia w kasie ponad podloge to, co jest winne wsiom (HoldForVillages - zloto sie nie rusza); PayDaily tej samej doby wyplaca z tej kwoty NAJPIERW
        // rachunki wsi (od najstarszego), potem jak dotad wszystkie rachunki od najstarszego. Liczniki od linii do linii DsTowns (TakeHold): wstrzymane = splacone wsiom
        // + niewykorzystane (kasa zeszla ponizej podlogi miedzy rata a splata, rachunek przepadl wojna). Sesja - bez zapisu (wstrzymanie zyje jedna dobe).
        private static readonly Dictionary<Settlement, long> _vilHold = new Dictionary<Settlement, long>();
        private static int _vilHoldDay = int.MinValue;
        internal static long HoldSet, HoldPaid, HoldUnused; internal static int HoldTowns;

        /// <summary>428 A8: rachunki wsi otwarte wobec miasta `town` (zl; bez komisu - ten splaca sprzedaz). Nie rzuca.</summary>
        internal static long VillageOpenOf(Settlement town)
        {
            long s = 0;
            try
            {
                Resolve();
                if (town == null || _acc.Count == 0) return 0;
                foreach (var a in _acc.Values) { if (!a.Vil || a.Dst != town) continue; foreach (var b in a.B) if (b.Left > 0) s += b.Left; }
            }
            catch (Exception e) { Stumble("VillageOpenOf", e); return 0; }
            return s;
        }

        /// <summary>428 A8 (DsTowns.Pay w dzien raty): z `avail` (kasa miasta ponad podloge firm) zostaje dla wsi najwyzej tyle, ile miasto jest im winne. Zwraca
        /// wstrzymane (wolajacy zmniejsza o tyle to, co bierze pan i korona). Zloto sie nie rusza. Nie rzuca.</summary>
        internal static long HoldForVillages(Settlement town, long avail)
        {
            try
            {
                if (town == null || town.Town == null || avail <= 0) return 0;
                long h = Math.Min(avail, VillageOpenOf(town));
                if (h <= 0) return 0;
                int today = Today();
                if (_vilHoldDay != today) { foreach (var v in _vilHold.Values) HoldUnused += v; _vilHold.Clear(); _vilHoldDay = today; }
                long had; _vilHold.TryGetValue(town, out had); _vilHold[town] = had + h;
                HoldSet += h; if (had <= 0) HoldTowns++;
                return h;
            }
            catch (Exception e) { Stumble("HoldForVillages", e); return 0; }
        }

        /// <summary>428 A8 (linia DsTowns po SupplyDemand.DailyTrade): liczniki wstrzymania od poprzedniej linii; wstrzymania, ktorych PayDaily nie obsluzyla - niewykorzystane.</summary>
        internal static void TakeHold(out long set, out long paid, out long unused, out int towns)
        {
            try { if (_vilHold.Count > 0 && _payDay >= _vilHoldDay) { foreach (var v in _vilHold.Values) HoldUnused += v; _vilHold.Clear(); } } catch { }
            set = HoldSet; paid = HoldPaid; unused = HoldUnused; towns = HoldTowns;
            HoldSet = HoldPaid = HoldUnused = 0; HoldTowns = 0;
        }

        internal static double RateShare { get { var s = Settings.Current; return s == null ? 0.15 : Math.Max(0.0, Math.Min(0.5, s.KsConsignRateShare)); } }
        internal static int RateReleaseDays { get { var s = Settings.Current; return s == null ? 91 : Math.Max(0, Math.Min(364, s.KsConsignRateReleaseDays)); } }

        internal static void Reset()
        {
            _acc.Clear(); _pending = null; _openPrev = -1; ForfeitTotal = 0; ForfeitTotalN = 0; _stum = 0; _err.Clear();
            _payDay = int.MinValue; _relDay = int.MinValue; _paidToday.Clear(); _paidPrev.Clear(); _released.Clear();
            _lordPaidToday.Clear(); _lordPaidPrev.Clear();
            _vilRepaidLast = 0; _vilRepaidDay = int.MinValue;
            _vilHold.Clear(); _vilHoldDay = int.MinValue; HoldSet = HoldPaid = HoldUnused = 0; HoldTowns = 0;   // 428
            ClearDay();
        }

        internal static void ClearDay()
        {
            DNew = DPaidNow = DBilled = DRepaid = DForfeit = 0; DNewN = DForfeitN = DDeliv = 0; GDstPaid = GSrcGot = 0;
            DLordRepaid = GLordPaid = 0; DLordN = 0;
            DLordEarly = 0; DLordEarlyN = 0;
            DClaimNew = 0; DClaimN = 0;
            DVilNew = DVilRepaid = 0; DVilNewN = 0;
            DKomNew = DKomPaid = DKomBilled = DKomBack = DKomSoldVal = DKomSoldBilled = 0; DKomNewPcs = DKomSoldPcs = DKomBackPcs = DKomBilledPcs = 0;
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
        /// <summary>324 runda 2: konto rachunku wsi (wierzyciel = wies, dluznik = miasto) - osobne od rachunkow miast.</summary>
        private static string VilKey(Settlement vil, Settlement town) { return "w|" + vil.StringId + "|" + town.StringId; }
        /// <summary>324 runda 2: rachunek, ktory blokuje dowoz / placi pan (za jedzenie dowiezione wozem KS-H) - bez roszczen z zadatkow i rachunkow wsi.</summary>
        private static bool Blocking(Acct a) { return !a.Claim && !a.Vil && !a.Kom; }
        /// <summary>324 runda 3: konto komisu wsi (wierzyciel = wies, dluznik = miasto) - osobne od rachunku wsi.</summary>
        private static string KomKey(Settlement vil, Settlement town) { return "k|" + vil.StringId + "|" + town.StringId; }

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

        /// <summary>324 runda 2: przelew cel -> wierzyciel (miasto albo wies - rachunek wsi) z pomiarem obu stron; zwraca to, co zeszlo z kasy celu.</summary>
        private static long TransferTo(Town dst, Settlement src, long x)
        {
            if (src == null) return 0;
            if (src.Town != null) return Transfer(dst, src.Town, x);
            var v = src.Village;
            if (x <= 0 || dst == null || v == null) return 0;
            int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, dst.Gold)));
            if (ask <= 0) return 0;
            int d0 = dst.Gold; dst.ChangeGold(-ask); long got = d0 - dst.Gold;
            if (got <= 0) return 0;
            int s0 = v.Gold; v.ChangeGold((int)got); long rec = v.Gold - s0;
            GDstPaid += got; GSrcGot += rec;
            return got;
        }

        /// <summary>324 runda 2: wierzyciel konta istnieje (miasto; rachunek wsi - wies).</summary>
        private static bool SrcOk(Acct a) { return a.Src != null && (a.Src.Town != null || ((a.Vil || a.Kom) && a.Src.Village != null)); }

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
                        if (!SrcOk(a) || a.Dst == null || a.Dst.Town == null || AtWar(a.Src, a.Dst))
                        {
                            // kupcy stracili - roszczenie znika (bez zlota)
                            foreach (var b in a.B) { DForfeit += b.Left; DForfeitN++; ForfeitTotal += b.Left; ForfeitTotalN++; }
                            a.B.Clear(); gone.Add(kv.Key); continue;
                        }
                        if (a.Kom) continue;   // 324 runda 3: komis splaca sprzedaz (TownGranary 322c), nie PayDaily
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
                        foreach (var p in l) { if (p.Value.Left <= 0) continue; owed0 += p.Value.Left; if (Blocking(p.Key)) food0 += p.Value.Left; }
                        long free = Free(dt), paidS = 0, paidR = 0;
                        // 428 A8: wstrzymane w dzien raty firmy (przed panem i korona) - najpierw rachunki wsi, od najstarszego, z kasy ponad podloge
                        long hold;
                        if (_vilHoldDay == today && _vilHold.TryGetValue(dst, out hold))
                        {
                            _vilHold.Remove(dst);
                            long left = Math.Min(hold, free), used = 0;
                            foreach (var p in l)
                            {
                                if (left <= 0) break;
                                if (!p.Key.Vil) continue;
                                var b = p.Value; if (b.Left <= 0) continue;
                                long got = TransferTo(dt, p.Key.Src, Math.Min(left, b.Left));
                                if (got <= 0) break;
                                b.Left -= got; left -= got; free -= got; used += got; DRepaid += got; paidS += got; DVilRepaid += got;
                            }
                            HoldPaid += used; HoldUnused += Math.Max(0L, hold - used);
                        }
                        foreach (var p in l)
                        {
                            if (free <= 0) break;
                            var b = p.Value; if (b.Left <= 0) continue;
                            long got = TransferTo(dt, p.Key.Src, Math.Min(free, b.Left));
                            if (got <= 0) break;
                            b.Left -= got; free -= got; DRepaid += got; paidS += got;
                            if (p.Key.Vil) DVilRepaid += got;
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
                                    long got = TransferTo(dt, p.Key.Src, Math.Min(due, b.Left));
                                    if (got <= 0) break;
                                    b.Left -= got; due -= got; DRepaid += got; paidR += got; DRateRepaid += got;
                                    if (p.Key.Vil) DVilRepaid += got;
                                }
                                if (paidR > 0) DRateN++;
                            }
                        }
                        long paid = paidS + paidR;
                        if (paid > 0) { long v; _paidToday.TryGetValue(dst, out v); _paidToday[dst] = v + paid; }
                        if (share > 0 && owed0 > 0)
                        {
                            long food1 = 0; int oldest = int.MaxValue;
                            foreach (var p in l) if (Blocking(p.Key) && p.Value.Left > 0) { food1 += p.Value.Left; oldest = Math.Min(oldest, p.Value.Day); }
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
                _vilRepaidLast = DVilRepaid; _vilRepaidDay = today;
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

        /// <summary>324 runda 2 (TownGranary 322c, klucz Winter Grain Village Credit): nowy rachunek wsi `vil` wobec miasta `town` (zboze na polce niezaplacone od razu).
        /// Zloto sie nie rusza. false - rachunek nie powstal (wtedy wolajacy nie oddaje towaru na kredyt). Nie rzuca.</summary>
        internal static bool OpenVillage(Settlement vil, Settlement town, long x, int today)
        {
            try
            {
                Resolve();
                if (x <= 0 || vil == null || town == null || vil.Village == null || town.Town == null || vil.StringId == null || town.StringId == null) return false;
                Acct a; string k = VilKey(vil, town);
                if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = vil, Dst = town, Vil = true }; _acc[k] = a; }
                var last = a.B.Count > 0 ? a.B[a.B.Count - 1] : null;
                if (last != null && (last.Day == today || a.B.Count >= MaxEntries)) last.Left += x;
                else a.B.Add(new Bill { Left = x, Day = today });
                DVilNew += x; DVilNewN++;
                return true;
            }
            catch (Exception e) { Stumble("OpenVillage", e); return false; }
        }

        /// <summary>324 runda 2: dlug miasta `dst` za jedzenie (rachunki KS-H i rachunki wsi, bez roszczen z zadatkow; zl) - pulap kredytu wsi. Nie rzuca.</summary>
        internal static long FoodDebtOf(Settlement dst)
        {
            long s = 0;
            try
            {
                Resolve();
                if (dst == null || _acc.Count == 0) return 0;
                foreach (var a in _acc.Values) { if (a.Claim || a.Kom || a.Dst != dst) continue; foreach (var b in a.B) if (b.Left > 0) s += b.Left; }
            }
            catch (Exception e) { Stumble("FoodDebtOf", e); }
            return s;
        }

        /// <summary>324 runda 2: rata 322e miasta `dst` z biezacego zarobku (ten sam wzor co PayDaily: udzial x (netto okna TownPurse + splaty wczoraj + splaty panow
        /// wczoraj)); false - bez pomiaru zarobku (TownPurse wylaczony, pierwsza doba) albo udzial 0. Nie rzuca.</summary>
        internal static bool RateOf(Settlement dst, out long rate)
        {
            rate = 0;
            try
            {
                double share = RateShare;
                if (dst == null || dst.Town == null || !(share > 0)) return false;
                long net; if (!TownPurse.NetOf(dst.Town, out net)) return false;
                int today = Today(); long prev = 0, lprev = 0;
                // splaty "wczoraj": PayDaily dzis juz byla - _paidPrev; ostatnia byla wczoraj - jeszcze w _paidToday
                if (_payDay == today) { _paidPrev.TryGetValue(dst, out prev); _lordPaidPrev.TryGetValue(dst, out lprev); }
                else if (_payDay == today - 1) { _paidToday.TryGetValue(dst, out prev); _lordPaidToday.TryGetValue(dst, out lprev); }
                long inc = net + prev + lprev;
                rate = inc > 0 ? (long)Math.Ceiling(share * inc) : 0;
                return true;
            }
            catch (Exception e) { Stumble("RateOf", e); rate = 0; return false; }
        }

        /// <summary>324 runda 2 (linia 322c): rachunki wsi otwarte (zl, miast dluznikow) i splacone wsiom w ostatniej PayDaily (doba `paidDay`). Nie rzuca.</summary>
        internal static void VillageStats(out long open, out int towns, out long paidLast, out int paidDay)
        {
            open = 0; towns = 0; paidLast = _vilRepaidLast; paidDay = _vilRepaidDay;
            try
            {
                Resolve();
                var h = new HashSet<Settlement>();
                foreach (var a in _acc.Values) { if (!a.Vil) continue; long s = 0; foreach (var b in a.B) if (b.Left > 0) s += b.Left; if (s > 0) { open += s; h.Add(a.Dst); } }
                towns = h.Count;
            }
            catch (Exception e) { Stumble("VillageStats", e); }
        }

        /// <summary>324 runda 3 (TownGranary 322c, klucz Winter Grain Consign): `pcs` sztuk zboza wsi `vil` na polce miasta `town` w komisie, wartosc `value` zl (cena
        /// dostawy). Zloto sie nie rusza. false - komis nie powstal (wtedy wolajacy nie oddaje tych sztuk). Nie rzuca.</summary>
        internal static bool OpenConsign(Settlement vil, Settlement town, int pcs, long value, int today)
        {
            try
            {
                Resolve();
                if (pcs <= 0 || value <= 0 || vil == null || town == null || vil.Village == null || town.Town == null || vil.StringId == null || town.StringId == null) return false;
                Acct a; string k = KomKey(vil, town);
                if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = vil, Dst = town, Kom = true }; _acc[k] = a; }
                var last = a.B.Count > 0 ? a.B[a.B.Count - 1] : null;
                if (last != null && (last.Day == today || a.B.Count >= MaxEntries)) { last.Left += value; last.Pcs += pcs; }
                else a.B.Add(new Bill { Left = value, Day = today, Pcs = pcs });
                DKomNew += value; DKomNewPcs += pcs;
                return true;
            }
            catch (Exception e) { Stumble("OpenConsign", e); return false; }
        }

        /// <summary>324 runda 3: sztuki w komisie wedlug miasta (dluznika) - konta Kom z sztukami. Nie rzuca.</summary>
        internal static Dictionary<Settlement, int> ConsignByTown()
        {
            var d = new Dictionary<Settlement, int>();
            try
            {
                Resolve();
                foreach (var a in _acc.Values)
                {
                    if (!a.Kom || a.Dst == null) continue;
                    long n = 0; foreach (var b in a.B) if (b.Left > 0 && b.Pcs > 0) n += b.Pcs;
                    if (n > 0) { int v; d.TryGetValue(a.Dst, out v); d[a.Dst] = (int)Math.Min(int.MaxValue, v + n); }
                }
            }
            catch (Exception e) { Stumble("ConsignByTown", e); }
            return d;
        }

        private static void KomLost(long x) { if (x <= 0) return; DForfeit += x; DForfeitN++; ForfeitTotal += x; ForfeitTotalN++; }

        /// <summary>324 runda 3: komis miasta `town` tej doby. (1) `sold` sztuk sprzedanych z polki - od najstarszej partii (doba, potem StringId wsi): miasto oddaje wsi
        /// cene dostawy tych sztuk z zywej kasy (utarg; przelew z pomiarem), czego kasa nie ma - rachunek wsi (OpenVillage, splacany w PayDaily). (2) partie starsze niz
        /// `relDays` dni: `giveBack(wies, sztuki)` zdejmuje z polki i oddaje do stodoly wsi, zwraca oddane sztuki (wartosc ich czesci - DKomBack, bez zlota); reszta partii
        /// (sztuk na polce juz nie ma - sprzedane bez pomiaru) - rachunek wsi (rozliczenie). Zwraca sztuki zdjete z komisu. Wyjatek per partia liczony.</summary>
        internal static int ConsignSettle(Settlement town, int sold, int today, int relDays, Func<Settlement, int, int> giveBack)
        {
            Resolve();
            if (town == null || town.Town == null || _acc.Count == 0) return 0;
            var l = new List<KeyValuePair<Acct, Bill>>();
            foreach (var a in _acc.Values) { if (!a.Kom || a.Dst != town || !SrcOk(a)) continue; foreach (var b in a.B) if (b.Left > 0 && b.Pcs > 0) l.Add(new KeyValuePair<Acct, Bill>(a, b)); }
            if (l.Count == 0) return 0;
            l.Sort((x, y) => x.Value.Day != y.Value.Day ? x.Value.Day.CompareTo(y.Value.Day) : string.CompareOrdinal(x.Key.Src.StringId, y.Key.Src.StringId));
            int outPcs = 0;
            foreach (var p in l)
            {
                try
                {
                    var b = p.Value; var vil = p.Key.Src;
                    // (1) sprzedane - cena dostawy z utargu (zywa kasa miasta -> kiesa wsi), reszta rachunek wsi
                    if (sold > 0)
                    {
                        int q = Math.Min(sold, b.Pcs);
                        long val = q >= b.Pcs ? b.Left : (long)Math.Floor(b.Left * (double)q / b.Pcs);
                        // 325 (recenzja R3 pkt 1): z utargu tylko do nadwyzki kasy ponad zapas kupcow (TownPurse.SpendableTown - jak gotowka dostawy 322c i p6);
                        // zapas kupcow placi zaleglosci firmy DS i raty TownDebt - reszta na rachunek wsi (OpenVillage, splacany w PayDaily)
                        long got = TransferTo(town.Town, vil, Math.Min(val, Math.Max(0L, (long)TownPurse.SpendableTown(town.Town))));
                        long unpaid = val - got;
                        if (unpaid > 0) { if (OpenVillage(vil, town, unpaid, today)) { DKomBilled += unpaid; DKomSoldBilled += unpaid; } else KomLost(unpaid); }
                        b.Left -= val; b.Pcs -= q; sold -= q; outPcs += q;
                        DKomSoldPcs += q; DKomSoldVal += val; DKomPaid += got;
                    }
                    // (2) termin komisu - zwrot z polki do stodoly, reszta rozliczenie (rachunek wsi)
                    if (b.Pcs > 0 && b.Left > 0 && today - b.Day > relDays)
                    {
                        int r = 0;
                        if (giveBack != null) { try { r = Math.Max(0, Math.Min(b.Pcs, giveBack(vil, b.Pcs))); } catch (Exception e) { Stumble("ConsignSettle.back", e); r = 0; } }
                        long valBack = r >= b.Pcs ? b.Left : (long)Math.Floor(b.Left * (double)r / b.Pcs);
                        long rest = b.Left - valBack;
                        DKomBack += valBack; DKomBackPcs += r;
                        if (rest > 0) { if (OpenVillage(vil, town, rest, today)) { DKomBilled += rest; DKomBilledPcs += b.Pcs - r; } else KomLost(rest); }
                        outPcs += b.Pcs;
                        b.Left = 0; b.Pcs = 0;
                    }
                    if (b.Pcs <= 0 && b.Left > 0) { KomLost(b.Left); b.Left = 0; }   // zaokraglenie - nie powinno sie zdarzyc (ostatnia sztuka bierze reszte wartosci)
                }
                catch (Exception e) { Stumble("ConsignSettle", e); }
            }
            var gone = new List<string>();
            foreach (var kv in _acc) { if (!kv.Value.Kom || kv.Value.Dst != town) continue; kv.Value.B.RemoveAll(b => b.Left <= 0 || b.Pcs <= 0); if (kv.Value.B.Count == 0) gone.Add(kv.Key); }
            foreach (var k in gone) _acc.Remove(k);
            return outPcs;
        }

        /// <summary>324 runda 3 (linia 322c): komis otwarty - sztuki, wartosc (zl) i miasta. Nie rzuca.</summary>
        internal static void ConsignStats(out long pcs, out long value, out int towns)
        {
            pcs = 0; value = 0; towns = 0;
            try
            {
                Resolve();
                var h = new HashSet<Settlement>();
                foreach (var a in _acc.Values) { if (!a.Kom) continue; foreach (var b in a.B) if (b.Left > 0) { pcs += b.Pcs; value += b.Left; h.Add(a.Dst); } }
                towns = h.Count;
            }
            catch (Exception e) { Stumble("ConsignStats", e); }
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
                if (!Blocking(a) || a.Dst != dst || a.Src == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst)) continue;   // recenzja ceny-r5: bez roszczen z zadatkow (324: i rachunkow wsi)
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
                if (!Blocking(a) || a.Dst == null || a.Src == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst)) continue;
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
                if (!Blocking(a)) continue;
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
                    if (!Blocking(a) || a.Dst != dst || a.Src == null || a.Src.Town == null || a.Dst.Town == null || AtWar(a.Src, a.Dst)) continue;   // recenzja ceny-r5: bez roszczen z zadatkow (324: i rachunkow wsi)
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
                if (a.Src == null || a.Vil || a.Kom) continue;   // 324: rachunki i komis wsi - nie narazenie miasta zrodla KS-H
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
                if (a.Dst == null || !Blocking(a)) continue;            // recenzja ceny-r5: roszczenie z zadatku (324: i rachunek wsi) nie blokuje dowozu do dluznika
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
                    if (today - b.Day > odd && Blocking(a)) { overdue += b.Left; od.Add(a.Dst); }   // recenzja ceny-r5: zalegle = blokujace dowoz (bez roszczen z zadatkow)
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
            // 324 runda 3: komis - nowe (DKomNew), wyszle z komisu: splacone z utargu, na rachunek wsi (ten trafia tez do DVilNew), zwrocone do stodol
            string s = _openPrev < 0 ? "-" : (_openPrev + DNew + DClaimNew + DVilNew + DKomNew - DRepaid - DForfeit - DKomPaid - DKomBilled - DKomBack - openNow).ToString(CultureInfo.InvariantCulture);
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
                    long sum = 0; int day = int.MaxValue; long pcs = 0;
                    foreach (var b in a.B) { if (b.Left <= 0) continue; sum += b.Left; pcs += b.Pcs; day = Math.Min(day, b.Day); }
                    if (sum > 0) { sb.Append('|').Append(a.Src.StringId).Append('~').Append(a.Dst.StringId).Append('~').Append(sum).Append('~').Append(day); if (a.Claim) sb.Append("~z"); else if (a.Vil) sb.Append("~w"); else if (a.Kom) sb.Append("~k~").Append(Math.Min(int.MaxValue, pcs)); }
                    continue;
                }
                // recenzja ceny-r5: roszczenie z zadatku - 5. pole "z" (starsza DLL czyta 4 pola i bierze je za zwykly rachunek pary - tak jak przed recenzja)
                foreach (var b in a.B)
                    if (b.Left > 0) { sb.Append('|').Append(a.Src.StringId).Append('~').Append(a.Dst.StringId).Append('~').Append(b.Left).Append('~').Append(b.Day); if (a.Claim) sb.Append("~z"); else if (a.Vil) sb.Append("~w"); else if (a.Kom) sb.Append("~k~").Append(b.Pcs); }
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
                        bool vil = q.Length >= 5 && q[4] == "w";       // 324 runda 2: rachunek wsi (wierzyciel - wies)
                        if (vil && src.Village == null) { bad++; continue; }
                        bool kom = q.Length >= 5 && q[4] == "k";       // 324 runda 3: komis wsi (6. pole - sztuki)
                        int pcs = 0;
                        if (kom && (src.Village == null || q.Length < 6 || !int.TryParse(q[5], out pcs) || pcs <= 0)) { bad++; continue; }
                        Acct a; string k = claim ? ClaimKey(src, dst) : vil ? VilKey(src, dst) : kom ? KomKey(src, dst) : Key(src, dst);
                        if (!_acc.TryGetValue(k, out a)) { a = new Acct { Src = src, Dst = dst, Claim = claim, Vil = vil, Kom = kom }; _acc[k] = a; }
                        if (a.B.Count >= MaxEntries) { a.B[a.B.Count - 1].Left += left; a.B[a.B.Count - 1].Pcs += pcs; } else a.B.Add(new Bill { Left = left, Day = day, Pcs = pcs });
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
