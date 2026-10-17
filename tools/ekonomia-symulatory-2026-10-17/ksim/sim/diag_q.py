import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['ownfirst'] = 0
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
for t in range(sim.NT):
    m = sim.T[t]['mk']
    for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
name = sys.argv[1]
q = [t for t in range(sim.NT) if sim.T[t]['name'] == name][0]
i = sim.GI['grain']; c = sim.gcat[i]
K = sim.trade_mod
arr = collections.Counter(); sentq = collections.Counter()
while sim.day < 109150:
    n0 = len(K.ship)
    before = sim.shelf[q][i]
    sim.step()
    new = [x for x in K.ship if x[2] == q and x[3] == i]
    tot_in = sum(x[4] for x in new)
    print(sim.day, 'shelf %.0f inc %.0f idx %.2f D %.1f slot %d' % (sim.shelf[q][i], K.inc[q][i], sim.idx[q][c], sim.dterm(q, i), K.slot[q]), 'transit to it', [(x[0], sim.T[x[1]]['name'], x[4]) for x in new][:4])
