import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'CatchK.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, a[:70]
    s = s.replace(a, b)
rep('public double HwV, V0, G, SurL, SurW, Alpha; public bool[] Ok; public int Sub = -1; }', 'public double HwV, V0, G, SurL, SurW, Alpha, Ret; public bool[] Ok; public int Sub = -1; }')
rep('        public double[] VSubKcal = new double[2];        // kcal zamiennika (krok 8, Subst)', '        public double[] VSubKcal = new double[2];        // kcal zamiennika (krok 8, Subst)\n        public double[] VRetKcal = new double[2];        // kcal wlasnego zboza zatrzymanego zamiast sprzedazy (krok 8, Retain)')
rep('Array.Clear(O.VSubKcal, 0, 2);', 'Array.Clear(O.VSubKcal, 0, 2); Array.Clear(O.VRetKcal, 0, 2);')
rep('''                    double budget = v.Alpha * salesB;''', '''                    if (v.Ret > 0) { salesB = Math.Max(0.0, salesB - v.Ret * bi[k * NI + KsDeliver.IGrain]); sales = Math.Max(0.0, sales - v.Ret * pi[k * NI + KsDeliver.IGrain]); }
                    double budget = v.Alpha * salesB;''')
rep('''                    v.V0 = v0;''', '''                    v.V0 = v0;
                    // Retain: kcal bez pokrycia (po zakupach z budzetu i pracy domowej) - z wlasnego ziarna, ktore szloby na targ (nie sprzedaje, je); najwyzej cale ziarno na targ
                    double rg = 0;
                    if (KI.Retain)
                    {
                        double covNow = raw > 0 ? Math.Min(1.0, hwc * v0 / raw) : 0.0, ukc = 0, okc = 0;
                        for (int z = 0; z < NZ; z++) { double kc = KsBasket.Kcal[z]; if (!(kc > 0)) continue; ukc += u[z] * kc; if (v.Ok[z]) okc += u[z] * kc; }
                        double unc = Math.Max(0.0, ukc - covNow * (okc + subK));
                        double avail = Math.Max(0.0, prod[KsDeliver.IGrain] - own[KsDeliver.IGrain]);
                        rg = Math.Min(unc / 3300.0, avail);
                    }
                    v.Ret = last ? v.Ret : 0.5 * (v.Ret + rg);
                    eat[k * NG + KsTab.GGrain] += v.Ret;''')
rep('''                        hkc = (hkc + subK) * cov;''', '''                        hkc = (hkc + subK) * cov + v.Ret * 3300.0;
                        O.VRetKcal[m] += v.Ret * 3300.0;''')
rep('''                        UnpaidToNeed(d.Need, u, false);''', '''                        UnpaidToNeed(d.Need, u, false);
                        d.Need[KsTab.NG_] += v.Ret;''')
rep('''        public bool ExcStart;''', '''        public bool Retain;                              // krok 8: kcal bez pokrycia z wlasnego ziarna zatrzymanego (nie sprzedanego)
        public bool ExcStart;''')
open(p, 'w', encoding='utf-8', newline='\n').write(s)
p = os.path.join(HERE, 'ProgramK.cs'); s = open(p, encoding='utf-8').read()
a = '''        if (Environment.GetEnvironmentVariable("KS_EXCSTART") == "1") KI.ExcStart = true;'''
assert s.count(a) == 1
s = s.replace(a, a + '''
        if (Environment.GetEnvironmentVariable("KS_RETAIN") == "1") KI.Retain = true;''')
a = '''"%), BEZ POKRYCIA " + (100 * (M.VUnpaidKcal[m] - M.VHomeKcal[m]) / fk).ToString("0.0", Inv)'''
assert s.count(a) == 1
s = s.replace(a, '''"%, zatrzymane ziarno " + (100 * O.VRetKcal[m] / fk).ToString("0.0", Inv) + "%), BEZ POKRYCIA " + (100 * (M.VUnpaidKcal[m] - M.VHomeKcal[m]) / fk).ToString("0.0", Inv)''')
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
