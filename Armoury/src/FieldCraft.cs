using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// Cialo ma swoje prawa - realia pola bitwy, o ktore prosil Jeff:
    /// 1. ZADYSZKA (model punktowy, wg slow Jeffa): czlowiek ma pule staminy
    ///    w PUNKTACH (RBM: baza 1000, Atletyka JUZ ja podnosi; my dokladamy
    ///    Endurance bohaterow). Sprint POBIERA stale punkty na sekunde,
    ///    tym wiecej, im ciezszy pancerz. Ile sekund pobiegniesz, wynika SAMO
    ///    z puli i drenazu - zadnych "sekund do zmeczenia" w ustawieniach.
    ///    W OSTATNICH 10% puli czlowiek zwalnia (bieg i ciosy), przy ZERZE
    ///    lapie zadyszke: -50% ruchu, walki i obrazen, i odsapuje, az oddech
    ///    wroci do 10%. Dotyczy kazdego na polu: gracza, jego ludzi i wroga.
    /// 2. RANNI: ponizej progu zdrowia czlowiek zwalnia i wolniej wymachuje
    ///    (kara rosnie z rana), a od 10 HP krwawi i moze sie wykrwawic na smierc.
    ///    AI bliskie smierci lamie sie i ucieka z pola.
    /// 3. JEZ ZE STRZAL: strzala, ktora ledwo drasnela (pancerz ja zatrzymal),
    ///    nie zostaje wbita w czlowieka - odpada. W tarczy zostaje jak nalezy.
    /// </summary>
    internal sealed class FieldCraft : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private sealed class State
        {
            public float Pace = 1f;        // mnoznik tempa z oddechu (1 = swiezy, 0.5 = odsapuje)
            public bool Winded;            // ZADYSZKA: pula spadla do zera - odsapuje, az wroci 10%
            public float BaseSwing = -1f;  // zlapana baza do kar bojowych
            public float BaseThrust = -1f;
            public bool SwingApplied;      // czy nasza kara siedzi teraz we wlasciwosciach agenta
            public bool Panicked;
            // policzone raz na agenta
            public float DrainPerSec = -1f; // punkty/s sprintu (baza + obciazenie)
            public float EndBonus = 1f;     // mnoznik puli z Endurance (bohaterowie)
            public float TiredFloor;
            public float PoolMax;           // wlasna pula, gdy RBM nieobecny
            public float Pool = -1f;
            public float RegenPerSec;       // regeneracja wlasnej puli
            public bool EnduranceApplied;   // bonus Endurance dolozony do puli RBM
            public object RbmStance;        // RBMAI.Stance tego agenta (jesli RBM obecny)
            public bool RbmChecked;
            public float RbmRecheck;        // RBM zaklada stance PO spawnie - ponawiamy szukanie
            public float SprintDebt;        // ile MY zabralismy z puli RBM sprintem - tyle oddajemy szybko
        }

        // ---- most do staminy RBM: sprint pije z TEGO SAMEGO paska, ktory widac w walce ----
        private static bool _rbmInit;
        private static System.Reflection.FieldInfo _rbmValues;    // AgentStances.values
        private static System.Reflection.FieldInfo _rbmStamina;   // Stance.stamina
        private static System.Reflection.FieldInfo _rbmMaxStamina;
        private static System.Reflection.FieldInfo _rbmRegen;     // Stance.staminaRegenPerTick

        private static void RbmInit()
        {
            if (_rbmInit) return;
            _rbmInit = true;
            try
            {
                Type stances = null, stance = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (stances == null) stances = asm.GetType("RBMAI.AgentStances");
                        if (stance == null) stance = asm.GetType("RBMAI.Stance");
                    }
                    catch { }
                }
                if (stances == null || stance == null) return;
                _rbmValues = stances.GetField("values", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                _rbmStamina = stance.GetField("stamina", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                _rbmMaxStamina = stance.GetField("maxStamina", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                _rbmRegen = stance.GetField("staminaRegenPerTick", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (_rbmValues != null && _rbmStamina != null)
                    Log.Info("FieldCraft: zadyszka wpieta w stamine RBM.");
            }
            catch (Exception e) { Log.Error("RbmInit", e); }
        }

        private static object RbmStanceOf(Agent agent, State st, float step)
        {
            if (st.RbmStance != null) return st.RbmStance;
            if (st.RbmChecked)
            {
                // RBM potrafi zalozyc agentowi stance JUZ PO naszym pierwszym
                // sprawdzeniu - kto sprawdzil raz i odpuscil, czytal potem wlasna,
                // NIEWIDOCZNA pule, a pasek RBM na ekranie stal na zerze bez kar.
                // Dlatego ponawiamy szukanie co ~2 s, az znajdziemy.
                st.RbmRecheck -= step;
                if (st.RbmRecheck > 0f) return null;
            }
            st.RbmChecked = true;
            st.RbmRecheck = 2f;
            try
            {
                RbmInit();
                if (_rbmValues == null) { st.RbmRecheck = float.MaxValue; return null; }
                var dict = _rbmValues.GetValue(null) as System.Collections.IDictionary;
                if (dict != null && dict.Contains(agent)) st.RbmStance = dict[agent];
            }
            catch { }
            return st.RbmStance;
        }

        /// <summary>
        /// Tempo z oddechu wg Jeffa: pelna para do 10% puli, w ostatnich 10%
        /// czlowiek zwalnia (bieg I ciosy), a przy ZERZE lapie ZADYSZKE:
        /// -50% ruchu, -50% tempa walki (a z nim obrazen) i odsapuje tak
        /// dlugo, az oddech wroci do 10% puli. Dotyczy KAZDEGO czlowieka
        /// na polu - gracza, jego ludzi i wroga.
        /// </summary>
        private static float PaceFromRatio(State st, float ratio)
        {
            if (ratio <= 0.005f) st.Winded = true;
            else if (st.Winded && ratio >= 0.10f) st.Winded = false;
            if (st.Winded) return 0.5f;
            if (ratio < 0.10f) return 0.5f + 0.5f * (ratio / 0.10f);
            return 1f;
        }

        // Model punktowy: drenaz sprintu to STALE punkty na sekunde (baza + obciazenie).
        // Atletyka nie zmniejsza drenazu - ona POWIEKSZA pule (w RBM robi to sam RBM:
        // maxStamina = 1000 * (1 + 3*Atletyka/500)). Endurance bohaterow dokladamy sami
        // jako mnoznik puli. Czas biegu wynika z puli / drenaz - sam z siebie.
        private static void MeasureWind(Agent agent, State st)
        {
            var c = Settings.Current;
            float ath = 0f;
            Hero hero = null;
            try
            {
                var co = agent.Character as CharacterObject;
                if (co != null)
                {
                    ath = co.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics);
                    if (co.IsHero) hero = co.HeroObject;
                }
            }
            catch { }
            // wykladniczy mnoznik Endurance - ta sama krzywa co BattleWind
            // (2^((END-2.5)/co_ile)); NPC bez bohatera zostaje na x1
            st.EndBonus = 1f;
            try { if (hero != null) st.EndBonus = BattleWind.EnduranceMul(hero); }
            catch { }
            float w = 0f;
            try { w = agent.SpawnEquipment.GetTotalWeightOfArmor(true); } catch { }

            st.DrainPerSec = Math.Max(1f, c.SprintDrainPerSecond)
                           + Math.Max(0f, w - c.FatigueFreeArmorKg) * Math.Max(0f, c.SprintDrainPerKg);
            st.TiredFloor = Math.Min(0.92f, c.TiredSpeedFactor + ath / 1000f);

            // wlasna pula (gdy RBM nieobecny) - te same zasady co RBM: 1000 * bonus z Atletyki
            float athCap = Math.Min(ath, 500f);
            st.PoolMax = 1000f * (1f + 3f * athCap / 500f) * st.EndBonus;
            st.Pool = st.PoolMax;
            // regeneracja rosnie z Atletyka (jak w RBM) ORAZ z Endurance bohatera
            st.RegenPerSec = Math.Max(1f, c.StaminaRegenPerSecond) * (1f + 2f * athCap / 500f) * st.EndBonus;
        }

        private static bool _sprintLogged;
        private readonly Dictionary<Agent, State> _states = new Dictionary<Agent, State>();
        private readonly List<KeyValuePair<Agent, int>> _unstick = new List<KeyValuePair<Agent, int>>();
        // czyj to wierzchowiec: kon -> bohater z klanu gracza (spisujemy w locie)
        private readonly Dictionary<Agent, Hero> _mountOwners = new Dictionary<Agent, Hero>();
        private float _accum;

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try { _states.Remove(affectedAgent); _mountOwners.Remove(affectedAgent); } catch { }
        }

        /// <summary>
        /// Martwy kon zostaje martwy. Vanilla po bitwie "wskrzesza" wierzchowca
        /// gracza - u nas kon zabity w PRAWDZIWEJ walce (nie na arenie i nie
        /// w turnieju) znika z ekwipunku na zawsze. Uprzaz zdejmujesz z trupa.
        /// Dotyczy bohaterow klanu gracza (Ty i towarzysze).
        /// </summary>
        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                var c = Settings.Current;
                if (!c.HorseDeathPermanent) return;
                if (affectedAgent == null || !affectedAgent.IsMount) return;
                if (agentState != AgentState.Killed) return;
                if (Mission.CombatType != Mission.MissionCombatType.Combat) return;   // arena/turniej: konie pozyczone

                Hero owner = null;
                var rider = affectedAgent.RiderAgent;
                if (rider != null)
                {
                    var co = rider.Character as CharacterObject;
                    if (co != null && co.IsHero) owner = co.HeroObject;
                }
                if (owner == null) _mountOwners.TryGetValue(affectedAgent, out owner);
                if (owner == null || owner.Clan != Clan.PlayerClan) return;

                var eq = owner.BattleEquipment;
                if (eq == null || eq[EquipmentIndex.Horse].IsEmpty) return;
                var horse = eq[EquipmentIndex.Horse].Item;
                eq[EquipmentIndex.Horse] = default(EquipmentElement);

                string who = owner == Hero.MainHero ? "Your" : owner.Name + "'s";
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                    who + " " + (horse != null ? horse.Name.ToString() : "horse") + " was killed. Dead horses stay dead - the harness came off the corpse.",
                    TaleWorlds.Library.Colors.Red));
                Log.Info("FieldCraft: kon bohatera " + owner.Name + " zabity w walce - usuniety z ekwipunku.");
            }
            catch (Exception e) { Log.Error("FieldCraft.HorseDeath", e); }
        }

        // ---- ksiega zuzycia: KTORY kawalek sprzetu gracza ucierpial i od czego.
        // Zbroja zuzywa sie TAM, GDZIE pada cios; bron - od kazdego celnego
        // uderzenia; tarcza - od blokow; uprzaz - gdy obrywa kon.
        // ArmouryBehavior konsumuje to po bitwie. ----
        internal static readonly float[] WearLedger = new float[12];

        internal static void TakeLedger(float[] into)
        {
            for (int i = 0; i < 12; i++) { into[i] = WearLedger[i]; WearLedger[i] = 0f; }
        }

        private static int ArmorSlotFor(BoneBodyPartType part, Agent a)
        {
            switch (part)
            {
                case BoneBodyPartType.Head:
                case BoneBodyPartType.Neck: return 5;                        // helm
                case BoneBodyPartType.ShoulderLeft:
                case BoneBodyPartType.ShoulderRight:
                    try { if (a.SpawnEquipment[EquipmentIndex.Cape].Item != null) return 9; } catch { }
                    return 6;                                                // peleryna albo kirys
                case BoneBodyPartType.ArmLeft:
                case BoneBodyPartType.ArmRight: return 8;                    // rekawice
                case BoneBodyPartType.Legs: return 7;                        // nogawice
                default: return 6;                                           // kirys
            }
        }

        private static int WeaponSlotFor(Agent a, ItemObject item)
        {
            if (item == null) return -1;
            try
            {
                for (int i = 0; i < 4; i++)
                    if (a.SpawnEquipment[(EquipmentIndex)i].Item == item) return i;
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Luk zuzywa sie OD STRZELANIA - kazdy strzal kosztuje, trafienie czy pudlo.
        /// Wytrzymalosc wg Jeffa: tier 1 = 2500 strzalow, kazdy tier mnozy (t6 = x6),
        /// a kazdy punkt umiejetnosci Bow/Crossbow dodaje +1% strzalow.
        /// </summary>
        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            try
            {
                var c = Settings.Current;
                if (!c.WearEnabled || shooterAgent == null || !shooterAgent.IsMainAgent) return;
                var item = shooterAgent.SpawnEquipment[weaponIndex].Item;
                if (item == null) return;
                if (item.ItemType != ItemObject.ItemTypeEnum.Bow && item.ItemType != ItemObject.ItemTypeEnum.Crossbow) return;

                int tier = Recipes.Grade(item);
                int skill = 0;
                try
                {
                    skill = Hero.MainHero.GetSkillValue(item.ItemType == ItemObject.ItemTypeEnum.Bow
                        ? TaleWorlds.Core.DefaultSkills.Bow : TaleWorlds.Core.DefaultSkills.Crossbow);
                }
                catch { }
                float uses = Math.Max(100, c.BowUsesAtTier1) * tier
                           * (1f + skill * Math.Max(0f, c.BowSkillBonusPercentPerPoint) / 100f);
                // ApplyWear dzieli potem przez solidnosc tieru - kompensujemy, zeby liczby Jeffa trzymaly co do sztuki
                float sturdiness = 1f + Math.Max(0, tier - 1) * Settings.Current.TierDurabilityFactor;
                int slot = (int)weaponIndex;
                if (slot >= 0 && slot < 4) WearLedger[slot] += 100f / uses * sturdiness;
            }
            catch { }
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                var c = Settings.Current;

                // --- zuzycie sprzetu gracza: tylko od faktycznych zdarzen ---
                if (c.WearEnabled)
                {
                    int raw = blow.InflictedDamage + Math.Max(0, attackCollisionData.AbsorbedByArmor);
                    // strzala robi w pancerzu dziurke, nie wyrwe - liczymy tylko ulamek
                    // jej obrazen jako zuzycie (obrazenia HP zostaja normalne)
                    float missileScale = blow.IsMissile
                        ? Math.Max(0f, Math.Min(100f, c.MissileArmorWearPercent)) / 100f
                        : 1f;
                    if (affectedAgent != null && affectedAgent.IsMainAgent && raw > 0)
                    {
                        if (attackCollisionData.AttackBlockedWithShield)
                        {
                            int sh = -1;
                            try
                            {
                                for (int i = 0; i < 4; i++)
                                {
                                    var it = affectedAgent.SpawnEquipment[(EquipmentIndex)i].Item;
                                    if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Shield) { sh = i; break; }
                                }
                            }
                            catch { }
                            if (sh >= 0) WearLedger[sh] += raw * c.WearDamageFactor * Math.Max(0f, c.WearShieldFactor);
                        }
                        else
                        {
                            int slot = ArmorSlotFor(blow.VictimBodyPart, affectedAgent);
                            // model Jeffa: do ksiegi idzie SUROWY cios - na stan przeliczy go
                            // pula wytrzymalosci sztuki (pancerz x 20 x tier), jeden za jeden
                            WearLedger[slot] += raw * missileScale;
                        }
                    }
                    // Twoj celny cios zuzywa TWOJA bron BIALA; luki i kusze placa
                    // ZA STRZAL (OnAgentShootMissile), nie za trafienie
                    if (affectorAgent != null && affectorAgent.IsMainAgent && raw > 0
                        && affectorWeapon.Item != null
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Bow
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Crossbow
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Thrown)
                    {
                        int ws = WeaponSlotFor(affectorAgent, affectorWeapon.Item);
                        if (ws >= 0) WearLedger[ws] += Math.Max(0f, c.WearWeaponPerHit);
                    }
                    // kon gracza obrywa - cierpi uprzaz (ulamek, nie polowa:
                    // kon to wielki cel i przy 0.5 siodla sypaly sie seriami)
                    if (affectedAgent != null && affectedAgent.IsMount && affectedAgent.RiderAgent != null
                        && affectedAgent.RiderAgent.IsMainAgent && raw > 0)
                        WearLedger[11] += raw * Math.Max(0f, c.HarnessWearFactor) * missileScale;
                }

                if (!c.ArrowUnstickEnabled || affectedAgent == null || !affectedAgent.IsHuman) return;
                if (!blow.IsMissile) return;
                if (attackCollisionData.AttackBlockedWithShield) return;      // w tarczy strzaly zostaja
                // OSZCZEP TO NIE STRZALA (Jeff 02.09: "jak dostaje wlocznia
                // rzucana, nie rob tak, ze tkwi w moim ciele - wlocznie tkwia,
                // jak zabijesz kogos albo obrazenia sa powyzej 80"): bron
                // miotana (oszczep, topor, noz) ma wlasny, wysoki prog;
                // zabity zostaje z pociskiem sam z siebie (martwy agent nie
                // wchodzi do odpinania w ticku).
                bool thrown = false;
                try
                {
                    var wc = (!affectorWeapon.IsEmpty && affectorWeapon.CurrentUsageItem != null) ? affectorWeapon.CurrentUsageItem.WeaponClass : WeaponClass.Undefined;
                    thrown = wc == WeaponClass.Javelin || wc == WeaponClass.ThrowingAxe || wc == WeaponClass.ThrowingKnife;
                }
                catch { }
                int minStick = thrown ? c.JavelinStickMinDamage : c.ArrowStickMinDamage;
                if (blow.InflictedDamage >= minStick) return;                  // weszla naprawde - tkwi
                _unstick.Add(new KeyValuePair<Agent, int>(affectedAgent, 0));
            }
            catch { }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                var c = Settings.Current;
                _accum += dt;

                // odpiecie strzal-drasniec (tick pozniej, zeby silnik zdazyl je przypiac)
                if (_unstick.Count > 0)
                {
                    for (int i = _unstick.Count - 1; i >= 0; i--)
                    {
                        var a = _unstick[i].Key;
                        _unstick.RemoveAt(i);
                        if (a == null || !a.IsActive()) continue;
                        int n = a.GetAttachedWeaponsCount();
                        if (n > 0) a.DeleteAttachedWeapon(n - 1);
                    }
                }

                if (_accum < 0.2f) return;                    // reszta co 0.2 s wystarczy
                float step = _accum; _accum = 0f;

                foreach (var agent in Mission.Agents)
                {
                    if (agent == null || !agent.IsActive() || !agent.IsHuman) continue;
                    // wight nie zna zadyszki, ran, krwawienia ani strachu - trup
                    // maszeruje i bije, poki go nie potna na kawalki
                    if (Undead.Character(agent.Character)) continue;
                    State st;
                    if (!_states.TryGetValue(agent, out st)) { st = new State(); _states[agent] = st; }

                    // spisz wlasciciela wierzchowca (na wypadek, gdy kon ginie bez jezdzca na grzbiecie)
                    if (agent.MountAgent != null && !_mountOwners.ContainsKey(agent.MountAgent))
                    {
                        try
                        {
                            var mco = agent.Character as CharacterObject;
                            if (mco != null && mco.IsHero && mco.HeroObject != null && mco.HeroObject.Clan == Clan.PlayerClan)
                                _mountOwners[agent.MountAgent] = mco.HeroObject;
                        }
                        catch { }
                    }

                    float hpr = agent.HealthLimit > 0f ? agent.Health / agent.HealthLimit : 1f;

                    // --- zadyszka (tylko piechota, sprint) - MODEL PUNKTOWY:
                    // pula staminy w punktach (RBM: 1000 * bonus z Atletyki; my raz dokladamy
                    // Endurance bohatera), sprint pobiera stale punkty/s wg obciazenia.
                    // Ile sekund pobiegnie - wynika samo z puli i drenazu.
                    if (c.SprintFatigueEnabled)
                    {
                        if (st.DrainPerSec < 0f) MeasureWind(agent, st);
                        // sprint drenuje TYLKO piechura (konia nie meczy jezdziec),
                        // ale ODDECH czytamy kazdemu: jezdziec tez wymachuje i tez
                        // moze zejsc do zera pula RBM od ciosow
                        bool onFoot = agent.MountAgent == null;
                        bool sprinting = false;
                        if (onFoot)
                        {
                            float speed = agent.GetCurrentVelocity().Length;
                            // prog biegu WZGLEDEM mozliwosci agenta - RBM mocno tnie
                            // predkosci (stary sztywny prog 4.2 m/s nigdy nie padal,
                            // stad "stamina nie spada"). Marsz ~40% maksimum, wiec
                            // wszystko powyzej ~55% maksimum traktujemy jako bieg.
                            float runThr = 3.0f;
                            try
                            {
                                float run = agent.GetMaximumForwardUnlimitedSpeed();
                                if (run > 0.8f)
                                {
                                    runThr = run * 0.55f;
                                    if (runThr < 2.2f) runThr = 2.2f;   // marsz (~1.6 m/s) to NIE bieg
                                    if (runThr > 4.2f) runThr = 4.2f;
                                }
                            }
                            catch { }
                            sprinting = speed > runThr;
                            if (sprinting && !_sprintLogged)
                            {
                                _sprintLogged = true;
                                Log.Info("FieldCraft: bieg wykryty (" + speed.ToString("0.0") + " m/s, prog "
                                         + runThr.ToString("0.0") + "), drenaz " + st.DrainPerSec.ToString("0.0") + " pkt/s.");
                            }
                        }
                        var stance = c.UseRbmStamina ? RbmStanceOf(agent, st, step) : null;
                        if (stance != null)
                        {
                            try
                            {
                                float stam = (float)_rbmStamina.GetValue(stance);
                                float maxStam = _rbmMaxStamina != null ? (float)_rbmMaxStamina.GetValue(stance) : 1000f;
                                if (maxStam < 1f) maxStam = 1000f;

                                // Endurance bohatera powieksza pule RBM - raz, przy pierwszym
                                // zetknieciu. Gdy BattleWind zalatwil to juz w InitializeStamina
                                // (Active), NIE dokladamy drugi raz.
                                if (!st.EnduranceApplied)
                                {
                                    st.EnduranceApplied = true;
                                    if (!BattleWind.Active && st.EndBonus > 1.001f && _rbmMaxStamina != null)
                                    {
                                        maxStam *= st.EndBonus;
                                        stam *= st.EndBonus;
                                        _rbmMaxStamina.SetValue(stance, maxStam);
                                        _rbmStamina.SetValue(stance, stam);
                                        // Endurance przyspiesza tez ODDECH: regeneracja RBM
                                        // (ktora Atletyka juz skaluje) rosnie o ten sam mnoznik
                                        if (_rbmRegen != null)
                                        {
                                            try
                                            {
                                                float rg = (float)_rbmRegen.GetValue(stance);
                                                _rbmRegen.SetValue(stance, rg * st.EndBonus);
                                            }
                                            catch { }
                                        }
                                    }
                                }

                                if (sprinting)
                                {
                                    float d = st.DrainPerSec * step;   // punkty, nie sekundy
                                    stam -= d;
                                    st.SprintDebt += d;
                                    if (stam < 0f) stam = 0f;
                                    _rbmStamina.SetValue(stance, stam);
                                }
                                else if (st.SprintDebt > 0.01f && stam < maxStam && !BattleWind.Manages(stance))
                                {
                                    // ODDECH SPRINTERA: to, co zabral bieg, wraca tempem
                                    // biegacza (25/s x Atletyka x Endurance) - ale TYLKO
                                    // dlug sprintu. Koszty ciosow RBM oddaja sie dalej po
                                    // ich staremu (~1/s), zeby nie zepsuc tempa melee.
                                    // Bohaterow z BattleWind pomijamy - ich regen RBM juz
                                    // jest tempem biegacza i oddalby dlug PODWOJNIE.
                                    float back = Math.Min(st.SprintDebt, st.RegenPerSec * step);
                                    if (back > maxStam - stam) back = maxStam - stam;
                                    stam += back;
                                    st.SprintDebt -= back;
                                    _rbmStamina.SetValue(stance, stam);
                                }
                                st.Pace = PaceFromRatio(st, stam / maxStam);
                            }
                            catch { st.Pace = 1f; st.Winded = false; }
                        }
                        else
                        {
                            // bez RBM: wlasna pula na tych samych zasadach
                            if (st.Pool < 0f) st.Pool = st.PoolMax;
                            if (sprinting) st.Pool -= st.DrainPerSec * step;
                            else st.Pool += st.RegenPerSec * step;
                            if (st.Pool < 0f) st.Pool = 0f;
                            if (st.Pool > st.PoolMax) st.Pool = st.PoolMax;
                            float ratio = st.PoolMax > 1f ? st.Pool / st.PoolMax : 1f;
                            st.Pace = PaceFromRatio(st, ratio);
                        }
                    }
                    else { st.Pace = 1f; st.Winded = false; }

                    // --- kara za rany (ruch liczony razem z zadyszka) ---
                    float woundSlow = 0f;
                    float thr = c.WoundedBelowPercent / 100f;
                    if (c.WoundedPenaltiesEnabled && hpr < thr && thr > 0.01f)
                        woundSlow = c.WoundedMaxSlow * (thr - hpr) / thr;

                    // ruch: zadyszka (ostatnie 10% puli -> do -50%) i rany licza sie razem
                    float limit = st.Pace;
                    limit = Math.Min(limit, 1f - woundSlow);
                    if (limit < 0.4f) limit = 0.4f;

                    // chod na zyczenie: gra na PC nie daje klawisza chodu (w silniku jest,
                    // ale przypisany tylko padowi) - u nas trzymanie lewego Ctrl = idziesz,
                    // stamina odpoczywa, wygladasz jak czlowiek, nie jak sprinter
                    if (c.WalkKeyEnabled && agent.IsMainAgent)
                    {
                        try
                        {
                            if (TaleWorlds.InputSystem.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftControl))
                                limit = Math.Min(limit, Math.Max(0.2f, c.WalkSpeedShare));
                        }
                        catch { }
                    }
                    agent.SetMaximumSpeedLimit(limit, true);

                    // tempo CIOSOW: zadyszka i rany skladaja sie w jeden mnoznik
                    // zamachu i pchniecia. Wolniejszy zamach = mniejsza energia
                    // ciosu, wiec obrazenia spadaja razem z tempem - dokladnie
                    // tak, jak chcial Jeff (-50% walki i obrazen przy zerze).
                    // Baze lapiemy TYLKO, gdy zadna kara nie dziala, zeby nie
                    // zjadac zmian RBM (postura) ani nie utrwalic wlasnej kary.
                    float swingF = st.Pace;
                    if (c.WoundedPenaltiesEnabled && hpr < thr) swingF *= 1f - woundSlow * 0.7f;
                    var adp = agent.AgentDrivenProperties;
                    if (swingF >= 0.999f)
                    {
                        if (st.SwingApplied)
                        {
                            if (st.BaseSwing > 0f) adp.SwingSpeedMultiplier = st.BaseSwing;
                            if (st.BaseThrust > 0f) adp.ThrustOrRangedReadySpeedMultiplier = st.BaseThrust;
                            agent.UpdateCustomDrivenProperties();
                            st.SwingApplied = false;
                        }
                        else
                        {
                            st.BaseSwing = adp.SwingSpeedMultiplier;
                            st.BaseThrust = adp.ThrustOrRangedReadySpeedMultiplier;
                        }
                    }
                    else
                    {
                        if (!st.SwingApplied && st.BaseSwing <= 0f)
                        {
                            st.BaseSwing = adp.SwingSpeedMultiplier;
                            st.BaseThrust = adp.ThrustOrRangedReadySpeedMultiplier;
                        }
                        if (st.BaseSwing > 0f) adp.SwingSpeedMultiplier = st.BaseSwing * swingF;
                        if (st.BaseThrust > 0f) adp.ThrustOrRangedReadySpeedMultiplier = st.BaseThrust * swingF;
                        agent.UpdateCustomDrivenProperties();
                        st.SwingApplied = true;
                    }

                    // --- wykrwawianie (od BleedBelowHp punktow zdrowia) i zalamanie ---
                    bool bleeding = agent.Health <= c.BleedBelowHp;
                    bool broken = hpr < c.AiFleeBelowPercent / 100f;
                    if (bleeding || broken)
                    {
                        if (bleeding && c.BleedPerSecond > 0f)
                        {
                            float nh = agent.Health - c.BleedPerSecond * step;
                            if (nh <= 0f)
                            {
                                var b = new Blow(agent.Index);
                                b.DamageType = DamageTypes.Cut;
                                b.InflictedDamage = (int)agent.Health + 1;
                                b.GlobalPosition = agent.Position;
                                agent.Die(b, Agent.KillInfo.Invalid);
                            }
                            else agent.Health = nh;
                        }
                        if (broken && c.AiFleeWhenNearDeath && !agent.IsMainAgent && agent.IsAIControlled && !st.Panicked)
                        {
                            st.Panicked = true;
                            FleeTheField(agent);
                        }
                        else if (st.Panicked && !agent.IsMainAgent && agent.IsAIControlled && !agent.IsRetreating())
                        {
                            // odwrot mu wygasl (zderzyl sie z kims, silnik go zawrocil) -
                            // przypominamy trupowi ze mial uciekac, nie boksowac
                            FleeTheField(agent);
                        }
                    }
                }
            }
            catch (Exception e) { Log.Error("FieldCraft.Tick", e); }
        }

        /// <summary>
        /// Zlamany UCIEKA Z POLA, jak chcial Jeff: od najblizszego wroga,
        /// w strone najblizszego skraju mapy po tamtej polkuli. Panika natywna
        /// wylacza mu walke, ale cel odwrotu liczymy SAMI - stary GetRetreatPos
        /// na scenach bez punktow odwrotu zwracal miejsce, w ktorym stal,
        /// wiec zlamani rzucali bron i stali jak kolki, boksujac z bliska.
        /// </summary>
        private static void FleeTheField(Agent agent)
        {
            try
            {
                var mission = TaleWorlds.MountAndBlade.Mission.Current;
                if (mission == null || agent == null || !agent.IsActive()) return;
                try { var cai = agent.CommonAIComponent; if (cai != null) cai.Panic(); } catch { }

                Vec2 my = agent.Position.AsVec2;
                Agent foe = null; float best = float.MaxValue;
                foreach (var a in mission.Agents)
                {
                    if (a == null || !a.IsActive() || !a.IsHuman) continue;
                    if (a.Team == null || agent.Team == null || !a.Team.IsEnemyOf(agent.Team)) continue;
                    float d = a.Position.AsVec2.DistanceSquared(my);
                    if (d < best) { best = d; foe = a; }
                }

                Vec2 dir;
                if (foe != null) dir = my - foe.Position.AsVec2;                  // plecami do wroga
                else dir = mission.GetClosestBoundaryPosition(my) - my;           // bez wroga: wprost do skraju
                if (dir.LengthSquared < 0.01f) dir = new Vec2(1f, 0f);
                dir.Normalize();

                Vec2 far = my + dir * 300f;
                Vec2 edge;
                try { edge = mission.GetClosestBoundaryPosition(far); }           // przyciecie do granicy mapy
                catch { edge = far; }
                var wp = new WorldPosition(mission.Scene, new Vec3(edge.x, edge.y, agent.Position.z));
                agent.Retreat(wp);
            }
            catch (Exception e) { Log.Error("FleeTheField", e); }
        }
    }
}
