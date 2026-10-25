using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P5 "okno-lupu": OKNO LUPU po bitwie i oblezeniu (raport SpoilsReport; projekt noc/projekty/okno-lupu.json "WYGLAD" i "KIEDY POKAZAC");
    /// noc P11 (125): takze po turnieju (ramka T, BuildTourney - "The Tourney at X"; ta sama warstwa, prefab i bramka).
    /// Wlasna warstwa Gauntlet na MapScreen (wzor CraftPopup: kolejnosc 4500, InputRestrictions, focus) z prefabem Modules\Armoury\GUI\Prefabs\ArmSpoilsWindow.xml
    /// w wygladzie okien wydarzen ROT (ROT-Content EventXml: tlo event-bg, Standard.ROTPanel, ramka frame_9, pedzel UI.ROTSummaryScreen.Text). Sprite'y ROT
    /// (ui_event_category - event-bg i obrazki tematow) laduje sama, jak ROT.Screens.EventScreen (SpriteCategory.Load jest idempotentne, ROT tez nie zwalnia).
    /// ZAPAS: brak prefabu w WidgetFactory, brak ROT (kategoria ui_ROT, prefab Standard.ROTPanel) albo wyjatek przy LoadMovie -> StoryWindow.Show (okno wydarzenia ROT
    /// z tym samym tekstem; samo ma zapas InformationManager.ShowInquiry). Tekst zapasu bez slow zaloby (straty jako "fallen") - lektor/muzyka wydarzen.
    /// KIEDY: raport gotowy (SpoilsReport.NextReady) i bramka: Prologue.SceneGate (bez wlasnego warunku - _selfGate), StoryWindow zamkniete, Spoils bez fazy, zadne menu
    /// realistic_loot_*, bez misji, GrandTourney bez kolejki (TourneyRansom._missionBouts/_offers/_popupOpen odbiciem, bez zmiany DLL GT), na wierzchu MapScreen -
    /// i to wszystko trzyma sie 1.0 s (jak StableSec Kroniki). Kronika i sceny prologu czekaja: SceneGate dostaje warunek IsOpen (okno otwarte albo raport gotowy).
    /// Zadnego SwitchToMenu (CLAUDE.md 7). Na czas okna czas stoi (TimeControlMode = Stop, bez blokady).
    /// ZAMKNIECIE: przycisk Done, Esc, Enter (wejscie warstwy co klatke - jak EventScreen), zmiana ekranu (TopScreen to nie nasz ekran), OnGameEnd; w autotescie
    /// (LogGate.Mode 1/2) samo po 1.5 s - inaczej test stanie. Zwolnienie: ReleaseMovie, RemoveLayer, OnFinalize VM, obrazek z pliku do kolejki
    /// StoryPictures (zwolnienie tekstury wstrzymane przy IsOpen - StoryPictures.AppTick). Bledy: licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static class SpoilsWindow
    {
        private const string Movie = "ArmSpoilsWindow";
        private const string DirLoot = "Loot";
        private const double StableSec = 1.0;
        private const double AutotestCloseSec = 1.5;
        private const double PhotoCloseSec = 12.0;   // paczka 132: okno proby zdjec zamyka WindowPhotos po zdjeciu - to tylko bezpiecznik
        private static readonly long CheckTicks = Stopwatch.Frequency / 4;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static GauntletLayer _layer;
        private static object _movie;
        private static ScreenBase _screen;
        private static SpoilsWindowVM _vm;
        private static SpoilsReport.Rep _rep;
        private static string _mode = "";
        private static string _fileSprite;
        private static StoryWindow.Spec _spec;      // zapas: okno opowiesci z tekstem raportu
        private static long _openedAt, _lastCheck, _okSince;
        private static bool _selfGate;
        private static string _closeKey;            // Esc/Enter - zamkniecie w nastepnej klatce
        private static int _stumbles;
        private static bool _photo;                 // paczka 132: okno proby zdjec (ShowPhoto) - raport probny, bez SpoilsReport.Finish
        private static readonly List<string> _blocks = new List<string>();

        internal static int Stumbles { get { return _stumbles; } }

        /// <summary>noc P11: okno lupu (wlasna warstwa albo zapas) na ekranie - proba autotestu turnieju czeka.</summary>
        internal static bool Busy { get { return _layer != null || _spec != null; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 8) Log.Error("SpoilsWindow." + where + " (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>Okno lupu otwarte albo raport gotowy czeka na pokazanie (czyta Prologue.SceneGate i StoryPictures.AppTick). Falsz w trakcie wlasnej bramki.</summary>
        internal static bool IsOpen
        {
            get
            {
                if (_selfGate) return false;
                if (_layer != null || _spec != null) return true;
                try { return SpoilsReport.AnyReady; } catch { return false; }
            }
        }

        // ------------------------------------------------------------ takt (SubModuleMain.OnApplicationTick - co klatke)

        internal static void AppTick()
        {
            if (_layer != null)
            {
                try { FrameTick(); }
                catch (Exception e) { Stumble("FrameTick", e); Close("blad ticku warstwy"); }
            }
            long t = Stopwatch.GetTimestamp();
            if (t - _lastCheck < CheckTicks) return;
            _lastCheck = t;
            try { Step(t); }
            catch (Exception e) { Stumble("Step", e); }
        }

        private static void FrameTick()
        {
            var l = _layer;
            if (l == null) return;
            if (_screen == null || !ReferenceEquals(ScreenManager.TopScreen, _screen) || !_screen.HasLayer(l)) { Close("zmiana ekranu"); return; }
            // klawisz zamyka w NASTEPNEJ klatce: zdarzenie puszczenia klawisza trwa jedna klatke, a warstwa zdjeta w tej samej klatce przepuscilaby Esc
            // do mapy pod spodem (menu Esc gry zaraz po oknie)
            if (_closeKey != null) { string k = _closeKey; _closeKey = null; Close(k); return; }
            var inp = l.Input;
            if (inp != null)
            {
                if (inp.IsKeyReleased(InputKey.Escape)) { _closeKey = "Esc"; return; }
                if (inp.IsKeyReleased(InputKey.Enter) || inp.IsKeyReleased(InputKey.NumpadEnter)) { _closeKey = "Enter"; return; }
            }
            double lim = _photo ? PhotoCloseSec : AutotestCloseSec;   // paczka 132: okno proby zdjec zamyka WindowPhotos (zdjecie po 1.8 s)
            if (Autotest() && Sec(_openedAt) >= lim) Close("autotest " + lim.ToString("0.0", Inv) + " s");
        }

        private static double Sec(long since) { return since == 0 ? 0 : (Stopwatch.GetTimestamp() - since) / (double)Stopwatch.Frequency; }

        private static bool Autotest() { int m = LogGate.Mode; return m == 1 || m == 2; }

        private static void Step(long t)
        {
            var c = Campaign.Current;
            if (c == null || !c.GameStarted) { _okSince = 0; return; }
            SpoilsReport.Watch();
            SpoilsReport.EnsureGtWired();   // noc P11: latki GrandTourney (zakresy kiesy turnieju) - raz na proces, w kampanii; wlasny try
            SpoilsReport.TestStep();        // noc P11: proba autotestu okna turnieju (LogGate.Mode 1/2 + plik flagi) - bez nich od razu wyjscie; wlasny try
            if (_spec != null) { WatchFallback(); return; }
            if (_layer != null) return;
            var r = SpoilsReport.NextReady();
            if (r == null) { _okSince = 0; return; }
            if (!ReferenceEquals(r, _rep)) { _rep = r; _blocks.Clear(); }
            string skip = SkipWhy(r);
            if (skip != null) { SpoilsReport.Finish(r, "pominiete", skip, null); _rep = null; return; }
            string why = Gate(c);
            if (why != null)
            {
                _okSince = 0;
                string k = why.Length > 40 ? why.Substring(0, 40) : why;
                if (_blocks.Count < 6 && !_blocks.Contains(k)) _blocks.Add(k);
                return;
            }
            if (_okSince == 0) { _okSince = t; return; }
            if ((t - _okSince) / (double)Stopwatch.Frequency < StableSec) return;
            _okSince = 0;
            Show(r);
        }

        /// <summary>Klucze MCM i prog wartosci (na zywo, przy pokazaniu). null - pokazac.</summary>
        private static string SkipWhy(SpoilsReport.Rep r)
        {
            var s = Settings.Current;
            if (r.Kind == SpoilsReport.KBattle && !s.SpoilsWindowAfterBattle) return "klucz Spoils Window After Battle NIE";
            if ((r.Kind == SpoilsReport.KSiege || r.Kind == SpoilsReport.KRansom) && !s.SpoilsWindowAfterSiege) return "klucz Spoils Window After Siege NIE";
            if (r.Kind == SpoilsReport.KTourney && !s.SpoilsWindowAfterTourney) return "klucz Spoils Window After Tourney NIE";
            // noc P11: turniej - takze same straty (zaklady, wykup konia i zbroi), bo gracz chce wiedziec, co zostalo; widz bez zakladow - pominiety
            if (r.Kind == SpoilsReport.KTourney ? !r.AnythingTourney : !r.AnythingWon) return "nic nie zdobyto";
            // recenzja P11 (2): turniej - prog liczy zyski i straty (TourneyStake); Worth liczy tylko zyski, wiec raport z samymi stratami zawsze bylby ponizej progu
            long worth = r.Kind == SpoilsReport.KTourney ? SpoilsReport.TourneyStake(r) : SpoilsReport.Worth(r);
            int min = Math.Max(0, Math.Min(5000, s.SpoilsWindowMinWorth));
            if (min > 0 && worth < min) return "wartosc " + worth.ToString(Inv) + " ponizej progu " + min.ToString(Inv) + " (Spoils Window Min Worth)";
            return null;
        }

        /// <summary>null - wolno pokazac; inaczej powod czekania.</summary>
        private static string Gate(Campaign c)
        {
            string why;
            _selfGate = true;
            try { why = Prologue.SceneGate(c); }
            finally { _selfGate = false; }
            if (why != null) return why;
            if (StoryWindow.IsOpen) return "okno opowiesci";
            if (SpoilsReport.SpoilsBusy()) return "Spoils w toku";
            try
            {
                var mc = c.CurrentMenuContext;
                var gm = mc != null ? mc.GameMenu : null;
                string id = gm != null ? gm.StringId : null;
                if (id != null && id.StartsWith("realistic_loot", StringComparison.Ordinal)) return "menu " + id;
            }
            catch { }
            if (TaleWorlds.MountAndBlade.Mission.Current != null) return "misja";
            if (GtBusy()) return "okna GrandTourney";
            var top = ScreenManager.TopScreen;
            if (!(top is SandBox.View.Map.MapScreen)) return "na wierzchu nie mapa (" + (top != null ? top.GetType().Name : "-") + ")";
            return null;
        }

        // ------------------------------------------------------------ GrandTourney (odbicie, bez zmiany DLL GT)

        private static int _gtKnown;   // 0 nie sprawdzone, 1 jest, -1 brak
        private static FieldInfo _gtBouts, _gtOffers, _gtPopup;

        internal static bool GtKnown()
        {
            if (_gtKnown == 0)
            {
                try
                {
                    var t = Type.GetType("GrandTourney.TourneyRansom, GrandTourney");
                    if (t != null)
                    {
                        _gtBouts = AccessTools.Field(t, "_missionBouts");
                        _gtOffers = AccessTools.Field(t, "_offers");
                        _gtPopup = AccessTools.Field(t, "_popupOpen");
                    }
                    bool ok = _gtBouts != null && _gtOffers != null && _gtPopup != null;
                    // brak - na stale dopiero w kampanii (linia startu pyta w menu glownym; zestaw GT mogl nie byc jeszcze zaladowany)
                    _gtKnown = ok ? 1 : (Campaign.Current != null ? -1 : 0);
                }
                catch { _gtKnown = Campaign.Current != null ? -1 : 0; }
            }
            return _gtKnown > 0;
        }

        /// <summary>noc P11: ramka turnieju czeka, dopoki GrandTourney rozlicza wykup konia i zbroi (SpoilsReport.TWatch).</summary>
        internal static bool GtBusyNow() { return GtBusy(); }

        private static bool GtBusy()
        {
            if (!GtKnown()) return false;
            try
            {
                var b = _gtBouts.GetValue(null) as ICollection;
                var o = _gtOffers.GetValue(null) as ICollection;
                object p = _gtPopup.GetValue(null);
                return (b != null && b.Count > 0) || (o != null && o.Count > 0) || (p is bool && (bool)p);
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ pokazanie

        internal static bool PrefabFileExists()
        {
            try
            {
                string img = StoryPictures.Root();
                if (string.IsNullOrEmpty(img)) return false;
                string mod = Path.GetDirectoryName(img);
                return File.Exists(Path.Combine(Path.Combine(Path.Combine(mod, "GUI"), "Prefabs"), Movie + ".xml"));
            }
            catch { return false; }
        }

        private static bool IsCustomType(string name)
        {
            try
            {
                var p = typeof(UIResourceManager).GetProperty("WidgetFactory", BindingFlags.Public | BindingFlags.Static);
                var wf = p != null ? p.GetValue(null, null) : null;
                var m = wf != null ? wf.GetType().GetMethod("IsCustomType", new[] { typeof(string) }) : null;
                return m != null && (bool)m.Invoke(wf, new object[] { name });
            }
            catch { return false; }
        }

        private static bool HasSprite(string name)
        {
            try
            {
                var d = UIResourceManager.SpriteData;
                return d != null && d.Sprites != null && d.Sprites.ContainsKey(name);
            }
            catch { return false; }
        }

        private static void Show(SpoilsReport.Rep r)
        {
            var content = Build(r);
            string header = Picture(r, out _fileSprite);
            string why = GauntletUnavailable();
            string mode = null;
            if (why == null)
            {
                try
                {
                    if (ShowGauntlet(r, content, header)) mode = "wlasne";
                    else why = "warstwa nie weszla";
                }
                catch (Exception e) { Stumble("Show.Gauntlet", e); why = "wyjatek " + e.GetType().Name; RemoveLayer(); }
            }
            if (mode == null) mode = ShowFallback(r, content, header, why);
            if (mode == null)
            {
                if (_fileSprite != null) { StoryPictures.Drop(_fileSprite); _fileSprite = null; }
                SpoilsReport.Finish(r, "pominiete", "okno nie weszlo (" + why + ", zapas tez nie)", null);
                _rep = null;
                return;
            }
            r.Shown = true;
            _rep = r;
            _mode = mode;
            _openedAt = Stopwatch.GetTimestamp();
            // na czas okna czas stoi (bez blokady - gracz puszcza go sam); w autotescie NIE - test steruje czasem sam, a okno i tak zamyka sie po 1.5 s
            if (!Autotest()) { try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop; } catch { } }
        }

        /// <summary>null - wlasne okno mozliwe; inaczej powod zapasu.</summary>
        private static string GauntletUnavailable()
        {
            try
            {
                var data = UIResourceManager.SpriteData;
                if (data == null || data.SpriteCategories == null) return "brak SpriteData";
                if (!data.SpriteCategories.ContainsKey("ui_ROT")) return "brak kategorii sprite'ow ui_ROT (ROT-Content)";
                if (!IsCustomType("Standard.ROTPanel")) return "brak prefabu Standard.ROTPanel (ROT-Content)";
                if (!IsCustomType(Movie)) return "brak prefabu " + Movie + " (GUI/Prefabs modulu)";
                return null;
            }
            catch (Exception e) { Stumble("GauntletUnavailable", e); return "blad sprawdzenia"; }
        }

        private static void EnsureEventSprites()
        {
            try
            {
                var data = UIResourceManager.SpriteData;
                SpriteCategoryLoad(data);
            }
            catch (Exception e) { Stumble("EnsureEventSprites", e); }
        }

        private static void SpriteCategoryLoad(TaleWorlds.TwoDimension.SpriteData data)
        {
            if (data == null || data.SpriteCategories == null) return;
            TaleWorlds.TwoDimension.SpriteCategory cat;
            if (data.SpriteCategories.TryGetValue("ui_event_category", out cat) && cat != null && !cat.IsLoaded)
                cat.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);   // jak ROT.Screens.EventScreen (bez zwalniania - ROT tez nie zwalnia)
        }

        // 150b (25.10, Jeff: "dopasuj obrazki w oknach lupu do miejsca"): obrazki ROT (ui_event_category) wedlug wroga, morza i regionu osady - przeglad albumu 150a:
        // tylko widoki i sceny bitew, obozow i zamkow, bez portretow i scen fabuly; Joffrey nigdzie, sala tronowa tylko turniej w King's Landing.
        private static readonly string[] PicUndead = { "nightking", "freefolk3", "jon_death", "wallfall" };       // Inni: Nocny Krol, polegli na lodzie
        private static readonly string[] PicWild = { "freefolk1", "freefolk2", "fist", "WildlingsFail" };         // dzicy (Wolni Ludzie): snieg za Murem
        private static readonly string[] PicSea = { "StannisSetsSail", "Ship2", "Ship1" };                        // bitwa na morzu
        private static readonly string[] PicSeaCrown = { "blackwater", "StannisSetsSail", "Ship2" };              // morze przy Koronnych Ziemiach (Czarny Nurt)
        private static readonly string[] PicSeaEssos = { "aegon", "StannisSetsSail", "Ship2" };                   // morze przy Essos (flota pod smokiem)
        private static readonly string[] PicBeyond = { "freefolk1", "fist", "freefolk2" };                         // osada za Murem
        private static readonly string[] PicWall = { "eastwatch", "fist", "messenger-img" };                       // Mur (Nocna Straz)
        private static readonly string[] PicNorth = { "jon_stand", "messenger-img", "RobbMarching3" };            // Polnoc, Skagos
        private static readonly string[] PicRiver = { "riverlands", "messenger-1", "RobbMarching3" };             // Dorzecze
        private static readonly string[] PicIron = { "StannisSetsSail", "Ship2", "messenger-2" };                 // Zelazne Wyspy
        private static readonly string[] PicEssos = { "dany_invasion", "aegon" };                                  // Essos
        private static readonly string[] PicField = { "jon_stand", "messenger-1", "messenger-2" };                // reszta Westeros: szarza, oboz po bitwie
        private static readonly string[] SiegeWall = { "eastwatch", "wallfall" };
        private static readonly string[] SiegeBeyond = { "fist", "eastwatch" };
        private static readonly string[] SiegeNorth = { "traveling_merchant", "harrenhal" };                       // zamek pod osniezonymi gorami
        private static readonly string[] SiegeRiver = { "harrenhal", "riverlands" };
        private static readonly string[] SiegeVale = { "traveling_merchant", "harrenhal" };
        private static readonly string[] SiegeCrown = { "harrenhal", "blackwater" };
        private static readonly string[] SiegeIron = { "harrenhal", "StannisSetsSail" };
        private static readonly string[] SiegeEssos = { "dany_invasion", "aegon" };
        private static readonly string[] SiegeAny = { "harrenhal", "traveling_merchant" };
        private static readonly string[] TourneyKl = { "kingslanding" };
        private static readonly string[] TourneyAny = { "traveling_merchant", "harrenhal" };                       // zamek z tlumem na moscie; Harrenhal - Wielki Turniej
        private const string KingsLanding = "town_EW4";                                                            // ROT-Map settlements.xml

        /// <summary>150b: ostatni obrazek okna i jego temat ("obrazek X (temat, osada Y / kultura)") - do linii logu okna i proby zdjec 132.</summary>
        internal static string LastPicture = "";

        /// <summary>150b: region obrazka z kultury osady (id kultur ROT - battania to Polnoc, aserai to Dorne, empire to Braavos, nord to Lorath).</summary>
        private static string PicRegion(string cul)
        {
            switch (cul ?? "")
            {
                case "battania": case "skagosi": return "north";
                case "nightswatch": return "wall";
                case "freefolk": case "wildlings": return "beyond";
                case "river": return "river";
                case "vale": case "hilltribesmen": return "vale";
                case "crownlands": return "crown";
                case "sturgia": return "iron";
                case "empire": case "nord": case "khuzait": case "ghiscari": case "valyrian": case "ibbenese": case "lyseni": case "myrish": case "norvos":
                case "pentoshi": case "qartheen": case "qohorik": case "sarnor": case "summer": case "tyroshi": case "volantine": case "yiti":
                    return "essos";
            }
            return "west";   // Westerlands, Reach, Stormlands, Dragonstone, Dorne - i nieznane
        }

        /// <summary>150b: osada regionu obrazka - osada raportu (szturm, okup, turniej) albo miejsca bitwy; kryjowka (kultura bandytow) - najblizsza osada nie-kryjowka.</summary>
        private static TaleWorlds.CampaignSystem.Settlements.Settlement PicSettlement(SpoilsReport.Rep r)
        {
            var st = r.Kind == SpoilsReport.KBattle ? (r.Near ?? r.Town) : (r.Town ?? r.Near);
            if (st == null && Hero.MainHero != null) st = Hero.MainHero.LastKnownClosestSettlement;
            if (st == null || !st.IsHideout) return st;
            TaleWorlds.CampaignSystem.Settlements.Settlement best = null; float bd = float.MaxValue;
            var p = st.GetPosition2D;
            foreach (var t in TaleWorlds.CampaignSystem.Settlements.Settlement.All)
            {
                if (t == null || t.IsHideout || !(t.IsTown || t.IsCastle || t.IsVillage)) continue;
                float d = p.DistanceSquared(t.GetPosition2D);
                if (d < bd) { bd = d; best = t; }
            }
            return best ?? st;
        }

        /// <summary>150b: pula obrazkow ROT wedlug rodzaju raportu, wroga (Inni, dzicy), morza i regionu osady; theme - nazwa tematu do logu.</summary>
        private static string[] PicPool(SpoilsReport.Rep r, string region, string townId, out string theme)
        {
            if (r.Kind == SpoilsReport.KTourney)
            {
                if (townId == KingsLanding) { theme = "turniej King's Landing"; return TourneyKl; }
                theme = "turniej"; return TourneyAny;
            }
            if (r.Kind == SpoilsReport.KSiege || r.Kind == SpoilsReport.KRansom)
            {
                theme = "zamek " + region;
                switch (region)
                {
                    case "wall": return SiegeWall;
                    case "beyond": return SiegeBeyond;
                    case "north": return SiegeNorth;
                    case "river": return SiegeRiver;
                    case "vale": return SiegeVale;
                    case "crown": return SiegeCrown;
                    case "iron": return SiegeIron;
                    case "essos": return SiegeEssos;
                }
                return SiegeAny;
            }
            if (r.Foe == "undead") { theme = "Inni"; return PicUndead; }
            if (r.Sea)
            {
                theme = "morze " + region;
                return region == "crown" ? PicSeaCrown : region == "essos" ? PicSeaEssos : PicSea;
            }
            if (r.Foe == "wild") { theme = "dzicy"; return PicWild; }
            theme = "pole " + region;
            switch (region)
            {
                case "beyond": return PicBeyond;
                case "wall": return PicWall;
                case "north": return PicNorth;
                case "river": return PicRiver;
                case "iron": return PicIron;
                case "essos": return PicEssos;
            }
            return PicField;
        }

        /// <summary>Obrazek okna: plik Images/Loot/loot_&lt;rodzaj&gt;_&lt;n&gt; (StoryPictures - walidacja bajtow), potem sprite ROT dopasowany do miejsca (150b: PicPool),
        /// ostatecznie messenger-img. fileSprite - nazwa sprite'a z pliku (do zwolnienia po zamknieciu) albo null.</summary>
        private static string Picture(SpoilsReport.Rep r, out string fileSprite)
        {
            fileSprite = null;
            LastPicture = "";
            try
            {
                EnsureEventSprites();
                string stem = r.Kind == SpoilsReport.KSiege ? (r.SackKind == CitySack.KMercy ? "siege" : "sack") : r.Kind == SpoilsReport.KRansom ? "siege"
                              : r.Kind == SpoilsReport.KTourney ? "tourney" : "battle";
                int files = StoryPictures.CountFiles(DirLoot, "loot_" + stem + "_");
                if (files > 0)
                {
                    string how;
                    string s = StoryPictures.Acquire(DirLoot, "loot_" + stem + "_" + (1 + r.Id % files).ToString(Inv), out how);
                    if (s != null) { fileSprite = s; LastPicture = "obrazek z pliku " + s; return s; }
                }
                TaleWorlds.CampaignSystem.Settlements.Settlement st = null;
                try { st = PicSettlement(r); } catch (Exception e) { Stumble("Picture.osada", e); }
                string cul = null, townId = null;
                try { cul = st != null && st.Culture != null ? st.Culture.StringId : null; townId = st != null ? st.StringId : null; } catch { }
                string region = PicRegion(cul), theme;
                var pool = PicPool(r, region, townId, out theme);
                for (int i = 0; i < pool.Length; i++)
                {
                    string n = pool[(r.Id + i) % pool.Length];
                    if (HasSprite(n))
                    {
                        LastPicture = "obrazek " + n + " (" + theme + ", osada " + (st != null ? SpoilsReport.Nm(st) : "-") + " / " + (cul ?? "-") + ")";
                        return n;
                    }
                }
                LastPicture = "brak obrazka tematu " + theme;
            }
            catch (Exception e) { Stumble("Picture", e); }
            return HasSprite("messenger-img") ? "messenger-img" : "";
        }

        private static bool ShowGauntlet(SpoilsReport.Rep r, Content c, string header)
        {
            var top = ScreenManager.TopScreen;
            if (top == null) return false;
            RemoveLayer();   // nigdy dwoch warstw naraz
            _closeKey = null;
            var vm = new SpoilsWindowVM(c, header, HasSprite("event-bg") ? "event-bg" : "");
            var layer = new GauntletLayer("ArmSpoilsWindow", 4500);
            _vm = vm; _layer = layer; _screen = top;
            _movie = layer.LoadMovie(Movie, vm);
            if (_movie == null) { RemoveLayer(); return false; }   // bez filmu warstwa z fokusem bylaby niewidoczna i blokowalaby wejscie
            layer.InputRestrictions.SetInputRestrictions();
            layer.IsFocusLayer = true;
            top.AddLayer(layer);
            ScreenManager.TrySetFocus(layer);
            return true;
        }

        private static void RemoveLayer()
        {
            var l = _layer; var scr = _screen;
            _layer = null; _screen = null;
            _photo = false;
            try
            {
                if (l != null)
                {
                    try { l.InputRestrictions.ResetInputRestrictions(); } catch { }
                    try { l.IsFocusLayer = false; ScreenManager.TryLoseFocus(l); } catch { }
                    if (_movie is GauntletMovieIdentifier m) l.ReleaseMovie(m);
                    if (scr != null && scr.HasLayer(l)) scr.RemoveLayer(l);
                    else if (ScreenManager.TopScreen != null && ScreenManager.TopScreen.HasLayer(l)) ScreenManager.TopScreen.RemoveLayer(l);
                }
            }
            catch (Exception e) { Stumble("RemoveLayer", e); }
            _movie = null;
            try { if (_vm != null) _vm.OnFinalize(); } catch (Exception e) { Stumble("RemoveLayer.OnFinalize", e); }
            _vm = null;
        }

        // ------------------------------------------------------------ zapas: okno opowiesci (ROT) albo zapytanie

        private static string ShowFallback(SpoilsReport.Rep r, Content c, string header, string why)
        {
            try
            {
                var spec = new StoryWindow.Spec
                {
                    Key = "loot_" + SpoilsReport.KindName(r.Kind), Title = c.Title, Upper = c.UpperText(), Lower = c.LowerText(), DoneText = "Done",
                    Header = header ?? "", Background = "event-bg", Tag = r.Id, Closed = OnFallbackClosed
                };
                _spec = spec;
                string how = StoryWindow.Show(spec);
                if (how == null) { _spec = null; return null; }
                return "zapas " + how + " (" + (why ?? "-") + ")";
            }
            catch (Exception e) { Stumble("ShowFallback", e); _spec = null; return null; }
        }

        private static void OnFallbackClosed(StoryWindow.Spec s, string how)
        {
            if (!ReferenceEquals(s, _spec)) return;
            _spec = null;
            FinishShown("zapas zamkniety: " + how);
        }

        private static void WatchFallback()
        {
            var s = _spec;
            if (s == null) return;
            try
            {
                if (s.Done) { OnFallbackClosed(s, "zamkniete"); return; }
                // autotest: okno wydarzenia ROT zamyka AutotestUi CrashScribe (1.5 s); zapytanie zapasu - tutaj
                if (Autotest() && Sec(_openedAt) >= AutotestCloseSec && s.Mode == "zapas")
                {
                    try { InformationManager.HideInquiry(); } catch { }
                    StoryWindow.OnClosed(s, "autotest " + AutotestCloseSec.ToString("0.0", Inv) + " s");
                }
            }
            catch (Exception e) { Stumble("WatchFallback", e); }
        }

        // ------------------------------------------------------------ zamkniecie

        /// <summary>Zamkniecie okna (przycisk, Esc, Enter, zmiana ekranu, autotest, OnGameEnd). Idempotentne.</summary>
        internal static void Close(string why)
        {
            bool had = _layer != null;
            if (had) RemoveLayer();
            if (_spec != null && why == "wyjscie z kampanii") { try { _spec.Done = true; } catch { } _spec = null; }
            if (had) FinishShown("zamkniete: " + why);
            if (why == "wyjscie z kampanii")
            {
                if (_fileSprite != null) { try { StoryPictures.Drop(_fileSprite); } catch { } _fileSprite = null; }
                _rep = null; _okSince = 0; _blocks.Clear();
            }
        }

        // ------------------------------------------------------------ paczka 132: proba zdjec okien (WindowPhotos - tylko autotest)

        /// <summary>Okno lupu z raportu PROBNEGO (WindowPhotos): ta sama tresc (Build), obrazek (Picture) i warstwa (ShowGauntlet) co po bitwie; bez zapasu,
        /// bez zatrzymania czasu i bez raportu (_rep null - zamkniecie nie wola SpoilsReport.Finish, liczniki sesji bez zmian). null - otwarte; inaczej powod.
        /// paczka 150 (tylko proba obrazkow SpoilsAtlas): sprite - obrazek WYMUSZONY (nazwa z SpriteData) zamiast Picture, title - tytul okna zamiast TitleOf;
        /// bez nich (WindowPhotos) wszystko jak dotad.</summary>
        internal static string ShowPhoto(SpoilsReport.Rep r, string sprite = null, string title = null)
        {
            if (r == null) return "brak raportu probnego";
            if (_layer != null || _spec != null) return "okno lupu juz otwarte";
            if (_rep != null && !_rep.Done) return "prawdziwy raport lupu czeka na okno";
            string why = GauntletUnavailable();
            if (why != null) return "wlasne okno niedostepne: " + why;
            var content = Build(r);
            if (title != null) content.Title = title;
            string header;
            if (sprite != null)
            {
                EnsureEventSprites();   // jak Picture: kategoria ui_event_category zaladowana przed LoadMovie
                if (!HasSprite(sprite)) return "brak sprite'a " + sprite + " w SpriteData";
                _fileSprite = null;     // sprite z arkusza - nic do zwolnienia po zamknieciu
                header = sprite;
            }
            else header = Picture(r, out _fileSprite);
            bool ok = false;
            try { ok = ShowGauntlet(r, content, header); }
            catch (Exception e) { Stumble("ShowPhoto", e); RemoveLayer(); why = "wyjatek " + e.GetType().Name; }
            if (!ok)
            {
                if (_fileSprite != null) { try { StoryPictures.Drop(_fileSprite); } catch { } _fileSprite = null; }
                return why ?? "warstwa nie weszla";
            }
            _photo = true;
            _rep = null;
            _mode = "zdjecie (132)";
            _openedAt = Stopwatch.GetTimestamp();
            return null;
        }

        /// <summary>paczka 132: okno proby zdjec na ekranie.</summary>
        internal static bool PhotoOpen { get { return _photo && _layer != null; } }

        /// <summary>paczka 132: zamkniecie okna proby zdjec (nic, gdy zamkniete juz inaczej - Done, Esc, Enter, zmiana ekranu).</summary>
        internal static void ClosePhoto() { if (PhotoOpen) Close("proba zdjec (132)"); }

        private static void FinishShown(string how)
        {
            var r = _rep;
            _rep = null;
            if (_fileSprite != null) { try { StoryPictures.Drop(_fileSprite); } catch { } _fileSprite = null; }
            if (r == null) return;
            double waited = r.ReadyTicks != 0 ? (_openedAt - r.ReadyTicks) / (double)Stopwatch.Frequency : 0;
            string w = _mode + " | pokazane po " + waited.ToString("0.0", Inv) + " s od gotowosci"
                       + (_blocks.Count > 0 ? " (blokady: " + string.Join(", ", _blocks.ToArray()) + ")" : "")
                       + ", otwarte " + Sec(_openedAt).ToString("0.0", Inv) + " s | " + how + (LastPicture.Length > 0 ? " | " + LastPicture : "");
            _blocks.Clear();
            SpoilsReport.Finish(r, "pokazane", null, w);
        }

        // ------------------------------------------------------------ tresc (wspolna dla okna i zapasu)

        internal sealed class Row { public string Label, Value; }
        internal sealed class Section { public string Title; public readonly List<Row> Rows = new List<Row>(); }
        internal sealed class Fine { public EquipmentElement E; public int N; public string Text; }

        internal sealed class Content
        {
            public string Title = "", Place = "", Total = "";
            public readonly List<Section> Sections = new List<Section>();
            public readonly List<Fine> Finest = new List<Fine>();

            internal string UpperText()
            {
                var sb = new StringBuilder();
                if (Place.Length > 0) sb.Append(Place).Append('\n');
                for (int i = 0; i < Sections.Count && i < 2; i++) AppendSection(sb, Sections[i]);
                return sb.ToString().TrimEnd();
            }

            internal string LowerText()
            {
                var sb = new StringBuilder();
                for (int i = 2; i < Sections.Count; i++) AppendSection(sb, Sections[i]);
                if (Finest.Count > 0)
                {
                    sb.Append("Finest spoils: ");
                    for (int i = 0; i < Finest.Count; i++) { if (i > 0) sb.Append("; "); sb.Append(Finest[i].Text); }
                    sb.Append('\n');
                }
                sb.Append(Total);
                return sb.ToString().TrimEnd();
            }

            private static void AppendSection(StringBuilder sb, Section s)
            {
                sb.Append(s.Title).Append(": ");
                for (int i = 0; i < s.Rows.Count; i++) { if (i > 0) sb.Append("; "); sb.Append(s.Rows[i].Label).Append(' ').Append(s.Rows[i].Value); }
                sb.Append('\n');
            }
        }

        private static string N(long v) { return v.ToString("N0", Inv); }
        private static string Den(long v) { return (v >= 0 ? "+" : "-") + N(Math.Abs(v)) + " denars"; }

        private const int GArm = 0, GWeap = 1, GAmmo = 2, GHorse = 3, GHarn = 4, GGoods = 5;
        private static readonly string[] GroupNames = { "Armour", "Weapons and shields", "Ammunition", "Horses", "Horse harness", "Goods and supplies" };

        private static int GroupOf(ItemObject it)
        {
            int k = LootTally.KindOf(it);
            switch (k)
            {
                case LootTally.Kon: return GHorse;
                case LootTally.Uprzaz: return GHarn;
                case LootTally.Tulow: case LootTally.Glowa: case LootTally.Rece: case LootTally.Nogi: case LootTally.Peleryna: return GArm;
                case LootTally.Bron: case LootTally.Tarcza: return GWeap;
                case LootTally.Pociski: return GAmmo;
                default: return GGoods;
            }
        }

        internal static string TitleOf(SpoilsReport.Rep r)
        {
            string town = SpoilsReport.Nm(r.Town);
            if (r.Kind == SpoilsReport.KTourney) return town != "-" ? "The Tourney at " + town : "Tourney Purse";   // noc P11
            if (r.Kind == SpoilsReport.KRansom) return "The Ransom of " + town;
            if (r.Kind == SpoilsReport.KSiege && r.Town != null)
            {
                if (r.SackKind == CitySack.KMercy) return "The Taking of " + town;
                if (r.SackKind == CitySack.KPillage || r.SackKind == CitySack.KDevastate) return "The Sack of " + town;
            }
            return r.Battles > 1 ? "Spoils of " + r.Battles.ToString(Inv) + " Battles" : "Spoils of Battle";
        }

        /// <summary>Tresc okna z raportu (tylko liczby raportu - zadnego liczenia od nowa). Puste sekcje sie nie pokazuja; wierszy najwyzej tyle, ile miesci panel.</summary>
        internal static Content Build(SpoilsReport.Rep r)
        {
            if (r.Kind == SpoilsReport.KTourney) return BuildTourney(r);   // noc P11: wlasne sekcje turnieju (szranki, nagroda, oddany rynsztunek)
            var c = new Content { Title = TitleOf(r) };
            try
            {
                // miejsce
                if (r.Kind == SpoilsReport.KRansom) c.Place = SpoilsReport.Nm(r.Town) + " paid to be spared the siege. " + r.Date + ".";
                else if (r.Kind == SpoilsReport.KSiege && r.Town != null) c.Place = "The walls of " + SpoilsReport.Nm(r.Town) + (r.Enemy.Length > 0 ? ", against " + r.Enemy : "") + ". " + r.Date + ".";
                else c.Place = (r.Place.Length > 0 && r.Place != "-" ? "Near " + r.Place : "In the field") + (r.Enemy.Length > 0 ? ", against " + r.Enemy : "") + ". " + r.Date + "."
                               + (r.Battles > 1 ? " " + r.Battles.ToString(Inv) + " battles before the spoils were counted." : "");

                // COIN
                var coin = new Section { Title = "Coin" };
                for (int i = 0; i < r.CoinLabels.Count && coin.Rows.Count < 5; i++)
                    if (r.CoinAmounts[i] != 0) coin.Rows.Add(new Row { Label = r.CoinLabels[i], Value = Den(r.CoinAmounts[i]) });
                long purse = r.CoinTotal;
                if (coin.Rows.Count > 0) coin.Rows.Add(new Row { Label = "To your purse", Value = Den(purse) });
                if (r.Men != 0) coin.Rows.Add(new Row { Label = "Your men's purses", Value = Den(r.Men) });
                if (r.Crown > 0) coin.Rows.Add(new Row { Label = "The crown will claim at dawn", Value = N(r.Crown) + " denars" });
                if (coin.Rows.Count > 0) c.Sections.Add(coin);

                // GEAR AND GOODS
                var gn = new int[GroupNames.Length]; var gw = new long[GroupNames.Length];
                var all = new List<SpoilsReport.Gain>(r.Gear.Values);
                foreach (var g in all) { int k = GroupOf(g.E.Item); gn[k] += g.N; gw[k] += (long)Math.Max(0, g.E.ItemValue) * g.N; }
                var gear = new Section { Title = "Gear and goods" };
                for (int k = 0; k < GroupNames.Length; k++)
                    if (gn[k] > 0) gear.Rows.Add(new Row { Label = GroupNames[k] + ": " + N(gn[k]) + (gn[k] == 1 ? " piece" : " pieces"), Value = "worth " + N(gw[k]) });
                if (r.LeftN > 0) gear.Rows.Add(new Row { Label = "Left on the field: " + N(r.LeftN) + (r.LeftN == 1 ? " piece" : " pieces"), Value = "worth " + N(r.LeftW) });
                if (gear.Rows.Count > 0) c.Sections.Add(gear);

                // PRISONERS
                var pris = new Section { Title = "Prisoners" };
                int troops = r.TroopCount;
                if (troops + r.Lords.Count > 0) pris.Rows.Add(new Row { Label = "Taken", Value = N(troops + r.Lords.Count) });
                if (r.Lords.Count > 0)
                {
                    var names = new StringBuilder();
                    for (int i = 0; i < r.Lords.Count && i < 3; i++) { if (i > 0) names.Append(", "); names.Append(SpoilsReport.Nm(r.Lords[i])); }
                    if (r.Lords.Count > 3) names.Append(" and ").Append(r.Lords.Count - 3).Append(" more");
                    pris.Rows.Add(new Row { Label = "Lords: " + names, Value = "" });
                }
                if (r.RansomWorth > 0) pris.Rows.Add(new Row { Label = "Ransom worth about", Value = N(r.RansomWorth) + " denars" });
                if (pris.Rows.Count > 0) c.Sections.Add(pris);

                // THE COST
                if (r.Fallen + r.Wounded > 0)
                {
                    var cost = new Section { Title = "The cost" };
                    cost.Rows.Add(new Row { Label = "Your fallen: " + N(r.Fallen) + ", wounded: " + N(r.Wounded), Value = "" });
                    c.Sections.Add(cost);
                }

                // RENOWN
                long ren = (long)Math.Round(r.Renown), inf = (long)Math.Round(r.Influence);
                if (ren != 0 || inf != 0)
                {
                    var rn = new Section { Title = "Renown" };
                    rn.Rows.Add(new Row { Label = (ren >= 0 ? "+" : "") + N(ren) + " renown" + (inf != 0 ? ", " + (inf >= 0 ? "+" : "") + N(inf) + " influence" : ""), Value = "" });
                    c.Sections.Add(rn);
                }

                // FINEST SPOILS - do 5 sztuk wedlug wartosci sztuki (przedmiot x stan)
                all.Sort(delegate (SpoilsReport.Gain a, SpoilsReport.Gain b) { return b.E.ItemValue.CompareTo(a.E.ItemValue); });
                for (int i = 0; i < all.Count && c.Finest.Count < 5; i++)
                {
                    var g = all[i];
                    if (g.E.Item == null || g.E.ItemValue <= 0) continue;
                    string name;
                    try { var t = g.E.GetModifiedItemName(); name = t != null ? t.ToString() : g.E.Item.Name.ToString(); } catch { name = g.E.Item.StringId; }
                    c.Finest.Add(new Fine { E = g.E, N = g.N, Text = name + (g.N > 1 ? " x" + N(g.N) : "") + " - worth " + N(g.E.ItemValue) + (g.N > 1 ? " each" : "") });
                }

                c.Total = "In all: " + N(Math.Max(0, purse) + Math.Max(0, r.Men)) + " denars in coin, " + N(r.GearWorth) + " in gear" + (r.RansomWorth > 0 ? ", " + N(r.RansomWorth) + " in ransoms" : "") + ".";
            }
            catch (Exception e) { Stumble("Build", e); }
            return c;
        }

        private static string Pieces(long n) { return N(n) + (n == 1 ? " piece" : " pieces"); }

        /// <summary>noc P11: etykieta wiersza najwyzej tyle znakow, ile miesci kolumna (ok. 360 px pedzlem 20) - dluzsza przycieta z "...".</summary>
        private static string Cut(string s, int max = 40)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max - 3).TrimEnd() + "...";
        }

        private static string ElName(EquipmentElement e)
        {
            try { var t = e.GetModifiedItemName(); return t != null ? t.ToString() : e.Item.Name.ToString(); }
            catch { return e.Item != null ? e.Item.StringId : "-"; }
        }

        /// <summary>noc P11 (125): tresc okna turnieju z raportu ramki T (tylko liczby raportu). Sekcje: The lists (mistrz, walki gracza, runda odpadniecia,
        /// final), Coin (etykiety zakresow kiesy + "Other", "To your purse" = roznica kiesy), The prize and the arms (nagroda turnieju, kon i zbroja oddane
        /// graczowi), The cost (kon i zbroja oddane przez druzyne gracza), Renown. Puste sekcje sie nie pokazuja.</summary>
        private static Content BuildTourney(SpoilsReport.Rep r)
        {
            var c = new Content { Title = TitleOf(r) };
            try
            {
                var t = r.T ?? new SpoilsReport.TInfo();
                string town = SpoilsReport.Nm(r.Town);
                if (town == "-") town = r.Place;
                c.Place = "The lists at " + town + ". " + r.Date + "." + (t.Rode ? " You rode in the lists." : " You watched from the stands.") + (r.Test ? " (autotest)" : "");

                // THE LISTS
                var ls = new Section { Title = "The lists" };
                // wiersze z imionami przyciete do szerokosci kolumny (wiersz ma stala wysokosc - bez zawijania)
                if (t.Champion.Length > 0)
                    ls.Rows.Add(new Row { Label = Cut("Champion: " + (t.ChampionWho == 1 ? "you" : t.Champion + (t.ChampionWho == 2 ? " (of your company)" : ""))), Value = "" });
                if (t.Rode && t.Bouts > 0) ls.Rows.Add(new Row { Label = "Your bouts", Value = N(t.BoutsWon) + " won of " + N(t.Bouts) });
                if (t.ElimRound >= 0)
                {
                    ls.Rows.Add(new Row { Label = "You fell in round " + N(t.ElimRound + 1) + (t.Rounds > 0 ? " of " + N(t.Rounds) : ""), Value = "" });
                    if (t.FellTo.Length > 0) ls.Rows.Add(new Row { Label = Cut("Bested by " + t.FellTo), Value = "" });
                }
                if (t.Final.Length > 0) ls.Rows.Add(new Row { Label = Cut("The final: " + t.Final), Value = "" });
                // zaklady - te same liczby co wiersze Coin (zakresy kiesy wokol PlaceABet i OnPlayerWinTournament): postawione i wyplacone albo przegrane
                long staked = 0, paidOut = 0;
                for (int i = 0; i < r.CoinLabels.Count; i++)
                {
                    if (r.CoinLabels[i] == SpoilsReport.LTBetStake) staked -= r.CoinAmounts[i];
                    else if (r.CoinLabels[i] == SpoilsReport.LTBetWon) paidOut += r.CoinAmounts[i];
                }
                if (staked > 0) ls.Rows.Add(new Row { Label = "Your wagers: " + N(staked) + " staked", Value = paidOut > 0 ? N(paidOut) + " paid out" : "lost" });
                if (ls.Rows.Count > 0) c.Sections.Add(ls);

                // COIN - kazda etykieta osobno (najwyzej 8 wierszy; nadmiar zlozony w jeden wiersz, zeby suma wierszy = "To your purse")
                var coin = new Section { Title = "Coin" };
                long folded = 0; int foldedN = 0;
                for (int i = 0; i < r.CoinLabels.Count; i++)
                {
                    long v = r.CoinAmounts[i];
                    if (v == 0) continue;
                    if (coin.Rows.Count < 8) coin.Rows.Add(new Row { Label = r.CoinLabels[i], Value = Den(v) });
                    else { folded += v; foldedN++; }
                }
                if (foldedN > 0) coin.Rows.Add(new Row { Label = "Other sums (" + N(foldedN) + ")", Value = Den(folded) });
                if (coin.Rows.Count > 0) coin.Rows.Add(new Row { Label = r.CoinTotal >= 0 ? "To your purse" : "Out of your purse", Value = Den(r.CoinTotal) });
                if (coin.Rows.Count > 0) c.Sections.Add(coin);

                // THE PRIZE AND THE ARMS - nagroda turnieju (przedmiot TournamentGame.Prize w sakwach) i reszta przyrostu sakw (kon i zbroja oddane graczowi w GrandTourney)
                var gear = new Section { Title = "The prize and the arms" };
                var all = new List<SpoilsReport.Gain>(r.Gear.Values);
                int otherN = 0; long otherW = 0; bool prizeShown = false;
                foreach (var g in all)
                {
                    long w = (long)Math.Max(0, g.E.ItemValue) * g.N;
                    if (!prizeShown && t.Prize != null && ReferenceEquals(g.E.Item, t.Prize))
                    {
                        gear.Rows.Add(new Row { Label = Cut("The prize: " + ElName(g.E) + (g.N > 1 ? " x" + N(g.N) : "")), Value = "worth " + N(w) });
                        prizeShown = true;
                        continue;
                    }
                    otherN += g.N; otherW += w;
                }
                if (otherN > 0) gear.Rows.Add(new Row { Label = (GtKnown() ? "Horse and armour won: " : "Other gear: ") + Pieces(otherN), Value = "worth " + N(otherW) });
                if (gear.Rows.Count > 0) c.Sections.Add(gear);

                // THE COST - kon i zbroja oddane przez gracza i jego druzyne (sloty puste po szrankach)
                if (t.LostN > 0)
                {
                    var cost = new Section { Title = "The cost" };
                    cost.Rows.Add(new Row { Label = "Horse and armour yielded: " + Pieces(t.LostN), Value = "worth " + N(t.LostW) });
                    if (t.LostNames.Count > 0)
                        cost.Rows.Add(new Row { Label = Cut(string.Join(", ", t.LostNames.ToArray()) + (t.LostN > t.LostNames.Count ? " and " + N(t.LostN - t.LostNames.Count) + " more" : "")), Value = "" });
                    c.Sections.Add(cost);
                }

                // RENOWN
                long ren = (long)Math.Round(r.Renown), inf = (long)Math.Round(r.Influence);
                if (ren != 0 || inf != 0)
                {
                    var rn = new Section { Title = "Renown" };
                    rn.Rows.Add(new Row { Label = (ren >= 0 ? "+" : "") + N(ren) + " renown" + (inf != 0 ? ", " + (inf >= 0 ? "+" : "") + N(inf) + " influence" : ""), Value = "" });
                    c.Sections.Add(rn);
                }

                // FINEST SPOILS - do 5 sztuk wedlug wartosci sztuki (jak po bitwie)
                all.Sort(delegate (SpoilsReport.Gain a, SpoilsReport.Gain b) { return b.E.ItemValue.CompareTo(a.E.ItemValue); });
                for (int i = 0; i < all.Count && c.Finest.Count < 5; i++)
                {
                    var g = all[i];
                    if (g.E.Item == null || g.E.ItemValue <= 0) continue;
                    c.Finest.Add(new Fine { E = g.E, N = g.N, Text = ElName(g.E) + (g.N > 1 ? " x" + N(g.N) : "") + " - worth " + N(g.E.ItemValue) + (g.N > 1 ? " each" : "") });
                }

                c.Total = "In all: " + (r.CoinTotal >= 0 ? Den(r.CoinTotal) + " to your purse" : N(-r.CoinTotal) + " denars out of your purse")
                          + (r.GearWorth > 0 ? ", " + N(r.GearWorth) + " in gear won" : "") + (t.LostW > 0 ? ", " + N(t.LostW) + " in gear yielded" : "") + ".";
            }
            catch (Exception e) { Stumble("BuildTourney", e); }
            return c;
        }
    }

    // ------------------------------------------------------------ ViewModele okna (wiazanie Gauntlet przez odbicie - wlasciwosci publiczne)

    internal sealed class SpoilsWindowVM : ViewModel
    {
        private readonly MBBindingList<SpoilsSectionVM> _sections = new MBBindingList<SpoilsSectionVM>();
        private readonly MBBindingList<SpoilsItemVM> _finest = new MBBindingList<SpoilsItemVM>();
        private readonly string _title, _header, _bg, _place, _total;

        internal SpoilsWindowVM(SpoilsWindow.Content c, string header, string background)
        {
            _title = c.Title ?? ""; _header = header ?? ""; _bg = background ?? ""; _place = c.Place ?? ""; _total = c.Total ?? "";
            foreach (var s in c.Sections) _sections.Add(new SpoilsSectionVM(s));
            foreach (var f in c.Finest) _finest.Add(new SpoilsItemVM(f));
        }

        [DataSourceProperty] public string Title { get { return _title; } }
        [DataSourceProperty] public string HeaderImage { get { return _header; } }
        [DataSourceProperty] public string BackgroundImage { get { return _bg; } }
        [DataSourceProperty] public string PlaceText { get { return _place; } }
        [DataSourceProperty] public string TotalText { get { return _total; } }
        [DataSourceProperty] public string DoneText { get { return "Done"; } }
        [DataSourceProperty] public string FinestTitle { get { return "Finest spoils"; } }
        [DataSourceProperty] public bool HasFinest { get { return _finest.Count > 0; } }
        [DataSourceProperty] public MBBindingList<SpoilsSectionVM> Sections { get { return _sections; } }
        [DataSourceProperty] public MBBindingList<SpoilsItemVM> Finest { get { return _finest; } }

        public void ExecuteDone() { SpoilsWindow.Close("przycisk Done"); }

        public void ExecuteCancel() { SpoilsWindow.Close("przycisk Cancel"); }

        public override void OnFinalize()
        {
            base.OnFinalize();
            foreach (var f in _finest) { try { f.OnFinalize(); } catch { } }
        }
    }

    internal sealed class SpoilsSectionVM : ViewModel
    {
        private readonly MBBindingList<SpoilsRowVM> _rows = new MBBindingList<SpoilsRowVM>();
        private readonly string _title;

        internal SpoilsSectionVM(SpoilsWindow.Section s)
        {
            _title = s.Title ?? "";
            foreach (var r in s.Rows) _rows.Add(new SpoilsRowVM(r.Label, r.Value));
        }

        [DataSourceProperty] public string Title { get { return _title; } }
        [DataSourceProperty] public MBBindingList<SpoilsRowVM> Rows { get { return _rows; } }
    }

    internal sealed class SpoilsRowVM : ViewModel
    {
        private readonly string _label, _value;
        internal SpoilsRowVM(string label, string value) { _label = label ?? ""; _value = value ?? ""; }
        [DataSourceProperty] public string Label { get { return _label; } }
        [DataSourceProperty] public string Value { get { return _value; } }
    }

    // recenzja P5 (5): "Finest spoils" w v1 tylko tekstem (nazwa i wartosc) - ikony przedmiotow (ItemImageIdentifierVM) odlozone do v1.1 (krytyk ZAKRES v1)
    internal sealed class SpoilsItemVM : ViewModel
    {
        private readonly string _text;

        internal SpoilsItemVM(SpoilsWindow.Fine f) { _text = f.Text ?? ""; }

        [DataSourceProperty] public string Text { get { return _text; } }
    }
}
