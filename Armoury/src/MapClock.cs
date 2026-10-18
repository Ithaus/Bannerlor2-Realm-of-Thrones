using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

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
        private static string _lastText;   // f9 recenzja: ostatni tekst wpisany przez nas na pasek (porownanie po tresci)

        public static void TickPostfix(object __instance) { Tick(__instance, false); }

        // f9-predkosc (MapSpeedPerf): przy biegu szybkim minuta gry mija co klatke (x8: 7 ms realnych), a kazda zmiana to tekst daty (CampaignTime.ToString
        // z latka kalendarza, GameTexts pory), SetValue przez odbicie i nowy uklad napisu paska - najwyzej 10 razy na sekunde realna. Sam widok: przy pauzie i x1
        // jak dotad (minuta po minucie), odswiezenie po RefreshValues od reki; pominieta zmiana zostaje (_lastMinute bez zmian) do nastepnej klatki po 0.1 s.
        // f9 recenzja: gra sama wpisuje na pasek date BEZ godziny - MapTimeControlVM.Refresh (z MapBarVM.Tick co 0.1-0.2 s, PRZED Tick) raz na dobe, gdy zmieni sie
        // napis dnia, i RefreshValues. Jesli tekst na pasku nie jest nasz (inna tresc niz _lastText), wpisujemy od reki - bez dlawika i bez sprawdzania minuty
        // (inaczej o polnocy przy x8 do 0.1 s sama data, a przy x1 do konca minuty, gdy Refresh przyszedl po zmianie minuty). Porownanie po tresci, nie referencji:
        // setter Date nie podmienia napisu o tej samej tresci, wiec referencja moglaby sie nie zgadzac na zawsze i wpis szedlby co klatke.
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
                var vm = __instance as MapTimeControlVM;
                if (vm == null) return;
                var now = CampaignTime.Now;
                int minuteOfDay = MinuteOfDay(now);
                bool foreign = !string.Equals(vm.Date, _lastText, StringComparison.Ordinal);   // gra (Refresh / RefreshValues) nadpisala pasek sama data
                if (!foreign && minuteOfDay == _lastMinute) return;
                if (!force && !foreign && s.MapSpeedPerf && FastNow(Campaign.Current) && t0 - _lastSetTs < System.Diagnostics.Stopwatch.Frequency / ThrottleDiv)
                {
                    MapSpeedProfile.ClockSkip();
                    return;
                }
                _lastSetTs = t0;
                _lastMinute = minuteOfDay;
                string text = BarText(now, minuteOfDay);
                vm.Date = text;
                _lastText = text;
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
                if (MapSpeed.BetterTimeActive) return;   // f17: Better Time ustawia mnoznik swoimi przyciskami - nie nadpisujemy go co godzine gry
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
