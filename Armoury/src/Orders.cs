using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// Zamowienia na zbroje od lordow. Gra ma zamowienia tylko na bron (CraftingOrder wymaga
    /// WeaponDesign, ktorego zbroja nie ma), wiec ten system jest nasz od poczatku do konca:
    /// lord sklada zamowienie u kowala w miescie, ty je przyjmujesz, wykuwasz sztuke w zakladce
    /// CRAFT i przynosisz przed terminem. Placa ponad cene targowa - kowal bez posrednika -
    /// a spoznienie kosztuje cie dobre imie u zamawiajacego.
    /// </summary>
    internal static class Orders
    {
        // townId|heroId|itemId|day|state     state: 0 = oferta (day = wygasa), 1 = przyjete (day = termin)
        internal static List<string> Board = new List<string>();
        // townId|day - kiedy miasto ostatnio wystawilo oferte
        internal static List<string> Cooldowns = new List<string>();

        private static float Today { get { return (float)CampaignTime.Now.ToDays; } }
        private static string F(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }
        private static float PF(string v) { return float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); }

        // ------------------------------------------------------------ generowanie ofert
        internal static void OnSettlementEntered(MobileParty party, Settlement settlement)
        {
            try
            {
                var s = Settings.Current;
                if (!s.ArmourOrdersEnabled) return;
                if (party == null || !party.IsMainParty) return;
                if (settlement == null || settlement.Town == null) return;

                // cooldown miasta
                foreach (var c in Cooldowns)
                {
                    var p = c.Split('|');
                    if (p[0] == settlement.StringId && Today - PF(p[1]) < s.OrderTownCooldownDays) return;
                }
                if (MBRandom.RandomFloat > s.OrderOfferChance) return;

                var lord = PickLord(settlement);
                var item = PickItem(settlement);
                if (lord == null || item == null) return;

                Cooldowns.RemoveAll(c => c.StartsWith(settlement.StringId + "|", StringComparison.Ordinal));
                Cooldowns.Add(settlement.StringId + "|" + F(Today));
                Board.Add(settlement.StringId + "|" + lord.StringId + "|" + item.StringId + "|" +
                          F(Today + s.OrderOfferLifeDays) + "|0");

                Log.Player("The smith has word: " + lord.Name + " will pay well for a " + item.Name +
                           ". Ask at the forge.", false);
                Log.Info("Oferta: " + lord.StringId + " chce " + item.StringId + " w " + settlement.StringId);
            }
            catch (Exception e) { Log.Error("Orders.OnSettlementEntered", e); }
        }

        private static Hero PickLord(Settlement settlement)
        {
            try
            {
                var pool = new List<Hero>();
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || !h.IsLord || h.IsPrisoner || h == Hero.MainHero) continue;
                    if (h.MapFaction == null || h.MapFaction != settlement.MapFaction) continue;
                    if (h.GetRelationWithPlayer() < -20) continue;
                    pool.Add(h);
                }
                return pool.Count > 0 ? pool[MBRandom.RandomInt(pool.Count)] : null;
            }
            catch { return null; }
        }

        private static ItemObject PickItem(Settlement settlement)
        {
            try
            {
                var s = Settings.Current;
                var pool = new List<ItemObject>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !it.HasArmorComponent || it.NotMerchandise) continue;
                    int tier = (int)it.Tier + 1;
                    if (tier < s.OrderMinTier || tier > s.OrderMaxTier) continue;
                    if (it.Value <= 0 || it.Value > s.OrderMaxItemValue) continue;
                    if (it.Culture != null && settlement.Culture != null && it.Culture != settlement.Culture) continue;
                    pool.Add(it);
                }
                return pool.Count > 0 ? pool[MBRandom.RandomInt(pool.Count)] : null;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ uplyw czasu
        internal static void DailyTick()
        {
            try
            {
                var s = Settings.Current;
                var copy = new List<string>(Board);
                foreach (var line in copy)
                {
                    var p = line.Split('|');
                    if (p.Length < 5) { Board.Remove(line); continue; }
                    float day = PF(p[3]);
                    if (Today <= day) continue;

                    Board.Remove(line);
                    if (p[4] == "1")
                    {
                        var lord = MBObjectManager.Instance.GetObject<Hero>(p[1]);
                        var item = MBObjectManager.Instance.GetObject<ItemObject>(p[2]);
                        if (lord != null)
                        {
                            ChangeRelationAction.ApplyPlayerRelation(lord, -s.OrderMissRelationPenalty);
                            Log.Player("The day came and went. " + lord.Name + " will remember that the " +
                                       (item != null ? item.Name.ToString() : "piece") + " never arrived.", true);
                        }
                        Log.Info("Zamowienie przepadlo: " + line);
                    }
                }
            }
            catch (Exception e) { Log.Error("Orders.DailyTick", e); }
        }

        // ------------------------------------------------------------ menu
        internal static int CountAt(Settlement settlement)
        {
            int n = 0;
            foreach (var line in Board)
                if (settlement != null && line.StartsWith(settlement.StringId + "|", StringComparison.Ordinal)) n++;
            foreach (var line in Board)
                if (line.EndsWith("|1", StringComparison.Ordinal) &&
                    settlement != null && !line.StartsWith(settlement.StringId + "|", StringComparison.Ordinal)) n++;
            return n;
        }

        internal static void Show()
        {
            try
            {
                var s = Settings.Current;
                var here = Settlement.CurrentSettlement;
                var elements = new List<InquiryElement>();

                foreach (var line in Board)
                {
                    var p = line.Split('|');
                    if (p.Length < 5) continue;
                    bool accepted = p[4] == "1";
                    bool isHere = here != null && p[0] == here.StringId;
                    if (!accepted && !isHere) continue;      // cudze oferty widac tylko na miejscu

                    var lord = MBObjectManager.Instance.GetObject<Hero>(p[1]);
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(p[2]);
                    var town = Settlement.Find(p[0]);
                    if (lord == null || item == null) continue;

                    int pay = (int)(item.Value * s.OrderPayMultiplier);
                    float daysLeft = PF(p[3]) - Today;
                    string title, hint;
                    bool enabled;

                    if (!accepted)
                    {
                        title = "OFFER: " + item.Name + " for " + lord.Name;
                        hint = "Pay " + pay + " gold on delivery (market worth " + item.Value + ")." +
                               "\nTier " + ((int)item.Tier + 1) + " - forge it in the CRAFT tab." +
                               "\nOnce taken: " + (int)s.OrderDeadlineDays + " days to deliver here." +
                               "\nThe offer itself stands " + Math.Max(0, (int)daysLeft) + " more days.";
                        enabled = CountAccepted() < s.MaxAcceptedOrders;
                        if (!enabled) hint += "\nYour book is full (" + s.MaxAcceptedOrders + " orders).";
                    }
                    else
                    {
                        bool have = HasItem(item);
                        title = item.Name + " for " + lord.Name + " - " + Math.Max(0, (int)daysLeft) + " days left";
                        hint = "Pay " + pay + " gold. Deliver at " + (town != null ? town.Name.ToString() : p[0]) + "." +
                               (have ? "\nYou carry the piece - select to hand it over." :
                                       "\nYou do not carry the piece yet.");
                        enabled = have && isHere;
                        if (have && !isHere) hint += "\nYou must be at " + (town != null ? town.Name.ToString() : p[0]) + ".";
                    }
                    elements.Add(new InquiryElement(line, title, null, enabled, hint));
                }

                if (elements.Count == 0)
                {
                    Log.Player("No lord has left an order with this smith.", false);
                    return;
                }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Order Book",
                    "Take an offer, or hand over a finished piece. The lords pay above the market - and remember a broken promise.",
                    elements, true, 1, 1, "Proceed", "Step back",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            Act(sel[0].Identifier as string);
                        }
                        catch (Exception ex) { Log.Error("Orders.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("Orders.Show", e); }
        }

        private static void Act(string line)
        {
            var s = Settings.Current;
            if (line == null || !Board.Contains(line)) return;
            var p = line.Split('|');
            var lord = MBObjectManager.Instance.GetObject<Hero>(p[1]);
            var item = MBObjectManager.Instance.GetObject<ItemObject>(p[2]);
            if (lord == null || item == null) return;

            if (p[4] == "0")
            {
                Board.Remove(line);
                Board.Add(p[0] + "|" + p[1] + "|" + p[2] + "|" + F(Today + s.OrderDeadlineDays) + "|1");
                Log.Player("Taken: a " + item.Name + " for " + lord.Name + ", within " +
                           (int)s.OrderDeadlineDays + " days. Forge it and bring it here.");
                Log.Info("Przyjeto: " + line);
                return;
            }

            // dostawa
            if (!HasItem(item)) { Log.Player("You do not carry the piece.", true); return; }
            var roster = MobileParty.MainParty.ItemRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster[i];
                if (el.EquipmentElement.Item == item && el.Amount > 0)
                { roster.AddToCounts(el.EquipmentElement, -1); break; }
            }
            int pay = (int)(item.Value * s.OrderPayMultiplier);
            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay);
            ChangeRelationAction.ApplyPlayerRelation(lord, s.OrderRelationReward);
            Board.Remove(line);
            Log.Player(lord.Name + " pays " + pay + " gold for the " + item.Name +
                       ". Word of your craft spreads.", false);
            Log.Info("Dostarczono: " + line + " za " + pay);
        }

        private static int CountAccepted()
        {
            int n = 0;
            foreach (var line in Board) if (line.EndsWith("|1", StringComparison.Ordinal)) n++;
            return n;
        }

        private static bool HasItem(ItemObject item)
        {
            var roster = MobileParty.MainParty.ItemRoster;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].EquipmentElement.Item == item && roster[i].Amount > 0) return true;
            return false;
        }
    }
}
