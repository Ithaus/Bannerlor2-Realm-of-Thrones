using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// CENA WERBUNKU HISTORYCZNIE (Jeff 04.10: "przelicz te pensje i to wszystko tak, aby mialo sens historyczny").
    /// Zold juz jest historyczny (t1 2 d ... t6 21 d dziennie wobec piechura 2 d, rycerza 24 d), ale cena werbunku
    /// byla w skali gry: wedle poziomu jednostki 10/20/50/100/200/400/600/1000/1500 (DefaultPartyWageModel:216) -
    /// tier 6 za 600 = prawie miesiac zoldu, przy broni i zbroi juz w pensach.
    /// Historycznie przy zaciagu placono zaliczke/"prest" - kilka do kilkunastu dni zoldu.
    /// Teraz: cena = dzienny zold jednostki (z modelu, z mnoznikiem BK) x `RecruitCostDays` (10), najemnicy x2;
    /// doplata za konia jak w grze (150/500, poza "bez sprzetu"); doplaty z praw BK zostaja proporcjonalnie.
    /// Postfix na kazdym modelu PartyWageModel (licznik zagniezdzenia - liczymy raz, na zewnatrz).
    /// </summary>
    internal static class RecruitCost
    {
        [ThreadStatic] private static int _depth;
        private static readonly TextObject _txt = new TextObject("{=!}Prest money (days of pay)");

        public static void Prefix() { _depth++; }
        public static Exception Finalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        public static void Postfix(CharacterObject __0, bool __2, ref ExplainedNumber __result)
        {
            if (_depth > 1) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HistoricalRecruitCost || __0 == null || __0.IsHero) return;
                int wage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(__0);
                float days = Math.Max(0f, s.RecruitCostDays);
                bool merc = __0.Occupation == Occupation.Mercenary || __0.Occupation == Occupation.Gangster || __0.Occupation == Occupation.CaravanGuard;
                if (merc) days *= 2f;
                float target = Math.Max(1f, wage * days);
                if (!__2 && __0.IsMounted) target += __0.Level < 26 ? 150f : 500f;
                float mult = 1f + __result.SumOfFactors;
                if (mult <= 0.01f) return;
                float baseNow = __result.ResultNumber / mult;
                __result.Add(target - baseNow, _txt);
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            int n = 0;
            var seen = new HashSet<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.PartyWageModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("GetTroopRecruitmentCost", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (m == null || m.DeclaringType != t || seen.Contains(t)) continue;
                        seen.Add(t);
                        h.Patch(m, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(Prefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(RecruitCost), nameof(Postfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(RecruitCost), nameof(Finalizer)));
                        n++;
                    }
                    catch { }
                }
            }
            Log.Info("RecruitCost: cena werbunku = dni zoldu w " + n + " modelach.");
        }
    }
}
