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
    /// i gracz przy PaidConstructionPlayer) ZASTEPUJA wynik oplacona praca - Mason martwy. Budowa w Armoury to oplacona praca, wiec Mason zwieksza wydajnosc tej
    /// pracy, a bez zaplaty nic nie daje: k = mason / max(5, surowa moc BK bez Masona), k &lt;= 1 (najwyzej podwaja prace); dopisek = k x (punkty pana + robota ludu).
    /// mason - tylko budynki SettlementMason / CastleMason (nie AddEffectOfBuildings - punkty kopalni BK spoza opisu nie wracaja); surowa moc BK - wynik
    /// CalculateDailyConstructionPowerWithoutBoost lancucha (wolany zagniezdzony: nasz licznik _depth > 1 przepuszcza wynik BK bez podmiany).
    /// Czyste (bez stanu) - wolaja to tez dymki. Dymek i postep doby (PowerIntPostfix) licza ten sam k. EngPaidNeed dzieli dodatkowo przez (1 + k).
    /// Galaz nieobjeta (lenna gracza bez PaidConstructionPlayer) - nic, bo tam BK liczy Masona sam.
    /// </summary>
    internal static class Buildings338Mason
    {
        private const float MinBase = 5f;
        [ThreadStatic] private static bool _skip;
        [ThreadStatic] private static float _lastK;   // k z ostatniego Add (linia doby czyta go po t.Construction - K poza PowerPostfix dostalby wynik Armoury, nie BK)
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

        /// <summary>Wspolczynnik Masona k (0..1). raw - surowa moc BK bez przyspieszenia (int lancucha), &lt; 0 = policz zagniezdzonym wywolaniem.</summary>
        internal static float K(Town t, int raw)
        {
            if (_skip || t == null || !Buildings338.MasonOn) return 0f;
            try
            {
                float m = MasonPoints(t);
                if (!(m > 0f)) return 0f;
                if (raw < 0) raw = BuildFunding.RawPower(t);   // surowy wynik lancucha (BK) - postfiksy BuildFunding go przepuszczaja
                float k = m / Math.Max(MinBase, raw - m);
                return k > 1f ? 1f : (k > 0f && !float.IsNaN(k) ? k : 0f);
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.K", e); return 0f; }
        }

        /// <summary>BuildFunding.PowerPostfix (galaz Covered): pozycja dymku "Mason" = k x (punkty pana + robota ludu).</summary>
        internal static void Add(Town t, float paid, ref ExplainedNumber r)
        {
            if (!(paid > 0f)) return;
            float k = K(t, -1);
            _lastK = k;
            if (!(k > 0f)) return;
            if (_txt == null) _txt = new TextObject("{=!}Mason (more work from the paid builders)");
            r.Add(k * paid, _txt);
        }

        /// <summary>BuildFunding.PowerIntPostfix (galaz Covered): ten sam dopisek; raw - wynik BK tej metody przed podmiana.</summary>
        internal static float Extra(Town t, float paid, int raw)
        {
            if (!(paid > 0f)) return 0f;
            return K(t, raw) * paid;
        }

        internal static void Daily(int day)
        {
            int n = 0; double pts = 0, kSum = 0; float kMax = 0f;
            if (Buildings338.MasonOn)
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        if (t.BuildingsInProgress == null || t.BuildingsInProgress.Count == 0 || !(MasonPoints(t) > 0f)) continue;
                        _lastK = 0f;
                        float a = t.Construction, b;
                        float k = _lastK;
                        bool was = _skip; _skip = true;
                        try { b = t.Construction; } finally { _skip = was; }
                        if (!(a > b)) continue;
                        n++; pts += a - b; kSum += k; if (k > kMax) kMax = k;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.Daily", e); }
                }
            Log.Info("Budynki (338) murarz: dzien " + day + " | osad " + n + " z Masonem w budowach oplaconych | punktow +" + pts.ToString("0.0", Buildings338.Inv) + "/d (sredni k "
                     + (n > 0 ? (kSum / n).ToString("0.000", Buildings338.Inv) : "0") + ", max k " + kMax.ToString("0.000", Buildings338.Inv) + ")" + Buildings338.Tail(Buildings338.GMason));
        }
    }
}
