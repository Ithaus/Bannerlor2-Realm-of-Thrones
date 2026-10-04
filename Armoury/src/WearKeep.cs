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

        internal static void Reset() { _out.Clear(); _kept = 0; _cleanIn.Clear(); _calls = 0; }

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
                    if (kv.Key.ItemModifier == null) continue;                  // sprawna sztuka - nic do pamietania
                    int after; now.TryGetValue(kv.Key, out after);
                    int gone = kv.Value - after;
                    if (gone <= 0) continue;
                    string id = kv.Key.Item.StringId;
                    List<ItemModifier> l;
                    if (!_out.TryGetValue(id, out l)) { l = new List<ItemModifier>(); _out[id] = l; }
                    for (int k = 0; k < gone; k++) l.Add(kv.Key.ItemModifier);
                }
            }
            catch { }
        }

        // audyt pelny W2/W5: NIE przechwytujemy zwrotow (DTE oddaje przez te sama metode lup, zbrojownie
        // pokonanych, rekrutow) - tylko liczymy, ile sztuk "czystych" wrocilo w tej bitwie.
        private static readonly Dictionary<string, int> _cleanIn = new Dictionary<string, int>();
        private static int _calls;
        public static void AddPostfix(ItemObject __0, int __1)
        {
            try
            {
                if (__0 == null || __1 <= 0 || _out.Count == 0) return;
                _calls++;
                int v; _cleanIn.TryGetValue(__0.StringId, out v); _cleanIn[__0.StringId] = v + __1;
            }
            catch { }
        }

        /// <summary>Po zwrotach DTE (FinalizeMission): tyle czystych sztuk, ile zuzytych wyszlo na bitwe (i nie wiecej
        /// niz wrocilo), dostaje z powrotem swoj stan. Bilans zamiast zgadywania, ktora sztuka byla ktora.</summary>
        public static void FinalizePostfix()
        {
            try
            {
                var armory = Armory();
                if (armory != null)
                    foreach (var kv in _out)
                    {
                        int back; _cleanIn.TryGetValue(kv.Key, out back);
                        int n = Math.Min(kv.Value.Count, back);
                        if (n <= 0) continue;
                        ItemObject item = null; int clean = 0;
                        for (int i = 0; i < armory.Count; i++)
                        {
                            var el = armory.GetElementCopyAtIndex(i);
                            if (el.EquipmentElement.Item != null && el.EquipmentElement.Item.StringId == kv.Key && el.EquipmentElement.ItemModifier == null) { item = el.EquipmentElement.Item; clean += el.Amount; }
                        }
                        n = Math.Min(n, clean);
                        for (int k = 0; k < n; k++)
                        {
                            armory.AddToCounts(new EquipmentElement(item), -1);
                            armory.AddToCounts(new EquipmentElement(item, kv.Value[k]), 1);
                            _kept++;
                        }
                    }
                Log.Info("WearKeep: bitwa zakonczona - zwrotow do magazynu " + _calls + ", zuzytych sztuk z powrotem w swoim stanie " + _kept + ".");
            }
            catch (Exception e) { Log.Error("WearKeep.Finalize", e); }
            _out.Clear(); _cleanIn.Clear(); _kept = 0; _calls = 0;
        }

        /// <summary>Zapas: po bitwie gracza czyscimy pamiec, gdyby FinalizeMission nie zadzialal.</summary>
        internal static void AfterBattle() { _out.Clear(); _cleanIn.Clear(); _kept = 0; _calls = 0; }

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
                if (add != null) h.Patch(add, postfix: new HarmonyMethod(typeof(WearKeep), nameof(AddPostfix)));
                var mlT = AccessTools.TypeByName("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var fin = mlT != null ? AccessTools.Method(mlT, "FinalizeMission") : null;
                if (fin != null) h.Patch(fin, postfix: new HarmonyMethod(typeof(WearKeep), nameof(FinalizePostfix)));
                Log.Info("WearKeep: stan sprzetu przez bitwe - wydanie " + (assign != null ? "wpiete" : "BRAK") + ", zwrot " + (add != null ? "wpiety" : "BRAK")
                         + ", magazyn " + (_armoryF != null || _armoryP != null ? "znaleziony" : "BRAK") + ".");
            }
            catch (Exception e) { Log.Error("WearKeep.ApplyAll", e); }
        }
    }
}
