using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m99-markietani (Jeff 20.10, lista obozowa pkt 7 "miasteczko obozowe z markietankami" i decyzja 14:20 pkt 4 "uslugi, rozrywka i sklep - tylko w duzych
    /// obozach (armia)") - RZAD MARKIETANOW w menu obozu gracza (arm_camp_wait - ZWYKLE menu, nie menu oczekiwania: SwitchToMenu z jego opcji jest bezpieczne,
    /// jak CampDrill "Train the men"). Klucz CampSutlersRow (domyslnie TAK). AI bez zmian - obieg AI liczy Sutlers.cs (E5/A3).
    ///
    /// KIEDY: oboz gracza poza osada, na ladzie, bez bitwy i spotkania; oboz = kolumna (druzyna gracza, a w armii - wodz i wszystkie doczepione partie, jak CampWalk);
    /// rzad stoi od SutlerRowMinMen (150) ludzi w obozie (zdrowi i ranni). Kramy: 1 na SutlerRowMenPerStall (100) ludzi, najwyzej SutlerRowMaxStalls (12):
    /// 150 ludzi - 1 kram, 400 - 4, 1000 - 10. Kramy wyznaczaja dzienna oferte (cyrulicy, zapas towaru, gotowka na skup).
    /// ZRODLO: najblizsze miasto niebedace w wojnie z graczem (Sutlers.NearestTown - to samo, do ktorego plynie obieg markietanow AI; brak - najblizsze w ogole).
    /// USLUGI (zaplata gracza = obieg markietanow jak Sutlers.One): czesc SutlerTownShare (85%) - kasa tego miasta, z hakiem 75/25 (TownFolk.EarnService KLife);
    /// reszta - kiesa najblizszej niewrogiej wsi w stanie Normal (z danina pana VillageTakings.Split, gdy Village Tribute From Sutlers; wlasna wies rodu gracza bez
    /// daniny); brak wsi - calosc do miasta.
    ///  1. Cyrulicy: opatruja rannych zolnierzy (bez bohaterow) - wraca do sluzby najwyzej SutlerSurgeonPerStall (8) na kram na dobe, najpierw najwyzszy tier;
    ///     cena za czlowieka SutlerSurgeonFee (15) x (tier + 1) - rekrut T1 30, T3 60, T5 90. Natychmiastowy powrot zamiast dni leczenia - bez zmian w modelu
    ///     leczenia gry (zadnej latki).
    ///  2. Zabawa (ale, kosci, minstrele): raz na dobe, SutlerRevelPerMan (2) denary na czlowieka druzyny gracza; morale +SutlerRevelMorale (6) przez
    ///     RecentEventsMorale (jak ArmyRestDay: gra zdejmuje 10% dodatku na dobe - po 3 dobach zostaje ok. 4.4, po 7 ok. 2.9). Tylko druzyna gracza (to on placi).
    /// SKLEP (ekran handlu gry, wzorzec SideVillageVisit handel koszykiem): koszyk liczony przy kliknieciu z polki miasta-zrodla - jedzenie, piwo, wino, narzedzia,
    /// strzaly i belty; kazdego rodzaju najwyzej SutlerRowStockPerStall (5) x kramy sztuk na dobe (mniej to, co gracz juz kupil dzis; nie wiecej niz na polce).
    /// Cena kupna = cena miasta x (1 + SutlerRowMarkupPercent 30%), skup = cena skupu miasta x (1 - 30%). Gotowka markietanow na skup = min(kasa miasta,
    /// SutlerRowCashPerStall 1500 x kramy). Po "Done": kupione schodzi z polki miasta (dokladnie roznica koszyka), sprzedane wchodzi na polke. Pieniadz: gracz
    /// zaplacil netto D (zmiana jego kiesy); narzut kupionego M = wartosc kupionego po cenie markietanow - po cenie miasta (w 0..D) idzie obiegiem uslug; reszta
    /// D - M do kasy miasta (towar po cenie miasta; ujemna = miasto placi za skup). Suma = D - nic z niczego. Anulowanie - gra cofa koszyk, nic sie nie zmienia.
    /// Stan (zapis arm_sutlerrow przez SaveText.Sync): doba, zabawa dzis, opatrzeni dzis, kupione dzis wedlug rodzaju. Wyjatek w menu - licznik potkniec, menu dziala.
    /// </summary>
    internal static class CampSutlers
    {
        internal const string P = "Markietani obozu (m99): ";
        private const string Menu = "arm_camp_sutlers";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CampSutlersRow; } }

        // ------------------------------------------------------------ stan dnia (zapis arm_sutlerrow)
        private static int _day = -1;                 // doba, do ktorej naleza liczniki ponizej
        private static bool _revelToday;
        private static int _treatedToday;
        private static readonly Dictionary<string, int> _boughtToday = new Dictionary<string, int>();

        // ------------------------------------------------------------ liczniki doby (linia doby) i potkniecia
        private static int _dVisits, _dTreated, _dRevels, _dTrades, _dPcsBought, _dPcsSold;
        private static long _dSurgeonGold, _dRevelGold, _dBuyNet, _dGoodsTown, _dMarkup, _dToTown, _dToFolk, _dToVill, _dToTax, _dPaid;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CampSutlers." + where, e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static void Roll()
        {
            int t = Today();
            if (t == _day) return;
            _day = t; _revelToday = false; _treatedToday = 0; _boughtToday.Clear();
        }

        private static void Reset()
        {
            _day = -1; _revelToday = false; _treatedToday = 0; _boughtToday.Clear();
            DayZero();
        }

        private static void DayZero()
        {
            _dVisits = _dTreated = _dRevels = _dTrades = _dPcsBought = _dPcsSold = 0;
            _dSurgeonGold = _dRevelGold = _dBuyNet = _dGoodsTown = _dMarkup = _dToTown = _dToFolk = _dToVill = _dToTax = _dPaid = 0;
        }

        // ------------------------------------------------------------ zapis: "1|doba|zabawa|opatrzeni|id:n,id:n"
        internal static string Export()
        {
            var sb = new StringBuilder(64);
            sb.Append("1|").Append(_day.ToString(Inv)).Append('|').Append(_revelToday ? '1' : '0').Append('|').Append(_treatedToday.ToString(Inv)).Append('|');
            bool first = true;
            foreach (var kv in _boughtToday)
            {
                if (kv.Key == null || kv.Key.IndexOfAny(new[] { '|', ',', ':' }) >= 0) continue;
                if (!first) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
                first = false;
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            Reset();
            try
            {
                if (string.IsNullOrEmpty(s)) return;
                var a = s.Split('|');
                if (a.Length < 5 || a[0] != "1") return;
                int v;
                if (int.TryParse(a[1], NumberStyles.Integer, Inv, out v)) _day = v;
                _revelToday = a[2] == "1";
                if (int.TryParse(a[3], NumberStyles.Integer, Inv, out v)) _treatedToday = Math.Max(0, v);
                foreach (var e in a[4].Split(','))
                {
                    int c = e.LastIndexOf(':');
                    if (c > 0 && int.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out v) && v > 0) _boughtToday[e.Substring(0, c)] = v;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ------------------------------------------------------------ wpiecie (ArmouryBehavior.RegisterEvents - jedna linia)
        internal static void RegisterEvents(CampaignBehaviorBase b)
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(b, delegate { try { Reset(); } catch (Exception e) { Stumble("Reset", e); } });
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(b, delegate (CampaignGameStarter s) { try { _stumbles = 0; _err.Clear(); DayZero(); if (s != null) AddMenus(s); } catch (Exception e) { Stumble("SessionStart", e); } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(b, delegate { try { Daily(); } catch (Exception e) { Stumble("Daily", e); } });
        }

        private static void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("arm_camp_wait", "arm_camp_sutlers_row", "{=!}Visit the sutlers' row",
                delegate (MenuCallbackArgs a)
                {
                    try
                    {
                        if (!On) return false;
                        a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        int men, stalls; string why;
                        if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                        return true;
                    }
                    catch (Exception e) { Stumble("RowCondition", e); return false; }
                },
                delegate (MenuCallbackArgs a) { try { _dVisits++; GameMenu.SwitchToMenu(Menu); } catch (Exception e) { Stumble("RowOpen", e); } }, false, 3);

            starter.AddGameMenu(Menu, "{=!}{ARM_ROW_TEXT}", RowInit);
            starter.AddGameMenuOption(Menu, "arm_row_surgeon", "{=!}{ARM_ROW_SURGEON}", SurgeonCondition, delegate (MenuCallbackArgs a) { Surgeon(); }, false, 0);
            starter.AddGameMenuOption(Menu, "arm_row_revel", "{=!}{ARM_ROW_REVEL}", RevelCondition, delegate (MenuCallbackArgs a) { Revel(); }, false, 1);
            starter.AddGameMenuOption(Menu, "arm_row_trade", "{=!}Trade with the sutlers", TradeCondition, delegate (MenuCallbackArgs a) { Trade(); }, false, 2);
            starter.AddGameMenuOption(Menu, "arm_row_back", "{=!}Back to the camp",
                delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                delegate (MenuCallbackArgs a) { try { GameMenu.SwitchToMenu("arm_camp_wait"); } catch (Exception e) { Stumble("Back", e); } }, true, 9);

            var s = Settings.Current;
            Log.Info(P + "start sesji | opcja \"Visit the sutlers' row\" w menu obozu (arm_camp_wait) | klucz Camp Sutlers Row " + (On ? "TAK" : "NIE")
                     + (s == null ? "" : " | prog obozu " + MinMen() + " ludzi, kram na " + PerStall() + " ludzi, najwyzej " + MaxStalls() + " kramow | narzut " + Markup().ToString("0.00", Inv)
                     + " | cyrulik " + SurgeonPer() + " rannych na kram na dobe, " + SurgeonFee() + " x (tier+1) za czlowieka | zabawa " + RevelPer() + " na czlowieka, morale +" + RevelMorale()
                     + " | towar " + StockPer() + " sztuk rodzaju na kram na dobe | gotowka skupu " + CashPer() + " na kram")
                     + " | stan dnia: doba " + _day + ", zabawa " + (_revelToday ? "tak" : "nie") + ", opatrzeni " + _treatedToday + ", rodzajow kupionych " + _boughtToday.Count + ".");
        }

        // ------------------------------------------------------------ liczby (przyciete jak suwaki MCM)
        private static int MinMen() { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(5000, s.SutlerRowMinMen)) : 150; }
        private static int PerStall() { var s = Settings.Current; return s != null ? Math.Max(10, Math.Min(1000, s.SutlerRowMenPerStall)) : 100; }
        private static int MaxStalls() { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(50, s.SutlerRowMaxStalls)) : 12; }
        private static float Markup() { var s = Settings.Current; return s != null ? MBMath.ClampFloat(s.SutlerRowMarkupPercent / 100f, 0f, 1f) : 0.3f; }
        private static int SurgeonPer() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(100, s.SutlerSurgeonPerStall)) : 8; }
        private static int SurgeonFee() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(1000, s.SutlerSurgeonFee)) : 15; }
        private static int RevelPer() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(50, s.SutlerRevelPerMan)) : 2; }
        private static int RevelMorale() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(50, s.SutlerRevelMorale)) : 6; }
        private static int StockPer() { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(100, s.SutlerRowStockPerStall)) : 5; }
        private static int CashPer() { var s = Settings.Current; return s != null ? Math.Max(0, Math.Min(100000, s.SutlerRowCashPerStall)) : 1500; }

        /// <summary>Ludzie obozu: druzyna gracza, w armii - wodz i doczepione partie (jak CampWalk). Zdrowi i ranni.</summary>
        private static int CampMen(out bool army)
        {
            var mp = MobileParty.MainParty;
            army = false;
            if (mp == null) return 0;
            var ar = mp.Army;
            var lead = ar != null ? ar.LeaderParty : null;
            if (lead != null && (lead == mp || mp.AttachedTo == lead))
            {
                army = true;
                int n = lead.MemberRoster != null ? lead.MemberRoster.TotalManCount : 0;
                var att = lead.AttachedParties;
                if (att != null) for (int i = 0; i < att.Count; i++) { var p = att[i]; if (p != null && p != lead && p.MemberRoster != null) n += p.MemberRoster.TotalManCount; }
                return n;
            }
            return mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
        }

        private static int Stalls(int men) { return Math.Max(1, Math.Min(MaxStalls(), men / PerStall())); }

        private static bool Can(out int men, out int stalls, out string why)
        {
            why = null; men = 0; stalls = 0;
            var mp = MobileParty.MainParty;
            bool army;
            if (Campaign.Current == null || mp == null || Hero.MainHero == null) why = "No campaign is running.";
            else if (mp.MapEvent != null || mp.SiegeEvent != null || PlayerEncounter.Current != null) why = "Not while in a battle or siege.";
            else if (mp.CurrentSettlement != null) why = "Leave the settlement first.";
            else if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) why = "No sutlers follow you at sea.";
            else
            {
                men = CampMen(out army);
                if (men < MinMen()) why = "Only a big camp draws sutlers - " + MinMen() + " men or more (your camp: " + men + ").";
                else stalls = Stalls(men);
            }
            return why == null;
        }

        private static Settlement Source()
        {
            var mp = MobileParty.MainParty;
            var p = mp.GetPosition2D;
            var t = Sutlers.NearestTown(p, mp.MapFaction);
            if (t == null) t = Sutlers.NearestTown(p, null);
            return t != null && t.Town != null ? t : null;
        }

        private static string CampText(int men, int stalls)
        {
            bool army; CampMen(out army);
            return men + " men" + (army ? " in the army's camp" : " in your camp");
        }

        private static void RowInit(MenuCallbackArgs a)
        {
            try
            {
                Roll();
                try { NightRest.SetCampBackground(a); } catch { }
                int men, stalls; string why;
                string text;
                if (!Can(out men, out stalls, out why)) text = why;
                else
                {
                    var src = Source();
                    text = "Behind the tents the sutlers have set up their row - " + stalls + (stalls == 1 ? " stall" : " stalls") + " for " + CampText(men, stalls)
                           + ": barber-surgeons, an ale tent with dice and a minstrel, and carts of food, drink, tools and arrows"
                           + (src != null ? " brought from " + src.Name + "." : ".")
                           + " Their prices are the town's, with " + (Markup() * 100f).ToString("0", Inv) + "% for the cartage.";
                    if (_revelToday) text += " The men have already had their revel today.";
                }
                MBTextManager.SetTextVariable("ARM_ROW_TEXT", text, false);
            }
            catch (Exception e) { Stumble("RowInit", e); MBTextManager.SetTextVariable("ARM_ROW_TEXT", "The sutlers' row.", false); }
        }

        // ------------------------------------------------------------ obieg zaplaty uslug (jak Sutlers.One)
        /// <summary>Zloto zdjete juz z kiesy gracza: miasto (z hakiem 75/25) i wies (z danina pana). Zwraca opis odbiorcow do logu. Tylko arytmetyka i ChangeGold.</summary>
        private static string Spread(Settlement town, int gold, out int toTown, out int toFolk, out int toVill, out int toTax)
        {
            toTown = toFolk = toVill = toTax = 0;
            if (gold <= 0 || town == null || town.Town == null) return "-";
            var s = Settings.Current;
            float townShare = s != null ? MBMath.ClampFloat(s.SutlerTownShare, 0f, 1f) : 0.85f;
            int villPart = (int)(gold * (double)(1f - townShare));
            Settlement vill = null;
            if (villPart > 0)
            {
                var mp = MobileParty.MainParty;
                vill = Sutlers.NearestVillage(mp.GetPosition2D, mp.MapFaction);
                if (vill != null && (vill.SettlementComponent == null || vill.Village == null)) vill = null;
                if (vill == null) villPart = 0;
            }
            int townPart = gold - villPart;
            int folk = townPart > 0 ? TownFolk.EarnService(town, townPart, TownFolk.KLife) : 0;
            if (townPart - folk != 0) town.Town.ChangeGold(townPart - folk);
            toTown = townPart - folk; toFolk = folk;
            try { TownFolk.Note(town, townPart, TownFolk.KLife); } catch { }   // jak Sutlers.Pay: wydatek ludzi w kasie miasta (tylko licznik)
            if (villPart > 0)
            {
                var vv = vill.Village;
                if (s != null && s.VillageTributeFromSutlers && !ReferenceEquals(vill.OwnerClan, Clan.PlayerClan))
                {
                    int vtax, vpurse;
                    VillageTakings.Split(vv, villPart, villPart, out vtax, out vpurse);
                    toVill = vpurse; toTax = vtax;
                }
                else { vv.ChangeGold(villPart); toVill = villPart; }
            }
            _dToTown += toTown; _dToFolk += toFolk; _dToVill += toVill; _dToTax += toTax; _dPaid += gold;
            return "kasa miasta " + town.StringId + " " + toTown + " + lud " + toFolk + (vill != null ? " + kiesa wsi " + vill.StringId + " " + toVill + " + danina " + toTax : " (bez wsi - calosc do miasta)");
        }

        private static bool TakeFromPlayer(int gold)
        {
            if (gold <= 0 || Hero.MainHero == null || Hero.MainHero.Gold < gold) return false;
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, gold, PayNotes.Quiet(false));
            return true;
        }

        // ------------------------------------------------------------ 1. cyrulicy
        private static int TreatPlan(int stalls, out int price, out int wounded)
        {
            price = 0; wounded = 0;
            var roster = MobileParty.MainParty.MemberRoster;
            int cap = Math.Max(0, stalls * SurgeonPer() - _treatedToday);
            var list = new List<TroopRosterElement>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero || el.WoundedNumber <= 0) continue;
                wounded += el.WoundedNumber;
                list.Add(el);
            }
            list.Sort((x, y) => y.Character.Tier.CompareTo(x.Character.Tier));
            int n = 0;
            foreach (var el in list)
            {
                if (n >= cap) break;
                int k = Math.Min(el.WoundedNumber, cap - n);
                n += k; price += k * SurgeonFee() * (Math.Max(0, el.Character.Tier) + 1);
            }
            return n;
        }

        private static bool SurgeonCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Manage;
                Roll();
                int men, stalls, price, wounded; string why;
                if (!Can(out men, out stalls, out why))
                {
                    MBTextManager.SetTextVariable("ARM_ROW_SURGEON", "Pay the barber-surgeons to tend the wounded", false);
                    a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true;
                }
                int n = TreatPlan(stalls, out price, out wounded);
                MBTextManager.SetTextVariable("ARM_ROW_SURGEON", n > 0 ? "Pay the barber-surgeons to tend " + n + " wounded (" + price + " denars)" : "Pay the barber-surgeons to tend the wounded", false);
                if (wounded <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}None of your soldiers is wounded."); }
                else if (n <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The surgeons have done all they can today (" + stalls * SurgeonPer() + " men a day at " + stalls + (stalls == 1 ? " stall" : " stalls") + ")."); }
                else if (Hero.MainHero.Gold < price) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You cannot pay " + price + " denars."); }
                else a.Tooltip = new TextObject("{=!}The surgeons dress the wounds of " + n + " of your " + wounded + " wounded soldiers, the most seasoned first, and they return to duty. " + SurgeonFee() + " denars x (tier + 1) a man; at most " + stalls * SurgeonPer() + " men a day.");
                return true;
            }
            catch (Exception e) { Stumble("SurgeonCondition", e); return false; }
        }

        private static void Surgeon()
        {
            try
            {
                Roll();
                int men, stalls, price, wounded; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                var src = Source();
                if (src == null) { Log.Player("No sutlers can reach you here.", true); return; }
                int n = TreatPlan(stalls, out price, out wounded);
                if (n <= 0 || price <= 0 || !TakeFromPlayer(price)) { Log.Player("The surgeons cannot help you now.", true); return; }
                // ---- zloto zdjete: najpierw obieg (arytmetyka), potem leczenie
                int tt, tf, tv, tx;
                string to = Spread(src, price, out tt, out tf, out tv, out tx);
                var roster = MobileParty.MainParty.MemberRoster;
                var list = new List<TroopRosterElement>();
                for (int i = 0; i < roster.Count; i++) { var el = roster.GetElementCopyAtIndex(i); if (el.Character != null && !el.Character.IsHero && el.WoundedNumber > 0) list.Add(el); }
                list.Sort((x, y) => y.Character.Tier.CompareTo(x.Character.Tier));
                int left = n, healed = 0;
                foreach (var el in list)
                {
                    if (left <= 0) break;
                    int k = Math.Min(el.WoundedNumber, left);
                    roster.AddToCounts(el.Character, 0, false, -k);
                    left -= k; healed += k;
                }
                _treatedToday += healed; _dTreated += healed; _dSurgeonGold += price;
                if (PayNotes.On) PayNotes.Paid("markietani-cyrulik", price, " to the barber-surgeons for tending " + healed + " wounded.");
                Log.Info(P + "cyrulik | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | opatrzeni " + healed + " z " + wounded + " rannych (dzis " + _treatedToday + " z " + stalls * SurgeonPer()
                         + ") | gracz zaplacil " + price + " -> " + to + " | kontrola: " + price + " = " + (tt + tf + tv + tx) + " (roznica " + (price - tt - tf - tv - tx) + ") | zrodlo " + src.StringId + ".");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Stumble("Surgeon", e); }
        }

        // ------------------------------------------------------------ 2. zabawa
        private static int RevelPrice() { var mp = MobileParty.MainParty; return mp != null && mp.MemberRoster != null ? mp.MemberRoster.TotalHealthyCount * RevelPer() : 0; }

        private static bool RevelCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Wait;
                Roll();
                int men, stalls; string why;
                int price = RevelPrice();
                MBTextManager.SetTextVariable("ARM_ROW_REVEL", "Stand the men a round - ale, dice and a minstrel (" + price + " denars)", false);
                if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                else if (_revelToday) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The men have had their revel today - once a day."); }
                else if (price <= 0) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}There is nobody to drink with."); }
                else if (Hero.MainHero.Gold < price) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You cannot pay " + price + " denars."); }
                else a.Tooltip = new TextObject("{=!}" + RevelPer() + " denars a man for your " + (price / Math.Max(1, RevelPer())) + " men. Morale +" + RevelMorale() + ", fading over the following days. Once a day.");
                return true;
            }
            catch (Exception e) { Stumble("RevelCondition", e); return false; }
        }

        private static void Revel()
        {
            try
            {
                Roll();
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                if (_revelToday) return;
                var src = Source();
                int price = RevelPrice();
                if (src == null || price <= 0 || !TakeFromPlayer(price)) { Log.Player("The ale tent cannot serve you now.", true); return; }
                int tt, tf, tv, tx;
                string to = Spread(src, price, out tt, out tf, out tv, out tx);
                var mp = MobileParty.MainParty;
                float m0 = mp.RecentEventsMorale;
                mp.RecentEventsMorale += RevelMorale();
                _revelToday = true; _dRevels++; _dRevelGold += price;
                if (PayNotes.On) PayNotes.Paid("markietani-zabawa", price, " for a round of ale, dice and a minstrel for your men.");
                Log.Player("The men drink, dice and sing late into the night. (Morale +" + RevelMorale() + ")");
                Log.Info(P + "zabawa | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | druzyna gracza " + (price / Math.Max(1, RevelPer())) + " zdrowych x " + RevelPer()
                         + " | morale RecentEvents " + m0.ToString("0.0", Inv) + " -> " + mp.RecentEventsMorale.ToString("0.0", Inv) + " | gracz zaplacil " + price + " -> " + to
                         + " | kontrola: " + price + " = " + (tt + tf + tv + tx) + " (roznica " + (price - tt - tf - tv - tx) + ") | zrodlo " + src.StringId + ".");
                GameMenu.SwitchToMenu(Menu);
            }
            catch (Exception e) { Stumble("Revel", e); }
        }

        // ------------------------------------------------------------ 3. sklep
        private static bool Sold(ItemObject it)
        {
            if (it == null) return false;
            if (it.IsFood) return true;
            if (it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts) return true;
            var c = it.ItemCategory != null ? it.ItemCategory.StringId : null;
            return c == "beer" || c == "wine" || c == "tools";
        }

        private static ItemRoster Basket(Settlement town, int stalls, out int kinds, out long pieces)
        {
            kinds = 0; pieces = 0;
            var basket = new ItemRoster();
            var shelf = town.ItemRoster;
            if (shelf == null) return basket;
            int per = stalls * StockPer();
            var used = new Dictionary<string, int>();
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || !Sold(it) || it.StringId == null) continue;
                int b; _boughtToday.TryGetValue(it.StringId, out b);
                int u; used.TryGetValue(it.StringId, out u);
                int n = Math.Min(el.Amount, per - b - u);
                if (n <= 0) continue;
                basket.AddToCounts(el.EquipmentElement, n);
                used[it.StringId] = u + n;
                kinds++; pieces += n;
            }
            return basket;
        }

        private static bool TradeCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                var src = Source();
                if (src == null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}No town is near enough to supply the sutlers."); return true; }
                a.Tooltip = new TextObject("{=!}Food, ale, wine, tools, arrows and bolts from the market of " + src.Name + " at its prices plus " + (Markup() * 100f).ToString("0", Inv)
                                           + "% - up to " + stalls * StockPer() + " of each kind a day. They buy from you at the town's price less the same share, with at most "
                                           + stalls * CashPer() + " denars in hand.");
                return true;
            }
            catch (Exception e) { Stumble("TradeCondition", e); return false; }
        }

        private static void Trade()
        {
            try
            {
                Roll();
                int men, stalls; string why;
                if (!Can(out men, out stalls, out why)) { Log.Player(why, true); return; }
                var src = Source();
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (src == null || gsm == null || Settlement.CurrentSettlement != null) return;   // z osady gra placi sama - tu tylko z pola
                int kinds; long pieces;
                var basket = Basket(src, stalls, out kinds, out pieces);
                var before = new ItemRoster(basket);
                float up = Markup();
                var market = new RowMarket(src.Town, up);
                var listener = new RowListener(src.Town, stalls * CashPer());
                var state = gsm.CreateState<InventoryState>();
                state.InventoryMode = InventoryScreenHelper.InventoryMode.Trade;
                var logic = new InventoryLogic(src.Party);
                logic.Initialize(basket, PartyBase.MainParty.ItemRoster, PartyBase.MainParty.MemberRoster, true, true, CharacterObject.PlayerCharacter,
                                 InventoryScreenHelper.InventoryCategoryType.None, market, false, state.InventoryMode, new TextObject("{=!}Sutlers' row"));
                logic.SetInventoryListener(listener);
                state.InventoryLogic = logic;
                int gold0 = Hero.MainHero.Gold, town0 = src.Town.Gold;
                state.DoneLogicExtrasDelegate = delegate { TradeDone(src, men, stalls, market, basket, before, gold0, town0); };
                Log.Info(P + "sklep otwarty | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | zrodlo " + src.StringId + " | koszyk z polki miasta: " + kinds + " rodzajow, " + pieces
                         + " sztuk (najwyzej " + stalls * StockPer() + " rodzaju na dobe) | narzut " + up.ToString("0.00", Inv) + " | gotowka skupu " + listener.GetGold() + " | kiesa gracza " + gold0 + ", kasa miasta " + town0 + ".");
                gsm.PushState(state);
            }
            catch (Exception e) { Stumble("Trade", e); }
        }

        /// <summary>Po udanym DoneLogic (gra zmienila juz kiese gracza - bez osady i bez spotkania tylko ja): polka miasta o roznice koszyka, pieniadz po stronie markietanow.</summary>
        private static void TradeDone(Settlement src, int men, int stalls, RowMarket market, ItemRoster basket, ItemRoster before, int gold0, int town0)
        {
            try
            {
                Roll();
                var shelf = src.ItemRoster;
                long buyRow = 0, buyTown = 0, sellRow = 0;
                int pcsOut = 0, pcsIn = 0, short_ = 0;
                // ceny liczone PRZED zmiana polki (ta sama chwila co ekran handlu)
                var moves = new List<KeyValuePair<EquipmentElement, int>>();
                for (int i = 0; i < basket.Count; i++)
                {
                    var el = basket.GetElementCopyAtIndex(i);
                    int bi = before.FindIndexOfElement(el.EquipmentElement);
                    int d = el.Amount - (bi >= 0 ? before.GetElementNumber(bi) : 0);
                    if (d != 0) moves.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, d));
                }
                for (int i = 0; i < before.Count; i++)
                {
                    var el = before.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && basket.FindIndexOfElement(el.EquipmentElement) < 0) moves.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, -el.Amount));
                }
                var mp = MobileParty.MainParty;
                foreach (var mv in moves)
                {
                    if (mv.Value < 0) { int k = -mv.Value; buyRow += (long)k * market.GetPrice(mv.Key, mp, false, src.Party); buyTown += (long)k * src.Town.MarketData.GetPrice(mv.Key, mp, false, src.Party); }
                    else sellRow += (long)mv.Value * market.GetPrice(mv.Key, mp, true, src.Party);
                }
                foreach (var mv in moves)
                {
                    if (mv.Value < 0)
                    {
                        int si = shelf.FindIndexOfElement(mv.Key);
                        int have = si >= 0 ? shelf.GetElementNumber(si) : 0;
                        int k = Math.Min(-mv.Value, Math.Max(0, have));
                        if (k < -mv.Value) short_ += -mv.Value - k;
                        if (k > 0) shelf.AddToCounts(mv.Key, -k);
                        pcsOut += -mv.Value;
                        var id = mv.Key.Item != null ? mv.Key.Item.StringId : null;
                        if (id != null) { int b; _boughtToday.TryGetValue(id, out b); _boughtToday[id] = b + (-mv.Value); }
                    }
                    else { shelf.AddToCounts(mv.Key, mv.Value); pcsIn += mv.Value; }
                }
                // ---- pieniadz: D = to, co gracz oddal netto (gra zdjela/dala juz zloto graczowi)
                int paid = gold0 - Hero.MainHero.Gold;
                long mk = Math.Max(0L, buyRow - buyTown);
                if (mk > Math.Max(0, paid)) mk = Math.Max(0, paid);
                int markup = (int)mk;
                int goods = paid - markup;                        // towar po cenie miasta (ujemne - miasto placi za skup)
                if (goods != 0) src.Town.ChangeGold(goods);
                int tt = 0, tf = 0, tv = 0, tx = 0;
                string to = markup > 0 ? Spread(src, markup, out tt, out tf, out tv, out tx) : "-";
                _dTrades++; _dPcsBought += pcsOut; _dPcsSold += pcsIn; _dBuyNet += paid; _dGoodsTown += goods; _dMarkup += markup;
                // komunikat zaplaty daje sama gra (DoneLogic: GiveGoldAction z powiadomieniem, jak w kazdym handlu) - bez drugiego naszego
                Log.Info(P + "sklep zamkniety | oboz " + men + " ludzi (prog " + MinMen() + "), kramy " + stalls + " | zrodlo " + src.StringId + " | kupione " + pcsOut + " sztuk (cena markietanow "
                         + buyRow + ", miasta " + buyTown + "), z polki miasta zdjete " + (pcsOut - short_) + " (brak na polce " + short_ + "), sprzedane " + pcsIn + " sztuk (skup " + sellRow
                         + ") na polke miasta " + pcsIn + " | gracz netto " + paid + " (kiesa " + gold0 + " -> " + Hero.MainHero.Gold + ") = towar do kasy miasta " + goods + " + narzut " + markup
                         + " | narzut -> " + to + " | kontrola: " + paid + " = " + goods + " + " + (tt + tf + tv + tx) + " (roznica " + (paid - goods - tt - tf - tv - tx) + ") | kasa miasta " + town0 + " -> " + src.Town.Gold + ".");
            }
            catch (Exception e) { Stumble("TradeDone", e); }
        }

        /// <summary>Ceny markietanow: cena miasta-zrodla x (1 + narzut) przy kupnie, x (1 - narzut) przy skupie.</summary>
        private sealed class RowMarket : IMarketData
        {
            private readonly Town _t; private readonly float _up;
            internal RowMarket(Town t, float up) { _t = t; _up = up; }
            public int GetPrice(ItemObject item, MobileParty tradingParty, bool isSelling, PartyBase merchantParty) { return GetPrice(new EquipmentElement(item), tradingParty, isSelling, merchantParty); }
            public int GetPrice(EquipmentElement e, MobileParty tradingParty, bool isSelling, PartyBase merchantParty)
            {
                int b = _t.MarketData.GetPrice(e, tradingParty, isSelling, merchantParty);
                return isSelling ? Math.Max(1, (int)(b * (1.0 - _up))) : Math.Max(1, (int)Math.Ceiling(b * (1.0 + _up)));
            }
        }

        /// <summary>Gotowka markietanow na skup = pieniadz miasta-zrodla, najwyzej limit kramow. SetGold gra wola tylko w osadzie - tu nie.</summary>
        private sealed class RowListener : InventoryListener
        {
            private readonly Town _t; private readonly int _cash;
            internal RowListener(Town t, int cash) { _t = t; _cash = Math.Max(0, cash); }
            public override int GetGold() { return Math.Max(0, Math.Min(_t.Gold, _cash)); }
            public override TextObject GetTraderName() { return new TextObject("{=!}Sutlers' row"); }
            public override void SetGold(int gold) { }
            public override PartyBase GetOppositeParty() { return _t.Settlement != null ? _t.Settlement.Party : null; }
            public override void OnTransaction() { }
        }

        // ------------------------------------------------------------ doba
        private static void Daily()
        {
            bool any = _dVisits + _dTreated + _dRevels + _dTrades > 0 || _stumbles > 0;
            if (any && Settings.Current != null && Settings.Current.LogEnabled)
                Log.Info(P + "dzien " + (Today() - 1) + " | klucz " + (On ? "TAK" : "WYL") + " | wizyty " + _dVisits + " | cyrulik: opatrzeni " + _dTreated + " za " + _dSurgeonGold
                         + " | zabawa " + _dRevels + " za " + _dRevelGold + " | sklep " + _dTrades + ": kupione " + _dPcsBought + " sztuk, sprzedane " + _dPcsSold + ", gracz netto " + _dBuyNet
                         + " = towar do kas miast " + _dGoodsTown + " + narzut " + _dMarkup + " | obrot uslug i narzutu " + _dPaid + " -> kasy miast " + _dToTown + " + lud " + _dToFolk
                         + " + kiesy wsi " + _dToVill + " + danina " + _dToTax + " (roznica " + (_dPaid - _dToTown - _dToFolk - _dToVill - _dToTax) + ") | potkniecia " + _stumbles + ".");
            DayZero();
        }
    }
}
