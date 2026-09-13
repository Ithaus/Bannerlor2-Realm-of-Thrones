using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// LICZNIK AMUNICJI (Jeff 13.09: "wrzucilem im strzaly, mialy 29/30, a po chwili
    /// znowu im cos zzarlo"). Czytanie kodu wyczerpalo sie bez odpowiedzi: wykluczone
    /// zostaly nasza latka na DTE (IsAmmoAndEmpty ma JEDEN punkt wywolania i tylko
    /// DODAJE kolczany), nasz AmmoAttrition (zero wpisow w logu tej sesji), zlomowanie
    /// DTE (ScrapArmyArmoryByCategory tnie dopiero powyzej 600 sztuk na kategorie,
    /// a tu chodzi o kilkanascie), SanitizeInPlace (scala i normalizuje, nie kasuje
    /// rozwiazywalnych pozycji) i RestoreReadyArmoryItems (tylko dodaje).
    /// Zamiast zgadywac dalej - MIERZYMY. Co godzine gry czytamy stan zbrojowni DTE
    /// dla amunicji i uprzezy; kazda ZMIANA lci do logu z data, godzina, miejscem
    /// pobytu i tym, czy gracz siedzi w ekranie zbrojowni. Po jednej sesji bedzie
    /// widac, o ktorej i o ile spada - a to wskaze sprawce.
    /// Modul jest czysto diagnostyczny: nic nie zmienia w grze, tylko pisze.
    /// </summary>
    internal static class AmmoTracer
    {
        private static readonly ItemObject.ItemTypeEnum[] Watched =
        {
            ItemObject.ItemTypeEnum.Arrows,
            ItemObject.ItemTypeEnum.Bolts,
            ItemObject.ItemTypeEnum.HorseHarness
        };

        private static readonly Dictionary<ItemObject.ItemTypeEnum, int> _last =
            new Dictionary<ItemObject.ItemTypeEnum, int>();

        internal static void HourlyCheck()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.AmmoTracerEnabled) return;
                if (Campaign.Current == null) return;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;

                foreach (var type in Watched)
                {
                    int now = QuartermasterLaw.HaveFor(armory, type);
                    int before;
                    if (!_last.TryGetValue(type, out before)) { _last[type] = now; continue; }
                    if (now == before) continue;
                    _last[type] = now;

                    string where = "w polu";
                    try
                    {
                        var st = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
                        if (st != null) where = st.Name.ToString();
                    }
                    catch { }
                    Log.Info("SLED AMUNICJI: " + type + " " + before + " -> " + now
                             + " (" + (now - before > 0 ? "+" : "") + (now - before) + ")"
                             + " | dzien " + ((int)CampaignTime.Now.ToDays)
                             + " | " + where
                             + " | ekran zbrojowni otwarty: " + QuartermasterEscrow.Active);
                }
            }
            catch (Exception e) { Log.Error("AmmoTracer", e); }
        }
    }
}
