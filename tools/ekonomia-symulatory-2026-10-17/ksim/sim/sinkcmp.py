import json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim as S, calib as C
W = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'world.json')))
C.run_once.W = W
P = {}
for kv in sys.argv[1:]:
    k, v = kv.split('='); P[k] = float(v)
s, fit, sim, rows = C.run_once(P)
nd = sim.c_days
print('%-10s | %6s %6s | %6s %6s | %6s %6s | %6s %6s | %6s %6s' % ('good', 'vilS', 'vilL', 'townS', 'townL', 'zc5S', 'zc5L', 'castS', 'castL', 'lordS', 'lordL'))
for i, g in enumerate(sim.G):
    L = W['sinks'].get(g, {})
    zc5L = L.get('craft', 0) + L.get('bkcraft', 0) + L.get('wsh', 0)
    print('%-10s | %6.0f %6.0f | %6.0f %6.0f | %6.0f %6.0f | %6.0f %6.0f | %6.0f %6.0f' % (g, sim.c_sg[('vil', i)] / nd, L.get('vil', 0), sim.c_sg[('town', i)] / nd, L.get('town', 0),
          sim.c_sg[('zc5', i)] / nd, zc5L, sim.c_sg[('castle', i)] / nd, L.get('castle', 0), sim.c_sg[('lords+court', i)] / nd, L.get('lords_buy', 0) + L.get('court', 0)))
