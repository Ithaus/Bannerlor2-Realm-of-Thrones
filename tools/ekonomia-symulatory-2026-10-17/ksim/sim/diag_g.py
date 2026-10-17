import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF)
for a in sys.argv[1:]:
    k, v = a.split('='); o[k] = float(v)
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
for t in range(sim.NT):
    m = sim.T[t]['mk']
    for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
while sim.day < 109140: sim.step()
sim.reset_counters()
while sim.day < 109167: sim.step()
nd = sim.c_days
for g in ('grain', 'fish', 'cheese', 'meat', 'olives', 'salt'):
    i = sim.GI[g]
    sinks = {k[0]: v / nd for k, v in sim.c_sg.items() if k[1] == i}
    for m in (0, 1):
        ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
        Q = sum(sim.Q[t][i] for t in ts); QV = sum(sim.QV[t][i] for t in ts); Der = sum(sim.der[t][i] for t in ts); One = sum(sim.one[t][i] for t in ts)
        want = sum(sim.c_vg.get((i, 'want'), 0) for _ in [0]) / nd
        print(g, 'WE'[m], 'prod %.0f cons %.0f' % (sim.c_prod[m][i] / nd, sim.c_cons[m][i] / nd), '| D-term Q %.0f QV %.0f Der %.0f One %.0f' % (Q, QV, Der, One))
    print('   sinks (both mk)', {k: round(v) for k, v in sinks.items()}, 'vil want/got/ungold/nogoods', [round(sim.c_vg.get((i, x), 0) / nd) for x in ('want', 'got', 'ungold', 'nogoods')])
