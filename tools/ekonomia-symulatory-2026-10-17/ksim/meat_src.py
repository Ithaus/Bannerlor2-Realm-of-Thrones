import sys, collections, json
p = sys.argv[1]
dump = json.load(open(sys.argv[2], encoding='utf-8'))
typ = {d['id']: d['type'] for d in dump['dist']}
hdr = None; acc = collections.defaultdict(lambda: [0.0]*7)
for line in open(p, encoding='utf-8'):
    r = line.rstrip('\n').split(';')
    if r[0] != 'D': continue
    if r[1] == 'id': hdr = r; continue
    row = dict(zip(hdr, r))
    t = typ.get(r[1], '?'); m = r[2]
    a = acc[(m, t)]
    for j, c in enumerate(('mk_meat', 'mk_WhaleMeat', 'mk_cow', 'mk_hog', 'mk_sheep', 'mk_hides')): a[j] += float(row.get(c, 0) or 0)
    a[6] += 1
def eq(a): return a[0] + a[1] + 0.5 * (a[2] + a[3] + a[4])
tot = collections.defaultdict(float)
for k, a in acc.items(): tot[k[0]] += eq(a)
for k, a in sorted(acc.items(), key=lambda kv: -eq(kv[1])):
    if eq(a) < 0.01 * tot[k[0]]: continue
    print(k, 'eq %.0f (%.0f%%) meat %.0f whale %.0f cow %.0f hog %.0f sheep %.0f hides %.0f n %d' % ((eq(a), 100*eq(a)/tot[k[0]]) + tuple(a)))
print({k: round(v) for k, v in tot.items()})
