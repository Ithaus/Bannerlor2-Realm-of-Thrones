import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/ksim/sim/r4port.py'
pairs = []
pairs.append(("""    def daily(self, day):
        sim = self.sim; o = sim.o; NT = sim.NT; NK = len(RAW14)
        self.c['d_bought'] = 0; self.c['d_arr'] = self.c.get('d_arr', 0)""",
"""    def _dump_state(self, day):
        \"\"\"round 4 port test (kmoney/port4, C# KsFoodCore): state of the day before the KS-H decisions, towns in C# order (StringId = rank).\"\"\"
        sim = self.sim; NT = sim.NT; ordr = self.ord; NK = len(RAW14)
        cats = sorted(set(sim.gcat[i] for i in self.items))
        js = {'day': day, 'nt': NT, 'nk': NK, 'mk': [sim.T[t]['mk'] for t in ordr],
              'review': [self.slot[t] == (day & 1) for t in ordr], 'siege': [False] * NT,
              'sh': [[int(max(0.0, sim.shelf[t][i])) for i in self.items] for t in ordr],
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
        if o.get('portdump', 0) and day in self._dump_days: self._dump_state(day)"""))
pairs.append(("""                    self.carts.append([day + int(max(1.0, L)), s, t, k, n, day, cost + fr])""",
"""                    self.carts.append([day + int(max(1.0, L)), s, t, k, n, day, cost + fr])
                    if self._dump is not None: self._dump['lots'].append([self.rank[s], self.rank[t], k, n, cost, fr])"""))
pairs.append(("""        # PayCarters: land freight to the source villages (by people, top 8 + rest to the largest)
        for s, x in pend.items():""",
"""        if self._dump is not None:
            pth = os.path.join(o.get('portdir', '.'), 'snap_%d_%d.json' % (int(o.get('seed_tag', 0)), day))
            json.dump(self._dump, open(pth, 'w', encoding='utf-8')); self._dump = None
        # PayCarters: land freight to the source villages (by people, top 8 + rest to the largest)
        for s, x in pend.items():"""))
pairs.append(("""        self.mar_pct = []; self.cof_short = []""", """        self.mar_pct = []; self.cof_short = []
        self._dump = None; D0 = o['D']; self._dump_days = set(D0 + k for k in (20, 23, 27, 30, 34, 37, 41, 44, 48))"""))
rep(P, pairs)
