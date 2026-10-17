# round 3 (sim): per market x raw food good - production, consumption by sink, paid D-term, shelves vs target, KS-H flows and reasons (1 seed, window D+22..D+49)
import json, os, sys, collections, statistics, math
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, os.environ.get("KSIM_WORLD", "world_r2.json")), encoding="utf-8"))
o = dict(R.DEF); o['dbg'] = 1
init = 'A'; plan = os.path.join(R.H3OUT, 'r2_8c2.csv'); goods = R.RAW14; seed = 1
args = []
for a in sys.argv[1:]:
    if a.startswith('init='): init = a[5:]
    elif a.startswith('plan='): plan = a[5:]
    elif a.startswith('goods='): goods = a[6:].split(',')
    elif a.startswith('seed='): seed = int(a[5:])
    else:
        k, v = a.split('='); o[k] = float(v) if '.' in v else int(v)
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, seed, None, None, init)
r = R.scenario(W, o, seed, plan, os.path.join(R.H3OUT, 'off.csv'), init, keep_sim=True, base_flow=b['_flow'])
sim = r['_sim']; ksh = sim.ksh; nd = max(1, sim.c_days)
sinks = ['vil1', 'vil2', 'own', 'town', 'zc5', 'lords', 'castle', 'old']
for m in (0, 1):
    print('==== market %s' % 'WE'[m])
    ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
    for g in goods:
        i = sim.GI[g]; k = R.RAW14.index(g)
        prod = sim.c_prod[m][i] / nd; cons = sim.c_cons[m][i] / nd
        dp = sum(sim.dp[t][i] for t in ts); Ts = sim.Ts[m][i]
        tgt = sum(Ts * sim.dp[t][i] for t in ts); sh = sum(max(0.0, sim.shelf[t][i]) for t in ts)
        rat = [sim.shelf[t][i] / (Ts * sim.dp[t][i]) for t in ts if sim.dp[t][i] > 0.5]
        short = sum(1 for x in rat if x < 0.5); glut = sum(1 for x in rat if x > 2.0)
        sk = ' '.join('%s %.0f' % (s, sim.c_sgm.get((s, m, i), 0.0) / nd) for s in sinks if sim.c_sgm.get((s, m, i), 0.0) > 0)
        sh_ = ksh.c.get('g_' + g, 0.0) / nd if ksh else 0
        rs = collections.Counter()
        if ksh and ksh.dbg is not None:
            for (t, kk), c in ksh.dbg.items():
                if kk == k and sim.T[t]['mk'] == m: rs.update(c)
        wh = sum(sim.wh[kv][i] for kv, v in enumerate(sim.Vl) if v['mk'] == m)
        print('%-10s prod %6.0f cons %6.0f S/C %.2f | Dp %6.0f T* %.1f tgt %7.0f shelf %7.0f (%.2f) wh %6.0f | towns %d short %d glut %d | KS-H %5.0f/d | %s | %s' % (
            g, prod, cons, prod / cons if cons else 0, dp, Ts, tgt, sh, sh / tgt if tgt else 0, wh, len(rat), short, glut, sh_, sk,
            ' '.join('%s %d' % (a, c) for a, c in sorted(rs.items()))))
