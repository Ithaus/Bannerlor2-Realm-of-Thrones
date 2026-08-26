using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// MIARKOWNIK SZARZY. RBM liczy obrazenia najechania koniem jako
    /// masa x predkosc / 70, gdzie masa = kon + jezdziec + pancerze + 100 kg.
    /// Rumak bojowy to ~670 kg, wiec galop placi ~85, a nawet WOLNE wbijanie
    /// sie w piechura miazdzy - u Jeffa "bardziej oplaca sie wbijac koniem
    /// niz walic mieczem". Postfix po prefiksie RBM: mnoznik w dol
    /// (domyslnie x0.6) plus KRZYWA PREDKOSCI - pelna stawka dopiero od
    /// pelnego galopu (7 m/s), telepanie sie stepem placi proporcjonalnie
    /// mniej. Rozpedzona szarza dalej lamie kosci, taran z miejsca - nie.
    /// </summary>
    internal static class ChargeTemperPatch
    {
        internal static void Postfix(in AttackInformation attackInformation, Vec2 attackerAgentVelocity,
            Vec2 victimAgentVelocity, ref float baseMagnitude, ref float specialMagnitude)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ChargeTemperEnabled) return;
                if (baseMagnitude <= 0f) return;

                float f = Math.Max(0f, s.ChargeDamageFactor);
                Vec2 dir = attackInformation.AttackerAgentMovementDirection;
                Vec2 rel = attackerAgentVelocity - dir * Vec2.DotProduct(victimAgentVelocity, dir);
                float speed = rel.Length;
                float full = Math.Max(1f, s.ChargeFullSpeed);
                float curve = Math.Min(1f, speed / full);   // step placi ulamek, galop pelna stawke
                baseMagnitude *= f * curve;
                specialMagnitude = baseMagnitude;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(MissionCombatMechanicsHelper), "ComputeBlowMagnitudeFromHorseCharge");
                if (m == null) { Log.Info("ChargeTemper: brak ComputeBlowMagnitudeFromHorseCharge."); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(ChargeTemperPatch).GetMethod(
                    "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
                { priority = Priority.Last });
                Log.Info("ChargeTemper: szarza konna zmiarkowana (x" +
                         (Settings.Current != null ? Settings.Current.ChargeDamageFactor : 0.6f) +
                         ", pelna stawka od " + (Settings.Current != null ? Settings.Current.ChargeFullSpeed : 7f) + " m/s).");
            }
            catch (Exception e) { Log.Error("ChargeTemper.ApplyAll", e); }
        }
    }
}
