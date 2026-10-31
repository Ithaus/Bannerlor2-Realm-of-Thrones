using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CrashScribe
{
    /// <summary>
    /// 305c (31.10, diagnoza BITWY: "ROT-Dragon: AmbiguousMatchException w GetAllShipPositions w bitwach ladowych"). Dowod: CrashScribe
    /// session-2026-10-30_21-04-44.log - stos GetAllShipPositions &lt;- AIDragon.RefreshAllCachedTargets, SUMMARY 98 x przypisane do ROT.Mission...
    /// i koszyk "(inne)" 3790 x z ta sama sygnatura GetMethodImpl (nie przypisane - po tescie porownac spadek koszyka).
    ///
    /// Dekompilacja ROT.dll (ROT-Core, ROT 8.1.8) ROT.MissionBehaviors.ROTDragonMissionBehavior.GetAllShipPositions (static, List&lt;Vec3&gt;):
    ///  1. NavalShipsLogic = Mission.Current?.GetMissionBehavior&lt;NavalShipsLogic&gt;(); jest - pozycje statkow, return (bitwa morska - bez zmian);
    ///  2. bez niej: typ ShipBurningSystem (raz na proces) - brak typu albo Mission.Current / Scene null - pusta lista;
    ///  3. inaczej Scene.GetEntities przez refleksje na CALEJ scenie (spis encji), potem typeof(GameEntity).GetMethod("HasScriptOfType", Instance|Public)
    ///     - GameEntity 1.4.8 ma 2 przeciazenia (HasScriptOfType&lt;T&gt;() i HasScriptOfType(Type)) - AmbiguousMatchException ZAWSZE, zanim cokolwiek
    ///     trafi do listy; catch ROT (Debug.Print) - pusta lista.
    /// Czyli bez NavalShipsLogic wynik jest zawsze pusta lista. Prefiks (klucz Config.RotDragonShipsOnLand): Mission.Current == null albo bez zachowania
    /// typu NavalShipsLogic (ten sam test co ROT: IsInstanceOfType, jak GetMissionBehavior&lt;T&gt;) - __result = nowa pusta lista, return false. Z NavalShipsLogic -
    /// oryginal (licznik). Wpiecie leniwe w pierwszej misji (SubModuleMain.OnMissionBehaviorInitialize - konstruktor statyczny klasy ROT tworzy
    /// ActionIndexCache, w menu glownym go nie wywolujemy), DeclaredMethod na klasie deklarujacej. Linia "Mends: smoki ROT na ladzie (305c)" przy wpieciu
    /// i raz na misje z wywolaniami (Logic.OnEndMission).
    /// </summary>
    internal static class DragonShips
    {
        private static bool _tried, _ok;
        private static Type _tNaval;
        private static int _skipped, _orig, _err, _skippedAll, _origAll;   // Interlocked (wolajacy ROT - zwykle glowny watek)
        private static string _missionKind = "-";

        /// <summary>Raz na proces, w pierwszej misji (glowny watek).</summary>
        internal static void EnsureInstalled(Harmony h)
        {
            if (_tried) return;
            _tried = true;
            if (!Config.RotDragonShipsOnLand) { Scribe.Line("Mends: smoki ROT na ladzie (305c) WYLACZONE (RotDragonShipsOnLand=false) - GetAllShipPositions jak w ROT."); return; }
            if (h == null) { Scribe.Line("Mends: smoki ROT na ladzie (305c) NIE wpiete - brak instancji Harmony."); return; }
            try
            {
                var t = AccessTools.TypeByName("ROT.MissionBehaviors.ROTDragonMissionBehavior");
                _tNaval = AccessTools.TypeByName("NavalDLC.Missions.MissionLogics.NavalShipsLogic");
                var target = t != null ? AccessTools.DeclaredMethod(t, "GetAllShipPositions", Type.EmptyTypes) : null;
                if (target == null || !target.IsStatic || target.ReturnType != typeof(List<Vec3>) || _tNaval == null)
                {
                    Scribe.Line("Mends: smoki ROT na ladzie (305c) NIE wpiete - " + (t == null ? "brak typu ROTDragonMissionBehavior" : target == null ? "brak GetAllShipPositions()"
                                : _tNaval == null ? "brak typu NavalShipsLogic" : "inny ksztalt metody (" + target.ReturnType.Name + ")") + "; ROT bez zmian.");
                    return;
                }
                h.Patch(target, prefix: new HarmonyMethod(typeof(DragonShips), nameof(Prefix)));
                var info = Harmony.GetPatchInfo(target);
                bool own = false;
                if (info != null) foreach (var p in info.Prefixes) if (p != null && p.owner == h.Id) own = true;
                _ok = own;
                Scribe.Line("Mends: smoki ROT na ladzie (305c) - prefiks ROTDragonMissionBehavior.GetAllShipPositions " + (own ? "wpiety 1/1" : "NIE wpiety 0/1")
                            + " (bez NavalShipsLogic pusta lista od razu, jak oryginal, bez spisu encji sceny i AmbiguousMatchException ROT).");
            }
            catch (Exception e) { Scribe.Line("Mends: smoki ROT na ladzie (305c) NIE wpiete - " + e.GetType().Name + ": " + e.Message); }
        }

        /// <summary>Prefiks: bez NavalShipsLogic (ten sam test co ROT) - pusta lista bez oryginalu.</summary>
        public static bool Prefix(ref List<Vec3> __result)
        {
            try
            {
                var m = Mission.Current;
                if (m != null && HasNaval(m)) { Interlocked.Increment(ref _orig); return true; }
                __result = new List<Vec3>();
                Interlocked.Increment(ref _skipped);
                return false;
            }
            catch { Interlocked.Increment(ref _err); return true; }
        }

        private static bool HasNaval(Mission m)
        {
            var tn = _tNaval;
            if (tn == null) return true;   // ostroznie: bez typu - oryginal
            var list = m.MissionBehaviors;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b != null && tn.IsInstanceOfType(b)) return true;
            }
            return false;
        }

        /// <summary>Linia misji (raz, gdy smok pytal o statki).</summary>
        private static void Flush(string how)
        {
            int s = Interlocked.Exchange(ref _skipped, 0), o = Interlocked.Exchange(ref _orig, 0), e = Interlocked.Exchange(ref _err, 0);
            if (s + o + e == 0) return;
            _skippedAll += s; _origAll += o;
            Scribe.Line("Mends: smoki ROT na ladzie (305c): misja " + _missionKind + " - GetAllShipPositions bez NavalShipsLogic " + s
                        + " razy (pusta lista bez refleksji), z NavalShipsLogic (oryginal ROT) " + o + ", bledy prefiksu " + e
                        + " | sesja: pominiete " + _skippedAll + ", oryginal " + _origAll + " | koniec: " + how + ".");
        }

        /// <summary>Zachowanie misji (SubModuleMain.OnMissionBehaviorInitialize, gdy prefiks wpiety) - linia na koniec misji.</summary>
        internal sealed class Logic : MissionLogic
        {
            private bool _done;

            public override void AfterStart()
            {
                base.AfterStart();
                try
                {
                    var m = Mission;
                    _missionKind = m == null ? "?" : m.IsNavalBattle ? "morze" : m.IsSiegeBattle ? "oblezenie" : m.IsFieldBattle ? "pole" : "inna";
                }
                catch { _missionKind = "?"; }
                Interlocked.Exchange(ref _skipped, 0); Interlocked.Exchange(ref _orig, 0); Interlocked.Exchange(ref _err, 0);
            }

            protected override void OnEndMission() { if (!_done) { _done = true; try { Flush("koniec misji"); } catch { } } base.OnEndMission(); }

            public override void OnRemoveBehavior() { if (!_done) { _done = true; try { Flush("zdjete zachowanie"); } catch { } } base.OnRemoveBehavior(); }
        }

        internal static bool Installed { get { return _ok; } }
    }
}
