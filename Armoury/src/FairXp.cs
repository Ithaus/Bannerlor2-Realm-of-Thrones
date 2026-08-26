using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// SPRAWIEDLIWA NAUKA. RBM podmienia caly rachunek XP za cios (prefix na
    /// GetXpFromHit) i gubi przy tym dwie rzeczy naraz:
    ///  1) kary za walke cwiczebna - arena placi PELNA bitewna stawke
    ///     (vanilla daje tam 1/16, w turnieju 1/3), a do tego RBM dosypuje
    ///     XP za kazdy blok i parade, wiec arena pompuje umiejetnosci;
    ///  2) wage ciosu - plaska oplata "od machniecia" (0.4 x sily x 30),
    ///     bez skalowania obrazeniami i bez podwojnej zaplaty za zabojstwo,
    ///     wiec prawdziwa bitwa, gdzie ciosow jest malo a trupow duzo,
    ///     uczy zalosnie wolno.
    /// Stad wrazenie Jeffa "na arenie szybko, w bitwie wolno" - nic nie jest
    /// pomieszane w naszych modach, to RBM z rozmyslu tak liczy.
    /// Postfix z Priority.Last dziala PO wszystkich modelach i prostuje:
    /// arena i turniej wracaja do ulamka stawki, a w bitwie XP znow rosnie
    /// z zadanymi obrazeniami i podwaja sie za sciecie wroga.
    /// </summary>
    internal static class FairXpPatch
    {
        internal static void Postfix(CharacterObject attackedTroop, int damage, bool isFatal,
            CombatXpModel.MissionTypeEnum missionType, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CombatXpFixEnabled) return;
                if (__result.ResultNumber <= 0f) return;

                if (missionType == CombatXpModel.MissionTypeEnum.PracticeFight)
                {
                    __result.AddFactor(MBMath.ClampFloat(s.ArenaXpPercent / 100f, 0f, 2f) - 1f);
                    return;
                }
                if (missionType == CombatXpModel.MissionTypeEnum.Tournament)
                {
                    __result.AddFactor(MBMath.ClampFloat(s.TournamentXpPercent / 100f, 0f, 2f) - 1f);
                    return;
                }
                if (missionType != CombatXpModel.MissionTypeEnum.Battle &&
                    missionType != CombatXpModel.MissionTypeEnum.SimulationBattle) return;
                if (!s.BattleXpScalesWithDamage || attackedTroop == null) return;

                // RBM placi plasko "30 za cios". Wracamy do vanilla proporcji:
                // XP goni obrazenia (do maksimum zdrowia ofiary), a zabojstwo
                // placi drugi raz pelne zdrowie. Blok i parada (0 obrazen)
                // ucza dalej, ale za cwierc stawki - dloniom tez cos sie nalezy.
                int hp = Math.Max(1, attackedTroop.MaxHitPoints());
                float weight = (Math.Min(damage, hp) + (isFatal ? hp : 0)) / 30f;
                __result.AddFactor(MBMath.ClampFloat(weight, 0.25f, 8f) - 1f);
            }
            catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var post = new HarmonyMethod(typeof(FairXpPatch).GetMethod(
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
                        if (t == null || t.IsAbstract || !typeof(CombatXpModel).IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = t.GetMethod("GetXpFromHit", BindingFlags.Public | BindingFlags.NonPublic |
                                                                BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            harmony.Patch(m, postfix: post);
                            done++;
                        }
                        catch (Exception e) { Log.Error("FairXp.Patch(" + t.Name + ")", e); }
                    }
                }
                Log.Info("FairXp: nauka z ciosu wyrownana w " + done + " modelach (arena " +
                         (Settings.Current != null ? Settings.Current.ArenaXpPercent : 20f) + "%, turniej " +
                         (Settings.Current != null ? Settings.Current.TournamentXpPercent : 50f) + "%, bitwa wg obrazen).");
            }
            catch (Exception e) { Log.Error("FairXp.ApplyAll", e); }
        }
    }
}
