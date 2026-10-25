using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P1 "marsz-doktryny" (projekt ai-marsz-taktyki.json B/C; klucze CultureDoctrines, ShootBeforeAttacking, ShootFirstSeconds, CavalrySeeksFlanks,
    /// ArchersFallBackBehindInfantry, ArcherFallBackDistance). Jeff 25.10: "zastosuj taktyki sredniowiecznej bitwy dla armii, pod wzgledem kultury np. Dothrakowie
    /// walcza inaczej niz Lannisterowie ... jesli jedna strona widzi, ze druga nie ma lucznikow, to najpierw wykorzystuje lucznikow".
    ///
    /// KONTRAKT (sygnatury ustalone w P0):
    ///  - ApplyMissionPatches(h) - z NightHooks.FirstBattle (raz na proces); opis do linii "Latki nocy (K3)".
    ///  - Begin / Tick / End     - z BattleSense (Begin PRZED WarCouncil.Council - tablice gotowe, zanim One wola ApplyList; Tick po MarchToContact, przed BattleSound).
    ///  - ResetCounters / Activity / Append - segment 'doktryna' linii "Bitwa (NB2)" (pod RoutHold.Gate).
    ///  - Zdarzenia dla dzwieku: Doctrine.ArchersFellBack(f) i Doctrine.FlankRide(f) sa w Doctrine.Events.cs (plik integratora) - P1 je WOLA, nie pisze.
    /// Potkniecia: RoutHold.Stumble(RoutHold.SiteDoctrine, ...).
    ///
    /// DOWOD [K]: TeamAIComponent.MakeDecision (co ok. 5 s, tick druzyn) wybiera MaxBy(GetTacticWeight x 1.5 dla biezacej), zmiana tylko gdy nowa > 1.5 x biezaca.
    /// RBM w EarlyStart daje liste wedlug kultury (Embolon, splity z waga 10, RangedHarrass tylko khuzait) i mnozy RangedHarrass x1.1 (khuzait) / x0.6 (reszta),
    /// FrontalCavalryCharge x0.75. Gra: RangedHarrass = udzial strzelcow x clamp(nasz / wroga dystans, 0.5..2) - przy wrogu bez lucznikow x2.
    ///
    /// ZASADY v1:
    ///  - kultura = BasicCulture strony (kultura KROLESTWA - ta sama, ktora bierze RBM i WarCouncil; jedna zasada), Skagos ma kulture freefolk;
    ///  - bramka Tactics z WarCouncil: ponizej 30 tlum - bez doktryny; od 30 mnozniki wag i dodatki listy: RangedHarrassmentOffensive (atak i obrona - waga gry
    ///    sama spada do zera bez strzelcow), TacticCharge dla dzikich (freefolk / wildlings / skagosi); druzyna GRACZA bez doktryny, sojusznicy AI tak;
    ///  - doktryna = mnozniki wag taktyk gry (postfiksy GetTacticWeight, Priority.Last po com.rbmai): Shock (FullScaleAttack, Charge), Shot (RangedHarrass),
    ///    Horse (FrontalCavalryCharge), Hold (DefensiveLine, DefensiveEngagement, DefensiveRing, HoldChokePoint); splity RBM (waga 10) i CoordinatedRetreat
    ///    bez zmian (prefiks RBM rozbija strone);
    ///  - S1 Shoot Before Attacking: nasze strzelectwo >= 15% i wroga <= max(5%, 1/3 naszego) -> Shot x2.5 (piechota w RangedHarrass idzie ostroznie jako
    ///    zaslona - BehaviorCautiousAdvance); licznik rusza, gdy wrog podejdzie na 200 m; koniec po ShootFirstSeconds (x ShootSec kultury / 90), przy stracie
    ///    ponad polowy strzelcow albo w zwarciu - wtedy Shot x0.3 do konca bitwy i MakeDecision przechodzi na glowny atak (histereza gry x1.5);
    ///  - druzyny DynamicReinforcements (powstaja w trakcie bitwy z kopia taktyk) - WarCouncil.One + doktryna przy pierwszym takcie, w ktorym je widac;
    ///  - postfiksy (tick druzyn) tylko czytaja tablice float pisana w OnMissionTick (glowny watek, nie biegnie rownolegle z tickiem druzyn).
    /// v1.1 (NIE w tej wersji, klucze czekaja): S3 konnica na flanke (CavalrySeeksFlanks), S4 lucznicy za piechote (ArchersFallBackBehindInfantry),
    /// Rush (bieg dzikich z daleka / Nieskalani od 45 m), S2 (obronca pod deszczem strzal), specjalne taktyki RBM z doktryny (Embolon, SplitArchers od 100).
    /// </summary>
    internal static partial class Doctrine
    {
        internal const int HordeBelow = 30;
        internal const float S1OursMin = 0.15f, S1EnemyFloor = 0.05f, S1Boost = 2.5f, S1Fade = 0.3f, S1Range = 200f, S1ContactGap = 25f, BaseShootSec = 90f;
        internal const float TickEvery = 2f;
        private const int MaxTeams = 16, K = 4;   // 0 Shock, 1 Shot, 2 Horse, 3 Hold
        private const int KShock = 0, KShot = 1, KHorse = 2, KHold = 3;

        private sealed class Profile
        {
            public string Name;
            public float Shock = 1f, Shot = 1f, Horse = 1f, Hold = 1f, Flank = 1f, ShootSec = BaseShootSec;
            public bool Charge;
            public Profile(string name, float shock, float shot, float horse, float hold, float flank, float shootSec = BaseShootSec, bool charge = false)
            {
                Name = name; Shock = shock; Shot = shot; Horse = horse; Hold = hold; Flank = flank; ShootSec = shootSec; Charge = charge;
            }
        }

        private static readonly Profile Neutral = new Profile("neutral", 1f, 1f, 1f, 1f, 1f);
        private static Dictionary<string, Profile> _profiles;

        /// <summary>Profile kultur ROT (kanon + sklad drzew ROT-Troops; propozycja do oceny Jeffa). Kolejnosc: Shock, Shot, Horse, Hold, Flank.</summary>
        private static Dictionary<string, Profile> Profiles()
        {
            if (_profiles != null) return _profiles;
            var d = new Dictionary<string, Profile>(StringComparer.Ordinal);
            d["vlandia"] = new Profile("Westerlands", 1.0f, 1.0f, 1.2f, 1.0f, 1.1f);
            d["battania"] = new Profile("The North", 1.15f, 0.9f, 1.0f, 1.1f, 1.1f);
            d["river"] = new Profile("Riverlands", 1.0f, 1.15f, 1.0f, 1.3f, 1.0f);
            d["vale"] = new Profile("The Vale", 0.9f, 1.0f, 1.4f, 1.0f, 1.3f);
            d["reach"] = new Profile("The Reach", 1.0f, 0.9f, 1.3f, 1.0f, 1.3f);
            d["stormlands"] = new Profile("Stormlands", 1.2f, 1.0f, 1.1f, 0.8f, 1.0f);
            d["dragonstone"] = new Profile("Dragonstone", 1.0f, 1.0f, 1.0f, 1.0f, 1.3f);
            d["crownlands"] = new Profile("Crownlands", 1.0f, 1.2f, 0.8f, 1.3f, 1.0f);
            d["aserai"] = new Profile("Dorne", 0.8f, 1.3f, 1.0f, 1.0f, 1.4f, 120f);
            d["sturgia"] = new Profile("Iron Islands", 1.3f, 0.7f, 0.5f, 0.7f, 1.0f);
            var wild = new Profile("Free Folk", 1.4f, 0.8f, 1.0f, 0.6f, 1.0f, BaseShootSec, true);
            d["freefolk"] = wild; d["wildlings"] = wild; d["skagosi"] = wild;
            d["nightswatch"] = new Profile("Night's Watch", 0.8f, 1.4f, 1.0f, 1.5f, 1.0f);
            d["khuzait"] = new Profile("Dothraki", 1.0f, 1.3f, 1.3f, 0.3f, 1.5f);
            d["empire"] = new Profile("Braavos", 1.0f, 1.1f, 1.0f, 1.1f, 1.0f);
            d["nord"] = new Profile("Lorath", 1.1f, 1.0f, 1.0f, 1.2f, 1.0f);
            d["volantine"] = new Profile("Volantis", 1.0f, 1.0f, 1.2f, 1.1f, 1.0f);
            d["ghiscari"] = new Profile("Slaver's Bay", 1.0f, 1.0f, 0.5f, 1.3f, 1.0f);
            d["qartheen"] = new Profile("Qarth", 1.0f, 1.1f, 1.0f, 1.4f, 1.0f);
            d["yiti"] = new Profile("Yi Ti", 1.0f, 1.3f, 1.0f, 1.2f, 1.0f);
            d["sarnor"] = new Profile("Sarnor", 1.0f, 1.0f, 1.3f, 1.0f, 0.8f);
            d["ibbenese"] = new Profile("Ib", 1.3f, 0.8f, 1.0f, 1.0f, 1.0f);
            d["summer"] = new Profile("Summer Isles", 0.7f, 1.6f, 1.0f, 1.0f, 1.0f, 150f);
            d["qohorik"] = new Profile("Qohor", 1.0f, 1.0f, 1.0f, 1.3f, 1.0f);
            d["norvos"] = new Profile("Norvos", 1.2f, 1.0f, 1.0f, 1.0f, 1.0f);
            d["myrish"] = new Profile("Myr", 1.0f, 1.3f, 1.0f, 1.0f, 1.0f);
            var free = new Profile("Free City", 1.0f, 1.0f, 1.0f, 1.1f, 1.0f);
            d["pentoshi"] = free; d["lyseni"] = new Profile("Lys", 1.0f, 1.0f, 1.0f, 1.1f, 1.0f); d["tyroshi"] = new Profile("Tyrosh", 1.0f, 1.0f, 1.0f, 1.1f, 1.0f);
            d["valyrian"] = new Profile("Targaryen host", 1.0f, 1.0f, 1.2f, 1.1f, 1.0f);
            _profiles = d;
            return d;
        }

        private static Profile Find(string culture)
        {
            Profile p;
            if (culture != null && Profiles().TryGetValue(culture, out p)) return p;
            return null;
        }

        // ---- stan czytany przez postfiksy (tick druzyn); zapis tylko z glownego watku
        private static volatile bool _on;
        private static readonly float[] _mul = new float[MaxTeams * K];
        private static readonly Team[] _teamRef = new Team[MaxTeams];

        // ---- stan glownego watku per druzyna
        private static readonly Team[] _seen = new Team[MaxTeams];
        private static readonly Profile[] _prof = new Profile[MaxTeams];
        private static readonly bool[] _doct = new bool[MaxTeams];       // doktryna wpieta (Tactics >= 30, druzyna AI)
        private static readonly int[] _tacOf = new int[MaxTeams];
        private static readonly string[] _culOf = new string[MaxTeams];
        private static readonly byte[] _s1 = new byte[MaxTeams];         // 0 nie, 1 ostrzal, 2 po ostrzale
        private static readonly float[] _s1Start = new float[MaxTeams];  // pierwsza chwila stanu 1
        private static readonly float[] _s1Clock = new float[MaxTeams];  // start licznika (wrog na 200 m), -1 = jeszcze nie
        private static readonly float[] _s1Ranged = new float[MaxTeams]; // strzelcow na starcie ostrzalu
        private static readonly TacticComponent[] _cur = new TacticComponent[MaxTeams];
        private static readonly StringBuilder[] _hist = new StringBuilder[MaxTeams];
        private static WeakReference _m;
        private static float _acc;

        // ---- latki
        private static bool _tried;
        private static int _patched, _wanted;
        private static AccessTools.FieldRef<TeamAIComponent, TacticComponent> _fCur;

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _teams, _horde, _noProfile, _late, _addShot, _addCharge, _s1On, _s1Limit, _s1Loss, _s1Contact, _s1Back, _switches, _stum;
        private static string _sampleA, _sampleD, _histA, _histD;
        private static int _sampleTeamA = -1, _sampleTeamD = -1;
        private static int _calls, _postErr;

        // ------------------------------------------------------------ latki (raz na proces, pierwsza bitwa)

        internal static string ApplyMissionPatches(Harmony h)
        {
            if (_tried) return "juz wpiete";
            _tried = true;
            try { _fCur = AccessTools.FieldRefAccess<TeamAIComponent, TacticComponent>("_currentTactic"); } catch { _fCur = null; }
            var miss = new StringBuilder(80);
            Patch(h, typeof(TacticFullScaleAttack), nameof(PostShock), miss);
            Patch(h, typeof(TacticCharge), nameof(PostShock), miss);
            Patch(h, typeof(TacticRangedHarrassmentOffensive), nameof(PostShot), miss);
            Patch(h, typeof(TacticFrontalCavalryCharge), nameof(PostHorse), miss);
            Patch(h, typeof(TacticDefensiveLine), nameof(PostHold), miss);
            Patch(h, typeof(TacticDefensiveEngagement), nameof(PostHold), miss);
            Patch(h, typeof(TacticDefensiveRing), nameof(PostHold), miss);
            Patch(h, typeof(TacticHoldChokePoint), nameof(PostHold), miss);
            return "GetTacticWeight " + _patched + "/" + _wanted + " (Priority.Last po com.rbmai" + (miss.Length > 0 ? "; NIE:" + miss.ToString() : "")
                   + "), biezaca taktyka " + (_fCur != null ? "TAK" : "NIE");
        }

        private static void Patch(Harmony h, Type t, string post, StringBuilder miss)
        {
            _wanted++;
            try
            {
                var target = AccessTools.DeclaredMethod(t, "GetTacticWeight");
                if (target == null) { miss.Append(' ').Append(t.Name); return; }
                h.Patch(target, postfix: new HarmonyMethod(typeof(Doctrine), post) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                bool ok = false;
                var info = Harmony.GetPatchInfo(target);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(Doctrine)) ok = true;
                if (ok) _patched++; else miss.Append(' ').Append(t.Name);
            }
            catch (Exception e)
            {
                miss.Append(' ').Append(t.Name).Append('(').Append(e.GetType().Name).Append(')');
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Latka " + t.Name, e);
            }
        }

        private static void PostShock(object __instance, ref float __result) { Mul(__instance, KShock, ref __result); }
        private static void PostShot(object __instance, ref float __result) { Mul(__instance, KShot, ref __result); }
        private static void PostHorse(object __instance, ref float __result) { Mul(__instance, KHorse, ref __result); }
        private static void PostHold(object __instance, ref float __result) { Mul(__instance, KHold, ref __result); }

        /// <summary>Postfiks GetTacticWeight (tick druzyn): waga x mnoznik doktryny druzyny. Tylko odczyt tablicy, bez alokacji.</summary>
        private static void Mul(object inst, int k, ref float r)
        {
            if (!_on) return;
            try
            {
                var tc = inst as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_teamRef[ti], team)) return;
                float f = _mul[ti * K + k];
                if (f == 1f || float.IsNaN(r) || float.IsInfinity(r)) return;
                r *= f;
                Interlocked.Increment(ref _calls);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ misja

        internal static void Begin(Mission m)
        {
            Clear();
            _m = m != null ? new WeakReference(m) : null;
            _acc = 0f;
            // druzyny obecne przy AfterStart obsluguje WarCouncil.Council (zaraz po nas); pozniejsze (DR) - Tick
            try
            {
                var teams = m != null ? m.Teams : null;
                int n = teams != null ? teams.Count : 0;
                for (int i = 0; i < n; i++)
                {
                    var t = teams[i];
                    if (t == null) continue;
                    int ti = t.TeamIndex;
                    if (ti >= 0 && ti < MaxTeams) _seen[ti] = t;
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Begin druzyny", e); }
            var c = Settings.Current;
            _on = c != null && (c.CultureDoctrines || c.ShootBeforeAttacking) && m != null && m.IsFieldBattle && _patched > 0;
        }

        internal static void End(Mission m)
        {
            _on = false;
            // historia taktyk probek do linii NB2 (RoutKeeper moze ja pisac po nas) - potem stan bez druzyn (skonczona misja nie wisi w pamieci)
            try { lock (RoutHold.Gate) { if (_sampleTeamA >= 0) _histA = Hist(_sampleTeamA); if (_sampleTeamD >= 0) _histD = Hist(_sampleTeamD); } } catch { }
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            _on = false;
            for (int i = 0; i < MaxTeams; i++)
            {
                for (int k = 0; k < K; k++) _mul[i * K + k] = 1f;
                _teamRef[i] = null; _seen[i] = null; _prof[i] = null; _doct[i] = false; _tacOf[i] = 0; _culOf[i] = null;
                _s1[i] = 0; _s1Start[i] = 0f; _s1Clock[i] = -1f; _s1Ranged[i] = 0f; _cur[i] = null; _hist[i] = null;
            }
            _sampleTeamA = _sampleTeamD = -1;
        }

        /// <summary>WarCouncil.One (glowny watek, AfterStart albo pozna druzyna DR): doktryna druzyny AI i dodatki do listy taktyk. have/note - lista typow
        /// i opis WarCouncil (bez dubli). Zwraca krotki opis do probki.</summary>
        internal static void ApplyList(Mission m, Team team, int tac, string culture, List<Type> have, StringBuilder note, bool late)
        {
            var c = Settings.Current;
            if (c == null || team == null || (!c.CultureDoctrines && !c.ShootBeforeAttacking)) return;
            int ti = team.TeamIndex;
            if (ti < 0 || ti >= MaxTeams) return;
            _seen[ti] = team;
            _tacOf[ti] = tac;
            _culOf[ti] = culture;
            if (tac < HordeBelow)
            {
                lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _teams++; _horde++; if (late) _late++; }
                return;
            }
            Profile p = c.CultureDoctrines ? Find(culture) : null;
            bool noProfile = c.CultureDoctrines && p == null;
            if (p == null) p = Neutral;
            _prof[ti] = p;
            _doct[ti] = true;
            _hist[ti] = new StringBuilder(120);
            int addShot = 0, addCharge = 0;
            // RangedHarrass (atak i obrona): bez strzelcow waga gry = 0 (TacticRangedHarrassmentOffensive.GetTacticWeight), wiec dodanie jest bezpieczne
            if (WarCouncil.TryAdd(team, have, typeof(TacticRangedHarrassmentOffensive), note)) addShot++;
            if (c.CultureDoctrines && p.Charge && WarCouncil.TryAdd(team, have, typeof(TacticCharge), note)) addCharge++;
            SetBase(ti, c);
            _teamRef[ti] = team;
            var s = new StringBuilder(160);
            s.Append('T').Append(tac).Append('/').Append(culture ?? "?").Append(' ').Append(p.Name)
             .Append(" (Shock ").Append(F(p.Shock)).Append(", Shot ").Append(F(p.Shot)).Append(", Horse ").Append(F(p.Horse)).Append(", Hold ").Append(F(p.Hold)).Append(')');
            if (addShot > 0) s.Append(" +RangedHarrass");
            if (addCharge > 0) s.Append(" +Charge");
            if (late) s.Append(" (DR)");
            bool att = team.Side == BattleSideEnum.Attacker;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _teams++;
                if (noProfile) _noProfile++;
                if (late) _late++;
                _addShot += addShot;
                _addCharge += addCharge;
                if (att) { if (_sampleA == null) { _sampleA = s.ToString(); _sampleTeamA = ti; } }
                else if (_sampleD == null) { _sampleD = s.ToString(); _sampleTeamD = ti; }
            }
            if (m != null && m.IsFieldBattle && _patched > 0 && (c.CultureDoctrines || c.ShootBeforeAttacking)) _on = true;
        }

        private static string F(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        private static void SetBase(int ti, Settings c)
        {
            var p = _prof[ti] ?? Neutral;
            bool d = c.CultureDoctrines;
            _mul[ti * K + KShock] = d ? p.Shock : 1f;
            _mul[ti * K + KShot] = d ? p.Shot : 1f;
            _mul[ti * K + KHorse] = d ? p.Horse : 1f;
            _mul[ti * K + KHold] = d ? p.Hold : 1f;
        }

        internal static void Tick(Mission m, float dt)
        {
            if (m == null) return;
            _acc += dt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;   // Begin nie biegl dla tej misji - nic
            var c = Settings.Current;
            if (c == null || !m.IsFieldBattle || m.MissionEnded) return;
            bool keys = c.CultureDoctrines || c.ShootBeforeAttacking;
            _on = keys && _patched > 0;
            if (!keys) return;
            float now = m.CurrentTime;
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                try
                {
                    if (!ReferenceEquals(_seen[ti], t))
                    {
                        // pozna druzyna (DynamicReinforcements) - rada wojenna i doktryna przy pierwszym takcie, w ktorym ja widac
                        _seen[ti] = t;
                        WarCouncil.LateTeam(m, t);
                    }
                    if (_doct[ti] && ReferenceEquals(_teamRef[ti], t)) TeamTick(m, t, ti, c, now);
                }
                catch (Exception e) { _stumLocal++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Druzyna", e); }
            }
            if (_stumLocal > 0) { lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _stum += _stumLocal; } _stumLocal = 0; }
        }

        private static int _stumLocal;

        private static void TeamTick(Mission m, Team t, int ti, Settings c, float now)
        {
            SetBase(ti, c);
            var p = _prof[ti] ?? Neutral;
            float baseShot = c.CultureDoctrines ? p.Shot : 1f;
            var q = t.QuerySystem;
            if (q != null && c.ShootBeforeAttacking && m.IsDeploymentFinished)
            {
                float ours = q.RangedRatio + q.RangedCavalryRatio;
                float enemy = q.EnemyRangedRatio + q.EnemyRangedCavalryRatio;
                int members = q.MemberCount;
                float rangedNow = ours * members;
                byte st = _s1[ti];
                if (st == 0)
                {
                    if (ours >= S1OursMin && enemy <= Math.Max(S1EnemyFloor, ours / 3f))
                    {
                        st = 1;
                        _s1Start[ti] = now;
                        _s1Clock[ti] = -1f;
                        _s1Ranged[ti] = rangedNow;
                        lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _s1On++; }
                        Note(ti, "ostrzal od s. ", now);
                    }
                }
                else if (st == 1)
                {
                    float gap = TeamGap(t);
                    if (_s1Clock[ti] < 0f && gap <= S1Range) _s1Clock[ti] = now;
                    float sec = Math.Max(0f, c.ShootFirstSeconds) * (p.ShootSec / BaseShootSec);
                    if (!c.CultureDoctrines) sec = Math.Max(0f, c.ShootFirstSeconds);
                    int why = 0;
                    if (_s1Clock[ti] >= 0f && now - _s1Clock[ti] >= sec) why = 1;
                    else if (_s1Ranged[ti] > 0f && rangedNow < 0.5f * _s1Ranged[ti]) why = 2;
                    else if (gap <= S1ContactGap) why = 3;
                    else if (enemy > Math.Max(S1EnemyFloor, ours / 3f) * 1.5f) why = 4;   // wrog jednak ma lucznikow (posilki) - bez wygaszenia
                    if (why > 0)
                    {
                        st = why == 4 ? (byte)0 : (byte)2;
                        lock (RoutHold.Gate)
                        {
                            RoutHold.EnsureLocked(m);
                            if (why == 1) _s1Limit++; else if (why == 2) _s1Loss++; else if (why == 3) _s1Contact++; else _s1Back++;
                        }
                        Note(ti, why == 1 ? "ostrzal koniec (limit) s. " : (why == 2 ? "ostrzal koniec (straty) s. " : (why == 3 ? "ostrzal koniec (zwarcie) s. " : "ostrzal cofniety s. ")), now);
                    }
                }
                _s1[ti] = st;
                if (st == 1) _mul[ti * K + KShot] = baseShot * S1Boost;
                else if (st == 2) _mul[ti * K + KShot] = baseShot * S1Fade;
            }
            // wybor taktyki gry (do linii: zmiany biezacej taktyki)
            if (_fCur != null)
            {
                TacticComponent cur = null;
                try { var ai = t.TeamAI; if (ai != null) cur = _fCur(ai); } catch { cur = null; }
                if (cur != null && !ReferenceEquals(cur, _cur[ti]))
                {
                    _cur[ti] = cur;
                    string nm = cur.GetType().Name;
                    if (nm.StartsWith("Tactic", StringComparison.Ordinal)) nm = nm.Substring(6);
                    var h = _hist[ti];
                    if (h != null && h.Length < 300) h.Append(h.Length > 0 ? ", " : "").Append(nm).Append(" s. ").Append(((int)now).ToString());
                    lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _switches++; }
                }
            }
        }

        private static void Note(int ti, string what, float now)
        {
            var h = _hist[ti];
            if (h != null && h.Length < 300) h.Append(h.Length > 0 ? ", " : "").Append(what).Append(((int)now).ToString());
        }

        /// <summary>Najmniejsza odleglosc od szyku druzyny do najblizszego szyku wroga (mediana - mediana minus polowy glebokosci).</summary>
        private static float TeamGap(Team t)
        {
            float best = float.MaxValue;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || f.CountOfUnits <= 0) continue;
                var eq = f.CachedClosestEnemyFormation;
                var ef = eq != null ? eq.Formation : null;
                if (ef == null || ef.CountOfUnits <= 0) continue;
                float d = f.CachedMedianPosition.AsVec2.Distance(ef.CachedMedianPosition.AsVec2) - 0.5f * (f.Depth + ef.Depth);
                if (d < best) best = d;
            }
            return best < 0f ? 0f : best;
        }

        // ------------------------------------------------------------ linia misji

        internal static void ResetCounters()
        {
            _teams = _horde = _noProfile = _late = _addShot = _addCharge = _s1On = _s1Limit = _s1Loss = _s1Contact = _s1Back = _switches = _stum = 0;
            _sampleA = _sampleD = _histA = _histD = null;
            Interlocked.Exchange(ref _calls, 0);
            Interlocked.Exchange(ref _postErr, 0);
        }

        internal static int Activity { get { return _teams + _s1On + _stum; } }

        /// <summary>Segment ' | doktryna ...' (bez znaku '|' w srodku). Wolajacy trzyma RoutHold.Gate. Historia taktyk probek czytana na koniec misji
        /// (glowny watek - Tick nie biegnie rownolegle).</summary>
        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | doktryna ").Append(c != null && c.CultureDoctrines ? "TAK" : "NIE").Append(" (najpierw ostrzal ").Append(c != null && c.ShootBeforeAttacking ? "TAK" : "NIE")
              .Append("): druzyn ").Append(_teams).Append(" (tlum ").Append(_horde).Append(", bez profilu kultury ").Append(_noProfile).Append(", pozne DR ").Append(_late)
              .Append("), lista +RangedHarrass ").Append(_addShot).Append(", +Charge ").Append(_addCharge)
              .Append("; najpierw ostrzal ").Append(_s1On).Append(" (koniec: limit czasu ").Append(_s1Limit).Append(", straty ").Append(_s1Loss)
              .Append(", zwarcie ").Append(_s1Contact).Append(", wrog jednak strzela ").Append(_s1Back).Append("); zmian taktyki ").Append(_switches)
              .Append("; probka atak ").Append(_sampleA ?? "-").Append(_histA ?? Hist(_sampleTeamA)).Append(", obrona ").Append(_sampleD ?? "-").Append(_histD ?? Hist(_sampleTeamD))
              .Append("; latki ").Append(_patched).Append('/').Append(_wanted).Append(", wywolan z mnoznikiem ").Append(Volatile.Read(ref _calls))
              .Append(", bledy latek ").Append(Volatile.Read(ref _postErr)).Append(", potkniec ").Append(_stum);
        }

        private static string Hist(int ti)
        {
            if (ti < 0 || ti >= MaxTeams) return "";
            var h = _hist[ti];
            if (h == null || h.Length == 0) return "";
            return " [" + h.ToString().Replace('|', '/') + "]";
        }
    }
}
