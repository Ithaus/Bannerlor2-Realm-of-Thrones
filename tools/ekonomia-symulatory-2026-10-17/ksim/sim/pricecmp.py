# per-good market median of the 28-day average town index (as 'KS: ceny miast dzien 109193'), sim vs obs; s = share of towns at ceiling (last reading)
import json, sys, os, statistics
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
P = {}
for kv in sys.argv[1:]:
    k, v = kv.split('='); P[k] = float(v)
sim = S.Sim(W, P, start=109123)
acc = [[0.0] * sim.NC for _ in range(sim.NT)]; n = 0
while sim.day < 109193:
    sim.step()
    if sim.day > 109193 - 28:
        n += 1
        for t in range(sim.NT):
            for c in range(sim.NC): acc[t][c] += sim.idx[t][c]
ob = W['obs_prices']
print('%-10s | %6s %6s %5s %5s | %6s %6s %5s %5s' % ('cat', 'W sim', 'W obs', 'sS', 'sO', 'E sim', 'E obs', 'sS', 'sO'))
for c, cn in enumerate(sim.cats):
    gname = cn if cn != 'fruit' else 'apple'
    row = []
    for m in (0, 1):
        ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
        med = statistics.median(acc[t][c] / n for t in ts)
        ceil = sum(1 for t in ts if sim.idx[t][c] >= 9.99) / len(ts)
        o = ob[str(m)].get(gname, [float('nan'), float('nan'), 0])
        row += [med, o[0], ceil, o[1]]
    print('%-10s | %6.2f %6.2f %5.2f %5.2f | %6.2f %6.2f %5.2f %5.2f' % tuple([cn] + row))
