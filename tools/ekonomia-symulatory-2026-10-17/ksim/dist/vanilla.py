# Vanilla+ROT (pre-KS) grain catchment per town: wheat-farm villages (plon-wsi 109076 baza_reguly, scaled to Towary wheat_farm 6989/d)
# vs town demand proportional to BK consumption (ludnosc-bk zuzycie_bk), total = townsfolk 4187 + town crafts/workshops 1207 (baseline Towary 109195). Read-only.
import csv, collections, statistics as st, sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
D = r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30'
ks = {r['id']: r for r in csv.DictReader(open(HERE + '/towns_109179.tsv', encoding='utf-8'), delimiter='\t')}
import geo
names, land, sea, vbound, pos = geo.load()
G = collections.defaultdict(dict)
for a, nb in land.items():
    for b, w in nb.items(): G[a][b] = w
T = list(ks)
dl = {t: geo.dijkstra(G, t) for t in T}
trade = {}
for v, b in vbound.items():
    if b in ks: trade[v] = b; continue
    c = [(dl[t].get(v), t) for t in T if dl[t].get(v) is not None]
    trade[v] = min(c)[1] if c else None
pw = [r for r in csv.DictReader(open(D + '/plon-wsi.csv', encoding='utf-8'), delimiter=';') if r['dzien'] == '109076']
sw = sum(float(r['baza_reguly']) for r in pw if r['typ'] == 'wheat_farm')
S = collections.defaultdict(float)
for r in pw:
    if r['typ'] == 'wheat_farm' and trade.get(r['wies_id']): S[trade[r['wies_id']]] += float(r['baza_reguly']) * 6989.0 / sw
bk = {r['osada_id']: float(r['zuzycie_bk']) for r in csv.DictReader(open(D + '/ludnosc-bk.csv', encoding='utf-8'), delimiter=';') if r['dzien'] == '109075' and r['rodzaj'] == 'miasto'}
tb = sum(bk.values())
Dm = {t: 5394.0 * bk.get(t, 0) / tb for t in T}
rat = {t: (S[t] / Dm[t] if Dm[t] > 0 else 99) for t in T}
bk2 = [('< 0.5', lambda x: x < 0.5), ('0.5-0.9', lambda x: 0.5 <= x < 0.9), ('0.9-1.5', lambda x: 0.9 <= x < 1.5), ('1.5-3', lambda x: 1.5 <= x < 3), ('>= 3', lambda x: x >= 3)]
print('VANILLA (pre-KS): wheat-farm grain to own town vs BK-consumption-weighted town demand (totals %.0f / %.0f szt/d)' % (sum(S.values()), sum(Dm.values())))
for nm, fn in bk2:
    ts = [t for t in T if fn(rat[t])]
    print('  own S/D %-8s towns %2d  dem %5.0f sup %5.0f | same towns in KS: median KS ratio %.2f, KS grain idx median %.2f' % (nm, len(ts), sum(Dm[t] for t in ts), sum(S[t] for t in ts),
          st.median(float(ks[t]['ratio']) for t in ts) if ts else 0, st.median(float(ks[t]['idx']) for t in ts) if ts else 0))
# overlap: KS short towns that were also vanilla short
kss = {t for t in T if float(ks[t]['ratio']) < 0.5}; vs = {t for t in T if rat[t] < 0.5}
print('  KS short (<0.5) %d, vanilla short %d, both %d' % (len(kss), len(vs), len(kss & vs)))
print('  towns with zero wheat farms in own catchment (vanilla): %d' % sum(1 for t in T if S[t] == 0))
# concentration: share of grain supply in top-10 town catchments; demand-weighted mismatch = sum|S/sumS - D/sumD|/2
def conc(Sd, Dd):
    sS = sum(Sd.values()); sD = sum(Dd.values())
    top = sorted(Sd.values(), reverse=True)[:10]
    mis = sum(abs(Sd.get(t, 0) / sS - Dd.get(t, 0) / sD) for t in T) / 2
    return sum(top) / sS, mis
ksS = {t: float(ks[t]['sup']) for t in T}; ksD = {t: float(ks[t]['dem']) for t in T}
print('  concentration top-10 catchments / mismatch index (share of supply that must move to other towns): vanilla %.2f / %.2f ; KS %.2f / %.2f' % (conc(S, Dm) + conc(ksS, ksD)))
print('  -> grain that must leave its own catchment: vanilla %.0f szt/d of %.0f ; KS %.0f szt/d of %.0f' % (conc(S, Dm)[1] * sum(Dm.values()), sum(Dm.values()), conc(ksS, ksD)[1] * sum(ksD.values()), sum(ksD.values())))
