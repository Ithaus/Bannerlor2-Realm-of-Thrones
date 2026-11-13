using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Armoury
{
    /// <summary>
    /// 476-d (Jeff 13.11: "Tax office w wiosce - czemu wylaczyc?" -> sesja: "lepszy pobor juz istniejacego czynszu: mniej pieniedzy przepada po drodze, nowe zloto nie
    /// powstaje"; Jeff: "na pozostale sie zgadzam") - URZAD PODATKOWY WSI (BK bannerkings_taxoffice). Dotad martwy: BK dosypywal zloto z niczego (BKTaxModel.
    /// AddVillagePopulationTaxes), co blokuje KingdomTreasury.TaxOfficePrefix (PolicyIncomeConserved).
    /// Nowy efekt w czynszu DS wsi (DsVillage.RentPay): zaleglosc czynszu jest darowana ("decasus") dopiero po DsRentForgiveDays x (1 + 0.5 x poziom) rat, a rata
    /// splaty zaleglosci jest wieksza: DsRentArrearsPayDays / (1 + 0.5 x poziom) - nadal tylko z kiesy ponad koszyk dnia (zaleglosc nigdy nie zabiera chleba).
    /// Zloto: tylko z kiesy wsi do pana (to samo roszczenie, mniej przepada) - nic z niczego. Klucz Buildings338 Village Tax Office (TAK); NIE - jak przed.
    /// </summary>
    internal static class Buildings338VillageTax
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338VillageTaxOffice; } }
        private static BuildingType _tax;
        private static int _hits;

        internal static int Level(Settlement st)
        {
            if (st == null || !st.IsVillage) return 0;
            if (_tax == null) _tax = Buildings338.VillageBuilding("TaxOffice");
            if (_tax == null) return 0;
            var vd = Buildings338.VillageDataOf(Buildings338.PopData(st));
            return Buildings338.VillageLevel(vd, _tax);
        }

        internal static int ForgiveDays(Settlement st, int fd)
        {
            if (!On || fd <= 0) return fd;
            try { int l = Level(st); if (l <= 0) return fd; _hits++; return (int)Math.Min(3640, fd * (1.0 + 0.5 * l)); }
            catch { return fd; }
        }

        internal static int PayDays(Settlement st, int pd)
        {
            if (!On || pd <= 0) return pd;
            try { int l = Level(st); return l <= 0 ? pd : Math.Max(1, (int)Math.Round(pd / (1.0 + 0.5 * l))); }
            catch { return pd; }
        }

        internal static int TakeHits() { int h = _hits; _hits = 0; return h; }
    }
}
