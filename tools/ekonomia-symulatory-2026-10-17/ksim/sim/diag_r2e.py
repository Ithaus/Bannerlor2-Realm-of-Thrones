# round 2: controller trajectory (P4) and where the market's stock of a good lies (war lands vs others), weekly
import json, os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R, sim as S
W = json.load(open(os.path.join(HERE, 'world_r2.json'), encoding='utf-8'))
o = dict(R.DEF); o.update(kwfix=1, candx=1, res_short=0.0)
for a in sys.argv[2:]:
    k, v = a.split('='); o[k] = float(v) if '.' in v else int(v)
goods = sys.argv[1].split(',') if len(sys.argv) > 1 else ['grain']
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, 1, None, None, 'A')
sim = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, 's1_on.csv'), plan_off=os.path.join(R.H3OUT, 'off.csv'), init='A', base_flow=b['_flow'])
D = o['D']
while sim.day < D + 62:
    sim.step()
    if (sim.day - D) % 7 == 0 and sim.day >= D:
        for g in goods:
            i = sim.GI[g]
            for m in (0, 1):
                ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
                war = [t for t in ts if sim.T[t]['kingdom'] in S.WAR_K]
                H = sum(max(0, sim.shelf[t][i]) for t in ts); Hw = sum(max(0, sim.shelf[t][i]) for t in war)
                hs = sum(sim.Ts[m][i] * sim.dp[t][i] for t in ts)
                short = sum(1 for t in ts if sim.dp[t][i] > 0 and sim.shelf[t][i] < 0.5 * sim.dp[t][i])
                tr = sum(c[4] for c in sim.ksh.carts if c[3] == R.RAW14.index(g) and sim.T[c[2]]['mk'] == m) if g in R.RAW14 else 0
                print('D+%2d %s %-6s mf %.3f C28 %7.0f U28 %6.0f P0 %7.0f | shelves %8.0f (war lands %7.0f) target %8.0f transit %6.0f | towns < 0.5 d %d' % (
                    sim.day - D, 'WE'[m], g, sim.mf[m][i], sim.c28[m][i], sim.u28[m][i], sim.P0[m][i], H, Hw, hs, tr, short))
