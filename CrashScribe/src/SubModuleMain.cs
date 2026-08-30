using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CrashScribe
{
    public class SubModuleMain : MBSubModuleBase
    {
        private const string HarmonyId = "com.jeff.crashscribe";
        private static bool _init;
        private static bool _netCast;
        private static Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            if (_init) return;
            _init = true;
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var moduleRoot = Path.GetFullPath(Path.Combine(dir, "..", ".."));
                Config.Load(Path.Combine(moduleRoot, "ModuleData"));
                Blame.Map();
                Scribe.Init();
                Trail.Init(Scribe.ReportDir);
                Scribe.Line("Scribe ready. Reports: " + Scribe.ReportDir);
                Watchdog.Init(Thread.CurrentThread);

                if (Config.CatchUnhandled)
                {
                    AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                    {
                        Scribe.Report("UNHANDLED ERROR (the game is about to die)", e.ExceptionObject as Exception,
                            "AppDomain.UnhandledException", "IsTerminating=" + e.IsTerminating);
                        Trail.Drop("DEATH", e.ExceptionObject != null ? e.ExceptionObject.GetType().Name : "?");
                        Scribe.Summary();
                    };
                    TaskScheduler.UnobservedTaskException += (s, e) =>
                    {
                        Scribe.Report("ERROR IN A BACKGROUND TASK", e.Exception, "TaskScheduler.UnobservedTaskException", null);
                        try { e.SetObserved(); } catch { }
                    };
                    AppDomain.CurrentDomain.ProcessExit += (s, e) => { Scribe.Summary(); Trail.Close(); };
                }

                if (Config.CatchFirstChance)
                {
                    AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
                    {
                        try
                        {
                            if (e.Exception == null) return;
                            if (Config.OnlyModdedFirstChance && Blame.Culprits(e.Exception).Count == 0) return;
                            Scribe.Report("ERROR CAUGHT FURTHER UP", e.Exception, "FirstChanceException", null);
                        }
                        catch { }
                    };
                }

                _harmony = new Harmony(HarmonyId);
                Net.WrapDebugChannel(_harmony);
                Scribe.Line("Hooked into the game's own error channel.");
            }
            catch (Exception e)
            {
                try { Scribe.Report("CrashScribe", e, "OnSubModuleLoad", null); } catch { }
            }
        }

        /// <summary>Siec zakladamy dopiero gdy wszystkie mody sa zaladowane.</summary>
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (_netCast) return;
            _netCast = true;
            try
            {
                if (_harmony == null) _harmony = new Harmony(HarmonyId);
                Watch.Install(_harmony);
                Net.WrapModules(_harmony);
                Net.WrapHotspots(_harmony);
                Quiet.Install(_harmony);
                Mends.Install(_harmony);
                Fabula.Install(_harmony);
                Scribe.Line("Net ready.");
                Watchdog.Start();
                // Sampler WYLACZONY 29.08: Suspend+StackTrace co 0.5 s na FF potrafi
                // zakleszczyc watek glowny zlapany w srodku locka (freeze Jeffa 08:06,
                // profil pokazywal 17% Monitor.Enter). Dane zebrane: FF muli od
                // Campaign.RealTick (silnik), nie od modow. Straznik zawieszen zostaje
                // (robi Suspend rzadko, tylko przy realnym hangu).
                // Sampler.Start(System.Threading.Thread.CurrentThread);
                if (Config.ShowInGame)
                    InformationManager.DisplayMessage(new InformationMessage(
                        "CrashScribe is watching. Reports: Documents\\Mount and Blade II Bannerlord\\CrashScribe", Colors.Cyan));
            }
            catch (Exception e)
            {
                try { Scribe.Report("CrashScribe", e, "OnBeforeInitialModuleScreenSetAsRoot", null); } catch { }
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            Watchdog.Beat();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try { Scribe.Line("=== New game / save loaded: " + game.GameType.GetType().Name + " ==="); Trail.Drop("game", "start " + game.GameType.GetType().Name); }
            catch { }
            try
            {
                var cgs = gameStarterObject as TaleWorlds.CampaignSystem.CampaignGameStarter;
                if (cgs != null) cgs.AddBehavior(new WarReportBehavior());
                if (cgs != null) cgs.AddBehavior(new MendsBehavior());
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "OnGameStart.WarReport", null); } catch { } }
        }

        protected override void OnSubModuleUnloaded()
        {
            try { Scribe.Summary(); Trail.Close(); } catch { }
            base.OnSubModuleUnloaded();
        }
    }
}
