using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Armoury;

// Test portu KS: wejscie z ks_dump.json (model v2 Pythona), rdzen C# (KsCore), porownanie z ks_bilans_v2_strefa_kosz_in1.25.tsv.
static class Program
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static int Main(string[] args)
    {
        string dump = args[0], bil = args.Length > 1 ? args[1] : null;
        int maxR = args.Length > 2 ? int.Parse(args[2]) : 6000;
        bool first = args.Length > 3 && args[3] == "first";
        var doc = JsonDocument.Parse(File.ReadAllText(dump));
        var root = doc.RootElement;
        var inp = new KsInput { RoundsFirst = maxR, RoundsR8 = Math.Min(maxR, 4000), RoundsR8b = Math.Min(maxR, 3000), StopAfterFirst = first, SilkTownDirect = true, SilverTownDirect = true };
        foreach (var kv in root.GetProperty("gd").EnumerateObject())
        {
            var o = kv.Value;
            Func<string, double> f = k => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;
            inp.Items[kv.Name] = new KsItem { Kg = f("kg"), Val = f("val"), Army = f("army"), Cast = f("cast"), Court = f("court"), Bud = f("bud"), Ws = f("ws"), Cons = f("cons"), Town = f("town") };
        }
        var unknownKeys = new HashSet<string>();
        Action<JsonElement, double[]> needs = (o, arr) =>
        {
            foreach (var kv in o.EnumerateObject())
            {
                int i;
                if (KsTab.NeedIndex.TryGetValue(kv.Name, out i)) arr[i] = kv.Value.GetDouble();
                else unknownKeys.Add(kv.Name);
            }
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
            inp.Districts.Add(kd);
        }
        foreach (var t in root.GetProperty("towns").EnumerateArray())
        {
            var ks = new KsSettlement { Id = t.GetProperty("id").GetString(), Land = t.GetProperty("land").GetString(), Market = t.GetProperty("mk").GetString() == "Westeros" ? 0 : 1, People = t.GetProperty("people").GetDouble() };
            needs(t.GetProperty("need0"), ks.Need0);
            inp.Towns.Add(ks);
        }
        if (unknownKeys.Count > 0) Console.WriteLine("nieznane klucze potrzeb: " + string.Join(",", unknownKeys));

        // test 1: pary i produkcja par (pierwsze 2000 par modelu)
        var pf = root.GetProperty("pairs_first"); var lp = root.GetProperty("lp"); var op = root.GetProperty("outp_first"); var la = root.GetProperty("landp_first");
        var byUid = new Dictionary<string, Tuple<KsDistrict, KsBlock>>();
        foreach (var d in inp.Districts) foreach (var b in d.Blocks) byUid[b.Uid] = Tuple.Create(d, b);
        int bad = 0, n = 0; var outv = new double[KsTab.NG];
        var candOf = new Dictionary<string, List<int>>();
        for (int k = 0; k < pf.GetArrayLength(); k++)
        {
            string uid = pf[k][0].GetString(), craft = pf[k][1].GetString();
            var db = byUid[uid];
            List<int> cands; if (!candOf.TryGetValue(uid, out cands)) { cands = KsCore.Candidates(db.Item2, db.Item1.Type); candOf[uid] = cands; }
            int c = KsTab.CraftIndex[craft];
            if (!cands.Contains(c)) { bad++; if (bad < 5) Console.WriteLine("brak kandydata " + uid + " " + craft); }
            double wH, sub, cl, W;
            KsCore.BlockOutput(db.Item1, db.Item2, c, outv, out wH, out sub, out cl, out W);
            double lpc = Math.Max(1e-6, KsCore.LandEff(db.Item2, c, db.Item1.Type));
            double landp = wH * KsTab.HaHome + sub * KsTab.HaOwnGrain + cl * KsTab.HaPerWorker[c];
            if (Math.Abs(lpc - lp[k].GetDouble()) > 1e-9) { bad++; if (bad < 10) Console.WriteLine("L " + uid + " " + craft + " " + lpc + " vs " + lp[k].GetDouble()); }
            if (Math.Abs(landp - la[k].GetDouble()) > 1e-6 * Math.Max(1, landp)) { bad++; if (bad < 10) Console.WriteLine("LANDP " + uid + " " + craft + " " + landp + " vs " + la[k].GetDouble()); }
            for (int g = 0; g < KsTab.NG; g++)
            {
                double want = op[k][g].GetDouble();
                if (Math.Abs(outv[g] - want) > 1e-6 * Math.Max(1, Math.Abs(want))) { bad++; if (bad < 10) Console.WriteLine("OUT " + uid + " " + craft + " " + KsTab.Goods[g] + " " + outv[g] + " vs " + want); }
            }
            n++;
        }
        // kandydaci wszystkich blokow wobec modelu
        var cand = root.GetProperty("cand"); int cbad = 0, ctot = 0;
        foreach (var kv in cand.EnumerateObject())
        {
            var db = byUid[kv.Name];
            var mine = KsCore.Candidates(db.Item2, db.Item1.Type);
            var want = new List<int>(); foreach (var e in kv.Value.EnumerateArray()) want.Add(KsTab.CraftIndex[e.GetString()]);
            ctot++;
            if (string.Join(",", mine) != string.Join(",", want)) { cbad++; if (cbad < 5) Console.WriteLine("KANDYDACI " + kv.Name + " moje " + string.Join(",", mine) + " model " + string.Join(",", want)); }
        }
        Console.WriteLine("test par: " + n + " par, roznic " + bad + "; kandydaci blokow " + ctot + ", roznic " + cbad + "; par modelu " + root.GetProperty("np").GetInt32());
        // test 2: potrzeby osad (needs_of) z ludzi stanow i koszykow modelu (KS-K)
        var bpath = Path.Combine(Path.GetDirectoryName(dump), "ks_bask.json");
        if (File.Exists(bpath))
        {
            var bj = JsonDocument.Parse(File.ReadAllText(bpath)).RootElement;
            string[] est = { "niewolni", "chlopi", "wyrobnicy", "rzemieslnicy", "kupcy", "szlachta" };
            int nbad = 0, ntot = 0; double worst = 0; string wk = "";
            Action<JsonElement, bool> chk = (s, town) =>
            {
                string cul = s.GetProperty("cul").GetString();
                if (!bj.TryGetProperty(cul, out var cb)) cul = s.GetProperty("land").GetString();
                if (!bj.TryGetProperty(cul, out cb)) return;
                var bask = new KsCore.Basket[6];
                for (int e = 0; e < 6; e++)
                {
                    var be = cb.GetProperty("b")[e]; var b = new KsCore.Basket { DatesExtra = be.GetProperty("de").GetDouble() };
                    for (int g = 0; g < 13; g++) b.G[g] = be.GetProperty("g")[g].GetDouble();
                    for (int i = 0; i < 6; i++) b.Vs[i] = be.GetProperty("vs")[i].GetDouble();
                    bask[e] = b;
                }
                var ppl = new double[6];
                foreach (var kv in s.GetProperty("ppl").EnumerateObject()) { int e = Array.IndexOf(est, kv.Name); if (e >= 0) ppl[e] = kv.Value.GetDouble(); }
                var nv = new double[KsTab.NN];
                KsCore.NeedsOf(ppl, bask, cb.GetProperty("oil").GetBoolean(), cb.GetProperty("north").GetBoolean(), true, town, s.GetProperty("people").GetDouble(), nv);
                var want = new double[KsTab.NN];
                foreach (var kv in s.GetProperty("need0").EnumerateObject()) { int i; if (KsTab.NeedIndex.TryGetValue(kv.Name, out i)) want[i] = kv.Value.GetDouble(); }
                ntot++;
                bool b2 = false;
                for (int i = 0; i < KsTab.NN; i++) { double d = Math.Abs(nv[i] - want[i]) / Math.Max(1.0, Math.Abs(want[i])); if (d > worst) { worst = d; wk = s.GetProperty("id").GetString() + " " + KsTab.NeedKeys[i] + " " + nv[i] + " vs " + want[i]; } if (d > 1e-6) b2 = true; }
                if (b2) nbad++;
            };
            foreach (var d in root.GetProperty("dist").EnumerateArray()) if (d.GetProperty("type").GetString() != "town_fields") chk(d, false);
            foreach (var t in root.GetProperty("towns").EnumerateArray()) chk(t, true);
            Console.WriteLine("test potrzeb: osad " + ntot + ", roznych " + nbad + ", najwieksza roznica wzgledna " + worst.ToString("0.000000", Inv) + " (" + wk + ")");
        }

        // tryb "ziemia": bloki z pliku arm_ks_land.tsv jak w grze (20 blokow wsi gry, wioski poboczne z udzialem z modelu, pola mieszczan 20 blokow)
        string landPath = args.Length > 4 ? args[4] : null;
        if (landPath != null && File.Exists(landPath))
        {
            var rows = new Dictionary<string, List<string[]>>();
            foreach (var line in File.ReadAllLines(landPath))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var c = line.Split('	');
                string key = (c[0] == "T" ? "T|" : "V|") + c[1];
                if (!rows.TryGetValue(key, out var l)) { l = new List<string[]>(); rows[key] = l; }
                l.Add(c);
            }
            int okD = 0, noL = 0;
            foreach (var d in inp.Districts)
            {
                string key = d.TownFields ? "T|" + d.Id.Replace("#pola", "") : "V|" + d.Id;
                if (!rows.TryGetValue(key, out var l)) { noL++; continue; }
                var side = new Dictionary<string, double>(); double sideSum = 0;
                foreach (var b in d.Blocks) if (!b.Main) { side[b.Uid] = b.Share; sideSum += b.Share; }
                double main = Math.Max(0.0, 1.0 - sideSum);
                if (d.TownFields) main = 1.0;
                var nb = new List<KsBlock>();
                foreach (var c in l)
                {
                    var o = new KsTab.Land { Zone = c[4], Terr = c[5], Kind = c[6], Model = c[7], DSea = double.Parse(c[8], Inv), DRiver = double.Parse(c[9], Inv), DMount = double.Parse(c[10], Inv), Main = c[0] != "S" };
                    if (c[0] == "S")
                    {
                        if (!side.TryGetValue(c[2], out var sh) || !(sh > 0)) continue;
                        nb.Add(new KsBlock { Uid = c[2], Share = sh, Main = false, Start = c[12], O = o });
                    }
                    else
                    {
                        bool b0 = c[11] == "1";
                        if (b0 && (d.Type == "fisherman" || d.Type == "whaler" || d.Type == "walrus_hunter") && o.DSea <= 12) o.Kind = "coast";
                        nb.Add(new KsBlock { Uid = c[2], Share = main / 20.0, Main = true, Block0 = b0, Start = d.TownFields ? "garden" : d.Type, O = o });
                    }
                }
                double tot = 0; foreach (var b in nb) tot += b.Share;
                foreach (var b in nb) { b.Share /= tot; b.Area = d.Area * b.Share; }
                d.Blocks.Clear(); d.Blocks.AddRange(nb); okD++;
            }
            Console.WriteLine("tryb ziemi z pliku: okregow " + okD + ", bez wierszy " + noL);
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var R = KsCore.Solve(inp, null);
        Console.WriteLine("Solve: " + (R.Error ?? "OK") + " rund " + R.Rounds + " (pierwsza " + R.RoundsFirst + ", R8 " + R.R8Rounds + ", R8b " + R.R8bRounds + ") czas " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s; blokow " + R.NB + ", par " + R.NP);
        if (R.Error != null) return 1;
        Console.WriteLine("stol R8b: Westeros x" + R.Table[0].ToString("0.000", Inv) + ", Essos x" + R.Table[1].ToString("0.000", Inv) + "; ziemia pelna " + (100 * R.LandBind).ToString("0", Inv) + "%, renta srednia " + R.RentMean.ToString("0.0000", Inv) + " max " + R.RentMax.ToString("0.000", Inv));
        Console.WriteLine("niezbiezne: " + string.Join(", ", R.Open) + " | na granicy: " + string.Join(", ", R.Bounds));
        // porownanie z bilansem modelu
        var model = new Dictionary<string, string[]>();
        string[] hdr = null;
        if (bil != null && File.Exists(bil))
            foreach (var line in File.ReadAllLines(bil))
            {
                var c = line.Split('\t');
                if (hdr == null) { hdr = c; continue; }
                model[c[0]] = c;
            }
        Func<string, int> col = name => hdr == null ? -1 : Array.IndexOf(hdr, name);
        double maxDiff = 0;
        Console.WriteLine("towar | popyt t/d | C Westeros (model) | C Essos (model) | cena W (model) / E (model)");
        for (int g = 0; g < KsTab.NG; g++)
        {
            double dw = R.D[g] + R.D[KsTab.NG + g];
            if (dw <= 0) continue;
            double rw = R.D[g] > 0 ? R.S[g] / R.D[g] : double.NaN, re = R.D[KsTab.NG + g] > 0 ? R.S[KsTab.NG + g] / R.D[KsTab.NG + g] : double.NaN;
            string[] m; model.TryGetValue(KsTab.Goods[g], out m);
            string mw = m != null ? m[col("C_Westeros")] : "-", me = m != null ? m[col("C_Essos")] : "-";
            string pw = m != null ? m[col("cena_Westeros")] : "-", pe = m != null ? m[col("cena_Essos")] : "-";
            if (m != null)
            {
                double a, b;
                if (double.TryParse(mw, NumberStyles.Float, Inv, out a) && !double.IsNaN(rw)) maxDiff = Math.Max(maxDiff, Math.Abs(a - rw));
                if (double.TryParse(me, NumberStyles.Float, Inv, out b) && !double.IsNaN(re)) maxDiff = Math.Max(maxDiff, Math.Abs(b - re));
            }
            Console.WriteLine(KsTab.Goods[g] + " | " + (dw / 1000).ToString("0", Inv) + " | " + rw.ToString("0.000", Inv) + " (" + mw + ") | " + re.ToString("0.000", Inv) + " (" + me + ") | "
                              + R.P[g].ToString("0.00", Inv) + " (" + pw + ") / " + R.P[KsTab.NG + g].ToString("0.00", Inv) + " (" + pe + ")");
        }
        Console.WriteLine("najwieksza roznica stosunku podaz/popyt wobec modelu: " + maxDiff.ToString("0.000", Inv));
        if (first && File.Exists(Path.Combine(Path.GetDirectoryName(dump), "ks_first.json")))
        {
            var fj = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(dump), "ks_first.json"))).RootElement;
            var mks = fj.GetProperty("MKS"); int pyW = mks[0].GetString() == "Westeros" ? 0 : 1, pyE = 1 - pyW;
            double md = 0, mp = 0, mdd = 0; string wd = "", wp = "";
            for (int g = 0; g < KsTab.NG; g++)
            {
                for (int m = 0; m < 2; m++)
                {
                    int py = m == 0 ? pyW : pyE;
                    double dm = fj.GetProperty("Dm0")[py][g].GetDouble(), sb = fj.GetProperty("SB")[py][g].GetDouble(), pb = fj.GetProperty("pB")[py][g].GetDouble();
                    double myD = R.D0[m * KsTab.NG + g], myS = R.S[m * KsTab.NG + g], myP = R.P[m * KsTab.NG + g];
                    mdd = Math.Max(mdd, Math.Abs(myD - dm) / Math.Max(1, dm));
                    if (dm > 1) { double a = Math.Abs(myS / Math.Max(1, myD) - sb / Math.Max(1, dm)); if (a > md) { md = a; wd = KsTab.Goods[g] + m; } }
                    double b = Math.Abs(myP - pb) / Math.Max(1e-9, pb); if (b > mp) { mp = b; wp = KsTab.Goods[g] + m + " " + myP + " vs " + pb; }
                }
            }
            Console.WriteLine("PIERWSZA ROWNOWAGA wobec Pythona: popyt rel max " + mdd.ToString("0.000000", Inv) + ", S/D abs max " + md.ToString("0.0000", Inv) + " (" + wd + "), cena rel max " + mp.ToString("0.0000", Inv) + " (" + wp + ")");
        }
        // test 3: rachunek dostawy (KsDeliver) z wyniku Planu - suma okregow = podaz rozwiazania; to, co okregi zjedza same; na targ w sztukach gry wobec dzisiejszej produkcji
        {
            var bjp = Path.Combine(Path.GetDirectoryName(dump), "ks_bask.json");
            var bj = File.Exists(bjp) ? JsonDocument.Parse(File.ReadAllText(bjp)).RootElement : default(JsonElement);
            // udzial z wlasnego pola wsi (DailyLife.Mk dla wsi): MK_PEAS / MK_GENT / niewolni 5% (sol 100%), dom zawsze z domu
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
            var sumP = new double[KsDeliver.ItemCount]; var sumOwn = new double[KsDeliver.ItemCount]; var sumTf = new double[KsDeliver.ItemCount];
            var prod = new double[KsDeliver.ItemCount]; var own = new double[KsDeliver.ItemCount];
            double kcOwn = 0, kcNeed = 0;
            var dj = new Dictionary<string, JsonElement>();
            foreach (var d in root.GetProperty("dist").EnumerateArray()) dj[d.GetProperty("id").GetString()] = d;
            foreach (var d in inp.Districts)
            {
                var kparts = new KsPart[d.Blocks.Count];
                for (int i = 0; i < kparts.Length; i++) { kparts[i] = new KsPart { K = d.Blocks[i] }; KsDeliver.FromPlan(R, d.Blocks[i].Index, kparts[i]); }
                KsDeliver.Produce(d, kparts, prod);
                for (int i = 0; i < KsDeliver.ItemCount; i++) { if (d.TownFields) sumTf[i] += prod[i]; else sumP[i] += prod[i]; }
                if (d.TownFields || bj.ValueKind != JsonValueKind.Object) continue;
                var de = dj[d.Id];
                string cul = de.GetProperty("cul").GetString();
                if (!bj.TryGetProperty(cul, out var cb)) cul = de.GetProperty("land").GetString();
                if (!bj.TryGetProperty(cul, out cb)) continue;
                var bask = new KsCore.Basket[6];
                for (int e = 0; e < 6; e++)
                {
                    var be = cb.GetProperty("b")[e]; var b = new KsCore.Basket { DatesExtra = be.GetProperty("de").GetDouble() };
                    for (int g = 0; g < 13; g++) b.G[g] = be.GetProperty("g")[g].GetDouble();
                    for (int i = 0; i < 6; i++) b.Vs[i] = be.GetProperty("vs")[i].GetDouble();
                    bask[e] = b;
                }
                var ppl = new double[6];
                foreach (var kv in de.GetProperty("ppl").EnumerateObject()) { int e = Array.IndexOf(est, kv.Name); if (e >= 0) ppl[e] = kv.Value.GetDouble(); }
                var on = new double[KsTab.NN];
                KsCore.NeedsOfKeep(ppl, bask, keep, cb.GetProperty("oil").GetBoolean(), d.People, on);
                double[] dn; R.DistNeed.TryGetValue(d.Id, out dn);
                for (int k = 0; k < KsTab.NN; k++) if (dn != null && d.Need0[k] > 1e-9) on[k] *= Math.Max(0.25, Math.Min(4.0, dn[k] / d.Need0[k]));
                kcOwn += KsDeliver.Own(prod, on, own);
                kcNeed += KsDeliver.KcalNeed(d.Need0);
                for (int i = 0; i < KsDeliver.ItemCount; i++) sumOwn[i] += own[i];
            }
            Console.WriteLine("DOSTAWA (kg/d -> szt. gry/d przy sigma 37): towar | produkcja wsi t/d | zjedzone w okregach t/d | na targ wsie szt./d | pola mieszczan szt./d | podaz rozwiazania t/d | dzis szt./d (ksiega)");
            for (int i = 0; i < KsDeliver.ItemCount; i++)
            {
                string id = KsDeliver.ItemIds[i];
                double kg = inp.Items.TryGetValue(id, out var it) ? Math.Max(0.01, it.Kg) : 10;
                int g = Array.IndexOf(KsTab.Goods, id);
                double sol = g >= 0 ? R.S[g] + R.S[KsTab.NG + g] : double.NaN;
                double today = root.GetProperty("gd").TryGetProperty(id, out var gde) && gde.TryGetProperty("prod", out var pe) && pe.ValueKind == JsonValueKind.Number ? pe.GetDouble() : double.NaN;
                Console.WriteLine(id + " | " + (sumP[i] / 1000).ToString("0.0", Inv) + " | " + (sumOwn[i] / 1000).ToString("0.0", Inv) + " | " + ((sumP[i] - sumOwn[i]) / (37 * kg)).ToString("0", Inv)
                                  + " | " + (sumTf[i] / (37 * kg)).ToString("0", Inv) + " | " + (sol / 1000).ToString("0.0", Inv) + " | " + today.ToString("0", Inv));
            }
            Console.WriteLine("kcal z wlasnego pola i domu / pelna potrzeba wsi: " + (100 * kcOwn / Math.Max(1, kcNeed)).ToString("0.0", Inv) + "%");
        }
        Zc4Test.Run(inp, R, root, Path.GetDirectoryName(dump));
        Console.WriteLine("rece: dom " + (R.HomeLabour / 1e6).ToString("0.00", Inv) + " mln, zboze wlasne " + (R.OwnGrainLabour / 1e6).ToString("0.00", Inv) + " mln, pola mieszczan " + (R.TownFieldHands / 1e6).ToString("0.00", Inv) + " mln");
        var parts = new List<string>();
        for (int c = 0; c < KsTab.NC; c++) if (R.CraftLabour[c] > 1000) parts.Add(KsTab.Crafts[c] + " " + (R.CraftLabour[c] / 1e6).ToString("0.00", Inv));
        Console.WriteLine("zajecia (mln): " + string.Join(", ", parts));
        parts.Clear();
        for (int c = 0; c < KsTab.NC; c++) if (R.RegLabour[c] > 0) parts.Add(KsTab.Crafts[c] + " " + R.RegLabour[c].ToString("0", Inv));
        Console.WriteLine("regale (rece): " + string.Join(", ", parts));
        return 0;
    }
}
