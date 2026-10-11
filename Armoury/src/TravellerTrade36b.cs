using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 36b (decyzja Jeffa 11.10 "tak": wedrowcy BK z prawdziwym towarem, kupionym w miescie i sprzedanym u celu). Wylacznik TravellersRealGoods
    /// (dziala tylko przy wlaczonym 3.6 TravellersNoGoods - inaczej ladunek BK z niczego zostaje i nie ma miejsca na prawdziwy).
    ///
    /// BK (dekompilacja BannerKings.dll z gry, 11.10): BKPartyBehavior.DailySettlementTickImpl (kazda osada z Town - miasto albo zamek) ->
    /// DecideSendTravellers (5% na dobe) -> cel = PIERWSZE lenno z GetTownsToTravel (Town.AllFiefs: miasta i zamki blizej niz 100, bez wojny w chwili
    /// budowy pamieci) -> SendTravellerParty (chlopi 30-70, rzemieslnicy 15-30, szlachta 10-15 + eskorta) -> PopulationPartyComponent.CreateTravellerParty:
    /// GivePackAnimals (juczne = 1/4 skladu), przy trading=false GiveFood (zywnosc N x 16/Value x U^4 kazdego rodzaju - ok. 3.3 jednostki na czlowieka,
    /// 30-60 dob zapasu) i GiveItems (towar - zdejmuje go 3.6). U celu (albo w domu, gdy cel oblegany) OnSettlementEntered -> AddPopulationPartyBehavior:
    /// wedrowcy (PopulationType != None, nie Trading, nie niewolnicy, nie zywnosc) dopisani do ludnosci osady, partia zniszczona RAZEM z ladunkiem.
    ///
    /// TERAZ (36b, przy wlaczonym wylaczniku):
    ///  1. ZAPASY DROGI NIE Z NICZEGO. Prefiksy GivePackAnimals i GiveFood (tylko w czasie CreateTravellerParty - flaga; GiveFood wolaja tez inne
    ///     komponenty BK, tych nie ruszamy) pomijaja dar BK. Zywnosc na droge: z polki osady pochodzenia (miasto albo zamek - jak zaopatrzenie BK 3.4),
    ///     najtansza najpierw, tyle ile droga potrzebuje: dzienne zuzycie partii (model gry, co najmniej 1/20 jednostki na czlowieka) x dni drogi
    ///     (odleglosc / (predkosc partii z modelu x 16 h marszu na dobe) w gore + 2 doby zapasu, 2-8 dob; droga nieznana - 4). Zwierzat jucznych nie ma wcale (pozycja planu 3.11: "albo wcale,
    ///     gdy ladunek miesci sie w udzwigu ludzi") - zywnosc na kilka dob i maly towar mieszcza sie w udzwigu samych ludzi (10 + 20 na zdrowego),
    ///     a sprzedaz jucznych u celu (kara gry +0.8 dla nie-karawany) i stale przesuwanie ich z miasta A do B (BK wysyla z A zawsze do tego samego
    ///     celu) bylyby gorsze niz brak. Gdy na polce brak zywnosci (licznik "brak na polce" - zwykle zamki), reszte potrzeby daje ZAPAS ZYWNOSCI
    ///     osady pochodzenia (Fief.FoodStocks - miasto i zamek; najwyzej polowa zapasu): jednostka zapasu = sztuka zywnosci (BK GetItemFoodValue,
    ///     dostawa konwoju zywnosci) = 100 punktow PartyBase.RemainingFoodPercentage - partia dostaje ja jako zapas bez sztuk (gra zjada go przed
    ///     sztukami: FoodConsumptionBehavior.PartyConsumeFood odejmuje dzienne zuzycie od RemainingFoodPercentage, sztuki je dopiero ponizej 0),
    ///     a co z niego zostanie u celu, wraca do zapasu osady, do ktorej weszli (poprawka po recenzji: glodujacy wedrowcy dostaja rany -
    ///     PartyHealCampaignBehavior, SlowHealing - a BK u celu liczy rannych podwojnie, Helpers.GetRosterCount = Number + WoundedNumber,
    ///     wiec glod tworzyl ludzi z niczego). Brak takze w zapasie - licznik "brak takze w zapasie".
    ///  2. TOWAR W KOMIS OD KUPCOW MIASTA POCHODZENIA (tylko miasto -> miasto: zamek to nie targ, 171 C2.3; bez wojny i bez oblezenia celu).
    ///     Wedrowcy nie maja wlasnego pieniadza (ludnosc BK bez kiesy; kiesa ludu to etap 5), a kasa miasta to kapital kupcow targu (zasada L1
    ///     PROJEKT-KIESA-LUDU) - kupcy powierzaja im towar z polki (zaliczka na kiese wedrowcow = zaplata za towar w tej samej kasie, wiec bez przeplywu).
    ///     Wybor jak u karawan, wedlug biezacego zysku: cena zakupu u zrodla (Town.GetItemPrice, kupno) i cena, jaka da miasto celu (sprzedaz) - towar
    ///     tylko gdy zysk >= 10% ceny i >= 1 zl; kolejnosc - najwiekszy zysk na zlotowke; kazda sztuka po swojej cenie liczonej od nowa (zasada "ceny hurtu"
    ///     09.10), zakup towaru konczy sie, gdy zysk spadnie ponizej progu. Mala ilosc: budzet = TravellerTradePerHead x ludzie, najwyzej 5% kasy
    ///     pochodzenia i 5% kasy celu; jeden towar najwyzej polowa budzetu i polowa pozycji polki; ladunek do 90% udzwigu. Bez surowcow masowych i kuzni
    ///     (ruda, drewno, skory, skora, len, plotno, welna, wegiel, sztaby - maja wlasne drogi: CaravanBulk wedlug brakow miast, kontrakty 174), bez
    ///     "stolen_goods" i BEZ ZYWNOSCI (poprawka po recenzji: gra zjada w drodze losowa sztuke zywnosci z ladunku - FoodConsumptionBehavior.
    ///     MakeFoodConsumption - wiec zywnosc kupiona na handel szlaby w brzuchy, a u celu liczylaby sie jako "zywnosc", nie "towar"; zywnosc
    ///     w ladunku to tylko zywnosc drogi).
    ///  3. U CELU (prefiks AddPopulationPartyBehavior, priorytet ponad First - PRZED oknem pomiaru O21 ksiegi obiegu, ktore inaczej wzieloby zaplate
    ///     za "konwoj w nicosc"): caly ladunek handlowy (towar, zywnosc, zwierzeta - takze zapasy BK partii sprzed wlaczenia) idzie na polke osady,
    ///     do ktorej weszli (miasto albo zamek), kazda sztuka po swojej cenie sprzedazy liczonej od nowa; kasa osady placi tylko, co ma (reszta na polke
    ///     bez zaplaty - licznik), a utarg idzie do kasy osady pochodzenia (kupcy, ktorzy powierzyli towar - jak zysk karawany do wlasciciela).
    ///     Kiesy wedrowcow (PartyTradeGold) nie ruszamy - zaplata idzie wprost kasa celu -> kasa pochodzenia, wiec w drodze kiesa jest pusta i z rozbita
    ///     partia nie ginie zloto (towar - lup zwyciezcy, jak u karawan). Zostawione na polce celu bez zaplaty i powrot do domu - towar nie ginie.
    ///     Wedrowcy zawroceni do domu: ladunek wraca na polke domu bez zaplaty (kasa placilaby sama sobie). Osada bez targu (Town == null) - nic.
    ///     Zaplata liczona osobno za towar, zywnosc i zwierzeta - zysk wypraw to utarg za towar wobec ceny zakupu towaru, ktory doszedl do obcego
    ///     targu (zywnosc drogi ma osobny rachunek: wydana, zjedzona, na polke celu). Przed liczeniem BK: cywile w skladzie (szeregowi z tym samym
    ///     filtrem w StringId co BK - villager / craftsman / noble) i ilu z nich rannych - tylu BK dopisze drugi raz (licznik; leczenie przed
    ///     liczeniem BK to osobna decyzja Jeffa - poprawiloby tez rannych z bitew).
    ///
    /// Ksiega towarow (GoodsLedger): zakup i sprzedaz w ramkach "handel partii" (FSell) - przewoz miedzy polka a partia znosi sie do zera
    /// ("inne partie kupily / sprzedaly"); zywnosc drogi zjedzona = "zywnosc partii"; dar BK (GiveFood) nie rodzi sie juz w "inne ticki dobowe (osady)"
    /// i nie ginie w "przepadlo z rozbitymi partiami". Ksiega pieniadza (MoneyLedger.Note, rodzaj NTravel): kasa celu -R i kasa pochodzenia +R w tej
    /// samej chwili - przelew miedzy posiadaczami (zloto swiata bez zmian), w linii "Przeplywy osad" para "wedrowcy BK (36b)".
    /// Zapas zywnosci osady (FoodStocks) to nie sztuki - ksiega towarow go nie widzi; jego droga w linii 36b: zapas pochodzenia -> zjedzony w drodze
    /// albo do zapasu osady celu (albo przepadl z partia - wies bez zapasu, partia zniknela).
    /// Wyjatki liczone per partia (potkniecia), bez globalnego wylacznika. Stan (spis wypraw w drodze) czyszczony przy innej kampanii; nie idzie
    /// do zapisu - partie w drodze w chwili wczytania sprzedaja u celu tak samo (licznik "spoza spisu").
    /// </summary>
    internal static class TravellerTrade36b
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.TravellersRealGoods && s.TravellersNoGoods; } }

        private const float MinMargin = 0.10f;   // zysk u celu co najmniej 10% ceny zakupu (i co najmniej 1 zl na sztuce)
        private const float TownShare = 0.05f;   // budzet wyprawy najwyzej 5% kasy pochodzenia i 5% kasy celu
        private const float ShelfShare = 0.5f;   // z jednej pozycji polki najwyzej polowa
        private const float ItemShare = 0.5f;    // jeden towar najwyzej polowa budzetu
        private const float LoadUse = 0.9f;      // ladunek do 90% udzwigu (jak karawany gry)
        private const float StockShare = 0.5f;   // luka zywnosci drogi z zapasu osady (FoodStocks) - najwyzej polowa zapasu (zaloga zamku tez je)
        private const float RoadPace = 2f;       // predkosc partii (jednostek na godzine), gdy model nie odpowie - tylko do liczenia dni drogi
        private const float MarchHours = 16f;    // godzin marszu na dobe (postoje, noc) - ostroznie, zeby zywnosci starczylo
        private const int ExtraDays = 2, MinDays = 2, MaxDays = 8, UnknownRoadDays = 4;

        // surowce masowe (CaravanBulk) i nie-towar BK - poza handlem wedrowcow; sztaby, wegiel, ruda i drewno dodatkowo przez Recipes.MaterialItem
        private static readonly HashSet<string> NotTraded = new HashSet<string> { "iron", "hardwood", "hides", "leather", "flax", "linen", "wool", "charcoal", "stolen_goods" };
        private static HashSet<ItemObject> _smith;

        private static Type _ppc;
        private static PropertyInfo _pTrading, _pSlave, _pKind, _pPop;
        private static bool _inCreate, _createTrading;
        private static string _wiredLine = "nie wpiete";

        private sealed class Venture { public int Units, Food, Bank; public long Cost, Est, FoodV; public double Day; }   // Bank - jednostki z zapasu osady (FoodStocks)
        private struct Cand { public EquipmentElement El; public int Buy, Sell, Have; public float Gain; }
        private static readonly Dictionary<MobileParty, Venture> _ventures = new Dictionary<MobileParty, Venture>();
        private static object _camp;   // kampania, ktorej dotyczy spis i liczniki (inna kampania = od zera)

        // liczniki doby (linia "Wedrowcy BK - prawdziwy towar (36b)")
        private static int _dParties, _dMen, _dPackSkipped, _dFoodSkippedMen, _dFoodNeed, _dFood, _dFoodShort, _dShortParties;
        private static int _dStock, _dStockShort, _dStockShortParties;   // luka z zapasu osady (FoodStocks) i brak takze w zapasie
        private static long _dFoodV, _dDaysSum;
        private static int _dVentures, _dWithGoods, _dNoGain, _dCastle, _dWar, _dSiege, _dNoBudget, _dUnits, _dLimLoad, _dLimBudget, _dLimMarket;
        private static long _dBudget, _dCost, _dEst;
        private static int _dArrived, _dOld, _dHome, _dHomeUnits, _dNoMarket, _dSoldGoods, _dSoldFood, _dSoldAnimals, _dUnpaid, _dStarved;
        private static long _dPaid, _dPaidGoods, _dPaidFood, _dPaidAnimals;
        // partie ze spisu: towar sprzedany u obcego targu (zysk) i zywnosc drogi (wydana / doszla) oraz zapas osady (dany / wrocil / przepadl)
        private static long _dKGoodsCost, _dKGoodsEst, _dKGoodsPaid;
        private static int _dKGoodsOut, _dKGoodsBack, _dKFoodOut, _dKFoodBack, _dBankOut, _dBankBack, _dBankLost;
        // cywile u celu (przed liczeniem BK): ludzie, ranni (BK dopisze ich drugi raz), ranni w glodnych partiach, partie z rannymi
        private static int _dCiv, _dCivWounded, _dCivWoundedStarved, _dWoundedParties;
        private static double _dRoadDays;
        private static int _dArrKnown;
        private static int _dStumbles, _stumbles;
        private static long _dTicks;

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var miss = new List<string>();
                _ppc = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent");
                var bkc = AccessTools.TypeByName("BannerKings.Components.BannerKingsComponent");
                var bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                MethodInfo create = null, pack = null, food = null, arrive = null;
                if (_ppc == null) miss.Add("typ PopulationPartyComponent");
                else
                {
                    create = AccessTools.Method(_ppc, "CreateTravellerParty");
                    pack = AccessTools.Method(_ppc, "GivePackAnimals");
                    _pTrading = AccessTools.Property(_ppc, "Trading");
                    _pSlave = AccessTools.Property(_ppc, "SlaveCaravan");
                    _pKind = AccessTools.Property(_ppc, "Kind");
                    _pPop = AccessTools.Property(_ppc, "PopulationType");
                    if (_pTrading == null || _pSlave == null || _pKind == null || _pPop == null) miss.Add("wlasciwosci Trading/SlaveCaravan/Kind/PopulationType");
                }
                if (bkc != null) food = AccessTools.Method(bkc, "GiveFood"); else miss.Add("typ BannerKingsComponent");
                if (bkp != null) arrive = AccessTools.Method(bkp, "AddPopulationPartyBehavior"); else miss.Add("typ BKPartyBehavior");
                // parametry po pozycji - typy sprawdzone tutaj; niezgodnosc = nic nie wpinamy (polowa reguly bylaby gorsza niz zadna:
                // zapasy zdjete bez zakupu albo towar kupiony bez sprzedazy)
                if (!Sig(create, typeof(MobileParty), new[] { 1, 2, 7 }, new[] { typeof(Settlement), typeof(Settlement), typeof(bool) })) miss.Add("CreateTravellerParty");
                if (!Sig(pack, null, new[] { 0 }, new[] { typeof(MobileParty).MakeByRefType() })) miss.Add("GivePackAnimals");
                if (!Sig(food, null, new[] { 0 }, new[] { typeof(MobileParty).MakeByRefType() })) miss.Add("GiveFood");
                if (!Sig(arrive, null, new[] { 0, 1 }, new[] { typeof(MobileParty), typeof(Settlement) })) miss.Add("AddPopulationPartyBehavior");
                if (miss.Count > 0)
                {
                    _wiredLine = "BRAK: " + string.Join(", ", miss.ToArray()) + " - nic nie wpiete, wedrowcy BK jak dotad";
                    Log.Info("TravellerTrade36b (36b): wedrowcy BK z prawdziwym towarem - " + _wiredLine + ".");
                    return;
                }
                h.Patch(create, prefix: new HarmonyMethod(typeof(TravellerTrade36b), nameof(CreatePre)),
                                postfix: new HarmonyMethod(typeof(TravellerTrade36b), nameof(CreatePost)) { priority = Priority.Low },
                                finalizer: new HarmonyMethod(typeof(TravellerTrade36b), nameof(CreateFin)));
                h.Patch(pack, prefix: new HarmonyMethod(typeof(TravellerTrade36b), nameof(PackPre)));
                h.Patch(food, prefix: new HarmonyMethod(typeof(TravellerTrade36b), nameof(FoodPre)));
                // ponad First (800): PRZED oknem O21 ksiegi obiegu (CirculationWindows.ConvoyPre) - nasz przelew kasa celu -> kasa pochodzenia
                // nie moze wpasc do tego okna jako "konwoj placi w nicosc"
                h.Patch(arrive, prefix: new HarmonyMethod(typeof(TravellerTrade36b), nameof(ArrivePre)) { priority = Priority.First + 100 });
                _wiredLine = "latki: zakladanie partii (CreateTravellerParty), juczne (GivePackAnimals), zywnosc drogi (GiveFood - tylko w CreateTravellerParty), dojscie (AddPopulationPartyBehavior, przed oknem O21); BRAK: -";
                Log.Info("TravellerTrade36b (36b): wedrowcy BK z prawdziwym towarem - " + _wiredLine + "; teraz " + (On ? "WLACZONE" : "WYLACZONE")
                         + " (Travellers Real Goods " + (Settings.Current != null && Settings.Current.TravellersRealGoods ? "tak" : "nie")
                         + ", wymaga Travellers No Goods 3.6: " + (Settings.Current != null && Settings.Current.TravellersNoGoods ? "tak" : "nie") + ").");
            }
            catch (Exception e) { Log.Error("TravellerTrade36b.ApplyAll", e); }
        }

        private static bool Sig(MethodInfo m, Type ret, int[] at, Type[] types)
        {
            if (m == null) return false;
            if (ret != null && m.ReturnType != ret) return false;
            var ps = m.GetParameters();
            for (int i = 0; i < at.Length; i++)
                if (at[i] >= ps.Length || ps[at[i]].ParameterType != types[i]) return false;
            return true;
        }

        // ------------------------------------------------------------ zakladanie partii
        public static void CreatePre(bool __7) { _inCreate = true; _createTrading = __7; }

        public static void CreateFin() { _inCreate = false; _createTrading = false; }

        /// <summary>Juczne BK z niczego - pomijane w czasie CreateTravellerParty (jedyne wywolanie GivePackAnimals).</summary>
        public static bool PackPre(ref MobileParty __0)
        {
            try
            {
                if (!_inCreate || _createTrading || !On) return true;
                if (__0 != null && __0.Party != null) _dPackSkipped += (int)(__0.Party.NumberOfAllMembers * 0.25f);
                return false;
            }
            catch (Exception e) { Stumble("PackPre", e); return true; }
        }

        /// <summary>Zywnosc BK z niczego - pomijana tylko w czasie CreateTravellerParty (GiveFood wolaja tez inne komponenty BK).</summary>
        public static bool FoodPre(ref MobileParty __0)
        {
            try
            {
                if (!_inCreate || _createTrading || !On) return true;
                if (__0 != null && __0.Party != null) _dFoodSkippedMen += __0.Party.NumberOfAllMembers;
                return false;
            }
            catch (Exception e) { Stumble("FoodPre", e); return true; }
        }

        /// <summary>Po CreateTravellerParty (po GiveItems, 3.6 i CastleSteel): zywnosc drogi z polki pochodzenia i towar w komis.</summary>
        public static void CreatePost(MobileParty __result, Settlement __1, Settlement __2, bool __7)
        {
            if (__7 || __result == null || !On) return;
            long t0 = Stopwatch.GetTimestamp();
            try { Equip(__result, __1, __2); }
            catch (Exception e) { Stumble("Equip", e); }
            finally { _dTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static void Equip(MobileParty party, Settlement origin, Settlement target)
        {
            SameCampaign();
            if (origin == null || origin.Town == null || origin.ItemRoster == null || party.ItemRoster == null || party.Party == null) return;
            int men = party.Party.NumberOfAllMembers;
            _dParties++; _dMen += men;
            var v = new Venture { Day = CampaignTime.Now.ToDays };
            _ventures[party] = v;
            var frame = GoodsLedger.Begin(GoodsLedger.FSell, origin.Party, party.Party, origin.ItemRoster, party.ItemRoster);
            try
            {
                RoadFood(party, origin, target, men, v);
                Trade(party, origin, target, men, v);
            }
            finally { GoodsLedger.End(frame); }
        }

        private static int RoadDays(MobileParty party, Settlement origin, Settlement target)
        {
            float d = -1f;
            try { if (target != null) d = Campaign.Current.Models.MapDistanceModel.GetDistance(origin, target, false, false, MobileParty.NavigationType.Default); }
            catch { d = -1f; }
            if (d <= 0f || float.IsNaN(d) || float.IsInfinity(d) || d > 2000f) return UnknownRoadDays;
            float pace = RoadPace;
            try { float s = party.Speed; if (s > 0.2f && !float.IsNaN(s) && !float.IsInfinity(s)) pace = s; } catch { }   // model predkosci (tempo swiata, prawa marszu)
            int days = (int)Math.Ceiling(d / (pace * MarchHours)) + ExtraDays;
            return Math.Max(MinDays, Math.Min(MaxDays, days));
        }

        private static void RoadFood(MobileParty party, Settlement origin, Settlement target, int men, Venture v)
        {
            int days = RoadDays(party, origin, target);
            _dDaysSum += days;
            float perDay = 0.05f * men;
            try { float fc = -party.FoodChange; if (fc > perDay) perDay = fc; } catch { }
            int need = Math.Max(1, (int)Math.Ceiling(perDay * days));
            _dFoodNeed += need;
            var shelf = origin.ItemRoster;
            var town = origin.Town;
            var cand = new List<Cand>();
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !it.IsFood) continue;
                cand.Add(new Cand { El = el.EquipmentElement, Buy = town.GetItemPrice(el.EquipmentElement, party, false), Have = el.Amount });
            }
            cand.Sort((a, b) => a.Buy.CompareTo(b.Buy));
            int got = 0;
            foreach (var c in cand)
            {
                if (got >= need) break;
                var el = c.El;
                long cost;
                int n = Take(shelf, party.ItemRoster, el, need - got, long.MaxValue, () => town.GetItemPrice(el, party, false), c.Buy, null, out cost);
                got += n; v.Food += n; v.FoodV += cost;
            }
            _dFood += got; _dFoodV += v.FoodV;
            if (got >= need) return;
            int gap = need - got;
            _dFoodShort += gap; _dShortParties++;
            // luka z zapasu zywnosci osady pochodzenia (poprawka po recenzji - glodujacy wedrowcy dostaja rany, a BK liczy rannych u celu
            // podwojnie): jednostka zapasu = sztuka zywnosci = 100 punktow RemainingFoodPercentage; zapas bez sztuk, gra zjada go przed sztukami
            int give = Math.Min(gap, (int)Math.Floor(Math.Max(0f, town.FoodStocks) * StockShare));
            if (give > 0)
            {
                town.FoodStocks -= give;
                party.Party.RemainingFoodPercentage = Math.Max(0, party.Party.RemainingFoodPercentage) + give * 100;
                v.Bank = give; _dStock += give;
            }
            if (gap > give) { _dStockShort += gap - give; _dStockShortParties++; }
        }

        private static void Trade(MobileParty party, Settlement origin, Settlement target, int men, Venture v)
        {
            if (!origin.IsTown || target == null || !target.IsTown || target.Town == null) { _dCastle++; return; }
            if (target.IsUnderSiege) { _dSiege++; return; }
            if (origin.MapFaction != null && target.MapFaction != null && origin.MapFaction.IsAtWarWith(target.MapFaction)) { _dWar++; return; }
            var from = origin.Town;
            var to = target.Town;
            long budget = Math.Min((long)Math.Max(0, Settings.Current.TravellerTradePerHead) * men,
                                   (long)Math.Min(from.Gold * TownShare, to.Gold * TownShare));
            if (budget <= 0) { _dNoBudget++; return; }
            _dVentures++; _dBudget += budget;
            var shelf = origin.ItemRoster;
            var cand = new List<Cand>();
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                // bez zywnosci (po recenzji): gra zjada w drodze losowa sztuke zywnosci z ladunku - zywnosc w ladunku to tylko zywnosc drogi
                if (it == null || el.Amount < 2 || !it.IsTradeGood || it.IsFood || it.NotMerchandise || el.EquipmentElement.ItemModifier != null || Excluded(it)) continue;
                int b = from.GetItemPrice(el.EquipmentElement, party, false);
                if (b <= 0 || b > budget) continue;
                int s = to.GetItemPrice(el.EquipmentElement, party, true);
                if (s - b < Math.Max(1f, b * MinMargin)) continue;
                cand.Add(new Cand { El = el.EquipmentElement, Buy = b, Sell = s, Have = el.Amount, Gain = (s - b) / (float)b });
            }
            if (cand.Count == 0) { _dNoGain++; return; }
            cand.Sort((a, b) => b.Gain.CompareTo(a.Gain));
            float cap = LoadUse * party.InventoryCapacity;
            float load = party.TotalWeightCarried;
            long left = budget;
            bool full = false;
            foreach (var c in cand)
            {
                if (left <= 0 || full) break;
                var el = c.El;
                int sell = c.Sell;
                float kg = Math.Max(0f, el.Item.Weight);
                float load0 = load;
                int maxN = (int)(c.Have * ShelfShare);
                if (maxN <= 0) continue;
                long cost;
                int n = Take(shelf, party.ItemRoster, el, maxN, Math.Min(left, (long)(budget * ItemShare)), () => from.GetItemPrice(el, party, false), c.Buy,
                             (p, k) =>
                             {
                                 if (sell - p < Math.Max(1f, p * MinMargin)) return true;   // zysk spadl ponizej progu - nastepny towar
                                 if (load0 + kg * (k + 1) > cap) { full = true; return true; }
                                 return false;
                             }, out cost);
                load += kg * n;
                left -= cost;
                v.Units += n; v.Cost += cost; v.Est += (long)sell * n;
            }
            _dUnits += v.Units; _dCost += v.Cost; _dEst += v.Est;
            if (v.Units > 0) _dWithGoods++;   // "z towarem" - proba wyprawy (_dVentures) liczy tez te, ktore nic nie kupily
            if (v.Units == 0) _dNoGain++;
            else if (full) _dLimLoad++;
            else if (left < budget / 10) _dLimBudget++;
            else _dLimMarket++;
        }

        private static bool Excluded(ItemObject it)
        {
            if (NotTraded.Contains(it.StringId ?? "")) return true;
            if (_smith == null)
            {
                var hs = new HashSet<ItemObject>();
                for (int i = 0; i < 9; i++) { var m = Recipes.MaterialItem((CraftingMaterials)i); if (m != null) hs.Add(m); }   // Iron1-6, ruda, drewno, wegiel
                _smith = hs;
            }
            return _smith.Contains(it);
        }

        /// <summary>
        /// Sztuka po sztuce z jednego rosteru do drugiego, przed kazda sztuka cena od nowa (known - cena pierwszej z przegladu polki); koniec, gdy
        /// cena nie miesci sie w budzecie albo stop(cena, juz_wziete) mowi "dosc". Nie wiecej, niz lezy. Zwraca liczbe sztuk, cost - ich suma.
        /// </summary>
        private static int Take(ItemRoster from, ItemRoster to, EquipmentElement el, int maxN, long budget, Func<int> price, int known, Func<int, int, bool> stop, out long cost)
        {
            cost = 0;
            int at = from.FindIndexOfElement(el);
            int have = at >= 0 ? from.GetElementNumber(at) : 0;
            if (maxN > have) maxN = have;
            int n = 0;
            while (n < maxN)
            {
                int p = n == 0 && known > 0 ? known : price();
                if (p <= 0 || cost + p > budget) break;
                if (stop != null && stop(p, n)) break;
                from.AddToCounts(el, -1);
                to.AddToCounts(el, 1);
                cost += p; n++;
            }
            return n;
        }

        // ------------------------------------------------------------ dojscie do celu
        /// <summary>Prefiks BK AddPopulationPartyBehavior (zaraz potem BK dopisuje ludzi i niszczy partie): ladunek na polke osady, utarg do kasy pochodzenia.</summary>
        public static void ArrivePre(MobileParty __0, Settlement __1)
        {
            if (__0 == null || __1 == null || !On) return;
            long t0 = 0;
            try
            {
                if (!IsTraveller(__0)) return;   // BK wola to dla kazdej partii wchodzacej do osady - reszta konczy sie tutaj
                t0 = Stopwatch.GetTimestamp();
                SameCampaign();
                Sell(__0, __1);
            }
            catch (Exception e) { Stumble("Sell", e); }
            finally { if (t0 != 0) _dTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Ta sama galaz co BK: nie Trading, nie niewolnicy, nie karawana zywnosci (Kind Unset), PopulationType != None.</summary>
        private static bool IsTraveller(MobileParty mp)
        {
            var pc = mp.PartyComponent;
            if (pc == null || _ppc == null || !_ppc.IsInstanceOfType(pc)) return false;
            if ((bool)_pTrading.GetValue(pc, null) || (bool)_pSlave.GetValue(pc, null)) return false;
            var kind = _pKind.GetValue(pc, null);
            var pop = _pPop.GetValue(pc, null);
            return kind != null && kind.ToString() == "Unset" && pop != null && pop.ToString() != "None";
        }

        private static void Sell(MobileParty mp, Settlement st)
        {
            Venture v;
            if (_ventures.TryGetValue(mp, out v)) _ventures.Remove(mp); else { v = null; _dOld++; }
            _dArrived++;
            if (v != null)
            {
                _dArrKnown++;
                _dRoadDays += Math.Max(0.0, CampaignTime.Now.ToDays - v.Day);
            }
            bool starving = false;
            try { starving = mp.Party != null && mp.Party.IsStarving; } catch { }   // zywnosci drogi zabraklo przed celem
            if (starving) _dStarved++;
            try { Wounded(mp, starving); } catch (Exception e) { Stumble("Wounded", e); }
            if (v != null) { try { BankBack(mp, st, v); } catch (Exception e) { Stumble("BankBack", e); } }   // blad tu nie zatrzymuje sprzedazy ladunku
            var town = st.Town;
            var r = mp.ItemRoster;
            if (town == null || r == null || st.ItemRoster == null) { _dNoMarket++; return; }
            var home = mp.HomeSettlement;
            bool same = home == null || home == st || home.Town == null;
            if (same) _dHome++;
            var shelf = st.ItemRoster;
            long paid = 0, pGoods = 0, pFood = 0, pAnimals = 0;
            int goodsBack = 0, foodBack = 0;
            var frame = GoodsLedger.Begin(GoodsLedger.FSell, mp.Party, st.Party, r, shelf);
            try
            {
                for (int i = r.Count - 1; i >= 0; i--)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    int n = el.Amount;
                    if (it == null || n <= 0 || !(it.IsTradeGood || it.IsFood || it.HasHorseComponent)) continue;
                    int k = 0;
                    long itemPaid = 0;
                    if (!same)
                    {
                        while (k < n)
                        {
                            int p = town.GetItemPrice(el.EquipmentElement, mp, true);
                            // E5/A1 (poprawka po recenzji): bez BuyFloor - czego kasa celu nie oplaci, idzie nizej na polke celu BEZ zaplaty (_dUnpaid),
                            // wiec prog zapasu zrobilby z tego wiecej towaru bez zaplaty (ten sam powod co RecruitKit); cala kasa jak przed A1
                            if (p <= 0 || town.Gold - itemPaid < p) break;   // kasa placi tylko, co ma
                            r.AddToCounts(el.EquipmentElement, -1);
                            shelf.AddToCounts(el.EquipmentElement, 1);
                            itemPaid += p; k++;
                        }
                        if (itemPaid > 0) { town.ChangeGold((int)-itemPaid); paid += itemPaid; }
                    }
                    int rest = n - k;
                    if (rest > 0)
                    {
                        r.AddToCounts(el.EquipmentElement, -rest);
                        shelf.AddToCounts(el.EquipmentElement, rest);
                        if (same) _dHomeUnits += rest; else _dUnpaid += rest;
                    }
                    // zaplata osobno za zwierzeta, zywnosc i towar (ta sama kolejnosc co liczniki sztuk)
                    if (it.HasHorseComponent) { _dSoldAnimals += n; pAnimals += itemPaid; }
                    else if (it.IsFood) { _dSoldFood += n; pFood += itemPaid; foodBack += n; }
                    else { _dSoldGoods += n; pGoods += itemPaid; goodsBack += n; }
                }
            }
            finally
            {
                GoodsLedger.End(frame);
                // utarg do kasy pochodzenia takze po wyjatku w srodku (to, co kasa celu juz zaplacila, nie moze zniknac)
                if (paid > 0 && !same)
                {
                    home.Town.ChangeGold((int)paid);
                    MoneyLedger.Note(MoneyLedger.NTravel, st, (int)-paid);
                    MoneyLedger.Note(MoneyLedger.NTravel, home, (int)paid);
                    _dPaid += paid;
                }
                _dPaidGoods += pGoods; _dPaidFood += pFood; _dPaidAnimals += pAnimals;
                if (v != null)
                {
                    _dKFoodOut += v.Food; _dKFoodBack += foodBack;   // zjedzona = wydana - doszla (towar bez zywnosci, dar BK pominiety)
                    if (!same)
                    {
                        // zysk: utarg za towar wobec ceny zakupu towaru tej partii (powrot do domu - towar na polke domu, bez zysku i straty)
                        _dKGoodsCost += v.Cost; _dKGoodsEst += v.Est; _dKGoodsPaid += pGoods;
                        _dKGoodsOut += v.Units; _dKGoodsBack += goodsBack;
                    }
                }
            }
        }

        /// <summary>
        /// Cywile przed liczeniem BK: BK dopisze osadzie Helpers.GetRosterCount(MemberRoster, filtr) = Number + WoundedNumber szeregowych
        /// z filtrem w StringId (dekompilacja BannerKings.dll 11.10), a Number juz zawiera rannych - ranni wchodza do ludnosci drugi raz.
        /// </summary>
        private static void Wounded(MobileParty mp, bool starving)
        {
            var roster = mp.MemberRoster;
            if (roster == null) return;
            var pop = _pPop.GetValue(mp.PartyComponent, null);
            string p = pop != null ? pop.ToString() : "";
            string filter = p == "Serfs" ? "villager" : p == "Craftsmen" ? "craftsman" : "noble";   // ta sama regula co BK AddPopulationPartyBehavior
            int civ = 0, wounded = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || ch.StringId == null || !ch.StringId.Contains(filter)) continue;
                civ += roster.GetElementNumber(i);
                wounded += roster.GetElementWoundedNumber(i);
            }
            _dCiv += civ; _dCivWounded += wounded;
            if (wounded > 0) { _dWoundedParties++; if (starving) _dCivWoundedStarved += wounded; }
        }

        /// <summary>
        /// Zapas z FoodStocks, ktory zostal u celu, wraca do zapasu osady, do ktorej weszli (ludzie wchodza do jej ludnosci). RemainingFoodPercentage
        /// >= 100 zostaje tylko z naszego zapasu: zjedzona sztuka daje +100, a gra je sztuki tylko ponizej 0 - wiec po sztukach wynik jest w 0-99.
        /// </summary>
        private static void BankBack(MobileParty mp, Settlement st, Venture v)
        {
            if (v.Bank <= 0 || mp.Party == null) return;
            _dBankOut += v.Bank;
            int left = Math.Min(v.Bank, Math.Max(0, mp.Party.RemainingFoodPercentage) / 100);
            if (left <= 0) return;
            mp.Party.RemainingFoodPercentage -= left * 100;
            if (st.Town != null) { st.Town.FoodStocks += left; _dBankBack += left; }
            else _dBankLost += left;   // wies - bez zapasu zywnosci (BK i tak dopisze tam ludzi)
        }

        // ------------------------------------------------------------ porzadki i linia doby
        private static void Stumble(string where, Exception e)
        {
            _dStumbles++;
            if (_stumbles++ < 3) Log.Error("TravellerTrade36b." + where, e);
        }

        /// <summary>Spis i liczniki z innej kampanii (wczytanie innego zapisu w tej samej sesji gry) nie przechodza do nowej.</summary>
        private static void SameCampaign()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(_camp, c)) return;
            _ventures.Clear();
            Clear();
            _smith = null;
            _camp = c;
        }

        private static void Clear()
        {
            _dParties = _dMen = _dPackSkipped = _dFoodSkippedMen = _dFoodNeed = _dFood = _dFoodShort = _dShortParties = 0;
            _dStock = _dStockShort = _dStockShortParties = 0;
            _dFoodV = _dDaysSum = 0;
            _dVentures = _dWithGoods = _dNoGain = _dCastle = _dWar = _dSiege = _dNoBudget = _dUnits = _dLimLoad = _dLimBudget = _dLimMarket = 0;
            _dBudget = _dCost = _dEst = 0;
            _dArrived = _dOld = _dHome = _dHomeUnits = _dNoMarket = _dSoldGoods = _dSoldFood = _dSoldAnimals = _dUnpaid = _dStarved = 0;
            _dPaid = _dPaidGoods = _dPaidFood = _dPaidAnimals = 0;
            _dKGoodsCost = _dKGoodsEst = _dKGoodsPaid = 0;
            _dKGoodsOut = _dKGoodsBack = _dKFoodOut = _dKFoodBack = _dBankOut = _dBankBack = _dBankLost = 0;
            _dCiv = _dCivWounded = _dCivWoundedStarved = _dWoundedParties = 0;
            _dRoadDays = 0; _dArrKnown = 0;
            _dStumbles = 0; _dTicks = 0;
        }

        internal static void Daily()
        {
            try
            {
                SameCampaign();
                int lostN = 0, lostUnits = 0, lostBank = 0, transitN = 0, transitUnits = 0;
                long lostV = 0, transitV = 0;
                List<MobileParty> gone = null;
                foreach (var kv in _ventures)
                {
                    var mp = kv.Key; var v = kv.Value;
                    if (mp == null || !mp.IsActive)
                    {
                        lostN++; lostUnits += v.Units; lostV += v.Cost;
                        if (v.Bank > 0)   // zapas z FoodStocks, ktory zostal w partii (ostatni stan; partia bez obiektu - caly)
                        {
                            int left = v.Bank;
                            try { if (mp != null && mp.Party != null) left = Math.Min(v.Bank, Math.Max(0, mp.Party.RemainingFoodPercentage) / 100); } catch { }
                            lostBank += left;
                        }
                        if (gone == null) gone = new List<MobileParty>();
                        gone.Add(mp);
                    }
                    else { transitN++; transitUnits += v.Units; transitV += v.Cost; }
                }
                if (gone != null) foreach (var mp in gone) _ventures.Remove(mp);
                if (!On && _dParties == 0 && _dArrived == 0) { Clear(); return; }
                var inv = CultureInfo.InvariantCulture;
                double ms = _dTicks * 1000.0 / Stopwatch.Frequency;
                Log.Info("Wedrowcy BK - prawdziwy towar (36b): dzien " + (int)CampaignTime.Now.ToDays + (On ? "" : " (WYLACZONE - tylko dojscia sprzed wylaczenia)")
                         + " | wyruszylo " + _dParties + " partii (ludzi " + _dMen + "); BK nie dal z niczego: jucznych " + _dPackSkipped + ", zywnosci drogi dla " + _dFoodSkippedMen + " ludzi"
                         + " | zywnosc drogi z polek osad pochodzenia " + _dFood + " szt. za " + _dFoodV + " zl wedlug cen targu (potrzeba wedlug drogi " + _dFoodNeed
                         + ", srednio " + (_dParties > 0 ? ((double)_dDaysSum / _dParties).ToString("0.0", inv) : "0") + " dob drogi (szacunek z zapasem 2 dob); brak na polce " + _dFoodShort + " szt. w " + _dShortParties + " partiach"
                         + " -> z zapasu zywnosci osady pochodzenia (FoodStocks, najwyzej polowa) " + _dStock + " jedn.; brak takze w zapasie " + _dStockShort + " jedn. w " + _dStockShortParties + " partiach)"
                         + " | towar w komis od kupcow miasta pochodzenia (bez zywnosci): prob wyprawy " + _dVentures + ", z towarem " + _dWithGoods + "; kupiono " + _dUnits + " szt. za " + _dCost + " zl (budzet " + _dBudget
                         + " zl, spodziewany utarg u celu " + _dEst + " zl); bez oplacalnego towaru " + _dNoGain + "; bez wyprawy: zamek na koncu drogi " + _dCastle + ", wojna " + _dWar
                         + ", oblezony cel " + _dSiege + ", bez budzetu " + _dNoBudget + "; koniec zakupu: udzwig " + _dLimLoad + ", budzet " + _dLimBudget + ", polka i marza " + _dLimMarket
                         + " | doszlo " + _dArrived + " partii (spoza spisu - sprzed wczytania albo wlaczenia: " + _dOld + ", glodnych u celu " + _dStarved + "): na polki osad celu towar " + _dSoldGoods + " szt., zywnosc " + _dSoldFood + ", zwierzeta " + _dSoldAnimals
                         + "; kasy celu zaplacily " + _dPaid + " zl -> do kas osad pochodzenia (za towar " + _dPaidGoods + ", zywnosc " + _dPaidFood + ", zwierzeta " + _dPaidAnimals + ")"
                         + ", bez zaplaty (kasa celu pusta) " + _dUnpaid + " szt., powrot do domu " + _dHome + " partii (" + _dHomeUnits + " szt. na polke domu bez zaplaty), osada bez targu " + _dNoMarket
                         + " | zysk towaru (partie ze spisu u obcego targu): koszt wedlug cen zakupu " + _dKGoodsCost + " zl (" + _dKGoodsOut + " szt., doszlo " + _dKGoodsBack + "), utarg za towar " + _dKGoodsPaid
                         + " zl (spodziewany " + _dKGoodsEst + " zl); partii ze spisu (wszystkie dojscia) " + _dArrKnown + ", srednio " + (_dArrKnown > 0 ? (_dRoadDays / _dArrKnown).ToString("0.0", inv) : "0") + " dob w drodze"
                         + " | zywnosc drogi u celu (partie ze spisu): wydana z polek " + _dKFoodOut + " szt., zjedzona " + (_dKFoodOut - _dKFoodBack) + ", na polke celu " + _dKFoodBack
                         + "; z zapasu osady " + _dBankOut + " jedn.: zjedzona " + (_dBankOut - _dBankBack - _dBankLost) + ", do zapasu osady celu " + _dBankBack + ", przepadla (osada bez zapasu) " + _dBankLost
                         + " | cywile u celu: BK dopisze " + _dCiv + " ludzi + " + _dCivWounded + " rannych drugi raz (BK liczy Number + WoundedNumber - ranni z niczego; partii z rannymi " + _dWoundedParties
                         + ", rannych w glodnych partiach " + _dCivWoundedStarved + ")"
                         + " | zniknely w drodze " + lostN + " partii z towarem " + lostUnits + " szt. za " + lostV + " zl (bitwa - towar w lupie zwyciezcy, inne - przepadl; zapas osady w nich " + lostBank + " jedn. - przepadl)"
                         + " | w drodze " + transitN + " partii z towarem " + transitUnits + " szt. za " + transitV + " zl"
                         + " | czas " + ms.ToString("0.0", inv) + " ms" + (_dStumbles > 0 ? "; potkniecia " + _dStumbles + " (od wczytania " + _stumbles + ")" : "") + ".");
            }
            catch (Exception e) { Log.Error("TravellerTrade36b.Daily", e); }
            Clear();
        }
    }
}
