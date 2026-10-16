using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Limit dopisu budzetu dnia do kategorii celu PRZY NOWYM POPYCIE TARGU (poprawka po recenzji noc-ekonomia2 16.10; wspolne dla zamiennika jedzenia B2
    /// FoodSubstitute i budzetu bez producenta N2-E2 NoMakerBudget).
    /// Dowod (autotesty 15.10 19-59-53 i 20-16-31, linia "Zamienniki jedzenia (B2)"): "kontrola limitu zamiennikow: z dopisem 693, przekroczen 86 (najwiecej 53.6%
    /// nadwyzki)" - co dobe 51-91 przekroczen, najwiecej 51.7-53.6% nadwyzki przy limicie 50%.
    /// Przyczyna [K]: limit w jednostkach budzetu dnia liczony nachyleniem BK przy STARYM indeksie ceny (BK CalculateBudget = popyt x indeks^a + dobrobyt / indeks),
    /// a dopis podnosi tez popyt targu (ItemData.Demand + waga wygladzania x dopis), z ktorego gra liczy indeks ceny (TownMarketData.GetPriceFactor czyta Demand) -
    /// budzet BK celu przy konsumpcji wychodzi wiekszy, niz zakladalo nachylenie.
    /// Regula: planowany dopis got do celu t (suma ze wszystkich zrodel osady) mnozymy przez f (0..1]: przyrost budzetu BK celu przy popycie targu po dopisie
    /// (d0 + waga x got x f) i slowniku po dopisie (bt0 + got x f) nie wiekszy niz limit w zlocie (0.5 nadwyzki polki po ofercie ludu). Krok siecznej przez zero
    /// (przyrost ~ dopis): f = f x limit / przyrost, najwyzej Steps krokow. Popyt targu celu wraca do d0 przed wyjsciem (takze przy wyjatku) - zmiany zapisuje
    /// wolajacy po rachunku calej osady (slownik albo przesuniety, albo nietkniety). Nie rzuca dalej niz pytanie o budzet BK (wolajacy liczy potkniecie).
    /// </summary>
    internal static class ShiftLimit
    {
        private const int Steps = 4;

        /// <summary>Czesc f planowanego dopisu got do kategorii t. used - przyrost budzetu BK celu przy zwroconym f (nad b0); trimmed - czy f &lt; 1.</summary>
        internal static float Fit(Town town, TownMarketData md, ItemCategory t, float bt0, float b0, float got, float gold, float alpha,
                                  Func<Town, float, ItemCategory, float> budget, out float used, out bool trimmed)
        {
            used = 0f; trimmed = false;
            if (!(got > 0f)) return 0f;
            float lim = gold * 1.001f + 0.5f;
            float d0 = md.GetCategoryData(t).Demand;
            float f = 1f;
            try
            {
                for (int i = 0; ; i++)
                {
                    md.SetDemand(t, d0 + alpha * got * f);
                    used = budget(town, bt0 + got * f, t) - b0;
                    if (!(used > lim) || i >= Steps) break;
                    float nf = f * Math.Max(0f, gold) / used;
                    if (!(nf >= 0f) || float.IsInfinity(nf)) nf = 0f;
                    f = Math.Min(f, nf);
                    trimmed = true;
                }
            }
            finally { md.SetDemand(t, d0); }
            return f;
        }
    }
}
