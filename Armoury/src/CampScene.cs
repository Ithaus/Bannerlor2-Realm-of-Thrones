using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// BITWA W OBOZIE (E7/O2, Jeff 09.10 "wlaczyc"). Jeff: "jak zaatakuja oboz,
    /// to walka ma byc w obozie, a nie na pustym polu". Pelna scene obozu robi
    /// tylko Honjin/Homesteads (wlasne assety, inna wersja gry) - my idziemy
    /// ich sprawdzonym mechanizmem: GameEntity.Instantiate na ZWYKLEJ scenie
    /// bitwy stawia scenografie obozu za linia gracza (namioty, ognisko,
    /// pochodnie, plot). Dziala tylko, gdy gracz stal obozem w chwili napasci
    /// (NightRest.PlayerCamped) i tylko w bitwie polowej.
    /// E7/O2 (v2, krytyka O2-fizyka): scenografia BEZ FIZYKI (navmesh sie nie zmienia - z fizyka
    /// AI grzezloby w namiotach i plocie; bez niej ludzie i strzaly przenikaja obiekty, tylko wyglad),
    /// nic blizej niz 6 m od gracza (ognisko dawniej stalo NA graczu), kierunki wzgledem frontu
    /// (do srodka wroga), stawiane po rozstawieniu wojska (koniec fazy Deployment).
    /// Kandydaci prefabow sprawdzeni w Prefabs/*.xml modulu Native 1.4.8 (dawne nazwy namiotow, campfire_a,
    /// bonfire_a i torch_a nie istnieja) i raz na proces przez GameEntity.PrefabExists - brakujace pomijane z logiem.
    /// </summary>
    internal sealed class CampScene : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private bool _done;

        // kandydaci per element - pierwszy istniejacy wygrywa (wszystkie z Native/Prefabs: archhitecture_*.xml, lighting.xml, props_archhitectural.xml)
        private static readonly string[] TentPrefabs = { "tent_vlandia_c", "tent_vlandia_a", "tent_vlandia_b", "tent_aserai_a", "khuzait_tent_a" };
        private static readonly string[] FirePrefabs = { "fire_stones_bonfire", "burning_campfire", "campfire" };
        private static readonly string[] TorchPrefabs = { "torch_long_d2", "torch" };
        private static readonly string[] FencePrefabs = { "barn_fence_a", "barn_fence_b" };

        // E7/O2: wynik PrefabExists raz na proces (null = brak prefabu danego elementu)
        private static bool _checked;
        private static string _tent, _fire, _torch, _fence;
        private static int _nTent, _nFire, _nTorch, _nFence, _checkErrors;

        // E7/O2: odleglosci od gracza (m) - najblizej pochodnie 6 m (projekt: nic w promieniu 3 m)
        private const float FireDist = 8f, TentMidDist = 13f, TentSideDist = 11f, TorchDist = 6f, FenceDist = 14f;

        /// <summary>E7/O2: lista kandydatow sprawdzona RAZ przez GameEntity.PrefabExists (tylko nazwy - zadnego odczytu z istniejacych encji).</summary>
        private static void EnsurePrefabs()
        {
            if (_checked) return;
            _checked = true;
            _tent = First(TentPrefabs, out _nTent);
            _fire = First(FirePrefabs, out _nFire);
            _torch = First(TorchPrefabs, out _nTorch);
            _fence = First(FencePrefabs, out _nFence);
        }

        private static string First(string[] candidates, out int found)
        {
            string first = null;
            found = 0;
            foreach (var name in candidates)
            {
                bool ok = false;
                try { ok = GameEntity.PrefabExists(name); }
                catch (Exception e) { if (_checkErrors++ == 0) Log.Error("CampScene.PrefabExists(" + name + ")", e); }   // jedna nazwa = pominieta, reszta dalej
                if (!ok) { Log.Info("CampScene: brak prefabu " + name); continue; }
                found++;
                if (first == null) first = name;
            }
            return first;
        }

        /// <summary>E7/O2: linia startowa (OnSessionLaunched) - tylko przy wlaczonym kluczu.</summary>
        internal static void StartLine()
        {
            var s = Settings.Current;
            if (s == null || !s.CampBattlePropsEnabled) return;
            EnsurePrefabs();
            Log.Info("CampScene (E7/O2): prefaby dostepne - namioty " + _nTent + "/" + TentPrefabs.Length + ", ogniska " + _nFire + "/" + FirePrefabs.Length
                     + ", pochodnie " + _nTorch + "/" + TorchPrefabs.Length + ", plot " + _nFence + "/" + FencePrefabs.Length + " (bez fizyki)"
                     + " | uzyte: " + (_tent ?? "-") + ", " + (_fire ?? "-") + ", " + (_torch ?? "-") + ", " + (_fence ?? "-")
                     + (_checkErrors > 0 ? " | bledy PrefabExists " + _checkErrors : ""));
        }

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
                if (m.Mode == MissionMode.Deployment) return;     // E7/O2: czekamy na koniec rozstawienia - gracz moze jeszcze przesunac szyk
                var main = Agent.Main;
                if (main == null) return;                         // czekamy, az gracz stanie na polu
                _done = true;
                Build(m, main);
            }
            catch (Exception e) { _done = true; Log.Error("CampScene", e); }
        }

        private static void Build(Mission m, Agent main)
        {
            EnsurePrefabs();
            Vec3 center = main.Position;
            string src;
            float front = FrontYaw(m, main, center, out src);
            float back = front + (float)Math.PI;
            int placed = 0, failed = 0;
            // namioty polkolem ZA graczem (przod zostaje wolny na walke): boczne 135/225 stopni od frontu 11 m, srodkowy 180 stopni 13 m
            for (int i = 0; i < 3; i++)
            {
                float ang = front + (float)(Math.PI * (0.75 + 0.25 * i));        // 135-180-225 stopni od frontu
                placed += TrySpawn(m, _tent, Ring(m, center, ang, i == 1 ? TentMidDist : TentSideDist), ang + (float)Math.PI, ref failed);
            }
            placed += TrySpawn(m, _fire, Ring(m, center, back, FireDist), back, ref failed);   // ognisko 8 m za graczem (dawniej na jego pozycji)
            for (int i = 0; i < 4; i++)
            {
                float ang = front + (float)(Math.PI * 0.5 * i);
                placed += TrySpawn(m, _torch, Ring(m, center, ang, TorchDist), ang, ref failed);
            }
            for (int i = 0; i < 2; i++)
            {
                float ang = front + (float)(Math.PI * (0.9 + 0.2 * i));
                placed += TrySpawn(m, _fence, Ring(m, center, ang, FenceDist), ang + (float)(Math.PI * 0.5), ref failed);
            }
            Log.Info("CampScene: postawiono " + placed + " elementow obozu na polu bitwy (E7/O2: bez fizyki, za linia gracza - front: " + src
                     + ", najblizej " + TorchDist.ToString("0") + " m od gracza" + (failed > 0 ? ", nieudanych " + failed : "") + ").");
        }

        /// <summary>E7/O2: kierunek frontu (radiany, 0 = +X) - do srodka wojska wroga; bez wroga wzrok gracza, potem os X jak dawniej.</summary>
        private static float FrontYaw(Mission m, Agent main, Vec3 center, out string src)
        {
            try
            {
                var enemy = m.PlayerEnemyTeam;
                if (enemy != null)
                {
                    float sx = 0f, sy = 0f;
                    int n = 0;
                    foreach (var a in enemy.ActiveAgents)
                    {
                        if (a == null) continue;
                        var p = a.Position;
                        sx += p.x; sy += p.y; n++;
                    }
                    if (n > 0)
                    {
                        float dx = sx / n - center.x, dy = sy / n - center.y;
                        if (dx * dx + dy * dy > 1f) { src = "wrog"; return (float)Math.Atan2(dy, dx); }
                    }
                }
            }
            catch { }
            try
            {
                var l = main.LookDirection;
                if (l.x * l.x + l.y * l.y > 0.01f) { src = "wzrok"; return (float)Math.Atan2(l.y, l.x); }
            }
            catch { }
            src = "os X";
            return 0f;
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

        /// <summary>E7/O2: prefab sprawdzony przez PrefabExists, BEZ FIZYKI (createPhysics: false), z ramka od poczatku
        /// (skrypty prefabu - dzwiek, swiatlo - startuja juz na miejscu). Wywrotka jednego obiektu = ten obiekt pominiety.</summary>
        private static int TrySpawn(Mission m, string name, Vec3 pos, float yaw, ref int failed)
        {
            if (name == null) return 0;                           // brak prefabu - w linii startowej
            try
            {
                var frame = MatrixFrame.Identity;
                frame.rotation.RotateAboutUp(yaw);
                frame.origin = pos;
                var ent = GameEntity.InstantiateWithRestOffset(m.Scene, name, false, frame, 0f);
                if (ent == null) { failed++; Log.Info("CampScene: prefab " + name + " nie powstal"); return 0; }
                ent.SetLocalPosition(pos);
                return 1;
            }
            catch (Exception e)
            {
                if (failed++ == 0) Log.Error("CampScene.Spawn(" + name + ")", e);
                return 0;
            }
        }
    }
}
