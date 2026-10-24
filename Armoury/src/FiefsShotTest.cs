using System;
using System.Globalization;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// AUTOTEST ZAKLADKI FIEFS (m112-inwest, Jeff 24.10: "ma byc to pokazane w Fiefs, zeby widzial, gdzie mam majatek i inwestycje") - tylko pod plikiem
    /// CrashScribe\armoury-fiefs-test.flag i DLL autotestu (LogGate 1/2); w zwyklej grze nic nie robi. Na mapie, po 60 s czasu mapy od wczytania (prace okregu
    /// wczytuja sie w pierwszej dobie), otwiera ekran rodu, wybiera zakladke Fiefs (ClanManagementVM.SetSelectedCategory(2) odbiciem), robi zdjecie silnika
    /// i zamyka ekran. Linie "Fiefs (test m112): ...".
    /// </summary>
    internal static class FiefsShotTest
    {
        private const string P = "Fiefs (test m112): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _want = -1, _stage;
        private static float _clock, _acc, _at, _ready;
        private static bool _done;

        private static string Dir()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe");
        }

        private static bool Wanted()
        {
            if (_want >= 0) return _want == 1;
            _want = 0;
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try { if (File.Exists(Path.Combine(Dir(), "armoury-fiefs-test.flag"))) _want = 1; } catch { }
            return _want == 1;
        }

        /// <summary>SubModuleMain.OnApplicationTick: co 0,5 s; bez pliku flagi - od razu wyjscie.</summary>
        internal static void AppTick(float dt)
        {
            if (_done || !Wanted()) return;
            _clock += dt;
            _acc += dt;
            if (_acc < 0.5f) return;
            float step = _acc;
            _acc = 0f;
            try { Step(step); }
            catch (Exception e)
            {
                Log.Error("FiefsShotTest.Step", e);
                try { if (Game.Current != null && Game.Current.GameStateManager.ActiveState is ClanState) Game.Current.GameStateManager.PopState(); } catch { }
                Log.Info(P + "przerwany wyjatkiem " + e.GetType().Name + ".");
                _done = true;
            }
        }

        private static void Step(float step)
        {
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (_stage == 0)
            {
                if (Campaign.Current == null || gsm == null || Mission.Current != null || !(gsm.ActiveState is MapState) || InformationManager.IsAnyInquiryActive()) { return; }
                _ready += step;
                if (_ready < 60f) return;
                gsm.PushState(gsm.CreateState<ClanState>());
                _stage = 1; _at = _clock + 3f;
                Log.Info(P + "ekran rodu otwarty.");
                return;
            }
            if (_clock < _at) return;
            if (_stage == 1)
            {
                var scr = ScreenManager.TopScreen;
                var pi = scr != null ? scr.GetType().GetProperty("_dataSource") : null;
                var vm = pi != null ? pi.GetValue(scr, null) : null;
                var mi = vm != null ? vm.GetType().GetMethod("SetSelectedCategory", new[] { typeof(int) }) : null;
                if (mi != null) mi.Invoke(vm, new object[] { 2 });
                Log.Info(P + "zakladka Fiefs " + (mi != null ? "wybrana" : "NIE wybrana (ekran " + (scr != null ? scr.GetType().Name : "brak") + ")") + ".");
                _stage = 2; _at = _clock + 3f;
                return;
            }
            if (_stage == 2)
            {
                string dir = Path.Combine(Dir(), "zdjecia");
                Directory.CreateDirectory(dir);
                string name = "fiefs-m112-" + DateTime.Now.ToString("HHmmss", Inv) + ".png";
                TaleWorlds.Engine.Utilities.TakeScreenshot(Path.Combine(dir, name).Replace('\\', '/'));
                Log.Info(P + "zdjecie zdjecia\\" + name + ".");
                _stage = 3; _at = _clock + 1f;
                return;
            }
            if (gsm != null && gsm.ActiveState is ClanState) gsm.PopState();
            Log.Info(P + "koniec.");
            _done = true;
        }
    }
}
