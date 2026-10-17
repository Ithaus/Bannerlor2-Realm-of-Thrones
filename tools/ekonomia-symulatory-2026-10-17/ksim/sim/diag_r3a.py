# diagnostics of the r3port Plan transfer: totals per good and market (to-market output, paid D-term), paid kcal share, home share, budgets
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
on = sys.argv[1] if len(sys.argv) > 1 else os.path.join(R.H3OUT, 'on_a3.csv')
off = os.path.join(R.H3OUT, 'off.csv')
o = dict(R.DEF)
s0 = R.R3Sim(W, dict(o, **R.BASE_OFF), seed=1, plan_on=None, plan_off=None)
s1 = R.R3Sim(W, o, seed=1, plan_on=on, plan_off=off)
print('plan rows transferred', s1._plan_n)
for m in (0, 1):
    print('market', m)
    for g in R.RAW14:
        i = s0.GI[g]
        a = sum(s0.out[k][i] for k, v in enumerate(s0.Vl) if v['mk'] == m) + sum(s0.tf[t][i] for t in range(s0.NT) if s0.T[t]['mk'] == m)
        b = sum(s1.out[k][i] for k, v in enumerate(s1.Vl) if v['mk'] == m) + sum(s1.tf[t][i] for t in range(s1.NT) if s1.T[t]['mk'] == m)
        da = sum(s0.dp[t][i] for t in range(s0.NT) if s0.T[t]['mk'] == m); db = sum(s1.dp[t][i] for t in range(s1.NT) if s1.T[t]['mk'] == m)
        cs = sum(c['dem'][i] for c in s1.C if c.get('mk', 0) == m)
        print('  %-10s out %8.0f -> %8.0f (x%.2f)   Dp %8.0f -> %8.0f  castles %6.0f  out/(Dp+castles) %.2f' % (g, a, b, b / a if a else 0, da, db, cs, b / max(1, db + cs)))
for m in (0, 1):
    ks = [k for k, v in enumerate(s1.Vl) if v['mk'] == m]
    w = [sum(s1.Vl[k]['bask'][i] * s1.kcal[i] for i in range(s1.NG)) for k in ks]
    pk0 = sum(s0.pk[k] * ww for k, ww in zip(ks, w)) / sum(w); pk1 = sum(s1.pk[k] * ww for k, ww in zip(ks, w)) / sum(w)
    fr = sum(s1.frac[k] * ww for k, ww in zip(ks, w)) / sum(w); un = sum(s1._pl_unc.get(k, 0) * ww for k, ww in zip(ks, w)) / sum(w)
    fr0 = sum(s0.frac[k] * ww for k, ww in zip(ks, w)) / sum(w)
    print('mk %d pk base %.3f r3 %.3f | frac base %.3f r3 %.3f | Plan unc %.3f | budget %.0f -> %.0f' % (m, pk0, pk1, fr0, fr, un, sum(s0.budget_v[k] for k in ks), sum(s1.budget_v[k] for k in ks)))
