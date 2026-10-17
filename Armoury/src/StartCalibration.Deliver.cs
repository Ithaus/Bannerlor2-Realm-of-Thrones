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
                KsCore.HomeLabour(d.Wf, kb.Share, d.Need0[KsTab.NH], kb.O, type, out W, out wH, out lg, out lh);
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
                    double L = KsCore.LandEff(kb, c, type);
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
                        double L = (kb.Main && KsTab.Crafts[c] == type) ? KsCore.LandEff(kb, c, type) : KsTab.Land2(c, kb.O, type);
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
}
