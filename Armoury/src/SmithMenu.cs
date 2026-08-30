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
        private const string MendMenu = "armoury_mend";

        /// <summary>Wejscie do warsztatu naprawczego - w podpowiedzi od razu stan calego dobytku.</summary>
        private static bool MendBenchCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                int bags, bagsCost; ScanBattleWorn(out bags, out bagsCost);
                int worn = 0;
                try
                {
                    var eq = Hero.MainHero.BattleEquipment;
                    for (int i = 0; i < 12; i++)
                        if (eq[i].Item != null && IsBattleWorn(eq[i].Item, eq[i].ItemModifier)) worn++;
                }
                catch { }
                int racks = 0, racksCost, cp, cc, dd;
                if (Settings.Current.TroopMendEnabled) ScanTroopWorn(out racks, out racksCost, out cp, out cc, out dd);
                args.Tooltip = new TextObject("{=!}Worn: {W} on your back, {B} in your bags, {R} on the men's racks.")
                    .SetTextVariable("W", worn).SetTextVariable("B", bags).SetTextVariable("R", racks);
                return true;
            }
            catch (Exception e) { Log.Error("MendBenchCondition", e); return true; }
        }

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
            // luczarnia WYPADLA z tego menu (Jeff 27.08: "wywal z forge kucie
            // lukow i strzal - kucie jest w smithy"): luki, kusze, strzaly
            // i belty kuje sie w zakladce CRAFT (ForgeView wstrzykuje je na
            // liste, FletchForge przejmuje robote) - opcja tutaj dublowala to.
            // Kod Fletchera zostaje - CRAFT z niego korzysta.

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

            // JEDNO wejscie dla wszystkich napraw (Jeff: "sprawdz czy opcje maja
            // sens i czy czegos nie wywalic" - piec pozycji naprawczych na glownym
            // menu mylilo, teraz siedza pod warsztatem)
            starter.AddGameMenuOption(Menu, "arm_mend_bench",
                "{=!}The mending bench - your gear and the men's", MendBenchCondition,
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(MendMenu); }, false, 2);

            // tygiel WYPADL z tego menu (Jeff 27.08: "smelt mozna robic
            // w smithy") - przetop zyje w zakladce Smelt ekranu kuzni
            // (SmeltTab z nasza nauka wzorow). Kod zostaje na zapas.

            starter.AddGameMenuOption(Menu, "arm_takeapart",
                "{=!}Take a piece apart to copy its pattern", TakeApartCondition, TakeApartConsequence, false, 4);

            starter.AddGameMenuOption(Menu, "arm_order_kit",
                "{=!}Order kit for the men - the smith procures it", OrderKitCondition, OrderKitConsequence, false, 6);

            // ---- warsztat naprawczy ----
            starter.AddGameMenu(MendMenu,
                "{=!}The mending bench. Coin for the smith's hammer, or your own metal and sweat.",
                OnMenuInit, GameMenu.MenuOverlayType.SettlementWithBoth);

            starter.AddGameMenuOption(MendMenu, "arm_kit_report",
                "{=!}Muster the men's kit - the quartermaster's report", KitReportCondition, KitReportConsequence, false, 0);

            starter.AddGameMenuOption(MendMenu, "arm_mend_pick",
                "{=!}Pick a damaged piece to mend", MendPickCondition, MendPickConsequence, false, 0);

            starter.AddGameMenuOption(MendMenu, "arm_selfrepair",
                "{=!}Mend everything you wear - your own hands", SelfRepairCondition, SelfRepairConsequence, false, 1);

            starter.AddGameMenuOption(MendMenu, "arm_repair",
                "{=!}Mend everything you wear - the smith's price", RepairCondition, RepairConsequence, false, 1);

            starter.AddGameMenuOption(MendMenu, "arm_mend_loot",
                "{=!}Restore ALL battle-worn loot from your bags", MendLootCondition, MendLootConsequence, false, 2);

            starter.AddGameMenuOption(MendMenu, "arm_mend_troops",
                "{=!}Send the men's worn gear to the smith", MendTroopsCondition, MendTroopsConsequence, false, 3);

            starter.AddGameMenuOption(MendMenu, "arm_mend_back",
                "{=!}Back to the forge", LeaveCondition,
                delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Menu); }, true, 9);

            // robota wymaga czasu: jedno wspolne menu oczekiwania dla napraw i przetopu
            starter.AddWaitGameMenu("arm_work_wait",
                "{=!}{ARM_WORK_TEXT}",
                WorkInit, delegate (MenuCallbackArgs a) { return true; }, null, WorkTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption,
                GameMenu.MenuOverlayType.SettlementWithBoth);
            starter.AddGameMenuOption("arm_work_wait", "arm_work_stop",
                "{=!}Put the work aside (nothing finished, nothing paid)",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                // ZAKAZ SwitchToMenu z opcji menu OCZEKIWANIA (CTD, CLAUDE.md),
                // a ExitToLast NISZCZY MenuContext (stosu menu nie ma - w miescie
                // laduje sie w town_outside). Opcja tylko podnosi flage,
                // przelacza WorkTick - SwitchToMenu z ticka to wzorzec vanilla.
                // RATUNEK Z DEADLOCKA (Jeff 30.08, White Harbor): po wczytaniu
                // save'a w srodku wait-menu init ze StartWait nie biegnie,
                // czas stoi na Stop i flaga czekalaby na tick w nieskonczonosc
                // - ruszamy zegar sami, tick przelaczy w nastepnej klatce
                delegate (MenuCallbackArgs a)
                {
                    _workApply = null; _workLeave = true;
                    try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay; a.MenuContext.GameMenu.StartWait(); } catch { }
                }, true, 9);

            // WYBOR CZELADNIKA (Jeff 30.08: "wybieram postac, ona uczy sie
            // kowalstwa, dostaje XP i mi pomaga") - dotad pomocnik byl
            // dobierany automatycznie (najlepszy kowal druzyny)
            starter.AddGameMenuOption(Menu, "arm_helper_pick",
                "{=!}Choose your helper at the bellows", HelperPickCondition, HelperPickConsequence, false, 8);

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
                // flaga -> ProjWaitTick przelaczy na Menu (SwitchToMenu z ticka
                // to wzorzec vanilla; z opcji wait-menu ZAKAZ, a ExitToLast
                // niszczy MenuContext i laduje w town_outside). Zegar ruszamy
                // sami - ratunek z deadlocka po load w srodku menu (White Harbor)
                delegate (MenuCallbackArgs a)
                {
                    _projLeave = true;
                    try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay; a.MenuContext.GameMenu.StartWait(); } catch { }
                }, true, 9);
        }

        // ------------------------------------------------- czeladnik przy miechu
        private static bool HelperPickCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                if (!Settings.Current.CompanionHelperEnabled) return false;
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null) return false;
                for (int i = 0; i < roster.Count; i++)
                {
                    var h = roster.GetElementCopyAtIndex(i).Character;
                    if (h != null && h.IsHero && h.HeroObject != Hero.MainHero) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static void HelperPickConsequence(MenuCallbackArgs args)
        {
            try
            {
                var cur = ArmouryBehavior.SmithHelperId;
                var list = new System.Collections.Generic.List<TaleWorlds.Core.InquiryElement>();
                list.Add(new TaleWorlds.Core.InquiryElement("auto",
                    "Best available (auto)" + (string.IsNullOrEmpty(cur) || cur == "auto" ? "  [current]" : ""), null));
                list.Add(new TaleWorlds.Core.InquiryElement("none",
                    "Work alone - no helper" + (cur == "none" ? "  [current]" : ""), null));
                var roster = MobileParty.MainParty.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var c = roster.GetElementCopyAtIndex(i).Character;
                    var h = c != null ? c.HeroObject : null;
                    if (h == null || h == Hero.MainHero || !h.IsAlive) continue;
                    int sk = h.GetSkillValue(TaleWorlds.Core.DefaultSkills.Crafting);
                    list.Add(new TaleWorlds.Core.InquiryElement(h.StringId,
                        h.Name + " (Smithing " + sk + ")" + (cur == h.StringId ? "  [current]" : "")
                        + (h.IsWounded ? "  [wounded]" : ""), null));
                }
                MBInformationManager.ShowMultiSelectionInquiry(new TaleWorlds.Core.MultiSelectionInquiryData(
                    "The helper at the bellows",
                    "Who works the forge with you? An apprentice learns the craft from every job; a skilled smith spares your arms and shortens the work.",
                    list, true, 1, 1, "Choose", "Back",
                    delegate (System.Collections.Generic.List<TaleWorlds.Core.InquiryElement> picked)
                    {
                        try
                        {
                            if (picked == null || picked.Count == 0) return;
                            var id = picked[0].Identifier as string ?? "auto";
                            ArmouryBehavior.SmithHelperId = id;
                            string who = id == "auto" ? "the best smith available"
                                       : id == "none" ? "no one - you work alone"
                                       : (Hero.FindFirst(x => x.StringId == id) != null
                                          ? Hero.FindFirst(x => x.StringId == id).Name.ToString() : id);
                            Log.Player("At the bellows from now on: " + who + ".", true);
                            Log.Info("Czeladnik wybrany: " + id);
                        }
                        catch (Exception e) { Log.Error("HelperPick.Affirm", e); }
                    }, null), true);
            }
            catch (Exception e) { Log.Error("HelperPickConsequence", e); }
        }

        // ------------------------------------------------- czekanie przy projektach
        private static float _projInitialHours;
        private static bool _projLeave;   // opcja "Step away" podnosi, ProjWaitTick przelacza

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
                _projLeave = false;
                var b = ArmouryBehavior.Instance;
                _projInitialHours = b != null ? Math.Max(1f, b.ProjectHoursLeftHere(Settlement.CurrentSettlement)) : 1f;
                // JEDNA PRAWDA GODZIN (Jeff 29.08: "inne godziny w smith, inne jak
                // czekam - balagan"): to jest SUMA wszystkich zlecen u tego kowala
                // (pracuje po kolei), a robota idzie takze bez ciebie
                MBTextManager.SetTextVariable("ARM_PROJ_TEXT",
                    "The smith works the jobs one by one - about " +
                    ((int)Math.Ceiling(_projInitialHours)) + " hours until ALL your work here is done. " +
                    "He needs no watching: leave, and the clock still runs.");
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Log.Error("ProjWaitInit", e); }
        }

        private static void ProjWaitTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                if (_projLeave) { _projLeave = false; GameMenu.SwitchToMenu(Menu); return; }
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

        /// <summary>Miniatura 3D przedmiotu do list wyboru - po opisie nie widac, co to za sztuka.</summary>
        internal static TaleWorlds.Core.ImageIdentifiers.ImageIdentifier ItemPic(ItemObject item)
        {
            try { return item != null ? new TaleWorlds.Core.ImageIdentifiers.ItemImageIdentifier(item) : null; }
            catch { return null; }
        }

        // ------------------------------------------------- naprawa lupow bitewnych
        // KAZDY przedmiot ze stanem obnizajacym wartosc - lupy Spoils (rl_looted_*),
        // wraki, rdza, pekniecia, nasze zuzycie - kowal doprowadza do stanu fabrycznego.

        /// <summary>Porzadek typow na listach (Jeff 29.08: "sortuj po typach -
        /// wszystkie luki, potem strzaly, zeby nie szukac wszedzie"): luki,
        /// strzaly, kusze, belty, bron reczna, tarcze, pancerz od helmu w dol,
        /// kon, rzad.</summary>
        internal static int TypeRank(ItemObject it)
        {
            if (it == null) return 99;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.Bow: return 0;
                case ItemObject.ItemTypeEnum.Arrows: return 1;
                case ItemObject.ItemTypeEnum.Crossbow: return 2;
                case ItemObject.ItemTypeEnum.Bolts: return 3;
                case ItemObject.ItemTypeEnum.OneHandedWeapon: return 4;
                case ItemObject.ItemTypeEnum.TwoHandedWeapon: return 5;
                case ItemObject.ItemTypeEnum.Polearm: return 6;
                case ItemObject.ItemTypeEnum.Thrown: return 7;
                case ItemObject.ItemTypeEnum.Shield: return 8;
                case ItemObject.ItemTypeEnum.HeadArmor: return 9;
                case ItemObject.ItemTypeEnum.BodyArmor: return 10;
                case ItemObject.ItemTypeEnum.LegArmor: return 11;
                case ItemObject.ItemTypeEnum.HandArmor: return 12;
                case ItemObject.ItemTypeEnum.Cape: return 13;
                case ItemObject.ItemTypeEnum.Horse: return 14;
                case ItemObject.ItemTypeEnum.HorseHarness: return 15;
                default: return 20;
            }
        }

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
                int stores = 0;
                try
                {
                    var st = QuartermasterLaw.DteArmory();
                    if (st != null)
                        for (int i = 0; i < st.Count; i++)
                        {
                            var e2 = st.GetElementCopyAtIndex(i);
                            if (e2.Amount > 0 && e2.EquipmentElement.Item != null
                                && IsBattleWorn(e2.EquipmentElement.Item, e2.EquipmentElement.ItemModifier)) stores += e2.Amount;
                        }
                }
                catch { }
                if (n + worn + stores == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}Nothing damaged on your back, in your bags or in the stores."); return true; }
                args.Tooltip = new TextObject("{=!}{N} damaged in your bags, {W} on your back, {S} in the company stores. Choose one - the smith's price or your own hands.")
                    .SetTextVariable("N", n).SetTextVariable("W", worn).SetTextVariable("S", stores);
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
                        "[EQUIPPED] " + ee0.GetModifiedItemName(), ItemPic(ee0.Item), true, hint0));
                }

                // NAJPIERW ZBIERZ WSZYSTKO, POTEM SORTUJ, NA KONCU TNIJ (Jeff
                // 29.08: "pancerze SA uszkodzone a lista ich nie ma!" - limit
                // MaxItemsListed ucinal roster ZANIM sort podniosl pancerze,
                // wiec masa zbitych broni z wielkich bitew zapychala liste).
                // Kazdy item w osobnym try - jedna zla sztuka nie ucina reszty.
                for (int i = 0; i < roster.Count; i++)
                {
                    try
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
                            ee.GetModifiedItemName() + "  x" + el.Amount, ItemPic(ee.Item), true, hint));
                    }
                    catch (Exception exi) { Log.Error("MendPick.bagItem", exi); }
                }

                // MAGAZYN WOJSKA - zbite sztuki zolnierzy ([STORES], slot=-2)
                var armory = QuartermasterLaw.DteArmory();
                if (armory != null)
                    for (int i = 0; i < armory.Count; i++)
                    {
                        try
                        {
                            var el = armory.GetElementCopyAtIndex(i);
                            var ee = el.EquipmentElement;
                            if (ee.Item == null || el.Amount <= 0 || !IsBattleWorn(ee.Item, ee.ItemModifier)) continue;
                            int pct2 = Math.Max(1, (int)Math.Round(ee.ItemModifier.PriceMultiplier * 100f));
                            int smith2 = PieceCost(ee);
                            string hint2 = "COMPANY STORES - the men's kit.\nCondition " + pct2 + "%" +
                                           "\nSmith: " + smith2 + " gold, " + Settings.Current.MendLootHoursPerPiece.ToString("0.#") + "h";
                            found.Add(ee); slots.Add(-2);
                            elements.Add(new InquiryElement(found.Count - 1,
                                "[STORES] " + ee.GetModifiedItemName() + "  x" + el.Amount, ItemPic(ee.Item), true, hint2));
                        }
                        catch (Exception exi) { Log.Error("MendPick.storeItem", exi); }
                    }

                if (elements.Count == 0) { Log.Player("Nothing damaged on your back, in your bags or in the stores.", true); return; }

                // sort po typach: luki razem, strzaly razem, PANCERZE dalej -
                // wszystko jeszcze przed cieciem limitu
                elements.Sort((a, b) =>
                {
                    var ia = found[(int)a.Identifier].Item; var ib = found[(int)b.Identifier].Item;
                    int r = TypeRank(ia).CompareTo(TypeRank(ib));
                    if (r != 0) return r;
                    r = ib.Tier.CompareTo(ia.Tier);
                    if (r != 0) return r;
                    return string.CompareOrdinal(ia.StringId, ib.StringId);
                });

                // limit z GWARANCJA PARYTETU TYPOW (Jeff x3: "nie moge naprawic
                // pancerzy!" - zwykly limit ucinal koniec listy, a pancerze
                // sortuja sie ZA broniami, wiec zawsze wypadaly). Kazdy obecny
                // typ dostaje swoja pule miejsc; dopiero reszta idzie nadwyzkom.
                int cap = Math.Max(24, Settings.Current.MaxItemsListed);
                if (elements.Count > cap)
                {
                    var byRank = new Dictionary<int, List<InquiryElement>>();
                    var rankOrder = new List<int>();
                    foreach (var el in elements)
                    {
                        int rank = TypeRank(found[(int)el.Identifier].Item);
                        List<InquiryElement> g;
                        if (!byRank.TryGetValue(rank, out g)) { byRank[rank] = g = new List<InquiryElement>(); rankOrder.Add(rank); }
                        g.Add(el);
                    }
                    int perType = Math.Max(4, cap / Math.Max(1, rankOrder.Count));
                    var final = new List<InquiryElement>();
                    foreach (var r in rankOrder)
                    {
                        var g = byRank[r];
                        for (int i = 0; i < g.Count && i < perType && final.Count < cap; i++) final.Add(g[i]);
                    }
                    // wolne miejsca dobieramy nadwyzkami w kolejnosci typow
                    foreach (var r in rankOrder)
                    {
                        if (final.Count >= cap) break;
                        var g = byRank[r];
                        for (int i = perType; i < g.Count && final.Count < cap; i++) final.Add(g[i]);
                    }
                    Log.Info("MendPick: lista " + elements.Count + " pozycji, pokazane " + final.Count
                             + " (po " + perType + "/typ, typow " + rankOrder.Count + ").");
                    elements.Clear();
                    elements.AddRange(final);
                }

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
                // slot -2 = magazyn wojska: naprawa zdejmuje zbita sztuke ze stanu
                // i odklada czysta na stan (zolnierze dostana ja przy przydziale)
                if (slot == -2)
                {
                    var store = QuartermasterLaw.DteArmory();
                    if (store == null || store.FindIndexOfElement(ee) < 0)
                    { Log.Player("The piece is no longer in the stores.", true); return; }
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
                            if (p.Item != null) MobileParty.MainParty.ItemRoster.AddToCounts(p.Item, -p.Count);
                        Forge.SpendStamina(stamina);
                        Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, 30 + 20 * Recipes.Grade(ee.Item));
                    }
                    store.AddToCounts(ee, -1);
                    store.AddToCounts(new EquipmentElement(ee.Item), 1);
                    Log.Player(ee.Item.Name + " is whole again and back in the company stores.");
                    Log.Info("Naprawa sztuki (magazyn): " + ee.Item.StringId + (bySmith ? " kowal " + gold : " wlasna"));
                    return;
                }

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

        // ------------------------------------------------- naprawa sprzetu WOJSKA
        // Jeff: "zolnierz w pancerzu 3% biega prawie golym - musi byc info,
        // ze u kowala naprawisz sprzet wojska, i logiczny koszt, nie majatek".
        // Zbrojownia DTE trzyma modyfikatory stanu, a nasz ConditionScaling
        // skaluje pancerz KAZDEGO, kto nosi zuzyta sztuke - takze zolnierza.
        // Kowal bierze polki hurtem: stawka TroopMendCostFactor od ceny
        // naprawy, najtansze najpierw, koszt wypisany Z GORY w podpowiedzi.

        /// <summary>
        /// Cena naprawy JEDNEJ sztuki wojska: wrak (1%) = TroopMendWreckShare
        /// wartosci (Jeff: "max 10%"), lzejsze zuzycie proporcjonalnie mniej;
        /// discount to rabat hurtowy juz policzony z calej roboty.
        /// </summary>
        private static int TroopPieceCost(EquipmentElement el, float discount)
        {
            float pm = el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f;
            if (pm < 0f) pm = 0f; if (pm > 1f) pm = 1f;
            float share = MathF.Max(0.01f, Settings.Current.TroopMendWreckShare);
            return Math.Max(1, (int)(el.Item.Value * (1f - pm) * share * (1f - discount)));
        }

        /// <summary>Rabat hurtowy: kazda sztuka na robocie zbija procent z rachunku, do pulapu.</summary>
        private static float TroopBulkDiscount(int pieces)
        {
            var s = Settings.Current;
            float pct = MathF.Min(MathF.Max(0f, s.TroopMendBulkDiscountMax),
                                  pieces * MathF.Max(0f, s.TroopMendBulkDiscountPP));
            return MBMath.ClampFloat(pct / 100f, 0f, 0.9f);
        }

        /// <summary>Ile zuzytych sztuk lezy w zbrojowni wojska i ile kosztuje naprawa (calosc / na ile stac).</summary>
        private static void ScanTroopWorn(out int pieces, out int cost, out int canPieces, out int canCost, out int discountPct)
        {
            pieces = 0; cost = 0; canPieces = 0; canCost = 0; discountPct = 0;
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null || QuartermasterEscrow.Active) return;
                var worn = new List<EquipmentElement>();
                int total = 0;
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var ee = el.EquipmentElement;
                    if (ee.Item == null || !IsBattleWorn(ee.Item, ee.ItemModifier)) continue;
                    for (int k = 0; k < el.Amount; k++) worn.Add(ee);
                    total += el.Amount;
                }
                float discount = TroopBulkDiscount(total);
                discountPct = (int)(discount * 100f);
                var costs = new List<int>();
                foreach (var ee in worn) costs.Add(TroopPieceCost(ee, discount));
                costs.Sort();
                int gold = Hero.MainHero.Gold;
                foreach (var c in costs)
                {
                    pieces++; cost += c;
                    if (canCost + c <= gold) { canPieces++; canCost += c; }
                }
            }
            catch (Exception e) { Log.Error("ScanTroopWorn", e); }
        }

        private static bool MendTroopsCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var s = Settings.Current;
                if (s == null || !s.TroopMendEnabled) return false;
                if (QuartermasterLaw.DteArmory() == null) return false;      // bez DTE nie ma zbrojowni
                int all, allCost, can, canCost, disc;
                ScanTroopWorn(out all, out allCost, out can, out canCost, out disc);
                if (all == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}The men's racks hold nothing worn - every piece is sound."); return true; }
                if (can == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}{ALL} worn pieces on the racks, {COST} gold for the lot (bulk discount {D}%) - you cannot afford even the cheapest.").SetTextVariable("ALL", all).SetTextVariable("COST", allCost).SetTextVariable("D", disc); return true; }
                if (can < all)
                    args.Tooltip = new TextObject("{=!}{ALL} worn pieces on the men's racks ({COST} gold for the lot, bulk discount {D}%). For your purse the smith will mend the {CAN} cheapest for {CANCOST}.")
                        .SetTextVariable("ALL", all).SetTextVariable("COST", allCost).SetTextVariable("CAN", can).SetTextVariable("CANCOST", canCost).SetTextVariable("D", disc);
                else
                    args.Tooltip = new TextObject("{=!}{ALL} worn pieces on the men's racks. The smith and his apprentices will make them whole for {COST} gold (bulk discount {D}%).")
                        .SetTextVariable("ALL", all).SetTextVariable("COST", allCost).SetTextVariable("D", disc);
                return true;
            }
            catch (Exception e) { Log.Error("MendTroopsCondition", e); return false; }
        }

        private static void MendTroopsConsequence(MenuCallbackArgs args)
        {
            try
            {
                var s = Settings.Current;
                int all, allCost, can, canCost, disc;
                ScanTroopWorn(out all, out allCost, out can, out canCost, out disc);
                if (can == 0) return;
                float hours = Math.Min(MathF.Max(1f, s.TroopMendMaxHours), can * s.MendLootHoursPerPiece);
                StartTimedWork(hours,
                    "The smith clears his benches and sets every apprentice on the men's gear.",
                    delegate { DoMendTroops(); });
            }
            catch (Exception e) { Log.Error("MendTroopsConsequence", e); }
        }

        private static void DoMendTroops()
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var worn = new List<ItemRosterElement>();
                int total = 0;
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var ee = el.EquipmentElement;
                    if (ee.Item != null && IsBattleWorn(ee.Item, ee.ItemModifier)) { worn.Add(el); total += el.Amount; }
                }
                if (worn.Count == 0) return;
                // rabat liczony z CALEJ roboty - ta sama liczba co w podpowiedzi
                float discount = TroopBulkDiscount(total);
                worn.Sort((a, b) => TroopPieceCost(a.EquipmentElement, discount).CompareTo(TroopPieceCost(b.EquipmentElement, discount)));
                int paid = 0, done = 0, skipped = 0;
                foreach (var el in worn)
                {
                    int per = TroopPieceCost(el.EquipmentElement, discount);
                    int fix2 = 0;
                    for (int k = 0; k < el.Amount; k++)
                    {
                        if (Hero.MainHero.Gold - paid - per < 0) { skipped += el.Amount - k; break; }
                        paid += per; fix2++;
                    }
                    if (fix2 > 0)
                    {
                        armory.AddToCounts(el.EquipmentElement, -fix2);
                        armory.AddToCounts(new EquipmentElement(el.EquipmentElement.Item), fix2);
                        done += fix2;
                    }
                }
                if (done == 0) { Log.Player("You cannot pay for even the cheapest mend.", true); return; }
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, paid);
                Log.Player(skipped > 0
                    ? "The men's " + done + " cheapest pieces are whole again for " + paid + " gold. " + skipped + " await a fuller purse."
                    : "The men's racks are mended: " + done + " pieces made whole for " + paid + " gold.");
                Log.Info("Naprawa zbrojowni wojska: " + done + " szt. za " + paid + ", pominieto " + skipped);
            }
            catch (Exception e) { Log.Error("DoMendTroops", e); }
        }

        // ------------------------------------------------- przeglad sprzetu WOJSKA
        // Jeff: "gdzie moge sprawdzic ogolny poziom sprzetu wojska - srednia
        // jakosc helmow, korpusow, nog, broni, lukow, koni". Kwatermistrz
        // robi przeglad calej zbrojowni: sztuki, braki, sredni stan i tier
        // per typ, do tego rachunek za naprawe calosci.

        private static bool KitReportCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            if (QuartermasterLaw.DteArmory() == null) return false;
            args.Tooltip = new TextObject("{=!}Counts, shortages, average condition and grade of every kind of kit the men own.");
            return true;
        }

        private static void KitReportConsequence(MenuCallbackArgs args)
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var needs = QuartermasterLaw.CountNeeds();
                var sb = new System.Text.StringBuilder();
                foreach (var type in QuartermasterLaw.KitTypes)
                {
                    int count = 0, worn = 0;
                    float condSum = 0f, tierSum = 0f;
                    bool wearable = false;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.ItemType != type || el.Amount <= 0) continue;
                        count += el.Amount;
                        tierSum += Recipes.Grade(it) * el.Amount;
                        if (!ArmouryBehavior.NoWear(it))
                        {
                            wearable = true;
                            var m = el.EquipmentElement.ItemModifier;
                            float pm = m != null ? m.PriceMultiplier : 1f;
                            if (pm < 0f) pm = 0f; if (pm > 1f) pm = 1f;
                            condSum += pm * el.Amount;
                            if (m != null && pm < 0.999f) worn += el.Amount;
                        }
                    }
                    int need = QuartermasterLaw.WornFor(type, needs);
                    if (count == 0 && need <= 0) continue;
                    sb.Append(type).Append(": ").Append(count).Append(" on hand");
                    if (need > 0) sb.Append(" / ").Append(need).Append(" needed").Append(count < need ? "  SHORT " + (need - count) : "");
                    if (count > 0)
                    {
                        sb.Append(", grade ").Append((tierSum / count).ToString("0.0"));
                        if (wearable)
                        {
                            sb.Append(", condition ").Append((int)(condSum / count * 100f)).Append("%");
                            if (worn > 0) sb.Append(" (").Append(worn).Append(" worn)");
                        }
                    }
                    sb.Append("\n");
                }
                int all, allCost, can, canCost, disc;
                ScanTroopWorn(out all, out allCost, out can, out canCost, out disc);
                if (all > 0)
                    sb.Append("\nThe smith will mend all ").Append(all).Append(" worn pieces for ")
                      .Append(allCost).Append(" gold (bulk discount ").Append(disc).Append("%).");
                else
                    sb.Append("\nEvery piece on the racks is sound.");

                InformationManager.ShowInquiry(new InquiryData("The Quartermaster's Muster",
                    sb.ToString(), true, false, "Good", "", null, null), true);
            }
            catch (Exception e) { Log.Error("KitReportConsequence", e); }
        }

        // ------------------------------------------------- zamowienie brakow WOJSKA
        // Jeff: "mam info ze brakuje wojsku np. throw - czy moge zamowic u kowala
        // brakujace rzeczy za oplata rynkowa, tier 1 tyle, tier 2 tyle". Kowal
        // sprowadza PROSTE sztuki wybranego typu i tieru wprost na polki zbrojowni;
        // cena = wartosc rynkowa x jego marza, wypisana przy kazdym tierze.

        private static bool OrderKitCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var s = Settings.Current;
                if (s == null || !s.TroopOrderEnabled) return false;
                if (QuartermasterLaw.DteArmory() == null) return false;
                var shortages = QuartermasterLaw.ShortageLines();
                args.Tooltip = shortages.Count > 0
                    ? new TextObject("{=!}The men go short: {LIST}. The smith will procure plain pieces of any tier for market worth plus his fee.")
                        .SetTextVariable("LIST", string.Join(", ", shortages.ToArray()))
                    : new TextObject("{=!}No shortages today - but the smith will procure spare kit all the same.");
                return true;
            }
            catch (Exception e) { Log.Error("OrderKitCondition", e); return false; }
        }

        private static void OrderKitConsequence(MenuCallbackArgs args)
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var needs = QuartermasterLaw.CountNeeds();
                var elements = new List<InquiryElement>();
                foreach (var type in QuartermasterLaw.KitTypes)
                {
                    int need = QuartermasterLaw.WornFor(type, needs);
                    if (need <= 0) continue;
                    int have = QuartermasterLaw.HaveFor(armory, type);
                    bool horse = type == ItemObject.ItemTypeEnum.Horse;
                    string label = type + "   (racks " + have + " / need " + need + ")"
                                 + (have < need ? "  - SHORT " + (need - have) : "");
                    elements.Add(new InquiryElement(type, label, null, !horse,
                        horse ? "The smith does not deal in horseflesh - see the stables."
                              : (have < need ? "The men lack " + (need - have) + " of these." : "Fully stocked - spares never hurt.")));
                }
                if (elements.Count == 0) { Log.Player("The men need nothing - there is no one to outfit.", true); return; }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Order Ledger", "What shall the smith procure for the men? Racks / need counts what the armoury holds against what they carry.",
                    elements, true, 1, 1, "Choose", "Leave",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        var type = (ItemObject.ItemTypeEnum)sel[0].Identifier;
                        int shortage = Math.Max(0, QuartermasterLaw.WornFor(type, QuartermasterLaw.CountNeeds())
                                                   - QuartermasterLaw.HaveFor(QuartermasterLaw.DteArmory(), type));
                        OrderKitTiers(type, shortage);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("OrderKitConsequence", e); }
        }

        /// <summary>Najtansza KUPNA sztuka danego typu i tieru - prosty, zolnierski wyrob.</summary>
        private static ItemObject CheapestOf(ItemObject.ItemTypeEnum type, int tier)
        {
            ItemObject best = null;
            try
            {
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.ItemType != type || item.NotMerchandise) continue;
                    if (item.Value <= 0) continue;
                    if (Recipes.Grade(item) != tier) continue;
                    string sId = (item.StringId ?? "").ToLowerInvariant();
                    if (sId.Contains("practice") || sId.Contains("tournament") || sId.Contains("dummy")
                        || sId.Contains("test_") || sId.Contains("_test") || sId.Contains("siege")) continue;
                    if (best == null || item.Value < best.Value) best = item;
                }
            }
            catch (Exception e) { Log.Error("CheapestOf", e); }
            return best;
        }

        private static int OrderPieceCost(ItemObject item)
        {
            float mk = MathF.Max(1f, Settings.Current.TroopOrderMarkup);
            return Math.Max(1, (int)(item.Value * mk));
        }

        private static void OrderKitTiers(ItemObject.ItemTypeEnum type, int shortage)
        {
            try
            {
                var elements = new List<InquiryElement>();
                for (int t = 1; t <= 6; t++)
                {
                    var item = CheapestOf(type, t);
                    if (item == null) continue;
                    int per = OrderPieceCost(item);
                    elements.Add(new InquiryElement(item, "Tier " + t + " - " + item.Name + ", " + per + " gold apiece",
                        ItemPic(item), true, "The plainest sound piece of its grade. Market worth plus the smith's fee."));
                }
                if (elements.Count == 0) { Log.Player("No such kit is traded at any market the smith knows.", true); return; }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Order Ledger", "Which grade of " + type + "?" + (shortage > 0 ? " The men are short " + shortage + "." : ""),
                    elements, true, 1, 1, "Choose", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        var item = sel[0].Identifier as ItemObject;
                        if (item != null) OrderKitCount(item, shortage);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("OrderKitTiers", e); }
        }

        private static void OrderKitCount(ItemObject item, int shortage)
        {
            try
            {
                int per = OrderPieceCost(item);
                int gold = Hero.MainHero.Gold;
                var counts = new List<int> { 1, 5, 10 };
                if (shortage > 0 && !counts.Contains(shortage)) counts.Add(shortage);
                counts.Sort();
                var elements = new List<InquiryElement>();
                foreach (var n in counts)
                {
                    int total = per * n;
                    string label = n + " x " + item.Name + " - " + total + " gold"
                                 + (n == shortage ? "  (fills the shortage)" : "");
                    elements.Add(new InquiryElement(n, label, null, gold >= total,
                        gold >= total ? "Straight onto the men's racks." : "Your purse comes up short."));
                }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    item.Name.ToString(), per + " gold apiece. The smith sends boys round the markets and the pieces land on the armoury racks.",
                    elements, true, 1, 1, "Order", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            int n = (int)sel[0].Identifier;
                            int total = per * n;
                            float hours = Math.Min(24f, 1f + n * 0.2f);
                            StartTimedWork(hours,
                                "The smith takes your coin and sends his boys round the markets.",
                                delegate { DoOrderKit(item, n, total); });
                        }
                        catch (Exception ex) { Log.Error("OrderKitCount.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("OrderKitCount", e); }
        }

        private static void DoOrderKit(ItemObject item, int n, int total)
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) { Log.Player("The armoury wagons are nowhere to be found.", true); return; }
                if (Hero.MainHero.Gold < total) { Log.Player("Your purse came up short.", true); return; }
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, total);
                armory.AddToCounts(item, n);
                Log.Player(n + " x " + item.Name + " delivered to the men's racks for " + total + " gold.");
                Log.Info("Zamowienie dla wojska: " + n + "x " + item.StringId + " za " + total);
            }
            catch (Exception e) { Log.Error("DoOrderKit", e); }
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
                    new InquiryElement(0, "Hastily - " + Project.TimeLabel(baseDays * s.TempoHastyTime), null, true,
                        "Half the time. Double the risk of ruining it, and almost no chance of fine work."),
                    new InquiryElement(1, "At a steady pace - " + Project.TimeLabel(baseDays), null, true,
                        "The honest way."),
                    new InquiryElement(2, "With care - " + Project.TimeLabel(baseDays * s.TempoCarefulTime), null, true,
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
                            Log.Player("You set to work on " + item.Name + ". " + Project.TimeLabel(days) +
                                       " at the anvil, and you must stay here to see it through.");
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
                    elements.Add(new InquiryElement(item, item.Name + "  (x" + el.Amount + ")", ItemPic(item), true,
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
                    elements.Add(new InquiryElement(item, item.Name + "  (x" + el.Amount + ")", ItemPic(item), true,
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
        private static bool _workLeave;   // opcja "Put the work aside" podnosi, WorkTick przelacza

        private static void StartTimedWork(float hours, string label, Action apply)
        {
            _workTarget = Math.Max(0.25f, hours);
            _workDone = 0f;
            _workLeave = false;
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
                if (_workLeave) { _workLeave = false; GameMenu.SwitchToMenu(Menu); return; }
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

        // ostatnia polka strzelecka (typ + tier) - Jeff: "jak wchodze w luki,
        // niech pamieta gdzie ostatnio bylem, zebym nie klikal w kolko"
        private static ItemObject.ItemTypeEnum _lastRangedType;
        private static int _lastRangedTier;
        private static bool _hasLastRanged;

        private static string TypeLabel(ItemObject.ItemTypeEnum t)
        {
            switch (t)
            {
                case ItemObject.ItemTypeEnum.Bow: return "Bows";
                case ItemObject.ItemTypeEnum.Crossbow: return "Crossbows";
                case ItemObject.ItemTypeEnum.Arrows: return "Arrows";
                case ItemObject.ItemTypeEnum.Bolts: return "Bolts";
                default: return t.ToString();
            }
        }

        private static void FletcherConsequence(MenuCallbackArgs args)
        {
            try
            {
                var slots = new List<InquiryElement>();
                if (_hasLastRanged)
                    slots.Add(new InquiryElement("last",
                        "Back to the bench you left - " + TypeLabel(_lastRangedType) + ", tier " + _lastRangedTier,
                        null, true, "Straight to the shelf you last worked at."));
                slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.Bow,      "Bows", null, true, "Warbows and hunting bows - wood, sinew and a good eye"));
                slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.Crossbow, "Crossbows", null, true, "Crossbows and windlasses - an iron lock in a wooden stock"));
                slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.Arrows,   "Arrows", null, true, "Fletched in batches - one job yields several sheaves"));
                slots.Add(new InquiryElement(ItemObject.ItemTypeEnum.Bolts,    "Bolts", null, true, "Quarrels in batches - one job yields several cases"));

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Bowyer's Bench", "Wood, horn and sinew. Your Smithing decides what you can attempt - and your stores pay for it.",
                    slots, true, 1, 1, "Choose", "Leave",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        if (sel[0].Identifier is string)
                        { ShowItems(_lastRangedType, false, _lastRangedTier); return; }
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
                if (rangedKit && tierFilter > 0)
                { _lastRangedType = type; _lastRangedTier = tierFilter; _hasLastRanged = true; }
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
                    elements.Add(new InquiryElement(item, label, ItemPic(item),
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
                            if (instant)
                            {
                                Forge.Smith(item);
                                // zostajemy na tej samej polce - bez klikania od nowa
                                ShowItems(type, true, tierFilter);
                            }
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
