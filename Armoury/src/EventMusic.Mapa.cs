using System;
using System.Reflection;
using SoundEvent = TaleWorlds.Engine.SoundEvent;   // bez using TaleWorlds.Engine - jego Path zaslania System.IO.Path

namespace Armoury
{
    /// <summary>
    /// m164-muzyka: rozdzielenie muzyki mapy kampanii i bitwy (Jeff 26.10 00:15: "muzyka wojenna nie [ma sie] odtwarzac na mapie kampanii, rozdzielmy muzyke mapa
    /// kampanii i bitew"; raport scratchpad/muzyka_bitwy/RAPORT.md 1c: dwie drogi muzyki wojennej na mapie z dowodem w logach Jeffa).
    ///  - Okna wydarzen z nastrojem "wojna" (m108, MoodOf) graja muz_korona.ogg (niska blacha i smyczki, bez bebnow) zamiast muz_wojna.ogg (bebny wojenne) -
    ///    MapMood; muzyka bitewna (bit_*.ogg, EventMusic.Bitwa.cs) gra tylko w misji bitwy, jej katalog Bitwa/ nie jest czytany przez okna (LoadTracks bez podkatalogow).
    ///  - Motywy wydarzen ROT: ROT przy oknie wydarzenia wola PsaiCore.StopMusic(true) + PlaySegment(418-431) (dekompilacja ROT.Events/*Event.cs, np.
    ///    HarrenhalSiegeEvent: segment 421 Blackwater 410 s), a przy zamknieciu okna muzyki nie zatrzymuje; CampaignMusicHandler.TickCampaignMusic nie przerywa
    ///    grajacego psai - motyw (np. Blackwater) gral dalej na mapie. Klucz StopEventThemesOnMap (TAK): przy koncu okna m108 (0,6 s po zamknieciu, poza bitwa),
    ///    gdy psai gra motyw wydarzenia ROT (GetCurrentThemeId 402-415, ThemeTypeInt 7 w ROT-Content/music/soundtrack.xml) - PsaiCore.StopMusic(true, 3f)
    ///    (Logik.StopMusic: wyciszenie glosu 3 s i tryb ciszy), zanim glosnosc psai wroci (FadeUp 2 s); mapa wraca do swoich motywow po przerwie gry (30-120 s).
    ///    Dziala tylko z oknami sledzonymi przez m108 (klucz EventMusicEnabled TAK).
    ///  - Stop naszych utworow (okna i bitwa): StopRelease - SoundEvent.Release wola Stop tylko dla IsPlaying (dekompilacja SoundEvent.Release), zdarzenie
    ///    spauzowane / startujace dostaloby samo ReleaseEvent (ryzyko H-stop z raportu: moglo przezyc wyjscie z misji); tu najpierw Stop, potem Release jak w grze.
    ///  - Linie "Muzyka (m108): mapa - ..." (start kampanii) i "Muzyka (m108): motyw wydarzenia ROT ... zatrzymany po oknie ..." (kazde zatrzymanie).
    /// </summary>
    internal static partial class EventMusic
    {
        private const int RotEventThemeMin = 402, RotEventThemeMax = 415;
        private const float RotThemeFadeSec = 3f;
        private const string MapWarMood = "wojna", MapWarInstead = "korona";

        private static string _stopState = "-";
        private static bool _thTried, _thOk;
        private static MethodInfo _mThemeId, _mSegId, _mStopMusic;
        private static int _thStops;

        private static bool ThemeKeyOn
        {
            get { var s = Settings.Current; return s == null || s.StopEventThemesOnMap; }
        }

        /// <summary>Nastroj okna wydarzenia do zagrania: "wojna" -> "korona" (bez muzyki wojennej poza bitwa), reszta bez zmian.</summary>
        internal static string MapMood(string mood) { return mood == MapWarMood ? MapWarInstead : mood; }

        /// <summary>Opis nastroju w linii okna: "korona (zamiast wojna)" albo sam nastroj.</summary>
        private static string MoodText(string asked, string played) { return asked == played ? played : played + " (zamiast " + asked + ")"; }

        /// <summary>Stop i zwolnienie zdarzenia utworu. Zwraca stan przed stopem: gra / pauza / stop / inne / niewazne / blad (pole _stopState i linia konca bitwy).</summary>
        private static string StopRelease(SoundEvent ev, bool battle)
        {
            if (ev == null) return "-";
            string st = "?";
            try
            {
                if (!ev.IsValid) return "niewazne";
                st = ev.IsPaused() ? "pauza" : ev.IsPlaying() ? "gra" : ev.IsStopped() ? "stop" : "inne";
                if (st == "pauza" || st == "inne") ev.Stop();   // Release gry zatrzymuje tylko IsPlaying; Stop zeruje id (IsValid NIE) - dalej jak Release gry po Stop
                if (ev.IsValid) ev.Release();
            }
            catch (Exception e)
            {
                if (battle) BattleStumble("StopRelease", e); else Stumble("StopRelease", e);
                st = "blad";
            }
            return st;
        }

        // ------------------------------------------------------------ start kampanii (BattleStartCheck - raz na obiekt Campaign)
        private static void MapStartLine()
        {
            ThemeResolve();
            Log.Info(P + "mapa - okna z nastrojem " + MapWarMood + " graja muz_" + MapWarInstead + ".ogg (muzyka wojenna tylko w bitwie), klucz StopEventThemesOnMap "
                     + (ThemeKeyOn ? "TAK" : "NIE") + " (motywy wydarzen ROT " + RotEventThemeMin + "-" + RotEventThemeMax + " zatrzymywane " + RotThemeFadeSec.ToString("0", Inv)
                     + " s po oknie), psai motywy " + (_thOk ? "TAK" : "NIE") + ", klucz EventMusicEnabled " + (KeyOn ? "TAK" : "NIE") + ".");
        }

        private static void ThemeResolve()
        {
            if (_thTried) return;
            _thTried = true;
            try
            {
                Type t = Type.GetType("psai.net.PsaiCore, TaleWorlds.PSAI");
                if (t == null) return;
                _mThemeId = t.GetMethod("GetCurrentThemeId", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _mSegId = t.GetMethod("GetCurrentSegmentId", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _mStopMusic = t.GetMethod("StopMusic", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool), typeof(float) }, null);
                _thOk = _mThemeId != null && _mSegId != null && _mStopMusic != null;
            }
            catch (Exception e) { Stumble("ThemeResolve", e); _thOk = false; }
        }

        /// <summary>Koniec okna wydarzenia / plotek poza bitwa (Step): motyw wydarzenia ROT w psai - stop z wyciszeniem 3 s. Nie rzuca.</summary>
        private static void EventThemeStop()
        {
            if (!ThemeKeyOn) return;
            try
            {
                ThemeResolve();
                if (!_thOk) return;
                object ps = Psai();
                if (ps == null) return;
                int theme = (int)_mThemeId.Invoke(ps, null);
                if (theme < RotEventThemeMin || theme > RotEventThemeMax) return;
                int seg = (int)_mSegId.Invoke(ps, null);
                object r = _mStopMusic.Invoke(ps, new object[] { true, RotThemeFadeSec });
                _thStops++;
                Log.Info(P + "motyw wydarzenia ROT " + theme + " (odcinek " + seg + ") zatrzymany po oknie - StopMusic " + RotThemeFadeSec.ToString("0", Inv) + " s, wynik "
                         + (r != null ? r.ToString() : "?") + "; muzyka mapy wraca po przerwie gry (zatrzyman w sesji " + _thStops + ").");
            }
            catch (Exception e) { Stumble("EventThemeStop", e); }
        }
    }
}
