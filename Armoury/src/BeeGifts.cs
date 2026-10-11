using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// E5/A5 (paczka 197, projekt etapu 5 v2 rozdz. 3 "E5/A5", regula R23; decyzja Jeffa 09.10 ok. 04:25 G: "BetterEconomy: prawdziwa wplata do kasy miasta
    /// + dar dla notabli - TAK"). Wylacznik BeePlayerGiftsReal (domyslnie wlaczony). Wpiecie: BeeSeal (prefiksy Try*, postfiksy Can*, owijka menu BEE).
    ///
    /// Dzis (170, BeeSeal): wplata gracza do skarbca miasta i zamku BEE zamknieta postfiksem Can*. BEE v1.4.5 zdejmuje zloto graczowi GiveGoldAction(gracz, nic)
    /// i dopisuje je do WIRTUALNEGO skarbca (BEE TownEconomyCampaignBehavior.TryPlayerContributeTreasury :449-475, CastleEconomyCampaignBehavior :1135-1158).
    /// Co ten skarbiec daje (dekompilacja BEE, DLL md5 267ba08d... = gra): projekty infrastruktury miasta (TickInfrastructureProject :2002 - zloto skarbca schodzi
    /// w nicosc, poziom projektu daje premie z niczego), zakupy wojskowe i gotowosc (TryRegisterMilitaryProcurement :2626), projekty i zaopatrzenie zamku
    /// (Castle TickProject :289, :1448), wyplata nadwyzki panu (TryPayTreasurySurplus :1953 - zamknieta kluczem pliku BEE TownTreasurySurplusPayoutFraction = 0).
    /// Wszystko z niczego albo w nicosc - wedlug projektu ("premie BEE tylko, jesli nie powstaja z niczego") premii BEE NIE dajemy: zostaje sam przelew
    /// i napis po angielsku, co daje.
    ///
    /// 1. PRAWDZIWA WPLATA: prefiks na BEE TryPlayerContributeTreasury (miasto i zamek; kazde wejscie - menu BEE, ekrany BK MilitaryVM / OverviewVM przez
    ///    BetterEconomyBridge, konsola BK) zamienia wplate do wirtualnego skarbca na przelew gracz -> kasa osady (GiveGoldAction.ApplyForCharacterToSettlement)
    ///    i pomija oryginal - stan BEE nietkniety (bez przerwy BEE, bez limitu skarbca BEE, bez "zablokowanego grantu"). Postfiks Can* (BeeSeal) przy czynnej
    ///    paczce liczy te same warunki co przelew (CanPay) - tylko gdy prefiks Try* tej osady jest wpiety (inaczej Can* otwarte puszczaloby zloto do skarbca BEE).
    ///    Opcja menu BEE wplaty - czynna, z wlasnym oknem kwot (te same kwoty co BEE: miasto 10/50/100 tys., zamek 5/15/30 tys.) i opisem po angielsku.
    ///    Warunek kasy: osada przyjmuje wplate tylko, gdy jej kasa jest prawdziwym pieniadzem (BesiegedPay.PurseReal - ta sama regula co R22): miasto - A1
    ///    (TownPurse.On; w tym drzewie zaslepka = false, wiec miasta zostaja zamkniete z powodem do scalenia z A1), zamek - 110 (CastlePurseEnabled i latka
    ///    regulatora). Inaczej regulator gry kasuje co dobe 25% nadwyzki kasy ponad cel (G DefaultSettlementEconomyModel.GetTownGoldChange :75-79) - wplata
    ///    w wiekszosci by zniknela. Pozostale warunki jak w BEE: osada rodu gracza, bez oblezenia i rabunku, gracz zywy i ma zloto.
    /// 2. DAR DLA NOTABLI: opcja "Give gifts to the notables" w menu BEE miasta i wsi (bee_town_menu, bee_village_menu, przed "Back"): kwota 1/5/20/50 tys.
    ///    dzielona miedzy notabli osady wedlug sily (Hero.Power; sila 0 u wszystkich - po rowno; reszta z zaokraglen - najsilniejszemu), przelew gracz -> notabl
    ///    (GiveGoldAction.ApplyBetweenCharacters). Relacja wedlug zwyklego daru gry = nadplata w barterze (G BarterManager.ApplyOverpayBonus :228-238):
    ///    BarterModel.CalculateOverpayRelationIncreaseCosts(notabl, jego czesc) i ChangeRelationAction.ApplyRelationChangeBetweenHeroes, gdy frakcja notabla nie
    ///    wojuje z graczem; przerwa jak po barterze (BarterManager.HandleHeroCooldown - BarterCooldownWithHeroInDays, w vanilla 3 dni, w zapisie gry) - notabl
    ///    w przerwie nie dostaje nic. Osada wroga - opcja nieaktywna z powodem. Dalej zloto notabla zyje wedlug istniejacych regul (pasmo 4 500-10 500 - E5/A3).
    /// Zamknieta ekonomia: oba przelewy maja platnika (gracz) i odbiorce (kasa osady / notabl) - zdarzenie gry HeroOrPartyTradedGold; ksiega pieniadza liczy
    /// wplate w "przelewach gry - gracz" kas miast/zamkow, dar - przelew miedzy bohaterami (suma swiata bez zmian). MoneyLedger bez nowych pozycji.
    /// Recenzja A5: wplata trafia zawsze do kasy WLASNEJ osady (CanPay), wiec to pieniadze rodu gracza - po przelewie idzie do zapasu "wlasnego"
    /// (CourtFull.OwnPlayer, ten sam co sprzet E5/A2 do wlasnej siedziby): zawor (zamek 110, miasto A1) oddaje ja panu jako "wlasne" D stalego, nie "ziemia".
    /// Bez tego wracalaby jako ziemia i zawyzala StableLand gracza - podstawe powinnosci wobec korony (165, CrownDuesFromLand) i limit Banku Zelaznego (168).
    /// Znana gorna granica (jak place budow BuildFunding i sprzet E5/A2): w wojnie ciecie Z8 zwrotu korony od zoldu zalog (SoldierPay.TakePaid) bierze
    /// caly zawor pana (LordDuesToday), takze zwrot tej wplaty - najwyzej zold zalog zamkow gracza od poprzedniego rozliczenia.
    /// Log: kazda wplata, odmowa przelewu (ekrany BK, konsola, menu) i dar - jedna linia "BEE (E5/A5) ..." ze zlotem gracza i odbiorcy przed i po (akcje gracza
    /// - rzadkie); stan paczki - dopisek linii kampanii BEE (170). Wylacznik wylaczony = stan sprzed paczki (wplata zamknieta jak w 170, brak opcji daru).
    /// LivingEconomySealed wylaczony = BEE jak dawniej (wplata do wirtualnego skarbca), dar dla notabli zostaje (to nie akcja BEE). Wyjatki: licznik potkniec
    /// (pierwszy z kazdego miejsca do logu bledow), bez globalnego wylacznika.
    /// </summary>
    internal static class BeeGifts
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BeePlayerGiftsReal; } }

        /// <summary>Prawdziwa wplata czynna: paczka i domkniecie 170 (bez 170 BEE jak dawniej - wirtualny skarbiec).</summary>
        internal static bool RealOn { get { var s = Settings.Current; return s != null && s.BeePlayerGiftsReal && s.LivingEconomySealed; } }

        private static readonly int[] TownAmounts = { 10000, 50000, 100000 };   // jak okno BEE (SettlementActionService :195)
        private static readonly int[] CastleAmounts = { 5000, 15000, 30000 };   // jak okno BEE (SettlementActionService :350)
        private static readonly int[] GiftAmounts = { 1000, 5000, 20000, 50000 };

        // teksty dla gracza (po angielsku, bez nawiasow klamrowych - ida do TextObject)
        private const string T_TOWN_TIP = "Real payment: your gold goes into this town's own purse - the coin its merchants trade with and from which the town's rent is paid. It conjures no prosperity, goods or readiness out of nothing.";
        private const string T_CASTLE_TIP = "Real payment: your gold goes into this castle's own purse - the coin it buys its food and stores with and from which its lord's dues are paid. It conjures no readiness, patrols or projects out of nothing.";
        private const string T_TOWN_TRIM = "Closed for now: the game still trims this town's purse every day, so most of what you paid in would vanish - this opens once town purses hold real coin (Town Purse Regulator).";
        private const string T_CASTLE_TRIM = "Closed for now: the game still trims this castle's purse every day, so most of what you paid in would vanish - this opens with Castle Purse Enabled.";
        private const string T_GIFT_TIP = "Your gold goes to the notables of this place, shared by their power. Each warms to you as after a generous barter - a few points at most, and the better they already like you, the more gold it takes - and can take your gift again after a few days.";
        private const string T_FAIL = "The payment could not be made.";

        private const string OptTown = "bee_town_notable_gift", OptVillage = "bee_village_notable_gift";

        private static MethodInfo _mCool;                    // BarterManager.HandleHeroCooldown (prywatna) - przerwa jak po barterze, w zapisie gry
        private static bool _coolLooked;
        private static readonly Dictionary<Hero, CampaignTime> _cool = new Dictionary<Hero, CampaignTime>();   // zapas, gdy metody gry brak (tylko sesja)
        private static int _payTaken, _giftAdded, _stumbles;   // opcje wplaty z naszym oknem kwot / opcje daru w menu BEE (linia kampanii); potkniecia sesji
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static int Day() { return (int)CampaignTime.Now.ToDays; }
        private static string Name(Settlement st) { return st == null ? "?" : (st.Name != null ? st.Name.ToString() : st.StringId); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("BeeGifts " + where, e); } catch { }
        }

        // ---------------------------------------------------------------- 1. prawdziwa wplata do kasy osady

        /// <summary>Kasa osady jest prawdziwym pieniadzem (BesiegedPay.PurseReal: miasto - A1, zamek - 110); inaczej powod po angielsku.</summary>
        internal static bool PurseOpen(Settlement st, out string why)
        {
            why = null;
            if (st != null && BesiegedPay.PurseReal(st)) return true;
            why = st != null && st.IsCastle ? T_CASTLE_TRIM : T_TOWN_TRIM;
            return false;
        }

        /// <summary>Warunki przelewu (te same dla menu, ekranow BK i konsoli): jak BEE (osada rodu gracza, bez oblezenia i rabunku, gracz zywy i ma zloto)
        /// plus kasa prawdziwym pieniadzem; bez przerwy i limitu wirtualnego skarbca BEE.</summary>
        internal static bool CanPay(Settlement st, Hero hero, int amount, bool castle, out string reason)
        {
            reason = null;
            string what = castle ? "castle" : "town";
            if (st == null || st.Town == null || (castle ? !st.IsCastle : !st.IsTown)) { reason = castle ? "Only castles have a castle purse." : "Only towns have a town purse."; return false; }
            if (st.OwnerClan != Clan.PlayerClan) { reason = "You can pay only into the purse of a " + what + " your clan owns."; return false; }
            if (hero == null || !hero.IsAlive) { reason = "You cannot pay right now."; return false; }
            if (st.IsUnderSiege || st.IsUnderRaid) { reason = "The " + what + " is under attack."; return false; }
            if (amount <= 0) { reason = "Nothing to pay."; return false; }
            if (!PurseOpen(st, out reason)) return false;
            if (hero.Gold < amount) { reason = "Not enough gold. Need " + amount + ", have " + hero.Gold + "."; return false; }
            return true;
        }

        /// <summary>Przelew gracz -> kasa osady i linia logu; zwraca zloto, ktore naprawde zeszlo z gracza.</summary>
        private static int Pay(Settlement st, Hero hero, int amount, string via)
        {
            int g0 = hero.Gold, p0 = st.SettlementComponent.Gold;
            GiveGoldAction.ApplyForCharacterToSettlement(hero, st, amount, true);
            int g1 = hero.Gold, p1 = st.SettlementComponent.Gold;
            // recenzja A5: pieniadze rodu w kasie wlasnej osady - do zapasu "wlasnego" (wraca zaworem jako wlasne D stalego, nie ziemia; opis klasy)
            int own = 0;
            try { int x = Math.Min(g0 - g1, p1 - p0); if (x > 0) own = CourtFull.OwnPlayer(st, hero.Clan, x); }
            catch (Exception e) { Stumble("zapas wlasny", e); }
            try
            {
                int paid = g0 - g1, got = p1 - p0;
                Log.Info("BEE (E5/A5) wplata: dzien " + Day() + " | " + Name(st) + " (" + (st.IsCastle ? "zamek" : "miasto") + ") | zrodlo " + via + " | kwota " + amount
                         + " | gracz " + g0 + " -> " + g1 + " (-" + paid + ") | kasa osady " + p0 + " -> " + p1 + " (+" + got + ")"
                         + (paid != got ? " | ROZNICA " + (paid - got) : "")
                         + " | do zapasu wlasnego " + own + (own == 0 && paid > 0 && !ClanIncomeBook.StableDOn ? " (D staly wylaczony)" : "")
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : ""));
                if (paid > 0)
                    Log.Player("You pay " + paid + " gold into the purse of " + Name(st) + ". " + (st.IsCastle ? T_CASTLE_TIP : T_TOWN_TIP));
                return paid;
            }
            catch (Exception e) { Stumble("linia wplaty", e); return g0 - g1; }
        }

        private static void Refused(Settlement st, int amount, string via, string why)
        {
            try
            {
                Log.Info("BEE (E5/A5) wplata odmowiona: dzien " + Day() + " | " + Name(st) + " (" + (st != null && st.IsCastle ? "zamek" : "miasto") + ") | zrodlo " + via
                         + " | kwota " + amount + " | powod: " + (why ?? "?"));
            }
            catch (Exception e) { Stumble("linia odmowy", e); }
        }

        /// <summary>Prefiks na BEE TryPlayerContributeTreasury (wolany z BeeSeal): przy czynnej paczce przelew do kasy osady zamiast wirtualnego skarbca,
        /// oryginal BEE pominiety zawsze (takze przy odmowie i wyjatku - inaczej zloto poszloby w nicosc). Paczka wylaczona - oryginal (z zamknieciem 170).</summary>
        internal static bool TryPre(bool castle, ref bool result, Settlement st, Hero hero, int amount, ref string reason)
        {
            if (!RealOn) return true;
            try
            {
                string why;
                if (!CanPay(st, hero, amount, castle, out why)) { result = false; reason = why; Refused(st, amount, "BEE/BK", why); return false; }
                int paid = Pay(st, hero, amount, "BEE/BK");
                result = paid > 0;
                reason = paid > 0 ? null : T_FAIL;
            }
            catch (Exception e) { Stumble("wplata BEE/BK", e); result = false; reason = T_FAIL; }
            return false;
        }

        /// <summary>Postfiks Can* (wolany z BeeSeal przy czynnej paczce i wpietym prefiksie Try* tej osady): wynik = warunki przelewu.</summary>
        internal static void CanPost(bool castle, ref bool result, ref string reason, Settlement st, Hero hero, int amount)
        {
            try { string why; result = CanPay(st, hero, amount, castle, out why); reason = why; }
            catch (Exception e) { Stumble("Can*", e); result = false; reason = T_FAIL; }
        }

        /// <summary>Owijka warunku opcji wplaty (BeeSeal.MenuGate przy czynnej paczce): widocznosc wedlug BEE (osada rodu gracza), czynna przy kasie
        /// prawdziwym pieniadzem, podpowiedz - co daje albo dlaczego zamknieta.</summary>
        internal static bool PayCondition(MenuCallbackArgs args, bool vis, bool castle)
        {
            if (!vis) return false;
            try
            {
                string why;
                bool open = PurseOpen(Settlement.CurrentSettlement, out why);
                if (args != null) { args.IsEnabled = open; args.Tooltip = new TextObject(open ? (castle ? T_CASTLE_TIP : T_TOWN_TIP) : why); }
            }
            catch (Exception e) { Stumble("warunek wplaty", e); }
            return true;
        }

        /// <summary>Okno kwot wplaty (zamiast okna BEE z opisem wirtualnego skarbca).</summary>
        private static void ShowPayPicker(Settlement st, bool castle)
        {
            var me = Hero.MainHero;
            if (st == null || me == null) return;
            string closed;
            if (!PurseOpen(st, out closed)) { Log.Player(closed, true); return; }
            string tip = castle ? T_CASTLE_TIP : T_TOWN_TIP;
            var opts = new List<InquiryElement>();
            foreach (int amt in castle ? CastleAmounts : TownAmounts)
            {
                string why;
                bool ok = CanPay(st, me, amt, castle, out why);
                opts.Add(new InquiryElement(amt, "Pay " + amt + " gold" + (ok ? "" : " - " + why), null, ok, ok ? tip : why));
            }
            string body = "Pay into the purse of " + Name(st) + ".\n\n" + tip + "\n\nYour gold: " + me.Gold + "\n" + (castle ? "Castle" : "Town") + " purse: " + st.SettlementComponent.Gold;
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                (castle ? "Castle purse - " : "Town purse - ") + Name(st), body, opts, true, 1, 1, "Pay", GameTexts.FindText("str_cancel").ToString(),
                delegate (List<InquiryElement> sel)
                {
                    try
                    {
                        if (sel == null || sel.Count == 0) return;
                        int amt = (int)sel[0].Identifier;
                        var h = Hero.MainHero;
                        string why;
                        if (!CanPay(st, h, amt, castle, out why)) { Refused(st, amt, "menu", why); Log.Player(why, true); return; }
                        Pay(st, h, amt, "menu");
                    }
                    catch (Exception e) { Stumble("wplata z menu", e); }
                },
                delegate (List<InquiryElement> _) { }), true);
        }

        /// <summary>Skutek opcji wplaty BEE: przy czynnej paczce nasze okno kwot, inaczej oryginal BEE (okno wirtualnego skarbca - zamkniete przez 170).</summary>
        private sealed class PayAct
        {
            private readonly GameMenuOption.OnConsequenceDelegate _orig;
            private readonly bool _castle;
            internal PayAct(GameMenuOption.OnConsequenceDelegate orig, bool castle) { _orig = orig; _castle = castle; }

            internal void Run(MenuCallbackArgs args)
            {
                if (!RealOn) { if (_orig != null) _orig(args); return; }
                try { ShowPayPicker(Settlement.CurrentSettlement, _castle); }
                catch (Exception e) { Stumble("okno wplaty", e); }
            }
        }

        // ---------------------------------------------------------------- 2. dar dla notabli

        private static bool Hostile(Settlement st)
        {
            var me = Hero.MainHero;
            IFaction mine = me != null ? me.MapFaction : null, theirs = st != null ? st.MapFaction : null;
            return mine != null && theirs != null && theirs.IsAtWarWith(mine);
        }

        /// <summary>Notabl moze dostac dar: bez przerwy po barterze / poprzednim darze.</summary>
        private static bool CoolOk(Hero h)
        {
            try
            {
                var bm = BarterManager.Instance;
                if (bm != null && !bm.CanPlayerBarterWithHero(h)) return false;
            }
            catch (Exception e) { Stumble("przerwa barteru", e); }
            CampaignTime t;
            return !_cool.TryGetValue(h, out t) || t.IsPast;
        }

        /// <summary>Przerwa jak po barterze (metoda gry, zapis gry); bez niej - zapas na czas sesji.</summary>
        private static void SetCool(Hero h)
        {
            try
            {
                if (!_coolLooked) { _coolLooked = true; _mCool = AccessTools.Method(typeof(BarterManager), "HandleHeroCooldown", new[] { typeof(Hero) }); }
                var bm = BarterManager.Instance;
                if (_mCool != null && bm != null) { _mCool.Invoke(bm, new object[] { h }); return; }
            }
            catch (Exception e) { Stumble("przerwa barteru", e); }
            int days = 3;
            try { days = Campaign.Current.Models.BarterModel.BarterCooldownWithHeroInDays; } catch { }
            _cool[h] = CampaignTime.DaysFromNow(days);
        }

        /// <summary>Notable osady, ktorym mozna dac dar (zywi, nie z rodu gracza, bez przerwy) - do listy `into`; `all` = wszyscy bez warunku przerwy.</summary>
        private static int Recipients(Settlement st, List<Hero> into, out int all)
        {
            all = 0;
            int ready = 0;
            var ns = st != null ? st.Notables : null;
            if (ns == null) return 0;
            foreach (var h in ns)
            {
                if (h == null || !h.IsAlive || !h.IsNotable || h == Hero.MainHero || (h.Clan != null && h.Clan == Clan.PlayerClan)) continue;
                all++;
                if (!CoolOk(h)) continue;
                ready++;
                if (into != null) into.Add(h);
            }
            return ready;
        }

        /// <summary>Powod, dla ktorego dar `amount` w tej osadzie jest niemozliwy (null = mozna).</summary>
        private static string GiftBlocked(Settlement st, int amount)
        {
            var me = Hero.MainHero;
            if (me == null || !me.IsAlive) return "You cannot give gifts right now.";
            if (Hostile(st)) return "You cannot send gifts to the notables of a place at war with you.";
            int all;
            int ready = Recipients(st, null, out all);
            if (all == 0) return "There are no notables here to receive a gift.";
            if (ready == 0) return "The notables here received your gifts only lately - wait a few days.";
            if (me.Gold < amount) return "Not enough gold. You need at least " + amount + ".";
            return null;
        }

        private static bool GiftCondition(MenuCallbackArgs a, bool village)
        {
            try
            {
                if (!On) return false;
                var st = Settlement.CurrentSettlement;
                if (st == null || (village ? !st.IsVillage : !st.IsTown)) return false;
                string why = GiftBlocked(st, GiftAmounts[0]);
                if (a != null)
                {
                    a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    a.IsEnabled = why == null;
                    a.Tooltip = new TextObject(why ?? T_GIFT_TIP);
                }
                return true;
            }
            catch (Exception e) { Stumble("warunek daru", e); return false; }
        }

        private static void GiftConsequence()
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                var me = Hero.MainHero;
                if (st == null || me == null) return;
                string why = GiftBlocked(st, GiftAmounts[0]);
                if (why != null) { Log.Player(why, true); return; }
                var list = new List<Hero>();
                int all;
                Recipients(st, list, out all);
                var sb = new StringBuilder("Your gold will be shared among the notables of " + Name(st) + " by their power:\n");
                foreach (var h in list)
                    sb.Append("\n").Append(h.Name != null ? h.Name.ToString() : h.StringId).Append(" - power ").Append((int)h.Power).Append(", relation ").Append((int)h.GetRelationWithPlayer());
                if (all > list.Count) sb.Append("\n\n").Append(all - list.Count).Append(all - list.Count == 1 ? " other notable" : " other notables").Append(" received your gifts only lately and get nothing this time.");
                sb.Append("\n\nEach warms to you as after a generous barter: a few points of relation at most, and the better they already like you, the more gold it takes. "
                          + "The gold is theirs - real coin that stays in this land; nothing is conjured.\n\nYour gold: ").Append(me.Gold);
                var opts = new List<InquiryElement>();
                foreach (int amt in GiftAmounts)
                {
                    bool ok = me.Gold >= amt;
                    opts.Add(new InquiryElement(amt, "Give " + amt + " gold" + (ok ? "" : " - not enough gold"), null, ok,
                                                ok ? "Shared by power among " + list.Count + (list.Count == 1 ? " notable." : " notables.") : "You have " + me.Gold + " gold."));
                }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Gifts to the notables - " + Name(st), sb.ToString(), opts, true, 1, 1, "Give", GameTexts.FindText("str_cancel").ToString(),
                    delegate (List<InquiryElement> sel)
                    {
                        try { if (sel != null && sel.Count > 0) Give(st, (int)sel[0].Identifier); }
                        catch (Exception e) { Stumble("dar", e); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Stumble("okno daru", e); }
        }

        /// <summary>Dar: czesci wedlug sily, przelew gracz -> notabl, relacja jak nadplata w barterze, przerwa jak po barterze, linia logu.</summary>
        private static void Give(Settlement st, int amount)
        {
            var me = Hero.MainHero;
            string why = GiftBlocked(st, amount);
            if (why != null) { Log.Player(why, true); return; }
            var list = new List<Hero>();
            int all;
            Recipients(st, list, out all);
            if (list.Count == 0) return;
            amount = Math.Min(amount, me.Gold);
            // czesci wedlug sily; sila 0 u wszystkich - po rowno; reszta z zaokraglen - najsilniejszemu (suma czesci = kwota)
            double sum = 0;
            foreach (var h in list) sum += Math.Max(0f, h.Power);
            var share = new int[list.Count];
            int given = 0, top = 0;
            for (int i = 0; i < list.Count; i++)
            {
                double w = sum > 0 ? Math.Max(0f, list[i].Power) / sum : 1.0 / list.Count;
                share[i] = (int)Math.Floor(amount * w);
                given += share[i];
                if (list[i].Power > list[top].Power) top = i;
            }
            share[top] += amount - given;
            IFaction mine = me.MapFaction;
            int g0 = me.Gold, n = 0, relSum = 0;
            long gotSum = 0;
            var det = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i];
                if (share[i] <= 0) continue;
                int h0 = h.Gold, r0 = (int)h.GetRelationWithPlayer();
                GiveGoldAction.ApplyBetweenCharacters(me, h, share[i], true);
                int got = h.Gold - h0;
                gotSum += got; n++;
                try
                {
                    var hf = h.MapFaction;
                    if (got > 0 && !(hf != null && mine != null && hf.IsAtWarWith(mine)))
                    {
                        int rel = Campaign.Current.Models.BarterModel.CalculateOverpayRelationIncreaseCosts(h, got);
                        if (rel > 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(me, h, rel);
                    }
                }
                catch (Exception e) { Stumble("relacja daru", e); }
                SetCool(h);
                try
                {
                    int r1 = (int)h.GetRelationWithPlayer();
                    relSum += r1 - r0;
                    det.Append(det.Length > 0 ? "; " : "").Append(h.Name != null ? h.Name.ToString() : h.StringId).Append(": sila ").Append((int)h.Power)
                       .Append(", +").Append(got).Append(" zl, relacja ").Append(r0).Append(" -> ").Append(r1);
                }
                catch (Exception e) { Stumble("linia daru", e); }
            }
            try
            {
                int g1 = me.Gold;
                Log.Info("BEE (E5/A5) dar: dzien " + Day() + " | " + Name(st) + " (" + (st.IsVillage ? "wies" : "miasto") + ") | kwota " + amount
                         + " | notabli " + n + " (w przerwie po darze " + (all - list.Count) + ") | gracz " + g0 + " -> " + g1 + " (-" + (g0 - g1) + ") | notable razem +" + gotSum
                         + (g0 - g1 != gotSum ? " | ROZNICA " + (g0 - g1 - gotSum) : "") + " | relacja razem +" + relSum + " | " + det
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : ""));
                if (g0 - g1 > 0)
                    Log.Player("You give " + (g0 - g1) + " gold to " + n + (n == 1 ? " notable" : " notables") + " of " + Name(st) + ".");
            }
            catch (Exception e) { Stumble("linia daru", e); }
        }

        // ---------------------------------------------------------------- menu BEE (start albo wczytanie kampanii)

        private static GameMenuOption Find(GameMenu menu, string id)
        {
            if (menu == null) return null;
            foreach (var o in menu.MenuOptions) if (o != null && o.IdString == id) return o;
            return null;
        }

        /// <summary>Wolane z BeeSeal.SessionPost (postfiks na BEE OnSessionLaunched - menu BEE juz dodane): nasze okno kwot przy opcjach wplaty
        /// i opcje daru w menu BEE miasta i wsi (przed "Back"). Ochrona przed podwojnym owinieciem i dodaniem.</summary>
        internal static void OnSession(CampaignGameStarter starter, GameMenuManager mgr)
        {
            _cool.Clear();
            _payTaken = _giftAdded = _stumbles = 0;
            if (mgr == null) return;
            TakePayOption(mgr, "bee_town_menu", "bee_town_treasury_contribute", false);
            TakePayOption(mgr, "bee_castle_menu", "bee_castle_treasury_contribute", true);
            if (starter == null) return;
            AddGiftOption(starter, mgr, "bee_town_menu", "bee_town_back", OptTown, false);
            AddGiftOption(starter, mgr, "bee_village_menu", "bee_village_back", OptVillage, true);
        }

        private static void TakePayOption(GameMenuManager mgr, string menuId, string optId, bool castle)
        {
            try
            {
                var opt = Find(mgr.GetGameMenu(menuId), optId);
                if (opt == null) return;
                if (!(opt.OnConsequence != null && opt.OnConsequence.Target is PayAct))
                    opt.OnConsequence = new GameMenuOption.OnConsequenceDelegate(new PayAct(opt.OnConsequence, castle).Run);
                _payTaken++;
            }
            catch (Exception e) { Stumble("menu wplaty " + optId, e); }
        }

        private static void AddGiftOption(CampaignGameStarter starter, GameMenuManager mgr, string menuId, string backId, string optId, bool village)
        {
            try
            {
                var menu = mgr.GetGameMenu(menuId);
                if (menu == null) return;
                int idx = -1, i = 0;
                foreach (var o in menu.MenuOptions)
                {
                    if (o != null && o.IdString == optId) { _giftAdded++; return; }   // juz jest (drugie OnSessionLaunched tej kampanii)
                    if (o != null && o.IdString == backId && idx < 0) idx = i;
                    i++;
                }
                bool v = village;
                starter.AddGameMenuOption(menuId, optId, "{=!}Give gifts to the notables",
                    delegate (MenuCallbackArgs a) { return GiftCondition(a, v); },
                    delegate (MenuCallbackArgs a) { GiftConsequence(); }, false, idx);
                _giftAdded++;
            }
            catch (Exception e) { Stumble("menu daru " + optId, e); }
        }

        /// <summary>Dopisek linii kampanii BEE (170): stan paczki, wpiecia, kasy prawdziwym pieniadzem, opcje menu.</summary>
        internal static string CampaignText(bool payTown, bool payCastle)
        {
            var sb = new StringBuilder("E5/A5 (prawdziwa wplata i dar dla notabli): wylacznik ");
            try
            {
                sb.Append(On ? "wlaczony" : "wylaczony");
                sb.Append("; prawdziwa wplata: ").Append(RealOn ? "czynna" : (On ? "nieczynna (Living Economy Sealed wylaczony - BEE jak dawniej)" : "nieczynna (wplata zamknieta jak w 170)"));
                sb.Append("; prefiksy Try*: miasto ").Append(payTown ? "tak" : "NIE").Append(", zamek ").Append(payCastle ? "tak" : "NIE");
                Settlement castle = null;
                foreach (var x in Settlement.All) if (x != null && x.IsCastle) { castle = x; break; }
                sb.Append("; kasa prawdziwym pieniadzem: miasta (A1) ").Append(TownPurse.On ? "tak" : "nie - wplata do miast zamknieta z powodem")
                  .Append(", zamki (110) ").Append(castle != null && BesiegedPay.PurseReal(castle) ? "tak" : "nie - wplata do zamkow zamknieta z powodem");
                sb.Append("; okno kwot przy opcjach wplaty ").Append(_payTaken).Append("/2; opcje daru dla notabli ").Append(_giftAdded).Append("/2 (bee_town_menu, bee_village_menu)");
                if (!_coolLooked) { _coolLooked = true; _mCool = AccessTools.Method(typeof(BarterManager), "HandleHeroCooldown", new[] { typeof(Hero) }); }
                sb.Append("; przerwa po darze: ").Append(_mCool != null ? "BarterManager.HandleHeroCooldown (jak po barterze, w zapisie)" : "BRAK metody gry - zapas na czas sesji");
                if (_stumbles > 0) sb.Append("; potkniecia ").Append(_stumbles);
            }
            catch (Exception e) { Stumble("linia kampanii", e); sb.Append(" (blad odczytu)"); }
            return sb.ToString();
        }
    }
}
