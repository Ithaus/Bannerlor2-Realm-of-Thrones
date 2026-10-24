using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// m111-walka faza 1 (24.10, Jeff: "jak atakuje caly czas, przeciwnik sie tylko broni; z drzewcem, ktorego sie dopiero ucze, bije duzo wyzsze
    /// poziomy; nie czulem, ze rycerz Gwardii Krolewskiej jest mocny"): PROFIL AI W POJEDYNKU - turniej, arena, pojedynek ROT (klucz DuelAiEnabled).
    ///
    /// Dowod [K]: RBM (RBMAI AgentAi.cs:95-121, galaz "postura, bez vanillaCombatAi") daje kazdemu od ok. 100 umiejetnosci ten sam zolw: blok
    /// clamp(2n, 0.3, 1), atak po decyzji 0.15 (z tarcza clamp(0.3n, 0.1, 0.15)), decyzja o ataku 0.5 x zmeczenie (gra przy D=0: 0), trzymanie
    /// zamachu 1 s (AgentAi.cs:178; gra dla wprawnych 0 s), atak po parowaniu najwyzej 0.4; poziom = umiejetnosc broni w reku / 250 x trudnosc
    /// (Utilities.cs:1320 - mnoznika rundy turnieju TournamentBehavior.cs:224 nie czyta, a i tak trafia on na opakowanie modelu Artema). VeteranAi
    /// dziala tylko w bitwach (BattleSense.InBattle), wiec w szrankach zostawal czysty RBM. Bron w turnieju wybiera mecz (RBMTournament), nie rycerz:
    /// rycerz 180 z mieczem dostaje halabarde i walczy jak nowicjusz.
    ///
    /// Zasady (wolane z VeteranAi.Postfix - ten sam postfiks SetAiRelatedProperties, Priority.Last po com.rbmai):
    ///  - tylko AI-czlowiek (nie gracz, nie umarly), tylko misja pojedynku (BattleSense.InDuel) i galaz RBM z postura (VeteranAi.RbmBranchOn);
    ///  - umiejetnosc s = umiejetnosc broni w reku (ta sama regula co RBM i gra: GetMeleeSkill); bohater: s = max(s, DuelAiBestSkillShare x
    ///    najlepsza z OneHanded / TwoHanded / Polearm);
    ///  - poziom L = clamp(s / 250 x trudnosc x (1 + DuelAiTourneyRoundStep x runda), 0, 1); ponizej 0.40 (umiejetnosc 100) - wartosci RBM bez zmian
    ///    (rekrut i dama zostaja slabi); powyzej t = (L - 0.4) / 0.6 i profil Shape (przy 100 rowny RBM, przy 250: atak po decyzji 0.45, decyzja
    ///    o ataku 0.35, atak po parowaniu 0.6, parowanie 0.85, wyczucie bloku 0.6, trzymanie zamachu 0.12 s, chamber 0.15, kopniecie 0.4);
    ///  - pola, ktore RBM mnozy przez zmeczenie, dostaja ten sam czynnik sFac (odczytany z wyniku RBM na wejsciu postfiksu);
    ///  - pola zostawione z gry (RBM ich nie pisze) - wzory gry z poziomem L; kopniecie i chamber nigdy ponizej wartosci gry, blok
    ///    max(RBM, clamp(2L, 0.3, 1)) - wzor RBM z poziomem pojedynku (po recenzji);
    ///  - zryw (DuelCraft) nadpisuje kilka pol na 1.2-1.5 s.
    /// Wydajnosc: RBM wola to dla kazdego czlowieka co 3 s; bez LINQ i alokacji (linia DU: najwyzej 40 migawek na misje w tablicy struktur).
    /// Postfiks bywa wolany z ticku asynchronicznego druzyn: czyta tylko statyki (BattleSense, DuelCraft), liczniki pod RoutHold.Gate.
    /// </summary>
    internal static class DuelAi
    {
        internal const float Floor = 0.40f;          // poziom RBM 100 umiejetnosci (x1.0) - ponizej profil RBM bez zmian
        internal const float PressedFrom = 0.56f;    // zryw pod naciskiem od poziomu 0.56 (umiejetnosc 140)

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal struct Shape
        {
            public float AoD, DoA, AoP, Parry, Feint, Incorrect, Chamber, Kick, Hold, HoldVar, MaxTime;
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        internal static float Share(Settings c) { return Clamp(c != null ? c.DuelAiBestSkillShare : 0.8f, 0f, 1f); }
        internal static float RoundStep(Settings c) { return Clamp(c != null ? c.DuelAiTourneyRoundStep : 0.08f, 0f, 0.2f); }

        /// <summary>Poziom pojedynku: clamp(s / 250 x trudnosc x (1 + krok x runda), 0, 1).</summary>
        internal static float Level(int s, float dm, int round, Settings c)
        {
            if (round < 0) round = 0;
            if (round > 3) round = 3;
            return Clamp(s / 250f * dm * (1f + RoundStep(c) * round), 0f, 1f);
        }

        /// <summary>Profil (jedna funkcja dla postfiksu i tabeli startowej). lvl ponizej Floor - t = 0 (wartosci rowne RBM przy 100).</summary>
        internal static Shape ShapeOf(float lvl, bool shield, bool mounted, float sFac)
        {
            float t = Clamp((lvl - Floor) / (1f - Floor), 0f, 1f);
            var r = new Shape();
            if (!shield)
            {
                r.AoD = 0.15f + 0.30f * t;
                r.AoP = (0.20f + 0.40f * t) * sFac;
                r.Parry = Math.Min(0.85f, Clamp(lvl, 0.1f, 0.6f) + 0.25f * t);
                r.Feint = (0.18f + 0.42f * t) * sFac;
                r.Incorrect = Math.Min(0.75f, Clamp(0.8f * lvl, 0.05f, 0.5f) + 0.25f * t) * sFac;
            }
            else
            {
                r.AoD = 0.12f + 0.24f * t;
                r.AoP = Math.Min(0.40f, (0.12f + 0.28f * t) * sFac);
                r.Parry = Clamp(0.5f * lvl, 0f, 0.6f) + 0.20f * t;
                r.Feint = Clamp(lvl * 0.46f, 0f, 0.35f) * sFac;                         // z tarcza: wyczucie bloku jak RBM
                r.Incorrect = (Clamp(0.3f * lvl, 0f, 0.2f) + 0.15f * t) * sFac;
            }
            if (mounted) r.AoD = Math.Max(r.AoD, 0.30f);                                // RBM: konno 0.3
            r.DoA = (0.50f - 0.15f * t) * sFac;
            r.Chamber = 0.15f * t;
            r.Kick = 0.22f + 0.18f * t;
            r.Hold = 1.0f - 0.88f * t;
            r.HoldVar = 1.0f - 0.5f * t;
            r.MaxTime = lvl * sFac;
            return r;
        }

        // ------------------------------------------------------------ umiejetnosc

        /// <summary>Umiejetnosc walki wrecz jak gra (AgentStatCalculateModel.GetMeleeSkill) - te sama bierze RBM.</summary>
        internal static SkillObject MeleeSkill(WeaponComponentData equippedItem, WeaponComponentData secondaryItem)
        {
            if (equippedItem == null) return DefaultSkills.Athletics;
            var rs = equippedItem.RelevantSkill;
            if (rs == DefaultSkills.OneHanded || rs == DefaultSkills.Polearm) return rs;
            if (rs != DefaultSkills.TwoHanded) return DefaultSkills.OneHanded;
            return secondaryItem == null ? DefaultSkills.TwoHanded : DefaultSkills.OneHanded;
        }

        /// <summary>Umiejetnosc pojedynku: bron w reku; bohater - co najmniej udzial najlepszej z trzech broni bialych.</summary>
        internal static int DuelSkill(Agent a, AgentStatCalculateModel model, WeaponComponentData eq, WeaponComponentData sec, Settings c, out int held, out int best)
        {
            held = 0; best = 0;
            if (a == null || model == null) return 0;
            held = model.GetEffectiveSkill(a, MeleeSkill(eq, sec));
            int s = held;
            var ch = a.Character;
            if (ch != null && ch.IsHero)
            {
                int oh = model.GetEffectiveSkill(a, DefaultSkills.OneHanded);
                int th = model.GetEffectiveSkill(a, DefaultSkills.TwoHanded);
                int pa = model.GetEffectiveSkill(a, DefaultSkills.Polearm);
                best = Math.Max(oh, Math.Max(th, pa));
                int floor = (int)(Share(c) * best);
                if (floor > s) s = floor;
            }
            return s;
        }

        /// <summary>To samo z broni w rekach agenta (haki postury, BattleWind, lista turnieju) - glowny watek albo hak ciosu.</summary>
        internal static int DuelSkill(Agent a, Settings c, out int held, out int best)
        {
            held = 0; best = 0;
            if (a == null) return 0;
            var mgm = MissionGameModels.Current;
            var model = mgm != null ? mgm.AgentStatCalculateModel : null;
            if (model == null) return 0;
            WeaponComponentData eq = null, sec = null;
            var pi = a.GetPrimaryWieldedItemIndex();
            if (pi >= EquipmentIndex.WeaponItemBeginSlot && pi < EquipmentIndex.NumAllWeaponSlots)
            {
                var w = a.Equipment[pi];
                if (!w.IsEmpty) eq = w.CurrentUsageItem;
            }
            var oi = a.GetOffhandWieldedItemIndex();
            if (oi >= EquipmentIndex.WeaponItemBeginSlot && oi < EquipmentIndex.NumAllWeaponSlots)
            {
                var w = a.Equipment[oi];
                if (!w.IsEmpty) sec = w.CurrentUsageItem;
            }
            return DuelSkill(a, model, eq, sec, c, out held, out best);
        }

        /// <summary>Poziom pojedynku agenta z broni w rekach (DuelCraft: zryw od 0.40 / 0.56).</summary>
        internal static float LevelOf(Agent a, Settings c)
        {
            int held, best;
            int s = DuelSkill(a, c, out held, out best);
            return Level(s, VeteranAi.DiffMulNow(), BattleSense.Round, c);
        }

        // ------------------------------------------------------------ postfiks (z VeteranAi.Postfix)

        internal static void Apply(Agent agent, AgentDrivenProperties p, WeaponComponentData equippedItem, WeaponComponentData secondaryItem,
                                   AgentStatCalculateModel model, float dm, float sFac, Settings c)
        {
            if (!agent.IsHuman || agent.IsMainAgent || !agent.IsAIControlled) return;
            if (equippedItem == null) return;              // bez broni w reku (widzowie trybun, przed wyciagnieciem broni) - RBM bez zmian, bez migawki
            var ch = agent.Character;
            if (ch == null || Undead.Character(ch)) return;
            var m = agent.Mission;

            int held, best;
            int s = DuelSkill(agent, model, equippedItem, secondaryItem, c, out held, out best);
            int round = BattleSense.Round;
            float lvl = Level(s, dm, round, c);
            int id = agent.Index;
            if (lvl < Floor)
            {
                Note(m, agent, equippedItem, held, best, s, round, lvl, null, 0);
                return;
            }

            int burst = c.DuelPressureBursts && m != null ? DuelCraft.BurstOf(id, m.CurrentTime) : DuelCraft.None;
            Write(agent, p, lvl, sFac, burst);
            Note(m, agent, equippedItem, held, best, s, round, lvl, p, burst);
        }

        /// <summary>m113-walka2 faza 2: bohater AI w bitwie w polu (klucz DuelistsInBattles, wolane z VeteranAi.Postfix po bramie bitwy i galezi RBM).
        /// Ten sam profil co w pojedynku przy rundzie 0; ponizej poziomu 0.40 (umiejetnosc 100 na Realistic) false - bohater idzie regula weteranow
        /// (tier z umiejetnosci: HeroTierFromSkill). best = najlepsza z trzech broni bialych (VeteranAi bierze ja do tieru bez ponownego liczenia).
        /// Zryw tylko przy DuelistBurstsInBattles. Tylko odczyt statykow; liczniki pod RoutHold.Gate (FieldDuel).</summary>
        internal static bool ApplyField(Agent agent, AgentDrivenProperties p, WeaponComponentData equippedItem, WeaponComponentData secondaryItem,
                                        AgentStatCalculateModel model, float dm, float sFac, Settings c, out int best)
        {
            best = -1;
            if (equippedItem == null) return false;        // bez broni w reku - regula weteranow (jak dotad)
            int held;
            int s = DuelSkill(agent, model, equippedItem, secondaryItem, c, out held, out best);
            float lvl = Level(s, dm, 0, c);
            if (lvl < Floor) return false;
            var m = agent.Mission;
            int burst = c.DuelistBurstsInBattles && m != null ? DuelCraft.BurstOf(agent.Index, m.CurrentTime) : DuelCraft.None;
            float aod, doa;
            Write(agent, p, lvl, sFac, burst, out aod, out doa);
            FieldDuel.NoteHero(m, agent.Index, s, lvl, aod, doa, burst);
            return true;
        }

        private static void Write(Agent agent, AgentDrivenProperties p, float lvl, float sFac, int burst)
        {
            float aod, doa;
            Write(agent, p, lvl, sFac, burst, out aod, out doa);
        }

        /// <summary>Profil Shape w polach RBM / gry i nakladka zrywu (wspolne dla pojedynku i bitwy). aod / doa - wartosci profilu przed zrywem.</summary>
        private static void Write(Agent agent, AgentDrivenProperties p, float lvl, float sFac, int burst, out float aod, out float doa)
        {
            bool shield = VeteranAi.HasShield(agent);
            var sh = ShapeOf(lvl, shield, agent.HasMount, sFac);
            p.AIAttackOnDecideChance = sh.AoD;
            p.AIDecideOnAttackChance = sh.DoA;
            p.AIAttackOnParryChance = sh.AoP;
            p.AIParryOnDecideAbility = sh.Parry;
            p.AIDecideOnRealizeEnemyBlockingAttackAbility = sh.Feint;
            p.AIRealizeBlockingFromIncorrectSideAbility = sh.Incorrect;
            // po recenzji: kopniecie i chamber nigdy ponizej wartosci gry na wejsciu (RBM ich nie pisze; gra: kopniecie -0.1 + min(n, 0.4) = 0.30
            // od umiejetnosci 125, chamber (n - 0.15) x 0.1) - profil ma byc smielszy, nie ostrozniejszy
            p.AiTryChamberAttackOnDecide = Math.Max(p.AiTryChamberAttackOnDecide, sh.Chamber);
            p.AiKick = Math.Max(p.AiKick, sh.Kick);
            p.AiAttackCalculationMaxTimeFactor = sh.MaxTime;
            // po recenzji: blok wedlug poziomu pojedynku - wzor RBM clamp(2 x poziom, 0.3, 1) (AgentAi.cs:95) z L zamiast umiejetnosci broni w reku;
            // nigdy ponizej RBM (rycerz 180 z obca bronia blokuje jak rycerz, nie jak nowicjusz)
            p.AIBlockOnDecideAbility = Math.Max(p.AIBlockOnDecideAbility, Clamp(2f * lvl, 0.3f, 1f));
            bool melee = !agent.IsRangedCached;
            if (melee)
            {
                p.AIHoldingReadyMaxDuration = sh.Hold;
                p.AIHoldingReadyVariationPercentage = sh.HoldVar;
            }

            // pola zostawione z gry (RBM ich nie pisze) - wzory gry (AgentStatCalculateModel.SetAiRelatedProperties) z poziomem L
            float l2 = lvl * lvl;
            p.AiRandomizedDefendDirectionChance = 1f - l2 * lvl;
            p.AIEstimateStunDurationPrecision = 1f - Lerp(0.2f, 1f, lvl);
            p.AiAttackOnParryTiming = -0.2f + 0.3f * lvl;
            p.AIParryOnAttackAbility = lvl;
            p.AIParryOnAttackingContinueAbility = Lerp(0.5f, 0.95f, lvl);
            p.AiDecideOnAttackWhenReceiveHitTiming = -0.25f * (1f - lvl);
            p.AiDecideOnAttackContinueAction = -0.5f * (1f - lvl);
            p.AiDecideOnAttackingContinue = 0.1f * lvl;
            p.AISetNoAttackTimerAfterBeingHitAbility = Lerp(0.33f, 1f, lvl);
            p.AISetNoAttackTimerAfterBeingParriedAbility = Lerp(0.2f, 1f, l2);
            p.AISetNoDefendTimerAfterHittingAbility = Lerp(0.1f, 0.99f, l2);
            p.AISetNoDefendTimerAfterParryingAbility = Lerp(0.15f, 1f, l2);
            p.AiParryDecisionChangeValue = 0.05f + 0.7f * lvl;

            // zryw (DuelCraft) - rodzaj odczytany przez wolajacego z tablic statycznych
            aod = p.AIAttackOnDecideChance;
            doa = p.AIDecideOnAttackChance;
            if (burst == DuelCraft.Opening)
            {
                p.AIAttackOnDecideChance = 0.90f;
                p.AIDecideOnAttackChance = 0.10f;
                if (melee) { p.AIHoldingReadyMaxDuration = 0f; p.AIHoldingReadyVariationPercentage = 0f; }
            }
            else if (burst == DuelCraft.Pressed)
            {
                p.AIAttackOnDecideChance = Math.Max(p.AIAttackOnDecideChance, 0.60f);
                p.AIDecideOnAttackChance = p.AIDecideOnAttackChance * 0.6f;
                p.AIAttackOnParryChance = Math.Max(p.AIAttackOnParryChance, 0.80f);
                p.AiKick = Math.Max(p.AiKick, 0.45f);
                p.AiTryChamberAttackOnDecide = Math.Max(p.AiTryChamberAttackOnDecide, 0.20f);
                if (melee) p.AIHoldingReadyMaxDuration = 0.05f;
            }
        }

        // ------------------------------------------------------------ linia DU (migawki, wypis w ticku BattleSense)

        private struct Snap
        {
            public Agent A;
            public WeaponComponentData Eq;      // nazwe broni bierze Flush (glowny watek) - postfiks nie wola silnika
            public int Held, Best, S, Round, Burst;
            public float Lvl, AoD, DoA, AoP, Par, Feint, Ch, Kick, Hold, Block;
            public bool Rbm;
        }

        internal const int MaxSnaps = 40;
        private static readonly Snap[] _snaps = new Snap[MaxSnaps];
        private static int _snapCount, _snapWritten;
        private static readonly HashSet<int> _snapKeys = new HashSet<int>();
        private static readonly HashSet<int>[] _bands = { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
        private static int _calls, _profiled;

        /// <summary>Zerowanie licznikow misji (DuelGuard.Begin, pod RoutHold.Gate).</summary>
        internal static void ResetLocked()
        {
            for (int i = 0; i < _snaps.Length; i++) _snaps[i] = default(Snap);
            _snapCount = _snapWritten = 0;
            _snapKeys.Clear();
            for (int i = 0; i < _bands.Length; i++) _bands[i].Clear();
            _calls = _profiled = 0;
        }

        private static void Note(Mission m, Agent a, WeaponComponentData eq, int held, int best, int s, int round, float lvl, AgentDrivenProperties p, int burst)
        {
            int id = a.Index;
            lock (RoutHold.Gate)
            {
                _calls++;
                if (p != null) _profiled++;
                _bands[s < 100 ? 0 : (s < 180 ? 1 : 2)].Add(id);
                if (_snapCount >= MaxSnaps) return;
                int key = id * 4 + (round < 0 ? 0 : (round > 3 ? 3 : round));
                if (!_snapKeys.Add(key)) return;
                // po recenzji: pod blokada tylko kopie i liczniki - bez a.WieldedWeapon (wywolanie silnika) i Enum.ToString (tick asynchroniczny)
                var sn = new Snap { A = a, Eq = eq, Held = held, Best = best, S = s, Round = round, Lvl = lvl, Burst = burst, Rbm = p == null };
                if (p != null)
                {
                    sn.AoD = p.AIAttackOnDecideChance; sn.DoA = p.AIDecideOnAttackChance; sn.AoP = p.AIAttackOnParryChance;
                    sn.Par = p.AIParryOnDecideAbility; sn.Feint = p.AIDecideOnRealizeEnemyBlockingAttackAbility; sn.Ch = p.AiTryChamberAttackOnDecide;
                    sn.Kick = p.AiKick; sn.Hold = p.AIHoldingReadyMaxDuration; sn.Block = p.AIBlockOnDecideAbility;
                }
                _snaps[_snapCount++] = sn;
            }
        }

        internal static void BandsLocked(StringBuilder sb)
        {
            sb.Append("AI wg umiejetnosci <100 ").Append(_bands[0].Count).Append(", 100-179 ").Append(_bands[1].Count).Append(", 180+ ").Append(_bands[2].Count)
              .Append(" (wywolan ").Append(_calls).Append(", z profilem ").Append(_profiled).Append(')');
        }

        /// <summary>Glowny watek (BattleSense.OnMissionTick): nowe migawki do logu - "DU agent ...". Postura z RBM (AgentStances) przez DuelGuard.</summary>
        internal static void Flush()
        {
            while (true)
            {
                Snap sn;
                lock (RoutHold.Gate)
                {
                    if (_snapWritten >= _snapCount) return;
                    sn = _snaps[_snapWritten++];
                }
                var sb = new StringBuilder(220);
                var a = sn.A;
                string name = "?";
                try { name = a != null && a.Character != null ? a.Character.StringId : "?"; } catch { }
                string weapon = sn.Eq != null ? sn.Eq.WeaponClass.ToString() : "-";
                try
                {
                    if (a != null && a.IsActive())
                    {
                        var ww = a.WieldedWeapon;
                        if (!ww.IsEmpty && ww.Item != null) weapon = ww.Item.StringId;
                    }
                }
                catch { }
                sb.Append("DU agent ").Append(a != null ? a.Index : -1).Append(' ').Append(name).Append(' ').Append(weapon).Append(' ').Append(sn.Held)
                  .Append(" (najlepsza ").Append(sn.Best).Append(", s ").Append(sn.S).Append(") runda ").Append(sn.Round)
                  .Append(" poziom ").Append(sn.Lvl.ToString("0.00", Inv)).Append(" -> ");
                if (sn.Rbm) sb.Append("RBM bez zmian (ponizej ").Append(Floor.ToString("0.00", Inv)).Append(')');
                else
                    sb.Append("AoD ").Append(F(sn.AoD)).Append(" DoA ").Append(F(sn.DoA)).Append(" AoP ").Append(F(sn.AoP)).Append(" Par ").Append(F(sn.Par))
                      .Append(" Feint ").Append(F(sn.Feint)).Append(" Ch ").Append(F(sn.Ch)).Append(" Kick ").Append(F(sn.Kick)).Append(" Hold ").Append(F(sn.Hold))
                      .Append(" Block ").Append(F(sn.Block))
                      .Append(sn.Burst == DuelCraft.Opening ? " (zryw: otwarcie)" : (sn.Burst == DuelCraft.Pressed ? " (zryw: nacisk)" : ""));
                sb.Append(" | postura ");
                DuelGuard.AppendPosture(sb, a);
                Log.Info(sb.ToString());
            }
        }

        private static string F(float v) { return v.ToString("0.00", Inv); }

        // ------------------------------------------------------------ tabela startowa

        /// <summary>Linia "DuelAi tabela" (start sesji, po McmSettings.Apply): Shape przy umiejetnosci 60..250, bez tarczy i z tarcza, runda 0 i 3.</summary>
        internal static void SessionStart()
        {
            var c = Settings.Current;
            if (c == null) return;
            float dm = VeteranAi.DiffMulNow();
            var sb = new StringBuilder(2400);
            sb.Append("DuelAi tabela (m111-walka): Duel AI ").Append(c.DuelAiEnabled ? "TAK" : "NIE").Append(" (latka ").Append(VeteranAi.Patched ? "TAK" : "NIE")
              .Append("), trudnosc x").Append(dm.ToString("0.00", Inv)).Append(", udzial najlepszej ").Append(Share(c).ToString("0.00", Inv))
              .Append(", krok rundy ").Append(RoundStep(c).ToString("0.00", Inv)).Append(", od poziomu ").Append(Floor.ToString("0.00", Inv))
              .Append(" (ponizej RBM); pola AoD/DoA/AoP/Par/Feint/Inc/Ch/Kick/Hold przy pelnej staminie");
            int[] skills = { 60, 100, 140, 180, 210, 250 };
            for (int sh = 0; sh < 2; sh++)
                for (int rd = 0; rd <= 3; rd += 3)
                {
                    sb.Append(" | ").Append(sh == 0 ? "bez tarczy" : "z tarcza").Append(", runda ").Append(rd).Append(':');
                    for (int i = 0; i < skills.Length; i++)
                    {
                        float lvl = Level(skills[i], dm, rd, c);
                        sb.Append(' ').Append(skills[i]).Append("->L").Append(F(lvl));
                        if (lvl < Floor) { sb.Append(" RBM;"); continue; }
                        var s = ShapeOf(lvl, sh == 1, false, 1f);
                        sb.Append(' ').Append(F(s.AoD)).Append('/').Append(F(s.DoA)).Append('/').Append(F(s.AoP)).Append('/').Append(F(s.Parry))
                          .Append('/').Append(F(s.Feint)).Append('/').Append(F(s.Incorrect)).Append('/').Append(F(s.Chamber)).Append('/').Append(F(s.Kick))
                          .Append('/').Append(F(s.Hold)).Append(';');
                    }
                }
            sb.Append(" | zrywy ").Append(c.DuelPressureBursts ? "TAK" : "NIE").Append(" (otwarcie ").Append(F(DuelCraft.OpeningSeconds)).Append(" s od L")
              .Append(F(Floor)).Append(", nacisk ").Append(F(DuelCraft.PressedSeconds)).Append(" s od L").Append(F(PressedFrom)).Append(", przerwa ")
              .Append(F(DuelCraft.Cooldown)).Append(" s, tylko nacisk) | postura: Duel Fair Guard ").Append(c.DuelFairGuard ? "TAK" : "NIE")
              .Append(BattleWind.PosturePatched ? "" : " (latka InitializePosture NIEWPIETA - bez efektu)").Append(" (END najwyzej x")
              .Append(F(BattleWind.DuelCap(c))).Append(", umiejetnosc 24 x").Append(F(BattleWind.SkillGuard(24))).Append(", 100 x").Append(F(BattleWind.SkillGuard(100)))
              .Append(", 180 x").Append(F(BattleWind.SkillGuard(180))).Append(", 250 x").Append(F(BattleWind.SkillGuard(250))).Append("), roznica umiejetnosci waga ")
              .Append(F(DuelGuard.Weight(c))).Append(", Duel Perfect Defence ").Append(c.DuelPerfectDefence ? "TAK" : "NIE").Append(", bez zwolnienia ")
              .Append(c.DuelNoSlowMotion ? "TAK" : "NIE").Append(", bez upadku Artema ").Append(c.DuelNoInjuredCollapse ? "TAK" : "NIE")
              .Append(" | haki: ").Append(DuelGuard.HookState()).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
