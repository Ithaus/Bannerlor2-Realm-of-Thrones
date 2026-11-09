using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 383 (Jeff 09.11: "nad brama sa kamienie ktore mozna zrzucac na atakujacych ... a nie jak teraz one leza i nikt ich nie uzywa, tak samo jak uzywaja
    /// machin powinni uzywac wszelkich dostepnych srodkow aby sie bronic"). OBRONCY ZRZUCAJA KAMIENIE. Dowod (dekompilacja 1.4.8, mb_full.cs):
    ///  - gracz-wodz: AssignPlayerRoleInTeamMissionController.OnTeamDeployed daje kazdemu szykowi druzyny gracza PlayerOwner -> SetControlledByAI(false),
    ///    czyli IsAIControlled = false (log 370: szyki strony O "(gracz)");
    ///  - StonePile.TickOccasionally: gdy w VolumeBox pod murem sa atakujacy, wybiera szyk obroncow z najblizszym czlowiekiem i wola
    ///    bestFormation.StartUsingMachine(this) BEZ flagi rozkazu gracza; ModuleExtensions.StartUsingMachine robi JoinDetachment tylko gdy
    ///    isPlayerOrder || IsAIControlled -> dla szyku gracza cicha no-op co 0.5-1 s, a ze szyk gracza jest najblizszy, kamieni nie dostaje nikt;
    ///    zwolnienie (ReleaseAllUserAgentsAndFormations -> StopUsingMachine bez flagi) - ta sama no-op;
    ///  - machiny miotajace gra obsadza dobrze (SiegeWeapon.TickAux: StartUsingMachine(this, !formation.IsAIControlled)) - blad dotyczy tylko kamieni;
    ///  - zaden inny mod nie zna StonePile (grep RBM, RBM_WS, EpicSieges, VanillaAiSiegesforRBM, RBL, ROT, BK, StrategicCampaignAI).
    /// ZMIANA (klucz SiegeDefendersUseStones; NIE = gra co do bitu): transpiler podmienia w StonePile.TickOccasionally wywolanie StartUsingMachine na Start,
    /// a w StonePile.ReleaseAllUserAgentsAndFormations StopUsingMachine na Stop. Start daje flage rozkazu, gdy szyk obroncow nie jest pod AI (ta sama zasada,
    /// co gra dla machin miotajacych), nie jest w odwrocie, a misja sie nie konczy; Stop - zawsze dla szyku nie pod AI (kupy kamieni gracz nie moze
    /// rozkazac, wiec szyk gracza jest przy kupie tylko z naszego Start - zwolnienie musi dzialac takze po wylaczeniu klucza). Reszta logiki gry bez zmian:
    /// ludzi tylu, ilu punktow rzutu (MaxUserCount), zwolnienie po kamieniach (AmmoCount 0), gdy atakujacy odejda spod muru albo wrog blizej niz 5 m.
    /// Cel przez AccessTools.DeclaredMethod; transpiler liczy podmiany - brak kotwicy = IL bez zmian i "latka nie weszla" w logu.
    /// Linia "Kamienie (383)" co 60 s czasu misji szturmu i na koniec misji (kupy, obsadzone, pobrane kamienie, rzuty i trafienia kamieniem, machiny obrony).
    /// Koszt: jedno porownanie przy wywolaniu (co 0.5-1 s na kupe); log - przeglad MissionObjects co 60 s. Wyjatki liczone (potkniecia), bez wylacznika.
    /// </summary>
    internal static class SiegeStones383
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int ErrMax = 3;
        private const float Every = 60f;

        private static bool _installed;
        private static string _installInfo = "NIE";
        private static int _anchorStart = -1, _anchorStop = -1;

        // liczniki misji
        private static int _forcedStart, _forcedStop, _throws, _hits, _stumbles, _errs;
        private static readonly HashSet<string> _stoneIds = new HashSet<string>(StringComparer.Ordinal) { "boulder" };

        private static bool On { get { var s = Settings.Current; return s != null && s.SiegeDefendersUseStones; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeStones383." + where, e); } catch { }
        }

        internal static void Install(Harmony h)
        {
            if (_installed || h == null) return;
            _installed = true;
            int ok = 0;
            var miss = new List<string>();
            ok += One(h, AccessTools.DeclaredMethod(typeof(StonePile), "TickOccasionally"), "StonePile.TickOccasionally", nameof(TickTranspiler), miss);
            if (_anchorStart <= 0) miss.Add("TickOccasionally: StartUsingMachine (kotwic " + _anchorStart + ")");
            ok += One(h, AccessTools.DeclaredMethod(typeof(StonePile), "ReleaseAllUserAgentsAndFormations"), "StonePile.ReleaseAllUserAgentsAndFormations",
                      nameof(ReleaseTranspiler), miss);
            if (_anchorStop <= 0) miss.Add("ReleaseAllUserAgentsAndFormations: StopUsingMachine (kotwic " + _anchorStop + ")");
            _installInfo = ok + "/2, podmian Start " + Math.Max(0, _anchorStart) + " / Stop " + Math.Max(0, _anchorStop)
                           + (miss.Count > 0 ? " | latka nie weszla: " + string.Join(", ", miss) : "");
            try { Log.Info("Kamienie (383): latki wpiete " + _installInfo + "."); } catch { }
        }

        private static int One(Harmony h, MethodBase target, string name, string transpiler, List<string> miss)
        {
            try
            {
                if (target == null) { miss.Add(name); return 0; }
                h.Patch(target, transpiler: new HarmonyMethod(typeof(SiegeStones383), transpiler));
                return 1;
            }
            catch (Exception e) { Stumble("Install." + name, e); miss.Add(name + " (" + e.GetType().Name + ")"); return 0; }
        }

        // ---------------- transpilery: podmiana wywolan (ta sama sygnatura: Formation, UsableMachine, bool) ----------------

        private static List<CodeInstruction> Swap(IEnumerable<CodeInstruction> instructions, string orig, string repl, out int hits)
        {
            var L = new List<CodeInstruction>(instructions);
            hits = 0;
            try
            {
                MethodInfo o = AccessTools.DeclaredMethod(typeof(ModuleExtensions), orig, new[] { typeof(Formation), typeof(UsableMachine), typeof(bool) });
                MethodInfo r = AccessTools.DeclaredMethod(typeof(SiegeStones383), repl);
                if (o == null || r == null) { hits = -1; return L; }
                foreach (var c in L)
                    if ((c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, o)) { c.opcode = OpCodes.Call; c.operand = r; hits++; }
            }
            catch (Exception e) { hits = -2; Stumble("Swap." + orig, e); }
            return L;
        }

        public static IEnumerable<CodeInstruction> TickTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var L = Swap(instructions, "StartUsingMachine", nameof(Start), out int n);
            _anchorStart = n;
            return L;
        }

        public static IEnumerable<CodeInstruction> ReleaseTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var L = Swap(instructions, "StopUsingMachine", nameof(Stop), out int n);
            _anchorStop = n;
            return L;
        }

        /// <summary>Zamiast ModuleExtensions.StartUsingMachine w StonePile.TickOccasionally.</summary>
        public static void Start(Formation formation, UsableMachine usable, bool isPlayerOrder)
        {
            bool force = false;
            try
            {
                if (!isPlayerOrder && formation != null && On && !formation.IsAIControlled && formation.Team != null
                    && formation.Team.Side == BattleSideEnum.Defender && Mission.Current != null && !Mission.Current.IsMissionEnding
                    && formation.GetReadonlyMovementOrderReference().OrderEnum != MovementOrder.MovementOrderEnum.Retreat
                    && HasVolumeBoxes(usable))
                    force = true;
            }
            catch (Exception e) { Stumble("Start", e); }
            if (force) _forcedStart++;
            formation.StartUsingMachine(usable, isPlayerOrder || force);
        }

        // 383b: kupa bez VolumeBox (gra: _volumeBoxTimerPairs.Count == 0 -> "flag" stale prawdziwe) wolalaby ludzi przez cala bitwe, takze bez
        // atakujacych pod murem - dla szyku gracza flagi wtedy nie dajemy (zachowanie gry). Pole czytane refleksja, wynik pamietany per kupa w misji.
        private static readonly FieldInfo _fBoxes = AccessTools.Field(typeof(StonePile), "_volumeBoxTimerPairs");
        private static readonly Dictionary<UsableMachine, bool> _boxes = new Dictionary<UsableMachine, bool>();
        private static int _noBoxPiles;

        private static bool HasVolumeBoxes(UsableMachine usable)
        {
            if (usable == null || _fBoxes == null) return false;
            bool has;
            if (_boxes.TryGetValue(usable, out has)) return has;
            var c = _fBoxes.GetValue(usable) as System.Collections.ICollection;
            has = c != null && c.Count > 0;
            _boxes[usable] = has;
            if (!has) _noBoxPiles++;
            return has;
        }

        /// <summary>Zamiast ModuleExtensions.StopUsingMachine w StonePile.ReleaseAllUserAgentsAndFormations.</summary>
        public static void Stop(Formation formation, UsableMachine usable, bool isPlayerOrder)
        {
            bool force = false;
            try { force = !isPlayerOrder && formation != null && !formation.IsAIControlled; }
            catch (Exception e) { Stumble("Stop", e); }
            if (force) _forcedStop++;
            formation.StopUsingMachine(usable, isPlayerOrder || force);
        }

        private static void Reset()
        {
            _forcedStart = _forcedStop = _throws = _hits = _stumbles = _errs = 0;
            _boxes.Clear(); _noBoxPiles = 0;
            _stoneIds.Clear();
            _stoneIds.Add("boulder");
        }

        // ---------------- pomiar w misji ----------------

        internal sealed class Logic : MissionLogic
        {
            private bool _known, _siege, _written;
            private float _next = -1f;

            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            private bool Siege()
            {
                if (!_known)
                {
                    try { _siege = Mission != null && Mission.IsSiegeBattle; } catch { _siege = false; }
                    _known = true;
                }
                return _siege;
            }

            public override void OnMissionTick(float dt)
            {
                if (_written || !Siege()) return;
                try
                {
                    float now = Mission.CurrentTime;
                    if (_next < 0f) { _next = now + Every; Scan(false); return; }   // pierwszy przeglad: identyfikatory kamieni z kup (bez linii)
                    if (now < _next) return;
                    _next = now + Every;
                    Scan(true);
                }
                catch (Exception e) { Stumble("Tick", e); }
            }

            public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
            {
                if (!_siege) return;
                try
                {
                    if (shooterAgent == null || weaponIndex == EquipmentIndex.None || shooterAgent.Team == null || shooterAgent.Team.Side != BattleSideEnum.Defender) return;
                    var it = shooterAgent.Equipment[weaponIndex].Item;
                    if (it != null && _stoneIds.Contains(it.StringId)) _throws++;
                }
                catch (Exception e) { Stumble("Shoot", e); }
            }

            public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
            {
                if (!_siege) return;
                try
                {
                    if (affectorAgent == null || affectedAgent == null || affectorAgent.Team == null || affectedAgent.Team == null) return;
                    if (affectorAgent.Team.Side != BattleSideEnum.Defender || affectedAgent.Team.Side != BattleSideEnum.Attacker) return;
                    var it = affectorWeapon.Item;
                    if (it != null && _stoneIds.Contains(it.StringId)) _hits++;
                }
                catch (Exception e) { Stumble("Hit", e); }
            }

            protected override void OnEndMission()
            {
                Final();
                base.OnEndMission();
            }

            public override void OnRemoveBehavior()
            {
                Final();
                base.OnRemoveBehavior();
            }

            private void Final()
            {
                if (_written) return;
                _written = true;
                if (_siege) Scan(true, " (koniec misji)");
                Reset();
            }

            private void Scan(bool write, string tag = "")
            {
                int piles = 0, manned = 0, taken = 0, left = 0, empty = 0, engines = 0, enginesManned = 0;
                try
                {
                    var objs = Mission != null ? Mission.MissionObjects : null;
                    if (objs == null) return;
                    foreach (var o in objs)
                    {
                        try
                        {
                            if (o is StonePile p)
                            {
                                if (p.IsDisabled || p.IsDeactivated) continue;
                                piles++;
                                if (!string.IsNullOrEmpty(p.GivenItemID)) _stoneIds.Add(p.GivenItemID);
                                int ammo = p.AmmoCount;
                                left += Math.Max(0, ammo);
                                taken += Math.Max(0, p.StartingAmmoCount - ammo);
                                if (ammo <= 0) empty++;
                                bool used = p.UserFormations.Count > 0;
                                if (!used)
                                    foreach (var sp in p.StandingPoints)
                                        if (sp.HasUser || sp.HasAIMovingTo) { used = true; break; }
                                if (used) manned++;
                            }
                            else if (o is RangedSiegeWeapon w)
                            {
                                if (w.IsDisabled || w.IsDestroyed || w.Side != BattleSideEnum.Defender) continue;
                                engines++;
                                if (w.PilotAgent != null || w.UserCountIncludingInStruckAction > 0) enginesManned++;
                            }
                        }
                        catch (Exception e) { Stumble("Scan.obj", e); }
                    }
                    if (!write || (piles == 0 && engines == 0 && _forcedStart == 0 && _stumbles == 0)) return;
                    Log.Info("Kamienie (383)" + tag + ": klucz " + (On ? "TAK" : "NIE") + ", kup " + piles + ", obsadzone " + manned + ", pustych " + empty
                             + ", kamieni pobranych " + taken + " (zostalo " + left + "), rzutow " + _throws + ", trafien " + _hits
                             + ", przydzialow szykom gracza " + _forcedStart + " (zwolnien " + _forcedStop + ", kup bez strefy pod murem " + _noBoxPiles + "), machin obrony obsadzonych "
                             + enginesManned + "/" + engines + ", czas misji " + (Mission != null ? Mission.CurrentTime.ToString("0", Inv) : "-")
                             + " s, potkniecia " + _stumbles + " | latki " + _installInfo + ".");
                }
                catch (Exception e) { Stumble("Scan", e); }
            }
        }
    }
}
