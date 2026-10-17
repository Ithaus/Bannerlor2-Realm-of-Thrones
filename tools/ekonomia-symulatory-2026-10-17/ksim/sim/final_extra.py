import json, sys, os, math, statistics, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['_vin'] = None; o['land'] = 0.5
for a in sys.argv[1:]:
    k, v = a.split('='); o[k] = float(v)
res = collections.defaultdict(list)
for seed in (1, 2, 3):
    oo = dict(o); oo['_vin'], oo['_osh'] = M.prerun_vin(W, oo, 'start', seed, 109140, 109167, 109118)
    sim = M.MSim(W, {}, oo, init='start', start=109118, seed=seed); sim.setup()
    for t in range(sim.NT):
        m = sim.T[t]['mk']
        for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
    purse0 = None
    while sim.day < 109167:
        sim.step()
        if sim.day == 109140: purse0 = sum(sim.gold)
        if sim.day in (109125, 109132):
            inb = 0; n = 0
            for m in (0, 1):
                ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
                for g in M.RAW:
                    i = sim.GI[g]; c = sim.gcat[i]
                    med = statistics.median(sim.idx[t][c] for t in ts)
                    n += 1; inb += abs(med - sim.start_idx[m][i]) <= 0.3
            res['inband_D+%d' % (sim.day - 109118)].append(inb / n)
    res['purse_drift_pct_d'].append(100 * (sum(sim.gold) / purse0 - 1) / 27)
    tr = sim.c_trade
    res['sea_share'].append(tr['sea_pcs'] / max(1, tr['k_sent']))
    res['grain_share'].append(tr['g_grain'] / max(1, tr['k_sent']))
for k, v in res.items(): print(k, [round(x, 3) for x in v], 'mean', round(sum(v) / len(v), 3))
