# round 2: land balance of a good in the sim at the Apply Plan (supply to market of villages by trade-town land + town fields vs paid D-term + castles)
import json, os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r3port as R
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(R.DEF)
g = sys.argv[1] if len(sys.argv) > 1 else 'grain'
s = R.R3Sim(W, o, seed=1, plan_on=os.path.join(R.H3OUT, 's1_on.csv'), plan_off=os.path.join(R.H3OUT, 'off.csv'), init='A')
i = s.GI[g]
S_ = collections.defaultdict(float); D_ = collections.defaultdict(float); C_ = collections.defaultdict(float); towns = collections.defaultdict(list)
for k, v in enumerate(s.Vl):
    L = s.town_land[v['trade']]; S_[(v['mk'], L)] += s.out[k][i]
for t in range(s.NT):
    L = s.town_land[t]; m = s.T[t]['mk']; S_[(m, L)] += s.tf[t][i]; D_[(m, L)] += s.dp[t][i]; towns[(m, L)].append(s.T[t]['name'])
for c in s.C:
    # castle demand: by the castle's trade town if known
    t = c.get('town'); 
    if t is None: continue
    C_[(s.T[t]['mk'], s.town_land[t])] += c['dem'][i]
print('%s: land S (to market) vs D-term + castles' % g)
for key in sorted(set(S_) | set(D_)):
    sv = S_[key]; dv = D_[key] + C_[key]
    print('  %s %-14s S %7.0f D %7.0f  S-D %+7.0f  S/D %5.2f  %s' % ('WE'[key[0]], key[1], sv, dv, sv - dv, sv / dv if dv else 0, ','.join(towns[key])[:80]))
print(list(s.C[0].keys()))
