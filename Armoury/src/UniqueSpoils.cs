using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// UNIKATY U POSTACI, NIE NA TARGU (Jeff 04.10: "unikatow ma nie byc na targu - te unikaty maja postacie i potem
    /// mozna je zdobyc od nich, musi gdzies to byc, aby mozna bylo to zdobyc"; "jak maja kupic na targu zbroje Brienne,
    /// skoro Brienne ja nosi").
    ///  1. Spis unikatow ROT (RotUniques, docs/ROT-UNIKATY.md) dostaje NotMerchandise - sklepy ich nie zaopatruja;
    ///     przy wczytaniu i codziennie schodza z polek wszystkich osad, przy wczytaniu tez z bagazy AI (kopie z
    ///     zaopatrzenia startowego - oryginal nosi postac).
    ///  2. Zdobycie: zwyczaj wojenny XIV w. - zbroja i kon jenca naleza do tego, kto go pojmal. Gdy gracz bierze
    ///     w niewole albo zabija w walce postac, ktora NOSI unikat (ekwipunek bojowy), unikat trafia do taboru gracza,
    ///     a postac dostaje zwykly zamiennik swojej kultury (UniqueLaw.StandInFor) - nic nie powstaje i nie znika.
    /// </summary>
    internal static class UniqueSpoils
    {
        private static bool Is(ItemObject it) { return it != null && it.StringId != null && RotUniques.Ids.Contains(it.StringId); }

        internal static void OnSessionLaunched()
        {
            int flagged = 0, bags = 0;
            try
            {
                var fMerch = HarmonyLib.AccessTools.Field(typeof(ItemObject), "<NotMerchandise>k__BackingField");
                if (fMerch != null)
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (Is(it) && !it.NotMerchandise) { fMerch.SetValue(it, true); flagged++; }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.flag", e); }
            try
            {
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp == MobileParty.MainParty || mp.ItemRoster == null) continue;
                    bags += Strip(mp.ItemRoster);
                }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.bags", e); }
            int shelves = SweepShelves();
            Log.Info("UniqueSpoils: unikaty ROT poza handlem - oznaczone " + flagged + ", zdjete z targow " + shelves + ", z bagazy AI " + bags + " szt.; zdobywa sie je od postaci, ktore je nosza.");
        }

        internal static void Daily()
        {
            int n = SweepShelves();
            if (n > 0) Log.Info("UniqueSpoils: dzien - " + n + " szt. unikatow zdjetych z targow.");
        }

        private static int Strip(ItemRoster roster)
        {
            int off = 0;
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || !Is(el.EquipmentElement.Item)) continue;
                roster.AddToCounts(el.EquipmentElement, -el.Amount);
                off += el.Amount;
            }
            return off;
        }

        private static int SweepShelves()
        {
            int off = 0;
            try
            {
                foreach (var st in Settlement.All)
                    if (st != null && st.ItemRoster != null) off += Strip(st.ItemRoster);
            }
            catch (Exception e) { Log.Error("UniqueSpoils.shelves", e); }
            return off;
        }

        // ------------------------------------------------------------ zdobycz
        internal static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try { if (capturer != null && capturer == PartyBase.MainParty) Take(prisoner, "taken captive"); }
            catch (Exception e) { Log.Error("UniqueSpoils.Prisoner", e); }
        }

        internal static void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (victim == null || killer == null) return;
                if (killer != Hero.MainHero && killer.PartyBelongedTo != MobileParty.MainParty) return;
                Take(victim, "slain");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.Killed", e); }
        }

        private static void Take(Hero h, string how)
        {
            if (h == null || h == Hero.MainHero || MobileParty.MainParty == null) return;
            var eq = h.BattleEquipment;
            if (eq == null) return;
            var got = new List<string>();
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var slot = (EquipmentIndex)i;
                var el = eq[slot];
                if (el.IsEmpty || !Is(el.Item)) continue;
                MobileParty.MainParty.ItemRoster.AddToCounts(el, 1);
                ItemObject stand = null;
                try { stand = UniqueLaw.StandInFor(el.Item, h.Culture); } catch { }
                eq[slot] = stand != null ? new EquipmentElement(stand) : EquipmentElement.Invalid;
                got.Add(el.Item.Name.ToString());
            }
            if (got.Count == 0) return;
            Log.Info("UniqueSpoils: " + h.Name + " (" + how + ") - zdobyte unikaty: " + string.Join(", ", got.ToArray()) + ".");
            Log.Player("By the custom of war, the arms of " + h.Name + " are yours: " + string.Join(", ", got.ToArray()) + ".");
        }
    }
}
