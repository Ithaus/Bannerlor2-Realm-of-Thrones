import json, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim as S, calib as C
W = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'world.json')))
C.run_once.W = W
P = {}
for kv in sys.argv[1:]:
    k, v = kv.split('='); P[k] = float(v)
s, fit, sim, rows = C.run_once(P)
o = S.observed(W, 109193)
lst = []
for t, (r, ob) in enumerate(zip(rows, o)):
    loc = sim.tf[t][sim.iGrain] + sum(sim.out[k][sim.iGrain] for k in range(sim.NV) if sim.Vl[k]['trade'] == t)
    lst.append((ob['days'], r['days'], W['towns'][t]['name'], W['towns'][t]['mk'], r['dem'], ob['dem'], loc, r['idx'], ob['idx'], r['want'] and r['got'] / r['want'], ob['want'] and ob['got'] / ob['want']))
lst.sort(key=lambda x: (x[0] if not math.isnan(x[0]) else -1))
print('%-22s %2s %7s %7s | %6s %6s %6s | %5s %5s | %4s %4s' % ('town', 'mk', 'd_obs', 'd_sim', 'Dsim', 'Dobs', 'local', 'i_sim', 'i_obs', 'gotS', 'gotO'))
for x in lst: print('%-22s %2d %7.1f %7.1f | %6.0f %6.0f %6.0f | %5.2f %5.2f | %4.2f %4.2f' % (x[2], x[3], x[0], x[1], x[4], x[5], x[6], x[7], x[8], x[9], x[10]))
