using System;
using System.Globalization;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// m96-len (20.10, PLAN 6 "skor i plotna w swiecie nie ubywa szybciej niz 10 dziennie") - PLAN KS LICZY PLOTNO I SUKNO, KTORE WSIE NAPRAWDE KUPUJA W MIASTACH.
    ///
    /// Dowod (4 biegi 160 dob z zapisu autotest-F-d240: WGRANIE 91 00-44-29, 92 04-01-39, 95-fala1 09-14-49, 95 12-25-19; ksiega towarow "Towary: ... flax / linen / felt / wool"):
    ///  - KS wlacza sie w dobie ok. 37 biegu (109111/2), a w dobach D..D+14 (109126-109140) plon lnu wsi spada z ok. 1000 do ok. 150 szt./d (+ ok. 125 z pol mieszczan)
    ///    we WSZYSTKICH czterech biegach; od tej pory len: produkcja 280 szt./d wobec zuzycia 454 / 415 / 366 / 341 / 322 (okna 20 dob) - zapas lnu swiata
    ///    12 181 -> 3 140, plotno: produkcja 345 / 310 / 281 / 266 / 253 wobec zuzycia 411 / 347 / 308 / 266 / 262 - zapas 8 657 -> 5 492;
    ///    sukno tak samo: zapas (okna 40 dob) 5 826 -> 4 499 -> 3 120 -> 2 427 (12-25-19; w trzech pozostalych biegach 5 383-5 731 -> 2 337-2 707), produkcja 174-189
    ///    wobec zuzycia 191-212, welna 382 -> 261 szt./d (zuzycie = produkcja); w nowej kampanii bez KS (120 dob, 08-45-54) len 720-1130 szt./d, zapas lnu 6 809 -> 11 310,
    ///    plotna 1 887 -> 3 841, sukna 2 234 -> 2 936 - spadek jest skutkiem KS;
    ///  - rece zajec pod KS stoja na Planie wlaczenia (A7 przesuwa je tylko za cena cienia Planu) - a Plan chce na targ ok. 284 szt. lnu/d (153 wsie + 131 pola)
    ///    i 227 welny (213 + 14), bo jego popyt na len = 1.25 x popyt na plotno, na welne = 1.45 x popyt na sukno (receptura KS);
    ///  - w popycie Planu na plotno i sukno sa: ludzie (koszyk zaplacony), odziez wojska, warsztaty zbrojne, naprawy, dwory. NIE MA plotna i sukna, ktore wsie kupuja
    ///    w miastach (ramka E5/B1: plotno 143 / 84 / 72 / 91 / 77 / 61 / 36 / 36 szt./d w oknach 20 dob; sukno 1-9 przed KS, 36-61 po KS) ponad koszyk zaplacony Planu -
    ///    Plan liczy niezaplacony koszyk wsi (plotno 116 W + 69 E, sukno 83 W + 45 E szt./d) jako "robiony w domu" z lnu i welny (KsBasket.RawGood), a wsie w grze
    ///    kupuja to plotno i sukno z kiesy w miescie targowym. Skora (tez ostatni szczebel koszyka) - bez tego obrazu: zapas skory 2 568 -> 2 378 (prawie staly),
    ///    produkcja = zuzycie, skory surowe rosna 4 964 -> 17 240 - poza regula.
    ///
    /// REGULA (klucz Ks Village Cloth Buys In Plan, domyslnie TAK): przy budowie wejscia Planu (watek glowny, co 7 dob) popyt 1:1 plotna i sukna dostaje
    ///   max(0, srednia zakupow towaru przez wsie w miastach (ksiega towarow, ramka E5/B1, srednia z najwyzej 28 dob; pierwsze 2 doby sesji pominiete - po wczytaniu
    ///          zapisu zakupy wsi skacza: plotno 433 i 245 wobec 120-150, we wszystkich 4 biegach; od 5 dob pomiaru)
    ///          - koszyk wsi tego towaru zaplacony w poprzednim Planie (KsMoney.PaidV = paid x q, kg/d w ostatniej rundzie kroku 8 -> szt. gry/d jak "koszyk niezaplacony"))
    /// - czesc zaplacona koszyka (juz w popycie Planu przez potrzeby ludzi) nie wchodzi drugi raz. Popyt Planu na len / welne rosnie o 1.25 / 1.45 x tyle (KsCore.Demand:
    /// D[len] = (potrzeby + 1:1 plotna) x LinenFlax, D[welna] = (potrzeby + 1:1 sukna) x FeltWool) - Plan daje rekom wsi wiecej lnu i owiec przy wlaczeniu, a po
    /// wlaczeniu wyzsza cena cienia lnu i welny idzie do sygnalu A7 (Ks Switch Signal Rule). Bez mnoznika plonu: wiecej lnu = wiecej rak przy lnie (te same tabele
    /// swiata i ziemia). Pieniadze i towar: bez przelewow (zmienia sie tylko liczba popytu w Planie). Zapis bez zmian (srednia od zera w kazdej sesji, nic w SyncData).
    /// Uwaga (recenzja, drobne): kupiona czesc zostaje tez w niezaplaconym koszyku kroku 8 (u[z] - surowiec pracy domowej okregu); to stara strata rak pracy domowej
    /// (rece "w domu" w grze nic nie tkaja), ok. 0.1% popytu swiata - bez zmiany.
    /// Klucz NIE - dopisek 0, Plan co do bitu jak przed paczka. Linia "Len KS (m96-len): plan doba N" przy kazdym Planie (tools/sprawdz_logi.py --grupa m96len).
    /// </summary>
    internal static partial class StartCalibration
    {
        // towary koszyka wsi z praca domowa w Planie (KsBasket.RawGood: plotno - len, sukno - welna), ktore wsie w grze kupuja w miastach
        private static readonly string[] M96Ids = { "linen", "felt" };
        private static readonly string[] M96Nm = { "plotno", "sukno" };
        private static readonly string[] M96RawNm = { "lnu", "welny" };
        private static readonly int[] M96Z = { KsBasket.Linen, KsBasket.Felt };
        private static readonly int[] M96RawZ = { KsBasket.Flax, KsBasket.Wool };
        private static readonly double[] M96Ratio = { KsTab.LinenFlax, KsTab.FeltWool };
        private const int M96Skip = 2, M96MinDays = 5, M96EmaDays = 28;
        private static readonly double[] _m96Ema = new double[2];          // srednia zakupow przez wsie w miastach (szt./d)
        private static readonly int[] _m96Seen = new int[2], _m96Days = new int[2], _m96LastDay = { int.MinValue, int.MinValue };
        private static readonly long[] _m96Today = new long[2];            // ostatnia doba ksiegi (szt.)
        private static int _m96Stum, _m96StumAll;

        internal static bool M96LenOn { get { var s = Settings.Current; return s == null || s.KsVillageClothBuysInPlan; } }

        private static void M96LenReset()
        {
            for (int g = 0; g < 2; g++) { _m96Ema[g] = 0; _m96Seen[g] = 0; _m96Days[g] = 0; _m96LastDay[g] = int.MinValue; _m96Today[g] = 0; }
            _m96Stum = 0;
        }

        /// <summary>GoodsLedger.KsNote (watek glowny, raz na dobe na towar): zakupy plotna / sukna przez wsie w miastach (ramka E5/B1) tej doby. Nie rzuca.</summary>
        internal static void M96LenLedger(string id, int day, long vilBuy)
        {
            try
            {
                int g = id == "linen" ? 0 : id == "felt" ? 1 : -1;
                if (g < 0 || day < 0 || day == _m96LastDay[g]) return;
                if (_m96LastDay[g] != int.MinValue && day < _m96LastDay[g]) M96LenReset();   // inna kampania / wczytanie wczesniejszej doby - od zera
                _m96LastDay[g] = day; _m96Today[g] = Math.Max(0, vilBuy);
                if (++_m96Seen[g] <= M96Skip) return;                     // pierwsze 2 doby sesji: zakupy wsi po wczytaniu skacza (433, 245) - poza srednia
                _m96Days[g]++;
                double w = 1.0 / Math.Min(M96EmaDays, _m96Days[g]);
                _m96Ema[g] += (_m96Today[g] - _m96Ema[g]) * w;
            }
            catch (Exception e) { _m96Stum++; _m96StumAll++; Stumble("M96LenLedger", e); }
        }

        /// <summary>BuildInput (watek glowny): dopisek popytu 1:1 plotna i sukna w wejsciu Planu i linia logu. Nie rzuca.</summary>
        private static void M96LenApply(KsInput inp, int day)
        {
            try
            {
                if (inp == null) return;
                bool on = M96LenOn;
                var pr = _plan; var mo = pr != null ? pr.Money : null;
                bool havePaid = mo != null && mo.PaidV != null && mo.PaidV.Length >= 2 * KsBasket.N;
                double sigma = Math.Max(1.0, inp.Sigma), sigmaPrev = _planIn != null && _planIn.Sigma >= 1.0 ? _planIn.Sigma : sigma;
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(700);
                sb.Append("Len KS (m96-len): plan doba ").Append(day).Append(" | klucz Ks Village Cloth Buys In Plan ").Append(on ? "TAK" : "NIE");
                for (int g = 0; g < 2; g++)
                {
                    KsItem it; inp.Items.TryGetValue(M96Ids[g], out it);
                    double paid = -1, extra = 0;
                    string why;
                    if (!on) why = "klucz NIE - Plan jak przed paczka";
                    else if (it == null) why = "brak towaru w wejsciu Planu";
                    else if (_m96Days[g] < M96MinDays) why = "pomiar " + _m96Days[g] + " dob (< " + M96MinDays + ", pierwsze " + M96Skip + " doby sesji pominiete) - bez dopisku";
                    else if (!havePaid) why = "brak poprzedniego Planu (koszyk zaplacony nieznany) - bez dopisku";
                    else
                    {
                        paid = PaidVillagePcs(mo, M96Z[g], sigmaPrev);
                        extra = Math.Max(0.0, _m96Ema[g] - paid);
                        why = extra > 0 ? "dopisane" : "zakupy wsi w koszyku zaplaconym Planu - bez dopisku";
                    }
                    if (it != null) it.VilBuy = on ? extra : 0.0;
                    double kg = it != null && it.Kg > 0 ? it.Kg : 10.0, rk = ZKg(M96RawZ[g]); if (!(rk > 0)) rk = 10.0;
                    double rawT = extra * sigma * kg * M96Ratio[g] / 1000.0, rawPcs = extra * kg * M96Ratio[g] / rk;
                    sb.Append(" | ").Append(M96Nm[g]).Append(" (").Append(M96Ids[g]).Append("): zakupy wsi w miastach (E5/B1) srednia ").Append(_m96Ema[g].ToString("0.0", inv))
                      .Append(" szt./d z ").Append(Math.Min(_m96Days[g], M96EmaDays)).Append(" dob (ostatnia doba ").Append(_m96Today[g]).Append(")")
                      .Append(", koszyk wsi zaplacony w poprzednim Planie ").Append(paid >= 0 ? paid.ToString("0.0", inv) : "-").Append(" szt./d")
                      .Append(", dopisek do popytu 1:1 ").Append(extra.ToString("0.0", inv)).Append(" szt./d (").Append(M96RawNm[g]).Append(" x").Append(M96Ratio[g].ToString("0.00", inv))
                      .Append(" = ").Append(rawPcs.ToString("0.0", inv)).Append(" szt./d, ").Append(rawT.ToString("0.0", inv)).Append(" t/d w Planie) - ").Append(why);
                }
                sb.Append(" | pieniadze i towar: bez przelewow | potkniecia ").Append(_m96Stum).Append(" (sesja ").Append(_m96StumAll).Append(").");
                Log.Info(sb.ToString());
                _m96Stum = 0;
            }
            catch (Exception e) { _m96Stum++; _m96StumAll++; Stumble("M96LenApply", e); }
        }

        /// <summary>Koszyk wsi towaru z (KsBasket) zaplacony w ostatnim gotowym Planie: KsMoney.PaidV (kg/d, oba rynki) / (sigma x kg sztuki) - szt. gry/d,
        /// ta sama miara co linia "KS: koszyk niezaplacony".</summary>
        private static double PaidVillagePcs(KsMoney mo, int z, double sigma)
        {
            double kg = Math.Max(0.0, mo.PaidV[z]) + Math.Max(0.0, mo.PaidV[KsBasket.N + z]);
            double pk = ZKg(z);
            return kg / (Math.Max(1.0, sigma) * (pk > 0 ? pk : 10.0));
        }
    }
}
