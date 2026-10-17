# KS-H money cap: which destinations are blocked, their coffers vs merchants' reserve
import json, os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(R.DEF)
ob = dict(o); ob.update(R.BASE_OFF)
b = R.scenario(W, ob, 1, None, None, 'split')
s = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, sys.argv[1] if len(sys.argv) > 1 else 'on_an1.csv'), plan_off=os.path.join(R.H3OUT, 'off.csv'), base_flow=b['_flow'])
poor = collections.Counter(); order = collections.Counter()
ks = s.ksh
orig = ks.daily
def daily(day):
    p0 = dict(ks.c)
    orig(day)
ks.daily = daily
rows = {}
while s.day < o['D'] + 49:
    s.step()
    if s.day in (o['D'] + 1, o['D'] + 21, o['D'] + 48):
        rows[s.day] = [(s.T[t]['name'], round(s.town_gold(t)), round(s.town_reserve(t)), round(s.town_gold_delta[t])) for t in range(s.NT)]
for d, r in rows.items():
    below = [x for x in r if x[1] < x[2]]
    print('day', d, 'towns with coffer < reserve:', len(below), sorted(below, key=lambda x: x[1] - x[2])[:12])
print('ksh counters', {k: round(v) for k, v in ks.c.items() if k in ('ordered', 'poor', 'bought', 'lots')})
