using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Library;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// E3/1 (paczka 164a-1) - PRAWDZIWY PLATNIK I KASY OSAD (projekt etapu 3, rozdz. 2 R2/R3, rozdz. 3 E3/1). Wylacznik TownPaysRewards.
    /// Wspolny mechanizm: prefiks (priorytet Low; okien ksiegi na tej metodzie nie ma) na GiveGoldAction.ApplyInternal. Gdy otwarty kontekst (stos do 4)
    /// i zdarzenie jest "z niczego" (dajacy bohater i partia null) do oczekiwanego odbiorcy - dajacy = osada (albo partia) platnika, kwota = najwyzej
    /// NADWYZKA platnika (Seal31.Spare z zywej kasy: miasto - kasa ponad zapas kupcow minus tarcze; zamek - CastlePurse.Spendable; wyplata obniza kase,
    /// wiec i nadwyzke - poprawka po recenzji: "wydane dzis" nie jest odejmowane drugi raz). Gra obcina kwote do CALEJ kasy dajacego (GiveGoldAction.cs:21-25), wiec nadwyzke liczymy sami; zdarzenie idzie z prawdziwym platnikiem -
    /// ksiega widzi przelew osada -> bohater ("Przeplywy osad" juz je licza). Bez kontekstu: wyjscie w pierwszej linii.
    /// Zastosowania (prefiks otwiera kontekst, finalizer zamyka - takze po wyjatku):
    ///  - jency (SellPrisonersAction.ApplyInternal): platnik = osada kupujaca (buyerParty.Settlement), przy kupujacym null (gracz u posrednika,
    ///    "sprzedaj wszystkich") - Settlement.CurrentSettlement (krytyka W1); placi tylko za ZOLNIERZY (wartosc z tych samych wywolan modelu co gra);
    ///    czesc za bohaterow zostaje jak dzis (z niczego - u gracza rownowazy ja 178), wyplacona osobnym zdarzeniem w POSTFIKSIE Priority.First
    ///    (poprawka po recenzji: postfiksy Harmony biegna przed finalizerami - wyplata w finalizerze przychodzila PO postfiksie 178 (Last), ktory
    ///    liczy zwrot gracza z jego kiesy; biedny gracz u posrednika dostawal zloto z niczego i do tego roszczenie); finalizer tylko awaryjnie
    ///    (wyjatek w metodzie gry - postfiks nie biegl). Oba jeszcze w oknie O09 (jego finalizer ma nizszy priorytet).
    ///    Twierdza sprzedaje sama (kupujacy null) - placi kupujacy, brak kupujacego - 0 (jency wypuszczeni). Gracz dostal mniej - komunikat.
    ///  - lup z oblezenia (SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied): platnik = zdobyta osada (kazda wyplata z tej samej nadwyzki);
    ///  - nagroda turnieju AI (TournamentManager.GivePrizeToWinner): platnik = miasto-gospodarz; prefiks High (przed RBMTournament, ktory wyplaca sam - audyt 11.10);
    ///  - jency z rabunkow BK (BKRaidCaptureBehavior.ExecuteCapture): platnik = najblizsze miasto krolestwa najezdzcy;
    ///  - myto (perk Tollgates; CaravansCampaignBehavior i BK BKCaravansBehavior OnSettlementEntered): gra dopisuje licznikowi cel miasta z niczego -
    ///    platnik = kasa wjezdzajacej karawany (najwyzej to, co ma; reszte licznik oddaje), okno myta ksiegi (O46) widzi przelew (ToCounterRun).
    /// Ksiega: "jency sprzedani przez partie", "jency do kas twierdz", "jency z rabunkow BK", "lup z oblezen", "nagrody turniejow", "myto" w ZRODLACH
    /// -> 0 (zostaje czesc bohaterow); kontrolki okien: wywolan n, trafien z niczego 0.
    /// </summary>
    internal static class RealPayer
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.TownPaysRewards; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private const int KPris = 0, KPrisFort = 1, KSiege = 2, KTourney = 3, KRaid = 4, KToll = 5, Kinds = 6;
        private static readonly string[] KName = { "jency sprzedani przez partie", "jency z twierdz (bez kupujacego - 0)", "lup z oblezen", "nagrody turniejow", "jency z rabunkow BK", "myto (z kasy karawany)" };

        internal sealed class Ctx
        {
            public int Kind; public Settlement Payer; public MobileParty PayerParty; public bool NoPayer;
            public Hero ExpHero; public PartyBase ExpParty; public HashSet<object> Allowed;
            public long SoldierLeft = -1, HeroRepay; public Hero RecHero; public PartyBase RecParty; public bool Swapped;
            public long PlDue, PlPaid;
        }

        private static readonly Ctx[] _stack = new Ctx[4];
        private static int _depth;
        private static readonly long[] _due = new long[Kinds], _paid = new long[Kinds];
        private static readonly int[] _n = new int[Kinds], _noSpare = new int[Kinds];
        private static long _heroRepay, _plDue, _plPaid, _tollShort;
        private static int _strays, _noPayer, _belowReserve, _tollNotSeen, _overflow, _heroLate;

        private static bool Push(Ctx c) { if (_depth >= _stack.Length) { _overflow++; return false; } _stack[_depth++] = c; return true; }

        private static void Pop(Ctx c)
        {
            if (c == null || _depth == 0) return;
            if (ReferenceEquals(_stack[_depth - 1], c)) { _stack[--_depth] = null; return; }
            for (int i = _depth - 1; i >= 0; i--) if (ReferenceEquals(_stack[i], c)) { for (int j = i; j < _depth - 1; j++) _stack[j] = _stack[j + 1]; _stack[--_depth] = null; Seal31.Stumbles++; return; }
        }

        // ------------------------------------------------------------ wspolny prefiks na GiveGoldAction.ApplyInternal
        public static void GivePrefix(ref Hero __0, ref PartyBase __1, Hero __2, PartyBase __3, ref int __4)
        {
            if (_depth == 0) return;
            try
            {
                var c = _stack[_depth - 1];
                if (c == null || __0 != null || __1 != null || __4 <= 0 || (__2 == null && __3 == null)) return;   // nie "z niczego" - bez zmian
                bool match = (c.Allowed != null ? ((__2 != null && c.Allowed.Contains(__2)) || (__3 != null && c.Allowed.Contains(__3)))
                                                         : (c.ExpHero != null ? ReferenceEquals(__2, c.ExpHero) : (c.ExpParty != null && ReferenceEquals(__3, c.ExpParty))));
                if (!match) { _strays++; return; }
                bool pris = c.Kind == KPris || c.Kind == KPrisFort;
                if (pris) { if (c.Swapped) { _strays++; return; } c.Swapped = true; }
                long amount = __4, due = amount;
                if (pris && c.SoldierLeft >= 0)
                {
                    due = Math.Min(amount, c.SoldierLeft); c.SoldierLeft -= due;
                    long hero = amount - due;
                    if (hero > 0) { c.HeroRepay += hero; c.RecHero = __2; c.RecParty = __3; }
                }
                long pay;
                if (c.NoPayer) pay = 0;
                else if (c.Payer != null)
                {
                    long sp = Seal31.Spare(c.Payer);
                    if (sp <= 0) _noSpare[c.Kind]++;
                    pay = Math.Min(due, sp);
                }
                else if (c.PayerParty != null) pay = Math.Min(due, Math.Max(0, c.PayerParty.PartyTradeGold));
                else { _noPayer++; if (pris) { c.HeroRepay = 0; } return; }   // bez platnika - jak w grze (z niczego), licznik (prog 0)
                if (c.Payer != null) { __1 = c.Payer.Party; Seal31.NoteSpent(c.Payer, pay); }
                else if (c.PayerParty != null) __1 = c.PayerParty.Party;
                __4 = (int)pay;
                if (pris && c.Payer != null) CitySack.NotePrisoners(c.Payer, pay);   // lup miast (L0): jency kupieni przez osade po nastepstwach oblezenia (tylko licznik)
                _due[c.Kind] += due; _paid[c.Kind] += pay; _n[c.Kind]++;
                if (ReferenceEquals(__2, Hero.MainHero) || (__3 != null && ReferenceEquals(__3, PartyBase.MainParty))) { c.PlDue += due; c.PlPaid += pay; }
                if (c.Payer != null && c.Payer.IsTown && c.Payer.Town != null && c.Payer.Town.Gold - pay < Seal31.TownReserve(c.Payer.Town) && c.Payer.Town.Gold >= Seal31.TownReserve(c.Payer.Town)) _belowReserve++;
            }
            catch (Exception e) { Seal31.Stumble("RealPayer.GivePrefix", e); }
        }

        // ------------------------------------------------------------ konteksty
        /// <summary>SellPrisonersAction.ApplyInternal(seller, buyer, prisoners, applyConsequences).</summary>
        public static void PrisPre(PartyBase __0, PartyBase __1, TroopRoster __2, bool __3, out Ctx __state)
        {
            __state = null;
            try
            {
                if (!On || !Live || !__3 || __0 == null || __2 == null) return;
                var c = new Ctx { Kind = __0.IsSettlement ? KPrisFort : KPris };
                var model = Campaign.Current.Models.RansomValueCalculationModel;
                long soldiers = 0;
                foreach (var el in __2.GetTroopRoster())
                {
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    soldiers += (long)el.Number * model.PrisonerRansomValue(ch, __0.LeaderHero);   // te same wywolania co gra (SellPrisonersAction.cs:61-62)
                }
                c.SoldierLeft = soldiers;
                if (__0.IsSettlement)
                {
                    if (__1 == null) c.NoPayer = true;                                          // twierdza bez kupujacego - nikt nie placi, jency wypuszczeni
                    else if (__1.IsSettlement) c.Payer = __1.Settlement;
                    else if (__1.IsMobile) c.PayerParty = __1.MobileParty;
                    c.ExpParty = __0.Settlement != null ? __0.Settlement.Party : null;
                }
                else
                {
                    var st = __1 != null ? __1.Settlement : null;
                    if (st == null && __1 == null) st = Settlement.CurrentSettlement;           // gracz u posrednika / "sprzedaj wszystkich" (krytyka W1)
                    if (st == null && __1 != null && __1.IsMobile) c.PayerParty = __1.MobileParty;
                    else c.Payer = st;
                    var mp = __0.MobileParty;
                    Hero rh = null;                                                              // odbiorca jak w grze (SellPrisonersAction.cs:69-79)
                    if (__0.LeaderHero != null && __0.LeaderHero.HeroState == Hero.CharacterStates.Active) rh = __0.LeaderHero;
                    else if (__0.Owner != null && __0.Owner.HeroState == Hero.CharacterStates.Active) rh = __0.Owner;
                    else if (mp != null && mp.ActualClan != null && mp.ActualClan.Leader != null) rh = mp.ActualClan.Leader;
                    if (rh == null) return;
                    c.ExpHero = rh;
                }
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Seal31.Stumble("RealPayer.PrisPre", e); }
        }

        public static void SiegePre(Settlement __1, Dictionary<MobileParty, float> __4, out Ctx __state)
        {
            __state = null;
            try
            {
                if (!On || !Live || __1 == null || __4 == null) return;
                var c = new Ctx { Kind = KSiege, Payer = __1, Allowed = new HashSet<object>() };   // odbiorcy jak w grze: wodz partii z udzialem albo jej kiesa
                foreach (var kv in __4) { var mp = kv.Key; if (mp == null) continue; c.Allowed.Add(mp.Party); if (mp.LeaderHero != null) c.Allowed.Add(mp.LeaderHero); }
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Seal31.Stumble("RealPayer.SiegePre", e); }
        }

        public static void TourneyPre(TournamentGame __0, Hero __1, out Ctx __state)
        {
            __state = null;
            PlayerFees8.PrizeBefore(__0, __1);   // E8/D2 (257): migawka sakw gracza, gdy zwyciezca jest w jego druzynie (wlasny try; niezalezne od TownPaysRewards) - przedmiot nagrody rozlicza postfiks PlayerFees8.PrizeAfter
            try
            {
                if (!On || !Live || __0 == null || __1 == null || __1.Clan == null || __1.Clan.Leader == null || __0.Town == null || __0.Town.Settlement == null) return;
                var c = new Ctx { Kind = KTourney, Payer = __0.Town.Settlement, ExpHero = __1.Clan.Leader };   // gra: GiveGoldAction(nic, glowa rodu zwyciezcy, cena nagrody)
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Seal31.Stumble("RealPayer.TourneyPre", e); }
        }

        public static void RaidPre(MobileParty __0, Hero __1, out Ctx __state)
        {
            __state = null;
            try
            {
                if (!On || !Live || __0 == null || __1 == null) return;
                var c = new Ctx { Kind = KRaid, ExpHero = __1, Payer = Seal31.NearestTownOf(__0.GetPosition2D, __0.MapFaction) };
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Seal31.Stumble("RealPayer.RaidPre", e); }
        }

        /// <summary>Postfiks jencow (Priority.First): czesc za bohaterow jak w grze - PRZED postfiksem 178 (Ransom178.SalePostfix, Priority.Last), ktory
        /// liczy zwrot gracza z jego kiesy (musi widziec w niej zloto za lorda, jak przed 3.1), i przed finalizerem okna O09 (okno widzi wyplate).
        /// Kontekst zdejmowany przed wyplata - jej zdarzenie nie trafia do naszego prefiksu (finalizer zdejmuje drugi raz bez skutku).</summary>
        public static void PrisPost(Ctx __state)
        {
            if (__state == null) return;
            Pop(__state);
            PayHeroes(__state);
        }

        /// <summary>Czesc za bohaterow (z niczego, jak w grze) - raz: kwota zerowana przed wyplata (postfiks albo awaryjnie finalizer).</summary>
        private static void PayHeroes(Ctx c)
        {
            try
            {
                if (c == null || c.HeroRepay <= 0) return;
                int h = (int)Math.Min(int.MaxValue, c.HeroRepay);
                c.HeroRepay = 0;
                if (c.RecHero != null) GiveGoldAction.ApplyBetweenCharacters(null, c.RecHero, h, c.RecHero != Hero.MainHero);   // graczowi - komunikat gry jak dotad
                else if (c.RecParty != null && c.RecParty.IsSettlement) GiveGoldAction.ApplyForPartyToSettlement(null, c.RecParty.Settlement, h, true);
                _heroRepay += h;
            }
            catch (Exception e) { Seal31.Stumble("RealPayer.PayHeroes", e); }
        }

        /// <summary>Wspolny finalizer (wyzszy priorytet niz okna ksiegi - biegnie, gdy okno O09 jeszcze otwarte): zdjecie kontekstu, czesc za bohaterow
        /// tylko awaryjnie (postfiks nie biegl - wyjatek w metodzie gry), komunikat gracza.</summary>
        public static void CtxFin(Ctx __state)
        {
            if (__state == null) return;
            Pop(__state);
            try
            {
                var c = __state;
                if (c.HeroRepay > 0) { _heroLate++; PayHeroes(c); }
                if (c.PlDue > c.PlPaid && c.Kind == KPris && PrisChain(c)) { }   // m110-jency: reszta lancuchem platnikow (rod osady, korona) albo dlug miasta
                else if (c.PlDue > c.PlPaid)
                {
                    string town = c.Payer != null ? c.Payer.Name.ToString() : "The town";
                    if (c.Kind == KPris) Log.Player(town + " could pay only " + c.PlPaid + " of " + c.PlDue + " for your prisoners.", true);
                    else if (c.Kind == KSiege) Log.Player(town + " could yield only " + c.PlPaid + " of " + c.PlDue + " in plunder for your party.", true);
                    else if (c.Kind == KTourney) Log.Player(town + " could pay only " + c.PlPaid + " of the " + c.PlDue + " prize.", true);
                    else if (c.Kind == KRaid) Log.Player(town + " could pay only " + c.PlPaid + " of " + c.PlDue + " for the captives you took.", true);
                }
                if (c.Kind == KPris && (c.PlDue > 0 || c.PlPaid > 0)) { _plDue += c.PlDue; _plPaid += c.PlPaid; }
            }
            catch (Exception e) { Seal31.Stumble("RealPayer.CtxFin", e); }
        }

        /// <summary>m110-jency (Jeff 23.10 22:28, zrzut Maidenpool: "could pay only 0 of 36 for your prisoners" - "jesli ich nie stac, to nie moga zaplacic, a zabrali
        /// mi jencow?"): miasto placi graczowi za jencow tylko z NADWYZKI (Seal31.Spare: kasa ponad zapas kupcow), a jency i tak przechodzili - Maidenpool mialo
        /// 36 723 przy zapasie 39 662, wiec 0. Jak nagroda turnieju (TP): reszte placi lancuch platnikow (TourneyPay.PayChain - rod, do ktorego nalezy miasto,
        /// potem rod panujacy, kazdy z tego, co moze dac), a czego nikt nie zaplaci - DLUG miasta wobec gracza (rodzaj MPrisoners, splata co dobe jak dlugi
        /// turniejowe). Osada z Town (miasto i zamek - PayDebts i TownCap licza oba przez Seal31.Spare / CastlePurse) i sprzedaz gracza; klucz PrisonerPayChain
        /// (oraz Tourney Payer Chain - lordowie, Tourney Debts - dlug). Po recenzji (wf_d5e8c01a): osada gracza - reszta to dlug KASY osady (kupuje kasa, nie
        /// kiesa gracza); osada krolestwa w wojnie z graczem - bez lordow, cala reszta w dlug do pokoju (TourneyPay.DebtOnly). Wiadomosc: kto zaplacil, ile osada
        /// jest winna. false - stara sciezka ("could pay only").</summary>
        private static bool PrisChain(Ctx c)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.PrisonerPayChain || !TourneyPay.AnyOn || c.Payer == null || c.Payer.Town == null) return false;
                long left = c.PlDue - c.PlPaid;
                if (left <= 0 || left > int.MaxValue) return false;
                TourneyPay.Parts parts;
                if (TourneyPay.AtWarWithMe(c.Payer)) { parts = TourneyPay.DebtOnly(c.Payer, (int)left, TourneyPay.MPrisoners); _prisChainWar++; }   // wrog: dlug do pokoju
                else if (!TourneyPay.PayChain(c.Payer, (int)left, 0, true, TourneyPay.MPrisoners, out parts)) return false;   // kasa dala juz swoja nadwyzke - lancuch od rodu
                _prisChainN++; _prisChainPaid += parts.Paid; _prisChainDebt += parts.Debt; _prisChainLost += parts.Lost;
                TourneyPay.Tell(TourneyPay.TPris, c.Payer, (int)Math.Min(int.MaxValue, c.PlDue), parts, (int)Math.Min(int.MaxValue, c.PlPaid));
                return true;
            }
            catch (Exception e) { Seal31.Stumble("RealPayer.PrisChain", e); return false; }
        }

        private static int _prisChainN, _prisChainWar;
        private static long _prisChainPaid, _prisChainDebt, _prisChainLost;

        // ------------------------------------------------------------ myto (perk Tollgates)
        public struct TollState { public bool On; public int C0; }

        public static void TollPreVanilla(MobileParty __0, Settlement __1, out TollState __state) { __state = default(TollState); TollOpen(__0, __1, true, ref __state); }
        public static void TollPreBk(MobileParty __0, Settlement __1, out TollState __state) { __state = default(TollState); TollOpen(__0, __1, false, ref __state); }

        private static void TollOpen(MobileParty mp, Settlement st, bool vanilla, ref TollState s)
        {
            try
            {
                if (!On || !Live || mp == null || !mp.IsCaravan || st == null || !st.IsTown || st.Town == null) return;
                if (vanilla && !mp.HasLandNavigationCapability) return;                      // ten sam warunek co gra (CaravansCampaignBehavior.cs:726)
                var gov = st.Town.Governor;
                if (gov == null || !gov.GetPerkValue(DefaultPerks.Trade.Tollgates)) return;
                s.On = true; s.C0 = st.Town.TradeTaxAccumulated;
            }
            catch (Exception e) { s.On = false; Seal31.Stumble("RealPayer.TollOpen", e); }
        }

        /// <summary>Postfiks (przed finalizerem okna myta O46): myto z kasy karawany; czego karawana nie ma - licznik cel oddaje.</summary>
        public static void TollPost(MobileParty __0, Settlement __1, TollState __state)
        {
            if (!__state.On) return;
            try
            {
                int toll = MathF.Round(DefaultPerks.Trade.Tollgates.SecondaryBonus);
                if (toll <= 0) return;
                if (__1.Town.TradeTaxAccumulated - __state.C0 < toll) { _tollNotSeen++; return; }   // gra myta nie dopisala - bez zmian
                int x = Math.Min(toll, Math.Max(0, __0.PartyTradeGold));
                __0.PartyTradeGold -= x;
                if (x < toll) { __1.Town.TradeTaxAccumulated -= (toll - x); _tollShort += toll - x; }
                CW.ToCounterRun += x;                                                            // okno O46: przelew karawana -> licznik cel, nie zloto z niczego
                _due[KToll] += toll; _paid[KToll] += x; _n[KToll]++;
            }
            catch (Exception e) { Seal31.Stumble("RealPayer.TollPost", e); }
        }

        // ------------------------------------------------------------ wpiecia
        internal static void ApplyAll()
        {
            var give = AccessTools.Method(typeof(GiveGoldAction), "ApplyInternal");
            Seal31.Patch("platnik: GiveGoldAction.ApplyInternal", give, typeof(RealPayer), nameof(GivePrefix), null, null, Priority.Low);
            Seal31.Patch("platnik: jency (SellPrisonersAction.ApplyInternal)", AccessTools.Method(typeof(SellPrisonersAction), "ApplyInternal"), typeof(RealPayer), nameof(PrisPre), nameof(PrisPost), nameof(CtxFin), Priority.Low, Priority.First, Priority.High);   // postfiks First - przed 178 (Last)
            Seal31.Patch("platnik: lup z oblezenia (OnSiegeAftermathApplied)", AccessTools.Method(typeof(SiegeAftermathCampaignBehavior), "OnSiegeAftermathApplied"), typeof(RealPayer), nameof(SiegePre), null, nameof(CtxFin), Priority.Low, Priority.Normal, Priority.High);
            // audyt zlota 11.10 (5a RP'): prefiks High - przed prefiksem RBMTournament (Normal, RBMTournament.cs:545), ktory sam wyplaca nagrode z niczego i zwraca false;
            // przy Low kontekst otwieral sie po wyplacie RBM (0 wyplat przez caly bieg). Nadal ponizej okna ksiegi O11 (First); finalizer bez zmian.
            Seal31.Patch("platnik: nagroda turnieju (GivePrizeToWinner)", AccessTools.Method(typeof(TournamentManager), "GivePrizeToWinner"), typeof(RealPayer), nameof(TourneyPre), null, nameof(CtxFin), Priority.High, Priority.Normal, Priority.High);
            const string bkRaid = "BannerKings.Behaviours.Raids.BKRaidCaptureBehavior", bkCar = "BannerKings.Behaviours.BKCaravansBehavior";
            var rt = AccessTools.TypeByName(bkRaid);
            Seal31.Patch("platnik: jency z rabunkow BK (ExecuteCapture)", rt != null ? AccessTools.Method(rt, "ExecuteCapture") : null, typeof(RealPayer), nameof(RaidPre), null, nameof(CtxFin), Priority.Low, Priority.Normal, Priority.High);
            var sig = new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) };
            Seal31.Patch("platnik: myto (vanilla)", AccessTools.Method(typeof(CaravansCampaignBehavior), "OnSettlementEntered", sig), typeof(RealPayer), nameof(TollPreVanilla), nameof(TollPost), null, Priority.Low, Priority.Normal);
            var ct = AccessTools.TypeByName(bkCar);
            Seal31.Patch("platnik: myto (BK)", ct != null ? AccessTools.Method(ct, "OnSettlementEntered", sig) : null, typeof(RealPayer), nameof(TollPreBk), nameof(TollPost), null, Priority.Low, Priority.Normal);
            // przy kodzie (projekt rozdz. 6): kto jeszcze lata GiveGoldAction.ApplyInternal
            try
            {
                var info = give != null ? Harmony.GetPatchInfo(give) : null;
                var others = new List<string>();
                if (info != null) foreach (var o in info.Owners) if (Seal31.H == null || o != Seal31.H.Id) others.Add(o);
                Log.Info("Szczelnosc (164a): GiveGoldAction.ApplyInternal - latki innych modow: " + (others.Count > 0 ? string.Join(", ", others.ToArray()) : "brak") + ".");
            }
            catch (Exception e) { Log.Info("Szczelnosc (164a): GiveGoldAction.ApplyInternal - odczyt latek nieudany (" + e.Message + ")."); }
        }

        // ------------------------------------------------------------ linia, porzadki
        internal static string Part()
        {
            var sb = new StringBuilder(800);
            if (!On) sb.Append("WYLACZONE (Town Pays Rewards) - ");
            for (int k = 0; k < Kinds; k++)
            {
                if (k > 0) sb.Append("; ");
                sb.Append(KName[k]).Append(": gra chciala ").Append(_due[k]).Append(", zaplacili ").Append(_paid[k]).Append(", uciete ").Append(_due[k] - _paid[k])
                  .Append(" (").Append(_n[k]).Append(" wyplat, platnik bez nadwyzki ").Append(_noSpare[k]).Append(')');
            }
            long spent; int spentN; Seal31.SpentToday(out spent, out spentN);
            sb.Append(" | kasy osad wydaly dzis ").Append(spent).Append(" w ").Append(spentN).Append(" osadach (nadwyzka z zywej kasy - wyplata juz ja obnizyla)");
            sb.Append(" | czesc za bohaterow jak w grze (z niczego) ").Append(_heroRepay).Append(" (w tym awaryjnie w finalizerze - postfiks nie biegl ").Append(_heroLate).Append(')')
              .Append(" | gracz u posrednika i w miescie: wycena ").Append(_plDue).Append(", zaplacone ").Append(_plPaid)
              .Append(" (m110-jency: reszta lancuchem ").Append(_prisChainN).Append(" razy - rody zaplacily ").Append(_prisChainPaid).Append(", w dlug miasta ").Append(_prisChainDebt)
              .Append(", przepadlo ").Append(_prisChainLost).Append(", osada wroga - sam dlug ").Append(_prisChainWar).Append(')')
              .Append(" | myto: karawana nie miala ").Append(_tollShort).Append(" (licznik oddal), gra nie dopisala ").Append(_tollNotSeen)
              .Append(" | kontrola (prog 0): bez platnika ").Append(_noPayer).Append(", miasto ponizej zapasu kupcow przez nasza wyplate ").Append(_belowReserve)
              .Append(", zdarzenia z niczego w kontekscie niedopasowane ").Append(_strays).Append(", stos przepelniony ").Append(_overflow);
            return sb.ToString();
        }

        internal static void ClearDay()
        {
            Array.Clear(_due, 0, Kinds); Array.Clear(_paid, 0, Kinds); Array.Clear(_n, 0, Kinds); Array.Clear(_noSpare, 0, Kinds);
            _heroRepay = _plDue = _plPaid = _tollShort = 0; _strays = _noPayer = _belowReserve = _tollNotSeen = _overflow = _heroLate = 0;
            _prisChainN = _prisChainWar = 0; _prisChainPaid = _prisChainDebt = _prisChainLost = 0;
        }

        internal static void Reset() { ClearDay(); Array.Clear(_stack, 0, _stack.Length); _depth = 0; }
    }
}
