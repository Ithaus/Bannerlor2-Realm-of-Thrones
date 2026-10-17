import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['ownfirst'] = 0
for a in sys.argv[2:]:
    k, v = a.split('='); o[k] = float(v)
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
for t in range(sim.NT):
    m = sim.T[t]['mk']
    for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
sent = collections.Counter()
orig = sim.trade_mod.ship
while sim.day < 109150:
    n0 = len(sim.trade_mod.ship)
    sim.step()
for (a, s, t, i, n) in sim.trade_mod.ship: sent[sim.G[i]] += n
print('in transit by good', dict(sent))
g = sys.argv[1]; i = sim.GI[g]; c = sim.gcat[i]
for m in (0, 1):
    rows = []
    for t in range(sim.NT):
        if sim.T[t]['mk'] != m: continue
        d = sim.dterm(t, i); T = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6)
        rows.append((sim.shelf[t][i], d, sim.idx[t][c], sim.T[t]['name'], sim.trade_mod.inc[t][i]))
    rows.sort(reverse=True)
    tot = sum(r[0] for r in rows); D = sum(r[1] for r in rows)
    print('market', m, 'stock', round(tot), 'D', round(D, 1), 'T*', round(T, 1))
    for r in rows[:8]: print('  top %-22s shelf %8.0f D %7.1f idx %.2f inc %.0f' % (r[3], r[0], r[1], r[2], r[4]))
    low = [r for r in rows if r[1] > 0 and r[0] < 0.5 * T * r[1]]
    print('  low towns', len(low), 'D', round(sum(r[1] for r in low), 1))
    for r in sorted(low, key=lambda r: -r[1])[:8]: print('  low %-22s shelf %8.0f D %7.1f idx %.2f inc %.0f' % (r[3], r[0], r[1], r[2], r[4]))
# why are low towns not served (grain)?
i = sim.GI['grain']; c = sim.gcat[i]; K = sim.trade_mod
for m in (0, 1):
    T = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6)
    srcs = [s for s in range(sim.NT) if sim.T[s]['mk'] == m and sim.shelf[s][i] - o['ksrc'] * T * max(0, sim.dterm(s, i)) - 5 > 1]
    print('market', m, 'sources with surplus', len(srcs), [sim.T[s]['name'] for s in srcs][:12])
    for t in range(sim.NT):
        if sim.T[t]['mk'] != m: continue
        d = sim.dterm(t, i)
        if d <= 0 or sim.shelf[t][i] + K.inc[t][i] >= 0.5 * T * d: continue
        cands = [(K.days[s][t], sim.T[s]['name'], round(sim.idx[s][c], 2)) for s in srcs if K.days[s][t] >= 0]
        cands.sort()
        print('  %-20s D %6.1f shelf %6.0f inc %6.0f idx %.2f | nearest src %s' % (sim.T[t]['name'], d, sim.shelf[t][i], K.inc[t][i], sim.idx[t][c], cands[:3]))
