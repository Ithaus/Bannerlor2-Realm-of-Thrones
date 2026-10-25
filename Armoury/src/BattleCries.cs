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
    ///  - 16.10: ustepuja obcym modom glosow (ForeignVoices: Battle Atmosphere, Immersive Battlefields) - szyk AI pelny / tylko kapitan / nic,
    ///    szyk gracza tylko wtedy, gdy IB wycial natywny okrzyk, a nie ma choru BA (np. kryjowka);
    ///  - jek: AI ponizej 30% zdrowia (zapisany przy trafieniu, nie w ucieczce), do 60 m od gracza: MakeVoice(Pain) najwyzej 3 razy na rannego na
    ///    bitwe - pierwszy 3-7 s po ciosie, drugi 7-14 s pozniej, trzeci 20-30 s pozniej (poprawka 16.10: dawniej co 7-14 s do konca bitwy);
    ///  - bez bitew morskich i wypadow morskich (IsNavalBattle, IsNavalRaidBattle - poprawka 16.10);
    ///  - jeden glos na czlowieka na 4 s (okrzyk: 20 s), najwyzej 6 glosow na sekunde w misji (okrzyk odlozony najwyzej 2 x o 1 s, potem odrzucony
    ///    i liczony) - zadnych powtorek w kolko;
    ///  - ludzi szyku zbieramy do wlasnej listy (ApplyActionOnEachUnit tylko dodaje do listy), natywne glosy dopiero po zakonczeniu iteracji gry;
    ///  - umarli, gracz, uciekajacy (IsRunningAway - zlamani maja swoj jeden okrzyk paniki), bez waznej druzyny - bez glosu;
    ///  - wyjatek liczony (RoutHold.Stumble, miejsce "glosy"), funkcja dalej dziala.
    /// noc 25.10 P2 (krytyk K4): BattleCries jest JEDYNYM detektorem szarzy - przejscie KAZDEGO szyku (AI i gracza) na Charge / ChargeToTarget po rozstawieniu
    /// wola ChargeStarted(f) (BattleCries.Events.cs: marsz P1 zdejmuje cap, dzwiek P2 gra rog / motto / rozkaz), takze przy BattleCriesAndMoans NIE (wtedy sam
    /// sygnal, bez okrzyku i bez jekow) - detektor biegnie, gdy wlaczony okrzyk albo marsz (MarchToContactEnabled) albo dzwiek (BattleSoundsEnabled).
    /// Okrzyk ludzi czeka na glos dowodcy: BattleSound.TakeYellLead(f) - 0.4 s po rozkazie "Charge!", do 2.5 s po motcie rodu (inaczej 0).
    /// Cheer(team, pos) - wiwat zwyciezcow przy zlamaniu wrogiej kompanii (wola P3): najblizszy szyk tej druzyny do 60 m od miejsca, czesc ludzi natywnym Yell,
    /// ten sam limit glosow na sekunde; najwyzej raz na druzyne na 8 s, najwyzej 12 glosow.
    /// </summary>
    internal static partial class BattleCries   // noc 25.10 P0: partial - sygnal szarzy ChargeStarted w BattleCries.Events.cs (plik integratora)
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
            ForeignVoices.Append(sb);   // 16.10: ustapienie Battle Atmosphere / Immersive Battlefields
        }

        /// <summary>AfterStart (BattleSense): stan od zera dla nowej misji.</summary>
        internal static void Begin(Mission m)
        {
            Clear();
            _m = new WeakReference(m);
            ForeignVoices.Detect(m);   // 16.10: nasze okrzyki szarzy ustepuja BA / IB (tryb szyku AI i gracza)
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
            _cheerAt.Clear();   // noc 25.10 P2: bez obiektow Team po misji (liczniki wiwatow zeruje BattleSound.Begin)
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

        /// <summary>144 (134d): oddech zadyszki (WindedShow) - ten sam limiter co okrzyki i jeki: jeden glos na czlowieka na VoiceGap (4 s), najwyzej
        /// MaxPerSecond glosow na sekunde w misji. true = miejsce wziete (glos zapisany na czlowieka). Glowny watek.</summary>
        internal static bool TryBreathVoice(Agent a, float now)
        {
            if (!CanVoice(a, now, VoiceGap)) return false;
            if (!TakeSlot(now)) return false;
            _lastVoice[a] = now;
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
            if (c == null || m == null) return;
            // noc 25.10 P2 (K4): detektor szarzy biegnie takze dla marszu (P1) i dzwieku (P2); okrzyki i jeki tylko przy BattleCriesAndMoans
            bool cries = c.BattleCriesAndMoans;
            if (!cries && !c.MarchToContactEnabled && !c.BattleSoundsEnabled) return;
            _acc += dt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            if (_m == null || !ReferenceEquals(_m.Target, m)) { Clear(); _m = new WeakReference(m); }
            if (m.MissionEnded || m.IsNavalBattle || m.IsNavalRaidBattle || !BattleSense.InBattle(m)) return;   // takze wypad morski (recenzja 16.10)
            float now = m.CurrentTime;

            // ranni z kolejki trafien (OnHit zapisuje tylko przy BattleCriesAndMoans)
            lock (_pend)
            {
                for (int i = 0; cries && i < _pending.Count; i++)
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
                try { Charges(m, now, cries); }
                catch (Exception e) { _buf.Clear(); RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Szarze", e); }
            }
            if (cries)
            {
                try { Moans(m, now); }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Jeki", e); }
            }
            else if (_hurt.Count > 0) { _hurt.Clear(); _hurtSet.Clear(); }   // klucz wylaczony w bitwie - ranni z kolejki nie czekaja

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

        private static void Charges(Mission m, float now, bool cries)
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
                    // 128: szarza z daleka idzie marszem - sygnal (okrzyk, rog, motto, bieg) dopiero na progu biegu (MarchToContact.ChargeHeld)
                    bool held = false;
                    if (charge && !st.Charge)
                    {
                        try { held = MarchToContact.ChargeHeld(f); }
                        catch (Exception e) { held = false; RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.ChargeHeld", e); }
                    }
                    if (charge && !st.Charge && !held)
                    {
                        st.Charge = true;
                        // noc 25.10 P2 (K4): jedyny sygnal szarzy - kazde przejscie, AI i gracza, takze bez okrzykow (dyspozytor w BattleCries.Events.cs)
                        ChargeStarted(f);
                        float lead = 0f;
                        try { lead = BattleSound.TakeYellLead(f); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.TakeYellLead", e); }
                        int mode = f.IsAIControlled ? ForeignVoices.AiMode : ForeignVoices.PlayerMode;   // 16.10: obce glosy (ForeignVoices)
                        if (cries && mode != ForeignVoices.ModeNone && now - st.LastCry >= FormationCooldown && now - st.ChargeEnd >= ChargeGap)
                        {
                            st.LastCry = now;
                            Cry(f, now, mode == ForeignVoices.ModeCaptain, lead);
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

        /// <summary>lead (noc 25.10 P2): sekundy na glos dowodcy przed okrzykiem (0 - jak dotad; najwyzej BattleSound.YellLeadMax).</summary>
        private static void Cry(Formation f, float now, bool captainOnly, float lead)
        {
            if (lead < 0f || float.IsNaN(lead)) lead = 0f;
            if (lead > BattleSound.YellLeadMax) lead = BattleSound.YellLeadMax;
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
            // noc 25.10 P2: kapitan, ktory wlasnie mowi motto / rozkaz (BattleSound), nie krzyczy natywnie na nim
            bool capSpoke = lead > 0f && cap != null && BattleSound.Busy(cap);
            if (cap != null && !capSpoke && CanVoice(cap, now, ManCooldown)) Yell(cap, now, lead + 0.1f + MBRandom.RandomFloat * 0.3f);
            for (int k = 0; !captainOnly && k < _buf.Count; k++)
            {
                var a = _buf[k];
                if (a == cap || !CanVoice(a, now, ManCooldown)) continue;
                if (lead > 0f && BattleSound.Busy(a)) continue;   // noc 25.10 P2: mowca rozkazu (gdy nie kapitan) nie krzyczy na swoim glosie
                int tier = BattleWind.TierOf(a.Character);
                float mor = a.GetMorale();
                float mf = mor < 0f ? 1f : Math.Max(0.25f, Math.Min(1.5f, mor / 50f));
                float p = Math.Min(ShareMax, (BaseShare + PerTier * tier) * mf + lb);
                if (MBRandom.RandomFloat >= p) continue;
                Yell(a, now, lead + DelayMin + MBRandom.RandomFloat * DelaySpan);
            }
            _buf.Clear();
        }

        /// <summary>charge = okrzyk szarzy (liczniki NB2); false - wiwat (noc 25.10 P2: liczony osobno w linii dzwieku). true = glos zaplanowany.</summary>
        private static bool Yell(Agent a, float now, float delay, bool charge = true)
        {
            float d = delay;
            bool ok = false;
            for (int tries = 0; tries < 3; tries++)
            {
                if (TakeSlot(now + d)) { ok = true; break; }
                d += 1f;
            }
            if (!ok) { if (charge) _tLimited++; return false; }
            try
            {
                a.YellAfterDelay(d);                              // natywne, jednorazowe (jak YellingBehaviour gry)
                _lastVoice[a] = now + d;
                if (charge)
                {
                    _tYells++;
                    _tYellIds.Add(a.Index);
                }
                return true;
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteVoice, "BattleCries.Okrzyk", e); return false; }
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

        // noc 25.10 P0 (kontrakt K6, wlasciciel tresci P2): okrzyk zwyciezcow, gdy kompania wroga sie zalamie (wola P3 - BannerCompaniesMission).
        // team = druzyna, ktora wiwatuje; pos = srodek zlamanej kompanii (najblizsza formacja tej druzyny w 60 m, ta sama droga co Cry i ten sam limit glosow).
        // Sygnal szarzy ChargeStarted(f) jest w BattleCries.Events.cs (plik integratora): P2 go WOLA z Charges.
        internal const float CheerRange = 60f, CheerTeamGap = 8f, CheerShare = 0.35f;
        internal const int CheerMaxVoices = 12;
        private static readonly Dictionary<Team, float> _cheerAt = new Dictionary<Team, float>();
        // liczniki wiwatow (glowny watek) - do linii "Dzwiek bitwy" (BattleSound); zerowane w BattleSound.Begin (Clean misji wola Clear przed BattleSound.End)
        internal static int CheerCalls, CheerYells, CheerNoFormation;

        internal static void ResetCheer() { CheerCalls = CheerYells = CheerNoFormation = 0; _cheerAt.Clear(); }

        /// <summary>noc 25.10 P2: wiwat druzyny team przy miejscu zlamania wrogiej kompanii (glowny watek, z logiki misji P3). Klucz decyduje P3 (BannerVictoryCheer);
        /// tu tylko bitwa, ten sam limit glosow na sekunde co okrzyki, najwyzej raz na druzyne na 8 s i 12 glosow.</summary>
        internal static void Cheer(Team team, Vec3 pos)
        {
            var m = Mission.Current;
            if (m == null || team == null || !MissionTeams.Valid(team) || m.MissionEnded || m.IsNavalBattle || m.IsNavalRaidBattle || !BattleSense.InBattle(m)) return;
            float now = m.CurrentTime;
            float last;
            if (_cheerAt.TryGetValue(team, out last) && now - last < CheerTeamGap) return;
            CheerCalls++;
            _cheerAt[team] = now;
            Formation best = null;
            float bd = CheerRange * CheerRange;
            var fl = team.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            Vec2 p2 = pos.AsVec2;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || f.CountOfUnits <= 0) continue;
                float d = f.CachedAveragePosition.DistanceSquared(p2);
                if (d <= bd) { bd = d; best = f; }
            }
            if (best == null) { CheerNoFormation++; return; }
            _buf.Clear();
            best.ApplyActionOnEachUnit(_collect);
            int n = 0;
            for (int k = 0; k < _buf.Count && n < CheerMaxVoices; k++)
            {
                var a = _buf[k];
                if (!CanVoice(a, now, ManCooldown) || BattleSound.Busy(a)) continue;
                if (MBRandom.RandomFloat >= CheerShare) continue;
                if (Yell(a, now, DelayMin + MBRandom.RandomFloat * DelaySpan, false)) { n++; CheerYells++; }   // wiwat liczony osobno (linia dzwieku), nie w NB2
            }
            _buf.Clear();
        }
    }
}
