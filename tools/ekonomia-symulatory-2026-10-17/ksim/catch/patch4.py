import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'CatchK.cs'); s = open(p, encoding='utf-8').read()
a = '''                            double pk = ZlPerKg(inp, KsTab.Goods[g], ShadowIndexK(KsTab.Goods[g], st.P, k));
                            double dmk = Math.Max(0.0, st.Dk[i] - eatPrev[i]), dk = st.Dk[i] + (margin - 1.0) * dmk;
                            e += (st.Snet[i] - dk) * pk; surL[i] = st.Snet[i] > dk;'''
b = '''                            double pk = ZlPerKg(inp, KsTab.Goods[g], KI.ExcStart ? StartIndexK(KsTab.Goods[g], st.P, k) : ShadowIndexK(KsTab.Goods[g], st.P, k));
                            double dmk = Math.Max(0.0, st.Dk[i] - eatPrev[i]), dk = st.Dk[i] + (margin - 1.0) * dmk;
                            e += (st.Snet[i] - dk) * pk; surL[i] = st.Snet[i] > dk;'''
assert s.count(a) == 1; s = s.replace(a, b)
a = '''        public bool GMarket = true;'''
b = '''        public bool ExcStart;                            // nadwyzka targow w cenach startu (0.7-1.5) zamiast cen cienia
        public bool GMarket = true;'''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
p = os.path.join(HERE, 'ProgramK.cs'); s = open(p, encoding='utf-8').read()
a = '''        if (Environment.GetEnvironmentVariable("KS_GTARG") == "1") KI.GMarket = false;'''
b = a + '''
        if (Environment.GetEnvironmentVariable("KS_EXCSTART") == "1") KI.ExcStart = true;'''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
