using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KS-R3 G1 (17.10, "kupcy i krainy") - KS-H "KUPCY ZYWNOSCI": miasto, ktoremu brakuje jedzenia, kupuje je w miescie tego samego rynku, ktore ma nadwyzke, i wiezie
    /// wozem (doby drogi wozu wsi; na wyspy ladem i morzem przez porty) - jak hurt kupcow TradeCarts (215), ale dla jedzenia. Czynne tylko przy zastosowanej kalibracji
    /// startu (od doby A = D-14 - rurociag napelnia sie w rampie) i kluczu Ks Food Carts; wozy w drodze dojezdzaja zawsze.
    ///  - TOWARY: zboze, ryby, mieso i mieso wieloryba, ser, maslo, jaja, jablka, marchew, pomarancze, winogrona, oliwki, daktyle, miod, sol, przyprawy. Chleba, placka,
    ///    piwa, wina, oliwy i miodu pitnego nie wozi (robi je ZC5 na miejscu), ani zywca, koni i broni. Psujace sie najwyzej 7 dob drogi, reszta 30; bez psucia w drodze.
    ///  - DOBY: droga ladowa (MapDistanceModel) / tempo wozu wsi (MarketCarts.PerDay); para morzem, gdy drogi ladowej nie ma albo jest dluzsza niz 1.15 x droga ladem
    ///    i morzem - wtedy statkiem: droga / (4 x tempo wozu) + 1 doba portu (recenzja rundy 3). Tylko pary tego samego rynku; tabela liczona najwyzej 200 wywolan na dobe
    ///    (w zapisie arm_ks_r3 z ukladem miast, wersja "S" - stara tabela z dobami wozu morzem liczona od nowa); do czasu - prosta x 1.25.
    ///  - DECYZJA (cel co 2. doba wedlug Fnv; czysty rdzen KsFoodCore - ten sam w tescie portu kmoney/port4): zamowienie Dz = max(popyt zaplacony miasta przy lambda 1,
    ///    odbior 7 dob) x kx (kx x1.10 do 2.5 tylko przy prawdziwym braku: pusta polka i polka + w drodze < (T* + doby najblizszego zrodla) x popyt; stoi przy dowozie
    ///    w drodze; inaczej x0.98, od 1); polka celu T* = 7 x cena startu^(-1/0.6) dni; zrodlo sprzedaje najwyzej polowe nadwyzki ponad 1.25 x T* x popyt (bez kx)
    ///    + 5 szt. na dobe (bez oblezonych i wrogich celowi); kandydaci - 12 najblizszych miast rynku (gdy nic - do 4 dalszych); koszt na miejscu c = indeks zrodla
    ///    x (1 + tau x doby), tau 0.025 ladem / 0.0025 morzem; zrodla od najtanszego, dopoki indeks celu >= 1.03 x c, najwyzej 3; ilosc q = min(oferta, (T* + doby + 2)
    ///    x Dz - polka z droga), najmniej max(5, Dz/2); lot tylko z ZYSKIEM kupca (koszt z frachtem <= 97% ceny polki celu, do 3 polowien lotu).
    ///  - ZLOTO (kazda moneta ma platnika i odbiorce): kasa celu -> kasa zrodla (towar, przy wyjezdzie, sztuka po sztuce - cena pierwszej sztuki dla 1/8 lotu, przy 1/8
    ///    do polowy, przy polowie reszta; 1 lot na 64 liczony sztuka po sztuce - blad w linii), fracht = wartosc x tau x doby z kasy celu: woz ladem - kiesy wsi
    ///    miasta zrodla (wedlug ognisk, najwyzej 8 wsi, reszta najwiekszej; raz na zrodlo i dobe), morzem - kiesa wyrobnikow miasta zrodla (zaloga); najwyzej 15%
    ///    kasy celu ponad zapas kupcow na dobe (przy pustej polce zboza i soli - do polowy zapasu, najwyzej 5% zapasu na dobe ponad to; reszta czeka - licznik). Powrot
    ///    30 dob po terminie albo przy utracie celu: zwrot zaplaty z kasy zrodla (najwyzej kasa), frachtu nie; na polke zrodla tylko towar odpowiadajacy zwrotowi, reszta -
    ///    strata wozu (ujscie). Cel oblezony - woz czeka. Psujace sie traca 1.5% sztuk na dobe drogi (ujscie). Wynik kupca lotu przy przyjezdzie w linii.
    ///  - KSIEGI: towar w drodze - posiadacz "kupcy zywnosci w drodze" (GoodsLedger), ramki bez ksiegowania (przewoz); zloto - pozycja MKsH "Przeplywy osad".
    ///  - KONTROLA DOBY: w drodze wczoraj + kupione - dowiezione - zawrocone - odrzucone = w drodze dzis (reszta 0); zloto: - cel + zrodla + furmani + zalogi + zwroty = 0.
    /// KS-R4 r4-kupcy (noc 17/18.10, PLAN-KS-RUNDA4 rozdz. 3 H1-H8, T9/T9b; Jeff 17.10 "dowoz jak w ksiazkach"; klucze w Settings.cs blok "KS-R4 r4-kupcy"):
    ///  - H1 statek wedlug dob (KsShipByDays): morzem, gdy statek szybszy w dobach (droga ladem i morzem / (4 x tempo wozu) + doba portu < doby wozu); tabela "T" liczona
    ///    w pierwszej dobie naraz (straznik 20 ms, potem 200 wywolan na dobe), zmiana klucza - tabela od nowa; Plan czyta ta sama tabele;
    ///  - H5 kredyt kupcow (KsFoodCartsConsign): przy wyjezdzie cel NIE placi (woz na kredyt: zaplata 0, znacznik Cons, w zapisie 10. pole), fracht placi kasa ZRODLA (ladem - wsie zrodla, morzem - zaloga
    ///    zrodla); przy dostawie cel placi wartosc lotu u siebie (H4) z kasy ponad 0.5 x zapas kupcow, reszta - rachunek (KsFoodCredit), splata codziennie od najstarszego;
    ///    zwloka > 30 dni = bez nowych dostaw; wojna = rachunek przepada; zawrocony woz na kredyt - towar wraca na polke zrodla bez zwrotu (nic nie zaplacono), fracht stracony;
    ///    miejsce kredytu zrodla = 0.5 x kasa - (rachunki otwarte + wozy na kredyt w drodze: wartosc zrodla + fracht);
    ///  - H4 jedna cena celu (KsDestValue): wartosc n sztuk u celu (cena polki schodzi z kazda sztuka, najwyzej 3 x cena startu, x 0.97) w decyzji i w rachunku;
    ///  - T9 (bit w bit): filtr typu przedmiotu w spisie polek, popyt i odbior 7 dob wierszem miasta, indeks ceny tylko dla zrodel z oferta i celow, pamiec wojny
    ///    i dob doby; T9b (KsLotPricePow): cena lotu
    ///    schodkami wzoru gry (dwie ceny gry - pierwsza i ostatnia sztuka - sprawdzaja wzor; inny wzor - trzy ceny jak dotad);
    ///  - linia: kredyt kupiecki (otwarte, nowe, zaplacone przy dostawie, splacone, starsze niz zwloka, przepadle), kontrole rachunkow i zlota, zablokowane z braku
    ///    pieniedzy wedlug towaru, glod pierwszy, Polnoc i Mur (battania, nightswatch, freefolk, skagosi).
    ///  - recenzja r4-kupcy: H4 - drugi i trzeci lot doby do (cel, towar) wyceniany za sztukami wczesniejszych lotow doby (port5: dotad 418 z 1021 takich lotow z rachunkiem
    ///    ponizej kosztu + frachtu); "glod pierwszy: celow" = rozne miasta doby (zamowien glodu - pary osobno); linia mowi wprost o zamkach Muru poza KS-H (Eastwatch
    ///    by the Sea, Shadow Tower, Nightfort, The Wall - KS-H wozi tylko do miast, zboze do Muru daje danina r4-mur) i o wozach na kredyt w drodze (przed cofnieciem
    ///    DLL ponizej r4-kupcy: 0 wozow i 0 zl otwartych - starsza DLL dowiozlaby je bez rachunku).
    /// f4 P5 (koncowy przeglad ekonomii 18.10, uwaga 11; klucz Ks Food Carts Return Keeps Goods): zawrocony woz oddaje CALY towar na polke zrodla, a zaplata, ktorej kasa
    /// zrodla nie oddala, to roszczenie celu wobec zrodla (KsFoodCredit.OpenClaim, jak zwrot zadatku); woz bez celu i zrodla - polka najblizszego miasta (UnloadNearest)
    /// w ramce ksiegi. Bez klucza - jak dotad (reszta towaru "zawrocone bez zwrotu zaplaty" / "bez celu" przez NoteKsLoss).
    /// </summary>
    internal static class KsFoodCarts
    {
        // pozycje koszyka ZC4 (KsBasket) przewozone przez KS-H
        internal static readonly int[] Z =
        {
            KsBasket.Grain, KsBasket.Fish, KsBasket.Meat, KsBasket.Whale, KsBasket.Cheese, KsBasket.Butter, KsBasket.Egg, KsBasket.Apple, KsBasket.Carrot, KsBasket.Orange,
            KsBasket.Grape, KsBasket.Olives, KsBasket.Date, KsBasket.Honey, KsBasket.Salt, KsBasket.Spice
        };
        internal const int NZ = 16;
        private static readonly bool[] Per = { false, true, true, true, false, true, true, true, true, true, true, false, true, false, false, false };
        // towar Planu (KsTab) pozycji - do korytarzy (przyprawy - -1)
        private static readonly int[] PlanG = { KsTab.GGrain, KsTab.GFish, KsTab.GMeat, KsTab.GMeat, KsTab.GCheese, KsTab.GButter, KsTab.GEgg, KsTab.GApple, KsTab.GCarrot, KsTab.GOrange,
                                                KsTab.GGrape, KsTab.GOlives, KsTab.GDate, KsTab.GHoney, KsTab.GSalt, -1 };
        // recenzja runda 3 (wazne): fracht ladem 2.5% wartosci na dobe, morzem 0.25% (dotad 1.0% / 0.5% - morze tylko 2 x taniej niz woz); te same stale co krok 5 Planu
        private const double TauLand = KsCore.CorrTauLand, TauSea = KsCore.CorrTauSea, Margin = 0.03, SrcKeep = 1.25, OfferShare = 0.5, KxUp = 1.10, KxDown = 0.98, KxMax = 2.5, CofferShare = 0.15;
        private const int MaxSrc = 3, NCand = 12, Keep = 5, ReturnDays = 30, CallsPerDay = 200;
        // recenzja runda 3 (wazne): kasa celu - zapas kupcow zostaje podloga; przy pustej polce zboza i soli najwyzej do polowy zapasu i najwyzej 5% zapasu na dobe ponad zwykly budzet
        private const double ReserveFloorShort = 0.5, ReserveDayShort = 0.05;
        // recenzja runda 3 (drobne): psucie w drodze - psujace sie traca 1.5% sztuk na dobe drogi (ujscie w ksiedze towarow)
        private const double SpoilPerDay = 0.015;
        private const int SampleCap = 256;                                  // probka wyceny: sztuka po sztuce najwyzej pierwsze 256 sztuk lotu (koszt)
        // KS-R3 (port symulatora 17.10, runda 2): gdy zadne z 12 najblizszych miast nie moze wyslac (bez nadwyzki, poza zasiegiem, wojna, cena) - najwyzej 4 dalsze miasta
        // rynku z nadwyzka w zasiegu (wedlug dob; Qarth, Meereen, Polnoc: nadwyzka zboza Volantis i Reach lezala poza 12 najblizszymi - 191 z 240 prob "bez nadwyzki")
        private const int NCandFar = 4;
        private const double MaxDur = 30.0, MaxPer = 7.0;

        internal static bool SwitchOn { get { var s = Settings.Current; return s == null || s.KsFoodCarts; } }
        // KS-R4 r4-kupcy: klucze (brak ustawien - domyslne TAK)
        private static bool ShipByDaysKey { get { var s = Settings.Current; return s == null || s.KsShipByDays; } }
        private static bool ConsignKey { get { var s = Settings.Current; return s == null || s.KsFoodCartsConsign; } }
        /// <summary>Scalenie r4-glodbunt + r4-kupcy: klucz kredytu kupcow (KsLordFood - tryb RACHUNKI przy TAK, DOPLATA KASY przy NIE - zaplata przy wysylce jak dotad).</summary>
        internal static bool ConsignOn { get { return ConsignKey; } }
        private static bool DestValueKey { get { var s = Settings.Current; return s == null || s.KsDestValue; } }
        private static bool LotPowKey { get { var s = Settings.Current; return s == null || s.KsLotPricePow; } }
        private static bool MinLotKey { get { var s = Settings.Current; return s == null || s.KsCartsMinLotTrueDemand; } }   // f19-e2
        /// <summary>Miejsce kredytu zrodla: najwyzej ten udzial kasy zrodla w towarze w drodze, frachcie i rachunkach (0..1).</summary>
        private static double SrcExposureShare { get { var s = Settings.Current; return s == null ? 0.5 : Math.Max(0.0, Math.Min(1.0, s.KsConsignSourceExposure)); } }
        /// <summary>KS-R5 ceny-r5: zadatek kupcow celu (klucz Ks Consign Deposit, tylko z kredytem kupcow) i jego udzial w wartosci lotu u celu (0..1).</summary>
        internal static bool DepositKey { get { var s = Settings.Current; return s == null || s.KsConsignDeposit; } }
        internal static double DepositShare { get { var s = Settings.Current; return s == null ? 0.5 : Math.Max(0.0, Math.Min(1.0, s.KsConsignDepositShare)); } }
        /// <summary>f4 P5: klucz Ks Food Carts Return Keeps Goods - zawrocony woz oddaje caly towar zrodlu (reszta zaplaty = roszczenie celu), woz bez celu i zrodla - polka
        /// najblizszego miasta (brak ustawien - TAK).</summary>
        private static bool ReturnKeepsGoods { get { var s = Settings.Current; return s == null || s.KsFoodCartsReturnKeepsGoods; } }
        /// <summary>Klucz Ks Ship By Days w tej chwili (Plan - wersja tabeli dob).</summary>
        internal static bool DaysByShipDays { get { return EnsureTowns() && _byDays; } }

        // ------------------------------------------------------------ miasta (Town.AllTowns wedlug StringId) i tabela dob
        private static Town[] _t; private static Settlement[] _st; private static int _nt; private static uint _tHash;
        private static Dictionary<Settlement, int> _ix;
        private static int[] _mk;
        private static float[] _days;                 // [nt * nt] -2 nie liczone, -1 brak drogi albo inny rynek
        private static bool[] _sea;
        private static int[] _pairs;                  // pary tego samego rynku (a * nt + b, a < b) w stalej kolejnosci
        private static int _cursor, _pairsDone;
        private static int[][] _cand;                 // 12 najblizszych miast rynku wedlug dob
        private static int[][] _candFar;              // KS-R3 runda 2: reszta miast rynku wedlug dob (szukane tylko, gdy 12 najblizszych nic nie da)
        private static bool _candDirty = true;
        private static string _pendingDays;
        private static float[] _kx;                   // [nt * NZ] sterowanie brakiem
        private static string _pendingKx;
        private static Dictionary<int, List<KeyValuePair<Village, float>>> _vilOf;   // zrodlo -> wsie (ogniska), najwyzej 8 + reszta najwiekszej
        private static int _vilDay = -1;

        private sealed class Cart { internal Settlement Src, Dst; internal int Z, N, DayBought, DayDue; internal long Paid, Freight; internal bool Sea; internal long Val = -1; internal bool Cons; internal long Dep; }   // r4-kupcy: Cons - woz na kredyt (zaplata 0, rachunek przy dostawie), Val - wartosc zrodla lotu przy wyjezdzie; KS-R5 ceny-r5: Dep - zadatek kupcow celu zaplacony przy wysylce (zl)
        private static readonly List<Cart> _carts = new List<Cart>();
        private static int[] _inc;                    // [nt * NZ] w drodze do celu
        private static string _pending;
        private static long _prevTransit = -1;
        // srednie (korytarze: dowiezione 28 dob wedlug krain; rurociag rampy: wyslane 7 dob i doby wedlug rynku i pozycji)
        private static readonly Dictionary<string, double> _delEma = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> _delDay = new Dictionary<string, double>(StringComparer.Ordinal);
        private static int _delN;
        private static readonly double[] _shipEma = new double[2 * NZ], _daysEma = new double[2 * NZ], _shipDay = new double[2 * NZ], _shipDaysDay = new double[2 * NZ];
        private static int _shipN;

        // liczniki doby (linia)
        private static int _dLots, _dLand, _dSea, _dArrCarts, _dBack, _dWaitSiege, _dStum, _dSample;
        private static long _dBought, _dGold, _dFreight, _dFrV, _dFrP, _dArr, _dBackPcs, _dRefund, _dDrop, _dPoor, _dOrdered, _dLost;
        private static int _dFar, _dRes; private static long _dResGold;   // KS-R3 runda 2: loty z dalszych miast, loty z zapasu kupcow (pusta polka) i ich zloto
        private static long _gDst, _gSrc, _gCarters, _gCrews, _gRefSrc, _gRefDst;
        private static readonly long[] _dByZ = new long[NZ];
        private static double _dSampleErr, _dSampleMax; private static long _dTicks, _dSampleTicks;
        // recenzja runda 3: kupcy - wynik lotow przy przyjezdzie (wartosc u celu - zaplata - fracht), loty bez zysku pominiete, kasy miast z brakiem wobec zapasu, psucie
        private static long _dMarVal, _dMarCost, _dNoProfit, _dSpoil, _dRetLost; private static int _dMarLots, _dMarLoss;
        private static readonly List<double> _dMarPct = new List<double>(), _dCofShort = new List<double>();
        private static long _daysTicks; private static int _daysCalls;
        // KS-R4 r4-kupcy: kredyt (wyslane na kredyt, wartosc zrodla, fracht ze zrodel), zablokowane (zrodlo bez miejsca, cel z zaleglym, z braku pieniedzy wedlug towaru),
        // glod pierwszy, Polnoc i Mur; tabela dob wedlug dob (wersja, przelacznik klucza, pierwsza doba naraz)
        private static long _dConsPcs, _dConsVal, _gSrcFr, _dSrcCap, _dOverdue, _dFamOrd, _dFamGot, _dNorthShip, _dNorthArr; private static int _dConsLots, _dFamReq, _dFamReqK;
        private static readonly long[] _dPoorByZ = new long[NZ];
        private static readonly Dictionary<string, long> _dNorthBy = new Dictionary<string, long>(StringComparer.Ordinal);
        private static bool[] _north;                 // [nt] miasto Polnocy, Muru albo za Murem (kultura battania, nightswatch, freefolk, wildlings, skagosi)
        // recenzja r4-kupcy: zamki Muru (kultura nightswatch: Eastwatch by the Sea, Shadow Tower, Nightfort, The Wall) - KS-H obsluguje tylko miasta (wiersz ZC4: popyt
        // zaplacony, T*, odbior 7 dob; indeksy miast wspolne z Planem i zapisem), zboze do Muru wiezie danina r4-mur do spichlerzy okregow Strazy - linia to mowi wprost
        private static int _wallCastles; private static string _wallCastleNames = "-";
        private static bool _byDays, _daysSlow;       // tabela dob liczona regula H1 (wersja "T"); pierwsza doba naraz przekroczyla 20 ms - dalej 200 wywolan na dobe
        private static bool _consignNow;              // klucz Ks Food Carts Consign w tej dobie (Ship)
        private static string _creditCtl = "-"; private static long _creditOpen;   // kontrola rachunkow doby (KsFoodCredit.EndDay) i otwarte na koniec doby
        // KS-R5 ceny-r5: zadatki kupcow celu (klucz Ks Consign Deposit) - doba: przy wysylce (wozy, szt., zl), odblokowane (szt., ktore bez zadatku nie pojechalyby), przy dostawie
        // zaliczone na rachunek, nadplata zwrocona przez zrodlo, roszczenia celu (zrodlo bez kasy na zwrot), zwrot zadatku zawroconego wozu, bez rozliczenia (woz bez celu
        // i zrodla); zadatki w drodze wczoraj (kontrola: wczoraj + zaplacone - rozliczone = dzis)
        private static long _dDepPcs, _dDepGold, _dDepFreed, _dDepCred, _dDepOver, _dDepClaim, _dDepBack, _dDepLost, _dDepSettled; private static int _dDepLots;
        private static long _dReexp; private static int _dReexpLots;   // KS-R5 (pomiar): wyslane ze zrodla, do ktorego ten sam towar jest w drodze (KS-H) - reeksport
        private static long _dDepShort;                               // KS-R5: zadatek planu rdzenia ponad to, co kasa celu miala ponad podloge przy wysylce (reszte niesie kredyt zrodla)
        // f4 P5 (klucz Ks Food Carts Return Keeps Goods): zawrocone - sztuki bez zwrotu zaplaty oddane na polke zrodla, roszczenia celu wobec zrodla (zl), zaplata bez
        // wierzyciela (zl); wozy bez celu i zrodla - sztuki na polce najblizszego miasta i wozy
        private static long _dRetKept, _dRetClaim, _dRetNoCred, _dNear; private static int _dNearCarts;
        private static long _depPrev = -1;
        private static long _lastDepPaid;                             // recenzja ceny-r5: zadatek zaplacony w ostatnim Ship (KsFoodCore.In.DepPaidLast - miejsce kredytu zrodla)
        // KS-R5 ceny-r5 (pomiar NKS-24b): brak do T* zamowien bez lotu wedlug przyczyny [t * NZ + k) * NMiss + przyczyna], suma od ostatniej linii tygodnia, doby w sumie
        private static double[] _miss; private static int _missDays;
        // recenzja ceny-r5 (pomiar NKS-7g): oferta zrodel na poczatku doby i niesprzedana na koniec [t * NZ + k] - suma od ostatniej linii tygodnia (doby jak _missDays)
        private static double[] _off0, _offLeft;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _ranDay = -1;

        internal static void Reset()
        {
            _t = null; _st = null; _nt = 0; _tHash = 0; _ix = null; _mk = null; _days = null; _sea = null; _pairs = null; _cursor = _pairsDone = 0; _cand = null; _candFar = null; _candDirty = true;
            _pendingDays = null; _kx = null; _pendingKx = null; _vilOf = null; _vilDay = -1;
            _carts.Clear(); _inc = null; _pending = null; _prevTransit = -1;
            _delEma.Clear(); _delDay.Clear(); _delN = 0; Array.Clear(_shipEma, 0, _shipEma.Length); Array.Clear(_daysEma, 0, _daysEma.Length); _shipN = 0; _exportLogged = -1; _daysTicks = 0; _daysCalls = 0;
            Array.Clear(_shipDay, 0, _shipDay.Length); Array.Clear(_shipDaysDay, 0, _shipDaysDay.Length);
            ClearDay(); _err.Clear(); _ranDay = -1;
            _north = null; _byDays = false; _daysSlow = false; _consignNow = false;
            _depPrev = -1; _miss = null; _missDays = 0; _off0 = null; _offLeft = null;
            KsFoodCredit.Reset();
        }

        private static void ClearDay()
        {
            _dLots = _dLand = _dSea = _dArrCarts = _dBack = _dWaitSiege = _dStum = _dSample = 0; _dFar = _dRes = 0; _dResGold = 0;
            _dBought = _dGold = _dFreight = _dFrV = _dFrP = _dArr = _dBackPcs = _dRefund = _dDrop = _dPoor = _dOrdered = _dLost = 0;
            _gDst = _gSrc = _gCarters = _gCrews = _gRefSrc = _gRefDst = 0;
            Array.Clear(_dByZ, 0, NZ); _dSampleErr = _dSampleMax = 0; _dTicks = 0; _dSampleTicks = 0;
            _dMarVal = _dMarCost = _dNoProfit = _dSpoil = _dRetLost = 0; _dMarLots = _dMarLoss = 0; _dMarPct.Clear(); _dCofShort.Clear();
            _dConsPcs = _dConsVal = _gSrcFr = _dSrcCap = _dOverdue = _dFamOrd = _dFamGot = _dNorthShip = _dNorthArr = 0; _dConsLots = _dFamReq = _dFamReqK = 0;
            Array.Clear(_dPoorByZ, 0, NZ); _dNorthBy.Clear();
            _dDepPcs = _dDepGold = _dDepFreed = _dDepCred = _dDepOver = _dDepClaim = _dDepBack = _dDepLost = _dDepSettled = 0; _dDepLots = 0;
            _dReexp = 0; _dReexpLots = 0; _dDepShort = 0;
            _dRetKept = _dRetClaim = _dRetNoCred = _dNear = 0; _dNearCarts = 0;
            KsFoodCredit.ClearDay();
            KsCostKsh.Clear();                                  // ks-koszt: pod-czasy KS-H od nowa (doba bez linii)
        }

        /// <summary>Recenzja runda 3 (drobne): czas tabeli dob (MapDistanceModel) od ostatniego odczytu (ms) i liczba wywolan - linia doby KS, NKS-9b; zeruje.</summary>
        internal static double TakeDaysMs(out int calls)
        {
            calls = _daysCalls; double ms = _daysTicks * 1000.0 / Stopwatch.Frequency;
            _daysTicks = 0; _daysCalls = 0;
            return ms;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStum++;
            try { if (_err.Add(where)) Log.Error("KsFoodCarts." + where, e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static uint Fnv(string s) { uint h = 2166136261; if (s != null) unchecked { foreach (char c in s) h = (h ^ c) * 16777619; } return h; }

        /// <summary>Miasta wedlug StringId (raz na sesje; zmiana listy - tabela dob od nowa).</summary>
        private static bool EnsureTowns()
        {
            if (_t != null) return _nt > 0;
            var l = new List<Town>();
            foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.Settlement != null && t.Settlement.StringId != null) l.Add(t);
            if (l.Count == 0) return false;                       // swiat jeszcze bez miast (wczytywanie) - sprobuje przy nastepnym wywolaniu
            l.Sort((a, b) => string.CompareOrdinal(a.Settlement.StringId, b.Settlement.StringId));
            _nt = l.Count; _t = l.ToArray(); _st = new Settlement[_nt]; _ix = new Dictionary<Settlement, int>(); _mk = new int[_nt];
            uint h = 2166136261;
            for (int i = 0; i < _nt; i++)
            {
                _st[i] = _t[i].Settlement; _ix[_st[i]] = i; _mk[i] = StartCalibration.TownMarketOf(_t[i]);
                unchecked { foreach (char c in _st[i].StringId) h = (h ^ c) * 16777619; h = (h ^ (uint)_mk[i]) * 16777619; }
            }
            _tHash = h;
            // KS-R4 r4-kupcy: miasta Polnocy, Muru i za Murem (kultury ROT: battania = Polnoc, nightswatch, freefolk / wildlings, skagosi) - linia "Polnoc i Mur"
            _north = new bool[_nt];
            for (int i = 0; i < _nt; i++)
            {
                string cu = null; try { cu = _st[i].Culture != null ? _st[i].Culture.StringId : null; } catch { cu = null; }
                _north[i] = cu == "battania" || cu == "nightswatch" || cu == "freefolk" || cu == "wildlings" || cu == "skagosi";
            }
            WallCastles();
            _byDays = ShipByDaysKey; _daysSlow = false;
            _days = new float[_nt * _nt]; _sea = new bool[_nt * _nt];
            var pairs = new List<int>();
            for (int a = 0; a < _nt; a++)
                for (int b = 0; b < _nt; b++)
                {
                    int k = a * _nt + b;
                    _days[k] = a == b || _mk[a] != _mk[b] ? -1f : -2f;
                    if (a < b && _mk[a] == _mk[b]) pairs.Add(k);
                }
            _pairs = pairs.ToArray(); _cursor = 0; _pairsDone = 0;
            _inc = new int[_nt * NZ]; _kx = new float[_nt * NZ]; for (int i = 0; i < _kx.Length; i++) _kx[i] = 1f;
            foreach (var c in _carts) AddInc(c, +1);
            if (_pendingDays != null) { string p = _pendingDays; _pendingDays = null; ImportDaysNow(p); }
            if (_pendingKx != null) { string p = _pendingKx; _pendingKx = null; ImportKxNow(p); }
            _candDirty = true;
            return _nt > 0;
        }

        /// <summary>Recenzja r4-kupcy: zamki Muru (zamek kultury nightswatch) poza KS-H - liczba i nazwy do linii (raz na sesje, z listy miast).</summary>
        private static void WallCastles()
        {
            _wallCastles = 0; _wallCastleNames = "-";
            try
            {
                var names = new List<string>();
                foreach (var t in Town.AllCastles)                 // Town.AllTowns to same miasta (Campaign.AllTowns), zamki - Campaign.AllCastles
                {
                    if (t == null || !t.IsCastle || t.Settlement == null) continue;
                    string cu = null; try { cu = t.Settlement.Culture != null ? t.Settlement.Culture.StringId : null; } catch { cu = null; }
                    if (cu != "nightswatch") continue;
                    names.Add(t.Settlement.Name != null ? t.Settlement.Name.ToString() : t.Settlement.StringId);
                }
                names.Sort(StringComparer.Ordinal);
                _wallCastles = names.Count; if (names.Count > 0) _wallCastleNames = string.Join(", ", names);
            }
            catch (Exception e) { Stumble("WallCastles", e); }
        }

        internal static int TownCount { get { EnsureTowns(); return _nt; } }
        /// <summary>Rynek (0 Westeros, 1 Essos) miasta o indeksie KS-H t albo -1.</summary>
        internal static int MarketOfIndex(int t) { return EnsureTowns() && t >= 0 && t < _nt ? _mk[t] : -1; }
        internal static int IndexOf(Settlement s) { int i; return s != null && EnsureTowns() && _ix.TryGetValue(s, out i) ? i : -1; }
        internal static bool DaysComplete { get { return EnsureTowns() && _pairsDone >= _pairs.Length; } }
        internal static int DaysPairs { get { EnsureTowns(); return _pairs != null ? _pairs.Length : 0; } }
        internal static int DaysPairsDone { get { EnsureTowns(); return _pairsDone; } }

        /// <summary>Doby wozem miedzy miastami (indeksy KS-H): tabela, do czasu - prosta x 1.25 / tempo wozu; -1 - inny rynek albo brak drogi.</summary>
        internal static float Days(int a, int b)
        {
            if (!EnsureTowns() || a < 0 || b < 0 || a >= _nt || b >= _nt || a == b) return -1f;
            float d = _days[a * _nt + b];
            if (d >= -1.5f) return d;
            // nie liczone jeszcze: prosta x 1.25 / tempo wozu
            try
            {
                double per = Math.Max(1.0, MarketCarts.PerDay(Settings.Current));
                float st = _st[a].GetPosition2D.Distance(_st[b].GetPosition2D);
                return (float)Math.Max(1.0, Math.Ceiling(1.25 * st / per));
            }
            catch { return -1f; }
        }

        internal static bool Sea(int a, int b) { return EnsureTowns() && a >= 0 && b >= 0 && a < _nt && b < _nt && _sea[a * _nt + b]; }

        /// <summary>Stan Plan i zastosowany (StartCalibration.Daily): do 200 wywolan MapDistanceModel na dobe - nastepne pary tabeli dob.</summary>
        internal static void DaysTick()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!EnsureTowns()) return;
                // KS-R4 r4-kupcy H1: zmiana klucza Ks Ship By Days - tabela dob od nowa inna regula (stara regula zostaje do przeliczenia pary - Days liczy prosta)
                bool rule = ShipByDaysKey;
                if (rule != _byDays) { ResetDaysTable(); _byDays = rule; _daysSlow = false; }
                if (_pairsDone >= _pairs.Length) return;
                var dm = Campaign.Current != null ? Campaign.Current.Models.MapDistanceModel : null;
                if (dm == null) return;
                double per = Math.Max(1.0, MarketCarts.PerDay(Settings.Current));
                int calls = 0, scan = 0;
                // H1: cala tabela w pierwszej dobie naraz (straznik 20 ms - potem 200 wywolan na dobe, jak dotad)
                bool fast = _byDays && !_daysSlow; long lim20 = Stopwatch.Frequency / 50;
                while ((fast || calls < CallsPerDay) && _pairsDone < _pairs.Length && scan++ < _pairs.Length)
                {
                    if (fast && Stopwatch.GetTimestamp() - t0 > lim20) { _daysSlow = true; break; }
                    int k = _pairs[_cursor % _pairs.Length]; _cursor++;
                    int a = k / _nt, b = k % _nt;
                    if (_days[k] >= -1.5f) { continue; }
                    float dl = -1f, da = -1f;
                    try { dl = dm.GetDistance(_st[a], _st[b], false, false, MobileParty.NavigationType.Default); } catch { dl = -1f; }
                    calls++;
                    bool landOk = dl >= 0f && dl < CartTownExit.BkLimit;
                    try { da = dm.GetDistance(_st[a], _st[b], _st[a].HasPort, _st[b].HasPort, MobileParty.NavigationType.All); } catch { da = -1f; }
                    calls++;
                    bool allOk = da >= 0f && da < CartTownExit.BkLimit;
                    bool sea = !landOk || (allOk && dl > 1.15f * da);
                    if (_byDays && landOk && allOk)
                    {
                        // KS-R4 r4-kupcy H1: statek, gdy szybszy w dobach (dotad: tylko gdy droga ladem dluzsza niz 1.15 x droga z morzem)
                        double daysLand = Math.Max(1.0, Math.Ceiling(dl / per)), daysSea = Math.Max(1.0, Math.Ceiling(da / (per * KsCore.ShipSpeedK))) + KsCore.PortDays;
                        sea = daysSea < daysLand;
                    }
                    float road = sea ? (allOk ? da : -1f) : dl;
                    // recenzja runda 3 (wazne): para morzem - statek (ShipSpeedK x tempo wozu) + doba portu; ladem - tempo wozu wsi
                    float days = road < 0f ? -1f : (sea ? (float)(Math.Max(1.0, Math.Ceiling(road / (per * KsCore.ShipSpeedK))) + KsCore.PortDays) : (float)Math.Max(1.0, Math.Ceiling(road / per)));
                    _days[k] = days; _days[b * _nt + a] = days; _sea[k] = sea && days >= 0f; _sea[b * _nt + a] = _sea[k];
                    _pairsDone++;
                    _candDirty = true;
                }
                _daysCalls += calls;
            }
            catch (Exception e) { Stumble("DaysTick", e); }
            finally { _daysTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>KS-R4 r4-kupcy H1: tabela dob od nowa (zmiana reguly statku) - pary tego samego rynku "nie liczone".</summary>
        private static void ResetDaysTable()
        {
            if (_days == null) return;
            for (int a = 0; a < _nt; a++)
                for (int b = 0; b < _nt; b++) { int k = a * _nt + b; _days[k] = a == b || _mk[a] != _mk[b] ? -1f : -2f; _sea[k] = false; }
            _cursor = 0; _pairsDone = 0; _candDirty = true;
        }

        // tabela dob w zapisie arm_ks (KS-R3 ks4): hash ukladu miast | liczba par | doby*2+morze w 2 znakach base36 dla par po kolei ("--" - nie liczone)
        private const string B36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        // KS-R4 r4-kupcy H1: "T" - statek wedlug dob (klucz Ks Ship By Days), "S" - regula rundy 3; tabela innej wersji z zapisu - liczona od nowa
        private static string DaysVer { get { return _byDays ? "T" : "S"; } }
        internal static string ExportDays()
        {
            if (_t == null) return _pendingDays ?? "";
            var sb = new StringBuilder(_pairs.Length * 2 + 32);
            sb.Append(DaysVer).Append(_tHash.ToString("X8")).Append(':').Append(_pairs.Length).Append(':');
            foreach (int k in _pairs)
            {
                float d = _days[k];
                if (d < -1.5f) { sb.Append("--"); continue; }
                int v = d < 0f ? 1295 : Math.Min(1294, (int)d * 2 + (_sea[k] ? 1 : 0));
                sb.Append(B36[v / 36]).Append(B36[v % 36]);
            }
            return sb.ToString();
        }

        internal static void ImportDays(string s) { _pendingDays = string.IsNullOrEmpty(s) ? null : s; if (_t != null && _pendingDays != null) { string p = _pendingDays; _pendingDays = null; ImportDaysNow(p); } }

        private static void ImportDaysNow(string s)
        {
            try
            {
                // recenzja runda 3: tabela z dobami statku (prefiks "S"); stara tabela (doby wozu morzem) - liczona od nowa
                if (!s.StartsWith(DaysVer, StringComparison.Ordinal)) return;
                var p = s.Substring(DaysVer.Length).Split(':');
                uint h; int n;
                if (p.Length < 3 || !uint.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h) || h != _tHash || !int.TryParse(p[1], out n) || n != _pairs.Length || p[2].Length != 2 * n) return;
                int done = 0;
                for (int i = 0; i < n; i++)
                {
                    string c = p[2].Substring(2 * i, 2);
                    if (c == "--") continue;
                    int v = B36.IndexOf(c[0]) * 36 + B36.IndexOf(c[1]);
                    if (v < 0) continue;
                    int k = _pairs[i], a = k / _nt, b = k % _nt;
                    float d = v >= 1295 ? -1f : v / 2; bool sea = v < 1295 && (v & 1) != 0;
                    _days[k] = d; _days[b * _nt + a] = d; _sea[k] = sea; _sea[b * _nt + a] = sea;
                    done++;
                }
                _pairsDone = done; _candDirty = true;
            }
            catch (Exception e) { Stumble("ImportDays", e); }
        }

        // sterowanie brakiem kx w zapisie: hash ukladu miast:znak na pozycje ((kx - 1) x 20, 0..30)
        internal static string ExportKx()
        {
            if (_t == null || _kx == null) return _pendingKx ?? "";
            var sb = new StringBuilder(_kx.Length + 16);
            sb.Append(_tHash.ToString("X8")).Append(':');
            foreach (var v in _kx) sb.Append(B36[Math.Max(0, Math.Min(30, (int)Math.Round((v - 1f) * 20f)))]);
            return sb.ToString();
        }

        internal static void ImportKx(string s) { _pendingKx = string.IsNullOrEmpty(s) ? null : s; if (_t != null && _pendingKx != null) { string p = _pendingKx; _pendingKx = null; ImportKxNow(p); } }

        private static void ImportKxNow(string s)
        {
            try
            {
                var p = s.Split(':'); uint h;
                if (p.Length < 2 || !uint.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h) || h != _tHash || p[1].Length != _kx.Length) return;
                for (int i = 0; i < _kx.Length; i++) { int v = B36.IndexOf(p[1][i]); if (v >= 0) _kx[i] = 1f + v / 20f; }
            }
            catch (Exception e) { Stumble("ImportKx", e); }
        }

        /// <summary>12 najblizszych miast rynku (wedlug dob) dla kazdego miasta.</summary>
        private static void EnsureCand()
        {
            if (!_candDirty && _cand != null) return;
            _candDirty = false;
            _cand = new int[_nt][]; _candFar = new int[_nt][];
            var l = new List<KeyValuePair<float, int>>(_nt);
            for (int t = 0; t < _nt; t++)
            {
                l.Clear();
                for (int s = 0; s < _nt; s++) { if (s == t || _mk[s] != _mk[t]) continue; float d = Days(s, t); if (d >= 0f) l.Add(new KeyValuePair<float, int>(d, s)); }
                l.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
                var c = new int[Math.Min(NCand, l.Count)];
                for (int i = 0; i < c.Length; i++) c[i] = l[i].Value;
                _cand[t] = c;
                var cf = new int[Math.Max(0, l.Count - c.Length)];
                for (int i = 0; i < cf.Length; i++) cf[i] = l[c.Length + i].Value;
                _candFar[t] = cf;
            }
        }

        // ------------------------------------------------------------ w drodze
        private static void AddInc(Cart c, int sign)
        {
            if (_inc == null || c == null || c.Dst == null) return;
            int t; if (!_ix.TryGetValue(c.Dst, out t)) return;
            int k = t * NZ + c.Z;
            _inc[k] = Math.Max(0, _inc[k] + sign * c.N);
        }

        /// <summary>Sztuki pozycji koszyka z w drodze do miasta (ZC4 / rampa / linia miast).</summary>
        internal static int Incoming(Settlement dst, int z)
        {
            int t = IndexOf(dst); if (t < 0 || _inc == null) return 0;
            int k = Array.IndexOf(Z, z); if (k < 0) return 0;
            return _inc[t * NZ + k];
        }

        internal static long InTransitPieces() { long n = 0; foreach (var c in _carts) n += c.N; return n; }

        /// <summary>GoodsLedger (posiadacz "kupcy zywnosci w drodze") i rampa: kazda sztuka w drodze (przedmiot, ile, rynek celu).</summary>
        internal static void ForEachInTransit(Action<ItemObject, int, int> f)
        {
            foreach (var c in _carts)
            {
                if (c.N <= 0) continue;
                var it = StartCalibration.ZItem(Z[c.Z]); if (it == null) continue;
                int t = IndexOf(c.Dst);
                f(it, c.N, t >= 0 ? _mk[t] : -1);
            }
        }

        /// <summary>Sterowanie brakiem kx miasta (indeks KS-H) i pozycji KS-H k.</summary>
        internal static double Kx(int t, int k) { return _kx != null && t >= 0 && t * NZ + k < _kx.Length ? _kx[t * NZ + k] : 1.0; }

        /// <summary>Rurociag rampy (P5): spodziewany towar w drodze rynku m i pozycji koszyka z = wyslane (srednia 7 dob) x srednie doby.</summary>
        internal static double Pipeline(int m, int z)
        {
            int k = Array.IndexOf(Z, z); if (k < 0 || m < 0 || m > 1) return 0;
            return _shipEma[m * NZ + k] * _daysEma[m * NZ + k];
        }

        /// <summary>Plan (CorrDelivered): dowiezione wozami KS-H wedlug krain (srednia 28 dob, szt./d): "zrodlo|cel|towar KsTab".</summary>
        internal static Dictionary<string, double> DeliveredSnapshot() { return new Dictionary<string, double>(_delEma, StringComparer.Ordinal); }

        // r4-glodbunt (recenzja): odczyty dla KsLordFood (pan miasta placi za dowoz) - nic tu nie zmienia stanu
        /// <summary>Czy doba `today` jest doba przegladu miasta jako celu (ta sama parzystosc Fnv co Daily -> KsFoodCore.In.Review; poza nia cel nie zamawia).</summary>
        internal static bool ReviewDay(Settlement st, int today) { return st != null && st.StringId != null && (Fnv(st.StringId) & 1u) == (uint)(today & 1); }

        /// <summary>Ksiega kupcow KS-H: zaplacone przez kase celu za wozy kupione w dobie `today` (towar + fracht - to, co Ship zdjal z kasy celu), wedlug celu.</summary>
        internal static void PaidAsDstOn(int today, Dictionary<Settlement, long> into)
        {
            into.Clear();
            foreach (var c in _carts)
            {
                if (c == null || c.Dst == null || c.DayBought != today) continue;
                long v; into.TryGetValue(c.Dst, out v);
                // scalenie: woz na kredyt - kasa celu nic nie zaplacila przy wysylce (fracht placi zrodlo); KS-R5 ceny-r5: poza zadatkiem kupcow celu
                if (c.Cons) { if (c.Dep > 0) into[c.Dst] = v + c.Dep; continue; }
                into[c.Dst] = v + Math.Max(0, c.Paid) + Math.Max(0, c.Freight);
            }
        }

        // ------------------------------------------------------------ doba (SupplyDemand.DailyTrade, po TradeCarts.Arrivals; okno MoneyLedger MTrade / MKsH)
        /// <summary>Dostawy, czekanie pod oblezeniem, powroty (30 dob po terminie albo utrata celu). Dziala takze przy wylaczonym kluczu (oplacone wozy dojezdzaja).</summary>
        internal static void Arrivals()
        {
            if (_pending != null) ResolvePending("doba");
            int today = Today();
            if (_ranDay != today) { _ranDay = today; }
            if (_carts.Count == 0) return;
            EnsureTowns();
            long tArr = Stopwatch.GetTimestamp(); KsCostKsh.ArrCarts += _carts.Count;   // ks-koszt: pod-czas przyjazdow (linia kosztu KS-H)
            var done = new List<Cart>();
            foreach (var c in _carts)
            {
                try
                {
                    if (today < c.DayDue) continue;
                    bool srcOk = c.Src != null && c.Src.ItemRoster != null;
                    bool dstOk = c.Dst != null && c.Dst.IsTown && c.Dst.ItemRoster != null;
                    if (dstOk && c.Src != null)
                    {
                        try { if (c.Src.MapFaction != null && c.Dst.MapFaction != null && FactionManager.IsAtWarAgainstFaction(c.Src.MapFaction, c.Dst.MapFaction)) dstOk = false; } catch { }
                    }
                    if (!dstOk && !srcOk && ReturnKeepsGoods && UnloadNearest(c))
                    {
                        // f4 P5 (klucz Ks Food Carts Return Keeps Goods): woz bez celu i zrodla - towar na polke najblizszego miasta w ramce ksiegi (nic w nicosc); zaplata
                        // i zadatek zostaja tam, gdzie sa (w kasie zrodla od wysylki) - jak dotad
                        if (c.Dep > 0) { _dDepLost += c.Dep; _dDepSettled += c.Dep; c.Dep = 0; }
                        AddInc(c, -1); c.N = 0; done.Add(c); continue;
                    }
                    if (!dstOk && !srcOk)
                    {
                        // recenzja runda 3 (drobne): towar wozu bez celu i bez zrodla - ujscie w ksiedze towarow (dotad znikal "bez wyjasnienia")
                        GoodsLedger.NoteKsLoss(StartCalibration.ZItem(Z[c.Z]), c.N, "woz bez celu i zrodla");
                        if (c.Dep > 0) { _dDepLost += c.Dep; _dDepSettled += c.Dep; c.Dep = 0; }   // KS-R5: zadatek bez rozliczenia (zloto zostalo w kasie zrodla przy wysylce)
                        _dLost += c.N; AddInc(c, -1); c.N = 0; done.Add(c); continue;
                    }
                    if (!dstOk || (today > c.DayDue + ReturnDays && srcOk)) { Back(c); done.Add(c); continue; }
                    if (c.Dst.IsUnderSiege) { _dWaitSiege++; continue; }
                    var it = StartCalibration.ZItem(Z[c.Z]);
                    if (it == null) { Back(c); done.Add(c); continue; }
                    AddInc(c, -1);
                    int n = c.N;
                    // recenzja runda 3 (drobne): psucie w drodze - psujace sie traca 1.5% sztuk na dobe drogi (ujscie)
                    if (Per[c.Z] && n > 0)
                    {
                        int dd = Math.Max(1, c.DayDue - c.DayBought);
                        int sp = Math.Min(n, (int)Math.Floor(n * Math.Min(0.5, SpoilPerDay * dd)));
                        if (sp > 0) { GoodsLedger.NoteKsLoss(it, sp, "psucie w drodze"); _dSpoil += sp; n -= sp; }
                    }
                    // KS-R4 r4-kupcy H5: woz na kredyt (Cons, zaplata 0) - rachunek = wartosc lotu u celu (H4: cena polki celu tej chwili, przed polozeniem towaru, schodzi z kazda
                    // sztuka, najwyzej 3 x cena startu, x 0.97; bez klucza Ks Dest Value - cena polki x sztuki x 0.97)
                    bool cons = c.Cons;
                    long bill = 0;
                    if (cons && n > 0)
                    {
                        try { bill = DestValueKey ? DestValueNow(c.Dst.Town, c.Z, it, n) : 0; }
                        catch (Exception be) { Stumble("Arrivals.bill", be); bill = 0; }
                        // bez klucza H4 albo bez ceny wzoru - cena polki celu x sztuki x 0.97 (towar nigdy za darmo)
                        if (bill <= 0) { try { bill = (long)Math.Floor(n * (double)Math.Max(1, DestUnit(c.Dst.Town, c.Z, it)) * (1.0 - Margin)); } catch { bill = n; } }
                    }
                    // recenzja runda 3 (wazne): wynik kupca lotu - wartosc u celu (cena polki celu dzis) wobec zaplaty i frachtu
                    try
                    {
                        long val, cost;
                        if (cons) { val = bill; cost = Math.Max(0, c.Val) + c.Freight; }
                        else { int up = DestUnit(c.Dst.Town, c.Z, it); val = (long)n * Math.Max(0, up); cost = c.Paid + c.Freight; }
                        _dMarVal += val; _dMarCost += cost; _dMarLots++; if (val < cost) _dMarLoss++;
                        if (cost > 0) _dMarPct.Add((val - cost) / (double)cost);
                    }
                    catch { }
                    if (n > 0)
                    {
                        var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Dst);
                        try { c.Dst.ItemRoster.AddToCounts(it, n); }
                        finally { GoodsLedger.End(gf); }
                        StartCalibration.ZRowDirty(c.Dst, it);          // wiersz polki z mapy ZC4 nieaktualny (H2) - zakupy wsi szukaja go same
                    }
                    if (cons && c.Dep > 0)
                    {
                        // KS-R5 ceny-r5: zadatek kupcow celu zaliczony na wartosc lotu u celu; reszta jak bez zadatku (zaplata ponad podloge, rachunek); nadplata (cena celu spadla
                        // ponizej zadatku) - zwrot z kasy zrodla, czego kasa zrodla nie ma - roszczenie celu (rachunek odwrotny); zloto tylko cel <-> zrodlo
                        try
                        {
                            long dep = c.Dep; c.Dep = 0;
                            _dDepCred += Math.Min(dep, Math.Max(0, bill)); _dDepSettled += dep;
                            if (bill > dep) KsFoodCredit.Deliver(c.Src, c.Dst, bill - dep, today);
                            else if (dep > bill) DepositReturn(c, dep - Math.Max(0, bill), today, false);
                        }
                        catch (Exception de) { Stumble("Arrivals.deposit", de); }
                    }
                    else if (cons && bill > 0)
                    {
                        // zaplata przy dostawie z kasy celu ponad 0.5 x zapas kupcow, reszta - rachunek (KsFoodCredit); zloto cel -> zrodlo
                        try { KsFoodCredit.Deliver(c.Src, c.Dst, bill, today); } catch (Exception de) { Stumble("Arrivals.credit", de); }
                    }
                    try
                    {
                        int di = IndexOf(c.Dst);
                        if (n > 0 && _north != null && di >= 0 && di < _north.Length && _north[di])
                        {
                            _dNorthArr += n;
                            string nm = c.Dst.Name != null ? c.Dst.Name.ToString() : c.Dst.StringId;
                            long v0; _dNorthBy.TryGetValue(nm, out v0); _dNorthBy[nm] = v0 + n;
                        }
                    }
                    catch { }
                    _dArr += n; _dArrCarts++;
                    int g = PlanG[c.Z];
                    if (g >= 0)
                    {
                        string key = StartCalibration.TownLandOf(c.Src) + "|" + StartCalibration.TownLandOf(c.Dst) + "|" + g.ToString(CultureInfo.InvariantCulture);
                        double v; _delDay.TryGetValue(key, out v); _delDay[key] = v + n;
                    }
                    c.N = 0;
                    done.Add(c);
                }
                catch (Exception e) { Stumble("Arrivals", e); }
            }
            foreach (var c in done) _carts.Remove(c);
            KsCostKsh.Arr += Stopwatch.GetTimestamp() - tArr;
        }

        /// <summary>Powrot wozu: towar na polke zrodla, zwrot zaplaty z kasy zrodla do kasy celu (najwyzej kasa zrodla), frachtu nie.
        /// KS-R4 r4-kupcy H5: woz na kredyt (zaplata 0) - caly towar wraca na polke zrodla bez zwrotu (nic nie zaplacono), fracht stracony (zostal u furmanow).
        /// f4 P5 (klucz Ks Food Carts Return Keeps Goods): woz zaplacony przy wysylce - caly towar na polke zrodla, niezwrocona zaplata = roszczenie celu wobec zrodla.</summary>
        private static void Back(Cart c)
        {
            AddInc(c, -1);
            var it = StartCalibration.ZItem(Z[c.Z]);
            _dBack++;
            // KS-R5 ceny-r5: woz na kredyt z zadatkiem kupcow celu - zadatek wraca z kasy zrodla do kasy celu (towar wraca na polke zrodla, jak bez zadatku); czego kasa zrodla
            // nie ma - roszczenie celu wobec zrodla (rachunek odwrotny)
            if (c.Cons && c.Dep > 0)
            {
                long dep = c.Dep; c.Dep = 0; _dDepSettled += dep;
                try { DepositReturn(c, dep, Today(), true); } catch (Exception e) { Stumble("Back.deposit", e); }
            }
            // zwrot zaplaty z kasy zrodla (najwyzej kasa zrodla), frachtu nie
            long got = 0;
            if (c.Src != null && c.Src.Town != null && c.Dst != null && c.Dst.Town != null)
            {
                long r = Math.Min(c.Paid, Math.Max(0, c.Src.Town.Gold));
                if (r > 0)
                {
                    int h0 = c.Src.Town.Gold;
                    c.Src.Town.ChangeGold(-(int)r);
                    got = h0 - c.Src.Town.Gold;
                    if (got > 0) { int d0 = c.Dst.Town.Gold; c.Dst.Town.ChangeGold((int)got); _gRefSrc += got; _gRefDst += c.Dst.Town.Gold - d0; _dRefund += got; }
                    else got = 0;
                }
            }
            // recenzja runda 3 (drobne): zrodlo odbiera tylko towar odpowiadajacy oddanej zaplacie (dotad caly towar i czesc zaplaty przy pustej kasie); reszta - strata wozu
            int nBack = c.N;
            if (c.Paid > 0) nBack = (int)Math.Min(c.N, Math.Floor(c.N * (double)got / c.Paid + 1e-9));
            // f4 P5 (koncowy przeglad ekonomii 18.10, uwaga 11; klucz Ks Food Carts Return Keeps Goods): oplacony towar nie znika - CALY ladunek wraca na polke zrodla,
            // a zaplata, ktorej kasa zrodla nie oddala od razu, to roszczenie celu wobec zrodla (KsFoodCredit.OpenClaim, jak zwrot zadatku w DepositReturn: splata
            // w PayDaily z kasy zrodla ponad podloge, przy wojnie czeka do pokoju). Zloto sie nie rusza (zaplata juz jest w kasie zrodla od wysylki), towar w ramce ksiegi.
            int kept = 0; long owed = 0;
            if (ReturnKeepsGoods && it != null && c.Src != null && c.Src.ItemRoster != null && nBack < c.N)
            {
                kept = c.N - nBack; owed = Math.Max(0, c.Paid - got); nBack = c.N;
            }
            if (it != null && c.Src != null && c.Src.ItemRoster != null && nBack > 0)
            {
                var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Src);
                try { c.Src.ItemRoster.AddToCounts(it, nBack); }
                finally { GoodsLedger.End(gf); }
                _dBackPcs += nBack;
            }
            else nBack = 0;
            if (kept > 0 && nBack > 0)
            {
                _dRetKept += kept;
                if (owed > 0)
                {
                    if (c.Dst != null && c.Src != c.Dst) { KsFoodCredit.OpenClaim(c.Dst, c.Src, owed, Today()); _dRetClaim += owed; }
                    else _dRetNoCred += owed;                           // bez wierzyciela (cel nie istnieje) - zaplata zostaje w kasie zrodla razem z towarem
                }
            }
            int lost = c.N - nBack;
            if (lost > 0) { GoodsLedger.NoteKsLoss(it, lost, "zawrocony woz bez zwrotu zaplaty"); _dRetLost += lost; }
            c.N = 0;
        }

        /// <summary>f4 P5 (koncowy przeglad ekonomii 18.10, uwaga 11): woz bez celu i bez zrodla (obie osady nie przyjma towaru) - caly ladunek na polke najblizszego
        /// miasta (wedlug pozycji celu albo zrodla, ktore jeszcze istnieje; oblezone tylko, gdy nie ma innego; bez zadnej osady - miasto z najmniejszym zapasem tego
        /// towaru) w ramce ksiegi towarow (FKsH, jak dostawa). False - brak towaru albo miasta (wolajacy - stara sciezka). Zloto sie nie rusza.</summary>
        private static bool UnloadNearest(Cart c)
        {
            try
            {
                var it = StartCalibration.ZItem(Z[c.Z]);
                if (it == null || c.N <= 0) return false;
                Settlement anchor = c.Dst ?? c.Src;
                Settlement best = null, bestSiege = null; double bd = double.MaxValue, bds = double.MaxValue;
                foreach (var t in Town.AllTowns)
                {
                    var s = t != null ? t.Settlement : null;
                    if (s == null || !t.IsTown || s.ItemRoster == null || s == anchor) continue;
                    double d = anchor != null ? anchor.GetPosition2D.DistanceSquared(s.GetPosition2D) : s.ItemRoster.GetItemNumber(it);
                    if (s.IsUnderSiege) { if (d < bds) { bds = d; bestSiege = s; } continue; }
                    if (d < bd) { bd = d; best = s; }
                }
                var to = best ?? bestSiege;
                if (to == null) return false;
                var gf = GoodsLedger.Begin(GoodsLedger.FKsH, to);
                try { to.ItemRoster.AddToCounts(it, c.N); }
                finally { GoodsLedger.End(gf); }
                _dNear += c.N; _dNearCarts++;                                // towar juz na polce - dalej nic nie cofa tej decyzji
                try { StartCalibration.ZRowDirty(to, it); } catch (Exception e2) { Stumble("UnloadNearest.row", e2); }
                return true;
            }
            catch (Exception e) { Stumble("UnloadNearest", e); return false; }
        }

        /// <summary>KS-R5 ceny-r5: zwrot zadatku (nadplata przy dostawie albo zawrocony woz) z kasy zrodla do kasy celu z pomiarem obu stron (pozycja "zwroty" kontroli zlota);
        /// czego kasa zrodla nie ma - roszczenie celu wobec zrodla (KsFoodCredit.OpenClaim, zloto sie nie rusza); bez osad - bez rozliczenia (licznik). Zwraca oddane.</summary>
        private static long DepositReturn(Cart c, long x, int today, bool back)
        {
            if (x <= 0) return 0;
            long got = 0;
            var st = c.Src != null ? c.Src.Town : null; var dt = c.Dst != null ? c.Dst.Town : null;
            if (st != null && dt != null)
            {
                long r = Math.Min(x, Math.Max(0, (long)st.Gold));
                if (r > 0)
                {
                    int s0 = st.Gold; st.ChangeGold(-(int)Math.Min(int.MaxValue, r)); got = s0 - st.Gold;
                    if (got > 0)
                    {
                        int d0 = dt.Gold; dt.ChangeGold((int)got);
                        _gRefSrc += got; _gRefDst += dt.Gold - d0;
                        if (back) _dRefund += got;
                    }
                    else got = 0;
                }
            }
            if (back) _dDepBack += got; else _dDepOver += got;
            long rest = x - got;
            if (rest > 0)
            {
                if (c.Src != null && c.Dst != null && c.Src != c.Dst) { KsFoodCredit.OpenClaim(c.Dst, c.Src, rest, today); _dDepClaim += rest; }
                else _dDepLost += rest;
            }
            return got;
        }

        /// <summary>Recenzja runda 3: cena sztuki towaru KS-H k na polce miasta celu (cena doby z mapy ZC4, inaczej gra; 0 - brak).</summary>
        private static int DestUnit(Town t, int k, ItemObject it)
        {
            if (t == null || it == null) return 0;
            int p = StartCalibration.ZPrice(t, Z[k]);
            if (p <= 0) { try { p = t.MarketData.GetPrice(it, null, false, null); } catch { p = 0; } }
            return Math.Max(0, p);
        }

        /// <summary>Decyzje doby: zakupy jedzenia dla miast z brakiem (cel co 2. doba). Tylko stan zastosowany od doby A, klucz Ks Food Carts.
        /// Recenzja runda 3: decyzje liczy czysty rdzen KsFoodCore (ten sam co w tescie portu kmoney/port4): kx tylko przy prawdziwym braku, zapas zrodla bez kx, kasa celu z zapasem
        /// kupcow jako podloga (zboze i sol przy pustej polce do polowy zapasu, najwyzej 5% zapasu na dobe ponad to), lot tylko z zyskiem kupca; probka wyceny poza czasem doby.</summary>
        internal static void Daily()
        {
            if (!SwitchOn || !StartCalibration.Applied || StartCalibration.RampDayA < 0 || Today() < StartCalibration.RampDayA) return;
            long t0 = Stopwatch.GetTimestamp(), sample0 = _dSampleTicks;
            var pendFreight = new Dictionary<int, long>();
            try
            {
                if (!EnsureTowns()) return;
                EnsureCand();
                int today = Today(), nt = _nt;
                var items = new ItemObject[NZ]; var cats = new ItemCategory[NZ];
                var ixOf = new Dictionary<ItemObject, int>();
                for (int k = 0; k < NZ; k++) { items[k] = StartCalibration.ZItem(Z[k]); cats[k] = items[k] != null ? items[k].ItemCategory : null; if (items[k] != null && !ixOf.ContainsKey(items[k])) ixOf[items[k]] = k; }
                var a = new KsFoodCore.In
                {
                    NT = nt, NK = NZ, Today = today, Mk = _mk, Siege = new bool[nt], Review = new bool[nt],
                    Sh = new double[nt * NZ], Inc = new double[nt * NZ], Ix = new double[nt * NZ], Dp = new double[nt * NZ], Off = new double[nt * NZ], Ts = new double[2 * NZ],
                    Gold = new double[nt], Res = new double[nt], Kx = _kx, Per = Per, Staple = new bool[NZ], Cand = _cand, CandFar = _candFar
                };
                for (int k = 0; k < NZ; k++) a.Staple[k] = (Z[k] == KsBasket.Grain || Z[k] == KsBasket.Salt) && items[k] != null;
                for (int m = 0; m < 2; m++) for (int k = 0; k < NZ; k++) a.Ts[m * NZ + k] = StartCalibration.ZTstar(m, Z[k]);
                // T9 (bit w bit): spis polek tylko przedmiotow typow towarow KS-H (bron i zbroja bez slownika)
                var types = new HashSet<ItemObject.ItemTypeEnum>();
                for (int k = 0; k < NZ; k++) if (items[k] != null) types.Add(items[k].ItemType);
                var has = new bool[NZ]; for (int k = 0; k < NZ; k++) has[k] = items[k] != null;
                // ks-koszt (klucz Ks Cost Fast): ten sam filtr typu tablica bool, kopia wiersza tylko dla przedmiotow typow KS-H (bit w bit)
                bool[] tmask = null;
                if (StartCalibration.SwCostFast) { int mx = 0; foreach (var ty in types) if ((int)ty > mx) mx = (int)ty; tmask = new bool[mx + 1]; foreach (var ty in types) if ((int)ty >= 0) tmask[(int)ty] = true; }
                for (int i = 0; i < nt; i++)
                {
                    var t = _t[i]; var st = _st[i];
                    a.Siege[i] = st.IsUnderSiege || StartCalibration.LandBackOf(st);   // ks-arm: miasto krainy wroconej (bramka krainy) - ani zrodlo, ani cel KS-H
                    a.Review[i] = (Fnv(st.StringId) & 1u) == (uint)(today & 1);
                    var ro = st.ItemRoster;
                    if (ro != null && tmask != null)
                        for (int r = 0; r < ro.Count; r++)
                        {
                            var eit = ro.GetItemAtIndex(r);
                            if (eit == null) continue;
                            int ty = (int)eit.ItemType; if (ty < 0 || ty >= tmask.Length || !tmask[ty]) continue;
                            var el = ro.GetElementCopyAtIndex(r); int k;
                            if (el.Amount > 0 && el.EquipmentElement.ItemModifier == null && ixOf.TryGetValue(eit, out k)) a.Sh[i * NZ + k] += el.Amount;
                        }
                    else if (ro != null)
                        for (int r = 0; r < ro.Count; r++)
                        {
                            var el = ro.GetElementCopyAtIndex(r); int k;
                            var eit = el.EquipmentElement.Item;
                            if (eit == null || !types.Contains(eit.ItemType)) continue;
                            if (el.Amount > 0 && el.EquipmentElement.ItemModifier == null && ixOf.TryGetValue(eit, out k)) a.Sh[i * NZ + k] += el.Amount;
                        }
                    for (int k = 0; k < NZ; k++)
                    {
                        int o = i * NZ + k;
                        a.Inc[o] = _inc[o];
                        if (items[k] == null) continue;                  // towar spoza swiata gry - bez popytu (zadnych lotow)
                        a.Ix[o] = double.NaN;                            // T9: indeks liczony w KsFoodCore przed pierwszym lotem tylko dla zrodel z oferta i celow (IxAt)
                    }
                    StartCalibration.ZDpOffRow(t, i, Z, has, a.Dp, a.Off, i * NZ);   // T9: popyt i odbior 7 dob calego wiersza (bit w bit z ZDp / ZOff7)
                    long res = 0; try { res = Seal31.TownReserve(t); } catch { res = 0; }
                    a.Gold[i] = t.Gold; a.Res[i] = Math.Max(0, res);
                }
                a.IxAt = (t, k) =>
                {
                    float x = 1f;
                    if (cats[k] != null) { try { x = _t[t].GetItemCategoryPriceIndex(cats[k]); } catch { x = 1f; } }
                    return x > 0f && !float.IsInfinity(x) ? x : 1f;
                };
                // T9 (bit w bit): doby i wojna pary z pamieci doby (tabela dob i wojny nie zmieniaja sie w trakcie decyzji)
                var daysMemo = new float[nt * nt]; for (int i = 0; i < daysMemo.Length; i++) daysMemo[i] = float.NaN;
                var warMemo = new sbyte[nt * nt];
                a.Days = (s, t) => { int o = s * nt + t; float v = daysMemo[o]; if (float.IsNaN(v)) { v = Days(s, t); daysMemo[o] = v; } return v; };
                a.Sea = (s, t) => _sea[s * nt + t];
                a.War = (s, t) =>
                {
                    int o = s * nt + t; sbyte w = warMemo[o];
                    if (w == 0)
                    {
                        bool at; try { at = _st[s].MapFaction != null && _st[t].MapFaction != null && FactionManager.IsAtWarAgainstFaction(_st[s].MapFaction, _st[t].MapFaction); } catch { at = false; }
                        w = at ? (sbyte)1 : (sbyte)2; warMemo[o] = w;
                    }
                    return w == 1;
                };
                bool pow = LotPowKey;
                a.LotPrice = (s, k, n) => LotPrice(_t[s], items[k], n, false, pow);
                a.DestUnit = (t, k) => DestUnit(_t[t], k, items[k]);
                bool kt = KsCostKsh.On;                                   // ks-koszt: probka 1/8 czasu wywolan do linii kosztu KS-H (te same wyniki)
                if (kt)
                {
                    var lp0 = a.LotPrice; var ix0 = a.IxAt;
                    a.LotPrice = (s, k, n) => { if (!KsCostKsh.SampleNow(ref KsCostKsh.LotN)) return lp0(s, k, n); long q0 = Stopwatch.GetTimestamp(); long v = lp0(s, k, n); KsCostKsh.LotT += 8 * (Stopwatch.GetTimestamp() - q0); return v; };
                    if (ix0 != null) a.IxAt = (t, k) => { if (!KsCostKsh.SampleNow(ref KsCostKsh.IxN)) return ix0(t, k); long q0 = Stopwatch.GetTimestamp(); double v = ix0(t, k); KsCostKsh.IxT += 8 * (Stopwatch.GetTimestamp() - q0); return v; };
                }
                // KS-R4 r4-kupcy: H2, H3, kolejka wedlug potrzeby, glod pierwszy, H4 jedna cena celu, H5 kredyt kupcow (miejsce kredytu zrodla, cele z zaleglym)
                var s0 = Settings.Current;
                a.FarAlways = s0 == null || s0.KsFarAlways;
                a.RelKeep = s0 == null || s0.KsRelKeep;
                a.ByNeed = s0 == null || s0.KsCartsBudgetByNeed;
                a.MinLotTrue = MinLotKey;                                // f19-e2 (Ks Carts Min Lot True Demand): najmniejszy lot z prawdziwego popytu (bez kx)
                a.Famine = s0 == null || s0.KsFaminePriority;
                if (a.Famine)
                {
                    a.FamineGood = new bool[NZ];
                    for (int k = 0; k < NZ; k++) a.FamineGood[k] = items[k] != null && (Z[k] == KsBasket.Grain || Z[k] == KsBasket.Fish || Z[k] == KsBasket.Meat || Z[k] == KsBasket.Whale);
                }
                if (DestValueKey)
                {
                    var dv = new double[nt * NZ * 4]; var dvOk = new sbyte[nt * NZ];
                    a.DestValue = (t, k, j0, n) => DestValueDay(t, k, items[k], j0, n, dv, dvOk);
                    if (kt) { var dv0 = a.DestValue; a.DestValue = (t, k, j0, n) => { if (!KsCostKsh.SampleNow(ref KsCostKsh.DvN)) return dv0(t, k, j0, n); long q0 = Stopwatch.GetTimestamp(); long v = dv0(t, k, j0, n); KsCostKsh.DvT += 8 * (Stopwatch.GetTimestamp() - q0); return v; }; }
                }
                _consignNow = ConsignKey;
                a.Consign = _consignNow;
                if (a.Consign)
                {
                    var exp = new double[nt];
                    foreach (var kv in KsFoodCredit.OwedBySource()) { int i; if (kv.Key != null && _ix.TryGetValue(kv.Key, out i)) exp[i] += kv.Value; }
                    foreach (var c in _carts)
                    {
                        if (!c.Cons || c.N <= 0 || c.Src == null) continue;
                        int i; if (!_ix.TryGetValue(c.Src, out i)) continue;
                        long v = c.Val;
                        if (v < 0) { int up = StartCalibration.ZPrice(_t[i], Z[c.Z]); v = (long)c.N * Math.Max(1, up); }
                        // KS-R5 ceny-r5: zadatek kupcow celu juz w kasie zrodla - narazenie zrodla tylko ponad zadatek (bez zadatkow: bit w bit)
                        if (c.Dep > 0) exp[i] += Math.Max(0L, v + Math.Max(0, c.Freight) - c.Dep);
                        else exp[i] += v + Math.Max(0, c.Freight);
                    }
                    double xs = SrcExposureShare;
                    a.SrcRoom = new double[nt];
                    for (int i = 0; i < nt; i++) a.SrcRoom[i] = Math.Max(0.0, xs * a.Gold[i] - exp[i]);
                    a.DstOverdue = new bool[nt];
                    foreach (var od in KsFoodCredit.OverdueDst(today)) { int i; if (od != null && _ix.TryGetValue(od, out i)) a.DstOverdue[i] = true; }
                    // KS-R5 ceny-r5: zadatek kupcow celu (klucz Ks Consign Deposit) - wolna kasa celu ponad podloge kredytu (ta sama, z ktorej cel placi przy dostawie)
                    if (DepositKey && DepositShare > 0)
                    {
                        a.Deposit = true; a.DepShare = DepositShare;
                        a.DstFree = new double[nt];
                        for (int i = 0; i < nt; i++) a.DstFree[i] = KsFoodCredit.FreeAbove((long)a.Gold[i], (long)a.Res[i]);
                        a.ShipDep = (s, t, k, n, cost, fr, sea, L, dep) => Ship(s, t, k, items[k], n, cost, fr, sea, L, today, pendFreight, dep);
                        // recenzja ceny-r5: wolna kasa celu TERAZ (ta sama podloga co Ship - FreeOf) i zadatek naprawde zaplacony w ostatnim Ship
                        a.DstFreeNow = t => KsFoodCredit.FreeOf(_t[t]);
                        a.DepPaidLast = () => _lastDepPaid;
                    }
                }
                // KS-R5 ceny-r5 (pomiar NKS-24b): brak do T* zamowien bez lotu wedlug przyczyny - suma do linii tygodnia (bez zmiany decyzji)
                if (_miss == null || _miss.Length != nt * NZ * KsFoodCore.NMiss) { _miss = new double[nt * NZ * KsFoodCore.NMiss]; _missDays = 0; }
                a.Miss = _miss; _missDays++;
                // recenzja ceny-r5 (pomiar NKS-7g): oferta zrodel i niesprzedana - czy nadwyzka eksporterow nie znajduje kupca (rozdzial), czy to tylko zapas roboczy
                if (_off0 == null || _off0.Length != nt * NZ || _offLeft == null || _offLeft.Length != nt * NZ) { _off0 = new double[nt * NZ]; _offLeft = new double[nt * NZ]; }
                a.Offer0 = _off0; a.OfferLeft = _offLeft;
                a.SampleAt = (s, t, k) => (Fnv(_st[s].StringId + _st[t].StringId) + (uint)today + (uint)k) % 64u == 0u;
                a.Sample = (s, k, n) => { long s0 = Stopwatch.GetTimestamp(); Sample(_t[s], items[k], Math.Min(n, SampleCap), pow); _dSampleTicks += Stopwatch.GetTimestamp() - s0; };
                a.Ship = (s, t, k, n, cost, fr, sea, L) => Ship(s, t, k, items[k], n, cost, fr, sea, L, today, pendFreight);
                if (kt) { var sh0 = a.Ship; a.Ship = (s, t, k, n, cost, fr, sea, L) => { if (!KsCostKsh.SampleNow(ref KsCostKsh.ShipN)) return sh0(s, t, k, n, cost, fr, sea, L); long q0 = Stopwatch.GetTimestamp(); int v = sh0(s, t, k, n, cost, fr, sea, L); KsCostKsh.ShipT += 8 * (Stopwatch.GetTimestamp() - q0); return v; }; }
                // scalenie ks-koszt + ceny-r5: wysylka z zadatkiem (ShipDep) w tym samym pod-czasie "wysylka" (tylko pomiar, te same wyniki)
                if (kt && a.ShipDep != null) { var sd0 = a.ShipDep; a.ShipDep = (s, t, k, n, cost, fr, sea, L, dep) => { if (!KsCostKsh.SampleNow(ref KsCostKsh.ShipN)) return sd0(s, t, k, n, cost, fr, sea, L, dep); long q0 = Stopwatch.GetTimestamp(); int v = sd0(s, t, k, n, cost, fr, sea, L, dep); KsCostKsh.ShipT += 8 * (Stopwatch.GetTimestamp() - q0); return v; }; }
                var stt = new KsFoodCore.Stats();
                long tDec = Stopwatch.GetTimestamp(); KsCostKsh.Setup += tDec - t0;   // ks-koszt: wstep doby (spis polek, popyt, kasy, kredyt)
                KsFoodCore.Decide(a, stt);
                KsCostKsh.Decide += Stopwatch.GetTimestamp() - tDec;
                _dOrdered += stt.Ordered; _dPoor += stt.Poor; _dNoProfit += stt.NoProfit; _dFar += stt.Far; _dRes += stt.ResLots; _dResGold += stt.ResGold;
                _dCofShort.AddRange(stt.CofShort);
                _dSrcCap += stt.SrcCap; _dOverdue += stt.Overdue; _dFamReq += stt.FamineReq; _dFamReqK += stt.FamineReqK; _dFamOrd += stt.FamineOrd; _dFamGot += stt.FamineGot;
                _dDepFreed += stt.DepFreed;   // KS-R5: sztuki, ktore bez zadatku nie pojechalyby (zrodlo bez miejsca) - zaplacone zadatki licza sie przy wysylce (Ship)
                if (stt.PoorByK != null) for (int k = 0; k < NZ && k < stt.PoorByK.Length; k++) _dPoorByZ[k] += stt.PoorByK[k];
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                long tPay = Stopwatch.GetTimestamp();
                try { PayCarters(pendFreight); } catch (Exception e) { Stumble("PayCarters", e); }
                KsCostKsh.Pay += Stopwatch.GetTimestamp() - tPay;
                _dTicks += Stopwatch.GetTimestamp() - t0 - (_dSampleTicks - sample0);   // probka wyceny poza czasem doby (osobny czas w linii)
            }
        }

        /// <summary>Cena lotu n sztuk z polki zrodla (kupno): sztuka po sztuce przy polce zmniejszanej o kazda sztuke - trzy ceny (pierwsza dla 1/8, przy 1/8 do polowy,
        /// przy polowie reszta), exact - kazda sztuka (probka 1/64).</summary>
        private static long LotPrice(Town src, ItemObject it, int n, bool exact, bool pow = false)
        {
            if (n <= 0 || src == null || it == null) return 0;
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            var md = src.MarketData;
            var cat = it.ItemCategory;
            var d = md.GetCategoryData(cat);
            int wv = Math.Max(1, HistoricalPrices.ShelfWorth(it));
            var el = new EquipmentElement(it, null, null, false);
            Func<int, long> P = k => Math.Max(1, model.GetPrice(el, null, null, false, Math.Max(0, d.InStoreValue - k * wv), d.Supply, d.Demand));
            if (exact || n < 8)
            {
                long t = 0;
                for (int k = 0; k < n; k++) t += P(k);
                return t;
            }
            if (pow)
            {
                long v;
                if (LotPriceSteps(model, it, cat, d.InStoreValue, d.Supply, d.Demand, wv, n, P, out v)) return v;
            }
            int m1 = n / 8, m2 = n / 2;
            return m1 * P(0) + (m2 - m1) * P(m1) + (n - m2) * P(m2);
        }

        /// <summary>KS-R4 r4-kupcy T9b (klucz Ks Lot Price Pow): cena lotu schodkami wzoru gry. Cena sztuki k = ceil(wartosc x czynnik bazowy(polka - k x wartosc sztuki)
        /// x (1 + kara handlu)) - czynnik bazowy z modelu gry (GetBasePriceFactor, wzor (D / (0.1 S + 0.04 V + 2))^0.6), kara raz. Dwie ceny gry (pierwsza i ostatnia sztuka)
        /// sprawdzaja wzor; poziomy cen miedzy nimi z odwrotnosci wzoru (jeden Pow na poziom, granica poprawiana wzorem obok). Inny wzor (model moda), ponad 128 poziomow cen
        /// albo niezgodna granica - false (wolajacy liczy trzy ceny, jak dotad).</summary>
        private static bool LotPriceSteps(TaleWorlds.CampaignSystem.ComponentInterfaces.TradeItemPriceFactorModel model, ItemObject it, ItemCategory cat, float ins0, float sup, float dem, int wv, int n, Func<int, long> P, out long sum)
        {
            sum = 0;
            long p0 = P(0), pl = P(n - 1);
            if (pl == p0) { sum = p0 * (long)n; return true; }            // cena stala na calym locie (niemalejaca od p0 do pl)
            if (pl < p0 || pl - p0 > 128) return false;
            float pen = model.GetTradePenalty(it, null, null, false, ins0, sup, dem);
            float val = it.Value;
            Func<int, long> Q = k =>
            {
                float ins = Math.Max(0, ins0 - k * wv);
                float b = model.GetBasePriceFactor(cat, ins, sup, dem, false, it.Value);
                float f = val * (b * (1f + pen));
                return Math.Max(1, (int)Math.Ceiling(f));
            };
            if (Q(0) != p0 || Q(n - 1) != pl) return false;
            return KsFoodCore.LotSteps(p0, pl, n, val * (1.0 + pen), ins0, sup, dem, wv, Q, out sum);
        }

        /// <summary>KS-R4 r4-kupcy H4: wartosc n sztuk u celu po marzy - pamiec doby (cena pierwszej sztuki, polka wzoru, sufit) na (cel, towar), decyzja KsFoodCore;
        /// recenzja: za j0 sztukami lotow wczesniejszych tej doby do (cel, towar) (jak rachunek przy dostawie, gdy wczesniejszy woz lezy juz na polce).</summary>
        private static long DestValueDay(int t, int k, ItemObject it, int j0, int n, double[] dv, sbyte[] ok)
        {
            if (it == null || n <= 0 || t < 0 || t >= _nt) return 0;
            int o = t * NZ + k;
            if (ok[o] == 0)
            {
                double p0, a0, c, cap;
                ok[o] = DestParams(_t[t], t, k, it, out p0, out a0, out c, out cap) ? (sbyte)1 : (sbyte)2;
                dv[4 * o] = p0; dv[4 * o + 1] = a0; dv[4 * o + 2] = c; dv[4 * o + 3] = cap;
            }
            if (ok[o] != 1) return 0;
            return (long)Math.Floor(KsFoodCore.DestIntegralFrom(dv[4 * o], dv[4 * o + 1], dv[4 * o + 2], dv[4 * o + 3], j0, n) * (1.0 - Margin));
        }

        /// <summary>KS-R4 r4-kupcy H4: wartosc n sztuk u celu po marzy przy cenie tej chwili (rachunek przy dostawie - przed polozeniem towaru na polce).</summary>
        private static long DestValueNow(Town tw, int k, ItemObject it, int n)
        {
            int t = tw != null ? IndexOf(tw.Settlement) : -1;
            double p0, a0, c, cap;
            if (n <= 0 || !DestParams(tw, t, k, it, out p0, out a0, out c, out cap)) return 0;
            return (long)Math.Floor(KsFoodCore.DestIntegral(p0, a0, c, cap, n) * (1.0 - Margin));
        }

        /// <summary>H4: cena pierwszej sztuki na polce celu (gra, bez sufitu pustej polki ZC4), A0 = 0.1 x podaz + 0.04 x wartosc polki + 2, c = 0.04 x wartosc sztuki,
        /// sufit sztuki 3 x cena startu rynku celu x wartosc (F5: pusta polka nie blokuje lotu glodu).</summary>
        private static bool DestParams(Town tw, int t, int k, ItemObject it, out double p0, out double a0, out double c, out double cap)
        {
            p0 = a0 = c = cap = 0;
            if (tw == null || it == null || it.ItemCategory == null) return false;
            try
            {
                var md = tw.MarketData;
                p0 = Math.Max(1, md.GetPrice(it, null, false, null));
                var d = md.GetCategoryData(it.ItemCategory);
                a0 = 0.1 * d.Supply + 0.04 * Math.Max(0f, d.InStoreValue) + 2.0;
                c = 0.04 * Math.Max(1, HistoricalPrices.ShelfWorth(it));
                int m = t >= 0 && t < _nt ? _mk[t] : StartCalibration.TownMarketOf(tw);
                cap = 3.0 * StartCalibration.ZStartIndex(m, Z[k]) * Math.Max(1, it.Value);
                return true;
            }
            catch { return false; }
        }

        private static void Sample(Town src, ItemObject it, int n, bool pow = false)
        {
            try
            {
                long approx = LotPrice(src, it, n, false, pow);   // KS-R4 r4-kupcy: probka sprawdza metode w uzyciu (T9b schodki albo trzy ceny)
                long ex = LotPrice(src, it, n, true);
                if (ex <= 0) return;
                double e = Math.Abs(approx - ex) / (double)ex;
                _dSample++; _dSampleErr += e; if (e > _dSampleMax) _dSampleMax = e;
            }
            catch (Exception e) { Stumble("Sample", e); }
        }

        /// <summary>Zakup i wyjazd: towar z polki zrodla (ramka bez ksiegowania - towar idzie do posiadacza "w drodze"), kasa celu -> kasa zrodla (towar), fracht z kasy celu
        /// (ladem - do wsi zrodla na koniec doby; morzem - kiesa wyrobnikow zrodla od razu). Zwraca wyslane sztuki.</summary>
        private static int Ship(int s, int t, int k, ItemObject it, int n, long cost, long fr, bool sea, float L, int today, Dictionary<int, long> pendFreight, long dep = 0)
        {
            var src = _st[s]; var dstS = _st[t]; var srcT = _t[s]; var dstT = _t[t];
            var ro = src.ItemRoster;
            bool consign = _consignNow;                          // KS-R4 r4-kupcy H5: woz na kredyt - cel nie placi przy wyjezdzie, fracht placi zrodlo
            if (!consign) dep = 0;                               // KS-R5: zadatek tylko przy kredycie kupcow
            _lastDepPaid = 0;                                    // recenzja ceny-r5: zadatek tego wywolania (0 przy kazdym wczesnym wyjsciu)
            if (ro == null || (consign ? srcT.Gold < fr : dstT.Gold < cost + fr)) return 0;
            // recenzja runda 3 (drobne): sztuki bez modyfikatora (jak polka w Daily), cena i fracht za sztuki naprawde zdjete z polki
            var el = new EquipmentElement(it, null, null, false);
            int ei = ro.FindIndexOfElement(el), before = ei >= 0 ? ro.GetElementNumber(ei) : 0;
            int take = Math.Min(n, Math.Max(0, before - Keep));
            if (take <= 0) return 0;
            if (take < n) { cost = (long)Math.Round(cost * (double)take / n); fr = (long)Math.Round(fr * (double)take / n); dep = (long)Math.Round(dep * (double)take / n); n = take; }
            var gf = GoodsLedger.Begin(GoodsLedger.FKsH, src);
            try { ro.AddToCounts(el, -n); }
            finally { GoodsLedger.End(gf); }
            int ea = ro.FindIndexOfElement(el), after = ea >= 0 ? ro.GetElementNumber(ea) : 0;
            int removed = before - after;
            if (removed != n)
            {
                _dDrop += Math.Max(0, n - removed);                  // zamowione, a nie zdjete z polki (nie weszlo do drogi - poza kontrola towaru)
                if (removed <= 0) return 0;
                cost = (long)Math.Round(cost * (double)removed / n); fr = (long)Math.Round(fr * (double)removed / n); dep = (long)Math.Round(dep * (double)removed / n); n = removed;
            }
            long paidG = 0, frPaid = 0;
            long depPaid = 0;
            if (consign && dep > 0)
            {
                // KS-R5 ceny-r5: zadatek kupcow celu przy wysylce - kasa celu (ponad podloge kredytu, ta sama co przy dostawie) -> kasa zrodla, z pomiarem obu stron
                // (w kontroli zlota: cel -, zrodla +); kasa celu ponizej podlogi - zadatek mniejszy (reszte niesie kredyt zrodla, jak bez zadatku)
                long ask = Math.Min(dep, KsFoodCredit.FreeOf(dstT));
                if (ask > 0)
                {
                    int d0 = dstT.Gold; dstT.ChangeGold(-(int)Math.Min(int.MaxValue, ask)); depPaid = d0 - dstT.Gold;
                    if (depPaid > 0) { int s0 = srcT.Gold; srcT.ChangeGold((int)depPaid); long gotS = srcT.Gold - s0; _gDst += depPaid; _gSrc += gotS; }
                    else depPaid = 0;
                }
                if (depPaid < dep) _dDepShort += dep - depPaid;
                _lastDepPaid = depPaid;
            }
            if (!consign)
            {
            // zloto: towar
            int d0 = dstT.Gold; dstT.ChangeGold(-(int)cost); paidG = d0 - dstT.Gold;
            int s0 = srcT.Gold; srcT.ChangeGold((int)paidG); long gotS = srcT.Gold - s0;
            _gDst += paidG; _gSrc += gotS;
            }
            if (consign && fr > 0)
            {
                // H5: fracht z kasy zrodla - ladem do wsi zrodla na koniec doby (PayCarters), morzem zaloga zrodla (kiesa wyrobnikow); bez kiesy ludu zaloga to kupcy zrodla
                // (koszt wewnetrzny zrodla - zloto sie nie rusza, fracht 0)
                if (sea)
                {
                    int f0 = srcT.Gold;
                    long crew = TownFolk.KsCrewPay(srcT, src, fr);
                    if (crew > 0) { frPaid = f0 - srcT.Gold; _gCrews += crew; _gSrcFr += frPaid; _dFrP += frPaid; }
                }
                else
                {
                    int f0 = srcT.Gold; srcT.ChangeGold(-(int)fr); frPaid = f0 - srcT.Gold;
                    _gSrcFr += frPaid; _dFrV += frPaid;
                    long pv; pendFreight.TryGetValue(s, out pv); pendFreight[s] = pv + frPaid;
                }
            }
            else if (fr > 0)
            {
                if (sea)
                {
                    int f0 = dstT.Gold;
                    long crew = TownFolk.KsCrewPay(dstT, src, fr);
                    if (crew > 0) { frPaid = f0 - dstT.Gold; _gCrews += crew; }
                    else
                    {
                        // kiesa ludu nieczynna - zaloga to kupcy portu zrodla (kasa zrodla)
                        f0 = dstT.Gold; dstT.ChangeGold(-(int)fr); frPaid = f0 - dstT.Gold;
                        int c0 = srcT.Gold; srcT.ChangeGold((int)frPaid); _gCrews += srcT.Gold - c0;
                    }
                    _gDst += frPaid; _dFrP += frPaid;
                }
                else
                {
                    int f0 = dstT.Gold; dstT.ChangeGold(-(int)fr); frPaid = f0 - dstT.Gold;
                    _gDst += frPaid; _dFrV += frPaid;
                    long pv; pendFreight.TryGetValue(s, out pv); pendFreight[s] = pv + frPaid;
                }
            }
            var c = new Cart { Src = src, Dst = dstS, Z = k, N = n, DayBought = today, DayDue = today + (int)Math.Max(1f, L) + SeaStorms.CartDays(SeaStorms.CKs, sea, null, null), Paid = paidG, Freight = frPaid, Sea = sea, Val = consign ? cost : paidG, Cons = consign, Dep = depPaid };
            _carts.Add(c);
            AddInc(c, +1);
            _dLots++; if (sea) _dSea++; else _dLand++;
            _dBought += n; _dGold += paidG; _dFreight += frPaid; _dByZ[k] += n;
            if (consign) { _dConsLots++; _dConsPcs += n; _dConsVal += cost; }
            if (depPaid > 0) { _dDepLots++; _dDepPcs += n; _dDepGold += depPaid; }
            if (_inc != null && s * NZ + k < _inc.Length && _inc[s * NZ + k] > 0) { _dReexp += n; _dReexpLots++; }   // KS-R5 (pomiar): zrodlo samo czeka na ten towar
            if (_north != null && t < _north.Length && _north[t]) _dNorthShip += n;
            int m = _mk[t];
            _shipDay[m * NZ + k] += n; _shipDaysDay[m * NZ + k] += n * Math.Max(1f, L);
            return n;
        }

        /// <summary>Fracht wozow ladem do kies wsi miasta zrodla (wedlug ognisk, najwyzej 8 wsi, reszta najwiekszej) - raz na zrodlo i dobe; bez wsi - kasa zrodla.</summary>
        private static void PayCarters(Dictionary<int, long> pend)
        {
            if (pend.Count == 0) return;
            int today = Today();
            if (_vilOf == null || _vilDay < 0 || today - _vilDay >= 7) BuildVillages(today);
            foreach (var kv in pend)
            {
                long x = kv.Value; if (x <= 0) continue;
                List<KeyValuePair<Village, float>> vl;
                if (_vilOf == null || !_vilOf.TryGetValue(kv.Key, out vl) || vl == null || vl.Count == 0)
                {
                    var tt = _t[kv.Key]; int g0 = tt.Gold; tt.ChangeGold((int)x); _gCarters += tt.Gold - g0;
                    continue;
                }
                double tot = 0; foreach (var p in vl) tot += p.Value;
                long left = x;
                for (int i = 1; i < vl.Count; i++)
                {
                    long y = tot > 0 ? (long)Math.Floor(x * vl[i].Value / tot) : 0;
                    if (y <= 0) continue;
                    int g0 = vl[i].Key.Gold; vl[i].Key.ChangeGold((int)y); long got = vl[i].Key.Gold - g0;
                    _gCarters += got; left -= got;
                    StartCalibration.KsVilFreight(vl[i].Key, got);
                }
                if (left > 0)
                {
                    var v0 = vl[0].Key; int g0 = v0.Gold; v0.ChangeGold((int)left); long got = v0.Gold - g0;
                    _gCarters += got; StartCalibration.KsVilFreight(v0, got);
                }
            }
        }

        /// <summary>Wsie miast zrodel (TradeBound = miasto): najwieksza pierwsza; 8 najwiekszych wedlug ognisk, ich ogniska + reszta do najwiekszej (wagi).</summary>
        private static void BuildVillages(int today)
        {
            _vilDay = today;
            var all = new Dictionary<int, List<KeyValuePair<Village, float>>>();
            foreach (var v in Village.All)
            {
                if (v == null || v.Settlement == null || v.TradeBound == null) continue;
                int t; if (!_ix.TryGetValue(v.TradeBound, out t)) continue;
                List<KeyValuePair<Village, float>> l; if (!all.TryGetValue(t, out l)) { l = new List<KeyValuePair<Village, float>>(); all[t] = l; }
                l.Add(new KeyValuePair<Village, float>(v, Math.Max(0f, v.Hearth)));
            }
            _vilOf = new Dictionary<int, List<KeyValuePair<Village, float>>>();
            foreach (var kv in all)
            {
                var l = kv.Value;
                l.Sort((a, b) => b.Value.CompareTo(a.Value));
                float rest = 0; for (int i = 8; i < l.Count; i++) rest += l[i].Value;
                var top = new List<KeyValuePair<Village, float>>();
                for (int i = 0; i < l.Count && i < 8; i++) top.Add(new KeyValuePair<Village, float>(l[i].Key, l[i].Value + (i == 0 ? rest : 0f)));
                _vilOf[kv.Key] = top;
            }
        }

        /// <summary>Koniec SupplyDemand.DailyTrade: linia doby (od A albo z wozami w drodze), kontrola towaru i zlota, srednie korytarzy i rurociagu.</summary>
        internal static void Flush()
        {
            long now = InTransitPieces();
            // KS-R4 r4-kupcy H6: kontrola rachunkow co dobe (takze w dobie bez linii - ciche logi), wynik do linii
            try { _creditCtl = KsFoodCredit.EndDay(out _creditOpen); } catch (Exception e) { Stumble("CreditEndDay", e); _creditCtl = "-"; }
            try
            {
                // srednie: dowiezione wedlug krain (28 dob), wyslane i doby wedlug rynku i pozycji (7 dob)
                _delN++;
                double w = 1.0 / Math.Min(28, _delN);
                var keys = new List<string>(_delEma.Keys);
                foreach (var k in keys) { double v; _delDay.TryGetValue(k, out v); _delEma[k] += (v - _delEma[k]) * w; if (_delEma[k] < 0.01 && v <= 0) _delEma.Remove(k); }
                foreach (var kv in _delDay) if (!_delEma.ContainsKey(kv.Key)) _delEma[kv.Key] = kv.Value * w;
                _delDay.Clear();
                _shipN++;
                double w7 = 1.0 / Math.Min(7, _shipN);
                for (int i = 0; i < _shipEma.Length; i++)
                {
                    _shipEma[i] += (_shipDay[i] - _shipEma[i]) * w7;
                    if (_shipDay[i] > 0) _daysEma[i] += (_shipDaysDay[i] / _shipDay[i] - _daysEma[i]) * (_daysEma[i] <= 0 ? 1.0 : w7);
                }
                Array.Clear(_shipDay, 0, _shipDay.Length); Array.Clear(_shipDaysDay, 0, _shipDaysDay.Length);
                bool active = StartCalibration.Applied && SwitchOn && StartCalibration.RampDayA >= 0 && Today() >= StartCalibration.RampDayA;
                // KS-R4 r4-kupcy: splaty i przepadki rachunkow tez daja linie (dlugi splacane takze poza stanem zastosowanym)
                bool credit = KsFoodCredit.Any || KsFoodCredit.DRepaid + KsFoodCredit.DForfeit + KsFoodCredit.DBilled != 0;
                // KS-R5 ceny-r5: zadatki w drodze (kontrola zadatkow co dobe, takze bez linii) i tydzien pomiaru przyczyn bez dowozu (zerowany co 7 dob, takze bez linii)
                long depNow = 0; int depCarts = 0; foreach (var c in _carts) if (c.Dep > 0) { depNow += c.Dep; depCarts++; }
                string depCtl = _depPrev < 0 ? "-" : (_depPrev + _dDepGold - _dDepSettled - depNow).ToString(CultureInfo.InvariantCulture);
                _depPrev = depNow;
                bool week = Today() % 7 == 0;
                if (!active && _dBought + _dArr + _dBackPcs + _dLost + _dNear == 0 && now == 0 && !credit) { _prevTransit = now; if (week) ClearMiss(); ClearDay(); return; }
                if (!LogGate.Lines) { _prevTransit = now; if (week) ClearMiss(); ClearDay(); return; }
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(1500);
                sb.Append("Kupcy zywnosci (KS-H) dzien ").Append(Today() - 1).Append(" | ").Append(active ? "CZYNNE" : (SwitchOn ? "poza stanem zastosowanym (tylko dowoz oplaconych)" : "WYLACZONE kluczem Ks Food Carts (tylko dowoz oplaconych)"))
                  .Append(" | wozow w ").Append(_dLots).Append(" (lad ").Append(_dLand).Append(", morze ").Append(_dSea).Append("), kupione ").Append(_dBought).Append(" szt. za ").Append(_dGold).Append(" zl (");
                bool f = true;
                for (int k = 0; k < NZ; k++) { if (_dByZ[k] <= 0) continue; sb.Append(f ? "" : ", ").Append(KsBasket.Ids[Z[k]]).Append(' ').Append(_dByZ[k]); f = false; }
                if (f) sb.Append('-');
                sb.Append("), fracht ").Append(_dFreight).Append(" zl (wsie ").Append(_dFrV).Append(", porty ").Append(_dFrP).Append("), dowiezione ").Append(_dArr).Append(", zawrocone ").Append(_dBackPcs)
                  .Append(" (zwrot ").Append(_dRefund).Append(" zl), czeka pod oblezeniem ").Append(_dWaitSiege).Append(", kasa celu za mala ").Append(_dPoor).Append(" szt. (zamowione ").Append(_dOrdered).Append(')')
                  .Append(" | w drodze ").Append(now).Append(" szt. w ").Append(_carts.Count).Append(" wozach");
                if (_prevTransit >= 0)
                {
                    // recenzja runda 3 (drobne): bez odrzuconych przy zaladunku (nie weszly do drogi); psucie i strata zawroconych - ujscia ksiegi towarow
                    long rest = _prevTransit + _dBought - _dArr - _dBackPcs - _dLost - _dSpoil - _dRetLost - _dNear - now;   // f4 P5: _dNear - wozy bez celu i zrodla na polke najblizszego miasta (bez klucza 0)
                    // KS-R4 r4-kupcy H6: zloto z kredytem - fracht ze zrodel (furmani i zalogi), cel placi zrodlu przy dostawie i w splatach
                    long gold = -_gDst + _gSrc + _gCarters + _gCrews + (_gRefDst - _gRefSrc) - _gSrcFr - KsFoodCredit.GDstPaid - KsFoodCredit.GLordPaid + KsFoodCredit.GSrcGot;   // scalenie r4-glodbunt: panowie splacaja zalegle rachunki
                    sb.Append(" | kontrola towaru reszta ").Append(rest).Append(" (bez celu ").Append(_dLost).Append(", psucie w drodze ").Append(_dSpoil).Append(", zawrocone bez zwrotu zaplaty ").Append(_dRetLost).Append(ReturnKeepsGoods ? ", na polke najblizszego miasta " + _dNear.ToString(inv) : "")
                      .Append("; odrzucone przy zaladunku ").Append(_dDrop).Append(" - nie weszly do drogi), zlota reszta ").Append(gold)
                      .Append(" (cel -").Append(_gDst).Append(", zrodla +").Append(_gSrc).Append(", furmani +").Append(_gCarters).Append(", zalogi +").Append(_gCrews).Append(", zwroty ").Append(_gRefDst).Append('/').Append(_gRefSrc)
                      .Append(", fracht ze zrodel -").Append(_gSrcFr).Append(", rachunki cel -").Append(KsFoodCredit.GDstPaid).Append(" / panowie miast -").Append(KsFoodCredit.GLordPaid).Append(" / zrodla +").Append(KsFoodCredit.GSrcGot).Append(')');
                }
                else sb.Append(" | kontrola od nastepnej doby");
                // f4 P5 (klucz Ks Food Carts Return Keeps Goods): zawrocone wozy oddaja caly towar zrodlu, reszta zaplaty = roszczenie celu (kontrola kredytu: w "nowe");
                // wozy bez celu i zrodla - polka najblizszego miasta (w kontroli towaru wyzej)
                if (ReturnKeepsGoods)
                    sb.Append(" | zawrocone i bez celu (f4): na polke zrodla bez zwrotu zaplaty ").Append(_dRetKept).Append(" szt. (roszczenia celu wobec zrodla ").Append(_dRetClaim)
                      .Append(" zl, bez wierzyciela ").Append(_dRetNoCred).Append(" zl), wozy bez celu i zrodla na polke najblizszego miasta ").Append(_dNearCarts).Append(" (").Append(_dNear).Append(" szt.)");
                // KS-R3 runda 2 symulatora: loty z dalszych miast i z zapasu kupcow (przed "probka" - regex NKS-22 bez zmian)
                sb.Append(" | z dalszych miast (12 najblizszych bez nadwyzki) ").Append(_dFar).Append(" wozow, z zapasu kupcow (polka zboza i soli ponizej T* x popyt, najwyzej do polowy zapasu) ").Append(_dRes).Append(" wozow, ").Append(_dResGold).Append(" zl");
                // recenzja runda 3 (wazne): wynik kupcow (loty dowiezione: wartosc u celu przy cenie polki dzis wobec zaplaty i frachtu) i loty pominiete bez zysku
                _dMarPct.Sort();
                sb.Append(" | kupcy: dowiezione loty ").Append(_dMarLots).Append(", wartosc u celu ").Append(_dMarVal).Append(" zl, zaplata + fracht ").Append(_dMarCost).Append(" zl, wynik ").Append(_dMarVal - _dMarCost)
                  .Append(" zl, mediana marzy ").Append(_dMarPct.Count > 0 ? (100.0 * _dMarPct[_dMarPct.Count / 2]).ToString("0.0", inv) + "%" : "-").Append(", ze strata ").Append(_dMarLoss).Append(", pominiete bez zysku ").Append(_dNoProfit);
                // recenzja runda 3 (wazne): kasy miast z pusta polka (cel KS-H dzis) wobec zapasu kupcow
                _dCofShort.Sort();
                sb.Append(" | kasy miast z brakiem / zapas kupcow: ").Append(_dCofShort.Count > 0 ? "q10 " + _dCofShort[(int)(0.1 * (_dCofShort.Count - 1))].ToString("0.00", inv) + ", mediana " + _dCofShort[(_dCofShort.Count - 1) / 2].ToString("0.00", inv) + " (" + _dCofShort.Count + " miast)" : "-");
                sb.Append(" | probka wyceny 1/64 ").Append(_dSample > 0 ? (100.0 * _dSampleErr / _dSample).ToString("0.00", inv) + "% (max " + (100.0 * _dSampleMax).ToString("0.00", inv) + "%, lotow " + _dSample + ", pierwsze " + SampleCap + " szt., czas " + (_dSampleTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00", inv) + " ms poza czasem)" : "-")
                  .Append(" | tabela dob ").Append(_pairsDone).Append('/').Append(_pairs != null ? _pairs.Length : 0);
                AppendR4(sb, inv);
                AppendR5(sb, inv, depNow, depCarts, depCtl);
                sb
                  .Append(" | czas ").Append((_dTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00", inv)).Append(" ms | potkniecia ").Append(_dStum).Append('.');
                // KS-R5 ceny-r5 (NKS-24): co 7 dob dowoz do krain importerow (suma wszystkich korytarzy, bez sufitu 120 pozycji listy korytarzy nizej) i przyczyny braku dowozu
                if (week) { AppendR5Week(sb, inv); ClearMiss(); }
                if (Today() % 7 == 0 && _delEma.Count > 0)
                {
                    var l = new List<KeyValuePair<string, double>>(_delEma);
                    l.Sort((a, b) => b.Value.CompareTo(a.Value));
                    sb.Append(" | korytarze (dowiezione, srednia 28 dob, szt./d):");
                    int n = 0;
                    foreach (var kv in l)
                    {
                        if (kv.Value < 1.0 || n >= 120) break;
                        var p = kv.Key.Split('|'); int g;
                        string gn = p.Length == 3 && int.TryParse(p[2], out g) && g >= 0 && g < KsTab.NG ? KsTab.Goods[g] : "?";
                        sb.Append(n == 0 ? " " : ", ").Append(p.Length > 1 ? p[0] + ">" + p[1] : kv.Key).Append(' ').Append(gn).Append(' ').Append(kv.Value.ToString("0", inv));
                        n++;
                    }
                }
                Log.Info(sb.ToString());
                if (active) { try { Log.Info(KsCostKsh.Line(Today() - 1, _dTicks * 1000.0 / Stopwatch.Frequency)); } catch { } }   // ks-koszt: pod-czasy KS-H (tylko log)
            }
            catch (Exception e) { Log.Error("KsFoodCarts.Flush", e); }
            _prevTransit = now;
            ClearDay();
        }

        /// <summary>KS-R4 r4-kupcy H6: czesc linii KS-H - kredyt kupiecki (stan, doba, kontrole), zablokowane z braku pieniedzy wedlug towaru, glod pierwszy, Polnoc i Mur.</summary>
        private static void AppendR4(StringBuilder sb, CultureInfo inv)
        {
            try
            {
                int today = Today();
                long open, overdue, maxOwed; int bills, towns, odTowns; string top;
                KsFoodCredit.Census(today, out open, out bills, out towns, out overdue, out odTowns, out maxOwed, out top);
                string ctl = _creditCtl;
                long rest2 = KsFoodCredit.DBilled - KsFoodCredit.DPaidNow - KsFoodCredit.DNew;
                sb.Append(" (wersja ").Append(_t != null ? DaysVer : "-").Append(_byDays ? ", statek wedlug dob" : ", statek gdy droga krotsza").Append(')');
                sb.Append(" | kredyt kupiecki ").Append(ConsignKey ? "TAK" : "NIE").Append(": otwarte ").Append(open).Append(" zl (").Append(bills).Append(" rachunkow, ").Append(towns).Append(" miast")
                  .Append(top != "-" ? "; najwiecej: " + top : "").Append("), nowe ").Append(KsFoodCredit.DNew).Append(" zl (").Append(KsFoodCredit.DNewN).Append("), u celu ").Append(KsFoodCredit.DBilled)
                  .Append(" zl w ").Append(KsFoodCredit.DDeliv).Append(" dostawach, zaplacone przy dostawie ").Append(KsFoodCredit.DPaidNow).Append(" zl, splacone ").Append(KsFoodCredit.DRepaid)
                  .Append(" zl, starsze niz ").Append(KsFoodCredit.OverdueDays).Append(" dni ").Append(overdue).Append(" zl (").Append(odTowns).Append(" miast), przepadle (wojna) ").Append(KsFoodCredit.DForfeit)
                  .Append(" zl (").Append(KsFoodCredit.DForfeitN).Append(" rachunkow; od wczytania ").Append(KsFoodCredit.ForfeitTotal).Append(" zl), na kredyt wyslane ").Append(_dConsPcs).Append(" szt. w ").Append(_dConsLots)
                  .Append(" wozach (wartosc zrodla ").Append(_dConsVal).Append(" zl, fracht ze zrodel ").Append(_gSrcFr).Append(" zl), zablokowane: zrodlo bez miejsca ").Append(_dSrcCap).Append(" szt., cel z zaleglym ")
                  .Append(_dOverdue).Append(" szt. | kontrola kredytu reszta ").Append(ctl).Append(" (otwarte wczoraj + nowe - splacone - przepadle - otwarte dzis), u celu reszta ").Append(rest2)
                  .Append(" (u celu - zaplacone przy dostawie - nowe), potkniecia kredytu ").Append(KsFoodCredit.TakeStumbles());
                // scalenie r4-glodbunt + r4-kupcy: zalegle rachunki glodnych miast splacone przez ich panow (KsLordFood, tryb RACHUNKI) - w "splacone" wyzej i w zlocie (panowie -)
                sb.Append(" | splacone przez panow miast (glod, zalegle rachunki) ").Append(KsFoodCredit.DLordRepaid).Append(" zl w ").Append(KsFoodCredit.DLordN).Append(" splatach, kasy zrodel przyjely ").Append(KsFoodCredit.GLordPaid).Append(" zl");
                // f4 P5: wczesna splata panow (klucz Ks Lord Food Bills Early Share) - czesc "splacone przez panow" przed zwloka i wiek rachunkow (bez klucza - bez segmentu)
                int eAge = KsLordFood.EarlyAge(Settings.Current);
                if (eAge >= 0 && Settings.Current != null && Settings.Current.KsLordFundsFood)
                {
                    int oldAge; long older; KsFoodCredit.AgeStats(today, eAge, out oldAge, out older);
                    sb.Append(", w tym przed zwloka (wczesna splata f4) ").Append(KsFoodCredit.DLordEarly).Append(" zl w ").Append(KsFoodCredit.DLordEarlyN).Append(" splatach")
                      .Append("; najstarszy rachunek ").Append(oldAge).Append(" dni, rachunki starsze niz ").Append(eAge).Append(" dni ").Append(older).Append(" zl");
                }
                // recenzja r4-kupcy: wozy na kredyt w drodze - przed cofnieciem DLL ponizej r4-kupcy musza byc 0 (starsza DLL dowiozlaby je bez rachunku) i otwarte rachunki 0
                long cN = 0, cPcs = 0, cVal = 0;
                foreach (var c in _carts) { if (!c.Cons || c.N <= 0) continue; cN++; cPcs += c.N; cVal += Math.Max(0, c.Val); }
                sb.Append(" | kredyt w drodze: wozow ").Append(cN).Append(" (").Append(cPcs).Append(" szt., wartosc zrodla ").Append(cVal).Append(" zl; przed cofnieciem DLL ponizej r4-kupcy: 0 wozow i 0 zl otwartych)");
                sb.Append(" | zablokowane z braku pieniedzy wedlug towaru (szt.):");
                bool f = true;
                for (int k = 0; k < NZ; k++) { if (_dPoorByZ[k] <= 0) continue; sb.Append(f ? " " : ", ").Append(KsBasket.Ids[Z[k]]).Append(' ').Append(_dPoorByZ[k]); f = false; }
                if (f) sb.Append(" -");
                sb.Append(" | glod pierwszy: celow ").Append(_dFamReq).Append(" (rozne miasta; zamowien glodu ").Append(_dFamReqK).Append(" - pary miasto i towar; zboze, ryby, mieso ponizej 0.5 T*), zamowione ").Append(_dFamOrd).Append(" szt., wyslane ").Append(_dFamGot).Append(" szt.");
                long inN = 0; int nN = 0;
                if (_north != null) { for (int i = 0; i < _nt && i < _north.Length; i++) if (_north[i]) { nN++; inN += _inc != null ? SumInc(i) : 0; } }
                sb.Append(" | Polnoc i Mur (").Append(nN).Append(" miast): wyslane ").Append(_dNorthShip).Append(" szt., dowiezione ").Append(_dNorthArr).Append(" szt. do ").Append(_dNorthBy.Count).Append(" miast");
                if (_dNorthBy.Count > 0)
                {
                    var l = new List<KeyValuePair<string, long>>(_dNorthBy); l.Sort((x, y) => y.Value.CompareTo(x.Value));
                    sb.Append(" (");
                    for (int i = 0; i < l.Count && i < 8; i++) sb.Append(i == 0 ? "" : ", ").Append(l[i].Key).Append(' ').Append(l[i].Value);
                    sb.Append(')');
                }
                sb.Append(", w drodze ").Append(inN).Append(" szt.");
                sb.Append(" | zamki Muru poza KS-H ").Append(_wallCastles).Append(" (").Append(_wallCastleNames).Append("; KS-H wozi tylko do miast - zboze do zamkow Muru: danina Muru r4-mur do spichlerzy okregow Strazy)");
            }
            catch (Exception e) { Stumble("AppendR4", e); }
        }

        private static long SumInc(int t) { long v = 0; for (int k = 0; k < NZ; k++) v += _inc[t * NZ + k]; return v; }

        /// <summary>KS-R5 ceny-r5: czesc linii KS-H - zadatki kupcow celu (klucz, udzial; przy wysylce, odblokowane, rozliczenie przy dostawie i przy zawroceniu, w drodze,
        /// kontrola zadatkow: w drodze wczoraj + zaplacone - rozliczone = w drodze dzis).</summary>
        private static void AppendR5(StringBuilder sb, CultureInfo inv, long depNow, int depCarts, string depCtl)
        {
            try
            {
                sb.Append(" | zadatki kupcow celu (Ks Consign Deposit ").Append(DepositKey && ConsignKey ? "TAK" : "NIE").Append(", udzial ").Append(DepositShare.ToString("0.00", inv))
                  .Append("; gdy zrodlo bez miejsca kredytu): przy wysylce ").Append(_dDepLots).Append(" wozow, ").Append(_dDepPcs).Append(" szt., ").Append(_dDepGold)
                  .Append(" zl (cel -> zrodlo; przyciete kasa celu ").Append(_dDepShort).Append(" zl), odblokowane ").Append(_dDepFreed).Append(" szt.; przy dostawie zaliczone ").Append(_dDepCred).Append(" zl, nadplata zwrocona ").Append(_dDepOver)
                  .Append(" zl; zwrot zawroconych ").Append(_dDepBack).Append(" zl; roszczenia celu ").Append(_dDepClaim).Append(" zl (zrodlo bez kasy na zwrot; rachunki ")
                  .Append(KsFoodCredit.DClaimN).Append("); bez rozliczenia ").Append(_dDepLost).Append(" zl | zadatki w drodze ").Append(depNow).Append(" zl w ").Append(depCarts)
                  .Append(" wozach | kontrola zadatkow reszta ").Append(depCtl).Append(" (w drodze wczoraj + zaplacone - rozliczone - w drodze dzis)")
                  .Append(" | reeksport (zrodlo czeka na ten sam towar KS-H w drodze do siebie) ").Append(_dReexp).Append(" szt. w ").Append(_dReexpLots).Append(" wozach");
            }
            catch (Exception e) { Stumble("AppendR5", e); }
        }

        /// <summary>KS-R5 ceny-r5 (NKS-24): co 7 dob - dowoz do krain spoza krainy (srednia 28 dob, szt./d, suma wszystkich korytarzy - lista korytarzy linii jest ucieta do 120
        /// pozycji) i brak do T* zamowien bez lotu wedlug przyczyny (kraina celu x towar, suma dob od ostatniej linii tygodnia).</summary>
        private static void AppendR5Week(StringBuilder sb, CultureInfo inv)
        {
            try
            {
                var imp = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var kv in _delEma)
                {
                    var p = kv.Key.Split('|'); int g;
                    if (p.Length != 3 || p[0] == p[1] || !int.TryParse(p[2], out g) || g < 0 || g >= KsTab.NG) continue;
                    string key = p[1] + " " + KsTab.Goods[g];
                    double v; imp.TryGetValue(key, out v); imp[key] = v + kv.Value;
                }
                var li = new List<KeyValuePair<string, double>>(imp); li.Sort((a, b) => b.Value.CompareTo(a.Value));
                sb.Append(" | dowoz do krain spoza krainy (srednia 28 dob, szt./d):");
                int n = 0;
                foreach (var kv in li) { if (kv.Value < 1.0) break; sb.Append(n == 0 ? " " : ", ").Append(kv.Key).Append(' ').Append(kv.Value.ToString("0", inv)); n++; }
                if (n == 0) sb.Append(" -");
                // KS-R5 (pomiar): krainy w obie strony - ten sam towar jedzie x>y i y>x (srednie 28 dob; mniejszy z dwoch kierunkow, szt./d)
                var both = new List<KeyValuePair<string, double>>();
                foreach (var kv in _delEma)
                {
                    var p = kv.Key.Split('|');
                    if (p.Length != 3 || p[0] == p[1] || string.CompareOrdinal(p[0], p[1]) > 0) continue;
                    double back; if (!_delEma.TryGetValue(p[1] + "|" + p[0] + "|" + p[2], out back)) continue;
                    double mn = Math.Min(kv.Value, back); int g;
                    if (mn >= 1.0 && int.TryParse(p[2], out g) && g >= 0 && g < KsTab.NG) both.Add(new KeyValuePair<string, double>(p[0] + "<>" + p[1] + " " + KsTab.Goods[g], mn));
                }
                both.Sort((a, b) => b.Value.CompareTo(a.Value));
                double bothSum = 0; foreach (var kv in both) bothSum += kv.Value;
                sb.Append(" | krainy w obie strony (srednia 28 dob, mniejszy kierunek, szt./d): razem ").Append(bothSum.ToString("0", inv));
                for (int i = 0; i < both.Count && i < 30; i++) sb.Append(i == 0 ? "; " : ", ").Append(both[i].Key).Append(' ').Append(both[i].Value.ToString("0", inv));
                AppendOfferLeft(sb, inv);
                // przyczyny braku dowozu (pomiar KsFoodCore.Miss)
                var m = _miss; int NR = KsFoodCore.NMiss;
                sb.Append(" | bez dowozu wedlug przyczyny (").Append(_missDays).Append(" dob, brak do T* szt.; kraina celu x towar; najmniejszy lot z prawdziwego popytu Ks Carts Min Lot True Demand ")
                  .Append(MinLotKey ? "TAK" : "NIE").Append("):");   // f19-e2: stan klucza w naglowku (checker NKS_KSHMISS: [^)]* - bez nawiasow w srodku)
                if (m == null || _t == null || m.Length != _nt * NZ * NR) { sb.Append(" -"); return; }
                var tot = new double[NR];
                var by = new Dictionary<string, double[]>(StringComparer.Ordinal);
                var land = new string[_nt];
                for (int t = 0; t < _nt; t++) { try { land[t] = StartCalibration.TownLandOf(_st[t]); } catch { land[t] = null; } if (string.IsNullOrEmpty(land[t])) land[t] = "?"; }
                for (int t = 0; t < _nt; t++)
                    for (int k = 0; k < NZ; k++)
                    {
                        int o = (t * NZ + k) * NR; double s = 0; for (int r = 0; r < NR; r++) s += m[o + r];
                        if (!(s > 0)) continue;
                        string key = land[t] + " " + (PlanG[k] >= 0 ? KsTab.Goods[PlanG[k]] : KsBasket.Ids[Z[k]]);
                        double[] a; if (!by.TryGetValue(key, out a)) { a = new double[NR]; by[key] = a; }
                        for (int r = 0; r < NR; r++) { a[r] += m[o + r]; tot[r] += m[o + r]; }
                    }
                sb.Append(" razem");
                for (int r = 0; r < NR; r++) sb.Append(r == 0 ? " " : ", ").Append(KsFoodCore.MissNames[r]).Append(' ').Append(tot[r].ToString("0", inv));
                var lb = new List<KeyValuePair<string, double[]>>(by);
                lb.Sort((x, y) => { double sx = 0, sy = 0; for (int r = 0; r < NR; r++) { sx += x.Value[r]; sy += y.Value[r]; } return sy.CompareTo(sx); });
                int q = 0;
                foreach (var kv in lb)
                {
                    if (q >= 40) break;
                    double s = 0; for (int r = 0; r < NR; r++) s += kv.Value[r];
                    if (s < 1.0) break;
                    sb.Append(q == 0 ? "; " : ", ").Append(kv.Key).Append(' ').Append(s.ToString("0", inv)).Append(" (");
                    var ord = new List<int>(); for (int r = 0; r < NR; r++) if (kv.Value[r] >= 1.0) ord.Add(r);
                    ord.Sort((x, y) => kv.Value[y].CompareTo(kv.Value[x]));
                    for (int i = 0; i < ord.Count; i++) sb.Append(i == 0 ? "" : " / ").Append(KsFoodCore.MissNames[ord[i]]).Append(' ').Append(kv.Value[ord[i]].ToString("0", inv));
                    sb.Append(')');
                    q++;
                }
            }
            catch (Exception e) { Stumble("AppendR5Week", e); }
        }

        /// <summary>Recenzja ceny-r5 (pomiar NKS-7g, co 7 dob): oferta zrodel niesprzedana wedlug krainy zrodla i towaru (szt./d, srednia dob od ostatniej linii tygodnia;
        /// niesprzedane / oferowane, miasto z najwieksza niesprzedana oferta). Duza niesprzedana oferta przy brakach w innych miastach rynku = rozdzial (towar jest,
        /// kupcy go nie wioza: zasieg, marza, wojna, kredyt - przyczyny w "bez dowozu wedlug przyczyny"); mala = zapas roboczy eksportera (polowa / 2/3 nadwyzki na dobe).</summary>
        private static void AppendOfferLeft(StringBuilder sb, CultureInfo inv)
        {
            sb.Append(" | oferta zrodel niesprzedana (").Append(_missDays).Append(" dob, srednia szt./d; kraina zrodla x towar: niesprzedane / oferowane, miasto z najwieksza):");
            var o0 = _off0; var ol = _offLeft;
            if (o0 == null || ol == null || _t == null || o0.Length != _nt * NZ || ol.Length != _nt * NZ || _missDays <= 0) { sb.Append(" -"); return; }
            double dn = _missDays;
            var by = new Dictionary<string, double[]>(StringComparer.Ordinal);
            var top = new Dictionary<string, KeyValuePair<int, double>>(StringComparer.Ordinal);
            double tl = 0, t0 = 0;
            for (int t = 0; t < _nt; t++)
            {
                string land; try { land = StartCalibration.TownLandOf(_st[t]); } catch { land = null; }
                if (string.IsNullOrEmpty(land)) land = "?";
                for (int k = 0; k < NZ; k++)
                {
                    int o = t * NZ + k; double x0 = o0[o], xl = ol[o];
                    if (!(x0 > 0)) continue;
                    string key = land + " " + (PlanG[k] >= 0 ? KsTab.Goods[PlanG[k]] : KsBasket.Ids[Z[k]]);
                    double[] v; if (!by.TryGetValue(key, out v)) { v = new double[2]; by[key] = v; }
                    v[0] += xl; v[1] += x0; tl += xl; t0 += x0;
                    KeyValuePair<int, double> tp; if (!top.TryGetValue(key, out tp) || xl > tp.Value) top[key] = new KeyValuePair<int, double>(t, xl);
                }
            }
            sb.Append(" razem ").Append((tl / dn).ToString("0", inv)).Append(" / ").Append((t0 / dn).ToString("0", inv));
            var li = new List<KeyValuePair<string, double[]>>(by);
            li.Sort((x, y) => y.Value[0].CompareTo(x.Value[0]));
            int q = 0;
            foreach (var kv in li)
            {
                if (q >= 30 || kv.Value[0] / dn < 1.0) break;
                KeyValuePair<int, double> tp; string tn = top.TryGetValue(kv.Key, out tp) && tp.Key >= 0 && tp.Key < _nt ? _st[tp.Key].StringId : "?";
                sb.Append(q == 0 ? "; " : ", ").Append(kv.Key).Append(' ').Append((kv.Value[0] / dn).ToString("0", inv)).Append('/').Append((kv.Value[1] / dn).ToString("0", inv)).Append(" (").Append(tn).Append(')');
                q++;
            }
        }

        private static void ClearMiss()
        {
            if (_miss != null) Array.Clear(_miss, 0, _miss.Length); _missDays = 0;
            if (_off0 != null) Array.Clear(_off0, 0, _off0.Length);
            if (_offLeft != null) Array.Clear(_offLeft, 0, _offLeft.Length);
        }

        // ------------------------------------------------------------ zapis wozow (arm_ks_kupcy): zrodlo|cel|towar|ile|doba zakupu|termin|zaplacone|fracht|morze~
        internal static string Export()
        {
            if (_pending != null) ResolvePending("zapis przed startem sesji");
            var sb = new StringBuilder(_carts.Count * 48);
            foreach (var c in _carts)
            {
                if (c.Src == null || c.Dst == null || c.N <= 0) continue;
                sb.Append(c.Src.StringId).Append('|').Append(c.Dst.StringId).Append('|').Append(KsBasket.Ids[Z[c.Z]]).Append('|').Append(c.N).Append('|').Append(c.DayBought).Append('|')
                  .Append(c.DayDue).Append('|').Append(c.Paid).Append('|').Append(c.Freight).Append('|').Append(c.Sea ? 1 : 0);
                if (c.Cons) sb.Append('|').Append(Math.Max(0, c.Val));   // KS-R4 r4-kupcy: woz na kredyt - 10. pole wartosc zrodla lotu (jest = woz na kredyt; starsza DLL czyta 9 pol i pomija reszte)
                if (c.Cons && c.Dep > 0) sb.Append('|').Append(c.Dep);   // KS-R5 ceny-r5: 11. pole - zadatek kupcow celu (DLL r4-kupcy czyta 10 pol i pomija reszte)
                sb.Append('~');
            }
            string res = sb.ToString();
            // recenzja runda 3 (drobne): rozmiar arm_ks_kupcy w linii (NKS-20)
            try
            {
                int today = Today();
                if (res.Length > 0 && today != _exportLogged)
                {
                    _exportLogged = today;
                    Log.Info("KS: zapis arm_ks_kupcy doba " + today + " | " + (res.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB (wozow " + _carts.Count + ", paczek SaveText " + ((res.Length + 7999) / 8000) + ").");
                }
            }
            catch { }
            return res;
        }
        private static int _exportLogged = -1;

        // recenzja runda 3 (wazne): srednie dowozu korytarzy (CorrDelivered Planu po D) i rurociagu rampy w zapisie (arm_ks_r3, sekcja H) - Plan pierwszej doby po wczytaniu
        // nie widzial dowozu (kazdy korytarz 20 szt./d, krainy w tryb "kraina", gradient skakal o ok. 25%)
        internal static string ExportEma()
        {
            var sb = new StringBuilder(_delEma.Count * 32 + 512);
            sb.Append(_delN).Append(';').Append(_shipN).Append(';');
            bool f = true;
            foreach (var kv in _delEma) { if (kv.Value < 0.01) continue; if (!f) sb.Append(','); f = false; sb.Append(kv.Key.Replace('|', '>')).Append('=').Append(kv.Value.ToString("0.##", CultureInfo.InvariantCulture)); }
            sb.Append(';');
            f = true;
            for (int i = 0; i < _shipEma.Length; i++)
            {
                if (_shipEma[i] <= 0 && _daysEma[i] <= 0) continue;
                if (!f) sb.Append(','); f = false;
                sb.Append(i).Append(':').Append(_shipEma[i].ToString("0.##", CultureInfo.InvariantCulture)).Append(':').Append(_daysEma[i].ToString("0.##", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        internal static void ImportEma(string s)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return;
                var p = s.Split(';'); if (p.Length < 4) return;
                int dn, sn; if (!int.TryParse(p[0], out dn) || !int.TryParse(p[1], out sn)) return;
                _delEma.Clear();
                foreach (var e in p[2].Split(','))
                {
                    int q = e.LastIndexOf('='); double v;
                    if (q <= 0 || !double.TryParse(e.Substring(q + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                    _delEma[e.Substring(0, q).Replace('>', '|')] = Math.Max(0.0, v);
                }
                Array.Clear(_shipEma, 0, _shipEma.Length); Array.Clear(_daysEma, 0, _daysEma.Length);
                foreach (var e in p[3].Split(','))
                {
                    var q = e.Split(':'); int i; double a, b;
                    if (q.Length != 3 || !int.TryParse(q[0], out i) || i < 0 || i >= _shipEma.Length || !double.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out a) || !double.TryParse(q[2], NumberStyles.Float, CultureInfo.InvariantCulture, out b)) continue;
                    _shipEma[i] = Math.Max(0.0, a); _daysEma[i] = Math.Max(0.0, b);
                }
                _delN = Math.Max(0, Math.Min(28, dn)); _shipN = Math.Max(0, Math.Min(7, sn));
            }
            catch (Exception e) { Stumble("ImportEma", e); }
        }

        internal static void Import(string s) { _carts.Clear(); _prevTransit = -1; _depPrev = -1; _pending = string.IsNullOrEmpty(s) ? null : s; }   // KS-R5: kontrola zadatkow od nastepnej doby

        internal static void ResolvePending(string why)
        {
            try { KsFoodCredit.Resolve(); } catch (Exception e) { Stumble("ResolveCredit", e); }   // KS-R4 r4-kupcy: rachunki kredytu z zapisu (arm_daniny_kupcy)
            var s = _pending; _pending = null;
            if (string.IsNullOrEmpty(s)) return;
            var om = MBObjectManager.Instance;
            int ok = 0, lost = 0; long pcs = 0;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    var a = rec.Split('|'); if (a.Length < 9) { lost++; continue; }
                    Settlement src = null, dst = null;
                    try { src = om.GetObject<Settlement>(a[0]); dst = om.GetObject<Settlement>(a[1]); } catch { }
                    int z = Array.IndexOf(KsBasket.Ids, a[2]), k = z >= 0 ? Array.IndexOf(Z, z) : -1;
                    int n, d0, d1; long paid, fr;
                    if (src == null || dst == null || k < 0 || !int.TryParse(a[3], out n) || n <= 0 || !int.TryParse(a[4], out d0) || !int.TryParse(a[5], out d1)
                        || !long.TryParse(a[6], out paid) || !long.TryParse(a[7], out fr)) { lost++; continue; }
                    var c = new Cart { Src = src, Dst = dst, Z = k, N = n, DayBought = d0, DayDue = d1, Paid = Math.Max(0, paid), Freight = Math.Max(0, fr), Sea = a[8] == "1" };
                    long vv; if (a.Length >= 10 && long.TryParse(a[9], out vv) && vv >= 0) { c.Val = vv; c.Cons = true; }
                    long dd; if (c.Cons && a.Length >= 11 && long.TryParse(a[10], out dd) && dd > 0) c.Dep = dd;   // KS-R5 ceny-r5: zadatek kupcow celu
                    _carts.Add(c);
                    if (_t != null) AddInc(c, +1);
                    ok++; pcs += n;
                }
                catch (Exception e) { Stumble("ResolvePending", e); lost++; }
            }
            Log.Info("Kupcy zywnosci (KS-H): z zapisu (" + why + ") " + ok + " wozow w drodze (" + pcs + " szt.; odrzucone bez osady albo towaru " + lost + ").");
        }
    }
}
