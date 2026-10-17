# KSIM calibration report: runs the current rules 109123 -> 109193 and compares with the failed autotest (ks-miasta, ledger, log).
# python -I calib.py [key=value ...] [--trade X] [--plan Y] [--seeds N]
import json, sys, os, math, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim as S

HERE = os.path.dirname(os.path.abspath(__file__))

# measured targets (autotest 2026-10-17_01-15-30; window sources in comments)
TARGET = {
    'shelf_q10': 0.0, 'shelf_med': 1.54, 'shelf_q90': 58.6,          # ks-miasta 109193, grain shelf / paid D-term
    'short': 0.392, 'glut': 0.175,                                   # ks-miasta 109193: share of towns with < 0.5 / > 30 days of grain
    'idx_med': 1.57, 'idx_q10': 0.29, 'ceil': 0.13,                  # ks-miasta 109193 grain index (ceil = index >= 9.9; log 's' share: W 20%, E 18%)
    'SC_world': 1.373,                                               # ledger grain production / consumption, both markets, days 109180-109193
    'v_got': 0.28, 'v_ungold': 0.39, 'v_nogoods': 0.34,              # ks-miasta week to 109193 (villages: bought / no money / no goods)
    'SC_W': 1.24, 'SC_E': 1.50,                                      # ledger 'KS: rynki' grain production / consumption, days 109180-109193
    'purses': 1758014,                                               # ks-miasta 109193 village purses
    'inflow': 171500, 'spent': 128200, 'other': 51300,               # ZC4 / Kiesa ludu lines, days 109168-109195 (zl/d)
    'deps': 120, 'other_town': 0.45, 'wdist': 100,                   # Dowoz (wozy) lines
    'spent_0': 70125, 'spent_1': 80862,                              # ZC4 line 109195: bought at shelf prices W / E (zl/d)
    'got_0': 0.282, 'ungold_0': 0.362, 'nogoods_0': 0.355, 'got_1': 0.267, 'ungold_1': 0.423, 'nogoods_1': 0.308,   # ks-miasta 109193 by market
    'purse_0': 1144629, 'purse_1': 613385,                           # ks-miasta 109193 by market
    'zc5_grain': 7284, 'town_grain': 6620, 'vil_grain': 3150, 'castle_grain': 3397,   # goods ledger, 14-day average
}


def run_once(P, trade='caravans', plan='current', seed=1, start=109123, end=109193, init='obs'):
    W = run_once.W
    sim = S.Sim(W, P, trade=trade, plan=plan, init=init, start=start, seed=seed)
    flows = None
    hist = []
    while sim.day < end:
        if sim.day == end - 14:
            sim_f = (sim.c_prod, sim.c_cons)
            sim.reset_counters()
        sim.step()
        if (sim.day - W['kdays'][0]) % 7 == 0 and sim.day < end - 7:
            # weekly counters for village stats: keep only last week before end
            pass
    rows = sim.snapshot()
    s = sim.summary(rows)
    nd = sim.c_days
    s['SC_W'] = s['grain_SC_0']; s['SC_E'] = s['grain_SC_1']
    s['SC_world'] = (sim.c_prod[0][sim.iGrain] + sim.c_prod[1][sim.iGrain]) / max(1e-9, sim.c_cons[0][sim.iGrain] + sim.c_cons[1][sim.iGrain])
    s['inflow'] = sim.c_inflow / nd; s['spent'] = sim.c_spent / nd; s['other'] = sim.c_other / nd
    s['deps'] = sim.c_dep / nd
    g = sim.iGrain
    s['zc5_grain'] = sim.c_sg[('zc5', g)] / nd; s['town_grain'] = sim.c_sg[('town', g)] / nd; s['vil_grain'] = sim.c_sg[('vil', g)] / nd
    s['castle_grain'] = sim.c_sg[('castle', g)] / nd
    for m in (0, 1):
        w = sim.c_vm[m][0] or 1
        s['spent_%d' % m] = sim.c_spm[m] / nd; s['got_%d' % m] = sim.c_vm[m][1] / w; s['ungold_%d' % m] = sim.c_vm[m][2] / w; s['nogoods_%d' % m] = sim.c_vm[m][3] / w
        s['purse_%d' % m] = sum(sim.gold[k] for k, v in enumerate(sim.Vl) if v['mk'] == m)
    o = S.observed(W, end)
    fit = S.town_fit(rows, o)
    return s, fit, sim, rows


def report(P, trade='caravans', plan='current', seeds=1, quiet=False, init='obs'):
    acc = {}; facc = {}
    t0 = time.time()
    for sd in range(1, seeds + 1):
        s, fit, sim, rows = run_once(P, trade, plan, sd, init=init)
        for k in TARGET: acc[k] = acc.get(k, 0) + s.get(k, float('nan')) / seeds
        for k, v in fit.items(): facc[k] = facc.get(k, 0) + v / seeds
    dt = time.time() - t0
    if not quiet:
        print('%-12s %10s %10s %8s' % ('metric', 'sim', 'target', 'err'))
        for k, tv in TARGET.items():
            sv = acc[k]
            if k in ('shelf_q10',): err = sv - tv; e = '%+.2f' % err
            elif tv: err = sv / tv - 1; e = '%+.0f%%' % (100 * err)
            else: e = '-'
            print('%-12s %10.3f %10.3f %8s' % (k, sv, tv, e))
        print('town fit (109193):', ' '.join('%s %.2f' % kv for kv in facc.items()))
        print('time %.1f s (%d seed(s), 70 days each)' % (dt, seeds))
    return acc, facc


if __name__ == '__main__':
    run_once.W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
    P = {}; trade = 'caravans'; plan = 'current'; seeds = 1; init = 'obs'
    args = sys.argv[1:]
    i = 0
    while i < len(args):
        a = args[i]
        if a == '--trade': trade = args[i + 1]; i += 2; continue
        if a == '--plan': plan = args[i + 1]; i += 2; continue
        if a == '--seeds': seeds = int(args[i + 1]); i += 2; continue
        if a == '--init': init = args[i + 1]; i += 2; continue
        k, v = a.split('='); P[k] = float(v); i += 1
    report(P, trade, plan, seeds, init=init)
