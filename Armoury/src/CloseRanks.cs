using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 146 (25.10, fala "AI walczy o zycie", krok 8 = projekt 135b "szyk pod ostrzalem" z design-weccrxf3h sekcja 5 i karta syntezy "138 Szyk pod
    /// ostrzalem w natarciu"). Jeff 13:30: "Jak widzi ze kolega padl od strzaly a ma tarcze to zalozy tarcze i sie za nia schowa i idzie na przeciwnika".
    /// Przyczyna (RBMAI Utilities.cs:200-217): szyk piechoty AI idacy naprzod pod RBM ma szyk wedlug broni, nie wedlug ostrzalu - ShieldWall tylko przy
    /// ponad 70% ludzi z DUZA tarcza (WeaponClass 29) w rece i najwyzej 1 sztuce innej broni, Loose przy ponad 50% broni uderzeniowej, inaczej gesta Line;
    /// RBM zastepuje BehaviorAdvance.TickOccasionally (OverrideBehaviorAdvance:353-418), wiec reguly gry "ShieldWall pod ostrzalem" (BehaviorAdvance:135-166)
    /// nie ma. Szyk idzie gesta linia pod strzalami.
    ///
    /// Regula (tylko szyk AI piechoty, tylko bitwa polowa, tryb ROZSADEK - spanikowani nie sa liczeni):
    ///  - szyk pod ostrzalem = sygnal S4 ze 140 (ShieldsUp.FormFire: co najmniej ShieldsUpFormationShots pociskow na szyk w 5 s, trzymanie
    ///    ShieldsUpHoldSeconds po ostatnim) - jedna definicja ostrzalu szyku dla tarczy czlowieka i szyku;
    ///  - przed zwarciem (pieszy wrog dalej niz Doctrine.S1ContactGap 25 m - ta sama miara co S1/S2; wyjscie ze zwarcia dopiero powyzej 40 m):
    ///    co najmniej 60% ludzi (bez paniki) trzyma tarcze w rece -> ShieldWall (nasz ShieldWall zostaje do 50% - histereza); mniej -> Loose,
    ///    ale nie przy szyku konnym wroga w 90 m (ShieldsUp.CavNear, K11 - luzny szyk pod szarza to rzez; wtedy decyduje RBM); gdy tarcze ma w ekwipunku
    ///    co najmniej 60%, a w rece mniej, czekamy do 4 s od poczatku ostrzalu na przelozenie ze 140 (bez zmiany szyku);
    ///  - po zwarciu, bez ostrzalu, szyk umarlych (Inni), szyk pod rozkazami gracza - decyduje RBM (wynik RBM zostaje);
    ///  - zmiana szyku (nasza i oddanie RBM) najwyzej raz na 10 s od ostatniej zmiany szyku formacji - OnApply wola UpdateAgentProperties i
    ///    EnforceShieldUsage kazdemu czlowiekowi i przebudowuje linie (ArrangementOrder:126-150, Formation.SetArrangementOrder:722-749, Z8);
    ///  - RBM wola decyzje tylko przy wrogu w 150 m i szyku od 30 ludzi (BehaviorAdvance) albo w szarzy (BehaviorCharge:300); gdy przez 3 s nie wola,
    ///    a nasz szyk juz nie jest potrzebny - przywracamy szyk sprzed naszej zmiany sami (glowny watek).
    /// Wpiecie: PREFIKS (nie postfiks) + postfiks na RBMAI.Utilities.DecideArrangementOrderForFormation(Formation) - metoda jest void i SAMA ustawia
    /// szyk; postfiks po RBM dawalby dwie zmiany szyku na decyzje (RBM -> nasz), czyli dwa OnApply na kazdego czlowieka co 0.5 s. Prefiks pomija cialo RBM
    /// tylko wtedy, gdy decydujemy my (albo trzymamy szyk do 10 s); postfiks liczy zmiany szyku RBM (K7). Cel przez AccessTools.TypeByName +
    /// DeclaredMethod (pulapka 15.10); latka raz na proces przy pierwszej bitwie z kluczem (K9). Metoda ma 84 bajty IL (zmierzone na RBMAI.dll Jeffa) -
    /// JIT moze ja wstawic w prefiks RBM i wtedy latka nie biegnie: licznik wywolan wobec liczby "sytuacji decyzji RBM" z passu (C: ryzyko inline).
    /// Watki: latka biegnie w ticku druzyn (TickAgentsAndTeamsImp - takze asynchronicznie), pass na glownym watku w OnMissionTick; nie nachodza na siebie
    /// (Mission:3530 / 3766), stan i tak pod jednym lockiem (Gate), SetArrangementOrder poza lockiem.
    /// Potkniecia: RoutHold.Stumble miejsce "szyk" (w latce pierwsze 3, reszta w liczniku) - funkcji nie gasimy. Linia "Szyk pod ostrzalem (146)" raz na
    /// bitwe (BattleSense.Clean, po linii 140). Klucz NIE: ta sama ocena na sucho (szyko-sekundy, co bysmy wybrali) - szyk zostaje RBM.
    /// Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings".
    /// </summary>
    internal static class CloseRanks
    {
        // ------------------------------------------------------------ stale
        internal const float PassEvery = 0.5f;      // pass szykow (glowny watek) - tyle co tick FormationAI (FormationAI:116)
        internal const float WallFrom = 0.6f;       // ShieldWall od 60% ludzi z tarcza w rece (projekt 135b)
        internal const float WallKeep = 0.5f;       // nasz ShieldWall zostaje do 50% (histereza)
        internal const float WaitShields = 4f;      // czekanie na przelozenie tarcz ze 140 (polecenia 16 na 0.25 s, kontrola po 1.5 s)
        internal const float MinGap = 10f;          // zmiana szyku najwyzej raz na 10 s
        internal const float ContactOff = 40f;      // wyjscie ze zwarcia (wejscie: Doctrine.S1ContactGap)
        internal const float StaleCall = 3f;        // RBM nie wola decyzji od 3 s - nasz szyk wraca w passie
        internal const int MinMen = 30;             // prog RBM (OverrideBehaviorAdvance:401, OverrideBehaviorCharge:296)
        private const byte WNone = 0, WWall = 1, WLoose = 2, WHold = 3;
        private const byte YFire = 0, YContact = 1, YQuiet = 2, YHorse = 3, YUndead = 4, YPlayer = 5, YEmpty = 6, YKind = 7;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class St
        {
            public byte Want, Why;
            public bool Fire, Contact, Ours, SeenOk;
            public ArrangementOrder.ArrangementOrderEnum Seen, Applied, Before;
            public float LastChange = -100f, LastCall = -100f, FireStart = -1f, Ratio;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<Formation, St> Forms = new Dictionary<Formation, St>();
        private static readonly List<Agent> Units = new List<Agent>();
        private static readonly Action<Agent> Collect = a => Units.Add(a);

        private static WeakReference _m;
        private static volatile bool _on;        // klucz TAK + sygnal S4 + bitwa polowa - decyduje latka
        private static volatile bool _live;      // sygnal S4 + bitwa polowa - pass i pomiar (takze klucz NIE)
        private static volatile bool _ended;
        private static volatile float _now;      // pisze glowny watek (Tick), czyta latka
        private static bool _key, _s4, _field, _naval, _written;
        private static float _acc;
        private static string _kind = "?";

        // ------------------------------------------------------------ latka (raz na proces)
        private static bool _patchTried;
        internal static bool Patched;
        private static MethodInfo _t;
        private static string _target = "bez latki", _rbmAdv = "?", _rbmCharge = "?";

        // ------------------------------------------------------------ liczniki misji (pod Gate; _calls i _patchErr przez Interlocked)
        private static int _calls, _patchErr;
        private static int _callsFire, _callsNoState, _sit, _sitFire;
        private static int _toWall, _toLoose, _reactN, _keep, _hold10, _keep10, _waitShields, _horse;
        private static double _ratioWall, _ratioLoose, _react;
        private static int _backContact, _backQuiet, _backHorse, _backOther, _restored, _oursLost, _playerTook, _outside;
        private static float _secWall, _secLoose;
        private static int _rbmRuns, _rbmWall, _rbmChg, _rbmToLine, _rbmToLoose, _rbmToWall, _rbmChgFire;
        private static readonly float[] Dry = new float[5];   // klucz NIE: szyko-sekundy pod ostrzalem w sytuacji decyzji RBM - ShieldWall / Loose / czekanie / RBM (zwarcie...) / RBM (konnica)
        private static readonly int[] Hit = new int[9];       // trafienia przed zwarciem w szykach pod ostrzalem: [szyk 0 nasz ShieldWall, 1 nasz Loose, 2 RBM] x [0 cialo, 1 tarcza w rece, 2 plecy]
        private static readonly HashSet<Formation> FireForms = new HashSet<Formation>(), PlayerSideForms = new HashSet<Formation>(),
                                                   PlayerOrderForms = new HashSet<Formation>(), DecidedForms = new HashSet<Formation>();

        // ------------------------------------------------------------ latka

        /// <summary>Pierwsza bitwa z kluczem TAK (Begin): prefiks i postfiks na RBMAI.Utilities.DecideArrangementOrderForFormation(Formation).</summary>
        private static void EnsurePatched()
        {
            if (_patchTried) return;
            _patchTried = true;
            var h = NightHooks.H;
            if (h == null)
            {
                _target = "brak Harmony";
                Log.Info("Szyk pod ostrzalem (146): brak instancji Harmony - latka NIE wpieta (szyk zostaje RBM).");
                return;
            }
            try
            {
                var type = AccessTools.TypeByName("RBMAI.Utilities");
                if (type == null) _target = "brak typu RBMAI.Utilities";
                else
                {
                    _t = AccessTools.DeclaredMethod(type, "DecideArrangementOrderForFormation", new[] { typeof(Formation) });
                    if (_t == null) _target = "brak metody";
                    else
                    {
                        h.Patch(_t, prefix: new HarmonyMethod(typeof(CloseRanks), nameof(DecidePrefix)) { priority = Priority.Last },
                                    postfix: new HarmonyMethod(typeof(CloseRanks), nameof(DecidePostfix)) { priority = Priority.Last });
                        Patched = Ours(_t);
                        _target = Patched ? "TAK" : "NIEWPIETA";
                    }
                }
            }
            catch (Exception e) { _target = "BLAD " + e.GetType().Name; Stumble("CloseRanks.Patch", e); }
            Log.Info("Szyk pod ostrzalem (146): latka raz na proces (pierwsza bitwa z kluczem AiFormationsUnderFire) - RBMAI.Utilities.DecideArrangementOrderForFormation "
                     + _target + " (prefiks: decyzja RBM pominieta tylko, gdy szyk pod ostrzalem przed zwarciem decydujemy my; postfiks: licznik zmian szyku RBM).");
        }

        private static bool Ours(MethodBase target)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                bool pre = false, post = false;
                foreach (var p in info.Prefixes) if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(CloseRanks)) pre = true;
                foreach (var p in info.Postfixes) if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(CloseRanks)) post = true;
                return pre && post;
            }
            catch { return false; }
        }

        /// <summary>Czy RBM AI wola decyzje szyku: prefiks com.rbmai na BehaviorAdvance.TickOccasionally i BehaviorCharge.CalculateCurrentOrder
        /// (RBM przepina latki co misje w EarlyStart - sprawdzane w AfterStart kazdej bitwy).</summary>
        private static string RbmPrefix(Type type, string name)
        {
            try
            {
                var mi = AccessTools.DeclaredMethod(type, name);
                if (mi == null) return "brak metody";
                var info = Harmony.GetPatchInfo(mi);
                if (info != null) foreach (var p in info.Prefixes) if (p != null && p.owner == "com.rbmai") return "TAK";
                return "NIE";
            }
            catch { return "?"; }
        }

        /// <summary>Przed RBM (tick druzyn - takze asynchroniczny): szyk pod ostrzalem przed zwarciem ustawiamy my (ShieldWall / Loose) i pomijamy cialo RBM;
        /// w czasie 10 s od ostatniej zmiany szyku nic nie zmieniamy (decyzja RBM tez wstrzymana); w pozostalych przypadkach decyduje RBM.
        /// __state = 1 + szyk przed RBM (postfiks liczy zmiane), 0 = RBM nie biegl.</summary>
        public static bool DecidePrefix(Formation formation, out byte __state)
        {
            __state = 0;
            Interlocked.Increment(ref _calls);
            if (!_live || formation == null) return true;
            try
            {
                var cur = formation.ArrangementOrder.OrderEnum;
                var target = ArrangementOrder.ArrangementOrderEnum.Line;
                lock (Gate)
                {
                    St st;
                    if (!Forms.TryGetValue(formation, out st)) { _callsNoState++; __state = (byte)(cur + 1); return true; }
                    float now = _now;
                    st.LastCall = now;
                    // zmiana szyku inna sciezka od ostatniego spojrzenia (RBM w tym samym ticku: OverrideBehaviorAdvance:265 / OverrideBehaviorCharge:229,270
                    // ShieldWall przeciw konnicy - przed ta decyzja; gra; gracz) - tez liczy sie do 10 s, wiec nie przestawiamy szyku RBM co tick
                    if (st.SeenOk && cur != st.Seen)
                    {
                        st.LastChange = now;
                        _outside++;
                        if (st.Ours && cur != st.Applied) { st.Ours = false; _oursLost++; }
                    }
                    st.Seen = cur;
                    st.SeenOk = true;
                    if (st.Fire && !st.Contact) { _callsFire++; DecidedForms.Add(formation); }
                    if (!_on || _ended || !formation.IsAIControlled) { __state = (byte)(cur + 1); return true; }
                    switch (st.Want)
                    {
                        case WWall:
                        case WLoose:
                            target = st.Want == WWall ? ArrangementOrder.ArrangementOrderEnum.ShieldWall : ArrangementOrder.ArrangementOrderEnum.Loose;
                            if (cur == target) { _keep++; return false; }                   // juz w tym szyku (nasz albo RBM) - bez zmiany
                            if (now - st.LastChange < MinGap) { _hold10++; return false; }  // zmiana najwyzej raz na 10 s - szyk zamrozony
                            if (!st.Ours)
                            {
                                st.Before = cur;
                                if (st.FireStart >= 0f) { _react += now - st.FireStart; _reactN++; }
                            }
                            st.Ours = true;
                            st.Applied = target;
                            st.LastChange = now;
                            st.Seen = target;
                            st.SeenOk = true;
                            if (st.Want == WWall) { _toWall++; _ratioWall += st.Ratio; } else { _toLoose++; _ratioLoose += st.Ratio; }
                            break;
                        case WHold:
                            _waitShields++;                                                    // tarcze w drodze (140) - szyk bez zmiany
                            return false;
                        default:
                            if (st.Ours && cur == st.Applied && now - st.LastChange < MinGap) { _keep10++; return false; }   // nasz szyk trzymany do 10 s
                            if (st.Why == YHorse && st.Fire && !st.Contact) _horse++;
                            if (st.Ours)
                            {
                                st.Ours = false;
                                if (st.Why == YContact) _backContact++;
                                else if (st.Why == YQuiet) _backQuiet++;
                                else if (st.Why == YHorse) _backHorse++;
                                else _backOther++;
                            }
                            __state = (byte)(cur + 1);
                            return true;
                    }
                }
                formation.SetArrangementOrder(target == ArrangementOrder.ArrangementOrderEnum.ShieldWall
                                              ? ArrangementOrder.ArrangementOrderShieldWall : ArrangementOrder.ArrangementOrderLoose);
                return false;
            }
            catch (Exception e)
            {
                PatchError("CloseRanks.DecidePrefix", e);
                __state = 0;
                return true;
            }
        }

        /// <summary>Po RBM (K7): zmiana szyku przez decyzje RBM - licznik wedlug szyku, czas zmiany do reguly 10 s.</summary>
        public static void DecidePostfix(Formation formation, byte __state)
        {
            if (__state == 0 || formation == null) return;
            try
            {
                var before = (ArrangementOrder.ArrangementOrderEnum)(__state - 1);
                var after = formation.ArrangementOrder.OrderEnum;
                lock (Gate)
                {
                    _rbmRuns++;
                    if (after == ArrangementOrder.ArrangementOrderEnum.ShieldWall) _rbmWall++;
                    if (after == before) return;
                    _rbmChg++;
                    if (after == ArrangementOrder.ArrangementOrderEnum.Line) _rbmToLine++;
                    else if (after == ArrangementOrder.ArrangementOrderEnum.Loose) _rbmToLoose++;
                    else if (after == ArrangementOrder.ArrangementOrderEnum.ShieldWall) _rbmToWall++;
                    St st;
                    if (Forms.TryGetValue(formation, out st))
                    {
                        st.LastChange = _now;
                        st.Seen = after;
                        st.SeenOk = true;
                        if (st.Fire) _rbmChgFire++;
                    }
                }
            }
            catch (Exception e) { PatchError("CloseRanks.DecidePostfix", e); }
        }

        private static void PatchError(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _patchErr);
            if (n <= 3) RoutHold.Stumble(RoutHold.SiteRanks, where, e);
        }

        private static void Stumble(string where, Exception e)
        {
            RoutHold.Stumble(RoutHold.SiteRanks, where, e);
        }

        // ------------------------------------------------------------ wejscia z BattleSense (glowny watek)

        /// <summary>BattleSense.AfterStart w bitwie, PO ShieldsUp.Begin (sygnal S4): stan od zera, klucz, latka raz na proces (K9), RBM AI.</summary>
        internal static void Begin(Mission m)
        {
            _on = false;
            _live = false;
            _ended = false;
            Clear();
            if (m == null) return;
            var c = Settings.Current;
            _m = new WeakReference(m);
            _written = false;
            _kind = MissionTeams.Kind(m);
            _naval = false;
            try { _naval = m.IsNavalBattle || m.IsNavalRaidBattle; } catch { }
            _key = c != null && c.AiFormationsUnderFire;
            _s4 = ShieldsUp.On;
            _field = false;
            try { _field = m.IsFieldBattle; } catch { }
            if (_key) EnsurePatched();
            _rbmAdv = RbmPrefix(typeof(BehaviorAdvance), "TickOccasionally");
            _rbmCharge = RbmPrefix(typeof(BehaviorCharge), "CalculateCurrentOrder");
            _now = m.CurrentTime;
            _live = _s4 && _field;
            _on = _live && _key && Patched;
        }

        /// <summary>BattleSense.OnMissionTick (po ShieldsUp.Tick - S4 z tej klatki): pass szykow co 0.5 s.</summary>
        internal static void Tick(Mission m, float dt)
        {
            if (!_live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            _now = m.CurrentTime;
            bool deployed = false;
            try { deployed = m.IsDeploymentFinished; } catch { }
            if (!deployed) return;
            _acc += dt;
            if (_acc < PassEvery) return;
            _acc = 0f;
            if (m.MissionEnded) _ended = true;
            var teams = m.Teams;
            var pt = m.PlayerTeam;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var t = teams[i];
                if (t == null) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    try { One(m, t, f, pt); }
                    catch (Exception e) { Units.Clear(); Stumble("CloseRanks.Szyk", e); }
                }
            }
        }

        /// <summary>ShieldsUp.Hit (glowny watek, kolejka pociskow): trafienie w szyku pod ostrzalem przed zwarciem - wedlug szyku (nasz ShieldWall / nasz Loose /
        /// szyk RBM) i miejsca (0 cialo, 1 tarcza w rece, 2 tarcza na plecach).</summary>
        internal static void OnHit(Agent v, byte kind)
        {
            if (!_live || v == null || kind > 2) return;
            try   // wlasny try - wyjatek tutaj nie moze przerwac znacznika 140 tego trafienia (ShieldsUp.Hit dalej)
            {
                var f = v.Formation;
                if (f == null) return;
                lock (Gate)
                {
                    St st;
                    if (!Forms.TryGetValue(f, out st) || !st.Fire || st.Contact) return;
                    int cat = st.Ours ? (st.Applied == ArrangementOrder.ArrangementOrderEnum.ShieldWall ? 0 : 1) : 2;
                    Hit[cat * 3 + kind]++;
                }
            }
            catch (Exception e) { Stumble("CloseRanks.OnHit", e); }
        }

        /// <summary>BattleSense.Clean: linia raz na bitwe, stan od zera (latka przestaje decydowac).</summary>
        internal static void End(Mission m)
        {
            var w = _m;
            if (w == null) return;
            if (m != null && !ReferenceEquals(w.Target, m)) return;
            _on = false;
            _live = false;
            if (!_written)
            {
                _written = true;
                try { Write(); } catch (Exception e) { Stumble("CloseRanks.Write", e); }
            }
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            lock (Gate)
            {
                Forms.Clear();
                FireForms.Clear(); PlayerSideForms.Clear(); PlayerOrderForms.Clear(); DecidedForms.Clear();
                _callsFire = _callsNoState = _sit = _sitFire = 0;
                _toWall = _toLoose = _reactN = _keep = _hold10 = _keep10 = _waitShields = _horse = 0;
                _ratioWall = _ratioLoose = _react = 0;
                _backContact = _backQuiet = _backHorse = _backOther = _restored = _oursLost = _playerTook = _outside = 0;
                _secWall = _secLoose = 0f;
                _rbmRuns = _rbmWall = _rbmChg = _rbmToLine = _rbmToLoose = _rbmToWall = _rbmChgFire = 0;
                for (int i = 0; i < Dry.Length; i++) Dry[i] = 0f;
                for (int i = 0; i < Hit.Length; i++) Hit[i] = 0;
            }
            Units.Clear();
            _acc = 0f;
            Interlocked.Exchange(ref _calls, 0);
            Interlocked.Exchange(ref _patchErr, 0);
        }

        // ------------------------------------------------------------ pass szykow (co 0.5 s)

        private static ArrangementOrder Of(ArrangementOrder.ArrangementOrderEnum e)
        {
            switch (e)
            {
                case ArrangementOrder.ArrangementOrderEnum.Circle: return ArrangementOrder.ArrangementOrderCircle;
                case ArrangementOrder.ArrangementOrderEnum.Column: return ArrangementOrder.ArrangementOrderColumn;
                case ArrangementOrder.ArrangementOrderEnum.Loose: return ArrangementOrder.ArrangementOrderLoose;
                case ArrangementOrder.ArrangementOrderEnum.Scatter: return ArrangementOrder.ArrangementOrderScatter;
                case ArrangementOrder.ArrangementOrderEnum.ShieldWall: return ArrangementOrder.ArrangementOrderShieldWall;
                case ArrangementOrder.ArrangementOrderEnum.Skein: return ArrangementOrder.ArrangementOrderSkein;
                case ArrangementOrder.ArrangementOrderEnum.Square: return ArrangementOrder.ArrangementOrderSquare;
                default: return ArrangementOrder.ArrangementOrderLine;
            }
        }

        private static void One(Mission m, Team t, Formation f, Team pt)
        {
            St st;
            lock (Gate) Forms.TryGetValue(f, out st);
            var q = f.QuerySystem;
            bool inf = q != null && q.IsInfantryFormation;
            if (st == null)
            {
                if (!inf) return;   // stan tylko dla piechoty (RBM wola decyzje tylko dla IsInfantryFormation)
                st = new St();
                lock (Gate) Forms[f] = st;
            }
            bool ai = f.IsAIControlled;
            bool fire = _s4 && ShieldsUp.FormFire(f);
            bool side = pt != null && (ReferenceEquals(t, pt) || t.Side == pt.Side);
            var cur = f.ArrangementOrder.OrderEnum;
            bool sit = false;
            float gap = float.MaxValue;
            if (ai && inf)
            {
                var fai = f.AI;
                var b = fai != null ? fai.ActiveBehavior : null;
                sit = f.CountOfUnitsWithoutDetachedOnes >= MinMen && (b is BehaviorAdvance || b is BehaviorCharge);
                gap = Doctrine.FootGap(t, f);
            }
            bool contact = st.Contact ? gap <= ContactOff : gap <= Doctrine.S1ContactGap;
            // spis ludzi tylko pod ostrzalem przed zwarciem (szyk AI piechoty): w rece / w ekwipunku / umarli, bez PANIKI (przelacznik 139)
            int men = 0, hand = 0, has = 0, dead = 0;
            bool horse = false;
            if (fire && ai && inf && !contact)
            {
                Units.Clear();
                f.ApplyActionOnEachUnit(Collect);   // gra iteruje swoje listy - my tylko zbieramy
                for (int u = 0; u < Units.Count; u++)
                {
                    var a = Units[u];
                    if (a == null || !a.IsActive() || !a.IsHuman) continue;
                    if (SurvivalMode.InPanic(a)) continue;
                    men++;
                    if (Undead.Character(a.Character)) dead++;
                    if (ShieldsUp.OffShield(a)) hand++;
                    if (a.HasShieldCached) has++;
                }
                Units.Clear();
                horse = ShieldsUp.CavNear(m, f);
            }
            bool restore = false;
            var back = ArrangementOrder.ArrangementOrderEnum.Line;
            lock (Gate)
            {
                // zmiana szyku poza latka (inne sciezki RBM - OverrideBehaviorAdvance:265 / OverrideBehaviorCharge:229,270 - gra, gracz) - tez liczy sie do 10 s
                if (st.SeenOk && cur != st.Seen)
                {
                    st.LastChange = _now;
                    _outside++;
                    if (st.Ours && cur != st.Applied) { st.Ours = false; _oursLost++; }
                }
                st.Seen = cur;
                st.SeenOk = true;
                if (!ai && st.Ours) { st.Ours = false; _playerTook++; }   // rozkazy gracza - nigdy nie ruszamy
                if (fire && inf)
                {
                    if (st.FireStart < 0f) st.FireStart = _now;
                    FireForms.Add(f);
                    if (!ai) PlayerOrderForms.Add(f);
                    else if (side) PlayerSideForms.Add(f);
                }
                else st.FireStart = -1f;
                st.Fire = fire && inf;
                st.Contact = contact;
                if (sit) { _sit++; if (fire) _sitFire++; }
                // decyzja
                byte want = WNone, why = YFire;
                if (!ai) why = YPlayer;
                else if (!inf) why = YKind;
                else if (!fire) why = YQuiet;
                else if (contact) why = YContact;
                else if (men == 0) why = YEmpty;
                else if (2 * dead >= men) why = YUndead;
                else
                {
                    float r = (float)hand / men;
                    st.Ratio = r;
                    float thr = st.Ours && st.Applied == ArrangementOrder.ArrangementOrderEnum.ShieldWall ? WallKeep : WallFrom;
                    if (r >= thr) want = WWall;
                    else if ((float)has / men >= WallFrom && st.FireStart >= 0f && _now - st.FireStart < WaitShields) want = WHold;
                    else if (horse) why = YHorse;
                    else want = WLoose;
                }
                st.Want = want;
                st.Why = why;
                if (st.Ours)
                {
                    if (st.Applied == ArrangementOrder.ArrangementOrderEnum.ShieldWall) _secWall += PassEvery; else _secLoose += PassEvery;
                }
                // klucz NIE: co bysmy wybrali (szyko-sekundy w sytuacji decyzji RBM pod ostrzalem)
                if (!_on && sit && fire)
                {
                    if (want == WWall) Dry[0] += PassEvery;
                    else if (want == WLoose) Dry[1] += PassEvery;
                    else if (want == WHold) Dry[2] += PassEvery;
                    else if (why == YHorse) Dry[4] += PassEvery;
                    else Dry[3] += PassEvery;
                }
                // nasz szyk niepotrzebny, a RBM nie wola decyzji (wrog > 150 m, < 30 ludzi, inne zachowanie) - przywrocenie szyku sprzed naszej zmiany
                if (_on && !_ended && st.Ours && ai && want == WNone && cur == st.Applied && _now - st.LastChange >= MinGap && _now - st.LastCall > StaleCall)
                {
                    restore = true;
                    back = st.Before;
                    st.Ours = false;
                    st.LastChange = _now;
                    st.Seen = back;
                    _restored++;
                }
            }
            if (restore) f.SetArrangementOrder(Of(back));
        }

        // ------------------------------------------------------------ linia bitwy

        private static string F1(double v) { return v.ToString("0.0", Inv); }

        private static string Avg(double sum, int n, string fmt) { return n > 0 ? (sum / n).ToString(fmt, Inv) : "-"; }

        private static void Write()
        {
            if (!_s4 || !_field)
            {
                if (!_key || _naval) return;   // klucz NIE albo morze (140 bez linii) - bez linii
                Log.Info("Szyk pod ostrzalem (146): misja " + _kind + ", klucz TAK - nieczynne: "
                         + (!_s4 ? "brak sygnalu S4 (AI Shields Up Under Fire NIE)" : "tylko bitwa polowa") + ".");
                return;
            }
            int stum;
            lock (RoutHold.Gate) { stum = RoutHold.StumblesLocked(RoutHold.SiteRanks); }
            int calls = Volatile.Read(ref _calls), perr = Volatile.Read(ref _patchErr);
            var sb = new StringBuilder(2000);
            lock (Gate)
            {
                sb.Append("Szyk pod ostrzalem (146): misja ").Append(_kind).Append(", klucz ").Append(_key ? "TAK" : "NIE (tylko pomiar: szyk zostaje RBM)")
                  .Append(" (ShieldWall od ").Append((WallFrom * 100f).ToString("0", Inv)).Append("% ludzi z tarcza w rece (nasz zostaje do ")
                  .Append((WallKeep * 100f).ToString("0", Inv)).Append("%), mniej - Loose, bez Loose przy konnicy wroga w ").Append(ShieldsUp.CavalryNear.ToString("0", Inv))
                  .Append(" m; czekanie na tarcze do ").Append(WaitShields.ToString("0", Inv)).Append(" s; zmiana najwyzej co ").Append(MinGap.ToString("0", Inv))
                  .Append(" s; zwarcie = pieszy wrog w ").Append(Doctrine.S1ContactGap.ToString("0", Inv)).Append(" m (wyjscie ").Append(ContactOff.ToString("0", Inv))
                  .Append(" m) - wtedy RBM; sygnal S4 z 140; tylko szyki AI piechoty)")
                  .Append(" | latka RBMAI.Utilities.DecideArrangementOrderForFormation ").Append(_target).Append(" (prefiks + postfiks), RBM AI: BehaviorAdvance ")
                  .Append(_rbmAdv).Append(", BehaviorCharge ").Append(_rbmCharge).Append("; wywolan latki ").Append(calls).Append(" (pod ostrzalem przed zwarciem ")
                  .Append(_callsFire).Append(", bez stanu ").Append(_callsNoState).Append("), sytuacji decyzji RBM (szyk AI piechoty od ").Append(MinMen)
                  .Append(", Advance / Charge) w passach co ").Append(PassEvery.ToString("0.0", Inv)).Append(" s: ").Append(_sit).Append(" (pod ostrzalem ").Append(_sitFire).Append(')');
                if (Patched && calls == 0 && _sit > 0 && (_rbmAdv == "TAK" || _rbmCharge == "TAK")) sb.Append("; UWAGA: 0 wywolan przy sytuacjach RBM - latka nie biegnie (JIT wstawil metode do prefiksu RBM?)");
                sb.Append(" | szyki pod ostrzalem: ").Append(FireForms.Count).Append(" (AI strony gracza ").Append(PlayerSideForms.Count)
                  .Append(", pod rozkazami gracza - pominiete ").Append(PlayerOrderForms.Count).Append("), z decyzja RBM pod ostrzalem ").Append(DecidedForms.Count);
                if (_key)
                {
                    sb.Append(" | nasze zmiany: ShieldWall ").Append(_toWall).Append(" (sr. tarcz w rece ").Append(Avg(_ratioWall, _toWall, "0.00")).Append("), Loose ")
                      .Append(_toLoose).Append(" (sr. ").Append(Avg(_ratioLoose, _toLoose, "0.00")).Append("), sr. od poczatku ostrzalu ").Append(Avg(_react, _reactN, "0.0"))
                      .Append(" s; decyzji: juz w szyku ").Append(_keep).Append(", wstrzymane do ").Append(MinGap.ToString("0", Inv)).Append(" s ").Append(_hold10)
                      .Append(", czekanie na tarcze ").Append(_waitShields).Append(", bez Loose przy konnicy ").Append(_horse)
                      .Append(" | oddane RBM: ").Append(_backContact + _backQuiet + _backHorse + _backOther).Append(" (zwarcie ").Append(_backContact)
                      .Append(", po ostrzale ").Append(_backQuiet).Append(", konnica ").Append(_backHorse).Append(", inne ").Append(_backOther)
                      .Append("), nasz szyk trzymany do ").Append(MinGap.ToString("0", Inv)).Append(" s ").Append(_keep10).Append(" decyzji; przywrocone poza decyzja RBM ")
                      .Append(_restored).Append("; nasz szyk zmieniony inna sciezka ").Append(_oursLost).Append(", przejete przez gracza ").Append(_playerTook)
                      .Append(" | czas w naszym szyku: ShieldWall ").Append(F1(_secWall)).Append(" s, Loose ").Append(F1(_secLoose)).Append(" s (szyko-sekundy)");
                }
                else
                {
                    sb.Append(" | na sucho (szyko-sekundy pod ostrzalem w sytuacji decyzji RBM): ShieldWall ").Append(F1(Dry[0])).Append(", Loose ").Append(F1(Dry[1]))
                      .Append(", czekanie na tarcze ").Append(F1(Dry[2])).Append(", RBM - konnica w ").Append(ShieldsUp.CavalryNear.ToString("0", Inv)).Append(" m ")
                      .Append(F1(Dry[4])).Append(", RBM - zwarcie / umarli / inne ").Append(F1(Dry[3]));
                }
                sb.Append(" | zmiany szyku RBM (K7): w tej metodzie ").Append(_rbmChg).Append(" (Line ").Append(_rbmToLine).Append(", Loose ").Append(_rbmToLoose)
                  .Append(", ShieldWall ").Append(_rbmToWall).Append("; pod ostrzalem ").Append(_rbmChgFire).Append("; RBM wybral ShieldWall w ").Append(_rbmWall)
                  .Append(" z ").Append(_rbmRuns).Append(" decyzji), poza ta metoda (inne sciezki RBM, gra, gracz) ").Append(_outside)
                  .Append(" | trafienia pociskami przed zwarciem w szykach pod ostrzalem (cialo/tarcza/plecy): nasz ShieldWall ").Append(Hit[0]).Append('/').Append(Hit[1])
                  .Append('/').Append(Hit[2]).Append(", nasz Loose ").Append(Hit[3]).Append('/').Append(Hit[4]).Append('/').Append(Hit[5])
                  .Append(", szyk bez naszej zmiany ").Append(Hit[6]).Append('/').Append(Hit[7]).Append('/').Append(Hit[8])
                  .Append(" | potkniecia szyk ").Append(stum).Append(" (w latce ").Append(perr).Append(").");
            }
            Log.Info(sb.ToString());
        }
    }
}
