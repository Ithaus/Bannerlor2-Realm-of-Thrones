using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using MenuContext = TaleWorlds.CampaignSystem.GameState.MenuContext;

namespace CrashScribe
{
    /// <summary>
    /// TRYB AUTOTESTU (Jeff 07.10: "zgoda na autotest"). Gra sama: menu glowne -> nowa kampania
    /// ROT ("Into the Realm", ta sama droga co gracz) -> kreator postaci z domyslnymi wyborami ->
    /// czekanie w bezpiecznym miescie z przewijaniem czasu -> po N dobach zapis "autotest-..." ->
    /// wyjscie z gry. Wszystko do Documents\...\CrashScribe\autotest-RRRR-MM-DD.log.
    ///
    /// WLACZNIK: wylacznie plik CrashScribe\autotest.json. Przy starcie gry (OnSubModuleLoad)
    /// plik dostaje nazwe autotest.json.uzyty ZANIM cokolwiek sie stanie - nastepne zwykle
    /// uruchomienie jest zwykle. Bez pliku: Active = false i ZADNA czesc tego trybu nie rusza
    /// (zadnej latki, zadnego zachowania kampanii, zadnego pliku).
    ///
    /// Na czas autotestu autozapis gry jest wylaczony (latki na SaveHandler.TryAutoSave /
    /// ForceAutoSave), a kazdy zapis o nazwie spoza "autotest-" jest przekierowany na
    /// "autotest-przekierowany-..." (latka na MBSaveLoad.OverwriteSaveAux - jedyna droga do
    /// pliku dla SaveAs, QuickSave i AutoSave) - saveauto1..3 Jeffa sa nietykalne.
    ///
    /// [AT2] TRYB ZDJEC (klucz "photos" w przelaczniku, AutotestPhotos.cs): po wejsciu na mape, przy jasnej
    /// porze dnia, kamera mapy na kazdy cel z kilku wysokosci i zrzut ekranu do CrashScribe\zdjecia\<run>\;
    /// potem zwykly bieg. Bez klucza "photos" ten etap nie istnieje.
    /// </summary>
    internal static class Autotest
    {
        internal const string SwitchName = "autotest.json";
        internal const string UsedSuffix = ".uzyty";
        internal const string SavePrefix = "autotest-";

        // --- konfiguracja z przelacznika ---
        internal static bool Active;
        internal static int Days = 40;
        internal static bool SaveAtEnd = true;
        internal static bool QuitAtEnd = true;
        internal static float Speed;               // 0 = mnoznik jak w grze Jeffa (Armoury MCM "Fast Forward Multiplier")
        internal static int CheckpointDays = 60;   // zapis kontrolny "autotest-dlugi" co tyle dob (tylko bieg dluzszy niz 60 dob)
        internal static int StallQuitMin = 10;     // czas gry stoi tyle minut mimo prob odblokowania -> koniec
        internal static int LimitS;                // miekki limit calego biegu w s (0 = brak); skrypt daje swoj twardy
        internal static string RunId = "";

        internal static string LogPath;
        internal static string Dir;                // katalog CrashScribe w Documents (log autotestu, zdjecia)
        internal static long Frames;               // [AT2] klatki (kazde wywolanie Tick) - zdjecia czekaja tez na klatki, nie tylko sekundy
        internal static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object LogGate = new object();
        private static readonly Stopwatch Clock = new Stopwatch();

        // [AT2] Photos przed Saving: porownania "Now < Stage.Saving" / ">= Stage.Saving" licza je jak bieg
        internal enum Stage { Off, Menu, Creation, Settle, Run, Photos, Saving, Quitting, Done }
        internal static Stage Now = Stage.Off;

        private static double _lastTick = -1, _stageSince, _menuSince = -1;
        private static int _menuTries;
        private static bool _noSaveGuard = true;   // dopoki Install nie potwierdzi obu latek, kampania nie rusza

        // bieg
        private static int _lastDay = -1;
        private static double _lastDayAt, _runStartAt;
        private static bool _firstPartial = true;
        private static string _partialNote;        // [AT2] opis niepelnej doby (null = "bieg ruszyl w trakcie doby")
        private static readonly List<double> PerDay = new List<double>();
        private static double _lastTicks = -1;
        private static double _lastMoveAt, _lastStallDiag, _lastAction;
        private static int _errors, _errorsAtDay;
        private static int _windows;
        private static int _stallTicks;            // Campaign.CurrentTickCount przy ostatnim ruchu zegara / ostatniej linii STOI

        // miasto
        private static Settlement _placedTown;
        private static double _placedAt;
        private static int _placeTries, _noWaitTries;
        // StringId, nie Settlement: inicjalizator statyczny tej klasy rusza tez w zwyklej grze (pierwszy
        // odczyt Active) - ma w nim nie byc zadnego typu gry
        private static readonly HashSet<string> BadTowns = new HashSet<string>();
        private static bool _waitingLogged;

        // zapis i wyjscie
        private static string _saveName;
        private static double _saveAt;
        private static bool _saveDone, _saveFinal, _saveWaitNoted;
        private static double _quitAt;
        private static volatile bool _quitCalled;   // Utilities.QuitGame zawolane (czyta tez straznik zawieszen)
        private static bool _finishOk;
        private static string _finishWhy;

        // potkniecia: pierwsze w danym miejscu w calosci, potem tylko licznik
        private static readonly Dictionary<string, int> Stumbles = new Dictionary<string, int>();

        internal static bool PreferDecline
        {
            get { return Now == Stage.Settle || Now == Stage.Run || Now == Stage.Photos || Now == Stage.Saving; }
        }

        // ------------------------------------------------------------------ wlacznik

        /// <summary>
        /// Wolane w OnSubModuleLoad zaraz po Scribe.Init. Bez pliku-przelacznika: natychmiastowy
        /// powrot, nic sie nie zmienia. Z plikiem: najpierw ZUZYCIE (zmiana nazwy), potem reszta.
        /// Jesli pliku nie da sie zuzyc, autotest NIE startuje (inaczej kazde uruchomienie gry
        /// przez Jeffa byloby autotestem).
        /// </summary>
        internal static void Arm(string dir)
        {
            try
            {
                if (Active || string.IsNullOrEmpty(dir)) return;
                string sw = Path.Combine(dir, SwitchName);
                if (!File.Exists(sw)) return;
                string text = null;
                try { text = File.ReadAllText(sw); } catch { }
                string used = sw + UsedSuffix;
                try
                {
                    if (File.Exists(used)) File.Delete(used);
                    File.Move(sw, used);
                }
                catch (Exception e)
                {
                    try { Scribe.Line("AUTOTEST: przelacznika " + sw + " nie da sie zuzyc (" + e.GetType().Name + ": " + e.Message + ") - autotest NIE rusza."); } catch { }
                    return;
                }
                Dir = dir;
                Parse(text);
                LogPath = Path.Combine(dir, "autotest-" + DateTime.Now.ToString("yyyy-MM-dd", Inv) + ".log");
                Active = true;
                Now = Stage.Menu;
                Clock.Restart();
                Header(text, used);
            }
            catch (Exception e)
            {
                try { Scribe.Report("AUTOTEST", e, "Autotest.Arm", null); } catch { }
            }
        }

        internal static void Parse(string json)
        {
            Days = Int(json, "days", 40, 1, 2000);
            SaveAtEnd = Bool(json, "save", true);
            QuitAtEnd = Bool(json, "quit", true);
            Speed = Flt(json, "speed", 0f, 0f, 64f);
            CheckpointDays = Int(json, "checkpoint", 60, 1, 2000);
            StallQuitMin = Int(json, "stall_min", 10, 1, 240);
            LimitS = Int(json, "limit_s", 0, 0, 30 * 24 * 3600);
            RunId = Str(json, "run", "");
            AutotestPhotos.Parse(json);   // [AT2] klucze photos / photo_shots / photo_hours; bez nich lista celow pusta
        }

        internal static string Raw(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\"(?<s>[^\"]*)\"|(?<v>[^,}\\s]+))", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            return m.Groups["s"].Success ? m.Groups["s"].Value : m.Groups["v"].Value;
        }

        private static int Int(string json, string key, int def, int min, int max)
        {
            int v;
            var r = Raw(json, key);
            if (r == null || !int.TryParse(r, NumberStyles.Integer, Inv, out v)) return def;
            return Math.Max(min, Math.Min(max, v));
        }

        private static float Flt(string json, string key, float def, float min, float max)
        {
            float v;
            var r = Raw(json, key);
            if (r == null || !float.TryParse(r, NumberStyles.Float, Inv, out v)) return def;
            return Math.Max(min, Math.Min(max, v));
        }

        private static bool Bool(string json, string key, bool def)
        {
            var r = Raw(json, key);
            if (r == null) return def;
            if (r.Equals("true", StringComparison.OrdinalIgnoreCase) || r == "1") return true;
            if (r.Equals("false", StringComparison.OrdinalIgnoreCase) || r == "0") return false;
            return def;
        }

        internal static string Str(string json, string key, string def)
        {
            var r = Raw(json, key);
            return string.IsNullOrEmpty(r) ? def : Regex.Replace(r, "[^A-Za-z0-9_.:-]", "");
        }

        private static void Header(string json, string used)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=====================================================================");
            sb.AppendLine(" AUTOTEST CrashScribe " + Ver.Text + "   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
            sb.AppendLine("=====================================================================");
            Write(sb.ToString().TrimEnd(), -1);
            Note("START run=" + (RunId.Length > 0 ? RunId : "-") + " | przelacznik zuzyty -> " + used, true);
            Note("  tresc przelacznika: " + OneLine(json, 300), false);
            Note("  dni=" + Days + " zapis=" + (SaveAtEnd ? "tak" : "nie") + " wyjscie=" + (QuitAtEnd ? "tak" : "nie")
                 + " mnoznik=" + (Speed > 0 ? "x" + Speed.ToString("0.##", Inv) + " (wymuszony)" : "jak w grze (Armoury MCM)")
                 + (Days > 60 ? " | zapis kontrolny \"autotest-dlugi\" co " + CheckpointDays + " dob" : "")
                 + " | koniec przy postoju " + StallQuitMin + " min"
                 + (LimitS > 0 ? " | limit " + (LimitS / 60) + " min" : ""), true);
            string ph = AutotestPhotos.Describe();
            if (ph.Length > 0) Note("  zdjecia: " + ph, true);
        }

        // ------------------------------------------------------------------ log

        internal static void Note(string text, bool mirror)
        {
            if (!Active) return;
            Write("[" + DateTime.Now.ToString("HH:mm:ss", Inv) + "] " + text, -1);
            if (mirror) { try { Scribe.Line("AUTOTEST: " + text); } catch { } }
        }

        internal static void Note(string text) { Note(text, false); }

        /// <summary>Zapis z innego watku (straznik zawieszen, bledy z FirstChance) - nigdy na wiszaco.</summary>
        internal static void NoteOffThread(string text)
        {
            if (!Active) return;
            Write("[" + DateTime.Now.ToString("HH:mm:ss", Inv) + "] " + text, 500);
        }

        /// <summary>Kazdy NOWY raport bledu CrashScribe (nie powtorki) - jedna linia; szczegoly w session-*.log.</summary>
        internal static void NoteError(string kind, string where, Exception ex)
        {
            if (!Active || ex == null) return;
            Interlocked.Increment(ref _errors);
            NoteOffThread("BLAD: " + kind + " | " + where + " | " + ex.GetType().Name + ": " + OneLine(ex.Message, 160) + " (szczegoly w " + SafeName(Scribe.SessionFile) + ")");
        }

        private static bool Write(string line, int timeoutMs)
        {
            if (LogPath == null) return false;
            bool got = false;
            try
            {
                if (timeoutMs < 0) { Monitor.Enter(LogGate); got = true; }
                else got = Monitor.TryEnter(LogGate, timeoutMs);
                if (!got) return false;
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                return true;
            }
            catch { return false; }
            finally { if (got) Monitor.Exit(LogGate); }
        }

        /// <summary>Potkniecie: pierwsze w danym miejscu w calosci (log + raport CrashScribe), dalej co 50.</summary>
        internal static void Fail(string where, Exception e)
        {
            try
            {
                int n;
                Stumbles.TryGetValue(where, out n);
                Stumbles[where] = ++n;
                var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                if (n == 1)
                {
                    Note("POTKNIECIE w " + where + ": " + inner.GetType().Name + ": " + OneLine(inner.Message, 200), true);
                    try { Scribe.Report("AUTOTEST", inner, "Autotest." + where, null); } catch { }
                }
                else if (n % 50 == 0) Note("POTKNIECIE w " + where + " x" + n + " (ostatnie: " + inner.GetType().Name + ")");
            }
            catch { }
        }

        internal static string OneLine(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, "\\s+", " ").Trim();
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }

        private static string SafeName(string path)
        {
            try { return string.IsNullOrEmpty(path) ? "session-*.log" : Path.GetFileName(path); } catch { return "session-*.log"; }
        }

        internal static void CountWindow() { _windows++; }

        // ------------------------------------------------------------------ latki (tylko gdy Active)

        internal static void Install(Harmony h)
        {
            if (!Active) return;
            bool tryAuto = false, force = false, redirect = false;
            try
            {
                var m = AccessTools.Method(typeof(SaveHandler), "TryAutoSave");
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(Autotest), nameof(SkipAutoSave))); tryAuto = true; }
            }
            catch (Exception e) { Fail("Install.TryAutoSave", e); }
            try
            {
                var m = AccessTools.Method(typeof(SaveHandler), "ForceAutoSave");
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(Autotest), nameof(SkipAutoSave))); force = true; }
            }
            catch (Exception e) { Fail("Install.ForceAutoSave", e); }
            try
            {
                var m = AccessTools.Method(typeof(MBSaveLoad), "OverwriteSaveAux");
                if (m != null && m.GetParameters().Any(p => p.Name == "saveName" && p.ParameterType == typeof(string)))
                {
                    var hm = new HarmonyMethod(typeof(Autotest), nameof(RedirectSave)) { priority = Priority.First };
                    h.Patch(m, prefix: hm);
                    redirect = true;
                }
            }
            catch (Exception e) { Fail("Install.OverwriteSaveAux", e); }

            Note("AUTOZAPIS: autozapis gry " + (tryAuto ? "WYLACZONY" : "BRAK latki TryAutoSave")
                 + ", wymuszony autozapis " + (force ? "WYLACZONY" : "BRAK latki ForceAutoSave")
                 + ", zapisy spoza \"" + SavePrefix + "\" " + (redirect ? "PRZEKIEROWANE" : "BRAK latki OverwriteSaveAux"), true);
            _noSaveGuard = !tryAuto || !redirect;
            if (_noSaveGuard)
            {
                Note("STOP: brak ochrony zapisow Jeffa (saveauto1..3) - autotest nie zalozy kampanii, gra zostanie zamknieta.", true);
            }
            try
            {
                foreach (var line in AutotestUi.SelfCheck()) Note("  sygnatury: " + line);
            }
            catch (Exception e) { Fail("Install.SelfCheck", e); }
        }

        private static int _autoSaveSkips;

        /// <summary>Prefiks na SaveHandler.TryAutoSave i ForceAutoSave: w autotescie autozapis gry nie rusza.</summary>
        public static bool SkipAutoSave(MethodBase __originalMethod)
        {
            if (!Active) return true;
            _autoSaveSkips++;
            if (_autoSaveSkips == 1 || _autoSaveSkips % 500 == 0)
                Note("AUTOZAPIS gry pominiety (" + (__originalMethod != null ? __originalMethod.Name : "?") + ", razy: " + _autoSaveSkips + ")");
            return false;
        }

        /// <summary>
        /// Prefiks na MBSaveLoad.OverwriteSaveAux (SaveAs, QuickSave i AutoSave przechodza tedy):
        /// w autotescie kazdy zapis spoza "autotest-" idzie pod nazwe "autotest-przekierowany-...".
        /// </summary>
        public static void RedirectSave(ref string saveName)
        {
            try
            {
                if (!Active) return;
                if (!string.IsNullOrEmpty(saveName) && saveName.StartsWith(SavePrefix, StringComparison.OrdinalIgnoreCase)) return;
                string orig = saveName ?? "";
                saveName = SavePrefix + "przekierowany-" + Regex.Replace(orig, "[^A-Za-z0-9_-]", "_");
                Note("ZAPIS PRZEKIEROWANY: gra chciala zapisac \"" + orig + "\" -> \"" + saveName + "\"", true);
            }
            catch { }
        }

        // ------------------------------------------------------------------ zdarzenia kampanii

        internal static void OnSaveOver(bool ok, string name)
        {
            if (!Active) return;
            try
            {
                if (_saveName != null && string.Equals(name, _saveName, StringComparison.OrdinalIgnoreCase))
                {
                    Note("ZAPIS koniec: \"" + name + "\" " + (ok ? "OK" : "BLAD") + " (" + (Clock.Elapsed.TotalSeconds - _saveAt).ToString("0.0", Inv) + " s)", true);
                    _saveDone = true;
                    _saveName = null;
                }
                else Note("ZAPIS (nie nasz): \"" + name + "\" " + (ok ? "OK" : "BLAD"), true);
            }
            catch (Exception e) { Fail("OnSaveOver", e); }
        }

        // ------------------------------------------------------------------ tik

        /// <summary>Z SubModuleMain.OnApplicationTick (watek glowny, poza ramka widokow menu).</summary>
        internal static void Tick(float dt)
        {
            if (!Active || Now == Stage.Done) return;
            Frames++;
            double t = Clock.Elapsed.TotalSeconds;
            if (t - _lastTick < 0.25) return;
            _lastTick = t;
            // okna najpierw; etapy biegna dalej (kazdy sam czeka na zamkniete okna), zeby postoj
            // przy oknie, ktorego nie da sie zamknac, tez skonczyl sie czysto (zapis + wyjscie)
            try { AutotestUi.Handle(t); } catch (Exception e) { Fail("okna", e); }
            try
            {
                if (LimitS > 0 && Now < Stage.Saving && t > LimitS)
                {
                    Finish("miekki limit czasu " + (LimitS / 60) + " min (doba " + Math.Max(0, _lastDay) + "/" + Days + ")", false, t);
                    return;
                }
                switch (Now)
                {
                    case Stage.Menu: StepMenu(t); break;
                    case Stage.Creation: StepCreation(t); break;
                    case Stage.Settle: StepSettle(t); break;
                    case Stage.Run: StepRun(t); break;
                    case Stage.Photos: AutotestPhotos.Step(t); break;
                    case Stage.Saving: StepSaving(t); break;
                    case Stage.Quitting: StepQuit(t); break;
                }
            }
            catch (Exception e) { Fail("etap " + Now, e); }
        }

        internal static object ActiveState()
        {
            try { return GameStateManager.Current != null ? GameStateManager.Current.ActiveState : null; } catch { return null; }
        }

        private static string StateName()
        {
            var s = ActiveState();
            return s != null ? s.GetType().Name : "-";
        }

        internal static double DayNow()
        {
            try { return (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch { return 0.0; }
        }

        private static void Go(Stage s, double t, string why)
        {
            Now = s;
            _stageSince = t;
            if (!string.IsNullOrEmpty(why)) Note("ETAP " + why, true);
        }

        // ------------------------------------------------------------------ 1. menu glowne

        private static void StepMenu(double t)
        {
            if (!(ActiveState() is InitialState)) { _menuSince = -1; return; }
            if (_menuSince < 0) { _menuSince = t; Note("ETAP menu glowne: gra w menu glownym po " + (int)t + " s od startu", true); return; }
            if (t - _menuSince < 8 || AutotestUi.AnyWindowOpen()) return;   // menu ROT laduje scene; okna obsluguje Handle

            if (_noSaveGuard) { Finish("brak ochrony zapisow - kampania nie zalozona", false, t); return; }

            var mod = TaleWorlds.MountAndBlade.Module.CurrentModule;
            var opts = mod.GetInitialStateOptions().ToList();
            string ids = string.Join(", ", opts.Select(o => o.Id).ToArray());
            InitialStateOption pick = opts.FirstOrDefault(o => o.Id == "Into the Realm") ?? opts.FirstOrDefault(o => o.Id == "SandBoxNewGame");
            if (pick == null) { Note("ETAP menu glowne: opcje [" + ids + "]", true); Finish("brak opcji nowej kampanii (Into the Realm / SandBoxNewGame)", false, t); return; }
            var dis = pick.IsDisabledAndReason != null ? pick.IsDisabledAndReason() : (false, null);
            if (dis.Item1)
            {
                if (++_menuTries > 6) { Finish("opcja " + pick.Id + " wylaczona: " + dis.Item2, false, t); return; }
                _menuSince = t;
                return;
            }
            Note("ETAP menu glowne: opcje [" + ids + "] -> wybieram \"" + pick.Name + "\" [" + pick.Id + "] (to samo co klikniecie gracza)", true);
            Go(Stage.Creation, t, null);
            pick.DoAction();
        }

        // ------------------------------------------------------------------ 2. kreator postaci

        private static void StepCreation(double t)
        {
            var st = ActiveState();
            var cc = st as CharacterCreationState;
            if (cc != null)
            {
                if (!AutotestUi.AnyWindowOpen()) AutotestUi.DriveCreation(cc, t);
                else if (t - _stageSince > 900) Finish("okno w kreatorze postaci nie daje sie zamknac (" + AutotestUi.OpenWindowName() + ")", false, t);
                return;
            }
            if (st is global::TaleWorlds.CampaignSystem.GameState.MapState)
            {
                string lbl = "kampania: mapa po " + (int)(t - _stageSince) + " s od wyboru w menu (" + DayLabel() + ")";
                // [AT2] zdjecia od razu na mapie (gracz jeszcze poza miastem, czas STOP); ciemno albo menu - zdjecia z biegu
                if (AutotestPhotos.Pending)
                {
                    Go(Stage.Photos, t, lbl + " -> zdjecia (" + AutotestPhotos.Plan() + ")");
                    AutotestPhotos.Begin(t, false);
                }
                else Go(Stage.Settle, t, lbl);
                return;
            }
            if (st is InitialState && Game.Current == null && t - _stageSince > 60)   // StartNewGame od razu wstawia GameLoadingState
            {
                if (++_menuTries > 3) { Finish("gra stoi w menu glownym mimo wyboru nowej kampanii", false, t); return; }
                Note("ETAP menu glowne: nowa kampania nie ruszyla - ponawiam", true);
                _menuSince = -1;
                Go(Stage.Menu, t, null);
                return;
            }
            if (t - _stageSince > 900) Finish("kreator postaci nie ruszyl przez 15 min (stan gry: " + StateName() + ")", false, t);
        }

        internal static void CreationStuck(string what, double t)
        {
            Finish("kreator postaci utknal: " + what, false, t);
        }

        // ------------------------------------------------------------------ 3. miasto

        private static void StepSettle(double t)
        {
            if (AutotestUi.AnyWindowOpen()) return;
            if (t - _stageSince < 3) return;   // okna po kreatorze (BK, ROT) maja czas sie pokazac
            if (EnsureWaiting(t))
            {
                StartRun(t);
                return;
            }
            // osadzanie stoi (np. menu "town_outside" po spotkaniu z miastem, rozmowa, obce menu
            // po kreatorze) - te same proby co w biegu: wyjscie z menu ("Leave"), koniec rozmowy;
            // potem EnsureWaiting przenosi do nastepnego miasta
            if (t - _stageSince > 20 && t - _lastStallDiag > 20)
            {
                _lastStallDiag = t;
                Note("OSADZANIE: po " + (int)(t - _stageSince) + " s gracz nadal nie czeka w miescie: " + Where());
                TryUnstick(t, t - _stageSince, false);
            }
            if (_placeTries > 8 || t - _stageSince > 600)
                Finish("nie udalo sie osadzic gracza w miescie (" + _placeTries + " prob, " + Where() + ")", false, t);
        }

        /// <summary>
        /// Pilnuje, zeby gracz czekal w miescie z plynacym czasem. Zwraca true, gdy tak jest.
        /// Klikniecia menu tylko z tego ticku (nigdy z wnetrza opcji menu oczekiwania).
        /// </summary>
        private static bool EnsureWaiting(double t)
        {
            var c = Campaign.Current;
            if (c == null) return false;
            if (!(ActiveState() is global::TaleWorlds.CampaignSystem.GameState.MapState)) return false;
            if (AutotestUi.AnyWindowOpen()) return false;
            if (c.ConversationManager != null && c.ConversationManager.IsConversationInProgress) return false;
            var mp = MobileParty.MainParty;
            if (mp == null || Hero.MainHero == null || Hero.MainHero.IsPrisoner) return false;

            var ctx = c.CurrentMenuContext;
            var menu = ctx != null ? ctx.GameMenu : null;
            string id = menu != null ? menu.StringId : null;
            var here = mp.CurrentSettlement;

            if (here != null && here.IsTown && !here.IsUnderSiege)
            {
                if (id == "town_wait_menus")
                {
                    if (!menu.IsWaitActive) menu.StartWait();
                    ForceRun(c);
                    if (!_waitingLogged)
                    {
                        _waitingLogged = true;
                        Note("ETAP miasto: czekam w " + here.Name + " [" + here.StringId + "] (menu town_wait_menus, czas " + c.TimeControlMode + " x" + c.SpeedUpMultiplier.ToString("0.##", Inv) + ")", true);
                    }
                    return true;
                }
                _waitingLogged = false;
                if (id != null && id.EndsWith("town_outside", StringComparison.Ordinal) && t - _lastAction > 2)
                {
                    // miasto nie wpuszcza (menu przed brama) - to miasto odpada, "Leave" i nastepne
                    _lastAction = t;
                    BadTowns.Add(here.StringId);
                    Note("ETAP miasto: " + here.Name + " nie wpuszcza (menu " + id + ", opcje " + ListOptions(ctx) + ") - wychodze, nastepne miasto", true);
                    ClickLeave(ctx);
                    return false;
                }
                if (id == "town" && t - _lastAction > 2)
                {
                    _lastAction = t;
                    if (ClickOption(ctx, "town_wait")) { _noWaitTries = 0; Note("ETAP miasto: " + here.Name + " - klikam \"Wait here for some time\" (town_wait)", true); }
                    else if (++_noWaitTries >= 3)
                    {
                        // tu czekac nie wolno (dostep do miasta) - to miasto odpada, wyjscie i nastepne
                        _noWaitTries = 0;
                        BadTowns.Add(here.StringId);
                        Note("ETAP miasto: " + here.Name + " - opcja town_wait niedostepna; opcje: " + ListOptions(ctx) + " - wychodze, nastepne miasto", true);
                        ClickLeave(ctx);
                    }
                }
                return false;
            }
            _waitingLogged = false;

            // gracz poza osada (po kreatorze albo po wyjsciu z miasta): czas STOP do konca przenosin,
            // zeby w tych kilku sekundach przy x8 nie wpadl na mape w spotkanie (bandyci, wrogowie)
            if (here == null && !c.TimeControlModeLock && c.TimeControlMode != CampaignTimeControlMode.Stop)
                c.TimeControlMode = CampaignTimeControlMode.Stop;

            // miasto, do ktorego przenieslismy, nie przyjelo gracza (30 s) albo gracz jest z powrotem na
            // mapie bez menu i spotkania (wyszedl / zostal wyprowadzony) - to miasto odpada, nastepne
            if (_placedTown != null && here != _placedTown
                && (t - _placedAt > 30 || (here == null && menu == null && PlayerEncounter.Current == null)))
            {
                BadTowns.Add(_placedTown.StringId);
                Note("ETAP miasto: " + _placedTown.Name + " nie zatrzymalo gracza (" + Where() + ") - nastepne miasto", true);
                _placedTown = null;
            }
            if (here == null && menu == null && PlayerEncounter.Current == null && mp.MapEvent == null && mp.Army == null && t - _lastAction > 5)
            {
                _lastAction = t;
                PlaceInTown(t);
            }
            return false;
        }

        private static void ForceRun(Campaign c)
        {
            if (c.TimeControlMode != CampaignTimeControlMode.UnstoppableFastForward && !c.TimeControlModeLock)
                c.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
            if (Speed > 0f && Math.Abs(c.SpeedUpMultiplier - Speed) > 0.01f) c.SpeedUpMultiplier = Speed;
        }

        /// <summary>
        /// Najbezpieczniejsze miasto: najdalej od najblizszej warowni frakcji, z ktora jego wlasciciel
        /// jest w wojnie (bandyci sie nie licza). Teleport pod brame i zwykle spotkanie z osada -
        /// dalej gra prowadzi to tak, jakby gracz dojechal sam.
        /// </summary>
        private static void PlaceInTown(double t)
        {
            var mp = MobileParty.MainParty;
            Settlement best = null;
            float bestNear = -1f;
            int candidates = 0;
            IFaction me = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null;
            foreach (var s in Settlement.All)
            {
                if (s == null || !s.IsTown || s.IsUnderSiege || BadTowns.Contains(s.StringId)) continue;
                var f = s.MapFaction;
                if (f == null || f.IsBanditFaction) continue;
                if (me != null && FactionManager.IsAtWarAgainstFaction(me, f)) continue;
                candidates++;
                float near = float.MaxValue;
                foreach (var o in Settlement.All)
                {
                    if (o == null || o == s || !(o.IsTown || o.IsCastle)) continue;
                    var of = o.MapFaction;
                    if (of == null || of.IsBanditFaction || !FactionManager.IsAtWarAgainstFaction(f, of)) continue;
                    float d = s.Position.Distance(o.Position);
                    if (d < near) near = d;
                }
                if (near > bestNear) { bestNear = near; best = s; }
            }
            _placeTries++;
            if (best == null) { Note("ETAP miasto: brak miasta do osadzenia (kandydatow " + candidates + ")", true); return; }
            mp.SetMoveModeHold();
            mp.Position = best.GatePosition;
            _placedTown = best;
            _placedAt = t;
            Note("ETAP miasto: przenosze druzyne pod brame " + best.Name + " [" + best.StringId + "] (" + best.MapFaction.Name
                 + "), najblizsza wroga warownia " + (bestNear >= float.MaxValue / 2 ? "brak (frakcja bez wojen)" : bestNear.ToString("0", Inv) + " j.")
                 + " | kandydatow " + candidates + ", proba " + _placeTries, true);
            EncounterManager.StartSettlementEncounter(mp, best);
        }

        internal static bool ClickOption(MenuContext ctx, string optionId)
        {
            var gm = Campaign.Current.GameMenuManager;
            int n = gm.GetVirtualMenuOptionAmount(ctx);
            for (int i = 0; i < n; i++)
            {
                var opt = gm.GetVirtualGameMenuOption(ctx, i);
                if (opt == null || opt.IdString != optionId) continue;
                if (!gm.GetVirtualMenuOptionConditionsHold(ctx, i) || !gm.GetVirtualMenuOptionIsEnabled(ctx, i)) return false;
                ctx.InvokeConsequence(i);
                return true;
            }
            return false;
        }

        /// <summary>Klika "wyjscie" z menu (opcja isLeave) - to samo, co gracz wybierajacy "Leave".</summary>
        internal static bool ClickLeave(MenuContext ctx)
        {
            var gm = Campaign.Current.GameMenuManager;
            int n = gm.GetVirtualMenuOptionAmount(ctx);
            int only = -1, avail = 0;
            for (int i = 0; i < n; i++)
            {
                bool ok = gm.GetVirtualMenuOptionConditionsHold(ctx, i) && gm.GetVirtualMenuOptionIsEnabled(ctx, i);
                if (!ok) continue;
                avail++;
                only = i;
                if (gm.GetVirtualMenuOptionIsLeave(ctx, i))
                {
                    Note("ODBLOKOWANIE: menu " + ctx.GameMenu.StringId + " - klikam wyjscie \"" + gm.GetVirtualMenuOptionText(ctx, i) + "\" [" + gm.GetVirtualGameMenuOption(ctx, i).IdString + "]", true);
                    ctx.InvokeConsequence(i);
                    return true;
                }
            }
            if (avail == 1 && only >= 0)
            {
                Note("ODBLOKOWANIE: menu " + ctx.GameMenu.StringId + " - jedyna opcja \"" + gm.GetVirtualMenuOptionText(ctx, only) + "\" [" + gm.GetVirtualGameMenuOption(ctx, only).IdString + "]", true);
                ctx.InvokeConsequence(only);
                return true;
            }
            return false;
        }

        private static string ListOptions(MenuContext ctx)
        {
            try
            {
                var gm = Campaign.Current.GameMenuManager;
                int n = gm.GetVirtualMenuOptionAmount(ctx);
                var parts = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    var opt = gm.GetVirtualGameMenuOption(ctx, i);
                    bool ok = gm.GetVirtualMenuOptionConditionsHold(ctx, i) && gm.GetVirtualMenuOptionIsEnabled(ctx, i);
                    parts.Add((opt != null ? opt.IdString : "?") + (ok ? "" : "(-)") + (gm.GetVirtualMenuOptionIsLeave(ctx, i) ? "(wyjscie)" : ""));
                }
                return "[" + string.Join(", ", parts.ToArray()) + "]";
            }
            catch { return "[?]"; }
        }

        // ------------------------------------------------------------------ 4. bieg

        private static void StartRun(double t)
        {
            _runStartAt = t;
            _lastDayAt = t;
            _lastDay = (int)Math.Floor(DayNow());
            _firstPartial = true;
            _partialNote = null;
            _lastTicks = CampaignTime.Now.ToMilliseconds;
            _stallTicks = Campaign.Current != null ? Campaign.Current.CurrentTickCount : 0;
            _lastMoveAt = t;
            _errorsAtDay = _errors;
            Go(Stage.Run, t, "bieg: przewijanie do doby " + Days + " (teraz " + DayLabel() + ", " + Where() + ")");
            try { foreach (var line in AutotestUi.SelfCheck(onlyMissing: true)) Note("  sygnatury (ponownie): " + line); } catch { }
            if (_lastDay >= Days) Finish("dotarl do " + _lastDay + "/" + Days + " dob", true, t);
        }

        private static void StepRun(double t)
        {
            var c = Campaign.Current;
            if (c == null || ActiveState() is InitialState)
            {
                Finish("gra wrocila do menu glownego w trakcie biegu", false, t);
                return;
            }
            bool waiting = EnsureWaiting(t);

            // [AT2] zdjecia odlozone przy wejsciu na mape (ciemno / menu): pierwsza jasna pora w miescie -> wyjscie
            // spod bramy na czas zdjec (czas STOP), potem EnsureWaiting osadza gracza jak zwykle
            if (waiting && AutotestPhotos.WantNow())
            {
                Go(Stage.Photos, t, "zdjecia: " + AutotestPhotos.Plan() + " - wychodze z miasta na czas zdjec (czas STOP)");
                AutotestPhotos.Begin(t, true);
                return;
            }

            // doby
            int d = (int)Math.Floor(DayNow());
            if (d > _lastDay && _lastDay >= 0)
            {
                double sec = t - _lastDayAt;
                int jumped = d - _lastDay;
                string mark = _firstPartial ? " (" + (_partialNote ?? "niepelna - bieg ruszyl w trakcie doby") + ")" : (jumped > 1 ? " (" + jumped + " doby naraz)" : "");
                if (!_firstPartial) for (int k = 0; k < jumped; k++) PerDay.Add(sec / jumped);
                _firstPartial = false;
                _partialNote = null;
                _lastDay = d;
                _lastDayAt = t;
                int newErr = _errors - _errorsAtDay;
                _errorsAtDay = _errors;
                Note("DOBA " + d + "/" + Days + " | " + sec.ToString("0.0", Inv) + " s" + mark + " | " + ShortWhere(c)
                     + " | okna " + _windows + " | bledy +" + newErr + " (razem " + _errors + ")");
                if (d % 10 == 0 && PerDay.Count > 0) Note(Averages(d));
                if (Days > 60 && CheckpointDays > 0 && d % CheckpointDays == 0 && d < Days) StartSave("autotest-dlugi", false, t);
                if (d >= Days) { Finish("dotarl do " + d + "/" + Days + " dob", true, t); return; }
            }

            // postoj czasu gry
            double ticks = CampaignTime.Now.ToMilliseconds;
            if (ticks != _lastTicks || c.SaveHandler.IsSaving) { _lastTicks = ticks; _lastMoveAt = t; _stallTicks = c.CurrentTickCount; return; }
            double still = t - _lastMoveAt;
            if (still > 20 && t - _lastStallDiag > 30)
            {
                _lastStallDiag = t;
                // [AT1b] tiki kampanii (Campaign.Tick z MapState.OnTick): 0 = MapState w ogole nie tyka
                // (stan wstrzymany - np. menu Esc - albo inny stan na wierzchu); >0 przy stojacym zegarze = dt 0
                int tc = c.CurrentTickCount, dtc = tc - _stallTicks;
                _stallTicks = tc;
                Note("STOI " + (int)still + " s: " + Where() + " | tiki kampanii +" + dtc
                     + (dtc == 0 ? " (MapState nie tyka)" : " (MapState tyka, dt " + c.CampaignDt.ToString("0.#####", Inv) + ")"));
                if (still > 60 && still < 95) Note("  stan gry (CrashScribe):" + Environment.NewLine + GameState.Describe());
                TryUnstick(t, still, waiting);
            }
            if (still > StallQuitMin * 60)
                Finish("czas gry stoi " + (int)(still / 60) + " min mimo prob odblokowania (" + Where() + ")", false, t);
        }

        /// <summary>
        /// [AT2] Koniec sesji zdjec (zrobione, przerwane albo odlozone). Z wejscia na mape -> osadzanie w miescie jak
        /// zawsze; z biegu -> bieg dalej: gracz stoi pod brama, EnsureWaiting osadza go znowu (to samo miasto nie trafia
        /// do odrzuconych), doba ze zdjeciami nie liczy sie do srednich, postoj liczony od teraz.
        /// </summary>
        internal static void PhotosOver(double t, bool fromRun, string why)
        {
            if (Now != Stage.Photos) return;
            if (!fromRun)
            {
                Go(Stage.Settle, t, "miasto: " + why + " - osadzam gracza w miescie");
                return;
            }
            _placedTown = null;
            _waitingLogged = false;
            _lastAction = 0;
            _lastMoveAt = t;
            _lastStallDiag = t;
            _stallTicks = Campaign.Current != null ? Campaign.Current.CurrentTickCount : 0;
            _lastTicks = Campaign.Current != null ? CampaignTime.Now.ToMilliseconds : _lastTicks;
            _firstPartial = true;
            _partialNote = "niepelna - zdjecia w trakcie doby";
            Go(Stage.Run, t, "bieg: " + why + " - z powrotem do miasta (" + DayLabel() + ", " + Where() + ")");
        }

        private static string Averages(int d)
        {
            int n = PerDay.Count;
            int k = Math.Min(10, n);
            double last = PerDay.Skip(n - k).Average();
            double first = PerDay.Take(Math.Min(10, n)).Average();
            double all = PerDay.Average();
            return "SREDNIA dob " + (d - k + 1) + "-" + d + ": " + last.ToString("0.0", Inv) + " s/dobe | pierwsze 10: "
                   + first.ToString("0.0", Inv) + " | od startu biegu: " + all.ToString("0.0", Inv) + " (" + n + " pelnych dob)"
                   + (last > first * 1.25 ? " | ZWALNIA wzgledem poczatku o " + ((last / first - 1) * 100).ToString("0", Inv) + "%" : "");
        }

        private static void TryUnstick(double t, double still, bool waiting)
        {
            var c = Campaign.Current;
            if (c == null || AutotestUi.AnyWindowOpen()) return;   // okna (takze menu Esc gry) zamyka Handle
            try
            {
                if (c.ConversationManager != null && c.ConversationManager.IsConversationInProgress)
                {
                    var who = Hero.OneToOneConversationHero;
                    Note("ODBLOKOWANIE: koncze rozmowe" + (who != null ? " z " + who.Name : ""), true);
                    c.ConversationManager.EndConversation();
                    return;
                }
                var st = ActiveState();
                if (!(st is global::TaleWorlds.CampaignSystem.GameState.MapState))
                {
                    var gsm = GameStateManager.Current;
                    bool mapBelow = gsm != null && gsm.LastOrDefault<global::TaleWorlds.CampaignSystem.GameState.MapState>() != null;
                    if (still > 60 && mapBelow && !(st is MissionState) && !(st is CharacterCreationState))
                    {
                        Note("ODBLOKOWANIE: zdejmuje stan gry " + StateName() + " (ekran " + TopScreenName() + ") - jak Esc", true);
                        gsm.PopState(0);
                    }
                    else Note("ODBLOKOWANIE: nie ruszam stanu " + StateName() + " (ekran " + TopScreenName() + ")");
                    return;
                }
                if (AutotestUi.TryCloseBkWindow()) return;
                // [AT1b] stan mapy wstrzymany przez cos, czego autotest nie zna (menu Esc gry jest oknem i zamyka
                // je Handle): czas i zapis stoja; cudzego wstrzymania nie zdejmujemy - tylko slad w logu
                string bl = Blockers();
                if (bl.Length > 0)
                {
                    Note("ODBLOKOWANIE: brak - stan gry wstrzymany przez " + bl + " (czas i zapis stoja); autotest zdejmuje tylko menu Esc gry", true);
                    return;
                }
                var ctx = c.CurrentMenuContext;
                if (ctx != null && ctx.GameMenu != null)
                {
                    var m = ctx.GameMenu;
                    if (m.IsWaitMenu)
                    {
                        bool acted = false;
                        if (!m.IsWaitActive) { m.StartWait(); acted = true; Note("ODBLOKOWANIE: menu oczekiwania " + m.StringId + " stalo - StartWait", true); }
                        ForceRun(c);
                        if (still > 60 && m.StringId != "town_wait_menus") acted = ClickLeave(ctx) || acted;
                        // [AT1b] bieg 07.10: ta galaz nic nie robila i nic nie pisala - 10 min postoju bez sladu prob
                        if (!acted) Note("ODBLOKOWANIE: brak - menu oczekiwania " + m.StringId + " czeka, czas " + c.TimeControlMode + " - w menu nie ma czego klikac");
                        return;
                    }
                    if (m.StringId == "town") { Note("ODBLOKOWANIE: menu town - EnsureWaiting kliknie town_wait"); return; }
                    if (!ClickLeave(ctx)) Note("ODBLOKOWANIE: menu " + m.StringId + " bez wyjscia - opcje: " + ListOptions(ctx));
                    return;
                }
                if (!waiting) Note("ODBLOKOWANIE: mapa bez menu - osadzam w miescie przy nastepnym ticku");
            }
            catch (Exception e) { Fail("TryUnstick", e); }
        }

        // ------------------------------------------------------------------ 5. zapis i wyjscie

        private static void StartSave(string name, bool final, double t)
        {
            var c = Campaign.Current;
            if (c == null || c.SaveHandler == null) { Note("ZAPIS \"" + name + "\" niemozliwy - brak kampanii", true); _saveDone = true; return; }
            _saveName = name;
            _saveAt = t;
            _saveDone = false;
            _saveWaitNoted = false;
            _saveFinal = final;
            if (final && !c.TimeControlModeLock) c.TimeControlMode = CampaignTimeControlMode.Stop;
            Note("ZAPIS start: \"" + name + "\" (" + DayLabel() + ")", true);
            c.SaveHandler.SaveAs(name);
        }

        /// <summary>Koniec biegu: wynik do logu, zapis (jesli zyczony i jest kampania), wyjscie.</summary>
        internal static void Finish(string why, bool ok, double t)
        {
            if (Now >= Stage.Saving) return;
            if (Now == Stage.Photos) AutotestPhotos.Abort("koniec biegu: " + why);   // [AT2] kamera wraca do druzyny przed zapisem
            _finishOk = ok;
            _finishWhy = why;
            string sum = "";
            if (PerDay.Count > 0) sum = " | " + PerDay.Count + " pelnych dob, srednio " + PerDay.Average().ToString("0.0", Inv) + " s/dobe";
            double runMin = _runStartAt > 0 ? (t - _runStartAt) / 60.0 : 0;
            Note((ok ? "KONIEC OK: " : "KONIEC BLAD: ") + why + " | czas od startu gry " + (t / 60.0).ToString("0.0", Inv)
                 + " min, biegu " + runMin.ToString("0.0", Inv) + " min" + sum + " | okna zamkniete: " + _windows + " | bledy: " + _errors
                 + AutotestPhotos.Summary(), true);
            if (SaveAtEnd && Campaign.Current != null && Campaign.Current.SaveHandler != null
                && ActiveState() is global::TaleWorlds.CampaignSystem.GameState.MapState)
            {
                string name = SavePrefix + DateTime.Now.ToString("yyyy-MM-dd-HHmm", Inv) + (ok ? "" : "-blad");
                StartSave(name, true, t);
                Now = Stage.Saving;
                _stageSince = t;
                return;
            }
            BeginQuit(t);
        }

        private static void StepSaving(double t)
        {
            // [AT1b] SaveAs tylko kolejkuje; zapis robi SaveTick z MapState.OnTick - przy wstrzymanym stanie
            // (bieg 07.10: menu Esc) nie rusza wcale. Jedna linia ze stanem, zanim minie 5 min.
            if (!_saveDone && !_saveWaitNoted && t - _saveAt > 60)
            {
                _saveWaitNoted = true;
                Note("ZAPIS czeka od 60 s (zapis rusza tylko z tiku mapy): " + Where(), true);
            }
            if (_saveDone || t - _saveAt > 300)
            {
                if (!_saveDone) Note("ZAPIS: brak potwierdzenia po 5 min - koncze mimo to", true);
                BeginQuit(t);
            }
        }

        private static void BeginQuit(double t)
        {
            if (!QuitAtEnd)
            {
                try { if (Campaign.Current != null && !Campaign.Current.TimeControlModeLock) Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop; } catch { }
                Note("KONIEC: gra zostaje otwarta (quit=false), czas zatrzymany", true);
                Now = Stage.Done;
                return;
            }
            Note("WYJSCIE: koniec gry -> menu glowne -> zamkniecie", true);
            Now = Stage.Quitting;
            _quitAt = t;
            try { if (Game.Current != null) MBGameManager.EndGame(); }
            catch (Exception e) { Fail("EndGame", e); }
        }

        private static void StepQuit(double t)
        {
            bool atMenu = ActiveState() is InitialState && Game.Current == null;
            if (atMenu || t - _quitAt > 120)
            {
                Note("WYJSCIE: QuitGame (" + (atMenu ? "z menu glownego" : "po 120 s bez menu glownego, stan " + StateName()) + ")", true);
                Now = Stage.Done;
                _quitCalled = true;
                TaleWorlds.Engine.Utilities.QuitGame();
            }
        }

        /// <summary>
        /// [AT1b] Dopisek do linii ZAWIESZENIE po Utilities.QuitGame: proces nie wyszedl, a czesc zarzadzana juz
        /// sie zamknela - to silnik przy sprzataniu (bieg 07.10: 0xC0000005 w TaleWorlds.Native po "Managed
        /// Interface deleted", okno bledu silnika czeka na klikniecie; ten sam adres w grze Jeffa 05.10).
        /// </summary>
        internal static string QuitHint()
        {
            if (!_quitCalled) return "";
            int pid = -1;
            try { pid = Process.GetCurrentProcess().Id; } catch { }
            return " (po QuitGame: proces gry nie wyszedl - silnik konczy sie albo wywrocil przy zamykaniu, zob. C:\\ProgramData\\Mount and Blade II Bannerlord\\logs\\rgl_log_errors_" + pid + ".txt)";
        }

        // ------------------------------------------------------------------ opisy

        private static string DayLabel()
        {
            try { return Campaign.Current != null ? "doba " + DayNow().ToString("0.00", Inv) : "bez kampanii"; } catch { return "?"; }
        }

        private static string TopScreenName()
        {
            try { var s = ScreenManager.TopScreen; return s != null ? s.GetType().Name : "-"; } catch { return "?"; }
        }

        private static string ShortWhere(Campaign c)
        {
            try
            {
                var s = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
                var m = c.CurrentMenuContext != null ? c.CurrentMenuContext.GameMenu : null;
                return (s != null ? s.Name.ToString() : "poza osada") + " | " + (m != null ? m.StringId : "mapa") + " | x" + c.SpeedUpMultiplier.ToString("0.##", Inv);
            }
            catch { return "?"; }
        }

        internal static string Where()
        {
            var sb = new StringBuilder();
            sb.Append("stan ").Append(StateName()).Append(" | ekran ").Append(TopScreenName());
            try
            {
                var c = Campaign.Current;
                if (c != null)
                {
                    var m = c.CurrentMenuContext != null ? c.CurrentMenuContext.GameMenu : null;
                    sb.Append(" | menu ").Append(m != null ? m.StringId : "-");
                    if (m != null && m.IsWaitMenu) sb.Append(m.IsWaitActive ? " (czeka)" : " (oczekiwanie stoi)");
                    var s = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
                    sb.Append(" | osada ").Append(s != null ? s.Name.ToString() : "-");
                    sb.Append(" | czas ").Append(c.TimeControlMode).Append(" x").Append(c.SpeedUpMultiplier.ToString("0.##", Inv));
                    if (c.TimeControlModeLock) sb.Append(" (zablokowany)");
                    if (PlayerEncounter.Current != null) sb.Append(" | spotkanie");
                    if (c.ConversationManager != null && c.ConversationManager.IsConversationInProgress) sb.Append(" | rozmowa");
                    if (Hero.MainHero != null && Hero.MainHero.IsPrisoner) sb.Append(" | gracz w niewoli");
                    if (c.SaveHandler != null && c.SaveHandler.IsSaving) sb.Append(" | zapis w toku");
                }
            }
            catch { }
            try { if (Mission.Current != null) sb.Append(" | misja"); } catch { }
            try { var w = AutotestUi.OpenWindowName(); if (w != null) sb.Append(" | okno ").Append(w); } catch { }
            // [AT1b] co trzyma czas poza menu i trybem czasu: wstrzymanie stanu gry i fokus okna gry
            try { var bl = Blockers(); if (bl.Length > 0) sb.Append(" | STAN WSTRZYMANY przez: ").Append(bl); } catch { }
            try { if (AutotestUi.WindowFocused() == false) sb.Append(" | okno gry bez fokusu"); } catch { }
            return sb.ToString();
        }

        private static FieldInfo _disableRequests;

        /// <summary>
        /// [AT1b] Kto wstrzymal stan gry na wierzchu (GameStateManager.ActiveStateDisabledByUser): nazwy typow
        /// obiektow z listy zadan wstrzymania; "" gdy nikt. Wstrzymany stan dostaje OnIdleTick zamiast OnTick -
        /// dla MapState znaczy to: czas kampanii i zapis (SaveTick) stoja. Bieg 07.10 05:09: MapScreen (menu Esc
        /// otwarte przez gre po utracie fokusu okna).
        /// </summary>
        internal static string Blockers()
        {
            try
            {
                var gsm = GameStateManager.Current;
                if (gsm == null || !gsm.ActiveStateDisabledByUser) return "";
                if (_disableRequests == null)
                    _disableRequests = typeof(GameStateManager).GetField("_activeStateDisableRequests", BindingFlags.Instance | BindingFlags.NonPublic);
                var list = _disableRequests != null ? _disableRequests.GetValue(gsm) as System.Collections.IEnumerable : null;
                var names = new List<string>();
                if (list != null)
                    foreach (var o in list)
                    {
                        var wr = o as WeakReference;
                        object target = wr != null ? wr.Target : null;
                        if (target != null) names.Add(target.GetType().Name);
                    }
                return names.Count > 0 ? string.Join(", ", names.ToArray()) : "(zadanie bez zywego obiektu)";
            }
            catch { return "?"; }
        }
    }

    /// <summary>Zachowanie kampanii dodawane TYLKO w autotescie: koniec zapisu i znaczniki etapow.</summary>
    internal sealed class AutotestBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, Autotest.OnSaveOver);
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCreationOver);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnCreationOver()
        {
            Autotest.Note("ETAP kreator: gra zglosila koniec tworzenia postaci (" + (Hero.MainHero != null ? Hero.MainHero.Name + ", kultura " + (Hero.MainHero.Culture != null ? Hero.MainHero.Culture.StringId : "?") : "?") + ")", true);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Autotest.Note("ETAP kampania: sesja wystartowala", true);
        }
    }
}
