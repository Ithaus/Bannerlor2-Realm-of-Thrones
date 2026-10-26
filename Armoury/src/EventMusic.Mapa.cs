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
    ///    gdy na warstwie psai gra motyw wydarzenia ROT - wyciszenie tej warstwy w 3 s, zanim glosnosc psai wroci (FadeUp 2 s); mapa wraca do swoich motywow
    ///    po przerwie gry (30-120 s; ROT przy otwarciu okna dal StopMusic - tryb ciszy). Dziala tylko z oknami sledzonymi przez m108 (klucz EventMusicEnabled TAK).
    ///    Recenzja 164 (dekompilacja TaleWorlds.PSAI 1.4.8): PsaiCore.PlaySegment -> Logik.PlaySegmentLayeredAndImmediately gra odcinek na glosie warstwy 7/8
    ///    (getNextVoiceNumber(forHighlight), glosy 0-6 to zwykle motywy) i NIE ustawia m_effectiveTheme - GetCurrentThemeId nigdy nie daje 402-415, a StopMusic
    ///    wycisza tylko glos glowny (startFade(m_currentVoiceNumber)). Dlatego odbicie na pola prywatne: PsaiCore.m_logik -> Logik.m_playbackChannels[7..8],
    ///    kanal IsPlaying() i Segment (ThemeId 402-415 = ThemeTypeInt 7 w ROT-Content/music/soundtrack.xml, odcinki 418-431), stop przez prywatne
    ///    Logik.startFade(glos, 3000, 0) - updateFades zbija FadeOutVolume i przy 0 StopChannel + ReleaseSegment (droga gry); glos glowny mapy nietkniety.
    ///    Watek: Logik.Update (updateFades) wola MBMusicManager.Update w glownym watku, nasz AppTick tez - bez wyscigu na m_fadeVoices.
    ///  - Stop naszych utworow (okna i bitwa): StopRelease - SoundEvent.Release wola Stop tylko dla IsPlaying (dekompilacja SoundEvent.Release), zdarzenie
    ///    spauzowane / startujace dostaloby samo ReleaseEvent (ryzyko H-stop z raportu: moglo przezyc wyjscie z misji); tu najpierw Stop, potem Release jak w grze.
    ///  - Linie "Muzyka (m108): mapa - ..." (start kampanii), "motyw wydarzenia ROT N (odcinek S) gra w oknie (warstwa psai, glos V)." (przy otwarciu okna,
    ///    klucz TAK), "motyw wydarzenia ROT ... zatrzymany po oknie - wyciszenie warstwy psai (glos V) 3 s; ..." (kazde zatrzymanie) albo, gdy motyw widziany
    ///    w oknie juz nie gra, "motyw wydarzenia ROT ... nie gra po oknie - bez zatrzymania." - tools/sprawdz_logi.py m109muz MUZ-8 paruje te linie.
    /// </summary>
    internal static partial class EventMusic
    {
        private const int RotEventThemeMin = 402, RotEventThemeMax = 415;
        private const float RotThemeFadeSec = 3f;
        private const int RotThemeFadeMs = 3000;
        private const int PsaiFirstLayerVoice = 7;   // Logik: glosy 0-6 zwykle motywy, 7-8 warstwa (PlaySegment / highlight)
        private const string MapWarMood = "wojna", MapWarInstead = "korona";

        private static string _stopState = "-";
        private static bool _thTried, _thOk;
        private static FieldInfo _fLogik, _fChannels;
        private static MethodInfo _mStartFade, _mChPlaying;
        private static PropertyInfo _pChSeg, _pSegId, _pSegTheme;
        private static int _thStops;
        private static int _thSeenTheme = -1, _thSeenSeg = -1;   // motyw ROT widziany przy otwarciu okna (para do linii konca okna)

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
                     + (ThemeKeyOn ? "TAK" : "NIE") + " (motywy wydarzen ROT " + RotEventThemeMin + "-" + RotEventThemeMax + " na warstwie psai wyciszane "
                     + RotThemeFadeSec.ToString("0", Inv) + " s po oknie), psai motywy " + (_thOk ? "TAK" : "NIE") + ", klucz EventMusicEnabled " + (KeyOn ? "TAK" : "NIE") + ".");
        }

        private static void ThemeResolve()
        {
            if (_thTried) return;
            _thTried = true;
            try
            {
                Type t = Type.GetType("psai.net.PsaiCore, TaleWorlds.PSAI");
                if (t == null) return;
                Assembly a = t.Assembly;
                Type tl = a.GetType("psai.net.Logik"), tc = a.GetType("psai.net.PlaybackChannel"), ts = a.GetType("psai.net.Segment");
                if (tl == null || tc == null || ts == null) return;
                const BindingFlags np = BindingFlags.NonPublic | BindingFlags.Instance, any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _fLogik = t.GetField("m_logik", np);
                _fChannels = tl.GetField("m_playbackChannels", np);
                _mStartFade = tl.GetMethod("startFade", np, null, new[] { typeof(int), typeof(int), typeof(int) }, null);
                _mChPlaying = tc.GetMethod("IsPlaying", any, null, Type.EmptyTypes, null);
                _pChSeg = tc.GetProperty("Segment", any);
                _pSegId = ts.GetProperty("Id", any);
                _pSegTheme = ts.GetProperty("ThemeId", any);
                _thOk = _fLogik != null && _fChannels != null && _fChannels.FieldType.IsArray && _mStartFade != null && _mChPlaying != null && _pChSeg != null
                        && _pSegId != null && _pSegTheme != null;
            }
            catch (Exception e) { Stumble("ThemeResolve", e); _thOk = false; }
        }

        /// <summary>Kanaly warstwy psai (glosy 7..8) z grajacym motywem wydarzenia ROT: (glos, motyw, odcinek). Logik do startFade w lg. Rzuca (wolajacy lapie).</summary>
        private static System.Collections.Generic.List<int[]> LayerThemes(out object lg)
        {
            var res = new System.Collections.Generic.List<int[]>();
            lg = null;
            ThemeResolve();
            if (!_thOk) return res;
            object ps = Psai();
            if (ps == null) return res;
            lg = _fLogik.GetValue(ps);
            var chs = lg != null ? _fChannels.GetValue(lg) as Array : null;
            if (chs == null) return res;
            for (int v = PsaiFirstLayerVoice; v < chs.Length; v++)
            {
                object ch = chs.GetValue(v);
                object seg = ch != null ? _pChSeg.GetValue(ch, null) : null;
                if (seg == null) continue;
                int theme = (int)_pSegTheme.GetValue(seg, null);
                if (theme < RotEventThemeMin || theme > RotEventThemeMax) continue;
                if (!(bool)_mChPlaying.Invoke(ch, null)) continue;
                res.Add(new[] { v, theme, (int)_pSegId.GetValue(seg, null) });
            }
            return res;
        }

        /// <summary>Start okna wydarzenia / plotek poza bitwa (Step, pierwsza klatka): motyw ROT na warstwie psai - linia i zapamietanie (para do konca okna). Nie rzuca.</summary>
        private static void EventThemeSeen()
        {
            _thSeenTheme = _thSeenSeg = -1;
            if (!ThemeKeyOn) return;
            try
            {
                object lg;
                var found = LayerThemes(out lg);
                if (found.Count == 0) return;
                int[] f = found[found.Count - 1];
                _thSeenTheme = f[1]; _thSeenSeg = f[2];
                Log.Info(P + "motyw wydarzenia ROT " + f[1] + " (odcinek " + f[2] + ") gra w oknie (warstwa psai, glos " + f[0] + ").");
            }
            catch (Exception e) { Stumble("EventThemeSeen", e); }
        }

        /// <summary>Koniec okna wydarzenia / plotek poza bitwa (Step): motyw wydarzenia ROT na warstwie psai - wyciszenie 3 s (Logik.startFade). Nie rzuca.</summary>
        private static void EventThemeStop()
        {
            int seenTheme = _thSeenTheme, seenSeg = _thSeenSeg;
            _thSeenTheme = _thSeenSeg = -1;
            if (!ThemeKeyOn) return;
            try
            {
                object lg;
                var found = LayerThemes(out lg);
                foreach (var f in found)
                {
                    _mStartFade.Invoke(lg, new object[] { f[0], RotThemeFadeMs, 0 });
                    _thStops++;
                    Log.Info(P + "motyw wydarzenia ROT " + f[1] + " (odcinek " + f[2] + ") zatrzymany po oknie - wyciszenie warstwy psai (glos " + f[0] + ") "
                             + RotThemeFadeSec.ToString("0", Inv) + " s; muzyka mapy wraca po przerwie gry (zatrzyman w sesji " + _thStops + ").");
                }
                if (found.Count == 0 && seenTheme >= 0)
                    Log.Info(P + "motyw wydarzenia ROT " + seenTheme + " (odcinek " + seenSeg + ") nie gra po oknie - bez zatrzymania.");
            }
            catch (Exception e) { Stumble("EventThemeStop", e); }
        }
    }
}
