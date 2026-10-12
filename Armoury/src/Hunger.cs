using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>Przyczyna glodu osady (E5/C2, D2): brak - nic; brak jedzenia ma pierwszenstwo przed brakiem pieniedzy.</summary>
    internal enum HungerCause { None = 0, NoFood = 1, NoMoney = 2 }

    /// <summary>
    /// PACZKA E5/C2 (203, PLAN 5.9; projekt etapu 5 rozdz. 3 "E5/C2", D2) - JEDNA DEFINICJA GLODU OSADY. Hunger.Of(osada) zwraca przyczyne:
    ///  - BRAK JEDZENIA (pierwszenstwo): warownia z Food 0 (gra Settlement.IsStarving = Town.FoodStocks &lt;= 0 - ten sam warunek, przy ktorym BK daje
    ///    "Food Shortage" (BKProsperityModel.CalculateProsperityChange, galaz IsStarving), RBL "Severe Starvation" -20 (RealisticProsperityModel, FoodStocks &lt;= 0),
    ///    a E4/6 zabija mieszczan) albo glod okregu E4/6 ponad prog (Famine.HungerOf: wies - jej wlasny, warownia - srednia wsi jak PopulationLaw.TownRate,
    ///    &gt; Famine.ThresholdNow - ta sama granica "w glodzie" co przyrost E4/4 i E4/8). Skutki: zgony i uchodzcy - tylko E4/6, dobrobyt - BK i RBL; tu nic.
    ///  - BRAK PIENIEDZY: lud miasta albo podzamcza (kiesa czynna E5/B1) nie stac na jedzenie (Afford_jedzenie &lt; 0.95) przez 7 dob z rzedu, kampania
    ///    &gt;= 120 dob (TownFolk.MoneyHungry). Skutek: zadowolenie BK z jedzenia (galaz jedzenia R14, TownFolk.R14 przy TownFolkHungerHurts) - i tylko on.
    /// Sam odczyt (bez zapisu stanu gry i zlota). Wolane z watku glownego (R14 w PayForGoods, linia doby). Nie rzuca: wyjatek - None i potkniecie TownFolk.
    /// </summary>
    internal static class Hunger
    {
        internal const double MoneyAfford = 0.95;    // Afford jedzenia na jutro ponizej - doba glodu z braku pieniedzy
        internal const int MoneyDays = 7;            // tyle dob z rzedu
        internal const double FromDay = 120.0;       // nie w pierwszych 120 dobach kampanii [D 09.10 A]

        internal static HungerCause Of(Settlement st)
        {
            if (st == null) return HungerCause.None;
            try
            {
                if (NoFood(st)) return HungerCause.NoFood;
                if (TownFolk.MoneyHungry(st)) return HungerCause.NoMoney;
                return HungerCause.None;
            }
            catch (Exception e) { TownFolk.StumbleOut("Hunger.Of", e); return HungerCause.None; }
        }

        /// <summary>Brak jedzenia: warownia z Food 0 albo glod okregu E4/6 ponad prog (wies - jej spichlerz).</summary>
        internal static bool NoFood(Settlement st)
        {
            if (st == null) return false;
            if (st.IsVillage) return st.Village != null && Famine.HungerOf(st.Village) > Famine.ThresholdNow;
            if (st.IsStarving) return true;
            return District(st) > Famine.ThresholdNow;
        }

        /// <summary>Warownia z Food 0 (gra IsStarving - warunek BK "Food Shortage" i RBL "Severe Starvation").</summary>
        internal static bool Starving(Settlement st) { return st != null && st.IsStarving; }

        /// <summary>Glod okregu warowni: srednia Famine.HungerOf wsi warowni (jak PopulationLaw.TownRate, 0..1).</summary>
        internal static float District(Settlement st)
        {
            if (st == null || st.BoundVillages == null) return 0f;
            float h = 0f; int n = 0;
            foreach (var v in st.BoundVillages) { if (v == null) continue; h += Famine.HungerOf(v); n++; }
            return n > 0 ? Math.Max(0f, Math.Min(1f, h / n)) : 0f;
        }

        /// <summary>Wiek kampanii w dobach (od CampaignStartTime modelu czasu - jak CastlePurse i ColdStart); 0 przy bledzie.</summary>
        internal static double CampaignAge()
        {
            try { var c = Campaign.Current; return c != null ? (CampaignTime.Now - c.Models.CampaignTimeModel.CampaignStartTime).ToDays : 0.0; }
            catch { return 0.0; }
        }
    }
}
