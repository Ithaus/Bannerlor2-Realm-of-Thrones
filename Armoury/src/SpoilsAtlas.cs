using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// paczka 150 (Jeff 25.10 ok. 17:00: "dopasuj obrazki w oknach lupu do miejsca") - krok 1: PROBA OBRAZKOW OKNA LUPU, tylko autotest: DLL autotestu CrashScribe
    /// (LogGate.Mode 1/2) i plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-zdjecia-obrazkow.flag (FlagName; plik tylko czytany). W zwyklej grze
    /// jedno sprawdzenie trybu na sesje (plik - tylko w autotescie), potem od razu wyjscie.
    /// KIEDY: raz na proces, w tych samych warunkach spokoju co proba zdjec okien 132 (WindowPhotos.Calm: MapState i ekran mapy, bez misji, menu Esc, bitwy,
    /// oblezenia, spotkania poza osada, rozmowy, zapytania, okna lupu i opowiesci, wspolna bramka Prologue.SceneGate, etap autotestu "Run"; 30 s po wejsciu na mape,
    /// 3 s spokoju, najdluzej 60 min czekania). Proby 132 i 150 nie ida naraz: 150 czeka, dopoki WindowPhotos.Active, a 132 nie startuje, dopoki Running
    /// (WindowPhotos.Calm(true)).
    /// CO: lista sprite'ow kategorii ui_event_category z SpriteData W CZASIE GRY (SpriteGeneric, SpritePart.Category.Name; bez event-bg - tlo okna - i testevent),
    /// po nazwie bez wielkosci liter. Dla kazdego okno lupu PROBNE: raport kroku 5 proby 132 (WindowPhotos.TestRep - budowany raz na probe; bez SpoilsReport.Finish,
    /// liczniki sesji i stan gry bez zmian) przez SpoilsWindow.ShowPhoto z WYMUSZONYM obrazkiem (ten sprite zamiast Picture) i tytulem "Sprite nn/N: nazwa (W x H)"
    /// - na zdjeciu widac, ktory to. Po 1.5 s (obce zapytanie nad oknem - czeka, najwyzej 8 s) TaleWorlds.Engine.Utilities.TakeScreenshot do
    /// CrashScribe\zdjecia\obrazek-150-nn-nazwa.png (silnik pisze BMP mimo rozszerzenia), zamkniecie 1 s po zdjeciu (SpoilsWindow.ClosePhoto), nastepny po 0.3 s.
    /// Linie: "Obrazki lupu (150): ..." - start (lista), po jednej na sprite ("TAK plik ..." / "NIE (powod)", z rozmiarem i proporcjami wobec miejsca 640x360 w prefabie)
    /// i koniec (pliki zapisane po zdjeciu - sprawdzone 6 s po ostatnim; plik starszy niz zdjecie = stary z poprzedniej proby, liczony jako brak).
    /// Przerwanie (misja, bitwa gracza, etap autotestu inny niz Run, inna kampania, 30 potkniec): okno zamkniete, linia "przerwana". Potkniecia liczone, zly obrazek -
    /// linia NIE i nastepny, bez wylacznika.
    /// </summary>
    internal static class SpoilsAtlas
    {
        internal const string FlagName = "armoury-zdjecia-obrazkow.flag";
        private const string P = "Obrazki lupu (150): ";
        private const string Category = "ui_event_category";
        private static readonly string[] Skip = { "event-bg", "testevent" };   // tlo okna (BackgroundImage) i obrazek probny ROT
        private const double StartAfterSec = 30.0, CalmSec = 3.0, GiveUpSec = 3600.0;
        private const double ShotSec = 1.5, ShotMaxSec = 8.0, CloseAfterShotSec = 1.0;
        private const double GapSec = 0.3, StepWaitSec = 20.0, FileWaitSec = 6.0;
        private const int MaxStumbles = 30;
        private const int SlotW = 640, SlotH = 360;   // miejsce na obrazek w prefabie ArmSpoilsWindow.xml (BrushWidget Sprite="@HeaderImage", 640 x 360)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Pic
        {
            public string Name;
            public int W, H;
            public string File;        // sciezka zdjecia (po TakeScreenshot) albo null
            public DateTime ShotUtc;   // chwila zdjecia - plik starszy to stary plik z poprzedniej proby
        }

        private static bool _checked, _done, _running;
        private static Campaign _camp;
        private static long _seenAt, _calmAt, _lastCheck, _beganAt, _endAt;
        private static int _i, _phase;             // _i - indeks obrazka; faza 0 czeka i otwiera, 1 otwarte - zdjecie, 2 po zdjeciu - zamkniecie
        private static long _stepAt, _openedAt, _shotAt;
        private static bool _shotOpen;
        private static string _shotErr;
        private static int _ok, _bad, _stumbles;
        private static readonly List<Pic> _list = new List<Pic>();
        private static SpoilsReport.Rep _rep;     // raport probny (WindowPhotos.TestRep krok 5) - jeden na cala probe, tylko do odczytu (SpoilsWindow.Build)

        /// <summary>Proba w toku (od startu do ostatniego zamkniecia) - proba zdjec okien 132 w tym czasie nie startuje (WindowPhotos.Calm(true)).</summary>
        internal static bool Running { get { return _running; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) { try { Log.Error("SpoilsAtlas." + where + " (potkniecie " + _stumbles + ")", e); } catch { } }
        }

        private static double Sec(long since, long now) { return since == 0 ? 0 : (now - since) / (double)Stopwatch.Frequency; }

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
                Log.Info(P + "plik " + FlagName + " jest - proba obrazkow kategorii " + Category + " w oknie lupu po " + StartAfterSec.ToString("0", Inv) + " s kampanii i "
                         + CalmSec.ToString("0", Inv) + " s spokoju na mapie, po probie zdjec okien 132, jesli trwa (etap autotestu teraz: " + (WindowPhotos.Stage() ?? "nieznany") + ").");
                return;
            }
            if (_running)
            {
                if (!ReferenceEquals(c, _camp)) { Abort(c == null ? "wyjscie z kampanii" : "inna kampania"); return; }
                StepTick(now);
                return;
            }
            if (_endAt != 0) { EndTick(now); return; }
            if (!ReferenceEquals(c, _camp)) { _camp = c; _seenAt = now; _calmAt = 0; return; }   // inna kampania przed proba - liczenie od nowa
            WaitStart(now);
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
            string why = WindowPhotos.Active ? "proba zdjec okien (132) w toku" : WindowPhotos.Calm(true);
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
            bool loaded;
            string why = BuildList(out loaded);
            if (why == null && _list.Count == 0) why = "kategoria bez obrazkow (poza " + string.Join(", ", Skip) + ")";
            string note = null;
            if (why == null)
            {
                try { _rep = WindowPhotos.TestRep(5, out note); }
                catch (Exception e) { Stumble("TestRep", e); _rep = null; }
                if (_rep == null) why = "brak raportu probnego (WindowPhotos.TestRep krok 5)";
            }
            if (why != null)
            {
                _done = true;
                Log.Info(P + "bez proby - " + why + ".");
                return;
            }
            _running = true;
            _beganAt = now;
            _i = 0; _phase = 0; _stepAt = now - (long)(GapSec * Stopwatch.Frequency);
            int off = 0;
            var names = new StringBuilder();
            foreach (var p in _list)
            {
                if (Math.Abs(AspectDiff(p)) > 5.0) off++;
                if (names.Length > 0) names.Append(", ");
                names.Append(p.Name);
            }
            Log.Info(P + "start | obrazkow " + _list.Count + " (pominiete: " + string.Join(", ", Skip) + "), kategoria zaladowana przed proba " + (loaded ? "TAK" : "NIE (laduje ShowPhoto)")
                     + " | miejsce na obrazek " + SlotW + "x" + SlotH + " (" + ((double)SlotW / SlotH).ToString("0.00", Inv) + "), proporcje inne niz miejsce o ponad 5%: " + off
                     + " | " + (note ?? "raport probny") + " | zdjecie po " + ShotSec.ToString("0.0", Inv) + " s | lista: " + names + ".");
        }

        /// <summary>Lista sprite'ow kategorii z SpriteData w czasie gry (bez Skip), po nazwie bez wielkosci liter. null - lista gotowa (moze byc pusta); inaczej powod.</summary>
        private static string BuildList(out bool loaded)
        {
            loaded = false;
            _list.Clear();
            var data = UIResourceManager.SpriteData;
            if (data == null || data.Sprites == null || data.SpriteCategories == null) return "brak SpriteData";
            TaleWorlds.TwoDimension.SpriteCategory cat;
            if (!data.SpriteCategories.TryGetValue(Category, out cat) || cat == null) return "brak kategorii sprite'ow " + Category + " (ROT-Content)";
            loaded = cat.IsLoaded;
            foreach (var kv in data.Sprites)
            {
                var g = kv.Value as TaleWorlds.TwoDimension.SpriteGeneric;
                var part = g != null ? g.SpritePart : null;
                if (part == null || part.Category == null || part.Category.Name != Category) continue;
                bool skip = false;
                foreach (var s in Skip) if (string.Equals(s, kv.Key, StringComparison.OrdinalIgnoreCase)) skip = true;
                if (skip) continue;
                _list.Add(new Pic { Name = kv.Key, W = part.Width, H = part.Height });
            }
            _list.Sort(delegate (Pic a, Pic b) { return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name); });
            return null;
        }

        // ------------------------------------------------------------ kroki

        private static void StepTick(long now)
        {
            string stop = WindowPhotos.StopWhy();
            if (stop != null) { Abort(stop); return; }
            var pic = _list[_i];
            if (_phase == 0)
            {
                if (Sec(_stepAt, now) < GapSec) return;
                string why = WindowPhotos.Calm(false);
                if (why != null)
                {
                    if (Sec(_stepAt, now) > StepWaitSec) { Result(pic, false, "okno nie otwarte - " + StepWaitSec.ToString("0", Inv) + " s bez spokoju (" + why + ")"); Next(now); }
                    return;
                }
                Open(pic, now);
                return;
            }
            if (_phase == 1)
            {
                double s = Sec(_openedAt, now);
                if (!SpoilsWindow.PhotoOpen) { Result(pic, false, "okno zamkniete przed zdjeciem, po " + s.ToString("0.00", Inv) + " s"); Next(now); return; }
                if (s < ShotSec) return;
                if (InformationManager.IsAnyInquiryActive() && s < ShotMaxSec) return;   // obce zapytanie nad oknem lupu - autotest je zamknie
                Shot(pic, now);
                return;
            }
            // faza 2: po zdjeciu - zamkniecie 1 s po zdjeciu (zrzut silnika bierze klatke z oknem)
            if (SpoilsWindow.PhotoOpen && Sec(_shotAt, now) < CloseAfterShotSec) return;
            string how = CloseMine();
            Result(pic, pic.File != null && _shotErr == null, how);
            Next(now);
        }

        private static void Open(Pic pic, long now)
        {
            string why;
            try
            {
                string title = "Sprite " + (_i + 1).ToString("00", Inv) + "/" + _list.Count.ToString("00", Inv) + ": " + pic.Name + " (" + pic.W + " x " + pic.H + ")";
                why = SpoilsWindow.ShowPhoto(_rep, pic.Name, title);
            }
            catch (Exception e) { Stumble("Open." + pic.Name, e); why = "wyjatek " + e.GetType().Name; }
            if (why != null)
            {
                try { SpoilsWindow.ClosePhoto(); } catch (Exception e) { Stumble("Open.Close." + pic.Name, e); }   // okno moglo wejsc przed wyjatkiem
                Result(pic, false, why);
                Next(now);
                return;
            }
            _openedAt = now;
            _phase = 1;
        }

        private static void Shot(Pic pic, long now)
        {
            _shotErr = null; pic.File = null;
            _shotOpen = SpoilsWindow.PhotoOpen;
            try
            {
                string dir = Path.Combine(CampWalk.CrashScribeDir(), "zdjecia");
                Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, "obrazek-150-" + (_i + 1).ToString("00", Inv) + "-" + Safe(pic.Name) + ".png");
                pic.ShotUtc = DateTime.UtcNow;
                TaleWorlds.Engine.Utilities.TakeScreenshot(f.Replace('\\', '/'));
                pic.File = f;
            }
            catch (Exception e) { Stumble("Shot." + pic.Name, e); _shotErr = e.GetType().Name; }
            _shotAt = now;
            _phase = 2;
        }

        private static string Safe(string name)
        {
            var sb = new StringBuilder(name.Length);
            var bad = Path.GetInvalidFileNameChars();
            foreach (char ch in name) sb.Append(Array.IndexOf(bad, ch) >= 0 || ch == ' ' ? '_' : ch);
            return sb.ToString();
        }

        private static string CloseMine()
        {
            try
            {
                if (!SpoilsWindow.PhotoOpen) return "okno zamkniete wczesniej (Done, Esc, Enter, zmiana ekranu albo bezpiecznik 12 s)";
                SpoilsWindow.ClosePhoto();
                return "okno zamkniete przez probe";
            }
            catch (Exception e) { Stumble("Close." + _i, e); return "blad zamkniecia " + e.GetType().Name; }
        }

        /// <summary>Proporcje obrazka wobec miejsca 640x360 w procentach (+ szerszy, - wezszy/wyzszy).</summary>
        private static double AspectDiff(Pic p)
        {
            if (p.W <= 0 || p.H <= 0) return 0;
            return ((double)p.W / p.H / ((double)SlotW / SlotH) - 1.0) * 100.0;
        }

        private static void Result(Pic pic, bool ok, string how)
        {
            if (ok) _ok++; else _bad++;
            try
            {
                double d = AspectDiff(pic);
                string head = P + (_i + 1).ToString("00", Inv) + " " + pic.Name + " " + pic.W + "x" + pic.H + " (proporcje "
                              + (pic.H > 0 ? ((double)pic.W / pic.H).ToString("0.00", Inv) : "-") + ", wobec miejsca " + (d >= 0 ? "+" : "") + d.ToString("0", Inv) + "%)";
                if (ok)
                {
                    long len = FileLen(pic.File);
                    Log.Info(head + " - TAK plik zdjecia\\" + Path.GetFileName(pic.File) + " | zdjecie po " + Sec(_openedAt, _shotAt).ToString("0.00", Inv) + " s, okno na ekranie "
                             + (_shotOpen ? "TAK" : "NIE") + " | " + how + " | plik " + (len >= 0 ? "jest (" + (len / 1024).ToString(Inv) + " KB)" : "jeszcze nie zapisany - sprawdzi linia koncowa") + ".");
                }
                else Log.Info(head + " - NIE (" + (_shotErr != null ? "zrzut silnika: wyjatek " + _shotErr + "; " : "") + how + ").");
            }
            catch (Exception e) { Stumble("Result", e); }
        }

        private static void Next(long now)
        {
            _i++; _phase = 0; _stepAt = now;
            _shotOpen = false; _shotErr = null;
            if (_i >= _list.Count) { _running = false; _endAt = now; }
        }

        private static void EndTick(long now)
        {
            if (Sec(_endAt, now) < FileWaitSec) return;
            _done = true;
            int have = 0, shots = 0;
            var miss = new List<string>();
            foreach (var p in _list)
            {
                if (p.File == null) continue;
                shots++;
                string st = FileState(p);
                if (st == null) have++; else miss.Add(Path.GetFileName(p.File) + " (" + st + ")");
            }
            Log.Info(P + "koniec | obrazki TAK " + _ok + "/" + _list.Count + ", NIE " + _bad + " | pliki zapisane po zdjeciu " + have + "/" + shots
                     + " (Documents\\Mount and Blade II Bannerlord\\CrashScribe\\zdjecia)" + (miss.Count > 0 ? " - BRAK: " + string.Join(", ", miss.ToArray()) : "")
                     + " | potkniecia " + _stumbles + ", okna lupu (SpoilsWindow) " + SpoilsWindow.Stumbles + " | czas proby " + Sec(_beganAt, _endAt).ToString("0.0", Inv)
                     + " s - wynik " + (_ok == _list.Count && have == shots ? "TAK" : "NIE") + ".");
            _rep = null;
        }

        /// <summary>null - plik zdjecia jest, niepusty i zapisany po zdjeciu; inaczej powod.</summary>
        private static string FileState(Pic p)
        {
            try
            {
                if (!File.Exists(p.File)) return "brak pliku";
                var fi = new FileInfo(p.File);
                if (fi.Length <= 0) return "pusty";
                if (fi.LastWriteTimeUtc < p.ShotUtc.AddSeconds(-2)) return "stary plik sprzed zdjecia - silnik nie nadpisal";
                return null;
            }
            catch (Exception e) { return "blad odczytu " + e.GetType().Name; }
        }

        private static long FileLen(string f)
        {
            try { return f != null && File.Exists(f) ? new FileInfo(f).Length : -1; } catch { return -1; }
        }

        private static void Abort(string why)
        {
            if (_done) return;
            _done = true;
            bool was = _running;
            try { if (was) SpoilsWindow.ClosePhoto(); }
            catch (Exception e) { Stumble("Abort.Close", e); }
            _running = false;
            _rep = null;
            try
            {
                Log.Info(P + "przerwana (" + why + ")" + (was && _i < _list.Count ? " przy obrazku " + (_i + 1).ToString("00", Inv) + " " + _list[_i].Name : "")
                         + " | obrazki TAK " + _ok + ", NIE " + _bad + " z " + _list.Count + " | potkniecia " + _stumbles + ".");
            }
            catch { }
        }
    }
}
