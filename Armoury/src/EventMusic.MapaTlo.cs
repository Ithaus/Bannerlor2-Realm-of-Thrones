using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Campaign = TaleWorlds.CampaignSystem.Campaign;
using GameStateManager = TaleWorlds.Core.GameStateManager;
using LoadingWindow = TaleWorlds.Engine.LoadingWindow;
using MapState = TaleWorlds.CampaignSystem.GameState.MapState;
using MBCommon = TaleWorlds.MountAndBlade.MBCommon;
using Mission = TaleWorlds.MountAndBlade.Mission;
using MissionState = TaleWorlds.MountAndBlade.MissionState;
using Scene = TaleWorlds.Engine.Scene;
using SoundEvent = TaleWorlds.Engine.SoundEvent;   // bez using TaleWorlds.Engine - jego Path zaslania System.IO.Path

namespace Armoury
{
    /// <summary>
    /// Paczka 165 (m165-mapa; Jeff 26.10 ok. 18:30: "zrob mi jeszcze 10 utworow na mape kampanii w stylu gry o tron lub sredniowiecza minimum 2 minuty dlugosci
    /// a najlepiej taka muzyka ktora nie jest zbyt intensywna i nie rozprasza ale jako dobry podklad"). Klucz MapMusicOurs ("Our Map Music", TAK), czytany na zywo.
    ///  - Kiedy: mapa kampanii - jest Campaign, brak Mission.Current i MissionState.Current (bitwa, oblezenie, spacer po miescie / zamku / wsi, turniej, arena, kryjowka, morze), brak
    ///    ekranu ladowania (LoadingWindow.IsLoadingWindowActive), aktywny stan gry MapState albo ekran kampanii nad mapa (ekwipunek, druzyna, klan, krolestwo,
    ///    postac, zadania, kuznia, herb, fryzjer, nauka, port - MapScreens; utwor gra dalej bez restartu). Menu osad i spotkan (GameMenu) oraz rozmowy na mapie
    ///    (MapConversationView - ICampaignMission, nie Mission) to dalej MapState - utwor gra dalej; pauza czasu tez. Tworzenie postaci, filmy, okno ROT
    ///    (EventPopupState), stan nieznany - nie mapa. Sprawdzane co klatke (same odczyty, bez alokacji - typ stanu raz na zmiane), logika utworu 4 razy na sekunde.
    ///  - Pierwszenstwo: okno wydarzenia / plotki (m108) > bitwa (m109) > mapa. Jeden wlasciciel glosnosci psai jak w oknach i bitwie (_ducked, _saved, _cur):
    ///    przez caly czas mapy (utwor i przerwa miedzy utworami) psai trzymane na 0 - przerwa to cisza, nie muzyka gry. Zanik psai jak okna (FadeDown 1,2 s),
    ///    potem trzymanie 0 co 0,25 s (gra nie podnosi psai sama - PsaiCore.SetVolume wola tylko nasz kod; odczyt przed zapisem).
    ///  - Utwory: ModuleData/Lektor/Muzyka/Mapa/lufs{17,20,23,26}/map_NN_nazwa.ogg (tools/muzyka/bitwa_v2.py --prefiks map: samo wzmocnienie z mp3 ElevenLabs,
    ///    wejscie 4 s i wyjscie 6 s w zrodle). Glosnosc: o jeden stopien ciszej niz muzyka bitwy (suwak BattleMusicLoudness: 4 -> -20, 3 -> -23, 2 i 1 -> -26 LUFS)
    ///    - tlo pod gra, nie nad zgielkiem bitwy; zestaw jak w bitwie (PickFrom: pierwszy pelny 10, inaczej najblizszy niepusty). Zdarzenie event:/Extra/external
    ///    jak okna i bitwa, w tym samym gniezdzie _mus (nastroj "mapa") - okno wydarzenia i bitwa zatrzymuja go jak kazdy inny utwor (nigdy dwa naraz).
    ///  - Kolejnosc: worek - kazdy utwor zestawu raz, potem nowy worek; nigdy ten sam dwa razy z rzedu (takze na granicy workow i po wczytaniu w tej samej sesji),
    ///    chyba ze zestaw ma jeden utwor ("jedyny - powtorka"). Po utworze cisza 20-40 s (losowo). Koniec utworu: silnik (!IsValid / IsStopped) albo czas pliku + 5 s.
    ///  - Przekazania: okno wydarzenia / plotki - utwor mapy stop od razu (SoundEvent nie ma glosnosci - dekompilacja TaleWorlds.Engine.SoundEvent: Play, Stop,
    ///    Pause, SetParameter, bez SetVolume; zanik 2 s niemozliwy na tej drodze odtwarzania; recenzja 165: kanaly TaleWorlds.Engine.Music - GetFreeMusicChannelIndex,
    ///    LoadClip, PlayMusic, SetVolume(int, float) - maja glosnosc (tak gra psai), ale to inna droga odtwarzania, niesprawdzona w grze (wolny kanal obok 9 kanalow
    ///    psai, stop kanalow przy zmianie stanu gry) - decyzja Jeffa), nastepny utwor ok. 3 s po zamknieciu okna (0,6 s zapasu m108 wliczone); misja - stop od razu,
    ///    psai wraca (FadeUp 2 s; w bitwie przejmuje m109), po powrocie na mape cisza 5-10 s i nastepny; klucz NIE / inny stan - stop, psai wraca. Ekran
    ///    ladowania bez misji (np. powrot z bitwy na mape) - psai trzymane bez FadeUp, mapa ani utwor sie nie zmieniaja (EventMusic.Step loadHold); decyzja po ladowaniu.
    ///    Nowa kampania (takze wczytanie zapisu) - stan od zera, pierwszy utwor po 5 s mapy. Nic nie jest zapisywane w zapisie gry.
    ///  - Linie "Muzyka mapy (m165): ..." - start kampanii (biblioteka wedlug zestawow, dlugosci, brak numerow), mapa wlaczona / stop (powod), muzyka gry wyciszona /
    ///    wrocila (glosnosc psai), kazdy utwor (zestaw, powod: start / po przerwie / po oknie / po misji, worek), koniec albo przerwanie utworu (czas, grany po 1 s,
    ///    nastepny za N s); bledy: pierwszy wyjatek z opisem, reszta liczona; po 3 bledach z rzedu rezygnacja do nastepnej misji albo wczytania.
    ///    tools/sprawdz_logi.py --grupa m165mapa.
    /// </summary>
    internal static partial class EventMusic
    {
        internal const string PM = "Muzyka mapy (m165): ";
        private const string MapMoodTag = "mapa";
        private const double MapGapMin = 20.0, MapGapMax = 40.0, MapAfterWindowSec = 3.0, MapAfterMissionMin = 5.0, MapAfterMissionMax = 10.0, MapStartSec = 5.0;
        private const double MapTickSec = 0.25, MapHoldSec = 0.25, MapRetrySec = 5.0;
        private const int MapFailMax = 3, MapLufsQuietest = 26, MapRejectLog = 3;
        private const string RotEventStateName = "ROT.CampaignBehaviors.EventPopupState";
        // ekrany kampanii nad mapa (TaleWorlds.CampaignSystem.GameState; PortState - NavalDLC przez CampaignSystem) - utwor gra dalej
        private static readonly string[] MapScreens = { "MapState", "InventoryState", "PartyState", "ClanState", "KingdomState", "CharacterDeveloperState", "QuestsState",
                                                        "CraftingState", "BannerEditorState", "BarberState", "EducationState", "PortState" };
        private static readonly object[] ZeroVol = { 0f };   // trzymanie 0 bez nowej tablicy

        // ---- biblioteka (raz na sesje)
        private static Dictionary<int, List<BTrack>> _mapSets;
        private static string _mapDir;
        private static int _mapBadFiles;
        private static readonly Random _mapRng = new Random();

        // ---- stan
        private static bool _mapOn, _duckMap, _mapDuckLogged, _mapWinHeld, _mapGaveUp, _mapNoReady;
        private static double _mapGap = MapStartSec, _mapAcc, _mapHoldAcc;
        private static string _mapNextWhy = "start", _mapOffWhy = "?", _mapOffState = "", _mapLast;
        private static Type _mapStType;
        private static bool _mapStOk, _mapStWin;
        private static readonly HashSet<string> _mapBagPlayed = new HashSet<string>(StringComparer.Ordinal);
        // biezacy utwor
        private static BTrack _mapCur;
        private static int _mapCurLufs, _mapN, _mapFails, _mapStumbles, _mapRejects;
        private static double _mapT0, _mapPlayT;
        private static bool _mapChecked, _mapPlayed1s;

        private static bool MapKeyOn
        {
            get { var s = Settings.Current; return s == null || s.MapMusicOurs; }
        }

        /// <summary>Zestaw mapy: o jeden stopien ciszej niz muzyka bitwy (17 + 3 * (4 - poziom) + 3), najciszej -26 LUFS.</summary>
        private static int MapLufs() { return Math.Min(MapLufsQuietest, LufsOf(Level()) + 3); }

        private static List<BTrack> PickMapSet(out int got) { return PickFrom(_mapSets, MapLufs(), out got); }

        // ============================================================ start kampanii (BattleStartCheck - raz na obiekt Campaign)
        /// <summary>Nowa kampania (takze wczytanie zapisu): stan mapy od zera, pierwszy utwor po 5 s mapy; linia biblioteki. Nie rzuca.</summary>
        private static void MapCampaignStart()
        {
            try
            {
                _mapGap = MapStartSec; _mapNextWhy = "start"; _mapGaveUp = false; _mapFails = 0; _mapWinHeld = false;
                _mapBagPlayed.Clear();   // _mapLast zostaje - po wczytaniu nie ten sam utwor co ostatnio
                if (!MapKeyOn)
                {
                    Log.Info(PM + "start - klucz MapMusicOurs NIE - mapa z muzyka gry.");
                    return;
                }
                LoadMapTracks();
                PsaiResolve();
                int got;
                var set = PickMapSet(out got);
                var sb = new StringBuilder(PM + "start - klucz MapMusicOurs TAK, glosnosc bitwy " + Level() + " -> mapa -" + MapLufs() + " LUFS (zestaw lufs" + got + "), utwory mapy");
                for (int i = 0; i < BattleLufs.Length; i++)
                {
                    List<BTrack> l;
                    sb.Append(i == 0 ? " " : ", ").Append("lufs").Append(BattleLufs[i]).Append(' ').Append(_mapSets.TryGetValue(BattleLufs[i], out l) ? l.Count : 0);
                }
                sb.Append(" (");
                if (set != null)
                    for (int i = 0; i < set.Count; i++) sb.Append(i == 0 ? "" : ", ").Append(set[i].Name).Append(' ').Append(DurText(set[i].Dur));
                sb.Append("), brak numerow ");
                int miss = 0;
                for (int n = 1; n <= 10; n++)
                {
                    bool has = false;
                    if (set != null) foreach (var t in set) if (t.Num == n) { has = true; break; }
                    if (has) continue;
                    sb.Append(miss == 0 ? "" : " ").Append(n.ToString("00", Inv));
                    miss++;
                }
                if (miss == 0) sb.Append('-');
                sb.Append(", odrzucone ").Append(_mapBadFiles).Append(", psai ").Append(_psaiOk ? "TAK" : "NIE").Append(", przerwa ").Append(MapGapMin.ToString("0", Inv)).Append('-')
                  .Append(MapGapMax.ToString("0", Inv)).Append(" s, po oknie ").Append(MapAfterWindowSec.ToString("0", Inv)).Append(" s, po misji ")
                  .Append(MapAfterMissionMin.ToString("0", Inv)).Append('-').Append(MapAfterMissionMax.ToString("0", Inv)).Append(" s, pierwszy po ")
                  .Append(MapStartSec.ToString("0", Inv)).Append(" s.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { MapStumble("Start", e); }
        }

        // ============================================================ czy mapa (AppTick, co klatke - same odczyty, bez alokacji)
        private static bool MapWanted()
        {
            try
            {
                if (!MapKeyOn) { _mapOffWhy = "klucz NIE"; return false; }
                if (Campaign.Current == null) { _mapOffWhy = "wyjscie z kampanii"; return false; }
                if (Mission.Current != null || MissionState.Current != null)   // MissionState od PushState (ekran ladowania misji, Mission.Current jeszcze null) do PopState
                {
                    _mapOffWhy = "misja";
                    if (_mapGaveUp) { _mapGaveUp = false; _mapFails = 0; }   // rezygnacja trwa do nastepnej misji (albo wczytania)
                    return false;
                }
                if (_mapGaveUp) { _mapOffWhy = "rezygnacja"; return false; }
                if (!MapReady()) { _mapOffWhy = "brak psai albo utworow"; return false; }   // przed ekranem ladowania - bez utworow psai dokladnie jak dotad (bez loadHold)
                if (LoadingWindow.IsLoadingWindowActive) { _mapOffWhy = "ekran ladowania"; return false; }
                var gsm = GameStateManager.Current;
                var st = gsm != null ? gsm.ActiveState : null;
                if (st == null) { _mapOffWhy = "brak stanu gry"; return false; }
                Type t = st.GetType();
                if (!ReferenceEquals(t, _mapStType))
                {
                    _mapStType = t;
                    _mapStOk = Array.IndexOf(MapScreens, t.Name) >= 0 || typeof(MapState).IsAssignableFrom(t);   // takze stan mapy moda dziedziczacy po MapState
                    _mapStWin = t.FullName == RotEventStateName;
                    _mapOffState = t.Name;
                }
                if (!_mapStOk) { _mapOffWhy = _mapStWin ? "okno wydarzenia" : "stan gry"; return false; }
                return true;
            }
            catch (Exception e) { MapStumble("Wanted", e); MapFail("Wanted"); _mapOffWhy = "blad"; return false; }
        }

        /// <summary>psai i utwory mapy sa; brak - linia raz na sesje (mapa z muzyka gry).</summary>
        private static bool MapReady()
        {
            PsaiResolve();
            if (!_psaiOk)
            {
                if (!_mapNoReady) { _mapNoReady = true; Log.Info(PM + "psai NIE - mapa z muzyka gry."); }
                return false;
            }
            LoadMapTracks();
            int got;
            if (PickMapSet(out got) == null)
            {
                if (!_mapNoReady) { _mapNoReady = true; Log.Info(PM + "brak utworow mapy w " + (_mapDir ?? "?") + " - mapa z muzyka gry."); }
                return false;
            }
            return true;
        }

        // ============================================================ mapa (EventMusic.Step, galaz want - bez okna, plotek i bitwy)
        private static void MapStep(float dt)
        {
            _idle += dt;
            if (_active && _idle >= ReleaseDelay)
            {   // okno / plotki / bitwa skonczone, jestesmy na mapie: wyciszenie psai przejmuje mapa (bez FadeUp)
                MapRelease();
            }
            if (_duckBattle && !_batActive)
            {   // recenzja 165: bitwa skonczona poza mapa (ekran ladowania, FadeUp nie skonczyl) - wyciszenie przejmuje mapa: linia m109 zamiast "wrocila" (sprawdz_logi
                // m109muz MUZ-3); co klatke mapy, nie tylko przy wlaczeniu (mapa mogla wlaczyc sie klatke przed ekranem ladowania, jeszcze przy aktywnej bitwie)
                _duckBattle = false;
                Log.Info(PB + "muzyka gry dalej wyciszona - przejmuje muzyka mapy.");
            }
            if (!_mapOn) MapOn();
            if (_mus != null && _musMood != MapMoodTag && !_active) StopTrack(null);   // obcy utwor po przejeciu (nie powinien zostac) - nigdy dwa naraz
            _mapAcc += dt;
            bool tick = _mapAcc >= MapTickSec;
            MapHold(dt, tick);
            if (!tick) return;
            float step = (float)_mapAcc;
            _mapAcc = 0;
            if (_active) return;   // utwor okna wybrzmiewa jeszcze 0,6 s (zapas m108 na kolejne okno)
            if (_mapWinHeld)
            {   // pierwszy takt po oknie: nastepny utwor ok. 3 s po zamknieciu okna (zapas 0,6 s wliczony)
                _mapWinHeld = false;
                _mapGap = Math.Max(0.0, MapAfterWindowSec - _idle);
                _mapNextWhy = "po oknie";
            }
            if (_mus != null && _musMood == MapMoodTag) MapCheck(step);
            else
            {
                _mapGap -= step;
                if (_mapGap <= 0) MapNext();
            }
        }

        /// <summary>Koniec okna / plotek / bitwy przy wlaczonej mapie (MapStep albo ekran ladowania nad mapa - EventMusic.Step loadHold): wyciszenie psai przejmuje
        /// mapa bez FadeUp; linia konca bitwy "muzyka gry dalej wyciszona (muzyka mapy)" albo "koniec wydarzenia - mapa dalej".</summary>
        private static void MapRelease()
        {
            _active = false;
            _paused = false;
            if (_batActive) { BattleEnd(_batWhy, false, true); _duckBattle = false; }
            else
            {
                StopTrack("koniec wydarzenia - mapa dalej");
                Log.Info(P + "koniec wydarzenia - mapa dalej, muzyka gry dalej wyciszona (nasza muzyka mapy).");
                EventThemeStop();   // m164: motyw wydarzenia ROT na warstwie psai stop (psai i tak trzymane na 0)
            }
        }

        /// <summary>Wlaczenie mapy: wyciszenie psai (jak okna), linia z zestawem i czasem do pierwszego utworu.</summary>
        private static void MapOn()
        {
            bool was = _ducked;
            _mapOn = true; _mapDuckLogged = false; _mapAcc = 0; _mapHoldAcc = 0;
            DuckStart();
            _duckMap = true;   // przejecie wyciszenia po bitwie - MapStep (recenzja 165)
            int got;
            var set = PickMapSet(out got);
            Log.Info(PM + "mapa - nasza muzyka, muzyka gry " + (was ? "dalej wyciszona" : "cichnie") + " (psai " + (_psaiOk && _ducked ? "glosnosc zapamietana "
                     + _saved.ToString("0.00", Inv) : "BRAK") + "), zestaw lufs" + got + " (" + (set != null ? set.Count : 0) + " utworow), "
                     + (_active ? "po koncu okna / bitwy, " : "") + "nastepny za " + _mapGap.ToString("0", Inv) + " s (" + _mapNextWhy + ").");
        }

        /// <summary>Psai na 0: zanik jak okna (FadeDown co klatke, 1,2 s), potem odczyt i zapis 0 co 0,25 s (bez alokacji tablicy); linia po pierwszym 0.</summary>
        private static void MapHold(float dt, bool tick)
        {
            if (!_ducked)
            {
                if (tick) DuckStart();   // instancja psai jeszcze nie gotowa - proba co 0,25 s
                return;
            }
            if (_cur > 0f) { FadeDown(dt); return; }
            _mapHoldAcc += dt;
            if (_mapHoldAcc < MapHoldSec && _mapDuckLogged) return;
            _mapHoldAcc = 0;
            object ps = Psai();
            if (ps == null) return;
            float v = (float)_mGetVol.Invoke(ps, null);
            if (v > 0.001f) _mSetVol.Invoke(ps, ZeroVol);
            if (!_mapDuckLogged)
            {
                _mapDuckLogged = true;
                float now = (float)_mGetVol.Invoke(ps, null);
                Log.Info(PM + "muzyka gry wyciszona (psai glosnosc teraz " + now.ToString("0.00", Inv) + ", zapamietana " + _saved.ToString("0.00", Inv) + ").");
            }
        }

        // ------------------------------------------------------------ utwor
        /// <summary>Losowanie z worka: utwory zestawu jeszcze nie grane w tym worku i inne niz ostatni; worek pusty - nowy (bez ostatniego); zestaw z jednym - powtorka.</summary>
        private static BTrack MapDraw(List<BTrack> set, out string bag)
        {
            var c = new List<BTrack>();
            foreach (var t in set) if (t.Name != _mapLast && !_mapBagPlayed.Contains(t.Name)) c.Add(t);
            string note = "";
            if (c.Count == 0)
            {
                _mapBagPlayed.Clear();
                foreach (var t in set) if (t.Name != _mapLast) c.Add(t);
                note = ", nowy worek";
            }
            if (c.Count == 0) { c.AddRange(set); note = ", jedyny - powtorka"; }
            var pick = c[_mapRng.Next(c.Count)];
            _mapBagPlayed.Add(pick.Name);
            int inBag = 0;
            foreach (var t in set) if (_mapBagPlayed.Contains(t.Name)) inBag++;
            bag = inBag + "/" + set.Count + note;
            return pick;
        }

        /// <summary>Nastepny utwor z worka przez event:/Extra/external (jak okna i bitwa). Odrzucony - ponowna proba za 5 s, po 3 bledach z rzedu rezygnacja.</summary>
        private static void MapNext()
        {
            int got;
            var set = PickMapSet(out got);
            if (set == null || set.Count == 0) { _mapGap = MapRetrySec; return; }
            string bag;
            BTrack pick = MapDraw(set, out bag);
            SoundEvent ev = null;
            bool ok = false;
            try
            {
                ev = SoundEvent.CreateEventFromExternalFile(MusicEvent, pick.Path, (Scene)null, false, false);
                ok = ev != null && ev.IsValid && ev.Play();
            }
            catch (Exception e) { MapStumble("Next", e); }
            if (!ok)
            {
                _mapRejects++;
                try { if (ev != null && ev.IsValid) ev.Release(); } catch { }
                if (_mapRejects <= MapRejectLog) Log.Info(PM + "silnik nie przyjal " + pick.File + " (zdarzenie " + MusicEvent + ") - proba za " + MapRetrySec.ToString("0", Inv) + " s.");
                _mapGap = MapRetrySec;
                MapFail("Next");
                return;
            }
            _mapLast = pick.Name;
            _mus = ev; _musMood = MapMoodTag; _musFile = pick.File;
            _mapCur = pick; _mapCurLufs = got; _mapT0 = Now; _mapPlayT = 0; _mapChecked = false; _mapPlayed1s = false; _mapN++;
            Log.Info(PM + "utwor " + _mapN + " - " + pick.File + " (lufs" + got + ", " + DurText(pick.Dur) + ", " + _mapNextWhy + ", worek " + bag + ").");
        }

        /// <summary>Biezacy utwor (co 0,25 s): kontrola po 1 s gry, koniec - silnik albo czas pliku + 5 s; potem cisza 20-40 s. Czas gry bez pauzy gry i zdarzenia.</summary>
        private static void MapCheck(float step)
        {
            var ev = _mus;
            var cur = _mapCur;
            if (cur == null) { StopTrack(null); return; }
            bool stopped = false, paused = false;
            try { stopped = !ev.IsValid || ev.IsStopped(); if (!stopped) paused = ev.IsPaused(); }
            catch (Exception e) { MapStumble("Check", e); stopped = true; }
            if (!stopped && !paused && !MBCommon.IsPaused) _mapPlayT += step;
            if (!_mapChecked && !stopped && _mapPlayT >= CheckSec)
            {
                _mapChecked = true;
                try { _mapPlayed1s = ev.IsPlaying(); } catch (Exception e) { MapStumble("IsPlaying", e); }
            }
            string why = null;
            if (stopped) why = "silnik";
            else if (_mapPlayT >= (cur.Dur > 0 ? cur.Dur + CapExtraSec : CapUnknownSec)) why = "po czasie pliku - silnik nie zglosil";
            if (why == null) return;
            bool notPlayed = stopped && !_mapChecked && Now - _mapT0 < NotPlayedRealSec;   // silnik skonczyl od razu - plik nie zagral
            _mapGap = notPlayed ? MapRetrySec : MapGapMin + _mapRng.NextDouble() * (MapGapMax - MapGapMin);
            _mapNextWhy = "po przerwie";
            MapEndPiece("koniec", notPlayed ? "nie grany - silnik skonczyl" : why, "nastepny za " + _mapGap.ToString("0", Inv) + " s");
            if (notPlayed) MapFail("NotPlayed");
            else if (_mapChecked && _mapPlayed1s) _mapFails = 0;   // bledy liczone z rzedu
        }

        /// <summary>Stop utworu mapy (StopRelease jak okna i bitwa) i linia konca / przerwania. Bez utworu mapy - nic.</summary>
        private static void MapEndPiece(string how, string why, string next)
        {
            var ev = _mus;
            var cur = _mapCur;
            if (ev == null || _musMood != MapMoodTag || cur == null) return;
            _mus = null; _mapCur = null;
            string st = StopRelease(ev, false);
            Log.Info(PM + how + " " + cur.File + " po " + _mapPlayT.ToString("0.0", Inv) + " s (" + why + "), grany po 1 s " + (_mapChecked ? (_mapPlayed1s ? "TAK" : "NIE") : "-")
                     + ", zdarzenie przy stopie " + st + "; " + next + ".");
        }

        // ------------------------------------------------------------ przekazania
        /// <summary>Okno wydarzenia / plotki przy wlaczonej mapie (co klatke okna): utwor mapy stop od razu, po oknie nastepny za ok. 3 s; wyciszenie zostaje.</summary>
        private static void MapYield(string why)
        {
            _mapWinHeld = true;
            if (_mus != null && _musMood == MapMoodTag) MapEndPiece("przerwany", why, "nastepny po oknie");
        }

        /// <summary>Koniec mapy (misja, klucz NIE, ekran ladowania, inny stan, rezygnacja): utwor stop od razu, linia; psai wraca w wolajacym (FadeUp) albo przejmuje bitwa.</summary>
        private static void MapOff(string why, string tail)
        {
            if (!_mapOn) return;
            _mapOn = false; _mapWinHeld = false; _mapAcc = 0;
            MapEndPiece("przerwany", why, "mapa stop");
            if (why == "misja") { _mapGap = MapAfterMissionMin + _mapRng.NextDouble() * (MapAfterMissionMax - MapAfterMissionMin); _mapNextWhy = "po misji"; }
            else if (why == "okno wydarzenia") { _mapGap = MapAfterWindowSec; _mapNextWhy = "po oknie"; }
            else { _mapGap = MapStartSec; _mapNextWhy = "start"; }
            Log.Info(PM + "mapa - stop (" + why + (why == "stan gry" ? " " + _mapOffState : "") + ") - " + tail + ".");
        }

        /// <summary>Bitwa przejmuje wyciszenie psai po mapie (mapa juz zatrzymana, FadeUp nie skonczyl): linia zamiast "wrocila".</summary>
        private static void MapHandOver(string who)
        {
            if (!_duckMap) return;
            _duckMap = false;
            Log.Info(PM + "muzyka gry dalej wyciszona - przejmuje " + who + ".");
        }

        /// <summary>Wyciszenie po mapie zdjete (FadeUp do konca albo RestoreNow): linia z glosnoscia psai teraz i zapamietana.</summary>
        private static void MapUnducked(object ps, string why)
        {
            if (!_duckMap) return;
            _duckMap = false;
            string got = "?";
            try { if (ps != null) got = ((float)_mGetVol.Invoke(ps, null)).ToString("0.00", Inv); } catch (Exception e) { MapStumble("Unducked", e); }
            Log.Info(PM + "muzyka gry wrocila" + (why != null ? " od razu (" + why + ")" : "") + " - psai glosnosc " + got + " (zapamietana " + _saved.ToString("0.00", Inv) + ").");
        }

        /// <summary>RestoreNow (wyjscie z kampanii, blad): mapa stop, stan od zera (biblioteka zostaje); psai przywraca RestoreNow.</summary>
        private static void MapReset(string why)
        {
            if (_mapOn) MapOff(why, "muzyka gry wraca od razu");
            else MapEndPiece("przerwany", why, "mapa stop");   // utwor mapy bez wlaczonej mapy - nie powinien byc
            _mapOn = false; _mapWinHeld = false; _mapAcc = 0; _mapHoldAcc = 0; _mapCur = null;
            _mapGap = MapStartSec; _mapNextWhy = "start";
        }

        // ------------------------------------------------------------ bledy: pierwszy z opisem, reszta liczona; 3 z rzedu - rezygnacja do nastepnej misji / wczytania
        private static void MapStumble(string where, Exception e)
        {
            _mapStumbles++;
            if (_mapStumbles == 1) Log.Error("EventMusic.MapaTlo." + where + " (potkniecie 1; kolejne tylko liczone)", e);
        }

        private static void MapFail(string where)
        {
            _mapFails++;
            if (_mapFails < MapFailMax || _mapGaveUp) return;
            _mapGaveUp = true;
            Log.Info(PM + "rezygnacja po " + _mapFails + " bledach z rzedu (" + where + ", potkniec w sesji " + _mapStumbles + ") - muzyka gry wraca, mapa od nowa po misji albo wczytaniu.");
        }

        // ------------------------------------------------------------ utwory mapy (raz na sesje)
        private static void LoadMapTracks()
        {
            if (_mapSets != null) return;
            _mapSets = new Dictionary<int, List<BTrack>>();
            try
            {
                _mapDir = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Lektor.ModuleDir(), "ModuleData"), "Lektor"), "Muzyka"), "Mapa");
                foreach (int lu in BattleLufs)
                {
                    var list = new List<BTrack>();
                    _mapSets[lu] = list;
                    string d = Path.Combine(_mapDir, "lufs" + lu.ToString(Inv));
                    if (!Directory.Exists(d)) continue;
                    var files = Directory.GetFiles(d, "map_*.ogg");
                    Array.Sort(files, StringComparer.Ordinal);
                    foreach (var f in files)
                    {
                        bool ogg = false;
                        double dur = 0;
                        try { if (new FileInfo(f).Length > 4096) dur = OggSeconds(f, out ogg); }
                        catch (Exception e) { MapStumble("OggSeconds", e); }
                        if (!ogg) { _mapBadFiles++; continue; }   // pusty, wyzerowany albo nie Ogg
                        string name = Path.GetFileNameWithoutExtension(f).Substring(4);   // "01_krolewski_trakt"
                        int num = 0;
                        if (name.Length >= 2 && char.IsDigit(name[0]) && char.IsDigit(name[1]) && (name.Length == 2 || name[2] == '_')) num = (name[0] - '0') * 10 + (name[1] - '0');
                        list.Add(new BTrack { Name = name, File = Path.GetFileName(f), Path = f, Dur = dur, Num = num });
                    }
                }
            }
            catch (Exception e) { MapStumble("LoadMapTracks", e); }
        }
    }
}
