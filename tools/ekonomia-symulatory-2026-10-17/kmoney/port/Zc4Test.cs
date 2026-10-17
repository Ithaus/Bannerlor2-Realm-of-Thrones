using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Armoury;

// Test 4 (KS etap 2): tozsamosc bilansu targu - podaz KS na targ (produkcja - zjedzone w okregu) wobec koszyka targu ZC4 (KsBasket: miasta + wsie) + wsady przetworow
// (te same proporcje KS) + 1:1 + zbytki; obie strony + zjedzone w okregach maja dac podaz i popyt Planu (R.S, R.D).
static class Zc4Test
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Run(KsInput inp, KsResult R, JsonElement root, string dir)
    {
        var bjp = Path.Combine(dir, "ks_bask.json");
        if (!File.Exists(bjp)) { Console.WriteLine("ZC4: brak ks_bask.json"); return; }
        var bj = JsonDocument.Parse(File.ReadAllText(bjp)).RootElement;
        double sigma = inp.Sigma;
        double[] mkPeas = { 0.05, 0, 0.20, 0.25, 0.10, 0.10, 0.50, 1.0, 0.20, 1.0, 0.10, 0.10, 0.30 };
        double[] mkGent = { 0.30, 0, 0.40, 0.80, 0.30, 0.30, 1.00, 1.0, 0.50, 1.0, 0.30, 0.70, 0.50 };
        var keep = new double[6][];
        for (int e = 0; e < 6; e++)
        {
            keep[e] = new double[13];
            for (int g = 0; g < 13; g++)
            {
                double mk = g == 1 ? 0 : (e == 1 || e == 3 || e == 4) ? mkPeas[g] : e == 5 ? mkGent[g] : e == 0 ? (g == 7 ? 1.0 : 0.05) : 1.0;
                keep[e][g] = 1.0 - mk;
            }
        }
        string[] est = { "niewolni", "chlopi", "wyrobnicy", "rzemieslnicy", "kupcy", "szlachta" };
        var dj = new Dictionary<string, JsonElement>();
        foreach (var d in root.GetProperty("dist").EnumerateArray()) dj[d.GetProperty("id").GetString()] = d;
        int NI = KsDeliver.ItemCount, NZ = KsBasket.N;
        var supM = new double[2, NI]; var ownM = new double[2, NI]; var bask = new double[2, NZ]; var fire = new double[2]; var ppl = new double[2];
        var prod = new double[NI]; var own = new double[NI]; var vq = new double[NZ];
        Func<JsonElement, KsCore.Basket[]> basketOf = cb =>
        {
            var b = new KsCore.Basket[6];
            for (int e = 0; e < 6; e++)
            {
                var be = cb.GetProperty("b")[e]; var x = new KsCore.Basket { DatesExtra = be.GetProperty("de").GetDouble() };
                for (int g = 0; g < 13; g++) x.G[g] = be.GetProperty("g")[g].GetDouble();
                for (int i = 0; i < 6; i++) x.Vs[i] = be.GetProperty("vs")[i].GetDouble();
                b[e] = x;
            }
            return b;
        };
        foreach (var d in inp.Districts)
        {
            int m = d.Market;
            var kparts = new KsPart[d.Blocks.Count];
            for (int i = 0; i < kparts.Length; i++) { kparts[i] = new KsPart { K = d.Blocks[i] }; KsDeliver.FromPlan(R, d.Blocks[i].Index, kparts[i]); }
            KsDeliver.Produce(d, kparts, prod);
            if (d.TownFields) { for (int i = 0; i < NI; i++) supM[m, i] += prod[i]; continue; }
            var de = dj[d.Id];
            string cul = de.GetProperty("cul").GetString();
            if (!bj.TryGetProperty(cul, out var cb)) cul = de.GetProperty("land").GetString();
            if (!bj.TryGetProperty(cul, out cb)) { Console.WriteLine("ZC4: bez koszyka " + d.Id); continue; }
            var bk = basketOf(cb);
            var pp = new double[6];
            foreach (var kv in de.GetProperty("ppl").EnumerateObject()) { int e = Array.IndexOf(est, kv.Name); if (e >= 0) pp[e] = kv.Value.GetDouble(); }
            var on = new double[KsTab.NN];
            KsCore.NeedsOfKeep(pp, bk, keep, cb.GetProperty("oil").GetBoolean(), d.People, on);
            double[] dn; R.DistNeed.TryGetValue(d.Id, out dn);
            var full = new double[KsTab.NN];
            for (int k = 0; k < KsTab.NN; k++)
            {
                double mult = dn != null && d.Need0[k] > 1e-9 ? Math.Max(0.25, Math.Min(4.0, dn[k] / d.Need0[k])) : 1.0;
                on[k] *= mult; full[k] = d.Need0[k] * mult;
            }
            KsDeliver.Own(prod, on, own);
            for (int i = 0; i < NI; i++) { supM[m, i] += prod[i] - own[i]; ownM[m, i] += own[i]; }
            Array.Clear(vq, 0, NZ);
            KsBasket.VillageKg(full, on, own, vq);
            for (int z = 0; z < NZ; z++) bask[m, z] += vq[z];
            ppl[m] += d.People;
        }
        // udzial wieloryba w miesie rynku (kg): jak w grze (Zc4Build)
        var whale = new double[2];
        for (int m = 0; m < 2; m++)
        {
            double wk = supM[m, KsDeliver.IWhaleMeat], mk = supM[m, KsDeliver.IMeat] + (supM[m, KsDeliver.ICow] + supM[m, KsDeliver.IHog] + supM[m, KsDeliver.ISheep]) * KsTab.MeatOfLive;
            whale[m] = wk + mk > 0 ? wk / (wk + mk) : 0;
        }
        foreach (var t in inp.Towns)
        {
            int m = t.Market;
            var q = new double[NZ]; double fk;
            KsBasket.TownKg(t.Need0, whale[m], q, out fk);
            for (int z = 0; z < NZ; z++) bask[m, z] += q[z];
            fire[m] += fk; ppl[m] += t.People;
        }
        // popyt targu w surowcach KS (kg): koszyk -> surowiec (wsady przetworow), 1:1 i zbytki podzielone wedlug ludzi rynku
        double pw = ppl[0] + ppl[1];
        var dem = new double[2, KsTab.NG];
        Func<string, double> one = id =>
        {
            if (!inp.Items.TryGetValue(id, out var it) || it == null) return 0.0;
            double pcs = it.Army + it.Cast + it.Court;
            if (id == "hardwood" || id == "clay" || id == "limestone" || id == "marble" || id == "tools" || id == "planks") pcs += it.Bud;
            if (id == "iron" || id == "hardwood" || id == "silver") pcs += it.Ws;
            return pcs * sigma * it.Kg;
        };
        for (int m = 0; m < 2; m++)
        {
            double sh = ppl[m] / pw;
            Func<int, double> b = z => bask[m, z];
            Func<string, double> o = id => one(id) * sh;
            void A(int g, double v) { dem[m, g] += v; }
            A(KsTab.GGrain, b(KsBasket.Grain) + b(KsBasket.Bread) + b(KsBasket.Pie) + (b(KsBasket.Beer) + o("beer")) * KsTab.BeerGrain + o("grain") + o("bread") + o("pie"));
            A(KsTab.GFish, b(KsBasket.Fish) + o("fish")); A(KsTab.GCheese, b(KsBasket.Cheese) + o("cheese")); A(KsTab.GButter, b(KsBasket.Butter) + o("butter")); A(KsTab.GEgg, b(KsBasket.Egg) + o("Egg"));
            A(KsTab.GSalt, b(KsBasket.Salt) + o("salt")); A(KsTab.GHoney, b(KsBasket.Honey) + o("honey") + (b(KsBasket.Mead) + o("mead")) * KsTab.MeadHoney);
            A(KsTab.GMeat, b(KsBasket.Meat) + b(KsBasket.Whale) + o("meat") + o("WhaleMeat"));
            A(KsTab.GApple, b(KsBasket.Apple) + o("apple")); A(KsTab.GCarrot, b(KsBasket.Carrot) + o("carrot")); A(KsTab.GOrange, b(KsBasket.Orange) + o("orange"));
            A(KsTab.GGrape, b(KsBasket.Grape) + o("grape") + (b(KsBasket.Wine) + o("wine")) * KsTab.WineGrape);
            A(KsTab.GOlives, b(KsBasket.Olives) + o("olives") + (b(KsBasket.Oil) + o("oil")) * KsTab.OilOlive);
            A(KsTab.GDate, b(KsBasket.Date) + o("date_fruit"));
            A(KsTab.GSpice, b(KsBasket.Spice));
            A(KsTab.GFlax, b(KsBasket.Flax) + (b(KsBasket.Linen) + o("linen")) * KsTab.LinenFlax);
            A(KsTab.GWool, b(KsBasket.Wool) + (b(KsBasket.Felt) + o("felt")) * KsTab.FeltWool);
            A(KsTab.GHides, (b(KsBasket.Leather) + o("leather")) * KsTab.LeatherHide);
            A(KsTab.GWood, fire[m] + o("hardwood"));
            A(KsTab.GClay, (b(KsBasket.Pottery) + o("pottery")) * KsTab.PotClay + o("clay"));
            A(KsTab.GLime, o("limestone")); A(KsTab.GMarble, o("marble"));
            A(KsTab.GIron, (b(KsBasket.Tools) + o("tools")) * KsTab.ToolsIron + o("iron"));
            A(KsTab.GWhaleOil, b(KsBasket.WhaleOil) + o("whale_oil"));
            A(KsTab.GFur, b(KsBasket.Fur));
            foreach (var id in new[] { "fur", "cotton", "spice", "walrus_tusk", "gold_ore" })
            {
                if (!inp.Items.TryGetValue(id, out var it) || it == null) continue;
                double cons = it.Cons - (id == "cotton" ? it.Town : 0.0);
                A(KsTab.GoodIndex[id], Math.Max(0.0, cons) * sigma * it.Kg * sh);
            }
            if (inp.Items.TryGetValue("velvet", out var vel) && vel != null) A(KsTab.GCotton, vel.Cons * sigma * vel.Kg * KsTab.VelvetSilk * sh);
            if (inp.Items.TryGetValue("jewelry", out var jew) && jew != null) A(KsTab.GSilver, jew.Cons * sigma * jew.Kg * KsTab.JewelSilver / 100.0 * sh);
            A(KsTab.GSilver, o("silver"));
        }
        // garum (krainy garum - wedlug rynku krainy w Planie: Essos)
        // (popyt Planu dodaje Garum x 2 ryb i x 0.4 soli - koszyk miast nie ma garum; przetwory ZC5 robia je z ryb i soli tylko w tych krainach)
        dem[1, KsTab.GFish] += KsTab.Garum * 2.0; dem[1, KsTab.GSalt] += KsTab.Garum * 0.4;
        Console.WriteLine("TEST ZC4 (tozsamosc targu, kg/d): towar | rynek | podaz targu KS | popyt targu ZC4 | targ podaz/popyt | Plan S/D | (podaz targu + zjedzone) / S Planu | (popyt targu + zjedzone) / D Planu");
        double worstS = 0, worstD = 0; string ws = "", wd = "";
        for (int m = 0; m < 2; m++)
            for (int g = 0; g < KsTab.NG; g++)
            {
                double D = R.D[m * KsTab.NG + g], S = R.S[m * KsTab.NG + g];
                if (D < 1000 || g == KsTab.GHorse) continue;
                double sup = 0, ow = 0;
                string id = KsTab.Goods[g];
                if (g == KsTab.GMeat)
                {
                    sup = supM[m, KsDeliver.IMeat] + supM[m, KsDeliver.IWhaleMeat] + (supM[m, KsDeliver.ICow] + supM[m, KsDeliver.IHog] + supM[m, KsDeliver.ISheep]) * KsTab.MeatOfLive;
                    ow = ownM[m, KsDeliver.IMeat] + ownM[m, KsDeliver.IWhaleMeat] + (ownM[m, KsDeliver.ICow] + ownM[m, KsDeliver.IHog] + ownM[m, KsDeliver.ISheep]) * KsTab.MeatOfLive;
                }
                else if (g == KsTab.GHides)
                {
                    sup = supM[m, KsDeliver.IHides] + supM[m, KsDeliver.ICow] * KsTab.HideOfLive(KsTab.GCow) + supM[m, KsDeliver.IHog] * KsTab.HideOfLive(KsTab.GHog) + supM[m, KsDeliver.ISheep] * KsTab.HideOfLive(KsTab.GSheep);
                }
                else
                {
                    int i = Array.IndexOf(KsDeliver.ItemIds, id);
                    if (i < 0) continue;
                    sup = supM[m, i]; ow = ownM[m, i];
                }
                double dm = dem[m, g];
                double rs = S > 0 ? (sup + ow) / S : double.NaN, rd = D > 0 ? (dm + ow) / D : double.NaN;
                if (Math.Abs(rs - 1) > worstS && !double.IsNaN(rs)) { worstS = Math.Abs(rs - 1); ws = id + m; }
                if (Math.Abs(rd - 1) > worstD && !double.IsNaN(rd)) { worstD = Math.Abs(rd - 1); wd = id + m; }
                Console.WriteLine(id + " | " + (m == 0 ? "W" : "E") + " | " + (sup / 1000).ToString("0.0", Inv) + " | " + (dm / 1000).ToString("0.0", Inv) + " | " + (dm > 0 ? (sup / dm).ToString("0.000", Inv) : "-")
                                  + " | " + (S / D).ToString("0.000", Inv) + " | " + rs.ToString("0.000", Inv) + " | " + rd.ToString("0.000", Inv));
            }
        Console.WriteLine("ZC4 najwieksza roznica: podaz " + worstS.ToString("0.0000", Inv) + " (" + ws + "), popyt " + worstD.ToString("0.0000", Inv) + " (" + wd + ")");
        // pomiar kosztu doby (jak LiveDaily: Produce + Own + VillageKg dla kazdego okregu) - czesci z pamiecia ziemi, 10 dob
        var parts = new Dictionary<KsDistrict, KsPart[]>();
        foreach (var d in inp.Districts) { var kp = new KsPart[d.Blocks.Count]; for (int i = 0; i < kp.Length; i++) { kp[i] = new KsPart { K = d.Blocks[i] }; KsDeliver.FromPlan(R, d.Blocks[i].Index, kp[i]); } parts[d] = kp; }
        var on2 = new double[KsTab.NN]; var full2 = new double[KsTab.NN];
        var sw = System.Diagnostics.Stopwatch.StartNew(); double first = 0;
        for (int day = 0; day < 10; day++)
        {
            foreach (var d in inp.Districts)
            {
                KsDeliver.Produce(d, parts[d], prod);
                if (d.TownFields) continue;
                Array.Copy(d.Need0, on2, KsTab.NN); for (int k = 0; k < KsTab.NN; k++) on2[k] *= 0.8;
                KsDeliver.Own(prod, on2, own);
                Array.Clear(vq, 0, NZ);
                KsBasket.VillageKg(d.Need0, on2, own, vq);
            }
            if (day == 0) first = sw.Elapsed.TotalMilliseconds;
        }
        Console.WriteLine("KOSZT doby (Produce + Own + koszyk wsi, " + inp.Districts.Count + " okregow): pierwsza " + first.ToString("0.0", Inv) + " ms, dalej srednio " + ((sw.Elapsed.TotalMilliseconds - first) / 9).ToString("0.00", Inv) + " ms");
    }
}
