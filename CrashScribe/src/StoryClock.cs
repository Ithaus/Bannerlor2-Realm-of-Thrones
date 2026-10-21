using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace CrashScribe
{
    /// <summary>
    /// m101-prolog E1a: START OPOWIESCI ROT. W kampanii z prologiem 298 (Armoury, Prologue.cs) model czasu Armoury (LongYearTimeModel) podaje w CampaignStartTime
    /// start KALENDARZA (188 dni przed opowiescia), a start opowiesci - w publicznej wlasciwosci StoryStartTime (data ROT 299, jak w klasycznym starcie).
    /// Rozrusznik Fabuly i kalendarz Innych (NightKingCall.Day) licza od startu opowiesci - tak samo jak latka Armoury na odczytach CampaignStartTime w ROT.dll.
    /// Odczyt odbiciem (CrashScribe nie odwoluje sie do Armoury): wlasciwosc StoryStartTime typu CampaignTime na czynnym modelu czasu; brak (inny model, Armoury
    /// bez dlugiego roku) - CampaignStartTime modelu, czyli dokladnie jak dotad. W klasycznej kampanii obie daty sa rowne co do tika.
    /// </summary>
    internal static class StoryClock
    {
        private static Type _modelType;
        private static PropertyInfo _pStory;

        internal static CampaignTime Start()
        {
            var m = Campaign.Current.Models.CampaignTimeModel;
            try
            {
                var t = m != null ? m.GetType() : null;
                if (t != _modelType)
                {
                    _modelType = t;
                    _pStory = null;
                    if (t != null)
                    {
                        var p = t.GetProperty("StoryStartTime", BindingFlags.Public | BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(CampaignTime) && p.GetIndexParameters().Length == 0) _pStory = p;
                    }
                }
                if (_pStory != null) return (CampaignTime)_pStory.GetValue(m, null);
            }
            catch { }
            return m.CampaignStartTime;
        }

        /// <summary>Doby od startu opowiesci (ujemne w prologu).</summary>
        internal static double Days()
        {
            return (CampaignTime.Now - Start()).ToDays;
        }
    }
}
