using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// BITWA W OBOZIE (EKSPERYMENT, domyslnie OFF). Jeff: "jak zaatakuja oboz,
    /// to walka ma byc w obozie, a nie na pustym polu". Pelna scene obozu robi
    /// tylko Honjin/Homesteads (wlasne assety, inna wersja gry) - my idziemy
    /// ich sprawdzonym mechanizmem: GameEntity.Instantiate na ZWYKLEJ scenie
    /// bitwy stawia scenografie obozu wokol pozycji gracza (namioty, ognisko,
    /// pochodnie, plot). Dziala tylko, gdy gracz stal obozem w chwili napasci
    /// (NightRest.PlayerCamped) i tylko w bitwie polowej.
    /// Nazwy prefabow to KANDYDACI - kazdy nieistniejacy jest pomijany
    /// z wpisem w logu "CampScene: brak prefabu X"; po pierwszym tescie
    /// zostawimy tylko te, ktore ROT/vanilla naprawde ma.
    /// </summary>
    internal sealed class CampScene : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private bool _done;

        // kandydaci per elmenet - pierwszy istniejacy wygrywa
        private static readonly string[] TentPrefabs = { "aserai_tent", "tent_big_a", "khuzait_yurt_a", "empire_tent_a", "tent_a" };
        private static readonly string[] FirePrefabs = { "campfire_a", "campfire", "bonfire_a" };
        private static readonly string[] TorchPrefabs = { "torch_long_d2", "torch_a" };
        private static readonly string[] FencePrefabs = { "barn_fence_a", "barn_fence_b" };

        public override void OnMissionTick(float dt)
        {
            try
            {
                if (_done) return;
                var s = Settings.Current;
                var m = Mission.Current;
                if (s == null || m == null || !s.CampBattlePropsEnabled || !NightRest.PlayerCamped)
                { _done = true; return; }
                if (m.Scene == null) return;
                if (!m.IsFieldBattle) { _done = true; return; }   // oblezenia, kryjowki, areny - nie
                var main = Agent.Main;
                if (main == null) return;                         // czekamy, az gracz stanie na polu
                _done = true;
                Build(m, main.Position);
            }
            catch (Exception e) { _done = true; Log.Error("CampScene", e); }
        }

        private static void Build(Mission m, Vec3 center)
        {
            int placed = 0;
            // namioty polkolem ZA graczem (przod zostaje wolny na walke)
            for (int i = 0; i < 3; i++)
            {
                float ang = (float)(Math.PI * (0.75 + 0.25 * i));                // 135-180-225 stopni
                placed += TrySpawn(m, TentPrefabs, Ring(m, center, ang, 11f), ang + (float)Math.PI);
            }
            placed += TrySpawn(m, FirePrefabs, Ring(m, center, 0f, 0f), 0f);     // ognisko posrodku
            for (int i = 0; i < 4; i++)
            {
                float ang = (float)(Math.PI * 0.5 * i);
                placed += TrySpawn(m, TorchPrefabs, Ring(m, center, ang, 6f), ang);
            }
            for (int i = 0; i < 2; i++)
            {
                float ang = (float)(Math.PI * (0.9 + 0.2 * i));
                placed += TrySpawn(m, FencePrefabs, Ring(m, center, ang, 14f), ang + (float)(Math.PI * 0.5));
            }
            Log.Info("CampScene: postawiono " + placed + " elementow obozu na polu bitwy.");
        }

        /// <summary>Punkt na okregu wokol srodka, z wysokoscia sciagnieta z terenu.</summary>
        private static Vec3 Ring(Mission m, Vec3 center, float angle, float radius)
        {
            var p = new Vec3(center.x + (float)Math.Cos(angle) * radius,
                             center.y + (float)Math.Sin(angle) * radius, center.z);
            try
            {
                float h = 0f;
                m.Scene.GetTerrainHeightAndNormal(new Vec2(p.x, p.y), out h, out _);
                if (h > 0f || center.z < 1f) p.z = h;
            }
            catch { }
            return p;
        }

        /// <summary>Pierwszy istniejacy prefab z listy; nieznane pomijamy z logiem (raz na nazwe).</summary>
        private static readonly System.Collections.Generic.HashSet<string> _missing =
            new System.Collections.Generic.HashSet<string>();

        private static int TrySpawn(Mission m, string[] candidates, Vec3 pos, float yaw)
        {
            foreach (var name in candidates)
            {
                if (_missing.Contains(name)) continue;
                try
                {
                    var frame = MatrixFrame.Identity;
                    frame.rotation.RotateAboutUp(yaw);
                    frame.origin = pos;
                    var ent = GameEntity.Instantiate(m.Scene, name, frame);
                    if (ent == null) { _missing.Add(name); Log.Info("CampScene: brak prefabu " + name); continue; }
                    ent.SetLocalPosition(pos);
                    return 1;
                }
                catch
                {
                    _missing.Add(name);
                    Log.Info("CampScene: brak prefabu " + name);
                }
            }
            return 0;
        }
    }
}
