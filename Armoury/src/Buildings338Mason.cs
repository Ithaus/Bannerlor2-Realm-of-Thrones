using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 4 - MURARZ (Mason miasta +3/6/9, zamku +2/4/6 punktow budowy na dobe; ConstructionPerDay).
    /// BKConstructionModel:431 czyta efekt, ale BuildFunding.PowerPostfix / PowerIntPostfix (Priority.Last) w osadach objetych rozliczeniem (Covered - kazdy pan AI
    /// i gracz przy PaidConstructionPlayer) ZASTEPUJA wynik oplacona praca - Mason martwy. Budowa w Armoury to oplacona praca, wiec Mason pracuje tylko przy
    /// oplaconej budowie i daje najwyzej swoje punkty z opisu: dopisek = min(punkty Masona, punkty pana + robota ludu) (recenzja 338 W1 - bylo k x praca,
    /// do 10x opisu).
    /// Punkty Masona - tylko budynki SettlementMason / CastleMason (nie AddEffectOfBuildings - punkty kopalni BK spoza opisu nie wracaja).
    /// Czyste (bez stanu) - wolaja to tez dymki. Dymek i postep doby licza ten sam dopisek (bez surowej mocy BK). EngPaidNeed odejmuje dopisek od potrzeby.
    /// Galaz nieobjeta (lenna gracza bez PaidConstructionPlayer) - nic, bo tam BK liczy Masona sam.
    /// </summary>
    internal static class Buildings338Mason
    {
        private static TextObject _txt;

        internal static void SessionStart() { }

        /// <summary>Punkty Masona w modelu gry (tylko typy Mason, perki modelu efektow wchodza same).</summary>
        private static float MasonPoints(Town t)
        {
            var e = new ExplainedNumber(0f);
            foreach (var b in t.Buildings)
                if (b != null && (b.BuildingType == DefaultBuildingTypes.SettlementMason || b.BuildingType == DefaultBuildingTypes.CastleMason))
                    b.AddEffectOfBuilding(BuildingEffectEnum.ConstructionPerDay, ref e);
            return e.ResultNumber;
        }

        /// <summary>Punkty Masona tej osady (0 - klucz wylaczony / brak Masona).</summary>
        internal static float Points(Town t)
        {
            if (t == null || !Buildings338.MasonOn) return 0f;
            try { float m = MasonPoints(t); return m > 0f && !float.IsNaN(m) ? m : 0f; }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.Points", e); return 0f; }
        }

        /// <summary>BuildFunding.PowerPostfix (galaz Covered): pozycja dymku "Mason" = min(punkty Masona, punkty pana + robota ludu + przyspieszenie zlotem (347e)).</summary>
        internal static void Add(Town t, float paid, ref ExplainedNumber r)
        {
            float x = Extra(t, paid);
            if (!(x > 0f)) return;
            if (_txt == null) _txt = new TextObject("{=!}Mason (works with the paid builders)");
            r.Add(x, _txt);
        }

        /// <summary>BuildFunding.PowerIntPostfix (galaz Covered) i dymek: ten sam dopisek.</summary>
        internal static float Extra(Town t, float paid)
        {
            if (!(paid > 0f)) return 0f;
            float m = Points(t);
            return m < paid ? m : paid;
        }

        internal static void Daily(int day)
        {
            int n = 0, over = 0; double pts = 0, mSum = 0;
            if (Buildings338.MasonOn)
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        if (t.BuildingsInProgress == null || t.BuildingsInProgress.Count == 0) continue;
                        float m = MasonPoints(t);
                        if (!(m > 0f)) continue;
                        float a = BuildFunding.MasonDay(t);
                        if (!(a > 0f)) continue;
                        n++; pts += a; mSum += m; if (a > m + 0.01f) over++;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.Daily", e); }
                }
            Log.Info("Budynki (338) murarz: dzien " + day + " | osad " + n + " z Masonem w budowach oplaconych | punktow +" + pts.ToString("0.0", Buildings338.Inv) + "/d (punkty Masona "
                     + mSum.ToString("0.0", Buildings338.Inv) + ", dopisek ponad punkty " + over + ")" + Buildings338.Tail(Buildings338.GMason));
        }
    }
}
