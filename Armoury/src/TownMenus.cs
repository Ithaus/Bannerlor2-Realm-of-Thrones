using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m95-menu (Jeff 20.10, zrzut menu miasta Seagard: "czy nie mozemy jakos posegregowac te opcje w miescie i ogolnie, jest tak duzo roznych opcji, np. spanie
    /// w tawernie, powinno byc go to inn district i wszystko tam powinno byc. Forge powinno byc w forge ... Trzeba to uporzadkowac.") - PORZADEK MENU MIASTA.
    /// Raz na sesje, PO rejestracji menu wszystkich modow i gry (SubModuleMain.OnAfterGameInitializationFinished - po OnSessionLaunched i OnAfterSessionLaunched
    /// wszystkich zachowan, przed pierwszym pokazaniem menu), opcje z menu gry "town" przechodza jako TE SAME obiekty GameMenuOption (warunek, skutek, napis,
    /// id bez zmian) do podmenu:
    ///  - karczma: podmenu gry "town_backstreet" ("Go to the tavern district"), przed "Back to town center" - nocleg (Armoury) i informatorzy (RB);
    ///  - kuznia: nowe podmenu "Go to the smithy" - kuznia gry (crafting), nasza kuznia, naprawa u kowala (RB), zbrojownia (DTE), mistrzowie Qohoru;
    ///  - praca: nowe podmenu "Look for paid work" - dniowka i warta nocna (RC);
    ///  - sprawy miasta: nowe podmenu "Town affairs" - ksiegi Armoury, Living Economy (BEE), Banner Kings, kupno domu (RC), pozyczka rady miasta,
    ///    turniej (GT), sprzet dla zalogi.
    /// Wejscia nowych podmenu stoja w menu miasta w miejscu dawnego "Enter smithy" i sa widoczne, gdy choc jedna opcja w srodku jest widoczna (warunki opcji
    /// wolane jak w grze, na swiezym MenuCallbackArgs - znaczniki zadan "!" przechodza na wejscie). Opis podmenu wymienia tylko opcje widoczne w tej chwili.
    /// Skutki przeniesionych opcji sprawdzone w dekompilacji: wracaja SwitchToMenu("town") albo do wlasnych podmenu - zadna nie zaklada menu nadrzednego.
    /// Nasze trzy powroty (karczma, kuznia, Qohor) wracaja do podmenu, z ktorego gracz przyszedl (BackTo).
    /// Klucz TownMenuTidy (wylaczony = nic sie nie rusza, menu jak dotad). Zmiana klucza w trakcie gry dziala od nastepnego wczytania (menu budowane raz na sesje;
    /// warunki wejsc NIE patrza na klucz, wiec wylaczenie w trakcie gry niczego nie chowa). Bez stanu w zapisie. Potkniecia liczone per opcja, nic sie nie gasi.
    /// Tylko miasto: zamek ("castle") i wies bez zmian.
    /// Poprawki po recenzjach: trzy podmenu rejestrowane zawsze (takze przy wylaczonym kluczu - zapis zrobiony w podmenu wczyta sie normalnie), podmenu maja
    /// tytul, napisy naszych wyjsc mowia, dokad prowadza (BackLabel), stan statyczny bez mocnych odwolan do menu i zachowan poprzedniej kampanii,
    /// blad przy linii logu po przeniesieniu nie gasi wejsc, opcje gry z GameMenuEventHandler("town", ...) nieprzenaszalne (straznik TownOnly).
    /// </summary>
    internal static class TownMenus
    {
        internal const string HubSmithy = "arm_tm_smithy", HubWork = "arm_tm_work", HubAffairs = "arm_tm_affairs";
        internal const string MenuSmithy = "arm_tm_smithy_menu", MenuWork = "arm_tm_work_menu", MenuAffairs = "arm_tm_affairs_menu";
        private const string Town = "town", Tavern = "town_backstreet", TavernBack = "town_backstreet_back", Anchor = "town_smithy";

        /// <summary>Wpis planu: id opcji w menu "town", menu docelowe, zrodlo (do logu), zdanie opisu podmenu (null - bez opisu, podmenu gry).</summary>
        private sealed class Move
        {
            public readonly string Id, To, Who, Line;
            public readonly bool Ours;
            public Move(string id, string to, string who, string line)
            {
                Id = id; To = to; Who = who; Line = line;
                Ours = who == "Armoury" || who == "RC" || who == "GT";
            }
        }

        // Opcje gry, ktorych prawdziwy skutek siedzi w GameMenuEventHandler("town", id) (TaleWorlds.CampaignSystem PlayerTownVisit i
        // PlayerTownVisitCampaignBehavior; GameMenuCallbackManager szuka handlera po id BIEZACEGO menu) - poza menu "town" bylyby widoczne, ale martwe.
        // NIE przenosic: "Manage town" (skutek w AddGameMenuOption = null), "Manage town (Cheat)", "Recruit troops". Straznik w Apply pomija je i loguje,
        // gdyby ktos dopisal je do planu.
        private static readonly HashSet<string> TownOnly = new HashSet<string> { "manage_production", "manage_production_cheat", "recruit_volunteers" };

        // kolejnosc w tablicy = kolejnosc w podmenu
        private static readonly Move[] Plan =
        {
            // karczma - podmenu gry
            new Move("arm_inn_enter", Tavern, "Armoury", null),
            new Move("espionage_contact_informants", Tavern, "RB", null),
            // kuznia
            new Move("town_smithy", MenuSmithy, "gra", "Enter smithy - the crafting bench: smith weapons, smelt and refine metal."),
            new Move("arm_enter", MenuSmithy, "Armoury", "Work the forge - your own hand at the anvil: armour, shields and tack, the mending bench, the lords' orders."),
            new Move("repair_equipment_town", MenuSmithy, "RB", "The local blacksmith - pay him to repair the gear of you and your companions."),
            new Move("army_armory_manage", MenuSmithy, "DTE", "Manage Armory - the store of arms and armour your troops draw their kit from."),
            new Move("arm_qohor_enter", MenuSmithy, "Armoury", "The masters of Qohor - Valyrian steel reforged to your order."),
            // praca
            new Move("rc_work_labour_town", MenuWork, "RC", "Day labour - dig, carry and haul from dawn to dusk for a day's wage."),
            new Move("rc_work_guard", MenuWork, "RC", "Night watch - walk the walls and gates by night; the merchants pay by the watch."),
            // sprawy miasta i ksiegi
            new Move("arm_people_town", MenuAffairs, "Armoury", "People of this land - who lives here, who serves under arms, who is held or outlawed."),
            new Move("arm_estates_town", MenuAffairs, "Armoury", "Estates and livelihood - the estates of this place and how well they live."),
            new Move("arm_realm_ledger_town", MenuAffairs, "Armoury", "Realm ledger - the estates and plenty of the realm, its regions and the world."),
            new Move("bee_town_root", MenuAffairs, "BEE", "Living Economy - the town's treasury, market and works."),
            new Move("bannerkings_submenu", MenuAffairs, "BK", "Banner Kings - demesne, titles and the town's people."),
            new Move("rc_home_buy_town", MenuAffairs, "RC", "Buy a house - a roof of your own, with a strongbox and a chest."),
            new Move("arm_townloan_borrow_town", MenuAffairs, "Armoury", "Borrow from the town council - a loan from the townsfolk's purse."),
            new Move("arm_townloan_repay_town", MenuAffairs, "Armoury", "Repay the town council - pay back what you owe."),
            new Move("arm_towndebt_town", MenuAffairs, "Armoury", "The council's debts - what the town owes, to whom, and what it costs."),   // m96-dlugi
            new Move("gt_proclaim_tourney", MenuAffairs, "GT", "Proclaim a tourney - call the knights of the realm to your lists."),
            new Move("arm_garrison_kit_town", MenuAffairs, "Armoury", "Hand kit to the garrison - arms and armour for the men who hold the town."),
        };

        /// <summary>Nowe podmenu z wejsciem w menu miasta: przeniesione opcje (te same obiekty) i ich zdania opisu.</summary>
        private sealed class Hub
        {
            public readonly string Opt, Menu, Label, Title, Head, TextVar;
            public readonly GameMenuOption.LeaveType Icon;
            public readonly List<GameMenuOption> Kids = new List<GameMenuOption>();
            public readonly List<string> Lines = new List<string>();     // zdanie na opcje (ta sama kolejnosc co Kids; null - bez zdania)

            public Hub(string opt, string menu, string label, string title, string head, string textVar, GameMenuOption.LeaveType icon)
            { Opt = opt; Menu = menu; Label = label; Title = title; Head = head; TextVar = textVar; Icon = icon; }

            /// <summary>Wejscie widoczne, gdy choc jedna opcja w srodku jest widoczna; znaczniki zadan opcji przechodza na wejscie, podpowiedz wymienia widoczne.</summary>
            public bool Condition(MenuCallbackArgs args)
            {
                args.optionLeaveType = Icon;
                if (!_on || Kids.Count == 0) return false;
                bool any = false;
                var names = new List<string>(Kids.Count);
                for (int i = 0; i < Kids.Count; i++)
                {
                    GameMenuOption.IssueQuestFlags f;
                    if (!Holds(Kids[i], args.MenuContext, out f)) continue;
                    any = true;
                    args.OptionQuestData |= f;
                    try { names.Add(Kids[i].Text != null ? Kids[i].Text.ToString() : Kids[i].IdString); } catch { names.Add(Kids[i].IdString); }
                }
                if (any) args.Tooltip = new TextObject("{=!}" + string.Join(", ", names.ToArray()) + ".");
                return any;
            }

            public void Consequence(MenuCallbackArgs args) { GameMenu.SwitchToMenu(Menu); }   // zwykle menu (nie menu oczekiwania) - SwitchToMenu wolno

            /// <summary>Opis podmenu: naglowek i zdanie dla kazdej opcji widocznej w tej chwili; tlo jak w karczmie (WaitMeshName osady); tytul paska
            /// (GameMenu.RunOnInit przepisuje args.MenuTitle do menu - bez niego pasek pusty; gra: "Town Center", "Backstreet").</summary>
            public void Init(MenuCallbackArgs args)
            {
                try { args.MenuTitle = new TextObject("{=!}" + Title); } catch { }
                try
                {
                    var st = Settlement.CurrentSettlement;
                    try { if (st != null && st.SettlementComponent != null && !string.IsNullOrEmpty(st.SettlementComponent.WaitMeshName)) args.MenuContext.SetBackgroundMeshName(st.SettlementComponent.WaitMeshName); } catch { }
                    var sb = new StringBuilder(Head);
                    bool first = true;
                    for (int i = 0; i < Kids.Count; i++)
                    {
                        if (Lines[i] == null) continue;
                        GameMenuOption.IssueQuestFlags f;
                        if (!Holds(Kids[i], args.MenuContext, out f)) continue;
                        sb.Append(first ? "\n\n" : "\n").Append(Lines[i]);
                        first = false;
                    }
                    MBTextManager.SetTextVariable(TextVar, sb.ToString());
                }
                catch (Exception e)
                {
                    Stumble("opis " + Menu, e);
                    try { MBTextManager.SetTextVariable(TextVar, Head); } catch { }
                }
            }
        }

        // Stan statyczny bez mocnych odwolan do menu i opcji kampanii (recenzja: dotad _hubs z opcjami - delegaty do zachowan - i menedzer menu trzymaly
        // poprzednia kampanie az do nastepnego Apply). Obiekty Hub zyja tylko w delegatach swoich menu (gina z GameMenuManager kampanii).
        private static bool _on;                                                   // ta sesja: opcje przeniesione, wejscia wpiete
        private static readonly Dictionary<string, string> _where = new Dictionary<string, string>();   // id przeniesionej opcji -> menu, w ktorym teraz stoi
        private static readonly Dictionary<string, int> _st = new Dictionary<string, int>();             // potkniecia per opcja / miejsce
        private static int _stAll;
        private static System.Reflection.FieldInfo _itemsField;
        private static WeakReference<GameMenuManager> _doneFor;                    // menedzer menu, w ktorym juz zrobiono (drugie wolanie w tej samej sesji - nic)

        private static void Reset()
        {
            _on = false; _where.Clear(); _st.Clear(); _stAll = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stAll++;
            int n; _st.TryGetValue(where, out n); n++; _st[where] = n;
            if (n <= 3 || n % 200 == 0) Log.Error("TownMenus." + where + " (potkniecie " + n + ")", e);
        }

        /// <summary>Warunek opcji wolany jak w grze (GameMenuOption.GetConditionsHold), ale na swiezym MenuCallbackArgs - stan opcji nie jest ruszany.
        /// Wyjatek w cudzym warunku: liczony per opcja, opcja traktowana jako widoczna (nic nie znika).</summary>
        private static bool Holds(GameMenuOption o, MenuContext mc, out GameMenuOption.IssueQuestFlags flags)
        {
            flags = GameMenuOption.IssueQuestFlags.None;
            if (o == null) return false;
            if (o.OnCondition == null) return true;
            try
            {
                var a = new MenuCallbackArgs(mc, o.Text);
                bool r = o.OnCondition(a);
                flags = a.OptionQuestData;
                return r;
            }
            catch (Exception e) { Stumble("warunek " + o.IdString, e); return true; }
        }

        /// <summary>Lista opcji menu (GameMenu._menuItems, prywatna w 1.4.8 - MenuOptions to tylko IEnumerable).</summary>
        private static List<GameMenuOption> Items(GameMenu menu)
        {
            if (menu == null) return null;
            if (_itemsField == null) _itemsField = AccessTools.Field(typeof(GameMenu), "_menuItems");
            return _itemsField != null ? _itemsField.GetValue(menu) as List<GameMenuOption> : null;
        }

        private static int IndexOf(List<GameMenuOption> items, string id)
        {
            for (int i = 0; i < items.Count; i++) if (items[i] != null && items[i].IdString == id) return i;
            return -1;
        }

        private static int FirstLeave(List<GameMenuOption> items)
        {
            for (int i = 0; i < items.Count; i++) if (items[i] != null && items[i].IsLeave) return i;
            return -1;
        }

        /// <summary>Menu, do ktorego wraca nasza opcja "wyjdz" (karczma, kuznia, Qohor): podmenu, w ktorym stoi jej wejscie, albo "town" (klucz wylaczony /
        /// nie przeniesiona / podmenu nie ma w biezacej kampanii - np. stan z poprzedniej sesji, gdyby Apply nie przeszlo).</summary>
        internal static string BackTo(string enterOptionId)
        {
            string m;
            if (!_on || enterOptionId == null || !_where.TryGetValue(enterOptionId, out m) || string.IsNullOrEmpty(m)) return Town;
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                if (mgr == null || mgr.GetGameMenu(m) == null) return Town;
            }
            catch { return Town; }
            return m;
        }

        /// <summary>Recenzja (gracz): napis naszego wyjscia mowi, dokad prowadzi - przy przeniesionym wejsciu nazwa podmenu, inaczej napis jak dotad.
        /// Wolane z warunku opcji (gra liczy warunek przed odczytem napisu - GameMenuVM.Refresh; ten sam wzor co CrimeCampaignBehavior).</summary>
        internal static void SetBackLabel(MenuCallbackArgs args, string textVar, string enterOptionId, string asBefore)
        {
            string label = asBefore;
            try
            {
                switch (BackTo(enterOptionId))
                {
                    case Tavern: label = "Back to the tavern district"; break;
                    case MenuSmithy: label = "Back to the smithy"; break;
                    case MenuWork: label = "Back to paid work"; break;
                    case MenuAffairs: label = "Back to town affairs"; break;
                }
            }
            catch { label = asBefore; }
            try { if (args != null && args.Text != null) args.Text.SetTextVariable(textVar, label); } catch { }
        }

        /// <summary>
        /// SubModuleMain.OnAfterGameInitializationFinished (kampania): przeniesienie opcji menu miasta i wpiecie wejsc podmenu. Raz na sesje (nowe GameMenuManager
        /// przy kazdym wczytaniu - stan od zera). Trzy podmenu rejestrowane zawsze, takze przy wylaczonym kluczu. Jedna linia logu "Menu miasta (m95-menu): start".
        /// Nie rzuca.
        /// </summary>
        internal static void Apply(CampaignGameStarter starter)
        {
            const string head = "Menu miasta (m95-menu): start | ";
            bool committed = false;
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                GameMenuManager done;
                if (mgr != null && _doneFor != null && _doneFor.TryGetTarget(out done) && ReferenceEquals(mgr, done)) { Log.Info(head + "drugie wolanie w tej samej sesji - juz zrobione, bez zmian."); return; }
                Reset();
                if (starter == null || mgr == null) { Log.Info(head + "BRAK startera albo GameMenuManager - nic nie przeniesione, menu jak dotad."); return; }

                var hubs = new[]
                {
                    new Hub(HubSmithy, MenuSmithy, "{=!}Go to the smithy", "Smithy", "The smiths' street - hammers ring from every door.", "ARM_TM_SMITHY_TEXT", GameMenuOption.LeaveType.Craft),
                    new Hub(HubWork, MenuWork, "{=!}Look for paid work", "Paid Work", "There is always work in a town for willing hands.", "ARM_TM_WORK_TEXT", GameMenuOption.LeaveType.Wait),
                    new Hub(HubAffairs, MenuAffairs, "{=!}Town affairs", "Town Affairs", "The town's people, its books and its dealings.", "ARM_TM_AFFAIRS_TEXT", GameMenuOption.LeaveType.Manage),
                };

                // 0) trzy podmenu z "Back to town center" (jak w podmenu gry) - ZAWSZE, takze przy wylaczonym kluczu i pustym podmenu (recenzja): zapis zrobiony
                //    w podmenu i wczytany po wylaczeniu klucza otwiera to podmenu (SandBoxGameManager.OnLoadFinished: GetGameMenu != null -> SetNextMenu), a "Back"
                //    wraca do miasta; bez rejestracji gra konczyla spotkanie (PlayerEncounter.Finish) i druzyna stala na mapie. Menu miasta jeszcze nieruszone.
                var subLists = new Dictionary<string, List<GameMenuOption>>();
                foreach (var h in hubs)
                {
                    starter.AddGameMenu(h.Menu, "{=!}{" + h.TextVar + "}", h.Init, GameMenu.MenuOverlayType.SettlementWithBoth);
                    starter.AddGameMenuOption(h.Menu, h.Menu + "_back", "{=qWAmxyYz}Back to town center",
                        delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                        delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Town); }, true, -1);
                    var list = Items(mgr.GetGameMenu(h.Menu));
                    if (list == null) throw new InvalidOperationException("brak listy opcji nowego podmenu " + h.Menu + " - nic nie przeniesione");
                    subLists[h.Menu] = list;
                }
                _doneFor = new WeakReference<GameMenuManager>(mgr);   // podmenu zarejestrowane - drugie wolanie w tej sesji nic nie robi (bez podwojnego "Back")

                var s = Settings.Current;
                if (s == null || !s.TownMenuTidy) { Log.Info(head + "WYLACZONY w MCM (Town Menu Tidy) - menu miasta jak dotad, nic nie przeniesione (puste podmenu zarejestrowane tylko dla zapisow zrobionych w podmenu)."); return; }
                var townItems = Items(mgr.GetGameMenu(Town));
                if (townItems == null) { Log.Info(head + "BRAK menu 'town' albo listy opcji (GameMenu._menuItems) - nic nie przeniesione, menu jak dotad."); return; }
                var tavernItems = Items(mgr.GetGameMenu(Tavern));

                // 1) co przenosimy (kolejnosc planu); kotwica wejsc - miejsce "Enter smithy" przed zmianami
                int anchor = IndexOf(townItems, Anchor);
                int before = 0;
                var picked = new List<KeyValuePair<Move, GameMenuOption>>();
                var missOurs = new List<string>();
                var missForeign = new List<string>();
                var pinned = new List<string>();
                int kept = 0;
                foreach (var m in Plan)
                {
                    if (TownOnly.Contains(m.Id)) { pinned.Add(m.Id + " (" + m.Who + ")"); continue; }   // skutek w GameMenuEventHandler("town") - zostaje
                    int at = IndexOf(townItems, m.Id);
                    if (at < 0) { (m.Ours ? missOurs : missForeign).Add(m.Id + " (" + m.Who + ")"); continue; }
                    if (m.To == Tavern && tavernItems == null) { kept++; continue; }   // brak podmenu gry - opcja zostaje w miescie
                    if (anchor >= 0 && at < anchor) before++;
                    picked.Add(new KeyValuePair<Move, GameMenuOption>(m, townItems[at]));
                }
                string pinnedNote = pinned.Count > 0 ? " | nieprzenaszalne (GameMenuEventHandler 'town'), zostaja w miescie " + pinned.Count + " [" + string.Join(", ", pinned.ToArray()) + "]" : "";

                if (picked.Count == 0) { Log.Info(head + "WLACZONY, ale zadnej opcji z planu nie ma w menu 'town' - nic nie przeniesione | nie znaleziono: " + string.Join(", ", missForeign.ToArray()) + "; " + string.Join(", ", missOurs.ToArray()) + pinnedNote); return; }

                // 2) przygotowanie bez ruszania menu miasta: listy opcji podmenu, miejsce w karczmie
                foreach (var kv in picked)
                    if (kv.Key.To != Tavern) { var h = HubOf(hubs, kv.Key.To); h.Kids.Add(kv.Value); h.Lines.Add(kv.Key.Line); }
                int tavernAt = -1;
                if (tavernItems != null)
                {
                    tavernAt = IndexOf(tavernItems, TavernBack);
                    if (tavernAt < 0) tavernAt = FirstLeave(tavernItems);
                    if (tavernAt < 0) tavernAt = tavernItems.Count;
                }
                int pos = anchor >= 0 ? anchor - before : -1;

                // 3) zmiana (te same obiekty GameMenuOption); kopie list - przy bledzie w polowie wszystko wraca na miejsce (nic nie znika)
                var townCopy = new List<GameMenuOption>(townItems);
                var tavernCopy = tavernItems != null ? new List<GameMenuOption>(tavernItems) : null;
                var subCopies = new Dictionary<string, List<GameMenuOption>>();
                foreach (var kv in subLists) subCopies[kv.Key] = new List<GameMenuOption>(kv.Value);
                int toTavern = 0, hubsIn = 0;
                try
                {
                    foreach (var kv in picked)
                    {
                        townItems.Remove(kv.Value);
                        if (kv.Key.To == Tavern) { tavernItems.Insert(Math.Min(tavernAt, tavernItems.Count), kv.Value); tavernAt++; toTavern++; }
                        _where[kv.Key.Id] = kv.Key.To;
                    }
                    foreach (var h in hubs)
                    {
                        if (h.Kids.Count == 0) continue;
                        subLists[h.Menu].InsertRange(0, h.Kids);    // opcje przed "Back to town center"
                    }
                    // wejscia: w miejscu dawnego "Enter smithy" (bez niego - przed "Wait here", przed pierwszym "Leave", na koncu)
                    if (pos < 0) pos = IndexOf(townItems, "town_wait");
                    if (pos < 0) pos = FirstLeave(townItems);
                    if (pos < 0 || pos > townItems.Count) pos = townItems.Count;
                    foreach (var h in hubs)
                    {
                        if (h.Kids.Count == 0) continue;
                        starter.AddGameMenuOption(Town, h.Opt, h.Label, h.Condition, h.Consequence, false, pos);
                        pos++; hubsIn++;
                    }
                    _on = true;
                    committed = true;
                }
                catch (Exception e)
                {
                    townItems.Clear(); townItems.AddRange(townCopy);
                    if (tavernCopy != null) { tavernItems.Clear(); tavernItems.AddRange(tavernCopy); }
                    foreach (var kv in subCopies) { subLists[kv.Key].Clear(); subLists[kv.Key].AddRange(kv.Value); }   // podmenu: tylko "Back" (opcje nie w dwoch miejscach)
                    _where.Clear(); _on = false;
                    Stumble("Apply (cofniete)", e);
                    Log.Info(head + "BLAD przy przenoszeniu (" + e.Message + ") - menu miasta i karczmy przywrocone jak dotad, nic nie przeniesione.");
                    return;
                }

                // linia startu: co dokad, czego nie znaleziono, menu miasta po zmianie. Wlasny try (recenzja): blad tutaj nie moze cofnac stanu po
                // zatwierdzeniu - opcje juz stoja w podmenu, zgaszenie _on schowaloby wejscia do konca sesji.
                try
                {
                    var moved = new StringBuilder();
                    moved.Append("do karczmy (town_backstreet) ").Append(toTavern).Append(" [").Append(Ids(picked, Tavern)).Append("]");
                    foreach (var h in hubs)
                        moved.Append("; ").Append(h.Menu).Append(' ').Append(h.Kids.Count).Append(" [").Append(Ids(picked, h.Menu)).Append("]");
                    var now = new List<string>(townItems.Count);
                    foreach (var o in townItems) now.Add(o != null ? o.IdString : "?");
                    Log.Info(head + "WLACZONY (Town Menu Tidy) | przeniesione " + picked.Count + " z " + Plan.Length + " w planie: " + moved
                             + " | wejscia podmenu wpiete " + hubsIn + " (miejsce dawnego 'Enter smithy' " + (anchor >= 0 ? "index " + anchor : "BRAK - przed Wait/Leave") + ")"
                             + " | nie znaleziono cudzych " + missForeign.Count + (missForeign.Count > 0 ? " [" + string.Join(", ", missForeign.ToArray()) + "]" : "")
                             + ", naszych " + missOurs.Count + (missOurs.Count > 0 ? " [" + string.Join(", ", missOurs.ToArray()) + "]" : "")
                             + (kept > 0 ? " | zostalo w miescie (brak podmenu town_backstreet) " + kept : "")
                             + pinnedNote
                             + " | menu miasta teraz " + now.Count + " opcji: " + string.Join(", ", now.ToArray()));
                }
                catch (Exception e) { Stumble("linia startu", e); }
            }
            catch (Exception e)
            {
                // blad przed zmiana menu miasta (przygotowanie) - menu jak dotad. Po zatwierdzeniu stanu nie ruszamy (wejscia musza zostac).
                if (!committed) { _on = false; _where.Clear(); }
                Stumble("Apply", e);
                Log.Info(head + (committed
                    ? "BLAD po przeniesieniu (" + e.Message + ") - przeniesienie zostaje, wejscia podmenu dzialaja."
                    : "BLAD przed przenoszeniem (" + e.Message + ") - menu miasta jak dotad, nic nie przeniesione."));
            }
        }

        private static Hub HubOf(Hub[] hubs, string menu)
        {
            foreach (var h in hubs) if (h.Menu == menu) return h;
            throw new InvalidOperationException("nieznane podmenu " + menu);
        }

        private static string Ids(List<KeyValuePair<Move, GameMenuOption>> picked, string to)
        {
            var ids = new List<string>();
            foreach (var kv in picked) if (kv.Key.To == to) ids.Add(kv.Key.Id);
            return string.Join(", ", ids.ToArray());
        }
    }
}
