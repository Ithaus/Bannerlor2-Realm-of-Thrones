# Catchment prep for the offline catchment-Plan prototype: town list (97, market), village -> home town (TradeBound approx.),
# village -> towns within wagon range (land road), town -> town distances (land; land + sea x0.5). Read-only on scratch data.
import csv, json, sys, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), 'dist'))
import geo
D = r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30'
RANGE = 250.0
names, land, sea, vbound, pos = geo.load()
km = list(csv.DictReader(open(D + '/ks-miasta.csv', encoding='utf-8'), delimiter=';'))
towns = {}
for r in km: towns[r['miasto_id']] = dict(name=r['miasto'], mk=0 if r['rynek'] == 'Westeros' else 1)
T = sorted(towns)
G = collections.defaultdict(dict)
for a, nb in land.items():
    for b, w in nb.items(): G[a][b] = w
S2 = collections.defaultdict(dict)
for a, nb in sea.items():
    for b, w in nb.items(): S2[a][b] = w * 0.5
dl = {t: geo.dijkstra(G, t) for t in T}
ds = {t: geo.dijkstra(G, t, S2) for t in T}
dump = json.load(open(os.path.join(HERE, '..', '..', 'ks', 'ks_dump.json')))
vil = {}
nohome = 0
for d in dump['dist']:
    vid = d['id']
    if d['type'] == 'town_fields':
        vil[vid] = dict(home=vid.split('#')[0], near=[]); continue
    b = vbound.get(vid)
    home = None
    if b in towns: home = b
    else:
        best = None
        for t in T:
            x = dl[t].get(vid)
            if x is None: continue
            if best is None or x < best[0]: best = (x, t)
        if best is None:
            for t in T:
                x = ds[t].get(vid)
                if x is None: continue
                if best is None or x < best[0]: best = (x, t)
        home = best[1] if best else None
    if home is None:
        # id pattern ROT_townN_villageK
        p = vid.rsplit('_village', 1)[0]
        home = p if p in towns else None
    if home is None: nohome += 1
    near = []
    for t in T:
        x = dl[t].get(vid)
        if x is not None and x <= RANGE: near.append([t, round(x, 1)])
    hd = dl[home].get(vid) if home else None
    vil[vid] = dict(home=home, near=near, dhome=(round(hd, 1) if hd is not None else None))
tt = {}
for a in T:
    row = {}
    for b in T:
        if a == b: continue
        x = dl[a].get(b); y = ds[a].get(b)
        if x is None and y is None: continue
        row[b] = [round(x, 1) if x is not None else -1, round(y, 1) if y is not None else -1]
    tt[a] = row
out = dict(towns=[dict(id=t, name=towns[t]['name'], mk=towns[t]['mk']) for t in T], vil=vil, tt=tt)
json.dump(out, open(os.path.join(HERE, 'catch.json'), 'w'), separators=(',', ':'))
c = collections.Counter(v['home'] for v in vil.values())
print('towns', len(T), 'districts', len(vil), 'no home', nohome, 'towns with no district', [towns[t]['name'] for t in T if c[t] == 0])
print('villages with >1 town in range', sum(1 for v in vil.values() if len(v['near']) > 1), 'mean towns in range', sum(len(v['near']) for v in vil.values()) / len(vil))
