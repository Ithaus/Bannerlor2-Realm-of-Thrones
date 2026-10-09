using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PACZKA 112 - UTARG WSI BEZ ZNIKANIA (projekt etapu 2 krok B, rozdz. "110 + 112 + klucz 114"; D-2 audytu 12, Z15-2 audytu 15; galaz
    /// `paczki/112-k7-utarg-wsi` 355aa6b przeniesiona na noc/sklad10). Zasada: z gotowki, ktora wies dostaje za swoj towar, pan bierze podatek
    /// wedle dekretu (BK: 70%, Low 50%, High 90%), a cala reszta zostaje w kiesie wsi. Nic nie znika. Trzy miejsca, w ktorych gra i BK gubily
    /// te gotowke:
    ///
    /// 1. POWROT TABORU DO WSI (VillageTakingsWhole). Latka BK VillagerSettlementEnterPatch (prefiks na
    ///    VillagerCampaignBehavior.OnSettlementEntered, zwraca false) liczy podatek z czynnego modelu, daje wsi
    ///    (int)((utarg - podatek) * 0.5f), zeruje sakwe taboru i dopisuje podatek przez EstateData.AccumulateTradeTax
    ///    (udzial wlascicieli majatkow - GiveGoldAction nic -> bohater - reszta na licznik podatku wsi). Druga polowa reszty
    ///    przepada. Para prefiks (First) / postfiks (Last) na tej samej metodzie: prefiks zapamietuje sakwe taboru, kiese
    ///    i licznik wsi, a nasluch HeroOrPartyTradedGold zbiera wyplaty majatkow; postfiks liczy, ile z oddanej gotowki nie
    ///    trafilo nigdzie, i dopisuje DOKLADNIE te roznice - nie staly procent, wiec gdy BK zmieni swoja latke, niczego nie
    ///    podwoimy. Podatek, majatki i licznik pana zostaja takie, jak je policzyl BK. Gdy BK nie dopisal podatku nigdzie
    ///    (osada bez danych BK), brakujaca czesc podatku idzie na licznik pana, nie do kiesy.
    ///    Dwa sufity: pan nigdy ponad podatek z modelu, wies nigdy ponad (utarg - podatek).
    /// 2. TOWAR KUPIONY WE WSI (VillageFoodSalesKept). SellItemsAction.ApplyInternal dopisuje wsi cene, po czym zdejmuje
    ///    cena x GetVillageTaxRatio (1.0; 0.95 z polityka) i - inaczej niz w miescie - nie dopisuje tego do zadnego licznika.
    ///    Kupcy to partie lordow (vanilla PartiesBuyFoodCampaignBehavior i BK BKPartyBehavior - glownie zywnosc). Para na tej
    ///    metodzie mierzy, ile zaplacil kupiec i ile zostalo wsi; to, co zniklo, wraca w CALOSCI do kiesy wsi (112-p, jedna regula
    ///    gracz/AI - 2.0b): gdy gracz kupuje we wsi przez ekran handlu, gra wplaca cala cene do kiesy wsi (InventoryLogic.DoneLogic ->
    ///    InventoryListener.SetGold), bez udzialu pana; tak samo teraz lord AI. Pan dostaje swoje pozniej renta wsi (PopulationLaw,
    ///    20% kiesy dziennie). Dotad czesc pana wedle dekretu szla na licznik podatku wsi - lord kupujacy we wlasnej wsi odzyskiwal tak
    ///    ok. 70% wlasnego wydatku (Z8), a gracz nie (dwie reguly).
    /// 3. SAKWA ZNISZCZONEGO TABORU (VillagerPurseSurvives; decyzja D - Jeff 09.10 04:25). Rozbitemu w bitwie gra zdejmuje 10% sakwy
    ///    dla zwyciezcow (CalculatePlunderedGoldAmountFromDefeatedParty), reszta ginie razem z partia; rozwiazanemu ginie wszystko.
    ///    Reszta idzie do zwyciezcy (jak przy poddaniu sie taboru graczowi gra oddaje cala sakwe): wodzowi partii albo kiesie bandy;
    ///    tabor rozwiazany albo bez zwyciezcy, ktory moglby ja wziac (martwy wodz, partia bez kiesy handlowej, nieumarli) - sakwa
    ///    wraca do wsi i jest dzielona jak utarg. Korona 1/9 z tej sakwy dopiero z paczka 178 (z licznikow doby - CartToLords).
    ///    112-p (2.0b, zapasowy odbiorca): tabor bez wsi macierzystej - kasa jego osady macierzystej (miasto, zamek), a bez niej kasa
    ///    najblizszego miasta (jak sakiewki rozbitych partii w MenPurse); w linii "zapasowemu odbiorcy".
    ///
    /// Majatki BK dostaja udzial tylko z podatku liczonego przez BK przy powrocie taboru (pkt 1) - z pkt 2 i z sakwy
    /// rozwiazanego taboru nie (dzis tez nic z nich nie maja).
    /// Linia dzienna "Utarg wsi (112)" (z pomiarem band - S16, R9) i pozycje w ksiedze pieniadza (MoneyLedger i ksiega obiegu czytaja
    /// liczniki doby z tej klasy albo widza zmiane w swoich oknach).
    /// Hak kiesy ludu (KL, etap 5; projekt etapu 2 rozdz. 2.0b): VillageRentYieldsToFees - renta wsi (PopulationLaw, 20% kiesy dziennie)
    /// ustepuje stalym oplatom wsi z paczki 173; do etapu 5 = false (renta jak dotad).
    /// </summary>
    internal static class VillageTakings
    {
        /// <summary>Hak kiesy ludu (KL, etap 5): renta wsi ustepuje stalym oplatom (173). Do etapu 5 = false - PopulationLaw pobiera rente wsi jak dotad.</summary>
        internal static readonly bool VillageRentYieldsToFees = false;

        /// <summary>Stan przed latka BK: sakwa taboru, kiesa i licznik podatku wsi macierzystej (i wsi wejscia, gdy inna).</summary>
        internal sealed class Entry
        {
            public MobileParty Party; public Village Home, Here;
            public int Gold, HomeGold, HomeTax, HereGold, HereTax;
        }

        /// <summary>Stan przed sprzedaza z magazynu wsi: kiesa wsi i zloto platnika.</summary>
        internal sealed class Sale
        {
            public Village V; public PartyBase Buyer;
            public int VillageGold; public long PayerGold;
        }

        // okno "tabor oddaje utarg": wyplaty majatkow BK miedzy naszym prefiksem a postfiksem
        private static Entry _open;
        private static CampaignTime _openTime;
        private static long _openEstates;

        private static bool _enterHooked, _saleHooked, _hooksLogged;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // ------------------------------------------------------------ liczniki doby
        private static int _tReturns, _tNoLoss, _tOver, _tStale, _tAddTaxN;
        private static long _tHanded, _tKept, _tTax, _tEstates, _tGone, _tAddPurse, _tAddTax, _tLeft;
        private static readonly List<string> _tNoBk = new List<string>();
        private static int _fN, _fShort;
        private static long _fPaid, _fGone, _fAddPurse;
        private static int _cFoughtN, _cDisbandN, _cToHeroN, _cToBandN, _cSpareN;
        private static long _cFought, _cDisband, _cToHero, _cToBand, _cToPlayer, _cHomeTax, _cHomePurse, _cSpare, _cLost;
        private static int _stumbles;

        // odczyt dla ksiegi pieniadza (MoneyLedger.Daily biegnie tuz przed naszym Daily - te same granice doby)
        internal static long TakingsToPurse { get { return _tAddPurse; } }
        internal static long TakingsToTax { get { return _tAddTax; } }
        internal static long FoodToPurse { get { return _fAddPurse; } }
        internal static long CartToLords { get { return _cToHero; } }
        internal static long CartToBands { get { return _cToBand; } }
        internal static long CartToVillages { get { return _cHomePurse + _cHomeTax; } }
        /// <summary>112-p: sakwy taborow bez wsi macierzystej oddane zapasowemu odbiorcy (kasa osady macierzystej albo najblizszego miasta).</summary>
        internal static long CartToSpare { get { return _cSpare; } }
        /// <summary>112-p: "bez odbiorcy" doby - utarg nieprzypisany, towar kupiony we wsi i sakwy, ktore zniknely (Obieg, prog 0).</summary>
        internal static long Unrouted { get { return _tLeft + Math.Max(0L, _fGone - _fAddPurse) + _cLost; } }

        internal static void Reset()
        {
            _open = null; _openEstates = 0; _hooksLogged = false; _errSites.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _tReturns = _tNoLoss = _tOver = _tStale = _tAddTaxN = 0;
            _tHanded = _tKept = _tTax = _tEstates = _tGone = _tAddPurse = _tAddTax = _tLeft = 0;
            _tNoBk.Clear();
            _fN = _fShort = 0; _fPaid = _fGone = _fAddPurse = 0;
            _cFoughtN = _cDisbandN = _cToHeroN = _cToBandN = _cSpareN = 0;
            _cFought = _cDisband = _cToHero = _cToBand = _cToPlayer = _cHomeTax = _cHomePurse = _cSpare = _cLost = 0;
            _stumbles = 0;
        }

        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        /// <summary>Wyjatek przy jednym zdarzeniu: liczony zawsze, do pliku raz na miejsce (mechanizmu nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        // ------------------------------------------------------------ wspolna regula podzialu
        /// <summary>Podatek pana od gotowki wsi wedle czynnego modelu (ten sam, ktorym licza gra i BK), w granicach 0..income.</summary>
        private static int TaxOf(Village v, int income)
        {
            if (v == null || income <= 0) return 0;
            try
            {
                var m = Campaign.Current.Models.SettlementTaxModel;
                int t = m != null ? m.CalculateVillageTaxFromIncome(v, income) : 0;
                return t < 0 ? 0 : (t > income ? income : t);
            }
            catch (Exception e) { Stumble("VillageTakings.TaxOf", e); return 0; }   // model padl: calosc zostaje w kiesie wsi - nic nie znika
        }

        /// <summary>
        /// Oddaje wsi `amount`: podatek od `income` (nie wiecej niz `amount`) na licznik podatku wsi, reszta do kiesy.
        /// Razem dokladnie `amount` - ani denara wiecej.
        /// </summary>
        private static void Split(Village v, int income, int amount, out int tax, out int purse)
        {
            tax = 0; purse = 0;
            if (v == null || amount <= 0) return;
            tax = Math.Min(TaxOf(v, income), amount);
            purse = amount - tax;
            if (tax > 0) v.TradeTaxAccumulated += tax;
            if (purse > 0) v.ChangeGold(purse);
        }

        // ------------------------------------------------------------ 1. powrot taboru do wsi
        /// <summary>
        /// VillagerCampaignBehavior.OnSettlementEntered(MobileParty, Settlement, Hero) - przed latka BK (Priority.First).
        /// Tylko odczyt. Prefiks jest void, ale ma parametry-obiekty, wiec Harmony pominie go, gdyby wczesniejszy prefiks
        /// zwrocil false - wtedy postfiks nie ma stanu i niczego nie dopisuje (strona bezpieczna).
        /// </summary>
        public static void EnterPrefix(MobileParty __0, Settlement __1, out Entry __state)
        {
            __state = null;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.VillageTakingsWhole || __0 == null || __1 == null) return;
                if (!__0.IsVillager || !__0.IsActive || !__1.IsVillage || !Live) return;
                int gold = __0.PartyTradeGold;
                if (gold <= 0) return;                       // powrot bez utargu
                var hs = __0.HomeSettlement;
                var home = hs != null ? hs.Village : null;
                if (home == null) return;
                if (_open != null) _tStale++;                // poprzednie okno nie doszlo do postfiksu (wyjatek w cudzym kodzie)
                var here = __1.Village;
                if (ReferenceEquals(here, home)) here = null;
                var e = new Entry
                {
                    Party = __0, Home = home, Here = here, Gold = gold,
                    HomeGold = home.Gold, HomeTax = home.TradeTaxAccumulated,
                    HereGold = here != null ? here.Gold : 0, HereTax = here != null ? here.TradeTaxAccumulated : 0
                };
                _open = e; _openTime = CampaignTime.Now; _openEstates = 0;
                __state = e;
            }
            catch (Exception e) { __state = null; _open = null; Stumble("VillageTakings.EnterPrefix", e); }
        }

        /// <summary>Po latce BK i po cudzych postfiksach (Priority.Last): dopisuje wsi to, co z oddanej gotowki nie trafilo nigdzie.</summary>
        public static void EnterPostfix(Entry __state)
        {
            if (__state == null) return;
            try
            {
                bool mine = ReferenceEquals(_open, __state) && _openTime == CampaignTime.Now;
                long estates = _openEstates;
                if (ReferenceEquals(_open, __state)) _open = null;
                if (!mine) { _tStale++; return; }            // okno nadpisane - bez pomiaru wyplat majatkow niczego nie dopisujemy
                var home = __state.Home; var here = __state.Here;
                int handed = __state.Gold - __state.Party.PartyTradeGold;
                if (handed <= 0) return;                     // tabor niczego nie oddal
                long kept = (long)home.Gold - __state.HomeGold;
                long tax = (long)home.TradeTaxAccumulated - __state.HomeTax;
                if (here != null)                            // tabor w obcej wsi: BK dopisuje podatek na licznik wsi wejscia
                {
                    tax += (long)here.TradeTaxAccumulated - __state.HereTax;
                    kept += (long)here.Gold - __state.HereGold;
                }
                long gone = handed - kept - tax - estates;
                _tReturns++; _tHanded += handed; _tKept += kept; _tTax += tax; _tEstates += estates;
                if (gone <= 0) { if (gone < 0) _tOver++; else _tNoLoss++; return; }
                _tGone += gone;
                // oddajemy tylko to, co wyszlo z sakwy taboru i nie ma odbiorcy; ubytek kiesy albo licznika zrobiony w tym oknie
                // przez kogos innego (ujemna zmiana) to nie nasz utarg - zostaje w liczniku "nieprzypisane"
                long owned = Math.Max(0L, kept) + Math.Max(0L, tax + estates);
                long cap = Math.Max(0L, handed - owned);
                if (gone > cap) { _tLeft += gone - cap; gone = cap; }
                if (gone <= 0) return;
                // podatek nalezny z tego samego modelu; to, czego BK nie dopisal nigdzie (brak danych BK osady), idzie na licznik pana.
                // Ujemna zmiana licznika w tym oknie (cudza latka) liczy sie jak zero - tak samo jak ubytek kiesy wyzej: inaczej pan
                // dostalby z sakwy taboru wiecej niz podatek z modelu
                int due = TaxOf(home, handed);
                long miss = due - Math.Max(0L, tax + estates);
                if (miss < 0) miss = 0;
                if (miss > gone) miss = gone;
                // wies dostaje reszte, ale nigdy ponad swoja czesc (utarg - podatek): gdyby zniklo wiecej, zostaje to w liczniku "nieprzypisane"
                long purse = gone - miss;
                long room = (long)handed - due - Math.Max(0L, kept);
                if (room < 0) room = 0;
                if (purse > room) { _tLeft += purse - room; purse = room; }
                if (miss > 0) { home.TradeTaxAccumulated += (int)miss; _tAddTax += miss; _tAddTaxN++; }
                if (purse > 0) { home.ChangeGold((int)purse); _tAddPurse += purse; }
                if (miss > 0 && _tNoBk.Count < 5)            // nazwa wsi do linii logu - dopiero po ruchu zlota, we wlasnym try
                {
                    try { string n = home.Name != null ? home.Name.ToString() : "?"; if (!_tNoBk.Contains(n)) _tNoBk.Add(n); }
                    catch (Exception e) { Stumble("VillageTakings.EnterPostfix(nazwa)", e); }
                }
            }
            catch (Exception e) { Stumble("VillageTakings.EnterPostfix", e); }
        }

        /// <summary>Nasluch HeroOrPartyTradedGold: wyplata "nic -> bohater" w oknie powrotu taboru to udzial wlasciciela majatku BK w podatku.</summary>
        internal static void OnGoldTraded((Hero, PartyBase) giver, (Hero, PartyBase) recipient, (int, string) amount, bool showNotification)
        {
            if (_open == null) return;
            try
            {
                int a = amount.Item1;
                if (a <= 0 || giver.Item1 != null || giver.Item2 != null || recipient.Item1 == null) return;
                if (_openTime != CampaignTime.Now) return;
                _openEstates += a;
            }
            catch (Exception e) { Stumble("VillageTakings.OnGoldTraded", e); }
        }

        // ------------------------------------------------------------ 2. towar kupiony we wsi
        /// <summary>Zloto, z ktorego SellItemsAction pobiera zaplate: kiesa wodza kupujacej partii i (gdy to nie ta sama) kiesa partii.</summary>
        private static long PayerGold(PartyBase b)
        {
            if (b == null) return 0;
            long g = 0;
            var hero = b.LeaderHero;
            if (hero != null) g += hero.Gold;
            var mp = b.MobileParty;
            if (mp != null && !(mp.IsLordParty && mp.LeaderHero != null)) g += mp.PartyTradeGold;   // kiesa partii rodu z wodzem to kiesa wodza
            return g;
        }

        /// <summary>SellItemsAction.ApplyInternal(sprzedawca, kupiec, towar, ile, osada) - tylko gdy sprzedaje wies. Sam odczyt.</summary>
        public static void SalePrefix(PartyBase __0, PartyBase __1, out Sale __state)
        {
            __state = null;
            try
            {
                if (__0 == null || !__0.IsSettlement) return;
                var st = __0.Settlement;
                var v = st != null ? st.Village : null;
                if (v == null || !Live) return;
                __state = new Sale { V = v, Buyer = __1, VillageGold = v.Gold, PayerGold = PayerGold(__1) };
            }
            catch (Exception e) { __state = null; Stumble("VillageTakings.SalePrefix", e); }
        }

        /// <summary>
        /// Po sprzedazy: zaplacone minus to, co zostalo wsi = skasowane przez gre. Pomiar zawsze; zwrot (calosc do kiesy wsi - 112-p)
        /// przy wlaczonym ustawieniu. Postfiks (Last) biegnie PRZED finalizerem okna "prowizja" ksiegi obiegu (CirculationWindows.SellFin)
        /// - ksiega widzi zmiane kiesy wsi po zwrocie (w nicosc tylko to, czego nie oddalismy).
        /// </summary>
        public static void SalePostfix(Sale __state)
        {
            if (__state == null) return;
            try
            {
                var v = __state.V;
                long paid = __state.PayerGold - PayerGold(__state.Buyer);
                if (paid < 0) paid = 0;
                long kept = (long)v.Gold - __state.VillageGold;
                long gone = paid - kept;
                if (paid == 0 && kept == 0) return;          // nic nie sprzedano (brak kupca i sprzedawcy-partii)
                _fN++; _fPaid += paid;
                if (gone <= 0) return;
                if (gone > paid) _fShort++;                  // kupiec zaplacil mniej, niz gra zdjela wsi - ubylo z wlasnej kiesy wsi
                if (gone > int.MaxValue) gone = int.MaxValue;
                _fGone += gone;
                var s = Settings.Current;
                if (s == null || !s.VillageFoodSalesKept) return;
                // 112-p: cala skasowana cena do kiesy wsi - jak przy zakupie gracza z ekranu handlu (gra: 100% ceny do kiesy wsi)
                v.ChangeGold((int)gone);
                _fAddPurse += gone;
            }
            catch (Exception e) { Stumble("VillageTakings.SalePostfix", e); }
        }

        // ------------------------------------------------------------ 3. sakwa zniszczonego taboru
        /// <summary>
        /// MobilePartyDestroyed: tabor wsi znika z mapy z gotowka w sakwie. Rozbitemu w bitwie gra zdjela juz dzialke zwyciezcow
        /// (MapEventParty.CommitGoldChanges biegnie przed zniszczeniem partii) - tu widzimy sama reszte, ktora przepadala.
        /// Nasluch wpiety PRZED ksiega obiegu (CirculationWindows.OnPartyDestroyed liczy kiesy partii, ktore znikaja) - widzi ona sakwe po nas.
        /// </summary>
        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                if (party == null || !party.IsVillager) return;
                int g = party.PartyTradeGold;
                if (g <= 0) return;
                bool fought = destroyer != null;
                if (fought) { _cFoughtN++; _cFought += g; } else { _cDisbandN++; _cDisband += g; }
                var s = Settings.Current;
                if (s == null || !s.VillagerPurseSurvives || !Live) { _cLost += g; return; }
                var win = fought ? destroyer.MobileParty : null;
                if (win != null && !ReferenceEquals(win, party) && win.IsActive && !Undead.Party(win))   // trup monety nie bierze - sakwa wraca do wsi
                {
                    var hero = win.LeaderHero;
                    if (hero != null && hero.IsAlive)
                    {
                        GiveGoldAction.ApplyForPartyToCharacter(party.Party, hero, g);
                        int moved = g - party.PartyTradeGold;
                        _cToHero += moved; _cToHeroN++; _cLost += g - moved;
                        if (hero == Hero.MainHero) _cToPlayer += moved;
                        return;
                    }
                    if (hero == null && win.IsPartyTradeActive)
                    {
                        GiveGoldAction.ApplyForPartyToParty(party.Party, win.Party, g, true);
                        int moved = g - party.PartyTradeGold;
                        _cToBand += moved; _cToBandN++; _cLost += g - moved;
                        return;
                    }
                }
                // rozwiazany albo bez zwyciezcy, ktory moglby wziac sakwe: gotowka wraca do wsi i dzieli sie jak utarg
                var hs = party.HomeSettlement;
                var home = hs != null ? hs.Village : null;
                if (home == null)
                {
                    // 112-p (2.0b): zapasowy odbiorca - kasa osady macierzystej (miasto, zamek), a bez niej kasa najblizszego miasta
                    var spare = hs != null && hs.Town != null ? hs : NearestTown(party);
                    if (spare == null || spare.Town == null) { _cLost += g; return; }
                    party.PartyTradeGold = 0;
                    spare.Town.ChangeGold(g);
                    _cSpare += g; _cSpareN++;
                    MoneyLedger.Note169(MoneyLedger.N169CartPurse, spare, g);   // kasa osady poza oknami ksiegi (tylko licznik)
                    return;
                }
                party.PartyTradeGold = 0;
                int tax, purse;
                Split(home, g, g, out tax, out purse);
                _cHomeTax += tax; _cHomePurse += purse;
                if (purse > 0) MoneyLedger.Note169(MoneyLedger.N169CartPurse, home.Settlement, purse);   // kiesa wsi poza oknami ksiegi (tylko licznik)
            }
            catch (Exception e) { Stumble("VillageTakings.OnPartyDestroyed", e); }
        }

        /// <summary>112-p: najblizsze miasto do pozycji partii (zapasowy odbiorca sakwy - ta sama regula co sakiewki rozbitych partii w MenPurse).</summary>
        private static Settlement NearestTown(MobileParty mp)
        {
            Settlement best = null; float bd = float.MaxValue;
            try
            {
                if (mp == null) return null;
                var p = mp.GetPosition2D;
                foreach (var t in Settlement.All)
                {
                    if (t == null || !t.IsTown || t.Town == null) continue;
                    float d = p.DistanceSquared(t.GetPosition2D);
                    if (d < bd) { bd = d; best = t; }
                }
            }
            catch (Exception e) { Stumble("VillageTakings.NearestTown", e); }
            return best;
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            try
            {
                var enter = AccessTools.Method(typeof(VillagerCampaignBehavior), "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) });
                if (enter != null && enter.ReturnType == typeof(void))
                {
                    // prefiks przed latka BK (ktora zwraca false i pomija oryginal), postfiks po wszystkich cudzych postfiksach
                    h.Patch(enter, prefix: new HarmonyMethod(typeof(VillageTakings), nameof(EnterPrefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(VillageTakings), nameof(EnterPostfix)) { priority = Priority.Last });
                    _enterHooked = true;
                    done.Add("powrot taboru do wsi");
                }
                else miss.Add("powrot taboru do wsi (brak VillagerCampaignBehavior.OnSettlementEntered)");
            }
            catch (Exception e) { miss.Add("powrot taboru do wsi (" + e.Message + ")"); Log.Error("VillageTakings.ApplyAll(tabor)", e); }
            try
            {
                var sell = AccessTools.Method(typeof(SellItemsAction), "ApplyInternal");
                var ps = sell != null ? sell.GetParameters() : null;
                if (sell != null && sell.IsStatic && ps.Length >= 2 && ps[0].ParameterType == typeof(PartyBase) && ps[1].ParameterType == typeof(PartyBase))
                {
                    h.Patch(sell, prefix: new HarmonyMethod(typeof(VillageTakings), nameof(SalePrefix)) { priority = Priority.First },
                                  postfix: new HarmonyMethod(typeof(VillageTakings), nameof(SalePostfix)) { priority = Priority.Last });
                    _saleHooked = true;
                    done.Add("sprzedaz z magazynu wsi");
                }
                else miss.Add("sprzedaz z magazynu wsi (brak SellItemsAction.ApplyInternal(PartyBase, PartyBase, ..))");
            }
            catch (Exception e) { miss.Add("sprzedaz z magazynu wsi (" + e.Message + ")"); Log.Error("VillageTakings.ApplyAll(towar)", e); }
            var s = Settings.Current;
            Log.Info("VillageTakings (112): utarg wsi bez znikania - latki wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "zadne")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "")
                     + "; sakwy zniszczonych taborow - z nasluchu zdarzen. Ustawienia: reszta utargu do kiesy wsi " + OnOff(s != null && s.VillageTakingsWhole)
                     + ", towar kupiony we wsi " + OnOff(s != null && s.VillageFoodSalesKept) + ", sakwa zniszczonego taboru " + OnOff(s != null && s.VillagerPurseSurvives)
                     + "; hak kiesy ludu (etap 5) VillageRentYieldsToFees " + (VillageRentYieldsToFees ? "true" : "false") + ".");
        }

        private static string OnOff(bool on) { return on ? "WLACZONE" : "wylaczone"; }

        private static string Patches(MethodBase m)
        {
            if (m == null) return "brak metody";
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return "bez latek";
            var sb = new StringBuilder();
            sb.Append("prefiksy [");
            bool first = true;
            foreach (var p in info.Prefixes) { if (!first) sb.Append(", "); first = false; sb.Append(p.owner).Append(':').Append(p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.Name : "?").Append(" (").Append(p.priority).Append(')'); }
            sb.Append("], postfiksy [");
            first = true;
            foreach (var p in info.Postfixes) { if (!first) sb.Append(", "); first = false; sb.Append(p.owner).Append(':').Append(p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.Name : "?").Append(" (").Append(p.priority).Append(')'); }
            sb.Append(']');
            if (info.Finalizers.Count > 0) sb.Append(", finalizery ").Append(info.Finalizers.Count);
            if (info.Transpilers.Count > 0) sb.Append(", transpilery ").Append(info.Transpilers.Count);
            return sb.ToString();
        }

        /// <summary>Raz na sesje, w pierwszej dobie (wszystkie mody maja juz swoje latki): kto siedzi na tych samych metodach i czy kiesa wsi ma sufit gry.</summary>
        private static string HooksLine()
        {
            try
            {
                var enter = AccessTools.Method(typeof(VillagerCampaignBehavior), "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) });
                var sell = AccessTools.Method(typeof(SellItemsAction), "ApplyInternal");
                var tick = AccessTools.Method(typeof(Village), "DailyTick");
                var ti = tick != null ? Harmony.GetPatchInfo(tick) : null;
                bool capOff = ti != null && ti.Prefixes.Count > 0;
                var m = Campaign.Current.Models.SettlementTaxModel;
                return "Utarg wsi (112) - latki: powrot taboru - " + Patches(enter) + "; sprzedaz z magazynu wsi - " + Patches(sell)
                       + "; model podatku " + (m != null ? m.GetType().FullName : "brak")
                       + "; sufit kiesy wsi: gra obcina ja co dobe do 1000 (Village.DailyTick) - latka innego moda na tej metodzie: "
                       + (capOff ? "jest (" + Patches(tick) + ")" : "BRAK - wszystko ponad 1000 w kiesie wsi zniknie nastepnej doby, dopisany utarg tez") + ".";
            }
            catch (Exception e) { return "Utarg wsi (112) - latki: blad odczytu (" + e.Message + ")."; }
        }

        private static string Pct(long part, long whole) { return whole != 0 ? (100.0 * part / whole).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "-"; }

        /// <summary>
        /// Pomiar band (S16, R9; audyt 15 Z15-3): bandy po 112 dostaja sakwy rozbitych taborow (77% taborow rozbijaja bandy) - za zloto
        /// kupuja sprzet u pasera i awansuja. Prog testu: sredni tier, zloto, awanse u pasera i rozbite tabory najwyzej +20% wobec biegu
        /// bez 112. Tu: bandy na mapie (bez Innych), ludzie, sredni tier ludzi, kiesy band; awanse u pasera sa w linii "Wyrzutki", rozbite
        /// tabory w linii "Dowoz".
        /// </summary>
        private static string BandsSegment()
        {
            try
            {
                int bands = 0; long men = 0, tierSum = 0, gold = 0;
                foreach (var p in MobileParty.AllBanditParties)
                {
                    if (p == null || !p.IsActive || Undead.Party(p)) continue;
                    bands++; gold += p.PartyTradeGold;
                    var r = p.MemberRoster;
                    if (r == null) continue;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var ch = el.Character;
                        if (ch == null || el.Number <= 0) continue;
                        men += el.Number; tierSum += (long)ch.Tier * el.Number;
                    }
                }
                return " | bandy (pomiar S16): band " + bands + ", ludzi " + men + ", sredni tier " + (men > 0 ? ((double)tierSum / men).ToString("0.000", CultureInfo.InvariantCulture) : "-")
                       + ", kiesy band " + gold + " (bez Innych)";
            }
            catch (Exception e) { Stumble("VillageTakings.BandsSegment", e); return " | bandy (pomiar S16): blad odczytu"; }
        }

        // ------------------------------------------------------------ raz na dobe
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var s = Settings.Current;
                if (s == null) return;
                if (!_hooksLogged) { _hooksLogged = true; Log.Info(HooksLine()); }
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder(1024);
                sb.Append("Utarg wsi (112): dzien ").Append(day).Append(" | powroty taborow z utargiem ");
                if (!_enterHooked) sb.Append("- latka nie jest wpieta");
                else if (!s.VillageTakingsWhole) sb.Append("- WYLACZONE w ustawieniach (podzial utargu mierzy linia \"Przeplywy osad:\")");
                else
                {
                    sb.Append(_tReturns).Append(": oddane ").Append(_tHanded).Append(" = pan (licznik podatku) ").Append(_tTax).Append(" (").Append(Pct(_tTax + _tAddTax, _tHanded))
                      .Append(" po dopisaniu) + wlasciciele majatkow BK ").Append(_tEstates).Append(" + kiesa wsi od BK ").Append(_tKept).Append(" + bez odbiorcy ").Append(_tGone)
                      .Append("; z tego dopisane kiesom wsi ").Append(_tAddPurse).Append(" (wies razem ").Append(Pct(_tKept + _tAddPurse, _tHanded)).Append(" utargu)")
                      .Append(", licznikom panow ").Append(_tAddTax).Append(" (").Append(_tAddTaxN).Append(" powrotow bez podatku BK")
                      .Append(_tNoBk.Count > 0 ? ": " + string.Join(", ", _tNoBk.ToArray()) : "").Append("), nieprzypisane ").Append(_tLeft)
                      .Append("; powroty, w ktorych nic nie zniklo ").Append(_tNoLoss).Append(", z nadwyzka rozliczona przez BK ").Append(_tOver)
                      .Append(", okna niedomkniete ").Append(_tStale);
                }
                sb.Append(" | towar kupiony we wsiach ");
                if (!_saleHooked) sb.Append("- latka nie jest wpieta");
                else
                {
                    sb.Append(_fN).Append(" zakupow: kupcy zaplacili ").Append(_fPaid).Append(", gra skasowala ").Append(_fGone).Append(" (").Append(Pct(_fGone, _fPaid))
                      .Append("; w tym ").Append(_fShort).Append(" zakupow, w ktorych zdjela wiecej, niz zaplacono)");
                    if (s.VillageFoodSalesKept) sb.Append("; oddane w calosci kiesom wsi (jak zakup gracza) ").Append(_fAddPurse).Append(", zniklo ").Append(_fGone - _fAddPurse);
                    else sb.Append("; zwrot WYLACZONY w ustawieniach - zniklo ").Append(_fGone);
                }
                sb.Append(" | tabory zniszczone z gotowka: w bitwie ").Append(_cFoughtN).Append(" (").Append(_cFought).Append(" po dzialce zwyciezcow), rozwiazane ")
                  .Append(_cDisbandN).Append(" (").Append(_cDisband).Append(")");
                if (s.VillagerPurseSurvives)
                    sb.Append("; oddane: wodzom zwyciezcow ").Append(_cToHero).Append(" (").Append(_cToHeroN).Append(" taborow").Append(_cToPlayer > 0 ? "; w tym graczowi " + _cToPlayer : "")
                      .Append("), kiesom band ").Append(_cToBand).Append(" (").Append(_cToBandN).Append(" taborow), wsiom macierzystym ").Append(_cHomePurse)
                      .Append(" do kies i ").Append(_cHomeTax).Append(" na liczniki panow, zapasowemu odbiorcy (bez wsi macierzystej - kasa osady macierzystej albo najblizszego miasta) ")
                      .Append(_cSpare).Append(" (").Append(_cSpareN).Append(" taborow), zniklo ").Append(_cLost);
                else sb.Append("; zwrot WYLACZONY w ustawieniach - zniklo ").Append(_cLost);
                sb.Append(" | zamkniete ujscia razem (dopisane wsiom, panom i zwyciezcom): ")
                  .Append(_tAddPurse + _tAddTax + _fAddPurse + _cToHero + _cToBand + _cHomePurse + _cHomeTax + _cSpare)
                  .Append("; bez odbiorcy razem (nieprzypisane + towar zniklo + sakwy zniklo; prog 0) ").Append(Unrouted);
                sb.Append(BandsSegment());
                if (_stumbles > 0) sb.Append(" | wyjatki zlapane ").Append(_stumbles).Append(" (pierwszy z kazdego miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("VillageTakings.Daily", e); }
            finally { ClearDay(); }
        }
    }
}
