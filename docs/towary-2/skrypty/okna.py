# okna.py JSON "a-b,c-d" klucz1 klucz2 ... (prefiks* = wszystkie klucze z prefiksem) -> srednie w oknach dob
import sys, json
D = json.load(open(sys.argv[1]))
wins = [tuple(int(x) for x in w.split('-')) for w in sys.argv[2].split(',')]
keys = []
allk = set()
for v in D.values(): allk.update(v.keys())
for k in sys.argv[3:]:
    if k.endswith('*'): keys += sorted(x for x in allk if x.startswith(k[:-1]))
    else: keys.append(k)
print('%-28s' % 'klucz' + ''.join('%14s' % ('d%d-%d' % w) for w in wins))
for k in keys:
    row = []
    for a, b in wins:
        vals = [D[str(d)][k] for d in range(a, b + 1) if str(d) in D and k in D[str(d)] and isinstance(D[str(d)][k], (int, float))]
        row.append(('%.1f' % (sum(vals) / len(vals))) if vals else '-')
    print('%-28s' % k + ''.join('%14s' % x for x in row))
