# Kupcy zbozowi (food merchants) - offline town-level simulator of grain distribution with price-driven carts.
# Read-only on the round-2 autotest CSV and the scratch road graph. Usage:
#   python -I sim.py [--no-merchants] [--days N] [--start 109123|109193|target] [--world 1.0] [--margin 0.05] [--freight 0.015]
#                    [--speed 41] [--sea 1.0] [--maxdays 30] [--share 0.25] [--war] [--wagons 0.4] [--csv out.csv]
# DESIGN RUN (round 3, KS-K3 - corridor contracts + arbitrage, stock-controlled ramp, Plan stock controller):
#   python -I sim.py --days 120 --ramp --hold --wagons 0.4 --ctrl --cap 1.2 --damp 1.0 --rule 5 --measured 0 --arbmax 15 --arbkeep 4.5
#                    --freight 0.015 --world 1.0 --planmargin 1.03 --ctrlmax 0.03 --pipe 5          (add --no-merchants for the round-2 baseline)
#   rule 5 = weekly min-cost transport contracts (Plan step 5) executed daily with c = clamp(1 + (D(T*+Lbar) - pos)/(3F), 0, 2),
#            export floor ShelfDays(I_start+0.3), price check, then nearest-cheapest arbitrage for what is left (<= arbmax days).
#   --good fish --start-index 1.0 --keep 7 --maxdays 7 --arbmax 7 --reach 7: perishable variant (demand pattern assumed like grain).
# Model (one good, grain; pieces of the game per day):
#   inflow_t   = own catchment to market (towns_109179.tsv 'sup', Plan of day 109179) x world scale; with --wagons w a share w of each
#                catchment's flow goes to the cheapest town within 250 road units (village wagons, MarketCarts.Choose), the rest to its own town
#   offtake_t  = min(stock_t, paid demand dem_t)  (ZC4 D-term, ks-miasta 'zboze_popyt_zaplacony')
#   index_t    = clamp((0.14 * stock/dem + 2/(dem*Value))^-0.6, 0.1, 10)   (vanilla steady state, research 3)
#   merchants  = each day, every source s with stock above its keep (T_keep days) offers share x surplus; each (s,d) pair in the same
#                market within maxdays of road (land, else land+sea, else straight) is ranked by profit per piece
#                  pi = V * (I_exp_d - I_s * (1 + kappa*L) - f*L) - m * V * I_s,     I_exp_d = index of d at arrival (stock + incoming - dem*L)
#                and shipped up to the arbitrage point I_d^eq = I_s * (1 + m + kappa*L) + f*L (inventory position target dem*(T_eq + L)), damped 0.5.
#   carts      = arrive after ceil(L / speed) days; durable grain - no loss.
#   money      = dst coffer pays src coffer q * V * I_s; freight q * V * f * L to carters (source catchment villages).
import sys, os, csv, math, collections, argparse, heapq
HERE = os.path.dirname(os.path.abspath(__file__))
DIST = os.path.join(os.path.dirname(HERE), 'dist')
sys.path.insert(0, DIST)
import geo

ap = argparse.ArgumentParser()
ap.add_argument('--no-merchants', action='store_true')
ap.add_argument('--days', type=int, default=120)
ap.add_argument('--start', default='109123')
ap.add_argument('--world', type=float, default=-1.0, help='scale catchment supply so that market supply/demand = this (per market); -1 = as Plan')
ap.add_argument('--margin', type=float, default=0.05)
ap.add_argument('--freight', type=float, default=0.015, help='freight per day of road, share of base value')
ap.add_argument('--kappa', type=float, default=0.005, help='capital cost per day, share of purchase price')
ap.add_argument('--speed', type=float, default=41.0)
ap.add_argument('--sea', type=float, default=1.0, help='sea leg length factor (1.0 = cart speed at sea, as TradeCarts.DaysFor)')
ap.add_argument('--maxdays', type=int, default=30)
ap.add_argument('--share', type=float, default=0.25)
ap.add_argument('--keep', type=float, default=3.6, help='days of paid demand a source keeps (start shelf of grain, index 1.5)')
ap.add_argument('--war', action='store_true', help='Dothraki towns cannot trade (Horde at war)')
ap.add_argument('--wagons', type=float, default=0.0, help='share of catchment flow carried by village wagons to the cheapest town within 250')
ap.add_argument('--offtake', type=float, default=1.0, help='realized offtake / D-term when goods are on the shelf')
ap.add_argument('--csv', default='')
ap.add_argument('--good', default='grain', help='grain | fish (fish: own-catchment fish of towns_109179.tsv, demand = grain demand pattern scaled to the market fish supply, 7-day haul, spoilage)')
ap.add_argument('--spoil', type=float, default=0.03)
ap.add_argument('--reach', type=int, default=0, help='Plan step 5b: demand of towns with no surplus source within REACH days is removed (moved to substitutes); supply rescaled')
ap.add_argument('--start-index', type=float, default=-1)
ap.add_argument('--rule', type=int, default=2)
ap.add_argument('--hold', action='store_true', help='ramp hold (RampDaily): supply beta stops rising when shelves + in transit >= start shelf + pipeline target')
ap.add_argument('--pipe', type=float, default=3.5, help='pipeline target in days of market demand (ramp hold)')
ap.add_argument('--ctrlmax', type=float, default=0.05)
ap.add_argument('--ncand', type=int, default=200)
ap.add_argument('--damp', type=float, default=0.5)
ap.add_argument('--tau', type=float, default=3.0)
ap.add_argument('--measured', type=int, default=1, help='contracts from measured inflow EMA (wagon arrivals) instead of the Plan catchment')
ap.add_argument('--every', type=int, default=7)
ap.add_argument('--arbmax', type=int, default=10)
ap.add_argument('--arbkeep', type=float, default=3.6)
ap.add_argument('--pricecheck', type=int, default=1)
ap.add_argument('--cap', type=float, default=2.0, help='merchants fill a destination at most to cap x start shelf (+ haul days)')
ap.add_argument('--ctrl', action='store_true', help='Plan stock controller every 7 days: market supply x clamp(1 + (target - shelves - transit)/(28 D), 0.95, 1.05)')
ap.add_argument('--ramp', action='store_true', help='KS ramp: supply D-14..D, demand D..D+14 from the vanilla level')
ap.add_argument('--vanilla', type=float, default=0.34, help='vanilla grain flow / KS flow (9.4k / 27.7k)')
ap.add_argument('--planmargin', type=float, default=1.0, help='Plan margin on market demand until D+28 (round 2: 1.03)')
ap.add_argument('--quiet', action='store_true')
a = ap.parse_args()

VAL = 3.0
LOG = r'D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30'
rows = list(csv.DictReader(open(os.path.join(DIST, 'towns_109179.tsv'), encoding='utf-8'), delimiter='\t'))
T = [r['id'] for r in rows]; R = {r['id']: r for r in rows}; N = len(T); ix = {t: i for i, t in enumerate(T)}
mk = [R[t]['mk'] for t in T]
sup = [float(R[t]['sup']) for t in T]
dem = [max(0.0, float(R[t]['dem'])) for t in T]
if a.good == 'fish':
    VAL = 10.0
    fs = [float(R[t]['fish']) for t in T]
    for m in ('Westeros', 'Essos'):
        S_ = sum(fs[i] for i in range(len(T)) if R[T[i]]['mk'] == m); D_ = sum(dem[i] for i in range(len(T)) if R[T[i]]['mk'] == m)
        for i in range(len(T)):
            if R[T[i]]['mk'] == m: dem[i] = dem[i] * S_ / D_
    sup = fs
name = [R[t]['name'] for t in T]
# initial shelves
km = collections.defaultdict(dict)
for r in csv.DictReader(open(LOG + '/ks-miasta.csv', encoding='utf-8'), delimiter=';'):
    km[r['dzien']][r['miasto_id']] = r
if a.start == 'target':
    stock = [d * a.keep for d in dem]
else:
    stock = [float(km[a.start][t]['zboze_polka_szt']) if t in km[a.start] else 0.0 for t in T]
# world scale per market
scale = [1.0] * N
if a.world > 0:
    for m in ('Westeros', 'Essos'):
        s = sum(sup[i] for i in range(N) if mk[i] == m); d = sum(dem[i] for i in range(N) if mk[i] == m)
        for i in range(N):
            if mk[i] == m: scale[i] = a.world * d / s
# road distances: land; else land+sea; else straight x 1.0 (TradeCarts.Road)
names, land, sea, vbound, pos = geo.load()
G = collections.defaultdict(dict)
for u, nb in land.items():
    for v, w in nb.items(): G[u][v] = w
S2 = collections.defaultdict(dict)
for u, nb in sea.items():
    for v, w in nb.items(): S2[u][v] = w * a.sea
dl = {t: geo.dijkstra(G, t) for t in T}
ds = {t: geo.dijkstra(G, t, S2) for t in T}
L = [[0] * N for _ in range(N)]; Lunits = [[0.0] * N for _ in range(N)]; kind = [[''] * N for _ in range(N)]
for i, s in enumerate(T):
    for j, d in enumerate(T):
        if i == j: continue
        x = dl[s].get(d); k = 'lad'
        if x is None: x = ds[s].get(d); k = 'morze'
        if x is None:
            ps, pd = pos.get(s), pos.get(d)
            x = math.hypot(ps[0] - pd[0], ps[1] - pd[1]) if ps and pd else 1e9; k = 'prosta'
        Lunits[i][j] = x; L[i][j] = max(1, int(math.ceil(x / a.speed))); kind[i][j] = k
if a.reach > 0:
    surplus = [i for i in range(N) if sup[i] > dem[i]]
    cut = 0.0; cutn = 0
    for d in range(N):
        if sup[d] > dem[d]: continue
        if not any(L[s_][d] <= a.reach and mk[s_] == mk[d] for s_ in surplus):
            cut += dem[d]; cutn += 1; dem[d] = 0.0
    for m in ('Westeros', 'Essos'):
        S_ = sum(sup[i] for i in range(N) if mk[i] == m); D_ = sum(dem[i] for i in range(N) if mk[i] == m)
        for i in range(N):
            if mk[i] == m: sup[i] *= D_ / S_
    print('reach rule: %d towns beyond %d days of any surplus source, demand %.0f/d moved to substitutes; supply rescaled to reachable demand' % (cutn, a.reach, cut))
war = set()
if a.war:
    for i in range(N):
        if name[i] in ('Vaes Dothrak', 'Vaes Diaf'): war.add(i)
# wagons: share of catchment flow to the cheapest town within 250 land units (else own)
near250 = [[j for j in range(N) if j != i and mk[j] == mk[i] and dl[T[i]].get(T[j]) is not None and dl[T[i]][T[j]] <= 250.0] for i in range(N)]

cand = [sorted([s for s in range(N) if s != d and mk[s] == mk[d] and L[s][d] <= a.maxdays], key=lambda s: L[s][d])[:a.ncand] for d in range(N)]

def transport(srcs, snks, cost):
    # min-cost flow (successive shortest paths, Bellman-Ford queue) on a bipartite graph; returns {(s, d): flow}
    S = list(srcs); K = list(snks); n = 2 + len(S) + len(K)
    gr = [[] for _ in range(n)]
    def add(u, v, c, w):
        gr[u].append([v, c, w, len(gr[v])]); gr[v].append([u, 0.0, -w, len(gr[u]) - 1])
    for i, u in enumerate(S): add(0, 2 + i, srcs[u], 0.0)
    for j, v in enumerate(K): add(2 + len(S) + j, 1, snks[v], 0.0)
    edge = {}
    for i, u in enumerate(S):
        for j, v in enumerate(K):
            c = cost(u, v)
            if c is not None:
                edge[(u, v)] = (2 + i, len(gr[2 + i])); add(2 + i, 2 + len(S) + j, 1e18, c)
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
    flows = {}
    for (u, v), (node, k) in edge.items():
        e = gr[node][k]; fl = 1e18 - e[1]
        if fl > 1e-6: flows[(u, v)] = fl
    return flows

def spoilf(l):
    return 1 - (1 - a.spoil) ** l if a.good == 'fish' else 0.0

def index(st, d):
    if d <= 0: return 1.0
    x = 0.14 * st / d + 2.0 / (d * VAL)
    return max(0.1, min(10.0, x ** -0.6))

def teq(I, d):
    # shelf days at which index = I (inverse of index)
    if I >= 10: return 0.0
    c0 = 2.0 / (max(1e-9, d) * VAL)
    return max(0.0, (I ** (-1 / 0.6) - c0) / 0.14)

carts = []   # (day_due, dst, q)
incoming = [0.0] * N
inc = [collections.defaultdict(float) for _ in range(N)]   # dst -> due day -> pieces
stats = collections.defaultdict(float)
out = []
START_I = 1.5 if a.start_index < 0 else a.start_index
TFLOOR = teq(START_I + 0.3, 1e9)   # shelf days at the top of the start band - an exporting town never sells below it (export ban in dearth)
V0 = a.vanilla
ctrl = [1.0] * N
win_m = {m: collections.defaultdict(float) for m in ('Westeros', 'Essos')}
ema = [-1.0] * N
contracts = None
ceilcount = [0] * N; emptycount = [0] * N; unmetT = [0.0] * N
beta = {'Westeros': 0.0, 'Essos': 0.0}
day0 = -14 if a.ramp else 0
if a.ramp:
    stock = [2.0 * V0 * d for d in dem]
for day in range(day0, a.days):
    rr = min(1.0, max(0.0, (day + 14) / 14.0)); ll = min(1.0, max(0.0, day / 14.0))
    sD = V0 + (1 - V0) * ll if a.ramp else 1.0
    if a.ramp:
        if a.hold and day < 14:
            for m in ('Westeros', 'Essos'):
                ii = [i for i in range(N) if mk[i] == m]
                Dfull = sum(dem[i] for i in ii)
                have = sum(stock[i] for i in ii) + sum(c[2] for c in carts if mk[c[1]] == m)
                tgt = Dfull * (a.keep + a.pipe)
                # stock-controlled ramp: beta = demand ramp + (target - stock) / (7 d x KS flow above vanilla), within [lambda, r]
                b = ll + (tgt - have) / (7.0 * Dfull * (1 - V0))
                beta[m] = max(ll, min(rr, b))
        else:
            beta['Westeros'] = beta['Essos'] = max(ll, rr)
    sSm = {m: (V0 + (1 - V0) * beta[m]) if a.ramp else 1.0 for m in ('Westeros', 'Essos')}
    pm = a.planmargin if day < 28 else 1.0
    if a.ctrl and day >= 0 and day % 7 == 0:
        for m in ('Westeros', 'Essos'):
            ii = [i for i in range(N) if mk[i] == m]
            Dm = sum(dem[i] * sD for i in ii)
            have = sum(stock[i] for i in ii) + sum(c[2] for c in carts if mk[c[1]] == m)
            pipe = sum(c[2] for c in carts if mk[c[1]] == m)
            want = Dm * a.keep + pipe
            f = max(1 - a.ctrlmax, min(1 + a.ctrlmax, 1.0 + (want - have) / (28.0 * Dm)))
            for i in ii: ctrl[i] = f
    # arrivals
    spoiled_day = 0.0
    keep = []
    for c in carts:
        if c[0] <= day:
            stock[c[1]] += c[2] * (1 - spoilf(c[3]) if len(c) > 3 else 1.0); spoiled_day += c[2] * (spoilf(c[3]) if len(c) > 3 else 0.0); incoming[c[1]] -= c[2]; inc[c[1]][c[0]] -= c[2]; stats['arrived'] += c[2]
            if inc[c[1]][c[0]] <= 1e-6: del inc[c[1]][c[0]]
        else: keep.append(c)
    carts = keep
    # production (with village wagons choosing the dearest town nearby)
    arr = [0.0] * N
    prod_day = 0.0
    for i in range(N):
        q = sup[i] * scale[i] * sSm[mk[i]] * pm * ctrl[i]
        prod_day += q
        if a.wagons > 0 and near250[i]:
            jb = max(near250[i] + [i], key=lambda j: index(stock[j], dem[j] * sD) if j not in war or j == i else -1)
            stock[jb] += q * a.wagons; stock[i] += q * (1 - a.wagons); arr[jb] += q * a.wagons; arr[i] += q * (1 - a.wagons)
        else: stock[i] += q; arr[i] += q
    for i in range(N): ema[i] = arr[i] if ema[i] < 0 else ema[i] + 0.25 * (arr[i] - ema[i])
    # merchants
    shipped = 0.0; lots = 0; gold = 0.0; freight = 0.0; tonday = 0.0
    if not a.no_merchants:
        Dd = [dem[i] * sD for i in range(N)]
        def due_by(d, until):
            return sum(v for k, v in inc[d].items() if k <= until)
        avail = [max(0.0, stock[i] - Dd[i] * max(TFLOOR, a.keep if a.rule == 1 else TFLOOR)) * a.share if i not in war else 0.0 for i in range(N)]
        if a.rule in (4, 5):
            if day % a.every == 0 or contracts is None:
                # corridor contracts (Plan step 5 / weekly): net flows from expected inflow (own catchment + wagons) vs paid demand, min ton-days
                inflow = list(ema) if a.measured else [sup[i] * scale[i] * sSm[mk[i]] * pm * ctrl[i] for i in range(N)]
                contracts = {}
                for m in ('Westeros', 'Essos'):
                    ii = [i for i in range(N) if mk[i] == m and i not in war]
                    src = {i: inflow[i] - Dd[i] for i in ii if inflow[i] > Dd[i] + 1}
                    snk = {i: Dd[i] - inflow[i] for i in ii if Dd[i] > inflow[i] + 1}
                    contracts.update(transport(src, snk, lambda u, v: L[u][v] if L[u][v] <= a.maxdays else None))
            byd = collections.defaultdict(list)
            for (s_, d_), f in contracts.items(): byd[d_].append((L[s_][d_], s_, f))
            for d, lst in byd.items():
                F = sum(f for _, _, f in lst); Lbar = sum(l * f for l, _, f in lst) / max(1e-9, F)
                pos = stock[d] + incoming[d]
                tpos = Dd[d] * (a.keep + Lbar)
                c = max(0.0, min(2.0, 1.0 + (tpos - pos) / (a.tau * max(1.0, F))))
                for l, s, f in sorted(lst):
                    q = f * c
                    q = min(q, max(0.0, stock[s] - Dd[s] * TFLOOR))
                    if q < 1: continue
                    Is = index(stock[s], Dd[s]); ie = index(max(0.0, stock[d] + due_by(d, day + l) - Dd[d] * l), Dd[d])
                    if a.pricecheck and ie - Is * (1 + a.kappa * l) - a.freight * l - a.margin * Is <= 0: continue
                    q = float(int(q))
                    stock[s] -= q; incoming[d] += q; inc[d][day + l] += q; carts.append((day + l, d, q, l))
                    shipped += q; lots += 1; gold += q * VAL * Is; freight += q * VAL * a.freight * l; tonday += q * l
        rounds = 3 if a.rule == 2 else (1 if a.rule in (3, 5) else 0)
        if a.rule == 5:
            # arbitrage after contracts: what is left above the export floor, nearest-first within arbmax days
            avail = [max(0.0, stock[i] - Dd[i] * max(TFLOOR, a.arbkeep)) * a.share if i not in war else 0.0 for i in range(N)]
        for rnd in range(rounds):
            # dearest destination first (highest bidder): price at the shelf the town will have in 2 days with carts due by then
            order = sorted((-index(max(0.0, stock[d] + due_by(d, day + 2) - Dd[d] * 2), Dd[d]), d) for d in range(N) if Dd[d] > 0 and d not in war)
            for _, d in order:
                opts = []
                for s in cand[d]:
                    if avail[s] <= 1: continue
                    l = L[s][d]
                    if a.rule == 5 and l > a.arbmax: continue
                    Is = index(stock[s], Dd[s])
                    opts.append((Is * (1 + a.kappa * l) + a.freight * l, s, l, Is))
                opts.sort()
                for c, s, l, Is in (opts[:1] if a.rule == 2 else opts):
                    if avail[s] <= 1: continue
                    Is = index(stock[s], Dd[s]); c = Is * (1 + a.kappa * l) + a.freight * l
                    ie = index(max(0.0, stock[d] + due_by(d, day + l) - Dd[d] * l), Dd[d])
                    if ie - c - a.margin * Is <= 0: break
                    ieq = Is * (1 + a.margin + a.kappa * l) + a.freight * l
                    tgt = Dd[d] * (min(teq(ieq, Dd[d]), a.cap * a.keep) + l)
                    gap = (tgt - stock[d] - due_by(d, day + l)) * a.damp
                    q = min(avail[s], gap)
                    if q < max(5.0, 0.25 * Dd[d]): continue
                    q = float(int(q))
                    avail[s] -= q; stock[s] -= q; incoming[d] += q; inc[d][day + l] += q; carts.append((day + l, d, q, l))
                    shipped += q; lots += 1; gold += q * VAL * Is; freight += q * VAL * a.freight * l; tonday += q * l
    # consumption
    unmet = 0.0; eaten = 0.0
    for i in range(N):
        want = dem[i] * a.offtake * sD
        e = min(stock[i], want); stock[i] -= e; eaten += e; unmet += want - e
    # metrics
    Dsum = sum(dem); idx = [index(stock[i], dem[i] * sD) for i in range(N)]; days_ = [stock[i] / (dem[i] * sD) if dem[i] > 0 else 0 for i in range(N)]
    inband = sum(dem[i] for i in range(N) if abs(idx[i] - START_I) <= 0.3) / Dsum
    ceil = sum(1 for i in range(N) if idx[i] >= 9.9); floor = sum(1 for i in range(N) if idx[i] <= 0.11)
    lt05 = sum(dem[i] for i in range(N) if days_[i] < 0.5) / Dsum
    srt = sorted(days_); q10, q50, q90 = srt[N // 10], srt[N // 2], srt[9 * N // 10]
    isrt = sorted(idx); i10, i50, i90 = isrt[N // 10], isrt[N // 2], isrt[9 * N // 10]
    transit = sum(c[2] for c in carts)
    if 22 <= day <= 49:
        for m in ('Westeros', 'Essos'):
            ii = [i for i in range(N) if mk[i] == m]
            Dm_ = sum(dem[i] for i in ii)
            win_m[m]['inband'] += sum(dem[i] for i in ii if abs(idx[i] - START_I) <= 0.3) / Dm_
            win_m[m]['med'] += sorted(idx[i] for i in ii)[len(ii) // 2]
            win_m[m]['shelf'] += sorted(days_[i] for i in ii)[len(ii) // 2]
            win_m[m]['lt05'] += sum(dem[i] for i in ii if days_[i] < 0.5) / Dm_
            win_m[m]['ceil'] += sum(1 for i in ii if idx[i] >= 9.9) / len(ii)
            win_m[m]['n'] += 1
            win_m[m]['lt2'] += sum(1 for i in ii if days_[i] < 2.0) / len(ii)
            win_m[m]['zero'] += sum(1 for i in ii if stock[i] < 1) / len(ii)
        for i in range(N):
            if idx[i] >= 9.9: ceilcount[i] += 1
            if days_[i] < 0.5: emptycount[i] += 1
            unmetT[i] += max(0.0, 0.0)
    out.append(dict(day=day, inband=inband, ceil=ceil, floor=floor, lt05=lt05, q10=q10, q50=q50, q90=q90, i10=i10, i50=i50, i90=i90,
                    shipped=shipped, prod=prod_day, spoiled=spoiled_day, lots=lots, gold=gold, freight=freight, transit=transit, unmet=unmet, eaten=eaten, stock=sum(stock), tonday=tonday))
if a.csv:
    with open(a.csv, 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=list(out[0].keys()), delimiter=';'); w.writeheader(); [w.writerow(r) for r in out]
if not a.quiet:
    print('start %s, world %s, merchants %s, margin %.2f freight %.3f/d kappa %.3f speed %.0f sea x%.1f maxdays %d share %.2f keep %.1f d war %s wagons %.2f offtake %.2f' % (
        a.start, a.world, not a.no_merchants, a.margin, a.freight, a.kappa, a.speed, a.sea, a.maxdays, a.share, a.keep, a.war, a.wagons, a.offtake))
    print('day | D in band 1.2-1.8 | ceil floor | D<0.5d | shelf days q10/q50/q90 | index q10/q50/q90 | shipped/d lots | in transit | unmet/d | stock')
    for r in out:
        if r['day'] in (-14, -7, 0, 1, 2, 3, 5, 7, 10, 14, 21, 28, 35, 42, 49, 56, 70, 90, 119) and r['day'] < a.days:
            print('%3d | %5.1f%% | %2d %2d | %5.1f%% | %5.1f %5.1f %6.1f | %5.2f %5.2f %5.2f | %6.0f %3d | %7.0f | %6.0f | %7.0f' % (
                r['day'], 100 * r['inband'], r['ceil'], r['floor'], 100 * r['lt05'], r['q10'], r['q50'], r['q90'], r['i10'], r['i50'], r['i90'],
                r['shipped'], r['lots'], r['transit'], r['unmet'], r['stock']))
    w = [r for r in out if 22 <= r['day'] <= 49] or out
    n = len(w)
    print('window D+22..D+49 (sim days 22-49): D in band %.1f%%, towns at ceiling %.1f, floor %.1f, D in towns <0.5 d %.1f%%, shipped %.0f/d in %.0f lots, in transit %.0f, merchant gold %.0f zl/d, freight %.0f zl/d, piece-days %.0f, unmet %.0f/d (%.1f%% of D)' % (
        100 * sum(r['inband'] for r in w) / n, sum(r['ceil'] for r in w) / n, sum(r['floor'] for r in w) / n, 100 * sum(r['lt05'] for r in w) / n,
        sum(r['shipped'] for r in w) / n, sum(r['lots'] for r in w) / n, sum(r['transit'] for r in w) / n, sum(r['gold'] for r in w) / n, sum(r['freight'] for r in w) / n,
        sum(r['tonday'] for r in w) / n, sum(r['unmet'] for r in w) / n, 100 * sum(r['unmet'] for r in w) / n / sum(dem)))
    # first day from which >= 80% of D is in band for 7 days in a row
    first = None
    for k in range(len(out) - 6):
        if all(out[k + j]['inband'] >= 0.8 for j in range(7)): first = out[k]['day']; break
    print('first day with >= 80%% of D in band for 7 days: %s' % first)
    print('window supply/consumption on the market %.3f (production %.0f/d, eaten %.0f/d, spoiled in transit %.0f/d; S/(eaten+spoiled) %.3f)' % (sum(r['prod'] for r in w) / max(1, sum(r['eaten'] for r in w)), sum(r['prod'] for r in w) / n, sum(r['eaten'] for r in w) / n, sum(r['spoiled'] for r in w) / n, sum(r['prod'] for r in w) / max(1, sum(r['eaten'] + r['spoiled'] for r in w))))
    for m in ('Westeros', 'Essos'):
        w_ = win_m[m]; n_ = max(1, w_['n'])
        print('  %s window: median town index %.2f, median shelf %.1f d, D in band %.1f%%, D in towns < 0.5 d %.1f%%, towns at ceiling %.1f%%, towns with shelf < 2 d %.1f%%, towns with no grain %.1f%%' % (m, w_['med'] / n_, w_['shelf'] / n_, 100 * w_['inband'] / n_, 100 * w_['lt05'] / n_, 100 * w_['ceil'] / n_, 100 * w_['lt2'] / n_, 100 * w_['zero'] / n_))
    kinds = collections.Counter()
    for i in range(N):
        for j in range(N):
            if i != j and mk[i] == mk[j]: kinds[kind[i][j]] += 1
    print('pair road kinds (same market):', dict(kinds))
if os.environ.get('SIMDIAG'):
    print('towns with shelf < 0.5 d on >= 7 of 28 window days:', ', '.join('%s (D %.0f, %d d, haul from nearest Plan-surplus town %s d)' % (name[i], dem[i], emptycount[i], min([L[s][i] for s in range(N) if s != i and mk[s] == mk[i] and sup[s] > dem[s]] or [99])) for i in sorted(range(N), key=lambda i: -dem[i]) if emptycount[i] >= 7))
    idx = [index(stock[i], dem[i]) for i in range(N)]
    print('towns at ceiling at end:')
    for i in sorted(range(N), key=lambda i: -dem[i]):
        if idx[i] >= 9.9:
            srcs = sorted((L[s][i], name[s]) for s in range(N) if s != i and mk[s] == mk[i] and stock[s] > dem[s] * a.keep)
            print('  %-18s %-8s dem %6.0f sup %6.0f stock %7.0f incoming %7.0f | nearest sources (days): %s' % (name[i], mk[i], dem[i], sup[i] * scale[i], stock[i], incoming[i], ', '.join('%s %d' % (n, l) for l, n in srcs[:3])))
    big = sorted(range(N), key=lambda i: -stock[i])[:8]
    print('biggest stocks:', ', '.join('%s %.0f (%.0f d)' % (name[i], stock[i], stock[i] / max(1, dem[i])) for i in big))
