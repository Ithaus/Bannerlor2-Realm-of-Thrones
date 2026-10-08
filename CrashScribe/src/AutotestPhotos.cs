using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using MapState = TaleWorlds.CampaignSystem.GameState.MapState;

namespace CrashScribe
{
    /// <summary>
    /// [AT2] TRYB ZDJEC autotestu (Jeff 08.10: "ikona wioski wyglada tak" - zrzut z gry pokazal szara brylke zamiast
    /// domow; zeby nie czekac na kolejny zrzut Jeffa, autotest sam fotografuje wioski z bliska).
    ///
    /// Wlacznik: klucz "photos" w autotest.json - lista celow, kazdy cel to obiekt
    ///   {"name":"stony-holt","x":426.51,"y":865.59}                         polozenie na mapie (jedn. mapy, jak posX/posY)
    ///   {"name":"tumbledown","settlement":"village_B2_1","dx":0,"dy":-6}    osada gry + przesuniecie
    ///   (opcjonalnie "bearing": obrot kamery w stopniach, 0 = jak domyslnie w grze)
    /// "photo_shots":"8,17,40" - wysokosci kamery nad celem (kazda = jedno ujecie, plik <cel>-z08.png ...);
    /// "photo_hours":"9-15" - jasna pora dnia (godziny gry).
    /// Bez klucza "photos" (albo bez poprawnego celu) nic z tego pliku nie rusza.
    ///
    /// Przebieg: zaraz po wejsciu na mape (gracz jeszcze poza miastem, czas STOP) - jesli jasno, kamera mapy na kazdy
    /// cel (tryb MoveToPosition jak "idz kamera do punktu" gry, bez ruszania druzyny), odleglosc i kat jak przy zoomie
    /// gracza (wzor gry: kat = odl * 0.0075 + 0.35 rad), kilka sekund i klatek na wczytanie (i bez kompilacji shaderow
    /// w toku, jak tryb zdjec gry w misji), zrzut silnika TaleWorlds.Engine.Utilities.TakeScreenshot (ta sama metoda, co
    /// katalog przedmiotow i testy obrazu gry; format z Modules\Native\engine_module.ini screenshot_format = 1 = Png) do
    /// Documents\...\CrashScribe\zdjecia\<run>\<cel>-<ujecie>.png; potem kamera wraca do druzyny i zwykly bieg.
    /// Zrzuty Jeffa w D:\SteamGames\...\bin\Win64_Shipping_Client\*.png to ReShade (ReShade.ini SavePath, klawisz PrtSc) -
    /// tam silnik nic nie pisze i tamtego katalogu nie ruszamy.
    /// Ciemno albo gracz w menu: najpierw miasto i bieg; pierwsza jasna pora w miescie -> "Stop waiting" + "Leave"
    /// (jak gracz), zdjecia spod bramy, potem autotest osadza gracza znowu.
    /// Silnik nie zapisal pliku: druga proba jako .bmp, trzecia - zrzut okna gry (PrintWindow); BMP zamieniany na PNG.
    /// Kazdy krok jedna linia w autotest-*.log ("ZDJECIA ...", "ZDJECIE ...").
    /// </summary>
    internal static class AutotestPhotos
    {
        internal sealed class Target
        {
            public string Name = "";
            public bool HasXY;
            public float X, Y;
            public string Settlement = "";
            public float Dx, Dy;
            public float BearingDeg;
        }

        internal sealed class Shot
        {
            public string Name = "";
            public float Height;     // zyczona wysokosc kamery nad celem (jedn. mapy)
            public float Distance;   // odleglosc kamery mapy (CameraDistance), z ktorej ta wysokosc wychodzi wedlug wzoru gry
        }

        internal static readonly List<Target> Targets = new List<Target>();
        internal static readonly List<Shot> Shots = new List<Shot>();
        internal static int HourFrom = 9, HourTo = 15;
        private static int _badTargets;
        private static bool _keyPresent;

        private const int MaxTargets = 40;
        private const int MaxShots = 6;
        private const double NewTargetWaitS = 6.0;   // nowy cel: teren, tekstury, wioski Armoury (tworzone w komorkach przy kamerze)
        private const double ShotWaitS = 2.5;        // ten sam cel, inna wysokosc
        private const int NewTargetFrames = 30, ShotFrames = 15;
        private const double FileWaitS = 8.0;        // na plik od silnika
        private const double ShaderWaitS = 30.0;     // dodatkowo, gdy silnik kompiluje shadery
        private const double PrepareLimitS = 300.0;  // mapa gotowa do zdjec (okna po kreatorze itp.)
        private const double LeaveLimitS = 60.0;     // wyjscie z miasta
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private enum Ph { Idle, Leave, Prepare, Aim, Wait, Await }
        private static Ph _ph = Ph.Idle;
        private static bool _fromRun, _deferred, _done;
        private static double _phAt, _sessionAt, _actAt, _limitS;
        private static long _phFrames;
        private static int _ti, _si, _attempt;     // cel, ujecie, proba (0 silnik .png, 1 silnik .bmp, 2 okno gry)
        private static double _needS;
        private static int _needFrames;
        private static object _view;
        private static bool _saved;
        private static object _svDist, _svTarget, _svAddElev, _svBearing;
        private static string _dir = "";
        private static string _file = "", _camNote = "";
        private static long _lastLen = -1;
        private static double _lastLenAt;
        private static int _ok, _missing, _shotFails, _converted, _windowShots, _engineOk, _engineFails;
        private static int _shaders;                // kompilacje shaderow w toku przy zdjeciu (-1 = nie wiadomo)
        private static Vec3 _aim;
        private static float _aimDist, _aimBearing, _aimGround;
        private static bool _camWarned;

        // ------------------------------------------------------------------ przelacznik

        internal static void Parse(string json)
        {
            Targets.Clear();
            Shots.Clear();
            _badTargets = 0;
            _keyPresent = false;
            HourFrom = 9;
            HourTo = 15;
            if (string.IsNullOrEmpty(json)) return;
            var m = Regex.Match(json, "\"photos\"\\s*:\\s*\\[(?<a>[^\\]]*)\\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success) return;
            _keyPresent = true;
            foreach (Match o in Regex.Matches(m.Groups["a"].Value, "\\{(?<o>[^{}]*)\\}"))
            {
                string body = "{" + o.Groups["o"].Value + "}";
                var tg = new Target();
                string xs = Autotest.Raw(body, "x"), ys = Autotest.Raw(body, "y");
                tg.HasXY = xs != null && ys != null
                           && float.TryParse(xs, NumberStyles.Float, Inv, out tg.X) && float.TryParse(ys, NumberStyles.Float, Inv, out tg.Y)
                           && Ok(tg.X) && Ok(tg.Y);
                tg.Settlement = Autotest.Str(body, "settlement", "");
                tg.Dx = Num(body, "dx");
                tg.Dy = Num(body, "dy");
                tg.BearingDeg = Num(body, "bearing");
                tg.Name = FileSafe(Autotest.Str(body, "name", ""));
                if (!tg.HasXY && tg.Settlement.Length == 0) { _badTargets++; continue; }
                if (tg.Name.Length == 0) tg.Name = tg.Settlement.Length > 0 ? FileSafe(tg.Settlement) : "cel" + (Targets.Count + 1);
                if (Targets.Any(z => string.Equals(z.Name, tg.Name, StringComparison.OrdinalIgnoreCase))) tg.Name += "-" + (Targets.Count + 1);
                if (Targets.Count >= MaxTargets) { _badTargets++; continue; }
                Targets.Add(tg);
            }

            var hs = new List<float>();
            string sv = Autotest.Raw(json, "photo_shots");
            if (!string.IsNullOrEmpty(sv))
                foreach (var p in sv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    float h;
                    if (float.TryParse(p.Trim(), NumberStyles.Float, Inv, out h) && h >= 2f && h <= 150f) hs.Add(h);
                }
            if (hs.Count == 0) hs.AddRange(new[] { 8f, 17f, 40f });
            foreach (var h in hs)
            {
                if (Shots.Count >= MaxShots) break;
                string name = "z" + ((int)Math.Round(h)).ToString("00", Inv);
                if (Shots.Any(s => s.Name == name)) continue;
                Shots.Add(new Shot { Name = name, Height = h, Distance = DistanceForHeight(h) });
            }

            var hm = Regex.Match(Autotest.Raw(json, "photo_hours") ?? "", "^\\s*(\\d{1,2})\\s*-\\s*(\\d{1,2})\\s*$");
            if (hm.Success)
            {
                int a = int.Parse(hm.Groups[1].Value, Inv), b = int.Parse(hm.Groups[2].Value, Inv);
                if (a >= 0 && b <= 24 && a < b) { HourFrom = a; HourTo = b; }
            }
        }

        private static bool Ok(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < 100000f; }

        private static float Num(string body, string key)
        {
            float v;
            var r = Autotest.Raw(body, key);
            return r != null && float.TryParse(r, NumberStyles.Float, Inv, out v) && Ok(v) ? v : 0f;
        }

        private static string FileSafe(string s)
        {
            return Regex.Replace(s ?? "", "[^A-Za-z0-9_.-]", "_").Trim('.');
        }

        /// <summary>
        /// Odleglosc kamery mapy, przy ktorej kamera stoi h nad celem: cel kamery = teren + 1, kamera w odleglosci
        /// (d + 2) pod katem e = d * 0.0075 + 0.35 rad (MapCameraView.ComputeMapCamera / CalculateCameraElevation).
        /// </summary>
        internal static float DistanceForHeight(float h)
        {
            double lo = 2.5, hi = 160.0;
            for (int i = 0; i < 60; i++)
            {
                double mid = (lo + hi) / 2;
                if (HeightAt(mid) < h) lo = mid; else hi = mid;
            }
            return (float)((lo + hi) / 2);
        }

        private static double HeightAt(double d)
        {
            double e = Math.Min(d * 0.0075 + 0.35, Math.PI * 99.0 / 200.0);
            return 1.0 + (d + 2.0) * Math.Sin(e);
        }

        internal static string Describe()
        {
            if (!_keyPresent) return "";
            if (Targets.Count == 0) return "klucz \"photos\" jest, ale bez poprawnego celu (" + _badTargets + " odrzuconych) - zdjec nie bedzie";
            var sb = new StringBuilder();
            sb.Append(Targets.Count).Append(" cel(e): ");
            sb.Append(string.Join(", ", Targets.Select(tg => tg.Name + " " + (tg.HasXY
                ? "(" + tg.X.ToString("0.##", Inv) + ", " + tg.Y.ToString("0.##", Inv) + ")"
                : "[" + tg.Settlement + (tg.Dx != 0f || tg.Dy != 0f ? " +(" + tg.Dx.ToString("0.##", Inv) + ", " + tg.Dy.ToString("0.##", Inv) + ")" : "") + "]")
                + (tg.BearingDeg != 0f ? " obrot " + tg.BearingDeg.ToString("0", Inv) : "")).ToArray()));
            sb.Append(" | ujecia ").Append(string.Join(" ", Shots.Select(s => s.Name + " (odl. " + s.Distance.ToString("0.0", Inv) + ")").ToArray()));
            sb.Append(" | jasno ").Append(HourFrom).Append("-").Append(HourTo);
            if (_badTargets > 0) sb.Append(" | odrzuconych celow: ").Append(_badTargets);
            sb.Append(" | katalog zdjecia\\").Append(RunDirName());
            return sb.ToString();
        }

        private static string RunDirName()
        {
            string r = FileSafe(Autotest.RunId);
            return r.Length > 0 ? r : "bieg-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm", Inv);
        }

        // ------------------------------------------------------------------ wejscie z Autotest

        internal static bool Pending { get { return Targets.Count > 0 && !_done; } }

        internal static string Plan()
        {
            float hour = HourNow();
            return Targets.Count + " cel(e) x " + Shots.Count + " ujecia, jasno " + HourFrom + "-" + HourTo + ", teraz godz. " + (hour >= 0 ? hour.ToString("0.0", Inv) : "?");
        }

        /// <summary>Zdjecia odlozone przy wejsciu na mape i teraz jasno (wolane z biegu, gdy gracz czeka w miescie).</summary>
        internal static bool WantNow()
        {
            if (!Pending || !_deferred || _ph != Ph.Idle) return false;
            float h = HourNow();
            return h >= HourFrom && h < HourTo;
        }

        private static float HourNow()
        {
            try { return Campaign.Current != null ? CampaignTime.Now.CurrentHourInDay : -1f; } catch { return -1f; }
        }

        internal static void Begin(double t, bool fromRun)
        {
            _fromRun = fromRun;
            _ph = fromRun ? Ph.Leave : Ph.Prepare;
            _phAt = t;
            _sessionAt = t;
            _actAt = 0;
            _phFrames = Autotest.Frames;
            _limitS = 120.0 + Targets.Count * (NewTargetWaitS + ShaderWaitS + Shots.Count * (ShotWaitS + 3 * FileWaitS + 2.0));
        }

        internal static string Summary()
        {
            if (Targets.Count == 0) return "";
            int all = Targets.Count * Shots.Count;
            if (_done) return " | zdjecia " + _ok + "/" + all + " (zdjecia\\" + RunDirName() + ")";
            return " | zdjecia NIE zrobione" + (_deferred ? " (czekaly na jasna pore " + HourFrom + "-" + HourTo + " w miescie)" : "");
        }

        /// <summary>Koniec biegu w trakcie zdjec (limit czasu, wyjscie): kamera z powrotem do druzyny, bez dalszych krokow.</summary>
        internal static void Abort(string why)
        {
            if (_ph == Ph.Idle) return;
            RestoreCamera();
            _ph = Ph.Idle;
            _done = true;
            Autotest.Note("ZDJECIA przerwane (" + why + ") | zdjec " + _ok + "/" + (Targets.Count * Shots.Count), true);
        }

        // ------------------------------------------------------------------ krok

        internal static void Step(double t)
        {
            var c = Campaign.Current;
            if (c == null || Autotest.ActiveState() is TaleWorlds.MountAndBlade.InitialState)
            {
                Autotest.Finish("gra wrocila do menu glownego w trakcie zdjec", false, t);
                return;
            }
            if (!(Autotest.ActiveState() is MapState))
            {
                // wydarzenie ROT itp. na wierzchu - zamyka je AutotestUi.Handle; mapa wroci
                if (t - _phAt > PrepareLimitS) End(t, "przez " + (int)PrepareLimitS + " s na wierzchu nie mapa (" + Autotest.Where() + ")", false);
                return;
            }
            StopTime(c);
            if (_ph >= Ph.Aim && t - _sessionAt > _limitS)
            {
                End(t, "limit czasu sesji zdjec " + (int)_limitS + " s", false);
                return;
            }
            // przygotowanie (wyjscie z miasta + gotowa mapa) razem, bez wzgledu na przejscia Leave <-> Prepare
            if (_ph < Ph.Aim && t - _sessionAt > PrepareLimitS + LeaveLimitS)
            {
                End(t, "przygotowanie do zdjec dluzej niz " + (int)(PrepareLimitS + LeaveLimitS) + " s (" + Autotest.Where() + ")", false);
                return;
            }
            switch (_ph)
            {
                case Ph.Leave: StepLeave(c, t); break;
                case Ph.Prepare: StepPrepare(c, t); break;
                case Ph.Aim: StepAim(t); break;
                case Ph.Wait: StepWait(c, t); break;
                case Ph.Await: StepAwait(t); break;
            }
        }

        private static void Go(Ph p, double t)
        {
            _ph = p;
            _phAt = t;
            _phFrames = Autotest.Frames;
        }

        private static void StopTime(Campaign c)
        {
            if (!c.TimeControlModeLock && c.TimeControlMode != CampaignTimeControlMode.Stop) c.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        private static bool Talking(Campaign c)
        {
            return c.ConversationManager != null && c.ConversationManager.IsConversationInProgress;
        }

        /// <summary>Z biegu: gracz czeka w miescie - "Stop waiting", potem "Leave" (to samo, co klika gracz); czas STOP.</summary>
        private static void StepLeave(Campaign c, double t)
        {
            if (AutotestUi.AnyWindowOpen() || Talking(c)) return;
            var mp = MobileParty.MainParty;
            if (mp == null) return;
            var ctx = c.CurrentMenuContext;
            var menu = ctx != null ? ctx.GameMenu : null;
            if (mp.CurrentSettlement == null && menu == null && PlayerEncounter.Current == null)
            {
                Autotest.Note("ZDJECIA: druzyna poza miastem, czas STOP (" + Autotest.Where() + ")");
                Go(Ph.Prepare, t);
                return;
            }
            if (t - _phAt > LeaveLimitS)
            {
                End(t, "nie da sie wyjsc z miasta w " + (int)LeaveLimitS + " s (" + Autotest.Where() + ")", false);
                return;
            }
            if (t - _actAt < 1.5 || menu == null) return;
            _actAt = t;
            string id = menu.StringId;
            if (id == "town_wait_menus" && Autotest.ClickOption(ctx, "wait_leave")) Autotest.Note("ZDJECIA: klikam \"Stop waiting\" [wait_leave]", true);
            else if (id == "town" && Autotest.ClickOption(ctx, "town_leave")) Autotest.Note("ZDJECIA: klikam \"Leave\" [town_leave] - druzyna wychodzi pod brame", true);
            else if (!Autotest.ClickLeave(ctx)) Autotest.Note("ZDJECIA: menu " + id + " bez wyjscia - czekam");
        }

        /// <summary>Mapa gotowa: bez okien i rozmowy, ekran mapy gotowy, bez animacji kamery; gracz poza menu; jasno.</summary>
        private static void StepPrepare(Campaign c, double t)
        {
            if (t - _phAt > PrepareLimitS)
            {
                End(t, "mapa nie byla gotowa do zdjec przez " + (int)PrepareLimitS + " s (" + Autotest.Where() + ")", false);
                return;
            }
            if (AutotestUi.AnyWindowOpen() || Talking(c)) return;
            if (t - _phAt < 3.0) return;   // okna po kreatorze (BK, ROT) maja czas sie pokazac
            var screen = Screen();
            if (screen == null || !Bool(AutotestUi.Get(screen, "IsReady"))) return;
            var view = AutotestUi.Get(screen, "MapCameraView");
            if (view == null || Bool(AutotestUi.Get(view, "CameraAnimationInProgress"))) return;
            var mp = MobileParty.MainParty;
            if (mp == null) return;
            var ctx = c.CurrentMenuContext;
            bool inMenu = ctx != null && ctx.GameMenu != null;
            if (mp.CurrentSettlement != null || inMenu || PlayerEncounter.Current != null || mp.MapEvent != null)
            {
                if (_fromRun) { Go(Ph.Leave, t); return; }
                Defer(t, "gracz w menu / osadzie / spotkaniu (" + Autotest.Where() + ")");
                return;
            }
            float hour = HourNow();
            if (!_fromRun && !(hour >= HourFrom && hour < HourTo))
            {
                Defer(t, "ciemno: godz. " + hour.ToString("0.0", Inv) + " (jasno " + HourFrom + "-" + HourTo + ")");
                return;
            }

            _view = view;
            SaveCamera();
            _dir = Path.Combine(Autotest.Dir ?? "", "zdjecia", RunDirName());
            try { Directory.CreateDirectory(_dir); }
            catch (Exception e) { End(t, "katalog " + _dir + " nie powstal: " + e.GetType().Name + ": " + e.Message, false); return; }
            _ti = 0;
            _si = 0;
            _ok = _missing = _shotFails = _converted = _windowShots = _engineOk = _engineFails = 0;
            _sessionAt = t;
            Autotest.Note("ZDJECIA start: " + Plan() + " | kamera " + view.GetType().Name + " | " + Autotest.Where() + " | katalog " + _dir, true);
            Go(Ph.Aim, t);
        }

        private static void Defer(double t, string why)
        {
            _deferred = true;
            _ph = Ph.Idle;
            Autotest.Note("ZDJECIA odlozone: " + why + " - najpierw miasto i bieg; zdjecia przy pierwszej jasnej porze (" + HourFrom + "-" + HourTo + ") w miescie", true);
            Autotest.PhotosOver(t, false, "zdjecia odlozone");
        }

        private static void StepAim(double t)
        {
            if (_ti >= Targets.Count) { End(t, "komplet celow", true); return; }
            var tg = Targets[_ti];
            Vec2 p;
            string src;
            if (!Resolve(tg, out p, out src))
            {
                Autotest.Note("ZDJECIA cel " + (_ti + 1) + "/" + Targets.Count + " \"" + tg.Name + "\": " + src + " - pomijam", true);
                _missing += Shots.Count;
                _ti++;
                _si = 0;
                return;
            }
            var shot = Shots[_si];
            _aimGround = Ground(p);
            _aim = new Vec3(p.x, p.y, _aimGround + 1f);
            _aimDist = shot.Distance;
            _aimBearing = (float)(tg.BearingDeg * Math.PI / 180.0);
            if (!ApplyCamera())
            {
                End(t, "kamery mapy nie da sie ustawic (brak skladowych MapCameraView - zob. linie \"sygnatury\" na starcie)", false);
                return;
            }
            if (_si == 0)
                Autotest.Note("ZDJECIA cel " + (_ti + 1) + "/" + Targets.Count + " \"" + tg.Name + "\": " + src + " | teren " + _aimGround.ToString("0.0", Inv), true);
            _needS = _si == 0 ? NewTargetWaitS : ShotWaitS;
            _needFrames = _si == 0 ? NewTargetFrames : ShotFrames;
            Go(Ph.Wait, t);
        }

        private static void StepWait(Campaign c, double t)
        {
            ApplyCamera();
            // okno, rozmowa albo ekran niegotowy - odliczanie od nowa (na zdjeciu ma byc mapa, nie okno)
            if (AutotestUi.AnyWindowOpen() || Talking(c) || !Bool(AutotestUi.Get(Screen(), "IsReady"))) { _phAt = t; _phFrames = Autotest.Frames; return; }
            if (t - _phAt < _needS || Autotest.Frames - _phFrames < _needFrames) return;
            // jak tryb zdjec gry (MissionGauntletPhotoMode): nie w trakcie kompilacji shaderow (nowe materialy rysuja sie wtedy
            // zastepczo) - najwyzej ShaderWaitS dluzej, potem zdjecie i tak, z liczba w logu
            _shaders = 0;
            try { _shaders = TaleWorlds.Engine.Utilities.GetNumberOfShaderCompilationsInProgress(); } catch { _shaders = -1; }
            if (_shaders > 0 && t - _phAt < _needS + ShaderWaitS) return;
            // silnik nie dal jeszcze zadnego pliku, a juz raz zawiodl na .png i .bmp - od razu zrzut okna (bez 16 s czekania)
            _attempt = _engineOk == 0 && _engineFails > 0 ? 2 : 0;
            Shoot(t);
        }

        private static void Shoot(double t)
        {
            var tg = Targets[_ti];
            var shot = Shots[_si];
            string baseName = tg.Name + "-" + shot.Name + (_attempt == 2 ? "-okno" : "");
            _file = Path.Combine(_dir, baseName + (_attempt == 1 ? ".bmp" : ".png"));
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
            _camNote = CameraNote();
            if (_attempt == 2)
            {
                string why;
                if (WindowShot(_file, out why)) { _windowShots++; Saved(t, "zrzut okna gry (PrintWindow; przy pelnym ekranie bywa czarny)"); }
                else Missing(t, "zrzut okna gry nieudany: " + why);
                return;
            }
            try
            {
                TaleWorlds.Engine.Utilities.TakeScreenshot(_file.Replace('\\', '/'));
            }
            catch (Exception e)
            {
                Autotest.Fail("Zdjecia.TakeScreenshot", e);
            }
            _lastLen = -1;
            _lastLenAt = t;
            Go(Ph.Await, t);
        }

        private static void StepAwait(double t)
        {
            ApplyCamera();
            long len = -1;
            try { var fi = new FileInfo(_file); if (fi.Exists) len = fi.Length; } catch { }
            if (len > 0)
            {
                if (len != _lastLen) { _lastLen = len; _lastLenAt = t; return; }
                if (t - _lastLenAt < 0.5) return;   // plik juz nie rosnie
                _engineOk++;
                Saved(t, "silnik (Utilities.TakeScreenshot)");
                return;
            }
            if (t - _phAt < FileWaitS) return;
            string name = Path.GetFileName(_file);
            if (_attempt == 0)
            {
                Autotest.Note("ZDJECIE " + name + ": silnik nie zapisal pliku w " + FileWaitS.ToString("0", Inv) + " s - druga proba jako .bmp", true);
                _attempt = 1;
                Shoot(t);
                return;
            }
            Autotest.Note("ZDJECIE " + name + ": silnik nie zapisal pliku w " + FileWaitS.ToString("0", Inv) + " s - trzecia proba: zrzut okna gry"
                          + (_engineOk == 0 ? " (dalsze ujecia od razu zrzutem okna, dopoki silnik nie da pliku)" : ""), true);
            _engineFails++;
            _attempt = 2;
            Shoot(t);
        }

        private static void Saved(double t, string how)
        {
            string kind = Kind(_file);
            string final = _file, conv = "";
            if (kind == "BMP" || kind == "JPG")
            {
                string png = Path.Combine(_dir, Path.GetFileNameWithoutExtension(_file) + ".png");
                string err;
                if (ToPng(_file, png, out err))
                {
                    if (!string.Equals(png, _file, StringComparison.OrdinalIgnoreCase)) { try { File.Delete(_file); } catch { } }
                    final = png;
                    _converted++;
                    conv = " (silnik dal " + kind + " - zamienione na PNG)";
                }
                else
                {
                    // nazwa ma mowic prawde o formacie: BMP w pliku .png dostaje .bmp
                    string ext = kind == "BMP" ? ".bmp" : ".jpg";
                    if (!_file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        string right = Path.Combine(_dir, Path.GetFileNameWithoutExtension(_file) + ext);
                        try { if (File.Exists(right)) File.Delete(right); File.Move(_file, right); final = right; } catch { }
                    }
                    conv = " (silnik dal " + kind + ", zamiana na PNG nieudana: " + err + ")";
                }
            }
            else if (kind == "PNG" && !_file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                string png = Path.Combine(_dir, Path.GetFileNameWithoutExtension(_file) + ".png");
                try { if (File.Exists(png)) File.Delete(png); File.Move(_file, png); final = png; } catch { }
            }
            else if (kind == "DDS")
            {
                // silnik z screenshot_format = 2 (engine_module.ini Native ma 1 = Png); System.Drawing DDS nie czyta
                string dds = Path.Combine(_dir, Path.GetFileNameWithoutExtension(_file) + ".dds");
                try { if (File.Exists(dds)) File.Delete(dds); File.Move(_file, dds); final = dds; } catch { }
                conv = " (silnik dal DDS - zostawiony jako .dds)";
            }
            else if (kind != "PNG") conv = " (format " + kind + " - zostawiony jak jest)";
            long len = 0;
            try { len = new FileInfo(final).Length; } catch { }
            _ok++;
            Autotest.Note("ZDJECIE " + Path.GetFileName(final) + " | " + (len / 1024) + " KB " + kind + conv + " | " + how
                          + (_shaders != 0 ? " | shadery w toku: " + (_shaders > 0 ? _shaders.ToString(Inv) : "?") : "") + " | " + _camNote, true);
            Next(t);
        }

        private static void Missing(double t, string why)
        {
            _missing++;
            _shotFails++;
            Autotest.Note("ZDJECIE BRAK " + Targets[_ti].Name + "-" + Shots[_si].Name + ": " + why + " | " + _camNote, true);
            if (_ok == 0 && _shotFails >= 2)
            {
                End(t, "dwa pierwsze ujecia bez pliku (silnik .png, .bmp i zrzut okna) - zrzuty w tej grze nie dzialaja, dalsze cele pominiete", false);
                return;
            }
            Next(t);
        }

        private static void Next(double t)
        {
            _si++;
            if (_si >= Shots.Count) { _si = 0; _ti++; }
            Go(Ph.Aim, t);
        }

        private static void End(double t, string why, bool ok)
        {
            RestoreCamera();
            _ph = Ph.Idle;
            _done = true;
            int all = Targets.Count * Shots.Count;
            Autotest.Note("ZDJECIA koniec" + (ok ? "" : " PRZERWANE") + ": " + why + " | zdjec " + _ok + "/" + all
                          + (_converted > 0 ? " (BMP->PNG: " + _converted + ")" : "") + (_windowShots > 0 ? " (zrzut okna: " + _windowShots + ")" : "")
                          + " | " + (Directory.Exists(_dir) ? "katalog " + _dir : "bez katalogu") + " | kamera z powrotem przy druzynie", true);
            Autotest.PhotosOver(t, _fromRun, ok ? "zdjecia zrobione" : "zdjecia przerwane");
        }

        // ------------------------------------------------------------------ cel i teren

        private static bool Resolve(Target tg, out Vec2 p, out string src)
        {
            if (tg.HasXY)
            {
                p = new Vec2(tg.X, tg.Y);
                src = "polozenie (" + tg.X.ToString("0.##", Inv) + ", " + tg.Y.ToString("0.##", Inv) + ")";
                return true;
            }
            Settlement s = null;
            try { s = Settlement.Find(tg.Settlement); } catch { }
            if (s == null) { p = Vec2.Zero; src = "brak osady \"" + tg.Settlement + "\""; return false; }
            Vec2 sp = s.Position.ToVec2();
            p = new Vec2(sp.x + tg.Dx, sp.y + tg.Dy);
            src = "osada " + s.StringId + " \"" + s.Name + "\" (" + sp.x.ToString("0.##", Inv) + ", " + sp.y.ToString("0.##", Inv) + ")"
                  + (tg.Dx != 0f || tg.Dy != 0f ? " + (" + tg.Dx.ToString("0.##", Inv) + ", " + tg.Dy.ToString("0.##", Inv) + ")" : "");
            return true;
        }

        private static float Ground(Vec2 p)
        {
            float h = 0f;
            try
            {
                var cv = new CampaignVec2(p, true);
                if (Campaign.Current.MapSceneWrapper.GetHeightAtPoint(in cv, ref h)) return h;
            }
            catch { }
            try { Vec3 n; Campaign.Current.MapSceneWrapper.GetTerrainHeightAndNormal(p, out h, out n); } catch { }
            return h;
        }

        // ------------------------------------------------------------------ kamera mapy (SandBox.View.Map.MapCameraView przez odbicia)

        private static object Screen()
        {
            var tm = AutotestUi.T("mapScreen");
            return tm != null ? AutotestUi.GetStatic(tm, "Instance") : null;
        }

        private static bool Bool(object v) { return v is bool && (bool)v; }

        private static MethodInfo _setMode;

        private static bool SetMode(object v, string mode)
        {
            if (_setMode == null)
            {
                for (var t = v.GetType(); t != null && _setMode == null; t = t.BaseType)
                    _setMode = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(m => m.Name == "SetCameraMode" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsEnum);
                if (_setMode == null) return false;
            }
            _setMode.Invoke(v, new[] { Enum.Parse(_setMode.GetParameters()[0].ParameterType, mode) });
            return true;
        }

        /// <summary>
        /// Kamera na cel: tryb MoveToPosition (wtedy MapCameraView.GetMapCameraInput nie ciagnie jej do druzyny), cel
        /// kamery od razu w punkcie (bez przelotu), odleglosc od razu docelowa, kat gry bez dodatkowego pochylenia.
        /// Wolane co cwierc sekundy az do zrzutu - kolko myszy / klawisze Jeffa nie przesuna ujecia.
        /// </summary>
        private static bool ApplyCamera()
        {
            var v = _view;
            if (v == null) return false;
            try
            {
                bool ok = SetMode(v, "MoveToPosition");
                ok &= AutotestUi.Set(v, "IdealCameraTarget", _aim);
                ok &= AutotestUi.Set(v, "_cameraTarget", _aim);
                ok &= AutotestUi.Set(v, "TargetCameraDistance", _aimDist);
                ok &= AutotestUi.Set(v, "CameraDistance", _aimDist);
                ok &= AutotestUi.Set(v, "AdditionalElevation", 0f);
                ok &= AutotestUi.Set(v, "CameraBearing", _aimBearing);
                // bez tych trzech tez dziala (gra przelicza je w nastepnej klatce) - brak nie jest bledem
                AutotestUi.Set(v, "CameraBearingVelocity", 0f);
                AutotestUi.Set(v, "_doFastCameraMovementToTarget", false);
                AutotestUi.Set(v, "_cameraElevation", (float)Math.Min(_aimDist * 0.0075 + 0.35, Math.PI * 99.0 / 200.0));
                if (!ok && !_camWarned) { _camWarned = true; Autotest.Note("ZDJECIA: nie wszystkie skladowe kamery mapy dalo sie ustawic (" + v.GetType().FullName + ")", true); }
                return ok;
            }
            catch (Exception e)
            {
                Autotest.Fail("Zdjecia.Kamera", e);
                return false;
            }
        }

        private static void SaveCamera()
        {
            try
            {
                _svDist = AutotestUi.Get(_view, "CameraDistance");
                _svTarget = AutotestUi.Get(_view, "TargetCameraDistance");
                _svAddElev = AutotestUi.Get(_view, "AdditionalElevation");
                _svBearing = AutotestUi.Get(_view, "CameraBearing");
                _saved = true;
            }
            catch (Exception e) { Autotest.Fail("Zdjecia.ZapisKamery", e); }
        }

        /// <summary>Kamera z powrotem: odleglosc, pochylenie i obrot sprzed zdjec, potem TeleportCameraToMainParty (tryb za druzyna).</summary>
        private static void RestoreCamera()
        {
            var v = _view;
            if (v == null) return;
            try
            {
                if (_saved)
                {
                    if (_svDist is float) AutotestUi.Set(v, "CameraDistance", _svDist);
                    if (_svTarget is float) AutotestUi.Set(v, "TargetCameraDistance", _svTarget);
                    if (_svAddElev is float) AutotestUi.Set(v, "AdditionalElevation", _svAddElev);
                    if (_svBearing is float) AutotestUi.Set(v, "CameraBearing", _svBearing);
                }
                if (Campaign.Current != null && MobileParty.MainParty != null) AutotestUi.Call(v, "TeleportCameraToMainParty");
            }
            catch (Exception e) { Autotest.Fail("Zdjecia.PowrotKamery", e); }
            _saved = false;
            _view = null;
        }

        private static string CameraNote()
        {
            float hour = HourNow();
            bool? f = AutotestUi.WindowFocused();
            string tail = " | cel (" + _aim.x.ToString("0.##", Inv) + ", " + _aim.y.ToString("0.##", Inv) + ") | godz. " + hour.ToString("0.0", Inv)
                          + " | okno gry " + (f == false ? "BEZ fokusu" : f == true ? "z fokusem" : "?");
            try
            {
                var cam = AutotestUi.Get(_view, "Camera") as TaleWorlds.Engine.Camera;
                var dist = AutotestUi.Get(_view, "CameraDistance");
                if (cam == null) return "kamera ?" + tail;
                Vec3 pos = cam.Position;
                Vec3 dir = cam.Direction;
                double len = Math.Max(1e-6, Math.Sqrt(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z));
                double pitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, -dir.z / len))) * 180.0 / Math.PI;
                float under = Ground(new Vec2(pos.x, pos.y));
                return "kamera: odl. " + (dist is float ? ((float)dist).ToString("0.0", Inv) : "?")
                       + ", wys. nad celem " + (pos.z - _aimGround).ToString("0.0", Inv)
                       + ", nad terenem pod kamera " + (pos.z - under).ToString("0.0", Inv)
                       + ", w dol " + pitch.ToString("0", Inv) + " st., obrot " + (_aimBearing * 180.0 / Math.PI).ToString("0", Inv) + " st." + tail;
            }
            catch (Exception e) { return "kamera ? (" + e.GetType().Name + ")" + tail; }
        }

        // ------------------------------------------------------------------ pliki

        private static string Kind(string path)
        {
            try
            {
                var b = new byte[8];
                int n;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) n = fs.Read(b, 0, 8);
                if (n >= 4 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "PNG";
                if (n >= 2 && b[0] == 0x42 && b[1] == 0x4D) return "BMP";
                if (n >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "JPG";
                if (n >= 4 && b[0] == 0x44 && b[1] == 0x44 && b[2] == 0x53 && b[3] == 0x20) return "DDS";
                return "nieznany (" + BitConverter.ToString(b, 0, n) + ")";
            }
            catch (Exception e) { return "nieczytelny (" + e.GetType().Name + ")"; }
        }

        private static bool ToPng(string src, string dst, out string err)
        {
            try
            {
                byte[] data = File.ReadAllBytes(src);
                string tmp = dst + ".tmp";
                using (var ms = new MemoryStream(data))
                using (var img = System.Drawing.Image.FromStream(ms))
                    img.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(tmp, dst);
                err = null;
                return true;
            }
            catch (Exception e)
            {
                err = e.GetType().Name + ": " + Autotest.OneLine(e.Message, 120);
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out Rect r);

        /// <summary>Ostatnia proba: zawartosc okna gry (PW_CLIENTONLY | PW_RENDERFULLCONTENT), wprost do PNG.</summary>
        private static bool WindowShot(string path, out string why)
        {
            try
            {
                IntPtr h;
                using (var p = Process.GetCurrentProcess()) h = p.MainWindowHandle;
                if (h == IntPtr.Zero) { why = "brak glownego okna procesu"; return false; }
                Rect r;
                if (!GetClientRect(h, out r) || r.Right - r.Left < 16 || r.Bottom - r.Top < 16) { why = "okno gry bez rozmiaru (zminimalizowane?)"; return false; }
                using (var bmp = new System.Drawing.Bitmap(r.Right - r.Left, r.Bottom - r.Top, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                {
                    bool ok;
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        IntPtr dc = g.GetHdc();
                        try { ok = PrintWindow(h, dc, 3); }
                        finally { g.ReleaseHdc(dc); }
                    }
                    if (!ok) { why = "PrintWindow zwrocilo false"; return false; }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                why = null;
                return true;
            }
            catch (Exception e)
            {
                why = e.GetType().Name + ": " + Autotest.OneLine(e.Message, 120);
                return false;
            }
        }
    }
}
