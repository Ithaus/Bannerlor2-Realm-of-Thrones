using System;
using Helpers;
using SandBox.View.Map;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m105-kolko (Jeff 21.10: "jak najezdzam na obiekt gdzie mozna wejsc pojawia sie takie kolko - dodaj do kazdego obiektu gdzie mozna wjechac, zajazdy, wioski
    /// poboczne"; "i zlotawe przy neutralnych"). Gra rysuje kolko najechania (MapScreen.TickCircles, decal "map_circle_decal") tylko dla MapEntityVisual&lt;PartyBase&gt;
    /// (osady i oddzialy gry). Zajazdy (RoadsideInnsView) i wioski poboczne (MapVillagesView) to nasze wlasne MapEntityVisual - stad brak kolka.
    /// Tu jedno wlasne kolko: ten sam material, argument (0.166, 1, 0.83, 0) i kolory co kolko gry nad wsia (MapScreen.GetDecalColorForParty: wrog czerwony,
    /// swoi zielony, sojusznik niebieski, reszta zlota). Pokaz w OnHover, schowaj w OnHoverEnd albo gdy dymek mapy przeszedl na inny obiekt
    /// (Tick z OnVisualTick widokow co klatke - samo porownanie odwolan). Jedna encja na scene mapy (powstaje przy pierwszym najechaniu, zdejmowana w OnFinalize
    /// widoku). Nic do zapisu gry. Klucz MCM MapHoverRingOurObjects.
    /// Autotest: DLL autotestu CrashScribe (LogGate.Mode 1/2) + plik CrashScribe\armoury-kolko.flag - widok zajazdow co 20 s pokazuje kolko przy najblizszym
    /// widocznym zajezdzie albo wiosce pobocznej na 8 s (log "Kolko najechania (m105): test"); progi sprawdz_logi --grupa m105kolko.
    /// </summary>
    internal static class MapHoverRing
    {
        internal const string AutoFlagName = "armoury-kolko.flag";
        internal const float InnScale = 1.25f;          // zajazd: mniejszy od wsi gry (1.75 w SettlementVisual bez znacznika kola)
        internal const float SideVillageMin = 1.1f, SideVillageMax = 1.75f;   // wioska poboczna: wedlug obrysu, nigdy wieksza od wsi gry

        private const uint ColorWar = 4292093218u, ColorSame = 4284183827u, ColorAlly = 4279386828u, ColorNeutral = 4291596077u;   // MapScreen.GetDecalColorForParty

        private static GameEntity _e;
        private static Decal _d;
        private static Scene _scene;
        private static MapEntityVisual _owner;
        private static bool _test;
        private static float _testLeft;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.MapHoverRingOurObjects; }
        }

        /// <summary>Kolor jak u gry wedlug frakcji wsi (wioska poboczna: wies okregu; zajazd: wies, na ktorej ziemi stoi). Brak wsi = zloty.</summary>
        internal static uint ColorFor(Settlement s)
        {
            try
            {
                IFaction f = s != null ? s.MapFaction : null;
                IFaction me = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
                if (f == null || me == null) return ColorNeutral;
                if (FactionManager.IsAtWarAgainstFaction(f, me)) return ColorWar;
                if (DiplomacyHelper.IsSameFactionAndNotEliminated(f, me)) return ColorSame;
                if (DiplomacyHelper.HasAllianceWithFaction(f, me)) return ColorAlly;
            }
            catch { }
            return ColorNeutral;
        }

        internal static void Show(MapEntityVisual owner, float x, float y, float scale, Settlement colorOf, bool test = false)
        {
            if (owner == null || (!On && !test)) return;
            try
            {
                MapScreen screen = MapScreen.Instance;
                Scene scene = screen != null ? screen.MapScene : null;
                if (scene == null) return;
                if (_e == null || !ReferenceEquals(_scene, scene))
                {
                    var de = MapScreen.DecalEntity.Create(scene, "map_circle_decal", "arm_hover_ring");   // ta sama droga co kolko gry (MapScreen :2216)
                    _e = de.GameEntity; _d = de.Decal; _scene = scene;
                    HoverRingStats.Made++;
                    Log.Info("Kolko najechania (m105): encja kola na scenie mapy postawiona (" + HoverRingStats.Made + ". raz od uruchomienia gry).");
                }
                MatrixFrame fr = MatrixFrame.Identity;
                fr.origin = new Vec3(x, y, scene.GetTerrainHeight(new Vec2(x, y), true));
                fr.rotation.ApplyScaleLocal(scale);
                _d.SetFactor1Linear(ColorFor(colorOf));
                _d.SetVectorArgument(0.166f, 1f, 0.83f, 0f);   // jak kolko gry nad wsia (MapScreen :3008)
                _e.SetGlobalFrame(in fr, true);
                _e.SetVisibilityExcludeParents(true);
                _owner = owner;
                _test = test;
                _testLeft = test ? 8f : 0f;
                HoverRingStats.Shows++;
                if (test) HoverRingStats.TestShows++;
            }
            catch (Exception e)
            {
                if (test) HoverRingStats.TestFails++;
                Stumble("Show", e);
            }
        }

        internal static void Hide(MapEntityVisual owner)
        {
            if (owner != null && !ReferenceEquals(owner, _owner)) return;
            HideNow();
        }

        private static void HideNow()
        {
            _owner = null;
            _test = false;
            if (_e == null) return;
            try { _e.SetVisibilityExcludeParents(false); }
            catch (Exception e) { Stumble("Hide", e); }
        }

        /// <summary>Co klatke z OnVisualTick widokow (przed ich licznikiem cwierc sekundy): dymek na innym obiekcie albo obiekt schowany - kolko znika.</summary>
        internal static void Tick(float realDt)
        {
            if (_owner == null) return;
            try
            {
                if (_test)
                {
                    _testLeft -= realDt;
                    if (_testLeft <= 0f) HideNow();
                    return;
                }
                MapScreen screen = MapScreen.Instance;
                if (!On || screen == null || !ReferenceEquals(screen.CurrentVisualOfTooltip, _owner) || !_owner.IsVisibleOrFadingOut()) HideNow();
            }
            catch (Exception e) { Stumble("Tick", e); HideNow(); }
        }

        /// <summary>Z OnFinalize widoku: encja tylko na zywej scenie tej samej mapy (inaczej same odwolania).</summary>
        internal static void Release(Scene sceneOfView, bool sceneAlive)
        {
            if (_e == null) { _owner = null; return; }
            if (sceneOfView != null && !ReferenceEquals(sceneOfView, _scene)) return;
            try { if (sceneAlive) _e.Remove(112); }
            catch (Exception e) { Stumble("Release", e); }
            _e = null; _d = null; _scene = null; _owner = null; _test = false;
        }

        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
                return System.IO.File.Exists(p);
            }
            catch { return false; }
        }

        private static void Stumble(string where, Exception e)
        {
            int n = ++HoverRingStats.St;
            if (n <= 3 || n % 200 == 0) Log.Error("MapHoverRing." + where + " (potkniecie " + n + ")", e);
        }
    }

    /// <summary>Liczniki kola bez typow SandBox.View / Engine (linia doby zajazdow czyta je takze, gdy typ widoku sie nie zaladowal - jak InnViewStats).</summary>
    internal static class HoverRingStats
    {
        internal static int Shows, Made, St, TestShows, TestFails;

        internal static string Text()
        {
            return "kolko najechania: pokazane " + Shows + " razy od uruchomienia gry, encji " + Made + ", potkniecia " + St
                   + (TestShows + TestFails > 0 ? ", test " + TestShows + " TAK / " + TestFails + " NIE" : "");
        }
    }
}
