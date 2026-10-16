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
    /// (ShipWaterEffects.cs:1366-1368) -> Frame (:1400) -> SetVectorArgument (:1406); wrappery decali trzyma w tablicy przez cale zycie skryptu,
    /// a przy zdjeciu wola RemoveDecalInstance(decal, "editor_set") (:390-397); wczesniej sprawdza Scene.HasDecalRenderer (:333, :427). Tak samo na
    /// mapie SandBox.View: MapScreen.cs:67-73, MapCursor.cs:61-64, SettlementVisualManager.cs:384-395 - zawsze SetMaterial PRZED AddDecalInstance,
    /// zawsze "editor_set". Materialy krwi sa w grze gotowe: blood_terrain_decal_0..11 (core_game.tpac: materialy decali z render_on_terrain /
    /// render_on_objects / render_on_grass; te same 12 nazw w decal_materials emiterow krwi, Native particle_systems_hardcoded_misc1.xml:50444-50469;
    /// ROT stawia je w scenach jako decal_component, ROT-Map Prefabs MyItems_Siege.xml:11077-11116, Mystaf_Outside.xml:7868). NativeObject liczy
    /// referencje (Construct: IncreaseReferenceCount, finalizer: DecreaseReferenceCount) - wrapper decala trzymamy do konca misji.
    ///
    /// Zasady:
    ///  - tylko bitwa na mapie / oblezenie / kryjowka / bitwa wlasna (BattleSense.InBattle), bez bitew i wypadow morskich (poklad sie rusza, decal
    ///    stoi w swiecie), bez turnieju, areny, treningu; po koncu bitwy (MissionEnded - zwyciestwo / odwrot, BattleEndLogic.MissionEnded) zadnych
    ///    nowych zadan; kolejka sprzed konca dochodzi (20/s), dopoki misja trwa (stan Continuing), przy zamykaniu misji - precz (liczone);
    ///  - zgon (Killed) albo nokaut (Unconscious) czlowieka: 1-3 nakladajace sie kaluze (promien 0.6-1.3 m, losowy obrot, lekka elipsa) wzdluz ciala -
    ///    od stop w strone upadku (od napastnika; bez napastnika losowo); nokaut 1-2, obuch 1 mniejsza; kon 1-2 (0.9-1.4 m); nieumarli ROT (Undead) bez
    ///    krwi; bron NoBlood bez krwi; raz na agenta;
    ///  - cios ciety / klujacy od 30 obrazen (po pancerzu, nie smiertelny - smierc da kaluze), nie tarcza, nie upadek, nie szarza konia, nie kopniak:
    ///    mala plama (0.25-0.45 m) przy miejscu trafienia na ziemi; 1 na czlowieka na 2 s, najwyzej 120 na misje;
    ///  - limity: najwyzej 600 decali na misje (gdy przybywa - mniej kaluz na zgon, zeby starczylo na wiecej zgonow), nowe najwyzej 20 na sekunde
    ///    (kubel zetonow) i 4 na klatke; kolejka najwyzej 240 (nadmiar liczony);
    ///  - zbior decali "editor_set" (swiadomie, jak gra w misji): bez czasu zycia i bez limitu na komorke - kaluze leza do konca bitwy (Jeff:
    ///    pobojowisko PO bitwie); "blood_set" (Native decal_sets.xml: 600 s widoczne, 900 s zycia, 4 na komorke, wygaszanie) zgasilby kaluze z
    ///    poczatku dluzszej bitwy i kasowal je w gestej walce, a z C# gra go nie uzywa. deletable = true (gra: Mission.ResetMission ->
    ///    Scene.ClearRuntimeDecals). Pulap i tempo pilnujemy sami (600, 20/s);
    ///  - materialy raz na misje (AfterStart, glowny watek): Material.GetFromResource, wazny wskaznik, nazwa = zadana (nie zastepczy material),
    ///    tekstura rozproszona wazna; bez ani jednego waznego materialu albo bez renderera decali - zadnych decali w tej misji. Decal trafia do
    ///    sceny TYLKO gdy GetMaterial po SetMaterial zwraca wazny material - nigdy decal bez materialu;
    ///  - wywolania zwrotne silnika (OnAgentRemoved - w petli gry po zachowaniach misji, OnAgentHit - bywa z ticku asynchronicznego) tylko licza i
    ///    dopisuja zadania do kolejki (pod RoutHold.Gate, bez wywolan silnika poza odczytem polozenia); decale stawia OnMissionTick (glowny watek,
    ///    poza cudza iteracja), wywolania silnika poza blokada;
    ///  - koniec misji (BattleSense.OnEndMission): RemoveDecalInstance kazdego naszego decala ze sceny tej misji (wzor ShipWaterEffects), potem
    ///    zwolnienie wrapperow; OnClearScene - silnik sam czysci decale, my tylko zapominamy (bez wywolan);
    ///  - z DismembermentPlus i BattleGore (Agent.CreateBloodBurstAtLimb - same czastki) sie nie dublujemy; gdy w misji jest MoreBlood.BloodPoolBehavior
    ///    (MoreBloodPoolMod - u Jeffa wylaczony od 16.10), ustepujemy;
    ///  - kazde wywolanie silnika w try/catch, wyjatek liczony (RoutHold.Stumble, miejsce "kaluze", pierwsze 3 w sesji do logu) - nic nie gasimy.
    /// Liczniki ida do linii "Bitwa (NB2): misja" (segment "kaluze krwi").
    /// </summary>
    internal static class BloodPools
    {
        internal const int MaxPerMission = 600, MaxSplats = 120, PerSecond = 20, PerTick = 4, QueueMax = 240;
        internal const int SplatMinDamage = 30;
        internal const float SplatPerManEvery = 2f;
        internal const string DecalSet = "editor_set";
        internal const string MaterialPrefix = "blood_terrain_decal_";
        internal const int Materials = 12;
        private const float PoolUp = 0.8f, SplatUp = 0.5f;   // pol wysokosci pudla decala (skala = pol wymiaru) - stok i cialo w pudle

        private struct Req { public float X, Y, Z, Rs, Rf, Up, Yaw; public int Mat; public bool Splat; }

        // ---- stan misji: kolejka, limity i losowanie pod RoutHold.Gate; materialy, decale i scena tylko z glownego watku
        private static readonly List<Req> _queue = new List<Req>();
        private static readonly Req[] _take = new Req[PerTick];
        private static readonly Dictionary<int, float> _lastSplat = new Dictionary<int, float>();
        private static readonly HashSet<int> _done = new HashSet<int>();
        private static readonly List<Decal> _decals = new List<Decal>();       // wrappery do konca misji (jak ShipWaterEffects._splashFoamDecals)
        private static readonly Material[] _mats = new Material[Materials];    // wazne materialy na poczatku tablicy (_matCount)
        private static readonly Random _rng = new Random();
        private static WeakReference _m;
        private static bool _on, _cleared, _sampleLogged;
        private static int _matCount, _used, _splatsUsed;
        private static float _tokens;

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _placed, _pools, _splats, _noMat, _men, _out, _horses, _undead, _skipCap, _skipQueue, _skipEnd, _matOk, _matAll;
        private static bool _renderer, _yield;

        internal static void ResetCounters()
        {
            _placed = _pools = _splats = _noMat = _men = _out = _horses = _undead = _skipCap = _skipQueue = _skipEnd = _matOk = _matAll = 0;
            _renderer = _yield = false;
        }

        internal static int Activity { get { return _placed + _noMat + _skipCap + _skipQueue + _skipEnd + _undead; } }

        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | kaluze krwi ").Append(c != null && c.GroundBloodPools ? "TAK" : "NIE").Append(": decali ").Append(_placed).Append(" z ").Append(MaxPerMission)
              .Append(" (kaluze ").Append(_pools).Append(", plamy ").Append(_splats).Append(", bez materialu ").Append(_noMat)
              .Append("), zgonow ").Append(_men + _out + _horses).Append(" (ludzi ").Append(_men).Append(", nieprzytomnych ").Append(_out)
              .Append(", koni ").Append(_horses).Append(", nieumarli pominieci ").Append(_undead).Append("), pominiete decale: limit misji ").Append(_skipCap)
              .Append(", kolejka ").Append(_skipQueue).Append(", koniec bitwy ").Append(_skipEnd).Append("; materialy ").Append(_matOk).Append('/').Append(_matAll)
              .Append(", renderer decali ").Append(_renderer ? "TAK" : "NIE").Append(", MoreBlood ").Append(_yield ? "TAK" : "NIE");
        }

        private static bool Live(Mission m)
        {
            var w = _m;
            return _on && w != null && m != null && ReferenceEquals(w.Target, m);
        }

        private static bool Ended(Mission m)
        {
            return m.MissionEnded || m.CurrentState != Mission.State.Continuing;
        }

        // ------------------------------------------------------------ poczatek i koniec misji (glowny watek)

        /// <summary>AfterStart (BattleSense, glowny watek, po ustaleniu rodzaju misji): materialy raz na misje, renderer decali, ustapienie MoreBlood.</summary>
        internal static void Begin(Mission m)
        {
            // poprzednia misja bez End: wrappery zostawione bez wywolan silnika (scena juz nie istnieje)
            _decals.Clear();
            for (int i = 0; i < Materials; i++) _mats[i] = null;
            lock (RoutHold.Gate)
            {
                _on = false; _m = null; _cleared = false;
                _queue.Clear(); _lastSplat.Clear(); _done.Clear();
                _matCount = 0; _used = 0; _splatsUsed = 0; _tokens = PerSecond;
            }
            var c = Settings.Current;
            if (c == null || !c.GroundBloodPools || m == null || !BattleSense.InBattle(m) || m.IsNavalBattle || m.IsNavalRaidBattle) return;

            bool yieldMb = false, rend = false;
            int ok = 0;
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
            if (!yieldMb)
            {
                try
                {
                    Scene sc = m.Scene;
                    rend = (object)sc != null && sc.HasDecalRenderer();
                }
                catch (Exception e) { rend = false; RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin (renderer)", e); }
                if (rend)
                {
                    for (int i = 0; i < Materials; i++)
                    {
                        string name = MaterialPrefix + i;
                        try
                        {
                            Material mt = Material.GetFromResource(name);
                            if ((object)mt == null || !mt.IsValid) continue;
                            string got = mt.Name;
                            if (got == null || !string.Equals(got, name, StringComparison.OrdinalIgnoreCase)) continue;   // zastepczy material - nie
                            Texture tex = mt.GetTexture(Material.MBTextureType.DiffuseMap);
                            if ((object)tex == null || !tex.IsValid) continue;
                            _mats[ok++] = mt;
                            if (sample == null)
                            {
                                try { sample = name + " -> tekstura " + tex.Name + " " + tex.Width + "x" + tex.Height; }
                                catch { sample = name + " -> tekstura (bez nazwy)"; }
                            }
                        }
                        catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Begin (material " + name + ")", e); }
                    }
                }
            }
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _yield = yieldMb;
                _renderer = rend;
                _matOk = ok;
                _matAll = yieldMb || !rend ? 0 : Materials;
                _matCount = ok;
                _m = new WeakReference(m);
                _on = !yieldMb && rend && ok > 0;
            }
            if (!_sampleLogged && rend && !yieldMb)
            {
                _sampleLogged = true;
                Log.Info("Kaluze krwi (NB2): materialy " + ok + "/" + Materials + " (" + MaterialPrefix + "0.." + (Materials - 1) + "), zbior decali "
                         + DecalSet + ", probka: " + (sample ?? "-") + ".");
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
            Scene sc = null;
            try { sc = m.Scene; } catch { sc = null; }
            if ((object)sc != null && !cleared)
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
            _decals.Clear();
            for (int i = 0; i < Materials; i++) _mats[i] = null;
            lock (RoutHold.Gate) { _m = null; _matCount = 0; _lastSplat.Clear(); _done.Clear(); }
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
                _queue.Clear();
                _on = false;
            }
        }

        // ------------------------------------------------------------ wywolania zwrotne silnika (tylko kolejka)

        private static float Rand(float a, float b) { return a + (float)_rng.NextDouble() * (b - a); }

        /// <summary>Zadanie kaluzy do kolejki (wolajacy trzyma Gate i sprawdzil limit misji).</summary>
        private static bool Push(float x, float y, float z, float r, float up, bool splat)
        {
            if (_queue.Count >= QueueMax) { _skipQueue++; return false; }
            if (_used >= MaxPerMission) { _skipCap++; return false; }
            var q = new Req
            {
                X = x, Y = y, Z = z, Rs = r, Rf = r * Rand(0.75f, 1f), Up = up,
                Yaw = Rand(0f, (float)(Math.PI * 2.0)), Mat = _matCount > 0 ? _rng.Next(_matCount) : 0, Splat = splat
            };
            _queue.Add(q);
            _used++;
            return true;
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
            Vec3 p = victim.Position;
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) return;
            // kierunek upadku: od napastnika (cialo leci od ciosu); bez napastnika - losowo (pod Gate)
            float dx = 0f, dy = 0f;
            if (human && affector != null && affector != victim && affector.IsActive())
            {
                Vec3 a = affector.Position;
                float ex = p.x - a.x, ey = p.y - a.y, len = (float)Math.Sqrt(ex * ex + ey * ey);
                if (len > 0.1f && !float.IsNaN(len)) { dx = ex / len; dy = ey / len; }
            }
            bool blunt = blow.IsValid && blow.DamageType == DamageTypes.Blunt;
            bool ended = Ended(m);
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                if (!Live(m)) return;
                if (ended) { _skipEnd++; return; }
                if (undead) { _undead++; return; }
                if (!_done.Add(victim.Index)) return;
                if (dx == 0f && dy == 0f)
                {
                    double t = _rng.NextDouble() * Math.PI * 2.0;
                    dx = (float)Math.Cos(t); dy = (float)Math.Sin(t);
                }
                // im wiecej juz lezy, tym mniej kaluz na zgon - pulap starcza na wiecej zgonow
                int n;
                if (horse) n = _used < 400 ? 1 + _rng.Next(2) : 1;
                else if (blunt) n = 1;
                else if (state == AgentState.Unconscious) n = _used < 300 ? 1 + _rng.Next(2) : 1;
                else n = _used < 200 ? 1 + _rng.Next(3) : (_used < 400 ? 1 + _rng.Next(2) : 1);
                int made = 0;
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
                        r = blunt ? Rand(0.6f, 0.9f) : (i == 0 ? Rand(0.8f, 1.3f) : Rand(0.6f, 1.0f));
                    }
                    float x = p.x + dx * along - dy * side, y = p.y + dy * along + dx * side;
                    if (Push(x, y, p.z, r, PoolUp, false)) made++;
                }
                if (made > 0)
                {
                    if (horse) _horses++;
                    else if (state == AgentState.Unconscious) _out++;
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
            if (blow.InflictedDamage < SplatMinDamage) return;
            if (blow.DamageType != DamageTypes.Cut && blow.DamageType != DamageTypes.Pierce) return;
            var rec = blow.WeaponRecord;
            if (!rec.HasWeapon() || (rec.WeaponFlags & WeaponFlags.NoBlood) != 0) return;
            if (Undead.Character(victim.Character)) return;
            Vec3 hit = col.CollisionGlobalPosition;
            Agent ground = victim.MountAgent ?? victim;
            Vec3 feet = ground.Position;
            if (float.IsNaN(hit.x) || float.IsNaN(hit.y) || float.IsNaN(feet.z)) return;
            // plama na ziemi przy miejscu trafienia (nie dalej niz 0.8 m od stop - krople spadaja przy czlowieku)
            float hx = hit.x - feet.x, hy = hit.y - feet.y, hl = (float)Math.Sqrt(hx * hx + hy * hy);
            if (hl > 0.8f) { hx *= 0.8f / hl; hy *= 0.8f / hl; }
            float now = m.CurrentTime;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                if (!Live(m) || Ended(m)) return;
                float last;
                if (_lastSplat.TryGetValue(victim.Index, out last) && now - last < SplatPerManEvery) return;
                if (_splatsUsed >= MaxSplats) return;
                _lastSplat[victim.Index] = now;
                float x = feet.x + hx + Rand(-0.25f, 0.25f), y = feet.y + hy + Rand(-0.25f, 0.25f);
                if (Push(x, y, feet.z, Rand(0.25f, 0.45f), SplatUp, true)) { _splatsUsed++; }
            }
        }

        // ------------------------------------------------------------ stawianie (OnMissionTick, glowny watek)

        /// <summary>OnMissionTick (BattleSense): najwyzej 4 decale na klatke i 20 na sekunde; wywolania silnika poza blokada.</summary>
        internal static void Tick(Mission m, float dt)
        {
            var c = Settings.Current;
            if (c == null || !c.GroundBloodPools || m == null) return;
            int n = 0;
            // zadania sprzed konca bitwy (MissionEnded = zwyciestwo / odwrot - BattleEndLogic.MissionEnded) dochodza w tempie 20/s, dopoki misja
            // trwa (stan Continuing); nowych po koncu nie przyjmujemy (OnRemoved / OnHit), przy zamykaniu misji kolejka precz
            bool teardown = m.CurrentState != Mission.State.Continuing;
            Scene sc = m.Scene;
            lock (RoutHold.Gate)
            {
                if (!Live(m)) return;
                if (teardown)
                {
                    if (_queue.Count > 0) { _skipEnd += _queue.Count; _used -= _queue.Count; _queue.Clear(); }
                    return;
                }
                if ((object)sc == null) return;
                if (dt > 0f && !float.IsNaN(dt)) _tokens = Math.Min(PerSecond, _tokens + dt * PerSecond);
                while (n < PerTick && _tokens >= 1f && _queue.Count > 0)
                {
                    _take[n++] = _queue[0];
                    _queue.RemoveAt(0);
                    _tokens -= 1f;
                }
            }
            if (n == 0) return;
            int pools = 0, splats = 0, noMat = 0;
            for (int i = 0; i < n; i++)
            {
                var q = _take[i];
                Material mat = q.Mat >= 0 && q.Mat < _matCount ? _mats[q.Mat] : null;
                if ((object)mat == null) { noMat++; continue; }
                try
                {
                    Decal d = Decal.CreateDecal(null);                              // wzor: ShipWaterEffects.cs:1366
                    if ((object)d == null || !d.IsValid) { noMat++; continue; }
                    d.SetMaterial(mat);                                             // :1367 - material PRZED dodaniem do sceny
                    Material got = d.GetMaterial();
                    if ((object)got == null || !got.IsValid) { noMat++; continue; }  // nigdy decal bez materialu (decal nie trafia do sceny)
                    sc.AddDecalInstance(d, DecalSet, true);                         // :1368
                    _decals.Add(d);                                                 // referencja do konca misji (przed ramka - wyjatek nie zgubi decala)
                    float co = (float)Math.Cos(q.Yaw), si = (float)Math.Sin(q.Yaw);
                    MatrixFrame fr = MatrixFrame.Identity;
                    fr.rotation.s = new Vec3(co * q.Rs, si * q.Rs, 0f);
                    fr.rotation.f = new Vec3(-si * q.Rf, co * q.Rf, 0f);
                    fr.rotation.u = new Vec3(0f, 0f, q.Up);
                    fr.origin = new Vec3(q.X, q.Y, q.Z);
                    d.Frame = fr;                                                   // :1400
                    d.SetVectorArgument(1f, 1f, 0f, 0f);                            // :1406 - cala tekstura (jak blood_decal_b w Native props_other.xml)
                    if (q.Splat) splats++; else pools++;
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SitePools, "BloodPools.Place", e); }
            }
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _placed += pools + splats;
                _pools += pools;
                _splats += splats;
                _noMat += noMat;
            }
        }
    }
}
