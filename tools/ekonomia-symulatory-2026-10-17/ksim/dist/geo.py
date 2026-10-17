# Geo: road graph (land) between fortresses + villages; positions. Read-only on scratch data.
import csv, heapq, collections, os
ROZ = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/roz'
GEO = ROZ + '/geo-dane'

def load():
    routes = list(csv.DictReader(open(GEO + '/geo-trasy.tsv', encoding='utf-8'), delimiter='\t'))
    names = {}
    land = collections.defaultdict(dict)   # node -> node -> length (land)
    sea = collections.defaultdict(dict)
    vbound = {}
    for r in routes:
        a, b = r['od'], r['do']; names[a] = r['od_nazwa']; names[b] = r['do_nazwa']
        try: L = float(r['dl_gry'])
        except: L = None
        if r['polaczenie'] == 'lad' and L is not None:
            if b not in land[a] or land[a][b] > L:
                land[a][b] = L; land[b][a] = L
        elif r['polaczenie'] == 'morze':
            try: Ls = float(r['dl_morze'])
            except: Ls = L
            if Ls is not None:
                sea[a][b] = Ls; sea[b][a] = Ls
        if r['rodzaj'] == 'twierdza-wies':
            vbound[b] = a
    # positions: first / last point of each route
    pos = {}
    first = {}; last = {}
    rid = {r['trasa']: (r['od'], r['do']) for r in routes}
    with open(GEO + '/geo-trasy-punkty.tsv', encoding='utf-8') as f:
        rd = csv.reader(f, delimiter='\t'); next(rd)
        for row in rd:
            t = row[0]
            if t not in rid: continue
            x, y = float(row[2]), float(row[3])
            if t not in first: first[t] = (x, y)
            last[t] = (x, y)
    for t, (a, b) in rid.items():
        if t in first and a not in pos: pos[a] = first[t]
        if t in last and b not in pos: pos[b] = last[t]
    return names, land, sea, vbound, pos

def dijkstra(graph, src, extra=None):
    dist = {src: 0.0}; pq = [(0.0, src)]
    while pq:
        d, u = heapq.heappop(pq)
        if d > dist.get(u, 1e18): continue
        for v, w in graph.get(u, {}).items():
            nd = d + w
            if nd < dist.get(v, 1e18):
                dist[v] = nd; heapq.heappush(pq, (nd, v))
        if extra:
            for v, w in extra.get(u, {}).items():
                nd = d + w
                if nd < dist.get(v, 1e18):
                    dist[v] = nd; heapq.heappush(pq, (nd, v))
    return dist
