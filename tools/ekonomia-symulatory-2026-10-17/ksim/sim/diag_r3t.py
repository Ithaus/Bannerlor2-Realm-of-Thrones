# round 3 (sim): per town for one market x good - local supply (catchment out + fields), wagon deliveries, Dp, shelf / target, idx, KS-H in/out, reasons
import json, os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, os.environ.get("KSIM_WORLD", "world_r2.json")), encoding="utf-8"))
o = dict(R.DEF); o['dbg'] = 1
init = 'A'; plan = os.path.join(R.H3OUT, 'r2_8c2.csv'); g = 'fish'; mk = 0; seed = 1
for a in sys.argv[1:]:
    if a.startswith('init='): init = a[5:]
    elif a.startswith('plan='): plan = a[5:]
    elif a.startswith('good='): g = a[5:]
    elif a.startswith('mk='): mk = int(a[3:])
    elif a.startswith('seed='): seed = int(a[5:])
    else:
        k, v = a.split('='); o[k] = float(v) if '.' in v else int(v)
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, seed, None, None, init)
r = R.scenario(W, o, seed, plan, os.path.join(R.H3OUT, 'off.csv'), init, keep_sim=True, base_flow=b['_flow'])
sim = r['_sim']; ksh = sim.ksh; i = sim.GI[g]; k = R.RAW14.index(g)
loc = collections.defaultdict(float)
for kv, v in enumerate(sim.Vl): loc[v['trade']] += sim.out[kv][i]
for t in range(sim.NT): loc[t] += sim.tf[t][i]
rows = []
Ts = sim.Ts[mk][i]
for t in range(sim.NT):
    if sim.T[t]['mk'] != mk: continue
    d = sim.dp[t][i]
    rows.append((loc[t] / d if d > 0 else 99, t))
rows.sort()
print('%s %s T* %.1f' % ('WE'[mk], g, Ts))
for _, t in rows:
    d = sim.dp[t][i]; c = ksh.dbg.get((t, k), {}) if ksh else {}
    print('%-20s %-14s loc %6.1f dp %6.1f off7 %6.1f kx %.2f shelf %7.0f (%5.2f T*) idx %.2f inc %4d gold %7.0f | %s' % (
        sim.T[t]['name'][:20], sim.town_land[t][:14], loc[t], d, sim.off7[t][i], ksh.kx[t][k] if ksh else 1, sim.shelf[t][i], sim.shelf[t][i] / (Ts * d) if d > 0 else 0,
        sim.idx[t][sim.gcat[i]], ksh.inc[t][k] if ksh else 0, sim.town_gold(t), ' '.join('%s %d' % (a, x) for a, x in sorted(c.items()))))
