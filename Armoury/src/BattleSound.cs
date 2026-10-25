using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Scene = TaleWorlds.Engine.Scene;
using SoundEvent = TaleWorlds.Engine.SoundEvent;
using SoundManager = TaleWorlds.Engine.SoundManager;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2 "dzwiek" (projekt dzwiek-glosy.json + krytyk K4/K7/K8; klucze BattleSoundsEnabled - glowny, BattleHornsCulture, BattleDrumsMarch, BattleCaptainOrders,
    /// BattleHouseSpeeches, BattleChargeMottos, BattleWoundedAfter, BattleRavensAfter, BattleWindAndRain, BattleHorseSounds, BattleClashOfArms, SiegeFireSounds,
    /// BattleSoundsLoudness - wszystkie czytane na zywo): bebny marszu, rogi i trabki kultur, rozkazy kapitanow, przemowy rodow, motta wodzow przy szarzy,
    /// okrzyki choragwi, jeki rannych i kruki po bitwie, tlo pola (wiatr, deszcz, konie, szczek broni, ogien oblezenia).
    ///
    /// ODTWARZANIE (v1 BEZ project.mbproj i module_sounds.xml - zly plik silnika = zly start gry): pliki Armoury/ModuleData/Lektor/Bitwa/&lt;kategoria&gt;/*.ogg
    /// (manifest.tsv P2a; bez manifestu - przeglad katalogow) przez SoundEvent.CreateEventFromExternalFile("event:/Extra/voiceover", plik, scena, is3d, false) - ta sama
    /// droga co Lektor (2D, scena null) i gra w misji (SandBox EavesdroppingMissionLogic: scena misji, is3d true; CampaignMissionComponent: SetPosition przed Play).
    /// 3D: scena misji + SetPosition + SetEventMinMaxDistance(min, max, 0) (wzor ROTBA VoicePlayer) przed Play. Czy silnik tlumi zdarzenie voiceover z odlegloscia -
    /// NIESPRAWDZONE w grze (dowod: linia "zdarzenie 3D" raz na proces z GetEventMinMaxDistance przed / po ustawieniu i odsluch Jeffa). Glosnosci zdarzenia z pliku
    /// nie da sie ustawic - suwak BattleSoundsLoudness skaluje odleglosci min / max (1: x0.7, 2: x1.0, 3: x1.4), a dzwiekow dalej niz max + 50 m od sluchacza
    /// (kamera, SoundManager.GetListenerFrame) nie tworzymy wcale. Tlo (wiatr, deszcz) - 2D jak Lektor.
    ///
    /// PULA: kazde zdarzenie w _live; koniec (silnik: !IsValid / IsStopped po 0,3 s, albo czas klipu + 2 s czasu misji, albo + 60 s czasu rzeczywistego) -> Release;
    /// OnClearScene i End -> Release WSZYSTKICH. Nigdy zdarzenie z pustej sciezki / bez sceny dla 3D. Limity (stale): glosy 6, rogi 2, bebny 6, tlo 6 naraz; najwyzej
    /// 5 nowych zdarzen na sekunde czasu rzeczywistego; kazdy odstep liczony w czasie misji ORAZ w czasie rzeczywistym (co najmniej polowa odstepu) - przy
    /// przyspieszeniu czasu (autotest -Speed 20, Better Time) bez lawiny. Wszystko tylko w Tick (glowny watek): zdarzenia innych paczek i wywolania zwrotne
    /// silnika (OnShoot / OnMissileHit / OnRemoved) - tylko kolejka pod lockiem, O(1).
    ///
    /// KONTRAKT (sygnatury P0): ApplyMissionPatches - v1 bez latek (krytyk K8: rogi gry zostaja); Begin (AfterStart, po ForeignVoices.Detect w BattleCries.Begin);
    /// Tick (po BattleCries -> MarchToContact -> Doctrine, K4); OnDeploymentFinished (przemowy +3 s); OnShoot / OnMissileHit / OnPanicked / OnFleeing / OnRemoved
    /// (kolejki); OnClearScene i End (Release wszystkich; End pisze linie misji raz). Zdarzenia: OnChargeStarted (jedyny sygnal szarzy z BattleCries, K4) - decyzja
    /// motto / rozkaz od razu (TakeYellLead dla okrzyku ludzi), dzwiek w Tick tej samej klatki; OnStandardFell / OnCommanderFell / OnCompanyBroke (P3);
    /// OnArchersFellBack / OnFlankRide (P1).
    /// Gracz (MainAgent) nigdy nie mowi; kobiety nie mowia naszymi meskimi glosami; Inni (whitewalker) i bandyci - cisza (poza jekami i ptakami).
    /// Bitwy morskie, wypady morskie, kryjowki - nic (linia misji "pominieta"). Turniej / arena / trening - BattleSense nie wola (pojedynek / nie bitwa).
    /// Wyjatki liczone (RoutHold.Stumble, miejsce "dzwiek"), bez wylacznika. Linia "Dzwiek bitwy (noc-p2): ..." raz na misje (wlasna, nie w NB2) i linia katalogu
    /// raz na proces; grupa progow nocdzwiek w tools/sprawdz_logi.py. Autotest (LogGate 1/2 + plik CrashScribe/armoury-dzwiek-bitwy.flag): probka po jednym klipie
    /// kazdej kategorii przy sluchaczu z linia na klip (BattleSound.Test.cs).
    /// </summary>
    internal static partial class BattleSound
    {
        internal const string P = "Dzwiek bitwy (noc-p2): ";
        internal const string VoiceEvent = "event:/Extra/voiceover";
        internal const float YellLeadMax = 2.5f;          // najdluzsze czekanie okrzyku ludzi na glos dowodcy (BattleCries)
        internal const float OrderLead = 0.4f;            // okrzyk ludzi 0,4 s po "Charge!" kapitana (krytyk K4)
        private const float TickEvery = 0.25f;
        private const float RealShare = 0.5f;             // odstep w czasie rzeczywistym = polowa odstepu w czasie misji
        private const float FarExtra = 50f;               // dalej niz max + 50 m od sluchacza - nie tworzymy
        private const int MaxNewPerSec = 5;
        private const int QueueMax = 256;

        // grupy puli i limity naraz
        internal const int GVoice = 0, GHorn = 1, GDrum = 2, GAmb = 3, Groups = 4;
        private static readonly int[] GroupCap = { 6, 2, 6, 6 };
        private static readonly string[] GroupName = { "glosy", "rogi", "bebny", "tlo" };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- zdarzenie w puli
        private sealed class Snd
        {
            internal SoundEvent Ev;
            internal Clip C;
            internal int Group;
            internal string Tag;
            internal float T0;
            internal double R0;
            internal bool Checked, Is3d, Test;
            internal Agent Follow;            // glos idzie za mowca (do jego smierci - potem gra w miejscu)
            internal Formation FollowF;       // beben / tetent idzie za szykiem
            internal Vec3 Pos;
            internal Clip Then;               // nastepny klip tego samego mowcy (przemowa regionu -> motto rodu)
            internal float ThenMin, ThenMax;
        }

        private static readonly List<Snd> _live = new List<Snd>();
        private static readonly List<Snd> _ended = new List<Snd>();

        // ---- stan misji (glowny watek)
        private static WeakReference _m;
        private static bool _on, _lineDone, _siege, _hideout, _naval;
        private static string _skip;                      // powod pominiecia misji (morska, kryjowka, klucz NIE) - linia
        private static string _kind = "?", _scene = "?";
        private static float _acc, _now, _beganAt;
        private static Vec3 _lis;
        private static bool _lisOk;
        private static readonly int[] _secStamp = { -1 };
        private static int _secCount;

        // ---- liczniki misji
        private static int _created, _played05, _notPlayed, _released, _early, _endCap, _rejLimit, _rejFar, _rejNoClip, _rejEngine, _rejRate, _peak;
        private static readonly int[] _byGroup = new int[Groups];
        private static readonly int[] _rejGroup = new int[Groups];

        // ---- czas
        private static double Real { get { return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; } }

        /// <summary>Odstep liczony w czasie misji ORAZ w czasie rzeczywistym (co najmniej RealShare odstepu) - przyspieszenie czasu bez lawiny.</summary>
        private struct Stamp
        {
            internal float M;
            internal double R;
            internal bool Any;
            internal bool Ready(float gap) { return !Any || (_now - M >= gap && Real - R >= gap * RealShare); }
            internal void Hit() { M = _now; R = Real; Any = true; }
        }

        private static Settings On()
        {
            var c = Settings.Current;
            return c != null && c.BattleSoundsEnabled && _on ? c : null;
        }

        private static float LoudScale(Settings c)
        {
            int v = c != null ? c.BattleSoundsLoudness : 2;
            return v <= 1 ? 0.7f : v >= 3 ? 1.4f : 1f;
        }

        // ============================================================ kontrakt P0

        /// <summary>v1 bez latek: rogi gry (TacticComponent.SoundTacticalHorn) zostaja - krytyk K8 (ryzyko wklejenia malej metody i podwojnego rogu).</summary>
        internal static string ApplyMissionPatches(Harmony h) { return "bez latek (v1: rogi gry bez zmian, K8)"; }

        /// <summary>AfterStart bitwy (BattleSense, po BattleCries.Begin - ForeignVoices juz wykryte). Stan od zera; klucz NIE / morska / kryjowka - nic poza linia.</summary>
        internal static void Begin(Mission m)
        {
            try { BattleCries.ResetCheer(); } catch { }
            if (_m != null && !ReferenceEquals(_m.Target, m)) ReleaseAll("nowa misja");
            ResetMission(m);
            var c = Settings.Current;
            if (m == null) return;
            try { _scene = m.SceneName ?? "?"; } catch { _scene = "?"; }
            try { _kind = MissionTeams.Kind(m); } catch { _kind = "?"; }
            _naval = m.IsNavalBattle || m.IsNavalRaidBattle;
            _hideout = false;
            try { _hideout = HideoutAlarm.IsHideout(m); } catch { }
            _siege = m.IsSiegeBattle;
            if (c == null || !c.BattleSoundsEnabled) { _skip = "klucz BattleSoundsEnabled NIE"; return; }
            if (_naval) { _skip = "bitwa morska"; return; }
            if (_hideout) { _skip = "kryjowka"; return; }
            EnsureCatalog();
            _on = true;
            _beganAt = m.CurrentTime;
            _now = _beganAt;
            BeginFeatures(m, c);
        }

        private static void ResetMission(Mission m)
        {
            _m = m != null ? new WeakReference(m) : null;
            _on = false; _lineDone = false; _skip = null; _siege = _hideout = _naval = false;
            _acc = 0f; _now = 0f; _beganAt = 0f; _lisOk = false;
            _secStamp[0] = -1; _secCount = 0;
            _created = _played05 = _notPlayed = _released = _early = _endCap = _rejLimit = _rejFar = _rejNoClip = _rejEngine = _rejRate = _peak = 0;
            for (int i = 0; i < Groups; i++) { _byGroup[i] = 0; _rejGroup[i] = 0; }
            ResetFeatures();
        }

        /// <summary>OnMissionTick (BattleSense, po BattleCries / MarchToContact / Doctrine). Jedyne miejsce CreateEvent / Play / Release.</summary>
        internal static void Tick(Mission m, float dt)
        {
            if (!_on || m == null) return;
            if (_m == null || !ReferenceEquals(_m.Target, m)) return;   // inna misja - Begin jej nie zaczal
            var c = Settings.Current;
            _now = m.CurrentTime;
            if (c == null || !c.BattleSoundsEnabled)
            {
                if (_live.Count > 0) ReleaseAll("klucz NIE w bitwie");
                return;
            }
            // sygnaly szarzy - w tej samej klatce (okrzyk ludzi czeka 0,4 s na rozkaz)
            if (HasChargeQueue())
            {
                Listener(m);
                try { DrainCharges(m, c); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Szarza", e); }
            }
            _acc += dt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            Listener(m);
            try { Pool(m); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Pula", e); }
            TickFeatures(m, c);
        }

        internal static void OnDeploymentFinished(Mission m)
        {
            if (!_on || m == null || _m == null || !ReferenceEquals(_m.Target, m)) return;
            DeploymentDone(m);
        }

        /// <summary>Strzal (bardzo czeste, moze przyjsc z ticku asynchronicznego): tylko szyk strzelca do zbioru pod lockiem.</summary>
        internal static void OnShoot(Mission m, Agent shooter, EquipmentIndex weaponIndex)
        {
            if (!_on || shooter == null || shooter.IsMainAgent) return;
            var f = shooter.Formation;
            if (f == null) return;
            lock (_q) { if (_shotQ.Count < QueueMax) _shotQ.Add(f); }
        }

        /// <summary>Trafienie pociskiem (czeste): tylko szyk trafionego do zbioru pod lockiem.</summary>
        internal static void OnMissileHit(Mission m, Agent attacker, Agent victim, bool isCanceled)
        {
            if (!_on || isCanceled || victim == null) return;
            var f = victim.Formation;
            if (f == null) return;
            lock (_q) { if (_hitQ.Count < QueueMax) _hitQ.Add(f); }
        }

        /// <summary>v1: panika i ucieczka bez dzwieku (zlamanie kompanii daje P3 - OnCompanyBroke; okrzyki paniki ma gra i ROTBA).</summary>
        internal static void OnPanicked(Mission m, Agent a) { }

        internal static void OnFleeing(Mission m, Agent a) { }

        /// <summary>Zejscie agenta (zgon / nokaut): miejsce ciala do kolejki (jeki rannych i kruki po bitwie, ranny kon).</summary>
        internal static void OnRemoved(Mission m, Agent affected, Agent affector, AgentState state)
        {
            if (!_on || affected == null) return;
            if (state != AgentState.Killed && state != AgentState.Unconscious) return;
            Vec3 p;
            bool mount, human;
            try { p = affected.Position; mount = affected.IsMount; human = affected.IsHuman; }
            catch { return; }
            if (!mount && !human) return;
            lock (_q) { if (_bodyQ.Count < QueueMax) _bodyQ.Add(new Body { Pos = p, Unconscious = state == AgentState.Unconscious, Mount = mount, At = _now }); }
        }

        /// <summary>Przed zniszczeniem sceny (reset misji / koniec): Release wszystkich zdarzen; bez linii.</summary>
        internal static void OnClearScene(Mission m)
        {
            if (_live.Count > 0) ReleaseAll("czyszczenie sceny");
        }

        /// <summary>Koniec misji (OnEndMission / OnRemoveBehavior - bezpieczne wielokrotnie): Release wszystkich, linia misji raz, stan od zera.</summary>
        internal static void End(Mission m)
        {
            if (_m == null || m == null || !ReferenceEquals(_m.Target, m))
            {
                if (_live.Count > 0) ReleaseAll("koniec innej misji");   // poprzednia bitwa bez wlasnego End - sprzatanie (Release niewaznego zdarzenia nic nie robi)
                return;
            }
            int activeEnd = _live.Count;   // grajace jeszcze w chwili konca - zwolnione tutaj
            ReleaseAll("koniec misji");
            try { FinishFeatures(); } catch { }
            if (!_lineDone)
            {
                _lineDone = true;
                try { WriteLine(m, activeEnd); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Linia", e); }
            }
            _on = false;
            ClearFeatures();
            _m = null;
        }

        // ============================================================ pula zdarzen

        private static void Listener(Mission m)
        {
            try
            {
                var fr = SoundManager.GetListenerFrame();
                _lis = fr.origin;
                _lisOk = true;
                if (_lis.x == 0f && _lis.y == 0f && _lis.z == 0f)
                {
                    var a = m.MainAgent;
                    if (a != null && a.IsActive()) _lis = a.Position;
                }
            }
            catch
            {
                _lisOk = false;
                var a = m.MainAgent;
                if (a != null && a.IsActive()) { _lis = a.Position; _lisOk = true; }
            }
        }

        private static float LisDist(Vec3 p) { return _lisOk ? p.Distance(_lis) : 0f; }

        private static int LiveIn(int group)
        {
            int n = 0;
            for (int i = 0; i < _live.Count; i++) if (_live[i].Group == group) n++;
            return n;
        }

        /// <summary>Miejsce w limicie nowych zdarzen na sekunde czasu rzeczywistego.</summary>
        private static bool RateOk()
        {
            int s = (int)Math.Floor(Real);
            if (_secStamp[0] != s) { _secStamp[0] = s; _secCount = 0; }
            return _secCount < MaxNewPerSec;
        }

        /// <summary>Nowe zdarzenie: limity, odleglosc od sluchacza, CreateEventFromExternalFile, pozycja i odleglosci 3D, Play. null = cisza (licznik).</summary>
        private static Snd Play(Mission m, Clip clip, int group, Vec3 pos, float min, float max, bool threeD, Agent follow, Formation ff, string tag)
        {
            if (clip == null) { _rejNoClip++; return null; }
            if (LiveIn(group) >= GroupCap[group]) { _rejLimit++; _rejGroup[group]++; return null; }
            if (!RateOk()) { _rejRate++; _rejGroup[group]++; return null; }
            var c = Settings.Current;
            float k = LoudScale(c);
            min *= k; max *= k;
            Scene scene = null;
            if (threeD)
            {
                if (LisDist(pos) > max + FarExtra) { _rejFar++; return null; }
                scene = m != null ? m.Scene : null;
                if (scene == null) { _rejEngine++; return null; }
            }
            SoundEvent ev = null;
            try { ev = SoundEvent.CreateEventFromExternalFile(VoiceEvent, clip.Path, scene, threeD, false); }
            catch (Exception e) { _rejEngine++; RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Create", e); return null; }
            bool ok = false;
            try
            {
                if (ev != null && ev.IsValid)
                {
                    if (threeD)
                    {
                        Vec3 before = Vec3.Zero;
                        bool probe = !_probed;
                        if (probe) { try { before = ev.GetEventMinMaxDistance(); } catch { } }
                        ev.SetPosition(pos);
                        ev.SetEventMinMaxDistance(new Vec3(min, max, 0f));
                        if (probe) Probe3D(before, ev, min, max);
                    }
                    ok = ev.Play();
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Play", e); ok = false; }
            if (!ok)
            {
                _rejEngine++;
                try { if (ev != null && ev.IsValid) ev.Release(); } catch { }
                return null;
            }
            _secCount++;
            var s = new Snd { Ev = ev, C = clip, Group = group, Tag = tag, T0 = _now, R0 = Real, Is3d = threeD, Follow = follow, FollowF = ff, Pos = pos };
            _live.Add(s);
            _created++;
            _byGroup[group]++;
            if (_live.Count > _peak) _peak = _live.Count;
            return s;
        }

        // pierwsze zdarzenie 3D w procesie: odleglosci silnika przed i po ustawieniu (dowod, ze zdarzenie ma przestrzen 3D)
        private static bool _probed;

        private static void Probe3D(Vec3 before, SoundEvent ev, float min, float max)
        {
            _probed = true;
            Vec3 after = Vec3.Zero;
            try { after = ev.GetEventMinMaxDistance(); } catch { }
            Log.Info(P + "zdarzenie 3D (pierwsze w procesie, " + VoiceEvent + ", scena misji, is3d tak): GetEventMinMaxDistance przed ustawieniem "
                     + F1(before.x) + "/" + F1(before.y) + ", po SetEventMinMaxDistance(" + F1(min) + ", " + F1(max) + ") " + F1(after.x) + "/" + F1(after.y)
                     + " - czy silnik tlumi z odlegloscia: odsluch (0/0 przed = zdarzenie bez przestrzeni w banku?).");
        }

        private static string F1(float v) { return v.ToString("0.0", Inv); }

        /// <summary>Takt puli (0,25 s): pozycja za mowca / szykiem, kontrola "grane po 0,5 s", koniec -> Release, nastepny klip mowcy.</summary>
        private static void Pool(Mission m)
        {
            double rnow = Real;
            for (int i = 0; i < _live.Count; i++)
            {
                var s = _live[i];
                bool ended = false;
                float em = _now - s.T0;
                double er = rnow - s.R0;
                try
                {
                    if (s.Is3d)
                    {
                        if (s.Follow != null)
                        {
                            if (s.Follow.IsActive()) { s.Pos = s.Follow.Position + new Vec3(0f, 0f, 1.6f); s.Ev.SetPosition(s.Pos); }
                            else s.Follow = null;   // zginal w trakcie - glos gra do konca w miejscu
                        }
                        else if (s.FollowF != null)
                        {
                            if (s.FollowF.CountOfUnits > 0) { s.Pos = s.FollowF.CachedMedianPosition.GetNavMeshVec3() + new Vec3(0f, 0f, 1.5f); s.Ev.SetPosition(s.Pos); }
                            else s.FollowF = null;
                        }
                    }
                    if (!s.Checked && er >= 0.5 && em >= 0.3f)
                    {
                        s.Checked = true;
                        bool playing = s.Ev.IsValid && s.Ev.IsPlaying();
                        if (playing) _played05++; else _notPlayed++;
                        if (s.Test) TestChecked(s, playing);
                    }
                    if (!s.Ev.IsValid) ended = true;
                    else if (er >= 0.3 && em >= 0.25f && s.Ev.IsStopped()) ended = true;
                    else if (em >= s.C.Dur + 2f) { ended = true; _endCap++; }
                    else if (er >= s.C.Dur + 60.0) { ended = true; _endCap++; }
                }
                catch (Exception e) { ended = true; RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Pula.Klip", e); }
                if (ended)
                {
                    if (s.Checked && s.C.Dur >= 1.5f && em < 0.6f * s.C.Dur) _early++;   // przerwane przez silnik (np. limit wystapien zdarzenia)
                    _ended.Add(s);
                }
            }
            if (_ended.Count == 0) return;
            for (int i = 0; i < _ended.Count; i++)
            {
                var s = _ended[i];
                _live.Remove(s);
                ReleaseOne(s);
                if (s.Test) TestEnded(s, _now - s.T0);
                if (s.Then != null && s.Follow != null) QueueThen(s);
                OnSndEnded(s);
            }
            _ended.Clear();
        }

        private static void ReleaseOne(Snd s)
        {
            try { if (s.Ev != null && s.Ev.IsValid) s.Ev.Release(); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Release", e); }
            _released++;
        }

        /// <summary>Zatrzymanie jednego zdarzenia (beben przy szarzy) - Release jak w grze.</summary>
        private static void Cut(Snd s)
        {
            if (s == null || !_live.Remove(s)) return;
            ReleaseOne(s);
            OnSndEnded(s);
        }

        private static void ReleaseAll(string why)
        {
            for (int i = 0; i < _live.Count; i++) ReleaseOne(_live[i]);
            _live.Clear();
            _ended.Clear();
        }

        internal static bool IsSpeaking(Agent a)
        {
            if (a == null) return false;
            for (int i = 0; i < _live.Count; i++) if (_live[i].Follow == a && _live[i].Group == GVoice) return true;
            return false;
        }

        // ============================================================ linia misji

        private static void WriteLine(Mission m, int activeEnd)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(1400);
            sb.Append(P).Append("misja ").Append(_kind).Append(" (scena ").Append(_scene).Append(", oblezenie ").Append(_siege ? "TAK" : "NIE");
            if (_skip != null)
            {
                sb.Append(") - pominieta (").Append(_skip).Append(") | wiwaty P3: wywolan ").Append(BattleCries.CheerCalls).Append(", okrzykow ").Append(BattleCries.CheerYells)
                  .Append(" | potkniecia ").Append(Stumbles()).Append('.');
                Log.Info(sb.ToString());
                return;
            }
            sb.Append(", glosnosc ").Append(c != null ? c.BattleSoundsLoudness : 2).Append(", katalog klipow ").Append(_clips != null ? _clips.Count : 0).Append(')');
            sb.Append(" | zdarzen utworzonych ").Append(_created).Append(" (glosy ").Append(_byGroup[GVoice]).Append(", rogi ").Append(_byGroup[GHorn])
              .Append(", bebny ").Append(_byGroup[GDrum]).Append(", tlo ").Append(_byGroup[GAmb]).Append("), grane po 0,5 s ").Append(_played05)
              .Append(" (nie grane ").Append(_notPlayed).Append("), zwolnionych ").Append(_released).Append(" (przy koncu misji ").Append(activeEnd).Append("), aktywnych po koncu ").Append(_live.Count)
              .Append(", najwiecej naraz ").Append(_peak).Append(", przerwane wczesnie ").Append(_early).Append(", koniec po czasie ").Append(_endCap)
              .Append("; odrzucone: limit ").Append(_rejLimit).Append(", na sekunde ").Append(_rejRate).Append(", daleko ").Append(_rejFar)
              .Append(", brak klipu ").Append(_rejNoClip).Append(", silnik ").Append(_rejEngine);
            AppendFeatures(sb, c);
            sb.Append(" | wiwaty P3: wywolan ").Append(BattleCries.CheerCalls).Append(", okrzykow ").Append(BattleCries.CheerYells).Append(", bez szyku ").Append(BattleCries.CheerNoFormation);
            sb.Append(" | obce: ");
            ForeignVoices.AppendHorns(sb);
            sb.Append(" | potkniecia ").Append(Stumbles()).Append('.');
            Log.Info(sb.ToString());
        }

        private static int Stumbles()
        {
            try { lock (RoutHold.Gate) { return RoutHold.StumblesLocked(RoutHold.SiteSound); } }
            catch { return -1; }
        }
    }
}
