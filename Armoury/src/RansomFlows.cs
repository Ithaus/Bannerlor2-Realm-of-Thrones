using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// OKUPY GRACZA I KURIER OKUPU. Dwa przeplywy gry bez prawdziwej drugiej strony (audyt 04 W3, L4):
    ///  - okup gracza z menu niewoli (PlayerCaptivityCampaignBehavior.game_menu_captivity_end_by_ransom_on_consequence): gra zdejmuje kwote
    ///    z kiesy gracza GiveGoldAction(gracz, nikt) - pieniadz znika, porywacz nic nie dostaje;
    ///  - kurier z oferta okupu (RansomOfferCampaignBehavior.AcceptRansomOffer): gdy placacy AI ma mniej niz cena + 1000, gra USTAWIA mu
    ///    kiese na cena + 1000 (dosypka z niczego), potem placi cene.
    /// 169c: prefiksy licza do linii "Niewola lordow i okupy (169c)".
    /// 2.14 (projekt etapu 2, rozdz. "2.14"; zasada 2.0b - nic z niczego, nic w nicosc, zapasowy odbiorca):
    ///  - `PlayerRansomToCaptor`: ta sama kwota idzie do porywacza - wodz partii porywacza; partia bez wodza - glowa jej rodu; banda - kasa
    ///    jej kryjowki; loch osady - pan osady (kryjowka - jej kasa); zapasowy odbiorca: glowa rodu porywacza, potem kasa najblizszego miasta;
    ///    licznik "bez odbiorcy" (prog 0). Prefiks robi to, co oryginal (przelew + EndCaptivityAction.ApplyByRansom), i pomija go - tylko gdy
    ///    przelew sie udal; inaczej oryginal jak dotad (w nicosc, liczone).
    ///  - `RansomCourierNoTopUp`: placacy AI ma cene, ale mniej niz cena + 1000 - oryginal biegnie, postfiks zdejmuje mu dosypke gry (placi
    ///    z wlasnej kiesy); nie ma ceny - oferta przepada jak po "Decline" (DeclineRansomOffer gry), jeniec zostaje. Gracz placacy - bez zmian.
    ///  Kwoty bez zmian (do 178). Harness w autotescie (CrashScribe.Autotest.Active, `RansomHarnessInAutotest`): bez prawdziwej niewoli gracza.
    /// Kazde cialo w try; bledy liczone.
    /// </summary>
    internal static class RansomFlows
    {
        // liczniki doby linii 169c (zerowane w Measure169c.CaptivityLine) i sesji
        internal static long DayPlayerPaid, DayPlayerToNothing, DayCourierIn, DayCourierOut, DayTopUp;
        internal static int DayPlayerN, DayCourierN, DayTopUpN;
        internal static long SessPlayerPaid, SessTopUp;
        internal static int Stumbles;
        // liczniki doby linii "Okupy (2.14)" (zerowane w Daily)
        private static long _dToCaptor, _dToNothing, _dTopUpBlocked, _dTopUpGiven, _dLapsedGold, _dNoRecipientGold;
        private static int _dToCaptorN, _dTopUpBlockedN, _dLapsedN, _dNoRecipientN;
        private static readonly int[] _dRoute = new int[6];
        private static readonly string[] RouteName = { "wodz partii", "glowa rodu partii bez wodza", "kasa kryjowki bandy", "pan osady (loch)", "zapasowy: glowa rodu porywacza", "zapasowy: kasa najblizszego miasta" };

        private static FieldInfo _fHero, _fPayer;
        private static MethodInfo _mDecline;
        private static bool _menuWired, _courierWired;

        internal static void ZeroDay() { DayPlayerPaid = DayPlayerToNothing = DayCourierIn = DayCourierOut = DayTopUp = 0; DayPlayerN = DayCourierN = DayTopUpN = 0; }
        private static void Zero214() { _dToCaptor = _dToNothing = _dTopUpBlocked = _dTopUpGiven = _dLapsedGold = _dNoRecipientGold = 0; _dToCaptorN = _dTopUpBlockedN = _dLapsedN = _dNoRecipientN = 0; Array.Clear(_dRoute, 0, _dRoute.Length); }
        internal static void Reset() { ZeroDay(); Zero214(); SessPlayerPaid = SessTopUp = 0; Stumbles = 0; _sessDay = 0; _harnessDone = 0; _harnessAuto = null; }

        internal static string Wired { get { return "menu okupu gracza " + (_menuWired ? "wpiete" : "BRAK") + ", kurier okupu " + (_courierWired ? "wpiety" : "BRAK"); } }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        internal static string Describe(PartyBase p)
        {
            try
            {
                if (p == null) return "-";
                if (p.IsSettlement && p.Settlement != null) return "loch " + p.Settlement.Name + " (pan " + Name(p.Settlement.OwnerClan != null ? p.Settlement.OwnerClan.Leader : null) + ")";
                var mp = p.MobileParty;
                if (mp == null) return "?";
                return "partia " + mp.Name + (mp.LeaderHero != null ? " (wodz " + Name(mp.LeaderHero) + ")" : " (bez wodza)") + (mp.IsBandit ? " - banda" : "");
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ 2.14: odbiorca okupu gracza
        private static bool Live(Hero h) { return h != null && h.IsAlive && h != Hero.MainHero; }

        /// <summary>Kto dostaje okup gracza (projekt 2.14 + zapasowy odbiorca 2.0b): bohater albo kasa osady; route - indeks drogi.</summary>
        internal static void ResolveRecipient(PartyBase captor, out Hero hero, out Settlement purse, out int route)
        {
            hero = null; purse = null; route = -1;
            Clan clan = null;
            if (captor != null)
            {
                var mp = captor.MobileParty;
                if (captor.IsMobile && mp != null)
                {
                    clan = mp.ActualClan;
                    if (Live(mp.LeaderHero)) { hero = mp.LeaderHero; route = 0; return; }
                    var hide = mp.IsBandit && mp.BanditPartyComponent != null && mp.BanditPartyComponent.Hideout != null ? mp.BanditPartyComponent.Hideout.Settlement : null;
                    if (hide != null && hide.SettlementComponent != null) { purse = hide; route = 2; return; }
                    if (clan != null && !clan.IsBanditFaction && Live(clan.Leader)) { hero = clan.Leader; route = 1; return; }
                }
                else if (captor.IsSettlement && captor.Settlement != null)
                {
                    var st = captor.Settlement;
                    if (st.IsHideout && st.SettlementComponent != null) { purse = st; route = 2; return; }
                    clan = st.OwnerClan;
                    if (clan != null && Live(clan.Leader)) { hero = clan.Leader; route = 3; return; }
                }
            }
            // zapasowy odbiorca (2.0b): glowa rodu porywacza (rod bandy - nie), potem kasa najblizszego miasta
            if (clan != null && !clan.IsBanditFaction && Live(clan.Leader)) { hero = clan.Leader; route = 4; return; }
            Settlement best = null; float bd = float.MaxValue;
            try
            {
                var pos = captor != null && captor.IsMobile && captor.MobileParty != null ? captor.MobileParty.Position.ToVec2()
                        : captor != null && captor.IsSettlement && captor.Settlement != null ? captor.Settlement.GetPosition2D
                        : (MobileParty.MainParty != null ? MobileParty.MainParty.Position.ToVec2() : default(TaleWorlds.Library.Vec2));
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown || st.Town == null) continue;
                    float d = st.GetPosition2D.DistanceSquared(pos);
                    if (d < bd) { bd = d; best = st; }
                }
            }
            catch { best = null; }
            if (best != null) { purse = best; route = 5; }
        }

        /// <summary>Okup gracza do porywacza: przelew (zdarzenie gry - ksiega obiegu go widzi). true - przelew zrobiony.</summary>
        internal static bool PayPlayerRansom(int amount, PartyBase captor, string why, out string to)
        {
            to = "-";
            var me = Hero.MainHero;
            if (me == null || amount <= 0) return false;
            Hero h; Settlement st; int route;
            ResolveRecipient(captor, out h, out st, out route);
            if (h != null) { GiveGoldAction.ApplyBetweenCharacters(me, h, amount); to = Name(h) + " (" + RouteName[route] + ")"; }
            else if (st != null) { GiveGoldAction.ApplyForCharacterToSettlement(me, st, amount); to = "kasa " + st.Name + " (" + RouteName[route] + ")"; }
            else { _dNoRecipientN++; _dNoRecipientGold += amount; return false; }
            _dToCaptor += amount; _dToCaptorN++; _dRoute[route]++;
            Log.Info("Okupy (2.14): okup gracza " + amount + " zl (" + why + ") -> " + to + "; porywacz: " + Describe(captor) + "; w nicosc 0.");
            return true;
        }

        // ------------------------------------------------------------ okup gracza z menu niewoli
        public static bool MenuRansomPrefix()
        {
            int amt = 0;
            PartyBase captor = null;
            try
            {
                var c = Campaign.Current;
                if (c == null || c.PlayerCaptivity == null) return true;
                amt = c.PlayerCaptivity.CurrentRansomAmount;
                if (amt <= 0) return true;
                captor = PlayerCaptivity.CaptorParty;
                DayPlayerN++; DayPlayerPaid += amt; SessPlayerPaid += amt;
                Log.Info("Okup gracza (169c): " + amt + " zl z kiesy gracza (ma " + (Hero.MainHero != null ? Hero.MainHero.Gold : 0) + "), porywacz: " + Describe(captor) + ".");
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.MenuRansomPrefix", e); return true; }
            var s = Settings.Current;
            bool paid = false;
            if (s != null && s.PlayerRansomToCaptor)
            {
                try { string to; paid = PayPlayerRansom(amt, captor, "menu niewoli", out to); }
                catch (Exception e) { Stumbles++; Log.Error("RansomFlows.PayPlayerRansom", e); paid = false; }
            }
            if (!paid)
            {
                DayPlayerToNothing += amt; _dToNothing += amt;   // oryginal gry: kwota do nikogo (wylacznik albo brak odbiorcy)
                return true;
            }
            // to samo, co dalej robi oryginal (po przelewie do nikogo) - wyjatek idzie do gry jak w oryginale
            Hero leaderHero = captor != null ? captor.LeaderHero : null;
            EndCaptivityAction.ApplyByRansom(Hero.MainHero, leaderHero);
            return false;
        }

        // ------------------------------------------------------------ kurier z oferta okupu
        internal sealed class TopUpState { public Hero Payer; public int Take; }

        /// <summary>Decyzja kuriera wobec placacego AI (2.14): 0 - jak w grze (stac go z zapasem 1000), 1 - placi z wlasnej kiesy bez dosypki
        /// (topUp = dosypka gry do zdjecia), 2 - oferta przepada (nie ma ceny).</summary>
        internal static int CourierDecide(int payerGold, int price, out int topUp)
        {
            topUp = 0;
            if ((long)payerGold >= (long)price + 1000) return 0;
            topUp = (int)Math.Min(int.MaxValue, (long)price + 1000 - payerGold);
            return payerGold >= price ? 1 : 2;
        }

        public static bool AcceptPrefix(object __instance, int __0, out TopUpState __state)
        {
            __state = null;
            try
            {
                if (__instance == null || _fHero == null || _fPayer == null) return true;
                var hero = _fHero.GetValue(__instance) as Hero;
                var payer = _fPayer.GetValue(__instance) as Hero;
                if (hero == null || payer == null) return true;
                int price = __0;
                DayCourierN++;
                if (payer == Hero.MainHero)
                {
                    DayCourierOut += price;
                    Log.Info("Kurier okupu (169c): " + Name(hero) + " - gracz wykupuje czlonka rodu za " + price + " zl.");
                    return true;
                }
                DayCourierIn += price;
                int topUp;
                int dec = CourierDecide(payer.Gold, price, out topUp);
                var s = Settings.Current;
                bool block = s != null && s.RansomCourierNoTopUp;
                string clan = payer.Clan != null ? payer.Clan.Name.ToString() : "-";
                if (dec == 0)
                {
                    Log.Info("Kurier okupu (169c): " + Name(hero) + " - placi " + Name(payer) + " (rod " + clan + ") " + price + " zl graczowi z wlasnej kiesy (ma " + payer.Gold + ").");
                    return true;
                }
                if (!block)
                {
                    DayTopUp += topUp; DayTopUpN++; SessTopUp += topUp; _dTopUpGiven += topUp;
                    Log.Info("Kurier okupu (169c): " + Name(hero) + " - placi " + Name(payer) + " (rod " + clan + ") " + price + " zl graczowi; ma " + payer.Gold
                             + " < cena + 1000 - gra dosypie mu " + topUp + " zl z niczego (RansomCourierNoTopUp wylaczone).");
                    return true;
                }
                if (dec == 1)
                {
                    __state = new TopUpState { Payer = payer, Take = topUp };
                    _dTopUpBlocked += topUp; _dTopUpBlockedN++;
                    Log.Info("Okupy (2.14): kurier - " + Name(hero) + ": " + Name(payer) + " (rod " + clan + ") placi " + price + " zl graczowi z wlasnej kiesy (ma " + payer.Gold
                             + "); dosypka gry " + topUp + " zl zdjeta po wyplacie; w nicosc 0.");
                    return true;
                }
                // dec == 2: placacy nie ma ceny - oferta przepada jak po odmowie gracza, jeniec zostaje
                _dLapsedN++; _dLapsedGold += price;
                Log.Info("Okupy (2.14): kurier - " + Name(hero) + ": " + Name(payer) + " (rod " + clan + ") ma " + payer.Gold + " < cena " + price
                         + " - oferta przepada, jeniec zostaje u gracza (gra dosypalaby " + topUp + " zl z niczego); w nicosc 0.");
                if (_mDecline != null) _mDecline.Invoke(__instance, null);
                Log.Player("The courier's masters could not raise the " + price + " denars they offered for " + Name(hero) + ". The offer lapses and " + Name(hero) + " stays your prisoner.", true);
                return false;
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.AcceptPrefix", e); __state = null; return true; }
        }

        public static void AcceptPostfix(TopUpState __state)
        {
            try
            {
                if (__state == null || __state.Payer == null || __state.Take <= 0) return;
                int take = Math.Min(__state.Take, Math.Max(0, __state.Payer.Gold));
                if (take > 0) __state.Payer.ChangeHeroGold(-take);   // dosypka gry (Gold = cena + 1000, bez zdarzenia) wraca w nicosc - placacy zaplacil z wlasnego
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.AcceptPostfix", e); }
        }

        /// <summary>Z Measure169c.EnsureHooks (kampania istnieje): latki na obu metodach gry, raz na proces.</summary>
        internal static void Hook(Harmony h)
        {
            if (h == null) return;
            try
            {
                if (!_menuWired)
                {
                    var m = AccessTools.Method(typeof(PlayerCaptivityCampaignBehavior), "game_menu_captivity_end_by_ransom_on_consequence");
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(RansomFlows), nameof(MenuRansomPrefix)) { priority = Priority.First }); _menuWired = true; }
                }
            }
            catch (Exception e) { Log.Error("RansomFlows.Hook(menu)", e); }
            try
            {
                if (!_courierWired)
                {
                    _fHero = AccessTools.Field(typeof(RansomOfferCampaignBehavior), "_currentRansomHero");
                    _fPayer = AccessTools.Field(typeof(RansomOfferCampaignBehavior), "_currentRansomPayer");
                    _mDecline = AccessTools.Method(typeof(RansomOfferCampaignBehavior), "DeclineRansomOffer");
                    var m = AccessTools.Method(typeof(RansomOfferCampaignBehavior), "AcceptRansomOffer", new[] { typeof(int) });
                    if (m != null && _fHero != null && _fPayer != null && _mDecline != null)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(RansomFlows), nameof(AcceptPrefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(RansomFlows), nameof(AcceptPostfix)) { priority = Priority.Last });
                        _courierWired = true;
                    }
                }
            }
            catch (Exception e) { Log.Error("RansomFlows.Hook(kurier)", e); }
        }

        // ------------------------------------------------------------ raz na dobe: linia "Okupy (2.14)" i harness w autotescie
        private static int _sessDay, _harnessDone;
        private static FieldInfo _harnessAuto;
        private static bool _harnessLooked;

        internal static void Daily()
        {
            _sessDay++;
            try { Harness(); } catch (Exception e) { Stumbles++; Log.Error("RansomFlows.Harness", e); }
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled) return;
                var routes = new System.Collections.Generic.List<string>();
                for (int i = 0; i < _dRoute.Length; i++) if (_dRoute[i] > 0) routes.Add(RouteName[i] + " " + _dRoute[i]);
                Log.Info("Okupy (2.14): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " | okup gracza do porywacza " + _dToCaptor + " zl (" + _dToCaptorN + "; drogi: " + (routes.Count > 0 ? string.Join(", ", routes.ToArray()) : "-")
                         + "), bez odbiorcy " + _dNoRecipientGold + " (" + _dNoRecipientN + ") | kurier: dosypka gry zdjeta " + _dTopUpBlocked + " zl (" + _dTopUpBlockedN + " ofert), oferty przepadle z braku zlota placacego "
                         + _dLapsedN + " (na " + _dLapsedGold + " zl) | w nicosc " + (_dToNothing + _dTopUpGiven) + " (okup gracza " + _dToNothing + ", dosypka gry przy wylaczonym RansomCourierNoTopUp " + _dTopUpGiven + ")"
                         + " | wylaczniki: PlayerRansomToCaptor " + (s.PlayerRansomToCaptor ? "tak" : "nie") + ", RansomCourierNoTopUp " + (s.RansomCourierNoTopUp ? "tak" : "nie") + "; " + Wired
                         + (Stumbles > 0 ? " | potkniecia " + Stumbles : "") + ".");
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.Daily", e); }
            finally { Zero214(); }
        }

        private static bool AutotestActive()
        {
            try
            {
                if (!_harnessLooked)
                {
                    _harnessLooked = true;
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "CrashScribe") continue;
                        Type t = asm.GetType("CrashScribe.Autotest", false);
                        var f = t != null ? t.GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                        if (f != null && f.FieldType == typeof(bool)) _harnessAuto = f;
                        break;
                    }
                }
                return _harnessAuto != null && (bool)_harnessAuto.GetValue(null);
            }
            catch { return false; }
        }

        /// <summary>
        /// Harness niewoli (projekt 2.14, K29) - tylko w autotescie i przy `RansomHarnessInAutotest`. BEZ prawdziwej niewoli gracza: TakePrisonerAction
        /// na glownym bohaterze otwiera menu niewoli i okna RC (slowo honoru - InformationManager.ShowInquiry z pauza), na ktore autotest nie ma kto odpowiedziec.
        /// Doba sesji 5: ta sama funkcja co menu okupu (PayPlayerRansom) placi prawdziwie min(1000, kiesa gracza) wodzowi partii lorda AI - kiesy w parze,
        /// 0 zl w nicosc; odbiorcy na sucho dla lochu miasta i dla bandy. Doba sesji 8: decyzja kuriera (CourierDecide) dla placacego bogatego i biednego
        /// (na sucho - bez przelewu) i wpiecie obu latek. Kazdy krok konczy sie "OK" albo "BLAD".
        /// </summary>
        private static void Harness()
        {
            var s = Settings.Current;
            if (s == null || !s.RansomHarnessInAutotest || Campaign.Current == null) return;
            if (_harnessDone >= 2 || (_sessDay != 5 && _sessDay != 8)) return;
            if (!AutotestActive()) return;
            var me = Hero.MainHero;
            if (_sessDay == 5 && _harnessDone == 0)
            {
                _harnessDone = 1;
                MobileParty cap = null;
                foreach (var mp in MobileParty.All)
                    if (mp != null && mp.IsActive && mp.IsLordParty && !mp.IsMainParty && mp.LeaderHero != null && mp.LeaderHero.IsAlive && mp.ActualClan != Clan.PlayerClan) { cap = mp; break; }
                int amt = me != null ? Math.Min(1000, me.Gold) : 0;
                if (cap == null || amt <= 0) { Log.Info("Harness niewoli (2.14): krok 1 - pominiety (" + (cap == null ? "brak partii lorda AI" : "gracz bez zlota") + ") OK."); }
                else
                {
                    var rec = cap.LeaderHero;
                    long p0 = me.Gold, r0 = rec.Gold;
                    string to; bool ok = PayPlayerRansom(amt, cap.Party, "harness", out to);
                    long p1 = me.Gold, r1 = rec.Gold;
                    bool pair = ok && p0 - p1 == amt && r1 - r0 == amt;
                    Log.Info("Harness niewoli (2.14): krok 1 - okup gracza " + amt + " zl przez PayPlayerRansom -> " + to + ": kiesa gracza " + p0 + " -> " + p1 + ", kiesa porywacza " + r0 + " -> " + r1
                             + ", w nicosc " + (p0 - p1 - (r1 - r0)) + " - " + (pair ? "OK" : "BLAD") + ".");
                }
                // odbiorcy na sucho: loch miasta i banda
                string dry = "";
                bool dryOk = true;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown || st.Party == null || st.OwnerClan == null || st.OwnerClan == Clan.PlayerClan || st.OwnerClan.Leader == null || !st.OwnerClan.Leader.IsAlive) continue;
                    Hero h; Settlement ps; int r; ResolveRecipient(st.Party, out h, out ps, out r);
                    dry += "loch " + st.Name + " -> " + (h != null ? Name(h) : ps != null ? "kasa " + ps.Name : "nikt") + " (" + (r >= 0 ? RouteName[r] : "-") + ")";
                    dryOk &= r == 3 && h == st.OwnerClan.Leader;
                    break;
                }
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !mp.IsBandit || mp.BanditPartyComponent == null || mp.BanditPartyComponent.Hideout == null) continue;
                    Hero h; Settlement ps; int r; ResolveRecipient(mp.Party, out h, out ps, out r);
                    dry += "; banda " + mp.Name + " -> " + (h != null ? Name(h) : ps != null ? "kasa " + ps.Name : "nikt") + " (" + (r >= 0 ? RouteName[r] : "-") + ")";
                    dryOk &= (r == 0 && h != null) || r == 2;
                    break;
                }
                Log.Info("Harness niewoli (2.14): krok 1b - odbiorcy na sucho: " + (dry.Length > 0 ? dry : "-") + " - " + (dryOk ? "OK" : "BLAD") + ".");
                return;
            }
            if (_sessDay == 8 && _harnessDone == 1)
            {
                _harnessDone = 2;
                int t1, t2, t3;
                int d1 = CourierDecide(5000, 4500, out t1);   // ma cene, mniej niz cena + 1000: placi sam, dosypka 500 do zdjecia
                int d2 = CourierDecide(1000, 4500, out t2);   // nie ma ceny: oferta przepada
                int d3 = CourierDecide(9000, 4500, out t3);   // stac go z zapasem: jak w grze
                bool ok = d1 == 1 && t1 == 500 && d2 == 2 && d3 == 0 && t3 == 0;
                Log.Info("Harness niewoli (2.14): krok 2 - kurier na sucho: placacy 5000 / cena 4500 -> " + (d1 == 1 ? "placi sam, dosypka " + t1 + " zdjeta" : "?") + "; placacy 1000 -> "
                         + (d2 == 2 ? "oferta przepada" : "?") + "; placacy 9000 -> " + (d3 == 0 ? "jak w grze" : "?") + "; latki: " + Wired + " - " + (ok && _menuWired && _courierWired ? "OK" : "BLAD") + ".");
            }
        }
    }
}
