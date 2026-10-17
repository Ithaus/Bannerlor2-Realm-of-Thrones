import json, sys, statistics
from collections import defaultdict

def load(names):
    data, king, crown = {}, {}, {}
    for n in names:
        j = json.load(open(n))
        for d, v in j['data'].items():
            data.setdefault(int(d), {}).update(v)
        for d, v in j['king'].items():
            king.setdefault(int(d), {}).update(v)
        for d, v in j['crown'].items():
            crown.setdefault(int(d), {}).update(v)
    return data, king, crown

SETS = {'base': ['base.json'], 'ks': ['ks.json'], 'jeff': ['j1.json', 'j2.json', 'j3.json', 'j4.json']}

def win(data, metric, days):
    vals = [data[d][metric] for d in days if d in data and metric in data[d]]
    return (sum(vals) / len(vals)) if vals else None

if __name__ == '__main__':
    metrics = sys.argv[1:] if len(sys.argv) > 1 else None
    out = {}
    for name, files in SETS.items():
        data, king, crown = load(files)
        days = sorted(d for d in data if 'swiat' in data[d] or 'k_wplywy' in data[d])
        d0 = days[0]
        w = {'first7': days[1:8], 'mid': [d for d in days if 50 <= d - d0 < 60], 'last28': days[-28:], 'all': days[1:]}
        ms = metrics or sorted({m for d in days for m in data[d]})
        out[name] = {m: {k: win(data, m, v) for k, v in w.items()} for m in ms}
        out[name]['_days'] = [days[0], days[-1], len(days)]
    ms = metrics or sorted({m for n in out for m in out[n] if m != '_days'})
    print('days', {n: out[n]['_days'] for n in out})
    print('%-26s | %-40s | %-40s | %-40s' % ('metric', 'base first7/mid/last28', 'ks first7/mid/last28', 'jeff first7/mid/last28'))
    def f(x):
        return '-' if x is None else ('%.0f' % x if abs(x) >= 100 else '%.2f' % x)
    for m in ms:
        row = []
        for n in ('base', 'ks', 'jeff'):
            r = out[n].get(m)
            row.append('-' if not r else '%s / %s / %s' % (f(r['first7']), f(r['mid']), f(r['last28'])))
        print('%-26s | %-40s | %-40s | %-40s' % (m, row[0], row[1], row[2]))
