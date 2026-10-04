using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// POBOR OD LUDNOSCI - krok 1: KTO SIE ZGLASZA (Jeff 04.10: "teraz nie ma losowosci - pewnie ci,
    /// co nie maja pracy, albo ci, co chca opuscic wioske - jaka bedzie zasada werbunku?").
    /// docs/PLAN-POBOR.md, pkt 2.1.
    ///
    /// BannerKings juz zabiera rekruta z ludnosci osady (MilitaryData.DeduceManpower) i BEE liczy
    /// produkcje wsi od liczby chlopow - pobor ma juz koszt w ludziach i plonach. Ale puste miejsce
    /// u notabla wypelnia sie z szansa ~0.5 DZIENNIE (BKVolunteerModel.GetDraftEfficiency), wiec
    /// ochotnicy sa zawsze, niezaleznie od tego, czy wies ma ludzi bez zajecia.
    ///
    /// Zasada: szansa BK x checi, gdzie checi =
    ///   `RecruitBaseWilling` (mlodsi synowie - zawsze ktos sie znajdzie, ale rzadko)
    ///   + nadwyzka rak (BK LandData.WorkforceExcess / AvailableWorkForce - ludzie, dla ktorych nie ma
    ///     pracy na polach i pastwiskach) x `RecruitExcessWeight`
    ///   + nedza regionu (ta sama co u wyrzutkow: bieda, wojna, spalone wsie, glod, bezprawie)
    ///     x `RecruitMiseryWeight` - ci, co chca odejsc.
    /// Liczone raz dziennie na osade (cache).
    /// </summary>
    internal static class Levy
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.LevyEnabled; } }

        private static object _popMgr;
        private static MethodInfo _getPopData;
        private static PropertyInfo _landData, _excess, _available;
        private static readonly Dictionary<Settlement, KeyValuePair<int, float>> _cache = new Dictionary<Settlement, KeyValuePair<int, float>>();
        private static int _dayStamp = -1, _calls;
        private static float _sumW;

        internal static void Reset() { _cache.Clear(); _popMgr = null; _dayStamp = -1; _calls = 0; _sumW = 0f; }

        private static bool Resolve()
        {
            if (_getPopData != null && _popMgr != null) return true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance")?.GetValue(null, null) : null;
                _popMgr = inst != null ? AccessTools.Field(cfgT, "PopulationManager")?.GetValue(inst) ?? AccessTools.Property(cfgT, "PopulationManager")?.GetValue(inst, null) : null;
                if (_popMgr == null) return false;
                _getPopData = AccessTools.Method(_popMgr.GetType(), "GetPopData", new[] { typeof(Settlement) });
                return _getPopData != null;
            }
            catch { return false; }
        }

        /// <summary>Udzial ludzi bez pracy na roli (0..1) wedle BK; -1 gdy brak danych.</summary>
        internal static float ExcessShare(Settlement st)
        {
            try
            {
                if (st == null || !Resolve()) return -1f;
                var data = _getPopData.Invoke(_popMgr, new object[] { st });
                if (data == null) return -1f;
                if (_landData == null) _landData = AccessTools.Property(data.GetType(), "LandData");
                var land = _landData != null ? _landData.GetValue(data, null) : null;
                if (land == null) return -1f;
                if (_excess == null) { _excess = AccessTools.Property(land.GetType(), "WorkforceExcess"); _available = AccessTools.Property(land.GetType(), "AvailableWorkForce"); }
                if (_excess == null || _available == null) return -1f;
                float ex = Convert.ToSingle(_excess.GetValue(land, null));
                float av = Convert.ToSingle(_available.GetValue(land, null));
                if (av <= 0f) return 0f;
                return MBMath.ClampFloat(ex / av, 0f, 1f);
            }
            catch { return -1f; }
        }

        internal static float Willingness(Settlement st)
        {
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays;
            if (_dayStamp != day) { Flush(); _dayStamp = day; }
            KeyValuePair<int, float> c;
            if (_cache.TryGetValue(st, out c) && c.Key == day) return c.Value;
            float ex = ExcessShare(st);
            if (ex < 0f) ex = 0.2f;                                    // brak danych BK - przecietnie
            float w = s.RecruitBaseWilling + ex * s.RecruitExcessWeight + OutlawLaw.MiseryOf(st) * s.RecruitMiseryWeight;
            w = MBMath.ClampFloat(w, 0f, Math.Max(0f, s.RecruitWillingMax));
            _cache[st] = new KeyValuePair<int, float>(day, w);
            return w;
        }

        public static void ProbabilityPostfix(Hero __0, int __1, Settlement __2, ref float __result)
        {
            try
            {
                if (!On || __2 == null) return;
                float w = Willingness(__2);
                __result *= w;
                _calls++; _sumW += w;
            }
            catch { }
        }

        private static void Flush()
        {
            if (_dayStamp < 0 || _calls == 0) return;
            Log.Info("Pobor: dzien " + _dayStamp + " - chec do sluzby srednio x" + (_sumW / _calls).ToString("0.00")
                     + " szansy BK (" + _calls + " losowan miejsc u notabli; nadwyzka rak + nedza regionu).");
            _calls = 0; _sumW = 0f;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKVolunteerModel");
                MethodInfo m = null;
                if (t == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { foreach (var x in asm.GetTypes()) if (x.Name == "BKVolunteerModel") { t = x; break; } } catch { }
                        if (t != null) break;
                    }
                }
                if (t != null) m = AccessTools.Method(t, "GetDailyVolunteerProductionProbability", new[] { typeof(Hero), typeof(int), typeof(Settlement) });
                if (m == null) m = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel), "GetDailyVolunteerProductionProbability", new[] { typeof(Hero), typeof(int), typeof(Settlement) });
                if (m != null) h.Patch(m, postfix: new HarmonyMethod(typeof(Levy), nameof(ProbabilityPostfix)));
                Log.Info("Levy: kto sie zglasza (nadwyzka rak + nedza) " + (m != null ? "wpiete w " + m.DeclaringType.Name : "BRAK modelu ochotnikow") + ".");
            }
            catch (Exception e) { Log.Error("Levy.ApplyAll", e); }
        }
    }
}
