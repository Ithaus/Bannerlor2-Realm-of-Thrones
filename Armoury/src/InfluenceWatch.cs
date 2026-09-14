using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// SKAD SIE BIERZE MOJ WPLYW (Jeff 14.09: "i czemu mam wplyw na minusie, to bez sensu").
    /// Gra tego NIE POKAZUJE. Sprawdzone w zdekompilowanej vanilli: rozbicie dziennej
    /// zmiany wplywu (Clan.InfluenceChangeExplained) nie jest czytane przez ZADEN
    /// vanillowy widok - jedyni odbiorcy CalculateInfluenceChange to Clan.cs
    /// i ClanVariablesCampaignBehavior, ktory bierze sama liczbe wynikowa.
    /// Gracz widzi wiec skutek i nigdy przyczyny.
    /// A przyczyn jest sporo i zadna nie jest przyciete do zera:
    /// ChangeClanInfluenceAction.Apply to golo "clan.Influence += amount", bez klamry,
    /// wiec kazdy wydatek (armia, glosowanie w krolestwie, poparcie innego rodu,
    /// proszenie lorda o dolaczenie do armii, utrata lenna, rada BannerKings) moze
    /// zepchnac wplyw pod zero i tam go zostawic. Najemnik ma przy tym prawie zadnych
    /// wplywow: vanilla JAWNIE pomija mu dochod z polityk krolestwa
    /// (DefaultClanPoliticsModel: "if (clan.Kingdom != null && !clan.IsUnderMercenaryService)"),
    /// lenn nie ma, notabli nie ma - a sam kontrakt najemny zjada mu 20% wplywu dziennie
    /// i zamienia je na zloto.
    /// Raz na dzien wypisujemy wiec CALE rozbicie do logu, a graczowi mowimy krotko,
    /// gdy siedzi pod kreska. Modul czysto diagnostyczny - niczego nie zmienia.
    /// </summary>
    internal static class InfluenceWatch
    {
        private static int _lastDay = -1;
        private static bool _wasNegative;

        internal static void DailyReport()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.InfluenceWatchEnabled) return;
                if (Campaign.Current == null || Clan.PlayerClan == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (day == _lastDay) return;
                _lastDay = day;

                var clan = Clan.PlayerClan;
                float now = clan.Influence;
                var exp = clan.InfluenceChangeExplained;
                float change = exp.ResultNumber;

                List<string> parts = new List<string>();
                try
                {
                    var lines = exp.GetLines();
                    if (lines != null)
                        foreach (var ln in lines)
                            parts.Add(ln.name + " " + (ln.number >= 0f ? "+" : "") + ln.number.ToString("0.#"));
                }
                catch { }

                Log.Info("WPLYW: stan " + now.ToString("0.#")
                         + ", dzienna zmiana " + (change >= 0f ? "+" : "") + change.ToString("0.#")
                         + " | " + (parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "brak rozbicia"));

                // graczowi mowimy tylko wtedy, gdy jest po co - i tylko raz na wejscie pod kreske
                if (now < 0f)
                {
                    if (!_wasNegative)
                    {
                        _wasNegative = true;
                        Log.Player("Your clan influence is " + ((int)now) + " - below zero. Nothing in the game stops you spending past zero; check the Armoury log for the full daily breakdown.", true);
                    }
                }
                else if (_wasNegative)
                {
                    _wasNegative = false;
                    Log.Player("Your clan influence is back above zero.", true);
                }
            }
            catch (Exception e) { Log.Error("InfluenceWatch.DailyReport", e); }
        }
    }
}
