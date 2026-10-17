# KSIM: per-village takings multiplier (current rules) so that each village's purse inflow over days 109151-109179 matches the run
# (ks-budzety do_kiesy_28d at plan day 109179). Iterates because purses feed back on prices. Writes vfit.json.
# python -I fit_villages.py [iterations] [key=value ...]
import json, sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
it = int(sys.argv[1]) if len(sys.argv) > 1 and '=' not in sys.argv[1] else 4
P = {'vfit': 0}
for kv in sys.argv[1:]:
    if '=' in kv: k, v = kv.split('='); P[k] = float(v)
fp = os.path.join(HERE, 'vfit.json')
rv = {v['id']: 1.0 for v in W['villages']}
for n in range(it):
    sim = S.Sim(W, P, start=109123)
    sim.rv = [rv[v['id']] for v in sim.Vl]
    while sim.day < 109151: sim.step()
    sim.reset_counters()
    while sim.day < 109179: sim.step()
    nd = sim.c_days
    a = []; b = []
    for k, v in enumerate(sim.Vl):
        x = sim.c_vin[k] / nd; y = v['topurse']
        if x > 1: rv[v['id']] = max(0.2, min(5.0, rv[v['id']] * (y / x) ** 0.8))
        else: rv[v['id']] = min(5.0, rv[v['id']] * 2.0) if y > 1 else rv[v['id']]
        a.append(math.log(1 + x)); b.append(math.log(1 + y))
    ma = sum(a) / len(a); mb = sum(b) / len(b)
    c = sum((x - ma) * (y - mb) for x, y in zip(a, b)) / math.sqrt(sum((x - ma) ** 2 for x in a) * sum((y - mb) ** 2 for y in b))
    print('iter %d: inflow sim %.0f obs %.0f, corr log per village %.3f, multiplier median %.2f' % (n, sum(sim.c_vin) / nd, sum(v['topurse'] for v in sim.Vl), c, sorted(rv.values())[len(rv) // 2]))
rv['_P'] = {k: v for k, v in P.items() if k != 'vfit'}
json.dump(rv, open(fp, 'w', encoding='utf-8'))
print('wrote', fp)
