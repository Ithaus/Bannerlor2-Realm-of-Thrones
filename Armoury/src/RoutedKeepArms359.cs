using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 359b (08.11, decyzja Jeffa "Obie rzeczy": w bitwie polowej uciekajacy NIE wyrzuca broni - osobny klucz RoutedMenKeepArms; kto juz wyrzucil, moze podniesc bron
    /// lezaca do 20 m - RallyRouted359.cs). Dowod (log 07.11 23:29 "w ucieczce z bronia 0, z tarcza 0 z 368"; dekompilacja ArtemsLivelyAnimations,
    /// scratchpad/decout/full/ala.cs:433-460): ArtemsLivelyAnimationsMissionLogic.OnAgentPanicked przy FleeAnims wola statyczne DropAllWeapons(agent) - przy ustawieniu
    /// Artema DropAllWeapons (domyslnie TAK) zrzuca wszystkie sloty broni 0-3. Bez broni zawrocony z paniki (359) nie ma czym walczyc.
    /// Latka: prefiks Harmony na DropAllWeapons (AccessTools.DeclaredMethod na klasie deklarujacej - pulapka 15.10), zakladana przy pierwszej bitwie (NightHooks.FirstBattle -
    /// typy Artema juz zaladowane), raz na proces. Pomija zrzut, gdy: klucz RoutedMenKeepArms, RoutedMenNeverRally i BrokenMenNeverRally TAK, misja w zakresie 142 (bitwa
    /// polowa bez kryjowki i morza - RoutKeeper.FieldAnyThread). Animacja strachu Artema zostaje. Oblezenia, wypady, kryjowki, morze, turnieje - jak dotad.
    /// Prefiks moze przyjsc z dowolnego watku (OnAgentPanicked w petli zachowan misji) - tylko odczyty pol i Interlocked. Wyjatek = zrzut jak u Artema.
    /// Linia startu (Latki nocy (K3)) mowi, czy latka weszla (Harmony.GetPatchInfo); licznik pominietych zrzutow w linii "ZBIORKA (359)".
    /// </summary>
    internal static class RoutedKeepArms359
    {
        private static MethodInfo _target;
        internal static bool Patched;

        /// <summary>NightHooks.FirstBattle (glowny watek, raz na proces).</summary>
        internal static string ApplyMissionPatches(Harmony h)
        {
            if (h == null) return "brak Harmony";
            var t = QuartermasterLaw.FindType("ArtemsLivelyAnimations.ArtemsLivelyAnimationsMissionLogic");
            if (t == null) return "Artem nieobecny - latka niepotrzebna";
            _target = AccessTools.DeclaredMethod(t, "DropAllWeapons", new[] { typeof(Agent) });
            if (_target == null || !_target.IsStatic || _target.ReturnType != typeof(void)) return "sygnatura DropAllWeapons(Agent) nie pasuje - latka NIE wpieta";
            h.Patch(_target, prefix: new HarmonyMethod(typeof(RoutedKeepArms359), nameof(DropPrefix)));
            Patched = Owned(_target, h.Id);
            return Patched ? "Artem DropAllWeapons 1/1 (klucz " + (Settings.Current != null && Settings.Current.RoutedMenKeepArms ? "TAK" : "NIE") + ")" : "Artem DropAllWeapons 0/1 - latka NIE weszla";
        }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                foreach (var p in info.Prefixes) if (p != null && p.owner == id) return true;
            }
            catch { }
            return false;
        }

        /// <summary>false = zrzut broni pominiety (uciekajacy w bitwie polowej z zakresu 142).</summary>
        private static bool DropPrefix(Agent agent)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.RoutedMenKeepArms || !c.RoutedMenNeverRally || !c.BrokenMenNeverRally) return true;
                if (agent == null || !agent.IsHuman || agent.IsMainAgent) return true;
                var k = RoutHold.Live;
                if (k == null || !k.FieldAnyThread(agent.Mission)) return true;
                System.Threading.Interlocked.Increment(ref Rally359.ArmsKept);
                return false;
            }
            catch { return true; }
        }
    }
}
