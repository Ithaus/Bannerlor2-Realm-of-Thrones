# per market and good: production / consumption (last 14 days) / town stock at 109193 and the median index, sim vs ledger
import json, sys, os, statistics
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
P = {}
for kv in sys.argv[1:]:
    k, v = kv.split('='); P[k] = float(v)
sim = S.Sim(W, P, start=109123)
while sim.day < 109179: sim.step()
sim.reset_counters()
while sim.day < 109193: sim.step()
nd = sim.c_days
op = {m: {g: [0, 0] for g in sim.G} for m in (0, 1)}
for d in range(109180, 109194):
    r = W['rynki'].get(str(d))
    for m in (0, 1):
        for g in sim.G:
            x = r[str(m)].get(g)
            if x: op[m][g][0] += x[0] / 14; op[m][g][1] += x[1] / 14
print('%-10s | %-34s | %-34s' % ('good', 'Westeros sim (prod/cons/stock idx)', 'Essos sim'))
for i, g in enumerate(sim.G):
    line = '%-10s' % g
    for m in (0, 1):
        ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
        st = sum(sim.shelf[t][i] for t in ts)
        med = statistics.median(sim.idx[t][sim.gcat[i]] for t in ts)
        o = W['rynki']['109193'][str(m)].get(g, [0, 0, 0])
        oi = W['obs_prices'][str(m)].get(g if g not in ('carrot', 'orange') else 'apple', [float('nan')])[0]
        line += ' | %5.0f/%5.0f/%6.0f %5.2f  obs %5.0f/%5.0f/%6.0f %5.2f' % (sim.c_prod[m][i] / nd, sim.c_cons[m][i] / nd, st, med, op[m][g][0], op[m][g][1], o[2], oi)
    print(line)
