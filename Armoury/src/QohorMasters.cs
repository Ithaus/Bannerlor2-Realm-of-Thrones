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
    ///  - 563c (Jeff 15.11 "ile jest materialu w mieczu"): WEDLUG MASY zamiast miar. Jedna opcja menu: klinga w ogien, 1-2 wzory seryjne, kazdy bierze tyle
    ///    stali, ile wazy jego klinga (ValyrianBlades.SteelG). Brak - ze skrzynki resztek i sztab gracza (Order.ExtraG, w spisie do wydania); reszta przy wydaniu
    ///    - gracz: sztaby + skrzynka, AI: skrzynka cechu (GuildAdd). AI: lord z obcym wielkim mieczem (Measures 2) - tyle wzorow, ile miesci stal klingi.
    ///    Oplata od stali w ogniu (gramy / 800 x HistValyrianPerMeasure x toll), wegiel na kazde 800 g. Zlecenia sprzed 563c: Settle.
    /// </summary>
    internal static partial class QohorMasters
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
            public string Own = "";                                     // wariant B (QohorMasters.Own): spec projektu gracza; "" = przekucie we wzor seryjny
            public int ExtraG;                                          // 563c: dodatek stali gracza (gramy: skrzynka + otwarte sztaby) u mistrzow do wydania
            public bool Legacy;                                         // recenzja 563c/d: zlecenie przyjete przed 563c (zapis bez pola dodatku) - wlasny projekt kuty na 1 miare jak wtedy
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
        /// <summary>Wzory wyniku: seryjne klingi ROT (istniejace w grze); 563c - stal kazdego wedlug masy jego klingi (ValyrianBlades.SteelG).</summary>
        private static List<ItemObject> ResultPatterns()
        {
            var l = new List<ItemObject>();
            foreach (var id in ValyrianBlades.SerialIds)
            {
                var it = ValyrianBlades.Item(id);
                if (it != null) l.Add(it);
            }
            return l;
        }

        /// <summary>563c: dodatek stali gracza do zlecenia (bez ruszania): brak = outG - inG; najpierw skrzynka resztek, potem cale sztaby.</summary>
        private static int PlanExtra(int inG, int outG, out int fromBox, out int ingots)
        {
            fromBox = 0; ingots = 0;
            int lack = outG - inG;
            if (lack <= 0) return 0;
            fromBox = Math.Min(ValyrianBlades.BoxG, lack);
            int rest = lack - fromBox;
            ingots = rest <= 0 ? 0 : (rest + ValyrianBlades.IngotG - 1) / ValyrianBlades.IngotG;
            return fromBox + ingots * ValyrianBlades.IngotG;
        }

        private static string Kg(int g) { return ValyrianBlades.KgText(g); }

        /// <summary>563c: oplata = praca + oplata cechu od wartosci stali, ktora idzie w ogien (gramy x cena kg; 1 kg = HistValyrianPerMeasure / 0.8).</summary>
        internal static int Fee(int steelG)
        {
            var s = Settings.Current;
            float wage = HistoricalPrices.WageFor(6) * TownWage.Index(Qohor());
            float labor = Math.Max(1, s.QohorDays) * Math.Max(1, s.QohorCrew) * wage;
            float toll = Math.Max(0, steelG) / (float)ValyrianBlades.MeasureG * Math.Max(0f, s.HistValyrianPerMeasure) * Math.Max(0f, s.QohorTollPercent) / 100f;
            return Math.Max(1, (int)Math.Round(labor + toll));
        }

        private static string FeeText(int steelG)
        {
            var s = Settings.Current;
            float wage = HistoricalPrices.WageFor(6) * TownWage.Index(Qohor());
            int labor = (int)Math.Round(Math.Max(1, s.QohorDays) * Math.Max(1, s.QohorCrew) * wage);
            // recenzja 177: stawka z ustawien (Qohor Toll Percent), "szesnasta czesc" tylko przy 6.25
            float pct = Math.Max(0f, s.QohorTollPercent);
            string part = Math.Abs(pct - 6.25f) < 0.01f ? "a sixteenth part" : pct.ToString("0.##", CultureInfo.InvariantCulture) + "%";
            return Fee(steelG) + " gold (" + Math.Max(1, s.QohorDays) + " days of " + Math.Max(1, s.QohorCrew) + " masters: " + labor
                   + "; the guild's toll of " + part + " of the worth of " + Kg(steelG) + " kg of steel: " + (Fee(steelG) - labor) + ")";
        }

        /// <summary>563c: dodatki stali graczy u mistrzow (gramy) - do spisu stali valyrianskiej (stan i baza).</summary>
        internal static int HeldExtraG()
        {
            int g = 0;
            foreach (var o in _orders) g += Math.Max(0, o.ExtraG);
            return g;
        }

        // ------------------------------------------------------------ menu gracza
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                // n20-napisy: krotkie napisy (ShortLabels), dawny pelny napis w podpowiedzi
                starter.AddGameMenuOption("town", "arm_qohor_enter", ShortLabels.Menu("{=!}Seek out the masters of Qohor", "{=!}Seek out the Valyrian steel masters of Qohor"),
                    ShortLabels.Tip(delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Craft;
                        if (!InQohor()) return false;
                        if (!Settings.Current.QohorReworkEnabled && !PlayerOrders().Any()) return false;
                        int run = PlayerOrders().Count(o => !o.Ready), ready = PlayerOrders().Count(o => o.Ready);
                        if (run + ready > 0) a.Tooltip = new TextObject("{=!}" + (run > 0 ? run + " order(s) at the forge" : "") + (run > 0 && ready > 0 ? ", " : "") + (ready > 0 ? ready + " ready to collect" : "") + ".");
                        return true;
                    }, "The Valyrian steel masters of Qohor."),
                    delegate { GameMenu.SwitchToMenu(Menu); }, false, 5);
                starter.AddGameMenu(Menu, "{=!}{ARM_QOHOR_TEXT}", delegate (MenuCallbackArgs a) { MBTextManager.SetTextVariable("ARM_QOHOR_TEXT", MastersText()); });
                // 563c: jedna opcja wedlug masy (dawne "1 miara" i "wielki miecz na dwa") - jedna klinga w ogien, 1-2 nowe wzory, brak stali z sztab i resztek gracza
                starter.AddGameMenuOption(Menu, "arm_qohor_one", "{=!}Have a blade reforged in a new form",
                    delegate (MenuCallbackArgs a) { return OrderCondition(a); },
                    delegate { ChooseBlade(); }, false, 0);
                starter.AddGameMenuOption(Menu, "arm_qohor_own", ShortLabels.Menu("{=!}Forge a blade of your own design", "{=!}Have a blade forged to your own design and name"),
                    ShortLabels.Tip(delegate (MenuCallbackArgs a) { return OwnCondition(a); }, "Have a blade forged to your own design and name."),
                    delegate { OwnChooseBlade(); }, false, 1);
                starter.AddGameMenuOption(Menu, "arm_qohor_waitopt", ShortLabels.Menu("{=!}Wait until the masters are done", "{=!}Wait in Qohor until the masters are done"),
                    ShortLabels.Tip(delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Wait; return PlayerOrders().Any(o => !o.Ready); }, "Wait in Qohor until the masters are done."),
                    delegate { GameMenu.SwitchToMenu(WaitMenu); }, false, 2);
                starter.AddGameMenuOption(Menu, "arm_qohor_collect", "{=!}Collect your reforged steel",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Trade; return PlayerOrders().Any(o => o.Ready); },
                    delegate { Collect(); GameMenu.SwitchToMenu(Menu); }, false, 3);
                starter.AddGameMenuOption(Menu, "arm_qohor_back", "{=!}Take back your blade",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Leave;
                        if (!PlayerOrders().Any(o => !o.Ready)) return false;
                        a.Tooltip = new TextObject("{=!}The masters give back the blade as it was, and any Valyrian steel of yours they took. Their fee is not returned - the work has begun.");
                        return true;
                    },
                    delegate { TakeBack(); }, false, 4);
                // m95-menu (recenzja): napis mowi, dokad prowadzi - "Back to the smithy", gdy wejscie stoi w "Go to the smithy"; inaczej jak dotad "Leave"
                starter.AddGameMenuOption(Menu, "arm_qohor_leave", "{=!}{ARM_QOHOR_LEAVE}",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; TownMenus.SetBackLabel(a, "ARM_QOHOR_LEAVE", "arm_qohor_enter", "Leave"); return true; },
                    delegate { GameMenu.SwitchToMenu(TownMenus.BackTo("arm_qohor_enter")); }, true, 9);   // m95-menu: do "Go to the smithy", gdy wejscie tam przeniesione

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
                         + " wpiete; zlecen w toku " + _orders.Count + "; oplata za klinge 0.8 kg " + Fee(ValyrianBlades.MeasureG) + " d, 1.6 kg " + Fee(2 * ValyrianBlades.MeasureG) + " d (563c: wedlug masy); dodatki stali u mistrzow " + HeldExtraG() + " g; wzorow wyniku " + ResultPatterns().Count + "; wlasny projekt gracza (177-2 B) " + (OwnOn ? "TAK" : "NIE") + ", wlasnych mieczy w rejestrze " + ValyrianBlades.OwnCount + ".");
            }
            catch (Exception e) { Log.Error("QohorMasters.AddMenus", e); }
        }

        private static IEnumerable<Order> PlayerOrders() { return _orders.Where(o => o.IsPlayer); }

        private static string MastersText()
        {
            var sb = new StringBuilder("Beneath the temple of the Black Goat the smiths of Qohor keep the last secret of Valyria. No one has made Valyrian steel since the Doom - but these masters can melt it and forge it anew, and nothing of it is lost.");
            // 563c: wedlug masy - nowa klinga bierze tyle stali, ile wazy; reszta wraca jako sztaby i resztki, brak - z sztab i resztek gracza
            sb.Append("\n\nBring a blade in your baggage (not on your back). The masters melt it and forge one or two new blades from its steel, each taking as much as it weighs - a Valyrian Steel Sword "
                      + Kg(ValyrianBlades.MeasureG) + " kg; Ice holds " + Kg(ValyrianBlades.SteelG("ice_sword")) + " kg. What is left over comes back to you as ingots of Valyrian steel and offcuts; "
                      + "if the new blades need more than the old one holds, the masters take it from your ingots and offcuts. ");
            if (OwnOn) sb.Append("A blade can also be forged to a design of your own from the forge - your guard, grip and pommel - and bear the name you give it. ");
            sb.Append("The work takes " + Math.Max(1, Settings.Current.QohorDays) + " days of their hands (longer when the smiths of Qohor are busy). Their fee for " + Kg(ValyrianBlades.MeasureG)
                      + " kg of steel: " + FeeText(ValyrianBlades.MeasureG) + "; more steel, a higher toll.");
            if (ValyrianBlades.BoxG > 0 || ValyrianBlades.IngotsInBaggage() > 0)
                sb.Append(" You have " + ValyrianBlades.IngotsInBaggage() + " ingots of Valyrian steel in your baggage and " + Kg(ValyrianBlades.BoxG) + " kg of offcuts.");
            foreach (var o in PlayerOrders())
                sb.Append("\n\n" + Describe(o) + (o.Ready ? " - READY." : " - " + o.DaysLeft + " day(s) of work left" + (o.Coal ? "" : ", waiting for charcoal") + "."));
            return sb.ToString();
        }

        private static string Describe(Order o)
        {
            return string.Join(" and ", o.InIds.Select(id => NameOf(id)).ToArray()) + " into " + OutNames(o);
        }

        /// <summary>Nazwy wyniku zlecenia; wlasny projekt (wariant B) - nadana nazwa.</summary>
        private static string OutNames(Order o)
        {
            return string.Join(" and ", o.OutIds.Select(id => id == OwnOut ? SpecName(o.Own) + " (your own design)" : NameOf(id)).ToArray());
        }

        private static string NameOf(string id) { var it = ValyrianBlades.Item(id); return it != null ? it.Name.ToString() : id; }

        /// <summary>563c: wszystkie klingi VS w taborze gracza (bez podzialu na miary - stal liczona wedlug masy).</summary>
        private static List<EquipmentElement> Blades()
        {
            var l = new List<EquipmentElement>();
            var r = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
            if (r == null) return l;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Amount > 0 && ValyrianBlades.Is(e.EquipmentElement.Item)) l.Add(e.EquipmentElement);
            }
            return l;
        }

        /// <summary>563c: najmniejsza oplata za klinge z taboru (oplata rosnie ze stala w ogniu) - do warunku opcji menu.</summary>
        private static int MinFee(List<EquipmentElement> blades)
        {
            int min = int.MaxValue;
            foreach (var el in blades) min = Math.Min(min, ValyrianBlades.SteelG(el.Item.StringId));
            return Fee(min == int.MaxValue ? ValyrianBlades.MeasureG : min);
        }

        private static bool OrderCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Craft;
            if (!Settings.Current.QohorReworkEnabled) return false;
            if (ResultPatterns().Count == 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The masters have no patterns to forge (Valyrian swords of the realm are missing from the game)."); return true; }
            var blades = Blades();
            if (blades.Count == 0)
            {
                a.IsEnabled = false;
                a.Tooltip = new TextObject("{=!}You carry no Valyrian blade in your baggage. A blade you wear must be put in your baggage first.");
                return true;
            }
            int fee = MinFee(blades);
            if (Hero.MainHero.Gold < fee)
            {
                a.IsEnabled = false;
                a.Tooltip = new TextObject("{=!}The masters ask at least " + fee + " gold. You have " + Hero.MainHero.Gold + ".");
                return true;
            }
            a.Tooltip = new TextObject("{=!}Fee from " + fee + " gold (more steel in the fire, a higher toll). " + Math.Max(1, Settings.Current.QohorDays) + " days of work.");
            return true;
        }

        private static void ChooseBlade()
        {
            try
            {
                var els = new List<InquiryElement>();
                var r = MobileParty.MainParty.ItemRoster;
                foreach (var el in Blades())
                {
                    int n = r.GetItemNumber(el.Item);
                    els.Add(new InquiryElement(el, el.Item.Name + (el.ItemModifier != null ? " (" + el.ItemModifier.Name + ")" : "") + (n > 1 ? "  x" + n : ""), SmithMenu.ItemPic(el.Item), true,
                        Kg(ValyrianBlades.SteelG(el.Item.StringId)) + " kg of Valyrian steel."));
                }
                if (els.Count == 0) return;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Masters of Qohor", "Which blade goes into the fire?", els, true, 1, 1, "This one", "Not now",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0 || !(sel[0].Identifier is EquipmentElement)) return;
                        ChoosePattern((EquipmentElement)sel[0].Identifier);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.ChooseBlade", e); }
        }

        /// <summary>563c: wybor 1-2 wzorow; wzor ciezszy niz stal pod reka (klinga + sztaby + resztki) - szary z powodem. Jeden wzor, a stali starczy na
        /// dwa - pytanie "jeden czy dwa".</summary>
        private static void ChoosePattern(EquipmentElement blade)
        {
            try
            {
                int inG = ValyrianBlades.SteelG(blade.Item.StringId);
                int pool = inG + ValyrianBlades.SteelAvailableG();
                var els = new List<InquiryElement>();
                foreach (var it in ResultPatterns())
                {
                    int g = ValyrianBlades.SteelG(it.StringId);
                    if (it == blade.Item && 2 * g > pool) continue;   // ten sam ksztalt jeden raz - nic by sie nie zmienilo
                    bool ok = g <= pool;
                    els.Add(new InquiryElement(it, it.Name.ToString(), SmithMenu.ItemPic(it), ok, Kg(g) + " kg of Valyrian steel."
                        + (ok ? "" : " Not enough steel: the blade, your ingots and offcuts make " + Kg(pool) + " kg.")));
                }
                if (els.Count == 0) return;
                string ask = "Choose the shape of the new blade, or two patterns for two blades. " + blade.Item.Name + " gives " + Kg(inG) + " kg of steel; with your ingots and offcuts you have "
                             + Kg(pool) + " kg.";
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Masters of Qohor", ask, els, true, 1, 2, "Forge it", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0) return;
                        var outs = sel.Select(x => x.Identifier as ItemObject).Where(x => x != null).ToList();
                        if (outs.Count == 0) return;
                        int outG = outs.Sum(x => ValyrianBlades.SteelG(x.StringId));
                        if (outG > pool) { Log.Player("There is not enough Valyrian steel for both: they need " + Kg(outG) + " kg and you have " + Kg(pool) + " kg.", true); return; }
                        if (outs.Count == 1 && 2 * outG <= pool)
                        {
                            var one = outs[0];
                            InformationManager.ShowInquiry(new InquiryData("The Masters of Qohor",
                                "There is steel enough for two blades of " + one.Name + " (" + Kg(outG) + " kg each). One blade or two?", true, true, "Two blades", "One blade",
                                delegate { Confirm(blade, new List<ItemObject> { one, one }); },
                                delegate { Confirm(blade, new List<ItemObject> { one }); }), true);
                            return;
                        }
                        Confirm(blade, outs);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.ChoosePattern", e); }
        }

        /// <summary>563c: opis stali zlecenia dla gracza - dodatek z resztek i sztab, reszta do odebrania.</summary>
        private static string SteelText(int inG, int outG)
        {
            int fromBox, k;
            int extra = PlanExtra(inG, outG, out fromBox, out k);
            int left = inG + extra - outG;
            string t = "";
            if (extra > 0)
                t += " The new steel needs " + Kg(outG - inG) + " kg more than the old blade holds: the masters take " + (fromBox > 0 ? Kg(fromBox) + " kg of your offcuts" : "")
                     + (fromBox > 0 && k > 0 ? " and " : "") + (k > 0 ? k + (k == 1 ? " ingot" : " ingots") + " of Valyrian steel" : "") + ".";
            if (left > 0) t += " What is left - " + Kg(left) + " kg - comes back to you as ingots and offcuts.";
            return t;
        }

        private static void Confirm(EquipmentElement blade, List<ItemObject> outs)
        {
            int inG = ValyrianBlades.SteelG(blade.Item.StringId);
            int outG = outs.Sum(x => ValyrianBlades.SteelG(x.StringId));
            int fb, k;
            int fee = Fee(inG + PlanExtra(inG, outG, out fb, out k));
            int days = Math.Max(1, Settings.Current.QohorDays);
            string result = string.Join(" and ", outs.Select(x => x.Name.ToString()).ToArray());
            InformationManager.ShowInquiry(new InquiryData("The Masters of Qohor",
                blade.Item.Name + " (" + Kg(inG) + " kg of Valyrian steel) goes into the fire. In " + days + " days of work the masters will hand you " + result + " (" + Kg(outG) + " kg)."
                + SteelText(inG, outG) + " Their fee of " + fee + " gold is paid now and is not returned.", true, true, "Pay " + fee, "Not now",
                delegate { Place(blade, outs, fee, days, null, outG); },
                delegate { }), true);
        }

        /// <summary>Przyjecie zlecenia gracza. 563c: outG - gramy wyniku; brak stali (outG ponad klinge) schodzi ze skrzynki resztek i z sztab taboru do
        /// o.ExtraG (u mistrzow do wydania - w spisie), reszta wraca przy wydaniu.</summary>
        private static void Place(EquipmentElement blade, List<ItemObject> outs, int fee, int days, string own, int outG)
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
                int inG = ValyrianBlades.SteelG(blade.Item.StringId);
                int extra = 0, k = 0, fromBox = 0, wasBox = ValyrianBlades.BoxG;
                if (outG > inG)
                {
                    // 563c: straznik sztab przed pobraniem (kopia z niczego nie wejdzie do Qohoru jako prawdziwa stal)
                    if (ValyrianBlades.IngotsInBaggage() > 0) ValyrianBlades.GuardIngotsNow("przed zleceniem w Qohorze");
                    if (!ValyrianBlades.TakeSteel(outG - inG, out extra, out k, out fromBox))
                    {
                        Log.Player("You no longer have the Valyrian steel the masters need: " + Kg(outG - inG) + " kg more than the blade holds.", true);
                        return;
                    }
                }
                GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, q, fee, true);
                r.AddToCounts(blade, -1);   // dokladna sztuka (z modyfikatorem) - zwrot przy "Take back" w tym samym stanie (krytyka pkt 12)
                var o = new Order
                {
                    Owner = PlayerOwner, Fee = fee, DaysLeft = days, DaysTotal = days, Town = q.StringId, Placed = (int)CampaignTime.Now.ToDays, ExtraG = extra
                };
                o.InIds.Add(blade.Item.StringId); o.InMods.Add(blade.ItemModifier != null ? blade.ItemModifier.StringId : "");
                foreach (var it in outs) o.OutIds.Add(it.StringId);
                if (!string.IsNullOrEmpty(own)) { o.Own = own; o.OutIds.Add(OwnOut); _dOwnPlaced++; }   // wariant B: wlasny projekt gracza
                _orders.Add(o);
                Log.Info("QohorMasters: gracz zleca " + Describe(o) + " - oplata " + fee + " d do kasy " + q.Name + ", " + days + " dni pracy (masa " + inG + " g + dodatek " + extra
                         + " g: skrzynka VS " + wasBox + " -> " + ValyrianBlades.BoxG + " g, sztaby " + k + "; wynik " + outG + " g" + (string.IsNullOrEmpty(own) ? "" : ", wlasny projekt gracza") + ").");
                Log.Player("The masters of Qohor take " + blade.Item.Name + (extra > 0 ? " and " + Kg(extra) + " kg of your Valyrian steel" : "") + " into their forge. Come back in " + days + " days, or wait here.");
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
            if (!string.IsNullOrEmpty(o.Own)) { DeliverOwn(o); return; }   // wariant B: wlasny projekt gracza (QohorMasters.Own)
            var outs = o.OutIds.Select(ValyrianBlades.Item).ToList();
            if (outs.Any(x => x == null))
            {
                Log.Info("QohorMasters: wzor wyniku zniknal z gry (" + string.Join(", ", o.OutIds.ToArray()) + ") - mistrzowie oddaja " + string.Join(", ", o.InIds.ToArray()) + ".");
                ReturnInputs(o);
                _orders.Remove(o);
                return;
            }
            // 563c: bilans w gramach - wynik bierze tyle stali, ile wazy; reszta do gracza (sztaby + skrzynka) albo do skrzynki cechu (AI)
            int inG = o.InIds.Sum(id => ValyrianBlades.SteelG(id)), outG = outs.Sum(x => ValyrianBlades.SteelG(x.StringId));
            string legacy = Settle(o, inG, ref outs, ref outG);
            if (legacy == null)
            {
                // recenzja 563c/d: zlecenie AI sprzed 563c, jeden wzor ciezszy niz klinga (np. Blackfyre 700 g -> wzor 730 g) - mistrzowie oddaja klinge
                // zamiast tworzyc stal z niczego (oplata zostaje w kasie Qohoru, jak przy nieudanym wlasnym projekcie)
                Log.Info("QohorMasters: zlecenie AI sprzed 563c - wzor ciezszy niz klinga (" + inG + " g + dodatek " + o.ExtraG + " g < " + outG + " g) - mistrzowie oddaja "
                         + string.Join(", ", o.InIds.ToArray()) + " wlascicielowi; oplata zostaje w kasie Qohoru.");
                ReturnInputs(o);
                _orders.Remove(o);
                return;
            }
            int left = inG + o.ExtraG - outG;
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
            string rest = LeftOver(o, left);
            Log.Info("Kronika unikatow: Qohor - " + string.Join(" + ", o.InIds.ToArray()) + " przekuty na " + string.Join(" + ", o.OutIds.ToArray())
                     + " (masa " + inG + " g + dodatek " + o.ExtraG + " g -> " + outG + " g, reszta " + left + " g: " + rest + legacy + "), odbiera " + to + ".");
        }

        /// <summary>563c: zlecenie sprzed masy (przyjete na miary, bez dodatku) moze potrzebowac wiecej stali niz klinga: gracz - dobranie ze skrzynki i sztab;
        /// dalej za malo - wynik bez ostatnich wzorow (Lod AI: jeden miecz zamiast dwoch); jeden wzor dalej ciezszy - wyrownanie startu bilansu (NoteLegacy).
        /// Zwraca dopisek do kroniki ("" - nic); null - zlecenie AI, ktorego jeden wzor dalej jest ciezszy (Deliver oddaje klinge, recenzja 563c/d).</summary>
        private static string Settle(Order o, int inG, ref List<ItemObject> outs, ref int outG)
        {
            if (inG + o.ExtraG >= outG) return "";
            string note = "";
            if (o.IsPlayer)
            {
                int extra, k, fb;
                int lack = outG - inG - o.ExtraG;
                if (ValyrianBlades.TakeSteel(lack, out extra, out k, out fb))
                {
                    o.ExtraG += extra;
                    Log.Player("The masters of Qohor take " + Kg(extra) + " kg of your Valyrian steel to finish the work.");
                    return "; zlecenie sprzed 563c - dobrane " + extra + " g (skrzynka " + fb + " g, sztaby " + k + ")";
                }
            }
            while (outs.Count > 1 && inG + o.ExtraG < outG)
            {
                var last = outs[outs.Count - 1];
                outs.RemoveAt(outs.Count - 1);
                o.OutIds.RemoveAt(o.OutIds.Count - 1);
                outG -= ValyrianBlades.SteelG(last.StringId);
                note += "; zlecenie sprzed 563c - bez " + last.StringId + " (za malo stali)";
            }
            if (inG + o.ExtraG < outG && !o.IsPlayer) return null;   // recenzja 563c/d: AI - zwrot klingi w Deliver, bez stali z niczego
            if (inG + o.ExtraG < outG)
            {
                int gap = outG - inG - o.ExtraG;
                ValyrianBlades.NoteLegacy(gap, "zlecenie Qohoru sprzed 563c (" + string.Join("+", o.InIds.ToArray()) + " -> " + string.Join("+", o.OutIds.ToArray()) + ")");
                o.ExtraG += gap;
                note += "; zlecenie sprzed 563c - wyrownanie +" + gap + " g";
            }
            return note;
        }

        /// <summary>563c: reszta stali zlecenia - gracz: skrzynka resztek i pelne sztaby do taboru; AI: skrzynka cechu Qohoru. Zwraca opis do kroniki.</summary>
        private static string LeftOver(Order o, int left)
        {
            o.ExtraG = 0;
            if (left <= 0) return "nic";
            if (o.IsPlayer)
            {
                int j; int wasBox = ValyrianBlades.BoxG;
                ValyrianBlades.GiveSteel(left, out j);
                Log.Player("The masters return what is left of the steel: " + Kg(left) + " kg" + (j > 0 ? " (" + j + (j == 1 ? " ingot" : " ingots") + " in your baggage)" : "") + ".");
                return j + " sztab do taboru, skrzynka VS " + wasBox + " -> " + ValyrianBlades.BoxG + " g";
            }
            ValyrianBlades.GuildAdd(left);
            return "do skrzynki cechu (razem " + ValyrianBlades.GuildG + " g)";
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
            // 563c: dodatek stali gracza wraca (sztaby + skrzynka) - zlecenie i tak znika zaraz po zwrocie
            if (o.ExtraG > 0)
            {
                int g = o.ExtraG;
                o.ExtraG = 0;
                if (o.IsPlayer) { int j; ValyrianBlades.GiveSteel(g, out j); }
                else ValyrianBlades.GuildAdd(g);
                Log.Info("QohorMasters: zwrot dodatku stali " + g + " g (" + (o.IsPlayer ? "gracz, skrzynka VS " + ValyrianBlades.BoxG + " g" : "cech") + ").");
            }
        }

        private static void TakeBack()
        {
            try
            {
                var run = PlayerOrders().Where(o => !o.Ready).ToList();
                if (run.Count == 0) return;
                // n20-napisy (recenzja): wiersz z obrazkiem ma 333 px i najwyzej 2 linie - "X into Y and Z" z nazwami ROT mialo 3 linie;
                // na przycisku klinga oddana mistrzom i dni, pelny opis zlecenia na gorze podpowiedzi
                var els = run.Select(o => new InquiryElement(o,
                                                             ShortLabels.Pick(NameOf(o.InIds[0]) + (o.InIds.Count > 1 ? " +" + (o.InIds.Count - 1) : "") + " - "
                                                                              + (o.DaysLeft == 1 ? "1 day left" : o.DaysLeft + " days left"),
                                                                              Describe(o) + " - " + o.DaysLeft + " day(s) left"),
                                                             SmithMenu.ItemPic(ValyrianBlades.Item(o.InIds[0])), true,
                                                             ShortLabels.Hint(Describe(o) + ".", "The fee of " + o.Fee + " gold is not returned."))).ToList();
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
                        // 563c: wegiel wedlug masy stali w ogniu - na kazde 800 g (miara), co najmniej raz
                        int steel = o.InIds.Sum(id => ValyrianBlades.SteelG(id)) + o.ExtraG;
                        int coalNeed = Math.Max(0, s.QohorCharcoalPerMeasure) * Math.Max(1, (steel + ValyrianBlades.MeasureG - 1) / ValyrianBlades.MeasureG);
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
                        if (o.IsPlayer) Log.Player("The masters of Qohor have finished " + OutNames(o) + ".");
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
            if (_orders.Count == 0 && _dAiOrders + _dAiDelivered + _dOwnPlaced + _dOwnMade + _dOwnFailed == 0) return;
            Log.Info("QohorMasters: dzien " + (int)CampaignTime.Now.ToDays + " - zlecen " + _orders.Count + " (gotowe " + _orders.Count(o => o.Ready) + ", AI " + _orders.Count(o => !o.IsPlayer)
                     + "); dzien pracy dostalo " + worked + ", bez wolnych rak kowali " + noHands + ", bez wegla " + noCoal + "; nowe zlecenia AI " + _dAiOrders + ", wydane AI " + _dAiDelivered + OwnLine() + ".");
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
                    // 563c: wedlug masy - tyle losowych wzorow seryjnych (najwyzej 2), ile miesci stal klingi; reszta do skrzynki cechu przy wydaniu
                    int inG = ValyrianBlades.SteelG(el.Item.StringId);
                    int fee = Fee(inG);
                    if (lord.Gold < fee * 2) continue;
                    var pats = ResultPatterns();
                    if (pats.Count == 0) return;
                    var picks = new List<ItemObject>();
                    int sumG = 0;
                    while (picks.Count < 2)
                    {
                        var fit = pats.Where(x => sumG + ValyrianBlades.SteelG(x.StringId) <= inG).ToList();
                        if (fit.Count == 0) break;
                        var pick = fit[MBRandom.RandomInt(fit.Count)];
                        picks.Add(pick);
                        sumG += ValyrianBlades.SteelG(pick.StringId);
                    }
                    if (picks.Count == 0) continue;   // zaden wzor sie nie miesci - lord zostaje z klinga
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
                    foreach (var pk in picks) o.OutIds.Add(pk.StringId);
                    _orders.Add(o);
                    _dAiOrders++;
                    Log.Info("QohorMasters: " + lord.Name + " (" + (lord.Clan != null ? lord.Clan.Name.ToString() : "-") + ") zleca przekucie " + el.Item.StringId + " zdobytego od rodu "
                             + start.Clan.Name + " na " + string.Join(" + ", o.OutIds.ToArray()) + " (masa " + inG + " g -> " + sumG + " g, reszta " + (inG - sumG) + " g do cechu) - oplata " + fee + " d do kasy " + q.Name + ".");
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
        /// <summary>"v1" + rekordy "#wlasciciel;wej1,wej2;mod1,mod2;wyn1,wyn2;zostalo;razem;oplata;miasto;wegiel;dzien;godziny;spec;dodatek" (spec - wariant B, "" = brak;
        /// dodatek - 563c, gramy stali gracza u mistrzow; starszy zapis bez pola = 0; naglowek zostaje v1 - starszy DLL czyta zlecenia dalej;
        /// pole 14 - recenzja 563c/d: "1" = zlecenie sprzed 563c).</summary>
        internal static string Export()
        {
            var sb = new StringBuilder("v1");
            var inv = CultureInfo.InvariantCulture;
            foreach (var o in _orders)
                sb.Append('#').Append(o.Owner).Append(';').Append(string.Join(",", o.InIds.ToArray())).Append(';').Append(string.Join(",", o.InMods.ToArray()))
                  .Append(';').Append(string.Join(",", o.OutIds.ToArray())).Append(';').Append(o.DaysLeft.ToString(inv)).Append(';').Append(o.DaysTotal.ToString(inv))
                  .Append(';').Append(o.Fee.ToString(inv)).Append(';').Append(o.Town).Append(';').Append(o.Coal ? "1" : "0").Append(';').Append(o.Placed.ToString(inv))
                  .Append(';').Append(o.Hours.ToString("0.##", inv)).Append(';').Append(o.Own ?? "").Append(';').Append(o.ExtraG.ToString(inv))
                  .Append(';').Append(o.Legacy ? "1" : "0");
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
                if (f.Length > 11) o.Own = f[11];   // wariant B: spec wlasnego projektu (starszy zapis - brak pola)
                if (f.Length > 12) { int.TryParse(f[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out o.ExtraG); o.ExtraG = Math.Max(0, o.ExtraG); }   // 563c
                // recenzja 563c/d: zapis bez pola dodatku (12 pol) = zlecenie sprzed 563c; potem flaga w polu 14 (starszy DLL 563c ja pomija)
                o.Legacy = f.Length <= 12 || (f.Length > 13 && f[13] == "1");
                while (o.InMods.Count < o.InIds.Count) o.InMods.Add("");
                if (o.InIds.Count == 0 || o.OutIds.Count == 0) { bad++; continue; }
                _orders.Add(o);
            }
            if (_orders.Count > 0 || bad > 0) Log.Info("QohorMasters: wczytano " + _orders.Count + " zlecen" + (bad > 0 ? " (bledne rekordy " + bad + " - pominiete)" : "") + ".");
        }
    }
}
