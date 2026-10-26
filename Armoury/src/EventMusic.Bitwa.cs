using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Campaign = TaleWorlds.CampaignSystem.Campaign;
using MapEvent = TaleWorlds.CampaignSystem.MapEvents.MapEvent;
using MBCommon = TaleWorlds.MountAndBlade.MBCommon;
using Mission = TaleWorlds.MountAndBlade.Mission;
using MissionState = TaleWorlds.MountAndBlade.MissionState;
using Scene = TaleWorlds.Engine.Scene;
using SoundEvent = TaleWorlds.Engine.SoundEvent;   // bez using TaleWorlds.Engine - jego Path zaslania System.IO.Path

namespace Armoury
{
    /// <summary>
    /// m109-muzyka (Jeff 22.10: "muzyka w czasie bitwy jest strasznie slaba, zastap ja naszymi 6 utworami, ktore leca losowo"); m164-muzyka (Jeff 26.10 00:15:
    /// "dodaj jeszcze muzyke, aby jak jest bitwa leciala sciezka dzwiekowa wojenna nie tylko dzwieki z bitwy ale tez 10 utworow bitewnych", "rozdzielmy muzyke
    /// mapa kampanii i bitew"; po probce: "tak swietny klimat"). Klucze BattleMusicOurs (TAK) i BattleMusicLoudness (1-4, 3), czytane na zywo.
    ///  - Kiedy: misja, w ktorej gra wlacza wlasna muzyke bitewna - w Mission.MissionBehaviors widok MusicBattleMissionView (Native) albo MusicNavalBattleMissionView
    ///    (NavalDLC, internal; nazwa typu, takze w typach bazowych): bitwa w polu, z bandytami, w obozie, napad na wies i obrona, szturm, wypad, odsiecz, bitwy na morzu.
    ///    Turniej, arena, kryjowka, spacer po miescie / wsi / obozie, rozmowy - inne widoki muzyki, bez zmian. Klasyfikacja raz na misje (pierwsza klatka Continuing);
    ///    muzyka gra, poki ta sama misja jest w Continuing / EndingNextFrame i trwa kampania. Okno wydarzenia i plotki maja pierwszenstwo (EventMusic.Step).
    ///  - Muzyka gry: ten sam stan i te same metody co okna (DuckStart / FadeDown - 0 trzymane co klatke, bez SetPaused; po bitwie FadeUp w 2 s).
    ///  - Nasze utwory (m164): TYLKO ModuleData/Lektor/Muzyka/Bitwa/lufs{17,20,23,26}/bit_NN_nazwa.ogg - 10 utworow bitewnych (tools/muzyka/bitwa_v2.py: samo
    ///    wzmocnienie z mp3 ElevenLabs, wypalone wejscie 2 s i wyjscie 4 s; suwak 4..1 = -17..-26 LUFS). Stare muz_*.ogg w tych katalogach (utwory wydarzen)
    ///    sa pomijane - muzyka wydarzen (mapa) i bitwy to osobne biblioteki. Zdarzenie event:/Extra/external jak okna, przerwa 1 s miedzy utworami.
    ///    Kolejnosc wedlug numeru: pierwszy w bitwie z puli otwarcia (pole i reszta: 01 zbiorka / 02 marsz; oblezenie i wypad: 09 pod murami), dalej losowo bez
    ///    powtorki az do wyczerpania puli starcia (03-07, w oblezeniu takze 10), potem nowe kolo; nigdy ten sam dwa razy z rzedu (takze miedzy bitwami), chyba
    ///    ze pula ma tylko ten jeden (linia "powtorka", licznik "wymuszone"). 08 (triumf) tylko po zwyciestwie gracza (wynik misji PlayerVictory, misja dalej
    ///    w Continuing - gracz chodzi po polu): biezacy utwor przerwany, 08 od razu, po nim cisza do konca misji. Brak 08 w zestawie - biezacy gra dalej.
    ///    Numer spoza 01-10 - do puli starcia. Koniec utworu: silnik (!IsValid / IsStopped) albo czas pliku + 5 s (z naglowka Ogg; bez dlugosci 200 s) bez pauzy gry.
    ///  - Stop utworu (m164, ryzyko H-stop z raportu): SoundEvent.Release zatrzymuje tylko zdarzenie grajace (IsPlaying) - spauzowane albo startujace dostaloby
    ///    samo ReleaseEvent i mogloby przezyc; StopRelease wola najpierw Stop. Stan przy stopie w linii konca bitwy ("zdarzenie przy stopie gra/pauza/stop/inne").
    ///  - Linie "Muzyka bitwy (m109): ..." (start kampanii, bitwa, utwory z powodem wyboru, zwyciestwo, koniec z lista granych, powrot muzyki gry, misje bez zmian).
    ///    Bledy liczone na misje (po 3 rezygnacja tylko z tej misji), bez wylacznika. Autotest: plik CrashScribe/armoury-muzyka-bitwy.flag (tryb 1/2) - utwor 4 s,
    ///    przerwa 1 s, linia kazdego utworu.
    /// </summary>
    internal static partial class EventMusic
    {
        internal const string PB = "Muzyka bitwy (m109): ";
        internal const string BattleFlagName = "armoury-muzyka-bitwy.flag";
        private const string BattleMood = "bitwa";
        private const double GapSec = 1.0, GapTestSec = 1.0, CheckSec = 1.0, TestCutSec = 4.0, CapExtraSec = 5.0, CapUnknownSec = 200.0, NotPlayedRealSec = 10.0;
        private const int MisFailMax = 3, DetailPerMission = 8, SkipLogFirst = 20, SkipLogEvery = 50, ErrEvery = 50, TrackSetFull = 10;
        private const int NumVictory = 8, NumSiegeOpen = 9, NumSiegeOnly = 10;   // role wedlug numeru pliku (01/02 otwarcie pola, 03-07 starcie)
        private static readonly int[] BattleLufs = { 17, 20, 23, 26 };   // suwak 4, 3, 2, 1

        private sealed class BTrack { internal string Name, File, Path; internal double Dur; internal int Num; }

        // ---- stan bitwy (wyciszenie psai i gniazdo utworu - wspolne z oknami w EventMusic.cs)
        private static WeakReference _batCampaign;
        private static Dictionary<int, List<BTrack>> _batSets;
        private static string _batDir;
        private static int _batBadFiles;
        private static readonly Random _batRng = new Random();
        private static Mission _batMission, _batOnMission;
        private static bool _batIs, _batGaveUp, _batTest, _batActive, _duckBattle, _batNoReady;
        private static string _batMisName = "?", _batMusicView, _batOnName = "?", _batWhy = "koniec misji", _batLast, _batLastFile, _batPrevText, _batPrevBattle;
        private static int _batMisFails, _batStumbles, _batSkipped, _batRejects;
        private static double _batBeganAt, _batGap;
        // m164: rodzaj bitwy, utwory zagrane w tej bitwie (kolo bez powtorek), zwyciestwo gracza
        private static bool _batSiege, _batWon, _batVicDone;
        private static string _batVicText = "NIE";
        private static readonly HashSet<string> _batPlayed = new HashSet<string>(StringComparer.Ordinal);
        private static readonly StringBuilder _batSeq = new StringBuilder();
        // biezacy utwor
        private static BTrack _batCur;
        private static int _batCurLufs;
        private static double _batT0, _batPlayT;
        private static bool _batChecked;
        private static string _batCurWhy = "";
        // podsumowanie bitwy (linia konca)
        private static int _bN, _bEndEngine, _bEndCap, _bEndTest, _bNotPlayed, _bNo1s, _bRepeats, _bForced, _bErrors, _bDetail;

        private static int Level()
        {
            var s = Settings.Current;
            int v = s != null ? s.BattleMusicLoudness : 4;
            return v < 1 ? 1 : v > 4 ? 4 : v;
        }

        private static int LufsOf(int level) { return 17 + 3 * (4 - level); }

        private static double GapOf() { return _batTest ? GapTestSec : GapSec; }

        /// <summary>Zestaw utworow najblizszy zadanej glosnosci: pierwszy pelny (10), inaczej pierwszy niepusty; null - brak.</summary>
        private static List<BTrack> PickSet(int lufs, out int got)
        {
            got = lufs;
            if (_batSets == null) return null;
            List<BTrack> part = null;
            int partLufs = lufs;
            for (int dist = 0; dist <= 9; dist += 3)
                for (int sgn = 0; sgn < 2; sgn++)
                {
                    if (dist == 0 && sgn == 1) continue;
                    int lu = sgn == 0 ? lufs - dist : lufs + dist;   // przy rownej odleglosci glosniejszy pierwszy
                    List<BTrack> l;
                    if (!_batSets.TryGetValue(lu, out l) || l.Count == 0) continue;
                    if (l.Count >= TrackSetFull) { got = lu; return l; }
                    if (part == null) { part = l; partLufs = lu; }
                }
            got = partLufs;
            return part;
        }

        // ============================================================ start kampanii (AppTick, przed wczesnym wyjsciem)
        /// <summary>Linia startu raz na kampanie (nowy obiekt Campaign - takze wczytanie zapisu). Nie rzuca.</summary>
        private static void BattleStartCheck()
        {
            var c = Campaign.Current;
            if (c == null) return;
            var w = _batCampaign;
            if (w != null && ReferenceEquals(w.Target, c)) return;
            _batCampaign = new WeakReference(c);
            try { MapStartLine(); }   // m164: rozdzielenie mapy i bitwy (EventMusic.Mapa.cs) - linia m108
            catch (Exception e) { Stumble("MapStart", e); }
            try { BattleStartLine(); }
            catch (Exception e) { BattleStumble("Start", e); }
        }

        private static void BattleStartLine()
        {
            var s = Settings.Current;
            if (s != null && !s.BattleMusicOurs)
            {
                Log.Info(PB + "start - klucz BattleMusicOurs NIE - bitwy z muzyka gry (jak w 103.5).");
                return;
            }
            LoadBattleTracks();
            PsaiResolve();
            int lv = Level(), got;
            var set = PickSet(LufsOf(lv), out got);
            var sb = new StringBuilder(PB + "start - klucz BattleMusicOurs TAK, glosnosc " + lv + " (-" + got + " LUFS), utwory bitewne");
            for (int i = 0; i < BattleLufs.Length; i++)
            {
                List<BTrack> l;
                sb.Append(i == 0 ? " " : ", ").Append("lufs").Append(BattleLufs[i]).Append(' ').Append(_batSets.TryGetValue(BattleLufs[i], out l) ? l.Count : 0);
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
            sb.Append(", odrzucone ").Append(_batBadFiles).Append(", psai ").Append(_psaiOk ? "TAK" : "NIE").Append(", test autotestu ").Append(BattleTestWanted() ? "TAK" : "NIE").Append('.');
            Log.Info(sb.ToString());
        }

        private static string DurText(double d) { return d > 0 ? d.ToString("0.0", Inv) + " s" : "dlugosc ?"; }

        // ============================================================ czy bitwa (AppTick, co klatke - same odczyty; skan zachowan raz na misje)
        private static bool BattleWanted()
        {
            try
            {
                var s = Settings.Current;
                if (s != null && !s.BattleMusicOurs) { _batWhy = "klucz NIE"; _batMission = null; return false; }   // klucz zmieniony w bitwie - obiekt Mission nie zostaje w polu po misji
                if (Campaign.Current == null) { _batWhy = "wyjscie z kampanii"; _batMission = null; return false; }
                Mission m = Mission.Current;
                if (m == null) { _batWhy = "koniec misji"; _batMission = null; return false; }
                Mission.State st = m.CurrentState;
                if (!ReferenceEquals(m, _batMission))
                {
                    if (st != Mission.State.Continuing) { _batWhy = "koniec misji"; return false; }   // jeszcze laduje (albo juz po)
                    _batMission = m;
                    Classify(m);
                }
                if (st != Mission.State.Continuing && st != Mission.State.EndingNextFrame) { _batWhy = "koniec misji"; return false; }
                if (!_batIs) { _batWhy = "koniec misji"; return false; }
                if (_batGaveUp) { _batWhy = "rezygnacja"; return false; }
                if (!BattleReady()) { _batWhy = "brak psai albo utworow"; return false; }
                return true;
            }
            catch (Exception e) { BattleStumble("Wanted", e); _batWhy = "blad"; return false; }
        }

        /// <summary>Raz na misje: widok muzyki bitewnej gry wsrod zachowan misji (nazwa typu, takze bazowe). Liczniki misji od zera; misja bez zmian - linia (pierwsze 20, potem co 50.).</summary>
        private static void Classify(Mission m)
        {
            _batIs = false; _batGaveUp = false; _batMisFails = 0; _batMusicView = null;
            string name = null;
            try { var ms = MissionState.Current; name = ms != null ? ms.MissionName : null; } catch { }
            _batMisName = string.IsNullOrEmpty(name) ? "?" : name;
            var list = m.MissionBehaviors;
            if (list != null)
                for (int i = 0; i < list.Count && !_batIs; i++)
                {
                    var b = list[i];
                    if (b == null) continue;
                    for (Type t = b.GetType(); t != null && t != typeof(object); t = t.BaseType)
                    {
                        string n = t.Name;
                        if (n == "MusicBattleMissionView" || n == "MusicNavalBattleMissionView") { _batIs = true; _batMusicView = b.GetType().Name; break; }
                        if (_batMusicView == null && n.StartsWith("Music", StringComparison.Ordinal) && n.EndsWith("MissionView", StringComparison.Ordinal)) _batMusicView = n;
                    }
                }
            _batTest = BattleTestWanted();
            if (_batIs) return;
            _batSkipped++;
            if (_batSkipped <= SkipLogFirst || _batSkipped % SkipLogEvery == 0)
                Log.Info(PB + "misja " + _batMisName + " - muzyka gry " + (_batMusicView ?? "bez widoku muzyki") + ", nie bitwa - bez zmian"
                         + (_batSkipped > SkipLogFirst ? " (misji bez zmian w sesji " + _batSkipped + ")" : "") + ".");
        }

        /// <summary>psai i utwory sa; brak - linia raz na sesje (bitwy z muzyka gry).</summary>
        private static bool BattleReady()
        {
            PsaiResolve();
            if (!_psaiOk)
            {
                if (!_batNoReady) { _batNoReady = true; Log.Info(PB + "psai NIE - bitwy z muzyka gry."); }
                return false;
            }
            LoadBattleTracks();
            int got;
            if (PickSet(LufsOf(Level()), out got) == null)
            {
                if (!_batNoReady) { _batNoReady = true; Log.Info(PB + "brak utworow bitewnych w " + (_batDir ?? "?") + " - bitwy z muzyka gry."); }
                return false;
            }
            return true;
        }

        // ============================================================ bitwa (EventMusic.Step, galaz want)
        private static void BattleStep(float dt)
        {
            _idle = 0f;
            if (_batActive && !ReferenceEquals(_batOnMission, _batMission)) BattleEnd("koniec misji", false);   // nastepna misja bitwy bez przerwy miedzy nimi
            if (_active && !_batActive)   // okno wydarzenia zamkniete, misja bitwy trwa: koniec okna linia m108 (para do linii okna), wyciszenie zostaje
            {
                StopTrack("koniec wydarzenia - bitwa dalej");
                Log.Info(P + "koniec wydarzenia - bitwa dalej, muzyka gry dalej wyciszona.");
            }
            if (!_active) { _active = true; DuckStart(); }
            if (!_batActive) BattleBegin();
            if (_mus != null && _musMood != BattleMood) StopTrack(null);   // utwor okna - nie w bitwie
            if (!_batWon && PlayerWon()) BattleVictory();   // m164: zwyciestwo gracza - 08 od razu
            if (_mus == null)
            {
                if (_batVicDone) return;   // m164: po utworze zwyciestwa cisza do konca misji
                _batGap -= dt;
                if (_batGap <= 0) BattleNext();
            }
            else BattleCheck(dt);
        }

        private static void BattleBegin()
        {
            _batActive = true; _batOnMission = _batMission; _batOnName = _batMisName; _duckBattle = true;
            _batBeganAt = Now; _batGap = 0; _batPrevText = null; _batPrevBattle = _batLastFile; _batCur = null; _batCurWhy = "";
            _bN = _bEndEngine = _bEndCap = _bEndTest = _bNotPlayed = _bNo1s = _bRepeats = _bForced = _bErrors = _bDetail = 0;
            _batPlayed.Clear(); _batSeq.Length = 0; _batWon = false; _batVicDone = false; _batVicText = "NIE"; _stopState = "-";
            _volLogged = false; _paused = false;   // linia m108 "muzyka gry wyciszona" w kazdej bitwie (takze gdy wyciszenie trwa po oknie)
            string map = "brak", kind = "-", scene = "?";
            try
            {
                var me = MapEvent.PlayerMapEvent;
                if (me != null)
                {
                    map = me.EventType.ToString();
                    kind = me.IsNavalMapEvent ? "morze" : me.IsSallyOut ? "wypad" : (me.IsSiegeAssault || me.IsSiegeOutside) ? "oblezenie" : me.IsRaid ? "wies"
                         : me.IsFieldBattle ? "pole" : "inne";
                }
            }
            catch { }
            _batSiege = kind == "oblezenie" || kind == "wypad";   // m164: otwarcie 09 i 10 w puli starcia; reszta (pole, wies, morze, inne, bez wydarzenia) jak pole
            try { scene = _batMission.SceneName ?? "?"; } catch { }
            int lv = Level(), got;
            var set = PickSet(LufsOf(lv), out got);
            Log.Info(PB + "bitwa - misja " + _batOnName + " (mapa " + map + ", " + kind + ", scena " + scene + ", oboz " + (NightRest.PlayerCamped ? "TAK" : "NIE")
                     + ", widok " + _batMusicView + (_batTest ? ", test autotestu TAK" : "") + ") - muzyka gry cichnie (psai "
                     + (_psaiOk && _ducked ? "glosnosc " + _saved.ToString("0.00", Inv) : "BRAK") + "), nasza: glosnosc " + lv + " (-" + got + " LUFS), utworow "
                     + (set != null ? set.Count : 0) + ".");
        }

        // ------------------------------------------------------------ wybor utworu (m164)
        private static bool InOpen(BTrack t) { return _batSiege ? t.Num == NumSiegeOpen : (t.Num == 1 || t.Num == 2); }

        private static bool InMain(BTrack t)
        {
            if (t.Num >= 3 && t.Num <= 7) return true;
            if (t.Num == NumSiegeOnly) return _batSiege;
            return t.Num < 1 || t.Num > 10;   // numer spoza 01-10 - do starcia
        }

        /// <summary>Losowanie z puli: najpierw nie grane w tej bitwie i inne niz poprzedni; pula wyczerpana - nowe kolo (bez poprzedniego); sam poprzedni - powtorka wymuszona.
        /// Opis wyboru bez nawiasow (linia utworu: "(lufsNN, dlugosc, wybor ...)" - tools/sprawdz_logi.py m109muz).</summary>
        private static BTrack Draw(List<BTrack> pool, out string note, out bool forced)
        {
            forced = false; note = "";
            if (pool.Count == 0) return null;
            var c = new List<BTrack>();
            foreach (var t in pool) if (t.Name != _batLast && !_batPlayed.Contains(t.Name)) c.Add(t);
            if (c.Count == 0)
            {
                foreach (var t in pool) _batPlayed.Remove(t.Name);
                foreach (var t in pool) if (t.Name != _batLast) c.Add(t);
                if (c.Count > 0) note = ", nowe kolo";
            }
            if (c.Count == 0) { c.AddRange(pool); forced = true; note = ", jedyny w puli - powtorka"; }
            note = " - losowo z " + c.Count + note;
            return c[_batRng.Next(c.Count)];
        }

        /// <summary>Nastepny utwor: pierwszy w bitwie z puli otwarcia, dalej z puli starcia; 08 nigdy (tylko BattleVictory). Brak puli roli - zastepczo.</summary>
        private static BTrack Choose(List<BTrack> set, out string why, out bool forced)
        {
            string note;
            var pool = new List<BTrack>();
            if (_bN == 0)
            {
                foreach (var t in set) if (InOpen(t)) pool.Add(t);
                var p = Draw(pool, out note, out forced);
                if (p != null) { why = (_batSiege ? "otwarcie oblezenia 09" : "otwarcie pola 01/02") + note; return p; }
            }
            pool.Clear();
            foreach (var t in set) if (InMain(t)) pool.Add(t);
            var q = Draw(pool, out note, out forced);
            if (q != null) { why = (_batSiege ? "starcie 03-07+10" : "starcie 03-07") + (_bN == 0 ? " bez otwarcia" : "") + note; return q; }
            pool.Clear();
            foreach (var t in set) if (t.Num != NumVictory) pool.Add(t);
            if (pool.Count == 0) pool.AddRange(set);
            q = Draw(pool, out note, out forced);
            why = "zastepczo bez utworow roli" + note;
            return q;
        }

        /// <summary>Nastepny utwor: wybor wedlug roli z zestawu glosnosci, jak StartTrack przez event:/Extra/external.</summary>
        private static void BattleNext()
        {
            int lv = Level(), got;
            var set = PickSet(LufsOf(lv), out got);
            _batGap = GapOf();
            if (set == null || set.Count == 0) return;
            string why;
            bool forced;
            BTrack pick = Choose(set, out why, out forced);
            if (pick == null) return;
            BattlePlay(pick, got, why, forced);
        }

        /// <summary>Start wybranego utworu. false - silnik nie przyjal (linia, blad misji).</summary>
        private static bool BattlePlay(BTrack pick, int got, string why, bool forced)
        {
            SoundEvent ev = null;
            try { ev = SoundEvent.CreateEventFromExternalFile(MusicEvent, pick.Path, (Scene)null, false, false); }
            catch (Exception e) { BattleStumble("Create", e); return false; }
            bool ok = false, threw = false;
            try { ok = ev != null && ev.IsValid && ev.Play(); } catch (Exception e) { threw = true; BattleStumble("Play", e); }
            if (!ok)
            {
                _batRejects++;
                try { if (ev != null && ev.IsValid) ev.Release(); } catch { }
                if (_batRejects <= 3) Log.Info(PB + "silnik nie przyjal " + pick.File + " (zdarzenie " + MusicEvent + ") - nastepny za " + GapOf().ToString("0", Inv) + " s.");
                if (!threw) MisFail("Next");
                return false;
            }
            if (pick.Name == _batLast) { _bRepeats++; if (forced) _bForced++; }
            _batLast = pick.Name; _batLastFile = pick.File;
            _batPlayed.Add(pick.Name);
            _batSeq.Append(_batSeq.Length == 0 ? "" : ">").Append(pick.Num > 0 ? pick.Num.ToString("00", Inv) : pick.Name);
            _mus = ev; _musMood = BattleMood; _musFile = pick.File;
            _batCur = pick; _batCurLufs = got; _batT0 = Now; _batPlayT = 0; _batChecked = false; _bN++; _batCurWhy = why;
            return true;
        }

        // ------------------------------------------------------------ zwyciestwo gracza (m164)
        /// <summary>Wynik misji ustalony (BattleEndLogic: wrog rozbity albo w odwrocie) na korzysc gracza, misja dalej trwa (gracz chodzi po polu do wyjscia).</summary>
        private static bool PlayerWon()
        {
            try
            {
                var m = _batOnMission;
                if (m == null || m.CurrentState != Mission.State.Continuing || !m.MissionEnded) return false;
                var r = m.MissionResult;
                return r != null && r.PlayerVictory;
            }
            catch (Exception e) { BattleStumble("PlayerWon", e); _batWon = true; _batVicText = "blad"; return false; }   // bez ponownego pytania co klatke
        }

        /// <summary>Zwyciestwo: biezacy utwor przerwany (Stop), 08 od razu; po nim cisza. Brak 08 w zestawie - biezacy gra dalej, kolejne jak dotad.</summary>
        private static void BattleVictory()
        {
            _batWon = true;
            int got;
            var set = PickSet(LufsOf(Level()), out got);
            BTrack v = null;
            if (set != null) foreach (var t in set) if (t.Num == NumVictory) { v = t; break; }
            if (v == null)
            {
                _batVicText = "brak-08";
                Log.Info(PB + "zwyciestwo gracza - brak bit_08 w zestawie lufs" + got + ", biezacy utwor gra dalej.");
                return;
            }
            string cut = "bez utworu";
            var cur = _batCur;
            var ev = _mus;
            if (ev != null && _musMood == BattleMood && cur != null)
            {
                double t = _batPlayT;
                _mus = null; _batCur = null;
                string st = StopRelease(ev, true);   // Stop przed Release, gdy zdarzenie nie gra
                _batPrevText = "poprzedni " + cur.File + " skonczony po " + t.ToString("0.0", Inv) + " s (zwyciestwo - przerwany)";
                cut = "przerwany " + cur.File + " po " + t.ToString("0.0", Inv) + " s (zdarzenie " + st + ")";
            }
            _batVicDone = true;   // po 08 (albo po odrzuceniu 08) cisza do konca misji
            bool ok = BattlePlay(v, got, "zwyciestwo gracza 08", false);
            _batVicText = ok ? "08" : "08-odrzucony";
            Log.Info(PB + "zwyciestwo gracza (wynik misji, misja trwa) - " + cut + ", " + (ok ? "gra " + v.File : "silnik nie przyjal " + v.File) + ", po nim cisza do konca misji.");
        }

        /// <summary>Biezacy utwor: kontrola po 1 s gry (linia), koniec - silnik, test autotestu (4 s), czas pliku + 5 s. Czas gry bez pauzy gry i pauzy zdarzenia.</summary>
        private static void BattleCheck(float dt)
        {
            var ev = _mus;
            var cur = _batCur;
            if (ev == null) return;
            if (cur == null) { StopTrack(null); return; }
            bool stopped = false, paused = false, threw = false;
            try { stopped = !ev.IsValid || ev.IsStopped(); if (!stopped) paused = ev.IsPaused(); }
            catch (Exception e) { threw = true; BattleStumble("Check", e); stopped = true; }
            if (!stopped && !paused && !MBCommon.IsPaused) _batPlayT += dt;
            if (!_batChecked && !stopped && _batPlayT >= CheckSec)
            {
                _batChecked = true;
                bool playing = false;
                try { playing = ev.IsPlaying(); } catch (Exception e) { BattleStumble("IsPlaying", e); }
                if (!playing) _bNo1s++;
                _bDetail++;
                if (_batTest || _bDetail <= DetailPerMission)
                    Log.Info(PB + "utwor " + _bN + " - " + cur.File + " (lufs" + _batCurLufs + ", " + DurText(cur.Dur) + ", wybor " + _batCurWhy + "), grany po 1 s " + (playing ? "TAK" : "NIE")
                             + "; " + (_batPrevText ?? ("pierwszy w bitwie" + (_batPrevBattle != null ? " (ostatni wczesniej " + _batPrevBattle + ")" : ""))) + ".");
            }
            string why = null;
            if (threw) why = "blad";   // wyjatek juz policzony w BattleStumble - bez drugiego bledu misji (NotPlayed) i bez linii "nie grany"
            else if (stopped) why = _batChecked || Now - _batT0 >= NotPlayedRealSec ? "silnik" : null;   // dluga pauza gry przed kontrola 1 s - nie blad
            else if (_batTest && _batChecked && Now - _batT0 >= TestCutSec) { why = "test 4 s"; _bEndTest++; }
            else if (_batPlayT >= (cur.Dur > 0 ? cur.Dur + CapExtraSec : CapUnknownSec)) { why = "po czasie pliku - silnik nie zglosil"; _bEndCap++; }
            if (!stopped && why == null) return;
            _mus = null; _batCur = null;
            StopRelease(ev, true);   // m164: Stop przed Release, gdy zdarzenie nie gra (pauza / start)
            _batGap = GapOf();
            if (why == null)
            {
                _bNotPlayed++;
                _batPrevText = "poprzedni " + cur.File + " nie grany";
                Log.Info(PB + "utwor " + _bN + " - " + cur.File + " (lufs" + _batCurLufs + ", " + DurText(cur.Dur) + ", wybor " + _batCurWhy + "), nie grany - silnik skonczyl po "
                         + (Now - _batT0).ToString("0.0", Inv) + " s; nastepny za " + GapOf().ToString("0", Inv) + " s.");
                MisFail("NotPlayed");
                return;
            }
            if (why == "silnik") _bEndEngine++;
            _batPrevText = "poprzedni " + cur.File + " skonczony po " + _batPlayT.ToString("0.0", Inv) + " s (" + why + ")";
        }

        /// <summary>Koniec bitwy: linia podsumowania, stop utworu. Wyciszenie psai zostaje - wraca FadeUp (linia "wrocila") albo RestoreNow, albo przejmuje okno.</summary>
        private static void BattleEnd(string powod, bool odRazu)
        {
            bool had = _mus != null && _musMood == BattleMood;
            string file = had ? _musFile : null;
            _stopState = "-";
            if (had) StopTrack(null);   // m164: StopRelease - stan zdarzenia w _stopState
            _batCur = null;
            _batActive = false; _batOnMission = null; _active = false;
            string tail = powod == "okno wydarzenia" ? "muzyka gry dalej wyciszona (okno wydarzenia)"
                        : odRazu ? "muzyka gry wraca od razu" : "muzyka gry wraca (" + FadeInSec.ToString("0.0", Inv) + " s)";
            Log.Info(PB + "koniec bitwy (" + powod + ", misja " + _batOnName + ", " + (Now - _batBeganAt).ToString("0", Inv) + " s) - utworow " + _bN + " (koniec silnik "
                     + _bEndEngine + ", po czasie " + _bEndCap + ", test " + _bEndTest + ", nie grane " + _bNotPlayed + ", grane po 1 s NIE " + _bNo1s + "), powtorki z rzedu "
                     + _bRepeats + " (wymuszone " + _bForced + "), bledy " + _bErrors + ", " + (_batSiege ? "oblezenie" : "pole") + ", grane " + (_batSeq.Length > 0 ? _batSeq.ToString() : "-")
                     + ", zwyciestwo " + _batVicText + ", zdarzenie przy stopie " + (had ? _stopState : "-") + ", " + (file != null ? "stop " + file : "bez utworu") + " - " + tail + ".");
        }

        /// <summary>Wyciszenie po bitwie zdjete (FadeUp do konca albo RestoreNow): linia z glosnoscia psai teraz i zapamietana.</summary>
        private static void BattleUnducked(object ps, string why)
        {
            if (!_duckBattle) return;
            _duckBattle = false;
            string got = "?";
            try { if (ps != null) got = ((float)_mGetVol.Invoke(ps, null)).ToString("0.00", Inv); } catch (Exception e) { Stumble("BattleUnducked", e); }
            Log.Info(PB + "muzyka gry wrocila" + (why != null ? " od razu (" + why + ")" : "") + " - psai glosnosc " + got + " (zapamietana " + _saved.ToString("0.00", Inv) + ").");
        }

        /// <summary>RestoreNow: stan bitwy od zera (zestawy utworow i liczniki sesji zostaja). Blad ("blad") w trwajacej misji: ta sama misja bez ponownej klasyfikacji,
        /// w misji bitwy rezygnacja z niej (inaczej przy stalym bledzie psai w kolko bitwa - rezygnacja - powrot co ok. 0,65 s, linie co klatke).</summary>
        private static void BattleReset(string why)
        {
            if (why == "blad" && _batMission != null)
            {
                if (_batIs && !_batGaveUp)
                {
                    _batGaveUp = true;
                    _batMisFails++;
                    Log.Info(PB + "rezygnacja w tej misji po " + _batMisFails + " bledach (RestoreNow) - muzyka gry wraca, nastepna bitwa od nowa.");
                }
            }
            else { _batMission = null; _batIs = false; _batGaveUp = false; _batMisFails = 0; }
            _batOnMission = null; _batActive = false; _duckBattle = false; _batCur = null; _batGap = 0;
            _batWon = false; _batVicDone = false;   // m164: nastepna bitwa od nowa (BattleBegin i tak zeruje)
        }

        // ------------------------------------------------------------ bledy: liczone na misje (po 3 rezygnacja tylko z tej misji), w sesji pierwsze 3 i co 50. do logu
        private static void BattleStumble(string where, Exception e)
        {
            _batStumbles++;
            if (_batStumbles <= 3 || _batStumbles % ErrEvery == 0) Log.Error("EventMusic.Bitwa." + where + " (potkniecie " + _batStumbles + ")", e);
            MisFail(where);
        }

        private static void MisFail(string where)
        {
            _bErrors++;
            if (_batMission == null || _batGaveUp) return;
            _batMisFails++;
            if (_batMisFails < MisFailMax) return;
            _batGaveUp = true;
            Log.Info(PB + "rezygnacja w tej misji po " + _batMisFails + " bledach (" + where + ") - muzyka gry wraca, nastepna bitwa od nowa.");
        }

        // ------------------------------------------------------------ utwory bitewne (raz na sesje)
        private static void LoadBattleTracks()
        {
            if (_batSets != null) return;
            _batSets = new Dictionary<int, List<BTrack>>();
            try
            {
                _batDir = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Lektor.ModuleDir(), "ModuleData"), "Lektor"), "Muzyka"), "Bitwa");
                foreach (int lu in BattleLufs)
                {
                    var list = new List<BTrack>();
                    _batSets[lu] = list;
                    string d = Path.Combine(_batDir, "lufs" + lu.ToString(Inv));
                    if (!Directory.Exists(d)) continue;
                    var files = Directory.GetFiles(d, "bit_*.ogg");   // m164: tylko utwory bitewne; muz_*.ogg (utwory wydarzen z 109) pomijane
                    Array.Sort(files, StringComparer.Ordinal);
                    foreach (var f in files)
                    {
                        bool ogg = false;
                        double dur = 0;
                        try { if (new FileInfo(f).Length > 4096) dur = OggSeconds(f, out ogg); }
                        catch (Exception e) { BattleStumble("OggSeconds", e); }
                        if (!ogg) { _batBadFiles++; continue; }   // pusty, wyzerowany albo nie Ogg - silnik i tak by go nie zagral
                        string name = Path.GetFileNameWithoutExtension(f).Substring(4);   // "01_zbiorka"
                        int num = 0;
                        if (name.Length >= 2 && char.IsDigit(name[0]) && char.IsDigit(name[1]) && (name.Length == 2 || name[2] == '_')) num = (name[0] - '0') * 10 + (name[1] - '0');
                        list.Add(new BTrack { Name = name, File = Path.GetFileName(f), Path = f, Dur = dur, Num = num });
                    }
                }
            }
            catch (Exception e) { BattleStumble("LoadBattleTracks", e); }
        }

        /// <summary>Dlugosc Ogg Vorbis: czestotliwosc z naglowka "\x01vorbis" (+12), pozycja granuli ostatniej strony "OggS" (+6) w ostatnich 64 KB. 0 - nie wiadomo.</summary>
        private static double OggSeconds(string path, out bool ogg)
        {
            ogg = false;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var head = new byte[(int)Math.Min(4096, fs.Length)];
                ReadFull(fs, head);
                if (head.Length < 64 || !IsPage(head, 0)) return 0;
                ogg = true;
                int at = -1;
                for (int i = 0; i + 16 <= head.Length; i++)
                    if (head[i] == 1 && head[i + 1] == (byte)'v' && head[i + 2] == (byte)'o' && head[i + 3] == (byte)'r' && head[i + 4] == (byte)'b' && head[i + 5] == (byte)'i'
                        && head[i + 6] == (byte)'s') { at = i; break; }
                if (at < 0) return 0;
                uint rate = BitConverter.ToUInt32(head, at + 12);
                if (rate < 8000 || rate > 192000) return 0;
                int tailLen = (int)Math.Min(65536, fs.Length);
                fs.Seek(fs.Length - tailLen, SeekOrigin.Begin);
                var tail = new byte[tailLen];
                ReadFull(fs, tail);
                for (int k = tail.Length - 27; k >= 0; k--)
                    if (IsPage(tail, k))
                    {
                        long g = BitConverter.ToInt64(tail, k + 6);
                        return g > 0 ? g / (double)rate : 0;
                    }
                return 0;
            }
        }

        private static bool IsPage(byte[] b, int k)
        {
            return b[k] == (byte)'O' && b[k + 1] == (byte)'g' && b[k + 2] == (byte)'g' && b[k + 3] == (byte)'S' && b[k + 4] == 0;
        }

        private static void ReadFull(Stream s, byte[] buf)
        {
            int off = 0;
            while (off < buf.Length)
            {
                int n = s.Read(buf, off, buf.Length - off);
                if (n <= 0) throw new EndOfStreamException();
                off += n;
            }
        }

        /// <summary>Autotest: tryb CrashScribe 1/2 i plik flagi w katalogu CrashScribe (wzor MapChatLog.TestWanted).</summary>
        private static bool BattleTestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), BattleFlagName));
            }
            catch { return false; }
        }
    }
}
