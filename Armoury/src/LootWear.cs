using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// m110-stan (Jeff 23.10 ok. 14:45: "kazdy pancerz ma durability i powinien byc w takim stanie, jak zadam mu obrazenia, np. walne goscia w body i zabijam,
    /// zeszlo mu z pancerza np. 480 punktow z 2000 ... to zniszczony w sumie") - STAN LUPU Z PRAWDZIWYCH OBRAZEN. Kazda czesc zbroi z ciala wroga powalonego
    /// przez druzyne gracza (droga cialo po ciele: BattlefieldLaw.LootTag, QueueWreck, LootTally.FelledPost) ma d = stan przed bitwa (z jej modyfikatora -
    /// srodek przedzialu stanu) minus obrazenia, ktore TA czesc dostala w tej bitwie, przez pule wytrzymalosci sztuki - ta sama formula i pula co przy zuzyciu
    /// rynsztunku gracza (FieldCraft.OnAgentHit + ArmouryBehavior.ApplyWearPerSlot: surowy cios = obrazenia + pochloniete przez pancerz, pocisk x
    /// MissileArmorWearPercent, trafienie konia x HarnessWearFactor do uprzezy jezdzca, blok tarcza nie obija zbroi; pula = ochrona x DurabilityPerArmorPoint x
    /// tier). Stan wedlug progow 80/40/10 (GearState); d &lt;= LootMinConditionPercent - zniszczona (nie trafia na ekran). Cios smiertelny jest juz policzony,
    /// gdy DTE rozlicza cialo (gra wola OnAgentHit przed Die - dekompilacja Agent.cs :5468).
    /// Ksiega misji: wrogowie strony gracza (ludzie, nie bohaterowie) - stan przed (OnAgentBuild, tylko gdy ktoras czesc nie jest sprawna: RBM obniza potem
    /// SpawnEquipment w trakcie walki) i obrazenia na czesc (OnAgentHit); czyszczona na koncu misji. Bez latki Harmony (MissionLogic).
    /// Hak autotestu (plik CrashScribe/armoury-stan.flag, tryb autotestu LogGate 1/2): wrogowie dostaja sztuczny stan "przed" po kolei (sprawny, uszkodzony,
    /// zmasakrowany, wrak) - tylko liczony stan lupu, nie rynsztunek ani walka; linie czesci bez limitu. W grze Jeffa nic nie robi.
    /// Bledy liczone (pierwsze 3 i co 50), bez globalnego wylacznika.
    /// </summary>
    internal static class LootWear
    {
        private const string P = "Stan lupu (m110): ";
        private const int PieceLinesPerBattle = 12;
        internal const int CLup = 0, CCios = 1, CPek = 2;
        private static readonly string[] CauseName = { "lup", "cios", "pekniete" };
        internal const int RTy = 0, RDz = 1, RZbr = 2, RWrak = 3, RPrzep = 4, RZost = 5, RZn = 6;
        private static readonly string[] RouteName = { "do Ciebie", "dzialka", "zbrojownia wojska", "wrak do Ciebie", "przepadla", "zostaje u wojska", "zniszczona" };
        private static readonly string[] KindName = { "kon", "uprzaz", "tulow", "glowa", "rece", "nogi", "peleryna", "bron", "pociski", "tarcza", "inne" };

        internal static bool On
        {
            get { var s = Settings.Current; return GearState.On && s != null && s.LootStateFromWounds && s.BattlefieldLawEnabled; }
        }

        /// <summary>Czesc zbroi z ciala wroga - stan z obrazen.</summary>
        internal sealed class PieceData
        {
            public ItemObject Item;
            public int Slot, Pool, State, Cause, Kind;   // State 0..3; Kind 1 Ty/towarzysz, 2 zolnierz
            public float Before, Hits, D;
            public string Who;
            public bool Destroyed, Routed, Split;
            internal Tally T;
        }

        internal sealed class Tally
        {
            public int N;
            public bool Closed;
            public int Enemies, Hits, BeforeNonSound, Pieces, Agents;
            public readonly int[] Lup = new int[5];          // do gracza wedlug stanu, [4] zniszczone
            public int LupZost;
            public readonly int[,] St = new int[3, 5];       // cios / pekniete: stan, [4] zniszczone (CLup nieuzywane)
            public readonly int[,] R = new int[3, 5];        // cios / pekniete: do Ciebie, dzialka, zbrojownia, wraki, przepadly
            public int NoBookLup, NoBookWr;
            public bool Test;
            public readonly int[] TestSt = new int[4];
            public int Lines, Stumbles;
            public int Min;                                   // recenzja R8: prog zniszczenia (LootMinConditionPercent) tej bitwy - czesci i linia bitwy
            public long Ticks;
            public readonly List<PieceData> Open = new List<PieceData>();
        }

        // ------------------------------------------------------------ ksiega misji
        private static readonly object _lock = new object();
        private static WeakReference _mref;
        private static int _battle = -1;          // -1 nie sprawdzone, 0 misja poza bitwa, 1 bitwa kampanii
        private static bool _test;
        private static int _testIdx;
        private static readonly HashSet<Agent> _enemies = new HashSet<Agent>();
        private static readonly Dictionary<Agent, float[]> _before = new Dictionary<Agent, float[]>();   // sloty 5..11 (indeks slot - 5)
        private static readonly Dictionary<Agent, float[]> _hits = new Dictionary<Agent, float[]>();
        private static Tally _t;
        private static int _battleN, _stumbles;

        internal sealed class Logic : MissionBehavior
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void AfterStart()
            {
                base.AfterStart();
                try { Begin(Mission); } catch (Exception e) { Stumble("LootWear.AfterStart", e); }
            }

            public override void OnAgentBuild(Agent agent, Banner banner)
            {
                try { Built(agent); } catch (Exception e) { Stumble("LootWear.OnAgentBuild", e); }
            }

            public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
            {
                try { Hit(affectedAgent, blow, attackCollisionData); } catch (Exception e) { Stumble("LootWear.OnAgentHit", e); }
            }

            protected override void OnEndMission()
            {
                try { EndMission(Mission); } catch (Exception e) { Stumble("LootWear.OnEndMission", e); }
                base.OnEndMission();
            }
        }

        private static object Target(WeakReference w)
        {
            try { return w != null ? w.Target : null; } catch { return null; }
        }

        /// <summary>Nowa misja: ksiega od zera; poprzednia bitwa nierozliczona (kolejka czeka) - linia "bez rozliczenia".</summary>
        private static void Begin(Mission m)
        {
            if (m == null || ReferenceEquals(Target(_mref), m)) return;
            lock (_lock)
            {
                _mref = new WeakReference(m);
                _battle = -1;
                _enemies.Clear(); _before.Clear(); _hits.Clear();
                _testIdx = 0;
                _test = false;
            }
        }

        /// <summary>Misja to bitwa kampanii z bitwa gracza na mapie (jak FieldCraft.InBattle) - liczone raz na misje; pierwsza bitwa otwiera ksiege bitwy.</summary>
        private static bool Battle(Mission m)
        {
            if (m == null) return false;
            if (!ReferenceEquals(Target(_mref), m)) Begin(m);
            if (_battle >= 0) return _battle == 1;
            bool b;
            try { b = MapEvent.PlayerMapEvent != null && m.CombatType == Mission.MissionCombatType.Combat; }
            catch { return false; }
            _battle = b ? 1 : 0;
            if (b)
            {
                if (_t != null && !_t.Closed) Close("bez rozliczenia - nastepna bitwa", 0, 0);
                var s0 = Settings.Current;
                _t = new Tally { N = ++_battleN, Min = s0 != null ? Math.Max(0, s0.LootMinConditionPercent) : 0 };
                _test = GearState.TestWanted();
                _t.Test = _test;
            }
            return b;
        }

        private static void Built(Agent agent)
        {
            if (!On || agent == null || !agent.IsHuman) return;
            var m = agent.Mission ?? Mission.Current;
            if (!Battle(m)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var ch = agent.Character;
                if (ch == null || ch.IsHero) return;
                if (!MissionTeams.Enemies(agent.Team, m.PlayerTeam)) return;
                var eq = agent.SpawnEquipment;
                float[] b = null;
                int forced = -1;
                if (_test) forced = (_testIdx++) % 4;
                for (int slot = 5; slot <= 11; slot++)
                {
                    if (slot == (int)EquipmentIndex.Horse) continue;
                    var el = eq[(EquipmentIndex)slot];
                    if (el.Item == null || !el.Item.HasArmorComponent) continue;
                    int st = forced >= 0 ? forced : GearState.Of(el.ItemModifier);
                    if (st <= GearState.Sound) continue;
                    if (b == null) { b = new float[7]; for (int i = 0; i < 7; i++) b[i] = 100f; }
                    b[slot - 5] = GearState.Mid[st];
                }
                lock (_lock)
                {
                    _enemies.Add(agent);
                    if (b != null) _before[agent] = b;
                    if (_t != null)
                    {
                        _t.Agents++;
                        if (b != null) _t.BeforeNonSound++;
                        if (forced >= 0) _t.TestSt[forced]++;
                    }
                }
            }
            finally { Lap(t0); }
        }

        private static void Hit(Agent affected, Blow blow, AttackCollisionData acd)
        {
            if (affected == null || !On) return;
            var m = affected.Mission ?? Mission.Current;
            if (!Battle(m)) return;
            if (acd.AttackBlockedWithShield) return;   // blok tarcza - nie zbroja (jak u gracza)
            int raw = blow.InflictedDamage + Math.Max(0, acd.AbsorbedByArmor);
            if (raw <= 0) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var c = Settings.Current;
                float scale = blow.IsMissile ? Math.Max(0f, Math.Min(100f, c.MissileArmorWearPercent)) / 100f : 1f;
                Agent victim;
                int slot;
                float amount;
                if (affected.IsMount)
                {
                    victim = affected.RiderAgent;   // kon wroga obrywa - cierpi uprzaz jezdzca (DTE bierze ja z ciala jezdzca, slot 11)
                    if (victim == null) return;
                    slot = (int)EquipmentIndex.HorseHarness;
                    amount = raw * Math.Max(0f, c.HarnessWearFactor) * scale;
                }
                else if (affected.IsHuman)
                {
                    victim = affected;
                    slot = FieldCraft.ArmorSlotFor(blow.VictimBodyPart, affected);
                    amount = raw * scale;
                }
                else return;
                if (slot < 5 || slot > 11 || amount <= 0f) return;
                lock (_lock)
                {
                    if (!_enemies.Contains(victim)) return;
                    float[] h;
                    if (!_hits.TryGetValue(victim, out h)) { h = new float[7]; _hits[victim] = h; if (_t != null) _t.Enemies++; }
                    h[slot - 5] += amount;
                    if (_t != null) _t.Hits++;
                }
            }
            finally { Lap(t0); }
        }

        private static void EndMission(Mission m)
        {
            lock (_lock)
            {
                if (!ReferenceEquals(Target(_mref), m)) return;
                _enemies.Clear(); _before.Clear(); _hits.Clear();   // agenci puszczeni (stan lupu policzony przy ciele)
            }
        }

        private static void Lap(long t0)
        {
            long d = Stopwatch.GetTimestamp() - t0;
            if (d > 0 && _t != null) _t.Ticks += d;
        }

        // ------------------------------------------------------------ czesc z ciala
        /// <summary>Stan czesci zbroi item z ciala ofiary (slot - jesli znany, inaczej szukany w SpawnEquipment 5-11); null - czesc spoza ksiegi (nie zbroja,
        /// nie wrog z tej bitwy, misja bez ksiegi, klucz wylaczony).</summary>
        internal static PieceData Piece(Agent victim, ItemObject it, int slot, int cause, int kind, string who)
        {
            if (!On || victim == null || it == null || !it.HasArmorComponent) return null;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var m = Mission.Current;
                if (m == null || !ReferenceEquals(Target(_mref), m) || _battle != 1 || _t == null) return null;
                var eq = victim.SpawnEquipment;
                if (slot < 5 || slot > 11 || slot == (int)EquipmentIndex.Horse || eq[(EquipmentIndex)slot].Item != it)
                {
                    slot = -1;
                    for (int s = 5; s <= 11; s++)
                    {
                        if (s == (int)EquipmentIndex.Horse) continue;
                        var x = eq[(EquipmentIndex)s].Item;
                        if (x != null && (x == it || x.StringId == it.StringId)) { slot = s; break; }
                    }
                }
                if (slot < 0) return null;
                float before = 100f, hits = 0f;
                lock (_lock)
                {
                    if (!_enemies.Contains(victim)) return null;
                    float[] a;
                    if (_before.TryGetValue(victim, out a)) before = a[slot - 5];
                    if (_hits.TryGetValue(victim, out a)) hits = a[slot - 5];
                }
                int pool = ArmouryBehavior.ArmorPool(it);
                if (pool <= 0) return null;
                float d = Math.Max(0f, Math.Min(100f, before - hits * 100f / pool));
                int min = _t.Min;   // recenzja R8: prog bitwy (ten sam w linii bitwy - STAN-2)
                var pd = new PieceData
                {
                    Item = it, Slot = slot, Pool = pool, Before = before, Hits = hits, D = d, State = GearState.OfCondition(d),
                    Cause = cause, Kind = kind, Who = who ?? "?", Destroyed = min > 0 && d <= min + 0.0001f, T = _t
                };
                _t.Pieces++;
                _t.Open.Add(pd);
                return pd;
            }
            catch (Exception e) { Stumble("LootWear.Piece", e); return null; }
            finally { Lap(t0); }
        }

        /// <summary>Kto powalil - napis do linii czesci.</summary>
        internal static string WhoOf(int kind, Agent killer)
        {
            if (kind == 2) return "zolnierz";
            try
            {
                var a = killer;
                if (a != null && a.IsMount && a.RiderAgent != null) a = a.RiderAgent;
                return a != null && a == Agent.Main ? "Ty" : "towarzysz";
            }
            catch { return "towarzysz"; }
        }

        /// <summary>Droga czesci (raz): do linii bitwy i - pierwsze 12 na bitwe, pod flaga testu kazda - linia czesci.</summary>
        internal static void Route(PieceData pd, int route)
        {
            if (pd == null || pd.Routed) return;
            pd.Routed = true;
            var t = pd.T;
            if (t == null) return;
            try
            {
                int c = pd.Cause;
                if (c == CLup)
                {
                    if (route == RZost) t.LupZost++;
                    else if (route == RZn || pd.Destroyed) t.Lup[4]++;
                    else t.Lup[pd.State]++;
                }
                else
                {
                    t.St[c, pd.Destroyed ? 4 : pd.State]++;
                    if (!pd.Destroyed)
                    {
                        int col = route == RTy ? 0 : route == RDz ? 1 : route == RZbr ? 2 : route == RWrak ? 3 : 4;
                        t.R[c, col]++;
                    }
                }
                if (t.Lines < PieceLinesPerBattle || t.Test)
                {
                    t.Lines++;
                    int k = LootTally.KindOf(pd.Item);
                    Log.Info(P + "czesc " + (k >= 0 && k < KindName.Length ? KindName[k] : "inne") + " " + pd.Item.StringId + " (" + CauseName[c] + ", " + pd.Who + "): przed "
                             + GearState.Pl[GearState.OfCondition(pd.Before)] + " (" + pd.Before.ToString("0", CultureInfo.InvariantCulture) + "%), obrazenia "
                             + pd.Hits.ToString("0", CultureInfo.InvariantCulture) + "/" + pd.Pool + ", po " + pd.D.ToString("0.0", CultureInfo.InvariantCulture) + "% -> "
                             + (pd.Destroyed ? "zniszczona" : GearState.Pl[pd.State]) + " | " + RouteName[route] + ".");
                }
            }
            catch (Exception e) { Stumble("LootWear.Route", e); }
        }

        /// <summary>Sztuka bez ksiegi (lup: dzialka zdjeta ze zbrojowni bez danych czesci; cios/pekniete: wrak bez danych) - tylko licznik bitwy.</summary>
        internal static void NoBook(bool wreck)
        {
            var t = _t;
            if (t == null || t.Closed || !On) return;
            if (wreck) t.NoBookWr++; else t.NoBookLup++;
        }

        // ------------------------------------------------------------ zamkniecie bitwy
        /// <summary>Kolejka lupu oddana (lista trofeow, sakwy, zwrot, ekran gry) albo nastepna bitwa: linia bitwy; czesci bez drogi - lup zostaje u wojska,
        /// cios/pekniete przepadly.</summary>
        internal static void Close(string how, int luckyOk, int luckyNeed)
        {
            var t = _t;
            if (t == null || t.Closed) return;
            t.Closed = true;
            try
            {
                foreach (var pd in t.Open) if (!pd.Routed) Route(pd, pd.Cause == CLup ? RZost : RPrzep);
                t.Open.Clear();
                if (t.Agents == 0 && t.Pieces == 0 && t.Hits == 0 && t.NoBookLup + t.NoBookWr == 0) return;   // bitwa bez wrogow w ksiedze - bez linii
                if (t.Test)
                    Log.Info(P + "test - bitwa " + t.N + ": sztuczny stan przed bitwa dla " + (t.TestSt[0] + t.TestSt[1] + t.TestSt[2] + t.TestSt[3]) + " wrogow (sprawny "
                             + t.TestSt[0] + ", uszkodzony " + t.TestSt[1] + ", zmasakrowany " + t.TestSt[2] + ", wrak " + t.TestSt[3] + ").");
                var sb = new StringBuilder(P);
                sb.Append("bitwa ").Append(t.N).Append(" | ksiega: wrogow ").Append(t.Agents).Append(" (trafionych ").Append(t.Enemies).Append("), trafien ").Append(t.Hits)
                  .Append(", przed nie-sprawnych ").Append(t.BeforeNonSound).Append(", czesci z ksiega ").Append(t.Pieces)
                  .Append(" | lup z cial [sprawny/uszkodzony/zmasakrowany/wrak/zniszczony]: do gracza ").Append(Join(t.Lup)).Append(", zostaje u wojska ").Append(t.LupZost);
                for (int c = CCios; c <= CPek; c++)
                {
                    sb.Append(" | ").Append(CauseName[c]).Append(" [sprawny/uszkodzony/zmasakrowany/wrak/zniszczony]: ");
                    for (int i = 0; i < 5; i++) { if (i > 0) sb.Append('/'); sb.Append(t.St[c, i]); }
                    sb.Append(" -> do Ciebie ").Append(t.R[c, 0]).Append(", dzialka ").Append(t.R[c, 1]).Append(", zbrojownia ").Append(t.R[c, 2])
                      .Append(", wraki ").Append(t.R[c, 3]).Append(", przepadly ").Append(t.R[c, 4]);
                }
                sb.Append(" | bez ksiegi: lup ").Append(t.NoBookLup).Append(", cios/pekniete ").Append(t.NoBookWr)
                  .Append(" | koniec: ").Append(how).Append(" (lucky ").Append(luckyOk).Append('/').Append(luckyNeed).Append(')')
                  .Append(" | prog zniszczenia ").Append(t.Min).Append('%')   // recenzja R8: STAN-2 sprawdza "zniszczona" wobec tego progu
                  .Append(" | pomiar ").Append((t.Ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms (trafien ").Append(t.Hits).Append(')')
                  .Append(" | potkniecia ").Append(t.Stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("LootWear.Close", e); }
        }

        private static string Join(int[] a)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(a[i]); }
            return sb.ToString();
        }

        /// <summary>Nowa gra albo wczytanie (BattlefieldLaw.LReset): otwarta bitwa zamknieta bez rozliczenia, ksiega misji od zera.</summary>
        internal static void Reset()
        {
            try { Close("bez rozliczenia - nowa kampania albo wczytanie", 0, 0); } catch { }
            lock (_lock) { _enemies.Clear(); _before.Clear(); _hits.Clear(); _mref = null; _battle = -1; }
            _t = null;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            var t = _t;
            if (t != null) t.Stumbles++;
            if (_stumbles <= 3 || _stumbles % 50 == 0) Log.Error(where, e);
        }
    }
}
