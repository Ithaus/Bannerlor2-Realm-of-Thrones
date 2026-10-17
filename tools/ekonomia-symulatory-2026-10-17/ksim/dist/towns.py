# Per-town distribution analysis of autotest 2026-10-17_01-15-30 (KS round 2). Read-only.
import csv, collections, sys, os, math, statistics as st
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import geo

D = r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30'
ROZ = geo.ROZ
OUT = os.path.dirname(os.path.abspath(__file__))
PLAN_DAY = sys.argv[1] if len(sys.argv) > 1 else '109179'
RANGE = 250.0

names, land, sea, vbound, pos = geo.load()

# ---- towns (ks-miasta)
km = list(csv.DictReader(open(D + '/ks-miasta.csv', encoding='utf-8'), delimiter=';'))
days = sorted(set(int(r['dzien']) for r in km))
town = {}
byday = collections.defaultdict(dict)
for r in km:
    byday[int(r['dzien'])][r['miasto_id']] = r
    town[r['miasto_id']] = dict(name=r['miasto'], mk=r['rynek'])
T = sorted(town)

# ---- villages
vt = {r['vid']: r for r in csv.DictReader(open(ROZ + '/kalibracja/ks_wsie_teren.tsv', encoding='utf-8'), delimiter='\t')}

# ---- road distances from every town (land graph incl. village edges)
G = collections.defaultdict(dict)
for a, nb in land.items():
    for b, w in nb.items(): G[a][b] = w
dist_from_town = {t: geo.dijkstra(G, t) for t in T}
dist_from_town_sea = {t: geo.dijkstra(G, t, sea) for t in T}

def road(t, v):
    return dist_from_town[t].get(v)

# trade town of each village: own town if bound is town, else nearest town by land road (approx. Village.TradeBound)
trade = {}
for v, b in vbound.items():
    if b in town: trade[v] = b; continue
    best = None
    for t in T:
        d = road(t, v)
        if d is None: continue
        if best is None or d < best[0]: best = (d, t)
    trade[v] = best[1] if best else None

# ---- plan (ks-plan.csv) day PLAN_DAY
goods_food = ['grain', 'fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'meat', 'honey']
prod = collections.defaultdict(lambda: collections.defaultdict(float))   # district -> good -> szt/d to market
prodkg = collections.defaultdict(lambda: collections.defaultdict(float))
eaten = collections.defaultdict(lambda: collections.defaultdict(float))
dpeople = {}; dland = {}; dtype = {}
with open(D + '/ks-plan.csv', encoding='utf-8') as f:
    for r in csv.DictReader(f, delimiter=';'):
        if r['dzien'] != PLAN_DAY: continue
        o = r['okreg_id']
        prod[o][r['towar']] += float(r['na_targ_szt_d'])
        prodkg[o][r['towar']] += float(r['produkcja_kg_d'])
        eaten[o][r['towar']] += float(r['zjedzone_w_okregu_kg_d'])
        dpeople[o] = float(r['ludzie']); dland[o] = r['kraina']; dtype[o] = r['typ']

def district_town(o):
    if o.endswith('#pola'): return o.split('#')[0]
    return trade.get(o)

own = collections.defaultdict(lambda: collections.defaultdict(float))     # town -> good -> szt/d (own catchment)
reach = collections.defaultdict(lambda: collections.defaultdict(float))   # town -> good -> share of every village within RANGE (split equally)
nvil = collections.Counter(); nvil250 = collections.Counter(); kraina = collections.defaultdict(collections.Counter)
for o, gd in prod.items():
    t = district_town(o)
    if t is None: continue
    for g, x in gd.items(): own[t][g] += x
    if not o.endswith('#pola'):
        nvil[t] += 1
        kraina[t][dland[o]] += 1
        cands = [tt for tt in T if (road(tt, o) is not None and road(tt, o) <= RANGE) or tt == vbound.get(o)]
        for tt in cands: nvil250[tt] += 1
        if not cands: cands = [t]
        for tt in cands:
            for g, x in gd.items(): reach[tt][g] += x / len(cands)
    else:
        for g, x in gd.items(): reach[t][g] += x

# ---- nearest other town by land and by land+sea
def nearest_other(t, dd):
    best = None
    for u in T:
        if u == t: continue
        d = dd[t].get(u)
        if d is None: continue
        if best is None or d < best[0]: best = (d, u)
    return best

def q(xs, p):
    xs = sorted(xs);
    if not xs: return float('nan')
    k = (len(xs) - 1) * p; i = int(k); j = min(i + 1, len(xs) - 1)
    return xs[i] + (xs[j] - xs[i]) * (k - i)

last = days[-1]; first = [d for d in days if d >= 109123][0]
rows = []
for t in T:
    r = byday[last][t]; r0 = byday[first][t]
    dem = float(r['zboze_popyt_zaplacony_szt_d'])
    sh = float(r['zboze_polka_szt']); sh0 = float(r0['zboze_polka_szt'])
    # shelf slope over window first..last (szt/d)
    slope = (sh - sh0) / (last - first)
    sup = own[t]['grain']; sup250 = reach[t]['grain']
    no = nearest_other(t, dist_from_town); nos = nearest_other(t, dist_from_town_sea)
    # weekly village buying stats summed over all 7d rows from first..last
    w = [byday[d][t] for d in days if first < d <= last]
    want = sum(float(x['wsie_chciane_szt_7d']) for x in w); got = sum(float(x['wsie_kupione_szt_7d']) for x in w)
    nog = sum(float(x['wsie_bez_kiesy_szt_7d']) for x in w); nogd = sum(float(x['wsie_bez_towaru_szt_7d']) for x in w)
    els = sum(float(x['wsie_w_innym_miescie_szt_7d']) for x in w)
    gi = [float(byday[d][t]['zboze_indeks']) for d in days if d >= first]
    food = sum(own[t][g] for g in goods_food)
    rows.append(dict(id=t, name=town[t]['name'], mk=town[t]['mk'], people=float(r['ludzie']), nvil=nvil[t], nvil250=nvil250[t],
                     kraina=','.join(k for k, _ in kraina[t].most_common(2)) or '-',
                     sup=sup, sup250=sup250, dem=dem, ratio=(sup / dem if dem > 0 else float('inf')), ratio250=(sup250 / dem if dem > 0 else float('inf')),
                     shelf=sh, days=(sh / dem if dem > 0 else float('nan')), slope=slope, idx=float(r['zboze_indeks']), idxmed=st.median(gi),
                     foodidx=float(r['indeks_jedzenia_mediana']), ceil=int(r['jedzenie_na_suficie']), floor=int(r['jedzenie_na_podlodze']),
                     near=(no[0] if no else None), near_name=(names.get(no[1]) if no else '-'), nears=(nos[0] if nos else None),
                     want=want, got=got, nog=nog, nogd=nogd, els=els, paidb=float(r['czesc_zaplacona_koszyka_miasta']),
                     fish=own[t]['fish'], food=food, kies=float(r['kiesy_wsi_zl'])))

def fmt(x, n=1):
    if x is None: return '-'
    if isinstance(x, float):
        if math.isinf(x): return 'inf'
        if math.isnan(x): return 'nan'
        return ('%.' + str(n) + 'f') % x
    return str(x)

hdr = ['id', 'name', 'mk', 'people', 'nvil', 'nvil250', 'kraina', 'sup', 'sup250', 'dem', 'ratio', 'ratio250', 'shelf', 'days', 'slope', 'idx', 'idxmed', 'foodidx', 'ceil', 'floor',
       'near', 'near_name', 'nears', 'want', 'got', 'nog', 'nogd', 'els', 'paidb', 'fish', 'food', 'kies']
with open(OUT + '/towns_%s.tsv' % PLAN_DAY, 'w', encoding='utf-8', newline='') as f:
    f.write('\t'.join(hdr) + '\n')
    for r in sorted(rows, key=lambda r: r['ratio']):
        f.write('\t'.join(fmt(r[k], 2 if k in ('ratio', 'ratio250', 'idx', 'idxmed', 'foodidx', 'paidb') else 1) for k in hdr) + '\n')

print('plan day', PLAN_DAY, 'window', first, '..', last, 'towns', len(rows))
for mk in ('Westeros', 'Essos'):
    rs = [r for r in rows if r['mk'] == mk]
    S = sum(r['sup'] for r in rs); Dm = sum(r['dem'] for r in rs)
    print(mk, 'own-catchment grain to market %.0f szt/d, paid town demand %.0f szt/d, ratio %.2f' % (S, Dm, S / Dm))
# correlation: log(ratio) vs log(idx), and vs days
import math
def corr(a, b):
    ma = sum(a) / len(a); mb = sum(b) / len(b)
    sa = math.sqrt(sum((x - ma) ** 2 for x in a)); sb = math.sqrt(sum((y - mb) ** 2 for y in b))
    return sum((x - ma) * (y - mb) for x, y in zip(a, b)) / (sa * sb)
ok = [r for r in rows if r['dem'] > 0 and r['sup'] > 0 and r['idx'] > 0]
print('corr log(own supply/demand) vs log(grain idx): %.2f (n=%d)' % (corr([math.log(r['ratio']) for r in ok], [math.log(r['idx']) for r in ok]), len(ok)))
ok2 = [r for r in rows if r['dem'] > 0 and r['sup250'] > 0 and r['idx'] > 0]
print('corr log(250-reach supply/demand) vs log(grain idx): %.2f' % corr([math.log(r['ratio250']) for r in ok2], [math.log(r['idx']) for r in ok2]))
# buckets
bk = [('own S/D < 0.5', lambda r: r['ratio'] < 0.5), ('0.5-0.9', lambda r: 0.5 <= r['ratio'] < 0.9), ('0.9-1.5', lambda r: 0.9 <= r['ratio'] < 1.5),
      ('1.5-3', lambda r: 1.5 <= r['ratio'] < 3), ('>= 3', lambda r: r['ratio'] >= 3)]
for name, fn in bk:
    rs = [r for r in rows if fn(r)]
    if not rs: print(name, 0); continue
    print('%-14s towns %2d | median grain idx %.2f | median shelf days %.1f | ceiling(idx>=9.9) %d | floor(idx<=0.31) %d | people %.2f M | dem %.0f sup %.0f' % (
        name, len(rs), st.median(r['idx'] for r in rs), st.median(r['days'] for r in rs if not math.isnan(r['days'])),
        sum(1 for r in rs if r['idx'] >= 9.9), sum(1 for r in rs if r['idx'] <= 0.31), sum(r['people'] for r in rs) / 1e6, sum(r['dem'] for r in rs), sum(r['sup'] for r in rs)))
