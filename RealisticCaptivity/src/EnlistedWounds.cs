using System;
using HarmonyLib;

namespace RealisticCaptivity
{
    /// <summary>
    /// Uczciwe rany w sluzbie. ROT po KAZDEJ bitwie zaciezного leczy do pelna
    /// (HealEnlistedHeroes(100)) - wychodzisz ledwo zywy, a masz 100% zdrowia.
    /// U nas: po bitwie tylko opatrunek polowy (kilka HP), dzienna opieka
    /// medyka obozowego tez przycieta. Reszta goi sie jak u ludzi - odpoczynkiem.
    /// Patch miekki przez refleksje - bez ROT-a nic sie nie dzieje.
    /// </summary>
    internal static class EnlistedWounds
    {
        internal static void Install(Harmony harmony)
        {
            try
            {
                if (!Settings.Current.EnlistedHonestWounds) return;
                var t = Type.GetType("ROT.CampaignBehaviors.ROTEnlistmentBehavior, ROT");
                if (t == null) return;
                var m = AccessTools.Method(t, "HealEnlistedHeroes");
                if (m == null) { Log.Info("EnlistedWounds: ROT.HealEnlistedHeroes nie znaleziona."); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(EnlistedWounds), "ClampHeal"));
                Log.Info("EnlistedWounds: cudowne leczenie po bitwie przyciete (opatrunek "
                         + Settings.Current.EnlistedPostBattleHealHp + " HP, medyk "
                         + Settings.Current.EnlistedDailyCareHp + " HP/dzien).");
            }
            catch (Exception e) { Log.Error("EnlistedWounds.Install", e); }
        }

        public static void ClampHeal(ref int amount)
        {
            try
            {
                var s = Settings.Current;
                if (!s.EnlistedHonestWounds) return;
                // ROT wola 100 po bitwie i 33 w codziennej opiece
                int cap = amount >= 100 ? s.EnlistedPostBattleHealHp : s.EnlistedDailyCareHp;
                if (cap < 0) cap = 0;
                if (amount > cap) amount = cap;
            }
            catch { }
        }
    }
}
