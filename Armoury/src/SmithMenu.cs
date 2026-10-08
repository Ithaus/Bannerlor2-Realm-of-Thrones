using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
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

            // kucie NAUCZONYCH unikatow poszlo do zakladki CRAFT (Jeff 30.08:
            // "kucie jest w kuzni, a dokladnie w craft") - ForgeView doklada
            // nauczone wzory na liste, RangedLore.KnownOf je otwiera

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
                "{=!}Put the work aside (pay only for what is finished)",
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

        // ------------------------------------------------- rytm doby przy kowadle
        // (Jeff 31.08: "nie moge pracowac 33 godzin - 18h pracy, potem sen
        // 6h albo wiecej przy dlugu; musi byc info ze spie"). Czekanie przy
        // projekcie liczy zmiany: po AnvilShiftHours pracy kowal klade sie
        // na NightRest.NeededHours() - w tym czasie stamina regeneruje po
        // obozowemu (wyjatek w NoRestAtWork), a rachunek snu nalicza sie sam
        // (postoj w osadzie). Po pobudce wraca do mlota.
        internal static bool AnvilSleeping { get { return _anvilAsleep > 0f; } }
        private static float _anvilAwake;
        private static float _anvilAsleep;

        private static void AnvilShift(float hours)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AnvilShiftEnabled || hours <= 0f) return;
                if (_anvilAsleep > 0f)
                {
                    _anvilAsleep -= hours;
                    if (_anvilAsleep <= 0f)
                    {
                        _anvilAwake = 0f;
                        Log.Player("You wake and return to the anvil.", false);
                    }
                    return;
                }
                _anvilAwake += hours;
                if (_anvilAwake >= Math.Max(6f, s.AnvilShiftHours))
                {
                    _anvilAsleep = NightRest.NeededHours();
                    Log.Player(((int)s.AnvilShiftHours) + " hours at the anvil - you bed down by the forge ("
                               + _anvilAsleep.ToString("0.#") + "h of sleep" + (NightRest.Debt > 0 ? ", paying off the debt" : "") + ").", true);
                }
            }
            catch (Exception e) { Log.Error("AnvilShift", e); }
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
                _anvilAwake = 0f; _anvilAsleep = 0f;
                _projLeave = false;
                var b = ArmouryBehavior.Instance;
                _projInitialHours = b != null ? Math.Max(1f, b.ProjectHoursLeftHere(Settlement.CurrentSettlement)) : 1f;
                // JEDNA PRAWDA GODZIN (Jeff 29.08: "inne godziny w smith, inne jak
                // czekam - balagan"): to jest SUMA wszystkich zlecen u tego kowala
                // (pracuje po kolei), a robota idzie takze bez ciebie
                MBTextManager.SetTextVariable("ARM_PROJ_TEXT",
                    "The smith works the jobs one by one - about " +
                    ((int)Math.Ceiling(_projInitialHours)) + " hours until ALL your work here is done. " +
                    (ForgeClock.On ? "Each piece comes off the anvil when it is done. Leave, and the work waits for you."
                                   : "He needs no watching: leave, and the clock still runs."));
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Log.Error("ProjWaitInit", e); }
        }

        private static void ProjWaitTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                AnvilShift((float)dt.ToHours);
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
            int old = Math.Max(2, (int)(el.Item.Value * (1f - pm) * Settings.Current.RepairCostFactor) / 2);
            // regula kowali miasta: ulamek wykonania sztuki po dniowce mistrza w tym miescie (MendMaterial.Labor)
            return MarketRule ? MendMaterial.Labor(el.Item, MendMaterial.Share(el), Settlement.CurrentSettlement, old) : old;
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
                    if (LootPrices.SmithRefuses(el.EquipmentElement.ItemModifier)) continue;   // paczka 158: wrak na zlom - kowal za monete go nie odnawia
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

        // ------------------------------------------------- kowale miasta za monete: robota + material z targu, bez wrakow
        // Jeff 07.10 (naprawa u kwatermistrza Spoils): "placi kasie miasta (kowale), zuzywa material z targu wedle stanu, wrakow
        // (Mangled) nie odnawia" - ta sama regula dla calej lawy naprawczej za monete: "Restore ALL", "Pick a piece - the smith",
        // "Send the men's worn gear", "Mend everything you wear - the smith's price". Robocizna jak dotad (PieceCost / TroopPieceCost /
        // uprzaz: RepairCostFactor), material MendMaterial.Order (wedle stanu, z polki miasta albo zapasu kowali, po cenie targu);
        // brak materialu - sztuka czeka; wrak - tylko wlasne rece z materialem ("Mend it yourself") albo przetop. Wylacznik SmithMendFromMarket.
        internal static bool MarketRule { get { return MendMaterial.RuleOn; } }

        /// <summary>Paczka 158: ile sztuk rostera to wraki, ktorych kowale miasta nie odnawiaja (WrecksToScrap) - do podpowiedzi i komunikatow
        /// sciezek bez reguly kowali (sciezki z regula licza je w MendMaterial.Order).</summary>
        private static int RefusedWrecks(TaleWorlds.CampaignSystem.Roster.ItemRoster roster)
        {
            int n = 0;
            if (roster == null || !LootPrices.ScrapRule) return 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ee = el.EquipmentElement;
                if (el.Amount > 0 && ee.Item != null && IsBattleWorn(ee.Item, ee.ItemModifier) && LootPrices.SmithRefuses(ee.ItemModifier)) n += el.Amount;
            }
            return n;
        }

        private static string WreckNote(int n) { return n > 0 ? " " + n + LootPrices.WreckWhatEn(n) : ""; }

        internal static string TownName()
        {
            var st = Settlement.CurrentSettlement;
            return st != null && st.Name != null ? st.Name.ToString() : "this town";
        }

        private struct Lot { public EquipmentElement El; public int Amount, Labor; public float[] Need; public float Est; }

        /// <summary>Plan zlecenia u kowali miasta dla zuzytych sztuk z rostera (sakwy albo zbrojownia wojska): wraki i sztuki bez receptury
        /// pominiete, reszta najtansze najpierw (robocizna + szacunek materialu - jak dotad najtansze najpierw), razem najwyzej limit zlota
        /// i maxPieces sztuk. Na probie (polka i zapas bez zmian) - wykonanie robi ApplyRoster.</summary>
        private static MendMaterial.Order PlanRoster(TaleWorlds.CampaignSystem.Roster.ItemRoster roster, Func<EquipmentElement, int> labor, long limit, int maxPieces)
        {
            var o = new MendMaterial.Order(Settlement.CurrentSettlement);
            if (roster == null) return o;
            var lots = new List<Lot>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ee = el.EquipmentElement;
                if (el.Amount <= 0 || ee.Item == null || !IsBattleWorn(ee.Item, ee.ItemModifier)) continue;
                if (LootPrices.IsWreck(ee.ItemModifier)) { o.Wrecks += el.Amount; continue; }   // wpis 97: wrak nie za monete
                var need = MendMaterial.Needs(ee);
                if (need == null) { o.NoSmith += el.Amount; continue; }
                lots.Add(new Lot { El = ee, Amount = el.Amount, Labor = labor(ee), Need = need });
            }
            for (int i = 0; i < lots.Count; i++) { var l = lots[i]; l.Est = l.Labor + o.Bench.Estimate(l.Need); lots[i] = l; }
            lots.Sort((a, b) => a.Est.CompareTo(b.Est));
            foreach (var l in lots) o.AddLot(l.El, l.Need, l.Labor, l.Amount, limit, maxPieces);
            return o;
        }

        /// <summary>Wykonanie planu na rosterze: material z polki (Commit), sztuki z zuzytych na czyste, zaplata w finally - za to, co naprawde
        /// zrobione (robocizna + material w calych pensach), z kiesy gracza do kasy miasta (Pay.ToSettlement - jak dotad).</summary>
        private static void ApplyRoster(MendMaterial.Order o, TaleWorlds.CampaignSystem.Roster.ItemRoster roster, out int done, out int paid)
        {
            done = 0; paid = 0;
            int labor = 0; float mat = 0f;
            try
            {
                o.Bench.Commit();
                foreach (var j in o.Jobs)
                {
                    bool off = false;
                    try
                    {
                        roster.AddToCounts(j.El, -j.N); off = true;
                        roster.AddToCounts(new EquipmentElement(j.El.Item), j.N); off = false;
                    }
                    finally { if (off) roster.AddToCounts(j.El, j.N); }
                    done += j.N; labor += j.Labor; mat += j.Mat;
                }
            }
            finally
            {
                if (done == o.Pieces) mat = o.Mat;   // cala robota: ta sama suma co w planie (podpowiedz opcji) - co do pensa
                paid = labor + MendMaterial.Gold(mat);
                Pay.ToSettlement(paid);
            }
        }

        /// <summary>Linia "Smith: ..." w podpowiedzi listy: cena kowali miasta (robota + material) albo dlaczego nie za monete.</summary>
        /// <summary>Poprawka po recenzji 158: czy kowale miasta odmawiaja tej sztuki. Na grzbiecie (slot >= 0) - ta sama regula co "Mend everything
        /// you wear" (modyfikator wraku ALBO stan w ksiedze zuzycia <= 10%, ArmouryBehavior.SlotWreck); w sakwach i w magazynie - modyfikator.
        /// Dotad "Pick a piece" patrzyl na grzbiecie tylko na modyfikator: miecz zuzyty w ksiedze do 8% (modyfikator zuzycia z grupy broni,
        /// nie wrak) kowal odnawial za kilka groszy, a "Mend everything you wear" tej samej sztuki odmawial.</summary>
        private static bool SmithRefusesHere(EquipmentElement ee, int slot)
        {
            if (slot >= 0 && ArmouryBehavior.Instance != null) return ArmouryBehavior.Instance.SlotWreck(slot);
            return LootPrices.SmithRefuses(ee.ItemModifier);
        }

        private static string SmithLine(MendMaterial.Order q, EquipmentElement ee, int slot = -1)
        {
            string hrs = Settings.Current.MendLootHoursPerPiece.ToString("0.#") + "h";
            const string scrap = "Smith: not for coin - a wreck (worn to a tenth of its worth or less) is scrap: melt it down, or mend it at your own anvil";   // paczka 158
            if (SmithRefusesHere(ee, slot)) return scrap;   // recenzja 158: na grzbiecie takze stan w ksiedze
            int labor = PieceCost(ee);
            if (q == null) return "Smith: " + labor + " gold, " + hrs;
            int total, miss; float mat;
            int r = q.Quote(ee, MendMaterial.Needs(ee), labor, out total, out mat, out miss);
            if (r == MendMaterial.Wreck) return "Smith: not for coin - a wreck is mended only with your own materials (or melted down)";
            if (r == MendMaterial.NoRecipe) return "Smith: no smith's work";
            if (!q.Ok) return "Smith: no town smiths here";
            if (r == MendMaterial.Waits) return "Smith: waits - the market has not enough " + MendMaterial.KindsEn(miss);
            return "Smith: " + total + " gold (work " + labor + ", materials " + MendMaterial.Gold(mat) + "), " + hrs;
        }

        private static bool MendLootCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                if (!Settings.Current.WreckSalvageEnabled && !Settings.Current.BattlefieldLawEnabled) return false;
                if (MarketRule)
                {
                    int bags, bagsCost; ScanBattleWorn(out bags, out bagsCost);
                    if (bags == 0)
                    { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}No battle-worn loot in your bags."); return true; }
                    var o = PlanRoster(MobileParty.MainParty.ItemRoster, PieceCost, Hero.MainHero.Gold, int.MaxValue);
                    string town = TownName();
                    if (!o.Ok)
                    { args.IsEnabled = false; args.Tooltip = new TextObject("There are no town smiths here."); return true; }
                    if (o.Pieces == 0)
                    { args.IsEnabled = false; args.Tooltip = new TextObject("The smiths of " + town + " can restore none of it for coin now." + o.LeftEn(town)); return true; }
                    args.Tooltip = new TextObject("The smiths of " + town + " will make " + o.Pieces + " battle-worn pieces whole for " + o.Total + " gold - "
                        + o.Labor + " for their work and " + o.MatGold + " for materials from the market." + MendMaterial.WageNote(Settlement.CurrentSettlement) + o.LeftEn(town));
                    return true;
                }
                int can, canCost, all, allCost;
                AffordableBattleWorn(out can, out canCost, out all, out allCost);
                int wrecksL = RefusedWrecks(MobileParty.MainParty.ItemRoster);   // paczka 158: wraki na zlom
                if (all == 0 && wrecksL > 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}The smith restores none of it for coin." + WreckNote(wrecksL)); return true; }
                if (all == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}No battle-worn loot in your bags."); return true; }
                if (can == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}{ALL} worn pieces, {COST} gold for the lot - you cannot afford even the cheapest." + WreckNote(wrecksL)).SetTextVariable("ALL", all).SetTextVariable("COST", allCost); return true; }
                if (can < all)
                    args.Tooltip = new TextObject("{=!}{ALL} worn pieces ({COST} gold for the lot). For your purse the smith will mend the {CAN} cheapest for {CANCOST}." + WreckNote(wrecksL))
                        .SetTextVariable("ALL", all).SetTextVariable("COST", allCost).SetTextVariable("CAN", can).SetTextVariable("CANCOST", canCost);
                else
                    args.Tooltip = new TextObject("{=!}{ALL} battle-worn pieces. The smith will make them whole for {COST} gold." + WreckNote(wrecksL))
                        .SetTextVariable("ALL", all).SetTextVariable("COST", allCost);
                return true;
            }
            catch (Exception e) { Log.Error("MendLootCondition", e); return false; }
        }

        private static void MendLootConsequence(MenuCallbackArgs args)
        {
            try
            {
                if (MarketRule)
                {
                    var o = PlanRoster(MobileParty.MainParty.ItemRoster, PieceCost, Hero.MainHero.Gold, int.MaxValue);
                    if (o.Pieces == 0) { Log.Player("The smiths of " + TownName() + " can restore none of it for coin now." + o.LeftEn(TownName()), true); return; }
                    StartTimedWork(o.Pieces * Settings.Current.MendLootHoursPerPiece,
                        "The smiths sort the battle spoils and take hammer to the worst of it.",
                        delegate { DoMendLoot(); });
                    return;
                }
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
                if (allN == 0) { Log.Player("The smith restores none of it for coin." + WreckNote(RefusedWrecks(roster)), true); return; }   // paczka 158: same wraki
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
                if (MarketRule)
                {
                    // plan od nowa przy koncu roboty (jak dotad: zloto, polka i ceny moga sie zmienic przez te godziny)
                    var bag = MobileParty.MainParty.ItemRoster;
                    var o = PlanRoster(bag, PieceCost, Hero.MainHero.Gold, int.MaxValue);
                    string town = TownName();
                    if (o.Pieces == 0)
                    {
                        Log.Player("The smiths of " + town + " could restore nothing." + o.LeftEn(town), true);
                        Log.Info("Lawa naprawcza (lup z sakw) - kowale " + town + ": " + o.LogPl());
                        return;
                    }
                    int doneM, paidM;
                    ApplyRoster(o, bag, out doneM, out paidM);
                    Log.Player("The smiths of " + town + " restored " + doneM + " battle-worn pieces for " + paidM + " gold, paid into the town's coffers: "
                               + o.Labor + " for their work and " + o.MatGold + " for materials from its market." + o.LeftEn(town));
                    Log.Info("Lawa naprawcza (lup z sakw) - kowale " + town + ": " + o.LogPl());
                    return;
                }
                var roster = MobileParty.MainParty.ItemRoster;
                var worn = new List<ItemRosterElement>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item != null && IsBattleWorn(el.EquipmentElement.Item, el.EquipmentElement.ItemModifier)
                        && !LootPrices.SmithRefuses(el.EquipmentElement.ItemModifier))   // paczka 158: wrak na zlom
                        worn.Add(el);
                }
                int wrecksD = RefusedWrecks(roster);
                if (worn.Count == 0) { if (wrecksD > 0) Log.Player("The smith restores none of it for coin." + WreckNote(wrecksD), true); return; }
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
                Pay.ToSettlement(paid);
                Log.Player((skipped > 0
                    ? "The smith mended the " + done + " cheapest pieces for " + paid + " gold. " + skipped + " await a fuller purse."
                    : "The smith hammered " + done + " battle-worn pieces back to true for " + paid + " gold.") + WreckNote(wrecksD));
                Log.Info("Naprawa lupow: " + done + " szt. za " + paid + ", pominieto " + skipped + ", wraki na zlom " + wrecksD);
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
                var quote = MarketRule ? new MendMaterial.Order(Settlement.CurrentSettlement) : null;   // wycena kowali miasta (robota + material)

                // najpierw to, co na grzbiecie - z wyraznym znacznikiem
                var beq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var ee0 = beq[slot];
                    if (ee0.Item == null || !IsBattleWorn(ee0.Item, ee0.ItemModifier)) continue;
                    int pct0 = Math.Max(1, (int)Math.Round(ee0.ItemModifier.PriceMultiplier * 100f));
                    var mats0 = SelfMendParts(ee0);
                    var sb0 = new System.Text.StringBuilder();
                    foreach (var p in mats0)
                    {
                        if (p.Item == null) continue;
                        if (sb0.Length > 0) sb0.Append(", ");
                        sb0.Append(p.Count + "x " + p.Item.Name + " (" + Recipes.CountInInventory(p.Item) + ")");
                    }
                    string hint0 = "EQUIPPED - you wear this now.\nCondition " + pct0 + "%" +
                                   "\n" + SmithLine(quote, ee0, slot) +
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
                        var mats = SelfMendParts(ee);
                        var sb = new System.Text.StringBuilder();
                        foreach (var p in mats)
                        {
                            if (p.Item == null) continue;
                            if (sb.Length > 0) sb.Append(", ");
                            sb.Append(p.Count + "x " + p.Item.Name + " (" + Recipes.CountInInventory(p.Item) + ")");
                        }
                        string hint = "Condition " + pct + "%" +
                                      "\n" + SmithLine(quote, ee) +
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
                            string hint2 = "COMPANY STORES - the men's kit.\nCondition " + pct2 + "%" +
                                           "\n" + SmithLine(quote, ee);
                            found.Add(ee); slots.Add(-2);
                            elements.Add(new InquiryElement(found.Count - 1,
                                "[STORES] " + ee.GetModifiedItemName() + "  x" + el.Amount, ItemPic(ee.Item), true, hint2));
                        }
                        catch (Exception exi) { Log.Error("MendPick.storeItem", exi); }
                    }

                if (elements.Count == 0) { Log.Player("Nothing damaged on your back, in your bags or in the stores.", true); return; }

                // NAJPIERW ZALOZONE (Jeff 01.09: "pierwsze na liscie moje
                // przedmioty equipped, potem dopiero wedlug wzoru i typu") -
                // [EQUIPPED] w kolejnosci slotow na gorze; reszta po typach:
                // luki razem, strzaly razem, PANCERZE dalej
                elements.Sort((a, b) =>
                {
                    int sa = slots[(int)a.Identifier], sb = slots[(int)b.Identifier];
                    bool wa = sa >= 0, wb = sb >= 0;
                    if (wa != wb) return wa ? -1 : 1;
                    if (wa) return sa.CompareTo(sb);
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

                string smithTitle = "The smith mends it - " + smith + " gold";
                string smithHint = Settings.Current.MendLootHoursPerPiece.ToString("0.#") + " hours. Coin does the sweating.";
                bool smithOk = Hero.MainHero.Gold >= smith;
                if (SmithRefusesHere(ee, slot))   // recenzja 158: na grzbiecie takze stan w ksiedze <= 10% (jak "Mend everything you wear")
                {
                    // paczka 158 (decyzja Jeffa 07.10 "wraki ida na zlom"): na kazdej drodze, takze bez reguly kowali miasta
                    smithOk = false;
                    smithTitle = "The smiths will not restore a wreck for coin";
                    smithHint = "A wreck (Mangled, or worn to a tenth of its worth or less) is scrap - it is mended only with your own materials at your own anvil, or melted down.";
                }
                else if (MarketRule)
                {
                    // kowale miasta: robota + material z targu wedle stanu; wrak tylko wlasnymi rekami albo przetop
                    var q = new MendMaterial.Order(Settlement.CurrentSettlement);
                    int total, miss; float mat;
                    int r = q.Quote(ee, MendMaterial.Needs(ee), smith, out total, out mat, out miss);
                    string town = TownName();
                    smithOk = false;
                    if (r == MendMaterial.Wreck)
                    { smithTitle = "The smith will not restore a wreck for coin"; smithHint = "A wreck (Mangled) is mended only with your own materials - or melt it down."; }
                    else if (r == MendMaterial.NoRecipe)
                    { smithTitle = "No smith's work"; smithHint = "The smiths of " + town + " do not mend this."; }
                    else if (!q.Ok)
                    { smithTitle = "No town smiths here"; smithHint = "Only a town's smiths take work for coin."; }
                    else if (r == MendMaterial.Waits)
                    { smithTitle = "The smith mends it - waits for materials"; smithHint = "The market of " + town + " has not enough " + MendMaterial.KindsEn(miss) + " - the piece waits."; }
                    else
                    {
                        smith = total; smithOk = Hero.MainHero.Gold >= total;
                        smithTitle = "The smith mends it - " + total + " gold";
                        smithHint = Settings.Current.MendLootHoursPerPiece.ToString("0.#") + " hours. " + (total - MendMaterial.Gold(mat)) + " for the smiths' work and "
                                    + MendMaterial.Gold(mat) + " for materials from the market of " + town + ", paid into the town's coffers." + MendMaterial.WageNote(Settlement.CurrentSettlement);
                    }
                }

                var opts = new List<InquiryElement>
                {
                    new InquiryElement(0, smithTitle, null, smithOk, smithHint),
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

        /// <summary>Kowale miasta robia JEDNA sztuke (SmithMendFromMarket): plan od nowa przy koncu roboty (robocizna PieceCost + material
        /// z polki / zapasu kowali), material z polki, zaplata do kasy miasta. false = nic nie zrobiono i nic nie zaplacono (wrak, nie robota
        /// kowala, brak materialu, za malo zlota) - komunikat juz pokazany.</summary>
        private static bool SmithTakesOne(EquipmentElement ee, out int paid, out MendMaterial.Order o)
        {
            paid = 0;
            o = new MendMaterial.Order(Settlement.CurrentSettlement);
            string town = TownName();
            if (o.AddLot(ee, MendMaterial.Needs(ee), PieceCost(ee), 1, Hero.MainHero.Gold, 1) <= 0)
            {
                string why = !o.Ok ? "There are no town smiths here - nothing was mended."
                           : o.Wrecks > 0 ? (LootPrices.ScrapRule ? "A" + LootPrices.WreckWhatEn(1)
                                                                  : "The smiths will not restore a wreck (Mangled) for coin - mend it yourself with your own materials, or melt it down.")
                           : o.NoSmith > 0 ? "That is no smith's work - nothing was mended."
                           : o.Wait > 0 ? "The smiths of " + town + " ran short of " + MendMaterial.KindsEn(o.WaitMask) + " - the piece waits, nothing paid."
                           : "Your purse came up short.";
                Log.Player(why, true);
                Log.Info("Lawa naprawcza (sztuka) - kowale " + town + ": " + ee.Item.StringId + " - " + o.LogPl());
                return false;
            }
            o.Bench.Commit();
            paid = o.Total;
            Pay.ToSettlement(paid);
            return true;
        }

        private static void DoMendOne(EquipmentElement ee, int slot, bool bySmith, int gold, List<Recipes.Part> mats, int stamina)
        {
            try
            {
                if (bySmith && SmithRefusesHere(ee, slot))
                { Log.Player("A" + LootPrices.WreckWhatEn(1), true); return; }   // paczka 158
                MendMaterial.Order order = null;   // SmithMendFromMarket: zlecenie kowali (robota + material)
                // slot -2 = magazyn wojska: naprawa zdejmuje zbita sztuke ze stanu
                // i odklada czysta na stan (zolnierze dostana ja przy przydziale)
                if (slot == -2)
                {
                    var store = QuartermasterLaw.DteArmory();
                    if (store == null || store.FindIndexOfElement(ee) < 0)
                    { Log.Player("The piece is no longer in the stores.", true); return; }
                    if (bySmith)
                    {
                        if (MarketRule) { if (!SmithTakesOne(ee, out gold, out order)) return; }
                        else
                        {
                            if (Hero.MainHero.Gold < gold) { Log.Player("Your purse came up short.", true); return; }
                            Pay.ToSettlement(gold);
                        }
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
                    Log.Player(ee.Item.Name + " is whole again and back in the company stores." + (order != null
                        ? " " + gold + " gold into the coffers of " + TownName() + " (" + order.Labor + " for the work, " + order.MatGold + " for materials)." : ""));
                    Log.Info("Naprawa sztuki (magazyn): " + ee.Item.StringId + (bySmith ? " kowal " + gold : " wlasna")
                             + (order != null ? " | lawa naprawcza - kowale " + TownName() + ": " + order.LogPl() : ""));
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
                    if (MarketRule) { if (!SmithTakesOne(ee, out gold, out order)) return; }
                    else
                    {
                        if (Hero.MainHero.Gold < gold) { Log.Player("Your purse came up short.", true); return; }
                        Pay.ToSettlement(gold);
                    }
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
                    // zbroja z kuzni jak bron: zuzyta w boju dobra/lordly/legendarna sztuka wraca do swojego stanu, reszta - zwykla
                    ItemModifier back = ArmouryBehavior.Instance != null ? ArmouryBehavior.Instance.GoodOriginal(slot) : null;
                    Hero.MainHero.BattleEquipment[slot] = new EquipmentElement(ee.Item, back);
                    if (ArmouryBehavior.Instance != null) ArmouryBehavior.Instance.ResetSlotCondition(slot);
                }
                else
                {
                    roster.AddToCounts(ee, -1);
                    roster.AddToCounts(new EquipmentElement(ee.Item), 1);
                }
                Log.Player(ee.Item.Name + " is whole again" + (order != null
                    ? " - " + gold + " gold into the coffers of " + TownName() + " (" + order.Labor + " for the work, " + order.MatGold + " for materials)."
                    : bySmith ? " - " + gold + " gold well spent." : " - your own work."));
                Log.Info("Naprawa sztuki: " + ee.Item.StringId + (bySmith ? " kowal " + gold : " wlasna")
                         + (order != null ? " | lawa naprawcza - kowale " + TownName() + ": " + order.LogPl() : ""));
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
            if (MarketRule)
            {
                // regula kowali miasta: ta sama robota co na lawie (MendMaterial.LaborF), rabat hurtowy jak dotad
                float lf = MendMaterial.LaborF(el.Item, MendMaterial.Share(el), Settlement.CurrentSettlement);
                if (lf >= 0f) return Math.Max(1, (int)Math.Round(lf * (1f - discount)));
            }
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
                    if (LootPrices.SmithRefuses(ee.ItemModifier)) continue;   // paczka 158: wrak na zlom (dotad za TroopMendWreckShare wartosci)
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

        /// <summary>Plan "Send the men's worn gear" (SmithMendFromMarket): zbrojownia wojska, robocizna TroopPieceCost z rabatem hurtowym
        /// liczonym od sztuk, ktore kowale moga wziac za monete (bez wrakow i sztuk bez receptury), material z targu. racks - wszystkie
        /// zuzyte sztuki na polkach (z wrakami), jak dotad w podpowiedziach.</summary>
        private static MendMaterial.Order PlanTroops(long limit, int maxPieces, out int discountPct, out int racks)
        {
            discountPct = 0; racks = 0;
            var armory = QuartermasterLaw.DteArmory();
            int cand = 0;
            if (armory != null)
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var ee = el.EquipmentElement;
                    if (el.Amount <= 0 || ee.Item == null || !IsBattleWorn(ee.Item, ee.ItemModifier)) continue;
                    racks += el.Amount;
                    if (!LootPrices.IsWreck(ee.ItemModifier) && MendMaterial.Needs(ee) != null) cand += el.Amount;
                }
            float discount = TroopBulkDiscount(cand);
            discountPct = (int)(discount * 100f);
            return PlanRoster(armory, ee => TroopPieceCost(ee, discount), limit, maxPieces);
        }

        private static bool MendTroopsCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var s = Settings.Current;
                if (s == null || !s.TroopMendEnabled) return false;
                if (QuartermasterLaw.DteArmory() == null) return false;      // bez DTE nie ma zbrojowni
                if (MarketRule)
                {
                    int dp = 0, racks = 0;
                    var o = QuartermasterEscrow.Active ? null : PlanTroops(Hero.MainHero.Gold, int.MaxValue, out dp, out racks);
                    if (o == null || racks == 0)
                    { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}The men's racks hold nothing worn - every piece is sound."); return true; }
                    string town = TownName();
                    if (!o.Ok)
                    { args.IsEnabled = false; args.Tooltip = new TextObject("There are no town smiths here."); return true; }
                    if (o.Pieces == 0)
                    { args.IsEnabled = false; args.Tooltip = new TextObject(racks + " worn pieces on the men's racks - the smiths of " + town + " can mend none of them for coin now." + o.LeftEn(town)); return true; }
                    args.Tooltip = new TextObject(racks + " worn pieces on the men's racks. The smiths of " + town + " and their apprentices will make " + o.Pieces + " of them whole for "
                        + o.Total + " gold - " + o.Labor + " for the work (bulk discount " + dp + "%) and " + o.MatGold + " for materials from the market." + MendMaterial.WageNote(Settlement.CurrentSettlement) + o.LeftEn(town));
                    return true;
                }
                int all, allCost, can, canCost, disc;
                ScanTroopWorn(out all, out allCost, out can, out canCost, out disc);
                int wrecksR = RefusedWrecks(QuartermasterLaw.DteArmory());   // paczka 158: wraki na zlom
                if (all == 0 && wrecksR > 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}The smith restores none of the men's gear for coin." + WreckNote(wrecksR)); return true; }
                if (all == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}The men's racks hold nothing worn - every piece is sound."); return true; }
                if (can == 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}{ALL} worn pieces on the racks, {COST} gold for the lot (bulk discount {D}%) - you cannot afford even the cheapest." + WreckNote(wrecksR)).SetTextVariable("ALL", all).SetTextVariable("COST", allCost).SetTextVariable("D", disc); return true; }
                if (can < all)
                    args.Tooltip = new TextObject("{=!}{ALL} worn pieces on the men's racks ({COST} gold for the lot, bulk discount {D}%). For your purse the smith will mend the {CAN} cheapest for {CANCOST}." + WreckNote(wrecksR))
                        .SetTextVariable("ALL", all).SetTextVariable("COST", allCost).SetTextVariable("CAN", can).SetTextVariable("CANCOST", canCost).SetTextVariable("D", disc);
                else
                    args.Tooltip = new TextObject("{=!}{ALL} worn pieces on the men's racks. The smith and his apprentices will make them whole for {COST} gold (bulk discount {D}%)." + WreckNote(wrecksR))
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
                if (MarketRule)
                {
                    int racks = 0;
                    var o = QuartermasterEscrow.Active ? null : PlanTroops(Hero.MainHero.Gold, int.MaxValue, out disc, out racks);
                    if (o == null || o.Pieces == 0) return;
                    int canM = o.Pieces;
                    StartTimedWork(Math.Min(MathF.Max(1f, s.TroopMendMaxHours), canM * s.MendLootHoursPerPiece),
                        "The smiths clear their benches and set every apprentice on the men's gear.",
                        delegate { DoMendTroops(int.MaxValue); },
                        delegate (float frac) { DoMendTroops((int)Math.Floor(canM * frac)); });
                    return;
                }
                ScanTroopWorn(out all, out allCost, out can, out canCost, out disc);
                if (can == 0) return;
                float hours = Math.Min(MathF.Max(1f, s.TroopMendMaxHours), can * s.MendLootHoursPerPiece);
                StartTimedWork(hours,
                    "The smith clears his benches and sets every apprentice on the men's gear.",
                    delegate { DoMendTroops(int.MaxValue); },
                    delegate (float frac) { DoMendTroops((int)Math.Floor(can * frac)); });
            }
            catch (Exception e) { Log.Error("MendTroopsConsequence", e); }
        }

        private static void DoMendTroops(int limit)
        {
            if (limit <= 0) { Log.Player("You call the smith off before a single piece is done - nothing to pay.", true); return; }
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                if (MarketRule)
                {
                    // plan od nowa przy koncu roboty; przerwana robota (wpis 84) - najwyzej limit sztuk, tylko gotowe zaplacone
                    int dp, racks;
                    var o = PlanTroops(Hero.MainHero.Gold, limit, out dp, out racks);
                    string town = TownName();
                    if (o.Pieces == 0)
                    {
                        Log.Player("The smiths of " + town + " could mend none of the men's gear." + o.LeftEn(town), true);
                        Log.Info("Lawa naprawcza (zbrojownia wojska) - kowale " + town + ": rabat " + dp + "%, " + o.LogPl());
                        return;
                    }
                    int doneM, paidM;
                    ApplyRoster(o, armory, out doneM, out paidM);
                    Log.Player("The smiths of " + town + " made " + doneM + " pieces of the men's gear whole for " + paidM + " gold, paid into the town's coffers: "
                               + o.Labor + " for the work (bulk discount " + dp + "%) and " + o.MatGold + " for materials from its market." + o.LeftEn(town));
                    Log.Info("Lawa naprawcza (zbrojownia wojska) - kowale " + town + ": rabat " + dp + "%, " + o.LogPl());
                    return;
                }
                var worn = new List<ItemRosterElement>();
                int total = 0;
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var ee = el.EquipmentElement;
                    if (ee.Item != null && IsBattleWorn(ee.Item, ee.ItemModifier) && !LootPrices.SmithRefuses(ee.ItemModifier)) { worn.Add(el); total += el.Amount; }   // paczka 158: bez wrakow
                }
                int wrecksT = RefusedWrecks(armory);
                if (worn.Count == 0) { if (wrecksT > 0) Log.Player("The smith restores none of the men's gear for coin." + WreckNote(wrecksT), true); return; }
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
                        if (done + fix2 >= limit) break;   // wpis 84: przerwana robota - tylko gotowe sztuki
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
                Pay.ToSettlement(paid);
                Log.Player((skipped > 0
                    ? "The men's " + done + " cheapest pieces are whole again for " + paid + " gold. " + skipped + " await a fuller purse."
                    : "The men's racks are mended: " + done + " pieces made whole for " + paid + " gold.") + WreckNote(wrecksT));
                Log.Info("Naprawa zbrojowni wojska: " + done + " szt. za " + paid + ", pominieto " + skipped + ", wraki na zlom " + wrecksT);
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
                        if (el.Amount <= 0 || !QuartermasterLaw.CountsAsKit(it, type)) continue;
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
                if (MarketRule)
                {
                    // przeglad 07.10: ta sama wycena co "Send the men's worn gear" - bez wrakow, robota + material z targu, rabat od sztuk,
                    // ktore kowale wezma za monete (dotad stary rachunek: wraki w cenie, bez materialu)
                    int racks = 0; disc = 0;
                    var o = QuartermasterEscrow.Active ? null : PlanTroops(Hero.MainHero.Gold, int.MaxValue, out disc, out racks);
                    string town = TownName();
                    if (o == null || racks == 0)
                        sb.Append("\nEvery piece on the racks is sound.");
                    else if (!o.Ok)
                        sb.Append("\n").Append(racks).Append(" worn pieces on the racks - there are no town smiths here.");
                    else if (o.Pieces == 0)
                        sb.Append("\n").Append(racks).Append(" worn pieces on the racks - the smiths of ").Append(town).Append(" can mend none of them for coin now.").Append(o.LeftEn(town));
                    else
                        sb.Append("\n").Append(racks).Append(" worn pieces on the racks. The smiths of ").Append(town).Append(" will make ").Append(o.Pieces)
                          .Append(" of them whole for ").Append(o.Total).Append(" gold - ").Append(o.Labor).Append(" for the work (bulk discount ").Append(disc)
                          .Append("%) and ").Append(o.MatGold).Append(" for materials from the market.").Append(o.LeftEn(town));
                }
                else
                {
                    ScanTroopWorn(out all, out allCost, out can, out canCost, out disc);
                    if (all > 0)
                        sb.Append("\nThe smith will mend all ").Append(all).Append(" worn pieces for ")
                          .Append(allCost).Append(" gold (bulk discount ").Append(disc).Append("%).");
                    else
                        sb.Append("\nEvery piece on the racks is sound.");
                }

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
                if (s.TroopOrderFromShelf)
                {
                    // paczka 145: z polki TEGO miasta po cenie polki + chodzenie kowala
                    var st = Settlement.CurrentSettlement;
                    args.Tooltip = (shortages.Count > 0
                        ? new TextObject("{=!}The men go short: {LIST}. The smith buys plain pieces of any tier off the stalls of {TOWN} at the stall's own price and asks {FEE} gold a piece for his legwork.")
                            .SetTextVariable("LIST", string.Join(", ", shortages.ToArray()))
                        : new TextObject("{=!}No shortages today - but the smith will buy spare kit off the stalls of {TOWN} all the same, at the stall's own price and {FEE} gold a piece for his legwork."))
                        .SetTextVariable("TOWN", st != null ? st.Name.ToString() : "this town").SetTextVariable("FEE", FeeText(st));
                    return true;
                }
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

        /// <summary>Sztuka, jaka kowal sprowadza dla ludzi: KUPNA, danego typu i tieru, prosty zolnierski wyrob (bez cwiczebnych,
        /// turniejowych, testowych, oblezniczych). Jeden filtr dla wzoru ze swiata (CheapestOf) i dla polki miasta (ShelfOrderable).</summary>
        private static bool Orderable(ItemObject item, ItemObject.ItemTypeEnum type, int tier)
        {
            if (item == null || item.ItemType != type || item.NotMerchandise) return false;
            if (item.Value <= 0) return false;
            if (Recipes.Grade(item) != tier) return false;
            string sId = (item.StringId ?? "").ToLowerInvariant();
            return !(sId.Contains("practice") || sId.Contains("tournament") || sId.Contains("dummy")
                     || sId.Contains("test_") || sId.Contains("_test") || sId.Contains("siege"));
        }

        /// <summary>Najtansza KUPNA sztuka danego typu i tieru - prosty, zolnierski wyrob.</summary>
        private static ItemObject CheapestOf(ItemObject.ItemTypeEnum type, int tier)
        {
            ItemObject best = null;
            try
            {
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!Orderable(item, type, tier)) continue;
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
            if (Settings.Current.TroopOrderFromShelf) { OrderShelfTiers(type, shortage); return; }   // paczka 145: z polki tego miasta
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
                // AUDYT 04.10 (C2): sztuki z TARGU tego miasta, zloto do jego kasy - wczesniej z niczego, zloto w nicosc
                var st = Settlement.CurrentSettlement;
                int per = n > 0 ? total / n : total;
                // audyt pelny W8: tylko sztuki w pelni sprawne (GetItemNumber liczyl tez zuzyte, a zdejmujemy nowe - reszta byla z niczego)
                int have = 0;
                if (st != null && st.ItemRoster != null)
                    for (int i = 0; i < st.ItemRoster.Count; i++)
                    {
                        var el = st.ItemRoster.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item == item && el.EquipmentElement.ItemModifier == null) have += el.Amount;
                    }
                int k = Math.Min(n, have);
                if (k <= 0) { Log.Player("The smith's boys could not find a single " + item.Name + " on the market.", true); return; }
                int pay = per * k;
                if (Hero.MainHero.Gold < pay) { Log.Player("Your purse came up short.", true); return; }
                st.ItemRoster.AddToCounts(item, -k);
                Pay.ToSettlement(pay);
                armory.AddToCounts(item, k);
                Log.Player(k + " x " + item.Name + " delivered to the men's racks for " + pay + " gold" + (k < n ? " - the market had no more." : "."));
                Log.Info("Zamowienie dla wojska: " + k + "/" + n + "x " + item.StringId + " za " + pay + " (z targu " + (st != null ? st.Name.ToString() : "?") + ")");
            }
            catch (Exception e) { Log.Error("DoOrderKit", e); }
        }

        // ------------------------------------------------- zamowienie z POLKI miasta (paczka 145, TroopOrderFromShelf)
        // Jeff 07.10 ("cena tak, wybor tak, brak towarow tak"). Dotad kowal bral najtansza sztuke typu i tieru z CALEGO swiata
        // (czesto nie ma jej na tej polce - "could not find"), liczyl wartosc x 1.15 bez wzgledu na cene polki i dawal tylko tyle,
        // ile tej jednej sztuki lezalo na polce. Teraz:
        //  - wybor: najtansza SPRAWNA sztuka typu i tieru, ktora NAPRAWDE lezy na polce tego miasta (te same filtry co dotad,
        //    bez unikatow i koni, stan nie ponizej 100%); kupuje po jednej - przy kolejnej znow najtansza z tego, co zostalo;
        //  - cena: cena kupna miasta za te sztuke (ta sama, ktora placi sakiewka ludzi, lordowie i ekran handlu - kolejna sztuka
        //    po cenie polki mniejszej o sztuki juz wziete, policzonej rachunkiem: polki NIE ruszamy przy wycenie, zdejmujemy dopiero
        //    przy dostawie) + chodzenie kowala po straganach: 0.2 dnia rzemieslnika (HistMasterWageT1, 3 d) x poziom plac miasta
        //    (TownWage) za sztuke; wszystko do kasy miasta (Pay.ToSettlement);
        //  - brak towaru: nic nie placisz, potrzeba idzie do warsztatow jako zamowienie w miescie (SupplyDemand, jak u lorda,
        //    ktory nie znalazl towaru: najwyzej 10 na rodzaj z jednej wizyty, raz na SupplyDemandOrderRepeatDays).

        private const float OrderLegworkDays = 0.2f;   // ulamek dnia rzemieslnika na jedna sprowadzona sztuke

        /// <summary>Chodzenie kowala za jedna sztuke (ulamek pensa): 0.2 dniowki rzemieslnika x poziom plac miasta.</summary>
        private static float OrderLegworkPerPiece(Settlement st)
        {
            var s = Settings.Current;
            return OrderLegworkDays * Math.Max(0f, s.HistMasterWageT1) * TownWage.Index(st);
        }

        /// <summary>Chodzenie kowala za k sztuk w calych pensach (za cale zamowienie; co najmniej 1 d, gdy cokolwiek przyniosl).</summary>
        private static int OrderLegwork(Settlement st, int k)
        {
            if (k <= 0) return 0;
            return Math.Max(1, (int)Math.Round(k * OrderLegworkPerPiece(st)));
        }

        private static string FeeText(Settlement st)
        {
            return OrderLegworkPerPiece(st).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Pieces(int k) { return k == 1 ? "1 piece" : k + " pieces"; }

        /// <summary>Sztuka z polki, ktora kowal kupi: Orderable (typ, tier, kupna), regula zbrojowni dla typu (CountsAsKit), nie unikat,
        /// nie kon (stajnia), w pelni sprawna - stan nie ponizej 100%.</summary>
        private static bool ShelfOrderable(EquipmentElement ee, ItemObject.ItemTypeEnum type, int tier)
        {
            var it = ee.Item;
            if (type == ItemObject.ItemTypeEnum.Horse || !Orderable(it, type, tier)) return false;
            if (!QuartermasterLaw.CountsAsKit(it, type) || ArmsPricing.IsUnique(it)) return false;
            var m = ee.ItemModifier;
            return m == null || m.PriceMultiplier >= 0.999f;
        }

        /// <summary>
        /// Cena kupna miasta za te sztuke, gdy zamowienie wzielo juz "taken" sztuk tego koszyka (typ x tier), o wartosci polki
        /// "takenWorth" w kategorii tej sztuki - BEZ ruszania polki. Liczy to samo co TownMarketData.GetPrice (jak ochotnicy, sakiewka
        /// ludzi i lordowie: model ceny z danymi kategorii, kupiec = osada, z podaza i popytem), tylko z zapasem pomniejszonym
        /// rachunkiem: wartosc polki kategorii minus takenWorth (tyle gra odejmuje przy zdjeciu - HistoricalPrices.ShelfWorth),
        /// sztuki koszyka dla SupplyDemand minus taken (SupplyDemand.Hold). Przy taken = 0 - dokladnie dzisiejsza cena polki.
        /// </summary>
        private static int ShelfPrice(Settlement st, EquipmentElement ee, int taken, int takenWorth)
        {
            int price;
            try
            {
                var cd = st.Town.MarketData.GetCategoryData(ee.Item.GetItemCategory());
                SupplyDemand.Hold(st.ItemRoster, ee.Item, taken);
                try { price = Campaign.Current.Models.TradeItemPriceFactorModel.GetPrice(ee, null, st.Party, false, cd.InStoreValue - takenWorth, cd.Supply, cd.Demand); }
                finally { SupplyDemand.Release(); }
            }
            catch { price = ee.ItemValue; }
            return Math.Max(1, price);
        }

        private struct ShelfPick { public EquipmentElement El; public int Price; }

        /// <summary>
        /// Plan zakupu kowala z POLKI st, BEZ ruszania polki: po jednej sztuce najtansza sprawna sztuka typu i tieru, ktorej na polce
        /// jeszcze zostalo (licznik wzietych na pozycje), po cenie polki pomniejszonej rachunkiem o sztuki juz wziete (ShelfPrice) -
        /// jak sakiewka ludzi i ekran handlu, gdzie kazda kolejna sztuka widzi mniejsza polke. purse >= 0: towar + chodzenie najwyzej
        /// tyle (shortPurse = przerwala kiesa, nie brak towaru). Ta sama funkcja dla wyceny i dostawy; zdejmuje dopiero DoOrderShelf.
        /// </summary>
        private static List<ShelfPick> ShelfPlan(Settlement st, ItemObject.ItemTypeEnum type, int tier, int n, int purse, out bool shortPurse)
        {
            var picks = new List<ShelfPick>();
            shortPurse = false;
            if (st == null || st.Town == null || st.ItemRoster == null || n <= 0) return picks;
            try
            {
                var shelf = st.ItemRoster;
                var used = new int[shelf.Count];                              // ile z pozycji i juz w planie
                var worth = new Dictionary<ItemCategory, int>();              // wartosc polki kategorii juz w planie
                int goods = 0;
                while (picks.Count < n)
                {
                    int best = -1, bestPrice = int.MaxValue;
                    for (int i = 0; i < used.Length; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        if (el.Amount - used[i] <= 0 || !ShelfOrderable(el.EquipmentElement, type, tier)) continue;
                        int w = 0; var cat = el.EquipmentElement.Item.GetItemCategory();
                        if (cat != null) worth.TryGetValue(cat, out w);
                        int price = ShelfPrice(st, el.EquipmentElement, picks.Count, w);
                        if (price < bestPrice) { bestPrice = price; best = i; }
                    }
                    if (best < 0) break;
                    if (purse >= 0 && goods + bestPrice + OrderLegwork(st, picks.Count + 1) > purse) { shortPurse = true; break; }
                    var ee = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                    used[best]++;
                    var c = ee.Item.GetItemCategory();
                    if (c != null) { int w0; worth.TryGetValue(c, out w0); worth[c] = w0 + HistoricalPrices.ShelfWorth(ee.Item); }
                    picks.Add(new ShelfPick { El = ee, Price = bestPrice });
                    goods += bestPrice;
                }
            }
            catch (Exception e) { Log.Error("ShelfPlan", e); }
            return picks;
        }

        private static int PickGoods(List<ShelfPick> picks, int k)
        {
            int g = 0;
            for (int i = 0; i < k && i < picks.Count; i++) g += picks[i].Price;
            return g;
        }

        /// <summary>"3 Leather Cap, 2 Padded Coif" - pierwsze k pozycji planu; ids: identyfikatory i ceny do logu ("3 leather_cap po 18-20").</summary>
        private static string PickNames(List<ShelfPick> picks, int k, bool ids)
        {
            var names = new List<string>(); var counts = new List<int>(); var lo = new List<int>(); var hi = new List<int>();
            for (int i = 0; i < k && i < picks.Count; i++)
            {
                var el = picks[i].El; int pr = picks[i].Price;
                string nm = ids ? el.Item.StringId + (el.ItemModifier != null ? "[" + el.ItemModifier.StringId + "]" : "") : el.Item.Name.ToString();
                int j = names.IndexOf(nm);
                if (j < 0) { names.Add(nm); counts.Add(1); lo.Add(pr); hi.Add(pr); }
                else { counts[j]++; lo[j] = Math.Min(lo[j], pr); hi[j] = Math.Max(hi[j], pr); }
            }
            var parts = new List<string>();
            for (int j = 0; j < names.Count; j++)
                parts.Add(counts[j] + " " + names[j] + (ids ? " po " + lo[j] + (hi[j] != lo[j] ? "-" + hi[j] : "") : ""));
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>Ile sprawnych sztuk typu i tieru lezy na polce i najtansza z nich - bez zmian w polce.</summary>
        private static int ShelfStock(Settlement st, ItemObject.ItemTypeEnum type, int tier, out EquipmentElement cheapest, out int price)
        {
            cheapest = default(EquipmentElement); price = 0;
            if (st == null || st.Town == null || st.ItemRoster == null) return 0;
            int n = 0, best = int.MaxValue;
            var shelf = st.ItemRoster;
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || !ShelfOrderable(el.EquipmentElement, type, tier)) continue;
                n += el.Amount;
                int p = ShelfPrice(st, el.EquipmentElement, 0, 0);
                if (p < best) { best = p; cheapest = el.EquipmentElement; }
            }
            if (n > 0) price = best;
            return n;
        }

        private static void OrderShelfTiers(ItemObject.ItemTypeEnum type, int shortage)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null || st.Town == null || st.ItemRoster == null) { Log.Player("There is no market here for the smith to buy from.", true); return; }
                string town = st.Name.ToString(), fee = FeeText(st);
                var elements = new List<InquiryElement>();
                for (int t = 1; t <= 6; t++)
                {
                    var rep = CheapestOf(type, t);
                    if (rep == null) continue;                       // takiego tieru nie ma w handlu nigdzie
                    EquipmentElement first; int price;
                    int stock = ShelfStock(st, type, t, out first, out price);
                    if (stock > 0)
                        elements.Add(new InquiryElement(t, "Tier " + t + " - " + first.Item.Name + ", " + price + " gold off the stall (" + stock + " on the stalls)",
                            ItemPic(first.Item), true, "The cheapest sound piece of its grade on the stalls of " + town + ", at the stall's own price - every further piece "
                            + "at the price of the emptier stall. The smith asks " + fee + " gold a piece for his legwork."));
                    else
                        elements.Add(new InquiryElement(t, "Tier " + t + " - none on the stalls of " + town,
                            ItemPic(rep), true, "Nothing of this grade lies on the stalls here. Ask anyway and the smith passes word of the want to the town's workshops - you pay nothing."));
                }
                if (elements.Count == 0) { Log.Player("No such kit is traded at any market the smith knows.", true); return; }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Order Ledger", "Which grade of " + type + "? The smith buys off the stalls of " + town + " at the stall's own price and asks " + fee
                    + " gold a piece for his legwork." + (shortage > 0 ? " The men are short " + shortage + "." : ""),
                    elements, true, 1, 1, "Choose", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            OrderShelfCount(type, (int)sel[0].Identifier, shortage);
                        }
                        catch (Exception ex) { Log.Error("OrderShelfTiers.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("OrderShelfTiers", e); }
        }

        private static void OrderShelfCount(ItemObject.ItemTypeEnum type, int tier, int shortage)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                var counts = new List<int> { 1, 5, 10 };
                if (shortage > 0 && !counts.Contains(shortage)) counts.Add(shortage);
                counts.Sort();
                bool sp;
                var plan = ShelfPlan(st, type, tier, counts[counts.Count - 1], -1, out sp);   // wycena - polki nie ruszamy
                if (plan.Count == 0) { OrderNoGoods(st, type, tier, Math.Max(1, shortage)); return; }
                string what = type + " (tier " + tier + ")";
                int gold = Hero.MainHero.Gold;
                var elements = new List<InquiryElement>();
                foreach (var n in counts)
                {
                    int k = Math.Min(n, plan.Count), goods = PickGoods(plan, k), fee = OrderLegwork(st, k), total = goods + fee;
                    string label = n + " x " + what + " - " + total + " gold" + (k < n ? "  (only " + k + " on the stalls)" : "")
                                 + (n == shortage ? "  (fills the shortage)" : "");
                    string hint = Pieces(k) + " for " + total + " gold - " + goods + " for the goods off the stall and " + fee + " for the smith's legwork: "
                                + PickNames(plan, k, false) + "." + (k < n ? " The stalls hold no more - word of the other " + (n - k) + " goes to the workshops." : "")
                                + (gold >= total ? "" : " Your purse comes up short.");
                    elements.Add(new InquiryElement(n, label, null, gold >= total, hint));
                }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    what + " off the stalls of " + st.Name, "Each piece at the stall's own price - the stall dearer by every piece it loses - and " + FeeText(st)
                    + " gold a piece for the smith's legwork, all into the town's coffers. The pieces land on the armoury racks.",
                    elements, true, 1, 1, "Order", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            int n = (int)sel[0].Identifier;
                            float hours = Math.Min(24f, 1f + n * 0.2f);
                            StartTimedWork(hours,
                                "The smith sends his boys round the stalls of " + st.Name + ".",
                                delegate { DoOrderShelf(type, tier, n); });
                        }
                        catch (Exception ex) { Log.Error("OrderShelfCount.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("OrderShelfCount", e); }
        }

        private static void DoOrderShelf(ItemObject.ItemTypeEnum type, int tier, int n)
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) { Log.Player("The armoury wagons are nowhere to be found.", true); return; }
                var st = Settlement.CurrentSettlement;
                bool shortPurse;
                var picks = ShelfPlan(st, type, tier, n, Hero.MainHero.Gold, out shortPurse);
                int k = picks.Count;
                if (k == 0 && !shortPurse) { OrderNoGoods(st, type, tier, n); return; }
                if (k == 0)
                {
                    Log.Player("Your purse came up short - the smith could not buy even one piece.", true);
                    LogShelfOrder(st, type, tier, n, picks, 0, 0, 0, true);
                    return;
                }
                int goods = PickGoods(picks, k), fee = OrderLegwork(st, k), total = goods + fee;
                Pay.ToSettlement(total);
                foreach (var p in picks) { st.ItemRoster.AddToCounts(p.El, -1); armory.AddToCounts(p.El, 1); }   // dostawa: dopiero teraz z polki na regaly
                int unmet = shortPurse ? 0 : n - k;   // za drogo to nie brak towaru (jak u lordow, wpis 81)
                NoteOrderUnmet(st, type, tier, unmet);
                Log.Player(Pieces(k) + " of " + type + " (tier " + tier + ") delivered to the men's racks for " + total + " gold - " + goods
                           + " for the goods off the stall and " + fee + " for the smith's legwork."
                           + (unmet > 0 ? " The stalls of " + st.Name + " had no more: word of the other " + unmet + " goes to the workshops." : "")
                           + (shortPurse ? " Your purse would stretch no further." : ""));
                LogShelfOrder(st, type, tier, n, picks, goods, fee, unmet, shortPurse);
            }
            catch (Exception e) { Log.Error("DoOrderShelf", e); }
        }

        /// <summary>Na polce nie ma ani jednej sprawnej sztuki tego typu i tieru: nic nie placisz, potrzeba idzie do warsztatow.</summary>
        private static void OrderNoGoods(Settlement st, ItemObject.ItemTypeEnum type, int tier, int n)
        {
            NoteOrderUnmet(st, type, tier, n);
            Log.Player("Not a single sound " + type + " of tier " + tier + " lies on the stalls of " + (st != null ? st.Name.ToString() : "this town")
                       + ". The smith passes word of the want of " + n + " to the town's workshops - you pay nothing.", true);
            LogShelfOrder(st, type, tier, n, null, 0, 0, n, false);
        }

        /// <summary>Niezaspokojona potrzeba = zamowienie w miescie (SupplyDemand) - jak lord, ktory nie znalazl towaru (AiGear):
        /// najwyzej 10 na rodzaj z jednej wizyty i raz na SupplyDemandOrderRepeatDays (zamowienie to potrzeba, nie licznik prob).</summary>
        private static void NoteOrderUnmet(Settlement st, ItemObject.ItemTypeEnum type, int tier, int missing)
        {
            if (st == null || missing <= 0) return;
            SupplyDemand.NoteUnmetOnce(MobileParty.MainParty, st, type, tier, Math.Min(10, missing));
        }

        /// <summary>Jedna linia logu na zamowienie: miasto, typ i tier, co przyniosl, towar + chodzenie, ile niezaspokojone.</summary>
        private static void LogShelfOrder(Settlement st, ItemObject.ItemTypeEnum type, int tier, int n, List<ShelfPick> picks, int goods, int fee, int unmet, bool shortPurse)
        {
            int k = picks != null ? picks.Count : 0;
            var rep = unmet > 0 ? CheapestOf(type, tier) : null;
            Log.Info("Zamowienie dla wojska (z polki): " + (st != null ? st.Name.ToString() : "?") + ", " + type + " t" + tier
                     + (k > 0 ? " [" + PickNames(picks, k, true) + "]" : " - brak na polce")
                     + ": dostarczono " + k + "/" + n + " szt., towar " + goods + " + oplata kowala " + fee + " = " + (goods + fee)
                     + " (poziom plac x" + TownWage.Index(st).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")"
                     + ", niezaspokojone " + unmet + (unmet > 0 ? " -> potrzeba dla warsztatow " + Math.Min(10, unmet) + " szt. (wzor " + (rep != null ? rep.StringId : "-") + ")" : "")
                     + (shortPurse ? ", kiesa nie starczyla" : ""));
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
                    item.Name + (Settings.Current.ForgeHireHistorical ? ". Forge hire " + Forge.ForgeFee(r) + " gold for every day of work. " : ". Forge fee " + Forge.ForgeFee(r) + " gold. ") + "Materials and stamina are spent now; " +
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
            // szansa odczytu wzoru to kosc, nie prog - stala skala trudnosci (pokretla Jeffa 07.10)
            need = Math.Max(0, (tier - 1) * (s != null ? s.SmithingDifficultyPerTier : 45));
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

                // UNIKAT (Jeff 31.08): jedyna sztuka na swiecie nie moze splonac
                // w partactwie. Rozbiorka wymaga Smithing 200 - ponizej progu
                // NIC sie nie dzieje (sztuka zostaje w sakwach); mistrz uczy sie
                // ZAWSZE (zadnego rzutu), sztuka przepada jak przy kazdej rozbiorce.
                if (UniqueGear.Is(item))
                {
                    int smithing = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
                    if (smithing < 200)
                    {
                        Log.Player("This famed piece is beyond your hands - Smithing 200 is needed before you dare take it apart (yours: " + smithing + ").");
                        return;
                    }
                    Helper.RemoveOne(roster, item);   // wpis 90: w dowolnym stanie (dotad zuzyty luk nie znikal - rozbiorka bez konca)
                    if (RangedLore.Learn(item))
                        Log.Player("You take the " + item.Name + " apart with a master's care - its making is yours now.");
                    Hero.MainHero.AddSkillXp(DefaultSkills.Crafting, Math.Max(20, tier * 20));
                    return;
                }

                Helper.RemoveOne(roster, item);   // rozlozona sztuka przepada tak czy siak (wpis 90: w dowolnym stanie)

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

        private static Action<float> _workPartial;

        private static void StartTimedWork(float hours, string label, Action apply, Action<float> partial)
        {
            StartTimedWork(hours, label, apply);
            _workPartial = partial;
        }

        private static void StartTimedWork(float hours, string label, Action apply)
        {
            _workPartial = null;
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
                if (_workLeave)
                {
                    _workLeave = false;
                    var part = _workPartial; float frac = _workTarget > 0f ? _workDone / _workTarget : 0f;
                    _workPartial = null;
                    GameMenu.SwitchToMenu(Menu);
                    if (part != null && frac > 0f) part(frac);   // wpis 84: gotowe sztuki zaplacone, reszta wraca nienaprawiona
                    return;
                }
                if (_workApply == null) return;
                _workDone += (float)dt.ToHours;
                if (_workDone < _workTarget) return;
                var apply = _workApply;
                _workApply = null; _workPartial = null;
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
                int hw = b.HarnessWrecks();   // paczka 158: wraki na grzbiecie (modyfikator wraku albo stan <= 10%)
                if (cost <= 0 && hw > 0)
                { args.IsEnabled = false; args.Tooltip = new TextObject("The smiths of " + TownName() + " restore none of your harness for coin." + WreckNote(hw)); return true; }
                if (cost <= 0) return false;
                if (MarketRule)
                {
                    // kowale miasta: robota jak dotad + material z targu wedle zuzycia; brak materialu - ta czesc czeka
                    var o = b.PlanRepair(null);
                    string town = TownName();
                    if (!o.Ok)
                    { args.IsEnabled = false; args.Tooltip = new TextObject("There are no town smiths here."); return true; }
                    if (o.Pieces == 0)
                    { args.IsEnabled = false; args.Tooltip = new TextObject("The smiths of " + town + " cannot mend your harness now." + o.LeftEn(town)); return true; }
                    args.Tooltip = new TextObject("The smiths of " + town + " will make " + o.Pieces + (o.Pieces == 1 ? " piece" : " pieces") + " of your harness sound again for "
                        + o.Total + " gold - " + o.Labor + " for their work and " + o.MatGold + " for materials from the market." + MendMaterial.WageNote(Settlement.CurrentSettlement) + o.LeftEn(town));
                    args.IsEnabled = Hero.MainHero.Gold >= o.Total;
                    return true;
                }
                args.Tooltip = new TextObject("The smith will make everything sound again for " + cost + " gold." + WreckNote(hw));
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
                if (MarketRule) pieces = Math.Max(1, ArmouryBehavior.Instance.PlanRepair(null).Pieces);   // godziny za czesci, ktore kowale wezma
                else pieces = Math.Max(1, pieces - ArmouryBehavior.Instance.HarnessWrecks());           // paczka 158: wrakow kowal nie bierze
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
