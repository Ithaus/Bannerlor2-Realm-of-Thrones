using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// STRZALY I BELTY SIE ZUZYWAJA (Jeff 04.10: "ok" na regule z docs/HISTORIA-STRZALY.md).
    /// Dotad amunicja byla niewyczerpalna: DTE oddaje kolczan do zbrojowni w CALOSCI, gdy nie jest pusty
    /// (Global.IsAmmoAndEmpty), a latka CrashScribe (Mends.QuiversComeBack) zwracala nawet puste.
    ///
    /// Historycznie (Poitiers, Towton, Bretania 1343; zakupy korony setkami tysiecy): strzaly zbieral ten,
    /// kto utrzymal pole; duzo wymagalo naprawy (pioro, grot), czesc przepadala. Regula (szacunek - zadne zrodlo
    /// nie podaje procentu): z WYSTRZELONYCH strzal zwyciezca odzyskuje `AmmoRecoverPercent` (45%) od razu i
    /// `AmmoRepairPercent` (20%) po naprawie przez fletcherow przy wojsku (wliczone w zold); reszta (~35%) przepada.
    /// Pokonany traci wszystko, co wystrzelil. Niewystrzelone zostaja. DTE liczy kolczan jako jedna sztuke, wiec
    /// kolczan wraca z prawdopodobienstwem: 1 - wystrzelona czesc x (1 - odzysk).
    /// Postfix na IsAmmoAndEmpty (Priority.Last - po latce CrashScribe): true = kolczan przepada.
    /// </summary>
    internal static class AmmoRecovery
    {
        private static readonly Random _rng = new Random();
        private static int _kept, _lost;

        // E8/A (250, PLAN 8.4b): ODZYSK WEDLUG STRONY AGENTA. DTE wola IsAmmoAndEmpty z Global.ProcessAgentEquipment dla agentow WSZYSTKICH partii bitwy
        // gracza, a o odzysku decydowal dotad wynik GRACZA takze dla wroga: wygrana gracza - pokonany wrog, ktory dotrwal do konca misji, odzyskiwal jak
        // zwyciezca; kleska gracza - zwycieski wrog tracil wszystko, co wystrzelil. Prefiks na ProcessAgentEquipment pamieta biezacego agenta
        // ([ThreadStatic], finalizer przywraca poprzedniego), a Postfix porownuje strone agenta (Team.Side) ze strona, ktora utrzymala pole - ta sama regula
        // co rozliczenie DTE i HorseFate.FinalizePrefix (misja rozstrzygnieta: PlayerVictory - strona gracza, PlayerDefeated - strona przeciwna).
        // Wynik nieznany (agent zdjety w trakcie misji), misja nierozstrzygnieta albo agent bez strony - stara regula (wynik gracza). Klucz AmmoRecoveryBySide.
        [ThreadStatic] private static Agent _agent;
        private static bool _sideWired;
        private static int _sideStumbles;
        private static readonly int[] _side = new int[6];   // wrocilo / przepadlo: strona gracza [0, 1], przeciwnik [2, 3], stara regula [4, 5]

        public static void AgentPrefix(Agent __0, out Agent __state) { __state = _agent; _agent = __0; }

        public static void AgentFinalizer(Agent __state) { _agent = __state; }

        /// <summary>Czy strona biezacego agenta utrzymala pole. null = nie wiadomo (wynik misji nieznany albo nierozstrzygniety, brak agenta lub strony) -
        /// wtedy stara regula. who: 0 strona gracza, 1 przeciwnik, 2 stara regula.</summary>
        private static bool? AgentHeldField(out int who)
        {
            who = 2;
            try
            {
                var a = _agent;
                var m = Mission.Current;
                if (a == null || m == null) return null;
                var r = m.MissionResult;
                if (r == null || r.PlayerVictory == r.PlayerDefeated) return null;   // w trakcie misji albo bez rozstrzygniecia (np. obroncy cofneli sie do stolpu)
                var pt = m.PlayerTeam;
                var at = a.Team;
                if (pt == null || at == null) return null;
                BattleSideEnum ps = pt.Side, sa = at.Side;
                if (ps != BattleSideEnum.Attacker && ps != BattleSideEnum.Defender) return null;
                if (sa != BattleSideEnum.Attacker && sa != BattleSideEnum.Defender) return null;
                bool own = sa == ps;
                who = own ? 0 : 1;
                return own == r.PlayerVictory;
            }
            catch { _sideStumbles++; who = 2; return null; }
        }

        public static void Postfix(MissionWeapon? __0, ref bool __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AmmoRecoveryEnabled || !__0.HasValue) return;
                var mw = __0.Value;
                if (mw.IsEmpty || mw.Item == null) return;
                if (!ArmouryBehavior.IsMissile(mw.Item)) return;   // E8/C (R3): jeden predykat "pocisk" - strzaly, belty, bron miotana (ten sam zbior co dotad)
                int max = Math.Max(1, (int)mw.ModifiedMaxAmount);
                float used = 1f - Math.Max(0, (int)mw.Amount) / (float)max;
                if (used <= 0.001f) { __result = false; return; }        // nic nie wystrzelil - kolczan wraca
                float recover = (s.AmmoRecoverPercent + s.AmmoRepairPercent) / 100f;
                // E8/A: strona agenta wobec strony, ktora utrzymala pole; gdy nie wiadomo - stara regula (wynik gracza dla wszystkich)
                int who = 2;
                bool? held = s.AmmoRecoveryBySide ? AgentHeldField(out who) : null;
                if (held.HasValue)
                {
                    if (!held.Value) recover = 0f;                        // strona agenta oddala pole
                }
                else
                {
                    bool? won = PlayerWon();
                    if (won.HasValue && !won.Value) recover = 0f;         // pole w rekach wroga
                }
                float lose = used * (1f - Math.Max(0f, Math.Min(1f, recover)));
                bool lost = _rng.NextDouble() < lose;
                __result = lost;
                if (lost) _lost++; else _kept++;
                _side[who * 2 + (lost ? 1 : 0)]++;
            }
            catch { }
        }

        /// <summary>Wynik bitwy gracza, gdy juz znany (przy zwrocie po bitwie); null w trakcie.</summary>
        private static bool? PlayerWon()
        {
            try
            {
                var m = Mission.Current;
                var r = m != null ? m.MissionResult : null;
                if (r == null) return null;
                return r.PlayerVictory;
            }
            catch { return null; }
        }

        internal static void AfterBattle()
        {
            _agent = null;
            if (_kept + _lost == 0) { Array.Clear(_side, 0, _side.Length); return; }
            Log.Info("AmmoRecovery: po bitwie kolczanow/stosow wrocilo " + _kept + ", przepadlo " + _lost + " (wystrzelone, nieodzyskane).");
            // E8/A: to samo wedlug stron (suma trzech pozycji = linia wyzej); "stara regula" = agenci zdjeci w trakcie misji (wynik nieznany) i misje bez rozstrzygniecia
            try
            {
                var s = Settings.Current;
                Log.Info("AmmoRecovery: po bitwie - strona gracza odzysk " + _side[0] + " z " + (_side[0] + _side[1])
                         + ", przeciwnik " + _side[2] + " z " + (_side[2] + _side[3])
                         + ", bez rozstrzygniecia (w trakcie misji - stara regula) " + _side[4] + " z " + (_side[4] + _side[5])
                         + " | odzysk wedlug strony (E8/A) [" + (s != null && s.AmmoRecoveryBySide ? "wl" : "wyl") + "], latka "
                         + (_sideWired ? "wpieta" : "NIE WPIETA") + ", potkniecia (sesja) " + _sideStumbles + ".");
            }
            catch { }
            _kept = _lost = 0;
            Array.Clear(_side, 0, _side.Length);
        }

        /// <summary>E8/A: Global.ProcessAgentEquipment(Agent, Action&lt;EquipmentIndex, ItemObject&gt;, Func&lt;...&gt;) - jedyna z trzech wersji, ktora przechodzi
        /// po sztukach i wola IsAmmoAndEmpty (dwie pozostale ja wolaja).</summary>
        private static System.Reflection.MethodInfo FindProcessAgentEquipment(Type t)
        {
            if (t == null) return null;
            foreach (var mi in AccessTools.GetDeclaredMethods(t))
            {
                if (mi.Name != "ProcessAgentEquipment") continue;
                var ps = mi.GetParameters();
                if (ps.Length == 3 && ps[0].ParameterType == typeof(Agent) && ps[1].ParameterType == typeof(Action<EquipmentIndex, ItemObject>)) return mi;
            }
            return null;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.Global");
                var m = t != null ? AccessTools.Method(t, "IsAmmoAndEmpty", new[] { typeof(MissionWeapon?) }) : null;
                if (m != null) h.Patch(m, postfix: new HarmonyMethod(typeof(AmmoRecovery), nameof(Postfix)) { priority = Priority.Last });
                Log.Info("AmmoRecovery: zuzycie amunicji wedle historii " + (m != null ? "wpiete" : "BRAK DTE Global.IsAmmoAndEmpty") + ".");
                // E8/A (250): biezacy agent dla odzysku wedlug strony - latka wpieta zawsze (klucz czytany w Postfix), bez niej Postfix liczy po staremu
                try
                {
                    var pae = m != null ? FindProcessAgentEquipment(t) : null;
                    if (pae != null)
                    {
                        h.Patch(pae, prefix: new HarmonyMethod(typeof(AmmoRecovery), nameof(AgentPrefix)),
                                finalizer: new HarmonyMethod(typeof(AmmoRecovery), nameof(AgentFinalizer)));
                        _sideWired = true;
                    }
                }
                catch (Exception e) { Log.Error("AmmoRecovery.ApplyAll (E8/A)", e); }
                var s = Settings.Current;
                Log.Info("Misja gracza (E8/A): start - odzysk amunicji wedlug strony agenta [" + (s != null && s.AmmoRecoveryBySide ? "wl" : "wyl")
                         + "], latki wpiete " + (_sideWired ? 1 : 0) + "/1 (DTE Global.ProcessAgentEquipment - biezacy agent)"
                         + (_sideWired ? "" : (m == null ? " | nie znaleziono: DTE Global.IsAmmoAndEmpty" : " | nie znaleziono: DTE Global.ProcessAgentEquipment(Agent, Action<EquipmentIndex, ItemObject>, Func)"))
                         + " | arena nie obija [" + (s != null && s.ArenaNoWear ? "wl" : "wyl") + "] (bez latki - FieldCraft w misji).");
            }
            catch (Exception e) { Log.Error("AmmoRecovery.ApplyAll", e); }
        }
    }
}
