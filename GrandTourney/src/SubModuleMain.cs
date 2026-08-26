using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GrandTourney
{
    public class SubModuleMain : MBSubModuleBase
    {
        private const string HarmonyId = "com.jeff.grandtourney";
        private static bool _patched;
        private static bool _modelsPatched;
        private static Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // bin/Win64_Shipping_Client
                var moduleRoot = Path.GetFullPath(Path.Combine(dir, "..", ".."));
                Log.Init(moduleRoot);
                Settings.Load(Path.Combine(moduleRoot, "ModuleData"));
                Log.Info("Ustawienia wczytane. MaxLordsInvited=" + Settings.Current.MaxLordsInvited
                         + " MinLordsToHold=" + Settings.Current.MinLordsToHold
                         + " GatherDays=" + Settings.Current.GatherDays);

                if (!_patched)
                {
                    _harmony = new Harmony(HarmonyId);
                    _harmony.PatchAll(Assembly.GetExecutingAssembly());
                    _patched = true;
                    Log.Info("Harmony: patche zaaplikowane.");
                }
            }
            catch (Exception e) { Log.Error("OnSubModuleLoad", e); }
        }

        /// <summary>
        /// Model turnieju lataamy dopiero tutaj - w OnSubModuleLoad inne mody moga jeszcze
        /// nie miec zaladowanych swoich typow, a chcemy zlapac kazda implementacje.
        /// </summary>
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            try
            {
                if (_modelsPatched) return;
                _modelsPatched = true;
                if (_harmony == null) _harmony = new Harmony(HarmonyId);
                NoEarlyEndPatch.ApplyAll(_harmony);
                ClosedListsPatch.ApplyAll(_harmony);
                NoblesFightPatch.ApplyAll(_harmony);
            }
            catch (Exception e) { Log.Error("OnBeforeInitialModuleScreenSetAsRoot", e); }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try
            {
                var starter = gameStarterObject as CampaignGameStarter;
                if (starter == null || !(game.GameType is Campaign)) return;
                McmSettings.Apply();
                starter.AddBehavior(new TourneyBehavior());
                Log.Info("Behavior dodany do kampanii.");
            }
            catch (Exception e) { Log.Error("OnGameStart", e); }
        }
    }
}
