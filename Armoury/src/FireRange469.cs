using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 469 (Jeff 12.11: "lucznicy zaczynaja strzelac na poczatku bitwy nawet jak nie maja zasiegu ... strzaly laduja w polu przed wojskiem, moze niech
    /// sprawdzaja czy jest zasieg i dopiero wtedy strzelaja"): lucznik / kusznik AI pieszo trzyma ogien, dopoki cel nie jest w zasiegu bojowym jego broni.
    /// Dowod (badanie 469, dekompilacja): decyzja o strzale jest natywna i patrzy na zasieg balistyczny (Agent.GetMissileRange, mb_full.cs:13520), nie na
    /// skuteczny; dla piechoty nie ma zadnego progu "strzelaj od X m"; RBM wlacza strzelcom IgnoreAmmoLimitForRangeCalculation (rbmai.cs:2509) i zdejmuje
    /// skrocenie zasiegu pogoda (rbmai.cs:3026). Przy krancu zasiegu, z rozrzutem pionowym RBM, duza czesc strzal spada przed celem.
    /// Zasieg bojowy G liczymy z tej samej predkosci, z ktora RBM naprawde wystrzeliwuje strzale (Utilities.calculateMissileSpeed, rbmcombat.cs:5466-5500:
    /// naciag w funtach z pola MissileSpeed + hit_points modyfikatora jakosci, waga strzaly z kolczana) - wiec zasieg zalezy od naciagu: G = 0.85 x zasieg
    /// maksymalny z oporem powietrza (AirFrictionArrow, wzor silnika v -= k v^2 dt; symulacja w C#, przechowana per predkosc i roznica wysokosci).
    /// Predkosci ani obrazen NIE zmieniamy (RBM liczy je sam; podwojne liczenie = rozjechane celowanie, badanie P3).
    /// Regula (klucz ArchersHoldFireOutOfRange; NIE = ten sam przeglad na sucho, bez wywolan silnika): bitwa polowa, czlowiek AI pieszo z lukiem lub kusza w reku
    /// (ludzie gracza pod SI tez; gracz nie): odleglosc do celu silnika (GetTargetAgent), bez celu - do skraju najblizszego szyku wroga (CachedClosestEnemyFormation).
    ///  d > 1.1 G - Agent.SetFiringOrder(HoldYourFire) (ponawiane przy kazdym przegladzie - zmiana rozkazu szyku nadpisuje rozkaz czlowieka, Formation.cs:69551);
    ///  d <= G - rozkaz ognia szyku wraca (Formation.FiringOrder); szyk z HoldYourFire - nie ruszamy.
    /// Zwolnienie od razu: wrog w 6 m (czlowiek walczy o zycie, jak 310), bron biala / pusty kolczan, panika / odwrot, kon, przejecie przez 204/283/148/447, chorazy.
    /// Pominieci: konni (RBM AiRangedHorsebackMissileRange 0.35), szyk piechoty (prefiks RBM OverrideAgent.SetFiringOrder, rbmai.cs:4403 - jak 310),
    /// poza szykiem, machiny, nieznany uzytek broni (RBM daje mu 10 m/s - nie zgadujemy). Jeden zarzadca wstrzyman z 310: rozkaz szyku wraca dopiero,
    /// gdy zaden powod wstrzymania nie trwa (CleanShot.Held / FireRange469.Holds).
    /// Budzet: przeglad wszystkich ludzi misji co ok. 0.5 s (ludzi na klatke wg dt, 8..96), zapytan o wroga w 6 m najwyzej 16 na klatke, nowych symulacji lotu najwyzej 2 w przegladzie i 2 przy strzalach (wynik przechowany per predkosc i koszyk wysokosci).
    /// Pomiar (linia "Ogien w zasiegu (469)" raz na bitwe polowa): strzaly AI z luku / kuszy wg odleglosci do celu, strzaly poza G, pierwsza salwa kazdej strony
    /// (6 s od pierwszego strzalu: sredni dystans vs srednie G i natywny GetMissileRange), wstrzymania, czas, bronie (naciag, predkosc, zasieg, G, natywny).
    /// Linia "Zasieg lukow (469)" na start sesji: tabela kilku typowych broni. Potkniecia liczone (RoutHold miejsce "tarcze"), funkcji nie gasimy.
    /// </summary>
    internal static class FireRange469
    {
        internal const float Every = 0.5f, HoldOver = 1.1f, Eff = 0.85f, ThreatR = 6f, FirstVolley = 6f, ArrowW = 0.043f, BoltW = 0.06f, DzStep = 5f;
        internal const int MinPer = 8, MaxPer = 96, NearPer = 16, SimPer = 2, WeapMax = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class St
        {
            public Agent A;
            public bool Held;
            public float Since;
        }

        private struct WKey : IEquatable<WKey>
        {
            public ItemObject W; public ItemModifier M; public ItemObject Ammo;
            public bool Equals(WKey o) { return ReferenceEquals(W, o.W) && ReferenceEquals(M, o.M) && ReferenceEquals(Ammo, o.Ammo); }
            public override bool Equals(object o) { return o is WKey && Equals((WKey)o); }
            public override int GetHashCode()
            {
                int h = W != null ? W.GetHashCode() : 0;
                h = h * 31 + (M != null ? M.GetHashCode() : 0);
                return h * 31 + (Ammo != null ? Ammo.GetHashCode() : 0);
            }
        }

        private sealed class Wp
        {
            public string Name, Usage;
            public int Lb, V;          // naciag (funty, z hit_points modyfikatora) i predkosc RBM (m/s); bez RBM Lb = 0, V = predkosc gry
            public bool Ok;            // false = nieznany uzytek (RBM 10 m/s) - czlowiek pominiety
            public int Shots, NatN, GN;
            public float NatSum, GSum;
        }

        private static WeakReference _m;
        private static bool _live, _key, _written, _rbm;
        private static string _kind = "?";
        private static float _k = 0.003f;
        private static int _rot, _frameNear, _frameSim;
        private static long _sumTicks, _maxTicks;

        private static readonly Dictionary<Agent, St> By = new Dictionary<Agent, St>();
        private static readonly Dictionary<WKey, Wp> Weap = new Dictionary<WKey, Wp>();
        private static readonly Dictionary<int, float> RangeCache = new Dictionary<int, float>();   // klucz: v * 64 + koszyk roznicy wysokosci
        private static readonly List<Wp> WeapSeen = new List<Wp>();
        private static readonly MBList<Agent> TBuf = new MBList<Agent>();
        private static readonly List<Agent> Drop = new List<Agent>();
        // naciag z danych broni zapisany na start sesji (poza misja - bez okna, w ktorym RBM podmienia MissileSpeed na m/s przy spawnie / strzale)
        private static readonly Dictionary<ItemObject, int> LbAtStart = new Dictionary<ItemObject, int>();

        // liczniki misji
        private static int _scanned, _archers, _holds, _heldMen, _reHold, _wouldHold, _stum, _overNear, _overSim;
        private static int _relRange, _relNear, _relWeapon, _relPanic, _relTaken, _relMount, _relForm, _relNoFoe, _relEnd, _relDead;
        private static int _skMount, _skInf, _skDetached, _skObject, _skUsage, _skFormHold, _skPlayer, _skNoAmmo, _skHunt, _skBanner, _skPanic;
        private static int _heldNoTarget, _shots, _shotsOver, _shotsHeld, _shotsNoTarget;
        private static readonly int[] ShotBy = new int[5];
        private static float _holdSecs, _holdMax;
        private static int _holdN;
        private static readonly HashSet<Agent> HeldEver = new HashSet<Agent>();
        // pierwsza salwa: [0] obrona, [1] atak
        private static readonly float[] FvT0 = new float[2], FvD = new float[2], FvG = new float[2], FvNat = new float[2];
        private static readonly int[] FvN = new int[2], FvNatN = new int[2], FvGN = new int[2];

        internal static bool Live { get { return _live; } }

        // ------------------------------------------------------------ start misji / koniec

        /// <summary>ShieldsUp.Begin (po CleanShot.Begin): stan od zera; tylko bitwa polowa (jak 310).</summary>
        internal static void Begin(Mission m, Settings c)
        {
            Clear();
            _live = false;
            _written = false;
            _m = null;
            if (m == null) return;
            bool field = false;
            try { field = m.IsFieldBattle && !m.IsNavalBattle && !m.IsNavalRaidBattle && !HideoutAlarm.IsHideout(m); } catch { field = false; }
            if (!field) return;
            _m = new WeakReference(m);
            _kind = MissionTeams.Kind(m);
            _key = c == null || c.ArchersHoldFireOutOfRange;
            _rbm = RangedRank.RbmLoaded;
            _k = Friction();
            _live = true;
        }

        private static float Friction()
        {
            try
            {
                float k = ManagedParameters.Instance.GetManagedParameter(ManagedParametersEnum.AirFrictionArrow);
                if (k > 0f && k < 0.1f) return k;
            }
            catch { }
            return 0.003f;
        }

        private static void Clear()
        {
            By.Clear(); Weap.Clear(); WeapSeen.Clear(); TBuf.Clear(); Drop.Clear(); HeldEver.Clear();
            _rot = _frameNear = _frameSim = 0;
            _sumTicks = _maxTicks = 0;
            _scanned = _archers = _holds = _heldMen = _reHold = _wouldHold = _stum = _overNear = _overSim = 0;
            _relRange = _relNear = _relWeapon = _relPanic = _relTaken = _relMount = _relForm = _relNoFoe = _relEnd = _relDead = 0;
            _skMount = _skInf = _skDetached = _skObject = _skUsage = _skFormHold = _skPlayer = _skNoAmmo = _skHunt = _skBanner = _skPanic = 0;
            _heldNoTarget = _shots = _shotsOver = _shotsHeld = _shotsNoTarget = 0;
            Array.Clear(ShotBy, 0, ShotBy.Length);
            _holdSecs = _holdMax = 0f; _holdN = 0;
            for (int i = 0; i < 2; i++) { FvT0[i] = -1f; FvD[i] = FvG[i] = FvNat[i] = 0f; FvN[i] = FvNatN[i] = FvGN[i] = 0; }
        }

        private static bool Mine(Mission m)
        {
            var w = _m;
            return _live && m != null && w != null && ReferenceEquals(w.Target, m);
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            RoutHold.Stumble(RoutHold.SiteShields, where, e);
        }

        /// <summary>310 (CleanShot.EndHold): czy 469 wciaz trzyma ogien tego czlowieka - wtedy 310 nie przywraca rozkazu szyku.</summary>
        internal static bool Holds(Agent a)
        {
            if (!_live || a == null) return false;
            St s;
            return By.TryGetValue(a, out s) && s.Held;
        }

        // ------------------------------------------------------------ bron: naciag, predkosc RBM, zasieg

        /// <summary>Bron w reku (luk / kusza) i jej dane; null = brak broni strzeleckiej albo amunicji.</summary>
        private static Wp Weapon(Agent a, bool needAmmo, out bool noAmmo)
        {
            noAmmo = false;
            var eq = a.Equipment;
            if (eq == null) return null;
            var wi = a.GetPrimaryWieldedItemIndex();
            if (wi < EquipmentIndex.WeaponItemBeginSlot || wi >= EquipmentIndex.NumAllWeaponSlots) return null;
            var w = eq[wi];
            if (w.IsEmpty) return null;
            var u = w.CurrentUsageItem;
            if (u == null || (u.WeaponClass != WeaponClass.Bow && u.WeaponClass != WeaponClass.Crossbow)) return null;
            if (needAmmo && eq.GetAmmoAmount(wi) <= 0) { noAmmo = true; return null; }
            // waga strzaly jak RBM (rbmcombat.cs:4544-4554): naladowana amunicja, inaczej pierwszy kolczan z zapasem; bez niej 0.07 kg
            ItemObject ammo = null;
            float aw = 0.07f;
            var ld = w.AmmoWeapon;
            if (!ld.IsEmpty && ld.Item != null && ld.Amount > 0) { ammo = ld.Item; aw = ld.GetWeight() / ld.Amount; }
            else
            {
                var want = u.WeaponClass == WeaponClass.Bow ? ItemObject.ItemTypeEnum.Arrows : ItemObject.ItemTypeEnum.Bolts;
                for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
                {
                    var x = eq[i];
                    if (x.IsEmpty || x.Item == null || x.Item.ItemType != want || x.Amount <= 0) continue;
                    ammo = x.Item; aw = x.GetWeight() / x.Amount;
                    break;
                }
            }
            var key = new WKey { W = w.Item, M = w.ItemModifier, Ammo = ammo };
            Wp p;
            if (Weap.TryGetValue(key, out p)) return p;
            p = new Wp { Name = w.Item != null ? w.Item.StringId : "?", Usage = u.ItemUsage ?? "" };
            if (_rbm)
            {
                int lb;
                if (w.Item == null || !LbAtStart.TryGetValue(w.Item, out lb)) lb = u.MissileSpeed;
                int hp = 0;
                try { if (w.ItemModifier != null) hp = w.ItemModifier.ModifyHitPoints((short)50) - 50; } catch { hp = 0; }
                p.Lb = lb + hp;
                p.Ok = Known(p.Usage);
                p.V = p.Ok ? RbmSpeed(aw, p.Usage, p.Lb) : 0;
            }
            else
            {
                p.Lb = 0;
                try { p.V = w.GetModifiedMissileSpeedForCurrentUsage(); } catch { p.V = u.MissileSpeed; }
                p.Ok = p.V > 5;
            }
            Weap[key] = p;
            if (WeapSeen.Count < 64) WeapSeen.Add(p);
            return p;
        }

        private static bool Known(string usage)
        {
            return usage == "bow" || usage == "long_bow" || usage == "crossbow" || usage == "crossbow_fast";
        }

        /// <summary>Predkosc strzaly wedlug RBM Utilities.calculateMissileSpeed (rbmcombat.cs:5466-5500) - co do wzoru.</summary>
        internal static int RbmSpeed(float ammoWeight, string usage, int drawWeight)
        {
            float stroke = 0.635f, eff = 0.9f, c = 0.00015f;
            if (usage == "long_bow") { eff = 0.835f; c = 0.00018f; }
            else if (usage == "crossbow" || usage == "crossbow_fast") { stroke = 0.508f; eff = 0.88f; }
            else if (usage != "bow") return 10;
            double e = 0.5f * ((float)drawWeight * 4.448f) * stroke * eff;
            float m = ammoWeight + (float)drawWeight * c;
            if (!(m > 0f)) return 10;
            return (int)Math.Floor(Math.Sqrt(e * 2.0 / (double)m));
        }

        /// <summary>Zasieg maksymalny z oporem (m), wystrzal z 1.5 m nad ziemia, cel o dz wyzej. Przechowany per (v, koszyk 5 m). -1 = bez budzetu klatki.</summary>
        private static float MaxRange(int v, float dz, bool budget)
        {
            int b = (int)Math.Round(dz / DzStep);
            if (b < -6) b = -6; else if (b > 6) b = 6;
            int key = v * 64 + (b + 6);
            float r;
            if (RangeCache.TryGetValue(key, out r)) return r;
            if (budget && _frameSim >= SimPer) { _overSim++; return -1f; }
            _frameSim++;
            r = Simulate(v, b * DzStep, _k);
            RangeCache[key] = r;
            return r;
        }

        /// <summary>Lot strzaly: najwiekszy zasieg po katach 4..60 st (co 2 st, potem +-1 st wokol najlepszego), krok 0.01 s (wzor silnika: v -= k v^2 dt).</summary>
        internal static float Simulate(float v, float dz, float k)
        {
            if (!(v > 0f)) return 0f;
            float best = 0f;
            int bestAng = 0;
            for (int ang = 4; ang <= 60; ang += 2)
            {
                float x = Fly(v, ang, dz, k);
                if (x > best) { best = x; bestAng = ang; }
            }
            if (bestAng > 0)
            {
                float lo = Fly(v, bestAng - 1, dz, k), hi = Fly(v, bestAng + 1, dz, k);
                if (lo > best) best = lo;
                if (hi > best) best = hi;
            }
            return best;
        }

        /// <summary>Jeden lot pod katem ang (st): odleglosc, na ktorej strzala schodzi na wysokosc celu; 0 = nie dochodzi.</summary>
        private static float Fly(float v, int ang, float dz, float k)
        {
            const float dt = 0.01f, g = 9.81f;
            double a = ang * Math.PI / 180.0;
            float vx = v * (float)Math.Cos(a), vz = v * (float)Math.Sin(a), x = 0f, z = 1.5f - dz;
            for (int s = 0; s < 3000; s++)
            {
                float sp = (float)Math.Sqrt(vx * vx + vz * vz);
                float f = k * sp;
                vx -= f * vx * dt;
                vz -= (f * vz + g) * dt;
                x += vx * dt;
                z += vz * dt;
                if (z < 0f && vz < 0f) return x;
            }
            return 0f;
        }

        /// <summary>Zasieg bojowy G (m) dla broni i roznicy wysokosci; -1 = bez budzetu klatki (nastepny przeglad).</summary>
        private static float Gate(Wp p, float dz, bool budget)
        {
            float r = MaxRange(p.V, dz, budget);
            if (r < 0f) return -1f;
            return Eff * r;
        }

        // ------------------------------------------------------------ przeglad (ShieldsUp.Tick, glowny watek)

        internal static void Tick(Mission m, float dt)
        {
            if (!Mine(m)) return;
            bool deployed = false;
            try { deployed = m.IsDeploymentFinished; } catch { deployed = false; }
            if (!deployed) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _frameNear = 0;
                _frameSim = 0;
                float now = m.CurrentTime;
                var list = m.Agents;
                int n = list != null ? list.Count : 0;
                if (n > 0)
                {
                    int per = (int)Math.Ceiling(n * Math.Max(dt, 0f) / Every);
                    if (per < MinPer) per = MinPer; else if (per > MaxPer) per = MaxPer;
                    if (per > n) per = n;
                    int start = _rot % n;
                    for (int k = 0; k < per; k++)
                    {
                        var a = list[(start + k) % n];
                        if (a == null || !a.IsHuman) continue;
                        try { Look(m, a, now); }
                        catch (Exception e) { Stumble("FireRange469.Czlowiek", e); St s; if (By.TryGetValue(a, out s)) End(s, now, true); }
                    }
                    _rot = start + per;
                    if (_rot > 1 << 30) _rot = 0;
                }
                if (By.Count > 0)
                {
                    Drop.Clear();
                    foreach (var kv in By) if (!kv.Value.Held) Drop.Add(kv.Key);
                    for (int i = 0; i < Drop.Count; i++) By.Remove(Drop[i]);
                    Drop.Clear();
                }
            }
            catch (Exception e) { Stumble("FireRange469.Przeglad", e); }
            long dtk = Stopwatch.GetTimestamp() - t0;
            _sumTicks += dtk;
            if (dtk > _maxTicks) _maxTicks = dtk;
        }

        private static void Look(Mission m, Agent a, float now)
        {
            St s;
            By.TryGetValue(a, out s);
            bool held = s != null && s.Held;
            if (!a.IsActive()) { if (held) { _relDead++; End(s, now, false); } return; }
            if (a.IsMainAgent || a.IsPlayerControlled || !a.IsAIControlled) { if (held) { _relTaken++; End(s, now, true); } return; }
            bool noAmmo;
            var p = Weapon(a, true, out noAmmo);
            if (p == null)
            {
                if (held) { _relWeapon++; End(s, now, true); }
                else if (noAmmo) _skNoAmmo++;
                return;
            }
            _scanned++;
            if (a.HasMount) { if (held) { _relMount++; End(s, now, true); } else _skMount++; return; }
            if (HorsePursuit.HuntAnyThread(a) || HorsePursuit.RidingAnyThread(a) || Doctrine.H447AnyThread(a)) { if (held) { _relTaken++; End(s, now, true); } else _skHunt++; return; }
            if (HorsePursuit.Banner(a)) { if (held) { _relTaken++; End(s, now, true); } else _skBanner++; return; }
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || a.IsRetreating()) { if (held) { _relPanic++; End(s, now, true); } else _skPanic++; return; }
            var f = a.Formation;
            if (f == null || a.IsDetachedFromFormation) { if (held) { _relForm++; End(s, now, true); } else _skDetached++; return; }
            if (a.IsUsingGameObject) { if (held) { _relForm++; End(s, now, true); } else _skObject++; return; }
            var q = f.QuerySystem;
            if (q != null && q.IsInfantryFormation) { if (held) { _relForm++; End(s, now, true); } else _skInf++; return; }   // prefiks RBM na SetFiringOrder - jak 310
            if (f.FiringOrder.OrderEnum == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire) { if (held) { _relForm++; s.Held = false; Close(s, now); } else _skFormHold++; return; }
            if (!p.Ok) { if (held) { _relWeapon++; End(s, now, true); } else _skUsage++; return; }
            _archers++;
            // odleglosc: cel silnika, bez celu - skraj najblizszego szyku wroga
            float d, dz = 0f;
            bool noTarget = false;
            Agent t = null;
            try { t = a.GetTargetAgent(); } catch { t = null; }
            Agent tm = t != null && t.IsMount ? t.RiderAgent : t;
            if (t != null && t.IsActive() && tm != null && tm.Team != null && a.Team != null && tm.Team.IsEnemyOf(a.Team))
            {
                Vec3 ap = a.Position, tp = t.Position;
                d = ap.AsVec2.Distance(tp.AsVec2);
                dz = tp.z - ap.z;
            }
            else
            {
                noTarget = true;
                d = -1f;
                try
                {
                    var eq = f.CachedClosestEnemyFormation;
                    var ef = eq != null ? eq.Formation : null;
                    if (ef != null && ef.CountOfUnits > 0) d = Math.Max(0f, a.Position.AsVec2.Distance(ef.CachedMedianPosition.AsVec2) - ef.Depth * 0.5f);
                }
                catch { d = -1f; }
                if (d < 0f) { if (held) { _relNoFoe++; End(s, now, true); } return; }
            }
            float g = Gate(p, dz, true);
            if (g < 0f) return;   // bez budzetu symulacji - nastepny przeglad
            if (held)
            {
                if (d <= g) { _relRange++; End(s, now, true); return; }
                if (d < ThreatR + 1f || Near(m, a)) { _relNear++; End(s, now, true); return; }
                if (_key) { a.SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire); _reHold++; }
                return;
            }
            if (d <= g * HoldOver) return;
            if (d < ThreatR + 1f) return;
            if (_frameNear >= NearPer) { _overNear++; return; }
            if (Near(m, a)) return;
            if (!_key) { _wouldHold++; return; }   // NIE: tylko pomiar
            if (s == null) { s = new St { A = a }; By[a] = s; }
            a.SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire);
            s.Held = true;
            s.Since = now;
            _holds++;
            if (noTarget) _heldNoTarget++;
            if (HeldEver.Add(a)) _heldMen++;
        }

        /// <summary>Aktywny, nieuciekajacy wrog w 6 m (jedno zapytanie, budzet klatki).</summary>
        private static bool Near(Mission m, Agent a)
        {
            if (_frameNear >= NearPer) { _overNear++; return false; }
            _frameNear++;
            try
            {
                m.GetNearbyEnemyAgents(a.Position.AsVec2, ThreatR, a.Team, TBuf);
                for (int i = 0; i < TBuf.Count; i++)
                {
                    var x = TBuf[i];
                    if (x == null || !x.IsActive()) continue;
                    var o = x.IsMount ? x.RiderAgent : x;
                    if (o == null || !o.IsHuman || !o.IsActive() || o.IsRunningAway) continue;
                    return true;
                }
            }
            finally { TBuf.Clear(); }
            return false;
        }

        /// <summary>Koniec wstrzymania: rozkaz szyku wraca, chyba ze 310 wciaz trzyma ogien tego czlowieka (apply = false: padl - bez wywolan silnika).</summary>
        private static void End(St s, float now, bool apply)
        {
            if (s == null || !s.Held) return;
            s.Held = false;
            Close(s, now);
            var a = s.A;
            if (!apply || a == null || !a.IsActive() || CleanShot.Held(a)) return;
            try
            {
                var f = a.Formation;
                a.SetFiringOrder(f != null ? f.FiringOrder.OrderEnum : FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill);
            }
            catch (Exception e) { Stumble("FireRange469.Ogien", e); }
        }

        private static void Close(St s, float now)
        {
            float dur = now - s.Since;
            if (dur < 0f) dur = 0f;
            _holdSecs += dur; _holdN++;
            if (dur > _holdMax) _holdMax = dur;
        }

        /// <summary>BattleSense.OnAgentRemoved (glowny watek): wpis bez wywolan silnika.</summary>
        internal static void OnRemoved(Mission m, Agent affected)
        {
            if (!Mine(m) || affected == null || By.Count == 0) return;
            try
            {
                St s;
                if (By.TryGetValue(affected, out s)) { if (s.Held) { _relDead++; End(s, m.CurrentTime, false); } By.Remove(affected); }
            }
            catch (Exception e) { Stumble("FireRange469.OnRemoved", e); }
        }

        // ------------------------------------------------------------ strzal (kolejka 140, glowny watek) - tylko pomiar

        internal static void OnShot(Mission m, Agent s, bool thrown)
        {
            if (!Mine(m) || s == null || thrown) return;
            long t0 = Stopwatch.GetTimestamp();
            try { Shot(m, s); }
            catch (Exception e) { Stumble("FireRange469.Strzal", e); }
            _sumTicks += Stopwatch.GetTimestamp() - t0;
        }

        private static void Shot(Mission m, Agent s)
        {
            if (!s.IsActive() || !s.IsHuman || s.Team == null || !s.IsAIControlled || s.IsMainAgent || s.IsPlayerControlled || s.HasMount) return;
            bool noAmmo;
            Wp p;
            // strzal juz poszedl - amunicja mogla sie skonczyc; dane broni bez warunku zapasu
            p = Weapon(s, false, out noAmmo);
            if (p == null || !p.Ok) return;
            Agent t = null;
            try { t = s.GetTargetAgent(); } catch { t = null; }
            Agent tm = t != null && t.IsMount ? t.RiderAgent : t;
            if (t == null || !t.IsActive() || tm == null || tm.Team == null || !tm.Team.IsEnemyOf(s.Team)) { _shotsNoTarget++; return; }
            Vec3 ap = s.Position, tp = t.Position;
            float d = ap.AsVec2.Distance(tp.AsVec2);
            float g = Gate(p, tp.z - ap.z, true);   // -1 = bez budzetu symulacji w tej klatce - strzal liczony bez G
            float nat = -1f;
            try { nat = s.GetMissileRange(); } catch { nat = -1f; }
            _shots++;
            ShotBy[d < 50f ? 0 : d < 100f ? 1 : d < 150f ? 2 : d < 200f ? 3 : 4]++;
            if (g >= 0f && d > g * HoldOver) _shotsOver++;
            if (Holds(s)) _shotsHeld++;
            p.Shots++;
            if (g >= 0f) { p.GSum += g; p.GN++; }
            if (nat > 0f) { p.NatSum += nat; p.NatN++; }
            int side = s.Team.Side == BattleSideEnum.Attacker ? 1 : 0;
            float now = m.CurrentTime;
            if (FvT0[side] < 0f) FvT0[side] = now;
            if (now - FvT0[side] <= FirstVolley)
            {
                FvN[side]++; FvD[side] += d;
                if (g >= 0f) { FvG[side] += g; FvGN[side]++; }
                if (nat > 0f) { FvNat[side] += nat; FvNatN[side]++; }
            }
        }

        /// <summary>BattleSense.Clean (po CleanShot.End): wstrzymania zdjete, linia raz na bitwe.</summary>
        internal static void End(Mission m)
        {
            var w = _m;
            if (w == null) return;
            if (m != null && !ReferenceEquals(w.Target, m)) return;
            bool live = _live;
            float now = 0f;
            try { if (m != null) now = m.CurrentTime; } catch { }
            try
            {
                foreach (var kv in By) if (kv.Value.Held) { _relEnd++; End(kv.Value, now, true); }
            }
            catch (Exception e) { Stumble("FireRange469.End", e); }
            _live = false;
            if (live && !_written)
            {
                _written = true;
                try { Write(); } catch (Exception e) { Stumble("FireRange469.Write", e); }
            }
            Clear();
            _m = null;
        }

        private static string F0(float v) { return v.ToString("0", Inv); }

        private static double Ms(long t) { return t * 1000.0 / Stopwatch.Frequency; }

        private static string Fv(int i)
        {
            if (FvN[i] == 0) return "brak";
            return FvN[i] + " strzal, sr. " + F0(FvD[i] / FvN[i]) + " m przy G " + (FvGN[i] > 0 ? F0(FvG[i] / FvGN[i]) : "-") + " m, natywny zasieg "
                + (FvNatN[i] > 0 ? F0(FvNat[i] / FvNatN[i]) : "-") + " m";
        }

        private static void Write()
        {
            var sb = new StringBuilder(1600);
            sb.Append("Ogien w zasiegu (469): misja ").Append(_kind).Append(", klucz ").Append(_key ? "TAK" : "NIE (tylko pomiar, bez wstrzyman)")
              .Append(_rbm ? "" : ", brak RBM (predkosc gry, bez naciagu)")
              .Append(" (G = ").Append(Eff.ToString("0.00", Inv)).Append(" x zasieg maks. z oporem k ").Append(_k.ToString("0.0000", Inv))
              .Append("; wstrzymanie powyzej ").Append(HoldOver.ToString("0.0", Inv)).Append(" G, ogien od G)")
              .Append(" | strzaly AI z luku / kuszy pieszo: ").Append(_shots).Append(" (<50/50-99/100-149/150-199/200+ m ")
              .Append(ShotBy[0]).Append('/').Append(ShotBy[1]).Append('/').Append(ShotBy[2]).Append('/').Append(ShotBy[3]).Append('/').Append(ShotBy[4])
              .Append("), poza ").Append(HoldOver.ToString("0.0", Inv)).Append(" G ").Append(_shotsOver).Append(", mimo wstrzymania ").Append(_shotsHeld)
              .Append(", bez celu ").Append(_shotsNoTarget)
              .Append(" | pierwsza salwa (").Append(F0(FirstVolley)).Append(" s): obrona ").Append(Fv(0)).Append("; atak ").Append(Fv(1))
              .Append(" | wstrzyman ").Append(_holds).Append(" (ludzi ").Append(_heldMen).Append(", bez celu - szyk wroga dalej ").Append(_heldNoTarget)
              .Append("), na sucho przegladow z wstrzymaniem ").Append(_wouldHold).Append(", ponowien rozkazu ").Append(_reHold)
              .Append(", czas sr. ").Append(_holdN > 0 ? (_holdSecs / _holdN).ToString("0.0", Inv) : "-").Append(" s, najdluzszy ").Append(_holdMax.ToString("0.0", Inv)).Append(" s")
              .Append(" | zwolnienia: w zasiegu ").Append(_relRange).Append(", wrog w ").Append(F0(ThreatR)).Append(" m ").Append(_relNear)
              .Append(", bron / kolczan ").Append(_relWeapon).Append(", panika ").Append(_relPanic).Append(", przejete / gracz ").Append(_relTaken)
              .Append(", kon ").Append(_relMount).Append(", szyk (piechota / wstrzymanie szyku / poza szykiem) ").Append(_relForm)
              .Append(", bez wroga ").Append(_relNoFoe).Append(", padl ").Append(_relDead).Append(", koniec misji ").Append(_relEnd)
              .Append(" | przegladow strzelcow ").Append(_scanned).Append(", w regule ").Append(_archers)
              .Append(" | pominiete: konni ").Append(_skMount).Append(", szyk piechoty ").Append(_skInf).Append(", szyk wstrzymany ").Append(_skFormHold)
              .Append(", poza szykiem ").Append(_skDetached).Append(", machina ").Append(_skObject).Append(", nieznany uzytek ").Append(_skUsage)
              .Append(", bez amunicji ").Append(_skNoAmmo).Append(", 204/283/148/447 ").Append(_skHunt).Append(", chorazy ").Append(_skBanner)
              .Append(", panika / odwrot ").Append(_skPanic).Append(", ponad budzet (zapytania ").Append(_overNear).Append(", symulacje ").Append(_overSim).Append(')')
              .Append(" | bronie (naciag lb / v m/s / G sr. / natywny sr. / strzal):");
            WeapSeen.Sort((x, y) => y.Shots.CompareTo(x.Shots));
            int shown = 0;
            for (int i = 0; i < WeapSeen.Count && shown < WeapMax; i++)
            {
                var p = WeapSeen[i];
                if (p.Shots <= 0) continue;
                shown++;
                sb.Append(' ').Append(p.Name).Append(' ').Append(p.Lb).Append('/').Append(p.V).Append('/').Append(p.GN > 0 ? F0(p.GSum / p.GN) : "-").Append('/')
                  .Append(p.NatN > 0 ? F0(p.NatSum / p.NatN) : "-").Append('/').Append(p.Shots).Append(';');
            }
            if (shown == 0) sb.Append(" brak");
            sb.Append(" | koszt ").Append(Ms(_sumTicks).ToString("0.0", Inv)).Append(" ms razem, najwiecej ").Append(Ms(_maxTicks).ToString("0.000", Inv))
              .Append(" ms na klatke | potkniecia ").Append(_stum).Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ start sesji: tabela zasiegow (tylko log) i naciag z danych broni

        private static readonly string[] Typical =
        {
            "hunting_bow", "steppe_bow", "composite_bow", "nordic_shortbow", "steppe_war_bow", "noble_bow", "highland_ranger_bow", "lowland_longbow",
            "woodland_longbow", "noble_long_bow", "weirwood_bow", "ravens_teeth_longbow", "crossbow_h", "crossbow_a", "crossbow_c", "crossbow_f"
        };

        /// <summary>OnAfterSessionLaunched (po RotBows420.ApplyLore): naciag wszystkich lukow i kusz zapamietany, linia "Zasieg lukow (469)".</summary>
        internal static void StartTable()
        {
            try
            {
                LbAtStart.Clear();
                bool rbm = RangedRank.RbmLoaded;
                float k = Friction();
                int bows = 0, xbows = 0;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || (it.ItemType != ItemObject.ItemTypeEnum.Bow && it.ItemType != ItemObject.ItemTypeEnum.Crossbow)) continue;
                    var w = it.PrimaryWeapon;
                    if (w == null) continue;
                    LbAtStart[it] = w.MissileSpeed;
                    if (it.ItemType == ItemObject.ItemTypeEnum.Bow) bows++; else xbows++;
                }
                var sb = new StringBuilder(1200);
                sb.Append("Zasieg lukow (469): start sesji | ").Append(rbm ? "RBM - predkosc z naciagu (wzor RBM)" : "brak RBM - predkosc gry")
                  .Append(", opor k ").Append(k.ToString("0.0000", Inv)).Append(", strzala ").Append(ArrowW.ToString("0.000", Inv)).Append(" kg, belt ")
                  .Append(BoltW.ToString("0.000", Inv)).Append(" kg, G = ").Append(Eff.ToString("0.00", Inv)).Append(" x zasieg maks. | lukow ").Append(bows)
                  .Append(", kusz ").Append(xbows).Append(" | bron: uzytek, naciag lb, v m/s, zasieg maks. m, G m:");
                int shown = 0;
                for (int i = 0; i < Typical.Length; i++)
                {
                    ItemObject it = null;
                    try { it = MBObjectManager.Instance.GetObject<ItemObject>(Typical[i]); } catch { it = null; }
                    if (it == null || it.PrimaryWeapon == null) continue;
                    var w = it.PrimaryWeapon;
                    string u = w.ItemUsage ?? "";
                    int lb = rbm ? w.MissileSpeed : 0;
                    int v = rbm ? RbmSpeed(it.ItemType == ItemObject.ItemTypeEnum.Crossbow ? BoltW : ArrowW, u, lb) : w.MissileSpeed;
                    float r = Simulate(v, 0f, k);
                    sb.Append(' ').Append(it.StringId).Append(' ').Append(u).Append(' ').Append(lb).Append(' ').Append(v).Append(' ').Append(F0(r))
                      .Append(' ').Append(F0(Eff * r)).Append(';');
                    shown++;
                }
                if (shown == 0) sb.Append(" brak typowych broni w danych");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("FireRange469.StartTable", e); }
        }
    }
}
