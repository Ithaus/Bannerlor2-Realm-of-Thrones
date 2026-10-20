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
    /// m96-zamek (Jeff 20.10, po porzadku menu miasta m95-menu: "menu zamku tez uporzadkuj") - PORZADEK MENU ZAMKU, ten sam wzor co TownMenus.
    /// Raz na sesje, PO rejestracji menu wszystkich modow i gry (SubModuleMain.OnAfterGameInitializationFinished - po OnSessionLaunched i OnAfterSessionLaunched
    /// wszystkich zachowan, przed pierwszym pokazaniem menu; zaraz po TownMenus.Apply), opcje z menu gry "castle" przechodza jako TE SAME obiekty GameMenuOption
    /// (warunek, skutek, napis, id bez zmian) do dwoch nowych podmenu:
    ///  - "Go to the barracks" (koszary i zbrojownia): zarzadzanie zaloga i oddanie ludzi do zalogi (gra - w miescie te dwie opcje gra trzyma w "Go to the keep"),
    ///    sprzet dla zalogi (Armoury), naprawa u kowala zamku (RB);
    ///  - "Castle affairs" (sprawy zamku): ksiegi Armoury (People of this land, Estates and livelihood, Realm ledger), Living Economy (BEE), Banner Kings (BK - w
    ///    zamku takze werbunek), pozyczka i splata u ludzi zamku (Armoury).
    /// W menu zamku zostaja miejsca i rzeczy pana: loch, sala lorda, rozmowa z kasztelanem (ROT), spacer, "Manage castle" (skutek w GameMenuEventHandler("castle") -
    /// nieprzenaszalne, straznik CastleOnly), schowek, czekanie, powrot do armii, wyjazd i opcje zadan.
    /// Wejscia podmenu stoja przed "Wait here for some time" i sa widoczne, gdy choc jedna opcja w srodku jest widoczna (warunki opcji wolane jak w grze, na swiezym
    /// MenuCallbackArgs - znaczniki zadan "!" przechodza na wejscie). Opis podmenu wymienia tylko opcje widoczne w tej chwili. Kazde podmenu ma "Back to the castle".
    /// Skutki przeniesionych opcji sprawdzone w dekompilacji: ekrany gry (oddzialy, zaloga, ekwipunek), okna (ShowInquiry) albo SwitchToMenu do wlasnych menu
    /// (BK "bannerkings" i BEE "bee_castle_menu" wracaja SwitchToMenu("castle"); RB po zaplacie SwitchToMenu("town") - stary blad RB, jak dotad) - zadna nie zaklada
    /// menu nadrzednego, zaden warunek nie patrzy na id biezacego menu. Zadna nasza opcja w zamku nie otwiera naszego menu - BackTo jak w miescie nie jest potrzebne.
    /// Klucz CastleMenuTidy (wylaczony = nic sie nie rusza, menu jak dotad; od nastepnego wczytania). Dwa podmenu rejestrowane zawsze, takze przy wylaczonym kluczu
    /// (zapis zrobiony w podmenu wczyta sie normalnie - puste podmenu z "Back to the castle"). Bez stanu w zapisie, bez latek Harmony (tylko odczyt prywatnego pola
    /// GameMenu._menuItems przez AccessTools). Potkniecia liczone per opcja / miejsce, nic sie nie gasi. Tylko zamek: wies bez zmian, miasto - TownMenus.
    /// </summary>
    internal static class CastleMenus
    {
        internal const string HubBarracks = "arm_cm_barracks", HubAffairs = "arm_cm_affairs";
        internal const string MenuBarracks = "arm_cm_barracks_menu", MenuAffairs = "arm_cm_affairs_menu";
        private const string Castle = "castle", Anchor = "town_wait";

        /// <summary>Wpis planu: id opcji w menu "castle", menu docelowe, zrodlo (do logu), zdanie opisu podmenu.</summary>
        private sealed class Move
        {
            public readonly string Id, To, Who, Line;
            public readonly bool Ours;
            public Move(string id, string to, string who, string line)
            {
                Id = id; To = to; Who = who; Line = line;
                Ours = who == "Armoury";
            }
        }

        // Opcje gry, ktorych prawdziwy skutek siedzi w GameMenuEventHandler("castle", id) (TaleWorlds.CampaignSystem PlayerTownVisit; GameMenuCallbackManager szuka
        // handlera po id BIEZACEGO menu) - poza menu "castle" bylyby widoczne, ale martwe. Sprawdzone w dekompilacji (atrybuty w CampaignSystem, SandBox, StoryMode,
        // NavalDLC, ROT, Armoury): jedyna taka opcja zamku to "Manage castle" (manage_production, skutek w AddGameMenuOption = null). Straznik w Apply pomija je
        // i loguje, gdyby ktos dopisal je do planu.
        private static readonly HashSet<string> CastleOnly = new HashSet<string> { "manage_production" };

        // kolejnosc w tablicy = kolejnosc w podmenu
        private static readonly Move[] Plan =
        {
            // koszary i zbrojownia
            new Move("manage_garrison", MenuBarracks, "gra", "Manage garrison - the men who hold this castle for you: move troops in and out."),
            new Move("leave_troops_to_garrison", MenuBarracks, "gra", "Donate troops - leave some of your men to strengthen the castle's garrison."),
            new Move("arm_garrison_kit_castle", MenuBarracks, "Armoury", "Hand kit to the garrison - arms and armour for the men who hold the castle."),
            new Move("repair_equipment_castle", MenuBarracks, "RB", "The castle smith - pay him to repair the gear of you and your companions."),
            // sprawy zamku i ksiegi
            new Move("arm_people_castle", MenuAffairs, "Armoury", "People of this land - who lives here, who serves under arms, who is held or outlawed."),
            new Move("arm_estates_castle", MenuAffairs, "Armoury", "Estates and livelihood - the estates of this place and how well they live."),
            new Move("arm_realm_ledger_castle", MenuAffairs, "Armoury", "Realm ledger - the estates and plenty of the realm, its regions and the world."),
            new Move("bee_castle_root", MenuAffairs, "BEE", "Living Economy - the castle's purse, stores and works, and the men it trains."),
            new Move("bannerkings_castle_submenu", MenuAffairs, "BK", "Banner Kings - demesne, titles, the castle's people and its recruits."),
            new Move("arm_townloan_borrow_castle", MenuAffairs, "Armoury", "Borrow from the castle folk - a loan from the castle folk's purse."),
            new Move("arm_townloan_repay_castle", MenuAffairs, "Armoury", "Repay the castle folk - pay back what you owe."),
        };

        /// <summary>Nowe podmenu z wejsciem w menu zamku: przeniesione opcje (te same obiekty) i ich zdania opisu.</summary>
        private sealed class Hub
        {
            public readonly string Opt, Menu, Label, Title, Head, TextVar;
            public readonly GameMenuOption.LeaveType Icon;
            public readonly List<GameMenuOption> Kids = new List<GameMenuOption>();
            public readonly List<string> Lines = new List<string>();     // zdanie na opcje (ta sama kolejnosc co Kids)

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

            /// <summary>Opis podmenu: naglowek i zdanie dla kazdej opcji widocznej w tej chwili; tlo osady (WaitMeshName - jak menu zamku gry); tytul paska
            /// (GameMenu.RunOnInit przepisuje args.MenuTitle do menu - bez niego pasek pusty; gra: "Castle").</summary>
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

        // Stan statyczny bez mocnych odwolan do menu i opcji kampanii (pulapka z recenzji m95-menu): obiekty Hub zyja tylko w delegatach swoich menu
        // (gina z GameMenuManager kampanii), menedzer menu jako WeakReference.
        private static bool _on;                                                   // ta sesja: opcje przeniesione, wejscia wpiete
        private static readonly Dictionary<string, int> _st = new Dictionary<string, int>();             // potkniecia per opcja / miejsce
        private static int _stAll;
        private static System.Reflection.FieldInfo _itemsField;
        private static WeakReference<GameMenuManager> _doneFor;                    // menedzer menu, w ktorym juz zrobiono (drugie wolanie w tej samej sesji - nic)

        private static void Reset()
        {
            _on = false; _st.Clear(); _stAll = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stAll++;
            int n; _st.TryGetValue(where, out n); n++; _st[where] = n;
            if (n <= 3 || n % 200 == 0) Log.Error("CastleMenus." + where + " (potkniecie " + n + ")", e);
        }

        /// <summary>Warunek opcji wolany jak w grze (GameMenuOption.GetConditionsHold), ale na swiezym MenuCallbackArgs - stan opcji (IsEnabled, podpowiedz)
        /// nie jest ruszany. Wyjatek w cudzym warunku: liczony per opcja, opcja traktowana jako widoczna (nic nie znika).</summary>
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

        private static string IdList(List<GameMenuOption> items)
        {
            var ids = new List<string>(items.Count);
            foreach (var o in items) ids.Add(o != null ? o.IdString : "?");
            return string.Join(", ", ids.ToArray());
        }

        /// <summary>
        /// SubModuleMain.OnAfterGameInitializationFinished (kampania, po TownMenus.Apply): przeniesienie opcji menu zamku i wpiecie wejsc podmenu. Raz na sesje (nowe
        /// GameMenuManager przy kazdym wczytaniu - stan od zera). Dwa podmenu rejestrowane zawsze, takze przy wylaczonym kluczu. Jedna linia logu
        /// "Menu zamku (m96-zamek): start". Nie rzuca.
        /// </summary>
        internal static void Apply(CampaignGameStarter starter)
        {
            const string head = "Menu zamku (m96-zamek): start | ";
            bool committed = false;
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                GameMenuManager done;
                if (mgr != null && _doneFor != null && _doneFor.TryGetTarget(out done) && ReferenceEquals(mgr, done)) { Log.Info(head + "drugie wolanie w tej samej sesji - juz zrobione, bez zmian."); return; }
                Reset();
                if (starter == null || mgr == null) { Log.Info(head + "BRAK startera albo GameMenuManager - nic nie przeniesione, menu zamku jak dotad."); return; }

                var hubs = new[]
                {
                    new Hub(HubBarracks, MenuBarracks, "{=!}Go to the barracks", "Barracks", "The barracks and the armoury, where the castle's men-at-arms sleep, drill and keep their kit.", "ARM_CM_BARRACKS_TEXT", GameMenuOption.LeaveType.ManageGarrison),
                    new Hub(HubAffairs, MenuAffairs, "{=!}Castle affairs", "Castle Affairs", "The castle's people, its books and its dealings.", "ARM_CM_AFFAIRS_TEXT", GameMenuOption.LeaveType.Manage),
                };

                // 0) dwa podmenu z "Back to the castle" - ZAWSZE, takze przy wylaczonym kluczu i pustym podmenu (pulapka z recenzji m95-menu): zapis zrobiony w podmenu
                //    i wczytany po wylaczeniu klucza otwiera to podmenu (SandBoxGameManager.OnLoadFinished: GetGameMenu != null -> SetNextMenu), a "Back" wraca do zamku;
                //    bez rejestracji gra konczyla spotkanie (PlayerEncounter.Finish) i druzyna stala na mapie. Menu zamku jeszcze nieruszone.
                var subLists = new Dictionary<string, List<GameMenuOption>>();
                foreach (var h in hubs)
                {
                    starter.AddGameMenu(h.Menu, "{=!}{" + h.TextVar + "}", h.Init, GameMenu.MenuOverlayType.SettlementWithBoth);
                    starter.AddGameMenuOption(h.Menu, h.Menu + "_back", "{=!}Back to the castle",
                        delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                        delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Castle); }, true, -1);
                    var list = Items(mgr.GetGameMenu(h.Menu));
                    if (list == null) throw new InvalidOperationException("brak listy opcji nowego podmenu " + h.Menu + " - nic nie przeniesione");
                    subLists[h.Menu] = list;
                }
                _doneFor = new WeakReference<GameMenuManager>(mgr);   // podmenu zarejestrowane - drugie wolanie w tej sesji nic nie robi (bez podwojnego "Back")

                var s = Settings.Current;
                if (s == null || !s.CastleMenuTidy) { Log.Info(head + "WYLACZONY w MCM (Castle Menu Tidy) - menu zamku jak dotad, nic nie przeniesione (puste podmenu zarejestrowane tylko dla zapisow zrobionych w podmenu)."); return; }
                var castleItems = Items(mgr.GetGameMenu(Castle));
                if (castleItems == null) { Log.Info(head + "BRAK menu 'castle' albo listy opcji (GameMenu._menuItems) - nic nie przeniesione, menu zamku jak dotad."); return; }
                string beforeIds = IdList(castleItems);
                int beforeN = castleItems.Count;

                // 1) co przenosimy (kolejnosc planu)
                var picked = new List<KeyValuePair<Move, GameMenuOption>>();
                var missOurs = new List<string>();
                var missForeign = new List<string>();
                var pinned = new List<string>();
                foreach (var m in Plan)
                {
                    if (CastleOnly.Contains(m.Id)) { pinned.Add(m.Id + " (" + m.Who + ")"); continue; }   // skutek w GameMenuEventHandler("castle") - zostaje
                    int at = IndexOf(castleItems, m.Id);
                    if (at < 0) { (m.Ours ? missOurs : missForeign).Add(m.Id + " (" + m.Who + ")"); continue; }
                    picked.Add(new KeyValuePair<Move, GameMenuOption>(m, castleItems[at]));
                }
                string pinnedNote = pinned.Count > 0 ? " | nieprzenaszalne (GameMenuEventHandler 'castle'), zostaja w zamku " + pinned.Count + " [" + string.Join(", ", pinned.ToArray()) + "]" : "";

                if (picked.Count == 0) { Log.Info(head + "WLACZONY, ale zadnej opcji z planu nie ma w menu 'castle' - nic nie przeniesione | nie znaleziono: " + string.Join(", ", missForeign.ToArray()) + "; " + string.Join(", ", missOurs.ToArray()) + pinnedNote + " | menu zamku " + beforeN + " opcji: " + beforeIds); return; }

                // 2) przygotowanie bez ruszania menu zamku: listy opcji podmenu
                foreach (var kv in picked) { var h = HubOf(hubs, kv.Key.To); h.Kids.Add(kv.Value); h.Lines.Add(kv.Key.Line); }

                // 3) zmiana (te same obiekty GameMenuOption); kopie list - przy bledzie w polowie wszystko wraca na miejsce (nic nie znika)
                var castleCopy = new List<GameMenuOption>(castleItems);
                var subCopies = new Dictionary<string, List<GameMenuOption>>();
                foreach (var kv in subLists) subCopies[kv.Key] = new List<GameMenuOption>(kv.Value);
                int hubsIn = 0, pos = -1;
                string posWhy = "";
                try
                {
                    foreach (var kv in picked) castleItems.Remove(kv.Value);
                    foreach (var h in hubs)
                    {
                        if (h.Kids.Count == 0) continue;
                        subLists[h.Menu].InsertRange(0, h.Kids);    // opcje przed "Back to the castle"
                    }
                    // wejscia: przed "Wait here for some time" (bez niego - przed pierwszym "Leave", na koncu)
                    pos = IndexOf(castleItems, Anchor);
                    posWhy = "przed '" + Anchor + "'";
                    if (pos < 0) { pos = FirstLeave(castleItems); posWhy = "BRAK '" + Anchor + "' - przed pierwszym Leave"; }
                    if (pos < 0 || pos > castleItems.Count) { pos = castleItems.Count; posWhy = "BRAK '" + Anchor + "' i Leave - na koncu"; }
                    posWhy += " (index " + pos + ")";
                    foreach (var h in hubs)
                    {
                        if (h.Kids.Count == 0) continue;
                        starter.AddGameMenuOption(Castle, h.Opt, h.Label, h.Condition, h.Consequence, false, pos);
                        pos++; hubsIn++;
                    }
                    _on = true;
                    committed = true;
                }
                catch (Exception e)
                {
                    castleItems.Clear(); castleItems.AddRange(castleCopy);
                    foreach (var kv in subCopies) { subLists[kv.Key].Clear(); subLists[kv.Key].AddRange(kv.Value); }   // podmenu: tylko "Back" (opcje nie w dwoch miejscach)
                    _on = false;
                    Stumble("Apply (cofniete)", e);
                    Log.Info(head + "BLAD przy przenoszeniu (" + e.Message + ") - menu zamku przywrocone jak dotad, nic nie przeniesione.");
                    return;
                }

                // linia startu: co dokad, czego nie znaleziono, menu zamku przed i po zmianie. Wlasny try: blad tutaj nie moze cofnac stanu po zatwierdzeniu
                // (opcje juz stoja w podmenu, zgaszenie _on schowaloby wejscia do konca sesji).
                try
                {
                    var moved = new StringBuilder();
                    bool firstHub = true;
                    foreach (var h in hubs)
                    {
                        moved.Append(firstHub ? "" : "; ").Append(h.Menu).Append(' ').Append(h.Kids.Count).Append(" [").Append(Ids(picked, h.Menu)).Append("]");
                        firstHub = false;
                    }
                    Log.Info(head + "WLACZONY (Castle Menu Tidy) | menu zamku przed " + beforeN + " opcji: " + beforeIds
                             + " | przeniesione " + picked.Count + " z " + Plan.Length + " w planie: " + moved
                             + " | wejscia podmenu wpiete " + hubsIn + " (" + posWhy + ")"
                             + " | nie znaleziono cudzych " + missForeign.Count + (missForeign.Count > 0 ? " [" + string.Join(", ", missForeign.ToArray()) + "]" : "")
                             + ", naszych " + missOurs.Count + (missOurs.Count > 0 ? " [" + string.Join(", ", missOurs.ToArray()) + "]" : "")
                             + pinnedNote
                             + " | menu zamku teraz " + castleItems.Count + " opcji: " + IdList(castleItems));
                }
                catch (Exception e) { Stumble("linia startu", e); }
            }
            catch (Exception e)
            {
                // blad przed zmiana menu zamku (przygotowanie) - menu jak dotad. Po zatwierdzeniu stanu nie ruszamy (wejscia musza zostac).
                if (!committed) _on = false;
                Stumble("Apply", e);
                Log.Info(head + (committed
                    ? "BLAD po przeniesieniu (" + e.Message + ") - przeniesienie zostaje, wejscia podmenu dzialaja."
                    : "BLAD przed przenoszeniem (" + e.Message + ") - menu zamku jak dotad, nic nie przeniesione."));
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
