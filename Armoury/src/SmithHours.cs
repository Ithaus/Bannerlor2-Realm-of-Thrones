using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// WSPOLNE RECE KOWALI (wpis 91; Jeff 05.10: "tak" - dzielic czas kowali miedzy wszystkich). Dotad naprawy ludzi gracza
    /// (TroopSelfMend), naprawy AI (AiWear.MendInTown, kazdy lord osobno) i warsztaty (WorkshopLaw) braly te same rece miasta,
    /// kazdy na pelna moc - robocizna z niczego. Teraz: kowale miasta = rece rzemieslnikow x udzial platnerzy i miecznikow,
    /// po WorkHoursPerManDay godzin dziennie. Naprawy (gracz i wszyscy lordowie) biora z tej puli po kolei; godziny zuzyte na
    /// naprawy wczoraj odejmuja sie od rak cechow platnerzy i miecznikow w warsztatach dzis (mniej nowych zbroi i mieczy).
    /// </summary>
    internal static class SmithHours
    {
        private static Dictionary<Town, float> _used = new Dictionary<Town, float>();
        private static Dictionary<Town, float> _yesterday = new Dictionary<Town, float>();
        private static int _day = -1;

        internal static void Reset() { _used.Clear(); _yesterday.Clear(); _day = -1; }

        private static float PerManDay { get { return Math.Max(1f, Settings.Current.WorkHoursPerManDay); } }

        private static void Roll()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _day) return;
            _yesterday = d == _day + 1 ? _used : new Dictionary<Town, float>();
            _used = new Dictionary<Town, float>();
            _day = d;
        }

        /// <summary>Kowale miasta (ludzie): rece rzemieslnikow x (platnerze + miecznicy) / wszystkie cechy.</summary>
        internal static float Smiths(Town t)
        {
            if (t == null) return 0f;
            float wsum = 0f; foreach (var g in new[] { "krawiec", "platnerz", "miecznik", "siodlarz", "lucznik", "tarczownik" }) wsum += WorkshopLaw.GuildWeight(g);
            return WorkshopLaw.TownHands(t) * (WorkshopLaw.GuildWeight("platnerz") + WorkshopLaw.GuildWeight("miecznik")) / Math.Max(0.01f, wsum);
        }

        internal static float Capacity(Town t) { return Smiths(t) * PerManDay; }

        /// <summary>Godziny kowali jeszcze wolne dzis w tym miescie.</summary>
        internal static float Available(Town t)
        {
            if (t == null) return 0f;
            Roll();
            float u; _used.TryGetValue(t, out u);
            return Math.Max(0f, Capacity(t) - u);
        }

        internal static void Use(Town t, float hours)
        {
            if (t == null || hours <= 0f) return;
            Roll();
            float u; _used.TryGetValue(t, out u); _used[t] = u + hours;
        }

        /// <summary>Roboczodni kowali zjedzone wczoraj przez naprawy - tyle mniej rak w warsztatach dzis.</summary>
        internal static float ManDaysYesterday(Town t)
        {
            if (t == null) return 0f;
            Roll();
            float u; _yesterday.TryGetValue(t, out u);
            return u / PerManDay;
        }
    }
}
