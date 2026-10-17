using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Armoury;

// PROTOTYP planu targow (offline): wejscie ks_dump.json + ks_bask.json + catch.json; tryb 0 - rownowaga 2 rynkow (jak dzis) i ocena per targ, tryb 1 - rownowaga targow
// z korytarzami. args: dump catch.json mode(0/1) capLong(-1 bez sufitu, 0 bez kupcow, >0 kg/d na korytarz i towar) rounds out.tsv [warm.tsv]
static class ProgramK
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] Est = { "niewolni", "chlopi", "wyrobnicy", "rzemieslnicy", "kupcy", "szlachta" };
    const double Speed = 41.0;

    static KsCore.Basket[] BasketOf(JsonElement cb)
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
    }

    public static double[][] Keep()
    {
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
        return keep;
    }

    static KsInput Build(string dump, int maxR)
    {
        var root = JsonDocument.Parse(File.ReadAllText(dump)).RootElement;
        var bj = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(dump), "ks_bask.json"))).RootElement;
        bool money = Environment.GetEnvironmentVariable("KS_MONEY") == "1";
        var inp = new KsInput { RoundsFirst = maxR, RoundsR8 = Math.Min(maxR, 4000), RoundsR8b = Math.Min(maxR, 3000), SilkTownDirect = true, SilverTownDirect = true, Money = false, StopAfterFirst = true, Keep = Keep() };
        { var a0 = Environment.GetEnvironmentVariable("KS_ALPHA"); double av = 0.47; if (!string.IsNullOrEmpty(a0)) av = double.Parse(a0, Inv); inp.VilAlpha[0] = inp.VilAlpha[1] = av; }
        { var mg = Environment.GetEnvironmentVariable("KS_MARGIN"); if (!string.IsNullOrEmpty(mg)) inp.MarketMargin = double.Parse(mg, Inv); }
        { var r8 = Environment.GetEnvironmentVariable("KS_R8B"); if (!string.IsNullOrEmpty(r8)) inp.RoundsR8b = int.Parse(r8); }
        Func<JsonElement, JsonElement> cbOf = s =>
        {
            string cul = s.GetProperty("cul").GetString();
            if (!bj.TryGetProperty(cul, out var cb)) bj.TryGetProperty(s.GetProperty("land").GetString(), out cb);
            return cb;
        };
        foreach (var kv in root.GetProperty("gd").EnumerateObject())
        {
            var o = kv.Value;
            Func<string, double> f = k => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;
            inp.Items[kv.Name] = new KsItem { Kg = f("kg"), Val = f("val"), Army = f("army"), Cast = f("cast"), Court = f("court"), Bud = f("bud"), Ws = f("ws"), Cons = f("cons"), Town = f("town") };
        }
        Action<JsonElement, double[]> needs = (o, arr) => { foreach (var kv in o.EnumerateObject()) { int i; if (KsTab.NeedIndex.TryGetValue(kv.Name, out i)) arr[i] = kv.Value.GetDouble(); } };
        foreach (var d in root.GetProperty("dist").EnumerateArray())
        {
            var kd = new KsDistrict
            {
                Id = d.GetProperty("id").GetString(), Type = d.GetProperty("type").GetString(), Land = d.GetProperty("land").GetString(),
                Market = d.GetProperty("mk").GetString() == "Westeros" ? 0 : 1, People = d.GetProperty("people").GetDouble(), Wf = d.GetProperty("wf").GetDouble(),
                Area = d.GetProperty("area").GetDouble()
            };
            kd.TownFields = kd.Type == "town_fields";
            needs(d.GetProperty("need0"), kd.Need0);
            foreach (var b in d.GetProperty("blocks").EnumerateArray())
            {
                var o = b.GetProperty("o");
                var kb = new KsBlock
                {
                    Uid = b.GetProperty("uid").GetString(), Share = b.GetProperty("share").GetDouble(), Area = b.GetProperty("area").GetDouble(),
                    Main = b.GetProperty("main").GetBoolean(), Start = b.GetProperty("start").GetString(),
                    O = new KsTab.Land
                    {
                        Zone = o.GetProperty("zone").GetString(), Terr = o.GetProperty("terr").GetString(), Kind = o.GetProperty("kind").GetString(),
                        Model = o.GetProperty("model").GetString(), DSea = o.GetProperty("d_sea").GetDouble(), DRiver = o.GetProperty("d_river").GetDouble(),
                        DMount = o.GetProperty("d_mount").GetDouble(), Main = o.GetProperty("main").GetBoolean()
                    }
                };
                kb.Block0 = kb.Uid.EndsWith("#main0", StringComparison.Ordinal);
                kd.Blocks.Add(kb);
            }
            if (!kd.TownFields)
            {
                var cb = cbOf(d);
                if (cb.ValueKind == JsonValueKind.Object)
                {
                    kd.Bask = BasketOf(cb); kd.Oil = cb.GetProperty("oil").GetBoolean();
                    kd.PplHome = new double[6];
                    foreach (var kv in d.GetProperty("ppl").EnumerateObject()) { int e = Array.IndexOf(Est, kv.Name); if (e >= 0) kd.PplHome[e] = kv.Value.GetDouble(); }
                }
            }
            inp.Districts.Add(kd);
        }
        foreach (var t in root.GetProperty("towns").EnumerateArray())
        {
            var ks = new KsSettlement { Id = t.GetProperty("id").GetString(), Land = t.GetProperty("land").GetString(), Market = t.GetProperty("mk").GetString() == "Westeros" ? 0 : 1, People = t.GetProperty("people").GetDouble() };
            needs(t.GetProperty("need0"), ks.Need0);
            var cb = cbOf(t);
            if (money && cb.ValueKind == JsonValueKind.Object)
            {
                var bask = BasketOf(cb); bool oil = cb.GetProperty("oil").GetBoolean(), north = cb.GetProperty("north").GetBoolean();
                var ppl = new double[6];
                foreach (var kv in t.GetProperty("ppl").EnumerateObject()) { int e = Array.IndexOf(Est, kv.Name); if (e >= 0) ppl[e] = kv.Value.GetDouble(); }
                ks.ClassNeed = new double[3][]; ks.Budget = new double[3];
                for (int c = 0; c < 3; c++) ks.ClassNeed[c] = new double[KsTab.NN];
                var one = new double[6]; var nn = new double[KsTab.NN];
                for (int e = 0; e < 6; e++)
                {
                    if (!(ppl[e] > 0)) continue;
                    Array.Clear(one, 0, 6); one[e] = ppl[e];
                    KsCore.NeedsOf(one, bask, oil, north, true, false, ppl[e], nn);
                    int c = (e == KsTab.ELabour || e == KsTab.EPeasant) ? 0 : e == KsTab.ECraft ? 1 : (e == KsTab.EMerchant || e == KsTab.EGentry) ? 2 : -1;
                    for (int k = 0; k < KsTab.NN; k++) { if (c >= 0) ks.ClassNeed[c][k] += nn[k]; else { ks.ClassNeed[1][k] += 0.4 * nn[k]; ks.ClassNeed[2][k] += 0.6 * nn[k]; } }
                }
                double[] fac = { 5.0, 5.0, 5.0 };
                for (int c = 0; c < 3; c++)
                {
                    var qq = new double[KsBasket.N]; double fk; KsBasket.TownKg(ks.ClassNeed[c], 0, qq, out fk);
                    double cost = 0;
                    for (int z = 0; z < KsBasket.N; z++) { KsItem it; if (qq[z] > 0 && inp.Items.TryGetValue(KsBasket.Ids[z], out it) && it.Kg > 0) cost += qq[z] * it.Val / (inp.Sigma * it.Kg); }
                    ks.Budget[c] = cost * fac[c];
                }
            }
            inp.Towns.Add(ks);
        }
        string bud = Environment.GetEnvironmentVariable("KS_BUDGETS");
        if (money && !string.IsNullOrEmpty(bud) && File.Exists(bud))
        {
            var tb = new Dictionary<string, double[]>(); var va = new Dictionary<string, double>(); int lastDay = int.MinValue;
            foreach (var line in File.ReadAllLines(bud))
            {
                var f = line.Split(';'); int dd;
                if (f.Length < 9 || !int.TryParse(f[0], out dd)) continue;
                if (dd > lastDay) { lastDay = dd; tb.Clear(); va.Clear(); } else if (dd < lastDay) continue;
                double w, r, pp, al;
                if (f[2] == "miasto" && double.TryParse(f[4], NumberStyles.Float, Inv, out w) && double.TryParse(f[5], NumberStyles.Float, Inv, out r) && double.TryParse(f[6], NumberStyles.Float, Inv, out pp)) tb[f[1]] = new[] { w, r, pp };
                else if (f[2] == "wies" && double.TryParse(f[8], NumberStyles.Float, Inv, out al)) va[f[1]] = al;
            }
            int nt = 0, nv = 0;
            double aMul = 1.0; { var am = Environment.GetEnvironmentVariable("KS_ALPHA_MUL"); if (!string.IsNullOrEmpty(am)) aMul = double.Parse(am, Inv); }
            foreach (var t in inp.Towns) { double[] b; if (t.ClassNeed != null && tb.TryGetValue(t.Id, out b)) { t.Budget = b; nt++; } }
            foreach (var d in inp.Districts) { double al; if (va.TryGetValue(d.Id, out al)) { d.Alpha = al * aMul; nv++; } }
            Console.WriteLine("budzety z gry (" + bud + ", doba " + lastDay + "): miast " + nt + ", alfa wsi " + nv + " x" + aMul);
        }
        return inp;
    }

    static int Main(string[] args)
    {
        string dump = args[0], cpath = args[1];
        int mode = int.Parse(args[2]);
        double capLong = double.Parse(args[3], Inv);
        int maxR = int.Parse(args[4]);
        string outp = args[5];
        string warm = args.Length > 6 ? args[6] : null;
        var inp = Build(dump, maxR);
        if (Environment.GetEnvironmentVariable("KS_NOSTALL") == "1") inp.StallExit = false;
        var cj = JsonDocument.Parse(File.ReadAllText(cpath)).RootElement;
        var tl = cj.GetProperty("towns").EnumerateArray().ToList();
        int K = tl.Count;
        var KI = new KInp { K = K, Mk = new int[K], Id = new string[K], Name = new string[K], Garum = new bool[K], Mode1 = mode == 1, CapLong = capLong,
                            Money = Environment.GetEnvironmentVariable("KS_MONEY") == "1", Subst = Environment.GetEnvironmentVariable("KS_SUBST") == "1" };
        { var cs = Environment.GetEnvironmentVariable("KS_CAPSHARE"); if (!string.IsNullOrEmpty(cs)) KI.CapShare = double.Parse(cs, Inv); }
        if (Environment.GetEnvironmentVariable("KS_GTARG") == "1") KI.GMarket = false;
        if (Environment.GetEnvironmentVariable("KS_EXCSTART") == "1") KI.ExcStart = true;
        if (Environment.GetEnvironmentVariable("KS_RETAIN") == "1") KI.Retain = true;
        var tix = new Dictionary<string, int>();
        for (int k = 0; k < K; k++) { KI.Id[k] = tl[k].GetProperty("id").GetString(); KI.Name[k] = tl[k].GetProperty("name").GetString(); KI.Mk[k] = tl[k].GetProperty("mk").GetInt32(); tix[KI.Id[k]] = k; }
        var vil = cj.GetProperty("vil");
        var landOf = new Dictionary<int, Dictionary<string, double>>();
        foreach (var t in inp.Towns) { int k; if (tix.TryGetValue(t.Id, out k)) { t.Ck = k; if (KsTab.GarumLands.Contains(t.Land)) KI.Garum[k] = true; } }
        int nod = 0;
        foreach (var d in inp.Districts)
        {
            JsonElement v; if (!vil.TryGetProperty(d.Id, out v)) { nod++; continue; }
            string h = v.GetProperty("home").GetString(); int k; if (h != null && tix.TryGetValue(h, out k)) d.Ck = k; else nod++;
        }
        // targ w innym rynku niz wies (wies Essos przy miescie Westeros) - wies zostaje w rynku targu
        int cross = 0; foreach (var d in inp.Districts) if (d.Ck >= 0 && KI.Mk[d.Ck] != d.Market) cross++;
        // lokalne towary: jedzenie rak (bez regale): zboze, ryby, mieso, ser, maslo, jaja, jablka, marchew, pomarancze, winogrona, oliwki, miod
        KI.Loc = new bool[KsTab.NG];
        foreach (int g in new[] { KsTab.GGrain, KsTab.GFish, KsTab.GMeat, KsTab.GCheese, KsTab.GButter, KsTab.GEgg, KsTab.GApple, KsTab.GCarrot, KsTab.GOrange, KsTab.GGrape, KsTab.GOlives, KsTab.GHoney }) KI.Loc[g] = true;
        // korytarze
        var ci = new List<int>(); var cjj = new List<int>(); var ct = new List<int>(); var cd = new List<double>(); var cdist = new List<double>(); var cr = new List<double>(); var ca = new List<double>();
        // wozy wsi: udzial rak wsi targu i w zasiegu miasta j, wsp. = (1 + 2 d_vj / s) / (1 + 2 d_vi / s)
        var wf = new double[K]; var aw = new Dictionary<long, double[]>();
        foreach (var d in inp.Districts)
        {
            if (d.Ck < 0 || d.TownFields) continue;
            wf[d.Ck] += d.Wf;
            JsonElement v; if (!vil.TryGetProperty(d.Id, out v)) continue;
            double dh = v.TryGetProperty("dhome", out var dhe) && dhe.ValueKind == JsonValueKind.Number ? dhe.GetDouble() : -1;
            foreach (var nb in v.GetProperty("near").EnumerateArray())
            {
                string tj = nb[0].GetString(); double dj = nb[1].GetDouble(); int j;
                if (!tix.TryGetValue(tj, out j) || j == d.Ck) continue;
                if (KI.Mk[j] != KI.Mk[d.Ck]) continue;
                double r = (1 + 2 * dj / Speed) / (1 + 2 * Math.Max(0, dh) / Speed);
                long key = (long)d.Ck * 1000 + j; double[] a; if (!aw.TryGetValue(key, out a)) { a = new double[3]; aw[key] = a; }
                a[0] += d.Wf; a[1] += d.Wf * r; a[2] = Math.Max(a[2], dj);
            }
        }
        foreach (var kv in aw)
        {
            int i = (int)(kv.Key / 1000), j = (int)(kv.Key % 1000);
            ci.Add(i); cjj.Add(j); ct.Add(0); cd.Add(kv.Value[2] / Speed); cdist.Add(kv.Value[2]); cr.Add(kv.Value[1] / kv.Value[0]); ca.Add(kv.Value[0] / Math.Max(1, wf[i]));
        }
        int nw = ci.Count;
        var tt = cj.GetProperty("tt");
        for (int i = 0; i < K; i++)
        {
            JsonElement row; if (!tt.TryGetProperty(KI.Id[i], out row)) continue;
            foreach (var pr in row.EnumerateObject())
            {
                int j; if (!tix.TryGetValue(pr.Name, out j) || KI.Mk[j] != KI.Mk[i]) continue;
                double dl = pr.Value[0].GetDouble(), ds = pr.Value[1].GetDouble();
                double dd; int typ;
                if (dl >= 0 && (ds < 0 || dl <= ds * 1.001)) { dd = dl; typ = 1; } else { dd = ds; typ = 2; }
                if (dd < 0 || dd / Speed > KI.DurDays) continue;
                ci.Add(i); cjj.Add(j); ct.Add(typ); cd.Add(dd / Speed); cdist.Add(dd); cr.Add(0); ca.Add(0);
            }
        }
        { var tn = Environment.GetEnvironmentVariable("KS_TOPN"); int topn; if (!string.IsNullOrEmpty(tn) && int.TryParse(tn, out topn) && topn > 0)
          {
            var keepIx = new List<int>();
            for (int e = 0; e < ci.Count; e++) if (ct[e] == 0) keepIx.Add(e);
            var byJ = new Dictionary<int, List<int>>();
            for (int e = 0; e < ci.Count; e++) if (ct[e] != 0) { List<int> l; if (!byJ.TryGetValue(cjj[e], out l)) { l = new List<int>(); byJ[cjj[e]] = l; } l.Add(e); }
            foreach (var kv in byJ) { kv.Value.Sort((a1, b1) => cdist[a1].CompareTo(cdist[b1])); for (int q = 0; q < Math.Min(topn, kv.Value.Count); q++) keepIx.Add(kv.Value[q]); }
            keepIx.Sort();
            ci = keepIx.Select(e => ci[e]).ToList(); cjj = keepIx.Select(e => cjj[e]).ToList(); ct = keepIx.Select(e => ct[e]).ToList(); cd = keepIx.Select(e => cd[e]).ToList();
            cdist = keepIx.Select(e => cdist[e]).ToList(); cr = keepIx.Select(e => cr[e]).ToList(); ca = keepIx.Select(e => ca[e]).ToList();
          } }
        KI.NE = ci.Count; KI.CI = ci.ToArray(); KI.CJ = cjj.ToArray(); KI.CT = ct.ToArray(); KI.CDays = cd.ToArray(); KI.CDist = cdist.ToArray(); KI.CR = cr.ToArray(); KI.CA = ca.ToArray();
        if (warm != null && File.Exists(warm))
        {
            var wp = new double[2 * KsTab.NG];
            foreach (var line in File.ReadAllLines(warm)) { var f = line.Split('\t'); if (f.Length == 3 && f[0] == "P2") { int ix = int.Parse(f[1]); wp[ix] = double.Parse(f[2], Inv); } }
            KI.WarmP2 = wp;
        }
        { var e1 = Environment.GetEnvironmentVariable("KS_THETA"); if (!string.IsNullOrEmpty(e1)) KI.Theta = double.Parse(e1, Inv); var e2 = Environment.GetEnvironmentVariable("KS_FDAMP"); if (!string.IsNullOrEmpty(e2)) KI.FDamp = double.Parse(e2, Inv); }
        inp.KIn = KI;
        Console.WriteLine("targi " + K + ", okregi bez targu " + nod + ", wsie przy targu innego rynku " + cross + ", korytarze: wozy " + nw + ", kupcy " + (KI.NE - nw) + " | tryb " + mode + ", sufit kupca " + capLong);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var R = KsCore.Solve(inp, null);
        if (R.Error != null) { Console.WriteLine("BLAD " + R.Error); return 1; }
        var O = R.KOut;
        { double fq = System.Diagnostics.Stopwatch.Frequency / 1000.0; Console.WriteLine("profil rownowagi ms: udzialy " + (KsCore.Prof[0] / fq).ToString("0") + ", podaz " + (KsCore.Prof[1] / fq).ToString("0") + ", renta " + (KsCore.Prof[4] / fq).ToString("0") + ", korytarze " + (KsCore.Prof[2] / fq).ToString("0") + ", ceny " + (KsCore.Prof[3] / fq).ToString("0") + " | Solve razem " + R.Ms.ToString("0")); }
        Console.WriteLine("Solve " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s, rund " + O.Rounds + ", zbiezny " + (O.Conv ? "TAK" : "NIE") + ", otwarte " + O.Open.Count + ", na granicy " + O.Bounds.Count);
        Console.WriteLine("  otwarte (pierwsze 30): " + string.Join(", ", O.Open.Take(30)));
        Console.WriteLine("  na granicy (pierwsze 40): " + string.Join(", ", O.Bounds.Take(40)));
        Report(inp, KI, O, outp);
        var M = R.Money;
        if (M != null)
        {
            foreach (var t in M.Trace) Console.WriteLine("  " + t);
            for (int m = 0; m < 2; m++)
            {
                double fk = Math.Max(1, M.VFullKcal[m]);
                Console.WriteLine((m == 0 ? "PIENIADZE Westeros" : "PIENIADZE Essos") + ": wsie " + M.VN[m] + " (ponad budzet " + M.VShort[m] + "), sprzedaz baz. " + M.VSalesB[m].ToString("0", Inv) + ", budzet " + M.VBudget[m].ToString("0", Inv)
                                  + ", koszyk " + M.VBasket[m].ToString("0", Inv) + ", zaplacony " + (100 * M.VPaid[m] / Math.Max(1, M.VBasket[m])).ToString("0", Inv) + "%, rece w pracy domowej " + (100 * M.VHomeHands[m] / Math.Max(1, M.VHands[m])).ToString("0.0", Inv)
                                  + "% | kcal koszyka: niezaplacone " + (100 * M.VUnpaidKcal[m] / fk).ToString("0.0", Inv) + "%, w domu " + (100 * M.VHomeKcal[m] / fk).ToString("0.0", Inv) + "% (z tego zamiennik " + (100 * O.VSubKcal[m] / fk).ToString("0.0", Inv)
                                  + "%, zatrzymane ziarno " + (100 * O.VRetKcal[m] / fk).ToString("0.0", Inv) + "%), BEZ POKRYCIA " + (100 * (M.VUnpaidKcal[m] - M.VHomeKcal[m]) / fk).ToString("0.0", Inv) + "% | nadwyzka rynku " + M.Excess[m].ToString("0", Inv));
                for (int c = 0; c < 3; c++) Console.WriteLine("   klasa " + "WRP"[c] + ": budzet " + M.TBudget[m * 3 + c].ToString("0", Inv) + ", koszyk " + M.TBasket[m * 3 + c].ToString("0", Inv) + ", zaplacony " + (100 * M.TPaid[m * 3 + c] / Math.Max(1, M.TBasket[m * 3 + c])).ToString("0.0", Inv) + "%");
            }
        }
        return 0;
    }

    static double Q(List<double> a, double q) { if (a.Count == 0) return double.NaN; a.Sort(); double k = (a.Count - 1) * q; int i = (int)k; int j = Math.Min(i + 1, a.Count - 1); return a[i] + (a[j] - a[i]) * (k - i); }

    static void Report(KsInput inp, KInp KI, KOut O, string outp)
    {
        int K = KI.K, NG = KsTab.NG;
        Func<int, double> pcs = g => { KsItem it; return inp.Items.TryGetValue(KsTab.Goods[g], out it) && it.Kg > 0 ? 1.0 / (inp.Sigma * it.Kg) : 0; };
        var pop = new double[K]; foreach (var t in inp.Towns) if (t.Ck >= 0) pop[t.Ck] += t.People;
        var vpop = new double[K]; foreach (var d in inp.Districts) if (d.Ck >= 0) vpop[d.Ck] += d.People;
        int[] food = { KsTab.GGrain, KsTab.GFish, KsTab.GMeat, KsTab.GCheese, KsTab.GButter, KsTab.GEgg, KsTab.GApple, KsTab.GCarrot, KsTab.GOrange, KsTab.GGrape, KsTab.GOlives, KsTab.GHoney };
        double[] kcal = new double[NG]; kcal[KsTab.GGrain] = 3300; kcal[KsTab.GFish] = 1100; kcal[KsTab.GMeat] = 2200; kcal[KsTab.GCheese] = 3500; kcal[KsTab.GButter] = 7000; kcal[KsTab.GEgg] = 1450; kcal[KsTab.GApple] = 500; kcal[KsTab.GCarrot] = 400; kcal[KsTab.GOrange] = 450; kcal[KsTab.GGrape] = 700; kcal[KsTab.GOlives] = 1150; kcal[KsTab.GHoney] = 3000;
        for (int m = 0; m < 2; m++)
        {
            var sb = new StringBuilder();
            sb.Append(m == 0 ? "WESTEROS" : "ESSOS").Append(" swiat S/D (targi razem; towary rynku - linia rynku):");
            foreach (int g in food)
            {
                double s = 0, d = 0; for (int k = 0; k < K; k++) if (KI.Mk[k] == m) { s += O.Snet[k * NG + g]; d += O.D[k * NG + g]; }
                if (d < 1000) continue;
                sb.Append(' ').Append(KsTab.Goods[g]).Append(' ').Append((s / d).ToString("0.00", Inv));
            }
            foreach (int g in new[] { KsTab.GSalt, KsTab.GWool, KsTab.GFlax, KsTab.GCotton, KsTab.GHides, KsTab.GIron, KsTab.GHorse })
            {
                double d = O.Dw[m * NG + g]; if (d < 1000) continue;
                sb.Append(' ').Append(KsTab.Goods[g]).Append(' ').Append((O.Sw[m * NG + g] / d).ToString("0.00", Inv));
            }
            Console.WriteLine(sb.ToString());
            // zboze per targ: wlasna podaz / popyt (przed korytarzami), po korytarzach; kcal jedzenia
            foreach (int g in new[] { KsTab.GGrain, KsTab.GFish, KsTab.GCheese, -1 })
            {
                var own = new List<double>(); var net = new List<double>(); double dsum = 0, shortOwn = 0, shortNet = 0, gl = 0; int n05 = 0, n09 = 0, nIn = 0, n15 = 0, n3 = 0, nNetIn = 0, nNetLow = 0, nCeil = 0, nk = 0;
                for (int k = 0; k < K; k++)
                {
                    if (KI.Mk[k] != m) continue;
                    double s, sn, d; double pk = 1;
                    if (g >= 0) { s = O.S[k * NG + g]; sn = O.Snet[k * NG + g]; d = O.D[k * NG + g]; pk = O.P[k * NG + g]; }
                    else { s = sn = d = 0; foreach (int f in food) { s += O.S[k * NG + f] * kcal[f]; sn += O.Snet[k * NG + f] * kcal[f]; d += O.D[k * NG + f] * kcal[f]; } }
                    if (d < 100) continue;
                    nk++;
                    double r = s / d, rn = sn / d; own.Add(r); net.Add(rn); dsum += d;
                    if (r < 0.5) n05++; else if (r < 0.9) n09++; else if (r < 1.5) nIn++; else if (r < 3) n15++; else n3++;
                    shortOwn += Math.Max(0, d - s); shortNet += Math.Max(0, d - sn); gl += Math.Max(0, sn - d);
                    if (rn >= 0.95 && rn <= 1.05) nNetIn++;
                    if (rn < 0.95) nNetLow++;
                    if (g >= 0 && pk >= 4.99) nCeil++;
                }
                string nm = g >= 0 ? KsTab.Goods[g] : "kcal jedzenia (12 towarow)";
                Console.WriteLine("  " + nm + ": targow " + nk + " | wlasna S/D q10 " + Q(own, 0.1).ToString("0.00", Inv) + " med " + Q(own, 0.5).ToString("0.00", Inv) + " q90 " + Q(own, 0.9).ToString("0.00", Inv)
                                  + " | <0.5 " + n05 + ", 0.5-0.9 " + n09 + ", 0.9-1.5 " + nIn + ", 1.5-3 " + n15 + ", >=3 " + n3 + " | brak wlasny " + (100 * shortOwn / Math.Max(1, dsum)).ToString("0.0", Inv) + "% popytu"
                                  + " || po korytarzach: w 0.95-1.05 " + nNetIn + ", ponizej 0.95 " + nNetLow + ", na suficie ceny " + nCeil + ", brak " + (100 * shortNet / Math.Max(1, dsum)).ToString("0.0", Inv) + "%, nadmiar " + (100 * gl / Math.Max(1, dsum)).ToString("0.0", Inv) + "%"
                                  + (g == KsTab.GGrain ? " | popyt " + (dsum * pcs(g)).ToString("0", Inv) + " szt/d, brak wlasny " + (shortOwn * pcs(g)).ToString("0", Inv) + " szt/d, brak po korytarzach " + (shortNet * pcs(g)).ToString("0", Inv) + " szt/d" : ""));
            }
            // przeplywy
            double[] fw = new double[3], fwd = new double[3]; double[] fk = new double[3];
            for (int e = 0; e < KI.NE; e++)
            {
                if (KI.Mk[KI.CI[e]] != m) continue;
                int t = KI.CT[e];
                double f = O.F[e * NG + KsTab.GGrain];
                fw[t] += f; fwd[t] += f * KI.CDays[e];
                foreach (int g in food) fk[t] += O.F[e * NG + g] * kcal[g];
            }
            double totd = 0; for (int k = 0; k < K; k++) if (KI.Mk[k] == m) totd += O.D[k * NG + KsTab.GGrain];
            Console.WriteLine("  przewoz zboza: wozy wsi " + (fw[0] * pcs(0)).ToString("0", Inv) + " szt/d, kupcy lad " + (fw[1] * pcs(0)).ToString("0", Inv) + " szt/d (srednio " + (fwd[1] / Math.Max(1e-9, fw[1])).ToString("0.0", Inv) + " dob), kupcy morze "
                              + (fw[2] * pcs(0)).ToString("0", Inv) + " szt/d (" + (fwd[2] / Math.Max(1e-9, fw[2])).ToString("0.0", Inv) + " dob); razem " + (100 * (fw[0] + fw[1] + fw[2]) / Math.Max(1, totd)).ToString("0.0", Inv) + "% popytu zboza; w drodze kupcow "
                              + ((fwd[1] + fwd[2]) * pcs(0)).ToString("0", Inv) + " szt | kcal jedzenia: wozy " + (fk[0] / 1e6).ToString("0", Inv) + " mln, kupcy lad " + (fk[1] / 1e6).ToString("0", Inv) + " mln, morze " + (fk[2] / 1e6).ToString("0", Inv) + " mln kcal/d");
            // rece: udzial w zbozu
            double hw = 0, ht = 0; for (int k = 0; k < K; k++) if (KI.Mk[k] == m) for (int c = 0; c < KsTab.NC; c++) { double h = O.HandsCraft[k * KsTab.NC + c]; ht += h; if (c == KsTab.CWheat) hw += h; }
            Console.WriteLine("  rece zajec w zbozu " + (100 * hw / Math.Max(1, ht)).ToString("0.0", Inv) + "% (" + (hw / 1e6).ToString("0.00", Inv) + " mln z " + (ht / 1e6).ToString("0.00", Inv) + " mln)");
        }
        // TSV per targ
        var sbT = new StringBuilder();
        sbT.Append("id\tname\tmk\tpop_miasta\tpop_wsi");
        foreach (int g in food) sbT.Append('\t').Append(KsTab.Goods[g]).Append("_D\t").Append(KsTab.Goods[g]).Append("_S\t").Append(KsTab.Goods[g]).Append("_Snet\t").Append(KsTab.Goods[g]).Append("_p");
        sbT.Append("\twheat_hands\thands\n");
        for (int k = 0; k < K; k++)
        {
            sbT.Append(KI.Id[k]).Append('\t').Append(KI.Name[k]).Append('\t').Append(KI.Mk[k]).Append('\t').Append(pop[k].ToString("0", Inv)).Append('\t').Append(vpop[k].ToString("0", Inv));
            foreach (int g in food) sbT.Append('\t').Append(O.D[k * NG + g].ToString("0.0", Inv)).Append('\t').Append(O.S[k * NG + g].ToString("0.0", Inv)).Append('\t').Append(O.Snet[k * NG + g].ToString("0.0", Inv)).Append('\t').Append(O.P[k * NG + g].ToString("0.000", Inv));
            double hw = O.HandsCraft[k * KsTab.NC + KsTab.CWheat], ht = 0; for (int c = 0; c < KsTab.NC; c++) ht += O.HandsCraft[k * KsTab.NC + c];
            sbT.Append('\t').Append(hw.ToString("0", Inv)).Append('\t').Append(ht.ToString("0", Inv)).Append('\n');
        }
        for (int i = 0; i < O.P2.Length; i++) sbT.Append("P2\t").Append(i).Append('\t').Append(O.P2[i].ToString("R", Inv)).Append('\n');
        var sbF = new StringBuilder();
        sbF.Append("from\tto\ttype\tdays\tdist\tgood\tkg_d\n");
        for (int e = 0; e < KI.NE; e++) foreach (int g in food) { double f = O.F[e * NG + g]; if (f > 1) sbF.Append(KI.Id[KI.CI[e]]).Append('\t').Append(KI.Id[KI.CJ[e]]).Append('\t').Append(KI.CT[e]).Append('\t').Append(KI.CDays[e].ToString("0.0", Inv)).Append('\t').Append(KI.CDist[e].ToString("0", Inv)).Append('\t').Append(KsTab.Goods[g]).Append('\t').Append(f.ToString("0.0", Inv)).Append('\n'); }
        File.WriteAllText(outp, sbT.ToString());
        File.WriteAllText(Path.ChangeExtension(outp, ".flows.tsv"), sbF.ToString());
    }
}
