using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
    ///    n36 cena wedlug stosunku: odpowiedz w rozmowie, podpowiedz wiersza listy i okno rachunku mowia, ile lat dochodu chce sprzedawca przy stosunku gracza
    ///    i do ilu zszedlby dla bliskiego przyjaciela (RelEn; odkup - ile placi i do ilu podniosl by przyjaciel).
    ///  - Menu: arm_sidevil_buy w menu wioski pobocznej ("Offer to buy this village from {holder}", indeks 6 = tuz przed Leave po poborze z indeksem 2, dodane po opcjach arm_sidevil), arm_land_lord w menu
    ///    wsi gry ("Offer to buy the lordship of {village}", indeks 8, B2), arm_side_fief_sellback w menu wioski gracza ("Offer {village} back to {holder}", Z7) -
    ///    wylaczone z powodem w podpowiedzi. Wycena w warunku raz na godzine gry na osade (warunek menu nie liczy ksiegi co klatke).
    ///  - Autotest (DLL autotestu CrashScribe, LogGate.Mode 1/2, plik armoury-ziemia.flag): raz na sesje - linia na sucho, potrzeba wymuszona (linia), kupno najblizszej
    ///    wioski, sprawdzenie pieniadza swiata, kiesy sprzedawcy i jednorazowych, wymuszony blad przeniesienia (zwrot co do grosza), odkup bez blokady 182 dob, zapis
    ///    tam i z powrotem trzech napisow, kupno wioski, ktora zostaje (T10, do zapisu); Lordship i zamek. Gdy oferty nie na kieszen gracza - dotacja testu
    ///    (linia "TEST dotacja testu ... (poza bilansem)"); krok, ktory nie mogl ruszyc, to NIEPOKRYTY, a linia wyniku konczy sie TAK / NIE / NIEPOKRYTY.
    ///    Test n36: T11/T12 - bramy, ktore gracz zmieni w grze (stosunek ze sprzedawca, wasal krolestwa sprzedawcy), test spelnia sam (linie "TEST warunek testu ..."),
    ///    potem kupno z kontrolami jak T3-T8; T12 "wyjatki przy zmianie pana" - ERROR in Armoury i wyjatki rzucone w czasie kupna (sluchacze innych modow).
    ///    T13 (n36 cena wedlug stosunku, zaraz po T2): krzywa lat YearsFor (prog, polowa, prog + rozpietosc, wyzej; odkup odwrotnie) i lata oferty z T2.
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
                int ok = 0; Offer firstOk = null; foreach (var o in l) if (o.Ok) { ok++; if (firstOk == null) firstOk = o; }
                string reply;
                if (!need) reply = "My lands are not for sale. A house sells land only when it must.";
                else reply = first.NeedEn + " " + (ok > 0 ? "I may part with some land - at its true worth, not for a song. My steward will show you what I would sell."
                                                          : "Yet there is nothing I could sell you now - my steward will tell you why.");
                // n36 cena wedlug stosunku (Jeff 24.10: "im wyzsza tym lepsza cena") - lata pierwszej oferty na sprzedaz slowami sprzedawcy
                if (need && firstOk != null)
                {
                    var s = Settings.Current;
                    string y = firstOk.YearsRel.ToString("0.#", Inv);
                    reply += firstOk.YearsRel <= Math.Max(1, s.LandYearsOfPurchase) + 1e-6
                        ? " You are a friend of my house - from you I ask only " + y + " years of its income."
                        : " Land goes cheaper between friends - from you I would ask " + y + " years of its income; a closer friend would pay less.";
                }
                MBTextManager.SetTextVariable("ARM_LAND_REPLY", reply);
                Log.Info(P + "rozmowa - " + NameOf(h) + " (" + (h != null && h.Clan != null ? h.Clan.StringId : "-") + "): ziem policzonych " + l.Count + ", na sprzedaz " + ok
                         + ", potrzeba " + (need ? first.NeedPl : "brak") + ", stosunek " + (h != null ? ((int)h.GetRelationWithPlayer()).ToString(Inv) : "-")
                         + (firstOk != null ? " (lata pierwszej oferty " + firstOk.YearsRel.ToString("0.##", Inv) + ")" : "") + ".");
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
        /// <summary>n36 cena wedlug stosunku: lata ze stosunku slowami gracza - "He asks 17.5 years of its income (your relation 20; a closer friend would ask less,
        /// down to 14)"; odkup - "He offers ... a closer friend would pay more, up to 20".</summary>
        internal static string RelEn(Offer o)
        {
            var s = Settings.Current;
            double best = Math.Max(1, s.LandYearsOfPurchase), worst = Math.Max(best, s.LandYearsWorst);
            int full = o.RelMin + Math.Max(1, s.LandYearsRelationSpan);
            string y = o.YearsRel.ToString("0.#", Inv), who = Cap(He(o.Head));
            if (o.SellBack)
                return who + " offers " + y + " years of its income (your relation " + o.Rel
                       + (o.YearsRel < worst - 1e-6 ? "; a closer friend would pay more, up to " + worst.ToString("0.#", Inv) + " at relation " + full + ")" : " - a close friend's price, the most " + He(o.Head) + " pays)");
            return who + " asks " + y + " years of its income (your relation " + o.Rel
                   + (o.YearsRel > best + 1e-6 ? "; a closer friend would ask less, down to " + best.ToString("0.#", Inv) + " at relation " + full + ")" : " - a close friend's price, the least " + He(o.Head) + " asks)");
        }

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
                    string hint = o.Ok ? "Income about " + o.NetDay.ToString("N0", Inv) + " a day; price " + o.Price.ToString("N0", Inv) + "; pays itself back in about " + Payback(o).ToString("0.0", Inv) + " years. " + RelEn(o) + "." : o.Why;
                    opts.Add(new InquiryElement(o, lab, null, o.Ok, hint));
                }
                if (opts.Count == 0) { InformationManager.ShowInquiry(new InquiryData(title, "There is no land to sell.", true, false, "Done", null, null, null), true); return; }
                var s0 = Settings.Current;
                double yb = Math.Max(1, s0.LandYearsOfPurchase), yw = Math.Max(yb, s0.LandYearsWorst);
                string body = "What the house would sell, at its true worth: what each holding earns its lord a year, net of its costs, times the years of purchase - "
                              + yw.ToString("0.#", Inv) + " years from a lord who only just thinks well enough of you to sell, down to " + yb.ToString("0.#", Inv) + " for a close friend. Your gold: "
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
                      .Append(o.SeasonFix != 1 ? ", set to the year's mean (x" + o.SeasonFix.ToString("0.00", Inv) + ")" : "").Append(dues).Append(".\n");
                else
                    sb.Append("Income: about ").Append(o.NetDay.ToString("N0", Inv)).Append(" a day (the tax of the village").Append(o.OwnDues > 0 ? ", less what it pays your own villages there" : "").Append(dues).Append(").\n");
                if (o.Kind != KSide) sb.Append("Measured over ").Append(o.Days).Append(o.Days == 1 ? " day" : " days").Append(".\n");
                if (o.Kind != KSide && o.OwnDues > 0 && !o.SellBack)
                    sb.Append("Your side villages here (about ").Append(o.OwnDues.ToString("N0", Inv)).Append(" a day) stop paying you - their share is part of the tax you buy; the price counts only the rest.\n");
                sb.Append(RelEn(o)).Append(".\n");
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
        /// <summary>OnAfterSessionLaunched: opcje menu (po opcjach gry i naszych - indeksy 6, 8 i 6). Nie rzuca.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption(SideVillageVisit.Menu, OptSideBuy, "{=!}{ARM_LAND_BUY}", SideBuyCond, SideBuyCons, false, 6);
                starter.AddGameMenuOption("village", OptLord, "{=!}{ARM_LAND_LORD}", LordCond, LordCons, false, 8);
                starter.AddGameMenuOption(SideVillageFief.Menu, OptSellBack, "{=!}{ARM_LAND_BACK}", BackCond, BackCons, false, 6);
                Log.Info(P + "menu wpiete (" + SideVillageVisit.Menu + " indeks 6 (przed Leave), village indeks 8, " + SideVillageFief.Menu + " indeks 6).");
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
                if (!TownInns.HostNearNow(SideVillageFief.MenuDistId, uid)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You are not at the village."); }   // n36: odkup tylko na miejscu
                else if (!o.Ok) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + o.Why); }
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
            var sb = new StringBuilder(2000);
            int pass = 0, fail = 0, unc = 0;
            long granted = 0;
            void add(string n, bool ok, string t) { sb.Append(" | ").Append(n).Append(' ').Append(ok ? "TAK" : "NIE").Append(" (").Append(t).Append(')'); if (ok) pass++; else fail++; }
            // test n36: krok, ktory nie mogl ruszyc (brak oferty mimo dotacji, wylaczony w ustawieniach) - NIEPOKRYTY, nie TAK
            void skip(string n, string t) { sb.Append(" | ").Append(n).Append(" NIEPOKRYTY (").Append(t).Append(')'); unc++; }
            var me = Hero.MainHero;
            // test n36: dotacja testu - zloto gracza do ceny oferty + zapas (cena/10 + 1000), poza bilansem (zadna ksiega jej nie liczy, linia w logu);
            // kazda kontrola pieniadza swiata bierze stan PO dotacji, wiec dotacja w zadnej roznicy nie siedzi
            void grant(long price, string what)
            {
                long g = price + price / 10 + 1000 - me.Gold;
                if (g <= 0) return;
                g = Math.Min(g, (long)int.MaxValue - Math.Max(0, me.Gold));
                if (g <= 0) return;
                long before = me.Gold;
                me.ChangeHeroGold((int)g);
                granted += g;
                Log.Info(P + "TEST dotacja testu " + g + " denarow (poza bilansem) - " + what + ": cena " + price + " + zapas, zloto gracza " + before + " -> " + me.Gold + ".");
            }
            _forceNeed = true;
            try
            {
                DryRun();
                add("T1 linia na sucho", true, "wyzej");
                Log.Info(P + "autotest: POTRZEBA WYMUSZONA dla wszystkich rodow (tylko ten test).");
                // T2 wioski na sprzedaz w 80 jedn.
                var recs = VillageBook.LayoutRecords();
                var pos = MobileParty.MainParty.GetPosition2D;
                int built = 0, okN = 0; string firstWhy = null; Offer okT2 = null;
                if (recs != null)
                    foreach (var r in recs)
                    {
                        if (r == null || SideVillageFief.IsMine(r.Uid)) continue;
                        if (pos.Distance(new Vec2(r.X, r.Y)) > 80f) continue;
                        var o = BuildSide(r.Uid); built++;
                        if (!o.Ok) { if (firstWhy == null) firstWhy = o.Why; continue; }
                        okN++; if (okT2 == null) okT2 = o;
                    }
                add("T2 oferty wiosek (80 jedn.)", okN > 0, "policzone " + built + ", na sprzedaz " + okN + (firstWhy != null ? ", pierwszy powod: " + firstWhy : "") + ", zloto gracza " + me.Gold);
                // T13 (n36 cena wedlug stosunku): krzywa lat - przy progu najgorsze, przy progu + rozpietosc i wyzej najlepsze, w polowie srodek, odkup odwrotnie;
                // oferta z T2: stosunek = stosunek glowy dzis, lata oferty = YearsFor, lata ceny = max(dolna granica, lata x wojna) przed upustem
                {
                    var s = Settings.Current;
                    double yb = Math.Max(1, s.LandYearsOfPurchase), yw = Math.Max(yb, s.LandYearsWorst);
                    int g = s.LandRelationSideVillage, sp = Math.Max(1, s.LandYearsRelationSpan);
                    bool eq(double a, double b) { return Math.Abs(a - b) < 1e-9; }
                    double a0 = YearsFor(g, g, false), a1 = YearsFor(g + sp, g, false), a2 = YearsFor(g + sp + 50, g, false), ah = YearsFor(g + sp / 2, g, false), b0 = YearsFor(g, g, true), b1 = YearsFor(g + sp, g, true);
                    bool okC = eq(a0, yw) && eq(a1, yb) && eq(a2, yb) && eq(ah, yw - (yw - yb) * (sp / 2) / (double)sp) && eq(b0, yb) && eq(b1, yw);
                    string t13 = "kupno prog " + g + " -> " + a0.ToString("0.##", Inv) + ", +" + sp / 2 + " -> " + ah.ToString("0.##", Inv) + ", +" + sp + " -> " + a1.ToString("0.##", Inv) + ", +" + (sp + 50) + " -> " + a2.ToString("0.##", Inv)
                               + "; odkup prog -> " + b0.ToString("0.##", Inv) + ", +" + sp + " -> " + b1.ToString("0.##", Inv);
                    bool okO = true;
                    if (okT2 != null)
                    {
                        int relNow = okT2.Head != null ? (int)okT2.Head.GetRelationWithPlayer() : int.MinValue;
                        double yp = Math.Max(Math.Max(1, s.LandYearsFloor), okT2.YearsRel * okT2.WarF);
                        okO = okT2.Rel == relNow && okT2.RelMin == g && okT2.Rel >= g && eq(okT2.YearsRel, YearsFor(okT2.Rel, g, false)) && eq(okT2.Years, yp);
                        t13 += "; oferta " + okT2.Name + " (" + okT2.Id + "): stosunek " + okT2.Rel + " (glowa dzis " + relNow + "), lata " + okT2.YearsRel.ToString("0.##", Inv) + " x wojna " + okT2.WarF.ToString("0.##", Inv)
                             + " = " + okT2.Years.ToString("0.##", Inv) + (okO ? " ok" : " ZLE (oczekiwane " + yp.ToString("0.##", Inv) + ")");
                    }
                    else t13 += "; bez oferty z T2";
                    add("T13 lata wedlug stosunku", okC && okO, t13);
                }
                // test n36: najblizsza wioska na kieszen gracza; gdy zadnej - najtansza, a gracz dostaje dotacje testu (inaczej T3-T10 nie ruszaja)
                Offer pickSide(string what)
                {
                    Offer near = null, cheap = null; float nd = float.MaxValue;
                    if (recs == null) return null;
                    foreach (var r in recs)
                    {
                        if (r == null || SideVillageFief.IsMine(r.Uid)) continue;
                        float d = pos.Distance(new Vec2(r.X, r.Y));
                        if (d > 80f) continue;
                        var o = BuildSide(r.Uid);
                        if (!o.Ok) continue;
                        if (o.Price <= me.Gold && d < nd) { nd = d; near = o; }
                        if (cheap == null || o.Price < cheap.Price) cheap = o;
                    }
                    if (near != null) return near;
                    if (cheap != null) grant(cheap.Price, what + " " + cheap.Name + " (" + cheap.Id + ")");
                    return cheap;
                }
                Offer best = okN > 0 ? pickSide("T3 kupno") : null;
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
                    // T7 wymuszony blad przeniesienia - zwrot co do grosza (druga oferta; gdy nie na kieszen - dotacja testu)
                    Offer b2 = pickSide("T7 druga oferta");
                    if (b2 != null)
                    {
                        long pgA = me.Gold, hgA = b2.Head.Gold; int nA = SideVillageFief.OwnedCount;
                        _forceFail = true;
                        Execute(b2);
                        add("T7 blad przeniesienia - zwrot", me.Gold == pgA && b2.Head.Gold == hgA && SideVillageFief.OwnedCount == nA, "gracz " + pgA + " -> " + me.Gold + ", sprzedawca " + hgA + " -> " + b2.Head.Gold);
                    }
                    else skip("T7 blad przeniesienia - zwrot", "brak drugiej oferty wioski w 80 jedn.");
                    _forceFail = false;
                    // T8 zapis tam i z powrotem (arm_side_fief2, arm_land113, arm_landval) - recenzja m113: odczyt na boku, stan zywy (pierscienie sesji, wioski) nietkniety
                    string e2 = SideVillageFief.Export2(), el = Export(), ev = LandValue.Export();
                    string f2 = SideVillageFief.RoundTrip2(e2), fl = RoundTrip(el), fv = LandValue.RoundTrip(ev);
                    add("T8 zapis tam i z powrotem", e2 == f2 && el == fl && ev == fv, "sf2 " + e2.GetHashCode().ToString("x") + "/" + f2.GetHashCode().ToString("x") + ", ls1 " + el.GetHashCode().ToString("x") + "/" + fl.GetHashCode().ToString("x")
                        + ", lv1 " + ev.Length + " znakow " + (ev == fv ? "rowne" : "ROZNE"));
                    // T9 odkup bez blokady 182 dob; test n36: cena moze splacic wierzycieli gracza (DebtLadder.LandProceeds), a splata zaliczki gry idzie w nicosc - jak T4
                    _bypassResale = true;
                    var sbk = BuildSellBack(best.Id);
                    if (sbk.Ok)
                    {
                        long pgB = me.Gold, w1 = WorldGold(), advB = DebtLadder.AdvNothingNow, owB = DebtLadder.OwedOf(Clan.PlayerClan);
                        ExecuteSellBack(sbk);
                        long advN9 = DebtLadder.AdvNothingNow - advB, cred9 = owB - DebtLadder.OwedOf(Clan.PlayerClan);
                        add("T9 odkup", !SideVillageFief.IsMine(best.Id) && me.Gold >= pgB && WorldGold() - w1 == -advN9, "cena " + sbk.Price + ", gracz " + pgB + " -> " + me.Gold + " (wierzycielom gracza " + cred9 + ")"
                            + ", pieniadz swiata " + w1 + " -> " + WorldGold() + ", zaliczka w nicosc " + advN9);
                    }
                    else add("T9 odkup", false, sbk.Why);
                    _bypassResale = false;
                    // T10 (test n36): wioska kupiona w tescie ZOSTAJE u gracza - zapis koncowy niesie arm_side_fief2 i arm_land113 naprawde (wczyt w0 -> w1);
                    // kupiona bez wioski gracza zostaje pierwsza (sf1), wiec najwyzej dwa kupna, az bedzie dodatkowa (sf2)
                    string kept = "", why10 = null; bool ok10 = true; int buys = 0;
                    for (int i = 0; i < 2 && SideVillageFief.OwnedCount - (SideVillageFief.HasFief ? 1 : 0) < 1; i++)
                    {
                        var k = pickSide("T10 kupno do zapisu");
                        if (k == null) { why10 = "brak oferty wioski w 80 jedn."; break; }
                        long wK = WorldGold(), advK = DebtLadder.AdvNothingNow; int nK = SideVillageFief.OwnedCount;
                        Execute(k); buys++;
                        long advNK = DebtLadder.AdvNothingNow - advK;
                        bool okK = SideVillageFief.IsMine(k.Id) && SideVillageFief.OwnedCount == nK + 1 && WorldGold() - wK == -advNK;
                        kept += (kept.Length > 0 ? ", " : "") + k.Name + " (" + k.Id + ") za " + k.Price
                              + (okK ? "" : " ZLE: wiosek " + nK + " -> " + SideVillageFief.OwnedCount + ", pieniadz swiata " + wK + " -> " + WorldGold() + ", zaliczka w nicosc " + advNK);
                        if (!okK) { ok10 = false; break; }
                    }
                    int extra = SideVillageFief.OwnedCount - (SideVillageFief.HasFief ? 1 : 0);
                    string t10 = (kept.Length > 0 ? kept : "bez kupna - gracz ma juz dodatkowa wioske") + " | wiosek gracza " + SideVillageFief.OwnedCount + ", dodatkowych (sf2) " + extra + ", transakcji w zapisie " + _deals.Count;
                    if (!ok10) add("T10 kupno do zapisu (zostaje)", false, t10);
                    else if (extra < 1) skip("T10 kupno do zapisu (zostaje)", (why10 ?? "brak dodatkowej wioski") + " | " + t10);
                    else
                    {
                        add("T10 kupno do zapisu (zostaje)", true, t10);
                        Log.Info(P + "TEST T10: wioski kupione w tescie ZOSTAJA u gracza (zapis arm_side_fief2 i arm_land113 przy najblizszym zapisie) - " + t10 + ".");
                    }
                }
                else
                {
                    string w = okN == 0 ? "brak oferty wioski w 80 jedn." : "brak oferty mimo dotacji testu";
                    foreach (var n in new[] { "T3 kupno wioski", "T4 pieniadz swiata", "T5 kiesa sprzedawcy", "T6 jednorazowe sprzedawcy", "T7 blad przeniesienia - zwrot", "T8 zapis tam i z powrotem", "T9 odkup", "T10 kupno do zapisu (zostaje)" })
                        skip(n, w);
                }
                // T11 Lordship (B2) i T12 zamek (B3) - test n36: oferty budowane z _testLift - bramy, ktore gracz zmieni w grze (stosunek ze sprzedawca, wasal
                // krolestwa sprzedawcy; zloto - dotacja testu), nie zamykaja oferty; test je spelnia sam (linia "TEST warunek testu ..."), buduje oferte od nowa
                // i kupuje z kontrolami jak T3-T8. Bramy, ktorych gracz nie zmieni (brak tytulu BK, jedyna siedziba, odstep, ustawienia) - NIEPOKRYTY z powodem.
                var pc = Clan.PlayerClan;
                string lw = null, cw = null;
                List<Offer> lifted(bool castle)
                {
                    var l = new List<Offer>();
                    string first = null, firstShown = null;
                    _testLift = true;
                    try
                    {
                        if (castle) { foreach (var st in Settlement.All) { if (st == null || !st.IsCastle) continue; var o = BuildCastle(st.StringId); if (o.Ok) l.Add(o); else { if (first == null) first = o.Why; if (firstShown == null && !o.Hide) firstShown = o.Why; } } }
                        else foreach (var v in Village.All) { if (v == null || v.Settlement == null) continue; var o = BuildLord(v.Settlement.StringId); if (o.Ok) l.Add(o); else { if (first == null) first = o.Why; if (firstShown == null && !o.Hide) firstShown = o.Why; } }
                    }
                    finally { _testLift = false; }
                    if (castle) cw = firstShown ?? first; else lw = firstShown ?? first;
                    return l;
                }
                bool deepOf(Offer o) { return DeepDebt(o.Owed, o.D); }
                bool noLift(Offer o) { return o.TestRel == int.MinValue && o.TestJoin == null; }
                // recenzja n36: wasal krolestwa bierze jego wojny (MapFaction rodu = krolestwo) - krolestwa, z ktorymi k walczy, a strona gracza jeszcze nie
                List<string> warsOf(IFaction f)
                {
                    var l = new List<string>();
                    if (f != null) foreach (var kk in Kingdom.All) if (kk != null && kk != f && !kk.IsEliminated && f.IsAtWarWith(kk)) l.Add(kk.StringId);
                    return l;
                }
                int newWars(Kingdom k)
                {
                    if (k == null) return 0;
                    var mf = pc.MapFaction; int n = 0;
                    foreach (var kk in Kingdom.All) if (kk != null && kk != k && !kk.IsEliminated && k.IsAtWarWith(kk) && !(mf != null && mf != kk && mf.IsAtWarWith(kk))) n++;
                    return n;
                }
                // zamek: sprzedawca w glebokim dlugu, potem bez zmiany krolestwa gracza, potem najmniej nowych wojen z wasalstwa, potem najtanszy
                int castleCmp(Offer a, Offer b)
                {
                    int x = (deepOf(b) ? 1 : 0) - (deepOf(a) ? 1 : 0); if (x != 0) return x;
                    x = (a.TestJoin != null ? 1 : 0) - (b.TestJoin != null ? 1 : 0); if (x != 0) return x;
                    x = newWars(a.TestJoin).CompareTo(newWars(b.TestJoin)); if (x != 0) return x;
                    return a.Price.CompareTo(b.Price);
                }
                // zamek planowany przed T11: Lordship z warunkami testu bierze krolestwo sprzedawcy tego zamku - jedno wasalstwo na oba kroki;
                // recenzja n36: Lordship od rodu sprzedawcy tego zamku na koncu - odstep sprzedawcy (LandSellerCooldownDays) zamknalby zamek w T12
                Kingdom planK = null; Clan planSeller = null; string planId = null, planName = null;
                if (Settings.Current.LandSalesCastles)
                {
                    var cp = lifted(true); cp.Sort(castleCmp);
                    if (cp.Count > 0) { planK = cp[0].TestJoin ?? (cp[0].Seller != null ? cp[0].Seller.Kingdom : null); planSeller = cp[0].Seller; planId = cp[0].Id; planName = cp[0].Name; }
                }
                int joins = 0;
                // warunki testu: wasal krolestwa sprzedawcy (ChangeKingdomAction.ApplyByJoinToKingdom - droga gry, konczy sluzbe najemnika, stosunki z wojnami
                // krolestwa), potem stosunek ze sprzedawca dokladnie do progu (ChangeRelationAction; przyrost gry z mnoznikiem moze przeskoczyc - zmiana w dol bez
                // mnoznika cofa do progu); na koniec te same bramy co oferta (CommonGate, VassalGate) - nie przeszly = NIE
                bool lift(Offer o, string what, out string note)
                {
                    note = "";
                    if (o.TestJoin != null && (pc.Kingdom != o.TestJoin || pc.IsUnderMercenaryService))
                    {
                        string was = pc.Kingdom != null ? Txt(pc.Kingdom.Name) + " (" + pc.Kingdom.StringId + (pc.IsUnderMercenaryService ? ", najemnik" : ", wasal") + ")" : "bez krolestwa";
                        var war0 = warsOf(pc.MapFaction);
                        // recenzja n36: ApplyInternal ustawia krolestwo przed OnClanChangedKingdom - wyjatek sluchacza (BK, Diplomacy, inny mod) nie moze zgubic linii testu
                        string jex = null;
                        try { ChangeKingdomAction.ApplyByJoinToKingdom(pc, o.TestJoin, default(CampaignTime), false); }
                        catch (Exception e) { jex = e.GetType().Name; Stumble("Test(ApplyByJoinToKingdom)", e); }
                        finally
                        {
                            joins++;
                            string now = pc.Kingdom != null ? Txt(pc.Kingdom.Name) + " (" + pc.Kingdom.StringId + (pc.IsUnderMercenaryService ? ", najemnik" : ", wasal") + ")" : "bez krolestwa";
                            var war1 = warsOf(pc.MapFaction); var wn = new List<string>(); foreach (var x in war1) if (!war0.Contains(x)) wn.Add(x);
                            Log.Info(P + "TEST warunek testu - " + what + ": rod gracza wasalem krolestwa " + Txt(o.TestJoin.Name) + " (" + o.TestJoin.StringId + ") przez ChangeKingdomAction.ApplyByJoinToKingdom"
                                     + " - wczesniej " + was + ", teraz " + now + (jex == null && pc.Kingdom == o.TestJoin && !pc.IsUnderMercenaryService ? "" : " - NIE WYSZLO") + (jex != null ? " (wyjatek " + jex + ")" : "")
                                     + "; wojny strony gracza [" + string.Join(",", war0.ToArray()) + "] -> [" + string.Join(",", war1.ToArray()) + "], nowe z wasalstwa " + (wn.Count > 0 ? string.Join(",", wn.ToArray()) : "brak") + " (tylko ten test).");
                        }
                        if (jex != null) { note = "wyjatek ApplyByJoinToKingdom " + jex + " (wczesniej " + was + ")"; return false; }
                        note = "wasal " + o.TestJoin.StringId + " (wczesniej " + was + ")";
                    }
                    if (o.TestRel != int.MinValue && o.Head != null)
                    {
                        int r0 = (int)o.Head.GetRelationWithPlayer(), r = r0;
                        for (int i = 0; i < 5 && r < o.TestRel; i++) { ChangeRelationAction.ApplyPlayerRelation(o.Head, o.TestRel - r, false, false); r = (int)o.Head.GetRelationWithPlayer(); }
                        int up = r;
                        if (r > o.TestRel) { ChangeRelationAction.ApplyPlayerRelation(o.Head, o.TestRel - r, false, false); r = (int)o.Head.GetRelationWithPlayer(); }
                        Log.Info(P + "TEST warunek testu - " + what + ": stosunek gracza z " + NameOf(o.Head) + " " + r0 + " -> " + r + " (prog " + o.TestRel + ", ChangeRelationAction.ApplyPlayerRelation"
                                 + (up != r ? ", przyrost gry do " + up + " cofniety do progu" : "") + ") (tylko ten test).");
                        note += (note.Length > 0 ? ", " : "") + "stosunek " + r0 + " -> " + r + " (prog " + o.TestRel + ")";
                    }
                    int relMin = o.Kind == KLord ? Settings.Current.LandRelationLordship : Settings.Current.LandRelationCastle;
                    string g = CommonGate(o.Head, relMin) ?? VassalGate(o.Seller, o.St != null ? o.St.MapFaction : null);
                    if (g != null) { note = "warunek testu nie spelniony: " + g + (note.Length > 0 ? " (po zmianach: " + note + ")" : ""); return false; }
                    if (note.Length > 0) note = "warunki testu: " + note;
                    return true;
                }
                // kupno z kontrolami T4-T6 (pieniadz swiata, kiesa sprzedawcy, jednorazowe sprzedawcy) i T8 (arm_land113 tam i z powrotem, transakcja w zapisie);
                // wyjatki w czasie kupna: ERROR in Armoury (Log.Errors), potkniecia ziemi i wyjatki rzucone na tym watku (FirstChanceException - takze zlapane
                // dalej, np. w sluchaczach zmiany pana innych modow): typ, miejsce rzutu i pierwsza ramka spoza mscorlib/TaleWorlds/Armoury
                bool buyChecked(Offer o, out string t, out string ex, out bool exOk)
                {
                    var head = o.Head; var seller = o.Seller;
                    long w0 = WorldGold(), hg0 = head.Gold, pg0 = me.Gold, adv0 = DebtLadder.AdvNothingNow, owed0 = DebtLadder.OwedOf(seller);
                    long once0, real0; ClanIncomeBook.OnceToday(seller, out once0, out real0);
                    int deals0 = _deals.Count, err0 = Log.Errors, st0 = _stAll, fcN = 0;
                    var fc = new List<string>(); bool inFc = false;
                    int tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> fh = (s, a) =>
                    {
                        if (inFc || System.Threading.Thread.CurrentThread.ManagedThreadId != tid) return;
                        inFc = true;
                        try
                        {
                            fcN++;
                            if (fc.Count < 4)
                            {
                                var e = a.Exception; var m = e != null ? e.TargetSite : null;
                                string at = m != null && m.DeclaringType != null ? m.DeclaringType.Assembly.GetName().Name + "!" + m.DeclaringType.Name + "." + m.Name : "?";
                                string by = "";
                                var fr = new StackTrace(1, false).GetFrames();
                                if (fr != null)
                                    foreach (var f in fr)
                                    {
                                        var fm = f != null ? f.GetMethod() : null;
                                        var dt = fm != null ? fm.DeclaringType : null;
                                        if (dt == null) continue;
                                        string an = dt.Assembly.GetName().Name;
                                        if (an == "mscorlib" || an.StartsWith("System", StringComparison.Ordinal) || an.StartsWith("TaleWorlds", StringComparison.Ordinal) || an == "Armoury" || an.StartsWith("0Harmony", StringComparison.Ordinal)) continue;
                                        by = ", z " + an + "!" + dt.Name + "." + fm.Name; break;
                                    }
                                fc.Add((e != null ? e.GetType().Name : "?") + " w " + at + by);
                            }
                        }
                        catch { }
                        finally { inFc = false; }
                    };
                    AppDomain.CurrentDomain.FirstChanceException += fh;
                    try { Execute(o); }
                    finally { AppDomain.CurrentDomain.FirstChanceException -= fh; }
                    long once1, real1; ClanIncomeBook.OnceToday(seller, out once1, out real1);
                    long cred = owed0 - DebtLadder.OwedOf(seller), advN = DebtLadder.AdvNothingNow - adv0;
                    bool okW = WorldGold() - w0 == -advN;
                    bool okP = head.Gold - hg0 == o.Price - cred && pg0 - me.Gold == o.Price;
                    bool okO = once1 == once0 && real1 == real0;
                    string el = Export(), fl = RoundTrip(el);
                    bool okS = el == fl && _deals.Count == Math.Min(200, deals0 + 1);
                    t = "pieniadz swiata " + w0 + " -> " + WorldGold() + " (zaliczka w nicosc " + advN + ") " + (okW ? "ok" : "ZLE")
                      + "; glowa sprzedawcy +" + (hg0 <= head.Gold ? "" : "-") + Math.Abs(head.Gold - hg0) + ", wierzycielom " + cred + ", gracz -" + (pg0 - me.Gold) + " " + (okP ? "ok" : "ZLE")
                      + "; jednorazowe sprzedawcy " + once0 + " -> " + once1 + ", od platnikow " + real0 + " -> " + real1 + " " + (okO ? "ok" : "ZLE")
                      + "; arm_land113 tam i z powrotem " + (el == fl ? "rowne" : "ROZNE") + ", transakcji " + deals0 + " -> " + _deals.Count + " " + (okS ? "ok" : "ZLE");
                    int errN = Log.Errors - err0, stN = _stAll - st0;
                    exOk = errN == 0 && stN == 0;
                    ex = "ERROR in Armoury +" + errN + ", potkniecia ziemi +" + stN + ", wyjatki rzucone w czasie kupna " + fcN + (fc.Count > 0 ? " (" + string.Join("; ", fc.ToArray()) + ")" : "");
                    return okW && okP && okO && okS;
                }
                if (!Settings.Current.LandSalesLordships) skip("T11 Lordship", "wylaczone w ustawieniach");
                else if (!LandBk.Ready) skip("T11 Lordship", "ksiega tytulow BK nieczynna");
                else
                {
                    var ll = lifted(false);
                    // Lordship: nie od rodu sprzedawcy planowanego zamku (odstep), bez warunkow testu, potem w krolestwie planowanego zamku (albo bez zmiany
                    // krolestwa), potem najmniej nowych wojen z wasalstwa, potem najtansza
                    ll.Sort((a, b) =>
                    {
                        int x = (planSeller != null && a.Seller == planSeller ? 1 : 0) - (planSeller != null && b.Seller == planSeller ? 1 : 0); if (x != 0) return x;
                        x = (noLift(a) ? 0 : 1) - (noLift(b) ? 0 : 1); if (x != 0) return x;
                        x = (a.TestJoin == null || a.TestJoin == planK ? 0 : 1) - (b.TestJoin == null || b.TestJoin == planK ? 0 : 1); if (x != 0) return x;
                        x = newWars(a.TestJoin).CompareTo(newWars(b.TestJoin)); if (x != 0) return x;
                        return a.Price.CompareTo(b.Price);
                    });
                    if (ll.Count == 0) skip("T11 Lordship", "brak oferty (" + (lw ?? "-") + ")");
                    else
                    {
                        var p = ll[0]; string note;
                        if (!lift(p, "T11 Lordship " + p.Name + " (" + p.Id + ")", out note)) add("T11 Lordship", false, p.Name + " (" + p.Id + "): " + note);
                        else
                        {
                            var lo = BuildLord(p.Id);
                            if (!lo.Ok) skip("T11 Lordship", "po warunkach testu brak oferty " + p.Name + " (" + lo.Why + ")" + (note.Length > 0 ? "; " + note : ""));
                            else
                            {
                                grant(lo.Price, "T11 Lordship " + lo.Name + " (" + lo.Id + ")");
                                var seller = lo.Head; string t, ex; bool exOk;
                                bool okM = buyChecked(lo, out t, out ex, out exOk);
                                bool mine = LandBk.DeJure(LandBk.Title(lo.St)) == me;
                                int left = LandBk.DeJureCount(seller);
                                add("T11 Lordship", mine && left >= 1 && okM, lo.Name + " (" + lo.Id + ") za " + lo.Price + " od " + NameOf(seller) + ", de iure gracza " + (mine ? "tak" : "NIE") + ", sprzedawcy zostaje tytulow " + left
                                    + "; " + t + "; " + ex + (note.Length > 0 ? "; " + note : ""));
                            }
                        }
                    }
                }
                if (!Settings.Current.LandSalesCastles) skip("T12 zamek", "wylaczone w ustawieniach");
                else
                {
                    var cl = lifted(true); cl.Sort(castleCmp);   // od nowa po T11 (wasalstwo z T11, zloto, odstepy)
                    // recenzja n36: inny zamek niz planowany przed T11 - powod z oferty planowanego zbudowanej teraz (z warunkami testu)
                    string planNote(Offer got)
                    {
                        if (planId == null || (got != null && got.Id == planId)) return "";
                        string why;
                        _testLift = true;
                        try { var po = BuildCastle(planId); why = po.Ok ? "oferta jest, ale nizej w kolejnosci" : po.Why; }
                        finally { _testLift = false; }
                        return "; planowany zamek " + planName + " (" + planId + ") nie kupiony: " + why;
                    }
                    if (cl.Count == 0) skip("T12 zamek", "brak oferty (" + (cw ?? "-") + ")" + planNote(null));
                    else
                    {
                        var p = cl[0]; string note; bool deep = deepOf(p);
                        if (!lift(p, "T12 zamek " + p.Name + " (" + p.Id + ")", out note)) add("T12 zamek", false, p.Name + " (" + p.Id + "): " + note);
                        else
                        {
                            var co = BuildCastle(p.Id);
                            if (!co.Ok) skip("T12 zamek", "po warunkach testu brak oferty " + p.Name + " (" + co.Why + ")" + (note.Length > 0 ? "; " + note : ""));
                            else
                            {
                                grant(co.Price, "T12 zamek " + co.Name + " (" + co.Id + ")");
                                var st = co.St; var head = co.Head; var seller = co.Seller;
                                var gp = st.Town.GarrisonParty; int g0 = gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalManCount : -1;
                                // tytuly BK sprzedawcy: zamek i wsie zamku, ktorych de iure jest sprzedawca - wszystkie maja przejsc do gracza (Transfer: AddBarter albo InheritTitle)
                                var tl = new List<object>();
                                if (LandBk.Ready)
                                {
                                    var ct = LandBk.Title(st); if (ct != null && LandBk.DeJure(ct) == head) tl.Add(ct);
                                    foreach (var bv in st.BoundVillages) { if (bv == null || bv.Settlement == null) continue; var vt = LandBk.Title(bv.Settlement); if (vt != null && LandBk.DeJure(vt) == head) tl.Add(vt); }
                                }
                                string home0 = seller.HomeSettlement != null ? seller.HomeSettlement.StringId : "-", ph0 = pc.HomeSettlement != null ? pc.HomeSettlement.StringId : "-";
                                string t, ex; bool exOk;
                                bool okM = buyChecked(co, out t, out ex, out exOk);
                                gp = st.Town.GarrisonParty; int g1 = gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalManCount : -1;
                                int tk = 0; foreach (var x in tl) if (LandBk.DeJure(x) == me) tk++;
                                string home1 = seller.HomeSettlement != null ? seller.HomeSettlement.StringId : "-", ph1 = pc.HomeSettlement != null ? pc.HomeSettlement.StringId : "-";
                                bool own = st.OwnerClan == pc, okT = own && g0 == g1 && tk == tl.Count && seller.HomeSettlement != st;
                                add("T12 zamek", okT && okM, co.Name + " (" + co.Id + ") za " + co.Price + " od " + NameOf(head) + (deep ? " (gleboki dlug)" : " (bez glebokiego dlugu)") + ", pan " + (own ? "gracz" : "NIE gracz")
                                    + ", zaloga " + g0 + " -> " + g1 + ", tytuly BK sprzedawcy u gracza " + tk + "/" + tl.Count + ", HomeSettlement sprzedawcy " + home0 + " -> " + home1 + (seller.HomeSettlement == st ? " ZLE" : "")
                                    + ", gracza " + ph0 + " -> " + ph1 + "; " + t + (note.Length > 0 ? "; " + note : "") + planNote(co));
                                // recenzja n36: bez zmiany pana (Execute wyszedl przed Transfer) wyjatki przy zmianie pana nie byly sprawdzone
                                if (!own) skip("T12 wyjatki przy zmianie pana", "kupno nie doszlo do zmiany pana; " + ex);
                                else add("T12 wyjatki przy zmianie pana", exOk, ex);
                            }
                        }
                    }
                }
                if (joins > 0) Log.Info(P + "TEST warunek testu: zmian krolestwa gracza " + joins + " - rod gracza zostaje w " + (pc.Kingdom != null ? Txt(pc.Kingdom.Name) + " (" + pc.Kingdom.StringId + ")" : "bez krolestwa") + " do konca biegu.");
            }
            catch (Exception e) { add("wyjatek", false, e.GetType().Name + ": " + e.Message); Stumble("Test", e); }
            finally { _forceNeed = false; _forceFail = false; _bypassResale = false; }
            string verdict = fail > 0 ? "NIE" : unc > 0 ? "NIEPOKRYTY" : "TAK";
            Log.Info(P + "test (autotest, plik " + AutoFlagName + ") | wynik TAK " + pass + ", NIE " + fail + ", NIEPOKRYTY " + unc + " = " + verdict
                     + " | dotacja testu " + granted + " denarow (poza bilansem)" + sb.ToString() + ".");
        }
    }
}
