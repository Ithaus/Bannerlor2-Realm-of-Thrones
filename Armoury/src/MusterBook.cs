using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KSIEGA MUSZTRY (Jeff 29.08): "klikam grupe -> popup jednostki: ile
    /// doswiadczenia do awansu, pelne uzbrojenie, i moge PRZYPISAC jaki pancerz
    /// albo bron maja nosic - dla pojedynczej jednostki (typu)".
    /// Wejscie z menu miasta/wioski/kuzni. Lancuch okien z obrazkami:
    ///   oddzial -> akcja (przeglad / przypisz slot) -> przedmiot z MAGAZYNU.
    /// Przypisania (piny) zapisuja sie w save i steruja przydzialem DTE
    /// (SkillsDecide wstrzykuje pin do referencji zolnierza przed rozdaniem).
    /// Pin dziala dla CALEGO stacka danego typu - to jest "grupa".
    /// </summary>
    internal sealed class MusterBook : CampaignBehaviorBase
    {
        // "troopId|slot" -> itemId
        private Dictionary<string, string> _pins = new Dictionary<string, string>();
        internal static MusterBook Instance;

        public MusterBook() { Instance = this; }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSession);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("armouryMusterBookPins", ref _pins);
        }

        /// <summary>Czy item jest gdziekolwiek przypisany rozkazem z ksiegi
        /// (taki sprzet jest swiety dla przycinania magazynu).</summary>
        internal static bool IsPinnedItem(string itemId)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(itemId)) return false;
                foreach (var v in self._pins.Values)
                    if (v == itemId) return true;
                return false;
            }
            catch { return false; }
        }

        internal static ItemObject PinFor(CharacterObject troop, int slot)
        {
            try
            {
                var self = Instance;
                if (self == null || troop == null) return null;
                string id;
                if (!self._pins.TryGetValue(troop.StringId + "|" + slot, out id) || string.IsNullOrEmpty(id)) return null;
                return TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(id);
            }
            catch { return null; }
        }

        private void OnSession(CampaignGameStarter starter)
        {
            // KSIEGA MIESZKA W "MANAGE ARMOURY" (Jeff 29.08: "muster book powinno
            // byc w manage armoury - przeniesc") - submenu magazynu DTE w miescie
            try
            {
                starter.AddGameMenuOption("army_armory_submenu", "arm_muster_book",
                    "Open the muster book",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Manage;
                        var r = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                        return Settings.Current.MusterBookEnabled && r != null && r.TotalRegulars > 0;
                    },
                    delegate (MenuCallbackArgs a) { OpenBook(); }, false, 1, false);
            }
            catch (Exception e) { Log.Error("MusterBook.menu army_armory_submenu", e); }
        }

        // ------------------------------------------------------------ ekran 1: oddzialy
        private static string ArmName(CharacterObject ch)
        {
            switch (ch.DefaultFormationClass)
            {
                case FormationClass.Cavalry: return "Cavalry";
                case FormationClass.HorseArcher: return "Horse archers";
                case FormationClass.Ranged: return "Archers";
                default: return "Infantry";
            }
        }

        private static void OpenBook()
        {
            try
            {
                var roster = MobileParty.MainParty.MemberRoster;
                var rows = new List<InquiryElement>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    string title = "[" + ArmName(ch) + " t" + ch.Tier + "] " + ch.Name + "  x" + el.Number;
                    rows.Add(new InquiryElement(ch, title,
                        new TaleWorlds.Core.ImageIdentifiers.CharacterImageIdentifier(CharacterCode.CreateFrom(ch)),
                        true, XpLine(el)));
                }
                if (rows.Count == 0) return;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The muster book", "Pick a company to inspect.", rows, true, 1, 1,
                    "Inspect", "Close",
                    delegate (List<InquiryElement> picked)
                    {
                        if (picked != null && picked.Count > 0)
                            OpenTroop(picked[0].Identifier as CharacterObject);
                    }, null), false);
            }
            catch (Exception e) { Log.Error("MusterBook.OpenBook", e); }
        }

        private static string XpLine(TaleWorlds.CampaignSystem.Roster.TroopRosterElement el)
        {
            try
            {
                var ch = el.Character;
                if (ch.UpgradeTargets == null || ch.UpgradeTargets.Length == 0) return "No further rank to earn.";
                int need = Campaign.Current.Models.PartyTroopUpgradeModel
                    .GetXpCostForUpgrade(PartyBase.MainParty, ch, ch.UpgradeTargets[0]);
                return "Experience: " + el.Xp + " / " + need + " per man toward " + ch.UpgradeTargets[0].Name + ".";
            }
            catch { return ""; }
        }

        // ------------------------------------------------------------ ekran 2: jednostka
        private static readonly int[] Slots = { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11 };
        private static string SlotName(int s)
        {
            switch (s)
            {
                case 0: return "Weapon 1"; case 1: return "Weapon 2";
                case 2: return "Weapon 3"; case 3: return "Weapon 4";
                case 5: return "Helmet"; case 6: return "Body armour";
                case 7: return "Boots"; case 8: return "Gloves";
                case 9: return "Cape";
                case 10: return "Mount"; default: return "Horse harness";
            }
        }

        private static void OpenTroop(CharacterObject ch)
        {
            try
            {
                if (ch == null) return;
                Equipment eq = null; foreach (var e in ch.BattleEquipments) { eq = e; break; }
                var rows = new List<InquiryElement>();
                foreach (var s in Slots)
                {
                    var pin = PinFor(ch, s);
                    var cur = pin ?? (eq != null ? eq[(EquipmentIndex)s].Item : null);
                    string label = SlotName(s) + ": " + (cur != null ? cur.Name.ToString() : "-")
                                   + (pin != null ? "  [ASSIGNED]" : "");
                    rows.Add(new InquiryElement(s, label, SmithMenu.ItemPic(cur), true,
                        pin != null ? "Assigned by you - the quartermaster issues this piece." : "From the company pattern."));
                }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    ch.Name.ToString(), "Pick a slot to assign gear from the company stores (whole company of this troop).",
                    rows, true, 1, 1, "Assign", "Back",
                    delegate (List<InquiryElement> picked)
                    {
                        if (picked != null && picked.Count > 0)
                            OpenSlot(ch, (int)picked[0].Identifier);
                    },
                    delegate (List<InquiryElement> x) { OpenBook(); }), false);
            }
            catch (Exception e) { Log.Error("MusterBook.OpenTroop", e); }
        }

        // ------------------------------------------------------------ ekran 3: przedmiot z magazynu
        private static bool Fits(ItemObject it, int slot)
        {
            if (it == null) return false;
            switch (slot)
            {
                case 5: return it.ItemType == ItemObject.ItemTypeEnum.HeadArmor;
                case 6: return it.ItemType == ItemObject.ItemTypeEnum.BodyArmor;
                case 7: return it.ItemType == ItemObject.ItemTypeEnum.LegArmor;
                case 8: return it.ItemType == ItemObject.ItemTypeEnum.HandArmor;
                case 9: return it.ItemType == ItemObject.ItemTypeEnum.Cape;
                case 10:
                    return it.ItemType == ItemObject.ItemTypeEnum.Horse
                           && (it.StringId == null || !it.StringId.StartsWith("dragon_"));
                case 11: return it.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
                default:
                    return it.HasWeaponComponent && it.ItemType != ItemObject.ItemTypeEnum.Banner
                           && !LegendaryLaw.IsLegend(it)
                           && (it.StringId == null || !it.StringId.StartsWith("dragon_"));
            }
        }

        /// <summary>Wymogi przedmiotu vs umiejetnosc jednostki - ponad stan
        /// pozycja jest WYSZARZONA. CALY ekwipunek: bron/kon wg swojego skilla,
        /// pancerz wg ATLETYKI (ItemReq - zasada nadrzedna Jeffa).</summary>
        private static bool MeetsReq(CharacterObject ch, ItemObject it, out string why)
        {
            return ItemReq.Meets(ch, it, out why);
        }

        /// <summary>Ilu ludzi liczy kompania tego typu w partii gracza.</summary>
        private static int MenOf(CharacterObject ch)
        {
            try
            {
                var roster = MobileParty.MainParty.MemberRoster;
                int n = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == ch) n += el.Number;
                }
                return n;
            }
            catch { return 0; }
        }

        private static string SupplyLine(int have, int need)
        {
            if (need <= 0) return "In the stores: " + have + " pieces.";
            return have >= need
                ? "In the stores: " + have + " of " + need + " needed - enough for ALL."
                : "In the stores: " + have + " of " + need + " needed - " + (need - have) + " SHORT.";
        }

        /// <summary>Zapotrzebowanie na sztuki danego typu: lucznik nosi
        /// 2 kolczany (RearmBySkill), wiec strzaly/belty licza sie x2 na
        /// glowe (Jeff 29.08: "czemu mam 49/214 arrow?" - prawda bylo 49/428).</summary>
        private static int NeedFor(ItemObject it, int men)
        {
            return (it != null && (it.ItemType == ItemObject.ItemTypeEnum.Arrows
                                   || it.ItemType == ItemObject.ItemTypeEnum.Bolts))
                ? men * 2 : men;
        }

        private static void OpenSlot(CharacterObject ch, int slot)
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                int men = MenOf(ch);
                var rows = new List<InquiryElement>();
                rows.Add(new InquiryElement("CLEAR", "(company pattern - clear the assignment)", null, true,
                    "Back to whatever the pattern and the stores decide."));
                var seen = new HashSet<ItemObject>();
                if (armory != null)
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !Fits(it, slot) || !seen.Add(it)) continue;
                        string why; bool ok = MeetsReq(ch, it, out why);
                        int needA = NeedFor(it, men);
                        rows.Add(new InquiryElement(it,
                            it.Name + "  x" + el.Amount + "/" + needA + "  (t" + ((int)it.Tier + 1) + ")",
                            SmithMenu.ItemPic(it), ok, ok ? SupplyLine(el.Amount, needA) : why));
                    }
                // BRON I PANCERZ NA TWOIM GRZBIECIE (Jeff: "mialem ten luk u siebie
                // i nie moge go wybrac") - zalozone sztuki nie leza w sakwach;
                // wybor zdejmuje sztuke z ciebie i oddaje na stan magazynu
                try
                {
                    var beq = TaleWorlds.CampaignSystem.Hero.MainHero.BattleEquipment;
                    for (int ws = 0; ws < 12; ws++)
                    {
                        var ee = beq[ws];
                        if (ee.Item == null || !Fits(ee.Item, slot) || !seen.Add(ee.Item)) continue;
                        string whyW; bool okW = MeetsReq(ch, ee.Item, out whyW);
                        int needW = NeedFor(ee.Item, men);
                        rows.Add(new InquiryElement(1000 + ws,
                            ee.Item.Name + "  x1/" + needW + "  (t" + ((int)ee.Item.Tier + 1) + ")  [WORN BY YOU]",
                            SmithMenu.ItemPic(ee.Item), okW,
                            okW ? "You wear this now - assigning takes it OFF your back into the stores. "
                                  + SupplyLine(1, needW)
                                : whyW));
                    }
                }
                catch { }
                // TWOJE SAKWY TEZ (Jeff 29.08: "nie widzi lukow, ktore wykulem") -
                // wybor takiej sztuki PRZENOSI ja na stan magazynu, bo kwatermistrz
                // wydaje tylko to, co ma na stanie
                var bags = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (bags != null)
                    for (int i = 0; i < bags.Count; i++)
                    {
                        var el = bags.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !Fits(it, slot) || !seen.Add(it)) continue;
                        string why2; bool ok2 = MeetsReq(ch, it, out why2);
                        int needB = NeedFor(it, men);
                        rows.Add(new InquiryElement(it,
                            it.Name + "  x" + el.Amount + "/" + needB + "  (t" + ((int)it.Tier + 1) + ")  [YOUR BAGGAGE]",
                            SmithMenu.ItemPic(it), ok2,
                            ok2 ? "In YOUR baggage - assigning moves them to the stores. " + SupplyLine(el.Amount, needB) : why2));
                    }
                // sort po typach (Jeff: "wszystkie luki razem, potem strzaly"),
                // w typie tier malejaco; wiersz "(company pattern)" zostaje na gorze
                Func<object, ItemObject> itemOf = delegate (object id)
                {
                    var io = id as ItemObject;
                    if (io != null) return io;
                    if (id is int code && code >= 1000)
                        try { return TaleWorlds.CampaignSystem.Hero.MainHero.BattleEquipment[code - 1000].Item; }
                        catch { return null; }
                    return null;   // "CLEAR" zostaje na gorze
                };
                rows.Sort((a, b) =>
                {
                    var ia = itemOf(a.Identifier); var ib = itemOf(b.Identifier);
                    if (ia == null && ib == null) return 0;
                    if (ia == null) return -1;
                    if (ib == null) return 1;
                    int r = SmithMenu.TypeRank(ia).CompareTo(SmithMenu.TypeRank(ib));
                    if (r != 0) return r;
                    r = ib.Tier.CompareTo(ia.Tier);
                    if (r != 0) return r;
                    return string.CompareOrdinal(ia.StringId, ib.StringId);
                });

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    ch.Name + " - " + SlotName(slot),
                    "The quartermaster will issue the assigned piece to every man of this troop (as stores allow).",
                    rows, true, 1, 1, "Assign", "Back",
                    delegate (List<InquiryElement> picked)
                    {
                        if (picked == null || picked.Count == 0) return;
                        var self = Instance; if (self == null) return;
                        string key = ch.StringId + "|" + slot;
                        // [WORN BY YOU]: sztuka schodzi z twojego grzbietu na stan
                        if (picked[0].Identifier is int wornCode && wornCode >= 1000)
                        {
                            try
                            {
                                int ws = wornCode - 1000;
                                var beq = TaleWorlds.CampaignSystem.Hero.MainHero.BattleEquipment;
                                var ee = beq[ws];
                                if (ee.Item != null)
                                {
                                    var store2 = QuartermasterLaw.DteArmory();
                                    if (store2 != null) store2.AddToCounts(ee, 1);
                                    ArmouryBehavior.StockDeposit(ee.Item.StringId, 1);   // wklad gracza
                                    beq[ws] = new EquipmentElement(null);
                                    self._pins[key] = ee.Item.StringId;
                                    Log.Player(ee.Item.Name + " goes from your back to the company stores - "
                                               + ch.Name + ": " + SlotName(slot) + " assigned.", true);
                                }
                            }
                            catch (Exception e3) { Log.Error("MusterBook.wornAssign", e3); }
                            OpenTroop(ch);
                            return;
                        }
                        if (picked[0].Identifier is ItemObject it)
                        {
                            self._pins[key] = it.StringId;
                            // sztuki z sakw ida na stan magazynu - kwatermistrz
                            // wydaje tylko to, co ma na stanie
                            try
                            {
                                var bags2 = MobileParty.MainParty.ItemRoster;
                                var store = QuartermasterLaw.DteArmory();
                                if (bags2 != null && store != null)
                                {
                                    int moved = 0;
                                    for (int i = bags2.Count - 1; i >= 0; i--)
                                    {
                                        var el = bags2.GetElementCopyAtIndex(i);
                                        if (el.EquipmentElement.Item != it || el.Amount <= 0) continue;
                                        store.AddToCounts(el.EquipmentElement, el.Amount);
                                        bags2.AddToCounts(el.EquipmentElement, -el.Amount);
                                        moved += el.Amount;
                                    }
                                    if (moved > 0)
                                    {
                                        ArmouryBehavior.StockDeposit(it.StringId, moved);   // wklad gracza - moze cofnac
                                        Log.Player(moved + " x " + it.Name + " moved from your baggage to the company stores.", true);
                                    }
                                }
                            }
                            catch (Exception e2) { Log.Error("MusterBook.moveToStores", e2); }
                            Log.Player(ch.Name + ": " + SlotName(slot) + " assigned - " + it.Name + ".", true);
                        }
                        else
                        {
                            self._pins.Remove(key);
                            Log.Player(ch.Name + ": " + SlotName(slot) + " back to the company pattern.", true);
                        }
                        OpenTroop(ch);
                    },
                    delegate (List<InquiryElement> x) { OpenTroop(ch); }), false);
            }
            catch (Exception e) { Log.Error("MusterBook.OpenSlot", e); }
        }
    }
}
