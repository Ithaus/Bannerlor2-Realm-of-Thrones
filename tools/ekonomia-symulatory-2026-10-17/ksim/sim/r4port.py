# KS-R3 PORT - the rules as IMPLEMENTED in n-ks (w-toku/noc-ks3, febf2779 and later), ported from the C# code into the town simulator (sim.py).
# Python 3 stdlib, run with python -I. Reads world.json / vfit.json, the Plan harness dumps (kmoney/h3port/out/*.csv), the round-3 CSV dir (kasy-miast.csv)
# and ks_dump.json (town lands). Nothing in the game folder is touched.
#
#   python -I r3port.py [--seeds 3] [--days 62] [--plan-on PATH] [--plan-off PATH] [--init start|split|r3] [--tag NAME] [opt=value ...]
#
# What is ported (C# file -> here):
#   KsFoodCarts.Daily/Arrivals/Ship/LotPrice/PayCarters (G1 KS-H) -> class KsH: 12 nearest same-market towns by cart days, slot every 2nd day (Fnv & 1),
#       order Dz = max(paid D-term at lambda 1, offtake EMA7) x kx (kx x1.10 on a day with stall <= 6 pieces and paid demand, else x0.98; 1..2.5),
#       source offer floor(0.5 x (shelf - 1.25 T* Dz - 5)), destination only if shelf + incoming < (T* + lim + 2) Dz, sorted by destination index (dearest first),
#       landed cost I_s (1 + tau L), I_d >= 1.03 c, up to 3 sources, q = min(offer, (T* + L + 2) Dz - H), skip q < max(5, Dz/2), 3-point lot price at the source,
#       freight round(cost x tau x L) (land 0.010 -> villages of the source town by people, top 8 + rest to the largest; sea 0.005 -> source town's labourers),
#       money cap 15% x (town gold - merchants' reserve) per destination per day, arrival on day + max(1, L); goods and gold identities.
#   TownFolk.Ks.cs Zc4BuyCore/ZcLadder/ZcTake/ZcTake2/OwnFirst (G2, G3) + TownFolk.Live.cs (H1 every 2nd day with lam x 2, G4 old-list cut) -> village_buy.
#   StartCalibration.Zc4.cs ZcTake H2 (stall absent at the day's map = no goods all day; KS-H arrival clears it) -> self.row0.
#   StartCalibration.Zc4.cs DerOf (P3: ZC5 inputs only for what the hands make) and One = EMA28 of lords' net purchases + courts at the town (castles out).
#   StartCalibration.Crafts.cs (G5: ZC5 input above 6 days of direct need + 5, output up to 4 days of what the town buys).
#   StartCalibration.R3.cs MarketControl/LandGlutWeekly (P4): C28/U28 EMA28 of market consumption and unmet paid demand, gap to the shelf target +-5%,
#       factor step +-7% per Plan, 0.85..1.10, from the first Plan at or after D+14 (Plans every 7 days: 109109, 109116, ...); EMULATION of the effect: the market
#       output of the good moves to factor / 1.03 of the Apply Plan's output (the Plan's step 8 moves home-work hands), districts adopt in 7-day rotation,
#       cuts weighted x3 in glut lands (G '+' attribution), raises in proportion to home work (G '-'), bounded by home work 0..0.85.
#   StartCalibration.Ramp.cs RampDaily (P5 beta hold: market stock incl. KS-H in transit >= T* x paid D-term + pipeline -> supply held at max(lambda, beta)).
#   Plan side (step 5 corridors and land gradient, reach rule, step 8b substitute, alpha_net, market correction) - NOT re-implemented: the C# Plan core itself
#       (KsCore.Solve in kmoney/h3port) is run on ks_dump.json and its per-district result is transferred onto the simulator's villages as ratios ON/OFF:
#       to-market output per good, Plan-paid share per basket item, town paid share, and the Plan's uncovered kcal share per village (home share = 1 - paid - uncovered).
# Round 2 (17.10, C# c1f61987): ZcTake2 wanted kcal once (kwfix), KS-H further towns / whole coffer on a short stall (candx, res_short), NKS-22 counters as
#   909aba37, alpha_net remeasure in the C# form as an option (remeasure=2; the sim cannot re-solve the Plan weekly, so the reference stays remeasure=1),
#   sim calibration qvcal=3 (non-grain food D-term per market = the ON Plan's paid demand) and world_r2.json (sea links of Lys and Evenfall Hall),
#   KS-H decision counters (dbg=1, diag_r2a.py). Plan dumps of round 2: kmoney/h3port/out/r2_8c2.csv (krok 8c), r2_w2.csv (909aba37 core, world_r2).
# Round 3 (17.10, C# after c1f61987): pass 2 of the village ladder only above 1.5 x the stall target (p2k, C# StartCalibration.ZPass2Floor) and pass-2 pieces out of
#   the KS-H 7-day offtake (off_p2=0, C# KsTakeP2 / TkP2); Plan step 8d (byproduct table: Egg, honey, carrot, apple, game meat over the market's paid demand stay on the
#   producing district's table) and the town-level perishable transport (measurement only; gradient TownGrad off) come with the Plan dump (kmoney/h3port/out/r3_f1.csv;
#   r3_f1i.csv = the same with the 1:1 demand of the round-3 ledger, KS_ITEMS1 = out/items_r3.json). Per-market sink counters c_sgm (diag_r3g.py), per-town diag
#   diag_r3t.py, perishable reach bound diag_r3p.py, Plan-dump uncovered comparison diag_unc.py. Sim-only options: p2flow / p2save (pass 2 from the purse's flow
#   surplus), ctrl_p2 (controller without pass 2). Outputs: r3/*.out, r3/final/*.out.
# ROUND 4 (17.10 evening, C# after 86bc5756 - review of round 3, 41 issues): ship speed (sea days = road / (4 x cart speed) + 1 port day; ship=1), freight tau land
#   0.025 / sea 0.0025 per day (tau, taus), kx only on a real stockout (shelf + in transit < (T* + L_min) x Dz; frozen while in transit >= L_min x Dz; kx4=1), source keep
#   from Dz without kx, coffer: reserve as floor - on a short stall of grain and salt at most down to 0.5 x reserve and at most +5% of the reserve a day (res4=1),
#   merchants' profit check (landed unit cost <= 0.97 x destination unit price, up to 3 halvings; profit=1), spoilage of perishables 1.5%/day of haul (spoil=1),
#   pass 2 by price (stall price <= 0.8 x start price, shelf above 0.8^(-1/0.6) = 1.45 x target; p2k=1.45, p2price=1), controller without pass 2 (ctrl_p2=0) and only
#   until D+49 (ctrl_end; then the factor goes back to 1 over 2 Plans), H2 only while the stall still has no stock (h2arr=1), town coffers clamped at 0 (gold0=1).
#   Plan dump of round 4: kmoney/h4/out/m1.csv (C# core with step 5 after step 8 on paid demand, one coffer budget per land, physical substitute 8b/8c, land-priced
#   baskets, step 8d inside the step-8 fixed point incl. livestock meat, ledger 1:1 KS_ITEMS1).
import json, sys, os, math, statistics, time, collections, copy, csv, random
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import sim as S

RAW14 = ['grain', 'fish', 'meat', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'honey', 'salt']
PERISH = {'fish', 'meat', 'Egg', 'butter', 'apple', 'carrot', 'orange', 'grape', 'date_fruit'}
BAND_EXC = {'carrot', 'apple', 'honey', 'Egg'}
KSB_IDS = ["grain", "bread", "pie", "meat", "WhaleMeat", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "oil", "salt", "honey",
           "spice", "beer", "wine", "mead", "linen", "felt", "leather", "tools", "pottery", "whale_oil", "hardwood", "fur", "flax", "wool", "hides", "iron", "clay", "limestone", "marble"]
MEAT_OF_LIVE = 0.5
SCRATCH = os.path.dirname(os.path.dirname(HERE))
H3OUT = os.path.join(SCRATCH, 'kmoney', 'h3port', 'out')
KSDUMP = os.path.join(SCRATCH, 'ks', 'ks_dump.json')

DEF = dict(carts=1, paidfirst=1, ownfirst=1, leak=1, leakk=0.61, zc5=1, realoff=1, ctrl=1, every2=1, h2=1, glut=1, beta=1, price_blend=1, dist_k_carts=1.18,
           tau=0.025, taus=0.0025, margin=0.03, srckeep=1.25, offer=0.5, kxup=1.10, kxdown=0.98, kxmax=2.5, coffer=0.15, maxsrc=3, ncand=12, maxd=30.0, maxp=7.0,
           D=109118, p2flow=0, p2save=0.001, p2k=1.45, off_p2=0, ctrl_p2=0, ctrl_from=14, plan_first=109109, money_cap=1, fix1=1, res_short=0.0, ctrl_step=0.07, ctrl_gap=0.05, remeasure=1, an_flow=0, ctrl_u=1, candx=1, candx_n=4, kwfix=1, qvcal=3,
           ship=1, kx4=1, res4=1, profit=1, spoil=1, p2price=1, ctrl_end=49, h2arr=1, gold0=1)
# round 3 as committed (86bc5756): tau=0.010 taus=0.005 p2k=1.5 ctrl_p2=1 ship=0 kx4=0 res4=0 profit=0 spoil=0 p2price=0 ctrl_end=999 h2arr=0 gold0=0
R3_RULES = dict(tau=0.010, taus=0.005, p2k=1.5, ctrl_p2=1, ship=0, kx4=0, res4=0, profit=0, spoil=0, p2price=0, ctrl_end=999, h2arr=0, gold0=0)
# febf2779 as committed: fix1=0 ctrl_u=0 and the Plan dump on_an1.csv (step 8 G down to -HwV); sim round 1 fixes (C#): fix1=1 (C28/U28 restart at D+14, >= 5 days),
# ctrl_u=1 (unmet paid demand only while the market's shelves are below target), Plan dump s1_on.csv (G '-' only above the village's own home work)
# sim round 2 (C# commit after 909aba37): kwfix=1 (ZcTake2 - wanted kcal not counted again in near towns), candx=1 (KS-H: up to 4 further towns when none of the 12
# nearest can send), res_short=0.0 (KS-H: short stall - the whole coffer, 15%/d); sim calibration: qvcal=3 (non-grain food D-term per market = the ON Plan's
# paid demand - Zc4Build takes the Plan's baskets), world_r2.json (sea links of Lys and Evenfall Hall). 909aba37 = kwfix=0 candx=0 res_short=1.0
# sim round 3 (C# after c1f61987): p2k=1.5 (TownFolk.ZcTake2 - pass 2 only above 1.5 x stall target, StartCalibration.ZPass2Floor), off_p2=0 (pass-2 pieces out of the
# KS-H 7-day offtake, StartCalibration.KsTakeP2 / TkP2); Plan step 8d (byproduct table) and the town-level perishable transport are in the Plan dump
# (kmoney/h3port/out/r3_f1.csv; r3_f1i.csv = the same with the 1:1 demand of the round-3 ledger, KS_ITEMS1). c1f61987 = p2k=1.0 off_p2=1.
# Options only in the sim (not in C#): p2flow (pass 2 from the purse's flow surplus), ctrl_p2 (controller without pass 2).


def fnv(s):
    h = 2166136261
    for c in s: h = ((h ^ ord(c)) * 16777619) & 0xffffffff
    return h


def tstar(I):
    return 7.0 * max(0.3, I) ** (-1.0 / 0.6)


def load_dump(path):
    D = {}; T = {}; G = {}
    hdr = None
    with open(path, encoding='utf-8') as f:
        for line in f:
            r = line.rstrip('\n').split(';')
            if r[0] == 'D':
                if r[1] == 'id': hdr = r; continue
                D[r[1]] = {hdr[j]: (float(r[j]) if j >= 2 else r[j]) for j in range(1, len(r))}
            elif r[0] == 'T':
                T[r[1]] = [float(x) for x in r[3:]]
            elif r[0] == 'G':
                G[r[1]] = {kv.split('=')[0]: float(kv.split('=')[1]) for kv in r[2:]}
    return D, T, G


def kg_of(row, g):
    if g == 'meat':
        return row.get('mk_meat', 0) + row.get('mk_WhaleMeat', 0) + MEAT_OF_LIVE * (row.get('mk_cow', 0) + row.get('mk_hog', 0) + row.get('mk_sheep', 0))
    return row.get('mk_' + g, 0.0)


_KGPP = None
def kg_per_piece(csvdir):
    """kg of the Plan per game piece (ks-plan.csv: na_targ_kg_d / na_targ_szt_d) per good - for districts that start a good the round-3 Plan did not make."""
    global _KGPP
    if _KGPP is not None: return _KGPP
    acc = collections.defaultdict(lambda: [0.0, 0.0])
    live = {'cow': 6.0, 'hog': 2.0, 'sheep': 1.0, 'WhaleMeat': 1.0}
    with open(os.path.join(csvdir, 'ks-plan.csv'), encoding='utf-8') as f:
        rd = csv.reader(f, delimiter=';'); hdr = next(rd)
        ig, ikg, ipc = hdr.index('towar'), hdr.index('na_targ_kg_d'), hdr.index('na_targ_szt_d')
        for r in rd:
            g = r[ig]
            try: kg = float(r[ikg]); pc = float(r[ipc])
            except ValueError: continue
            if pc <= 0: continue
            if g in live: acc['meat'][0] += kg * MEAT_OF_LIVE if g != 'WhaleMeat' else kg; acc['meat'][1] += pc * live[g]
            else: acc[g][0] += kg; acc[g][1] += pc
    _KGPP = {g: (a[0] / a[1] if a[1] > 0 else 10.0) for g, a in acc.items()}
    return _KGPP


class KsH:
    """G1 KS-H, ported from KsFoodCarts.cs (decision math, money cap, lot price, freight routes, arrivals)."""
    def __init__(self, sim):
        self.sim = sim; o = sim.o; NT = sim.NT
        self.ord = sorted(range(NT), key=lambda t: sim.T[t]['id'])       # C# town index = StringId order
        self.rank = {t: i for i, t in enumerate(self.ord)}
        dl = sim.W['dland']; ds = sim.W['dsea']; sp = sim.P['speed']; dk = o['dist_k_carts']
        self.days = [[-1.0] * NT for _ in range(NT)]; self.sea = [[False] * NT for _ in range(NT)]
        for a in range(NT):
            for b in range(NT):
                if a == b or sim.T[a]['mk'] != sim.T[b]['mk']: continue
                l = dl[a][b]; s = ds[a][b]
                land_ok = l >= 0; all_ok = s >= 0
                sea = (not land_ok) or (all_ok and l > 1.15 * s)
                road = (s if all_ok else -1) if sea else l
                if road < 0: continue
                if sea and o.get('ship', 0): self.days[a][b] = max(1.0, math.ceil(road * dk / (sp * 4.0))) + 1.0     # round 4: ship 4 x cart speed + 1 port day
                else: self.days[a][b] = max(1.0, math.ceil(road * dk / sp))
                self.sea[a][b] = sea
        self.cand = []; self.cand_all = []
        for t in range(NT):
            l = [(self.days[s][t], self.rank[s], s) for s in range(NT) if s != t and sim.T[s]['mk'] == sim.T[t]['mk'] and self.days[s][t] >= 0]
            l.sort()
            self.cand.append([s for (_, _, s) in l[:int(o['ncand'])]])
            self.cand_all.append([s for (_, _, s) in l[int(o['ncand']):]])     # round-2 fix candx: the rest by days (searched only when the nearest have nothing)
        self.slot = [fnv(sim.T[t]['id']) & 1 for t in range(NT)]
        self.items = [sim.GI[g] for g in RAW14]
        self.per = [g in PERISH for g in RAW14]
        self.kx = [[1.0] * len(RAW14) for _ in range(NT)]
        self.carts = []                                   # [due, src, dst, k, n]
        self.inc = [[0] * len(RAW14) for _ in range(NT)]
        self.prev_transit = 0
        # villages of each source town (TradeBound): top 8 by people, rest to the largest
        self.vil = []
        for t in range(NT):
            vs = sorted([k for k, v in enumerate(sim.Vl) if v['trade'] == t], key=lambda k: -sim.Vl[k]['people'])
            rest = sum(sim.Vl[k]['people'] for k in vs[8:])
            top = [(k, sim.Vl[k]['people'] + (rest if i == 0 else 0.0)) for i, k in enumerate(vs[:8])]
            self.vil.append(top)
        self.ship_ema = [[0.0] * len(RAW14) for _ in (0, 1)]; self.days_ema = [[0.0] * len(RAW14) for _ in (0, 1)]; self.ship_n = 0
        self.c = collections.defaultdict(float)
        self.dbg = {} if o.get('dbg', 0) else None
        self.mar_pct = []; self.cof_short = []
        self._dump = None; D0 = o['D']; self._dump_days = set(D0 + k for k in (20, 23, 27, 30, 34, 37, 41, 44, 48))

    def war(self, a, b):
        sim = self.sim
        ka, kb = sim.T[a]['kingdom'], sim.T[b]['kingdom']
        return ka != kb and (ka in S.WAR_K or kb in S.WAR_K)

    def transit(self):
        return sum(c[4] for c in self.carts)

    def pipeline(self, m, k):
        return self.ship_ema[m][k] * self.days_ema[m][k]

    def lot_price(self, s, i, n):
        sim = self.sim; c = sim.gcat[i]; v = sim.val[i]
        V0 = sum(max(0.0, sim.shelf[s][j]) * sim.val[j] for j in sim.catg[c]); D = sim.Dv[s][c]; Sv = sim.Sv[s][c]
        def P(k): return max(1, math.ceil(v * sim.index(D, Sv, max(0.0, V0 - k * v)) - 1e-9))
        if n < 8: return sum(P(k) for k in range(n))
        m1 = n // 8; m2 = n // 2
        return m1 * P(0) + (m2 - m1) * P(m1) + (n - m2) * P(m2)

    def arrivals(self, day):
        sim = self.sim; keep = []
        for cart in self.carts:
            due, s, t, k, n = cart[:5]
            if day < due: keep.append(cart); continue
            i = self.items[k]
            self.inc[t][k] = max(0, self.inc[t][k] - n)
            if sim.o.get('spoil', 0) and self.per[k]:
                L = max(1.0, due - (cart[5] if len(cart) > 5 else due - 1))
                sp_ = min(n, int(math.floor(n * min(0.5, 0.015 * L))))
                if sp_ > 0: n -= sp_; self.c['spoil'] += sp_; self.c['d_spoil'] += sp_
            if sim.o.get('profit', 0) and len(cart) > 6:
                up = self._dest_unit(t, i); val = n * up; cost = cart[6]
                self.c['mar_val'] += val; self.c['mar_cost'] += cost; self.c['mar_lots'] += 1
                if val < cost: self.c['mar_loss'] += 1
                if cost > 0: self.mar_pct.append((val - cost) / cost)
            sim.shelf[t][i] += n
            sim.row0[t][i] = False                          # ZRowDirty: H2 row reset - villages see it
            self.c['arrived'] += n; self.c['d_arr'] += n
        self.carts = keep

    def _dest_unit(self, t, i):
        sim = self.sim
        up = sim.val[i] * sim.idx[t][sim.gcat[i]]
        if sim.shelf[t][i] <= 5: up = min(up, 1.5 * sim.start_idx[sim.T[t]['mk']][i] * sim.val[i])   # C# ZPrice cap of an empty stall
        return max(1, math.ceil(up - 1e-9))

    def _dump_state(self, day):
        """round 4 port test (kmoney/port4, C# KsFoodCore): state of the day before the KS-H decisions, towns in C# order (StringId = rank)."""
        sim = self.sim; NT = sim.NT; ordr = self.ord; NK = len(RAW14)
        cats = sorted(set(sim.gcat[i] for i in self.items))
        js = {'day': day, 'nt': NT, 'nk': NK, 'mk': [sim.T[t]['mk'] for t in ordr],
              'review': [self.slot[t] == (day & 1) for t in ordr], 'siege': [False] * NT,
              'sh': [[int(max(0.0, sim.shelf[t][i])) for i in self.items] for t in ordr], 'shf': [[max(0.0, sim.shelf[t][i]) for i in self.items] for t in ordr],
              'inc': [[self.inc[t][k] for k in range(NK)] for t in ordr],
              'ix': [[sim.idx[t][sim.gcat[i]] for i in self.items] for t in ordr],
              'dp': [[sim.dp[t][i] for i in self.items] for t in ordr], 'off': [[sim.off7[t][i] for i in self.items] for t in ordr],
              'kx': [[self.kx[t][k] for k in range(NK)] for t in ordr],
              'ts': [[tstar(sim.start_idx[m][i]) for i in self.items] for m in (0, 1)],
              'per': self.per, 'staple': [g in ('grain', 'salt') for g in RAW14],
              'gold': [sim.town_gold(t) for t in ordr], 'res': [sim.town_reserve(t) for t in ordr],
              'cand': [[self.rank[s] for s in self.cand[t]] for t in ordr], 'candfar': [[self.rank[s] for s in self.cand_all[t]] for t in ordr],
              'days': [[self.days[s][t] for t in ordr] for s in ordr], 'sea': [[self.sea[s][t] for t in ordr] for s in ordr],
              'war': [[self.war(s, t) for t in ordr] for s in ordr],
              'val': [sim.val[i] for i in self.items], 'startidx': [[sim.start_idx[m][i] for i in self.items] for m in (0, 1)],
              'cat': [cats.index(sim.gcat[i]) for i in self.items],
              'catD': [[sim.Dv[t][c] for c in cats] for t in ordr], 'catS': [[sim.Sv[t][c] for c in cats] for t in ordr],
              'catV0x': [[sum(max(0.0, sim.shelf[t][j]) * sim.val[j] for j in sim.catg[c] if j not in self.items) for c in cats] for t in ordr],
              'c0': sim.P['c0'], 'margin': sim.o['margin'], 'lots': []}
        self._dump = js

    def daily(self, day):
        sim = self.sim; o = sim.o; NT = sim.NT; NK = len(RAW14)
        self.c['d_bought'] = 0; self.c['d_arr'] = self.c.get('d_arr', 0)
        self._dump = None
        if o.get('portdump', 0) and day in self._dump_days: self._dump_state(day)
        sh = [[int(max(0.0, sim.shelf[t][i])) for i in self.items] for t in range(NT)]
        ix = [[sim.idx[t][sim.gcat[i]] for i in self.items] for t in range(NT)]
        dz = [[0.0] * NK for _ in range(NT)]; dzB = [[0.0] * NK for _ in range(NT)]
        Ts = [[tstar(sim.start_idx[m][i]) for i in self.items] for m in (0, 1)]
        for t in range(NT):
            lmin = max(1.0, self.days[self.cand[t][0]][t]) if self.cand[t] else 1.0
            for k, i in enumerate(self.items):
                p = sim.dp[t][i]; off = sim.off7[t][i]
                kxv = self.kx[t][k]; b = max(p, off)
                if o.get('kx4', 0):
                    # round 4 (C# KsFoodCarts): kx up only on a real stockout (shelf + in transit < (T* + L_min) x Dz), frozen while in transit >= L_min x Dz
                    H = sh[t][k] + self.inc[t][k]; T_ = Ts[sim.T[t]['mk']][k]
                    if p > 0 and sh[t][k] <= 6 and H < (T_ + lmin) * b: kxv = min(o['kxmax'], kxv * o['kxup'])
                    elif not (b > 0 and self.inc[t][k] >= lmin * b): kxv = max(1.0, kxv * o['kxdown'])
                else:
                    if p > 0 and sh[t][k] <= 6: kxv = min(o['kxmax'], kxv * o['kxup'])
                    else: kxv = max(1.0, kxv * o['kxdown'])
                self.kx[t][k] = kxv
                dzB[t][k] = b
                dz[t][k] = b * kxv
        budget = [o['coffer'] * max(0.0, sim.town_gold(t) - sim.town_reserve(t)) if o['money_cap'] else 1e18 for t in range(NT)]
        if o.get('res4', 0):
            # round 4: reserve as floor - short stall of grain / salt: down to 0.5 x reserve, at most +5% of the reserve a day over the normal budget
            budget_short = [max(budget[t], min(o['coffer'] * max(0.0, sim.town_gold(t) - 0.5 * sim.town_reserve(t)), budget[t] + 0.05 * sim.town_reserve(t))) if o['money_cap'] else 1e18 for t in range(NT)]
        else:
            budget_short = [o['coffer'] * max(0.0, sim.town_gold(t) - o['res_short'] * sim.town_reserve(t)) if o['money_cap'] else 1e18 for t in range(NT)]
        spent = [0.0] * NT
        offer = [[0.0] * NK for _ in range(NT)]
        for t in range(NT):
            m = sim.T[t]['mk']
            for k in range(NK):
                sur = sh[t][k] - o['srckeep'] * Ts[m][k] * (dzB[t][k] if o.get('kx4', 0) else dz[t][k]) - 5
                if sur > 1: offer[t][k] = math.floor(o['offer'] * sur)
        short_seen = set()
        pend = collections.defaultdict(float)
        for k in range(NK):
            i = self.items[k]; lim = o['maxp'] if self.per[k] else o['maxd']
            dst = []
            for t in range(NT):
                if self.slot[t] != (day & 1): continue
                d = dz[t][k]
                if not d > 0: continue
                H = sh[t][k] + self.inc[t][k]
                if H >= (Ts[sim.T[t]['mk']][k] + lim + 2) * d: continue
                dst.append(t)
            dst.sort(key=lambda t: (-ix[t][k], self.rank[t]))
            for t in dst:
                d = dz[t][k]; T_ = Ts[sim.T[t]['mk']][k]
                Hc = sh[t][k] + self.inc[t][k]
                cand = []
                dbg = self.dbg.setdefault((t, k), collections.Counter()) if self.dbg is not None else None
                for s in self.cand[t]:
                    if offer[s][k] < 1:
                        if dbg is not None: dbg['nooffer'] += 1
                        continue
                    L = self.days[s][t]
                    if not (L >= 0) or L > lim:
                        if dbg is not None: dbg['range'] += 1
                        continue
                    if self.war(s, t):
                        if dbg is not None: dbg['war'] += 1
                        continue
                    tau = o['taus'] if self.sea[s][t] else o['tau']
                    c = ix[s][k] * (1.0 + tau * L)
                    if ix[t][k] < c * (1.0 + o['margin']):
                        if dbg is not None: dbg['margin'] += 1
                        continue
                    cand.append((c, self.rank[s], s))
                candF = []
                if o.get('candx', 0) == 2 or (o.get('candx', 0) == 1 and not cand):
                    # round-2 fix (candx): further towns with an offer in range (same tests), at most candx_n; 1 - only when none of the 12 nearest can send,
                    # 2 - always, taken after the nearest (they get only what the nearest did not fill)
                    for s in self.cand_all[t]:
                        if len(candF) >= int(o.get('candx_n', 4)): break
                        if offer[s][k] < 1: continue
                        L = self.days[s][t]
                        if not (L >= 0) or L > lim: break
                        if self.war(s, t): continue
                        tau = o['taus'] if self.sea[s][t] else o['tau']
                        c = ix[s][k] * (1.0 + tau * L)
                        if ix[t][k] < c * (1.0 + o['margin']): continue
                        candF.append((c, self.rank[s], s))
                    if candF and dbg is not None: dbg['candx'] += 1
                if dbg is not None: dbg['days'] += 1
                if not cand and not candF:
                    if dbg is not None: dbg['nocand'] += 1
                    continue
                cand.sort(); candF.sort(); cand = cand + candF
                used = 0
                short_ = (o['res_short'] < 1.0 or o.get('res4', 0)) and Hc < T_ * d      # round-2 fix (C# KsFoodCarts): short stall - the lot may use the reserve
                if o.get('res4', 0) and RAW14[k] not in ('grain', 'salt'): short_ = False   # round 4: only grain and salt
                if Hc < T_ * d and t not in short_seen:
                    short_seen.add(t); rv = sim.town_reserve(t)
                    if rv > 0: self.cof_short.append(sim.town_gold(t) / rv)
                dunit = self._dest_unit(t, i) if o.get('profit', 0) else 0
                ord0 = -1; got0 = 0; poorHit = False                  # C# 909aba37: NKS-22 counters once per destination x good x day
                for (c, _, s) in cand:
                    if used >= o['maxsrc']: break
                    L = self.days[s][t]
                    want = (T_ + L + 2) * d - Hc
                    q = min(offer[s][k], want)
                    if q < max(5.0, 0.5 * d): continue
                    n = int(math.floor(q))
                    if ord0 < 0: ord0 = n
                    sea = self.sea[s][t]; tau = o['taus'] if sea else o['tau']
                    cost = self.lot_price(s, i, n); fr = round(cost * tau * L)
                    if dunit > 0:
                        # round 4: merchants' profit - landed cost <= 0.97 x destination unit price, else smaller lots (cheaper pieces of the source stall), up to 3 halvings
                        tries = 0
                        while n > 0 and cost + fr > math.floor(n * dunit * (1.0 - o['margin'])) and tries < 3:
                            n //= 2; tries += 1
                            if n < max(5.0, 0.5 * d): n = 0; break
                            cost = self.lot_price(s, i, n); fr = round(cost * tau * L)
                        if n <= 0 or cost + fr > math.floor(n * dunit * (1.0 - o['margin'])): self.c['noprofit'] += 1; continue
                    avail = (max(budget[t], budget_short[t]) if short_ else budget[t]) - spent[t]
                    if cost + fr > avail:
                        nf = int(math.floor(n * avail / (cost + fr))) if cost + fr > 0 and avail > 0 else 0
                        poorHit = True
                        if dbg is not None: dbg['poor'] += 1; dbg['poor_pcs'] += n - max(0, nf)
                        if nf < max(5.0, 0.5 * d): continue
                        n = nf
                        cost = self.lot_price(s, i, n); fr = round(cost * tau * L)
                        if cost + fr > avail + 1: continue
                    if n <= 0 or cost <= 0: continue
                    if sim.town_gold(t) < cost + fr: continue
                    take = min(n, int(max(0.0, sim.shelf[s][i])) - 5)
                    if take <= 0: continue
                    if take < n: cost = round(cost * take / n); fr = round(fr * take / n); n = take
                    sim.shelf[s][i] -= n
                    sim.gold_flow(t, -cost); sim.gold_flow(s, +cost)
                    self.c['g_dst'] += cost; self.c['g_src'] += cost
                    if fr > 0:
                        sim.gold_flow(t, -fr); self.c['g_dst'] += fr
                        if sea: self.c['g_crews'] += fr; self.c['fr_sea'] += fr
                        else: pend[s] += fr; self.c['fr_land'] += fr
                    self.carts.append([day + int(max(1.0, L)), s, t, k, n, day, cost + fr])
                    if self._dump is not None: self._dump['lots'].append([self.rank[s], self.rank[t], k, n, cost, fr])
                    self.inc[t][k] += n
                    m = sim.T[t]['mk']
                    self.c['lots'] += 1; self.c['sea_lots'] += 1 if sea else 0; self.c['bought'] += n; self.c['d_bought'] += n; self.c['gold'] += cost
                    self.c['piece_days'] += n * max(1.0, L); self.c['g_' + RAW14[k]] += n; self.c['sea_pcs'] += n if sea else 0
                    self.ship_day[m][k] += n; self.ship_days_day[m][k] += n * max(1.0, L)
                    used += 1; got0 += n
                    if dbg is not None: dbg['ship'] += 1; dbg['ship_pcs'] += n
                    if spent[t] + cost + fr > max(budget[t], spent[t]): self.c['res_lots'] += 1
                    offer[s][k] -= n; sh[s][k] -= n; Hc += n; spent[t] += cost + fr
                if ord0 > 0:
                    self.c['ordered'] += ord0
                    if poorHit: self.c['poor'] += max(0, ord0 - got0)
        if self._dump is not None:
            pth = os.path.join(SCRATCH, 'kmoney', 'port4', 'snap', 'snap_%d_%d.json' % (sim.seed if hasattr(sim, 'seed') else 0, day))
            json.dump(self._dump, open(pth, 'w', encoding='utf-8')); self._dump = None
        # PayCarters: land freight to the source villages (by people, top 8 + rest to the largest)
        for s, x in pend.items():
            vl = self.vil[s]
            if not vl:
                sim.gold_flow(s, x); self.c['g_carters'] += x; continue
            tot = sum(w for (_, w) in vl); left = x
            for (kk, w) in vl[1:]:
                y = math.floor(x * w / tot) if tot > 0 else 0
                if y <= 0: continue
                sim.gold[kk] += y; sim.c_inflow += y; sim.v_fr[kk] += y; left -= y; self.c['g_carters'] += y
            if left > 0:
                kk = vl[0][0]; sim.gold[kk] += left; sim.c_inflow += left; sim.v_fr[kk] += left; self.c['g_carters'] += left

    def flush(self, day):
        now = self.transit()
        rest = self.prev_transit + self.c['d_bought'] - self.c['d_arr'] - self.c['d_spoil'] - now
        self.c['d_spoil'] = 0
        self.c['goods_rest_abs'] += abs(rest)
        self.c['transit_sum'] += now; self.c['days_n'] += 1
        self.prev_transit = now; self.c['d_arr'] = 0
        self.ship_n += 1; w7 = 1.0 / min(7, self.ship_n)
        for m in (0, 1):
            for k in range(len(RAW14)):
                self.ship_ema[m][k] += (self.ship_day[m][k] - self.ship_ema[m][k]) * w7
                if self.ship_day[m][k] > 0: self.days_ema[m][k] += (self.ship_days_day[m][k] / self.ship_day[m][k] - self.days_ema[m][k]) * (1.0 if self.days_ema[m][k] <= 0 else w7)
                self.ship_day[m][k] = 0.0; self.ship_days_day[m][k] = 0.0

    def start_day(self):
        self.ship_day = [[0.0] * len(RAW14) for _ in (0, 1)]; self.ship_days_day = [[0.0] * len(RAW14) for _ in (0, 1)]


class R3Sim(S.Sim):
    def __init__(self, W, o, seed=1, plan_on=None, plan_off=None, init='split', base_flow=None):
        self.o = o; self.base_flow = base_flow
        self._plan_on = plan_on; self._plan_off = plan_off; self._init = init
        W2 = dict(W)
        W2['villages'] = copy.deepcopy(W['villages']); W2['Q'] = copy.deepcopy(W['Q']); W2['tf'] = copy.deepcopy(W['tf']); W2['castles'] = copy.deepcopy(W['castles'])
        if o.get('qvcal', 0): self._qvcal(W2)
        if o.get('qvcal', 0) >= 2: self._qvcal_market(W2, qvcal_factors(W, o))
        P = {}
        if o['leak']: P['other_rate'] = S.DEFAULTS['other_rate'] * o['leakk']; P['old_salt'] = 0.0
        if o['zc5']: P['zc5_in_days'] = 6.0; P['zc5_out_days'] = 4.0
        st = o['D'] - 14 if init == 'A' else o['D']
        S.Sim.__init__(self, W2, P, trade='caravans', plan='r3' if plan_on else 'current', init='start', start=st, seed=seed)

    @staticmethod
    def _qvcal(W2):
        """Round-2 sim calibration (qvcal): build_world fitted the village Plan-paid shares to the ZC4 world D-term of villages (QV) only through the partial
        shares - villages already at a share of 1 were never lowered, so for fish, meat, cheese, butter and other protein the world QV stayed 40-65% above the
        game's ZC4 line (fish 3035 vs 1834 pcs/d at plan day 109179). Scale every village's share of a non-grain food good down to the ZC4 total, and the
        town baskets Q likewise (shape per village / town kept)."""
        G = W2['goods']; GI = {g: i for i, g in enumerate(G)}
        zq = W2['zc4']['QV']; zt = W2['zc4']['Q']
        for g in ('meat', 'fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'honey'):
            if g not in GI: continue
            i = GI[g]
            tgt = zq.get(g, 0.0) + (zq.get('WhaleMeat', 0.0) if g == 'meat' else 0.0)
            sv = sum(v['bask'][i] * v['paid'][i] for v in W2['villages'])
            if tgt > 0 and sv > 1.02 * tgt:
                f = tgt / sv
                for v in W2['villages']: v['paid'][i] *= f
            tq = zt.get(g, 0.0) + (zt.get('WhaleMeat', 0.0) if g == 'meat' else 0.0)
            sq = sum(r[i] for r in W2['Q'])
            if tq > 0 and sq > 1.02 * tq:
                f = tq / sq
                for r in W2['Q']: r[i] *= f

    @staticmethod
    def _qvcal_market(W2, F):
        """qvcal=2: per market the town and village D-term of a non-grain food good scaled to the round-3 Plan's own paid demand of that market (Plan OFF
        to-market supply / NA TARG) - in the game Zc4Build takes the Plan's paid baskets, so its D-term per market is the Plan's demand by construction."""
        G = W2['goods']; GI = {g: i for i, g in enumerate(G)}
        tmk = [t['mk'] for t in W2['towns']]
        for (m, g), f in F.items():
            i = GI[g]
            for v in W2['villages']:
                if v['mk'] == m: v['paid'][i] = min(1.0, v['paid'][i] * f)
            for t, r in enumerate(W2['Q']):
                if tmk[t] == m: r[i] *= f

    def _qvcal_on(self):
        """qvcal=3: after the Plan transfer the town + village D-term of each non-grain food good per market = the ON Plan's own paid demand (ON to-market /
        NA TARG of the ON harness run) minus ZC5 inputs, lords + courts and castles - the game's Zc4Build takes the Plan's paid baskets."""
        don, _, _ = load_dump(self._plan_on)
        nat = plan_sd_from(self._plan_on.replace('.csv', '.out'))
        kgpp = kg_per_piece(self.W['csv'])
        NT = self.NT; self._qv3 = {}
        for m in (0, 1):
            ts = [t for t in range(NT) if self.T[t]['mk'] == m]
            for g in ('meat', 'fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'honey'):
                i = self.GI[g]
                pl = sum(kg_of(r, g) for r in don.values() if int(r['mk']) == m) / kgpp.get(g, 10.0)
                dt = pl / max(0.5, nat.get((m, g), 1.0))
                qq = sum(self.Q[t][i] + self.QV[t][i] for t in ts)
                fixed = sum(self.der[t][i] + self.one[t][i] for t in ts) + sum(c['dem'][i] for c in self.C if c.get('mk', 0) == m)
                if not (qq > 1.0 and dt > fixed): continue
                f = max(0.25, min(4.0, (dt - fixed) / qq)); self._qv3[(m, g)] = f
                for t in ts: self.Q[t][i] *= f
                for v in self.Vl:
                    if v['mk'] == m: v['paid'][i] = min(1.0, v['paid'][i] * f)
        self.QV = [[0.0] * self.NG for _ in range(NT)]
        for v in self.Vl:
            t = v['trade']
            for i in range(self.NG): self.QV[t][i] += v['bask'][i] * v['paid'][i]
        self._dp_refresh()

    # ------------------------------------------------------------------ Plan transfer (C# Plan core result, harness dumps ON / OFF)
    def apply_plan(self, plan):
        don, ton, gon = load_dump(self._plan_on); doff, toff, _ = load_dump(self._plan_off)
        self.plan_grad = gon
        kgpp = kg_per_piece(self.W['csv'])
        GI = self.GI
        self._pl_unc = {}; self._pl_hw = {}; self._pl_homekcal = {}
        self._out_base = [list(r) for r in self.out]; self._tf_base = [list(r) for r in self.tf]
        nx = 0
        for k, v in enumerate(self.Vl):
            a = doff.get(v['id']); b = don.get(v['id'])
            if a is None or b is None: continue
            nx += 1
            o = self.out[k]
            for g in RAW14:
                # additive in pieces (ON - OFF of the C# Plan); ratios blow up where the harness OFF plan has almost none of a good the game plan had
                i = GI[g]; o[i] = max(0.0, o[i] + (kg_of(b, g) - kg_of(a, g)) / kgpp.get(g, 10.0))
            pd = v['paid']
            for g in self.G:
                pa = a.get('paid_' + g); pb = b.get('paid_' + g)
                if pa is None or pb is None: continue
                pd[GI[g]] = max(0.0, min(1.0, pd[GI[g]] + pb - pa))
            self._pl_unc[k] = b['unc']; self._pl_hw[k] = b['hw']; self._pl_homekcal[k] = b['homekcal']
        # town fields
        for t, tw in enumerate(self.T):
            a = doff.get(tw['id'] + '#pola'); b = don.get(tw['id'] + '#pola')
            if a is None or b is None: continue
            for g in RAW14:
                i = GI[g]; self.tf[t][i] = max(0.0, self.tf[t][i] + (kg_of(b, g) - kg_of(a, g)) / kgpp.get(g, 10.0))
        # market totals: the additive shift is clamped at 0 where the sim district has less than the harness moves away - rescale every good of every market to the
        # C# Plan's own relative change (sum ON / sum OFF), keeping where the shift lands
        for m in (0, 1):
            for g in RAW14:
                i = GI[g]
                hoff = sum(kg_of(r, g) for r in doff.values() if int(r['mk']) == m); hon = sum(kg_of(r, g) for r in don.values() if int(r['mk']) == m)
                if hoff <= 0: continue
                base = sum(self._out_base[k][i] for k, v in enumerate(self.Vl) if v['mk'] == m) + sum(self._tf_base[t][i] for t in range(len(self.T)) if self.T[t]['mk'] == m)
                cur = sum(self.out[k][i] for k, v in enumerate(self.Vl) if v['mk'] == m) + sum(self.tf[t][i] for t in range(len(self.T)) if self.T[t]['mk'] == m)
                if cur <= 0: continue
                f = base * (hon / hoff) / cur
                for k, v in enumerate(self.Vl):
                    if v['mk'] == m: self.out[k][i] *= f
                for t in range(len(self.T)):
                    if self.T[t]['mk'] == m: self.tf[t][i] *= f
        # town paid share
        for t, tw in enumerate(self.T):
            a = toff.get(tw['id']); b = ton.get(tw['id'])
            if a is None or b is None: continue
            for z, g in enumerate(KSB_IDS):
                if g not in GI: continue
                if a[z] > 1e-6: self.Q[t][GI[g]] *= max(0.0, min(2.0, b[z] / a[z]))
        self._plan_n = nx

    # ------------------------------------------------------------------ init (P3 D-term, P5 shelves, KS-H, controller state)
    def init_state(self, init, start):
        o = self.o; NT, NG = self.NT, self.NG
        init = getattr(self, '_init', init) or init
        self._vil_of = [[k for k, v in enumerate(self.Vl) if v['trade'] == t] for t in range(NT)]
        # P3 Der: ZC5 inputs only for what the hands make (bread / pie / beer: the missing output is bought as grain - same grain pieces)
        if o['realoff']:
            self.der = [[0.0] * NG for _ in range(NT)]
            for t in range(NT):
                lab = sum((self.Q[t][oo] + self.QV[t][oo]) * labr for (oo, ii, r, labr) in self.pairs)
                f = min(1.0, self.hands[t] / lab) if lab > 0 else 1.0
                for (oo, ii, r, labr) in self.pairs:
                    outD = self.Q[t][oo] + self.QV[t][oo]
                    if outD <= 0: continue
                    if ii == self.iGrain: self.der[t][ii] += outD * r          # made x r + missing x r (bought as grain)
                    else: self.der[t][ii] += outD * f * r
            # P3 One: lords' net purchases + courts at the town (EMA28 of what they really take; start at the ledger value)
            self.one = [[max(0.0, self.lords_buy[t][i] - self.lords_drop[t][i]) + self.court[t][i] for i in range(NG)] for t in range(NT)]
        if o.get('qvcal', 0) >= 3 and getattr(self, '_plan_on', None): self._qvcal_on()
        self.one_ema = [list(r) for r in self.one]
        # home share from the C# Plan: home kcal = 1 - paid kcal - Plan uncovered (per village); without a Plan transfer - sim default (round 3)
        kc = self.kcal
        self.pk = [0.0] * self.NV; self.frac = [0.0] * self.NV
        for k, v in enumerate(self.Vl):
            w = sum(v['bask'][i] * kc[i] for i in range(NG)); p = sum(v['bask'][i] * v['paid'][i] * kc[i] for i in range(NG))
            self.pk[k] = p / w if w > 0 else 0.0
            if hasattr(self, '_pl_unc') and k in self._pl_unc:
                self.frac[k] = max(0.0, 1.0 - self.pk[k] - self._pl_unc[k])
            else:
                self.frac[k] = self.home_v[k]
        self.hw = [self._pl_hw.get(k, 0.2) if hasattr(self, '_pl_hw') else 0.2 for k in range(self.NV)]
        # budget per village per day (paid basket at start prices) - pass-2 purse reserve 7 x budget
        for k, v in enumerate(self.Vl):
            m = v['mk']
            self.budget_v[k] = sum(v['bask'][i] * v['paid'][i] * self.val[i] * self.start_idx[m][i] for i in range(NG))
        self._dp_refresh()
        S.Sim.init_state(self, 'start', start)     # warehouses, trips, castles, caravans (shelves replaced below)
        self.dterm_full = bool(o['price_blend'])
        # P5 start shelves at D: market stock of the round-3 run at D (rampa line / ledger), split 50% by paid D-term share, 50% by local supply share
        self.Ts = [[tstar(self.start_idx[m][i]) for i in range(NG)] for m in (0, 1)]
        loc = [[self.tf[t][i] for i in range(NG)] for t in range(NT)]
        for k, v in enumerate(self.Vl):
            for i in range(NG): loc[v['trade']][i] += self.out[k][i]
        ry = self.W['rynki'].get(str(start)) or self.W['rynki'].get(str(start - 1))
        for m in (0, 1):
            ts = [t for t in range(NT) if self.T[t]['mk'] == m]
            for i in range(NG):
                tgt = [self.Ts[m][i] * self.dp[t][i] for t in ts]
                TT = sum(tgt)
                if init == 'start': tot = TT
                elif init == 'r3' or init == 'split' or init == 'A':
                    st = ry[str(m)].get(self.G[i], [0, 0, None])[2] if ry and str(m) in ry else None
                    tot = st if st is not None else 0.5 * TT
                else: tot = TT
                L = sum(loc[t][i] for t in ts)
                for j, t in enumerate(ts):
                    a = (tgt[j] / TT if TT > 0 else 0.0)
                    b = (loc[t][i] / L if L > 0 else a)
                    share = a if init == 'start' else 0.5 * a + 0.5 * b
                    self.shelf[t][i] = tot * share
        # price state at D (StartShelves: smoothed supply = start shelf value where the real shelf holds >= half of it)
        for t in range(NT):
            m = self.T[t]['mk']
            for c in range(self.NC):
                V = sum(self.shelf[t][i] * self.val[i] for i in self.catg[c])
                E = sum(self.dterm(t, i) * self.val[i] for i in self.catg[c])
                tv = sum(self.Ts[m][i] * self.dp[t][i] * self.val[i] for i in self.catg[c])
                days = tv / E if E > 0 else 0.0
                if E > 0 and V >= 0.5 * days * E:
                    want = (0.14 * days * E - 0.04 * V) / 0.1
                    sup = max(0.1, min(1.4 * days * E, max(days * E * 0.5, want)))
                else: sup = max(0.1, V)
                self.Sv[t][c] = sup; self.Dv[t][c] = E; self.idx[t][c] = self.index(E, sup, V)
        # state of the R3 parts
        self.off7 = [[self.dp[t][i] for i in range(NG)] for t in range(NT)]
        self._tk = [[0.0] * NG for _ in range(NT)]            # offtake of the day (villages, town folk, ZC5, lords net, courts)
        self._lord_take = [[0.0] * NG for _ in range(NT)]
        self.row0 = [[False] * NG for _ in range(NT)]
        self.carry2 = [[0.0] * NG for _ in range(self.NV)]
        self.v_in = [0.0] * self.NV; self.v_fr = [0.0] * self.NV; self.v_out = [0.0] * self.NV; self.v_out_all = [0.0] * self.NV
        self.bought_v = [-1.0] * self.NV
        self.e_in = [None] * self.NV; self.e_fr = [0.0] * self.NV; self.e_out = [0.0] * self.NV
        self._vprev = (list(self.v_in), list(self.v_fr), list(self.v_out))
        self.paid0 = [list(v['paid']) for v in self.Vl]
        self.ptarget = None
        self.town_gold_delta = [0.0] * NT; self.flow_hist = {}; self.flow_today = [0.0] * NT
        self._load_gold()
        self.ksh = KsH(self) if o['carts'] else None
        if self.ksh: self.ksh.start_day()
        # controller (P4)
        self.out0 = [list(r) for r in self.out]
        self.P0 = [[sum(self.out[k][i] for k in range(self.NV) if self.Vl[k]['mk'] == m) + sum(self.tf[t][i] for t in range(NT) if self.T[t]['mk'] == m) for i in range(NG)] for m in (0, 1)]
        self.mf = [[1.03] * NG for _ in (0, 1)]
        # C28 at D: the C# EMA runs from the session start - before D it averages the old economy's market consumption; start it at the round-3 ledger
        # consumption of D-14..D-1 ('KS: rynki'), U28 at 0
        self.c28 = [[0.0] * NG for _ in (0, 1)]
        nd0 = 0
        for d in range(start - 14, start):
            r = self.W['rynki'].get(str(d))
            if not r: continue
            nd0 += 1
            for m in (0, 1):
                for i in range(NG):
                    self.c28[m][i] += r[str(m)].get(self.G[i], [0, 0, 0])[1]
        for m in (0, 1):
            for i in range(NG): self.c28[m][i] = self.c28[m][i] / nd0 if nd0 else self.P0[m][i] / 1.03
        self.u28 = [[0.0] * NG for _ in (0, 1)]; self.cN = 28
        self.vtarget = None; self.vslot = [fnv(v['id']) % 7 for v in self.Vl]
        self._land_prev = {}
        self.town_land = self._town_lands()
        self.beta = [[1.0] * NG for _ in (0, 1)]
        # init 'A' (start at the Apply day A = D - 14): the old game economy until D - production of the game chain x (1 - beta) and the market consumption of the
        # round-3 ledger before A (minus what the sim books itself: castles, lords, courts, party food) x (1 - lambda), spread over the towns by paid D-term
        self.old_rate = None; self.c_old = None
        if init == 'A':
            self.beta = [[0.0] * NG for _ in (0, 1)]
            P_ = [[0.0] * NG for _ in (0, 1)]; C_ = [[0.0] * NG for _ in (0, 1)]; n_ = 0
            for d in range(start - 4, start):
                r = self.W['rynki'].get(str(d))
                if not r: continue
                n_ += 1
                for m in (0, 1):
                    for i in range(NG):
                        a = r[str(m)].get(self.G[i], [0, 0, 0]); P_[m][i] += a[0]; C_[m][i] += a[1]
            if n_:
                for m in (0, 1):
                    for i in range(NG): P_[m][i] /= n_; C_[m][i] /= n_
            mp = [0.0, 0.0]
            for t in range(NT): mp[self.T[t]['mk']] += self.one_share[t]
            self.c_old = [[0.0] * NG for _ in (0, 1)]
            for m in (0, 1):
                for i in range(NG):
                    own = sum(c['dem'][i] for c in self.C if c.get('mk', 0) == m) + sum(self.lords_buy[t][i] - self.lords_drop[t][i] + self.court[t][i] + self.party[t][i] for t in range(NT) if self.T[t]['mk'] == m)
                    self.c_old[m][i] = max(0.0, C_[m][i] - own)
            self.old_rate = [[0.0] * NG for _ in range(self.NV)]
            for m in (0, 1):
                vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
                for i in range(NG):
                    tot = sum(self.out[k][i] for k in vs)
                    if tot <= 0: continue
                    for k in vs: self.old_rate[k][i] = P_[m][i] * self.out[k][i] / tot
            self.dp_share = [[0.0] * NG for _ in range(NT)]
            for m in (0, 1):
                ts = [t for t in range(NT) if self.T[t]['mk'] == m]
                for i in range(NG):
                    tot = sum(self.dp[t][i] for t in ts)
                    for t in ts: self.dp_share[t][i] = self.dp[t][i] / tot if tot > 0 else 0.0
        self.ctl_log = []
        self.c_day_cons = [[0.0] * NG for _ in (0, 1)]; self.c_day_unmet = [[0.0] * NG for _ in (0, 1)]; self.c_day_p2 = [[0.0] * NG for _ in (0, 1)]

    def _town_lands(self):
        try:
            d = json.load(open(KSDUMP, encoding='utf-8'))
            m = {t['id']: t['land'] for t in d['towns']}
        except Exception:
            m = {}
        out = []
        for t, tw in enumerate(self.T):
            if tw['id'] in m: out.append(m[tw['id']]); continue
            c = collections.Counter()
            for k in self._vil_of[t]: c[self.Vl[k]['land']] += self.Vl[k]['people']
            out.append(c.most_common(1)[0][0] if c else '?')
        return out

    def _load_gold(self):
        self._gold = {}; self._res = {}
        p = os.path.join(self.W['csv'], 'kasy-miast.csv')
        idix = {tw['id']: t for t, tw in enumerate(self.T)}
        with open(p, encoding='utf-8') as f:
            for r in csv.DictReader(f, delimiter=';'):
                t = idix.get(r['miasto_id'])
                if t is None: continue
                d = int(r['dzien'])
                self._gold[(t, d)] = float(r['kasa']); self._res[(t, d)] = float(r['zapas_kupcow'])
        self._gmaxday = max(d for (_, d) in self._gold)

    def town_gold(self, t):
        d = min(self.day, self._gmaxday)
        g = self._gold.get((t, d), self._gold.get((t, d - 1), 0.0)) + self.town_gold_delta[t]
        return max(0.0, g) if self.o.get('gold0', 0) else g      # round 4: coffers never below 0 (the game refuses - KsFoodCarts.Ship)

    def town_reserve(self, t):
        d = min(self.day, self._gmaxday)
        return self._res.get((t, d), self._res.get((t, d - 1), 0.0))

    def gold_flow(self, t, x):
        self.town_gold_delta[t] += x

    def _dp_refresh(self):
        NT, NG = self.NT, self.NG
        self.dp = [[self.Q[t][i] + self.QV[t][i] + self.der[t][i] + self.one[t][i] for i in range(NG)] for t in range(NT)]

    def dterm(self, t, i):
        if getattr(self, 'dterm_full', False):
            return self.Q[t][i] + self.QV[t][i] + self.der[t][i] + self.one[t][i]
        return self.lam(self.T[t]['mk']) * (self.Q[t][i] + self.QV[t][i] + self.der[t][i]) + self.one[t][i]

    # ------------------------------------------------------------------ counters
    def reset_counters(self):
        S.Sim.reset_counters(self)
        self.c_kr = [0.0, 0.0]; self.c_kh = [0.0, 0.0]; self.c_kwant = [0.0, 0.0]
        self.c_p1 = [[0.0] * 3, [0.0] * 3]; self.c_p2 = [[0.0] * 3, [0.0] * 3]
        self.c_zlp = [0.0, 0.0]; self.c_zlg = [0.0, 0.0]; self.c_zlg1 = [0.0, 0.0]
        self.c_g1 = [[[0.0] * 5 for _ in range(self.NG)] for _ in (0, 1)]
        self.c_sgm = collections.defaultdict(float)
        self.c_own_pcs = 0.0; self.c_vin_m = [0.0, 0.0]; self.c_vout_m = [0.0, 0.0]; self.c_vfr_m = [0.0, 0.0]; self.c_budget_m = [0.0, 0.0]

    # ------------------------------------------------------------------ takes with offtake / unmet bookkeeping
    def take(self, t, i, n):
        x = min(n, max(0.0, self.shelf[t][i]))
        if x > 0: self.shelf[t][i] -= x; self._tk[t][i] += x; self.flow_today[t] += x * self.buy_price(t, i); self.c_sgm[(getattr(self, '_tag', 'town'), self.T[t]['mk'], i)] += x
        if n > x: self.c_day_unmet[self.T[t]['mk']][i] += n - x
        return x

    def zc5(self, t):
        P = self.P; sh = self.shelf[t]; m = self.T[t]['mk']; lam = self.lam(m)
        hands = self.hands[t]; opts = []
        for (oo, i, r, lab) in self.pairs:
            dem_o = lam * (self.Q[t][oo] + self.QV[t][oo])
            if dem_o <= 0: continue
            target = max(0.0, P['zc5_out_days'] * dem_o - sh[oo])
            direct = lam * (self.Q[t][i] + self.QV[t][i])
            avail = max(0.0, sh[i] - P['zc5_in_days'] * direct - P['keep'])
            if target > 0 and avail / r < 1: self.c_day_unmet[m][i] += math.ceil(target / 6.0) * r
            mk = min(target, avail / r)
            if mk <= 0: continue
            po = self.val[oo] * self.idx[t][self.gcat[oo]]; pi = self.val[i] * self.idx[t][self.gcat[i]]
            prof = po - (1 + P['zc5_margin']) * r * pi
            if prof <= 0: continue
            opts.append((prof / lab, oo, i, r, lab, mk))
        opts.sort(reverse=True)
        for (_, oo, i, r, lab, mk) in opts:
            if hands <= 0: break
            x = min(mk, hands / lab); x = min(x, max(0.0, sh[i] - P['keep']) / r)
            if x <= 0: continue
            sh[i] -= x * r; sh[oo] += x; hands -= x * lab; self._tk[t][i] += x * r; self.c_sgm[('zc5', m, i)] += x * r
            self.c_cons[m][i] += x * r; self.c_prod[m][oo] += x; self.c_sink['zc5_in_' + self.G[i]] += x * r; self.c_sg[('zc5', i)] += x * r

    # ------------------------------------------------------------------ G2 village ladder (Zc4BuyCore), H1, H2, G3
    def _ztake(self, b, tt, i, n, count=True, pass2=False):
        """ZcTake / ZcTake2 (C#) on town tt."""
        if n <= 0: return 0
        sh = self.shelf[tt]
        absent = self.o['h2'] and self.o['every2'] and self.row0[tt][i] and (not self.o.get('h2arr', 0) or int(sh[i]) <= 5)   # round 4: H2 only while still no stock
        have = (int(sh[i]) - 5) if not absent else 0
        price = self.buy_price(tt, i)
        kc = self.kcal[i]
        if pass2:
            if not (b.get('alt') and self.o.get('kwfix', 0)): b['kw'] += n * kc; b['zlw'] += n * price   # round-2 fix kwfix: wanted kcal once (not again in near towns)
            if have <= 0: b['p2shelf'] += n; return 0
            if self.o.get('p2price', 0) and price > 0.8 * self.start_idx[self.T[tt]['mk']][i] * self.val[i]: b['p2shelf'] += n; return 0   # round 4: pass 2 only while cheap
            above = int(math.floor(have + 5 - self.o['p2k'] * self.Ts[self.T[tt]['mk']][i] * self.dp[tt][i]))
            k = min(n, have, above)
            if k <= 0: b['p2shelf'] += n; return 0
            if k < n: b['p2shelf'] += n - k
            room = b['gold'] - max(0.0, b['lim'])
            if room < price: b['p2gold'] += k; return 0
            if k * price > room: b['p2gold'] += k - int(room // price); k = int(room // price)
            if k <= 0: return 0
            b['_p2'] = True; self._buy(b, tt, i, k, price); b['_p2'] = False; b['p2got'] += k
            return k
        if count:
            b['want'] += n; b['kw'] += n * kc; b['zlw'] += n * price
            self.c_g1[b['m']][i][0] += n
        if have <= 0:
            b['nogoods'] += 1
            if count: b['ngpcs'] += n; b['miss'][i] += n; self.c_g1[b['m']][i][2] += n
            return 0
        if b['broke']:
            if count: b['ungold'] += min(n, have); self.c_g1[b['m']][i][3] += min(n, have)
            if count and have < n: b['ngpcs'] += n - have; b['miss'][i] += n - have
            return 0
        if count and have < n: b['ngpcs'] += n - have; b['miss'][i] += n - have; self.c_g1[b['m']][i][2] += n - have
        k = min(n, have)
        room = b['gold'] - max(0.0, b['lim'])
        if room < price:
            if count: b['ungold'] += k; self.c_g1[b['m']][i][3 if b['lim'] <= 0 else 4] += k
            if b['lim'] <= 0: b['broke'] = True
            return 0
        if k * price > room:
            if count: b['ungold'] += k - int(room // price); self.c_g1[b['m']][i][3 if b['lim'] <= 0 else 4] += k - int(room // price)
            k = int(room // price)
            if k <= 0:
                if b['lim'] <= 0: b['broke'] = True
                return 0
        self._buy(b, tt, i, k, price); b['got'] += k
        if count: self.c_g1[b['m']][i][1] += k
        return k

    def _buy(self, b, tt, i, k, price):
        b['gold'] -= k * price; self.shelf[tt][i] -= k
        if not b.get('_p2') or self.o['off_p2']: self._tk[tt][i] += k
        if b.get('_p2'): self.c_day_p2[b['m']][i] += k
        b['spent'] += k * price; b['kg'] += k * self.kcal[i]; b['zlg'] += k * price
        m = b['m']
        self.c_vsp[(m, i)] += k * price
        self.c_cons[m][i] += k; self.c_sink['vil'] += k; self.c_sg[('vil', i)] += k
        self.c_sgm[('vil2' if b.get('_p2') else 'vil1', m, i)] += k
        self.flow_today[tt] += k * price

    def _miss_cov(self, b, i, n):
        if n <= 0 or b['miss'][i] <= 0: return 0
        c = min(n, b['miss'][i]); b['miss'][i] -= c; b['ngpcs'] = max(0, b['ngpcs'] - c)
        return c

    def _ladder(self, b, t, near, n, lim):
        iG, iS = self.iGrain, self.iSalt
        b['lim'] = 0.0
        self._ztake(b, t, iG, n[iG]); self._ztake(b, t, iS, n[iS])
        b['lim'] = lim
        def unit(i, per):
            have = int(self.shelf[t][i]) - 5
            if have <= 0 or (self.o['h2'] and self.o['every2'] and self.row0[t][i] and (not self.o.get('h2arr', 0) or int(self.shelf[t][i]) <= 5)): return 1e18
            return self.buy_price(t, i) / per
        dr = sorted(self.drinks, key=lambda i: unit(i, 1.0))
        un = {}
        for i in dr: un[i] = n[i] - self._ztake(b, t, i, n[i])
        iB, iW = self.iBeer, self.iWine
        if un.get(iW, 0) > 0:
            bw = self._ztake(b, t, iB, un[iW], count=False)
            un[iB] = un.get(iB, 0) + un[iW] - bw
            self._miss_cov(b, iW, bw); un[iW] = 0
        if un.get(iB, 0) > 0:
            g = int(math.floor(un[iB] * 0.4))
            if g > 0:
                gb = self._ztake(b, t, iG, g, count=False)
                brew = int(math.floor(gb / 0.4 + 1e-9))
                brew -= self._miss_cov(b, iB, brew)
                if brew > 0: self._miss_cov(b, iW, brew)
        pr = sorted(self.protein, key=lambda i: unit(i, self.prot_kcal[i] / 1000.0))
        for i in pr: self._ztake(b, t, i, n[i])
        for i in self.rest: self._ztake(b, t, i, n[i])
        if not b['broke'] and b['ngpcs'] > 0:
            for alt in near:
                if b['broke']: break
                if not any(x > 0 for x in b['miss']): break
                for i in self.ladder_order:
                    if b['miss'][i] <= 0: continue
                    b['lim'] = 0.0 if i in (iG, iS) else lim
                    got = self._ztake(b, alt, i, b['miss'][i], count=False)
                    if got > 0: b['miss'][i] -= got; b['else'] += got
                    if b['broke']: break
        b['lim'] = 0.0

    def village_buy(self, k, v):
        o = self.o; m = v['mk']; NG = self.NG
        every = 2 if o['every2'] else 1
        if every == 2 and ((fnv(v['id']) + self.day) & 1) != 0: return
        lam = self.lam(m) * every
        t = v['trade']; near = v['_near']
        if not hasattr(self, 'ladder_order'):
            self.ladder_order = [self.iGrain, self.iSalt, self.iBeer, self.iWine, self.iMead] + [self.GI[g] for g in ('meat', 'fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'oil', 'honey')]
        b = {'gold': self.gold[k], 'lim': 0.0, 'broke': False, 'want': 0, 'got': 0, 'ungold': 0, 'nogoods': 0, 'ngpcs': 0, 'else': 0, 'miss': [0] * NG,
             'kw': 0.0, 'kg': 0.0, 'zlw': 0.0, 'zlg': 0.0, 'spent': 0.0, 'm': m, 'p2got': 0, 'p2shelf': 0, 'p2gold': 0}
        q = v['bask']; cr = self.carry[k]
        n = [0] * NG; n2 = [0] * NG
        if o['paidfirst']:
            qp = [q[i] * v['paid'][i] for i in range(NG)]
            c2 = self.carry2[k]
            for i in range(NG):
                p1 = max(0.0, min(q[i], qp[i]))
                w = p1 * lam + cr[i]; kk = int(math.floor(w)); cr[i] = max(0.0, min(1.0, w - kk)); n[i] = max(0, kk)
                w2 = max(0.0, q[i] - p1) * lam + c2[i]; k2 = int(math.floor(w2)); c2[i] = max(0.0, min(1.0, w2 - k2)); n2[i] = max(0, k2)
            pg = self.buy_price(t, self.iGrain); ps = self.buy_price(t, self.iSalt)
            perDay = lam / every
            resGS = round(7.0 * perDay * (qp[self.iGrain] * pg + qp[self.iSalt] * ps))
        else:
            qp = None
            for i in range(NG):
                w = q[i] * lam + cr[i]; kk = int(math.floor(w)); cr[i] = max(0.0, min(1.0, w - kk)); n[i] = max(0, kk)
            resGS = 0
        self._ladder(b, t, near, n, resGS)
        if qp is not None:
            miss = sum(b['miss'])
            for i in range(NG):
                if b['miss'][i] > 0 and self.G[i] in RAW14: self.c_day_unmet[m][i] += b['miss'][i]
            self.c_p1[m][0] += b['want']; self.c_p1[m][1] += miss; self.c_p1[m][2] += b['ungold']
            self.c_zlg1[m] += b['zlg']
            # pass 2 (rest of the full basket): purse above 7 days of the Plan budget, stall above its target
            b['lim'] = round(7.0 * max(0.0, self.budget_v[k])); b['broke'] = False
            if o['p2flow']:
                # round-3 sim fix p2flow: pass 2 spends the purse's flow surplus over the Plan budget (EMA28 net inflow - budget) plus p2save of the savings per day
                net = (self.e_in[k] or 0.0) + self.e_fr[k] - self.e_out[k]
                allow = every * (max(0.0, net - self.budget_v[k]) + o['p2save'] * max(0.0, b['gold']))
                b['lim'] = max(b['lim'], round(b['gold'] - allow))
            left = [0] * NG; anyl = False
            for i in self.ladder_order:
                if n2[i] <= 0: continue
                got = self._ztake(b, t, i, n2[i], pass2=True)
                left[i] = n2[i] - got
                if left[i] > 0: anyl = True
            if anyl:
                b['alt'] = True
                for alt in near:
                    rest = False
                    for i in self.ladder_order:
                        if left[i] <= 0: continue
                        got = self._ztake(b, alt, i, left[i], pass2=True)
                        if got > 0: left[i] -= got; b['else'] += got
                        if left[i] > 0: rest = True
                    if not rest: break
            b['alt'] = False
            self.c_p2[m][0] += b['p2got']; self.c_p2[m][1] += b['p2shelf']; self.c_p2[m][2] += b['p2gold']
            self.c_zlp[m] += sum(qp[i] * lam * self.buy_price(t, i) for i in range(NG) if qp[i] > 0)
        spent = self.gold[k] - b['gold']; self.gold[k] = b['gold']
        self.c_spent += spent; self.c_spm[m] += spent
        self.c_zlg[m] += b['zlg']
        ng = max(0, b['ngpcs'] - b['else'])
        self.c_vm[m][0] += b['want']; self.c_vm[m][1] += b['got']; self.c_vm[m][2] += b['ungold']; self.c_vm[m][3] += ng
        self.c_want[t] += b['want']; self.c_got[t] += b['got']; self.c_ungold[t] += b['ungold']; self.c_nogoods[t] += ng; self.c_else[t] += b['else']
        self.c_kw[m] += b['kw']; self.c_kg[m] += b['kg']
        self.bought_v[k] = min(1.0, b['kg'] / b['kw']) if b['kw'] > 1e-6 else -1.0
        # G3 own food first
        kr = 0.0
        if o['ownfirst'] and qp is not None:
            paidK = sum(min(q[i], qp[i]) * lam * self.kcal[i] for i in range(NG) if qp[i] > 0 and self.kcal[i] > 0)
            short = paidK - b['kg']
            if short > 0:
                wh = self.wh[k]
                for g in ('grain', 'fish', 'meat', 'cheese', 'butter', 'Egg', 'date_fruit', 'olives', 'grape', 'apple', 'carrot', 'orange', 'honey'):
                    if short <= 0: break
                    i = self.GI[g]; kc = self.kcal[i]
                    if kc <= 0: continue
                    have = int(wh[i]); cap = have // 2
                    if cap <= 0: continue
                    x = min(cap, int(math.ceil(short / kc)))
                    if x <= 0: continue
                    wh[i] -= x; kr += x * kc; short -= x * kc; self.c_sgm[('own', m, i)] += x
                    self.c_own_pcs += x; self.c_cons[m][i] += x
            self.c_kr[m] += kr
        # home kcal (G6 accounting): want x min(Plan home share, 1 - bought share); per day
        want_day = sum(q[i] * self.kcal[i] for i in range(NG)) * every
        bv = self.bought_v[k] if self.bought_v[k] >= 0 else self.pk[k]
        self.c_kh[m] += want_day * max(0.0, min(self.frac[k], 1.0 - max(0.0, min(1.0, bv))))
        self.c_kwant[m] += want_day
        if b['kw'] > 0:
            bfr = min(1.0, b['kg'] / b['kw'])
            self.c_unc[m] += b['kw'] * max(0.0, 1.0 - bfr - min(self.frac[k], 1.0 - bfr) - kr / b['kw'])

    # ------------------------------------------------------------------ day
    def step(self):
        P = self.P; NG = self.NG; NT = self.NT; day = self.day; o = self.o
        self.c_days += 1
        # H2: stall rows of the day's map (absent = no goods all day; KS-H arrival clears it)
        if o['h2']:
            for t in range(NT):
                sh = self.shelf[t]; r0 = self.row0[t]
                for i in range(NG): r0[i] = sh[i] < 1
        if self.ksh: self.ksh.start_day()
        self.c_day_cons = [[x for x in r] for r in self.c_cons]
        self.c_day_unmet = [[0.0] * NG for _ in (0, 1)]; self.c_day_p2 = [[0.0] * NG for _ in (0, 1)]
        self.flow_today = [0.0] * NT
        # 1. wagon arrivals
        keep = []
        for tr in self.trips:
            if tr[0] <= day + 1e-9:
                _, k, t, load, nfv = tr
                rev = nfv * P['nf_idx']
                for i, n in load:
                    if n <= 0: continue
                    rv_ = self.sell_value(t, i, n)
                    rev += rv_; self.flow_today[t] -= rv_
                    self.shelf[t][i] += n; self.news[t][i] -= n
                pin = rev * P['purse_share'] * self.rv[k]
                self.gold[k] += pin; self.c_inflow += pin; self.c_rev += rev; self.c_vin[k] += pin; self.v_in[k] += pin
                self.c_vnf[k] += nfv * P['nf_idx'] * P['purse_share'] * self.rv[k]
            else: keep.append(tr)
        self.trips = keep
        # 2. production (P5 beta during the ramp D..D+14)
        lamD = self.lam(0)
        for k, v in enumerate(self.Vl):
            ov = self.out[k]; w = self.wh[k]; m = v['mk']; self.wh_nf[k] += self.nf[k]
            bt = self.beta[m]; orr = self.old_rate[k] if self.old_rate is not None else None
            for i in range(NG):
                x = ov[i] * bt[i] if ov[i] else 0.0
                if orr is not None and orr[i] and bt[i] < 1.0: x += orr[i] * (1.0 - bt[i])
                if x: w[i] += x; self.c_prod[m][i] += x
        for t in range(NT):
            m = self.T[t]['mk']; bt = self.beta[m]
            for i in range(NG):
                x = self.tf[t][i]
                if x: x *= bt[i]; self.shelf[t][i] += x; self.c_prod[m][i] += x
        # 3. departures (castle stop at start prices - the castle pays nothing modelled)
        for k, v in enumerate(self.Vl):
            if self.next_dep[k] > day + 1e-9: continue
            g0 = self.gold[k]
            self.depart(k, v)
            self.v_in[k] += self.gold[k] - g0
        # 4. ZC5
        for t in range(NT): self.zc5(t)
        # 5. town folk (before D the old game economy - the ledger sink below)
        if day >= o['D']:
            for t in range(NT): self.town_folk(t)
        if self.c_old is not None:
            for t in range(NT):
                m = self.T[t]['mk']; lamt = self.lam(m) if day >= o['D'] else 0.0
                if lamt >= 1.0: continue
                for i in range(NG):
                    n_ = (1.0 - lamt) * self.c_old[m][i] * self.dp_share[t][i]
                    if n_ <= 0: continue
                    self._tag = 'old'; x = self.take(t, i, n_); self._tag = 'town'
                    self.c_cons[m][i] += x; self.c_sink['old'] += x
        # 6. villages
        for k, v in enumerate(self.Vl): self.village_buy(k, v)
        # 7. lords, courts, castles
        for t in range(NT):
            m = self.T[t]['mk']; sh = self.shelf[t]
            for i in range(NG):
                lb, ct = self.lords_buy[t][i], self.court[t][i]
                x = min(max(0.0, sh[i]), lb + ct)
                if x > 0:
                    self.flow_today[t] += x * self.buy_price(t, i)
                    sh[i] -= x
                    xc = x * (ct / (lb + ct)); xl = x - xc
                    self.c_cons[m][i] += xc
                    self.c_sink['lords+court'] += x; self.c_sg[('lords+court', i)] += x; self.c_sgm[('lords', m, i)] += x
                    d = self.lords_drop[t][i]
                    self._tk[t][i] += max(0.0, xl - d) + xc
                    self._lord_take[t][i] = max(0.0, xl - d) + xc
                else: self._lord_take[t][i] = 0.0
                d = self.lords_drop[t][i]
                if d: sh[i] += d
                self.c_cons[m][i] += self.party[t][i]
        for c, cs in enumerate(self.C):
            st = self.cstock[c]
            for i in range(NG):
                x = min(st[i], cs['dem'][i])
                if x > 0: st[i] -= x; self.c_sink['castle'] += x; self.c_sg[('castle', i)] += x; self.c_cons[cs.get('mk', 0)][i] += x; self.c_sgm[('castle', cs.get('mk', 0), i)] += x
        # 8. BK caravans as measured, KS-H arrivals and decisions
        if self.trade_mod: self.trade_mod.daily(self, day)
        if self.ksh and day >= self.o['D'] - 14:
            self.ksh.arrivals(day); self.ksh.daily(day); self.ksh.flush(day)
        # 9. other purse outflows (per village; G4 leak cut lowers the rate)
        if P['old_salt'] > 0:
            isl = self.iSalt
            for k, v in enumerate(self.Vl):
                nn = int(P['old_salt'] * v['bask'][isl] + self.rng.random())
                if nn <= 0 or self.gold[k] <= 0: continue
                t = v['trade']; have = int(self.shelf[t][isl]) - P['keep']
                if have <= 0: continue
                p = self.buy_price(t, isl); x = min(nn, have, int(self.gold[k] // p))
                if x <= 0: continue
                self.shelf[t][isl] -= x; self.gold[k] -= x * p; self.c_other += x * p; self.v_out[k] += x * p; self.flow_today[t] += x * p
                self.c_cons[v['mk']][isl] += x; self.c_sg[('vil_old', isl)] += x
        for k in range(self.NV):
            op = P['other_rate'] * max(0.0, self.gold[k] - self.floor_v[k]); ox = op + self.dues_v[k]
            ox = min(ox, self.gold[k]); self.gold[k] -= ox; self.c_other += ox
            # alpha_net outflows: C# counts every KsVilOut (war levy and hoard tax on the purse included); fix an_flow=1 - only flow outflows (dues, BK market, old list)
            if o.get('an_split', -1) >= 0: self.v_out[k] += (ox - min(op, ox)) + o['an_split'] * min(op, ox)
            else: self.v_out[k] += (ox - min(op, ox)) if o['an_flow'] else ox
            self.v_out_all[k] += ox
        # 10. prices
        self.prices()
        # 11. daily EMAs: offtake (7 d) and One (28 d, P3), controller C28/U28 (P4), beta (P5), weekly Plan
        a7 = 1.0 / 7.0; a28 = 1.0 / 28.0
        for t in range(NT):
            e7 = self.off7[t]; tk = self._tk[t]; oe = self.one_ema[t]; lt = self._lord_take[t]
            for i in range(NG):
                e7[i] += a7 * (tk[i] - e7[i]); tk[i] = 0.0
                if o['realoff']: oe[i] += a28 * (lt[i] - oe[i])
        if o['realoff']:
            self.one = [list(r) for r in self.one_ema]; self._dp_refresh()
        # P4 averages: C# ControlDaily - EMA28 from the session start (fix1=1: restart at the end of the demand ramp D+14, only full-lambda days, w = 1/min(28, n))
        if o['fix1'] and day == o['D'] + 14: self.cN = 0
        self.cN += 1
        wc = 1.0 / min(28, self.cN)
        for m in (0, 1):
            for i in range(NG):
                cday = self.c_cons[m][i] - self.c_day_cons[m][i]
                if not o['ctrl_p2']: cday -= self.c_day_p2[m][i]
                if self.cN <= 1: self.c28[m][i] = cday; self.u28[m][i] = self.c_day_unmet[m][i]
                else:
                    self.c28[m][i] += (cday - self.c28[m][i]) * wc; self.u28[m][i] += (self.c_day_unmet[m][i] - self.u28[m][i]) * wc
        self._beta_daily()
        # village money EMA28 (MoneyEma: purse inflow, KS-H freight, nonbasket outflows)
        pv = self._vprev
        for k in range(self.NV):
            di = self.v_in[k] - pv[0][k]; df = self.v_fr[k] - pv[1][k]; do = self.v_out[k] - pv[2][k]
            if self.e_in[k] is None: self.e_in[k] = di; self.e_fr[k] = df; self.e_out[k] = do
            else:
                self.e_in[k] += (di - self.e_in[k]) * a28; self.e_fr[k] += (df - self.e_fr[k]) * a28; self.e_out[k] += (do - self.e_out[k]) * a28
        self._vprev = (list(self.v_in), list(self.v_fr), list(self.v_out))
        # town coffers: round-3 observed + KS-H flows + (sales - wagon payments of this run) - (the same of the round-3-rules run, same seed)
        self.flow_hist[day] = list(self.flow_today)
        if self.base_flow is not None and day in self.base_flow:
            bf = self.base_flow[day]
            for t in range(NT): self.town_gold_delta[t] += self.flow_today[t] - bf[t]
        self.day += 1
        if (self.day - 1 - o['plan_first']) % 7 == 0 and self.day - 1 >= o['plan_first']: self._plan_week(self.day - 1)
        self._adopt()

    def _beta_daily(self):
        o = self.o
        t = self.day + 1 - (o['D'] - 14)          # tomorrow's ramp day
        r = max(0.0, min(1.0, t / 14.0)); lam = max(0.0, min(1.0, (t - 14) / 14.0))
        if not o['beta']:
            for m in (0, 1):
                for i in range(self.NG): self.beta[m][i] = max(lam, r)
            return
        for m in (0, 1):
            ts = [x for x in range(self.NT) if self.T[x]['mk'] == m]
            for i in range(self.NG):
                if t >= 28: self.beta[m][i] = 1.0; continue
                stock = sum(self.shelf[x][i] for x in ts)
                pipe = 0.0
                if self.ksh and self.G[i] in RAW14:
                    k = RAW14.index(self.G[i])
                    stock += sum(c[4] for c in self.ksh.carts if c[3] == k and self.T[c[2]]['mk'] == m)
                    pipe = self.ksh.pipeline(m, k)
                target = sum(self.Ts[m][i] * self.dp[x][i] for x in ts) + pipe
                ob = self.beta[m][i]
                if target > 0 and stock >= target: self.beta[m][i] = max(lam, min(r, ob))
                else: self.beta[m][i] = max(lam, r)

    def _plan_week(self, day):
        """Plan of `day` (weekly): P2 re-measured alpha_net (paid shares), P4 market factor (C# MarketControl) and glut lands; districts adopt over 7 days."""
        o = self.o
        if o['remeasure'] == 2 and day >= o['D']:
            if not hasattr(self, '_a3'): self._load_alpha()
            tgt = []
            for k, v in enumerate(self.Vl):
                a3 = self._a3.get(v['id']); au = self._a_used[k]
                kin = self.e_in[k] or 0.0
                if a3 is None or au is None or au <= 1e-6 or kin <= 1e-6: tgt.append(list(v['paid'])); continue
                an = max(0.0, min(0.95, a3 * max(0.0, kin + self.e_fr[k] - self.e_out[k]) / kin))
                r = max(0.05, min(20.0, an / au))
                self._a_next[k] = an
                tgt.append([min(1.0, p * r) for p in v['paid']])
            self.ptarget = tgt
        elif o['remeasure'] and day >= o['D']:
            # alpha_net budget = 0.95 x (inflow + freight - outflow) EMA28 (unclamped alpha <= 0.95 of the base value); paid shares scale with the budget (cap 1),
            # Plan uncovered per village kept (home share = 1 - paid kcal - uncovered)
            kc = self.kcal; NGl = self.NG
            tgt = []
            for k, v in enumerate(self.Vl):
                bv = sum(self.out[k][i] * self.val[i] for i in range(NGl)) + self.nf[k]
                net = max(0.0, (self.e_in[k] or 0.0) + self.e_fr[k] - self.e_out[k])
                bnew = min(0.95 * bv, 0.95 * net)
                cur = self.budget_v[k]
                r = max(0.05, min(20.0, bnew / cur)) if cur > 1e-6 else 1.0
                tgt.append([min(1.0, p * r) for p in v['paid']])
            self.ptarget = tgt
        if not o['ctrl'] or day < o['D'] + o['ctrl_from']: return
        if day > o['D'] + o.get('ctrl_end', 999):
            # round 4: after the start window the factor goes back to 1 over 2 Plans (then rules and prices move the hands)
            for m in (0, 1):
                for g in RAW14:
                    i = self.GI[g]; k0 = self.mf[m][i]; kk = 1.0 + (k0 - 1.0) * 0.5
                    self.mf[m][i] = 1.0 if abs(kk - 1.0) < 0.01 else kk
            return
        if o['fix1'] and self.cN < 5: return                 # fix1: at least 5 full-lambda days in the averages before the first factor change
        NT, NG = self.NT, self.NG
        hs = [[0.0] * NG for _ in (0, 1)]; h = [[0.0] * NG for _ in (0, 1)]
        for t in range(NT):
            m = self.T[t]['mk']
            for i in range(NG):
                hs[m][i] += self.Ts[m][i] * self.dp[t][i]; h[m][i] += max(0.0, self.shelf[t][i])
        glut = set()
        if o['glut']:
            shL = collections.defaultdict(float); tgL = collections.defaultdict(float)
            for t in range(NT):
                L = self.town_land[t]; m = self.T[t]['mk']
                for g in RAW14:
                    i = self.GI[g]; shL[(L, i)] += max(0.0, self.shelf[t][i]); tgL[(L, i)] += self.Ts[m][i] * self.dp[t][i]
            for key, s in shL.items():
                tg = tgL[key]; prev = self._land_prev.get(key)
                if tg > 1.0 and s > 2.0 * tg + 50 and prev is not None and s > prev: glut.add(key)
                self._land_prev[key] = s
        new = [[None] * NG for _ in (0, 1)]
        for m in (0, 1):
            for g in RAW14:
                i = self.GI[g]
                dm = self.P0[m][i] / 1.03
                # ctrl_u=1: unmet paid demand counts only while the market's shelves are below their target (above it the gap is distribution - KS-H, not hands)
                uf = max(0.0, min(1.0, 1.0 - h[m][i] / hs[m][i])) if o['ctrl_u'] and hs[m][i] > 0 else 1.0
                cu = self.c28[m][i] + self.u28[m][i] * uf
                kold = self.mf[m][i]; kk = kold
                if dm > 1.0 and cu > 1.0:
                    gap = max(-o['ctrl_gap'], min(o['ctrl_gap'], (hs[m][i] - h[m][i]) / (28.0 * cu)))
                    tgt = cu * (1.0 + gap) / dm
                    kk = max(kold * (1 - o['ctrl_step']), min(kold * (1 + o['ctrl_step']), tgt)); kk = max(0.85, min(1.10, kk))
                self.mf[m][i] = kk; new[m][i] = kk
        self.ctl_log.append((day, [[round(self.mf[m][self.GI[g]], 3) for g in RAW14] for m in (0, 1)], len(glut)))
        # village targets: market output of g -> k / 1.03 of the Apply Plan; cuts x3 in glut lands, raises by home work; bounds by home work 0..0.85
        tgt = [list(r) for r in self.out]
        for m in (0, 1):
            vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
            tfm = [sum(self.tf[t][i] for t in range(NT) if self.T[t]['mk'] == m) for i in range(NG)]
            for g in RAW14:
                i = self.GI[g]
                Sstar = self.mf[m][i] / 1.03 * self.P0[m][i]
                cur = sum(self.out[k][i] for k in vs) + tfm[i]
                dlt = Sstar - cur
                if abs(dlt) < 1e-9: continue
                if dlt < 0:
                    w = {k: self.out[k][i] * (3.0 if (self.Vl[k]['land'], i) in glut or (self.town_land[self.Vl[k]['trade']], i) in glut else 1.0) for k in vs}
                else:
                    w = {k: self.out0[k][i] * self.hw[k] for k in vs}
                W_ = sum(w.values())
                if W_ <= 0: continue
                for k in vs:
                    x = self.out[k][i] + dlt * w[k] / W_
                    hwk = min(0.85, self.hw[k])
                    lo = self.out0[k][i] * (1 - 0.85) / max(0.05, 1 - hwk); hi = self.out0[k][i] / max(0.05, 1 - hwk)
                    tgt[k][i] = max(lo, min(hi, x))
        self.vtarget = tgt; self.vt_day = day

    def _load_alpha(self):
        # round-3 alfa per village (ks-budzety.csv last day) and the alpha the current Plan dump used (alpha_net2.csv - KS_ALPHA of the harness)
        self._a3 = {}; last = None
        for row in csv.reader(open(os.path.join(self.W['csv'], 'ks-budzety.csv'), encoding='utf-8'), delimiter=';'):
            if len(row) < 9 or not row[0].isdigit(): continue
            d = int(row[0])
            if last is None or d > last: last = d; self._a3 = {}
            if d < last or row[2] != 'wies': continue
            try: self._a3[row[1]] = float(row[8])
            except ValueError: pass
        au = {}
        ap = self.o.get('alpha_file') or os.path.join(H3OUT, 'alpha_net2.csv')
        for line in open(ap, encoding='utf-8'):
            f = line.strip().split(';')
            if len(f) >= 2:
                try: au[f[0]] = float(f[1])
                except ValueError: pass
        self._a_used = [au.get(v['id'], self._a3.get(v['id'])) for v in self.Vl]
        self._a_next = list(self._a_used)

    def _adopt(self):
        d = self.day
        for k in range(self.NV):
            if (d + self.vslot[k]) % 7 != 0: continue
            if self.vtarget is not None: self.out[k] = list(self.vtarget[k])
            if self.ptarget is not None and hasattr(self, '_a_next'): self._a_used[k] = self._a_next[k]
            if self.ptarget is not None:
                v = self.Vl[k]; v['paid'] = list(self.ptarget[k]); m = v['mk']; NGl = self.NG; kc = self.kcal
                self.budget_v[k] = sum(v['bask'][i] * v['paid'][i] * self.val[i] * self.start_idx[m][i] for i in range(NGl))
                w = sum(v['bask'][i] * kc[i] for i in range(NGl)); p = sum(v['bask'][i] * v['paid'][i] * kc[i] for i in range(NGl))
                pk = p / w if w > 0 else 0.0
                unc = self._pl_unc.get(k, max(0.0, 1.0 - self.pk[k] - self.frac[k])) if hasattr(self, '_pl_unc') else max(0.0, 1.0 - self.pk[k] - self.frac[k])
                self.pk[k] = pk; self.frac[k] = max(0.0, 1.0 - pk - unc)
                self._qv_dirty = True
        if getattr(self, '_qv_dirty', False):
            self._qv_dirty = False
            NT, NGl = self.NT, self.NG
            self.QV = [[0.0] * NGl for _ in range(NT)]
            for v in self.Vl:
                t = v['trade']
                for i in range(NGl): self.QV[t][i] += v['bask'][i] * v['paid'][i]
            self._dp_refresh()

    def snapshot(self):
        rows = []
        for t in range(self.NT):
            d = self.dp[t][self.iGrain]; sh = self.shelf[t][self.iGrain]
            rows.append({'idx': self.idx[t][self.gcat[self.iGrain]], 'shelf': sh, 'dem': d, 'days': sh / d if d > 0 else float('nan'),
                         'want': self.c_want[t], 'got': self.c_got[t], 'ungold': self.c_ungold[t], 'nogoods': self.c_nogoods[t], 'else': self.c_else[t],
                         'purse': sum(self.gold[k] for k in self._vil_of[t]), 'land': self.town_land[t], 'kingdom': self.T[t]['kingdom']})
        return rows


_QVCAL = None
def qvcal_factors(W, o):
    """Factors (m, g) -> f for qvcal=2 from a base sim (round-3 rules, qvcal=1) and the Plan OFF dump (off.csv / off.out NA TARG)."""
    global _QVCAL
    if _QVCAL is not None: return _QVCAL
    ob = dict(o); ob.update(BASE_OFF); ob['qvcal'] = 1
    s0 = R3Sim(W, ob, seed=1, plan_on=None, plan_off=None, init='start')
    doff, _, _ = load_dump(os.path.join(H3OUT, 'off.csv'))
    nat = plan_sd_from(os.path.join(H3OUT, 'off.out'))
    kgpp = kg_per_piece(W['csv'])
    F = {}
    for m in (0, 1):
        ts = [t for t in range(s0.NT) if s0.T[t]['mk'] == m]
        for g in ('meat', 'fish', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'honey'):
            i = s0.GI[g]
            pl = sum(kg_of(r, g) for r in doff.values() if int(r['mk']) == m) / kgpp.get(g, 10.0)
            dt = pl / max(0.5, nat.get((m, g), 1.0))
            qq = sum(s0.Q[t][i] + s0.QV[t][i] for t in ts)
            fixed = sum(s0.der[t][i] + s0.one[t][i] for t in ts) + sum(c['dem'][i] for c in s0.C if c.get('mk', 0) == m)
            if qq > 1.0 and dt > fixed: F[(m, g)] = max(0.25, min(4.0, (dt - fixed) / qq))
    _QVCAL = F
    return F


# ====================================================================== scenario and gates
BASE_OFF = dict(carts=0, paidfirst=0, ownfirst=0, leak=0, zc5=0, realoff=0, ctrl=0, every2=0, h2=0, beta=0, glut=0, remeasure=0)


def scenario(W, o, seed, plan_on, plan_off, init, days=62, keep_sim=False, base_flow=None):
    sim = R3Sim(W, o, seed=seed, plan_on=plan_on, plan_off=plan_off, init=init, base_flow=base_flow)
    D = o['D']; w0, w1 = D + 22, D + 49
    NT, NG = sim.NT, sim.NG
    idx_acc = [[0.0] * sim.NC for _ in range(NT)]; nidx = 0
    st0 = None; purse0 = None; d7 = None
    end = D + days
    while sim.day < end:
        if sim.day == w0:
            sim.reset_counters()
            st0 = [[sum(sim.shelf[t][i] for t in range(NT) if sim.T[t]['mk'] == m) + (sum(c[4] for c in sim.ksh.carts if c[3] == RAW14.index(sim.G[i]) and sim.T[c[2]]['mk'] == m) if sim.ksh and sim.G[i] in RAW14 else 0) for i in range(NG)] for m in (0, 1)]
            purse0 = [sum(sim.gold[k] for k, v in enumerate(sim.Vl) if v['mk'] == m) for m in (0, 1)]
            if sim.ksh:
                sim.ksh.c = collections.defaultdict(float); sim.ksh.prev_transit = sim.ksh.transit()
                if sim.ksh.dbg is not None: sim.ksh.dbg = {}
            vin0 = list(sim.v_in); vfr0 = list(sim.v_fr); vout0 = list(sim.v_out)
        sim.step()
        if sim.day == D + 7:
            d7 = {}; d7med = {}
            for m in (0, 1):
                ts = [t for t in range(NT) if sim.T[t]['mk'] == m]
                for g in RAW14:
                    i = sim.GI[g]; c = sim.gcat[i]
                    med = statistics.median([sim.idx[t][c] for t in ts])
                    d7[(m, g)] = abs(med - sim.start_idx[m][i]) <= 0.3
                    d7med[(m, g)] = med
        if w0 < sim.day <= w1:
            nidx += 1
            for t in range(NT):
                for c in range(sim.NC): idx_acc[t][c] += sim.idx[t][c]
        if sim.day == w1:
            r = collect(sim, st0, purse0, idx_acc, nidx, w0, w1, vin0, vfr0, vout0)
    r['d7'] = d7
    r['d7med'] = d7med
    r['ctl'] = sim.ctl_log
    r['_flow'] = sim.flow_hist
    r['beta_end'] = None
    if keep_sim: r['_sim'] = sim
    return r


def collect(sim, st0, purse0, idx_acc, nidx, w0, w1, vin0, vfr0, vout0):
    NT, NG = sim.NT, sim.NG
    nd = max(1, sim.c_days)
    r = {}
    st1 = [[sum(sim.shelf[t][i] for t in range(NT) if sim.T[t]['mk'] == m) + (sum(c[4] for c in sim.ksh.carts if c[3] == RAW14.index(sim.G[i]) and sim.T[c[2]]['mk'] == m) if sim.ksh and sim.G[i] in RAW14 else 0) for i in range(NG)] for m in (0, 1)]
    sc = {}; ds = {}
    for m in (0, 1):
        for g in RAW14:
            i = sim.GI[g]
            p = sim.c_prod[m][i] / nd; c = sim.c_cons[m][i] / nd
            if c >= 20:
                sc[(m, g)] = p / c; ds[(m, g)] = (st1[m][i] - st0[m][i]) / nd / c
    r['sc'] = sc; r['ds'] = ds
    pr = {}; cf = {}; tw = {}
    for m in (0, 1):
        ts = [t for t in range(NT) if sim.T[t]['mk'] == m]
        for g in RAW14:
            i = sim.GI[g]; c = sim.gcat[i]
            av = [idx_acc[t][c] / max(1, nidx) for t in ts]
            pr[(m, g)] = (statistics.median(av), sim.start_idx[m][i])
            cf[(m, g)] = (sum(1 for t in ts if sim.idx[t][c] >= 9.99) / len(ts), sum(1 for t in ts if sim.idx[t][c] <= 0.101) / len(ts))
            Dw = 0.0; Dlow = 0.0
            for t in ts:
                d = sim.dp[t][i]
                if d <= 0: continue
                Dw += d
                if sim.shelf[t][i] < 0.5 * sim.Ts[m][i] * d: Dlow += d
            tw[(m, g)] = Dlow / Dw if Dw else float('nan')
    r['pr'] = pr; r['cf'] = cf; r['tw'] = tw
    rows = sim.snapshot()
    days = [x['days'] for x in rows if not math.isnan(x['days'])]
    war_lands = set(x['land'] for x in rows if x['kingdom'] in S.WAR_K)
    r['shelf'] = (S.q(days, .1), S.q(days, .5), S.q(days, .9), max(days))
    r['short'] = sum(1 for x in days if x < 0.5) / len(days); r['lt2'] = sum(1 for x in days if x < 2.0) / len(days)
    r['glut'] = sum(1 for x in days if x > 30) / len(days)
    r['glut_nowar'] = sum(1 for x in rows if not math.isnan(x['days']) and x['days'] > 30 and x['land'] not in war_lands)
    r['glut_list'] = sorted([(sim.T[t]['name'], round(x['days'])) for t, x in enumerate(rows) if not math.isnan(x['days']) and x['days'] > 30], key=lambda z: -z[1])[:8]
    r['short_list'] = sorted([sim.T[t]['name'] for t, x in enumerate(rows) if not math.isnan(x['days']) and x['days'] < 0.5])
    gi = [x['idx'] for x in rows]
    r['idx_med'] = S.q(gi, .5); r['ceil'] = sum(1 for x in gi if x >= 9.9) / len(gi)
    W_ = sum(x['want'] for x in rows) or 1
    r['v'] = (sum(x['got'] for x in rows) / W_, sum(x['ungold'] for x in rows) / W_, sum(x['nogoods'] for x in rows) / W_)
    kb = [sim.c_kg[m] / max(1, sim.c_kw[m]) for m in (0, 1)]
    kh = [sim.c_kh[m] / max(1, sim.c_kwant[m]) for m in (0, 1)]
    kr = [sim.c_kr[m] / max(1, sim.c_kw[m]) for m in (0, 1)]
    r['kb'] = tuple(kb); r['kh'] = tuple(kh); r['kr'] = tuple(kr)
    r['unc'] = tuple(max(0.0, 1 - kb[m] - kh[m] - kr[m]) for m in (0, 1))
    r['p1'] = (sim.c_p1[0][1] / max(1, sim.c_p1[0][0]), sim.c_p1[1][1] / max(1, sim.c_p1[1][0]), sim.c_p1[0][2] / max(1, sim.c_p1[0][0]), sim.c_p1[1][2] / max(1, sim.c_p1[1][0]))
    r['p2'] = tuple(sim.c_p2[m][0] / nd for m in (0, 1))
    r['zl'] = tuple(sim.c_zlg[m] / max(1, sim.c_zlp[m]) for m in (0, 1))
    r['zl1'] = tuple(sim.c_zlg1[m] / max(1, sim.c_zlp[m]) for m in (0, 1))
    purse1 = [sum(sim.gold[k] for k, v in enumerate(sim.Vl) if v['mk'] == m) for m in (0, 1)]
    r['drift'] = tuple(((purse1[m] / purse0[m]) ** (1.0 / nd) - 1.0) if purse0[m] > 0 else 0.0 for m in (0, 1))
    vin = [0.0, 0.0]; vfr = [0.0, 0.0]; vout = [0.0, 0.0]; bud = [0.0, 0.0]
    for k, v in enumerate(sim.Vl):
        m = v['mk']; vin[m] += (sim.v_in[k] - vin0[k]) / nd; vfr[m] += (sim.v_fr[k] - vfr0[k]) / nd; vout[m] += (sim.v_out[k] - vout0[k]) / nd; bud[m] += sim.budget_v[k]
    r['bud_net'] = tuple(bud[m] / max(1.0, vin[m] + vfr[m] - vout[m]) for m in (0, 1))
    r['money'] = (sum(purse1), sum(vin), sum(vfr), sum(vout), sim.c_spent / nd)
    r['vnet'] = [((sim.v_in[k] - vin0[k]) / nd, (sim.v_fr[k] - vfr0[k]) / nd, (sim.v_out[k] - vout0[k]) / nd) for k in range(sim.NV)]
    if sim.ksh:
        c = sim.ksh.c
        r['ksh'] = {'pcs_d': c['bought'] / nd, 'lots_d': c['lots'] / nd, 'haul': c['piece_days'] / max(1, c['bought']), 'transit': c['transit_sum'] / max(1, c['days_n']),
                    'gold_d': c['gold'] / nd, 'fr_land_d': c['fr_land'] / nd, 'fr_sea_d': c['fr_sea'] / nd, 'goods_rest': c['goods_rest_abs'],
                    'gold_rest': -c['g_dst'] + c['g_src'] + c['g_carters'] + c['g_crews'], 'poor_share': c['poor'] / max(1, c['ordered']),
                    'sea_share': c['sea_pcs'] / max(1, c['bought']), 'grain_share': c['g_grain'] / max(1, c['bought']),
                    'mar_med': (sorted(sim.ksh.mar_pct)[len(sim.ksh.mar_pct) // 2] if sim.ksh.mar_pct else 0.0), 'mar_res_d': (c['mar_val'] - c['mar_cost']) / nd,
                    'noprofit_d': c['noprofit'] / nd, 'spoil_d': c['spoil'] / nd,
                    'cof_q10': (sorted(sim.ksh.cof_short)[int(0.1 * (len(sim.ksh.cof_short) - 1))] if sim.ksh.cof_short else 0.0),
                    'cof_med': (sorted(sim.ksh.cof_short)[(len(sim.ksh.cof_short) - 1) // 2] if sim.ksh.cof_short else 0.0)}
    r['own_pcs_d'] = sim.c_own_pcs / nd
    r['mf'] = [[round(sim.mf[m][sim.GI[g]], 3) for g in RAW14] for m in (0, 1)]
    return r


def avg(rs):
    out = {}
    for k in rs[0]:
        x = rs[0][k]
        if k in ('ctl', '_sim', 'beta_end', 'vnet', 'glut_list', 'short_list', 'mf', '_flow'): out[k] = x; continue
        if k == 'd7med': out[k] = {kk: sum(r[k][kk] for r in rs) / len(rs) for kk in x}; continue
        if x is None: out[k] = None; continue
        if isinstance(x, dict):
            out[k] = {}
            for kk in x:
                vals = [r[k].get(kk) for r in rs]
                if isinstance(vals[0], tuple): out[k][kk] = tuple(sum(v[j] for v in vals) / len(vals) for j in range(len(vals[0])))
                elif isinstance(vals[0], bool): out[k][kk] = sum(1 for v in vals if v) / len(vals)
                else: out[k][kk] = sum(vals) / len(vals)
        elif isinstance(x, tuple):
            out[k] = tuple(sum(r[k][j] for r in rs) / len(rs) for j in range(len(x)) if not isinstance(x[j], (tuple, list)))
        elif isinstance(x, (int, float)): out[k] = sum(r[k] for r in rs) / len(rs)
        else: out[k] = x
    return out


def gates(a, plan_sd):
    """S-G1..S-G9 on the averaged result a; plan_sd: (m, g) -> Plan NA TARG S/D (exceptions)."""
    G = collections.OrderedDict()
    mk = {0: 'W', 1: 'E'}
    g1 = [(m, a['sc'].get((m, 'grain'))) for m in (0, 1)]
    G['S-G1 grain S/C 0.95-1.05'] = (all(x is not None and 0.95 <= x <= 1.05 for _, x in g1), ', '.join('%s %.3f' % (mk[m], x) for m, x in g1))
    bad = []
    for (m, g), x in sorted(a['sc'].items()):
        if g in BAND_EXC:
            ref = plan_sd.get((m, g), 1.0)
            if abs(x / ref - 1) > 0.10: bad.append('%s %s %.2f (Plan %.2f)' % (mk[m], g, x, ref))
        elif not (0.95 <= x <= 1.05): bad.append('%s %s %.2f' % (mk[m], g, x))
    G['S-G2 raw food S/C 0.95-1.05 (exc +-10%% of Plan)'] = (not bad, '%d/%d out: %s' % (len(bad), len(a['sc']), ', '.join(bad)))
    q10, med, q90, mx = a['shelf']
    T_grain = tstar(1.5)
    ok3 = a['short'] <= 0.05 and a['lt2'] <= 0.10 and q90 <= 3 * T_grain and a['glut_nowar'] < 0.5
    G['S-G3 grain towns'] = (ok3, '<0.5 d %.0f%% (<=5), <2 d %.0f%% (<=10), q10/med/q90 %.1f/%.1f/%.1f d (q90 <= %.1f), >30 d outside war lands %.1f towns (0)' % (
        100 * a['short'], 100 * a['lt2'], q10, med, q90, 3 * T_grain, a['glut_nowar']))
    g_in = [abs(a['pr'][(m, 'grain')][0] - a['pr'][(m, 'grain')][1]) <= 0.3 for m in (0, 1)]
    oth = [(m, g) for (m, g) in a['pr'] if g != 'grain']
    oin = [k for k in oth if abs(a['pr'][k][0] - a['pr'][k][1]) <= 0.3]
    bad7 = ['%s %s %.2f' % (mk[m], g, a['pr'][(m, g)][0]) for (m, g) in sorted(oth) if (m, g) not in oin]
    G['S-G4 NKS-7 median in start +-0.3'] = (all(g_in) and len(oin) >= 0.75 * len(oth), 'grain W %.2f E %.2f; others in band %d/%d (>= 75%%); out: %s' % (
        a['pr'][(0, 'grain')][0], a['pr'][(1, 'grain')][0], len(oin), len(oth), ', '.join(bad7)))
    bad7b = ['%s %s s%.0f f%.0f' % (mk[m], g, 100 * x[0], 100 * x[1]) for (m, g), x in sorted(a['cf'].items()) if x[0] > 0.2 or x[1] > 0.2]
    G['S-G5 NKS-7b ceil/floor <= 20%'] = (not bad7b, '%d/%d out: %s' % (len(bad7b), len(a['cf']), ', '.join(bad7b)))
    G['S-G6 uncovered <= 5%'] = (all(u <= 0.05 for u in a['unc']), 'W %.1f%% E %.1f%% (kb %.0f/%.0f, kh %.0f/%.0f, kr %.1f/%.1f)' % (
        100 * a['unc'][0], 100 * a['unc'][1], 100 * a['kb'][0], 100 * a['kb'][1], 100 * a['kh'][0], 100 * a['kh'][1], 100 * a['kr'][0], 100 * a['kr'][1]))
    G['S-G7 purses -0.1..+0.5 %/d'] = (all(-0.001 <= d <= 0.005 for d in a['drift']), 'W %+.2f%%/d E %+.2f%%/d' % (100 * a['drift'][0], 100 * a['drift'][1]))
    k = a.get('ksh')
    if k:
        ok8 = k['pcs_d'] <= 25000 and k['transit'] <= 200000 and k['haul'] <= 12 and abs(k['goods_rest']) < 0.5 and abs(k['gold_rest']) < 0.5
        G['S-G8 merchants'] = (ok8, '%.0f pcs/d (<=25k), %.0f lots/d, haul %.1f d (<=12), in transit %.0f (<=200k), grain %.0f%%, sea %.0f%%, buy %.0f zl/d, freight land %.0f sea %.0f zl/d, money cap %.1f%% of ordered, identities goods %.0f gold %.0f' % (
            k['pcs_d'], k['lots_d'], k['haul'], k['transit'], 100 * k['grain_share'], 100 * k['sea_share'], k['gold_d'], k['fr_land_d'], k['fr_sea_d'], 100 * k['poor_share'], k['goods_rest'], k['gold_rest'])
            + ' | round 4: merchants median margin %.1f%%, result %.0f zl/d, lots skipped without profit %.1f/d, spoiled %.0f pcs/d, short-town coffers / reserve q10 %.2f med %.2f' % (
            100 * k.get('mar_med', 0), k.get('mar_res_d', 0), k.get('noprofit_d', 0), k.get('spoil_d', 0), k.get('cof_q10', 0), k.get('cof_med', 0)))
    else:
        G['S-G8 merchants'] = (None, 'carts off')
    d7 = a.get('d7') or {}
    share = sum(d7.values()) / len(d7) if d7 else float('nan')
    G['S-G9 D+7 half of market medians in band'] = (share >= 0.5, '%.0f%% of %d' % (100 * share, len(d7)))
    return G


def report(name, a, plan_sd):
    print('=== %s' % name)
    G = gates(a, plan_sd)
    for kk, (ok, txt) in G.items():
        print('  %-48s %s  %s' % (kk, {True: 'PASS', False: 'FAIL', None: '-'}[ok], txt))
    print('  NKS-6 stock change > 2%%/d: %s' % ', '.join('%s %s %+.1f%%' % ('WE'[m], g, 100 * x) for (m, g), x in sorted(a['ds'].items()) if abs(x) > 0.02))
    print('  NKS-23b D in towns shelf < 0.5 T*: %s' % ', '.join('%s %s %.0f%%' % ('WE'[m], g, 100 * x) for (m, g), x in sorted(a['tw'].items()) if x == x and x > 0.10))
    print('  villages: bought %.0f%% / no money %.0f%% / no goods %.0f%% (pieces); pass 1 no goods W %.0f%% E %.0f%%, no money W %.0f%% E %.0f%%; pass 2 %.0f/%.0f pcs/d; value bought/Plan-paid W %.2f E %.2f (pass 1 %.2f/%.2f); Plan budget / net inflow W %.2f E %.2f; own-first %.0f pcs/d' % (
        100 * a['v'][0], 100 * a['v'][1], 100 * a['v'][2], 100 * a['p1'][0], 100 * a['p1'][1],
        100 * a['p1'][2], 100 * a['p1'][3], a['p2'][0], a['p2'][1], a['zl'][0], a['zl'][1], a['zl1'][0], a['zl1'][1], a['bud_net'][0], a['bud_net'][1], a['own_pcs_d']))
    print('  money: purses %.2f M, inflow %.0f, freight to villages %.0f, other outflows %.0f, basket spending %.0f zl/d' % ((a['money'][0] / 1e6,) + tuple(a['money'][1:])))
    print('  grain idx med %.2f, ceiling %.0f%%; glut towns: %s; short towns: %s' % (a['idx_med'], 100 * a['ceil'], a['glut_list'], ', '.join(a['short_list'][:20])))
    print('  market factor (last seed) W %s | E %s' % (a['mf'][0], a['mf'][1]))
    if a.get('d7med'): print('  D+7 market medians (start idx): ' + ', '.join('%s %s %.2f%s' % ('WE'[m], g, x, '' if a['d7'][(m, g)] >= 0.5 else '*') for (m, g), x in sorted(a['d7med'].items())))


def plan_sd_from(out_path):
    """Plan NA TARG S/D per market x good from a harness .out file (line 'W NA TARG (S-zj)/(D-zj): ...')."""
    sd = {}
    try:
        for line in open(out_path, encoding='utf-8'):
            for m, tag in ((0, '  W NA TARG (S-zj)/(D-zj): '), (1, '  E NA TARG (S-zj)/(D-zj): ')):
                if line.startswith(tag):
                    for part in line[len(tag):].strip().split(', '):
                        g, x = part.rsplit(' ', 1); sd[(m, g)] = float(x)
    except OSError:
        pass
    return sd


def main(argv):
    wname = 'world_r2.json'
    if '--world' in argv: j = argv.index('--world'); wname = argv[j + 1]; argv = argv[:j] + argv[j + 2:]
    W = json.load(open(os.path.join(HERE, wname), encoding='utf-8'))
    o = dict(DEF); seeds = 3; init = 'split'; tag = 'R3PORT'; days = 62; show_base = False
    plan_on = os.path.join(H3OUT, 's1_on.csv'); plan_off = os.path.join(H3OUT, 'off.csv')
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == '--seeds': seeds = int(argv[i + 1]); i += 2; continue
        if a == '--init': init = argv[i + 1]; i += 2; continue
        if a == '--tag': tag = argv[i + 1]; i += 2; continue
        if a == '--days': days = int(argv[i + 1]); i += 2; continue
        if a == '--plan-on': plan_on = argv[i + 1]; i += 2; continue
        if a == '--plan-off': plan_off = argv[i + 1]; i += 2; continue
        if a == '--no-plan': plan_on = None; i += 1; continue
        if a == '--base': show_base = True; i += 1; continue
        k, v = a.split('='); o[k] = float(v) if '.' in v else int(v); i += 1
    t0 = time.time()
    rs = []; rb = []
    for sd in range(1, seeds + 1):
        ob = dict(o); ob.update(BASE_OFF)
        b = scenario(W, ob, sd, None, None, init, days=days)
        rb.append(b)
        rs.append(scenario(W, o, sd, plan_on, plan_off, init, days=days, base_flow=b['_flow'] if o.get('coffers', 1) else None))
    if show_base: report('BASE round-3 rules (port, switches off) init=%s' % init, avg(rb), {})
    a = avg(rs)
    sdp = plan_sd_from(plan_on.replace('.csv', '.out')) if plan_on else {}
    report('%s init=%s plan=%s %s' % (tag, init, os.path.basename(plan_on) if plan_on else 'round-3', ' '.join('%s=%s' % (k, v) for k, v in o.items() if DEF.get(k) != v)), a, sdp)
    print('time %.1f s' % (time.time() - t0))
    return a


if __name__ == '__main__':
    main(sys.argv[1:])
