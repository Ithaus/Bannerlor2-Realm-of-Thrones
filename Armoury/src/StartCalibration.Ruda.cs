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
        internal static void F5Reset285() { _f5Tot[0] = _f5Tot[1] = -1; }

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
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown || t.Settlement == null || t.Settlement.ItemRoster == null) continue;
                    int m = TownMarket(t); if (m < 0 || m > 1) continue;
                    int have = Math.Max(0, t.Settlement.ItemRoster.GetItemNumber(iron));
                    int tg = Math.Max(0, CaravanBulk.TargetFor(t, iron));
                    S[m] += have; T[m] += tg; nT[m]++;
                    R[m] += Math.Max(0, MaterialOrders.InTransitTo(t.Settlement, iron));
                    if (have <= 0) nE[m]++;
                    if (have < tg) { nS[m]++; D[m] += tg - have; }
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
            // w ostatnich 10 dobach przy PASMIE). Pierwszy krok sesji bez pomiaru - bez trendu.
            var s285 = Settings.Current;
            bool fullR = rOn && s285 != null && s285.KsIronFullCountsTransit, trendOn = s285 != null && s285.KsIronStockTrend;
            var tot = new long[2]; var prevTot = new long[2]; var trend = new bool[2];
            var dec = new int[2];                                  // +1 brak, -1 pelny, 0 pasmo, 2 bez celu (zapas rzemiosla nieznany)
            for (int m = 0; m < 2; m++)
            {
                tot[m] = S[m] + R[m] + C[m]; prevTot[m] = _f5Tot[m];
                dec[m] = T[m] <= 0 ? 2 : S[m] + (rOn ? R[m] : 0) < T[m] ? 1 : S[m] + (fullR ? R[m] : 0) > 2 * T[m] ? -1 : 0;
                if (dec[m] == 0 && trendOn && prevTot[m] >= 0 && tot[m] > prevTot[m]) { dec[m] = -1; trend[m] = true; }
                _f5Tot[m] = tot[m];
            }
            // 2. bloki z zelazem
            int nIn = 0, nUp = 0, nDnFull = 0, nDnStarv = 0, nPart = 0, nBand = 0, nNoT = 0, nNoData = 0, nCap = 0, nBusy = 0;
            int nColl = 0;                                         // f7-wegiel (recenzja): wpisy bez wzrostu tylko przez rece weglarzy (bez drewna < 10%, z weglarzami >= 10%)
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
            // 3. linia: stawka KS zelaza na jutro (wsie) i wytop 7 dob wedlug rynku
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
                  .Append(dec[m] == 1 ? "BRAK (w gore)" : dec[m] == -1 ? (trend[m] ? "PELNY (w dol, zapas rosnie - 285)" : "PELNY (w dol)") : dec[m] == 0 ? "PASMO (bez zmian)" : "BEZ CELU (bez zmian)")
                  .Append(", w drodze do miast ").Append(R[m]).Append(" (polki z droga ").Append(T[m] > 0 ? ((double)(S[m] + R[m]) / T[m]).ToString("0.00", inv) : "-")
                  .Append(rOn ? ", 282 - liczone)" : ", 282 - klucz NIE, nie liczone)")
                  .Append("; 285: pelny z droga ").Append(fullR ? "TAK" : "NIE").Append(", zapas rynku (polki + droga + juki karawan) ")
                  .Append(prevTot[m] >= 0 ? prevTot[m].ToString(inv) : "-").Append(" -> ").Append(tot[m])
                  .Append(prevTot[m] >= 0 ? " (" + (tot[m] - prevTot[m] >= 0 ? "+" : "") + (tot[m] - prevTot[m]).ToString(inv) + ")" : "")
                  .Append(", juki karawan ").Append(C[m]).Append(", trend ").Append(trend[m] ? "W DOL" : trendOn ? "-" : "NIE (klucz)");
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
