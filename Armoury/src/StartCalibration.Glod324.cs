using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 324 GLOD ZIMY (Jeff 03.11 "na wsiach tez nie ma zboza, praktycznie nie ma produkcji innych surowcow"; noc/324-glod). Rynki zbankrutowaly w pierwszej dobie zimy
    /// Westeros (109178, zima ok. 1794 dni): koszyk ZC4 wsi = pelna potrzeba - Own, a Own = min(plon po zimie, potrzeba) - zima tnie plon o polowe, wies rusza po
    /// zboze do miast (10-11k szt./d przy produkcji swiata 3.9k), polki miast schodza ze 103k do 2.8k w 15 dob, a w stodolach lezy nietkniete 628 dni zapasu
    /// (Famine E4/6); 322c dawal z tego 1.4k szt./d (rezerwa stala 455 dni / 1720 dni zimy). Teraz jeden BILANS ZIMY WSI (bez nowych stalych):
    ///   left - dni do konca zimy (WesterosClimate.SeasonInfo, jak 322c); def - brak wlasnego stolu (racje/d): wies KS - kcal koszyka, ktorego dom nie robi
    ///   (pelny koszyk x (1 - czesc robiona w domu z Planu)) / kcal racji okregu; wies poza KS (kraina wrocona, Essos) - max(0, potrzeba - plon Famine) (plon bez
    ///   zakupow - bez petli zakupy-bilans); floor - prog racjonowania Famine (FamineRationDays x ludzie w domu).
    ///  1. WIES JE NAJPIERW ZE STODOLY (klucz Winter Villages Eat Own Granary): zima kcal koszyka jedzenia wsi KS zmniejszone o to, co wies moze dzis zjesc ze swojego
    ///     spichlerza, rozkladajac zapas ponad floor na reszte zimy: pokrycie = min(def, (zapas - floor) / left) racji (x kcal racji); zboze pierwsze, potem reszta
    ///     jedzenia wedlug drabiny koszyka (KsBasket.Rungs 0-3, towary z kcal). Wies kupuje w miescie tylko to, czego stodola nie pokryje; racje KS (GranaryFloor)
    ///     wychodza nizsze, a Famine.Daily sam schodzi z zapasu o ten brak - nic nie jest zdejmowane recznie (ekonomia zamknieta). Jedzenie robione w domu
    ///     (HomeKcal) liczone z czesci pelnego koszyka, ktorej nie kupiono i nie pokrywa stodola (te same kcal nie sa liczone dwa razy). Sol i reszta towarow bez zmian.
    ///     Okreg przeliczany co 7 dob (LiveEvery) - pokrycie tez (zmiana pory dochodzi do wsi najpozniej w 7 dob).
    ///  2. 322c Z BILANSU (klucz Winter Grain From Winter Balance, TownGranary.Zima322.cs): wies sprzedaje na polke tylko wolne = max(0, zapas - floor - left x def) /
    ///     left racji na dobe (zamiast zapasu ponad stala rezerwe 455 dni / max(91, left)).
    ///  3. 322c DLA CALEGO SWIATA (klucz Winter Grain All Towns): miasto poza ZC4 (krainy wrocone, bez popytu zaplaconego KS) - popyt dobowy zboza z modelu jedzenia
    ///     miasta w grze (spozycie Food jak p6: dobrobyt / 40 + zaloga / 20), cel polki 2 doby; wies bez rachunku KS - kcal racji sredni swiata (wsie KS).
    ///  4. BRAMKA KRAINY ZIMA (klucz Winter Land Gate Planned Draw, StartCalibration.Arm.cs): zaplanowane zejscie stodoly (pokrycie koszyka z pkt 1) nie jest spadkiem,
    ///     a zamiast progu zapasu Ks Land Gate Reserve Days i budzetu straty kraina liczy serie tylko przy zapasie ponizej sumy floor + left x def (wsie nie dotrwaja
    ///     do wiosny). Poza zima - jak dotad.
    /// Linia "Glod zimy (324): dzien N" (co dobe przy LogEnabled): koszyk wsi z targu i ze stodoly, bilans zimy, dowoz 322c na polki, bramka. Klucze wylaczone - jak dotad.
    /// Stan bez zapisu (pola okregu LiveDist z przeliczenia). Wyjatki per wies liczone (bez globalnych wylacznikow, CLAUDE.md 7).
    /// </summary>
    internal static partial class StartCalibration
    {
        private static bool Sw324Barn { get { var s = Settings.Current; return s != null && s.WinterVillagesEatOwnGranary; } }
        private static bool Sw324Gate { get { var s = Settings.Current; return s != null && s.WinterLandGatePlannedDraw; } }
        private static double _w324GatePlan;   // racje zaplanowanego zejscia stodol odjete dzis od spadku w bramce krainy (LandGateDaily tej doby)
        private static int _w324GateLands;     // krainy, w ktorych dzis zamiast progu zapasu liczyl bilans zimy
        private static int _w324Stum;
        private static int _w324KprDay = int.MinValue; private static double _w324Kpr = double.NaN;

        /// <summary>324: dni do konca zimy Westeros (jak 322c); 0 - poza zima albo klimat bez stanu. Nie rzuca.</summary>
        internal static int WinterLeft324()
        {
            try
            {
                if (!WinterBite.WinterNow()) return 0;
                int season, sday, len;
                if (WesterosClimate.SeasonInfo(out season, out sday, out len) && season == (int)CampaignTime.Seasons.Winter && len > 0) return Math.Max(0, len - sday + 1);
            }
            catch { }
            return 0;
        }

        /// <summary>324: brak wlasnego stolu wsi KS (racje/d) z ostatniego rachunku okregu; NaN - stan nie zastosowany, wies poza KS, kraina wrocona albo brak rachunku.
        /// Tylko watek glowny. Nie rzuca.</summary>
        internal static double WinterDeficit324(Village v)
        {
            try
            {
                if (v == null || v.Settlement == null || v.Settlement.StringId == null || !Applied || LandBackOf(v.Settlement)) return double.NaN;
                LiveDist ld;
                if (!_live.TryGetValue(v.Settlement.StringId, out ld) || ld == null || ld.TF || ld.Res == null) return double.NaN;
                double d = ld.DefR324;
                return d >= 0.0 && !double.IsInfinity(d) ? d : double.NaN;
            }
            catch { return double.NaN; }
        }

        /// <summary>324: bilans zimy wsi z migawki Famine tej doby - zapas (racje teraz), ludzie w domu, brak wlasnego stolu (racje/d; KS z rachunku okregu, poza KS
        /// potrzeba - plon Famine), floor (prog racjonowania Famine). false - spichlerz nieczynny albo bez migawki doby `today`. Nie rzuca.</summary>
        internal static bool WinterBalance324(Village v, int today, out double food, out double home, out double def, out double floor)
        {
            food = home = def = floor = 0.0;
            try
            {
                double harv, f0, cap; float u; int d;
                if (!Famine.DayOf(v, out home, out harv, out f0, out cap, out u, out d) || d != today || !(home > 0.0)) return false;
                if (!Famine.FoodOf(v, out food)) return false;
                double k = WinterDeficit324(v);
                def = double.IsNaN(k) ? Math.Max(0.0, home - harv) : k;
                var s = Settings.Current;
                floor = Math.Max(0, s != null ? s.FamineRationDays : 90) * home;
                return true;
            }
            catch { return false; }
        }

        /// <summary>324 (322c dla calego swiata): sredni kcal racji wsi KS (KcalPerRation322 wszystkich okregow z rachunkiem) - zamiana racji spichlerza wsi bez
        /// rachunku KS na sztuki zboza. Raz na dobe; NaN - brak okregow. Tylko watek glowny. Nie rzuca.</summary>
        internal static double KcalPerRationWorld324(int today)
        {
            if (_w324KprDay == today) return _w324Kpr;
            double sum = 0.0; int n = 0;
            try
            {
                if (Applied)
                    foreach (var ld in _live.Values)
                    {
                        if (ld == null || ld.TF || ld.Res == null || !(ld.PeopleHome > 0)) continue;
                        double k = ld.Res.KcalNeed / ld.PeopleHome;
                        if (k >= 800.0 && k <= 6000.0) { sum += k; n++; }
                    }
            }
            catch { n = 0; }
            _w324KprDay = today; _w324Kpr = n > 0 ? sum / n : double.NaN;
            return _w324Kpr;
        }

        /// <summary>VillageBasket (przeliczenie okregu): brak wlasnego stolu (DefR324) i zima pokrycie koszyka ze stodoly - kg jedzenia zdjete z `kg` (indeksy ZIds), zwraca
        /// zdjete kcal. Poza zima, klucz wylaczony, bez spichlerza - 0 i koszyk bez zmian. Wyjatek leci do wolajacego (koszyk wtedy nietkniety - zmiana na koncu).</summary>
        private static double Barn324(LiveDist ld, DistResult res, double[] kg)
        {
            ld.BarnR324 = ld.BarnKcal324 = ld.BarnGrainKg324 = 0.0;
            ld.DefR324 = double.NaN;
            if (ld.V == null || res == null || !(res.KcalNeed > 0) || !(ld.PeopleHome > 0)) return 0.0;
            double kpr = res.KcalNeed / ld.PeopleHome;                                    // kcal racji okregu (jak KcalPerRation322)
            if (!(kpr > 0) || double.IsInfinity(kpr)) return 0.0;
            double wantF = 0.0;
            for (int z = 0; z < kg.Length && z < KsBasket.Kcal.Length; z++) if (kg[z] > 0) wantF += kg[z] * KsBasket.Kcal[z];
            double frac = Math.Max(0.0, Math.Min(1.0, ld.HomeKcalFrac));
            double defK = Math.Max(0.0, wantF * (1.0 - frac));                           // kcal koszyka, ktorego dom nie robi - to wies musi kupic albo zjesc z zapasu
            ld.DefR324 = defK / kpr;
            if (!Sw324Barn || !(defK > 0)) return 0.0;
            int left = WinterLeft324();
            if (left <= 0) return 0.0;
            if (ld.V.VillageState != Village.VillageStates.Normal) return 0.0;              // spladrowana - jak dotad (najazd i odbudowa)
            double food, home, def, floor;
            if (!WinterBalance324(ld.V, Today(), out food, out home, out def, out floor)) return 0.0;
            double availK = kpr * Math.Max(0.0, food - floor) / left;
            double cover = Math.Min(defK, availK);
            if (!(cover > 1e-6)) return 0.0;
            // zdejmowanie: zboze pierwsze, potem drabina koszyka (napoj, bialko, reszta jedzenia) - tylko towary z kcal
            var take = new double[kg.Length];
            double rest = cover, grainKg = 0.0;
            for (int r = 0; r < 4 && r < KsBasket.Rungs.Length && rest > 1e-9; r++)
                foreach (int z in KsBasket.Rungs[r])
                {
                    if (rest <= 1e-9) break;
                    if (z < 0 || z >= kg.Length || z >= KsBasket.Kcal.Length) continue;
                    double kc = KsBasket.Kcal[z];
                    if (!(kc > 0) || !(kg[z] > 0)) continue;
                    double t = Math.Min(kg[z], rest / kc);
                    take[z] = t; rest -= t * kc;
                    if (z == KsBasket.Grain) grainKg = t;
                }
            double got = cover - Math.Max(0.0, rest);
            if (!(got > 1e-6)) return 0.0;
            for (int z = 0; z < kg.Length; z++) if (take[z] > 0) kg[z] = Math.Max(0.0, kg[z] - take[z]);
            ld.BarnKcal324 = got; ld.BarnR324 = got / kpr; ld.BarnGrainKg324 = grainKg;
            return got;
        }

        /// <summary>LandGateDaily: zaplanowane zejscie stodoly wsi tej doby (racje pokrycia koszyka z ostatniego przeliczenia okregu) - zima i klucz; inaczej 0.</summary>
        private static double GatePlanned324(LiveDist ld, int left)
        {
            if (ld == null || left <= 0 || !Sw324Gate) return 0.0;
            double r = ld.BarnR324;
            return r > 0 && !double.IsInfinity(r) ? r : 0.0;
        }

        /// <summary>Linia "Glod zimy (324): dzien N" - co dobe w stanie zastosowanym (LogEnabled).</summary>
        private static void Line324(int day)
        {
            var s = Settings.Current;
            if (s == null || !s.LogEnabled) { _w324GatePlan = 0.0; _w324GateLands = 0; return; }
            int today = Today(), left = WinterLeft324();
            int nv = 0, full = 0, part = 0, none = 0, atFloor = 0;
            double wantK = 0, coverK = 0, boughtK = 0, coverR = 0, grainKg = 0;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.TF || ld.V == null || ld.Res == null) continue;
                try
                {
                    nv++;
                    wantK += ld.WantKcal + ld.BarnKcal324; coverK += ld.BarnKcal324; coverR += ld.BarnR324; grainKg += ld.BarnGrainKg324;
                    double bk; if (_bKcal.TryGetValue(ld.V, out bk) && bk > 0) boughtK += bk;
                    if (left <= 0) continue;
                    double kpr = ld.PeopleHome > 0 ? ld.Res.KcalNeed / ld.PeopleHome : 0.0;
                    double defK = ld.DefR324 >= 0 && kpr > 0 ? ld.DefR324 * kpr : 0.0;
                    if (!(defK > 1e-6)) continue;
                    if (ld.BarnKcal324 >= defK - 1e-3) full++;
                    else if (ld.BarnKcal324 > 1e-6) part++;
                    else
                    {
                        none++;
                        double food, home, def, floor;
                        if (WinterBalance324(ld.V, today, out food, out home, out def, out floor) && food <= floor) atFloor++;
                    }
                }
                catch (Exception e) { if (++_w324Stum <= 3) Stumble("Line324(wies)", e); }
            }
            // bilans zimy wszystkich wsi ze spichlerzem (KS i poza KS)
            int bn = 0, bKs = 0; double bFood = 0, bDef = 0, bFloor = 0, bFree = 0;
            if (left > 0)
                foreach (var v in Village.All)
                {
                    try
                    {
                        double food, home, def, floor;
                        if (v == null || !WinterBalance324(v, today, out food, out home, out def, out floor)) continue;
                        bn++; if (!double.IsNaN(WinterDeficit324(v))) bKs++;
                        bFood += food; bDef += left * def; bFloor += floor;
                        bFree += Math.Max(0.0, food - floor - left * def) / left;
                    }
                    catch (Exception e) { if (++_w324Stum <= 3) Stumble("Line324(bilans)", e); }
                }
            double sigma = Math.Max(1, DailyLife.KsSigma), gk = 0;
            try { gk = ZKg(KsBasket.Grain); } catch { gk = 0; }
            double kprW = KcalPerRationWorld324(today);
            double freePcs = !double.IsNaN(kprW) && gk > 0 ? bFree * kprW / (3300.0 * sigma * gk) : double.NaN;
            int p322, p322Out, t322Out; TownGranary.Last324(today, out p322, out p322Out, out t322Out);
            var sb = new StringBuilder(1200);
            sb.Append("Glod zimy (324): dzien ").Append(day)
              .Append(" | zima ").Append(left > 0 ? "TAK (do konca " + left + " dni)" : "NIE - koszyk wsi pelny, 322c nic")
              .Append(" | klucze: wies je najpierw ze stodoly ").Append(s.WinterVillagesEatOwnGranary ? "TAK" : "NIE").Append(", 322c z bilansu zimy ").Append(s.WinterGrainFromWinterBalance ? "TAK" : "NIE")
              .Append(", 322c caly swiat ").Append(s.WinterGrainAllTowns ? "TAK" : "NIE").Append(", bramka krainy zima ").Append(s.WinterLandGatePlannedDraw ? "TAK" : "NIE")
              .Append(" | wsie KS ").Append(nv).Append(": koszyk (kcal/d) pelny ").Append(wantK.ToString("0", Inv)).Append(", z targu (kupione, srednia 2 dob) ").Append(boughtK.ToString("0", Inv))
              .Append(" (").Append(wantK > 0 ? (100.0 * boughtK / wantK).ToString("0.0", Inv) : "-").Append("%), z wlasnej stodoly ").Append(coverK.ToString("0", Inv))
              .Append(" (").Append(wantK > 0 ? (100.0 * coverK / wantK).ToString("0.0", Inv) : "-").Append("%; ").Append(coverR.ToString("0", Inv)).Append(" racji/d, zboza nie kupionego w miescie ok. ")
              .Append(gk > 0 ? (grainKg / (sigma * gk)).ToString("0", Inv) : "-").Append(" szt./d)")
              .Append(" | wsi z brakiem stolu: stodola pokrywa calosc ").Append(full).Append(", czesc ").Append(part).Append(", nic ").Append(none).Append(" (w tym na progu racjonowania ").Append(atFloor).Append(')')
              .Append(" | bilans zimy (wsi ze spichlerzem ").Append(bn).Append(", z rachunkiem KS ").Append(bKs).Append("): zapas ").Append((bFood / 1e6).ToString("0.00", Inv)).Append(" mln racji, brak do wiosny ")
              .Append((bDef / 1e6).ToString("0.00", Inv)).Append(" mln, prog racjonowania ").Append((bFloor / 1e6).ToString("0.00", Inv)).Append(" mln, wolne na targ ").Append(bFree.ToString("0", Inv)).Append(" racji/d (ok. ")
              .Append(double.IsNaN(freePcs) ? "-" : freePcs.ToString("0", Inv)).Append(" szt. zboza/d; kcal racji swiata ").Append(double.IsNaN(kprW) ? "-" : kprW.ToString("0", Inv)).Append(')')
              .Append(" | dowoz ze spichlerzy na polki (322c) dzis ").Append(p322).Append(" szt. (miasta poza ZC4 ").Append(t322Out).Append(", ").Append(p322Out).Append(" szt.)")
              .Append(" | bramka krainy: zaplanowane zejscie stodol odjete od spadku ").Append(_w324GatePlan.ToString("0", Inv)).Append(" racji, krain z progiem bilansu zimy ").Append(_w324GateLands)
              .Append(" | potkniecia ").Append(_w324Stum).Append('.');
            Log.Info(sb.ToString());
            _w324GatePlan = 0.0; _w324GateLands = 0; _w324Stum = 0;
        }
    }
}
