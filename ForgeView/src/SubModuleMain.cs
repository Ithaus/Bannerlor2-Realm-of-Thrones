using System;
using System.Reflection;
using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ForgeView
{
    public class SubModuleMain : MBSubModuleBase
    {
        private static bool _done;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            if (_done) return;
            _done = true;
            try
            {
                Log.Init();
                var extender = UIExtender.Create("ForgeView");
                extender.Register(Assembly.GetExecutingAssembly());
                extender.Enable();
                Log.Info("UIExtenderEx: rozszerzenia zarejestrowane i wlaczone.");
                var harmony = new Harmony("com.jeff.forgeview");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Info("Harmony: laty na CraftingMixin zaaplikowane.");
                // znane wzory na gorze ekranu: postfiksy na przyciskach sortowania BK - wpinane
                // recznie (metody deklarowane), kazdy cel osobno, z linia w logu
                SortKnownFirst.Apply(harmony);
            }
            catch (Exception e) { Log.Error("OnSubModuleLoad", e); }
        }
    }
}
