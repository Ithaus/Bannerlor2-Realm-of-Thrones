using System;
using System.Diagnostics;
using System.Globalization;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 474-P8b (Jeff 13.11, decyzja 4: "jak przejezdzam obok karczmy to slysze odglosy karczmy, jak sie oddalam na mapie to cichna i milkna"):
    /// DZWIEK KARCZMY NA MAPIE przy zajezdzie przy szlaku. Jedno zrodlo naraz - przy najblizszym zajezdzie w promieniu Radius od druzyny gracza.
    /// DOWOD (dekompilacja 1.4.8): mapa gra dzwieki 3D na scenie mapy (SoundEvent.CreateEvent(id, _mapScene) - MapCameraView, deszcz/snieg MapWeatherView),
    /// sluchacz = punkt celu kamery podniesiony o 7.5% ku kamerze (MapCameraView: SoundManager.SetListenerFrame co klatke); SoundEvent NIE ma glosnosci
    /// (TaleWorlds.Engine.SoundEvent: Play/Stop/SetPosition/SetParameter/SetEventMinMaxDistance - bez SetVolume). Zdarzenie gry
    /// "event:/mission/ambient/detail/tavern_exterior" (Native sound_event_data.gen.xml: max_distance 50, gwar i muzyka zza scian karczmy) ma tlumienie FMOD z odlegloscia.
    /// Glosnosc wedlug odleglosci DRUZYNY (nie kamery): zrodlo stawiane wzgledem sluchacza (SoundManager.GetListenerFrame) w kierunku zajazdu od druzyny, w odleglosci
    /// d / Radius x zasieg zdarzenia (GetEventMinMaxDistance) - przy d = Radius zrodlo na granicy zasiegu FMOD = cisza, dalej zatrzymane i zwolnione. Kierunek (lewo /
    /// prawo) zostaje. Nie zalezy od tego, czy nadpisanie zasiegu dziala w tym zdarzeniu.
    /// Kiedy: mapa (MapState, ekran mapy gotowy, bez ekranu ladowania, bez misji), bez otwartego menu (menu zajazdu ma wlasne tlo karczmy - RoadsideInnStay U2) i bez
    /// rozmowy, druzyna na ladzie poza osada. Inaczej - zrodlo zatrzymane. Koszt: 10 razy na sekunde siatka 3 x 3 komorek zajazdow wokol jednej partii + SetPosition;
    /// zrodlo tworzone raz przy wjezdzie w zasieg zajazdu, zwalniane przy wyjezdzie (Stop + BattleSound.ReleaseById - SoundEvent.Release po Stop nie zwalnia).
    /// Klucz RoadsideInnMapSound (MCM "Roadside Inn Map Sound"); NIE = nic nie biegnie (jedno porownanie na tick aplikacji). Wywrotka - licznik potkniec,
    /// zajazd z wywrotka przy tworzeniu pomijany przez 10 s (bez globalnego wylacznika). Linie "Dzwiek karczmy na mapie (474-P8b): ..." - start sesji,
    /// pierwsze 10 wejsc w zasieg (zajazd, odleglosc, zasieg zdarzenia), doba (wejscia, czas grania, sredni koszt ticku).
    /// </summary>
    internal static class RoadsideInnMapSound
    {
        internal const string P = "Dzwiek karczmy na mapie (474-P8b): ";
        private const string Ev = "event:/mission/ambient/detail/tavern_exterior";
        internal const float Radius = 6f;           // jedn. mapy od druzyny (wejscie do zajazdu 1.6, pytanie wraca po 3.5; zajazdy co 30-110)
        private const float EvMaxFallback = 50f;    // max_distance zdarzenia w sound_event_data.gen.xml
        internal static bool On { get { var s = Settings.Current; return s != null && s.RoadsideInns && s.RoadsideInnMapSound; } }

        private static SoundEvent _ev;
        private static Scene _scene;
        private static string _uid, _badUid;
        private static float _evMax = EvMaxFallback;
        private static long _last, _badUntil, _playT0;
        private static int _st, _dStarts, _dStops, _dTicks, _lines;
        private static long _dTickTicks, _dPlayTicks;
        private static bool _errLogged;

        private static void Stumble(string where, Exception e)
        {
            _st++;
            if (!_errLogged) { _errLogged = true; try { Log.Error("RoadsideInnMapSound." + where, e); } catch { } }
        }

        /// <summary>SubModuleMain.OnApplicationTick (jedna linia, wlasny licznik bledow ladowania typu). Klucz NIE i nic nie gra - od razu wyjscie.</summary>
        internal static void AppTick()
        {
            if (_ev == null && !On) return;
            long t = Stopwatch.GetTimestamp();
            if (t - _last < Stopwatch.Frequency / 10) return;
            _last = t;
            try
            {
                string why;
                RoadsideInns.Inn inn = Want(out why);
                if (inn == null) { if (_ev != null) Stop(why); }
                else Step(inn);
            }
            catch (Exception e) { Stumble("AppTick", e); try { Stop("wywrotka"); } catch { } }
            finally { _dTickTicks += Stopwatch.GetTimestamp() - t; _dTicks++; }
        }

        /// <summary>Zajazd do grania albo null z powodem.</summary>
        private static RoadsideInns.Inn Want(out string why)
        {
            why = null;
            if (!On) { why = "klucz wylaczony"; return null; }
            var c = Campaign.Current;
            if (c == null) { why = "poza kampania"; return null; }
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (gsm == null || !(gsm.ActiveState is MapState)) { why = "nie mapa"; return null; }
            if (gsm.ActiveStateDisabledByUser) { why = "menu Esc"; return null; }   // 474-P8b-p: menu Esc / encyklopedia nad mapa - cisza (jak CampWalk)
            if (LoadingWindow.IsLoadingWindowActive || Mission.Current != null) { why = "ladowanie / misja"; return null; }
            var ms = MapScreen.Instance;
            if (ms == null || !ms.IsReady || ms.MapScene == null) { why = "ekran mapy"; return null; }
            if (c.CurrentMenuContext != null) { why = "menu"; return null; }
            var cm = c.ConversationManager;
            if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) { why = "rozmowa"; return null; }
            var mp = MobileParty.MainParty;
            if (mp == null || mp.CurrentSettlement != null || mp.IsCurrentlyAtSea || mp.MapEvent != null) { why = "druzyna"; return null; }
            RoadsideInns.EnsureLoaded();
            var inn = RoadsideInns.Nearest(mp.GetPosition2D, Radius);
            if (inn == null) { why = "poza zasiegiem"; return null; }
            if (inn.Uid == _badUid && Stopwatch.GetTimestamp() < _badUntil) { why = "zajazd po wywrotce"; return null; }
            return inn;
        }

        private static void Step(RoadsideInns.Inn inn)
        {
            var scene = MapScreen.Instance.MapScene;
            if (_ev != null && (!ReferenceEquals(scene, _scene) || _uid != inn.Uid)) Stop(_uid != inn.Uid ? "inny zajazd blizej" : "nowa scena mapy");
            var mp = MobileParty.MainParty;
            Vec2 party = mp.GetPosition2D, at = new Vec2(inn.X, inn.Y);
            float d = party.Distance(at);
            if (_ev == null)
            {
                try
                {
                    _ev = SoundEvent.CreateEventFromString(Ev, scene);
                    if (_ev == null || !_ev.IsValid) { _ev = null; Bad(inn, "zdarzenie niewazne"); return; }
                    _scene = scene; _uid = inn.Uid;
                    float mx = 0f;
                    try { mx = _ev.GetEventMinMaxDistance().y; } catch { }
                    _evMax = mx > 0.5f && mx < 5000f ? mx : EvMaxFallback;
                    Place(party, at, d);
                    _ev.Play();
                    _playT0 = Stopwatch.GetTimestamp();
                    _dStarts++;
                    if (_lines < 10)
                    {
                        _lines++;
                        Log.Info(P + "slychac " + inn.Name + " (" + inn.Uid + ") - druzyna " + d.ToString("0.0", CultureInfo.InvariantCulture) + " jedn. (cisza od " + Radius.ToString("0", CultureInfo.InvariantCulture)
                                 + ") | zasieg zdarzenia " + (mx > 0f ? mx.ToString("0.#", CultureInfo.InvariantCulture) : "?") + (Math.Abs(_evMax - mx) > 0.01f ? " (przyjete " + _evMax.ToString("0", CultureInfo.InvariantCulture) + ")" : "")
                                 + " | wejscie w zasieg nr " + _dStarts + " dzis" + (_lines == 10 ? " (dalej bez linii do konca sesji)" : "") + ".");
                    }
                }
                catch (Exception e) { Stumble("Start", e); FreeNow(); Bad(inn, "wywrotka"); }
                return;
            }
            Place(party, at, d);
        }

        /// <summary>Zrodlo wzgledem sluchacza: kierunek od druzyny do zajazdu, odleglosc d / Radius x zasieg zdarzenia (przy Radius - granica zasiegu FMOD, cisza).</summary>
        private static void Place(Vec2 party, Vec2 at, float d)
        {
            Vec2 dir = at - party;
            if (dir.LengthSquared < 0.0001f) dir = new Vec2(0f, 1f);
            dir.Normalize();
            float vd = Math.Min(1f, d / Radius) * _evMax;
            var lf = SoundManager.GetListenerFrame();
            _ev.SetPosition(new Vec3(lf.origin.x + dir.x * vd, lf.origin.y + dir.y * vd, lf.origin.z));
        }

        private static void Bad(RoadsideInns.Inn inn, string why)
        {
            _badUid = inn.Uid;
            _badUntil = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 10;
            if (_lines < 10) { _lines++; Log.Info(P + "zajazd " + inn.Name + " (" + inn.Uid + ") - " + why + "; ponowna proba za 10 s."); }
        }

        private static void Stop(string why)
        {
            if (_ev == null) return;
            if (_playT0 != 0) _dPlayTicks += Stopwatch.GetTimestamp() - _playT0;
            _playT0 = 0;
            FreeNow();
            _dStops++;
        }

        private static void FreeNow()
        {
            var ev = _ev;
            _ev = null; _scene = null; _uid = null;
            if (ev == null) return;
            try
            {
                if (!ev.IsValid) return;   // 474-P8b-p: zdarzenie juz niewazne (scena mapy zniszczona) - nic nie wolamy
                int id = ev.GetSoundId();
                ev.Stop();
                BattleSound.ReleaseById(id);
            }
            catch (Exception e) { Stumble("Free", e); }
        }

        /// <summary>474-P8b-p: SubModuleMain.OnGameEnd - zrodlo zatrzymane i zwolnione, zanim zniknie scena mapy (wyjscie do menu, wczytanie innego zapisu).</summary>
        internal static void GameEnd()
        {
            try { Stop("koniec gry"); } catch (Exception e) { Stumble("GameEnd", e); }
        }

        internal static void SessionStart()
        {
            try
            {
                Stop("nowa sesja");
                _lines = 0;
                Log.Info(P + "start sesji | klucz Roadside Inn Map Sound " + (On ? "TAK" : "NIE") + " | zdarzenie " + Ev + " | cisza od " + Radius.ToString("0", CultureInfo.InvariantCulture)
                         + " jedn. od zajazdu, jedno zrodlo naraz | potkniecia " + _st + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Czesc linii doby zajazdow (przy kluczu albo gdy cos gralo). Zeruje liczniki doby.</summary>
        internal static string DayText()
        {
            double ms = _dTicks > 0 ? _dTickTicks * 1000.0 / Stopwatch.Frequency / _dTicks : 0.0;
            long play = _dPlayTicks + (_playT0 != 0 ? Stopwatch.GetTimestamp() - _playT0 : 0);
            string t = "dzwiek na mapie (474-P8b): wejscia w zasieg " + _dStarts + ", zatrzymania " + _dStops + ", gralo " + (play / (double)Stopwatch.Frequency).ToString("0", CultureInfo.InvariantCulture)
                       + " s, ticki " + _dTicks + " (srednio " + ms.ToString("0.0000", CultureInfo.InvariantCulture) + " ms), potkniecia " + _st;
            _dStarts = _dStops = _dTicks = 0; _dTickTicks = 0; _dPlayTicks = 0;
            if (_playT0 != 0) _playT0 = Stopwatch.GetTimestamp();
            return t;
        }
    }
}
