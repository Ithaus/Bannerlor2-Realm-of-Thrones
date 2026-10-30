using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA 139 (30.10, Jeff: "Napraw karawany" - po tym, jak uslyszal, ze tylko ok. 53% karawan zarabia na swoj zold, cel 60%; wczesniej "Napraw te karwany one traz
    /// nie maja sensu bo glownie traca" i "rob tak dlugo porawi az bedziesz zadowlony z wyniku") - CZEKANIE NA LEPSZY RYNEK KOSZTUJE ZOLD.
    /// Dowod (autotest 138c = WGRANE 138, 90 dob): 106 ze 139 karawan bez sprzedazy w 7 dobach ma towar z rynkiem z zyskiem i czeka (linia 298, klasa 3) - kazda doba
    /// czekania to ok. 94-118 zl zoldu i jedzenia eskorty; T1 staje na polowie stosu 85-95 razy na dobe (wlasna regula 'czekaj na lepsze miasto'); BK BuyCategory wydaje
    /// najwyzej 1 500 na kategorie na wizyte - przy cenach w nowej monecie (bawelna, aksamit x10, klejnoty, welna x3) to 0-2 sztuki luksusu; cel karawany BK
    /// (sprzedaz + kupno) / (droga + 8) nie liczy zoldu za dni drogi.
    /// Regula (jedna regula na zjawisko - wszedzie ten sam koszt dnia karawany: zold + jedzenie eskorty; zamknieta ekonomia - zmieniaja sie tylko decyzje, kto komu placi
    /// jak dotad; liczby z istniejacych modeli: zmierzony odcinek 288, cena sztuki w miescie odniesienia 294b, przelicznik monety kategorii HistoricalPrices):
    ///  A (klucz CaravanWaitCostsWage) - T1 sprzedaje ponad polowe stosu, gdy cena tutaj po mycie >= max(koszt sztuki, cena tej sztuki w typowym drozszym miescie po mycie
    ///     (CaravanTrade284.DepthNetAged) - udzial sztuki w koszcie dnia karawany x zmierzony odcinek); nigdy ponizej kosztu; bez miary - polowa stosu jak dotad;
    ///  B (klucz CaravanBkLotInItemCoin) - limit BK 1 500 na kategorie w monecie towaru (1 500 / przelicznik monety, gdy towar podrozal), reguly zakupu 284/294b na
    ///     KAZDA sztuke (glebokosc rynku, kapital obrotowy, bramka przeplywu) - straznik zakupu w CaravanTrade284.SellGuardPrefix;
    ///  C (klucz CaravanRouteRoadCost) - karawana z ladunkiem (towar po koszcie >= kiesa albo kapital obrotowy pelny) wybiera miasto z najwiekszym (zysk sprzedazy -
    ///     koszt dnia x dni drogi) / dni drogi sposrod miast nie dalszych niz wybor BK; utknieta (bez sprzedazy w 7 dobach, przy kluczu A) - takze do zmierzonego odcinka.
    ///     Oceny BK nie sa zerowane (bez drugiego przebiegu BK bez limitu - blad 294c D1), zysk sprzedazy z tych samych zasad co sprzedaz (SaleValue: T1 z rezerwa kasy
    ///     i 1/20, potem BK SellGoods z cala kasa i wzorem BK, nasz straznik kosztu, surowce masowe regula CaravanBulk).
    /// Linia doby 'Karawany - czekanie, limit BK i droga (139): dzien N' (po linii 294c; pomiar czekajacych takze przy kluczach NIE). Wyjatek - licznik potkniec.
    /// </summary>
    internal static class CaravanWait139
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int TownDayShare = 20;          // limit T1 (CaravanGoods.TownDayShare)
        private const float BkCap = 1500f;            // BKCaravansBehavior.MaxMoneyToSpendOnSingleCategory
        private const int RouteTowns = 10;            // miast ocenianych w C (najblizsze dozwolone)
        private const int MeasureTowns = 12;          // miast sprawdzanych w pomiarze czekajacych (najblizsze)
        private const int MeasureMax = 40;            // recenzja 139: karawan czekajacych z szukaniem miasta na dobe (proba co k-ta, przesuwana co dobe) - czas doby

        internal static bool AOn(Settings s) { return s != null && s.CaravanWaitCostsWage; }
        internal static bool BOn(Settings s) { return s != null && s.CaravanBkLotInItemCoin; }
        internal static bool COn(Settings s) { return s != null && s.CaravanRouteRoadCost; }

        // ------------------------------------------------------------ liczniki doby
        private static int _aSteps, _aUnits; private static long _aNet, _aHeld; private static double _aCost;
        private static int _bVisits, _bBound, _bLift, _bOver, _bLiftUnits; private static long _bLiftGold, _bClipDepth, _bClipCap, _bClipGate;
        private static readonly Dictionary<string, int> _bTop = new Dictionary<string, int>();
        private static int _cT, _cP, _cQ, _cE, _cF, _cG, _cD, _cNone, _cHome, _cStake, _mSample;
        private static double _cDb, _cDn, _cVb, _cVn, _cRb, _cRn;
        private static long _ticks;
        private static int _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        // latki
        private static bool _wScore, _wBuy; private static int _bFound; private static string _owners = "-";
        // B: biezacy lot BK (BuyCategory - od porownania z limitem do nastepnej kategorii)
        // recenzja 139: _lotBk - budzet lotu ksiegowany jak BK (num3 -= (num7 + 1) x itemPrice, num7 sprzed przyciecia) - z niego sztuki, ktore 138 kupilby z 1 500
        private static MobileParty _lotMp; private static ItemCategory _lotCat; private static double _lotCap, _lotBk; private static int _lotMaxPrice; private static bool _lotLift;
        // C: oceny miast BK w biezacym przebiegu (karawana w decyzji)
        private static readonly Dictionary<Town, float> _recs = new Dictionary<Town, float>();
        private static readonly Dictionary<(ItemObject, double), double> _floorC = new Dictionary<(ItemObject, double), double>();
        // recenzja 139: DepthNetAged karawany (nie zalezy od miasta oceny) - pamiec jednej decyzji / jednej karawany pomiaru
        private static readonly Dictionary<ItemObject, double> _dnC = new Dictionary<ItemObject, double>();
        // recenzja 139: BK CaravanOrdersBehavior.Instance / IsClanStakeholderTown(Clan, Settlement) - miasto udzialowca klanu (BK x1.5)
        private static PropertyInfo _ordInst; private static MethodInfo _stakeMi;
        // pamiec doby: najnizszy indeks kategorii w miastach (BK CreatePriceDataCache - MinBuySellPriceIndex)
        private static readonly Dictionary<ItemCategory, float> _minIdx = new Dictionary<ItemCategory, float>(); private static int _minDay = -1;

        internal static void Reset()
        {
            ZeroDay();
            _stumbleSess = 0; _err.Clear();
            _lotMp = null; _lotCat = null; _recs.Clear(); _floorC.Clear(); _dnC.Clear(); _minIdx.Clear(); _minDay = -1;
            CaravanTrade284.Waiting298.Clear();
        }

        private static void ZeroDay()
        {
            _aSteps = _aUnits = 0; _aNet = _aHeld = 0; _aCost = 0;
            _bVisits = _bBound = _bLift = _bOver = _bLiftUnits = 0; _bLiftGold = _bClipDepth = _bClipCap = _bClipGate = 0; _bTop.Clear();
            _cT = _cP = _cQ = _cE = _cF = _cG = _cD = _cNone = _cHome = _cStake = _mSample = 0; _cDb = _cDn = _cVb = _cVn = _cRb = _cRn = 0;
            _ticks = 0; _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanWait139." + where, e); } catch { }
        }

        // ------------------------------------------------------------ koszt dnia karawany (jedna miara w A, C i pomiarze)
        /// <summary>Towar handlowy pod ocena (jak T1 i BK: nie konie, nie juczne, nie zwierzeta, nie poza handlem).</summary>
        private static bool Goods(ItemObject it)
        {
            return it != null && it.IsTradeGood && !it.HasHorseComponent && !it.NotMerchandise && it.ItemCategory != null && it.ItemCategory != DefaultItemCategories.PackAnimal && !it.ItemCategory.IsAnimal;
        }

        /// <summary>Cena sztuki jedzenia eskorty: najtansze jedzenie w jukach po koszcie z ksiegi (TYLKO ODCZYT - HeldCost), bez jedzenia - najtansze na polce miasta,
        /// w ktorym karawana stoi (ta sama cena, ktora 288 placi przy wyjezdzie), w drodze - wartosc najtanszego jedzenia jukow x srednia indeksu miast; 0 - brak.</summary>
        private static double FoodUnitCost(MobileParty mp)
        {
            var r = mp.ItemRoster;
            double best = double.MaxValue;
            ItemObject cheap = null;
            for (int i = 0; r != null && i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !it.IsFood) continue;
                int have = r.GetItemNumber(it);
                double u = CaravanGoods.HeldCost(mp, it, have) / Math.Max(1, have);
                if (u > 0 && u < best) { best = u; cheap = it; }
            }
            if (cheap != null) return best;
            var cs = mp.CurrentSettlement;
            if (cs != null && cs.IsTown && cs.Town != null && cs.ItemRoster != null)
            {
                var sh = cs.ItemRoster; int bv = int.MaxValue; EquipmentElement be = default(EquipmentElement); bool any = false;
                for (int i = 0; i < sh.Count; i++)
                {
                    var el = sh.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || !it.IsFood || it.NotMerchandise) continue;
                    if (it.Value < bv) { bv = it.Value; be = el.EquipmentElement; any = true; }
                }
                if (any) return Math.Max(1, cs.Town.GetItemPrice(be, mp, false));
            }
            var g = DefaultItems.Grain;
            return g != null ? Math.Max(1, g.Value) * CaravanGoods.AvgIndexOf(g.ItemCategory) : 0.0;
        }

        /// <summary>Koszt jednej doby karawany: zold (po polowie zoldu eskorty 288 - TotalWage) + jedzenie eskorty (-FoodChange x cena sztuki jedzenia).</summary>
        internal static double DayCost(MobileParty mp, out double wage, out double food)
        {
            wage = 0; food = 0;
            if (mp == null) return 0.0;
            wage = Math.Max(0, mp.TotalWage);
            double use = Math.Max(0.0, -mp.FoodChange);
            if (use > 0) food = use * FoodUnitCost(mp);
            return wage + food;
        }

        internal static double DayCost(MobileParty mp) { double w, f; return DayCost(mp, out w, out f); }

        /// <summary>Ladunek towarow handlowych po koszcie - TYLKO ODCZYT (CaravanGoods.HeldCost, bez Sync); ta sama miara co CargoCost.</summary>
        internal static double CargoRO(MobileParty mp)
        {
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null) return 0.0;
            double c = 0; HashSet<ItemObject> seen = null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !it.IsTradeGood) continue;
                if (seen == null) seen = new HashSet<ItemObject>();
                if (!seen.Add(it)) continue;
                c += CaravanGoods.HeldCost(mp, it, r.GetItemNumber(it));
            }
            return c;
        }

        /// <summary>Koszt przewiezienia sztuki o koszcie unit przez days dob: udzial sztuki w koszcie dnia karawany (wzor 284 c.Min = koszt + koszt x zold x dni / ladunek -
        /// tam za doby juz minione, tu za doby przed karawana).</summary>
        internal static double Carry(double dayCost, double unit, double days, double cargo)
        {
            if (!(dayCost > 0) || !(unit > 0) || !(days > 0) || !(cargo > 0)) return 0.0;
            return dayCost * days * unit / cargo;
        }

        /// <summary>Zmierzony odcinek karawan (288, Stage8World.LegDays); 0 - bez pomiaru.</summary>
        internal static double Leg { get { double l = Stage8World.LegDays; return l > 0 && !double.IsInfinity(l) ? l : 0.0; } }

        /// <summary>Dni drogi do osady: ta sama miara co jedzenie na droge 288 (CaravanEscort.RoadDaysTo), najmniej 1 (RoadDays). Barki rzeczne (RiverBarges) - tylko
        /// rachunek oceny BK i wozow wsi, karawana jedzie droga ladowa i placi zold za cala droge - bez mnoznika.</summary>
        internal static double Days(MobileParty mp, Settlement st) { return Math.Max(1.0, CaravanEscort.RoadDaysTo(mp, st)); }

        /// <summary>Recenzja 139: karawana dojedzie jeszcze w tej dobie (czesc doby, ktora minela + dni drogi bez minimum < 1) - limit T1 miasta (1/20 kasy na dobe)
        /// jest wtedy wspolny z karawanami, ktore sprzedaly tam dzis (CaravanGoods.T1LeftPeek).</summary>
        private static bool SameDay(MobileParty mp, Settlement st)
        {
            double now = CampaignTime.Now.ToDays;
            return now - Math.Floor(now) + CaravanEscort.RoadDaysTo(mp, st) < 1.0;
        }

        /// <summary>DepthNetAged z pamieci decyzji (_dnC; ta sama karawana - cena w miescie odniesienia nie zalezy od miasta oceny).</summary>
        private static double DnAged(MobileParty mp, ItemObject it)
        {
            double v;
            if (_dnC.TryGetValue(it, out v)) return v;
            v = CaravanTrade284.DepthNetAged(mp, it);
            _dnC[it] = v;
            return v;
        }

        // ------------------------------------------------------------ A (CaravanGoods.Sell)
        internal static void NoteA1Step(int moved, long net, double cost) { if (moved > 0) { _aSteps++; _aUnits += moved; _aNet += net; _aCost += cost; } }
        internal static void NoteA1Held(int n) { if (n > 0) _aHeld += n; }

        // ------------------------------------------------------------ zysk sprzedazy wizyty (C i pomiar)
        private sealed class Lot { public ItemObject It; public EquipmentElement El; public int Have, Trade; public double Unit, Thr, Q, Qn; public ItemData D; public int Wv, P1; public double Gain; public bool Bulk; }

        /// <summary>Najnizszy indeks kategorii w miastach (BK CreatePriceDataCache: MinBuySellPriceIndex) - pamiec doby.</summary>
        private static float MinIdx(ItemCategory c)
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _minDay) { _minIdx.Clear(); _minDay = d; }
            float v;
            if (_minIdx.TryGetValue(c, out v)) return v;
            v = 1000f;
            foreach (var t in Town.AllTowns) { if (t == null) continue; float x = t.GetItemCategoryPriceIndex(c); if (x < v) v = x; }
            if (!(v > 0f) || v >= 1000f) v = 1f;
            _minIdx[c] = v;
            return v;
        }

        private static int Price(TradeItemPriceFactorModel m, Lot l, MobileParty mp, int k) { return Math.Max(1, CaravanGoods.PriceAtExtra(m, l.El, mp, l.D, l.Q, k, l.Wv)); }

        /// <summary>Najwieksze k w [from, to], dla ktorego k-ta sztuka (przy polce + Q) po mycie >= thr (cena maleje z k); from - 1, gdy zadna.</summary>
        private static int MaxK(TradeItemPriceFactorModel m, Lot l, MobileParty mp, double keep, double thr, int from, int to)
        {
            if (to < from) return from - 1;
            if (Price(m, l, mp, from) * keep < thr) return from - 1;
            int lo = from, hi = to;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Price(m, l, mp, mid) * keep >= thr) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>Zaplata miasta za sztuki from..to (przed mytem) - trapez cen pierwszej i ostatniej (gra placi sztuka po sztuce, cena maleje).</summary>
        private static double Money(TradeItemPriceFactorModel m, Lot l, MobileParty mp, int from, int to)
        {
            if (to < from) return 0.0;
            int n = to - from + 1;
            return n * 0.5 * (Price(m, l, mp, from) + Price(m, l, mp, to));
        }

        /// <summary>
        /// Zysk sprzedazy jednej wizyty karawany w miescie (zl po mycie, minus koszt sztuk z ksiegi): kolejnosc jak przy wjezdzie. 1) T1 (towar handlowy poza surowcami
        /// masowymi, najwiekszy zysk na denara najpierw): sztuki do limitu T1 (polowa stosu; przy kluczu A ponad polowe tylko po cenie >= max(prog, DepthNetAged - przewoz
        /// odcinka)), kazda po mycie >= prog, zaplata razem <= (kasa - rezerwa BuyFloorPeek) / 20. 2) BK SellGoods na reszte - wzor BK (h = sqrt(min(V, 5000) / 5000) x 0.4,
        /// indeks / srednia >= 1.1 - h, indeks >= (0.8 srednia + 0.2 najnizszy) x (1.1 - h), sztuki = round(min(ratio^0.67 x (n8 x wartosc + n8 x 200) x 3 x wl., 20 x popyt)
        /// / cena)), cala kasa miasta po T1 (BK czyta kase raz), potem nasz straznik kosztu (MaxAtCost na polce po T1). 3) Surowce masowe - regula CaravanBulk.Sell (cala
        /// nadwyzka kasy ponad rezerwe, brak miasta minus towar w drodze). Polka = polka miasta + towar innych karawan jadacych tam (Inbound) - to, co miasto zje w dniach
        /// drogi. route - prog z CaravanGoods.SaleFloor (te same progi co T1 i straznik); pomiar - koszt sztuki TYLKO ODCZYT. Sprzedaz gry ('inne') bez modelu.
        /// </summary>
        internal static double SaleValue(MobileParty mp, Town town, double days, bool sameDay, bool route, Dictionary<ItemObject, int> pantry, out int units, out double rev, out double cost)
        {
            units = 0; rev = 0; cost = 0;
            var s = Settings.Current;
            var cg = Campaign.Current;
            var model = cg != null && cg.Models != null ? cg.Models.TradeItemPriceFactorModel : null;
            var st = town != null ? town.Settlement : null;
            var r = mp != null ? mp.ItemRoster : null;
            if (s == null || model == null || st == null || town.MarketData == null || r == null) return 0.0;
            double keep = 1.0 - TownToll.CaravanRateFor(mp, st);
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            int spare = town.Gold - TownPurse.BuyFloorPeek(town, reserve);
            double t1Left = s.CaravanSellWhereDear ? Math.Max(0, spare) / (double)TownDayShare : 0.0;
            if (sameDay && t1Left > 0)
            {
                int peek = CaravanGoods.T1LeftPeek(town);                    // recenzja 139: limit doby juz czesciowo wydany przez inne karawany
                if (peek >= 0) t1Left = Math.Min(t1Left, peek);
            }
            double bkGold = town.Gold;
            bool a1 = AOn(s);
            double leg = Leg, dc = -1, cargo = -1;
            var lots = new List<Lot>();
            var vcat = new Dictionary<ItemCategory, double>();
            HashSet<ItemObject> seen = null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.ItemCategory == null) continue;
                double v0; vcat.TryGetValue(it.ItemCategory, out v0); vcat[it.ItemCategory] = v0 + el.Amount * (double)(it.Value + 10);   // BK RefreshTotalValueOfItemsAtCategoryForParty
                if (!Goods(it)) continue;
                if (seen == null) seen = new HashSet<ItemObject>();
                if (!seen.Add(it)) continue;
                int have = r.GetItemNumber(it), kp = 0;
                if (pantry != null && it.IsFood) pantry.TryGetValue(it, out kp);
                int trade = have - Math.Max(0, kp);
                if (trade <= 0) continue;
                double unit = CaravanGoods.HeldCost(mp, it, have) / Math.Max(1, have);
                double thr;
                if (route)
                {
                    if (!_floorC.TryGetValue((it, keep), out thr)) { thr = CaravanGoods.SaleFloor(mp, it, st); _floorC[(it, keep)] = thr; }
                }
                else thr = unit;
                var cat = it.ItemCategory;
                var inb = CaravanRoute294c.InboundFor(town, cat, mp);
                double eat = CaravanTrade284.MktTownValue(town, cat) * Math.Max(0.0, days);
                double q = inb != null ? Math.Max(0.0, inb[1] - eat) : 0.0;
                double qn = inb != null && inb[1] > 0 ? Math.Max(0.0, inb[0] * (q / inb[1])) : 0.0;
                var lot = new Lot { It = it, El = new EquipmentElement(it), Have = have, Trade = trade, Unit = unit, Thr = thr, Q = q, Qn = qn, D = town.MarketData.GetCategoryData(cat),
                                    Wv = Math.Max(1, HistoricalPrices.ShelfWorth(it)), Bulk = CaravanBulk.Owns(it) };
                lot.P1 = Price(model, lot, mp, 1);
                lot.Gain = (lot.P1 * keep - thr) / lot.P1;
                lots.Add(lot);
            }
            if (lots.Count == 0) return 0.0;
            lots.Sort((a, b) => b.Gain.CompareTo(a.Gain));                  // T1: najpierw najwiekszy zysk na denara kasy miasta
            foreach (var l in lots)
            {
                int sold = 0; double money = 0;
                if (l.Bulk)
                {
                    // CaravanBulk.Sell (D1 294c): cala nadwyzka kasy ponad rezerwe / cena pierwszej sztuki, brak miasta minus towar innych karawan w drodze, prog sprzedazy
                    int k = MaxK(model, l, mp, keep, l.Thr, 1, l.Trade);
                    int kc = spare > 0 ? spare / l.P1 : 0;
                    k = Math.Min(k, kc);
                    int lack = CaravanBulk.SellableAt(town, l.It);
                    if (lack >= 0) k = Math.Min(k, Math.Max(0, lack - (int)Math.Ceiling(l.Qn)));
                    if (k > 0) { sold = k; money = Money(model, l, mp, 1, k); }
                }
                else
                {
                    // 1) T1
                    int half = (l.Have + 1) / 2, lim = a1 ? l.Trade : Math.Min(l.Trade, half);
                    int k1 = 0;
                    if (t1Left > 0)
                    {
                        int kp = MaxK(model, l, mp, keep, l.Thr, 1, Math.Min(lim, half));
                        if (a1 && kp >= half && lim > half)
                        {
                            double dn = DnAged(mp, l.It);
                            if (!double.IsNaN(dn))
                            {
                                if (dc < 0) { dc = DayCost(mp); cargo = CargoRO(mp); }
                                double need = Math.Max(l.Thr, dn - Carry(dc, l.Unit, leg, cargo));
                                kp = MaxK(model, l, mp, keep, need, half + 1, lim);
                            }
                        }
                        k1 = kp;
                        if (k1 > 0 && Money(model, l, mp, 1, k1) > t1Left)
                        {
                            int lo = 0, hi = k1;
                            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (Money(model, l, mp, 1, mid) <= t1Left) lo = mid; else hi = mid - 1; }
                            k1 = lo;
                        }
                    }
                    if (k1 > 0) { double m1 = Money(model, l, mp, 1, k1); t1Left -= m1; bkGold -= m1; money += m1; sold += k1; }
                    // 2) BK SellGoods na reszte (wzor BK, cala kasa po T1, nasz straznik kosztu)
                    int rest = l.Trade - k1;
                    var cat = l.It.ItemCategory;
                    float avg = CaravanGoods.AvgIndexOf(cat);
                    if (rest > 0 && avg > 0f && bkGold > 0)
                    {
                        double vc; vcat.TryGetValue(cat, out vc); vc = Math.Max(0.0, vc - k1 * (double)(l.It.Value + 10));
                        double h = Math.Sqrt(Math.Min(vc, 5000.0) / 5000.0) * 0.4, lim6 = 1.1 - h;
                        double pf = model.GetBasePriceFactor(cat, (float)(l.D.InStoreValue + l.Q + k1 * (double)l.Wv), l.D.Supply, l.D.Demand, false, 0);
                        double ratio = pf / avg, n7 = 0.8 * avg + 0.2 * MinIdx(cat);
                        if (ratio >= lim6 && pf >= n7 * lim6)
                        {
                            double n8 = pf - n7 * lim6;
                            double n14 = (ratio > 1.0 ? Math.Pow(ratio, 0.67) : ratio) * (n8 * l.It.Value + n8 * 200.0) * ((int)cat.Properties == 2 ? 1.1 : 1.0) * 3.0;
                            if (n14 > l.D.Demand * 20.0) n14 = l.D.Demand * 20.0;
                            int p = Price(model, l, mp, k1 + 1);
                            int n = (int)Math.Round(n14 / p);
                            if (n > rest) n = rest;
                            if (n * (double)p > bkGold) n = (int)(bkGold / p);
                            if (n > 0 && s.CaravanBkSellAtCost) n = MaxK(model, l, mp, keep, l.Thr, k1 + 1, k1 + n) - k1;
                            if (n > 0) { money += Money(model, l, mp, k1 + 1, k1 + n); sold += n; }
                        }
                    }
                }
                if (sold > 0) { units += sold; rev += money * keep; cost += sold * l.Unit; }
            }
            return rev - cost;
        }

        // ------------------------------------------------------------ B: limit BK w monecie towaru (transpiler BuyCategory)
        /// <summary>Limit kategorii dla tej karawany: 1 500 (BK), przy kluczu B dla kategorii pod zasadami 284 (Tracked) i karawany z zasadami zakupu w tej wizycie
        /// 1 500 / przelicznik monety kategorii, gdy towar podrozal w nowej monecie (przelicznik < 1; HistoricalPrices.CoinRatio - ten sam, ktorym przeliczono ceny).</summary>
        internal static float CapFor(MobileParty mp, ItemCategory cat, bool active)
        {
            var s = Settings.Current;
            var c = Campaign.Current;
            if (!BOn(s) || !active || cat == null || c == null || !c.GameStarted || !CaravanTrade284.Tracked(cat)) return BkCap;
            float r = HistoricalPrices.CoinRatio(cat);
            if (!(r > 0f) || r >= 1f) return BkCap;
            return BkCap / r;
        }

        /// <summary>Transpiler: pierwsze 'ldc.r4 1500' (porownanie num2 > 1500) -> dup; ldarg.1-3; call BkCatCapCmp (num2 do licznikow).</summary>
        public static float BkCatCapCmp(float num2, MobileParty mp, Town town, ItemCategory cat)
        {
            float cap = BkCap;
            try
            {
                bool active = CaravanTrade284.BuyRulesActive(mp);
                cap = CapFor(mp, cat, active);
                LotEnd();
                if (active && CaravanTrade284.Tracked(cat))
                {
                    _bVisits++;
                    if (num2 > BkCap) _bBound++;
                    _lotMp = mp; _lotCat = cat; _lotCap = Math.Min(num2, cap); _lotBk = 0; _lotMaxPrice = 0; _lotLift = cap > BkCap && num2 > BkCap;
                    if (_lotLift) _bLift++;
                }
            }
            catch (Exception e) { Stumble("BkCatCapCmp", e); cap = BkCap; }
            return cap;
        }

        /// <summary>Transpiler: drugie 'ldc.r4 1500' (num2 = 1500) -> ldarg.1-3; call BkCatCap.</summary>
        public static float BkCatCap(MobileParty mp, Town town, ItemCategory cat)
        {
            try { return CapFor(mp, cat, CaravanTrade284.BuyRulesActive(mp)); }
            catch (Exception e) { Stumble("BkCatCap", e); return BkCap; }
        }

        /// <summary>Koniec lotu: lot podniesiony, w ktorym budzet ksiegowany jak BK przekroczyl limit o wiecej niz dwie ceny sztuki (BK konczy przy num3 <= 0, krok
        /// najwyzej round(num3 / cena) + 1 sztuka) - kontrola, ze BK wydaje tyle, ile limit z latki (licznik; recenzja 139: tylko loty podniesione).</summary>
        private static void LotEnd()
        {
            if (_lotMp != null && _lotLift && _lotCap > 0 && _lotBk > _lotCap + 2.0 * _lotMaxPrice) _bOver++;
            _lotMp = null; _lotCat = null; _lotBk = 0; _lotMaxPrice = 0; _lotLift = false;
        }

        /// <summary>Recenzja 139: biezacy lot BK tej karawany i kategorii ma limit podniesiony (num2 > 1 500 i limit w monecie towaru > 1 500) - tylko wtedy dziala
        /// straznik zakupu; lot do 1 500 - jak 138.</summary>
        internal static bool LotLifted(MobileParty mp, ItemCategory cat) { return _lotLift && mp != null && ReferenceEquals(mp, _lotMp) && ReferenceEquals(cat, _lotCat); }

        /// <summary>Recenzja 139: sztuki kroku, ktore 138 kupilby z budzetu 1 500 (BK: num7 = round(num3 / itemPrice), num3 = 1 500 - ksiegowane kroki) - te ida jak
        /// w 138, reguly na sztuke dopiero ponad nie.</summary>
        internal static int LotFree(int pBk)
        {
            double rem = BkCap - _lotBk;
            return rem > 0 && pBk > 0 ? (int)Math.Min(int.MaxValue, Math.Round(rem / pBk)) : 0;
        }

        /// <summary>Straznik zakupu (CaravanTrade284.BuyGuard139), lot podniesiony: krok BK - n0 sztuk zadanych przez BK (ksiegowanie budzetu BK: (n0 + 1) x pBk), n po
        /// przycieciu, free - sztuki jak w 138; ponad nie (n - free) - licznik podniesienia z zaplata (oszacowanie); przyciete wedlug reguly.</summary>
        internal static void NoteBuyStep(MobileParty mp, ItemCategory cat, int n0, int n, int free, int pBk, double extraGold, int cDepth, int cCap, int cGate)
        {
            if (!LotLifted(mp, cat)) return;
            _bClipDepth += cDepth; _bClipCap += cCap; _bClipGate += cGate;
            _lotBk += (n0 + 1) * (double)pBk; if (pBk > _lotMaxPrice) _lotMaxPrice = pBk;
            int extra = n - Math.Min(n, Math.Max(0, free));
            if (extra > 0)
            {
                _bLiftUnits += extra; _bLiftGold += (long)Math.Round(extraGold);
                string id = cat.StringId ?? "?"; int x; _bTop.TryGetValue(id, out x); _bTop[id] = x + extra;
            }
        }

        private static bool IsLdloc(CodeInstruction c)
        {
            var o = c.opcode;
            return o == OpCodes.Ldloc || o == OpCodes.Ldloc_S || o == OpCodes.Ldloc_0 || o == OpCodes.Ldloc_1 || o == OpCodes.Ldloc_2 || o == OpCodes.Ldloc_3;
        }

        /// <summary>Transpiler BKCaravansBehavior.BuyCategory: dokladnie 2 x 'ldc.r4 1500' (if (num2 > 1500f) num2 = 1500f;), pierwsze po ldloc - inaczej bez zmian (BRAK).</summary>
        public static IEnumerable<CodeInstruction> BuyCatTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var idx = new List<int>();
            for (int i = 0; i < list.Count; i++)
                if (list[i].opcode == OpCodes.Ldc_R4 && list[i].operand is float f && f == BkCap) idx.Add(i);
            _bFound = idx.Count;
            var cmp = AccessTools.Method(typeof(CaravanWait139), nameof(BkCatCapCmp));
            var cap = AccessTools.Method(typeof(CaravanWait139), nameof(BkCatCap));
            if (idx.Count != 2 || idx[0] <= 0 || !IsLdloc(list[idx[0] - 1]) || cmp == null || cap == null) { _wBuy = false; return list; }
            _wBuy = true;
            var outl = new List<CodeInstruction>(list.Count + 8);
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (i == idx[0])
                {
                    outl.Add(new CodeInstruction(OpCodes.Dup) { labels = c.labels, blocks = c.blocks });
                    outl.Add(new CodeInstruction(OpCodes.Ldarg_1)); outl.Add(new CodeInstruction(OpCodes.Ldarg_2)); outl.Add(new CodeInstruction(OpCodes.Ldarg_3));
                    outl.Add(new CodeInstruction(OpCodes.Call, cmp));
                }
                else if (i == idx[1])
                {
                    outl.Add(new CodeInstruction(OpCodes.Ldarg_1) { labels = c.labels, blocks = c.blocks });
                    outl.Add(new CodeInstruction(OpCodes.Ldarg_2)); outl.Add(new CodeInstruction(OpCodes.Ldarg_3));
                    outl.Add(new CodeInstruction(OpCodes.Call, cap));
                }
                else outl.Add(c);
            }
            return outl;
        }

        // ------------------------------------------------------------ C: cel z kosztem drogi
        /// <summary>Prefiks przebiegu FindNextDestinationForCaravan (z CaravanRoute294c.FindPrefix) - oceny od nowa.</summary>
        internal static void PassStart() { _recs.Clear(); }

        /// <summary>Postfiks GetTradeScoreForTown (Priority.Last - po BKROTPatch, IslandRoads, RiverBarges): ocena BK miasta w przebiegu z limitem odleglosci (tylko zapis,
        /// wynik bez zmian).</summary>
        public static void ScorePostfix139(MobileParty __0, Town __1, bool __4, float __result)
        {
            if (!__4 || !(__result > 0f) || __1 == null) return;
            var d = CaravanRoute294c.Deciding;
            if (d == null || !ReferenceEquals(__0, d) || !COn(Settings.Current)) return;
            _recs[__1] = __result;
        }

        /// <summary>FindNextDestinationForCaravan(distanceCut true) z wyborem BK b* (CaravanRoute294c.FindPostfix): karawana z ladunkiem - miasta nie dalsze niz b*,
        /// utknieta (klucz A, bez sprzedazy w 7 dobach) - do max(b*, zmierzony odcinek); sposrod 10 najblizszych dozwolonych miasto z najwiekszym
        /// R = (zysk sprzedazy - koszt dnia x dni) / max(1, dni), gdy R > 0 i lepsze niz R wyboru BK. Bez zmian: karawana pusta, bez ladunku, przebieg bez limitu.</summary>
        internal static void Route(MobileParty mp, ref Town result)
        {
            var s = Settings.Current;
            if (!COn(s) || mp == null || result == null || result.Settlement == null) return;
            long t0 = Stopwatch.GetTimestamp();
            _floorC.Clear(); _dnC.Clear();
            try
            {
                var r = mp.ItemRoster;
                var pantry = CaravanTrade284.PantryCopy(mp);
                bool has = false;
                for (int i = 0; r != null && i < r.Count && !has; i++)
                {
                    var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (!Goods(it)) continue;
                    int kp = 0; if (pantry != null && it.IsFood) pantry.TryGetValue(it, out kp);
                    if (r.GetItemNumber(it) - Math.Max(0, kp) > 0) has = true;
                }
                if (!has) return;
                _cT++;
                double cargo = CargoRO(mp);
                bool loaded = cargo >= mp.PartyTradeGold;
                if (!loaded && mp.Owner != null)
                {
                    int cap, floor;
                    CaravanSettle.CapFloor(mp, out cap, out floor);
                    loaded = cap - floor > 0 && cargo >= cap - floor;
                }
                bool stuck = AOn(s) && CaravanTrade284.Stuck7(mp, (int)CampaignTime.Now.ToDays);   // recenzja 139: ksiega 284 co najmniej 7 dob (nie nowa karawana)
                if (loaded) _cP++;
                if (stuck) _cQ++;
                if (!loaded && !stuck) return;
                // recenzja 139: wybor BK = dom karawany (przyciaganie domu BK rosnie z dobami od ostatniej wizyty) albo miasto udzialowca klanu wlasciciela (BK x1.5,
                // wyplaty klanu K259) - bez zmian
                if (mp.HomeSettlement != null && result.Settlement == mp.HomeSettlement) { _cHome++; return; }
                if (Stakeholder(mp, result.Settlement)) { _cStake++; return; }
                double db = Days(mp, result.Settlement);
                double dmax = stuck ? Math.Max(db, Leg) : db;
                var cand = new List<KeyValuePair<Town, double>>();
                foreach (var kv in _recs)
                {
                    var t = kv.Key;
                    if (t == null || t == result || t.Settlement == null) continue;
                    double d = Days(mp, t.Settlement);
                    if (d <= dmax + 1e-6) cand.Add(new KeyValuePair<Town, double>(t, d));
                }
                double dc = DayCost(mp);
                int u; double rv, cs;
                double vb = SaleValue(mp, result, db, SameDay(mp, result.Settlement), true, pantry, out u, out rv, out cs);
                double rb = (vb - dc * db) / Math.Max(1.0, db);
                Town best = null; double rbest = rb, vbest = 0, dbest = 0, rmax = rb;
                if (cand.Count > 0)
                {
                    cand.Sort((a, b) => a.Value.CompareTo(b.Value));
                    for (int i = 0; i < cand.Count && i < RouteTowns; i++)
                    {
                        double d = cand[i].Value;
                        double v = SaleValue(mp, cand[i].Key, d, SameDay(mp, cand[i].Key.Settlement), true, pantry, out u, out rv, out cs);
                        double rr = (v - dc * d) / Math.Max(1.0, d);
                        if (rr > rmax) rmax = rr;
                        if (rr > rbest) { rbest = rr; best = cand[i].Key; vbest = v; dbest = d; }
                    }
                }
                if (!(rmax > 0)) _cNone++;
                if (best == null || !(rbest > 0)) return;
                _cE++;
                if (dbest < db - 1e-6) _cF++; else if (dbest > db + 1e-6) _cG++;
                if (stuck) _cD++;
                _cDb += db; _cDn += dbest; _cVb += vb; _cVn += vbest; _cRb += dc * db; _cRn += dc * dbest;
                result = best;
            }
            catch (Exception e) { Stumble("Route", e); }
            finally { _floorC.Clear(); _dnC.Clear(); _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Recenzja 139: miasto udzialowca klanu wlasciciela karawany (BK CaravanOrdersBehavior.IsClanStakeholderTown - ocena BK x1.5); bez BK - false.</summary>
        private static bool Stakeholder(MobileParty mp, Settlement st)
        {
            if (_stakeMi == null || _ordInst == null || st == null || mp == null || mp.Owner == null || mp.Owner.Clan == null) return false;
            try
            {
                var beh = _ordInst.GetValue(null, null);
                return beh != null && _stakeMi.Invoke(beh, new object[] { mp.Owner.Clan, st }) is bool b && b;
            }
            catch (Exception e) { Stumble("Stakeholder", e); return false; }
        }

        // ------------------------------------------------------------ doba
        private static double Med(List<double> l, double q = 0.5)
        {
            if (l == null || l.Count == 0) return double.NaN;
            l.Sort();
            int i = (int)Math.Floor(q * (l.Count - 1) + 0.5);
            return l[Math.Max(0, Math.Min(l.Count - 1, i))];
        }

        private static string F(double v, string f) { return double.IsNaN(v) ? "-" : v.ToString(f, Inv); }

        /// <summary>Linia doby 'Karawany - czekanie, limit BK i droga (139): dzien N' (po linii 294c). Pomiar czekajacych (karawany klasy 3 linii 298 - towar z rynkiem
        /// z zyskiem, bez sprzedazy w 7 dobach) takze przy kluczach NIE: towar po koszcie, doby i zold od ostatniej sprzedazy, najblizsze miasto, ktore placi koszt + droge
        /// (SaleValue - ten sam rachunek co C, prog = koszt sztuki, tylko odczyt).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            var sb = new StringBuilder(1600);
            try
            {
                LotEnd();
                double leg = Leg;
                // koszt dnia karawan handlujacych
                var dcL = new List<double>(); double wS = 0, fS = 0;
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (mp == null || !mp.IsActive || mp.ItemRoster == null || !CaravanBulk.TradesForT1(mp)) continue;
                    try { double w, f; dcL.Add(DayCost(mp, out w, out f)); wS += w; fS += f; }
                    catch (Exception e) { Stumble("Daily(koszt)", e); }
                }
                int nC = dcL.Count;
                double dcMed = Med(dcL);
                // czekajace (klasa 3 linii 298)
                var wait = CaravanTrade284.Waiting298;
                double vSum = 0, zSum = 0; int inLeg = 0, far = 0, none = 0;
                var daysL = new List<double>(); var roadL = new List<double>(); var ratioL = new List<double>();
                // recenzja 139: szukanie miasta (do 12 x SaleValue) tylko dla proby co k-tej karawany (najwyzej MeasureMax), przesuwanej co dobe; reszta miar - wszystkie
                int stride = Math.Max(1, (wait.Count + MeasureMax - 1) / MeasureMax), off = stride > 1 ? today % stride : 0, wi = -1;
                foreach (var mp in wait)
                {
                    wi++;
                    if (mp == null || !mp.IsActive || mp.ItemRoster == null) continue;
                    try
                    {
                        double dc = DayCost(mp);
                        vSum += CargoRO(mp);
                        int ds = CaravanTrade284.DaysSinceSale(mp, today);
                        daysL.Add(ds); zSum += dc * ds;
                        if (wi % stride != off) continue;
                        _mSample++;
                        _dnC.Clear();
                        var pantry = CaravanTrade284.PantryCopy(mp);
                        Vec2 from = mp.CurrentSettlement != null ? mp.CurrentSettlement.GetPosition2D : mp.GetPosition2D;
                        var towns = new List<KeyValuePair<Town, float>>();
                        foreach (var t in Town.AllTowns)
                        {
                            if (t == null || !t.IsTown || t.Settlement == null || t.IsUnderSiege || t.Settlement == mp.CurrentSettlement) continue;
                            if (t.MapFaction != null && mp.MapFaction != null && t.MapFaction.IsAtWarWith(mp.MapFaction)) continue;
                            towns.Add(new KeyValuePair<Town, float>(t, from.Distance(t.Settlement.GetPosition2D)));
                        }
                        towns.Sort((a, b) => a.Value.CompareTo(b.Value));
                        bool found = false;
                        for (int i = 0; i < towns.Count && i < MeasureTowns && !found; i++)
                        {
                            var t = towns[i].Key;
                            double d = Days(mp, t.Settlement);
                            int u; double rv, cs;
                            double v = SaleValue(mp, t, d, SameDay(mp, t.Settlement), false, pantry, out u, out rv, out cs);
                            if (u > 0 && v - dc * d > 0)
                            {
                                found = true;
                                if (leg > 0 && d <= leg) inLeg++; else far++;
                                roadL.Add(d);
                                if (cs > 0) ratioL.Add(rv / cs);
                            }
                        }
                        if (!found) none++;
                    }
                    catch (Exception e) { Stumble("Daily(czekajace)", e); }
                }
                _dnC.Clear();
                double ms = (Stopwatch.GetTimestamp() - t0 + _ticks) * 1000.0 / Stopwatch.Frequency;
                string top = "-";
                if (_bTop.Count > 0)
                {
                    var tl = new List<KeyValuePair<string, int>>(_bTop);
                    tl.Sort((a, b) => b.Value.CompareTo(a.Value));
                    var ts = new List<string>();
                    for (int i = 0; i < tl.Count && i < 3; i++) ts.Add(tl[i].Key + " " + tl[i].Value);
                    top = string.Join(", ", ts.ToArray());
                }
                string patch = _wBuy ? "2/2" : "BRAK " + _bFound + "/2";
                sb.Append("Karawany - czekanie, limit BK i droga (139): dzien ").Append(day)
                  .Append(" | klucze: czekanie wedlug zoldu ").Append(AOn(s) ? "TAK" : "NIE").Append(", limit BK w monecie towaru ").Append(BOn(s) ? "TAK" : "NIE")
                  .Append(", koszt drogi w celu ").Append(COn(s) ? "TAK" : "NIE")
                  .Append(" | koszt dnia karawany mediana ").Append(F(dcMed, "0")).Append(" zl/d, srednio ").Append(nC > 0 ? ((wS + fS) / nC).ToString("0", Inv) : "-")
                  .Append(" zl/d (zold ").Append(nC > 0 ? (wS / nC).ToString("0", Inv) : "-").Append(" + jedzenie ").Append(nC > 0 ? (fS / nC).ToString("0", Inv) : "-")
                  .Append("), karawan ").Append(nC).Append(", odcinek ").Append(leg.ToString("0.00", Inv)).Append(" dob")
                  .Append(" | A: T1 ponad polowe stosu krokow ").Append(_aSteps).Append(", szt. ").Append(_aUnits).Append(" za ").Append(_aNet).Append(" zl (koszt ")
                  .Append(((long)Math.Round(_aCost)).ToString(Inv)).Append(" zl), zatrzymane - czekanie sie oplaca ").Append(_aHeld).Append(" szt.")
                  .Append("; czekajace ").Append(wait.Count).Append(": towar po koszcie ").Append(((long)vSum).ToString(Inv)).Append(" zl, dni od ostatniej sprzedazy mediana ")
                  .Append(F(Med(daysL), "0")).Append(" / p90 ").Append(F(Med(daysL, 0.9), "0")).Append(", zold od ostatniej sprzedazy ").Append(((long)zSum).ToString(Inv))
                  .Append(" zl, najblizsze miasto placace >= koszt + droga (proba ").Append(_mSample).Append("): w odcinku ").Append(inLeg).Append(" / dalej ").Append(far).Append(" / brak ").Append(none)
                  .Append(", dni drogi mediana ").Append(F(Med(roadL), "0.0")).Append(", cena tam / koszt mediana ").Append(F(Med(ratioL), "0.00"))
                  .Append("; skierowane utkniete ").Append(_cD)
                  .Append(" | B: wizyt BK ").Append(_bVisits).Append(", limit 1500 wiazal ").Append(_bBound).Append(", podniesiony ").Append(_bLift).Append(" (szt. ").Append(_bLiftUnits)
                  .Append(" za ").Append(_bLiftGold).Append(" zl), lotow ponad limit ").Append(_bOver).Append(", przyciete glebokosc ").Append(_bClipDepth).Append(" / kapital ")
                  .Append(_bClipCap).Append(" / bramka ").Append(_bClipGate).Append(" szt., latka ").Append(patch).Append(", najwiecej: ").Append(top)
                  .Append(" | C: decyzji z towarem ").Append(_cT).Append(" (zaladowane ").Append(_cP).Append(", utkniete ").Append(_cQ).Append("), zmienione ").Append(_cE)
                  .Append(" (blizej ").Append(_cF).Append(", dalej w odcinku ").Append(_cG).Append("), dni drogi BK ").Append(_cE > 0 ? (_cDb / _cE).ToString("0.00", Inv) : "-")
                  .Append(" -> 139 ").Append(_cE > 0 ? (_cDn / _cE).ToString("0.00", Inv) : "-")
                  .Append(", zysk sprzedazy BK ").Append(_cE > 0 ? (_cVb / _cE).ToString("0", Inv) : "-").Append(" -> 139 ").Append(_cE > 0 ? (_cVn / _cE).ToString("0", Inv) : "-")
                  .Append(" zl, koszt drogi ").Append(_cE > 0 ? (_cRb / _cE).ToString("0", Inv) : "-").Append(" -> ").Append(_cE > 0 ? (_cRn / _cE).ToString("0", Inv) : "-")
                  .Append(" zl, bez miasta z zyskiem ").Append(_cNone).Append(", bez zmian - dom ").Append(_cHome).Append(" / udzialowiec ").Append(_cStake)
                  .Append(" | czas ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            }
            catch (Exception e)
            {
                Stumble("Daily", e);
                sb.Clear();
                sb.Append("Karawany - czekanie, limit BK i droga (139): dzien ").Append(day).Append(" | blad linii | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
            }
            Log.Info(sb.ToString());
            ZeroDay();
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            var miss = new List<string>();
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                if (bkc == null) { Log.Info("CaravanWait139 (139): BRAK BannerKings.Behaviours.BKCaravansBehavior - limit BK i cel z kosztem drogi bez zmian."); return; }
                var score = AccessTools.Method(bkc, "GetTradeScoreForTown");
                if (score != null && score.ReturnType == typeof(float))
                {
                    var ps = score.GetParameters();
                    if (ps.Length >= 5 && ps[0].ParameterType == typeof(MobileParty) && ps[1].ParameterType == typeof(Town) && ps[4].ParameterType == typeof(bool))
                    {
                        h.Patch(score, postfix: new HarmonyMethod(typeof(CaravanWait139), nameof(ScorePostfix139)) { priority = Priority.Last });
                        _wScore = true;
                    }
                }
                if (!_wScore) miss.Add("GetTradeScoreForTown(MobileParty, Town, .., bool)");
                try
                {
                    var info = score != null ? Harmony.GetPatchInfo(score) : null;
                    if (info != null) _owners = string.Join(", ", new List<string>(info.Owners).ToArray());
                }
                catch (Exception e) { _owners = "blad " + e.GetType().Name; }
                try
                {
                    var tco = AccessTools.TypeByName("BannerKings.Behaviours.Caravans.CaravanOrdersBehavior");
                    _ordInst = tco != null ? AccessTools.Property(tco, "Instance") : null;
                    _stakeMi = tco != null ? AccessTools.Method(tco, "IsClanStakeholderTown", new[] { typeof(Clan), typeof(Settlement) }) : null;
                    if (_ordInst == null || _stakeMi == null || _stakeMi.ReturnType != typeof(bool)) { _ordInst = null; _stakeMi = null; miss.Add("CaravanOrdersBehavior.IsClanStakeholderTown"); }
                }
                catch (Exception e) { _ordInst = null; _stakeMi = null; miss.Add("CaravanOrdersBehavior.IsClanStakeholderTown"); Log.Error("CaravanWait139.ApplyAll(udzialowcy)", e); }
                var buy = AccessTools.Method(bkc, "BuyCategory", new[] { typeof(MobileParty), typeof(Town), typeof(ItemCategory), typeof(float), typeof(float), typeof(List<(EquipmentElement, int)>) });
                if (buy != null && !buy.IsStatic)
                {
                    _wBuy = false;
                    h.Patch(buy, transpiler: new HarmonyMethod(typeof(CaravanWait139), nameof(BuyCatTranspiler)));
                    if (!_wBuy) miss.Add("BuyCategory: 'ldc.r4 1500' x" + _bFound + " (oczekiwane 2, pierwsze po ldloc)");
                }
                else miss.Add("BuyCategory(MobileParty, Town, ItemCategory, float, float, List)");
            }
            catch (Exception e) { Log.Error("CaravanWait139.ApplyAll", e); }
            Log.Info("CaravanWait139 (139): ocena miast BK (zapis, Priority.Last) " + (_wScore ? "wpieta" : "BRAK") + ", latki GetTradeScoreForTown: " + _owners
                     + "; limit kategorii BK BuyCategory " + (_wBuy ? "wpiety 2/2" : "BRAK") + (miss.Count > 0 ? "; BRAK w kodzie BK: " + string.Join(", ", miss.ToArray()) : "") + ".");
        }
    }
}
