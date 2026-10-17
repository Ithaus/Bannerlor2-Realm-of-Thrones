import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'CatchK.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, a[:70]
    s = s.replace(a, b)
rep('public double HwV, V0, G, SurL, SurW, Alpha, Ret; public bool[] Ok;', 'public double HwV, V0, G, SurL, SurW, Alpha, Ret; public double[] RetI = new double[KsDeliver.ItemCount]; public bool[] Ok;')
rep('''                    if (v.Ret > 0) { salesB = Math.Max(0.0, salesB - v.Ret * bi[k * NI + KsDeliver.IGrain]); sales = Math.Max(0.0, sales - v.Ret * pi[k * NI + KsDeliver.IGrain]); }''',
'''                    for (int i = 0; i < NI; i++) if (v.RetI[i] > 0) { salesB = Math.Max(0.0, salesB - v.RetI[i] * bi[k * NI + i]); sales = Math.Max(0.0, sales - v.RetI[i] * pi[k * NI + i]); }''')
rep('''                    double rg = 0;
                    if (KI.Retain)
                    {
                        double covNow = raw > 0 ? Math.Min(1.0, hwc * v0 / raw) : 0.0, ukc = 0, okc = 0;
                        for (int z = 0; z < NZ; z++) { double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue; ukc += u[z] * kc; if (v.Ok[z]) okc += u[z] * kc; }
                        double unc = Math.Max(0.0, ukc - covNow * (okc + subK));
                        double avail = Math.Max(0.0, prod[KsDeliver.IGrain] - own[KsDeliver.IGrain]);
                        rg = Math.Min(unc / 3300.0, avail);
                    }
                    v.Ret = last ? v.Ret : 0.5 * (v.Ret + rg);
                    eat[k * NG + KsTab.GGrain] += v.Ret;''',
'''                    if (KI.Retain && !last)
                    {
                        double covNow = raw > 0 ? Math.Min(1.0, hwc * v0 / raw) : 0.0, ukc = 0, okc = 0;
                        for (int z = 0; z < NZ; z++) { double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue; ukc += u[z] * kc; if (v.Ok[z]) okc += u[z] * kc; }
                        double unc = Math.Max(0.0, ukc - covNow * (okc + subK));
                        // najtansze za kcal (ceny cienia targu) wlasne jedzenie, ktore idzie na targ
                        var cand = new List<KeyValuePair<double, int>>();
                        for (int i = 0; i < NI; i++) { double kc = KsDeliver.ItemKcal[i]; if (!(kc > 0)) continue; double av = prod[i] - own[i]; if (av <= 1e-9) continue; cand.Add(new KeyValuePair<double, int>(si[k * NI + i] / kc, i)); }
                        cand.Sort((a1, b1) => a1.Key.CompareTo(b1.Key));
                        var nr = new double[NI];
                        foreach (var c1 in cand)
                        {
                            if (unc <= 0) break;
                            int i = c1.Value; double av = Math.Max(0.0, prod[i] - own[i]), kc = KsDeliver.ItemKcal[i];
                            double tk = Math.Min(av, unc / kc); nr[i] = tk; unc -= tk * kc;
                        }
                        for (int i = 0; i < NI; i++) v.RetI[i] = 0.5 * (v.RetI[i] + nr[i]);
                    }
                    v.Ret = 0;
                    for (int i = 0; i < NI; i++)
                    {
                        if (!(v.RetI[i] > 0)) continue;
                        v.Ret += v.RetI[i] * KsDeliver.ItemKcal[i] / 3300.0;
                        int gk = KsDeliver.ItemGood[i]; if (gk >= 0) eat[k * NG + gk] += v.RetI[i] * (i == KsDeliver.ICow || i == KsDeliver.IHog || i == KsDeliver.ISheep ? KsTab.MeatOfLive : 1.0);
                    }''')
rep('''                        d.Need[KsTab.NG_] += v.Ret;''', '''                        for (int i = 0; i < NI; i++)
                        {
                            double r1 = v.RetI[i]; if (!(r1 > 0)) continue;
                            int key = NeedKeyOfItem(i); if (key < 0) continue;
                            d.Need[key] += r1 * (i == KsDeliver.ICow || i == KsDeliver.IHog || i == KsDeliver.ISheep ? KsTab.MeatOfLive : 1.0);
                        }''')
rep('''        private static int FirstOf(KInp KI, int m)''', '''        private static int NeedKeyOfItem(int i)
        {
            switch (i)
            {
                case KsDeliver.IGrain: return KsTab.NG_; case KsDeliver.IFish: return KsTab.NFish; case KsDeliver.ICheese: return KsTab.NCheese; case KsDeliver.IButter: return KsTab.NButter;
                case KsDeliver.IEgg: return KsTab.NEgg; case KsDeliver.IApple: return KsTab.NApple; case KsDeliver.ICarrot: return KsTab.NCarrot; case KsDeliver.IOrange: return KsTab.NOrange;
                case KsDeliver.IGrape: return KsTab.NGrape; case KsDeliver.IOlives: return KsTab.NOlives; case KsDeliver.IDate: return KsTab.NDate; case KsDeliver.IHoney: return KsTab.NHoney;
                case KsDeliver.IMeat: case KsDeliver.IWhaleMeat: case KsDeliver.ICow: case KsDeliver.IHog: case KsDeliver.ISheep: return KsTab.NMeat;
                default: return -1;
            }
        }

        private static int FirstOf(KInp KI, int m)''')
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
