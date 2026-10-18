using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
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
    ///   10-50% albo brak danych wytopu - bez zmian. Sufit jak RegaleCycle: suma rak przy zlozach bloku &lt;= polowa rak bloku poza domem. Bez nowych zloz (tylko wpisy
    ///   z Planu wlaczenia). Wpisy zelaza nie biora juz progu indeksu w RegaleCycle (inne zloza bez zmian). Pola mieszczan (zelazo bez dymarki, fin88 0 szt./d) -
    ///   bez wzrostu, w dol tylko przy rynku pelnym; kampania bez wytopu przy kopalni (Bloomery nieczynne) - wsie sam rynek.
    /// Rece przesuwaja sie tylko w bloku (rece zelaza + rece reszty zajec = rece bloku) - zadnego zlota ani towaru (produkcja z prawdziwych rak i ziemi, jedna tabela
    /// swiata). RF jest w zapisie KS (arm_ks, Live.cs) - bez nowego klucza zapisu. Klucz NIE - RegaleCycle jak dotad co do bitu, krok tygodniowy nie biegnie.
    /// Linia "Ruda KS (f5-ruda): dzien N" co 7 dob. Koszt: 97 miast + bloki z zelazem (ok. 550) raz na 7 dob (czas w linii).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const int F5IronEvery = 7;
        private const float F5Up = 1.05f, F5Down = 0.95f;
        private const float F5StarvUp = 0.1f, F5StarvDown = 0.5f;   // wzrost tylko przy dymarce prawie bez brakow drewna (< 10% modelu bez drewna) - 'kopalnia bez wegla' nie rosnie z rekami
        private static int _f5Stum;

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
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown || t.Settlement == null || t.Settlement.ItemRoster == null) continue;
                    int m = TownMarket(t); if (m < 0 || m > 1) continue;
                    int have = Math.Max(0, t.Settlement.ItemRoster.GetItemNumber(iron));
                    int tg = Math.Max(0, CaravanBulk.TargetFor(t, iron));
                    S[m] += have; T[m] += tg; nT[m]++;
                    if (have <= 0) nE[m]++;
                    if (have < tg) { nS[m]++; D[m] += tg - have; }
                }
                catch (Exception e) { _f5Stum++; Stumble("F5IronWeek(miasto)", e); }
            }
            var dec = new int[2];                                  // +1 brak, -1 pelny, 0 pasmo, 2 bez celu (zapas rzemiosla nieznany)
            for (int m = 0; m < 2; m++) dec[m] = T[m] <= 0 ? 2 : S[m] < T[m] ? 1 : S[m] > 2 * T[m] ? -1 : 0;
            // 2. bloki z zelazem
            int nIn = 0, nUp = 0, nDnFull = 0, nDnStarv = 0, nPart = 0, nBand = 0, nNoT = 0, nNoData = 0, nCap = 0, nBusy = 0;
            var h0 = new double[2]; var h1 = new double[2];
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
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.RC == null || b.RF == null) continue;
                        bool has = false;
                        for (int j = 0; j < b.RC.Length; j++) if (b.RC[j] == KsTab.CIron) { has = true; break; }
                        if (!has) continue;
                        if (b.Target >= 0) { nBusy++; continue; }       // blok w przebudowie A7 - RegaleCycle tez go pomija
                        double sum = 0;
                        for (int j = 0; j < b.RC.Length && j < b.RF.Length; j++)
                        {
                            if (b.RC[j] == KsTab.CIron)
                            {
                                nIn++;
                                h0[m] += b.Hands * b.RF[j];
                                float f = 1f;
                                if (starv >= F5StarvDown) { f = F5Down; nDnStarv++; }
                                else if (dec[m] == 2) nNoT++;
                                else if (dec[m] == -1) { f = F5Down; nDnFull++; }
                                else if (dec[m] == 1)
                                {
                                    if (starv == -1f) nNoData++;
                                    else if (starv < F5StarvUp) { f = F5Up; nUp++; }   // -2 (bez dymarki) albo < 10% bez drewna
                                    else nPart++;
                                }
                                else nBand++;
                                if (f != 1f) b.RF[j] *= f;
                            }
                            sum += b.RF[j];
                        }
                        double cap = 0.5 * Math.Max(0.0, 1.0 - b.HomeFrac);   // ten sam sufit co RegaleCycle
                        if (sum > cap && sum > 0) { float k = (float)(cap / sum); for (int j = 0; j < b.RF.Length; j++) b.RF[j] *= k; nCap++; }
                        for (int j = 0; j < b.RC.Length && j < b.RF.Length; j++) if (b.RC[j] == KsTab.CIron) h1[m] += b.Hands * b.RF[j];
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
                  .Append(dec[m] == 1 ? "BRAK (w gore)" : dec[m] == -1 ? "PELNY (w dol)" : dec[m] == 0 ? "PASMO (bez zmian)" : "BEZ CELU (bez zmian)");
            }
            sb.Append(" | wpisow zelaza ").Append(nIn).Append(": w gore ").Append(nUp).Append(", w dol (rynek pelny) ").Append(nDnFull).Append(", w dol (dymarka bez drewna) ").Append(nDnStarv)
              .Append(", bez zmian: dymarka czesciowo bez drewna ").Append(nPart).Append(", bez danych wytopu ").Append(nNoData).Append(", pasmo ").Append(nBand).Append(", bez celu ").Append(nNoT)
              .Append("; bloki przyciete sufitem ").Append(nCap).Append(", w przebudowie (pominiete) ").Append(nBusy);
            sb.Append(" | rece przy zelazie: Westeros ").Append(h0[0].ToString("0", inv)).Append(" -> ").Append(h1[0].ToString("0", inv))
              .Append(", Essos ").Append(h0[1].ToString("0", inv)).Append(" -> ").Append(h1[1].ToString("0", inv))
              .Append(" (razem ").Append(Pct5(h0[0] + h0[1], h1[0] + h1[1])).Append(")");
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
