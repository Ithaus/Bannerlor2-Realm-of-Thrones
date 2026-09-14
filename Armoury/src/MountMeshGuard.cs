using System;
using HarmonyLib;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// STRAZNIK SIATKI WIERZCHOWCA (Jeff 14.09, crash 11:17:58): natywny
    /// AccessViolation w TaleWorlds.MountAndBlade.View.MountVisualCreator.
    /// AddMountMesh(agentVisual, mountItem, harnessItem, ...) przy
    /// rozstawianiu bitwy. Silnik buduje siatke uprzezy na szkielecie
    /// wierzchowca - uprzaz konska (family 1) na wielbladzie (2) albo sloniu
    /// (10) to czytanie cudzej pamieci, nie wyjatek .NET, wiec zadne catch
    /// tego nie lapie. Tu, na granicy silnika, pilnujemy jednego: uprzaz
    /// musi byc z rodziny wierzchowca, inaczej jedzie BEZ uprzezy.
    /// Patch po nazwie typu (AccessTools.TypeByName) - zero referencji do
    /// View.dll w projekcie; argumenty pozycyjne __1/__2, bo nazwy
    /// parametrow silnika sa poza nasza kontrola.
    /// </summary>
    internal static class MountMeshGuard
    {
        private static int _logged;

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var t = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.MountVisualCreator");
                var m = t != null ? AccessTools.Method(t, "AddMountMesh") : null;
                if (m == null) { Log.Info("MountMeshGuard: MountVisualCreator.AddMountMesh nieznaleziony - straznik spi."); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(MountMeshGuard), "Guard") { priority = Priority.High });
                Log.Info("MountMeshGuard: uprzaz spoza rodziny wierzchowca schodzi przed budowa siatki (koniec AccessViolation w AddMountMesh).");
            }
            catch (Exception e) { Log.Error("MountMeshGuard.ApplyAll", e); }
        }

        public static void Guard(ItemObject __1, ref ItemObject __2)
        {
            try
            {
                var mount = __1;
                var harness = __2;
                if (harness == null) return;
                if (mount == null) { __2 = null; return; }
                var hc = mount.HorseComponent;
                var ac = harness.ArmorComponent;
                if (hc == null || hc.Monster == null || ac == null) return;
                int famM = hc.Monster.FamilyType, famH = ac.FamilyType;
                if (famM == famH) return;
                __2 = null;
                if (_logged++ < 20)
                    Log.Info("MountMeshGuard: " + harness.StringId + " (rodzina " + famH + ") nie pasuje do "
                             + mount.StringId + " (rodzina " + famM + ") - jedzie bez uprzezy.");
            }
            catch { }
        }
    }
}
