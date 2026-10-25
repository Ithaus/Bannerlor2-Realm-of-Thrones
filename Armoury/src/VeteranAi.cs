using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NB2 czesc 2 (16.10, Jeff: "tier 6 doswiadczony, tier 1 slabiutki"): WETERANI WEDLUG TIERU - wspolczynniki walki AI po RBM (klucz VeteransByTier).
    /// Rozpoznanie rozp_ai (16.10) krok 3.
    ///
    /// Dowod [K]: RBM (RBMAI AgentAi.cs:22-300, postfiks AgentStatCalculateModel.SetAiRelatedProperties) liczy poziom = umiejetnosc walki wrecz / 250 x
    /// trudnosc (0.7 / 0.85 / 1.0); od ok. 150 umiejetnosci (tier 5) blok i parowanie maja juz limity RBM (parowanie bez tarczy 0.6, zla strona
    /// bloku 0.5, atak po parowaniu 0.4, wyczucie bloku wroga 0.35) - t5 = t6; agresja stala (AIDecideOnAttackChance 0.5, AIAttackOnDecideChance 0.15);
    /// czesc pol zostaje z gry (umiejetnosc / 300 x 0.1 / 0.32 / 0.96 - na Easy t6 broni sie prawie losowo: AiRandomizedDefendDirectionChance 0.98).
    ///
    /// Zasady (postfiks Priority.Last po "com.rbmai" - RBM przepina swoje latki co misje w EarlyStart, Harmony sortuje od nowa; RBM zostaje):
    ///  - tylko AI-czlowiek (nie gracz, nie umarly), tylko bitwa na mapie / kryjowka / bitwa wlasna, tylko gdy RBM AI z postura (rbmAiEnabled,
    ///    postureEnabled, bez vanillaCombatAi) - inaczej limity sa inne i nic nie ruszamy;
    ///  - poziom = max(poziom RBM, prog tieru 0.05 + 0.13 x (tier - 1)): T1 0.05 ... T5 0.57, T6 0.70, T7 0.83 (na Realistic t6 i tak ok. 0.96);
    ///    kapitan szyku z Tactics >= 150 dodaje 0.05;
    ///  - pola zostawione z gry (RBM ich nie pisze): od tieru 3 wzory gry z tym poziomem (losowy kierunek obrony, timery po trafieniu i parowaniu,
    ///    ocena ogluszenia, parowanie w ataku, chamber, zmiana decyzji parowania); tier 1-2 - pola gry bez zmian (poprawka 16.10);
    ///  - brama misji z BattleSense.InBattle (bez MissionTeams.Kind - postfiks bywa wolany z ticku asynchronicznego; poprawka 16.10);
    ///  - tier 5+ bez tarczy: limity RBM wyzej (parowanie 0.7 / 0.8 / 0.85, zla strona 0.55 / 0.6 / 0.65, wyczucie bloku 0.38 / 0.42 / 0.46, atak po
    ///    parowaniu 0.42 / 0.46 / 0.5 dla T5 / T6 / T7); pola, ktore RBM mnozy przez zmeczenie (stamina), skalujemy stosunkiem - zmeczenie RBM zostaje;
    ///  - agresja wedlug tieru: obie szanse ataku x (0.8 + 0.08 x (tier - 1)) - T1 x0.80, T4 x1.04, T6 x1.20, T7 x1.28.
    /// Wydajnosc: RBM wola to dla kazdego czlowieka co 3 s (StanceLogic.cs:1923-1932) - bez LINQ i alokacji (poza zbiorem liczb do linii misji).
    /// Wyjatek liczony (RoutHold.Stumble, miejsce "weteran"), funkcja dalej dziala.
    /// m111-walka faza 1 (24.10): ten sam postfiks niesie profil pojedynku (DuelAi, klucz DuelAiEnabled) - latka wpinana, gdy VeteransByTier
    /// albo DuelAiEnabled; w misji pojedynku (BattleSense.InDuel) przy DuelAiEnabled biegnie DuelAi.Apply i wyjscie, w bitwie kod weteranow bez
    /// zmian. Brama galezi RBM (RbmBranchOn) wspolna; czynnik zmeczenia RBM sFac = clamp(AIDecideOnAttackChance / 0.5, 0.85, 1) czytany na wejsciu.
    /// m113-walka2 faza 2 (24.10, Jeff po fazie 1: walka w polu ma byc trudniejsza): w bitwie bohater AI (lord, towarzysz, przeciwnik-bohater) od
    /// poziomu 0.40 dostaje profil DuelAi (DuelistsInBattles, DuelAi.ApplyField, runda 0), ponizej - regula weteranow z tierem co najmniej
    /// z najlepszej umiejetnosci (HeroTierFromSkill, SkillTier); VeteranAggressionFix - AIDecideOnAttackChance (szansa porzucenia zamachu) bez
    /// mnozenia w gore od T4 (robilo weterana bardziej biernym), T1-T3 dalej w dol jak w 106. Srednie AoD / DoA wedlug tieru do linii FieldDuel.
    /// Po recenzji: bohater z profilem dostaje co najmniej prog tieru z reguly weteranow (DuelAi.ApplyField, TierMin).
    /// 145 (25.10, fala "AI walczy o zycie"): ten sam postfiks niesie "ranny walczy ostroznie" (WoundedWary, klucz WoundedFightWarily) - szeregowy AI ponizej
    /// WoundedBelowPercent HP w trybie ROZSADEK: atak po decyzji x f, porzucenie zamachu i obrona tarcza x (2 - f), blok blizej 1 (f = 0.4 + 0.6 x HP / prog);
    /// na koncu postfiksu, po licznikach weteranow; bohaterowie poza zasada (krok 149). Latka wpinana takze przy samym WoundedFightWarily.
    /// </summary>
    internal static class VeteranAi
    {
        internal const string HarmonyId = "com.jeff.armoury";
        internal static bool Patched;
        private static MethodInfo _target;

        // brama misji: bitwa i galaz RBM z postura (liczone raz na misje)
        private static WeakReference _gm;
        private static bool _gOn;
        private static float _gDm = 1f;
        private static FieldInfo _fAi, _fPosture, _fVanilla;
        private static bool _cfgDone;

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _calls;
        private static readonly HashSet<int>[] _tiers = NewTiers();
        private static readonly HashSet<int> _above = new HashSet<int>(), _shield = new HashSet<int>(), _cmd = new HashSet<int>();
        private static string _sample6;
        private static string _order = "?";

        private static HashSet<int>[] NewTiers()
        {
            var a = new HashSet<int>[8];
            for (int i = 0; i < 8; i++) a[i] = new HashSet<int>();
            return a;
        }

        internal static void ResetCounters()
        {
            _calls = 0;
            for (int i = 0; i < _tiers.Length; i++) _tiers[i].Clear();
            _above.Clear(); _shield.Clear(); _cmd.Clear();
            _sample6 = null;
            _order = "?";
        }

        /// <summary>145: kolejnosc latki wobec RBM (CheckOrder) do linii "ranni ostrozni (145)" - wolajacy trzyma Gate.</summary>
        internal static string OrderLocked { get { return _order; } }

        internal static int Activity
        {
            get { int s = 0; for (int i = 1; i < _tiers.Length; i++) s += _tiers[i].Count; return s; }
        }

        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | weterani wg tieru ").Append(c != null && c.VeteransByTier ? "TAK" : "NIE").Append(" (latka ").Append(Patched ? "TAK" : "NIE")
              .Append(", po RBM ").Append(_order).Append("): ludzi ").Append(Activity).Append(" (");
            for (int i = 1; i < _tiers.Length; i++) sb.Append(i > 1 ? ", " : "").Append('T').Append(i).Append(' ').Append(_tiers[i].Count);
            sb.Append("), wywolan ").Append(_calls).Append(", ponad limit RBM ").Append(_above.Count).Append(", z tarcza ").Append(_shield.Count)
              .Append(", kapitan Tactics 150+ ").Append(_cmd.Count).Append("; probka T6 parowanie ").Append(_sample6 ?? "-");
        }

        // ------------------------------------------------------------ wpiecie (menu glowne, SubModuleMain)

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                // 145: takze przy WoundedFightWarily (ranny walczy ostroznie - ten sam postfiks)
                if (c == null || (!c.VeteransByTier && !c.DuelAiEnabled && !c.DuelistsInBattles && !c.WoundedFightWarily)) { Log.Info("VeteranAi (NB2): weterani wedlug tieru, Duel AI, bohaterowie-pojedynkowicze i ranni ostrozni wylaczeni przy starcie (VeteransByTier, DuelAiEnabled, DuelistsInBattles, WoundedFightWarily) - latka niewpieta."); return; }
                if (QuartermasterLaw.FindType("RBMAI.AgentAi") == null) { Log.Info("VeteranAi (NB2): RBM nieobecny - latka niewpieta (wspolczynniki gry bez zmian)."); return; }
                _target = AccessTools.DeclaredMethod(typeof(AgentStatCalculateModel), "SetAiRelatedProperties");
                if (_target == null) { Log.Info("VeteranAi (NB2): brak AgentStatCalculateModel.SetAiRelatedProperties - latka niewpieta."); return; }
                var hm = new HarmonyMethod(typeof(VeteranAi), nameof(Postfix)) { priority = Priority.Last, after = new[] { "com.rbmai" } };
                var r = h.Patch(_target, postfix: hm);
                Patched = r != null && Owned(_target, h.Id);
                Log.Info("VeteranAi (NB2): weterani wedlug tieru - latka SetAiRelatedProperties " + (Patched ? "wpieta" : "NIEWPIETA")
                         + " (postfiks Priority.Last po com.rbmai; klucze VeteransByTier i DuelAiEnabled na zywo, przy starcie " + (c.VeteransByTier ? "TAK" : "NIE") + " / "
                         + (c.DuelAiEnabled ? "TAK" : "NIE") + " / DuelistsInBattles " + (c.DuelistsInBattles ? "TAK" : "NIE") + " / WoundedFightWarily " + (c.WoundedFightWarily ? "TAK" : "NIE") + "; prog tieru 0.05 + 0.13 x (T-1), limity RBM wyzej od T5, agresja x0.80..x1.28; w pojedynku profil DuelAi).");
            }
            catch (Exception e) { Log.Error("VeteranAi.ApplyAll", e); }
        }

        private static bool Owned(MethodBase target, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                foreach (var p in info.Postfixes) if (p != null && p.owner == id) return true;
            }
            catch { }
            return false;
        }

        /// <summary>AfterStart (BattleSense): po przepieciu latek RBM w EarlyStart - czy nasz postfiks jest i stoi po RBM (do linii misji).</summary>
        internal static void CheckOrder(Mission m)
        {
            string order = "?";
            try
            {
                if (!Patched || _target == null) order = "bez latki";
                else
                {
                    var info = Harmony.GetPatchInfo(_target);
                    bool rbm = false, ours = false, after = false;
                    if (info != null)
                        foreach (var p in info.Postfixes)
                        {
                            if (p == null) continue;
                            if (p.owner == "com.rbmai") rbm = true;
                            if (p.owner == HarmonyId && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(VeteranAi))
                            {
                                ours = true;
                                if (p.priority == Priority.Last && p.after != null && Array.IndexOf(p.after, "com.rbmai") >= 0) after = true;
                            }
                        }
                    order = !ours ? "brak naszej" : (!rbm ? "brak RBM" : (after ? "TAK" : "NIE"));
                }
            }
            catch { order = "?"; }
            lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _order = order; }
        }

        // ------------------------------------------------------------ brama misji

        private static void Config()
        {
            if (_cfgDone) return;
            _cfgDone = true;
            try
            {
                var t = QuartermasterLaw.FindType("RBMConfig.RBMConfig");
                if (t == null) return;
                _fAi = t.GetField("rbmAiEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                _fPosture = t.GetField("postureEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                _fVanilla = t.GetField("vanillaCombatAi", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }
            catch { _fAi = _fPosture = _fVanilla = null; }
        }

        private static bool Flag(FieldInfo f, bool dflt)
        {
            try { if (f != null) { var v = f.GetValue(null); if (v is bool) return (bool)v; } } catch { }
            return dflt;
        }

        /// <summary>m111-walka: mnoznik trudnosci RBM teraz (tabela DuelAi, lista meczu, poziom w hakach postury).</summary>
        internal static float DiffMulNow() { return DiffMul(); }

        /// <summary>RBM: CampaignOptions.CombatAIDifficulty 0 / 1 / 2 -> 0.7 / 0.85 / 1.0 (Utilities.GetCombatAIDifficultyMultiplier, bez vanillaCombatAi).</summary>
        private static float DiffMul()
        {
            try
            {
                int d = (int)TaleWorlds.CampaignSystem.CampaignOptions.CombatAIDifficulty;
                return d == 0 ? 0.7f : (d == 1 ? 0.85f : 1f);
            }
            catch { return 1f; }
        }

        /// <summary>Brama misji. Poprawka po recenzji (16.10): rodzaj misji z BattleSense.InBattle (liczone w AfterStart na glownym watku, potem
        /// tylko czytane) zamiast MissionTeams.Kind - Kind moze wolac MissionTeams.Ensure (czysci HashSety bez blokady), a ten postfiks bywa
        /// wolany z ticku asynchronicznego druzyn. InBattle sprawdzane przy kazdym wywolaniu (dwa odczyty pol), w pamieci misji tylko galaz RBM.</summary>
        private static bool GateFor(Mission m, out float dm)
        {
            dm = 1f;
            if (!BattleSense.InBattle(m)) return false;
            return RbmBranchOn(m, out dm);
        }

        /// <summary>m111-walka: galaz RBM "postura, bez vanillaCombatAi" i mnoznik trudnosci - liczone raz na misje (wspolne dla bitwy i pojedynku).</summary>
        internal static bool RbmBranchOn(Mission m, out float dm)
        {
            dm = 1f;
            var gm = _gm;
            if (gm != null && ReferenceEquals(gm.Target, m)) { dm = _gDm; return _gOn; }
            bool on = false;
            float d = 1f;
            try
            {
                Config();
                on = _fAi != null && Flag(_fAi, false) && Flag(_fPosture, false) && !Flag(_fVanilla, true);
                d = DiffMul();
            }
            catch { on = false; }
            _gOn = on;
            _gDm = d;
            _gm = new WeakReference(m);
            dm = d;
            return on;
        }

        // ------------------------------------------------------------ postfiks

        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        /// <summary>Umiejetnosc walki wrecz jak gra (AgentStatCalculateModel.GetMeleeSkill) - te sama bierze RBM.</summary>
        private static SkillObject MeleeSkill(WeaponComponentData equippedItem, WeaponComponentData secondaryItem)
        {
            if (equippedItem == null) return DefaultSkills.Athletics;
            var rs = equippedItem.RelevantSkill;
            if (rs == DefaultSkills.OneHanded || rs == DefaultSkills.Polearm) return rs;
            if (rs != DefaultSkills.TwoHanded) return DefaultSkills.OneHanded;
            return secondaryItem == null ? DefaultSkills.TwoHanded : DefaultSkills.OneHanded;
        }

        /// <summary>Tarcza w drugiej rece (jak flaga RBM: bron drugiej reki to tarcza).</summary>
        internal static bool HasShield(Agent agent)
        {
            var idx = agent.GetOffhandWieldedItemIndex();
            if (idx < EquipmentIndex.WeaponItemBeginSlot || idx >= EquipmentIndex.NumAllWeaponSlots) return false;
            var w = agent.Equipment[idx];
            if (w.IsEmpty) return false;
            var u = w.CurrentUsageItem;
            return u != null && u.IsShield;
        }

        /// <summary>m113-walka2: najlepsza umiejetnosc broni bialej (OneHanded / TwoHanded / Polearm), skuteczna jak u RBM.</summary>
        private static int BestMelee(Agent agent, AgentStatCalculateModel model)
        {
            int oh = model.GetEffectiveSkill(agent, DefaultSkills.OneHanded);
            int th = model.GetEffectiveSkill(agent, DefaultSkills.TwoHanded);
            int pa = model.GetEffectiveSkill(agent, DefaultSkills.Polearm);
            return Math.Max(oh, Math.Max(th, pa));
        }

        /// <summary>m113-walka2 (HeroTierFromSkill): tier z najlepszej umiejetnosci broni bialej = clamp(1 + (best - 40) / 30, 1, 7) - 40 T1, 70 T2,
        /// 100 T3, 130 T4, 160 T5, 190 T6, 220+ T7.</summary>
        internal static int SkillTier(int best)
        {
            int t = best < 40 ? 1 : 1 + (best - 40) / 30;
            return t > 7 ? 7 : t;
        }

        public static void Postfix(Agent agent, AgentDrivenProperties agentDrivenProperties, WeaponComponentData equippedItem,
                                   WeaponComponentData secondaryItem, AgentStatCalculateModel __instance)
        {
            try
            {
                var c = Settings.Current;
                if (c == null) return;
                if (agent == null || agentDrivenProperties == null || __instance == null) return;
                var m = agent.Mission;
                if (m == null) return;
                float dm;
                // m111-walka: pojedynek (turniej, arena, pojedynek ROT) - profil DuelAi zamiast weteranow; czynnik zmeczenia RBM z jego wyniku
                if (c.DuelAiEnabled && BattleSense.InDuel(m))
                {
                    if (!RbmBranchOn(m, out dm)) return;
                    float sFac = Clamp(agentDrivenProperties.AIDecideOnAttackChance / 0.5f, 0.85f, 1f);
                    try { DuelAi.Apply(agent, agentDrivenProperties, equippedItem, secondaryItem, __instance, dm, sFac, c); }
                    catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelAi.Apply", e); }
                    return;
                }
                // m113-walka2 faza 2: w bitwie takze bohaterowie z profilem pojedynku (DuelistsInBattles) - weterani i bohaterowie za ta sama brama
                // 145: ranny walczy ostroznie (WoundedWary) za ta sama brama - takze bez weteranow; przy kluczu NIE pomiar na sucho (gdy latka jest)
                bool vet = c.VeteransByTier, duelists = c.DuelistsInBattles, wary = c.WoundedFightWarily;
                if (!vet && !duelists && !wary) return;
                if (!GateFor(m, out dm)) return;
                if (!agent.IsHuman || agent.IsMainAgent || !agent.IsAIControlled) return;
                var ch = agent.Character;
                if (ch == null || Undead.Character(ch)) return;

                bool hero = ch.IsHero;
                if (hero) WoundedWary.Hero(agent, m, c);       // 145: bohater poza zasada (krok 149, decyzja D4) - tylko licznik rannych bohaterow
                int best = -1;
                if (hero && duelists)
                {
                    // czynnik zmeczenia RBM z jego wyniku (przed nasza zmiana), jak w pojedynku
                    float sFac = Clamp(agentDrivenProperties.AIDecideOnAttackChance / 0.5f, 0.85f, 1f);
                    bool done = false;
                    try { done = DuelAi.ApplyField(agent, agentDrivenProperties, equippedItem, secondaryItem, __instance, dm, sFac, c, out best); }
                    catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVeteran, "DuelAi.ApplyField", e); }
                    if (done) return;
                }
                if (!vet)
                {
                    if (!hero) WoundedWary.Apply(agent, agentDrivenProperties, m, c);   // 145: bez weteranow - wprost na wyniku RBM
                    return;
                }

                int tier = BattleWind.TierOf(ch);
                // m113-walka2: bohater bez profilu pojedynku - tier co najmniej z najlepszej umiejetnosci broni bialej (40 = T1, co 30 punktow tier wiecej)
                bool raised = false;
                if (hero && c.HeroTierFromSkill)
                {
                    if (best < 0) best = BestMelee(agent, __instance);
                    int ts = SkillTier(best);
                    if (ts > tier) { tier = ts; raised = true; }
                }
                int skill = __instance.GetEffectiveSkill(agent, MeleeSkill(equippedItem, secondaryItem));
                float numR = Clamp(skill / 250f * dm, 0f, 1f);                     // poziom RBM (Utilities.CalculateAILevel)
                float lvl = Math.Max(numR, 0.05f + 0.13f * (tier - 1));
                bool cmd = false;
                var f = agent.Formation;
                var cap = f != null ? f.Captain : null;
                if (cap != null && cap != agent)
                {
                    var cc = cap.Character;
                    if (cc != null && cc.GetSkillValue(DefaultSkills.Tactics) >= 150) { lvl += 0.05f; cmd = true; }
                }
                lvl = Clamp(lvl, 0f, 1f);
                var p = agentDrivenProperties;

                // (1) pola zostawione z gry - wzory gry (AgentStatCalculateModel.SetAiRelatedProperties) z naszym poziomem. Poprawka po recenzji
                // (16.10): tier 1-2 bez zmian - poziom RBM (umiejetnosc / 250 x trudnosc) jest wyzszy niz poziom gry (umiejetnosc / 300 x 0.1 / 0.32 /
                // 0.96 x _AILevelMultiplier), wiec przeliczenie podnosiloby rekruta; "tier 1 slabiutki" = pola gry + agresja x0.80-0.88 + postura x0.85
                if (tier >= 3)
                {
                    float l2 = lvl * lvl;
                    p.AiRandomizedDefendDirectionChance = 1f - l2 * lvl;
                    p.AISetNoAttackTimerAfterBeingHitAbility = Lerp(0.33f, 1f, lvl);
                    p.AISetNoAttackTimerAfterBeingParriedAbility = Lerp(0.2f, 1f, l2);
                    p.AISetNoDefendTimerAfterHittingAbility = Lerp(0.1f, 0.99f, l2);
                    p.AISetNoDefendTimerAfterParryingAbility = Lerp(0.15f, 1f, l2);
                    p.AIEstimateStunDurationPrecision = 1f - Lerp(0.2f, 1f, lvl);
                    p.AIParryOnAttackAbility = lvl;
                    p.AIParryOnAttackingContinueAbility = Lerp(0.5f, 0.95f, lvl);
                    p.AiTryChamberAttackOnDecide = (lvl - 0.15f) * 0.1f;
                    p.AiParryDecisionChangeValue = 0.05f + 0.7f * lvl;
                }

                // (2) tier 5+ bez tarczy: limity RBM wyzej (galaz RBM "postura, bez tarczy" - AgentAi.cs:95-121)
                bool shield = HasShield(agent);
                bool above = false;
                string sample = null;
                if (tier >= 5 && !shield)
                {
                    int k = tier >= 7 ? 2 : (tier == 6 ? 1 : 0);
                    // parowanie: RBM clamp(num, 0.1, 0.6), bez zmeczenia - podnosimy tylko tam, gdzie RBM uderzyl w swoj limit
                    float before = p.AIParryOnDecideAbility;
                    if (before >= 0.6f - 1e-4f)
                    {
                        float now = Math.Min(k == 2 ? 0.85f : (k == 1 ? 0.8f : 0.7f), Math.Max(0.6f, lvl));
                        if (now > before + 1e-4f)
                        {
                            p.AIParryOnDecideAbility = now;
                            above = true;
                            if (tier == 6 && _sample6 == null) sample = before.ToString("0.00", Inv) + "->" + now.ToString("0.00", Inv);   // raz na misje (odczyt bez blokady - najwyzej dwie probki)
                        }
                    }
                    // pola ze zmeczeniem RBM (x Lerp(0.85, 1, stamina)): stosunek nowego surowego do surowego RBM - zmeczenie zostaje
                    float rawOld = Clamp(numR * 0.8f, 0.05f, 0.5f);
                    float rawNew = Clamp(lvl * 0.8f, 0.05f, k == 2 ? 0.65f : (k == 1 ? 0.6f : 0.55f));
                    if (rawNew > rawOld + 1e-4f) { p.AIRealizeBlockingFromIncorrectSideAbility *= rawNew / rawOld; above = true; }
                    rawOld = Clamp(numR * 0.46f, 0f, 0.35f);
                    rawNew = Clamp(lvl * 0.46f, 0f, k == 2 ? 0.46f : (k == 1 ? 0.42f : 0.38f));
                    if (rawOld > 0.01f && rawNew > rawOld + 1e-4f) { p.AIDecideOnRealizeEnemyBlockingAttackAbility *= rawNew / rawOld; above = true; }
                    rawOld = Clamp(numR * 0.45f, 0.2f, 0.4f);
                    rawNew = Clamp(lvl * 0.5f, 0.2f, k == 2 ? 0.5f : (k == 1 ? 0.46f : 0.42f));
                    if (rawNew > rawOld + 1e-4f) { p.AIAttackOnParryChance *= rawNew / rawOld; above = true; }
                }

                // (3) agresja wedlug tieru (RBM: stale 0.5 x zmeczenie i 0.15 / 0.3 konno). m113-walka2 (VeteranAggressionFix): AIDecideOnAttackChance
                // to szansa porzucenia zamachu dla decyzji (profil DuelAi obniza ja z umiejetnoscia 0.50 -> 0.35, zryw otwarcia daje 0.10) - mnozenie przez
                // agresje T6 x1.20 kazalo weteranowi czesciej rezygnowac z ataku; z kluczem odpada tylko mnozenie w gore (T4+ zostaje 0.50), T1-T3
                // dalej x0.80 / x0.88 / x0.96 jak w 106 (po recenzji: bez tego rekrut porzucal zamach do 25% czesciej - walka latwiejsza, nie trudniejsza)
                float agg = 0.8f + 0.08f * (tier - 1);
                if (!c.VeteranAggressionFix || agg < 1f) p.AIDecideOnAttackChance = Clamp(p.AIDecideOnAttackChance * agg, 0f, 1f);
                p.AIAttackOnDecideChance = Clamp(p.AIAttackOnDecideChance * agg, 0.05f, 0.6f);

                int id = agent.Index;
                lock (RoutHold.Gate)
                {
                    RoutHold.EnsureLocked(m);
                    _calls++;
                    _tiers[tier < 1 ? 1 : (tier > 7 ? 7 : tier)].Add(id);
                    if (above) _above.Add(id);
                    if (shield) _shield.Add(id);
                    if (cmd) _cmd.Add(id);
                    if (sample != null && _sample6 == null) _sample6 = sample;
                    FieldDuel.NoteTierLocked(id, tier, p.AIAttackOnDecideChance, p.AIDecideOnAttackChance, hero, raised);
                }
                // 145: ranny walczy ostroznie - PO licznikach weteranow (srednie tierow m113 bez rannych); bohater poza zasada (krok 149)
                if (!hero) WoundedWary.Apply(agent, p, m, c);
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVeteran, "VeteranAi.Postfix", e); }
        }
    }
}
