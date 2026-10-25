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

        /// <summary>m111-walka (po recenzji): latka wpieta z powodu oddechu albo postury szeregowych (stan przy starcie). False = wpieta tylko dla
        /// Duel Fair Guard - poza pojedynkiem postfiks wychodzi od razu (bitwy jak bez latki).</summary>
        private static bool _baseOn;

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

        // ---- 137 ODDECH DWUFAZOWY (Jeff 25.10 ok. 12:55: "regeneracja ... powinna byc tak samo szybsza im wiecej mam atletyki oraz im mam mniej staminy
        // a odpoczywam ... jak jestem wyczerpany i dysze to wyjscie z zadyszki jak w prawdziwej fizjologii ludzi"; "ogolna zasada", "wyjsc od sredniej").
        // Jedna regula dla kazdego czlowieka (gracz, bohaterowie, szeregowi; umarli bez oddechu): pasek = pula - F - S, F dlug szybki (fosfokreatyna,
        // szybka faza tlenu), S dlug wolny (kwasica, wolna faza). Wydatek (cios, blok, trafienie, strzal, bieg) dzieli sie miekko: szybki zbiornik
        // o pojemnosci Fm = BreathFastShare x pula, dF/dc = 1 - (F/Fm)^2 (Chorley 2022: po wyczerpaniu 50.67% szybko / 49.33% wolno; Lievens 2024:
        // po czesciowym zmeczeniu jedna krzywa). Odnowa: F i S wykladniczo z tau szybkim i wolnym, razy mnoznik aktywnosci (stoi 1, idzie wedlug zbroi,
        // kon 0.85, walka BreathFightShare, bieg 0). Projekt: scratchpad regen-model.txt (MODEL, IMPL, poprawki CHECK E).
        private sealed class Breath
        {
            public Agent Agent;
            public float F, S, E;          // dlug szybki, dlug wolny (pkt), licznik zadyszek 0..2
            public float LastT;            // MBCommon.GetTotalMissionTime ostatniego ticku
            public float LastSpendT;       // czas ostatniego wlasnego wydatku poza biegiem (okno walki)
            public float TauF, TauS, G, WalkM, Fm;
            public bool AtZero;            // tylko do licznika E (stan zadyszki trzyma FieldCraft st.Winded)
            public bool IsPlayer;
        }

        // stance RBM -> oddech; sprzata sie sam razem z obiektem RBM (wzor Managed)
        private static readonly ConditionalWeakTable<object, Breath> Breaths = new ConditionalWeakTable<object, Breath>();
        // odczyt i zapis pol Stance bez boxingu i bez alokacji w ticku
        private static AccessTools.FieldRef<object, float> _rStamina, _rMaxStamina;
        private static bool _breathReady;        // latki InitializeStamina i tickStaminaRegen wpiete i pola znalezione

        // stale z komentarzem i zrodlem (projekt regen-model.txt, sekcja MODEL 3)
        private const float AthFastK = 0.75f;    // Atletyka 300 skraca tau szybki x1.75 (Pesta 2013: niewytrenowani 41 s, wytrzymalosciowi 30 s; Chorley 2022 21.5 s)
        private const float AthSlowK = 0.30f;    // Atletyka 300 skraca tau wolny tylko x1.3 (Chorley 2022: brak zwiazku tau wolnego z kondycja)
        private const float KgFast = 0.002f;     // zbroja: tau szybki +0.2%/kg (46 kg x1.09; Haseler 1999: nawet niedotlenienie tylko +34%)
        private const float KgSlow = 0.005f;     // zbroja: tau wolny +0.5%/kg (46 kg x1.23; Askew 2012: utrudniony oddech w zbroi)
        private const float WalkBase = 0.85f, WalkPerKg = 0.0075f, WalkMin = 0.4f;   // chod: 0.85 - 0.0075 x kg, najmniej 0.4 (Askew 2012 chod w zbroi x2.1-2.3; Lievens 2021)
        private const float MountedShare = 0.85f;  // na koniu: jak chod bez zbroi
        private const float ECap = 2f, EDecaySeconds = 900f;   // licznik zadyszek: sufit 2, zanik 1 na 15 min (Chorley 2022: druga zadyszka tau wolny 388 -> 716 s)
        private const float FightWindow = 5f;    // walka = wlasny wydatek w ostatnich 5 s
        private const float ShotWindow = 2f;     // wydatek tuz po strzale (lucznik) - okno walki tylko 2 s
        private const float ShotLag = 0.6f;      // "tuz po strzale": LastRangedAttackTime mlodszy niz 0.6 s (tick RBM co 0.5 s)

        // liczniki misji - linia "Oddech (137)" raz na koniec misji (FieldCraft.OnEndMission; zapasowo przy pierwszym oddechu nastepnej misji)
        private static int _bN, _bStumbles, _bWindIn, _bWindPeople, _bExitN, _plWindN;
        private static float _bTauFSum, _bTauSSum, _bWindSecs, _bExitSecs, _plWindSecs;
        private static float _plA = -1f, _plA2, _plKg, _plP, _plTauF, _plTauS;
        private static WeakReference _bMission;

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

        // 130: krzywa Atletyki (klucz AthleticsStaminaCurve; 1.0 = linia RBM). A' = 300 x (A / 300)^p - przy 300 tyle co RBM, nizej troche mniej,
        // wyzej wiecej; kazdy kolejny punkt daje nieco wiecej niz poprzedni (p 1.12: 10 -> 7, 100 -> 88, 200 -> 191, 230 -> 223, 300 -> 300, 400 -> 414).
        private const float CurveRef = 300f;
        private static float _lastAth, _lastAth2;
        private static int _curveN;

        internal static float CurveP(Settings c) { return Clamp(c != null ? c.AthleticsStaminaCurve : 1f, 1f, 1.5f); }

        internal static float CurvedAthletics(float a, Settings c)
        {
            if (a <= 0f) return 0f;
            float p = CurveP(c);
            if (p <= 1.0001f) return a;
            return CurveRef * (float)Math.Pow(a / CurveRef, p);
        }

        /// <summary>130: pula i regeneracja RBM z Atletyki po krzywej - stosunek do linii RBM (zostaja inne mnozniki RBM: ciezki korpus x0.5, mnoznik gracza).</summary>
        private static void ApplyAthleticsCurve(Agent agent, object stance, Settings c)
        {
            if (CurveP(c) <= 1.0001f) return;
            float a;
            try { a = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(agent, DefaultSkills.Athletics); }
            catch { return; }
            float a2 = CurvedAthletics(a, c);
            if (agent.IsPlayerControlled) { _lastAth = a; _lastAth2 = a2; }
            if (Math.Abs(a2 - a) < 0.5f) return;
            float kPool = (1f + 3f * a2 / 500f) / (1f + 3f * a / 500f);
            float kRegen = (1f + 2f * a2 / 500f) / (1f + 2f * a / 500f);
            _fMaxStamina.SetValue(stance, (float)_fMaxStamina.GetValue(stance) * kPool);
            _fStamina.SetValue(stance, (float)_fStamina.GetValue(stance) * kPool);
            _fStaminaRegen.SetValue(stance, (float)_fStaminaRegen.GetValue(stance) * kRegen);
            _curveN++;
        }

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
                    Breath bd; if (Breaths.TryGetValue(stance, out bd)) Breaths.Remove(stance);   // 137: umarli bez oddechu - tick idzie oryginalem RBM
                    // 137: latka wpieta takze bez oddechu bohaterow (BattleStaminaEnabled wylaczony przy starcie) - umarli wtedy jak dotad: czysty RBM
                    if (!Active) return;
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
                // 130 (Jeff 25.10: "to powinno isc wykladniczo ... kazdy kolejny punkt atletyki kosztuje wiecej XP zatem tez kazdy punkt daje nieco wiecej
                // do staminy jak i jej regeneracji"): kazdy czlowiek (szeregowy i bohater) - pula i regeneracja RBM liczone z Atletyki po krzywej (CurvedAthletics)
                var cs0 = Settings.Current;
                if (cs0 != null) ApplyAthleticsCurve(agent, stance, cs0);
                // 137: oddech dwufazowy - kazdy czlowiek (po krzywej 130 i po galezi umarlych); pula zostaje, jak ja policzono wyzej
                if (cs0 != null && cs0.BreathTwoPhase && _breathReady) MakeBreath(agent, stance, cs0);
                var hero = HeroOf(agent);
                if (hero == null) return;
                // 130 (25.10, Jeff: "rycerz tier 6 ma atletyka 200 i ja mam atletyka 200 powinnismy miec taka sama stamine", "to powinno byc zalezne od atletyki
                // nie od tieru"): klucz StaminaFromAthleticsOnly - bohater (gracz, lordowie, towarzysze) ma pule i regeneracje RBM z Atletyki jak kazdy szeregowy;
                // bez mnoznika Endurance i bez wlasnego Profile (tick idzie oryginalem RBM). Postura bez zmian (HeroPostureFromSkill / DuelFairGuard).
                var cs = Settings.Current;
                if (cs != null && cs.StaminaFromAthleticsOnly)
                {
                    Profile drop; if (Managed.TryGetValue(stance, out drop)) Managed.Remove(stance);
                    if (agent.IsPlayerControlled)
                        Log.Info("BattleWind (130): gracz - stamina z Atletyki jak kazdy (RBM po krzywej " + CurveP(cs).ToString("0.00") + "): Atletyka "
                                 + _lastAth.ToString("0") + " liczona jak " + _lastAth2.ToString("0") + " | pula " + (int)(float)_fMaxStamina.GetValue(stance)
                                 + ", regeneracja " + ((float)_fStaminaRegen.GetValue(stance) * RbmTickCount / RbmTickSeconds).ToString("0.0") + " pkt/s (bez mnoznika Endurance).");
                    return;
                }
                // 137: latka wpieta takze bez oddechu bohaterow (BattleStaminaEnabled wylaczony przy starcie) - wtedy jak dotad: Endurance doklada FieldCraft
                // (BattleWind.Active = false), tu nic - inaczej mnoznik Endurance poszedlby dwa razy
                if (!Active) return;
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
                if (!_baseOn && !BattleSense.InDuel(agent.Mission)) return;   // m111-walka: latka tylko dla pojedynku - poza nim nic (ani trupy, ani NB2)
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
                    // m111-postawa (Jeff 24.10: "czemu moj pasek postawy jest powiekszany, to cheatowanie, to bylo nieporozumienie" - 26.08 chodzilo o oddech,
                    // nie o garde): klucz HeroPostureFromSkill - postura bohatera z umiejetnosci broni w reku (SkillGuard x0.75..1.5, jak szeregowi z tieru),
                    // w bitwie i w pojedynku; Endurance juz nie powieksza gardy (zostaje przy oddechu - StaminaInitPostfix)
                    if (cd != null && cd.HeroPostureFromSkill) { SkillPosture(agent, stance, cd); return; }
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

        /// <summary>m111-postawa: mnoznik postury bohatera z umiejetnosci broni - TA SAMA skala co szeregowi wedlug tieru (SoldierMul), tier rownowazny
        /// t = 1 + (s - 20) / 32 (ciagly, 1..7: umiejetnosc 20 = T1, 84 = T3, 148 = T5, 180 = T6, 212+ = T7) - lord 180 broni sie jak szeregowy T6.
        /// Przy domyslnych suwakach: 24 x0.86, 100 x1.20, 140 x1.43, 180 x1.70, 212+ x1.95. (Dotad m111-walka: 0.75 + s / 400 - lord 180 x1.2 przy T6 x1.7.)</summary>
        internal static float SkillGuard(int s)
        {
            var c = Settings.Current;
            float m1 = Clamp(c != null ? c.SoldierPostureTier1Multiplier : 0.85f, 0.5f, 1.5f);
            float m6 = Clamp(c != null ? c.SoldierPostureTier6Multiplier : 1.7f, 1f, 3f);
            double t = 1.0 + (s - 20) / 32.0;
            if (t < 1.0) t = 1.0;
            if (t > 7.0) t = 7.0;
            return (float)(m1 * Math.Pow(m6 / m1, (t - 1.0) / 5.0));
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

        private static WeakReference _skillLogM;
        private static float _skillLogMul = -1f;

        /// <summary>m111-postawa: postura bohatera z umiejetnosci broni w reku (DuelAi.DuelSkill - bohater co najmniej udzial najlepszej) - pula i regeneracja
        /// x SkillGuard(s); bez Endurance. Linia gracza raz na misje i przy zmianie mnoznika.</summary>
        private static void SkillPosture(Agent agent, object stance, Settings c)
        {
            int held, best;
            int s = DuelAi.DuelSkill(agent, c, out held, out best);
            float mul = SkillGuard(s);
            _fMaxPosture.SetValue(stance, (float)_fMaxPosture.GetValue(stance) * mul);
            _fPosture.SetValue(stance, (float)_fPosture.GetValue(stance) * mul);
            _fPostureRegen.SetValue(stance, (float)_fPostureRegen.GetValue(stance) * mul);
            RoutHold.PostureHero(agent);
            if (BattleSense.InDuel(agent.Mission)) DuelGuard.NotePosture(agent, mul, false);
            if (agent.IsPlayerControlled)
            {
                var w = _skillLogM;
                bool same = w != null && ReferenceEquals(w.Target, agent.Mission) && Math.Abs(_skillLogMul - mul) < 0.005f;
                if (!same)
                {
                    _skillLogM = new WeakReference(agent.Mission);
                    _skillLogMul = mul;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    Log.Info("BattleWind (m111-postawa): gracz postura x" + mul.ToString("0.00", inv) + " z umiejetnosci broni (skill " + s + ", w reku " + held
                             + (best > 0 ? ", najlepsza " + best : "") + ", Endurance bez wplywu na garde" + (BattleSense.InDuel(agent.Mission) ? ", pojedynek" : ", bitwa")
                             + ", pula " + ((float)_fMaxPosture.GetValue(stance)).ToString("0", inv) + ").");
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
                // 137: czlowiek z oddechem dwufazowym - wlasna odnowa zamiast RBM (bez gumki, bez x3 po 10 s spokoju, bez x0.5 za helm);
                // klucz BreathTwoPhase czytany na zywo - wylaczony = ponizej jak dotad
                Breath b;
                if (_breathReady && Breaths.TryGetValue(__instance, out b))
                {
                    var cb = Settings.Current;
                    if (cb != null && cb.BreathTwoPhase) return BreathTick(__instance, b, cb);
                }
                // 127: w biegu oddech nie wraca nikomu - bohaterom ani szeregowym (FieldCraft wpisuje biegnacych co 0.2 s)
                if (FieldCraft.IsRunning(__instance)) { FieldCraft.NoteRegenSkipped(); return false; }
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

        // ---- 137 oddech dwufazowy ----

        /// <summary>137: oddech czlowieka - raz przy InitializeStamina (po krzywej 130). Tau liczone raz: A' = CurvedAthletics(Atletyka efektywna),
        /// kg = zbroja (GetTotalWeightOfArmor(true) - ta sama liczba co w biegu 127 i w kosztach RBM). Wyjatek liczony, funkcja zostaje.</summary>
        private static void MakeBreath(Agent agent, object stance, Settings c)
        {
            try
            {
                float a = 0f;
                try { a = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(agent, DefaultSkills.Athletics); } catch { a = 0f; }
                if (a < 0f) a = 0f;
                float a2 = CurvedAthletics(a, c);
                float kg = 0f;
                try { kg = Math.Max(0f, agent.SpawnEquipment.GetTotalWeightOfArmor(true)); } catch { kg = 0f; }
                float max = _rMaxStamina(stance);
                var b = new Breath();
                b.Agent = agent;
                // tau szybki: BreathFastSeconds (40 s przy Atletyce 0) / (1 + 0.75 A'/300) x (1 + 0.002 kg) - A0 40 s, A100 33 s, A200 27 s, A300 23 s
                b.TauF = Clamp(c.BreathFastSeconds, 15f, 90f) / (1f + AthFastK * a2 / 300f) * (1f + KgFast * kg);
                // tau wolny: BreathSlowSeconds (400 s przy Atletyce 0) / (1 + 0.30 A'/300) x (1 + 0.005 kg) - A0 400 s, A300 308 s
                b.TauS = Clamp(c.BreathSlowSeconds, 120f, 1200f) / (1f + AthSlowK * a2 / 300f) * (1f + KgSlow * kg);
                // spowolnienie wolnego po kolejnej zadyszce: 1 - 0.5 A'/300 (Chorley 2022: mniejsze przy wyzszym VO2max, r = -0.68)
                b.G = Clamp(1f - 0.5f * a2 / 300f, 0f, 1f);
                b.WalkM = Math.Max(WalkMin, WalkBase - WalkPerKg * kg);
                b.Fm = Clamp(c.BreathFastShare, 0.2f, 0.8f) * max;
                b.LastT = MBCommon.GetTotalMissionTime();
                b.LastSpendT = -1000000f;
                b.IsPlayer = agent.IsPlayerControlled;
                Breath old; if (Breaths.TryGetValue(stance, out old)) Breaths.Remove(stance);   // ponowne InitializeStamina na tym samym obiekcie - nadpisz
                Breaths.Add(stance, b);
                BreathMission(agent.Mission);
                _bN++;
                _bTauFSum += b.TauF;
                _bTauSSum += b.TauS;
                if (b.IsPlayer) { _plA = a; _plA2 = a2; _plKg = kg; _plP = max; _plTauF = b.TauF; _plTauS = b.TauS; }
            }
            catch (Exception e) { BreathStumble("BattleWind.MakeBreath", e); }
        }

        /// <summary>137: tick oddechu (RBM co 0.5 s, tylko gdy stamina &lt; max). Bez alokacji; funkcje przestepne tylko przy nowym wydatku.
        /// Zwraca false (RBM nie dolicza swojej regeneracji); przy wyjatku true - ten jeden tick idzie oryginalem RBM, potkniecie liczone.</summary>
        private static bool BreathTick(object stance, Breath b, Settings c)
        {
            try
            {
                float max = _rMaxStamina(stance);
                if (max < 1f) return true;
                float cur = _rStamina(stance);
                float now = MBCommon.GetTotalMissionTime();
                float raw = now - b.LastT;
                b.LastT = now;
                if (raw < 0f) raw = 0f;
                float dt = raw > 2f ? 2f : raw;            // F i S: przyciete (tick nie przychodzi przy pelnym pasku)
                // licznik zadyszek zanika z PELNEGO czasu - takze z minut przy pelnym pasku, gdy RBM nie wola ticku
                if (b.E > 0f) { b.E -= raw / EDecaySeconds; if (b.E < 0f) b.E = 0f; }
                bool running = FieldCraft.IsRunning(stance);

                // 1. uzgodnienie zamiast lapania kosztow: D = dlug widoczny na pasku, T = F + S
                float fm = Clamp(c.BreathFastShare, 0.2f, 0.8f) * max;
                b.Fm = fm;
                float d = max - cur;
                if (d < 0f) d = 0f;
                float t = b.F + b.S;
                if (d > t + 0.01f)
                {
                    // nowy wydatek (cios i blok RBM, trafienie, strzal, bieg FieldCraft) - szybki zbiornik przelewa sie miekko:
                    // F' = Fm x tanh(atanh(F/Fm) + c/Fm), reszta do wolnego (.NET 4.7.2 nie ma Math.Atanh)
                    float spend = d - t;
                    double x = b.F / fm;
                    if (x < 0.0) x = 0.0;
                    if (x > 0.999999) x = 0.999999;
                    double at = 0.5 * Math.Log((1.0 + x) / (1.0 - x));
                    float fn = (float)(fm * Math.Tanh(at + spend / fm));
                    if (fn < b.F) fn = b.F;                  // zbiornik juz pelny (np. mniejszy udzial z MCM) - wszystko do wolnego
                    if (fn - b.F > spend) fn = b.F + spend;
                    b.S += spend - (fn - b.F);
                    b.F = fn;
                    if (!running)
                    {
                        // walka = WLASNY wydatek (cios, blok, trafienie); pola Agent.Last*Time nie widza blokow (RegisterBlow wychodzi przy 0 obrazen)
                        float shot = float.MinValue;
                        try { if (b.Agent != null) shot = b.Agent.LastRangedAttackTime; } catch { }
                        if (now - shot < ShotLag)
                        {
                            // lucznik: wydatek tuz po strzale otwiera okno walki tylko na 2 s (inaczej strzal co 4-5 s trzyma go na stale w walce)
                            float w = now - (FightWindow - ShotWindow);
                            if (w > b.LastSpendT) b.LastSpendT = w;
                        }
                        else b.LastSpendT = now;
                    }
                }
                else if (d < t - 0.01f && t > 0f)
                {
                    // ktos dodal staminy (inna latka, mod) - dlugi w tej samej proporcji
                    float k = d / t;
                    b.F *= k;
                    b.S *= k;
                }

                // 2. licznik zadyszek E: +1 raz na zadyszke (pasek <= 0.5% puli), zwolnienie przy pasku >= progu wyjscia (ten sam co FieldCraft)
                float bar = max - b.F - b.S;
                if (bar <= 0.005f * max)
                {
                    if (!b.AtZero) { b.AtZero = true; b.E = Math.Min(ECap, b.E + 1f); }
                }
                else if (b.AtZero && bar >= WindedExit(c) * max) b.AtZero = false;

                // 3. aktywnosc: bieg 0 (127), walka BreathFightShare, kon 0.85, chod wedlug zbroi, postoj 1
                float m;
                var ag = b.Agent;
                if (running) { m = 0f; FieldCraft.NoteRegenSkipped(); }
                else if (now - b.LastSpendT < FightWindow) m = Clamp(c.BreathFightShare, 0f, 1f);
                else if (ag != null && ag.MountAgent != null) m = MountedShare;
                else if (ag != null && ag.GetCurrentVelocity().LengthSquared > 0.25f) m = b.WalkM;   // ponad 0.5 m/s
                else m = 1f;

                // 4. odnowa: pierwszy rzad (dt/tau <= 0.09 przy 2 s i tau 23 s)
                if (m > 0f && dt > 0f)
                {
                    float kf = m * dt / b.TauF;
                    if (kf > 1f) kf = 1f;
                    float tsEf = b.TauS * (1f + b.G * Math.Max(0f, b.E - 1f));
                    float ks = m * dt / tsEf;
                    if (ks > 1f) ks = 1f;
                    b.F -= b.F * kf;
                    b.S -= b.S * ks;
                }
                if (b.F + b.S < 0.5f)
                {
                    // resztka ponizej pol punktu: pasek pelny od razu (inaczej float zrownywalby go z max godzinami i tick szedlby dalej)
                    b.F = 0f;
                    b.S = 0f;
                    _rStamina(stance) = max;
                }
                else
                {
                    float v = max - b.F - b.S;
                    _rStamina(stance) = v < 0f ? 0f : v;
                }
                if (b.IsPlayer) _plP = max;
                return false;
            }
            catch (Exception e) { BreathStumble("BattleWind.BreathTick", e); return true; }
        }

        /// <summary>137: czy ta postawa RBM ma oddech dwufazowy (klucz BreathTwoPhase na zywo) - FieldCraft: prog wyjscia z zadyszki i bez zwrotu SprintDebt.</summary>
        internal static bool HasBreath(object stance)
        {
            if (stance == null || !_breathReady) return false;
            var c = Settings.Current;
            if (c == null || !c.BreathTwoPhase) return false;
            Breath b;
            return Breaths.TryGetValue(stance, out b);
        }

        /// <summary>137: prog wyjscia z zadyszki przy oddechu dwufazowym (klucz WindedExitBar, przyciety jak suwak 0.10..0.50).</summary>
        internal static float WindedExit(Settings c)
        {
            return Clamp(c != null ? c.WindedExitBar : 0.30f, 0.10f, 0.50f);
        }

        // 137: zadyszki z FieldCraft (jedno zrodlo stanu: st.Winded) - tylko ludzie z oddechem dwufazowym
        internal static void NoteWindIn(bool player, bool firstForMan)
        {
            _bWindIn++;
            if (firstForMan) _bWindPeople++;
            if (player) _plWindN++;
        }

        internal static void NoteWindTime(float seconds, bool player)
        {
            _bWindSecs += seconds;
            if (player) _plWindSecs += seconds;
        }

        internal static void NoteWindOut(float seconds)
        {
            _bExitN++;
            _bExitSecs += seconds;
        }

        private static void BreathStumble(string where, Exception e)
        {
            _bStumbles++;
            if (_bStumbles <= 3) Log.Error(where, e);   // licz potkniecia, nie gas funkcji (pulapka _tentBroken)
        }

        /// <summary>137: nowa misja przy pierwszym oddechu - jesli poprzednia nie zapisala linii (np. bez FieldCraft), zapis teraz.</summary>
        private static void BreathMission(Mission m)
        {
            var w = _bMission;
            if (w != null && ReferenceEquals(w.Target, m)) return;
            if (_bN > 0) BreathFlush();
            _bMission = new WeakReference(m);
        }

        /// <summary>137: linia "Oddech (137)" raz na koniec misji (FieldCraft.OnEndMission) i zerowanie licznikow.</summary>
        internal static void BreathFlush()
        {
            try
            {
                if (_bN > 0)
                {
                    var c = Settings.Current;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var sb = new System.Text.StringBuilder(400);
                    sb.Append("Oddech (137): ludzi ").Append(_bN)
                      .Append(" | zadyszek ").Append(_bWindIn).Append(" (ludzi ").Append(_bWindPeople).Append(')')
                      .Append(" | sr. czas w zadyszce ").Append(_bWindPeople > 0 ? (_bWindSecs / _bWindPeople).ToString("0", inv) : "-").Append(" s")
                      .Append(" | sr. wyjscie z zadyszki ").Append(_bExitN > 0 ? (_bExitSecs / _bExitN).ToString("0", inv) : "-").Append(" s (wyjsc ").Append(_bExitN).Append(')')
                      .Append(" | sr. TauF/TauS ").Append((_bTauFSum / _bN).ToString("0.0", inv)).Append(" / ").Append((_bTauSSum / _bN).ToString("0", inv)).Append(" s");
                    if (_plA >= 0f)
                        sb.Append(" | gracz: Atletyka ").Append(_plA.ToString("0", inv)).Append(" (").Append(_plA2.ToString("0", inv)).Append(")")
                          .Append(", zbroja ").Append(_plKg.ToString("0.0", inv)).Append(" kg, pula ").Append(_plP.ToString("0", inv))
                          .Append(", TauF ").Append(_plTauF.ToString("0.0", inv)).Append(" s, TauS ").Append(_plTauS.ToString("0", inv))
                          .Append(" s, zadyszek ").Append(_plWindN).Append(", sekund w zadyszce ").Append(_plWindSecs.ToString("0", inv));
                    else sb.Append(" | gracz: -");
                    sb.Append(" | potkniecia ").Append(_bStumbles);
                    if (c != null)
                        sb.Append(" | regula: szybki ").Append(Clamp(c.BreathFastSeconds, 15f, 90f).ToString("0", inv)).Append(" s, wolny ")
                          .Append(Clamp(c.BreathSlowSeconds, 120f, 1200f).ToString("0", inv)).Append(" s, zbiornik ").Append(Clamp(c.BreathFastShare, 0.2f, 0.8f).ToString("0.00", inv))
                          .Append(", walka x").Append(Clamp(c.BreathFightShare, 0f, 1f).ToString("0.00", inv)).Append(", wyjscie ").Append((WindedExit(c) * 100f).ToString("0", inv))
                          .Append("% | klucz BreathTwoPhase ").Append(c.BreathTwoPhase ? "TAK" : "NIE");
                    Log.Info(sb.ToString());
                }
            }
            catch { }
            _bN = 0; _bStumbles = 0; _bWindIn = 0; _bWindPeople = 0; _bExitN = 0; _plWindN = 0;
            _bTauFSum = 0f; _bTauSSum = 0f; _bWindSecs = 0f; _bExitSecs = 0f; _plWindSecs = 0f;
            _plA = -1f; _plA2 = 0f; _plKg = 0f; _plP = 0f; _plTauF = 0f; _plTauS = 0f;
            _bMission = null;
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
                // m111-walka (po recenzji): Duel Fair Guard tez potrzebuje latki InitializePosture - dotad przy wylaczonym oddechu i tierach nie dzialal,
                // a linia "DuelAi tabela" mowila TAK; bitwy bez zmian (_baseOn = false -> postfiks poza pojedynkiem wychodzi od razu)
                bool duelGuard = c != null && c.DuelFairGuard;
                bool heroSkill = c != null && c.HeroPostureFromSkill;   // m111-postawa: postura bohaterow z umiejetnosci tez w bitwie - latka potrzebna poza pojedynkiem
                bool noBreath = c != null && c.NoBreathWhileRunning;    // 127: latka tickStaminaRegen potrzebna takze bez oddechu bohaterow
                // 137 (CHECK E5): InitializeStamina niesie krzywa 130 i oddech dwufazowy - dotad wpinana tylko przy BattleStaminaEnabled, wiec wylaczenie
                // tego klucza po cichu gasilo krzywa (i gasiloby oddech). Teraz takze przy BreathTwoPhase albo krzywej > 1; tickStaminaRegen takze przy oddechu.
                bool breath = c != null && c.BreathTwoPhase;
                bool curve = c != null && CurveP(c) > 1.0001f;
                bool initS = wind || breath || curve;
                bool tickS = wind || noBreath || breath;
                _baseOn = wind || tiers || heroSkill;
                if (!wind && !tiers && !duelGuard && !heroSkill && !noBreath && !breath && !curve) { Log.Info("BattleWind: wylaczone."); return; }
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
                if (mIP == null || (wind && (mIS == null || mTS == null || mTP == null)) || (initS && mIS == null) || (tickS && mTS == null))
                { Log.Info("BattleWind: metody RBMAI.Stance nie pasuja - odpuszczam."); return; }

                h.Patch(mIP, postfix: new HarmonyMethod(typeof(BattleWind), "PostureInitPostfix"));
                PosturePatched = true;
                if (initS) h.Patch(mIS, postfix: new HarmonyMethod(typeof(BattleWind), "StaminaInitPostfix"));
                if (tickS) h.Patch(mTS, prefix: new HarmonyMethod(typeof(BattleWind), "TickStaminaPrefix"));
                if (wind)
                {
                    h.Patch(mTP, prefix: new HarmonyMethod(typeof(BattleWind), "TickPosturePrefix"));
                    Active = true;
                    Log.Info("BattleWind: oddech bitewny bohaterow wg Endurance (pula 2^((END-2.5)/"
                             + Settings.Current.BattleEndDoubleEvery.ToString("0.#") + "), regen "
                             + (int)Settings.Current.BattleRegenAtEnd1 + "-" + (int)Settings.Current.BattleRegenAtEnd10
                             + " pkt/s, zadyszka " + Settings.Current.BattleWindedFloor.ToString("0.00") + ").");
                }
                else if (tickS)
                    Log.Info("BattleWind (127/137): oddech bohaterow wylaczony (BattleStaminaEnabled) - latka tickStaminaRegen wpieta dla zasady 'w biegu oddech nie wraca'"
                             + " i oddechu dwufazowego; InitializeStamina " + (initS ? "wpieta (krzywa 130, oddech 137)" : "niewpieta") + ".");
                // 137: oddech dwufazowy - pola stamina / maxStamina przez FieldRefAccess (bez boxingu); klucz BreathTwoPhase dalej na zywo (spawn i tick)
                _breathReady = false;
                if (initS && tickS)
                {
                    try
                    {
                        _rStamina = AccessTools.FieldRefAccess<float>(tStance, "stamina");
                        _rMaxStamina = AccessTools.FieldRefAccess<float>(tStance, "maxStamina");
                        _breathReady = _rStamina != null && _rMaxStamina != null;
                    }
                    catch (Exception e) { _breathReady = false; Log.Error("BattleWind.ApplyAll (137 FieldRefAccess)", e); }
                }
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                Log.Info("BattleWind (137): oddech dwufazowy " + (_breathReady ? "gotowy" : "NIEGOTOWY (latki albo pola RBM)") + ", klucz BreathTwoPhase przy starcie "
                         + (breath ? "TAK" : "NIE") + " (na zywo) | szybki " + (c != null ? c.BreathFastSeconds.ToString("0", inv) : "-") + " s, wolny "
                         + (c != null ? c.BreathSlowSeconds.ToString("0", inv) : "-") + " s, zbiornik " + (c != null ? c.BreathFastShare.ToString("0.00", inv) : "-")
                         + ", walka x" + (c != null ? c.BreathFightShare.ToString("0.00", inv) : "-") + ", wyjscie z zadyszki " + (c != null ? (WindedExit(c) * 100f).ToString("0", inv) : "-")
                         + "% | InitializeStamina " + (initS ? "TAK" : "NIE") + ", tickStaminaRegen " + (tickS ? "TAK" : "NIE") + ".");
                if (!wind && tiers) Log.Info("BattleWind: oddech bohaterow wylaczony (BattleStaminaEnabled) - wpieta postura szeregowych wedlug tieru.");
                else if (!wind && !tickS) Log.Info("BattleWind: oddech bohaterow i postura szeregowych wylaczone - latka InitializePosture wpieta tylko dla postury w pojedynku (DuelFairGuard).");
                Log.Info("BattleWind (NB2): postura szeregowych wedlug tieru - latka InitializePosture wpieta (klucz SoldierPostureByTier na zywo"
                         + ", przy starcie " + (tiers ? "TAK" : "NIE") + "); bez broni glownej bez ponownego mnozenia; postura bohatera w pojedynku (DuelFairGuard) "
                         + (duelGuard ? "TAK" : "NIE") + (_baseOn ? "" : ", poza pojedynkiem latka nic nie robi") + ".");
            }
            catch (Exception e) { Log.Error("BattleWind.ApplyAll", e); }
        }
    }
}
