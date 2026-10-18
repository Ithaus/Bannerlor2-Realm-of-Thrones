using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SandBox.View;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// ZJ (sw-zajazdy) - ZAJAZDY NA MAPIE: budynek zajazdu przy drodze (dom kultury krainy jako gospoda, stodola-stajnia, zagroda dla koni, woz, beczki, stog;
    /// przy 2 jakosciach przybudowka, przy 3 studnia). Ta sama droga co wioski na mapie (MapVillagesView): KOPIE SIATEK po nazwie (MetaMesh.GetCopy, jak namiot partii
    /// gry) w pustych encjach - bez fizyki, skryptow, czastek, dzwiekow, tagow; skala z BB siatki do wielkosci w jedn. mapy; kazdy element na swoim gruncie (dol BB na
    /// najnizszym punkcie terenu pod nim). Obrazek powstaje RAZ (przy kamerze, najwyzej 6 na cwierc sekundy) i potem tylko pokaz / schowaj przy zmianie stanu - nic
    /// co klatke (CLAUDE.md 7). Widoczny tylko, gdy wies okregu jest widoczna (mgla zwiadu jak wioski) i kamera nisko (MapVillagesHideAboveCameraHeight).
    /// Dymek po najechaniu (nazwa, okreg, lozka, ceny, jak wejsc); klik = RoadsideInns.OnClick (jazda do zajazdu, menu po dojechaniu). Nic do zapisu gry.
    /// Potkniecia per zajazd (3 = ten jeden pominiety), nigdy cala warstwa.
    /// </summary>
    public sealed class RoadsideInnsView : EntityVisualManagerBase
    {
        public override int Priority => 101;   // po wioskach (100): dymek i klik tylko, gdy nikt inny nie trafil

        private const float TickEvery = 0.25f;
        private const float ShowRadiusExtra = 120f;
        private const float HoverR = 1.15f;           // promien dymka / kliku wokol budynku
        private const int CreatePerTick = 6;
        private const int StrikesPerInn = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly long CreateBudgetTicks = System.Diagnostics.Stopwatch.Frequency * 6 / 1000;

        private sealed class Slot
        {
            public RoadsideInns.Inn Inn;
            public GameEntity Root;
            public Visual V;
            public bool Shown, Failed, NeedRes;
            public int Strikes, Pieces;
            public float Z;
        }

        private MapScreen _screen;
        private bool _active, _finalized, _wasOff;
        private float _acc;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<Slot> _shown = new List<Slot>();
        private readonly Dictionary<string, MetaMesh> _meshes = new Dictionary<string, MetaMesh>(StringComparer.Ordinal);
        private readonly HashSet<string> _missLogged = new HashSet<string>(StringComparer.Ordinal);

        // liczniki - w InnViewStats (RoadsideInns.cs, bez typow SandBox.View: linia doby czyta je takze, gdy typ widoku sie nie zaladowal)

        // ---------- rejestracja (z SubModuleMain.OnApplicationTick; tani test co klatke, jak MapVillagesView) ----------
        private static WeakReference _regVm, _regScreen;
        private static int _stRegister;

        internal static void EnsureRegistered()
        {
            try
            {
                if (!Settings.Current.RoadsideInns) return;
                if (Campaign.Current == null) return;
                MapScreen screen = MapScreen.Instance;
                if (screen == null) return;
                SandBoxViewVisualManager vm = SandBoxViewSubModule.SandBoxViewVisualManager;
                if (vm == null) return;
                if (_regVm != null && ReferenceEquals(_regVm.Target, vm) && _regScreen != null && ReferenceEquals(_regScreen.Target, screen)) return;
                if (vm.GetEntityComponent<SettlementVisualManager>() == null) return;
                var mine = vm.GetEntityComponent<RoadsideInnsView>();
                if (mine != null && !ReferenceEquals(mine._screen, screen)) { vm.RemoveEntityComponent(mine); mine = null; }
                if (mine == null) vm.AddEntityComponent<RoadsideInnsView>();
                _regVm = new WeakReference(vm);
                _regScreen = new WeakReference(screen);
            }
            catch (Exception e)
            {
                _stRegister++;
                if (_stRegister <= 3 || _stRegister % 500 == 0) Log.Error("RoadsideInnsView.EnsureRegistered (potkniecie " + _stRegister + ")", e);
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _screen = MapScreen.Instance;
            try
            {
                RoadsideInns.EnsureLoaded();
                foreach (var inn in RoadsideInns.All) _slots.Add(new Slot { Inn = inn });
                _active = _slots.Count > 0;
                InnViewStats.MapReset();
                Log.Info("Zajazdy (ZJ): widok mapy - zajazdow " + _slots.Count + "; obrazki powstaja raz przy kamerze (z <= "
                         + Settings.Current.MapVillagesHideAboveCameraHeight.ToString("0", Inv) + "), potem tylko pokaz / schowaj; nic do zapisu gry.");
            }
            catch (Exception e) { _active = false; Log.Error("RoadsideInnsView.OnInitialize - zajazdy na mapie wylaczone do nastepnego wczytania mapy", e); }
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
                    s.Root = null; s.V = null; s.Shown = false;
                }
                if (_active) Log.Info("Zajazdy (ZJ): koniec mapy - zdjete obrazki " + removed + (sceneAlive ? "" : " (scena juz nie ta - same odwolania)") + "; postawione od wczytania " + InnViewStats.Created + ".");
            }
            catch (Exception e) { Log.Error("RoadsideInnsView.OnFinalize", e); }
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
                if (!Settings.Current.RoadsideInns)
                {
                    if (!_wasOff)
                    {
                        foreach (var s in _shown) { try { if (s.Root != null) s.Root.SetVisibilityExcludeParents(false); } catch { } s.Shown = false; }
                        _shown.Clear();
                        _wasOff = true;
                        Log.Info("Zajazdy (ZJ): wylaczone w MCM - obrazki schowane.");
                    }
                    return;
                }
                _wasOff = false;
                UpdateVisibility(screen);
            }
            catch (Exception e) { InnViewStats.StVis++; if (InnViewStats.StVis <= 3 || InnViewStats.StVis % 200 == 0) Log.Error("RoadsideInnsView.OnVisualTick (potkniecie " + InnViewStats.StVis + ")", e); }
        }

        private static bool InnVisible(Slot s)
        {
            var v = s.Inn.Village;
            return v != null && v.IsVisible;
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
                    float dx = s.Inn.Bx - cam.x, dy = s.Inn.By - cam.y;
                    if (far || dx * dx + dy * dy >= r2 || !InnVisible(s))
                    {
                        if (s.Root != null) s.Root.SetVisibilityExcludeParents(false);
                        s.Shown = false; _shown.RemoveAt(i); InnViewStats.HideDay++;
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
                float dx = s.Inn.Bx - cam.x, dy = s.Inn.By - cam.y;
                if (dx * dx + dy * dy >= r2) continue;
                try
                {
                    if (!InnVisible(s)) continue;
                    if (s.Root == null)
                    {
                        if (made >= CreatePerTick || (made > 0 && System.Diagnostics.Stopwatch.GetTimestamp() - t0 > CreateBudgetTicks)) continue;
                        long c0 = System.Diagnostics.Stopwatch.GetTimestamp();
                        Create(s);
                        InnViewStats.CreateTicks += System.Diagnostics.Stopwatch.GetTimestamp() - c0;
                        made++;
                        if (s.Root == null) continue;
                    }
                    s.Root.SetVisibilityExcludeParents(true);   // tylko przy zmianie stanu
                    s.Root.CheckResources(true, false);
                    s.NeedRes = false;
                    s.Shown = true;
                    _shown.Add(s); InnViewStats.ShowDay++;
                }
                catch (Exception e) { Strike(s, "pokaz", e); if (s.Root != null) { try { s.Root.SetVisibilityExcludeParents(false); } catch { } } }
            }
            if (_shown.Count > InnViewStats.ShownMax) InnViewStats.ShownMax = _shown.Count;
        }

        private void Strike(Slot s, string where, Exception e)
        {
            s.Strikes++;
            if (where == "tworzenie") InnViewStats.StCreate++; else InnViewStats.StVis++;
            if (s.Strikes >= StrikesPerInn && !s.Failed) { s.Failed = true; InnViewStats.Failed++; }
            int n = InnViewStats.StCreate + InnViewStats.StVis;
            if (n <= 3 || n % 200 == 0) Log.Error("RoadsideInnsView " + where + " " + s.Inn.Uid + " '" + s.Inn.Name + "' (potkniecie zajazdu " + s.Strikes + "/" + StrikesPerInn + ")", e);
        }

        // ---------- obrazek ----------
        private sealed class Piece { public string[] Meshes; public float Size, X, Y, Yaw; public bool Main; }

        /// <summary>Styl budynkow z kultury krainy zajazdu (id kultur ROT: battania = Polnoc, vlandia = Westerlands, aserai = Dorne ...); zapas - empire.</summary>
        private static string StyleOf(string culture)
        {
            switch (culture)
            {
                case "battania": case "nightswatch": case "skagosi": return "sturgia";
                case "reach": case "river": case "vale": case "stormlands": case "crownlands": case "dragonstone": case "vlandia": case "westerlands": return "vlandia";
                case "aserai": case "ghiscari": case "qartheen": case "yiti": case "summer": return "aserai";
                case "khuzait": case "sarnor": return "khuzait";
                default: return "empire";
            }
        }

        private static List<Piece> Recipe(RoadsideInns.Inn inn, MapVillageData.StyleKit st, uint h)
        {
            var l = new List<Piece>();
            var houses = st.Houses.Length > 0 ? st.Houses : new[] { "mi_emp_house_a" };
            var hs = new string[houses.Length];
            for (int i = 0; i < hs.Length; i++) hs[i] = houses[(int)((h + (uint)i) % (uint)houses.Length)];
            l.Add(new Piece { Meshes = hs, Size = 0.80f, X = 0f, Y = 0f, Main = true });                                         // gospoda
            if (st.FarmBarn.Length > 0) l.Add(new Piece { Meshes = st.FarmBarn, Size = 0.85f, X = -0.92f, Y = -0.08f });          // stajnia
            l.Add(new Piece { Meshes = new[] { "mi_cattle_farm_a", "mi_cattle_farm_b" }, Size = 0.70f, X = 0.10f, Y = -0.86f });   // zagroda dla koni
            l.Add(new Piece { Meshes = new[] { "mi_cart_a" }, Size = 0.32f, X = 0.66f, Y = 0.44f, Yaw = 20f });                   // woz podroznych
            l.Add(new Piece { Meshes = new[] { "mi_barrels_a" }, Size = 0.16f, X = -0.24f, Y = 0.50f });                          // beczki piwa
            l.Add(new Piece { Meshes = new[] { "mi_straw_pile" }, Size = 0.22f, X = 0.74f, Y = -0.30f });                         // stog dla koni
            l.Add(new Piece { Meshes = new[] { "mi_sack_a" }, Size = 0.13f, X = -0.02f, Y = 0.52f });
            if (inn.Tiers >= 2) l.Add(new Piece { Meshes = new[] { houses[(int)((h >> 5) % (uint)houses.Length)] }, Size = 0.52f, X = 0.86f, Y = 0.06f, Yaw = 90f });   // przybudowka (izby)
            if (inn.Tiers >= 3 && !string.IsNullOrEmpty(st.Well)) l.Add(new Piece { Meshes = new[] { st.Well }, Size = 0.18f, X = -0.55f, Y = 0.62f });               // studnia
            return l;
        }

        private MetaMesh MeshByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            MetaMesh mm;
            if (_meshes.TryGetValue(name, out mm)) return mm;
            _meshes[name] = null;
            mm = MetaMesh.GetCopy(name, false, true);
            if (mm != null && !mm.IsValid) mm = null;
            if (mm != null && !HasMaterial(mm)) mm = null;
            _meshes[name] = mm;
            if (mm == null) { InnViewStats.MeshMiss++; if (_missLogged.Add(name)) Log.Info("Zajazdy (ZJ): brak siatki '" + name + "' w grze (albo bez materialu) - element pominiety."); }
            return mm;
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

        private static bool Land(TaleWorlds.CampaignSystem.Map.IMapScene ms, float x, float y)
        {
            try
            {
                var cv = new CampaignVec2(new Vec2(x, y), true);
                PathFaceRecord f = ms.GetFaceIndex(in cv);
                return f.IsValid() && MapVillageData.IsLandGroup(f.FaceGroupIndex);
            }
            catch { return true; }
        }

        /// <summary>Obrazek zajazdu (raz): korzen w budynku (bx, by) obrocony frontem do drogi, elementy z przepisu - kazdy w swojej ramce (obrot + skala z BB),
        /// dol BB na najnizszym gruncie pod nim (minus male zaglebienie); element poza ladem (poza gospoda) - zdjety.</summary>
        private void Create(Slot s)
        {
            try
            {
                Scene scene = MapScene;
                var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
                if (scene == null || ms == null) throw new InvalidOperationException("brak sceny mapy");
                var inn = s.Inn;
                uint h = MapVillageData.Fnv(inn.Uid);
                var st = MapVillageData.StyleFor(StyleOf(inn.Culture), false);
                float yaw = inn.FrontDeg * (float)Math.PI / 180f;
                float ca = (float)Math.Cos(yaw), sa = (float)Math.Sin(yaw);
                float rootZ = Ground(ms, inn.Bx, inn.By);
                GameEntity root = GameEntity.CreateEmpty(scene, false, false, false);
                if (root == (GameEntity)null) throw new InvalidOperationException("CreateEmpty zwrocil null");
                root.Name = "arm_roadinn_" + inn.Id.ToString(Inv);   // nazwa nie moze byc id osady (gra szuka osad po nazwie encji)
                Mat3 rr = Mat3.Identity;
                rr.RotateAboutUp(yaw);
                MatrixFrame rf = new MatrixFrame(in rr, new Vec3(inn.Bx, inn.By, rootZ, 1f));
                root.SetFrame(ref rf, true);
                int pieces = 0;
                foreach (var p in Recipe(inn, st, h))
                {
                    MetaMesh mm = null;
                    foreach (var name in p.Meshes) { mm = MeshByName(name); if (mm != null) break; }
                    if (mm == null) continue;
                    BoundingBox bb = mm.GetBoundingBox();
                    float ex = bb.max.x - bb.min.x, ey = bb.max.y - bb.min.y, ez = bb.max.z - bb.min.z;
                    if (!(ex > 0.001f) || !(ey > 0.001f) || ex > 1000f || ey > 1000f) continue;
                    float sc = p.Size / Math.Max(ex, ey);
                    // obrot elementu: dluzsza os BB wzdluz drogi (lokalna X), plus obrot z przepisu
                    float py = (ey > ex * 1.15f ? 90f : 0f) + p.Yaw;
                    float pyr = py * (float)Math.PI / 180f;
                    Mat3 cr = Mat3.Identity;
                    cr.RotateAboutUp(pyr);
                    cr.ApplyScaleLocal(new Vec3(sc, sc, sc));
                    var bc = new Vec3((bb.min.x + bb.max.x) * 0.5f, (bb.min.y + bb.max.y) * 0.5f, 0f);
                    Vec3 off = cr.TransformToParent(in bc);
                    // grunt: srodek i rogi obrysu elementu (swiat)
                    float hx = Math.Max(ex, ey) * sc * 0.5f;
                    float wx = inn.Bx + ca * p.X - sa * p.Y, wy = inn.By + sa * p.X + ca * p.Y;
                    if (!p.Main && !Land(ms, wx, wy)) { InnViewStats.PiecesWater++; continue; }
                    float gmin = Ground(ms, wx, wy);
                    gmin = Math.Min(gmin, Ground(ms, wx + hx, wy + hx)); gmin = Math.Min(gmin, Ground(ms, wx - hx, wy + hx));
                    gmin = Math.Min(gmin, Ground(ms, wx + hx, wy - hx)); gmin = Math.Min(gmin, Ground(ms, wx - hx, wy - hx));
                    float sink = MapVillageData.Sink(ez * sc);
                    float zl = gmin - rootZ - bb.min.z * sc - sink;
                    var f = new MatrixFrame(in cr, new Vec3(p.X - off.x, p.Y - off.y, zl, 1f));
                    GameEntity e = GameEntity.CreateEmpty(scene, false, false, false);
                    if (e == (GameEntity)null) continue;
                    MetaMesh copy = mm.CreateCopy();
                    if (copy == null || !copy.IsValid) copy = MetaMesh.GetCopy(mm.GetName(), false, true);
                    if (copy == null || !copy.IsValid) continue;
                    e.AddMultiMesh(copy, true);
                    e.SetFrame(ref f, true);
                    root.AddChild(e, false);
                    e.EntityFlags = e.EntityFlags | EntityFlags.DoNotTick;
                    e.SetReadyToRender(true);
                    pieces++;
                }
                if (pieces == 0) { try { root.Remove(111); } catch { } throw new InvalidOperationException("obrazek bez siatek"); }
                root.EntityFlags = root.EntityFlags | EntityFlags.DoNotTick;
                root.SetReadyToRender(true);
                root.SetEntityEnvMapVisibility(false);
                root.SetVisibilityExcludeParents(false);
                s.Root = root; s.Pieces = pieces; s.Z = rootZ;
                InnViewStats.Created++; InnViewStats.CreatedDay++;
            }
            catch (Exception e) { Strike(s, "tworzenie", e); }
        }

        // ---------- dymek i klik: punkt terenu pod kursorem do HoverR od budynku ----------
        public override bool OnVisualIntersected(Ray mouseRay, UIntPtr[] intersectedEntityIDs, Intersection[] intersectionInfos, int entityCount,
            Vec3 worldMouseNear, Vec3 worldMouseFar, Vec3 terrainIntersectionPoint, ref MapEntityVisual hoveredVisual, ref MapEntityVisual selectedVisual)
        {
            if (hoveredVisual != null || !_active || _finalized || _shown.Count == 0) return false;
            try
            {
                if (!Settings.Current.RoadsideInns) return false;
                var p = new Vec2(terrainIntersectionPoint.x, terrainIntersectionPoint.y);
                Slot best = null; float bd = HoverR;
                foreach (var s in _shown)
                {
                    if (s.Root == null) continue;
                    float d = p.Distance(new Vec2(s.Inn.Bx, s.Inn.By));
                    if (d < bd) { bd = d; best = s; }
                }
                if (best != null)
                {
                    if (best.V == null) best.V = new Visual(best);
                    hoveredVisual = best.V;
                    if (selectedVisual == null) selectedVisual = best.V;   // klik idzie do zajazdu (OnMapClick), nie w teren
                }
            }
            catch (Exception e) { InnViewStats.StHover++; if (InnViewStats.StHover <= 3 || InnViewStats.StHover % 500 == 0) Log.Error("RoadsideInnsView.Hover (potkniecie " + InnViewStats.StHover + ")", e); }
            return false;
        }

        /// <summary>"Wizerunek" zajazdu dla MapScreen: dymek i klik (jazda do zajazdu). Kursor raczki (IsMobileEntity = false).</summary>
        private sealed class Visual : MapEntityVisual
        {
            private readonly Slot _s;
            internal Visual(Slot s) { _s = s; }

            public override CampaignVec2 InteractionPositionForPlayer => new CampaignVec2(new Vec2(_s.Inn.X, _s.Inn.Y), true);
            public override MapEntityVisual AttachedTo => null;
            public override bool IsVisibleOrFadingOut() { return _s.Shown; }
            public override Vec3 GetVisualPosition() { return new Vec3(_s.Inn.Bx, _s.Inn.By, _s.Z); }
            public override void OnOpenEncyclopedia() { }

            public override bool OnMapClick(bool followModifierUsed)
            {
                try
                {
                    bool ok = RoadsideInns.OnClick(_s.Inn);
                    if (ok) { InnViewStats.Clicks++; InnViewStats.ClicksDay++; }
                    return ok;
                }
                catch (Exception e) { InnViewStats.StClick++; if (InnViewStats.StClick <= 3) Log.Error("RoadsideInnsView.OnMapClick " + _s.Inn.Uid, e); return false; }
            }

            public override void OnHover()
            {
                try
                {
                    var inn = _s.Inn;
                    var list = new List<TooltipProperty>();
                    list.Add(new TooltipProperty("", inn.Name, 0, false, TooltipProperty.TooltipPropertyFlags.Title));
                    list.Add(new TooltipProperty("", Cap(RoadsideInns.KindText(inn)) + " on the lands of " + inn.Village.Name + ".", 0));
                    list.Add(new TooltipProperty("", RoadsideInns.BedsText(inn), 0));
                    var beds = new StringBuilder("A man / a horse / your room: common room 1 / 1 / 3");
                    if (inn.Tiers >= 2) beds.Append(", decent inn 2 / 2 / 8");
                    if (inn.Tiers >= 3) beds.Append(", good house 4 / 3 / 20");
                    list.Add(new TooltipProperty("", beds.ToString() + ".", 0));
                    if (Rumours.On) list.Add(new TooltipProperty("", "Travellers by the hearth trade the news of the road.", 0));
                    list.Add(new TooltipProperty("", "Click to ride there and go in.", 0));
                    InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new object[] { list });
                }
                catch (Exception e) { InnViewStats.StHover++; if (InnViewStats.StHover <= 3 || InnViewStats.StHover % 200 == 0) Log.Error("RoadsideInnsView.OnHover " + _s.Inn.Uid, e); }
            }

            private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
        }
    }
}
