using System;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E4/7 (164b-1, U21; Jeff 05.10 SP:347 "ksiega ma byc do sprawdzenia ... pozycja w menu osady"): pozycja "People of this land" w menu wsi, miasta i zamku -
    /// okno informacyjne (InformationManager.ShowInquiry) z kontami regionu osady (warownia OutlawLaw.RegionFor): w domu, pod bronia, w niewoli, wyrzutki, uchodzcy,
    /// polegli i narodziny od poczatku ksiegi, wolne rece i plon. Liczby z cache doby (ta sama doba co linie "Pochodzenie (E4/7)" i "Wolne rece (E4/7)").
    /// Bez GameMenu.SwitchToMenu (CLAUDE.md 7) i bez stanu. Napisy po angielsku. Wylacznik PeopleMenuEntry.
    /// E4/10 (167): weterani krainy czekajacy w karczmach (konto WETERANI wedlug regionu pochodzenia) i - w miescie - weterani w karczmie tego miasta.
    /// E4/11 (193, plan rozdz. 6): pod tekstem E4/7 sekcja okregu z ksiegi wiosek (VillageBook.MenuSection - wies: lista wiosek, miasto i zamek: wiersz na okreg);
    /// ta sama pozycja takze w menu village_looted (gracz we wsi spladrowanej trafia do niego zamiast do "village"), index 0 - nad "Leave...".
    /// Scalenie calosci etapu 4: najpierw wszystkie dopiski E4/7/E4/8/E4/10 do sb, potem body = sb.TrimEnd i sekcja okregu E4/11.
    /// E5/C4 (205, PLAN 5.13): na koncu czesci E4/7 jedno zdanie "czy ludzi stac na chleb" dla osady, w ktorej stoi gracz (TownFolk.BreadLine - kiesa ludu z ostatniej doby),
    /// linia "PeopleMenu (E5/C4)" do logu przy kazdym otwarciu; wylacznik PeopleMenuShowsBread. Dalej ShowInquiry, bez SwitchToMenu.
    /// P1 (obieg kiesy ludu): zaraz pod nim zdanie "Savings: ..." (TownFolk.SavingsLine - dni zycia w kiesie, kiesa ponad zapas, danina wojenna ostatniej doby),
    /// linia "PeopleMenu (P1 Savings)" do logu; wylacznik PeopleMenuShowsSavings.
    /// </summary>
    internal static class PeopleMenu
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                foreach (var menu in new[] { "village", "town", "castle" })
                    starter.AddGameMenuOption(menu, "arm_people_" + menu, "{=!}People of this land", Condition, Consequence, false, 6);
                // E4/11 (5.3, RB K9): wies Looted - gra przelacza na village_looted (jedyna opcja "Leave..." isLeave); AddGameMenuOption wstawia na index 0 (nad nia)
                starter.AddGameMenuOption("village_looted", "arm_people_village_looted", "{=!}People of this land", Condition, Consequence, false, 0);
                Log.Info("PeopleMenu (E4/7): pozycja \"People of this land\" w menu wsi, miasta i zamku wpieta (wylacznik People Menu Entry); E4/11: takze we wsi spladrowanej (village_looted) i z sekcja okregu (ksiega wiosek).");
                var s0 = Settings.Current;
                Log.Info("PeopleMenu (E5/C4): start | zdanie o chlebie (PeopleMenuShowsBread) " + (s0 != null && s0.PeopleMenuShowsBread ? "wlaczone" : "wylaczone")
                         + " | kiesa ludu " + (TownFolk.LiveKey ? "czynna" : "NIECZYNNA - bez zdania") + " | liczby z ostatniej doby kiesy (linia Kiesa ludu:), linia PeopleMenu (E5/C4) przy kazdym otwarciu okna.");
            }
            catch (Exception e) { Log.Error("PeopleMenu.AddMenus", e); }
        }

        private static bool Condition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                var s = Settings.Current;
                return s != null && s.PeopleMenuEntry && Settlement.CurrentSettlement != null;
            }
            catch { return false; }
        }

        private static string N(double v) { return Math.Max(0.0, v).ToString("N0", Inv); }
        private static string P(double v) { return (100.0 * v).ToString("0.#", Inv) + "%"; }

        private static void Consequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null) return;
                var region = OutlawLaw.RegionFor(st);
                string name = region != null && region.Name != null ? region.Name.ToString() : (st.Name != null ? st.Name.ToString() : "this land");
                string title = "The people of " + name + (st.IsVillage ? " and its villages" : "");
                var v = PeopleBook.ViewOf(region);
                var sb = new StringBuilder(800);
                if (v == null || !PeopleBook.OriginLive)
                {
                    sb.Append("The people of this land have not been reckoned yet.\n");     // recenzja E4/10 R1-4: dalej moze isc linia karczmy miasta (TrimEnd na koncu)
                }
                else
                {
                    sb.Append("At home: ").Append(N(v.Dom)).Append(" (villages ").Append(N(v.VillagePeople)).Append(" in ").Append(v.Villages).Append(v.Villages == 1 ? " village" : " villages");
                    if (v.TownPeople > 0) sb.Append(", town ").Append(N(v.TownPeople));
                    sb.Append(")\n");
                    // E4/8 (R2, 9.6): miasto - mieszczanie i rok w dzisiejszym tempie (z liczb doby, bez nowego stanu)
                    try
                    {
                        double tp, tn, tm;
                        if (region != null && region.IsTown && TownGrowth.MenuOf(region, out tp, out tn, out tm))
                            sb.Append("Townsfolk: ").Append(N(tp)).Append(" (a year at today's rate: births minus deaths ").Append(tn >= 0 ? "+" : "-").Append(N(Math.Abs(tn)))
                              .Append(", newcomers from the villages ").Append(N(tm)).Append(")\n");
                    }
                    catch (Exception e) { Log.Error("PeopleMenu.Townsfolk", e); }
                    sb.Append("Under arms: ").Append(N(v.S)).Append(" men from this land serve in parties, garrisons, militias, caravans and bands\n");
                    sb.Append("Held captive: ").Append(N(v.J)).Append('\n');
                    sb.Append("Outlaws from this land: ").Append(N(v.Wy)).Append('\n');
                    if (v.Carts > 0) sb.Append("On the roads with the village carts and boats: ").Append(N(v.Carts)).Append('\n');
                    if (v.Road >= 1.0) sb.Append("Travelling with Banner Kings travellers and convoys: ").Append(N(v.Road)).Append('\n');     // E4/8 (R19)
                    if (v.Rally >= 1.0) sb.Append("Stragglers waiting to rejoin their lords: ").Append(N(v.Rally)).Append('\n');     // N7 (recenzja K3)
                    if (v.U > 0 || Devastation.On) sb.Append("Refugees: ").Append(N(v.U)).Append('\n');
                    // E4/9 (5.6): niewolni trzymani w tej krainie i ludzie stad w niewoli (konto NIEWOLNI ksiegi)
                    if (ThrallLaw.On)
                    {
                        if (v.THeld >= 1.0) sb.Append("Slaves held in this land: ").Append(N(v.THeld)).Append(v.Realm == "sturgia" ? " (they work the fields, so more free men can go reaving)" : "").Append('\n');
                        // recenzja E4/9 R1-3: od siewu (TSince = zywi + zmarli - stan dnia siewu) - bilans otwarcia NK (komorka wysp, pochodzenie = wyspy) nie jest "zabrany"
                        if (v.TSince >= 1.0) sb.Append("Taken from this land into slavery since the reckoning began: ").Append(N(v.TSince)).Append('\n');
                    }
                    sb.Append("Fallen since the reckoning began: ").Append(N(v.Fallen)).Append('\n');
                    if (v.Enslaved + v.ToWall >= 1.0) sb.Append("Sold into slavery or sent to the Wall since the reckoning began: ").Append(N(v.Enslaved + v.ToWall)).Append('\n');
                    if (Math.Abs(v.Born) >= 1.0) sb.Append(v.Born >= 0 ? "Born since the reckoning began (net): " : "Lost to hard years since the reckoning began (net): ").Append(N(Math.Abs(v.Born))).Append('\n');
                    if (v.Civil >= 1.0) sb.Append("Common folk who died of war and want: ").Append(N(v.Civil)).Append('\n');
                    var c = FreeHands.Of(region);
                    if (v.Realm == "nightswatch") sb.Append("The Watch takes men from all of Westeros - no limit here.\n");
                    else if (c != null)
                    {
                        sb.Append("Free hands used: ").Append(P(c.A)).Append(" of ").Append(c.F >= 0.999 ? "all" : P(c.F)).Append(" of the men");
                        // recenzja E4/7 R2-4: rzeczywisty czynnik okna zniw z MCM (Free Hands Harvest Factor), nie stale "halved"
                        if (FreeHands.HarvestNow && c.F < 0.999 && Settings.Current != null)
                            sb.Append(" (harvest: only ").Append(P(Math.Max(0.0, Math.Min(1.0, Settings.Current.FreeHandsHarvestFactor)))).Append(" of the usual share is free)");
                        sb.Append('\n');
                        if (c.Brake && Settings.Current != null && Settings.Current.FreeHandsBrake) sb.Append("No new levies: this land has no free hands left.\n");
                    }
                    if (c != null && c.L < 1.0) sb.Append("Harvest of the fields: ").Append(P(c.L)).Append('\n');
                    if (PeopleBook.VetLive) sb.Append("Veterans of this land waiting in taverns: ").Append(N(v.Vet)).Append('\n');
                }
                // GL-1 (P1, projekt "gospodarka od ludzi" rozdz. 9 "Pominiete"): we wsi jedno zdanie, dlaczego wies daje mniej albo wiecej niz dawniej (czynnik reguly,
                // ludzie okregu i skala wobec sredniej wsi swiata; Straz - jawny wyjatek). Tylko odczyt pamieci doby; klucz wylaczony - bez zdania
                if (st.IsVillage)
                {
                    try { string pl = VillageByPeople.MenuLine(st); if (!string.IsNullOrEmpty(pl)) sb.Append(pl).Append('\n'); }
                    catch (Exception e) { Log.Error("PeopleMenu.GoodsByPeople", e); }
                }
                // E4/10: karczma tego miasta - weterani z calego swiata, ktorzy staneli tu najblizej (ta sama liczba co linia dzienna "Weterani (E4/10)")
                if (st.IsTown && PeopleBook.VetLive)
                {
                    int mounted, days;
                    int men = PeopleBook.VetInTown(st, out mounted, out days);
                    if (men > 0) sb.Append("In this town's tavern: ").Append(men).Append(men == 1 ? " veteran waits" : " veterans wait").Append(" for a master (").Append(mounted).Append(" mounted); the longest has waited ")
                                   .Append(days).Append(days == 1 ? " day." : " days.").Append('\n');
                    else sb.Append("No veterans wait in this town's tavern.\n");
                }
                // E5/C4 (205, PLAN 5.13): czy ludzi tej osady stac na chleb - liczby kiesy ludu z ostatniej doby (tylko odczyt), linia do logu przy kazdym otwarciu
                var s1 = Settings.Current;
                if (s1 != null && s1.PeopleMenuShowsBread)
                {
                    try
                    {
                        string blog;
                        string bread = TownFolk.BreadLine(st, out blog);
                        if (blog != null) Log.Info(blog);
                        if (!string.IsNullOrEmpty(bread)) sb.Append(bread).Append('\n');
                    }
                    catch (Exception e) { Log.Error("PeopleMenu.Bread", e); }
                }
                // P1 (obieg kiesy ludu, projekt rozdz. 7): na ile dni zycia lud tej osady ma monety, ile lezy ponad zapasem i ile oddal koronie w daninie wojennej
                // o ostatnim swicie - liczby kiesy ludu z ostatniej doby (tylko odczyt), linia do logu przy kazdym otwarciu; wylacznik PeopleMenuShowsSavings
                if (s1 != null && s1.PeopleMenuShowsSavings)
                {
                    try
                    {
                        string slog;
                        string sav = TownFolk.SavingsLine(st, out slog);
                        if (slog != null) Log.Info(slog);
                        if (!string.IsNullOrEmpty(sav)) sb.Append(sav).Append('\n');
                    }
                    catch (Exception e) { Log.Error("PeopleMenu.Savings", e); }
                }
                string body = sb.ToString().TrimEnd('\n');
                // E4/11: sekcja okregu (wies) albo okregi regionu (miasto, zamek) z ksiegi wiosek - liczby na zywo, bez stanu menu
                try { string dist = VillageBook.MenuSection(st); if (!string.IsNullOrEmpty(dist)) body += "\n\n" + dist; }
                catch (Exception e) { Log.Error("PeopleMenu.Consequence(E4/11)", e); }
                InformationManager.ShowInquiry(new InquiryData(title, body, true, false, "Close", null, delegate { }, null), true);
            }
            catch (Exception e) { Log.Error("PeopleMenu.Consequence", e); }
        }
    }
}
