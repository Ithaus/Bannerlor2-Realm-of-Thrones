using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// m110-stan krok 2 (Jeff 23.10 ok. 15:50 "tak" na pytanie "czy dla sprzetu, ktory nosisz ty i towarzysze, ochrona ma sie liczyc z dokladnego HP"; 15:10:
    /// "3 x zbroja Nocnej Strazy, ktore sa uszkodzone, ale jedna ma hp 41%, druga 56%, trzecia 78% - kazda daje inny pancerz"). OCHRONA Z DOKLADNEGO HP:
    /// sprzet NOSZONY w bitwie kampanii przez bohaterow druzyny gracza dziala wedlug dokladnej wytrzymalosci z ksiegi (gracz - arm_condition, towarzysze -
    /// HeroBook) zamiast wedlug stanu. Jedna zasada dla pancerza, obrazen broni i pancerza tarczy: krzywa ConditionScaling (StatFactor, ConditionPenaltyMax /
    /// Exponent) z dokladnego ulamka; pola dodawane wprost (szybkosc, wytrzymalosc tarczy, predkosc pocisku) zostaja wedlug stanu. Stos w sakwach, kupcy i
    /// nazwy - wedlug stanu (GearFourStates).
    /// Droga: postfiks Agent.InitializeSpawnEquipment (ten sam cel latkuje RBM - CampaignChanges.InitializeSpawnEquipmentPatch): w klonie sprzetu agenta
    /// (SpawnEquipment - Mission.SpawnAgent klonuje zawsze, RBM klonuje drugi raz dla bohaterow) modyfikator sztuki zamieniony na KOPIE (MemberwiseClone
    /// oryginalu - ten sam StringId, Id, nazwa i pola; sztuka sprawna bez modyfikatora - kopia neutralna) zapisana w rejestrze kopia -> (oryginal, d). Kopia
    /// jest na miejscu przed InitializeMissionEquipment (bron w MissionWeapon) i BuildAgent (statystyki agenta, RBMAI); uprzaz konia - z gniazda 11 (gracz:
    /// ten sam obiekt; towarzysz - klon RBM, wiec kopia przechodzi na konia w jego InitializeSpawnEquipment). Latki ConditionScaling na ItemModifier.ModifyArmor /
    /// ModifyDamage licza dla kopii wynik z d (Exact); GearState.Of mapuje kopie na oryginal (nazwy, ceny, stan).
    /// Rejestry - migawki podmieniane przy zapisie (zapis tylko przy wejsciu agentow, odczyt bez blokady), czyszczone na koncu misji i przy zmianie misji;
    /// kontrola przeciekow na koncu misji (kopia w BattleEquipment bohatera wraca na oryginal i jest liczona). Klucz WornExactHpProtection (wymaga GearFourStates
    /// i ConditionScalesStats); NIE - zadnych kopii (jak 6f3e0314). Hak autotestu: plik CrashScribe/armoury-hp.flag przy LogGate 1/2. Bledy liczone (pierwsze 3
    /// i co 50), bez globalnego wylacznika.
    /// </summary>
    internal static class HeroHp
    {
        private const string P = "HP sprzetu (m110): ";
        internal const string AutoFlagName = "armoury-hp.flag";
        private const int LinesPerBattle = 20;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        /// <summary>Gniazda sprzetu noszonego w bitwie (bez sztandaru 4 i konia 10) - kolejnosc haka testu.</summary>
        private static readonly int[] Slots = { 5, 6, 7, 8, 9, 11, 0, 1, 2, 3 };
        /// <summary>Hak testu: sztuczna wytrzymalosc gniazd gracza po kolei (Slots) - stany uszkodzony, sprawny, zmasakrowany, wrak i srodki stanow.</summary>
        private static readonly float[] TestD = { 41f, 56f, 78f, 92f, 25f, 5f, 63f, 88f };
        private static readonly string[] KindName = { "glowa", "tulow", "rece", "nogi", "kon", "ciecie", "pchniecie", "pocisk", "tarcza" };
        private const int KHead = 0, KBody = 1, KArm = 2, KLeg = 3, KHorse = 4, KSwing = 5, KThrust = 6, KMissile = 7, KShield = 8;

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }

        /// <summary>Kopia modyfikatora w klonie sprzetu agenta.</summary>
        internal sealed class CopyInfo
        {
            public ItemModifier Orig;      // modyfikator, ktory kopia udaje (null - sztuka sprawna bez modyfikatora: kopia neutralna)
            public ItemModifier HeroMod;   // modyfikator sztuki u bohatera (powrot przy przecieku; hak testu - inny niz Orig)
            public float D;                // dokladna wytrzymalosc w procentach puli (po przycieciu do przedzialu stanu)
            public int State, Slot;
            public bool Clipped;
            public Hero Hero;
        }

        /// <summary>Agent bohatera druzyny gracza w tej misji.</summary>
        internal sealed class AgentInfo
        {
            public Hero Hero;
            public bool Player;
            public float[] Hits;           // ksiega ciosow tej misji (towarzysz przy CompanionGearWear) - pisze FieldCraft, koniec misji przenosi do HeroBook
            public bool[] Match;           // gniazda zgodne z BattleEquipment bohatera (tylko one sie zuzywaja)
            public readonly List<KeyValuePair<ItemModifier, CopyInfo>> Copies = new List<KeyValuePair<ItemModifier, CopyInfo>>();
        }

        private sealed class Tally
        {
            public int N;
            public bool Test, Done;
            public int Agents, WithCopies, MismHeroes, MismSlots, Copies, Neutral, Clipped, Horse, Lines, Stumbles, TestLines;
        }

        private static readonly RefEq<ItemModifier> ModCmp = new RefEq<ItemModifier>();
        private static readonly RefEq<Agent> AgentCmp = new RefEq<Agent>();
        private static volatile Dictionary<ItemModifier, CopyInfo> _copies = new Dictionary<ItemModifier, CopyInfo>(ModCmp);
        private static volatile Dictionary<Agent, AgentInfo> _agents = new Dictionary<Agent, AgentInfo>(AgentCmp);
        private static readonly List<Hero> _heroes = new List<Hero>();   // bohaterowie z agentem w tej misji (kontrola przeciekow)
        private static WeakReference _mref;
        private static int _battle = -1;          // -1 nie sprawdzone, 0 misja poza bitwa, 1 bitwa kampanii
        private static bool _test;
        private static Tally _t;
        private static int _battleN, _stumbles;
        // uprzaz konia towarzysza: kopia z gniazda 11 jezdzca czeka na InitializeSpawnEquipment konia (Mission.SpawnAgent: jezdziec, potem kon z tego samego sprzetu)
        private static ItemObject _pendItem;
        private static ItemModifier _pendMod, _pendCopy;

        private static MethodInfo _clone, _setName, _setPrice, _setQuality;

        internal static void Reset()
        {
            _mref = null; _battle = -1; _test = false; _t = null; _battleN = 0;
            Clear();
        }

        private static void Clear()
        {
            _copies = new Dictionary<ItemModifier, CopyInfo>(ModCmp);
            _agents = new Dictionary<Agent, AgentInfo>(AgentCmp);
            _heroes.Clear();
            _pendItem = null; _pendMod = null; _pendCopy = null;
        }

        // ------------------------------------------------------------ odczyt (latki ConditionScaling, GearState, panel)
        /// <summary>Kopia z rejestru: oryginal i dokladna wytrzymalosc. Pusty rejestr - szybkie wyjscie.</summary>
        internal static bool TryCopy(ItemModifier m, out ItemModifier orig, out float d)
        {
            orig = null; d = 100f;
            var reg = _copies;
            CopyInfo ci;
            if (m == null || reg.Count == 0 || !reg.TryGetValue(m, out ci)) return false;
            orig = ci.Orig; d = ci.D;
            return true;
        }

        /// <summary>Oryginal kopii (GearState.Of: nazwa, cena i stan kopii jak oryginalu); null - kopia neutralna (sprawna).</summary>
        internal static bool CopyOrig(ItemModifier m, out ItemModifier orig)
        {
            float d;
            return TryCopy(m, out orig, out d);
        }

        /// <summary>Wartosc sztuki z dokladnego HP d (procent) - ta sama krzywa co stan (ConditionScaling.StatFactor). Oryginal brak albo dobra jakosc: wynik gry
        /// (RBM) razy krzywa; oryginal zly (stan): wartosc bazowa razy krzywa (jak ArmorPatch dla stanu). Panel "Armoury Stats" liczy ta sama funkcja.</summary>
        internal static int Exact(int baseVal, int result, ItemModifier orig, float d)
        {
            float sf = ConditionScaling.StatFactor(d / 100f);
            if (orig == null || ConditionScaling.ConditionOf(orig) >= 0.999f) return Math.Max(1, (int)Math.Floor(result * sf));
            return Math.Max(1, (int)Math.Floor(baseVal * sf));
        }

        /// <summary>d przyciete do przedzialu stanu widocznego w nazwie (sprawny (80,100], uszkodzony (40,80], zmasakrowany [10,40], wrak [0.1,10)) - ochrona
        /// nigdy nie przeczy nazwie (wpis ksiegi starszy niz stan, obnizka RBM).</summary>
        internal static float Clip(float d, int st, out bool clipped)
        {
            float lo, hi;
            switch (st)
            {
                case GearState.Sound: lo = 80.01f; hi = 100f; break;
                case GearState.Damaged: lo = 40.01f; hi = 80f; break;
                case GearState.Mangled: lo = 10f; hi = 40f; break;
                default: lo = 0.1f; hi = 9.99f; break;
            }
            float r = d < lo ? lo : (d > hi ? hi : d);
            clipped = Math.Abs(r - d) > 0.001f;
            return r;
        }

        private static bool SameAsState(float d, int st)
        {
            return st == GearState.Sound ? d >= 99.95f : Math.Abs(d - GearState.Mid[st]) < 0.05f;
        }

        // ------------------------------------------------------------ misja
        [HarmonyPatch(typeof(Agent), "InitializeSpawnEquipment")]
        internal static class SpawnPatch
        {
            private static void Postfix(Agent __instance)
            {
                try { Spawned(__instance); } catch (Exception e) { Stumble("HeroHp.Spawn", e); }
            }
        }

        internal sealed class Logic : MissionBehavior
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnAgentBuild(Agent agent, Banner banner)
            {
                try { Built(agent); } catch (Exception e) { Stumble("HeroHp.OnAgentBuild", e); }
            }

            protected override void OnEndMission()
            {
                try { EndMission(Mission); } catch (Exception e) { Stumble("HeroHp.OnEndMission", e); }
                base.OnEndMission();
            }
        }

        private static object Target(WeakReference w)
        {
            try { return w != null ? w.Target : null; } catch { return null; }
        }

        /// <summary>Misja to bitwa kampanii z bitwa gracza na mapie (jak FieldCraft.InBattle) - liczone raz na misje; zmiana misji czysci rejestry.</summary>
        private static bool Battle(Mission m)
        {
            if (m == null) return false;
            if (!ReferenceEquals(Target(_mref), m))
            {
                _mref = new WeakReference(m);
                _battle = -1; _test = false; _t = null;
                Clear();
            }
            if (_battle >= 0) return _battle == 1;
            bool b;
            try { b = Campaign.Current != null && MapEvent.PlayerMapEvent != null && m.CombatType == Mission.MissionCombatType.Combat; }
            catch { return false; }
            _battle = b ? 1 : 0;
            if (b)
            {
                _test = TestWanted();
                _t = new Tally { N = ++_battleN, Test = _test };
            }
            return b;
        }

        private static bool ProtOn(Settings s)
        {
            return s != null && s.WornExactHpProtection && s.ConditionScalesStats && GearState.On;
        }

        /// <summary>Postfiks Agent.InitializeSpawnEquipment: kopie modyfikatorow sztuk bohatera druzyny gracza z dokladnym HP (klon sprzetu agenta), linia bitwy.</summary>
        private static void Spawned(Agent agent)
        {
            var s = Settings.Current;
            if (agent == null || !ProtOn(s) || Campaign.Current == null) return;
            var m = agent.Mission ?? Mission.Current;
            if (!Battle(m)) return;
            var eq = agent.SpawnEquipment;
            if (eq == null) return;
            if (agent.Character == null) { MountTakes(eq); return; }   // kon (CreateHorseAgentFromRosterElements - bez postaci)
            _pendItem = null; _pendMod = null; _pendCopy = null;
            var co = agent.Character as CharacterObject;
            if (co == null || !co.IsHero) return;
            var hero = co.HeroObject;
            if (hero == null || hero.PartyBelongedTo != MobileParty.MainParty) return;
            var heq = hero.BattleEquipment;
            var dead = Campaign.Current.DeadBattleEquipment;
            if (heq == null || ReferenceEquals(eq, heq) || ReferenceEquals(heq, dead) || ReferenceEquals(eq, dead)) return;
            var t = _t;
            if (t == null) return;
            bool player = hero == Hero.MainHero;
            bool test = player && _test;
            var beh = player ? ArmouryBehavior.Instance : null;
            if (!_heroes.Contains(hero)) _heroes.Add(hero);
            t.Agents++;
            var ai = new AgentInfo { Hero = hero, Player = player, Match = new bool[12] };
            int mism = 0;
            var put = new List<KeyValuePair<int, ItemModifier>>();
            for (int k = 0; k < Slots.Length; k++)
            {
                int slot = Slots[k];
                try
                {
                    var el = eq[slot];
                    var hel = heq[slot];
                    if (el.Item != hel.Item || !ReferenceEquals(el.ItemModifier, hel.ItemModifier)) { mism++; continue; }   // lord RBM, oblezenie bez konia, inny stroj
                    ai.Match[slot] = true;
                    if (el.Item == null || ArmouryBehavior.NoWear(el.Item) || !GearState.Handles(el.Item)) continue;
                    var cur = el.ItemModifier;
                    if (cur != null && _copies.ContainsKey(cur)) continue;   // juz kopia (drugie wejscie tego samego sprzetu)
                    int st = GearState.Of(cur);
                    if (st < 0) continue;
                    ItemModifier orig = cur;
                    float d;
                    if (test)
                    {
                        // hak testu: sztuczna wytrzymalosc i kanoniczny modyfikator jej stanu (sprawny - kopia neutralna albo dobrej jakosci gracza)
                        d = TestD[k % TestD.Length];
                        st = GearState.OfCondition(d);
                        orig = st == GearState.Sound ? (cur != null && GearState.Of(cur) == GearState.Sound ? cur : null) : GearState.ModFor(el.Item, st);
                        if (st != GearState.Sound && orig == null) continue;
                    }
                    else if (player)
                    {
                        if (beh == null || !beh.WornExactD(slot, out d)) continue;
                    }
                    else continue;
                    bool clipped;
                    d = Clip(d, st, out clipped);
                    if (!test && SameAsState(d, st)) continue;   // wynik jak ze stanu - bez kopii
                    var copy = MakeCopy(orig);
                    if (copy == null) continue;
                    var ci = new CopyInfo { Orig = orig, HeroMod = cur, D = d, State = st, Slot = slot, Clipped = clipped, Hero = hero };
                    ai.Copies.Add(new KeyValuePair<ItemModifier, CopyInfo>(copy, ci));
                    put.Add(new KeyValuePair<int, ItemModifier>(slot, copy));
                }
                catch (Exception e) { Stumble("HeroHp.Spawn(" + slot + ")", e); }
            }
            // rejestr najpierw (odczyty na innych watkach widza kopie od razu z jej d), potem kopie do klonu sprzetu agenta
            if (ai.Copies.Count > 0)
            {
                var nd = new Dictionary<ItemModifier, CopyInfo>(_copies, ModCmp);
                foreach (var kv in ai.Copies) nd[kv.Key] = kv.Value;
                _copies = nd;
            }
            var na = new Dictionary<Agent, AgentInfo>(_agents, AgentCmp);
            na[agent] = ai;
            _agents = na;
            foreach (var kv in put)
            {
                try
                {
                    var el = eq[kv.Key];
                    var hm = el.ItemModifier;
                    el.SetModifier(kv.Value);
                    eq[kv.Key] = el;
                    if (kv.Key == (int)EquipmentIndex.HorseHarness) { _pendItem = el.Item; _pendMod = hm; _pendCopy = kv.Value; }
                }
                catch (Exception e) { Stumble("HeroHp.Spawn(wstaw " + kv.Key + ")", e); }
            }
            if (mism > 0) { t.MismHeroes++; t.MismSlots += mism; }
            if (ai.Copies.Count > 0) t.WithCopies++;
            foreach (var kv in ai.Copies)
            {
                t.Copies++;
                if (kv.Value.Orig == null) t.Neutral++;
                if (kv.Value.Clipped) t.Clipped++;
            }
            HeroLine(t, ai, eq);
        }

        /// <summary>Kon jezdzca: kopia uprzezy z gniazda 11 (towarzysz - RBM dal jezdzcowi klon, a kon powstaje z sprzetu sprzed klonu; gracz - ten sam obiekt).</summary>
        private static void MountTakes(Equipment eq)
        {
            var copy = _pendCopy;
            var it = _pendItem;
            var hm = _pendMod;
            _pendItem = null; _pendMod = null; _pendCopy = null;
            if (copy == null) return;
            var el = eq[(int)EquipmentIndex.HorseHarness];
            if (el.Item != it) return;
            var t = _t;
            if (ReferenceEquals(el.ItemModifier, copy)) { if (t != null) t.Horse++; return; }
            if (!ReferenceEquals(el.ItemModifier, hm)) return;
            el.SetModifier(copy);
            eq[(int)EquipmentIndex.HorseHarness] = el;
            if (t != null) t.Horse++;
        }

        /// <summary>Kopia oryginalu (MemberwiseClone - ten sam StringId, Id, nazwa i pola); null - kopia neutralna: nazwa "{ITEMNAME}", cena 1, jakosc zwykla, id puste.</summary>
        private static ItemModifier MakeCopy(ItemModifier orig)
        {
            if (orig != null)
            {
                if (_clone == null) _clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
                return _clone != null ? _clone.Invoke(orig, null) as ItemModifier : null;
            }
            if (_setName == null)
            {
                _setName = AccessTools.PropertySetter(typeof(ItemModifier), "Name");
                _setPrice = AccessTools.PropertySetter(typeof(ItemModifier), "PriceMultiplier");
                _setQuality = AccessTools.PropertySetter(typeof(ItemModifier), "ItemQuality");
            }
            if (_setName == null || _setPrice == null || _setQuality == null) return null;
            var m = new ItemModifier();
            m.StringId = "";
            _setName.Invoke(m, new object[] { new TextObject("{ITEMNAME}") });
            _setPrice.Invoke(m, new object[] { 1f });
            _setQuality.Invoke(m, new object[] { ItemQuality.Common });
            return m;
        }

        // ------------------------------------------------------------ wartosci (linie, test)
        /// <summary>Rodzaj wartosci sztuki do linii (pancerz czesci ciala wedlug gniazda, kon, obrazenia broni, pancerz tarczy) i wartosc bazowa; -1 - brak.</summary>
        private static int KindOf(ItemObject it, int slot, out int b)
        {
            b = 0;
            if (it == null) return -1;
            if (it.HasArmorComponent)
            {
                var a = it.ArmorComponent;
                if (it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) { b = a.BodyArmor; return b > 0 ? KHorse : -1; }
                int k;
                switch (slot)
                {
                    case 5: k = KHead; b = a.HeadArmor; break;
                    case 7: k = KLeg; b = a.LegArmor; break;
                    case 8: k = KArm; b = a.ArmArmor; break;
                    default: k = KBody; b = a.BodyArmor; break;
                }
                if (b > 0) return k;
                k = KHead; b = a.HeadArmor;
                if (a.BodyArmor > b) { k = KBody; b = a.BodyArmor; }
                if (a.ArmArmor > b) { k = KArm; b = a.ArmArmor; }
                if (a.LegArmor > b) { k = KLeg; b = a.LegArmor; }
                return b > 0 ? k : -1;
            }
            var w = it.WeaponComponent != null ? it.WeaponComponent.PrimaryWeapon : null;
            if (w == null) return -1;
            if (it.ItemType == ItemObject.ItemTypeEnum.Shield) { b = w.BodyArmor; return b > 0 ? KShield : -1; }
            if (w.SwingDamage > 0) { b = w.SwingDamage; return KSwing; }
            if (w.ThrustDamage > 0) { b = w.ThrustDamage; return KThrust; }
            if (w.MissileDamage > 0) { b = w.MissileDamage; return KMissile; }
            return -1;
        }

        /// <summary>Wartosc przez modyfikator (ten sam lancuch co gra: RBM i latki ConditionScaling na ModifyArmor / ModifyDamage).</summary>
        private static int Val(int kind, int b, ItemModifier m)
        {
            if (b <= 0 || m == null) return b;
            return kind == KSwing || kind == KThrust || kind == KMissile ? m.ModifyDamage(b) : m.ModifyArmor(b);
        }

        private static string OrigKind(ItemModifier orig)
        {
            if (orig == null) return "sprawny";
            return ConditionScaling.ConditionOf(orig) >= 0.999f ? "dobry" : "stan";
        }

        /// <summary>Linia "bitwa N | bohater X" (20 na bitwe, pod flaga wszystkie).</summary>
        private static void HeroLine(Tally t, AgentInfo ai, Equipment eq)
        {
            if (!t.Test && t.Lines >= LinesPerBattle) return;
            t.Lines++;
            var sb = new StringBuilder(P);
            sb.Append("bitwa ").Append(t.N).Append(" | bohater ").Append(ai.Hero.Name).Append(ai.Player ? " (gracz)" : " (towarzysz)").Append(" | ");
            int n = 0;
            foreach (var kv in ai.Copies)
            {
                var ci = kv.Value;
                try
                {
                    var it = eq[ci.Slot].Item;
                    int b;
                    int kind = KindOf(it, ci.Slot, out b);
                    if (kind < 0) continue;
                    int x = Val(kind, b, ci.Orig);
                    int e = Val(kind, b, kv.Key);
                    if (n++ > 0) sb.Append(" ; ");
                    sb.Append("slot ").Append(ci.Slot).Append(' ').Append(it.StringId).Append(' ').Append(KindName[kind]).Append(' ').Append(b).Append(" -> ").Append(e)
                      .Append(" (HP ").Append(ci.D.ToString("0.0", CI)).Append("%, ").Append(GearState.En[ci.State]).Append(", poczatek ").Append(OrigKind(ci.Orig))
                      .Append(ci.Clipped ? ", przyciete" : "").Append("; stan dalby ").Append(x).Append(')');
                }
                catch (Exception ex) { Stumble("HeroHp.HeroLine", ex); }
            }
            if (n == 0) sb.Append("bez kopii");
            var s = Settings.Current;
            bool wear = ai.Player ? s != null && s.WearEnabled : ai.Hits != null;
            sb.Append(" | zuzycie z trafien ").Append(wear ? "TAK" : "NIE").Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ hak testu (pod flaga): pomiar ochrony gracza przez gettery gry
        private static void Built(Agent agent)
        {
            var t = _t;
            if (!_test || t == null || agent == null) return;
            AgentInfo ai;
            if (!_agents.TryGetValue(agent, out ai) || !ai.Player || ai.Copies.Count == 0) return;
            var eq = agent.SpawnEquipment;
            var sb = new StringBuilder(P);
            sb.Append("test - bohater ").Append(ai.Hero.Name).Append(" | ");
            int n = 0;
            foreach (var kv in ai.Copies)
            {
                var ci = kv.Value;
                try
                {
                    var el = eq[ci.Slot];
                    if (!ReferenceEquals(el.ItemModifier, kv.Key)) continue;   // RBM obnizyl juz sztuke (nie przed pierwszym ciosem)
                    var it = el.Item;
                    int b;
                    int kind = KindOf(it, ci.Slot, out b);
                    if (kind < 0) continue;
                    int x = Val(kind, b, ci.Orig);
                    int got = Getter(el, kind);
                    int wz = Exact(b, ci.Orig == null ? b : x, ci.Orig, ci.D);
                    if (n++ > 0) sb.Append(" ; ");
                    sb.Append("slot ").Append(ci.Slot).Append(' ').Append(it.StringId).Append(" HP ").Append(ci.D.ToString("0.0", CI)).Append("% ").Append(GearState.En[ci.State])
                      .Append(": sztuka ").Append(KindName[kind]).Append(" baza ").Append(b).Append(", stan ").Append(x).Append(" -> dokladnie ").Append(got)
                      .Append(" (wzor ").Append(wz).Append(')');
                    if (ci.Slot == (int)EquipmentIndex.Head && kind == KHead)
                    {
                        // lancuch ochrony glowy agenta (RBM liczy ja z helmu w SpawnEquipment) - z kopia i chwilowo z oryginalem
                        float now = agent.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Head);
                        float was;
                        var tmp = el;
                        tmp.SetModifier(ci.Orig);
                        eq[ci.Slot] = tmp;
                        try { was = agent.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Head); }
                        finally { eq[ci.Slot] = el; }
                        sb.Append(", lancuch Head ").Append((int)Math.Round(was)).Append(" -> ").Append((int)Math.Round(now)).Append(ChainOwner());
                    }
                    else if (ci.Slot <= (int)EquipmentIndex.Weapon3)
                    {
                        var mw = agent.Equipment[(EquipmentIndex)ci.Slot];
                        int mv = kind == KShield ? mw.GetGetModifiedArmorForCurrentUsage() : kind == KSwing ? mw.GetModifiedSwingDamageForCurrentUsage()
                               : kind == KThrust ? mw.GetModifiedThrustDamageForCurrentUsage() : mw.GetModifiedMissileDamageForCurrentUsage();
                        sb.Append(", MissionWeapon ").Append(mv).Append(ReferenceEquals(mw.ItemModifier, kv.Key) ? "" : " (bez kopii)");
                    }
                    else if (ci.Slot == (int)EquipmentIndex.HorseHarness)
                    {
                        var mount = agent.MountAgent;
                        var hel = mount != null && mount.SpawnEquipment != null ? mount.SpawnEquipment[(int)EquipmentIndex.HorseHarness] : default(EquipmentElement);
                        bool has = hel.Item == it && ReferenceEquals(hel.ItemModifier, kv.Key);
                        sb.Append(", kon agenta kopia ").Append(has ? "TAK" : "NIE");
                        if (has) sb.Append(' ').Append(hel.GetModifiedMountBodyArmor());
                    }
                }
                catch (Exception ex) { Stumble("HeroHp.Test", ex); }
            }
            if (n == 0) return;
            t.TestLines++;
            Log.Info(sb.Append('.').ToString());
        }

        private static int Getter(EquipmentElement el, int kind)
        {
            switch (kind)
            {
                case KHead: return el.GetModifiedHeadArmor();
                case KBody: return el.GetModifiedBodyArmor();
                case KArm: return el.GetModifiedArmArmor();
                case KLeg: return el.GetModifiedLegArmor();
                case KHorse: return el.GetModifiedMountBodyArmor();
                case KSwing: return el.GetModifiedSwingDamageForUsage(0);
                case KThrust: return el.GetModifiedThrustDamageForUsage(0);
                case KMissile: return el.GetModifiedMissileDamageForUsage(0);
                default: return el.Item.WeaponComponent.PrimaryWeapon.GetModifiedArmor(el.ItemModifier);
            }
        }

        private static string _chainOwner;

        /// <summary>Kto liczy ochrone czesci ciala agenta: (RBM) - prefiks RBM na Agent.GetBaseArmorEffectivenessForBodyPart (czyta helm z SpawnEquipment),
        /// (gra) - wlasciwosci agenta z budowy.</summary>
        private static string ChainOwner()
        {
            if (_chainOwner != null) return _chainOwner;
            string r = " (gra)";
            try
            {
                var mi = AccessTools.DeclaredMethod(typeof(Agent), "GetBaseArmorEffectivenessForBodyPart");
                var pi = mi != null ? Harmony.GetPatchInfo(mi) : null;
                if (pi != null)
                    foreach (var p in pi.Prefixes)
                    {
                        string o = (p.owner ?? "") + " " + (p.PatchMethod != null && p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.FullName : "");
                        r = o.IndexOf("RBM", StringComparison.OrdinalIgnoreCase) >= 0 ? " (RBM)" : " (inny)";
                        if (r == " (RBM)") break;
                    }
            }
            catch { }
            _chainOwner = r;
            return r;
        }

        // ------------------------------------------------------------ koniec misji
        private static void EndMission(Mission m)
        {
            var t = _t;
            if (t == null || t.Done || !ReferenceEquals(Target(_mref), m)) { Clear(); return; }
            t.Done = true;
            int leaks = 0;
            var reg = _copies;
            var dead = Campaign.Current != null ? Campaign.Current.DeadBattleEquipment : null;
            foreach (var h in _heroes)
            {
                try
                {
                    var heq = h.BattleEquipment;
                    if (heq == null || ReferenceEquals(heq, dead)) continue;
                    for (int i = 0; i < 12; i++)
                    {
                        var el = heq[i];
                        CopyInfo ci;
                        if (el.ItemModifier == null || !reg.TryGetValue(el.ItemModifier, out ci)) continue;
                        el.SetModifier(ci.HeroMod);   // kopia wyciekla do sprzetu bohatera - wraca oryginal
                        heq[i] = el;
                        leaks++;
                    }
                }
                catch (Exception e) { Stumble("HeroHp.Leaks", e); }
            }
            Log.Info(P + "koniec bitwy " + t.N + " | bohaterow z agentem " + t.Agents + ", z kopiami " + t.WithCopies + ", niezgodny sprzet " + t.MismHeroes + " (sloty "
                     + t.MismSlots + ") | kopii " + t.Copies + " (neutralnych " + t.Neutral + ", przycietych " + t.Clipped + ", kon agenta " + t.Horse + ") | przecieki "
                     + leaks + " | test " + (t.Test ? "TAK" : "NIE") + " (linii " + t.TestLines + ") | potkniecia " + t.Stumbles + ".");
            Clear();
        }

        // ------------------------------------------------------------ start sesji
        internal static void StartLine()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                bool prot = ProtOn(s);
                int pl = 0;
                var beh = ArmouryBehavior.Instance;
                if (beh != null && Hero.MainHero != null)
                    for (int slot = 0; slot < 12; slot++) { float d; if (beh.WornExactD(slot, out d)) pl++; }
                Log.Info(P + "start | ochrona z HP " + YN(prot) + " (WornExactHpProtection " + YN(s.WornExactHpProtection) + ", GearFourStates " + YN(GearState.On)
                         + ", ConditionScalesStats " + YN(s.ConditionScalesStats) + "), zuzycie towarzyszy NIE | latka Agent.InitializeSpawnEquipment " + PatchState()
                         + " | ksiega gracza: sztuk z HP " + pl + " | ksiegi towarzyszy: bohaterow 0 (w druzynie 0), sztuk 0, bledne wiersze 0 | " + Curve(s)
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("HeroHp.StartLine", e); }
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        /// <summary>Kontrola krzywej: kolczuga 50 przy 41/56/78% i stan Damaged (ta sama funkcja Exact co w bitwie i w panelu).</summary>
        private static string Curve(Settings s)
        {
            int a = Exact(50, 50, null, 41f), b = Exact(50, 50, null, 56f), c = Exact(50, 50, null, 78f);
            int st = Math.Max(1, (int)Math.Floor(50 * ConditionScaling.StatFactor(GearState.Value[GearState.Damaged])));
            bool ok = a <= b && b <= c && c <= 50 && a >= 1 && a == Math.Max(1, (int)Math.Floor(50 * ConditionScaling.StatFactor(0.41f)));
            return "krzywa " + s.ConditionPenaltyMax.ToString("0.##", CI) + "/" + s.ConditionPenaltyExponent.ToString("0.##", CI) + ": 50 przy 41/56/78% -> " + a + "/" + b + "/" + c
                   + ", stan Damaged " + st + " - " + (ok ? "OK" : "BLAD");
        }

        /// <summary>Stan latki na Agent.InitializeSpawnEquipment: nasz postfiks TAK/NIE i wlasciciele innych latek (RBM).</summary>
        private static string PatchState()
        {
            try
            {
                var mi = AccessTools.DeclaredMethod(typeof(Agent), "InitializeSpawnEquipment");
                var pi = mi != null ? Harmony.GetPatchInfo(mi) : null;
                if (pi == null) return "NIE (inne: brak)";
                bool ours = false;
                var other = new List<string>();
                foreach (var list in new[] { pi.Prefixes, pi.Postfixes, pi.Transpilers, pi.Finalizers })
                    foreach (var p in list)
                    {
                        if (p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(SpawnPatch)) { ours = true; continue; }
                        string o = string.IsNullOrEmpty(p.owner) ? "?" : p.owner;
                        if (!other.Contains(o)) other.Add(o);
                    }
                return YN(ours) + " (inne: " + (other.Count > 0 ? string.Join(", ", other.ToArray()) : "brak") + ")";
            }
            catch { return "? (inne: ?)"; }
        }

        /// <summary>Hak autotestu: plik CrashScribe/armoury-hp.flag i tryb autotestu (LogGate 1/2) - w grze Jeffa false.</summary>
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
            }
            catch { return false; }
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            var t = _t;
            if (t != null) t.Stumbles++;
            if (_stumbles <= 3 || _stumbles % 50 == 0) Log.Error(where, e);
        }
    }
}
