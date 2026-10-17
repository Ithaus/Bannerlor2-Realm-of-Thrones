import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['ownfirst'] = 0
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
K = sim.trade_mod; i = sim.GI['grain']
q = [t for t in range(sim.NT) if sim.T[t]['name'] == 'Qarth'][0]
while sim.day < 109152:
    n0 = len(K.ship); d0 = sim.day
    sim.step()
    new = [x for x in K.ship[n0:] if x[2] == q and x[3] == i]
    print(d0, 'new grain lots to Qarth', [(sim.T[x[1]]['name'], x[4], x[0]) for x in new], 'inc', round(K.inc[q][i]))
