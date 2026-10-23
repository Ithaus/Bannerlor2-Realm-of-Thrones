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

        /// <summary>NB2: latka na RBMAI.Stance.InitializePosture wpieta (oddech bohaterow albo postura szeregowych wedlug tieru).</summary>
        internal static bool PosturePatched;

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

        /// <summary>Po RBM-owym przeliczeniu puli staminy: bohater dostaje pule x END i nasz regen.
        /// Umarly (wight, Wedrowiec, Nocny Krol) dostaje pule bez dna - trup nie zna zmeczenia.</summary>
        public static void StaminaInitPostfix(Agent agent, object stance)
        {
            try
            {
                if (agent == null || stance == null) return;
                if (Undead.Character(agent.Character))
                {
                    // TRUP NIE ZNA ZMECZENIA, ALE TEZ SIE NIE LECZY (13.09, audyt
                    // 229 agentow). RBM (RBMAI.StanceLogic.OnMissionTick) daje +0.9 HP
                    // co 10 s KAZDEMU, czyj pasek staminy stoi POWYZEJ 85% maksimum.
                    // Nasza pelna pula (1000000/1000000 = 1.00) trzymala kazdego wighta,
                    // Wedrowca i Nocnego Krola nad tym progiem przez cala bitwe, wiec
                    // umarli regenerowali ~6 HP/min bez konca - nasza wpadka, nie RBM.
                    // Pula zostaje ogromna (trupa nie zmeczysz), ale STOSUNEK siedzi
                    // pod progiem leczenia, a regen 0 pilnuje, zeby do niego nie dorosl.
                    // Progi RBM sprawdzone w dekompilacji StanceLogic: 0.85 leczenie,
                    // 0.7 szybki regen na postoju, zadnych kar bojowych od staminy.
                    // zadnego Profile: tick idzie oryginalem RBM, a pasek i tak nie drgnie
                    _fMaxStamina.SetValue(stance, 1000000f);
                    _fStamina.SetValue(stance, 800000f);      // 0.80 - pod progiem leczenia 0.85
                    _fStaminaRegen.SetValue(stance, 0f);
                    Profile drop; if (Managed.TryGetValue(stance, out drop)) Managed.Remove(stance);
                    return;
                }
                var hero = HeroOf(agent);
                if (hero == null) return;
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
        /// NB2 (16.10, Jeff "wysoki tier traci zielony pasek po kilku ciosach"): szeregowi (nie bohaterowie) dostaja pule x mnoznik tieru
        /// (SoldierMul: T1 x0.85 ... T6 x1.7, geometrycznie), regeneracje x pierwiastek z mnoznika. Bez broni glownej RBM nic nie przelicza
        /// (Stance.cs:84 - InitializePosture tylko przy primaryWieldedItemIndex != -1), wiec wtedy nie mnozymy - dotad kazda zmiana broni
        /// "na pusto" mnozyla pule bohatera od nowa.
        /// </summary>
        public static void PostureInitPostfix(Agent agent, object stance)
        {
            try
            {
                if (agent == null || stance == null) return;
                if (Undead.Character(agent.Character))
                {
                    // trupa nie zmeczysz w klinczu - postura bez dna (RBM liczy
                    // ja od zera przy kazdej zmianie broni, my za kazdym razem tez)
                    _fMaxPosture.SetValue(stance, 1000000f);
                    _fPosture.SetValue(stance, 1000000f);
                    _fPostureRegen.SetValue(stance, 1000f);
                    return;
                }
                // NB2: dziura ponownego mnozenia - RBM nie przeliczyl, wiec pula juz nosi nasz mnoznik z ostatniego przeliczenia
                if (agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.None) { RoutHold.PostureNoWeapon(agent); return; }
                var hero = HeroOf(agent);
                if (hero != null)
                {
                    // m111-walka (24.10): pojedynek (turniej, arena, pojedynek ROT) przy DuelFairGuard - Endurance najwyzej x DuelGuardEnduranceCap,
                    // razy mnoznik umiejetnosci SkillGuard; bitwy bez zmian
                    var cd = Settings.Current;
                    if (cd != null && cd.DuelFairGuard && BattleSense.InDuel(agent.Mission)) { DuelPosture(agent, hero, stance, cd); return; }
                    if (!Active) return;     // oddech bohaterow wylaczony przy starcie (BattleStaminaEnabled) - jak dotad bez mnoznika
                    float mul = EnduranceMul(hero);
                    _fMaxPosture.SetValue(stance, (float)_fMaxPosture.GetValue(stance) * mul);
                    _fPosture.SetValue(stance, (float)_fPosture.GetValue(stance) * mul);
                    _fPostureRegen.SetValue(stance, (float)_fPostureRegen.GetValue(stance) * mul);
                    RoutHold.PostureHero(agent);
                    return;
                }
                // NB2: szeregowy wedlug tieru (klucz czytany na zywo)
                var c = Settings.Current;
                if (c == null || !c.SoldierPostureByTier || !agent.IsHuman) return;
                var ch = agent.Character;
                if (ch == null || ch.IsHero) return;    // bohater bez Hero (bitwa wlasna) - jak dotad, czysty RBM
                int tier = TierOf(ch);
                float k = SoldierMul(tier);
                float max0 = (float)_fMaxPosture.GetValue(stance);
                _fMaxPosture.SetValue(stance, max0 * k);
                _fPosture.SetValue(stance, (float)_fPosture.GetValue(stance) * k);         // stosunek posture / max zostaje (RBM tez go trzyma)
                _fPostureRegen.SetValue(stance, (float)_fPostureRegen.GetValue(stance) * (float)Math.Sqrt(k));
                RoutHold.PostureSoldier(agent, tier, max0, max0 * k);
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SitePosture, "BattleWind.PostureInit", e); }
        }

        /// <summary>m111-walka: mnoznik postury z umiejetnosci pojedynku: clamp(0.75 + s / 400, 0.75, 1.5) - 24 x0.81, 100 x1.0, 180 x1.2, 250 x1.375.</summary>
        internal static float SkillGuard(int s)
        {
            return Clamp(0.75f + s / 400f, 0.75f, 1.5f);
        }

        /// <summary>m111-walka: najwyzszy mnoznik Endurance w pojedynku (klucz DuelGuardEnduranceCap, przyciety jak suwak 1..4).</summary>
        internal static float DuelCap(Settings c)
        {
            return Clamp(c != null ? c.DuelGuardEnduranceCap : 1.5f, 1f, 4f);
        }

        private static WeakReference _duelLogM;
        private static float _duelLogMul = -1f;

        /// <summary>
        /// m111-walka faza 1 (24.10, Jeff: "z drzewcem, ktorego sie dopiero ucze, bije duzo wyzsze poziomy"): POSTURA BOHATERA W POJEDYNKU (klucz DuelFairGuard).
        /// Dowod [K/L]: pula postury bohatera rosla tylko z Endurance (2^((END-2.5)/2.5) - gracz x3.48, ok. 550 pkt), a RBM liczy ja prawie bez umiejetnosci
        /// broni - nowicjusz z drzewcem mial garde 2-3 razy wieksza niz rycerz 180. Zasada: mnoznik = min(Endurance, DuelGuardEnduranceCap) x SkillGuard(s),
        /// s - umiejetnosc pojedynku (DuelAi.DuelSkill: bron w reku, bohater co najmniej udzial najlepszej); pula i regeneracja postury x mnoznik,
        /// stamina bez zmian. Bez oddechu bohaterow (BattleStaminaEnabled wylaczony) Endurance liczy sie x1. Linia gracza raz na misje i przy zmianie.
        /// </summary>
        private static void DuelPosture(Agent agent, Hero hero, object stance, Settings c)
        {
            float end = Active ? EnduranceMul(hero) : 1f;
            float cap = DuelCap(c);
            int held, best;
            int s = DuelAi.DuelSkill(agent, c, out held, out best);
            float mul = Math.Min(end, cap) * SkillGuard(s);
            _fMaxPosture.SetValue(stance, (float)_fMaxPosture.GetValue(stance) * mul);
            _fPosture.SetValue(stance, (float)_fPosture.GetValue(stance) * mul);
            _fPostureRegen.SetValue(stance, (float)_fPostureRegen.GetValue(stance) * mul);
            RoutHold.PostureHero(agent);
            DuelGuard.NotePosture(agent, mul, end > cap + 1e-4f);
            if (agent.IsPlayerControlled)
            {
                var w = _duelLogM;
                bool same = w != null && ReferenceEquals(w.Target, agent.Mission) && Math.Abs(_duelLogMul - mul) < 0.005f;
                if (!same)
                {
                    _duelLogM = new WeakReference(agent.Mission);
                    _duelLogMul = mul;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    Log.Info("BattleWind: gracz w pojedynku postura x" + mul.ToString("0.00", inv) + " (END x" + end.ToString("0.00", inv) + ", cap "
                             + cap.ToString("0.0#", inv) + ", skill " + s + (best > 0 ? ", najlepsza " + best : "") + ", pula "
                             + ((float)_fMaxPosture.GetValue(stance)).ToString("0", inv) + ").");
                }
            }
        }

        /// <summary>NB2: tier jednostki z poziomu jak w grze (DefaultCharacterStatsModel.GetTier: ceil((Level - 5) / 5)), 1..7. Bez
        /// CharacterObject.Tier - ten wola Campaign.Current (null w bitwie wlasnej).</summary>
        internal static int TierOf(BasicCharacterObject ch)
        {
            int lv = 1;
            try { lv = ch != null ? ch.Level : 1; } catch { }
            int t = (int)Math.Ceiling((lv - 5) / 5.0);
            if (t < 1) t = 1;
            if (t > 7) t = 7;
            return t;
        }

        /// <summary>NB2: mnoznik puli postury szeregowego: T1 x m1 ... T6 x m6, miedzy nimi (i T7) geometrycznie: m1 * (m6/m1)^((T-1)/5).
        /// Klucze przyciete jak suwaki MCM (T1 0.5-1.5, T6 1.0-3.0).</summary>
        internal static float SoldierMul(int tier)
        {
            var c = Settings.Current;
            float m1 = Clamp(c != null ? c.SoldierPostureTier1Multiplier : 0.85f, 0.5f, 1.5f);
            float m6 = Clamp(c != null ? c.SoldierPostureTier6Multiplier : 1.7f, 1f, 3f);
            if (tier < 1) tier = 1;
            if (tier > 7) tier = 7;
            return (float)(m1 * Math.Pow(m6 / m1, (tier - 1) / 5.0));
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
                // NB2: latka InitializePosture potrzebna takze samej posturze szeregowych wedlug tieru (SoldierPostureByTier); stamina i oba
                // ticki dalej tylko przy BattleStaminaEnabled, Active tylko przy staminie bohaterow (czyta to FieldCraft - Endurance nie dwa razy)
                bool wind = c != null && c.BattleStaminaEnabled;
                bool tiers = c != null && c.SoldierPostureByTier;
                if (!wind && !tiers) { Log.Info("BattleWind: wylaczone."); return; }
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

                // NB2: cele deklarowane w RBMAI.Stance (AccessTools.DeclaredMethod; Stance dziedziczy tylko po object - te same metody co dotad)
                var mIS = AccessTools.DeclaredMethod(tStance, "InitializeStamina");
                var mIP = AccessTools.DeclaredMethod(tStance, "InitializePosture");
                var mTS = AccessTools.DeclaredMethod(tStance, "tickStaminaRegen");
                var mTP = AccessTools.DeclaredMethod(tStance, "tickPostureRegen");
                if (mIP == null || (wind && (mIS == null || mTS == null || mTP == null)))
                { Log.Info("BattleWind: metody RBMAI.Stance nie pasuja - odpuszczam."); return; }

                h.Patch(mIP, postfix: new HarmonyMethod(typeof(BattleWind), "PostureInitPostfix"));
                PosturePatched = true;
                if (wind)
                {
                    h.Patch(mIS, postfix: new HarmonyMethod(typeof(BattleWind), "StaminaInitPostfix"));
                    h.Patch(mTS, prefix: new HarmonyMethod(typeof(BattleWind), "TickStaminaPrefix"));
                    h.Patch(mTP, prefix: new HarmonyMethod(typeof(BattleWind), "TickPosturePrefix"));
                    Active = true;
                    Log.Info("BattleWind: oddech bitewny bohaterow wg Endurance (pula 2^((END-2.5)/"
                             + Settings.Current.BattleEndDoubleEvery.ToString("0.#") + "), regen "
                             + (int)Settings.Current.BattleRegenAtEnd1 + "-" + (int)Settings.Current.BattleRegenAtEnd10
                             + " pkt/s, zadyszka " + Settings.Current.BattleWindedFloor.ToString("0.00") + ").");
                }
                else Log.Info("BattleWind: oddech bohaterow wylaczony (BattleStaminaEnabled) - wpieta tylko postura szeregowych wedlug tieru.");
                Log.Info("BattleWind (NB2): postura szeregowych wedlug tieru - latka InitializePosture wpieta (klucz SoldierPostureByTier na zywo"
                         + ", przy starcie " + (tiers ? "TAK" : "NIE") + "); bez broni glownej bez ponownego mnozenia.");
            }
            catch (Exception e) { Log.Error("BattleWind.ApplyAll", e); }
        }
    }
}
