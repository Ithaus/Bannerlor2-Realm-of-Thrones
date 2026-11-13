using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 478-S1 (noc 13.11, analiza at-120c pkt 1 - W1): ludnosc wsi BK traci ok. 7.4% na 120 dob (ok. 20% na rok 364 dob), 97% spadku to "reszta" linii A16
    /// (zmiany poza UpdatePopType). Przyczyna (dekompilacja BK PopulationData.MirrorClassCount, scratchpad/an120c/popdata.cs:414-428): kazda klasa codziennie
    /// lustrzona z BetterEconomy jako (int)BetterEconomyBridge.GetClassCount(...) - obciecie ulamka w dol. BE przeskalowuje klasy do wartosci ulamkowych
    /// (be.cs SyncClassesFromPopulation), suma BK idzie z powrotem do BE - petla traci do 1 osoby na klase na dobe (402 z 571 wsi traci 1-3 osoby dziennie).
    /// Zmiana: prefiks na MirrorClassCount (DeclaredMethod, Priority.First) liczy to samo, ale Math.Round zamiast obciecia (blad bez kierunku). Klasy bez
    /// wpisu w liscie - oryginal (tam gra tworzy wpis). Klucz BkMirrorRound (NIE = oryginal co do bitu). Licznik zaokraglen w gore / w dol dla linii A16.
    /// Zakladane w menu glownym (A16Signals.ApplyAll) - PopulationData to nie model kampanii. Potkniecie per wywolanie -> oryginal, bez wylacznika.
    /// </summary>
    internal static class BkMirrorRound478
    {
        private static MethodInfo _getCount;
        private static PropertyInfo _settlementP, _classesP, _typeP, _countP;
        private static long _up, _down, _calls;
        private static int _stumbles;

        internal static string ApplyAll(Harmony h)
        {
            var pdT = AccessTools.TypeByName("BannerKings.Managers.Populations.PopulationData");
            var brT = AccessTools.TypeByName("BannerKings.Utils.BetterEconomyBridge");
            var pcT = AccessTools.TypeByName("BannerKings.Managers.Populations.PopulationClass");
            if (pdT == null || brT == null || pcT == null) return "zaokraglenie lustra BK (478-S1): BRAK typow BK";
            var m = AccessTools.DeclaredMethod(pdT, "MirrorClassCount");
            foreach (var g in AccessTools.GetDeclaredMethods(brT))
                if (g.Name == "GetClassCount" && g.GetParameters().Length == 2) { _getCount = g; break; }
            _settlementP = AccessTools.Property(pdT, "Settlement");
            _classesP = AccessTools.Property(pdT, "classes");
            _typeP = AccessTools.Property(pcT, "type");
            _countP = AccessTools.Property(pcT, "count");
            if (m == null || _getCount == null || _settlementP == null || _classesP == null || _typeP == null || _countP == null)
                return "zaokraglenie lustra BK (478-S1): BRAK celu (MirrorClassCount / GetClassCount / pola)";
            h.Patch(m, prefix: new HarmonyMethod(typeof(BkMirrorRound478), nameof(Pre)) { priority = Priority.First });
            return "zaokraglenie lustra BK (478-S1): wpiete";
        }

        public static bool Pre(object __instance, object[] __args)
        {
            var s = Settings.Current;
            if (s == null || !s.BkMirrorRound) return true;
            try
            {
                if (__instance == null || __args == null || __args.Length < 1) return true;
                object type = __args[0];
                var st = _settlementP.GetValue(__instance, null) as Settlement;
                var list = _classesP.GetValue(__instance, null) as IList;
                if (st == null || list == null) return true;
                object pc = null;
                foreach (var c in list) { if (c != null && Equals(_typeP.GetValue(c, null), type)) { pc = c; break; } }
                if (pc == null) return true;                  // brak wpisu - oryginal tworzy go jak dotad
                float f = Convert.ToSingle(_getCount.Invoke(null, new object[] { st, type }));
                int floor = (int)f, num = (int)Math.Round(f, MidpointRounding.AwayFromZero);
                if (floor < 0) floor = 0;
                if (num < 0) num = 0;
                if (num > floor) Interlocked.Add(ref _up, num - floor);
                else if (num < floor) Interlocked.Add(ref _down, floor - num);
                Interlocked.Increment(ref _calls);
                _countP.SetValue(pc, num, null);
                return false;
            }
            catch (Exception e)
            {
                if (Interlocked.Increment(ref _stumbles) <= 3) { try { Log.Error("BkMirrorRound478.Pre", e); } catch { } }
                return true;
            }
        }

        /// <summary>Dopisek do linii A16 Ludnosc BK i zerowanie licznikow doby.</summary>
        internal static string DayText()
        {
            var s = Settings.Current;
            string t = " | zaokraglenie lustra BK (478-S1, klucz Bk Mirror Round " + (s != null && s.BkMirrorRound ? "TAK" : "NIE") + "): wywolan " + Interlocked.Exchange(ref _calls, 0)
                     + ", w gore +" + Interlocked.Exchange(ref _up, 0) + " (ile obciecie by zabralo), w dol -" + Interlocked.Exchange(ref _down, 0) + ", potkniecia " + _stumbles;
            return t;
        }
    }
}
