import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'ProgramK.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, a[:80]
    s = s.replace(a, b)
rep('''    static KsInput Build(string dump, int maxR)
    {''', '''    public static double[][] Keep()
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
    {''')
rep('''        var inp = new KsInput { RoundsFirst = maxR, RoundsR8 = Math.Min(maxR, 4000), RoundsR8b = Math.Min(maxR, 3000), SilkTownDirect = true, SilverTownDirect = true, Money = false, StopAfterFirst = true };''',
'''        bool money = Environment.GetEnvironmentVariable("KS_MONEY") == "1";
        var inp = new KsInput { RoundsFirst = maxR, RoundsR8 = Math.Min(maxR, 4000), RoundsR8b = Math.Min(maxR, 3000), SilkTownDirect = true, SilverTownDirect = true, Money = false, StopAfterFirst = true, Keep = Keep() };
        { var a0 = Environment.GetEnvironmentVariable("KS_ALPHA"); double av = 0.47; if (!string.IsNullOrEmpty(a0)) av = double.Parse(a0, Inv); inp.VilAlpha[0] = inp.VilAlpha[1] = av; }
        { var mg = Environment.GetEnvironmentVariable("KS_MARGIN"); if (!string.IsNullOrEmpty(mg)) inp.MarketMargin = double.Parse(mg, Inv); }
        { var r8 = Environment.GetEnvironmentVariable("KS_R8B"); if (!string.IsNullOrEmpty(r8)) inp.RoundsR8b = int.Parse(r8); }
        Func<JsonElement, JsonElement> cbOf = s =>
        {
            string cul = s.GetProperty("cul").GetString();
            if (!bj.TryGetProperty(cul, out var cb)) bj.TryGetProperty(s.GetProperty("land").GetString(), out cb);
            return cb;
        };''')
rep('''                kb.Block0 = kb.Uid.EndsWith("#main0", StringComparison.Ordinal);
                kd.Blocks.Add(kb);
            }
            inp.Districts.Add(kd);''', '''                kb.Block0 = kb.Uid.EndsWith("#main0", StringComparison.Ordinal);
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
            inp.Districts.Add(kd);''')
rep('''            needs(t.GetProperty("need0"), ks.Need0);
            inp.Towns.Add(ks);''', '''            needs(t.GetProperty("need0"), ks.Need0);
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
            inp.Towns.Add(ks);''')
rep('''        return inp;
    }

    static int Main''', '''        string bud = Environment.GetEnvironmentVariable("KS_BUDGETS");
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

    static int Main''')
rep('''        var KI = new KInp { K = K, Mk = new int[K], Id = new string[K], Name = new string[K], Garum = new bool[K], Mode1 = mode == 1, CapLong = capLong };''',
'''        var KI = new KInp { K = K, Mk = new int[K], Id = new string[K], Name = new string[K], Garum = new bool[K], Mode1 = mode == 1, CapLong = capLong,
                            Money = Environment.GetEnvironmentVariable("KS_MONEY") == "1", Subst = Environment.GetEnvironmentVariable("KS_SUBST") == "1" };
        { var cs = Environment.GetEnvironmentVariable("KS_CAPSHARE"); if (!string.IsNullOrEmpty(cs)) KI.CapShare = double.Parse(cs, Inv); }''')
rep('''        Report(inp, KI, O, outp);
        return 0;''', '''        Report(inp, KI, O, outp);
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
                                  + "%), BEZ POKRYCIA " + (100 * (M.VUnpaidKcal[m] - M.VHomeKcal[m]) / fk).ToString("0.0", Inv) + "% | nadwyzka rynku " + M.Excess[m].ToString("0", Inv));
                for (int c = 0; c < 3; c++) Console.WriteLine("   klasa " + "WRP"[c] + ": budzet " + M.TBudget[m * 3 + c].ToString("0", Inv) + ", koszyk " + M.TBasket[m * 3 + c].ToString("0", Inv) + ", zaplacony " + (100 * M.TPaid[m * 3 + c] / Math.Max(1, M.TBasket[m * 3 + c])).ToString("0.0", Inv) + "%");
            }
        }
        return 0;''')
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
