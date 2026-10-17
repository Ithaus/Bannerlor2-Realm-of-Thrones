# small grid search of the free knobs against the calibration targets (current rules); prints the best configurations
import json, sys, os, math, itertools, time
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import sim as S, calib as C
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8')); C.run_once.W = W
T = C.TARGET
WT = {'short': 1.5, 'glut': 1.0, 'shelf_med': 0.5, 'shelf_q90': 0.5, 'idx_med': 1.0, 'ceil': 0.7, 'v_got': 1.0, 'v_ungold': 1.0, 'v_nogoods': 1.0, 'SC_W': 1.5, 'SC_E': 1.0,
      'purse_0': 0.5, 'purse_1': 0.5, 'got_0': 0.5, 'got_1': 0.5}
def score(s, fit):
    e = 0.0
    for k, w in WT.items():
        a = max(1e-3, s[k]); b = max(1e-3, T[k])
        e += w * math.log(a / b) ** 2
    e += 1.0 * (1 - fit['corr_logdays']) ** 2 + 1.0 * (1 - fit['same_class']) ** 2
    return e
base = {'floor_days': 0}
space = json.loads(sys.argv[1]) if len(sys.argv) > 1 else {'dist_k': [1.1, 1.25, 1.4], 'other_rate': [0.015, 0.02, 0.025], 'cycle': [3.8, 4.6], 'zc5_out_days': [4, 7]}
keys = list(space)
res = []
t0 = time.time()
for vals in itertools.product(*[space[k] for k in keys]):
    P = dict(base); P.update(dict(zip(keys, vals)))
    acc = {}; fa = {}
    for sd in (1, 2):
        s, fit, sim, rows = C.run_once(P, seed=sd)
        for k in list(WT) + ['shelf_q10', 'SC_world']: acc[k] = acc.get(k, 0) + s[k] / 2
        for k in fit: fa[k] = fa.get(k, 0) + fit[k] / 2
    res.append((score(acc, fa), P, acc, fa))
res.sort(key=lambda x: x[0])
for sc, P, acc, fa in res[:6]:
    print('%.3f' % sc, {k: P[k] for k in keys}, ' '.join('%s %.2f' % (k, acc[k]) for k in ('short', 'glut', 'shelf_med', 'shelf_q90', 'idx_med', 'ceil', 'v_got', 'v_ungold', 'v_nogoods', 'SC_W', 'SC_E')), 'purses W/E %.2f/%.2f M' % (acc['purse_0'] / 1e6, acc['purse_1'] / 1e6), 'corr %.2f cls %.2f' % (fa['corr_logdays'], fa['same_class']))
print('runs %d, %.0f s' % (len(res) * 2, time.time() - t0))
