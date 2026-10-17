import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['_vin'] = None
for a in sys.argv[2:]:
    k, v = a.split('='); o[k] = float(v)
if o.get('alphanet'): o['_vin'], o['_osh'] = M.prerun_vin(W, o, 'start', 1, 109140, 109167, 109118)
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
for t in range(sim.NT):
    m = sim.T[t]['mk']
    for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
K = sim.trade_mod; i = sim.GI['grain']
q = [t for t in range(sim.NT) if sim.T[t]['name'] == sys.argv[1]][0]
while sim.day < 109160:
    arr = sum(n for (a, s, t, ii, n) in K.ship if t == q and ii == i and a <= sim.day)
    sh0 = sim.shelf[q][i]; inc0 = K.inc[q][i]
    sim.step()
    if sim.day > 109135:
        print(sim.day, 'shelf %6.0f -> %6.0f | cart arrivals %6.0f | inc %6.0f | offema %5.0f D %5.0f kx %.2f idx %.2f' % (sh0, sim.shelf[q][i], arr, K.inc[q][i], sim.offema[q][i], sim.dterm(q, i), K.kx[q][i], sim.idx[q][sim.gcat[i]]))
