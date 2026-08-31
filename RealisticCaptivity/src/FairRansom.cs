using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace RealisticCaptivity
{
    /// <summary>
    /// UCZCIWY HANDLARZ OKUPOW. "Choose the prisoners to be ransomed" placi
    /// przez RansomValueCalculationModel, a BannerKings podmienia tam wycene
    /// szeregowych jencow na CENE NIEWOLNIKA w danym miescie (polityka
    /// "Enslavement" - domyslna). Miasto zalane niewolnikami placi ZERO,
    /// wiec Jeff oddawal jencow za darmo i zadne zloto nie przychodzilo.
    /// Postfix po wszystkich modelach: szeregowy jeniec nigdy nie schodzi
    /// ponizej starej stawki posrednika (cwierc kosztu rekrutacji).
    /// Bohaterowie (lordowie) - bez zmian, ich okupy licza sie osobno.
    /// </summary>
    internal static class FairRansomPatch
    {
        internal static void Postfix(CharacterObject prisoner, Hero sellerHero, ref int __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.PrisonerSaleFloor) return;
                if (prisoner == null || prisoner.IsHero) return;

                int recruit = Campaign.Current.Models.PartyWageModel
                    .GetTroopRecruitmentCost(prisoner, null).RoundedResultNumber;
                int floor = (int)(recruit * 0.25f * Math.Max(0f, s.PrisonerSaleFloorFactor));
                if (floor < 1) floor = 1;
                if (__result < floor) __result = floor;

                // GEOGRAFIA HANDLU ZYWYM TOWAREM (Jeff 31.08: "robimy jencow").
                // W Westeros niewolnictwo jest ZAKAZANE - jenca kupi tylko paser
                // za ulamek ceny; w Essos (za Waskim Morzem) targi niewolnikow
                // placa pelna stawke. Granica po mapie: X > 770 = Essos
                // (Sunspear 689, Braavos 857 - miedzy nimi Waskie Morze).
                // Tylko sprzedaz GRACZA w osadzie; wyceny AI w tle nietkniete.
                if (s.PrisonerGeoSale && TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement != null
                    && (sellerHero == null || sellerHero == Hero.MainHero))
                {
                    var st = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
                    bool essos = st.GetPosition2D.X > 770f;
                    if (!essos)
                    {
                        int cut = (int)(__result * Math.Max(0, Math.Min(100, s.WesterosFencePercent)) / 100f);
                        __result = Math.Max(1, cut);
                    }
                }
            }
            catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var post = new HarmonyMethod(typeof(FairRansomPatch).GetMethod(
                    "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
                { priority = Priority.Last };

                int done = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }

                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(RansomValueCalculationModel).IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = t.GetMethod("PrisonerRansomValue", BindingFlags.Public | BindingFlags.NonPublic |
                                                                       BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            harmony.Patch(m, postfix: post);
                            done++;
                        }
                        catch (Exception e) { Log.Error("FairRansom.Patch(" + t.Name + ")", e); }
                    }
                }
                Log.Info("FairRansom: cena szeregowego jenca ma podloge (broker rate) w " + done + " modelach.");
            }
            catch (Exception e) { Log.Error("FairRansom.ApplyAll", e); }
        }
    }
}
