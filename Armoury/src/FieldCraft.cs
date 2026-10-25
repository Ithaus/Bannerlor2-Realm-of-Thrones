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
            public float RunSeconds = -1f;  // 127: sekundy pelnego biegu do zera (fizjologia: Atletyka i waga zbroi) - drenaz = cala pula / RunSeconds
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
            public float BleedDebt;         // uzbierany ulamek krwawienia - silnik przyjmuje tylko pelne punkty
            public bool SpeedApplied;       // noc 25.10 P1 (M0): nasz limit predkosci siedzi teraz w agencie (do jednorazowego zdjecia po karze)
        }

        // ---- noc 25.10 P1 (M0, klucz SpeedCapOnlyWhenWinded): liczniki zapisow limitu predkosci (glowny watek; segment 'marsz' linii NB2,
        // zerowane w RoutHold.Ensure przez MarchToContact.ResetCounters)
        private static int _spdPenalty, _spdMarch, _spdRelease, _spdOld;

        internal static void ResetSpeedCounters() { _spdPenalty = _spdMarch = _spdRelease = _spdOld = 0; }

        /// <summary>Koncowka segmentu 'marsz' (bez znaku '|'): zapisy limitu FieldCraft w tej misji. Wolajacy trzyma RoutHold.Gate.</summary>
        internal static void AppendSpeed(System.Text.StringBuilder sb, Settings c)
        {
            sb.Append("; FieldCraft limit tylko przy karze ").Append(c != null && c.SpeedCapOnlyWhenWinded ? "TAK" : "NIE")
              .Append(": zapisy kary ").Append(_spdPenalty).Append(" (w marszu ").Append(_spdMarch).Append("), zdjecia ").Append(_spdRelease)
              .Append(", stare zapisy co 0.2 s ").Append(_spdOld);
        }

        // ---- 127 (25.10, Jeff: "bieganie w zbrojach ... w zaleznosci od tego ile mam na sobie", "ogolna zasada", "wyjsc od sredniej"):
        // postawy RBM ludzi, ktorzy TERAZ biegna - BattleWind.TickStaminaPrefix nie oddaje im oddechu (glowny watek: FieldCraft pisze co 0.2 s,
        // RBM StanceLogic.OnMissionTick czyta). Liczniki linii "Bieg (127)" na koniec misji.
        private static readonly HashSet<object> _runningStances = new HashSet<object>();
        private static int _runN, _regenSkipped;
        private static float _runKgSum, _runKgMax, _runTSum, _runTMin = float.MaxValue;
        private static float _plKg = -1f, _plAth, _plT;

        internal static bool IsRunning(object stance) { return stance != null && _runningStances.Count > 0 && _runningStances.Contains(stance); }

        internal static void NoteRegenSkipped() { _regenSkipped++; }

        private static void ResetRunStats()
        {
            _runningStances.Clear();
            _runN = 0; _regenSkipped = 0;
            _runKgSum = 0f; _runKgMax = 0f; _runTSum = 0f; _runTMin = float.MaxValue;
            _plKg = -1f; _plAth = 0f; _plT = 0f;
        }

        /// <summary>127: sekundy pelnego biegu do zera, jedna regula dla kazdego czlowieka na polu:
        /// T = RunSecondsUnarmoured x sprawnosc / (1 + k x kg)^2; sprawnosc = 0.75 + Atletyka/400 (0.6..1.6); k = ArmourRunCostPerKgPercent/100
        /// (Askew, Formenti, Minetti 2012: zbroja 30-50 kg = koszt biegu x1.9 -> 2.25%/kg; kwadrat - drozszy krok i nizsze tempo, ktore cialo utrzyma).</summary>
        internal static float RunSecondsFor(float athletics, float armourKg, Settings c)
        {
            float fit = 0.75f + BattleWind.CurvedAthletics(Math.Max(0f, athletics), c) / 400f;   // 130: Atletyka po tej samej krzywej co pula RBM
            if (fit < 0.6f) fit = 0.6f;
            if (fit > 1.6f) fit = 1.6f;
            float m = 1f + Math.Max(0f, armourKg) * Math.Max(0f, c.ArmourRunCostPerKgPercent) / 100f;
            return Math.Max(5f, Math.Max(10f, c.RunSecondsUnarmoured) * fit / (m * m));
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

        // 127: czas pelnego biegu z fizjologii (RunSecondsFor), drenaz = cala pula / ten czas - wielkosc puli (Endurance bohaterow,
        // Atletyka w RBM) wydluza WALKE, nie bieg. Dawniej staly drenaz 4 + 1 pkt/kg: bohater z regeneracja 37 pkt/s (x3 po 10 s bez walki)
        // w 46 kg zbroi nie tracil nic. Endurance bohaterow dalej powieksza pule (raz, gdy BattleWind nieczynny).
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
            try { if (hero != null && !c.StaminaFromAthleticsOnly) st.EndBonus = BattleWind.EnduranceMul(hero); }   // 130: bez Endurance - Atletyka jak u kazdego
            catch { }
            float w = 0f;
            try { w = agent.SpawnEquipment.GetTotalWeightOfArmor(true); } catch { }

            st.RunSeconds = RunSecondsFor(ath, w, c);
            _runN++;
            _runKgSum += w;
            if (w > _runKgMax) _runKgMax = w;
            _runTSum += st.RunSeconds;
            if (st.RunSeconds < _runTMin) _runTMin = st.RunSeconds;
            if (agent.IsMainAgent) { _plKg = w; _plAth = ath; _plT = st.RunSeconds; }
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
            try
            {
                State gone;
                if (_states.TryGetValue(affectedAgent, out gone) && gone.RbmStance != null) _runningStances.Remove(gone.RbmStance);
                _states.Remove(affectedAgent); _mountOwners.Remove(affectedAgent);
            }
            catch { }
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

        /// <summary>m110-stan krok 2 (CompanionGearWear): ksiega ciosow agenta - gracz WearLedger (jak dotad), towarzysz w druzynie gracza zarejestrowany przy wejsciu
        /// do bitwy (HeroHp, zgodna zbroja) - jego ksiega misji (koniec misji przenosi ja do HeroBook); reszta (i klucz NIE) - null.</summary>
        internal static float[] LedgerOf(Agent a)
        {
            if (a == null) return null;
            if (a.IsMainAgent) return WearLedger;
            return HeroHp.LedgerOf(a);
        }

        // E8/A (250, PLAN 8.8e, regula R4 "arena i turniej nie obijaja"): ksiegi zuzycia (WearLedger bohatera i TroopWearLedger wojska) pisza tylko w BITWIE
        // kampanii - misja z bitwa gracza na mapie (MapEvent.PlayerMapEvent) i CombatType Combat. Sam CombatType nie wystarcza: zaden zarzadzany kod gry
        // 1.4.8 go nie ustawia (recenzja E8/X), wiec arena i turniej moga miec Combat; rozstrzyga bitwa na mapie - ten sam warunek co strzal w
        // XpFair.ShotPrefix. Arena, turniej, cwiczenia, bojka w zaulku, ucieczka z lochu nie maja bitwy na mapie - ciosy z nich nie ida do ksiag.
        // Ksiega bohatera jest tez zerowana na poczatku kazdej bitwy gracza i po bitwie przegranej albo opuszczonej (ArmouryBehavior) - dotad ciosy spoza
        // bitwy i z kleski czekaly w niej do najblizszego zwyciestwa. Klucz ArenaNoWear.
        private int _battle = -1;                 // -1 nie sprawdzone, 0 misja poza bitwa, 1 bitwa kampanii (stale przez cala misje)
        private int _skipHits, _skipShots;        // ciosy / strzaly bohatera pominiete w tej misji (misja poza bitwa)

        private bool InBattle()
        {
            if (_battle < 0)
            {
                try
                {
                    _battle = TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent != null
                              && Mission.CombatType == Mission.MissionCombatType.Combat ? 1 : 0;
                }
                catch { return true; }            // nie wiadomo - jak dotad (ksiegi pisza); sprawdzimy przy nastepnym ciosie
            }
            return _battle == 1;
        }

        /// <summary>E8/A: suma ksiegi bohatera i jej wyzerowanie (poczatek bitwy gracza, bitwa przegrana albo opuszczona).</summary>
        internal static float DropLedger()
        {
            float sum = 0f;
            for (int i = 0; i < 12; i++) { sum += WearLedger[i]; WearLedger[i] = 0f; }
            return sum;
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_skipHits + _skipShots > 0)
                    Log.Info("Zuzycie (E8/A): misja poza bitwa (arena, turniej, bojka) - " + _skipHits + " ciosow i " + _skipShots
                             + " strzalow bohatera nie poszlo do ksiegi zuzycia; rynsztunek bez zmian.");
                _skipHits = 0; _skipShots = 0;
            }
            catch { }
            try
            {
                var c = Settings.Current;
                if (_runN > 0 && c != null)
                    Log.Info("Bieg (127): ludzi zmierzonych " + _runN + " | zbroja sr. " + (_runKgSum / _runN).ToString("0.0") + " kg (najwiecej "
                             + _runKgMax.ToString("0.0") + ") | pelny bieg do zera sr. " + (_runTSum / _runN).ToString("0") + " s (najkrocej "
                             + _runTMin.ToString("0") + ") | regula: " + c.RunSecondsUnarmoured.ToString("0") + " s bez zbroi przy Atletyce 100, "
                             + c.ArmourRunCostPerKgPercent.ToString("0.##") + "%/kg do kwadratu | bez oddechu w biegu " + (c.NoBreathWhileRunning ? "TAK" : "NIE")
                             + " (pominiete regeneracje RBM " + _regenSkipped + ")"
                             + (_plKg >= 0f ? " | gracz: zbroja " + _plKg.ToString("0.0") + " kg, Atletyka " + _plAth.ToString("0") + " -> " + _plT.ToString("0") + " s" : " | gracz: -"));
            }
            catch { }
            ResetRunStats();
            try { MissionTeams.Flush(Mission); } catch { }   // NM: linia "Druzyny w misji (NM)" raz na misje
            base.OnEndMission();
        }

        // NM: liczniki misji od poczatku misji (rodzaj misji i bitwa na mapie liczone raz)
        public override void AfterStart()
        {
            try { MissionTeams.Begin(Mission); } catch { }
            ResetRunStats();
            base.AfterStart();
        }

        internal static int ArmorSlotFor(BoneBodyPartType part, Agent a)   // m110-stan: takze LootWear (ta sama czesc ciala -> slot zbroi co u gracza)
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
                // E8/A: poza bitwa kampanii (arena, turniej, cwiczenia) strzal nie zuzywa luku bohatera ani wojska - cala metoda pisze tylko do ksiag
                if (c.ArenaNoWear && !InBattle())
                {
                    if (c.WearEnabled && shooterAgent != null && shooterAgent.IsMainAgent) _skipShots++;
                    return;
                }
                // wpis 96: strzal zolnierza partii gracza - luk/kusza
                if (c.TroopWearEnabled && shooterAgent != null && shooterAgent.IsHuman && !shooterAgent.IsHero && BattlefieldLaw.IsMainPartyAgent(shooterAgent))
                {
                    var ti = shooterAgent.SpawnEquipment[weaponIndex].Item;
                    if (ti != null && (ti.ItemType == ItemObject.ItemTypeEnum.Bow || ti.ItemType == ItemObject.ItemTypeEnum.Crossbow)) TroopWearLedger.Add(ti.ItemType, 1f);
                }
                if (!c.WearEnabled || shooterAgent == null) return;
                var led = LedgerOf(shooterAgent);   // m110-stan krok 2: gracz albo towarzysz z ksiega misji (CompanionGearWear)
                if (led == null) return;
                var item = shooterAgent.SpawnEquipment[weaponIndex].Item;
                if (item == null) return;
                if (item.ItemType != ItemObject.ItemTypeEnum.Bow && item.ItemType != ItemObject.ItemTypeEnum.Crossbow) return;

                int tier = Recipes.Grade(item);
                int skill = 0;
                try
                {
                    var who = shooterAgent.IsMainAgent ? Hero.MainHero : (HeroHp.HeroOf(shooterAgent) ?? Hero.MainHero);   // umiejetnosc strzelca
                    skill = who.GetSkillValue(item.ItemType == ItemObject.ItemTypeEnum.Bow
                        ? TaleWorlds.Core.DefaultSkills.Bow : TaleWorlds.Core.DefaultSkills.Crossbow);
                }
                catch { }
                float uses = Math.Max(100, c.BowUsesAtTier1) * tier
                           * (1f + skill * Math.Max(0f, c.BowSkillBonusPercentPerPoint) / 100f);
                // ApplyWear dzieli potem przez solidnosc tieru - kompensujemy, zeby liczby Jeffa trzymaly co do sztuki
                float sturdiness = 1f + Math.Max(0, tier - 1) * Settings.Current.TierDurabilityFactor;
                int slot = (int)weaponIndex;
                if (slot >= 0 && slot < 4) led[slot] += 100f / uses * sturdiness;
            }
            catch { }
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                var c = Settings.Current;
                // E8/A: ksiegi zuzycia pisza tylko w bitwie kampanii (arena, turniej, bojka - nie); odpinanie strzal nizej dziala wszedzie jak dotad
                bool counts = !c.ArenaNoWear || InBattle();
                if (!counts && c.WearEnabled && blow.InflictedDamage + Math.Max(0, attackCollisionData.AbsorbedByArmor) > 0
                    && ((affectedAgent != null && affectedAgent.IsMainAgent) || (affectorAgent != null && affectorAgent.IsMainAgent)))
                    _skipHits++;

                // wpis 96: zuzycie WOJSKA z przebiegu walki - trafienia zolnierzy partii gracza (nie bohaterow)
                if (c.TroopWearEnabled && counts)
                {
                    try
                    {
                        int raw0 = blow.InflictedDamage + Math.Max(0, attackCollisionData.AbsorbedByArmor);
                        if (raw0 > 0 && affectedAgent != null && affectedAgent.IsHuman && !affectedAgent.IsHero && BattlefieldLaw.IsMainPartyAgent(affectedAgent))
                        {
                            if (attackCollisionData.AttackBlockedWithShield) TroopWearLedger.Add(ItemObject.ItemTypeEnum.Shield, 1f);
                            else TroopWearLedger.Add(TroopWearLedger.TypeOfSlot(ArmorSlotFor(blow.VictimBodyPart, affectedAgent)),
                                blow.IsMissile ? Math.Max(0f, Math.Min(100f, c.MissileArmorWearPercent)) / 100f : 1f);
                        }
                        if (raw0 > 0 && affectorAgent != null && affectorAgent.IsHuman && !affectorAgent.IsHero && affectorWeapon.Item != null
                            && !blow.IsMissile && BattlefieldLaw.IsMainPartyAgent(affectorAgent))
                            TroopWearLedger.Add(affectorWeapon.Item.ItemType, 1f);
                    }
                    catch { }
                }

                // --- zuzycie sprzetu gracza: tylko od faktycznych zdarzen ---
                if (c.WearEnabled && counts)
                {
                    int raw = blow.InflictedDamage + Math.Max(0, attackCollisionData.AbsorbedByArmor);
                    // strzala robi w pancerzu dziurke, nie wyrwe - liczymy tylko ulamek
                    // jej obrazen jako zuzycie (obrazenia HP zostaja normalne)
                    float missileScale = blow.IsMissile
                        ? Math.Max(0f, Math.Min(100f, c.MissileArmorWearPercent)) / 100f
                        : 1f;
                    // m110-stan krok 2: ksiega ofiary - gracz (WearLedger, jak dotad) albo towarzysz z ksiega misji (CompanionGearWear)
                    var vled = affectedAgent != null && raw > 0 ? LedgerOf(affectedAgent) : null;
                    if (vled != null)
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
                            // TA SAMA ULGA CO W BITWIE (Jeff 13.09): tarcza zatrzymujaca
                            // strzale liczyla PELNE zuzycie, bez znizki dla pociskow, ktora
                            // dostaje pancerz linijke nizej - a przy wlaczonym "Break At Zero
                            // Condition" dojechana do zera tarcza znika z ekwipunku na zawsze.
                            // Strzaly zuzywaja tarcze w tym samym procencie, w jakim ja lupia.
                            float shieldMissile = blow.IsMissile
                                ? Math.Max(0f, Math.Min(100f, c.MissileShieldDamagePercent)) / 100f
                                : 1f;
                            if (sh >= 0) vled[sh] += raw * c.WearDamageFactor * Math.Max(0f, c.WearShieldFactor) * shieldMissile;
                        }
                        else
                        {
                            int slot = ArmorSlotFor(blow.VictimBodyPart, affectedAgent);
                            // model Jeffa: do ksiegi idzie SUROWY cios - na stan przeliczy go
                            // pula wytrzymalosci sztuki (pancerz x 20 x tier), jeden za jeden
                            vled[slot] += raw * missileScale;
                        }
                    }
                    // Twoj celny cios zuzywa TWOJA bron BIALA; luki i kusze placa
                    // ZA STRZAL (OnAgentShootMissile), nie za trafienie
                    // m110-stan krok 2: takze bron towarzysza z ksiega misji (LedgerOf)
                    var aled = affectorAgent != null && raw > 0
                        && affectorWeapon.Item != null
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Bow
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Crossbow
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Thrown ? LedgerOf(affectorAgent) : null;
                    if (aled != null)
                    {
                        int ws = WeaponSlotFor(affectorAgent, affectorWeapon.Item);
                        if (ws >= 0) aled[ws] += Math.Max(0f, c.WearWeaponPerHit);
                    }
                    // kon gracza obrywa - cierpi uprzaz (ulamek, nie polowa:
                    // kon to wielki cel i przy 0.5 siodla sypaly sie seriami); m110-stan krok 2: takze kon towarzysza z ksiega misji
                    var hled = affectedAgent != null && affectedAgent.IsMount && affectedAgent.RiderAgent != null && raw > 0 ? LedgerOf(affectedAgent.RiderAgent) : null;
                    if (hled != null)
                        hled[11] += raw * Math.Max(0f, c.HarnessWearFactor) * missileScale;
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

                // MIGAWKA LISTY (log 15.09: "Collection was modified" x25 w OnMissionTick):
                // agent.Die() przy wykrwawieniu usuwa agenta z Mission.Agents W TRAKCIE
                // tej petli - reszta ludzi nie dostawala tiku. Iterujemy po kopii.
                foreach (var agent in new System.Collections.Generic.List<Agent>(Mission.Agents))
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
                        if (st.RunSeconds < 0f) MeasureWind(agent, st);
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
                                         + runThr.ToString("0.0") + "), pelny bieg do zera " + st.RunSeconds.ToString("0") + " s (127).");
                            }
                        }
                        var stance = c.UseRbmStamina ? RbmStanceOf(agent, st, step) : null;
                        // 127: w biegu oddech nie wraca nikomu (BattleWind.TickStaminaPrefix pomija regeneracje RBM tej postawy)
                        if (stance != null)
                        {
                            if (sprinting && c.NoBreathWhileRunning) _runningStances.Add(stance);
                            else _runningStances.Remove(stance);
                        }
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
                                    float d = maxStam / st.RunSeconds * step;   // 127: cala pula w RunSeconds pelnego biegu
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
                            if (sprinting) st.Pool -= st.PoolMax / st.RunSeconds * step;
                            else st.Pool += st.RegenPerSec * step;
                            if (st.Pool < 0f) st.Pool = 0f;
                            if (st.Pool > st.PoolMax) st.Pool = st.PoolMax;
                            float ratio = st.PoolMax > 1f ? st.Pool / st.PoolMax : 1f;
                            st.Pace = PaceFromRatio(st, ratio);
                        }
                    }
                    else
                    {
                        st.Pace = 1f; st.Winded = false;
                        if (st.RbmStance != null) _runningStances.Remove(st.RbmStance);
                    }

                    // --- kara za rany (ruch liczony razem z zadyszka) ---
                    float woundSlow = 0f;
                    float thr = c.WoundedBelowPercent / 100f;
                    if (c.WoundedPenaltiesEnabled && hpr < thr && thr > 0.01f)
                        woundSlow = c.WoundedMaxSlow * (thr - hpr) / thr;

                    // ruch: zadyszka (ostatnie 10% puli -> do -50%) i rany licza sie razem
                    float limit = st.Pace;
                    limit = Math.Min(limit, 1f - woundSlow);
                    if (limit < 0.4f) limit = 0.4f;

                    // chod na zyczenie: trzymanie lewego Ctrl = idziesz, stamina odpoczywa, wygladasz
                    // jak czlowiek, nie jak sprinter. 136 (poprawka komentarza, bez zmiany zachowania):
                    // gra na PC MA przelacznik chodu - GameKey 30 ToggleWalkMode = CapsLock
                    // (CombatHotKeyCategory.cs:161; MissionMainAgentController.cs:811 wysyla Walk/Run
                    // takze w siodle); Ctrl to nasz chod "na przytrzymanie". W siodle Ctrl ogranicza
                    // tylko jezdzca (limit czlowieka), nie konia - czy kon go slucha, niesprawdzone
                    if (c.WalkKeyEnabled && agent.IsMainAgent)
                    {
                        try
                        {
                            if (TaleWorlds.InputSystem.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftControl))
                                limit = Math.Min(limit, Math.Max(0.2f, c.WalkSpeedShare));
                        }
                        catch { }
                    }
                    // noc 25.10 P1 (M0, Jeff: "armie nie biegly non stop tylko szly"): dawniej limit 1.0 co 0.2 s KAZDEMU kasowal limit szyku, ktory
                    // silnik liczy co 0.5 s (HumanAIComponent.AdjustSpeedLimit - linia i tempo szyku, progi RBM). Teraz zapis tylko przy karze
                    // (zadyszka, rany, klawisz chodu: limit < 0.999), a po jej zejsciu raz (-1, bez mnoznika) - silnik przywraca swoj limit w ciagu 0.5 s.
                    // Kara spotyka marsz (MarchToContact): Min(limit x pelna predkosc, gorny limit marszu), wartosc bezwzgledna. Klucz NIE = jak dawniej.
                    if (!c.SpeedCapOnlyWhenWinded)
                    {
                        agent.SetMaximumSpeedLimit(limit, true);
                        st.SpeedApplied = true;
                        _spdOld++;
                    }
                    else if (limit < 0.999f)
                    {
                        float cap = agent.MountAgent == null ? MarchToContact.CapFor(agent) : -1f;
                        float run = 0f;
                        if (cap > 0f) { try { run = agent.GetMaximumForwardUnlimitedSpeed(); } catch { run = 0f; } }
                        if (cap > 0f && run > 0.5f)
                        {
                            agent.SetMaximumSpeedLimit(Math.Min(limit * run, cap), false);
                            _spdMarch++;
                        }
                        else agent.SetMaximumSpeedLimit(limit, true);
                        st.SpeedApplied = true;
                        _spdPenalty++;
                    }
                    else if (st.SpeedApplied)
                    {
                        agent.SetMaximumSpeedLimit(-1f, false);
                        st.SpeedApplied = false;
                        _spdRelease++;
                    }

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
                            // ZAOKRAGLANIE SILNIKA (13.09, audyt): setter Agent.Health robi
                            // MathF.Ceiling na kazdym zapisie, wiec strata 0.1 HP na takt
                            // (0.5/s x 0.2 s) zaokraglala sie z powrotem w gore i NIGDY sie
                            // nie zapisywala - krwawienie bylo martwym kodem, nikt nigdy
                            // nie wykrwawil sie na polu. Zbieramy ulamki w dlugu i zdejmujemy
                            // dopiero PELNE punkty, ktore silnik przyjmuje.
                            st.BleedDebt += c.BleedPerSecond * step;
                            int lose = (int)st.BleedDebt;
                            if (lose > 0)
                            {
                                st.BleedDebt -= lose;
                                float nh = agent.Health - lose;
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
                        }
                        // NM: FleeHere na koncu warunku - poza bitwa na mapie (turniej, arena, bojka) st.Panicked zostaje false
                        // NB2 (16.10, klucz BrokenMenNeverRally): jedna regula z BrokenMen - RoutHold.Break (raz Panic na bitwe, morale
                        // przypiete na 0, ponowienia bez okrzyku i cel ucieczki w RoutKeeper). Odmowa Break (koniec bitwy, bez druzyny, odwrot
                        // z rozkazu szyku) zostawia st.Panicked false - sprobujemy przy nastepnym takcie. Ponowne FleeTheField (Panic co 0,2 s -
                        // zapetlony krzyk z logu 16.10: 257 wywolan na 7 ludzi) tylko na starej drodze (klucz wylaczony).
                        bool rout = RoutHold.On(Mission);
                        if (broken && c.AiFleeWhenNearDeath && !agent.IsMainAgent && agent.IsAIControlled && !st.Panicked && FleeHere(agent))
                        {
                            if (rout) st.Panicked = RoutHold.Break(agent, MissionTeams.Flee);
                            else
                            {
                                st.Panicked = true;
                                FleeTheField(agent);
                            }
                        }
                        else if (!rout && st.Panicked && !agent.IsMainAgent && agent.IsAIControlled && !agent.IsRetreating() && FleeHere(agent))
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

        // NM (klucz WoundedFleeBattlesOnly): zlamany ucieka tylko w bitwie na mapie (MissionTeams.IsBattle, raz na misje) - w turnieju, na arenie
        // i w bojce walczy do nokautu, jak chce gra (TournamentFightMissionController.CanAgentRout = false). Wylaczenie klucza = jak dotad.
        private int _fleeHere = -1;               // -1 nie sprawdzone, 0 misja poza bitwa na mapie, 1 bitwa

        private bool FleeHere(Agent agent)
        {
            var c = Settings.Current;
            if (c == null || !c.WoundedFleeBattlesOnly) return true;
            if (_fleeHere < 0) _fleeHere = MissionTeams.IsBattle(Mission) ? 1 : 0;
            if (_fleeHere == 1) return true;
            MissionTeams.Held(MissionTeams.Flee, agent);
            return false;
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
                // NM: czlowiek bez druzyny (Team.Invalid) nie ucieka - przed Panic, zeby nie zostal w panice bez odwrotu
                if (!MissionTeams.Valid(agent.Team)) { MissionTeams.Skip(agent, MissionTeams.Flee); return; }
                try { var cai = agent.CommonAIComponent; if (cai != null) cai.Panic(); } catch { }

                Vec2 my = agent.Position.AsVec2;
                Agent foe = null; float best = float.MaxValue;
                foreach (var a in mission.Agents)
                {
                    if (a == null || !a.IsActive() || !a.IsHuman) continue;
                    // NM: wrog tylko z wazna druzyna (natywne IsEnemy na Team.Invalid sie wywraca)
                    if (MissionTeams.Skip(a, MissionTeams.Flee) || !MissionTeams.Enemies(a.Team, agent.Team)) continue;
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
                MissionTeams.Pushed(MissionTeams.Flee, agent);
                agent.Retreat(wp);
            }
            catch (Exception e) { MissionTeams.Stumble(MissionTeams.Flee, "FleeTheField", e); }
        }
    }
}
