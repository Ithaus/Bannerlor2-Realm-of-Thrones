using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 177-3 MISTRZOWIE QOHORU (Jeff 09.10, decyzja 24a pkt 3; projekt docs/PROJEKT-177-STAL-VALYRIANSKA-2026-10-09.md rozdz. 5 i "Krytyka
    /// i odpowiedzi"). Jedyna droga do nowego miecza valyrianskiego: PRZEKUCIE istniejacej stali u cechu mistrzow w Qohorze (town_ES6;
    /// ROT sam pisze w opisie miasta, ze jego kowale umieja "reforge Valyrian steel"). Kanon: Tobho Mott, wyszkolony w Qohorze, z Lodu
    /// zrobil dwa miecze - "There was enough metal for two new blades" (ASOS 60).
    ///  - ZLECENIA (miary z ValyrianBlades - stal ani nie przybywa, ani nie ubywa): klinga 1 miary -> jeden miecz 1 miary w nowym ksztalcie;
    ///    wielki miecz 2 miar (Lod, Heartsbane, Blackfyre, Brightroar) -> dwa miecze. Wynik: gotowe seryjne wzory ROT ("Valyrian Steel Sword
    ///    type 1-8, Blue, Red" - legendy, unikaty, poza handlem, w spisie). Nazwanych legend mistrzowie nie robia. "Dwa w jeden" - nie
    ///    (krytyka pkt 18: Jeff chcial tylko "wielki miecz moze dac dwa mniejsze"; seryjnego wyniku 2 miar nie ma).
    ///  - CZAS: QohorDays (7) dni pracy dla kazdego zlecenia [S] - skrot gry na zyczenie Jeffa ("kilka dni"); w ksiazkach - miesiace.
    ///    Kazdy dzien pracy zabiera rece kowali Qohoru (SmithHours.Use - te same, z ktorych biora naprawy i warsztaty; krytyka pkt 25):
    ///    cech bierze tyle wolnych godzin, ile dzis zostalo (najwyzej QohorCrew x WorkHoursPerManDay) i odklada je na zlecenie - dzien pracy
    ///    schodzi, gdy uzbiera sie pelny (zajete rece = praca wolniej, nie stoi). Wegiel z polki Qohoru przy pierwszym dniu
    ///    (QohorCharcoalPerMeasure na miare); brak = zlecenie czeka.
    ///  - OPLATA (krytyka pkt 22 - liczona jawnie, nie dopasowana do ceny miecza) = PRACA: dni x QohorCrew mistrzow x dniowka mistrza t6
    ///    (HistoricalPrices.WageFor(6) = 10.5 d) x poziom plac Qohoru (TownWage.Index) + OPLATA CECHU: QohorTollPercent (6.25% = 1/16)
    ///    wartosci stali, ktora przechodzi przez ogien (miary x HistValyrianPerMeasure). Uzasadnienie 1/16: cech jest jedynym na swiecie,
    ///    ktory to umie - monopol jak mlyn dworski (soke), ktory bral od ziarna miarke (multure) 1/24-1/16 [S, zwyczaj angielski XIII-XIV w.];
    ///    klient nie ma innego mlyna, wiec stawka wyzsza. Zloto z gory do kasy Qohoru (GiveGoldAction -> SettlementComponent) - zamkniety
    ///    obieg; nie wraca przy odebraniu klingi przed czasem (praca zaczeta).
    ///  - GRACZ: menu w Qohorze, wybor klingi z taboru (dokladna sztuka - EquipmentElement z modyfikatorem; krytyka pkt 12), wybor
    ///    wzoru, potwierdzenie; czekanie w miescie (menu oczekiwania - wyjscie przez flage, NIGDY SwitchToMenu z opcji menu czekania) albo
    ///    odjazd i odbior pozniej; "Take back your blade" zwraca te sama sztuke.
    ///  - AI (krytyka pkt 21 - ta sama regula, kanon: Tywin przekul cudzy Lod): lord, ktorego partia stoi w Qohorze i ktory nosi wielki
    ///    miecz 2 miar zdobyty od INNEGO rodu (wlasciciel ze stanu startowego z innego rodu), majacy w kiesie co najmniej 2 x oplata, zleca
    ///    rozdzielenie na dwa miecze - ta sama oplata do kasy Qohoru, te same dni i rece. Recenzja 177: gotowe miecze mistrzowie odsylaja
    ///    w dniu ukonczenia (lord AI nie wraca do Qohoru sam - zlecenie wisialoby na zawsze): miecz dla niego, drugi dla dziedzica / glowy
    ///    rodu; zmarly - jego dziedzic; bez nikogo - polka Qohoru. Partie rodu gracza (towarzysze) nie zlecaja - o rzeczach rodu decyduje gracz.
    ///  - PRZYJECIE (recenzja 177): przed przyjeciem klingi straznik stali valyrianskiej sprawdza jej wzor (ValyrianBlades.GuardNow) - kopia
    ///    z niczego zamienia sie w zwykla stal, zanim wejdzie do Qohoru (tam jest nie do zmiany, a przekucie przesuwa rejestr).
    ///  - SPIS: klingi u mistrzow licza sie w spisie stali valyrianskiej jako "u mistrzow Qohoru" (wejscie) az do wydania; przy wydaniu
    ///    rejestr zamienia wejscie na wynik (ValyrianBlades.RegistryMove).
    ///  - ZAPIS: arm_qohor_orders (SaveText, wlasny try). Przed cofnieciem DLL odebrac zlecenia (inaczej klingi zostaja tylko w napisie).
    /// </summary>
    internal static class QohorMasters
    {
        internal const string Menu = "arm_qohor_masters";
        internal const string WaitMenu = "arm_qohor_wait";
        private const string PlayerOwner = "@player";

        private sealed class Order
        {
            public string Owner = PlayerOwner;                         // "@player" albo StringId bohatera AI
            public List<string> InIds = new List<string>();
            public List<string> InMods = new List<string>();          // StringId modyfikatora wejscia ("" = bez)
            public List<string> OutIds = new List<string>();
            public int DaysLeft, DaysTotal, Fee, Placed;
            public float Hours;                                         // godziny kowali zebrane na biezacy dzien pracy (gdy rak brakuje - praca idzie wolniej, nie stoi)
            public string Town = "";
            public bool Coal;
            public bool Ready { get { return DaysLeft <= 0; } }
            public bool IsPlayer { get { return Owner == PlayerOwner; } }
        }

        private static readonly List<Order> _orders = new List<Order>();
        private static bool _waitLeave;
        private static float _waitTotal = 1f;
        private static int _dAiOrders, _dAiDelivered, _stumbles;
        private static Settlement _qohor;
        private static string _qohorFor;

        internal static void Reset() { _orders.Clear(); _waitLeave = false; _dAiOrders = _dAiDelivered = 0; _stumbles = 0; _qohor = null; _qohorFor = null; }

        private static Settlement Qohor()
        {
            var id = Settings.Current != null ? Settings.Current.QohorTownId : "town_ES6";
            if (_qohor != null && _qohorFor == id) return _qohor;
            _qohor = null; _qohorFor = id;
            foreach (var s in Settlement.All) if (s != null && s.StringId == id) { _qohor = s; break; }
            return _qohor;
        }

        private static bool InQohor() { var st = Settlement.CurrentSettlement; return st != null && st == Qohor(); }

        // ------------------------------------------------------------ wzory wyniku, oplata
        /// <summary>Wzory wyniku: seryjne klingi ROT o 1 mierze (istniejace w grze).</summary>
        private static List<ItemObject> ResultPatterns()
        {
            var l = new List<ItemObject>();
            foreach (var id in ValyrianBlades.SerialIds)
            {
                var it = ValyrianBlades.Item(id);
                if (it != null && ValyrianBlades.Measures(it) == 1) l.Add(it);
            }
            return l;
        }

        internal static int Fee(int measures)
        {
            var s = Settings.Current;
            float wage = HistoricalPrices.WageFor(6) * TownWage.Index(Qohor());
            float labor = Math.Max(1, s.QohorDays) * Math.Max(1, s.QohorCrew) * wage;
            float toll = Math.Max(0, measures) * Math.Max(0f, s.HistValyrianPerMeasure) * Math.Max(0f, s.QohorTollPercent) / 100f;
            return Math.Max(1, (int)Math.Round(labor + toll));
        }

        private static string FeeText(int measures)
        {
            var s = Settings.Current;
            float wage = HistoricalPrices.WageFor(6) * TownWage.Index(Qohor());
            int labor = (int)Math.Round(Math.Max(1, s.QohorDays) * Math.Max(1, s.QohorCrew) * wage);
            // recenzja 177: stawka z ustawien (Qohor Toll Percent), "szesnasta czesc" tylko przy 6.25
            float pct = Math.Max(0f, s.QohorTollPercent);
            string part = Math.Abs(pct - 6.25f) < 0.01f ? "a sixteenth part" : pct.ToString("0.##", CultureInfo.InvariantCulture) + "%";
            return Fee(measures) + " gold (" + Math.Max(1, s.QohorDays) + " days of " + Math.Max(1, s.QohorCrew) + " masters: " + labor
                   + "; the guild's toll of " + part + " of the steel's worth: " + (Fee(measures) - labor) + ")";
        }

        // ------------------------------------------------------------ menu gracza
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("town", "arm_qohor_enter", "{=!}Seek out the Valyrian steel masters of Qohor",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Craft;
                        if (!InQohor()) return false;
                        if (!Settings.Current.QohorReworkEnabled && !PlayerOrders().Any()) return false;
                        int run = PlayerOrders().Count(o => !o.Ready), ready = PlayerOrders().Count(o => o.Ready);
                        if (run + ready > 0) a.Tooltip = new TextObject("{=!}" + (run > 0 ? run + " order(s) at the forge" : "") + (run > 0 && ready > 0 ? ", " : "") + (ready > 0 ? ready + " ready to collect" : "") + ".");
                        return true;
                    },
                    delegate { GameMenu.SwitchToMenu(Menu); }, false, 5);
                starter.AddGameMenu(Menu, "{=!}{ARM_QOHOR_TEXT}", delegate (MenuCallbackArgs a) { MBTextManager.SetTextVariable("ARM_QOHOR_TEXT", MastersText()); });
                starter.AddGameMenuOption(Menu, "arm_qohor_one", "{=!}Have a blade reforged in a new form",
                    delegate (MenuCallbackArgs a) { return OrderCondition(a, 1); },
                    delegate { ChooseBlade(1); }, false, 0);
                starter.AddGameMenuOption(Menu, "arm_qohor_two", "{=!}Have a greatsword reforged into two blades",
                    delegate (MenuCallbackArgs a) { return OrderCondition(a, 2); },
                    delegate { ChooseBlade(2); }, false, 1);
                starter.AddGameMenuOption(Menu, "arm_qohor_waitopt", "{=!}Wait in Qohor until the masters are done",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Wait; return PlayerOrders().Any(o => !o.Ready); },
                    delegate { GameMenu.SwitchToMenu(WaitMenu); }, false, 2);
                starter.AddGameMenuOption(Menu, "arm_qohor_collect", "{=!}Collect your reforged steel",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Trade; return PlayerOrders().Any(o => o.Ready); },
                    delegate { Collect(); GameMenu.SwitchToMenu(Menu); }, false, 3);
                starter.AddGameMenuOption(Menu, "arm_qohor_back", "{=!}Take back your blade",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Leave;
                        if (!PlayerOrders().Any(o => !o.Ready)) return false;
                        a.Tooltip = new TextObject("{=!}The masters give back the blade as it was. Their fee is not returned - the work has begun.");
                        return true;
                    },
                    delegate { TakeBack(); }, false, 4);
                starter.AddGameMenuOption(Menu, "arm_qohor_leave", "{=!}Leave",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate { GameMenu.SwitchToMenu("town"); }, true, 9);

                starter.AddWaitGameMenu(WaitMenu, "{=!}{ARM_QOHOR_WAIT_TEXT}", WaitInit, delegate (MenuCallbackArgs a) { return true; }, null, WaitTick,
                    GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption, GameMenu.MenuOverlayType.SettlementWithBoth);
                starter.AddGameMenuOption(WaitMenu, "arm_qohor_wait_stop", "{=!}Leave the masters to their work",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    // ZAKAZ SwitchToMenu z opcji menu oczekiwania (CTD, CLAUDE.md) - flaga, przelacza WaitTick; zegar ruszamy sami (ratunek
                    // z zastoju po wczytaniu w srodku menu czekania - wzor arm_project_wait)
                    delegate (MenuCallbackArgs a)
                    {
                        _waitLeave = true;
                        try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay; a.MenuContext.GameMenu.StartWait(); } catch { }
                    }, true, 9);
                Log.Info("QohorMasters: menu w " + (Qohor() != null ? Qohor().StringId + " (" + Qohor().Name + ")" : "BRAK " + Settings.Current.QohorTownId + " - przekucie spi")
                         + " wpiete; zlecen w toku " + _orders.Count + "; oplata za klinge 1 miary " + Fee(1) + " d, 2 miar " + Fee(2) + " d; wzorow wyniku " + ResultPatterns().Count + ".");
            }
            catch (Exception e) { Log.Error("QohorMasters.AddMenus", e); }
        }

        private static IEnumerable<Order> PlayerOrders() { return _orders.Where(o => o.IsPlayer); }

        private static string MastersText()
        {
            var sb = new StringBuilder("Beneath the temple of the Black Goat the smiths of Qohor keep the last secret of Valyria. No one has made Valyrian steel since the Doom - but these masters can melt it and forge it anew, and nothing of it is lost.");
            sb.Append("\n\nBring a blade in your baggage (not on your back). A blade of one measure becomes one new sword; a greatsword of two measures, such as Ice, becomes two. ");
            sb.Append("The work takes " + Math.Max(1, Settings.Current.QohorDays) + " days of their hands (longer when the smiths of Qohor are busy). Their fee for one measure: " + FeeText(1) + "; for two: " + Fee(2) + " gold.");
            foreach (var o in PlayerOrders())
                sb.Append("\n\n" + Describe(o) + (o.Ready ? " - READY." : " - " + o.DaysLeft + " day(s) of work left" + (o.Coal ? "" : ", waiting for charcoal") + "."));
            return sb.ToString();
        }

        private static string Describe(Order o)
        {
            return string.Join(" and ", o.InIds.Select(id => NameOf(id)).ToArray()) + " into " + string.Join(" and ", o.OutIds.Select(id => NameOf(id)).ToArray());
        }

        private static string NameOf(string id) { var it = ValyrianBlades.Item(id); return it != null ? it.Name.ToString() : id; }

        private static List<EquipmentElement> Blades(int measures)
        {
            var l = new List<EquipmentElement>();
            var r = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
            if (r == null) return l;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Amount > 0 && ValyrianBlades.Is(e.EquipmentElement.Item) && ValyrianBlades.Measures(e.EquipmentElement.Item) == measures) l.Add(e.EquipmentElement);
            }
            return l;
        }

        private static bool OrderCondition(MenuCallbackArgs a, int measures)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Craft;
            if (!Settings.Current.QohorReworkEnabled) return false;
            if (ResultPatterns().Count == 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The masters have no patterns to forge (Valyrian swords of the realm are missing from the game)."); return true; }
            int fee = Fee(measures);
            if (Blades(measures).Count == 0)
            {
                a.IsEnabled = false;
                a.Tooltip = new TextObject("{=!}" + (measures == 1 ? "You carry no Valyrian blade of one measure in your baggage." : "You carry no Valyrian greatsword (two measures) in your baggage.")
                                           + " A blade you wear must be put in your baggage first.");
                return true;
            }
            if (Hero.MainHero.Gold < fee)
            {
                a.IsEnabled = false;
                a.Tooltip = new TextObject("{=!}The masters ask " + fee + " gold. You have " + Hero.MainHero.Gold + ".");
                return true;
            }
            a.Tooltip = new TextObject("{=!}Fee: " + FeeText(measures) + ". " + Math.Max(1, Settings.Current.QohorDays) + " days of work.");
            return true;
        }

        private static void ChooseBlade(int measures)
        {
            try
            {
                var els = new List<InquiryElement>();
                var r = MobileParty.MainParty.ItemRoster;
                foreach (var el in Blades(measures))
                {
                    int n = r.GetItemNumber(el.Item);
                    els.Add(new InquiryElement(el, el.Item.Name + (el.ItemModifier != null ? " (" + el.ItemModifier.Name + ")" : "") + (n > 1 ? "  x" + n : ""), SmithMenu.ItemPic(el.Item), true,
                        (measures == 2 ? "Two measures" : "One measure") + " of Valyrian steel."));
                }
                if (els.Count == 0) return;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Masters of Qohor", "Which blade goes into the fire?", els, true, 1, 1, "This one", "Not now",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0 || !(sel[0].Identifier is EquipmentElement)) return;
                        ChoosePattern((EquipmentElement)sel[0].Identifier, measures);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.ChooseBlade", e); }
        }

        private static void ChoosePattern(EquipmentElement blade, int measures)
        {
            try
            {
                var els = new List<InquiryElement>();
                foreach (var it in ResultPatterns())
                    if (it != blade.Item || measures == 2)
                        els.Add(new InquiryElement(it, it.Name.ToString(), SmithMenu.ItemPic(it), true, "One measure of Valyrian steel."));
                if (els.Count == 0) return;
                string ask = measures == 2 ? "Choose the shape of the two new blades - one pattern for both, or two patterns." : "Choose the shape of the new blade.";
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Masters of Qohor", ask, els, true, 1, measures, "Forge it", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        var outs = sel.Select(x => x.Identifier as ItemObject).Where(x => x != null).ToList();
                        if (outs.Count == 0) return;
                        while (outs.Count < measures) outs.Add(outs[0]);
                        Confirm(blade, outs, measures);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.ChoosePattern", e); }
        }

        private static void Confirm(EquipmentElement blade, List<ItemObject> outs, int measures)
        {
            int fee = Fee(measures);
            int days = Math.Max(1, Settings.Current.QohorDays);
            string result = string.Join(" and ", outs.Select(x => x.Name.ToString()).ToArray());
            InformationManager.ShowInquiry(new InquiryData("The Masters of Qohor",
                blade.Item.Name + " goes into the fire. In " + days + " days of work the masters will hand you " + result + ". Their fee of " + fee
                + " gold is paid now and is not returned.", true, true, "Pay " + fee, "Not now",
                delegate { Place(blade, outs, measures, fee, days); },
                delegate { }), true);
        }

        private static void Place(EquipmentElement blade, List<ItemObject> outs, int measures, int fee, int days)
        {
            try
            {
                var q = Qohor();
                var r = MobileParty.MainParty.ItemRoster;
                // recenzja 177: straznik dla tego wzoru przed przyjeciem - kopia z niczego staje sie zwykla stala (komunikat w ValyrianBlades.Forge)
                bool forged = q != null && ValyrianBlades.GuardNow(blade.Item.StringId);
                int idx = r.FindIndexOfElement(blade);
                if (q == null || idx < 0 || r.GetElementNumber(idx) <= 0)
                {
                    if (!forged) Log.Player("The blade is no longer in your baggage.", true);
                    else Log.Player("The masters of Qohor will not put a forgery into their fire.", true);
                    return;
                }
                if (Hero.MainHero.Gold < fee) { Log.Player("You cannot pay the masters' fee.", true); return; }
                GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, q, fee, true);
                r.AddToCounts(blade, -1);   // dokladna sztuka (z modyfikatorem) - zwrot przy "Take back" w tym samym stanie (krytyka pkt 12)
                var o = new Order
                {
                    Owner = PlayerOwner, Fee = fee, DaysLeft = days, DaysTotal = days, Town = q.StringId, Placed = (int)CampaignTime.Now.ToDays
                };
                o.InIds.Add(blade.Item.StringId); o.InMods.Add(blade.ItemModifier != null ? blade.ItemModifier.StringId : "");
                foreach (var it in outs) o.OutIds.Add(it.StringId);
                _orders.Add(o);
                Log.Info("QohorMasters: gracz zleca " + Describe(o) + " - oplata " + fee + " d do kasy " + q.Name + ", " + days + " dni pracy (miary " + measures + " -> " + outs.Count + ").");
                Log.Player("The masters of Qohor take " + blade.Item.Name + " into their forge. Come back in " + days + " days, or wait here.");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Log.Error("QohorMasters.Place", e); }
        }

        private static void Collect()
        {
            try
            {
                var ready = PlayerOrders().Where(o => o.Ready).ToList();
                foreach (var o in ready) Deliver(o);
            }
            catch (Exception e) { Log.Error("QohorMasters.Collect", e); }
        }

        /// <summary>Wydanie: wynik do gracza (tabor) albo do wlasciciela AI (zaklada; drugi miecz - dziedzic / glowa rodu; zmarly - dziedzic; nikt -
        /// polka Qohoru), DOPIERO POTEM zlecenie znika, a rejestr zamienia wejscie na wynik (recenzja 177: wyjatek w srodku nie gubi klingi -
        /// kazda sztuka wydawana osobno, przy bledzie na polke Qohoru). Brak przedmiotu wyniku po aktualizacji ROT - mistrzowie oddaja wejscie.</summary>
        private static void Deliver(Order o)
        {
            var outs = o.OutIds.Select(ValyrianBlades.Item).ToList();
            if (outs.Any(x => x == null))
            {
                Log.Info("QohorMasters: wzor wyniku zniknal z gry (" + string.Join(", ", o.OutIds.ToArray()) + ") - mistrzowie oddaja " + string.Join(", ", o.InIds.ToArray()) + ".");
                ReturnInputs(o);
                _orders.Remove(o);
                return;
            }
            int inM = o.InIds.Sum(id => ValyrianBlades.Measures(ValyrianBlades.Item(id))), outM = outs.Sum(x => ValyrianBlades.Measures(x));
            string to;
            if (o.IsPlayer)
            {
                foreach (var it in outs) MobileParty.MainParty.ItemRoster.AddToCounts(new EquipmentElement(it), 1);
                // recenzja 177: licznik w okienku tylko przy jednym wzorze (dwa rozne - okienko pierwszego bez "2x"; oba w komunikacie)
                try { CraftPopup.Show(outs[0], null, outs.All(x => x == outs[0]) ? outs.Count : 1); } catch { }
                Log.Player("The masters of Qohor lay " + string.Join(" and ", outs.Select(x => x.Name.ToString()).ToArray()) + " before you. The ripples of the old steel run through the new blade.");
                to = "gracz";
            }
            else
            {
                var els = outs.Select(x => new EquipmentElement(x)).ToList();
                to = GiveAi(o, els, "przekuty");
                _dAiDelivered++;
            }
            _orders.Remove(o);
            ValyrianBlades.RegistryMove(o.InIds, o.OutIds);
            Log.Info("Kronika unikatow: Qohor - " + string.Join(" + ", o.InIds.ToArray()) + " przekuty na " + string.Join(" + ", o.OutIds.ToArray())
                     + " (miary " + inM + " -> " + outM + "), odbiera " + to + ".");
        }

        /// <summary>Wlasciciel zlecenia AI: zywy - on; zmarly albo wylaczony - jego dziedzic; brak - null (polka Qohoru).</summary>
        private static Hero AiOwner(Order o)
        {
            var owner = ValyrianBlades.FindHero(o.Owner);
            if (UniqueSpoils.Usable(owner)) return owner;
            return owner != null ? UniqueSpoils.HeirOf(owner) : null;
        }

        /// <summary>Sztuki zlecenia AI: pierwsza wlascicielowi (albo dziedzicowi zmarlego), reszta jego dziedzicowi / glowie rodu; nikt - polka
        /// Qohoru. Kazda sztuka osobno, blad = polka Qohoru (sztuka nie ginie). Zwraca opis do kroniki.</summary>
        private static string GiveAi(Order o, List<EquipmentElement> els, string what)
        {
            var to = AiOwner(o);
            Hero second = to != null ? (UniqueSpoils.HeirOf(to) ?? to) : null;
            for (int i = 0; i < els.Count; i++)
            {
                var t = i == 0 ? to : second;
                try
                {
                    if (t == null) UniqueSpoils.Shelve(els[i], Shelf(), "Qohor - " + what + ", wlasciciel " + o.Owner + " bez dziedzica");
                    else UniqueSpoils.Give(t, els[i], t, null, "Qohor - " + what + " (zlecenie " + o.Owner + ")", "from the masters of Qohor");
                }
                catch (Exception e)
                {
                    Log.Error("QohorMasters.GiveAi", e);
                    try { UniqueSpoils.Shelve(els[i], Shelf(), "Qohor - blad wydania"); } catch { }
                }
            }
            return to != null ? to.Name + (second != null && second != to && els.Count > 1 ? " i " + second.Name : "") : "polka Qohoru";
        }

        /// <summary>Polka dla klingi bez odbiorcy: targ Qohoru (bez Qohoru na mapie - pierwsze miasto, zeby sztuka nie zginela).</summary>
        private static Settlement Shelf() { return Qohor() ?? UniqueSpoils.TownFor(null, null); }

        private static void ReturnInputs(Order o)
        {
            var els = new List<EquipmentElement>();
            for (int i = 0; i < o.InIds.Count; i++)
            {
                var it = ValyrianBlades.Item(o.InIds[i]);
                if (it == null) continue;
                ItemModifier mod = null;
                try { if (i < o.InMods.Count && o.InMods[i].Length > 0) mod = MBObjectManager.Instance.GetObject<ItemModifier>(o.InMods[i]); } catch { }
                els.Add(new EquipmentElement(it, mod));
            }
            if (o.IsPlayer) foreach (var el in els) MobileParty.MainParty.ItemRoster.AddToCounts(el, 1);
            else GiveAi(o, els, "zwrot");
        }

        private static void TakeBack()
        {
            try
            {
                var run = PlayerOrders().Where(o => !o.Ready).ToList();
                if (run.Count == 0) return;
                var els = run.Select(o => new InquiryElement(o, Describe(o) + " - " + o.DaysLeft + " day(s) left", SmithMenu.ItemPic(ValyrianBlades.Item(o.InIds[0])), true,
                                                             "The fee of " + o.Fee + " gold is not returned.")).ToList();
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Masters of Qohor", "Which blade do you take back as it was?", els, true, 1, 1, "Take it back", "Leave it with them",
                    delegate (List<InquiryElement> sel)
                    {
                        var o = sel != null && sel.Count > 0 ? sel[0].Identifier as Order : null;
                        if (o == null || !_orders.Contains(o) || o.Ready) return;
                        ReturnInputs(o);
                        _orders.Remove(o);
                        Log.Info("QohorMasters: gracz odbiera przed czasem " + string.Join(", ", o.InIds.ToArray()) + " (oplata " + o.Fee + " d zostaje w kasie Qohoru).");
                        Log.Player("The masters hand back " + string.Join(" and ", o.InIds.Select(NameOf).ToArray()) + " as it was.");
                        GameMenu.SwitchToMenu(Menu);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.TakeBack", e); }
        }

        // ------------------------------------------------------------ czekanie w miescie
        private static void WaitInit(MenuCallbackArgs args)
        {
            try
            {
                _waitLeave = false;
                var run = PlayerOrders().Where(o => !o.Ready).ToList();
                int left = run.Count > 0 ? run.Max(o => o.DaysLeft) : 0;
                _waitTotal = Math.Max(1f, run.Count > 0 ? run.Max(o => o.DaysTotal) : 1);
                MBTextManager.SetTextVariable("ARM_QOHOR_WAIT_TEXT", "You wait in Qohor while the masters work. About " + left + " day(s) of their work remain"
                                              + (run.Any(o => !o.Coal) ? " (they wait for charcoal from the stalls)" : "") + ".");
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Log.Error("QohorMasters.WaitInit", e); }
        }

        private static void WaitTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                if (_waitLeave) { _waitLeave = false; GameMenu.SwitchToMenu(Menu); return; }
                var run = PlayerOrders().Where(o => !o.Ready).ToList();
                if (run.Count == 0) { GameMenu.SwitchToMenu(Menu); return; }
                float left = run.Max(o => o.DaysLeft);
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(Math.Max(0f, Math.Min(1f, 1f - left / _waitTotal)));
            }
            catch (Exception e) { Log.Error("QohorMasters.WaitTick", e); }
        }

        internal static void OnEntered(MobileParty mp, Settlement st, Hero h)
        {
            try
            {
                if (mp != MobileParty.MainParty || st == null || st != Qohor()) return;
                if (PlayerOrders().Any(o => o.Ready)) Log.Player("Your Valyrian steel is ready at the masters' forge.");
            }
            catch { }
        }

        // ------------------------------------------------------------ doba: praca mistrzow (rece kowali i wegiel Qohoru)
        internal static void Daily()
        {
            try
            {
                var q = Qohor();
                if (q == null || _orders.Count == 0) { Line(0, 0, 0); return; }   // bez Qohoru (inna mapa) zlecen nie ma - menu i AI spia
                var town = q.Town;
                var s = Settings.Current;
                float need = Math.Max(1, s.QohorCrew) * Math.Max(1f, s.WorkHoursPerManDay);
                int worked = 0, noHands = 0, noCoal = 0;
                foreach (var o in _orders.Where(x => !x.Ready).OrderBy(x => x.Placed).ToList())
                {
                    if (!o.Coal)
                    {
                        int coalNeed = Math.Max(0, s.QohorCharcoalPerMeasure) * o.InIds.Sum(id => Math.Max(1, ValyrianBlades.Measures(ValyrianBlades.Item(id))));
                        if (!TakeCoal(q, coalNeed)) { noCoal++; continue; }
                        o.Coal = true;
                    }
                    // rece kowali Qohoru: tyle, ile dzis wolnych (najwyzej dzien pracy cechu); dzien pracy zlecenia schodzi, gdy zbierze sie pelny
                    float take = town != null ? Math.Min(need - o.Hours, SmithHours.Available(town)) : need - o.Hours;
                    if (take > 0f) { if (town != null) SmithHours.Use(town, take); o.Hours += take; }
                    if (o.Hours + 0.01f < need) { noHands++; continue; }
                    o.Hours = 0f;
                    o.DaysLeft--;
                    worked++;
                    if (o.Ready)
                    {
                        Log.Info("QohorMasters: gotowe - " + Describe(o) + " (" + (o.IsPlayer ? "gracz" : o.Owner) + ").");
                        if (o.IsPlayer) Log.Player("The masters of Qohor have finished " + string.Join(" and ", o.OutIds.Select(NameOf).ToArray()) + ".");
                    }
                }
                // recenzja 177: gotowe zlecenia AI mistrzowie odsylaja od razu (wlasciciel, dziedzic albo polka Qohoru) - lord AI do Qohoru sam nie
                // wraca, a zlecenie zamrazaloby skonczony zasob na zawsze
                foreach (var o in _orders.Where(x => !x.IsPlayer && x.Ready).ToList())
                {
                    try { Deliver(o); }
                    catch (Exception e) { if (_stumbles++ < 3) Log.Error("QohorMasters.Deliver(AI)", e); }
                }
                Line(worked, noHands, noCoal);
            }
            catch (Exception e) { Log.Error("QohorMasters.Daily", e); }
        }

        private static void Line(int worked, int noHands, int noCoal)
        {
            if (_orders.Count == 0 && _dAiOrders + _dAiDelivered == 0) return;
            Log.Info("QohorMasters: dzien " + (int)CampaignTime.Now.ToDays + " - zlecen " + _orders.Count + " (gotowe " + _orders.Count(o => o.Ready) + ", AI " + _orders.Count(o => !o.IsPlayer)
                     + "); dzien pracy dostalo " + worked + ", bez wolnych rak kowali " + noHands + ", bez wegla " + noCoal + "; nowe zlecenia AI " + _dAiOrders + ", wydane AI " + _dAiDelivered + ".");
            _dAiOrders = _dAiDelivered = 0;
        }

        /// <summary>Wegiel z polki Qohoru (zuzyty przez mistrzow - z ich oplaty, zloto zostaje w miescie). Za malo - nic nie bierzemy.</summary>
        private static bool TakeCoal(Settlement q, int need)
        {
            if (need <= 0) return true;
            var coal = Recipes.MaterialItem(CraftingMaterials.Charcoal);
            var r = q.ItemRoster;
            if (coal == null || r == null) return true;
            if (r.GetItemNumber(coal) < need) return false;
            for (int i = r.Count - 1; i >= 0 && need > 0; i--)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.EquipmentElement.Item != coal || e.Amount <= 0) continue;
                int take = Math.Min(need, e.Amount);
                r.AddToCounts(e.EquipmentElement, -take);
                need -= take;
            }
            return true;
        }

        // ------------------------------------------------------------ AI: ta sama regula (krytyka pkt 21)
        internal static void OnDailyTickParty(MobileParty mp)
        {
            try
            {
                var s = Settings.Current;
                if (mp == null || mp.IsMainParty || !mp.IsLordParty || mp.LeaderHero == null || mp.CurrentSettlement == null) return;
                // recenzja 177: partia rodu gracza (towarzysz) nie zleca sama - o rzeczach rodu gracza decyduje gracz
                if (mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan) return;
                var q = Qohor();
                if (q == null || mp.CurrentSettlement != q) return;
                var lord = mp.LeaderHero;
                // odbior gotowych - w Daily (mistrzowie odsylaja od razu)
                if (!s.QohorReworkEnabled || !s.QohorAiRework || _orders.Any(x => x.Owner == lord.StringId)) return;
                var eq = lord.BattleEquipment;
                for (int i = 0; i < 4; i++)
                {
                    var el = eq[(EquipmentIndex)i];
                    if (el.IsEmpty || !ValyrianBlades.Is(el.Item) || ValyrianBlades.Measures(el.Item) != 2) continue;
                    var start = ValyrianBlades.StartOwner(el.Item.StringId);
                    if (start == null || start.Clan == null || start.Clan == lord.Clan) continue;   // klinga wlasnego rodu - po co niszczyc dziedzictwo
                    int fee = Fee(2);
                    if (lord.Gold < fee * 2) continue;
                    var pats = ResultPatterns();
                    if (pats.Count == 0) return;
                    // recenzja 177: straznik dla tego wzoru przed przyjeciem (jak u gracza) - kopia z niczego staje sie zwykla stala i nie wchodzi
                    if (ValyrianBlades.GuardNow(el.Item.StringId))
                    {
                        var now = eq[(EquipmentIndex)i];
                        if (now.IsEmpty || now.Item != el.Item) return;
                    }
                    GiveGoldAction.ApplyForCharacterToSettlement(lord, q, fee, false);
                    eq[(EquipmentIndex)i] = EquipmentElement.Invalid;
                    UniqueSpoils.ClearTwin(lord, el.Item.StringId);
                    var o = new Order { Owner = lord.StringId, Fee = fee, DaysLeft = Math.Max(1, s.QohorDays), DaysTotal = Math.Max(1, s.QohorDays), Town = q.StringId, Placed = (int)CampaignTime.Now.ToDays };
                    o.InIds.Add(el.Item.StringId); o.InMods.Add(el.ItemModifier != null ? el.ItemModifier.StringId : "");
                    o.OutIds.Add(pats[MBRandom.RandomInt(pats.Count)].StringId); o.OutIds.Add(pats[MBRandom.RandomInt(pats.Count)].StringId);
                    _orders.Add(o);
                    _dAiOrders++;
                    Log.Info("QohorMasters: " + lord.Name + " (" + (lord.Clan != null ? lord.Clan.Name.ToString() : "-") + ") zleca rozdzielenie " + el.Item.StringId + " zdobytego od rodu "
                             + start.Clan.Name + " na " + string.Join(" + ", o.OutIds.ToArray()) + " - oplata " + fee + " d do kasy " + q.Name + ".");
                    Log.Info("Kronika unikatow: " + el.Item.StringId + " u mistrzow Qohoru (zlecenie " + lord.Name + ").");
                    return;
                }
            }
            catch (Exception e) { if (_stumbles++ < 3) Log.Error("QohorMasters.OnDailyTickParty", e); }
        }

        // ------------------------------------------------------------ spis stali valyrianskiej
        /// <summary>Klingi wejscia zlecen do wydania - dla spisu ValyrianBlades ("u mistrzow Qohoru").</summary>
        internal static void AddCensus(Action<string, string> add)
        {
            foreach (var o in _orders)
                foreach (var id in o.InIds)
                    add(id, "u mistrzow Qohoru (" + (o.IsPlayer ? "gracz" : o.Owner) + (o.Ready ? ", gotowe" : ", zostalo " + o.DaysLeft + " dni") + ")");
        }

        // ------------------------------------------------------------ zapis
        /// <summary>"v1" + rekordy "#wlasciciel;wej1,wej2;mod1,mod2;wyn1,wyn2;zostalo;razem;oplata;miasto;wegiel;dzien;godziny".</summary>
        internal static string Export()
        {
            var sb = new StringBuilder("v1");
            var inv = CultureInfo.InvariantCulture;
            foreach (var o in _orders)
                sb.Append('#').Append(o.Owner).Append(';').Append(string.Join(",", o.InIds.ToArray())).Append(';').Append(string.Join(",", o.InMods.ToArray()))
                  .Append(';').Append(string.Join(",", o.OutIds.ToArray())).Append(';').Append(o.DaysLeft.ToString(inv)).Append(';').Append(o.DaysTotal.ToString(inv))
                  .Append(';').Append(o.Fee.ToString(inv)).Append(';').Append(o.Town).Append(';').Append(o.Coal ? "1" : "0").Append(';').Append(o.Placed.ToString(inv))
                  .Append(';').Append(o.Hours.ToString("0.##", inv));
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _orders.Clear();
            if (string.IsNullOrEmpty(s) || !s.StartsWith("v1")) return;
            int bad = 0;
            foreach (var rec in s.Substring(2).Split('#'))
            {
                if (rec.Length == 0) continue;
                var f = rec.Split(';');
                if (f.Length < 10) { bad++; continue; }
                var o = new Order { Owner = f[0], Town = f[7], Coal = f[8] == "1" };
                o.InIds.AddRange(f[1].Split(',').Where(x => x.Length > 0));
                o.InMods.AddRange(f[2].Split(','));
                o.OutIds.AddRange(f[3].Split(',').Where(x => x.Length > 0));
                int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out o.DaysLeft);
                int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out o.DaysTotal);
                int.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out o.Fee);
                int.TryParse(f[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out o.Placed);
                if (f.Length > 10) float.TryParse(f[10], NumberStyles.Float, CultureInfo.InvariantCulture, out o.Hours);
                while (o.InMods.Count < o.InIds.Count) o.InMods.Add("");
                if (o.InIds.Count == 0 || o.OutIds.Count == 0) { bad++; continue; }
                _orders.Add(o);
            }
            if (_orders.Count > 0 || bad > 0) Log.Info("QohorMasters: wczytano " + _orders.Count + " zlecen" + (bad > 0 ? " (bledne rekordy " + bad + " - pominiete)" : "") + ".");
        }
    }
}
