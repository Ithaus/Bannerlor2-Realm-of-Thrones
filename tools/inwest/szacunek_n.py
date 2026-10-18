"""Uzycie: python -I tools/inwest/szacunek_n.py Armoury/ModuleData <Logs/.../ks-plan.csv>

Szacunek offline n(typ) neutralnego startu zajec wiosek (paczka inwest): ludzie okregu z ks-plan.csv, osady wiosek z arm_map_villages.tsv (v6),
zajecie startowe wioski z arm_ks_land.tsv (S) i kolumny ks_start (przysiolki). Udzial czesci = osady / n (n = ludzie / 250). Tylko szacunek (w grze: ludzie w domu)."""
import csv, sys, collections
md = sys.argv[1]
plan = sys.argv[2]
land = {}
for line in open(md + '/arm_ks_land.tsv', encoding='utf-8'):
    if line.startswith('#'):
        continue
    c = line.rstrip('\n').split('\t')
    if c[0] == 'S':
        land[c[2]] = c[12]
types, people = {}, {}
for r in csv.DictReader(open(plan, encoding='utf-8'), delimiter=';'):
    if r['rodzaj'] != 'wies':
        continue
    types[r['okreg_id']] = r['typ']
    people[r['okreg_id']] = float(r['ludzie'])
hdr = None
side = collections.defaultdict(list)
for line in open(md + '/arm_map_villages.tsv', encoding='utf-8'):
    if line.startswith('#id'):
        hdr = line[1:].rstrip('\n').split('\t'); continue
    if line.startswith('#') or not line.strip():
        continue
    c = line.rstrip('\n').split('\t')
    d = dict(zip(hdr, c))
    start = land.get(d['uid'])
    if d.get('ks_start') and d['ks_start'] != '-':
        start = d['ks_start']
    side[d['village_id']].append((d['uid'], int(d['settlements']), start))
used = set(types.values())
horse = lambda t: t is not None and t.endswith('horse_ranch')
before = collections.Counter(); after = collections.Counter()
other_n = 0; side_n = 0; snow = 0
for v, t in types.items():
    P = people.get(v, 0)
    if P <= 0:
        continue
    n = max(1.0, round(P / 250.0))
    sv = side.get(v, [])
    sf = sum(s for _, s, _ in sv)
    mf = max([s for _, s, _ in sv] + [0])
    if sf + mf > n and sv:
        k = n / (sf + mf)
        sv = [(u, s * k, st) for u, s, st in sv]
        sf = sum(s for _, s, _ in sv)
    before[t] += P
    after[t] += P * max(0.0, n - sf) / n
    for u, s, st in sv:
        side_n += 1
        c = st if (st in used and not horse(st)) else t
        if c == 'cattle_farm:maslo':
            c = 'cattle_farm'
        if c != t:
            other_n += 1
        after[c] += P * s / n
print('okregow', len(types), 'wiosek', side_n, 'inne niz typ', other_n)
rows = []
for t in sorted(set(before) | set(after)):
    b, a = before[t], after[t]
    nn = b / a if a > 0 and b > 0 else 1.0
    rows.append((t, nn, b, a))
for t, nn, b, a in sorted(rows, key=lambda x: x[1]):
    flag = ' PRZYCIETE' if nn < 0.5 or nn > 2.0 else ''
    print(f'{t:24s} n={nn:6.3f}  przed {b:12.0f}  po {a:12.0f}{flag}')
