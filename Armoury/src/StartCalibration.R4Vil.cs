using System;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS RUNDA 4 - PACZKA r4-bkwies: (1) towary koszyka ZC4 wedlug kategorii (rynek wsi BK - wies z koszykiem nie kupuje tam ani jedzenia, ani towarow koszyka:
    /// kupuje je drabina z limitem potrzeby i ceny); (2) spichlerz G1 (PLAN-KS-RUNDA4 rozdz. 5): przy Ks Granary No Floor wzor gry jako podloga tylko awaryjnie - spichlerz
    /// ponizej Ks Granary Emergency Days dni jedzenia - liczone jako "z niczego (awaryjnie)"; linia podlogi pokazuje podloge DANA (wzor gry), wsi bez podlogi (brak schodzi
    /// z zapasu spichlerza), podloge awaryjna i zapas spichlerzy wsi KS wedlug rynku (diagnoza fin80: linia liczyla STAN podlogi, nie podloge dana).
    /// </summary>
    internal static partial class StartCalibration
    {
        // ------------------------------------------------------------ (1) koszyk ZC4 wedlug kategorii
        /// <summary>Kategoria towaru koszyka ZC4 (jedzenie albo szczebel drabiny zakupow wsi: sol, napoje, przedza, len, sukno, skora, narzedzia, garnki, tran, futra).</summary>
        internal static bool ZBasketCat(ItemCategory c)
        {
            if (c == null) return false;
            Zc4EnsureMap();
            int[] zs;
            if (_zCats == null || !_zCats.TryGetValue(c, out zs) || zs == null) return false;
            foreach (int z in zs) if (ZFood(z) || Array.IndexOf(ZLadder, z) >= 0) return true;
            return false;
        }

        /// <summary>Kategoria jedzenia koszyka ZC4 (szczebel drabiny jedzenia) - bramka NKS-29 "bez jedzenia u wsi z koszykiem".</summary>
        internal static bool ZFoodCat(ItemCategory c)
        {
            if (c == null) return false;
            Zc4EnsureMap();
            int[] zs;
            if (_zCats == null || !_zCats.TryGetValue(c, out zs) || zs == null) return false;
            foreach (int z in zs) if (ZFood(z)) return true;
            return false;
        }

        // ------------------------------------------------------------ (2) spichlerz G1
        private static int _granEm; private static double _granEmFood, _granPpl;
        private static readonly double[] _granBarnF = new double[2], _granBarnP = new double[2];
        // doba zamknieta (LiveDaily) - do linii podlogi (FloorDaily)
        private static int _gUp = -1, _gNoFloor, _gEm, _gAbove, _gRel; private static double _gEmFood, _gPpl;
        private static readonly double[] _gBarnF = new double[2], _gBarnP = new double[2];

        private static int GranEmergencyDays { get { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(364, s.KsGranaryEmergencyDays)) : 60; } }

        /// <summary>GranaryFloor: wies zastosowana z racjami - potrzeba (osobodni) i zapas spichlerza wedlug rynku (srednia dni zapasu w linii podlogi).</summary>
        private static void GranNote(Settlement st, double people, double barnFood)
        {
            if (!(people > 0)) return;
            _granPpl += people;
            if (!(barnFood >= 0)) return;
            int m = MarketOfSettlement(st);
            if (m != 0 && m != 1) return;
            _granBarnF[m] += barnFood; _granBarnP[m] += people;
        }

        /// <summary>Spichlerz ponizej Ks Granary Emergency Days dni jedzenia (0 - nigdy; zapas nieznany - nie).</summary>
        private static bool GranEmergency(double people, double barnFood)
        {
            int ed = GranEmergencyDays;
            return ed > 0 && people > 0 && barnFood >= 0 && barnFood < ed * people;
        }

        private static void GranEm(double food) { _granEm++; if (food > 0 && !double.IsInfinity(food)) _granEmFood += food; }

        /// <summary>LiveDaily (po odczycie licznikow spichlerza doby): liczby doby do linii podlogi; bez podlogi, awaryjne i zapasy od zera.</summary>
        private static void GranDayClose(int up, int above, int rel)
        {
            _gUp = up; _gAbove = above; _gRel = rel;
            _gNoFloor = _granNoFloor; _granNoFloor = 0;
            _gEm = _granEm; _gEmFood = _granEmFood; _gPpl = _granPpl; _granEm = 0; _granEmFood = 0; _granPpl = 0;
            for (int m = 0; m < 2; m++) { _gBarnF[m] = _granBarnF[m]; _gBarnP[m] = _granBarnP[m]; _granBarnF[m] = 0; _granBarnP[m] = 0; }
        }

        /// <summary>FloorDaily: dopisek linii "KS: podloga spichlerza" - podloga dana, bez podlogi, awaryjna (z niczego) i zapasy spichlerzy wsi KS.</summary>
        private static string GranText(bool noFloor)
        {
            if (_gUp < 0) return " | podloga dana: brak doby spichlerza";
            var sb = new StringBuilder(400);
            int ed = GranEmergencyDays;
            sb.Append(" | klucz Ks Granary No Floor ").Append(noFloor ? "TAK" : "NIE").Append(": podloga dana (wzor gry) ").Append(_gUp).Append(" wsi, bez podlogi (brak z zapasu spichlerza) ")
              .Append(_gNoFloor).Append(" wsi, racje KS ponad wzor ").Append(_gAbove).Append(" wsi, podloga zdjeta (KS-R3) ").Append(_gRel).Append(" wsi; z niczego (awaryjnie, spichlerz < ")
              .Append(ed).Append(" dni) ").Append(_gEm).Append(" wsi, ").Append(Math.Round(_gEmFood).ToString("0", Inv)).Append(" osobodni (")
              .Append(_gPpl > 0 ? (100.0 * _gEmFood / _gPpl).ToString("0.000", Inv) : "-").Append("% potrzeby wsi KS); zapas spichlerzy wsi KS:");
            string[] mk = { "Westeros", "Essos" };
            for (int m = 0; m < 2; m++) sb.Append(m == 0 ? " " : ", ").Append(mk[m]).Append(' ').Append(_gBarnP[m] > 0 ? (_gBarnF[m] / _gBarnP[m]).ToString("0", Inv) : "-").Append(" dni");
            return sb.ToString();
        }
    }
}
