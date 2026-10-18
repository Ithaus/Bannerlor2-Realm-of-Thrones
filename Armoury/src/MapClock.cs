using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// ZEGAR I BIEG CZASU (Jeff 04.10: "czy mozna dodac szybszy uplyw czasu plus ktora jest
    /// dokladnie godzina").
    /// 1. Pasek mapy (MapTimeControlVM.Tick) odswieza date raz na dzien - doklejamy godzine
    ///    "Year 299, Day 256, Autumn - 14:30" (13.10: format daty z CalendarText; dawniej "Day N of Summer, 299 AC - 14:30"),
    ///    odswiezana, gdy zmieni sie minuta.
    /// 2. Przyspieszenie: Campaign.SpeedUpMultiplier (vanilla 4, gra przywraca 4 przy kazdym
    ///    wczytaniu) - ustawiamy z MCM co godzine gry i przy starcie sesji. f9-predkosc: przy przyciskach biegu
    ///    (MapSpeed.cs) - wybrany bieg szybki x4 / x8 (takze co klatke paska w MapSpeed.TickPostfix).
    /// </summary>
    internal static class MapClock
    {
        private static int _lastMinute = -1;
        private static System.Reflection.PropertyInfo _date;

        public static void TickPostfix(object __instance) { Tick(__instance, false); }

        // f9-predkosc (MapSpeedPerf): przy biegu szybkim minuta gry mija co klatke (x8: 7 ms realnych), a kazda zmiana to tekst daty (CampaignTime.ToString
        // z latka kalendarza, GameTexts pory), SetValue przez odbicie i nowy uklad napisu paska - najwyzej 10 razy na sekunde realna. Sam widok: przy pauzie i x1
        // jak dotad (minuta po minucie), odswiezenie po RefreshValues od reki; pominieta zmiana zostaje (_lastMinute bez zmian) do nastepnej klatki po 0.1 s.
        private static long _lastSetTs;
        private const long ThrottleDiv = 10;   // 1/10 s

        private static bool FastNow(Campaign c)
        {
            var m = c.TimeControlMode;
            return m == CampaignTimeControlMode.StoppableFastForward || m == CampaignTimeControlMode.UnstoppableFastForward
                   || m == CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime;
        }

        private static void Tick(object __instance, bool force)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (s == null || !s.MapClockEnabled || Campaign.Current == null) return;
                var now = CampaignTime.Now;
                int minuteOfDay = MinuteOfDay(now);
                if (minuteOfDay == _lastMinute) return;
                if (!force && s.MapSpeedPerf && FastNow(Campaign.Current) && t0 - _lastSetTs < System.Diagnostics.Stopwatch.Frequency / ThrottleDiv)
                {
                    MapSpeedProfile.ClockSkip();
                    return;
                }
                _lastSetTs = t0;
                _lastMinute = minuteOfDay;
                if (_date == null) _date = AccessTools.Property(__instance.GetType(), "Date");
                if (_date == null) return;
                _date.SetValue(__instance, BarText(now, minuteOfDay), null);
                MapSpeedProfile.ClockSet();
            }
            catch { }
            finally { MapSpeedProfile.Hook(MapSpeedProfile.HClock, System.Diagnostics.Stopwatch.GetTimestamp() - t0); }
        }

        private static int MinuteOfDay(CampaignTime now) { return (int)Math.Floor((now.ToHours % 24.0) * 60.0); }

        private static string BarText(CampaignTime now, int minuteOfDay)
        {
            return now.ToString() + " - " + (minuteOfDay / 60).ToString("00") + ":" + (minuteOfDay % 60).ToString("00");
        }

        /// <summary>
        /// Tekst daty na pasku mapy dla chwili 'now': data gry (CampaignTime.ToString - latka WesterosClimate.ToStringPrefix: "Year 299,
        /// Day 256, Autumn" z CalendarText albo stary format) i godzina "HH:MM". Dzien i godzina z tych samych tickow zegara kampanii.
        /// Ta sama funkcja sklada pasek i linie dowodu "Data na pasku (kalendarz)" (CalendarText.Hourly).
        /// </summary>
        internal static string BarText(CampaignTime now) { return BarText(now, MinuteOfDay(now)); }

        public static void RefreshPostfix(object __instance) { _lastMinute = -1; Tick(__instance, true); }

        internal static void ApplySpeed()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                // f9-predkosc: przy przyciskach biegu (MapSpeedButtons) - wybrany bieg szybki (MapSpeedFast / MapSpeedVeryFast); bez nich FastForwardMultiplier 1..64 jak dotad
                float m = MapSpeed.FfMultiplier(s);
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
