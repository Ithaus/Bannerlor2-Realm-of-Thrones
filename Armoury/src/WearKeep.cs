using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Roster;

namespace Armoury
{
    /// <summary>
    /// ZUZYCIE NIE ZNIKA W BITWIE (Jeff 04.10: "napraw system ... ma byc historycznie"; audyt: DTE
    /// ArmyArmory.AssignEquipment zdejmuje z magazynu PIERWSZY egzemplarz o tym StringId bez wzgledu na stan,
    /// a po bitwie oddaje przez AddItemToArmory(ItemObject) - BEZ modyfikatora. Zuzyta kolczuga wracala nowa).
    ///
    /// Teraz: przy wydaniu na bitwe zapamietujemy stan (modyfikator) kazdej zdjetej sztuki; przy zwrocie
    /// (AddItemToArmory) ta sama liczba sztuk wraca z zapamietanymi stanami. Nowe sztuki (lup, rekruci)
    /// ponad to, co wyszlo na bitwe, wchodza jak dotad. Potem nasze WearTheTroops (po wygranej) doklada
    /// zuzycie walki - uszkodzenia sie kumuluja i trzeba je naprawiac.
    /// </summary>
    internal static class WearKeep
    {
        private static readonly Dictionary<string, List<ItemModifier>> _out = new Dictionary<string, List<ItemModifier>>();
        private static System.Reflection.FieldInfo _armoryF;
        private static System.Reflection.PropertyInfo _armoryP;
        private static int _kept;

        internal static void Reset() { _out.Clear(); _kept = 0; }

        private static ItemRoster Armory()
        {
            try
            {
                if (_armoryF != null) return _armoryF.GetValue(null) as ItemRoster;
                if (_armoryP != null) return _armoryP.GetValue(null, null) as ItemRoster;
            }
            catch { }
            return null;
        }

        private static Dictionary<EquipmentElement, int> Snapshot(ItemRoster r)
        {
            var d = new Dictionary<EquipmentElement, int>();
            if (r == null) return d;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                int v; d.TryGetValue(el.EquipmentElement, out v); d[el.EquipmentElement] = v + el.Amount;
            }
            return d;
        }

        public static void AssignPrefix(out Dictionary<EquipmentElement, int> __state)
        {
            __state = null;
            try { var s = Settings.Current; if (s != null && s.KeepWearThroughBattle) __state = Snapshot(Armory()); } catch { }
        }

        public static void AssignPostfix(Dictionary<EquipmentElement, int> __state)
        {
            if (__state == null) return;
            try
            {
                var now = Snapshot(Armory());
                foreach (var kv in __state)
                {
                    int after; now.TryGetValue(kv.Key, out after);
                    int gone = kv.Value - after;
                    if (gone <= 0) continue;
                    string id = kv.Key.Item.StringId;
                    List<ItemModifier> l;
                    if (!_out.TryGetValue(id, out l)) { l = new List<ItemModifier>(); _out[id] = l; }
                    for (int k = 0; k < gone; k++) l.Add(kv.Key.ItemModifier);      // null = sztuka w pelni sprawna
                }
            }
            catch { }
        }

        public static bool AddPrefix(ItemObject __0, int __1)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.KeepWearThroughBattle || __0 == null || __1 <= 0) return true;
                List<ItemModifier> l;
                if (!_out.TryGetValue(__0.StringId, out l) || l.Count == 0) return true;
                var armory = Armory();
                if (armory == null) return true;
                int n = __1;
                while (n > 0 && l.Count > 0)
                {
                    var m = l[l.Count - 1];
                    l.RemoveAt(l.Count - 1);
                    armory.AddToCounts(new EquipmentElement(__0, m), 1);
                    if (m != null) _kept++;
                    n--;
                }
                if (n > 0) armory.AddToCounts(__0, n);          // reszta (np. lup tej samej sztuki) - jak dotad
                return false;
            }
            catch { return true; }
        }

        /// <summary>Po bitwie: sztuki, ktore nie wrocily (zniszczone, stracone), nie czekaja na kolejna bitwe.</summary>
        internal static void AfterBattle()
        {
            if (_out.Count == 0 && _kept == 0) return;
            if (_kept > 0) Log.Info("WearKeep: " + _kept + " zuzytych sztuk wrocilo z bitwy ze swoim stanem (nie jako nowe).");
            _out.Clear(); _kept = 0;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmory");
                if (t == null) { Log.Info("WearKeep: brak DTE ArmyArmory."); return; }
                _armoryF = AccessTools.Field(t, "Armory");
                if (_armoryF == null) _armoryP = AccessTools.Property(t, "Armory");
                var assign = AccessTools.Method(t, "AssignEquipment", new[] { typeof(Equipment) });
                var add = AccessTools.Method(t, "AddItemToArmory", new[] { typeof(ItemObject), typeof(int) });
                if (assign != null) h.Patch(assign, prefix: new HarmonyMethod(typeof(WearKeep), nameof(AssignPrefix)), postfix: new HarmonyMethod(typeof(WearKeep), nameof(AssignPostfix)));
                if (add != null) h.Patch(add, prefix: new HarmonyMethod(typeof(WearKeep), nameof(AddPrefix)));
                Log.Info("WearKeep: stan sprzetu przez bitwe - wydanie " + (assign != null ? "wpiete" : "BRAK") + ", zwrot " + (add != null ? "wpiety" : "BRAK")
                         + ", magazyn " + (_armoryF != null || _armoryP != null ? "znaleziony" : "BRAK") + ".");
            }
            catch (Exception e) { Log.Error("WearKeep.ApplyAll", e); }
        }
    }
}
