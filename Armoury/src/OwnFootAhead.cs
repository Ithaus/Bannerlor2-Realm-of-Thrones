using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 305c (31.10, Jeff: "sprawdz bitwe ... jak mozesz podkrec AI i trudnosc zachowania AI"; zasada 25.10 "AI walczy o zycie" - zywy jezdziec nie tratuje
    /// swoich). Dowod: Willow Wood 30.10 linia "FF (pole)": szarza w swoich 2869 trafien (AI 2866), udzial FF 27.9% trafien ludzi, 281 swoich powalonych;
    /// konnica AI przejezdza przez wlasna piechote (157: przejazd RBM). Gra i RBM nie omijaja swoich, bo w grze FF jest wylaczony (FriendlyFire.cs otwiera
    /// bramke szarzy).
    ///
    /// (a) Pomiar (zawsze, gdy zachowanie misji jest - klucz albo FriendlyFireEnabled): kazda szarza konia AI w swojego czlowieka (FriendlyFire.DamagePost,
    ///     galaz szarzy AI - watek glowny, MBCallback) wedlug stanu szyku jezdzca (RBMBehaviorCavalryCharge._chargeState: Charging 1 / ChargingPast 2 /
    ///     Reforming 3 - RBMAI, ten sam odczyt co ShieldsUp.Behave 157; BehaviorCharge gry albo rozkaz Charge; BehaviorProtectFlank; inne) i klasy ofiary
    ///     (pieszy wrecz / pieszy strzelec / konny), takze czy jezdziec byl w tej chwili zwolniony przez (b).
    /// (b) Klucz CavalrySlowsForOwnFoot (tylko bitwa polowa: IsFieldBattle, nie kryjowka, nie morze): co 0.2 s glowny watek bierze jezdzcow AI w ruchu
    ///     (kon szybciej niz 1.5 m/s; nie gracz, nie uciekajacy, nie smok ROT - MarchToContact.IsDragon) i jednym zapytaniem gry GetNearbyAgents patrzy w
    ///     korytarz przed lbem konia: dlugosc 3 m + 0.5 s jazdy (najwyzej 8 m - z galopu kon nie stanie w 3 m), szerokosc 1.1 m w kazda strone. Swoj pieszy
    ///     czlowiek w korytarzu blizej niz pierwszy wrog w korytarzu (wrog blizej = szarza na wroga, bez zmian) - jezdziec zwolniony: znacznik w tablicy
    ///     wedlug Agent.Index (wzor HorsePursuit._riding - odczyt bez blokady z watkow roboczych), od razu SetMaximumSpeedLimit(Step) jezdzcowi i koniowi,
    ///     a latka MarchToContact.PostSpeed (AdjustSpeedLimit po RBM) trzyma ten sam limit przy kazdym przeliczeniu gry - jedna regula z capem marszu 128 /
    ///     odpoczynku 136: limit = najmniejszy z nich (Step ponizej klusa 128 i odpoczynku 136). Zwolnieni sprawdzani co takt (zwolnienie zdjete zaraz po
    ///     oczyszczeniu drogi - gra / RBM wpisze swoj limit przy nastepnym AdjustSpeedLimit), reszta po kolei z limitem zapytan na takt.
    /// Nic co klatke na agenta poza taktem 0.2 s, bez wizerunkow, bez Settings.Current w latce. Wyjatek - licznik potkniec, bez globalnego wylacznika.
    /// Linia "Szarza przez swoich (305c)" raz na bitwe (OnEndMission / OnRemoveBehavior).
    /// </summary>
    internal static class OwnFootAhead
    {
        internal const float Step = 2.0f;               // m/s - step konia (lekki step; klus podejscia 128 to ok. 0.45 x pelna, odpoczynek 136 step / lekki klus)
        internal const float TickEvery = 0.2f, MinSpeed = 1.5f, HeadOff = 1.3f, LookMin = 3f, LookMax = 8f, LookSec = 0.5f, Side = 1.1f;
        private const int QueriesPerTick = 160;
        // 305f (przeglad scalenia): zwolnieni tez z budzetem - najwyzej polowa zapytan taktu (osobny wskaznik obrotu, niesprawdzonym tylko odnowiony limit),
        // i twardy sufit liczby zwolnionych naraz (ponad niego jezdziec nie jest zwalniany - licznik w linii)
        private const int HeldQueriesPerTick = QueriesPerTick / 2, MaxSlowed = 200;
        // recenzja 305c: zwolniony (do stepa 2 m/s) puszczany dopiero ponizej 0.5 m/s - inaczej step pod 1.5 m/s w tlumie dawal petle zwolnij-pusc;
        // kierunek korytarza zwolnionego wolnego konia z kierunku ruchu agenta (GetMovementDirection), nie ze skaczacego wektora predkosci
        private const float HeldMinSpeed = 0.5f;
        private const int MaxIndex = 65536;

        // ---- znaczniki dla latki (watki robocze); pisze tylko glowny watek
        private static Agent[] _slow;
        private static volatile bool _on;
        private static int _postHits;    // Interlocked

        // ---- stan misji (glowny watek)
        private static bool _key, _field;
        private static string _kind = "-";
        private static float _acc;
        private static int _rot, _heldRot;
        private static readonly List<Agent> Riders = new List<Agent>();
        private static readonly List<Agent> Slowed = new List<Agent>();
        private static readonly HashSet<Agent> SlowSet = new HashSet<Agent>();
        private static readonly MBList<Agent> Buf = new MBList<Agent>();
        private static readonly HashSet<int> SeenRiders = new HashSet<int>(), SlowRiders = new HashSet<int>();
        private static readonly Stopwatch Sw = new Stopwatch();

        // ---- liczniki misji
        private static int _ticks, _queries, _budgetHit, _episodes, _withFoeFurther, _maxAtOnce, _stum, _heldDeferred, _capHit;
        private static float _slowSec, _msSum, _msMax;
        private static string _stumWhere;
        // (a) szarze AI w swoich: stan jezdzca x klasa ofiary, w czasie zwolnienia
        private const int SRbmCharge = 0, SRbmPast = 1, SRbmReform = 2, SRbmOther = 3, SGameCharge = 4, SFlank = 5, SOther = 6, SKinds = 7;
        private static readonly string[] SName = { "RBM szarzuje", "RBM przejazd", "RBM przegrupowanie", "RBM inny stan", "szarza gry", "oslona skrzydla", "inne (marsz, stoi)" };
        private const int VFoot = 0, VFootRanged = 1, VMounted = 2, VKinds = 3;
        private static readonly int[,] Ff = new int[SKinds, VKinds];
        private static int _ffAll, _ffWhileSlow;
        private static Type _tRbm;
        private static FieldInfo _fRbm;

        // ------------------------------------------------------------ dla latki MarchToContact.PostSpeed (watki robocze)

        /// <summary>Jezdziec zwolniony przed swoim pieszym (bez blokady).</summary>
        internal static bool SlowAnyThread(Agent a)
        {
            if (!_on || a == null) return false;
            var k = Volatile.Read(ref _slow);
            if (k == null) return false;
            int i = a.Index;
            return i >= 0 && i < k.Length && ReferenceEquals(k[i], a);
        }

        internal static void NotePost() { Interlocked.Increment(ref _postHits); }

        private static void Mark(Agent a, bool on)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= MaxIndex) return;
            var k = _slow;
            if (k == null || i >= k.Length)
            {
                if (!on) return;
                int n = k == null ? 1024 : k.Length;
                while (n <= i) n *= 2;
                var nk = new Agent[n];
                if (k != null) Array.Copy(k, nk, k.Length);
                nk[i] = a;
                Volatile.Write(ref _slow, nk);
                return;
            }
            if (on) k[i] = a;
            else if (ReferenceEquals(k[i], a)) k[i] = null;
        }

        // ------------------------------------------------------------ (a) pomiar z FriendlyFire.DamagePost (galaz szarzy AI)

        /// <summary>Szarza konia AI w swojego czlowieka (r - jezdziec, v - ofiara). Watek glowny (MBCallback); bez alokacji poza pierwszym odczytem pola RBM.</summary>
        internal static void NoteChargeFf(Agent r, Agent v)
        {
            try
            {
                if (r == null || v == null || !v.IsHuman) return;
                int s = StateOf(r.Formation);
                int k = v.MountAgent != null ? VMounted : (Ranged(v) ? VFootRanged : VFoot);
                Ff[s, k]++;
                _ffAll++;
                if (SlowSet.Contains(r)) _ffWhileSlow++;
            }
            catch (Exception e) { Stumble("OwnFootAhead.NoteChargeFf", e); }
        }

        private static bool Ranged(Agent v)
        {
            var f = v.Formation;
            var q = f != null ? f.QuerySystem : null;
            if (q != null && q.IsRangedFormation) return true;
            return v.IsRangedCached;
        }

        private static int StateOf(Formation f)
        {
            if (f == null) return SOther;
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            var ai = f.AI;
            var b = ai != null ? ai.ActiveBehavior : null;
            if (b != null)
            {
                var t = b.GetType();
                if (t.Name == "RBMBehaviorCavalryCharge")
                {
                    if (!ReferenceEquals(t, _tRbm))
                    {
                        _tRbm = t;
                        try { _fRbm = AccessTools.Field(t, "_chargeState"); } catch { _fRbm = null; }
                    }
                    int st = -1;
                    if (_fRbm != null) { try { var o = _fRbm.GetValue(b); st = o != null ? Convert.ToInt32(o) : -1; } catch { st = -1; } }
                    return st == 1 ? SRbmCharge : st == 2 ? SRbmPast : st == 3 ? SRbmReform : SRbmOther;
                }
                if (b is BehaviorCharge) return SGameCharge;
                if (b is BehaviorProtectFlank) return SFlank;
            }
            if (oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget) return SGameCharge;
            return SOther;
        }

        // ------------------------------------------------------------ misja

        private static void Begin(Mission m)
        {
            Clear();
            var c = Settings.Current;
            _kind = MissionTeams.Kind(m);
            bool field = false;
            try { field = m.IsFieldBattle && !m.IsNavalBattle && !HideoutAlarm.IsHideout(m); } catch { field = false; }
            _field = field;
            _key = c != null && c.CavalrySlowsForOwnFoot && field;
            _on = _key;
        }

        private static void Clear()
        {
            _on = false;
            Volatile.Write(ref _slow, null);
            Interlocked.Exchange(ref _postHits, 0);
            Riders.Clear(); Slowed.Clear(); SlowSet.Clear(); Buf.Clear(); SeenRiders.Clear(); SlowRiders.Clear();
            _acc = 0f; _rot = 0; _heldRot = 0;
            _ticks = _queries = _budgetHit = _episodes = _withFoeFurther = _maxAtOnce = _stum = _heldDeferred = _capHit = 0;
            _slowSec = _msSum = _msMax = 0f;
            _stumWhere = null;
            Array.Clear(Ff, 0, Ff.Length);
            _ffAll = _ffWhileSlow = 0;
        }

        private static void Tick(Mission m, float dt)
        {
            if (!_key) return;
            _acc += dt;
            if (_acc < TickEvery) return;
            float step = _acc;
            _acc = 0f;
            if (m.MissionEnded) { ReleaseAll(); return; }
            Sw.Restart();
            _ticks++;
            _slowSec += Slowed.Count * step;
            int budget = QueriesPerTick;
            // 1) zwolnieni - co takt (zdjecie zaraz po oczyszczeniu drogi); 305f: zapytanie korytarza najwyzej HeldQueriesPerTick zwolnionym na takt, po kolei
            // od wskaznika _heldRot (kazdy sprawdzony najdalej co ceil(n / 80) taktow), reszta - tylko Eligible (bez zapytania) i odnowiony limit
            int heldN = Slowed.Count, heldQ = Math.Min(heldN, HeldQueriesPerTick);
            if (_heldRot >= heldN) _heldRot = 0;
            int qFrom = _heldRot, qTo = _heldRot + heldQ;   // zakres indeksow (z zawinieciem) sprawdzanych w tym takcie - wedlug stanu listy na poczatku taktu
            for (int i = heldN - 1; i >= 0; i--)
            {
                var a = Slowed[i];
                bool ask = (i >= qFrom && i < qTo) || (qTo > heldN && i < qTo - heldN);
                bool keep = false;
                try
                {
                    keep = Eligible(a);
                    if (keep && ask) { keep = Blocked(m, a, true); budget--; }
                    else if (keep) _heldDeferred++;
                }
                catch (Exception e) { Stumble("OwnFootAhead.Zwolniony", e); keep = false; }
                if (!keep) Release(i);
                else Apply(a);   // limit odnowiony co takt (bez latki AdjustSpeedLimit gra wpisalaby swoj po ok. 0.5 s)
            }
            _heldRot = heldN > 0 ? qTo % heldN : 0;   // nastepny takt od kolejnych (po zwolnieniach z listy wskaznik moze przeskoczyc kilku - dojda w nastepnym obrocie)
            // 2) reszta jezdzcow po kolei (lista odswiezana na poczatku obrotu)
            if (_rot == 0 || _rot >= Riders.Count) { Collect(m); _rot = 0; }
            while (_rot < Riders.Count)
            {
                if (budget <= 0) { _budgetHit++; break; }
                var a = Riders[_rot++];
                if (SlowSet.Contains(a)) continue;
                try
                {
                    if (!Eligible(a)) continue;
                    budget--;
                    if (Blocked(m, a, false)) Slow(a);
                }
                catch (Exception e) { Stumble("OwnFootAhead.Jezdziec", e); }
            }
            if (Slowed.Count > _maxAtOnce) _maxAtOnce = Slowed.Count;
            Sw.Stop();
            float ms = (float)Sw.Elapsed.TotalMilliseconds;
            _msSum += ms;
            if (ms > _msMax) _msMax = ms;
        }

        private static void Collect(Mission m)
        {
            Riders.Clear();
            var ags = m.Agents;
            int n = ags != null ? ags.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var a = ags[i];
                if (a == null || !a.IsHuman || a.MountAgent == null || !a.IsActive()) continue;
                Riders.Add(a);
            }
        }

        /// <summary>Jezdziec AI w ruchu: nie gracz, nie uciekajacy, nie smok, wazna druzyna strony bitwy, kon szybciej niz MinSpeed.</summary>
        private static bool Eligible(Agent a)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || a.IsMainAgent || !a.IsAIControlled || a.IsPlayerControlled) return false;
            var mt = a.MountAgent;
            if (mt == null || !mt.IsActive() || MarchToContact.IsDragon(mt)) return false;
            if (a.IsRunningAway || SurvivalMode.InPanic(a)) return false;
            var t = a.Team;
            if (!MissionTeams.Valid(t) || (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender)) return false;
            SeenRiders.Add(a.Index);
            return true;
        }

        /// <summary>Swoj pieszy w korytarzu przed lbem konia blizej niz pierwszy wrog w korytarzu (jedno zapytanie gry).</summary>
        private static bool Blocked(Mission m, Agent a, bool held)
        {
            var mt = a.MountAgent;
            Vec2 vel = mt.GetRealGlobalVelocity().AsVec2;
            float sp = vel.Length;
            if (!(sp > (held ? HeldMinSpeed : MinSpeed))) return false;
            Vec2 fwd = vel * (1f / sp);
            if (held && sp < MinSpeed)
            {
                Vec2 md = mt.GetMovementDirection();
                float ml = md.Length;
                if (ml > 0.01f) fwd = md * (1f / ml);
            }
            float look = LookMin + LookSec * sp;
            if (look > LookMax) look = LookMax;
            Vec2 head = mt.Position.AsVec2 + fwd * HeadOff;
            Vec2 c = head + fwd * (0.5f * look);
            _queries++;
            m.GetNearbyAgents(c, 0.5f * look + Side, Buf);
            float own = float.MaxValue, foe = float.MaxValue;
            var t = a.Team;
            for (int i = 0; i < Buf.Count; i++)
            {
                var x = Buf[i];
                if (x == null || ReferenceEquals(x, a) || !x.IsHuman || !x.IsActive()) continue;
                var xt = x.Team;
                if (xt == null) continue;
                Vec2 d = x.Position.AsVec2 - head;
                float along = d.x * fwd.x + d.y * fwd.y;
                if (along < -0.3f || along > look) continue;
                float lat = Math.Abs(d.x * fwd.y - d.y * fwd.x);
                if (lat > Side) continue;
                if (xt.IsEnemyOf(t)) { if (along < foe) foe = along; }
                else if (xt.Side == t.Side && x.MountAgent == null) { if (along < own) own = along; }
            }
            Buf.Clear();
            if (own == float.MaxValue) return false;
            if (foe < own) return false;
            if (foe < float.MaxValue) _withFoeFurther++;
            return true;
        }

        private static void Slow(Agent a)
        {
            if (Slowed.Count >= MaxSlowed) { _capHit++; return; }   // 305f: twardy sufit zwolnionych naraz
            if (!SlowSet.Add(a)) return;
            Slowed.Add(a);
            Mark(a, true);
            _episodes++;
            SlowRiders.Add(a.Index);
            Apply(a);
        }

        private static void Apply(Agent a)
        {
            try
            {
                a.SetMaximumSpeedLimit(Step, false);
                var mt = a.MountAgent;
                if (mt != null) mt.SetMaximumSpeedLimit(Step, false);
            }
            catch (Exception e) { Stumble("OwnFootAhead.Limit", e); }
        }

        private static void Release(int i)
        {
            var a = Slowed[i];
            Slowed.RemoveAt(i);
            SlowSet.Remove(a);
            Mark(a, false);
        }

        private static void ReleaseAll()
        {
            for (int i = Slowed.Count - 1; i >= 0; i--) Release(i);
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            if (_stumWhere == null) _stumWhere = where + ": " + (e != null ? e.GetType().Name + " " + e.Message : "?");
        }

        private static void Write()
        {
            if (!_field) return;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(900);
            sb.Append("Szarza przez swoich (305c): misja ").Append(_kind).Append(", klucz Cavalry Slows For Own Foot ").Append(_key ? "TAK" : "NIE (tylko pomiar)")
              .Append(" (korytarz ").Append(LookMin.ToString("0", inv)).Append(" m + ").Append(LookSec.ToString("0.0", inv)).Append(" s jazdy, najwyzej ")
              .Append(LookMax.ToString("0", inv)).Append(" m, szer. 2 x ").Append(Side.ToString("0.0", inv)).Append(" m; step ").Append(Step.ToString("0.0", inv))
              .Append(" m/s; latka AdjustSpeedLimit ").Append(MarchToContact.SpeedPatched ? "TAK" : "NIE").Append(')');
            if (_key)
            {
                sb.Append(" | taktow ").Append(_ticks).Append(" (sr. ").Append(_ticks > 0 ? (_msSum / _ticks).ToString("0.000", inv) : "-").Append(" ms, najwyzej ")
                  .Append(_msMax.ToString("0.000", inv)).Append(" ms), jezdzcow sprawdzonych ").Append(SeenRiders.Count).Append(", zapytan ").Append(_queries)
                  .Append(" (limit taktu ").Append(_budgetHit).Append(", 305f: zwolnionych bez zapytania w takcie ").Append(_heldDeferred).Append(" (najwyzej ").Append(HeldQueriesPerTick)
                  .Append(" zapytan zwolnionym na takt), sufit ").Append(MaxSlowed).Append(" zwolnionych naraz - odmow ").Append(_capHit).Append(')')
                  .Append(" | zwolnieni przed swoim pieszym: razy ").Append(_episodes).Append(" (jezdzcow ").Append(SlowRiders.Count).Append("), jezdzco-sekund ")
                  .Append(_slowSec.ToString("0", inv)).Append(", ocen z wrogiem dalej w drodze ").Append(_withFoeFurther).Append(", naraz najwyzej ").Append(_maxAtOnce)
                  .Append(", limit w latce ").Append(Volatile.Read(ref _postHits));
            }
            sb.Append(" | szarze AI w swoich (FF) ").Append(_ffAll).Append(" (w czasie zwolnienia ").Append(_ffWhileSlow).Append("): ");
            for (int s = 0; s < SKinds; s++)
            {
                if (s > 0) sb.Append(", ");
                sb.Append(SName[s]).Append(' ').Append(Ff[s, VFoot] + Ff[s, VFootRanged] + Ff[s, VMounted]);
            }
            int foot = 0, rng = 0, mtd = 0;
            for (int s = 0; s < SKinds; s++) { foot += Ff[s, VFoot]; rng += Ff[s, VFootRanged]; mtd += Ff[s, VMounted]; }
            sb.Append(" | ofiary: piesi wrecz ").Append(foot).Append(", piesi strzelcy ").Append(rng).Append(", konni ").Append(mtd)
              .Append(" | potkniecia ").Append(_stum);
            if (_stum > 0 && _stumWhere != null) sb.Append(" (pierwsze: ").Append(_stumWhere).Append(')');
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Zachowanie misji (SubModuleMain.OnMissionBehaviorInitialize: kampania i klucz CavalrySlowsForOwnFoot albo FriendlyFireEnabled).</summary>
        internal sealed class Logic : MissionLogic
        {
            private bool _done;

            public override void AfterStart()
            {
                base.AfterStart();
                try { Begin(Mission); }
                catch (Exception e) { _key = false; _on = false; Stumble("OwnFootAhead.Begin", e); }
            }

            // 309: koszt klatki (FrameCost309) - pomiar calego ticku, zachowanie bez zmian
            public override void OnMissionTick(float dt)
            {
                long t309 = FrameCost309.T();
                try { Tick309(dt); }
                finally { FrameCost309.Add(FrameCost309.MOwnFoot, t309); }
            }

            private void Tick309(float dt)
            {
                base.OnMissionTick(dt);
                try { Tick(Mission, dt); }
                catch (Exception e) { Stumble("OwnFootAhead.Tick", e); }
            }

            protected override void OnEndMission()
            {
                Finish();
                base.OnEndMission();
            }

            public override void OnRemoveBehavior()
            {
                Finish();
                base.OnRemoveBehavior();
            }

            private void Finish()
            {
                if (_done) return;
                _done = true;
                try { ReleaseAll(); } catch { }
                try { Write(); } catch (Exception e) { Log.Error("OwnFootAhead.Write", e); }
                try { Clear(); } catch { }
                _field = false;
                _key = false;
            }
        }
    }
}
