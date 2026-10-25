using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// paczka 132 (Jeff 25.10 ok. 12:52: "daj mi zdjecia z gry jak wygladaja te nowe widoki popupy armii, lupow etc") - PROBA ZDJEC NOWYCH OKIEN, tylko autotest:
    /// DLL autotestu CrashScribe (LogGate.Mode 1/2) i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-zdjecia-okien.flag (FlagName; plik tylko
    /// czytany). W zwyklej grze jedno sprawdzenie trybu na sesje (plik - tylko w autotescie), potem od razu wyjscie.
    /// KIEDY: raz na proces, w kampanii (najdluzej 60 min czekania), 30 s po wejsciu na mape i po 3 s spokoju: MapState na wierzchu i ekran mapy, bez misji, menu Esc, bitwy, oblezenia,
    /// spotkania poza osada (miasto, w ktorym autotest czeka, wolno), rozmowy, zapytania, okna lupu i okna opowiesci, do tego wspolna bramka okien Prologue.SceneGate
    /// (recenzja 132: ekran wczytywania, MapScreen.IsReady, menu nad mapa, scena powiadomienia, pauza silnika, blokada czasu, okno BK, okno wydarzenia ROT); etap autotestu CrashScribe (Autotest.Now
    /// odbiciem, jak LogGate.Detect) "Run" - bieg dob, juz po bitwach autotestu (gdy etapu nie da sie odczytac - bez tego warunku). Inny etap, misja albo bitwa
    /// w trakcie proby - przerwanie: nasze okno zamkniete, linia "przerwana".
    /// OKNA (te same funkcje co gracz): 1 "Review the army" (ArmyReview.OpenMain), 2 "Banners for the next battle" (OpenBanners), 3 pierwsza wystawiona choragiew
    /// (OpenBanner), 4 "Move troop types into banners" (OpenPins), 5-7 okno lupu po bitwie, po oblezeniu (grabiez) i po turnieju (SpoilsWindow.ShowPhoto - Build,
    /// Picture i warstwa jak po bitwie) z raportem PROBNYM: prawdziwe przedmioty (najcenniejsze z sakw gracza, brakujace rodzaje z listy przedmiotow gry), etykiety
    /// zlota, jency, straty, slawa - raport nie trafia do SpoilsReport (bez Finish, bez licznikow sesji) i niczego w grze nie zmienia.
    /// 133g (okna paczki 133, te same funkcje co gracz - ArmyReview.PhotoOpen): 8 lista typow "Kit for the next battle" (OpenKitTypes), 9 strona typu z brakami
    /// (pierwszy typ z brakami albo jezdzcami pieszo; bez brakow - pierwszy typ), 10 "Last battle", 11 "Horses and harness" (HorsePages - ta sama funkcja co
    /// OpenMain), 12 "Speed", 13 "Herd", 14-16 "Pay" / "Who gets what" / "Where it goes" (pierwsza strona kazdej), 17 lista "Recent messages" (pusty pierscien -
    /// jeden komunikat proby z ArmyReview.News.cs, ta sama droga co test T1). Strony (ShowPages) maja w probie przyciski bez skutku jak okna wyboru.
    /// Recenzja 133: 18 okno wyboru "On the march" (OpenMarch), 19 okno wyboru "Pay" (OpenPay), 20 strona "Unused in the troops' armoury", 21 strona "Column",
    /// 22 strona jednego komunikatu ("Message - Temat") - te same funkcje co okno gracza. Krok bez danych, ktorych od wczytania byc nie moze (10 "Last battle" bez
    /// bitwy od wczytania - zapis nie trafia do zapisu gry) - POMINIETY (nie dotyczy): linia "POMINIETY", poza mianownikiem wyniku (wynik TAK = TAK + pominiete
    /// = wszystkie kroki).
    /// ZDJECIE: TaleWorlds.Engine.Utilities.TakeScreenshot do CrashScribe\zdjecia\okna-132-N-nazwa-HHmmss.png (kroki 8-17: okna-133-N-...; silnik pisze BMP mimo rozszerzenia). Zapytania gry -
    /// zdjecie po 0.6 s czasu rzeczywistego (Stopwatch): autotest (AutotestUi.Handle co 0.25 s) zamyka zapytanie po >= 1.0 s od zobaczenia (odmowa / wyjscie), wiec
    /// 1.5 s sie nie da (0.4 s zapasu na zrzut silnika); prefab zapytania nie ma animacji wejscia (Native MultiSelectionQueryPopup.xml), rysuje sie w pierwszej klatce. Okno lupu (wlasna warstwa,
    /// autotest jej nie widzi) - po 1.8 s (obce zapytanie nad nim - czeka, najwyzej 8 s). Zamkniecie: autotest albo my po 1 s od zdjecia
    /// (InformationManager.HideInquiry -> GauntletQueryManager.CloseQuery: bez wywolania akcji okna; SpoilsWindow.ClosePhoto). recenzja 132: HideInquiry tylko na NASZYM
    /// zapytaniu (MineOnScreen: faza >= 1 i _activeQueryData to dane kroku) - takze przy przerwaniu; obce okno (np. z kolejki po naszym) zostaje autotestowi.
    /// STAN GRY: na czas proby (Active) i dla kazdego okna otwartego w probie akcje przyciskow okien przegladu sa bez skutku (ArmyReview.Inert na poczatku callbackow),
    /// "Review the army" otwiera sie takze w osadzie (ArmyReview.Can -> InTownOk, tylko Active); kopia robocza okna choragwi (_work, _workPins, _dirty, _fromGame)
    /// wraca do stanu sprzed proby. Zadnego zapisu Order of Battle, przypiec, zlota, sakw ani zapisu gry. Nasze zapytanie rozpoznajemy po
    /// GauntletQueryManager._activeQueryData (odbiciem, to samo pole czyta AutotestUi); bez pola - IsAnyInquiryActive tylko do odczytu (linia logu, czekanie),
    /// nigdy do zamkniecia (bez pola nie zamykamy - autotest zamknie okno po 1-1.5 s, przyciski bez skutku).
    /// Linie: "Zdjecia okien (132): ..." - start, po jednej na okno ("TAK plik ..." / "NIE (powod)") i koniec (pliki na dysku 6 s po ostatnim zdjeciu).
    /// Potkniecia liczone (Stumble); zly krok - linia NIE i nastepny krok, bez wylacznika (po 30 potknieciach proba konczy sie linia "przerwana").
    /// </summary>
    internal static class WindowPhotos
    {
        internal const string FlagName = "armoury-zdjecia-okien.flag";
        private const string P = "Zdjecia okien (132): ";
        private const int Steps = 22;                     // 133g: 8-17 okna paczki 133 (zapytania jak 1-4); recenzja 133: 18-22
        private const int LayerFirst = 5, LayerLast = 7;  // okna lupu (wlasna warstwa)
        private const double StartAfterSec = 30.0, CalmSec = 3.0, GiveUpSec = 3600.0;
        private const double InquiryShotSec = 0.6, LayerShotSec = 1.8, LayerShotMaxSec = 8.0, CloseAfterShotSec = 1.0;
        private const double GapSec = 0.3, StepWaitSec = 20.0, FileWaitSec = 6.0;
        private const int MaxStumbles = 30;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Names = { "", "przeglad", "choragwie", "choragiew", "przypinanie", "lup-bitwa", "lup-oblezenie", "lup-turniej",
                                                   "sprzet-lista", "sprzet-typ", "ostatnia-bitwa", "konie", "predkosc", "stado", "zold", "zold-kto", "zold-dokad", "komunikaty",
                                                   "marsz", "zold-okno", "nieuzyte", "kolumna", "komunikat" };

        /// <summary>Recenzja 133: poczatek powodu z ArmyReview.PhotoOpen dla kroku "nie dotyczy" (okna nie da sie otworzyc z braku danych, ktorych od wczytania
        /// byc nie moze) - krok POMINIETY, poza mianownikiem wyniku.</summary>
        internal const string SkipMark = "nie dotyczy: ";

        private static bool _checked, _done, _active;
        private static Campaign _camp;
        private static long _seenAt, _calmAt, _lastCheck, _beganAt;
        private static int _step, _phase;          // krok 1..7 (8 - koniec, czekanie na pliki); faza 0 czeka i otwiera, 1 otwarte - zdjecie, 2 po zdjeciu - zamkniecie
        private static long _stepAt, _openedAt, _shotAt, _endAt;
        private static object _myData;             // zapytanie gry tego kroku (GauntletQueryManager._activeQueryData)
        private static bool _dataKnown, _shotOpen;
        private static string _file, _note, _title, _shotErr;
        private static int _ok, _bad, _skip, _stumbles;
        private static readonly List<string> _files = new List<string>();

        /// <summary>Proba w toku - akcje przyciskow okien przegladu bez skutku (ArmyReview.Inert).</summary>
        internal static bool Active { get { return _active; } }

        /// <summary>ArmyReview.Can: w probie przeglad otwiera sie takze w osadzie (autotest czeka w miescie - spotkanie z osada); bitwa i oblezenie dalej nie.</summary>
        internal static bool InTownOk()
        {
            try { return _active && PlayerEncounter.EncounterSettlement != null; } catch { return false; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) { try { Log.Error("WindowPhotos." + where + " (potkniecie " + _stumbles + ")", e); } catch { } }
        }

        private static double Sec(long since, long now) { return since == 0 ? 0 : (now - since) / (double)Stopwatch.Frequency; }

        private static bool Layer() { return _step >= LayerFirst && _step <= LayerLast; }

        // ------------------------------------------------------------ takt (SubModuleMain.SpoilsTick - co klatke)

        internal static void AppTick()
        {
            if (_done) return;
            try { Tick(Stopwatch.GetTimestamp()); }
            catch (Exception e)
            {
                Stumble("Tick", e);
                if (_stumbles >= MaxStumbles) Abort("za duzo potkniec (" + _stumbles + ")");
            }
        }

        private static void Tick(long now)
        {
            var c = Campaign.Current;
            if (!_checked)
            {
                if (c == null || !c.GameStarted) return;
                _checked = true;
                int mode = LogGate.Mode;
                if (mode != 1 && mode != 2) { _done = true; return; }   // zwykla gra: jedno sprawdzenie trybu na sesje, pliku nie czytamy
                if (!Wanted())
                {
                    _done = true;
                    Log.Info(P + "bez proby - brak pliku " + FlagName + " w CrashScribe (tryb autotestu " + mode + ").");
                    return;
                }
                _camp = c; _seenAt = now;
                Log.Info(P + "plik " + FlagName + " jest - proba " + Steps + " okien po " + StartAfterSec.ToString("0", Inv) + " s kampanii i " + CalmSec.ToString("0", Inv)
                         + " s spokoju na mapie (etap autotestu teraz: " + (Stage() ?? "nieznany") + ").");
                return;
            }
            if (_step > Steps) { EndTick(now); return; }
            if (_step == 0)
            {
                if (!ReferenceEquals(c, _camp)) { _camp = c; _seenAt = now; _calmAt = 0; return; }   // inna kampania przed proba - liczenie od nowa
                WaitStart(now);
                return;
            }
            if (!ReferenceEquals(c, _camp)) { Abort(c == null ? "wyjscie z kampanii" : "inna kampania"); return; }
            StepTick(now);
        }

        private static bool Wanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;   // tylko DLL autotestu CrashScribe
            try { return File.Exists(Path.Combine(CampWalk.CrashScribeDir(), FlagName)); }
            catch { return false; }
        }

        private static void WaitStart(long now)
        {
            if (now - _lastCheck < Stopwatch.Frequency / 4) return;
            _lastCheck = now;
            double since = Sec(_seenAt, now);
            string why = Calm(true);
            if (why != null)
            {
                _calmAt = 0;
                if (since > GiveUpSec)
                {
                    _done = true;
                    Log.Info(P + "bez proby - " + (GiveUpSec / 60).ToString("0", Inv) + " min bez spokojnej mapy (ostatnio: " + why + ").");
                }
                return;
            }
            if (since < StartAfterSec) return;
            if (_calmAt == 0) { _calmAt = now; return; }
            if (Sec(_calmAt, now) < CalmSec) return;
            Begin(now);
        }

        private static void Begin(long now)
        {
            _active = true;
            _beganAt = now;
            try { ArmyReview.PhotoSave(); } catch (Exception e) { Stumble("PhotoSave", e); }
            _step = 1; _phase = 0; _stepAt = now - (long)(GapSec * Stopwatch.Frequency);
            Log.Info(P + "start | " + Where() + " | akcje przyciskow okien bez skutku na czas proby | zapytania: zdjecie po " + InquiryShotSec.ToString("0.0", Inv)
                     + " s (autotest zamyka je po >= 1 s), okno lupu: po " + LayerShotSec.ToString("0.0", Inv) + " s.");
        }

        // ------------------------------------------------------------ kroki

        private static void StepTick(long now)
        {
            string stop = StopWhy();
            if (stop != null) { Abort(stop); return; }
            if (_phase == 0)
            {
                if (Sec(_stepAt, now) < GapSec) return;
                string why = Calm(false);
                if (why != null)
                {
                    if (Sec(_stepAt, now) > StepWaitSec) { Result(false, "okno nie otwarte - " + StepWaitSec.ToString("0", Inv) + " s bez spokoju (" + why + ")"); Next(now); }
                    return;
                }
                Open(now);
                return;
            }
            if (_phase == 1)
            {
                double s = Sec(_openedAt, now);
                if (!StillOpen()) { Result(false, "okno zamkniete przed zdjeciem, po " + s.ToString("0.00", Inv) + " s"); Next(now); return; }
                if (Layer())
                {
                    if (s < LayerShotSec) return;
                    if (InformationManager.IsAnyInquiryActive() && s < LayerShotMaxSec) return;   // obce zapytanie nad oknem lupu - autotest je zamknie
                }
                else if (s < InquiryShotSec) return;
                Shot(now);
                return;
            }
            // faza 2: po zdjeciu - zamkniecie (autotest albo my po 1 s)
            if (StillOpen() && Sec(_shotAt, now) < CloseAfterShotSec) return;
            string how = CloseMine();
            Result(_file != null && _shotErr == null, how);
            Next(now);
        }

        private static void Open(long now)
        {
            string why;
            _myData = null; _dataKnown = false; _title = null; _note = null;
            try
            {
                if (!Layer())
                {
                    why = ArmyReview.PhotoOpen(_step, out _note);
                    if (why == null)
                    {
                        bool known;
                        object d = ActiveQuery(out known);
                        _dataKnown = known;
                        if (known ? d == null : !InformationManager.IsAnyInquiryActive()) why = "okno sie nie pokazalo";
                        else { _myData = d; _title = TitleOf(d); }
                    }
                }
                else
                {
                    var r = TestRep(_step, out _note);
                    why = r == null ? "brak danych probnych (brak miasta)" : SpoilsWindow.ShowPhoto(r);
                    if (why == null) { try { _title = SpoilsWindow.TitleOf(r); } catch { _title = null; } }
                    if (why == null && SpoilsWindow.LastPicture.Length > 0) _note = (_note != null ? _note + ", " : "") + SpoilsWindow.LastPicture;   // 150b
                }
            }
            catch (Exception e) { Stumble("Open." + _step, e); why = "wyjatek " + e.GetType().Name; }
            if (why != null)
            {
                // okno moglo wejsc przed wyjatkiem (przed otwarciem zadnego zapytania nie bylo - Calm) - nie zostawiac go z martwymi przyciskami
                try
                {
                    if (Layer()) SpoilsWindow.ClosePhoto();
                    else if (InformationManager.IsAnyInquiryActive()) InformationManager.HideInquiry();
                }
                catch (Exception e) { Stumble("Open.Close." + _step, e); }
                if (why.StartsWith(SkipMark, StringComparison.Ordinal)) Skipped(why.Substring(SkipMark.Length)); else Result(false, why);
                Next(now);
                return;
            }
            _openedAt = now;
            _phase = 1;
        }

        /// <summary>Okno kroku dalej na ekranie: warstwa okna lupu albo nasze zapytanie (_activeQueryData to ten sam obiekt).</summary>
        private static bool StillOpen()
        {
            if (Layer()) return SpoilsWindow.PhotoOpen;
            if (_dataKnown) { bool k; var d = ActiveQuery(out k); return k && d != null && ReferenceEquals(d, _myData); }
            return InformationManager.IsAnyInquiryActive();
        }

        /// <summary>recenzja 132: na ekranie na pewno NASZE zapytanie (faza >= 1, pole _activeQueryData czytelne i to te same dane kroku). Tylko wtedy HideInquiry -
        /// zapas StillOpen (IsAnyInquiryActive) bywa obcym oknem (faza 0 czeka wlasnie na obce zapytanie; CloseQuery wyciaga z kolejki okno stojace za naszym),
        /// a CloseQuery zamyka okno bez wywolania jego akcji - obcy mod zostalby z niezamknietym stanem.</summary>
        private static bool MineOnScreen()
        {
            if (Layer() || _phase < 1 || !_dataKnown || _myData == null) return false;
            bool k; var d = ActiveQuery(out k);
            return k && ReferenceEquals(d, _myData);
        }

        private static void Shot(long now)
        {
            _shotErr = null; _file = null;
            _shotOpen = StillOpen();
            try
            {
                string dir = Path.Combine(CampWalk.CrashScribeDir(), "zdjecia");
                Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, "okna-" + (_step > LayerLast ? "133" : "132") + "-" + _step + "-" + Names[_step] + "-" + DateTime.Now.ToString("HHmmss", Inv) + ".png");
                TaleWorlds.Engine.Utilities.TakeScreenshot(f.Replace('\\', '/'));
                _file = f;
                _files.Add(f);
            }
            catch (Exception e) { Stumble("Shot." + _step, e); _shotErr = e.GetType().Name; }
            _shotAt = now;
            _phase = 2;
        }

        private static string CloseMine()
        {
            try
            {
                if (Layer())
                {
                    if (!SpoilsWindow.PhotoOpen) return "okno zamkniete wczesniej (Done, Esc, Enter albo zmiana ekranu)";
                    SpoilsWindow.ClosePhoto();
                    return "okno zamkniete przez probe";
                }
                if (MineOnScreen()) { InformationManager.HideInquiry(); return "okno zamkniete przez probe (HideInquiry)"; }
                if (!_dataKnown && InformationManager.IsAnyInquiryActive())
                    return "zapytanie na ekranie, bez odczytu _activeQueryData nie wiadomo czy nasze - zostawione autotestowi (przyciski bez skutku)";
                return "okno zamkniete przez autotest albo gracza (przycisk bez skutku)";
            }
            catch (Exception e) { Stumble("Close." + _step, e); return "blad zamkniecia " + e.GetType().Name; }
        }

        private static void Result(bool ok, string how)
        {
            if (ok) _ok++; else _bad++;
            try
            {
                string head = P + _step + " " + Names[_step] + (_title != null ? " \"" + _title + "\"" : "") + (_note != null ? " (" + _note + ")" : "");
                if (ok)
                {
                    long len = FileLen(_file);
                    Log.Info(head + " - TAK plik zdjecia\\" + Path.GetFileName(_file) + " | zdjecie po " + Sec(_openedAt, _shotAt).ToString("0.00", Inv) + " s, okno na ekranie "
                             + (_shotOpen ? "TAK" : "NIE") + " | " + how + " | plik " + (len >= 0 ? "jest (" + (len / 1024).ToString(Inv) + " KB)" : "jeszcze nie zapisany - sprawdzi linia koncowa") + ".");
                }
                else Log.Info(head + " - NIE (" + (_shotErr != null ? "zrzut silnika: wyjatek " + _shotErr + "; " : "") + how + ").");
            }
            catch (Exception e) { Stumble("Result", e); }
        }

        /// <summary>Recenzja 133: krok "nie dotyczy" - linia POMINIETY, licznik _skip (poza mianownikiem wyniku).</summary>
        private static void Skipped(string why)
        {
            _skip++;
            try { Log.Info(P + _step + " " + Names[_step] + " - POMINIETY (nie dotyczy: " + why + ")."); }
            catch (Exception e) { Stumble("Skipped", e); }
        }

        private static void Next(long now)
        {
            if (_step == 4) { try { ArmyReview.PhotoRestore(); } catch (Exception e) { Stumble("PhotoRestore", e); } }   // okna przegladu za nami - kopia robocza sprzed proby
            _step++; _phase = 0; _stepAt = now;
            _myData = null; _dataKnown = false; _shotOpen = false; _file = null; _shotErr = null; _note = null; _title = null;
            if (_step > Steps) { _active = false; _endAt = now; }
        }

        private static void EndTick(long now)
        {
            if (Sec(_endAt, now) < FileWaitSec) return;
            _done = true;
            int have = 0;
            var miss = new List<string>();
            foreach (var f in _files) { if (FileLen(f) > 0) have++; else miss.Add(Path.GetFileName(f)); }
            Log.Info(P + "koniec | okna TAK " + _ok + "/" + Steps + ", pominiete (nie dotyczy) " + _skip + ", NIE " + _bad + " | pliki na dysku " + have + "/" + _files.Count
                     + " (Documents\\Mount and Blade II Bannerlord\\CrashScribe\\zdjecia)" + (miss.Count > 0 ? " - BRAK: " + string.Join(", ", miss.ToArray()) : "")
                     + " | kopia robocza okna choragwi przywrocona | potkniecia " + _stumbles + " | czas proby " + Sec(_beganAt, _endAt).ToString("0.0", Inv)
                     + " s - wynik " + (_ok + _skip == Steps && have == _files.Count ? "TAK" : "NIE") + ".");
        }

        private static void Abort(string why)
        {
            if (_done) return;
            _done = true;
            try
            {
                if (_step >= 1 && _step <= Steps)
                {
                    if (Layer()) SpoilsWindow.ClosePhoto();
                    else if (MineOnScreen()) InformationManager.HideInquiry();   // recenzja 132: tylko nasze zapytanie (faza 0 - obce okno, na ktore krok czeka)
                }
            }
            catch (Exception e) { Stumble("Abort.Close", e); }
            try
            {
                // inna kampania: ArmyReview.Reset juz wyczyscil okno - kopii sprzed proby nie przywracamy
                if (_step >= 1 && _step <= 4 && ReferenceEquals(Campaign.Current, _camp)) ArmyReview.PhotoRestore();
                else ArmyReview.PhotoForget();
            }
            catch (Exception e) { Stumble("Abort.Restore", e); }
            _active = false;
            try
            {
                Log.Info(P + "przerwana (" + why + ") w kroku " + _step + " " + (_step >= 1 && _step <= Steps ? Names[_step] : "-") + " | okna TAK " + _ok + ", pominiete " + _skip + ", NIE " + _bad
                         + " | potkniecia " + _stumbles + ".");
            }
            catch { }
        }

        // ------------------------------------------------------------ warunki

        /// <summary>null - spokojnie (wolno otworzyc okno); inaczej powod. start - takze etap autotestu (tylko bieg dob).</summary>
        internal static string Calm(bool start)
        {
            var c = Campaign.Current;
            if (c == null || !c.GameStarted) return "brak kampanii";
            var gsm = GameStateManager.Current;
            var st = gsm != null ? gsm.ActiveState : null;
            if (!(st is MapState)) return "na wierzchu stan " + (st != null ? st.GetType().Name : "-");
            if (TaleWorlds.MountAndBlade.Mission.Current != null) return "misja";
            var top = ScreenManager.TopScreen;
            if (!(top is SandBox.View.Map.MapScreen)) return "na wierzchu ekran " + (top != null ? top.GetType().Name : "-");
            var ms = SandBox.View.Map.MapScreen.Instance;
            if (ms != null && ms.IsEscapeMenuOpened) return "menu Esc gry";
            var mp = MobileParty.MainParty;
            var me = Hero.MainHero;
            if (mp == null || me == null || !me.IsAlive || me.IsPrisoner) return "brak gracza albo niewola";
            if (mp.MapEvent != null || mp.SiegeEvent != null) return "bitwa albo oblezenie";
            if (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null) return "spotkanie";
            if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return "morze";
            if (c.ConversationManager != null && c.ConversationManager.IsConversationInProgress) return "rozmowa";
            if (InformationManager.IsAnyInquiryActive()) return "zapytanie na ekranie";
            if (SpoilsWindow.Busy || SpoilsReport.AnyReady) return "okno lupu";
            if (StoryWindow.IsOpen) return "okno opowiesci";
            // recenzja 132: wspolna bramka okien (ta sama co okno lupu i Kronika): ekran wczytywania, MapScreen.IsReady, menu nad mapa, scena powiadomienia, pauza silnika,
            // blokada czasu (zdarzenie mapy), bitwa w spotkaniu, okno BK, okno wydarzenia ROT. Miasto autotestu (town_wait_menus, bieg) przepuszcza - Kronika pokazala
            // tam okno 24.10 22:16:43 (Armoury-2026-10-24_22-13-58.log, autotest: "czekam w The Twins ... town_wait_menus").
            string g = Prologue.SceneGate(c);
            if (g != null) return g;
            if (start)
            {
                string sg = Stage();
                if (sg != null && sg != "Run") return "etap autotestu " + sg;
                if (SpoilsAtlas.Running) return "proba obrazkow lupu (150) w toku";   // paczka 150: obie proby uzywaja okna lupu (SpoilsWindow.ShowPhoto) - nie naraz
            }
            return null;
        }

        /// <summary>Przerwanie proby w toku: misja, bitwa gracza, etap autotestu inny niz bieg dob (zapis, wyjscie, bitwy, zdjecia mapy).</summary>
        internal static string StopWhy()
        {
            if (TaleWorlds.MountAndBlade.Mission.Current != null) return "misja";
            var mp = MobileParty.MainParty;
            if (mp != null && mp.MapEvent != null) return "bitwa gracza";
            string sg = Stage();
            if (sg != null && sg != "Run") return "etap autotestu " + sg;
            return null;
        }

        private static FieldInfo _stageF, _queryF;
        private static bool _stageTried, _queryTried;

        /// <summary>Etap autotestu CrashScribe (Autotest.Now - Menu, Creation, Settle, Run, Photos, Battle, Saving...) odbiciem jak LogGate.Detect; null - nie da sie.</summary>
        internal static string Stage()
        {
            if (!_stageTried)
            {
                _stageTried = true;
                try
                {
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "CrashScribe") continue;
                        var t = asm.GetType("CrashScribe.Autotest", false);
                        _stageF = t != null ? t.GetField("Now", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                        break;
                    }
                }
                catch { _stageF = null; }
            }
            try { var v = _stageF != null ? _stageF.GetValue(null) : null; return v != null ? v.ToString() : null; }
            catch { return null; }
        }

        /// <summary>Zapytanie na ekranie (GauntletQueryManager._activeQueryData, statyczne - to samo pole czyta AutotestUi). known false - pola nie ma.</summary>
        private static object ActiveQuery(out bool known)
        {
            if (!_queryTried)
            {
                _queryTried = true;
                try
                {
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "TaleWorlds.MountAndBlade.GauntletUI") continue;
                        var t = asm.GetType("TaleWorlds.MountAndBlade.GauntletUI.GauntletQueryManager", false);
                        _queryF = t != null ? t.GetField("_activeQueryData", BindingFlags.Static | BindingFlags.NonPublic) : null;
                        break;
                    }
                }
                catch { _queryF = null; }
            }
            known = _queryF != null;
            if (!known) return null;
            try { return _queryF.GetValue(null); }
            catch { known = false; return null; }
        }

        private static string TitleOf(object d)
        {
            var m = d as MultiSelectionInquiryData;
            if (m != null) return m.TitleText;
            var q = d as InquiryData;
            return q != null ? q.TitleText : null;
        }

        private static long FileLen(string f)
        {
            try { return f != null && File.Exists(f) ? new FileInfo(f).Length : -1; } catch { return -1; }
        }

        private static string Where()
        {
            var sb = new StringBuilder();
            try
            {
                var s = Settlement.CurrentSettlement;
                var mc = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
                string menu = mc != null && mc.GameMenu != null ? mc.GameMenu.StringId : "-";
                sb.Append("osada ").Append(s != null && s.Name != null ? s.Name.ToString() : "-").Append(", menu ").Append(menu)
                  .Append(", etap autotestu ").Append(Stage() ?? "nieznany").Append(", klucz Army Review Enabled ").Append(ArmyReview.On ? "TAK" : "NIE")
                  .Append(", zapytanie gry odczyt ").Append(ActiveQueryKnown() ? "TAK" : "NIE (IsAnyInquiryActive)");
            }
            catch (Exception e) { Stumble("Where", e); }
            return sb.ToString();
        }

        private static bool ActiveQueryKnown() { bool k; ActiveQuery(out k); return k; }

        // ------------------------------------------------------------ raporty PROBNE okna lupu (tylko tresc okna - nic w grze)

        private static readonly int[] Kinds = { LootTally.Tulow, LootTally.Glowa, LootTally.Rece, LootTally.Bron, LootTally.Tarcza, LootTally.Pociski, LootTally.Kon, LootTally.Uprzaz, LootTally.Inne };
        private static readonly int[] BattleCounts = { 7, 5, 3, 12, 6, 9, 3, 2, 15 };
        private static readonly int[] SiegeCounts = { 16, 11, 8, 28, 12, 40, 7, 5, 60 };

        internal static SpoilsReport.Rep TestRep(int step, out string note)
        {
            note = null;
            var me = Hero.MainHero;
            Settlement near = null;
            try { near = Settlement.CurrentSettlement; } catch { near = null; }
            if (near == null && me != null) near = me.LastKnownClosestSettlement;
            Settlement town = near != null && near.IsTown ? near : null;
            if (town == null) foreach (var tw in Town.AllTowns) if (tw != null && tw.Settlement != null) { town = tw.Settlement; break; }
            var items = PickItems();
            var r = new SpoilsReport.Rep { Id = 132000 + step, Date = CampaignTime.Now.ToString() };
            if (step == 5)
            {
                r.Kind = SpoilsReport.KBattle; r.Battles = 1;
                r.Place = near != null ? SpoilsReport.Nm(near) : "-";
                r.Near = near;   // 150b: obrazek wedlug regionu jak po prawdziwej bitwie
                r.Enemy = "Outlaws";
                r.AddCoin(SpoilsReport.LPlunder, 1240); r.AddCoin(SpoilsReport.LPurses, 385); r.AddCoin(SpoilsReport.LField, 96); r.AddCoin(SpoilsReport.LOther, 14);
                r.Men = 212; r.Crown = 245;
                AddGear(r, items, BattleCounts);
                r.LeftN = 14; r.LeftW = 2310;
                AddPrisoners(r, 14);
                r.RansomWorth = 980; r.Fallen = 7; r.Wounded = 19; r.Renown = 12f; r.Influence = 3f;
            }
            else if (step == 6)
            {
                if (town == null) return null;
                var owner = town.OwnerClan;
                r.Kind = SpoilsReport.KSiege; r.Battles = 1; r.Town = town; r.SackKind = CitySack.KPillage; r.Place = SpoilsReport.Nm(town);
                r.Enemy = owner != null && owner.Name != null ? owner.Name.ToString() : "";
                r.AddCoin(SpoilsReport.LSack, 4200); r.AddCoin(SpoilsReport.LChest, 1530); r.AddCoin(SpoilsReport.LPlunder, 640); r.AddCoin(SpoilsReport.LOther, 35);
                r.Men = 930; r.Crown = 1460;
                AddGear(r, items, SiegeCounts);
                r.LeftN = 41; r.LeftW = 8750;
                AddPrisoners(r, 38);
                var lord = owner != null ? owner.Leader : null;
                if (lord != null && lord != me && lord.IsAlive) r.Lords.Add(lord);   // tylko imie w oknie
                r.RansomWorth = 6400; r.Fallen = 23; r.Wounded = 47; r.Renown = 35f; r.Influence = 12f;
            }
            else
            {
                if (town == null) return null;
                var owner = town.OwnerClan;
                var rival = owner != null ? owner.Leader : null;
                r.Kind = SpoilsReport.KTourney; r.Battles = 0; r.Town = town; r.Place = SpoilsReport.Nm(town);
                var t = new SpoilsReport.TInfo
                {
                    Rode = true, SawMission = true, Finished = true, Champion = "you", ChampionWho = 1, Rounds = 4, Bouts = 4, BoutsWon = 4,
                    Final = "you against " + (rival != null && rival != me && rival.Name != null ? rival.Name.ToString() : "a hedge knight")
                };
                r.T = t;
                r.AddCoin(SpoilsReport.LTBetStake, -150); r.AddCoin(SpoilsReport.LTBetWon, 345); r.AddCoin(SpoilsReport.LTPrize, 300);
                r.AddCoin(SpoilsReport.LTChampion, 500); r.AddCoin(SpoilsReport.LTFolk, 60);
                ItemObject prize, horse;
                if (items.TryGetValue(LootTally.Tulow, out prize)) { r.Gear[prize.StringId] = new SpoilsReport.Gain { E = new EquipmentElement(prize), N = 1 }; t.Prize = prize; }
                if (items.TryGetValue(LootTally.Kon, out horse) && horse != prize) r.Gear[horse.StringId] = new SpoilsReport.Gain { E = new EquipmentElement(horse), N = 1 };
                r.Renown = 8f;
            }
            note = "raport probny: przedmiotow " + r.GearCount + " (" + r.Gear.Count + " rodzajow), zloto " + r.CoinTotal.ToString(Inv) + ", jency " + r.TroopCount + " + lordow " + r.Lords.Count;
            return r;
        }

        private static void AddGear(SpoilsReport.Rep r, Dictionary<int, ItemObject> items, int[] counts)
        {
            for (int i = 0; i < Kinds.Length; i++)
            {
                ItemObject it;
                if (!items.TryGetValue(Kinds[i], out it) || it == null || r.Gear.ContainsKey(it.StringId)) continue;
                r.Gear[it.StringId] = new SpoilsReport.Gain { E = new EquipmentElement(it), N = counts[i] };
            }
        }

        /// <summary>Jency do okna: typ z lochu gracza, inaczej "looter", inaczej szeregowy z druzyny (tylko liczba w oknie).</summary>
        private static void AddPrisoners(SpoilsReport.Rep r, int n)
        {
            CharacterObject ch = null;
            var mp = MobileParty.MainParty;
            var pr = mp != null ? mp.PrisonRoster : null;
            if (pr != null) for (int i = 0; i < pr.Count && ch == null; i++) { var x = pr.GetCharacterAtIndex(i); if (x != null && !x.IsHero) ch = x; }
            if (ch == null) { try { ch = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<CharacterObject>("looter") : null; } catch { ch = null; } }
            var mr = mp != null ? mp.MemberRoster : null;
            if (ch == null && mr != null) for (int i = 0; i < mr.Count && ch == null; i++) { var x = mr.GetCharacterAtIndex(i); if (x != null && !x.IsHero) ch = x; }
            if (ch != null) r.Troops[ch] = n;
        }

        /// <summary>Po jednym przedmiocie rodzaju (LootTally): najcenniejszy z sakw gracza, brakujace - najcenniejszy z listy gry do 20 000 (bez wyrobow gracza i rzeczy
        /// spoza handlu); "Goods and supplies" - tylko towary.</summary>
        private static Dictionary<int, ItemObject> PickItems()
        {
            var best = new Dictionary<int, ItemObject>();
            try
            {
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (bag != null)
                    for (int i = 0; i < bag.Count; i++)
                    {
                        var it = bag.GetElementCopyAtIndex(i).EquipmentElement.Item;
                        if (it != null && it.Value > 0) Keep(best, it);
                    }
                bool missing = false;
                foreach (int k in Kinds) if (!best.ContainsKey(k)) missing = true;
                if (missing && MBObjectManager.Instance != null)
                {
                    var more = new Dictionary<int, ItemObject>();
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (it != null && it.Value > 0 && it.Value <= 20000 && !it.NotMerchandise && !it.IsCraftedByPlayer) Keep(more, it);
                    foreach (var kv in more) if (!best.ContainsKey(kv.Key)) best[kv.Key] = kv.Value;
                }
            }
            catch (Exception e) { Stumble("PickItems", e); }
            return best;
        }

        private static void Keep(Dictionary<int, ItemObject> best, ItemObject it)
        {
            int k = LootTally.KindOf(it);
            if (Array.IndexOf(Kinds, k) < 0) return;
            if (k == LootTally.Inne && it.ItemType != ItemObject.ItemTypeEnum.Goods) return;
            ItemObject cur;
            if (!best.TryGetValue(k, out cur) || it.Value > cur.Value) best[k] = it;
        }
    }

    internal static partial class ArmyReview
    {
        // ------------------------------------------------------------ paczka 132: proba zdjec okien (WindowPhotos, tylko autotest)

        private static Slot[] _phWork;
        private static Dictionary<string, int> _phWorkPins;
        private static bool _phDirty, _phFromGame, _phSaved;

        /// <summary>Callback okna: bez skutku, gdy okno otwarto w probie zdjec (photo) albo proba trwa.</summary>
        private static bool Inert(bool photo) { return photo || WindowPhotos.Active; }

        /// <summary>Kopia stanu okna choragwi przed proba (kopia robocza, przypiecia robocze, zmiany, porzadek gry).</summary>
        internal static void PhotoSave()
        {
            _phWork = _work; _phWorkPins = _workPins; _phDirty = _dirty; _phFromGame = _fromGame; _phSaved = true;
        }

        /// <summary>Stan okna choragwi jak przed proba.</summary>
        internal static void PhotoRestore()
        {
            if (!_phSaved) return;
            _work = _phWork; _workPins = _phWorkPins; _dirty = _phDirty; _fromGame = _phFromGame;
            PhotoForget();
        }

        /// <summary>Bez przywracania (inna kampania - Reset juz wyczyscil okno).</summary>
        internal static void PhotoForget()
        {
            _phSaved = false; _phWork = null; _phWorkPins = null; _phDirty = false; _phFromGame = false;
        }

        /// <summary>Okno kroku 1..4 proby zdjec - te same funkcje co gracz. null - otwarte; inaczej powod. note - co pokazano (linia logu).</summary>
        internal static string PhotoOpen(int step, out string note)
        {
            note = null;
            string why;
            if (!Can(out why)) return "przeglad armii niedostepny (" + why + ")";
            switch (step)
            {
                case 1:
                    note = "klucz Army Review Enabled " + Yes(On);
                    OpenMain(false);
                    return null;
                case 2:
                    {
                        if (Behavior() == null) return "brak OrderOfBattleCampaignBehavior";
                        OpenBanners(true);
                        int n = 0;
                        if (_work != null) foreach (var s in _work) if (s != null) n++;
                        note = "choragwi " + n + (_fromGame ? " (porzadek gry)" : " (zapis gry)") + ", zapis Order of Battle " + Yes(Writes);
                        return null;
                    }
                case 3:
                    if (_work == null) return "brak kopii roboczej choragwi (okno choragwi nie otwarte)";
                    for (int i = 0; i < Slots; i++)
                    {
                        if (_work[i] == null) continue;
                        note = "choragiew " + Roman[i] + " " + KindName(_work[i].Cls) + (Writes ? "" : ", tylko odczyt");
                        OpenBanner(i);
                        return null;
                    }
                    return "brak wystawionej choragwi (druzyna bez wojska)";
                case 4:
                    {
                        if (_work == null) return "brak kopii roboczej choragwi (okno choragwi nie otwarte)";
                        int t = PartyTypes().Count;
                        note = t > 0 ? "typow wojska " + t + ", przypiec w kopii " + (_workPins != null ? _workPins.Count : 0) + ", klucz Pin Troop Types " + Yes(PinOn)
                                     : "druzyna bez wojska - okno informacji";
                        OpenPins();
                        return null;
                    }
            }
            if (step >= 8 && step <= 22) return PhotoOpen133(step, out note);
            return "nieznany krok " + step;
        }

        /// <summary>133g: okna paczki 133 (kroki 8-17) - te same funkcje i te same dane co okno gracza (OpenMain i jego podokna); przyciski bez skutku (Inert,
        /// ShowPages). null - otwarte; inaczej powod (okno, ktorego gracz by nie otworzyl - wiersz wyszarzony - nie jest pokazywane; wyjatek: pusty pierscien
        /// "Recent messages" dostaje jeden komunikat proby, zeby lista miala co pokazac).</summary>
        private static string PhotoOpen133(int step, out string note)
        {
            note = null;
            var mp = MobileParty.MainParty;
            switch (step)
            {
                case 8:
                case 9:
                    {
                        var pv = KitPreview.Get(false);   // pamiec po wersjach i dniu - te same liczby co wiersz przegladu (krok 1)
                        if (pv == null || !pv.Ok) return "podglad sprzetu niedostepny (" + (pv != null && pv.Why != null ? pv.Why : "-") + ")";
                        if (pv.Rows.Count == 0) return "podglad bez typow (druzyna bez zdrowych szeregowych)";
                        if (step == 8)
                        {
                            note = "typow " + pv.Rows.Count + " | " + KitPreview.Brief(pv);
                            OpenKitTypes();
                            return null;
                        }
                        KitPreview.TypeRow t = null;
                        foreach (var x in pv.Rows) if (x.Men > 0 && (x.Missing > 0 || x.Foot > 0)) { t = x; break; }
                        bool gaps = t != null;
                        if (t == null) foreach (var x in pv.Rows) if (x.Men > 0) { t = x; break; }
                        if (t == null) return "podglad bez zdrowych ludzi";
                        var pages = Paginate(KitPreview.TypeText(pv, t));
                        note = "typ " + t.Id + " x" + t.Men + ", puste " + t.Missing + ", pieszo " + t.Foot + (gaps ? "" : " (zaden typ z brakami - pierwszy typ)") + ", stron " + PagesInfo(pages);
                        ShowPages(t.Name, pages, 0, OpenKitTypes);
                        return null;
                    }
                case 10:
                    {
                        var b = KitGaps.Last;
                        // recenzja 133: zapis zyje od wczytania (nie trafia do zapisu gry) - bez bitwy w tej sesji okna byc nie moze: krok POMINIETY, nie NIE
                        if (b == null || b.All == null) return WindowPhotos.SkipMark + "brak zapisu ostatniej bitwy od wczytania - wiersz w przegladzie wyszarzony (" + LastBattleBrief() + ")";
                        var pages = Paginate(LastBattleText());
                        note = "zapis: " + LastBattleBrief() + ", stron " + PagesInfo(pages);
                        ShowPages("Last battle", pages, 0);
                        return null;
                    }
                case 11:
                    {
                        var fit = new int[5];
                        var wnd = new int[5];
                        CountMen(mp, fit, wnd);
                        var pairs = SaddleLaw.Pairs(mp);
                        MarchPace.Column col = null;
                        try { col = MarchPace.Explain(mp); } catch (Exception e) { Stumble("Photo.Explain", e); }
                        KitPreview.Result next = null;
                        try { next = KitPreview.Get(false); } catch (Exception e) { Stumble("Photo.KitPreview", e); }
                        HorseView hv;
                        var pages = HorsePages(mp, fit, pairs, col, next, out hv);
                        note = (hv != null ? "z podgladu" : "z SaddleLaw.Pairs (bez podgladu)") + ", stron " + PagesInfo(pages);
                        ShowPages("Horses and harness", pages, 0);
                        return null;
                    }
                case 12:
                case 13:
                    {
                        var mv = MarchOf(mp);
                        if (!mv.Ok) return "predkosc niedostepna (" + mv.Why + ")";   // wiersz "Speed" / "Herd" wyszarzony - gracz go nie otworzy
                        MarchPace.Column col = null;
                        if (step == 13) { try { col = MarchPace.Explain(mp); } catch (Exception e) { Stumble("Photo.Explain", e); } }
                        var pages = Paginate(step == 12 ? SpeedText(mv) : HerdText(mv, col));
                        note = (step == 12 ? SpeedBrief(mv) : HerdBrief(mv)) + ", stron " + PagesInfo(pages);
                        ShowPages(step == 12 ? "Speed" : "Herd", pages, 0);   // "Back" gracza wraca do "On the march" - w probie bez skutku, napis przycisku ten sam
                        return null;
                    }
                case 14:
                case 15:
                case 16:
                    {
                        var pay = PayOf(mp);
                        if (!pay.Ok) return "zold niedostepny (" + pay.Why + ")";   // wiersz "Pay" wyszarzony - gracz go nie otworzy
                        string title = step == 14 ? "Pay" : step == 15 ? "Who gets what" : "Where it goes";
                        var pages = Paginate(step == 14 ? PayText(pay) : step == 15 ? WhoText(pay) : WhereText(pay, mp));
                        note = "zold " + pay.Total + " (suma wierszy " + pay.RowsSum + "), stron " + PagesInfo(pages);
                        ShowPages(title, pages, 0);   // "Back" gracza wraca do okna "Pay" - w probie bez skutku, napis przycisku ten sam
                        return null;
                    }
                case 17:
                    {
                        int had = PlayerNews.Count;
                        int n = PhotoNewsSeed();
                        if (n <= 0) return "pierscien komunikatow pusty takze po komunikacie proby (" + PlayerNews.Brief() + ")";
                        note = "komunikatow " + n + (had == 0 ? " (pierscien byl pusty - dopisany komunikat proby)" : "") + " | " + PlayerNews.Brief();
                        OpenNews();
                        return null;
                    }
                // recenzja 133: okna wyboru "On the march" i "Pay", strony "Unused in the troops' armoury", "Column" i jednego komunikatu - te same funkcje co gracz
                case 18:
                case 21:
                    {
                        var fit = new int[5];
                        var wnd = new int[5];
                        CountMen(mp, fit, wnd);
                        var pairs = SaddleLaw.Pairs(mp);
                        MarchPace.Column col = null;
                        try { col = MarchPace.Explain(mp); } catch (Exception e) { Stumble("Photo.Explain", e); }
                        KitPreview.Result next = null;
                        try { next = KitPreview.Get(false); } catch (Exception e) { Stumble("Photo.KitPreview", e); }
                        HorseView hv;
                        HorsePages(mp, fit, pairs, col, next, out hv);   // jak OpenMain: hv dla strony "Column" (wolne konie z podgladu)
                        var pCol = Paginate(ColumnText(mp, col, pairs, next, hv));
                        if (step == 21)
                        {
                            note = "kolumna " + (col != null ? "rodzaj " + col.Kind + ", n=" + col.N : "wylaczona") + ", konie " + (hv != null ? "z podgladu" : "z SaddleLaw.Pairs") + ", stron " + PagesInfo(pCol);
                            ShowPages("Column", pCol, 0);   // "Back" gracza wraca do "On the march" - w probie bez skutku, napis przycisku ten sam
                            return null;
                        }
                        MarchView mv = null;
                        try { mv = MarchOf(mp); } catch (Exception e) { Stumble("Photo.MarchOf", e); }
                        List<string> pS = null, pH = null;
                        try { pS = Paginate(SpeedText(mv)); pH = Paginate(HerdText(mv, col)); } catch (Exception e) { Stumble("Photo.MarchPages", e); mv = null; }
                        note = (mv != null ? SpeedBrief(mv) : "predkosc niedostepna - wiersze Speed i Herd wyszarzone, jak u gracza");
                        OpenMarch(mv, col, pS, pH, pCol);
                        return null;
                    }
                case 19:
                    {
                        var pay = PayOf(mp);
                        if (!pay.Ok) return "zold niedostepny (" + pay.Why + ")";   // wiersz "Pay" wyszarzony - gracz go nie otworzy
                        note = "zold " + pay.Total + " (suma wierszy " + pay.RowsSum + ")";
                        OpenPay(pay, Paginate(PayText(pay)), Paginate(WhoText(pay)), Paginate(WhereText(pay, mp)));
                        return null;
                    }
                case 20:
                    {
                        var pv = KitPreview.Get(false);
                        if (pv == null || !pv.Ok) return "podglad sprzetu niedostepny (" + (pv != null && pv.Why != null ? pv.Why : "-") + ")";
                        var pages = Paginate(KitPreview.UnusedText(pv));
                        note = KitPreview.UnusedTitle(pv) + ", stron " + PagesInfo(pages);
                        ShowPages("Unused in the troops' armoury", pages, 0, OpenKitTypes);
                        return null;
                    }
                case 22:
                    {
                        var l = PlayerNews.Newest();
                        if (l.Count == 0) { PhotoNewsSeed(); l = PlayerNews.Newest(); }
                        if (l.Count == 0) return "pierscien komunikatow pusty takze po komunikacie proby (" + PlayerNews.Brief() + ")";
                        var it = l[0];
                        var pages = Paginate(PlayerNews.PageText(it));
                        note = "najnowszy komunikat [" + it.Topic + "], stron " + PagesInfo(pages);
                        ShowPages("Message - " + it.Topic, pages, 0, OpenNews);
                        return null;
                    }
            }
            return "nieznany krok " + step;
        }
    }
}
