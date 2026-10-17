import csv, statistics as st, sys
D = sys.argv[1]

def q(v, p):
    v = sorted(v);
    if not v: return float('nan')
    i = min(len(v) - 1, max(0, int(round(p * (len(v) - 1)))))
    return v[i]

def show(name, v):
    v = [x for x in v if x is not None]
    if not v: print(name, 'brak'); return
    print('%-40s n %4d p10 %9.1f p25 %9.1f med %9.1f p75 %9.1f p90 %9.1f max %9.1f sum %12.0f mean %9.1f' % (name, len(v), q(v, .1), q(v, .25), q(v, .5), q(v, .75), q(v, .9), max(v), sum(v), sum(v) / len(v)))

rows = list(csv.DictReader(open(D + '/ks-budzety.csv', encoding='utf-8'), delimiter=';'))
days = sorted(set(r['dzien'] for r in rows))
for d in days:
    vv = [r for r in rows if r['dzien'] == d and r['rodzaj'] == 'wies']
    ut = [float(r['utarg_28d_zl_d']) for r in vv if r['utarg_28d_zl_d'] not in ('', '-')]
    dk = [float(r['do_kiesy_28d_zl_d']) for r in vv if r['do_kiesy_28d_zl_d'] not in ('', '-')]
    al = [float(r['alfa']) for r in vv if r['alfa'] not in ('', '-')]
    print('dzien', d, 'wsi', len(vv), 'z liczbami', len(ut))
    show('  utarg 28d zl/d na wies', ut)
    show('  do kiesy 28d zl/d na wies', dk)
    show('  alfa', al)
    print('   alfa=0:', sum(1 for a in al if a == 0), 'alfa>0.3:', sum(1 for a in al if a > 0.3))

rows = list(csv.DictReader(open(D + '/ks-miasta.csv', encoding='utf-8'), delimiter=';'))
for d in sorted(set(r['dzien'] for r in rows))[::5] + [sorted(set(r['dzien'] for r in rows))[-1]]:
    vv = [float(r['kiesy_wsi_zl']) for r in rows if r['dzien'] == d and r['kiesy_wsi_zl'] not in ('', '-')]
    show('kiesy wsi na miasto targowe d' + d, vv)
    bk = [float(r['wsie_bez_kiesy_szt_7d']) for r in rows if r['dzien'] == d and r['wsie_bez_kiesy_szt_7d'] not in ('', '-')]
    ch = [float(r['wsie_chciane_szt_7d']) for r in rows if r['dzien'] == d and r['wsie_chciane_szt_7d'] not in ('', '-')]
    ku = [float(r['wsie_kupione_szt_7d']) for r in rows if r['dzien'] == d and r['wsie_kupione_szt_7d'] not in ('', '-')]
    bt = [float(r['wsie_bez_towaru_szt_7d']) for r in rows if r['dzien'] == d and r['wsie_bez_towaru_szt_7d'] not in ('', '-')]
    print('   wsie 7d: chciane %d kupione %d bez kiesy %d bez towaru %d' % (sum(ch), sum(ku), sum(bk), sum(bt)))

rows = list(csv.DictReader(open(D + '/stany-dostatek.csv', encoding='utf-8'), delimiter=';'))
ds = sorted(set(r['dzien'] for r in rows))
for d in [ds[0], ds[len(ds)//2], ds[-1]]:
    vv = [r for r in rows if r['dzien'] == d and r['rodzaj'] == 'wies']
    show('D wsi (dochod na glowe?) d' + d, [float(r['D']) for r in vv if r['D'] not in ('', '-')])
    show('dochod_na_glowe_zl_rok wsi d' + d, [float(r['dochod_na_glowe_zl_rok']) for r in vv if r['dochod_na_glowe_zl_rok'] not in ('', '-')])
    show('najedzeni wsi d' + d, [float(r['najedzeni']) for r in vv if r['najedzeni'] not in ('', '-')])
