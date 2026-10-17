using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Linq;
using Armoury;

// Test kroku 8 (pieniadze) KS: wejscie z ks_dump.json (model v2 Pythona) + koszyki ks_bask.json, rdzen C# (KsCore) z krokiem 8 i bez;
// tozsamosc: podaz targu (produkcja - zjedzone, z praca domowa) = popyt zaplacony (koszyk miast x TownPaid + koszyk wsi x DistPaid + wsady + 1:1).
// args: dump [alpha] [scenariusz miast: full|tight|none] [rundy] [money 0/1]
static class Program
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] Est = { "niewolni", "chlopi", "wyrobnicy", "rzemieslnicy", "kupcy", "szlachta" };

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

    static int Main(string[] args)
    {
        string dump = args[0];
        double alpha = args.Length > 1 ? double.Parse(args[1], Inv) : 0.47;
        string scen = args.Length > 2 ? args[2] : "full";
        int maxR = args.Length > 3 ? int.Parse(args[3]) : 3000;
        bool money = args.Length <= 4 || args[4] != "0";
        string landPath = args.Length > 5 ? args[5] : null;
        var root = JsonDocument.Parse(File.ReadAllText(dump)).RootElement;
        var bj = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(dump), "ks_bask.json"))).RootElement;
        var inp = new KsInput { RoundsFirst = maxR, RoundsR8 = Math.Min(maxR, 4000), RoundsR8b = Math.Min(maxR, 3000), SilkTownDirect = true, SilverTownDirect = true, Money = money, Keep = Keep() };
        inp.VilAlpha[0] = inp.VilAlpha[1] = alpha;
        if (Environment.GetEnvironmentVariable("KS_NOSTALL") == "1") inp.StallExit = false;
        { var te = Environment.GetEnvironmentVariable("KS_TEXP"); double tv; if (!string.IsNullOrEmpty(te) && double.TryParse(te, NumberStyles.Float, Inv, out tv)) { if (tv > 1.0) KsCore.TownExport = false; else KsCore.TownExportMin = tv; } }
        if (Environment.GetEnvironmentVariable("KS_TIMP") == "0") KsCore.TownImport = false;
        if (Environment.GetEnvironmentVariable("KS_TABLE") == "0") KsCore.TableOn = false;
        if (Environment.GetEnvironmentVariable("KS_TREACH") == "0") KsCore.TownReach = false;
        if (Environment.GetEnvironmentVariable("KS_TREACH") == "1") KsCore.TownReach = true;
        if (Environment.GetEnvironmentVariable("KS_TGRAD") == "1") KsCore.TownGrad = true;
        { var ta = Environment.GetEnvironmentVariable("KS_TALPHA"); double tv; if (!string.IsNullOrEmpty(ta) && double.TryParse(ta, NumberStyles.Float, Inv, out tv)) KsCore.TableAlpha = tv; }
        { var mg = Environment.GetEnvironmentVariable("KS_MARGIN"); double mv; if (!string.IsNullOrEmpty(mg) && double.TryParse(mg, NumberStyles.Float, Inv, out mv)) inp.MarketMargin = mv; }
        foreach (var kv in root.GetProperty("gd").EnumerateObject())
        {
            var o = kv.Value;
            Func<string, double> f = k => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;
            inp.Items[kv.Name] = new KsItem { Kg = f("kg"), Val = f("val"), Army = f("army"), Cast = f("cast"), Court = f("court"), Bud = f("bud"), Ws = f("ws"), Cons = f("cons"), Town = f("town") };
        }
        // h3port runda 3: 1:1 (wojsko, zamki, dwory) z ksiegi rundy 3 (gra bierze je z ksiegi towarow w dobie Planu) zamiast starego zrzutu
        { var ip = Environment.GetEnvironmentVariable("KS_ITEMS1"); if (!string.IsNullOrEmpty(ip) && File.Exists(ip)) { int ni = 0; foreach (var kv in JsonDocument.Parse(File.ReadAllText(ip)).RootElement.EnumerateObject()) { KsItem it; if (!inp.Items.TryGetValue(kv.Name, out it)) continue; it.Army = kv.Value.GetProperty("army").GetDouble(); it.Cast = kv.Value.GetProperty("cast").GetDouble(); it.Court = kv.Value.GetProperty("court").GetDouble(); ni++; } Console.WriteLine("1:1 z pliku " + ip + ": towarow " + ni); } }
        Action<JsonElement, double[]> needs = (o, arr) => { foreach (var kv in o.EnumerateObject()) { int i; if (KsTab.NeedIndex.TryGetValue(kv.Name, out i)) arr[i] = kv.Value.GetDouble(); } };
        Func<JsonElement, JsonElement> cbOf = s =>
        {
            string cul = s.GetProperty("cul").GetString();
            if (!bj.TryGetProperty(cul, out var cb)) bj.TryGetProperty(s.GetProperty("land").GetString(), out cb);
            return cb;
        };
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
        double sigma = inp.Sigma;
        foreach (var t in root.GetProperty("towns").EnumerateArray())
        {
            var ks = new KsSettlement { Id = t.GetProperty("id").GetString(), Land = t.GetProperty("land").GetString(), Market = t.GetProperty("mk").GetString() == "Westeros" ? 0 : 1, People = t.GetProperty("people").GetDouble() };
            needs(t.GetProperty("need0"), ks.Need0);
            var cb = cbOf(t);
            if (scen != "none" && cb.ValueKind == JsonValueKind.Object)
            {
                var bask = BasketOf(cb); bool oil = cb.GetProperty("oil").GetBoolean(), north = cb.GetProperty("north").GetBoolean();
                var ppl = new double[6];
                foreach (var kv in t.GetProperty("ppl").EnumerateObject()) { int e = Array.IndexOf(Est, kv.Name); if (e >= 0) ppl[e] = kv.Value.GetDouble(); }
                ks.ClassNeed = new double[3][]; ks.Budget = new double[3];
                for (int c = 0; c < 3; c++) ks.ClassNeed[c] = new double[KsTab.NN];
                var one = new double[6]; var n = new double[KsTab.NN];
                for (int e = 0; e < 6; e++)
                {
                    if (!(ppl[e] > 0)) continue;
                    Array.Clear(one, 0, 6); one[e] = ppl[e];
                    KsCore.NeedsOf(one, bask, oil, north, true, false, ppl[e], n);
                    int c = (e == KsTab.ELabour || e == KsTab.EPeasant) ? 0 : e == KsTab.ECraft ? 1 : (e == KsTab.EMerchant || e == KsTab.EGentry) ? 2 : -1;
                    for (int k = 0; k < KsTab.NN; k++)
                    {
                        if (c >= 0) ks.ClassNeed[c][k] += n[k];
                        else { ks.ClassNeed[1][k] += 0.4 * n[k]; ks.ClassNeed[2][k] += 0.6 * n[k]; }
                    }
                }
                // budzet scenariusza: koszt koszyka klasy przy cenie bazowej (wartosc / (sigma x kg)) x czynnik
                double[] fac = scen == "tight" ? new[] { 3.0, 0.8, 0.9 } : new[] { 5.0, 5.0, 5.0 };
                for (int c = 0; c < 3; c++)
                {
                    var q = new double[KsBasket.N]; double fk; KsBasket.TownKg(ks.ClassNeed[c], 0, q, out fk);
                    double cost = 0;
                    for (int z = 0; z < KsBasket.N; z++) { KsItem it; if (q[z] > 0 && inp.Items.TryGetValue(KsBasket.Ids[z], out it) && it.Kg > 0) cost += q[z] * it.Val / (sigma * it.Kg); }
                    ks.Budget[c] = cost * fac[c];
                }
            }
            inp.Towns.Add(ks);
        }
        if (landPath != null && File.Exists(landPath)) Console.WriteLine("(tryb ziemi z pliku pominiety w tym tescie)");
        // recenzja 17.10: budzety miast W/R/P i alfa wsi zmierzone w grze (ks-budzety.csv z sesji logu) zamiast scenariusza "full/tight"
        string bud = Environment.GetEnvironmentVariable("KS_BUDGETS");
        if (!string.IsNullOrEmpty(bud) && File.Exists(bud))
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
            foreach (var t in inp.Towns) { double[] b; if (t.ClassNeed != null && tb.TryGetValue(t.Id, out b)) { t.Budget = b; nt++; } }
            foreach (var d in inp.Districts) { double al; if (va.TryGetValue(d.Id, out al)) { d.Alpha = al; nv++; } }
            Console.WriteLine("budzety z gry (" + bud + ", doba " + lastDay + "): miast " + nt + ", alfa wsi " + nv);
        }
        // h3port: alfa okregow z pliku (id;alfa) - alfa netto z symulatora; czynnik popytu targu (g:m:v,... albo *:v)
        string alf = Environment.GetEnvironmentVariable("KS_ALPHA");
        if (!string.IsNullOrEmpty(alf) && File.Exists(alf))
        {
            var am = new Dictionary<string, double>();
            foreach (var line in File.ReadAllLines(alf)) { var f = line.Split(';'); double a; if (f.Length >= 2 && double.TryParse(f[1], NumberStyles.Float, Inv, out a)) am[f[0]] = a; }
            int na = 0; foreach (var d in inp.Districts) { double a; if (am.TryGetValue(d.Id, out a)) { d.Alpha = a; na++; } }
            Console.WriteLine("alfa z pliku " + alf + ": okregow " + na);
        }
        string mfs = Environment.GetEnvironmentVariable("KS_MF");
        if (!string.IsNullOrEmpty(mfs))
        {
            var mf = new double[2 * KsTab.NG]; double mg0 = inp.MarketMargin > 1.0 ? inp.MarketMargin : 1.0; for (int i = 0; i < mf.Length; i++) mf[i] = mg0;
            foreach (var e in mfs.Split(','))
            {
                var f = e.Split(':'); int g = Array.IndexOf(KsTab.Goods, f[0]); int m; double v;
                if (f.Length == 3 && g >= 0 && int.TryParse(f[1], out m) && double.TryParse(f[2], NumberStyles.Float, Inv, out v)) mf[m * KsTab.NG + g] = v;
                else if (f.Length == 2 && f[0] == "*" && double.TryParse(f[1], NumberStyles.Float, Inv, out v)) for (int i = 0; i < mf.Length; i++) mf[i] = v;
            }
            inp.MarketFactor = mf;
            Console.WriteLine("czynnik popytu targu z KS_MF: " + mfs);
        }
        var dbg = new Dictionary<string, List<string>>();
        string dbgPath = Environment.GetEnvironmentVariable("KS_DEBUG");
        if (!string.IsNullOrEmpty(dbgPath))
            KsCore.MoneyDebug = (it, id, ty, m, hw, tg, hv, g, raw, v0, bud, sal, bas, pv) =>
            {
                List<string> l; if (!dbg.TryGetValue(id, out l)) { l = new List<string>(); dbg[id] = l; }
                l.Add(it + ";" + id + ";" + ty + ";" + m + ";" + hw.ToString("0.0000", Inv) + ";" + tg.ToString("0.0000", Inv) + ";" + hv.ToString("0.0000", Inv) + ";" + g.ToString("0.0000", Inv) + ";" + raw.ToString("0.0", Inv) + ";" + v0.ToString("0.0", Inv) + ";" + bud.ToString("0.0", Inv) + ";" + sal.ToString("0.0", Inv) + ";" + bas.ToString("0.0", Inv) + ";" + pv.ToString("0.0", Inv));
            };
        string world = Environment.GetEnvironmentVariable("KS_WORLD");
        int nPlans = 1;
        if (!string.IsNullOrEmpty(world) && File.Exists(world))
        {
            // KS-R3 krok 5 na swiecie symulatora (ksim/sim/world.json): doby miast z drog geo (41 jedn./d), morze, wojna (Dothrakowie, Wolni Ludzie)
            var w = JsonDocument.Parse(File.ReadAllText(world)).RootElement;
            var wt = new List<string>(); var wk = new List<string>();
            foreach (var t in w.GetProperty("towns").EnumerateArray()) { wt.Add(t.GetProperty("id").GetString()); wk.Add(t.GetProperty("kingdom").GetString()); }
            var dl = w.GetProperty("dland"); var ds = w.GetProperty("dsea");
            int nt = inp.Towns.Count;
            var map = new int[nt];
            for (int i = 0; i < nt; i++) map[i] = wt.IndexOf(inp.Towns[i].Id);
            inp.Corridors = Environment.GetEnvironmentVariable("KS_GRAD") != "0"; inp.Reach = Environment.GetEnvironmentVariable("KS_REACH") != "0"; inp.HomeSub = Environment.GetEnvironmentVariable("KS_SUB") != "0";
            inp.ApplyPlan = true; inp.AfterD = false;
            inp.TownDays = new float[nt * nt]; inp.TownSea = new bool[nt * nt]; inp.TownWar = new bool[nt * nt]; inp.TownGold28 = new double[nt];
            var war = new HashSet<string> { "Dothraki Horde", "Free Folk" };
            Dictionary<string, double> goldMap = null; string gp = Environment.GetEnvironmentVariable("KS_GOLD");
            if (!string.IsNullOrEmpty(gp) && File.Exists(gp))
            {
                goldMap = new Dictionary<string, double>();
                foreach (var line in File.ReadAllLines(gp)) { var f = line.Split(';'); double gv; if (f.Length >= 2 && double.TryParse(f[1], NumberStyles.Float, Inv, out gv)) goldMap[f[0]] = gv; }
                Console.WriteLine("kasy 28 dob z pliku " + gp + ": " + goldMap.Count);
            }
            for (int a2 = 0; a2 < nt; a2++)
            {
                inp.TownGold28[a2] = 150000;
                if (goldMap != null) { double gv; if (goldMap.TryGetValue(inp.Towns[a2].Id, out gv)) inp.TownGold28[a2] = gv; }
                for (int b2 = 0; b2 < nt; b2++)
                {
                    int k = a2 * nt + b2; inp.TownDays[k] = -1f;
                    if (a2 == b2 || map[a2] < 0 || map[b2] < 0 || inp.Towns[a2].Market != inp.Towns[b2].Market) continue;
                    double l = dl[map[a2]][map[b2]].GetDouble(), sea = ds[map[a2]][map[b2]].GetDouble();
                    bool sf = l < 0 || (sea >= 0 && l > 1.15 * sea);
                    double road = sf ? sea : l;
                    if (road < 0) continue;
                    inp.TownDays[k] = (float)Math.Max(1.0, Math.Ceiling(road / 41.0)); inp.TownSea[k] = sf;
                    string ka = wk[map[a2]], kb = wk[map[b2]];
                    inp.TownWar[k] = ka != kb && (war.Contains(ka) || war.Contains(kb));
                }
            }
            var tix = new Dictionary<string, int>(); for (int i = 0; i < nt; i++) tix[inp.Towns[i].Id] = i;
            var vtr = new Dictionary<string, int>();
            foreach (var v in w.GetProperty("villages").EnumerateArray()) { int tr = v.GetProperty("trade").GetInt32(); if (tr >= 0 && tr < wt.Count) { int ti; if (tix.TryGetValue(wt[tr], out ti)) vtr[v.GetProperty("id").GetString()] = ti; } }
            inp.DistTown = new Dictionary<string, int>();
            foreach (var d in inp.Districts) { int ti; if (d.TownFields) { if (tix.TryGetValue(d.Id.Replace("#pola", ""), out ti)) inp.DistTown[d.Id] = ti; } else if (vtr.TryGetValue(d.Id, out ti)) inp.DistTown[d.Id] = ti; }
            int.TryParse(Environment.GetEnvironmentVariable("KS_PLANS") ?? "4", out nPlans);
            Console.WriteLine("korytarze: miast " + nt + " (w swiecie " + map.Count(x => x >= 0) + "), okregi z miastem targowym " + inp.DistTown.Count + "/" + inp.Districts.Count + ", plany " + nPlans);
        }
        KsResult R = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int pl = 0; pl < Math.Max(1, nPlans); pl++)
        {
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            R = KsCore.Solve(inp, null);
            if (R.Error != null) { Console.WriteLine("BLAD: " + R.Error); return 1; }
            Console.WriteLine("PLAN " + (pl + 1) + ": " + sw1.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s, rund " + R.Rounds + ", krok 5 rund " + R.CorrRounds + " (" + R.CorrMs.ToString("0", Inv) + " ms), gradient max " + R.GradMax.ToString("0.000", Inv) + ", zmiana " + (100 * R.GradDMax).ToString("0.0", Inv) + "% (komorek > 5%: " + R.GradDBig + ")");
            if (R.CorrLine != null) Console.WriteLine("  " + R.CorrLine.Substring(0, Math.Min(R.CorrLine.Length, 2500)));
            var M0 = R.Money;
            if (M0 != null) for (int m = 0; m < 2; m++) { double fk = Math.Max(1e-9, M0.VFullKcal[m]); Console.WriteLine("  " + (m == 0 ? "W" : "E") + ": Plan bez pokrycia " + (100 * Math.Max(0.0, M0.VUnpaidKcal[m] - M0.VHomeKcal[m]) / fk).ToString("0.0", Inv) + "%, zastepcze " + (100 * M0.VSubKcal[m] / fk).ToString("0.0", Inv) + "%, sprzedaz " + M0.VSalesB[m].ToString("0", Inv) + ", stol 8d " + M0.TableKg[m].ToString("0", Inv) + " kg/d (" + (100 * M0.TableKcal[m] / fk).ToString("0.0", Inv) + "% kcal)"); }
            for (int m = 0; m < 2; m++)
            {
                var o2 = new List<string>();
                foreach (int g in KsCore.Food) { double D = R.D[m * KsTab.NG + g]; if (D < 1000) continue; double E = R.Eaten != null ? R.Eaten[m * KsTab.NG + g] : 0; o2.Add(KsTab.Goods[g] + " " + ((R.S[m * KsTab.NG + g] - E) / Math.Max(1, D - E)).ToString("0.00", Inv)); }
                Console.WriteLine("  " + (m == 0 ? "W" : "E") + " NA TARG: " + string.Join(", ", o2));
            }
            if (R.Grad != null && Environment.GetEnvironmentVariable("KS_GDUMP") == "1")
            {
                var gl = new List<string>();
                foreach (var kv in R.Grad) foreach (int g in KsCore.Food) if (Math.Abs(kv.Value[g] - 1.0) > 0.02) gl.Add(kv.Key + ":" + KsTab.Goods[g] + "=" + kv.Value[g].ToString("0.00", Inv));
                Console.WriteLine("  GRAD: " + string.Join(" ", gl));
                Console.WriteLine("  KRAINA: " + string.Join(", ", R.Kraina) + " | BEZ ZRODLA: " + R.Unreached.Count);
            }
            inp.GradPrev = R.Grad; inp.WarmPrices = (double[])R.P.Clone();
            inp.WarmRent = new Dictionary<string, double>(); for (int b = 0; b < R.NB; b++) if (R.Blocks[b].Uid != null) inp.WarmRent[R.Blocks[b].Uid] = R.Rent[b];
            if (R.DistHome != null && R.DistHome.Count > 0) inp.WarmHome = new Dictionary<string, double>(R.DistHome);
            inp.ApplyPlan = pl + 2 >= nPlans;
        }
        if (!string.IsNullOrEmpty(dbgPath)) { var all = new List<string> { "it;id;typ;m;hw;cel;HwV;G;surowiec;V0;budzet;sprzedaz;koszyk;zaplacone" }; foreach (var kv in dbg) all.AddRange(kv.Value); File.WriteAllLines(dbgPath, all); }
        Console.WriteLine("Solve: " + (R.Error ?? "OK") + " rund " + R.Rounds + " (pierwsza " + R.RoundsFirst + ", R8 " + R.R8Rounds + ", R8b " + R.R8bRounds + ") czas " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s; blokow " + R.NB + ", par " + R.NP
                          + " | alfa " + alpha.ToString("0.00", Inv) + ", miasta " + scen + ", krok 8 " + (money ? "TAK" : "NIE"));
        if (R.Error != null) return 1;
        Console.WriteLine("niezbiezne: " + string.Join(", ", R.Open) + " | na granicy: " + string.Join(", ", R.Bounds));
        var M = R.Money;
        if (M != null)
        {
            foreach (var t in M.Trace) Console.WriteLine("  " + t);
            for (int m = 0; m < 2; m++)
            {
                Console.WriteLine((m == 0 ? "Westeros" : "Essos") + ": wsie " + M.VN[m] + " (ponad budzet " + M.VShort[m] + "), sprzedaz " + M.VSales[m].ToString("0", Inv) + " zl/d, budzet " + M.VBudget[m].ToString("0", Inv)
                                  + ", koszyk pelny " + M.VBasket[m].ToString("0", Inv) + ", zaplacony " + M.VPaid[m].ToString("0", Inv) + " (" + (100 * M.VPaid[m] / Math.Max(1, M.VBasket[m])).ToString("0", Inv) + "%), w domu " + M.VHome[m].ToString("0", Inv)
                                  + ", rece w pracy domowej " + (100 * M.VHomeHands[m] / Math.Max(1, M.VHands[m])).ToString("0.0", Inv) + "%, nadwyzka rynku " + M.Excess[m].ToString("0", Inv) + ", wspolna " + M.Global[m].ToString("0.000", Inv));
                Console.WriteLine("   sprzedaz bazowa " + M.VSalesB[m].ToString("0", Inv) + " (konie " + M.VHorse[m].ToString("0", Inv) + "), bez pracy domowej " + M.VSalesNoHw[m].ToString("0", Inv)
                                  + ", kcal: pelny " + (M.VFullKcal[m] / 1e6).ToString("0.0", Inv) + " mln, niezaplacony " + (100 * M.VUnpaidKcal[m] / Math.Max(1, M.VFullKcal[m])).ToString("0", Inv) + "%, robiony w domu " + (100 * M.VHomeKcal[m] / Math.Max(1, M.VFullKcal[m])).ToString("0", Inv)
                                  + "%, surowiec bez pokrycia " + M.VUnmetRaw[m].ToString("0", Inv) + " | zbiezny " + (M.Conv[m] ? "TAK" : "NIE") + (M.NoHands[m] ? " (brak rak do oddania)" : "") + ", nadwyzka/popyt targu " + (100 * M.ExcessRel[m]).ToString("0.0", Inv) + "%, zmiana sum " + (100 * M.DTot[m]).ToString("0.00", Inv) + "%, zmiana hw max " + M.DHwM[m].ToString("0.0000", Inv) + " w " + M.DHwN[m] + " wsiach >= 0.05"
                                  + " | alfa " + M.AlphaMin[m].ToString("0.00", Inv) + "/" + M.AlphaMed[m].ToString("0.00", Inv) + "/" + M.AlphaMax[m].ToString("0.00", Inv));
                for (int c = 0; c < 3; c++)
                    Console.WriteLine("   klasa " + "WRP"[c] + ": budzet " + M.TBudget[m * 3 + c].ToString("0", Inv) + ", koszyk " + M.TBasket[m * 3 + c].ToString("0", Inv) + ", zaplacony " + (100 * M.TPaid[m * 3 + c] / Math.Max(1, M.TBasket[m * 3 + c])).ToString("0.0", Inv) + "%");
            }
        }
        Console.WriteLine("BILANS Planu (S/D, D >= 1000 kg/d):");
        for (int m = 0; m < 2; m++)
        {
            var o = new List<string>();
            for (int g = 0; g < KsTab.NG; g++) { double D = R.D[m * KsTab.NG + g]; if (D < 1000) continue; o.Add(KsTab.Goods[g] + " " + (R.S[m * KsTab.NG + g] / D).ToString("0.00", Inv)); }
            Console.WriteLine("  " + (m == 0 ? "W" : "E") + ": " + string.Join(", ", o));
            if (R.Eaten != null)
            {
                var o2 = new List<string>();
                for (int g = 0; g < KsTab.NG; g++) { double D = R.D[m * KsTab.NG + g]; if (D < 1000) continue; double E = R.Eaten[m * KsTab.NG + g]; o2.Add(KsTab.Goods[g] + " " + ((R.S[m * KsTab.NG + g] - E) / Math.Max(1, D - E)).ToString("0.00", Inv)); }
                Console.WriteLine("  " + (m == 0 ? "W" : "E") + " NA TARG (S-zj)/(D-zj): " + string.Join(", ", o2));
            }
        }
        // praca domowa wedlug typu wsi (srednia wazona rekami)
        var byType = new SortedDictionary<string, double[]>();
        foreach (var d in inp.Districts)
        {
            if (d.TownFields) continue;
            double hw; if (!R.DistHome.TryGetValue(d.Id, out hw)) continue;
            float[] pz; R.DistPaid.TryGetValue(d.Id, out pz);
            double pf = 0; if (pz != null) { foreach (var x in pz) pf += x; pf /= pz.Length; }
            string k = (d.Market == 0 ? "W " : "E ") + d.Type;
            double[] a; if (!byType.TryGetValue(k, out a)) { a = new double[4]; byType[k] = a; }
            a[0] += d.Wf * hw; a[1] += d.Wf; a[2] += 1; a[3] += pf;
        }
        Console.WriteLine("PRACA DOMOWA wedlug typu wsi (rece, srednia czesc zaplacona pozycji koszyka):");
        foreach (var kv in byType) Console.WriteLine("  " + kv.Key + ": wsi " + kv.Value[2] + ", praca domowa " + (100 * kv.Value[0] / Math.Max(1, kv.Value[1])).ToString("0", Inv) + "%, zaplacone pozycje " + (100 * kv.Value[3] / Math.Max(1, kv.Value[2])).ToString("0", Inv) + "%");
        MoneyIdentity(inp, R);
        string outp = Environment.GetEnvironmentVariable("KS_OUT");
        if (!string.IsNullOrEmpty(outp)) Dump(inp, R, outp);
        return 0;
    }

    /// <summary>h3port: na okreg - praca domowa, kcal domu (DistHomeKcal), czesc kcal zaplacona, Plan bez pokrycia, na targ kg/d wedlug towaru dostawy, czesc zaplacona koszyka;
    /// na miasto - czesc zaplacona koszyka miasta. Ten sam rachunek co MoneyStep / ComputeDistrict (Produce, NeedsOfKeep x lepszy stol, Own).</summary>
    static void Dump(KsInput inp, KsResult R, string path)
    {
        int NI = KsDeliver.ItemCount, NZ = KsBasket.N;
        var sb = new System.Text.StringBuilder(1 << 20);
        sb.Append("D;id;mk;tf;hw;homekcal;pk;unc;fullkcal");
        foreach (var id in KsDeliver.ItemIds) sb.Append(";mk_").Append(id);
        foreach (var id in KsBasket.Ids) sb.Append(";paid_").Append(id);
        sb.Append('\n');
        var prod = new double[NI]; var own = new double[NI]; var vq = new double[NZ];
        foreach (var d in inp.Districts)
        {
            var kparts = new KsPart[d.Blocks.Count];
            for (int i = 0; i < kparts.Length; i++) { kparts[i] = new KsPart { K = d.Blocks[i] }; KsDeliver.FromPlan(R, d.Blocks[i].Index, kparts[i]); }
            double hw; if (!d.TownFields && R.DistHome.TryGetValue(d.Id, out hw)) d.HomeWork = hw; else hw = d.TownFields ? 0 : d.HomeWork;
            KsDeliver.Produce(d, kparts, prod);
            Array.Clear(own, 0, NI);
            double pk = 0, unc = 0, fk = 0; float hkf = 0; float[] pz = null;
            if (!d.TownFields && d.PplHome != null && d.Bask != null)
            {
                var on = new double[KsTab.NN]; double home = 0; foreach (var e in d.PplHome) home += e;
                KsCore.NeedsOfKeep(d.PplHome, d.Bask, inp.Keep, d.Oil, home, on);
                double[] dn; R.DistNeed.TryGetValue(d.Id, out dn);
                var full = new double[KsTab.NN];
                double hr = d.People > 0 ? Math.Min(1.0, home / d.People) : 0;
                for (int k = 0; k < KsTab.NN; k++)
                {
                    double mult = dn != null && d.Need0[k] > 1e-9 ? Math.Max(0.25, Math.Min(4.0, dn[k] / d.Need0[k])) : 1.0;
                    on[k] *= mult; full[k] = (dn != null ? dn[k] : d.Need0[k]) * hr;
                }
                KsDeliver.Own(prod, on, own);
                Array.Clear(vq, 0, NZ);
                KsBasket.VillageKg(full, on, own, vq);
                R.DistPaid.TryGetValue(d.Id, out pz); R.DistHomeKcal.TryGetValue(d.Id, out hkf);
                double pkc = 0;
                for (int z = 0; z < NZ; z++) { double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue; fk += vq[z] * kc; pkc += vq[z] * kc * (pz != null ? pz[z] : 1f); }
                pk = fk > 0 ? pkc / fk : 0; unc = Math.Max(0.0, 1.0 - pk - hkf);
            }
            sb.Append("D;").Append(d.Id).Append(';').Append(d.Market).Append(';').Append(d.TownFields ? 1 : 0).Append(';').Append(hw.ToString("0.#####", Inv)).Append(';').Append(hkf.ToString("0.#####", Inv))
              .Append(';').Append(pk.ToString("0.#####", Inv)).Append(';').Append(unc.ToString("0.#####", Inv)).Append(';').Append(fk.ToString("0.#", Inv));
            for (int i = 0; i < NI; i++) sb.Append(';').Append(Math.Max(0.0, prod[i] - own[i]).ToString("0.####", Inv));
            for (int z = 0; z < NZ; z++) sb.Append(';').Append(pz != null ? pz[z].ToString("0.#####", Inv) : "1");
            sb.Append('\n');
        }
        foreach (var t in inp.Towns)
        {
            float[] pz; R.TownPaid.TryGetValue(t.Id, out pz);
            sb.Append("T;").Append(t.Id).Append(';').Append(t.Market);
            for (int z = 0; z < NZ; z++) sb.Append(';').Append(pz != null ? pz[z].ToString("0.#####", Inv) : "1");
            sb.Append('\n');
        }
        if (R.Grad != null) foreach (var kv in R.Grad) { sb.Append("G;").Append(kv.Key); foreach (int g in KsCore.Food) sb.Append(';').Append(KsTab.Goods[g]).Append('=').Append(kv.Value[g].ToString("0.###", Inv)); sb.Append('\n'); }
        foreach (var k in R.Kraina) sb.Append("K;").Append(k).Append('\n');
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine("zrzut okregow: " + path);
    }

    /// <summary>Tozsamosc targu z krokiem 8: podaz targu KS (produkcja z praca domowa - zjedzone) wobec popytu zaplaconego (koszyki x czesc zaplacona, wsady, 1:1).</summary>
    static void MoneyIdentity(KsInput inp, KsResult R)
    {
        int NI = KsDeliver.ItemCount, NZ = KsBasket.N; double sigma = inp.Sigma;
        var supM = new double[2, NI]; var ownM = new double[2, NI]; var bask = new double[2, NZ]; var fire = new double[2]; var ppl = new double[2];
        var prod = new double[NI]; var own = new double[NI]; var vq = new double[NZ];
        foreach (var d in inp.Districts)
        {
            int m = d.Market;
            var kparts = new KsPart[d.Blocks.Count];
            for (int i = 0; i < kparts.Length; i++) { kparts[i] = new KsPart { K = d.Blocks[i] }; KsDeliver.FromPlan(R, d.Blocks[i].Index, kparts[i]); }
            KsDeliver.Produce(d, kparts, prod);
            if (d.TownFields) { for (int i = 0; i < NI; i++) supM[m, i] += prod[i]; continue; }
            if (d.PplHome == null) continue;
            var on = new double[KsTab.NN]; double home = 0; foreach (var e in d.PplHome) home += e;
            KsCore.NeedsOfKeep(d.PplHome, d.Bask, inp.Keep, d.Oil, home, on);
            double[] dn; R.DistNeed.TryGetValue(d.Id, out dn);
            var full = new double[KsTab.NN];
            double hr = d.People > 0 ? Math.Min(1.0, home / d.People) : 0;
            for (int k = 0; k < KsTab.NN; k++)
            {
                double mult = dn != null && d.Need0[k] > 1e-9 ? Math.Max(0.25, Math.Min(4.0, dn[k] / d.Need0[k])) : 1.0;
                on[k] *= mult; full[k] = (dn != null ? dn[k] : d.Need0[k]) * hr;
            }
            KsDeliver.Own(prod, on, own);
            for (int i = 0; i < NI; i++) { supM[m, i] += prod[i] - own[i]; ownM[m, i] += own[i]; }
            Array.Clear(vq, 0, NZ);
            KsBasket.VillageKg(full, on, own, vq);
            float[] pz; R.DistPaid.TryGetValue(d.Id, out pz);
            for (int z = 0; z < NZ; z++) bask[m, z] += vq[z] * (pz != null ? pz[z] : 1f);
            ppl[m] += d.People;
        }
        foreach (var t in inp.Towns)
        {
            int m = t.Market;
            var q = new double[NZ]; double fk;
            KsBasket.TownKg(t.Need0, 0.0, q, out fk);
            float[] pz; R.TownPaid.TryGetValue(t.Id, out pz);
            for (int z = 0; z < NZ; z++) bask[m, z] += q[z] * (pz != null ? pz[z] : 1f);
            fire[m] += fk; ppl[m] += t.People;
        }
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
            A(KsTab.GFlax, b(KsBasket.Flax) + (b(KsBasket.Linen) + o("linen")) * KsTab.LinenFlax);
            A(KsTab.GWool, b(KsBasket.Wool) + (b(KsBasket.Felt) + o("felt")) * KsTab.FeltWool);
        }
        dem[1, KsTab.GFish] += KsTab.Garum * 2.0; dem[1, KsTab.GSalt] += KsTab.Garum * 0.4;
        Console.WriteLine("TOZSAMOSC z krokiem 8 (kg/d): towar | rynek | podaz targu | popyt zaplacony | targ podaz/popyt | Plan S/D | (podaz targu + zjedzone)/S | (popyt + zjedzone)/D");
        int[] gs = { KsTab.GGrain, KsTab.GFish, KsTab.GMeat, KsTab.GCheese, KsTab.GButter, KsTab.GGrape, KsTab.GOlives, KsTab.GOrange, KsTab.GDate, KsTab.GSalt, KsTab.GWool, KsTab.GFlax };
        for (int m = 0; m < 2; m++)
            foreach (int g in gs)
            {
                double D = R.D[m * KsTab.NG + g], S = R.S[m * KsTab.NG + g];
                if (D < 1000) continue;
                double sup, ow;
                if (g == KsTab.GMeat)
                {
                    sup = supM[m, KsDeliver.IMeat] + supM[m, KsDeliver.IWhaleMeat] + (supM[m, KsDeliver.ICow] + supM[m, KsDeliver.IHog] + supM[m, KsDeliver.ISheep]) * KsTab.MeatOfLive;
                    ow = ownM[m, KsDeliver.IMeat] + ownM[m, KsDeliver.IWhaleMeat] + (ownM[m, KsDeliver.ICow] + ownM[m, KsDeliver.IHog] + ownM[m, KsDeliver.ISheep]) * KsTab.MeatOfLive;
                }
                else { int i = Array.IndexOf(KsDeliver.ItemIds, KsTab.Goods[g]); sup = supM[m, i]; ow = ownM[m, i]; }
                double dm = dem[m, g];
                Console.WriteLine(KsTab.Goods[g] + " | " + (m == 0 ? "W" : "E") + " | " + (sup / 1000).ToString("0.0", Inv) + " | " + (dm / 1000).ToString("0.0", Inv) + " | " + (dm > 0 ? (sup / dm).ToString("0.000", Inv) : "-")
                                  + " | " + (S / D).ToString("0.000", Inv) + " | " + ((sup + ow) / S).ToString("0.000", Inv) + " | " + ((dm + ow) / D).ToString("0.000", Inv));
            }
    }
}
