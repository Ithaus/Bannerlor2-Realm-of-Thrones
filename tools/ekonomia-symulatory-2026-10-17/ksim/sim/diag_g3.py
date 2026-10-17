import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['_vin'] = None
for a in sys.argv[1:]:
    k, v = a.split('='); o[k] = float(v)
if o.get('alphanet'): o['_vin'], o['_osh'] = M.prerun_vin(W, o, 'start', 1, 109140, 109167, 109118)
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
for t in range(sim.NT):
    m = sim.T[t]['mk']
    for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
i = sim.GI['grain']; c = sim.gcat[i]; K = sim.trade_mod
acc = collections.defaultdict(float); offa = collections.defaultdict(float); n = 0; zero = collections.Counter()
while sim.day < 109167:
    sim.step()
    if sim.day > 109140:
        n += 1
        for t in range(sim.NT):
            d = sim.dterm(t, i)
            if d > 0:
                acc[t] += sim.shelf[t][i] / d; offa[t] += sim.offema[t][i]
                if sim.shelf[t][i] <= 6: zero[t] += 1
rows = sorted(((acc[t] / n, t) for t in acc), key=lambda x: x[0])
for days, t in rows[:30]:
    cands = sorted((K.days[s][t], sim.T[s]['name']) for s in range(sim.NT) if K.days[s][t] >= 0)[:2]
    print('%-20s %s shelf %.2f d (empty %2d of %d days) D %5.0f off %5.0f kx %.2f idx %.2f inc %6.0f near %s' % (sim.T[t]['name'], 'WE'[sim.T[t]['mk']], days, zero[t], n, sim.dterm(t, i), offa[t] / n, K.kx[t][i], sim.idx[t][c], K.inc[t][i], cands))
