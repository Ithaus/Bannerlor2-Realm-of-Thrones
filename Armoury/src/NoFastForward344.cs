using System;
using System.IO;
using System.Threading;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 344 - BIEG AUTOTESTU BEZ PRZEWIJANIA (tylko DLL autotestu CrashScribe - LogGate.Mode 1 - i plik Documents\Mount and Blade II Bannerlord\CrashScribe\
    /// armoury-bez-przewijania344.flag). Powod (projekt 344 rozdz. 7): przy przewijaniu misji (Mission.SetFastForwardingFromUI(true) - autotest CrashScribe wlacza je
    /// co 3 s) gra liczy AI szykow w GLOWNYM watku, a w zwyklym tempie (gra Jeffa) w watku silnika - testy 333 przechodzily, gra Jeffa nigdy (prefiks tylko w glownym
    /// watku). Bieg bez przewijania jest warunkiem wgrania 344. Prefiks Mission.SetFastForwardingFromUI: przy pliku-fladze wlaczenie przewijania odrzucone (licznik),
    /// wylaczenie przepuszczone. Latka wpinana tylko w biegu autotestu (Doctrine.ApplyMissionPatches, pierwsza bitwa) - w grze Jeffa jej nie ma.
    /// Limit bitwy CrashScribe (-BattleSec) liczy sekundy rzeczywiste - przy biegu bez przewijania podniesc go w skrypcie testu.
    /// </summary>
    internal static class NoFastForward344
    {
        internal const string FlagName = "armoury-bez-przewijania344.flag";
        private static bool _tried, _flag;
        private static string _patch = "nie wpieta";
        private static int _blocked;

        internal static int Blocked { get { return Volatile.Read(ref _blocked); } }

        internal static string ApplyPatch(Harmony h)
        {
            if (_tried) return _patch;
            _tried = true;
            try
            {
                if (LogGate.Mode != 1) { _patch = "nie (gra, nie autotest)"; return _patch; }
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                _flag = File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), FlagName));
                if (!_flag) { _patch = "nie (bez pliku " + FlagName + ")"; return _patch; }
                var t = AccessTools.DeclaredMethod(typeof(Mission), nameof(Mission.SetFastForwardingFromUI), new[] { typeof(bool) });
                if (t == null) { _patch = "NIE (brak Mission.SetFastForwardingFromUI)"; return _patch; }
                h.Patch(t, prefix: new HarmonyMethod(typeof(NoFastForward344), nameof(Pre)));
                _patch = "TAK";
                Log.Info("AI bitwy (344) TEST bez przewijania: plik " + FlagName + " - przewijanie misji autotestu wylaczone (AI szykow w watku silnika jak w grze Jeffa).");
            }
            catch (Exception e)
            {
                _patch = "NIE (" + e.GetType().Name + ")";
                Log.Error("NoFastForward344.Latka", e);
            }
            return _patch;
        }

        public static bool Pre(Mission __instance, bool fastForwarding)
        {
            if (!fastForwarding || !_flag) return true;
            Interlocked.Increment(ref _blocked);
            return false;
        }
    }
}
