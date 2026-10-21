using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;
using EngineTex = TaleWorlds.Engine.Texture;
using UiTex = TaleWorlds.TwoDimension.Texture;

namespace Armoury
{
    /// <summary>
    /// m103-sceny: sprite z calej tekstury (UV 0..1) - odpowiednik TaleWorlds.GauntletUI.BaseTypes.SpriteFromTexture (w 1.4.8 internal i zawsze o nazwie "Sprite").
    /// Sprite jest public abstract z konstruktorem protected (TaleWorlds.TwoDimension); SpriteNinePatchParameters.Empty (IsValid false) - bez ciecia na dziewiec czesci.
    /// Rysowanie (BrushRenderer): sprite?.Texture, potem GetMinUvs/GetMaxUvs, warstwa StretchToFit - rozmiar sprite'a bez znaczenia (ramka okna ROT 1024x576).
    /// </summary>
    internal sealed class ArmFileSprite : Sprite
    {
        private readonly UiTex _texture;

        public ArmFileSprite(string name, UiTex texture, int width, int height) : base(name, width, height, SpriteNinePatchParameters.Empty)
        {
            _texture = texture;
        }

        public override UiTex Texture { get { return _texture; } }

        public override Vec2 GetMinUvs() { return Vec2.Zero; }

        public override Vec2 GetMaxUvs() { return Vec2.One; }
    }

    /// <summary>
    /// m103-sceny (E4): OBRAZKI OKIEN OPOWIESCI Z PLIKOW - ogolne (prolog 298, obrazki Jeffa w oknach wydarzen ROT, pozniej Kronika Westeros: cala ksiazka, potem serial).
    /// Pliki: Modules\Armoury\Images\&lt;katalog&gt;\&lt;nazwa&gt;.png|.jpg|.jpeg (Prolog\prolog_NN, ROT\rot_&lt;KlasaROT&gt;_&lt;n&gt;, Kronika\kronika_&lt;id&gt;). Plik sprawdzany przy KAZDYM
    /// pokazaniu okna (data zmiany i rozmiar) - nowy albo podmieniony PNG dziala bez przebudowy DLL i bez restartu gry.
    /// DROGA (dekompilacja 1.4.8, rozpoznanie m103 "obrazki" + krytyk): File.ReadAllBytes -> WALIDACJA BAJTOW (dysk C Jeffa zeruje pliki; zly plik podany natywnemu
    /// dekoderowi moze wywrocic gre poza zasiegiem try/catch): PNG - sygnatura, IHDR, kazdy blok z typem z liter i zgodna suma CRC, IDAT, IEND na koncu; JPG - SOI na
    /// poczatku, EOI na koncu, znacznik SOF z wymiarami; wymiary 1..4096, plik z samych zer odrzucony -> TaleWorlds.Engine.Texture.CreateFromMemory (jak gra dla awatarow
    /// Steam w AvatarThumbnailCache; dekoder stb_image silnika) -> EngineTexture -> TaleWorlds.TwoDimension.Texture -> ArmFileSprite -> wpis w
    /// UIResourceManager.SpriteData.Sprites pod nazwa "arm_&lt;nazwa&gt;" TUZ PRZED pchnieciem okna (wiazanie Gauntlet zamienia napis HeaderImage na sprite przez
    /// SpriteData.GetSprite - WidgetExtensions.ConvertObject; SpriteData.Reload wymienia slownik, dlatego wpis przy kazdym pokazaniu). Unikalna nazwa tekstury silnika.
    /// ZWALNIANIE: rysowanie zwolnionej tekstury = FailedAssert "Trying to render a released texture" (TwoDimensionEnginePlatform.DrawImage) - tekstura poprzedniego
    /// obrazka czeka w kolejce i jest zwalniana dopiero, gdy na stosie stanow gry nie ma okna wydarzenia (EventPopupState) i minelo ReleaseDelaySec (AppTick).
    /// KONTROLA PRZED ZWOLNIENIEM (recenzja m103): w grze wydaniowej ten assert nie trafia do zadnego logu (Debug.FailedAssert -> MBDebugManager.Assert ma puste
    /// cialo, ButterLib pisze go jako Debug, a na dysk idzie tylko Info), a po naszym Release EngineTexture.Texture = null i DrawImage po cichu nic nie rysuje (pusta
    /// ramka okna). Dlatego AppTick przed kazdym zwolnieniem sprawdza ekran (OnScreen): okno wydarzenia ROT na wierzchu z tym obrazkiem (albo z obrazkiem
    /// nieczytelnym) = zwolnienie wstrzymane + linia "ZWOLNIENIE PRZY OKNIE" (raz na obrazek; prog SC-13 grupy m103sceny).
    /// W pamieci najwyzej jeden obrazek w uzyciu + kolejka do zwolnienia. ReleaseAll: koniec prologu (kolejka), OnGameEnd (od razu).
    /// Brak pliku albo zly plik = null (okno bierze obrazek ROT) + linia logu (zly plik - raz na zmiane pliku). Bledy: licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static class StoryPictures
    {
        internal const string Tag = "Obrazki (m103): ";
        internal const string DirPrologue = "Prolog";
        internal const string DirRot = "ROT";
        internal const string DirChronicle = "Kronika";
        internal const string SpritePrefix = "arm_";
        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };
        private const int MaxSide = 4096;
        private const long MaxBytes = 48L * 1024 * 1024;
        private const double ReleaseDelaySec = 1.0;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Entry
        {
            public string Sprite;
            public string File;
            public DateTime Stamp;
            public long Size;
            public int W, H;
            public EngineTex Engine;   // silna referencja - bez niej finalizer NativeObject zwolni teksture
            public UiTex Ui;
            public ArmFileSprite SpriteObj;
            public long QueuedAt;
            public bool HeldLogged;    // linia "ZWOLNIENIE PRZY OKNIE" juz byla dla tego obrazka
        }

        private static readonly Dictionary<string, Entry> _loaded = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly List<Entry> _pending = new List<Entry>();
        private static readonly Dictionary<string, string> _badLogged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static int _stumbles, _loads, _rejects, _released, _seq, _held;
        private static FieldInfo _fScreenState;   // ROT.Screens.EventScreen._eventPopupState (do OnScreen)
        private static Type _fScreenType;
        private static string _root;

        internal static int Stumbles { get { return _stumbles; } }
        internal static int Loads { get { return _loads; } }
        internal static int Rejects { get { return _rejects; } }
        internal static int Released { get { return _released; } }
        internal static int Pending { get { return _pending.Count; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("StoryPictures." + where + " (potkniecie " + _stumbles + ")", e);
        }

        // ---------------------------------------------------------------- pliki

        /// <summary>Modules\Armoury\Images (wedlug polozenia DLL, zapas BasePath - jak MapVillageTooltip.ModulePath).</summary>
        internal static string Root()
        {
            if (_root != null) return _root;
            string r = null;
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // Modules\Armoury\bin\Win64_Shipping_Client
                r = Path.GetFullPath(Path.Combine(Path.Combine(dir, ".."), Path.Combine("..", "Images")));
            }
            catch { }
            if (r == null || !Directory.Exists(Path.GetDirectoryName(r)))
            {
                try { r = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(BasePath.Name, "Modules"), "Armoury"), "Images")); } catch { }
            }
            _root = r ?? "";
            return _root;
        }

        internal static string Dir(string sub)
        {
            string r = Root();
            return string.IsNullOrEmpty(r) ? "" : Path.Combine(r, sub);
        }

        internal static string FindFile(string sub, string stem)
        {
            try
            {
                string d = Dir(sub);
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return null;
                foreach (string ext in Extensions)
                {
                    string p = Path.Combine(d, stem + ext);
                    if (File.Exists(p)) return p;
                }
            }
            catch (Exception e) { Stumble("FindFile " + sub + "/" + stem, e); }
            return null;
        }

        /// <summary>Ile plikow obrazkow (prefix*.png|jpg|jpeg) w katalogu - do linii startu.</summary>
        internal static int CountFiles(string sub, string prefix)
        {
            try
            {
                string d = Dir(sub);
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return 0;
                int n = 0;
                foreach (string f in Directory.GetFiles(d, prefix + "*"))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg") n++;
                }
                return n;
            }
            catch { return 0; }
        }

        // ---------------------------------------------------------------- wczytanie

        /// <summary>Nazwa sprite'a do HeaderImage okna (arm_&lt;stem&gt;) albo null: brak pliku, zly plik, blad silnika. how - opis do logu
        /// ("prolog_05.png 1024x576, 23 ms" / "brak pliku" / "zly plik: ..."). Watek glowny, tuz przed pchnieciem okna.</summary>
        internal static string Acquire(string sub, string stem, out string how)
        {
            how = "-";
            string name = SpritePrefix + stem;
            try
            {
                var data = UIResourceManager.SpriteData;
                if (data == null) { how = "brak SpriteData"; return null; }
                string file = FindFile(sub, stem);
                if (file == null)
                {
                    Entry gone;
                    if (_loaded.TryGetValue(name, out gone)) Queue(gone);   // plik usuniety w trakcie gry - stara tekstura do zwolnienia
                    how = "brak pliku";
                    return null;
                }
                var info = new FileInfo(file);
                long size = info.Length;
                DateTime stamp = info.LastWriteTimeUtc;
                Entry e;
                if (_loaded.TryGetValue(name, out e) && Same(e, file, stamp, size))
                {
                    data.Sprites[name] = e.SpriteObj;
                    QueueOthers(name);
                    how = Path.GetFileName(file) + " " + e.W + "x" + e.H + ", z pamieci";
                    return name;
                }
                // ten sam plik czeka jeszcze w kolejce do zwolnienia (okno ROT zamkniete i otwarte znowu) - bierzemy go z powrotem
                for (int i = 0; i < _pending.Count; i++)
                {
                    var p = _pending[i];
                    if (p.Sprite == name && Same(p, file, stamp, size))
                    {
                        _pending.RemoveAt(i);
                        if (e != null) Queue(e);
                        _loaded[name] = p;
                        data.Sprites[name] = p.SpriteObj;
                        QueueOthers(name);
                        how = Path.GetFileName(file) + " " + p.W + "x" + p.H + ", z pamieci";
                        return name;
                    }
                }
                if (size <= 0 || size > MaxBytes) { how = Bad(file, stamp, size, "rozmiar " + size + " B"); return null; }
                byte[] bytes = File.ReadAllBytes(file);
                int w, h;
                string why = Validate(bytes, out w, out h);
                if (why != null) { how = Bad(file, stamp, size, why); return null; }
                long t0 = Stopwatch.GetTimestamp();
                EngineTex tex = EngineTex.CreateFromMemory(bytes);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                if ((object)tex == null) { how = Bad(file, stamp, size, "silnik nie odczytal obrazka (CreateFromMemory = null)"); return null; }
                if (!tex.IsValid) { how = Bad(file, stamp, size, "silnik nie odczytal obrazka (tekstura bez wskaznika)"); return null; }
                _seq++;
                try { tex.Name = name + "_" + _seq.ToString(Inv); } catch { }
                var ui = new UiTex(new EngineTexture(tex));
                var sprite = new ArmFileSprite(name, ui, w, h);
                if (e != null) Queue(e);
                var ne = new Entry { Sprite = name, File = file, Stamp = stamp, Size = size, W = w, H = h, Engine = tex, Ui = ui, SpriteObj = sprite };
                _loaded[name] = ne;
                data.Sprites[name] = sprite;
                QueueOthers(name);
                _loads++;
                _badLogged.Remove(file);
                how = Path.GetFileName(file) + " " + w + "x" + h + ", dekodowanie " + ms.ToString("0", Inv) + " ms";
                return name;
            }
            catch (Exception ex)
            {
                Stumble("Acquire " + sub + "/" + stem, ex);
                how = "blad " + ex.GetType().Name;
                return null;
            }
        }

        private static bool Same(Entry e, string file, DateTime stamp, long size)
        {
            return e != null && string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase) && e.Stamp == stamp && e.Size == size
                   && (object)e.Engine != null && !e.Engine.IsReleased;
        }

        private static string Bad(string file, DateTime stamp, long size, string why)
        {
            _rejects++;
            string key = stamp.Ticks.ToString(Inv) + "|" + size.ToString(Inv);
            string was;
            if (!_badLogged.TryGetValue(file, out was) || was != key)
            {
                _badLogged[file] = key;
                Log.Info(Tag + "ZLY PLIK " + file + " - " + why + " (okno bierze obrazek ROT; popraw plik narzedziem tools/obrazki_okien.py).");
            }
            return "zly plik: " + why;
        }

        // ---------------------------------------------------------------- walidacja bajtow (przed dekoderem natywnym)

        private static readonly uint[] CrcTable = MakeCrcTable();

        private static uint[] MakeCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        private static uint Crc(byte[] b, int start, int len)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = start, end = start + len; i < end; i++) c = CrcTable[(c ^ b[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        private static int BE32(byte[] b, int i) { return (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]; }

        /// <summary>null = plik dobry (w, h - wymiary z naglowka); inaczej powod odrzucenia.</summary>
        internal static string Validate(byte[] b, out int w, out int h)
        {
            w = h = 0;
            if (b == null || b.Length == 0) return "pusty plik";
            bool zero = true;
            for (int i = 0; i < b.Length; i += 509) if (b[i] != 0) { zero = false; break; }
            if (zero && b[b.Length - 1] == 0) return "same zera (plik wyzerowany na dysku)";
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
                return ValidatePng(b, out w, out h);
            if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ValidateJpg(b, out w, out h);
            return "nie PNG ani JPG (sygnatura " + (b.Length >= 4 ? b[0].ToString("X2") + b[1].ToString("X2") + b[2].ToString("X2") + b[3].ToString("X2") : "-") + ")";
        }

        private static string ValidatePng(byte[] b, out int w, out int h)
        {
            w = h = 0;
            int pos = 8;
            bool ihdr = false, idat = false, iend = false;
            while (pos + 12 <= b.Length)
            {
                int len = BE32(b, pos);
                if (len < 0 || pos + 12L + len > b.Length) return "blok PNG poza plikiem (plik uciety albo uszkodzony) przy bajcie " + pos;
                for (int k = 4; k < 8; k++)
                {
                    byte c = b[pos + k];
                    if (!((c >= 65 && c <= 90) || (c >= 97 && c <= 122))) return "zly typ bloku PNG przy bajcie " + pos + " (uszkodzony plik)";
                }
                string type = new string(new[] { (char)b[pos + 4], (char)b[pos + 5], (char)b[pos + 6], (char)b[pos + 7] });
                uint crc = (uint)BE32(b, pos + 8 + len);
                if (Crc(b, pos + 4, len + 4) != crc) return "zla suma CRC bloku " + type + " przy bajcie " + pos + " (uszkodzony plik)";
                if (!ihdr)
                {
                    if (type != "IHDR" || len < 13) return "pierwszy blok PNG to nie IHDR";
                    ihdr = true;
                    w = BE32(b, pos + 8);
                    h = BE32(b, pos + 12);
                    if (w <= 0 || h <= 0 || w > MaxSide || h > MaxSide) return "wymiary " + w + "x" + h + " (dozwolone 1.." + MaxSide + ")";
                }
                else if (type == "IDAT") idat = true;
                else if (type == "IEND") { iend = true; pos += 12 + len; break; }
                pos += 12 + len;
            }
            if (!ihdr) return "brak IHDR";
            if (!idat) return "brak danych obrazka (IDAT)";
            if (!iend) return "brak IEND (plik uciety)";
            return null;
        }

        private static string ValidateJpg(byte[] b, out int w, out int h)
        {
            w = h = 0;
            int end = b.Length;
            if (end < 4 || b[end - 2] != 0xFF || b[end - 1] != 0xD9) return "brak znacznika konca JPG (EOI) - plik uciety albo wyzerowany";
            int pos = 2;
            while (pos + 4 <= end)
            {
                if (b[pos] != 0xFF) return "zly znacznik JPG przy bajcie " + pos;
                byte m = b[pos + 1];
                if (m == 0xFF) { pos++; continue; }
                if (m == 0xD8 || m == 0x01 || (m >= 0xD0 && m <= 0xD7)) { pos += 2; continue; }
                int len = (b[pos + 2] << 8) | b[pos + 3];
                if (len < 2 || pos + 2 + len > end) return "segment JPG poza plikiem przy bajcie " + pos;
                bool sof = (m >= 0xC0 && m <= 0xCF) && m != 0xC4 && m != 0xC8 && m != 0xCC;
                if (sof)
                {
                    if (len < 7) return "za krotki naglowek SOF";
                    h = (b[pos + 5] << 8) | b[pos + 6];
                    w = (b[pos + 7] << 8) | b[pos + 8];
                    if (w <= 0 || h <= 0 || w > MaxSide || h > MaxSide) return "wymiary " + w + "x" + h + " (dozwolone 1.." + MaxSide + ")";
                }
                if (m == 0xDA) return w > 0 ? null : "brak naglowka SOF przed danymi obrazka";
                pos += 2 + len;
            }
            return "brak danych obrazka (SOS)";
        }

        // ---------------------------------------------------------------- zwalnianie

        /// <summary>Wszystkie wczytane poza keep do kolejki zwolnienia (najwyzej jeden obrazek w uzyciu).</summary>
        private static void QueueOthers(string keep)
        {
            if (_loaded.Count <= 1) return;
            var names = new List<string>(_loaded.Keys);
            foreach (var n in names) if (n != keep) Queue(_loaded[n]);
        }

        private static void Queue(Entry e)
        {
            if (e == null) return;
            Entry cur;
            if (_loaded.TryGetValue(e.Sprite, out cur) && ReferenceEquals(cur, e)) _loaded.Remove(e.Sprite);
            try
            {
                var data = UIResourceManager.SpriteData;
                Sprite s;
                if (data != null && data.Sprites.TryGetValue(e.Sprite, out s) && ReferenceEquals(s, e.SpriteObj)) data.Sprites.Remove(e.Sprite);
            }
            catch (Exception ex) { Stumble("Queue.Sprites", ex); }
            if (_pending.Contains(e)) return;
            e.QueuedAt = Stopwatch.GetTimestamp();
            _pending.Add(e);
        }

        /// <summary>Obrazek przestaje byc potrzebny (okno ROT zamkniete) - do kolejki zwolnienia.</summary>
        internal static void Drop(string spriteName)
        {
            try
            {
                Entry e;
                if (spriteName != null && _loaded.TryGetValue(spriteName, out e)) Queue(e);
            }
            catch (Exception ex) { Stumble("Drop", ex); }
        }

        /// <summary>Koniec prologu (i podobne): wszystkie obrazki katalogu sub do kolejki zwolnienia.</summary>
        internal static void DropDir(string sub)
        {
            try
            {
                string d = Dir(sub);
                var names = new List<string>(_loaded.Keys);
                foreach (var n in names)
                {
                    var e = _loaded[n];
                    if (e.File != null && string.Equals(Path.GetDirectoryName(e.File), d, StringComparison.OrdinalIgnoreCase)) Queue(e);
                }
            }
            catch (Exception ex) { Stumble("DropDir", ex); }
        }

        /// <summary>Okno wydarzenia ROT (albo nasze) na stosie stanow gry - po nazwie typu, bez typu ROT w kodzie.</summary>
        internal static bool EventWindowOnStack()
        {
            var gsm = GameStateManager.Current;
            if (gsm == null) return false;
            foreach (var s in gsm.GameStates)
                if (s != null && s.GetType().FullName == "ROT.CampaignBehaviors.EventPopupState") return true;
            return false;
        }

        /// <summary>SubModuleMain.OnApplicationTick (przez Prologue.ScenesAppTick, 4 razy na sekunde): zwalnia tekstury z kolejki, gdy zadne okno wydarzenia nie jest
        /// na stosie stanow i minelo ReleaseDelaySec od wstawienia do kolejki (okno zdjete, warstwa juz nie rysuje).</summary>
        internal static void AppTick()
        {
            if (_pending.Count == 0) return;
            try
            {
                if (EventWindowOnStack()) return;
                long now = Stopwatch.GetTimestamp();
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    var e = _pending[i];
                    if (now - e.QueuedAt < (long)(ReleaseDelaySec * Stopwatch.Frequency)) continue;
                    string busy = OnScreen(e);
                    if (busy != null)
                    {
                        e.QueuedAt = now;   // tekstura zostaje do zamkniecia okna (ReleaseAll przy koncu gry zwalnia ja i tak)
                        if (!e.HeldLogged)
                        {
                            e.HeldLogged = true;
                            _held++;
                            Log.Info(Tag + "ZWOLNIENIE PRZY OKNIE " + e.Sprite + " - " + busy + " - zwolnienie wstrzymane (blad kolejki obrazkow: tekstura w kolejce, a okno"
                                     + " ja rysuje) | wstrzymanych w sesji " + _held + ".");
                        }
                        continue;
                    }
                    _pending.RemoveAt(i);
                    ReleaseNow(e);
                }
            }
            catch (Exception ex) { Stumble("AppTick", ex); }
        }

        /// <summary>Kontrola przed zwolnieniem (AppTick): null = obrazka nie ma na ekranie; inaczej powod. Rysowany jest tylko TopScreen - okno wydarzenia ROT to ekran
        /// ROT.Screens.EventScreen (warstwa "EventScreen"); jego obrazek: _eventPopupState.Event.HeaderImage (nazwa sprite'a wpisana przy LoadMovie). Sprawdzenie
        /// niezalezne od bramki EventWindowOnStack (inna nazwa typu - ekran, nie stan). Obrazek okna nieczytelny (zmiana ROT) = traktowany jak ten sam.</summary>
        private static string OnScreen(Entry e)
        {
            try
            {
                var top = ScreenManager.TopScreen;
                if (top == null) return null;
                var tt = top.GetType();
                if (tt.FullName != "ROT.Screens.EventScreen" && top.FindLayer<ScreenLayer>("EventScreen") == null) return null;
                string hi = null;
                if (_fScreenType != tt) { _fScreenType = tt; _fScreenState = AccessTools.Field(tt, "_eventPopupState"); }
                var st = _fScreenState != null ? _fScreenState.GetValue(top) : null;
                var pe = st != null ? AccessTools.Property(st.GetType(), "Event") : null;
                var ev = pe != null ? pe.GetValue(st, null) : null;
                var ph = ev != null ? AccessTools.Property(ev.GetType(), "HeaderImage") : null;
                if (ph != null) hi = ph.GetValue(ev, null) as string;
                if (hi == null) return "okno wydarzenia ROT na wierzchu ekranu (obrazka okna nie da sie odczytac)";
                if (hi != e.Sprite) return null;
                Entry cur;
                // ta sama nazwa, ale w slowniku juz nowszy obrazek (plik podmieniony) - okno wzielo nowszy przy LoadMovie, ten mozna zwolnic
                if (_loaded.TryGetValue(e.Sprite, out cur) && !ReferenceEquals(cur, e)) return null;
                return "okno wydarzenia ROT na wierzchu ekranu z tym obrazkiem";
            }
            catch (Exception ex) { Stumble("OnScreen", ex); return "blad sprawdzenia ekranu " + ex.GetType().Name; }
        }

        private static void ReleaseNow(Entry e)
        {
            try
            {
                var data = UIResourceManager.SpriteData;
                Sprite s;
                if (data != null && data.Sprites.TryGetValue(e.Sprite, out s) && ReferenceEquals(s, e.SpriteObj)) data.Sprites.Remove(e.Sprite);
            }
            catch { }
            try
            {
                // EngineTexture.Release -> Texture.ReleaseImmediately (to samo robi SpriteCategory.Unload z arkuszami); po nim EngineTexture.Texture = null,
                // a DrawImage pomija sprite bez tekstury silnika - bez assertu
                if (e.Ui != null && (object)e.Engine != null && !e.Engine.IsReleased) e.Ui.PlatformTexture.Release();
                _released++;
            }
            catch (Exception ex) { Stumble("Release " + e.Sprite, ex); }   // zapas: teksture zwolni finalizer po utracie referencji
            e.Engine = null; e.Ui = null; e.SpriteObj = null;
        }

        /// <summary>OnGameEnd: wszystko od razu (okna juz nie ma - stany gry czyszczone przy wyjsciu z kampanii; bez kontroli OnScreen - po koncu gry nic nie rysuje
        /// okien kampanii).</summary>
        internal static void ReleaseAll(string why)
        {
            int n = 0;
            try
            {
                foreach (var e in new List<Entry>(_loaded.Values)) { Queue(e); }
                foreach (var e in _pending) { ReleaseNow(e); n++; }
                _pending.Clear();
                _loaded.Clear();
            }
            catch (Exception ex) { Stumble("ReleaseAll", ex); }
            if (n > 0) Log.Info(Tag + "zwolnione obrazki: " + n + " (" + why + ") | wczytanych w sesji " + _loads + ", odrzuconych " + _rejects + ", zwolnionych razem " + _released
                                + ", wstrzymanych przy oknie " + _held + " | potkniecia " + _stumbles + ".");
        }

        internal static string StartText()
        {
            string r = Root();
            bool has = !string.IsNullOrEmpty(r) && Directory.Exists(r);
            return "katalog obrazkow " + (string.IsNullOrEmpty(r) ? "-" : r) + (has ? "" : " (brak)")
                   + " | plikow: Prolog " + CountFiles(DirPrologue, "prolog_") + ", ROT " + CountFiles(DirRot, "rot_") + ", Kronika " + CountFiles(DirChronicle, "kronika_");
        }
    }
}
