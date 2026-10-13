using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// DLUZSZY ROK. Vanilla liczy rok jako 7 dni x 3 tygodnie x 4 sezony = 84 dni.
    /// Jeff 04.10 ("1 dzien = 1 dzien"): 13 tygodni w sezonie = rok 364 dni,
    /// prawie prawdziwy (docs/AUDYT-KALENDARZ.md).
    ///
    /// Data startu: wczesniej dziedziczylismy po DefaultCampaignTimeModel, a nasz model
    /// dodawany po ROT zastepowal ROTCampaignTimeModel - kampania zaczynala sie w roku
    /// 1084 zamiast 299 AC. Teraz bierzemy CampaignStartTime (i wschod/zachod slonca)
    /// od modelu, ktory byl przed nami (ROT), a sami zmieniamy tylko dlugosc sezonu.
    /// CampaignTime.Years() liczy wedle zegara po Initialize(), wiec 299 AC zostaje
    /// rokiem 299 przy kazdej dlugosci roku.
    ///
    /// UWAGA: to zmienia sposob, w jaki gra CZYTA zapisany czas. Ustaw raz,
    /// przed zalozeniem kampanii, i nie ruszaj w trakcie.
    /// </summary>
    internal sealed class LongYearTimeModel : DefaultCampaignTimeModel
    {
        internal const int MaxWeeks = 13;
        private readonly CampaignTimeModel _prev;

        internal LongYearTimeModel(CampaignTimeModel prev) { _prev = prev; }

        public override CampaignTime CampaignStartTime
        {
            get
            {
                try { if (_prev != null) return _prev.CampaignStartTime; } catch { }
                return base.CampaignStartTime;
            }
        }

        public override int SunRise { get { try { if (_prev != null) return _prev.SunRise; } catch { } return base.SunRise; } }
        public override int SunSet { get { try { if (_prev != null) return _prev.SunSet; } catch { } return base.SunSet; } }

        public override int WeeksInSeason
        {
            get
            {
                try
                {
                    var c = Settings.Current;
                    if (c == null || !c.LongYearEnabled) return base.WeeksInSeason;
                    return Clamp(c.WeeksPerSeason);
                }
                catch { return base.WeeksInSeason; }
            }
        }

        private static int Clamp(int w) { return w < 1 ? 1 : (w > MaxWeeks ? MaxWeeks : w); }

        /// <summary>Ile dni ma rok przy obecnych ustawieniach - do meldunku w logu.</summary>
        internal static int DaysPerYear(Settings c)
        {
            try
            {
                if (c == null || !c.LongYearEnabled) return 84;
                return 7 * Clamp(c.WeeksPerSeason) * 4;
            }
            catch { return 84; }
        }

        internal static void Install(IGameStarter starter)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.LongYearEnabled) { Log.Info("Kalendarz: rok wedle gry (84 dni)."); return; }
                CampaignTimeModel prev = null;
                try { prev = starter.Models.OfType<CampaignTimeModel>().LastOrDefault(); } catch { }
                // model vanilla nie jest "poprzednikiem" wartym kopiowania - jego start to 1084
                if (prev is DefaultCampaignTimeModel) prev = null;
                starter.AddModel(new LongYearTimeModel(prev));
                Log.Info("Kalendarz: rok ma " + DaysPerYear(c) + " dni (" + Clamp(c.WeeksPerSeason)
                         + " tygodni w sezonie, sezon " + (7 * Clamp(c.WeeksPerSeason)) + " dni). Data startu wedle "
                         + (prev != null ? prev.GetType().FullName : "gry (vanilla)") + ".");
            }
            catch (Exception e) { Log.Error("Calendar.Install", e); }
        }
    }

    /// <summary>
    /// DATA DLA GRACZA (Jeff 13.10: "o 00:01 powinien byc pokazywany nastepny dzien, a teraz zmienia sie dopiero kolo 10-12";
    /// "dalbym 8 rok i 256 dzien autumn, bo 3659 dzien ciezko sie czyta").
    /// JEDNA funkcja tekstu daty: "Year 299, Day 256, Autumn" - rok swiata (CampaignTime.GetYear; ROT startuje kampanie w 299),
    /// dzien roku 1..364 (GetDayOfYear + 1; rok Armoury 364 dni - LongYearTimeModel) i pora roku klimatu Westeros.
    /// Dzien roku liczy sie z tych samych tickow zegara co godzina na pasku (ticki / doba), wiec zmienia sie dokladnie o 0:00.
    /// Stary tekst "Day 3659 of Summer, 299 AC" liczyl PELNE DOBY od poczatku odcinka pory (WesterosClimate.Seg.Start = chwila
    /// pierwszego odczytu klimatu minus 3640 dni - z godzina tej chwili, nie z polnoca), wiec numer zmienial sie o tamtej godzinie.
    /// Kto uzywa: CampaignTime.ToString (pasek mapy, encyklopedia, dziennik wojen - latka WesterosClimate.ToStringPrefix),
    /// powiadomienia ze scena (FormalPrefix), Zelazny Bank (IronBank.Describe, DebtLadder - DayStamp).
    /// To TYLKO tekst: bieg czasu, tiki dobowe i dzien pory czytany przez pogode / gospodarke (WesterosClimate.Day) bez zmian.
    /// Wylacznik ReadableDateEnabled (off = teksty sprzed zmiany).
    /// </summary>
    internal static class CalendarText
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.ReadableDateEnabled; } }

        private static string Text(long year, long dayOfYear, string season)
        {
            string s = "Year " + year + ", Day " + dayOfYear;
            return string.IsNullOrEmpty(season) ? s : s + ", " + season;
        }

        /// <summary>"Year 299, Day 256, Autumn" (bez pory: "Year 299, Day 256"). Pora wedle klimatu Westeros; data sprzed znanej historii por - bez pory.</summary>
        internal static string Date(CampaignTime t, bool withSeason)
        {
            return Text(t.GetYear, t.GetDayOfYear + 1, withSeason ? SeasonName(t) : null);
        }

        /// <summary>
        /// Termin zapisany jako numer doby kampanii ((int)CampaignTime.Now.ToDays - np. IronBank.DueDay): "Year 299, Day 183" zamiast "day 109018".
        /// Liczone z samej liczby (nie przez CampaignTime.Days(float) - mnozenie we float gubi do 14 minut i moze cofnac o dobe).
        /// Wylacznik off = "day N" jak dotad.
        /// </summary>
        internal static string DayStamp(int campaignDay)
        {
            if (!On) return "day " + campaignDay;
            int diy = Math.Max(1, CampaignTime.DaysInYear);
            long d = Math.Max(0, campaignDay);
            return Text(d / diy, d % diy + 1, null);
        }

        /// <summary>Nazwa pory roku daty (tekst gry str_season_*); null - klimat nie zna tej daty (sprzed pierwszego odcinka pory).</summary>
        internal static string SeasonName(CampaignTime t)
        {
            int season = WesterosClimate.TextSeason(ref t);
            if (season < 0) return null;
            return GameTexts.FindText("str_season_" + (CampaignTime.Seasons)season).ToString();
        }

        private static string Ordinal(int n)
        {
            int t = n % 100;
            if (t >= 11 && t <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }

        /// <summary>
        /// Powiadomienia ze scena (slub, smierc, nowe krolestwo; wolaja to tez sceny BannerKings i ROT): gra sklada
        /// "the {DAY_ORDINAL} day of {SEASON_NAME}" z liczebnika str_ordinal_number, a ma ich tylko 0..31 (Native module_strings.xml).
        /// Dzien pory klimatu (tysiace) albo pory 91 dni dawal "On the ERROR: Text with id str_ordinal_number doesn't exist! Variation: 3660
        /// day of Summer, 299, ...". Teraz "the 256th day of the year (Autumn)" - zdanie gry: "On {DAY_OF_YEAR}, {YEAR}, ...".
        /// </summary>
        public static bool FormalPrefix(CampaignTime time, ref TextObject __result)
        {
            try
            {
                if (!On) return true;
                string season = SeasonName(time);
                __result = new TextObject("{=!}the " + Ordinal(time.GetDayOfYear + 1) + " day of the year" + (string.IsNullOrEmpty(season) ? "" : " (" + season + ")"));
                return false;
            }
            catch { return true; }
        }

        // ------------------------------------------------------------ dowod do autotestu (tylko log, wylacznik WorldMeasureLog)
        // Przez pierwsze ProofHours godzin gry kazdej kampanii w sesji (3 doby) jedna linia na godzine: godzina zegara, tekst z paska,
        // dzien roku i numer, ktory dawal stary wzor - widac, ze dzien zmienia sie miedzy godzina 23 a 0, a stary numer o innej godzinie. Potem cisza.
        private const int ProofHours = 72;
        private static WeakReference _proofCampaign;
        private static int _proofLeft;

        internal static void Hourly()
        {
            var s = Settings.Current;
            var camp = Campaign.Current;
            if (s == null || !s.WorldMeasureLog || camp == null) return;
            if (_proofCampaign == null || !ReferenceEquals(_proofCampaign.Target, camp)) { _proofCampaign = new WeakReference(camp); _proofLeft = ProofHours; }
            if (_proofLeft <= 0) return;
            _proofLeft--;
            var now = CampaignTime.Now;
            string shown = s.MapClockEnabled ? MapClock.BarText(now) : now.ToString();
            Log.Info("Data na pasku (kalendarz): godzina " + now.GetHourOfDay + " - '" + shown + "' | dzien roku " + (now.GetDayOfYear + 1)
                     + " | stary wzor dawal " + WesterosClimate.OldTextDay(ref now));
        }
    }
}
