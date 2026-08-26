using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    internal static class SmithMenu
    {
        private const string Menu = "armoury_forge";

        internal static void Add(CampaignGameStarter starter)
        {
            // Jedno wejscie w menu miasta - reszta chowa sie pod nim, zeby nie robic balaganu.
            starter.AddGameMenuOption("town", "arm_enter",
                "{=!}Work the forge", EnterCondition,
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Menu); }, false, 5);

            starter.AddGameMenu(Menu,
                "{=!}The forge is hot and the anvil is free. The smith takes his fee and leaves you to it.",
                OnMenuInit, GameMenu.MenuOverlayType.SettlementWithBoth);

            starter.AddGameMenuOption(Menu, "arm_commission",
                "{=!}Forge armour, shields and tack", CommissionCondition, CommissionConsequence, false, 0);

            // luczarnia na widoku (Jeff: "dodaj do forge rzeczy strzeleckie") -
            // luki, kusze i amunicja maja WLASNE wejscie, nie schowane w zbrojach
            starter.AddGameMenuOption(Menu, "arm_fletcher",
                "{=!}String bows and fletch arrows", FletcherCondition, FletcherConsequence, false, 0);

            starter.AddGameMenuOption(Menu, "arm_orders",
                "{=!}The order book - commissions from the lords", OrdersCondition,
                delegate (MenuCallbackArgs a) { Orders.Show(); }, false, 1);

            starter.AddGameMenuOption(Menu, "arm_progress",
                "{=!}Look in on the work in hand", ProgressCondition, ProgressConsequence, false, 1);

            // czekanie PRZY ROBOCIE: zegar konczy sie razem z robota i wyrob
            // trafia do rak od razu - bez doczekiwania zwyklym "Wait" (i bez
            // odpoczywania: mlot to nie drzemka)
            starter.AddGameMenuOption(Menu, "arm_proj_wait_opt",
                "{=!}Stay at the forge until the work is done", ProjWaitCondition,
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("arm_project_wait"); }, false, 1);

            starter.AddGameMenuOption(Menu, "arm_selfrepair",
                "{=!}Mend a piece yourself at the anvil", SelfRepairCondition, SelfRepairConsequence, false, 2);

            starter.AddGameMenuOption(Menu, "arm_repair",
                "{=!}Pay the smith to mend your harness", RepairCondition, RepairConsequence, false, 3);

            starter.AddGameMenuOption(Menu, "arm_smelt",
                "{=!}Break metal down at the crucible", SmeltCondition, SmeltConsequence, false, 4);

            starter.AddGameMenuOption(Menu, "arm_takeapart",
                "{=!}Take a piece apart to copy its pattern", TakeApartCondition, TakeApartConsequence, false, 4);

            starter.AddGameMenuOption(Menu, "arm_mend_pick",
                "{=!}Pick a damaged piece to mend", MendPickCondition, MendPickConsequence, false, 5);

            starter.AddGameMenuOption(Menu, "arm_mend_loot",
                "{=!}Restore ALL battle-worn loot from your bags", MendLootCondition, MendLootConsequence, false, 6);

            // robota wymaga czasu: jedno wspolne menu oczekiwania dla napraw i przetopu
            starter.AddWaitGameMenu("arm_work_wait",
                "{=!}{ARM_WORK_TEXT}",
                WorkInit, delegate (MenuCallbackArgs a) { return true; }, null, WorkTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption,
                GameMenu.MenuOverlayType.SettlementWithBoth);
            starter.AddGameMenuOption("arm_work_wait", "arm_work_stop",
                "{=!}Put the work aside (nothing finished, nothing paid)",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                delegate (MenuCallbackArgs a) { _workApply = null; GameMenu.SwitchToMenu(Menu); }, true, 9);

            starter.AddGameMenuOption(Menu, "arm_leave",
                "{=!}Wipe your hands and step out", LeaveCondition,
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("town"); }, true, 9);

            starter.AddWaitGameMenu("arm_project_wait",
                "{=!}{ARM_PROJ_TEXT}",
                ProjWaitInit, delegate (MenuCallbackArgs a) { return true; }, null, ProjWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption,
                GameMenu.MenuOverlayType.SettlementWithBoth);
            starter.AddGameMenuOption("arm_project_wait", "arm_proj_wait_stop",
                "{=!}Step away - the work can wait",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Menu); }, true, 9);
        }

        // ------------------------------------------------- czekanie przy projektach
        private static float _projInitialHours;

        private static bool ProjWaitCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            var b = ArmouryBehavior.Instance;
            return b != null && b.HasProjectsHere(Settlement.CurrentSettlement);
        }

        private static void ProjWaitInit(MenuCallbackArgs args)
        {
            try
            {
                var b = ArmouryBehavior.Instance;
                _projInitialHours = b != null ? Math.Max(1f, b.ProjectHoursLeftHere(Settlement.CurrentSettlement)) : 1f;
                MBTextManager.SetTextVariable("ARM_PROJ_TEXT",
                    "You keep to the forge while the work is finished. About " +
                    ((int)Math.Ceiling(_projInitialHours)) + " hours of labour remain.");
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Log.Error("ProjWaitInit", e); }
        }

        private static void ProjWaitTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                var b = ArmouryBehavior.Instance;
                float left = b != null ? b.ProjectHoursLeftHere(Settlement.CurrentSettlement) : 0f;
                if (left <= 0.01f)
                {
                    GameMenu.SwitchToMenu(Menu);   // wyroby juz wydane przez Forge.Finish
                    return;
                }
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(1f - left / Math.Max(1f, _projInitialHours));
            }
            catch (Exception e) { Log.Error("ProjWaitTick", e); }
        }

        private static bool EnterCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Craft;
            return Settings.Current.CraftingEnabled;
        }

        // ------------------------------------------------- naprawa lupow bitewnych
        // KAZDY przedmiot ze stanem obnizajacym wartosc - lupy Spoils (rl_looted_*),
        // wraki, rdza, pekniecia, nasze zuzycie - kowal doprowadza do stanu fabrycznego.

        private static bool IsBattleWorn(ItemObject it, ItemModifier m)
        {
            if (ArmouryBehavior.NoWear(it)) return false;   // strzaly/belty poza systemem zuzycia
            return m != null && m.PriceMultiplier < 0.999f && m.PriceMultiplier > 0f;
        }

        private static void ScanBattleWorn(out int pieces, out int cost)
        {
            pieces = 0; cost = 0;
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var mod = el.EquipmentElement.ItemModifier;
                    if (el.EquipmentElement.Item == null || !IsBattleWorn(el.EquipmentElement.Item, mod)) continue;
                    float pm = mod.PriceMultiplier;
                    if (pm < 0f) pm = 0f; if (pm > 1f) pm = 1f;
                    int per = Math.Max(2, (int)(el.EquipmentElement.Item.Value * (1f - pm) * Settings.Current.RepairCostFactor) / 2);
                    pieces += el.Amount;
                    cost += per * el.Amount;
                }
            }
            catch (Exception e) { Log.Error("ScanBattleWorn", e); }
        }

        private static int PieceCost(EquipmentElement el)
        {
            float pm = el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f;
            if (pm < 0f) pm = 0f; if (pm > 1f) pm = 1f;
            // liczone jak dawniej, na koncu POL CENY (zyczenie Jeffa), w dol
            return Math.Max(2, (int)(el.Item.Value * (1f - pm) * Settings.Current.RepairCostFactor) / 2);
        }

        /// <summary>Ile sztuk (najtansze najpierw) i za ile zmiesci sie w sakwie.</summary>
        private static void AffordableBattleWorn(out int pieces, out int cost, out int totalPieces, out int totalCost)
        {
            pieces = 0; cost = 0; totalPieces = 0; totalCost = 0;
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                var costs = new List<int>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null || !IsBattleWorn(el.EquipmentElement.Item, el.EquipmentElement.ItemModifier)) continue;
                    int per = PieceCost(el.EquipmentElement);
                    for (int k = 0; k < el.Amount; k++) costs.Add(per);
                }
                costs.Sort();
                int gold = Hero.MainHero.Gold;
                foreach (var cst in costs)
                {
                    totalPieces++; totalCost += cst;
                    if (cost + cst <= gold) { pieces++; cost += cst; }
                }
            }
            catch (Exception e) { Log.Error("AffordableBattleWorn", e); }
        }

        private static bool MendLootCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                if (!Settings.Current.WreckSalvageEnabled && !Settings.Current.BattlefieldLawEnabled) return false;
                int can, canCost, all, allCost;
                AffordableBattleWorn(out can, out canCost, out all, out allCost);
                if (all == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}No battle-worn loot in your bags."); return true; }
                if (can == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}{ALL} worn pieces, {COST} gold for the lot - you cannot afford even the cheapest.").SetTextVariable("ALL", all).SetTextVariable("COST", allCost); return true; }
                if (can < all)
                    args.Tooltip = new TextObject("{=!}{ALL} worn pieces ({COST} gold for the lot). For your purse the smith will mend the {CAN} cheapest for {CANCOST}.")
                        .SetTextVariable("ALL", all).SetTextVariable("COST", allCost).SetTextVariable("CAN", can).SetTextVariable("CANCOST", canCost);
                else
                    args.Tooltip = new TextObject("{=!}{ALL} battle-worn pieces. The smith will make them whole for {COST} gold.")
                        .SetTextVariable("ALL", all).SetTextVariable("COST", allCost);
                return true;
            }
            catch (Exception e) { Log.Error("MendLootCondition", e); return false; }
        }

        private static void MendLootConsequence(MenuCallbackArgs args)
        {
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                var worn = new List<ItemRosterElement>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item != null && IsBattleWorn(el.EquipmentElement.Item, el.EquipmentElement.ItemModifier))
                        worn.Add(el);
                }
                if (worn.Count == 0) return;
                int canN, canC, allN, allC;
                AffordableBattleWorn(out canN, out canC, out allN, out allC);
                if (canN == 0) { Log.Player("You cannot pay for even the cheapest mend.", true); return; }
                StartTimedWork(canN * Settings.Current.MendLootHoursPerPiece,
                    "The smith sorts the battle spoils and takes hammer to the worst of it.",
                    delegate { DoMendLoot(); });
            }
            catch (Exception e) { Log.Error("MendLootConsequence", e); }
        }

        private static void DoMendLoot()
        {
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                var worn = new List<ItemRosterElement>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item != null && IsBattleWorn(el.EquipmentElement.Item, el.EquipmentElement.ItemModifier))
                        worn.Add(el);
                }
                if (worn.Count == 0) return;
                // najtansze najpierw - za posiadane zloto naprawiamy ile sie da
                worn.Sort((a, b) => PieceCost(a.EquipmentElement).CompareTo(PieceCost(b.EquipmentElement)));
                int paid = 0, done = 0, skipped = 0;
                foreach (var el in worn)
                {
                    int per = PieceCost(el.EquipmentElement);
                    int fix2 = 0;
                    for (int k = 0; k < el.Amount; k++)
                    {
                        if (Hero.MainHero.Gold - paid - per < 0) { skipped += el.Amount - k; break; }
                        paid += per; fix2++;
                    }
                    if (fix2 > 0)
                    {
                        roster.AddToCounts(el.EquipmentElement, -fix2);
                        roster.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), fix2);
                        done += fix2;
                    }
                }
                if (done == 0) { Log.Player("You cannot pay for even the cheapest mend.", true); return; }
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, paid);
                Log.Player(skipped > 0
                    ? "The smith mended the " + done + " cheapest pieces for " + paid + " gold. " + skipped + " await a fuller purse."
                    : "The smith hammered " + done + " battle-worn pieces back to true for " + paid + " gold.");
                Log.Info("Naprawa lupow: " + done + " szt. za " + paid + ", pominieto " + skipped);
            }
            catch (Exception e) { Log.Error("DoMendLoot", e); }
        }

        // ------------------------------------------------- naprawa NA SZTUKI
        // Jeff: "brakuje opcji wyboru, ktory przedmiot chce naprawic" - lista
        // uszkodzonych rzeczy, kazda z wlasna wycena; kowal za zloto ALBO
        // wlasnorecznie za materialy (dokladnie wypisane), stamine i skill.

        private static float MendShare(EquipmentElement el)
        {
            float pm = el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f;
            if (pm < 0f) pm = 0f; if (pm > 1f) pm = 1f;
            return 1f - pm;                                    // ile zniszczenia trzeba nadrobic
        }

        private static List<Recipes.Part> SelfMendParts(EquipmentElement el)
        {
            // Regula Jeffa: NAPRAWIAMY, nie kujemy od nowa. Nawet wrak (1%)
            // bierze najwyzej MendMaterialMaxShare (20%) pelnej receptury,
            // lzejsze uszkodzenia proporcjonalnie mniej. Drobna naprawa
            // potrafi nie zjesc zadnego materialu - tylko czas i pot.
            var list = new List<Recipes.Part>();
            try
            {
                var r = Recipes.For(el.Item);
                float share = Math.Max(0f, Settings.Current.MendMaterialMaxShare) * MendShare(el);
                foreach (var p in r.Parts)
                {
                    int need = (int)Math.Round(p.Count * share);
                    if (need <= 0) continue;
                    list.Add(new Recipes.Part(p.Item, need));
                }
            }
            catch (Exception e) { Log.Error("SelfMendParts", e); }
            return list;
        }

        private static int SelfMendStamina(EquipmentElement el)
        {
            var r = Recipes.For(el.Item);
            return Math.Max(2, (int)(r.Stamina * MendShare(el) * 0.6f) / 2);   // polowa wymagan, w dol
        }

        private static int SelfMendSkill(EquipmentElement el)
        {
            var r = Recipes.For(el.Item);
            return Math.Max(0, r.SkillNeeded - 10);
        }

        private static bool MendPickCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                int n, c; ScanBattleWorn(out n, out c);
                int worn = 0;
                try
                {
                    var eq = Hero.MainHero.BattleEquipment;
                    for (int i = 0; i < 12; i++)
                        if (eq[i].Item != null && IsBattleWorn(eq[i].Item, eq[i].ItemModifier)) worn++;
                }
                catch { }
                if (n + worn == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}Nothing damaged on your back or in your bags."); return true; }
                args.Tooltip = new TextObject("{=!}{N} damaged in your bags, {W} on your back. Choose one - the smith's price or your own hands.")
                    .SetTextVariable("N", n).SetTextVariable("W", worn);
                return true;
            }
            catch (Exception e) { Log.Error("MendPickCondition", e); return false; }
        }

        private static void MendPickConsequence(MenuCallbackArgs args)
        {
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                var found = new List<EquipmentElement>();
                var slots = new List<int>();                      // -1 = z torby, >=0 = zalozone (slot)
                var elements = new List<InquiryElement>();

                // najpierw to, co na grzbiecie - z wyraznym znacznikiem
                var beq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var ee0 = beq[slot];
                    if (ee0.Item == null || !IsBattleWorn(ee0.Item, ee0.ItemModifier)) continue;
                    int pct0 = Math.Max(1, (int)Math.Round(ee0.ItemModifier.PriceMultiplier * 100f));
                    int smith0 = PieceCost(ee0);
                    var mats0 = SelfMendParts(ee0);
                    var sb0 = new System.Text.StringBuilder();
                    foreach (var p in mats0)
                    {
                        if (p.Item == null) continue;
                        if (sb0.Length > 0) sb0.Append(", ");
                        sb0.Append(p.Count + "x " + p.Item.Name + " (" + Recipes.CountInInventory(p.Item) + ")");
                    }
                    string hint0 = "EQUIPPED - you wear this now.\nCondition " + pct0 + "%" +
                                   "\nSmith: " + smith0 + " gold, " + Settings.Current.MendLootHoursPerPiece.ToString("0.#") + "h" +
                                   "\nYourself: " + (sb0.Length > 0 ? sb0.ToString() : "no materials") +
                                   "\n  + stamina " + SelfMendStamina(ee0) + " (you have " + Forge.Stamina() + ")" +
                                   ", Smithing " + SelfMendSkill(ee0) +
                                   " (yours " + Hero.MainHero.GetSkillValue(DefaultSkills.Crafting) + ")";
                    found.Add(ee0); slots.Add(slot);
                    elements.Add(new InquiryElement(found.Count - 1,
                        "[EQUIPPED] " + ee0.GetModifiedItemName(), null, true, hint0));
                }

                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null || !IsBattleWorn(el.EquipmentElement.Item, el.EquipmentElement.ItemModifier)) continue;
                    var ee = el.EquipmentElement;
                    int pct = Math.Max(1, (int)Math.Round(ee.ItemModifier.PriceMultiplier * 100f));
                    int smith = PieceCost(ee);
                    var mats = SelfMendParts(ee);
                    var sb = new System.Text.StringBuilder();
                    foreach (var p in mats)
                    {
                        if (p.Item == null) continue;
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(p.Count + "x " + p.Item.Name + " (" + Recipes.CountInInventory(p.Item) + ")");
                    }
                    string hint = "Condition " + pct + "%" +
                                  "\nSmith: " + smith + " gold, " + Settings.Current.MendLootHoursPerPiece.ToString("0.#") + "h" +
                                  "\nYourself: " + (sb.Length > 0 ? sb.ToString() : "no materials") +
                                  "\n  + stamina " + SelfMendStamina(ee) + " (you have " + Forge.Stamina() + ")" +
                                  ", Smithing " + SelfMendSkill(ee) +
                                  " (yours " + Hero.MainHero.GetSkillValue(DefaultSkills.Crafting) + ")";
                    found.Add(ee); slots.Add(-1);
                    elements.Add(new InquiryElement(found.Count - 1,
                        ee.GetModifiedItemName() + "  x" + el.Amount, null, true, hint));
                    if (elements.Count >= Settings.Current.MaxItemsListed) break;
                }
                if (elements.Count == 0) { Log.Player("Nothing damaged on your back or in your bags.", true); return; }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Mending Bench", "Every piece has its price - in coin at the smith's rate, or in your own metal and sweat.",
                    elements, true, 1, 1, "Choose", "Leave it",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            int idx = (int)sel[0].Identifier;
                            if (idx >= 0 && idx < found.Count) AskHowToMend(found[idx], slots[idx]);
                        }
                        catch (Exception ex) { Log.Error("MendPick.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("MendPickConsequence", e); }
        }

        private static void AskHowToMend(EquipmentElement ee, int slot)
        {
            try
            {
                int smith = PieceCost(ee);
                var mats = SelfMendParts(ee);
                int stam = SelfMendStamina(ee);
                int skillNeed = SelfMendSkill(ee);
                int mySkill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);

                bool haveMats = true;
                var sb = new System.Text.StringBuilder();
                foreach (var p in mats)
                {
                    if (p.Item == null) continue;
                    int have = Recipes.CountInInventory(p.Item);
                    if (have < p.Count) haveMats = false;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(p.Count + "x " + p.Item.Name + " (" + have + ")");
                }
                bool haveStam = Forge.Stamina() >= stam;
                bool haveSkill = mySkill >= skillNeed;

                var opts = new List<InquiryElement>
                {
                    new InquiryElement(0, "The smith mends it - " + smith + " gold", null,
                        Hero.MainHero.Gold >= smith,
                        Settings.Current.MendLootHoursPerPiece.ToString("0.#") + " hours. Coin does the sweating."),
                    new InquiryElement(1, "Mend it yourself - materials and sweat", null,
                        haveMats && haveStam && haveSkill,
                        "Needs: " + (sb.Length > 0 ? sb.ToString() : "nothing") +
                        "\nStamina " + stam + " (you have " + Forge.Stamina() + ")" +
                        "\nSmithing " + skillNeed + " (yours " + mySkill + ")" +
                        "\n" + Settings.Current.SelfRepairHoursPerPiece.ToString("0.#") + " hours at the anvil" +
                        (haveMats ? "" : "\nYou lack materials.") +
                        (haveStam ? "" : "\nYou are too spent.") +
                        (haveSkill ? "" : "\nBeyond your hand."))
                };

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    ee.GetModifiedItemName().ToString(), "How will it be made whole?",
                    opts, true, 1, 1, "So be it", "Step back",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            int mode = (int)sel[0].Identifier;
                            if (mode == 0)
                                StartTimedWork(Settings.Current.MendLootHoursPerPiece,
                                    "The smith takes the " + ee.Item.Name + " to his bench.",
                                    delegate { DoMendOne(ee, slot, true, smith, null, 0); });
                            else
                                StartTimedWork(Settings.Current.SelfRepairHoursPerPiece,
                                    "You lay the " + ee.Item.Name + " on the anvil and set to work.",
                                    delegate { DoMendOne(ee, slot, false, 0, mats, stam); });
                        }
                        catch (Exception ex) { Log.Error("AskHowToMend.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("AskHowToMend", e); }
        }

        private static void DoMendOne(EquipmentElement ee, int slot, bool bySmith, int gold, List<Recipes.Part> mats, int stamina)
        {
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                if (slot < 0 && roster.FindIndexOfElement(ee) < 0) { Log.Player("The piece is no longer in your bags.", true); return; }
                if (slot >= 0)
                {
                    var cur = Hero.MainHero.BattleEquipment[slot];
                    if (cur.Item != ee.Item) { Log.Player("You no longer wear that piece.", true); return; }
                }
                if (bySmith)
                {
                    if (Hero.MainHero.Gold < gold) { Log.Player("Your purse came up short.", true); return; }
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, gold);
                }
                else
                {
                    foreach (var p in mats)
                        if (p.Item != null && Recipes.CountInInventory(p.Item) < p.Count)
                        { Log.Player("Your materials ran short before the work was done.", true); return; }
                    foreach (var p in mats)
                        if (p.Item != null) roster.AddToCounts(p.Item, -p.Count);
                    Forge.SpendStamina(stamina);
                    Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, 30 + 20 * Recipes.Grade(ee.Item));
                }
                if (slot >= 0)
                {
                    Hero.MainHero.BattleEquipment[slot] = new EquipmentElement(ee.Item);
                    if (ArmouryBehavior.Instance != null) ArmouryBehavior.Instance.ResetSlotCondition(slot);
                }
                else
                {
                    roster.AddToCounts(ee, -1);
                    roster.AddToCounts(new EquipmentElement(ee.Item), 1);
                }
                Log.Player(ee.Item.Name + " is whole again" + (bySmith ? " - " + gold + " gold well spent." : " - your own work."));
                Log.Info("Naprawa sztuki: " + ee.Item.StringId + (bySmith ? " kowal " + gold : " wlasna"));
            }
            catch (Exception e) { Log.Error("DoMendOne", e); }
        }

        private static bool OrdersCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            if (!Settings.Current.ArmourOrdersEnabled) return false;
            int n = Orders.CountAt(Settlement.CurrentSettlement);
            if (n > 0) args.Tooltip = new TextObject("{=!}" + n + " orders in the book.");
            return true;
        }

        private static bool LeaveCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        /// <summary>Naglowek menu - od razu widac, na czym stoisz.</summary>
        private static void OnMenuInit(MenuCallbackArgs args)
        {
            try
            {
                int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
                int stam = Forge.Stamina();
                var text = new TextObject("{=!}The forge is hot and the anvil is free.\n \nSmithing {SKILL}   Stamina {STAM}\n \n" +
                                          "Plain work is within any hand; the finer harness waits on skill. " +
                                          "The smith takes his fee for the use of the fire.");
                text.SetTextVariable("SKILL", skill);
                text.SetTextVariable("STAM", stam);
                MBTextManager.SetTextVariable("ARMOURY_HEAD", text, false);
                args.MenuContext.GameMenu.GetText().SetTextVariable("SKILL", skill);
            }
            catch (Exception e) { Log.Error("OnMenuInit", e); }
        }

        // ---------------------------------------------------------- tempo pracy
        private static void AskTempo(ItemObject item)
        {
            try
            {
                var r = Recipes.For(item);
                var s = Settings.Current;
                float baseDays = r.Tier * s.DaysPerTier;

                var opts = new List<InquiryElement>
                {
                    new InquiryElement(0, "Hastily - " + (baseDays * s.TempoHastyTime).ToString("0.#") + " days", null, true,
                        "Half the time. Double the risk of ruining it, and almost no chance of fine work."),
                    new InquiryElement(1, "At a steady pace - " + baseDays.ToString("0.#") + " days", null, true,
                        "The honest way."),
                    new InquiryElement(2, "With care - " + (baseDays * s.TempoCarefulTime).ToString("0.#") + " days", null, true,
                        "Half again as long. Half the risk, and twice the chance of a fine piece.")
                };

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "How will you work?",
                    item.Name + ". Forge fee " + Forge.ForgeFee(r) + " gold. Materials and stamina are spent now; " +
                    "the piece is finished only when the work is done - and only while you remain in this settlement.",
                    opts, true, 1, 1, "Set to work", "Step back",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            int tempo = (int)sel[0].Identifier;
                            float days;
                            if (!Forge.Begin(item, tempo, out days)) return;
                            ArmouryBehavior.Instance.StartProject(item, tempo, days, Settlement.CurrentSettlement);
                            Log.Player("You set to work on " + item.Name + ". " + days.ToString("0.#") +
                                       " days at the anvil, and you must stay here to see it through.");
                        }
                        catch (Exception ex) { Log.Error("AskTempo.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("AskTempo", e); }
        }

        // ---------------------------------------------------------- przetapianie
        private static bool SmeltCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Craft;
            return Settings.Current.CraftingEnabled;
        }

        private static void SmeltConsequence(MenuCallbackArgs args)
        {
            try
            {
                var elements = new List<InquiryElement>();
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    var item = el.EquipmentElement.Item;
                    if (item == null || el.Amount <= 0) continue;
                    if (!item.HasArmorComponent && item.ItemType != ItemObject.ItemTypeEnum.Shield) continue;
                    if (!Recipes.IsMetalwork(item)) continue;   // tkanina i skora nie ida do tygla
                    var r = Recipes.For(item);
                    int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
                    float share = MathF.Min(0.9f, Settings.Current.SmeltingReturnShare + skill * Settings.Current.SmeltingSkillBonus);
                    var y = Recipes.SmeltYield(r, share);
                    var sb = new System.Text.StringBuilder();
                    foreach (var p in y) { if (sb.Length > 0) sb.Append(", "); sb.Append(p.Count + "x " + p.Item.Name); }
                    elements.Add(new InquiryElement(item, item.Name + "  (x" + el.Amount + ")", null, true,
                        "Yields about " + (sb.Length > 0 ? sb.ToString() : "scrap") +
                        "\nStamina " + (r.Stamina / 2)));
                }
                if (elements.Count == 0) { Log.Player("Only metalwork goes into the crucible - cloth and leather are no business of the furnace."); return; }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Crucible", "What goes into the fire? Better Smithing recovers more of the metal.",
                    elements, true, 1, 1, "Break it down", "Leave it",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            var item = sel[0].Identifier as ItemObject;
                            if (item == null) return;
                            int tier = Recipes.Grade(item);
                            StartTimedWork(Math.Max(0.5f, tier * Settings.Current.SmeltHoursPerTier),
                                "The crucible glows around the " + item.Name + ".",
                                delegate { Forge.Smelt(item); });
                        }
                        catch (Exception ex) { Log.Error("SmeltSelected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("SmeltConsequence", e); }
        }

        // ------------------------------------------------ wzor zdjety z gotowej sztuki
        // Jeff: "jak zdobede luk, to widze go i moge skopiowac - rozlozyc jako
        // wzor, ale wtedy go trace". Kolejka nauki idzie najtanszym-najpierw;
        // TO omija kolejke - sam wybierasz, co chcesz umiec, ale placisz sztuka.
        private static bool TakeApartCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Craft;
            var s = Settings.Current;
            if (s == null || !s.CraftingEnabled || !s.TakeApartEnabled) return false;
            args.Tooltip = new TextObject("{=!}Cut a finished piece apart on the bench and draw out how it was made. The piece is destroyed. Only patterns you do not already know.");
            return true;
        }

        /// <summary>Wymagana Smithing i szansa odczytania wzoru z rzeczy danego tieru.</summary>
        private static void ApartOdds(ItemObject item, out int need, out int have, out float chance)
        {
            var s = Settings.Current;
            int tier = Recipes.Grade(item);
            need = Math.Max(0, (tier - 1) * (s != null ? s.SmithingSkillPerTier : 45));
            have = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
            float span = s != null ? MathF.Max(50f, s.TakeApartSkillSpan) : 300f;
            float bas = s != null ? s.TakeApartBaseChance : 0.6f;
            chance = MBMath.ClampFloat(bas + (have - need) / span, 0.05f, 0.95f);
        }

        private static void TakeApartConsequence(MenuCallbackArgs args)
        {
            try
            {
                var elements = new List<InquiryElement>();
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    var item = el.EquipmentElement.Item;
                    if (item == null || el.Amount <= 0) continue;
                    if (!RangedLore.CanLearnFrom(item)) continue;
                    int need, have; float chance;
                    ApartOdds(item, out need, out have, out chance);
                    int tier = Recipes.Grade(item);
                    elements.Add(new InquiryElement(item, item.Name + "  (x" + el.Amount + ")", null, true,
                        "Tier " + tier + " pattern"
                        + "\nSmithing wanted " + need + ", you have " + have
                        + "\nChance to read the pattern: " + ((int)(chance * 100f)) + "%"
                        + "\nThe piece is destroyed either way."));
                }
                if (elements.Count == 0)
                {
                    Log.Player("Nothing in your bags teaches you anything new - you already know how every piece you carry was made.");
                    return;
                }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Copy a Pattern", "Cut it apart, measure it, draw it. You will not get the piece back.",
                    elements, true, 1, 1, "Take it apart", "Leave it",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            var item = sel[0].Identifier as ItemObject;
                            if (item == null) return;
                            int tier = Recipes.Grade(item);
                            StartTimedWork(Math.Max(0.5f, tier * Settings.Current.SmeltHoursPerTier),
                                "You take the " + item.Name + " apart piece by piece, drawing as you go.",
                                delegate { TakeApartApply(item); });
                        }
                        catch (Exception ex) { Log.Error("TakeApartSelected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("TakeApartConsequence", e); }
        }

        private static void TakeApartApply(ItemObject item)
        {
            try
            {
                if (item == null) return;
                var roster = MobileParty.MainParty.ItemRoster;
                if (roster.GetItemNumber(item) <= 0)
                { Log.Player("The piece is gone from your bags - nothing to take apart."); return; }

                int need, have; float chance;
                ApartOdds(item, out need, out have, out chance);
                int tier = Recipes.Grade(item);
                var s = Settings.Current;

                roster.AddToCounts(item, -1);   // rozlozona sztuka przepada tak czy siak

                // odzysk materialu, jesli Jeff wlaczy (domyslnie nic - to nie tygiel)
                try
                {
                    float salv = s != null ? s.TakeApartSalvage : 0f;
                    if (salv > 0f)
                    {
                        var r = Recipes.For(item);
                        var y = Recipes.SmeltYield(r, MBMath.ClampFloat(salv, 0f, 1f));
                        foreach (var pcs in y) if (pcs.Item != null && pcs.Count > 0) roster.AddToCounts(pcs.Item, pcs.Count);
                    }
                }
                catch (Exception ex) { Log.Error("TakeApartSalvage", ex); }

                bool ok = MBRandom.RandomFloat < chance;
                if (ok && RangedLore.Learn(item))
                {
                    Log.Player("You have the whole of it now - the " + item.Name + " will come off your own bench from here on.");
                    Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, Math.Max(10, tier * 15));
                    RangedLore.ReportSchoolOf(item);
                }
                else
                {
                    Log.Player("The " + item.Name + " came apart in your hands before you had the measure of it - the making is still not yours.");
                    Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, Math.Max(5, tier * 5));
                    RangedLore.Study(item, tier);   // przynajmniej cos sie z tego nauczyles
                }
            }
            catch (Exception e) { Log.Error("TakeApartApply", e); }
        }

        // ---------------------------------------------------------- robota w toku
        private static bool ProgressCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Craft;
            var b = ArmouryBehavior.Instance;
            if (b == null || !b.HasProjects) return false;
            args.Tooltip = new TextObject(b.ProjectSummary());
            return true;
        }

        private static void ProgressConsequence(MenuCallbackArgs args)
        {
            try
            {
                var b = ArmouryBehavior.Instance;
                InformationManager.ShowInquiry(new InquiryData("At the Forge",
                    b.ProjectSummary() + "\n\nWork advances only on days you spend in this settlement.",
                    true, false, "Good", "", null, null), true);
            }
            catch (Exception e) { Log.Error("ProgressConsequence", e); }
        }

        // ---------------------------------------------------------- naprawa wlasnoreczna
        private static bool SelfRepairCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                if (!Settings.Current.WearEnabled) return false;
                var b = ArmouryBehavior.Instance;
                if (b == null || b.RepairCost() <= 0) return false;
                args.Tooltip = new TextObject("Metal and sweat instead of coin. Requires the Smithing to have made the piece.");
                return true;
            }
            catch (Exception e) { Log.Error("SelfRepairCondition", e); return false; }
        }

        // ------------------------------------------------- praca wymaga czasu
        private static float _workTarget, _workDone;
        private static Action _workApply;

        private static void StartTimedWork(float hours, string label, Action apply)
        {
            _workTarget = Math.Max(0.25f, hours);
            _workDone = 0f;
            _workApply = apply;
            MBTextManager.SetTextVariable("ARM_WORK_TEXT",
                label + " It will take about " + string.Format("{0:0.#}", _workTarget) + " hours of work.");
            GameMenu.SwitchToMenu("arm_work_wait");
        }

        private static void WorkInit(MenuCallbackArgs args)
        {
            try
            {
                var here = Settlement.CurrentSettlement;
                if (here != null && here.SettlementComponent != null &&
                    !string.IsNullOrEmpty(here.SettlementComponent.WaitMeshName))
                    args.MenuContext.SetBackgroundMeshName(here.SettlementComponent.WaitMeshName);
            }
            catch { }
        }

        private static void WorkTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                if (_workApply == null) return;
                _workDone += (float)dt.ToHours;
                if (_workDone < _workTarget) return;
                var apply = _workApply;
                _workApply = null;
                GameMenu.SwitchToMenu(Menu);
                apply();
            }
            catch (Exception e) { Log.Error("WorkTick", e); }
        }

        private static void SelfRepairConsequence(MenuCallbackArgs args)
        {
            try
            {
                int pieces = ArmouryBehavior.Instance != null ? ArmouryBehavior.Instance.WornPieces() : 0;
                if (pieces == 0) return;
                StartTimedWork(pieces * Settings.Current.SelfRepairHoursPerPiece,
                    "You strip the harness on the anvil and set to work yourself.",
                    delegate { ArmouryBehavior.Instance.RepairAllSelf(); });
            }
            catch (Exception e) { Log.Error("SelfRepairConsequence", e); }
        }

        // ---------------------------------------------------------- naprawa
        private static bool RepairCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                if (!Settings.Current.WearEnabled) return false;
                var b = ArmouryBehavior.Instance;
                if (b == null) return false;
                int cost = b.RepairCost();
                if (cost <= 0) return false;
                args.Tooltip = new TextObject("The smith will make everything sound again for " + cost + " gold.");
                args.IsEnabled = Hero.MainHero.Gold >= cost;
                return true;
            }
            catch (Exception e) { Log.Error("RepairCondition", e); return false; }
        }

        private static void RepairConsequence(MenuCallbackArgs args)
        {
            try
            {
                int pieces = ArmouryBehavior.Instance != null ? ArmouryBehavior.Instance.WornPieces() : 0;
                if (pieces == 0) return;
                StartTimedWork(pieces * Settings.Current.SmithRepairHoursPerPiece,
                    "The smith lays your harness out and mends it piece by piece.",
                    delegate { ArmouryBehavior.Instance.RepairAll(); });
            }
            catch (Exception e) { Log.Error("RepairConsequence", e); }
        }

        // ---------------------------------------------------------- zamawianie
        private static bool CommissionCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Craft;
            // Kucie zbroi robi zakladka Banner Kings w ekranie kuzni - nasze menu tego nie dubluje.
            return Settings.Current.CraftingEnabled && Settings.Current.ForgeArmourEnabled;
        }

        private static void CommissionConsequence(MenuCallbackArgs args)
        {
            try
            {
                var slots = new List<InquiryElement>
                {
                    new InquiryElement(ItemObject.ItemTypeEnum.BodyArmor,  "Body armour", null, true, "Hauberks, plate, brigandine"),
                    new InquiryElement(ItemObject.ItemTypeEnum.HeadArmor,  "Helmets", null, true, "Helms, coifs, caps"),
                    new InquiryElement(ItemObject.ItemTypeEnum.LegArmor,   "Leg armour", null, true, "Greaves, boots"),
                    new InquiryElement(ItemObject.ItemTypeEnum.HandArmor,  "Gauntlets", null, true, "Gloves and gauntlets"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Cape,       "Shoulders and cloaks", null, true, "Pauldrons, mantles"),
                    new InquiryElement(ItemObject.ItemTypeEnum.HorseHarness,"Horse armour", null, true, "Barding for your mount"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Shield,     "Shields", null, true, "Boards and bucklers"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Bow,        "Bows", null, Settings.Current.AllowRangedCrafting, "Warbows and hunting bows"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Crossbow,   "Crossbows", null, Settings.Current.AllowRangedCrafting, "Crossbows and windlasses"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Arrows,     "Arrows", null, Settings.Current.AllowRangedCrafting, "Sheaves of arrows"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Bolts,      "Bolts", null, Settings.Current.AllowRangedCrafting, "Quarrels")
                };

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Forge", "What will you make? Your Smithing decides what you can attempt - and your metal pays for it.",
                    slots, true, 1, 1, "Choose", "Leave",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        var picked = (ItemObject.ItemTypeEnum)sel[0].Identifier;
                        if (picked == ItemObject.ItemTypeEnum.Bow || picked == ItemObject.ItemTypeEnum.Crossbow
                            || picked == ItemObject.ItemTypeEnum.Arrows || picked == ItemObject.ItemTypeEnum.Bolts)
                            ShowRangedTiers(picked, false);
                        else ShowItems(picked);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("CommissionConsequence", e); }
        }

        private static bool FletcherCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Craft;
            return Settings.Current.CraftingEnabled && Settings.Current.AllowRangedCrafting;
        }

        private static void FletcherConsequence(MenuCallbackArgs args)
        {
            try
            {
                var slots = new List<InquiryElement>
                {
                    new InquiryElement(ItemObject.ItemTypeEnum.Bow,      "Bows", null, true, "Warbows and hunting bows - wood, sinew and a good eye"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Crossbow, "Crossbows", null, true, "Crossbows and windlasses - an iron lock in a wooden stock"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Arrows,   "Arrows", null, true, "Fletched in batches - one job yields several sheaves"),
                    new InquiryElement(ItemObject.ItemTypeEnum.Bolts,    "Bolts", null, true, "Quarrels in batches - one job yields several cases")
                };

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Bowyer's Bench", "Wood, horn and sinew. Your Smithing decides what you can attempt - and your stores pay for it.",
                    slots, true, 1, 1, "Choose", "Leave",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        ShowRangedTiers((ItemObject.ItemTypeEnum)sel[0].Identifier, false);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("FletcherConsequence", e); }
        }

        /// <summary>Nie do wykucia w polowej kuzni: machiny, sprzet cwiczebny, smieci testowe.</summary>
        internal static bool BannedRanged(ItemObject it)
        {
            try
            {
                if (it.WeaponComponent == null) return true;
                string sId = (it.StringId ?? "").ToLowerInvariant();
                string nm = it.Name != null ? it.Name.ToString().ToLowerInvariant() : "";
                string[] bans = { "ballista", "catapult", "trebuchet", "boulder", "siege", "practice", "tournament", "dummy", "test_", "_test" };
                foreach (var b in bans)
                    if (sId.Contains(b) || nm.Contains(b)) return true;
                return false;
            }
            catch { return false; }
        }

        internal static void ShowItems(ItemObject.ItemTypeEnum type) { ShowItems(type, false, -1); }
        internal static void ShowItems(ItemObject.ItemTypeEnum type, bool instant) { ShowItems(type, instant, -1); }

        /// <summary>
        /// Wybor tieru dla lukow i kusz (Jeff: "wybieram tier 1 2 3 4 5 i pokazuje
        /// jakie luki moge wykuc") - kazdy tier pokazuje, ile wzorow juz znasz.
        /// </summary>
        internal static void ShowRangedTiers(ItemObject.ItemTypeEnum type, bool instant)
        {
            try
            {
                bool progress = type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow;
                var elements = new List<InquiryElement>();
                for (int t = 1; t <= 6; t++)
                {
                    int known, total;
                    RangedLore.CountTier(type, t, out known, out total);
                    if (total == 0) continue;
                    string label = "Tier " + t + "   (" + (progress ? known + "/" + total + " patterns known" : total + " designs") + ")";
                    string hint = progress
                        ? (known > 0 ? "Pick a design of this tier." : "No pattern of this tier is known yet - keep crafting lower tiers to work them out.")
                        : "Pick a design of this tier.";
                    // tier zawsze do obejrzenia - nieznane wzory widac w srodku na szaro
                    elements.Add(new InquiryElement(t, label, null, true, hint));
                }
                if (elements.Count == 0) { Log.Player("Nothing of that sort is made at any bench.", true); return; }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Bowyer's Bench", "Which grade of work? You start knowing tier 1 patterns - the craft itself teaches you the rest.",
                    elements, true, 1, 1, "Choose", "Leave",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        ShowItems(type, instant, (int)sel[0].Identifier);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("ShowRangedTiers", e); }
        }

        /// <summary>instant=true: kucie od reki (ekran kuzni) - materialy i stamina schodza natychmiast.</summary>
        internal static void ShowItems(ItemObject.ItemTypeEnum type, bool instant, int tierFilter)
        {
            try
            {
                var s = Settings.Current;
                int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
                bool rangedKit = type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow
                              || type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts;
                var candidates = new List<ItemObject>();
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.ItemType != type) continue;
                    // WSZYSTKIE rodzaje strzal i lukow (Jeff) - takze te spoza kramow;
                    // odpada tylko sprzet obleczniczy, cwiczebny i testowy
                    if (item.NotMerchandise && !rangedKit) continue;
                    if (rangedKit && BannedRanged(item)) continue;
                    if (tierFilter > 0 && RangedLore.TierOf(item) != tierFilter) continue;
                    candidates.Add(item);
                }
                // Najpierw to, co uniesiesz, od najprostszej roboty w gore. Reszta ponizej, zeby bylo widac,
                // co czeka na wyzsza umiejetnosc - a nie zeby wygladalo, ze kuznia jest pusta.
                candidates.Sort(delegate (ItemObject a, ItemObject b)
                {
                    bool la = Recipes.For(a).SkillNeeded > skill;
                    bool lb = Recipes.For(b).SkillNeeded > skill;
                    if (la != lb) return la ? 1 : -1;
                    int ta = (int)a.Tier, tb = (int)b.Tier;
                    if (ta != tb) return ta.CompareTo(tb);
                    return a.Value.CompareTo(b.Value);
                });
                int cap = rangedKit ? s.MaxItemsListed * 2 : s.MaxItemsListed;   // strzeleckie: pelniejsza polka
                if (candidates.Count > cap) candidates = candidates.GetRange(0, cap);

                if (candidates.Count == 0)
                {
                    Log.Player("Nothing of that sort in the racks here.", true);
                    return;
                }

                var elements = new List<InquiryElement>();
                foreach (var item in candidates)
                {
                    var r = Recipes.For(item);
                    bool locked = r.SkillNeeded > skill;
                    bool hasMats = Recipes.HasMaterials(r);
                    bool hasStamina = Forge.Stamina() >= r.Stamina;
                    bool legend = Recipes.IsLegendary(item);
                    string legendWhy = null;
                    bool legendLocked = legend && !Forge.LegendAllowed(item, out legendWhy);
                    bool unknown = !RangedLore.KnownOf(item);   // wzor jeszcze nie odkryty - widac, ale szary
                    int fail = (int)(Forge.FailureChance(r) * 100f);

                    // statystyki wyrobu (Jeff: "zeby byl widok 3D luku i statystyki")
                    string stats = "";
                    try
                    {
                        var w = item.WeaponComponent != null ? item.WeaponComponent.PrimaryWeapon : null;
                        if (w != null)
                        {
                            if (type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow)
                                stats = "\nDamage " + w.ThrustDamage + ", missile speed " + w.MissileSpeed + ", accuracy " + w.Accuracy;
                            else if (type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts)
                                stats = "\nDamage " + w.MissileDamage + ", " + w.MaxDataValue + " to the sheaf";
                        }
                    }
                    catch { }

                    string hint = (legend ? "A LEGEND. Only one may ever exist - and the bill is legendary.\n" : "") +
                                  (unknown ? "Pattern NOT yet discovered - the craft itself will teach you (keep making bows and crossbows).\n" : "") +
                                  Recipes.Describe(r) + stats +
                                  "\nStamina " + r.Stamina + " (you have " + Forge.Stamina() + ")" +
                                  "\nSmithing " + r.SkillNeeded + " required (yours: " + skill + ")" +
                                  (locked ? "\nBeyond your hand for now." : "\nRisk of ruining it: " + fail + "%") +
                                  (hasMats ? "" : "\nYou lack materials.") +
                                  (hasStamina ? "" : "\nYou are too spent.") +
                                  (legendLocked ? "\n" + legendWhy : "");
                    string label = (legend ? "LEGEND - " : "") + item.Name + "   (tier " + RangedLore.TierOf(item) + ")" +
                                   (unknown ? "  - pattern unknown" : (locked ? "  - needs Smithing " + r.SkillNeeded : ""));
                    TaleWorlds.Core.ImageIdentifiers.ImageIdentifier pic = null;
                    try { pic = new TaleWorlds.Core.ImageIdentifiers.ItemImageIdentifier(item); } catch { }
                    elements.Add(new InquiryElement(item, label, pic,
                        !locked && hasMats && hasStamina && !legendLocked && !unknown, hint));
                }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "At the Anvil", "Smithing " + skill + ", stamina " + Forge.Stamina() + ". You forge it yourself, from your own metal.",
                    elements, true, 1, 1, "Forge it", "Not today",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            var item = sel[0].Identifier as ItemObject;
                            if (item == null) return;
                            if (instant) Forge.Smith(item);
                            else AskTempo(item);
                        }
                        catch (Exception ex) { Log.Error("ShowItems.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("ShowItems", e); }
        }
    }
}
