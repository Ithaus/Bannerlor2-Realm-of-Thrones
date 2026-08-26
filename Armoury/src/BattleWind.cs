using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// ODDECH BITEWNY BOHATEROW (Jeff, 26.08: "po 10 machnieciach nie mam sily
    /// przy 150 atletyki"). RBM liczy pule postury/staminy niemal bez udzialu
    /// statystyk (atletyka 0 vs 150 = 6 pkt puli postury), a regeneracja ma
    /// gumke DO GORY (im pusciej, tym szybciej) - odwrotnie niz zadyszka.
    /// Tu, TYLKO dla bohaterow (gracz, lordowie, towarzysze; szeregowi zostaja
    /// na czystym RBM):
    ///   pula (postura I stamina) x 2^((END - 2.5) / BattleEndDoubleEvery)
    ///     - podwaja sie co pare pkt Endurance: END 5 = x2, END 10 = x8;
    ///   regeneracja staminy wykladniczo od Endurance:
    ///     R = R1 * (R10/R1)^((END-1)/9) pkt/s, razy (1 + Atletyka/500),
    ///     ciezki korpus (>=30 pancerza) tnie na pol jak w RBM;
    ///   zadyszka zamiast gumki: tempo x (floor + (1-floor) * stan/max)
    ///     - pusty pasek oddycha na BattleWindedFloor (25%) tempa.
    /// Wszystko przez Harmony na RBMAI.Stance, zero zmian w plikach RBM.
    /// </summary>
    internal static class BattleWind
    {
        /// <summary>True = patche weszly; FieldCraft wtedy NIE dokleja starego
        /// liniowego bonusu Endurance (bo bylby podwojny).</summary>
        internal static bool Active;

        // regen RBM biega co 0,5 s z tickCount=30 - stad przelicznik pkt/s -> add
        private const float RbmTickSeconds = 0.5f;
        private const int RbmTickCount = 30;

        private sealed class Profile
        {
            public float StaminaRegenPerSec;   // pelne tempo (bez zadyszki)
        }

        // Stance bohatera -> jego profil; NPC-ow tu nie ma i ida oryginalem
        private static readonly ConditionalWeakTable<object, Profile> Managed =
            new ConditionalWeakTable<object, Profile>();

        private static FieldInfo _fStamina, _fMaxStamina, _fStaminaRegen;
        private static FieldInfo _fPosture, _fMaxPosture, _fPostureRegen;
        private static MethodInfo _mAddStamina, _mAddPosture;

        internal static bool Manages(object stance)
        {
            Profile p;
            return stance != null && Managed.TryGetValue(stance, out p);
        }

        /// <summary>Mnoznik puli z Endurance: 2^((END - 2.5) / DoubleEvery).</summary>
        internal static float EnduranceMul(Hero hero)
        {
            try
            {
                if (hero == null) return 1f;
                var c = Settings.Current;
                float every = Math.Max(0.5f, c.BattleEndDoubleEvery);
                float end = hero.GetAttributeValue(DefaultCharacterAttributes.Endurance);
                return (float)Math.Pow(2.0, (end - 2.5f) / every);
            }
            catch { return 1f; }
        }

        private static Hero HeroOf(Agent agent)
        {
            try
            {
                var co = agent != null ? agent.Character as CharacterObject : null;
                return co != null && co.IsHero ? co.HeroObject : null;
            }
            catch { return null; }
        }

        /// <summary>Regeneracja staminy w pkt/s wg Endurance (wykladniczo R1 -> R10).</summary>
        private static float RegenPerSec(Hero hero, Agent agent)
        {
            var c = Settings.Current;
            float r1 = Math.Max(1f, c.BattleRegenAtEnd1);
            float r10 = Math.Max(r1, c.BattleRegenAtEnd10);
            float end = 1f;
            try { end = hero.GetAttributeValue(DefaultCharacterAttributes.Endurance); } catch { }
            float r = r1 * (float)Math.Pow(r10 / r1, (Clamp(end, 1f, 10f) - 1f) / 9f);
            float ath = 0f;
            try
            {
                var co = agent.Character as CharacterObject;
                if (co != null) ath = co.GetSkillValue(DefaultSkills.Athletics);
            }
            catch { }
            r *= 1f + Math.Min(ath, 500f) / 500f;
            // ciezki korpus dusi oddech - ta sama granica co w RBM
            try
            {
                var body = agent.SpawnEquipment[EquipmentIndex.Body];
                if (!body.IsEmpty && body.GetModifiedBodyArmor() >= 30f) r *= 0.5f;
            }
            catch { }
            return r;
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private static float Winded(float current, float max)
        {
            float floor = Clamp(Settings.Current.BattleWindedFloor, 0.05f, 1f);
            float frac = max > 1f ? Clamp(current / max, 0f, 1f) : 1f;
            return floor + (1f - floor) * frac;
        }

        // ---- patche ----

        /// <summary>Po RBM-owym przeliczeniu puli staminy: bohater dostaje pule x END i nasz regen.</summary>
        public static void StaminaInitPostfix(Agent agent, object stance)
        {
            try
            {
                var hero = HeroOf(agent);
                if (hero == null || stance == null) return;
                float mul = EnduranceMul(hero);
                float max = (float)_fMaxStamina.GetValue(stance) * mul;
                _fMaxStamina.SetValue(stance, max);
                _fStamina.SetValue(stance, max);          // na starcie pelna, jak w RBM
                var p = new Profile { StaminaRegenPerSec = RegenPerSec(hero, agent) };
                // nadpisz takze perTick RBM - gdyby cos poza naszym prefixem z niego czytalo
                _fStaminaRegen.SetValue(stance, p.StaminaRegenPerSec * RbmTickSeconds / RbmTickCount);
                Profile old; if (Managed.TryGetValue(stance, out old)) Managed.Remove(stance);
                Managed.Add(stance, p);
                if (agent.IsPlayerControlled)
                    Log.Info("BattleWind: gracz END-mul " + mul.ToString("0.00") + ", stamina "
                             + (int)max + ", regen " + (int)p.StaminaRegenPerSec + " pkt/s.");
            }
            catch (Exception e) { Log.Error("BattleWind.StaminaInit", e); }
        }

        /// <summary>
        /// Po RBM-owym przeliczeniu postury (takze przy KAZDEJ zmianie broni -
        /// RBM liczy max od zera, wiec mnozymy za kazdym razem od nowa).
        /// Regen postury skalujemy TYM SAMYM mnoznikiem co pule - pasek jest
        /// wiekszy, ale wraca w tym samym czasie co w RBM; balans wymian zostaje.
        /// </summary>
        public static void PostureInitPostfix(Agent agent, object stance)
        {
            try
            {
                var hero = HeroOf(agent);
                if (hero == null || stance == null) return;
                float mul = EnduranceMul(hero);
                _fMaxPosture.SetValue(stance, (float)_fMaxPosture.GetValue(stance) * mul);
                _fPosture.SetValue(stance, (float)_fPosture.GetValue(stance) * mul);
                _fPostureRegen.SetValue(stance, (float)_fPostureRegen.GetValue(stance) * mul);
            }
            catch (Exception e) { Log.Error("BattleWind.PostureInit", e); }
        }

        /// <summary>Zadyszka zamiast gumki RBM: pusty pasek oddycha wolno, pelny szybko.</summary>
        public static bool TickStaminaPrefix(object __instance, int tickCount, float multiplier)
        {
            try
            {
                Profile p;
                if (!Managed.TryGetValue(__instance, out p)) return true;   // NPC - oryginal
                float cur = (float)_fStamina.GetValue(__instance);
                float max = (float)_fMaxStamina.GetValue(__instance);
                float add = p.StaminaRegenPerSec * RbmTickSeconds * (tickCount / (float)RbmTickCount)
                          * multiplier * Winded(cur, max);
                _mAddStamina.Invoke(__instance, new object[] { add });
                return false;
            }
            catch { return true; }
        }

        /// <summary>Postura bohatera tez lapie zadyszke (RBM nie mial tu zadnej krzywej).</summary>
        public static bool TickPosturePrefix(object __instance, int tickCount, float multiplier)
        {
            try
            {
                Profile p;
                if (!Managed.TryGetValue(__instance, out p)) return true;
                float cur = (float)_fPosture.GetValue(__instance);
                float max = (float)_fMaxPosture.GetValue(__instance);
                float add = (float)_fPostureRegen.GetValue(__instance) * tickCount
                          * multiplier * Winded(cur, max);
                _mAddPosture.Invoke(__instance, new object[] { add });
                return false;
            }
            catch { return true; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.BattleStaminaEnabled) { Log.Info("BattleWind: wylaczone."); return; }
                var tStance = QuartermasterLaw.FindType("RBMAI.Stance");
                if (tStance == null) { Log.Info("BattleWind: RBM nieobecny - nic do roboty."); return; }

                _fStamina = AccessTools.Field(tStance, "stamina");
                _fMaxStamina = AccessTools.Field(tStance, "maxStamina");
                _fStaminaRegen = AccessTools.Field(tStance, "staminaRegenPerTick");
                _fPosture = AccessTools.Field(tStance, "posture");
                _fMaxPosture = AccessTools.Field(tStance, "maxPosture");
                _fPostureRegen = AccessTools.Field(tStance, "postureRegenPerTick");
                _mAddStamina = AccessTools.Method(tStance, "addStamina");
                _mAddPosture = AccessTools.Method(tStance, "addPosture");
                if (_fStamina == null || _fMaxStamina == null || _fStaminaRegen == null
                    || _fPosture == null || _fMaxPosture == null || _fPostureRegen == null
                    || _mAddStamina == null || _mAddPosture == null)
                { Log.Info("BattleWind: pola RBMAI.Stance nie pasuja - odpuszczam."); return; }

                var mIS = AccessTools.Method(tStance, "InitializeStamina");
                var mIP = AccessTools.Method(tStance, "InitializePosture");
                var mTS = AccessTools.Method(tStance, "tickStaminaRegen");
                var mTP = AccessTools.Method(tStance, "tickPostureRegen");
                if (mIS == null || mIP == null || mTS == null || mTP == null)
                { Log.Info("BattleWind: metody RBMAI.Stance nie pasuja - odpuszczam."); return; }

                h.Patch(mIS, postfix: new HarmonyMethod(typeof(BattleWind), "StaminaInitPostfix"));
                h.Patch(mIP, postfix: new HarmonyMethod(typeof(BattleWind), "PostureInitPostfix"));
                h.Patch(mTS, prefix: new HarmonyMethod(typeof(BattleWind), "TickStaminaPrefix"));
                h.Patch(mTP, prefix: new HarmonyMethod(typeof(BattleWind), "TickPosturePrefix"));
                Active = true;
                Log.Info("BattleWind: oddech bitewny bohaterow wg Endurance (pula 2^((END-2.5)/"
                         + Settings.Current.BattleEndDoubleEvery.ToString("0.#") + "), regen "
                         + (int)Settings.Current.BattleRegenAtEnd1 + "-" + (int)Settings.Current.BattleRegenAtEnd10
                         + " pkt/s, zadyszka " + Settings.Current.BattleWindedFloor.ToString("0.00") + ").");
            }
            catch (Exception e) { Log.Error("BattleWind.ApplyAll", e); }
        }
    }
}
