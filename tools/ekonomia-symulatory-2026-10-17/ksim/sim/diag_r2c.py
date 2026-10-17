# round 2: decomposition of village uncovered kcal per market (Plan uncovered, paid kcal share, bought, own-first) at the end of the window
import json, os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(R.DEF)
init = sys.argv[1] if len(sys.argv) > 1 else 'A'
plan = sys.argv[2] if len(sys.argv) > 2 else os.path.join(R.H3OUT, 's1_on.csv')
for a in sys.argv[3:]:
    k, v = a.split('='); o[k] = float(v) if '.' in v else int(v)
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, 1, None, None, init)
r = R.scenario(W, o, 1, plan, os.path.join(R.H3OUT, 'off.csv'), init, keep_sim=True, base_flow=b['_flow'])
s = r['_sim']
print('unc', r['unc'], 'kb', r['kb'], 'kh', r['kh'], 'kr', r['kr'])
for m in (0, 1):
    ks = [k for k, v in enumerate(s.Vl) if v['mk'] == m]
    w = {k: sum(s.Vl[k]['bask'][i] * s.kcal[i] for i in range(s.NG)) for k in ks}; W_ = sum(w.values())
    pk = sum(s.pk[k] * w[k] for k in ks) / W_; fr = sum(s.frac[k] * w[k] for k in ks) / W_
    un = sum(s._pl_unc.get(k, 0) * w[k] for k in ks) / W_
    bv = sum(max(0, s.bought_v[k]) * w[k] for k in ks) / W_
    # villages by bought vs paid
    short = sorted(ks, key=lambda k: -(s.pk[k] - max(0, s.bought_v[k])) * w[k])
    print('%s: Plan unc %.3f paid kcal %.3f home share %.3f | bought (last day) %.3f -> paid shortfall %.3f' % ('WE'[m], un, pk, fr, bv, pk - bv))
    # uncovered by Plan unc buckets
    hi = [k for k in ks if s._pl_unc.get(k, 0) > 0.2]
    print('   villages with Plan unc > 20%%: %d of %d, kcal share %.2f, their unc %.3f' % (len(hi), len(ks), sum(w[k] for k in hi) / W_, sum(s._pl_unc.get(k, 0) * w[k] for k in hi) / max(1, sum(w[k] for k in hi))))
    tl = collections.Counter(); tw = collections.Counter()
    for k in hi: tl[s.town_land[s.Vl[k]['trade']]] += w[k] * s._pl_unc.get(k, 0); tw[s.Vl[k].get('type', '?')] += w[k] * s._pl_unc.get(k, 0)
    print('   by land', [(a, round(100 * x / W_, 2)) for a, x in tl.most_common(8)])
    print('   by type', [(a, round(100 * x / W_, 2)) for a, x in tw.most_common(8)])
    print('   top paid shortfall', [(s.Vl[k]['name'] if 'name' in s.Vl[k] else s.Vl[k]['id'], round(s.pk[k], 2), round(s.bought_v[k], 2)) for k in short[:8]])
print(list(s.Vl[0].keys()))
