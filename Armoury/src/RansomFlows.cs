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
    ///    jej kryjowki; loch osady - pan osady (kryjowka - jej kasa); zapasowy odbiorca: glowa rodu porywacza, potem pan (glowa rodu wlasciciela)
    ///    najblizszego miasta spoza rodu gracza, na koncu kasa tego miasta (przeglad 2.14: kasa miasta ponad cel regulatora gry jest kasowana);
    ///    licznik "bez odbiorcy" (prog 0). Prefiks robi to, co oryginal (przelew + EndCaptivityAction.ApplyByRansom), i pomija go - tylko gdy
    ///    przelew sie udal (takze gdy wyjatek padl juz po przelewie - kiesa gracza mniejsza o kwote); inaczej oryginal jak dotad (w nicosc, liczone).
    ///  - `RansomCourierNoTopUp`: placacy AI ma cene, ale mniej niz cena + 1000 - oryginal biegnie, postfiks zdejmuje mu dosypke gry (placi
    ///    z wlasnej kiesy; nic, gdy oryginal nie biegl); nie ma ceny - oferta znika (SetCurrentRansomHero(null) gry, bez wpisu na liste odmow -
    ///    gracz przyjal), jeniec zostaje. Gracz placacy - bez zmian.
    ///  Kwoty bez zmian (do 178). Harness w autotescie (CrashScribe.Autotest.Active, `RansomHarnessInAutotest`): bez prawdziwej niewoli gracza;
    ///  kurier - prawdziwe AcceptRansomOffer gry na lordzie AI w druzynie gracza.
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
        private static readonly int[] _dRoute = new int[7];
        private static readonly string[] RouteName = { "wodz partii", "glowa rodu partii bez wodza", "kasa kryjowki bandy", "pan osady (loch)", "zapasowy: glowa rodu porywacza",
                                                       "zapasowy: pan najblizszego miasta", "zapasowy: kasa najblizszego miasta" };

        private static FieldInfo _fHero, _fPayer;
        private static MethodInfo _mAccept;
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
            // zapasowy odbiorca (2.0b): glowa rodu porywacza (rod bandy - nie), potem pan najblizszego miasta spoza rodu gracza (przeglad 2.14:
            // banda bez kryjowki - np. maruderzy - dawala okup do kasy miasta, a nadwyzke ponad cel regulator gry kasuje), na koncu kasa tego miasta
            if (clan != null && !clan.IsBanditFaction && Live(clan.Leader)) { hero = clan.Leader; route = 4; return; }
            Settlement best = null; float bd = float.MaxValue;
            try
            {
                var pos = captor != null && captor.IsMobile && captor.MobileParty != null ? captor.MobileParty.Position.ToVec2()
                        : captor != null && captor.IsSettlement && captor.Settlement != null ? captor.Settlement.GetPosition2D
                        : (MobileParty.MainParty != null ? MobileParty.MainParty.Position.ToVec2() : default(TaleWorlds.Library.Vec2));
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown || st.Town == null || st.OwnerClan == Clan.PlayerClan) continue;   // gracz nie placi sam sobie
                    float d = st.GetPosition2D.DistanceSquared(pos);
                    if (d < bd) { bd = d; best = st; }
                }
            }
            catch { best = null; }
            if (best == null) return;
            var owner = best.OwnerClan;
            if (owner != null && !owner.IsBanditFaction && Live(owner.Leader)) { hero = owner.Leader; route = 5; return; }
            purse = best; route = 6;
        }

        /// <summary>Okup gracza do porywacza: przelew (zdarzenie gry - ksiega obiegu go widzi). true - przelew zrobiony.</summary>
        internal static bool PayPlayerRansom(int amount, PartyBase captor, string why, out string to)
        {
            to = "-";
            var me = Hero.MainHero;
            if (me == null || amount <= 0) return false;
            Hero h; Settlement st; int route;
            ResolveRecipient(captor, out h, out st, out route);
            if (h != null) GiveGoldAction.ApplyBetweenCharacters(me, h, amount);
            else if (st != null) GiveGoldAction.ApplyForCharacterToSettlement(me, st, amount);
            else { _dNoRecipientN++; _dNoRecipientGold += amount; return false; }
            // przelew zrobiony - liczniki i log we wlasnym try (przeglad 2.14: wyjatek po przelewie nie moze puscic oryginalu, ktory zabralby drugi raz)
            try
            {
                to = h != null ? Name(h) + " (" + RouteName[route] + ")" : "kasa " + st.Name + " (" + RouteName[route] + ")";
                _dToCaptor += amount; _dToCaptorN++; _dRoute[route]++;
                Log.Info("Okupy (2.14): okup gracza " + amount + " zl (" + why + ") -> " + to + "; porywacz: " + Describe(captor) + "; w nicosc 0.");
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.PayPlayerRansom(log)", e); }
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
                long g0 = Hero.MainHero != null ? Hero.MainHero.Gold : 0;
                try { string to; paid = PayPlayerRansom(amt, captor, "menu niewoli", out to); }
                catch (Exception e)
                {
                    Stumbles++; Log.Error("RansomFlows.PayPlayerRansom", e);
                    // przeglad 2.14: wyjatek mogl pasc juz po przelewie (np. sluchacz zdarzenia gry) - wtedy oryginal nie moze zabrac kwoty drugi raz
                    paid = Hero.MainHero != null && Hero.MainHero.Gold <= g0 - amt;
                }
            }
            if (!paid)
            {
                DayPlayerToNothing += amt; _dToNothing += amt;   // oryginal gry: kwota do nikogo (wylacznik albo brak odbiorcy)
                return true;
            }
            // 178: reszta okupu gracza (ponad gotowke z menu) - dlug gracza wobec odbiorcy w ksiedze 168
            try { Hero rh; Settlement rs; int rr; ResolveRecipient(captor, out rh, out rs, out rr); if (rh != null) Ransom178.PlayerRansomPaid(rh, amt); }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.PlayerRansomPaid", e); }
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
                // 178: okup wedlug majatku - gotowka od rodziny placacego (bez dosypki), reszta dlug w ksiedze 168, jeniec wolny (oryginal gry pominiety)
                if (Ransom178.On && Ransom178.CourierAccept(__instance, hero, payer)) { DayCourierN++; return false; }
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
                // dec == 2: placacy nie ma ceny - oferta znika, jeniec zostaje. Przeglad 2.14: nie DeclineRansomOffer gry (wpisuje jenca na liste
                // odmow - kolejna oferta z szansa 0.12 zamiast 0.2, choc gracz przyjal), tylko to, czym Decline konczy: publiczne SetCurrentRansomHero(null)
                _dLapsedN++; _dLapsedGold += price;
                DayCourierIn -= price;   // przeglad 2.14 (harness uruchamia te droge): linia 169c "gracz dostal" - oferta przepadla, gracz nic nie dostal
                Log.Info("Okupy (2.14): kurier - " + Name(hero) + ": " + Name(payer) + " (rod " + clan + ") ma " + payer.Gold + " < cena " + price
                         + " - oferta przepada (bez wpisu na liste odmow), jeniec zostaje u gracza (gra dosypalaby " + topUp + " zl z niczego); w nicosc 0.");
                var beh = __instance as RansomOfferCampaignBehavior;
                if (beh != null) beh.SetCurrentRansomHero(null);
                Log.Player("The courier's masters could not raise the " + price + " denars they offered for " + Name(hero) + ". The offer lapses and " + Name(hero) + " stays your prisoner.", true);
                return false;
            }
            catch (Exception e) { Stumbles++; Log.Error("RansomFlows.AcceptPrefix", e); __state = null; return true; }
        }

        public static void AcceptPostfix(TopUpState __state, bool __runOriginal)
        {
            try
            {
                // przeglad 2.14: dosypke zdejmujemy tylko, gdy oryginal naprawde biegl (inny prefiks moglby go pominac - wtedy zdjecie szloby w nicosc)
                if (!__runOriginal || __state == null || __state.Payer == null || __state.Take <= 0) return;
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
                    var m = AccessTools.Method(typeof(RansomOfferCampaignBehavior), "AcceptRansomOffer", new[] { typeof(int) });
                    _mAccept = m;   // harness kuriera w autotescie wola ta sama (zalatana) metode
                    if (m != null && _fHero != null && _fPayer != null)
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
        /// 0 zl w nicosc; odbiorcy na sucho dla lochu miasta i dla bandy. Doba sesji 8: decyzja kuriera (CourierDecide) na sucho i wpiecie obu latek,
        /// potem (przeglad 2.14, K29) PRAWDZIWE AcceptRansomOffer gry (z latkami) na lordzie AI w druzynie gracza (juz jencu albo wzietym TakePrisonerAction -
        /// to nie otwiera okien RC, te sa tylko dla niewoli gracza): krok 2b cena = kiesa placacego + 1 (oferta przepada, jeniec zostaje, kiesy bez zmian),
        /// krok 2c cena = kiesa placacego - 500 (placacy -cena, gracz +cena, dosypka 0, jeniec wolny). Kazdy krok konczy sie "OK" albo "BLAD".
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
                CourierHarness(s);
            }
        }

        /// <summary>Lord AI do harnessu kuriera: already - juz jeniec w druzynie gracza; inaczej wolny lord bez partii (nie glowa rodu, nie krol,
        /// nie gubernator), ktorego glowa rodu (placacy, jak w grze) ma 2 000 - 10 mln.</summary>
        private static bool HarnessCandidate(Hero h, bool already)
        {
            if (h == null || !h.IsAlive || h == Hero.MainHero || !h.IsLord || h.IsChild) return false;
            if (Ransom178.IsGreatCaptive(h)) return false;   // recenzja D: nastepca tronu przeszedlby na korone gracza (nagroda, okup do skarbca) - nie w harnessie
            var c = h.Clan;
            if (c == null || c == Clan.PlayerClan || c.IsBanditFaction || c.IsEliminated || ClanIncomeBook.IsUndeadClan(c)) return false;
            var lead = c.Leader;
            if (lead == null || lead == h || !lead.IsAlive || lead.IsPrisoner || lead == Hero.MainHero || lead.Gold < 2000 || lead.Gold > 10000000) return false;
            if (already) return h.IsPrisoner && h.PartyBelongedToAsPrisoner == PartyBase.MainParty;
            if (h.IsPrisoner || h.HeroState != Hero.CharacterStates.Active || h.PartyBelongedTo != null || h.GovernorOf != null) return false;
            return c.Kingdom == null || c.Kingdom.Leader != h;
        }

        private static bool InMainParty(Hero h) { return h != null && h.IsPrisoner && h.PartyBelongedToAsPrisoner == PartyBase.MainParty; }

        /// <summary>Krok 2b i 2c harnessu: prawdziwe AcceptRansomOffer gry (przez refleksje - biegna latki AcceptPrefix/AcceptPostfix).</summary>
        private static void CourierHarness(Settings s)
        {
            const string pre = "Harness niewoli (2.14): krok 2b/2c - kurier prawdziwy (AcceptRansomOffer gry): ";
            var me = Hero.MainHero;
            var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<RansomOfferCampaignBehavior>() : null;
            if (!s.RansomCourierNoTopUp) { Log.Info(pre + "pominiety (RansomCourierNoTopUp wylaczone)."); return; }
            if (beh == null || _mAccept == null || _fHero == null || !_courierWired) { Log.Info(pre + "brak zachowania gry albo latki kuriera - BLAD."); return; }
            if (_fHero.GetValue(beh) != null) { Log.Info(pre + "pominiety (trwa prawdziwa oferta okupu)."); return; }
            if (me == null || me.IsPrisoner || PartyBase.MainParty == null) { Log.Info(pre + "pominiety (gracz w niewoli albo bez druzyny)."); return; }
            Hero prisoner = null; bool taken = false;
            foreach (var h in Hero.AllAliveHeroes) if (HarnessCandidate(h, true)) { prisoner = h; break; }
            if (prisoner == null) foreach (var h in Hero.AllAliveHeroes) if (HarnessCandidate(h, false)) { prisoner = h; break; }
            if (prisoner == null) { Log.Info(pre + "pominiety (brak lorda AI bez partii z placacym 2 000 - 10 mln)."); return; }
            var payer = prisoner.Clan.Leader;
            try
            {
                if (!prisoner.IsPrisoner) { TakePrisonerAction.Apply(PartyBase.MainParty, prisoner); taken = true; }
                if (!InMainParty(prisoner)) { Log.Info(pre + Name(prisoner) + " nie trafil do druzyny gracza - BLAD."); return; }
                string who = Name(prisoner) + " (rod " + (prisoner.Clan != null ? prisoner.Clan.Name.ToString() : "-") + (taken ? ", wziety w niewole przez harness" : ", jeniec gracza") + "), placacy " + Name(payer);
                if (Ransom178.On)
                {
                    // 178: kurier placi okup wedlug majatku - gotowka od rodziny jenca (najwyzej polowa ponad 5 000), reszta dlug okupu w ksiedze 168, jeniec wolny;
                    // cena z oferty ignorowana (178 liczy sama), dosypki z niczego brak: rodzina zaplacila = gracz dostal
                    try
                    {
                        var pc = prisoner.Clan;
                        long q0 = ClanIncomeBook.FamilyGoldOf(pc), p0 = me.Gold, d0 = DebtLadder.RansomDebtOf(pc);
                        beh.SetCurrentRansomHero(prisoner, payer);
                        _mAccept.Invoke(beh, new object[] { 1 });
                        long paidBy = q0 - ClanIncomeBook.FamilyGoldOf(pc), got = me.Gold - p0, debt = DebtLadder.RansomDebtOf(pc) - d0;
                        bool free = !prisoner.IsPrisoner, gone = _fHero.GetValue(beh) == null;
                        bool ok = paidBy >= 0 && paidBy == got && free && gone && debt >= 0;
                        Log.Info("Harness niewoli (178): krok 2 - kurier prawdziwy, okup wedlug majatku: " + who + " -> rodzina zaplacila gotowka " + paidBy + ", gracz dostal " + got
                                 + ", z niczego " + (got - paidBy) + ", dlug okupu wobec gracza +" + debt + ", jeniec " + (free ? "wolny" : "DALEJ W NIEWOLI") + ", oferta " + (gone ? "zamknieta" : "WISI")
                                 + " - " + (ok ? "OK" : "BLAD") + ".");
                    }
                    catch (Exception e) { Stumbles++; Log.Error("RansomFlows.Harness(178)", e); Log.Info("Harness niewoli (178): krok 2 - wyjatek (" + e.GetType().Name + ") - BLAD."); }
                    return;
                }
                // 2b: placacy nie ma ceny - oferta przepada, jeniec zostaje, kiesy bez zmian
                try
                {
                    long q0 = payer.Gold, p0 = me.Gold; int lapsed0 = _dLapsedN;
                    int price = (int)Math.Min(int.MaxValue, q0 + 1);
                    beh.SetCurrentRansomHero(prisoner, payer);
                    _mAccept.Invoke(beh, new object[] { price });
                    bool gone = _fHero.GetValue(beh) == null, stays = InMainParty(prisoner);
                    bool ok = gone && stays && me.Gold == p0 && payer.Gold == q0 && _dLapsedN == lapsed0 + 1;
                    Log.Info("Harness niewoli (2.14): krok 2b - kurier prawdziwy, cena = kiesa placacego + 1: " + who + " ma " + q0 + ", cena " + price + " -> oferta " + (gone ? "przepadla" : "WISI")
                             + ", jeniec " + (stays ? "zostaje u gracza" : "UWOLNIONY") + ", kiesa gracza " + p0 + " -> " + me.Gold + ", kiesa placacego " + q0 + " -> " + payer.Gold + " - " + (ok ? "OK" : "BLAD") + ".");
                }
                catch (Exception e) { Stumbles++; Log.Error("RansomFlows.Harness(2b)", e); Log.Info("Harness niewoli (2.14): krok 2b - wyjatek (" + e.GetType().Name + ") - BLAD."); }
                // 2c: placacy ma cene, ale mniej niz cena + 1000 - gra dosypalaby 500; placi z wlasnej kiesy, gracz dostaje cene, jeniec wolny
                try
                {
                    if (!InMainParty(prisoner)) { Log.Info("Harness niewoli (2.14): krok 2c - jeniec nie jest juz w druzynie gracza po kroku 2b - BLAD."); return; }
                    long q0 = payer.Gold, p0 = me.Gold;
                    int price = (int)Math.Min(int.MaxValue, q0 - 500);
                    beh.SetCurrentRansomHero(prisoner, payer);
                    _mAccept.Invoke(beh, new object[] { price });
                    long paidBy = q0 - payer.Gold, got = me.Gold - p0;
                    bool free = !prisoner.IsPrisoner, gone = _fHero.GetValue(beh) == null;
                    bool ok = paidBy == price && got == price && free && gone;
                    Log.Info("Harness niewoli (2.14): krok 2c - kurier prawdziwy, cena = kiesa placacego - 500: " + who + " ma " + q0 + ", cena " + price + " -> placacy zaplacil " + paidBy
                             + ", gracz dostal " + got + ", z niczego " + (got - paidBy) + " (dosypka gry zdjeta), jeniec " + (free ? "wolny" : "DALEJ W NIEWOLI") + ", oferta " + (gone ? "zamknieta" : "WISI")
                             + " - " + (ok ? "OK" : "BLAD") + ".");
                }
                catch (Exception e) { Stumbles++; Log.Error("RansomFlows.Harness(2c)", e); Log.Info("Harness niewoli (2.14): krok 2c - wyjatek (" + e.GetType().Name + ") - BLAD."); }
            }
            finally
            {
                // sprzatanie: jeniec wziety przez harness nie zostaje u gracza (gdy krok 2c go nie uwolnil); oferta nie wisi
                try
                {
                    if (taken && InMainParty(prisoner)) { EndCaptivityAction.ApplyByReleasedByChoice(prisoner); Log.Info("Harness niewoli (2.14): sprzatanie - " + Name(prisoner) + " uwolniony."); }
                    if (_fHero.GetValue(beh) != null) beh.SetCurrentRansomHero(null);
                }
                catch (Exception e) { Stumbles++; Log.Error("RansomFlows.Harness(sprzatanie)", e); }
            }
        }
    }
}
