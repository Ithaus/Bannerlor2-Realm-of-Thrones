using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NB2 czesc 2 (16.10, Jeff: "lepszy dzwiek bitwy"): OKRZYKI BOJOWE PRZY SZARZY AI I JEKI RANNYCH, z limitami i bez petli (klucz BattleCriesAndMoans).
    /// Rozpoznanie rozp_mody (16.10) krok 4.
    ///
    /// Dowod [K]: w grze okrzyki graja tylko przy rozkazie szarzy GRACZA (OrderController.cs:626-634 -> Agent.YellingBehaviour, szansa morale x 0.33%
    /// na czlowieka) - szarze AI sa nieme; jek (Pain, 0.8 s, slychac na 80 m) silnik gra tylko w chwili trafienia (Mission.cs:5795). Natywne:
    /// Agent.YellAfterDelay (jednorazowe, Yell 0.5 s, 450 m) i Agent.MakeVoice(Pain).
    ///
    /// Zasady (wszystko z OnMissionTick, glowny watek, takt 0.5 s; bitwa na mapie / kryjowka / bitwa wlasna, bez bitew morskich i po koncu bitwy):
    ///  - szarza: szyk AI (Formation.IsAIControlled - szarze gracza ma gra) przechodzi na rozkaz Charge / ChargeToTarget po rozstawieniu; ten sam szyk
    ///    najwyzej raz na 40 s i nie po krotkiej przerwie w szarzy (8 s - RBM przelacza rozkazy czesto); krzyczy kapitan (zawsze, pierwszy) i czesc
    ///    ludzi: (10% + 3% x tier) x morale/50 (0.25..1.5) + 5% za kazde 50 Leadership kapitana (najwyzej 15%), najwyzej 75%; opoznienie 0.2-2.5 s;
    ///  - jek: AI ponizej 30% zdrowia (zapisany przy trafieniu, nie w ucieczce), do 60 m od gracza: MakeVoice(Pain) najwyzej 3 razy na rannego na
    ///    bitwe - pierwszy 3-7 s po ciosie, drugi 7-14 s pozniej, trzeci 20-30 s pozniej (poprawka 16.10: dawniej co 7-14 s do konca bitwy);
    ///  - bez bitew morskich i wypadow morskich (IsNavalBattle, IsNavalRaidBattle - poprawka 16.10);
    ///  - jeden glos na czlowieka na 4 s (okrzyk: 20 s), najwyzej 6 glosow na sekunde w misji (okrzyk odlozony najwyzej 2 x o 1 s, potem odrzucony
    ///    i liczony) - zadnych powtorek w kolko;
    ///  - ludzi szyku zbieramy do wlasnej listy (ApplyActionOnEachUnit tylko dodaje do listy), natywne glosy dopiero po zakonczeniu iteracji gry;
    ///  - umarli, gracz, uciekajacy (IsRunningAway - zlamani maja swoj jeden okrzyk paniki), bez waznej druzyny - bez glosu;
    ///  - wyjatek liczony (RoutHold.Stumble, miejsce "glosy"), funkcja dalej dziala.
    /// </summary>
    internal static class BattleCries
    {
        internal const float TickEvery = 0.5f;
        internal const float FormationCooldown = 40f, ChargeGap = 8f, ManCooldown = 20f, VoiceGap = 4f;
        internal const float BaseShare = 0.10f, PerTier = 0.03f, LeaderPer50 = 0.05f, LeaderMax = 0.15f, ShareMax = 0.75f;
        internal const float DelayMin = 0.2f, DelaySpan = 2.3f;
        internal const float MoanBelow = 0.30f, MoanRange = 60f, MoanMin = 7f, MoanSpan = 7f, MoanFirstMin = 3f, MoanFirstSpan = 4f;
        // poprawka po recenzji (16.10, Jeff zglaszal "krzyk zapetlony"): najwyzej 3 jeki na rannego na bitwe, odstep rosnie: 3-7 s po ciosie,
        // potem 7-14 s, potem 20-30 s; nowy cios ponizej progu nie zeruje licznika
        internal const int MoansPerMan = 3;
        internal const float MoanLateMin = 20f, MoanLateSpan = 10f;
        internal const int MaxPerSecond = 6, PendingMax = 500;

        private sealed class Form { public bool Charge; public float ChargeEnd = -1000f; public float LastCry = -1000f; }
        private sealed class Hurt { public Agent A; public float Next; public bool Dead; }

        // stan misji (glowny watek; _pending pod _pend - OnAgentHit moze przyjsc z ticku asynchronicznego)
        private static readonly Dictionary<Formation, Form> _forms = new Dictionary<Formation, Form>();
        private static readonly Dictionary<Agent, float> _lastVoice = new Dictionary<Agent, float>();
        private static readonly Dictionary<Agent, int> _moanCount = new Dictionary<Agent, int>();   // jeki na rannego w misji (glowny watek)
        private static readonly List<Hurt> _hurt = new List<Hurt>();
        private static readonly HashSet<Agent> _hurtSet = new HashSet<Agent>();
        private static readonly object _pend = new object();
        private static readonly List<Agent> _pending = new List<Agent>();
        private static readonly List<Agent> _buf = new List<Agent>();
        private static readonly Action<Agent> _collect = Collect;
        private static readonly int[] _bucketSec = { -1, -1, -1, -1, -1, -1, -1, -1 };
        private static readonly int[] _bucketN = new int[8];
        private static WeakReference _m;
        private static float _acc;

        // liczniki ticku (przenoszone do licznikow misji pod RoutHold.Gate na koniec ticku)
        private static int _tCharges, _tYells, _tMoans, _tLimited;
        private static readonly List<int> _tYellIds = new List<int>(), _tMoanIds = new List<int>();

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _charges, _yells, _moans, _limited;
        private static readonly HashSet<int> _yellPeople = new HashSet<int>(), _moanPeople = new HashSet<int>();

        private static void Collect(Agent a) { _buf.Add(a); }

        internal static void ResetCounters()
        {
            _charges = _yells = _moans = _limited = 0;
            _yellPeople.Clear();
            _moanPeople.Clear();
        }

        internal static int Activity { get { return _charges + _yells + _moans + _limited; } }

        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | okrzyki i jeki ").Append(c != null && c.BattleCriesAndMoans ? "TAK" : "NIE").Append(": szarze ").Append(_charges)
              .Append(" (okrzykow ").Append(_yells).Append(", roznych ludzi ").Append(_yellPeople.Count).Append("), jeki ").Append(_moans)
              .Append(" (rannych ").Append(_moanPeople.Count).Append("), odrzucone limitem ").Append(_limited);
        }

        /// <summary>AfterStart (BattleSense): stan od zera dla nowej misji.</summary>
        internal static void Begin(Mission m)
        {
            Clear();
            _m = new WeakReference(m);
        }

        internal static void Clear()
        {
            _forms.Clear();
            _lastVoice.Clear();
            _moanCount.Clear();
            _hurt.Clear();
            _hurtSet.Clear();
            _buf.Clear();
            lock (_pend) { _pending.Clear(); }
            for (int i = 0; i < 8; i++) { _bucketSec[i] = -1; _bucketN[i] = 0; }
            _acc = 0f;
            _m = null;
            _tCharges = _tYells = _tMoans = _tLimited = 0;
            _tYellIds.Clear();
            _tMoanIds.Clear();
        }

        /// <summary>OnAgentHit (BattleSense) - wywolanie zwrotne silnika: tylko zapis rannego do kolejki (wolajacy lapie wyjatki).</summary>
        internal static void OnHit(Mission m, Agent victim)
        {
            var c = Settings.Current;
            if (c == null || !c.BattleCriesAndMoans || victim == null || m == null || !BattleSense.InBattle(m)) return;
            if (!victim.IsHuman || victim.IsMainAgent || !victim.IsAIControlled) return;
            float lim = victim.HealthLimit, hp = victim.Health;
            if (lim <= 0f || hp < 1f || hp / lim >= MoanBelow) return;
            lock (_pend) { if (_pending.Count < PendingMax) _pending.Add(victim); }
        }

        /// <summary>Miejsce w limicie glosow na sekunde (pierscien 8 sekund; planujemy najwyzej 5 s naprzod).</summary>
        private static bool TakeSlot(float t)
        {
            int s = (int)Math.Floor(t);
            int k = s & 7;
            if (_bucketSec[k] != s) { _bucketSec[k] = s; _bucketN[k] = 0; }
            if (_bucketN[k] >= MaxPerSecond) return false;
            _bucketN[k]++;
            return true;
        }

        private static bool CanVoice(Agent a, float now, float cooldown)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || !a.IsAIControlled || a.IsMainAgent || a.IsRunningAway) return false;
            if (!MissionTeams.Valid(a.Team) || Undead.Character(a.Character)) return false;
            float lv;
            if (_lastVoice.TryGetValue(a, out lv) && now - lv < cooldown) return false;
            return true;
        }

        internal static void Tick(Mission m, float dt)
        {
            var c = Settings.Current;
            if (c == null || !c.BattleCriesAndMoans || m == null) return;
            _acc += dt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            if (_m == null || !ReferenceEquals(_m.Target, m)) { Clear(); _m = new WeakReference(m); }
            if (m.MissionEnded || m.IsNavalBattle || m.IsNavalRaidBattle || !BattleSense.InBattle(m)) return;   // takze wypad morski (recenzja 16.10)
            float now = m.CurrentTime;

            // ranni z kolejki trafien
            lock (_pend)
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    var a = _pending[i];
                    if (a == null || _hurtSet.Contains(a)) continue;
                    int done;
                    if (_moanCount.TryGetValue(a, out done) && done >= MoansPerMan) continue;   // swoje juz wyjeczal w tej bitwie
                    _hurtSet.Add(a);
                    _hurt.Add(new Hurt { A = a, Next = now + MoanFirstMin + MBRandom.RandomFloat * MoanFirstSpan });
                }
                _pending.Clear();
            }

            if (m.IsDeploymentFinished)
            {
                try { Charges(m, now); }
                catch (Exception e) { _buf.Clear(); RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Szarze", e); }
            }
            try { Moans(m, now); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Jeki", e); }

            if (_tCharges + _tYells + _tMoans + _tLimited > 0)
            {
                lock (RoutHold.Gate)
                {
                    RoutHold.EnsureLocked(m);
                    _charges += _tCharges; _yells += _tYells; _moans += _tMoans; _limited += _tLimited;
                    for (int i = 0; i < _tYellIds.Count; i++) _yellPeople.Add(_tYellIds[i]);
                    for (int i = 0; i < _tMoanIds.Count; i++) _moanPeople.Add(_tMoanIds[i]);
                }
                _tCharges = _tYells = _tMoans = _tLimited = 0;
                _tYellIds.Clear();
                _tMoanIds.Clear();
            }
        }

        private static void Charges(Mission m, float now)
        {
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
                    bool charge = oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget;
                    Form st;
                    if (!_forms.TryGetValue(f, out st)) { st = new Form(); _forms[f] = st; }
                    if (charge && !st.Charge)
                    {
                        st.Charge = true;
                        if (f.IsAIControlled && now - st.LastCry >= FormationCooldown && now - st.ChargeEnd >= ChargeGap)
                        {
                            st.LastCry = now;
                            Cry(f, now);
                        }
                    }
                    else if (!charge && st.Charge)
                    {
                        st.Charge = false;
                        st.ChargeEnd = now;
                    }
                }
            }
        }

        private static void Cry(Formation f, float now)
        {
            _buf.Clear();
            f.ApplyActionOnEachUnit(_collect);                    // gra iteruje swoje listy - my tylko zbieramy
            Agent cap = f.Captain;
            float lb = 0f;
            try
            {
                if (cap != null && cap.Character != null)
                    lb = Math.Min(LeaderMax, LeaderPer50 * cap.Character.GetSkillValue(DefaultSkills.Leadership) / 50f);
            }
            catch { lb = 0f; }
            _tCharges++;
            if (cap != null && CanVoice(cap, now, ManCooldown)) Yell(cap, now, 0.1f + MBRandom.RandomFloat * 0.3f);
            for (int k = 0; k < _buf.Count; k++)
            {
                var a = _buf[k];
                if (a == cap || !CanVoice(a, now, ManCooldown)) continue;
                int tier = BattleWind.TierOf(a.Character);
                float mor = a.GetMorale();
                float mf = mor < 0f ? 1f : Math.Max(0.25f, Math.Min(1.5f, mor / 50f));
                float p = Math.Min(ShareMax, (BaseShare + PerTier * tier) * mf + lb);
                if (MBRandom.RandomFloat >= p) continue;
                Yell(a, now, DelayMin + MBRandom.RandomFloat * DelaySpan);
            }
            _buf.Clear();
        }

        private static void Yell(Agent a, float now, float delay)
        {
            float d = delay;
            bool ok = false;
            for (int tries = 0; tries < 3; tries++)
            {
                if (TakeSlot(now + d)) { ok = true; break; }
                d += 1f;
            }
            if (!ok) { _tLimited++; return; }
            try
            {
                a.YellAfterDelay(d);                              // natywne, jednorazowe (jak YellingBehaviour gry)
                _lastVoice[a] = now + d;
                _tYells++;
                _tYellIds.Add(a.Index);
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Okrzyk", e); }
        }

        private static void Moans(Mission m, float now)
        {
            var main = m.MainAgent;
            bool listener = main != null && main.IsActive();
            Vec3 mp = listener ? main.Position : Vec3.Zero;
            bool dead = false;
            for (int i = 0; i < _hurt.Count; i++)
            {
                var h = _hurt[i];
                var a = h.A;
                try
                {
                    if (a == null || !a.IsActive()) { h.Dead = true; dead = true; continue; }
                    float lim = a.HealthLimit;
                    if (lim <= 0f || a.Health / lim >= MoanBelow) { h.Dead = true; dead = true; continue; }
                    if (now < h.Next) continue;
                    if (!listener || !CanVoice(a, now, VoiceGap)) { h.Next = now + 2f; continue; }
                    if (a.Position.DistanceSquared(mp) > MoanRange * MoanRange) { h.Next = now + 2f; continue; }
                    if (!TakeSlot(now)) { h.Next = now + 1f; _tLimited++; continue; }
                    a.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                    _lastVoice[a] = now;
                    _tMoans++;
                    _tMoanIds.Add(a.Index);
                    int n;
                    _moanCount.TryGetValue(a, out n);
                    n++;
                    _moanCount[a] = n;
                    if (n >= MoansPerMan) { h.Dead = true; dead = true; continue; }   // trzeci jek - koniec dla tego rannego w tej bitwie
                    h.Next = now + (n == 1 ? MoanMin + MBRandom.RandomFloat * MoanSpan : MoanLateMin + MBRandom.RandomFloat * MoanLateSpan);
                }
                catch (Exception e)
                {
                    h.Dead = true;
                    dead = true;
                    RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Jek", e);
                }
            }
            if (dead)
            {
                for (int i = 0; i < _hurt.Count; i++) if (_hurt[i].Dead && _hurt[i].A != null) _hurtSet.Remove(_hurt[i].A);
                _hurt.RemoveAll(x => x.Dead);
            }
        }
    }
}
