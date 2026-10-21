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
    /// m96-wies (Jeff 20.10: "Wioski menu tez ustaw, aby byly poszeregowane jak w miescie i zamkach") - PORZADEK MENU WSI, ten sam wzor co TownMenus i CastleMenus.
    /// Raz na sesje, PO rejestracji menu wszystkich modow i gry (SubModuleMain.OnAfterGameInitializationFinished - po OnSessionLaunched i OnAfterSessionLaunched
    /// wszystkich zachowan, przed pierwszym pokazaniem menu; zaraz po CastleMenus.Apply), opcje z menu gry "village" przechodza jako TE SAME obiekty GameMenuOption
    /// (warunek, skutek, napis, id bez zmian) do dwoch nowych podmenu:
    ///  - "Village affairs" (sprawy wsi): ksiegi Armoury (People of this land, Estates and livelihood), okreg (Look over the district, Invest in the district's works),
    ///    Living Economy (BEE), Banner Kings (BK - demesne, tytuly, projekty wsi, ziemia; werbunek wsi to "Recruit troops" gry, ktory zostaje na wierzchu - jak w miescie),
    ///    kupno domu (RC);
    ///  - "Look for paid work" (praca, jak w miescie): dniowka (RC).
    /// W menu wsi zostaja: werbunek i kupno towarow (gra - skutek w GameMenuEventHandler("village"), nieprzenaszalne, straznik VillageOnly), spacer, "Take a hostile
    /// action" (podmenu gry village_hostile_action: rabunek, zapasy, rekruci i nasz okup "Demand tribute" - wrogie czyny juz sa w jednym podmenu gry, zostaja jak sa),
    /// wioska poboczna gracza ("Visit X" - miejsce), wlasny dom RC ("Go to your house" - miejsce, jak w miescie), zadania (Collect revenue i inne), czekanie, powrot do
    /// armii, opcje morskie (Disembark, Set Sail) i wyjazd.
    /// Wejscia podmenu stoja przed "Wait here for some time" i sa widoczne, gdy choc jedna opcja w srodku jest widoczna (warunki opcji wolane jak w grze, na swiezym
    /// MenuCallbackArgs - znaczniki zadan "!" przechodza na wejscie). Opis podmenu wymienia tylko opcje widoczne w tej chwili. Kazde podmenu ma "Back to the village".
    /// Skutki przeniesionych opcji sprawdzone w dekompilacji: okna (ShowInquiry / ShowMultiSelectionInquiry), wlasne menu z powrotem SwitchToMenu("village") (BK, BEE,
    /// dom RC przez Outside(), dniowka RC przez BackMenu()) - zadna nie zaklada menu nadrzednego, zaden warunek nie patrzy na id biezacego menu. Cudzy powrot laduje
    /// w glownym menu wsi (jak w miescie i zamku).
    /// Klucz VillageMenuTidy (wylaczony = nic sie nie rusza, menu jak dotad; od nastepnego wczytania). Dwa podmenu rejestrowane zawsze, takze przy wylaczonym kluczu
    /// (zapis zrobiony w podmenu wczyta sie normalnie - puste podmenu z "Back to the village"). Bez stanu w zapisie, bez latek Harmony (tylko odczyt prywatnego pola
    /// GameMenu._menuItems przez AccessTools). Potkniecia liczone per opcja / miejsce, nic sie nie gasi. Tylko "village": wies spladrowana (village_looted) bez zmian.
    /// </summary>
    internal static class VillageMenus
    {
        internal const string HubAffairs = "arm_vm_affairs", HubWork = "arm_vm_work";
        internal const string MenuAffairs = "arm_vm_affairs_menu", MenuWork = "arm_vm_work_menu";
        private const string Village = "village", Anchor = "village_wait";

        /// <summary>Wpis planu: id opcji w menu "village", menu docelowe, zrodlo (do logu), zdanie opisu podmenu.</summary>
        private sealed class Move
        {
            public readonly string Id, To, Who, Line;
            public readonly bool Ours;
            public Move(string id, string to, string who, string line)
            {
                Id = id; To = to; Who = who; Line = line;
                Ours = who == "Armoury" || who == "RC";
            }
        }

        // Opcje gry, ktorych prawdziwy skutek siedzi w GameMenuEventHandler("village", id) (CampaignSystem PlayerTownVisit i PlayerTownVisitCampaignBehavior;
        // GameMenuCallbackManager szuka handlera po id BIEZACEGO menu) - poza menu "village" bylyby widoczne, ale martwe. Sprawdzone w dekompilacji: "trade" (Buy
        // products - skutek w AddGameMenuOption = null), "recruit_volunteers", "manage_production" i "buy_goods" (handlery bez opcji we wsi). Straznik w Apply pomija je
        // i loguje, gdyby ktos dopisal je do planu.
        private static readonly HashSet<string> VillageOnly = new HashSet<string> { "trade", "recruit_volunteers", "manage_production", "buy_goods" };

        // kolejnosc w tablicy = kolejnosc w podmenu
        private static readonly Move[] Plan =
        {
            // sprawy wsi i ksiegi
            new Move("arm_people_village", MenuAffairs, "Armoury", "People of this land - who lives here, who serves under arms, who is held or outlawed."),
            new Move("arm_estates_village", MenuAffairs, "Armoury", "Estates and livelihood - the estates of this place and how well they live."),
            new Move(DistrictMenu.OptVillage, MenuAffairs, "Armoury", "Look over the district - the villages around, their lords, their herds and their works."),
            new Move(DistrictWorks.OptVillage, MenuAffairs, "Armoury", "Invest in the district's works - pay the villagers for boats, herds, ploughs or tools."),
            new Move("bee_village_root", MenuAffairs, "BEE", "Living Economy - the village's purse, stores and market."),
            new Move("bannerkings_village_submenu", MenuAffairs, "BK", "Banner Kings - demesne, titles, village projects and lands."),
            new Move("rc_home_buy_village", MenuAffairs, "RC", "Buy a house - a roof of your own, with a strongbox and a chest."),
            // praca (jak w miescie)
            new Move("rc_work_labour_village", MenuWork, "RC", "Day labour - dig, reap and haul from dawn to dusk for a day's wage."),
        };

        /// <summary>Nowe podmenu z wejsciem w menu wsi: przeniesione opcje (te same obiekty) i ich zdania opisu.</summary>
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

            /// <summary>Opis podmenu: naglowek i zdanie dla kazdej opcji widocznej w tej chwili; tlo wsi (WaitMeshName - jak menu wsi gry); tytul paska
            /// (GameMenu.RunOnInit przepisuje args.MenuTitle do menu - bez niego pasek pusty).</summary>
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
        private static readonly Dictionary<string, string> _where = new Dictionary<string, string>();    // id przeniesionej opcji -> podmenu (dla linii logu okregu)

        private static void Reset()
        {
            _on = false; _st.Clear(); _stAll = 0; _where.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stAll++;
            int n; _st.TryGetValue(where, out n); n++; _st[where] = n;
            if (n <= 3 || n % 200 == 0) Log.Error("VillageMenus." + where + " (potkniecie " + n + ")", e);
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

        /// <summary>Linie logu okregu (DistrictMenu.Where, DistrictWorks.MenuText): gdzie stoi opcja wsi przeniesiona w tej sesji - "arm_vm_affairs_menu index 2 z 9
        /// (przed: arm_estates_village, po: arm_works_village)"; null, gdy nie przeniesiona (klucz wylaczony, opcja zostala w menu wsi, podmenu brak).</summary>
        internal static string MovedTo(string optId)
        {
            try
            {
                string m;
                if (!_on || optId == null || !_where.TryGetValue(optId, out m)) return null;
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                var list = Items(mgr != null ? mgr.GetGameMenu(m) : null);
                if (list == null) return null;
                int at = IndexOf(list, optId);
                if (at < 0) return null;
                return m + " index " + at + " z " + list.Count + " (przed: " + (at > 0 && list[at - 1] != null ? list[at - 1].IdString : "-")
                       + ", po: " + (at + 1 < list.Count && list[at + 1] != null ? list[at + 1].IdString : "-") + ")";
            }
            catch { return null; }
        }

        /// <summary>
        /// SubModuleMain.OnAfterGameInitializationFinished (kampania, po CastleMenus.Apply): przeniesienie opcji menu wsi i wpiecie wejsc podmenu. Raz na sesje (nowe
        /// GameMenuManager przy kazdym wczytaniu - stan od zera). Dwa podmenu rejestrowane zawsze, takze przy wylaczonym kluczu. Jedna linia logu
        /// "Menu wsi (m96-wies): start". Nie rzuca.
        /// </summary>
        internal static void Apply(CampaignGameStarter starter)
        {
            const string head = "Menu wsi (m96-wies): start | ";
            bool committed = false;
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                GameMenuManager done;
                if (mgr != null && _doneFor != null && _doneFor.TryGetTarget(out done) && ReferenceEquals(mgr, done)) { Log.Info(head + "drugie wolanie w tej samej sesji - juz zrobione, bez zmian."); return; }
                Reset();
                if (starter == null || mgr == null) { Log.Info(head + "BRAK startera albo GameMenuManager - nic nie przeniesione, menu wsi jak dotad."); return; }

                var hubs = new[]
                {
                    new Hub(HubAffairs, MenuAffairs, "{=!}Village affairs", "Village Affairs", "The village's people, its fields and herds, and its dealings.", "ARM_VM_AFFAIRS_TEXT", GameMenuOption.LeaveType.Manage),
                    new Hub(HubWork, MenuWork, "{=!}Look for paid work", "Paid Work", "There is always work in the fields for willing hands.", "ARM_VM_WORK_TEXT", GameMenuOption.LeaveType.Wait),
                };

                // 0) dwa podmenu z "Back to the village" - ZAWSZE, takze przy wylaczonym kluczu i pustym podmenu (pulapka z recenzji m95-menu): zapis zrobiony w podmenu
                //    i wczytany po wylaczeniu klucza otwiera to podmenu (SandBoxGameManager.OnLoadFinished: GetGameMenu != null -> SetNextMenu), a "Back" wraca do wsi;
                //    bez rejestracji gra konczyla spotkanie (PlayerEncounter.Finish) i druzyna stala na mapie. Menu wsi jeszcze nieruszone.
                var subLists = new Dictionary<string, List<GameMenuOption>>();
                foreach (var h in hubs)
                {
                    starter.AddGameMenu(h.Menu, "{=!}{" + h.TextVar + "}", h.Init, GameMenu.MenuOverlayType.SettlementWithBoth);
                    starter.AddGameMenuOption(h.Menu, h.Menu + "_back", "{=!}Back to the village",
                        delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                        delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(Village); }, true, -1);
                    var list = Items(mgr.GetGameMenu(h.Menu));
                    if (list == null) throw new InvalidOperationException("brak listy opcji nowego podmenu " + h.Menu + " - nic nie przeniesione");
                    subLists[h.Menu] = list;
                }
                _doneFor = new WeakReference<GameMenuManager>(mgr);   // podmenu zarejestrowane - drugie wolanie w tej sesji nic nie robi (bez podwojnego "Back")

                var s = Settings.Current;
                if (s == null || !s.VillageMenuTidy) { Log.Info(head + "WYLACZONY w MCM (Village Menu Tidy) - menu wsi jak dotad, nic nie przeniesione (puste podmenu zarejestrowane tylko dla zapisow zrobionych w podmenu)."); return; }
                var villageItems = Items(mgr.GetGameMenu(Village));
                if (villageItems == null) { Log.Info(head + "BRAK menu 'village' albo listy opcji (GameMenu._menuItems) - nic nie przeniesione, menu wsi jak dotad."); return; }
                string beforeIds = IdList(villageItems);
                int beforeN = villageItems.Count;

                // 1) co przenosimy (kolejnosc planu)
                var picked = new List<KeyValuePair<Move, GameMenuOption>>();
                var missOurs = new List<string>();
                var missForeign = new List<string>();
                var pinned = new List<string>();
                foreach (var m in Plan)
                {
                    if (VillageOnly.Contains(m.Id)) { pinned.Add(m.Id + " (" + m.Who + ")"); continue; }   // skutek w GameMenuEventHandler("village") - zostaje
                    int at = IndexOf(villageItems, m.Id);
                    if (at < 0) { (m.Ours ? missOurs : missForeign).Add(m.Id + " (" + m.Who + ")"); continue; }
                    picked.Add(new KeyValuePair<Move, GameMenuOption>(m, villageItems[at]));
                }
                string pinnedNote = pinned.Count > 0 ? " | nieprzenaszalne (GameMenuEventHandler 'village'), zostaja we wsi " + pinned.Count + " [" + string.Join(", ", pinned.ToArray()) + "]" : "";

                if (picked.Count == 0) { Log.Info(head + "WLACZONY, ale zadnej opcji z planu nie ma w menu 'village' - nic nie przeniesione | nie znaleziono: " + string.Join(", ", missForeign.ToArray()) + "; " + string.Join(", ", missOurs.ToArray()) + pinnedNote + " | menu wsi " + beforeN + " opcji: " + beforeIds); return; }

                // 2) przygotowanie bez ruszania menu wsi: listy opcji podmenu
                foreach (var kv in picked) { var h = HubOf(hubs, kv.Key.To); h.Kids.Add(kv.Value); h.Lines.Add(kv.Key.Line); }

                // 3) zmiana (te same obiekty GameMenuOption); kopie list - przy bledzie w polowie wszystko wraca na miejsce (nic nie znika)
                var villageCopy = new List<GameMenuOption>(villageItems);
                var subCopies = new Dictionary<string, List<GameMenuOption>>();
                foreach (var kv in subLists) subCopies[kv.Key] = new List<GameMenuOption>(kv.Value);
                int hubsIn = 0, pos = -1;
                string posWhy = "";
                try
                {
                    foreach (var kv in picked) villageItems.Remove(kv.Value);
                    foreach (var h in hubs)
                    {
                        if (h.Kids.Count == 0) continue;
                        subLists[h.Menu].InsertRange(0, h.Kids);    // opcje przed "Back to the village"
                    }
                    // wejscia: przed "Wait here for some time" (bez niego - przed pierwszym "Leave", na koncu)
                    pos = IndexOf(villageItems, Anchor);
                    posWhy = "przed '" + Anchor + "'";
                    if (pos < 0) { pos = FirstLeave(villageItems); posWhy = "BRAK '" + Anchor + "' - przed pierwszym Leave"; }
                    if (pos < 0 || pos > villageItems.Count) { pos = villageItems.Count; posWhy = "BRAK '" + Anchor + "' i Leave - na koncu"; }
                    posWhy += " (index " + pos + ")";
                    foreach (var h in hubs)
                    {
                        if (h.Kids.Count == 0) continue;
                        starter.AddGameMenuOption(Village, h.Opt, h.Label, h.Condition, h.Consequence, false, pos);
                        pos++; hubsIn++;
                    }
                    foreach (var kv in picked) _where[kv.Key.Id] = kv.Key.To;
                    _on = true;
                    committed = true;
                }
                catch (Exception e)
                {
                    villageItems.Clear(); villageItems.AddRange(villageCopy);
                    foreach (var kv in subCopies) { subLists[kv.Key].Clear(); subLists[kv.Key].AddRange(kv.Value); }   // podmenu: tylko "Back" (opcje nie w dwoch miejscach)
                    _on = false; _where.Clear();
                    Stumble("Apply (cofniete)", e);
                    Log.Info(head + "BLAD przy przenoszeniu (" + e.Message + ") - menu wsi przywrocone jak dotad, nic nie przeniesione.");
                    return;
                }

                // linia startu: co dokad, czego nie znaleziono, menu wsi przed i po zmianie. Wlasny try: blad tutaj nie moze cofnac stanu po zatwierdzeniu
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
                    Log.Info(head + "WLACZONY (Village Menu Tidy) | menu wsi przed " + beforeN + " opcji: " + beforeIds
                             + " | przeniesione " + picked.Count + " z " + Plan.Length + " w planie: " + moved
                             + " | wejscia podmenu wpiete " + hubsIn + " (" + posWhy + ")"
                             + " | nie znaleziono cudzych " + missForeign.Count + (missForeign.Count > 0 ? " [" + string.Join(", ", missForeign.ToArray()) + "]" : "")
                             + ", naszych " + missOurs.Count + (missOurs.Count > 0 ? " [" + string.Join(", ", missOurs.ToArray()) + "]" : "")
                             + pinnedNote
                             + " | menu wsi teraz " + villageItems.Count + " opcji: " + IdList(villageItems));
                }
                catch (Exception e) { Stumble("linia startu", e); }
            }
            catch (Exception e)
            {
                // blad przed zmiana menu wsi (przygotowanie) - menu jak dotad. Po zatwierdzeniu stanu nie ruszamy (wejscia musza zostac).
                if (!committed) { _on = false; _where.Clear(); }
                Stumble("Apply", e);
                Log.Info(head + (committed
                    ? "BLAD po przeniesieniu (" + e.Message + ") - przeniesienie zostaje, wejscia podmenu dzialaja."
                    : "BLAD przed przenoszeniem (" + e.Message + ") - menu wsi jak dotad, nic nie przeniesione."));
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
