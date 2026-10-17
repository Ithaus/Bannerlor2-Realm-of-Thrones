import csv, sys, json
from collections import defaultdict

def num(x):
    try:
        return float(x)
    except Exception:
        return 0.0

def load(paths):
    rows = []
    for p in paths:
        with open(p, encoding='utf-8', errors='replace') as f:
            r = csv.DictReader(f, delimiter=';')
            for row in r:
                rows.append(row)
    return rows

def agg(rows, days):
    ds = set(days)
    K = defaultdict(lambda: defaultdict(float))
    rodzaj = defaultdict(int)
    for row in rows:
        d = int(row['dzien'])
        if d not in ds:
            continue
        k = row['krolestwo'] or '(bez)'
        rodzaj[row['rodzaj']] += 1
        a = K[k]
        a['n'] += 1
        a['wojna'] += num(row['wojna'])
        a['zold_p'] += num(row['zold_partii']); a['zold_z'] += num(row['zold_zalog']); a['zold_k'] += num(row['zold_karawan'])
        a['D'] += num(row['D']); a['d_ziemia'] += num(row['d_ziemia']); a['d_korona'] += num(row['d_korona']); a['d_kontrakt'] += num(row['d_kontrakt'])
        a['renty'] += num(row['renty']); a['zwrot'] += num(row['zwrot_korony']); a['renta180'] += num(row['renta_180'])
        a['w5'] += num(row['doplata_w5']); a['w5b'] += num(row['doplata_ze_skarbca_w5b']); a['w7'] += num(row['pobor_wojenny_w7']); a['m1'] += num(row['doplata_m1'])
        a['w6_pob'] += num(row['tarczowe_w6'])
        a['G'] += num(row['G']); a['R'] += num(row['R']) if row['R'] else 0
        if row['R'] and num(row['G']) < num(row['R']):
            a['G_lt_R'] += 1
        a['pulap'] += num(row['pulap'])
        a['dlug_bank'] += num(row['dlug_bank'])
        a['ludzi_p'] += num(row['ludzi_partie']); a['ludzi_z'] += num(row['ludzi_zalogi']); a['limit_p'] += num(row['limit_partii'])
        a['wplyw'] += num(row['wplyw_doby']); a['model'] += num(row['dochod_modelu'])
        a['jednoraz'] += num(row['sr_jednorazowe']); a['wlasne'] += num(row['sr_wlasne'])
        if row['szczebel'] not in ('-', ''):
            a['szczebel_' + row['szczebel']] += 1
    nd = len(ds)
    out = {}
    for k, a in K.items():
        out[k] = {m: v / nd for m, v in a.items()}
    return out, rodzaj

if __name__ == '__main__':
    name = sys.argv[1]
    paths = sys.argv[2:]
    rows = load(paths)
    days = sorted({int(r['dzien']) for r in rows})
    res = {}
    for w, ds in (('first7', days[1:8]), ('last28', days[-28:]), ('all', days[1:])):
        o, rz = agg(rows, ds)
        res[w] = o
    json.dump(res, open(name + '_k.json', 'w'))
    print(name, 'days', days[0], days[-1], len(days), dict(rz))
    o = res['last28']
    cols = ['n', 'wojna', 'zold_p', 'zold_z', 'D', 'd_ziemia', 'd_korona', 'zwrot', 'renta180', 'w5', 'w5b', 'm1', 'w7', 'G', 'G_lt_R', 'dlug_bank', 'ludzi_p', 'limit_p']
    print('%-34s' % 'krolestwo' + ''.join('%10s' % c for c in cols))
    tot = defaultdict(float)
    for k in sorted(o, key=lambda k: -(o[k]['zold_p'] + o[k]['zold_z'])):
        print('%-34s' % k[:34] + ''.join('%10.0f' % o[k].get(c, 0) for c in cols))
        for c in cols:
            tot[c] += o[k].get(c, 0)
    print('%-34s' % 'RAZEM' + ''.join('%10.0f' % tot[c] for c in cols))
