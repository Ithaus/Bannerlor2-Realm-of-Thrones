using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Scene = TaleWorlds.Engine.Scene;
using SoundEvent = TaleWorlds.Engine.SoundEvent;   // bez using TaleWorlds.Engine - jego Path zaslania System.IO.Path

namespace Armoury
{
    /// <summary>
    /// m108-muzyka (Jeff 22.10: "jak uruchamiaja sie wydarzenia, lektor czyta, a w tle leci muzyka i zaglusza - muzyka ma cichnac i mamy nasz utwor, nieco
    /// ciszej, aby posluchac"; "plotkowanie, cale wydarzenie - muzyka tylko nasza i zeby nie przeszkadzala"). Klucz EventMusicEnabled (TAK).
    ///  - Kiedy: przez cale okno wydarzenia (stan gry ROT EventPopupState - okna ROT, Kronika, sceny prologu; Lektor.EventStateActive) i przez czytanie strony
    ///    plotek (Lektor.GossipPageActive). Sprawdzane co klatke z SubModuleMain.OnApplicationTick (bez latek - sam odczyt stanu).
    ///  - Muzyka gry: psai (TaleWorlds.PSAI, psai.net.PsaiCore - odbicie, bez referencji): GetVolume zapamietane, SetVolume plynnie do 0 w 1,2 s i 0 trzymane
    ///    co klatke (bez SetPaused - 103.5, Jeff 22.10 "w bitwie muzyka chwile gra i nagle cichnie"); po wydarzeniu (0,6 s bez okna i plotek - kolejne okno zaraz
    ///    po zamknieciu nie przywraca) SetVolume plynnie do zapamietanej w 2 s. Wyjscie z kampanii / blad - przywrocenie od razu. Glosnosci gracza nie zmieniamy w ustawieniach.
    ///  - m109-muzyka (EventMusic.Bitwa.cs): w misji bitwy nasze 6 utworow bitewnych losowo zamiast muzyki gry - ten sam stan wyciszenia psai (_ducked, _saved, _cur,
    ///    _active, _mus) i te same DuckStart / FadeDown / FadeUp: jeden wlasciciel glosnosci psai, okno i bitwa nie przywracaja sie nawzajem.
    ///  - Nasz utwor: ModuleData/Lektor/Muzyka/muz_[nastroj].ogg (ElevenLabs Music, instrumentalne, -28 LUFS - ok. 13 dB pod lektorem -14.5 LUFS, pasmo 2.5 kHz -3 dB
    ///    pod glos; tools/muzyka). Odtwarzanie jak lektor: SoundEvent.CreateEventFromExternalFile("event:/Extra/external", ...) - zdarzenie z instrumentem
    ///    programisty (MasterBank.strings: Extra/external obok Extra/voiceover; voiceover NIE - jeden glos naraz, uciszylby lektora). Po koncu utworu - od nowa.
    ///    Nastroj z tytulu, klucza i tekstu okna (slowa: smierc -> zaloba, wojna, Polnoc, korona i smoki, reszta intryga); plotki - plotka.
    ///    m164: nastroj "wojna" gra muz_korona.ogg (MapMood, EventMusic.Mapa.cs) - muzyka wojenna tylko w bitwie; po oknie motyw wydarzenia ROT w psai zatrzymany
    ///    (EventThemeStop, klucz StopEventThemesOnMap); stop utworu przez StopRelease (Stop przed Release, gdy zdarzenie nie gra).
    ///  - Linie "Muzyka (m108): ..." (start okna z nastrojem i glosnoscia psai, koniec, bledy pierwsze 3). Potkniecia liczone, bez wylacznika.
    ///  - Paczka 165 (EventMusic.MapaTlo.cs): na mapie kampanii nasze 10 spokojnych utworow map_NN zamiast muzyki mapy gry - ten sam stan wyciszenia psai i to samo
    ///    gniazdo utworu (_mus, nastroj "mapa"); pierwszenstwo okno / plotki > bitwa > mapa. Okno przy mapie: utwor mapy stop, po oknie wyciszenie zostaje
    ///    (linia "koniec wydarzenia - mapa dalej"); ekran ladowania - psai trzymane bez FadeUp (decyzja po ladowaniu: mapa albo misja).
    /// </summary>
    internal static partial class EventMusic
    {
        internal const string P = "Muzyka (m108): ";
        private const string MusicEvent = "event:/Extra/external";
        private const float FadeOutSec = 1.2f, FadeInSec = 2.0f, ReleaseDelay = 0.6f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- psai (odbicie raz)
        private static bool _psaiTried, _psaiOk;
        private static MethodInfo _mInit, _mGetVol, _mSetVol, _mPause;
        private static PropertyInfo _pInst;

        // ---- stan
        private static bool _ducked, _paused, _active;
        private static float _saved = 1f, _cur = 1f, _idle;
        private static SoundEvent _mus;
        private static string _musMood, _musFile;
        private static int _winSeqSeen = -1, _stumbles, _createFails, _starts;
        private static string _mood = "intryga", _moodTag = "";
        private static Dictionary<string, string> _tracks;
        private static double _lastTick;
        private static bool _volLogged;

        private static double Now { get { return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("EventMusic." + where + " (potkniecie " + _stumbles + ")", e);
        }

        private static bool KeyOn
        {
            get { var s = Settings.Current; return s == null || s.EventMusicEnabled; }
        }

        // ============================================================ nastroj okna (Lektor.OnActivatePost, przed kluczem lektora)
        /// <summary>Nowe okno wydarzenia: nastroj z tytulu, klucza i tekstu. Glowny watek. Nie rzuca.</summary>
        internal static void NoteWindow(string title, string key, string text)
        {
            try
            {
                string all = ((title ?? "") + " | " + (key ?? "") + " | " + (text ?? "")).ToLowerInvariant();
                _mood = MoodOf(all);
                _moodTag = string.IsNullOrEmpty(title) ? (key ?? "?") : title;
            }
            catch (Exception e) { Stumble("NoteWindow", e); }
        }

        private static readonly string[] WZaloba = { "death", "dies", "died", "dead", "killed", "slain", "murder", "execut", "funeral", "beheaded", "poison", "burned at", "burning of", "pyre", "red wedding", "mourn", "grief", "corpse" };
        private static readonly string[] WWojna = { "war ", "war,", "war.", " war", "battle", "siege", "army", "armies", "host of", "banners", "march", "invasion", "invade", "rebellion", "attack", "raid", "sack of", "storm the", "besieg" };
        private static readonly string[] WPolnoc = { "the wall", "night's watch", "wildling", "free folk", "white walker", "the others", "winterfell", "the north", "beyond the", "snow", "winter", "castle black", "hardhome", "frost" };
        private static readonly string[] WKorona = { "dragon", "crown", "coronat", "throne", "targaryen", "daenerys", "khal", "comet", "royal", "the king", "king's", "the queen", "queen's", "wedding" };

        internal static string MoodOf(string all)
        {
            if (Any(all, WZaloba)) return "zaloba";
            if (Any(all, WWojna)) return "wojna";
            if (Any(all, WPolnoc)) return "polnoc";
            if (Any(all, WKorona)) return "korona";
            return "intryga";
        }

        private static bool Any(string s, string[] words)
        {
            foreach (var w in words) if (s.IndexOf(w, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        // ============================================================ takt (SubModuleMain.OnApplicationTick)
        internal static void AppTick()
        {
            double now = Now;
            float dt = _lastTick > 0 ? (float)Math.Min(0.25, now - _lastTick) : 0f;
            _lastTick = now;
            BattleStartCheck();   // m109-muzyka: linia startu raz na kampanie (wlasny try w srodku)
            bool win = false, gossip = false;
            if (KeyOn)
            {
                try { win = Lektor.MusicWindowActive(); } catch (Exception e) { Stumble("Window", e); }
                if (!win) { try { gossip = Lektor.MusicGossipActive(); } catch (Exception e) { Stumble("Gossip", e); } }
            }
            bool battle = !win && !gossip && BattleWanted();   // m109-muzyka: okno i plotki maja pierwszenstwo (wlasny try w srodku)
            bool map = !win && !gossip && !battle && MapWanted();   // paczka 165: mapa kampanii na koncu kolejki (wlasny try w srodku, bez alokacji)
            bool want = win || gossip || battle || map;
            if (!want && !_active && !_ducked && _mus == null && !_mapOn) return;   // nic nie gra i nic nie czeka
            try { Step(want, win, battle, map, dt); }
            catch (Exception e)
            {
                if (battle) BattleStumble("Step", e);   // m109-muzyka: blad w bitwie liczony na misje (po 3 rezygnacja z tej misji), bez przywracania co klatke
                else if (map) { MapStumble("Step", e); MapFail("Step"); RestoreNow("blad"); }   // paczka 165: po 3 z rzedu rezygnacja do nastepnej misji / wczytania
                else { Stumble("Step", e); RestoreNow("blad"); }
            }
        }

        private static void Step(bool want, bool win, bool battle, bool map, float dt)
        {
            if (want)
            {
                if (battle)
                {
                    if (_mapOn) MapOff("misja", "przejmuje bitwa");   // paczka 165: nie powinno sie zdarzyc (misja wylacza mape klatki wczesniej)
                    MapHandOver("bitwa");                               // paczka 165: psai po mapie jeszcze nie wrocilo - przejmuje bitwa (linia zamiast "wrocila")
                    BattleStep(dt); FadeDown(dt); return;               // m109-muzyka: nasze utwory bitewne, psai trzymane na 0
                }
                if (map) { MapStep(dt); return; }                       // paczka 165: nasze utwory mapy, psai trzymane na 0 (EventMusic.MapaTlo.cs)
                if (_mapOn) MapYield(win ? "okno wydarzenia" : "plotki");   // paczka 165: utwor mapy stop od razu, po oknie nastepny za ok. 3 s
                if (_batActive) BattleEnd("okno wydarzenia", false);    // m109-muzyka: okno wydarzenia przejmuje (wyciszenie zostaje)
                _idle = 0f;
                string asked = win ? _mood : "plotka";
                string mood = MapMood(asked);   // m164: "wojna" -> "korona" (muzyka wojenna tylko w bitwie)
                if (!_active)
                {
                    _active = true;
                    _starts++;
                    DuckStart();
                    Log.Info(P + (win ? "okno \"" + _moodTag + "\"" : "plotki") + " - nastroj " + MoodText(asked, mood) + ", muzyka gry cichnie (psai " + (_psaiOk ? "glosnosc " + _saved.ToString("0.00", Inv) : "BRAK") + ").");
                    EventThemeSeen();   // m164 recenzja: motyw wydarzenia ROT na warstwie psai przy otwarciu okna - linia, para do EventThemeStop
                }
                int seq = Lektor.WindowSeq;
                if (_mus == null || _musMood != mood || (win && seq != _winSeqSeen)) { _winSeqSeen = seq; if (_mus == null || _musMood != mood) StartTrack(mood); }
                else LoopTrack();
                FadeDown(dt);
                return;
            }
            // bez okna i plotek: chwila zapasu (kolejne okno zaraz po zamknieciu), potem stop utworu i powrot muzyki gry
            _idle += dt;
            // paczka 165: ekran ladowania w kampanii przy wyciszonym psai (np. powrot z bitwy na mape) - psai trzymane, bez FadeUp; po ladowaniu mapa albo misja
            bool loadHold = _mapOffWhy == "ekran ladowania" && _ducked;
            if (_mapOn && !loadHold) MapOff(_mapOffWhy, "muzyka gry wraca (" + FadeInSec.ToString("0.0", Inv) + " s)");   // paczka 165: misja, klucz NIE, inny stan
            if (_active && _idle >= ReleaseDelay)
            {
                _active = false;
                if (_batActive) BattleEnd(_batWhy, false);   // m109-muzyka: koniec bitwy (powod z BattleWanted) - linia bitwy zamiast linii wydarzenia
                else
                {
                    StopTrack("koniec wydarzenia");
                    Log.Info(P + "koniec wydarzenia - muzyka gry wraca (" + FadeInSec.ToString("0.0", Inv) + " s).");
                    EventThemeStop();   // m164: motyw wydarzenia ROT nie gra dalej na mapie (psai jeszcze ~0 - FadeUp rusza w tej samej klatce, warstwa stoi po 1 kroku psai, 50 ms)
                }
                _paused = false;
            }
            if (!_active && _ducked && !loadHold) FadeUp(dt);
        }

        // ------------------------------------------------------------ psai
        private static void PsaiResolve()
        {
            if (_psaiTried) return;
            _psaiTried = true;
            try
            {
                Type t = Type.GetType("psai.net.PsaiCore, TaleWorlds.PSAI");
                if (t == null) { Log.Info(P + "psai.net.PsaiCore brak - muzyka gry bez wyciszania."); return; }
                _mInit = t.GetMethod("IsInstanceInitialized", BindingFlags.Public | BindingFlags.Static);
                _pInst = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _mGetVol = t.GetMethod("GetVolume", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _mSetVol = t.GetMethod("SetVolume", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null);
                _mPause = t.GetMethod("SetPaused", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                _psaiOk = _mInit != null && _pInst != null && _mGetVol != null && _mSetVol != null && _mPause != null;
                Log.Info(P + "psai " + (_psaiOk ? "TAK (GetVolume/SetVolume/SetPaused)" : "NIE - brak metod") + ".");
            }
            catch (Exception e) { Stumble("PsaiResolve", e); _psaiOk = false; }
        }

        private static object Psai()
        {
            PsaiResolve();
            if (!_psaiOk) return null;
            if (!(bool)_mInit.Invoke(null, null)) return null;
            return _pInst.GetValue(null, null);
        }

        private static void DuckStart()
        {
            object ps = Psai();
            if (ps == null) return;
            if (!_ducked)
            {
                float v = (float)_mGetVol.Invoke(ps, null);
                if (v > 0.02f) _saved = v;          // gra po naszym wyciszeniu nie wrocila w pelni - zostaje poprzednia zapamietana
                _cur = v;
                _ducked = true;
                _volLogged = false;
            }
        }

        private static void FadeDown(float dt)
        {
            if (!_ducked) return;
            object ps = Psai();
            if (ps == null) return;
            if (_cur > 0f)
            {
                _cur = Math.Max(0f, _cur - dt * Math.Max(_saved, 0.1f) / FadeOutSec);
                _mSetVol.Invoke(ps, new object[] { _cur });
            }
            else
            {
                if (_cur <= 0f) _mSetVol.Invoke(ps, new object[] { 0f });   // trzymamy 0 (gra moglaby podniesc)
                if (_paused) return;
                _paused = true;   // Jeff 22.10: "w bitwie muzyka chwile chodzi i nagle cichnie - wczesniej tak nie bylo" - SetPaused psai rozstraja zegar odcinkow na reszte sesji; tylko glosnosc 0
                float got = (float)_mGetVol.Invoke(ps, null);
                if (!_volLogged) { _volLogged = true; Log.Info(P + "muzyka gry wyciszona (bez pauzy psai; psai glosnosc teraz " + got.ToString("0.00", Inv) + ", zapamietana " + _saved.ToString("0.00", Inv) + ")."); }
            }
        }

        private static void FadeUp(float dt)
        {
            object ps = Psai();
            if (ps == null) { _ducked = false; BattleUnducked(null, null); MapUnducked(null, null); return; }
            _cur = Math.Min(_saved, _cur + dt * Math.Max(_saved, 0.1f) / FadeInSec);
            _mSetVol.Invoke(ps, new object[] { _cur });
            if (_cur >= _saved - 0.0001f) { _ducked = false; BattleUnducked(ps, null); MapUnducked(ps, null); }   // m109-muzyka: linia powrotu po bitwie; paczka 165: po mapie
        }

        private static void PsaiPause(bool p)
        {
            object ps = Psai();
            if (ps != null) _mPause.Invoke(ps, new object[] { p });
        }

        /// <summary>Wyjscie z kampanii albo blad: utwor stop, muzyka gry od razu z powrotem.</summary>
        internal static void RestoreNow(string why)
        {
            try { if (_batActive) BattleEnd(why, true); } catch (Exception e) { Stumble("RestoreNow.Bitwa", e); }   // m109-muzyka: podsumowanie bitwy i stop utworu
            try { MapReset(why); } catch (Exception e) { MapStumble("RestoreNow.Mapa", e); }   // paczka 165: utwor mapy stop, stan mapy od zera
            try { StopTrack(why); } catch (Exception e) { Stumble("RestoreNow.Stop", e); }
            try
            {
                if (_ducked || _paused)
                {
                    object ps = Psai();
                    if (ps != null)
                    {
                        _mSetVol.Invoke(ps, new object[] { _saved });
                    }
                    Log.Info(P + "muzyka gry przywrocona od razu (" + why + ").");
                    BattleUnducked(ps, why);   // m109-muzyka: linia powrotu po bitwie "od razu"
                    MapUnducked(ps, why);      // paczka 165: linia powrotu po mapie "od razu"
                }
            }
            catch (Exception e) { Stumble("RestoreNow.Psai", e); }
            _ducked = false; _paused = false; _active = false; _idle = 0f; _cur = _saved; _duckMap = false;
            BattleReset(why);   // m109-muzyka: stan bitwy od zera (wczytanie zapisu z misji, wyjscie do menu); blad w misji bitwy - rezygnacja z tej misji
        }

        // ------------------------------------------------------------ nasz utwor
        private static void LoadTracks()
        {
            if (_tracks != null) return;
            _tracks = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string dir = Path.Combine(Path.Combine(Path.Combine(Lektor.ModuleDir(), "ModuleData"), "Lektor"), "Muzyka");
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "muz_*.ogg"))
                    {
                        string n = Path.GetFileNameWithoutExtension(f).Substring(4);
                        if (new FileInfo(f).Length > 4096) _tracks[n] = f;
                    }
                Log.Info(P + "utworow " + _tracks.Count + " (" + string.Join(", ", new List<string>(_tracks.Keys).ToArray()) + ") w " + dir + ".");
            }
            catch (Exception e) { Stumble("LoadTracks", e); }
        }

        private static void StartTrack(string mood)
        {
            StopTrack(null);
            LoadTracks();
            string path;
            if (!_tracks.TryGetValue(mood, out path) && !_tracks.TryGetValue("intryga", out path)) return;
            SoundEvent ev = null;
            try { ev = SoundEvent.CreateEventFromExternalFile(MusicEvent, path, (Scene)null, false, false); }
            catch (Exception e) { Stumble("CreateEventFromExternalFile", e); return; }
            bool ok = false;
            try { ok = ev != null && ev.IsValid && ev.Play(); } catch (Exception e) { Stumble("Play", e); }
            if (!ok)
            {
                _createFails++;
                try { if (ev != null && ev.IsValid) ev.Release(); } catch { }
                if (_createFails <= 3) Log.Info(P + "silnik nie przyjal utworu " + Path.GetFileName(path) + " (zdarzenie " + MusicEvent + ") - bez naszej muzyki, muzyka gry dalej wyciszona.");
                return;
            }
            _mus = ev; _musMood = mood; _musFile = Path.GetFileName(path);
            Log.Info(P + "gra " + _musFile + " (nastroj " + mood + ").");
        }

        private static void LoopTrack()
        {
            var ev = _mus;
            if (ev == null) return;
            bool end = false;
            try { end = !ev.IsValid || ev.IsStopped(); } catch (Exception e) { Stumble("Loop", e); end = true; }
            if (end) { string m = _musMood; _mus = null; StopRelease(ev, false); StartTrack(m); }
        }

        private static void StopTrack(string why)
        {
            var ev = _mus;
            if (ev == null) return;
            _mus = null;
            _stopState = StopRelease(ev, _musMood == BattleMood);   // m164: Stop przed Release, gdy zdarzenie nie gra (pauza / start); stan do linii konca bitwy
            if (why != null) Log.Info(P + "stop " + _musFile + " (" + why + ").");
        }

        internal static string DayText() { return "wydarzen z muzyka " + _starts + ", bledy utworu " + _createFails + ", potkniecia " + _stumbles; }
    }
}
