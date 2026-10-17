import json, sys, os, math, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import merged as M, sim as S
W = json.load(open(os.path.join(HERE, 'world.json'), encoding='utf-8'))
o = dict(M.DEF); o['ownfirst'] = 0
sim = M.MSim(W, {}, o, init='start', start=109118, seed=1); sim.setup()
for t in range(sim.NT):
    m = sim.T[t]['mk']
    for i in range(sim.NG): sim.shelf[t][i] = 7.14 * sim.start_idx[m][i] ** (-1 / 0.6) * sim.dterm(t, i)
while sim.day < 109146: sim.step()
i = sim.GI['grain']; c = sim.gcat[i]; K = sim.trade_mod
q = [t for t in range(sim.NT) if sim.T[t]['name'] == 'Qarth'][0]
T = 3.6
for s in range(sim.NT):
    if sim.T[s]['mk'] != 1: continue
    d = max(0, sim.dterm(s, i)); sur = sim.shelf[s][i] - 1.25 * 7.14 * 1.5 ** (-1 / 0.6) * d - 5
    if sur > 1 or sim.shelf[s][i] > 2000:
        print('%-16s shelf %7.0f D %6.1f sur %7.0f idx %.2f days->Qarth %s' % (sim.T[s]['name'], sim.shelf[s][i], d, sur, sim.idx[s][c], K.days[s][q]))
sim.step()   # 109147
print('day now', sim.day, 'slot', K.slot[q], 'inc', K.inc[q][i], 'shelf', sim.shelf[q][i])
print([ (x[0], sim.T[x[1]]['name'], x[4]) for x in K.ship if x[2]==q])
print('day used inside daily = sim.day at call time; step increments after; parity check', (sim.day-1) % 2)
sim.step()  # 109148 -> daily ran with day 109147 (odd)
o_=K.o; day=sim.day
offer={}
for t in range(sim.NT):
    if sim.T[t]['mk']!=1: continue
    d=max(0.0,sim.dterm(t,i)); Ts=7.14*K.tsi[t][i]**(-1/0.6)
    sur=sim.shelf[t][i]-o_['ksrc']*Ts*d-5
    if sur>1: offer[t]=sur*o_['offer']*2
best=None
Id=sim.idx[q][c]
for s,off in offer.items():
    L=K.days[s][q]
    if L<0 or L>30: continue
    cost=sim.idx[s][c]*(1+0.01*L)
    ok = cost*(1.03) <= Id
    print('%-14s off %7.0f L %s cost %.2f ok %s'%(sim.T[s]['name'],off,L,cost,ok))
print('Qarth idx', Id, 'H', sim.shelf[q][i]+K.inc[q][i], 'T*d', 3.6*sim.dterm(q,i))
