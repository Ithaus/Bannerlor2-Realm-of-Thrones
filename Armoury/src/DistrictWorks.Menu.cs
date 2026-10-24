using System;
using System.Collections.Generic;
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
    /// inwest - PRACE OKREGU: MENU GRACZA (projekt ui_text 1-3). Pozycja "Invest in the district's works" w menu "village" (za "Look over the district"), w "village_looted"
    /// (tylko podglad i odbudowa) i w menu BEE wsi przed "Back" (BeeSeal.SessionPost -> OnBeeSession; opcje BEE 5/15/30 tys. ukryte przez BeeSeal przy tym kluczu).
    /// Okno 1 (wybor czesci okregu, odbudowa, przelacznik odbudowy samoczynnej) -> okno 2 (kwoty: 1 000, 2 500, 5 000, cale zloto, "jeszcze 12% na ostatniej monecie",
    /// pelny sprzet) - zwykle menu, nie menu oczekiwania (bez SwitchToMenu - CLAUDE.md 7); zloto sprawdzane od nowa przy zaplacie (DistrictWorks.Buy).
    /// Wszystkie napisy po angielsku. Bledy liczone (potkniecia), nic sie nie gasi.
    /// </summary>
    internal static partial class DistrictWorks
    {
        internal const string OptVillage = "arm_works_village", OptLooted = "arm_works_village_looted", OptBee = "arm_works_bee";
        private const string Tip = "Pay the villagers of one village in this district for boats, herds, ploughs or tools. That village makes more of its own goods, and half of what the extra sells for is yours. Raiders burn the works in the homesteads they empty.";
        private static bool _menuWired; private static int _beeAdded;
        private static int _mOpen, _mBuy;
        private static bool _autoOff;                    // gracz wylaczyl odbudowe samoczynna w oknie (zapis: sekcja a: arm_works)

        private static void MenuReset() { _menuWired = false; _beeAdded = 0; _mOpen = _mBuy = 0; _autoOff = false; }

        private static string MenuText()
        {
            string where = _menuWired ? "village i village_looted" : "NIEWPIETE";
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                var menu = mgr != null ? mgr.GetGameMenu("village") : null;
                if (menu != null)
                {
                    int i = 0, at = -1; foreach (var o in menu.MenuOptions) { if (o != null && o.IdString == OptVillage) at = i; i++; }
                    var mv = at < 0 ? VillageMenus.MovedTo(OptVillage) : null;   // m96-wies: pozycja przeniesiona do "Village affairs"
                    where += mv != null ? ", village -> " + mv : ", village index " + at + " z " + i;
                }
            }
            catch { }
            return where + ", okien " + _mOpen + ", zakupow z menu " + _mBuy;
        }

        private static string BeeText()
        {
            var s = Settings.Current;
            return "opcja w bee_village_menu " + (_beeAdded > 0 ? "TAK" : "NIE (BEE nieobecny albo menu bez Back)") + ", inwestycje BEE 5/15/30 tys. " + (On ? "ukryte (takze bez Living Economy Sealed)" : (s != null && s.LivingEconomySealed ? "jak dotad (zamkniete 170)" : "jak dotad (otwarte - Living Economy Sealed wylaczony)"));
        }

        /// <summary>BeeSeal: przy kluczu District Works opcje BEE "Invest 5 000 / 15 000 / 30 000" sa ukryte (zastepuje je ta pozycja).</summary>
        internal static bool HidesBeeInvest { get { return On; } }

        /// <summary>BeeSeal (CanPlayerInvest wsi - ekran Demesne BK i konsola): tekst zamkniecia przy kluczu.</summary>
        internal const string BeeClosedText = "Closed: BetterEconomy would burn this gold and make peasants out of nothing. Use 'Invest in the district's works' in the village menu.";

        /// <summary>ArmouryBehavior.OnSessionLaunched (za DistrictMenu.AddMenus): pozycje w menu wsi i wsi spladrowanej.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("village", OptVillage, "{=!}Invest in the district's works", Condition, Consequence, false, 8);
                starter.AddGameMenuOption("village_looted", OptLooted, "{=!}Invest in the district's works", ConditionLooted, Consequence, false, 2);
                _menuWired = true;
            }
            catch (Exception e) { _menuWired = false; Log.Error("DistrictWorks.AddMenus", e); }
        }

        /// <summary>BeeSeal.SessionPost (po BeeGifts.OnSession): pozycja w menu BEE wsi przed "Back" (raz).</summary>
        internal static void OnBeeSession(CampaignGameStarter starter, GameMenuManager mgr)
        {
            try
            {
                if (starter == null || mgr == null) return;
                var menu = mgr.GetGameMenu("bee_village_menu");
                if (menu == null) return;
                int idx = -1, i = 0;
                foreach (var o in menu.MenuOptions)
                {
                    if (o != null && o.IdString == OptBee) { _beeAdded++; return; }
                    if (o != null && o.IdString == "bee_village_back" && idx < 0) idx = i;
                    i++;
                }
                starter.AddGameMenuOption("bee_village_menu", OptBee, "{=!}Invest in the district's works", Condition, Consequence, false, idx);
                _beeAdded++;
            }
            catch (Exception e) { Stumble("OnBeeSession", e); }
        }

        private static bool Condition(MenuCallbackArgs args) { return Cond(args, false); }
        private static bool ConditionLooted(MenuCallbackArgs args) { return Cond(args, true); }

        private static bool Cond(MenuCallbackArgs args, bool looted)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var st = Settlement.CurrentSettlement;
                if (st == null || !st.IsVillage || st.Village == null || !VillageBook.Active) return false;
                var s = Settings.Current;
                if (s == null) return false;
                if (!On) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}District works are switched off (mod settings)."); return true; }   // recenzja: projekt ui_text 1 - widoczna nieaktywna z powodem
                string why;
                if (!CanTrade(Clan.PlayerClan, st.Village, true, out why)) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}" + why); return true; }
                if (!looted && st.Village.VillageState == Village.VillageStates.Looted) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}The village was plundered - wait until people return."); return true; }
                args.Tooltip = new TextObject("{=!}" + Tip);
                return true;
            }
            catch { return false; }
        }

        private static void Consequence(MenuCallbackArgs args) { OpenDistrict(Settlement.CurrentSettlement); }

        private sealed class Pick { public int Kind; public string Uid; public double Units; public int Amount; }   // 0 czesc, 1 odbudowa, 2 przelacznik odbudowy

        /// <summary>Okno 1: "Works in the X district" - czesci okregu (wiersz z ceny), odbudowy gracza, przelacznik odbudowy samoczynnej.</summary>
        private static void OpenDistrict(Settlement st)
        {
            try
            {
                if (st == null || st.Village == null) return;
                var v = st.Village;
                var qs = new List<Quote>();
                if (!QuotesOf(v, qs, Clan.PlayerClan)) { Log.Player("The villages of this district are not known yet.", true); return; }
                _mOpen++;
                var me = Hero.MainHero;
                string dname = Name(v);
                var sb = new StringBuilder(1200);
                double vd = qs.Count > 0 ? qs[0].VDist : 0, room = 0, sens = 0, ppl = 0, n = 0;
                foreach (var q in qs) { room += q.Room; sens += Math.Min(q.Room, q.Sensible); ppl += q.Home; n += q.N; }
                var town = MarketOf(v);
                sb.Append(dname).Append(" district: ").Append(qs.Count).Append(qs.Count == 1 ? " village, " : " villages, ").Append(Math.Round(n).ToString("N0", Inv)).Append(" homesteads, ")
                  .Append(Math.Round(ppl).ToString("N0", Inv)).Append(" people at home.\n");
                sb.Append("It makes about ").Append(Math.Round(vd).ToString("N0", Inv)).Append(" a day (").Append(Math.Round(vd * 364).ToString("N0", Inv)).Append(" a year) at ")
                  .Append(town != null && town.Name != null ? town.Name.ToString() : "market").Append(" prices: ").Append(DistrictIcon.MixText(v, qs)).Append(".\n");
                double winter = qs.Count > 0 ? qs[0].Winter : 0;
                sb.Append(winter > 0.05 ? "Winter now: it cuts what villages make by about " + Math.Round(100 * winter) + "%, and your income with it.\n" : "Winter cuts what villages make by about 40%, and your income with it.\n");
                sb.Append("Side villages toward the enemy burn first; the main village burns last.\n");
                sb.Append("Room for works: ").Append(Math.Round(room).ToString("N0", Inv)).Append(" (").Append(Math.Round(sens).ToString("N0", Inv)).Append(" of it still earns at least 12% a year).\n");
                string head = HeadText(st);
                if (head != null && head.IndexOf("Your works here", StringComparison.Ordinal) >= 0) sb.Append(head.Substring(head.IndexOf("Your works here", StringComparison.Ordinal))).Append('\n');
                else sb.Append("Your works here: none.\n");
                sb.Append("Your gold: ").Append(me != null ? me.Gold.ToString("N0", Inv) : "0").Append('.');
                var list = new List<InquiryElement>();
                foreach (var q in qs)
                {
                    var m = VillageCrafts.Meta(q.Craft);
                    string place = q.Main ? "main village" : VillageBook.PlaceOf(q.Kind).Trim();
                    double ret = q.WKind >= 0 ? (Settings.Current != null ? Settings.Current.DistrictWorksShare : 0.5f) * q.H * q.Bmax * 2 * (1 - Math.Min(1, q.S > 0 ? q.UEff / q.S : 1)) * (1 - q.Winter) : 0;
                    string title = (q.Name ?? "?") + (place.Length > 0 ? " (" + place + ")" : "") + " - " + (m != null ? m.Name : "?") + " - " + Math.Round(q.N) + " homesteads, "
                                   + Math.Round(100 * q.ShareDist).ToString("0", Inv) + "%";
                    if (q.Allowed) title += " - full kit " + Math.Round(q.KitPer * q.S).ToString("N0", Inv) + " - first coins about " + Math.Round(100 * ret).ToString("0", Inv) + "% a year" + (q.Main ? ", burns last" : "");
                    else title += " - " + q.Why;
                    // n20-napisy: na przycisku wioska, zajecie i zysk pierwszych monet (albo "closed"); reszta dawnego napisu na gorze podpowiedzi;
                    // recenzja: "up to" - to zysk krancowy pierwszych monet (najwyzszy), nie calej wplaty
                    string shortTitle = (q.Name ?? "?") + " - " + (m != null ? m.Name : "?") + (q.Allowed ? " - up to " + Math.Round(100 * ret).ToString("0", Inv) + "% a year" : " - closed");
                    string moved = (q.Main ? "Main village" : place.Length > 0 ? "Side village, " + place : "Side village") + ": " + Math.Round(q.N) + " homesteads, "
                                   + Math.Round(100 * q.ShareDist).ToString("0", Inv) + "% of the district."
                                   + (q.Allowed ? " Full kit " + Math.Round(q.KitPer * q.S).ToString("N0", Inv) + "; the first coins earn about " + Math.Round(100 * ret).ToString("0", Inv) + "% a year"
                                                  + (q.Main ? "; burns last." : ".") : "");
                    title = ShortLabels.Pick(shortTitle, title);
                    var hint = new StringBuilder();
                    hint.Append(q.Name).Append(q.Main ? ", the main village: " : ", a side village: ").Append(m != null ? m.GoodsText : "?").Append(". ")
                        .Append(Math.Round(q.S)).Append(" of ").Append(Math.Round(q.N)).Append(" homesteads standing, ").Append(Math.Round(q.Home).ToString("N0", Inv)).Append(" people. Makes ")
                        .Append(Math.Round(q.Pc != null ? q.Pc.ValDay : 0).ToString("N0", Inv)).Append(" a day (").Append(Math.Round(100 * q.ShareDist)).Append("% of the district).");
                    if (q.WKind >= 0) hint.Append(" Works: ").Append(KindName[q.WKind]).Append(", ready in ").Append(KReady[q.WKind]).Append(" days.");
                    hint.Append(" Works here now: ").Append(q.UEff > 0 ? Math.Round(100 * Math.Min(1, q.S > 0 ? q.UEff / q.S : 0)) + "% of a full kit (yours " + Math.Round(100 * Math.Min(1, q.S > 0 ? q.UMine / q.S : 0)) + "%)." : "none.");
                    if (!q.Allowed && q.Why != null) hint.Append("\n").Append(q.Why);
                    list.Add(new InquiryElement(new Pick { Kind = 0, Uid = q.Uid }, title, null, q.Allowed && v.VillageState != Village.VillageStates.Looted, ShortLabels.Hint(moved, hint.ToString())));
                }
                // odbudowy gracza (ui_text 2: "Rebuild 3 homesteads: 1,500")
                string pid = PlayerClanId();
                lock (Gate)
                    foreach (var r in _rows)
                    {
                        if (r.Dist != st.StringId || r.Clan != pid || r.Rebs.Count == 0) continue;
                        Quote q = null; foreach (var x in qs) if (PartKey(x.Uid) == r.Part) { q = x; break; }
                        if (q == null) continue;
                        double u = r.Rebuildable();
                        int cost = (int)Math.Ceiling(KR[r.Kind] * q.KitPer * u);
                        if (cost <= 0) continue;
                        int dl = int.MaxValue; foreach (var rb in r.Rebs) dl = Math.Min(dl, rb.Deadline);
                        bool ok = me != null && me.Gold >= cost;
                        // n20-napisy: termin odbudowy z napisu do podpowiedzi
                        list.Add(new InquiryElement(new Pick { Kind = 1, Uid = q.Uid, Units = u, Amount = cost },
                                                    ShortLabels.Pick("Rebuild " + KindShort[r.Kind] + " in " + q.Name + ": " + cost.ToString("N0", Inv),
                                                                     "Rebuild your " + KindShort[r.Kind] + " in " + q.Name + ": " + cost.ToString("N0", Inv) + " (by day " + dl + ")"),
                                                    null, ok, ShortLabels.Hint("Rebuild by day " + dl + ".", ok ? "People are back in " + Math.Round(u, 1) + " homesteads. Same split as buying: wages to the villagers, materials at the market." : "You have " + (me != null ? me.Gold : 0).ToString("N0", Inv) + ".")));
                    }
                bool auto = AutoRebuild();
                // n20-napisy: "click to switch ..." z napisu do podpowiedzi
                list.Add(new InquiryElement(new Pick { Kind = 2 }, ShortLabels.Pick("Automatic rebuilding: " + (auto ? "ON" : "OFF"), "Automatic rebuilding: " + (auto ? "ON - click to switch off" : "OFF - click to switch on")), null, true,
                                            ShortLabels.Hint(auto ? "Choose it to switch it off." : "Choose it to switch it on.",
                                                             "When people return to homesteads where your works burned, pay for them at once, as long as 30 days of your party's wages stay in your purse.")));
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Works in the " + dname + " district", sb.ToString(), list, true, 1, 1, "Choose", GameTexts.FindText("str_cancel").ToString(),
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            var p = sel[0].Identifier as Pick;
                            if (p == null) return;
                            if (p.Kind == 2) { _autoOff = !_autoOff; OpenDistrict(st); return; }
                            if (p.Kind == 1)
                            {
                                var res = Buy(Hero.MainHero, Clan.PlayerClan, st.Village, p.Uid, p.Amount, p.Units, false);
                                if (res.Ok) { lock (Gate) RemoveRebs(st.StringId, PartKey(p.Uid)); _mBuy++; Log.Player(res.Text); } else Log.Player(res.Why, true);
                                return;
                            }
                            OpenPart(st, p.Uid);
                        }
                        catch (Exception e) { Stumble("OknoOkregu", e); }
                    }, null), true);
            }
            catch (Exception e) { Stumble("OpenDistrict", e); }
        }

        private static void RemoveRebs(string dist, string part)
        {
            string pid = PlayerClanId();
            foreach (var r in _rows) if (r.Dist == dist && r.Part == part && r.Clan == pid) r.Rebs.Clear();
        }

        /// <summary>Odbudowa samoczynna gracza: klucz MCM i przelacznik okna.</summary>
        internal static bool AutoRebuild() { var s = Settings.Current; return (s == null || s.DistrictWorksAutoRebuild) && !_autoOff; }

        /// <summary>n20-wioska: menu wioski gracza (SideVillageFief) - okno 2 tej jednej wioski (te same warunki i zaplata co z okna okregu; czesc niedozwolona - komunikat z powodem).</summary>
        internal static void OpenPartFor(Settlement st, string uid) { if (st != null && st.Village != null && uid != null) OpenPart(st, uid); }

        /// <summary>Okno 2: "{wioska} - {rodzaj}" z kwotami (ui_text 3).</summary>
        private static void OpenPart(Settlement st, string uid)
        {
            try
            {
                var v = st.Village;
                var qs = new List<Quote>();
                if (!QuotesOf(v, qs, Clan.PlayerClan)) return;
                Quote q = null; foreach (var x in qs) if (x.Uid == uid) { q = x; break; }
                if (q == null || !q.Allowed || q.WKind < 0) { Log.Player(q != null && q.Why != null ? q.Why : "No works here.", true); return; }
                var me = Hero.MainHero;
                int gold = me != null ? Math.Max(0, me.Gold) : 0;
                var s = Settings.Current;
                int min = s != null ? Math.Max(0, s.DistrictWorksMinGold) : 200;
                var m = VillageCrafts.Meta(q.Craft);
                string craft = m != null ? m.Name : "goods";
                var town = MarketOf(v);
                string tname = town != null && town.Name != null ? town.Name.ToString() : "the market";
                double full = q.KitPer * q.S;
                var sb = new StringBuilder(1200);
                sb.Append(q.Name).Append(q.Main ? " (the main village)" : "").Append(" works at ").Append(craft).Append(": ").Append(m != null ? m.GoodsText : craft).Append(", about ")
                  .Append(Math.Round(q.V).ToString("N0", Inv)).Append(" a day (").Append(Math.Round(q.V * 364).ToString("N0", Inv)).Append(" a year).\n");
                sb.Append("A full kit for all ").Append(Math.Round(q.S)).Append(" standing homesteads costs ").Append(Math.Round(full).ToString("N0", Inv)).Append(", about ")
                  .Append((s != null ? s.DistrictWorksKitYears : 1f).ToString("0.#", Inv)).Append(" year of what they make. In place now: ").Append(Math.Round(100 * Math.Min(1, q.S > 0 ? q.UEff / q.S : 0))).Append("%.\n");
                sb.Append("Your money: ").Append(Math.Round(100 * (1 - KM[q.WKind]))).Append("% wages to the villagers of ").Append(Name(v)).Append(", ").Append(Math.Round(100 * KM[q.WKind]))
                  .Append(q.WKind == KHerd ? "% for animals bought at " : "% for timber, tools and other goods bought at ").Append(tname).Append(". Ready in ").Append(KReady[q.WKind]).Append(" days.\n");
                sb.Append(q.Name).Append(" then makes more, and half of what the extra sells for is yours, paid whenever ").Append(Name(v)).Append("'s carts come back from market. The lord and the village share the other half as usual. The first coins earn the most.\n");
                // m114-prace: wycena z tego samego wzoru co wyplata - towar okregu na targ i prawdziwy utarg wozow (bez 7 dob pomiaru - srednia swiata)
                if (q.ByMkt && q.MkDay > 0)
                    sb.Append(Name(v)).Append(" eats what it ate before - the extra all goes to market, where the district's goods for sale are worth about ").Append(Math.Round(q.MkDay).ToString("N0", Inv))
                      .Append(" a day").Append(q.HdDay > 0 ? " and its carts have lately brought home about " + Math.Round(q.HdDay).ToString("N0", Inv) + " a day" : "").Append(".\n");
                sb.Append("If raiders empty homesteads of ").Append(q.Name).Append(", ").Append(q.WKind == KHerd ? "they drive off your animals there - that part is lost; when people return you can buy them again." : "your works there burn" + (KL[q.WKind] < 1 ? " in part" : "") + "; when people return you can rebuild them for " + Math.Round(100 * KR[q.WKind]) + "% of the price.")
                  .Append(" At war with ").Append(st.MapFaction != null && st.MapFaction.Name != null ? st.MapFaction.Name.ToString() : "this realm").Append(" you are paid nothing. Your gold: ").Append(gold.ToString("N0", Inv)).Append('.');
                var amounts = new List<KeyValuePair<int, string>>
                {
                    new KeyValuePair<int, string>(1000, ""), new KeyValuePair<int, string>(2500, ""), new KeyValuePair<int, string>(5000, ""),
                    new KeyValuePair<int, string>(gold, " (all you have)"),
                    new KeyValuePair<int, string>((int)Math.Floor(Math.Min(q.Room, q.Sensible)), " (still 12% on the last coin)"),
                    new KeyValuePair<int, string>((int)Math.Floor(q.Room), " (full kit)")
                };
                var list = new List<InquiryElement>();
                var seen = new HashSet<int>();
                foreach (var a in amounts)
                {
                    int k = a.Key;
                    if (k < 1 || !seen.Add(k)) continue;
                    double units = k / Math.Max(1e-6, q.KitPer);
                    double inc = IncomeDay(q, q.UMine + units, q.UEff + units) - IncomeDay(q, q.UMine, q.UEff);
                    double x1 = q.S > 0 ? Math.Min(1, (q.UEff + units) / q.S) : 0, b1 = q.Bmax * (2 * x1 - x1 * x1);
                    double myShare = (q.UEff + units) > 0 ? (s != null ? s.DistrictWorksShare : 0.5f) * b1 / (1 + b1) * (q.UMine + units) / (q.UEff + units) : 0;
                    string text = k.ToString("N0", Inv) + a.Value + " - +" + (100 * b1).ToString("0.0", Inv) + "% " + craft + " - about " + inc.ToString("0.0", Inv) + " a day (" + Math.Round(inc * 364).ToString("N0", Inv)
                                  + " a year, " + Math.Round(100 * inc * 364 / Math.Max(1, k)) + "%) - " + (100 * myShare).ToString("0.0", Inv) + "% of its " + craft + " yours";
                    string why = null;
                    if (k > gold) why = "You have " + gold.ToString("N0", Inv);
                    else if (k > q.Room + 0.5) why = "There is room for " + Math.Floor(q.Room).ToString("N0", Inv) + " more";
                    else if (k < min) why = "At least " + min.ToString("N0", Inv);
                    // n20-napisy: na przycisku kwota i dochod; przyrost towaru, rok i udzial w podpowiedzi (przy kwocie niedostepnej powod na gorze)
                    // recenzja: kwota "still 12% on the last coin" to najwiecej przy 12% na ostatniej monecie, nie "najlepszy zakup" (mniejsze kwoty maja wyzszy procent)
                    string tag = a.Value.Length == 0 ? "" : a.Value == " (all you have)" ? " (all)" : a.Value == " (full kit)" ? " (full kit)" : " (last coin 12%)";
                    string shortText = k.ToString("N0", Inv) + tag + " - " + inc.ToString("0.0", Inv) + " a day (" + Math.Round(100 * inc * 364 / Math.Max(1, k)) + "% a year)";
                    string details = (a.Value.Length == 0 ? "" : a.Value == " (all you have)" ? "All the gold you have. " : a.Value == " (full kit)" ? "Enough for the full kit. "
                                      : "The most that still earns 12% a year on the last coin. ")
                                   + "+" + (100 * b1).ToString("0.0", Inv) + "% " + craft + " in " + q.Name + ". About " + inc.ToString("0.0", Inv) + " a day, " + Math.Round(inc * 364).ToString("N0", Inv)
                                   + " a year (" + Math.Round(100 * inc * 364 / Math.Max(1, k)) + "% of what you pay). " + (100 * myShare).ToString("0.0", Inv) + "% of its " + craft + " yours.";
                    string rowHint = ShortLabels.On ? (why != null ? why + ".\n" + details : details) : (why ?? text);
                    list.Add(new InquiryElement(k, ShortLabels.Pick(shortText, text), null, why == null, rowHint));
                }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(q.Name + " - " + KindName[q.WKind], sb.ToString(), list, true, 1, 1, "Pay", GameTexts.FindText("str_cancel").ToString(),
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            int k = (int)sel[0].Identifier;
                            var res = Buy(Hero.MainHero, Clan.PlayerClan, st.Village, uid, k, 0, false);
                            if (!res.Ok) { Log.Player(res.Why, true); return; }
                            _mBuy++;
                            var q2 = new List<Quote>(); double inc2 = 0;
                            if (QuotesOf(st.Village, q2, Clan.PlayerClan)) foreach (var x in q2) if (x.Uid == uid) inc2 = IncomeDay(x, x.UMine, x.UEff);
                            Log.Player(res.Text + " Expected: about " + inc2.ToString("0.0", Inv) + " a day (" + Math.Round(inc2 * 364).ToString("N0", Inv) + " a year).");
                        }
                        catch (Exception e) { Stumble("OknoKwot", e); }
                    }, null), true);
            }
            catch (Exception e) { Stumble("OpenPart", e); }
        }
    }
}
