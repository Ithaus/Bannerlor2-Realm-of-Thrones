import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'CatchK.cs'); s = open(p, encoding='utf-8').read()
a = '''            var inp = x.In; int NC = KsTab.NC, NG = KsTab.NG;
            var Rm = new double[K * NC];
            for (int k = 0; k < K; k++)
                for (int c = 0; c < NC; c++)
                {
                    double s = 0; int o = c * NG, pm = k * NG;
                    for (int g = 0; g < NG; g++) { double v = x.MV[o + g]; if (v != 0) s += p[pm + g] * v; }
                    Rm[k * NC + c] = s;
                }'''
b = '''            var inp = x.In; int NC = KsTab.NC, NG = KsTab.NG;
            if (_mvG == null || _mvRef != x.MV)
            {
                _mvRef = x.MV; _mvG = new int[NC][]; _mvV = new double[NC][];
                for (int c = 0; c < NC; c++) { var lg = new List<int>(); var lv = new List<double>(); for (int g = 0; g < NG; g++) { double v = x.MV[c * NG + g]; if (v != 0) { lg.Add(g); lv.Add(v); } } _mvG[c] = lg.ToArray(); _mvV[c] = lv.ToArray(); }
            }
            if (_rm == null || _rm.Length != K * NC) _rm = new double[K * NC];
            var Rm = _rm;
            for (int k = 0; k < K; k++)
            {
                int pm = k * NG;
                for (int c = 0; c < NC; c++)
                {
                    double s = 0; var gg = _mvG[c]; var vv = _mvV[c];
                    for (int q = 0; q < gg.Length; q++) s += p[pm + gg[q]] * vv[q];
                    Rm[k * NC + c] = s;
                }
            }'''
assert s.count(a) == 1; s = s.replace(a, b)
a = '''        private static void SupplyK('''
b = '''        [ThreadStatic] private static int[][] _mvG; [ThreadStatic] private static double[][] _mvV; [ThreadStatic] private static double[] _mvRef, _rm;

        private static void SupplyK('''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
