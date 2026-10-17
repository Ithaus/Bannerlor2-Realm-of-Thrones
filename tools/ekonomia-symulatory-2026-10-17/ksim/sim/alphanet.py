# alpha_net per village (KS-R3 P2, StartCalibration.Money.cs VilAlphaFor with Ks Alpha Net): 0.95 x (purse inflow + KS-H freight - nonbasket outflows) / base value
#   = alpha_round3 x (inflow + freight - outflow) / inflow   (alpha_round3 = 0.95 x inflow / base value - ks-budzety.csv, the same base)
# measured in the r3port run (window D+22..D+49, KS-R3 daily rules) on the Plan given; writes id;alpha for the harness (KS_ALPHA).
# python -I alphanet.py PLAN_ON.csv OUT_ALPHA.csv [seeds]
import json, os, sys, csv
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R

def main(argv):
    plan_on, outp = argv[0], argv[1]
    seeds = int(argv[2]) if len(argv) > 2 else 2
    W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
    o = dict(R.DEF)
    for a in argv[3:]:
        k, v = a.split('='); o[k] = float(v) if '.' in v else int(v)
    acc = None
    for sd in range(1, seeds + 1):
        ob = dict(o); ob.update(R.BASE_OFF)
        b = R.scenario(W, ob, sd, None, None, 'split')
        r = R.scenario(W, o, sd, plan_on, os.path.join(R.H3OUT, 'off.csv'), 'split', base_flow=b['_flow'])
        v = r['vnet']
        acc = [list(x) for x in v] if acc is None else [[a + c for a, c in zip(A, B)] for A, B in zip(acc, v)]
    acc = [[x / seeds for x in a] for a in acc]
    # round-3 alpha per village (last day of ks-budzety.csv)
    al = {}; last = None
    for row in csv.reader(open(os.path.join(W['csv'], 'ks-budzety.csv'), encoding='utf-8'), delimiter=';'):
        if len(row) < 9 or not row[0].isdigit(): continue
        d = int(row[0])
        if last is None or d > last: last = d; al = {}
        if d < last: continue
        if row[2] == 'wies':
            try: al[row[1]] = float(row[8])
            except ValueError: pass
    n = 0; f_all = []; tot = [[0.0] * 3, [0.0] * 3]
    with open(outp, 'w', encoding='utf-8', newline='\n') as f:
        for k, v in enumerate(W['villages']):
            a3 = al.get(v['id'])
            if a3 is None: continue
            vin, vfr, vout = acc[k]
            tot[v['mk']][0] += vin; tot[v['mk']][1] += vfr; tot[v['mk']][2] += vout
            if vin <= 1e-6: continue
            fac = max(0.0, vin + vfr - vout) / vin
            an = max(0.0, min(0.95, a3 * fac))
            f.write('%s;%.5f\n' % (v['id'], an)); n += 1; f_all.append(fac)
    f_all.sort()
    print('alpha_net written for %d villages; factor q10/med/q90 %.2f/%.2f/%.2f' % (n, f_all[len(f_all) // 10], f_all[len(f_all) // 2], f_all[9 * len(f_all) // 10]))
    for m in (0, 1): print('  %s: inflow %.0f, freight %.0f, outflows %.0f zl/d -> net/inflow %.3f' % ('WE'[m], tot[m][0], tot[m][1], tot[m][2], (tot[m][0] + tot[m][1] - tot[m][2]) / max(1, tot[m][0])))

if __name__ == '__main__':
    main(sys.argv[1:])
