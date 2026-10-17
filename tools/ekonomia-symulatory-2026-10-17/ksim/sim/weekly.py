# KSIM: weekly trajectory sim (mean of N seeds) vs the failed run (ks-miasta every 7 days), current rules, start 109123 from the observed state
import json, sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
seeds = int(sys.argv[1]) if len(sys.argv) > 1 else 3
keys = ['short', 'glut', 'shelf_med', 'shelf_q90', 'idx_med', 'ceil', 'v_got', 'v_ungold', 'v_nogoods', 'purses']
acc = {}
for sd in range(1, seeds + 1):
    sim = S.Sim(W, {}, start=109123, seed=sd)
    while sim.day < 109193:
        sim.step()
        if (sim.day - 109123) % 7 == 0:
            s = sim.summary(sim.snapshot())
            for k in keys: acc[(sim.day, k)] = acc.get((sim.day, k), 0) + s[k] / seeds
            sim.reset_counters()
print('%-7s ' % 'day' + ' '.join('%-17s' % k for k in keys))
for d in range(109130, 109194, 7):
    o = S.obs_summary(W, d)
    print('%-7d ' % d + ' '.join(('%7.2f/%-9.2f' % (acc[(d, k)] / (1e6 if k == 'purses' else 1), o[k] / (1e6 if k == 'purses' else 1))) for k in keys))
print('(sim/obs; purses in M zl)')
