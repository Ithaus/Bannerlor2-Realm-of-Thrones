import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'CatchK.cs'); s = open(p, encoding='utf-8').read()
a = '''                        if (v.V0 > 0)
                        {
                            double eL = excK[k];'''
b = '''                        if (v.V0 > 0 && KI.GMarket)
                        {
                            double eT = excW[m] + excKm[m], sS = sumSW[m] + sumSLm[m];
                            if (eT > 0 && sS > 0) dG += 0.5 * eT * ((v.SurW + v.SurL) / sS) / v.V0;
                            else if (eT < 0 && sumHvM[m] > 0) dG += 0.5 * eT * HomeOf(v.D) / sumHvM[m];
                        }
                        else if (v.V0 > 0)
                        {
                            double eL = excK[k];'''
assert s.count(a) == 1; s = s.replace(a, b)
a = '''                    foreach (var v in vs) { int k = v.D.Ck, m = KI.Mk[k]; sumSL[k] += v.SurL; sumSW[m] += v.SurW; double h = HomeOf(v.D) * v.V0; sumHvK[k] += h; sumHvM[m] += h; }'''
b = '''                    foreach (var v in vs) { int k = v.D.Ck, m = KI.Mk[k]; sumSL[k] += v.SurL; sumSW[m] += v.SurW; double h = HomeOf(v.D) * v.V0; sumHvK[k] += h; sumHvM[m] += h; }
                    var excKm = new double[2]; var sumSLm = new double[2];
                    for (int k = 0; k < K; k++) { excKm[KI.Mk[k]] += excK[k]; sumSLm[KI.Mk[k]] += sumSL[k]; }
                    M.ExcessRel[0] = excKm[0]; M.ExcessRel[1] = excKm[1];'''
assert s.count(a) == 1; s = s.replace(a, b)
a = '''        public bool Subst;                               // krok 8: kcal niezaplacone spoza HomeMask robione z wlasnego zboza / jedzenia (zamiennik kcal)'''
b = a + '''
        public bool GMarket = true;                      // poprawka G na rynek (suma nadwyzek targow + towarow rynku), jak MoneyStep; false - G na targ'''
assert s.count(a) == 1; s = s.replace(a, b)
a = '''+ ", zaplacone " + Math.Round(M.VPaid[0]) + "/" + Math.Round(M.VPaid[1]) + ", nadwyzka rynku (towary rynku) " + Math.Round(excW[0]) + "/" + Math.Round(excW[1])'''
b = '''+ ", zaplacone " + Math.Round(M.VPaid[0]) + "/" + Math.Round(M.VPaid[1]) + ", nadwyzka rynku (towary rynku) " + Math.Round(excW[0]) + "/" + Math.Round(excW[1]) + ", targow " + Math.Round(M.ExcessRel[0]) + "/" + Math.Round(M.ExcessRel[1])'''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
p = os.path.join(HERE, 'ProgramK.cs'); s = open(p, encoding='utf-8').read()
a = '''        { var cs = Environment.GetEnvironmentVariable("KS_CAPSHARE"); if (!string.IsNullOrEmpty(cs)) KI.CapShare = double.Parse(cs, Inv); }'''
b = a + '''
        if (Environment.GetEnvironmentVariable("KS_GTARG") == "1") KI.GMarket = false;'''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
