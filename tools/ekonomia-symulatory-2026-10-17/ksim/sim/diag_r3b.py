import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(R.DEF)
s = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, sys.argv[1] if len(sys.argv) > 1 else 'on_an1.csv'), plan_off=os.path.join(R.H3OUT, 'off.csv'))
orig = s.village_buy
acc = {'want': 0, 'got': 0, 'miss': 0, 'ungold': 0, 'zlw': 0.0, 'zlg': 0.0, 'zlp': 0.0, 'p2got': 0, 'n': 0, 'gold': 0.0, 'lim': 0.0, 'resgs': 0.0, 'budget7': 0.0}
while s.day < o['D'] + 30:
    if s.day == o['D'] + 25:
        s.reset_counters()
    s.step()
print('p1 want/miss/ungold', s.c_p1, 'zlg1', s.c_zlg1, 'zlp', s.c_zlp, 'zlg', s.c_zlg)
print('kb', [s.c_kg[m] / s.c_kw[m] for m in (0, 1)], 'kw', s.c_kw)
for m in (0, 1):
    ks = [k for k, v in enumerate(s.Vl) if v['mk'] == m]
    import statistics
    g = sorted(s.gold[k] for k in ks); b7 = sorted(7 * s.budget_v[k] for k in ks)
    print('mk', m, 'purse q25/med/q75', g[len(g)//4], g[len(g)//2], g[3*len(g)//4], ' 7xbudget q25/med/q75', b7[len(b7)//4], b7[len(b7)//2], b7[3*len(b7)//4])
for m in (0, 1):
    print('mk', m, ' | '.join('%s want %d got %d miss %d broke %d res %d' % (s.G[i], *s.c_g1[m][i]) for i in range(s.NG) if s.c_g1[m][i][0] > 0))
