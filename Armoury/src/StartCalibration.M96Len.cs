using System;
using System.Globalization;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// m96-len (20.10, PLAN 6 "skor i plotna w swiecie nie ubywa szybciej niz 10 dziennie") - PLAN KS LICZY PLOTNO, KTORE WSIE NAPRAWDE KUPUJA W MIASTACH.
    ///
    /// Dowod (4 biegi 160 dob z zapisu autotest-F-d240: WGRANIE 91 00-44-29, 92 04-01-39, 95-fala1 09-14-49, 95 12-25-19; ksiega towarow "Towary: ... flax / linen"):
    ///  - KS wlacza sie w dobie ok. 37 biegu (109111/2), a w dobach D..D+14 (109126-109140) plon lnu wsi spada z ok. 1000 do ok. 150 szt./d (+ ok. 125 z pol mieszczan)
    ///    we WSZYSTKICH czterech biegach; od tej pory len: produkcja 280 szt./d wobec zuzycia 454 / 415 / 366 / 341 / 322 (okna 20 dob) - zapas lnu swiata
    ///    12 181 -> 3 140, plotno: produkcja 345 / 310 / 281 / 266 / 253 wobec zuzycia 411 / 347 / 308 / 266 / 262 - zapas 8 657 -> 5 492 (-32 szt./d);
    ///    w nowej kampanii bez KS (120 dob, 08-45-54) len 720-1130 szt./d, zapas lnu 6 809 -> 11 310, plotna 1 887 -> 3 841 - spadek jest skutkiem KS;
    ///  - rece zajec pod KS stoja na Planie wlaczenia (A7 przesuwa je tylko za cena cienia Planu) - a Plan chce na targ ok. 284 szt. lnu/d (153 wsie + 131 pola),
    ///    bo jego popyt na len = 1.25 x popyt na plotno (receptura KS - tkacze miast i tkalnie po wlaczeniu tkaja 1.25 lnu na plotno: 315 lnu -> 253 plotna);
    ///  - w popycie Planu na plotno sa: ludzie (koszyk zaplacony), odziez wojska, warsztaty zbrojne, naprawy, dwory. NIE MA plotna, ktore wsie kupuja w miastach
    ///    (ramka E5/B1 "wsie (zakupy w miescie)": 143 / 84 / 72 / 91 / 77 / 61 / 36 / 36 szt./d w oknach 20 dob) ponad koszyk zaplacony Planu - Plan liczy
    ///    niezaplacony koszyk wsi (plotno 115 W + 69 E = 184 szt./d, len 150) jako "robiony w domu" (bez lnu i bez rak w grze), a wsie w grze kupuja to plotno
    ///    z kiesy w miescie targowym.
    ///
    /// REGULA (klucz Ks Linen Village Buys In Plan, domyslnie TAK): przy budowie wejscia Planu (watek glowny, co 7 dob) popyt 1:1 plotna dostaje
    ///   max(0, srednia zakupow plotna przez wsie w miastach (ksiega towarow, ramka E5/B1, srednia z najwyzej 28 dob, od 7 dob pomiaru)
    ///          - plotno koszyka wsi zaplacone w poprzednim Planie (KsResult.DistPaid, szt./d))
    /// - to samo plotno nie jest liczone dwa razy (czesc zaplacona juz jest w popycie Planu). Popyt Planu na len rosnie o 1.25 x tyle (KsCore.Demand: D[len] =
    /// (potrzeby + 1:1 plotna) x LinenFlax) - Plan daje rekom wsi wiecej lnu przy wlaczeniu, a po wlaczeniu wyzsza cena cienia lnu idzie do sygnalu A7
    /// (Ks Switch Signal Rule - zmiana zajec wsi z hamulcem Jeffa). Bez mnoznika plonu: wiecej lnu = wiecej rak przy lnie (te same tabele swiata i ziemia).
    /// Pieniadze i towar: bez przelewow (zmienia sie tylko liczba popytu w Planie). Zapis bez zmian (srednia od zera w kazdej sesji, nic w SyncData).
    /// Klucz NIE - dopisek 0, Plan co do bitu jak przed paczka. Linia "Len KS (m96-len): plan doba N" przy kazdym Planie (tools/sprawdz_logi.py --grupa m96len).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const int M96MinDays = 7, M96EmaDays = 28;
        private static double _m96Ema;            // srednia zakupow plotna przez wsie w miastach (szt./d)
        private static int _m96Days, _m96LastDay = int.MinValue, _m96Stum, _m96StumAll;
        private static long _m96Today;            // ostatnia doba ksiegi (szt.)

        internal static bool M96LenOn { get { var s = Settings.Current; return s == null || s.KsLinenVillageBuysInPlan; } }

        private static void M96LenReset() { _m96Ema = 0; _m96Days = 0; _m96LastDay = int.MinValue; _m96Today = 0; _m96Stum = 0; }

        /// <summary>GoodsLedger.KsNote (watek glowny, raz na dobe na towar): zakupy plotna przez wsie w miastach (ramka E5/B1) tej doby. Nie rzuca.</summary>
        internal static void M96LenLedger(string id, int day, long vilBuy)
        {
            try
            {
                if (id != "linen" || day < 0) return;
                if (day == _m96LastDay) return;
                if (_m96LastDay != int.MinValue && day < _m96LastDay) M96LenReset();   // inna kampania / wczytanie wczesniejszej doby - od zera
                _m96LastDay = day; _m96Today = Math.Max(0, vilBuy);
                _m96Days++;
                double w = 1.0 / Math.Min(M96EmaDays, _m96Days);
                _m96Ema += (_m96Today - _m96Ema) * w;
            }
            catch (Exception e) { _m96Stum++; _m96StumAll++; Stumble("M96LenLedger", e); }
        }

        /// <summary>BuildInput (watek glowny): dopisek popytu 1:1 plotna w wejsciu Planu i linia logu. Nie rzuca.</summary>
        private static void M96LenApply(KsInput inp, int day)
        {
            try
            {
                if (inp == null) return;
                KsItem lin; inp.Items.TryGetValue("linen", out lin);
                bool on = M96LenOn;
                double paid = -1, extra = 0, flaxKg = 0, sigma = Math.Max(1.0, inp.Sigma);
                string why;
                if (!on) why = "klucz NIE - Plan jak przed paczka";
                else if (lin == null) why = "brak plotna w wejsciu Planu";
                else if (_m96Days < M96MinDays) why = "pomiar " + _m96Days + " dob (< " + M96MinDays + ") - bez dopisku";
                else
                {
                    paid = PaidVillageLinen(sigma);
                    if (paid < 0) why = "brak poprzedniego Planu (koszyk zaplacony nieznany) - bez dopisku";
                    else
                    {
                        extra = Math.Max(0.0, _m96Ema - paid);
                        why = extra > 0 ? "dopisane" : "zakupy wsi w koszyku zaplaconym Planu - bez dopisku";
                    }
                }
                if (lin != null) lin.VilBuy = on ? extra : 0.0;
                flaxKg = extra * sigma * (lin != null ? lin.Kg : 10.0) * KsTab.LinenFlax / 1000.0;
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(400);
                sb.Append("Len KS (m96-len): plan doba ").Append(day).Append(" | klucz Ks Linen Village Buys In Plan ").Append(on ? "TAK" : "NIE")
                  .Append(" | zakupy plotna przez wsie w miastach (E5/B1) srednia ").Append(_m96Ema.ToString("0.0", inv)).Append(" szt./d z ").Append(Math.Min(_m96Days, M96EmaDays))
                  .Append(" dob (ostatnia doba ").Append(_m96Today).Append(")")
                  .Append(", koszyk wsi zaplacony w poprzednim Planie ").Append(paid >= 0 ? paid.ToString("0.0", inv) : "-").Append(" szt./d")
                  .Append(" | dopisek do popytu 1:1 plotna ").Append(extra.ToString("0.0", inv)).Append(" szt./d (lnu x").Append(KsTab.LinenFlax.ToString("0.00", inv))
                  .Append(" = ").Append((extra * KsTab.LinenFlax).ToString("0.0", inv)).Append(" szt./d, ").Append(flaxKg.ToString("0.0", inv)).Append(" t/d w Planie) - ").Append(why)
                  .Append(" | pieniadze i towar: bez przelewow | potkniecia ").Append(_m96Stum).Append(" (sesja ").Append(_m96StumAll).Append(").");
                Log.Info(sb.ToString());
                _m96Stum = 0;
            }
            catch (Exception e) { _m96Stum++; _m96StumAll++; Stumble("M96LenApply", e); }
        }

        /// <summary>Plotno koszyka wsi zaplacone w ostatnim gotowym Planie (szt. gry/d, suma okregow); -1 - brak Planu.</summary>
        private static double PaidVillageLinen(double sigma)
        {
            var pr = _plan;
            if (pr == null || pr.DistPaid == null) return -1;
            int z = KsBasket.Linen;
            double kg = 0;
            foreach (var kv in pr.DistPaid)
            {
                var a = kv.Value;
                if (a != null && z < a.Length && a[z] > 0) kg += a[z];
            }
            double pk = ZKg(z);
            return kg / (sigma * (pk > 0 ? pk : 10.0));
        }
    }
}
