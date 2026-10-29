using System;
using System.Globalization;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// 297 (29.10, klucz WinterSparesMinesAndFelling): ZIMA NIE TNIE RUDY Z KOPALNI ANI DREWNA Z WYREBU (KS - ComputeDistrict).
    ///
    /// Przyczyna z liczbami (autotest 137a = 136 + 290, 90 dob 109077-109166 na kopii zapisu Jeffa, Armoury-2026-10-29_12-05-50.log):
    ///  - przed zima (109079-109140, 62 doby): wydobycie wsi 347 lup/d (model 374), zuzycie 356 (warsztaty zbrojne ok. 200, narzedzia ok. 130, strzelarze 7,
    ///    przetwory 3), zlom +10 - zelazo swiata 9320 -> 9110 (-3.4/d): krok 290 trzyma wydobycie za zuzyciem; stawka KS na 1000 rak stala (Westeros 3.66 -> 3.68,
    ///    Essos 3.69 -> 3.72) - spadek rak (105 740 -> 78 800) to krok 290 i krainy wrocone (volantine 109089, myrish 109093: -12 tys. rak Essos poza ksiega KS);
    ///  - zima od 109141 (WinterBite: ciecie plonu wsi 48.6% reszta, 15.7% cieple krainy) - ComputeDistrict mnozyl KAZDY towar okregu przez (1 - ciecie):
    ///    model kopalni 365 -> 227 lup/d w 9 dobach (przeliczenie okregow co LiveEvery dob), stawka KS Westeros 194 -> 109 przy rekach +3%, Essos 97 -> 59;
    ///    wydobycie 228 przy zuzyciu 338 - zelazo swiata -105.6/d (26 dob zimy: 9149 -> 6508), miasta bez rudy 15 -> 22, polki miast 5580 -> 3520. Mediana indeksu
    ///    ceny rudy 0.75-0.82 przez cala zime (polki dalej ponad cel 2 170) - hamulec ceny jeszcze nie wchodzi, zuzycie stoi; krok 290 dokladal +5% rak na tydzien
    ///    (Westeros 52.7 -> 60.4 tys.), wobec -38% modelu w 9 dobach to za malo, a po odwilzy dalby nadmiar.
    ///  - ta sama zima tnie drewno wyrebu: wsie 3660 -> 2067 ladunkow/d, drewno swiata 51.2 -> 36.7 tys. (-570/d), polki miast 11.5 -> 6.6 tys.
    /// Zima w WinterBite mowi "wioski nie rodza" - to plon pola, sadu, stada i polowu. Ruda z kopalni i wyrab lasu to praca rak (zima byla pora wyrebu i wozenia
    /// saniami, kopalnie pod ziemia pracuja caly rok). Regula: przy kluczu ruda zelaza, srebra i zlota oraz drewno (WinterBite.SparedId) dostaja czynnik okregu
    /// bez zimy - rece spustoszenia (Devastation) i wojny (FreeHands) zostaja; pola mieszczan - bez ciecia zimy miasta. Te sama lista w modelu gry
    /// (WinterBite.VillageProdPostfix - wsie poza KS, krainy wrocone) i w dopiskach zajec wiosek pobocznych (VillageCrafts). Bez zlota i towaru z niczego: wiecej
    /// rak przy pracy zima nie ma - zima zabierala wynik tej samej pracy. Klucz NIE albo poza zima - rachunek co do bitu jak dotad.
    /// Linia "Zima - kopalnie i wyrab (297): dzien N" w kazdej dobie zimy (KS zastosowany).
    /// </summary>
    internal static partial class StartCalibration
    {
        private static bool[] _w297Ids;

        /// <summary>ComputeDistrict (z czynnikami): maska towarow KS bez ciecia zimy; null - klucz NIE albo poza zima (petla jak dotad).</summary>
        private static bool[] W297Mask()
        {
            if (!WinterBite.SparesOn || !WinterBite.WinterNow()) return null;
            var m = _w297Ids;
            if (m == null)
            {
                m = new bool[ItemCount];
                for (int i = 0; i < ItemCount; i++) m[i] = WinterBite.SparedId(ItemIds[i]);
                _w297Ids = m;
            }
            return m;
        }

        /// <summary>LiveDaily (po stawkach jutra): linia doby zimy - ile produkcji okregow KS zima juz nie zabiera (stawki opublikowane, takze okregi przeliczone wczesniej).</summary>
        private static void W297Line(int day)
        {
            if (!LogGate.Lines || !WinterBite.WinterNow()) return;
            var inv = CultureInfo.InvariantCulture;
            bool on = WinterBite.SparesOn;
            int games = WinterBite.TakeSpareGame();
            int nAll = 0, nSp = 0, stum = 0; double cutSum = 0;
            var pcs = new double[ItemCount];
            EnsureItems();
            double sigma = Math.Max(1, DailyLife.KsSigma);
            foreach (var ld in _live.Values)
            {
                try
                {
                    if (ld == null || ld.St == null || ld.K == null || ld.Res == null) continue;
                    if (LandBackOf(ld.St)) continue;
                    nAll++;
                    var sk = ld.Res.WSpareKg;
                    if (sk == null) continue;
                    nSp++;
                    if (!ld.TF && ld.V != null) cutSum += WinterBite.VillageCut(ld.V);
                    else if (ld.TF && ld.T != null) cutSum += WinterBite.TownCut(ld.T);
                    for (int i = 0; i < ItemCount && i < sk.Length; i++) if (sk[i] > 0) pcs[i] += sk[i] / (sigma * _itemKg[i]);
                }
                catch (Exception e) { stum++; Stumble("W297Line", e); }
            }
            var sb = new StringBuilder(600);
            sb.Append("Zima - kopalnie i wyrab (297): dzien ").Append(day).Append(" | klucz Winter Spares Mines And Timber Felling ").Append(on ? "TAK" : "NIE")
              .Append(on ? " (ruda zelaza, srebra, zlota i drewno wyrebu bez ciecia zimy; rece spustoszenia i wojny zostaja)" : " (zima tnie kazdy towar wsi jak dotad)")
              .Append(" | okregi KS z ruda albo drewnem bez ciecia ").Append(nSp).Append(" z ").Append(nAll)
              .Append(", ciecie zimy tych okregow srednio ").Append(nSp > 0 ? (100.0 * cutSum / nSp).ToString("0.0", inv) : "-").Append('%')
              .Append(" | produkcja okregow, ktorej zima nie zabiera (szt./d, przed stolem okregu): zelazo ").Append(pcs[KsDeliver.IIron].ToString("0.0", inv))
              .Append(", srebro ").Append(pcs[KsDeliver.ISilver].ToString("0.0", inv)).Append(", zloto ").Append(pcs[KsDeliver.IGold].ToString("0.0", inv))
              .Append(", drewno ").Append(pcs[KsDeliver.IWood].ToString("0.0", inv))
              .Append(" | model gry (wsie poza pelna stawka KS): wywolan bez ciecia zimy ").Append(games)
              .Append(" | pieniadze i towar: bez przelewow - czynnik okregu bez zimy dla tych towarow | potkniecia ").Append(stum).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
