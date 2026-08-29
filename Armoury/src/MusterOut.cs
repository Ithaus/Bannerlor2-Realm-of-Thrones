using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZWALNIANY ZOSTAWIA RYNSZTUNEK (Jeff 29.08: "jak wyrzucam wojsko, to
    /// odchodza z bronia i pancerzem - bez sensu"). Sprzet nalezy do wojska,
    /// nie do czlowieka: przy zamknieciu ekranu partii porownujemy sklad
    /// przed/po; ubytek, ktory NIE jest awansem (UpgradeTargets), to zwolnieni -
    /// ich wyposazenie (wedle szablonu, ze stanem bojowym) wraca do magazynu
    /// kwatermistrza (DTE). Smoki i legendy nie wracaja (filtry stoja).
    /// </summary>
    internal static class MusterOut
    {
        private static Dictionary<CharacterObject, int> _before;

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var s = Settings.Current;
                if (!s.DismissedLeaveGear && !s.AutoSortParty) { Log.Info("MusterOut: wylaczone."); return; }
                var mInit = AccessTools.Method(typeof(PartyScreenLogic), "Initialize");
                var mDone = AccessTools.Method(typeof(PartyScreenLogic), "DoneLogic");
                if (mInit == null || mDone == null) { Log.Info("MusterOut: nie znalazlem PartyScreenLogic - patch spi."); return; }
                harmony.Patch(mInit, postfix: new HarmonyMethod(typeof(MusterOut), "AfterInit"));
                harmony.Patch(mDone, postfix: new HarmonyMethod(typeof(MusterOut), "AfterDone"));
                Log.Info("MusterOut: zwalniani zostawiaja sprzet=" + s.DismissedLeaveGear
                         + ", auto-sort partii=" + s.AutoSortParty + ".");
            }
            catch (Exception e) { Log.Error("MusterOut.ApplyAll", e); }
        }

        /// <summary>
        /// PORZADEK W SZEREGU (Jeff 29.08: "recznie musze ustawiac jednostki,
        /// chcialbym zeby segregowaly sie same: kawaleria, konni lucznicy,
        /// piechota, strzelcy - po tierze"). Rebuild rosteru w tej kolejnosci;
        /// XP stacka jedzie razem z nim, herosi zostaja na gorze jak zawsze.
        /// </summary>
        private static int ArmRank(CharacterObject ch)
        {
            try
            {
                switch (ch.DefaultFormationClass)
                {
                    case FormationClass.Cavalry: return 0;
                    case FormationClass.HorseArcher: return 1;
                    case FormationClass.Infantry: return 2;
                    case FormationClass.Ranged: return 3;
                    default: return 4;
                }
            }
            catch { return 4; }
        }

        internal static void AutoSort()
        {
            try
            {
                if (!Settings.Current.AutoSortParty) return;
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null) return;

                var stacks = new List<TaleWorlds.CampaignSystem.Roster.TroopRosterElement>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    stacks.Add(el);
                }
                if (stacks.Count < 2) return;

                var sorted = new List<TaleWorlds.CampaignSystem.Roster.TroopRosterElement>(stacks);
                sorted.Sort((a, b) =>
                {
                    int r = ArmRank(a.Character).CompareTo(ArmRank(b.Character));
                    if (r != 0) return r;
                    r = b.Character.Tier.CompareTo(a.Character.Tier);
                    if (r != 0) return r;
                    return string.CompareOrdinal(a.Character.StringId, b.Character.StringId);
                });

                bool same = true;
                for (int i = 0; i < stacks.Count; i++)
                    if (!ReferenceEquals(stacks[i].Character, sorted[i].Character)) { same = false; break; }
                if (same) return;

                foreach (var el in stacks)
                    roster.AddToCounts(el.Character, -el.Number, false, -el.WoundedNumber);
                foreach (var el in sorted)
                    roster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber, el.Xp);
            }
            catch (Exception e) { Log.Error("MusterOut.AutoSort", e); }
        }

        private static Dictionary<CharacterObject, int> Snapshot()
        {
            var map = new Dictionary<CharacterObject, int>();
            try
            {
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null) return map;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    int v; map.TryGetValue(el.Character, out v);
                    map[el.Character] = v + el.Number;
                }
            }
            catch { }
            return map;
        }

        public static void AfterInit()
        {
            try
            {
                AutoSort();            // posortowane ZANIM gracz zobaczy ekran
                _before = Snapshot();
            }
            catch { }
        }

        public static void AfterDone()
        {
            try
            {
                if (_before == null) return;
                var before = _before; _before = null;
                var after = Snapshot();

                var lost = new Dictionary<CharacterObject, int>();
                var gained = new Dictionary<CharacterObject, int>();
                foreach (var kv in before)
                {
                    int now; after.TryGetValue(kv.Key, out now);
                    if (kv.Value > now) lost[kv.Key] = kv.Value - now;
                }
                foreach (var kv in after)
                {
                    int was; before.TryGetValue(kv.Key, out was);
                    if (kv.Value > was) gained[kv.Key] = kv.Value - was;
                }
                if (lost.Count == 0) return;

                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;

                int men = 0, pieces = 0;
                foreach (var kv in lost)
                {
                    int leftGone = kv.Value;
                    // awans to nie zwolnienie: ubytek pokryty przyrostem celu awansu
                    var ups = kv.Key.UpgradeTargets;
                    if (ups != null)
                        foreach (var up in ups)
                        {
                            if (leftGone <= 0 || up == null) continue;
                            int g; gained.TryGetValue(up, out g);
                            if (g <= 0) continue;
                            int used = Math.Min(g, leftGone);
                            leftGone -= used;
                            gained[up] = g - used;
                        }
                    for (int m = 0; m < leftGone; m++)
                    {
                        Equipment eq = null; int n = 0;
                        foreach (var e in kv.Key.BattleEquipments) { n++; if (MBRandom.RandomInt(n) == 0) eq = e; }
                        if (eq == null) continue;
                        men++;
                        for (int slot = 0; slot < 12; slot++)
                        {
                            if (slot == 4) continue;   // choragiew zostaje przy sztandarze
                            var item = eq[(EquipmentIndex)slot].Item;
                            if (item == null || item.ItemType == ItemObject.ItemTypeEnum.Banner) continue;
                            if (item.StringId != null && item.StringId.StartsWith("dragon_")) continue;
                            if (LegendaryLaw.IsLegend(item)) continue;
                            armory.AddToCounts(new EquipmentElement(item, ArmouryBehavior.PickWornModifier(item)), 1);
                            pieces++;
                        }
                    }
                }
                if (pieces > 0)
                {
                    Log.Info("MusterOut: " + men + " zwolnionych oddalo " + pieces + " sztuk do magazynu.");
                    Log.Player("The dismissed hand their arms to the quartermaster - " + pieces + " pieces back in the wagons.", true);
                }
            }
            catch (Exception e) { Log.Error("MusterOut.AfterDone", e); }
        }
    }
}
