# Design 3 estimate: if the Plan balanced each kraina (region) instead of each continent, how much grain must still move
# (a) between regions (corridors) and (b) between towns inside a region; how much of it a neighbour trade within N days can carry.
# Supply per region = min(s_k, 1.0) x paid demand of the region (s_k = old model kcal self-sufficiency, kraina variant),
# spread over the region's towns like today's own-catchment supply (or by village count when the region sells ~0 today). Read-only.
import csv, sys, os, collections, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), 'dist'))
import geo
TSV = os.path.join(os.path.dirname(HERE), 'dist', 'towns_109179.tsv')
SPEED = 41.0
OLD = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/roz/kalibracja/ks_krainy_v2_kraina_kosz_in1.25_lowy.tsv'
MAP = {'Reach': 'reach', 'Volantis': 'volantine', 'Qarth': 'qartheen', 'Westerlands': 'vlandia', 'Dorzecze': 'river', 'Dolina': 'vale', 'Polnoc': 'battania',
       'Zatoka Niewolnicza': 'ghiscari', 'Braavos': 'empire', 'Krainy Burzy': 'stormlands', 'Norvos': 'norvos', 'Pentos': 'pentoshi', 'Qohor': 'qohorik', 'Dorne': 'aserai',
       'Lys': 'lyseni', 'Myr': 'myrish', 'Tyrosh': 'tyroshi', 'Smocza Skala': 'dragonstone', 'Lorath': 'nord', 'Dothrakowie': 'khuzait', 'Ziemie Korony': 'crownlands',
       'Zelazne Wyspy': 'sturgia', 'Ibben': 'ibbenese', 'Wolni Ludzie': 'freefolk', 'Valyria': 'valyrian', 'Sarnor': 'sarnor', 'Wyspy Letnie': 'summer', 'Yi Ti': 'yiti',
       'Skagos': 'skagosi', 'Nocna Straz': 'nightswatch'}
s_old = {}
for r in csv.DictReader(open(OLD, encoding='utf-8'), delimiter='\t'):
    k = MAP.get(r['kraina'])
    if k: s_old[k] = float(r['samowystarczalnosc_kcal'])
names, land, sea, vbound, pos = geo.load()
rows = list(csv.DictReader(open(TSV, encoding='utf-8'), delimiter='\t'))
T = [r['id'] for r in rows]; R = {r['id']: r for r in rows}
G = collections.defaultdict(dict)
for a, nb in land.items():
    for b, w in nb.items(): G[a][b] = w
S2 = collections.defaultdict(dict)
for a, nb in sea.items():
    for b, w in nb.items(): S2[a][b] = w * 0.5
dl = {t: geo.dijkstra(G, t) for t in T}
ds = {t: geo.dijkstra(G, t, S2) for t in T}
SLACK = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
EXTRA = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0


def reg(t):
    k = R[t]['kraina']
    return k.split(',')[0] if k and k not in ('-', '?', '') else '?'


for t in T:
    if reg(t) == '?':
        best = None
        for u in T:
            if u == t or reg(u) == '?': continue
            d = ds[t].get(u)
            if d is not None and (best is None or d < best[0]): best = (d, u)
        R[t]['kraina'] = reg(best[1]) if best else '?'
byk = collections.defaultdict(list)
for t in T: byk[reg(t)].append(t)
sup = {}; dem = {t: float(R[t]['dem']) for t in T}
corr_need = 0; corr_rows = []
for k, ts in byk.items():
    D = sum(dem[t] for t in ts); S = sum(float(R[t]['sup']) for t in ts)
    s = s_old.get(k, 1.0); target = min(s, SLACK) * D
    corr_need += max(0.0, D - target)
    corr_rows.append((k, len(ts), D, S, s, target))
    w = {t: float(R[t]['sup']) for t in ts}
    if S < 0.05 * D:
        w = {t: float(R[t]['nvil']) + 1e-6 for t in ts}
    tw = sum(w.values())
    for t in ts: sup[t] = target * w[t] / tw if tw > 0 else 0.0
print('REGION BALANCE (grain szt/d): region | towns | paid demand | supply today (continental Plan) | old kcal self-sufficiency s | regional supply')
for r in sorted(corr_rows, key=lambda r: r[4]):
    print('  %-12s %2d %7.0f %7.0f %5.2f %7.0f' % r)
print('corridor (inter-region) need = sum max(0, D - min(s,%.2f) D) = %.0f szt/d of %.0f' % (SLACK, corr_need, sum(dem.values())))


def mcmf(sources, sinks, cost):
    idx = {('s', u): i + 2 for i, u in enumerate(sources)}; off = 2 + len(sources)
    for j, v in enumerate(sinks): idx[('k', v)] = off + j
    n = off + len(sinks); gr = [[] for _ in range(n)]

    def add(u, v, c, w):
        gr[u].append([v, c, w, len(gr[v])]); gr[v].append([u, 0.0, -w, len(gr[u]) - 1])
    for u, c in sources.items(): add(0, idx[('s', u)], c, 0.0)
    for v, c in sinks.items(): add(idx[('k', v)], 1, c, 0.0)
    for u in sources:
        for v in sinks:
            d = cost(u, v)
            if d is not None: add(idx[('s', u)], idx[('k', v)], 1e18, d)
    flow = 0.0; tc = 0.0
    while True:
        dist = [math.inf] * n; inq = [False] * n; prev = [None] * n
        dist[0] = 0.0; q = collections.deque([0]); inq[0] = True
        while q:
            u = q.popleft(); inq[u] = False
            for k2, e in enumerate(gr[u]):
                if e[1] > 1e-9 and dist[u] + e[2] < dist[e[0]] - 1e-12:
                    dist[e[0]] = dist[u] + e[2]; prev[e[0]] = (u, k2)
                    if not inq[e[0]]: q.append(e[0]); inq[e[0]] = True
        if dist[1] == math.inf: break
        f = math.inf; v = 1
        while v != 0:
            u, k2 = prev[v]; f = min(f, gr[u][k2][1]); v = u
        v = 1
        while v != 0:
            u, k2 = prev[v]; e = gr[u][k2]; e[1] -= f; gr[e[0]][e[3]][1] += f; v = u
        flow += f; tc += f * dist[1]
    return flow, tc


LAB = (('land<=250', dl, 250), ('land<=7d(287)', dl, 287), ('land+sea<=7d', ds, 287), ('land+sea<=14d', ds, 574), ('any land+sea', ds, 1e9))
tot_need = 0; cov = collections.defaultdict(float); cost = collections.defaultdict(float)
for k, ts in byk.items():
    src = {t: sup[t] - dem[t] for t in ts if sup[t] > dem[t]}
    snk_raw = {t: dem[t] - sup[t] for t in ts if dem[t] > sup[t]}
    need = sum(snk_raw.values()); have = sum(src.values())
    if need <= 0 or have <= 0: continue
    f_s = min(1.0, have / need)
    snk = {t: v * f_s for t, v in snk_raw.items()}
    tot_need += sum(snk.values())
    for label, dd, lim in LAB:
        f, c = mcmf(src, snk, lambda u, v: (dd[u].get(v) if dd[u].get(v) is not None and dd[u].get(v) <= lim else None))
        cov[label] += f; cost[label] += c
print('INTRA-REGION redistribution need (own catchment vs regional supply, inside each region): %.0f szt/d' % tot_need)
for label, _, _ in LAB:
    f = cov[label]; c = cost[label]
    print('  %-14s covered %6.0f (%5.1f%%), avg haul %4.0f units = %4.1f d, in transit %6.0f szt' % (label, f, 100 * f / tot_need if tot_need else 0, c / f if f else 0, (c / f if f else 0) / SPEED, c / SPEED))
# corridor: region deficits served by surplus regions (D - target where s<1 vs surplus of regions with s>1 at s capped 1.15)
src = {}; snk = {}
for k, ts in byk.items():
    D = sum(dem[t] for t in ts); s = s_old.get(k, 1.0)
    if s < 1.0:
        # deficit lands into their biggest-demand town
        t = max(ts, key=lambda t: dem[t]); snk[t] = snk.get(t, 0) + (1.0 - s) * D
    elif s > 1.0:
        t = max(ts, key=lambda t: float(R[t]['sup'])); src[t] = src.get(t, 0) + min(s - 1.0 + EXTRA, 9.0) * D
print('CORRIDORS: deficit lands need %.0f szt/d, surplus lands (s-1 capped 0.25) offer %.0f szt/d' % (sum(snk.values()), sum(src.values())))
for label, dd, lim in LAB:
    f, c = mcmf(src, snk, lambda u, v: (dd[u].get(v) if dd[u].get(v) is not None and dd[u].get(v) <= lim else None))
    print('  %-14s covered %6.0f (%5.1f%%), avg haul %4.0f units = %4.1f d, in transit %6.0f szt' % (label, f, 100 * f / sum(snk.values()), c / f if f else 0, (c / f if f else 0) / SPEED, c / SPEED))
print('NEAREST SURPLUS SOURCE per deficit land (land+sea, sea x0.5):')
for t, need in sorted(snk.items(), key=lambda kv: -kv[1]):
    best = min(((ds[u].get(t, 1e18), u) for u in src), default=(1e18, None))
    bl = min(((dl[u].get(t, 1e18), u) for u in src), default=(1e18, None))
    print('  %-22s %-11s need %5.0f | nearest source %-18s %5.0f units = %4.1f d | by land %s' % (R[t]['name'], reg(t), need, R[best[1]]['name'] if best[1] else '-', best[0], best[0] / SPEED,
          ('%5.0f' % bl[0]) if bl[0] < 1e17 else 'none'))
