# Transport problem: town surplus (own catchment grain to market - paid demand) -> town deficits, min-cost max-flow under range limits. Read-only.
import csv, sys, os, collections, math
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import geo
TSV = HERE + '/towns_' + (sys.argv[1] if len(sys.argv) > 1 else '109179') + '.tsv'
SPEED = float(sys.argv[2]) if len(sys.argv) > 2 else 41.0     # road units per day (3.43 x 24 x WorldPace 50%)
names, land, sea, vbound, pos = geo.load()
rows = list(csv.DictReader(open(TSV, encoding='utf-8'), delimiter='\t'))
T = [r['id'] for r in rows]; R = {r['id']: r for r in rows}
G = collections.defaultdict(dict)
for a, nb in land.items():
    for b, w in nb.items(): G[a][b] = w
SEA_F = 0.5   # sea leg counted as half the road length (ships about 2x faster - project ch. 5)
S2 = collections.defaultdict(dict)
for a, nb in sea.items():
    for b, w in nb.items(): S2[a][b] = w * SEA_F
dl = {t: geo.dijkstra(G, t) for t in T}
ds = {t: geo.dijkstra(G, t, S2) for t in T}

def mcmf(sources, sinks, cost):
    # sources: {node: cap}, sinks: {node: cap}, cost(u, v) -> distance or None. Successive shortest path (SPFA).
    N = ['S', 'K'] + list(sources) + list(sinks)
    idx = {('s', u): i + 2 for i, u in enumerate(sources)}
    off = 2 + len(sources)
    for j, v in enumerate(sinks): idx[('k', v)] = off + j
    n = off + len(sinks)
    gr = [[] for _ in range(n)]   # edges: [to, cap, cost, rev]
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
            for k, e in enumerate(gr[u]):
                if e[1] > 1e-9 and dist[u] + e[2] < dist[e[0]] - 1e-12:
                    dist[e[0]] = dist[u] + e[2]; prev[e[0]] = (u, k)
                    if not inq[e[0]]: q.append(e[0]); inq[e[0]] = True
        if dist[1] == math.inf: break
        f = math.inf; v = 1
        while v != 0:
            u, k = prev[v]; f = min(f, gr[u][k][1]); v = u
        v = 1
        while v != 0:
            u, k = prev[v]; e = gr[u][k]; e[1] -= f; gr[e[0]][e[3]][1] += f; v = u
        flow += f; tc += f * dist[1]
    return flow, tc

for mk in ('Westeros', 'Essos', None):
    sel = [t for t in T if mk is None or R[t]['mk'] == mk]
    src = {}; snk = {}
    for t in sel:
        s = float(R[t]['sup']); d = float(R[t]['dem'])
        if s > d: src[t] = s - d
        elif d > s: snk[t] = d - s
    need = sum(snk.values()); have = sum(src.values())
    print('== %s: towns %d, deficit towns %d need %.0f szt/d, surplus towns %d have %.0f szt/d' % (mk or 'world (cross-market by sea)', len(sel), len(snk), need, len(src), have))
    for label, dd, lim in (('land <= 250 (village wagon / 215 range)', dl, 250), ('land <= 500', dl, 500), ('land, any distance', dl, 1e9), ('land + sea (sea x0.5), any', ds, 1e9)):
        f, c = mcmf(src, snk, lambda u, v: (dd[u].get(v) if dd[u].get(v) is not None and dd[u].get(v) <= lim else None))
        avg = c / f if f > 0 else 0
        print('  %-40s covered %6.0f szt/d (%5.1f%%) | avg haul %5.0f units = %4.1f days | load in transit %7.0f szt (x days one way)' % (label, f, 100 * f / need if need else 0, avg, avg / SPEED, c / SPEED))
    # towns with no surplus town reachable by land at any distance
    unreach = [t for t in snk if not any(dl[u].get(t) is not None for u in src)]
    if unreach: print('  deficit towns with no surplus town by land at all:', ', '.join('%s %.0f' % (R[t]['name'], snk[t]) for t in sorted(unreach, key=lambda t: -snk[t])))
