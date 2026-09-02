using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// AUDYT I WYROWNANIE JEDNOSTEK (Jeff 29.08: "przejrzyj jednostki, sprawdz
    /// czy maja umiejetnosci do noszonego pancerza, koni i broni - aby na swoim
    /// tierze mogly niesc swoj sprzet"). ROT daje elicie zbroje difficulty
    /// 140-200 przy atletyce 60 - zasada nadrzedna by ja rozebrala.
    /// Przy kazdym wczytaniu: dla kazdej jednostki liczymy NAJWYZSZY wymog
    /// kazdego skilla w jej wlasnym szablonie (bron wg klasy, kon wg Riding,
    /// pancerz wg Athletics) i PODNOSIMY skill do wymogu (nigdy w dol).
    /// Szablon to zamierzony wyglad jednostki - skille maja go udzwignac.
    /// Raport najwiekszych deficytow idzie do logu.
    /// </summary>
    internal static class TroopFit
    {
        internal static void Run()
        {
            try
            {
                if (!Settings.Current.TroopSkillAutoFit) { Log.Info("TroopFit: wylaczone."); return; }
                var all = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();
                if (all == null) return;
                var fSkills = HarmonyLib.AccessTools.Field(typeof(BasicCharacterObject), "DefaultCharacterSkills");
                if (fSkills == null) { Log.Info("TroopFit: brak pola DefaultCharacterSkills - odpuszczam."); return; }

                int troopsFixed = 0, skillsRaised = 0;
                var worst = new List<KeyValuePair<string, int>>();

                foreach (var ch in all)
                {
                    if (ch == null || ch.IsHero) continue;
                    var container = fSkills.GetValue(ch) as MBCharacterSkills;
                    if (container == null || container.Skills == null) continue;

                    // najwyzszy wymog kazdego skilla w szablonie tej jednostki
                    var need = new Dictionary<SkillObject, int>();
                    foreach (var eq in ch.BattleEquipments)
                    {
                        if (eq == null) continue;
                        for (int slot = 0; slot < 12; slot++)
                        {
                            var it = eq[(EquipmentIndex)slot].Item;
                            if (it == null || it.Difficulty <= 0) continue;
                            var skill = ItemReq.SkillFor(it);   // pancerz -> Atletyka, strzaly -> Bow, belty -> Crossbow
                            if (skill == null) continue;
                            int cur;
                            if (!need.TryGetValue(skill, out cur) || it.Difficulty > cur)
                                need[skill] = it.Difficulty;
                        }
                    }
                    if (need.Count == 0) continue;

                    bool touched = false;
                    foreach (var kv in need)
                    {
                        int have = ch.GetSkillValue(kv.Key);
                        if (have >= kv.Value) continue;
                        container.Skills.SetPropertyValue(kv.Key, kv.Value);
                        skillsRaised++; touched = true;
                        if (worst.Count < 40)
                            worst.Add(new KeyValuePair<string, int>(
                                ch.StringId + ": " + kv.Key.Name + " " + have + " -> " + kv.Value, kv.Value - have));
                    }
                    if (touched) troopsFixed++;
                }

                if (troopsFixed > 0)
                {
                    worst.Sort((a, b) => b.Value.CompareTo(a.Value));
                    Log.Info("TroopFit: " + troopsFixed + " jednostek wyrownanych, " + skillsRaised
                             + " skilli podniesionych do wymogow wlasnego szablonu.");
                    int shown = 0;
                    foreach (var w in worst)
                    {
                        if (shown++ >= 15) break;
                        Log.Info("TroopFit:   " + w.Key + " (deficyt " + w.Value + ")");
                    }
                }
                else Log.Info("TroopFit: wszystkie jednostki udzwigna swoj szablon - bez zmian.");
            }
            catch (Exception e) { Log.Error("TroopFit.Run", e); }
        }
    }
}
