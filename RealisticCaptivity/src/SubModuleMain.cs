using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RealisticCaptivity
{
    public class SubModuleMain : MBSubModuleBase
    {
        private const string HarmonyId = "com.jeff.realisticcaptivity";
        private static bool _patched;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // bin/Win64_Shipping_Client
                var moduleRoot = Path.GetFullPath(Path.Combine(dir, "..", ".."));
                Log.Init(moduleRoot);
                Settings.Load(Path.Combine(moduleRoot, "ModuleData"));
                Log.Info("Wczytano ustawienia. StripEquipment=" + Settings.Current.StripEquipment
                         + " MinDaysBeforeEscape=" + Settings.Current.MinDaysBeforeEscape
                         + " RansomMultiplier=" + Settings.Current.RansomMultiplier);

                if (!_patched)
                {
                    var harmony = new Harmony(HarmonyId);
                    harmony.PatchAll(Assembly.GetExecutingAssembly());
                    _patched = true;
                    Log.Info("Harmony: patche zaaplikowane.");
                    EnlistedWounds.Install(harmony);
                    MountedGetaway.ApplyAll(harmony);
                    FairRansomPatch.ApplyAll(harmony);

                    // zakladka Klan -> Other: grupa "Houses" (mixin + prefab inserty)
                    var extender = Bannerlord.UIExtenderEx.UIExtender.Create("RealisticCaptivity");
                    extender.Register(Assembly.GetExecutingAssembly());
                    extender.Enable();
                    Log.Info("UIExtenderEx: grupa Houses w klanie zarejestrowana.");
                }
            }
            catch (Exception e) { Log.Error("OnSubModuleLoad", e); }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try
            {
                var starter = gameStarterObject as CampaignGameStarter;
                if (starter == null || !(game.GameType is Campaign)) return;
                McmSettings.Apply();
                HorseFlightModel.Install(gameStarterObject);
                starter.AddBehavior(new CaptivityBehavior());
                Log.Info("Behavior dodany do kampanii.");
            }
            catch (Exception e) { Log.Error("OnGameStart", e); }
        }
    }
}
