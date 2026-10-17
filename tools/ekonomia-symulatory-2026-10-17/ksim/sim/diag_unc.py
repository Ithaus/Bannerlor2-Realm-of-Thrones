# compare Plan dumps: per district uncovered kcal (unc x fullkcal) - biggest increases
import sys, json, os
HERE = os.path.dirname(os.path.abspath(__file__))
W = json.load(open(os.path.join(HERE, 'world_r2.json'), encoding='utf-8'))
nm = {v['id']: (v['name'], W['towns'][v['trade']]['name'], v['type']) for v in W['villages']}
def load(p):
    hdr = None; rows = {}
    for line in open(p, encoding='utf-8'):
        r = line.rstrip('\n').split(';')
        if r[0] != 'D': continue
        if r[1] == 'id': hdr = r; continue
        rows[r[1]] = {hdr[j]: r[j] for j in range(2, len(r))}
    return rows
a = load(sys.argv[1]); b = load(sys.argv[2])
tot = {0: [0, 0, 0], 1: [0, 0, 0]}
dl = []
for k, rb in b.items():
    ra = a.get(k)
    if ra is None or rb['tf'] == '1': continue
    m = int(rb['mk']); fk = float(rb['fullkcal'])
    ua = float(ra['unc']) * float(ra['fullkcal']); ub = float(rb['unc']) * fk
    tot[m][0] += ua; tot[m][1] += ub; tot[m][2] += fk
    dl.append((ub - ua, k, float(ra['unc']), float(rb['unc']), float(ra['hw']), float(rb['hw']), float(ra['pk']), float(rb['pk'])))
for m in (0, 1): print('WE'[m], 'unc A %.2f%% B %.2f%%' % (100 * tot[m][0] / tot[m][2], 100 * tot[m][1] / tot[m][2]))
dl.sort(reverse=True)
for x in dl[:15]: print('%+.3g %-18s %s unc %.3f->%.3f hw %.2f->%.2f pk %.3f->%.3f' % (x[0] / 1e6, x[1], nm.get(x[1]), x[2], x[3], x[4], x[5], x[6], x[7]))
