using System;
using System.Globalization;

namespace Armoury
{
    /// <summary>
    /// N11 (noc 16.10, audyt ekonomii 15.10 rozdz. 3.1, tabela problemow nr 22) - JEDNOSTKA LADUNKU NA CZAS KAMPANII. Suwak MCM "Hist Bulk Unit Factor"
    /// (0-40, domyslnie 10; w Armoury.json Jeffa go nie ma) przeliczal przy kazdym wczytaniu wage rudy i drewna (HistoricalPrices.Apply), a liczba sztuk
    /// na stosach swiata zostawala - jedno przesuniecie suwaka = x2 albo /10 drewna i rudy calego swiata w biegnacej kampanii.
    /// Teraz (klucz BulkUnitPerCampaign): czynnik kampanii w zapisie (arm_bulk_unit, obok arm_startstock) - nowa kampania bierze suwak z chwili pierwszego
    /// Apply, stary zapis bez klucza dostaje 10 (to, co ma), kazdy nastepny Apply bierze czynnik kampanii; inny suwak = linia "MCM X ignored, campaign uses Y".
    /// Klucz wylaczony: suwak na zywo jak dotad, a czynnik kampanii idzie za nim (zapis trzyma to, czego kampania faktycznie uzywa).
    /// Obieg: zloto i towar nie ruszaja - to tylko jednostka liczenia.
    /// </summary>
    internal static class BulkUnit
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float OldSave = 10f;            // zapis sprzed N11: kampania liczyla ladunek domyslnym suwakiem 10
        private static float _camp = -1f;              // -1 = nieustalony (nowa kampania - ustali pierwszy Apply)
        private static string _src = "nowa kampania";
        private static string _last = "";
        private static int _stumbles;

        internal static bool On { get { var s = Settings.Current; return s != null && s.BulkUnitPerCampaign; } }

        internal static void Reset() { _camp = -1f; _src = "nowa kampania"; _last = ""; _stumbles = 0; }

        internal static string Export() { return _camp > 0f ? "1|" + _camp.ToString("R", Inv) : ""; }

        /// <summary>Wczytanie: brak pola / pusty / zly - stary zapis (10); "1|czynnik" - czynnik kampanii.</summary>
        internal static void Import(string v)
        {
            _camp = OldSave; _src = "stary zapis (brak arm_bulk_unit) - 10";
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var p = v.Split('|');
                float f;
                if (p.Length >= 2 && p[0] == "1" && float.TryParse(p[1], NumberStyles.Float, Inv, out f) && f > 0f && f <= 1000f)
                {
                    _camp = f; _src = "z zapisu";
                }
            }
            catch (Exception e) { _stumbles++; Log.Error("BulkUnit.Import", e); }
        }

        /// <summary>HistoricalPrices.Apply: czynnik ladunku, ktorego uzywa ta kampania (klucz wylaczony - suwak MCM, jak dotad).</summary>
        internal static float Factor(Settings s)
        {
            float mcm = s != null ? s.HistBulkUnitFactor : OldSave;
            try
            {
                if (_camp <= 0f) { _camp = mcm > 0f ? mcm : 1f; _src = "nowa kampania (suwak MCM z chwili startu)"; }
                if (!On)
                {
                    _camp = mcm > 0f ? mcm : 1f;
                    _last = "Bulk Unit Per Campaign NIE - suwak MCM na zywo " + F(mcm) + " (zapis bierze to, czego kampania uzywa)";
                    return mcm;
                }
                _last = "Bulk Unit Per Campaign TAK - kampania " + F(_camp) + " (" + _src + "), MCM " + F(mcm)
                        + (Math.Abs(mcm - _camp) > 0.001f ? " | MCM " + F(mcm) + " ignored, campaign uses " + F(_camp) : " (zgodne)");
                return _camp;
            }
            catch (Exception e) { _stumbles++; Log.Error("BulkUnit.Factor", e); return mcm; }
        }

        private static string F(float v) { return v.ToString("0.##", Inv); }

        /// <summary>Linia startu (po HistoricalPrices.Apply): "Noc-ekonomia (NE): jednostka ladunku ...".</summary>
        internal static string Line() { return (_last.Length > 0 ? _last : "nie liczone (ceny historyczne wylaczone)") + "; potkniecia " + _stumbles; }
    }
}
