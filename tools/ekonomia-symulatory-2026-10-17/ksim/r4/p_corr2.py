import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Corridors.cs'
pairs = []
pairs.append(("""            var sup = new double[nt * NG]; var dem = new double[nt * NG]; var tmp = new double[NG];
            double wpop = Math.Max(1.0, x.WPop);
            foreach (var d in x.D)
            {
                int t = d.CorrTown; if (t < 0 || t >= nt) continue;
                double[] a;
                if (prod.TryGetValue(d.Id, out a)) foreach (int g in Food) if (PerishG[g]) sup[t * NG + g] += a[g];
                Array.Clear(tmp, 0, NG);
                CivilGoods(d.Need, tmp);
                OneGoods(x, d.People / wpop, tmp);
                foreach (int g in Food) if (PerishG[g]) dem[t * NG + g] += tmp[g];
            }
            for (int t = 0; t < nt; t++)
            {
                var ts = inp.Towns[t];
                Array.Clear(tmp, 0, NG);
                CivilGoods(ts.Need, tmp);
                OneGoods(x, ts.People / wpop, tmp);
                foreach (int g in Food) if (PerishG[g]) dem[t * NG + g] += tmp[g];
            }""",
"""            var sup = new double[nt * NG]; var dem = new double[nt * NG]; var tmp = new double[NG];
            double wpop = Math.Max(1.0, x.WPop);
            // recenzja runda 3: popyt zaplacony (potrzeby po kroku 8) + odbior panow i dworow w miescie + reszta 1:1 wedlug ludzi (jak krainy)
            var rest = OneRest(x);
            foreach (var d in x.D)
            {
                int t = d.CorrTown; if (t < 0 || t >= nt) continue;
                double[] a;
                if (prod.TryGetValue(d.Id, out a)) foreach (int g in Food) if (PerishG[g]) sup[t * NG + g] += a[g];
                Array.Clear(tmp, 0, NG);
                CivilGoods(d.Need, tmp);
                foreach (int g in Food) if (PerishG[g]) dem[t * NG + g] += tmp[g] + rest[g] * d.People / wpop;
            }
            for (int t = 0; t < nt; t++)
            {
                var ts = inp.Towns[t];
                Array.Clear(tmp, 0, NG);
                CivilGoods(ts.Need, tmp);
                TownOneAdd(x, ts.Id, tmp);
                foreach (int g in Food) if (PerishG[g]) dem[t * NG + g] += tmp[g] + rest[g] * ts.People / wpop;
            }
            // recenzja runda 3 (wazne): jeden budzet kasy miasta celu na wszystkie psujace sie (udzial wedlug wartosci braku towaru), jak krainy
            var tsh = new double[nt * NG];
            for (int t = 0; t < nt; t++)
            {
                int mt = inp.Towns[t].Market == 0 ? 0 : 1; double tot = 0;
                foreach (int g in Food) { if (!PerishG[g]) continue; double e = dem[t * NG + g] - sup[t * NG + g]; if (e > 0) { tsh[t * NG + g] = e * ZlPerKg(inp, KsTab.Goods[g], StartIndex(KsTab.Goods[g], p, mt)); tot += tsh[t * NG + g]; } }
                foreach (int g in Food) if (PerishG[g]) tsh[t * NG + g] = tot > 0 ? tsh[t * NG + g] / tot : 0.0;
            }"""))
pairs.append(("""                    var ex = new double[n];
                    for (int i = 0; i < n; i++) ex[i] = sup[nodes[i] * NG + g] - dem[nodes[i] * NG + g];
                    double idx = StartIndex(KsTab.Goods[g], p, m), zl = ZlPerKg(inp, KsTab.Goods[g], idx);""",
"""                    var ex = new double[n]; var exM = new double[n];
                    for (int i = 0; i < n; i++) ex[i] = sup[nodes[i] * NG + g] - dem[nodes[i] * NG + g];
                    double idx = StartIndex(KsTab.Goods[g], p, m), zl = ZlPerKg(inp, KsTab.Goods[g], idx);
                    for (int i = 0; i < n; i++)
                    {
                        int tj = nodes[i];
                        double capN = inp.TownGold28 != null && tj < inp.TownGold28.Length && zl > 0 ? tsh[tj * NG + g] * CorrCofferDay * Math.Max(0.0, inp.TownGold28[tj]) / zl : double.PositiveInfinity;
                        exM[i] = ex[i] < 0 ? -Math.Min(-ex[i], capN) : ex[i];
                    }"""))
pairs.append(("""                            double c = inp.TownGold28 != null && tj < inp.TownGold28.Length && zl > 0 ? 0.5 * Math.Max(0.0, inp.TownGold28[tj]) / (zl * Math.Max(1.0, dd)) : 1e12;""",
"""                            double c = inp.TownGold28 != null && tj < inp.TownGold28.Length && zl > 0 ? tsh[tj * NG + g] * 0.5 * Math.Max(0.0, inp.TownGold28[tj]) / (zl * Math.Max(1.0, dd)) : 1e12;"""))
pairs.append(("""                    var flow = KsCorridors.Transport(n, ex, cost, cap);
                    var inF = new double[n]; var inT = new double[n]; var outF = new double[n];
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            double f = flow[i * n + j];
                            if (!(f > 1e-9)) continue;
                            int ti = nodes[i], tj = nodes[j];""",
"""                    var flow = KsCorridors.Transport(n, exM, cost, cap);
                    var inF = new double[n]; var inT = new double[n]; var outF = new double[n];
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            double f = flow[i * n + j];
                            if (!(f > 1e-9)) continue;
                            int ti = nodes[i], tj = nodes[j];"""))
rep(P, pairs)
