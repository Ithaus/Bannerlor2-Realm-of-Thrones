using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 314b (01.11, Jeff: "CZEMU TRACE INFLUENCE, to jakis blad" - podpowiedz wplywow: Lawspeakers -1, Indebted Lord -2, razem -3 na dobe).
    /// Dekompilacja BannerKings.dll: BKInfluenceModel.CalculateInfluenceChange daje rodowi gracza -2 na dobe, gdy
    /// BKCampaignStartBehavior.HasDebuff(DefaultStartOptions.IndebtedLord) - start "Indebted Lord" i startTime.ElapsedYearsUntilNow &lt; 5.
    /// BK liczyl to na rok gry 84 dni (5 lat = 420 dob, -840 wplywow); rok Armoury ma 364 dni (Calendar.cs), wiec kara trwa 1820 dob (-3640) -
    /// to nasza kolizja, nie zamysl BK. Postfiks HasDebuff (tylko IndebtedLord): kara konczy sie po 5 x 84 = 420 dobach od startu (klucz
    /// IndebtedLordVanillaYears). Lawspeakers -1 to polityka krolestwa z gry (glowa rodu bez Charyzmy) - bez zmian.
    /// Inne debuffy startow BK (HasDebuff z innymi opcjami) bez zmian. Wpiecie w kampanii (OnSessionLaunched) obok SideVillageFief.EnsureHooks.
    /// </summary>
    internal static class IndebtedSpan
    {
        private const string P = "Kara Indebted Lord (314b): ";
        internal const float VanillaDays = 5f * 84f;
        private static Harmony _harmony;
        private static bool _hooked;
        private static FieldInfo _fOption, _fStart;
        private static Type _tOpts;
        private static object _indebted;
        private static int _stumbles;

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        /// <summary>DefaultStartOptions.Instance.IndebtedLord - leniwie (BK wypelnia opcje w swoim starcie sesji; kolejnosc wobec nas niepewna).</summary>
        private static object Indebted()
        {
            if (_indebted != null || _tOpts == null) return _indebted;
            var inst = AccessTools.Property(_tOpts, "Instance")?.GetValue(null);
            _indebted = inst != null ? AccessTools.Property(_tOpts, "IndebtedLord")?.GetValue(inst) : null;
            return _indebted;
        }

        internal static void EnsureHooks()
        {
            var h = _harmony;
            if (_hooked || h == null || Campaign.Current == null) return;
            _hooked = true;
            string why;
            bool ok = false;
            try
            {
                var tB = QuartermasterLaw.FindType("BannerKings.Behaviours.BKCampaignStartBehavior");
                var tO = QuartermasterLaw.FindType("BannerKings.Managers.CampaignStart.DefaultStartOptions");
                var m = tB != null ? AccessTools.DeclaredMethod(tB, "HasDebuff") : null;
                _fOption = tB != null ? AccessTools.Field(tB, "option") : null;
                _fStart = tB != null ? AccessTools.Field(tB, "startTime") : null;
                if (m == null || _fOption == null || _fStart == null || _fStart.FieldType != typeof(CampaignTime) || tO == null)
                    why = "brak BKCampaignStartBehavior.HasDebuff / option / startTime";
                else
                {
                    _tOpts = tO;
                    if (AccessTools.Property(tO, "IndebtedLord") == null) why = "brak DefaultStartOptions.IndebtedLord";
                    else
                    {
                        var post = new HarmonyMethod(typeof(IndebtedSpan), nameof(Postfix));
                        h.Patch(m, postfix: post);
                        var pi = Harmony.GetPatchInfo(m);
                        if (pi != null && pi.Postfixes != null)
                            foreach (var p in pi.Postfixes) if (p != null && p.owner == h.Id && p.PatchMethod.DeclaringType == typeof(IndebtedSpan)) ok = true;
                        why = ok ? "wpiete" : "latka nie weszla";
                    }
                }
            }
            catch (Exception e) { why = "wyjatek " + e.GetType().Name; Log.Error("IndebtedSpan.EnsureHooks", e); }
            var c = Settings.Current;
            Log.Info(P + "postfiks BKCampaignStartBehavior.HasDebuff " + (ok ? "TAK" : "NIE") + " (" + why + ") | klucz Indebted Lord Penalty In Game Years "
                     + (c != null && c.IndebtedLordVanillaYears ? "TAK" : "NIE") + " | gracz: " + State());
        }

        /// <summary>Stan dla linii logu: start gracza i ile dob od niego.</summary>
        private static string State()
        {
            try
            {
                var tB = _fOption?.DeclaringType;
                if (tB == null || Campaign.Current == null) return "-";
                object beh = null;
                foreach (var x in Campaign.Current.GetCampaignBehaviors<CampaignBehaviorBase>())
                    if (x != null && tB.IsInstanceOfType(x)) { beh = x; break; }
                if (beh == null) return "brak zachowania startu BK";
                var opt = _fOption.GetValue(beh);
                if (opt == null) return "brak startu BK";
                if (!Equals(opt, Indebted())) return "inny start niz Indebted Lord";
                var st = (CampaignTime)_fStart.GetValue(beh);
                float d = (float)st.ElapsedDaysUntilNow;
                return "start Indebted Lord " + d.ToString("0") + " dob temu, kara " + (d < VanillaDays ? "trwa jeszcze " + (VanillaDays - d).ToString("0") + " dob" : "skonczona")
                       + " (bez latki: " + (st.ElapsedYearsUntilNow < 5f ? "trwa" : "skonczona") + ")";
            }
            catch (Exception e) { return "wyjatek " + e.GetType().Name; }
        }

        private static void Postfix(object __instance, object option, ref bool __result)
        {
            if (!__result) return;
            try
            {
                var c = Settings.Current;
                if (c == null || !c.IndebtedLordVanillaYears || !Equals(option, Indebted())) return;
                var st = (CampaignTime)_fStart.GetValue(__instance);
                if (st.ElapsedDaysUntilNow >= VanillaDays) __result = false;
            }
            catch (Exception e)
            {
                _stumbles++;
                if (_stumbles <= 3) Log.Error("IndebtedSpan.Postfix (potkniecie " + _stumbles + ")", e);
            }
        }
    }
}
