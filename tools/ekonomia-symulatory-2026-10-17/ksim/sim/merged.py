# KS round 3 - MERGED design ("KS-R3": kupcy zywnosci + gradient kraina + samopomoc wsi + szczelna kiesa + D-term z prawdziwego odbioru)
# offline acceptance on the town simulator (sim.py). Python 3 stdlib, run with python -I. Reads world.json / vfit.json only.
# python -I merged.py [--seeds 2] [--days ...] [--init start|obs] [--no-base] [--tag NAME] [opt=value ...]
#   options (default = merged design):
#     carts=1        food merchants by cart (rules G1 of the spec)
#     tau=0.01 taus=0.005 margin=0.03 ksrc=1.25 offer=0.25 maxd=30 maxp=7 slot=2   cart parameters
#     onefix=1       town price D-term One = net lords' purchases + courts at the town (castle 1:1 out of the town D-term)
#     leak=1         KS villages: no old-list surplus buying (other_rate x leakk, old_salt 0)
#     leakk=0.61     share of the other purse outflows left (war levy, BK village market, dues) - research 2: 38k of 97.5k gross is the old list
#     ownfirst=1     village eats its own warehouse food when it bought less than its Plan-paid kcal share
#     planunc=0.03/0.035  Plan uncovered kcal share after step 8b (home substitute), W/E (C# prototype CatchK ml1: 3.0/3.5%)
#     alpha=1.0      Plan-paid shares x alpha with the same pieces taken off the villages' market output (alpha_net emulation)
#     land=0.0       elasticity of the land gradient (0 = hands as the round-3 Plan; 0.5 = partial shift toward land demand, cap 2)
#     freight=1      freight paid to the source villages' purses (carters)
import json, sys, os, math, statistics, time, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import sim as S

RAW = ['grain', 'fish', 'meat', 'cheese', 'butter', 'Egg', 'apple', 'carrot', 'orange', 'grape', 'olives', 'date_fruit', 'salt', 'honey']
CARRY = RAW
PERISH = {'fish', 'meat', 'Egg', 'butter', 'apple', 'carrot', 'orange', 'grape', 'date_fruit'}   # KsCore.Perish (WhaleMeat merged into meat)

DEF = dict(carts=1, tau=0.01, taus=0.005, margin=0.03, ksrc=1.25, offer=0.25, maxd=30.0, maxp=7.0, slot=2, onefix=1, leak=1, leakk=0.61,
           ownfirst=1, nsrc=3, maxlead=30.0, paidfirst=1, resd=7.0, reach=1, kmax=2.5, resg=7.0, alphanet=1, ctrl=1, useoff=1, pipe=1.3, zc5in=6, zc5out=4, war=1, ctrl_d=2, landctrl=1, planunc_w=0.03, planunc_e=0.035, alpha=1.0, land=0.0, landcap=2.0, freight=1, dstk=1.0)


def fnv(s):
    h = 2166136261
    for c in s: h = ((h ^ ord(c)) * 16777619) & 0xffffffff
    return h


class Kupcy(S.Caravans):
    """G1: food merchants by cart. Per good and market: sources hold more than ksrc x T* days of their paid D-term and offer `offer` of the surplus per
    day; a destination below T* (shelf + incoming) buys from the source with the lowest landed cost I_s x (1 + tau x days) whose landed cost x (1 + margin)
    is not above its own index; it fills to (T* + days) x D. Days = road / 41 (land road; islands land+sea at the same cart speed, tau_sea on such pairs).
    Perishables only within maxp days. Each destination is served on its slot day (every `slot` days) with slot days of offer. BK caravans as measured."""
    def __init__(self, sim, o):
        self.o = o
        self.ship = []          # (arrive, src, dst, i, n)
        self.inc = [[0.0] * sim.NG for _ in range(sim.NT)]
        self.dl = sim.W['dland']; self.ds = sim.W['dsea']
        NT = sim.NT
        self.days = [[-1.0] * NT for _ in range(NT)]; self.sea = [[False] * NT for _ in range(NT)]
        for a in range(NT):
            for b in range(NT):
                if a == b or sim.T[a]['mk'] != sim.T[b]['mk']: continue
                ka, kb = sim.T[a]['kingdom'], sim.T[b]['kingdom']
                if o.get('war', 1) and ka != kb and (ka in S.WAR_K or kb in S.WAR_K): continue   # no merchant trade with a kingdom at war
                dl = self.dl[a][b]; ds = self.ds[a][b]
                if dl >= 0 and (ds < 0 or dl <= 1.15 * ds): self.days[a][b] = max(1.0, math.ceil(dl / sim.P['speed']))
                elif ds >= 0: self.days[a][b] = max(1.0, math.ceil(ds / sim.P['speed'])); self.sea[a][b] = True
        self.slot = [fnv(t['id']) % max(1, int(o['slot'])) for t in sim.T]
        self.tsi = [[1.0] * sim.NG for _ in range(NT)]
        for t in range(NT):
            for i in range(sim.NG): self.tsi[t][i] = sim.start_idx[sim.T[t]['mk']][i]
        self.ci = [sim.GI[g] for g in CARRY]
        self.kx = [[1.0] * sim.NG for _ in range(NT)]   # stockout controller: order demand = D-term x kx (empty stall yesterday -> +10%, else -2%, 1..kmax)

    def daily(self, sim, day):
        S.Caravans.daily(self, sim, day)
        o = self.o
        keep = []
        for (a, s, t, i, n) in self.ship:
            if a <= day:
                sim.shelf[t][i] += n; self.inc[t][i] -= n; sim.c_trade['k_arrived'] += n
            else: keep.append((a, s, t, i, n))
        self.ship = keep
        sl = max(1, int(o['slot']))
        if o.get('kmax', 2.5) > 1.0:
            for t in range(sim.NT):
                for i in self.ci:
                    if sim.shelf[t][i] <= sim.P['keep'] + 1 and sim.dterm(t, i) > 0: self.kx[t][i] = min(o.get("kmax", 2.5), self.kx[t][i] * o.get("kup", 1.10))
                    else: self.kx[t][i] = max(1.0, self.kx[t][i] * 0.98)
        for i in self.ci:
            g = sim.G[i]; c = sim.gcat[i]
            lim = o['maxp'] if g in PERISH else o['maxd']
            for m in (0, 1):
                ts = [t for t in range(sim.NT) if sim.T[t]['mk'] == m]
                offer = {}
                for t in ts:
                    d = max(0.0, sim.dterm(t, i))
                    T = 7.14 * self.tsi[t][i] ** (-1 / 0.6)
                    sur = sim.shelf[t][i] - o['ksrc'] * T * d - sim.P['keep']
                    if sur > 1: offer[t] = sur * o['offer'] * sl
                if not offer: continue
                dst = []
                for t in ts:
                    if self.slot[t] != day % sl: continue
                    d = (max(sim.dterm(t, i), sim.offema[t][i]) if o.get('useoff') else sim.dterm(t, i)) * self.kx[t][i]
                    if d <= 0: continue
                    T = 7.14 * self.tsi[t][i] ** (-1 / 0.6) * o['dstk']
                    H = sim.shelf[t][i] + self.inc[t][i]
                    if H < (T + o['maxlead']) * d: dst.append((sim.idx[t][c], t, d, T, H))
                dst.sort(reverse=True)
                for (Id, t, d, T, H) in dst:
                    cands = []
                    for s, off in offer.items():
                        if off <= 0 or s == t: continue
                        L = self.days[s][t]
                        if L < 0 or L > lim: continue
                        tau = o['taus'] if self.sea[s][t] else o['tau']
                        cost = sim.idx[s][c] * (1 + tau * L)
                        if cost * (1 + o['margin']) > Id: continue
                        cands.append((cost, s, L, tau))
                    if not cands: continue
                    cands.sort()
                    Hc = H
                    for (cost, s, L, tau) in cands[:int(o.get('nsrc', 3))]:
                        need = (T + L + sl) * d - Hc          # reorder point and order-up-to level include the lead time of THIS source
                        if need < 1: break
                        x = math.floor(min(need, offer[s]))
                        if x < 1: continue
                        Hc += x
                        offer[s] -= x; sim.shelf[s][i] -= x; self.inc[t][i] += x
                        self.ship.append((day + L, s, t, i, x))
                        sim.c_trade['k_sent'] += x; sim.c_trade['k_piece_days'] += x * L; sim.c_trade['k_lots'] += 1; sim.c_trade['g_' + g] += x; sim.c_trade['sea_pcs'] += x if self.sea[s][t] else 0
                        val = x * sim.val[i] * sim.idx[s][c]
                        sim.c_trade['k_gold'] += val
                        fr = val * tau * L
                        sim.c_trade['k_freight'] += fr
                        if o['freight'] and fr > 0:
                            vs = sim._vil_of[s]
                            if vs:
                                pp = sum(sim.Vl[k]['people'] for k in vs) or 1.0
                                for k in vs:
                                    g_ = fr * sim.Vl[k]['people'] / pp
                                    sim.gold[k] += g_; sim.c_inflow += g_
        sim.c_trade['k_transit'] += sum(n for (_, _, _, _, n) in self.ship)


class MSim(S.Sim):
    def __init__(self, W, P, o, init='start', start=109118, seed=1):
        self.o = o
        P = dict(P)
        if o['leak']:
            P['other_rate'] = S.DEFAULTS['other_rate'] * o['leakk']; P['old_salt'] = 0.0
        if o.get('zc5in', 0) > 0: P['zc5_in_days'] = o['zc5in']
        if o.get('zc5out', 0) > 0: P['zc5_out_days'] = o['zc5out']
        S.Sim.__init__(self, W, P, trade='caravans', plan='current', init='__defer__' if False else init, start=start, seed=seed)

    # plan-side emulations happen after the base constructor via setup()
    def setup(self):
        o = self.o; NG = self.NG; NT = self.NT
        self._vil_of = [[k for k, v in enumerate(self.Vl) if v['trade'] == t] for t in range(NT)]
        # alpha_net emulation: paid x alpha, the same pieces off the market output (hands to home work)
        if o['alpha'] != 1.0:
            for m in (0, 1):
                vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
                for i in range(NG):
                    if self.G[i] not in RAW: continue
                    cut = sum((1 - o['alpha']) * self.Vl[k]['bask'][i] * self.Vl[k]['paid'][i] for k in vs)
                    tot = sum(self.out[k][i] for k in vs)
                    f = max(0.0, 1 - cut / tot) if tot > 0 else 1.0
                    for k in vs: self.out[k][i] *= f
            for v in self.Vl: v['paid'] = [p * o['alpha'] for p in v['paid']]
        # land gradient emulation: to-market output of each land scaled toward its land demand (elasticity land), market totals kept
        if o['land'] > 0:
            tl = []
            for t in range(NT):
                c = collections.Counter()
                for k in self._vil_of[t]: c[self.Vl[k]['land']] += self.Vl[k]['people']
                tl.append(c.most_common(1)[0][0] if c else None)
            self._tland = tl
            for m in (0, 1):
                vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
                for i in range(NG):
                    if self.G[i] not in RAW: continue
                    Sl = collections.defaultdict(float); Dl = collections.defaultdict(float)
                    for k in vs: Sl[self.Vl[k]['land']] += self.out[k][i]
                    for t in range(NT):
                        if self.T[t]['mk'] != m or tl[t] is None: continue
                        Dl[tl[t]] += self.Q[t][i] + sum(self.Vl[k]['bask'][i] * self.Vl[k]['paid'][i] for k in self._vil_of[t]) + self.one[t][i]
                    tot = sum(Sl.values())
                    if tot <= 0: continue
                    totD = sum(Dl.values()) or 1.0
                    f = {}
                    for L, s in Sl.items():
                        if s <= 0: continue
                        r = (Dl.get(L, 0.0) / totD) / (s / tot)
                        f[L] = min(o['landcap'], max(1 / o['landcap'], r ** o['land'])) if r > 0 else 1 / o['landcap']
                    new = sum(Sl[L] * f.get(L, 1.0) for L in Sl)
                    k0 = tot / new if new > 0 else 1.0
                    for k in vs: self.out[k][i] *= f.get(self.Vl[k]['land'], 1.0) * k0
        # alpha_net per village (Plan step 8 on the measured net purse inflow): paid share x a_v, a_v = 0.95 x net inflow / Plan budget at start prices;
        # the market output of each good changes by the change of the paid village demand (home work absorbs / releases the hands)
        if o.get('_vin') is not None:
            vin = o['_vin']; osh = o['_osh']
            oldp = [list(v['paid']) for v in self.Vl]
            for k, v in enumerate(self.Vl):
                B = self.budget_v[k]
                if B <= 0: continue
                a = max(0.0, min(3.0, 0.95 * vin[k] * (1 - osh) / B))
                v['paid'] = [min(1.0, p * a) for p in v['paid']]
            for m in (0, 1):
                vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
                for i in range(NG):
                    if self.G[i] not in RAW: continue
                    dR = sum(self.Vl[k]['bask'][i] * (oldp[k][i] - self.Vl[k]['paid'][i]) for k in vs)
                    tot = sum(self.out[k][i] for k in vs)
                    f = max(0.0, 1 - dR / tot) if tot > 0 else 1.0
                    for k in vs: self.out[k][i] *= f
            for k, v in enumerate(self.Vl):
                m = v['mk']
                self.budget_v[k] = sum(v['bask'][i] * v['paid'][i] * self.val[i] * self.start_idx[m][i] for i in range(NG))
        # perishable reach rule (Plan step 5): a town (and its trade villages) with no producing catchment of a perishable good of its own and no
        # surplus catchment within maxp cart days gets paid demand 0 for it; the Plan's market demand falls by the same pieces (fewer hands on it)
        if o.get('reach'):
            dl = self.W['dland']; ds = self.W['dsea']; sp = self.P['speed']
            for i in range(NG):
                g = self.G[i]
                if g not in PERISH: continue
                sup = [self.tf[t][i] + sum(self.out[k][i] for k in self._vil_of[t]) for t in range(NT)]
                dem = [self.Q[t][i] + sum(self.Vl[k]['bask'][i] * self.Vl[k]['paid'][i] for k in self._vil_of[t]) for t in range(NT)]
                cut = [0.0, 0.0]
                for t in range(NT):
                    if sup[t] > 0.05 * dem[t] or dem[t] <= 0: continue
                    ok = False
                    for u in range(NT):
                        if u == t or self.T[u]['mk'] != self.T[t]['mk'] or sup[u] <= dem[u]: continue
                        d_ = dl[u][t] if dl[u][t] >= 0 else ds[u][t]
                        if d_ >= 0 and d_ / sp <= o['maxp']: ok = True; break
                    if ok: continue
                    m = self.T[t]['mk']
                    cut[m] += dem[t]
                    self.Q[t][i] = 0.0
                    for k in self._vil_of[t]: self.Vl[k]['paid'][i] = 0.0
                    self.c_trade['reach_cut_' + g] = self.c_trade.get('reach_cut_' + g, 0.0)
                for m in (0, 1):
                    if cut[m] <= 0: continue
                    vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
                    tot = sum(self.out[k][i] for k in vs)
                    f = max(0.0, 1 - cut[m] / tot) if tot > 0 else 1.0
                    for k in vs: self.out[k][i] *= f
                    self._reach_log = getattr(self, '_reach_log', []) + [(g, m, round(cut[m]), round(tot))]
            for k, v in enumerate(self.Vl):
                mm = v['mk']
                self.budget_v[k] = sum(v['bask'][i] * v['paid'][i] * self.val[i] * self.start_idx[mm][i] for i in range(NG))
        # recompute QV / Der after alpha (D-term and ZC5 'what the town buys')
        self.QV = [[0.0] * NG for _ in range(NT)]
        for vi, v in enumerate(self.Vl):
            t = v['trade']
            for i in range(NG): self.QV[t][i] += v['bask'][i] * v['paid'][i]
        self.der = [[0.0] * NG for _ in range(NT)]
        for t in range(NT):
            for (oo, ii, r, lab) in self.pairs: self.der[t][ii] += (self.Q[t][oo] + self.QV[t][oo]) * r
        # D-term One from the real offtake at the town
        if o['onefix']:
            for t in range(NT):
                for i in range(NG):
                    self.one[t][i] = max(0.0, self.lords_buy[t][i] - self.lords_drop[t][i]) + self.court[t][i]
        # Plan home share after step 8b (substitute): home = 1 - paid kcal share - Plan uncovered
        kc = self.kcal
        for k, v in enumerate(self.Vl):
            w = sum(v['bask'][i] * kc[i] for i in range(NG))
            p = sum(v['bask'][i] * v['paid'][i] * kc[i] for i in range(NG))
            pk = p / w if w > 0 else 0.0
            v['_pk'] = pk
            u = o['planunc_w'] if v['mk'] == 0 else o['planunc_e']
            self.home_v[k] = max(0.0, 1.0 - pk - u)
        self._off = [[0.0] * NG for _ in range(NT)]
        self.c_unmet = [[0.0] * NG for _ in (0, 1)]
        self.offema = [[self.dterm(t, i) for i in range(NG)] for t in range(NT)]
        if o['carts']: self.trade_mod = Kupcy(self, o)
        self.c_kr = [0.0, 0.0]

    def village_buy2(self, k, v):
        """G3 'paid first': pass 1 buys the Plan-paid quantities along the ladder (grain, salt, drinks, protein, rest); pass 2 buys the rest of the
        full basket only from the purse above a reserve of `resd` days of the paid basket at start prices."""
        P = self.P; m = v['mk']; lam = self.lam(m); t = v['trade']; keep = P['keep']; NG = self.NG
        cr = self.carry[k]; b = v['bask']; pd = v['paid']
        if not hasattr(self, 'carry2'): self.carry2 = [[0.0] * NG for _ in range(self.NV)]
        c2 = self.carry2[k]
        n1 = [0] * NG; n2 = [0] * NG
        for i in range(NG):
            w = b[i] * pd[i] * lam + cr[i]; kk = int(w); cr[i] = min(1.0, max(0.0, w - kk)); n1[i] = kk
            w2 = b[i] * (1 - pd[i]) * lam + c2[i]; k2 = int(w2); c2[i] = min(1.0, max(0.0, w2 - k2)); n2[i] = k2
        st = {'want': 0.0, 'got': 0.0, 'ungold': 0.0, 'nogoods': 0.0, 'else': 0.0, 'kw': 0.0, 'kg': 0.0}
        gold = self.gold[k]; kc = self.kcal
        reserve = self.o['resd'] * self.budget_v[k]
        def take(tt, i, nn, lim, extra=False):
            nonlocal gold
            if nn <= 0: return 0, 0
            sh = self.shelf[tt]; have = int(sh[i]) - keep
            if extra and self.o.get('p2shelf', 1):
                # beyond the Plan-paid part: only from the stall's stock above its target (T* days of the paid D-term)
                have = int(sh[i] - 7.14 * self.start_idx[self.T[tt]['mk']][i] ** (-1 / 0.6) * self.dterm(tt, i)) - keep
            if have <= 0: return 0, nn
            kk = min(nn, have); p = self.buy_price(tt, i)
            afford = int(max(0.0, gold - lim) // p)
            x = min(kk, afford)
            if x > 0:
                self._off[tt][i] += x
                gold -= x * p; sh[i] -= x; self.c_vsp[(m, i)] += x * p
                self.c_cons[m][i] += x; self.c_sink['vil'] += x; self.c_sg[('vil', i)] += x
            return x, nn - kk
        order = [self.iGrain, self.iSalt] + sorted(self.drinks, key=lambda i: self.buy_price(t, i)) + sorted(self.protein, key=lambda i: self.buy_price(t, i) / (self.prot_kcal[i] / 1000.0)) + self.rest
        miss = [0] * NG
        staple = (b[self.iGrain] * pd[self.iGrain] * self.buy_price(t, self.iGrain) + b[self.iSalt] * pd[self.iSalt] * self.buy_price(t, self.iSalt)) * lam
        resg = self.o.get('resg', 7.0) * staple
        for (nn, lim0) in ((n1, 0.0), (n2, reserve)):
            for i in order:
                q = nn[i]
                if q <= 0: continue
                lim = lim0 if i in (self.iGrain, self.iSalt) else max(lim0, resg)
                st['want'] += q; st['kw'] += q * kc[i]; self.c_vg[(i, 'want')] += q
                got, ng = take(t, i, q, lim, lim0 > 0)
                if ng > 0 and v['_near']:
                    for alt in v['_near']:
                        g2, ng2 = take(alt, i, ng, lim, lim0 > 0)
                        if g2 > 0: got += g2; st['else'] += g2; ng = ng2 if ng2 < ng else ng - g2
                        if ng <= 0: break
                ung = max(0, q - got - max(0, ng))
                if lim0 == 0.0 and ng > 0: self.c_unmet[m][i] += min(ng, q - got)
                st['got'] += got; st['kg'] += got * kc[i]; st['nogoods'] += max(0, min(ng, q - got)); st['ungold'] += ung
                self.c_vg[(i, 'got')] += got; self.c_vg[(i, 'ungold')] += ung; self.c_vg[(i, 'nogoods')] += max(0, min(ng, q - got))
        spent = self.gold[k] - gold; self.gold[k] = gold
        self.c_spent += spent; self.c_spm[m] += spent
        self.c_vm[m][0] += st['want']; self.c_vm[m][1] += st['got']; self.c_vm[m][2] += st['ungold']; self.c_vm[m][3] += st['nogoods']
        self.c_want[t] += st['want']; self.c_got[t] += st['got']; self.c_ungold[t] += st['ungold']; self.c_nogoods[t] += st['nogoods']; self.c_else[t] += st['else']
        self.c_kw[m] += st['kw']; self.c_kg[m] += st['kg']
        if st['kw'] > 0:
            bfr = min(1.0, st['kg'] / st['kw'])
            self.c_unc[m] += st['kw'] * max(0.0, 1.0 - bfr - self.home_v[k])

    def take(self, t, i, n):
        x = S.Sim.take(self, t, i, n)
        self._off[t][i] += x
        if n > x: self.c_unmet[self.T[t]['mk']][i] += n - x
        return x

    def zc5(self, t):
        before = list(self.shelf[t])
        S.Sim.zc5(self, t)
        for i in range(self.NG):
            d = before[i] - self.shelf[t][i]
            if d > 0: self._off[t][i] += d

    def village_buy(self, k, v):
        m = v['mk']
        kw0, kg0, un0 = self.c_kw[m], self.c_kg[m], self.c_unc[m]
        if self.o.get('paidfirst'): self.village_buy2(k, v)
        else: S.Sim.village_buy(self, k, v)
        kw = self.c_kw[m] - kw0; kg = self.c_kg[m] - kg0
        if kw <= 0 or not self.o['ownfirst']: return
        short = kw * v['_pk'] - kg
        kr = 0.0
        if short > 0:
            wh = self.wh[k]
            order = [self.iGrain] + [self.GI[g] for g in ('fish', 'meat', 'cheese', 'butter', 'Egg', 'date_fruit', 'olives', 'grape', 'apple', 'carrot', 'orange', 'honey')]
            for i in order:
                if short <= 0: break
                if wh[i] < 1 or self.kcal[i] <= 0: continue
                n = min(math.floor(wh[i]), math.ceil(short / self.kcal[i]))
                if n <= 0: continue
                wh[i] -= n; kr += n * self.kcal[i]; short -= n * self.kcal[i]
                self.c_trade['own_first_pcs'] += n
                self.c_cons[m][i] += n   # eaten: leaves the market supply (to-market output that never reaches a shelf)
                self.c_prod[m][i] += 0.0
        if kr > 0:
            self.c_kr[m] += kr
            old = kw * max(0.0, 1.0 - min(1.0, kg / kw) - self.home_v[k])
            new = kw * max(0.0, 1.0 - min(1.0, (kg + kr) / kw) - self.home_v[k])
            self.c_unc[m] += new - old

    def reset_counters(self):
        S.Sim.reset_counters(self)
        self.c_kr = [0.0, 0.0]

    def step(self):
        o = self.o
        if o.get('ctrl') and not hasattr(self, '_cw'):
            self._cw = None
        if o.get('ctrl'):
            if self._cw is None:
                self._cw = ([[x for x in r] for r in self.c_prod], [[x for x in r] for r in self.c_cons], self.day, [[x for x in r] for r in self.c_unmet])
            elif self.day - self._cw[2] >= 7 and self.day >= o.get('ctrl_from', 109132):
                p0, c0, d0, u0 = self._cw; nd = self.day - d0
                for m in (0, 1):
                    vs = [k for k, v in enumerate(self.Vl) if v['mk'] == m]
                    for i in range(self.NG):
                        if self.G[i] not in RAW: continue
                        P_ = (self.c_prod[m][i] - p0[m][i]) / nd; C_ = (self.c_cons[m][i] - c0[m][i]) / nd
                        if P_ <= 0 or C_ <= 0: continue
                        # stock target: start shelves (7.14 x I^-5/3 days of D-term) over all towns of the market
                        tgt = sum(7.14 * self.start_idx[m][i] ** (-1 / 0.6) * self.dterm(t, i) for t in range(self.NT) if self.T[t]['mk'] == m)
                        stock = sum(self.shelf[t][i] for t in range(self.NT) if self.T[t]['mk'] == m)   # shelves only: goods in transit are on top of the shelf target (pipeline)
                        if o.get('ctrl_d', 1) == 2:
                            C_ = C_ + (self.c_unmet[m][i] - u0[m][i]) / nd     # demand at the shelf = consumption + paid demand not served (no goods)
                        elif o.get('ctrl_d', 1) == 1:
                            # demand at the shelf, not censored consumption: max(consumption, paid D-term of the towns + castle demand)
                            Dm = sum(self.dterm(t, i) for t in range(self.NT) if self.T[t]['mk'] == m) + sum(cs['dem'][i] for cs in self.C if cs.get('mk', 0) == m)
                            C_ = max(C_, Dm)
                        want = C_ * (1 + max(-0.05, min(0.05, (tgt * o.get('pipe', 1.0) - stock) / (28 * C_))))
                        f = max(1 - o.get('ctrl_step', 0.10), min(1 + o.get('ctrl_up', 0.05), want / P_))
                        # only the villages' part of the production moves (town fields fixed)
                        vp = sum(self.out[k][i] for k in vs)
                        if vp <= 0: continue
                        tf = P_ - vp
                        fv = max(0.0, (P_ * f - tf) / vp)
                        fv = max(1 - o.get('ctrl_step', 0.10), min(1 + o.get('ctrl_up', 0.05), fv))
                        for k in vs: self.out[k][i] *= fv
                # per-land (kraina) rule: a land whose shelves of g hold more than 2 x its target and keep growing (no outlet - war, no buyer) moves
                # 10%/week of its villages' market output of g to home work; a land below 0.5 x target with unmet demand gets +5%/week
                if o.get('landctrl'):
                    if not hasattr(self, '_tl'):
                        tl = []
                        for t in range(self.NT):
                            c = collections.Counter()
                            for k in self._vil_of[t]: c[self.Vl[k]['land']] += self.Vl[k]['people']
                            tl.append(c.most_common(1)[0][0] if c else None)
                        self._tl = tl; self._lst = {}
                    for i in range(self.NG):
                        if self.G[i] not in RAW: continue
                        st = collections.defaultdict(float); tg = collections.defaultdict(float)
                        for t in range(self.NT):
                            L = self._tl[t]
                            if L is None: continue
                            st[L] += self.shelf[t][i]; tg[L] += 7.14 * self.start_idx[self.T[t]['mk']][i] ** (-1 / 0.6) * self.dterm(t, i)
                        for L in st:
                            prev = self._lst.get((L, i))
                            self._lst[(L, i)] = st[L]
                            if prev is None: continue
                            f = 1.0
                            if st[L] > 2 * tg[L] + 50 and st[L] > prev: f = 0.9
                            if f != 1.0:
                                for k, v in enumerate(self.Vl):
                                    if v['land'] == L: self.out[k][i] *= f
                self._cw = ([[x for x in r] for r in self.c_prod], [[x for x in r] for r in self.c_cons], self.day, [[x for x in r] for r in self.c_unmet])
        S.Sim.step(self)
        a = 1.0 / 7.0
        for t in range(self.NT):
            r = self._off[t]; e = self.offema[t]
            for i in range(self.NG):
                e[i] += a * (r[i] + self.lords_buy[t][i] + self.court[t][i] - e[i]); r[i] = 0.0


def prerun_vin(W, o, init, seed, w0, w1, start):
    o2 = dict(o); o2['alphanet'] = 0; o2['ctrl'] = 0; o2['_vin'] = None
    sim = MSim(W, {}, o2, init=init, start=start, seed=seed); sim.setup()
    while sim.day < w0: sim.step()
    sim.reset_counters()
    while sim.day < w1: sim.step()
    nd = max(1, sim.c_days)
    return [x / nd for x in sim.c_vin], (sim.c_other / nd) / max(1.0, sim.c_inflow / nd)


def scenario(W, o, init, seed, w0, w1, start):
    if o.get('alphanet') and o.get('_vin') is None:
        o = dict(o); o['_vin'], o['_osh'] = prerun_vin(W, o, init, seed, w0, w1, start)
    sim = MSim(W, {}, o, init=init, start=start, seed=seed)
    sim.setup()
    # re-init shelves for the new D-term (init start = start shelves on the corrected D-term)
    if init == 'start':
        # start shelves on the corrected D-term (7.14 x start index^-5/3 days), price state at steady value; trips/warehouses untouched
        for t in range(sim.NT):
            m = sim.T[t]['mk']
            for i in range(sim.NG):
                sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
            for c in range(sim.NC):
                V = sum(sim.shelf[t][i] * sim.val[i] for i in sim.catg[c]); E = sum(sim.dterm(t, i) * sim.val[i] for i in sim.catg[c])
                sim.Sv[t][c] = max(0.1, V); sim.Dv[t][c] = E; sim.idx[t][c] = sim.index(E, max(0.1, V), V)
    NT, NG = sim.NT, sim.NG
    idx_acc = [[0.0] * sim.NC for _ in range(NT)]; nidx = 0
    st0 = None
    sh_hist = []
    while sim.day < w1:
        if sim.day == w0:
            sim.reset_counters()
            st0 = [[sum(sim.shelf[t][i] for t in range(NT) if sim.T[t]['mk'] == m) for i in range(NG)] for m in (0, 1)]
        sim.step()
        if w0 < sim.day <= w1:
            nidx += 1
            for t in range(NT):
                for c in range(sim.NC): idx_acc[t][c] += sim.idx[t][c]
    nd = max(1, sim.c_days)
    r = {}
    sc = {}; ds = {}
    st1 = [[sum(sim.shelf[t][i] for t in range(NT) if sim.T[t]['mk'] == m) for i in range(NG)] for m in (0, 1)]
    for m in (0, 1):
        for i, g in enumerate(sim.G):
            p = sim.c_prod[m][i] / nd; c = sim.c_cons[m][i] / nd
            if g in RAW and c >= 20:
                # own-first eats count as consumption but the output never reached the market: count them on both sides out of the market
                sc[(m, g)] = p / c
                ds[(m, g)] = (st1[m][i] - st0[m][i]) / nd / c
    r['sc'] = sc; r['ds'] = ds
    pr = {}; cf = {}; tw = {}
    for m in (0, 1):
        ts = [t for t in range(NT) if sim.T[t]['mk'] == m]
        for i, g in enumerate(sim.G):
            if g not in RAW: continue
            c = sim.gcat[i]
            av = [idx_acc[t][c] / max(1, nidx) for t in ts]
            pr[(m, g)] = (statistics.median(av), sim.start_idx[m][i])
            cf[(m, g)] = (sum(1 for t in ts if sim.idx[t][c] >= 9.99) / len(ts), sum(1 for t in ts if sim.idx[t][c] <= 0.101) / len(ts))
            # demand-weighted town band (window-average index within start +-0.3) and D in towns with shelf < 0.5 T*
            Dw = 0.0; Din = 0.0; Dlow = 0.0
            for t, a in zip(ts, av):
                d = sim.dterm(t, i)
                if d <= 0: continue
                T = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6)
                Dw += d
                if abs(a - sim.start_idx[m][i]) <= 0.3: Din += d
                if sim.shelf[t][i] < 0.5 * T * d: Dlow += d
            tw[(m, g)] = (Din / Dw if Dw else float('nan'), Dlow / Dw if Dw else float('nan'))
    r['pr'] = pr; r['cf'] = cf; r['tw'] = tw
    rows = sim.snapshot()
    days = [x['days'] for x in rows if not math.isnan(x['days'])]
    r['shelf'] = (S.q(days, .1), S.q(days, .5), S.q(days, .9), max(days))
    r['short'] = sum(1 for x in days if x < 0.5) / len(days); r['glut'] = sum(1 for x in days if x > 30) / len(days)
    r['lt1'] = sum(1 for x in days if x < 1.0); r['lt2'] = sum(1 for x in days if x < 2.0) / len(days)
    gi = [x['idx'] for x in rows]
    r['idx_med'] = S.q(gi, .5); r['ceil'] = sum(1 for x in gi if x >= 9.9) / len(gi)
    W_ = sum(x['want'] for x in rows) or 1
    r['v'] = (sum(x['got'] for x in rows) / W_, sum(x['ungold'] for x in rows) / W_, sum(x['nogoods'] for x in rows) / W_)
    r['unc'] = (sim.c_unc[0] / max(1, sim.c_kw[0]), sim.c_unc[1] / max(1, sim.c_kw[1]))
    r['kb'] = (sim.c_kg[0] / max(1, sim.c_kw[0]), sim.c_kg[1] / max(1, sim.c_kw[1]))
    r['kr'] = (sim.c_kr[0] / max(1, sim.c_kw[0]), sim.c_kr[1] / max(1, sim.c_kw[1]))
    pkw = [0.0, 0.0]; ww = [0.0, 0.0]
    for v in sim.Vl:
        w = sum(v['bask'][i] * sim.kcal[i] for i in range(NG)); pkw[v['mk']] += w * v.get('_pk', 0.0); ww[v['mk']] += w
    r['pk'] = (pkw[0] / ww[0], pkw[1] / ww[1])
    r['trade'] = {k: v / nd for k, v in sim.c_trade.items()}
    r['purses'] = sum(sim.gold); r['inflow'] = sim.c_inflow / nd; r['spent'] = sim.c_spent / nd; r['other'] = sim.c_other / nd
    r['rev'] = sim.c_rev / nd
    return r


def avg(rs):
    out = {}
    for k in rs[0]:
        x = rs[0][k]
        if isinstance(x, dict):
            out[k] = {}
            for kk in x:
                vals = [r[k].get(kk) for r in rs]
                if isinstance(vals[0], tuple): out[k][kk] = tuple(sum(v[j] for v in vals) / len(vals) for j in range(len(vals[0])))
                else: out[k][kk] = sum(vals) / len(vals)
        elif isinstance(x, tuple): out[k] = tuple(sum(r[k][j] for r in rs) / len(rs) for j in range(len(x)))
        else: out[k] = sum(r[k] for r in rs) / len(rs)
    return out


def report(name, o):
    mk = {0: 'W', 1: 'E'}
    core = ('grain', 'fish', 'meat', 'cheese', 'butter', 'olives', 'salt', 'grape', 'date_fruit', 'honey', 'Egg', 'apple', 'carrot', 'orange')
    bad5 = ['%s %s %.2f' % (mk[m], g, x) for (m, g), x in sorted(o['sc'].items()) if not (0.95 <= x <= 1.05)]
    bad6 = ['%s %s %+.1f%%' % (mk[m], g, 100 * x) for (m, g), x in sorted(o['ds'].items()) if abs(x) > 0.02]
    bad7 = ['%s %s %.2f' % (mk[m], g, x[0]) for (m, g), x in sorted(o['pr'].items()) if abs(x[0] - x[1]) > 0.3]
    bad7b = ['%s %s s%.0f p%.0f' % (mk[m], g, 100 * x[0], 100 * x[1]) for (m, g), x in sorted(o['cf'].items()) if x[0] > 0.2 or x[1] > 0.2]
    print('=== %s' % name)
    print('  NKS-5  S/C outside 0.95-1.05: %d of %d  %s' % (len(bad5), len(o['sc']), ', '.join(bad5)))
    print('  NKS-6  stock change > 2%%/d: %d of %d  %s' % (len(bad6), len(o['ds']), ', '.join(bad6)))
    print('  NKS-7  market median idx outside start+-0.3: %d of %d  %s' % (len(bad7), len(o['pr']), ', '.join(bad7)))
    print('  NKS-7b ceil/floor > 20%% towns: %d of %d  %s' % (len(bad7b), len(o['cf']), ', '.join(bad7b)))
    tw = o['tw']
    print('  town band (D-weighted window idx in start+-0.3) / D in towns shelf<0.5T*: ' + ', '.join('%s %s %.0f/%.0f' % (mk[m], g, 100 * a, 100 * b) for (m, g), (a, b) in sorted(tw.items()) if g in core))
    print('  grain towns: shelf q10/med/q90/max %.1f/%.1f/%.1f/%.0f | <0.5d %.0f%% | <1d %.1f towns | <2d %.0f%% | >30d %.0f%% | idx med %.2f ceil %.0f%%' % (
        o['shelf'] + (100 * o['short'], o['lt1'], 100 * o['lt2'], 100 * o['glut'], o['idx_med'], 100 * o['ceil'])))
    print('  villages: bought %.0f%% / no money %.0f%% / no goods %.0f%%; kcal bought W %.0f%% E %.0f%%, own-first W %.1f%% E %.1f%%, UNCOVERED W %.1f%% E %.1f%% (Plan-paid kcal W %.0f%% E %.0f%%)' % (
        100 * o['v'][0], 100 * o['v'][1], 100 * o['v'][2], 100 * o['kb'][0], 100 * o['kb'][1], 100 * o['kr'][0], 100 * o['kr'][1], 100 * o['unc'][0], 100 * o['unc'][1], 100 * o['pk'][0], 100 * o['pk'][1]))
    print('  money: purses end %.2f M, inflow %.0f, spent %.0f, other %.0f zl/d, wagon revenue %.0f zl/d' % (o['purses'] / 1e6, o['inflow'], o['spent'], o['other'], o['rev']))
    tr = o['trade']
    if tr.get('k_sent'):
        print('  merchants: sent %.0f pcs/d in %.0f lots (mean %.1f days), in transit %.0f, buy value %.0f zl/d, freight %.0f zl/d; caravans buy %.0f sell %.0f; own-first %.0f pcs/d' % (
            tr['k_sent'], tr.get('k_lots', 0), tr.get('k_piece_days', 0) / max(1, tr['k_sent']), tr.get('k_transit', 0), tr.get('k_gold', 0), tr.get('k_freight', 0),
            tr.get('car_buy', 0), tr.get('car_sell', 0), tr.get('own_first_pcs', 0)))
    else:
        print('  caravans buy %.0f sell %.0f; own-first %.0f pcs/d' % (tr.get('car_buy', 0), tr.get('car_sell', 0), tr.get('own_first_pcs', 0)))


def main(argv):
    W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
    o = dict(DEF); seeds = 2; init = 'start'; base = True; tag = 'MERGED'
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == '--seeds': seeds = int(argv[i + 1]); i += 2; continue
        if a == '--init': init = argv[i + 1]; i += 2; continue
        if a == '--no-base': base = False; i += 1; continue
        if a == '--tag': tag = argv[i + 1]; i += 2; continue
        k, v = a.split('='); o[k] = float(v); i += 1
    o.setdefault('_vin', None)
    start, w0, w1 = (109118, 109140, 109167) if init == 'start' else (109123, 109140, 109167)
    t0 = time.time()
    if base:
        ob = dict(DEF); ob.update(carts=0, onefix=0, leak=0, ownfirst=0, planunc_w=-1, planunc_e=-1, alphanet=0, ctrl=0, useoff=0, paidfirst=0, zc5in=0, zc5out=0, _vin=None)
        # base: Plan home share as round 3 (home_kcal_w/e fixed) -> emulate with planunc so that home = sim default
        rb = []
        for sd in range(1, seeds + 1):
            sim_o = dict(ob)
            rb.append(base_scn(W, sim_o, init, sd, w0, w1, start))
        report('BASE round-3 rules (sim)', avg(rb))
    rf = [scenario(W, o, init, sd, w0, w1, start) for sd in range(1, seeds + 1)]
    report('%s %s' % (tag, ' '.join('%s=%g' % (k, v) for k, v in o.items() if not k.startswith('_') and DEF.get(k) != v)), avg(rf))
    print('time %.1f s' % (time.time() - t0))


def base_scn(W, o, init, seed, w0, w1, start):
    # round-3 rules: no carts, no fixes, home share fixed (sim defaults)
    class B(MSim):
        def setup(self):
            self._vil_of = [[k for k, v in enumerate(self.Vl) if v['trade'] == t] for t in range(self.NT)]
            for v in self.Vl: v['_pk'] = 0.0
            self.c_kr = [0.0, 0.0]
            self._off = [[0.0] * self.NG for _ in range(self.NT)]
            self.offema = [[0.0] * self.NG for _ in range(self.NT)]
            self.c_unmet = [[0.0] * self.NG for _ in (0, 1)]
    global MSim_
    orig = MSim
    try:
        globals()['MSim'] = B
        return scenario(W, o, init, seed, w0, w1, start)
    finally:
        globals()['MSim'] = orig


if __name__ == '__main__':
    main(sys.argv[1:])
