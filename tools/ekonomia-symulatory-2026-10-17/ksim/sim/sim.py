# KSIM - offline TOWN-LEVEL daily market simulator of the KS goods (food + drinks), Python 3 stdlib only (run with python -I).
# Purpose: accept a fix of the KS start (food trade between towns, per-town / corridor Plan) BEFORE any in-game test. Nothing here touches the game.
#
# FILES (all in this folder)
#   build_world.py  reads the round-3 autotest (D:/Backup-Bannerlord/logi-testow/2026-10-17_01-15-30 CSVs + Armoury-2026-10-17_01-15-30.log),
#                   ks_dump.json (needs per district) and roz/geo-dane (road graph) -> world.json. Args: [csvdir] [log] [plan_day=109179].
#   fit_villages.py per-village takings multiplier (current rules) so each village's purse inflow 109151-109179 matches ks-budzety -> vfit.json.
#                   Re-run after build_world.py or after changing DEFAULTS (3-4 iterations, ~10 s).
#   sim.py          the simulator (this file) + trade modules; CLI prints the weekly sim-vs-run comparison.
#   calib.py        calibration report vs the failed run (python -I calib.py [--seeds 5] [key=value]).
#   weekly.py       weekly trajectory sim vs run (python -I weekly.py 3).
#   accept.py       acceptance run of a scenario vs the current rules with NKS-5/6/7/7b recomputed per market + town distribution + villages
#                   (python -I accept.py --trade carts --plan local --seeds 2; --init start = from the KS start D 109118, window D+22..D+49).
#   check_static.py, diag.py, sinkcmp.py, goodscmp.py, pricecmp.py, towns_cmp.py, grid.py: diagnostics used for the calibration.
#
# RUN   python -I sim.py [--days 70] [--start 109123] [--init obs|start] [--trade caravans|none|carts[:k=v...]]
#                        [--plan current|local[:cap=3]|csv=<ks-plan.csv>[@day]] [--set key=value ...] [--seed 1] [--quiet] [--csv out.csv]
#   --init obs   start from the run's state on --start (grain shelves and village purses per town from ks-miasta, other goods from the ledger
#                market stock split 50/50 by demand and local supply); --init start = KS start shelves (7.14 x start index^-5/3 days of D-term).
#   ~1.5 s per 70 simulated days.
#
# MODULES (pluggable)
#   trade: a class with daily(sim, day), registered in make_trade(). 'caravans' = BK caravans as measured (ledger volumes, cheapest -> dearest
#     towns, no distance); 'carts' = example fix: merchant food export by cart (FoodCarts: surplus above src_days, buyers below dst_days, cheapest
#     landed price incl. 5%/100 units, land+sea road, travel days at wagon speed, perishables <= 7 days; params carts:src_days=10:dst_days=3:...).
#   plan: Sim.apply_plan(); 'csv=PATH[@DAY]' loads village to-market output from any ks-plan.csv-format file (e.g. a per-town Plan from the
#     C# harness), 'local[:cap=3]' = idealised per-town Plan (each catchment's output scaled to its town's paid D-term, market totals kept).
#
# MECHANICS (each mirrors the game code named in brackets)
#   villages: KS to-market output into the warehouse; one wagon per village leaves every `cycle` days with the whole store
#     [MarketCarts.Choose: town within MarketMaxDistance 250 by land road (own town always), score = value of the load sold piece by piece x
#     min(1, 5 / round-trip days), road news = goods other wagons are carrying; hostile towns skipped]; castle villages stop at their castle first
#     and sell the castle the food it lacks for 10 days [CastleMarket]; takings x 0.552 to the village purse (35.6% lord tax + 9.3% BK estates).
#     Non-food goods (wood, ore, fur, fibre, hides, clay, horses) are revenue only (base value x nf_idx).
#   town fields: KS output straight onto the town shelf [DeliverTownFields].
#   ZC5: bread/pie/beer from grain, wine from grape, oil from olives, mead from honey, from the town's own shelf, output up to 7 days of what
#     the town buys, input only above 2 days of direct need + 5 pieces, hands of town craftsmen, profit per man-day [StartCalibration.Crafts].
#   town folk: buy the Plan-paid town basket Q x measured affordability of the W/R/P purses (kiesa-ludu stac_jedzenie); missing bread/pie ->
#     grain, missing beer -> grain 0.4 per litre [Zc4DictPostfix, AnchorDict].
#   villages: buy the full basket with the purse along the ladder (grain, salt, drinks cheapest per litre with wine->beer->grain, protein
#     cheapest per kcal, rest), pieces above 5 on the shelf, empty purse stops; missing goods in 2 near towns (F2) [TownFolk.Zc4BuyCore];
#     counters as Zc4NoteTown (bought incl. substitutes and F2, no money, no goods minus bought elsewhere).
#   other purse outflows (old-list surplus buying of salt, war levy, BK village market, dues) [TownFolk.Villages, KingdomTreasury].
#   lords buy party food and abandon leftovers, courts eat, castles eat their own stock, party food counted as consumption (ledger sinks,
#     spread by market-people share).
#   prices: D-term = lambda x (Q + QV + Der) + One (value), S/D averages 0.85/0.15, index = clamp((D / (0.1 S + 0.04 V + c0))^0.6, 0.1, 10)
#     [DefaultTradeItemPriceFactorModel, DefaultSettlementEconomyModel, StartCalibration.Zc4Demand]; fruit (apple, carrot, orange) one category.
# Fitted inputs (build_world.py): village baskets per trade town = the wants the game counted (ks-miasta), village Plan-paid shares per town =
#   the grain D-term the game counted, town baskets Q scaled where the D-term needs it, castle-village TradeBound = nearest town of the castle's
#   kingdom, oil demand split by market from the ledger.
import json, math, os, sys, random, time, collections

HERE = os.path.dirname(os.path.abspath(__file__))

DEFAULTS = {
    'range': 250.0,        # Settings.MarketMaxDistance (land road units)
    'speed': 41.0,         # wagon road units per day (3.43 x 24 x WorldPace 50%)
    'cycle': 3.8,          # days between departures of a village wagon when the trip is short (measured ~116-128 departures/d for 569 villages)
    'wait': 0.486,         # MarketCarts.CycleWait (days)
    'store_days': 5.0,     # warehouse = 5 days of production (score factor min(1, 5 / T))
    'purse_share': 0.552,  # share of wagon takings that reaches the village purse (log: Przeplywy osad)
    'other_rate': 0.018,   # other purse outflows per day on the purse above the floor (surplus buying, war levy, BK village market; fit: proportional ~2%/d)
    'floor_days': 0.0,     # floor in days of the old village basket nv (game: 15 x nv; the run's purse path fits a floor ~0)
    'nv_total': 100000.0,  # old village basket nv, zl/d world (VillagePurchasePerHead x heads), spread by people
    'dues': 7345.0,        # fixed dues per day, world (spread by people)
    'keep': 5,             # village buys only pieces above 5 on the shelf
    'c0': 2.0,             # constant of the vanilla price formula
    'castle_days': 10.0,   # castle buys food from its villages' wagons up to 10 days of its demand
    'zc5_hands': 1.0,      # multiplier on ZC5 hands (craftsmen x 0.55 / sigma x 0.5)
    'zc5_out_days': 7.0, 'zc5_in_days': 2.0,
    'zc5_margin': 0.0,     # profit rule: make only if out price >= (1 + margin) x input price x ratio
    'home_kcal_w': 0.431, 'home_kcal_e': 0.362,   # Plan: share of basket kcal made at home (for the 'no coverage' metric only)
    'car_scale': 1.0,      # caravans: multiplier on measured volumes
    'lam_day0': 109117.0, 'lam_days': 14.0,
    'dist_k': 1.18,        # village-town road = geo estimate x dist_k (geo graph / 1.25 x straight line under-estimates the game's MapDistanceModel; fit to the measured mean trip)
    'war': 2,              # hostile towns skipped by wagons and F2: 0 none, 1 every foreign kingdom, 2 only kingdoms in WAR_K (vs everyone)
    'old_salt': 0.75,      # old village list bought from the purse surplus (TownFolk.Villages BuyGoods, salt and tools only under P5): extra salt x basket salt per day
    'folk_afford': 1,      # town folk buy Q x the measured affordability of their food basket (kiesa-ludu stac_jedzenie; ~1 in 86 of 97 towns)
    'price_order': 0,      # 0: prices after every offtake of the day; 1: right after ZC5 + town folk (town daily tick), before villages / lords / caravans
    'init_transit': 1,     # start with one load per village already on the road (the run was in steady flow at the start day)
    'vfit': 1,             # 1 = per-village takings multiplier from vfit.json (fit of the current run's purse inflow per village, ks-budzety do_kiesy_28d)
    'nf_idx': 1.8,         # price index of the non-food goods and horses village wagons also sell (wood, ore, fur, fibre, hides, clay) - revenue only
}

START_IDX = {('grain', 0): 1.5, ('grain', 1): 1.5, ('cheese', 0): 1.08, ('cheese', 1): 1.41, ('butter', 0): 1.43, ('butter', 1): 1.50,
             ('olives', 0): 0.7, ('olives', 1): 0.7}


WAR_K = {'Dothraki Horde', 'Free Folk'}


def q(xs, p):
    xs = sorted(xs)
    if not xs: return float('nan')
    k = (len(xs) - 1) * p; i = int(k); j = min(i + 1, len(xs) - 1)
    return xs[i] + (xs[j] - xs[i]) * (k - i)


class Sim:
    def __init__(self, W, P=None, trade='caravans', plan='current', init='obs', start=109123, seed=1):
        self.W = W
        self.P = dict(DEFAULTS); self.P.update(P or {})
        self.rng = random.Random(seed)
        G = W['goods']; self.G = G; self.GI = {g: i for i, g in enumerate(G)}; NG = len(G); self.NG = NG
        self.val = W['val']; self.kcal = W['kcal']
        cats = []
        for c in W['cat']:
            if c not in cats: cats.append(c)
        self.cats = cats; self.gcat = [cats.index(c) for c in W['cat']]; self.NC = len(cats)
        self.catg = [[i for i in range(NG) if self.gcat[i] == c] for c in range(self.NC)]
        self.T = W['towns']; NT = len(self.T); self.NT = NT
        self.Vl = W['villages']; NV = len(self.Vl); self.NV = NV
        wm = int(self.P['war'])
        def hostile(ka, kb):
            if wm == 0 or ka == kb or ka in ('-', None) or kb in ('-', None): return False
            if wm == 1: return True
            return ka in WAR_K or kb in WAR_K
        for v in self.Vl:
            kv = v.get('kingdom', '-')
            v['_cand'] = [(t, d * self.P['dist_k']) for (t, d) in v['cand'] if not hostile(kv, self.T[t]['kingdom'])]
            v['_near'] = [t for t in v['near'] if not hostile(self.T[v['trade']]['kingdom'], self.T[t]['kingdom'])]
        self.C = W['castles']
        for v in self.Vl:
            if v.get('castle_i', -1) >= 0: self.C[v['castle_i']]['mk'] = v['mk']
        self.Q = W['Q']; self.tf = W['tf']; self.one_share = W['one_share']
        self.day = start
        g = self.GI
        self.iGrain, self.iSalt, self.iBeer, self.iWine, self.iMead = g['grain'], g['salt'], g['beer'], g['wine'], g['mead']
        self.iBread, self.iPie = g['bread'], g['pie']
        self.drinks = [g['beer'], g['wine'], g['mead']]
        self.protein = [g['meat'], g['fish'], g['cheese'], g['butter'], g['Egg']]
        self.prot_kcal = {g['meat']: 2200, g['fish']: 1100, g['cheese']: 3600, g['butter']: 7200, g['Egg']: 1450}
        self.rest = [g[x] for x in ('apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'oil', 'honey')]
        self.pairs = [(g[o], g[i], r, 10.0 / kd) for (o, i, r, kd) in W['pairs']]   # (out, in, input pcs per out pc, man-days per out pc)
        self.start_idx = [[START_IDX.get((G[i], m), 1.0) for i in range(NG)] for m in (0, 1)]
        # ---- plan module (village to-market output)
        self.out = [list(v['out']) for v in self.Vl]
        if plan != 'current': self.apply_plan(plan)
        # ---- per-town Plan-paid village demand QV (price D-term and ZC5 'what the town buys'), Der, One
        self.trade_town = [v['trade'] for v in self.Vl]
        self.QV = [[0.0] * NG for _ in range(NT)]
        for vi, v in enumerate(self.Vl):
            t = v['trade']
            for i in range(NG): self.QV[t][i] += v['bask'][i] * v['paid'][i]
        one_w = W['zc4']['One']
        self.one = [[one_w.get(G[i], 0.0) * self.one_share[t] for i in range(NG)] for t in range(NT)]
        self.der = [[0.0] * NG for _ in range(NT)]
        for t in range(NT):
            for (o, i, r, lab) in self.pairs: self.der[t][i] += (self.Q[t][o] + self.QV[t][o]) * r
        # hands of ZC5 (man-days/d)
        sig = W['sigma']
        self.hands = [self.P['zc5_hands'] * tw['craft'] * 0.55 / sig * 0.5 for tw in self.T]
        # ---- sinks from the ledger (pieces/d world) split by market-people share
        S = W['sinks']
        def sink(gname, k): return S.get(gname, {}).get(k, 0.0)
        self.lords_buy = [[sink(G[i], 'lords_buy') * self.one_share[t] for i in range(NG)] for t in range(NT)]
        self.lords_drop = [[sink(G[i], 'lords_drop') * self.one_share[t] for i in range(NG)] for t in range(NT)]
        self.court = [[sink(G[i], 'court') * self.one_share[t] for i in range(NG)] for t in range(NT)]
        # party food eaten by lords' parties (ledger 'zywnosc partii'): from their own packs (bought in towns above) - counted as market consumption only
        self.party = [[sink(G[i], 'party') * self.one_share[t] for i in range(NG)] for t in range(NT)]
        self.car_buy = [sink(G[i], 'car_buy') * self.P['car_scale'] for i in range(NG)]
        self.car_sell = [sink(G[i], 'car_sell') * self.P['car_scale'] for i in range(NG)]
        # ---- state
        self.shelf = [[0.0] * NG for _ in range(NT)]
        self.Sv = [[0.1] * self.NC for _ in range(NT)]
        self.Dv = [[0.0] * self.NC for _ in range(NT)]
        self.idx = [[1.0] * self.NC for _ in range(NT)]
        self.cstock = [[0.0] * NG for _ in self.C]
        self.wh = [[0.0] * NG for _ in range(NV)]
        self.nf = [float(v.get('nonfood', 0.0)) for v in self.Vl]; self.wh_nf = [0.0] * NV
        self.rv = [1.0] * NV
        fp = os.path.join(HERE, 'vfit.json')
        if self.P['vfit'] and os.path.exists(fp):
            fit = json.load(open(fp, encoding='utf-8'))
            for k, v in enumerate(self.Vl): self.rv[k] = float(fit.get(v['id'], 1.0))
            if 'nf_idx' in fit.get('_P', {}) and 'nf_idx' not in (P or {}): self.P['nf_idx'] = fit['_P']['nf_idx']
        self.gold = [float(v.get('purse0', 0.0)) for v in self.Vl]
        self.carry = [[0.0] * NG for _ in range(NV)]
        self.next_dep = [start + self.rng.random() * self.P['cycle'] for _ in range(NV)]
        self.trips = []            # (arrive_day, village, town, load list)
        self.news = [[0.0] * NG for _ in range(NT)]
        self.car_inv = [[0.0] * NG for _ in (0, 1)]
        self.home_v = [self.P['home_kcal_w'] if v['mk'] == 0 else self.P['home_kcal_e'] for v in self.Vl]
        self.budget_v = []         # Plan budget per village per day (value of the paid basket at start prices) - other-outflow floor
        for vi, v in enumerate(self.Vl):
            m = v['mk']
            self.budget_v.append(sum(v['bask'][i] * v['paid'][i] * self.val[i] * self.start_idx[m][i] for i in range(NG)))
        PP = sum(v['people'] for v in self.Vl)
        self.dues_v = [self.P['dues'] * v['people'] / PP for v in self.Vl]
        self.floor_v = [self.P['floor_days'] * self.P['nv_total'] * v['people'] / PP for v in self.Vl]
        self.trade_mod = make_trade(trade, self)
        self.init_state(init, start)
        self.reset_counters()
        self.hist = []

    # ------------------------------------------------------------ plan modules
    def apply_plan(self, plan):
        if plan.startswith('csv='):
            # ks-plan.csv format (dzien;okreg_id;...;towar;...;na_targ_szt_d) - any day, e.g. a per-town Plan from the C# harness: csv=PATH[@DAY]
            body = plan[4:]
            path, day = (body.rsplit('@', 1) + [None])[:2] if '@' in body else (body, None)
            import csv
            idx = {v['id']: k for k, v in enumerate(self.Vl)}
            new = {}
            live = {'cow': 6.0, 'hog': 2.0, 'sheep': 1.0, 'WhaleMeat': 1.0}
            for r in csv.DictReader(open(path, encoding='utf-8'), delimiter=';'):
                if day and r['dzien'] != day: continue
                o = r['okreg_id']
                if o not in idx: continue
                a = new.setdefault(o, [0.0] * self.NG)
                g = r['towar']; x = float(r['na_targ_szt_d'])
                if g in self.GI: a[self.GI[g]] += x
                elif g in live: a[self.GI['meat']] += x * live[g]
            for o, a in new.items(): self.out[idx[o]] = a
        elif plan.startswith('local'):
            # idealised per-town Plan (upper bound for a 'kraina'/corridor plan): the to-market output of each good in each trade-town catchment is
            # scaled towards that town's paid D-term (what the Plan balances against), the market total per good unchanged; factor capped at
            # `cap` (hands and land do not multiply without limit: plan 'local:cap=3'); catchments without producers keep 0 and must import.
            cap = 3.0
            for part in plan.split(':')[1:]:
                k, v = part.split('=')
                if k == 'cap': cap = float(v)
            NT = len(self.W['towns'])
            QV = [[0.0] * self.NG for _ in range(NT)]
            for v in self.Vl:
                for i in range(self.NG): QV[v['trade']][i] += v['bask'][i] * v['paid'][i]
            der = [[0.0] * self.NG for _ in range(NT)]
            for t in range(NT):
                for (o, i, r, lab) in self.pairs: der[t][i] += (self.Q[t][o] + QV[t][o]) * r
            one_w = self.W['zc4']['One']
            for i in range(self.NG):
                for m in (0, 1):
                    vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
                    tot = sum(self.out[k][i] for k in vs)
                    if tot <= 0: continue
                    sup = [0.0] * NT
                    for k in vs: sup[self.Vl[k]['trade']] += self.out[k][i]
                    need = [max(0.0, self.Q[t][i] + QV[t][i] + der[t][i] + one_w.get(self.G[i], 0.0) * self.one_share[t] - self.tf[t][i])
                            if self.T[t]['mk'] == m and sup[t] > 0 else 0.0 for t in range(NT)]
                    f = [0.0] * NT; fixed = [False] * NT
                    for _ in range(10):   # water-filling with the cap
                        free_need = sum(need[t] for t in range(NT) if not fixed[t])
                        rest = tot - sum(sup[t] * f[t] for t in range(NT) if fixed[t])
                        if free_need <= 0: break
                        again = False
                        for t in range(NT):
                            if fixed[t] or sup[t] <= 0: continue
                            f[t] = rest * need[t] / free_need / sup[t]
                            if f[t] > cap: f[t] = cap; fixed[t] = True; again = True
                        if not again: break
                    for k in vs:
                        t = self.Vl[k]['trade']
                        if sup[t] > 0: self.out[k][i] *= f[t]
        else:
            raise ValueError('unknown plan ' + plan)

    # ------------------------------------------------------------ init
    def dterm(self, t, i):
        lam = self.lam(self.T[t]['mk'])
        return lam * (self.Q[t][i] + self.QV[t][i] + self.der[t][i]) + self.one[t][i]

    def lam(self, m):
        x = (self.day - self.P['lam_day0']) / self.P['lam_days']
        return max(0.0, min(1.0, x))

    def init_state(self, init, start):
        W = self.W; NG = self.NG; NT = self.NT
        obs = W['obs'].get(str(start)); keys = W['obs_keys']
        ryn = W['rynki'].get(str(start)) or W['rynki'].get(str(start - 1))
        if init == 'obs' and obs is None: raise ValueError('no ks-miasta snapshot at day %d' % start)
        # local supply of every town (own catchment + town fields): prior for where stock lies
        loc = [[self.tf[t][i] for i in range(NG)] for t in range(NT)]
        for k, v in enumerate(self.Vl):
            for i in range(NG): loc[v['trade']][i] += self.out[k][i]
        for t in range(NT):
            m = self.T[t]['mk']
            for i in range(NG):
                d = self.dterm(t, i)
                if init == 'start' or obs is None:
                    I = self.start_idx[m][i]
                    days = 7.14 * I ** (-1 / 0.6)
                    self.shelf[t][i] = days * d
                else:
                    if i == self.iGrain:
                        self.shelf[t][i] = obs[t][keys.index('zboze_polka_szt')]
                    else:
                        # market stock of the day (ledger) split 50/50 by demand and by local supply
                        st = ryn[str(m)].get(self.G[i], [0, 0, 0])[2] if ryn and str(m) in ryn else 0.0
                        dm = sum(self.dterm(u, i) for u in range(NT) if self.T[u]['mk'] == m)
                        lm = sum(loc[u][i] for u in range(NT) if self.T[u]['mk'] == m)
                        a = 0.5 * (d / dm if dm > 0 else 0) + 0.5 * (loc[t][i] / lm if lm > 0 else 0)
                        self.shelf[t][i] = st * a
        if init == 'obs':
            for k, v in enumerate(self.Vl):
                pass   # purses from build (first ks-miasta day); rescale to the start day below
            if start != W['kdays'][0]:
                ki = keys.index('kiesy_wsi_zl')
                for t in range(NT):
                    vs = [k for k, v in enumerate(self.Vl) if v['trade'] == t]
                    s0 = sum(self.gold[k] for k in vs)
                    tgt = obs[t][ki]
                    for k in vs: self.gold[k] = self.gold[k] * tgt / s0 if s0 > 0 else tgt / len(vs)
        # castles at half of their target
        for c, cs in enumerate(self.C):
            for i in range(NG): self.cstock[c][i] = 0.5 * self.P['castle_days'] * cs['dem'][i]
        # village warehouses: random part of a cycle; the previous load on the road to the trade town (arrives within 2.5 days)
        for k in range(self.NV):
            f = self.rng.random()
            for i in range(NG): self.wh[k][i] = self.out[k][i] * f * self.P['cycle']
            self.wh_nf[k] = self.nf[k] * f * self.P['cycle']
            if self.P['init_transit']:
                t = self.Vl[k]['trade']
                load = [(i, int(self.out[k][i] * self.P['cycle'])) for i in range(NG) if self.out[k][i] * self.P['cycle'] >= 1]
                self.trips.append((start + self.rng.random() * 2.5, k, t, load, self.nf[k] * self.P['cycle']))
                for i, n in load: self.news[t][i] += n
        # caravan holdings (ledger) split by market people
        h = {g: self.W['sinks'].get(g, {}).get('h_car', 0.0) for g in self.G}
        mp = [0.0, 0.0]
        for t in range(NT): mp[self.T[t]['mk']] += self.one_share[t]
        for m in (0, 1):
            for i in range(NG): self.car_inv[m][i] = h[self.G[i]] * mp[m] * 0.2
        # price state at steady value
        for t in range(NT):
            for c in range(self.NC):
                V = sum(self.shelf[t][i] * self.val[i] for i in self.catg[c])
                E = sum(self.dterm(t, i) * self.val[i] for i in self.catg[c])
                self.Sv[t][c] = max(0.1, V); self.Dv[t][c] = E
                self.idx[t][c] = self.index(E, max(0.1, V), V)

    # ------------------------------------------------------------ prices
    def index(self, D, S, V):
        den = 0.1 * S + 0.04 * V + self.P['c0']
        x = (D / den) ** 0.6 if D > 0 else 0.0
        return 0.1 if x < 0.1 else (10.0 if x > 10.0 else x)

    def buy_price(self, t, i):
        return max(1, math.ceil(self.val[i] * self.idx[t][self.gcat[i]] - 1e-9))

    def sell_value(self, t, i, n, extra=0.0):
        """value a wagon gets for n pieces of good i in town t, piece by piece with the shelf rising (3-point rule, like MarketCarts.Upper)"""
        if n <= 0: return 0.0
        c = self.gcat[i]; v = self.val[i]
        V0 = sum(self.shelf[t][j] * self.val[j] for j in self.catg[c]) + extra
        D = self.Dv[t][c]; S = self.Sv[t][c]
        def p(m): return max(1.0, math.floor(v * self.index(D, S, V0 + (m + 1) * v)))
        if n < 8: return n * p(0)
        m1 = n / 8.0; m2 = n / 2.0
        return m1 * p(0) + (m2 - m1) * p(m1) + (n - m2) * p(m2)

    # ------------------------------------------------------------ counters
    def reset_counters(self):
        NT, NG = self.NT, self.NG
        self.c_want = [0.0] * NT; self.c_got = [0.0] * NT; self.c_ungold = [0.0] * NT; self.c_nogoods = [0.0] * NT; self.c_else = [0.0] * NT
        self.c_kw = [0.0, 0.0]; self.c_kg = [0.0, 0.0]; self.c_unc = [0.0, 0.0]
        self.c_prod = [[0.0] * NG for _ in (0, 1)]; self.c_cons = [[0.0] * NG for _ in (0, 1)]
        self.c_sink = collections.defaultdict(float); self.c_sg = collections.defaultdict(float); self.c_vg = collections.defaultdict(float)
        self.c_vin = [0.0] * self.NV; self.c_vnf = [0.0] * self.NV
        self.c_spm = [0.0, 0.0]; self.c_vm = [[0.0] * 4, [0.0] * 4]; self.c_vsp = collections.defaultdict(float)
        self.c_inflow = 0.0; self.c_spent = 0.0; self.c_other = 0.0; self.c_rev = 0.0
        self.c_dep = 0; self.c_other_town = 0; self.c_days = 0; self.c_wdist = 0.0
        self.c_trade = collections.defaultdict(float)

    # ------------------------------------------------------------ one day
    def step(self):
        P = self.P; NG = self.NG; NT = self.NT; day = self.day
        self.c_days += 1
        # 1. wagon arrivals
        keep = []
        for tr in self.trips:
            if tr[0] <= day + 1e-9:
                _, k, t, load, nfv = tr
                rev = nfv * P['nf_idx']
                for i, n in load:
                    if n <= 0: continue
                    rev += self.sell_value(t, i, n)
                    self.shelf[t][i] += n
                    self.news[t][i] -= n
                pin = rev * P['purse_share'] * self.rv[k]
                self.gold[k] += pin
                self.c_inflow += pin; self.c_rev += rev; self.c_vin[k] += pin; self.c_vnf[k] += nfv * P['nf_idx'] * P['purse_share'] * self.rv[k]
            else:
                keep.append(tr)
        self.trips = keep
        # 2. production
        for k, v in enumerate(self.Vl):
            o = self.out[k]; w = self.wh[k]; m = v['mk']; self.wh_nf[k] += self.nf[k]
            for i in range(NG):
                if o[i]: w[i] += o[i]; self.c_prod[m][i] += o[i]
        for t in range(NT):
            m = self.T[t]['mk']
            for i in range(NG):
                x = self.tf[t][i]
                if x: self.shelf[t][i] += x; self.c_prod[m][i] += x
        # 3. wagon departures
        for k, v in enumerate(self.Vl):
            if self.next_dep[k] > day + 1e-9: continue
            self.depart(k, v)
        # 4. ZC5 processing
        for t in range(NT): self.zc5(t)
        # 5. town folk
        for t in range(NT): self.town_folk(t)
        if P['price_order']: self.prices()   # game order: town daily tick = consumption then supply/demand update; villages, lords, caravans after
        # 6. villages
        for k, v in enumerate(self.Vl): self.village_buy(k, v)
        # 7. lords, courts, castles
        for t in range(NT):
            m = self.T[t]['mk']; sh = self.shelf[t]
            for i in range(NG):
                x = min(max(0.0, sh[i]), self.lords_buy[t][i] + self.court[t][i])
                if x > 0:
                    sh[i] -= x; self.c_cons[m][i] += x * (self.court[t][i] / (self.lords_buy[t][i] + self.court[t][i]))
                    self.c_sink['lords+court'] += x; self.c_sg[('lords+court', i)] += x
                d = self.lords_drop[t][i]
                if d: sh[i] += d
                self.c_cons[m][i] += self.party[t][i]
        for c, cs in enumerate(self.C):
            st = self.cstock[c]
            mkc = 0
            for i in range(NG):
                x = min(st[i], cs['dem'][i])
                if x > 0: st[i] -= x; self.c_sink['castle'] += x; self.c_sg[('castle', i)] += x; self.c_cons[cs.get('mk', 0)][i] += x
        # 8. trade module (caravans / food carts)
        if self.trade_mod: self.trade_mod.daily(self, day)
        # 9. other purse outflows (old-list salt first - it takes goods off the shelf)
        if P['old_salt'] > 0:
            isl = self.iSalt
            for k, v in enumerate(self.Vl):
                n = int(P['old_salt'] * v['bask'][isl] + self.rng.random())
                if n <= 0 or self.gold[k] <= 0: continue
                t = v['trade']
                have = int(self.shelf[t][isl]) - P['keep']
                if have <= 0: continue
                p = self.buy_price(t, isl)
                x = min(n, have, int(self.gold[k] // p))
                if x <= 0: continue
                self.shelf[t][isl] -= x; self.gold[k] -= x * p; self.c_other += x * p
                self.c_cons[v['mk']][isl] += x; self.c_sg[('vil_old', isl)] += x
        for k in range(self.NV):
            o = P['other_rate'] * max(0.0, self.gold[k] - self.floor_v[k]) + self.dues_v[k]
            o = min(o, self.gold[k])
            self.gold[k] -= o; self.c_other += o
        # 10. prices (unless taken right after the town's own consumption - price_order 1)
        if not P['price_order']: self.prices()
        self.day += 1

    def prices(self):
        for t in range(self.NT):
            sh = self.shelf[t]; Sv = self.Sv[t]; Dv = self.Dv[t]; ix = self.idx[t]
            for c in range(self.NC):
                gs = self.catg[c]
                V = 0.0; E = 0.0
                for i in gs:
                    V += max(0.0, sh[i]) * self.val[i]
                    E += self.dterm(t, i) * self.val[i]
                Sv[c] = max(0.1, 0.85 * Sv[c] + 0.15 * V)
                Dv[c] = 0.85 * Dv[c] + 0.15 * E
                ix[c] = self.index(Dv[c], Sv[c], V)

    def depart(self, k, v):
        P = self.P
        load = [(i, int(self.wh[k][i])) for i in range(self.NG) if self.wh[k][i] >= 1]
        for i, n in load: self.wh[k][i] -= n
        nfv = self.wh_nf[k]; self.wh_nf[k] = 0.0
        if not load and nfv <= 0:
            self.next_dep[k] = self.day + 1; return
        # castle first stop: sells the castle the food it lacks for castle_days
        if v['castle'] and v['castle_i'] >= 0:
            c = v['castle_i']; cs = self.C[c]; st = self.cstock[c]; nl = []
            for i, n in load:
                need = P['castle_days'] * cs['dem'][i] - st[i]
                x = int(min(n, max(0.0, need)))
                if x > 0:
                    st[i] += x
                    rev = x * self.val[i] * self.start_idx[v['mk']][i]
                    pin = rev * P['purse_share'] * self.rv[k]
                    self.gold[k] += pin; self.c_inflow += pin; self.c_rev += rev; self.c_vin[k] += pin
                    n -= x
                if n > 0: nl.append((i, n))
            load = nl
        own = v['bound'] if not v['castle'] else None
        best = None; bestsc = -1.0; bestd = 0.0
        rng = P['range']
        for (t, d) in v['_cand']:
            if d > rng and not (own is not None and self.T[t]['id'] == own): continue
            T = 2.0 * d / P['speed'] + P['wait']
            rate = min(1.0, P['store_days'] / T) if T > 0 else 1.0
            val = 0.0
            for i, n in load:
                val += self.sell_value(t, i, n, self.news[t][i] * self.val[i])
            sc = val * rate
            if sc > bestsc or (sc == bestsc and d < bestd): best = t; bestsc = sc; bestd = d
        if best is None:
            # no town in range: the castle (or own town) takes it at start prices (game: BK sends to castle)
            t = v['trade']; bestd = 0.0
            best = t
        days = bestd / P['speed']
        self.trips.append((self.day + max(0.5, days), k, best, load, nfv))
        for i, n in load: self.news[best][i] += n
        self.c_dep += 1; self.c_wdist += bestd
        if best != v['trade']: self.c_other_town += 1
        self.next_dep[k] = self.day + max(P['cycle'], 2 * days + P['wait'])

    def zc5(self, t):
        P = self.P; sh = self.shelf[t]; m = self.T[t]['mk']; lam = self.lam(m)
        hands = self.hands[t]
        opts = []
        for (o, i, r, lab) in self.pairs:
            dem_o = lam * (self.Q[t][o] + self.QV[t][o])
            if dem_o <= 0: continue
            target = max(0.0, P['zc5_out_days'] * dem_o - sh[o])
            direct = lam * (self.Q[t][i] + self.QV[t][i])
            avail = max(0.0, sh[i] - P['zc5_in_days'] * direct - P['keep'])
            mk = min(target, avail / r)
            if mk <= 0: continue
            po = self.val[o] * self.idx[t][self.gcat[o]]; pi = self.val[i] * self.idx[t][self.gcat[i]]
            prof = po - (1 + P['zc5_margin']) * r * pi
            if prof <= 0: continue
            opts.append((prof / lab, o, i, r, lab, mk))
        opts.sort(reverse=True)
        for (_, o, i, r, lab, mk) in opts:
            if hands <= 0: break
            x = min(mk, hands / lab)
            x = min(x, max(0.0, sh[i] - P['keep']) / r)
            if x <= 0: continue
            sh[i] -= x * r; sh[o] += x; hands -= x * lab
            self.c_cons[m][i] += x * r; self.c_prod[m][o] += x; self.c_sink['zc5_in_' + self.G[i]] += x * r; self.c_sg[('zc5', i)] += x * r

    def take(self, t, i, n):
        x = min(n, max(0.0, self.shelf[t][i]))
        if x > 0: self.shelf[t][i] -= x
        return x

    def town_folk(self, t):
        m = self.T[t]['mk']; Q = self.Q[t]; sh = self.shelf[t]
        g0 = 0.0
        af = self.T[t].get('afford', 1.0) if self.P['folk_afford'] else 1.0   # W/R/P purses: measured share of the food basket they pay (kiesa-ludu)
        for i in range(self.NG):
            n = Q[i] * af
            if n <= 0: continue
            x = self.take(t, i, n)
            self.c_cons[m][i] += x; self.c_sink['town'] += x; self.c_sg[('town', i)] += x
            if i in (self.iBread, self.iPie) and x < n: g0 += n - x
            elif i == self.iBeer and x < n: g0 += 0.4 * (n - x)
        if g0 > 0:
            x = self.take(t, self.iGrain, g0)
            self.c_cons[m][self.iGrain] += x; self.c_sink['town_sub_grain'] += x; self.c_sg[('town', self.iGrain)] += x

    def village_buy(self, k, v):
        P = self.P; m = v['mk']; lam = self.lam(m)
        t = v['trade']; keep = P['keep']; NG = self.NG
        cr = self.carry[k]; b = v['bask']
        n = [0] * NG
        for i in range(NG):
            w = b[i] * lam + cr[i]
            kk = int(w); cr[i] = min(1.0, max(0.0, w - kk)); n[i] = kk
        st = {'want': 0.0, 'got': 0.0, 'ungold': 0.0, 'nogoods': 0.0, 'else': 0.0, 'kw': 0.0, 'kg': 0.0, 'broke': False}
        miss = [0] * NG
        gold = self.gold[k]
        shelf = self.shelf[t]
        kc = self.kcal

        def take(tt, i, nn, count=True):
            nonlocal gold
            if nn <= 0: return 0
            if count:
                st['want'] += nn; st['kw'] += nn * kc[i]; self.c_vg[(i, 'want')] += nn
            if st['broke']:
                if count: st['ungold'] += nn; self.c_vg[(i, 'ungold')] += nn
                return 0
            sh = self.shelf[tt]
            have = int(sh[i]) - keep
            if have <= 0:
                if count: st['nogoods'] += nn; miss[i] += nn; self.c_vg[(i, 'nogoods')] += nn
                return 0
            if count and have < nn: st['nogoods'] += nn - have; miss[i] += nn - have; self.c_vg[(i, 'nogoods')] += nn - have
            kk = min(nn, have)
            p = self.buy_price(tt, i)
            if kk * p > gold:
                if count: st['ungold'] += kk - int(gold // p); self.c_vg[(i, 'ungold')] += kk - int(gold // p)
                kk = int(gold // p)
                if kk <= 0: st['broke'] = True; return 0
            gold -= kk * p; sh[i] -= kk; self.c_vsp[(m, i)] += kk * p
            st['got'] += kk; st['kg'] += kk * kc[i]; self.c_vg[(i, 'got')] += kk
            self.c_cons[m][i] += kk; self.c_sink['vil'] += kk; self.c_sg[('vil', i)] += kk
            return kk

        take(t, self.iGrain, n[self.iGrain]); take(t, self.iSalt, n[self.iSalt])
        # drinks cheapest per litre (missing: not available)
        def unit(i, per):
            have = int(shelf[i]) - keep
            if have <= 0: return 1e18
            return self.buy_price(t, i) / per
        dr = sorted(self.drinks, key=lambda i: unit(i, 1.0))
        un = {}
        for i in dr: un[i] = n[i] - take(t, i, n[i])
        if un.get(self.iWine, 0) > 0:
            bw = take(t, self.iBeer, un[self.iWine], False)
            c = min(bw, miss[self.iWine]); miss[self.iWine] -= c; st['nogoods'] = max(0.0, st['nogoods'] - c)
            un[self.iBeer] = un.get(self.iBeer, 0) + un[self.iWine] - bw
        if un.get(self.iBeer, 0) > 0:
            g = int(un[self.iBeer] * 0.4)
            if g > 0:
                gb = take(t, self.iGrain, g, False)
                brew = int(gb / 0.4 + 1e-9)
                c = min(brew, miss[self.iBeer]); miss[self.iBeer] -= c; st['nogoods'] = max(0.0, st['nogoods'] - c); brew -= c
                if brew > 0:
                    c = min(brew, miss[self.iWine]); miss[self.iWine] -= c; st['nogoods'] = max(0.0, st['nogoods'] - c)
                st['kg'] += 0.0
        pr = sorted(self.protein, key=lambda i: unit(i, self.prot_kcal[i] / 1000.0))
        for i in pr: take(t, i, n[i])
        for i in self.rest: take(t, i, n[i])
        # F2: missing goods in up to 2 near towns
        if not st['broke'] and any(miss):
            for alt in v['_near']:
                if st['broke']: break
                if not any(miss): break
                for i in [self.iGrain, self.iSalt] + self.drinks + self.protein + self.rest:
                    if miss[i] <= 0: continue
                    got = take(alt, i, miss[i], False)
                    if got > 0: miss[i] -= got; st['else'] += got
                    if st['broke']: break
        spent = self.gold[k] - gold
        self.gold[k] = gold
        self.c_spent += spent; self.c_spm[m] += spent
        self.c_vm[m][0] += st['want']; self.c_vm[m][1] += st['got']; self.c_vm[m][2] += st['ungold']; self.c_vm[m][3] += max(0.0, st['nogoods'] - st['else'])
        self.c_want[t] += st['want']; self.c_got[t] += st['got']; self.c_ungold[t] += st['ungold']
        self.c_nogoods[t] += max(0.0, st['nogoods'] - st['else']); self.c_else[t] += st['else']
        self.c_kw[m] += st['kw']; self.c_kg[m] += st['kg']
        if st['kw'] > 0:
            bfr = min(1.0, st['kg'] / st['kw'])
            self.c_unc[m] += st['kw'] * max(0.0, 1.0 - bfr - self.home_v[k])

    # ------------------------------------------------------------ snapshot / report
    def snapshot(self):
        """per-town numbers in the ks-miasta format (7-day counters since the last snapshot)"""
        rows = []
        for t in range(self.NT):
            d = self.dterm(t, self.iGrain)
            sh = self.shelf[t][self.iGrain]
            rows.append({'idx': self.idx[t][self.gcat[self.iGrain]], 'shelf': sh, 'dem': d, 'days': sh / d if d > 0 else float('nan'),
                         'want': self.c_want[t], 'got': self.c_got[t], 'ungold': self.c_ungold[t], 'nogoods': self.c_nogoods[t], 'else': self.c_else[t],
                         'purse': sum(self.gold[k] for k in range(self.NV) if self.trade_town[k] == t)})
        return rows

    def summary(self, rows=None):
        rows = rows or self.snapshot()
        s = {}
        days = [r['days'] for r in rows if not math.isnan(r['days'])]
        s['shelf_q10'], s['shelf_med'], s['shelf_q90'], s['shelf_max'] = q(days, .1), q(days, .5), q(days, .9), max(days)
        ix = [r['idx'] for r in rows]
        s['idx_q10'], s['idx_med'], s['idx_q90'] = q(ix, .1), q(ix, .5), q(ix, .9)
        s['ceil'] = sum(1 for x in ix if x >= 9.9) / len(ix); s['floor'] = sum(1 for x in ix if x <= 0.11) / len(ix)
        s['short'] = sum(1 for x in days if x < 0.5) / len(days); s['glut'] = sum(1 for x in days if x > 30) / len(days)
        W = sum(r['want'] for r in rows)
        s['v_got'] = sum(r['got'] for r in rows) / W if W else 0
        s['v_ungold'] = sum(r['ungold'] for r in rows) / W if W else 0
        s['v_nogoods'] = sum(r['nogoods'] for r in rows) / W if W else 0
        s['v_else'] = sum(r['else'] for r in rows) / W if W else 0
        for m in (0, 1):
            p = self.c_prod[m][self.iGrain]; c = self.c_cons[m][self.iGrain]
            s['grain_SC_%d' % m] = p / c if c > 0 else float('nan')
            s['unc_%d' % m] = self.c_unc[m] / self.c_kw[m] if self.c_kw[m] else 0
        s['purses'] = sum(self.gold)
        nd = max(1, self.c_days)
        s['inflow_d'] = self.c_inflow / nd; s['spent_d'] = self.c_spent / nd; s['other_d'] = self.c_other / nd
        s['dep_d'] = self.c_dep / nd; s['other_town'] = self.c_other_town / max(1, self.c_dep); s['wdist'] = self.c_wdist / max(1, self.c_dep)
        s['grain_stock'] = [sum(self.shelf[t][self.iGrain] for t in range(self.NT) if self.T[t]['mk'] == m) for m in (0, 1)]
        return s


# ---------------------------------------------------------------- trade modules
class Caravans:
    """BK caravans as measured: per good, buy the measured pieces/d at the cheapest towns (index below the market median) and sell the measured
    pieces/d at the dearest towns of the same market (no distance, no money - capacity proxy only)."""
    def __init__(self, sim): pass

    def daily(self, sim, day):
        for i in range(sim.NG):
            b = sim.car_buy[i]; s = sim.car_sell[i]
            if b <= 0 and s <= 0: continue
            c = sim.gcat[i]
            for m in (0, 1):
                ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
                w = sum(sim.one_share[t] for t in ts) / sum(sim.one_share)
                ts.sort(key=lambda t: sim.idx[t][c])
                need = b * w
                for t in ts[:max(1, len(ts) // 3)]:
                    if need <= 0: break
                    x = min(need, max(0.0, sim.shelf[t][i] - sim.P['keep'] - 7 * sim.dterm(t, i)))
                    if x > 0: sim.shelf[t][i] -= x; sim.car_inv[m][i] += x; need -= x; sim.c_trade['car_buy'] += x
                give = min(s * w, sim.car_inv[m][i])
                for t in reversed(ts[-max(1, len(ts) // 3):]):
                    if give <= 0: break
                    x = min(give, max(0.0, 3 * sim.dterm(t, i) - sim.shelf[t][i]))
                    if x > 0: sim.shelf[t][i] += x; sim.car_inv[m][i] -= x; give -= x; sim.c_trade['car_sell'] += x


class FoodCarts(Caravans):
    """Example fix module (merchant food export by cart, like package 215 for arms): every day each town with more than `src_days` of D-term on
    its shelf offers the surplus; towns below `dst_days` buy it from the cheapest reachable source (land road, or land + sea) whose price plus
    transport cost (5% per 100 units) is below the local price; delivery after road/speed days; perishables (fish, fruit, Egg, butter, grape)
    only within `perish_days`. Money: buyer town -> seller town (town coffers, not modelled). Caravans as measured still run."""
    PERISH = {'fish', 'Egg', 'butter', 'apple', 'carrot', 'orange', 'grape', 'meat', 'cheese'}

    def __init__(self, sim, src_days=10.0, dst_days=3.0, max_days=30.0, perish_days=7.0, share=0.15, cost100=0.05, sea=True):
        self.src_days, self.dst_days, self.max_days, self.perish_days, self.share, self.cost100, self.sea = src_days, dst_days, max_days, perish_days, share, cost100, sea
        self.ship = []
        self.D = sim.W['dsea'] if sea else sim.W['dland']

    def daily(self, sim, day):
        Caravans.daily(self, sim, day)
        keep = []
        for (a, t, i, n) in self.ship:
            if a <= day: sim.shelf[t][i] += n; sim.c_trade['carts_arrived'] += n
            else: keep.append((a, t, i, n))
        self.ship = keep
        spd = sim.P['speed']
        for i in range(sim.NG):
            if sim.G[i] in ('bread', 'pie', 'beer'): continue
            lim = self.perish_days if sim.G[i] in self.PERISH else self.max_days
            c = sim.gcat[i]
            for m in (0, 1):
                ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
                src = []
                for t in ts:
                    d = sim.dterm(t, i)
                    sur = sim.shelf[t][i] - self.src_days * d - sim.P['keep']
                    if sur > 1: src.append([t, sur * self.share])
                if not src: continue
                dst = []
                for t in ts:
                    d = sim.dterm(t, i)
                    lack = self.dst_days * d - sim.shelf[t][i]
                    if lack > 1: dst.append((sim.idx[t][c], t, lack))
                dst.sort(reverse=True)
                for (pi, t, lack) in dst:
                    best = None
                    for s in src:
                        if s[1] <= 0: continue
                        dd = self.D[s[0]][t]
                        if dd < 0: continue
                        days = dd / spd
                        if days > lim: continue
                        cost = sim.idx[s[0]][c] * (1 + self.cost100 * dd / 100.0)
                        if cost >= pi: continue
                        if best is None or cost < best[0]: best = (cost, s, days)
                    if best is None: continue
                    _, s, days = best
                    x = min(lack, s[1])
                    s[1] -= x; sim.shelf[s[0]][i] -= x
                    self.ship.append((day + max(1.0, days), t, i, x))
                    sim.c_trade['carts_sent'] += x; sim.c_trade['carts_dist'] += x * days


def make_trade(name, sim):
    if name in (None, 'none'): return None
    if name == 'caravans': return Caravans(sim)
    if name.startswith('carts'):
        kw = {}
        for part in name.split(':')[1:]:
            k, v = part.split('='); kw[k] = (v == '1') if k == 'sea' else float(v)
        return FoodCarts(sim, **kw)
    raise ValueError('unknown trade module ' + name)


# ---------------------------------------------------------------- comparison with the observed run
def observed(W, day):
    o = W['obs'].get(str(day)); keys = W['obs_keys']
    if o is None: return None
    K = {k: i for i, k in enumerate(keys)}
    rows = []
    for t, r in enumerate(o):
        d = r[K['zboze_popyt_zaplacony_szt_d']]; sh = r[K['zboze_polka_szt']]
        rows.append({'idx': r[K['zboze_indeks']], 'shelf': sh, 'dem': d, 'days': sh / d if d > 0 else float('nan'),
                     'want': r[K['wsie_chciane_szt_7d']], 'got': r[K['wsie_kupione_szt_7d']], 'ungold': r[K['wsie_bez_kiesy_szt_7d']],
                     'nogoods': r[K['wsie_bez_towaru_szt_7d']], 'else': r[K['wsie_w_innym_miescie_szt_7d']], 'purse': r[K['kiesy_wsi_zl']]})
    return rows


def obs_summary(W, day):
    rows = observed(W, day)
    s = {}
    days = [r['days'] for r in rows if not math.isnan(r['days'])]
    s['shelf_q10'], s['shelf_med'], s['shelf_q90'], s['shelf_max'] = q(days, .1), q(days, .5), q(days, .9), max(days)
    ix = [r['idx'] for r in rows]
    s['idx_q10'], s['idx_med'], s['idx_q90'] = q(ix, .1), q(ix, .5), q(ix, .9)
    s['ceil'] = sum(1 for x in ix if x >= 9.9) / len(ix); s['floor'] = sum(1 for x in ix if x <= 0.11) / len(ix)
    s['short'] = sum(1 for x in days if x < 0.5) / len(days); s['glut'] = sum(1 for x in days if x > 30) / len(days)
    Wt = sum(r['want'] for r in rows)
    s['v_got'] = sum(r['got'] for r in rows) / Wt; s['v_ungold'] = sum(r['ungold'] for r in rows) / Wt
    s['v_nogoods'] = sum(r['nogoods'] for r in rows) / Wt; s['v_else'] = sum(r['else'] for r in rows) / Wt
    s['purses'] = sum(r['purse'] for r in rows)
    ry = W['rynki'].get(str(day))
    if ry:
        for m in (0, 1):
            g = ry[str(m)].get('grain')
            if g: s['grain_SC_%d' % m] = g[0] / g[1]
        s['grain_stock'] = [ry[str(m)]['grain'][2] for m in (0, 1)]
    return s


def town_fit(sim_rows, obs_rows):
    """town-level agreement: correlation of log shelf days and of log grain index, and share of towns in the same class (short <1 d / mid / glut >30 d)"""
    def lg(x): return math.log(max(0.05, x))
    a = []; b = []; ia = []; ib = []; same = 0; n = 0
    def cls(x): return 0 if x < 1 else (2 if x > 30 else 1)
    for r, o in zip(sim_rows, obs_rows):
        if math.isnan(r['days']) or math.isnan(o['days']): continue
        a.append(lg(r['days'])); b.append(lg(o['days'])); ia.append(math.log(r['idx'])); ib.append(math.log(max(0.1, o['idx'])))
        same += cls(r['days']) == cls(o['days']); n += 1
    def corr(x, y):
        mx = sum(x) / len(x); my = sum(y) / len(y)
        sx = math.sqrt(sum((u - mx) ** 2 for u in x)); sy = math.sqrt(sum((v - my) ** 2 for v in y))
        return sum((u - mx) * (v - my) for u, v in zip(x, y)) / (sx * sy) if sx > 0 and sy > 0 else float('nan')
    rmse = math.sqrt(sum((u - v) ** 2 for u, v in zip(ia, ib)) / len(ia))
    return {'corr_logdays': corr(a, b), 'corr_logidx': corr(ia, ib), 'rmse_logidx': rmse, 'same_class': same / n}


def fmt_s(s):
    keys = ['shelf_q10', 'shelf_med', 'shelf_q90', 'shelf_max', 'idx_q10', 'idx_med', 'idx_q90', 'ceil', 'floor', 'v_got', 'v_ungold', 'v_nogoods', 'v_else',
            'grain_SC_0', 'grain_SC_1', 'purses']
    out = []
    for k in keys:
        if k in s:
            x = s[k]
            out.append('%s %s' % (k, ('%.0f' % x) if k == 'purses' else ('%.2f' % x)))
    return ' | '.join(out)


def run(argv):
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument('--world', default=os.path.join(HERE, 'world.json'))
    ap.add_argument('--days', type=int, default=70)
    ap.add_argument('--start', type=int, default=109123)
    ap.add_argument('--init', default='obs')
    ap.add_argument('--trade', default='caravans')
    ap.add_argument('--plan', default='current')
    ap.add_argument('--seed', type=int, default=1)
    ap.add_argument('--set', action='append', default=[])
    ap.add_argument('--quiet', action='store_true')
    ap.add_argument('--csv', default=None)
    a = ap.parse_args(argv)
    W = json.load(open(a.world, encoding='utf-8'))
    P = {}
    for kv in a.set:
        k, v = kv.split('='); P[k] = float(v)
    t0 = time.time()
    sim = Sim(W, P, trade=a.trade, plan=a.plan, init=a.init, start=a.start, seed=a.seed)
    last = None
    out_rows = []
    for dd in range(a.days):
        sim.step()
        day = sim.day
        if (day - W['kdays'][0]) % 7 == 0:
            rows = sim.snapshot(); s = sim.summary(rows)
            o = observed(W, day)
            line = 'day %d sim: %s' % (day, fmt_s(s))
            if o:
                os_ = obs_summary(W, day); fit = town_fit(rows, o)
                line += '\n          obs: %s\n          fit: %s' % (fmt_s(os_), ' '.join('%s %.2f' % kv for kv in fit.items()))
            if not a.quiet: print(line)
            for t, r in enumerate(rows):
                out_rows.append([day, W['towns'][t]['id'], W['towns'][t]['name'], W['towns'][t]['mk']] + [r[k] for k in ('idx', 'shelf', 'dem', 'days', 'want', 'got', 'ungold', 'nogoods', 'else', 'purse')])
            last = (s, rows)
            sim.reset_counters()
    if a.csv:
        with open(a.csv, 'w', encoding='utf-8', newline='\n') as f:
            f.write('dzien;miasto_id;miasto;rynek;zboze_indeks;zboze_polka;zboze_popyt;dni_polki;wsie_chciane;wsie_kupione;bez_kiesy;bez_towaru;w_innym;kiesy\n')
            for r in out_rows: f.write(';'.join(('%.3f' % x) if isinstance(x, float) else str(x) for x in r) + '\n')
    if not a.quiet:
        print('time %.1f s for %d days' % (time.time() - t0, a.days))
    return sim, last


if __name__ == '__main__':
    run(sys.argv[1:])
