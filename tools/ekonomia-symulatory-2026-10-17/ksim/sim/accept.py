# KSIM acceptance run: a scenario (trade module, plan module) against the current rules, with the in-game NKS checks recomputed per market
# and the per-town distribution Jeff's rule needs (every good 95-105% of demand, towns supplied, prices near start).
# python -I accept.py [--trade caravans|none|carts[:k=v...]] [--plan current|local[:cap=3]|csv=<ks-plan.csv>[@day]] [--init start|obs] [--days 50] [--seeds 2] [key=value ...]
# Window = D+22..D+49 (109140..109167) like NKS-5/6/7 when --init start (D = 109118); with --init obs the run starts at 109123 (obs state).
import json, sys, os, math, statistics, time, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import sim as S

RAW = ['grain', 'fish', 'meat', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'honey']


def scenario(W, P, trade, plan, init, start, end, w0, w1, seed):
    sim = S.Sim(W, P, trade=trade, plan=plan, init=init, start=start, seed=seed)
    NT, NG = sim.NT, sim.NG
    idx_acc = [[0.0] * sim.NC for _ in range(NT)]; nidx = 0
    st0 = None
    while sim.day < end:
        if sim.day == w0:
            sim.reset_counters()
            st0 = [[sum(sim.shelf[t][i] for t in range(NT) if sim.T[t]['mk'] == m) for i in range(NG)] for m in (0, 1)]
        sim.step()
        if w0 < sim.day <= w1:
            nidx += 1
            for t in range(NT):
                for c in range(sim.NC): idx_acc[t][c] += sim.idx[t][c]
        if sim.day == w1:
            break
    nd = max(1, sim.c_days)
    r = {'sim': sim, 'nd': nd}
    # NKS-5: production / consumption per market x good (raw goods with consumption >= 20 pcs/d)
    sc = {}
    for m in (0, 1):
        for i, g in enumerate(sim.G):
            p = sim.c_prod[m][i] / nd; c = sim.c_cons[m][i] / nd
            if g in RAW and c >= 20: sc[(m, g)] = p / c
    r['sc'] = sc
    # NKS-6: town stock change per day / consumption
    st1 = [[sum(sim.shelf[t][i] for t in range(NT) if sim.T[t]['mk'] == m) for i in range(NG)] for m in (0, 1)]
    r['dstock'] = {(m, g): (st1[m][i] - st0[m][i]) / nd / max(1e-9, sim.c_cons[m][i] / nd) for m in (0, 1) for i, g in enumerate(sim.G)
                   if g in RAW and sim.c_cons[m][i] / nd >= 20}
    # NKS-7 / 7b: market median of the window-average town index vs start +-0.3; share of towns at ceiling / floor (last reading)
    pr = {}; cf = {}
    for m in (0, 1):
        ts = [t for t in range(NT) if sim.T[t]['mk'] == m]
        for i, g in enumerate(sim.G):
            if g not in RAW: continue
            c = sim.gcat[i]
            med = statistics.median(idx_acc[t][c] / max(1, nidx) for t in ts)
            pr[(m, g)] = (med, sim.start_idx[m][i])
            cf[(m, g)] = (sum(1 for t in ts if sim.idx[t][c] >= 9.99) / len(ts), sum(1 for t in ts if sim.idx[t][c] <= 0.101) / len(ts))
    r['pr'] = pr; r['cf'] = cf
    # towns: grain shelf days, index in band, grain on shelf 1-15 days
    rows = sim.snapshot()
    days = [x['days'] for x in rows if not math.isnan(x['days'])]
    r['shelf'] = (S.q(days, .1), S.q(days, .5), S.q(days, .9))
    r['short'] = sum(1 for x in days if x < 0.5) / len(days); r['glut'] = sum(1 for x in days if x > 30) / len(days)
    r['band_days'] = sum(1 for x in days if 1 <= x <= 15) / len(days)
    gi = [x['idx'] for x in rows]
    r['idx_med'] = S.q(gi, .5); r['idx_band'] = sum(1 for x in gi if abs(x - 1.5) <= 0.3) / len(gi); r['ceil'] = sum(1 for x in gi if x >= 9.9) / len(gi)
    # per-town food balance: town supply reaching the shelf vs its paid D-term (grain) - share of towns with 0.95-1.05 is not observable per day;
    # use the window: shelf change + offtake ~ arrivals; report towns whose grain shelf moved by less than 1 day of D-term per week
    W_ = sum(x['want'] for x in rows) or 1
    r['v'] = (sum(x['got'] for x in rows) / W_, sum(x['ungold'] for x in rows) / W_, sum(x['nogoods'] for x in rows) / W_)
    r['unc'] = (sim.c_unc[0] / max(1, sim.c_kw[0]), sim.c_unc[1] / max(1, sim.c_kw[1]))
    r['trade'] = {k: v / nd for k, v in sim.c_trade.items()}
    r['purses'] = sum(sim.gold)
    r['inflow'] = sim.c_inflow / nd
    return r


def summarize(rs):
    """average over seeds"""
    out = {}
    keys = ['short', 'glut', 'band_days', 'idx_med', 'idx_band', 'ceil', 'purses', 'inflow']
    for k in keys: out[k] = sum(r[k] for r in rs) / len(rs)
    out['shelf'] = tuple(sum(r['shelf'][j] for r in rs) / len(rs) for j in range(3))
    out['v'] = tuple(sum(r['v'][j] for r in rs) / len(rs) for j in range(3))
    out['unc'] = tuple(sum(r['unc'][j] for r in rs) / len(rs) for j in range(2))
    for k in ('sc', 'dstock'):
        out[k] = {kk: sum(r[k].get(kk, float('nan')) for r in rs) / len(rs) for kk in rs[0][k]}
    out['pr'] = {kk: (sum(r['pr'][kk][0] for r in rs) / len(rs), rs[0]['pr'][kk][1]) for kk in rs[0]['pr']}
    out['cf'] = {kk: (sum(r['cf'][kk][0] for r in rs) / len(rs), sum(r['cf'][kk][1] for r in rs) / len(rs)) for kk in rs[0]['cf']}
    tr = collections.defaultdict(float)
    for r in rs:
        for k, v in r['trade'].items(): tr[k] += v / len(rs)
    out['trade'] = dict(tr)
    return out


def report(name, o):
    mk = {0: 'W', 1: 'E'}
    bad5 = [('%s %s %.2f' % (mk[m], g, x)) for (m, g), x in sorted(o['sc'].items()) if not (0.95 <= x <= 1.05)]
    bad6 = [('%s %s %+.1f%%' % (mk[m], g, 100 * x)) for (m, g), x in sorted(o['dstock'].items()) if abs(x) > 0.02]
    bad7 = [('%s %s %.2f' % (mk[m], g, x[0])) for (m, g), x in sorted(o['pr'].items()) if abs(x[0] - x[1]) > 0.3]
    bad7b = [('%s %s s%.0f p%.0f' % (mk[m], g, 100 * x[0], 100 * x[1])) for (m, g), x in sorted(o['cf'].items()) if x[0] > 0.2 or x[1] > 0.2]
    print('=== %s' % name)
    print('  NKS-5  S/C per market x raw good outside 0.95-1.05: %d of %d  %s' % (len(bad5), len(o['sc']), ', '.join(bad5[:14])))
    print('  NKS-6  town stock change > 2%%/d of consumption:     %d of %d  %s' % (len(bad6), len(o['dstock']), ', '.join(bad6[:14])))
    print('  NKS-7  market median index outside start +-0.3:      %d of %d  %s' % (len(bad7), len(o['pr']), ', '.join(bad7[:14])))
    print('  NKS-7b ceiling/floor in > 20%% of towns:             %d of %d  %s' % (len(bad7b), len(o['cf']), ', '.join(bad7b[:14])))
    print('  grain towns: shelf days q10/med/q90 %.1f/%.1f/%.1f | < 0.5 d %.0f%% | > 30 d %.0f%% | 1-15 d %.0f%% | index median %.2f, in 1.5+-0.3 %.0f%%, at ceiling %.0f%%' % (
        o['shelf'] + (100 * o['short'], 100 * o['glut'], 100 * o['band_days'], o['idx_med'], 100 * o['idx_band'], 100 * o['ceil'])))
    print('  villages: bought %.0f%% / no money %.0f%% / no goods %.0f%% of wanted pieces; basket kcal with no coverage W %.0f%% E %.0f%%; purses %.2f M, inflow %.0f zl/d' % (
        100 * o['v'][0], 100 * o['v'][1], 100 * o['v'][2], 100 * o['unc'][0], 100 * o['unc'][1], o['purses'] / 1e6, o['inflow']))
    if o['trade']: print('  trade module per day: ' + ', '.join('%s %.0f' % kv for kv in sorted(o['trade'].items())))


def main(argv):
    W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
    trade = 'caravans'; plan = 'current'; init = 'start'; seeds = 2; P = {}; days = None; base = True
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == '--trade': trade = argv[i + 1]; i += 2; continue
        if a == '--plan': plan = argv[i + 1]; i += 2; continue
        if a == '--init': init = argv[i + 1]; i += 2; continue
        if a == '--seeds': seeds = int(argv[i + 1]); i += 2; continue
        if a == '--nobase': base = False; i += 1; continue
        k, v = a.split('='); P[k] = float(v); i += 1
    if init == 'start':
        start, w0, w1 = 109118, 109140, 109167          # D, D+22, D+49
    else:
        start, w0, w1 = 109123, 109140, 109167
    t0 = time.time()
    if base:
        rb = [scenario(W, P, 'caravans', 'current', init, start, w1, w0, w1, sd) for sd in range(1, seeds + 1)]
        report('BASE (current rules, init %s, window %d..%d, %d seeds)' % (init, w0, w1, seeds), summarize(rb))
    if trade != 'caravans' or plan != 'current' or not base:
        rf = [scenario(W, P, trade, plan, init, start, w1, w0, w1, sd) for sd in range(1, seeds + 1)]
        report('FIX trade=%s plan=%s' % (trade, plan), summarize(rf))
    print('time %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main(sys.argv[1:])
