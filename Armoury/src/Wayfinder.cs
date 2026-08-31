using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// NAWIGATOR KURSU (Jeff 31.08: "klikam gdzie jechac - znaczek na mapie
    /// i ile to km i godzin marszu"). Po kazdym wskazaniu celu: meldunek
    /// z dystansem PO TRASIE (MapDistanceModel, nie linia prosta),
    /// kilometrami w skali lore (4.75 km/jedn. - kalibracja Mur->Sunspear
    /// = 3000 mil), godzinami w siodle i dniami drogi (z 6 h snu na dobe).
    /// Cel-osada dostaje FLAGE sledzenia vanilla (te od questow) + strzalke
    /// na kompasie; flaga schodzi po dotarciu albo przy nowym kursie.
    /// Kreski trasy silnik nie rysuje bez grzebania w renderze mapy -
    /// flaga + meldunek daja kurs, dystans i czas.
    /// </summary>
    internal static class Wayfinder
    {
        internal const float KmPerUnit = 4.75f;
        private static Settlement _flagged;
        private static DateTime _lastMsg = DateTime.MinValue;
        private static string _lastDest = "";

        public static void GoPointPostfix(MobileParty __instance, CampaignVec2 point)
        {
            try { if (__instance == MobileParty.MainParty) Report(null, point); } catch { }
        }

        public static void GoSettlementPostfix(MobileParty __instance, Settlement settlement)
        {
            try
            {
                if (__instance == MobileParty.MainParty && settlement != null)
                    Report(settlement, new CampaignVec2(settlement.GetPosition2D, false));
            }
            catch { }
        }

        private static void Report(Settlement dest, CampaignVec2 point)
        {
            var s = Settings.Current;
            if (s == null || !s.CoursePlotterEnabled) return;
            var main = MobileParty.MainParty;
            if (main == null || Campaign.Current == null) return;

            string destKey = dest != null ? dest.StringId : "pt";
            bool same = destKey == _lastDest && (DateTime.Now - _lastMsg).TotalSeconds < 2;
            _lastDest = destKey; _lastMsg = DateTime.Now;

            float lr;
            float dist = dest != null
                ? Campaign.Current.Models.MapDistanceModel.GetDistance(main, dest, false, MobileParty.NavigationType.Default, out lr)
                : Campaign.Current.Models.MapDistanceModel.GetDistance(main, point, MobileParty.NavigationType.Default, out lr);
            if (dist <= 0.01f || float.IsNaN(dist) || float.IsInfinity(dist)) return;

            if (!same)
            {
                float speed = MathF.Max(0.5f, main.Speed);
                float rideH = dist / speed;
                float clockH = rideH * 24f / 18f;              // 6 h snu na dobe
                float days = clockH / 24f;
                int km = (int)Math.Round(dist * KmPerUnit);
                string name = dest != null ? dest.Name.ToString() : "the marked ground";
                Log.Player("Course set for " + name + ": ~" + km + " km - about "
                           + (int)Math.Round(rideH) + " h in the saddle, "
                           + days.ToString("0.#") + " days on the road.", false);
            }
            Flag(dest);
        }

        private static void Flag(Settlement dest)
        {
            try
            {
                var vt = Campaign.Current.VisualTrackerManager;
                if (vt == null) return;
                if (_flagged != null && _flagged != dest)
                { vt.RemoveTrackedObject(_flagged); _flagged = null; }
                if (dest == null || _flagged == dest) return;
                if (vt.CheckTracked(dest)) return;   // questowa flaga - nie dublujemy i nie ruszamy
                vt.RegisterObject(dest);
                _flagged = dest;
            }
            catch { }
        }

        /// <summary>Dotarles - flaga schodzi (wolane z ArmouryBehavior).</summary>
        internal static void OnSettlementEntered(Settlement st)
        {
            try
            {
                if (_flagged == null || st != _flagged) return;
                Campaign.Current.VisualTrackerManager.RemoveTrackedObject(_flagged);
                _flagged = null;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var mP = AccessTools.Method(typeof(MobileParty), "SetMoveGoToPoint");
                var mS = AccessTools.Method(typeof(MobileParty), "SetMoveGoToSettlement");
                if (mP != null) h.Patch(mP, postfix: new HarmonyMethod(typeof(Wayfinder), "GoPointPostfix"));
                if (mS != null) h.Patch(mS, postfix: new HarmonyMethod(typeof(Wayfinder), "GoSettlementPostfix"));
                Log.Info("Wayfinder: meldunek kursu (km, godziny, dni) + flaga celu na mapie.");
            }
            catch (Exception e) { Log.Error("Wayfinder.ApplyAll", e); }
        }
    }
}
