using System;
using System.Collections.Generic;
using System.Globalization;
using SandBox.View;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// p6-szubienice (R3-07a) - SZUBIENICE NA MAPIE: dwa slupy i belka (kopie siatki slupa pomostu "wooden_platform_plank_f_column" ze skala nierowna - dlugosc
    /// i grubosc w jedn. mapy), na belce 1-3 wisielcow (siatka ROT "burnt_corpse" - smolony trup jak na krzyzu Boltonow; zapas: worek) na cienkich linach.
    /// Ta sama droga co zajazdy (RoadsideInnsView): KOPIE SIATEK po nazwie (MetaMesh.GetCopy) w pustych encjach - bez fizyki, skryptow, czastek, dzwiekow, tagow
    /// i bez klikania. Obrazek powstaje RAZ (przy kamerze, najwyzej 6 na cwierc sekundy), potem tylko pokaz / schowaj przy zmianie stanu; zdjecie (Remove) tylko,
    /// gdy szubienica zniknie z listy Gallows (koniec czasu, zastapiona) albo zmieni liczbe wisielcow - lista sprawdzana po numerze wersji (nic co klatke, CLAUDE.md 7).
    /// Widoczna tylko, gdy wies okregu jest widoczna (mgla zwiadu jak zajazdy i wioski) i kamera nisko (MapVillagesHideAboveCameraHeight). Nic do zapisu gry
    /// (lista szubienic - Gallows, klucz arm_gallows). Potkniecia per szubienica (3 = ta jedna pominieta), nigdy cala warstwa.
    /// </summary>
    public sealed class GallowsView : EntityVisualManagerBase
    {
        public override int Priority => 102;

        private const float TickEvery = 0.25f;
        private const float ShowRadiusExtra = 120f;
        private const int CreatePerTick = 6;
        private const int StrikesPer = 3;
        // wymiary (jedn. mapy; zajazd ~0.8, wioska ~2): szubienica wyolbrzymiona jak ikony mapy, zeby byla widoczna z kamery
        private const float H = 0.40f, W = 0.36f, PostT = 0.028f, BeamT = 0.026f, RopeL = 0.045f, RopeT = 0.006f, BodyL = 0.15f, BodyW = 0.10f, Sink = 0.03f;
        private static readonly string[] WoodMeshes = { "wooden_platform_plank_f_column", "wooden_platform_plank_a" };
        private static readonly string[] BodyMeshes = { "burnt_corpse", "bd_sack_b", "mi_sack_a" };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly long CreateBudgetTicks = System.Diagnostics.Stopwatch.Frequency * 6 / 1000;

        private sealed class Slot
        {
            public Gallows.Entry E;
            public GameEntity Root;
            public bool Shown, Failed, NeedRes;
            public int Strikes, Bodies;
        }

        private MapScreen _screen;
        private bool _active, _finalized, _wasOff;
        private float _acc;
        private int _seenVersion = int.MinValue;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<Slot> _shown = new List<Slot>();
        private readonly Dictionary<string, MetaMesh> _meshes = new Dictionary<string, MetaMesh>(StringComparer.Ordinal);
        private readonly HashSet<string> _missLogged = new HashSet<string>(StringComparer.Ordinal);

        // ---------- rejestracja (z SubModuleMain.OnApplicationTick; tani test co klatke, jak RoadsideInnsView) ----------
        private static WeakReference _regVm, _regScreen;
        private static int _stRegister;

        internal static void EnsureRegistered()
        {
            try
            {
                if (!Settings.Current.MapGallows) return;
                if (Campaign.Current == null) return;
                MapScreen screen = MapScreen.Instance;
                if (screen == null) return;
                SandBoxViewVisualManager vm = SandBoxViewSubModule.SandBoxViewVisualManager;
                if (vm == null) return;
                if (_regVm != null && ReferenceEquals(_regVm.Target, vm) && _regScreen != null && ReferenceEquals(_regScreen.Target, screen)) return;
                if (vm.GetEntityComponent<SettlementVisualManager>() == null) return;
                var mine = vm.GetEntityComponent<GallowsView>();
                if (mine != null && !ReferenceEquals(mine._screen, screen)) { vm.RemoveEntityComponent(mine); mine = null; }
                if (mine == null) vm.AddEntityComponent<GallowsView>();
                _regVm = new WeakReference(vm);
                _regScreen = new WeakReference(screen);
            }
            catch (Exception e)
            {
                _stRegister++;
                if (_stRegister <= 3 || _stRegister % 500 == 0) Log.Error("GallowsView.EnsureRegistered (potkniecie " + _stRegister + ")", e);
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _screen = MapScreen.Instance;
            _active = true;
            _seenVersion = int.MinValue;
            try
            {
                Log.Info("Szubienice (p6): widok mapy - szubienic na liscie " + Gallows.Active.Count + "; obrazki powstaja raz przy kamerze (z <= "
                         + Settings.Current.MapVillagesHideAboveCameraHeight.ToString("0", Inv) + "), potem tylko pokaz / schowaj; zdjecie przy zmianie listy.");
            }
            catch (Exception e) { Log.Error("GallowsView.OnInitialize", e); }
        }

        protected override void OnFinalize()
        {
            if (_finalized) { base.OnFinalize(); return; }
            _finalized = true;
            int removed = 0;
            try
            {
                bool sceneAlive = _screen != null && ReferenceEquals(MapScreen.Instance, _screen);
                foreach (var s in _slots)
                {
                    if (s.Root != null && sceneAlive) { try { s.Root.Remove(111); removed++; } catch { } }
                    s.Root = null; s.Shown = false;
                }
                Log.Info("Szubienice (p6): koniec mapy - zdjete obrazki " + removed + (sceneAlive ? "" : " (scena juz nie ta - same odwolania)") + "; postawione od wczytania " + Gallows.VCreated + ".");
            }
            catch (Exception e) { Log.Error("GallowsView.OnFinalize", e); }
            _slots.Clear(); _shown.Clear(); _meshes.Clear();
            _active = false;
            _screen = null;
            base.OnFinalize();
        }

        public override void ClearVisualMemory()
        {
            foreach (var s in _shown) s.NeedRes = true;
        }

        public override void OnVisualTick(MapScreen screen, float realDt, float dt)
        {
            if (!_active || _finalized) return;
            _acc += realDt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            try
            {
                if (!Settings.Current.MapGallows)
                {
                    if (!_wasOff)
                    {
                        foreach (var s in _shown) { try { if (s.Root != null) s.Root.SetVisibilityExcludeParents(false); } catch { } s.Shown = false; }
                        _shown.Clear();
                        _wasOff = true;
                        Log.Info("Szubienice (p6): wylaczone w MCM - obrazki schowane.");
                    }
                    return;
                }
                _wasOff = false;
                if (Gallows.Version != _seenVersion) Sync();
                UpdateVisibility(screen);
            }
            catch (Exception e) { Gallows.VStVis++; if (Gallows.VStVis <= 3 || Gallows.VStVis % 200 == 0) Log.Error("GallowsView.OnVisualTick (potkniecie " + Gallows.VStVis + ")", e); }
        }

        /// <summary>Uzgodnienie z lista Gallows (tylko przy zmianie numeru wersji): zniknela - obrazek zdjety; inna liczba wisielcow - obrazek od nowa przy kamerze; nowa - slot.</summary>
        private void Sync()
        {
            _seenVersion = Gallows.Version;
            var live = new HashSet<Gallows.Entry>(Gallows.Active);
            var have = new HashSet<Gallows.Entry>();
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var s = _slots[i];
                bool gone = !live.Contains(s.E);
                if (gone || (s.Root != null && s.Bodies != s.E.Bodies))
                {
                    if (s.Root != null) { try { s.Root.Remove(111); Gallows.VRemoved++; } catch { } }
                    s.Root = null;
                    if (s.Shown) { s.Shown = false; _shown.Remove(s); }
                    if (gone) { _slots.RemoveAt(i); continue; }
                }
                have.Add(s.E);
            }
            foreach (var e in Gallows.Active) if (!have.Contains(e)) _slots.Add(new Slot { E = e });
        }

        private static bool Visible(Slot s)
        {
            var e = s.E;
            if (e.Village == null && !string.IsNullOrEmpty(e.District)) { try { e.Village = Settlement.Find(e.District); } catch { } }
            return e.Village == null || e.Village.IsVisible;
        }

        private void UpdateVisibility(MapScreen screen)
        {
            if (screen == null || screen.MapCameraView == null || screen.MapCameraView.Camera == null) return;
            Vec3 cam = screen.MapCameraView.Camera.Position;
            float hideZ = Settings.Current.MapVillagesHideAboveCameraHeight;
            if (hideZ <= 0f) hideZ = 160f;
            bool far = cam.z > hideZ;
            float r = cam.z + ShowRadiusExtra, r2 = r * r;
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                var s = _shown[i];
                try
                {
                    float dx = s.E.X - cam.x, dy = s.E.Y - cam.y;
                    if (far || dx * dx + dy * dy >= r2 || !Visible(s))
                    {
                        if (s.Root != null) s.Root.SetVisibilityExcludeParents(false);
                        s.Shown = false; _shown.RemoveAt(i); Gallows.VHideDay++;
                    }
                    else if (s.NeedRes && s.Root != null) { s.Root.CheckResources(true, false); s.NeedRes = false; }
                }
                catch (Exception e) { Strike(s, "widocznosc", e); s.Shown = false; _shown.RemoveAt(i); }
            }
            if (far) return;
            int made = 0;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (var s in _slots)
            {
                if (s.Shown || s.Failed) continue;
                float dx = s.E.X - cam.x, dy = s.E.Y - cam.y;
                if (dx * dx + dy * dy >= r2) continue;
                try
                {
                    if (!Visible(s)) continue;
                    if (s.Root == null)
                    {
                        if (made >= CreatePerTick || (made > 0 && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > CreateBudgetTicks)) continue;
                        Create(s);
                        made++;
                        if (s.Root == null) continue;
                    }
                    s.Root.SetVisibilityExcludeParents(true);   // tylko przy zmianie stanu
                    s.Root.CheckResources(true, false);
                    s.NeedRes = false;
                    s.Shown = true;
                    _shown.Add(s); Gallows.VShowDay++;
                }
                catch (Exception e) { Strike(s, "pokaz", e); if (s.Root != null) { try { s.Root.SetVisibilityExcludeParents(false); } catch { } } }
            }
            if (_shown.Count > Gallows.VShownMax) Gallows.VShownMax = _shown.Count;
        }

        private void Strike(Slot s, string where, Exception e)
        {
            s.Strikes++;
            if (where == "tworzenie") Gallows.VStCreate++; else Gallows.VStVis++;
            if (s.Strikes >= StrikesPer && !s.Failed) { s.Failed = true; Gallows.VFailed++; }
            int n = Gallows.VStCreate + Gallows.VStVis;
            if (n <= 3 || n % 200 == 0) Log.Error("GallowsView " + where + " " + s.E.District + " (potkniecie szubienicy " + s.Strikes + "/" + StrikesPer + ")", e);
        }

        // ---------- obrazek ----------
        private MetaMesh MeshOf(string[] names)
        {
            foreach (var name in names)
            {
                MetaMesh mm;
                if (!_meshes.TryGetValue(name, out mm))
                {
                    mm = MetaMesh.GetCopy(name, false, true);
                    if (mm != null && !mm.IsValid) mm = null;
                    if (mm != null && !HasMaterial(mm)) mm = null;
                    _meshes[name] = mm;
                    if (mm == null) { Gallows.VMeshMiss++; if (_missLogged.Add(name)) Log.Info("Szubienice (p6): brak siatki '" + name + "' w grze (albo bez materialu) - zapas z listy."); }
                }
                if (mm != null) return mm;
            }
            return null;
        }

        private static bool HasMaterial(MetaMesh mm)
        {
            try
            {
                for (int k = 0; k < mm.MeshCount; k++)
                {
                    var sm = mm.GetMeshAtIndex(k);
                    if (sm == null || !sm.IsValid) continue;
                    var mat = sm.GetMaterial();
                    if (mat != null && mat.IsValid && !string.IsNullOrEmpty(mat.Name)) return true;
                }
                return false;
            }
            catch { return true; }
        }

        private static float Ground(TaleWorlds.CampaignSystem.Map.IMapScene ms, float x, float y)
        {
            float z; Vec3 n;
            ms.GetTerrainHeightAndNormal(new Vec2(x, y), out z, out n);
            return z;
        }

        /// <summary>Obrot kopii siatki: najdluzsza os BB siatki -> os docelowa (vertical: lokalna Z, inaczej lokalna X korzenia); wiersze s, f, u = obrazy osi x, y, z siatki.</summary>
        private static Mat3 AxisTo(int longAxis, bool vertical)
        {
            if (vertical)
            {
                if (longAxis == 0) return new Mat3(new Vec3(0f, 0f, 1f), new Vec3(0f, 1f, 0f), new Vec3(-1f, 0f, 0f));
                if (longAxis == 1) return new Mat3(new Vec3(1f, 0f, 0f), new Vec3(0f, 0f, 1f), new Vec3(0f, -1f, 0f));
                return Mat3.Identity;
            }
            if (longAxis == 2) return new Mat3(new Vec3(0f, 0f, -1f), new Vec3(0f, 1f, 0f), new Vec3(1f, 0f, 0f));
            if (longAxis == 1) return new Mat3(new Vec3(0f, -1f, 0f), new Vec3(1f, 0f, 0f), new Vec3(0f, 0f, 1f));
            return Mat3.Identity;
        }

        /// <summary>Element-belka: siatka rozciagnieta tak, ze jej najdluzsza os ma dlugosc len wzdluz osi docelowej, a pozostale dwie - grubosc thick; srodek BB w c.</summary>
        private bool Stick(Scene scene, GameEntity root, MetaMesh mm, bool vertical, float len, float thick, Vec3 c)
        {
            BoundingBox bb = mm.GetBoundingBox();
            float ex = bb.max.x - bb.min.x, ey = bb.max.y - bb.min.y, ez = bb.max.z - bb.min.z;
            if (!(ex > 0.0005f) || !(ey > 0.0005f) || !(ez > 0.0005f) || ex > 1000f || ey > 1000f || ez > 1000f) return false;
            int la = ex >= ey && ex >= ez ? 0 : ey >= ez ? 1 : 2;
            Mat3 m = AxisTo(la, vertical);
            m.ApplyScaleLocal(new Vec3(la == 0 ? len / ex : thick / ex, la == 1 ? len / ey : thick / ey, la == 2 ? len / ez : thick / ez));
            return Piece(scene, root, mm, m, bb, c);
        }

        private bool Piece(Scene scene, GameEntity root, MetaMesh mm, Mat3 m, BoundingBox bb, Vec3 c)
        {
            var bc = new Vec3((bb.min.x + bb.max.x) * 0.5f, (bb.min.y + bb.max.y) * 0.5f, (bb.min.z + bb.max.z) * 0.5f);
            Vec3 off = m.TransformToParent(in bc);
            var f = new MatrixFrame(in m, new Vec3(c.x - off.x, c.y - off.y, c.z - off.z, 1f));
            GameEntity e = GameEntity.CreateEmpty(scene, false, false, false);
            if (e == (GameEntity)null) return false;
            MetaMesh copy = mm.CreateCopy();
            if (copy == null || !copy.IsValid) copy = MetaMesh.GetCopy(mm.GetName(), false, true);
            if (copy == null || !copy.IsValid) return false;
            e.AddMultiMesh(copy, true);
            e.SetFrame(ref f, true);
            root.AddChild(e, false);
            e.EntityFlags = e.EntityFlags | EntityFlags.DoNotTick;
            e.SetReadyToRender(true);
            return true;
        }

        /// <summary>Obrazek szubienicy (raz): korzen na najnizszym gruncie pod slupami (slupy wkopane o Sink), belka na szczycie, liny i wisielcy pod belka.</summary>
        private void Create(Slot s)
        {
            GameEntity root = null;
            try
            {
                Scene scene = MapScene;
                var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
                if (scene == null || ms == null) throw new InvalidOperationException("brak sceny mapy");
                var g = s.E;
                MetaMesh wood = MeshOf(WoodMeshes);
                if (wood == null) { s.Failed = true; Gallows.VFailed++; return; }
                MetaMesh body = MeshOf(BodyMeshes);
                float ca = (float)Math.Cos(g.Yaw), sa = (float)Math.Sin(g.Yaw), hw = W * 0.5f;
                float z0 = Ground(ms, g.X, g.Y);
                z0 = Math.Min(z0, Ground(ms, g.X + ca * hw, g.Y + sa * hw));
                z0 = Math.Min(z0, Ground(ms, g.X - ca * hw, g.Y - sa * hw));
                root = GameEntity.CreateEmpty(scene, false, false, false);
                if (root == (GameEntity)null) throw new InvalidOperationException("CreateEmpty zwrocil null");
                root.Name = "arm_gallows_" + Gallows.VCreated.ToString(Inv);   // nazwa nie moze byc id osady (gra szuka osad po nazwie encji)
                Mat3 rr = Mat3.Identity;
                rr.RotateAboutUp(g.Yaw);
                MatrixFrame rf = new MatrixFrame(in rr, new Vec3(g.X, g.Y, z0, 1f));
                root.SetFrame(ref rf, true);
                int pieces = 0;
                float postLen = H + Sink;
                if (Stick(scene, root, wood, true, postLen, PostT, new Vec3(-hw, 0f, H - postLen * 0.5f))) pieces++;
                if (Stick(scene, root, wood, true, postLen, PostT, new Vec3(hw, 0f, H - postLen * 0.5f))) pieces++;
                if (Stick(scene, root, wood, false, W + 4f * PostT, BeamT, new Vec3(0f, 0f, H - BeamT * 0.5f))) pieces++;
                if (pieces < 3) throw new InvalidOperationException("rama szubienicy niepelna (" + pieces + "/3)");
                int n = Math.Max(1, Math.Min(3, g.Bodies));
                float step = n == 1 ? 0f : n == 2 ? 0.16f : 0.11f;
                uint h = MapVillageData.Fnv(g.District + g.X.ToString("0.0", Inv));
                for (int i = 0; i < n; i++)
                {
                    float x = (i - (n - 1) * 0.5f) * step;
                    float top = H - BeamT;
                    if (Stick(scene, root, wood, true, RopeL, RopeT, new Vec3(x, 0f, top - RopeL * 0.5f))) pieces++;
                    if (body == null) continue;
                    BoundingBox bb = body.GetBoundingBox();
                    float bz = bb.max.z - bb.min.z, bw = Math.Max(bb.max.x - bb.min.x, bb.max.y - bb.min.y);
                    if (!(bz > 0.0005f) || !(bw > 0.0005f) || bz > 1000f || bw > 1000f) continue;
                    // wisielec: siatka stojaca (jak na krzyzu Boltonow), tylko obrot wokol pionu dla rozmaitosci; skala do wysokosci BodyL, ale nie szerzej niz BodyW
                    // (rozlozone rece albo siatka lezaca nie wychodza poza swoje miejsce na belce); gora siatki przy koncu liny
                    float sc = Math.Min(BodyL / bz, BodyW / bw);
                    Mat3 m = Mat3.Identity;
                    m.RotateAboutUp(((h >> (i * 5)) & 31u) / 31f * 0.9f - 0.45f);
                    m.ApplyScaleLocal(sc);
                    if (Piece(scene, root, body, m, bb, new Vec3(x, 0f, top - RopeL - bz * sc * 0.5f))) pieces++;
                }
                root.EntityFlags = root.EntityFlags | EntityFlags.DoNotTick;
                root.SetReadyToRender(true);
                root.SetEntityEnvMapVisibility(false);
                root.SetVisibilityExcludeParents(false);
                s.Root = root; s.Bodies = g.Bodies;
                Gallows.VCreated++; Gallows.VPiecesDay += pieces;
            }
            catch (Exception e)
            {
                if (root != null && s.Root == null) { try { root.Remove(111); } catch { } }
                Strike(s, "tworzenie", e);
            }
        }
    }
}
