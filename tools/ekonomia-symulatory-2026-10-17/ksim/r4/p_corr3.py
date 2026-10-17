import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Corridors.cs'
pairs = []
pairs.append(("""            x.ReachT = new Dictionary<string, bool[]>(StringComparer.Ordinal); x.ReachD = new Dictionary<string, bool[]>(StringComparer.Ordinal);
            R.Unreached.Clear();""",
"""            // recenzja runda 3 (drobne): podaz spoza tabeli (lodzie rybackie NavalDLC - ExoLand krainy) w podazy miasta wedlug ludzi okregow miasta (jak Plan rozklada lodzie);
            // pola mieszczan sa okregami miasta (byTown) - juz w podazy
            if (x.TownLand != null && inp.ExoLand.Count > 0)
                for (int t = 0; t < nt; t++)
                {
                    int l = x.TownLand[t]; if (l < 0) continue;
                    double[] exo; double lp;
                    if (!inp.ExoLand.TryGetValue(x.Lands[l], out exo) || exo == null || !x.LPop.TryGetValue(x.Lands[l], out lp) || !(lp > 0)) continue;
                    double pp = inp.Towns[t].People; if (byTown[t] != null) foreach (var d in byTown[t]) pp += d.People;
                    double sh = Math.Min(1.0, pp / lp);
                    for (int g = 0; g < NG; g++) if (PerishG[g] && exo[g] > 0) sup[t * NG + g] += exo[g] * sh;
                }
            x.ReachT = new Dictionary<string, bool[]>(StringComparer.Ordinal); x.ReachD = new Dictionary<string, bool[]>(StringComparer.Ordinal);
            R.Unreached.Clear();"""))
rep(P, pairs)
