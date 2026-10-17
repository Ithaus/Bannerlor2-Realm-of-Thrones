# round 2: why grain-short towns get no KS-H grain (reasons per destination x good over the window D+22..D+49)
import json, os, sys, collections, statistics
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, os.environ.get("KSIM_WORLD", "world_r2.json")), encoding="utf-8"))
o = dict(R.DEF); o['dbg'] = 1
init = sys.argv[1] if len(sys.argv) > 1 else 'A'
goods = sys.argv[2].split(',') if len(sys.argv) > 2 else ['grain']
for a in sys.argv[3:]:
    k, v = a.split('='); o[k] = float(v) if '.' in v else int(v)
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, 1, None, None, init)
r = R.scenario(W, o, 1, os.path.join(R.H3OUT, 's1_on.csv'), os.path.join(R.H3OUT, 'off.csv'), init, keep_sim=True, base_flow=b['_flow'])
sim = r['_sim']; ksh = sim.ksh
for g in goods:
    k = R.RAW14.index(g); i = sim.GI[g]
    print('=== %s' % g)
    rows = []
    for t in range(sim.NT):
        d = sim.dp[t][i]
        if d <= 0: continue
        days = sim.shelf[t][i] / d
        rows.append((days, t))
    rows.sort()
    for days, t in rows[:16]:
        c = ksh.dbg.get((t, k), {})
        m = sim.T[t]['mk']
        print('%-20s %-16s %-12s days %5.1f dp %6.0f dz %6.0f inc %5d gold %8.0f res %8.0f idx %.2f | %s' % (
            sim.T[t]['name'][:20], sim.T[t]['kingdom'][:16], sim.town_land[t][:12], days, sim.dp[t][i], max(sim.dp[t][i], sim.off7[t][i]) * ksh.kx[t][k], ksh.inc[t][k],
            sim.town_gold(t), sim.town_reserve(t), sim.idx[t][sim.gcat[i]], dict(c)))
    tot = collections.Counter()
    for (t, kk), c in ksh.dbg.items():
        if kk == k: tot.update(c)
    print('ALL towns %s: %s' % (g, dict(tot)))
