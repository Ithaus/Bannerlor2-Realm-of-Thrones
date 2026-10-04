using System;
using System.Collections.Generic;
using System.Linq;
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
    /// UNIKATY KRAZA PO SWIECIE, NIE POWSTAJA I NIE ZNIKAJA (Jeff 04.10: "unikatow ma nie byc na targu - te unikaty maja
    /// postacie i potem mozna je zdobyc od nich"; "jak maja kupic na targu zbroje Brienne, skoro Brienne ja nosi";
    /// "jesli sprzedam, to bedzie gdzies w swiecie - maja byc unikaty monitorowane, kto potem kupil, gdzie jest, aby nie
    /// znikaly; jak sprzedam, moze pojawia sie gdzies indziej, bo ktos sprzeda albo jakis lord kupi"; AI pojmujace: "tak").
    ///  1. Spis unikatow ROT (RotUniques, docs/ROT-UNIKATY.md) dostaje NotMerchandise - sklepy ich nie zaopatruja i warsztaty
    ///     ich nie robia. RAZ na kampanie (flaga w save) zdejmujemy kopie z zaopatrzenia startowego: z polek i bagazy AI -
    ///     oryginal nosi postac. Potem NIC nie znika: unikat sprzedany na targu lezy na polce.
    ///  2. Zwyczaj wojenny XIV w. (zbroja i kon jenca dla pojmujacego): kto bierze w niewole albo zabija w walce postac, ktora
    ///     NOSI unikat - gracz albo lord AI - dostaje go (gracz do taboru, lord zaklada; jego stara sztuka do taboru partii).
    ///     Pojmany dostaje zwykly zamiennik swojej kultury.
    ///  3. Lord AI w miescie, w ktorym na polce lezy unikat lepszy od tego, co nosi w tym miejscu, kupuje go (placi miastu)
    ///     i zaklada - stara sztuka do taboru.
    ///  4. Kronika: codziennie spis, gdzie jest kazdy unikat (na kim, w czyim taborze, na jakim targu) - log tylko zmian.
    /// </summary>
    internal static class UniqueSpoils
    {
        private static bool _initDone;
        private static Dictionary<string, string> _last = new Dictionary<string, string>();

        internal static void Reset() { _initDone = false; _last = new Dictionary<string, string>(); }
        internal static string Export() { return _initDone ? "init" : ""; }
        internal static void Import(string s) { _initDone = s == "init"; }

        internal static bool Is(ItemObject it) { return it != null && it.StringId != null && RotUniques.Ids.Contains(it.StringId); }

        internal static void OnSessionLaunched()
        {
            int flagged = 0, bags = 0, shelves = 0;
            try
            {
                var fMerch = HarmonyLib.AccessTools.Field(typeof(ItemObject), "<NotMerchandise>k__BackingField");
                if (fMerch != null)
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (Is(it) && !it.NotMerchandise) { fMerch.SetValue(it, true); flagged++; }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.flag", e); }
            if (!_initDone)
            {
                _initDone = true;
                try
                {
                    foreach (var mp in MobileParty.All)
                        if (mp != null && mp != MobileParty.MainParty && mp.ItemRoster != null) bags += Strip(mp.ItemRoster);
                    foreach (var st in Settlement.All)
                        if (st != null && st.ItemRoster != null) shelves += Strip(st.ItemRoster);
                }
                catch (Exception e) { Log.Error("UniqueSpoils.init", e); }
            }
            Log.Info("UniqueSpoils: unikaty ROT poza zaopatrzeniem sklepow - oznaczone " + flagged + "; kopie startowe zdjete raz na kampanie: z targow " + shelves + ", z bagazy AI " + bags + " szt.");
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

        // ------------------------------------------------------------ zdobycz (pojmanie, smierc w walce)
        internal static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try { if (capturer != null) Take(prisoner, capturer.LeaderHero, capturer == PartyBase.MainParty, "pojmanie"); }
            catch (Exception e) { Log.Error("UniqueSpoils.Prisoner", e); }
        }

        internal static void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
        {
            try
            {
                if (victim == null || killer == null || detail != KillCharacterAction.KillCharacterActionDetail.DiedInBattle) return;
                bool player = killer == Hero.MainHero || killer.PartyBelongedTo == MobileParty.MainParty;
                Take(victim, player ? Hero.MainHero : (killer.PartyBelongedTo != null && killer.PartyBelongedTo.LeaderHero != null ? killer.PartyBelongedTo.LeaderHero : killer), player, "smierc w walce");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.Killed", e); }
        }

        private static void Take(Hero from, Hero to, bool toPlayer, string how)
        {
            if (from == null || to == null || from == to) return;
            if (from == Hero.MainHero && !Settings.Current.UniqueSpoilsFromPlayer) return;
            var eq = from.BattleEquipment;
            if (eq == null) return;
            var got = new List<string>();
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var slot = (EquipmentIndex)i;
                var el = eq[slot];
                if (el.IsEmpty || !Is(el.Item)) continue;
                ItemObject stand = null;
                try { stand = UniqueLaw.StandInFor(el.Item, from.Culture); } catch { }
                eq[slot] = stand != null ? new EquipmentElement(stand) : EquipmentElement.Invalid;
                if (toPlayer) MobileParty.MainParty.ItemRoster.AddToCounts(el, 1);
                else Wear(to, el);
                got.Add(el.Item.Name.ToString());
            }
            if (got.Count == 0) return;
            string list = string.Join(", ", got.ToArray());
            Log.Info("Kronika unikatow: " + from.Name + " -> " + to.Name + " (" + how + "): " + list + ".");
            if (toPlayer) Log.Player("By the custom of war, the arms of " + from.Name + " are yours: " + list + ".");
            else if (from == Hero.MainHero) Log.Player(to.Name + " takes your arms by the custom of war: " + list + ".", true);
            // cudze zdobycze tylko w kronice (Jeff: za duzo smieci)
        }

        /// <summary>Lord AI zaklada sztuke w jej miejsce; to, co nosil, idzie do taboru jego partii (nic nie znika).</summary>
        private static void Wear(Hero h, EquipmentElement el)
        {
            var eq = h.BattleEquipment;
            var slot = SlotFor(eq, el.Item);
            var old = eq[slot];
            eq[slot] = el;
            if (!old.IsEmpty)
            {
                var roster = h.PartyBelongedTo != null ? h.PartyBelongedTo.ItemRoster : null;
                if (roster != null) roster.AddToCounts(old, 1);
            }
        }

        private static EquipmentIndex SlotFor(Equipment eq, ItemObject it)
        {
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor: return EquipmentIndex.Head;
                case ItemObject.ItemTypeEnum.BodyArmor: return EquipmentIndex.Body;
                case ItemObject.ItemTypeEnum.LegArmor: return EquipmentIndex.Leg;
                case ItemObject.ItemTypeEnum.HandArmor: return EquipmentIndex.Gloves;
                case ItemObject.ItemTypeEnum.Cape: return EquipmentIndex.Cape;
                case ItemObject.ItemTypeEnum.HorseHarness: return EquipmentIndex.HorseHarness;
            }
            // bron: ten sam typ, potem pusty slot, potem pierwszy
            for (int i = 0; i < 4; i++) { var e = eq[(EquipmentIndex)i]; if (!e.IsEmpty && e.Item.ItemType == it.ItemType) return (EquipmentIndex)i; }
            for (int i = 0; i < 4; i++) if (eq[(EquipmentIndex)i].IsEmpty) return (EquipmentIndex)i;
            return EquipmentIndex.Weapon0;
        }

        // ------------------------------------------------------------ lord AI kupuje unikat z polki
        internal static void OnDailyTickParty(MobileParty mp)
        {
            try
            {
                if (mp == null || mp.IsMainParty || !mp.IsLordParty || mp.LeaderHero == null || mp.CurrentSettlement == null) return;
                var st = mp.CurrentSettlement;
                if (!st.IsTown || st.Town == null || st.ItemRoster == null) return;
                var lord = mp.LeaderHero;
                var shelf = st.ItemRoster;
                for (int i = shelf.Count - 1; i >= 0; i--)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || !Is(it)) continue;
                    if (it.Difficulty > 0 && it.RelevantSkill != null && lord.GetSkillValue(it.RelevantSkill) < it.Difficulty) continue;
                    var cur = lord.BattleEquipment[SlotFor(lord.BattleEquipment, it)];
                    if (!cur.IsEmpty && (Is(cur.Item) || cur.Item.Effectiveness >= it.Effectiveness)) continue;
                    int price = st.Town.MarketData.GetPrice(el.EquipmentElement, mp, false, st.Party);
                    if (price <= 0 || lord.Gold < price * 2) continue;     // nie kupi za ostatnie pieniadze
                    shelf.AddToCounts(el.EquipmentElement, -1);
                    lord.ChangeHeroGold(-price);
                    st.Town.ChangeGold(price);
                    Wear(lord, el.EquipmentElement);
                    Log.Info("Kronika unikatow: " + lord.Name + " kupil " + it.StringId + " w " + st.Name + " za " + price + ".");

                    return;
                }
            }
            catch (Exception e) { Log.Error("UniqueSpoils.Buy", e); }
        }

        // ------------------------------------------------------------ kronika
        internal static void Daily()
        {
            try
            {
                var now = new Dictionary<string, string>();
                Action<string, string> add = (id, where) =>
                {
                    string v; now[id] = now.TryGetValue(id, out v) ? v + "; " + where : where;
                };
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null) continue;
                    var eq = h.BattleEquipment;
                    if (eq == null) continue;
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        var el = eq[(EquipmentIndex)i];
                        if (!el.IsEmpty && Is(el.Item)) add(el.Item.StringId, "nosi " + h.Name);
                    }
                }
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp.ItemRoster == null) continue;
                    var r = mp.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        if (el.Amount > 0 && Is(el.EquipmentElement.Item)) add(el.EquipmentElement.Item.StringId, "tabor " + mp.Name + (el.Amount > 1 ? " x" + el.Amount : ""));
                    }
                }
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.ItemRoster == null) continue;
                    var r = st.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        if (el.Amount > 0 && Is(el.EquipmentElement.Item)) add(el.EquipmentElement.Item.StringId, "targ " + st.Name + (el.Amount > 1 ? " x" + el.Amount : ""));
                    }
                }
                var changes = new List<string>();
                foreach (var kv in now)
                {
                    string was;
                    if (!_last.TryGetValue(kv.Key, out was) || was != kv.Value) changes.Add(kv.Key + ": " + kv.Value);
                }
                foreach (var kv in _last) if (!now.ContainsKey(kv.Key)) changes.Add(kv.Key + ": nigdzie (zniszczony/stracony z bohaterem?)");
                bool first = _last.Count == 0;
                _last = now;
                if (changes.Count > 0)
                    Log.Info("Kronika unikatow: dzien " + (int)CampaignTime.Now.ToDays + (first ? " - stan swiata (" + now.Count + " unikatow w obiegu): " : " - zmiany: ") + string.Join(" | ", changes.ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("UniqueSpoils.Daily", e); }
        }
    }
}
