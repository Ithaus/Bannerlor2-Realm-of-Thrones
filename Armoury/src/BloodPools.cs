using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NB2 czesc 3 (16.10, Jeff: "wiecej krwi ... zeby wygladalo na pobojowisko"): KALUZE KRWI NA ZIEMI (klucz GroundBloodPools).
    ///
    /// Dowod [K] - dlaczego MoreBloodPoolMod wywalil gre (16.10 10:06, 0xC0000005 w TaleWorlds.Native przy pierwszym mocnym ciosie): jego
    /// BloodPoolBehavior.SpawnDecal robi Decal.CreateDecal + SetFrame + Scene.AddDecalInstance(decal, "blood_set", false), a przygotowanego materialu
    /// (kopia deferred_decal_material z tekstura blood_terrain_decal_i_d) NIGDY nie przypisuje - do sceny trafial decal bez materialu.
    ///
    /// Wzor gry [K] (dekompilacja 1.4.8): decale w misji z C# stawia NavalDLC ShipWaterEffects (skrypt misji): Decal.CreateDecal() ->
    /// decal.SetMaterial(Material.GetFromResource("decal_water_foam")) -> scene.AddDecalInstance(decal, "editor_set", deletable: true)
    /// (ShipWaterEffects.cs:1366-1368) -> Frame (:1400) -> SetAlpha (:1401, potem wygaszanie / rozjasnianie :1310-1325) -> SetVectorArgument (:1406);
    /// wrappery decali trzyma w tablicy przez cale zycie skryptu, a przy zdjeciu wola RemoveDecalInstance(decal, "editor_set") (:390-397) - tylko gdy
    /// Scene.HasDecalRenderer() (:390); renderer sprawdza tez w kazdym ticku przed stawianiem (:427). Tak samo na mapie SandBox.View: MapScreen.cs:67-73,
    /// MapCursor.cs:61-64, SettlementVisualManager.cs:384-395 - zawsze SetMaterial PRZED AddDecalInstance, zawsze "editor_set". Materialy krwi sa w grze
    /// gotowe: blood_terrain_decal_0..11 (core_game.tpac: materialy decali z render_on_terrain / render_on_objects / render_on_grass; te same 12 nazw
    /// w decal_materials emiterow krwi, Native particle_systems_hardcoded_misc1.xml:50444-50469; ROT stawia je w scenach jako decal_component, ROT-Map
    /// Prefabs MyItems_Siege.xml:11077-11116, Mystaf_Outside.xml:7868). NativeObject liczy referencje (Construct: IncreaseReferenceCount, finalizer:
    /// DecreaseReferenceCount) - wrapper decala trzymamy do konca misji.
    ///
    /// Zasady (poprawki po recenzji 16.10 w nawiasach):
    ///  - tylko bitwa na mapie / oblezenie / kryjowka / bitwa wlasna (BattleSense.InBattle), bez bitew i wypadow morskich (poklad sie rusza, decal
    ///    stoi w swiecie), bez turnieju, areny, treningu; nowe zadania przyjmujemy do zamykania misji (stan inny niz Continuing) - takze po zwyciestwie
    ///    (MissionEnded przychodzi, gdy wrog ucieka; poscig to duza czesc zabitych na polu, ktore Jeff oglada po bitwie); pulap 600 i 20/s ograniczaja koszt;
    ///  - zgon czlowieka: 1-3 nakladajace sie kaluze wzdluz ciala (od stop w strone upadku - od napastnika, przy szybkim ruchu wzdluz predkosci,
    ///    srodek przesuniety o predkosc x 0.4 s, najwyzej 3 m), dluzsza os wzdluz ciala (+-25 stopni); promien wedlug ciosu: glowa / szyja wiekszy,
    ///    strzala / belt mniejszy (i o jedna kaluze mniej), obrazenia 80+ troche wiekszy; obuch 1 kaluza 0.4-0.7 m. Nokaut obuchem (albo bez znanego
    ///    ciosu): tylko mala plama 0.2-0.35 m; nokaut cieciem / kluciem (ranny): 1 kaluza 0.5-0.8 m. Kon 1-2 (0.9-1.4 m, losowy obrot). Nieumarli ROT
    ///    (Undead) i bron NoBlood bez krwi; agent w wodzie (IsInWater - dla jezdzca kon) bez krwi; raz na agenta;
    ///  - kaluza pojawia sie po upadku ciala (zgon 1.5-2.5 s, kolejne kaluzy tego samego zgonu co 0.3-0.6 s pozniej, kon 2-3 s, plama po ciosie
    ///    0.25-0.45 s) i rozjasnia sie przez 2.5 s (Decal.SetAlpha jak ShipWaterEffects; najwyzej 40 rosnacych naraz, co 0.1 s; nadmiar od razu pelna);
    ///  - cios ciety / klujacy / strzala od 30 obrazen (po pancerzu, nie smiertelny - smierc da kaluze), nie tarcza, nie upadek, nie szarza konia,
    ///    nie kopniak: mala plama (0.15-0.3 m wedlug obrazen) przy miejscu trafienia na ziemi; 1 na czlowieka na 2 s, 2 na sekunde w misji, tylko
    ///    gdy zuzyto mniej niz 450 z 600 (reszta dla zgonow), najwyzej 150 na misje; przy RBM bez Blood Through Armour ciecie w kolczuge / plyty
    ///    (RBM daje pot, rany nie widac) - bez plamy;
    ///  - limity: najwyzej 600 decali na misje (gdy przybywa - mniej kaluz na zgon), nowe najwyzej 20 na sekunde (kubel zetonow) i 4 na klatke; kolejka
    ///    najwyzej 240; na komorke 1.75 m najwyzej 3 kaluze i 2 plamy (w bramie / wylomie / murze tarcz krew i tak nie przybywa od 4. warstwy -
    ///    pulap starcza na cale pole); nadmiar liczony;
    ///  - zbior decali "editor_set" (swiadomie, jak gra w misji): bez czasu zycia i bez limitu na komorke (komorki pilnujemy sami) - kaluze leza do
    ///    konca bitwy (Jeff: pobojowisko PO bitwie); "blood_set" (Native decal_sets.xml: 600 s widoczne, 900 s zycia, 4 na komorke, wygaszanie) zgasilby
    ///    kaluze z poczatku dluzszej bitwy, a z C# gra go nie uzywa. deletable = true (gra: Mission.ResetMission -> Scene.ClearRuntimeDecals);
    ///  - materialy raz na misje (AfterStart, glowny watek): Material.GetFromResource, wazny wskaznik, nazwa = zadana (nie zastepczy material); tekstura
    ///    rozproszona tylko do probki w logu (tekstury decali sa w atlasie - brak nie odrzuca materialu); odrzucenia liczone wedlug przyczyny. Decal
    ///    trafia do sceny TYLKO gdy GetMaterial po SetMaterial zwraca TEN material (ten sam wskaznik albo ta sama nazwa) - nigdy decal bez naszego materialu;
    ///  - przed stawianiem (glowny watek): wszystkie liczby skonczone (bez NaN / nieskonczonosci), punkt w pudle sceny (Scene.GetBoundingBox raz na
    ///    misje, jak MissionBoundaryPlacer; zle pudlo - zastepcze +-20 km) - wspolrzedne to jedyne dane z rozgrywki, ktore ida do natywnego zbioru
    ///    decali; ziemia pod srodkiem (GetGroundHeightAndBodyFlagsAtPosition pod Scene.PhysicsAndRayCastLock jak SiegeWeaponMovementComponent; bez
    ///    agentow, ragdolli i wody - CommonCollisionExcludeFlags) w granicy 0.6 m od stop + 0.35 x odleglosc (drabina, mur, wieza, upadek - bez kaluzy),
    ///    nie na ruchomym ani na drabinie (Moveable / Ladder - wieza oblezicza, jak SpawnedItemEntity); pochylenie z dwoch probek 0.5 m obok (normalna, pudlo wzdluz stoku,
    ///    stromo ponad 45 stopni - bez kaluzy; GetGroundHeightAtPosition(out normal) w 1.4.8 zwraca Vec3.Invalid); woda: Mission.GetWaterLevelAtPosition
    ///    ponad ziemia (wzor SpawnedItemEntity.cs:317) - bez kaluzy; renderer decali sprawdzany w kazdym ticku przed stawianiem (jak ShipWaterEffects);
    ///  - wywolania zwrotne silnika (OnAgentRemoved - w petli gry po zachowaniach misji, OnAgentHit - bywa z ticku asynchronicznego) tylko licza i
    ///    dopisuja zadania do kolejki (pod RoutHold.Gate, bez wywolan silnika poza odczytem polozenia, predkosci i trybu ruchu); decale stawia
    ///    OnMissionTick (glowny watek, poza cudza iteracja), wywolania silnika poza blokada;
    ///  - koniec misji (BattleSense.OnEndMission): RemoveDecalInstance kazdego naszego decala ze sceny tej misji, gdy scena ma renderer decali (wzor
    ///    ShipWaterEffects.OnRemoved), potem zwolnienie wrapperow; OnClearScene - silnik sam czysci decale, my tylko zapominamy (bez wywolan);
    ///  - z DismembermentPlus i BattleGore (Agent.CreateBloodBurstAtLimb - same czastki) sie nie dublujemy; gdy w misji jest MoreBlood.BloodPoolBehavior
    ///    (MoreBloodPoolMod - u Jeffa wylaczony od 16.10), ustepujemy;
    ///  - kazde wywolanie silnika w try/catch, wyjatek liczony (RoutHold.Stumble, miejsce "kaluze", pierwsze 3 w sesji do logu) - nic nie gasimy.
    /// Liczniki ida do linii "Bitwa (NB2): misja" (segment "kaluze krwi").
    /// </summary>
    internal static class BloodPools
    {
        internal const int MaxPerMission = 600, MaxSplats = 150, PerSecond = 20, PerTick = 4, QueueMax = 240;
        internal const int SplatMinDamage = 30, SplatsPerSecond = 2, SplatReserveFrom = 450;
        internal const float SplatPerManEvery = 2f;
        internal const float CellSize = 1.75f;
        internal const int PoolsPerCell = 3, SplatsPerCell = 2;
        internal const string DecalSet = "editor_set";
        internal const string MaterialPrefix = "blood_terrain_decal_";
        internal const int Materials = 12;
        private const float PoolUp = 0.4f, SplatUp = 0.25f;   // pol wysokosci pudla decala (skala = pol wymiaru); pudlo wzdluz normalnej ziemi
        private const float GroundTol = 0.6f, GroundTolPerMetre = 0.35f, ProbeAbove = 0.25f;
        private const float SlopeProbe = 0.5f, SlopeEdge = 0.75f, MinNormalZ = 0.7f;
        private const float GrowEvery = 0.1f, GrowTime = 2.5f, GrowFrom = 0.15f;
        private const int GrowMax = 40;

        private struct Req
        {
            public float X, Y, Z, Rs, Rf, Up, Yaw, Due, Tol;
            public int Mat;
            public bool Splat, Fade;
            public long Cell;
        }

        private struct Grow { public Decal D; public float T0; }

        // ---- stan misji: kolejka, limity, komorki i losowanie pod RoutHold.Gate; materialy, decale, rosnace i scena tylko z glownego watku
        private static readonly List<Req> _queue = new List<Req>();
        private static readonly Req[] _take = new Req[PerTick];
        private static readonly Dictionary<int, float> _lastSplat = new Dictionary<int, float>();
        private static readonly HashSet<int> _done = new HashSet<int>();
        private static readonly Dictionary<long, int> _cellPools = new Dictionary<long, int>();
        private static readonly Dictionary<long, int> _cellSplats = new Dictionary<long, int>();
        private static readonly List<Decal> _decals = new List<Decal>();       // wrappery do konca misji (jak ShipWaterEffects._splashFoamDecals)
        private static readonly List<Grow> _grow = new List<Grow>();           // kaluze w trakcie rozjasniania (glowny watek)
        private static readonly Material[] _mats = new Material[Materials];    // wazne materialy na poczatku tablicy (_matCount)
        private static readonly Random _rng = new Random();
        private static WeakReference _m;
        private static bool _on, _cleared;
        private static string _logged;                                         // ostatnia linia "Kaluze krwi (NB2)" (nowa tylko przy zmianie)
        private static int _matCount, _used, _splatsUsed;
        private static float _tokens, _splatTokens, _splatT, _growNext;
        private static float _bx0, _by0, _bz0, _bx1, _by1, _bz1;              // pudlo sceny z zapasem (glowny watek)

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _placed, _pools, _splats, _noMat, _deaths, _bled, _men, _out, _horses, _undead, _waterDeaths, _endDeaths;
        private static int _skipCap, _skipQueue, _skipDense, _skipGround, _skipWater, _skipOutside, _skipBad, _skipNoRend, _skipEnd, _matOk, _matAll;
        private static bool _renderer, _yield;

        internal static void ResetCounters()
        {
            _placed = _pools = _splats = _noMat = _deaths = _bled = _men = _out = _horses = _undead = _waterDeaths = _endDeaths = 0;
            _skipCap = _skipQueue = _skipDense = _skipGround = _skipWater = _skipOutside = _skipBad = _skipNoRend = _skipEnd = _matOk = _matAll = 0;
            _renderer = _yield = false;
        }

        internal static int Activity
        {
            get
            {
                return _placed + _noMat + _deaths + _skipCap + _skipQueue + _skipDense + _skipGround + _skipWater + _skipOutside + _skipBad
                       + _skipNoRend + _skipEnd;
            }
        }

        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | kaluze krwi ").Append(c != null && c.GroundBloodPools ? "TAK" : "NIE").Append(": decali ").Append(_placed).Append(" z ").Append(MaxPerMission)
              .Append(" (kaluze ").Append(_pools).Append(", plamy ").Append(_splats).Append(", bez materialu ").Append(_noMat)
              .Append("), zgonow ").Append(_deaths).Append(" (z krwia ").Append(_bled).Append(": ludzi ").Append(_men).Append(", nieprzytomnych ").Append(_out)
              .Append(", koni ").Append(_horses).Append("; nieumarli ").Append(_undead).Append(", w wodzie ").Append(_waterDeaths)
              .Append(", po zamknieciu misji ").Append(_endDeaths).Append("), pominiete decale: limit misji ").Append(_skipCap)
              .Append(", kolejka ").Append(_skipQueue).Append(", zageszczenie ").Append(_skipDense).Append(", bez ziemi ").Append(_skipGround)
              .Append(", w wodzie ").Append(_skipWater).Append(", poza polem ").Append(_skipOutside).Append(", zle wspolrzedne ").Append(_skipBad)
              .Append(", bez renderera ").Append(_skipNoRend).Append(", przy zamknieciu misji ").Append(_skipEnd)
              .Append("; materialy ").Append(_matOk).Append('/').Append(_matAll)
              .Append(", renderer decali ").Append(_renderer ? "TAK" : "NIE").Append(", MoreBlood ").Append(_yield ? "TAK" : "NIE");
        }

        private static bool Live(Mission m)
        {
            var w = _m;
            return _on && w != null && m != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>Zamykanie misji (nie zwyciestwo): stan inny niz Continuing - nowych zadan juz nie ma.</summary>
        private static bool Closing(Mission m)
        {
            return m.CurrentState != Mission.State.Continuing;
        }

        private static bool F(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

        // ------------------------------------------------------------ poczatek i koniec misji (glowny watek)

        /// <summary>AfterStart (BattleSense, glowny watek, po ustaleniu rodzaju misji): materialy raz na misje, pudlo sceny, ustapienie MoreBlood.</summary>
        internal static void Begin(Mission m)
        {
            // poprzednia misja bez End: wrappery zostawione bez wywolan silnika (scena juz nie istnieje)
            _decals.Clear();
            _grow.Clear();
            for (int i = 0; i < Materials; i++) _mats[i] = null;
            lock (RoutHold.Gate)
            {
                _on = false; _m = null; _cleared = false;
                _queue.Clear(); _lastSplat.Clear(); _done.Clear(); _cellPools.Clear(); _cellSplats.Clear();
                _matCount = 0; _used = 0; _splatsUsed = 0; _tokens = PerSecond; _splatTokens = SplatsPerSecond; _splatT = -1f; _growNext = 0f;
            }
            var c = Settings.Current;
            if (c == null || !c.GroundBloodPools || m == null || !BattleSense.InBattle(m) || m.IsNavalBattle || m.IsNavalRaidBattle) return;

            bool yieldMb = false, rend = false, boxGame = false;
            int ok = 0, rejNull = 0, rejName = 0, noTex = 0;
            string sample = null;
            try
            {
                var list = m.MissionBehaviors;
                int n = list != null ? list.Count : 0;
                for (int i = 0; i < n && !yieldMb; i++)
                {
                    var b = list[i];
                    if (b != null && b.GetType().FullName == "MoreBlood.BloodPoolBehavior") yieldMb = true;
                }
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin (MoreBlood)", e); }
            // pudlo sceny (jak MissionBoundaryPlacer, gdy scena nie ma walk_area) z zapasem; zle - zastepcze +-20 km (rzut na komorke zbioru decali bez przepelnienia)
            _bx0 = _by0 = -20000f; _bx1 = _by1 = 20000f; _bz0 = -5000f; _bz1 = 5000f;
            Scene sc = null;
            try { sc = m.Scene; } catch (Exception e) { sc = null; RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin (scena)", e); }
            if (!yieldMb && (object)sc != null)
            {
                try { rend = sc.HasDecalRenderer(); }
                catch (Exception e) { rend = false; RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin (renderer)", e); }
                try
                {
                    Vec3 mn, mx;
                    sc.GetBoundingBox(out mn, out mx);
                    if (F(mn.x) && F(mn.y) && F(mn.z) && F(mx.x) && F(mx.y) && F(mx.z) && mx.x - mn.x > 10f && mx.y - mn.y > 10f && mx.z >= mn.z
                        && mn.x > -50000f && mn.y > -50000f && mx.x < 50000f && mx.y < 50000f && mn.z > -10000f && mx.z < 10000f)
                    {
                        _bx0 = mn.x - 25f; _by0 = mn.y - 25f; _bz0 = mn.z - 100f;
                        _bx1 = mx.x + 25f; _by1 = mx.y + 25f; _bz1 = mx.z + 100f;
                        boxGame = true;
                    }
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin (pudlo sceny)", e); }
                // materialy nie zaleza od renderera - renderer sprawdza Tick przed kazdym stawianiem
                for (int i = 0; i < Materials; i++)
                {
                    string name = MaterialPrefix + i;
                    try
                    {
                        Material mt = Material.GetFromResource(name);
                        if ((object)mt == null || !mt.IsValid) { rejNull++; continue; }
                        string got = mt.Name;
                        if (got == null || !string.Equals(got, name, StringComparison.OrdinalIgnoreCase)) { rejName++; continue; }   // zastepczy material - nie
                        Texture tex = null;
                        try { tex = mt.GetTexture(Material.MBTextureType.DiffuseMap); } catch { tex = null; }
                        bool hasTex = (object)tex != null && tex.IsValid;
                        if (!hasTex) noTex++;                                       // tekstury decali w atlasie - material przyjety
                        _mats[ok++] = mt;
                        if (sample == null)
                        {
                            try { sample = name + " -> tekstura " + (hasTex ? tex.Name + " " + tex.Width + "x" + tex.Height : "(brak - atlas)"); }
                            catch { sample = name + " -> tekstura (bez nazwy)"; }
                        }
                    }
                    catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin (material " + name + ")", e); }
                }
            }
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _yield = yieldMb;
                _renderer = rend;
                _matOk = ok;
                _matAll = yieldMb || (object)sc == null ? 0 : Materials;
                _matCount = ok;
                _m = new WeakReference(m);
                _on = !yieldMb && (object)sc != null && ok > 0;
            }
            if (!yieldMb && (object)sc != null)
            {
                string line = "Kaluze krwi (NB2): materialy " + ok + "/" + Materials + " (" + MaterialPrefix + "0.." + (Materials - 1) + "; odrzucone: brak materialu "
                              + rejNull + ", inna nazwa " + rejName + "; bez tekstury rozproszonej " + noTex + " - przyjete), renderer decali na starcie "
                              + (rend ? "TAK" : "NIE") + ", pudlo sceny " + (boxGame ? "z gry" : "zastepcze") + ", zbior decali " + DecalSet
                              + ", probka: " + (sample ?? "-") + ".";
                if (line != _logged) { _logged = line; Log.Info(line); }
            }
        }

        /// <summary>OnEndMission (BattleSense, glowny watek): nasze decale zdjete ze sceny tej misji (wzor ShipWaterEffects.OnRemoved), wrappery zwolnione.</summary>
        internal static void End(Mission m)
        {
            bool mine, cleared;
            lock (RoutHold.Gate)
            {
                var w = _m;
                mine = w != null && m != null && ReferenceEquals(w.Target, m);
                cleared = _cleared;
                if (mine && _queue.Count > 0) { _skipEnd += _queue.Count; _used -= _queue.Count; }
                if (mine || w == null) { _queue.Clear(); _on = false; }
            }
            if (!mine) return;
            _grow.Clear();
            Scene sc = null;
            try { sc = m.Scene; } catch { sc = null; }
            if ((object)sc != null && !cleared && _decals.Count > 0)
            {
                bool rend = false;
                try { rend = sc.HasDecalRenderer(); }                                // ShipWaterEffects.OnRemoved (:390): tylko z rendererem
                catch (Exception e) { rend = false; RoutHold.Stumble(RoutHold.SitePools, "BloodPools.End (renderer)", e); }
                if (rend)
                {
                    bool reported = false;
                    for (int i = 0; i < _decals.Count; i++)
                    {
                        var d = _decals[i];
                        if ((object)d == null) continue;
                        try { sc.RemoveDecalInstance(d, DecalSet); }
                        catch (Exception e)
                        {
                            if (!reported) { reported = true; RoutHold.Stumble(RoutHold.SitePools, "BloodPools.End (RemoveDecalInstance)", e); }
                        }
                    }
                }
            }
            _decals.Clear();
            for (int i = 0; i < Materials; i++) _mats[i] = null;
            lock (RoutHold.Gate) { _m = null; _matCount = 0; _lastSplat.Clear(); _done.Clear(); _cellPools.Clear(); _cellSplats.Clear(); }
        }

        /// <summary>OnClearScene (BattleSense): gra czysci decale sceny sama (Mission.ResetMission -> ClearRuntimeDecals) - kolejka precz, bez wywolan silnika.</summary>
        internal static void Cleared(Mission m)
        {
            lock (RoutHold.Gate)
            {
                var w = _m;
                if (w == null || !ReferenceEquals(w.Target, m)) return;
                _cleared = true;          // decale zostaja w liscie (referencje) do End, ale End ich juz nie zdejmuje
                _skipEnd += _queue.Count;
                _used -= _queue.Count;
                _queue.Clear();
                _on = false;
            }
            _grow.Clear();
        }

        // ------------------------------------------------------------ wywolania zwrotne silnika (tylko kolejka)

        private static float Rand(float a, float b) { return a + (float)_rng.NextDouble() * (b - a); }

        private static long CellOf(float x, float y)
        {
            int ix = (int)Math.Floor(x / CellSize), iy = (int)Math.Floor(y / CellSize);
            return ((long)ix << 32) ^ (uint)iy;
        }

        /// <summary>Zadanie decala do kolejki (wolajacy trzyma Gate). yaw &lt; 0 - losowy obrot; tol - granica ziemi od stop.</summary>
        private static bool Push(float x, float y, float z, float r, float yaw, float up, bool splat, bool fade, float due, float tol)
        {
            if (!F(x) || !F(y) || !F(z) || !F(r) || !F(due) || !F(tol) || Math.Abs(x) > 1e6f || Math.Abs(y) > 1e6f) { _skipBad++; return false; }
            if (_queue.Count >= QueueMax) { _skipQueue++; return false; }
            if (_used >= MaxPerMission) { _skipCap++; return false; }
            long cell = CellOf(x, y);
            var cells = splat ? _cellSplats : _cellPools;
            int have;
            cells.TryGetValue(cell, out have);
            if (have >= (splat ? SplatsPerCell : PoolsPerCell)) { _skipDense++; return false; }
            cells[cell] = have + 1;
            var q = new Req
            {
                X = x, Y = y, Z = z, Rs = r, Rf = r * Rand(0.75f, 1f), Up = up,
                Yaw = yaw >= 0f ? yaw : Rand(0f, (float)(Math.PI * 2.0)), Mat = _matCount > 0 ? _rng.Next(_matCount) : 0,
                Splat = splat, Fade = fade, Due = due, Tol = tol, Cell = cell
            };
            _queue.Add(q);
            _used++;
            return true;
        }

        /// <summary>Zadanie nie postawione (Tick): budzet misji i komorka wracaja (wolajacy trzyma Gate).</summary>
        private static void Release(ref Req q)
        {
            if (_used > 0) _used--;
            var cells = q.Splat ? _cellSplats : _cellPools;
            int have;
            if (cells.TryGetValue(q.Cell, out have))
            {
                if (have <= 1) cells.Remove(q.Cell); else cells[q.Cell] = have - 1;
            }
        }

        private static float Yaw(float dx, float dy)
        {
            double a = Math.Atan2(dy, dx) + (_rng.NextDouble() * 2.0 - 1.0) * (25.0 * Math.PI / 180.0);
            if (a < 0) a += Math.PI * 2.0;
            return (float)a;
        }

        /// <summary>OnAgentRemoved (BattleSense) - wywolanie zwrotne silnika w petli gry po zachowaniach misji; wolajacy lapie wyjatki.</summary>
        internal static void OnRemoved(Mission m, Agent victim, Agent affector, AgentState state, in KillingBlow blow)
        {
            if (state != AgentState.Killed && state != AgentState.Unconscious) return;
            var c = Settings.Current;
            if (c == null || !c.GroundBloodPools || victim == null || m == null || !Live(m)) return;
            bool human = victim.IsHuman;
            bool horse = !human && victim.IsMount;
            if (!human && !horse) return;
            if (blow.IsValid && (blow.WeaponRecordWeaponFlags & WeaponFlags.NoBlood) != 0) return;
            bool undead = human ? Undead.Character(victim.Character) : RiderUndead(victim);
            Agent ground = human ? (victim.MountAgent ?? victim) : victim;     // jezdziec: ziemia pod koniem (jak plama w OnHit)
            Vec3 p = ground.Position;
            if (!F(p.x) || !F(p.y) || !F(p.z)) return;
            bool inWater = false;
            // Agent.cs:3319 IsInWater - tryb ruchu WaterSurface / WaterDiving; NIE !IsOnLand: Land = 1, a martwy agent moze miec tryb None (0)
            try { inWater = ground.IsInWater(); } catch { inWater = false; }
            // kierunek upadku: od napastnika (cialo leci od ciosu); bez napastnika - losowo (pod Gate)
            float dx = 0f, dy = 0f;
            if (human && affector != null && affector != victim && affector.IsActive())
            {
                Vec3 a = affector.Position;
                float ex = p.x - a.x, ey = p.y - a.y, len = (float)Math.Sqrt(ex * ex + ey * ey);
                if (len > 0.1f && F(len)) { dx = ex / len; dy = ey / len; }
            }
            // szybki ruch (galop, bieg): cialo laduje dalej wzdluz predkosci
            float ox = 0f, oy = 0f;
            try
            {
                Vec3 v = ground.Velocity;
                float sp = (float)Math.Sqrt(v.x * v.x + v.y * v.y);
                if (F(sp) && sp > 2.5f)
                {
                    float off = Math.Min(sp * 0.4f, 3f);
                    ox = v.x / sp * off; oy = v.y / sp * off;
                    if (human && sp > 4f) { dx = v.x / sp; dy = v.y / sp; }
                }
            }
            catch { ox = oy = 0f; }
            bool known = blow.IsValid;
            bool blunt = known && blow.DamageType == DamageTypes.Blunt;
            bool head = known && (blow.VictimBodyPart == BoneBodyPartType.Head || blow.VictimBodyPart == BoneBodyPartType.Neck);
            bool missile = known && blow.IsMissile;
            int dmg = known ? blow.InflictedDamage : 0;
            bool closing = Closing(m);
            float now = m.CurrentTime;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                if (!Live(m)) return;
                if (!_done.Add(victim.Index)) return;
                _deaths++;
                if (closing) { _endDeaths++; return; }
                if (undead) { _undead++; return; }
                if (inWater) { _waterDeaths++; return; }
                if (dx == 0f && dy == 0f)
                {
                    double t = _rng.NextDouble() * Math.PI * 2.0;
                    dx = (float)Math.Cos(t); dy = (float)Math.Sin(t);
                }
                float bx = p.x + ox, by = p.y + oy, shift = (float)Math.Sqrt(ox * ox + oy * oy);
                int made = 0;
                bool ko = state == AgentState.Unconscious;
                if (human && ko && (blunt || !known))
                {
                    // nokaut obuchem (albo bez znanego ciosu): tylko mala plama przy glowie / tulowiu
                    float along = Rand(0.4f, 0.9f), side = Rand(-0.2f, 0.2f);
                    float x = bx + dx * along - dy * side, y = by + dy * along + dx * side;
                    if (Push(x, y, p.z, Rand(0.2f, 0.35f), -1f, SplatUp, true, false, now + Rand(1.2f, 2f), GroundTol + GroundTolPerMetre * (shift + along)))
                        made++;
                }
                else
                {
                    // im wiecej juz lezy, tym mniej kaluz na zgon - pulap starcza na wiecej zgonow
                    int n;
                    if (horse) n = _used < 400 ? 1 + _rng.Next(2) : 1;
                    else if (ko || blunt) n = 1;
                    else n = _used < 200 ? 1 + _rng.Next(3) : (_used < 400 ? 1 + _rng.Next(2) : 1);
                    if (missile && n > 1) n--;
                    float k = 1f;
                    if (head) k *= 1.15f;
                    if (missile) k *= 0.8f;
                    if (dmg >= 80) k *= 1.08f; else if (known && dmg < 40) k *= 0.9f;
                    float due = now + (horse ? Rand(2f, 3f) : Rand(1.5f, 2.5f));
                    for (int i = 0; i < n; i++)
                    {
                        float along, side = Rand(-0.2f, 0.2f), r;
                        if (horse)
                        {
                            along = Rand(-0.5f, 0.5f);
                            r = Rand(0.9f, 1.4f);
                        }
                        else
                        {
                            // pierwsza pod tulowiem, druga przy biodrach, trzecia przy glowie
                            along = i == 0 ? Rand(0.45f, 0.7f) : (i == 1 ? Rand(0.05f, 0.3f) : Rand(0.9f, 1.15f));
                            if (blunt) r = Rand(0.4f, 0.7f);
                            else if (ko) r = Rand(0.5f, 0.8f);
                            else r = Math.Max(0.45f, Math.Min(1.35f, (i == 0 ? Rand(0.8f, 1.3f) : Rand(0.6f, 1.0f)) * k));
                        }
                        float x = bx + dx * along - dy * side, y = by + dy * along + dx * side;
                        float yaw = horse ? -1f : Yaw(dx, dy);
                        float tol = GroundTol + GroundTolPerMetre * (shift + Math.Abs(along));
                        if (Push(x, y, p.z, r, yaw, PoolUp, false, true, due, tol)) made++;
                        due += Rand(0.3f, 0.6f);
                    }
                }
                if (made > 0)
                {
                    _bled++;
                    if (horse) _horses++;
                    else if (ko) _out++;
                    else _men++;
                }
            }
        }

        private static bool RiderUndead(Agent mount)
        {
            try
            {
                var r = mount.RiderAgent;
                return r != null && Undead.Character(r.Character);
            }
            catch { return false; }
        }

        /// <summary>OnAgentHit (BattleSense) - wywolanie zwrotne silnika (bywa z ticku asynchronicznego); wolajacy lapie wyjatki.</summary>
        internal static void OnHit(Mission m, Agent victim, in Blow blow, in AttackCollisionData col)
        {
            var c = Settings.Current;
            if (c == null || !c.GroundBloodPools || victim == null || m == null || !Live(m)) return;
            if (col.AttackBlockedWithShield || col.CollidedWithShieldOnBack || col.IsFallDamage || col.IsHorseCharge || col.IsAlternativeAttack) return;
            if (!victim.IsHuman || victim.Health < 1f) return;                 // smiertelny: kaluza przyjdzie z OnAgentRemoved
            int dmg = blow.InflictedDamage;
            if (dmg < SplatMinDamage) return;
            if (blow.DamageType != DamageTypes.Cut && blow.DamageType != DamageTypes.Pierce) return;
            var rec = blow.WeaponRecord;
            if (!rec.HasWeapon() || (rec.WeaponFlags & WeaponFlags.NoBlood) != 0) return;
            if (Undead.Character(victim.Character)) return;
            // RBM bez Blood Through Armour: ciecie w kolczuge / plyty daje tylko pot (BattleGore) - bez krwi na ziemi, skoro rany nie widac
            if (!c.BloodThroughArmour && BattleGore.RbmCombat && blow.DamageType == DamageTypes.Cut)
            {
                var mat = BattleGore.MaterialOf(victim, col.VictimHitBodyPart);
                if (mat == ArmorComponent.ArmorMaterialTypes.Chainmail || mat == ArmorComponent.ArmorMaterialTypes.Plate) return;
            }
            Vec3 hit = col.CollisionGlobalPosition;
            Agent ground = victim.MountAgent ?? victim;
            Vec3 feet = ground.Position;
            if (!F(hit.x) || !F(hit.y) || !F(feet.x) || !F(feet.y) || !F(feet.z)) return;
            bool inWater = false;
            try { inWater = ground.IsInWater(); } catch { inWater = false; }
            if (inWater) return;
            // plama na ziemi przy miejscu trafienia (nie dalej niz 0.8 m od stop - krople spadaja przy czlowieku)
            float hx = hit.x - feet.x, hy = hit.y - feet.y, hl = (float)Math.Sqrt(hx * hx + hy * hy);
            if (!F(hl)) return;
            if (hl > 0.8f) { hx *= 0.8f / hl; hy *= 0.8f / hl; hl = 0.8f; }
            float now = m.CurrentTime;
            float rad = 0.15f + 0.15f * Math.Max(0f, Math.Min(1f, (dmg - SplatMinDamage) / 70f));
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                if (!Live(m) || Closing(m)) return;
                float last;
                if (_lastSplat.TryGetValue(victim.Index, out last) && now - last < SplatPerManEvery) return;
                if (_splatsUsed >= MaxSplats || _used >= SplatReserveFrom) return;            // reszta pulapu dla zgonow
                if (_splatT < 0f || now < _splatT) _splatT = now;
                _splatTokens = Math.Min(SplatsPerSecond * 2f, _splatTokens + (now - _splatT) * SplatsPerSecond);
                _splatT = now;
                if (_splatTokens < 1f) return;                                                 // 2 na sekunde - plamy na cala bitwe
                _lastSplat[victim.Index] = now;
                float x = feet.x + hx + Rand(-0.2f, 0.2f), y = feet.y + hy + Rand(-0.2f, 0.2f);
                float r = Math.Max(0.12f, rad * Rand(0.85f, 1.15f));
                if (Push(x, y, feet.z, r, -1f, SplatUp, true, false, now + Rand(0.25f, 0.45f), GroundTol + GroundTolPerMetre * (hl + 0.3f)))
                {
                    _splatsUsed++;
                    _splatTokens -= 1f;
                }
            }
        }

        // ------------------------------------------------------------ stawianie (OnMissionTick, glowny watek)

        /// <summary>OnMissionTick (BattleSense): najwyzej 4 decale na klatke i 20 na sekunde, tylko zadania, ktorych czas nadszedl; wywolania silnika poza blokada.</summary>
        internal static void Tick(Mission m, float dt)
        {
            var c = Settings.Current;
            if (c == null || !c.GroundBloodPools || m == null) return;
            int n = 0;
            // zadania dochodza, dopoki misja trwa (stan Continuing - takze po zwyciestwie); przy zamykaniu misji kolejka precz (liczone)
            bool teardown = Closing(m);
            Scene sc = m.Scene;
            float now = m.CurrentTime;
            lock (RoutHold.Gate)
            {
                if (!Live(m)) return;
                if (teardown)
                {
                    if (_queue.Count > 0) { _skipEnd += _queue.Count; _used -= _queue.Count; _queue.Clear(); }
                    return;
                }
                if ((object)sc == null) return;
                if (dt > 0f && F(dt)) _tokens = Math.Min(PerSecond, _tokens + dt * PerSecond);
                // kolejnosc FIFO wsrod zadan, ktorych czas nadszedl (kaluza po upadku ciala, plama po ciosie)
                for (int i = 0; i < _queue.Count && n < PerTick && _tokens >= 1f;)
                {
                    if (_queue[i].Due <= now)
                    {
                        _take[n++] = _queue[i];
                        _queue.RemoveAt(i);
                        _tokens -= 1f;
                    }
                    else i++;
                }
            }
            if (_grow.Count > 0 && now >= _growNext) GrowTick(now);
            if (n == 0) return;
            bool rend;
            try { rend = sc.HasDecalRenderer(); }                                   // ShipWaterEffects.cs:427 - w kazdym ticku przed stawianiem
            catch (Exception e) { rend = false; RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Tick (renderer)", e); }
            if (!rend)
            {
                lock (RoutHold.Gate)
                {
                    RoutHold.EnsureLocked(m);
                    for (int i = 0; i < n; i++) Release(ref _take[i]);
                    _skipNoRend += n;
                }
                return;
            }
            int pools = 0, splats = 0, noMat = 0, bad = 0, outside = 0, noGround = 0, water = 0;
            uint placedMask = 0;
            for (int i = 0; i < n; i++)
            {
                var q = _take[i];
                // jedna brama na wspolrzedne przed silnikiem: liczby skonczone, punkt w pudle sceny
                if (!F(q.X) || !F(q.Y) || !F(q.Z) || !F(q.Rs) || !F(q.Rf) || !F(q.Up) || !F(q.Yaw) || !F(q.Tol) || q.Rs <= 0f || q.Rf <= 0f)
                { bad++; continue; }
                if (q.X < _bx0 || q.X > _bx1 || q.Y < _by0 || q.Y > _by1 || q.Z < _bz0 || q.Z > _bz1) { outside++; continue; }
                Material mat = q.Mat >= 0 && q.Mat < _matCount ? _mats[q.Mat] : null;
                if ((object)mat == null) { noMat++; continue; }
                try
                {
                    // ziemia pod srodkiem: w granicy od stop, nie ruchoma; pochylenie z dwoch probek obok
                    float co = (float)Math.Cos(q.Yaw), si = (float)Math.Sin(q.Yaw);
                    float h, h1, h2;
                    BodyFlags flags;
                    using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))    // jak SiegeWeaponMovementComponent.cs:500
                    {
                        h = sc.GetGroundHeightAndBodyFlagsAtPosition(new Vec3(q.X, q.Y, q.Z + q.Tol + ProbeAbove), out flags, BodyFlags.CommonCollisionExcludeFlags);
                        if (F(h))
                        {
                            h1 = sc.GetGroundHeightAtPosition(new Vec3(q.X + co * SlopeProbe, q.Y + si * SlopeProbe, h + SlopeEdge), BodyFlags.CommonCollisionExcludeFlags);
                            h2 = sc.GetGroundHeightAtPosition(new Vec3(q.X - si * SlopeProbe, q.Y + co * SlopeProbe, h + SlopeEdge), BodyFlags.CommonCollisionExcludeFlags);
                        }
                        else h1 = h2 = float.NaN;
                    }
                    if (!F(h) || !(Math.Abs(h - q.Z) <= q.Tol) || (flags & (BodyFlags.Moveable | BodyFlags.Ladder)) != 0) { noGround++; continue; }
                    float wl = m.GetWaterLevelAtPosition(new Vec2(q.X, q.Y), true);      // wzor SpawnedItemEntity.cs:317 (misja na ladzie)
                    if (F(wl) && wl > h - 0.05f) { water++; continue; }
                    Vec3 nrm = new Vec3(0f, 0f, 1f);
                    float d1 = h1 - h, d2 = h2 - h;
                    if (F(d1) && F(d2) && Math.Abs(d1) <= SlopeEdge && Math.Abs(d2) <= SlopeEdge)
                    {
                        // (P1 - P0) x (P2 - P0), P1 wzdluz s, P2 wzdluz f
                        float nx = SlopeProbe * (si * d2 - co * d1), ny = -SlopeProbe * (si * d1 + co * d2), nz = SlopeProbe * SlopeProbe;
                        float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        if (F(nl) && nl > 1e-6f)
                        {
                            nrm = new Vec3(nx / nl, ny / nl, nz / nl);
                            if (nrm.z < MinNormalZ) { noGround++; continue; }      // stromo ponad 45 stopni
                        }
                    }
                    // osie pudla: u = normalna, s = kierunek obrotu rzutowany na plaszczyzne ziemi, f = u x s
                    float sd = co * nrm.x + si * nrm.y;
                    float sx = co - nrm.x * sd, sy = si - nrm.y * sd, sz = -nrm.z * sd;
                    float sl = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
                    if (!F(sl) || sl < 1e-4f) { sx = co; sy = si; sz = 0f; sl = 1f; nrm = new Vec3(0f, 0f, 1f); }
                    sx /= sl; sy /= sl; sz /= sl;
                    float fx = nrm.y * sz - nrm.z * sy, fy = nrm.z * sx - nrm.x * sz, fz = nrm.x * sy - nrm.y * sx;
                    MatrixFrame fr = MatrixFrame.Identity;
                    fr.rotation.s = new Vec3(sx * q.Rs, sy * q.Rs, sz * q.Rs);
                    fr.rotation.f = new Vec3(fx * q.Rf, fy * q.Rf, fz * q.Rf);
                    fr.rotation.u = new Vec3(nrm.x * q.Up, nrm.y * q.Up, nrm.z * q.Up);
                    fr.origin = new Vec3(q.X, q.Y, h);
                    if (!F(fr.origin.z) || fr.origin.z < _bz0 || fr.origin.z > _bz1) { outside++; continue; }

                    Decal d = Decal.CreateDecal(null);                              // wzor: ShipWaterEffects.cs:1366
                    if ((object)d == null || !d.IsValid) { noMat++; continue; }
                    d.SetMaterial(mat);                                             // :1367 - material PRZED dodaniem do sceny
                    Material got = d.GetMaterial();
                    // nigdy decal bez NASZEGO materialu (zastepczy domyslny tez nie): ten sam wskaznik albo ta sama nazwa
                    if ((object)got == null || !got.IsValid
                        || (got.Pointer != mat.Pointer && !string.Equals(got.Name, mat.Name, StringComparison.OrdinalIgnoreCase)))
                    { noMat++; continue; }
                    sc.AddDecalInstance(d, DecalSet, true);                         // :1368
                    _decals.Add(d);                                                 // referencja do konca misji (przed ramka - wyjatek nie zgubi decala)
                    placedMask |= 1u << i;
                    d.Frame = fr;                                                   // :1400
                    d.SetVectorArgument(1f, 1f, 0f, 0f);                            // :1406 - cala tekstura (jak blood_decal_b w Native props_other.xml)
                    if (q.Fade)
                    {
                        // kaluza rozlewa sie po upadku: rozjasnianie jak ShipWaterEffects (:1401 SetAlpha(0) po ramce, potem w gore); bez miejsca - od razu pelna
                        if (_grow.Count < GrowMax)
                        {
                            d.SetAlpha(GrowFrom);
                            _grow.Add(new Grow { D = d, T0 = now });
                            if (_growNext < now) _growNext = now + GrowEvery;
                        }
                        else d.SetAlpha(1f);
                    }
                    if (q.Splat) splats++; else pools++;
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Place", e); }
            }
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                // nie postawione (brama, ziemia, woda, material, wyjatek przed dodaniem) - budzet i komorka wracaja
                for (int i = 0; i < n; i++) if ((placedMask & (1u << i)) == 0) Release(ref _take[i]);
                _renderer = true;
                _placed += pools + splats;
                _pools += pools;
                _splats += splats;
                _noMat += noMat;
                _skipBad += bad;
                _skipOutside += outside;
                _skipGround += noGround;
                _skipWater += water;
            }
        }

        /// <summary>Rozjasnianie kaluz (glowny watek, co 0.1 s, najwyzej 40): alfa od 0.15 do 1 przez 2.5 s; wyjatek - kaluza zostaje, jak jest.</summary>
        private static void GrowTick(float now)
        {
            _growNext = now + GrowEvery;
            for (int i = _grow.Count - 1; i >= 0; i--)
            {
                var g = _grow[i];
                float t = (now - g.T0) / GrowTime;
                if (!F(t)) t = 1f;
                bool done = t >= 1f;
                if (t < 0f) t = 0f;
                if (done) t = 1f;
                float s = t * t * (3f - 2f * t);
                try { g.D.SetAlpha(GrowFrom + (1f - GrowFrom) * s); }
                catch (Exception e) { done = true; RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Grow", e); }
                if (done) _grow.RemoveAt(i);
            }
        }
    }
}
