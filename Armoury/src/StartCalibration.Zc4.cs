using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZC4 - POPYT OD KOSZYKA (projekt ZYCIE CODZIENNE rozdz. 8 i 14 "ZC4"; KS rozdz. 11.2 "KS i ZC4 jednym kluczem"): czynny TYLKO w stanie zastosowanym KS
    /// (klucz Start Calibration, zapis zastosowany), lambda 0 do D, potem 0 -> 1 przez 14 dob (StartCalibration.Ramp.cs).
    /// RECENZJA 16.10 (krytyczne): szacunek popytu do wzoru ceny = to, co lud je i zuzywa (miasto + wsie) + WSAD przetworow (pary ZC5: ziarno na chleb, placek i piwo,
    /// winogrona na wino, oliwki na oliwe, miod na miod pitny, glina na garnki, zywiec na mieso z uboju - z wyrobu, ktory kupuje lud) + 1:1 (wojsko, zamki, dwory -
    /// srednia ksiegi towarow swiata x udzial ludzi rynku miasta; glina, wapien, marmur i narzedzia takze budowy - jak 1:1 Planu); zywiec - dzisiejszy popyt + lambda x wsad
    /// (bez tego cena gliny, wapienia i marmuru szla na podloge, a regale scinaly rece bez konca). Ceny miasta liczone raz na dobe (koszt) i uzyte w slowniku budzetu,
    /// drabinie i zakupach wsi; czas hakow w linii ZC4.
    ///  - POTRZEBA (raz na dobe, po stawkach KS): miasto - ludzie stanow z ksiegi stanow x koszyk stanu krainy (KS-K) w kg, cala z targu (rozdz. 8.1), zboze miasta: chleb 45%,
    ///    placek 10%, ziarno 45%; wies zastosowana - to, czego nie zjada z wlasnego pola (StartCalibration.Live.cs VillageBasket); w sztukach gry = kg / (sigma x kg sztuki).
    ///    Ta sama potrzeba co popyt Planu (cywile), wiec podaz KS na targ = popyt targu.
    ///  - POPYT U ZRODLA (HistoricalPrices.DemandPostfix, szacunek popytu do wzoru ceny - wywolanie z dodatkiem dobrobytu): kategoria koszyka = (1 - lambda) x dzis +
    ///    lambda x (miasto + wsie kupujace w tym miescie) x wartosc sztuki (przy cenie surowcow masowych z prawdziwego zuzycia - / czesc domowa, ktora RawPrice mnozy).
    ///  - BUDZET DNIA (postfiks UpdateDemandShift, priorytet 420 - po zamiennikach jedzenia i budzecie bez wytworcy, przed kotwica kiesy ludu): kategoria koszyka =
    ///    (1 - lambda) x budzet BK dzis + lambda x potrzeba x cena targu (w zlocie); BK CalculateBudget tych kategorii w tym miescie zwraca budzet bez zmian (postfiks
    ///    VeryLow) - kupione = potrzeba x cena, potem kotwica kiesy (TownFolk.AnchorDict) tnie do tego, na co stac lud: KOTWICA = min(potrzeba x cena, kiesa).
    ///    Piwo domowe: czesc piwa, ktorej brak na polce, lud kupuje jako ziarno (0.4 kg na litr - ta sama receptura co Plan).
    ///  - DRABINA ZAKUPOW (TownFolk.AnchorDict, gdy kiesy nie stac na caly koszyk jedzenia): budzet jedzenia po kolei - przetrwanie (0.47 kg ziarna na osobe z najtanszego
    ///    za kilogram: ziarno, chleb, placek), sol, napoj (najtanszy za litr), bialko (najtansze za kcal: mieso, ryby, ser, maslo, jaja), reszta koszyka po rowno.
    ///  - KOTWICA KIESY (TownFolk.Anchor): koszyki klas BW/BR/BP/BS zastapione kosztem koszyka stanow po cenach targu (W: wyrobnicy i chlopi miasta, R: rzemieslnicy
    ///    + 40% niewolnych, P: kupcy i szlachta + 60% niewolnych) z waga lambda; bez sufitu "koszyk / wydatek" - jedynym sufitem jest kiesa.
    ///  - KOSZYK WSI (TownFolk.Villages -> TownFolk.Zc4BuyVillage): lambda x koszyk wsi z kiesy wsi w miescie targowym, po kolei drabiny; stary koszyk BV x (1 - lambda).
    ///  - OFERTA NADWYZKI ludu (lepszy stol, lista dostatku) do kategorii koszyka x (1 - lambda) - koszyk stanow juz ma lepszy stol bogatych; reszta zostaje w kiesie.
    ///  - OPAL MIESZCZAN (TownFolk.FirewoodDaily): (1 - lambda) x dzis + lambda x 1.5 kg na osobe (tabela KS) - ta sama droga zakupu (zapas rzemiosla pierwszy).
    /// Zloto: zadnej nowej monety - lud placi kasie kupcow (PayForGoods), wies placi kasie miasta (BuyGoods). Linia "Koszyk stanow (ZC4): dzien N".
    /// </summary>
    internal static partial class StartCalibration
    {
        private const string PZ = "Koszyk stanow (ZC4): ";
        internal static readonly string[] ZIds = KsBasket.Ids;
        internal const int ZN = KsBasket.N;
        internal const int ZGrain = KsBasket.Grain, ZBread = KsBasket.Bread, ZPie = KsBasket.Pie, ZMeat = KsBasket.Meat, ZWhale = KsBasket.Whale, ZFish = KsBasket.Fish, ZCheese = KsBasket.Cheese,
                           ZButter = KsBasket.Butter, ZEgg = KsBasket.Egg, ZApple = KsBasket.Apple, ZCarrot = KsBasket.Carrot, ZOrange = KsBasket.Orange, ZGrape = KsBasket.Grape,
                           ZOlives = KsBasket.Olives, ZDate = KsBasket.Date, ZOil = KsBasket.Oil, ZSalt = KsBasket.Salt, ZHoney = KsBasket.Honey, ZSpice = KsBasket.Spice, ZBeer = KsBasket.Beer,
                           ZWine = KsBasket.Wine, ZMead = KsBasket.Mead, ZLinen = KsBasket.Linen, ZFelt = KsBasket.Felt, ZLeather = KsBasket.Leather, ZTools = KsBasket.Tools,
                           ZPottery = KsBasket.Pottery, ZWhaleOil = KsBasket.WhaleOil, ZWood = KsBasket.Wood, ZFur = KsBasket.Fur, ZFlax = KsBasket.Flax, ZWool = KsBasket.Wool,
                           ZHides = KsBasket.Hides, ZIron = KsBasket.Iron, ZClay = KsBasket.Clay, ZLime = KsBasket.Lime, ZMarble = KsBasket.Marble;
        // dodatek do dzisiejszego popytu (zbytki sterowane pieniedzmi + kozuchy KS-K i przyprawy koszyka) - reszta kategorii koszyka ZASTEPUJE dzisiejszy popyt
        private static bool ZAdd(int z) { return z == ZSpice || z == ZFur; }
        // kategorie bez udzialu w koszyku ludu - budowy i rzemioslo: popyt ceny = wsad przetworow + 1:1 z budowami (jak Plan; recenzja 16.10), lud ich nie kupuje (jak Plan)
        private static bool BuildUse(int z) { return z == ZClay || z == ZLime || z == ZMarble || z == ZTools; }
        // kolejnosc drabiny zakupow wsi (przetrwanie, sol, napoj, bialko, reszta jedzenia, przedza, rzeczy)
        internal static readonly int[] ZLadder =
        {
            ZGrain, ZSalt, ZBeer, ZWine, ZMead, ZMeat, ZWhale, ZFish, ZCheese, ZButter, ZEgg, ZApple, ZCarrot, ZOrange, ZGrape, ZOlives, ZDate, ZOil, ZHoney, ZSpice,
            ZFlax, ZWool, ZLinen, ZFelt, ZLeather, ZTools, ZPottery, ZWhaleOil, ZFur
        };
        private static readonly double[] ZKcalKg = BuildZKcal();
        private static double[] BuildZKcal()
        {
            var k = new double[ZN];
            k[ZGrain] = 3300; k[ZBread] = 3300; k[ZPie] = 3300; k[ZMeat] = 2200; k[ZWhale] = 2200; k[ZFish] = 1100; k[ZCheese] = 3600; k[ZButter] = 7200; k[ZEgg] = 1450;
            k[ZBeer] = 400; k[ZWine] = 750; k[ZMead] = 1000;
            return k;
        }

        private sealed class ZTown
        {
            public Town T; public Settlement St; public int Market; public double Lam, People, Surv;
            public double Hands; public bool Garum;              // ZC5: rece przetworow (roboczodni/d) i kraina garum
            public readonly double[] Q = new double[ZN];         // lud miasta (sztuki/d, pelna lambda)
            public readonly double[] QV = new double[ZN];        // wsie kupujace w tym miescie (sztuki/d, pelna lambda)
            public double Fire;                                  // opal (ladunki/d, pelna lambda)
            public readonly double[] Cls = new double[3];        // koszt koszyka klas W/R/P (zl/d przy cenach targu, pelna lambda, bez opalu)
            public readonly double[] Der = new double[ZN];       // wsad przetworow ZC5 z wyrobu, ktory kupuje lud miasta i wsi (sztuki/d, pelna lambda)
            public readonly double[] One = new double[ZN];       // 1:1 (wojsko, zamki, dwory) - srednia swiata x udzial ludzi rynku miasta (sztuki/d)
            public readonly double[] DerLive = new double[3];    // zywiec (krowy, swinie, owce) na mieso z uboju (sztuki/d, pelna lambda)
            public readonly int[] Px = new int[ZN];              // cena sztuki na polce miasta (raz na dobe, Zc4Build)
            public readonly int[] Ix = new int[ZN];              // koszt zakupow wsi (poprawka po autotescie 16.10): indeks wiersza towaru na polce (raz na dobe; -1 - brak)
            public float[] Paid;                                 // krok 8: czesc zaplacona koszyka miasta (Plan) - Q juz pomnozone; null - cala
        }

        private static ItemObject[] _zItems;
        private static double[] _zKg;
        private static Dictionary<ItemCategory, int[]> _zCats = new Dictionary<ItemCategory, int[]>();
        private static Dictionary<ItemObject, int> _zIx;
        private static Dictionary<ItemCategory, int> _zLiveCats = new Dictionary<ItemCategory, int>();   // kategoria zywca -> 0 krowy, 1 swinie, 2 owce
        private static ItemObject[] _zLive;
        private static readonly string[] ZLiveIds = { "cow", "hog", "sheep" };
        private static readonly int[] ZLiveItem = { KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep };
        private static long _zTkDemand, _zTkDict, _zTkVill, _zTkLadder; private static int _zDemandN;   // czas hakow (Stopwatch) od ostatniej linii
        private static double _zFoodBud, _zLadCut; private static long _zvUnGold;
        private static volatile Dictionary<Settlement, ZTown> _zt = new Dictionary<Settlement, ZTown>();
        private static volatile Dictionary<Village, float[]> _zv = new Dictionary<Village, float[]>();        // zakupy wsi: pelny koszyk (recenzja 17.10)
        private static Dictionary<Village, float[]> _zvp = new Dictionary<Village, float[]>();                // koszyk zaplacony z Planu (popyt ceny, wsad ZC5, linia)
        // recenzja 17.10: zakupy wsi wedlug rynku od ostatniej linii [m]: chciane / kupione kcal (pelny koszyk x lambda), wartosc przy cenach polki chciana / zaplacona z Planu /
        // kupiona; czesc kcal kupiona wczoraj wedlug wsi (jedzenie robione w domu - racje spichlerza); czas budowy mapy koszyka (koszt doby KS)
        private static readonly double[] _zvKcalW = new double[2], _zvKcalG = new double[2], _zvZlW = new double[2], _zvZlP = new double[2], _zvZlG = new double[2];
        private static readonly Dictionary<Village, float> _zvBought = new Dictionary<Village, float>();
        private static long _zTkBuild;
        private static Town _zcDictTown;                                     // miasto, ktorego slownik budzetu dnia jest juz w zlocie (BK CalculateBudget - bez zmian)
        [ThreadStatic] private static bool _zcBypass;
        private static Func<Town, float, ItemCategory, float> _zBk; private static bool _zBkLooked;
        private static string _zHook = "nie wpiety";
        // liczniki od ostatniej linii
        private static int _zDictN, _zLadderN, _zBrewN, _zBreadN, _zStum; private static double _zBrewPcs, _zBreadPcs, _zDictOld, _zDictNew;
        private static readonly int[] BreadForms = { ZBread, ZPie };
        private static long _zvGold, _zvPcs, _zvBrew; private static int _zvN, _zvNoGold, _zvNoGoods; private static double _zvWant, _zvGot;
        private static readonly double[] _zvRung = new double[3], _zvRungWant = new double[3];

        private static void Zc4Reset()
        {
            _zt = new Dictionary<Settlement, ZTown>(); _zv = new Dictionary<Village, float[]>(); _zvp = new Dictionary<Village, float[]>(); _zcDictTown = null; _zvBought.Clear();
            _zItems = null; _zKg = null; _zCats = new Dictionary<ItemCategory, int[]>(); _zIx = null; _zLiveCats = new Dictionary<ItemCategory, int>(); _zLive = null;
            Zc4Zero();
        }

        private static void Zc4Zero()
        {
            _zDictN = _zLadderN = _zBrewN = _zBreadN = 0; _zBrewPcs = _zBreadPcs = _zDictOld = _zDictNew = 0;
            _zvGold = _zvPcs = _zvBrew = 0; _zvN = _zvNoGold = _zvNoGoods = 0; _zvWant = _zvGot = 0; _zvElse = 0;
            Array.Clear(_zvRung, 0, 3); Array.Clear(_zvRungWant, 0, 3);
            System.Threading.Interlocked.Exchange(ref _zTkDemand, 0); _zTkDict = _zTkVill = _zTkLadder = 0; _zDemandN = 0; _zFoodBud = _zLadCut = 0; _zvUnGold = 0;
            for (int m = 0; m < 2; m++) { _zvKcalW[m] = _zvKcalG[m] = _zvZlW[m] = _zvZlP[m] = _zvZlG[m] = 0; }
            _zTkBuild = 0;
        }

        private static void Zc4Off()
        {
            if (_zt.Count > 0) _zt = new Dictionary<Settlement, ZTown>();
            if (_zv.Count > 0) _zv = new Dictionary<Village, float[]>();
            if (_zvp.Count > 0) _zvp = new Dictionary<Village, float[]>();
        }

        internal static double ZKg(int z) { Zc4EnsureMap(); return _zKg[z]; }
        internal static ItemObject ZItem(int z) { Zc4EnsureMap(); return _zItems[z]; }

        private static void Zc4EnsureMap()
        {
            if (_zItems != null) return;
            var items = new ItemObject[ZN]; var kg = new double[ZN]; var ix = new Dictionary<ItemObject, int>();
            var cats = new Dictionary<ItemCategory, List<int>>();
            for (int z = 0; z < ZN; z++)
            {
                var it = ItemOf(ZIds[z]);
                items[z] = it;
                kg[z] = it != null && it.Weight > 0.01f ? it.Weight : (z == ZWood ? 100.0 : 10.0);
                if (it == null || it.ItemCategory == null) continue;
                if (!ix.ContainsKey(it)) ix[it] = z;
                List<int> l; if (!cats.TryGetValue(it.ItemCategory, out l)) { l = new List<int>(); cats[it.ItemCategory] = l; }
                l.Add(z);
            }
            var cm = new Dictionary<ItemCategory, int[]>();
            foreach (var kv in cats) cm[kv.Key] = kv.Value.ToArray();
            var live = new ItemObject[3]; var lc = new Dictionary<ItemCategory, int>();
            for (int k = 0; k < 3; k++) { live[k] = ItemOf(ZLiveIds[k]); if (live[k] != null && live[k].ItemCategory != null && !cm.ContainsKey(live[k].ItemCategory) && !lc.ContainsKey(live[k].ItemCategory)) lc[live[k].ItemCategory] = k; }
            _zKg = kg; _zIx = ix; _zCats = cm; _zLive = live; _zLiveCats = lc; _zItems = items;
        }

        internal static void Zc4ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            var m = AccessTools.Method(typeof(ItemConsumptionBehavior), "UpdateDemandShift", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>) });
            if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(StartCalibration), nameof(Zc4DictPostfix)) { priority = 420 }); parts.Add("budzet dnia (UpdateDemandShift, 420)"); }
            var b = AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget");
            if (b != null) { h.Patch(b, postfix: new HarmonyMethod(typeof(StartCalibration), nameof(Zc4BudgetPostfix)) { priority = Priority.VeryLow }); parts.Add("wiersz polki BK (CalculateBudget, VeryLow)"); }
            var mc = AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>), typeof(Dictionary<ItemCategory, int>) });
            if (mc != null) { h.Patch(mc, postfix: new HarmonyMethod(typeof(StartCalibration), nameof(Zc4ConsumeEnd)) { priority = Priority.Last }, finalizer: new HarmonyMethod(typeof(StartCalibration), nameof(Zc4ConsumeFin))); parts.Add("koniec konsumpcji (MakeConsumption)"); }
            _zHook = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "BRAK";
            Log.Info(PZ + "latki: " + _zHook + (m == null ? "; BRAK UpdateDemandShift" : "") + (b == null ? "; BRAK BK CalculateBudget (budzet koszyka przez model gry x indeks^0.3)" : "")
                     + " | czynne tylko przy zastosowanej kalibracji startu (KS) - inaczej nic nie zmieniaja.");
        }

        // ============================================================ doba: potrzeba miast i wsi (po stawkach KS)
        /// <summary>Potrzeba koszyka na jutro: miasta (stany ksiegi x koszyk krainy, cala z targu), wsie zastosowane (vil - z rachunku okregu), koszt klas W/R/P, opal;
        /// lambda rynku z rampy; ceny miasta (raz na dobe); wsad przetworow ZC5 z wyrobu ludu i 1:1 swiata wedlug ludzi rynku miasta (popyt ceny - recenzja 16.10).
        /// full - przeplyw na targ KS [rynek x towar] (podzial miesa: mieso / mieso wieloryba / zywiec wedlug podazy rynku). fresh = false (stany ZC nieswieze) -
        /// ostatnia mapa miast z biezaca lambda (recenzja 16.10: nie pusta mapa - bez niej popyt wracal do dzisiejszego przy podazy KS).</summary>
        private static void Zc4Build(int today, List<DailyLife.KsRec> recs, Dictionary<Village, float[]> vil, Dictionary<Village, float[]> vilPaid, double[] full, bool fresh)
        {
            Zc4EnsureMap();
            // recenzja 17.10 (wazne): wsie kupuja pelny koszyk (vil - kiesa i drabina decyduja), popyt ceny, "na zbyt" i wsad ZC5 licza koszyk zaplacony z Planu (vilPaid -
            // to Plan postawil na targu)
            var qvSrc = vilPaid ?? vil;
            if (!fresh && _zt.Count > 0)
            {
                var old = _zt;
                foreach (var zt in old.Values) { zt.Lam = Lam(zt.Market); Array.Clear(zt.QV, 0, ZN); }
                if (qvSrc != null)
                    foreach (var kv in qvSrc)
                    {
                        var v = kv.Key; if (v == null) continue;
                        var mk0 = v.TradeBound ?? v.Bound; ZTown z0;
                        if (mk0 != null && old.TryGetValue(mk0, out z0)) for (int z = 0; z < ZN; z++) z0.QV[z] += kv.Value[z];
                    }
                _zv = vil ?? new Dictionary<Village, float[]>();
                _zvp = vilPaid ?? new Dictionary<Village, float[]>();
                return;
            }
            var map = new Dictionary<Settlement, ZTown>();
            double sigma = Math.Max(1, DailyLife.KsSigma);
            // udzial miesa wieloryba w miesie rynku (kg): wieloryb / (mieso + wieloryb + polowa zywca); udzial zywca w miesie z targu (bez wieloryba)
            var whale = new double[2]; var liveSh = new double[6];
            for (int m = 0; m < 2; m++)
            {
                int o = m * ItemCount;
                double wk = full[o + IWhaleMeat] * _itemKg[IWhaleMeat], md = full[o + IMeat] * _itemKg[IMeat], lt = 0;
                var w = new double[3];
                for (int k = 0; k < 3; k++) { w[k] = full[o + ZLiveItem[k]] * _itemKg[ZLiveItem[k]] * KsTab.MeatOfLive; lt += w[k]; }
                double mk = md + lt;
                whale[m] = wk + mk > 0 ? wk / (wk + mk) : 0.0;
                for (int k = 0; k < 3; k++) liveSh[m * 3 + k] = mk > 0 ? w[k] / mk : 0.0;
            }
            // 1:1 swiata (sztuki/d - srednia ksiegi towarow) i ludzie rynku kazdego miasta (miasto + wsie z tym miastem targowym)
            double world = 0; var mkPeople = new Dictionary<Settlement, double>();
            foreach (var r in recs)
            {
                if (r == null || r.St == null) continue;
                double l = Math.Max(0, r.L); world += l;
                var mkS = r.Town ? r.St : r.Market;
                if (mkS == null) continue;
                double v0; mkPeople.TryGetValue(mkS, out v0); mkPeople[mkS] = v0 + l;
            }
            var one = new double[ZN];
            for (int z = 0; z < ZN; z++)
                if (!ZAdd(z)) one[z] = Math.Max(0.0, Ema(ZIds[z], GoodsLedger.KsArmy) + Ema(ZIds[z], GoodsLedger.KsCastle) + Ema(ZIds[z], GoodsLedger.KsCourt) + (BuildUse(z) ? Ema(ZIds[z], GoodsLedger.KsBuild) : 0.0));
            List<KPair> pairs = null;
            try { pairs = KPairs(); } catch (Exception e) { Stumble("Zc4Build(pary)", e); }
            bool fireOn = FirewoodMechanism();
            var n = new double[KsTab.NN]; var one6 = new double[6]; var px = new double[ZN];
            foreach (var r in recs)
            {
                if (r == null || !r.Town || r.St == null || r.St.Town == null) continue;
                try
                {
                    var t = r.St.Town;
                    var zt = new ZTown { T = t, St = r.St, Market = TownMarket(t), People = Math.Max(0, r.L) };
                    zt.Lam = Lam(zt.Market);
                    var bask = DailyLife.KsBasket(r.Cul, true);
                    bool oil = DailyLife.OilProf(r.ProfId), north = DailyLife.NorthFur(r.ProfId);
                    // cale miasto
                    KsCore.NeedsOf(r.N, bask, oil, north, true, true, r.L, n);
                    TownItems(n, zt.Q, whale[zt.Market], sigma, fireOn, out zt.Fire);
                    // krok 8 (pieniadze Planu): koszyk miasta tylko w czesci, za ktora klasy W/R/P zaplaca z zarobku - popyt ceny, slownik budzetu, przetwory i koszt klas
                    var tp = TownPaidOf(r.St); zt.Paid = tp;
                    if (tp != null) for (int z = 0; z < ZN && z < tp.Length; z++) zt.Q[z] *= Math.Max(0f, Math.Min(1f, tp[z]));
                    zt.Hands = Math.Max(0, r.N[KsTab.ECraft]) * 0.55 / sigma * 0.5;      // ZC5: polowa rak rzemieslnikow miasta (praca stanu w miescie 0.55)
                    zt.Garum = r.Cul != null && KsTab.GarumLands.Contains(r.Cul);
                    double surv = 0; for (int e = 0; e < 6; e++) surv += Math.Max(0, r.N[e]);
                    zt.Surv = surv * 0.47 / (sigma * _zKg[ZGrain]);
                    // ceny miasta raz na dobe (koszt klas, slownik budzetu, drabina, zakupy wsi)
                    var md = t.MarketData;
                    var q1 = new double[ZN];
                    // indeksy wierszy polki (zakupy wsi bez szukania liniowego - recenzja kosztu) i ilosc towaru
                    var ro = r.St.ItemRoster; var cnt = new int[ZN];
                    for (int z = 0; z < ZN; z++) zt.Ix[z] = -1;
                    if (ro != null)
                        for (int k = 0; k < ro.Count; k++)
                        {
                            var el = ro.GetElementCopyAtIndex(k); var ei = el.EquipmentElement.Item; int zi;
                            if (ei == null || el.EquipmentElement.ItemModifier != null || !_zIx.TryGetValue(ei, out zi) || zt.Ix[zi] >= 0) continue;
                            zt.Ix[zi] = k; cnt[zi] = el.Amount;
                        }
                    for (int z = 0; z < ZN; z++)
                    {
                        var it = _zItems[z]; int p = 1;
                        if (it != null) { try { p = md.GetPrice(it, null, false, null); } catch { p = it.Value; } }
                        // F3 (poprawka po autotescie): towaru brak na polce (najwyzej 5 sztuk) - cena wzoru gry idzie na sufit (10 x wartosc) i cielaby budzet koszyka
                        // towarow, ktore SA na polce; w koszcie klas, drabinie i zakupach wsi najwyzej 1.5 x cena startu
                        if (it != null && cnt[z] <= 5) { int cap = (int)Math.Ceiling(1.5 * KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, zt.Market) * Math.Max(1, it.Value)); if (p > cap) p = cap; }
                        px[z] = Math.Max(1, p); zt.Px[z] = (int)px[z];
                    }
                    for (int e = 0; e < 6; e++)
                    {
                        if (!(r.N[e] > 0)) continue;
                        Array.Clear(one6, 0, 6); one6[e] = r.N[e];
                        KsCore.NeedsOf(one6, bask, oil, north, true, false, r.N[e], n);   // bez opalu (opal placi kiesa ponad koszyk - TownFolk.FirewoodDaily)
                        double f0; Array.Clear(q1, 0, ZN);
                        TownItems(n, q1, whale[zt.Market], sigma, true, out f0);
                        double cost = 0;
                        for (int z = 0; z < ZN; z++) if (q1[z] > 0 && _zItems[z] != null) cost += q1[z] * px[z] * (tp != null && z < tp.Length ? Math.Max(0f, Math.Min(1f, tp[z])) : 1f);
                        if (e == KsTab.ELabour || e == KsTab.EPeasant) zt.Cls[0] += cost;
                        else if (e == KsTab.ECraft) zt.Cls[1] += cost;
                        else if (e == KsTab.EMerchant || e == KsTab.EGentry) zt.Cls[2] += cost;
                        else { zt.Cls[1] += 0.4 * cost; zt.Cls[2] += 0.6 * cost; }      // niewolni: wlasciciele R 40% / P 60% (jak kiesa ludu)
                    }
                    double pp; mkPeople.TryGetValue(r.St, out pp);
                    double sh = world > 0 ? pp / world : 0.0;
                    for (int z = 0; z < ZN; z++) zt.One[z] = one[z] * sh;
                    map[r.St] = zt;
                }
                catch (Exception e) { _zStum++; Stumble("Zc4Build(miasto)", e); }
            }
            // wsie: koszyk zaplacony z Planu do miasta targowego (popyt ceny, wsad ZC5)
            if (qvSrc != null)
                foreach (var kv in qvSrc)
                {
                    var v = kv.Key; if (v == null) continue;
                    var mk = v.TradeBound ?? v.Bound;
                    ZTown zt;
                    if (mk == null || !map.TryGetValue(mk, out zt)) continue;
                    for (int z = 0; z < ZN; z++) zt.QV[z] += kv.Value[z];
                }
            // wsad przetworow z wyrobu, ktory kupuje lud miasta i wsi (popyt ceny; budzet ludu bez zmian - lud nie kupuje ziarna na chleb)
            foreach (var zt in map.Values) { try { Zc4Derived(zt, pairs, liveSh); } catch (Exception e) { _zStum++; Stumble("Zc4Derived", e); } }
            _zt = map;
            _zv = vil ?? new Dictionary<Village, float[]>();
            _zvp = vilPaid ?? new Dictionary<Village, float[]>();
        }

        /// <summary>Wsad przetworow ZC5 z wyrobu ludu (sztuki wsadu na dobe przy pelnej lambdzie): chleb, placek i piwo -> ziarno, wino -> winogrona, oliwa -> oliwki,
        /// miod pitny -> miod, garnki -> glina, mieso -> zywiec (czesc miesa z zywca wedlug podazy rynku). Bez garum (wyrob spoza koszyka) i bez zelaza na narzedzia
        /// (zuzycie rzemiosla zelaza liczy RawPrice z prawdziwego zuzycia).</summary>
        private static void Zc4Derived(ZTown zt, List<KPair> pairs, double[] liveSh)
        {
            Array.Clear(zt.Der, 0, ZN); Array.Clear(zt.DerLive, 0, 3);
            if (pairs == null) return;
            foreach (var p in pairs)
            {
                if (p.Garum || p.ZOut < 0) continue;
                double outD = zt.Q[p.ZOut] + zt.QV[p.ZOut];
                if (!(outD > 0)) continue;
                if (p.Live >= 0) outD *= liveSh[(zt.Market == 0 ? 0 : 3) + p.Live];
                for (int j = 0; j < p.InIt.Length; j++)
                {
                    double x = outD * p.Ratio[j];
                    if (p.Live >= 0) zt.DerLive[p.Live] += x;
                    else if (p.ZIn[j] >= 0 && p.ZIn[j] != ZIron) zt.Der[p.ZIn[j]] += x;
                }
            }
        }

        /// <summary>Potrzeby osady (NeedsOf, kg/d) -> sztuki gry na dobe wedlug ZIds (miasto: zboze 45% ziarno, 45% chleb, 10% placek; mieso / wieloryb wedlug podazy rynku;
        /// opal - osobno, do zakupu opalu, albo do kategorii drewna, gdy zakup opalu nieczynny).</summary>
        private static void TownItems(double[] n, double[] q, double whaleShare, double sigma, bool fireOn, out double fire)
        {
            var kg = new double[ZN]; double fk;
            KsBasket.TownKg(n, whaleShare, kg, out fk);
            for (int z = 0; z < ZN; z++) if (kg[z] > 0) q[z] += kg[z] / (sigma * _zKg[z]);
            fire = fk > 0 ? fk / (sigma * _zKg[ZWood]) : 0.0;
            if (!fireOn) { q[ZWood] += fire; fire = 0; }
        }

        /// <summary>Zakup opalu mieszczan czynny (TownFolkFirewood i kiesa ludu) - wtedy opal koszyka idzie ta droga (zapas rzemiosla pierwszy), nie budzetem BK.</summary>
        private static bool FirewoodMechanism() { var s = Settings.Current; return s != null && s.TownFolkFirewood && TownFolk.LiveKey; }

        private static bool ZTownOf(Town town, out ZTown zt)
        {
            zt = null;
            if (town == null || town.Settlement == null || _state != StateApplied) return false;
            var map = _zt;
            if (map.Count == 0 || !map.TryGetValue(town.Settlement, out zt) || zt == null || !(zt.Lam > 0) || !KeyOn) { zt = null; return false; }
            return true;
        }

        // ============================================================ haki: popyt u zrodla, budzet dnia, wiersz polki
        /// <summary>HistoricalPrices.DemandPostfix (wywolanie zewnetrzne, po przeliczeniu monety i m_t): szacunek popytu do wzoru ceny (extra > 0) w kategoriach koszyka =
        /// (1 - lambda) x dzis + lambda x (miasto + wsie) x wartosc sztuki. Budzet dnia (extra 0) - bez zmian tutaj (Zc4DictPostfix).</summary>
        internal static void Zc4Demand(Town town, ItemCategory cat, int extra, ref float result)
        {
            if (extra <= 0 || cat == null) return;
            ZTown zt;
            if (!ZTownOf(town, out zt)) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                int[] zs; int lk;
                double lam = zt.Lam, r;
                if (_zCats.TryGetValue(cat, out zs))
                {
                    double v = 0; bool add = false;
                    foreach (int z in zs)
                    {
                        var it = _zItems[z]; if (it == null) continue;
                        double q = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z] + (z == ZWood ? zt.Fire : 0.0);
                        v += q * Math.Max(1, it.Value);
                        if (ZAdd(z)) add = true;
                    }
                    if (RawPrice.UseOn) { float hs = HistoricalPrices.HouseShare(cat); if (hs > 0.05f && hs < 0.999f) v /= hs; }   // RawPrice mnozy popyt domowy surowcow masowych przez czesc domowa
                    r = add ? result + lam * v : (1 - lam) * result + lam * v;
                }
                else if (_zLiveCats.TryGetValue(cat, out lk) && _zLive != null && _zLive[lk] != null)
                    r = result + lam * zt.DerLive[lk] * Math.Max(1, _zLive[lk].Value);                     // zywiec na mieso z uboju (dzis + lambda x wsad)
                else return;
                if (r >= 0 && !double.IsNaN(r) && !double.IsInfinity(r)) result = (float)r;
            }
            catch (Exception e) { Zc4HookStumble("Zc4Demand", e); }
            finally { System.Threading.Interlocked.Add(ref _zTkDemand, System.Diagnostics.Stopwatch.GetTimestamp() - t0); System.Threading.Interlocked.Increment(ref _zDemandN); }
        }

        /// <summary>Potkniecie haka ZC4 (moze isc z watkow AI): licznik linii ZC4, pierwsze 3 w logu.</summary>
        private static void Zc4HookStumble(string where, Exception e)
        {
            int n = System.Threading.Interlocked.Increment(ref _zStum);
            if (n <= 3) { try { Log.Error("StartCalibration." + where, e); } catch { } }
        }

        /// <summary>TownFolk.Zc4BuyVillage: cena sztuki towaru koszyka z na polce miasta (raz na dobe, Zc4Build); 0 - poza ZC4 (wolajacy pyta gre).</summary>
        internal static int ZPrice(Town t, int z)
        {
            ZTown zt;
            return z >= 0 && z < ZN && ZTownOf(t, out zt) ? zt.Px[z] : 0;
        }

        /// <summary>TownFolk.Zc4BuyVillage (koszt): indeks wiersza towaru koszyka z na polce miasta z doby (Zc4Build) albo -1 (wolajacy sprawdza wiersz i szuka sam).</summary>
        internal static int ZRow(Town t, int z)
        {
            ZTown zt;
            return z >= 0 && z < ZN && ZTownOf(t, out zt) ? zt.Ix[z] : -1;
        }

        /// <summary>TownFolk.Villages: czas zakupow wsi (Stopwatch) do linii ZC4.</summary>
        internal static void Zc4NoteTime(long ticks) { _zTkVill += ticks; }

        /// <summary>BK CalculateBudget przez lancuch Armoury, z pominieciem wiersza koszyka (budzet BK dzisiejszej czesci kategorii).</summary>
        private static float ChainBudget(Town t, float demand, ItemCategory c)
        {
            if (!_zBkLooked)
            {
                _zBkLooked = true;
                try
                {
                    var m = AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget");
                    if (m != null && m.IsStatic) _zBk = (Func<Town, float, ItemCategory, float>)Delegate.CreateDelegate(typeof(Func<Town, float, ItemCategory, float>), m);
                }
                catch { _zBk = null; }
            }
            _zcBypass = true;
            try
            {
                float v = _zBk != null ? _zBk(t, demand, c) : Campaign.Current.Models.SettlementEconomyModel.CalculateDailySettlementBudgetForItemCategory(t, demand, c);
                return float.IsNaN(v) || float.IsInfinity(v) || v < 0f ? 0f : v;
            }
            finally { _zcBypass = false; }
        }

        /// <summary>Postfiks UpdateDemandShift (priorytet 420): slownik budzetu dnia miasta w kategoriach koszyka w ZLOCIE = (1 - lambda) x budzet BK dzis + lambda x potrzeba
        /// x cena targu (dodatki: budzet dzis + lambda x potrzeba x cena). Piwo domowe: brak piwa na polce - czesc piwa jako ziarno (0.4 kg na litr).</summary>
        public static void Zc4DictPostfix(Town __0, Dictionary<ItemCategory, float> __1)
        {
            ZTown zt;
            if (__0 == null || __1 == null || !ZTownOf(__0, out zt)) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (Campaign.Current == null || !Campaign.Current.GameStarted) return;
                var md = __0.MarketData; var ro = __0.Settlement.ItemRoster;
                if (md == null || ro == null) return;
                double lam = zt.Lam;
                var q = new double[ZN];
                Array.Copy(zt.Q, q, ZN);
                // piwo domowe: piwa na polce mniej niz dzienna potrzeba - brakujace litry kupowane jako ziarno (0.4 kg/l)
                var beer = _zItems[ZBeer]; var grain = _zItems[ZGrain];
                if (beer != null && grain != null && q[ZBeer] > 0)
                {
                    int have = ro.GetItemNumber(beer);
                    double miss = Math.Max(0.0, q[ZBeer] - have);
                    if (miss > 0)
                    {
                        q[ZBeer] -= miss;
                        double g = miss * _zKg[ZBeer] * KsTab.BeerGrain / _zKg[ZGrain];
                        q[ZGrain] += g; _zBrewN++; _zBrewPcs += g;
                    }
                }
                // brak chleba albo placka na polce - ziarno (1 kg na kg; drabina rozdz. 8.4: "brak chleba = ziarno")
                if (grain != null)
                    foreach (int z in BreadForms)
                    {
                        var it = _zItems[z];
                        if (it == null || !(q[z] > 0)) continue;
                        double miss = Math.Max(0.0, q[z] - ro.GetItemNumber(it));
                        if (!(miss > 0)) continue;
                        q[z] -= miss; q[ZGrain] += miss * _zKg[z] / _zKg[ZGrain];
                        _zBreadN++; _zBreadPcs += miss;
                    }
                foreach (var kv in _zCats)
                {
                    var c = kv.Key;
                    double bz = 0; bool add = false, food = false;
                    foreach (int z in kv.Value)
                    {
                        var it = _zItems[z]; if (it == null) continue;
                        if (ZAdd(z)) add = true;
                        if (Rung(z) >= 0) food = true;
                        if (q[z] <= 0) continue;
                        bz += q[z] * Math.Max(1, zt.Px[z]);                          // cena miasta z doby (Zc4Build) - recenzja 16.10, koszt
                    }
                    float old; __1.TryGetValue(c, out old);
                    // koszt (poprawka po autotescie 16.10): przy lambda 1 budzet BK kategorii zastepowanej mnozy sie przez 0 - BK CalculateBudget pominiety
                    double bOld = old > 0.01f && (add || lam < 0.9999) ? ChainBudget(__0, old, c) : 0.0;
                    double nv = add ? bOld + lam * bz : (1 - lam) * bOld + lam * bz;
                    if (!(nv >= 0) || double.IsInfinity(nv)) continue;
                    __1[c] = (float)nv;
                    _zDictOld += bOld; _zDictNew += nv;
                    if (food) _zFoodBud += nv;
                }
                _zcDictTown = __0;
                _zDictN++;
            }
            catch (Exception e) { _zStum++; Stumble("Zc4DictPostfix", e); }
            finally { _zTkDict += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Postfiks BK CalculateBudget (VeryLow, po kotwicy wiersza i przed pomiarem nawiasu): w miescie, ktorego slownik dnia jest juz w zlocie (Zc4DictPostfix), wiersz
        /// kategorii koszyka dostaje budzet = reszta slownika (bez indeksu^a i dodatku dobrobytu BK) - kupione = potrzeba x cena, reszta przechodzi na nastepny wiersz.</summary>
        public static void Zc4BudgetPostfix(Town __0, float __1, ItemCategory __2, ref float __result)
        {
            if (_zcBypass || __2 == null || !ReferenceEquals(__0, _zcDictTown)) return;
            try
            {
                if (!_zCats.ContainsKey(__2)) return;
                ZTown zt;
                if (!ZTownOf(__0, out zt)) return;
                __result = __1 > 0f ? __1 : 0f;
            }
            catch (Exception e) { Zc4HookStumble("Zc4BudgetPostfix", e); }
        }

        /// <summary>Koniec konsumpcji miasta: slownik w zlocie tylko w tym nawiasie (inne wywolania CalculateBudget - jak dotad).</summary>
        public static void Zc4ConsumeEnd() { _zcDictTown = null; }
        public static Exception Zc4ConsumeFin(Exception __exception) { _zcDictTown = null; return __exception; }

        /// <summary>TownFolk.AnchorDict: kategoria jedzenia koszyka w miescie z lambda > 0 - budzet jedzenia dzieli drabina (Zc4Ladder), nie rowne ciecie.</summary>
        internal static bool Zc4LadderCat(Town town, ItemCategory c)
        {
            ZTown zt;
            if (c == null || !ZTownOf(town, out zt)) return false;
            int[] zs;
            if (!_zCats.TryGetValue(c, out zs)) return false;
            foreach (int z in zs) if (Rung(z) >= 0) return true;
            return false;
        }

        /// <summary>Towar koszyka jest jedzeniem (szczebel drabiny 0..4).</summary>
        internal static bool ZFood(int z) { return Rung(z) >= 0; }

        /// <summary>Szczebel drabiny: 0 zboze (przetrwanie, potem reszta), 1 sol, 2 napoj, 3 bialko, 4 reszta jedzenia; -1 - poza drabina.</summary>
        private static int Rung(int z)
        {
            switch (z)
            {
                case ZGrain: case ZBread: case ZPie: return 0;
                case ZSalt: return 1;
                case ZBeer: case ZWine: case ZMead: return 2;
                case ZMeat: case ZWhale: case ZFish: case ZCheese: case ZButter: case ZEgg: return 3;
                case ZApple: case ZCarrot: case ZOrange: case ZGrape: case ZOlives: case ZDate: case ZOil: case ZHoney: case ZSpice: return 4;
                default: return -1;
            }
        }

        /// <summary>
        /// DRABINA ZAKUPOW (TownFolk.AnchorDict, kiesy nie stac na caly koszyk jedzenia, ff &lt; 1): budzet B = ff x suma kategorii jedzenia koszyka (keys) - po kolei:
        /// (1) przetrwanie: 0.47 kg ziarna na osobe w najtanszej za kilogram z ziarna / chleba / placka (z towarem na polce), (2) sol, (3) napoj (najtanszy za litr),
        /// (4) bialko (najtansze za kcal), kazda kategoria najwyzej do swojej potrzeby, (5) reszta (reszta zboza, owoce, oliwa, miod, przyprawy) po rowno. Zwraca sume slownika.
        /// </summary>
        internal static double Zc4Ladder(Town town, Dictionary<ItemCategory, float> dict, List<ItemCategory> keys, double ff)
        {
            double tot = 0;
            foreach (var c in keys) { float v; if (dict.TryGetValue(c, out v) && v > 0) tot += v; }
            ZTown zt;
            if (!(tot > 0) || !ZTownOf(town, out zt)) { foreach (var c in keys) { float v; if (dict.TryGetValue(c, out v) && v > 0) dict[c] = (float)(v * ff); } return tot * ff; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            double B = Math.Max(0.0, ff) * tot, left = B;
            var md = town.MarketData; var ro = town.Settlement.ItemRoster;
            var need = new Dictionary<ItemCategory, double>(); var give = new Dictionary<ItemCategory, double>();
            var rung = new Dictionary<ItemCategory, int>(); var unit = new Dictionary<ItemCategory, double>(); var stock = new Dictionary<ItemCategory, bool>();
            foreach (var c in keys)
            {
                float v; dict.TryGetValue(c, out v);
                need[c] = Math.Max(0, (double)v); give[c] = 0;
                int[] zs; _zCats.TryGetValue(c, out zs);
                int z0 = zs != null && zs.Length > 0 ? zs[0] : -1, rg = z0 >= 0 ? Rung(z0) : 4;
                rung[c] = rg < 0 ? 4 : rg;
                double best = double.MaxValue; bool has = false;
                if (zs != null)
                    foreach (int z in zs)
                    {
                        var it = _zItems[z]; if (it == null) continue;
                        int p = zt.Px[z];
                        double per = Math.Max(1, p) / _zKg[z];                                         // zl za kg (litr)
                        if (ZKcalKg[z] > 0 && (rg == 3)) per /= ZKcalKg[z] / 1000.0;                   // bialko: zl za 1000 kcal
                        if (per < best) best = per;
                        if (ro != null && ro.GetItemNumber(it) > 0) has = true;
                    }
                unit[c] = best < double.MaxValue ? best : 1e9; stock[c] = has;
            }
            // (1) przetrwanie: z najtanszej formy zboza z towarem na polce
            ItemCategory cheap = null;
            foreach (var c in keys) if (rung[c] == 0 && (cheap == null || (stock[c] && !stock[cheap]) || (stock[c] == stock[cheap] && unit[c] < unit[cheap]))) cheap = c;
            double grainNeed = 0; foreach (var c in keys) if (rung[c] == 0) grainNeed += need[c];
            if (cheap != null)
            {
                int pz = Math.Max(1, zt.Px[_zCats[cheap][0]]);
                double surv = Math.Min(grainNeed, zt.Surv * zt.Lam * Math.Max(1, pz));
                double g = Math.Min(left, surv);
                give[cheap] += g; left -= g; grainNeed -= g;
            }
            // (2) sol, (3) napoj, (4) bialko - najtansze najpierw, kazda do swojej potrzeby
            for (int rg = 1; rg <= 3 && left > 0; rg++)
            {
                var l = new List<ItemCategory>(); foreach (var c in keys) if (rung[c] == rg) l.Add(c);
                l.Sort((a, b) => unit[a].CompareTo(unit[b]));
                foreach (var c in l) { double g = Math.Min(left, need[c] - give[c]); if (g > 0) { give[c] += g; left -= g; } }
            }
            // (5) reszta po rowno: reszta zboza (wedlug potrzeby form zboza) i szczebel 4
            if (left > 0)
            {
                double rest = 0; foreach (var c in keys) if (rung[c] == 0 || rung[c] == 4) rest += Math.Max(0, need[c] - give[c]);
                double k = rest > 0 ? Math.Min(1.0, left / rest) : 0;
                foreach (var c in keys) if (rung[c] == 0 || rung[c] == 4) { double g = Math.Max(0, need[c] - give[c]) * k; give[c] += g; left -= g; }
            }
            double after = 0;
            foreach (var c in keys) { dict[c] = (float)give[c]; after += give[c]; }
            _zLadderN++;
            _zLadCut += Math.Max(0.0, tot - after);                                                     // jedzenie koszyka niekupione z braku pieniedzy (zl)
            _zTkLadder += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            return after;
        }

        /// <summary>TownFolk.Anchor (miasto): koszt koszyka klas W/R/P (zl/d przy cenach targu, pelna lambda) i lambda; false - poza ZC4.</summary>
        internal static bool Zc4ClassCost(Settlement st, double[] cls, out double lam)
        {
            lam = 0;
            if (st == null || st.Town == null) return false;
            ZTown zt;
            if (!ZTownOf(st.Town, out zt)) return false;
            for (int c = 0; c < 3; c++) cls[c] = zt.Cls[c];
            lam = zt.Lam;
            return true;
        }

        /// <summary>TownFolk.AddOffer / LadderOffer: czesc oferty nadwyzki ludu do kategorii c - kategorie koszyka x (1 - lambda) (koszyk stanow juz ma lepszy stol bogatych;
        /// dodatki - zbytki - bez zmian); poza ZC4 - 1.</summary>
        internal static double Zc4OfferFactor(Town town, ItemCategory c)
        {
            ZTown zt;
            if (c == null || !ZTownOf(town, out zt)) return 1.0;
            int[] zs;
            if (!_zCats.TryGetValue(c, out zs)) return 1.0;
            foreach (int z in zs) if (ZAdd(z)) return 1.0;
            return Math.Max(0.0, 1.0 - zt.Lam);
        }

        /// <summary>TownFolk.FirewoodDaily: potrzeba opalu miasta (ladunki/d) = (1 - lambda) x dzis + lambda x 1.5 kg na osobe (tabela KS) x czynnik zimy (ten sam, co dzis:
        /// 1 + kraina x polnoc; recenzja 16.10 - bez niego zima nie dokladala opalu przy lambda 1); poza ZC4 - dzis.</summary>
        internal static double Zc4Firewood(Town town, double old, double winterK)
        {
            ZTown zt;
            if (!ZTownOf(town, out zt)) return old;
            double k = winterK > 0 && !double.IsNaN(winterK) && !double.IsInfinity(winterK) ? winterK : 1.0;
            return (1 - zt.Lam) * old + zt.Lam * zt.Fire * k;
        }

        /// <summary>TownFolk.Villages: koszyk wsi zastosowanej (sztuki/d przy pelnej lambdzie, ZIds) i lambda jej miasta targowego; null - poza ZC4.</summary>
        internal static float[] Zc4VillageNeed(Village v, out double lam)
        {
            lam = 0;
            if (v == null || _state != StateApplied || !KeyOn) return null;
            var map = _zv; float[] q;
            if (map.Count == 0 || !map.TryGetValue(v, out q) || q == null) return null;
            var mk = v.TradeBound ?? v.Bound;
            ZTown zt;
            if (mk == null || mk.Town == null || !ZTownOf(mk.Town, out zt)) return null;
            lam = zt.Lam;
            return lam > 0 ? q : null;
        }

        /// <summary>TownFolk.Zc4BuyVillage: liczniki zakupow wsi (zl, sztuki, chciane / kupione sztuki wedlug szczebla: 0 zboze i sol, 1 reszta jedzenia, 2 rzeczy).</summary>
        internal static void Zc4NoteVillage(long gold, long pcs, double want, double got, int noGold, int noGoods, double[] rw, double[] rg, long brew, long unGold)
        {
            Zc4NoteVillageInner(gold, pcs, want, got, noGold, noGoods, rw, rg, brew, unGold);
        }
        private static double _zvElse;

        /// <summary>TownFolk.Zc4BuyVillage (recenzja 17.10): kcal i wartosc pelnego koszyka chciane (x lambda) i kupione przez wies v w miescie targowym mk (z zakupami
        /// zastepczymi i w miastach obok) - czesc kcal kupiona do jedzenia robionego w domu (racje), sumy rynku do linii ZC4 (wartosc zaplacona z Planu przy cenach polki).</summary>
        internal static void Zc4NoteBought(Village v, Settlement mk, double lam, double kcalWant, double kcalGot, double zlWant, double zlGot)
        {
            if (v == null) return;
            _zvBought[v] = kcalWant > 1e-6 ? (float)Math.Max(0.0, Math.Min(1.0, kcalGot / kcalWant)) : -1f;
            int m = mk != null && mk.Town != null ? TownMarket(mk.Town) : MarketOfSettlement(v.Settlement);
            if (m < 0 || m > 1) return;
            _zvKcalW[m] += kcalWant; _zvKcalG[m] += kcalGot; _zvZlW[m] += zlWant; _zvZlG[m] += zlGot;
            float[] qp; ZTown zt;
            if (_zvp.TryGetValue(v, out qp) && qp != null && mk != null && _zt.TryGetValue(mk, out zt) && zt != null)
                for (int z = 0; z < ZN && z < qp.Length; z++) if (qp[z] > 0f) _zvZlP[m] += qp[z] * lam * Math.Max(1, zt.Px[z]);
        }
        private static void Zc4NoteVillageInner(long gold, long pcs, double want, double got, int noGold, int noGoods, double[] rw, double[] rg, long brew, long unGold)
        {
            _zvGold += gold; _zvPcs += pcs; _zvWant += want; _zvGot += got; _zvN++; _zvNoGold += noGold; _zvNoGoods += noGoods; _zvBrew += brew; _zvUnGold += unGold;
            for (int i = 0; i < 3; i++) { _zvRungWant[i] += rw[i]; _zvRung[i] += rg[i]; }
        }

        // ============================================================ linia doby
        private static readonly int[] ZLineItems = { ZGrain, ZBread, ZPie, ZMeat, ZFish, ZCheese, ZButter, ZEgg, ZApple, ZCarrot, ZOrange, ZGrape, ZOlives, ZDate, ZOil, ZSalt, ZHoney, ZBeer, ZWine, ZMead, ZLinen, ZFelt, ZLeather, ZTools, ZPottery, ZWood };

        /// <summary>Linia "Koszyk stanow (ZC4): dzien N": lambda, potrzeba miast i wsi (sztuki/d przy biezacej lambdzie) wobec zuzycia mieszczan i zakupow wsi z ksiegi towarow,
        /// zakupy wsi (zl, szczeble), drabina, piwo domowe, slownik budzetu (dzis -> koszyk).</summary>
        private static string Zc4Line(int day)
        {
            var map = _zt;
            var sb = new StringBuilder(3000);
            double lw = 0, le = 0; int nw = 0, ne = 0;
            var qt = new double[ZN]; var qv = new double[ZN]; double fire = 0, people = 0, cls = 0;
            foreach (var zt in map.Values)
            {
                if (zt.Market == 0) { lw = zt.Lam; nw++; } else { le = zt.Lam; ne++; }
                for (int z = 0; z < ZN; z++) { qt[z] += zt.Q[z] * zt.Lam; qv[z] += zt.QV[z] * zt.Lam; }
                fire += zt.Fire * zt.Lam; people += zt.People; cls += zt.Cls[0] + zt.Cls[1] + zt.Cls[2];
            }
            sb.Append(PZ).Append("dzien ").Append(day).Append(" | lambda Westeros ").Append(lw.ToString("0.00", Inv)).Append(" (").Append(nw).Append(" miast), Essos ").Append(le.ToString("0.00", Inv)).Append(" (").Append(ne).Append(" miast)")
              .Append(" | ludzie miast ksiegi ").Append(F0(people)).Append(", koszyk miast przy cenach targu ").Append(F0(cls)).Append(" zl/d (pelna lambda)")
              .Append(" | potrzeba szt./d przy lambdzie (miasta + wsie; w nawiasie wczoraj z ksiegi towarow: mieszczanie / zuzycie razem):");
            bool first = true;
            foreach (int z in ZLineItems)
            {
                double a = qt[z] + qv[z] + (z == ZWood ? fire : 0);
                if (a < 0.5) continue;
                string id = ZIds[z];
                sb.Append(first ? " " : ", ").Append(id).Append(' ').Append(F0(qt[z])).Append('+').Append(F0(qv[z])).Append(z == ZWood ? "+opal " + F0(fire) : "")
                  .Append(" (").Append(F0(LastLedger(id, GoodsLedger.KsTown))).Append('/').Append(F0(LastLedger(id, GoodsLedger.KsCons))).Append(')');
                first = false;
            }
            sb.Append(" | wsie: zakupow ").Append(_zvN).Append(" za ").Append(_zvGold).Append(" zl, sztuk ").Append(_zvPcs).Append(" (chciane ").Append(F0(_zvWant)).Append(", kupione ").Append(F0(_zvGot))
              .Append("; zboze i sol ").Append(F0(_zvRung[0])).Append('/').Append(F0(_zvRungWant[0])).Append(", reszta jedzenia ").Append(F0(_zvRung[1])).Append('/').Append(F0(_zvRungWant[1]))
              .Append(", rzeczy ").Append(F0(_zvRung[2])).Append('/').Append(F0(_zvRungWant[2])).Append("), ziarno na piwo domowe ").Append(_zvBrew).Append(" szt., bez kiesy ").Append(_zvNoGold).Append(", bez towaru ").Append(_zvNoGoods)
              .Append(" | miasta: slowniki budzetu ").Append(_zDictN).Append(" (budzet kategorii koszyka dzis ").Append(F0(_zDictOld)).Append(" -> ").Append(F0(_zDictNew)).Append(" zl)")
              .Append(", drabina (kiesy nie stac na cale jedzenie) ").Append(_zLadderN).Append(", piwo domowe w ").Append(_zBrewN).Append(" (ziarna ").Append(F0(_zBrewPcs)).Append(" szt.)")
              .Append(", brak chleba i placka - ziarno w ").Append(_zBreadN).Append(" (").Append(F0(_zBreadPcs)).Append(" szt.)")
              .Append(" | stac na cale jedzenie: miasta ").Append(_zDictN > 0 ? (100.0 * (_zDictN - _zLadderN) / _zDictN).ToString("0.0", Inv) : "-").Append("% (").Append(_zDictN - _zLadderN).Append('/').Append(_zDictN)
              .Append("), jedzenie niekupione z braku pieniedzy: miasta ").Append(_zFoodBud > 0 ? (100.0 * _zLadCut / _zFoodBud).ToString("0.0", Inv) : "-").Append("% budzetu jedzenia (").Append(F0(_zLadCut)).Append(" zl)")
              .Append(", wsie ").Append(_zvWant > 0 ? (100.0 * _zvUnGold / _zvWant).ToString("0.0", Inv) : "-").Append("% chcianych sztuk (").Append(_zvUnGold).Append(" szt.)");
            // krok 8 (pieniadze Planu): chciane = koszyk zaplacony (czesc z Planu); kiesy wsi zastosowanych razem (dryf w oknie odbioru - NKS-13), czesc zaplacona koszyka miast
            double tpf = 0; int tpn = 0;
            foreach (var zt in map.Values) { if (zt.Paid == null) continue; double s0 = 0; foreach (var x in zt.Paid) s0 += x; tpf += s0 / zt.Paid.Length; tpn++; }
            // recenzja 17.10: wsie kupuja PELNY koszyk (kiesa i drabina decyduja) - wartosc chciana / zaplacona z Planu / kupiona, kcal kupione / robione w domu / bez pokrycia,
            // wplyw do kies wsi z utargu (pomiar - NKS-13: budzet Planu wobec pomiaru)
            var hk = new double[2]; var wk = new double[2];
            foreach (var ld in _live.Values) { if (ld == null || ld.TF || ld.St == null) continue; int m = ld.Market == 0 ? 0 : 1; hk[m] += ld.HomeKcal; wk[m] += ld.WantKcal; }
            sb.Append(" | krok 8: wsie kupuja pelny koszyk z kiesy (drabina zakupow)");
            for (int m = 0; m < 2; m++)
            {
                double kb = _zvKcalW[m] > 0 ? _zvKcalG[m] / _zvKcalW[m] : 0.0, kh = wk[m] > 0 ? hk[m] / wk[m] : 0.0;
                sb.Append(m == 0 ? ": Westeros" : "; Essos").Append(" wartosc przy cenach polki chciana ").Append(F0(_zvZlW[m])).Append(" / zaplacona z Planu ").Append(F0(_zvZlP[m])).Append(" / kupiona ").Append(F0(_zvZlG[m]))
                  .Append(" zl, kcal koszyka kupione ").Append((100 * kb).ToString("0.0", Inv)).Append("% / robione w domu ").Append((100 * kh).ToString("0.0", Inv)).Append("% / bez pokrycia ")
                  .Append((100 * Math.Max(0.0, 1.0 - kb - kh)).ToString("0.0", Inv)).Append("%, wplyw do kies wsi (utarg doby) ").Append(F0(_inflowDay[m])).Append(" zl");
            }
            sb.Append(" | kiesy wsi zastosowanych razem ").Append(AppliedVillagePurses()).Append(" zl")
              .Append(", czesc zaplacona koszyka miast ").Append(tpn > 0 ? (tpf / tpn).ToString("0.000", Inv) : "1 (bez Planu)").Append(" (").Append(tpn).Append(" miast)")
              .Append(", zakupy wsi w miescie obok (brak na polce targu) ").Append(F0(_zvElse)).Append(" szt.");
            // wsad przetworow i 1:1 w popycie ceny (sztuki/d przy lambdzie, wszystkie miasta)
            var der = new double[ZN]; var one = new double[ZN]; var dl = new double[3];
            foreach (var zt in map.Values) { for (int z = 0; z < ZN; z++) { der[z] += zt.Der[z] * zt.Lam; one[z] += zt.One[z] * zt.Lam; } for (int k = 0; k < 3; k++) dl[k] += zt.DerLive[k] * zt.Lam; }
            sb.Append(" | popyt ceny ponad koszyk (szt./d przy lambdzie): wsad przetworow");
            bool fd = true;
            foreach (int z in new[] { ZGrain, ZGrape, ZOlives, ZHoney, ZClay }) { if (der[z] < 0.5) continue; sb.Append(fd ? " " : ", ").Append(ZIds[z]).Append(' ').Append(F0(der[z])); fd = false; }
            for (int k = 0; k < 3; k++) { if (dl[k] < 0.5) continue; sb.Append(fd ? " " : ", ").Append(ZLiveIds[k]).Append(' ').Append(F0(dl[k])); fd = false; }
            if (fd) sb.Append(" -");
            sb.Append("; 1:1 (wojsko, zamki, dwory)");
            fd = true;
            foreach (int z in new[] { ZGrain, ZFish, ZMeat, ZCheese, ZBeer, ZWine, ZLinen, ZLeather, ZTools, ZWood, ZClay, ZLime, ZMarble })
            { if (one[z] < 0.5) continue; sb.Append(fd ? " " : ", ").Append(ZIds[z]).Append(' ').Append(F0(one[z])); fd = false; }
            if (fd) sb.Append(" -");
            double tf = System.Diagnostics.Stopwatch.Frequency / 1000.0;
            double msDem = System.Threading.Interlocked.Read(ref _zTkDemand) / tf, msDict = _zTkDict / tf, msVill = _zTkVill / tf, msLad = _zTkLadder / tf, msCraft = _kTicks / tf, msBuild = _zTkBuild / tf;
            sb.Append(" | czas hakow od ostatniej linii: popyt ceny ").Append(msDem.ToString("0.00", Inv)).Append(" ms (").Append(_zDemandN).Append(" wywolan), slowniki budzetu ").Append(msDict.ToString("0.00", Inv))
              .Append(" ms, drabina ").Append(msLad.ToString("0.00", Inv)).Append(" ms, zakupy wsi ").Append(msVill.ToString("0.00", Inv)).Append(" ms, przetwory ZC5 ").Append(msCraft.ToString("0.00", Inv))
              .Append(" ms, razem ").Append((msDem + msDict + msVill + msLad + msCraft).ToString("0.00", Inv)).Append(" ms")
              .Append(" (w dobie KS: mapa koszyka ").Append(msBuild.ToString("0.00", Inv)).Append(" ms - recenzja 17.10: pomiar kosztu wierszy polek)");
            _kTicks = 0;
            sb.Append(" | latki: ").Append(_zHook).Append(" | potkniecia ").Append(_zStum).Append('.');
            Zc4Zero(); _zStum = 0;
            return sb.ToString();
        }
    }
}
