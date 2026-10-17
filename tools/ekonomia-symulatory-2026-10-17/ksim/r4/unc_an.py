import sys, json, collections
p = sys.argv[1]; dump = json.load(open(sys.argv[2], encoding='utf-8'))
typ = {d['id']: (d['type'], d['land']) for d in dump['dist']}
hdr = None; rows = []
for line in open(p, encoding='utf-8'):
    r = line.rstrip('\n').split(';')
    if r[0] != 'D': continue
    if r[1] == 'id': hdr = r; continue
    rows.append(dict(zip(hdr, r)))
tot = collections.defaultdict(float); unc = collections.defaultdict(float)
cap = collections.defaultdict(float); bytype = collections.defaultdict(lambda: [0.0, 0.0])
for r in rows:
    if r['tf'] == '1': continue
    m = r['mk']; fk = float(r['fullkcal']); u = float(r['unc']) * fk; hw = float(r['hw'])
    tot[m] += fk; unc[m] += u
    if hw >= 0.849: cap[m] += u
    t, l = typ.get(r['id'], ('?', '?'))
    if m == '0': a = bytype[(l, t)]; a[0] += u; a[1] += fk
print('uncovered share', {m: round(100*unc[m]/tot[m], 1) for m in tot}, 'of which at HomeMax cap', {m: round(100*cap[m]/max(1,unc[m]), 0) for m in unc})
for k, a in sorted(bytype.items(), key=lambda kv: -kv[1][0])[:25]:
    print(k, 'unc %.0f mln (%.0f%% of W unc), %.0f%% of own kcal' % (a[0]/1e6, 100*a[0]/unc['0'], 100*a[0]/max(1, a[1])))
