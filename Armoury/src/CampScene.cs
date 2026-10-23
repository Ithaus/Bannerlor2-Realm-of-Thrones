using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Path = System.IO.Path;

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
    /// m109-oboz (Jeff 22.10 21:20: "inny jest oboz, jak wchodze do obozu, a inny, jak jestem atakowany w obozie"; klucz CampBattleLikeWalk, domyslnie TAK):
    /// bitwa stawia TEN SAM oboz co spacer (CampLayout - reguly namiotow wedlug zdrowych ludzi kolumny, prefaby ROT, ogniska, poslania, wozy CampCart, konie
    /// taboru uwiazane), po rozstawieniu wojska, za linia gracza (srodek obozu 21 m za ostatnim zolnierzem strony gracza), namiotami do wroga, bez fizyki,
    /// nic blizej niz 6 m od agentow i od linii; bez ludzi przy ogniskach, bez konia gracza i koni jezdzcow (miejsca gracza i jego konia puste). Klucz NIE -
    /// stary zestaw Native ponizej (Build) 1:1. Jency nie dodaja namiotow (ani tu, ani w spacerze - liczy sie MemberRoster bez bohaterow).
    /// Linie "CampScene (m109): " - start, "bitwa w obozie nr", uklad ("oboz jak spacer nr", liczby wedlug regul, postawione, pominiete z przyczyna, odleglosci,
    /// encje, czas), bledy "ERROR in CampScene.<miejsce>" (pierwsze 3 i co 50 na miejsce). Test autotestu (LogGate.Mode 1/2 + plik CrashScribe\
    /// armoury-oboz-bitwa.flag): kazda bitwa w polu jak w obozie (NightRest bez zmian), linie "bitwaK: ludzi=N konie=N wozy=N" albo "bitwaK: stary"
    /// zmieniaja plan i scenografie (roster bez zmian), zdjecie obozu z kamery nad linia.
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

        // m109-oboz: uklad spaceru w bitwie
        internal const string P = "CampScene (m109): ";
        internal const string TestFlag = "armoury-oboz-bitwa.flag";
        private const float ClearM = 6f;                  // nic blizej niz 6 m od agentow i od linii wojska (jak E7/O2 od gracza)
        private static int _battleNo, _stumbles;          // numer bitwy w obozie w procesie (test: "bitwaK"), potkniecia razem
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();
        private static Dictionary<int, string> _spec;      // test: tresc pliku (bitwaK -> reszta linii), czytana raz na proces
        private int _no, _shotStage;
        private bool _tried, _test, _tOld;
        private int? _tHealthy, _tHorses, _tCarts;
        private CampLayout _lay;
        private float _holdT;
        private Vec2 _shotC, _shotUp;
        private object _shotScreen, _shotCam;
        private string _shotPath, _shotName;
        private System.Diagnostics.Stopwatch _shotClock;

        public CampScene()
        {
            try { _test = TestWanted(); } catch { _test = false; }
        }

        /// <summary>m109-oboz: potkniecie per miejsce - Log.Error przy 1., 2., 3. i co 50. (bez wylacznika).</summary>
        internal static void Stumble(string where, Exception e)
        {
            int n;
            lock (_stumbleAt)
            {
                _stumbles++;
                _stumbleAt.TryGetValue(where, out n);
                n++;
                _stumbleAt[where] = n;
            }
            if (n <= 3 || n % 50 == 0) { try { Log.Error("CampScene." + where, e); } catch { } }
        }

        /// <summary>m109-oboz: test bitwy w obozie - DLL autotestu CrashScribe (LogGate.Mode 1/2) i plik CrashScribe\armoury-oboz-bitwa.flag.</summary>
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try { return File.Exists(Path.Combine(CampWalk.CrashScribeDir(), TestFlag)); }
            catch { return false; }
        }

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

        /// <summary>E7/O2: linia startowa (OnSessionLaunched) - tylko przy wlaczonym kluczu. m109-oboz: pod nia linia startu ukladu spaceru w bitwie.</summary>
        internal static void StartLine()
        {
            var s = Settings.Current;
            if (s == null || !s.CampBattlePropsEnabled) return;
            EnsurePrefabs();
            Log.Info("CampScene (E7/O2): prefaby dostepne - namioty " + _nTent + "/" + TentPrefabs.Length + ", ogniska " + _nFire + "/" + FirePrefabs.Length
                     + ", pochodnie " + _nTorch + "/" + TorchPrefabs.Length + ", plot " + _nFence + "/" + FencePrefabs.Length + " (bez fizyki)"
                     + " | uzyte: " + (_tent ?? "-") + ", " + (_fire ?? "-") + ", " + (_torch ?? "-") + ", " + (_fence ?? "-")
                     + (_checkErrors > 0 ? " | bledy PrefabExists " + _checkErrors : ""));
            try
            {
                int pt = Math.Max(2, Math.Min(20, s.CampWalkMenPerTent));
                var ci = CultureInfo.InvariantCulture;
                Log.Info(P + "start | klucz Camp Battle Like Walk " + (s.CampBattleLikeWalk ? "TAK" : "NIE (stary zestaw: 3 namioty, ognisko, 4 pochodnie, plot)")
                         + " | uklad spaceru: namiot na " + pt + " ludzi, namioty do " + CampLayout.MaxTents + ", ogniska do " + CampLayout.MaxFires + ", wozy do " + CampLayout.MaxCarts
                         + ", konie taboru do " + CampLayout.MaxHorses + " | odstep od wojska " + ClearM.ToString("0", ci) + " m, srodek obozu "
                         + (ClearM + CampLayout.FrontReach).ToString("0", ci) + " m za ostatnim zolnierzem"
                         + " | prefaby: " + Pf(CampLayout.TentSmall) + ", " + Pf(CampLayout.TentLeader) + ", " + Pf(CampLayout.TentOfficer) + ", " + Pf(CampLayout.Fire)
                         + ", " + Pf(CampLayout.Bed) + ", " + Pf(CampLayout.Cart)
                         + " | test bitwy w obozie " + (TestWanted() ? "TAK" : "NIE") + ", test spaceru " + (CampWalk.TestWanted() ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static string Pf(string[] names) { return CampWalkPrefabs.First(names) ?? "-"; }

        public override void OnMissionTick(float dt)
        {
            // m109-oboz: uwiazane konie obozu na miejsce (co HoldEvery) i kroki zdjecia testu
            if (_lay != null)
            {
                try
                {
                    _holdT += dt;
                    if (_holdT >= CampLayout.HoldEvery) { _holdT = 0f; _lay.HoldHorses(); }
                    if (_shotStage > 0) ShotTick();
                }
                catch (Exception e) { _shotStage = 0; Stumble("Tick", e); }
            }
            try
            {
                if (_done) return;
                var s = Settings.Current;
                var m = Mission.Current;
                if (s == null || m == null || !s.CampBattlePropsEnabled || !(NightRest.PlayerCamped || _test))
                { _done = true; return; }
                if (m.Scene == null) return;
                if (!m.IsFieldBattle) { _done = true; return; }   // oblezenia, kryjowki, areny - nie
                if (_no == 0) Announce(s);                        // m109: raz na bitwe w polu - numer bitwy w obozie i linia "nr"
                if (m.Mode == MissionMode.Deployment) return;     // E7/O2: czekamy na koniec rozstawienia - gracz moze jeszcze przesunac szyk
                var main = Agent.Main;
                if (main == null) return;                         // czekamy, az gracz stanie na polu
                _done = true;
                _tried = true;
                if (s.CampBattleLikeWalk && !_tOld) BuildLikeWalk(m, main);   // m109-oboz: oboz jak spacer
                else Build(m, main);
            }
            catch (Exception e) { _done = true; Log.Error("CampScene", e); }
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_no > 0 && !_tried) Log.Info(P + "bitwa w obozie nr " + _no + " - bez obozu (koniec misji przed rozstawieniem).");
                if (_shotStage > 0)
                {
                    ShotRestore();
                    ShotLine(_shotPath != null && File.Exists(_shotPath) ? "TAK" : "NIE (koniec misji przed zdjeciem)");
                    _shotStage = 0;
                }
            }
            catch (Exception e) { Stumble("OnEndMission", e); }
            if (_lay != null) _lay.Reset();
            _lay = null;
            base.OnEndMission();
        }

        /// <summary>m109-oboz: linia "bitwa w obozie nr" (raz na bitwe w polu, przed koncem rozstawienia) i ustawienia testu z pliku dla tej bitwy.</summary>
        private void Announce(Settings s)
        {
            _no = ++_battleNo;
            string note = "";
            if (_test)
            {
                note += " | test z pliku";
                try { TestSpec(_no, ref note); } catch (Exception e) { Stumble("TestSpec", e); }
            }
            Log.Info(P + "bitwa w obozie nr " + _no + " | klucz " + (s.CampBattleLikeWalk ? "TAK" : "NIE") + note + " - oboz stanie po rozstawieniu wojska.");
        }

        /// <summary>Test: linia "bitwaK: ludzi=N konie=N wozy=N" albo "bitwaK: stary" dla tej bitwy; nieznane - w linii "nr".</summary>
        private void TestSpec(int k, ref string note)
        {
            if (_spec == null)
            {
                _spec = new Dictionary<int, string>();
                foreach (var raw in File.ReadAllLines(Path.Combine(CampWalk.CrashScribeDir(), TestFlag)))
                {
                    var ln = raw.Trim();
                    int colon = ln.IndexOf(':');
                    int n;
                    if (!ln.StartsWith("bitwa", StringComparison.OrdinalIgnoreCase) || colon < 6) continue;
                    if (int.TryParse(ln.Substring(5, colon - 5).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) _spec[n] = ln.Substring(colon + 1);
                }
            }
            string spec;
            if (!_spec.TryGetValue(k, out spec)) return;
            foreach (var tok in spec.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = tok.Trim().ToLowerInvariant();
                if (t == "stary") { _tOld = true; note += " | test: stary zestaw"; continue; }
                int eq = t.IndexOf('=');
                int v;
                if (eq > 0 && int.TryParse(t.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 0)
                {
                    string key = t.Substring(0, eq);
                    if (key == "ludzi") { _tHealthy = v; note += " | test: ludzi " + v; continue; }
                    if (key == "konie") { _tHorses = v; note += " | test: konie " + v; continue; }
                    if (key == "wozy") { _tCarts = v; note += " | test: wozy " + v; continue; }
                }
                note += " | test: nieznane " + t;
            }
        }

        private void NoCamp(string why)
        {
            Log.Info(P + "bitwa w obozie nr " + _no + " - bez obozu (" + why + ").");
        }

        /// <summary>m109-oboz: oboz jak spacer (CampLayout) za linia gracza. Strona gracza = aktywni ludzie wszystkich druzyn tej strony (srodek G); kierunek do wroga
        /// (FrontYaw ze srodkiem G); os obozu Up = od wroga; tyl = najdalszy agent strony gracza wzdluz Up (z koniem); srodek obozu C = tyl + 6 m + 15 m
        /// (FrontReach). Bez ludzi i koni jezdzcow; nic blizej niz 6 m od agentow (migawka) i od linii (FrontY). Pomiar odleglosci po postawieniu.</summary>
        private void BuildLikeWalk(Mission m, Agent main)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int st0 = _stumbles;
            var s = Settings.Current;
            var party = MobileParty.MainParty;
            var pteam = m.PlayerTeam;
            if (party == null || pteam == null) { NoCamp("brak druzyny gracza"); return; }
            var side = pteam.Side;
            // strona gracza: aktywni ludzie wszystkich druzyn tej strony (konie Native nie maja druzyny - liczone przez MountAgent jezdzca)
            var mine = new List<Agent>();
            float gx = 0f, gy = 0f;
            foreach (var t in m.Teams)
            {
                if (t == null || t.Side != side) continue;
                foreach (var a in t.ActiveAgents)
                {
                    if (a == null || !a.IsHuman) continue;
                    mine.Add(a);
                    var q = a.Position;
                    gx += q.x; gy += q.y;
                }
            }
            if (mine.Count == 0) { NoCamp("brak wojska"); return; }
            var g = new Vec2(gx / mine.Count, gy / mine.Count);
            string src;
            float front = FrontYaw(m, main, new Vec3(g.x, g.y, 0f), out src);
            var up = new Vec2(-(float)Math.Cos(front), -(float)Math.Sin(front));
            float rear = float.MinValue;
            foreach (var a in mine)
            {
                rear = Math.Max(rear, Vec2.DotProduct(a.Position.AsVec2 - g, up));
                var mount = a.MountAgent;
                if (mount != null) rear = Math.Max(rear, Vec2.DotProduct(mount.Position.AsVec2 - g, up));
            }
            float off = ClearM + CampLayout.FrontReach;   // linia wojska w ukladzie obozu: y = -off
            var c = g + up * (rear + off);
            float depth = 0f;
            if (m.IsPositionInsideBoundaries(c))
                while (depth < 400f && m.IsPositionInsideBoundaries(c + up * (depth + 2f))) depth += 2f;
            int perTent = s != null ? Math.Max(2, Math.Min(20, s.CampWalkMenPerTent)) : 6;
            var rng = new Random();
            var lay = new CampLayout(m, c, up, P, Stumble, rng)
            {
                Clear = ClearM, FrontY = ClearM - off, RiderHorses = false, DepthLimit = depth, CountEntities = true
            };
            var pts = new List<Vec2>();
            foreach (var a in m.Agents) if (a != null) pts.Add(a.Position.AsVec2);
            lay.SetTroops(pts);
            var plan = CampLayout.Count(party, perTent, rng, _tHealthy);
            if (_tHorses.HasValue) TestHorses(plan, _tHorses.Value);
            plan.Recount(perTent, _tCarts.HasValue ? _tCarts.Value : CampCart.Carts);
            _lay = lay;
            lay.Probe();
            lay.Lay(plan, perTent);

            // pomiar: najblizszy obrys od gracza i od kazdego agenta (bez koni obozu), front obozu za linia
            var tied = new HashSet<Agent>();
            foreach (var t in lay.TiedHorses) if (t.Agent != null) tied.Add(t.Agent);
            float dMain = float.MaxValue, dTroop = float.MaxValue, minY0 = float.MaxValue;
            foreach (var b in lay.Placed) minY0 = Math.Min(minY0, b.Y0);
            if (lay.Placed.Count > 0)
            {
                if (main != null && main.IsActive())
                {
                    var l = lay.L(main.Position.AsVec2);
                    foreach (var b in lay.Placed) dMain = Math.Min(dMain, CampLayout.Dist(b, l));
                }
                foreach (var a in m.Agents)
                {
                    if (a == null || tied.Contains(a)) continue;
                    var l = lay.L(a.Position.AsVec2);
                    foreach (var b in lay.Placed) dTroop = Math.Min(dTroop, CampLayout.Dist(b, l));
                }
            }
            float frontD = minY0 < float.MaxValue ? minY0 + off : float.MaxValue;
            bool night = CampaignTime.Now.IsNightTime || NightRest.InCamp(CampaignTime.Now.GetHourOfDay);
            var ci = CultureInfo.InvariantCulture;
            long ms = sw.ElapsedMilliseconds;
            Log.Info(P + "oboz jak spacer nr " + _no + " | " + (night ? "noc" : "dzien") + (plan.Army ? ", armia (kolumna " + plan.Column.Count + " partii)" : "")
                     + " | ludzi zdrowych " + plan.Healthy + (plan.HealthyFromTest ? " (test z pliku)" : "") + " (namiot na " + perTent + ")"
                     + " | wedlug regul spaceru: " + lay.PlanText(plan)
                     + " | postawione: namioty " + lay.Tents + ", poslania " + lay.Beds + ", ogniska " + lay.Fires + ", wozy " + lay.Carts + ", konie " + lay.TrainHorses
                     + " | pominiete: granica " + lay.SkipBorder + ", teren " + lay.SkipTerrain + ", obrysy " + lay.SkipOverlap + ", wojsko " + lay.SkipTroops
                     + ", prefab " + lay.SkipPrefab + ", bledy " + lay.SkipError
                     + " | rzad po " + lay.PerRow + ", glebokosc za srodkiem obozu " + depth.ToString("0", ci) + " m | " + lay.GeometryText()
                     + " | najblizej: gracz " + D1(dMain) + " m, wojsko " + D1(dTroop) + " m | front obozu " + D1(frontD) + " m za linia"
                     + " | kierunek " + src + " | agentow strony gracza " + mine.Count + " | encje " + lay.Entities
                     + " | czas " + ms + " ms | potkniecia " + (_stumbles - st0) + ".");
            if (_test)
            {
                _shotC = c;
                _shotUp = up;
                _shotStage = 1;
                _shotClock = System.Diagnostics.Stopwatch.StartNew();
            }
        }

        private static string D1(float v) { return v >= float.MaxValue ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>Test: konie taboru do n - najpierw ze stosow taboru (plan), brak dopelnia juczny kon gry (sumpter_horse, inaczej pierwszy juczny
        /// z HorseComponent bez bydla). Tylko plan i scenografia - ItemRoster bez zmian.</summary>
        private static void TestHorses(CampLayout.Plan p, int n)
        {
            n = Math.Max(0, Math.Min(CampLayout.MaxHorses, n));
            if (p.Train.Count > n) p.Train.RemoveRange(n, p.Train.Count - n);
            if (p.Train.Count >= n) return;
            ItemObject it = null;
            try
            {
                var sumpter = MBObjectManager.Instance.GetObject<ItemObject>("sumpter_horse");
                if (sumpter != null && sumpter.HorseComponent != null && !sumpter.HorseComponent.IsLiveStock) it = sumpter;
                if (it == null)
                    foreach (var x in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (x != null && x.HorseComponent != null && x.HorseComponent.IsPackAnimal && !x.HorseComponent.IsLiveStock) { it = x; break; }
            }
            catch (Exception e) { Stumble("TestHorses", e); }
            if (it == null) return;
            while (p.Train.Count < n) p.Train.Add(new EquipmentElement(it));
        }

        // ------------------------------------------------------------ m109-oboz: zdjecie obozu (tylko test) - kamera nad linia, patrzy na oboz

        /// <summary>Kroki od postawienia obozu (czas rzeczywisty): 1.0 s kamera (MissionScreen.CustomCamera), 1.6 s zdjecie silnika, 2.5 s kamera gry,
        /// 3.5 s sprawdzenie pliku i linia testu. Typy widoku misji tylko tu (osobna metoda).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ShotTick()
        {
            long ms = _shotClock != null ? _shotClock.ElapsedMilliseconds : 0;
            if (_shotStage == 1 && ms >= 1000)
            {
                _shotStage = 2;
                var scr = TaleWorlds.ScreenSystem.ScreenManager.TopScreen as TaleWorlds.MountAndBlade.View.Screens.MissionScreen;
                if (scr == null) { ShotLine("NIE (brak MissionScreen)"); _shotStage = 0; return; }
                float z = 0f;
                try { z = Mission.Current.Scene.GetTerrainHeight(_shotC, true); } catch { }
                var eye = _shotC - _shotUp * 40f;
                var at = _shotC + _shotUp * 20f;
                var cam = Camera.CreateCamera();
                cam.LookAt(new Vec3(eye.x, eye.y, z + 28f), new Vec3(at.x, at.y, z), new Vec3(0f, 0f, 1f));
                cam.SetFovVertical(65f * (float)Math.PI / 180f, TaleWorlds.Engine.Screen.AspectRatio, 0.1f, 5000f);
                scr.CustomCamera = cam;
                _shotScreen = scr;
                _shotCam = cam;
                return;
            }
            if (_shotStage == 2 && ms >= 1600)
            {
                _shotStage = 3;
                string dir = Path.Combine(CampWalk.CrashScribeDir(), "zdjecia");
                Directory.CreateDirectory(dir);
                _shotName = "oboz-m109-" + _no + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png";
                _shotPath = Path.Combine(dir, _shotName);
                TaleWorlds.Engine.Utilities.TakeScreenshot(_shotPath.Replace('\\', '/'));
                return;
            }
            if (_shotStage == 3 && ms >= 2500)
            {
                _shotStage = 4;
                ShotRestore();
                return;
            }
            if (_shotStage == 4 && ms >= 3500)
            {
                _shotStage = 0;
                ShotLine(_shotPath != null && File.Exists(_shotPath) ? "TAK" : "NIE (brak pliku)");
            }
        }

        /// <summary>Kamera gry z powrotem (tylko gdy na ekranie misji jest jeszcze nasza kamera).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ShotRestore()
        {
            var scr = _shotScreen as TaleWorlds.MountAndBlade.View.Screens.MissionScreen;
            if (scr != null && _shotCam != null && ReferenceEquals(scr.CustomCamera, _shotCam)) scr.CustomCamera = null;
            _shotScreen = null;
            _shotCam = null;
        }

        private void ShotLine(string result)
        {
            Log.Info(P + "test - zdjecie nr " + _no + ": zdjecia\\" + (_shotName ?? "-") + " - " + result + ".");
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
