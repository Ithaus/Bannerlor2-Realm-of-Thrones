import json, sys
from collections import defaultdict
from summ import load, SETS

def run(name, files):
    data, king, crown = load(files)
    cdays = sorted(crown)
    kdays = sorted(king)
    last = cdays[-28:]
    first = cdays[1:8]
    print('=== ', name, 'crown days', cdays[0], cdays[-1], len(cdays), 'skarbce days', kdays[0], kdays[-1])
    ks = sorted({k for d in cdays for k in crown[d]})
    # counts over all days
    tot = defaultdict(float)
    print('%-34s %8s %8s %8s %8s %8s %8s %8s %8s %8s %6s %6s %6s | %9s %9s %9s %8s %7s' % ('krolestwo(last28 avg)', 'wpl', 'zapas', 'P1', 't184', 'zwrot', 'renty', 'W5', 'W5b', 'kred', 'dKred', 'dRent0', 'dShort', 'skarb0', 'skarbEnd', 'dlug186', 'wojsko', 'minus'))
    rows = []
    for k in ks:
        def avg(m, days):
            v = [crown[d][k].get(m, 0) for d in days if k in crown[d]]
            return sum(v) / len(v) if v else 0
        allc = [d for d in cdays[1:] if k in crown[d]]
        dk = sum(1 for d in allc if crown[d][k].get('kred186', 0) > 0)
        dr0 = sum(1 for d in allc if crown[d][k].get('renty', 0) == 0 and crown[d][k].get('zwrot_nal', 0) > 0)
        dsh = sum(1 for d in allc if crown[d][k].get('zwrot', 0) < crown[d][k].get('zwrot_nal', 0))
        s0 = next((king[d][k]['skarbiec'] for d in kdays if k in king[d]), 0)
        s1 = next((king[d][k]['skarbiec'] for d in reversed(kdays) if k in king[d]), 0)
        dl = next((king[d][k].get('dlug186', 0) for d in reversed(kdays) if k in king[d]), 0)
        wj = next((king[d][k].get('wojsko', 0) for d in reversed(kdays) if k in king[d]), 0)
        mn = next((king[d][k].get('na_minusie', 0) for d in reversed(kdays) if k in king[d]), 0)
        r = (k, avg('wpl', last), avg('zapas', last), avg('P1', last), avg('t184', last), avg('zwrot', last), avg('renty', last), avg('W5', last), avg('W5b', last), avg('kred186', last), dk, dr0, dsh, s0, s1, dl, wj, mn, len(allc))
        rows.append(r)
    rows.sort(key=lambda r: -r[5])
    for r in rows:
        print('%-34s %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %6d %6d %6d | %9d %9d %9d %8d %7d' % ((r[0][:34],) + tuple(r[1:18])))
        for i, m in enumerate(['wpl', 'zapas', 'P1', 't184', 'zwrot', 'renty', 'W5', 'W5b', 'kred']):
            tot[m] += r[1 + i]
        tot['s0'] += r[13]; tot['s1'] += r[14]; tot['dl'] += r[15]
    print('%-34s %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %8.0f %6s %6s %6s | %9d %9d %9d' % ('RAZEM', tot['wpl'], tot['zapas'], tot['P1'], tot['t184'], tot['zwrot'], tot['renty'], tot['W5'], tot['W5b'], tot['kred'], '', '', '', tot['s0'], tot['s1'], tot['dl']))
    n = len(cdays) - 1
    print('kingdom-days with Bank credit for refund: %d of %d (%.1f%%); with zero rents (all day income eaten by refund etc.): %d; refund short: %d' % (
        sum(r[10] for r in rows), sum(r[18] for r in rows), 100.0 * sum(r[10] for r in rows) / max(1, sum(r[18] for r in rows)), sum(r[11] for r in rows), sum(r[12] for r in rows)))

if __name__ == '__main__':
    for name, files in SETS.items():
        run(name, files)
