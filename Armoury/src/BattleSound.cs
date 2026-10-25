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
    /// okrzyki choragwi, jeki rannych i kruki po bitwie, tlo pola (wiatr, deszcz, konie, szczek broni, ogien oblezenia). P10 (klucze SiegeAssaultCrowd, SiegeDrums):
    /// tlum i krzyki szturmu murow, bebny oblezenia - BattleSound.Siege.cs.
    ///
    /// ODTWARZANIE (v1 BEZ project.mbproj i module_sounds.xml - zly plik silnika = zly start gry): pliki Armoury/ModuleData/Lektor/Bitwa/&lt;kategoria&gt;/*.ogg
    /// (manifest.tsv P2a; bez manifestu - przeglad katalogow) przez SoundEvent.CreateEventFromExternalFile(zdarzenie programisty, plik, scena, is3d, false).
    /// 154 (Jeff 25.10 22:45: "zrodlo jest glosniejsze jak jestem blisko a ciszej jak jestem dalej"): do P2 KAZDY klip szedl przez "event:/Extra/voiceover" - w banku gry
    /// zdarzenie 2D (voice.bank: na sciezce glownej brak spatializera FMOD - DSP typu 27 PAN; sound_event_data.gen.xml max_distance 100000), wiec SetPosition
    /// i SetEventMinMaxDistance nic nie dawaly - wszystko gralo przy uchu. Teraz 3D (EventFor wedlug kodu klipu) - zdarzenia szablonow module_sounds z Native mod.bank,
    /// kazde z instrumentem programisty (PRIB) i spatializerem na sciezce glownej (sprawdzone w banku 1.4.8):
    ///  - event:/mod/mission/voice_shout (max_distance 450, os 8 s): rozkazy, motta, przemowy, okrzyki choragwi i murow, rogi, konie, bebny marszu i oblezenia, ryk i wylom;
    ///  - event:/mod/mission/voice (80, os 4 s): jeki po bitwie do 4 s (dluzsze - voice_shout);
    ///  - event:/mod/mission/ambient_3d_big (100, zdarzenie trwale - dlugosc -1, instrument programisty w multi-instrumencie podpietym w naglowku zdarzenia, nie na osi): petle szczeku, ognia, tlumu szturmu
    ///    i zalogi taranu oraz ptaki; zdarzenie samo sie nie konczy (moze zapetlic klip), wiec Track tnie je na koncu klipu (czas rzeczywisty);
    ///  - klip dluzszy niz os zdarzenia -> ambient_3d_big: os instrumentu programisty mod/* jest zapisana inaczej niz w Extra/voiceover i Extra/external (te graja
    ///    klipy dluzsze niz os - Lektor 20-43 s na osi 5 s; mod/* - prawdopodobnie bez "async"), a opis kategorii w Native module_sounds.xml mowi "max 8 sec." /
    ///    "max 4 sec." - dluzszy klip bylby zapewne uciety na koncu osi (wniosek z banku, NIESPRAWDZONE w grze);
    ///  - event:/mod/mission/siege_loud NIE (choc ma instrument programisty i spatializer): ma drugi arkusz (parametr "Distance Siege 500", 0-500) z multi-instrumentem
    ///    dwoch probek ballista_break - prawdopodobnie dograliby sie do kazdego taktu bebna; bebny ida przez voice_shout;
    ///  - wiatr i deszcz - "event:/Extra/voiceover" 2D jak dotad (tlo calej sceny, jak Lektor).
    /// Zdarzenie 3D odrzucone przez silnik (id -1, Play false, wyjatek) - licznik i zapas: stara droga voiceover TYLKO dla tego klipu (cisza nie bez sladu - linia misji).
    /// 3D: scena misji + SetPosition + SetEventMinMaxDistance(min, max, 0) (wzor ROTBA VoicePlayer) przed Play; zrodla za mowca / szykiem - pozycja i predkosc co klatke
    /// (Track). Czy silnik stosuje nasze min / max (spatializer zdarzenia moze miec wlasny zakres) i jak glosno slychac - silnik NIE podaje; dowod tylko odsluchem
    /// (probka odleglosci autotestu 5 m / 150 m). Glosnosci zdarzenia z pliku nie da sie ustawic - suwak BattleSoundsLoudness skaluje odleglosci min / max
    /// (1: x0.7, 2: x1.0, 3: x1.4), a dzwiekow dalej niz max + 50 m od sluchacza (kamera, SoundManager.GetListenerFrame) nie tworzymy wcale.
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
    /// motto / rozkaz od razu, gdy jest miejsce w limitach (TakeYellLead dla okrzyku ludzi), dzwiek w Tick tej samej klatki; OnStandardFell / OnCommanderFell /
    /// OnCompanyBroke (P3); OnArchersFellBack / OnFlankRide (P1).
    /// Gracz (MainAgent) nigdy nie mowi; kobiety nie mowia naszymi meskimi glosami (takze bez jeku po bitwie); Inni (whitewalker) / umarli - cisza, takze bez jeku;
    /// bandyci - cisza poza jekami; ptaki nad wszystkimi cialami.
    /// Bitwy morskie, wypady morskie, kryjowki - nic (linia misji "pominieta"). Turniej / arena / trening - BattleSense nie wola (pojedynek / nie bitwa).
    /// Wyjatki liczone (RoutHold.Stumble, miejsce "dzwiek"), bez wylacznika. Linia "Dzwiek bitwy (noc-p2): ..." raz na misje (wlasna, nie w NB2) i linia katalogu
    /// raz na proces; grupa progow nocdzwiek w tools/sprawdz_logi.py. Autotest (LogGate 1/2 + plik CrashScribe/armoury-dzwiek-bitwy.flag): probka po jednym klipie
    /// kazdej kategorii przy sluchaczu z linia na klip (BattleSound.Test.cs).
    /// </summary>
    internal static partial class BattleSound
    {
        internal const string P = "Dzwiek bitwy (noc-p2): ";
        internal const string VoiceEvent = "event:/Extra/voiceover";   // 2D (wiatr, deszcz) i zapas po odmowie zdarzenia 3D
        // 154: zdarzenia 3D z banku gry (Native mod.bank / sound_event_data.gen.xml 1.4.8) - instrument programisty + spatializer na sciezce glownej
        internal const string Ev3dShout = "event:/mod/mission/voice_shout";      // max_distance 450 m, os 8 s
        internal const string Ev3dVoice = "event:/mod/mission/voice";            // max_distance 80 m, os 4 s
        internal const string Ev3dAmb = "event:/mod/mission/ambient_3d_big";     // max_distance 100 m, zdarzenie trwale (bez konca)
        private const float ShoutLen = 8f, VoiceLen = 4f, LenSlack = 0.05f;      // dlugosc osi zdarzenia (s) i luz na zaokraglenie dlugosci klipu w manifescie
        private const float LoopCutEarly = 0.05f;                                // ambient_3d_big: ciecie tyle przed koncem klipu (czas rzeczywisty) - bez zapetlenia
        private const float ClipTail = 0.5f;                                     // voice_shout / voice: koniec zdarzenia tyle po koncu klipu (zapas na wczytanie pliku)
        private const float MaxSpeed = 40f;                                       // predkosc zrodla (m/s) - wiecej = przeskok (nowa pozycja), predkosc 0
        internal const int EvShout = 0, EvVoice = 1, EvAmb = 2, EvOver = 3, EvKinds = 4;
        private static readonly string[] EvName = { Ev3dShout, Ev3dVoice, Ev3dAmb, VoiceEvent };
        private static readonly string[] EvShort = { "voice_shout", "voice", "ambient_3d_big", "voiceover" };
        private static readonly string[] EvBankMax = { "450", "80", "100", "100000 (2D)" };
        internal const float YellLeadMax = 2.5f;          // najdluzsze czekanie okrzyku ludzi na glos dowodcy (BattleCries)
        internal const float OrderLead = 0.4f;            // okrzyk ludzi 0,4 s po "Charge!" kapitana (krytyk K4)
        private const float TickEvery = 0.25f;
        private const float RealShare = 0.5f;             // odstep w czasie rzeczywistym = polowa odstepu w czasie misji
        private const float FarExtra = 50f;               // dalej niz max + 50 m od sluchacza - nie tworzymy
        private const int MaxNewPerSec = 5;
        private const int QueueMax = 256;

        // grupy puli i limity naraz
        // noc-final (audyt): GSiegeFx - jednorazowe chwile szturmu (ryk przy drabinie/wiezy, wylom, smola) z wlasnym limitem - petle tla (ogien, tlum, taran,
        // szczek, deszcz) zajmowaly caly GAmb i chwile, o ktore prosil Jeff, dostawaly cisze
        internal const int GVoice = 0, GHorn = 1, GDrum = 2, GAmb = 3, GSiegeFx = 4, Groups = 5;
        private static readonly int[] GroupCap = { 6, 2, 6, 6, 2 };
        private static readonly string[] GroupName = { "glosy", "rogi", "bebny", "tlo", "chwile szturmu" };

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
            internal int Kind;                // 154: zdarzenie (EvShout / EvVoice / EvAmb / EvOver)
            internal Vec3 Last;               // 154: poprzednia pozycja sledzonego zrodla (predkosc)
            internal bool HasLast;
            internal string TestNote;         // 154: dopisek linii probki autotestu (odleglosc)
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
        private static int _created, _played05, _notPlayed, _released, _relPlaying, _relById, _relByStop, _relLeak, _early, _endCap, _rejLimit, _rejFar, _rejNoClip, _rejEngine, _rejRate, _peak;
        private static readonly int[] _byGroup = new int[Groups];
        private static readonly int[] _rejGroup = new int[Groups];
        // 154: zdarzenia wedlug rodzaju, odmowy zdarzenia 3D (zapas voiceover), ciecia petli na koncu klipu, sledzenie co klatke
        private static readonly int[] _cEv = new int[EvKinds];
        private static int _fail3d, _fallback, _loopCut, _trackPeak, _clipEnd;
        private static string _fail3dWhy;

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
            _created = _played05 = _notPlayed = _released = _relPlaying = _relById = _relByStop = _relLeak = _early = _endCap = _rejLimit = _rejFar = _rejNoClip = _rejEngine = _rejRate = _peak = 0;
            for (int i = 0; i < Groups; i++) { _byGroup[i] = 0; _rejGroup[i] = 0; }
            for (int i = 0; i < EvKinds; i++) _cEv[i] = 0;
            _fail3d = _fallback = _loopCut = _trackPeak = _clipEnd = 0;
            _fail3dWhy = null;
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
            // 154: pozycja zrodel za mowca / szykiem i koniec klipu na zdarzeniu trwalym - co klatke (praca: najwyzej tyle, ile zywych zdarzen, limity grup razem 22)
            if (_live.Count > 0) { try { Track(dt); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Sledzenie", e); } }
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
            bool mount, human, mute = false;
            try { p = affected.Position; mount = affected.IsMount; human = affected.IsHuman; }
            catch { return; }
            if (!mount && !human) return;
            // recenzja 25.10: jeki sa meskie - kobieta i Inni / umarli bez jeku (jak CanSpeak); tylko nieprzytomni jecza, wiec sprawdzamy tylko ich
            if (human && state == AgentState.Unconscious)
            {
                try { mute = affected.IsFemale || Undead.Character(affected.Character); } catch { mute = true; }
            }
            lock (_q) { if (_bodyQ.Count < QueueMax) _bodyQ.Add(new Body { Pos = p, Unconscious = state == AgentState.Unconscious, Mount = mount, Mute = mute, At = _now }); }
        }

        /// <summary>Przed zniszczeniem sceny (reset misji / koniec): Release wszystkich zdarzen; bez linii.</summary>
        internal static void OnClearScene(Mission m)
        {
            if (_live.Count > 0) ReleaseAll("czyszczenie sceny");
            try { SiegeSceneCleared(); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Szturm.Scena", e); }   // P10: bez odwolan do obiektow starej sceny
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

        /// <summary>154: zdarzenie wedlug kodu klipu (opis klasy). 2D - zawsze voiceover. Klip dluzszy niz os zdarzenia - dluzsze zdarzenie (bez uciecia konca).</summary>
        private static int EventFor(Clip c, bool threeD)
        {
            if (!threeD) return EvOver;
            switch (c.Code)
            {
                case "sz": case "fi": case "pt": return EvAmb;                       // petle szczeku i ognia, ptaki
                case "sc":
                    if (c.Key != null && (c.Key.StartsWith("assault", StringComparison.Ordinal) || c.Key.StartsWith("defend", StringComparison.Ordinal))) return EvAmb;   // petle tlumu szturmu
                    break;
                case "sg":
                    if (c.Key == "ram") return EvAmb;                                // petla zalogi taranu
                    break;
                case "jk":
                    if (c.Dur <= VoiceLen + LenSlack) return EvVoice;                  // jek - krotki zasieg w banku (80 m)
                    break;
            }
            return c.Dur <= ShoutLen + LenSlack ? EvShout : EvAmb;
        }

        /// <summary>Nowe zdarzenie: limity, odleglosc od sluchacza, zdarzenie wedlug klipu (EventFor), pozycja i odleglosci 3D, Play; odmowa zdarzenia 3D - zapas
        /// voiceover dla tego klipu. null = cisza (licznik).</summary>
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
            int kind = EventFor(clip, threeD);
            SoundEvent ev = null;
            string why;
            bool tried3d = kind != EvOver;
            if (tried3d)
            {
                ev = Start(EvName[kind], clip, scene, true, pos, min, max, out why);
                if (ev == null)
                {
                    // silnik nie przyjal zdarzenia 3D - licznik ("silnik" w linii misji) i stara droga voiceover TYLKO dla tego klipu
                    _fail3d++;
                    if (_fail3dWhy == null) _fail3dWhy = EvShort[kind] + " " + (why ?? "?") + " (" + clip.Id + ")";
                    kind = EvOver;
                }
            }
            if (ev == null)
            {
                ev = Start(VoiceEvent, clip, scene, threeD, pos, min, max, out why);
                if (ev == null) { _rejEngine++; return null; }
                if (tried3d) _fallback++;
            }
            _secCount++;
            _cEv[kind]++;
            if (kind != EvOver) EventFirst(kind, ev, clip, min, max, tag);
            var s = new Snd { Ev = ev, C = clip, Group = group, Tag = tag, T0 = _now, R0 = Real, Is3d = threeD, Follow = follow, FollowF = ff, Pos = pos, Kind = kind };
            _live.Add(s);
            _created++;
            _byGroup[group]++;
            if (_live.Count > _peak) _peak = _live.Count;
            return s;
        }

        /// <summary>CreateEventFromExternalFile, dla 3D pozycja i odleglosci, Play. null = silnik odmowil (why: id -1 / Play false / wyjatek - wyjatek liczony
        /// jako potkniecie); zdarzenie po nieudanym Play zwolnione.</summary>
        private static SoundEvent Start(string evName, Clip clip, Scene scene, bool threeD, Vec3 pos, float min, float max, out string why)
        {
            why = null;
            SoundEvent ev;
            try { ev = SoundEvent.CreateEventFromExternalFile(evName, clip.Path, scene, threeD, false); }
            catch (Exception e) { why = "wyjatek " + e.GetType().Name; RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Create", e); return null; }
            if (ev == null || !ev.IsValid) { why = "nie utworzone (id " + (ev != null ? ev.GetSoundId() : -1).ToString(Inv) + ")"; return null; }
            bool ok = false;
            try
            {
                if (threeD)
                {
                    ev.SetPosition(pos);
                    ev.SetEventMinMaxDistance(new Vec3(min, max, 0f));
                }
                ok = ev.Play();
                if (!ok) why = "Play false";
            }
            catch (Exception e) { why = "wyjatek " + e.GetType().Name; RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Play", e); ok = false; }
            if (!ok)
            {
                try { if (ev.IsValid) ev.Release(); } catch { }
                return null;
            }
            return ev;
        }

        // 154: pierwsze zdarzenie kazdego rodzaju 3D w procesie - uczciwa linia (zastepuje probke "zdarzenie 3D" z P2: odczyt -1/-1 przed i 10/150 po niczego
        // nie dowodzil - to tylko nadpisanie wlasnosci zdarzenia, -1 = nieustawione)
        private static readonly bool[] _evFirst = new bool[EvKinds];

        private static void EventFirst(int kind, SoundEvent ev, Clip clip, float min, float max, string tag)
        {
            if (kind < 0 || kind >= EvKinds || _evFirst[kind]) return;
            _evFirst[kind] = true;
            Vec3 got = Vec3.Zero;
            try { got = ev.GetEventMinMaxDistance(); } catch { }
            Log.Info(P + "zdarzenie 3D " + EvName[kind] + " - pierwsze w procesie (" + tag + ", " + clip.Id + ", " + F1(clip.Dur) + " s): utworzone, Play tak, ustawione min/max "
                     + F1(min) + "/" + F1(max) + " m (odczyt GetEventMinMaxDistance " + F1(got.x) + "/" + F1(got.y) + " = tylko zapisana wlasnosc zdarzenia, -1 = nieustawiona; "
                     + "max_distance w banku gry " + EvBankMax[kind] + " m). Silnik NIE podaje, czy nasze min/max dzialaja ani jak glosno klip slychac - dowod 3D tylko odsluchem "
                     + "(probka odleglosci autotestu: ten sam klip 5 m i 150 m od sluchacza).");
        }

        private static string F1(float v) { return v.ToString("0.0", Inv); }

        /// <summary>154: co klatke misji (Tick) - pozycja i predkosc zrodel za mowca / szykiem (dotad co 0,25 s w Pool) oraz ciecie klipu na zdarzeniu trwalym
        /// ambient_3d_big na jego koncu (czas rzeczywisty = zegar dzwieku; inaczej zdarzenie bez konca zapetliloby klip, a LoopTick dolozyl nastepny).
        /// Praca: jedno przejscie po _live (limity grup razem 22, w logu szczyt 6) - kilka wywolan silnika na zrodlo. Wyjatek - to zrodlo przestaje byc sledzone
        /// (licznik potkniec), reszta dalej.</summary>
        private static void Track(float dt)
        {
            double rnow = Real;
            int tracked = 0;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                try
                {
                    if (s.Kind == EvAmb && rnow - s.R0 >= s.C.Dur - LoopCutEarly)
                    {
                        Check(s, rnow - s.R0, _now - s.T0);
                        _live.RemoveAt(i);
                        _loopCut++;
                        Finish(s, _now - s.T0);
                        continue;
                    }
                    if (!s.Is3d) continue;
                    Vec3 p;
                    if (s.Follow != null)
                    {
                        if (!s.Follow.IsActive()) { s.Follow = null; continue; }   // zginal w trakcie - glos gra do konca w miejscu
                        p = s.Follow.Position + new Vec3(0f, 0f, 1.6f);
                    }
                    else if (s.FollowF != null)
                    {
                        if (s.FollowF.CountOfUnits <= 0) { s.FollowF = null; continue; }
                        p = s.FollowF.CachedMedianPosition.GetNavMeshVec3() + new Vec3(0f, 0f, 1.5f);
                    }
                    else continue;
                    tracked++;
                    Vec3 v = Vec3.Zero;
                    if (s.HasLast && dt > 0.0001f)
                    {
                        v = (p - s.Last) * (1f / dt);
                        if (v.LengthSquared > MaxSpeed * MaxSpeed) v = Vec3.Zero;   // przeskok (np. pierwszy odczyt szyku) - bez predkosci
                    }
                    s.Last = p; s.HasLast = true; s.Pos = p;
                    s.Ev.SetPosition(p);
                    s.Ev.SetVelocity(v);
                }
                catch (Exception e) { s.Follow = null; s.FollowF = null; RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Sledzenie.Klip", e); }
            }
            if (tracked > _trackPeak) _trackPeak = tracked;
        }

        /// <summary>Kontrola "grane po 0,5 s" (raz na zdarzenie): Pool po 0,5 s; Track przed cieciem krotkiego klipu petli.</summary>
        private static void Check(Snd s, double er, float em)
        {
            if (s.Checked || er < 0.5 || em < 0.3f) return;
            s.Checked = true;
            bool playing = s.Ev.IsValid && s.Ev.IsPlaying();
            if (playing) _played05++; else _notPlayed++;
            if (s.Test) TestChecked(s, playing);
        }

        /// <summary>Koniec zdarzenia (juz poza _live): Release, linia probki, nastepny klip mowcy, beben. Wlasny try - wywrotka nie zostawia zdarzenia bez Release.</summary>
        private static void Finish(Snd s, float em)
        {
            ReleaseOne(s);
            try
            {
                if (s.Test) TestEnded(s, em);
                if (s.Then != null && s.Follow != null) QueueThen(s);
                OnSndEnded(s);
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Koniec", e); }
        }

        /// <summary>Takt puli (0,25 s): kontrola "grane po 0,5 s", koniec -> Release, nastepny klip mowcy (pozycja za mowca / szykiem - co klatke w Track, 154).</summary>
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
                    Check(s, er, em);
                    if (!s.Ev.IsValid) ended = true;
                    else if (er >= 0.3 && em >= 0.25f && s.Ev.IsStopped()) ended = true;
                    // 154: voice_shout / voice - os zdarzenia (8 / 4 s) dluzsza niz klip, silnik konczy zdarzenie dopiero na koncu osi; bez tego mowca bylby "zajety"
                    // (IsSpeaking), a motto po przemowie czekalo do konca osi. Klip skonczony (czas rzeczywisty = zegar dzwieku, misji - pauza nie tnie) -> koniec
                    else if ((s.Kind == EvShout || s.Kind == EvVoice) && er >= s.C.Dur + ClipTail && em >= s.C.Dur) { ended = true; _clipEnd++; }
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
                Finish(s, _now - s.T0);
            }
            _ended.Clear();
        }

        private static void ReleaseOne(Snd s)
        {
            // recenzja 25.10: Release na grajacym zdarzeniu = Stop (StopEvent, _soundId -1) i ReleaseEvent(-1) - instancja silnika zostaje (wyciek).
            // Audyt 25.10: dla grajacego zapamietaj id, Stop, a potem ReleaseEvent(id) przez ISoundEvent silnika - tylko gdy id wciaz wazne
            // (jesli StopEvent sam zwolnil instancje, nie ruszamy go drugi raz). Brak odbicia -> stara droga gry, licznik 'wyciek'.
            try
            {
                if (s.Ev != null && s.Ev.IsValid)
                {
                    if (s.Ev.IsPlaying())
                    {
                        _relPlaying++;
                        int id = s.Ev.GetSoundId();
                        if (EngineSnd())
                        {
                            s.Ev.Stop();
                            if ((bool)_mSndValid.Invoke(_iSnd, new object[] { id })) { _mSndRelease.Invoke(_iSnd, new object[] { id }); _relById++; }
                            else _relByStop++;
                        }
                        else { s.Ev.Release(); _relLeak++; }
                    }
                    else s.Ev.Release();
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Release", e); }
            _released++;
        }

        // ISoundEvent silnika (internal) - raz na proces; null = brak (wtedy stara droga Release)
        private static object _iSnd;
        private static System.Reflection.MethodInfo _mSndRelease, _mSndValid;
        private static int _sndResolved;   // 0 nie szukano, 1 jest, 2 brak

        private static bool EngineSnd()
        {
            if (_sndResolved == 0)
            {
                _sndResolved = 2;
                try
                {
                    var asm = typeof(SoundEvent).Assembly;
                    var tEai = asm.GetType("TaleWorlds.Engine.EngineApplicationInterface");
                    var tIf = asm.GetType("TaleWorlds.Engine.ISoundEvent");
                    var f = tEai != null ? AccessTools.Field(tEai, "ISoundEvent") : null;
                    _iSnd = f != null ? f.GetValue(null) : null;
                    _mSndRelease = tIf != null ? AccessTools.Method(tIf, "ReleaseEvent", new[] { typeof(int) }) : null;
                    _mSndValid = tIf != null ? AccessTools.Method(tIf, "IsValid", new[] { typeof(int) }) : null;
                    if (_iSnd != null && _mSndRelease != null && _mSndValid != null) _sndResolved = 1;
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.EngineSnd", e); }
            }
            return _sndResolved == 1;
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
              .Append(", bebny ").Append(_byGroup[GDrum]).Append(", tlo ").Append(_byGroup[GAmb]).Append(", chwile szturmu ").Append(_byGroup[GSiegeFx]).Append("), grane po 0,5 s ").Append(_played05)
              .Append(" (nie grane ").Append(_notPlayed).Append("), zwolnionych ").Append(_released).Append(" (przy koncu misji ").Append(activeEnd)
              .Append(", w trakcie grania ").Append(_relPlaying).Append(": zwolnione po id ").Append(_relById).Append(", zwolnil Stop ").Append(_relByStop)
              .Append(", wyciek ").Append(_relLeak).Append(_sndResolved == 2 ? " - BRAK ISoundEvent" : "").Append("), najwiecej naraz ").Append(_peak).Append(", przerwane wczesnie ").Append(_early).Append(", koniec po czasie ").Append(_endCap)
              .Append("; odrzucone: limit ").Append(_rejLimit).Append(" (glosy ").Append(_rejGroup[GVoice]).Append(", rogi ").Append(_rejGroup[GHorn]).Append(", bebny ").Append(_rejGroup[GDrum]).Append(", tlo ").Append(_rejGroup[GAmb]).Append(", chwile szturmu ").Append(_rejGroup[GSiegeFx]).Append(')').Append(", na sekunde ").Append(_rejRate).Append(", daleko ").Append(_rejFar)
              .Append(", brak klipu ").Append(_rejNoClip).Append(", silnik ").Append(_rejEngine)
              .Append(" (+ zdarzenie 3D odrzucone ").Append(_fail3d).Append(" -> zapas voiceover ").Append(_fallback).Append(_fail3dWhy != null ? "; pierwsze: " + _fail3dWhy : "").Append(')');
            // 154: ktore zdarzenia gry niosly klipy (3D przy zrodle / 2D tlo) i praca sledzenia co klatke
            sb.Append(" | zdarzenia (154): voice_shout ").Append(_cEv[EvShout]).Append(", voice ").Append(_cEv[EvVoice]).Append(", ambient_3d_big ").Append(_cEv[EvAmb])
              .Append(" (ciete na koncu klipu ").Append(_loopCut).Append("), voiceover ").Append(_cEv[EvOver])
              .Append(" (2D tlo + zapas); koniec klipu przed koncem osi zdarzenia ").Append(_clipEnd).Append(", sledzone co klatke najwiecej naraz ").Append(_trackPeak);
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
