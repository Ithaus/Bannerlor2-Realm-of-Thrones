using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 515 (perk 100 audytu perkow 14.11; Jeff 14.11 "78-105 ok" - NavalDLC wedlug propozycji): NavalDLC po rozstawieniu w bitwie morskiej.
    ///
    /// DOWOD (dekompilacja): Mission.OnAfterDeploymentFinished (Mission.cs:6765) i dolaczenie agenta po rozstawieniu (Mission.cs:4378) woluja na CZYNNYM
    /// modelu MissionGameModels.Current.AgentStatCalculateModel dwie metody: InitializeAgentStatsAfterDeploymentFinished i
    /// InitializeMissionEquipmentAfterDeploymentFinished. W klasie bazowej AgentStatCalculateModel to puste metody wirtualne. NavalDLC
    /// (NavalAgentStatCalculateModel.cs:30-122) robi w nich: premie galionow (Hawk, Boar, Raven, SaberToothTiger, Oxen) i Shipmaster.Commodore
    /// (galion okretu flagowego dla calej armii), +obrazenia zalogi wedlug okretu, wytrzymalosc tarcz wedlug okretu, dodatkowe kolczany / bron miotana
    /// okretu, Boatswain.WellStocked (+amunicja strzelcow i miotaczy kapitana) i Boatswain.ShipwrightsInsight (druga premia: +amunicja). Czynne owijki
    /// nad NavalDLC - ArtemsLivelyAnimationsAgentStatCalculateModel i ROT RoTElephantAgentStatCalculateModel (oraz ROTAgentStatCalculateModel) - tych
    /// dwoch metod nie deklaruja, wiec gra wola pusta metode bazowa i NavalDLC nie biegnie wcale (wszystkie inne metody owijki przekazuja _previousModel).
    ///
    /// NAPRAWA: postfiks na obu pustych metodach klasy bazowej (AccessTools.DeclaredMethod na AgentStatCalculateModel). Dziala tylko, gdy obiekt to CZYNNY
    /// model misji (wywolanie gry trafilo w pusta metode bazowa - wiec nic pod nim nie biegnie), w bitwie morskiej (Mission.IsNavalBattle) i gdy w lancuchu
    /// czynnego modelu (pola typu AgentStatCalculateModel: _previousModel owijek, BaseModel gry) jest model NavalDLC (NavalAgentStatCalculateModel albo
    /// NavalCustomBattleAgentStatCalculateModel) - wtedy wolamy jego metode raz dla agenta. Jego wlasne wywolanie modelu bazowego (Sandbox) trafia znow
    /// w pusta metode bazowa - znacznik watku (_in) przepuszcza je bez nas. Gdy kiedys owijka zacznie przekazywac te metody, wywolanie gry nie trafi w metode
    /// bazowa czynnego modelu i nasz postfiks nic nie zrobi (bez podwojenia).
    /// Dwa klucze: NavalDeployStatsFix (galiony, Commodore, obrazenia zalogi) i NavalDeployAmmoFix (WellStocked, ShipwrightsInsight, kolczany i tarcze okretu -
    /// osobno, bo Jeff 14.11 o DeepQuivers: "dodatkowe strzaly to glupota"; NIE = amunicja jak dotad). Lancuch liczony raz na obiekt czynnego modelu.
    /// Pomiar: pierwsza bitwa morska misji pisze linie z lancuchem; liczniki (agenci, amunicja przed / po) w linii doby "NavalDLC po rozstawieniu (515)".
    /// Bledy liczone (potkniecia) - wyjatek = agent bez premii NavalDLC (jak dotad), bez globalnego wylacznika. Nic co klatke: dwa wywolania na agenta na bitwe.
    /// </summary>
    internal static class NavalDeploy515
    {
        internal const string P = "NavalDLC po rozstawieniu (515): ";
        private const string Owner = "com.jeff.armoury.navaldeploy515";
        private static readonly string[] NavalNames = { "NavalDLC.GameComponents.NavalAgentStatCalculateModel", "NavalDLC.ComponentInterfaces.NavalCustomBattleAgentStatCalculateModel" };

        private static Harmony _h;
        private static string _hook = "nie wpieta";
        [ThreadStatic] private static bool _in;

        // pamiec lancucha (watek glowny misji)
        private static object _forActive;
        private static AgentStatCalculateModel _naval;
        private static string _walk = "-";
        private static object _loggedMission;

        // liczniki doby
        private static int _dStats, _dEquip, _dMissions, _dAmmoBefore, _dAmmoAfter, _dHpAdd, _stumbles, _dStumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            _forActive = null; _naval = null; _walk = "-"; _loggedMission = null;
            ResetDay();
            _stumbles = 0;
            _errSites.Clear();
        }

        private static void ResetDay() { _dStats = 0; _dEquip = 0; _dMissions = 0; _dAmmoBefore = 0; _dAmmoAfter = 0; _dHpAdd = 0; _dStumbles = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_errSites.Add(where) || _stumbles % 500 == 0) Log.Error("NavalDeploy515." + where + " (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>OnSessionLaunched: wpiecie raz na proces (klasa bazowa misji - bez modeli kampanii), linia startu.</summary>
        internal static void EnsureHooks()
        {
            try
            {
                if (_h == null)
                {
                    _h = new Harmony(Owner);
                    var t = typeof(AgentStatCalculateModel);
                    var mS = AccessTools.DeclaredMethod(t, "InitializeAgentStatsAfterDeploymentFinished", new[] { typeof(Agent) });
                    var mE = AccessTools.DeclaredMethod(t, "InitializeMissionEquipmentAfterDeploymentFinished", new[] { typeof(Agent) });
                    int n = 0;
                    if (mS != null) { _h.Patch(mS, postfix: new HarmonyMethod(typeof(NavalDeploy515), nameof(StatsPostfix))); n++; }
                    if (mE != null) { _h.Patch(mE, postfix: new HarmonyMethod(typeof(NavalDeploy515), nameof(EquipPostfix))); n++; }
                    _hook = n == 2 ? "wpiete 2/2" : "BRAK (" + n + "/2: " + (mS == null ? "brak InitializeAgentStatsAfterDeploymentFinished " : "") + (mE == null ? "brak InitializeMissionEquipmentAfterDeploymentFinished" : "") + ")";
                }
                bool navalDll = AccessTools.TypeByName(NavalNames[0]) != null;
                var s = Settings.Current;
                Log.Info(P + "start sesji | latki " + _hook + " (postfiks na pustych metodach AgentStatCalculateModel - tylko czynny model w bitwie morskiej)"
                         + " | NavalDLC " + (navalDll ? "jest" : "BRAK - nic do naprawy")
                         + " | klucze: statystyki (galiony, Commodore, zaloga) " + (s != null && s.NavalDeployStatsFix ? "TAK" : "NIE")
                         + ", amunicja i tarcze (WellStocked, ShipwrightsInsight, kolczany okretu) " + (s != null && s.NavalDeployAmmoFix ? "TAK" : "NIE")
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Stumble("EnsureHooks", e); }
        }

        /// <summary>Model NavalDLC w lancuchu czynnego modelu (pola typu AgentStatCalculateModel w calej hierarchii klasy - _previousModel owijek, BaseModel gry).</summary>
        private static AgentStatCalculateModel NavalUnder(AgentStatCalculateModel active)
        {
            if (ReferenceEquals(active, _forActive)) return _naval;
            _forActive = active; _naval = null;
            var sb = new StringBuilder();
            object cur = active;
            var seen = new List<object>();
            for (int i = 0; cur != null && i < 16; i++)
            {
                foreach (var o in seen) if (ReferenceEquals(o, cur)) { sb.Append(" (petla)"); cur = null; break; }
                if (cur == null) break;
                seen.Add(cur);
                var tn = cur.GetType().FullName;
                sb.Append(i > 0 ? " -> " : "").Append(cur.GetType().Name);
                if (i > 0 && Array.IndexOf(NavalNames, tn) >= 0) { _naval = cur as AgentStatCalculateModel; break; }
                object next = null;
                for (var k = cur.GetType(); k != null && k != typeof(object) && next == null; k = k.BaseType)
                    foreach (var fi in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (!typeof(AgentStatCalculateModel).IsAssignableFrom(fi.FieldType)) continue;
                        var v = fi.GetValue(cur);
                        if (v != null && !ReferenceEquals(v, cur)) { next = v; break; }
                    }
                cur = next;
            }
            _walk = sb.ToString();
            return _naval;
        }

        private static AgentStatCalculateModel Target(AgentStatCalculateModel inst, bool stats)
        {
            if (_in || inst == null) return null;
            var s = Settings.Current;
            if (s == null || (stats ? !s.NavalDeployStatsFix : !s.NavalDeployAmmoFix)) return null;
            var mi = Mission.Current;
            if (mi == null || !mi.IsNavalBattle) return null;
            var mg = MissionGameModels.Current;
            var active = mg != null ? mg.AgentStatCalculateModel : null;
            if (active == null || !ReferenceEquals(inst, active)) return null;   // tylko gdy wywolanie gry trafilo w metode bazowa czynnego modelu
            var nav = NavalUnder(active);
            if (!ReferenceEquals(_loggedMission, mi))
            {
                _loggedMission = mi; _dMissions++;
                Log.Info(P + "bitwa morska - czynny model " + active.GetType().FullName + " | lancuch: " + _walk + " | "
                         + (nav != null ? "NavalDLC pod owijkami nie dostawal tych dwoch wywolan - przekazujemy" : "brak modelu NavalDLC w lancuchu - bez zmian") + ".");
            }
            return nav;
        }

        private static int Ammo(Agent a)
        {
            int n = 0;
            var eq = a.Equipment;
            if (eq == null) return 0;
            for (int i = 0; i < 5; i++)
            {
                var w = eq[(EquipmentIndex)i];
                if (w.IsEmpty) continue;
                var u = w.CurrentUsageItem;
                if (u != null && u.IsConsumable) n += w.Amount;
            }
            return n;
        }

        public static void StatsPostfix(AgentStatCalculateModel __instance, Agent __0)
        {
            try
            {
                if (__0 == null) return;
                var nav = Target(__instance, true);
                if (nav == null) return;
                float hp = __0.HealthLimit;
                _in = true;
                try { nav.InitializeAgentStatsAfterDeploymentFinished(__0); }
                finally { _in = false; }
                _dStats++;
                if (__0.HealthLimit > hp + 0.01f) _dHpAdd++;   // Oxen
            }
            catch (Exception e) { Stumble("StatsPostfix", e); }
        }

        public static void EquipPostfix(AgentStatCalculateModel __instance, Agent __0)
        {
            try
            {
                if (__0 == null || !__0.IsHuman) return;
                var nav = Target(__instance, false);
                if (nav == null) return;
                int before = Ammo(__0);
                _in = true;
                try { nav.InitializeMissionEquipmentAfterDeploymentFinished(__0); }
                finally { _in = false; }
                _dEquip++;
                _dAmmoBefore += before;
                _dAmmoAfter += Ammo(__0);
            }
            catch (Exception e) { Stumble("EquipPostfix", e); }
        }

        internal static void Daily(int day)
        {
            try
            {
                if (_dMissions == 0 && _dStumbles == 0) { ResetDay(); return; }
                Log.Info(P + "dzien " + day + " | bitew morskich " + _dMissions + " | lancuch: " + _walk + " | przekazane: statystyki " + _dStats + " agentow (Oxen +HP "
                         + _dHpAdd + "), ekwipunek " + _dEquip + " agentow - amunicja " + _dAmmoBefore + " -> " + _dAmmoAfter + " szt. (WellStocked, ShipwrightsInsight, kolczany okretu)"
                         + " | potkniecia doby " + _dStumbles + " (od startu " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
