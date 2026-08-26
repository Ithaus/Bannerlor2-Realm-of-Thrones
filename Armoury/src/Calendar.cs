using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// DLUZSZY ROK, wedle zyczenia Jeffa ("a mozemy zrobic, zeby rok mial
    /// 168 dni?"). Vanilla liczy rok jako 7 dni x 3 tygodnie x 4 sezony = 84
    /// dni, przez co swiat pedzi: dzieci dorastaja w kilkanascie godzin gry,
    /// lordowie siwieja w trakcie jednej wojny, a zima trwa trzy tygodnie.
    /// Podnosimy liczbe tygodni w sezonie - sezon robi sie dluzszy, a z nim
    /// caly rok. Przy 6 tygodniach: 7 x 6 x 4 = 168 dni, sezon 42 dni,
    /// starzenie o polowe wolniejsze na dzien gry.
    ///
    /// UWAGA: to zmienia sposob, w jaki gra CZYTA zapisany czas. Ustaw raz,
    /// przed zalozeniem kampanii, i nie ruszaj w trakcie - inaczej wczytany
    /// save pokaze inna date i inny wiek bohaterow.
    /// </summary>
    internal sealed class LongYearTimeModel : DefaultCampaignTimeModel
    {
        public override int WeeksInSeason
        {
            get
            {
                try
                {
                    var c = Settings.Current;
                    if (c == null || !c.LongYearEnabled) return base.WeeksInSeason;
                    int w = c.WeeksPerSeason;
                    if (w < 1) w = 1;
                    if (w > 12) w = 12;
                    return w;
                }
                catch { return base.WeeksInSeason; }
            }
        }

        /// <summary>Ile dni ma rok przy obecnych ustawieniach - do meldunku w logu.</summary>
        internal static int DaysPerYear(Settings c)
        {
            try
            {
                if (c == null || !c.LongYearEnabled) return 84;
                int w = c.WeeksPerSeason; if (w < 1) w = 1; if (w > 12) w = 12;
                return 7 * w * 4;
            }
            catch { return 84; }
        }

        internal static void Install(IGameStarter starter)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.LongYearEnabled) { Log.Info("Kalendarz: rok wedle gry (84 dni)."); return; }
                starter.AddModel(new LongYearTimeModel());
                Log.Info("Kalendarz: rok ma " + DaysPerYear(c) + " dni (" + c.WeeksPerSeason
                         + " tygodni w sezonie, sezon " + (7 * c.WeeksPerSeason) + " dni).");
            }
            catch (Exception e) { Log.Error("Calendar.Install", e); }
        }
    }
}
