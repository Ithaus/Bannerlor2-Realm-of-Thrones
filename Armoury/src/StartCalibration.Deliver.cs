using System;

namespace Armoury
{
    /// <summary>KS: stan czesci okregu (bloku) do rachunku produkcji - zajecia i udzial rak, regale (udzial rak bloku przy zlozu), renta ziemi z Planu, ziemia bloku.</summary>
    internal class KsPart
    {
        public byte[] C; public float[] X;                 // zajecia bloku i udzial rak (suma 1)
        public byte[] RC; public float[] RF;               // regale: zajecie i udzial rak bloku przy zlozu
        public float Rent;                                 // renta ziemi z Planu (d/ha/d)
        public KsBlock K;                                  // ziemia, udzial w okregu, zajecie startowe (pamiec sesji)
        public double Hands, HomeFrac;                     // rece bloku i czesc rak domu (ostatni rachunek)
        // pamiec przydatnosci ziemi bloku (ziemia sie nie zmienia): L zajecia c (NaN - jeszcze nie liczone), L zboza; klucz - blok ziemi K (nowy K = od nowa)
        internal double[] LC; internal double LG = double.NaN; internal object LKey;

        /// <summary>Przydatnosc ziemi zajecia c w tym bloku (LandEff zajec pola; regale - land2, a w bloku wsi gry przy typie wsi LandEff) - z pamieci bloku.</summary>
        internal double L(int c, string type)
        {
            var kb = K;
            if (!ReferenceEquals(LKey, kb)) { LKey = kb; LC = null; LG = double.NaN; }
            if (LC == null) { LC = new double[KsTab.NC]; for (int i = 0; i < LC.Length; i++) LC[i] = double.NaN; }
            double v = LC[c];
            if (double.IsNaN(v))
            {
                v = KsTab.IsRegale[c] ? ((kb.Main && KsTab.Crafts[c] == type) ? KsCore.LandEff(kb, c, type) : KsTab.Land2(c, kb.O, type)) : KsCore.LandEff(kb, c, type);
                LC[c] = v;
            }
            return v;
        }

        /// <summary>Przydatnosc ziemi zboza (land2 wheat_farm) - z pamieci bloku.</summary>
        internal double LGrain(string type)
        {
            var kb = K;
            if (!ReferenceEquals(LKey, kb)) { LKey = kb; LC = null; LG = double.NaN; }
            if (double.IsNaN(LG)) LG = KsTab.Land2(KsTab.CWheat, kb.O, type);
            return LG;
        }
    }

    /// <summary>
    /// KS - TOWARY DOSTAWY I RACHUNEK OKREGU (czysty C#; projekt rozdz. 11.2): produkcja okregu w prawdziwych kg na towar gry (zywiec jako zwierzeta - mieso i skory
    /// robia miasta; dziczyzna mysliwych jako mieso; konie poza KS), to, co okreg zje sam (potrzeby stanow x udzial z wlasnego pola ZC; mieso - najpierw dziczyzna
    /// i wieloryb, potem uboj wlasnego zywca), kcal czesci domowej do spichlerza. Ten sam rachunek w grze (stan zastosowany) i w linii Planu.
    /// </summary>
    internal static class KsDeliver
    {
        internal static readonly string[] ItemIds =
        {
            "grain", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "salt", "honey", "spice", "wool", "flax", "cotton",
            "hardwood", "clay", "limestone", "marble", "iron", "silver", "gold_ore", "fur", "whale_oil", "walrus_tusk", "cow", "hog", "sheep", "WhaleMeat", "meat", "hides"
        };
        internal const int ItemCount = 33;
        internal const int IGrain = 0, IFish = 1, ICheese = 2, IButter = 3, IEgg = 4, IApple = 5, ICarrot = 6, IOrange = 7, IGrape = 8, IOlives = 9, IDate = 10, ISalt = 11,
                           IHoney = 12, ISpice = 13, IWool = 14, IFlax = 15, ICotton = 16, IWood = 17, IClay = 18, ILime = 19, IMarble = 20, IIron = 21, ISilver = 22, IGold = 23,
                           IFur = 24, IWhaleOil = 25, ITusk = 26, ICow = 27, IHog = 28, ISheep = 29, IWhaleMeat = 30, IMeat = 31, IHides = 32;
        internal static readonly int[] GoodToItem = BuildG2I();
        private static int[] BuildG2I()
        {
            var a = new int[KsTab.NG];
            for (int g = 0; g < KsTab.NG; g++) a[g] = Array.IndexOf(ItemIds, KsTab.Goods[g]);
            a[KsTab.GMeat] = -1; a[KsTab.GHides] = -1; a[KsTab.GHorse] = -1;    // mieso i skory z uboju zywca na targu robia miasta (rzemieslnicy BK); konie - lancuch gry
            // (skory z uboju zywca zjedzonego w okregu - Own: na targ)
            return a;
        }
        internal static readonly double[] ItemKcal = BuildKcal();
        private static double[] BuildKcal()
        {
            var k = new double[ItemCount];
            k[IGrain] = 3300; k[IFish] = 1100; k[ICheese] = 3600; k[IButter] = 7200; k[IEgg] = 1450; k[IApple] = 500; k[ICarrot] = 400; k[IOrange] = 450; k[IGrape] = 700;
            k[IOlives] = 1150; k[IDate] = 2800; k[IHoney] = 3000; k[IWhaleMeat] = 2200; k[IMeat] = 2200;
            k[ICow] = 2200 * KsTab.MeatOfLive; k[IHog] = 2200 * KsTab.MeatOfLive; k[ISheep] = 2200 * KsTab.MeatOfLive;
            return k;
        }

        /// <summary>Produkcja okregu (kg/d na towar dostawy) z czesci (bloki): dom (jaja, miod), zboze wlasne, zajecia (P0 x L), regale; bez zimy i czynnikow rak.</summary>
        internal static void Produce(KsDistrict d, KsPart[] parts, double[] prod)
        {
            Array.Clear(prod, 0, prod.Length);
            string type = d.Type;
            for (int i = 0; i < parts.Length; i++)
            {
                var lb = parts[i];
                if (lb == null || lb.K == null || lb.C == null) continue;
                var kb = lb.K;
                double W, wH, lg, lh;
                // KsCore.HomeLabour z przydatnoscia zboza z pamieci bloku (ten sam wzor)
                W = d.Wf * kb.Share; lg = lb.LGrain(type); lh = 0.5 + 0.5 * Math.Min(1.0, lg);
                wH = Math.Min(W, d.Need0[KsTab.NH] * kb.Share / (KsTab.PH * lh)); if (!(wH >= 0)) wH = 0;
                lb.Hands = W; lb.HomeFrac = W > 0 ? wH / W : 0;
                double rsum = 0; if (lb.RF != null) for (int j = 0; j < lb.RF.Length; j++) rsum += lb.RF[j];
                double scale = Math.Max(0.0, 1.0 - rsum);
                double rem = W - wH;
                for (int j = 0; j < lb.C.Length; j++)
                {
                    int c = lb.C[j]; double x = lb.X[j] * scale;
                    if (!(x > 0)) continue;
                    prod[IEgg] += x * wH * KsTab.PHEgg * lh; prod[IHoney] += x * wH * KsTab.PHHoney * lh;
                    double sub = 0;
                    if (c != KsTab.CWheat && lg > 0)
                    {
                        sub = Math.Min(rem, KsTab.OwnGrainShare(lg) * KsCore.GrainOwnNeed(d.Need0) * kb.Share / (KsTab.PGrainSub * lg));
                        prod[IGrain] += x * sub * KsTab.PGrainSub * lg;
                    }
                    double cl = rem - sub;
                    double L = lb.L(c, type);
                    foreach (var kv in KsTab.P0[c])
                    {
                        int it = kv.Key == KsTab.GWhaleMeat && c == KsTab.CHunter ? IMeat : GoodToItem[kv.Key];
                        if (it >= 0) prod[it] += x * cl * kv.Value * L;
                    }
                }
                if (lb.RC != null)
                    for (int j = 0; j < lb.RC.Length; j++)
                    {
                        int c = lb.RC[j]; double lab = lb.RF[j] * W;
                        double L = lb.L(c, type);
                        foreach (var kv in KsTab.P0[c]) { int it = GoodToItem[kv.Key]; if (it >= 0) prod[it] += lab * kv.Value * L; }
                    }
            }
        }

        /// <summary>To, co okreg zje sam (kg/d na towar), z potrzeb domowych (NeedsOfKeep x lepszy stol): zboze z piwem domowym, ryby, nabial, owoce, winogrona na wino,
        /// oliwki na oliwe, miod na miod pitny, len i welna przedzione w domu; mieso - dziczyzna i wieloryb, potem uboj zywca (mieso = polowa zywca; skory tego uboju
        /// dopisane do produkcji - na targ). Zwraca kcal zjedzone z wlasnego pola i domu (z warstwa dom).</summary>
        internal static double Own(double[] prod, double[] n, double[] own)
        {
            Array.Clear(own, 0, own.Length);
            var want = new double[ItemCount];
            want[IGrain] = n[KsTab.NG_] + n[KsTab.NBeer] * KsTab.BeerGrain;
            want[IFish] = n[KsTab.NFish]; want[ICheese] = n[KsTab.NCheese]; want[IButter] = n[KsTab.NButter]; want[IEgg] = n[KsTab.NEgg];
            want[IApple] = n[KsTab.NApple]; want[ICarrot] = n[KsTab.NCarrot]; want[IOrange] = n[KsTab.NOrange]; want[IDate] = n[KsTab.NDate];
            want[IGrape] = n[KsTab.NGrape] + n[KsTab.NWine] * KsTab.WineGrape;
            want[IOlives] = n[KsTab.NOlives] + n[KsTab.NOil] * KsTab.OilOlive;
            want[IHoney] = n[KsTab.NHoney] + n[KsTab.NMead] * KsTab.MeadHoney;
            want[IFlax] = n[KsTab.NPlotno] * KsTab.LinenFlax; want[IWool] = n[KsTab.NSukno] * KsTab.FeltWool;
            for (int i = 0; i < ItemCount; i++) if (want[i] > 0) own[i] = Math.Min(prod[i], want[i]);
            double meat = n[KsTab.NMeat];
            foreach (int it in new[] { IMeat, IWhaleMeat }) { double t = Math.Min(prod[it], meat); own[it] = t; meat -= t; }
            double live = prod[ICow] + prod[IHog] + prod[ISheep];
            if (meat > 0 && live > 0)
            {
                double need = Math.Min(live, meat / KsTab.MeatOfLive), k = need / live;
                own[ICow] = prod[ICow] * k; own[IHog] = prod[IHog] * k; own[ISheep] = prod[ISheep] * k;
                // skory zywca ubitego na wlasny stol ida na targ (do garbarzy miasta) - jak skory z uboju w modelu
                prod[IHides] += own[ICow] * KsTab.HideOfLive(KsTab.GCow) + own[IHog] * KsTab.HideOfLive(KsTab.GHog) + own[ISheep] * KsTab.HideOfLive(KsTab.GSheep);
            }
            double kc = n[KsTab.NH] * 1200.0;
            for (int i = 0; i < ItemCount; i++) kc += own[i] * ItemKcal[i];
            return kc;
        }

        /// <summary>Pelna potrzeba kcal (caly koszyk, takze to, co z targu) z potrzeb osady.</summary>
        internal static double KcalNeed(double[] a)
        {
            return a[KsTab.NG_] * 3300 + a[KsTab.NH] * 1200 + a[KsTab.NMeat] * 2200 + a[KsTab.NFish] * 1100 + (a[KsTab.NCheese] + a[KsTab.NButter] + a[KsTab.NEgg]) * 2000
                   + (a[KsTab.NApple] + a[KsTab.NCarrot] + a[KsTab.NOrange] + a[KsTab.NGrape] + a[KsTab.NOlives] + a[KsTab.NDate]) * 450 + a[KsTab.NOil] * 8800
                   + a[KsTab.NHoney] * 3000 + a[KsTab.NBeer] * 400 + a[KsTab.NWine] * 750 + a[KsTab.NMead] * 1000;
        }

        /// <summary>Czesc z Planu: zajecia z logitu (>= 0.2% rak, do 1), regale (udzial rak bloku), renta - dla bloku rachunku b.</summary>
        internal static void FromPlan(KsResult R, int b, KsPart lb)
        {
            int a = R.BlockStart[b], e = R.BlockStart[b + 1];
            var cs = new System.Collections.Generic.List<byte>(); var xs = new System.Collections.Generic.List<float>(); double tot = 0;
            for (int k = a; k < e; k++) if (R.X[k] >= 0.002) { cs.Add((byte)R.PairCraft[k]); xs.Add((float)R.X[k]); tot += R.X[k]; }
            if (cs.Count == 0) { int best = a; for (int k = a; k < e; k++) if (R.X[k] > R.X[best]) best = k; cs.Add((byte)R.PairCraft[best]); xs.Add(1f); tot = 1; }
            for (int i = 0; i < xs.Count; i++) xs[i] = (float)(xs[i] / tot);
            lb.C = cs.ToArray(); lb.X = xs.ToArray();
            double W = R.BlockW[b];
            var rc = new System.Collections.Generic.List<byte>(); var rf = new System.Collections.Generic.List<float>();
            if (W > 0) foreach (int c in KsTab.Regale) { double hnd = R.RegHands[b * KsTab.NC + c]; if (hnd > 0) { rc.Add((byte)c); rf.Add((float)(hnd / W)); } }
            lb.RC = rc.Count > 0 ? rc.ToArray() : null; lb.RF = rf.Count > 0 ? rf.ToArray() : null;
            lb.Rent = (float)R.Rent[b];
        }
    }

    /// <summary>
    /// KS etap 2 - KOSZYK TARGU W KG (czysty C#, ZC4; ten sam w grze i w tescie portu): to, co lud kupuje na targu, wedlug towaru gry (Ids), z potrzeb osady (NeedsOf):
    ///  - miasto (TownKg): cala potrzeba z targu; zboze - ziarno 45%, chleb 45%, placek 10% (rozdz. 8.1 ZC); mieso / mieso wieloryba wedlug udzialu wieloryba w podazy
    ///    rynku; oliwa z oliwa lampowa, tran; opal (1.5 kg na osobe) osobno;
    ///  - wies (VillageKg): pelna potrzeba ludzi w domu minus zjedzone z wlasnego pola i domu (KsDeliver.Own): ziarno z czescia na piwo domowe, reszta piwa jako piwo;
    ///    winogrona i oliwki na wino i oliwe domowe, reszta jako wino i oliwa; miod i miod pitny; len i welna na przedze domowa, reszta plotna i sukna; mieso - dziczyzna,
    ///    wieloryb i polowa wlasnego zywca; sol, przyprawy, skora, narzedzia, garnki, swiatlo, futra - cale z targu (jak popyt Planu).
    /// Suma: podaz KS na targ (produkcja - zjedzone) = koszyk miast + koszyk wsi + wsady przetworow (te same proporcje) + 1:1 - tozsamosc bilansu Planu.
    /// </summary>
    internal static class KsBasket
    {
        internal static readonly string[] Ids =
        {
            "grain", "bread", "pie", "meat", "WhaleMeat", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "oil", "salt", "honey",
            "spice", "beer", "wine", "mead", "linen", "felt", "leather", "tools", "pottery", "whale_oil", "hardwood", "fur", "flax", "wool", "hides", "iron", "clay", "limestone", "marble"
        };
        internal const int N = 37;
        internal const int Grain = 0, Bread = 1, Pie = 2, Meat = 3, Whale = 4, Fish = 5, Cheese = 6, Butter = 7, Egg = 8, Apple = 9, Carrot = 10, Orange = 11, Grape = 12,
                           Olives = 13, Date = 14, Oil = 15, Salt = 16, Honey = 17, Spice = 18, Beer = 19, Wine = 20, Mead = 21, Linen = 22, Felt = 23, Leather = 24, Tools = 25,
                           Pottery = 26, WhaleOil = 27, Wood = 28, Fur = 29, Flax = 30, Wool = 31, Hides = 32, Iron = 33, Clay = 34, Lime = 35, Marble = 36;

        private static void Put(double[] q, int z, double kg) { if (kg > 1e-9) q[z] += kg; }

        /// <summary>Koszyk miasta w kg/d (dodaje do q); opal w kg/d osobno.</summary>
        internal static void TownKg(double[] n, double whaleShare, double[] q, out double fireKg)
        {
            Put(q, Grain, 0.45 * n[KsTab.NG_]); Put(q, Bread, 0.45 * n[KsTab.NG_]); Put(q, Pie, 0.10 * n[KsTab.NG_]);
            Put(q, Meat, n[KsTab.NMeat] * (1 - whaleShare)); Put(q, Whale, n[KsTab.NMeat] * whaleShare);
            Put(q, Fish, n[KsTab.NFish]); Put(q, Cheese, n[KsTab.NCheese]); Put(q, Butter, n[KsTab.NButter]); Put(q, Egg, n[KsTab.NEgg]);
            Put(q, Apple, n[KsTab.NApple]); Put(q, Carrot, n[KsTab.NCarrot]); Put(q, Orange, n[KsTab.NOrange]); Put(q, Grape, n[KsTab.NGrape]); Put(q, Olives, n[KsTab.NOlives]); Put(q, Date, n[KsTab.NDate]);
            Put(q, Oil, n[KsTab.NOil] + n[KsTab.NSwOlej]); Put(q, Salt, n[KsTab.NSalt]); Put(q, Honey, n[KsTab.NHoney]); Put(q, Spice, n[KsTab.NSpice]);
            Put(q, Beer, n[KsTab.NBeer]); Put(q, Wine, n[KsTab.NWine]); Put(q, Mead, n[KsTab.NMead]);
            Put(q, Linen, n[KsTab.NPlotno]); Put(q, Felt, n[KsTab.NSukno]); Put(q, Leather, n[KsTab.NSkora]); Put(q, Tools, n[KsTab.NNarz]); Put(q, Pottery, n[KsTab.NGarnki]);
            Put(q, WhaleOil, n[KsTab.NSwTran]); Put(q, Fur, n[KsTab.NFutro]);
            fireKg = Math.Max(0.0, n[KsTab.NDrewno]);
        }

        /// <summary>Koszyk wsi w kg/d (dodaje do q): N - pelna potrzeba ludzi w domu (x lepszy stol), K - czesc z wlasnego pola (NeedsOfKeep x lepszy stol), own - zjedzone
        /// z wlasnego (KsDeliver.Own, indeksy KsDeliver).</summary>
        internal static void VillageKg(double[] N, double[] K, double[] own, double[] q)
        {
            Put(q, Grain, N[KsTab.NG_] + K[KsTab.NBeer] * KsTab.BeerGrain - own[KsDeliver.IGrain]);
            Put(q, Beer, N[KsTab.NBeer] - K[KsTab.NBeer]);
            Put(q, Grape, N[KsTab.NGrape] + K[KsTab.NWine] * KsTab.WineGrape - own[KsDeliver.IGrape]);
            Put(q, Wine, N[KsTab.NWine] - K[KsTab.NWine]);
            Put(q, Olives, N[KsTab.NOlives] + K[KsTab.NOil] * KsTab.OilOlive - own[KsDeliver.IOlives]);
            Put(q, Oil, N[KsTab.NOil] - K[KsTab.NOil] + N[KsTab.NSwOlej]);
            Put(q, Honey, N[KsTab.NHoney] + K[KsTab.NMead] * KsTab.MeadHoney - own[KsDeliver.IHoney]);
            Put(q, Mead, N[KsTab.NMead] - K[KsTab.NMead]);
            Put(q, Flax, K[KsTab.NPlotno] * KsTab.LinenFlax - own[KsDeliver.IFlax]);
            Put(q, Linen, N[KsTab.NPlotno] - K[KsTab.NPlotno]);
            Put(q, Wool, K[KsTab.NSukno] * KsTab.FeltWool - own[KsDeliver.IWool]);
            Put(q, Felt, N[KsTab.NSukno] - K[KsTab.NSukno]);
            Put(q, Fish, N[KsTab.NFish] - own[KsDeliver.IFish]);
            Put(q, Cheese, N[KsTab.NCheese] - own[KsDeliver.ICheese]);
            Put(q, Butter, N[KsTab.NButter] - own[KsDeliver.IButter]);
            Put(q, Egg, N[KsTab.NEgg] - own[KsDeliver.IEgg]);
            Put(q, Apple, N[KsTab.NApple] - own[KsDeliver.IApple]);
            Put(q, Carrot, N[KsTab.NCarrot] - own[KsDeliver.ICarrot]);
            Put(q, Orange, N[KsTab.NOrange] - own[KsDeliver.IOrange]);
            Put(q, Date, N[KsTab.NDate] - own[KsDeliver.IDate]);
            double meatOwn = own[KsDeliver.IMeat] + own[KsDeliver.IWhaleMeat] + (own[KsDeliver.ICow] + own[KsDeliver.IHog] + own[KsDeliver.ISheep]) * KsTab.MeatOfLive;
            Put(q, Meat, N[KsTab.NMeat] - meatOwn);
            Put(q, Salt, N[KsTab.NSalt]); Put(q, Spice, N[KsTab.NSpice]);
            Put(q, Leather, N[KsTab.NSkora]); Put(q, Tools, N[KsTab.NNarz]); Put(q, Pottery, N[KsTab.NGarnki]);
            Put(q, WhaleOil, N[KsTab.NSwTran]); Put(q, Fur, N[KsTab.NFutro]);
        }
    }
}
