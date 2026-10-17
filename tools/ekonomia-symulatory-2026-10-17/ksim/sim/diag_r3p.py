# round 3 (sim): perishable placement - per market x good: catchment supply vs Dp per town, reachable-in-7-days transport bound (greedy max flow on days)
import json, os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, os.environ.get("KSIM_WORLD", "world_r2.json")), encoding="utf-8"))
o = dict(R.DEF)
plan = sys.argv[1] if len(sys.argv) > 1 else os.path.join(R.H3OUT, 'r2_8c2.csv')
sim = R.R3Sim(W, o, seed=1, plan_on=plan, plan_off=os.path.join(R.H3OUT, 'off.csv'), init='A')
ksh = R.KsH(sim)
for m in (0, 1):
    ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
    for g in R.RAW14:
        i = sim.GI[g]; per = g in R.PERISH; lim = 7 if per else 30
        loc = collections.defaultdict(float)
        for k, v in enumerate(sim.Vl):
            if v['mk'] == m: loc[v['trade']] += sim.out[k][i]
        for t in ts: loc[t] += sim.tf[t][i]
        S = sum(loc[t] for t in ts); D = sum(sim.dp[t][i] for t in ts)
        ex = {t: loc[t] - sim.dp[t][i] for t in ts}
        sur = {t: x for t, x in ex.items() if x > 0}; dfc = {t: -x for t, x in ex.items() if x < 0}
        # greedy: shortest pairs first
        pairs = sorted((ksh.days[s][t], s, t) for s in sur for t in dfc if ksh.days[s][t] >= 0 and ksh.days[s][t] <= lim and not ksh.war(s, t))
        moved = 0.0
        for dd, s, t in pairs:
            q = min(sur[s], dfc[t])
            if q <= 0: continue
            sur[s] -= q; dfc[t] -= q; moved += q
        stuck = sum(sur.values()); unmet = sum(dfc.values())
        top = sorted(((loc[t], sim.T[t]['name']) for t in ts), reverse=True)[:3]
        print('%s %-10s S %6.0f D %6.0f | local cover %5.0f moved<=%2dd %5.0f stuck %5.0f (%.0f%% of S) unmet %5.0f (%.0f%% of D) | top %s' % (
            'WE'[m], g, S, D, sum(min(loc[t], sim.dp[t][i]) for t in ts), lim, moved, stuck, 100 * stuck / max(1, S), unmet, 100 * unmet / max(1, D),
            ', '.join('%s %.0f' % (n, x) for x, n in top)))
