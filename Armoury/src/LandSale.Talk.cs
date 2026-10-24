using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m113-zakup - ROZMOWA, OKNA I MENU KUPNA ZIEMI (zasady w LandSale.cs).
    ///  - Rozmowa (hero_main_options, priorytet 105; warunek: glowa rodu, ktora moglaby cos sprzedac): "I hear your house needs coin. Would you sell me land?"
    ///    Bez potrzeby: "My lands are not for sale. A house sells land only when it must." Z potrzeba: potrzeba slowami, rozmowa sie konczy, a po niej (TickEvent,
    ///    bez rozmowy i bez innego okna) okno wyboru - wiersz na kazda ziemie (nazwa, rodzaj, dochod dnia, cena, lata zwrotu; niedostepne wyszarzone z powodem),
    ///    potem okno rachunku (dochod dnia, ile dob mierzony, lata, wojna, upust i dlaczego, cena, zwrot, dokad ida pieniadze) z "Buy" / "Not now".
    ///  - Menu: arm_sidevil_buy w menu wioski pobocznej ("Offer to buy this village from {holder}", indeks 5, dodane po opcjach arm_sidevil), arm_land_lord w menu
    ///    wsi gry ("Offer to buy the lordship of {village}", indeks 8, B2), arm_side_fief_sellback w menu wioski gracza ("Offer {village} back to {holder}", Z7) -
    ///    wylaczone z powodem w podpowiedzi. Wycena w warunku raz na godzine gry na osade (warunek menu nie liczy ksiegi co klatke).
    ///  - Autotest (DLL autotestu CrashScribe, LogGate.Mode 1/2, plik armoury-ziemia.flag): raz na sesje - linia na sucho, potrzeba wymuszona (linia), kupno najblizszej
    ///    wioski, sprawdzenie pieniadza swiata, kiesy sprzedawcy i jednorazowych, wymuszony blad przeniesienia (zwrot co do grosza), odkup bez blokady 182 dob, zapis
    ///    tam i z powrotem trzech napisow; Lordship i zamek - gdy jest oferta i zloto.
    /// </summary>
    internal static partial class LandSale
    {
        internal const string OptSideBuy = "arm_sidevil_buy", OptLord = "arm_land_lord", OptSellBack = "arm_side_fief_sellback";

        private static List<Offer> _winOffers;      // oferty do okna po rozmowie
        private static string _winTitle;
        private static long _talkCheck;
        private sealed class MenuC { public int Hour = -1; public string Key; public Offer O; }
        private static readonly MenuC _mSide = new MenuC(), _mLord = new MenuC(), _mBack = new MenuC();

        private static void TalkReset()
        {
            _winOffers = null; _winTitle = null; _talkCheck = 0;
            _mSide.Hour = _mLord.Hour = _mBack.Hour = -1; _mSide.O = _mLord.O = _mBack.O = null;
        }

        private static bool ConversationOn()
        {
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            return cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive);
        }

        // ============================================================ rozmowa
        /// <summary>OnAfterSessionLaunched: linie rozmowy. Nie rzuca.</summary>
        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                starter.AddPlayerLine("arm_land_ask", "hero_main_options", "arm_land_r", "{=!}I hear your house needs coin. Would you sell me land?", AskCond, AskCons, 105);
                starter.AddDialogLine("arm_land_no", "arm_land_r", "hero_main_options", "{=!}{ARM_LAND_REPLY}", NoNeedCond, null, 100);
                starter.AddDialogLine("arm_land_yes", "arm_land_r", "close_window", "{=!}{ARM_LAND_REPLY}", NeedCond, YesCons, 99);
                Log.Info(P + "rozmowa wpieta (hero_main_options, priorytet 105).");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        private static bool AskCond()
        {
            try { return CouldSell(Hero.OneToOneConversationHero); }
            catch (Exception e) { Stumble("AskCond", e); return false; }
        }

        private static void AskCons()
        {
            try
            {
                _dAsk++;
                var h = Hero.OneToOneConversationHero;
                var l = OffersOf(h);
                // potrzeba rodu wprost (najszersza miara - wioska poboczna, Z2), nie z ofert: oferta odrzucona wczesniej (relacja, wojna) nie liczy potrzeby
                var first = new Offer();
                bool need = h != null && h.Clan != null && NeedFor(h.Clan, KSide, first);
                _winOffers = need ? l : null;
                _winTitle = h != null && h.Clan != null ? "Land of " + HouseOf(h.Clan) : "Land for sale";
                int ok = 0; foreach (var o in l) if (o.Ok) ok++;
                string reply;
                if (!need) reply = "My lands are not for sale. A house sells land only when it must.";
                else reply = first.NeedEn + " " + (ok > 0 ? "I may part with some land - at its true worth, not for a song. My steward will show you what I would sell."
                                                          : "Yet there is nothing I could sell you now - my steward will tell you why.");
                MBTextManager.SetTextVariable("ARM_LAND_REPLY", reply);
                Log.Info(P + "rozmowa - " + NameOf(h) + " (" + (h != null && h.Clan != null ? h.Clan.StringId : "-") + "): ziem policzonych " + l.Count + ", na sprzedaz " + ok
                         + ", potrzeba " + (need ? first.NeedPl : "brak") + ".");
            }
            catch (Exception e) { _winOffers = null; MBTextManager.SetTextVariable("ARM_LAND_REPLY", "My lands are not for sale."); Stumble("AskCons", e); }
        }

        private static bool NoNeedCond() { return _winOffers == null; }
        private static bool NeedCond() { return _winOffers != null; }
        private static void YesCons() { /* okno po rozmowie - TalkTick */ }

        /// <summary>LandSale.Tick: okno ofert po rozmowie (bez rozmowy i bez innego okna). Nie rzuca.</summary>
        private static void TalkTick()
        {
            if (_winOffers == null) return;
            long tq = Stopwatch.GetTimestamp();
            if (tq - _talkCheck < Stopwatch.Frequency / 4) return;
            _talkCheck = tq;
            if (_winOffers != null && !ConversationOn() && !InformationManager.IsAnyInquiryActive())
            {
                var l = _winOffers; _winOffers = null;
                ShowList(_winTitle, l);
            }
        }

        // ============================================================ okna
        internal static string KindEn(Offer o)
        {
            if (o.Kind == KSide) return "side village of " + (o.St != null ? Txt(o.St.Name) : "?");
            if (o.Kind == KLord) return "lordship of the village";
            return o.Seat ? "castle, seat of its house" : "castle";
        }

        private static void ShowList(string title, List<Offer> l)
        {
            try
            {
                _dWindows++;
                var opts = new List<InquiryElement>();
                l.Sort((a, b) => a.Ok != b.Ok ? (a.Ok ? -1 : 1) : a.Price.CompareTo(b.Price));
                foreach (var o in l)
                {
                    string lab = o.Name + " (" + KindEn(o) + ")" + (o.Ok ? " - " + o.NetDay.ToString("N0", Inv) + " a day - " + o.Price.ToString("N0", Inv) + " (" + Payback(o).ToString("0.0", Inv) + " years)" : "");
                    string hint = o.Ok ? "Income about " + o.NetDay.ToString("N0", Inv) + " a day; price " + o.Price.ToString("N0", Inv) + "; pays itself back in about " + Payback(o).ToString("0.0", Inv) + " years." : o.Why;
                    opts.Add(new InquiryElement(o, lab, null, o.Ok, hint));
                }
                if (opts.Count == 0) { InformationManager.ShowInquiry(new InquiryData(title, "There is no land to sell.", true, false, "Done", null, null, null), true); return; }
                string body = "What the house would sell, at its true worth: what each holding earns its lord a year, net of its costs, times the years of purchase. Your gold: "
                              + (Hero.MainHero != null ? Hero.MainHero.Gold.ToString("N0", Inv) : "0") + ".";
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, body, opts, true, 1, 1, "Look closer", "Not now",
                    delegate (List<InquiryElement> sel)
                    {
                        try { if (sel != null && sel.Count > 0) ShowDeal(sel[0].Identifier as Offer); }
                        catch (Exception e) { Stumble("ShowList.Wybor", e); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Stumble("ShowList", e); }
        }

        /// <summary>Okno rachunku oferty: dochod, doby pomiaru, lata, wojna, upust i dlaczego, cena, zwrot, dokad ida pieniadze; "Buy" / "Not now".</summary>
        internal static void ShowDeal(Offer o)
        {
            if (o == null) return;
            try
            {
                var me = Hero.MainHero;
                var s = Settings.Current;
                var sb = new StringBuilder(900);
                if (o.SellBack) sb.Append(NameOf(o.Head)).Append(" of ").Append(HouseOf(o.Seller)).Append(" would buy ").Append(o.Name).Append(" back from you.\n\n");
                else sb.Append(o.Name).Append(", ").Append(KindEn(o)).Append(" - sold by ").Append(NameOf(o.Head)).Append(" of ").Append(HouseOf(o.Seller)).Append(".\n\n");
                if (o.Kind == KSide && o.Payer != null && o.Payer != o.Head && !o.SellBack) sb.Append("Its tax is taken by ").Append(NameOf(o.Payer)).Append(" of the same house; ").Append(NameOf(o.Head)).Append(" sells it for the house, and ").Append(NameOf(o.Payer)).Append(" pays you its share.\n");
                if (!o.SellBack && o.NeedEn.Length > 0) sb.Append("Why ").Append(He(o.Head)).Append(" sells: ").Append(o.NeedEn).Append('\n');
                string dues = o.DuesRate > 0 ? ", less the crown's dues of " + (100 * o.DuesRate).ToString("0.#", Inv) + "%" : "";
                if (o.Kind == KCastle)
                    sb.Append("Income: about ").Append(o.NetDay.ToString("N0", Inv)).Append(" a day - the castle's own dues ").Append(o.CastleLr.ToString("N0", Inv))
                      .Append(", rents of its villages ").Append(o.VilRent.ToString("N0", Inv)).Append(", taxes of the villages its lord holds (").Append(o.VilSeller).Append(") ").Append(o.VilVt.ToString("N0", Inv))
                      .Append(", less the wages of its guard ").Append(o.Guard.ToString("N0", Inv)).Append(" (").Append((s.LandCastleGuardShare * o.GuardMen).ToString("0", Inv)).Append(" men at ")
                      .Append(o.GuardWage.ToString("0.#", Inv)).Append(")").Append(o.OwnDues > 0 ? ", less what it pays your own villages there " + o.OwnDues.ToString("N0", Inv) : "").Append(dues)
                      .Append(". The crown's rent is not counted - it is not the land's.\n");
                else if (o.Kind == KSide)
                    sb.Append("Income today: about ").Append(o.NetDay.ToString("N0", Inv)).Append(" a day (its share of the district's tax)")
                      .Append(o.SeasonFix != 1 ? ", set to the year's mean from " + o.Days + " days of the district's tax (x" + o.SeasonFix.ToString("0.00", Inv) + ")" : "").Append(".\n");
                else
                    sb.Append("Income: about ").Append(o.NetDay.ToString("N0", Inv)).Append(" a day (the tax of the village").Append(o.OwnDues > 0 ? ", less what it pays your own villages there" : "").Append(dues).Append(").\n");
                if (o.Kind != KSide) sb.Append("Measured over ").Append(o.Days).Append(o.Days == 1 ? " day" : " days").Append(".\n");
                if (o.Kind != KSide && o.OwnDues > 0 && !o.SellBack)
                    sb.Append("Your side villages here (about ").Append(o.OwnDues.ToString("N0", Inv)).Append(" a day) stop paying you - their share is part of the tax you buy; the price counts only the rest.\n");
                sb.Append("Years of purchase: ").Append(o.Years.ToString("0.##", Inv)).Append(o.WarF < 1 ? " (its realm is at war - land sells for less)" : "").Append('\n');
                if (o.Premium > 1) sb.Append(o.Seat ? "Family seat: x" : "Castle: x").Append(o.Premium.ToString("0.##", Inv)).Append(" - lords hardly ever sell ").Append(o.Seat ? "the seat of their house" : "a fortress").Append(".\n");
                sb.Append(o.Disc > 0 ? "Discount: " + (100 * o.Disc).ToString("0.#", Inv) + "% - " + o.DiscWhy + "." : "No discount - " + o.DiscWhy + ".").Append('\n');
                if (o.SellBack) sb.Append("Price: ").Append(o.Price.ToString("N0", Inv)).Append(". You give up about ").Append(o.NetDay.ToString("N0", Inv)).Append(" a day.\n");
                else sb.Append("Price: ").Append(o.Price.ToString("N0", Inv)).Append(". It pays itself back in about ").Append(Payback(o).ToString("0.0", Inv)).Append(" years.\n");
                if (o.SellBack)
                {
                    long ow = DebtLadder.TakesLandProceeds(Clan.PlayerClan) ? DebtLadder.OwedOf(Clan.PlayerClan) : 0;   // recenzja m113: tylko gdy drabina bierze z ceny
                    sb.Append("Where the money goes: to you").Append(ow > 0 ? "; your creditors take " + Math.Min(o.Price, ow).ToString("N0", Inv) + " of it first (you owe " + ow.ToString("N0", Inv) + ")" : "").Append(".\n");
                }
                else
                {
                    bool credTake = o.Owed > 0 && DebtLadder.TakesLandProceeds(o.Seller);
                    sb.Append("Where the money goes: to ").Append(NameOf(o.Head)).Append(credTake ? "; " + His(o.Head) + " creditors take " + Math.Min(o.Price, o.Owed).ToString("N0", Inv) + " of it first (" + His(o.Head) + " house owes " + o.Owed.ToString("N0", Inv) + ")" : "").Append(".\n");
                    if (o.Kind == KSide) sb.Append("The village flies your banner and pays you its share of the tax every day, out of the district's tax and its lord's purse.\n");
                    if (o.Kind == KLord) sb.Append("The village stays bound to its castle: its rent and its people stay with the castle lord - you get its tax.\n");
                    if (o.Kind == KCastle)
                    {
                        float a, lim;
                        if (LandBk.DemesneAfter(Hero.MainHero, o.St, out a, out lim)) sb.Append("Your demesne after the purchase: ").Append(a.ToString("0.#", Inv)).Append(" of ").Append(lim.ToString("0.#", Inv)).Append(a > lim ? " - over your limit." : ".").Append('\n');
                        sb.Append("Its garrison of ").Append(o.Garrison).Append(" men stays and is now yours to pay - the price counts only the ").Append((s.LandCastleGuardShare * o.GuardMen).ToString("0", Inv))
                          .Append(" men the realm asks a castle to keep; more is your choice. If you leave the realm in peace, the castle goes back to the king.\n");
                    }
                }
                bool can = o.SellBack || (me != null && me.Gold >= o.Price);
                sb.Append(o.SellBack ? "" : "Your gold: " + (me != null ? me.Gold.ToString("N0", Inv) : "0") + (can ? "." : " - not enough."));
                string title = o.Name + " - " + o.Price.ToString("N0", Inv);
                var off = o;
                InformationManager.ShowInquiry(new InquiryData(title, sb.ToString(), can, true, o.SellBack ? "Sell" : "Buy", "Not now",
                    delegate { try { Queue(off); } catch (Exception e) { Stumble("ShowDeal.Buy", e); } }, null), true);
            }
            catch (Exception e) { Stumble("ShowDeal", e); }
        }

        // ============================================================ menu
        /// <summary>OnAfterSessionLaunched: opcje menu (po opcjach gry i naszych - indeksy 5 i 8). Nie rzuca.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption(SideVillageVisit.Menu, OptSideBuy, "{=!}{ARM_LAND_BUY}", SideBuyCond, SideBuyCons, false, 5);
                starter.AddGameMenuOption("village", OptLord, "{=!}{ARM_LAND_LORD}", LordCond, LordCons, false, 8);
                starter.AddGameMenuOption(SideVillageFief.Menu, OptSellBack, "{=!}{ARM_LAND_BACK}", BackCond, BackCons, false, 6);
                Log.Info(P + "menu wpiete (" + SideVillageVisit.Menu + " indeks 5, village indeks 8, " + SideVillageFief.Menu + " indeks 6).");
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static Offer Cached(MenuC c, string key, Func<Offer> make)
        {
            int h = (int)CampaignTime.Now.ToHours;
            if (c.O == null || c.Hour != h || !string.Equals(c.Key, key, StringComparison.Ordinal)) { c.Hour = h; c.Key = key; c.O = make(); }
            return c.O;
        }

        private static bool SideBuyCond(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            try
            {
                var s = Settings.Current;
                if (!On || s == null || !s.LandSalesSideVillages) return false;
                string uid = SideVillageVisit.MenuUid;
                if (uid == null || SideVillageFief.IsMine(uid)) return false;
                var o = Cached(_mSide, uid, () => BuildSide(uid));
                if (o.Hide) return false;   // recenzja m113: powod, ktorego gracz nie zmieni - bez martwej opcji
                MBTextManager.SetTextVariable("ARM_LAND_BUY", "Offer to buy this village from " + NameOf(o.Head));
                if (!o.Ok) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + o.Why); }
                else a.Tooltip = new TextObject("{=!}" + o.Name + " earns about " + o.NetDay.ToString("N0", Inv) + " a day; " + NameOf(o.Head) + " would sell it for " + o.Price.ToString("N0", Inv) + ".");
                return true;
            }
            catch (Exception e) { Stumble("SideBuyCond", e); return false; }
        }

        private static void SideBuyCons(MenuCallbackArgs a)
        {
            try { _mSide.Hour = -1; string uid = SideVillageVisit.MenuUid; var o = uid != null ? BuildSide(uid) : null; if (o != null && o.Ok) { _dWindows++; ShowDeal(o); } }
            catch (Exception e) { Stumble("SideBuyCons", e); }
        }

        private static bool LordCond(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            try
            {
                var s = Settings.Current;
                if (!On || s == null || !s.LandSalesLordships || !LandBk.Ready) return false;
                var st = Settlement.CurrentSettlement;
                if (st == null || !st.IsVillage) return false;
                var o = Cached(_mLord, st.StringId, () => BuildLord(st.StringId));
                if (o.Hide) return false;   // recenzja m113: ustawienia, bez tytulu, bez BK, juz gracza - opcja ukryta (nie wyszarzona w kazdej wsi)
                MBTextManager.SetTextVariable("ARM_LAND_LORD", "Offer to buy the lordship of " + Txt(st.Name));
                if (!o.Ok) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + o.Why); }
                else a.Tooltip = new TextObject("{=!}Its tax brings about " + o.NetDay.ToString("N0", Inv) + " a day; " + NameOf(o.Head) + " would sell the title for " + o.Price.ToString("N0", Inv) + ". The village stays bound to its castle.");
                return true;
            }
            catch (Exception e) { Stumble("LordCond", e); return false; }
        }

        private static void LordCons(MenuCallbackArgs a)
        {
            try { _mLord.Hour = -1; var st = Settlement.CurrentSettlement; var o = st != null ? BuildLord(st.StringId) : null; if (o != null && o.Ok) { _dWindows++; ShowDeal(o); } }
            catch (Exception e) { Stumble("LordCons", e); }
        }

        private static bool BackCond(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            try
            {
                var s = Settings.Current;
                if (!On || s == null || !s.LandSalesSellBack || !SideVillageFief.HasFief) return false;
                string uid = SideVillageFief.MenuUid;
                if (uid == null) return false;
                var o = Cached(_mBack, uid, () => BuildSellBack(uid));
                MBTextManager.SetTextVariable("ARM_LAND_BACK", "Offer " + (SideVillageFief.MenuName ?? "this village") + " back to " + NameOf(o.Head));
                if (!o.Ok) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + o.Why); }
                else a.Tooltip = new TextObject("{=!}" + NameOf(o.Head) + " would buy it back for " + o.Price.ToString("N0", Inv) + ".");
                return true;
            }
            catch (Exception e) { Stumble("BackCond", e); return false; }
        }

        private static void BackCons(MenuCallbackArgs a)
        {
            try { _mBack.Hour = -1; string uid = SideVillageFief.MenuUid; var o = uid != null ? BuildSellBack(uid) : null; if (o != null && o.Ok) { _dWindows++; ShowDeal(o); } }
            catch (Exception e) { Stumble("BackCons", e); }
        }

        // ------------------------------------------------------------ recenzja m113: menu po umowie
        private static string CurMenuId()
        {
            try { var c = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null; return c != null && c.GameMenu != null ? c.GameMenu.StringId : null; }
            catch { return null; }
        }

        /// <summary>Po udanej umowie: wyceny menu od nowa (stara oferta z ceny sprzed umowy nie wisi do konca godziny).</summary>
        private static void DealDone() { _mSide.Hour = _mLord.Hour = _mBack.Hour = -1; _mSide.O = _mLord.O = _mBack.O = null; }

        /// <summary>Kupno wioski z menu tej wioski (obcej) - menu przechodzi na menu wioski gracza tej wioski. Nie rzuca.</summary>
        private static void AfterDealMenu(Offer o)
        {
            try
            {
                if (o == null || o.Kind != KSide) return;
                if (CurMenuId() == SideVillageVisit.Menu && string.Equals(SideVillageVisit.MenuUid, o.Id, StringComparison.Ordinal) && SideVillageFief.SetCurrent(o.Id))
                {
                    GameMenu.SwitchToMenu(SideVillageFief.Menu);
                    Log.Info(P + "menu po kupnie - " + o.Name + " (" + o.Id + "): menu wioski gracza.");
                }
            }
            catch (Exception e) { Stumble("AfterDealMenu", e); }
        }

        /// <summary>Odkup z menu wioski gracza - wyjscie z niego (inaczej menu pokazaloby inna wioske gracza). Nie rzuca.</summary>
        private static void AfterSellBackMenu(Offer o)
        {
            try
            {
                if (CurMenuId() != SideVillageFief.Menu) return;
                SideVillageFief.LeaveMenu();
                Log.Info(P + "menu po odkupie - " + (o != null ? o.Name : "?") + ": wyjscie z menu wioski gracza.");
            }
            catch (Exception e) { Stumble("AfterSellBackMenu", e); }
        }

        // ============================================================ autotest
        private static bool TestWanted()
        {
            if (_testDone) return false;
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
            }
            catch { return false; }
        }

        /// <summary>HourlyTick: autotest raz na sesje (flaga armoury-ziemia.flag), gdy ksiega wiosek czynna i ksiega wartosci ziemi ma co najmniej 1 dobe. Nie rzuca.</summary>
        internal static void Hourly()
        {
            try { if (!_testDone && TestWanted() && VillageBook.Active && LandValue.DaysMeasured >= 1) Test(); }
            catch (Exception e) { _testDone = true; Stumble("Hourly", e); }
        }

        private static void Test()
        {
            _testDone = true;
            var sb = new StringBuilder(1500);
            int pass = 0, fail = 0;
            Action<string, bool, string> add = (n, ok, t) => { sb.Append(" | ").Append(n).Append(' ').Append(ok ? "TAK" : "NIE").Append(" (").Append(t).Append(')'); if (ok) pass++; else fail++; };
            var me = Hero.MainHero;
            _forceNeed = true;
            try
            {
                DryRun();
                add("T1 linia na sucho", true, "wyzej");
                Log.Info(P + "autotest: POTRZEBA WYMUSZONA dla wszystkich rodow (tylko ten test).");
                // T2 najblizsza wioska na sprzedaz, na ktora gracza stac
                var recs = VillageBook.LayoutRecords();
                var pos = MobileParty.MainParty.GetPosition2D;
                Offer best = null; float bd = float.MaxValue; int built = 0, okN = 0; string firstWhy = null;
                if (recs != null)
                    foreach (var r in recs)
                    {
                        if (r == null || SideVillageFief.IsMine(r.Uid)) continue;
                        float d = pos.Distance(new Vec2(r.X, r.Y));
                        if (d > 80f) continue;
                        var o = BuildSide(r.Uid); built++;
                        if (!o.Ok) { if (firstWhy == null) firstWhy = o.Why; continue; }
                        okN++;
                        if (o.Price <= me.Gold && d < bd) { bd = d; best = o; }
                    }
                add("T2 oferty wiosek (80 jedn.)", okN > 0, "policzone " + built + ", na sprzedaz " + okN + (firstWhy != null ? ", pierwszy powod: " + firstWhy : "") + ", zloto gracza " + me.Gold);
                if (best != null)
                {
                    long w0 = WorldGold(), hg0 = best.Head.Gold, pg0 = me.Gold, adv0 = DebtLadder.AdvNothingNow;
                    long owed0 = DebtLadder.OwedOf(best.Seller);
                    long once0, real0; ClanIncomeBook.OnceToday(best.Seller, out once0, out real0);
                    int n0 = SideVillageFief.OwnedCount;
                    Execute(best);
                    long once1, real1; ClanIncomeBook.OnceToday(best.Seller, out once1, out real1);
                    long cred = owed0 - DebtLadder.OwedOf(best.Seller);
                    add("T3 kupno wioski", SideVillageFief.IsMine(best.Id) && SideVillageFief.OwnedCount == n0 + 1, best.Name + " za " + best.Price + ", wiosek " + n0 + " -> " + SideVillageFief.OwnedCount);
                    long advN = DebtLadder.AdvNothingNow - adv0;   // recenzja m113: splata zaliczki gry w nicosc (OBIEG-1) z ceny - jedyna dozwolona roznica
                    add("T4 pieniadz swiata bez zmian (poza zaliczka gry w nicosc)", WorldGold() - w0 == -advN, w0 + " -> " + WorldGold() + ", zaliczka w nicosc " + advN);
                    add("T5 kiesa sprzedawcy = cena - wierzyciele", best.Head.Gold - hg0 == best.Price - cred && pg0 - me.Gold == best.Price, "glowa +" + (best.Head.Gold - hg0) + ", wierzycielom " + cred + ", gracz -" + (pg0 - me.Gold));
                    add("T6 jednorazowe sprzedawcy bez ceny", real1 == real0 && once1 == once0, "jednorazowe " + once0 + " -> " + once1 + ", od platnikow " + real0 + " -> " + real1);
                    // T7 wymuszony blad przeniesienia - zwrot co do grosza
                    Offer b2 = null;
                    foreach (var r in recs)
                    {
                        if (r == null || SideVillageFief.IsMine(r.Uid) || pos.Distance(new Vec2(r.X, r.Y)) > 80f) continue;
                        var o = BuildSide(r.Uid);
                        if (o.Ok && o.Price <= me.Gold) { b2 = o; break; }
                    }
                    if (b2 != null)
                    {
                        long pgA = me.Gold, hgA = b2.Head.Gold; int nA = SideVillageFief.OwnedCount;
                        _forceFail = true;
                        Execute(b2);
                        add("T7 blad przeniesienia - zwrot", me.Gold == pgA && b2.Head.Gold == hgA && SideVillageFief.OwnedCount == nA, "gracz " + pgA + " -> " + me.Gold + ", sprzedawca " + hgA + " -> " + b2.Head.Gold);
                    }
                    else add("T7 blad przeniesienia - zwrot", false, "brak drugiej oferty na kieszen gracza");
                    _forceFail = false;
                    // T8 zapis tam i z powrotem (arm_side_fief2, arm_land113, arm_landval) - recenzja m113: odczyt na boku, stan zywy (pierscienie sesji, wioski) nietkniety
                    string e2 = SideVillageFief.Export2(), el = Export(), ev = LandValue.Export();
                    string f2 = SideVillageFief.RoundTrip2(e2), fl = RoundTrip(el), fv = LandValue.RoundTrip(ev);
                    add("T8 zapis tam i z powrotem", e2 == f2 && el == fl && ev == fv, "sf2 " + e2.GetHashCode().ToString("x") + "/" + f2.GetHashCode().ToString("x") + ", ls1 " + el.GetHashCode().ToString("x") + "/" + fl.GetHashCode().ToString("x")
                        + ", lv1 " + ev.Length + " znakow " + (ev == fv ? "rowne" : "ROZNE"));
                    // T9 odkup bez blokady 182 dob
                    _bypassResale = true;
                    var sbk = BuildSellBack(best.Id);
                    if (sbk.Ok)
                    {
                        long pgB = me.Gold, w1 = WorldGold();
                        ExecuteSellBack(sbk);
                        add("T9 odkup", !SideVillageFief.IsMine(best.Id) && me.Gold >= pgB && WorldGold() == w1, "cena " + sbk.Price + ", gracz " + pgB + " -> " + me.Gold);
                    }
                    else add("T9 odkup", false, sbk.Why);
                    _bypassResale = false;
                }
                // T10 Lordship (B2) i T11 zamek (B3) - pierwsza oferta na kieszen gracza
                if (LandBk.Ready && Settings.Current.LandSalesLordships)
                {
                    Offer lo = null; string lw = null;
                    foreach (var v in Village.All) { if (v == null || v.Settlement == null) continue; var o = BuildLord(v.Settlement.StringId); if (o.Ok && o.Price <= me.Gold) { lo = o; break; } if (lw == null && !o.Ok) lw = o.Why; }
                    if (lo != null)
                    {
                        var seller = lo.Head; Execute(lo);
                        bool mine = LandBk.DeJure(LandBk.Title(lo.St)) == me;
                        add("T10 Lordship", mine && LandBk.DeJureCount(seller) >= 1, lo.Name + " za " + lo.Price + ", de iure gracza " + (mine ? "tak" : "NIE") + ", sprzedawcy zostaje tytulow " + LandBk.DeJureCount(seller));
                    }
                    else add("T10 Lordship", true, "brak oferty na kieszen gracza (" + (lw ?? "-") + ") - pominiety");
                }
                if (Settings.Current.LandSalesCastles)
                {
                    Offer co = null; string cw = null;
                    foreach (var st in Settlement.All) { if (st == null || !st.IsCastle) continue; var o = BuildCastle(st.StringId); if (o.Ok && o.Price <= me.Gold) { co = o; break; } if (cw == null && !o.Ok) cw = o.Why; }
                    if (co != null)
                    {
                        var gp = co.St.Town.GarrisonParty; int g0 = gp != null ? gp.MemberRoster.TotalManCount : -1;
                        Execute(co);
                        gp = co.St.Town.GarrisonParty; int g1 = gp != null ? gp.MemberRoster.TotalManCount : -1;
                        add("T11 zamek", co.St.OwnerClan == Clan.PlayerClan && g0 == g1, co.Name + " za " + co.Price + ", zaloga " + g0 + " -> " + g1);
                    }
                    else add("T11 zamek", true, "brak oferty na kieszen gracza (" + (cw ?? "-") + ") - pominiety");
                }
            }
            catch (Exception e) { add("wyjatek", false, e.GetType().Name + ": " + e.Message); Stumble("Test", e); }
            finally { _forceNeed = false; _forceFail = false; _bypassResale = false; }
            Log.Info(P + "test (autotest, plik " + AutoFlagName + ") | wynik TAK " + pass + ", NIE " + fail + sb.ToString() + ".");
        }
    }
}
