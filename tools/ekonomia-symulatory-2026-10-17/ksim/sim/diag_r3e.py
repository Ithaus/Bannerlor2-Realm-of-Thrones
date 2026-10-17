# per-town state of one good at day D+N: shelf, paid D-term, offtake, kx, incoming, index, village want; KS-H candidate sources
import json, os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(R.DEF); o['fix1'] = 1
g = sys.argv[2] if len(sys.argv) > 2 else 'salt'; mk = int(sys.argv[3]) if len(sys.argv) > 3 else 0; dd = int(sys.argv[4]) if len(sys.argv) > 4 else 30
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, 1, None, None, 'split')
s = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, sys.argv[1]), plan_off=os.path.join(R.H3OUT, 'off.csv'), base_flow=b['_flow'])
while s.day < o['D'] + dd: s.step()
i = s.GI[g]; k = R.RAW14.index(g); ks = s.ksh
T = s.Ts[mk][i]
rows = []
for t in range(s.NT):
    if s.T[t]['mk'] != mk: continue
    vw = sum(s.Vl[v]['bask'][i] * s.Vl[v]['paid'][i] for v in s._vil_of[t])
    c = ks.cand[t]
    best = None
    for src in c:
        L = ks.days[src][t]
        tau = 0.005 if ks.sea[src][t] else 0.01
        cost = s.idx[src][s.gcat[i]] * (1 + tau * L)
        sur = s.shelf[src][i] - 1.25 * T * max(s.dp[src][i], s.off7[src][i]) * ks.kx[src][k] - 5
        if best is None or cost < best[0]: best = (cost, s.T[src]['name'], round(sur), L)
    rows.append((s.shelf[t][i] / max(0.1, s.dp[t][i]), s.T[t]['name'], round(s.shelf[t][i]), round(s.dp[t][i], 1), round(s.off7[t][i], 1), round(ks.kx[t][k], 2), ks.inc[t][k], round(s.idx[t][s.gcat[i]], 2), round(vw, 1), best, round(s.town_gold(t) - s.town_reserve(t))))
rows.sort()
print('%s market %d day D+%d T* %.1f' % (g, mk, dd, T))
for r in rows[:25]: print('  days %5.1f %-20s shelf %5d Dp %6.1f off7 %6.1f kx %.2f inc %4d idx %.2f vilpaid %6.1f best %s margin %d' % r)
print('  ...'); 
for r in rows[-6:]: print('  days %5.1f %-20s shelf %5d Dp %6.1f off7 %6.1f kx %.2f inc %4d idx %.2f vilpaid %6.1f best %s margin %d' % r)
