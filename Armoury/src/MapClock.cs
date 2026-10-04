using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// ZEGAR I BIEG CZASU (Jeff 04.10: "czy mozna dodac szybszy uplyw czasu plus ktora jest
    /// dokladnie godzina").
    /// 1. Pasek mapy (MapTimeControlVM.Tick) odswieza date raz na dzien - doklejamy godzine
    ///    "Day N of Summer, 299 AC - 14:30", odswiezana, gdy zmieni sie minuta.
    /// 2. Przyspieszenie: Campaign.SpeedUpMultiplier (vanilla 4, gra przywraca 4 przy kazdym
    ///    wczytaniu) - ustawiamy z MCM co godzine gry i przy starcie sesji.
    /// </summary>
    internal static class MapClock
    {
        private static int _lastMinute = -1;
        private static System.Reflection.PropertyInfo _date;

        public static void TickPostfix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.MapClockEnabled || Campaign.Current == null) return;
                var now = CampaignTime.Now;
                double hours = now.ToHours;
                int minuteOfDay = (int)Math.Floor((hours % 24.0) * 60.0);
                if (minuteOfDay == _lastMinute) return;
                _lastMinute = minuteOfDay;
                if (_date == null) _date = AccessTools.Property(__instance.GetType(), "Date");
                if (_date == null) return;
                _date.SetValue(__instance, now.ToString() + " - " + (minuteOfDay / 60).ToString("00") + ":" + (minuteOfDay % 60).ToString("00"), null);
            }
            catch { }
        }

        public static void RefreshPostfix(object __instance) { _lastMinute = -1; TickPostfix(__instance); }

        internal static void ApplySpeed()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                float m = Math.Max(1f, Math.Min(64f, s.FastForwardMultiplier));
                if (Math.Abs(Campaign.Current.SpeedUpMultiplier - m) > 0.01f) Campaign.Current.SpeedUpMultiplier = m;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar.MapTimeControlVM");
                var m = t != null ? AccessTools.Method(t, "Tick") : null;
                if (m != null) h.Patch(m, postfix: new HarmonyMethod(typeof(MapClock), nameof(TickPostfix)));
                var r = t != null ? AccessTools.Method(t, "RefreshValues") : null;   // vanilla wpisuje tu sama date - dopisz godzine od razu
                if (r != null) h.Patch(r, postfix: new HarmonyMethod(typeof(MapClock), nameof(RefreshPostfix)));
                Log.Info("MapClock: zegar na pasku mapy " + (m != null ? "wpiety" : "BRAK MapTimeControlVM.Tick") + ".");
            }
            catch (Exception e) { Log.Error("MapClock.ApplyAll", e); }
        }
    }
}
