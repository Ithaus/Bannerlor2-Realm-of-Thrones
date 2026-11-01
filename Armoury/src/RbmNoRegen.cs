using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 314a (01.11, Jeff: "zdrowia sie nie odzyskuje w walce, a zauwazylem ze jakis blad jest ze sie wojsko regeneruje hp podczas bitwy").
    /// Zrodlo (dekompilacja RBMAI.dll, scratchpad/rbmdec/RBMAI.cs:10263): RBMAI.StanceLogic.OnMissionTick co timeToCalcStaminaHealth (10 s) kazdemu
    /// agentowi ze stamina > 85% robi Health = min(HealthLimit, Health + 0.9) - setter silnika zaokragla w gore, wiec +1 HP co 10 s calej bitwie
    /// (zolnierze, lordowie, gracz, konie RBM nie licza - tylko agenci ze stanem postawy). Nasz kod (FieldCraft, Bleed313) HP tylko odejmuje.
    /// Transpiler: jedyne wywolanie Agent.set_Health w tej metodzie idzie przez Gate - zapis WYZSZEGO HP odrzucony (klucz RbmBattleRegenOff), nizszy
    /// / rowny przechodzi. Inne leczenia (mak ROT PoppyMissionVM, smok ROT, Krwawe Gody) nietkniete - to inne metody. Zly ksztalt IL (dopasowan != 1)
    /// - latka zdjeta tym samym MethodInfo, RBM jak dotad. Linia "Bez leczenia w bitwie (314a)" przy wpieciu i raz na koniec misji (FieldCraft).
    /// </summary>
    internal static class RbmNoRegen
    {
        private const string P = "Bez leczenia w bitwie (314a): ";
        private static MethodInfo _target, _tr;
        private static string _owner;
        private static bool _ok;
        private static int _match;
        private static Mission _mis;
        private static int _blocked, _passed, _stumbles;
        private static readonly HashSet<int> _agents = new HashSet<int>();

        internal static void ApplyAll(Harmony h)
        {
            string why = "nie sprawdzane";
            bool patched = false;
            try
            {
                var t = QuartermasterLaw.FindType("RBMAI.StanceLogic");
                _target = t != null ? AccessTools.DeclaredMethod(t, "OnMissionTick") : null;
                _tr = typeof(RbmNoRegen).GetMethod(nameof(Transpiler), BindingFlags.NonPublic | BindingFlags.Static);
                if (t == null) why = "RBM nieobecny";
                else if (_target == null) why = "brak StanceLogic.OnMissionTick";
                else
                {
                    _owner = h.Id;
                    h.Patch(_target, transpiler: new HarmonyMethod(_tr));
                    patched = true;
                    _ok = _match == 1 && Has(_target, h.Id, _tr);
                    why = "dopasowan set_Health " + _match + "/1";
                    if (!_ok)
                    {
                        try { h.Unpatch(_target, _tr); } catch (Exception e) { Log.Error("RbmNoRegen (zdjecie)", e); }
                        why += " - zdjete, RBM jak dotad";
                    }
                }
            }
            catch (Exception e)
            {
                why = "wyjatek " + e.GetType().Name;
                Log.Error("RbmNoRegen.ApplyAll", e);
                if (patched) { try { h.Unpatch(_target, _tr); } catch { } }
            }
            var c = Settings.Current;
            Log.Info(P + "latka RBMAI.StanceLogic.OnMissionTick " + (_ok ? "TAK" : "NIE") + " (" + why + ") | klucz No Healing In Battle "
                     + (c != null && c.RbmBattleRegenOff ? "TAK" : "NIE (RBM leczy jak dotad)")
                     + " | RBM: +1 HP co 10 s przy staminie > 85% - odrzucane; mak ROT i inne leczenia bez zmian");
        }

        private static bool Has(MethodBase target, string owner, MethodInfo patch)
        {
            var pi = Harmony.GetPatchInfo(target);
            if (pi == null || pi.Transpilers == null) return false;
            foreach (var p in pi.Transpilers) if (p != null && p.owner == owner && p.PatchMethod == patch) return true;
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var setter = AccessTools.PropertySetter(typeof(Agent), nameof(Agent.Health));
            var gate = typeof(RbmNoRegen).GetMethod(nameof(Gate), BindingFlags.NonPublic | BindingFlags.Static);
            _match = 0;
            foreach (var ci in instructions)
            {
                if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand is MethodInfo mi && mi == setter)
                {
                    _match++;
                    var n = new CodeInstruction(OpCodes.Call, gate);
                    n.labels.AddRange(ci.labels);
                    n.blocks.AddRange(ci.blocks);
                    yield return n;
                }
                else yield return ci;
            }
        }

        /// <summary>Zamiast agent.Health = v w petli RBM: wyzsze HP odrzucone (klucz), nizsze / rowne zapisane jak w RBM.</summary>
        private static void Gate(Agent a, float v)
        {
            try
            {
                var m = Mission.Current;
                if (!ReferenceEquals(m, _mis)) { _mis = m; _blocked = _passed = 0; _agents.Clear(); }
                var c = Settings.Current;
                if (c != null && c.RbmBattleRegenOff && v > a.Health)
                {
                    _blocked++;
                    _agents.Add(a.Index);
                    return;
                }
                _passed++;
            }
            catch (Exception e)
            {
                _stumbles++;
                if (_stumbles <= 3) Log.Error("RbmNoRegen.Gate (potkniecie " + _stumbles + ")", e);
            }
            a.Health = v;
        }

        /// <summary>Raz na koniec misji (FieldCraft.OnEndMission): ile podniesien HP RBM odrzucono. Misja bez prob - nic.</summary>
        internal static void Flush(Mission m)
        {
            try
            {
                if (!ReferenceEquals(m, _mis)) return;
                if (_blocked + _passed > 0)
                {
                    var c = Settings.Current;
                    Log.Info(P + "misja | latka " + (_ok ? "TAK" : "NIE") + ", klucz " + (c != null && c.RbmBattleRegenOff ? "TAK" : "NIE")
                             + " | podniesien HP RBM odrzucone " + _blocked + " (agentow " + _agents.Count + "), przepuszczone " + _passed
                             + " | potkniecia " + _stumbles);
                }
                _mis = null; _blocked = _passed = 0; _agents.Clear();
            }
            catch (Exception e) { Log.Error("RbmNoRegen.Flush", e); }
        }
    }
}
