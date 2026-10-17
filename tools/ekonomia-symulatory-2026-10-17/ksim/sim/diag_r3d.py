# controller trajectory and weekly market balance (port)
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(R.DEF)
for a in sys.argv[2:]:
    k, v = a.split('='); o[k] = float(v) if '.' in v else int(v)
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, 1, None, None, 'split')
s = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, sys.argv[1]), plan_off=os.path.join(R.H3OUT, 'off.csv'), base_flow=b['_flow'])
goods = ['grain', 'fish', 'cheese', 'olives', 'salt']
last = None
while s.day < o['D'] + 56:
    p0 = [[x for x in r] for r in s.c_prod]; c0 = [[x for x in r] for r in s.c_cons]
    s.step()
    if (s.day - o['D']) % 7 == 0:
        line = 'day D+%d' % (s.day - o['D'])
        for m in (0, 1):
            for g in goods:
                i = s.GI[g]
                stock = sum(s.shelf[t][i] for t in range(s.NT) if s.T[t]['mk'] == m)
                tgt = sum(s.Ts[m][i] * s.dp[t][i] for t in range(s.NT) if s.T[t]['mk'] == m)
                line += ' | %s%s P/C %.2f st/tg %.2f mf %.3f c28 %.0f u28 %.0f P0 %.0f' % ('WE'[m], g, (s.c_prod[m][i] - p0[m][i]) / max(1, s.c_cons[m][i] - c0[m][i]), stock / max(1, tgt), s.mf[m][i], s.c28[m][i], s.u28[m][i], s.P0[m][i])
        print(line)
