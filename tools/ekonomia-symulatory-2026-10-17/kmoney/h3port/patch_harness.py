import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'Program.cs')
s = open(p, encoding='utf-8').read()
NL = chr(92) + 'n'   # backslash-n inside C# string literals

anchor = '        var dbg = new Dictionary<string, List<string>>();'
assert anchor in s
add = '''        // h3port: alfa okregow z pliku (id;alfa) - alfa netto z symulatora; czynnik popytu targu (g:m:v,... albo *:v)
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
'''
s = s.replace(anchor, add + anchor, 1)

a3 = '            var war = new HashSet<string> { "Dothraki Horde", "Free Folk" };'
assert a3 in s
s = s.replace(a3, a3 + '''
            Dictionary<string, double> goldMap = null; string gp = Environment.GetEnvironmentVariable("KS_GOLD");
            if (!string.IsNullOrEmpty(gp) && File.Exists(gp))
            {
                goldMap = new Dictionary<string, double>();
                foreach (var line in File.ReadAllLines(gp)) { var f = line.Split(';'); double gv; if (f.Length >= 2 && double.TryParse(f[1], NumberStyles.Float, Inv, out gv)) goldMap[f[0]] = gv; }
                Console.WriteLine("kasy 28 dob z pliku " + gp + ": " + goldMap.Count);
            }''', 1)
a2 = '                inp.TownGold28[a2] = 150000;'
assert a2 in s
s = s.replace(a2, a2 + '''
                if (goldMap != null) { double gv; if (goldMap.TryGetValue(inp.Towns[a2].Id, out gv)) inp.TownGold28[a2] = gv; }''', 1)

a4 = '        MoneyIdentity(inp, R);\n        return 0;'
assert a4 in s
s = s.replace(a4, '''        MoneyIdentity(inp, R);
        string outp = Environment.GetEnvironmentVariable("KS_OUT");
        if (!string.IsNullOrEmpty(outp)) Dump(inp, R, outp);
        return 0;''', 1)

a5 = '    /// <summary>Tozsamosc targu z krokiem 8'
assert a5 in s
dump = '''    /// <summary>h3port: na okreg - praca domowa, kcal domu (DistHomeKcal), czesc kcal zaplacona, Plan bez pokrycia, na targ kg/d wedlug towaru dostawy, czesc zaplacona koszyka;
    /// na miasto - czesc zaplacona koszyka miasta. Ten sam rachunek co MoneyStep / ComputeDistrict (Produce, NeedsOfKeep x lepszy stol, Own).</summary>
    static void Dump(KsInput inp, KsResult R, string path)
    {
        int NI = KsDeliver.ItemCount, NZ = KsBasket.N;
        var sb = new System.Text.StringBuilder(1 << 20);
        sb.Append("D;id;mk;tf;hw;homekcal;pk;unc;fullkcal");
        foreach (var id in KsDeliver.ItemIds) sb.Append(";mk_").Append(id);
        foreach (var id in KsBasket.Ids) sb.Append(";paid_").Append(id);
        sb.Append('@NL@');
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
            sb.Append('@NL@');
        }
        foreach (var t in inp.Towns)
        {
            float[] pz; R.TownPaid.TryGetValue(t.Id, out pz);
            sb.Append("T;").Append(t.Id).Append(';').Append(t.Market);
            for (int z = 0; z < NZ; z++) sb.Append(';').Append(pz != null ? pz[z].ToString("0.#####", Inv) : "1");
            sb.Append('@NL@');
        }
        if (R.Grad != null) foreach (var kv in R.Grad) { sb.Append("G;").Append(kv.Key); foreach (int g in KsCore.Food) sb.Append(';').Append(KsTab.Goods[g]).Append('=').Append(kv.Value[g].ToString("0.###", Inv)); sb.Append('@NL@'); }
        foreach (var k in R.Kraina) sb.Append("K;").Append(k).Append('@NL@');
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine("zrzut okregow: " + path);
    }

'''.replace('@NL@', NL)
s = s.replace(a5, dump + a5, 1)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
