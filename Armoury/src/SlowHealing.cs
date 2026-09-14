using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// POWOLNE GOJENIE. Jeff 28.08: "regeneracja zdrowia na mapie za szybka,
    /// ma byc dwa razy dluzsza - bonusy z perkow niech dalej dzialaja".
    /// Postfix na dzienne leczenie szeregowych i bohaterow. Po audycie 30.08
    /// dwie korekty:
    /// 1) OSOBNY procent dla AI (domyslnie 100 = vanilla). Wspolne -50%
    ///    sprawialo, ze partie AI po glodzie/bitwie jezdzily tygodniami
    ///    samymi rannymi (AI leczy 2.5-5.5 ludzi/dzien przy bitwach co
    ///    kilka dni) - obraz "Ramsay zdrowy, wszyscy inni ranni".
    /// 2) Czynnik tnie WYLACZNIE dodatni wynik. Przy glodzie model zwraca
    ///    liczbe UJEMNA (Starving: -25% skladu/dzien zamienianego w rannych)
    ///    i AddFactor lagodzilby te kare o polowe - tniemy leczenie,
    ///    nie kary.
    /// Uwaga do AddFactor: czynniki sa ADDYTYWNE z bonusem medycyny
    /// (wynik = baza * (1 + medycyna + nasz czynnik)), a nie "cieciem
    /// wyniku na koncu" - dlatego najmocniej zwalnia partie bez medyka.
    /// </summary>
    internal static class SlowHealing
    {
        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var s = Settings.Current;
                var t = typeof(DefaultPartyHealingModel);
                var m1 = AccessTools.Method(t, "GetDailyHealingForRegulars");
                var m2 = AccessTools.Method(t, "GetDailyHealingHpForHeroes");
                // GLOD RANI POWOLI (Jeff 14.09: "czemu wszyscy maja rannych?").
                // Vanilla: partia glodujaca W POLU dostaje leczenie -25% skladu
                // DZIENNIE (DefaultPartyHealingModel, galaz IsStarving) - czyli
                // cala armia ranna w cztery dni. Zima (+50% jedzenia), drozyzna
                // i cap zapasow AI 4 dni robia z tego kaskade: lordowie jezdza
                // z 0 zdrowych/200 rannych, a infekcje RB dobijaja rannych.
                // Kara zostaje, ale w ludzkim tempie (suwak, dom. 5%/dzien) -
                // gracz i AI rowno; garnizony (-10%) bez zmian.
                if (m1 != null && s.StarvationWoundPercent != 25)
                {
                    harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), "StarvePostfix") { priority = Priority.Last });
                    Log.Info("SlowHealing: glod rani " + s.StarvationWoundPercent + "% skladu dziennie (vanilla 25%).");
                }
                if (s.HealingRegenPercent == 100 && s.AiHealingRegenPercent == 100)
                { Log.Info("SlowHealing: 100%/100% - vanilla tempo gojenia, patch spi."); return; }
                int patched = 0;
                if (m1 != null) { harmony.Patch(m1, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix")); patched++; }
                if (m2 != null) { harmony.Patch(m2, postfix: new HarmonyMethod(typeof(SlowHealing), "SlowPostfix")); patched++; }
                Log.Info("SlowHealing: gojenie na mapie - gracz " + s.HealingRegenPercent
                         + "%, AI " + s.AiHealingRegenPercent + "% (" + patched + "/2 metod).");
            }
            catch (Exception e) { Log.Error("SlowHealing.ApplyAll", e); }
        }

        private static readonly TextObject _starveText = new TextObject("{=armStarve}Starving", null);

        /// <summary>Priority.Last: po BK (dodaje wlasna kare za glod w oblezeniu)
        /// - skalujemy CALA ujemna wartosc z 25%/dzien do suwaka.</summary>
        public static void StarvePostfix(PartyBase party, bool includeDescriptions, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || party == null || !party.IsMobile) return;
                var mp = party.MobileParty;
                if (mp == null || mp.IsGarrison || !party.IsStarving) return;   // garnizon: vanilla -10%
                float v = __result.ResultNumber;
                if (v >= 0f) return;
                int pct = Math.Max(0, Math.Min(25, s.StarvationWoundPercent));
                float scaled = v * (pct / 25f);
                int rounded = pct == 0 ? 0 : -Math.Max(1, (int)Math.Round(-scaled));   // glod bez kary tylko przy 0
                __result = new TaleWorlds.CampaignSystem.ExplainedNumber(rounded, includeDescriptions, _starveText);
            }
            catch { }
        }

        public static void SlowPostfix(PartyBase party, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                bool player = party != null && party == PartyBase.MainParty;
                int pct = player ? s.HealingRegenPercent : s.AiHealingRegenPercent;
                if (pct == 100 || pct < 0) return;         // >100 = szybsze gojenie, tez dozwolone
                if (__result.ResultNumber <= 0f) return;   // kara (glod itp.) - nie lagodzic
                __result.AddFactor(pct / 100f - 1f, new TextObject("{=armSlowMend}Wounds knit slowly", null));
            }
            catch { }
        }
    }
}
