using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f5-ruda (noc 18.10, fala 5; fin88 NOP-7 ruda 3.06 / 57 miast bez rudy, NOP-8 "kopalnia bez wegla" 45 lup/d) - RECE PRZY ZLOZACH ZELAZA ZA BRAKIEM RUDY RYNKU.
    ///
    /// Przyczyna z liczbami (fin88 Armoury-2026-10-18_03-13-38.log wobec fin84 bez KS 21-11-36, srednie od wlaczenia 109109-109235):
    ///  - zapas rudy swiata 4862 wobec 10913, polki miast 1151 wobec 5059; roznica ok. 54 lup/d przez 126 dob = pol na pol:
    ///    produkcja lup -32/d (236 wobec 268; pelna stawka KS 307/d, wytopione 241 - reszta to dymarki bez drewna, Bloomery.F5) i kucie narzedzi +30/d
    ///    (linie "smithy" + rzemieslnicy BK 96 wobec 71 lup/d przy narzedziach 120 wobec 284/d: receptura KS wedlug wagi 4 lupy -> 5 narzedzi, gra 1 -> 4;
    ///    receptura zostaje - zasada Jeffa "receptury wedlug wagi", ta sama stala ToolsIron 1.2 co Plan).
    ///  - Plan KS liczy wszystkich odbiorcow zelaza (popyt Planu 348 szt./d: narzedzia ludu i budow x 1.2 + warsztaty zbrojne, linie warsztatow, rzemieslnicy BK,
    ///    naprawy, strzelarze; linie narzedzi nawet dwa razy) - ale po wlaczeniu rece przy zlozach NIE ida za Planem: RC/RF z Planu wlaczenia (Deliver.FromPlan),
    ///    potem tylko RegaleCycle co 28 dob z progu indeksu miasta targowego kopalni (> 1.2 +10%, < 0.8 -10%). Miasto targowe kopalni jest zalane jej wlasna ruda
    ///    (f4 "regale wobec polki": 106-168 wpisow zelaza przy polce > 2 x cel, cykle "w dol przy pustej" 23-64), a reszta rynku stoi pusta - rece przy zelazie
    ///    81840 -> 83058 (+1.5%) przez 56 dob przy "brakuje 1470" lup w 86 miastach (Karawany (stan)).
    ///
    /// REGULA (klucz Ks Iron Hands Follow Market, tylko KS zastosowany - RuleDaily): co 7 dob od D, dla rynku (Westeros / Essos) - suma rudy na polkach miast S
    /// wobec sumy zapasow rzemiosla T (CaravanBulk.TargetFor - ta sama miara, ktora karawany dopelniaja miasta i ktorej uzywa f4 "regale wobec polki"):
    ///   S &lt; T  - rynek w braku: kopalnia, ktorej dymarka miala drewno (lupy bez drewna &lt; 10% modelu w ostatnich 7 dobach, Bloomery.F5Starved) - rece przy zlozu x1.05;
    ///   S &gt; 2T - rynek pelny: x0.95;  T..2T - bez zmian;
    ///   kopalnia z dymarka bez drewna przez pol tygodnia (&gt;= 50%) - x0.95 niezaleznie od rynku (rece, ktore nie wytopia lup, wracaja do pola);
    ///   10-50% albo brak danych wytopu - bez zmian. Sufit jak RegaleCycle: suma rak przy zlozach bloku &lt;= polowa rak bloku poza domem - przycinany TYLKO w bloku,
    ///   w ktorym krok podniosl zelazo, i najwyzej do sumy sprzed kroku (recenzja: inaczej krok co 7 dob przycinal inne zloza blokow z zelazem, gdy praca domowa
    ///   urosla miedzy cyklami RegaleCycle, a w bloku juz nad sufitem "wzrost" obnizal zelazo; blok nad sufitem przycina RegaleCycle co 28 dob jak dotad). Przy suficie wzrost zelaza bierze rece innych zloz bloku, nie pol - linia
    ///   pokazuje to osobno: bloki z zelazem na suficie PRZED krokiem (i ich rece przy zelazie), rece zelaza po mnoznikach (zamiar) i po suficie, rece innych zloz
    ///   oddane zelazu. Recenzja (f4 fin88: wpisy w gore 227/356 przy rekach -1.5%/+3.1%): czy sufit bloku wiaze, pokaze pierwsza linia (D+7); gdy wiaze - sufit
    ///   na wpis jak w Planie (Core: craftLab x 0.5 na wiersz) albo pierwszenstwo zelaza to decyzja Jeffa (NRU-3 DECYZJA). Bez nowych zloz (tylko wpisy
    ///   z Planu wlaczenia). Wpisy zelaza nie biora juz progu indeksu w RegaleCycle (inne zloza bez zmian). Pola mieszczan (zelazo bez dymarki, fin88 0 szt./d) -
    ///   bez wzrostu, w dol tylko przy rynku pelnym; kampania bez wytopu przy kopalni (Bloomery nieczynne) - wsie sam rynek.
    /// Rece przesuwaja sie tylko w bloku (rece zelaza + rece reszty zajec = rece bloku) - zadnego zlota ani towaru (produkcja z prawdziwych rak i ziemi, jedna tabela
    /// swiata). RF jest w zapisie KS (arm_ks, Live.cs) - bez nowego klucza zapisu. Klucz NIE - RegaleCycle jak dotad co do bitu, krok tygodniowy nie biegnie.
    /// Linia "Ruda KS (f5-ruda): dzien N" co 7 dob. Koszt: 97 miast + bloki z zelazem (ok. 550) raz na 7 dob (czas w linii).
    /// 282 (F4, klucz Ks Iron Hands Count Transit): brak rynku liczony z polek i zelaza w drodze do miast (wozy kupcow, kontrakty karawan) - log 27.10: krok podnosil
    /// rece, gdy zapas swiata rosl +36 lup/d, bo nie widzial 1000-1300 lup w wozach; segment ", w drodze do miast R" w linii po decyzji rynku.
    /// </summary>
    internal static partial class StartCalibration
    {
        private const int F5IronEvery = 7;
        private const float F5Up = 1.05f, F5Down = 0.95f;
        private const float F5StarvUp = 0.1f, F5StarvDown = 0.5f;   // wzrost tylko przy dymarce prawie bez brakow drewna (< 10% modelu bez drewna) - 'kopalnia bez wegla' nie rosnie z rekami
        private static int _f5Stum;
        // 285: zapas rynku (polki + droga + juki karawan) z poprzedniego kroku - trend; -1 = bez pomiaru (pierwszy krok sesji). Pamiec sesji, zerowana w Reset.
        private static readonly long[] _f5Tot = { -1, -1 };
        // 290: zapas swiata rynku (polki + droga + juki karawan + wsie + wozy wsi + zamki) z poprzedniego kroku; -1 = bez pomiaru. Pamiec sesji, zerowana razem z 285.
        private static readonly long[] _f5W = { -1, -1 };
        internal static void F5Reset285() { _f5Tot[0] = _f5Tot[1] = -1; _f5W[0] = _f5W[1] = -1; }

        internal static bool F5IronOn { get { var s = Settings.Current; return s != null && s.KsIronHandsFollowMarket; } }

        /// <summary>RegaleCycle: wpis zelaza przy kluczu - bez progu indeksu (krok tygodniowy F5IronWeek).</summary>
        private static bool F5IronSkip(int craft) { return craft == KsTab.CIron && F5IronOn; }

        /// <summary>RuleDaily: doba kroku (co 7 dob od D, przy kluczu).</summary>
        private static bool F5IronDay(int today, int d0) { return d0 >= 0 && today > d0 && (today - d0) % F5IronEvery == 0 && F5IronOn; }

        private static void F5IronWeek(int today, int d0)
        {
            long t0 = Stopwatch.GetTimestamp();
            var inv = CultureInfo.InvariantCulture;
            EnsureItems();
            var iron = _items != null ? _items[KsDeliver.IIron] : null;
            if (iron == null) { Stumble("F5IronWeek(brak przedmiotu iron)", null); return; }
            // 1. rynek: polki wobec zapasu rzemiosla
            var S = new long[2]; var T = new long[2]; var D = new long[2]; var nT = new int[2]; var nE = new int[2]; var nS = new int[2];
            // 282 (F4, klucz KsIronHandsCountTransit): zelazo w drodze do miast rynku (wozy kupcow P3/Z1 + kontrakty karawan 174) - brak rynku to polki + droga < cel
            // (ta sama regula co jedzenie f20-d4); pelny - jak dotad S > 2T (S + R > 2T wynika z tego samego). Klucz NIE - co do bitu jak dotad (R tylko w linii)
            var R = new long[2];
            var sR = Settings.Current;
            bool rOn = sR != null && sR.KsIronHandsCountTransit;
            // 286 (klucz KsIronLocalGlut): miasto targowe kopalni pelne (polki + droga > 2 x zapas rzemiosla - ta sama miara i prog co rynek pelny) - log 134a 109055:
            // 37 zrodel z 2 136 lupami ponad punkt zamowienia, zrodla sprzedaja po 17 d, miasta bez rudy placa 81 d (stosunek 2.42 -> 3.59 w 30 dobach); krok rynku
            // (Westeros / Essos) podnosil rece takze kopalniom przy zalanych miastach (Essos BRAK 109049 - 123 wpisy w gore)
            bool locOn = sR != null && sR.KsIronLocalGlut;
            var fullTown = new HashSet<Town>(); var nFullT = new int[2];
            // poprawka 135a (286b, klucz KsIronLocalGlutBand): log 135a (90 dob) - rece przy zelazie 113 344 -> 73 226, kopalnie przy miastach pelnych ciete takze przy
            // rynku w BRAKU (Essos 109049/109056/109070 - 124/103/60 wpisow, Westeros 109077-109112 - 195/129/108/21/19), miasta bez rudy 9 -> 25 na koniec. Miasto pelne
            // dopiero, gdy nadmiar ponad 2 x cel jest wiekszy niz tydzien wydobycia jego kopalni (stawka KS zelaza wsi tego miasta targowego x 7 dob - jeden krok; mniejszy
            // nadmiar zejdzie wozami w kroku, ciecie rak go nie zmieni); przy BRAKU rynku te kopalnie trzymaja rece (bez wzrostu, nigdy w dol); w pasmie kopalnie miasta,
            // ktore spadlo ponizej celu (polki + droga < cel), rosna jak przy braku (powrot po cieciu). Klucz NIE - jak 286.
            bool bandOn = locOn && sR.KsIronLocalGlutBand;
            // 287 (klucz KsIronFullOnlyOwnTown, przy 286b): autotest 135b - rynek PELNY (polki + droga > 2 x cel calego rynku) ciac WSZYSTKIE kopalnie rynku (512 wpisow
            // w kroku), takze przy miastach targowych z brakiem: Westeros PELNY 109035-109077 przy 5-15 z 59 miast bez rudy, Essos 109056-109105 przy 2-8 z 38; rece przy
            // zelazie 113 344 -> 79 346, zelazo swiata ostatnie 30 dob -69/d. Przy rynku PELNYM tna sie tylko kopalnie, ktorych wlasne miasto targowe jest pelne (ta sama
            // miara co 286b: polki + droga - 2 x cel > tydzien wydobycia jego kopalni); kopalnie miasta bez nadmiaru trzymaja rece (bez wzrostu - rynek jako calosc ma
            // nadmiar, rozwoza go wozy). Kopalnia bez znanego miasta targowego - jak dotad (w dol). Klucz NIE - jak 286b.
            bool fullOwnOn = bandOn && sR.KsIronFullOnlyOwnTown;
            var nFullHold = new int[2];
            var townRate = new Dictionary<Town, double>(); var shortTown = new HashSet<Town>(); var nShortT = new int[2];
            if (bandOn)
            {
                try
                {
                    foreach (var kv in _rates)
                    {
                        var a = kv.Value; var v = kv.Key;
                        if (a == null || a.A == null || v == null || KsDeliver.IIron >= a.A.Length) continue;
                        float x = a.A[KsDeliver.IIron]; if (!(x > 0f)) continue;
                        var mk = v.TradeBound ?? v.Bound;
                        var tw = mk != null ? mk.Town : null;
                        if (tw == null) continue;
                        double r0; townRate.TryGetValue(tw, out r0); townRate[tw] = r0 + x;
                    }
                }
                catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(stawki miast 286b)", e); }
            }
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown || t.Settlement == null || t.Settlement.ItemRoster == null) continue;
                    int m = TownMarket(t); if (m < 0 || m > 1) continue;
                    int have = Math.Max(0, t.Settlement.ItemRoster.GetItemNumber(iron));
                    int tg = Math.Max(0, CaravanBulk.TargetFor(t, iron));
                    S[m] += have; T[m] += tg; nT[m]++;
                    int tr = Math.Max(0, MaterialOrders.InTransitTo(t.Settlement, iron));
                    R[m] += tr;
                    if (have <= 0) nE[m]++;
                    if (have < tg) { nS[m]++; D[m] += tg - have; }
                    long onT = have + (rOn ? tr : 0);
                    if (bandOn)
                    {
                        double wk; townRate.TryGetValue(t, out wk); wk *= F5IronEvery;
                        if (tg > 0 && onT - 2L * tg > wk) { fullTown.Add(t); nFullT[m]++; }
                        else if (tg > 0 && onT < tg) { shortTown.Add(t); nShortT[m]++; }
                    }
                    else if (locOn && tg > 0 && onT > 2L * tg) { fullTown.Add(t); nFullT[m]++; }
                }
                catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(miasto)", e); }
            }
            // 285: juki karawan rynku (miasto, w ktorym stoi, jego cel albo dom) - do trendu zapasu rynku (rozladunek karawan na polki to nie produkcja)
            var C = new long[2];
            try
            {
                foreach (var cp in MobileParty.AllCaravanParties)
                {
                    if (cp == null || !cp.IsActive || cp.ItemRoster == null) continue;
                    int n = cp.ItemRoster.GetItemNumber(iron);
                    // recenzja: ruda kontraktu karawany jest juz w R (InTransitTo celu) - w jukach tylko reszta, inaczej kontrakt do miasta z brakiem liczy sie jak wzrost zapasu
                    if (n > 0) n -=Math.Min(n, MaterialOrders.ContractQty(cp, iron));
                    if (n <= 0) continue;
                    var ct = cp.CurrentSettlement != null ? cp.CurrentSettlement.Town : null;
                    if (ct == null && cp.TargetSettlement != null) ct = cp.TargetSettlement.Town;
                    if (ct == null && cp.HomeSettlement != null) ct = cp.HomeSettlement.Town;
                    int cm = ct != null ? TownMarket(ct) : -1;
                    if (cm == 0 || cm == 1) C[cm] += n;
                }
            }
            catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(juki 285)", e); }
            // 285 (klucz KsIronFullCountsTransit): rynek pelny liczony tak jak brak - z polkami i zelazem w drodze: S + R > 2T (dotad S > 2T przy brak S + R < T -
            // log 133a: Westeros polki z droga 2.10 i 2.29 przy decyzji PASMO w 109028 i 109035, pierwszy PELNY dopiero 109042). Klucz KsIronStockTrend: w pasmie
            // T..2T zapas rynku (polki + droga + juki karawan) wiekszy niz przy poprzednim kroku = wydobycie ponad zuzycie - w dol x0.95 (log 133a: swiat +52 lup/d
            // w ostatnich 10 dobach przy PASMIE). Pierwszy krok sesji bez pomiaru - bez trendu. Recenzja: tylko zmiana ponad prog kroku i w obie strony (nizej).
            var s285 = Settings.Current;
            bool fullR = rOn && s285 != null && s285.KsIronFullCountsTransit, trendOn = s285 != null && s285.KsIronStockTrend;
            // recenzja: prog trendu = zmiana wydobycia, ktora robi jeden krok (rece x0.95 / x1.05 = 5% stawki KS zelaza rynku przez 7 dob) - zmiana zapasu mniejsza
            // niz to, co zmienia krok, jest w szumie kroku (krok by ja przestrzelil); w pasmie dziala w obie strony: zapas rosnie ponad prog - w dol, spada ponad prog
            // - w gore (kopalnie z drewnem, jak BRAK). Dotad kazdy wzrost o 1 obnizal rece, a spadek nic - zapas schodzil krokami do krawedzi braku T. Bez stawki KS - bez trendu
            var rate = new double[2]; var rateN = new int[2];
            try
            {
                var rates = _rates;
                foreach (var kv in rates)
                {
                    var a = kv.Value; if (a == null || a.A == null || KsDeliver.IIron >= a.A.Length) continue;
                    float x = a.A[KsDeliver.IIron]; if (!(x > 0f)) continue;
                    int m = a.M == 0 ? 0 : 1; rate[m] += x; rateN[m]++;
                }
            }
            catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(stawki)", e); }
            var tot = new long[2]; var prevTot = new long[2]; var trend = new int[2]; var thr = new long[2];
            var dec = new int[2];                                  // +1 brak, -1 pelny, 0 pasmo, 2 bez celu (zapas rzemiosla nieznany)
            for (int m = 0; m < 2; m++)
            {
                tot[m] = S[m] + R[m] + C[m]; prevTot[m] = _f5Tot[m];
                thr[m] = (long)Math.Round(rate[m] * F5IronEvery * (1.0 - F5Down));
                dec[m] = T[m] <= 0 ? 2 : S[m] + (rOn ? R[m] : 0) < T[m] ? 1 : S[m] + (fullR ? R[m] : 0) > 2 * T[m] ? -1 : 0;
                if (dec[m] == 0 && trendOn && prevTot[m] >= 0 && thr[m] > 0)
                {
                    long dl = tot[m] - prevTot[m];
                    if (dl > thr[m]) { dec[m] = -1; trend[m] = -1; }
                    else if (-dl > thr[m]) { dec[m] = 1; trend[m] = 1; }
                }
                _f5Tot[m] = tot[m];
            }
            // 290 (klucz KsIronWorldFlow): RECE ZA ZAPASEM SWIATA RYNKU. Autotest 136a (zainstalowany 136, 90 dob 109045-109134 na kopii zapisu Jeffa): Westeros PELNY
            // w 13 z 13 krokow (polki + droga 2.1-2.9 x cel - nadmiar stoi w 7-14 miastach przy kopalniach, 'nadwyzka zrodel-miast' 900-1 400), Essos PELNY w 12 z 13;
            // krok cial co tydzien (rece przy zelazie 113 896 -> 76 705, w tym -19 tys. krokami), takze gdy zapas swiata juz spadal (109091-109133: -34, -30, -284,
            // -493, -500, -486, -256 lup na tydzien). Wydobycie 386 -> 289 lup/d przy zuzyciu ok. 365/d (warsztaty zbrojne 213, narzedzia 141, strzelarze 7) - zelazo
            // swiata +35/d w pierwszych 30 dobach, -66/d w ostatnich 30. Poziom polek mowi, GDZIE jest zelazo (zrodla pelne), a nie czy swiat wydobywa wiecej niz zuzywa.
            // Regula: W = zapas swiata rynku (polki + droga + juki karawan jak 285 + magazyny wsi + wozy wsi + zamki - wszystko poza taborami lordow i band), dW = zmiana
            // od poprzedniego kroku (7 dob = wydobycie - zuzycie - straty rynku), prog = prog trendu 285 (zmiana wydobycia, ktora robi jeden krok). Poziom (bez trendu 285):
            //   PELNY (polki + droga > 2 x cel): w dol tylko, gdy W jeszcze rosnie (dW > 0); W spada szybciej niz 2 progi - w gore; inaczej trzymane (nadmiar schodzi sam
            //     najwyzej o 2 progi na tydzien - nie szybciej, wiec wydobycie nie zostaje daleko pod zuzyciem, gdy polki wroca do pasma);
            //   BRAK (polki + droga < cel): w gore, dopoki W nie rosnie o prog na tydzien; potem trzymane (zelazo jest w drodze, wsiach, jukach - przyjdzie);
            //   PASMO: W rosnie ponad prog - w dol, spada ponad prog - w gore, inaczej trzymane (jak trend 285, ale na calym zapasie).
            // Kopalnie: w dol - wszystkie poza kopalniami miasta targowego z brakiem (286b - trzymaja); w gore - kopalnie z drewnem (< 10% bez drewna, jak BRAK) poza
            // kopalniami miasta targowego pelnego (286 - trzymaja); bez danych wytopu i pola mieszczan - trzymane. Dymarka bez drewna >= 50% - w dol jak dotad (rece, ktore
            // nie wytopia). Pierwszy krok sesji (bez pomiaru dW) - trzymane. Bez stawki KS rynku (prog 0), bez celu, potkniecie pomiaru - reguly 282-287 jak dotad.
            // Decyzja rynku w linii (BRAK/PELNY/PASMO z trendem 285) bez zmian - ruch 290 i kubly decyzji w segmencie '; 290: ...' (osobne kubly, suma decyzji = wpisy).
            // Tylko podzial rak bloku (jak caly krok) - bez zlota i towaru. Klucz NIE - co do bitu jak 287 (segment 290 nie jest drukowany).
            bool flowOn = sR != null && sR.KsIronWorldFlow, flowOk = flowOn;
            var wV = new long[2]; var wP = new long[2]; var wC = new long[2]; var wTot = new long[2]; var wPrev = new long[2];
            var lv = new int[2]; var act = new int[2];             // act: +1 w gore, -1 w dol, 0 trzymane, 3 bez pomiaru (trzymane), 2 poza regula 290 (reguly 282-287)
            var u290 = new int[2]; var d290 = new int[2]; var h290 = new int[2];
            // 294c (F, klucz KsIronWorldFlowOwnTownCut): autotest 296c - przy rynku PELNYM krok 290 cial rece przy KAZDYM wzroscie zapasu swiata (dW > 0, nie > prog) i we
            // wszystkich kopalniach poza miastami z brakiem (z pominieciem reguly 287 KsIronFullOnlyOwnTown); zelazo swiata ostatnie 30 dob -30.5/d (296b bez 294b -8.3),
            // wytop bez wegla 20.1 lup/d. Teraz przy PELNYM: w dol tylko przy dW > prog (jak PASMO; nadmiar mniejszy niz krok schodzi sam), a ciecie (przy 287) tylko kopalnie
            // miast targowych pelnych - kopalnie miast bez nadmiaru trzymaja rece (kubel 'trzymane'); kopalnia bez znanego miasta targowego - w dol jak dotad. W gore,
            // BRAK i PASMO bez zmian. Tylko podzial rak bloku; klucz NIE - co do bitu jak 290.
            bool fCutOn = flowOn && sR.KsIronWorldFlowOwnTownCut;
            var fDn = new int[2]; var fHold = new int[2];
            if (flowOn)
            {
                try
                {
                    foreach (var st in Settlement.All)
                    {
                        if (st == null || st.IsTown || st.ItemRoster == null || !(st.IsVillage || st.IsCastle)) continue;
                        int n = st.ItemRoster.GetItemNumber(iron); if (n <= 0) continue;
                        int mk = MarketOfSettlement(st); if (mk != 0 && mk != 1) continue;
                        if (st.IsVillage) wV[mk] += n; else wC[mk] += n;
                    }
                    foreach (var vp in MobileParty.AllVillagerParties)
                    {
                        if (vp == null || !vp.IsActive || vp.ItemRoster == null) continue;
                        int n = vp.ItemRoster.GetItemNumber(iron); if (n <= 0) continue;
                        int mk = MarketOfSettlement(vp.HomeSettlement); if (mk != 0 && mk != 1) continue;
                        wP[mk] += n;
                    }
                }
                catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(zapas swiata 290)", e); flowOk = false; }
            }
            for (int m = 0; m < 2; m++)
            {
                wTot[m] = tot[m] + wV[m] + wP[m] + wC[m]; wPrev[m] = _f5W[m];
                lv[m] = T[m] <= 0 ? 2 : S[m] + (rOn ? R[m] : 0) < T[m] ? 1 : S[m] + (fullR ? R[m] : 0) > 2 * T[m] ? -1 : 0;
                if (!flowOk || lv[m] == 2 || thr[m] <= 0) act[m] = 2;
                else if (wPrev[m] < 0) act[m] = 3;
                else
                {
                    long dl = wTot[m] - wPrev[m];
                    if (lv[m] == -1) act[m] = dl > (fCutOn ? thr[m] : 0) ? -1 : dl < -2 * thr[m] ? 1 : 0;   // 294c (F): przy kluczu w dol dopiero ponad prog
                    else if (lv[m] == 1) act[m] = dl < thr[m] ? 1 : 0;
                    else act[m] = dl > thr[m] ? -1 : dl < -thr[m] ? 1 : 0;
                }
                _f5W[m] = flowOk ? wTot[m] : -1;
            }
            // 2. bloki z zelazem
            int nIn = 0, nUp = 0, nDnFull = 0, nDnStarv = 0, nPart = 0, nBand = 0, nNoT = 0, nNoData = 0, nCap = 0, nBusy = 0;
            int nColl = 0;                                         // f7-wegiel (recenzja): wpisy bez wzrostu tylko przez rece weglarzy (bez drewna < 10%, z weglarzami >= 10%)
            var nLoc = new int[2]; var nLocUp = new int[2]; var nLocBand = new int[2];   // 286: w dol przy miescie targowym pelnym (w tym zamiast wzrostu przy BRAK, w pasmie)
            var nLocHold = new int[2]; var nLocRec = new int[2];   // 286b: trzymane przy BRAK (miasto pelne), w gore w pasmie (miasto ponizej celu)
            int nBlk = 0, nAtCap0 = 0, nOver = 0;                  // recenzja: bloki z zelazem, na suficie przed krokiem, nad sufitem bez wzrostu zelaza (bez przyciecia)
            var h0 = new double[2]; var h1 = new double[2]; var hMid = new double[2]; var hAt0 = new double[2];
            double capIron = 0, capOther = 0;                      // rece zdjete sufitem po wzroscie: zelazu (z zamiaru) i innym zlozom bloku (oddane zelazu)
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.St == null || ld.K == null || ld.B == null) continue;
                if (LandBackOf(ld.St)) continue;                     // ks-arm: kraina wrocona - bez regali
                try
                {
                    int m = ld.Market == 0 ? 0 : 1;
                    // pola mieszczan: zelazo bez dymarki (bez wegla) - bez wzrostu (jak brak danych wytopu), w dol tylko przy rynku pelnym; fin88: zelaza z pol mieszczan 0
                    float starv = -1f, model7; int days;
                    if (!ld.TF && ld.V != null) starv = Bloomery.F5Starved(ld.V, out model7, out days);
                    // f7-wegiel (recenzja): do WZROSTU rece weglarzy licza sie jak brak drewna (kopalnia w duzej czesci weglarnia nie rosnie z rekami); zjazd przy >= 50% - samo starv.
                    // Klucz Bloomery Colliers NIE (rece weglarzy 0) - starvG == starv co do bitu
                    float starvG = starv;
                    if (starv >= 0f && ld.V != null) starvG = Bloomery.F7StarvedGrow(ld.V, starv);
                    // 286: miasto targowe kopalni (pola mieszczan - ich miasto; wies - TradeBound, inaczej Bound - jak RegaleCycle)
                    bool locFull = false, locShort = false, mtKnown = false;
                    if (locOn)   // 287: miasto targowe potrzebne takze bez miast pelnych i z brakiem (rynek PELNY - kopalnia przy miescie bez nadmiaru trzyma rece)
                    {
                        Town mt = ld.TF ? ld.T : (ld.V != null && ld.V.TradeBound != null ? ld.V.TradeBound.Town : (ld.V != null && ld.V.Bound != null ? ld.V.Bound.Town : null));
                        mtKnown = mt != null;
                        locFull = mt != null && fullTown.Contains(mt);
                        locShort = mt != null && shortTown.Contains(mt);
                    }
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.RC == null || b.RF == null) continue;
                        bool has = false;
                        for (int j = 0; j < b.RC.Length; j++) if (b.RC[j] == KsTab.CIron) { has = true; break; }
                        if (!has) continue;
                        if (b.Target >= 0) { nBusy++; continue; }       // blok w przebudowie A7 - RegaleCycle tez go pomija
                        nBlk++;
                        double sum = 0, sum0 = 0, ironB0 = 0, ironMid = 0;
                        bool raised = false;
                        for (int j = 0; j < b.RC.Length && j < b.RF.Length; j++)
                        {
                            sum0 += b.RF[j];
                            if (b.RC[j] == KsTab.CIron)
                            {
                                nIn++;
                                h0[m] += b.Hands * b.RF[j]; ironB0 += b.Hands * b.RF[j];
                                float f = 1f;
                                if (starv >= F5StarvDown) { f = F5Down; nDnStarv++; }
                                else if (dec[m] == 2) nNoT++;
                                else if (act[m] != 2)
                                {
                                    // 290: ruch rynku za zapasem swiata (opis wyzej) - osobne kubly; reguly 282-287 tego rynku nie biora udzialu
                                    if (act[m] == -1)
                                    {
                                        if (bandOn && locShort) h290[m]++;
                                        else if (fCutOn && lv[m] == -1 && fullOwnOn && mtKnown && !locFull) { h290[m]++; fHold[m]++; }   // 294c (F): PELNY - miasto bez nadmiaru trzyma
                                        else { f = F5Down; d290[m]++; if (fCutOn && lv[m] == -1) fDn[m]++; }
                                    }
                                    else if (act[m] == 1)
                                    {
                                        if (locFull || starv == -1f || !(starvG < F5StarvUp)) h290[m]++;
                                        else { f = F5Up; u290[m]++; }
                                    }
                                    else h290[m]++;
                                }
                                else if (bandOn && locFull && dec[m] == 1) nLocHold[m]++;   // 286b: rynek w braku - kopalnia przy miescie pelnym trzyma rece (nigdy w dol)
                                else if (bandOn && locShort && dec[m] == 0)
                                {
                                    // 286b: pasmo, miasto targowe kopalni ponizej celu - powrot jak przy braku (te same warunki drewna; bez danych / czesciowo - jak dotad)
                                    if (starv == -1f) nNoData++;
                                    else if (starvG < F5StarvUp) { f = F5Up; nLocRec[m]++; }
                                    else { nPart++; if (starv < F5StarvUp) nColl++; }
                                }
                                else if (locFull && dec[m] != -1)
                                {
                                    // 286: rynek w braku albo w pasmie, ale miasto targowe tej kopalni zalane - rece w dol (wydobycie tu tylko zalewa zrodlo; brak rynku
                                    // dopelniaja kopalnie przy miastach bez nadmiaru i wozy ze zrodel)
                                    f = F5Down; nLoc[m]++;
                                    if (dec[m] == 1) nLocUp[m]++; else nLocBand[m]++;
                                }
                                else if (fullOwnOn && dec[m] == -1 && mtKnown && !locFull) nFullHold[m]++;   // 287: rynek pelny, wlasne miasto bez nadmiaru - trzyma rece (miasto pelne - w dol nizej)
                                else if (dec[m] == -1) { f = F5Down; nDnFull++; }
                                else if (dec[m] == 1)
                                {
                                    if (starv == -1f) nNoData++;
                                    else if (starvG < F5StarvUp) { f = F5Up; nUp++; }   // -2 (bez dymarki) albo < 10% bez drewna (f7: z rekami weglarzy)
                                    else { nPart++; if (starv < F5StarvUp) nColl++; }
                                }
                                else nBand++;
                                if (f != 1f) b.RF[j] *= f;
                                if (f > 1f) raised = true;
                                ironMid += b.Hands * b.RF[j];
                            }
                            sum += b.RF[j];
                        }
                        hMid[m] += ironMid;
                        double cap = 0.5 * Math.Max(0.0, 1.0 - b.HomeFrac);   // ten sam sufit co RegaleCycle
                        if (cap > 0 && sum0 >= cap * 0.999) { nAtCap0++; hAt0[m] += ironB0; }
                        // recenzja: sufit tylko po wzroscie zelaza w tym bloku i najwyzej do sumy sprzed kroku (lim = max(sufit, suma przed krokiem)) - krok nie robi pracy
                        // RegaleCycle (przyciecie bloku, ktory wyszedl nad sufit, bo urosla praca domowa) i wzrost nigdy nie obniza zelaza; rece innych zloz oddane zelazu osobno
                        double lim = Math.Max(cap, sum0);
                        if (raised && sum > lim && sum > 0)
                        {
                            float k = (float)(lim / sum);
                            double other0 = Math.Max(0.0, sum * b.Hands - ironMid);   // rece innych zloz bloku przed sufitem
                            for (int j = 0; j < b.RF.Length; j++) b.RF[j] *= k;
                            nCap++;
                            capOther += other0 * (1.0 - k);
                        }
                        else if (!raised && sum > cap) nOver++;            // nad sufitem bez wzrostu zelaza - bez przyciecia (RegaleCycle co 28 dob jak dotad)
                        double ironB1 = 0;
                        for (int j = 0; j < b.RC.Length && j < b.RF.Length; j++) if (b.RC[j] == KsTab.CIron) ironB1 += b.Hands * b.RF[j];
                        h1[m] += ironB1;
                        if (ironMid > ironB1) capIron += ironMid - ironB1;
                    }
                }
                catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(okreg)", e); }
            }
            // 3. linia: stawka KS zelaza na jutro (wsie - policzona przed decyzja, prog trendu) i wytop 7 dob wedlug rynku
            var wm = new double[2]; var wu = new double[2]; var ok = new int[2]; var part = new int[2]; var bad = new int[2];
            try { Bloomery.F5Sums(wm, wu, ok, part, bad); } catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(wytop)", e); }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            string[] mn = { "Westeros", "Essos" };
            var sb = new StringBuilder(1400);
            sb.Append("Ruda KS (f5-ruda): dzien ").Append(today).Append(" | klucz Ks Iron Hands Follow Market TAK, co ").Append(F5IronEvery).Append(" dob od D ").Append(d0)
              .Append(" (rece przy zlozach zelaza za ruda rynku; kopalnia z drewnem x").Append(F5Up.ToString("0.00", inv)).Append(" przy braku, x").Append(F5Down.ToString("0.00", inv))
              .Append(" przy rynku pelnym albo dymarce bez drewna >= ").Append((F5StarvDown * 100).ToString("0", inv)).Append("%)");
            for (int m = 0; m < 2; m++)
            {
                sb.Append(" | ").Append(mn[m]).Append(": polki ").Append(S[m]).Append(" / cel ").Append(T[m])
                  .Append(" (").Append(T[m] > 0 ? ((double)S[m] / T[m]).ToString("0.00", inv) : "-").Append("), brak ").Append(D[m]).Append(" w ").Append(nS[m])
                  .Append(" miastach, bez rudy ").Append(nE[m]).Append(" z ").Append(nT[m]).Append(" miast -> ")
                  .Append(dec[m] == 1 ? (trend[m] == 1 ? "BRAK (w gore, zapas spada - 285)" : "BRAK (w gore)") : dec[m] == -1 ? (trend[m] == -1 ? "PELNY (w dol, zapas rosnie - 285)" : "PELNY (w dol)") : dec[m] == 0 ? "PASMO (bez zmian)" : "BEZ CELU (bez zmian)")
                  .Append(", w drodze do miast ").Append(R[m]).Append(" (polki z droga ").Append(T[m] > 0 ? ((double)(S[m] + R[m]) / T[m]).ToString("0.00", inv) : "-")
                  .Append(rOn ? ", 282 - liczone)" : ", 282 - klucz NIE, nie liczone)")
                  .Append("; 285: pelny z droga ").Append(fullR ? "TAK" : "NIE").Append(", zapas rynku (polki + droga + juki karawan) ")
                  .Append(prevTot[m] >= 0 ? prevTot[m].ToString(inv) : "-").Append(" -> ").Append(tot[m])
                  .Append(prevTot[m] >= 0 ? " (" + (tot[m] - prevTot[m] >= 0 ? "+" : "") + (tot[m] - prevTot[m]).ToString(inv) + ")" : "")
                  .Append(", juki karawan ").Append(C[m]).Append(", trend ").Append(trend[m] == -1 ? "W DOL" : trend[m] == 1 ? "W GORE" : trendOn ? "-" : "NIE (klucz)")
                  .Append(", prog trendu ").Append(thr[m]);
                if (locOn) sb.Append("; 286: miasta targowe pelne (polki + droga > 2 x cel) ").Append(nFullT[m]).Append(", kopalnie przy nich w dol ").Append(nLoc[m])
                  .Append(" (zamiast wzrostu przy BRAK ").Append(nLocUp[m]).Append(", w pasmie ").Append(nLocBand[m]).Append(')');
                if (bandOn) sb.Append("; 286b: pas martwy tydzien wydobycia kopalni miasta TAK, trzymane przy BRAK ").Append(nLocHold[m])
                  .Append(", miasta targowe z brakiem (polki + droga < cel) ").Append(nShortT[m]).Append(", kopalnie przy nich w gore (pasmo) ").Append(nLocRec[m]);
                if (fullOwnOn) sb.Append("; 287: przy rynku PELNYM w dol tylko kopalnie miast pelnych TAK, trzymane przy PELNYM (miasto bez nadmiaru) ").Append(nFullHold[m]);
                if (flowOn)
                    sb.Append("; 290: zapas swiata rynku (polki + droga + juki + wsie + wozy wsi + zamki) ").Append(wPrev[m] >= 0 ? wPrev[m].ToString(inv) : "-").Append(" -> ")
                      .Append(flowOk ? wTot[m].ToString(inv) : "-")
                      .Append(flowOk && wPrev[m] >= 0 ? " (" + (wTot[m] - wPrev[m] >= 0 ? "+" : "") + (wTot[m] - wPrev[m]).ToString(inv) + ")" : "")
                      .Append(", wsie ").Append(wV[m]).Append(", wozy wsi ").Append(wP[m]).Append(", zamki ").Append(wC[m]).Append(", prog ").Append(thr[m])
                      .Append(", poziom ").Append(lv[m] == 1 ? "BRAK" : lv[m] == -1 ? "PELNY" : lv[m] == 0 ? "PASMO" : "BEZ CELU")
                      .Append(", ruch ").Append(act[m] == 1 ? "W GORE" : act[m] == -1 ? "W DOL" : act[m] == 0 ? "TRZYMANE" : act[m] == 3 ? "BEZ POMIARU (trzymane)" : "REGULY 282-287")
                      .Append(", w gore ").Append(u290[m]).Append(", w dol ").Append(d290[m]).Append(", trzymane ").Append(h290[m])
                      .Append("; 294c: PELNY w dol tylko przy dW > prog ").Append(fCutOn ? "TAK" : "NIE").Append(", kopalnie miast pelnych w dol ").Append(fDn[m])
                      .Append(", trzymane (miasto bez nadmiaru) ").Append(fHold[m]);
            }
            sb.Append(" | wpisow zelaza ").Append(nIn).Append(": w gore ").Append(nUp).Append(", w dol (rynek pelny) ").Append(nDnFull).Append(", w dol (dymarka bez drewna) ").Append(nDnStarv)
              .Append(", bez zmian: dymarka czesciowo bez drewna ").Append(nPart).Append(Bloomery.F7On ? " (w tym przez rece weglarzy f7-wegiel " + nColl + ")" : "").Append(", bez danych wytopu ").Append(nNoData).Append(", pasmo ").Append(nBand).Append(", bez celu ").Append(nNoT)
              .Append("; bloki przyciete sufitem ").Append(nCap).Append(", w przebudowie (pominiete) ").Append(nBusy);
            // recenzja: czy sufit bloku (suma rak przy zlozach <= 0.5 x rece poza domem) wiaze zelazo
            double hAll0 = h0[0] + h0[1], hAt = hAt0[0] + hAt0[1];
            sb.Append(" | sufit bloku (suma rak przy zlozach <= 0.5 x rece poza domem): blokow z zelazem ").Append(nBlk).Append(", na suficie przed krokiem ").Append(nAtCap0)
              .Append(" (rece przy zelazie ").Append(hAt.ToString("0", inv)).Append(" = ").Append(hAll0 > 0 ? (100.0 * hAt / hAll0).ToString("0", inv) : "0").Append("% rak zelaza)")
              .Append(", przyciete po wzroscie ").Append(nCap).Append(" (zelazo -").Append(capIron.ToString("0", inv)).Append(" rak z zamiaru, inne zloza -").Append(capOther.ToString("0", inv))
              .Append(" rak -> do zelaza), nad sufitem bez wzrostu (bez przyciecia - RegaleCycle co 28 dob) ").Append(nOver);
            sb.Append(" | rece przy zelazie: Westeros ").Append(h0[0].ToString("0", inv)).Append(" -> ").Append(h1[0].ToString("0", inv))
              .Append(", Essos ").Append(h0[1].ToString("0", inv)).Append(" -> ").Append(h1[1].ToString("0", inv))
              .Append(" (razem ").Append(Pct5(h0[0] + h0[1], h1[0] + h1[1])).Append("; zamiar po mnoznikach ").Append(Pct5(h0[0] + h0[1], hMid[0] + hMid[1])).Append(")");
            sb.Append(" | stawka KS zelaza na jutro (wsie, szt./d): Westeros ").Append(rate[0].ToString("0", inv)).Append(" w ").Append(rateN[0]).Append(" wsiach, Essos ")
              .Append(rate[1].ToString("0", inv)).Append(" w ").Append(rateN[1]).Append(" wsiach");
            sb.Append(" | wytop 7 dob (dymarki): Westeros model ").Append(wm[0].ToString("0", inv)).Append(", bez drewna ").Append(wu[0].ToString("0", inv))
              .Append(" (").Append(wm[0] > 0 ? (100.0 * wu[0] / wm[0]).ToString("0", inv) : "0").Append("%), Essos model ").Append(wm[1].ToString("0", inv)).Append(", bez drewna ")
              .Append(wu[1].ToString("0", inv)).Append(" (").Append(wm[1] > 0 ? (100.0 * wu[1] / wm[1]).ToString("0", inv) : "0").Append("%); kopalnie z drewnem ")
              .Append(ok[0] + ok[1]).Append(", czesciowo ").Append(part[0] + part[1]).Append(", bez drewna ").Append(bad[0] + bad[1]);
            sb.Append(" | pieniadze i towar: bez przelewow - zmiana tylko podzialu rak bloku (rece zelaza + rece reszty zajec = rece bloku)")
              .Append(" | czas ").Append(ms.ToString("0.0", inv)).Append(" ms | potkniecia ").Append(_f5Stum).Append('.');
            Log.Info(sb.ToString());
            _f5Stum = 0;
        }

        private static string Pct5(double a, double b)
        {
            if (!(a > 0)) return "-";
            double p = 100.0 * (b - a) / a;
            return (p >= 0 ? "+" : "") + p.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }
    }
}
