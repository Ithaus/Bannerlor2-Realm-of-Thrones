import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/ksim/sim/r4port.py'
pairs = []
pairs.append(("""import json, sys, os, math, statistics, time, collections, copy, csv, random""",
"""# ROUND 4 (17.10 evening, C# after 86bc5756 - review of round 3, 41 issues): ship speed (sea days = road / (4 x cart speed) + 1 port day; ship=1), freight tau land
#   0.025 / sea 0.0025 per day (tau, taus), kx only on a real stockout (shelf + in transit < (T* + L_min) x Dz; frozen while in transit >= L_min x Dz; kx4=1), source keep
#   from Dz without kx, coffer: reserve as floor - on a short stall of grain and salt at most down to 0.5 x reserve and at most +5% of the reserve a day (res4=1),
#   merchants' profit check (landed unit cost <= 0.97 x destination unit price, up to 3 halvings; profit=1), spoilage of perishables 1.5%/day of haul (spoil=1),
#   pass 2 by price (stall price <= 0.8 x start price, shelf above 0.8^(-1/0.6) = 1.45 x target; p2k=1.45, p2price=1), controller without pass 2 (ctrl_p2=0) and only
#   until D+49 (ctrl_end; then the factor goes back to 1 over 2 Plans), H2 only while the stall still has no stock (h2arr=1), town coffers clamped at 0 (gold0=1).
#   Plan dump of round 4: kmoney/h4/out/m1.csv (C# core with step 5 after step 8 on paid demand, one coffer budget per land, physical substitute 8b/8c, land-priced
#   baskets, step 8d inside the step-8 fixed point incl. livestock meat, ledger 1:1 KS_ITEMS1).
import json, sys, os, math, statistics, time, collections, copy, csv, random"""))
pairs.append(("""DEF = dict(carts=1, paidfirst=1, ownfirst=1, leak=1, leakk=0.61, zc5=1, realoff=1, ctrl=1, every2=1, h2=1, glut=1, beta=1, price_blend=1, dist_k_carts=1.18,
           tau=0.010, taus=0.005, margin=0.03, srckeep=1.25, offer=0.5, kxup=1.10, kxdown=0.98, kxmax=2.5, coffer=0.15, maxsrc=3, ncand=12, maxd=30.0, maxp=7.0,
           D=109118, p2flow=0, p2save=0.001, p2k=1.5, off_p2=0, ctrl_p2=1, ctrl_from=14, plan_first=109109, money_cap=1, fix1=1, res_short=0.0, ctrl_step=0.07, ctrl_gap=0.05, remeasure=1, an_flow=0, ctrl_u=1, candx=1, candx_n=4, kwfix=1, qvcal=3)""",
"""DEF = dict(carts=1, paidfirst=1, ownfirst=1, leak=1, leakk=0.61, zc5=1, realoff=1, ctrl=1, every2=1, h2=1, glut=1, beta=1, price_blend=1, dist_k_carts=1.18,
           tau=0.025, taus=0.0025, margin=0.03, srckeep=1.25, offer=0.5, kxup=1.10, kxdown=0.98, kxmax=2.5, coffer=0.15, maxsrc=3, ncand=12, maxd=30.0, maxp=7.0,
           D=109118, p2flow=0, p2save=0.001, p2k=1.45, off_p2=0, ctrl_p2=0, ctrl_from=14, plan_first=109109, money_cap=1, fix1=1, res_short=0.0, ctrl_step=0.07, ctrl_gap=0.05, remeasure=1, an_flow=0, ctrl_u=1, candx=1, candx_n=4, kwfix=1, qvcal=3,
           ship=1, kx4=1, res4=1, profit=1, spoil=1, p2price=1, ctrl_end=49, h2arr=1, gold0=1)
# round 3 as committed (86bc5756): tau=0.010 taus=0.005 p2k=1.5 ctrl_p2=1 ship=0 kx4=0 res4=0 profit=0 spoil=0 p2price=0 ctrl_end=999 h2arr=0 gold0=0
R3_RULES = dict(tau=0.010, taus=0.005, p2k=1.5, ctrl_p2=1, ship=0, kx4=0, res4=0, profit=0, spoil=0, p2price=0, ctrl_end=999, h2arr=0, gold0=0)"""))
# ship days
pairs.append(("""                if road < 0: continue
                self.days[a][b] = max(1.0, math.ceil(road * dk / sp)); self.sea[a][b] = sea""",
"""                if road < 0: continue
                if sea and o.get('ship', 0): self.days[a][b] = max(1.0, math.ceil(road * dk / (sp * 4.0))) + 1.0     # round 4: ship 4 x cart speed + 1 port day
                else: self.days[a][b] = max(1.0, math.ceil(road * dk / sp))
                self.sea[a][b] = sea"""))
# arrivals: spoilage
pairs.append(("""            due, s, t, k, n = cart
            if day < due: keep.append(cart); continue
            i = self.items[k]
            sim.shelf[t][i] += n; self.inc[t][k] = max(0, self.inc[t][k] - n)""",
"""            due, s, t, k, n = cart[:5]
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
            sim.shelf[t][i] += n"""))
pairs.append(("""    def daily(self, day):
        sim = self.sim; o = sim.o; NT = sim.NT; NK = len(RAW14)
        self.c['d_bought'] = 0; self.c['d_arr'] = self.c.get('d_arr', 0)
        sh = [[int(max(0.0, sim.shelf[t][i])) for i in self.items] for t in range(NT)]
        ix = [[sim.idx[t][sim.gcat[i]] for i in self.items] for t in range(NT)]
        dz = [[0.0] * NK for _ in range(NT)]
        for t in range(NT):
            for k, i in enumerate(self.items):
                p = sim.dp[t][i]; off = sim.off7[t][i]
                kxv = self.kx[t][k]
                if p > 0 and sh[t][k] <= 6: kxv = min(o['kxmax'], kxv * o['kxup'])
                else: kxv = max(1.0, kxv * o['kxdown'])
                self.kx[t][k] = kxv
                dz[t][k] = max(p, off) * kxv
        budget = [o['coffer'] * max(0.0, sim.town_gold(t) - sim.town_reserve(t)) if o['money_cap'] else 1e18 for t in range(NT)]
        budget_short = [o['coffer'] * max(0.0, sim.town_gold(t) - o['res_short'] * sim.town_reserve(t)) if o['money_cap'] else 1e18 for t in range(NT)]
        spent = [0.0] * NT
        Ts = [[tstar(sim.start_idx[m][i]) for i in self.items] for m in (0, 1)]
        offer = [[0.0] * NK for _ in range(NT)]
        for t in range(NT):
            m = sim.T[t]['mk']
            for k in range(NK):
                sur = sh[t][k] - o['srckeep'] * Ts[m][k] * dz[t][k] - 5
                if sur > 1: offer[t][k] = math.floor(o['offer'] * sur)""",
"""    def _dest_unit(self, t, i):
        sim = self.sim
        up = sim.val[i] * sim.idx[t][sim.gcat[i]]
        if sim.shelf[t][i] <= 5: up = min(up, 1.5 * sim.start_idx[sim.T[t]['mk']][i] * sim.val[i])   # C# ZPrice cap of an empty stall
        return max(1, math.ceil(up - 1e-9))

    def daily(self, day):
        sim = self.sim; o = sim.o; NT = sim.NT; NK = len(RAW14)
        self.c['d_bought'] = 0; self.c['d_arr'] = self.c.get('d_arr', 0)
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
        short_seen = set()"""))
pairs.append(("""                short_ = o['res_short'] < 1.0 and Hc < T_ * d      # round-2 fix (C# KsFoodCarts): short stall - the lot may use the whole coffer (15%/d), not only above the reserve""",
"""                short_ = (o['res_short'] < 1.0 or o.get('res4', 0)) and Hc < T_ * d      # round-2 fix (C# KsFoodCarts): short stall - the lot may use the reserve
                if o.get('res4', 0) and RAW14[k] not in ('grain', 'salt'): short_ = False   # round 4: only grain and salt
                if Hc < T_ * d and t not in short_seen:
                    short_seen.add(t); rv = sim.town_reserve(t)
                    if rv > 0: self.cof_short.append(sim.town_gold(t) / rv)
                dunit = self._dest_unit(t, i) if o.get('profit', 0) else 0"""))
pairs.append(("""                    cost = self.lot_price(s, i, n); fr = round(cost * tau * L)
                    avail = (max(budget[t], budget_short[t]) if short_ else budget[t]) - spent[t]""",
"""                    cost = self.lot_price(s, i, n); fr = round(cost * tau * L)
                    if dunit > 0:
                        # round 4: merchants' profit - landed cost <= 0.97 x destination unit price, else smaller lots (cheaper pieces of the source stall), up to 3 halvings
                        tries = 0
                        while n > 0 and cost + fr > math.floor(n * dunit * (1.0 - o['margin'])) and tries < 3:
                            n //= 2; tries += 1
                            if n < max(5.0, 0.5 * d): n = 0; break
                            cost = self.lot_price(s, i, n); fr = round(cost * tau * L)
                        if n <= 0 or cost + fr > math.floor(n * dunit * (1.0 - o['margin'])): self.c['noprofit'] += 1; continue
                    avail = (max(budget[t], budget_short[t]) if short_ else budget[t]) - spent[t]"""))
pairs.append(("""                    self.carts.append([day + int(max(1.0, L)), s, t, k, n])""",
"""                    self.carts.append([day + int(max(1.0, L)), s, t, k, n, day, cost + fr])"""))
pairs.append(("""    def flush(self, day):
        now = self.transit()
        rest = self.prev_transit + self.c['d_bought'] - self.c['d_arr'] - now""",
"""    def flush(self, day):
        now = self.transit()
        rest = self.prev_transit + self.c['d_bought'] - self.c['d_arr'] - self.c['d_spoil'] - now
        self.c['d_spoil'] = 0"""))
pairs.append(("""        self.c = collections.defaultdict(float)
        self.dbg = {} if o.get('dbg', 0) else None""",
"""        self.c = collections.defaultdict(float)
        self.dbg = {} if o.get('dbg', 0) else None
        self.mar_pct = []; self.cof_short = []"""))
# town gold clamp
pairs.append(("""    def town_gold(self, t):
        d = min(self.day, self._gmaxday)
        return self._gold.get((t, d), self._gold.get((t, d - 1), 0.0)) + self.town_gold_delta[t]""",
"""    def town_gold(self, t):
        d = min(self.day, self._gmaxday)
        g = self._gold.get((t, d), self._gold.get((t, d - 1), 0.0)) + self.town_gold_delta[t]
        return max(0.0, g) if self.o.get('gold0', 0) else g      # round 4: coffers never below 0 (the game refuses - KsFoodCarts.Ship)"""))
# H2 arrivals + pass 2 by price
pairs.append(("""        absent = self.o['h2'] and self.o['every2'] and self.row0[tt][i]
        have = (int(sh[i]) - 5) if not absent else 0""",
"""        absent = self.o['h2'] and self.o['every2'] and self.row0[tt][i] and (not self.o.get('h2arr', 0) or int(sh[i]) <= 5)   # round 4: H2 only while still no stock
        have = (int(sh[i]) - 5) if not absent else 0"""))
pairs.append(("""            if have <= 0: b['p2shelf'] += n; return 0
            above = int(math.floor(have + 5 - self.o['p2k'] * self.Ts[self.T[tt]['mk']][i] * self.dp[tt][i]))""",
"""            if have <= 0: b['p2shelf'] += n; return 0
            if self.o.get('p2price', 0) and price > 0.8 * self.start_idx[self.T[tt]['mk']][i] * self.val[i]: b['p2shelf'] += n; return 0   # round 4: pass 2 only while cheap
            above = int(math.floor(have + 5 - self.o['p2k'] * self.Ts[self.T[tt]['mk']][i] * self.dp[tt][i]))"""))
pairs.append(("""            if have <= 0 or (self.o['h2'] and self.o['every2'] and self.row0[t][i]): return 1e18""",
"""            if have <= 0 or (self.o['h2'] and self.o['every2'] and self.row0[t][i] and (not self.o.get('h2arr', 0) or int(self.shelf[t][i]) <= 5)): return 1e18"""))
# controller window
pairs.append(("""        if not o['ctrl'] or day < o['D'] + o['ctrl_from']: return
        if o['fix1'] and self.cN < 5: return                 # fix1: at least 5 full-lambda days in the averages before the first factor change""",
"""        if not o['ctrl'] or day < o['D'] + o['ctrl_from']: return
        if day > o['D'] + o.get('ctrl_end', 999):
            # round 4: after the start window the factor goes back to 1 over 2 Plans (then rules and prices move the hands)
            for m in (0, 1):
                for g in RAW14:
                    i = self.GI[g]; k0 = self.mf[m][i]; kk = 1.0 + (k0 - 1.0) * 0.5
                    self.mf[m][i] = 1.0 if abs(kk - 1.0) < 0.01 else kk
            return
        if o['fix1'] and self.cN < 5: return                 # fix1: at least 5 full-lambda days in the averages before the first factor change"""))
rep(P, pairs)
