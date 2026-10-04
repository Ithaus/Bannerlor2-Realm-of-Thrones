using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

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
}
